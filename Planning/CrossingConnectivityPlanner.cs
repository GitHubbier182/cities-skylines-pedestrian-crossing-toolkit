using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public static class CrossingConnectivityPlanner
    {
        private const int MaxCandidateLogsPerRefresh = 16;
        private const int MaxLinkLogsPerRefresh = 16;
        private static readonly CrossingConnectivityCandidate[] CandidateBuffer = new CrossingConnectivityCandidate[2048];
        private static readonly CrossingConnectivityLink[] LinkBuffer = new CrossingConnectivityLink[1024];
        private static readonly int[] AssetsWithoutLinks = new int[1024];
        private static int _candidateCount;
        private static int _linkCount;
        private static int _assetWithoutLinkCount;

        public static CrossingConnectivitySummary Refresh(string reason, CrossingPlacementAsset[] assets, int count)
        {
            _candidateCount = 0;
            _linkCount = 0;
            _assetWithoutLinkCount = 0;
            int assetsWithPedestrianLanes = 0;
            int assetsMissingPedestrianLanes = 0;
            int assetsWithoutLinks = 0;
            int junctionSegmentsScanned = 0;
            NetManager netManager = NetManager.instance;

            for (int i = 0; i < count; i++)
            {
                CrossingPlacementAsset asset = assets[i];
                if (!asset.Plan.IsValid)
                    continue;

                int before = _candidateCount;
                int linksBefore = _linkCount;
                if (GradeSeparatedPlacementGeometryResolver.IsGradeSeparated(asset.Plan.ApplicationKind))
                {
                    if (TryAddGradeSeparatedLink(asset))
                        assetsWithPedestrianLanes++;
                    else
                        assetsMissingPedestrianLanes++;

                    AddSegmentCandidates(netManager, asset, asset.Placement.SegmentId, 0);
                }
                else if (asset.Plan.TargetNodeId != 0)
                {
                    junctionSegmentsScanned += AddJunctionCandidates(netManager, asset);
                    if (_candidateCount > before)
                        assetsWithPedestrianLanes++;
                    else
                        assetsMissingPedestrianLanes++;

                    AddLinksForAsset(asset, before, _candidateCount - before);
                }
                else
                {
                    AddSegmentCandidates(netManager, asset, asset.Placement.SegmentId, 0);
                    if (_candidateCount > before)
                        assetsWithPedestrianLanes++;
                    else
                        assetsMissingPedestrianLanes++;

                    AddLinksForAsset(asset, before, _candidateCount - before);
                }

                if (_linkCount == linksBefore)
                {
                    assetsWithoutLinks++;
                    AddAssetWithoutLinks(asset.Id);
                }
            }

            CrossingConnectivitySummary summary = new CrossingConnectivitySummary(count, assetsWithPedestrianLanes, assetsMissingPedestrianLanes, _candidateCount, _linkCount, 0, assetsWithoutLinks, junctionSegmentsScanned);
            Debug.Log("[PedestrianCrossingToolkit] Connectivity planning: reason=" + reason + " " + summary.ToLogString());
            LogCandidates(reason);
            LogLinks(reason);
            CrossingLandingConnectorPlanner.Refresh(reason, LinkBuffer, _linkCount, CandidateBuffer, _candidateCount);
            CrossingPathWorkOrderPlanner.Refresh(reason, LinkBuffer, _linkCount);
            CrossingConstructionPlanner.Refresh(reason, CrossingPathWorkOrderPlanner.LastSummary, CrossingLandingConnectorPlanner.LastSummary, CrossingLandingConnectorPlanner.AccessAssetCount);
            return summary;
        }

        public static void Reset()
        {
            _candidateCount = 0;
            _linkCount = 0;
            CrossingPathWorkOrderPlanner.Reset();
            CrossingLandingConnectorPlanner.Reset();
            CrossingConstructionPlanner.Reset();
        }

        public static int CopyLinksTo(CrossingConnectivityLink[] buffer)
        {
            int count = Mathf.Min(buffer.Length, _linkCount);
            for (int i = 0; i < count; i++)
                buffer[i] = LinkBuffer[i];

            return count;
        }

        public static int CopyAssetsWithoutLinksTo(int[] buffer)
        {
            int count = Mathf.Min(buffer.Length, _assetWithoutLinkCount);
            for (int i = 0; i < count; i++)
                buffer[i] = AssetsWithoutLinks[i];

            return count;
        }

        public static bool HasLinkForAsset(int assetId)
        {
            if (assetId <= 0)
                return false;

            for (int i = 0; i < _linkCount; i++)
            {
                if (LinkBuffer[i].AssetId == assetId)
                    return true;
            }

            return false;
        }

        private static void AddAssetWithoutLinks(int assetId)
        {
            if (assetId <= 0 || _assetWithoutLinkCount >= AssetsWithoutLinks.Length)
                return;

            AssetsWithoutLinks[_assetWithoutLinkCount++] = assetId;
        }

        private static int AddJunctionCandidates(NetManager netManager, CrossingPlacementAsset asset)
        {
            ushort nodeId = asset.Plan.TargetNodeId;
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return 0;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return 0;

            int scanned = 0;
            int segmentCount = node.CountSegments();
            for (int i = 0; i < segmentCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0)
                    continue;

                scanned++;
                AddSegmentCandidates(netManager, asset, segmentId, nodeId);
            }

            return scanned;
        }

        private static bool TryAddGradeSeparatedLink(CrossingPlacementAsset asset)
        {
            if (_linkCount >= LinkBuffer.Length || !GradeSeparatedPlacementGeometryResolver.IsGradeSeparated(asset.Plan.ApplicationKind))
                return false;

            GradeSeparatedPlacementGeometry geometry;
            if (!GradeSeparatedPlacementGeometryResolver.TryBuild(asset, out geometry))
                return false;

            LinkBuffer[_linkCount++] = new CrossingConnectivityLink(
                asset.Id,
                asset.Placement.SegmentId,
                geometry.LinkKind,
                0,
                0,
                geometry.FirstDeckPosition,
                geometry.SecondDeckPosition,
                geometry.UsesLaneTargets,
                asset.Plan.FlipBridgeAccess);
            return true;
        }

        private static void AddSegmentCandidates(NetManager netManager, CrossingPlacementAsset asset, ushort segmentId, ushort nodeId)
        {
            if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null || !(segment.Info.m_netAI is RoadBaseAI))
                return;

            NetInfo.Lane[] lanes = segment.Info.m_lanes;
            if (lanes == null || lanes.Length == 0)
                return;

            uint laneId = segment.m_lanes;
            for (int i = 0; i < lanes.Length; i++)
            {
                NetInfo.Lane lane = lanes[i];
                uint currentLaneId = laneId;
                if (currentLaneId != 0 && currentLaneId < netManager.m_lanes.m_size)
                    laneId = netManager.m_lanes.m_buffer[currentLaneId].m_nextLane;
                else
                    laneId = 0;

                if (lane == null || (lane.m_laneType & NetInfo.LaneType.Pedestrian) == 0)
                    continue;

                if (currentLaneId == 0 || currentLaneId >= netManager.m_lanes.m_size)
                    continue;

                AddCandidate(netManager, asset, segmentId, nodeId, currentLaneId, i, lane.m_position);
            }
        }

        private static void AddCandidate(NetManager netManager, CrossingPlacementAsset asset, ushort segmentId, ushort nodeId, uint laneId, int laneIndex, float lanePosition)
        {
            if (_candidateCount >= CandidateBuffer.Length)
                return;

            Vector3 worldPosition = GetCandidateWorldPosition(netManager, asset, segmentId, nodeId, lanePosition);
            CandidateBuffer[_candidateCount++] = new CrossingConnectivityCandidate(asset.Id, segmentId, nodeId, laneId, laneIndex, lanePosition, worldPosition);
        }

        private static Vector3 GetCandidateWorldPosition(NetManager netManager, CrossingPlacementAsset asset, ushort segmentId, ushort nodeId, float lanePosition)
        {
            Vector3 center = asset.Plan.Center;
            Vector3 crossingDirection = asset.Plan.CrossingDirection;

            if (nodeId != 0
                && nodeId < netManager.m_nodes.m_size
                && segmentId != 0
                && segmentId < netManager.m_segments.m_size)
            {
                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                ushort otherNodeId = segment.m_startNode == nodeId ? segment.m_endNode : segment.m_startNode;
                if (otherNodeId != 0 && otherNodeId < netManager.m_nodes.m_size)
                {
                    Vector3 nodePosition = netManager.m_nodes.m_buffer[nodeId].m_position;
                    Vector3 awayDirection = netManager.m_nodes.m_buffer[otherNodeId].m_position - nodePosition;
                    awayDirection.y = 0f;
                    if (awayDirection.sqrMagnitude > 0.01f)
                    {
                        awayDirection.Normalize();
                        crossingDirection = new Vector3(-awayDirection.z, 0f, awayDirection.x);
                        float nodeSetback = asset.Plan.ApplicationKind == CrossingApplicationKind.SignalizedSurfaceCrossing
                            ? 0.5f
                            : Mathf.Clamp(asset.Plan.Width * 0.35f, 8f, 18f);
                        center = nodePosition + awayDirection * nodeSetback;
                    }
                }
            }

            return center + crossingDirection * lanePosition;
        }

        private static void AddLinksForAsset(CrossingPlacementAsset asset, int startIndex, int candidateCount)
        {
            if (candidateCount < 2)
                return;

            if (asset.Plan.TargetNodeId == 0)
            {
                AddExtremeLaneLink(asset, asset.Placement.SegmentId, startIndex, candidateCount, GetLinkKind(asset.Plan.ApplicationKind, false));
                return;
            }

            int endIndex = startIndex + candidateCount;
            for (int i = startIndex; i < endIndex; i++)
            {
                ushort segmentId = CandidateBuffer[i].SegmentId;
                if (segmentId == 0)
                    continue;

                bool alreadyHandled = false;
                for (int j = startIndex; j < i; j++)
                {
                    if (CandidateBuffer[j].SegmentId == segmentId)
                    {
                        alreadyHandled = true;
                        break;
                    }
                }

                if (alreadyHandled)
                    continue;

                AddExtremeLaneLinkForSegment(asset, segmentId, startIndex, candidateCount, GetLinkKind(asset.Plan.ApplicationKind, true));
            }
        }

        private static CrossingConnectivityLinkKind GetLinkKind(CrossingApplicationKind applicationKind, bool junction)
        {
            switch (applicationKind)
            {
                case CrossingApplicationKind.SignalizedSurfaceCrossing:
                    return CrossingConnectivityLinkKind.SignalizedSurfaceSpan;
                case CrossingApplicationKind.SubwayLink:
                case CrossingApplicationKind.SubwayPointToPoint:
                    return CrossingConnectivityLinkKind.SubwaySpan;
                case CrossingApplicationKind.SubwayJunctionSuppressSurface:
                    return CrossingConnectivityLinkKind.JunctionSubwayApproach;
                case CrossingApplicationKind.PedestrianBridge:
                    return junction ? CrossingConnectivityLinkKind.JunctionBridgeApproach : CrossingConnectivityLinkKind.PedestrianBridgeSpan;
                default:
                    return CrossingConnectivityLinkKind.SurfaceSpan;
            }
        }

        private static void AddExtremeLaneLinkForSegment(CrossingPlacementAsset asset, ushort segmentId, int startIndex, int candidateCount, CrossingConnectivityLinkKind kind)
        {
            int filteredCount = 0;
            int endIndex = startIndex + candidateCount;
            for (int i = startIndex; i < endIndex; i++)
            {
                if (CandidateBuffer[i].SegmentId != segmentId)
                    continue;

                filteredCount++;
            }

            if (filteredCount < 2)
                return;

            AddExtremeLaneLink(asset, segmentId, startIndex, candidateCount, kind);
        }

        private static void AddExtremeLaneLink(CrossingPlacementAsset asset, ushort segmentId, int startIndex, int candidateCount, CrossingConnectivityLinkKind kind)
        {
            if (_linkCount >= LinkBuffer.Length || candidateCount < 2)
                return;

            int endIndex = startIndex + candidateCount;
            int minIndex = -1;
            int maxIndex = -1;
            float minPosition = 0f;
            float maxPosition = 0f;
            for (int i = startIndex; i < endIndex; i++)
            {
                CrossingConnectivityCandidate candidate = CandidateBuffer[i];
                if (candidate.SegmentId != segmentId)
                    continue;

                if (minIndex < 0 || candidate.LanePosition < minPosition)
                {
                    minIndex = i;
                    minPosition = candidate.LanePosition;
                }

                if (maxIndex < 0 || candidate.LanePosition > maxPosition)
                {
                    maxIndex = i;
                    maxPosition = candidate.LanePosition;
                }
            }

            if (minIndex < 0 || maxIndex < 0 || minIndex == maxIndex)
                return;

            CrossingConnectivityCandidate first = CandidateBuffer[minIndex];
            CrossingConnectivityCandidate second = CandidateBuffer[maxIndex];
            LinkBuffer[_linkCount++] = new CrossingConnectivityLink(asset.Id, segmentId, kind, first.LaneId, second.LaneId, first.WorldPosition, second.WorldPosition, true, asset.Plan.FlipBridgeAccess);
        }

        private static void LogCandidates(string reason)
        {
            if (!PedestrianCrossingLog.VerboseDiagnostics)
                return;

            int logCount = Mathf.Min(_candidateCount, MaxCandidateLogsPerRefresh);
            for (int i = 0; i < logCount; i++)
            {
                Debug.Log("[PedestrianCrossingToolkit] Connectivity candidate: reason=" + reason + " index=" + i + " " + CandidateBuffer[i].ToLogString());
            }

            if (_candidateCount > logCount)
            {
                Debug.Log("[PedestrianCrossingToolkit] Connectivity candidate log truncated: reason="
                          + reason
                          + " shown=" + logCount
                          + " total=" + _candidateCount);
            }
        }

        private static void LogLinks(string reason)
        {
            if (!PedestrianCrossingLog.VerboseDiagnostics)
                return;

            int logCount = Mathf.Min(_linkCount, MaxLinkLogsPerRefresh);
            for (int i = 0; i < logCount; i++)
            {
                Debug.Log("[PedestrianCrossingToolkit] Connectivity link: reason=" + reason + " index=" + i + " " + LinkBuffer[i].ToLogString());
            }

            if (_linkCount > logCount)
            {
                Debug.Log("[PedestrianCrossingToolkit] Connectivity link log truncated: reason="
                          + reason
                          + " shown=" + logCount
                          + " total=" + _linkCount);
            }
        }
    }
}
