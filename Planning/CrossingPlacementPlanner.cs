using ColossalFramework.Math;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public static class CrossingPlacementPlanner
    {
        private const float EndpointExtensionTolerance = 0.025f;
        private const float EndpointExtensionMinDistance = 0.5f;
        private const float EndpointExtensionMaxDistance = 32f;

        public static CrossingPlacementPlan Build(CrossingPlacementRecord record)
        {
            return Build(record, false);
        }

        public static CrossingPlacementPlan BuildExisting(CrossingPlacementAsset asset)
        {
            bool allowExistingSignalSurfaceCrossing = asset.Placement.Mode == PedestrianToolMode.SignalCrossing;
            return Build(asset.Placement, allowExistingSignalSurfaceCrossing);
        }

        private static CrossingPlacementPlan Build(CrossingPlacementRecord record, bool allowExistingSignalSurfaceCrossing)
        {
            if (record.Mode == PedestrianToolMode.SubwayPointToPoint)
                return SubwayPointToPointPlacementPlanner.Build(record);

            if (!record.IsValid || record.SegmentId == 0)
                return CrossingPlacementPlan.Invalid;

            NetManager netManager = NetManager.instance;
            if (record.SegmentId >= netManager.m_segments.m_size)
                return CrossingPlacementPlan.Invalid;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[record.SegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null)
                return CrossingPlacementPlan.Invalid;

            CrossingPlacementPolicyResult policy = CrossingPlacementPolicy.Evaluate(record, allowExistingSignalSurfaceCrossing);
            if (!policy.Success)
                return CrossingPlacementPlan.Invalid;

            Vector3 center;
            Vector3 roadDirection;
            Vector3 crossingDirection;
            if (!TryGetRoadFrameForPlacement(netManager, ref segment, record.SegmentPosition, record.WorldPosition, out center, out roadDirection, out crossingDirection))
                return CrossingPlacementPlan.Invalid;

            float halfWidth = EstimateRoadHalfWidth(segment.Info);
            Vector3 leftEdge = center - crossingDirection * halfWidth;
            Vector3 rightEdge = center + crossingDirection * halfWidth;
            ushort targetNodeId = policy.TargetNodeId;
            int connectedSegmentCount = 0;
            Vector3[] junctionExitPoints = new Vector3[0];

            return new CrossingPlacementPlan(true, record.Mode, record.SegmentId, center, roadDirection, crossingDirection, leftEdge, rightEdge, halfWidth * 2f, targetNodeId, connectedSegmentCount, junctionExitPoints, policy.SuppressSurfaceCrossing, policy.ApplicationKind);
        }

        public static bool TryGetRoadFrameForPlacement(NetManager netManager, ref NetSegment segment, float segmentPosition, Vector3 fallbackPosition, out Vector3 center, out Vector3 roadDirection, out Vector3 roadRight)
        {
            center = fallbackPosition;
            roadDirection = Vector3.zero;
            roadRight = Vector3.zero;

            if (segment.m_startNode == 0
                || segment.m_endNode == 0
                || segment.m_startNode >= netManager.m_nodes.m_size
                || segment.m_endNode >= netManager.m_nodes.m_size)
            {
                return false;
            }

            Vector3 start = netManager.m_nodes.m_buffer[segment.m_startNode].m_position;
            Vector3 end = netManager.m_nodes.m_buffer[segment.m_endNode].m_position;
            Vector3 middleA;
            Vector3 middleB;
            NetSegment.CalculateMiddlePoints(start, segment.m_startDirection, end, segment.m_endDirection, false, false, out middleA, out middleB);
            Bezier3 bezier = new Bezier3
            {
                a = start,
                b = middleA,
                c = middleB,
                d = end
            };

            float clampedPosition = Mathf.Clamp01(segmentPosition);
            center = bezier.Position(clampedPosition);
            TryExtendEndpointCenterline(bezier, clampedPosition, fallbackPosition, ref center);
            center.y = fallbackPosition.y;

            roadDirection = GetBezierRoadDirection(bezier, clampedPosition);
            if (roadDirection.sqrMagnitude <= 0.01f)
            {
                roadDirection = end - start;
                roadDirection.y = 0f;
            }

            if (roadDirection.sqrMagnitude <= 0.01f)
                return false;

            roadDirection.Normalize();
            roadRight = new Vector3(-roadDirection.z, 0f, roadDirection.x);
            return true;
        }

        private static void TryExtendEndpointCenterline(Bezier3 bezier, float clampedPosition, Vector3 fallbackPosition, ref Vector3 center)
        {
            Vector3 flatFallback = fallbackPosition;
            flatFallback.y = 0f;

            if (clampedPosition <= EndpointExtensionTolerance)
            {
                Vector3 start = bezier.Position(0f);
                Vector3 direction = bezier.Position(EndpointExtensionTolerance) - start;
                direction.y = 0f;
                if (TryProjectEndpointExtension(flatFallback, start, direction, true, out center))
                    return;
            }

            if (clampedPosition >= 1f - EndpointExtensionTolerance)
            {
                Vector3 end = bezier.Position(1f);
                Vector3 direction = end - bezier.Position(1f - EndpointExtensionTolerance);
                direction.y = 0f;
                TryProjectEndpointExtension(flatFallback, end, direction, false, out center);
            }
        }

        private static bool TryProjectEndpointExtension(Vector3 flatFallback, Vector3 endpoint, Vector3 direction, bool startEndpoint, out Vector3 center)
        {
            center = endpoint;
            endpoint.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return false;

            direction.Normalize();
            float projected = Vector3.Dot(flatFallback - endpoint, direction);
            bool outsideEndpoint = startEndpoint
                ? projected < -EndpointExtensionMinDistance
                : projected > EndpointExtensionMinDistance;
            if (!outsideEndpoint || Mathf.Abs(projected) > EndpointExtensionMaxDistance)
                return false;

            center = endpoint + direction * projected;
            return true;
        }

        private static Vector3 GetBezierRoadDirection(Bezier3 bezier, float clampedPosition)
        {
            float before = Mathf.Clamp01(clampedPosition - 0.01f);
            float after = Mathf.Clamp01(clampedPosition + 0.01f);
            Vector3 direction = bezier.Position(after) - bezier.Position(before);
            direction.y = 0f;
            return direction;
        }

        public static float EstimateRoadHalfWidthForInfo(NetInfo info)
        {
            return EstimateRoadHalfWidth(info);
        }

        private static float EstimateRoadHalfWidth(NetInfo info)
        {
            float halfWidth = 8f;
            if (info == null || info.m_lanes == null)
                return halfWidth;

            for (int i = 0; i < info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = info.m_lanes[i];
                if (lane == null)
                    continue;

                float laneEdge = Mathf.Abs(lane.m_position) + Mathf.Max(1f, lane.m_width * 0.5f);
                if (laneEdge > halfWidth)
                    halfWidth = laneEdge;
            }

            return Mathf.Clamp(halfWidth + 1.5f, 6f, 40f);
        }

        public static bool IsSimpleStraightRoadJoin(ushort segmentId, float segmentPosition)
        {
            NetManager netManager = NetManager.instance;
            if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ushort targetNodeId = GetTargetNodeId(segmentId, segmentPosition);
            if (targetNodeId == 0 || targetNodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[targetNodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0 || node.CountSegments() != 2)
                return false;

            ushort firstSegmentId = node.GetSegment(0);
            ushort secondSegmentId = node.GetSegment(1);
            if (firstSegmentId == 0 || secondSegmentId == 0)
                return false;

            Vector3 firstDirection;
            Vector3 secondDirection;
            if (!TryGetDirectionAwayFromNode(netManager, firstSegmentId, targetNodeId, out firstDirection)
                || !TryGetDirectionAwayFromNode(netManager, secondSegmentId, targetNodeId, out secondDirection))
                return false;

            return Vector3.Dot(firstDirection, secondDirection) <= -0.85f;
        }

        public static ushort GetTargetNodeId(ushort segmentId, float segmentPosition)
        {
            NetManager netManager = NetManager.instance;
            if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return 0;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return 0;

            return segmentPosition <= 0.5f ? segment.m_startNode : segment.m_endNode;
        }

        private static bool TryGetDirectionAwayFromNode(NetManager netManager, ushort segmentId, ushort nodeId, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return false;

            ushort otherNodeId;
            if (segment.m_startNode == nodeId)
                otherNodeId = segment.m_endNode;
            else if (segment.m_endNode == nodeId)
                otherNodeId = segment.m_startNode;
            else
                return false;

            if (otherNodeId == 0 || otherNodeId >= netManager.m_nodes.m_size)
                return false;

            Vector3 nodePosition = netManager.m_nodes.m_buffer[nodeId].m_position;
            direction = netManager.m_nodes.m_buffer[otherNodeId].m_position - nodePosition;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return false;

            direction.Normalize();
            return true;
        }
    }
}
