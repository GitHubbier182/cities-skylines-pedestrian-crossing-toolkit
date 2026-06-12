using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public struct SubwayPointToPointEndpoint
    {
        public readonly CrossingPlacementRecord Placement;
        public readonly Vector3 EntrancePosition;

        public SubwayPointToPointEndpoint(CrossingPlacementRecord placement, Vector3 entrancePosition)
        {
            Placement = placement;
            EntrancePosition = entrancePosition;
        }
    }

    public static class SubwayPointToPointPlacementPlanner
    {
        public const int CurrentEntranceCount = 2;
        public const float MaxEntranceDistance = 50f;
        private const float MinEntranceDistance = 2f;
        private const float RoadEdgeLandingSetback = 5f;
        private const float MaxPedestrianLaneSurfaceSnapDistance = 8f;

        public static CrossingPlacementResult PreviewEndpoint(CrossingPlacementRecord endpoint)
        {
            CrossingPlacementRecord previewEndpoint;
            return PreviewEndpoint(endpoint, out previewEndpoint);
        }

        public static CrossingPlacementResult PreviewEndpoint(CrossingPlacementRecord endpoint, out CrossingPlacementRecord previewEndpoint)
        {
            CrossingPlacementResult endpointResult = ValidateEndpoint(endpoint);
            previewEndpoint = new CrossingPlacementRecord(
                endpoint.Mode,
                endpoint.SegmentId,
                endpoint.SegmentPosition,
                endpoint.WorldPosition,
                endpointResult.Success,
                endpointResult.Message,
                endpoint.NearNode,
                endpoint.SlotNumber,
                endpoint.IsEndSegmentSlot,
                endpoint.TargetNodeId);
            if (!endpointResult.Success)
                return endpointResult;

            SubwayPointToPointEndpoint resolvedEndpoint;
            endpointResult = TryResolveEndpoint(endpoint, out resolvedEndpoint);
            if (!endpointResult.Success)
            {
                previewEndpoint = new CrossingPlacementRecord(
                    endpoint.Mode,
                    endpoint.SegmentId,
                    endpoint.SegmentPosition,
                    endpoint.WorldPosition,
                    false,
                    endpointResult.Message,
                    endpoint.NearNode,
                    endpoint.SlotNumber,
                    endpoint.IsEndSegmentSlot,
                    endpoint.TargetNodeId);
                return endpointResult;
            }

            previewEndpoint = new CrossingPlacementRecord(
                endpoint.Mode,
                endpoint.SegmentId,
                endpoint.SegmentPosition,
                resolvedEndpoint.EntrancePosition,
                true,
                "Subway entrance accepted.",
                endpoint.NearNode,
                endpoint.SlotNumber,
                endpoint.IsEndSegmentSlot,
                endpoint.TargetNodeId);

            if (!SubwayPointToPointTool.HasStartEndpoint)
            {
                previewEndpoint = new CrossingPlacementRecord(
                    endpoint.Mode,
                    endpoint.SegmentId,
                    endpoint.SegmentPosition,
                    resolvedEndpoint.EntrancePosition,
                    true,
                    "Start subway entrance accepted.",
                    endpoint.NearNode,
                    endpoint.SlotNumber,
                    endpoint.IsEndSegmentSlot,
                    endpoint.TargetNodeId);
                return CrossingPlacementResult.Valid("Start subway entrance accepted.");
            }

            CrossingPlacementRecord route;
            CrossingPlacementResult routeResult = TryCreateRoute(SubwayPointToPointTool.StartEndpoint, previewEndpoint, out route);
            previewEndpoint = new CrossingPlacementRecord(
                endpoint.Mode,
                endpoint.SegmentId,
                endpoint.SegmentPosition,
                resolvedEndpoint.EntrancePosition,
                routeResult.Success,
                routeResult.Message,
                endpoint.NearNode,
                endpoint.SlotNumber,
                endpoint.IsEndSegmentSlot,
                endpoint.TargetNodeId);
            return routeResult;
        }

        public static CrossingPlacementResult TryCreateRoute(CrossingPlacementRecord start, CrossingPlacementRecord end, out CrossingPlacementRecord route)
        {
            route = CrossingPlacementRecord.None;

            SubwayPointToPointEndpoint startEndpoint;
            CrossingPlacementResult startResult = TryResolveEndpoint(start, out startEndpoint);
            if (!startResult.Success)
                return startResult;

            SubwayPointToPointEndpoint endEndpoint;
            CrossingPlacementResult endResult = TryResolveEndpoint(end, out endEndpoint);
            if (!endResult.Success)
                return endResult;

            float distance = HorizontalDistance(startEndpoint.EntrancePosition, endEndpoint.EntrancePosition);
            if (distance < MinEntranceDistance)
                return CrossingPlacementResult.Invalid("Subway entrances need a little separation.");

            if (distance > MaxEntranceDistance)
                return CrossingPlacementResult.Invalid("Subway entrances must be within " + MaxEntranceDistance.ToString("0") + "f.");

            route = new CrossingPlacementRecord(
                PedestrianToolMode.SubwayPointToPoint,
                start.SegmentId,
                start.SegmentPosition,
                startEndpoint.EntrancePosition,
                true,
                "Point-to-point subway preview accepted.",
                start.NearNode,
                start.SlotNumber,
                start.IsEndSegmentSlot,
                start.TargetNodeId,
                true,
                end.SegmentId,
                end.SegmentPosition,
                endEndpoint.EntrancePosition,
                end.NearNode,
                end.SlotNumber,
                end.IsEndSegmentSlot,
                end.TargetNodeId);

            return CrossingPlacementResult.Valid("Point-to-point subway preview accepted.");
        }

        public static CrossingPlacementPlan Build(CrossingPlacementRecord record)
        {
            if (!record.IsValid || record.Mode != PedestrianToolMode.SubwayPointToPoint || !record.HasSecondaryPoint)
                return CrossingPlacementPlan.Invalid;

            CrossingPlacementRecord start = new CrossingPlacementRecord(
                PedestrianToolMode.SubwayPointToPoint,
                record.SegmentId,
                record.SegmentPosition,
                record.WorldPosition,
                true,
                string.Empty,
                record.NearNode,
                record.SlotNumber,
                record.IsEndSegmentSlot,
                record.TargetNodeId);

            CrossingPlacementRecord end = new CrossingPlacementRecord(
                PedestrianToolMode.SubwayPointToPoint,
                record.SecondarySegmentId,
                record.SecondarySegmentPosition,
                record.SecondaryWorldPosition,
                true,
                string.Empty,
                record.SecondaryNearNode,
                record.SecondarySlotNumber,
                record.SecondaryIsEndSegmentSlot,
                record.SecondaryTargetNodeId);

            CrossingPlacementRecord route;
            CrossingPlacementResult result = TryCreateRoute(start, end, out route);
            if (!result.Success)
                return CrossingPlacementPlan.Invalid;

            Vector3 first = route.WorldPosition;
            Vector3 second = route.SecondaryWorldPosition;
            Vector3 span = second - first;
            span.y = 0f;
            float distance = span.magnitude;
            if (distance <= 0.01f)
                return CrossingPlacementPlan.Invalid;

            Vector3 roadDirection = span / distance;
            Vector3 crossingDirection = new Vector3(-roadDirection.z, 0f, roadDirection.x);
            Vector3 center = (first + second) * 0.5f;
            return new CrossingPlacementPlan(
                true,
                PedestrianToolMode.SubwayPointToPoint,
                route.SegmentId,
                center,
                roadDirection,
                crossingDirection,
                first,
                second,
                distance,
                0,
                0,
                new Vector3[0],
                false,
                CrossingApplicationKind.SubwayPointToPoint);
        }

        public static bool TryBuildGeometry(CrossingPlacementAsset asset, out GradeSeparatedPlacementGeometry geometry)
        {
            geometry = default(GradeSeparatedPlacementGeometry);
            if (!asset.Plan.IsValid || asset.Plan.ApplicationKind != CrossingApplicationKind.SubwayPointToPoint)
                return false;

            Vector3 first = asset.Plan.LeftEdge;
            Vector3 second = asset.Plan.RightEdge;
            Vector3 direction = second - first;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return false;

            direction.Normalize();
            bool usesLaneTargets = EndpointUsesPedestrianLane(asset.Placement, false)
                                   && EndpointUsesPedestrianLane(asset.Placement, true);
            geometry = new GradeSeparatedPlacementGeometry(CrossingConnectivityLinkKind.SubwaySpan, first, second, direction, false, usesLaneTargets);
            return true;
        }

        private static CrossingPlacementResult ValidateEndpoint(CrossingPlacementRecord endpoint)
        {
            if (endpoint.SegmentId == 0)
                return CrossingPlacementResult.Invalid("No road segment selected.");

            if (!RoadPlacementRules.AllowsManualSubwayPlacementTarget(endpoint.SegmentId))
                return CrossingPlacementResult.Invalid("Manual subway entrances can only be placed by normal surface roads.");

            if (RoadPlacementRules.IsGradeSeparatedPlacementTooCloseToStation(endpoint.WorldPosition))
                return CrossingPlacementResult.Invalid("Manual subway entrances cannot be placed within 5f of a station.");

            if (RoadPlacementRules.IsRoadGradeSeparatedPlacementTarget(endpoint.SegmentId)
                && RoadPlacementRules.IsBlockedGradeSeparatedJunctionApproach(endpoint.SegmentId, endpoint.WorldPosition))
                return CrossingPlacementResult.Invalid("Point-to-point subway cannot be placed here. Move outside the exclusion zone.");

            SubwayPointToPointEndpoint resolved;
            return TryResolveEndpoint(endpoint, out resolved);
        }

        private static CrossingPlacementResult TryResolveEndpoint(CrossingPlacementRecord endpoint, out SubwayPointToPointEndpoint resolved)
        {
            resolved = default(SubwayPointToPointEndpoint);
            CrossingPlacementResult valid = ValidateEndpointSurface(endpoint);
            if (!valid.Success)
                return valid;

            Vector3 entrancePosition;
            if (!TryResolveEntrancePosition(endpoint, out entrancePosition))
                return CrossingPlacementResult.Invalid("Unable to resolve a subway entrance position.");

            resolved = new SubwayPointToPointEndpoint(endpoint, entrancePosition);
            return CrossingPlacementResult.Valid("Subway entrance accepted.");
        }

        private static CrossingPlacementResult ValidateEndpointSurface(CrossingPlacementRecord endpoint)
        {
            if (endpoint.SegmentId == 0)
                return CrossingPlacementResult.Invalid("No road segment selected.");

            NetManager netManager = NetManager.instance;
            if (netManager == null || endpoint.SegmentId >= netManager.m_segments.m_size)
                return CrossingPlacementResult.Invalid("Invalid road segment.");

            ref NetSegment segment = ref netManager.m_segments.m_buffer[endpoint.SegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0
                || segment.Info == null
                || !RoadPlacementRules.AllowsManualSubwayPlacementTarget(endpoint.SegmentId))
            {
                return CrossingPlacementResult.Invalid("Selected object is not a normal surface road.");
            }

            if (RoadPlacementRules.IsGradeSeparatedPlacementTooCloseToStation(endpoint.WorldPosition))
                return CrossingPlacementResult.Invalid("Manual subway entrances cannot be placed within 5f of a station.");

            return CrossingPlacementResult.Valid("Subway entrance accepted.");
        }

        private static bool TryResolveEntrancePosition(CrossingPlacementRecord endpoint, out Vector3 entrancePosition)
        {
            entrancePosition = endpoint.WorldPosition;
            NetManager netManager = NetManager.instance;
            if (netManager == null || endpoint.SegmentId == 0 || endpoint.SegmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[endpoint.SegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null)
                return false;

            Vector3 center;
            Vector3 roadDirection;
            Vector3 roadRight;
            if (!CrossingPlacementPlanner.TryGetRoadFrameForPlacement(netManager, ref segment, endpoint.SegmentPosition, endpoint.WorldPosition, out center, out roadDirection, out roadRight))
                return false;

            float targetLateral = Vector3.Dot(endpoint.WorldPosition - center, roadRight);
            float resolvedLateral;
            if (RoadPavementAnchorResolver.TryGetNearestPedestrianLanePosition(segment.Info, targetLateral, out resolvedLateral))
            {
                entrancePosition = center + roadRight * resolvedLateral;
                Vector3 laneSurfacePosition;
                if (RoadPavementAnchorResolver.TryGetPedestrianLaneSurfacePosition(endpoint.SegmentId, entrancePosition, MaxPedestrianLaneSurfaceSnapDistance, out laneSurfacePosition))
                    entrancePosition = laneSurfacePosition;
                else
                    entrancePosition.y = ResolveEndpointHeight(endpoint.SegmentId, entrancePosition, endpoint.WorldPosition.y, true, false);

                return true;
            }

            float roadHalfWidth = Mathf.Max(
                CrossingPlacementPlanner.EstimateRoadHalfWidthForInfo(segment.Info),
                RoadSurfacePlacementGuard.EstimateCarriageHalfWidth(segment.Info));
            float side = targetLateral < 0f ? -1f : 1f;
            if (Mathf.Abs(targetLateral) <= 0.1f)
                side = 1f;

            entrancePosition = center + roadRight * side * (roadHalfWidth + RoadEdgeLandingSetback);
            entrancePosition.y = ResolveEndpointHeight(endpoint.SegmentId, entrancePosition, endpoint.WorldPosition.y, false);
            return true;
        }

        private static bool EndpointUsesPedestrianLane(CrossingPlacementRecord placement, bool secondary)
        {
            ushort segmentId = secondary ? placement.SecondarySegmentId : placement.SegmentId;
            float segmentPosition = secondary ? placement.SecondarySegmentPosition : placement.SegmentPosition;
            Vector3 worldPosition = secondary ? placement.SecondaryWorldPosition : placement.WorldPosition;

            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null)
                return false;

            Vector3 center;
            Vector3 roadDirection;
            Vector3 roadRight;
            if (!CrossingPlacementPlanner.TryGetRoadFrameForPlacement(netManager, ref segment, segmentPosition, worldPosition, out center, out roadDirection, out roadRight))
                return false;

            float targetLateral = Vector3.Dot(worldPosition - center, roadRight);
            float resolvedLateral;
            return RoadPavementAnchorResolver.TryGetNearestPedestrianLanePosition(segment.Info, targetLateral, out resolvedLateral);
        }

        private static float ResolveEndpointHeight(ushort segmentId, Vector3 position, float fallbackHeight, bool onPedestrianLane)
        {
            return ResolveEndpointHeight(segmentId, position, fallbackHeight, onPedestrianLane, true);
        }

        private static float ResolveEndpointHeight(ushort segmentId, Vector3 position, float fallbackHeight, bool onPedestrianLane, bool allowLaneSurfaceSnap)
        {
            float height = fallbackHeight;

            Vector3 laneSurfacePosition;
            if (onPedestrianLane && allowLaneSurfaceSnap && RoadPavementAnchorResolver.TryGetPedestrianLaneSurfacePosition(segmentId, position, out laneSurfacePosition))
            {
                return laneSurfacePosition.y;
            }

            TerrainManager terrainManager = TerrainManager.instance;
            if (terrainManager != null)
            {
                float terrainHeight = terrainManager.SampleRawHeightSmooth(position);
                height = onPedestrianLane
                    ? Mathf.Max(height, terrainHeight)
                    : terrainHeight;
            }

            return height;
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }
    }
}
