using System.Collections.Generic;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public struct CrossingApplicationSummary
    {
        public static readonly CrossingApplicationSummary Empty = new CrossingApplicationSummary(0, 0, 0, 0, 0, 0, 0, 0);

        public readonly int Total;
        public readonly int PlannedOperations;
        public readonly int SurfaceCrossings;
        public readonly int SignalCrossings;
        public readonly int SubwayLinks;
        public readonly int PedestrianBridges;
        public readonly int SurfaceSuppressions;
        public readonly int InvalidPlans;

        public CrossingApplicationSummary(int total, int plannedOperations, int surfaceCrossings, int signalCrossings, int subwayLinks, int pedestrianBridges, int surfaceSuppressions, int invalidPlans)
        {
            Total = total;
            PlannedOperations = plannedOperations;
            SurfaceCrossings = surfaceCrossings;
            SignalCrossings = signalCrossings;
            SubwayLinks = subwayLinks;
            PedestrianBridges = pedestrianBridges;
            SurfaceSuppressions = surfaceSuppressions;
            InvalidPlans = invalidPlans;
        }

        public string ToLogString()
        {
            return "total=" + Total
                   + " operations=" + PlannedOperations
                   + " surface=" + SurfaceCrossings
                   + " signals=" + SignalCrossings
                   + " subways=" + SubwayLinks
                   + " bridges=" + PedestrianBridges
                   + " suppressSurface=" + SurfaceSuppressions
                   + " invalid=" + InvalidPlans;
        }
    }

    public static class CrossingApplicationEngine
    {
        private static readonly CrossingPlacementAsset[] AssetBuffer = new CrossingPlacementAsset[512];
        private static readonly CrossingNetworkOperation[] OperationBuffer = new CrossingNetworkOperation[1024];
        private const int MaxOperationLogsPerRefresh = 12;
        private static CrossingApplicationSummary _lastSummary = CrossingApplicationSummary.Empty;
        private static CrossingNetworkValidationSummary _lastValidationSummary = CrossingNetworkValidationSummary.Empty;
        private static CrossingConnectivitySummary _lastConnectivitySummary = CrossingConnectivitySummary.Empty;
        private static int _lastOperationCount;

        public static CrossingApplicationSummary LastSummary
        {
            get { return _lastSummary; }
        }

        public static CrossingNetworkValidationSummary LastValidationSummary
        {
            get { return _lastValidationSummary; }
        }

        public static CrossingConnectivitySummary LastConnectivitySummary
        {
            get { return _lastConnectivitySummary; }
        }

        public static int LastOperationCount
        {
            get { return _lastOperationCount; }
        }

        public static bool HasAppliedChanges
        {
            get { return CrossingNetworkApplier.HasAppliedChanges; }
        }

        public static void Refresh(string reason)
        {
            int count = CrossingPlacementRegistry.CopyTo(AssetBuffer);
            _lastOperationCount = 0;
            int surfaceCrossings = 0;
            int signalCrossings = 0;
            int subwayLinks = 0;
            int pedestrianBridges = 0;
            int surfaceSuppressions = 0;
            int invalidPlans = 0;

            for (int i = 0; i < count; i++)
            {
                CrossingPlacementAsset asset = AssetBuffer[i];
                CrossingPlacementPlan plan = asset.Plan;
                if (!plan.IsValid)
                {
                    invalidPlans++;
                    continue;
                }

                switch (plan.ApplicationKind)
                {
                    case CrossingApplicationKind.SurfaceCrossing:
                        surfaceCrossings++;
                        PlanSurfaceCrossing(asset);
                        break;
                    case CrossingApplicationKind.SignalizedSurfaceCrossing:
                        surfaceCrossings++;
                        signalCrossings++;
                        PlanSignalCrossing(asset);
                        break;
                    case CrossingApplicationKind.SubwayLink:
                        subwayLinks++;
                        AddOperation(CrossingNetworkOperationKind.CreateSubwayLink, asset, 0);
                        break;
                    case CrossingApplicationKind.SubwayPointToPoint:
                        subwayLinks++;
                        AddOperation(CrossingNetworkOperationKind.CreatePointToPointSubwayLink, asset, 0);
                        break;
                    case CrossingApplicationKind.SubwayJunctionSuppressSurface:
                        subwayLinks++;
                        surfaceSuppressions++;
                        AddOperation(CrossingNetworkOperationKind.CreateJunctionSubwayLink, asset, plan.TargetNodeId);
                        AddOperation(CrossingNetworkOperationKind.SuppressNodeSurfaceCrossings, asset, plan.TargetNodeId);
                        break;
                    case CrossingApplicationKind.PedestrianBridge:
                        pedestrianBridges++;
                        AddOperation(plan.TargetNodeId == 0 ? CrossingNetworkOperationKind.CreatePedestrianBridge : CrossingNetworkOperationKind.CreateJunctionPedestrianBridge, asset, plan.TargetNodeId);
                        if (plan.TargetNodeId != 0)
                        {
                            surfaceSuppressions++;
                            AddOperation(CrossingNetworkOperationKind.SuppressNodeSurfaceCrossings, asset, plan.TargetNodeId);
                        }
                        break;
                    default:
                        invalidPlans++;
                        break;
                }
            }

            _lastSummary = new CrossingApplicationSummary(count, _lastOperationCount, surfaceCrossings, signalCrossings, subwayLinks, pedestrianBridges, surfaceSuppressions, invalidPlans);
            _lastValidationSummary = ValidatePlannedOperations(reason);
            Debug.Log("[PedestrianCrossingToolkit] Application refresh: reason=" + reason + " " + _lastSummary.ToLogString());
            Debug.Log("[PedestrianCrossingToolkit] Network operation validation: reason=" + reason + " " + _lastValidationSummary.ToLogString());
            LogPlannedOperations(reason);
            _lastConnectivitySummary = CrossingConnectivityPlanner.Refresh(reason, AssetBuffer, count);
        }

        public static void Reset()
        {
            CrossingNetworkApplier.Revert("engine-reset");
            CrossingConnectivityPlanner.Reset();
            _lastSummary = CrossingApplicationSummary.Empty;
            _lastValidationSummary = CrossingNetworkValidationSummary.Empty;
            _lastConnectivitySummary = CrossingConnectivitySummary.Empty;
            _lastOperationCount = 0;
        }

        public static void ForgetStateForLevelUnload()
        {
            int forgottenSnapshots = CrossingNetworkApplier.ForgetSnapshots("engine-level-unload");
            CrossingConnectivityPlanner.Reset();
            _lastSummary = CrossingApplicationSummary.Empty;
            _lastValidationSummary = CrossingNetworkValidationSummary.Empty;
            _lastConnectivitySummary = CrossingConnectivitySummary.Empty;
            _lastOperationCount = 0;
            if (forgottenSnapshots > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Application unload state forgotten without reverting network flags: snapshots="
                          + forgottenSnapshots);
            }
        }

        public static int ApplyReadyOperations(out int skipped)
        {
            return CrossingNetworkApplier.ApplyReadyOperations(OperationBuffer, _lastOperationCount, out skipped);
        }

        public static int CopyBlockedOperationAssetIdsTo(int[] buffer)
        {
            if (buffer == null)
                return 0;

            int copied = 0;
            for (int i = 0; i < _lastOperationCount; i++)
            {
                CrossingNetworkOperation operation = OperationBuffer[i];
                CrossingNetworkOperationValidation validation = ValidateOperation(operation);
                if (validation.Readiness == CrossingNetworkOperationReadiness.Blocked)
                    AddUniqueAssetId(buffer, ref copied, operation.AssetId);
            }

            return copied;
        }

        public static int RevertAppliedOperations(string reason)
        {
            return CrossingNetworkApplier.Revert(reason);
        }

        private static void AddUniqueAssetId(int[] buffer, ref int count, int assetId)
        {
            if (assetId == 0 || count >= buffer.Length)
                return;

            for (int i = 0; i < count; i++)
            {
                if (buffer[i] == assetId)
                    return;
            }

            buffer[count++] = assetId;
        }

        private static void PlanSurfaceCrossing(CrossingPlacementAsset asset)
        {
            ushort nodeId = asset.Plan.TargetNodeId;
            if (nodeId != 0)
                AddOperation(CrossingNetworkOperationKind.EnableNodeSurfaceCrossing, asset, nodeId);
        }

        private static void PlanSignalCrossing(CrossingPlacementAsset asset)
        {
            ushort nodeId = asset.Plan.TargetNodeId;
            if (nodeId == 0)
                return;

            AddOperation(CrossingNetworkOperationKind.EnablePedestrianSignal, asset, nodeId);
        }

        private static void AddOperation(CrossingNetworkOperationKind kind, CrossingPlacementAsset asset, ushort nodeId)
        {
            if (_lastOperationCount >= OperationBuffer.Length)
                return;

            OperationBuffer[_lastOperationCount++] = new CrossingNetworkOperation(kind, asset.Id, asset.Placement.SegmentId, nodeId, asset.Plan.Center);
        }

        private static void LogPlannedOperations(string reason)
        {
            if (!PedestrianCrossingLog.VerboseDiagnostics)
                return;

            int logCount = Mathf.Min(_lastOperationCount, MaxOperationLogsPerRefresh);
            for (int i = 0; i < logCount; i++)
            {
                Debug.Log("[PedestrianCrossingToolkit] Planned operation: reason=" + reason + " index=" + i + " " + OperationBuffer[i].ToLogString());
            }

            if (_lastOperationCount > logCount)
            {
                Debug.Log("[PedestrianCrossingToolkit] Planned operation log truncated: reason="
                          + reason
                          + " shown=" + logCount
                          + " total=" + _lastOperationCount);
            }
        }

        private static CrossingNetworkValidationSummary ValidatePlannedOperations(string reason)
        {
            int ready = 0;
            int blocked = 0;
            int unsupported = 0;
            int logCount = Mathf.Min(_lastOperationCount, MaxOperationLogsPerRefresh);

            for (int i = 0; i < _lastOperationCount; i++)
            {
                CrossingNetworkOperationValidation validation = ValidateOperation(OperationBuffer[i]);
                switch (validation.Readiness)
                {
                    case CrossingNetworkOperationReadiness.Ready:
                        ready++;
                        break;
                    case CrossingNetworkOperationReadiness.Blocked:
                        blocked++;
                        break;
                    default:
                        unsupported++;
                        break;
                }

                if (PedestrianCrossingLog.VerboseDiagnostics && i < logCount)
                {
                    Debug.Log("[PedestrianCrossingToolkit] Operation validation detail: reason="
                              + reason
                              + " index=" + i
                              + " readiness=" + validation.Readiness
                              + " detail=" + validation.Reason);
                }
            }

            if (PedestrianCrossingLog.VerboseDiagnostics && _lastOperationCount > logCount)
            {
                Debug.Log("[PedestrianCrossingToolkit] Operation validation detail truncated: reason="
                          + reason
                          + " shown=" + logCount
                          + " total=" + _lastOperationCount);
            }

            return new CrossingNetworkValidationSummary(ready, blocked, unsupported);
        }

        public static CrossingNetworkOperationValidation ValidateOperation(CrossingNetworkOperation operation)
        {
            switch (operation.Kind)
            {
                case CrossingNetworkOperationKind.EnableNodeSurfaceCrossing:
                case CrossingNetworkOperationKind.EnablePedestrianSignal:
                case CrossingNetworkOperationKind.SuppressNodeSurfaceCrossings:
                    if (!IsRoadSegmentReady(operation.SegmentId))
                        return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Blocked, "target road segment is missing or invalid");

                    if ((operation.Kind == CrossingNetworkOperationKind.EnableNodeSurfaceCrossing
                         || operation.Kind == CrossingNetworkOperationKind.EnablePedestrianSignal)
                        && !RoadPlacementRules.AllowsSurfaceCrossing(operation.SegmentId))
                    {
                        return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Blocked, "surface crossings are not allowed on this road type");
                    }

                    return IsRoadNodeReady(operation.NodeId)
                        ? new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Ready, "target road node is ready")
                        : new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Blocked, "target road node is missing or invalid");
                case CrossingNetworkOperationKind.CreateMidBlockSignalCrossing:
                    if (!IsRoadSegmentReady(operation.SegmentId))
                        return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Blocked, "target road segment is missing or invalid");

                    if (!RoadPlacementRules.AllowsSurfaceCrossing(operation.SegmentId))
                        return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Blocked, "surface crossings are not allowed on this road type");

                    return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Unsupported, "signal crossing requires an existing road node");
                case CrossingNetworkOperationKind.CreateSubwayLink:
                case CrossingNetworkOperationKind.CreateJunctionSubwayLink:
                    if (!RoadPlacementRules.AllowsSubwayPlacementTarget(operation.SegmentId))
                        return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Blocked, "subways require a normal surface road, surface/elevated train track, or terrain-level/elevated metro track");

                    if (RoadPlacementRules.IsGradeSeparatedPlacementTooCloseToStation(operation.Position))
                        return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Blocked, "subways cannot be placed within 5f of a station");

                    if (RoadPlacementRules.IsRailOrMetroJunctionTooClose(operation.SegmentId, operation.Position))
                        return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Blocked, "subways cannot be placed within 20f of rail or metro points/junctions");

                    return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Unsupported, "requires hidden pedestrian path and compact entrance assets");
                case CrossingNetworkOperationKind.CreatePointToPointSubwayLink:
                    if (!RoadPlacementRules.AllowsManualSubwayPlacementTarget(operation.SegmentId))
                        return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Blocked, "manual subways require a normal surface road");

                    if (RoadPlacementRules.IsGradeSeparatedPlacementTooCloseToStation(operation.Position))
                        return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Blocked, "manual subways cannot be placed within 5f of a station");

                    return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Unsupported, "requires hidden pedestrian path and compact entrance assets");
                case CrossingNetworkOperationKind.CreatePedestrianBridge:
                case CrossingNetworkOperationKind.CreateJunctionPedestrianBridge:
                    if (!RoadPlacementRules.AllowsPedestrianBridgePlacementTarget(operation.SegmentId))
                        return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Blocked, "pedestrian bridges require a normal surface road, surface train track, or terrain-level metro track");

                    if (RoadPlacementRules.IsGradeSeparatedPlacementTooCloseToStation(operation.Position))
                        return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Blocked, "pedestrian bridges cannot be placed within 5f of a station");

                    if (RoadPlacementRules.IsRailOrMetroJunctionTooClose(operation.SegmentId, operation.Position))
                        return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Blocked, "pedestrian bridges cannot be placed within 20f of rail or metro points/junctions");

                    if (RoadPlacementRules.IsPedestrianBridgeTooCloseToMonorailStationWithRoad(operation.Position))
                        return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Blocked, "pedestrian bridges cannot be placed within 10f of a monorail station with road");

                    return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Unsupported, "requires elevated pedestrian path and compact stair/ramp assets");
                default:
                    return new CrossingNetworkOperationValidation(CrossingNetworkOperationReadiness.Unsupported, "operation kind is not implemented");
            }
        }

        private static bool IsRoadSegmentReady(ushort segmentId)
        {
            NetManager netManager = NetManager.instance;
            if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            return (segment.m_flags & NetSegment.Flags.Created) != 0
                   && segment.Info != null
                   && segment.Info.m_netAI is RoadBaseAI;
        }

        private static bool IsGradeSeparatedSegmentReady(ushort segmentId)
        {
            NetManager netManager = NetManager.instance;
            if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            return (segment.m_flags & NetSegment.Flags.Created) != 0
                   && segment.Info != null
                   && RoadPlacementRules.AllowsGradeSeparatedPlacementTarget(segmentId);
        }

        private static bool IsRoadNodeReady(ushort nodeId)
        {
            NetManager netManager = NetManager.instance;
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            return (node.m_flags & NetNode.Flags.Created) != 0
                   && node.Info != null
                   && node.Info.m_netAI is RoadBaseAI;
        }
    }

    public static class CrossingNetworkApplier
    {
        private static readonly Dictionary<ushort, NetSegment.Flags> SegmentSnapshots = new Dictionary<ushort, NetSegment.Flags>();
        private static readonly Dictionary<ushort, NetNode.Flags> NodeSnapshots = new Dictionary<ushort, NetNode.Flags>();

        public static bool HasAppliedChanges
        {
            get { return SegmentSnapshots.Count > 0 || NodeSnapshots.Count > 0; }
        }

        public static int ApplyReadyOperations(CrossingNetworkOperation[] operations, int count, out int skipped)
        {
            Revert("reapply");
            skipped = 0;
            int applied = 0;
            for (int i = 0; i < count; i++)
            {
                CrossingNetworkOperation operation = operations[i];
                CrossingNetworkOperationValidation validation = CrossingApplicationEngine.ValidateOperation(operation);
                if (validation.Readiness != CrossingNetworkOperationReadiness.Ready)
                {
                    skipped++;
                    continue;
                }

                if (ApplyOperation(operation))
                    applied++;
                else
                    skipped++;
            }

            Debug.Log("[PedestrianCrossingToolkit] Applied ready crossing operations: applied=" + applied + " skipped=" + skipped);
            return applied;
        }

        public static int Revert(string reason)
        {
            if (!HasAppliedChanges)
                return 0;

            NetManager netManager = NetManager.instance;
            int reverted = 0;
            foreach (KeyValuePair<ushort, NetSegment.Flags> snapshot in SegmentSnapshots)
            {
                ushort segmentId = snapshot.Key;
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                    continue;

                segment.m_flags = snapshot.Value;
                RestoreSegmentPedestrianCrossingMarkers(segmentId, snapshot.Value);
                netManager.UpdateSegmentFlags(segmentId);
                netManager.UpdateSegmentRenderer(segmentId, true);
                reverted++;
            }

            foreach (KeyValuePair<ushort, NetNode.Flags> snapshot in NodeSnapshots)
            {
                ushort nodeId = snapshot.Key;
                if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                    continue;

                ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
                if ((node.m_flags & NetNode.Flags.Created) == 0)
                    continue;

                node.m_flags = snapshot.Value;
                netManager.UpdateNodeFlags(nodeId);
                netManager.UpdateNodeRenderer(nodeId, true);
                reverted++;
            }

            SegmentSnapshots.Clear();
            NodeSnapshots.Clear();
            Debug.Log("[PedestrianCrossingToolkit] Reverted applied crossing operation targets: reason=" + reason + " reverted=" + reverted);
            return reverted;
        }

        public static int ForgetSnapshots(string reason)
        {
            int tracked = SegmentSnapshots.Count + NodeSnapshots.Count;
            SegmentSnapshots.Clear();
            NodeSnapshots.Clear();
            if (tracked > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Applied crossing operation snapshots forgotten without network revert: reason="
                          + reason
                          + " snapshots="
                          + tracked);
            }

            return tracked;
        }

        private static bool ApplyOperation(CrossingNetworkOperation operation)
        {
            switch (operation.Kind)
            {
                case CrossingNetworkOperationKind.EnableNodeSurfaceCrossing:
                    return SetNodeSurfaceCrossings(operation.NodeId, true);
                case CrossingNetworkOperationKind.EnablePedestrianSignal:
                    return SetNodeFlag(operation.NodeId, NetNode.Flags.TrafficLights, true);
                case CrossingNetworkOperationKind.SuppressNodeSurfaceCrossings:
                    return SetNodeSurfaceCrossings(operation.NodeId, false);
                default:
                    return false;
            }
        }

        private static bool SetNodeFlag(ushort nodeId, NetNode.Flags flag, bool enabled)
        {
            NetManager netManager = NetManager.instance;
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return false;

            SnapshotNode(nodeId, node.m_flags);
            if (enabled)
                node.m_flags |= flag;
            else
                node.m_flags &= ~flag;

            netManager.UpdateNodeFlags(nodeId);
            netManager.UpdateNodeRenderer(nodeId, true);
            return true;
        }

        private static bool SetNodeSurfaceCrossings(ushort nodeId, bool enabled)
        {
            NetManager netManager = NetManager.instance;
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return false;

            bool changed = false;
            int segmentCount = node.CountSegments();
            for (int i = 0; i < segmentCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                    continue;

                NetSegment.Flags crossingFlag;
                bool startNode;
                if (segment.m_startNode == nodeId)
                {
                    crossingFlag = NetSegment.Flags.CrossingStart;
                    startNode = true;
                }
                else if (segment.m_endNode == nodeId)
                {
                    crossingFlag = NetSegment.Flags.CrossingEnd;
                    startNode = false;
                }
                else
                    continue;

                SnapshotSegment(segmentId, segment.m_flags);
                if (enabled)
                    segment.m_flags |= crossingFlag;
                else
                    segment.m_flags &= ~crossingFlag;

                TrafficManagerPedestrianCrossingIntegration.SetPedestrianCrossingAllowed(segmentId, startNode, enabled);
                netManager.UpdateSegmentFlags(segmentId);
                netManager.UpdateSegmentRenderer(segmentId, true);
                changed = true;
            }

            return changed;
        }

        private static void SnapshotSegment(ushort segmentId, NetSegment.Flags flags)
        {
            if (!SegmentSnapshots.ContainsKey(segmentId))
                SegmentSnapshots.Add(segmentId, flags);
        }

        private static void SnapshotNode(ushort nodeId, NetNode.Flags flags)
        {
            if (!NodeSnapshots.ContainsKey(nodeId))
                NodeSnapshots.Add(nodeId, flags);
        }

        private static void RestoreSegmentPedestrianCrossingMarkers(ushort segmentId, NetSegment.Flags flags)
        {
            bool startAllowed = (flags & NetSegment.Flags.CrossingStart) != 0;
            bool endAllowed = (flags & NetSegment.Flags.CrossingEnd) != 0;
            TrafficManagerPedestrianCrossingIntegration.SetPedestrianCrossingAllowed(segmentId, true, startAllowed);
            TrafficManagerPedestrianCrossingIntegration.SetPedestrianCrossingAllowed(segmentId, false, endAllowed);
        }
    }
}
