using System;
using System.Collections.Generic;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public static class CrossingPlacementTool
    {
        public static void Reset()
        {
        }

        public static CrossingPlacementResult PreviewMidBlockCrossing(ushort segmentId, float segmentPosition, Vector3 worldPosition, bool nearNode)
        {
            return CrossingPlacementPolicy.Evaluate(new CrossingPlacementRecord(PedestrianToolMode.MidBlockCrossing, segmentId, segmentPosition, worldPosition, true, string.Empty, nearNode)).ToPlacementResult();
        }
    }

    public static class SignalCrossingTool
    {
        public static void Reset()
        {
        }

        public static CrossingPlacementResult PreviewControlledCrossing(ushort segmentId, float segmentPosition, Vector3 worldPosition, bool nearNode)
        {
            return CrossingPlacementPolicy.Evaluate(new CrossingPlacementRecord(PedestrianToolMode.SignalCrossing, segmentId, segmentPosition, worldPosition, true, string.Empty, nearNode)).ToPlacementResult();
        }
    }

    public static class SubwayLinkTool
    {
        public static void Reset()
        {
        }

        public static CrossingPlacementResult PreviewSubwayLink(ushort segmentId, float segmentPosition, Vector3 worldPosition, bool nearNode)
        {
            return CrossingPlacementPolicy.Evaluate(new CrossingPlacementRecord(PedestrianToolMode.SubwayLink, segmentId, segmentPosition, worldPosition, true, string.Empty, nearNode)).ToPlacementResult();
        }
    }

    public static class PedestrianBridgeTool
    {
        public static void Reset()
        {
        }

        public static CrossingPlacementResult PreviewPedestrianBridge(ushort segmentId, float segmentPosition, Vector3 worldPosition, bool nearNode)
        {
            return CrossingPlacementPolicy.Evaluate(new CrossingPlacementRecord(PedestrianToolMode.PedestrianBridge, segmentId, segmentPosition, worldPosition, true, string.Empty, nearNode)).ToPlacementResult();
        }
    }

    public static class RoadPlacementRules
    {
        public const float SurfaceJunctionBlockSegmentPosition = 0.08f;
        public const float MonorailStationWithRoadBridgeClearance = 10f;
        public const float GradeSeparatedStationClearance = 5f;
        public const float GradeSeparatedRailJunctionClearance = 20f;
        private const float SurfaceJunctionBlockDistance = 18f;
        public const float SurfaceCrossingPostVanillaBuffer = 10f;
        public const float GradeSeparatedJunctionCenterBlockDistance = 8f;
        private const float GradeSeparatedVanillaCrossingClearance = 2f;
        private const int NetNodeSegmentSlotCount = 8;
        private const float VanillaCrossingCacheNetworkPollSeconds = 0.75f;
        private const float VanillaCrossingCacheNetworkQuietSeconds = 3f;
        private const int VanillaCrossingCacheRefreshSegmentBatchSize = 512;
        private static readonly List<CachedVanillaCrossingPoint> VanillaCrossingCache = new List<CachedVanillaCrossingPoint>();
        private static readonly List<CachedVanillaCrossingPoint> VanillaCrossingCacheRefreshBuffer = new List<CachedVanillaCrossingPoint>();
        private static VanillaCrossingNetworkSignature _vanillaCrossingNetworkSignature;
        private static bool _hasVanillaCrossingNetworkSignature;
        private static bool _vanillaCrossingCacheReady;
        private static bool _vanillaCrossingCacheDirty = true;
        private static float _vanillaCrossingNetworkPollTimer;
        private static float _vanillaCrossingNetworkQuietTimer;
        private static bool _vanillaCrossingCacheRefreshInProgress;
        private static ushort _vanillaCrossingCacheRefreshNextSegmentId;
        private static string _vanillaCrossingCacheRefreshReason = string.Empty;
        private static readonly List<ushort> MonorailStationWithRoadSegmentCache = new List<ushort>();
        private static bool _monorailStationWithRoadCacheReady;
        private static int _monorailStationWithRoadCachedSegmentCount;
        private static readonly List<ushort> StationSegmentCache = new List<ushort>();
        private static bool _stationSegmentCacheReady;
        private static int _stationSegmentCachedSegmentCount;

        private struct CachedVanillaCrossingPoint
        {
            public readonly ushort OwnerSegmentId;
            public readonly VanillaCrossingPoint CrossingPoint;

            public CachedVanillaCrossingPoint(ushort ownerSegmentId, VanillaCrossingPoint crossingPoint)
            {
                OwnerSegmentId = ownerSegmentId;
                CrossingPoint = crossingPoint;
            }
        }

        private struct VanillaCrossingNetworkSignature
        {
            public readonly int CreatedSegments;
            public readonly int CreatedNodes;
            public readonly int Hash;

            public VanillaCrossingNetworkSignature(int createdSegments, int createdNodes, int hash)
            {
                CreatedSegments = createdSegments;
                CreatedNodes = createdNodes;
                Hash = hash;
            }

            public bool Matches(VanillaCrossingNetworkSignature other)
            {
                return CreatedSegments == other.CreatedSegments
                       && CreatedNodes == other.CreatedNodes
                       && Hash == other.Hash;
            }
        }

        public struct JunctionExclusionZone
        {
            public readonly Vector3 Center;
            public readonly Vector3 XAxis;
            public readonly Vector3 ZAxis;
            public readonly float HalfX;
            public readonly float HalfZ;
            public readonly Vector3 FirstCorner;
            public readonly Vector3 SecondCorner;
            public readonly Vector3 ThirdCorner;
            public readonly Vector3 FourthCorner;
            public readonly Vector3[] PolygonPoints;
            public readonly Vector3[][] SurfacePolygons;

            public int PolygonPointCount
            {
                get { return PolygonPoints == null ? 0 : PolygonPoints.Length; }
            }

            public int SurfacePolygonCount
            {
                get { return SurfacePolygons == null ? 0 : SurfacePolygons.Length; }
            }

            public JunctionExclusionZone(Vector3 center, Vector3 xAxis, Vector3 zAxis, float halfX, float halfZ)
                : this(center, xAxis, zAxis, halfX, halfZ, null, null)
            {
            }

            public JunctionExclusionZone(Vector3 center, Vector3 xAxis, Vector3 zAxis, float halfX, float halfZ, Vector3[] polygonPoints)
                : this(center, xAxis, zAxis, halfX, halfZ, polygonPoints, null)
            {
            }

            public JunctionExclusionZone(Vector3 center, Vector3 xAxis, Vector3 zAxis, float halfX, float halfZ, Vector3[] polygonPoints, Vector3[][] surfacePolygons)
            {
                Center = center;
                XAxis = xAxis;
                ZAxis = zAxis;
                HalfX = halfX;
                HalfZ = halfZ;
                PolygonPoints = polygonPoints;
                SurfacePolygons = surfacePolygons;

                if (polygonPoints != null && polygonPoints.Length >= 4)
                {
                    FirstCorner = polygonPoints[0];
                    SecondCorner = polygonPoints[1];
                    ThirdCorner = polygonPoints[2];
                    FourthCorner = polygonPoints[3];
                }
                else
                {
                    Vector3 xOffset = xAxis * halfX;
                    Vector3 zOffset = zAxis * halfZ;
                    FirstCorner = center - xOffset - zOffset;
                    SecondCorner = center + xOffset - zOffset;
                    ThirdCorner = center + xOffset + zOffset;
                    FourthCorner = center - xOffset + zOffset;
                }
            }

            public bool Contains(Vector3 worldPosition)
            {
                if (SurfacePolygons != null)
                {
                    for (int i = 0; i < SurfacePolygons.Length; i++)
                    {
                        Vector3[] polygon = SurfacePolygons[i];
                        if (polygon != null && polygon.Length >= 3 && ContainsPolygonPoint(polygon, worldPosition))
                            return true;
                    }

                    return false;
                }

                if (PolygonPoints != null && PolygonPoints.Length >= 3)
                    return ContainsPolygonPoint(PolygonPoints, worldPosition);

                Vector3 offset = worldPosition - Center;
                offset.y = 0f;
                float x = Vector3.Dot(offset, XAxis);
                float z = Vector3.Dot(offset, ZAxis);
                return Mathf.Abs(x) <= HalfX && Mathf.Abs(z) <= HalfZ;
            }

            private static bool ContainsPolygonPoint(Vector3[] polygon, Vector3 worldPosition)
            {
                bool hasPositive = false;
                bool hasNegative = false;
                Vector2 point = new Vector2(worldPosition.x, worldPosition.z);
                for (int i = 0; i < polygon.Length; i++)
                {
                    Vector3 current = polygon[i];
                    Vector3 next = polygon[(i + 1) % polygon.Length];
                    Vector2 a = new Vector2(current.x, current.z);
                    Vector2 b = new Vector2(next.x, next.z);
                    float cross = ((b.x - a.x) * (point.y - a.y)) - ((b.y - a.y) * (point.x - a.x));
                    if (cross > 0.05f)
                        hasPositive = true;
                    else if (cross < -0.05f)
                        hasNegative = true;

                    if (hasPositive && hasNegative)
                        return false;
                }

                return true;
            }
        }

        public struct VanillaCrossingPoint
        {
            public readonly ushort NodeId;
            public readonly bool IsEndSegment;
            public readonly Vector3 WorldPosition;
            public readonly float SegmentPosition;
            public readonly float DistanceFromNode;

            public VanillaCrossingPoint(ushort nodeId, bool isEndSegment, Vector3 worldPosition, float segmentPosition, float distanceFromNode)
            {
                NodeId = nodeId;
                IsEndSegment = isEndSegment;
                WorldPosition = worldPosition;
                SegmentPosition = segmentPosition;
                DistanceFromNode = distanceFromNode;
            }
        }

        public static void ForceRefreshVanillaCrossingCache(string reason)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null)
            {
                ResetVanillaCrossingCache();
                return;
            }

            _vanillaCrossingNetworkSignature = BuildVanillaCrossingNetworkSignature(netManager);
            _hasVanillaCrossingNetworkSignature = true;
            RefreshVanillaCrossingCache(netManager, reason);
        }

        public static void ResetVanillaCrossingCache()
        {
            VanillaCrossingCache.Clear();
            VanillaCrossingCacheRefreshBuffer.Clear();
            _hasVanillaCrossingNetworkSignature = false;
            _vanillaCrossingCacheReady = false;
            _vanillaCrossingCacheDirty = true;
            _vanillaCrossingNetworkPollTimer = 0f;
            _vanillaCrossingNetworkQuietTimer = 0f;
            _vanillaCrossingCacheRefreshInProgress = false;
            _vanillaCrossingCacheRefreshNextSegmentId = 0;
            _vanillaCrossingCacheRefreshReason = string.Empty;
        }

        public static void UpdateVanillaCrossingCache(float realTimeDelta)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null)
            {
                ResetVanillaCrossingCache();
                return;
            }

            if (_vanillaCrossingCacheRefreshInProgress)
                ProcessVanillaCrossingCacheRefresh(netManager);

            _vanillaCrossingNetworkPollTimer += Mathf.Max(0f, realTimeDelta);
            if (_vanillaCrossingNetworkPollTimer < VanillaCrossingCacheNetworkPollSeconds)
                return;

            float elapsed = _vanillaCrossingNetworkPollTimer;
            _vanillaCrossingNetworkPollTimer = 0f;

            VanillaCrossingNetworkSignature signature = BuildVanillaCrossingNetworkSignature(netManager);
            if (!_hasVanillaCrossingNetworkSignature)
            {
                _vanillaCrossingNetworkSignature = signature;
                _hasVanillaCrossingNetworkSignature = true;
                RefreshVanillaCrossingCache(netManager, "initial");
                return;
            }

            if (!_vanillaCrossingNetworkSignature.Matches(signature))
            {
                _vanillaCrossingNetworkSignature = signature;
                _vanillaCrossingCacheDirty = true;
                _vanillaCrossingNetworkQuietTimer = 0f;
                _vanillaCrossingCacheRefreshInProgress = false;
                VanillaCrossingCacheRefreshBuffer.Clear();
                return;
            }

            if (!_vanillaCrossingCacheDirty || _vanillaCrossingCacheRefreshInProgress)
                return;

            _vanillaCrossingNetworkQuietTimer += elapsed;
            if (_vanillaCrossingNetworkQuietTimer >= VanillaCrossingCacheNetworkQuietSeconds)
                StartVanillaCrossingCacheRefresh("network-quiet");
        }

        public static bool AllowsSurfaceCrossing(ushort segmentId)
        {
            return AllowsSurfacePlacementTarget(segmentId);
        }

        public static bool AllowsSurfacePlacementTarget(ushort segmentId)
        {
            NetManager netManager = NetManager.instance;
            if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null)
                return false;

            return IsNormalSurfaceRoad(segment.Info) && !IsHighwayRoad(segment.Info);
        }

        public static bool AllowsPedestrianBridgePlacement(ushort segmentId, Vector3 worldPosition)
        {
            return AllowsPedestrianBridgePlacementTarget(segmentId)
                   && !IsGradeSeparatedPlacementTooCloseToStation(worldPosition)
                   && !IsRailOrMetroJunctionTooClose(segmentId, worldPosition)
                   && !IsPedestrianBridgeTooCloseToMonorailStationWithRoad(worldPosition);
        }

        public static bool AllowsPedestrianBridgePlacementTarget(ushort segmentId)
        {
            return AllowsPlacementTarget(segmentId, IsPedestrianBridgePlacementTarget);
        }

        public static bool AllowsSubwayPlacement(ushort segmentId, Vector3 worldPosition)
        {
            return AllowsSubwayPlacementTarget(segmentId)
                   && !IsGradeSeparatedPlacementTooCloseToStation(worldPosition)
                   && !IsRailOrMetroJunctionTooClose(segmentId, worldPosition);
        }

        public static bool AllowsSubwayPlacementTarget(ushort segmentId)
        {
            return AllowsPlacementTarget(segmentId, IsSubwayPlacementTarget);
        }

        public static bool AllowsManualSubwayPlacement(ushort segmentId, Vector3 worldPosition)
        {
            return AllowsManualSubwayPlacementTarget(segmentId)
                   && !IsGradeSeparatedPlacementTooCloseToStation(worldPosition);
        }

        public static bool AllowsManualSubwayPlacementTarget(ushort segmentId)
        {
            return AllowsPlacementTarget(segmentId, IsGradeSeparatedRoadTarget);
        }

        public static bool AllowsGradeSeparatedPlacementTarget(ushort segmentId)
        {
            return AllowsPlacementTarget(segmentId, IsAnyGradeSeparatedPlacementTarget);
        }

        private static bool AllowsPlacementTarget(ushort segmentId, Func<NetInfo, bool> predicate)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null)
                return false;

            return predicate(segment.Info);
        }

        public static bool IsRoadGradeSeparatedPlacementTarget(ushort segmentId)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            return (segment.m_flags & NetSegment.Flags.Created) != 0
                   && IsGradeSeparatedRoadTarget(segment.Info);
        }

        public static bool IsNonRoadGradeSeparatedPlacementTarget(ushort segmentId)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            return (segment.m_flags & NetSegment.Flags.Created) != 0
                   && IsGradeSeparatedNonRoadTarget(segment.Info);
        }

        public static bool IsGradeSeparatedPlacementTooCloseToStation(Vector3 worldPosition)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || netManager.m_segments == null || netManager.m_segments.m_buffer == null)
                return false;

            RefreshStationSegmentCache(netManager);
            if (StationSegmentCache.Count == 0)
                return false;

            Vector3 flatPosition = worldPosition;
            flatPosition.y = 0f;
            float clearanceSqr = GradeSeparatedStationClearance * GradeSeparatedStationClearance;
            for (int i = 0; i < StationSegmentCache.Count; i++)
            {
                ushort segmentId = StationSegmentCache[i];
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !IsStationNet(segment.Info))
                    continue;

                Vector3 closest = segment.GetClosestPosition(worldPosition);
                closest.y = 0f;
                if ((flatPosition - closest).sqrMagnitude <= clearanceSqr)
                    return true;
            }

            return false;
        }

        private static void RefreshStationSegmentCache(NetManager netManager)
        {
            if (_stationSegmentCacheReady
                && _stationSegmentCachedSegmentCount == netManager.m_segmentCount)
            {
                return;
            }

            StationSegmentCache.Clear();
            for (ushort segmentId = 1; segmentId < netManager.m_segments.m_size; segmentId++)
            {
                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !IsStationNet(segment.Info))
                    continue;

                StationSegmentCache.Add(segmentId);
            }

            _stationSegmentCachedSegmentCount = netManager.m_segmentCount;
            _stationSegmentCacheReady = true;
        }

        public static bool IsRailOrMetroJunctionTooClose(ushort segmentId, Vector3 worldPosition)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0
                || segment.Info == null
                || !IsRailOrMetroTrack(segment.Info))
            {
                return false;
            }

            float clearanceSqr = GradeSeparatedRailJunctionClearance * GradeSeparatedRailJunctionClearance;
            return IsRailOrMetroJunctionNodeWithin(netManager, segment.m_startNode, worldPosition, clearanceSqr)
                   || IsRailOrMetroJunctionNodeWithin(netManager, segment.m_endNode, worldPosition, clearanceSqr);
        }

        private static bool IsRailOrMetroJunctionNodeWithin(NetManager netManager, ushort nodeId, Vector3 worldPosition, float clearanceSqr)
        {
            if (!IsRailOrMetroJunctionNode(netManager, nodeId))
                return false;

            Vector3 nodePosition = netManager.m_nodes.m_buffer[nodeId].m_position;
            nodePosition.y = 0f;
            Vector3 flatPosition = worldPosition;
            flatPosition.y = 0f;
            return (flatPosition - nodePosition).sqrMagnitude <= clearanceSqr;
        }

        private static bool IsRailOrMetroJunctionNode(NetManager netManager, ushort nodeId)
        {
            if (netManager == null || nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return false;

            int trackCount = CountRailOrMetroTrackSegments(netManager, nodeId);
            if (trackCount < 2)
                return false;

            return (node.m_flags & NetNode.Flags.Junction) != 0 || trackCount >= 3;
        }

        private static int CountRailOrMetroTrackSegments(NetManager netManager, ushort nodeId)
        {
            if (netManager == null || nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return 0;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            int trackCount = 0;
            for (int i = 0; i < NetNodeSegmentSlotCount; i++)
            {
                ushort attachedSegmentId = node.GetSegment(i);
                if (attachedSegmentId == 0 || attachedSegmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment attachedSegment = ref netManager.m_segments.m_buffer[attachedSegmentId];
                if ((attachedSegment.m_flags & NetSegment.Flags.Created) != 0
                    && attachedSegment.Info != null
                    && IsRailOrMetroTrack(attachedSegment.Info))
                {
                    trackCount++;
                }
            }

            return trackCount;
        }

        public static bool IsPedestrianBridgeTooCloseToMonorailStationWithRoad(Vector3 worldPosition)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || netManager.m_segments == null || netManager.m_segments.m_buffer == null)
                return false;

            RefreshMonorailStationWithRoadSegmentCache(netManager);
            if (MonorailStationWithRoadSegmentCache.Count == 0)
                return false;

            Vector3 flatPosition = worldPosition;
            flatPosition.y = 0f;
            float clearanceSqr = MonorailStationWithRoadBridgeClearance * MonorailStationWithRoadBridgeClearance;
            for (int i = 0; i < MonorailStationWithRoadSegmentCache.Count; i++)
            {
                ushort segmentId = MonorailStationWithRoadSegmentCache[i];
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !IsMonorailStationWithRoad(segment.Info))
                    continue;

                Vector3 closest = segment.GetClosestPosition(worldPosition);
                closest.y = 0f;
                if ((flatPosition - closest).sqrMagnitude <= clearanceSqr)
                    return true;
            }

            return false;
        }

        private static void RefreshMonorailStationWithRoadSegmentCache(NetManager netManager)
        {
            if (_monorailStationWithRoadCacheReady
                && _monorailStationWithRoadCachedSegmentCount == netManager.m_segmentCount)
            {
                return;
            }

            MonorailStationWithRoadSegmentCache.Clear();
            for (ushort segmentId = 1; segmentId < netManager.m_segments.m_size; segmentId++)
            {
                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !IsMonorailStationWithRoad(segment.Info))
                    continue;

                MonorailStationWithRoadSegmentCache.Add(segmentId);
            }

            _monorailStationWithRoadCachedSegmentCount = netManager.m_segmentCount;
            _monorailStationWithRoadCacheReady = true;
        }

        public static bool AllowsSurfaceCrossingOverlayTarget(ushort segmentId)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null)
                return false;

            return IsNormalSurfaceRoad(segment.Info) && HasPedestrianLane(segment.Info);
        }

        private static bool IsNormalSurfaceRoad(NetInfo info)
        {
            if (info == null || !(info.m_netAI is RoadBaseAI))
                return false;

            return IsNormalSurfaceNet(info);
        }

        private static bool IsGradeSeparatedRoadTarget(NetInfo info)
        {
            return IsNormalSurfaceRoad(info);
        }

        private static bool IsAnyGradeSeparatedPlacementTarget(NetInfo info)
        {
            return IsGradeSeparatedRoadTarget(info)
                   || IsGradeSeparatedNonRoadTarget(info);
        }

        private static bool IsPedestrianBridgePlacementTarget(NetInfo info)
        {
            return IsGradeSeparatedRoadTarget(info)
                   || IsSurfaceTrainTrack(info)
                   || IsSurfaceMetroTrack(info);
        }

        private static bool IsSubwayPlacementTarget(NetInfo info)
        {
            return IsGradeSeparatedRoadTarget(info)
                   || IsSurfaceTrainTrack(info)
                   || IsElevatedTrainTrack(info)
                   || IsSurfaceMetroTrack(info)
                   || IsElevatedMetroTrack(info);
        }

        private static bool IsGradeSeparatedNonRoadTarget(NetInfo info)
        {
            return IsSurfaceTrainTrack(info)
                   || IsElevatedTrainTrack(info)
                   || IsSurfaceMetroTrack(info)
                   || IsElevatedMetroTrack(info);
        }

        private static bool IsRailOrMetroTrack(NetInfo info)
        {
            return IsTrainTrack(info) || IsMetroTrack(info);
        }

        private static bool IsHighwayRoad(NetInfo info)
        {
            if (info == null)
                return false;

            string aiName = info.m_netAI == null ? string.Empty : info.m_netAI.GetType().Name;
            string prefabName = info.name ?? string.Empty;
            return ContainsAny(aiName, "Highway")
                   || ContainsAny(prefabName, "Highway");
        }

        private static bool IsSurfaceTrainTrack(NetInfo info)
        {
            return IsTrainTrack(info) && IsNormalSurfaceNet(info);
        }

        private static bool IsElevatedTrainTrack(NetInfo info)
        {
            return IsTrainTrack(info)
                   && IsElevatedNet(info)
                   && !IsUndergroundNet(info)
                   && !IsSlopeNet(info);
        }

        private static bool IsTrainTrack(NetInfo info)
        {
            if (info == null)
                return false;

            return info.m_netAI is TrainTrackBaseAI
                   || info.m_netAI is TrainTrackAI
                   || info.m_netAI is TrainTrackBridgeAI
                   || info.m_netAI is TrainTrackTunnelAI
                   || (NetInfoTextContains(info, "Train") && NetInfoTextContains(info, "Track"));
        }

        private static bool IsSurfaceMetroTrack(NetInfo info)
        {
            return IsMetroTrack(info) && IsNormalSurfaceNet(info);
        }

        private static bool IsElevatedMetroTrack(NetInfo info)
        {
            return IsMetroTrack(info)
                   && IsElevatedNet(info)
                   && !IsUndergroundNet(info)
                   && !IsSlopeNet(info);
        }

        private static bool IsMetroTrack(NetInfo info)
        {
            if (info == null)
                return false;

            return info.m_netAI is MetroTrackBaseAI
                   || info.m_netAI is MetroTrackAI
                   || info.m_netAI is MetroTrackBridgeAI
                   || info.m_netAI is MetroTrackTunnelAI
                   || (NetInfoTextContains(info, "Metro") && NetInfoTextContains(info, "Track"));
        }

        private static bool IsNormalSurfaceNet(NetInfo info)
        {
            return !IsElevatedNet(info)
                   && !IsUndergroundNet(info)
                   && !IsSlopeNet(info);
        }

        private static bool IsElevatedNet(NetInfo info)
        {
            string aiName = info == null || info.m_netAI == null ? string.Empty : info.m_netAI.GetType().Name;
            string prefabName = info == null ? string.Empty : info.name ?? string.Empty;
            return ContainsAny(aiName, "Bridge", "Elevated")
                   || ContainsAny(prefabName, "Bridge", "Elevated");
        }

        private static bool IsUndergroundNet(NetInfo info)
        {
            string aiName = info == null || info.m_netAI == null ? string.Empty : info.m_netAI.GetType().Name;
            string prefabName = info == null ? string.Empty : info.name ?? string.Empty;
            return ContainsAny(aiName, "Tunnel", "Underground")
                   || ContainsAny(prefabName, "Tunnel", "Underground");
        }

        private static bool IsSlopeNet(NetInfo info)
        {
            string aiName = info == null || info.m_netAI == null ? string.Empty : info.m_netAI.GetType().Name;
            string prefabName = info == null ? string.Empty : info.name ?? string.Empty;
            return ContainsAny(aiName, "Slope")
                   || ContainsAny(prefabName, "Slope");
        }

        private static bool IsMonorailStationWithRoad(NetInfo info)
        {
            return NetInfoTextContains(info, "Monorail")
                   && NetInfoTextContains(info, "Station")
                   && NetInfoTextContains(info, "Road");
        }

        private static bool IsStationNet(NetInfo info)
        {
            return NetInfoTextContains(info, "Station");
        }

        private static bool NetInfoTextContains(NetInfo info, string token)
        {
            if (info == null)
                return false;

            string aiName = info.m_netAI == null ? string.Empty : info.m_netAI.GetType().Name;
            string prefabName = info.name ?? string.Empty;
            string className = info.m_class == null ? string.Empty : info.m_class.name ?? string.Empty;
            return ContainsAny(aiName, token)
                   || ContainsAny(prefabName, token)
                   || ContainsAny(className, token);
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                if (value.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        public static bool HasExistingSurfaceCrossingNearNode(ushort segmentId, float segmentPosition, Vector3 worldPosition)
        {
            NetManager netManager = NetManager.instance;
            ushort nodeId = GetNearbySurfaceNode(netManager, segmentId, segmentPosition, worldPosition);

            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            return HasExistingSurfaceCrossingAtNode(netManager, nodeId);
        }

        public static bool IsThreePlusJunctionNode(ushort nodeId)
        {
            NetManager netManager = NetManager.instance;
            return IsThreePlusJunctionNode(netManager, nodeId);
        }

        public static bool TryGetThreePlusJunctionExclusionZone(ushort nodeId, out JunctionExclusionZone zone)
        {
            return TryBuildThreePlusJunctionExclusionZone(nodeId, false, out zone);
        }

        public static bool TryGetSurfaceJunctionExclusionZone(ushort nodeId, out JunctionExclusionZone zone)
        {
            return TryBuildThreePlusJunctionExclusionZone(nodeId, true, out zone);
        }

        private static bool TryBuildThreePlusJunctionExclusionZone(ushort nodeId, bool surfaceAndSignalRules, out JunctionExclusionZone zone)
        {
            zone = default(JunctionExclusionZone);

            NetManager netManager = NetManager.instance;
            if (!IsJunctionExclusionCandidateNode(netManager, nodeId, surfaceAndSignalRules))
                return false;

            Vector3 center = netManager.m_nodes.m_buffer[nodeId].m_position;
            Vector3 xAxis;
            if (!TryGetJunctionPrimaryAxis(netManager, nodeId, center, out xAxis))
                xAxis = Vector3.right;

            Vector3 zAxis = new Vector3(-xAxis.z, 0f, xAxis.x);
            if (zAxis.sqrMagnitude <= 0.01f)
                zAxis = Vector3.forward;
            else
                zAxis.Normalize();

            Vector3[][] surfacePolygons = new Vector3[48][];
            int surfacePolygonCount = 0;
            if (!surfaceAndSignalRules)
                AddGradeSeparatedCenterBlock(center, xAxis, zAxis, surfacePolygons, ref surfacePolygonCount);

            AddJunctionCenterBlock(netManager, nodeId, center, surfaceAndSignalRules, surfacePolygons, ref surfacePolygonCount);
            AddThreePlusJunctionCrossingBlocks(netManager, nodeId, center, surfaceAndSignalRules, surfacePolygons, ref surfacePolygonCount);

            if (surfacePolygonCount <= 0)
                return false;

            zone = new JunctionExclusionZone(center, xAxis, zAxis, GradeSeparatedJunctionCenterBlockDistance, GradeSeparatedJunctionCenterBlockDistance, null, TrimSurfacePolygons(surfacePolygons, surfacePolygonCount));
            return true;
        }

        private static void AddGradeSeparatedCenterBlock(Vector3 center, Vector3 xAxis, Vector3 zAxis, Vector3[][] surfacePolygons, ref int surfacePolygonCount)
        {
            Vector3 xOffset = xAxis * GradeSeparatedJunctionCenterBlockDistance;
            Vector3 zOffset = zAxis * GradeSeparatedJunctionCenterBlockDistance;
            AddSurfacePolygon(
                surfacePolygons,
                ref surfacePolygonCount,
                center - xOffset - zOffset,
                center + xOffset - zOffset,
                center + xOffset + zOffset,
                center - xOffset + zOffset);
        }

        private static void AddJunctionCenterBlock(NetManager netManager, ushort nodeId, Vector3 center, bool surfaceAndSignalRules, Vector3[][] surfacePolygons, ref int surfacePolygonCount)
        {
            if (netManager == null || nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            Vector3[] cornerPoints = new Vector3[16];
            int cornerPointCount = 0;
            for (int i = 0; i < NetNodeSegmentSlotCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null || !IsNormalSurfaceRoad(segment.Info))
                    continue;

                bool endSegment = segment.m_endNode == nodeId;
                if (!endSegment && segment.m_startNode != nodeId)
                    continue;

                Vector3 roadDirection;
                Vector3 crossingDirection;
                Vector3 insideEdgeCenter;
                if (!TryGetJunctionApproachFrame(netManager, nodeId, segmentId, ref segment, endSegment, center, surfaceAndSignalRules, out roadDirection, out crossingDirection, out insideEdgeCenter))
                    continue;

                float halfWidth = RoadSurfacePlacementGuard.EstimateCarriageHalfWidth(segment.Info);
                AddUniquePoint(cornerPoints, ref cornerPointCount, insideEdgeCenter - crossingDirection * halfWidth);
                AddUniquePoint(cornerPoints, ref cornerPointCount, insideEdgeCenter + crossingDirection * halfWidth);
            }

            Vector3[] hull;
            if (TryBuildConvexHull(cornerPoints, cornerPointCount, out hull))
                AddSurfacePolygon(surfacePolygons, ref surfacePolygonCount, hull);
        }

        private static void AddThreePlusJunctionCrossingBlocks(
            NetManager netManager,
            ushort nodeId,
            Vector3 center,
            bool surfaceAndSignalRules,
            Vector3[][] surfacePolygons,
            ref int surfacePolygonCount)
        {
            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            for (int i = 0; i < NetNodeSegmentSlotCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null || !IsNormalSurfaceRoad(segment.Info))
                    continue;

                bool endSegment = segment.m_endNode == nodeId;
                if (!endSegment && segment.m_startNode != nodeId)
                    continue;

                Vector3 roadDirection;
                Vector3 crossingDirection;
                Vector3 insideEdgeCenter;
                if (!TryGetJunctionApproachFrame(netManager, nodeId, segmentId, ref segment, endSegment, center, surfaceAndSignalRules, out roadDirection, out crossingDirection, out insideEdgeCenter))
                    continue;

                float halfWidth = RoadSurfacePlacementGuard.EstimateCarriageHalfWidth(segment.Info);
                Vector3 from;
                Vector3 to;
                if (surfaceAndSignalRules)
                {
                    from = insideEdgeCenter;
                    to = insideEdgeCenter + roadDirection * SurfaceCrossingPostVanillaBuffer;
                }
                else
                {
                    from = insideEdgeCenter;
                    to = from - roadDirection * SurfaceCrossingPostVanillaBuffer;
                }

                AddSurfacePolygon(surfacePolygons, ref surfacePolygonCount, BuildRoadWidthBlock(from, to, crossingDirection, halfWidth));
            }
        }

        private static bool TryGetJunctionApproachFrame(
            NetManager netManager,
            ushort nodeId,
            ushort segmentId,
            ref NetSegment segment,
            bool endSegment,
            Vector3 center,
            bool surfaceAndSignalRules,
            out Vector3 roadDirection,
            out Vector3 crossingDirection,
            out Vector3 insideEdgeCenter)
        {
            roadDirection = GetSegmentDirectionAwayFromNode(netManager, ref segment, nodeId);
            crossingDirection = Vector3.zero;
            insideEdgeCenter = Vector3.zero;

            if (roadDirection.sqrMagnitude <= 0.01f)
                return false;

            roadDirection.Normalize();
            crossingDirection = new Vector3(-roadDirection.z, 0f, roadDirection.x);

            VanillaCrossingPoint crossingPoint;
            if (TryGetVanillaCrossingPoint(segmentId, endSegment, out crossingPoint) && crossingPoint.NodeId == nodeId)
            {
                insideEdgeCenter = crossingPoint.WorldPosition - roadDirection * GradeSeparatedVanillaCrossingClearance;
                return true;
            }

            Vector3 boundaryCenter;
            if (TryCalculateVanillaCrossingPosition(segmentId, ref segment, endSegment, out boundaryCenter))
            {
                insideEdgeCenter = boundaryCenter - roadDirection * GradeSeparatedVanillaCrossingClearance;
                return true;
            }

            insideEdgeCenter = center + roadDirection * GradeSeparatedJunctionCenterBlockDistance;
            return true;
        }

        private static bool TryGetJunctionPrimaryAxis(NetManager netManager, ushort nodeId, Vector3 center, out Vector3 axis)
        {
            axis = Vector3.zero;
            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            for (int i = 0; i < NetNodeSegmentSlotCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !AllowsGradeSeparatedPlacementTarget(segmentId))
                    continue;

                ushort otherNodeId = segment.m_startNode == nodeId ? segment.m_endNode : segment.m_startNode;
                if (otherNodeId == 0 || otherNodeId >= netManager.m_nodes.m_size)
                    continue;

                Vector3 direction = netManager.m_nodes.m_buffer[otherNodeId].m_position - center;
                direction.y = 0f;
                if (direction.sqrMagnitude <= 0.01f)
                    continue;

                axis = direction.normalized;
                return true;
            }

            return false;
        }

        private static void AddSurfacePolygon(Vector3[][] surfacePolygons, ref int surfacePolygonCount, Vector3 first, Vector3 second, Vector3 third, Vector3 fourth)
        {
            if (surfacePolygons == null || surfacePolygonCount >= surfacePolygons.Length)
                return;

            surfacePolygons[surfacePolygonCount++] = new[] { first, second, third, fourth };
        }

        private static void AddSurfacePolygon(Vector3[][] surfacePolygons, ref int surfacePolygonCount, Vector3[] polygon)
        {
            if (surfacePolygons == null || surfacePolygonCount >= surfacePolygons.Length || polygon == null || polygon.Length < 3)
                return;

            surfacePolygons[surfacePolygonCount++] = polygon;
        }

        private static void AddUniquePoint(Vector3[] points, ref int pointCount, Vector3 point)
        {
            if (points == null || pointCount >= points.Length || point == Vector3.zero)
                return;

            for (int i = 0; i < pointCount; i++)
            {
                if ((points[i] - point).sqrMagnitude <= 0.25f)
                    return;
            }

            points[pointCount++] = point;
        }

        private static bool TryBuildConvexHull(Vector3[] points, int pointCount, out Vector3[] hull)
        {
            hull = null;
            if (points == null || pointCount < 3)
                return false;

            SortPointsForHull(points, pointCount);

            Vector3[] working = new Vector3[pointCount * 2];
            int hullCount = 0;
            for (int i = 0; i < pointCount; i++)
            {
                while (hullCount >= 2 && Cross2D(working[hullCount - 2], working[hullCount - 1], points[i]) <= 0.01f)
                    hullCount--;

                working[hullCount++] = points[i];
            }

            int lowerHullEnd = hullCount + 1;
            for (int i = pointCount - 2; i >= 0; i--)
            {
                while (hullCount >= lowerHullEnd && Cross2D(working[hullCount - 2], working[hullCount - 1], points[i]) <= 0.01f)
                    hullCount--;

                working[hullCount++] = points[i];
            }

            if (hullCount > 1)
                hullCount--;

            if (hullCount < 3)
                return false;

            hull = new Vector3[hullCount];
            for (int i = 0; i < hullCount; i++)
                hull[i] = working[i];

            return true;
        }

        private static void SortPointsForHull(Vector3[] points, int count)
        {
            for (int i = 1; i < count; i++)
            {
                Vector3 current = points[i];
                int j = i - 1;
                while (j >= 0 && CompareHullPoints(points[j], current) > 0)
                {
                    points[j + 1] = points[j];
                    j--;
                }

                points[j + 1] = current;
            }
        }

        private static int CompareHullPoints(Vector3 first, Vector3 second)
        {
            if (first.x < second.x)
                return -1;
            if (first.x > second.x)
                return 1;
            if (first.z < second.z)
                return -1;
            if (first.z > second.z)
                return 1;
            return 0;
        }

        private static float Cross2D(Vector3 origin, Vector3 first, Vector3 second)
        {
            return ((first.x - origin.x) * (second.z - origin.z)) - ((first.z - origin.z) * (second.x - origin.x));
        }

        private static Vector3[] BuildRoadWidthBlock(Vector3 from, Vector3 to, Vector3 crossingDirection, float halfWidth)
        {
            return new[]
            {
                from - crossingDirection * halfWidth,
                from + crossingDirection * halfWidth,
                to + crossingDirection * halfWidth,
                to - crossingDirection * halfWidth
            };
        }

        private static Vector3[][] TrimSurfacePolygons(Vector3[][] surfacePolygons, int surfacePolygonCount)
        {
            if (surfacePolygons == null || surfacePolygonCount <= 0)
                return new Vector3[0][];

            Vector3[][] trimmed = new Vector3[surfacePolygonCount][];
            for (int i = 0; i < surfacePolygonCount; i++)
                trimmed[i] = surfacePolygons[i];

            return trimmed;
        }

        private static Vector3 GetSegmentDirectionAwayFromNode(NetManager netManager, ref NetSegment segment, ushort nodeId)
        {
            ushort otherNodeId = segment.m_startNode == nodeId ? segment.m_endNode : segment.m_startNode;
            if (otherNodeId == 0 || otherNodeId >= netManager.m_nodes.m_size || nodeId >= netManager.m_nodes.m_size)
                return Vector3.zero;

            Vector3 direction = netManager.m_nodes.m_buffer[otherNodeId].m_position - netManager.m_nodes.m_buffer[nodeId].m_position;
            direction.y = 0f;
            return direction;
        }

        public static bool TryGetThreePlusJunctionInteriorNode(ushort segmentId, float segmentPosition, Vector3 worldPosition, out ushort nodeId)
        {
            nodeId = 0;
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null)
                return false;

            ushort startNodeId = segment.m_startNode;
            ushort endNodeId = segment.m_endNode;
            if (IsThreePlusJunctionNodeWithin(netManager, startNodeId, worldPosition))
            {
                nodeId = startNodeId;
                return true;
            }

            if (IsThreePlusJunctionNodeWithin(netManager, endNodeId, worldPosition))
            {
                nodeId = endNodeId;
                return true;
            }

            return false;
        }

        public static bool TryGetSurfaceCrossingExclusionNode(ushort segmentId, float segmentPosition, Vector3 worldPosition, out ushort nodeId)
        {
            ushort ownerSegmentId;
            VanillaCrossingPoint crossingPoint;
            if (!TryGetNearestActualVanillaCrossing(worldPosition, SurfaceCrossingPostVanillaBuffer, out ownerSegmentId, out crossingPoint))
            {
                nodeId = 0;
                return false;
            }

            nodeId = crossingPoint.NodeId;
            return true;
        }

        public static bool TryGetSurfacePlacementExclusionNode(ushort segmentId, Vector3 worldPosition, out ushort nodeId)
        {
            if (TryGetSurfaceCrossingExclusionNode(segmentId, 0f, worldPosition, out nodeId))
                return true;

            nodeId = 0;
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return false;

            return IsInsideSurfacePlacementExclusionNode(segment.m_startNode, worldPosition, out nodeId)
                   || IsInsideSurfacePlacementExclusionNode(segment.m_endNode, worldPosition, out nodeId);
        }

        private static bool IsInsideSurfacePlacementExclusionNode(ushort candidateNodeId, Vector3 worldPosition, out ushort nodeId)
        {
            nodeId = 0;
            JunctionExclusionZone zone;
            if (TryGetSurfaceJunctionExclusionZone(candidateNodeId, out zone) && zone.Contains(worldPosition))
            {
                nodeId = candidateNodeId;
                return true;
            }

            return false;
        }

        public static bool TryGetNearestActualVanillaCrossing(Vector3 worldPosition, float maxDistance, out ushort ownerSegmentId, out VanillaCrossingPoint crossingPoint)
        {
            ownerSegmentId = 0;
            crossingPoint = default(VanillaCrossingPoint);
            NetManager netManager = NetManager.instance;
            if (netManager == null)
                return false;

            if (!_vanillaCrossingCacheReady)
                ForceRefreshVanillaCrossingCache("on-demand");

            bool found = false;
            float bestDistance = maxDistance;
            for (int i = 0; i < VanillaCrossingCache.Count; i++)
            {
                CachedVanillaCrossingPoint cached = VanillaCrossingCache[i];
                if (HorizontalDistance(cached.CrossingPoint.WorldPosition, worldPosition) > bestDistance + SurfaceCrossingPostVanillaBuffer)
                    continue;

                VanillaCrossingPoint live;
                if (!TryGetActualVanillaCrossingPoint(cached.OwnerSegmentId, cached.CrossingPoint.IsEndSegment, out live))
                    continue;

                found = TryUseNearestActualVanillaCrossing(cached.OwnerSegmentId, live, worldPosition, ref bestDistance, ref ownerSegmentId, ref crossingPoint) || found;
            }

            return found;
        }

        private static void RefreshVanillaCrossingCache(NetManager netManager, string reason)
        {
            VanillaCrossingCache.Clear();
            if (netManager == null)
            {
                _vanillaCrossingCacheReady = false;
                _vanillaCrossingCacheDirty = true;
                return;
            }

            for (ushort segmentId = 1; segmentId < netManager.m_segments.m_size; segmentId++)
            {
                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null || !(segment.Info.m_netAI is RoadBaseAI))
                    continue;

                VanillaCrossingPoint crossingPoint;
                if (TryGetActualVanillaCrossingPoint(segmentId, false, out crossingPoint))
                    VanillaCrossingCache.Add(new CachedVanillaCrossingPoint(segmentId, crossingPoint));

                if (TryGetActualVanillaCrossingPoint(segmentId, true, out crossingPoint))
                    VanillaCrossingCache.Add(new CachedVanillaCrossingPoint(segmentId, crossingPoint));
            }

            _vanillaCrossingCacheReady = true;
            _vanillaCrossingCacheDirty = false;
            _vanillaCrossingNetworkQuietTimer = 0f;
            Debug.Log("[PedestrianCrossingToolkit] Vanilla crossing cache refreshed: reason="
                      + reason
                      + " crossings="
                      + VanillaCrossingCache.Count);
        }

        private static void StartVanillaCrossingCacheRefresh(string reason)
        {
            VanillaCrossingCacheRefreshBuffer.Clear();
            _vanillaCrossingCacheRefreshNextSegmentId = 1;
            _vanillaCrossingCacheRefreshReason = reason;
            _vanillaCrossingCacheRefreshInProgress = true;
            _vanillaCrossingNetworkQuietTimer = 0f;
        }

        private static void ProcessVanillaCrossingCacheRefresh(NetManager netManager)
        {
            if (netManager == null)
            {
                ResetVanillaCrossingCache();
                return;
            }

            ushort endSegmentId = (ushort)Mathf.Min(
                netManager.m_segments.m_size,
                _vanillaCrossingCacheRefreshNextSegmentId + VanillaCrossingCacheRefreshSegmentBatchSize);

            for (ushort segmentId = _vanillaCrossingCacheRefreshNextSegmentId; segmentId < endSegmentId; segmentId++)
                AddVanillaCrossingCacheEntriesForSegment(netManager, segmentId, VanillaCrossingCacheRefreshBuffer);

            _vanillaCrossingCacheRefreshNextSegmentId = endSegmentId;
            if (_vanillaCrossingCacheRefreshNextSegmentId < netManager.m_segments.m_size)
                return;

            VanillaCrossingCache.Clear();
            VanillaCrossingCache.AddRange(VanillaCrossingCacheRefreshBuffer);
            VanillaCrossingCacheRefreshBuffer.Clear();
            _vanillaCrossingCacheReady = true;
            _vanillaCrossingCacheDirty = false;
            _vanillaCrossingCacheRefreshInProgress = false;
            Debug.Log("[PedestrianCrossingToolkit] Vanilla crossing cache refreshed: reason="
                      + _vanillaCrossingCacheRefreshReason
                      + " crossings="
                      + VanillaCrossingCache.Count);
            _vanillaCrossingCacheRefreshReason = string.Empty;
        }

        private static void AddVanillaCrossingCacheEntriesForSegment(NetManager netManager, ushort segmentId, List<CachedVanillaCrossingPoint> cache)
        {
            if (netManager == null || cache == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null || !(segment.Info.m_netAI is RoadBaseAI))
                return;

            VanillaCrossingPoint crossingPoint;
            if (TryGetActualVanillaCrossingPoint(segmentId, false, out crossingPoint))
                cache.Add(new CachedVanillaCrossingPoint(segmentId, crossingPoint));

            if (TryGetActualVanillaCrossingPoint(segmentId, true, out crossingPoint))
                cache.Add(new CachedVanillaCrossingPoint(segmentId, crossingPoint));
        }

        private static VanillaCrossingNetworkSignature BuildVanillaCrossingNetworkSignature(NetManager netManager)
        {
            if (netManager == null)
                return new VanillaCrossingNetworkSignature(0, 0, 0);

            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + netManager.m_segmentCount;
                hash = (hash * 31) + netManager.m_nodeCount;
                return new VanillaCrossingNetworkSignature(netManager.m_segmentCount, netManager.m_nodeCount, hash);
            }
        }

        private static bool TryUseNearestActualVanillaCrossing(
            ushort segmentId,
            VanillaCrossingPoint candidate,
            Vector3 worldPosition,
            ref float bestDistance,
            ref ushort ownerSegmentId,
            ref VanillaCrossingPoint crossingPoint)
        {
            float distance = HorizontalDistance(candidate.WorldPosition, worldPosition);
            if (distance > bestDistance)
                return false;

            bestDistance = distance;
            ownerSegmentId = segmentId;
            crossingPoint = candidate;
            return true;
        }

        public static bool IsBlockedGradeSeparatedJunctionApproach(ushort segmentId, Vector3 worldPosition)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null)
                return false;

            return IsBlockedGradeSeparatedJunctionApproachNode(netManager, segmentId, ref segment, false, worldPosition)
                   || IsBlockedGradeSeparatedJunctionApproachNode(netManager, segmentId, ref segment, true, worldPosition);
        }

        public static bool TryGetActualVanillaCrossingNear(ushort segmentId, Vector3 worldPosition, bool includeSurfaceBuffer, out VanillaCrossingPoint crossingPoint)
        {
            crossingPoint = default(VanillaCrossingPoint);
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null)
                return false;

            bool found = false;
            float bestDistance = float.MaxValue;
            VanillaCrossingPoint candidate;
            if (TryGetActualVanillaCrossingPoint(segmentId, false, out candidate)
                && IsWithinActualVanillaCrossingZone(netManager, candidate, worldPosition, includeSurfaceBuffer))
            {
                found = true;
                bestDistance = HorizontalDistance(candidate.WorldPosition, worldPosition);
                crossingPoint = candidate;
            }

            if (TryGetActualVanillaCrossingPoint(segmentId, true, out candidate)
                && IsWithinActualVanillaCrossingZone(netManager, candidate, worldPosition, includeSurfaceBuffer))
            {
                float distance = HorizontalDistance(candidate.WorldPosition, worldPosition);
                if (!found || distance < bestDistance)
                {
                    found = true;
                    crossingPoint = candidate;
                }
            }

            return found;
        }

        public static bool TryGetVanillaCrossingNear(ushort segmentId, Vector3 worldPosition, float maxDistance, out VanillaCrossingPoint crossingPoint)
        {
            crossingPoint = default(VanillaCrossingPoint);
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null)
                return false;

            bool found = false;
            float bestDistance = maxDistance;
            VanillaCrossingPoint candidate;
            if (TryGetVanillaCrossingPoint(segmentId, false, out candidate))
                found = TryUseNearestVanillaCrossing(candidate, worldPosition, ref bestDistance, ref crossingPoint);

            if (TryGetVanillaCrossingPoint(segmentId, true, out candidate))
                found = TryUseNearestVanillaCrossing(candidate, worldPosition, ref bestDistance, ref crossingPoint) || found;

            return found;
        }

        private static bool TryUseNearestVanillaCrossing(VanillaCrossingPoint candidate, Vector3 worldPosition, ref float bestDistance, ref VanillaCrossingPoint crossingPoint)
        {
            float distance = HorizontalDistance(candidate.WorldPosition, worldPosition);
            if (distance > bestDistance)
                return false;

            bestDistance = distance;
            crossingPoint = candidate;
            return true;
        }

        public static bool TryGetActualVanillaCrossingPoint(ushort segmentId, bool endSegment, out VanillaCrossingPoint crossingPoint)
        {
            if (!TryGetVanillaCrossingPoint(segmentId, endSegment, out crossingPoint))
                return false;

            return IsThreePlusJunctionNode(crossingPoint.NodeId);
        }

        public static bool TryGetVanillaCrossingPoint(ushort segmentId, bool endSegment, out VanillaCrossingPoint crossingPoint)
        {
            crossingPoint = default(VanillaCrossingPoint);
            NetManager netManager = NetManager.instance;
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null)
                return false;

            NetSegment.Flags crossingFlag = endSegment ? NetSegment.Flags.CrossingEnd : NetSegment.Flags.CrossingStart;
            if ((segment.m_flags & crossingFlag) == 0)
                return false;

            ushort nodeId = endSegment ? segment.m_endNode : segment.m_startNode;
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            Vector3 crossingPosition;
            if (!TryCalculateVanillaCrossingPosition(segmentId, ref segment, endSegment, out crossingPosition))
                return false;

            Vector3 nodePosition = netManager.m_nodes.m_buffer[nodeId].m_position;
            float segmentPosition = EstimateSegmentPosition(netManager, ref segment, crossingPosition);
            crossingPoint = new VanillaCrossingPoint(nodeId, endSegment, crossingPosition, segmentPosition, HorizontalDistance(nodePosition, crossingPosition));
            return true;
        }

        private static bool IsBlockedGradeSeparatedJunctionApproachNode(NetManager netManager, ushort segmentId, ref NetSegment segment, bool endSegment, Vector3 worldPosition)
        {
            ushort nodeId = endSegment ? segment.m_endNode : segment.m_startNode;
            if (!IsThreePlusJunctionNode(netManager, nodeId))
                return false;

            JunctionExclusionZone zone;
            if (TryGetThreePlusJunctionExclusionZone(nodeId, out zone) && zone.Contains(worldPosition))
                return true;

            VanillaCrossingPoint crossingPoint;
            if (!TryGetActualVanillaCrossingPoint(segmentId, endSegment, out crossingPoint))
                return false;

            float distance = HorizontalDistance(netManager.m_nodes.m_buffer[nodeId].m_position, worldPosition);
            return distance < crossingPoint.DistanceFromNode - GetActualCrossingTolerance(crossingPoint.DistanceFromNode);
        }

        private static bool IsWithinActualVanillaCrossingZone(NetManager netManager, VanillaCrossingPoint crossingPoint, Vector3 worldPosition, bool includeSurfaceBuffer)
        {
            if (crossingPoint.NodeId == 0 || crossingPoint.NodeId >= netManager.m_nodes.m_size)
                return false;

            Vector3 nodePosition = netManager.m_nodes.m_buffer[crossingPoint.NodeId].m_position;
            float distanceFromNode = HorizontalDistance(nodePosition, worldPosition);
            float maxDistance = crossingPoint.DistanceFromNode + GetActualCrossingTolerance(crossingPoint.DistanceFromNode);
            if (includeSurfaceBuffer)
                maxDistance += SurfaceCrossingPostVanillaBuffer;

            return distanceFromNode <= maxDistance;
        }

        private static float GetActualCrossingTolerance(float crossingDistance)
        {
            return Mathf.Max(1.5f, crossingDistance * 0.25f);
        }

        private static bool TryCalculateVanillaCrossingPosition(ushort segmentId, ref NetSegment segment, bool endSegment, out Vector3 position)
        {
            position = Vector3.zero;
            Vector3 left;
            Vector3 right;
            Vector3 direction;
            bool smooth;
            segment.CalculateCorner(segmentId, true, !endSegment, true, out left, out direction, out smooth);
            segment.CalculateCorner(segmentId, true, !endSegment, false, out right, out direction, out smooth);
            position = (left + right) * 0.5f;
            return position != Vector3.zero;
        }

        private static float EstimateSegmentPosition(NetManager netManager, ref NetSegment segment, Vector3 position)
        {
            Vector3 start = netManager.m_nodes.m_buffer[segment.m_startNode].m_position;
            Vector3 end = netManager.m_nodes.m_buffer[segment.m_endNode].m_position;
            Vector3 segmentVector = end - start;
            segmentVector.y = 0f;
            float lengthSquared = segmentVector.sqrMagnitude;
            if (lengthSquared <= 0.01f)
                return 0.5f;

            Vector3 offset = position - start;
            offset.y = 0f;
            return Mathf.Clamp01(Vector3.Dot(offset, segmentVector) / lengthSquared);
        }

        private static bool IsThreePlusJunctionNode(NetManager netManager, ushort nodeId)
        {
            return IsGradeSeparatedJunctionNode(netManager, nodeId);
        }

        private static bool IsJunctionExclusionCandidateNode(NetManager netManager, ushort nodeId, bool surfaceAndSignalRules)
        {
            return surfaceAndSignalRules
                ? IsSurfaceSignalJunctionNode(netManager, nodeId)
                : IsThreePlusJunctionNode(netManager, nodeId);
        }

        private static bool IsSurfaceSignalJunctionNode(NetManager netManager, ushort nodeId)
        {
            if (!IsUsableSurfaceNode(netManager, nodeId, true))
                return false;

            int roadCount = CountNormalSurfaceRoadSegments(netManager, nodeId, true);
            if (roadCount < 2)
                return false;

            NetNode.Flags flags = netManager.m_nodes.m_buffer[nodeId].m_flags;
            return (flags & (NetNode.Flags.Junction | NetNode.Flags.TrafficLights)) != 0 || roadCount >= 3;
        }

        private static bool IsGradeSeparatedJunctionNode(NetManager netManager, ushort nodeId)
        {
            if (!IsUsableSurfaceNode(netManager, nodeId, true))
                return false;

            int roadCount = CountNormalSurfaceRoadSegments(netManager, nodeId, true);
            if (roadCount < 2)
                return false;

            NetNode.Flags flags = netManager.m_nodes.m_buffer[nodeId].m_flags;
            return (flags & (NetNode.Flags.Junction | NetNode.Flags.TrafficLights)) != 0 || roadCount >= 3;
        }

        private static bool IsUsableSurfaceNode(NetManager netManager, ushort nodeId, bool allowTransition)
        {
            if (netManager == null || nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0 || (node.m_flags & NetNode.Flags.Underground) != 0)
                return false;

            if (!allowTransition && (node.m_flags & NetNode.Flags.Transition) != 0)
                return false;

            return true;
        }

        private static int CountNormalSurfaceRoadSegments(NetManager netManager, ushort nodeId, bool allowTransition)
        {
            if (!IsUsableSurfaceNode(netManager, nodeId, allowTransition))
                return 0;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            int roadCount = 0;
            for (int i = 0; i < NetNodeSegmentSlotCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) != 0
                    && segment.Info != null
                    && IsNormalSurfaceRoad(segment.Info))
                {
                    roadCount++;
                }
            }

            return roadCount;
        }

        private static bool IsThreePlusJunctionNodeWithin(NetManager netManager, ushort nodeId, Vector3 worldPosition)
        {
            JunctionExclusionZone zone;
            if (!TryGetThreePlusJunctionExclusionZone(nodeId, out zone))
                return false;

            return zone.Contains(worldPosition);
        }

        private static bool HasExistingSurfaceCrossingAtNode(NetManager netManager, ushort nodeId)
        {
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return false;

            if (node.CountSegments() <= 2)
                return false;

            for (int i = 0; i < NetNodeSegmentSlotCount; i++)
            {
                ushort attachedSegmentId = node.GetSegment(i);
                if (attachedSegmentId == 0 || attachedSegmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment attachedSegment = ref netManager.m_segments.m_buffer[attachedSegmentId];
                if ((attachedSegment.m_flags & NetSegment.Flags.Created) == 0 || attachedSegment.Info == null)
                    continue;

                if (!HasPedestrianLane(attachedSegment.Info))
                    continue;

                if (attachedSegment.m_startNode == nodeId)
                {
                    if ((attachedSegment.m_flags & NetSegment.Flags.CrossingStart) != 0)
                        return true;
                }
                else if (attachedSegment.m_endNode == nodeId)
                {
                    if ((attachedSegment.m_flags & NetSegment.Flags.CrossingEnd) != 0)
                        return true;
                }
            }

            return false;
        }

        private static ushort GetNearbySurfaceNode(NetManager netManager, ushort segmentId, float segmentPosition, Vector3 worldPosition)
        {
            ushort nodeId = GetSurfaceSlotNode(netManager, segmentId, segmentPosition);
            if (nodeId == 0)
                return 0;

            Vector3 nodePosition = netManager.m_nodes.m_buffer[nodeId].m_position;
            return HorizontalDistance(nodePosition, worldPosition) <= SurfaceJunctionBlockDistance
                ? nodeId
                : (ushort)0;
        }

        private static ushort GetSurfaceSlotNode(NetManager netManager, ushort segmentId, float segmentPosition)
        {
            if (segmentPosition > SurfaceJunctionBlockSegmentPosition
                && segmentPosition < 1f - SurfaceJunctionBlockSegmentPosition)
                return 0;

            if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return 0;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return 0;

            ushort nodeId = segmentPosition <= 0.5f ? segment.m_startNode : segment.m_endNode;
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return 0;

            return nodeId;
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            float dx = first.x - second.x;
            float dz = first.z - second.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static bool HasPedestrianLane(NetInfo info)
        {
            if (info == null || info.m_lanes == null)
                return false;

            for (int i = 0; i < info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = info.m_lanes[i];
                if (lane == null)
                    continue;

                if ((lane.m_laneType & NetInfo.LaneType.Pedestrian) != 0)
                    return true;
            }

            return false;
        }
    }

    public static class RemovalCrossingTool
    {
        public static void Reset()
        {
        }

        public static CrossingPlacementResult PreviewRemoval(ushort segmentId, float segmentPosition, Vector3 worldPosition, bool nearNode, int slotNumber, bool isEndSegmentSlot)
        {
            if (segmentId == 0)
                return CrossingPlacementResult.Invalid("No supported crossing target selected.");

            CrossingPlacementRecord probe = new CrossingPlacementRecord(PedestrianToolMode.RemoveCrossing, segmentId, segmentPosition, worldPosition, true, "Remove crossing preview accepted.", nearNode, slotNumber, isEndSegmentSlot);
            return CrossingPlacementRegistry.HasAssetAt(probe)
                ? CrossingPlacementResult.Valid("Remove crossing preview accepted.")
                : CrossingPlacementResult.Invalid("No crossing at this location.");
        }
    }

    public struct CrossingPlacementResult
    {
        public readonly bool Success;
        public readonly string Message;

        private CrossingPlacementResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static CrossingPlacementResult Valid(string message)
        {
            return new CrossingPlacementResult(true, message);
        }

        public static CrossingPlacementResult Invalid(string message)
        {
            return new CrossingPlacementResult(false, message);
        }
    }
}
