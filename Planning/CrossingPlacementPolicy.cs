using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public struct CrossingPlacementPolicyResult
    {
        public readonly bool Success;
        public readonly string Message;
        public readonly ushort TargetNodeId;
        public readonly bool SuppressSurfaceCrossing;
        public readonly CrossingApplicationKind ApplicationKind;

        private CrossingPlacementPolicyResult(bool success, string message, ushort targetNodeId, bool suppressSurfaceCrossing, CrossingApplicationKind applicationKind)
        {
            Success = success;
            Message = message;
            TargetNodeId = targetNodeId;
            SuppressSurfaceCrossing = suppressSurfaceCrossing;
            ApplicationKind = applicationKind;
        }

        public static CrossingPlacementPolicyResult Valid(string message, ushort targetNodeId, bool suppressSurfaceCrossing, CrossingApplicationKind applicationKind)
        {
            return new CrossingPlacementPolicyResult(true, message, targetNodeId, suppressSurfaceCrossing, applicationKind);
        }

        public static CrossingPlacementPolicyResult Invalid(string message)
        {
            return new CrossingPlacementPolicyResult(false, message, 0, false, CrossingApplicationKind.None);
        }

        public CrossingPlacementResult ToPlacementResult()
        {
            return Success
                ? CrossingPlacementResult.Valid(Message)
                : CrossingPlacementResult.Invalid(Message);
        }
    }

    public static class CrossingPlacementPolicy
    {
        public static CrossingPlacementPolicyResult Evaluate(CrossingPlacementRecord placement)
        {
            return Evaluate(placement, false);
        }

        public static CrossingPlacementPolicyResult Evaluate(CrossingPlacementRecord placement, bool allowExistingSignalSurfaceCrossing)
        {
            if (placement.SegmentId == 0)
                return CrossingPlacementPolicyResult.Invalid("No road segment selected.");

            switch (placement.Mode)
            {
                case PedestrianToolMode.MidBlockCrossing:
                    return EvaluateSurfaceCrossing(placement);
                case PedestrianToolMode.SignalCrossing:
                    return EvaluateSignalCrossing(placement, allowExistingSignalSurfaceCrossing);
                case PedestrianToolMode.SubwayLink:
                    return EvaluateSubway(placement);
                case PedestrianToolMode.SubwayPointToPoint:
                    return EvaluatePointToPointSubwayEndpoint(placement);
                case PedestrianToolMode.PedestrianBridge:
                    return EvaluateBridge(placement);
                default:
                    return CrossingPlacementPolicyResult.Invalid("No pedestrian crossing tool selected.");
            }
        }

        private static CrossingPlacementPolicyResult EvaluateSurfaceCrossing(CrossingPlacementRecord placement)
        {
            if (!RoadPlacementRules.AllowsSurfaceCrossing(placement.SegmentId))
                return CrossingPlacementPolicyResult.Invalid("Surface crossings cannot be placed on highways or train tracks.");

            ushort exclusionNodeId;
            if (RoadPlacementRules.TryGetSurfacePlacementExclusionNode(placement.SegmentId, placement.WorldPosition, out exclusionNodeId))
                return CrossingPlacementPolicyResult.Invalid("Surface crossing cannot be placed here. Move outside the exclusion zone.");

            return CrossingPlacementPolicyResult.Valid("Surface crossing preview accepted.", 0, false, CrossingApplicationKind.SurfaceCrossing);
        }

        private static CrossingPlacementPolicyResult EvaluateSignalCrossing(CrossingPlacementRecord placement, bool allowExistingSurfaceCrossing)
        {
            if (!RoadPlacementRules.AllowsSurfaceCrossing(placement.SegmentId))
                return CrossingPlacementPolicyResult.Invalid("Signal crossings cannot be placed on highways or train tracks.");

            RoadSnapResult highlightedJoin;
            if (placement.TargetNodeId == 0
                || placement.SlotNumber != RoadSnapResolver.SegmentJoinSlot
                || !RoadSnapResolver.TryResolveSignalJoinNode(placement.TargetNodeId, allowExistingSurfaceCrossing, out highlightedJoin))
            {
                return CrossingPlacementPolicyResult.Invalid("Move the cursor to a highlighted point.");
            }

            return CrossingPlacementPolicyResult.Valid("Segment-join signal crossing preview accepted.", placement.TargetNodeId, false, CrossingApplicationKind.SignalizedSurfaceCrossing);
        }

        private static CrossingPlacementPolicyResult EvaluateSubway(CrossingPlacementRecord placement)
        {
            if (!RoadPlacementRules.AllowsSubwayPlacementTarget(placement.SegmentId))
                return CrossingPlacementPolicyResult.Invalid("Subways can only be placed on normal surface roads, surface/elevated train tracks, or terrain-level/elevated metro tracks.");

            if (RoadPlacementRules.IsGradeSeparatedPlacementTooCloseToStation(placement.WorldPosition))
                return CrossingPlacementPolicyResult.Invalid("Subways cannot be placed within 5f of a station.");

            if (RoadPlacementRules.IsRailOrMetroJunctionTooClose(placement.SegmentId, placement.WorldPosition))
                return CrossingPlacementPolicyResult.Invalid("Subways cannot be placed within 20f of rail or metro points/junctions.");

            bool roadTarget = RoadPlacementRules.IsRoadGradeSeparatedPlacementTarget(placement.SegmentId);
            ushort suppressionNodeId = roadTarget ? GetGradeSeparatedSurfaceCrossingNode(placement) : (ushort)0;
            if (suppressionNodeId == 0
                && roadTarget
                && RoadPlacementRules.IsBlockedGradeSeparatedJunctionApproach(placement.SegmentId, placement.WorldPosition))
            {
                return CrossingPlacementPolicyResult.Invalid("Subway link cannot be placed here. Move outside the exclusion zone.");
            }

            bool suppress = suppressionNodeId != 0;
            CrossingApplicationKind applicationKind = suppress
                ? CrossingApplicationKind.SubwayJunctionSuppressSurface
                : CrossingApplicationKind.SubwayLink;

            return CrossingPlacementPolicyResult.Valid(
                suppress ? "Junction subway link preview accepted." : "Subway link preview accepted.",
                suppressionNodeId,
                suppress,
                applicationKind);
        }

        private static CrossingPlacementPolicyResult EvaluatePointToPointSubwayEndpoint(CrossingPlacementRecord placement)
        {
            CrossingPlacementResult result = SubwayPointToPointPlacementPlanner.PreviewEndpoint(placement);
            return result.Success
                ? CrossingPlacementPolicyResult.Valid(result.Message, 0, false, CrossingApplicationKind.SubwayPointToPoint)
                : CrossingPlacementPolicyResult.Invalid(result.Message);
        }

        private static CrossingPlacementPolicyResult EvaluateBridge(CrossingPlacementRecord placement)
        {
            if (!RoadPlacementRules.AllowsPedestrianBridgePlacementTarget(placement.SegmentId))
                return CrossingPlacementPolicyResult.Invalid("Pedestrian bridges can only be placed on normal surface roads, surface train tracks, or terrain-level metro tracks.");

            if (RoadPlacementRules.IsGradeSeparatedPlacementTooCloseToStation(placement.WorldPosition))
                return CrossingPlacementPolicyResult.Invalid("Pedestrian bridges cannot be placed within 5f of a station.");

            if (RoadPlacementRules.IsRailOrMetroJunctionTooClose(placement.SegmentId, placement.WorldPosition))
                return CrossingPlacementPolicyResult.Invalid("Pedestrian bridges cannot be placed within 20f of rail or metro points/junctions.");

            if (RoadPlacementRules.IsPedestrianBridgeTooCloseToMonorailStationWithRoad(placement.WorldPosition))
                return CrossingPlacementPolicyResult.Invalid("Pedestrian bridge cannot be placed within 10f of a monorail station with road.");

            bool roadTarget = RoadPlacementRules.IsRoadGradeSeparatedPlacementTarget(placement.SegmentId);
            ushort suppressionNodeId = roadTarget ? GetGradeSeparatedSurfaceCrossingNode(placement) : (ushort)0;
            if (suppressionNodeId == 0
                && roadTarget
                && RoadPlacementRules.IsBlockedGradeSeparatedJunctionApproach(placement.SegmentId, placement.WorldPosition))
            {
                return CrossingPlacementPolicyResult.Invalid("Pedestrian bridge cannot be placed here. Move outside the exclusion zone.");
            }

            return CrossingPlacementPolicyResult.Valid(
                suppressionNodeId != 0 ? "Junction pedestrian bridge preview accepted." : "Pedestrian bridge preview accepted.",
                suppressionNodeId,
                suppressionNodeId != 0,
                CrossingApplicationKind.PedestrianBridge);
        }

        private static ushort GetGradeSeparatedSurfaceCrossingNode(CrossingPlacementRecord placement)
        {
            if (placement.TargetNodeId != 0 && RoadPlacementRules.IsThreePlusJunctionNode(placement.TargetNodeId))
                return placement.TargetNodeId;

            RoadPlacementRules.VanillaCrossingPoint crossingPoint;
            if (RoadPlacementRules.TryGetVanillaCrossingNear(
                    placement.SegmentId,
                    placement.WorldPosition,
                    RoadPlacementRules.SurfaceCrossingPostVanillaBuffer,
                    out crossingPoint))
            {
                return crossingPoint.NodeId;
            }

            return 0;
        }
    }
}
