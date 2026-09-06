using System;
using System.Collections.Generic;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public struct CrossingAutoScanSummary
    {
        public static readonly CrossingAutoScanSummary Empty = new CrossingAutoScanSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, string.Empty);

        public readonly int ScannedNodes;
        public readonly int ScannedExistingCrossings;
        public readonly int ScannedLongRoadSegments;
        public readonly int Hotspots;
        public readonly int PlannedPlacements;
        public readonly int PlannedRemovals;
        public readonly int GradeSeparatedPlacements;
        public readonly int SignalPlacements;
        public readonly int SurfacePlacements;
        public readonly int SkippedExisting;
        public readonly int Rejected;
        public readonly int Capped;
        public readonly string FirstRejection;

        public CrossingAutoScanSummary(
            int scannedNodes,
            int scannedExistingCrossings,
            int scannedLongRoadSegments,
            int hotspots,
            int plannedPlacements,
            int plannedRemovals,
            int gradeSeparatedPlacements,
            int signalPlacements,
            int surfacePlacements,
            int skippedExisting,
            int rejected,
            int capped,
            string firstRejection)
        {
            ScannedNodes = scannedNodes;
            ScannedExistingCrossings = scannedExistingCrossings;
            ScannedLongRoadSegments = scannedLongRoadSegments;
            Hotspots = hotspots;
            PlannedPlacements = plannedPlacements;
            PlannedRemovals = plannedRemovals;
            GradeSeparatedPlacements = gradeSeparatedPlacements;
            SignalPlacements = signalPlacements;
            SurfacePlacements = surfacePlacements;
            SkippedExisting = skippedExisting;
            Rejected = rejected;
            Capped = capped;
            FirstRejection = firstRejection ?? string.Empty;
        }

        public string ToStatusString()
        {
            if (PlannedPlacements == 0 && PlannedRemovals == 0)
            {
                return "Auto scan found no high-confidence pedestrian traffic fixes."
                       + ToRescanRecommendationString()
                       + (string.IsNullOrEmpty(FirstRejection) ? string.Empty : " First skip: " + FirstRejection);
            }

            return "Auto scan planned "
                   + PlannedPlacements
                   + " crossing"
                   + (PlannedPlacements == 1 ? string.Empty : "s")
                   + " and "
                   + PlannedRemovals
                   + " removal"
                   + (PlannedRemovals == 1 ? string.Empty : "s")
                   + "."
                   + ToRescanRecommendationString();
        }

        public string ToRescanRecommendationString()
        {
            return Capped > 0
                ? " PCT found more beneficial locations beyond this run's "
                  + CrossingAutoScanPlanner.MaxPlannedPlacements
                  + "-crossing safety limit; another Auto Scan is recommended."
                : string.Empty;
        }

        public string ToLogString()
        {
            return "nodes=" + ScannedNodes
                   + " existingCrossings=" + ScannedExistingCrossings
                   + " longRoads=" + ScannedLongRoadSegments
                   + " hotspots=" + Hotspots
                   + " placements=" + PlannedPlacements
                   + " removals=" + PlannedRemovals
                   + " gradeSeparated=" + GradeSeparatedPlacements
                   + " signals=" + SignalPlacements
                   + " surface=" + SurfacePlacements
                   + " skippedExisting=" + SkippedExisting
                   + " rejected=" + Rejected
                   + " capped=" + Capped
                   + (string.IsNullOrEmpty(FirstRejection) ? string.Empty : " firstRejection=\"" + FirstRejection + "\"");
        }
    }

    public struct CrossingAutoScanPlan
    {
        public static readonly CrossingAutoScanPlan Empty = new CrossingAutoScanPlan(new CrossingPlacementRecord[0], 0, new int[0], 0, new int[0], CrossingAutoScanSummary.Empty);

        public readonly CrossingPlacementRecord[] Placements;
        public readonly int PlacementCount;
        public readonly int[] RemovalAssetIds;
        public readonly int RemovalCount;
        public readonly int[] PlacementRemovalAssetIds;
        public readonly CrossingAutoScanSummary Summary;

        public CrossingAutoScanPlan(CrossingPlacementRecord[] placements, int placementCount, int[] removalAssetIds, int removalCount, CrossingAutoScanSummary summary)
            : this(placements, placementCount, removalAssetIds, removalCount, null, summary)
        {
        }

        public CrossingAutoScanPlan(CrossingPlacementRecord[] placements, int placementCount, int[] removalAssetIds, int removalCount, int[] placementRemovalAssetIds, CrossingAutoScanSummary summary)
        {
            Placements = placements ?? new CrossingPlacementRecord[0];
            PlacementCount = Mathf.Clamp(placementCount, 0, Placements.Length);
            RemovalAssetIds = removalAssetIds ?? new int[0];
            RemovalCount = Mathf.Clamp(removalCount, 0, RemovalAssetIds.Length);
            PlacementRemovalAssetIds = placementRemovalAssetIds ?? new int[0];
            Summary = summary;
        }

        public bool HasWork
        {
            get { return PlacementCount > 0 || RemovalCount > 0; }
        }
    }

    public static class CrossingAutoScanPlanner
    {
        public const int MaxPlannedPlacements = 100;
        private const int MaxPlannedRemovals = MaxPlannedPlacements;
        private const int InitialObservationCandidateCapacity = 4096;
        private const int GridTraversalLimit = 65536;
        private const int NetNodeSegmentSlotCount = 8;
        private const int ContinuousRoadTraversalLimit = 128;
        private const int SnapshotPathUnitsPerSlice = 128;
        private const float JunctionPathPlacementFraction = 0.10f;
        private const float CrossingTraversalRadius = 2.75f;
        private const float CrossingTraversalDirectionDot = 0.25f;
        private const float RoadObservationCandidateSpacing = 125f;
        private const float PavementInnerTolerance = 3f;
        private const float PavementOuterTolerance = 10f;
        private const float SignalRelocationMaxDistance = 140f;
        internal const float AutoPlacementMinimumSpacing = 250f;
        private const float LongRoadObservationHalfLength = 100f;
        private const float SlowPedestrianSpeedSqr = 0.04f;
        private const float JunctionPathEndpointOffset = 96f;
        private const float JunctionPathCrossingTolerance = 4.5f;
        private const float JunctionPathVerticalTolerance = 6f;

        private const int BusyCrossingPedestrianSightingsThreshold = 6;
        private const int LongRoadPedestrianThreshold = 10;

        private static readonly float[] SurfaceCandidatePositions = new[] { 0.50f, 0.42f, 0.58f, 0.34f, 0.66f };
        private static int[] ObservationOrderBuffer = new int[InitialObservationCandidateCapacity];
        private static readonly ushort[] CorridorSegmentBuffer = new ushort[ContinuousRoadTraversalLimit];
        private static readonly bool[] CorridorForwardBuffer = new bool[ContinuousRoadTraversalLimit];
        private static readonly HashSet<ushort> PavementPedestrianIds = new HashSet<ushort>();

        private struct TrafficCounts
        {
            public int Pedestrians;
            public int CrossingPedestrians;
            public int PavementPedestriansFirstSide;
            public int PavementPedestriansSecondSide;
        }

        private enum ObservationCandidateKind
        {
            ImpactedJunction,
            ExistingSurfaceCrossing,
            ExistingSignalCrossing,
            LongRoadSegment
        }

        private struct ObservedTrafficCounts
        {
            public int Samples;
            public int PedestrianSightings;
            public int CrossingPedestrianSightings;
            public int PeakPedestrians;
            public int PeakCrossingPedestrians;
            public int PavementPedestrianFirstSideSightings;
            public int PavementPedestrianSecondSideSightings;

            public void Add(TrafficCounts counts)
            {
                Samples++;
                PedestrianSightings += counts.Pedestrians;
                CrossingPedestrianSightings += counts.CrossingPedestrians;
                PavementPedestrianFirstSideSightings += counts.PavementPedestriansFirstSide;
                PavementPedestrianSecondSideSightings += counts.PavementPedestriansSecondSide;
                PeakPedestrians = Math.Max(PeakPedestrians, counts.Pedestrians);
                PeakCrossingPedestrians = Math.Max(PeakCrossingPedestrians, counts.CrossingPedestrians);
            }

            public TrafficCounts ToUsageCounts()
            {
                TrafficCounts counts = new TrafficCounts();
                counts.Pedestrians = Math.Max(PeakPedestrians, PedestrianSightings);
                counts.CrossingPedestrians = Math.Max(PeakCrossingPedestrians, CrossingPedestrianSightings);
                counts.PavementPedestriansFirstSide = PavementPedestrianFirstSideSightings;
                counts.PavementPedestriansSecondSide = PavementPedestrianSecondSideSightings;
                return counts;
            }
        }

        private struct ObservationCandidate
        {
            public ObservationCandidateKind Kind;
            public ushort NodeId;
            public ushort SegmentId;
            public RoadPlacementRules.VanillaCrossingPoint CrossingPoint;
            public int AssetId;
            public Vector3 Center;
            public Vector3 CrossingFirst;
            public Vector3 CrossingSecond;
            public bool HasCrossingSpan;
            public CrossingPlacementRecord SuggestedPlacement;
            public CrossingPlacementPlan SuggestedPlan;
            public ushort[] CorridorSegmentIds;
            public float[] CorridorSegmentFrom;
            public float[] CorridorSegmentTo;
            public bool[] CorridorSegmentForward;
            public int CorridorSegmentCount;
            public ObservedTrafficCounts ObservedCounts;
            public int PlannedPathCrossings;
        }

        public sealed class ObservationSession
        {
            private sealed class CandidateIndexComparer : IComparer<int>
            {
                private readonly ObservationSession _owner;

                public CandidateIndexComparer(ObservationSession owner)
                {
                    _owner = owner;
                }

                public int Compare(int first, int second)
                {
                    return _owner.GetObservedScore(_owner._candidates[second])
                        .CompareTo(_owner.GetObservedScore(_owner._candidates[first]));
                }
            }

            private struct JunctionPathHit
            {
                public int CandidateIndex;
                public float Distance;

                public JunctionPathHit(int candidateIndex, float distance)
                {
                    CandidateIndex = candidateIndex;
                    Distance = distance;
                }
            }

            private const int CandidatesPerFrame = 64;
            private const int NetworkRecordsPerFrame = 512;
            private const int CitizenInstancesPerFrame = 512;
            private readonly HashSet<uint> _snapshotVisitedUnits = new HashSet<uint>();
            private int _snapshotInstanceIndex = -1;
            private uint _snapshotCitizen;
            private uint _snapshotRoot;
            private uint _snapshotNextUnit;
            private int _snapshotFirstPosition;
            private PathUnit.Position _snapshotPrevious;
            private bool _snapshotHasPrevious;
            private bool _snapshotReadAny;
            private const float SlowBatchWarningMs = 50f;
            private ObservationCandidate[] _candidates = new ObservationCandidate[InitialObservationCandidateCapacity];
            private readonly bool[] _continuousRoadSegmentsVisited = new bool[ushort.MaxValue + 1];
            private readonly HashSet<long> _junctionCandidateKeys = new HashSet<long>();
            private readonly Dictionary<ushort, List<int>> _junctionCandidateIndexesByNode =
                new Dictionary<ushort, List<int>>();
            private readonly Dictionary<long, List<Vector3>> _vanillaCrossingExclusionsByCell =
                new Dictionary<long, List<Vector3>>();
            private readonly HashSet<long> _vanillaCrossingExclusionKeys = new HashSet<long>();
            private readonly Dictionary<ushort, int> _junctionPathCitizenCounts =
                new Dictionary<ushort, int>();
            private readonly Dictionary<ushort, JunctionPathHit> _currentCitizenJunctionHits =
                new Dictionary<ushort, JunctionPathHit>();
            private int _candidateCount;
            private int _junctionCandidateCount;
            private int _actualVanillaJunctionCandidateCount;
            private int _derivedJunctionCandidateCount;
            private int _existingCrossingCandidateCount;
            private int _longRoadCandidateCount;
            private int _sampleCursor;
            private bool _sampleInProgress;
            private int _collectionPhase;
            private int _collectionCursor;
            private bool _candidatesReady;
            private int _snapshottedCitizenPaths;
            private readonly CandidateIndexComparer _candidateIndexComparer;

            public ObservationSession()
            {
                _candidateIndexComparer = new CandidateIndexComparer(this);
            }

            public bool IsComplete { get; private set; }

            public int CandidateCount
            {
                get { return _candidateCount; }
            }

            public int JunctionCandidateCount
            {
                get { return _junctionCandidateCount; }
            }

            public int SurfaceCandidateCount
            {
                get { return _existingCrossingCandidateCount; }
            }

            public int LongRoadCandidateCount
            {
                get { return _longRoadCandidateCount; }
            }

            public int SampleCount { get; private set; }

            public int ProgressPercent
            {
                get
                {
                    if (!_candidatesReady)
                        return 0;
                    if (IsComplete)
                        return 100;

                    if (_candidateCount <= 0)
                        return 99;

                    return Mathf.Clamp(
                        50 + Mathf.FloorToInt((_sampleCursor * 49f) / _candidateCount),
                        50,
                        99);
                }
            }

            public string ProgressDetail
            {
                get
                {
                    if (!_candidatesReady)
                    {
                        if (_collectionPhase == 2)
                        {
                            return "Snapshotting live pedestrian paths  •  "
                                   + _snapshottedCitizenPaths
                                   + " routed citizen"
                                   + (_snapshottedCitizenPaths == 1 ? string.Empty : "s");
                        }

                        return "Preparing "
                               + CandidateCount
                               + " observation area"
                               + (CandidateCount == 1 ? string.Empty : "s");
                    }

                    return "Snapshotting current non-junction demand  •  "
                           + _sampleCursor
                           + "/"
                           + CandidateCount
                           + " areas";
                }
            }

            public bool HasSamples
            {
                get { return SampleCount > 0; }
            }

            public bool Tick(float realTimeDelta)
            {
                if (IsComplete)
                    return true;

                if (!_candidatesReady)
                {
                    CollectCandidateBatch();
                    return false;
                }

                if (!_sampleInProgress && SampleCount == 0)
                    _sampleInProgress = true;

                if (_sampleInProgress)
                    SampleBatch();

                if (SampleCount > 0 && !_sampleInProgress)
                    IsComplete = true;

                return IsComplete;
            }

            public string ToStatusString()
            {
                if (!_candidatesReady)
                {
                    return "Scanning crossings, please wait: preparing "
                           + CandidateCount
                           + " observation area"
                           + (CandidateCount == 1 ? string.Empty : "s")
                           + ".";
                }

                return "Scanning crossings, please wait: snapshotting "
                       + CandidateCount
                       + " current-demand area"
                       + (CandidateCount == 1 ? string.Empty : "s")
                       + ".";
            }

            public CrossingAutoScanPlan BuildPlan()
            {
                AutoScanAccumulator accumulator = new AutoScanAccumulator();
                NetManager netManager = NetManager.instance;
                if (netManager == null || netManager.m_nodes == null || netManager.m_segments == null)
                {
                    accumulator.Reject("network manager is unavailable");
                    return accumulator.ToPlan();
                }

                ScanObservedImpactedJunctions(netManager, accumulator);
                ScanObservedSignalCrossings(netManager, accumulator);
                ScanObservedSurfaceCrossings(netManager, accumulator);
                ScanObservedLongRoadSegments(netManager, accumulator);

                CrossingAutoScanPlan plan = accumulator.ToPlan();
                PedestrianCrossingLog.Advanced("[PedestrianCrossingToolkit] Auto scan planned from observation: samples="
                          + SampleCount
                          + " candidates="
                          + CandidateCount
                          + " routedCitizens="
                          + _snapshottedCitizenPaths
                          + " "
                          + plan.Summary.ToLogString());
                return plan;
            }

            public void AddJunctionCandidate(
                ushort nodeId,
                ushort segmentId,
                RoadPlacementRules.VanillaCrossingPoint crossingPoint,
                bool isActualVanillaCrossing)
            {
                long candidateKey = ((long)nodeId << 16) | segmentId;
                if (!_junctionCandidateKeys.Add(candidateKey))
                    return;

                Vector3 crossingFirst;
                Vector3 crossingSecond;
                if (!TryGetJunctionCrossingSpan(
                    segmentId,
                    crossingPoint,
                    out crossingFirst,
                    out crossingSecond))
                {
                    return;
                }

                ManagerCapacity.EnsureArrayCapacity(ref _candidates, _candidateCount + 1);
                int candidateIndex = _candidateCount;
                _candidates[_candidateCount++] = new ObservationCandidate
                {
                    Kind = ObservationCandidateKind.ImpactedJunction,
                    NodeId = nodeId,
                    SegmentId = segmentId,
                    CrossingPoint = crossingPoint,
                    Center = crossingPoint.WorldPosition,
                    CrossingFirst = crossingFirst,
                    CrossingSecond = crossingSecond,
                    HasCrossingSpan = true
                };
                List<int> candidateIndexes;
                if (!_junctionCandidateIndexesByNode.TryGetValue(nodeId, out candidateIndexes))
                {
                    candidateIndexes = new List<int>(NetNodeSegmentSlotCount);
                    _junctionCandidateIndexesByNode.Add(nodeId, candidateIndexes);
                }

                candidateIndexes.Add(candidateIndex);
                _junctionCandidateCount++;
                if (isActualVanillaCrossing)
                    _actualVanillaJunctionCandidateCount++;
                else
                    _derivedJunctionCandidateCount++;
            }

            public void AddVanillaCrossingExclusion(
                ushort nodeId,
                ushort segmentId,
                Vector3 center)
            {
                long crossingKey = ((long)nodeId << 16) | segmentId;
                if (!_vanillaCrossingExclusionKeys.Add(crossingKey))
                    return;

                int cellX = Mathf.FloorToInt(center.x / AutoPlacementMinimumSpacing);
                int cellZ = Mathf.FloorToInt(center.z / AutoPlacementMinimumSpacing);
                long cellKey = MakeCrossingExclusionCellKey(cellX, cellZ);
                List<Vector3> centers;
                if (!_vanillaCrossingExclusionsByCell.TryGetValue(cellKey, out centers))
                {
                    centers = new List<Vector3>();
                    _vanillaCrossingExclusionsByCell.Add(cellKey, centers);
                }

                centers.Add(center);
            }

            private bool HasVanillaCrossingWithinHorizontalDistance(
                Vector3 center,
                float distance)
            {
                int centerCellX = Mathf.FloorToInt(center.x / AutoPlacementMinimumSpacing);
                int centerCellZ = Mathf.FloorToInt(center.z / AutoPlacementMinimumSpacing);
                int cellRadius = Mathf.Max(
                    1,
                    Mathf.CeilToInt(distance / AutoPlacementMinimumSpacing));
                float distanceSqr = distance * distance;
                for (int cellZ = centerCellZ - cellRadius; cellZ <= centerCellZ + cellRadius; cellZ++)
                {
                    for (int cellX = centerCellX - cellRadius; cellX <= centerCellX + cellRadius; cellX++)
                    {
                        List<Vector3> centers;
                        if (!_vanillaCrossingExclusionsByCell.TryGetValue(
                            MakeCrossingExclusionCellKey(cellX, cellZ),
                            out centers))
                        {
                            continue;
                        }

                        for (int i = 0; i < centers.Count; i++)
                        {
                            if (HorizontalSqrDistance(centers[i], center) < distanceSqr)
                                return true;
                        }
                    }
                }

                return false;
            }

            private static long MakeCrossingExclusionCellKey(int cellX, int cellZ)
            {
                return ((long)cellX << 32) ^ (uint)cellZ;
            }

            public void AddExistingCrossingCandidate(CrossingPlacementAsset asset)
            {
                if (asset.Id == 0 || !asset.Plan.IsValid)
                    return;

                ManagerCapacity.EnsureArrayCapacity(ref _candidates, _candidateCount + 1);
                _candidates[_candidateCount++] = new ObservationCandidate
                {
                    Kind = asset.Placement.Mode == PedestrianToolMode.SignalCrossing
                        ? ObservationCandidateKind.ExistingSignalCrossing
                        : ObservationCandidateKind.ExistingSurfaceCrossing,
                    AssetId = asset.Id,
                    SegmentId = asset.Placement.SegmentId,
                    Center = asset.Plan.Center,
                    CrossingFirst = asset.Plan.LeftEdge,
                    CrossingSecond = asset.Plan.RightEdge,
                    HasCrossingSpan = true
                };
                _existingCrossingCandidateCount++;
            }

            public bool HasVisitedContinuousRoadSegment(ushort segmentId)
            {
                return segmentId != 0 && _continuousRoadSegmentsVisited[segmentId];
            }

            public void MarkContinuousRoadSegmentVisited(ushort segmentId)
            {
                if (segmentId != 0)
                    _continuousRoadSegmentsVisited[segmentId] = true;
            }

            public void AddLongRoadCandidate(
                CrossingPlacementRecord placement,
                CrossingPlacementPlan plan,
                ushort[] corridorSegmentIds,
                float[] corridorSegmentFrom,
                float[] corridorSegmentTo,
                bool[] corridorSegmentForward,
                int corridorSegmentCount)
            {
                if (placement.SegmentId == 0
                    || !plan.IsValid)
                    return;

                if (CrossingPlacementRegistry.HasAssetWithinHorizontalDistance(
                    plan.Center,
                    AutoPlacementMinimumSpacing,
                    0))
                {
                    return;
                }

                if (HasVanillaCrossingWithinHorizontalDistance(
                    plan.Center,
                    AutoPlacementMinimumSpacing))
                {
                    return;
                }

                ManagerCapacity.EnsureArrayCapacity(ref _candidates, _candidateCount + 1);
                _candidates[_candidateCount++] = new ObservationCandidate
                {
                    Kind = ObservationCandidateKind.LongRoadSegment,
                    SegmentId = placement.SegmentId,
                    Center = plan.Center,
                    SuggestedPlacement = placement,
                    SuggestedPlan = plan,
                    CorridorSegmentIds = corridorSegmentIds,
                    CorridorSegmentFrom = corridorSegmentFrom,
                    CorridorSegmentTo = corridorSegmentTo,
                    CorridorSegmentForward = corridorSegmentForward,
                    CorridorSegmentCount = corridorSegmentCount
                };
                _longRoadCandidateCount++;
            }

            private int BuildCandidateOrder(ObservationCandidateKind kind)
            {
                ManagerCapacity.EnsureArrayCapacity(ref ObservationOrderBuffer, _candidateCount);
                int count = 0;
                for (int i = 0; i < _candidateCount && count < ObservationOrderBuffer.Length; i++)
                {
                    if (_candidates[i].Kind == kind)
                        ObservationOrderBuffer[count++] = i;
                }

                Array.Sort(ObservationOrderBuffer, 0, count, _candidateIndexComparer);

                return count;
            }

            private float GetObservedScore(ObservationCandidate candidate)
            {
                TrafficCounts counts = candidate.ObservedCounts.ToUsageCounts();
                switch (candidate.Kind)
                {
                    case ObservationCandidateKind.ImpactedJunction:
                        return candidate.PlannedPathCrossings;
                    case ObservationCandidateKind.ExistingSurfaceCrossing:
                    case ObservationCandidateKind.ExistingSignalCrossing:
                        return counts.CrossingPedestrians;
                    case ObservationCandidateKind.LongRoadSegment:
                        return Math.Max(
                            counts.PavementPedestriansFirstSide,
                            counts.PavementPedestriansSecondSide);
                    default:
                        return 0f;
                }
            }

            private void ScanObservedImpactedJunctions(NetManager netManager, AutoScanAccumulator accumulator)
            {
                int count = BuildCandidateOrder(ObservationCandidateKind.ImpactedJunction);
                int junctionPlacementBudget = Mathf.Clamp(
                    Mathf.FloorToInt(CandidateCount * JunctionPathPlacementFraction),
                    0,
                    MaxPlannedPlacements);
                int placedJunctionCrossings = 0;
                for (int i = 0; i < count && (placedJunctionCrossings < junctionPlacementBudget || !accumulator.HasPlacementCapacity()); i++)
                {
                    ObservationCandidate candidate = _candidates[ObservationOrderBuffer[i]];
                    accumulator.ScannedNodes++;
                    if (candidate.PlannedPathCrossings <= 0)
                        continue;

                    accumulator.Hotspots++;

                    CrossingPlacementRecord placement;
                    CrossingPlacementPlan plan;
                    if (TryCreateGradeSeparatedJunctionPlacement(
                            candidate.NodeId,
                            candidate.SegmentId,
                            candidate.CrossingPoint.WorldPosition,
                            accumulator,
                            out placement,
                            out plan))
                    {
                        if (!accumulator.TryAddPathRankedJunctionPlacement(placement, plan))
                            continue;
                        placedJunctionCrossings++;
                        PedestrianCrossingLog.Advanced(
                            "[PedestrianCrossingToolkit] Auto scan junction proposal accepted: node="
                            + candidate.NodeId
                            + " segment="
                            + candidate.SegmentId
                            + " mode="
                            + placement.Mode
                            + " routedCitizens="
                            + candidate.PlannedPathCrossings
                            + " position="
                            + FormatVector(candidate.CrossingPoint.WorldPosition));
                    }
                    else
                    {
                        accumulator.Reject("no legal subway or bridge placement found at path-ranked junction arm");
                    }
                }
            }

            private void ScanObservedLongRoadSegments(NetManager netManager, AutoScanAccumulator accumulator)
            {
                ScanLongRoadCandidates(netManager, accumulator, false);
            }

            internal void ScanImmediateLongRoadSegments(NetManager netManager, AutoScanAccumulator accumulator)
            {
                ScanLongRoadCandidates(netManager, accumulator, true);
            }

            private void ScanLongRoadCandidates(
                NetManager netManager,
                AutoScanAccumulator accumulator,
                bool sampleImmediately)
            {
                int count = BuildCandidateOrder(ObservationCandidateKind.LongRoadSegment);
                for (int i = 0; i < count; i++)
                {
                    ObservationCandidate candidate = _candidates[ObservationOrderBuffer[i]];
                    if (candidate.SegmentId == 0 || candidate.SegmentId >= netManager.m_segments.m_size)
                        continue;

                    ref NetSegment segment = ref netManager.m_segments.m_buffer[candidate.SegmentId];
                    if ((segment.m_flags & NetSegment.Flags.Created) == 0
                        || !RoadPlacementRules.AllowsSurfaceCrossing(candidate.SegmentId)
                        || !candidate.SuggestedPlan.IsValid)
                    {
                        continue;
                    }

                    accumulator.ScannedLongRoadSegments++;
                    TrafficCounts counts = sampleImmediately
                        ? CountPavementPedestrians(candidate)
                        : candidate.ObservedCounts.ToUsageCounts();
                    if (counts.PavementPedestriansFirstSide < LongRoadPedestrianThreshold
                        && counts.PavementPedestriansSecondSide < LongRoadPedestrianThreshold)
                        continue;

                    accumulator.Hotspots++;

                    if (!accumulator.TryAddPlacement(
                        candidate.SuggestedPlacement,
                        candidate.SuggestedPlan))
                    {
                        accumulator.Reject("straight-road crossing was no longer safely placeable");
                    }
                }
            }

            private void ScanObservedSurfaceCrossings(NetManager netManager, AutoScanAccumulator accumulator)
            {
                int count = BuildCandidateOrder(ObservationCandidateKind.ExistingSurfaceCrossing);
                for (int i = 0; i < count; i++)
                {
                    ObservationCandidate candidate = _candidates[ObservationOrderBuffer[i]];
                    CrossingPlacementAsset asset;
                    if (!CrossingPlacementRegistry.TryGetAssetById(candidate.AssetId, out asset)
                        || asset.Placement.Mode != PedestrianToolMode.MidBlockCrossing
                        || !asset.Plan.IsValid)
                    {
                        continue;
                    }

                    accumulator.ScannedExistingCrossings++;
                    TrafficCounts counts = candidate.ObservedCounts.ToUsageCounts();
                    if (counts.CrossingPedestrians < BusyCrossingPedestrianSightingsThreshold)
                        continue;

                    accumulator.Hotspots++;

                    CrossingPlacementRecord signalPlacement;
                    CrossingPlacementPlan signalPlan;
                    if (!TryFindLegalSignalReplacement(netManager, asset, out signalPlacement, out signalPlan))
                    {
                        accumulator.Reject("no legal nearby signal join found for observed busy surface crossing");
                        continue;
                    }

                    accumulator.TryAddUpgrade(signalPlacement, signalPlan, asset.Id);
                }
            }

            private void ScanObservedSignalCrossings(NetManager netManager, AutoScanAccumulator accumulator)
            {
                int count = BuildCandidateOrder(ObservationCandidateKind.ExistingSignalCrossing);
                for (int i = 0; i < count; i++)
                {
                    ObservationCandidate candidate = _candidates[ObservationOrderBuffer[i]];
                    CrossingPlacementAsset asset;
                    if (!CrossingPlacementRegistry.TryGetAssetById(candidate.AssetId, out asset)
                        || asset.Placement.Mode != PedestrianToolMode.SignalCrossing
                        || !asset.Plan.IsValid)
                    {
                        continue;
                    }

                    accumulator.ScannedExistingCrossings++;
                    TrafficCounts counts = candidate.ObservedCounts.ToUsageCounts();
                    if (counts.CrossingPedestrians < BusyCrossingPedestrianSightingsThreshold)
                        continue;

                    accumulator.Hotspots++;

                    CrossingPlacementRecord gradePlacement;
                    CrossingPlacementPlan gradePlan;
                    ushort nodeId = asset.Plan.TargetNodeId != 0
                        ? asset.Plan.TargetNodeId
                        : asset.Placement.TargetNodeId;
                    if (nodeId == 0
                        || !TryCreateGradeSeparatedJunctionPlacement(
                            nodeId,
                            asset.Placement.SegmentId,
                            asset.Plan.Center,
                            accumulator,
                            out gradePlacement,
                            out gradePlan))
                    {
                        accumulator.Reject("no legal subway or bridge placement found for observed busy signal crossing");
                        continue;
                    }

                    accumulator.TryAddUpgrade(gradePlacement, gradePlan, asset.Id);
                }
            }

            private void SampleBatch()
            {
                float startedAt = Time.realtimeSinceStartup;
                int end = Math.Min(_candidateCount, _sampleCursor + CandidatesPerFrame);
                for (int i = _sampleCursor; i < end; i++)
                {
                    ObservationCandidate candidate = _candidates[i];
                    if (candidate.Kind == ObservationCandidateKind.ImpactedJunction)
                        continue;

                    TrafficCounts counts = candidate.HasCrossingSpan
                        ? CountPedestriansTraversingCrossing(
                            candidate.CrossingFirst,
                            candidate.CrossingSecond)
                        : CountPavementPedestrians(candidate);
                    candidate.ObservedCounts.Add(counts);
                    _candidates[i] = candidate;
                }

                _sampleCursor = end;
                WarnIfSlowBatch("observation", startedAt, _sampleCursor, _candidateCount);
                if (_sampleCursor < _candidateCount)
                    return;

                _sampleCursor = 0;
                _sampleInProgress = false;
                SampleCount++;
                PedestrianCrossingLog.Advanced("[PedestrianCrossingToolkit] Auto scan current-demand snapshot complete: samples="
                          + SampleCount
                          + " candidates="
                          + CandidateCount);
            }

            private void CollectCandidateBatch()
            {
                float startedAt = Time.realtimeSinceStartup;
                NetManager netManager = NetManager.instance;
                if (netManager == null || netManager.m_nodes == null || netManager.m_segments == null)
                {
                    _candidatesReady = true;
                    return;
                }

                if (_collectionPhase == 0)
                {
                    if (CollectSurfaceObservationCandidates(
                        this,
                        ref _collectionCursor,
                        NetworkRecordsPerFrame))
                    {
                        _collectionPhase = 1;
                        _collectionCursor = 1;
                    }
                }
                else if (_collectionPhase == 1)
                {
                    if (CollectJunctionObservationCandidates(netManager, this, ref _collectionCursor, NetworkRecordsPerFrame))
                    {
                        _collectionPhase = 2;
                        _collectionCursor = 1;
                    }
                }
                else if (_collectionPhase == 2)
                {
                    if (SnapshotCitizenPathBatch(ref _collectionCursor))
                    {
                        _collectionPhase = 3;
                        _collectionCursor = 1;
                    }
                }
                else if (_collectionPhase == 3)
                {
                    if (CollectLongRoadObservationCandidates(netManager, this, ref _collectionCursor, NetworkRecordsPerFrame))
                        _collectionPhase = 4;
                }

                if (_collectionPhase < 4)
                {
                    WarnIfSlowBatch("candidate-collection", startedAt, _collectionCursor, 0);
                    return;
                }

                _candidatesReady = true;
                WarnIfSlowBatch("candidate-collection", startedAt, _collectionCursor, 0);
                PedestrianCrossingLog.Advanced("[PedestrianCrossingToolkit] Auto scan candidate collection complete: candidates="
                          + CandidateCount
                          + " junctionCandidates="
                          + JunctionCandidateCount
                          + " actualVanillaJunctionCandidates="
                          + _actualVanillaJunctionCandidateCount
                          + " derivedJunctionCandidates="
                          + _derivedJunctionCandidateCount
                          + " surfaceCandidates="
                          + SurfaceCandidateCount
                          + " longRoadCandidates="
                          + LongRoadCandidateCount
                          + " routedCitizens="
                          + _snapshottedCitizenPaths
                          + " pathRankedJunctions="
                          + _junctionPathCitizenCounts.Count);
            }

            private bool SnapshotCitizenPathBatch(ref int nextInstanceIndex)
            {
                CitizenManager citizenManager = CitizenManager.instance;
                PathManager pathManager = PathManager.instance;
                NetManager netManager = NetManager.instance;
                if (citizenManager == null
                    || pathManager == null
                    || netManager == null
                    || citizenManager.m_instances == null
                    || citizenManager.m_instances.m_buffer == null
                    || pathManager.m_pathUnits == null
                    || pathManager.m_pathUnits.m_buffer == null)
                {
                    return true;
                }

                CitizenInstance[] instances = citizenManager.m_instances.m_buffer;
                int limit = Math.Min((int)citizenManager.m_instances.m_size, instances.Length);
                int end = Math.Min(limit, nextInstanceIndex + CitizenInstancesPerFrame);
                for (int instanceIndex = nextInstanceIndex; instanceIndex < end; instanceIndex++)
                {
                    CitizenInstance instance = instances[instanceIndex];
                    if (!IsPedestrianCandidate(instance)
                        || instance.m_path == 0u
                        || instance.Info == null
                        || !(instance.Info.m_citizenAI is HumanAI))
                    {
                        continue;
                    }

                    bool complete;
                    if (SnapshotCitizenPath(instanceIndex, instance, pathManager, netManager, out complete))
                        _snapshottedCitizenPaths++;
                    if (!complete)
                    {
                        nextInstanceIndex = instanceIndex;
                        return false;
                    }
                }

                nextInstanceIndex = end;
                return nextInstanceIndex >= limit;
            }

            private bool SnapshotCitizenPath(
                int instanceIndex,
                CitizenInstance instance,
                PathManager pathManager,
                NetManager netManager,
                out bool complete)
            {
                complete = true;
                if (_snapshotInstanceIndex == instanceIndex
                    && (_snapshotRoot != instance.m_path || _snapshotCitizen != instance.m_citizen))
                {
                    // Do not combine two routes if vanilla changed ownership between slices.
                    _snapshotInstanceIndex = -1;
                    _snapshotVisitedUnits.Clear();
                    _currentCitizenJunctionHits.Clear();
                    return false;
                }
                if (_snapshotInstanceIndex != instanceIndex)
                {
                    _snapshotInstanceIndex = instanceIndex;
                    _snapshotCitizen = instance.m_citizen;
                    _snapshotRoot = instance.m_path;
                    _snapshotNextUnit = instance.m_path;
                    _snapshotFirstPosition = instance.m_pathPositionIndex >> 1;
                    _snapshotHasPrevious = false;
                    _snapshotReadAny = false;
                    _snapshotVisitedUnits.Clear();
                    _currentCitizenJunctionHits.Clear();
                }

                int units = 0;
                uint limit = Math.Min(pathManager.m_pathUnits.m_size,
                    (uint)pathManager.m_pathUnits.m_buffer.Length);
                while (_snapshotNextUnit != 0u && units++ < SnapshotPathUnitsPerSlice)
                {
                    uint id = _snapshotNextUnit;
                    if (id >= limit || !_snapshotVisitedUnits.Add(id))
                    {
                        _snapshotNextUnit = 0;
                        break;
                    }
                    PathUnit pathUnit = pathManager.m_pathUnits.m_buffer[id];
                    if ((pathUnit.m_pathFindFlags & PathUnit.FLAG_READY) == 0
                        || (pathUnit.m_pathFindFlags & PathUnit.FLAG_FAILED) != 0)
                    {
                        _snapshotNextUnit = 0;
                        break;
                    }
                    int count = Math.Min(pathUnit.m_positionCount, (byte)PathUnit.MAX_POSITIONS);
                    for (int i = Mathf.Clamp(_snapshotFirstPosition, 0, count); i < count; i++)
                    {
                        PathUnit.Position current;
                        if (!pathUnit.GetPosition(i, out current) || !IsPedestrianPathPosition(current, netManager))
                        {
                            _snapshotHasPrevious = false;
                            continue;
                        }
                        _snapshotReadAny = true;
                        if (_snapshotHasPrevious)
                            RecordJunctionPathCrossing(_snapshotPrevious, current, netManager);
                        _snapshotPrevious = current;
                        _snapshotHasPrevious = true;
                    }
                    _snapshotNextUnit = pathUnit.m_nextPathUnit;
                    _snapshotFirstPosition = 0;
                }
                if (_snapshotNextUnit != 0u)
                {
                    complete = false;
                    return false;
                }
                foreach (KeyValuePair<ushort, JunctionPathHit> pair in _currentCitizenJunctionHits)
                {
                    int count;
                    _junctionPathCitizenCounts.TryGetValue(pair.Key, out count);
                    _junctionPathCitizenCounts[pair.Key] = count + 1;
                    ObservationCandidate candidate = _candidates[pair.Value.CandidateIndex];
                    candidate.PlannedPathCrossings++;
                    _candidates[pair.Value.CandidateIndex] = candidate;
                }
                _snapshotInstanceIndex = -1;
                _snapshotVisitedUnits.Clear();
                _currentCitizenJunctionHits.Clear();
                return _snapshotReadAny;
            }

            private void RecordJunctionPathCrossing(
                PathUnit.Position first,
                PathUnit.Position second,
                NetManager netManager)
            {
                ushort nodeId;
                if (!TryGetSharedPathJunction(first, second, netManager, out nodeId))
                    return;

                List<int> candidateIndexes;
                if (!_junctionCandidateIndexesByNode.TryGetValue(nodeId, out candidateIndexes))
                    return;

                Vector3 firstWorld = PathManager.CalculatePosition(first);
                Vector3 secondWorld = PathManager.CalculatePosition(second);
                if (!IsFinite(firstWorld) || !IsFinite(secondWorld))
                    return;

                int bestCandidateIndex = -1;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < candidateIndexes.Count; i++)
                {
                    int candidateIndex = candidateIndexes[i];
                    ObservationCandidate candidate = _candidates[candidateIndex];
                    float distance;
                    if (TryMeasurePlannedCrossing(
                            firstWorld,
                            secondWorld,
                            candidate.CrossingFirst,
                            candidate.CrossingSecond,
                            out distance)
                        && distance < bestDistance)
                    {
                        bestCandidateIndex = candidateIndex;
                        bestDistance = distance;
                    }
                }

                if (bestCandidateIndex < 0)
                    return;

                JunctionPathHit existing;
                if (!_currentCitizenJunctionHits.TryGetValue(nodeId, out existing)
                    || bestDistance < existing.Distance)
                {
                    _currentCitizenJunctionHits[nodeId] =
                        new JunctionPathHit(bestCandidateIndex, bestDistance);
                }
            }

            private static bool IsPedestrianPathPosition(
                PathUnit.Position position,
                NetManager netManager)
            {
                if (position.m_segment == 0
                    || position.m_segment >= netManager.m_segments.m_size)
                {
                    return false;
                }

                ref NetSegment segment = ref netManager.m_segments.m_buffer[position.m_segment];
                NetInfo info = segment.Info;
                if ((segment.m_flags & NetSegment.Flags.Created) == 0
                    || info == null
                    || info.m_lanes == null
                    || position.m_lane >= info.m_lanes.Length)
                {
                    return false;
                }

                NetInfo.Lane lane = info.m_lanes[position.m_lane];
                return lane != null
                       && (lane.m_laneType & NetInfo.LaneType.Pedestrian) != 0;
            }

            private static bool TryGetSharedPathJunction(
                PathUnit.Position first,
                PathUnit.Position second,
                NetManager netManager,
                out ushort nodeId)
            {
                nodeId = 0;
                if (first.m_segment == 0
                    || second.m_segment == 0
                    || first.m_segment >= netManager.m_segments.m_size
                    || second.m_segment >= netManager.m_segments.m_size)
                {
                    return false;
                }

                ref NetSegment firstSegment = ref netManager.m_segments.m_buffer[first.m_segment];
                ref NetSegment secondSegment = ref netManager.m_segments.m_buffer[second.m_segment];
                if (first.m_segment == second.m_segment)
                {
                    if (first.m_lane == second.m_lane)
                        return false;

                    if (first.m_offset <= JunctionPathEndpointOffset
                        && second.m_offset <= JunctionPathEndpointOffset)
                    {
                        nodeId = firstSegment.m_startNode;
                    }
                    else if (first.m_offset >= byte.MaxValue - JunctionPathEndpointOffset
                             && second.m_offset >= byte.MaxValue - JunctionPathEndpointOffset)
                    {
                        nodeId = firstSegment.m_endNode;
                    }
                }
                else
                {
                    if (firstSegment.m_startNode != 0
                        && (firstSegment.m_startNode == secondSegment.m_startNode
                            || firstSegment.m_startNode == secondSegment.m_endNode))
                    {
                        nodeId = firstSegment.m_startNode;
                    }
                    else if (firstSegment.m_endNode != 0
                             && (firstSegment.m_endNode == secondSegment.m_startNode
                                 || firstSegment.m_endNode == secondSegment.m_endNode))
                    {
                        nodeId = firstSegment.m_endNode;
                    }

                    if (nodeId != 0
                        && (!IsPathPositionNearNode(first, ref firstSegment, nodeId)
                            || !IsPathPositionNearNode(second, ref secondSegment, nodeId)))
                    {
                        nodeId = 0;
                    }
                }

                return nodeId != 0
                       && RoadPlacementRules.IsThreePlusJunctionNode(nodeId);
            }

            private static bool IsPathPositionNearNode(
                PathUnit.Position position,
                ref NetSegment segment,
                ushort nodeId)
            {
                return (segment.m_startNode == nodeId
                        && position.m_offset <= JunctionPathEndpointOffset)
                       || (segment.m_endNode == nodeId
                           && position.m_offset >= byte.MaxValue - JunctionPathEndpointOffset);
            }

            private static bool TryMeasurePlannedCrossing(
                Vector3 pathFirst,
                Vector3 pathSecond,
                Vector3 crossingFirst,
                Vector3 crossingSecond,
                out float distance)
            {
                distance = float.MaxValue;
                Vector3 pathDirection = pathSecond - pathFirst;
                Vector3 crossingDirection = crossingSecond - crossingFirst;
                pathDirection.y = 0f;
                crossingDirection.y = 0f;
                if (pathDirection.sqrMagnitude <= 0.01f
                    || crossingDirection.sqrMagnitude <= 0.01f)
                {
                    return false;
                }

                pathDirection.Normalize();
                crossingDirection.Normalize();
                if (Mathf.Abs(Vector3.Dot(pathDirection, crossingDirection))
                    < CrossingTraversalDirectionDot)
                {
                    return false;
                }

                float pathHeight = (pathFirst.y + pathSecond.y) * 0.5f;
                float crossingHeight = (crossingFirst.y + crossingSecond.y) * 0.5f;
                if (Mathf.Abs(pathHeight - crossingHeight) > JunctionPathVerticalTolerance)
                    return false;

                float unused;
                distance = Mathf.Min(
                    Mathf.Min(
                        DistanceToSegment2D(pathFirst, crossingFirst, crossingSecond, out unused),
                        DistanceToSegment2D(pathSecond, crossingFirst, crossingSecond, out unused)),
                    Mathf.Min(
                        DistanceToSegment2D(crossingFirst, pathFirst, pathSecond, out unused),
                        DistanceToSegment2D(crossingSecond, pathFirst, pathSecond, out unused)));
                if (SegmentsIntersect2D(pathFirst, pathSecond, crossingFirst, crossingSecond))
                    distance = 0f;

                return distance <= JunctionPathCrossingTolerance;
            }

            private static bool SegmentsIntersect2D(
                Vector3 firstStart,
                Vector3 firstEnd,
                Vector3 secondStart,
                Vector3 secondEnd)
            {
                Vector2 p = new Vector2(firstStart.x, firstStart.z);
                Vector2 r = new Vector2(firstEnd.x - firstStart.x, firstEnd.z - firstStart.z);
                Vector2 q = new Vector2(secondStart.x, secondStart.z);
                Vector2 s = new Vector2(secondEnd.x - secondStart.x, secondEnd.z - secondStart.z);
                float denominator = (r.x * s.y) - (r.y * s.x);
                if (Mathf.Abs(denominator) <= 0.0001f)
                    return false;

                Vector2 difference = q - p;
                float firstAlong = ((difference.x * s.y) - (difference.y * s.x)) / denominator;
                float secondAlong = ((difference.x * r.y) - (difference.y * r.x)) / denominator;
                return firstAlong >= 0f
                       && firstAlong <= 1f
                       && secondAlong >= 0f
                       && secondAlong <= 1f;
            }

            private static void WarnIfSlowBatch(string stage, float startedAt, int cursor, int total)
            {
                float elapsedMs = (Time.realtimeSinceStartup - startedAt) * 1000f;
                if (elapsedMs < SlowBatchWarningMs)
                    return;

                PedestrianCrossingLog.AdvancedWarning("Slow Auto Scan slice: stage="
                                 + stage
                                 + " elapsedMs="
                                 + elapsedMs.ToString("0.0")
                                 + " cursor="
                                 + cursor
                                 + (total > 0 ? "/" + total : string.Empty));
            }
        }

        private struct SignalNodeCandidate
        {
            public ushort NodeId;
            public float DistanceSqr;

            public SignalNodeCandidate(ushort nodeId, float distanceSqr)
            {
                NodeId = nodeId;
                DistanceSqr = distanceSqr;
            }
        }

        internal sealed class AutoScanAccumulator
        {
            public readonly CrossingPlacementRecord[] Placements = new CrossingPlacementRecord[MaxPlannedPlacements];
            public readonly Vector3[] PlacementCenters = new Vector3[MaxPlannedPlacements];
            public readonly int[] RemovalAssetIds = new int[MaxPlannedRemovals];
            public readonly int[] PlacementRemovalAssetIds = new int[MaxPlannedPlacements];
            public int PlacementCount;
            public int RemovalCount;
            public int ScannedNodes;
            public int ScannedExistingCrossings;
            public int ScannedLongRoadSegments;
            public int Hotspots;
            public int GradeSeparatedPlacements;
            public int SignalPlacements;
            public int SurfacePlacements;
            public int SkippedExisting;
            public int Rejected;
            public int Capped;
            public string FirstRejection = string.Empty;

            public CrossingAutoScanPlan ToPlan()
            {
                CrossingAutoScanSummary summary = new CrossingAutoScanSummary(
                    ScannedNodes,
                    ScannedExistingCrossings,
                    ScannedLongRoadSegments,
                    Hotspots,
                    PlacementCount,
                    RemovalCount,
                    GradeSeparatedPlacements,
                    SignalPlacements,
                    SurfacePlacements,
                    SkippedExisting,
                    Rejected,
                    Capped,
                    FirstRejection);

                return new CrossingAutoScanPlan(Placements, PlacementCount, RemovalAssetIds, RemovalCount, PlacementRemovalAssetIds, summary);
            }

            public bool HasPlacementCapacity()
            {
                return PlacementCount < Placements.Length;
            }

            public bool TryAddPlacement(CrossingPlacementRecord placement, CrossingPlacementPlan plan)
            {
                return TryAddPlacement(placement, plan, 0);
            }

            public bool TryAddPathRankedJunctionPlacement(
                CrossingPlacementRecord placement,
                CrossingPlacementPlan plan)
            {
                return TryAddPlacement(placement, plan, 0, true);
            }

            public bool TryAddPlacement(
                CrossingPlacementRecord placement,
                CrossingPlacementPlan plan,
                int ignoredExistingAssetId)
            {
                return TryAddPlacement(placement, plan, ignoredExistingAssetId, false);
            }

            private bool TryAddPlacement(
                CrossingPlacementRecord placement,
                CrossingPlacementPlan plan,
                int ignoredExistingAssetId,
                bool allowDistinctJunctionArms)
            {
                if (!placement.IsValid || !plan.IsValid)
                {
                    Reject(string.IsNullOrEmpty(placement.Message) ? "candidate placement is invalid" : placement.Message);
                    return false;
                }

                if (IsGradeSeparatedMode(placement.Mode)
                    && IsAutoGradeSeparatedThroatCovered(placement, plan, Placements, PlacementCount))
                {
                    SkippedExisting++;
                    return false;
                }

                if (CrossingPlacementRegistry.HasSameModeAssetAt(placement) || HasSamePlannedModePlacement(placement))
                {
                    SkippedExisting++;
                    return false;
                }

                Vector3 center = plan.IsValid ? plan.Center : placement.WorldPosition;
                if (HasBlockingExistingAssetWithinDistance(
                        center,
                        AutoPlacementMinimumSpacing,
                        ignoredExistingAssetId,
                        placement,
                        plan,
                        allowDistinctJunctionArms)
                    || HasPlannedPlacementWithinDistance(
                        center,
                        AutoPlacementMinimumSpacing,
                        placement,
                        allowDistinctJunctionArms))
                {
                    SkippedExisting++;
                    return false;
                }

                if (PlacementCount >= Placements.Length)
                {
                    Capped++;
                    return false;
                }

                PlacementCenters[PlacementCount] = center;
                Placements[PlacementCount++] = placement;
                switch (placement.Mode)
                {
                    case PedestrianToolMode.SignalCrossing:
                        SignalPlacements++;
                        break;
                    case PedestrianToolMode.MidBlockCrossing:
                        SurfacePlacements++;
                        break;
                    case PedestrianToolMode.SubwayLink:
                    case PedestrianToolMode.PedestrianBridge:
                        GradeSeparatedPlacements++;
                        break;
                }

                return true;
            }

            public bool TryAddUpgrade(
                CrossingPlacementRecord placement,
                CrossingPlacementPlan plan,
                int removalAssetId)
            {
                if (removalAssetId == 0 || HasRemoval(removalAssetId))
                    return false;

                if (!TryAddPlacement(placement, plan, removalAssetId))
                    return false;

                return TryAddRemoval(removalAssetId);
            }

            public bool TryAddRemoval(int assetId)
            {
                if (assetId == 0 || HasRemoval(assetId))
                    return false;

                if (RemovalCount >= RemovalAssetIds.Length)
                {
                    Capped++;
                    return false;
                }

                RemovalAssetIds[RemovalCount++] = assetId;
                if (PlacementCount > 0 && PlacementCount - 1 < PlacementRemovalAssetIds.Length)
                    PlacementRemovalAssetIds[PlacementCount - 1] = assetId;
                return true;
            }

            public void Reject(string reason)
            {
                Rejected++;
                if (string.IsNullOrEmpty(FirstRejection) && !string.IsNullOrEmpty(reason))
                    FirstRejection = reason;
            }

            private bool HasSamePlannedModePlacement(CrossingPlacementRecord placement)
            {
                for (int i = 0; i < PlacementCount; i++)
                {
                    CrossingPlacementRecord existing = Placements[i];
                    if (existing.Mode == placement.Mode && CrossingPlacementRegistry.IsSamePlacementLocation(existing, placement))
                        return true;
                }

                return false;
            }

            private bool HasPlannedPlacementWithinDistance(
                Vector3 center,
                float distance,
                CrossingPlacementRecord placement,
                bool allowDistinctJunctionArms)
            {
                float distanceSqr = distance * distance;
                for (int i = 0; i < PlacementCount; i++)
                {
                    if (HorizontalSqrDistance(PlacementCenters[i], center) >= distanceSqr)
                        continue;

                    if (allowDistinctJunctionArms
                        && AreDistinctGradeSeparatedJunctionArms(
                            Placements[i],
                            CrossingPlacementPlan.Invalid,
                            placement,
                            CrossingPlacementPlan.Invalid))
                    {
                        continue;
                    }

                    return true;
                }

                return false;
            }

            private bool HasRemoval(int assetId)
            {
                for (int i = 0; i < RemovalCount; i++)
                {
                    if (RemovalAssetIds[i] == assetId)
                        return true;
                }

                return false;
            }
        }

        private static bool HasBlockingExistingAssetWithinDistance(
            Vector3 center,
            float distance,
            int ignoredExistingAssetId,
            CrossingPlacementRecord placement,
            CrossingPlacementPlan plan,
            bool allowDistinctJunctionArms)
        {
            float distanceSqr = distance * distance;
            int existingCount = CrossingPlacementRegistry.Count;
            for (int i = 0; i < existingCount; i++)
            {
                CrossingPlacementAsset asset;
                if (!CrossingPlacementRegistry.TryGetAssetAtIndex(i, out asset)
                    || asset.Id == 0
                    || asset.Id == ignoredExistingAssetId)
                {
                    continue;
                }

                Vector3 existingCenter = asset.Plan.IsValid
                    ? asset.Plan.Center
                    : asset.Placement.WorldPosition;
                if (HorizontalSqrDistance(existingCenter, center) >= distanceSqr)
                    continue;

                if (allowDistinctJunctionArms
                    && AreDistinctGradeSeparatedJunctionArms(
                        asset.Placement,
                        asset.Plan,
                        placement,
                        plan))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool AreDistinctGradeSeparatedJunctionArms(
            CrossingPlacementRecord first,
            CrossingPlacementPlan firstPlan,
            CrossingPlacementRecord second,
            CrossingPlacementPlan secondPlan)
        {
            if (!IsGradeSeparatedMode(first.Mode)
                || !IsGradeSeparatedMode(second.Mode))
            {
                return false;
            }

            ushort firstNodeId = GetGradeSeparatedTargetNode(first, firstPlan);
            ushort secondNodeId = GetGradeSeparatedTargetNode(second, secondPlan);
            return firstNodeId != 0
                   && firstNodeId == secondNodeId
                   && !IsSamePlacementThroat(first, second);
        }

        private static bool IsAutoGradeSeparatedThroatCovered(
            CrossingPlacementRecord placement,
            CrossingPlacementPlan plan,
            CrossingPlacementRecord[] plannedPlacements,
            int plannedCount)
        {
            ushort targetNodeId = GetGradeSeparatedTargetNode(placement, plan);
            if (targetNodeId == 0)
                return false;

            int existingCount = CrossingPlacementRegistry.Count;
            for (int i = 0; i < existingCount; i++)
            {
                CrossingPlacementAsset asset;
                if (!CrossingPlacementRegistry.TryGetAssetAtIndex(i, out asset))
                    continue;

                if (asset.Id == 0 || !IsGradeSeparatedMode(asset.Placement.Mode))
                    continue;

                if (GetGradeSeparatedTargetNode(asset.Placement, asset.Plan) == targetNodeId
                    && IsSamePlacementThroat(asset.Placement, placement))
                {
                    return true;
                }
            }

            if (plannedPlacements == null)
                return false;

            for (int i = 0; i < plannedCount && i < plannedPlacements.Length; i++)
            {
                CrossingPlacementRecord existing = plannedPlacements[i];
                if (!IsGradeSeparatedMode(existing.Mode))
                    continue;

                if (GetGradeSeparatedTargetNode(existing, CrossingPlacementPlan.Invalid) == targetNodeId
                    && IsSamePlacementThroat(existing, placement))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSamePlacementThroat(CrossingPlacementRecord existing, CrossingPlacementRecord candidate)
        {
            if (existing.SegmentId == 0 || candidate.SegmentId == 0)
                return false;

            if (existing.SegmentId == candidate.SegmentId)
                return true;

            if (existing.HasSecondaryPoint && existing.SecondarySegmentId == candidate.SegmentId)
                return true;

            return candidate.HasSecondaryPoint && candidate.SecondarySegmentId == existing.SegmentId;
        }

        private static ushort GetGradeSeparatedTargetNode(CrossingPlacementRecord placement, CrossingPlacementPlan plan)
        {
            if (plan.IsValid && plan.TargetNodeId != 0)
                return plan.TargetNodeId;

            if (placement.TargetNodeId != 0)
                return placement.TargetNodeId;

            return 0;
        }

        private static bool IsGradeSeparatedMode(PedestrianToolMode mode)
        {
            return mode == PedestrianToolMode.SubwayLink
                   || mode == PedestrianToolMode.SubwayPointToPoint
                   || mode == PedestrianToolMode.PedestrianBridge;
        }

        private static bool IsSubwayMode(PedestrianToolMode mode)
        {
            return mode == PedestrianToolMode.SubwayLink
                   || mode == PedestrianToolMode.SubwayPointToPoint;
        }

        private static bool TryGetStraightRoadContinuation(
            NetManager netManager,
            ushort currentSegmentId,
            ref NetSegment currentSegment,
            ushort nodeId,
            out ushort continuationSegmentId)
        {
            continuationSegmentId = 0;
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return false;

            ushort otherSegmentId = 0;
            int roadSegmentCount = 0;
            for (int i = 0; i < NetNodeSegmentSlotCount; i++)
            {
                ushort candidateSegmentId = node.GetSegment(i);
                if (candidateSegmentId == 0)
                    continue;

                NetSegment candidateSegment;
                if (!TryGetCreatedRoadSegment(netManager, candidateSegmentId, out candidateSegment))
                    continue;

                roadSegmentCount++;
                if (candidateSegmentId != currentSegmentId)
                    otherSegmentId = candidateSegmentId;
            }

            if (roadSegmentCount != 2 || otherSegmentId == 0)
                return false;

            NetSegment otherSegment;
            if (!TryGetCreatedRoadSegment(netManager, otherSegmentId, out otherSegment))
                return false;

            Vector3 currentDirection;
            Vector3 otherDirection;
            if (!TryGetDirectionAwayFromNode(netManager, ref currentSegment, nodeId, out currentDirection)
                || !TryGetDirectionAwayFromNode(netManager, ref otherSegment, nodeId, out otherDirection))
            {
                return false;
            }

            if (Vector3.Dot(currentDirection, otherDirection) > -0.75f)
                return false;

            continuationSegmentId = otherSegmentId;
            return true;
        }

        private static bool TryGetCreatedRoadSegment(NetManager netManager, ushort segmentId, out NetSegment segment)
        {
            segment = default(NetSegment);
            if (netManager == null || segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            segment = netManager.m_segments.m_buffer[segmentId];
            return (segment.m_flags & NetSegment.Flags.Created) != 0
                   && segment.Info != null
                   && segment.Info.m_netAI is RoadBaseAI
                   && segment.m_startNode != 0
                   && segment.m_endNode != 0;
        }

        private static bool TryGetDirectionAwayFromNode(
            NetManager netManager,
            ref NetSegment segment,
            ushort nodeId,
            out Vector3 direction)
        {
            direction = Vector3.zero;
            ushort otherNodeId;
            if (segment.m_startNode == nodeId)
                otherNodeId = segment.m_endNode;
            else if (segment.m_endNode == nodeId)
                otherNodeId = segment.m_startNode;
            else
                return false;

            if (otherNodeId == 0 || otherNodeId >= netManager.m_nodes.m_size)
                return false;

            direction = netManager.m_nodes.m_buffer[otherNodeId].m_position
                        - netManager.m_nodes.m_buffer[nodeId].m_position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                return false;

            direction.Normalize();
            return true;
        }

        public static ObservationSession BeginObservation()
        {
            ObservationSession session = new ObservationSession();
            NetManager netManager = NetManager.instance;
            if (netManager == null || netManager.m_nodes == null || netManager.m_segments == null)
                return session;

            RoadPlacementRules.RequestVanillaCrossingCacheRefresh("auto-scan-observation");
            PedestrianCrossingLog.Advanced("[PedestrianCrossingToolkit] Auto scan cooperative snapshot scheduled.");
            return session;
        }

        public static CrossingAutoScanPlan Build()
        {
            return Build(null);
        }

        internal static bool IsPlacementSpacedFromExisting(
            CrossingPlacementPlan plan,
            CrossingPlacementRecord placement,
            int ignoredExistingAssetId)
        {
            Vector3 center = plan.IsValid ? plan.Center : placement.WorldPosition;
            return !HasBlockingExistingAssetWithinDistance(
                center,
                AutoPlacementMinimumSpacing,
                ignoredExistingAssetId,
                placement,
                plan,
                true);
        }

        public static CrossingAutoScanPlan Build(ObservationSession observation)
        {
            AutoScanAccumulator accumulator = new AutoScanAccumulator();
            NetManager netManager = NetManager.instance;
            if (netManager == null || netManager.m_nodes == null || netManager.m_segments == null)
            {
                accumulator.Reject("network manager is unavailable");
                return accumulator.ToPlan();
            }

            if (observation != null && observation.HasSamples)
            {
                return observation.BuildPlan();
            }
            else
            {
                RoadPlacementRules.ForceRefreshVanillaCrossingCache("auto-scan");
                ScanImpactedJunctions(netManager, accumulator);
                ScanExistingSignalCrossings(netManager, accumulator);
                ScanExistingSurfaceCrossings(netManager, accumulator);
                ScanLongRoadSegments(netManager, accumulator);
            }

            CrossingAutoScanPlan plan = accumulator.ToPlan();
            PedestrianCrossingLog.Advanced("[PedestrianCrossingToolkit] Auto scan planned: " + plan.Summary.ToLogString());
            return plan;
        }

        private static bool CollectJunctionObservationCandidates(NetManager netManager, ObservationSession session, ref int nextNodeIndex, int batchSize)
        {
            int nodeLimit = ManagerCapacity.GetExclusiveUpperBound(
                netManager.m_nodes.m_size,
                netManager.m_nodes.m_buffer.Length);
            int endNodeIndex = Math.Min(nodeLimit, nextNodeIndex + batchSize);
            for (int nodeIndex = nextNodeIndex; nodeIndex < endNodeIndex; nodeIndex++)
            {
                ushort nodeId = (ushort)nodeIndex;
                ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
                if ((node.m_flags & NetNode.Flags.Created) == 0)
                    continue;

                bool isGradeSeparatedJunction =
                    RoadPlacementRules.IsThreePlusJunctionNode(nodeId);

                for (int i = 0; i < NetNodeSegmentSlotCount; i++)
                {
                    ushort segmentId = node.GetSegment(i);
                    if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                        continue;

                    ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                    if ((segment.m_flags & NetSegment.Flags.Created) == 0
                        || segment.Info == null
                        || !(segment.Info.m_netAI is RoadBaseAI))
                    {
                        continue;
                    }

                    bool isEnd = segment.m_endNode == nodeId;
                    if (!isEnd && segment.m_startNode != nodeId)
                        continue;

                    RoadPlacementRules.VanillaCrossingPoint crossingPoint;
                    bool hasVanillaCrossing =
                        RoadPlacementRules.TryGetVanillaCrossingPoint(
                            segmentId,
                            isEnd,
                            out crossingPoint);
                    if (hasVanillaCrossing)
                    {
                        session.AddVanillaCrossingExclusion(
                            nodeId,
                            segmentId,
                            crossingPoint.WorldPosition);
                    }

                    if (isGradeSeparatedJunction
                        && RoadPlacementRules.IsRoadGradeSeparatedPlacementTarget(segmentId)
                        && (hasVanillaCrossing
                            || RoadPlacementRules.TryGetJunctionApproachCrossingPoint(
                                segmentId,
                                isEnd,
                                out crossingPoint)))
                    {
                        session.AddJunctionCandidate(
                            nodeId,
                            segmentId,
                            crossingPoint,
                            hasVanillaCrossing);
                    }
                }
            }

            nextNodeIndex = endNodeIndex;
            return nextNodeIndex >= nodeLimit;
        }

        private static bool CollectSurfaceObservationCandidates(
            ObservationSession session,
            ref int nextAssetIndex,
            int batchSize)
        {
            int count = CrossingPlacementRegistry.Count;
            int endAssetIndex = Math.Min(count, nextAssetIndex + batchSize);
            for (int i = nextAssetIndex; i < endAssetIndex; i++)
            {
                CrossingPlacementAsset asset;
                if (!CrossingPlacementRegistry.TryGetAssetAtIndex(i, out asset))
                    continue;

                if (asset.Id == 0 || !asset.Plan.IsValid)
                    continue;

                if (asset.Placement.Mode == PedestrianToolMode.MidBlockCrossing
                    || asset.Placement.Mode == PedestrianToolMode.SignalCrossing)
                {
                    session.AddExistingCrossingCandidate(asset);
                }
            }

            nextAssetIndex = endAssetIndex;
            return nextAssetIndex >= count;
        }

        private static bool CollectLongRoadObservationCandidates(NetManager netManager, ObservationSession session, ref int nextSegmentIndex, int batchSize)
        {
            int segmentLimit = ManagerCapacity.GetExclusiveUpperBound(
                netManager.m_segments.m_size,
                netManager.m_segments.m_buffer.Length);
            int endSegmentIndex = Math.Min(segmentLimit, nextSegmentIndex + batchSize);
            for (int segmentIndex = nextSegmentIndex; segmentIndex < endSegmentIndex; segmentIndex++)
            {
                ushort segmentId = (ushort)segmentIndex;
                if (session.HasVisitedContinuousRoadSegment(segmentId))
                    continue;

                int corridorCount;
                if (!TryBuildStraightRoadCorridor(
                    netManager,
                    segmentId,
                    CorridorSegmentBuffer,
                    CorridorForwardBuffer,
                    out corridorCount))
                {
                    session.MarkContinuousRoadSegmentVisited(segmentId);
                    continue;
                }

                for (int i = 0; i < corridorCount; i++)
                    session.MarkContinuousRoadSegmentVisited(CorridorSegmentBuffer[i]);

                AddStraightRoadCorridorCandidates(
                    netManager,
                    session,
                    CorridorSegmentBuffer,
                    CorridorForwardBuffer,
                    corridorCount);
            }

            nextSegmentIndex = endSegmentIndex;
            return nextSegmentIndex >= segmentLimit;
        }

        private static bool TryBuildStraightRoadCorridor(
            NetManager netManager,
            ushort seedSegmentId,
            ushort[] segmentIds,
            bool[] forward,
            out int count)
        {
            count = 0;
            NetSegment seed;
            if (!TryGetCreatedRoadSegment(netManager, seedSegmentId, out seed)
                || !RoadPlacementRules.AllowsSurfaceCrossing(seedSegmentId)
                || segmentIds == null
                || forward == null)
            {
                return false;
            }

            ushort terminalSegmentId = seedSegmentId;
            ushort terminalBoundaryNodeId = seed.m_startNode;
            ushort currentSegmentId = seedSegmentId;
            ushort outwardNodeId = seed.m_startNode;
            ushort[] reverseWalk = new ushort[ContinuousRoadTraversalLimit];
            int reverseCount = 0;
            while (reverseCount < reverseWalk.Length)
            {
                reverseWalk[reverseCount++] = currentSegmentId;
                NetSegment current;
                ushort continuation;
                if (!TryGetCreatedRoadSegment(netManager, currentSegmentId, out current)
                    || !TryGetStraightRoadContinuation(
                        netManager,
                        currentSegmentId,
                        ref current,
                        outwardNodeId,
                        out continuation)
                    || !RoadPlacementRules.AllowsSurfaceCrossing(continuation))
                {
                    terminalSegmentId = currentSegmentId;
                    terminalBoundaryNodeId = outwardNodeId;
                    break;
                }

                for (int i = 0; i < reverseCount; i++)
                {
                    if (reverseWalk[i] == continuation)
                        return false;
                }

                NetSegment next;
                if (!TryGetCreatedRoadSegment(netManager, continuation, out next))
                    break;

                currentSegmentId = continuation;
                outwardNodeId = next.m_startNode == outwardNodeId
                    ? next.m_endNode
                    : next.m_startNode;
            }

            currentSegmentId = terminalSegmentId;
            ushort entryNodeId = terminalBoundaryNodeId;
            while (count < segmentIds.Length && count < forward.Length)
            {
                NetSegment current;
                if (!TryGetCreatedRoadSegment(netManager, currentSegmentId, out current))
                    break;

                for (int i = 0; i < count; i++)
                {
                    if (segmentIds[i] == currentSegmentId)
                        return count > 0;
                }

                segmentIds[count] = currentSegmentId;
                forward[count] = current.m_startNode == entryNodeId;
                count++;

                ushort exitNodeId = current.m_startNode == entryNodeId
                    ? current.m_endNode
                    : current.m_startNode;
                ushort continuation;
                if (!TryGetStraightRoadContinuation(
                    netManager,
                    currentSegmentId,
                    ref current,
                    exitNodeId,
                    out continuation)
                    || !RoadPlacementRules.AllowsSurfaceCrossing(continuation))
                {
                    break;
                }

                currentSegmentId = continuation;
                entryNodeId = exitNodeId;
            }

            return count > 0;
        }

        private static void AddStraightRoadCorridorCandidates(
            NetManager netManager,
            ObservationSession session,
            ushort[] segmentIds,
            bool[] forward,
            int segmentCount)
        {
            if (segmentIds == null || forward == null || segmentCount <= 0)
                return;

            float totalLength = 0f;
            int max = Math.Min(segmentCount, Math.Min(segmentIds.Length, forward.Length));
            for (int i = 0; i < max; i++)
            {
                NetSegment segment;
                if (TryGetCreatedRoadSegment(netManager, segmentIds[i], out segment))
                    totalLength += Mathf.Max(1f, segment.m_averageLength);
            }

            if (totalLength <= 1f)
                return;

            AddLocalizedStraightRoadCandidates(
                netManager,
                session,
                segmentIds,
                forward,
                max,
                totalLength);
        }

        private static void AddLocalizedStraightRoadCandidates(
            NetManager netManager,
            ObservationSession session,
            ushort[] segmentIds,
            bool[] forward,
            int segmentCount,
            float totalLength)
        {
            int candidateCount = Mathf.Max(
                1,
                Mathf.CeilToInt(totalLength / RoadObservationCandidateSpacing));
            float candidateSpacing = totalLength / candidateCount;
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                float candidateOffset = candidateSpacing * (candidateIndex + 0.5f);
                ushort placementSegmentId;
                float placementSegmentPosition;
                if (!TryResolveCorridorOffset(
                    netManager,
                    segmentIds,
                    forward,
                    segmentCount,
                    candidateOffset,
                    out placementSegmentId,
                    out placementSegmentPosition))
                {
                    continue;
                }

                NetSegment placementSegment;
                if (!TryGetCreatedRoadSegment(netManager, placementSegmentId, out placementSegment))
                    continue;

                Vector3 sample = GetSegmentSamplePosition(
                    netManager,
                    ref placementSegment,
                    placementSegmentPosition);
                CrossingPlacementRecord placement;
                CrossingPlacementPlan plan;
                if (!TryCreateRoadPlacement(
                    PedestrianToolMode.MidBlockCrossing,
                    placementSegmentId,
                    sample,
                    out placement,
                    out plan))
                {
                    continue;
                }

                ushort[] localSegments;
                float[] localFrom;
                float[] localTo;
                bool[] localForward;
                int localSegmentCount;
                if (!TryBuildCorridorGapSegments(
                    netManager,
                    segmentIds,
                    forward,
                    segmentCount,
                    Mathf.Max(0f, candidateOffset - LongRoadObservationHalfLength),
                    Mathf.Min(totalLength, candidateOffset + LongRoadObservationHalfLength),
                    out localSegments,
                    out localFrom,
                    out localTo,
                    out localForward,
                    out localSegmentCount))
                {
                    continue;
                }

                session.AddLongRoadCandidate(
                    placement,
                    plan,
                    localSegments,
                    localFrom,
                    localTo,
                    localForward,
                    localSegmentCount);
            }
        }

        private static bool TryResolveCorridorOffset(
            NetManager netManager,
            ushort[] segmentIds,
            bool[] forward,
            int segmentCount,
            float targetOffset,
            out ushort segmentId,
            out float segmentPosition)
        {
            segmentId = 0;
            segmentPosition = 0f;
            float offset = 0f;
            for (int i = 0; i < segmentCount; i++)
            {
                NetSegment segment;
                if (!TryGetCreatedRoadSegment(netManager, segmentIds[i], out segment))
                    continue;

                float length = Mathf.Max(1f, segment.m_averageLength);
                if (targetOffset <= offset + length || i == segmentCount - 1)
                {
                    float along = Mathf.Clamp01((targetOffset - offset) / length);
                    segmentId = segmentIds[i];
                    segmentPosition = forward[i] ? along : 1f - along;
                    return true;
                }

                offset += length;
            }

            return false;
        }

        private static bool TryBuildCorridorGapSegments(
            NetManager netManager,
            ushort[] segmentIds,
            bool[] forward,
            int segmentCount,
            float gapStart,
            float gapEnd,
            out ushort[] candidateSegments,
            out float[] candidateFrom,
            out float[] candidateTo,
            out bool[] candidateForward,
            out int candidateCount)
        {
            candidateSegments = new ushort[segmentCount];
            candidateFrom = new float[segmentCount];
            candidateTo = new float[segmentCount];
            candidateForward = new bool[segmentCount];
            candidateCount = 0;
            float offset = 0f;
            for (int i = 0; i < segmentCount; i++)
            {
                NetSegment segment;
                if (!TryGetCreatedRoadSegment(netManager, segmentIds[i], out segment))
                    continue;

                float length = Mathf.Max(1f, segment.m_averageLength);
                float segmentStart = offset;
                float segmentEnd = offset + length;
                float overlapStart = Mathf.Max(gapStart, segmentStart);
                float overlapEnd = Mathf.Min(gapEnd, segmentEnd);
                offset = segmentEnd;
                if (overlapEnd <= overlapStart)
                    continue;

                float from = Mathf.Clamp01((overlapStart - segmentStart) / length);
                float to = Mathf.Clamp01((overlapEnd - segmentStart) / length);
                if (!forward[i])
                {
                    float reversedFrom = 1f - to;
                    to = 1f - from;
                    from = reversedFrom;
                }

                candidateSegments[candidateCount] = segmentIds[i];
                candidateFrom[candidateCount] = Mathf.Min(from, to);
                candidateTo[candidateCount] = Mathf.Max(from, to);
                candidateForward[candidateCount] = forward[i];
                candidateCount++;
            }

            return candidateCount > 0;
        }

        private static void ScanImpactedJunctions(NetManager netManager, AutoScanAccumulator accumulator)
        {
            int nodeLimit = ManagerCapacity.GetExclusiveUpperBound(
                netManager.m_nodes.m_size,
                netManager.m_nodes.m_buffer.Length);
            for (int nodeIndex = 1; nodeIndex < nodeLimit; nodeIndex++)
            {
                ushort nodeId = (ushort)nodeIndex;
                ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
                if ((node.m_flags & NetNode.Flags.Created) == 0 || !RoadPlacementRules.IsThreePlusJunctionNode(nodeId))
                    continue;

                bool countedNode = false;
                for (int i = 0; i < NetNodeSegmentSlotCount; i++)
                {
                    ushort segmentId = node.GetSegment(i);
                    if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                        continue;

                    ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                    if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !RoadPlacementRules.IsRoadGradeSeparatedPlacementTarget(segmentId))
                        continue;

                    bool isEnd = segment.m_endNode == nodeId;
                    if (!isEnd && segment.m_startNode != nodeId)
                        continue;

                    RoadPlacementRules.VanillaCrossingPoint crossingPoint;
                    if (!RoadPlacementRules.TryGetJunctionApproachCrossingPoint(
                        segmentId,
                        isEnd,
                        out crossingPoint))
                        continue;

                    if (!countedNode)
                    {
                        accumulator.ScannedNodes++;
                        countedNode = true;
                    }

                    Vector3 crossingFirst;
                    Vector3 crossingSecond;
                    if (!TryGetJunctionCrossingSpan(
                        segmentId,
                        crossingPoint,
                        out crossingFirst,
                        out crossingSecond))
                    {
                        continue;
                    }

                    TrafficCounts counts = CountPedestriansTraversingCrossing(
                        crossingFirst,
                        crossingSecond);
                    if (counts.CrossingPedestrians <= 0)
                        continue;

                    accumulator.Hotspots++;

                    CrossingPlacementRecord placement;
                    CrossingPlacementPlan plan;
                    if (TryCreateGradeSeparatedJunctionPlacement(nodeId, segmentId, crossingPoint.WorldPosition, accumulator, out placement, out plan))
                        accumulator.TryAddPlacement(placement, plan);
                    else
                        accumulator.Reject("no legal subway or bridge placement found at impacted junction");
                }
            }
        }

        private static void ScanLongRoadSegments(NetManager netManager, AutoScanAccumulator accumulator)
        {
            ObservationSession session = new ObservationSession();
            int nextNodeIndex = 1;
            while (!CollectJunctionObservationCandidates(
                netManager,
                session,
                ref nextNodeIndex,
                512))
            {
            }

            int nextSegmentIndex = 1;
            while (!CollectLongRoadObservationCandidates(
                netManager,
                session,
                ref nextSegmentIndex,
                512))
            {
            }

            session.ScanImmediateLongRoadSegments(netManager, accumulator);
        }

        private static void ScanExistingSurfaceCrossings(NetManager netManager, AutoScanAccumulator accumulator)
        {
            int count = CrossingPlacementRegistry.Count;
            for (int i = 0; i < count; i++)
            {
                CrossingPlacementAsset asset;
                if (!CrossingPlacementRegistry.TryGetAssetAtIndex(i, out asset))
                    continue;

                if (asset.Id == 0 || asset.Placement.Mode != PedestrianToolMode.MidBlockCrossing || !asset.Plan.IsValid)
                    continue;

                accumulator.ScannedExistingCrossings++;
                TrafficCounts counts = CountPedestriansTraversingCrossing(
                    asset.Plan.LeftEdge,
                    asset.Plan.RightEdge);
                if (counts.CrossingPedestrians <= 0)
                    continue;

                accumulator.Hotspots++;

                CrossingPlacementRecord signalPlacement;
                CrossingPlacementPlan signalPlan;
                if (!TryFindLegalSignalReplacement(netManager, asset, out signalPlacement, out signalPlan))
                {
                    accumulator.Reject("no legal nearby signal join found for busy surface crossing");
                    continue;
                }

                accumulator.TryAddUpgrade(signalPlacement, signalPlan, asset.Id);
            }
        }

        private static void ScanExistingSignalCrossings(NetManager netManager, AutoScanAccumulator accumulator)
        {
            int count = CrossingPlacementRegistry.Count;
            for (int i = 0; i < count; i++)
            {
                CrossingPlacementAsset asset;
                if (!CrossingPlacementRegistry.TryGetAssetAtIndex(i, out asset))
                    continue;

                if (asset.Id == 0
                    || asset.Placement.Mode != PedestrianToolMode.SignalCrossing
                    || !asset.Plan.IsValid)
                {
                    continue;
                }

                accumulator.ScannedExistingCrossings++;
                TrafficCounts counts = CountPedestriansTraversingCrossing(
                    asset.Plan.LeftEdge,
                    asset.Plan.RightEdge);
                if (counts.CrossingPedestrians <= 0)
                    continue;

                accumulator.Hotspots++;

                ushort nodeId = asset.Plan.TargetNodeId != 0
                    ? asset.Plan.TargetNodeId
                    : asset.Placement.TargetNodeId;
                CrossingPlacementRecord gradePlacement;
                CrossingPlacementPlan gradePlan;
                if (nodeId == 0
                    || !TryCreateGradeSeparatedJunctionPlacement(
                        nodeId,
                        asset.Placement.SegmentId,
                        asset.Plan.Center,
                        accumulator,
                        out gradePlacement,
                        out gradePlan))
                {
                    accumulator.Reject("no legal subway or bridge placement found for busy signal crossing");
                    continue;
                }

                accumulator.TryAddUpgrade(gradePlacement, gradePlan, asset.Id);
            }
        }

        private static bool TryFindLegalSignalReplacement(
            NetManager netManager,
            CrossingPlacementAsset asset,
            out CrossingPlacementRecord placement,
            out CrossingPlacementPlan plan)
        {
            placement = CrossingPlacementRecord.None;
            plan = CrossingPlacementPlan.Invalid;
            if (asset.Placement.SegmentId == 0 || asset.Placement.SegmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[asset.Placement.SegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                return false;

            SignalNodeCandidate[] candidates = new SignalNodeCandidate[18];
            int candidateCount = 0;
            AddSignalNodeCandidate(netManager, candidates, ref candidateCount, segment.m_startNode, asset.Plan.Center);
            AddSignalNodeCandidate(netManager, candidates, ref candidateCount, segment.m_endNode, asset.Plan.Center);
            AddOneHopSignalNodeCandidates(netManager, candidates, ref candidateCount, segment.m_startNode, asset.Plan.Center);
            AddOneHopSignalNodeCandidates(netManager, candidates, ref candidateCount, segment.m_endNode, asset.Plan.Center);
            SortSignalNodeCandidates(candidates, candidateCount);

            float maxDistanceSqr = SignalRelocationMaxDistance * SignalRelocationMaxDistance;
            for (int i = 0; i < candidateCount; i++)
            {
                if (candidates[i].DistanceSqr > maxDistanceSqr)
                    continue;

                if (TryCreateSignalJoinPlacement(candidates[i].NodeId, out placement, out plan))
                    return true;
            }

            return false;
        }

        private static void AddOneHopSignalNodeCandidates(
            NetManager netManager,
            SignalNodeCandidate[] candidates,
            ref int candidateCount,
            ushort nodeId,
            Vector3 origin)
        {
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return;

            for (int i = 0; i < NetNodeSegmentSlotCount; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !RoadPlacementRules.AllowsSurfaceCrossing(segmentId))
                    continue;

                ushort otherNodeId = segment.m_startNode == nodeId ? segment.m_endNode : segment.m_startNode;
                AddSignalNodeCandidate(netManager, candidates, ref candidateCount, otherNodeId, origin);
            }
        }

        private static void AddSignalNodeCandidate(
            NetManager netManager,
            SignalNodeCandidate[] candidates,
            ref int candidateCount,
            ushort nodeId,
            Vector3 origin)
        {
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size || candidates == null || candidateCount >= candidates.Length)
                return;

            for (int i = 0; i < candidateCount; i++)
            {
                if (candidates[i].NodeId == nodeId)
                    return;
            }

            Vector3 nodePosition = netManager.m_nodes.m_buffer[nodeId].m_position;
            candidates[candidateCount++] = new SignalNodeCandidate(nodeId, HorizontalSqrDistance(origin, nodePosition));
        }

        private static void SortSignalNodeCandidates(SignalNodeCandidate[] candidates, int count)
        {
            for (int i = 1; i < count; i++)
            {
                SignalNodeCandidate current = candidates[i];
                int j = i - 1;
                while (j >= 0 && candidates[j].DistanceSqr > current.DistanceSqr)
                {
                    candidates[j + 1] = candidates[j];
                    j--;
                }

                candidates[j + 1] = current;
            }
        }

        private static bool TryCreateGradeSeparatedJunctionPlacement(
            ushort nodeId,
            ushort segmentId,
            Vector3 referencePosition,
            AutoScanAccumulator accumulator,
            out CrossingPlacementRecord placement,
            out CrossingPlacementPlan plan)
        {
            placement = CrossingPlacementRecord.None;
            plan = CrossingPlacementPlan.Invalid;

            PedestrianToolMode preferredMode;
            bool hasPreferredMode = TryGetPreferredGradeSeparatedModeForJunction(nodeId, accumulator, out preferredMode);
            if (!hasPreferredMode)
                preferredMode = GetRandomGradeSeparatedMode();

            if (TryCreateRoadPlacement(preferredMode, segmentId, referencePosition, out placement, out plan))
                return true;

            if (hasPreferredMode)
                return false;

            PedestrianToolMode fallbackMode = preferredMode == PedestrianToolMode.PedestrianBridge
                ? PedestrianToolMode.SubwayLink
                : PedestrianToolMode.PedestrianBridge;
            return TryCreateRoadPlacement(fallbackMode, segmentId, referencePosition, out placement, out plan);
        }

        private static bool TryGetPreferredGradeSeparatedModeForJunction(
            ushort nodeId,
            AutoScanAccumulator accumulator,
            out PedestrianToolMode mode)
        {
            mode = PedestrianToolMode.None;
            if (nodeId == 0)
                return false;

            int existingCount = CrossingPlacementRegistry.Count;
            for (int i = 0; i < existingCount; i++)
            {
                CrossingPlacementAsset asset;
                if (!CrossingPlacementRegistry.TryGetAssetAtIndex(i, out asset))
                    continue;

                if (asset.Id == 0 || !IsGradeSeparatedMode(asset.Placement.Mode))
                    continue;

                if (GetGradeSeparatedTargetNode(asset.Placement, asset.Plan) != nodeId)
                    continue;

                mode = IsSubwayMode(asset.Placement.Mode)
                    ? PedestrianToolMode.SubwayLink
                    : PedestrianToolMode.PedestrianBridge;
                return true;
            }

            if (accumulator == null)
                return false;

            for (int i = 0; i < accumulator.PlacementCount && i < accumulator.Placements.Length; i++)
            {
                CrossingPlacementRecord placement = accumulator.Placements[i];
                if (!IsGradeSeparatedMode(placement.Mode))
                    continue;

                if (GetGradeSeparatedTargetNode(placement, CrossingPlacementPlan.Invalid) != nodeId)
                    continue;

                mode = IsSubwayMode(placement.Mode)
                    ? PedestrianToolMode.SubwayLink
                    : PedestrianToolMode.PedestrianBridge;
                return true;
            }

            return false;
        }

        private static PedestrianToolMode GetRandomGradeSeparatedMode()
        {
            return UnityEngine.Random.value < 0.5f
                ? PedestrianToolMode.SubwayLink
                : PedestrianToolMode.PedestrianBridge;
        }

        private static bool TryCreateRoadPlacement(
            PedestrianToolMode mode,
            ushort segmentId,
            Vector3 referencePosition,
            out CrossingPlacementRecord placement,
            out CrossingPlacementPlan plan)
        {
            placement = CrossingPlacementRecord.None;
            plan = CrossingPlacementPlan.Invalid;
            RoadSnapResult snap;
            if (!RoadSnapResolver.TryResolve(segmentId, referencePosition, mode, out snap))
                return false;

            return TryCreatePlacementFromSnap(mode, snap, out placement, out plan);
        }

        private static bool TryCreateSurfaceCompensationPlacement(
            NetManager netManager,
            ushort segmentId,
            ref NetSegment segment,
            out CrossingPlacementRecord placement,
            out CrossingPlacementPlan plan)
        {
            placement = CrossingPlacementRecord.None;
            plan = CrossingPlacementPlan.Invalid;
            for (int i = 0; i < SurfaceCandidatePositions.Length; i++)
            {
                Vector3 sample = GetSegmentSamplePosition(netManager, ref segment, SurfaceCandidatePositions[i]);
                if (TryCreateRoadPlacement(PedestrianToolMode.MidBlockCrossing, segmentId, sample, out placement, out plan))
                    return true;
            }

            return false;
        }

        private static bool TryCreateSignalJoinPlacement(
            ushort nodeId,
            out CrossingPlacementRecord placement,
            out CrossingPlacementPlan plan)
        {
            placement = CrossingPlacementRecord.None;
            plan = CrossingPlacementPlan.Invalid;
            RoadSnapResult snap;
            if (!RoadSnapResolver.TryResolveSignalJoinNode(nodeId, out snap))
                return false;

            return TryCreatePlacementFromSnap(PedestrianToolMode.SignalCrossing, snap, out placement, out plan);
        }

        private static bool TryCreatePlacementFromSnap(
            PedestrianToolMode mode,
            RoadSnapResult snap,
            out CrossingPlacementRecord placement,
            out CrossingPlacementPlan plan)
        {
            placement = CrossingPlacementRecord.None;
            plan = CrossingPlacementPlan.Invalid;
            if (!snap.IsResolved || snap.SegmentId == 0)
                return false;

            CrossingPlacementRecord candidate = new CrossingPlacementRecord(
                mode,
                snap.SegmentId,
                snap.SegmentPosition,
                snap.WorldPosition,
                true,
                string.Empty,
                snap.IsEndpointSlot,
                snap.SlotNumber,
                snap.IsEndSegmentSlot,
                snap.TargetNodeId);

            CrossingPlacementPolicyResult policy = CrossingPlacementPolicy.Evaluate(candidate);
            if (!policy.Success)
            {
                placement = new CrossingPlacementRecord(
                    mode,
                    snap.SegmentId,
                    snap.SegmentPosition,
                    snap.WorldPosition,
                    false,
                    policy.Message,
                    snap.IsEndpointSlot,
                    snap.SlotNumber,
                    snap.IsEndSegmentSlot,
                    snap.TargetNodeId);
                return false;
            }

            CrossingPlacementPlan candidatePlan = CrossingPlacementPlanner.Build(candidate);
            if (!candidatePlan.IsValid)
                return false;

            placement = new CrossingPlacementRecord(
                mode,
                snap.SegmentId,
                snap.SegmentPosition,
                snap.WorldPosition,
                true,
                policy.Message,
                snap.IsEndpointSlot,
                snap.SlotNumber,
                snap.IsEndSegmentSlot,
                snap.TargetNodeId);
            plan = candidatePlan;
            return true;
        }

        private static bool TryGetJunctionCrossingSpan(
            ushort segmentId,
            RoadPlacementRules.VanillaCrossingPoint crossingPoint,
            out Vector3 first,
            out Vector3 second)
        {
            first = Vector3.zero;
            second = Vector3.zero;
            NetManager netManager = NetManager.instance;
            if (netManager == null
                || segmentId == 0
                || segmentId >= netManager.m_segments.m_size)
            {
                return false;
            }

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null)
                return false;

            Vector3 center;
            Vector3 roadDirection;
            Vector3 crossingDirection;
            if (!CrossingPlacementPlanner.TryGetRoadFrameForPlacement(
                netManager,
                ref segment,
                crossingPoint.SegmentPosition,
                crossingPoint.WorldPosition,
                out center,
                out roadDirection,
                out crossingDirection))
            {
                return false;
            }

            float halfWidth = Mathf.Max(4f, segment.Info.m_halfWidth + 1f);
            first = center - (crossingDirection * halfWidth);
            second = center + (crossingDirection * halfWidth);
            return true;
        }

        private static TrafficCounts CountPedestriansTraversingCrossing(
            Vector3 first,
            Vector3 second)
        {
            TrafficCounts counts = new TrafficCounts();
            CitizenManager citizenManager = CitizenManager.instance;
            if (citizenManager == null
                || citizenManager.m_instances == null
                || citizenManager.m_instances.m_buffer == null)
            {
                return counts;
            }

            CitizenInstance[] buffer = citizenManager.m_instances.m_buffer;
            if (TryCountCrossingPedestriansInGrid(
                citizenManager,
                buffer,
                first,
                second,
                ref counts))
            {
                return counts;
            }

            uint max = Math.Min(citizenManager.m_instances.m_size, (uint)buffer.Length);
            for (uint i = 1; i < max; i++)
                CountCrossingPedestrianIfTraversing(buffer[i], first, second, ref counts);

            return counts;
        }

        private static TrafficCounts CountPavementPedestrians(
            ObservationCandidate candidate)
        {
            TrafficCounts counts = new TrafficCounts();
            if (candidate.CorridorSegmentIds == null
                || candidate.CorridorSegmentFrom == null
                || candidate.CorridorSegmentTo == null
                || candidate.CorridorSegmentForward == null)
            {
                return counts;
            }

            CitizenManager citizenManager = CitizenManager.instance;
            NetManager netManager = NetManager.instance;
            if (citizenManager == null
                || netManager == null
                || citizenManager.m_instances == null
                || citizenManager.m_instances.m_buffer == null
                || citizenManager.m_citizenGrid == null)
            {
                return counts;
            }

            CitizenInstance[] buffer = citizenManager.m_instances.m_buffer;
            int resolution = CitizenManager.CITIZENGRID_RESOLUTION;
            float cellSize = CitizenManager.CITIZENGRID_CELL_SIZE;
            if (resolution <= 0
                || cellSize <= 0f
                || citizenManager.m_citizenGrid.Length < resolution * resolution)
            {
                return counts;
            }

            PavementPedestrianIds.Clear();
            int max = Math.Min(
                candidate.CorridorSegmentCount,
                Math.Min(
                    candidate.CorridorSegmentIds.Length,
                    Math.Min(
                        candidate.CorridorSegmentFrom.Length,
                        Math.Min(
                            candidate.CorridorSegmentTo.Length,
                            candidate.CorridorSegmentForward.Length))));
            for (int i = 0; i < max; i++)
            {
                ushort segmentId = candidate.CorridorSegmentIds[i];
                NetSegment segment;
                if (!TryGetCreatedRoadSegment(netManager, segmentId, out segment)
                    || segment.Info == null)
                {
                    continue;
                }

                Vector3 start = netManager.m_nodes.m_buffer[segment.m_startNode].m_position;
                Vector3 end = netManager.m_nodes.m_buffer[segment.m_endNode].m_position;
                float padding = segment.Info.m_halfWidth + PavementOuterTolerance;
                int minX = GetGridCoord(Mathf.Min(start.x, end.x) - padding, cellSize, resolution);
                int maxX = GetGridCoord(Mathf.Max(start.x, end.x) + padding, cellSize, resolution);
                int minZ = GetGridCoord(Mathf.Min(start.z, end.z) - padding, cellSize, resolution);
                int maxZ = GetGridCoord(Mathf.Max(start.z, end.z) + padding, cellSize, resolution);
                int traversalLimit = Math.Min(buffer.Length, GridTraversalLimit);
                for (int gridZ = minZ; gridZ <= maxZ; gridZ++)
                {
                    int rowOffset = gridZ * resolution;
                    for (int gridX = minX; gridX <= maxX; gridX++)
                    {
                        ushort instanceId = citizenManager.m_citizenGrid[rowOffset + gridX];
                        int traversed = 0;
                        while (instanceId != 0 && traversed++ < traversalLimit)
                        {
                            if (instanceId >= buffer.Length)
                                break;

                            CitizenInstance instance = buffer[instanceId];
                            ushort nextInstanceId = instance.m_nextGridInstance;
                            bool firstSide;
                            if (!PavementPedestrianIds.Contains(instanceId)
                                && IsPedestrianUsingCorridorPavement(
                                    instance,
                                    netManager,
                                    ref segment,
                                    candidate.CorridorSegmentFrom[i],
                                    candidate.CorridorSegmentTo[i],
                                    candidate.CorridorSegmentForward[i],
                                    out firstSide))
                            {
                                PavementPedestrianIds.Add(instanceId);
                                if (firstSide)
                                    counts.PavementPedestriansFirstSide++;
                                else
                                    counts.PavementPedestriansSecondSide++;
                            }

                            instanceId = nextInstanceId;
                        }
                    }
                }
            }

            return counts;
        }

        private static bool IsPedestrianUsingCorridorPavement(
            CitizenInstance instance,
            NetManager netManager,
            ref NetSegment segment,
            float from,
            float to,
            bool forward,
            out bool firstSide)
        {
            firstSide = false;
            if (!IsPedestrianCandidate(instance)
                || segment.Info == null
                || segment.m_startNode == 0
                || segment.m_endNode == 0)
            {
                return false;
            }

            Vector3 position = instance.GetLastFramePosition();
            Vector3 start = netManager.m_nodes.m_buffer[segment.m_startNode].m_position;
            Vector3 end = netManager.m_nodes.m_buffer[segment.m_endNode].m_position;
            Vector3 flatStart = start;
            Vector3 flatEnd = end;
            Vector3 flatPosition = position;
            flatStart.y = 0f;
            flatEnd.y = 0f;
            flatPosition.y = 0f;
            Vector3 roadDirection = flatEnd - flatStart;
            float lengthSqr = roadDirection.sqrMagnitude;
            if (lengthSqr <= 0.01f)
                return false;

            float positionAlong = Mathf.Clamp01(
                Vector3.Dot(flatPosition - flatStart, roadDirection) / lengthSqr);
            if (positionAlong + 0.01f < from || positionAlong - 0.01f > to)
                return false;

            Vector3 closest = segment.GetClosestPosition(position);
            float distance = Mathf.Sqrt(HorizontalSqrDistance(position, closest));
            float inner = Mathf.Max(0f, segment.Info.m_halfWidth - PavementInnerTolerance);
            float outer = segment.Info.m_halfWidth + PavementOuterTolerance;
            if (distance < inner || distance > outer)
                return false;

            roadDirection.Normalize();
            Vector3 roadRight = new Vector3(-roadDirection.z, 0f, roadDirection.x);
            float side = Vector3.Dot(flatPosition - closest, roadRight);
            firstSide = forward ? side >= 0f : side < 0f;
            return true;
        }

        private static bool TryCountCrossingPedestriansInGrid(
            CitizenManager citizenManager,
            CitizenInstance[] buffer,
            Vector3 first,
            Vector3 second,
            ref TrafficCounts counts)
        {
            if (citizenManager.m_citizenGrid == null || buffer == null)
                return false;

            int resolution = CitizenManager.CITIZENGRID_RESOLUTION;
            float cellSize = CitizenManager.CITIZENGRID_CELL_SIZE;
            if (resolution <= 0
                || cellSize <= 0f
                || citizenManager.m_citizenGrid.Length < resolution * resolution)
            {
                return false;
            }

            float padding = CrossingTraversalRadius + 1f;
            int minX = GetGridCoord(Mathf.Min(first.x, second.x) - padding, cellSize, resolution);
            int maxX = GetGridCoord(Mathf.Max(first.x, second.x) + padding, cellSize, resolution);
            int minZ = GetGridCoord(Mathf.Min(first.z, second.z) - padding, cellSize, resolution);
            int maxZ = GetGridCoord(Mathf.Max(first.z, second.z) + padding, cellSize, resolution);
            int traversalLimit = Math.Min(buffer.Length, GridTraversalLimit);

            for (int gridZ = minZ; gridZ <= maxZ; gridZ++)
            {
                int rowOffset = gridZ * resolution;
                for (int gridX = minX; gridX <= maxX; gridX++)
                {
                    ushort instanceId = citizenManager.m_citizenGrid[rowOffset + gridX];
                    int traversed = 0;
                    while (instanceId != 0 && traversed++ < traversalLimit)
                    {
                        if (instanceId >= buffer.Length)
                            break;

                        CitizenInstance instance = buffer[instanceId];
                        ushort nextInstanceId = instance.m_nextGridInstance;
                        CountCrossingPedestrianIfTraversing(
                            instance,
                            first,
                            second,
                            ref counts);
                        instanceId = nextInstanceId;
                    }
                }
            }

            return true;
        }

        private static void CountCrossingPedestrianIfTraversing(
            CitizenInstance instance,
            Vector3 first,
            Vector3 second,
            ref TrafficCounts counts)
        {
            if (!IsPedestrianCandidate(instance))
                return;

            Vector3 velocity = instance.GetLastFrameData().m_velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude <= SlowPedestrianSpeedSqr)
                return;

            float along;
            if (DistanceToSegment2D(
                instance.GetLastFramePosition(),
                first,
                second,
                out along) > CrossingTraversalRadius)
            {
                return;
            }

            Vector3 crossingDirection = second - first;
            crossingDirection.y = 0f;
            if (crossingDirection.sqrMagnitude <= 0.01f)
                return;

            crossingDirection.Normalize();
            velocity.Normalize();
            if (Mathf.Abs(Vector3.Dot(velocity, crossingDirection))
                < CrossingTraversalDirectionDot)
            {
                return;
            }

            counts.CrossingPedestrians++;
        }

        private static TrafficCounts CountPedestriansNear(Vector3 center, float pedestrianRadius)
        {
            TrafficCounts counts = new TrafficCounts();
            CountPedestriansNear(center, pedestrianRadius, ref counts);
            return counts;
        }

        private static void CountPedestriansNear(Vector3 center, float radius, ref TrafficCounts counts)
        {
            CitizenManager citizenManager = CitizenManager.instance;
            if (citizenManager == null || citizenManager.m_instances == null || citizenManager.m_instances.m_buffer == null)
                return;

            CitizenInstance[] buffer = citizenManager.m_instances.m_buffer;
            if (TryCountPedestriansInGrid(citizenManager, buffer, center, radius, ref counts))
                return;

            uint max = Math.Min(citizenManager.m_instances.m_size, (uint)buffer.Length);
            float radiusSqr = radius * radius;
            for (uint i = 1; i < max; i++)
                CountPedestrianIfNear(buffer[i], center, radiusSqr, ref counts);
        }

        private static bool TryCountPedestriansInGrid(
            CitizenManager citizenManager,
            CitizenInstance[] buffer,
            Vector3 center,
            float radius,
            ref TrafficCounts counts)
        {
            if (citizenManager.m_citizenGrid == null || buffer == null)
                return false;

            int resolution = CitizenManager.CITIZENGRID_RESOLUTION;
            float cellSize = CitizenManager.CITIZENGRID_CELL_SIZE;
            if (resolution <= 0 || cellSize <= 0f || citizenManager.m_citizenGrid.Length < resolution * resolution)
                return false;

            int minX = GetGridCoord(center.x - radius, cellSize, resolution);
            int maxX = GetGridCoord(center.x + radius, cellSize, resolution);
            int minZ = GetGridCoord(center.z - radius, cellSize, resolution);
            int maxZ = GetGridCoord(center.z + radius, cellSize, resolution);
            float radiusSqr = radius * radius;
            int traversalLimit = Math.Min(buffer.Length, GridTraversalLimit);

            for (int gridZ = minZ; gridZ <= maxZ; gridZ++)
            {
                int rowOffset = gridZ * resolution;
                for (int gridX = minX; gridX <= maxX; gridX++)
                {
                    ushort instanceId = citizenManager.m_citizenGrid[rowOffset + gridX];
                    int traversed = 0;
                    while (instanceId != 0 && traversed++ < traversalLimit)
                    {
                        if (instanceId >= buffer.Length)
                            break;

                        CitizenInstance instance = buffer[instanceId];
                        ushort nextInstanceId = instance.m_nextGridInstance;
                        CountPedestrianIfNear(instance, center, radiusSqr, ref counts);
                        instanceId = nextInstanceId;
                    }
                }
            }

            return true;
        }

        private static void CountPedestrianIfNear(CitizenInstance instance, Vector3 center, float radiusSqr, ref TrafficCounts counts)
        {
            if (!IsPedestrianCandidate(instance))
                return;

            Vector3 position = instance.GetLastFramePosition();
            if (HorizontalSqrDistance(position, center) > radiusSqr)
                return;

            counts.Pedestrians++;
        }

        private static bool IsPedestrianCandidate(CitizenInstance instance)
        {
            CitizenInstance.Flags flags = instance.m_flags;
            return (flags & CitizenInstance.Flags.Created) != 0
                   && (flags & (CitizenInstance.Flags.Deleted
                                | CitizenInstance.Flags.InsideBuilding
                                | CitizenInstance.Flags.WaitingTransport
                                | CitizenInstance.Flags.WaitingTaxi
                                | CitizenInstance.Flags.EnteringVehicle
                                | CitizenInstance.Flags.SittingDown)) == 0;
        }

        private static bool IsWaitingPedestrian(CitizenInstance instance)
        {
            CitizenInstance.Flags flags = instance.m_flags;
            if ((flags & (CitizenInstance.Flags.WaitingPath | CitizenInstance.Flags.BoredOfWaiting)) != 0)
                return true;

            if (instance.m_waitCounter > 0)
                return true;

            Vector3 velocity = instance.GetLastFrameData().m_velocity;
            velocity.y = 0f;
            return velocity.sqrMagnitude <= SlowPedestrianSpeedSqr;
        }

        private static Vector3 GetSegmentSamplePosition(NetManager netManager, ref NetSegment segment, float t)
        {
            Vector3 start = netManager.m_nodes.m_buffer[segment.m_startNode].m_position;
            Vector3 end = netManager.m_nodes.m_buffer[segment.m_endNode].m_position;
            Vector3 linear = Vector3.Lerp(start, end, Mathf.Clamp01(t));
            return segment.GetClosestPosition(linear);
        }

        private static int GetGridCoord(float value, float cellSize, int resolution)
        {
            return Mathf.Clamp((int)((value / cellSize) + (resolution * 0.5f)), 0, resolution - 1);
        }

        private static float DistanceToSegment2D(Vector3 point, Vector3 start, Vector3 end, out float along)
        {
            Vector2 p = new Vector2(point.x, point.z);
            Vector2 a = new Vector2(start.x, start.z);
            Vector2 b = new Vector2(end.x, end.z);
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared <= 0.01f)
            {
                along = 0f;
                return Vector2.Distance(p, a);
            }

            along = Vector2.Dot(p - a, ab) / lengthSquared;
            Vector2 closest = a + ab * Mathf.Clamp01(along);
            return Vector2.Distance(p, closest);
        }

        private static float HorizontalSqrDistance(Vector3 first, Vector3 second)
        {
            float dx = first.x - second.x;
            float dz = first.z - second.z;
            return (dx * dx) + (dz * dz);
        }

        private static string FormatVector(Vector3 value)
        {
            return "("
                   + value.x.ToString("0.0")
                   + ", "
                   + value.y.ToString("0.0")
                   + ", "
                   + value.z.ToString("0.0")
                   + ")";
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x)
                   && !float.IsNaN(value.y)
                   && !float.IsNaN(value.z)
                   && !float.IsInfinity(value.x)
                   && !float.IsInfinity(value.y)
                   && !float.IsInfinity(value.z);
        }
    }
}
