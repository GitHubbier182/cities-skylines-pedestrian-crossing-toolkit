using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public struct CrossingPathExecutionSummary
    {
        public static readonly CrossingPathExecutionSummary Empty = new CrossingPathExecutionSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        public readonly int Total;
        public readonly int Ready;
        public readonly int Skipped;
        public readonly int Applied;
        public readonly int Surface;
        public readonly int Signal;
        public readonly int Subway;
        public readonly int Bridge;
        public readonly int RoadEdgeLanding;
        public readonly int AccessTotal;
        public readonly int AccessReady;
        public readonly int AccessSkipped;

        public CrossingPathExecutionSummary(int total, int ready, int skipped, int applied, int surface, int signal, int subway, int bridge, int roadEdgeLanding, int accessTotal, int accessReady, int accessSkipped)
        {
            Total = total;
            Ready = ready;
            Skipped = skipped;
            Applied = applied;
            Surface = surface;
            Signal = signal;
            Subway = subway;
            Bridge = bridge;
            RoadEdgeLanding = roadEdgeLanding;
            AccessTotal = accessTotal;
            AccessReady = accessReady;
            AccessSkipped = accessSkipped;
        }

        public string ToLogString()
        {
            return "total=" + Total
                   + " ready=" + Ready
                   + " skipped=" + Skipped
                   + " applied=" + Applied
                   + " surface=" + Surface
                   + " signal=" + Signal
                   + " subway=" + Subway
                   + " bridge=" + Bridge
                   + " roadEdge=" + RoadEdgeLanding
                   + " accessTotal=" + AccessTotal
                   + " accessReady=" + AccessReady
                   + " accessSkipped=" + AccessSkipped;
        }
    }

    public static class CrossingPathExecutionBoundary
    {
        private const int MaxPathExecutionLogsPerRefresh = 16;
        public const float MinExecutablePathLength = 4f;
        public const float MinExecutableAccessPathLength = 2f;
        private const float SubwayEntrancePadSurfaceLift = 0.006f;
        private const bool EnableLivePathCreation = false;
        private static readonly CrossingPathWorkOrder[] WorkOrderBuffer = new CrossingPathWorkOrder[1024];
        private static readonly CrossingLandingAccessAssetWorkOrder[] AccessWorkOrderBuffer = new CrossingLandingAccessAssetWorkOrder[2048];
        private static CrossingPathExecutionSummary _lastSummary = CrossingPathExecutionSummary.Empty;

        public static CrossingPathExecutionSummary LastSummary
        {
            get { return _lastSummary; }
        }

        public static bool LivePathCreationEnabled
        {
            get { return EnableLivePathCreation; }
        }

        public static void Reset()
        {
            _lastSummary = CrossingPathExecutionSummary.Empty;
        }

        public static CrossingPathExecutionSummary Sync(string reason)
        {
            int count = CrossingPathWorkOrderPlanner.CopyWorkOrdersTo(WorkOrderBuffer);
            int ready = 0;
            int skipped = 0;
            int applied = 0;
            int surface = 0;
            int signal = 0;
            int subway = 0;
            int bridge = 0;
            int roadEdgeLanding = 0;
            int accessReady = 0;
            int accessSkipped = 0;
            int logCount = Mathf.Min(count, MaxPathExecutionLogsPerRefresh);

            for (int i = 0; i < count; i++)
            {
                CrossingPathWorkOrder order = WorkOrderBuffer[i];
                CountWorkOrderKind(order, ref surface, ref signal, ref subway, ref bridge);
                if (order.UsesRoadEdgeLandings)
                    roadEdgeLanding++;

                string validation;
                bool canExecute = ValidatePathWorkOrder(order, out validation);
                if (canExecute)
                    ready++;
                else
                    skipped++;

                if (PedestrianCrossingLog.VerboseDiagnostics && i < logCount)
                {
                    Debug.Log("[PedestrianCrossingToolkit] Path execution detail: reason="
                              + reason
                              + " index="
                              + i
                              + " readiness="
                              + (canExecute ? "Ready" : "Blocked")
                              + " detail="
                              + validation
                              + " "
                              + order.ToLogString());
                }

                if (!canExecute || !EnableLivePathCreation)
                    continue;

                if (ApplyPathWorkOrder(order))
                    applied++;
                else
                    skipped++;
            }

            if (PedestrianCrossingLog.VerboseDiagnostics && count > logCount)
            {
                Debug.Log("[PedestrianCrossingToolkit] Path execution detail truncated: reason="
                          + reason
                          + " shown="
                          + logCount
                          + " total="
                          + count);
            }

            int accessCount = ValidateAccessWorkOrders(reason, ref accessReady, ref accessSkipped);
            _lastSummary = new CrossingPathExecutionSummary(count, ready, skipped, applied, surface, signal, subway, bridge, roadEdgeLanding, accessCount, accessReady, accessSkipped);
            Debug.Log("[PedestrianCrossingToolkit] Path execution sync: reason="
                      + reason
                      + " livePaths="
                      + EnableLivePathCreation
                      + " "
                      + _lastSummary.ToLogString());
            return _lastSummary;
        }

        private static int ValidateAccessWorkOrders(string reason, ref int ready, ref int skipped)
        {
            int count = CrossingLandingConnectorPlanner.CopyAccessAssetsTo(AccessWorkOrderBuffer);
            int logCount = Mathf.Min(count, MaxPathExecutionLogsPerRefresh);
            for (int i = 0; i < count; i++)
            {
                string validation;
                bool canExecute = ValidateAccessWorkOrder(AccessWorkOrderBuffer[i], out validation);
                if (canExecute)
                    ready++;
                else
                    skipped++;

                if (PedestrianCrossingLog.VerboseDiagnostics && i < logCount)
                {
                    Debug.Log("[PedestrianCrossingToolkit] Access path execution detail: reason="
                              + reason
                              + " index="
                              + i
                              + " readiness="
                              + (canExecute ? "Ready" : "Blocked")
                              + " detail="
                              + validation
                              + " "
                              + AccessWorkOrderBuffer[i].ToLogString());
                }
            }

            if (PedestrianCrossingLog.VerboseDiagnostics && count > logCount)
            {
                Debug.Log("[PedestrianCrossingToolkit] Access path execution detail truncated: reason="
                          + reason
                          + " shown="
                          + logCount
                          + " total="
                          + count);
            }

            return count;
        }

        private static void CountWorkOrderKind(CrossingPathWorkOrder order, ref int surface, ref int signal, ref int subway, ref int bridge)
        {
            switch (order.Kind)
            {
                case CrossingPathWorkOrderKind.SignalizedSurfacePath:
                    signal++;
                    break;
                case CrossingPathWorkOrderKind.SubwayPath:
                    subway++;
                    break;
                case CrossingPathWorkOrderKind.BridgePath:
                    bridge++;
                    break;
                default:
                    surface++;
                    break;
            }
        }

        private static bool ValidatePathWorkOrder(CrossingPathWorkOrder order, out string reason)
        {
            if (!order.HasPrefab || order.Prefab == null)
            {
                reason = "missing pedestrian path prefab";
                return false;
            }

            if (!IsFinite(order.FirstBuildPosition) || !IsFinite(order.SecondBuildPosition))
            {
                reason = "path endpoint position is invalid";
                return false;
            }

            float length = HorizontalDistance(order.FirstBuildPosition, order.SecondBuildPosition);
            if (length < MinExecutablePathLength)
            {
                reason = "path endpoints are too close for connector build";
                return false;
            }

            if (!IsRoadSegmentReady(order.SegmentId))
            {
                reason = "source road segment is missing or invalid";
                return false;
            }

            reason = "path creation is staged";
            return true;
        }

        private static bool ValidateAccessWorkOrder(CrossingLandingAccessAssetWorkOrder order, out string reason)
        {
            if (order.AssetKind == CrossingLandingAccessAssetKind.SubwayEntrance
                && order.ConnectorTargetKind != CrossingLandingConnectorTargetKind.PedestrianLane)
            {
                reason = "road-edge subway entrance uses visual-only access pad";
                return false;
            }

            NetInfo prefab = GetAccessPathPrefab(order);
            if (prefab == null)
            {
                reason = "missing access path prefab";
                return false;
            }

            Vector3 deckBuildPosition = order.DeckPosition + Vector3.up * GetAccessDeckElevation(order);
            Vector3 groundPosition = GetAccessGroundPosition(order);
            if (!IsFinite(deckBuildPosition) || !IsFinite(groundPosition))
            {
                reason = "access endpoint position is invalid";
                return false;
            }

            float length = HorizontalDistance(deckBuildPosition, groundPosition);
            if (length < MinExecutableAccessPathLength)
            {
                reason = "access endpoints are too close for connector build";
                return false;
            }

            reason = "access path creation is staged";
            return true;
        }

        private static bool ApplyPathWorkOrder(CrossingPathWorkOrder order)
        {
            return false;
        }

        private static NetInfo GetAccessPathPrefab(CrossingLandingAccessAssetWorkOrder order)
        {
            if (order.AssetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding)
            {
                return PedestrianCrossingPrefabCatalog.BridgeDeckPrefab
                       ?? PedestrianCrossingPrefabCatalog.PedestrianPathPrefab;
            }

            return PedestrianCrossingPrefabCatalog.PedestrianTunnelPrefab
                   ?? PedestrianCrossingPrefabCatalog.PedestrianPathPrefab;
        }

        private static float GetAccessDeckElevation(CrossingLandingAccessAssetWorkOrder order)
        {
            if (order.AssetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding)
                return CrossingVerticalProfile.BridgeDeckHeight;

            return CrossingVerticalProfile.SubwayTunnelDepth;
        }

        private static Vector3 GetAccessGroundPosition(CrossingLandingAccessAssetWorkOrder order)
        {
            if (order.AssetKind != CrossingLandingAccessAssetKind.SubwayEntrance)
                return order.Position;

            Vector3 position = order.Position;
            float height = order.Position.y;

            Vector3 laneSurfacePosition;
            if (order.ConnectorTargetKind == CrossingLandingConnectorTargetKind.PedestrianLane
                && RoadPavementAnchorResolver.TryGetPedestrianLaneSurfacePosition(order.SegmentId, position, out laneSurfacePosition))
            {
                height = laneSurfacePosition.y;
            }
            else
            {
                TerrainManager terrainManager = TerrainManager.instance;
                if (terrainManager != null)
                    height = terrainManager.SampleRawHeightSmooth(position);
            }

            position.y = height;
            position.y += SubwayEntrancePadSurfaceLift;
            return position;
        }

        private static bool IsRoadSegmentReady(ushort segmentId)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            return (segment.m_flags & NetSegment.Flags.Created) != 0;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }
    }
}
