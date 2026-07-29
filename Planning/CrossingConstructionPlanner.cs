using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public struct CrossingConstructionSummary
    {
        public static readonly CrossingConstructionSummary Empty = new CrossingConstructionSummary(0, 0, 0, 0, 0, 0, 0, 0, false);

        public readonly int TotalWorkItems;
        public readonly int PathWorkItems;
        public readonly int ConnectorWorkItems;
        public readonly int AccessAssetWorkItems;
        public readonly int PrefabReady;
        public readonly int MissingPrefab;
        public readonly int UnresolvedLandings;
        public readonly int RoadEdgeLandingPaths;
        public readonly bool ReadyForBuild;

        public CrossingConstructionSummary(int totalWorkItems, int pathWorkItems, int connectorWorkItems, int accessAssetWorkItems, int prefabReady, int missingPrefab, int unresolvedLandings, int roadEdgeLandingPaths, bool readyForManagedBuild)
        {
            TotalWorkItems = totalWorkItems;
            PathWorkItems = pathWorkItems;
            ConnectorWorkItems = connectorWorkItems;
            AccessAssetWorkItems = accessAssetWorkItems;
            PrefabReady = prefabReady;
            MissingPrefab = missingPrefab;
            UnresolvedLandings = unresolvedLandings;
            RoadEdgeLandingPaths = roadEdgeLandingPaths;
            ReadyForBuild = readyForManagedBuild;
        }

        public string ToLogString()
        {
            return "totalWorkItems=" + TotalWorkItems
                   + " paths=" + PathWorkItems
                   + " connectors=" + ConnectorWorkItems
                   + " accessAssets=" + AccessAssetWorkItems
                   + " prefabReady=" + PrefabReady
                   + " missingPrefab=" + MissingPrefab
                   + " unresolvedLandings=" + UnresolvedLandings
                   + " roadEdgeLandingPaths=" + RoadEdgeLandingPaths
                   + " connectorBuildReady=" + ReadyForBuild;
        }
    }

    public static class CrossingConstructionPlanner
    {
        private static CrossingConstructionSummary _lastSummary = CrossingConstructionSummary.Empty;

        public static CrossingConstructionSummary LastSummary
        {
            get { return _lastSummary; }
        }

        public static void Refresh(string reason, CrossingPathWorkOrderSummary pathSummary, CrossingLandingConnectorSummary connectorSummary, int accessAssetCount)
        {
            int totalWorkItems = pathSummary.Total + connectorSummary.Connected + accessAssetCount;
            int prefabReady = pathSummary.PrefabReady + connectorSummary.PrefabReady;
            int missingPrefab = pathSummary.MissingPrefab + connectorSummary.MissingPrefab;
            bool buildReady = totalWorkItems > 0 && missingPrefab == 0;

            _lastSummary = new CrossingConstructionSummary(
                totalWorkItems,
                pathSummary.Total,
                connectorSummary.Connected,
                accessAssetCount,
                prefabReady,
                missingPrefab,
                connectorSummary.Unresolved,
                pathSummary.RoadEdgeLandingPaths,
                buildReady);

            Debug.Log("[PedestrianCrossingToolkit] Construction planning: reason=" + reason + " " + _lastSummary.ToLogString());

            if (connectorSummary.Unresolved > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Construction planning note: unresolvedLandings="
                          + connectorSummary.Unresolved
                          + " will need standalone landing slabs or compact access assets before path creation.");
            }
        }

        public static void Reset()
        {
            _lastSummary = CrossingConstructionSummary.Empty;
        }
    }
}
