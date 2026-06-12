using ColossalFramework.Math;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public struct RoadSnapResult
    {
        public static readonly RoadSnapResult None = new RoadSnapResult(false, 0, 0f, 0f, 0f, Vector3.zero, 0f, 0, 0, Vector3.zero, Vector3.zero, false, 0, false, 0, string.Empty);

        public readonly bool IsResolved;
        public readonly ushort SegmentId;
        public readonly float SegmentPosition;
        public readonly float SnapSegmentPosition;
        public readonly float BuildSegmentPosition;
        public readonly Vector3 WorldPosition;
        public readonly float ModuleLength;
        public readonly ushort ModuleStartNodeId;
        public readonly ushort ModuleEndNodeId;
        public readonly Vector3 ModuleStartPosition;
        public readonly Vector3 ModuleEndPosition;
        public readonly bool IsEndpointSlot;
        public readonly int SlotNumber;
        public readonly bool IsEndSegmentSlot;
        public readonly ushort TargetNodeId;
        public readonly string SlotLabel;

        public RoadSnapResult(
            bool isResolved,
            ushort segmentId,
            float segmentPosition,
            float snapSegmentPosition,
            float buildSegmentPosition,
            Vector3 worldPosition,
            float moduleLength,
            ushort moduleStartNodeId,
            ushort moduleEndNodeId,
            Vector3 moduleStartPosition,
            Vector3 moduleEndPosition,
            bool isEndpointSlot,
            int slotNumber,
            bool isEndSegmentSlot,
            ushort targetNodeId,
            string slotLabel)
        {
            IsResolved = isResolved;
            SegmentId = segmentId;
            SegmentPosition = segmentPosition;
            SnapSegmentPosition = snapSegmentPosition;
            BuildSegmentPosition = buildSegmentPosition;
            WorldPosition = worldPosition;
            ModuleLength = moduleLength;
            ModuleStartNodeId = moduleStartNodeId;
            ModuleEndNodeId = moduleEndNodeId;
            ModuleStartPosition = moduleStartPosition;
            ModuleEndPosition = moduleEndPosition;
            IsEndpointSlot = isEndpointSlot;
            SlotNumber = slotNumber;
            IsEndSegmentSlot = isEndSegmentSlot;
            TargetNodeId = targetNodeId;
            SlotLabel = slotLabel;
        }
    }

    public static class RoadSnapResolver
    {
        public const int ContinuousRoadSlot = -1;
        public const int SegmentJoinSlot = 0;
        public const int VanillaCrossingStartSlot = 2;
        public const int VanillaCrossingEndSlot = 3;

        private const int BezierSamples = 48;
        private const float SignalJoinClickBufferDistance = 6f;
        private const float SignalJoinEndpointPositionBuffer = 0.02f;

        public static bool TryResolve(ushort raycastSegmentId, Vector3 hitPosition, PedestrianToolMode mode, out RoadSnapResult snap)
        {
            snap = RoadSnapResult.None;

            NetManager netManager = NetManager.instance;
            NetSegment segment;
            if (netManager == null || !TryGetCreatedPlacementSegment(netManager, raycastSegmentId, mode, out segment))
                return false;

            Vector3 placementReference = hitPosition;
            float rawPosition = EstimateSegmentPosition(netManager, raycastSegmentId, placementReference);
            if (mode == PedestrianToolMode.SignalCrossing
                && TryResolveSignalJoinNearSegment(netManager, segment, hitPosition, rawPosition, out snap))
            {
                return true;
            }

            return TryBuildRoadPositionSnap(netManager, raycastSegmentId, segment, placementReference.y, rawPosition, hitPosition, mode, out snap);
        }

        public static bool TryResolveSignalJoinNode(ushort nodeId, out RoadSnapResult snap)
        {
            return TryResolveSignalJoinNode(nodeId, false, out snap);
        }

        public static bool TryResolveSignalJoinNode(ushort nodeId, bool allowSurfaceExclusion, out RoadSnapResult snap)
        {
            snap = RoadSnapResult.None;

            NetManager netManager = NetManager.instance;
            ushort segmentId;
            NetSegment segment;
            bool isEndSegment;
            if (!TryGetSignalJoinSegment(netManager, nodeId, allowSurfaceExclusion, out segmentId, out segment, out isEndSegment))
                return false;

            Vector3 start = GetNodePosition(netManager, segment.m_startNode);
            Vector3 end = GetNodePosition(netManager, segment.m_endNode);
            Bezier3 bezier = GetSegmentBezier(start, segment.m_startDirection, end, segment.m_endDirection);
            float length = EstimateBezierLength(bezier);
            if (length <= 0.01f)
                return false;

            float segmentPosition = isEndSegment ? 1f : 0f;
            Vector3 world = GetNodePosition(netManager, nodeId);
            snap = new RoadSnapResult(
                true,
                segmentId,
                segmentPosition,
                segmentPosition,
                segmentPosition,
                world,
                length,
                segment.m_startNode,
                segment.m_endNode,
                start,
                end,
                true,
                SegmentJoinSlot,
                isEndSegment,
                nodeId,
                FormatSlot(SegmentJoinSlot));
            return true;
        }

        public static string FormatSlot(int slotNumber)
        {
            switch (slotNumber)
            {
                case ContinuousRoadSlot:
                    return "road position";
                case SegmentJoinSlot:
                    return "segment join";
                case VanillaCrossingStartSlot:
                    return "vanilla crossing start";
                case VanillaCrossingEndSlot:
                    return "vanilla crossing end";
                default:
                    return "mid-block";
            }
        }

        private static bool TryBuildRoadPositionSnap(NetManager netManager, ushort segmentId, NetSegment segment, float height, float rawPosition, Vector3 hitPosition, PedestrianToolMode mode, out RoadSnapResult snap)
        {
            snap = RoadSnapResult.None;
            Vector3 start = GetNodePosition(netManager, segment.m_startNode);
            Vector3 end = GetNodePosition(netManager, segment.m_endNode);
            Bezier3 bezier = GetSegmentBezier(start, segment.m_startDirection, end, segment.m_endDirection);
            float length = EstimateBezierLength(bezier);
            if (length <= 0.01f)
                return false;

            float buildPosition = Mathf.Clamp01(rawPosition);
            Vector3 world = hitPosition;
            world.y = height;
            float segmentPosition = EstimateSegmentPosition(netManager, segmentId, world, buildPosition);
            int slotNumber = ContinuousRoadSlot;
            ushort targetNodeId = 0;
            bool isEndpointSlot = false;
            bool isEndSegmentSlot = false;

            RoadPlacementRules.VanillaCrossingPoint surfaceCrossingPoint;
            if (IsGradeSeparatedMode(mode)
                && RoadPlacementRules.IsRoadGradeSeparatedPlacementTarget(segmentId)
                && RoadPlacementRules.TryGetVanillaCrossingNear(
                    segmentId,
                    world,
                    RoadPlacementRules.SurfaceCrossingPostVanillaBuffer,
                    out surfaceCrossingPoint))
            {
                targetNodeId = surfaceCrossingPoint.NodeId;
                slotNumber = surfaceCrossingPoint.NodeId == segment.m_endNode
                    ? VanillaCrossingEndSlot
                    : VanillaCrossingStartSlot;
                isEndpointSlot = true;
                isEndSegmentSlot = slotNumber == VanillaCrossingEndSlot;
            }

            snap = new RoadSnapResult(
                true,
                segmentId,
                Mathf.Clamp01(segmentPosition),
                Mathf.Clamp01(segmentPosition),
                buildPosition,
                world,
                length,
                segment.m_startNode,
                segment.m_endNode,
                start,
                end,
                isEndpointSlot,
                slotNumber,
                isEndSegmentSlot,
                targetNodeId,
                FormatSlot(slotNumber));
            return true;
        }

        private static bool TryResolveSignalJoinNearSegment(NetManager netManager, NetSegment segment, Vector3 hitPosition, float rawPosition, out RoadSnapResult snap)
        {
            snap = RoadSnapResult.None;
            RoadSnapResult startSnap;
            RoadSnapResult endSnap;
            bool hasStart = TryResolveSignalJoinNode(segment.m_startNode, out startSnap);
            bool hasEnd = TryResolveSignalJoinNode(segment.m_endNode, out endSnap);

            if (hasStart && rawPosition <= SignalJoinEndpointPositionBuffer)
            {
                snap = startSnap;
                return true;
            }

            if (hasEnd && rawPosition >= 1f - SignalJoinEndpointPositionBuffer)
            {
                snap = endSnap;
                return true;
            }

            float bestDistance = SignalJoinClickBufferDistance;
            bool found = false;

            if (hasStart)
            {
                float distance = HorizontalDistance(startSnap.WorldPosition, hitPosition);
                if (distance <= bestDistance)
                {
                    snap = startSnap;
                    bestDistance = distance;
                    found = true;
                }
            }

            if (hasEnd)
            {
                float distance = HorizontalDistance(endSnap.WorldPosition, hitPosition);
                if (distance <= bestDistance)
                {
                    snap = endSnap;
                    found = true;
                }
            }

            return found;
        }

        private static bool TryGetSignalJoinSegment(NetManager netManager, ushort nodeId, bool allowSurfaceExclusion, out ushort segmentId, out NetSegment segment, out bool isEndSegment)
        {
            segmentId = 0;
            segment = default(NetSegment);
            isEndSegment = false;

            if (netManager == null || nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0 || node.Info == null || !(node.Info.m_netAI is RoadBaseAI))
                return false;

            if (node.CountSegments() != 2)
                return false;

            int connectedRoadSegments = 0;
            for (int i = 0; i < 8; i++)
            {
                ushort candidateSegmentId = node.GetSegment(i);
                if (candidateSegmentId == 0)
                    continue;

                NetSegment candidateSegment;
                if (!TryGetCreatedRoadSegment(netManager, candidateSegmentId, out candidateSegment))
                    return false;

                if (candidateSegment.m_startNode != nodeId && candidateSegment.m_endNode != nodeId)
                    return false;

                if (!RoadPlacementRules.AllowsSurfaceCrossing(candidateSegmentId))
                    return false;

                connectedRoadSegments++;
                if (segmentId == 0)
                {
                    segmentId = candidateSegmentId;
                    segment = candidateSegment;
                    isEndSegment = candidateSegment.m_endNode == nodeId;
                }
            }

            if (connectedRoadSegments != 2 || segmentId == 0)
                return false;

            if (allowSurfaceExclusion)
                return true;

            ushort blockedNodeId;
            float segmentPosition = isEndSegment ? 1f : 0f;
            return !RoadPlacementRules.TryGetSurfaceCrossingExclusionNode(
                segmentId,
                segmentPosition,
                GetNodePosition(netManager, nodeId),
                out blockedNodeId);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt((dx * dx) + (dz * dz));
        }

        private static float EstimateSegmentPosition(NetManager netManager, ushort segmentId, Vector3 position)
        {
            return EstimateSegmentPosition(netManager, segmentId, position, 0.5f);
        }

        private static float EstimateSegmentPosition(NetManager netManager, ushort segmentId, Vector3 position, float fallback)
        {
            NetSegment segment;
            if (!TryGetCreatedSegment(netManager, segmentId, out segment))
                return Mathf.Clamp01(fallback);

            Vector3 start = GetNodePosition(netManager, segment.m_startNode);
            Vector3 end = GetNodePosition(netManager, segment.m_endNode);
            Bezier3 bezier = GetSegmentBezier(start, segment.m_startDirection, end, segment.m_endDirection);

            float bestPosition = fallback;
            float bestDistance = float.MaxValue;
            for (int i = 0; i <= BezierSamples; i++)
            {
                float t = i / (float)BezierSamples;
                float distance = Vector3.SqrMagnitude(bezier.Position(t) - position);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestPosition = t;
            }

            float sampleWidth = 1f / BezierSamples;
            float min = Mathf.Clamp01(bestPosition - sampleWidth);
            float max = Mathf.Clamp01(bestPosition + sampleWidth);
            for (int i = 0; i < 8; i++)
            {
                float left = Mathf.Lerp(min, max, 1f / 3f);
                float right = Mathf.Lerp(min, max, 2f / 3f);
                float leftDistance = Vector3.SqrMagnitude(bezier.Position(left) - position);
                float rightDistance = Vector3.SqrMagnitude(bezier.Position(right) - position);
                if (leftDistance < rightDistance)
                    max = right;
                else
                    min = left;
            }

            return Mathf.Clamp01((min + max) * 0.5f);
        }

        private static float EstimateBezierLength(Bezier3 bezier)
        {
            float length = 0f;
            Vector3 previous = bezier.Position(0f);
            for (int i = 1; i <= BezierSamples; i++)
            {
                Vector3 current = bezier.Position(i / (float)BezierSamples);
                length += Vector3.Distance(previous, current);
                previous = current;
            }

            return length;
        }

        private static Vector3 GetNodePosition(NetManager netManager, ushort nodeId)
        {
            if (netManager == null || nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return Vector3.zero;

            return netManager.m_nodes.m_buffer[nodeId].m_position;
        }

        private static Bezier3 GetSegmentBezier(Vector3 start, Vector3 startDirection, Vector3 end, Vector3 endDirection)
        {
            Vector3 middleA;
            Vector3 middleB;
            NetSegment.CalculateMiddlePoints(start, startDirection, end, endDirection, false, false, out middleA, out middleB);
            return new Bezier3
            {
                a = start,
                b = middleA,
                c = middleB,
                d = end
            };
        }

        private static bool TryGetCreatedRoadSegment(NetManager netManager, ushort segmentId, out NetSegment segment)
        {
            segment = default(NetSegment);
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            segment = netManager.m_segments.m_buffer[segmentId];
            return (segment.m_flags & NetSegment.Flags.Created) != 0
                   && segment.m_startNode != 0
                   && segment.m_endNode != 0
                   && segment.Info != null
                   && segment.Info.m_netAI is RoadBaseAI;
        }

        private static bool TryGetCreatedSegment(NetManager netManager, ushort segmentId, out NetSegment segment)
        {
            segment = default(NetSegment);
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            segment = netManager.m_segments.m_buffer[segmentId];
            return (segment.m_flags & NetSegment.Flags.Created) != 0
                   && segment.m_startNode != 0
                   && segment.m_endNode != 0
                   && segment.Info != null;
        }

        private static bool TryGetCreatedPlacementSegment(NetManager netManager, ushort segmentId, PedestrianToolMode mode, out NetSegment segment)
        {
            segment = default(NetSegment);
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            segment = netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0
                || segment.m_startNode == 0
                || segment.m_endNode == 0
                || segment.Info == null)
            {
                return false;
            }

            if (IsGradeSeparatedMode(mode))
                return AllowsPlacementTargetForMode(mode, segmentId);

            if (mode == PedestrianToolMode.RemoveCrossing)
            {
                return segment.Info.m_netAI is RoadBaseAI
                       || RoadPlacementRules.AllowsGradeSeparatedPlacementTarget(segmentId);
            }

            return segment.Info.m_netAI is RoadBaseAI;
        }

        private static bool AllowsPlacementTargetForMode(PedestrianToolMode mode, ushort segmentId)
        {
            switch (mode)
            {
                case PedestrianToolMode.PedestrianBridge:
                    return RoadPlacementRules.AllowsPedestrianBridgePlacementTarget(segmentId);
                case PedestrianToolMode.SubwayLink:
                    return RoadPlacementRules.AllowsSubwayPlacementTarget(segmentId);
                case PedestrianToolMode.SubwayPointToPoint:
                    return RoadPlacementRules.AllowsManualSubwayPlacementTarget(segmentId);
                default:
                    return RoadPlacementRules.AllowsGradeSeparatedPlacementTarget(segmentId);
            }
        }

        private static bool IsGradeSeparatedMode(PedestrianToolMode mode)
        {
            return mode == PedestrianToolMode.SubwayLink
                   || mode == PedestrianToolMode.SubwayPointToPoint
                   || mode == PedestrianToolMode.PedestrianBridge;
        }
    }
}
