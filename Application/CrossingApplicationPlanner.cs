namespace PedestrianCrossingToolkit
{
    public static class CrossingApplicationPlanner
    {
        public static CrossingApplicationKind Determine(CrossingPlacementRecord record)
        {
            switch (record.Mode)
            {
                case PedestrianToolMode.MidBlockCrossing:
                    return CrossingApplicationKind.SurfaceCrossing;
                case PedestrianToolMode.SignalCrossing:
                    return CrossingApplicationKind.SignalizedSurfaceCrossing;
                case PedestrianToolMode.SubwayLink:
                    return CrossingApplicationKind.SubwayLink;
                case PedestrianToolMode.SubwayPointToPoint:
                    return CrossingApplicationKind.SubwayPointToPoint;
                case PedestrianToolMode.PedestrianBridge:
                    return CrossingApplicationKind.PedestrianBridge;
                default:
                    return CrossingApplicationKind.None;
            }
        }

        public static string GetShortLabel(CrossingApplicationKind kind)
        {
            switch (kind)
            {
                case CrossingApplicationKind.SurfaceCrossing:
                    return "X";
                case CrossingApplicationKind.SignalizedSurfaceCrossing:
                    return "SIG";
                case CrossingApplicationKind.SubwayLink:
                    return "SUB";
                case CrossingApplicationKind.SubwayPointToPoint:
                    return "SUB2";
                case CrossingApplicationKind.SubwayJunctionSuppressSurface:
                    return "OFF";
                case CrossingApplicationKind.PedestrianBridge:
                    return "BRG";
                default:
                    return "-";
            }
        }
    }
}
