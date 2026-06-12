using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public enum CrossingLandingConnectorTargetKind
    {
        None,
        PedestrianLane,
        RoadEdgeLanding
    }

    public enum CrossingLandingAccessKind
    {
        DirectLanding,
        BridgeStraightStairs,
        BridgeUShapedStairs,
        BridgeZShapedStairs,
        BridgeXShapedStairs,
        BridgeYShapedStairs,
        SubwayStraightEntrance,
        SubwayUShapedEntrance,
        SubwayZShapedEntrance,
        SubwayXShapedEntrance,
        SubwayYShapedEntrance
    }

    public struct CrossingLandingConnectorWorkOrder
    {
        public readonly int AssetId;
        public readonly ushort SegmentId;
        public readonly CrossingConnectivityLinkKind CrossingKind;
        public readonly string EndpointName;
        public readonly CrossingLandingAccessKind AccessKind;
        public readonly CrossingLandingConnectorTargetKind TargetKind;
        public readonly int TargetAssetId;
        public readonly NetInfo Prefab;
        public readonly string PrefabName;
        public readonly bool HasPrefab;
        public readonly float Distance;
        public readonly Vector3 FromPosition;
        public readonly Vector3 ToPosition;

        public CrossingLandingConnectorWorkOrder(int assetId, ushort segmentId, CrossingConnectivityLinkKind crossingKind, string endpointName, CrossingLandingAccessKind accessKind, CrossingLandingConnectorTargetKind targetKind, int targetAssetId, NetInfo prefab, Vector3 fromPosition, Vector3 toPosition)
        {
            AssetId = assetId;
            SegmentId = segmentId;
            CrossingKind = crossingKind;
            EndpointName = endpointName;
            AccessKind = accessKind;
            TargetKind = targetKind;
            TargetAssetId = targetAssetId;
            Prefab = prefab;
            PrefabName = prefab == null ? "none" : prefab.name;
            HasPrefab = prefab != null;
            FromPosition = fromPosition;
            ToPosition = toPosition;
            Distance = Vector3.Distance(fromPosition, toPosition);
        }

        public string ToLogString()
        {
            return "asset=" + AssetId
                   + " segment=" + SegmentId
                   + " crossingKind=" + CrossingKind
                   + " endpoint=" + EndpointName
                   + " access=" + AccessKind
                   + " target=" + TargetKind
                   + " targetAsset=" + TargetAssetId
                   + " prefab=" + PrefabName
                   + " prefabReady=" + HasPrefab
                   + " distance=" + Distance.ToString("0.0")
                   + " from=" + FromPosition
                   + " to=" + ToPosition;
        }
    }

    public enum CrossingLandingAccessAssetKind
    {
        SubwayEntrance,
        BridgeStairRampLanding
    }

    public struct CrossingLandingAccessAssetWorkOrder
    {
        public readonly int AssetId;
        public readonly ushort SegmentId;
        public readonly CrossingConnectivityLinkKind CrossingKind;
        public readonly string EndpointName;
        public readonly CrossingLandingAccessAssetKind AssetKind;
        public readonly CrossingLandingAccessKind AccessKind;
        public readonly CrossingLandingConnectorTargetKind ConnectorTargetKind;
        public readonly Vector3 DeckPosition;
        public readonly Vector3 Position;
        public readonly Vector3 FacingDirection;
        public readonly float FootprintLength;
        public readonly float FootprintWidth;
        public readonly bool ReusesExistingEntrance;
        public readonly int ReusedEntranceAssetId;
        public readonly string ReusedEntranceEndpointName;

        public CrossingLandingAccessAssetWorkOrder(int assetId, ushort segmentId, CrossingConnectivityLinkKind crossingKind, string endpointName, CrossingLandingAccessAssetKind assetKind, CrossingLandingAccessKind accessKind, CrossingLandingConnectorTargetKind connectorTargetKind, Vector3 deckPosition, Vector3 position, Vector3 facingDirection, float footprintLength, float footprintWidth, bool reusesExistingEntrance = false, int reusedEntranceAssetId = 0, string reusedEntranceEndpointName = null)
        {
            AssetId = assetId;
            SegmentId = segmentId;
            CrossingKind = crossingKind;
            EndpointName = endpointName;
            AssetKind = assetKind;
            AccessKind = accessKind;
            ConnectorTargetKind = connectorTargetKind;
            DeckPosition = deckPosition;
            Position = position;
            FacingDirection = facingDirection;
            FootprintLength = footprintLength;
            FootprintWidth = footprintWidth;
            ReusesExistingEntrance = reusesExistingEntrance;
            ReusedEntranceAssetId = reusedEntranceAssetId;
            ReusedEntranceEndpointName = reusedEntranceEndpointName ?? string.Empty;
        }

        public string ToLogString()
        {
            return "asset=" + AssetId
                   + " segment=" + SegmentId
                   + " crossingKind=" + CrossingKind
                   + " endpoint=" + EndpointName
                   + " assetKind=" + AssetKind
                   + " access=" + AccessKind
                   + " connectorTarget=" + ConnectorTargetKind
                   + " footprint=" + FootprintLength.ToString("0.0") + "x" + FootprintWidth.ToString("0.0")
                   + " deck=" + DeckPosition
                   + " position=" + Position
                   + " facing=" + FacingDirection
                   + " reusesExistingEntrance=" + ReusesExistingEntrance
                   + " reusedEntrance=" + ReusedEntranceAssetId + ":" + ReusedEntranceEndpointName;
        }
    }

    public struct CrossingLandingConnectorSummary
    {
        public static readonly CrossingLandingConnectorSummary Empty = new CrossingLandingConnectorSummary(0, 0, 0, 0, 0, 0, 0);

        public readonly int Landings;
        public readonly int Connected;
        public readonly int Unresolved;
        public readonly int PedestrianLaneTargets;
        public readonly int SiblingLandingTargets;
        public readonly int PrefabReady;
        public readonly int MissingPrefab;

        public CrossingLandingConnectorSummary(int landings, int connected, int unresolved, int pedestrianLaneTargets, int siblingLandingTargets, int prefabReady, int missingPrefab)
        {
            Landings = landings;
            Connected = connected;
            Unresolved = unresolved;
            PedestrianLaneTargets = pedestrianLaneTargets;
            SiblingLandingTargets = siblingLandingTargets;
            PrefabReady = prefabReady;
            MissingPrefab = missingPrefab;
        }

        public string ToLogString()
        {
            return "landings=" + Landings
                   + " connected=" + Connected
                   + " unresolved=" + Unresolved
                   + " pedestrianLaneTargets=" + PedestrianLaneTargets
                   + " siblingLandingTargets=" + SiblingLandingTargets
                   + " prefabReady=" + PrefabReady
                   + " missingPrefab=" + MissingPrefab;
        }
    }

    public static class CrossingLandingConnectorPlanner
    {
        private const int MaxConnectorLogsPerRefresh = 16;
        private const float MaxConnectorDistance = 18f;
        private const float BridgeRampStandardDistance = 8.5f;
        private const float BridgeAccessOffset = BridgeRampStandardDistance;
        private const float BridgeExitWalkOutDistance = 2f;
        private const float BridgeRampMinDistance = 6.5f;
        private const float BridgeRampMaxDistance = 12.5f;
        private const float BridgeRampLaneSurfaceSnapDistance = 5f;
        private const float MinBridgeRampDrop = 1f;
        public const float SubwayEntranceFunctionalEntryOffset = 1.25f;
        private const float OffRoadAccessNudge = 2f;
        private const int MaxOffRoadAccessNudges = 8;
        private const float BentEntranceDistance = 11f;
        private const float SplitEntranceDistance = 7f;
        public const float SubwayEntranceReuseRadius = 10f;
        private static readonly CrossingLandingConnectorWorkOrder[] WorkOrderBuffer = new CrossingLandingConnectorWorkOrder[2048];
        private static readonly CrossingLandingAccessAssetWorkOrder[] AccessAssetBuffer = new CrossingLandingAccessAssetWorkOrder[2048];
        private static int _workOrderCount;
        private static int _accessAssetCount;
        private static CrossingLandingConnectorSummary _lastSummary = CrossingLandingConnectorSummary.Empty;

        public static CrossingLandingConnectorSummary LastSummary
        {
            get { return _lastSummary; }
        }

        public static int AccessAssetCount
        {
            get { return _accessAssetCount; }
        }

        public static void Refresh(string reason, CrossingConnectivityLink[] links, int linkCount, CrossingConnectivityCandidate[] candidates, int candidateCount)
        {
            _workOrderCount = 0;
            _accessAssetCount = 0;
            int landings = 0;
            int connected = 0;
            int unresolved = 0;
            int pedestrianLaneTargets = 0;
            int siblingLandingTargets = 0;
            int prefabReady = 0;
            int missingPrefab = 0;

            int max = Mathf.Min(linkCount, links.Length);
            for (int i = 0; i < max; i++)
            {
                CrossingConnectivityLink link = links[i];
                if (link.UsesLaneTargets)
                {
                    if (IsBridgeKind(link.Kind))
                    {
                        PlanBridgeSpanAccess(link, candidates, candidateCount);
                        continue;
                    }

                    if (IsSubwayKind(link.Kind))
                    {
                        PlanSubwayLaneAccess(link);
                        continue;
                    }

                    PlanJunctionLaneAccess(link);
                    continue;
                }

                PlanEndpoint(reason, links, max, candidates, candidateCount, i, true, ref landings, ref connected, ref unresolved, ref pedestrianLaneTargets, ref siblingLandingTargets, ref prefabReady, ref missingPrefab);
                PlanEndpoint(reason, links, max, candidates, candidateCount, i, false, ref landings, ref connected, ref unresolved, ref pedestrianLaneTargets, ref siblingLandingTargets, ref prefabReady, ref missingPrefab);
            }

            _lastSummary = new CrossingLandingConnectorSummary(landings, connected, unresolved, pedestrianLaneTargets, siblingLandingTargets, prefabReady, missingPrefab);
            Debug.Log("[PedestrianCrossingToolkit] Landing connector planning: reason="
                      + reason
                      + " "
                      + _lastSummary.ToLogString()
                      + " maxDistance="
                      + MaxConnectorDistance.ToString("0.0")
                      + " bridgeAccessOffset="
                      + BridgeAccessOffset.ToString("0.0"));
            Debug.Log("[PedestrianCrossingToolkit] Landing access asset planning: reason="
                      + reason
                      + " total="
                      + _accessAssetCount
                      + " bridge="
                      + CountAccessAssets(CrossingLandingAccessAssetKind.BridgeStairRampLanding)
                      + " subway="
                      + CountAccessAssets(CrossingLandingAccessAssetKind.SubwayEntrance));
            LogWorkOrders(reason);
            LogAccessAssets(reason);
        }

        public static void Reset()
        {
            _workOrderCount = 0;
            _accessAssetCount = 0;
            _lastSummary = CrossingLandingConnectorSummary.Empty;
        }

        public static int CopyWorkOrdersTo(CrossingLandingConnectorWorkOrder[] buffer)
        {
            int count = Mathf.Min(buffer.Length, _workOrderCount);
            for (int i = 0; i < count; i++)
                buffer[i] = WorkOrderBuffer[i];

            return count;
        }

        public static int CopyAccessAssetsTo(CrossingLandingAccessAssetWorkOrder[] buffer)
        {
            int count = Mathf.Min(buffer.Length, _accessAssetCount);
            for (int i = 0; i < count; i++)
                buffer[i] = AccessAssetBuffer[i];

            return count;
        }

        public static bool TryGetAccessAssetPosition(int assetId, string endpointName, CrossingLandingAccessAssetKind assetKind, out Vector3 position)
        {
            position = Vector3.zero;
            for (int i = 0; i < _accessAssetCount; i++)
            {
                CrossingLandingAccessAssetWorkOrder order = AccessAssetBuffer[i];
                if (order.AssetId != assetId
                    || order.AssetKind != assetKind
                    || order.EndpointName != endpointName)
                {
                    continue;
                }

                position = order.AssetKind == CrossingLandingAccessAssetKind.SubwayEntrance
                    ? order.DeckPosition
                    : order.Position;
                return true;
            }

            return false;
        }

        private static void PlanEndpoint(string reason, CrossingConnectivityLink[] links, int linkCount, CrossingConnectivityCandidate[] candidates, int candidateCount, int linkIndex, bool firstEndpoint, ref int landings, ref int connected, ref int unresolved, ref int pedestrianLaneTargets, ref int siblingLandingTargets, ref int prefabReady, ref int missingPrefab)
        {
            CrossingConnectivityLink link = links[linkIndex];
            Vector3 position = GetLandingAccessPosition(link, firstEndpoint);
            string endpointName = firstEndpoint ? "A" : "B";
            CrossingLandingAccessAssetKind assetKind = GetAccessAssetKind(link);
            Vector3 deckPosition = firstEndpoint ? link.FirstPosition : link.SecondPosition;
            if ((IsBridgeKind(link.Kind) || IsSubwayKind(link.Kind))
                && !TryResolveOffRoadAccessPosition(link, assetKind, deckPosition, ref position, endpointName))
            {
                unresolved++;
                Debug.Log("[PedestrianCrossingToolkit] Landing connector unresolved because access could not be moved off road: reason="
                          + reason
                          + " asset="
                          + link.AssetId
                          + " segment="
                          + link.SegmentId
                          + " crossingKind="
                          + link.Kind
                          + " endpoint="
                          + endpointName
                          + " position="
                          + position);
                return;
            }

            landings++;

            Vector3 targetPosition = position;
            int targetAssetId = 0;
            CrossingLandingConnectorTargetKind targetKind = CrossingLandingConnectorTargetKind.None;
            if (TryFindNearestPedestrianLane(link, position, candidates, candidateCount, out targetPosition, out targetAssetId))
            {
                targetKind = CrossingLandingConnectorTargetKind.PedestrianLane;
                pedestrianLaneTargets++;
            }
            else if (TryFindNearestSiblingLanding(links, linkCount, linkIndex, position, out targetPosition, out targetAssetId))
            {
                targetKind = CrossingLandingConnectorTargetKind.RoadEdgeLanding;
                siblingLandingTargets++;
            }
            else
            {
                unresolved++;
                if (IsBridgeKind(link.Kind) || IsSubwayKind(link.Kind))
                    targetKind = CrossingLandingConnectorTargetKind.RoadEdgeLanding;

                AddAccessAsset(link, firstEndpoint, endpointName, GetDefaultAccessKind(link), targetKind, position);
                Debug.Log("[PedestrianCrossingToolkit] Landing connector unresolved: reason="
                          + reason
                          + " asset="
                          + link.AssetId
                          + " segment="
                          + link.SegmentId
                          + " crossingKind="
                          + link.Kind
                          + " endpoint="
                          + endpointName
                          + " position="
                          + position);
                return;
            }

            NetInfo prefab = PedestrianCrossingPrefabCatalog.SurfaceCrossingPathPrefab
                             ?? PedestrianCrossingPrefabCatalog.PedestrianPathPrefab;
            CrossingLandingAccessKind accessKind = GetAccessKind(link, firstEndpoint, position, targetPosition, targetKind);
            AddAccessAsset(link, firstEndpoint, endpointName, accessKind, targetKind, position);
            CrossingLandingConnectorWorkOrder order = new CrossingLandingConnectorWorkOrder(link.AssetId, link.SegmentId, link.Kind, endpointName, accessKind, targetKind, targetAssetId, prefab, position, targetPosition);
            AddWorkOrder(order);
            connected++;

            if (order.HasPrefab)
                prefabReady++;
            else
                missingPrefab++;
        }

        private static Vector3 GetLandingAccessPosition(CrossingConnectivityLink link, bool firstEndpoint)
        {
            Vector3 endpoint = firstEndpoint ? link.FirstPosition : link.SecondPosition;
            if (!IsBridgeKind(link.Kind) && !IsSubwayKind(link.Kind))
                return endpoint;

            Vector3 crossingDirection = link.SecondPosition - link.FirstPosition;
            crossingDirection.y = 0f;
            if (crossingDirection.sqrMagnitude < 0.01f)
                return endpoint;

            crossingDirection.Normalize();
            if (!link.UsesLaneTargets)
            {
                if (IsSubwayKind(link.Kind))
                    return endpoint;

                Vector3 outwardDirection = firstEndpoint ? -crossingDirection : crossingDirection;
                return endpoint + outwardDirection * BridgeAccessOffset;
            }

            if (IsSubwayKind(link.Kind))
                return endpoint;

            return endpoint + (firstEndpoint ? -crossingDirection : crossingDirection) * BridgeAccessOffset;
        }

        private static CrossingLandingAccessKind GetDefaultAccessKind(CrossingConnectivityLink link)
        {
            if (IsBridgeKind(link.Kind))
                return CrossingLandingAccessKind.BridgeZShapedStairs;

            if (IsSubwayKind(link.Kind))
                return CrossingLandingAccessKind.SubwayZShapedEntrance;

            return CrossingLandingAccessKind.DirectLanding;
        }

        private static CrossingLandingAccessKind GetAccessKind(CrossingConnectivityLink link, bool firstEndpoint, Vector3 fromPosition, Vector3 toPosition, CrossingLandingConnectorTargetKind targetKind)
        {
            if (IsBridgeKind(link.Kind))
                return GetBridgeAccessKind(link, firstEndpoint, fromPosition, toPosition, targetKind);

            if (IsSubwayKind(link.Kind))
                return GetSubwayAccessKind(link, firstEndpoint, fromPosition, toPosition, targetKind);

            return CrossingLandingAccessKind.DirectLanding;
        }

        private static CrossingLandingAccessKind GetBridgeAccessKind(CrossingConnectivityLink link, bool firstEndpoint, Vector3 fromPosition, Vector3 toPosition, CrossingLandingConnectorTargetKind targetKind)
        {
            float alignment = GetOutwardAlignment(link, firstEndpoint, fromPosition, toPosition);
            float distance = HorizontalDistance(fromPosition, toPosition);

            if (targetKind == CrossingLandingConnectorTargetKind.RoadEdgeLanding && distance <= SplitEntranceDistance)
                return CrossingLandingAccessKind.BridgeYShapedStairs;

            if (targetKind == CrossingLandingConnectorTargetKind.RoadEdgeLanding)
                return CrossingLandingAccessKind.BridgeXShapedStairs;

            if (alignment > 0.72f)
                return CrossingLandingAccessKind.BridgeStraightStairs;

            if (alignment < -0.25f || distance > BentEntranceDistance)
                return CrossingLandingAccessKind.BridgeUShapedStairs;

            return CrossingLandingAccessKind.BridgeZShapedStairs;
        }

        private static float GetOutwardAlignment(CrossingConnectivityLink link, bool firstEndpoint, Vector3 fromPosition, Vector3 toPosition)
        {
            Vector3 crossingDirection = link.SecondPosition - link.FirstPosition;
            crossingDirection.y = 0f;
            if (crossingDirection.sqrMagnitude < 0.01f)
                return 1f;

            crossingDirection.Normalize();
            Vector3 outwardDirection = firstEndpoint ? -crossingDirection : crossingDirection;
            Vector3 targetDirection = toPosition - fromPosition;
            targetDirection.y = 0f;
            if (targetDirection.sqrMagnitude < 0.01f)
                return 1f;

            targetDirection.Normalize();
            return Vector3.Dot(outwardDirection, targetDirection);
        }

        private static bool TryFindNearestPedestrianLane(CrossingConnectivityLink link, Vector3 position, CrossingConnectivityCandidate[] candidates, int candidateCount, out Vector3 targetPosition, out int targetAssetId)
        {
            targetPosition = Vector3.zero;
            targetAssetId = 0;
            float bestDistance = MaxConnectorDistance;
            int max = Mathf.Min(candidateCount, candidates.Length);
            for (int i = 0; i < max; i++)
            {
                CrossingConnectivityCandidate candidate = candidates[i];
                if (candidate.AssetId == link.AssetId)
                    continue;

                float distance = HorizontalDistance(position, candidate.WorldPosition);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                targetPosition = candidate.WorldPosition;
                targetAssetId = candidate.AssetId;
            }

            return targetAssetId != 0;
        }

        private static bool TryFindNearestPedestrianLaneForAsset(int assetId, Vector3 position, CrossingConnectivityCandidate[] candidates, int candidateCount, out Vector3 targetPosition, out int targetAssetId)
        {
            targetPosition = Vector3.zero;
            targetAssetId = 0;
            float bestDistance = MaxConnectorDistance;
            int max = Mathf.Min(candidateCount, candidates.Length);
            for (int i = 0; i < max; i++)
            {
                CrossingConnectivityCandidate candidate = candidates[i];
                if (candidate.AssetId != assetId)
                    continue;

                float distance = HorizontalDistance(position, candidate.WorldPosition);
                if (distance <= 0.5f || distance >= bestDistance)
                    continue;

                bestDistance = distance;
                targetPosition = candidate.WorldPosition;
                targetAssetId = candidate.AssetId;
            }

            return targetAssetId != 0;
        }

        private static bool TryFindNearestSiblingLanding(CrossingConnectivityLink[] links, int linkCount, int sourceIndex, Vector3 position, out Vector3 targetPosition, out int targetAssetId)
        {
            targetPosition = Vector3.zero;
            targetAssetId = 0;
            float bestDistance = MaxConnectorDistance;
            for (int i = 0; i < linkCount; i++)
            {
                if (i == sourceIndex)
                    continue;

                CrossingConnectivityLink link = links[i];
                if (link.UsesLaneTargets)
                    continue;

                CheckSiblingLanding(link, true, position, ref bestDistance, ref targetPosition, ref targetAssetId);
                CheckSiblingLanding(link, false, position, ref bestDistance, ref targetPosition, ref targetAssetId);
            }

            return targetAssetId != 0;
        }

        private static void CheckSiblingLanding(CrossingConnectivityLink link, bool firstEndpoint, Vector3 position, ref float bestDistance, ref Vector3 targetPosition, ref int targetAssetId)
        {
            Vector3 candidatePosition = GetLandingAccessPosition(link, firstEndpoint);
            float distance = HorizontalDistance(position, candidatePosition);
            if (distance >= bestDistance)
                return;

            bestDistance = distance;
            targetPosition = candidatePosition;
            targetAssetId = link.AssetId;
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }

        private static bool TryGetReusableSubwayEntrance(int assetId, Vector3 position, out CrossingLandingAccessAssetWorkOrder reusableEntrance)
        {
            reusableEntrance = default(CrossingLandingAccessAssetWorkOrder);
            float bestDistance = SubwayEntranceReuseRadius;
            for (int i = 0; i < _accessAssetCount; i++)
            {
                CrossingLandingAccessAssetWorkOrder order = AccessAssetBuffer[i];
                if (order.AssetKind != CrossingLandingAccessAssetKind.SubwayEntrance
                    || order.AssetId == assetId
                    || order.ReusesExistingEntrance)
                {
                    continue;
                }

                float distance = HorizontalDistance(position, order.Position);
                if (distance > bestDistance)
                    continue;

                bestDistance = distance;
                reusableEntrance = order;
            }

            return reusableEntrance.AssetId != 0;
        }

        private static void LogSubwayEntranceReuse(int assetId, string endpointName, CrossingLandingAccessAssetWorkOrder reusableEntrance, Vector3 requestedPosition)
        {
            if (!PedestrianCrossingLog.VerboseDiagnostics)
                return;

            Debug.Log("[PedestrianCrossingToolkit] Subway entrance reused: asset="
                      + assetId
                      + " endpoint="
                      + endpointName
                      + " reusedAsset="
                      + reusableEntrance.AssetId
                      + " reusedEndpoint="
                      + reusableEntrance.EndpointName
                      + " distance="
                      + HorizontalDistance(requestedPosition, reusableEntrance.Position).ToString("0.0"));
        }

        private static bool IsBridgeKind(CrossingConnectivityLinkKind kind)
        {
            return kind == CrossingConnectivityLinkKind.PedestrianBridgeSpan
                   || kind == CrossingConnectivityLinkKind.JunctionBridgeApproach;
        }

        private static bool IsSubwayKind(CrossingConnectivityLinkKind kind)
        {
            return kind == CrossingConnectivityLinkKind.SubwaySpan
                   || kind == CrossingConnectivityLinkKind.JunctionSubwayApproach;
        }

        private static bool IsJunctionGradeSeparatedKind(CrossingConnectivityLinkKind kind)
        {
            return kind == CrossingConnectivityLinkKind.JunctionSubwayApproach
                   || kind == CrossingConnectivityLinkKind.JunctionBridgeApproach;
        }

        private static void AddWorkOrder(CrossingLandingConnectorWorkOrder order)
        {
            if (_workOrderCount >= WorkOrderBuffer.Length)
                return;

            WorkOrderBuffer[_workOrderCount++] = order;
        }

        private static void AddAccessAsset(CrossingConnectivityLink link, bool firstEndpoint, string endpointName, CrossingLandingAccessKind accessKind, CrossingLandingConnectorTargetKind targetKind, Vector3 position)
        {
            if (_accessAssetCount >= AccessAssetBuffer.Length)
                return;

            Vector3 deckPosition = firstEndpoint ? link.FirstPosition : link.SecondPosition;
            Vector3 crossingDirection = link.SecondPosition - link.FirstPosition;
            crossingDirection.y = 0f;
            if (crossingDirection.sqrMagnitude < 0.01f)
                crossingDirection = Vector3.forward;
            else
                crossingDirection.Normalize();

            CrossingLandingAccessAssetKind assetKind = GetAccessAssetKind(link);
            if (!TryResolveOffRoadAccessPosition(link, assetKind, deckPosition, ref position, endpointName))
                return;

            if (assetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding)
                position = GetBridgeAccessPadGroundPosition(link, position);

            Vector3 facing = ResolveRoadParallelFacing(link, endpointName, ResolveFacingDirection(deckPosition, position, firstEndpoint ? -crossingDirection : crossingDirection));
            Vector3 accessPosition = position;
            if (assetKind == CrossingLandingAccessAssetKind.SubwayEntrance)
                deckPosition = GetSubwayEntranceFunctionalEntryPosition(accessPosition, facing);

            float length = assetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding ? HorizontalDistance(deckPosition, position) : 5f;
            float width = assetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding ? 1f : 1.4f;

            ushort accessSegmentId = ResolveEndpointSegmentId(link, endpointName);
            CrossingLandingAccessAssetWorkOrder reusableEntrance;
            if (assetKind == CrossingLandingAccessAssetKind.SubwayEntrance
                && TryGetReusableSubwayEntrance(link.AssetId, accessPosition, out reusableEntrance))
            {
                AccessAssetBuffer[_accessAssetCount++] = new CrossingLandingAccessAssetWorkOrder(
                    link.AssetId,
                    accessSegmentId,
                    link.Kind,
                    endpointName,
                    assetKind,
                    accessKind,
                    targetKind,
                    reusableEntrance.DeckPosition,
                    reusableEntrance.Position,
                    reusableEntrance.FacingDirection,
                    reusableEntrance.FootprintLength,
                    reusableEntrance.FootprintWidth,
                    true,
                    reusableEntrance.AssetId,
                    reusableEntrance.EndpointName);
                LogSubwayEntranceReuse(link.AssetId, endpointName, reusableEntrance, accessPosition);
                return;
            }

            AccessAssetBuffer[_accessAssetCount++] = new CrossingLandingAccessAssetWorkOrder(link.AssetId, accessSegmentId, link.Kind, endpointName, assetKind, accessKind, targetKind, deckPosition, accessPosition, facing, length, width);
            if (assetKind == CrossingLandingAccessAssetKind.SubwayEntrance
                && !RoadPlacementRules.IsNonRoadGradeSeparatedPlacementTarget(accessSegmentId))
            {
                AddLaneTargetGroundConnector(link, endpointName, accessKind, accessPosition, deckPosition, accessSegmentId);
            }
        }

        private static void PlanJunctionLaneAccess(CrossingConnectivityLink link)
        {
            if (!IsJunctionGradeSeparatedKind(link.Kind))
                return;

            if (IsBridgeKind(link.Kind))
            {
                PlanBridgeSpanAccess(link, null, 0);
                return;
            }

            if (IsSubwayKind(link.Kind))
            {
                PlanSubwayLaneAccess(link);
                return;
            }

            Vector3 midpoint = (link.FirstPosition + link.SecondPosition) * 0.5f;
            CrossingLandingAccessKind accessKind = CrossingLandingAccessKind.DirectLanding;
            AddJunctionLaneAccessAsset(link, "A", accessKind, midpoint, link.FirstPosition);
            AddJunctionLaneAccessAsset(link, "B", accessKind, midpoint, link.SecondPosition);
        }

        private static void AddJunctionLaneAccessAsset(CrossingConnectivityLink link, string endpointName, CrossingLandingAccessKind accessKind, Vector3 deckPosition, Vector3 position)
        {
            Vector3 accessPosition;
            TryAddJunctionLaneAccessAsset(link, endpointName, accessKind, deckPosition, position, out accessPosition);
        }

        private static bool TryAddJunctionLaneAccessAsset(CrossingConnectivityLink link, string endpointName, CrossingLandingAccessKind accessKind, Vector3 deckPosition, Vector3 position, out Vector3 accessPosition)
        {
            if (_accessAssetCount >= AccessAssetBuffer.Length)
            {
                accessPosition = Vector3.zero;
                return false;
            }

            CrossingLandingAccessAssetKind assetKind = GetAccessAssetKind(link);
            if (!TryResolveOffRoadAccessPosition(link, assetKind, deckPosition, ref position, endpointName))
            {
                accessPosition = Vector3.zero;
                return false;
            }

            if (assetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding)
                position = GetBridgeAccessPadGroundPosition(link, position);

            Vector3 facing = ResolveRoadParallelFacing(link, endpointName, ResolveFacingDirection(deckPosition, position, Vector3.forward));
            accessPosition = position;
            if (assetKind == CrossingLandingAccessAssetKind.SubwayEntrance)
                deckPosition = GetSubwayEntranceFunctionalEntryPosition(accessPosition, facing);

            float length = assetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding ? HorizontalDistance(deckPosition, position) : 5f;
            float width = assetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding ? 1f : 1.4f;

            ushort accessSegmentId = ResolveEndpointSegmentId(link, endpointName);
            CrossingLandingAccessAssetWorkOrder reusableEntrance;
            if (assetKind == CrossingLandingAccessAssetKind.SubwayEntrance
                && TryGetReusableSubwayEntrance(link.AssetId, accessPosition, out reusableEntrance))
            {
                AccessAssetBuffer[_accessAssetCount++] = new CrossingLandingAccessAssetWorkOrder(
                    link.AssetId,
                    accessSegmentId,
                    link.Kind,
                    endpointName,
                    assetKind,
                    accessKind,
                    CrossingLandingConnectorTargetKind.PedestrianLane,
                    reusableEntrance.DeckPosition,
                    reusableEntrance.Position,
                    reusableEntrance.FacingDirection,
                    reusableEntrance.FootprintLength,
                    reusableEntrance.FootprintWidth,
                    true,
                    reusableEntrance.AssetId,
                    reusableEntrance.EndpointName);
                LogSubwayEntranceReuse(link.AssetId, endpointName, reusableEntrance, accessPosition);
                accessPosition = reusableEntrance.Position;
                return true;
            }

            AccessAssetBuffer[_accessAssetCount++] = new CrossingLandingAccessAssetWorkOrder(link.AssetId, accessSegmentId, link.Kind, endpointName, assetKind, accessKind, CrossingLandingConnectorTargetKind.PedestrianLane, deckPosition, accessPosition, facing, length, width);
            if (IsSubwayKind(link.Kind)
                && !RoadPlacementRules.IsNonRoadGradeSeparatedPlacementTarget(accessSegmentId))
            {
                AddLaneTargetGroundConnector(link, endpointName, accessKind, accessPosition, deckPosition, accessSegmentId);
            }

            return true;
        }

        private static void AddLaneTargetGroundConnector(CrossingConnectivityLink link, string endpointName, CrossingLandingAccessKind accessKind, Vector3 fromPosition, Vector3 toPosition, ushort segmentId)
        {
            if (!IsBridgeKind(link.Kind) && !IsSubwayKind(link.Kind))
                return;

            float minDistance = IsSubwayKind(link.Kind) ? 0.25f : 1.5f;
            if (HorizontalDistance(fromPosition, toPosition) <= minDistance)
                return;

            NetInfo prefab = PedestrianCrossingPrefabCatalog.SurfaceCrossingPathPrefab
                             ?? PedestrianCrossingPrefabCatalog.PedestrianPathPrefab;
            CrossingLandingConnectorWorkOrder order = new CrossingLandingConnectorWorkOrder(
                link.AssetId,
                segmentId,
                link.Kind,
                endpointName,
                accessKind,
                CrossingLandingConnectorTargetKind.PedestrianLane,
                link.AssetId,
                prefab,
                fromPosition,
                toPosition);
            AddWorkOrder(order);
        }

        private static Vector3 GetSubwayEntranceFunctionalEntryPosition(Vector3 position, Vector3 facing)
        {
            Vector3 direction = facing;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return position;

            direction.Normalize();
            return position + direction * SubwayEntranceFunctionalEntryOffset;
        }

        private static CrossingLandingAccessKind GetSubwayAccessKind(CrossingConnectivityLink link, bool firstEndpoint, Vector3 fromPosition, Vector3 toPosition, CrossingLandingConnectorTargetKind targetKind)
        {
            float alignment = GetOutwardAlignment(link, firstEndpoint, fromPosition, toPosition);
            float distance = HorizontalDistance(fromPosition, toPosition);

            if (targetKind == CrossingLandingConnectorTargetKind.RoadEdgeLanding && distance <= SplitEntranceDistance)
                return CrossingLandingAccessKind.SubwayYShapedEntrance;

            if (targetKind == CrossingLandingConnectorTargetKind.RoadEdgeLanding)
                return CrossingLandingAccessKind.SubwayXShapedEntrance;

            if (alignment > 0.72f)
                return CrossingLandingAccessKind.SubwayStraightEntrance;

            if (alignment < -0.25f || distance > BentEntranceDistance)
                return CrossingLandingAccessKind.SubwayUShapedEntrance;

            return CrossingLandingAccessKind.SubwayZShapedEntrance;
        }

        private static void PlanSubwayLaneAccess(CrossingConnectivityLink link)
        {
            if (!IsSubwayKind(link.Kind))
                return;

            Vector3 crossing = link.SecondPosition - link.FirstPosition;
            crossing.y = 0f;
            if (crossing.sqrMagnitude < 0.01f)
                return;

            crossing.Normalize();
            AddSubwayLaneAccessAsset(link, "A", link.FirstPosition, link.FirstPosition);
            AddSubwayLaneAccessAsset(link, "B", link.SecondPosition, link.SecondPosition);
        }

        private static void AddSubwayLaneAccessAsset(CrossingConnectivityLink link, string endpointName, Vector3 deckPosition, Vector3 position)
        {
            AddJunctionLaneAccessAsset(link, endpointName, CrossingLandingAccessKind.SubwayStraightEntrance, deckPosition, position);
        }

        private static Vector3 ResolveRoadParallelFacing(CrossingConnectivityLink link, string endpointName, Vector3 fallback)
        {
            Vector3 roadDirection;
            if (GradeSeparatedPlacementGeometryResolver.TryGetAccessDirection(link, endpointName, out roadDirection))
                return NormalizeHorizontal(roadDirection);

            return NormalizeHorizontal(fallback);
        }

        private static ushort ResolveEndpointSegmentId(CrossingConnectivityLink link, string endpointName)
        {
            CrossingPlacementAsset asset;
            if (!CrossingPlacementRegistry.TryGetAssetById(link.AssetId, out asset)
                || asset.Plan.ApplicationKind != CrossingApplicationKind.SubwayPointToPoint)
            {
                return link.SegmentId;
            }

            return string.Equals(endpointName, "B", System.StringComparison.OrdinalIgnoreCase)
                ? asset.Placement.SecondarySegmentId
                : asset.Placement.SegmentId;
        }

        private static Vector3 NormalizeHorizontal(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return Vector3.forward;

            direction.Normalize();
            return direction;
        }

        private static void PlanBridgeSpanAccess(CrossingConnectivityLink link, CrossingConnectivityCandidate[] candidates, int candidateCount)
        {
            Vector3 crossing = link.SecondPosition - link.FirstPosition;
            crossing.y = 0f;
            if (crossing.sqrMagnitude < 0.01f)
                return;

            crossing.Normalize();
            Vector3 sidewalkDirection = new Vector3(-crossing.z, 0f, crossing.x);
            float rampRun = BridgeRampStandardDistance;
            if (link.Kind == CrossingConnectivityLinkKind.JunctionBridgeApproach)
            {
                Vector3 junctionDirection;
                if (!TryGetJunctionAccessDirection(link, out junctionDirection))
                    junctionDirection = sidewalkDirection;

                AddBridgeLaneAccessEndpoints(link, junctionDirection, candidates, candidateCount);
                return;
            }

            Vector3 accessDirection = GetBridgeSpanAccessDirection(link, sidewalkDirection, rampRun);
            AddBridgeLaneAccessEndpoints(link, accessDirection, candidates, candidateCount);
        }

        private static void AddBridgeLaneAccessEndpoints(CrossingConnectivityLink link, Vector3 accessDirection, CrossingConnectivityCandidate[] candidates, int candidateCount)
        {
            accessDirection = NormalizeHorizontal(accessDirection);
            float firstRun;
            float secondRun;
            GetBridgeSlopeAdjustedRampRuns(link, accessDirection, out firstRun, out secondRun);
            AddBridgeLaneAccessEndpoint(link, "A", link.FirstPosition, link.FirstPosition + accessDirection * firstRun, candidates, candidateCount);
            AddBridgeLaneAccessEndpoint(link, "B", link.SecondPosition, link.SecondPosition + accessDirection * secondRun, candidates, candidateCount);
        }

        private static void AddBridgeLaneAccessEndpoint(CrossingConnectivityLink link, string endpointName, Vector3 deckPosition, Vector3 position, CrossingConnectivityCandidate[] candidates, int candidateCount)
        {
            Vector3 accessPosition;
            if (!TryAddBridgeLaneAccessAsset(link, endpointName, deckPosition, position, out accessPosition))
            {
                Vector3 alternativePosition = deckPosition - (position - deckPosition);
                if (!TryAddBridgeLaneAccessAsset(link, endpointName, deckPosition, alternativePosition, out accessPosition))
                    return;
            }

            Vector3 exitPosition = GetBridgeExitWalkOutPosition(deckPosition, accessPosition);
            AddBridgeGroundConnectorToNearestLane(link, endpointName, CrossingLandingAccessKind.BridgeStraightStairs, deckPosition, exitPosition, candidates, candidateCount);
        }

        private static void GetBridgeSlopeAdjustedRampRuns(CrossingConnectivityLink link, Vector3 accessDirection, out float firstRun, out float secondRun)
        {
            firstRun = BridgeRampStandardDistance;
            secondRun = BridgeRampStandardDistance;

            float firstDrop;
            float secondDrop;
            if (!TryGetBridgeRampDrop(link, link.FirstPosition, accessDirection, BridgeRampStandardDistance, out firstDrop)
                || !TryGetBridgeRampDrop(link, link.SecondPosition, accessDirection, BridgeRampStandardDistance, out secondDrop))
            {
                return;
            }

            secondRun = GetBridgeRampRunRelativeToDrop(secondDrop, firstDrop);
        }

        private static float GetBridgeRampRunRelativeToDrop(float drop, float referenceDrop)
        {
            return GetBridgeRampRunDistance(BridgeRampStandardDistance * Mathf.Max(MinBridgeRampDrop, drop) / Mathf.Max(MinBridgeRampDrop, referenceDrop));
        }

        private static bool TryGetBridgeRampDrop(CrossingConnectivityLink link, Vector3 deckPosition, Vector3 accessDirection, float runLength, out float drop)
        {
            drop = 0f;
            Vector3 samplePosition = deckPosition + accessDirection * runLength;
            float pavementHeight;
            if (!TryGetBridgePavementHeight(link.SegmentId, samplePosition, out pavementHeight))
                return false;

            drop = deckPosition.y + CrossingVerticalProfile.BridgeDeckHeight - pavementHeight;
            return drop > 0.1f;
        }

        private static Vector3 GetBridgeAccessPadGroundPosition(CrossingConnectivityLink link, Vector3 position)
        {
            float height;
            if (TryGetBridgePavementHeight(link.SegmentId, position, out height))
            {
                position.y = height;
                return position;
            }

            TerrainManager terrainManager = TerrainManager.instance;
            if (terrainManager != null)
                position.y = terrainManager.SampleRawHeightSmooth(position);

            return position;
        }

        private static bool TryGetBridgePavementHeight(ushort segmentId, Vector3 position, out float height)
        {
            height = position.y;
            Vector3 laneSurface;
            if (RoadPavementAnchorResolver.TryGetPedestrianLaneSurfacePosition(
                    segmentId,
                    position,
                    BridgeRampLaneSurfaceSnapDistance,
                    out laneSurface))
            {
                height = laneSurface.y;
                return true;
            }

            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return false;

            height = segment.GetClosestPosition(position).y;
            return true;
        }

        private static Vector3 GetBridgeExitWalkOutPosition(Vector3 deckPosition, Vector3 position)
        {
            Vector3 direction = position - deckPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return position;

            direction.Normalize();
            return position + direction * BridgeExitWalkOutDistance;
        }

        private static void AddBridgeGroundConnectorToNearestLane(CrossingConnectivityLink link, string endpointName, CrossingLandingAccessKind accessKind, Vector3 deckPosition, Vector3 fromPosition, CrossingConnectivityCandidate[] candidates, int candidateCount)
        {
            if (candidates == null || candidateCount <= 0)
                return;

            Vector3 targetPosition;
            int targetAssetId;
            if (!TryFindNearestBridgeExitPedestrianLaneForAsset(link.AssetId, deckPosition, fromPosition, candidates, candidateCount, out targetPosition, out targetAssetId)
                && !TryFindNearestPedestrianLaneForAsset(link.AssetId, fromPosition, candidates, candidateCount, out targetPosition, out targetAssetId))
            {
                return;
            }

            NetInfo prefab = PedestrianCrossingPrefabCatalog.SurfaceCrossingPathPrefab
                             ?? PedestrianCrossingPrefabCatalog.PedestrianPathPrefab;
            CrossingLandingConnectorWorkOrder order = new CrossingLandingConnectorWorkOrder(
                link.AssetId,
                link.SegmentId,
                link.Kind,
                endpointName,
                accessKind,
                CrossingLandingConnectorTargetKind.PedestrianLane,
                targetAssetId,
                prefab,
                fromPosition,
                targetPosition);
            AddWorkOrder(order);
        }

        private static bool TryFindNearestBridgeExitPedestrianLaneForAsset(int assetId, Vector3 deckPosition, Vector3 position, CrossingConnectivityCandidate[] candidates, int candidateCount, out Vector3 targetPosition, out int targetAssetId)
        {
            targetPosition = Vector3.zero;
            targetAssetId = 0;
            Vector3 exitDirection = position - deckPosition;
            exitDirection.y = 0f;
            if (exitDirection.sqrMagnitude <= 0.01f)
                return false;

            exitDirection.Normalize();
            float bestDistance = MaxConnectorDistance;
            int max = Mathf.Min(candidateCount, candidates.Length);
            for (int i = 0; i < max; i++)
            {
                CrossingConnectivityCandidate candidate = candidates[i];
                if (candidate.AssetId != assetId)
                    continue;

                Vector3 candidateDirection = candidate.WorldPosition - position;
                candidateDirection.y = 0f;
                float distance = candidateDirection.magnitude;
                if (distance <= 0.5f || distance >= bestDistance)
                    continue;

                candidateDirection /= distance;
                if (Vector3.Dot(exitDirection, candidateDirection) < -0.1f)
                    continue;

                bestDistance = distance;
                targetPosition = candidate.WorldPosition;
                targetAssetId = candidate.AssetId;
            }

            return targetAssetId != 0;
        }

        private static Vector3 GetBridgeSpanAccessDirection(CrossingConnectivityLink link, Vector3 fallbackDirection, float rampRun)
        {
            Vector3 sidewalkDirection;
            Vector3 spanCenter = (link.FirstPosition + link.SecondPosition) * 0.5f;
            if (!TryGetBridgeSpanSidewalkDirection(link.SegmentId, spanCenter, out sidewalkDirection))
            {
                sidewalkDirection = fallbackDirection;
                if (sidewalkDirection.sqrMagnitude <= 0.01f)
                    sidewalkDirection = Vector3.forward;
                else
                    sidewalkDirection.Normalize();
            }

            Vector3 oppositeDirection = -sidewalkDirection;
            Vector3 junctionPosition;
            if (TryGetNearbyThreePlusJunctionPosition(link, out junctionPosition))
            {
                float sidewalkDistance = GetNearestBridgeSpanPadDistanceTo(link, sidewalkDirection, rampRun, junctionPosition);
                float oppositeDistance = GetNearestBridgeSpanPadDistanceTo(link, oppositeDirection, rampRun, junctionPosition);
                Vector3 selected = sidewalkDistance >= oppositeDistance ? sidewalkDirection : oppositeDirection;
                return link.FlipBridgeAccess ? -selected : selected;
            }

            int sidewalkBlocked = CountBlockedBridgeSpanPads(link, sidewalkDirection, rampRun);
            int oppositeBlocked = CountBlockedBridgeSpanPads(link, oppositeDirection, rampRun);
            if (sidewalkBlocked < oppositeBlocked)
                return link.FlipBridgeAccess ? -sidewalkDirection : sidewalkDirection;

            if (oppositeBlocked < sidewalkBlocked)
                return link.FlipBridgeAccess ? -oppositeDirection : oppositeDirection;

            return link.FlipBridgeAccess ? -sidewalkDirection : sidewalkDirection;
        }

        private static bool TryGetJunctionAccessDirection(CrossingConnectivityLink link, out Vector3 direction)
        {
            if (GradeSeparatedPlacementGeometryResolver.TryGetAccessDirection(link, out direction))
                return true;

            Vector3 junctionPosition;
            if (TryGetNearbyThreePlusJunctionPosition(link, out junctionPosition))
            {
                direction = ((link.FirstPosition + link.SecondPosition) * 0.5f) - junctionPosition;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.01f)
                {
                    direction.Normalize();
                    return true;
                }
            }

            direction = Vector3.zero;
            return false;
        }

        private static bool TryGetBridgeSpanSidewalkDirection(ushort segmentId, Vector3 spanCenter, out Vector3 sidewalkDirection)
        {
            sidewalkDirection = Vector3.zero;
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0
                || segment.m_startNode == 0
                || segment.m_endNode == 0
                || segment.m_startNode >= netManager.m_nodes.m_size
                || segment.m_endNode >= netManager.m_nodes.m_size)
            {
                return false;
            }

            Vector3 start = netManager.m_nodes.m_buffer[segment.m_startNode].m_position;
            Vector3 end = netManager.m_nodes.m_buffer[segment.m_endNode].m_position;
            Vector3 roadDirection = end - start;
            roadDirection.y = 0f;
            if (roadDirection.sqrMagnitude <= 0.01f)
                return false;

            roadDirection.Normalize();
            float startDistance = HorizontalDistance(spanCenter, start);
            float endDistance = HorizontalDistance(spanCenter, end);
            sidewalkDirection = startDistance < endDistance ? roadDirection : -roadDirection;
            return true;
        }

        private static int CountBlockedBridgeSpanPads(CrossingConnectivityLink link, Vector3 direction, float rampRun)
        {
            int blocked = 0;
            if (IsBridgeAccessPadBlocked(link, link.FirstPosition, link.FirstPosition + direction * rampRun, false))
                blocked++;

            if (IsBridgeAccessPadBlocked(link, link.SecondPosition, link.SecondPosition + direction * rampRun, false))
                blocked++;

            return blocked;
        }

        private static float GetNearestBridgeSpanPadDistanceTo(CrossingConnectivityLink link, Vector3 direction, float rampRun, Vector3 target)
        {
            float firstDistance = HorizontalDistance(link.FirstPosition + direction * rampRun, target);
            float secondDistance = HorizontalDistance(link.SecondPosition + direction * rampRun, target);
            return Mathf.Min(firstDistance, secondDistance);
        }

        private static void AddBridgeLaneAccessAsset(CrossingConnectivityLink link, string endpointName, Vector3 deckPosition, Vector3 position)
        {
            AddJunctionLaneAccessAsset(link, endpointName, CrossingLandingAccessKind.BridgeStraightStairs, deckPosition, position);
        }

        private static bool TryAddBridgeLaneAccessAsset(CrossingConnectivityLink link, string endpointName, Vector3 deckPosition, Vector3 position, out Vector3 accessPosition)
        {
            return TryAddJunctionLaneAccessAsset(link, endpointName, CrossingLandingAccessKind.BridgeStraightStairs, deckPosition, position, out accessPosition);
        }

        private static float GetBridgeRampRunDistance(float preferredDistance)
        {
            return Mathf.Clamp(preferredDistance, BridgeRampMinDistance, BridgeRampMaxDistance);
        }

        private static CrossingLandingAccessAssetKind GetAccessAssetKind(CrossingConnectivityLink link)
        {
            return IsBridgeKind(link.Kind)
                ? CrossingLandingAccessAssetKind.BridgeStairRampLanding
                : CrossingLandingAccessAssetKind.SubwayEntrance;
        }

        private static bool IsBridgeAccessPadBlocked(CrossingConnectivityLink link, Vector3 deckPosition, Vector3 position)
        {
            return IsBridgeAccessPadBlocked(link, deckPosition, position, true);
        }

        private static bool IsBridgeAccessPadBlocked(CrossingConnectivityLink link, Vector3 deckPosition, Vector3 position, bool log)
        {
            return IsBridgePadTowardNearbyJunction(link, deckPosition, position, log);
        }

        private static bool TryResolveOffRoadAccessPosition(CrossingConnectivityLink link, CrossingLandingAccessAssetKind assetKind, Vector3 deckPosition, ref Vector3 position, string endpointName)
        {
            Vector3 anchoredPosition;
            if (TryResolvePedestrianLaneAccessPosition(link, assetKind, deckPosition, position, endpointName, out anchoredPosition))
            {
                position = anchoredPosition;
                return true;
            }

            Vector3 direction = position - deckPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
            {
                direction = GetOutwardDirection(link, endpointName == "A");
            }
            else
            {
                direction.Normalize();
            }

            for (int i = 0; i <= MaxOffRoadAccessNudges; i++)
            {
                if (!RoadSurfacePlacementGuard.IsOnRoadSurface(position)
                    && (assetKind != CrossingLandingAccessAssetKind.BridgeStairRampLanding
                        || !IsBridgeAccessPadBlocked(link, deckPosition, position)))
                {
                    return true;
                }

                position += direction * OffRoadAccessNudge;
            }

            Debug.Log("[PedestrianCrossingToolkit] Grade separated access asset could not be moved off road: asset="
                      + link.AssetId
                      + " segment="
                      + link.SegmentId
                      + " endpoint="
                      + endpointName
                      + " assetKind="
                      + assetKind
                      + " position="
                      + position);
            return false;
        }

        private static bool TryResolvePedestrianLaneAccessPosition(CrossingConnectivityLink link, CrossingLandingAccessAssetKind assetKind, Vector3 deckPosition, Vector3 requestedPosition, string endpointName, out Vector3 resolvedPosition)
        {
            resolvedPosition = Vector3.zero;
            if (!link.UsesLaneTargets)
                return false;

            if (assetKind == CrossingLandingAccessAssetKind.SubwayEntrance)
            {
                resolvedPosition = requestedPosition;
                return true;
            }

            if (assetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding)
                return false;

            NetManager netManager = NetManager.instance;
            if (netManager == null || link.SegmentId == 0 || link.SegmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[link.SegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0
                || segment.Info == null
                || segment.Info.m_lanes == null
                || segment.m_startNode == 0
                || segment.m_endNode == 0
                || segment.m_startNode >= netManager.m_nodes.m_size
                || segment.m_endNode >= netManager.m_nodes.m_size)
            {
                return false;
            }

            Vector3 start = netManager.m_nodes.m_buffer[segment.m_startNode].m_position;
            Vector3 end = netManager.m_nodes.m_buffer[segment.m_endNode].m_position;
            Vector3 roadVector = end - start;
            roadVector.y = 0f;
            float roadLength = roadVector.magnitude;
            if (roadLength <= 0.01f)
                return false;

            Vector3 roadDirection = roadVector / roadLength;
            Vector3 roadRight = new Vector3(-roadDirection.z, 0f, roadDirection.x);
            float deckAlong = Mathf.Clamp(Vector3.Dot(deckPosition - start, roadDirection), 0f, roadLength);
            Vector3 deckCenter = start + roadDirection * deckAlong;
            float deckLateral = Vector3.Dot(deckPosition - deckCenter, roadRight);

            float lanePosition;
            if (!RoadPavementAnchorResolver.TryGetNearestPedestrianLanePosition(segment.Info, deckLateral, out lanePosition))
                return false;

            float requestedAlongDelta = Vector3.Dot(requestedPosition - deckPosition, roadDirection);
            float requestedLateralDelta = Vector3.Dot(requestedPosition - deckPosition, roadRight);
            if (Mathf.Abs(requestedAlongDelta) <= 0.01f && Mathf.Abs(requestedLateralDelta) > 0.01f)
                requestedAlongDelta = 0f;

            float accessAlong = Mathf.Clamp(deckAlong + requestedAlongDelta, 0f, roadLength);
            Vector3 accessCenter = start + roadDirection * accessAlong;
            resolvedPosition = accessCenter + roadRight * lanePosition;
            resolvedPosition.y = Mathf.Lerp(start.y, end.y, roadLength <= 0.01f ? 0f : accessAlong / roadLength);

            if (assetKind == CrossingLandingAccessAssetKind.BridgeStairRampLanding
                && IsBridgeAccessPadBlocked(link, deckPosition, resolvedPosition))
            {
                return false;
            }

            if (HorizontalDistance(resolvedPosition, requestedPosition) > 0.25f)
            {
                Debug.Log("[PedestrianCrossingToolkit] Access asset anchored to pedestrian lane: asset="
                          + link.AssetId
                          + " segment="
                          + link.SegmentId
                          + " endpoint="
                          + endpointName
                          + " assetKind="
                          + assetKind
                          + " lanePosition="
                          + lanePosition.ToString("0.00")
                          + " requested="
                          + requestedPosition
                          + " resolved="
                          + resolvedPosition);
            }

            return true;
        }

        private static Vector3 GetOutwardDirection(CrossingConnectivityLink link, bool firstEndpoint)
        {
            Vector3 crossingDirection = link.SecondPosition - link.FirstPosition;
            crossingDirection.y = 0f;
            if (crossingDirection.sqrMagnitude <= 0.01f)
                return Vector3.forward;

            crossingDirection.Normalize();
            return firstEndpoint ? -crossingDirection : crossingDirection;
        }

        private static Vector3 ResolveFacingDirection(Vector3 deckPosition, Vector3 position, Vector3 fallback)
        {
            Vector3 facing = position - deckPosition;
            facing.y = 0f;
            if (facing.sqrMagnitude <= 0.01f)
                facing = fallback;

            facing.y = 0f;
            if (facing.sqrMagnitude <= 0.01f)
                return Vector3.forward;

            facing.Normalize();
            return facing;
        }

        private static bool IsBridgePadTowardNearbyJunction(CrossingConnectivityLink link, Vector3 deckPosition, Vector3 position, bool log)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || link.SegmentId == 0 || link.SegmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[link.SegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return false;

            ushort nodeId = GetNearestSegmentNode(netManager, ref segment, (link.FirstPosition + link.SecondPosition) * 0.5f);
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            if (CountCreatedRoadSegmentsAtNode(netManager, nodeId) < 3)
                return false;

            Vector3 nodePosition = netManager.m_nodes.m_buffer[nodeId].m_position;
            Vector3 spanCenter = (link.FirstPosition + link.SecondPosition) * 0.5f;
            if (HorizontalDistance(spanCenter, nodePosition) > 36f)
                return false;

            float deckNodeDistance = HorizontalDistance(deckPosition, nodePosition);
            float padNodeDistance = HorizontalDistance(position, nodePosition);
            if (padNodeDistance >= deckNodeDistance - 0.25f)
                return false;

            Vector3 padDirection = position - deckPosition;
            Vector3 nodeDirection = nodePosition - deckPosition;
            padDirection.y = 0f;
            nodeDirection.y = 0f;
            if (padDirection.sqrMagnitude < 0.01f || nodeDirection.sqrMagnitude < 0.01f)
                return false;

            padDirection.Normalize();
            nodeDirection.Normalize();
            if (Vector3.Dot(padDirection, nodeDirection) < 0.45f)
                return false;

            if (log)
            {
                Debug.Log("[PedestrianCrossingToolkit] Bridge access pad skipped toward junction: asset="
                          + link.AssetId
                          + " segment="
                          + link.SegmentId
                          + " node="
                          + nodeId
                          + " deck="
                          + deckPosition
                          + " position="
                          + position);
            }

            return true;
        }

        private static bool TryGetNearbyThreePlusJunctionPosition(CrossingConnectivityLink link, out Vector3 junctionPosition)
        {
            junctionPosition = Vector3.zero;
            NetManager netManager = NetManager.instance;
            if (netManager == null || link.SegmentId == 0 || link.SegmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[link.SegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return false;

            ushort nodeId = GetNearestSegmentNode(netManager, ref segment, (link.FirstPosition + link.SecondPosition) * 0.5f);
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            if (CountCreatedRoadSegmentsAtNode(netManager, nodeId) < 3)
                return false;

            junctionPosition = netManager.m_nodes.m_buffer[nodeId].m_position;
            Vector3 spanCenter = (link.FirstPosition + link.SecondPosition) * 0.5f;
            return HorizontalDistance(spanCenter, junctionPosition) <= 36f;
        }

        private static int CountCreatedRoadSegmentsAtNode(NetManager netManager, ushort nodeId)
        {
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return 0;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            int count = 0;
            int segmentCount = node.CountSegments();
            for (int i = 0; i < segmentCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null || !(segment.Info.m_netAI is RoadBaseAI))
                    continue;

                count++;
            }

            return count;
        }

        private static ushort GetNearestSegmentNode(NetManager netManager, ref NetSegment segment, Vector3 position)
        {
            ushort startNodeId = segment.m_startNode;
            ushort endNodeId = segment.m_endNode;
            if (startNodeId == 0)
                return endNodeId;
            if (endNodeId == 0)
                return startNodeId;
            if (startNodeId >= netManager.m_nodes.m_size)
                return endNodeId;
            if (endNodeId >= netManager.m_nodes.m_size)
                return startNodeId;

            Vector3 start = netManager.m_nodes.m_buffer[startNodeId].m_position;
            Vector3 end = netManager.m_nodes.m_buffer[endNodeId].m_position;
            return HorizontalDistance(position, start) <= HorizontalDistance(position, end)
                ? startNodeId
                : endNodeId;
        }

        private static int CountAccessAssets(CrossingLandingAccessAssetKind kind)
        {
            int count = 0;
            for (int i = 0; i < _accessAssetCount; i++)
            {
                if (AccessAssetBuffer[i].AssetKind == kind)
                    count++;
            }

            return count;
        }

        private static void LogWorkOrders(string reason)
        {
            if (!PedestrianCrossingLog.VerboseDiagnostics)
                return;

            int logCount = Mathf.Min(_workOrderCount, MaxConnectorLogsPerRefresh);
            for (int i = 0; i < logCount; i++)
            {
                Debug.Log("[PedestrianCrossingToolkit] Landing connector work order: reason=" + reason + " index=" + i + " " + WorkOrderBuffer[i].ToLogString());
            }

            if (_workOrderCount > logCount)
            {
                Debug.Log("[PedestrianCrossingToolkit] Landing connector work order log truncated: reason="
                          + reason
                          + " shown=" + logCount
                          + " total=" + _workOrderCount);
            }
        }

        private static void LogAccessAssets(string reason)
        {
            if (!PedestrianCrossingLog.VerboseDiagnostics)
                return;

            int logCount = Mathf.Min(_accessAssetCount, MaxConnectorLogsPerRefresh);
            for (int i = 0; i < logCount; i++)
            {
                Debug.Log("[PedestrianCrossingToolkit] Landing access asset work order: reason=" + reason + " index=" + i + " " + AccessAssetBuffer[i].ToLogString());
            }

            if (_accessAssetCount > logCount)
            {
                Debug.Log("[PedestrianCrossingToolkit] Landing access asset work order log truncated: reason="
                          + reason
                          + " shown=" + logCount
                          + " total=" + _accessAssetCount);
            }
        }
    }
}
