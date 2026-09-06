using System;
using System.Collections.Generic;
using System.IO;

namespace PedestrianCrossingToolkit
{
    public static partial class GradeSeparatedVanillaCrossingSuppression
    {
        // Capture the original permission, never infer it from an already suppressed save.
        // Loading only restores our ledger; existing reconciliation owns native changes.
        internal static byte[] SerializePermissions()
        {
            lock (PermissionGate)
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(1);
                writer.Write(SuppressedEndSnapshots.Count);
                foreach (KeyValuePair<SegmentEndKey, SuppressedEndSnapshot> pair in SuppressedEndSnapshots)
                {
                    writer.Write(pair.Key.SegmentId);
                    writer.Write(pair.Key.StartNode);
                    SuppressedEndSnapshot value = pair.Value;
                    writer.Write(value.BuildIndex);
                    writer.Write(value.StartNode);
                    writer.Write(value.EndNode);
                    writer.Write(value.PrefabName);
                    writer.Write(value.OriginalCrossingAllowed);
                    writer.Write(value.HasOriginalTrafficManagerAllowed);
                    writer.Write(value.OriginalTrafficManagerAllowed);
                    writer.Write(value.TrafficManagerBanApplied);
                }
                return stream.ToArray();
            }
        }

        internal static void RestoreSavedPermissions(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            Dictionary<SegmentEndKey, SuppressedEndSnapshot> restored =
                new Dictionary<SegmentEndKey, SuppressedEndSnapshot>();
            using (MemoryStream stream = new MemoryStream(data, false))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != 1) throw new InvalidDataException("Unknown crossing permission schema.");
                int count = reader.ReadInt32();
                if (count < 0 || count > 131070 || count > (stream.Length - stream.Position) / 16)
                    throw new InvalidDataException("Invalid crossing permission count.");
                for (int i = 0; i < count; i++)
                {
                    SegmentEndKey key = new SegmentEndKey(reader.ReadUInt16(), reader.ReadBoolean());
                    SuppressedEndSnapshot value = new SuppressedEndSnapshot();
                    value.BuildIndex = reader.ReadUInt32();
                    value.StartNode = reader.ReadUInt16();
                    value.EndNode = reader.ReadUInt16();
                    value.PrefabName = reader.ReadString();
                    value.OriginalCrossingAllowed = reader.ReadBoolean();
                    value.HasOriginalTrafficManagerAllowed = reader.ReadBoolean();
                    value.OriginalTrafficManagerAllowed = reader.ReadBoolean();
                    value.TrafficManagerBanApplied = reader.ReadBoolean();
                    if (key.SegmentId == 0 || value.StartNode == 0 || value.EndNode == 0
                        || string.IsNullOrEmpty(value.PrefabName) || restored.ContainsKey(key))
                        throw new InvalidDataException("Invalid crossing permission identity.");
                    restored.Add(key, value);
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Trailing crossing permission data.");
            }
            lock (PermissionGate)
            {
                SuppressedEndSnapshots.Clear();
                foreach (KeyValuePair<SegmentEndKey, SuppressedEndSnapshot> pair in restored)
                    SuppressedEndSnapshots.Add(pair.Key, pair.Value);
            }
        }
    }
}
