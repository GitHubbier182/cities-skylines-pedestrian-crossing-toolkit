using System.Collections.Generic;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public enum CrossingPathWorkOrderKind
    {
        SurfacePath,
        SignalizedSurfacePath,
        SubwayPath,
        BridgePath
    }
    public struct CrossingPathWorkOrder
    {
        public readonly int AssetId;
        public readonly ushort SegmentId;
        public readonly CrossingPathWorkOrderKind Kind;
        public readonly NetInfo Prefab;
        public readonly string PrefabName;
        public readonly bool HasPrefab;
        public readonly bool UsesRoadEdgeLandings;
        public readonly Vector3 FirstPosition;
        public readonly Vector3 SecondPosition;
        public readonly Vector3 FirstBuildPosition;
        public readonly Vector3 SecondBuildPosition;
        public readonly float VerticalOffset;

        public CrossingPathWorkOrder(int assetId, ushort segmentId, CrossingPathWorkOrderKind kind, NetInfo prefab, bool usesRoadEdgeLandings, Vector3 firstPosition, Vector3 secondPosition)
        {
            AssetId = assetId;
            SegmentId = segmentId;
            Kind = kind;
            Prefab = prefab;
            PrefabName = prefab == null ? "none" : prefab.name;
            HasPrefab = prefab != null;
            UsesRoadEdgeLandings = usesRoadEdgeLandings;
            FirstPosition = firstPosition;
            SecondPosition = secondPosition;
            VerticalOffset = CrossingVerticalProfile.GetPathOffset(kind);
            FirstBuildPosition = firstPosition + Vector3.up * VerticalOffset;
            SecondBuildPosition = secondPosition + Vector3.up * VerticalOffset;
        }

        public string ToLogString()
        {
            return "asset=" + AssetId
                   + " segment=" + SegmentId
                   + " kind=" + Kind
                   + " prefab=" + PrefabName
                   + " prefabReady=" + HasPrefab
                   + " endpointSource=" + (UsesRoadEdgeLandings ? "road-edge-landings" : "pedestrian-lanes")
                   + " from=" + FirstPosition
                   + " to=" + SecondPosition
                   + " verticalOffset=" + VerticalOffset.ToString("0.00")
                   + " buildFrom=" + FirstBuildPosition
                   + " buildTo=" + SecondBuildPosition;
        }
    }

    public struct CrossingPathWorkOrderSummary
    {
        public static readonly CrossingPathWorkOrderSummary Empty = new CrossingPathWorkOrderSummary(0, 0, 0, 0, 0, 0, 0);

        public readonly int Total;
        public readonly int PrefabReady;
        public readonly int MissingPrefab;
        public readonly int SurfacePaths;
        public readonly int SubwayPaths;
        public readonly int BridgePaths;
        public readonly int RoadEdgeLandingPaths;

        public CrossingPathWorkOrderSummary(int total, int prefabReady, int missingPrefab, int surfacePaths, int subwayPaths, int bridgePaths, int roadEdgeLandingPaths)
        {
            Total = total;
            PrefabReady = prefabReady;
            MissingPrefab = missingPrefab;
            SurfacePaths = surfacePaths;
            SubwayPaths = subwayPaths;
            BridgePaths = bridgePaths;
            RoadEdgeLandingPaths = roadEdgeLandingPaths;
        }

        public string ToLogString()
        {
            return "total=" + Total
                   + " prefabReady=" + PrefabReady
                   + " missingPrefab=" + MissingPrefab
                   + " surface=" + SurfacePaths
                   + " subway=" + SubwayPaths
                   + " bridge=" + BridgePaths
                   + " roadEdgeLandings=" + RoadEdgeLandingPaths;
        }
    }

    public static class CrossingPathWorkOrderPlanner
    {
        private const int MaxWorkOrderLogsPerRefresh = 16;
        private const float SubwayEntranceAutoLinkRadius = CrossingLandingConnectorPlanner.SubwayEntranceReuseRadius;
        private const float SubwayEntranceAutoLinkMinDistance = 0.5f;
        private const bool EnableNearbySubwayEntranceAutoLinks = true;
        private static readonly CrossingPathWorkOrder[] WorkOrderBuffer = new CrossingPathWorkOrder[1024];
        private static readonly CrossingLandingAccessAssetWorkOrder[] SubwayEntranceBuffer = new CrossingLandingAccessAssetWorkOrder[2048];
        private static int _workOrderCount;
        private static CrossingPathWorkOrderSummary _lastSummary = CrossingPathWorkOrderSummary.Empty;

        public static CrossingPathWorkOrderSummary LastSummary
        {
            get { return _lastSummary; }
        }

        public static int WorkOrderCount
        {
            get { return _workOrderCount; }
        }

        public static int CopyWorkOrdersTo(CrossingPathWorkOrder[] buffer)
        {
            int count = Mathf.Min(buffer.Length, _workOrderCount);
            for (int i = 0; i < count; i++)
                buffer[i] = WorkOrderBuffer[i];

            return count;
        }

        public static void Refresh(string reason, CrossingConnectivityLink[] links, int count)
        {
            _workOrderCount = 0;
            int prefabReady = 0;
            int missingPrefab = 0;
            int surfacePaths = 0;
            int subwayPaths = 0;
            int bridgePaths = 0;
            int roadEdgeLandingPaths = 0;

            int max = Mathf.Min(count, links.Length);
            for (int i = 0; i < max; i++)
            {
                CrossingConnectivityLink link = links[i];
                CrossingPathWorkOrderKind kind = GetWorkOrderKind(link.Kind);
                NetInfo prefab = GetPrefab(kind);
                Vector3 firstPosition = link.FirstPosition;
                Vector3 secondPosition = link.SecondPosition;
                if (kind == CrossingPathWorkOrderKind.SubwayPath)
                    ResolveSubwayPathEndpoints(link, ref firstPosition, ref secondPosition);

                CrossingPathWorkOrder order = new CrossingPathWorkOrder(link.AssetId, link.SegmentId, kind, prefab, !link.UsesLaneTargets, firstPosition, secondPosition);
                AddWorkOrder(order);

                if (order.HasPrefab)
                    prefabReady++;
                else
                    missingPrefab++;

                if (order.UsesRoadEdgeLandings)
                    roadEdgeLandingPaths++;

                switch (kind)
                {
                    case CrossingPathWorkOrderKind.SubwayPath:
                        subwayPaths++;
                        break;
                    case CrossingPathWorkOrderKind.BridgePath:
                        bridgePaths++;
                        break;
                    default:
                        surfacePaths++;
                        break;
                }
            }

            int nearbySubwayLinks = EnableNearbySubwayEntranceAutoLinks
                ? AddNearbySubwayEntranceLinks(ref prefabReady, ref missingPrefab, ref subwayPaths)
                : 0;
            _lastSummary = new CrossingPathWorkOrderSummary(_workOrderCount, prefabReady, missingPrefab, surfacePaths, subwayPaths, bridgePaths, roadEdgeLandingPaths);
            Debug.Log("[PedestrianCrossingToolkit] Path work order planning: reason="
                      + reason
                      + " "
                      + _lastSummary.ToLogString()
                      + " nearbySubwayLinks="
                      + nearbySubwayLinks
                      + " nearbySubwayRadius="
                      + SubwayEntranceAutoLinkRadius.ToString("0.0"));
            LogWorkOrders(reason);
        }

        public static void Reset()
        {
            _workOrderCount = 0;
            _lastSummary = CrossingPathWorkOrderSummary.Empty;
        }

        private static void AddWorkOrder(CrossingPathWorkOrder order)
        {
            if (_workOrderCount >= WorkOrderBuffer.Length)
                return;

            WorkOrderBuffer[_workOrderCount++] = order;
        }

        private static int AddNearbySubwayEntranceLinks(ref int prefabReady, ref int missingPrefab, ref int subwayPaths)
        {
            HashSet<string> connectedPairs = new HashSet<string>();
            for (int i = 0; i < _workOrderCount; i++)
            {
                CrossingPathWorkOrder order = WorkOrderBuffer[i];
                if (order.Kind != CrossingPathWorkOrderKind.SubwayPath)
                    continue;

                connectedPairs.Add(CrossingConnectorKey.Make(order.FirstPosition, order.SecondPosition));
            }

            int entranceCount = CopyUniqueSubwayEntrances();
            int added = 0;
            float minDistanceSqr = SubwayEntranceAutoLinkMinDistance * SubwayEntranceAutoLinkMinDistance;
            float radiusSqr = SubwayEntranceAutoLinkRadius * SubwayEntranceAutoLinkRadius;
            for (int i = 0; i < entranceCount; i++)
            {
                for (int j = i + 1; j < entranceCount; j++)
                {
                    CrossingLandingAccessAssetWorkOrder first = SubwayEntranceBuffer[i];
                    CrossingLandingAccessAssetWorkOrder second = SubwayEntranceBuffer[j];
                    float distanceSqr = HorizontalDistanceSqr(first.Position, second.Position);
                    if (distanceSqr <= minDistanceSqr || distanceSqr > radiusSqr)
                        continue;

                    string key = CrossingConnectorKey.Make(first.DeckPosition, second.DeckPosition);
                    if (connectedPairs.Contains(key))
                        continue;

                    if (_workOrderCount >= WorkOrderBuffer.Length)
                        return added;

                    connectedPairs.Add(key);
                    NetInfo prefab = GetPrefab(CrossingPathWorkOrderKind.SubwayPath);
                    CrossingPathWorkOrder order = new CrossingPathWorkOrder(
                        first.AssetId,
                        first.SegmentId,
                        CrossingPathWorkOrderKind.SubwayPath,
                        prefab,
                        false,
                        first.DeckPosition,
                        second.DeckPosition);
                    AddWorkOrder(order);

                    if (order.HasPrefab)
                        prefabReady++;
                    else
                        missingPrefab++;

                    subwayPaths++;
                    added++;
                }
            }

            return added;
        }

        private static float HorizontalDistanceSqr(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return (first - second).sqrMagnitude;
        }

        private static int CopyUniqueSubwayEntrances()
        {
            int sourceCount = CrossingLandingConnectorPlanner.CopyAccessAssetsTo(SubwayEntranceBuffer);
            int entranceCount = 0;
            int max = Mathf.Min(sourceCount, SubwayEntranceBuffer.Length);
            for (int i = 0; i < max; i++)
            {
                CrossingLandingAccessAssetWorkOrder order = SubwayEntranceBuffer[i];
                if (order.AssetKind != CrossingLandingAccessAssetKind.SubwayEntrance
                    || order.ReusesExistingEntrance)
                {
                    continue;
                }

                if (HasMatchingSubwayEntrance(order, entranceCount))
                    continue;

                SubwayEntranceBuffer[entranceCount++] = order;
            }

            return entranceCount;
        }

        private static bool HasMatchingSubwayEntrance(CrossingLandingAccessAssetWorkOrder candidate, int entranceCount)
        {
            float minDistanceSqr = SubwayEntranceAutoLinkMinDistance * SubwayEntranceAutoLinkMinDistance;
            for (int i = 0; i < entranceCount; i++)
            {
                CrossingLandingAccessAssetWorkOrder existing = SubwayEntranceBuffer[i];
                if (HorizontalDistanceSqr(existing.Position, candidate.Position) <= minDistanceSqr
                    || HorizontalDistanceSqr(existing.DeckPosition, candidate.DeckPosition) <= minDistanceSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ResolveSubwayPathEndpoints(CrossingConnectivityLink link, ref Vector3 firstPosition, ref Vector3 secondPosition)
        {
            Vector3 resolved;
            if (CrossingLandingConnectorPlanner.TryGetAccessAssetPosition(link.AssetId, "A", CrossingLandingAccessAssetKind.SubwayEntrance, out resolved))
                firstPosition = resolved;

            if (CrossingLandingConnectorPlanner.TryGetAccessAssetPosition(link.AssetId, "B", CrossingLandingAccessAssetKind.SubwayEntrance, out resolved))
                secondPosition = resolved;
        }

        private static CrossingPathWorkOrderKind GetWorkOrderKind(CrossingConnectivityLinkKind linkKind)
        {
            switch (linkKind)
            {
                case CrossingConnectivityLinkKind.SignalizedSurfaceSpan:
                    return CrossingPathWorkOrderKind.SignalizedSurfacePath;
                case CrossingConnectivityLinkKind.SubwaySpan:
                case CrossingConnectivityLinkKind.JunctionSubwayApproach:
                    return CrossingPathWorkOrderKind.SubwayPath;
                case CrossingConnectivityLinkKind.PedestrianBridgeSpan:
                case CrossingConnectivityLinkKind.JunctionBridgeApproach:
                    return CrossingPathWorkOrderKind.BridgePath;
                default:
                    return CrossingPathWorkOrderKind.SurfacePath;
            }
        }

        private static NetInfo GetPrefab(CrossingPathWorkOrderKind kind)
        {
            switch (kind)
            {
                case CrossingPathWorkOrderKind.SurfacePath:
                case CrossingPathWorkOrderKind.SignalizedSurfacePath:
                    return PedestrianCrossingPrefabCatalog.SurfaceCrossingPathPrefab
                           ?? PedestrianCrossingPrefabCatalog.PedestrianPathPrefab;
                case CrossingPathWorkOrderKind.SubwayPath:
                    return PedestrianCrossingPrefabCatalog.PedestrianTunnelPrefab ?? PedestrianCrossingPrefabCatalog.PedestrianPathPrefab;
                case CrossingPathWorkOrderKind.BridgePath:
                    return PedestrianCrossingPrefabCatalog.InvisibleBridgePathPrefab
                           ?? PedestrianCrossingPrefabCatalog.PedestrianPathPrefab;
                default:
                    return PedestrianCrossingPrefabCatalog.PedestrianPathPrefab;
            }
        }

        private static void LogWorkOrders(string reason)
        {
            if (!PedestrianCrossingLog.VerboseDiagnostics)
                return;

            int logCount = Mathf.Min(_workOrderCount, MaxWorkOrderLogsPerRefresh);
            for (int i = 0; i < logCount; i++)
            {
                Debug.Log("[PedestrianCrossingToolkit] Path work order: reason=" + reason + " index=" + i + " " + WorkOrderBuffer[i].ToLogString());
            }

            if (_workOrderCount > logCount)
            {
                Debug.Log("[PedestrianCrossingToolkit] Path work order log truncated: reason="
                          + reason
                          + " shown=" + logCount
                          + " total=" + _workOrderCount);
            }
        }
    }
}
