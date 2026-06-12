using System;
using System.Collections.Generic;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public static class GradeSeparatedVanillaCrossingSuppression
    {
        private const float SuppressionDistance = 5f;
        private const int AssetBufferSize = 1024;

        private static readonly CrossingPlacementAsset[] AssetBuffer = new CrossingPlacementAsset[AssetBufferSize];
        private static readonly List<SuppressionSpan> SpanBuffer = new List<SuppressionSpan>(AssetBufferSize);
        private static readonly HashSet<ushort> TargetNodes = new HashSet<ushort>();
        private static readonly HashSet<SegmentEndKey> TargetSegmentEnds = new HashSet<SegmentEndKey>();
        private static readonly Dictionary<SegmentEndKey, SuppressedEndSnapshot> SuppressedEndSnapshots = new Dictionary<SegmentEndKey, SuppressedEndSnapshot>();
        private static readonly List<SegmentEndKey> RestoreBuffer = new List<SegmentEndKey>();

        private struct SuppressionSpan
        {
            public readonly Vector3 First;
            public readonly Vector3 Second;

            public SuppressionSpan(Vector3 first, Vector3 second)
            {
                First = first;
                Second = second;
            }
        }

        private struct SegmentEndKey : IEquatable<SegmentEndKey>
        {
            public readonly ushort SegmentId;
            public readonly bool StartNode;

            public SegmentEndKey(ushort segmentId, bool startNode)
            {
                SegmentId = segmentId;
                StartNode = startNode;
            }

            public bool Equals(SegmentEndKey other)
            {
                return SegmentId == other.SegmentId && StartNode == other.StartNode;
            }

            public override bool Equals(object obj)
            {
                return obj is SegmentEndKey && Equals((SegmentEndKey)obj);
            }

            public override int GetHashCode()
            {
                return (SegmentId * 397) ^ (StartNode ? 1 : 0);
            }
        }

        private struct SuppressedEndSnapshot
        {
            public readonly bool OriginalCrossingAllowed;
            public readonly bool HasOriginalTrafficManagerAllowed;
            public readonly bool OriginalTrafficManagerAllowed;
            public bool TrafficManagerBanApplied;

            public SuppressedEndSnapshot(
                bool originalCrossingAllowed,
                bool hasOriginalTrafficManagerAllowed,
                bool originalTrafficManagerAllowed)
            {
                OriginalCrossingAllowed = originalCrossingAllowed;
                HasOriginalTrafficManagerAllowed = hasOriginalTrafficManagerAllowed;
                OriginalTrafficManagerAllowed = originalTrafficManagerAllowed;
                TrafficManagerBanApplied = false;
            }
        }

        public static int Reconcile(bool log, string reason)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null)
                return 0;

            TargetNodes.Clear();
            TargetSegmentEnds.Clear();
            int assetCount;
            int spanCount = BuildSuppressionSpans(out assetCount);
            int scannedEnds = 0;
            int matchedEnds = 0;
            if (spanCount > 0)
            {
                ScanNetworkForTargetNodes(netManager, ref scannedEnds, ref matchedEnds);
            }

            int tmpeRestores;
            int restored = RestoreReleasedSegmentEnds(netManager, out tmpeRestores);
            int tmpeBans;
            int tmpeAlreadyBanned;
            int changed = ApplyTargetSegmentEnds(netManager, out tmpeBans, out tmpeAlreadyBanned);
            if (log)
            {
                bool tmpeAvailable = PedestrianCrossingToolkitState.TrafficManagerInteropAllowed
                                     && TrafficManagerPedestrianCrossingIntegration.IsAvailable;
                Debug.Log("[PedestrianCrossingToolkit] Built vanilla surface suppression scan: reason="
                          + reason
                          + " assets="
                          + assetCount
                          + " spans="
                          + spanCount
                          + " scannedEnds="
                          + scannedEnds
                          + " matchedEnds="
                          + matchedEnds
                          + " targetNodes="
                          + TargetNodes.Count
                          + " targetEnds="
                          + TargetSegmentEnds.Count
                          + " changedEnds="
                          + changed
                          + " restoredEnds="
                          + restored
                          + " tmpe="
                          + tmpeAvailable
                          + " tmpeBans="
                          + tmpeBans
                          + " tmpeAlreadyBanned="
                          + tmpeAlreadyBanned
                          + " tmpeRestores="
                          + tmpeRestores);
            }

            return changed;
        }

        public static int Clear(string reason)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null)
            {
                SuppressedEndSnapshots.Clear();
                return 0;
            }

            RestoreBuffer.Clear();
            foreach (KeyValuePair<SegmentEndKey, SuppressedEndSnapshot> snapshot in SuppressedEndSnapshots)
                RestoreBuffer.Add(snapshot.Key);

            int restored = 0;
            int tmpeRestores = 0;
            for (int i = 0; i < RestoreBuffer.Count; i++)
            {
                SegmentEndKey key = RestoreBuffer[i];
                SuppressedEndSnapshot snapshot;
                if (!SuppressedEndSnapshots.TryGetValue(key, out snapshot))
                    continue;

                if (snapshot.TrafficManagerBanApplied
                    && TrafficManagerPedestrianCrossingIntegration.SetPedestrianCrossingAllowed(
                        key.SegmentId,
                        key.StartNode,
                        GetTrafficManagerRestoreAllowed(snapshot)))
                {
                    tmpeRestores++;
                }

                if (snapshot.OriginalCrossingAllowed && SetCrossingFlag(netManager, key, true))
                    restored++;
            }

            int tracked = SuppressedEndSnapshots.Count;
            SuppressedEndSnapshots.Clear();
            TargetNodes.Clear();
            TargetSegmentEnds.Clear();
            if (tracked > 0 || restored > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Restored grade-separated vanilla crossing suppression: reason="
                          + reason
                          + " trackedEnds="
                          + tracked
                          + " restoredEnds="
                          + restored
                          + " tmpe="
                          + (PedestrianCrossingToolkitState.TrafficManagerInteropAllowed
                             && TrafficManagerPedestrianCrossingIntegration.IsAvailable)
                          + " tmpeRestores="
                          + tmpeRestores);
            }

            return restored;
        }

        public static int ForgetStateForLevelUnload(string reason)
        {
            int tracked = SuppressedEndSnapshots.Count;
            int targets = TargetSegmentEnds.Count;
            SuppressedEndSnapshots.Clear();
            TargetNodes.Clear();
            TargetSegmentEnds.Clear();
            RestoreBuffer.Clear();
            if (tracked > 0 || targets > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Grade-separated suppression unload state forgotten without restoring network flags: reason="
                          + reason
                          + " trackedEnds="
                          + tracked
                          + " targetEnds="
                          + targets);
            }

            return tracked;
        }

        public static bool HasActiveSuppressionForAsset(CrossingPlacementAsset asset)
        {
            if (!asset.Plan.IsValid
                || !GradeSeparatedPlacementGeometryResolver.IsGradeSeparated(asset.Plan.ApplicationKind)
                || !RoadPlacementRules.IsRoadGradeSeparatedPlacementTarget(asset.Placement.SegmentId)
                || SuppressedEndSnapshots.Count == 0)
            {
                return false;
            }

            GradeSeparatedPlacementGeometry geometry;
            if (!GradeSeparatedPlacementGeometryResolver.TryBuild(asset, out geometry))
                return false;

            NetManager netManager = NetManager.instance;
            if (netManager == null)
                return false;

            foreach (KeyValuePair<SegmentEndKey, SuppressedEndSnapshot> snapshot in SuppressedEndSnapshots)
            {
                SegmentEndKey key = snapshot.Key;
                if (key.SegmentId == 0 || key.SegmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[key.SegmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                    continue;

                Vector3 crossingPosition;
                if (TryCalculateSegmentEndCrossingPosition(key.SegmentId, ref segment, key.StartNode, out crossingPosition)
                    && HorizontalPointSegmentDistanceSqr(crossingPosition, geometry.FirstDeckPosition, geometry.SecondDeckPosition) <= SuppressionDistance * SuppressionDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private static int BuildSuppressionSpans(out int assetCount)
        {
            SpanBuffer.Clear();
            assetCount = 0;
            int copied = CrossingPlacementRegistry.CopyTo(AssetBuffer);
            for (int i = 0; i < copied; i++)
            {
                CrossingPlacementAsset asset = AssetBuffer[i];
                if (!asset.Plan.IsValid || !GradeSeparatedPlacementGeometryResolver.IsGradeSeparated(asset.Plan.ApplicationKind))
                    continue;

                assetCount++;
                if (!RoadPlacementRules.IsRoadGradeSeparatedPlacementTarget(asset.Placement.SegmentId))
                    continue;

                GradeSeparatedPlacementGeometry geometry;
                if (!GradeSeparatedPlacementGeometryResolver.TryBuild(asset, out geometry))
                    continue;

                SpanBuffer.Add(new SuppressionSpan(geometry.FirstDeckPosition, geometry.SecondDeckPosition));
            }

            return SpanBuffer.Count;
        }

        private static void ScanNetworkForTargetNodes(NetManager netManager, ref int scannedEnds, ref int matchedEnds)
        {
            for (ushort segmentId = 1; segmentId < netManager.m_segments.m_size; segmentId++)
            {
                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !IsSuppressibleRoadSegment(segment.Info))
                    continue;

                ScanSegmentEndForTargetNode(netManager, segmentId, ref segment, true, ref scannedEnds, ref matchedEnds);
                ScanSegmentEndForTargetNode(netManager, segmentId, ref segment, false, ref scannedEnds, ref matchedEnds);
            }
        }

        private static void ScanSegmentEndForTargetNode(
            NetManager netManager,
            ushort segmentId,
            ref NetSegment segment,
            bool startNode,
            ref int scannedEnds,
            ref int matchedEnds)
        {
            Vector3 crossingPosition;
            if (!TryCalculateSegmentEndCrossingPosition(segmentId, ref segment, startNode, out crossingPosition))
                return;

            scannedEnds++;
            if (!IsWithinAnySuppressionSpan(crossingPosition))
                return;

            ushort nodeId = startNode ? segment.m_startNode : segment.m_endNode;
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return;

            TargetNodes.Add(nodeId);
            TargetSegmentEnds.Add(new SegmentEndKey(segmentId, startNode));
            matchedEnds++;
        }

        private static int RestoreReleasedSegmentEnds(NetManager netManager, out int tmpeRestores)
        {
            RestoreBuffer.Clear();
            foreach (KeyValuePair<SegmentEndKey, SuppressedEndSnapshot> snapshot in SuppressedEndSnapshots)
            {
                if (!TargetSegmentEnds.Contains(snapshot.Key))
                    RestoreBuffer.Add(snapshot.Key);
            }

            int restored = 0;
            tmpeRestores = 0;
            for (int i = 0; i < RestoreBuffer.Count; i++)
            {
                SegmentEndKey key = RestoreBuffer[i];
                SuppressedEndSnapshot snapshot;
                if (!SuppressedEndSnapshots.TryGetValue(key, out snapshot))
                    continue;

                if (snapshot.TrafficManagerBanApplied
                    && TrafficManagerPedestrianCrossingIntegration.SetPedestrianCrossingAllowed(
                        key.SegmentId,
                        key.StartNode,
                        GetTrafficManagerRestoreAllowed(snapshot)))
                {
                    tmpeRestores++;
                }

                if (snapshot.OriginalCrossingAllowed && SetCrossingFlag(netManager, key, true))
                    restored++;

                SuppressedEndSnapshots.Remove(key);
            }

            return restored;
        }

        private static int ApplyTargetSegmentEnds(NetManager netManager, out int tmpeBans, out int tmpeAlreadyBanned)
        {
            int changed = 0;
            tmpeBans = 0;
            tmpeAlreadyBanned = 0;
            foreach (SegmentEndKey key in TargetSegmentEnds)
            {
                if (key.SegmentId == 0 || key.SegmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[key.SegmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !IsSuppressibleRoadSegment(segment.Info))
                    continue;

                bool currentlyAllowed = IsCrossingAllowed(ref segment, key.StartNode);
                SuppressedEndSnapshot snapshot;
                bool alreadyTracked = SuppressedEndSnapshots.TryGetValue(key, out snapshot);
                if (!alreadyTracked)
                {
                    bool originalTrafficManagerAllowed;
                    bool hasOriginalTrafficManagerAllowed =
                        TrafficManagerPedestrianCrossingIntegration.TryGetPedestrianCrossingAllowed(
                            key.SegmentId,
                            key.StartNode,
                            out originalTrafficManagerAllowed);
                    snapshot = new SuppressedEndSnapshot(
                        currentlyAllowed,
                        hasOriginalTrafficManagerAllowed,
                        originalTrafficManagerAllowed);
                }

                if (SetCrossingFlag(netManager, key, false))
                    changed++;

                if (!snapshot.TrafficManagerBanApplied)
                {
                    bool trafficManagerAllowed;
                    bool hasTrafficManagerState =
                        TrafficManagerPedestrianCrossingIntegration.TryGetPedestrianCrossingAllowed(
                            key.SegmentId,
                            key.StartNode,
                            out trafficManagerAllowed);
                    bool trafficManagerChanged =
                        TrafficManagerPedestrianCrossingIntegration.SetPedestrianCrossingAllowed(
                            key.SegmentId,
                            key.StartNode,
                            false);
                    if (trafficManagerChanged || (hasTrafficManagerState && !trafficManagerAllowed))
                    {
                        snapshot.TrafficManagerBanApplied = true;
                        if (trafficManagerChanged)
                            tmpeBans++;
                        else
                            tmpeAlreadyBanned++;
                    }
                }

                SuppressedEndSnapshots[key] = snapshot;
            }

            return changed;
        }

        private static bool GetTrafficManagerRestoreAllowed(SuppressedEndSnapshot snapshot)
        {
            return snapshot.HasOriginalTrafficManagerAllowed
                ? snapshot.OriginalTrafficManagerAllowed
                : snapshot.OriginalCrossingAllowed;
        }

        private static bool SetCrossingFlag(NetManager netManager, SegmentEndKey key, bool enabled)
        {
            if (key.SegmentId == 0 || key.SegmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[key.SegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return false;

            NetSegment.Flags flag = key.StartNode ? NetSegment.Flags.CrossingStart : NetSegment.Flags.CrossingEnd;
            bool currentlyEnabled = (segment.m_flags & flag) != 0;
            if (currentlyEnabled == enabled)
                return false;

            if (enabled)
                segment.m_flags |= flag;
            else
                segment.m_flags &= ~flag;

            netManager.UpdateSegmentFlags(key.SegmentId);
            netManager.UpdateSegmentRenderer(key.SegmentId, true);

            ushort nodeId = key.StartNode ? segment.m_startNode : segment.m_endNode;
            if (nodeId != 0 && nodeId < netManager.m_nodes.m_size)
                netManager.UpdateNodeRenderer(nodeId, true);

            return true;
        }

        private static bool IsCrossingAllowed(ref NetSegment segment, bool startNode)
        {
            NetSegment.Flags flag = startNode ? NetSegment.Flags.CrossingStart : NetSegment.Flags.CrossingEnd;
            return (segment.m_flags & flag) != 0;
        }

        private static bool TryCalculateSegmentEndCrossingPosition(ushort segmentId, ref NetSegment segment, bool startNode, out Vector3 position)
        {
            position = Vector3.zero;
            Vector3 left;
            Vector3 right;
            Vector3 direction;
            bool smooth;
            bool endSegment = !startNode;
            segment.CalculateCorner(segmentId, true, !endSegment, true, out left, out direction, out smooth);
            segment.CalculateCorner(segmentId, true, !endSegment, false, out right, out direction, out smooth);
            position = (left + right) * 0.5f;
            return position != Vector3.zero;
        }

        private static bool IsWithinAnySuppressionSpan(Vector3 crossingPosition)
        {
            float maxDistanceSqr = SuppressionDistance * SuppressionDistance;
            for (int i = 0; i < SpanBuffer.Count; i++)
            {
                SuppressionSpan span = SpanBuffer[i];
                if (HorizontalPointSegmentDistanceSqr(crossingPosition, span.First, span.Second) <= maxDistanceSqr)
                    return true;
            }

            return false;
        }

        private static float HorizontalPointSegmentDistanceSqr(Vector3 point, Vector3 first, Vector3 second)
        {
            float sx = second.x - first.x;
            float sz = second.z - first.z;
            float lengthSqr = (sx * sx) + (sz * sz);
            if (lengthSqr <= 0.0001f)
            {
                float dx = point.x - first.x;
                float dz = point.z - first.z;
                return (dx * dx) + (dz * dz);
            }

            float t = (((point.x - first.x) * sx) + ((point.z - first.z) * sz)) / lengthSqr;
            t = Mathf.Clamp01(t);
            float closestX = first.x + (sx * t);
            float closestZ = first.z + (sz * t);
            float cx = point.x - closestX;
            float cz = point.z - closestZ;
            return (cx * cx) + (cz * cz);
        }

        private static bool IsSuppressibleRoadSegment(NetInfo info)
        {
            return info != null && info.m_netAI is RoadBaseAI && HasVehicleLane(info);
        }

        private static bool HasVehicleLane(NetInfo info)
        {
            if (info == null || info.m_lanes == null)
                return false;

            for (int i = 0; i < info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = info.m_lanes[i];
                if (lane != null && (lane.m_laneType & NetInfo.LaneType.Vehicle) != 0)
                    return true;
            }

            return false;
        }
    }
}
