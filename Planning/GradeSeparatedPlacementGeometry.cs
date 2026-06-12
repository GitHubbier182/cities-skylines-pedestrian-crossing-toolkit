using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public struct GradeSeparatedPlacementGeometry
    {
        public readonly CrossingConnectivityLinkKind LinkKind;
        public readonly Vector3 FirstDeckPosition;
        public readonly Vector3 SecondDeckPosition;
        public readonly Vector3 AccessDirection;
        public readonly bool IsJunctionPlacement;
        public readonly bool UsesLaneTargets;

        public GradeSeparatedPlacementGeometry(CrossingConnectivityLinkKind linkKind, Vector3 firstDeckPosition, Vector3 secondDeckPosition, Vector3 accessDirection, bool isJunctionPlacement, bool usesLaneTargets)
        {
            LinkKind = linkKind;
            FirstDeckPosition = firstDeckPosition;
            SecondDeckPosition = secondDeckPosition;
            AccessDirection = accessDirection;
            IsJunctionPlacement = isJunctionPlacement;
            UsesLaneTargets = usesLaneTargets;
        }
    }

    public static class GradeSeparatedPlacementGeometryResolver
    {
        private const float RoadEdgeLandingSetback = 5f;
        private const float SubwayEntranceRoadOffset = 2f;
        private const float MaxGradeSeparatedPedestrianLaneSurfaceSnapDistance = 8f;

        public static bool IsGradeSeparated(CrossingApplicationKind kind)
        {
            return kind == CrossingApplicationKind.PedestrianBridge
                   || kind == CrossingApplicationKind.SubwayLink
                   || kind == CrossingApplicationKind.SubwayPointToPoint
                   || kind == CrossingApplicationKind.SubwayJunctionSuppressSurface;
        }

        public static bool TryBuild(CrossingPlacementAsset asset, out GradeSeparatedPlacementGeometry geometry)
        {
            geometry = default(GradeSeparatedPlacementGeometry);
            if (!asset.Plan.IsValid || !IsGradeSeparated(asset.Plan.ApplicationKind))
                return false;

            if (asset.Plan.ApplicationKind == CrossingApplicationKind.SubwayPointToPoint)
                return SubwayPointToPointPlacementPlanner.TryBuildGeometry(asset, out geometry);

            NetManager netManager = NetManager.instance;
            if (netManager == null
                || asset.Placement.SegmentId == 0
                || asset.Placement.SegmentId >= netManager.m_segments.m_size)
            {
                return false;
            }

            ref NetSegment segment = ref netManager.m_segments.m_buffer[asset.Placement.SegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0
                || segment.Info == null
                || !AllowsPlacementTargetForKind(asset))
            {
                return false;
            }

            Vector3 roadDirection;
            if (!TryGetRoadDirection(asset, netManager, ref segment, out roadDirection))
                return false;

            Vector3 crossingDirection = new Vector3(-roadDirection.z, 0f, roadDirection.x);
            if (Vector3.Dot(crossingDirection, asset.Plan.CrossingDirection) < 0f)
                crossingDirection = -crossingDirection;

            float firstLateralOffset;
            float secondLateralOffset;
            bool hasPedestrianLanes = false;
            bool usesLaneTargets = true;
            bool forceOffRoadSubwayLandings = ShouldUseOffRoadSubwayLandings(asset);
            string endpointSource = "pedestrian-lanes";
            if (forceOffRoadSubwayLandings
                || !RoadPavementAnchorResolver.TryGetPedestrianLaneExtents(segment.Info, out firstLateralOffset, out secondLateralOffset, out hasPedestrianLanes))
            {
                if (!forceOffRoadSubwayLandings && hasPedestrianLanes)
                {
                    Debug.Log("[PedestrianCrossingToolkit] Grade separated geometry rejected: unusable pedestrian lane extents for asset="
                              + asset.Id
                              + " segment="
                              + asset.Placement.SegmentId);
                    return false;
                }

                float roadHalfWidth = Mathf.Max(
                    CrossingPlacementPlanner.EstimateRoadHalfWidthForInfo(segment.Info),
                    RoadSurfacePlacementGuard.EstimateCarriageHalfWidth(segment.Info));
                float landingHalfWidth = roadHalfWidth + RoadEdgeLandingSetback;
                firstLateralOffset = -landingHalfWidth;
                secondLateralOffset = landingHalfWidth;
                usesLaneTargets = false;
                endpointSource = forceOffRoadSubwayLandings ? "non-road-subway-landings" : "road-edge-landings";
                Debug.Log("[PedestrianCrossingToolkit] Grade separated geometry using off-road landing endpoints: asset="
                          + asset.Id
                          + " segment="
                          + asset.Placement.SegmentId
                          + " source="
                          + endpointSource
                          + " roadHalfWidth="
                          + roadHalfWidth.ToString("0.0")
                          + " setback="
                          + RoadEdgeLandingSetback.ToString("0.0"));
            }

            bool junction = false;
            Vector3 center = asset.Plan.Center;
            CrossingConnectivityLinkKind linkKind = GetLinkKind(asset.Plan.ApplicationKind, junction);

            Vector3 first = center + crossingDirection * firstLateralOffset;
            Vector3 second = center + crossingDirection * secondLateralOffset;
            if (linkKind == CrossingConnectivityLinkKind.SubwaySpan
                || linkKind == CrossingConnectivityLinkKind.JunctionSubwayApproach)
            {
                Vector3 subwayAccessDirection = GetSubwayAccessDirectionAwayFromNearestNode(netManager, ref segment, center, roadDirection);
                first += subwayAccessDirection * SubwayEntranceRoadOffset;
                second += subwayAccessDirection * SubwayEntranceRoadOffset;
            }

            if (usesLaneTargets)
            {
                Vector3 firstLaneSurface;
                if (TryGetPedestrianLaneSurfacePosition(asset.Placement.SegmentId, first, linkKind, out firstLaneSurface))
                    first = firstLaneSurface;

                Vector3 secondLaneSurface;
                if (TryGetPedestrianLaneSurfacePosition(asset.Placement.SegmentId, second, linkKind, out secondLaneSurface))
                    second = secondLaneSurface;
            }

            geometry = new GradeSeparatedPlacementGeometry(linkKind, first, second, roadDirection, junction, usesLaneTargets);
            Debug.Log("[PedestrianCrossingToolkit] Grade separated geometry resolved: asset="
                      + asset.Id
                      + " segment="
                      + asset.Placement.SegmentId
                      + " kind="
                      + linkKind
                      + " junction="
                      + junction
                      + " center="
                      + center
                      + " roadDirection="
                      + roadDirection
                      + " lanes="
                      + firstLateralOffset.ToString("0.00")
                      + "/"
                      + secondLateralOffset.ToString("0.00")
                      + " source="
                      + endpointSource
                      + " first="
                      + first
                      + " second="
                      + second);
            return true;
        }

        private static bool ShouldUseOffRoadSubwayLandings(CrossingPlacementAsset asset)
        {
            if (!RoadPlacementRules.IsNonRoadGradeSeparatedPlacementTarget(asset.Placement.SegmentId))
                return false;

            return asset.Plan.ApplicationKind == CrossingApplicationKind.SubwayLink
                   || asset.Plan.ApplicationKind == CrossingApplicationKind.SubwayJunctionSuppressSurface;
        }

        private static bool AllowsPlacementTargetForKind(CrossingPlacementAsset asset)
        {
            switch (asset.Plan.ApplicationKind)
            {
                case CrossingApplicationKind.PedestrianBridge:
                    return RoadPlacementRules.AllowsPedestrianBridgePlacement(asset.Placement.SegmentId, asset.Placement.WorldPosition);
                case CrossingApplicationKind.SubwayLink:
                case CrossingApplicationKind.SubwayJunctionSuppressSurface:
                    return RoadPlacementRules.AllowsSubwayPlacement(asset.Placement.SegmentId, asset.Placement.WorldPosition);
                default:
                    return RoadPlacementRules.AllowsGradeSeparatedPlacementTarget(asset.Placement.SegmentId);
            }
        }

        private static Vector3 GetSubwayAccessDirectionAwayFromNearestNode(NetManager netManager, ref NetSegment segment, Vector3 center, Vector3 roadDirection)
        {
            if (segment.m_startNode == 0
                || segment.m_endNode == 0
                || segment.m_startNode >= netManager.m_nodes.m_size
                || segment.m_endNode >= netManager.m_nodes.m_size)
            {
                return roadDirection;
            }

            Vector3 start = netManager.m_nodes.m_buffer[segment.m_startNode].m_position;
            Vector3 end = netManager.m_nodes.m_buffer[segment.m_endNode].m_position;
            return Vector3.SqrMagnitude(center - start) <= Vector3.SqrMagnitude(center - end)
                ? roadDirection
                : -roadDirection;
        }

        private static bool TryGetPedestrianLaneSurfacePosition(ushort segmentId, Vector3 targetPosition, CrossingConnectivityLinkKind linkKind, out Vector3 laneSurfacePosition)
        {
            if (IsGuardedGradeSeparatedLinkKind(linkKind))
            {
                return RoadPavementAnchorResolver.TryGetPedestrianLaneSurfacePosition(
                    segmentId,
                    targetPosition,
                    MaxGradeSeparatedPedestrianLaneSurfaceSnapDistance,
                    out laneSurfacePosition);
            }

            return RoadPavementAnchorResolver.TryGetPedestrianLaneSurfacePosition(segmentId, targetPosition, out laneSurfacePosition);
        }

        private static bool IsGuardedGradeSeparatedLinkKind(CrossingConnectivityLinkKind linkKind)
        {
            return linkKind == CrossingConnectivityLinkKind.SubwaySpan
                   || linkKind == CrossingConnectivityLinkKind.JunctionSubwayApproach
                   || linkKind == CrossingConnectivityLinkKind.PedestrianBridgeSpan
                   || linkKind == CrossingConnectivityLinkKind.JunctionBridgeApproach;
        }

        public static bool TryGetAccessDirection(CrossingConnectivityLink link, out Vector3 direction)
        {
            return TryGetAccessDirection(link, string.Empty, out direction);
        }

        public static bool TryGetAccessDirection(CrossingConnectivityLink link, string endpointName, out Vector3 direction)
        {
            direction = Vector3.zero;
            CrossingPlacementAsset asset;
            if (!CrossingPlacementRegistry.TryGetAssetById(link.AssetId, out asset) || !asset.Plan.IsValid)
                return false;

            if (asset.Plan.ApplicationKind == CrossingApplicationKind.SubwayPointToPoint
                && TryGetPointToPointEndpointRoadDirection(asset, endpointName, out direction))
            {
                return true;
            }

            NetManager netManager = NetManager.instance;
            if (netManager == null
                || link.SegmentId == 0
                || link.SegmentId >= netManager.m_segments.m_size)
            {
                return false;
            }

            ref NetSegment segment = ref netManager.m_segments.m_buffer[link.SegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return false;

            return TryGetRoadDirection(asset, netManager, ref segment, out direction);
        }

        private static bool TryGetPointToPointEndpointRoadDirection(CrossingPlacementAsset asset, string endpointName, out Vector3 direction)
        {
            direction = Vector3.zero;
            CrossingPlacementRecord placement = asset.Placement;
            bool useSecondary = string.Equals(endpointName, "B", System.StringComparison.OrdinalIgnoreCase);
            ushort segmentId = useSecondary ? placement.SecondarySegmentId : placement.SegmentId;
            float segmentPosition = useSecondary ? placement.SecondarySegmentPosition : placement.SegmentPosition;
            Vector3 worldPosition = useSecondary ? placement.SecondaryWorldPosition : placement.WorldPosition;

            NetManager netManager = NetManager.instance;
            if (netManager == null
                || segmentId == 0
                || segmentId >= netManager.m_segments.m_size)
            {
                return false;
            }

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null)
                return false;

            Vector3 center;
            Vector3 roadRight;
            if (!CrossingPlacementPlanner.TryGetRoadFrameForPlacement(netManager, ref segment, segmentPosition, worldPosition, out center, out direction, out roadRight))
                return false;

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return false;

            direction.Normalize();
            return true;
        }

        private static bool TryGetRoadDirection(CrossingPlacementAsset asset, NetManager netManager, ref NetSegment segment, out Vector3 roadDirection)
        {
            roadDirection = Vector3.zero;

            roadDirection = asset.Plan.RoadDirection;
            roadDirection.y = 0f;
            if (roadDirection.sqrMagnitude <= 0.01f)
                return false;

            roadDirection.Normalize();
            return true;
        }

        private static CrossingConnectivityLinkKind GetLinkKind(CrossingApplicationKind applicationKind, bool junction)
        {
            switch (applicationKind)
            {
                case CrossingApplicationKind.SubwayJunctionSuppressSurface:
                    return CrossingConnectivityLinkKind.SubwaySpan;
                case CrossingApplicationKind.SubwayLink:
                    return CrossingConnectivityLinkKind.SubwaySpan;
                case CrossingApplicationKind.SubwayPointToPoint:
                    return CrossingConnectivityLinkKind.SubwaySpan;
                case CrossingApplicationKind.PedestrianBridge:
                    return CrossingConnectivityLinkKind.PedestrianBridgeSpan;
                default:
                    return CrossingConnectivityLinkKind.SurfaceSpan;
            }
        }
    }
}
