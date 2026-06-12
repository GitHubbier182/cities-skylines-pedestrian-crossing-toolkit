namespace PedestrianCrossingToolkit
{
    public static class SubwayPointToPointTool
    {
        private static CrossingPlacementRecord _startEndpoint = CrossingPlacementRecord.None;

        public static bool HasStartEndpoint
        {
            get { return _startEndpoint.IsValid && _startEndpoint.SegmentId != 0; }
        }

        public static CrossingPlacementRecord StartEndpoint
        {
            get { return _startEndpoint; }
        }

        public static void SetStartEndpoint(CrossingPlacementRecord endpoint)
        {
            _startEndpoint = endpoint;
        }

        public static void Reset()
        {
            _startEndpoint = CrossingPlacementRecord.None;
        }

        public static CrossingPlacementResult PreviewEndpoint(CrossingPlacementRecord endpoint)
        {
            return SubwayPointToPointPlacementPlanner.PreviewEndpoint(endpoint);
        }

        public static CrossingPlacementResult PreviewEndpoint(CrossingPlacementRecord endpoint, out CrossingPlacementRecord previewEndpoint)
        {
            return SubwayPointToPointPlacementPlanner.PreviewEndpoint(endpoint, out previewEndpoint);
        }

        public static CrossingPlacementResult TryCreateRoute(CrossingPlacementRecord endEndpoint, out CrossingPlacementRecord route)
        {
            route = CrossingPlacementRecord.None;
            if (!HasStartEndpoint)
                return CrossingPlacementResult.Invalid("Select a start subway entrance first.");

            return SubwayPointToPointPlacementPlanner.TryCreateRoute(_startEndpoint, endEndpoint, out route);
        }
    }
}
