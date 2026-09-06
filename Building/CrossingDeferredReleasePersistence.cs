using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public static partial class CrossingPathBuilder
    {
        private struct DeferredNodeRelease
        {
            internal ushort Id;
            internal uint BuildIndex;
            internal string PrefabName;
            internal Vector3 Position;
        }
        private static readonly Queue<DeferredNodeRelease> DeferredNodeReleases = new Queue<DeferredNodeRelease>();
        private static readonly HashSet<ushort> DeferredNodeReleaseIds = new HashSet<ushort>();

        private static void QueueDeferredNodeRelease(ushort nodeId)
        {
            NetManager manager = NetManager.instance;
            if (manager == null || nodeId == 0 || nodeId >= manager.m_nodes.m_buffer.Length)
                return;
            ref NetNode node = ref manager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0 || !IsManagedPathPrefab(node.Info))
                return;
            DeferredNodeRelease identity;
            if (!BuiltNodeIdentities.TryGetValue(nodeId, out identity) || node.m_buildIndex != identity.BuildIndex
                || node.Info.name != identity.PrefabName || node.m_position != identity.Position)
                return;
            lock (DeferredNetworkReleaseGate)
            {
                if (DeferredNodeReleaseIds.Add(nodeId))
                    DeferredNodeReleases.Enqueue(new DeferredNodeRelease {
                        Id = nodeId, BuildIndex = node.m_buildIndex,
                        PrefabName = node.Info.name, Position = node.m_position });
            }
        }

        // Called under the queue gate on the native simulation callback after paths drain.
        private static void ProcessDeferredNodeRelease()
        {
            if (DeferredNodeReleases.Count == 0)
                return;
            DeferredNodeRelease release = DeferredNodeReleases.Peek();
            bool retry = false;
            try
            {
                NetManager manager = NetManager.instance;
                if (manager == null)
                {
                    retry = true;
                    return;
                }
                if (release.Id >= manager.m_nodes.m_buffer.Length)
                    return;
                ref NetNode node = ref manager.m_nodes.m_buffer[release.Id];
                if ((node.m_flags & NetNode.Flags.Created) != 0
                    && node.m_buildIndex == release.BuildIndex && node.Info != null
                    && node.Info.name == release.PrefabName && node.m_position == release.Position
                    && node.CountSegments() == 0)
                    retry = !SafeReleaseUnusedManagedNode(manager, release.Id);
            }
            finally
            {
                DeferredNodeReleases.Dequeue();
                if (retry)
                {
                    DeferredNodeReleases.Enqueue(release);
                    _deferredReleaseRetryFrames = 256;
                }
                else
                    DeferredNodeReleaseIds.Remove(release.Id);
            }
        }
        // Saving while paused must not forget already-authorised path cleanup.
        // Native records are still released only by the existing simulation callback.
        internal static byte[] SerializeDeferredNetworkReleases()
        {
            lock (DeferredNetworkReleaseGate)
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(1);
                writer.Write(DeferredNetworkReleases.Count + (_activeDeferredNetworkRelease.HasValue ? 1 : 0));
                if (_activeDeferredNetworkRelease.HasValue)
                    WriteDeferredNetworkRelease(writer, _activeDeferredNetworkRelease.Value);
                foreach (DeferredNetworkRelease release in DeferredNetworkReleases)
                    WriteDeferredNetworkRelease(writer, release);
                writer.Write(DeferredNodeReleases.Count);
                foreach (DeferredNodeRelease release in DeferredNodeReleases)
                {
                    writer.Write(release.Id);
                    writer.Write(release.BuildIndex);
                    writer.Write(release.PrefabName);
                    writer.Write(release.Position.x);
                    writer.Write(release.Position.y);
                    writer.Write(release.Position.z);
                }
                return stream.ToArray();
            }
        }

        private static void WriteDeferredNetworkRelease(BinaryWriter writer, DeferredNetworkRelease release)
        {
            writer.Write(release.SegmentId);
            writer.Write(release.BuildIndex);
            writer.Write(release.Lanes);
            writer.Write(release.StartNode);
            writer.Write(release.EndNode);
            writer.Write(release.PrefabName ?? string.Empty);
        }

        internal static void RestoreDeferredNetworkReleases(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            using (MemoryStream stream = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != 1)
                    throw new InvalidDataException("Unsupported pending path cleanup version.");
                int count = reader.ReadInt32();
                if (count < 0 || count > ushort.MaxValue || count > (stream.Length - stream.Position) / 15)
                    throw new InvalidDataException("Invalid pending path cleanup count.");

                List<DeferredNetworkRelease> restored = new List<DeferredNetworkRelease>(count);
                HashSet<ushort> ids = new HashSet<ushort>();
                for (int i = 0; i < count; i++)
                {
                    ushort id = reader.ReadUInt16();
                    uint build = reader.ReadUInt32();
                    uint lanes = reader.ReadUInt32();
                    ushort start = reader.ReadUInt16();
                    ushort end = reader.ReadUInt16();
                    string prefab = reader.ReadString();
                    if (id == 0 || start == 0 || end == 0 || string.IsNullOrEmpty(prefab)
                        || !ids.Add(id))
                        throw new InvalidDataException("Invalid pending path cleanup identity.");
                    restored.Add(new DeferredNetworkRelease(id, "saved-path-cleanup", build,
                        lanes, start, end, prefab));
                }
                int nodeCount = reader.ReadInt32();
                if (nodeCount < 0 || nodeCount > ushort.MaxValue || nodeCount > (stream.Length - stream.Position) / 19)
                    throw new InvalidDataException("Invalid pending node cleanup count.");
                List<DeferredNodeRelease> nodes = new List<DeferredNodeRelease>(nodeCount);
                HashSet<ushort> nodeIds = new HashSet<ushort>();
                for (int i = 0; i < nodeCount; i++)
                {
                    DeferredNodeRelease node = new DeferredNodeRelease {
                        Id = reader.ReadUInt16(), BuildIndex = reader.ReadUInt32(),
                        PrefabName = reader.ReadString(),
                        Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()) };
                    if (node.Id == 0 || string.IsNullOrEmpty(node.PrefabName) || !nodeIds.Add(node.Id)
                        || float.IsNaN(node.Position.x) || float.IsInfinity(node.Position.x)
                        || float.IsNaN(node.Position.y) || float.IsInfinity(node.Position.y)
                        || float.IsNaN(node.Position.z) || float.IsInfinity(node.Position.z))
                        throw new InvalidDataException("Invalid pending node cleanup identity.");
                    nodes.Add(node);
                }
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Unexpected pending path cleanup data.");

                lock (DeferredNetworkReleaseGate)
                {
                    foreach (DeferredNetworkRelease release in restored)
                    {
                        if (DeferredNetworkReleaseIds.Add(release.SegmentId))
                            DeferredNetworkReleases.Enqueue(release);
                    }
                    foreach (DeferredNodeRelease release in nodes)
                    {
                        if (DeferredNodeReleaseIds.Add(release.Id))
                            DeferredNodeReleases.Enqueue(release);
                    }
                }
            }
        }
    }
}
