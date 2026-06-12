using System;
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
                       + (Capped > 0 ? " Reached this run's Auto Scan limit of " + CrossingAutoScanPlanner.MaxPlannedPlacements + " crossings; run Auto Scan again for more." : string.Empty)
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
                   + (Capped > 0 ? " Reached this run's Auto Scan limit of " + CrossingAutoScanPlanner.MaxPlannedPlacements + " crossings; run Auto Scan again for more." : string.Empty);
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
        public const int MaxPlannedPlacements = 32;
        private const int MaxPlannedRemovals = 8;
        private const int ExistingAssetBufferSize = 2048;
        private const int MaxObservationCandidates = 1024;
        private const int GridTraversalLimit = 65536;
        private const int NetNodeSegmentSlotCount = 8;
        private const int ContinuousRoadTraversalLimit = 128;
        private const float ObservationSampleIntervalSeconds = 1f;

        private const float HotspotPedestrianRadius = 18f;
        private const float HotspotVehicleRadius = 26f;
        private const float SurfaceCrossingJamPedestrianRadius = 14f;
        private const float SurfaceCrossingJamVehicleRadius = 22f;
        private const float LongSegmentTraceRadius = 9f;
        private const float LongRoadPedestrianRadius = 32f;
        private const float LongSegmentMinimumLength = 92f;
        private const float SignalRelocationMaxDistance = 140f;
        private const float SlowPedestrianSpeedSqr = 0.04f;
        private const float SlowVehicleSpeedSqr = 0.20f;
        private const float CitizenTargetNearJunctionDistance = 32f;

        private const int HotspotPedestrianThreshold = 8;
        private const int HotspotWaitingPedestrianThreshold = 3;
        private const int HotspotVehicleThreshold = 3;
        private const int CrossingJamPedestrianThreshold = 8;
        private const int CrossingJamVehicleThreshold = 3;
        private const int LongSegmentTracePedestrianThreshold = 4;
        private const int LongRoadPedestrianThreshold = 10;

        private static readonly CrossingPlacementAsset[] ExistingAssetBuffer = new CrossingPlacementAsset[ExistingAssetBufferSize];
        private static readonly float[] SurfaceCandidatePositions = new[] { 0.50f, 0.42f, 0.58f, 0.34f, 0.66f };
        private static readonly int[] ObservationOrderBuffer = new int[MaxObservationCandidates];

        private struct TrafficCounts
        {
            public int Pedestrians;
            public int WaitingPedestrians;
            public int Vehicles;
        }

        private enum ObservationCandidateKind
        {
            ImpactedJunction,
            ExistingSurfaceCrossing,
            LongRoadSegment
        }

        private struct ObservedTrafficCounts
        {
            public int Samples;
            public int PedestrianSightings;
            public int WaitingPedestrianSightings;
            public int VehicleSightings;
            public int PeakPedestrians;
            public int PeakWaitingPedestrians;
            public int PeakVehicles;

            public void Add(TrafficCounts counts)
            {
                Samples++;
                PedestrianSightings += counts.Pedestrians;
                WaitingPedestrianSightings += counts.WaitingPedestrians;
                VehicleSightings += counts.Vehicles;
                PeakPedestrians = Math.Max(PeakPedestrians, counts.Pedestrians);
                PeakWaitingPedestrians = Math.Max(PeakWaitingPedestrians, counts.WaitingPedestrians);
                PeakVehicles = Math.Max(PeakVehicles, counts.Vehicles);
            }

            public TrafficCounts ToUsageCounts()
            {
                TrafficCounts counts = new TrafficCounts();
                counts.Pedestrians = Math.Max(PeakPedestrians, PedestrianSightings);
                counts.WaitingPedestrians = Math.Max(PeakWaitingPedestrians, WaitingPedestrianSightings);
                counts.Vehicles = Math.Max(PeakVehicles, VehicleSightings);
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
            public ObservedTrafficCounts ObservedCounts;
        }

        public sealed class ObservationSession
        {
            private readonly ObservationCandidate[] _candidates = new ObservationCandidate[MaxObservationCandidates];
            private readonly float _durationSeconds;
            private float _elapsedSeconds;
            private float _nextSampleSeconds;
            private int _candidateCount;
            private int _junctionCandidateCount;
            private int _surfaceCandidateCount;
            private int _longRoadCandidateCount;

            public ObservationSession(float durationSeconds)
            {
                _durationSeconds = Mathf.Max(1f, durationSeconds);
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
                get { return _surfaceCandidateCount; }
            }

            public int LongRoadCandidateCount
            {
                get { return _longRoadCandidateCount; }
            }

            public int SampleCount { get; private set; }

            public float RemainingSeconds
            {
                get { return Mathf.Max(0f, _durationSeconds - _elapsedSeconds); }
            }

            public bool HasSamples
            {
                get { return SampleCount > 0; }
            }

            public bool Tick(float realTimeDelta)
            {
                if (IsComplete)
                    return true;

                _elapsedSeconds += Mathf.Max(0f, realTimeDelta);
                if (_elapsedSeconds + 0.0001f >= _nextSampleSeconds)
                {
                    Sample();
                    _nextSampleSeconds = _elapsedSeconds + ObservationSampleIntervalSeconds;
                }

                if (_elapsedSeconds >= _durationSeconds)
                    IsComplete = true;

                return IsComplete;
            }

            public string ToStatusString()
            {
                return "Scanning crossings, please wait: "
                       + Mathf.CeilToInt(RemainingSeconds)
                       + "s remaining. Monitoring "
                       + CandidateCount
                       + " candidate"
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

                RoadPlacementRules.ForceRefreshVanillaCrossingCache("auto-scan");
                ScanObservedImpactedJunctions(netManager, accumulator);
                ScanObservedLongRoadSegments(netManager, accumulator);
                ScanObservedSurfaceCrossings(netManager, accumulator);

                CrossingAutoScanPlan plan = accumulator.ToPlan();
                Debug.Log("[PedestrianCrossingToolkit] Auto scan planned from observation: samples="
                          + SampleCount
                          + " candidates="
                          + CandidateCount
                          + " "
                          + plan.Summary.ToLogString());
                return plan;
            }

            public void AddJunctionCandidate(ushort nodeId, ushort segmentId, RoadPlacementRules.VanillaCrossingPoint crossingPoint)
            {
                if (_candidateCount >= _candidates.Length)
                    return;

                for (int i = 0; i < _candidateCount; i++)
                {
                    if (_candidates[i].Kind == ObservationCandidateKind.ImpactedJunction
                        && _candidates[i].NodeId == nodeId
                        && _candidates[i].SegmentId == segmentId)
                    {
                        return;
                    }
                }

                _candidates[_candidateCount++] = new ObservationCandidate
                {
                    Kind = ObservationCandidateKind.ImpactedJunction,
                    NodeId = nodeId,
                    SegmentId = segmentId,
                    CrossingPoint = crossingPoint,
                    Center = crossingPoint.WorldPosition
                };
                _junctionCandidateCount++;
            }

            public void AddSurfaceCandidate(CrossingPlacementAsset asset)
            {
                if (_candidateCount >= _candidates.Length || asset.Id == 0 || !asset.Plan.IsValid)
                    return;

                _candidates[_candidateCount++] = new ObservationCandidate
                {
                    Kind = ObservationCandidateKind.ExistingSurfaceCrossing,
                    AssetId = asset.Id,
                    SegmentId = asset.Placement.SegmentId,
                    Center = asset.Plan.Center
                };
                _surfaceCandidateCount++;
            }

            public void AddLongRoadCandidate(ushort segmentId, Vector3 center)
            {
                if (_candidateCount >= _candidates.Length || segmentId == 0)
                    return;

                _candidates[_candidateCount++] = new ObservationCandidate
                {
                    Kind = ObservationCandidateKind.LongRoadSegment,
                    SegmentId = segmentId,
                    Center = center
                };
                _longRoadCandidateCount++;
            }

            private int BuildCandidateOrder(ObservationCandidateKind kind)
            {
                int count = 0;
                for (int i = 0; i < _candidateCount && count < ObservationOrderBuffer.Length; i++)
                {
                    if (_candidates[i].Kind == kind)
                        ObservationOrderBuffer[count++] = i;
                }

                for (int i = 1; i < count; i++)
                {
                    int current = ObservationOrderBuffer[i];
                    float currentScore = GetObservedScore(_candidates[current]);
                    int j = i - 1;
                    while (j >= 0 && GetObservedScore(_candidates[ObservationOrderBuffer[j]]) < currentScore)
                    {
                        ObservationOrderBuffer[j + 1] = ObservationOrderBuffer[j];
                        j--;
                    }

                    ObservationOrderBuffer[j + 1] = current;
                }

                return count;
            }

            private float GetObservedScore(ObservationCandidate candidate)
            {
                TrafficCounts counts = candidate.ObservedCounts.ToUsageCounts();
                switch (candidate.Kind)
                {
                    case ObservationCandidateKind.ImpactedJunction:
                        return counts.Pedestrians + (counts.WaitingPedestrians * 2f) + (counts.Vehicles * 3f);
                    case ObservationCandidateKind.ExistingSurfaceCrossing:
                        return counts.Pedestrians + (counts.Vehicles * 2f);
                    case ObservationCandidateKind.LongRoadSegment:
                        return counts.Pedestrians;
                    default:
                        return 0f;
                }
            }

            private void ScanObservedImpactedJunctions(NetManager netManager, AutoScanAccumulator accumulator)
            {
                int count = BuildCandidateOrder(ObservationCandidateKind.ImpactedJunction);
                for (int i = 0; i < count && accumulator.HasPlacementCapacity(); i++)
                {
                    ObservationCandidate candidate = _candidates[ObservationOrderBuffer[i]];
                    accumulator.ScannedNodes++;
                    TrafficCounts counts = candidate.ObservedCounts.ToUsageCounts();
                    if (counts.Pedestrians < HotspotPedestrianThreshold
                        || counts.WaitingPedestrians < HotspotWaitingPedestrianThreshold
                        || counts.Vehicles < HotspotVehicleThreshold)
                    {
                        continue;
                    }

                    accumulator.Hotspots++;
                    CrossingPlacementRecord placement;
                    CrossingPlacementPlan plan;
                    if (TryCreateGradeSeparatedJunctionPlacement(candidate.NodeId, candidate.SegmentId, candidate.CrossingPoint.WorldPosition, accumulator, out placement, out plan))
                        accumulator.TryAddPlacement(placement, plan);
                    else
                        accumulator.Reject("no legal subway or bridge placement found at observed impacted junction");
                }
            }

            private void ScanObservedLongRoadSegments(NetManager netManager, AutoScanAccumulator accumulator)
            {
                int count = BuildCandidateOrder(ObservationCandidateKind.LongRoadSegment);
                for (int i = 0; i < count && accumulator.HasPlacementCapacity(); i++)
                {
                    ObservationCandidate candidate = _candidates[ObservationOrderBuffer[i]];
                    if (candidate.SegmentId == 0 || candidate.SegmentId >= netManager.m_segments.m_size)
                        continue;

                    ref NetSegment segment = ref netManager.m_segments.m_buffer[candidate.SegmentId];
                    if ((segment.m_flags & NetSegment.Flags.Created) == 0
                        || !RoadPlacementRules.AllowsSurfaceCrossing(candidate.SegmentId)
                        || segment.m_averageLength < LongSegmentMinimumLength)
                    {
                        continue;
                    }

                    accumulator.ScannedLongRoadSegments++;
                    TrafficCounts counts = candidate.ObservedCounts.ToUsageCounts();
                    if (counts.Pedestrians < LongRoadPedestrianThreshold)
                        continue;

                    accumulator.Hotspots++;
                    CrossingPlacementRecord placement;
                    CrossingPlacementPlan plan;
                    if (TryCreateSurfaceCompensationPlacement(netManager, candidate.SegmentId, ref segment, out placement, out plan))
                        accumulator.TryAddPlacement(placement, plan);
                    else
                        accumulator.Reject("no legal middle crossing found for observed long road");
                }
            }

            private void ScanObservedSurfaceCrossings(NetManager netManager, AutoScanAccumulator accumulator)
            {
                int count = BuildCandidateOrder(ObservationCandidateKind.ExistingSurfaceCrossing);
                for (int i = 0; i < count && accumulator.HasPlacementCapacity(); i++)
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
                    if (counts.Pedestrians < CrossingJamPedestrianThreshold || counts.Vehicles < CrossingJamVehicleThreshold)
                        continue;

                    CrossingPlacementRecord signalPlacement;
                    CrossingPlacementPlan signalPlan;
                    if (!TryFindLegalSignalReplacement(netManager, asset, out signalPlacement, out signalPlan))
                    {
                        accumulator.Reject("no legal nearby signal join found for observed busy surface crossing");
                        continue;
                    }

                    if (accumulator.TryAddPlacement(signalPlacement, signalPlan))
                        accumulator.TryAddRemoval(asset.Id);
                }
            }

            private void Sample()
            {
                for (int i = 0; i < _candidateCount; i++)
                {
                    ObservationCandidate candidate = _candidates[i];
                    TrafficCounts counts = candidate.Kind == ObservationCandidateKind.ImpactedJunction
                        ? CountTrafficNear(candidate.Center, HotspotPedestrianRadius, HotspotVehicleRadius)
                        : candidate.Kind == ObservationCandidateKind.ExistingSurfaceCrossing
                            ? CountTrafficNear(candidate.Center, SurfaceCrossingJamPedestrianRadius, SurfaceCrossingJamVehicleRadius)
                            : CountPedestriansNear(candidate.Center, LongRoadPedestrianRadius);
                    candidate.ObservedCounts.Add(counts);
                    _candidates[i] = candidate;
                }

                SampleCount++;
                Debug.Log("[PedestrianCrossingToolkit] Auto scan observation sample: samples="
                          + SampleCount
                          + " candidates="
                          + CandidateCount
                          + " remaining="
                          + RemainingSeconds.ToString("0.0"));
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

        private sealed class AutoScanAccumulator
        {
            public readonly CrossingPlacementRecord[] Placements = new CrossingPlacementRecord[MaxPlannedPlacements];
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

                if (placement.Mode == PedestrianToolMode.MidBlockCrossing
                    && IsContinuousRoadCrossingCovered(placement.SegmentId, Placements, PlacementCount))
                {
                    SkippedExisting++;
                    return false;
                }

                if (CrossingPlacementRegistry.HasSameModeAssetAt(placement) || HasSamePlannedModePlacement(placement))
                {
                    SkippedExisting++;
                    return false;
                }

                if (PlacementCount >= Placements.Length)
                {
                    Capped++;
                    return false;
                }

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

        private static bool IsAutoGradeSeparatedThroatCovered(
            CrossingPlacementRecord placement,
            CrossingPlacementPlan plan,
            CrossingPlacementRecord[] plannedPlacements,
            int plannedCount)
        {
            ushort targetNodeId = GetGradeSeparatedTargetNode(placement, plan);
            if (targetNodeId == 0)
                return false;

            int existingCount = CrossingPlacementRegistry.CopyTo(ExistingAssetBuffer);
            for (int i = 0; i < existingCount; i++)
            {
                CrossingPlacementAsset asset = ExistingAssetBuffer[i];
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

        private static bool IsContinuousRoadCrossingCovered(
            ushort segmentId,
            CrossingPlacementRecord[] plannedPlacements,
            int plannedCount)
        {
            if (segmentId == 0)
                return false;

            int existingCount = CrossingPlacementRegistry.CopyTo(ExistingAssetBuffer);
            for (int i = 0; i < existingCount; i++)
            {
                CrossingPlacementAsset asset = ExistingAssetBuffer[i];
                if (asset.Id == 0 || !IsAutoManagedRoadCrossingMode(asset.Placement.Mode))
                    continue;

                if (IsSameContinuousRoad(segmentId, asset.Placement.SegmentId)
                    || (asset.Placement.HasSecondaryPoint && IsSameContinuousRoad(segmentId, asset.Placement.SecondarySegmentId)))
                {
                    return true;
                }
            }

            if (plannedPlacements == null)
                return false;

            for (int i = 0; i < plannedCount && i < plannedPlacements.Length; i++)
            {
                CrossingPlacementRecord planned = plannedPlacements[i];
                if (!IsAutoManagedRoadCrossingMode(planned.Mode))
                    continue;

                if (IsSameContinuousRoad(segmentId, planned.SegmentId)
                    || (planned.HasSecondaryPoint && IsSameContinuousRoad(segmentId, planned.SecondarySegmentId)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAutoManagedRoadCrossingMode(PedestrianToolMode mode)
        {
            return mode == PedestrianToolMode.MidBlockCrossing
                   || mode == PedestrianToolMode.SignalCrossing
                   || IsGradeSeparatedMode(mode);
        }

        private static bool IsSameContinuousRoad(ushort startSegmentId, ushort targetSegmentId)
        {
            if (startSegmentId == 0 || targetSegmentId == 0)
                return false;

            if (startSegmentId == targetSegmentId)
                return true;

            NetManager netManager = NetManager.instance;
            NetSegment startSegment;
            NetSegment targetSegment;
            if (netManager == null
                || !TryGetCreatedRoadSegment(netManager, startSegmentId, out startSegment)
                || !TryGetCreatedRoadSegment(netManager, targetSegmentId, out targetSegment))
            {
                return false;
            }

            ushort[] queue = new ushort[ContinuousRoadTraversalLimit];
            int read = 0;
            int write = 0;
            queue[write++] = startSegmentId;

            while (read < write)
            {
                ushort currentSegmentId = queue[read++];
                if (currentSegmentId == targetSegmentId)
                    return true;

                NetSegment currentSegment;
                if (!TryGetCreatedRoadSegment(netManager, currentSegmentId, out currentSegment))
                    continue;

                QueueStraightRoadContinuation(netManager, currentSegmentId, ref currentSegment, currentSegment.m_startNode, queue, ref write);
                QueueStraightRoadContinuation(netManager, currentSegmentId, ref currentSegment, currentSegment.m_endNode, queue, ref write);
            }

            return false;
        }

        private static void QueueStraightRoadContinuation(
            NetManager netManager,
            ushort currentSegmentId,
            ref NetSegment currentSegment,
            ushort nodeId,
            ushort[] queue,
            ref int write)
        {
            if (queue == null || write >= queue.Length)
                return;

            ushort continuation;
            if (!TryGetStraightRoadContinuation(netManager, currentSegmentId, ref currentSegment, nodeId, out continuation))
                return;

            for (int i = 0; i < write; i++)
            {
                if (queue[i] == continuation)
                    return;
            }

            queue[write++] = continuation;
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

        public static ObservationSession BeginObservation(float durationSeconds)
        {
            ObservationSession session = new ObservationSession(durationSeconds);
            NetManager netManager = NetManager.instance;
            if (netManager == null || netManager.m_nodes == null || netManager.m_segments == null)
                return session;

            RoadPlacementRules.ForceRefreshVanillaCrossingCache("auto-scan-observation");
            CollectJunctionObservationCandidates(netManager, session);
            CollectSurfaceObservationCandidates(session);
            CollectLongRoadObservationCandidates(netManager, session);
            session.Tick(0f);
            Debug.Log("[PedestrianCrossingToolkit] Auto scan observation started: duration="
                      + durationSeconds.ToString("0.0")
                      + " candidates="
                      + session.CandidateCount
                      + " junctionCandidates="
                      + session.JunctionCandidateCount
                      + " surfaceCandidates="
                      + session.SurfaceCandidateCount
                      + " longRoadCandidates="
                      + session.LongRoadCandidateCount);
            return session;
        }

        public static CrossingAutoScanPlan Build()
        {
            return Build(null);
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

            RoadPlacementRules.ForceRefreshVanillaCrossingCache("auto-scan");
            if (observation != null && observation.HasSamples)
            {
                return observation.BuildPlan();
            }
            else
            {
                ScanImpactedJunctions(netManager, accumulator);
                ScanLongRoadSegments(netManager, accumulator);
                ScanExistingSurfaceCrossings(netManager, accumulator);
            }

            CrossingAutoScanPlan plan = accumulator.ToPlan();
            Debug.Log("[PedestrianCrossingToolkit] Auto scan planned: " + plan.Summary.ToLogString());
            return plan;
        }

        private static void CollectJunctionObservationCandidates(NetManager netManager, ObservationSession session)
        {
            for (ushort nodeId = 1; nodeId < netManager.m_nodes.m_size; nodeId++)
            {
                if (session.CandidateCount >= MaxObservationCandidates)
                    break;

                ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
                if ((node.m_flags & NetNode.Flags.Created) == 0 || !RoadPlacementRules.IsThreePlusJunctionNode(nodeId))
                    continue;

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
                    if (RoadPlacementRules.TryGetActualVanillaCrossingPoint(segmentId, isEnd, out crossingPoint))
                        session.AddJunctionCandidate(nodeId, segmentId, crossingPoint);
                }
            }
        }

        private static void CollectSurfaceObservationCandidates(ObservationSession session)
        {
            int count = CrossingPlacementRegistry.CopyTo(ExistingAssetBuffer);
            for (int i = 0; i < count && session.CandidateCount < MaxObservationCandidates; i++)
            {
                CrossingPlacementAsset asset = ExistingAssetBuffer[i];
                if (asset.Id == 0 || !asset.Plan.IsValid)
                    continue;

                if (asset.Placement.Mode == PedestrianToolMode.MidBlockCrossing)
                    session.AddSurfaceCandidate(asset);
            }
        }

        private static void CollectLongRoadObservationCandidates(NetManager netManager, ObservationSession session)
        {
            for (ushort segmentId = 1; segmentId < netManager.m_segments.m_size && session.CandidateCount < MaxObservationCandidates; segmentId++)
            {
                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0
                    || !RoadPlacementRules.AllowsSurfaceCrossing(segmentId)
                    || segment.m_averageLength < LongSegmentMinimumLength)
                {
                    continue;
                }

                CrossingPlacementRecord placement;
                CrossingPlacementPlan plan;
                if (TryCreateSurfaceCompensationPlacement(netManager, segmentId, ref segment, out placement, out plan))
                    session.AddLongRoadCandidate(segmentId, placement.WorldPosition);
            }
        }

        private static void ScanImpactedJunctions(NetManager netManager, AutoScanAccumulator accumulator)
        {
            for (ushort nodeId = 1; nodeId < netManager.m_nodes.m_size; nodeId++)
            {
                if (!accumulator.HasPlacementCapacity())
                    break;

                ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
                if ((node.m_flags & NetNode.Flags.Created) == 0 || !RoadPlacementRules.IsThreePlusJunctionNode(nodeId))
                    continue;

                bool countedNode = false;
                for (int i = 0; i < NetNodeSegmentSlotCount && accumulator.HasPlacementCapacity(); i++)
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
                    if (!RoadPlacementRules.TryGetActualVanillaCrossingPoint(segmentId, isEnd, out crossingPoint))
                        continue;

                    if (!countedNode)
                    {
                        accumulator.ScannedNodes++;
                        countedNode = true;
                    }

                    TrafficCounts counts = CountTrafficNear(crossingPoint.WorldPosition, HotspotPedestrianRadius, HotspotVehicleRadius);
                    if (counts.Pedestrians < HotspotPedestrianThreshold
                        || counts.WaitingPedestrians < HotspotWaitingPedestrianThreshold
                        || counts.Vehicles < HotspotVehicleThreshold)
                    {
                        continue;
                    }

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
            for (ushort segmentId = 1; segmentId < netManager.m_segments.m_size; segmentId++)
            {
                if (!accumulator.HasPlacementCapacity())
                    break;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0
                    || !RoadPlacementRules.AllowsSurfaceCrossing(segmentId)
                    || segment.m_averageLength < LongSegmentMinimumLength)
                {
                    continue;
                }

                accumulator.ScannedLongRoadSegments++;
                CrossingPlacementRecord placement;
                CrossingPlacementPlan plan;
                if (!TryCreateSurfaceCompensationPlacement(netManager, segmentId, ref segment, out placement, out plan))
                    continue;

                TrafficCounts counts = CountPedestriansNear(placement.WorldPosition, LongRoadPedestrianRadius);
                if (counts.Pedestrians < LongRoadPedestrianThreshold)
                    continue;

                accumulator.Hotspots++;
                if (!accumulator.TryAddPlacement(placement, plan))
                    accumulator.Reject("long road already has an auto-managed crossing");
            }
        }

        private static void ScanExistingSurfaceCrossings(NetManager netManager, AutoScanAccumulator accumulator)
        {
            int count = CrossingPlacementRegistry.CopyTo(ExistingAssetBuffer);
            for (int i = 0; i < count; i++)
            {
                if (!accumulator.HasPlacementCapacity())
                    break;

                CrossingPlacementAsset asset = ExistingAssetBuffer[i];
                if (asset.Id == 0 || asset.Placement.Mode != PedestrianToolMode.MidBlockCrossing || !asset.Plan.IsValid)
                    continue;

                accumulator.ScannedExistingCrossings++;
                TrafficCounts counts = CountTrafficNear(asset.Plan.Center, SurfaceCrossingJamPedestrianRadius, SurfaceCrossingJamVehicleRadius);
                if (counts.Pedestrians < CrossingJamPedestrianThreshold || counts.Vehicles < CrossingJamVehicleThreshold)
                    continue;

                CrossingPlacementRecord signalPlacement;
                CrossingPlacementPlan signalPlan;
                if (!TryFindLegalSignalReplacement(netManager, asset, out signalPlacement, out signalPlan))
                {
                    accumulator.Reject("no legal nearby signal join found for busy surface crossing");
                    continue;
                }

                if (accumulator.TryAddPlacement(signalPlacement, signalPlan))
                    accumulator.TryAddRemoval(asset.Id);
            }
        }

        private static bool TryGetBestVanillaCrossingAtNode(
            NetManager netManager,
            ushort nodeId,
            out ushort segmentId,
            out RoadPlacementRules.VanillaCrossingPoint crossingPoint)
        {
            segmentId = 0;
            crossingPoint = default(RoadPlacementRules.VanillaCrossingPoint);
            float bestScore = float.MinValue;
            bool found = false;
            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            for (int i = 0; i < NetNodeSegmentSlotCount; i++)
            {
                ushort candidateSegmentId = node.GetSegment(i);
                if (candidateSegmentId == 0 || candidateSegmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[candidateSegmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || !RoadPlacementRules.IsRoadGradeSeparatedPlacementTarget(candidateSegmentId))
                    continue;

                bool isEnd = segment.m_endNode == nodeId;
                if (!isEnd && segment.m_startNode != nodeId)
                    continue;

                RoadPlacementRules.VanillaCrossingPoint candidate;
                if (!RoadPlacementRules.TryGetActualVanillaCrossingPoint(candidateSegmentId, isEnd, out candidate))
                    continue;

                TrafficCounts counts = CountTrafficNear(candidate.WorldPosition, HotspotPedestrianRadius, HotspotVehicleRadius);
                float score = counts.Pedestrians + counts.WaitingPedestrians + (counts.Vehicles * 2f);
                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    segmentId = candidateSegmentId;
                    crossingPoint = candidate;
                }
            }

            return found;
        }

        private static void AddLongSegmentCompensationCrossings(NetManager netManager, ushort nodeId, AutoScanAccumulator accumulator)
        {
            if (!accumulator.HasPlacementCapacity() || nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return;

            ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
            for (int i = 0; i < NetNodeSegmentSlotCount && accumulator.HasPlacementCapacity(); i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                    continue;

                ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0
                    || !RoadPlacementRules.AllowsSurfaceCrossing(segmentId)
                    || segment.m_averageLength < LongSegmentMinimumLength)
                {
                    continue;
                }

                int tracePedestrians = CountPedestriansTracingLongSegment(netManager, segmentId, ref segment, nodeId);
                if (tracePedestrians < LongSegmentTracePedestrianThreshold)
                    continue;

                CrossingPlacementRecord placement;
                CrossingPlacementPlan plan;
                if (TryCreateSurfaceCompensationPlacement(netManager, segmentId, ref segment, out placement, out plan))
                    accumulator.TryAddPlacement(placement, plan);
                else
                    accumulator.Reject("no legal mid-segment compensation crossing found");
            }
        }

        private static void RememberCompensationNode(ushort[] nodeIds, ref int count, ushort nodeId)
        {
            if (nodeIds == null || nodeId == 0 || count >= nodeIds.Length)
                return;

            for (int i = 0; i < count; i++)
            {
                if (nodeIds[i] == nodeId)
                    return;
            }

            nodeIds[count++] = nodeId;
        }

        private static void AddDeferredLongSegmentCompensationCrossings(
            NetManager netManager,
            ushort[] nodeIds,
            int count,
            AutoScanAccumulator accumulator)
        {
            if (netManager == null || nodeIds == null || count <= 0 || !accumulator.HasPlacementCapacity())
                return;

            int max = Math.Min(count, nodeIds.Length);
            for (int i = 0; i < max && accumulator.HasPlacementCapacity(); i++)
                AddLongSegmentCompensationCrossings(netManager, nodeIds[i], accumulator);
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

            int existingCount = CrossingPlacementRegistry.CopyTo(ExistingAssetBuffer);
            for (int i = 0; i < existingCount; i++)
            {
                CrossingPlacementAsset asset = ExistingAssetBuffer[i];
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

        private static TrafficCounts CountTrafficNear(Vector3 center, float pedestrianRadius, float vehicleRadius)
        {
            TrafficCounts counts = new TrafficCounts();
            CountPedestriansNear(center, pedestrianRadius, ref counts);
            CountVehiclesNear(center, vehicleRadius, ref counts);
            return counts;
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
            if (IsWaitingPedestrian(instance))
                counts.WaitingPedestrians++;
        }

        private static void CountVehiclesNear(Vector3 center, float radius, ref TrafficCounts counts)
        {
            VehicleManager vehicleManager = VehicleManager.instance;
            if (vehicleManager == null || vehicleManager.m_vehicles == null || vehicleManager.m_vehicles.m_buffer == null)
                return;

            Vehicle[] buffer = vehicleManager.m_vehicles.m_buffer;
            if (TryCountVehiclesInGrid(vehicleManager, buffer, center, radius, ref counts))
                return;

            uint max = Math.Min(vehicleManager.m_vehicles.m_size, (uint)buffer.Length);
            float radiusSqr = radius * radius;
            for (uint i = 1; i < max && i <= ushort.MaxValue; i++)
                CountVehicleIfNear(buffer[(int)i], center, radiusSqr, ref counts);
        }

        private static bool TryCountVehiclesInGrid(
            VehicleManager vehicleManager,
            Vehicle[] buffer,
            Vector3 center,
            float radius,
            ref TrafficCounts counts)
        {
            if (vehicleManager.m_vehicleGrid == null || buffer == null)
                return false;

            int resolution = VehicleManager.VEHICLEGRID_RESOLUTION;
            float cellSize = VehicleManager.VEHICLEGRID_CELL_SIZE;
            if (resolution <= 0 || cellSize <= 0f || vehicleManager.m_vehicleGrid.Length < resolution * resolution)
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
                    ushort vehicleId = vehicleManager.m_vehicleGrid[rowOffset + gridX];
                    int traversed = 0;
                    while (vehicleId != 0 && traversed++ < traversalLimit)
                    {
                        if (vehicleId >= buffer.Length)
                            break;

                        Vehicle vehicle = buffer[vehicleId];
                        ushort nextVehicleId = vehicle.m_nextGridVehicle;
                        CountVehicleIfNear(vehicle, center, radiusSqr, ref counts);
                        vehicleId = nextVehicleId;
                    }
                }
            }

            return true;
        }

        private static void CountVehicleIfNear(Vehicle vehicle, Vector3 center, float radiusSqr, ref TrafficCounts counts)
        {
            if (!IsVehicleCandidate(vehicle))
                return;

            if (HorizontalSqrDistance(vehicle.GetLastFramePosition(), center) > radiusSqr)
                return;

            if (IsTrafficImpactedVehicle(vehicle))
                counts.Vehicles++;
        }

        private static int CountPedestriansTracingLongSegment(NetManager netManager, ushort segmentId, ref NetSegment segment, ushort targetNodeId)
        {
            CitizenManager citizenManager = CitizenManager.instance;
            if (citizenManager == null || citizenManager.m_instances == null || citizenManager.m_instances.m_buffer == null)
                return 0;

            CitizenInstance[] buffer = citizenManager.m_instances.m_buffer;
            uint max = Math.Min(citizenManager.m_instances.m_size, (uint)buffer.Length);
            int count = 0;
            for (uint i = 1; i < max; i++)
            {
                CitizenInstance instance = buffer[i];
                if (!IsPedestrianCandidate(instance))
                    continue;

                Vector3 position = instance.GetLastFramePosition();
                float along;
                if (DistanceToSegment2D(position, netManager.m_nodes.m_buffer[segment.m_startNode].m_position, netManager.m_nodes.m_buffer[segment.m_endNode].m_position, out along) > LongSegmentTraceRadius)
                    continue;

                if (along < 0.18f || along > 0.82f)
                    continue;

                if (!IsWaitingPedestrian(instance) && !IsCitizenTargetNearNode(netManager, instance, targetNodeId))
                    continue;

                count++;
                if (count >= LongSegmentTracePedestrianThreshold)
                    return count;
            }

            return count;
        }

        private static bool IsCitizenTargetNearNode(NetManager netManager, CitizenInstance instance, ushort nodeId)
        {
            if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                return false;

            Vector4 target = instance.m_targetPos;
            Vector3 targetPosition = new Vector3(target.x, target.y, target.z);
            if (!IsFinite(targetPosition))
                return false;

            return HorizontalSqrDistance(targetPosition, netManager.m_nodes.m_buffer[nodeId].m_position)
                   <= CitizenTargetNearJunctionDistance * CitizenTargetNearJunctionDistance;
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

        private static bool IsVehicleCandidate(Vehicle vehicle)
        {
            Vehicle.Flags flags = vehicle.m_flags;
            return (flags & Vehicle.Flags.Created) != 0
                   && (flags & (Vehicle.Flags.Deleted
                                | Vehicle.Flags.Flying
                                | Vehicle.Flags.TakingOff
                                | Vehicle.Flags.Landing
                                | Vehicle.Flags.InsideBuilding
                                | Vehicle.Flags.Parking)) == 0;
        }

        private static bool IsTrafficImpactedVehicle(Vehicle vehicle)
        {
            if ((vehicle.m_flags & (Vehicle.Flags.Stopped | Vehicle.Flags.Congestion | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace)) != 0)
                return true;

            if (vehicle.m_waitCounter >= 8)
                return true;

            Vector3 velocity = vehicle.GetLastFrameVelocity();
            velocity.y = 0f;
            return velocity.sqrMagnitude <= SlowVehicleSpeedSqr;
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
