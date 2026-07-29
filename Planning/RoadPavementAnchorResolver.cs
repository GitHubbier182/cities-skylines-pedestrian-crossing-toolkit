using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public static class RoadPavementAnchorResolver
    {
        private const float MinPavementWidth = 0.05f;

        public static bool HasUsablePavement(NetInfo info)
        {
            return info != null && info.m_pavementWidth > MinPavementWidth;
        }

        public static bool TryGetPedestrianLaneExtents(NetInfo info, out float leftLanePosition, out float rightLanePosition, out bool hasPedestrianLanes)
        {
            leftLanePosition = 0f;
            rightLanePosition = 0f;
            hasPedestrianLanes = false;
            if (!HasUsablePavement(info) || info.m_lanes == null || info.m_lanes.Length == 0)
                return false;

            bool found = false;
            for (int i = 0; i < info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = info.m_lanes[i];
                if (lane == null || (lane.m_laneType & NetInfo.LaneType.Pedestrian) == 0)
                    continue;

                hasPedestrianLanes = true;
                if (!found)
                {
                    leftLanePosition = lane.m_position;
                    rightLanePosition = lane.m_position;
                    found = true;
                    continue;
                }

                if (lane.m_position < leftLanePosition)
                    leftLanePosition = lane.m_position;
                if (lane.m_position > rightLanePosition)
                    rightLanePosition = lane.m_position;
            }

            return found && Mathf.Abs(rightLanePosition - leftLanePosition) > 0.5f;
        }

        public static bool TryGetNearestPedestrianLanePosition(NetInfo info, float targetLateralPosition, out float lanePosition)
        {
            lanePosition = 0f;
            if (!HasUsablePavement(info) || info.m_lanes == null)
                return false;

            bool found = false;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = info.m_lanes[i];
                if (lane == null || (lane.m_laneType & NetInfo.LaneType.Pedestrian) == 0)
                    continue;

                float distance = Mathf.Abs(lane.m_position - targetLateralPosition);
                if (found && distance >= bestDistance)
                    continue;

                lanePosition = lane.m_position;
                bestDistance = distance;
                found = true;
            }

            return found;
        }

        public static bool TryGetPedestrianLaneSurfacePosition(ushort segmentId, Vector3 targetPosition, out Vector3 laneSurfacePosition)
        {
            laneSurfacePosition = targetPosition;
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null || !HasUsablePavement(segment.Info))
                return false;

            uint laneId;
            int laneIndex;
            float laneOffset;
            return segment.GetClosestLanePosition(
                targetPosition,
                NetInfo.LaneType.Pedestrian,
                VehicleInfo.VehicleType.None,
                VehicleInfo.VehicleCategory.None,
                out laneSurfacePosition,
                out laneId,
                out laneIndex,
                out laneOffset);
        }

        public static bool TryGetPedestrianLaneSurfacePosition(ushort segmentId, Vector3 targetPosition, float maxHorizontalDistance, out Vector3 laneSurfacePosition)
        {
            if (!TryGetPedestrianLaneSurfacePosition(segmentId, targetPosition, out laneSurfacePosition))
                return false;

            if (maxHorizontalDistance <= 0f || HorizontalDistance(targetPosition, laneSurfacePosition) > maxHorizontalDistance)
            {
                laneSurfacePosition = targetPosition;
                return false;
            }

            return true;
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }
    }
}
