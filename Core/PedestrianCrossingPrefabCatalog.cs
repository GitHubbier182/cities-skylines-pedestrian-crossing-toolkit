using System;
using System.Collections.Generic;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public static class PedestrianCrossingPrefabCatalog
    {
        private const int MaxPrefabLogs = 14;
        private const float SubwayTraversalSpeedMultiplier = 1.05f;
        private static readonly HashSet<NetInfo> SpeedAdjustedTunnelPrefabs = new HashSet<NetInfo>();
        private static bool _hasScanned;
        private static NetInfo _pedestrianPathPrefab;
        private static NetInfo _pedestrianBridgePrefab;
        private static NetInfo _pedestrianTunnelPrefab;

        public static NetInfo PedestrianPathPrefab
        {
            get { return _pedestrianPathPrefab; }
        }

        public static NetInfo SurfaceCrossingPathPrefab
        {
            get { return FindPreferredSurfaceCrossingPath() ?? _pedestrianPathPrefab; }
        }

        public static NetInfo PedestrianBridgePrefab
        {
            get { return _pedestrianBridgePrefab; }
        }

        public static NetInfo BridgeDeckPrefab
        {
            get
            {
                if (_pedestrianPathPrefab == null)
                    return null;

                string pathName = GetPrefabName(_pedestrianPathPrefab);
                if (pathName.IndexOf("gravel", StringComparison.Ordinal) >= 0)
                {
                    NetInfo alternate = FindPreferredBridgeDeckPath();
                    if (alternate != null)
                        return alternate;
                }

                return _pedestrianPathPrefab;
            }
        }

        public static NetInfo BridgeDeckBuildPrefab
        {
            get
            {
                return BridgeDeckPrefab
                       ?? _pedestrianPathPrefab;
            }
        }

        public static NetInfo InvisibleBridgePathPrefab
        {
            get
            {
                return _pedestrianTunnelPrefab
                       ?? BridgeDeckPrefab
                       ?? _pedestrianPathPrefab;
            }
        }

        public static NetInfo PedestrianTunnelPrefab
        {
            get { return _pedestrianTunnelPrefab; }
        }

        public static bool HasPathAssets
        {
            get { return _pedestrianPathPrefab != null || _pedestrianBridgePrefab != null || _pedestrianTunnelPrefab != null; }
        }

        public static void Refresh(string reason)
        {
            if (_hasScanned)
                return;

            _hasScanned = true;
            _pedestrianPathPrefab = null;
            _pedestrianBridgePrefab = null;
            _pedestrianTunnelPrefab = null;

            try
            {
                int loadedCount = PrefabCollection<NetInfo>.LoadedCount();
                int scanned = 0;
                int pedestrianPrefabs = 0;
                int pathPrefabs = 0;
                int bridgePrefabs = 0;
                int tunnelPrefabs = 0;
                int logged = 0;
                int selectedPathScore = int.MinValue;
                int selectedBridgeScore = int.MinValue;
                int selectedTunnelScore = int.MinValue;

                for (uint i = 0; i < loadedCount; i++)
                {
                    NetInfo info = PrefabCollection<NetInfo>.GetLoaded(i);
                    if (info == null || info.m_netAI == null)
                        continue;

                    scanned++;
                    PedestrianPathPrefabKind kind = Classify(info);
                    if (kind == PedestrianPathPrefabKind.None)
                        continue;

                    pedestrianPrefabs++;
                    int pedestrianLaneCount = CountPedestrianLanes(info);
                    switch (kind)
                    {
                        case PedestrianPathPrefabKind.Path:
                            pathPrefabs++;
                            int pathScore = ScorePathPrefab(info);
                            if (_pedestrianPathPrefab == null || pathScore > selectedPathScore)
                            {
                                _pedestrianPathPrefab = info;
                                selectedPathScore = pathScore;
                            }
                            break;
                        case PedestrianPathPrefabKind.Bridge:
                            bridgePrefabs++;
                            int bridgeScore = ScoreBridgePrefab(info);
                            if (_pedestrianBridgePrefab == null || bridgeScore > selectedBridgeScore)
                            {
                                _pedestrianBridgePrefab = info;
                                selectedBridgeScore = bridgeScore;
                            }
                            break;
                        case PedestrianPathPrefabKind.Tunnel:
                            tunnelPrefabs++;
                            int tunnelScore = ScoreTunnelPrefab(info);
                            if (_pedestrianTunnelPrefab == null || tunnelScore > selectedTunnelScore)
                            {
                                _pedestrianTunnelPrefab = info;
                                selectedTunnelScore = tunnelScore;
                            }
                            break;
                    }

                    if (!PedestrianCrossingLog.VerboseDiagnostics || logged >= MaxPrefabLogs)
                        continue;

                    logged++;
                    Debug.Log("[PedestrianCrossingToolkit] Pedestrian prefab candidate: reason="
                              + reason
                              + " kind=" + kind
                              + " name=" + info.name
                              + " ai=" + info.m_netAI.GetType().Name
                              + " lanes=" + (info.m_lanes == null ? 0 : info.m_lanes.Length)
                              + " pedestrianLanes=" + pedestrianLaneCount);
                }

                Debug.Log("[PedestrianCrossingToolkit] Pedestrian prefab catalog: reason="
                          + reason
                          + " scanned=" + scanned
                          + " candidates=" + pedestrianPrefabs
                          + " paths=" + pathPrefabs
                          + " bridges=" + bridgePrefabs
                          + " tunnels=" + tunnelPrefabs
                          + " selectedPath=" + FormatPrefabName(_pedestrianPathPrefab)
                          + " selectedBridge=" + FormatPrefabName(_pedestrianBridgePrefab)
                          + " selectedTunnel=" + FormatPrefabName(_pedestrianTunnelPrefab));
                ApplySubwayTraversalSpeedPreference(_pedestrianTunnelPrefab);
            }
            catch (Exception e)
            {
                Debug.LogError("[PedestrianCrossingToolkit] Pedestrian prefab catalog scan failed: " + e);
            }
        }

        public static void Reset()
        {
            _hasScanned = false;
            _pedestrianPathPrefab = null;
            _pedestrianBridgePrefab = null;
            _pedestrianTunnelPrefab = null;
        }

        private static PedestrianPathPrefabKind Classify(NetInfo info)
        {
            if (info.m_netAI is PedestrianBridgeAI)
                return PedestrianPathPrefabKind.Bridge;

            if (info.m_netAI is PedestrianTunnelAI)
                return PedestrianPathPrefabKind.Tunnel;

            if (info.m_netAI is PedestrianPathAI)
                return PedestrianPathPrefabKind.Path;

            string name = info.name == null ? string.Empty : info.name.ToLowerInvariant();
            if (name.IndexOf("pedestrian", StringComparison.Ordinal) < 0 && name.IndexOf("foot", StringComparison.Ordinal) < 0)
                return PedestrianPathPrefabKind.None;

            if (name.IndexOf("bridge", StringComparison.Ordinal) >= 0)
                return PedestrianPathPrefabKind.Bridge;

            if (name.IndexOf("tunnel", StringComparison.Ordinal) >= 0 || name.IndexOf("subway", StringComparison.Ordinal) >= 0)
                return PedestrianPathPrefabKind.Tunnel;

            return HasPedestrianLane(info) ? PedestrianPathPrefabKind.Path : PedestrianPathPrefabKind.None;
        }

        private static bool HasPedestrianLane(NetInfo info)
        {
            return CountPedestrianLanes(info) > 0;
        }

        private static int CountPedestrianLanes(NetInfo info)
        {
            if (info == null || info.m_lanes == null)
                return 0;

            int count = 0;
            for (int i = 0; i < info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = info.m_lanes[i];
                if (lane != null && (lane.m_laneType & NetInfo.LaneType.Pedestrian) != 0)
                    count++;
            }

            return count;
        }

        private static int ScorePathPrefab(NetInfo info)
        {
            string name = GetPrefabName(info);
            int score = 0;
            if (name.IndexOf("gravel", StringComparison.Ordinal) >= 0)
                score += 20;
            if (name.IndexOf("connection", StringComparison.Ordinal) >= 0)
                score -= 10;

            return score;
        }

        private static NetInfo FindPreferredBridgeDeckPath()
        {
            int loadedCount = PrefabCollection<NetInfo>.LoadedCount();
            NetInfo best = null;
            int bestScore = int.MinValue;

            for (uint i = 0; i < loadedCount; i++)
            {
                NetInfo info = PrefabCollection<NetInfo>.GetLoaded(i);
                if (Classify(info) != PedestrianPathPrefabKind.Path)
                    continue;

                int score = ScoreBridgeDeckPath(info);
                if (best == null || score > bestScore)
                {
                    best = info;
                    bestScore = score;
                }
            }

            return best;
        }

        private static NetInfo FindPreferredSurfaceCrossingPath()
        {
            int loadedCount = PrefabCollection<NetInfo>.LoadedCount();
            NetInfo best = null;
            int bestScore = int.MinValue;

            for (uint i = 0; i < loadedCount; i++)
            {
                NetInfo info = PrefabCollection<NetInfo>.GetLoaded(i);
                if (Classify(info) != PedestrianPathPrefabKind.Path)
                    continue;

                int score = ScoreSurfaceCrossingPath(info);
                if (best == null || score > bestScore)
                {
                    best = info;
                    bestScore = score;
                }
            }

            return best;
        }

        private static int ScoreSurfaceCrossingPath(NetInfo info)
        {
            string name = GetPrefabName(info);
            int score = 0;

            if (name.IndexOf("invisible", StringComparison.Ordinal) >= 0)
                score += 100;
            if (name.IndexOf("connection surface", StringComparison.Ordinal) >= 0)
                score += 80;
            if (name.IndexOf("connection path", StringComparison.Ordinal) >= 0)
                score += 65;
            if (name.IndexOf("connection", StringComparison.Ordinal) >= 0)
                score += 50;
            if (name.IndexOf("inside", StringComparison.Ordinal) >= 0)
                score += 30;
            if (name.IndexOf("level", StringComparison.Ordinal) >= 0)
                score += 10;
            if (name.IndexOf("gravel", StringComparison.Ordinal) >= 0)
                score -= 60;
            if (name.IndexOf("elevated", StringComparison.Ordinal) >= 0
                || name.IndexOf("tunnel", StringComparison.Ordinal) >= 0
                || name.IndexOf("underground", StringComparison.Ordinal) >= 0
                || name.IndexOf("transition", StringComparison.Ordinal) >= 0)
                score -= 80;

            return score;
        }

        private static int ScoreBridgeDeckPath(NetInfo info)
        {
            string name = GetPrefabName(info);
            int score = 0;

            if (name.IndexOf("pavement", StringComparison.Ordinal) >= 0)
                score += 40;
            if (name.IndexOf("level", StringComparison.Ordinal) >= 0)
                score += 25;
            if (name.IndexOf("connection path", StringComparison.Ordinal) >= 0)
                score += 10;
            if (name.IndexOf("connection", StringComparison.Ordinal) >= 0)
                score -= 5;
            if (name.IndexOf("inside", StringComparison.Ordinal) >= 0
                || name.IndexOf("underground", StringComparison.Ordinal) >= 0
                || name.IndexOf("transition", StringComparison.Ordinal) >= 0)
                score -= 40;
            if (name.IndexOf("gravel", StringComparison.Ordinal) >= 0)
                score -= 60;

            return score;
        }

        private static int ScoreBridgePrefab(NetInfo info)
        {
            string name = GetPrefabName(info);
            int score = 0;
            if (name.IndexOf("elevated", StringComparison.Ordinal) >= 0)
                score += 30;
            if (name.IndexOf("gravel", StringComparison.Ordinal) >= 0)
                score -= 5;

            return score;
        }

        private static int ScoreTunnelPrefab(NetInfo info)
        {
            string name = GetPrefabName(info);
            int score = 0;
            if (name.IndexOf("tunnel", StringComparison.Ordinal) >= 0)
                score += 40;
            if (name.IndexOf("slope", StringComparison.Ordinal) >= 0
                || name.IndexOf("transition", StringComparison.Ordinal) >= 0)
                score -= 30;

            return score;
        }

        private static void ApplySubwayTraversalSpeedPreference(NetInfo info)
        {
            if (info == null || info.m_lanes == null || SpeedAdjustedTunnelPrefabs.Contains(info))
                return;

            int adjusted = 0;
            for (int i = 0; i < info.m_lanes.Length; i++)
            {
                NetInfo.Lane lane = info.m_lanes[i];
                if (lane == null || (lane.m_laneType & NetInfo.LaneType.Pedestrian) == 0)
                    continue;

                lane.m_speedLimit *= SubwayTraversalSpeedMultiplier;
                adjusted++;
            }

            SpeedAdjustedTunnelPrefabs.Add(info);
            if (adjusted > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Subway traversal speed preference applied: prefab="
                          + FormatPrefabName(info)
                          + " pedestrianLanes="
                          + adjusted
                          + " multiplier="
                          + SubwayTraversalSpeedMultiplier.ToString("0.00"));
            }
        }

        private static string GetPrefabName(NetInfo info)
        {
            return info == null || info.name == null ? string.Empty : info.name.ToLowerInvariant();
        }

        private static string FormatPrefabName(NetInfo info)
        {
            return info == null ? "none" : info.name;
        }

        private enum PedestrianPathPrefabKind
        {
            None,
            Path,
            Bridge,
            Tunnel
        }
    }
}
