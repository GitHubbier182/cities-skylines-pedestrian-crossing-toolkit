using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public static class RoadSurfacePlacementGuard
    {
        public static bool IsOnRoadSurface(Vector3 position)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null)
                return false;

            for (ushort segmentId = 1; segmentId < netManager.m_segments.m_size; segmentId++)
            {
                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null || !(segment.Info.m_netAI is RoadBaseAI))
                    continue;

                if (segment.m_startNode == 0 || segment.m_endNode == 0)
                    continue;

                Vector3 start = netManager.m_nodes.m_buffer[segment.m_startNode].m_position;
                Vector3 end = netManager.m_nodes.m_buffer[segment.m_endNode].m_position;
                float along;
                float distance = DistanceToSegment2D(position, start, end, out along);
                if (along < -0.05f || along > 1.05f)
                    continue;

                if (distance <= EstimateCarriageHalfWidth(segment.Info))
                    return true;
            }

            return false;
        }

        public static float EstimateCarriageHalfWidth(NetInfo info)
        {
            float halfWidth = 0f;
            if (info == null || info.m_lanes == null)
                return 5f;

            for (int i = 0; i < info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = info.m_lanes[i];
                if (lane == null || !IsVehicleRoadLane(lane))
                    continue;

                float laneEdge = Mathf.Abs(lane.m_position) + Mathf.Max(1f, lane.m_width * 0.5f);
                if (laneEdge > halfWidth)
                    halfWidth = laneEdge;
            }

            return Mathf.Clamp(halfWidth + 1.5f, 4.5f, 36.5f);
        }

        private static bool IsVehicleRoadLane(NetInfo.Lane lane)
        {
            return (lane.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0;
        }

        private static float DistanceToSegment2D(Vector3 point, Vector3 start, Vector3 end, out float along)
        {
            Vector2 p = new Vector2(point.x, point.z);
            Vector2 a = new Vector2(start.x, start.z);
            Vector2 b = new Vector2(end.x, end.z);
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared <= 0.01f)
            {
                along = 0f;
                return Vector2.Distance(p, a);
            }

            along = Vector2.Dot(p - a, ab) / lengthSquared;
            Vector2 closest = a + ab * Mathf.Clamp01(along);
            return Vector2.Distance(p, closest);
        }
    }
}
