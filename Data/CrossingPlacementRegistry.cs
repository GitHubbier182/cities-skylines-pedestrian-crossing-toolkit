using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public static class CrossingPlacementRegistry
    {
        private static readonly List<CrossingPlacementAsset> Assets = new List<CrossingPlacementAsset>();
        private const int SerializationVersion = 7;
        private static int _nextId = 1;
        private static bool _autoRebuildBuiltStructures;
        private const float SameLocationSegmentTolerance = 0.001f;
        private const float SameLocationDistanceTolerance = 4f;
        private const float RemovalDistanceTolerance = 18f;
        private const float CrossingReplacementFootprintHalfWidth = 10f;
        private const float CrossingReplacementAccessRadius = 6f;
        private static readonly CrossingLandingAccessAssetWorkOrder[] AccessAssetBuffer = new CrossingLandingAccessAssetWorkOrder[2048];
        private static int _revision;

        public static int Count
        {
            get { return Assets.Count; }
        }

        public static int Revision
        {
            get { return _revision; }
        }

        public static bool AutoRebuildBuiltStructures
        {
            get { return _autoRebuildBuiltStructures; }
        }

        public static void SetAutoRebuildBuiltStructures(bool enabled)
        {
            bool nextValue = enabled && Assets.Count > 0;
            if (_autoRebuildBuiltStructures == nextValue)
                return;

            _autoRebuildBuiltStructures = nextValue;
            Debug.Log("[PedestrianCrossingToolkit] Built structure auto-rebuild changed: enabled="
                      + _autoRebuildBuiltStructures
                      + " pending="
                      + Assets.Count);
        }

        public static CrossingPlacementAsset AddOrReplace(CrossingPlacementRecord placement, CrossingPlacementPlan plan, out CrossingPlacementAsset replaced, out bool didReplace)
        {
            return AddOrReplace(placement, plan, SignalRoadStateSnapshot.Empty, out replaced, out didReplace);
        }

        public static CrossingPlacementAsset AddOrReplace(CrossingPlacementRecord placement, CrossingPlacementPlan plan, SignalRoadStateSnapshot signalRoadState, out CrossingPlacementAsset replaced, out bool didReplace)
        {
            CrossingPlacementAsset asset = new CrossingPlacementAsset(_nextId++, placement, plan, signalRoadState);
            int removedCount = RemoveMatchingAssets(placement, plan, out replaced);
            didReplace = removedCount > 0;
            Assets.Add(asset);
            _revision++;

            Debug.Log("[PedestrianCrossingToolkit] Pending asset added: id=" + asset.Id
                      + " mode=" + placement.Mode
                      + " segment=" + placement.SegmentId
                      + " replaced=" + didReplace
                      + " replacedAsset=" + replaced.Id
                      + " removedDuplicates=" + removedCount
                      + " count=" + Assets.Count);
            return asset;
        }

        public static bool RemoveAt(CrossingPlacementRecord placement, out CrossingPlacementAsset asset)
        {
            int removed = RemoveRemovalMatches(placement, out asset);
            if (removed <= 0)
            {
                asset = CrossingPlacementAsset.None;
                return false;
            }

            Debug.Log("[PedestrianCrossingToolkit] Pending assets removed at placement: newestRemoved="
                      + asset.Id
                      + " removed="
                      + removed
                      + " count="
                      + Assets.Count);
            _revision++;
            return true;
        }

        public static bool RemoveById(int assetId, out CrossingPlacementAsset asset)
        {
            for (int i = Assets.Count - 1; i >= 0; i--)
            {
                if (Assets[i].Id != assetId)
                    continue;

                asset = Assets[i];
                Assets.RemoveAt(i);
                _revision++;
                Debug.Log("[PedestrianCrossingToolkit] Pending asset removed by id: id="
                          + asset.Id
                          + " mode="
                          + asset.Placement.Mode
                          + " count="
                          + Assets.Count);
                return true;
            }

            asset = CrossingPlacementAsset.None;
            return false;
        }

        public static void Reset()
        {
            Assets.Clear();
            _nextId = 1;
            _autoRebuildBuiltStructures = false;
            _revision++;
        }

        public static bool RemoveLast(out CrossingPlacementAsset asset)
        {
            int index = Assets.Count - 1;
            if (index < 0)
            {
                asset = CrossingPlacementAsset.None;
                return false;
            }

            asset = Assets[index];
            Assets.RemoveAt(index);
            _revision++;
            return true;
        }

        public static bool TryGetLast(out CrossingPlacementAsset asset)
        {
            int index = Assets.Count - 1;
            if (index < 0)
            {
                asset = CrossingPlacementAsset.None;
                return false;
            }

            asset = Assets[index];
            return true;
        }

        public static int CopyTo(CrossingPlacementAsset[] buffer)
        {
            int count = Mathf.Min(buffer.Length, Assets.Count);
            for (int i = 0; i < count; i++)
                buffer[i] = Assets[i];

            return count;
        }

        public static bool TryGetAssetById(int assetId, out CrossingPlacementAsset asset)
        {
            for (int i = 0; i < Assets.Count; i++)
            {
                if (Assets[i].Id != assetId)
                    continue;

                asset = Assets[i];
                return true;
            }

            asset = CrossingPlacementAsset.None;
            return false;
        }

        public static int CountAssetsTouchingSegment(ushort segmentId)
        {
            if (segmentId == 0)
                return 0;

            int count = 0;
            for (int i = 0; i < Assets.Count; i++)
            {
                if (IsAssetTouchingSegment(Assets[i], segmentId))
                    count++;
            }

            return count;
        }

        private static bool IsAssetTouchingSegment(CrossingPlacementAsset asset, ushort segmentId)
        {
            if (asset.Id == 0 || segmentId == 0)
                return false;

            if (asset.Placement.SegmentId == segmentId)
                return true;

            if (asset.Placement.HasSecondaryPoint && asset.Placement.SecondarySegmentId == segmentId)
                return true;

            ushort targetNodeId = asset.Plan.TargetNodeId != 0 ? asset.Plan.TargetNodeId : asset.Placement.TargetNodeId;
            return targetNodeId != 0 && IsSegmentAttachedToNode(segmentId, targetNodeId);
        }

        private static bool IsSegmentAttachedToNode(ushort segmentId, ushort nodeId)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null
                || segmentId == 0
                || nodeId == 0
                || segmentId >= netManager.m_segments.m_size
                || nodeId >= netManager.m_nodes.m_size)
            {
                return false;
            }

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return false;

            return segment.m_startNode == nodeId || segment.m_endNode == nodeId;
        }

        public static bool TryGetAssetAt(CrossingPlacementRecord placement, out CrossingPlacementAsset asset)
        {
            int index = FindMatchingAssetIndex(placement, placement.Mode == PedestrianToolMode.RemoveCrossing);
            if (index >= 0)
            {
                asset = Assets[index];
                return true;
            }

            asset = CrossingPlacementAsset.None;
            return false;
        }

        public static bool HasAssetAt(CrossingPlacementRecord placement)
        {
            return FindMatchingAssetIndex(placement, placement.Mode == PedestrianToolMode.RemoveCrossing) >= 0;
        }

        public static bool HasSameModeAssetAt(CrossingPlacementRecord placement)
        {
            for (int i = Assets.Count - 1; i >= 0; i--)
            {
                CrossingPlacementAsset asset = Assets[i];
                if (asset.Placement.Mode == placement.Mode
                    && IsSamePlacementLocation(asset.Placement, placement))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool CanCandidateReplace(CrossingPlacementAsset existing, CrossingPlacementRecord placement, CrossingPlacementPlan plan)
        {
            return IsCandidateTouchingAsset(existing, placement, plan);
        }

        public static bool TryGetAssetAt(CrossingPlacementRecord placement, CrossingPlacementPlan plan, out CrossingPlacementAsset asset)
        {
            for (int i = Assets.Count - 1; i >= 0; i--)
            {
                if (!IsCandidateTouchingAsset(Assets[i], placement, plan))
                    continue;

                asset = Assets[i];
                return true;
            }

            asset = CrossingPlacementAsset.None;
            return false;
        }

        public static byte[] Serialize()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(SerializationVersion);
                    writer.Write(_autoRebuildBuiltStructures && Assets.Count > 0);
                    writer.Write(Assets.Count);
                    for (int i = 0; i < Assets.Count; i++)
                    {
                        CrossingPlacementAsset asset = Assets[i];
                        CrossingPlacementRecord placement = asset.Placement;
                        writer.Write(asset.Id);
                        writer.Write((int)placement.Mode);
                        writer.Write(placement.SegmentId);
                        writer.Write(placement.SegmentPosition);
                        writer.Write(placement.WorldPosition.x);
                        writer.Write(placement.WorldPosition.y);
                        writer.Write(placement.WorldPosition.z);
                        writer.Write(placement.NearNode);
                        writer.Write(placement.SlotNumber);
                        writer.Write(placement.IsEndSegmentSlot);
                        writer.Write(placement.TargetNodeId);
                        writer.Write(placement.HasSecondaryPoint);
                        if (placement.HasSecondaryPoint)
                        {
                            writer.Write(placement.SecondarySegmentId);
                            writer.Write(placement.SecondarySegmentPosition);
                            writer.Write(placement.SecondaryWorldPosition.x);
                            writer.Write(placement.SecondaryWorldPosition.y);
                            writer.Write(placement.SecondaryWorldPosition.z);
                            writer.Write(placement.SecondaryNearNode);
                            writer.Write(placement.SecondarySlotNumber);
                            writer.Write(placement.SecondaryIsEndSegmentSlot);
                            writer.Write(placement.SecondaryTargetNodeId);
                        }

                        WriteSignalRoadStateSnapshot(writer, asset.SignalRoadState);
                    }
                }

                return stream.ToArray();
            }
        }

        public static int Restore(byte[] data)
        {
            Assets.Clear();
            _nextId = 1;
            _revision++;

            using (MemoryStream stream = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int version = reader.ReadInt32();
                if (version < 1 || version > SerializationVersion)
                {
                    Debug.LogWarning("[PedestrianCrossingToolkit] Ignoring unsupported pending crossing data version: " + version);
                    return 0;
                }

                _autoRebuildBuiltStructures = version == 1 || reader.ReadBoolean();
                int count = Math.Max(reader.ReadInt32(), 0);
                int maxId = 0;
                for (int i = 0; i < count; i++)
                {
                    int id = reader.ReadInt32();
                    PedestrianToolMode mode = (PedestrianToolMode)reader.ReadInt32();
                    ushort segmentId = reader.ReadUInt16();
                    float segmentPosition = reader.ReadSingle();
                    Vector3 worldPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    bool nearNode = reader.ReadBoolean();
                    int slotNumber = 0;
                    bool isEndSegmentSlot = false;
                    if (version >= 3)
                    {
                        slotNumber = reader.ReadInt32();
                        isEndSegmentSlot = reader.ReadBoolean();
                    }
                    ushort targetNodeId = 0;
                    if (version >= 4)
                    {
                        targetNodeId = reader.ReadUInt16();
                        if (version == 4)
                        {
                            reader.ReadSingle();
                            reader.ReadSingle();
                        }
                    }
                    bool hasSecondaryPoint = false;
                    ushort secondarySegmentId = 0;
                    float secondarySegmentPosition = 0f;
                    Vector3 secondaryWorldPosition = Vector3.zero;
                    bool secondaryNearNode = false;
                    int secondarySlotNumber = 0;
                    bool secondaryIsEndSegmentSlot = false;
                    ushort secondaryTargetNodeId = 0;
                    if (version >= 6)
                    {
                        hasSecondaryPoint = reader.ReadBoolean();
                        if (hasSecondaryPoint)
                        {
                            secondarySegmentId = reader.ReadUInt16();
                            secondarySegmentPosition = reader.ReadSingle();
                            secondaryWorldPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                            secondaryNearNode = reader.ReadBoolean();
                            secondarySlotNumber = reader.ReadInt32();
                            secondaryIsEndSegmentSlot = reader.ReadBoolean();
                            secondaryTargetNodeId = reader.ReadUInt16();
                        }
                    }

                    SignalRoadStateSnapshot signalRoadState = SignalRoadStateSnapshot.Empty;
                    if (version >= 7)
                        signalRoadState = ReadSignalRoadStateSnapshot(reader);

                    CrossingPlacementRecord placement = new CrossingPlacementRecord(
                        mode,
                        segmentId,
                        segmentPosition,
                        worldPosition,
                        true,
                        "Restored pending crossing.",
                        nearNode,
                        slotNumber,
                        isEndSegmentSlot,
                        targetNodeId,
                        hasSecondaryPoint,
                        secondarySegmentId,
                        secondarySegmentPosition,
                        secondaryWorldPosition,
                        secondaryNearNode,
                        secondarySlotNumber,
                        secondaryIsEndSegmentSlot,
                        secondaryTargetNodeId);
                    CrossingPlacementAsset restoredAsset = new CrossingPlacementAsset(id, placement, CrossingPlacementPlan.Invalid, signalRoadState);
                    CrossingPlacementAsset ignored;
                    RemoveMatchingAssets(placement, out ignored);
                    Assets.Add(restoredAsset);
                    _revision++;
                    if (id > maxId)
                        maxId = id;
                }

                _nextId = maxId + 1;
                PruneDuplicateAssets();
                if (Assets.Count == 0)
                    _autoRebuildBuiltStructures = false;

                Debug.Log("[PedestrianCrossingToolkit] Pending crossing restore flags: version="
                          + version
                          + " autoRebuildBuiltStructures="
                          + _autoRebuildBuiltStructures);
                return Assets.Count;
            }
        }

        public static int RebuildPlans()
        {
            int rebuilt = 0;
            int removedInvalid = 0;
            for (int i = Assets.Count - 1; i >= 0; i--)
            {
                CrossingPlacementAsset asset = Assets[i];
                CrossingPlacementPlan plan = CrossingPlacementPlanner.BuildExisting(asset);
                if (!plan.IsValid)
                {
                    Assets.RemoveAt(i);
                    _revision++;
                    removedInvalid++;
                    continue;
                }

                Assets[i] = new CrossingPlacementAsset(asset.Id, asset.Placement, plan, asset.SignalRoadState);
                rebuilt++;
            }

            if (Assets.Count > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Rebuilt pending crossing plans: rebuilt="
                          + rebuilt
                          + " removedInvalid="
                          + removedInvalid
                          + " count=" + Assets.Count);
            }
            else if (removedInvalid > 0)
            {
                _autoRebuildBuiltStructures = false;
                Debug.Log("[PedestrianCrossingToolkit] Removed invalid pending crossings during plan rebuild: removedInvalid="
                          + removedInvalid);
            }

            return rebuilt;
        }

        private static int FindMatchingAssetIndex(CrossingPlacementRecord placement, bool allowPhysicalFallback)
        {
            int bestPhysicalIndex = -1;
            float bestPhysicalDistance = float.MaxValue;
            for (int i = Assets.Count - 1; i >= 0; i--)
            {
                if (IsSamePlacementLocation(Assets[i].Placement, placement))
                    return i;

                if (!allowPhysicalFallback)
                    continue;

                float distance = GetRemovalDistanceSqr(Assets[i], placement);
                float limit = GetPhysicalRemovalDistanceLimit(Assets[i], placement);
                if (distance > limit * limit || distance >= bestPhysicalDistance)
                    continue;

                bestPhysicalDistance = distance;
                bestPhysicalIndex = i;
            }

            return bestPhysicalIndex;
        }

        private static int RemoveMatchingAssets(CrossingPlacementRecord placement, out CrossingPlacementAsset newestRemoved)
        {
            return RemoveMatchingAssets(placement, CrossingPlacementPlan.Invalid, out newestRemoved);
        }

        private static int RemoveMatchingAssets(CrossingPlacementRecord placement, CrossingPlacementPlan plan, out CrossingPlacementAsset newestRemoved)
        {
            int removed = 0;
            newestRemoved = CrossingPlacementAsset.None;
            for (int i = Assets.Count - 1; i >= 0; i--)
            {
                if (!IsCandidateTouchingAsset(Assets[i], placement, plan))
                    continue;

                if (removed == 0)
                    newestRemoved = Assets[i];

                Assets.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        private static int RemoveRemovalMatches(CrossingPlacementRecord placement, out CrossingPlacementAsset newestRemoved)
        {
            newestRemoved = CrossingPlacementAsset.None;
            int bestIndex = -1;
            float bestDistance = float.MaxValue;
            for (int i = Assets.Count - 1; i >= 0; i--)
            {
                CrossingPlacementAsset existing = Assets[i];
                bool match = IsSamePlacementLocation(existing.Placement, placement);
                float distance = 0f;
                if (!match)
                {
                    distance = GetRemovalDistanceSqr(existing, placement);
                    float limit = GetPhysicalRemovalDistanceLimit(existing, placement);
                    match = distance <= limit * limit;
                }

                if (!match)
                    continue;

                if (bestIndex >= 0 && distance >= bestDistance)
                    continue;

                bestIndex = i;
                bestDistance = distance;
            }

            if (bestIndex < 0)
                return 0;

            newestRemoved = Assets[bestIndex];
            Assets.RemoveAt(bestIndex);
            _revision++;
            return 1;
        }

        private static bool IsSameAssetLocation(CrossingPlacementAsset existing, CrossingPlacementRecord placement, CrossingPlacementPlan plan)
        {
            if (IsSubwayMode(existing.Placement.Mode) && IsSubwayMode(placement.Mode))
                return IsSameSubwayAssetLocation(existing, placement, plan);

            if (IsSamePlacementLocation(existing.Placement, placement))
                return true;

            if (!IsGradeSeparatedMode(existing.Placement.Mode) && !IsGradeSeparatedMode(placement.Mode))
                return false;

            if (existing.Placement.Mode == placement.Mode)
                return false;

            if (IsSharedGradeSeparatedApproach(existing, placement, plan))
                return true;

            return false;
        }

        private static bool IsSameSubwayAssetLocation(CrossingPlacementAsset existing, CrossingPlacementRecord placement, CrossingPlacementPlan plan)
        {
            Vector3 existingFirst;
            Vector3 existingSecond;
            bool existingHasRoute = TryGetSubwayRouteEndpoints(existing.Placement, existing.Plan, out existingFirst, out existingSecond);

            Vector3 candidateFirst;
            Vector3 candidateSecond;
            bool candidateHasRoute = TryGetSubwayRouteEndpoints(placement, plan, out candidateFirst, out candidateSecond);

            if (existingHasRoute || candidateHasRoute)
            {
                return existingHasRoute
                       && candidateHasRoute
                       && IsSameEndpointPair(existingFirst, existingSecond, candidateFirst, candidateSecond);
            }

            return IsSameRoadPlacementPoint(existing.Placement, placement);
        }

        private static bool TryGetSubwayRouteEndpoints(CrossingPlacementRecord placement, CrossingPlacementPlan plan, out Vector3 first, out Vector3 second)
        {
            first = Vector3.zero;
            second = Vector3.zero;

            if (placement.HasSecondaryPoint)
            {
                first = placement.WorldPosition;
                second = placement.SecondaryWorldPosition;
                return true;
            }

            if (plan.IsValid
                && IsSubwayMode(plan.Mode)
                && HorizontalSqrDistance(plan.LeftEdge, plan.RightEdge) > 0.01f)
            {
                first = plan.LeftEdge;
                second = plan.RightEdge;
                return true;
            }

            return false;
        }

        private static bool IsSameEndpointPair(Vector3 existingFirst, Vector3 existingSecond, Vector3 candidateFirst, Vector3 candidateSecond)
        {
            float toleranceSqr = SameLocationDistanceTolerance * SameLocationDistanceTolerance;
            return HorizontalSqrDistance(existingFirst, candidateFirst) <= toleranceSqr
                   && HorizontalSqrDistance(existingSecond, candidateSecond) <= toleranceSqr
                   || HorizontalSqrDistance(existingFirst, candidateSecond) <= toleranceSqr
                   && HorizontalSqrDistance(existingSecond, candidateFirst) <= toleranceSqr;
        }

        private static bool IsCandidateTouchingAsset(CrossingPlacementAsset existing, CrossingPlacementRecord placement, CrossingPlacementPlan plan)
        {
            if (IsSameAssetLocation(existing, placement, plan))
                return true;

            if (IsSubwayMode(existing.Placement.Mode) && IsSubwayMode(placement.Mode))
                return false;

            return IsPointInsideAssetFootprint(existing, placement.WorldPosition);
        }

        private static bool IsPointInsideAssetFootprint(CrossingPlacementAsset asset, Vector3 point)
        {
            if (asset.Plan.IsValid)
            {
                if (HorizontalPointSegmentDistanceSqr(point, asset.Plan.LeftEdge, asset.Plan.RightEdge) <= CrossingReplacementFootprintHalfWidth * CrossingReplacementFootprintHalfWidth)
                    return true;

                if (HorizontalSqrDistance(point, asset.Plan.Center) <= CrossingReplacementFootprintHalfWidth * CrossingReplacementFootprintHalfWidth)
                    return true;

                if (HorizontalSqrDistance(point, asset.Plan.LeftEdge) <= CrossingReplacementAccessRadius * CrossingReplacementAccessRadius
                    || HorizontalSqrDistance(point, asset.Plan.RightEdge) <= CrossingReplacementAccessRadius * CrossingReplacementAccessRadius)
                {
                    return true;
                }
            }

            int accessCount = CrossingLandingConnectorPlanner.CopyAccessAssetsTo(AccessAssetBuffer);
            for (int i = 0; i < accessCount; i++)
            {
                CrossingLandingAccessAssetWorkOrder order = AccessAssetBuffer[i];
                if (order.AssetId != asset.Id)
                    continue;

                float halfWidth = Mathf.Max(CrossingReplacementFootprintHalfWidth, order.FootprintWidth * 0.5f);
                float landingRadius = Mathf.Max(CrossingReplacementAccessRadius, order.FootprintWidth + 1f);
                if (HorizontalPointSegmentDistanceSqr(point, order.DeckPosition, order.Position) <= halfWidth * halfWidth
                    || HorizontalSqrDistance(point, order.Position) <= landingRadius * landingRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPlanInsideAssetFootprint(CrossingPlacementAsset asset, CrossingPlacementPlan plan)
        {
            if (!plan.IsValid)
                return false;

            if (asset.Plan.IsValid)
            {
                float footprintSqr = CrossingReplacementFootprintHalfWidth * CrossingReplacementFootprintHalfWidth;
                if (HorizontalSegmentSegmentDistanceSqr(plan.LeftEdge, plan.RightEdge, asset.Plan.LeftEdge, asset.Plan.RightEdge) <= footprintSqr)
                    return true;

                if (HorizontalSqrDistance(plan.Center, asset.Plan.Center) <= footprintSqr)
                    return true;
            }

            if (IsPointInsideAssetFootprint(asset, plan.Center)
                || IsPointInsideAssetFootprint(asset, plan.LeftEdge)
                || IsPointInsideAssetFootprint(asset, plan.RightEdge))
            {
                return true;
            }

            for (int i = 0; i < plan.JunctionExitCount; i++)
            {
                if (IsPointInsideAssetFootprint(asset, plan.JunctionExitPoints[i]))
                    return true;
            }

            return false;
        }

        private static void WriteSignalRoadStateSnapshot(BinaryWriter writer, SignalRoadStateSnapshot snapshot)
        {
            writer.Write(snapshot.HasSnapshot);
            if (!snapshot.HasSnapshot)
                return;

            writer.Write(snapshot.NodeId);
            writer.Write((int)snapshot.NodeFlags);
            int segmentCount = snapshot.SegmentCount;
            writer.Write(segmentCount);
            for (int i = 0; i < segmentCount; i++)
            {
                SignalRoadSegmentState segment = snapshot.Segments[i];
                writer.Write(segment.SegmentId);
                writer.Write(segment.StartNode);
                writer.Write((int)segment.SegmentFlags);
                writer.Write((int)segment.VehicleState);
                writer.Write((int)segment.PedestrianState);
                writer.Write(segment.Vehicles);
                writer.Write(segment.Pedestrians);
            }
        }

        private static SignalRoadStateSnapshot ReadSignalRoadStateSnapshot(BinaryReader reader)
        {
            if (!reader.ReadBoolean())
                return SignalRoadStateSnapshot.Empty;

            ushort nodeId = reader.ReadUInt16();
            NetNode.Flags nodeFlags = (NetNode.Flags)reader.ReadInt32();
            int segmentCount = Math.Max(reader.ReadInt32(), 0);
            SignalRoadSegmentState[] segments = new SignalRoadSegmentState[segmentCount];
            for (int i = 0; i < segmentCount; i++)
            {
                segments[i] = new SignalRoadSegmentState(
                    reader.ReadUInt16(),
                    reader.ReadBoolean(),
                    (NetSegment.Flags)reader.ReadInt32(),
                    (RoadBaseAI.TrafficLightState)reader.ReadInt32(),
                    (RoadBaseAI.TrafficLightState)reader.ReadInt32(),
                    reader.ReadBoolean(),
                    reader.ReadBoolean());
            }

            return new SignalRoadStateSnapshot(true, nodeId, nodeFlags, segments);
        }

        private static bool IsSharedGradeSeparatedApproach(CrossingPlacementAsset existing, CrossingPlacementRecord placement, CrossingPlacementPlan plan)
        {
            if (existing.Placement.SegmentId == 0 || placement.SegmentId == 0)
                return false;

            if (existing.Placement.TargetNodeId != 0
                && placement.TargetNodeId != 0
                && existing.Placement.TargetNodeId == placement.TargetNodeId
                && existing.Placement.SegmentId == placement.SegmentId)
            {
                return true;
            }

            if (plan.IsValid
                && existing.Plan.IsValid
                && existing.Plan.TargetNodeId != 0
                && plan.TargetNodeId != 0
                && existing.Plan.TargetNodeId == plan.TargetNodeId
                && existing.Placement.SegmentId == placement.SegmentId)
            {
                return true;
            }

            return false;
        }

        public static int PruneDuplicateAssets()
        {
            int removed = 0;
            for (int i = Assets.Count - 1; i >= 0; i--)
            {
                CrossingPlacementAsset newest = Assets[i];
                for (int j = i - 1; j >= 0; j--)
                {
                    if (!IsSameAssetLocation(newest, Assets[j].Placement, Assets[j].Plan))
                        continue;

                    Debug.Log("[PedestrianCrossingToolkit] Pruned restored duplicate: kept="
                              + newest.Id
                              + " removed=" + Assets[j].Id);
                    Assets.RemoveAt(j);
                    removed++;
                    i--;
                }
            }

            if (removed > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Pruned duplicate pending crossings: removed="
                          + removed
                          + " count=" + Assets.Count);
            }

            return removed;
        }

        public static bool IsSamePlacementLocation(CrossingPlacementRecord existing, CrossingPlacementRecord candidate)
        {
            if (existing.SegmentId == 0 || candidate.SegmentId == 0)
                return false;

            if (IsSameGradeSeparatedPlacementFamily(existing.Mode, candidate.Mode))
                return IsSameRoadPlacementPoint(existing, candidate);

            if (HasStructuredSnap(existing) && HasStructuredSnap(candidate))
            {
                if (existing.SlotNumber != candidate.SlotNumber)
                    return false;

                if (existing.TargetNodeId != 0 || candidate.TargetNodeId != 0)
                    return existing.TargetNodeId != 0
                           && candidate.TargetNodeId != 0
                           && existing.TargetNodeId == candidate.TargetNodeId
                           && existing.SegmentId == candidate.SegmentId;

                return existing.SegmentId == candidate.SegmentId
                       && Vector3.SqrMagnitude(existing.WorldPosition - candidate.WorldPosition) <= SameLocationDistanceTolerance * SameLocationDistanceTolerance;
            }

            if (existing.NearNode && candidate.NearNode)
            {
                ushort existingNode = existing.TargetNodeId != 0
                    ? existing.TargetNodeId
                    : CrossingPlacementPlanner.GetTargetNodeId(existing.SegmentId, existing.SegmentPosition);
                ushort candidateNode = candidate.TargetNodeId != 0
                    ? candidate.TargetNodeId
                    : CrossingPlacementPlanner.GetTargetNodeId(candidate.SegmentId, candidate.SegmentPosition);
                return existingNode != 0 && existingNode == candidateNode;
            }

            if (existing.SegmentId == candidate.SegmentId
                && existing.NearNode != candidate.NearNode
                && (IsGradeSeparatedMode(existing.Mode) || IsGradeSeparatedMode(candidate.Mode))
                && Vector3.SqrMagnitude(existing.WorldPosition - candidate.WorldPosition) <= 9f * 9f)
                return true;

            if (existing.SegmentId == candidate.SegmentId
                && Mathf.Abs(existing.SegmentPosition - candidate.SegmentPosition) <= SameLocationSegmentTolerance)
                return true;

            float distanceLimit = SameLocationDistanceTolerance;
            if (existing.NearNode || candidate.NearNode)
                distanceLimit = Mathf.Max(distanceLimit, 6f);

            return Vector3.SqrMagnitude(existing.WorldPosition - candidate.WorldPosition) <= distanceLimit * distanceLimit;
        }

        private static bool IsSameRoadPlacementPoint(CrossingPlacementRecord existing, CrossingPlacementRecord candidate)
        {
            if (existing.SegmentId != candidate.SegmentId)
                return false;

            if (Mathf.Abs(existing.SegmentPosition - candidate.SegmentPosition) <= SameLocationSegmentTolerance)
                return true;

            return Vector3.SqrMagnitude(existing.WorldPosition - candidate.WorldPosition) <= SameLocationDistanceTolerance * SameLocationDistanceTolerance;
        }

        private static bool IsSameGradeSeparatedPlacementFamily(PedestrianToolMode existingMode, PedestrianToolMode candidateMode)
        {
            if (IsSubwayMode(existingMode) && IsSubwayMode(candidateMode))
                return true;

            return existingMode == PedestrianToolMode.PedestrianBridge
                   && candidateMode == PedestrianToolMode.PedestrianBridge;
        }

        private static bool IsGradeSeparatedMode(PedestrianToolMode mode)
        {
            return mode == PedestrianToolMode.PedestrianBridge
                   || mode == PedestrianToolMode.SubwayLink
                   || mode == PedestrianToolMode.SubwayPointToPoint;
        }

        private static bool IsSubwayMode(PedestrianToolMode mode)
        {
            return mode == PedestrianToolMode.SubwayLink
                   || mode == PedestrianToolMode.SubwayPointToPoint;
        }

        private static float GetPhysicalRemovalDistanceLimit(CrossingPlacementAsset existing, CrossingPlacementRecord placement)
        {
            if (IsGradeSeparatedMode(existing.Placement.Mode) || IsGradeSeparatedMode(placement.Mode))
                return RemovalDistanceTolerance;

            if (existing.Plan.IsValid)
            {
                float spanWidth = HorizontalDistance(existing.Plan.LeftEdge, existing.Plan.RightEdge);
                if (spanWidth > 0.1f)
                    return Mathf.Max(12f, Mathf.Min(RemovalDistanceTolerance, spanWidth * 0.4f));
            }

            if (existing.Placement.NearNode || placement.NearNode)
                return Mathf.Max(SameLocationDistanceTolerance, 12f);

            return SameLocationDistanceTolerance;
        }

        private static float GetRemovalDistanceSqr(CrossingPlacementAsset existing, CrossingPlacementRecord placement)
        {
            if (existing.Plan.IsValid)
            {
                float spanDistance = HorizontalPointSegmentDistanceSqr(placement.WorldPosition, existing.Plan.LeftEdge, existing.Plan.RightEdge);
                float centerDistance = HorizontalSqrDistance(existing.Plan.Center, placement.WorldPosition);
                return Mathf.Min(spanDistance, centerDistance);
            }

            return HorizontalSqrDistance(existing.Placement.WorldPosition, placement.WorldPosition);
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            return Mathf.Sqrt(HorizontalSqrDistance(first, second));
        }

        private static float HorizontalSqrDistance(Vector3 first, Vector3 second)
        {
            float dx = first.x - second.x;
            float dz = first.z - second.z;
            return (dx * dx) + (dz * dz);
        }

        private static float HorizontalPointSegmentDistanceSqr(Vector3 point, Vector3 first, Vector3 second)
        {
            Vector2 p = new Vector2(point.x, point.z);
            Vector2 a = new Vector2(first.x, first.z);
            Vector2 b = new Vector2(second.x, second.z);
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            if (lengthSqr <= 0.001f)
                return (p - a).sqrMagnitude;

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSqr);
            Vector2 nearest = a + ab * t;
            return (p - nearest).sqrMagnitude;
        }

        private static float HorizontalSegmentSegmentDistanceSqr(Vector3 firstStart, Vector3 firstEnd, Vector3 secondStart, Vector3 secondEnd)
        {
            float distance = HorizontalPointSegmentDistanceSqr(firstStart, secondStart, secondEnd);
            distance = Mathf.Min(distance, HorizontalPointSegmentDistanceSqr(firstEnd, secondStart, secondEnd));
            distance = Mathf.Min(distance, HorizontalPointSegmentDistanceSqr(secondStart, firstStart, firstEnd));
            distance = Mathf.Min(distance, HorizontalPointSegmentDistanceSqr(secondEnd, firstStart, firstEnd));
            return distance;
        }

        private static bool HasStructuredSnap(CrossingPlacementRecord placement)
        {
            return placement.TargetNodeId != 0 || placement.SlotNumber > 0 || placement.IsEndSegmentSlot;
        }
    }
}
