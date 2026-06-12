namespace PedestrianCrossingToolkit
{
    public static class CrossingPlacementConflictValidator
    {
        public static bool TryValidateAndAdjust(CrossingPlacementRecord placement, CrossingPlacementPlan plan, out CrossingPlacementPlan adjustedPlan, out string message)
        {
            adjustedPlan = plan;
            message = string.Empty;
            return true;
        }
    }
}
