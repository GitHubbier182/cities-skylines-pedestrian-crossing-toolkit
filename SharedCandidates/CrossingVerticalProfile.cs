using UnityEngine;

namespace PedestrianCrossingToolkit
{
    // Candidate for a shared dependency mod; kept local while the connector crossing model stabilizes.
    public static class CrossingVerticalProfile
    {
        public const float SurfaceConnectorLift = 0f;
        public const float BridgeDeckHeight = 5.0f;
        public const float SubwayTunnelDepth = -6.0f;

        public static float GetPathOffset(CrossingPathWorkOrderKind kind)
        {
            switch (kind)
            {
                case CrossingPathWorkOrderKind.BridgePath:
                    return BridgeDeckHeight;
                case CrossingPathWorkOrderKind.SubwayPath:
                    return SubwayTunnelDepth;
                case CrossingPathWorkOrderKind.SignalizedSurfacePath:
                case CrossingPathWorkOrderKind.SurfacePath:
                    return SurfaceConnectorLift;
                default:
                    return 0f;
            }
        }

        public static string GetLinkLabel(CrossingConnectivityLink link)
        {
            switch (link.Kind)
            {
                case CrossingConnectivityLinkKind.SubwaySpan:
                case CrossingConnectivityLinkKind.JunctionSubwayApproach:
                    return link.UsesLaneTargets ? "SUB -6m" : "SUB edge -6m";
                case CrossingConnectivityLinkKind.PedestrianBridgeSpan:
                case CrossingConnectivityLinkKind.JunctionBridgeApproach:
                    return link.UsesLaneTargets ? "BRG +5m" : "BRG edge +5m";
                case CrossingConnectivityLinkKind.SignalizedSurfaceSpan:
                    return "SIG surface";
                default:
                    return link.UsesLaneTargets ? "lane link" : "edge link";
            }
        }
    }
}
