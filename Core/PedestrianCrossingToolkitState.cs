using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public static class PedestrianCrossingToolkitState
    {
        public static bool Enabled { get; set; }
        public static PedestrianToolMode ActiveMode { get; private set; }
        public static CrossingPlacementRecord LastPreview { get; private set; }
        public static CrossingPlacementRecord LastPlacement { get; private set; }
        public static CrossingPlacementPlan LastPlacementPlan { get; private set; }
        public static CrossingPlacementAsset LastAsset { get; private set; }
        public static string StatusMessage { get; private set; }
        public static bool TrafficManagerInteropAllowed { get; private set; }
        private static bool _deferredLoadRebuildPending;
        private static float _deferredLoadRebuildElapsed;
        private static int _deferredLoadRebuildReadyFrames;
        private static int _deferredLoadRebuildAssetCount;
        private static int _deferredLoadRebuildAssetIndex;
        private static int _deferredLoadRebuildBuilt;
        private static int _deferredLoadRebuildSkipped;
        private static float _deferredLoadRebuildWorkStartedAt;
        private static float _deferredLoadRebuildWorkElapsedMs;
        private static bool _deferredLoadRebuildBatchActive;
        private static bool _deferredLoadRebuildManaged;
        private static bool _deferredNetworkDependencyCleanupPending;
        private static float _deferredNetworkDependencyCleanupElapsed;
        private static int _deferredNetworkDependencyCleanupReadyFrames;
        private static uint _deferredNetworkDependencyLastBuildIndex;
        private static int _deferredNetworkDependencyRemovalCount;
        private const float LoadRebuildMinimumDelaySeconds = 1.5f;
        private const float LoadRebuildFallbackDelaySeconds = 8f;
        private const int LoadRebuildStableFrameCount = 30;
        private const float LoadRebuildSlowAssetWarningMs = 100f;
        private const float NetworkDependencyCleanupMinimumDelaySeconds = 2.0f;
        private const float NetworkDependencyCleanupFallbackDelaySeconds = 12f;
        private const int NetworkDependencyCleanupStableFrameCount = 45;
        private const float NetworkDependencyRoadWidthChangeTolerance = 1f;
        private const float AutoScanObservationSeconds = 60f;
        private const float NetworkDependencyScanSeconds = 1f;
        private const int NetworkDependencyAssetBufferSize = 4096;
        private const int NetworkDependencyMaxTouchedSegments = 16;
        private const int ValidationAssetBufferSize = 4096;
        private const int ValidationAssetIdBufferSize = 4096;
        private const int ValidationProblemAssetBufferSize = 4096;
        private const int AutoScanChangedAssetBufferSize = 4096;
        private const float AutoScanPreviewPickRadiusPixels = 44f;
        private const int ValidationMaxDetailLogs = 12;
        private const string AutoScanPreviewInstructionsSuppressedKey = "PedestrianCrossingToolkit.AutoScanPreviewInstructionsSuppressed";
        private static CrossingAutoScanPlanner.ObservationSession _autoScanObservation;
        private static int _autoScanObservationStatusSecond = -1;
        private static bool _autoScanObservationManaged;
        private static float _autoScanManagedLastRealtime;
        private static bool _autoScanPreviewConfirmEnabled;
        private static bool _autoScanPreviewInstructionsSuppressedLoaded;
        private static bool _autoScanPreviewInstructionsSuppressed;
        private static CrossingAutoScanPlan _autoScanPreviewPlan = CrossingAutoScanPlan.Empty;
        private static int _autoScanPreviewProposalCount;
        private static int _autoScanPreviewAcceptedCount;
        private static int _autoScanPreviewRejectedCount;
        private static int _autoScanPreviewRevision;
        private static float _networkDependencyScanTimer;
        private static readonly CrossingPlacementAsset[] ClearPlacementAssets = new CrossingPlacementAsset[4096];
        private static readonly CrossingPlacementAsset[] NetworkDependencyAssetBuffer = new CrossingPlacementAsset[NetworkDependencyAssetBufferSize];
        private static readonly int[] NetworkDependencyRemovalIds = new int[NetworkDependencyAssetBufferSize];
        private static readonly int[] DeferredNetworkDependencyRemovalIds = new int[NetworkDependencyAssetBufferSize];
        private static readonly CrossingPlacementAsset[] ValidationAssetBuffer = new CrossingPlacementAsset[ValidationAssetBufferSize];
        private static readonly int[] ValidationAssetIdBuffer = new int[ValidationAssetIdBufferSize];
        private static readonly int[] ValidationProblemAssetIds = new int[ValidationProblemAssetBufferSize];
        private static readonly int[] AutoScanChangedAssetIds = new int[AutoScanChangedAssetBufferSize];
        private static readonly bool[] AutoScanPreviewAccepted = new bool[CrossingAutoScanPlanner.MaxPlannedPlacements];
        private static readonly Dictionary<int, NetworkDependencySnapshot> NetworkDependencySnapshots = new Dictionary<int, NetworkDependencySnapshot>();
        private static readonly List<int> StaleNetworkDependencySnapshotIds = new List<int>();
        private static int _validationProblemAssetCount;
        private static int _validationProblemRevision;
        private static bool _hasLastValidationSummary;
        private static CrossingValidationSummary _lastValidationSummary = CrossingValidationSummary.Empty;

        public static bool IsAutoScanObservationActive
        {
            get { return _autoScanObservation != null; }
        }

        public static bool AutoScanPreviewConfirmEnabled
        {
            get { return _autoScanPreviewConfirmEnabled; }
        }

        public static bool AutoScanPreviewInstructionsSuppressed
        {
            get
            {
                EnsureAutoScanPreviewInstructionsPreferenceLoaded();
                return _autoScanPreviewInstructionsSuppressed;
            }
        }

        public static bool HasAutoScanPreviewPlan
        {
            get { return _autoScanPreviewProposalCount > 0; }
        }

        public static int AutoScanPreviewProposalCount
        {
            get { return _autoScanPreviewProposalCount; }
        }

        public static int AutoScanPreviewAcceptedCount
        {
            get { return _autoScanPreviewAcceptedCount; }
        }

        public static int AutoScanPreviewRejectedCount
        {
            get { return _autoScanPreviewRejectedCount; }
        }

        public static int AutoScanPreviewRevision
        {
            get { return _autoScanPreviewRevision; }
        }

        public static bool HasValidationProblemAssets
        {
            get { return _validationProblemAssetCount > 0; }
        }

        public static int ValidationProblemRevision
        {
            get { return _validationProblemRevision; }
        }

        public static int CopyValidationProblemAssetsTo(CrossingPlacementAsset[] buffer)
        {
            if (buffer == null)
                return 0;

            int copied = 0;
            for (int i = 0; i < _validationProblemAssetCount && copied < buffer.Length; i++)
            {
                CrossingPlacementAsset asset;
                if (CrossingPlacementRegistry.TryGetAssetById(ValidationProblemAssetIds[i], out asset))
                    buffer[copied++] = asset;
            }

            return copied;
        }

        public static void SetAutoScanPreviewConfirmEnabled(bool enabled)
        {
            _autoScanPreviewConfirmEnabled = enabled;
            if (!enabled && HasAutoScanPreviewPlan)
                ClearAutoScanPreviewPlan(false);
        }

        public static void SetAutoScanPreviewInstructionsSuppressed(bool suppressed)
        {
            EnsureAutoScanPreviewInstructionsPreferenceLoaded();
            _autoScanPreviewInstructionsSuppressed = suppressed;
            PlayerPrefs.SetInt(AutoScanPreviewInstructionsSuppressedKey, suppressed ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static int CopyAutoScanPreviewPlacementsTo(CrossingPlacementRecord[] placements, int[] proposalIndices)
        {
            if (placements == null)
                return 0;

            int copied = 0;
            int max = Mathf.Min(_autoScanPreviewProposalCount, _autoScanPreviewPlan.PlacementCount);
            for (int i = 0; i < max && copied < placements.Length; i++)
            {
                if (!AutoScanPreviewAccepted[i])
                    continue;

                CrossingPlacementRecord placement = _autoScanPreviewPlan.Placements[i];
                if (!placement.IsValid)
                    continue;

                placements[copied] = placement;
                if (proposalIndices != null && copied < proposalIndices.Length)
                    proposalIndices[copied] = i;
                copied++;
            }

            return copied;
        }

        public static bool TryGetAutoScanPreviewProposalNearScreen(Camera camera, Vector2 screenPosition, out int proposalIndex, out CrossingPlacementRecord placement, out CrossingPlacementPlan plan)
        {
            proposalIndex = -1;
            placement = CrossingPlacementRecord.None;
            plan = CrossingPlacementPlan.Invalid;
            if (camera == null || !HasAutoScanPreviewPlan)
                return false;

            float bestDistanceSqr = AutoScanPreviewPickRadiusPixels * AutoScanPreviewPickRadiusPixels;
            int max = Mathf.Min(_autoScanPreviewProposalCount, _autoScanPreviewPlan.PlacementCount);
            for (int i = 0; i < max; i++)
            {
                if (!AutoScanPreviewAccepted[i])
                    continue;

                CrossingPlacementRecord candidate = _autoScanPreviewPlan.Placements[i];
                if (!candidate.IsValid)
                    continue;

                CrossingPlacementPlan candidatePlan = CrossingPlacementPlanner.Build(candidate);
                if (!candidatePlan.IsValid)
                    continue;

                Vector3 screen = camera.WorldToScreenPoint(candidatePlan.Center);
                if (screen.z <= 0f)
                    continue;

                float dx = screen.x - screenPosition.x;
                float dy = screen.y - screenPosition.y;
                float distanceSqr = dx * dx + dy * dy;
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                proposalIndex = i;
                placement = candidate;
                plan = candidatePlan;
            }

            return proposalIndex >= 0;
        }

        public static bool RejectAutoScanPreviewProposal(int proposalIndex)
        {
            if (proposalIndex < 0
                || proposalIndex >= _autoScanPreviewProposalCount
                || proposalIndex >= AutoScanPreviewAccepted.Length
                || !AutoScanPreviewAccepted[proposalIndex])
            {
                StatusMessage = "Click a yellow Auto Scan preview marker to reject it.";
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return false;
            }

            AutoScanPreviewAccepted[proposalIndex] = false;
            _autoScanPreviewAcceptedCount = Mathf.Max(0, _autoScanPreviewAcceptedCount - 1);
            _autoScanPreviewRejectedCount++;
            _autoScanPreviewRevision++;
            CrossingPlacementRecord placement = _autoScanPreviewPlan.Placements[proposalIndex];
            StatusMessage = "Rejected Auto Scan " + GetModeLabel(placement.Mode) + ". "
                            + _autoScanPreviewAcceptedCount
                            + " proposal"
                            + (_autoScanPreviewAcceptedCount == 1 ? string.Empty : "s")
                            + " remain.";
            PedestrianCrossingToolkitPanel.RefreshInstance();
            Debug.Log("[PedestrianCrossingToolkit] Auto scan preview proposal rejected: index="
                      + proposalIndex
                      + " mode="
                      + placement.Mode
                      + " segment="
                      + placement.SegmentId
                      + " acceptedRemaining="
                      + _autoScanPreviewAcceptedCount
                      + " rejected="
                      + _autoScanPreviewRejectedCount);
            return true;
        }

        public static CrossingAutoScanSummary ApplyAutoScanPreview()
        {
            if (!HasAutoScanPreviewPlan)
            {
                StatusMessage = "No Auto Scan preview is waiting to apply.";
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return CrossingAutoScanSummary.Empty;
            }

            CrossingAutoScanPlan plan = BuildAcceptedAutoScanPreviewPlan();
            if (!plan.HasWork)
            {
                StatusMessage = "All Auto Scan proposals were rejected. Cancel preview or run Auto Scan again.";
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return CrossingAutoScanSummary.Empty;
            }

            ClearAutoScanPreviewPlan(false);
            return ApplyAutoScanPlan(plan, "auto-scan-preview");
        }

        public static void CancelAutoScanPreview()
        {
            if (!HasAutoScanPreviewPlan)
                return;

            int rejected = _autoScanPreviewRejectedCount;
            ClearAutoScanPreviewPlan(false);
            StatusMessage = rejected > 0
                ? "Auto Scan preview cancelled after rejecting " + rejected + " proposal" + (rejected == 1 ? string.Empty : "s") + "."
                : "Auto Scan preview cancelled.";
            PedestrianCrossingToolkitPanel.RefreshInstance();
        }

        private struct SegmentDependencySignature
        {
            public bool IsValid;
            public ushort SegmentId;
            public ushort StartNode;
            public ushort EndNode;
            public int PrefabId;
            public float RoadWidth;
            public bool HasPavement;
            public int StartX;
            public int StartY;
            public int StartZ;
            public int EndX;
            public int EndY;
            public int EndZ;
            public int StartDirectionX;
            public int StartDirectionZ;
            public int EndDirectionX;
            public int EndDirectionZ;
            public int AverageLength;

            public bool Equals(SegmentDependencySignature other)
            {
                return IsValid == other.IsValid
                       && SegmentId == other.SegmentId
                       && StartNode == other.StartNode
                       && EndNode == other.EndNode
                       && PrefabId == other.PrefabId
                       && Mathf.Abs(RoadWidth - other.RoadWidth) <= 0.01f
                       && HasPavement == other.HasPavement
                       && StartX == other.StartX
                       && StartY == other.StartY
                       && StartZ == other.StartZ
                       && EndX == other.EndX
                       && EndY == other.EndY
                       && EndZ == other.EndZ
                       && StartDirectionX == other.StartDirectionX
                       && StartDirectionZ == other.StartDirectionZ
                       && EndDirectionX == other.EndDirectionX
                       && EndDirectionZ == other.EndDirectionZ
                       && AverageLength == other.AverageLength;
            }
        }

        private sealed class NetworkDependencySnapshot
        {
            public int AssetId;
            public int SegmentCount;
            public readonly SegmentDependencySignature[] Segments = new SegmentDependencySignature[NetworkDependencyMaxTouchedSegments];

            public bool SameAs(NetworkDependencySnapshot other)
            {
                if (other == null || AssetId != other.AssetId || SegmentCount != other.SegmentCount)
                    return false;

                for (int i = 0; i < SegmentCount; i++)
                {
                    if (!Segments[i].Equals(other.Segments[i]))
                        return false;
                }

                return true;
            }

            public bool RequiresRemovalComparedTo(NetworkDependencySnapshot other, out string reason)
            {
                reason = string.Empty;
                if (other == null || AssetId != other.AssetId)
                    return false;

                for (int i = 0; i < SegmentCount; i++)
                {
                    SegmentDependencySignature current = Segments[i];
                    SegmentDependencySignature previous;
                    if (!TryGetSegment(other, current.SegmentId, out previous))
                        continue;

                    if (current.PrefabId != previous.PrefabId)
                    {
                        reason = "road-prefab-changed";
                        return true;
                    }

                    if (Mathf.Abs(current.RoadWidth - previous.RoadWidth) > NetworkDependencyRoadWidthChangeTolerance)
                    {
                        reason = "road-width-changed";
                        return true;
                    }

                    if (previous.HasPavement && !current.HasPavement)
                    {
                        reason = "road-lost-pavement";
                        return true;
                    }
                }

                return false;
            }

            private static bool TryGetSegment(NetworkDependencySnapshot snapshot, ushort segmentId, out SegmentDependencySignature signature)
            {
                if (snapshot != null)
                {
                    for (int i = 0; i < snapshot.SegmentCount; i++)
                    {
                        if (snapshot.Segments[i].SegmentId == segmentId)
                        {
                            signature = snapshot.Segments[i];
                            return true;
                        }
                    }
                }

                signature = default(SegmentDependencySignature);
                return false;
            }
        }

        public static void SetActiveMode(PedestrianToolMode mode)
        {
            if (ActiveMode != mode || mode == PedestrianToolMode.None)
                SubwayPointToPointTool.Reset();

            ActiveMode = mode;
            if (mode == PedestrianToolMode.None)
                LastPreview = CrossingPlacementRecord.None;

            StatusMessage = mode == PedestrianToolMode.None
                ? "No pedestrian crossing tool selected."
                : mode == PedestrianToolMode.RemoveCrossing
                    ? "Remove mode selected. Hover an existing crossing and click to remove it."
                    : mode == PedestrianToolMode.AutoScanReject
                        ? "Reject Proposal selected. Click a yellow Auto Scan preview marker to reject it."
                    : mode == PedestrianToolMode.SubwayPointToPoint
                        ? "Selected point-to-point subway. Click a road-side start entrance, then a road-side end entrance within 50f."
                        : mode == PedestrianToolMode.SubwayLink
                            ? "Selected subway. Hover a road, surface/elevated train track, or terrain-level/elevated metro track and click to place."
                        : mode == PedestrianToolMode.PedestrianBridge
                            ? "Selected pedestrian bridge. Hover a road, surface train track, or terrain-level metro track and click to place."
                        : "Selected " + GetModeLabel(mode) + ". Hover a road and click to place.";

            Debug.Log("[PedestrianCrossingToolkit] Active mode changed: " + mode);
            PedestrianCrossingToolkitPanel.RefreshInstance();
        }

        public static bool ConfirmSubwayPointToPointClick(CrossingPlacementRecord endpoint, out string blockedMessage)
        {
            blockedMessage = string.Empty;
            if (!endpoint.IsValid)
            {
                blockedMessage = endpoint.Message;
                StatusMessage = blockedMessage;
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return false;
            }

            if (!SubwayPointToPointTool.HasStartEndpoint)
            {
                SubwayPointToPointTool.SetStartEndpoint(endpoint);
                LastPlacement = CrossingPlacementRecord.None;
                LastPlacementPlan = CrossingPlacementPlan.Invalid;
                LastAsset = CrossingPlacementAsset.None;
                StatusMessage = "Start subway entrance selected. Click an end entrance within "
                                + SubwayPointToPointPlacementPlanner.MaxEntranceDistance.ToString("0")
                                + "f.";
                Debug.Log("[PedestrianCrossingToolkit] Point-to-point subway start selected: segment="
                          + endpoint.SegmentId
                          + " position="
                          + endpoint.SegmentPosition.ToString("0.000")
                          + " world="
                          + endpoint.WorldPosition);
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return true;
            }

            CrossingPlacementRecord placement;
            CrossingPlacementResult routeResult = SubwayPointToPointTool.TryCreateRoute(endpoint, out placement);
            if (!routeResult.Success)
            {
                blockedMessage = routeResult.Message;
                StatusMessage = blockedMessage;
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return false;
            }

            bool placed = ConfirmPlacement(placement, out blockedMessage);
            if (placed)
                SubwayPointToPointTool.Reset();

            return placed;
        }

        public static void SetPreview(CrossingPlacementRecord preview)
        {
            string statusMessage = preview.IsValid
                ? "Ready to place " + GetModeLabel(preview.Mode) + " at " + FormatPlacementLocation(preview) + "."
                : preview.Message;
            bool refreshPanel = !IsSamePreviewPanelState(LastPreview, preview)
                                || StatusMessage != statusMessage;

            LastPreview = preview;
            StatusMessage = statusMessage;

            if (refreshPanel)
                PedestrianCrossingToolkitPanel.RefreshInstance();
        }

        public static bool ConfirmPlacement(CrossingPlacementRecord placement, out string blockedMessage)
        {
            blockedMessage = string.Empty;
            CrossingPlacementPlan plan = CrossingPlacementPlanner.Build(placement);
            if (!plan.IsValid)
            {
                CrossingPlacementPolicyResult policy = CrossingPlacementPolicy.Evaluate(placement);
                LastPlacement = placement;
                LastPlacementPlan = CrossingPlacementPlan.Invalid;
                LastAsset = CrossingPlacementAsset.None;
                blockedMessage = policy.Success ? "Placement is no longer valid." : policy.Message;
                StatusMessage = blockedMessage;
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return false;
            }

            CrossingPlacementAsset existingAtPreview;
            if (CrossingPlacementRegistry.TryGetAssetAt(placement, plan, out existingAtPreview)
                && existingAtPreview.Placement.Mode == placement.Mode
                && placement.Mode == PedestrianToolMode.SignalCrossing)
            {
                LastPlacement = placement;
                LastPlacementPlan = CrossingPlacementPlan.Invalid;
                LastAsset = CrossingPlacementAsset.None;
                StatusMessage = "there's already a " + GetModeLabel(placement.Mode) + " here.";
                Debug.Log("[PedestrianCrossingToolkit] Placement ignored: same crossing type already exists at segment="
                          + placement.SegmentId
                          + " position="
                          + placement.SegmentPosition.ToString("0.000")
                          + " mode="
                          + placement.Mode
                          + " existingAsset="
                          + existingAtPreview.Id);
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return true;
            }

            CrossingPlacementPlan adjustedPlan;
            string adjustmentMessage;
            if (!CrossingPlacementConflictValidator.TryValidateAndAdjust(placement, plan, out adjustedPlan, out adjustmentMessage))
            {
                LastPlacement = placement;
                LastPlacementPlan = CrossingPlacementPlan.Invalid;
                LastAsset = CrossingPlacementAsset.None;
                blockedMessage = adjustmentMessage;
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return false;
            }

            plan = adjustedPlan;
            CrossingPlacementAsset replaced;
            bool didReplace;
            SignalRoadStateSnapshot signalRoadState = CrossingPathBuilder.CaptureSignalRoadState(placement, plan);
            CrossingPlacementAsset asset = CrossingPlacementRegistry.AddOrReplace(placement, plan, signalRoadState, out replaced, out didReplace);
            RememberNetworkDependencySnapshot(asset, replaced);
            LastPlacement = placement;
            LastPlacementPlan = plan;
            LastAsset = asset;
            StatusMessage = placement.IsValid
                ? (didReplace ? "Updated " : "Added ")
                  + GetModeLabel(placement.Mode) + " at " + FormatPlacementLocation(placement) + FormatSurfaceSuppression(plan) + "."
                : placement.Message;
            if (!string.IsNullOrEmpty(adjustmentMessage))
                StatusMessage = StatusMessage + " " + adjustmentMessage;

            Debug.Log("[PedestrianCrossingToolkit] Placement added: mode=" + placement.Mode
                      + " asset=" + asset.Id
                      + " replaced=" + didReplace
                      + " replacedAsset=" + replaced.Id
                      + " segment=" + placement.SegmentId
                      + " position=" + placement.SegmentPosition.ToString("0.000")
                      + " nearNode=" + placement.NearNode
                      + " slot=" + FormatPlacementSlot(placement)
                      + " world=" + placement.WorldPosition
                      + " planValid=" + plan.IsValid
                      + " width=" + plan.Width.ToString("0.0")
                      + " targetNode=" + plan.TargetNodeId
                      + " connectedSegments=" + plan.ConnectedSegmentCount
                      + " junctionExits=" + plan.JunctionExitCount
                      + " suppressSurface=" + plan.SuppressSurfaceCrossing
                      + " flipBridgeAccess=" + plan.FlipBridgeAccess
                      + " application=" + plan.ApplicationKind
                      + " planCenter=" + plan.Center
                      + " left=" + plan.LeftEdge
                      + " right=" + plan.RightEdge);

            if (didReplace)
                CleanupRemovedAssetForIncrementalSync(replaced, "placement-replace");

            SyncBuiltStructures("placement", false, asset.Id);
            PedestrianCrossingToolkitPanel.RefreshInstance();
            return true;
        }

        private static void CleanupRemovedAssetForIncrementalSync(CrossingPlacementAsset replaced, string reason)
        {
            if (replaced.Id == 0)
                return;

            bool replacedSignalCrossing = replaced.Placement.Mode == PedestrianToolMode.SignalCrossing;
            bool gradeSeparatedReplaced = GradeSeparatedPlacementGeometryResolver.IsGradeSeparated(replaced.Plan.ApplicationKind);
            int builtRemoved = 0;
            int surfaceControlsRefreshed = 0;
            int signalControllersRemoved = 0;
            int signalRoadStatesRestored = 0;
            bool preparedSignalStateRestore = false;
            float startedAt = Time.realtimeSinceStartup;
            try
            {
                NetworkDependencySnapshots.Remove(replaced.Id);
                if (replacedSignalCrossing)
                {
                    CrossingPathBuilder.PrepareSignalControllerStateRestoreExcept(replaced.Id, reason);
                    preparedSignalStateRestore = true;
                }

                builtRemoved = CrossingPathBuilder.RemoveBuiltPathsForAsset(replaced.Id, reason);
                surfaceControlsRefreshed = CrossingPathBuilder.RefreshSurfaceCrossingControlForAssetRemoval(replaced, reason);
                if (replacedSignalCrossing)
                {
                    signalControllersRemoved = CrossingPathBuilder.RemoveSignalControllerForAsset(replaced.Id, reason);
                    signalRoadStatesRestored = CrossingPathBuilder.ClearSignalRoadStateForAsset(replaced, reason + "-targeted");
                }

                if (gradeSeparatedReplaced)
                    CrossingPathBuilder.MaintainSuppressedSurfaceCrossings();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PedestrianCrossingToolkit] Targeted asset cleanup failed: reason="
                               + reason
                               + " replacedAsset="
                               + replaced.Id
                               + " mode="
                               + replaced.Placement.Mode
                               + " error="
                               + e);
            }
            finally
            {
                if (preparedSignalStateRestore)
                    CrossingPathBuilder.ClearPreparedSignalControllerStateRestores();
            }

            Debug.Log("[PedestrianCrossingToolkit] Targeted asset cleanup complete: reason="
                      + reason
                      + " replacedAsset="
                      + replaced.Id
                      + " mode="
                      + replaced.Placement.Mode
                      + " builtRemoved="
                      + builtRemoved
                      + " surfaceControlsRefreshed="
                      + surfaceControlsRefreshed
                      + " signalControllersRemoved="
                      + signalControllersRemoved
                      + " signalRoadStatesRestored="
                      + signalRoadStatesRestored
                      + " suppressionReconciled="
                      + gradeSeparatedReplaced
                      + " elapsedMs="
                      + ((Time.realtimeSinceStartup - startedAt) * 1000f).ToString("0.0"));
        }

        public static bool ConfirmPlacements(CrossingPlacementRecord[] placements, int count, out string blockedMessage)
        {
            blockedMessage = string.Empty;
            if (placements == null || count <= 0)
            {
                blockedMessage = "No junction crossings were detected.";
                return false;
            }

            int added = 0;
            int skippedExisting = 0;
            int rejected = 0;
            CrossingPlacementAsset lastAdded = CrossingPlacementAsset.None;
            CrossingPlacementPlan lastPlan = CrossingPlacementPlan.Invalid;
            CrossingPlacementRecord lastPlacement = CrossingPlacementRecord.None;
            string firstRejection = string.Empty;
            bool didReplaceAny = false;
            for (int i = 0; i < count; i++)
            {
                CrossingPlacementRecord placement = placements[i];
                if (!placement.IsValid || placement.SegmentId == 0)
                {
                    rejected++;
                    if (string.IsNullOrEmpty(firstRejection))
                        firstRejection = placement.Message;
                    continue;
                }

                if (CrossingPlacementRegistry.HasSameModeAssetAt(placement))
                {
                    skippedExisting++;
                    Debug.Log("[PedestrianCrossingToolkit] Junction placement skipped existing: segment="
                              + placement.SegmentId
                              + " position="
                              + placement.SegmentPosition.ToString("0.000")
                              + " mode="
                              + placement.Mode);
                    continue;
                }

                CrossingPlacementPlan plan = CrossingPlacementPlanner.Build(placement);
                if (!plan.IsValid)
                {
                    rejected++;
                    CrossingPlacementPolicyResult policy = CrossingPlacementPolicy.Evaluate(placement);
                    if (string.IsNullOrEmpty(firstRejection))
                        firstRejection = policy.Success ? "Placement is no longer valid." : policy.Message;
                    Debug.Log("[PedestrianCrossingToolkit] Junction placement invalid: segment="
                              + placement.SegmentId
                              + " position="
                              + placement.SegmentPosition.ToString("0.000")
                              + " mode="
                              + placement.Mode
                              + " reason="
                              + firstRejection);
                    continue;
                }

                CrossingPlacementPlan adjustedPlan;
                string adjustmentMessage;
                if (!CrossingPlacementConflictValidator.TryValidateAndAdjust(placement, plan, out adjustedPlan, out adjustmentMessage))
                {
                    rejected++;
                    if (string.IsNullOrEmpty(firstRejection))
                        firstRejection = adjustmentMessage;
                    Debug.Log("[PedestrianCrossingToolkit] Junction placement rejected: segment="
                              + placement.SegmentId
                              + " position="
                              + placement.SegmentPosition.ToString("0.000")
                              + " mode="
                              + placement.Mode
                              + " reason="
                              + adjustmentMessage);
                    continue;
                }

                CrossingPlacementAsset replaced;
                bool didReplace;
                SignalRoadStateSnapshot signalRoadState = CrossingPathBuilder.CaptureSignalRoadState(placement, adjustedPlan);
                CrossingPlacementAsset asset = CrossingPlacementRegistry.AddOrReplace(placement, adjustedPlan, signalRoadState, out replaced, out didReplace);
                RememberNetworkDependencySnapshot(asset, replaced);
                didReplaceAny |= didReplace;
                added++;
                lastAdded = asset;
                lastPlan = adjustedPlan;
                lastPlacement = placement;
                Debug.Log("[PedestrianCrossingToolkit] Junction placement added: mode=" + placement.Mode
                          + " asset=" + asset.Id
                          + " replaced=" + didReplace
                          + " replacedAsset=" + replaced.Id
                          + " segment=" + placement.SegmentId
                          + " position=" + placement.SegmentPosition.ToString("0.000")
                          + " slot=" + FormatPlacementSlot(placement)
                          + " world=" + placement.WorldPosition
                          + " targetNode=" + adjustedPlan.TargetNodeId
                          + " suppressSurface=" + adjustedPlan.SuppressSurfaceCrossing
                          + " application=" + adjustedPlan.ApplicationKind);
            }

            if (added > 0)
            {
                LastPlacement = lastPlacement;
                LastPlacementPlan = lastPlan;
                LastAsset = lastAdded;
                StatusMessage = "Added " + added + " junction " + GetModeLabel(lastPlacement.Mode) + (added == 1 ? string.Empty : "s") + ".";
                if (skippedExisting > 0)
                    StatusMessage += " Skipped " + skippedExisting + " existing.";
                if (rejected > 0)
                    StatusMessage += " Rejected " + rejected + ".";

                SyncBuiltStructures("junction-placement", didReplaceAny);
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return true;
            }

            LastPlacement = CrossingPlacementRecord.None;
            LastPlacementPlan = CrossingPlacementPlan.Invalid;
            LastAsset = CrossingPlacementAsset.None;
            if (skippedExisting > 0)
            {
                StatusMessage = "Existing " + GetModeLabel(placements[0].Mode) + " already selected at this junction.";
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return true;
            }

            blockedMessage = string.IsNullOrEmpty(firstRejection) ? "No junction crossings could be placed." : firstRejection;
            StatusMessage = blockedMessage;
            CrossingApplicationEngine.Refresh("junction-placement-rejected");
            SyncPathExecutionBoundary("junction-placement-rejected");
            PedestrianCrossingToolkitPanel.RefreshInstance();
            return false;
        }

        public static bool BeginAutoScanObservation()
        {
            if (HasAutoScanPreviewPlan)
            {
                StatusMessage = "Apply or cancel the current Auto Scan preview before starting another scan.";
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return false;
            }

            if (_autoScanObservation != null)
            {
                StatusMessage = _autoScanObservation.ToStatusString();
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return false;
            }

            LastPreview = CrossingPlacementRecord.None;
            LastPlacement = CrossingPlacementRecord.None;
            LastPlacementPlan = CrossingPlacementPlan.Invalid;
            LastAsset = CrossingPlacementAsset.None;
            _autoScanObservation = CrossingAutoScanPlanner.BeginObservation(AutoScanObservationSeconds);
            _autoScanObservationStatusSecond = Mathf.CeilToInt(_autoScanObservation.RemainingSeconds);
            _autoScanManagedLastRealtime = Time.realtimeSinceStartup;
            _autoScanObservationManaged =
                PedestrianCrossingScanCoordinator.TryQueueAutoScan(
                    ProcessManagedAutoScanStep,
                    CompleteManagedAutoScan,
                    FailManagedAutoScan);
            StatusMessage = _autoScanObservation.ToStatusString();
            PedestrianCrossingToolkitPanel.RefreshInstance();
            PedestrianCrossingToolkitPanel.BeginAutoScanMonitoringInstance();
            Debug.Log("[PedestrianCrossingToolkit] Auto scan observation requested: duration="
                      + AutoScanObservationSeconds.ToString("0.0")
                      + " candidates="
                      + _autoScanObservation.CandidateCount);
            return true;
        }

        public static void ProcessAutoScanObservation(float realTimeDelta)
        {
            if (_autoScanObservation == null || _autoScanObservationManaged)
                return;

            bool complete = _autoScanObservation.Tick(realTimeDelta);
            if (!complete)
            {
                int statusSecond = Mathf.CeilToInt(_autoScanObservation.RemainingSeconds);
                if (statusSecond != _autoScanObservationStatusSecond)
                {
                    _autoScanObservationStatusSecond = statusSecond;
                    StatusMessage = _autoScanObservation.ToStatusString();
                    PedestrianCrossingToolkitPanel.RefreshInstance();
                }

                return;
            }

            CompleteAutoScanObservation();
        }

        private static bool ProcessManagedAutoScanStep()
        {
            if (_autoScanObservation == null)
                return true;

            float now = Time.realtimeSinceStartup;
            float realTimeDelta = Mathf.Max(0f, now - _autoScanManagedLastRealtime);
            _autoScanManagedLastRealtime = now;
            return _autoScanObservation.Tick(realTimeDelta);
        }

        private static void CompleteManagedAutoScan()
        {
            _autoScanObservationManaged = false;
            _autoScanManagedLastRealtime = 0f;
            CompleteAutoScanObservation();
        }

        private static void FailManagedAutoScan(System.Exception exception)
        {
            _autoScanObservationManaged = false;
            _autoScanManagedLastRealtime = 0f;
            Debug.LogWarning(
                "[PedestrianCrossingToolkit] Scratchy's Scan Manager Auto"
                + " Scan request failed; resuming the same observation"
                + " through PCT's local scheduler. exception="
                + exception);
        }

        private static void CompleteAutoScanObservation()
        {
            if (_autoScanObservation == null)
                return;

            CrossingAutoScanPlanner.ObservationSession observation = _autoScanObservation;
            _autoScanObservation = null;
            _autoScanObservationStatusSecond = -1;
            StatusMessage = "Auto scan is analysing measured pedestrian use.";
            PedestrianCrossingToolkitPanel.EndAutoScanMonitoringInstance();
            PedestrianCrossingToolkitPanel.RefreshInstance();
            CrossingAutoScanSummary summary = RunAutoScan(observation);
            Debug.Log("[PedestrianCrossingToolkit] Auto scan observation completed: " + summary.ToLogString());
        }

        public static CrossingValidationSummary ValidateCrossings()
        {
            if (_autoScanObservation != null)
            {
                StatusMessage = _autoScanObservation.ToStatusString();
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return CrossingValidationSummary.Empty;
            }

            LastPreview = CrossingPlacementRecord.None;
            LastPlacement = CrossingPlacementRecord.None;
            LastPlacementPlan = CrossingPlacementPlan.Invalid;
            LastAsset = CrossingPlacementAsset.None;

            CrossingValidationSummary summary = BuildValidationSummary("manual-validate");
            _lastValidationSummary = summary;
            _hasLastValidationSummary = true;
            StatusMessage = summary.ToStatusString();
            if (summary.HasIssues && HasValidationProblemAssets)
                StatusMessage += " Red X markers stay until the Toolkit is closed, so you can fix multiple marked crossings from one scan. Run Validate Crossings again when finished.";
            else if (summary.HasIssues)
                StatusMessage += " Remove/rebuild affected crossings, then run Validate Crossings again.";

            PedestrianCrossingToolkitPanel.RefreshInstance();
            Debug.Log("[PedestrianCrossingToolkit] Crossing validation complete: "
                      + summary.ToLogString()
                      + " markedProblemAssets="
                      + _validationProblemAssetCount);
            return summary;
        }

        public static string BuildUserInfoReport()
        {
            int standard = 0;
            int signal = 0;
            int subway = 0;
            int manualSubway = 0;
            int bridge = 0;
            int copied = CrossingPlacementRegistry.CopyTo(ValidationAssetBuffer);
            for (int i = 0; i < copied; i++)
            {
                CrossingPlacementAsset asset = ValidationAssetBuffer[i];
                ValidationAssetBuffer[i] = CrossingPlacementAsset.None;
                switch (asset.Placement.Mode)
                {
                    case PedestrianToolMode.MidBlockCrossing:
                        standard++;
                        break;
                    case PedestrianToolMode.SignalCrossing:
                        signal++;
                        break;
                    case PedestrianToolMode.SubwayLink:
                        subway++;
                        break;
                    case PedestrianToolMode.SubwayPointToPoint:
                        manualSubway++;
                        break;
                    case PedestrianToolMode.PedestrianBridge:
                        bridge++;
                        break;
                }
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Pedestrian Crossing Toolkit user info");
            builder.AppendLine("Version: 1.3.0");
            builder.AppendLine("Enabled: " + FormatBool(Enabled));
            builder.AppendLine("Active mode: " + ActiveMode);
            builder.AppendLine("Auto Scan: " + FormatAutoScanForUserInfo());
            builder.AppendLine("Crossings: total=" + CrossingPlacementRegistry.Count
                               + " standard=" + standard
                               + " signal=" + signal
                               + " autoSubway=" + subway
                               + " manualSubway=" + manualSubway
                               + " bridge=" + bridge);
            builder.AppendLine("Validation: " + FormatLastValidationForUserInfo());
            builder.AppendLine("Validation markers: " + _validationProblemAssetCount);
            builder.AppendLine("Built structures: hasBuiltPaths=" + FormatBool(CrossingPathBuilder.HasBuiltPaths)
                               + " autoRebuild=" + FormatBool(CrossingPlacementRegistry.AutoRebuildBuiltStructures));
            builder.AppendLine("TM:PE interop allowed: " + FormatBool(TrafficManagerInteropAllowed));
            builder.AppendLine("Prefabs: path=" + FormatPrefabName(PedestrianCrossingPrefabCatalog.PedestrianPathPrefab)
                               + " surfacePath=" + FormatPrefabName(PedestrianCrossingPrefabCatalog.SurfaceCrossingPathPrefab)
                               + " bridge=" + FormatPrefabName(PedestrianCrossingPrefabCatalog.PedestrianBridgePrefab)
                               + " tunnel=" + FormatPrefabName(PedestrianCrossingPrefabCatalog.PedestrianTunnelPrefab));
            builder.AppendLine("Application: " + CrossingApplicationEngine.LastSummary.ToLogString());
            builder.AppendLine("Network validation: " + CrossingApplicationEngine.LastValidationSummary.ToLogString());
            builder.AppendLine("Connectivity: " + CrossingApplicationEngine.LastConnectivitySummary.ToLogString());
            builder.AppendLine("Path work orders: " + CrossingPathWorkOrderPlanner.LastSummary.ToLogString());
            builder.AppendLine("Landing connectors: " + CrossingLandingConnectorPlanner.LastSummary.ToLogString());
            builder.AppendLine("Construction: " + CrossingConstructionPlanner.LastSummary.ToLogString());
            builder.AppendLine("Path execution: " + CrossingPathExecutionBoundary.LastSummary.ToLogString());
            builder.AppendLine("Status: " + (string.IsNullOrEmpty(StatusMessage) ? "none" : StatusMessage));
            builder.AppendLine("Log: " + PedestrianCrossingLog.LogPath);
            return builder.ToString();
        }

        public static void ShowUserInfoStatus(bool copied)
        {
            StatusMessage = copied
                ? "Debug info copied. Include PedestrianCrossingToolkit.log if reporting a bug."
                : "Debug info written to PedestrianCrossingToolkit.log. Clipboard copy was unavailable.";
            PedestrianCrossingToolkitPanel.RefreshInstance();
        }

        public static void ProcessNetworkDependencyChanges(float realTimeDelta)
        {
            if (ProcessDeferredNetworkDependencyCleanup(realTimeDelta))
                return;

            if (_deferredLoadRebuildPending)
                return;

            if (CrossingPlacementRegistry.Count == 0)
            {
                if (NetworkDependencySnapshots.Count > 0)
                    NetworkDependencySnapshots.Clear();

                _networkDependencyScanTimer = 0f;
                return;
            }

            _networkDependencyScanTimer += Mathf.Max(0f, realTimeDelta);
            if (_networkDependencyScanTimer < NetworkDependencyScanSeconds)
                return;

            _networkDependencyScanTimer = 0f;
            ScanNetworkDependencies();
        }

        private static CrossingValidationSummary BuildValidationSummary(string reason)
        {
            ClearValidationProblemAssets();
            int totalAssets = CrossingPlacementRegistry.Count;
            int copied = CrossingPlacementRegistry.CopyTo(ValidationAssetBuffer);
            int validPlans = 0;
            int invalidPlans = 0;
            int stalePlans = 0;
            int missingNetworkReferences = 0;
            int detailLogs = 0;

            for (int i = 0; i < copied; i++)
            {
                CrossingPlacementAsset asset = ValidationAssetBuffer[i];
                ValidationAssetBuffer[i] = CrossingPlacementAsset.None;
                if (asset.Id == 0)
                    continue;

                CrossingPlacementPlan currentPlan = CrossingPlacementPlanner.BuildExisting(asset);
                if (!asset.Plan.IsValid || !currentPlan.IsValid)
                {
                    invalidPlans++;
                    AddValidationProblemAsset(asset.Id);
                    LogValidationAssetDetail(asset, "placement plan is no longer valid", ref detailLogs);
                }
                else
                {
                    validPlans++;
                    if (IsPlacementPlanStale(asset.Plan, currentPlan))
                    {
                        stalePlans++;
                        AddValidationProblemAsset(asset.Id);
                        LogValidationAssetDetail(asset, "stored placement plan differs from the live network", ref detailLogs);
                    }
                }

                NetworkDependencySnapshot snapshot;
                if (!TryBuildNetworkDependencySnapshot(asset, out snapshot))
                {
                    missingNetworkReferences++;
                    AddValidationProblemAsset(asset.Id);
                    LogValidationAssetDetail(asset, "one or more touched network references are missing", ref detailLogs);
                }
            }

            CrossingApplicationEngine.Refresh(reason);
            MarkBlockedOperationProblemAssets();
            CrossingPathExecutionBoundary.Sync(reason);
            CrossingPathBuilder.BuiltConnectorValidationSummary builtConnectors = CrossingPathBuilder.RefreshBuiltConnectorValidationSummary();
            CrossingApplicationSummary application = CrossingApplicationEngine.LastSummary;
            CrossingNetworkValidationSummary networkValidation = CrossingApplicationEngine.LastValidationSummary;
            CrossingConnectivitySummary connectivity = CrossingApplicationEngine.LastConnectivitySummary;
            CrossingPathWorkOrderSummary pathWorkOrders = CrossingPathWorkOrderPlanner.LastSummary;
            CrossingLandingConnectorSummary landingConnectors = CrossingLandingConnectorPlanner.LastSummary;
            CrossingConstructionSummary construction = CrossingConstructionPlanner.LastSummary;
            CrossingPathExecutionSummary pathExecution = CrossingPathExecutionBoundary.LastSummary;
            LogAssetsWithoutConnectivityLinks(ref detailLogs);
            int missingBuiltStructures = construction.TotalWorkItems > 0 && !CrossingPathBuilder.HasBuiltPaths
                ? construction.TotalWorkItems
                : 0;
            int missingBuiltSegments = Mathf.Max(0, builtConnectors.Segments - builtConnectors.CreatedSegments);

            return new CrossingValidationSummary(
                totalAssets,
                validPlans,
                invalidPlans,
                stalePlans,
                missingNetworkReferences,
                networkValidation.Blocked,
                construction.MissingPrefab,
                construction.UnresolvedLandings,
                connectivity.AssetsWithoutLinks,
                missingBuiltStructures,
                missingBuiltSegments,
                builtConnectors.UnattachedSurfaceTerminalNodes,
                application,
                networkValidation,
                connectivity,
                pathWorkOrders,
                landingConnectors,
                construction,
                pathExecution,
                builtConnectors);
        }

        private static bool IsPlacementPlanStale(CrossingPlacementPlan stored, CrossingPlacementPlan current)
        {
            if (stored.Mode != current.Mode
                || stored.SegmentId != current.SegmentId
                || stored.TargetNodeId != current.TargetNodeId
                || stored.ApplicationKind != current.ApplicationKind
                || stored.SuppressSurfaceCrossing != current.SuppressSurfaceCrossing
                || stored.FlipBridgeAccess != current.FlipBridgeAccess)
            {
                return true;
            }

            return HorizontalDistanceSqr(stored.Center, current.Center) > 1f
                   || HorizontalDistanceSqr(stored.LeftEdge, current.LeftEdge) > 4f
                   || HorizontalDistanceSqr(stored.RightEdge, current.RightEdge) > 4f
                   || Mathf.Abs(stored.Width - current.Width) > 1f;
        }

        private static float HorizontalDistanceSqr(Vector3 first, Vector3 second)
        {
            float dx = first.x - second.x;
            float dz = first.z - second.z;
            return dx * dx + dz * dz;
        }

        private static void LogValidationAssetDetail(CrossingPlacementAsset asset, string issue, ref int detailLogs)
        {
            if (detailLogs >= ValidationMaxDetailLogs)
                return;

            detailLogs++;
            Debug.Log("[PedestrianCrossingToolkit] Crossing validation detail: asset="
                      + asset.Id
                      + " mode="
                      + asset.Placement.Mode
                      + " segment="
                      + asset.Placement.SegmentId
                      + " targetNode="
                      + asset.Plan.TargetNodeId
                      + " issue="
                      + issue);
        }

        private static void LogAssetsWithoutConnectivityLinks(ref int detailLogs)
        {
            int count = CrossingConnectivityPlanner.CopyAssetsWithoutLinksTo(ValidationAssetIdBuffer);
            for (int i = 0; i < count; i++)
            {
                int assetId = ValidationAssetIdBuffer[i];
                ValidationAssetIdBuffer[i] = 0;
                CrossingPlacementAsset asset;
                if (CrossingPlacementRegistry.TryGetAssetById(assetId, out asset))
                    LogValidationAssetDetail(asset, "no pedestrian connectivity link was planned", ref detailLogs);
            }
        }

        private static void MarkBlockedOperationProblemAssets()
        {
            int count = CrossingApplicationEngine.CopyBlockedOperationAssetIdsTo(ValidationAssetIdBuffer);
            for (int i = 0; i < count; i++)
            {
                AddValidationProblemAsset(ValidationAssetIdBuffer[i]);
                ValidationAssetIdBuffer[i] = 0;
            }
        }

        private static void ClearValidationProblemAssets()
        {
            if (_validationProblemAssetCount <= 0)
                return;

            for (int i = 0; i < _validationProblemAssetCount; i++)
                ValidationProblemAssetIds[i] = 0;

            _validationProblemAssetCount = 0;
            _validationProblemRevision++;
        }

        public static void ClearValidationProblemMarkersForToolkitClose()
        {
            ClearValidationProblemAssets();
            ClearAutoScanPreviewPlan(false);
        }

        private static void AddValidationProblemAsset(int assetId)
        {
            if (assetId == 0 || _validationProblemAssetCount >= ValidationProblemAssetIds.Length)
                return;

            for (int i = 0; i < _validationProblemAssetCount; i++)
            {
                if (ValidationProblemAssetIds[i] == assetId)
                    return;
            }

            ValidationProblemAssetIds[_validationProblemAssetCount++] = assetId;
            _validationProblemRevision++;
        }

        private static void ScanNetworkDependencies()
        {
            int count = CrossingPlacementRegistry.CopyTo(NetworkDependencyAssetBuffer);
            int removalCount = 0;
            StaleNetworkDependencySnapshotIds.Clear();
            foreach (int assetId in NetworkDependencySnapshots.Keys)
                StaleNetworkDependencySnapshotIds.Add(assetId);

            for (int i = 0; i < count; i++)
            {
                CrossingPlacementAsset asset = NetworkDependencyAssetBuffer[i];
                NetworkDependencyAssetBuffer[i] = CrossingPlacementAsset.None;
                if (asset.Id == 0)
                    continue;

                StaleNetworkDependencySnapshotIds.Remove(asset.Id);
                string removalReason;
                if (ShouldAutoRemoveForNetworkDependencyChange(asset, out removalReason))
                {
                    Debug.Log("[PedestrianCrossingToolkit] Crossing dependency invalidated: asset="
                              + asset.Id
                              + " mode="
                              + asset.Placement.Mode
                              + " segment="
                              + asset.Placement.SegmentId
                              + " reason="
                              + removalReason);
                    AddNetworkDependencyRemoval(asset.Id, ref removalCount);
                }
            }

            for (int i = 0; i < StaleNetworkDependencySnapshotIds.Count; i++)
                NetworkDependencySnapshots.Remove(StaleNetworkDependencySnapshotIds[i]);
            StaleNetworkDependencySnapshotIds.Clear();

            if (removalCount > 0)
                ScheduleNetworkDependencyCleanup(removalCount);
        }

        private static bool ShouldAutoRemoveForNetworkDependencyChange(CrossingPlacementAsset asset, out string reason)
        {
            if (HasNetworkPlacementChanged(asset, out reason))
                return true;

            NetworkDependencySnapshot current;
            if (!TryBuildNetworkDependencySnapshot(asset, out current))
            {
                reason = string.Empty;
                Debug.Log("[PedestrianCrossingToolkit] Network dependency snapshot unavailable during scan; keeping crossing: asset="
                          + asset.Id
                          + " mode="
                          + asset.Placement.Mode
                          + " segment="
                          + asset.Placement.SegmentId);
                return false;
            }

            NetworkDependencySnapshot previous;
            if (NetworkDependencySnapshots.TryGetValue(asset.Id, out previous)
                && current.RequiresRemovalComparedTo(previous, out reason))
            {
                return true;
            }

            NetworkDependencySnapshots[asset.Id] = current;
            reason = string.Empty;
            return false;
        }

        private static bool HasNetworkPlacementChanged(CrossingPlacementAsset asset, out string reason)
        {
            if (!asset.Plan.IsValid)
            {
                reason = "stored-plan-invalid";
                return true;
            }

            if (!IsNetworkDependencySegmentResolvable(asset.Placement.SegmentId))
            {
                reason = "primary-segment-missing";
                return true;
            }

            if (asset.Placement.HasSecondaryPoint
                && !IsNetworkDependencySegmentResolvable(asset.Placement.SecondarySegmentId))
            {
                reason = "secondary-segment-missing";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        private static void RememberNetworkDependencySnapshot(CrossingPlacementAsset asset, CrossingPlacementAsset replaced)
        {
            if (replaced.Id != 0)
                NetworkDependencySnapshots.Remove(replaced.Id);

            if (asset.Id == 0)
                return;

            NetworkDependencySnapshot snapshot;
            if (TryBuildNetworkDependencySnapshot(asset, out snapshot))
            {
                NetworkDependencySnapshots[asset.Id] = snapshot;
                return;
            }

            NetworkDependencySnapshots.Remove(asset.Id);
            Debug.Log("[PedestrianCrossingToolkit] Network dependency snapshot unavailable after placement: asset="
                      + asset.Id
                      + " mode="
                      + asset.Placement.Mode
                      + " segment="
                      + asset.Placement.SegmentId);
        }

        private static void AddNetworkDependencyRemoval(int assetId, ref int removalCount)
        {
            if (assetId == 0 || removalCount >= NetworkDependencyRemovalIds.Length)
                return;

            for (int i = 0; i < removalCount; i++)
            {
                if (NetworkDependencyRemovalIds[i] == assetId)
                    return;
            }

            NetworkDependencyRemovalIds[removalCount++] = assetId;
        }

        private static void AddChangedAssetId(int[] buffer, int assetId, ref int count, ref bool overflow)
        {
            if (assetId == 0 || buffer == null)
                return;

            for (int i = 0; i < count; i++)
            {
                if (buffer[i] == assetId)
                    return;
            }

            if (count >= buffer.Length)
            {
                overflow = true;
                return;
            }

            buffer[count++] = assetId;
        }

        private static void ScheduleNetworkDependencyCleanup(int removalCount)
        {
            int added = 0;
            for (int i = 0; i < removalCount; i++)
            {
                int assetId = NetworkDependencyRemovalIds[i];
                NetworkDependencyRemovalIds[i] = 0;
                if (assetId == 0 || _deferredNetworkDependencyRemovalCount >= DeferredNetworkDependencyRemovalIds.Length)
                    continue;

                bool duplicate = false;
                for (int j = 0; j < _deferredNetworkDependencyRemovalCount; j++)
                {
                    if (DeferredNetworkDependencyRemovalIds[j] == assetId)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                    continue;

                DeferredNetworkDependencyRemovalIds[_deferredNetworkDependencyRemovalCount++] = assetId;
                added++;
            }

            if (added == 0)
                return;

            _deferredNetworkDependencyCleanupPending = true;
            _deferredNetworkDependencyCleanupElapsed = 0f;
            _deferredNetworkDependencyCleanupReadyFrames = 0;
            _deferredNetworkDependencyLastBuildIndex = GetCurrentBuildIndex();
            StatusMessage = "Waiting for road upgrade to settle before removing affected crossing" + (_deferredNetworkDependencyRemovalCount == 1 ? string.Empty : "s") + ".";
            PedestrianCrossingToolkitPanel.RefreshInstance();
            Debug.Log("[PedestrianCrossingToolkit] Scheduled network dependency cleanup: pending="
                      + _deferredNetworkDependencyRemovalCount
                      + " added="
                      + added
                      + " buildIndex="
                      + _deferredNetworkDependencyLastBuildIndex
                      + " minDelay="
                      + NetworkDependencyCleanupMinimumDelaySeconds.ToString("0.0")
                      + " stableFrames="
                      + NetworkDependencyCleanupStableFrameCount
                      + " fallbackDelay="
                      + NetworkDependencyCleanupFallbackDelaySeconds.ToString("0.0"));
        }

        private static bool ProcessDeferredNetworkDependencyCleanup(float realTimeDelta)
        {
            if (!_deferredNetworkDependencyCleanupPending)
                return false;

            _deferredNetworkDependencyCleanupElapsed += Mathf.Max(0f, realTimeDelta);
            uint buildIndex = GetCurrentBuildIndex();
            if (buildIndex != _deferredNetworkDependencyLastBuildIndex)
            {
                _deferredNetworkDependencyLastBuildIndex = buildIndex;
                _deferredNetworkDependencyCleanupReadyFrames = 0;
            }

            if (_deferredNetworkDependencyCleanupElapsed < NetworkDependencyCleanupMinimumDelaySeconds)
                return true;

            if (IsReadyForDeferredNetworkDependencyCleanup())
                _deferredNetworkDependencyCleanupReadyFrames++;
            else
                _deferredNetworkDependencyCleanupReadyFrames = 0;

            if (_deferredNetworkDependencyCleanupReadyFrames < NetworkDependencyCleanupStableFrameCount
                && _deferredNetworkDependencyCleanupElapsed < NetworkDependencyCleanupFallbackDelaySeconds)
            {
                return true;
            }

            int removalCount = _deferredNetworkDependencyRemovalCount;
            _deferredNetworkDependencyCleanupPending = false;
            _deferredNetworkDependencyCleanupElapsed = 0f;
            _deferredNetworkDependencyCleanupReadyFrames = 0;
            _deferredNetworkDependencyRemovalCount = 0;
            RemoveCrossingsForNetworkDependencyChange(removalCount);
            return true;
        }

        private static void RemoveCrossingsForNetworkDependencyChange(int removalCount)
        {
            const string reason = "network-dependency-change";
            float startedAt = Time.realtimeSinceStartup;
            int removed = 0;
            int builtRemoved = 0;
            int signalControllersRemoved = 0;
            int signalRoadStatesRestored = 0;
            int surfaceControlsRefreshed = 0;
            bool gradeSeparatedRemoved = false;
            for (int i = 0; i < removalCount; i++)
            {
                int assetId = DeferredNetworkDependencyRemovalIds[i];
                DeferredNetworkDependencyRemovalIds[i] = 0;
                CrossingPlacementAsset asset;
                if (!CrossingPlacementRegistry.TryGetAssetById(assetId, out asset))
                    continue;

                string removalReason;
                if (!ShouldAutoRemoveForNetworkDependencyChange(asset, out removalReason))
                {
                    Debug.Log("[PedestrianCrossingToolkit] Skipped deferred network dependency removal after recheck: asset="
                              + asset.Id
                              + " mode="
                              + asset.Placement.Mode
                              + " segment="
                              + asset.Placement.SegmentId);
                    continue;
                }

                bool removedSignalCrossing = asset.Placement.Mode == PedestrianToolMode.SignalCrossing;
                if (removedSignalCrossing)
                    CrossingPathBuilder.PrepareSignalControllerStateRestoreExcept(asset.Id, reason);

                if (!CrossingPlacementRegistry.RemoveById(assetId, out asset))
                {
                    if (removedSignalCrossing)
                        CrossingPathBuilder.ClearPreparedSignalControllerStateRestores();
                    continue;
                }

                NetworkDependencySnapshots.Remove(assetId);
                removed++;
                builtRemoved += CrossingPathBuilder.RemoveBuiltPathsForAsset(asset.Id, reason);
                surfaceControlsRefreshed += CrossingPathBuilder.RefreshSurfaceCrossingControlForAssetRemoval(asset, reason);
                gradeSeparatedRemoved |= GradeSeparatedPlacementGeometryResolver.IsGradeSeparated(asset.Plan.ApplicationKind);
                if (removedSignalCrossing)
                {
                    signalControllersRemoved += CrossingPathBuilder.RemoveSignalControllerForAsset(asset.Id, reason);
                    signalRoadStatesRestored += CrossingPathBuilder.ClearSignalRoadStateForAsset(asset, reason + "-targeted");
                    CrossingPathBuilder.ClearPreparedSignalControllerStateRestores();
                }

                Debug.Log("[PedestrianCrossingToolkit] Crossing auto-removed after road dependency traits changed: asset="
                          + asset.Id
                          + " mode="
                          + asset.Placement.Mode
                          + " segment="
                          + asset.Placement.SegmentId
                          + " reason="
                          + removalReason);
            }

            if (removed == 0)
                return;

            CrossingApplicationEngine.Refresh(reason);
            SyncPathExecutionBoundary(reason);
            if (gradeSeparatedRemoved)
                CrossingPathBuilder.MaintainSuppressedSurfaceCrossings();

            RefreshLastAssetAfterRemoval();
            CrossingPlacementRegistry.SetAutoRebuildBuiltStructures(CrossingPlacementRegistry.Count > 0);
            StatusMessage = "Road upgrade removed " + removed + " PCT crossing" + (removed == 1 ? string.Empty : "s") + "; replace after upgrades finish.";
            PedestrianCrossingToolkitPanel.RefreshInstance();
            Debug.Log("[PedestrianCrossingToolkit] Network dependency cleanup complete: removed="
                      + removed
                      + " mode=targeted"
                      + " builtRemoved="
                      + builtRemoved
                      + " surfaceControlsRefreshed="
                      + surfaceControlsRefreshed
                      + " signalControllersRemoved="
                      + signalControllersRemoved
                      + " signalRoadStatesRestored="
                      + signalRoadStatesRestored
                      + " suppressionReconciled="
                      + gradeSeparatedRemoved
                      + " remaining="
                      + CrossingPlacementRegistry.Count
                      + " elapsedMs="
                      + ((Time.realtimeSinceStartup - startedAt) * 1000f).ToString("0.0"));
        }

        private static bool TryBuildNetworkDependencySnapshot(CrossingPlacementAsset asset, out NetworkDependencySnapshot snapshot)
        {
            snapshot = new NetworkDependencySnapshot { AssetId = asset.Id };
            NetManager netManager = NetManager.instance;
            if (netManager == null || netManager.m_segments == null || netManager.m_nodes == null)
                return false;

            if (!TryAddTouchedSegment(snapshot, netManager, asset.Placement.SegmentId))
                return false;

            if (asset.Placement.HasSecondaryPoint
                && !TryAddTouchedSegment(snapshot, netManager, asset.Placement.SecondarySegmentId))
            {
                return false;
            }

            SortNetworkDependencySegments(snapshot);
            return snapshot.SegmentCount > 0;
        }

        private static bool TryAddTouchedSegment(NetworkDependencySnapshot snapshot, NetManager netManager, ushort segmentId)
        {
            if (segmentId == 0)
                return false;

            for (int i = 0; i < snapshot.SegmentCount; i++)
            {
                if (snapshot.Segments[i].SegmentId == segmentId)
                    return true;
            }

            if (snapshot.SegmentCount >= snapshot.Segments.Length)
                return true;

            SegmentDependencySignature signature;
            if (!TryCaptureSegmentDependencySignature(netManager, segmentId, out signature))
                return false;

            snapshot.Segments[snapshot.SegmentCount++] = signature;
            return true;
        }

        private static bool TryCaptureSegmentDependencySignature(NetManager netManager, ushort segmentId, out SegmentDependencySignature signature)
        {
            signature = default(SegmentDependencySignature);
            if (segmentId == 0 || segmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0
                || segment.Info == null
                || segment.m_startNode == 0
                || segment.m_endNode == 0
                || segment.m_startNode >= netManager.m_nodes.m_size
                || segment.m_endNode >= netManager.m_nodes.m_size)
            {
                return false;
            }

            ref NetNode startNode = ref netManager.m_nodes.m_buffer[segment.m_startNode];
            ref NetNode endNode = ref netManager.m_nodes.m_buffer[segment.m_endNode];
            if ((startNode.m_flags & NetNode.Flags.Created) == 0
                || (endNode.m_flags & NetNode.Flags.Created) == 0)
            {
                return false;
            }

            Vector3 startPosition = startNode.m_position;
            Vector3 endPosition = endNode.m_position;
            signature = new SegmentDependencySignature
            {
                IsValid = true,
                SegmentId = segmentId,
                StartNode = segment.m_startNode,
                EndNode = segment.m_endNode,
                PrefabId = segment.Info.GetInstanceID(),
                RoadWidth = CrossingPlacementPlanner.EstimateRoadHalfWidthForInfo(segment.Info) * 2f,
                HasPavement = RoadPavementAnchorResolver.HasUsablePavement(segment.Info),
                StartX = QuantizeNetworkDependencyPosition(startPosition.x),
                StartY = QuantizeNetworkDependencyPosition(startPosition.y),
                StartZ = QuantizeNetworkDependencyPosition(startPosition.z),
                EndX = QuantizeNetworkDependencyPosition(endPosition.x),
                EndY = QuantizeNetworkDependencyPosition(endPosition.y),
                EndZ = QuantizeNetworkDependencyPosition(endPosition.z),
                StartDirectionX = QuantizeNetworkDependencyDirection(segment.m_startDirection.x),
                StartDirectionZ = QuantizeNetworkDependencyDirection(segment.m_startDirection.z),
                EndDirectionX = QuantizeNetworkDependencyDirection(segment.m_endDirection.x),
                EndDirectionZ = QuantizeNetworkDependencyDirection(segment.m_endDirection.z),
                AverageLength = QuantizeNetworkDependencyPosition(segment.m_averageLength)
            };
            return true;
        }

        private static void SortNetworkDependencySegments(NetworkDependencySnapshot snapshot)
        {
            for (int i = 1; i < snapshot.SegmentCount; i++)
            {
                SegmentDependencySignature current = snapshot.Segments[i];
                int j = i - 1;
                while (j >= 0 && snapshot.Segments[j].SegmentId > current.SegmentId)
                {
                    snapshot.Segments[j + 1] = snapshot.Segments[j];
                    j--;
                }

                snapshot.Segments[j + 1] = current;
            }
        }

        private static int QuantizeNetworkDependencyPosition(float value)
        {
            return Mathf.RoundToInt(value * 10f);
        }

        private static int QuantizeNetworkDependencyDirection(float value)
        {
            return Mathf.RoundToInt(value * 1000f);
        }

        private static bool IsNetworkDependencySegmentResolvable(ushort segmentId)
        {
            NetManager netManager = NetManager.instance;
            if (netManager == null || netManager.m_segments == null || segmentId == 0)
                return false;

            SegmentDependencySignature signature;
            return TryCaptureSegmentDependencySignature(netManager, segmentId, out signature);
        }

        public static CrossingAutoScanSummary RunAutoScan()
        {
            return RunAutoScan(null);
        }

        private static CrossingAutoScanSummary RunAutoScan(CrossingAutoScanPlanner.ObservationSession observation)
        {
            CrossingAutoScanPlan autoPlan = CrossingAutoScanPlanner.Build(observation);
            if (_autoScanPreviewConfirmEnabled && autoPlan.HasWork)
            {
                StageAutoScanPreviewPlan(autoPlan);
                Debug.Log("[PedestrianCrossingToolkit] Auto scan staged for preview: " + autoPlan.Summary.ToLogString());
                return autoPlan.Summary;
            }

            return ApplyAutoScanPlan(autoPlan, "auto-scan");
        }

        private static void StageAutoScanPreviewPlan(CrossingAutoScanPlan plan)
        {
            ClearAutoScanPreviewPlan(false);
            _autoScanPreviewPlan = plan;
            _autoScanPreviewProposalCount = Mathf.Min(plan.PlacementCount, AutoScanPreviewAccepted.Length);
            _autoScanPreviewAcceptedCount = _autoScanPreviewProposalCount;
            _autoScanPreviewRejectedCount = 0;
            for (int i = 0; i < _autoScanPreviewProposalCount; i++)
                AutoScanPreviewAccepted[i] = true;

            LastPreview = CrossingPlacementRecord.None;
            LastPlacement = CrossingPlacementRecord.None;
            LastPlacementPlan = CrossingPlacementPlan.Invalid;
            LastAsset = CrossingPlacementAsset.None;
            if (ActiveMode == PedestrianToolMode.AutoScanReject)
                SetActiveMode(PedestrianToolMode.None);

            _autoScanPreviewRevision++;
            StatusMessage = "Auto Scan preview staged "
                            + _autoScanPreviewAcceptedCount
                            + " proposal"
                            + (_autoScanPreviewAcceptedCount == 1 ? string.Empty : "s")
                            + ". Yellow markers show suggestions; reject unwanted proposals, then apply the rest."
                            + plan.Summary.ToRescanRecommendationString();
            PedestrianCrossingToolkitPanel.RefreshInstance();
            PedestrianCrossingToolkitPanel.ShowAutoScanPreviewInstructionsIfNeeded();
        }

        private static void ClearAutoScanPreviewPlan(bool refreshPanel)
        {
            bool hadPreview = HasAutoScanPreviewPlan;
            int max = Mathf.Min(_autoScanPreviewProposalCount, AutoScanPreviewAccepted.Length);
            for (int i = 0; i < max; i++)
                AutoScanPreviewAccepted[i] = false;

            _autoScanPreviewPlan = CrossingAutoScanPlan.Empty;
            _autoScanPreviewProposalCount = 0;
            _autoScanPreviewAcceptedCount = 0;
            _autoScanPreviewRejectedCount = 0;
            if (ActiveMode == PedestrianToolMode.AutoScanReject)
                SetActiveMode(PedestrianToolMode.None);

            if (hadPreview)
                _autoScanPreviewRevision++;

            if (refreshPanel)
                PedestrianCrossingToolkitPanel.RefreshInstance();
        }

        private static CrossingAutoScanPlan BuildAcceptedAutoScanPreviewPlan()
        {
            CrossingPlacementRecord[] placements = new CrossingPlacementRecord[CrossingAutoScanPlanner.MaxPlannedPlacements];
            int[] removalAssetIds = new int[CrossingAutoScanPlanner.MaxPlannedPlacements];
            int[] placementRemovalAssetIds = new int[CrossingAutoScanPlanner.MaxPlannedPlacements];
            int placementCount = 0;
            int removalCount = 0;
            int gradeSeparated = 0;
            int signal = 0;
            int surface = 0;

            int max = Mathf.Min(_autoScanPreviewProposalCount, _autoScanPreviewPlan.PlacementCount);
            for (int i = 0; i < max && placementCount < placements.Length; i++)
            {
                if (!AutoScanPreviewAccepted[i])
                    continue;

                CrossingPlacementRecord placement = _autoScanPreviewPlan.Placements[i];
                if (!placement.IsValid)
                    continue;

                placements[placementCount] = placement;
                int removalId = GetAutoScanPreviewPlacementRemovalAssetId(i);
                if (AddAutoScanPreviewRemoval(removalAssetIds, ref removalCount, removalId))
                    placementRemovalAssetIds[placementCount] = removalId;

                switch (placement.Mode)
                {
                    case PedestrianToolMode.SignalCrossing:
                        signal++;
                        break;
                    case PedestrianToolMode.MidBlockCrossing:
                        surface++;
                        break;
                    case PedestrianToolMode.SubwayLink:
                    case PedestrianToolMode.PedestrianBridge:
                        gradeSeparated++;
                        break;
                }

                placementCount++;
            }

            CrossingAutoScanSummary source = _autoScanPreviewPlan.Summary;
            CrossingAutoScanSummary summary = new CrossingAutoScanSummary(
                source.ScannedNodes,
                source.ScannedExistingCrossings,
                source.ScannedLongRoadSegments,
                source.Hotspots,
                placementCount,
                removalCount,
                gradeSeparated,
                signal,
                surface,
                source.SkippedExisting,
                source.Rejected,
                source.Capped,
                source.FirstRejection);

            return new CrossingAutoScanPlan(placements, placementCount, removalAssetIds, removalCount, placementRemovalAssetIds, summary);
        }

        private static int GetAutoScanPreviewPlacementRemovalAssetId(int proposalIndex)
        {
            if (proposalIndex < 0
                || _autoScanPreviewPlan.PlacementRemovalAssetIds == null
                || proposalIndex >= _autoScanPreviewPlan.PlacementRemovalAssetIds.Length)
            {
                return 0;
            }

            return _autoScanPreviewPlan.PlacementRemovalAssetIds[proposalIndex];
        }

        private static bool AddAutoScanPreviewRemoval(int[] removalAssetIds, ref int removalCount, int assetId)
        {
            if (assetId == 0 || removalAssetIds == null || removalCount >= removalAssetIds.Length)
                return false;

            for (int i = 0; i < removalCount; i++)
            {
                if (removalAssetIds[i] == assetId)
                    return false;
            }

            removalAssetIds[removalCount++] = assetId;
            return true;
        }

        private static CrossingAutoScanSummary ApplyAutoScanPlan(CrossingAutoScanPlan autoPlan, string reason)
        {
            reason = string.IsNullOrEmpty(reason) ? "auto-scan" : reason;
            if (!autoPlan.HasWork)
            {
                LastPlacement = CrossingPlacementRecord.None;
                LastPlacementPlan = CrossingPlacementPlan.Invalid;
                LastAsset = CrossingPlacementAsset.None;
                StatusMessage = autoPlan.Summary.ToStatusString();
                PedestrianCrossingToolkitPanel.RefreshInstance();
                Debug.Log("[PedestrianCrossingToolkit] Auto scan applied: no changes " + autoPlan.Summary.ToLogString());
                return autoPlan.Summary;
            }

            CrossingApplicationEngine.RevertAppliedOperations(reason + "-change");
            int removed = 0;
            int added = 0;
            int skipped = 0;
            bool didReplaceAny = false;
            CrossingPlacementAsset lastAdded = CrossingPlacementAsset.None;
            CrossingPlacementPlan lastPlan = CrossingPlacementPlan.Invalid;
            CrossingPlacementRecord lastPlacement = CrossingPlacementRecord.None;
            int changedAssetId = 0;
            int changedAssetCount = 0;
            bool changedAssetOverflow = false;

            for (int i = 0; i < autoPlan.PlacementCount; i++)
            {
                CrossingPlacementRecord placement = autoPlan.Placements[i];
                CrossingPlacementPlan plan = CrossingPlacementPlanner.Build(placement);
                if (!placement.IsValid || !plan.IsValid)
                {
                    skipped++;
                    continue;
                }

                if (CrossingPlacementRegistry.HasSameModeAssetAt(placement))
                {
                    skipped++;
                    continue;
                }

                CrossingPlacementPlan adjustedPlan;
                string adjustmentMessage;
                if (!CrossingPlacementConflictValidator.TryValidateAndAdjust(placement, plan, out adjustedPlan, out adjustmentMessage))
                {
                    skipped++;
                    continue;
                }

                int removalAssetId = GetAutoScanPlanPlacementRemovalAssetId(autoPlan, i);
                if (!CrossingAutoScanPlanner.IsPlacementSpacedFromExisting(
                    adjustedPlan,
                    placement,
                    removalAssetId))
                {
                    skipped++;
                    continue;
                }

                CrossingPlacementAsset removedUpgradeAsset = CrossingPlacementAsset.None;
                if (removalAssetId != 0)
                {
                    if (!CrossingPlacementRegistry.TryGetAssetById(removalAssetId, out removedUpgradeAsset)
                        || !CrossingPlacementRegistry.RemoveById(removalAssetId, out removedUpgradeAsset))
                    {
                        skipped++;
                        continue;
                    }

                    NetworkDependencySnapshots.Remove(removedUpgradeAsset.Id);
                    CleanupRemovedAssetForIncrementalSync(removedUpgradeAsset, reason + "-upgrade-remove");
                    removed++;
                }

                CrossingPlacementAsset replaced;
                bool didReplace;
                SignalRoadStateSnapshot signalRoadState = CrossingPathBuilder.CaptureSignalRoadState(placement, adjustedPlan);
                CrossingPlacementAsset asset = CrossingPlacementRegistry.AddOrReplace(placement, adjustedPlan, signalRoadState, out replaced, out didReplace);
                RememberNetworkDependencySnapshot(asset, replaced);
                didReplaceAny |= didReplace;
                if (didReplace)
                    CleanupRemovedAssetForIncrementalSync(replaced, reason + "-replace");
                added++;
                changedAssetId = added == 1 ? asset.Id : 0;
                AddChangedAssetId(AutoScanChangedAssetIds, asset.Id, ref changedAssetCount, ref changedAssetOverflow);
                lastAdded = asset;
                lastPlan = adjustedPlan;
                lastPlacement = placement;
                Debug.Log("[PedestrianCrossingToolkit] Auto scan placement added: mode="
                          + placement.Mode
                          + " asset="
                          + asset.Id
                          + " replaced="
                          + didReplace
                          + " replacedAsset="
                          + replaced.Id
                          + " segment="
                          + placement.SegmentId
                          + " position="
                          + placement.SegmentPosition.ToString("0.000")
                          + " slot="
                          + FormatPlacementSlot(placement)
                          + " targetNode="
                          + adjustedPlan.TargetNodeId
                          + " suppressSurface="
                          + adjustedPlan.SuppressSurfaceCrossing
                          + " application="
                          + adjustedPlan.ApplicationKind
                          + (string.IsNullOrEmpty(adjustmentMessage) ? string.Empty : " adjustment=\"" + adjustmentMessage + "\""));
            }

            for (int i = 0; i < autoPlan.RemovalCount; i++)
            {
                int removalAssetId = autoPlan.RemovalAssetIds[i];
                if (IsAutoScanRemovalMappedToPlacement(autoPlan, removalAssetId))
                    continue;

                CrossingPlacementAsset removedAsset;
                if (CrossingPlacementRegistry.RemoveById(removalAssetId, out removedAsset))
                {
                    NetworkDependencySnapshots.Remove(removedAsset.Id);
                    CleanupRemovedAssetForIncrementalSync(removedAsset, reason + "-remove");
                    removed++;
                }
                else
                    skipped++;
            }

            if (added > 0)
            {
                LastPlacement = lastPlacement;
                LastPlacementPlan = lastPlan;
                LastAsset = lastAdded;
            }
            else
            {
                RefreshLastAssetAfterRemoval();
            }

            StatusMessage = "Auto scan applied " + added + " crossing" + (added == 1 ? string.Empty : "s")
                            + " and " + removed + " removal" + (removed == 1 ? string.Empty : "s") + ".";
            if (skipped > 0)
                StatusMessage += " Skipped " + skipped + ".";
            if (autoPlan.Summary.Capped > 0)
                StatusMessage += autoPlan.Summary.ToRescanRecommendationString();

            bool fullRebuild = changedAssetOverflow;
            if (fullRebuild)
            {
                Debug.LogWarning("[PedestrianCrossingToolkit] Auto scan incremental sync overflow; falling back to full rebuild: changedAssets="
                                 + changedAssetCount
                                 + " added="
                                 + added
                                 + " removed="
                                 + removed);
                SyncBuiltStructures(reason, true, changedAssetId);
            }
            else
            {
                SyncBuiltStructuresForChangedAssets(reason, AutoScanChangedAssetIds, changedAssetCount);
            }

            PedestrianCrossingToolkitPanel.RefreshInstance();
            Debug.Log("[PedestrianCrossingToolkit] Auto scan applied: added="
                      + added
                      + " removed="
                      + removed
                      + " skipped="
                      + skipped
                      + " didReplaceAny="
                      + didReplaceAny
                      + " changedAssets="
                      + changedAssetCount
                      + " syncMode="
                      + (fullRebuild ? "full-rebuild-overflow" : "incremental-batch")
                      + " "
                      + autoPlan.Summary.ToLogString());
            return autoPlan.Summary;
        }

        private static int GetAutoScanPlanPlacementRemovalAssetId(
            CrossingAutoScanPlan plan,
            int placementIndex)
        {
            if (plan.PlacementRemovalAssetIds == null
                || placementIndex < 0
                || placementIndex >= plan.PlacementRemovalAssetIds.Length)
            {
                return 0;
            }

            return plan.PlacementRemovalAssetIds[placementIndex];
        }

        private static bool IsAutoScanRemovalMappedToPlacement(
            CrossingAutoScanPlan plan,
            int removalAssetId)
        {
            if (removalAssetId == 0 || plan.PlacementRemovalAssetIds == null)
                return false;

            int count = Mathf.Min(plan.PlacementCount, plan.PlacementRemovalAssetIds.Length);
            for (int i = 0; i < count; i++)
            {
                if (plan.PlacementRemovalAssetIds[i] == removalAssetId)
                    return true;
            }

            return false;
        }

        public static void ConfirmRemoval(CrossingPlacementRecord placement)
        {
            CrossingPlacementAsset removed;
            if (!CrossingPlacementRegistry.RemoveAt(placement, out removed))
            {
                StatusMessage = "No crossing at this location.";
                Debug.Log("[PedestrianCrossingToolkit] Remove click ignored: no pending asset at segment="
                          + placement.SegmentId
                          + " position=" + placement.SegmentPosition.ToString("0.000")
                          + " nearNode=" + placement.NearNode);
                PedestrianCrossingToolkitPanel.RefreshInstance();
                return;
            }

            RefreshLastAssetAfterRemoval();
            NetworkDependencySnapshots.Remove(removed.Id);
            StatusMessage = "Removed " + GetModeLabel(removed.Placement.Mode) + ".";
            Debug.Log("[PedestrianCrossingToolkit] Pending asset removed by location: id=" + removed.Id
                      + " segment=" + removed.Placement.SegmentId
                      + " remaining=" + CrossingPlacementRegistry.Count);
            bool removedSignalCrossing = removed.Placement.Mode == PedestrianToolMode.SignalCrossing;
            if (removedSignalCrossing)
                CrossingPathBuilder.PrepareSignalControllerStateRestoreExcept(removed.Id, "remove-location");

            CrossingPathBuilder.RemoveBuiltPathsForAsset(removed.Id, "remove-location");
            CrossingPathBuilder.RefreshSurfaceCrossingControlForAssetRemoval(removed, "remove-location");
            if (removedSignalCrossing)
                CrossingPathBuilder.RemoveSignalControllerForAsset(removed.Id, "remove-location");

            SyncBuiltStructures("remove-location", false, removed.Id);

            if (removedSignalCrossing)
            {
                CrossingPathBuilder.ClearSignalRoadStateForAsset(removed, "remove-location-post-sync");
                CrossingPathBuilder.ClearPreparedSignalControllerStateRestores();
            }

            PedestrianCrossingToolkitPanel.RefreshInstance();
        }

        internal static bool TryDetachAssetForRoadReplacement(
            int assetId,
            out CrossingPlacementAsset removed)
        {
            removed = CrossingPlacementAsset.None;
            CrossingPlacementAsset asset;
            if (!CrossingPlacementRegistry.TryGetAssetById(assetId, out asset))
                return false;

            bool removedSignalCrossing = asset.Placement.Mode == PedestrianToolMode.SignalCrossing;
            if (removedSignalCrossing)
                CrossingPathBuilder.PrepareSignalControllerStateRestoreExcept(asset.Id, "api-road-replacement");

            try
            {
                if (!CrossingPlacementRegistry.RemoveById(assetId, out removed))
                    return false;

                NetworkDependencySnapshots.Remove(assetId);
                CrossingPathBuilder.RemoveBuiltPathsForAsset(asset.Id, "api-road-replacement");
                CrossingPathBuilder.RefreshSurfaceCrossingControlForAssetRemoval(asset, "api-road-replacement");
                if (removedSignalCrossing)
                {
                    CrossingPathBuilder.RemoveSignalControllerForAsset(asset.Id, "api-road-replacement");
                    CrossingPathBuilder.ClearSignalRoadStateForAsset(asset, "api-road-replacement");
                }

                return true;
            }
            finally
            {
                if (removedSignalCrossing)
                    CrossingPathBuilder.ClearPreparedSignalControllerStateRestores();
            }
        }

        internal static bool TryRebindAssetForRoadReplacement(
            CrossingPlacementAsset asset,
            out CrossingPlacementAsset rebound,
            out string error)
        {
            rebound = asset;
            error = string.Empty;
            if (asset.Id == 0)
            {
                error = "the crossing registry asset is missing";
                return false;
            }

            CrossingPlacementRecord placement = asset.Placement;
            if (placement.Mode == PedestrianToolMode.SignalCrossing)
            {
                RoadSnapResult liveJoin;
                if (placement.TargetNodeId == 0
                    || !RoadSnapResolver.TryResolveSignalJoinNode(
                        placement.TargetNodeId,
                        true,
                        out liveJoin))
                {
                    error = "the saved signal join is no longer a live two-road join";
                    return false;
                }

                placement = new CrossingPlacementRecord(
                    placement.Mode,
                    liveJoin.SegmentId,
                    liveJoin.SegmentPosition,
                    liveJoin.WorldPosition,
                    true,
                    placement.Message,
                    liveJoin.IsEndpointSlot,
                    liveJoin.SlotNumber,
                    liveJoin.IsEndSegmentSlot,
                    liveJoin.TargetNodeId);
            }
            else
            {
                ushort liveSegmentId;
                float liveSegmentPosition;
                Vector3 liveWorldPosition;
                if (!RoadSnapResolver.TryResolveNearestLiveRoad(
                    placement.WorldPosition,
                    32f,
                    out liveSegmentId,
                    out liveSegmentPosition,
                    out liveWorldPosition))
                {
                    error = "the live road beneath the saved crossing position could not be resolved";
                    return false;
                }

                ushort secondarySegmentId = placement.SecondarySegmentId;
                float secondarySegmentPosition = placement.SecondarySegmentPosition;
                Vector3 secondaryWorldPosition = placement.SecondaryWorldPosition;
                if (placement.HasSecondaryPoint
                    && !RoadSnapResolver.TryResolveNearestLiveRoad(
                        placement.SecondaryWorldPosition,
                        32f,
                        out secondarySegmentId,
                        out secondarySegmentPosition,
                        out secondaryWorldPosition))
                {
                    error = "the live road beneath the saved secondary crossing position could not be resolved";
                    return false;
                }

                placement = new CrossingPlacementRecord(
                    placement.Mode,
                    liveSegmentId,
                    liveSegmentPosition,
                    liveWorldPosition,
                    true,
                    placement.Message,
                    placement.NearNode,
                    placement.SlotNumber,
                    placement.IsEndSegmentSlot,
                    placement.TargetNodeId,
                    placement.HasSecondaryPoint,
                    secondarySegmentId,
                    secondarySegmentPosition,
                    secondaryWorldPosition,
                    placement.SecondaryNearNode,
                    placement.SecondarySlotNumber,
                    placement.SecondaryIsEndSegmentSlot,
                    placement.SecondaryTargetNodeId);
            }

            CrossingPlacementAsset probe = new CrossingPlacementAsset(
                asset.Id,
                placement,
                asset.Plan,
                asset.SignalRoadState);
            CrossingPlacementPlan plan = CrossingPlacementPlanner.BuildExisting(probe);
            if (!plan.IsValid)
            {
                error = "the crossing is not valid on the live road beneath its saved position";
                return false;
            }

            rebound = new CrossingPlacementAsset(
                asset.Id,
                placement,
                plan,
                asset.SignalRoadState);
            return true;
        }

        internal static void CompleteRoadReplacementDetach(int detachedCount, string reason)
        {
            CrossingApplicationEngine.Refresh(reason);
            SyncPathExecutionBoundary(reason);
            CrossingPathBuilder.MaintainSuppressedSurfaceCrossings();
            RefreshLastAssetAfterRemoval();
            CrossingPlacementRegistry.SetAutoRebuildBuiltStructures(CrossingPlacementRegistry.Count > 0);
            StatusMessage = "Temporarily removed " + detachedCount + " PCT crossing" +
                            (detachedCount == 1 ? string.Empty : "s") +
                            " while another mod replaces the road.";
        }

        internal static bool TryRestoreAssetAfterRoadReplacement(
            CrossingPlacementAsset previous,
            ushort[] originalSegmentIds,
            ushort[] replacementSegmentIds,
            out int restoredAssetId,
            out string error)
        {
            restoredAssetId = 0;
            error = string.Empty;
            CrossingPlacementRecord placement = RemapRoadReplacementPlacement(
                previous.Placement,
                originalSegmentIds,
                replacementSegmentIds);
            if (previous.Placement.Mode == PedestrianToolMode.SignalCrossing)
            {
                RoadSnapResult liveJoin;
                if (previous.Placement.TargetNodeId == 0
                    || !RoadSnapResolver.TryResolveSignalJoinNode(
                        previous.Placement.TargetNodeId,
                        true,
                        out liveJoin))
                {
                    error = "the live two-road signal join could not be reacquired";
                    return false;
                }

                CrossingPlacementRecord liveSignalPlacement = new CrossingPlacementRecord(
                    PedestrianToolMode.SignalCrossing,
                    liveJoin.SegmentId,
                    liveJoin.SegmentPosition,
                    liveJoin.WorldPosition,
                    true,
                    string.Empty,
                    liveJoin.IsEndpointSlot,
                    liveJoin.SlotNumber,
                    liveJoin.IsEndSegmentSlot,
                    liveJoin.TargetNodeId);
                CrossingPlacementPolicyResult liveSignalPolicy =
                    CrossingPlacementPolicy.Evaluate(liveSignalPlacement, true);
                if (!liveSignalPolicy.Success)
                {
                    error = string.IsNullOrEmpty(liveSignalPolicy.Message)
                        ? "the live signal join no longer accepts a signal crossing"
                        : liveSignalPolicy.Message;
                    return false;
                }

                placement = new CrossingPlacementRecord(
                    PedestrianToolMode.SignalCrossing,
                    liveJoin.SegmentId,
                    liveJoin.SegmentPosition,
                    liveJoin.WorldPosition,
                    true,
                    liveSignalPolicy.Message,
                    liveJoin.IsEndpointSlot,
                    liveJoin.SlotNumber,
                    liveJoin.IsEndSegmentSlot,
                    liveJoin.TargetNodeId);
            }

            CrossingPlacementAsset probe = new CrossingPlacementAsset(
                previous.Id,
                placement,
                previous.Plan,
                SignalRoadStateSnapshot.Empty);
            CrossingPlacementPlan plan = CrossingPlacementPlanner.BuildExisting(probe);
            if (!plan.IsValid)
            {
                error = "the remapped crossing is no longer valid";
                return false;
            }

            if (previous.Plan.FlipBridgeAccess != plan.FlipBridgeAccess)
                plan = plan.WithBridgeAccessFlipped();

            CrossingPlacementPlan adjustedPlan;
            string adjustmentMessage;
            if (!CrossingPlacementConflictValidator.TryValidateAndAdjust(
                placement,
                plan,
                out adjustedPlan,
                out adjustmentMessage))
            {
                error = string.IsNullOrEmpty(adjustmentMessage)
                    ? "the remapped crossing conflicts with the replacement road"
                    : adjustmentMessage;
                return false;
            }

            CrossingPlacementAsset replaced;
            bool didReplace;
            SignalRoadStateSnapshot signalRoadState =
                CrossingPathBuilder.CaptureSignalRoadState(placement, adjustedPlan);
            CrossingPlacementAsset restored = CrossingPlacementRegistry.AddOrReplace(
                placement,
                adjustedPlan,
                signalRoadState,
                out replaced,
                out didReplace);
            RememberNetworkDependencySnapshot(restored, replaced);
            if (didReplace)
                CleanupRemovedAssetForIncrementalSync(replaced, "api-road-replacement-restore");

            restoredAssetId = restored.Id;
            LastPlacement = placement;
            LastPlacementPlan = adjustedPlan;
            LastAsset = restored;
            return true;
        }

        internal static void CompleteRoadReplacementRestore(
            int[] restoredAssetIds,
            int restoredCount,
            string reason)
        {
            if (restoredCount > 0)
                SyncBuiltStructuresForChangedAssets(reason, restoredAssetIds, restoredCount);
            else
            {
                CrossingApplicationEngine.Refresh(reason);
                SyncPathExecutionBoundary(reason);
                CrossingPlacementRegistry.SetAutoRebuildBuiltStructures(CrossingPlacementRegistry.Count > 0);
            }

            CrossingPathBuilder.MaintainSuppressedSurfaceCrossings();
            RefreshLastAssetAfterRemoval();
            StatusMessage = restoredCount > 0
                ? "Restored " + restoredCount + " PCT crossing" +
                  (restoredCount == 1 ? string.Empty : "s") + " after the road replacement."
                : "No PCT crossings were restored after the road replacement.";
        }

        internal static int CompleteRoadReplacementVisuals(
            int[] restoredAssetIds,
            int restoredCount,
            string reason)
        {
            int validated = CrossingPathBuilder.RebuildUnityVisualsForAssets(
                restoredAssetIds,
                restoredCount);
            if (validated != restoredCount)
            {
                string failure = "one or more restored crossing visual families are missing";
                int max = restoredAssetIds == null
                    ? 0
                    : Mathf.Min(restoredCount, restoredAssetIds.Length);
                for (int i = 0; i < max; i++)
                {
                    string assetError;
                    if (!CrossingPathBuilder.TryValidateRoadReplacementVisualAsset(
                        restoredAssetIds[i],
                        out assetError))
                    {
                        failure = "asset " + restoredAssetIds[i] + ": " + assetError;
                        break;
                    }
                }

                throw new System.InvalidOperationException(failure);
            }

            PedestrianCrossingToolkitPanel.RefreshInstance();
            Debug.Log("[PedestrianCrossingToolkit] Main-thread road-replacement visuals completed: reason="
                      + reason
                      + " assets="
                      + restoredCount
                      + " validatedVisualAssets="
                      + validated);
            return validated;
        }

        private static CrossingPlacementRecord RemapRoadReplacementPlacement(
            CrossingPlacementRecord placement,
            ushort[] originalSegmentIds,
            ushort[] replacementSegmentIds)
        {
            ushort primarySegmentId = RemapRoadReplacementSegment(
                placement.SegmentId,
                originalSegmentIds,
                replacementSegmentIds);
            ushort secondarySegmentId = placement.HasSecondaryPoint
                ? RemapRoadReplacementSegment(
                    placement.SecondarySegmentId,
                    originalSegmentIds,
                    replacementSegmentIds)
                : placement.SecondarySegmentId;

            return new CrossingPlacementRecord(
                placement.Mode,
                primarySegmentId,
                placement.SegmentPosition,
                placement.WorldPosition,
                placement.IsValid,
                placement.Message,
                placement.NearNode,
                placement.SlotNumber,
                placement.IsEndSegmentSlot,
                placement.TargetNodeId,
                placement.HasSecondaryPoint,
                secondarySegmentId,
                placement.SecondarySegmentPosition,
                placement.SecondaryWorldPosition,
                placement.SecondaryNearNode,
                placement.SecondarySlotNumber,
                placement.SecondaryIsEndSegmentSlot,
                placement.SecondaryTargetNodeId);
        }

        private static ushort RemapRoadReplacementSegment(
            ushort segmentId,
            ushort[] originalSegmentIds,
            ushort[] replacementSegmentIds)
        {
            if (originalSegmentIds == null || replacementSegmentIds == null)
                return segmentId;

            int count = Mathf.Min(originalSegmentIds.Length, replacementSegmentIds.Length);
            for (int i = 0; i < count; i++)
            {
                if (originalSegmentIds[i] == segmentId && replacementSegmentIds[i] != 0)
                    return replacementSegmentIds[i];
            }

            return segmentId;
        }

        public static void ClearPlacements()
        {
            ClearAutoScanPreviewPlan(false);
            ClearDeferredNetworkDependencyCleanup();
            CrossingApplicationEngine.RevertAppliedOperations("clear-change");
            int removed = CrossingPlacementRegistry.Count;
            int builtRemoved = CrossingPathBuilder.ClearBuiltPaths("clear-placements");
            int signalRoadStatesRestored = RestoreSignalRoadStatesForClear("clear-placements-post-built-clear");
            CrossingPlacementRegistry.Reset();
            CrossingPlacementRegistry.SetAutoRebuildBuiltStructures(false);
            NetworkDependencySnapshots.Clear();
            StaleNetworkDependencySnapshotIds.Clear();
            ClearValidationProblemAssets();
            _networkDependencyScanTimer = 0f;
            LastAsset = CrossingPlacementAsset.None;
            LastPlacement = CrossingPlacementRecord.None;
            LastPlacementPlan = CrossingPlacementPlan.Invalid;
            StatusMessage = removed == 0
                ? "No crossings to clear."
                : "Cleared " + removed + " crossing" + (removed == 1 ? string.Empty : "s") + ".";

            Debug.Log("[PedestrianCrossingToolkit] Pending assets cleared: removed="
                      + removed
                      + " builtRemoved="
                      + builtRemoved
                      + " signalRoadStatesRestored="
                      + signalRoadStatesRestored);
            CrossingApplicationEngine.Refresh("clear");
            SyncPathExecutionBoundary("clear");
            PedestrianCrossingToolkitPanel.RefreshInstance();
        }

        public static void Reset()
        {
            CrossingApplicationEngine.RevertAppliedOperations("state-reset");
            int builtRemoved = CrossingPathBuilder.ClearBuiltPaths("state-reset");
            int signalRoadStatesRestored = RestoreSignalRoadStatesForClear("state-reset-post-built-clear");
            if (builtRemoved > 0 || signalRoadStatesRestored > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Reset restored built road state: builtRemoved="
                          + builtRemoved
                          + " signalRoadStatesRestored="
                          + signalRoadStatesRestored);
            }

            Enabled = false;
            ActiveMode = PedestrianToolMode.None;
            LastPreview = CrossingPlacementRecord.None;
            LastPlacement = CrossingPlacementRecord.None;
            LastPlacementPlan = CrossingPlacementPlan.Invalid;
            LastAsset = CrossingPlacementAsset.None;
            StatusMessage = "Pedestrian Crossing Toolkit inactive.";
            TrafficManagerInteropAllowed = false;
            _deferredLoadRebuildPending = false;
            _deferredLoadRebuildElapsed = 0f;
            _deferredLoadRebuildReadyFrames = 0;
            ResetDeferredLoadRebuildBatch();
            ClearDeferredNetworkDependencyCleanup();
            _autoScanObservation = null;
            _autoScanObservationStatusSecond = -1;
            _autoScanObservationManaged = false;
            _autoScanManagedLastRealtime = 0f;
            PedestrianCrossingToolkitPanel.ResetAutoScanMonitoringInstance();
            ClearAutoScanPreviewPlan(false);
            _networkDependencyScanTimer = 0f;
            NetworkDependencySnapshots.Clear();
            StaleNetworkDependencySnapshotIds.Clear();
            ClearValidationProblemAssets();
            PedestrianCrossingToolkitApi.ResetForLevelChange();
            CrossingPlacementRegistry.Reset();
            CrossingPlacementTool.Reset();
            SignalCrossingTool.Reset();
            RoadPlacementRules.ResetVanillaCrossingCache();
            SubwayLinkTool.Reset();
            SubwayPointToPointTool.Reset();
            PedestrianBridgeTool.Reset();
            RemovalCrossingTool.Reset();
            PedestrianCrossingPrefabCatalog.Reset();
            CrossingApplicationEngine.Reset();
            CrossingPathExecutionBoundary.Reset();
        }

        public static void ResetForLevelUnload()
        {
            int builtForgotten = CrossingPathBuilder.ForgetBuiltPathsForLevelUnload("state-reset-level-unload");
            int suppressionForgotten = GradeSeparatedVanillaCrossingSuppression.ForgetStateForLevelUnload("state-reset-level-unload");
            CrossingApplicationEngine.ForgetStateForLevelUnload();
            if (builtForgotten > 0 || suppressionForgotten > 0)
            {
                Debug.Log("[PedestrianCrossingToolkit] Level unload reset skipped generated network release: builtTracked="
                          + builtForgotten
                          + " suppressionTracked="
                          + suppressionForgotten);
            }

            Enabled = false;
            ActiveMode = PedestrianToolMode.None;
            LastPreview = CrossingPlacementRecord.None;
            LastPlacement = CrossingPlacementRecord.None;
            LastPlacementPlan = CrossingPlacementPlan.Invalid;
            LastAsset = CrossingPlacementAsset.None;
            StatusMessage = "Pedestrian Crossing Toolkit inactive.";
            TrafficManagerInteropAllowed = false;
            _deferredLoadRebuildPending = false;
            _deferredLoadRebuildElapsed = 0f;
            _deferredLoadRebuildReadyFrames = 0;
            ResetDeferredLoadRebuildBatch();
            ClearDeferredNetworkDependencyCleanup();
            _autoScanObservation = null;
            _autoScanObservationStatusSecond = -1;
            _autoScanObservationManaged = false;
            _autoScanManagedLastRealtime = 0f;
            PedestrianCrossingToolkitPanel.ResetAutoScanMonitoringInstance();
            ClearAutoScanPreviewPlan(false);
            _networkDependencyScanTimer = 0f;
            NetworkDependencySnapshots.Clear();
            StaleNetworkDependencySnapshotIds.Clear();
            ClearValidationProblemAssets();
            PedestrianCrossingToolkitApi.ResetForLevelChange();
            CrossingPlacementRegistry.Reset();
            CrossingPlacementTool.Reset();
            SignalCrossingTool.Reset();
            RoadPlacementRules.ResetVanillaCrossingCache();
            SubwayLinkTool.Reset();
            SubwayPointToPointTool.Reset();
            PedestrianBridgeTool.Reset();
            RemovalCrossingTool.Reset();
            PedestrianCrossingPrefabCatalog.Reset();
            CrossingPathExecutionBoundary.Reset();
        }

        public static void ScheduleBuiltStructureRebuildOnLoad()
        {
            TrafficManagerInteropAllowed = false;
            _deferredLoadRebuildPending = true;
            _deferredLoadRebuildElapsed = 0f;
            _deferredLoadRebuildReadyFrames = 0;
            ResetDeferredLoadRebuildBatch();
            Debug.Log("[PedestrianCrossingToolkit] Scheduled built structure rebuild after load: pending="
                      + _deferredLoadRebuildPending
                      + " count="
                      + CrossingPlacementRegistry.Count
                      + " minDelay="
                      + LoadRebuildMinimumDelaySeconds.ToString("0.0")
                      + " fallbackDelay="
                      + LoadRebuildFallbackDelaySeconds.ToString("0.0"));
        }

        public static void ProcessDeferredLoadWork(float realTimeDelta)
        {
            if (!_deferredLoadRebuildPending)
            {
                TrafficManagerInteropAllowed = true;
                return;
            }

            _deferredLoadRebuildElapsed += Mathf.Max(0f, realTimeDelta);
            if (_deferredLoadRebuildElapsed < LoadRebuildMinimumDelaySeconds)
                return;

            if (IsReadyForDeferredLoadRebuild())
                _deferredLoadRebuildReadyFrames++;
            else
                _deferredLoadRebuildReadyFrames = 0;

            if (_deferredLoadRebuildReadyFrames < LoadRebuildStableFrameCount
                && _deferredLoadRebuildElapsed < LoadRebuildFallbackDelaySeconds)
            {
                return;
            }

            TrafficManagerInteropAllowed = true;
            if (!_deferredLoadRebuildBatchActive)
            {
                StartDeferredLoadRebuildBatch();
                if (_deferredLoadRebuildBatchActive)
                {
                    _deferredLoadRebuildManaged =
                        PedestrianCrossingScanCoordinator.TryQueueLoadRebuild(
                            ProcessDeferredLoadRebuildBatchStep,
                            CompleteManagedLoadRebuild,
                            FailManagedLoadRebuild);
                }
            }

            if (!_deferredLoadRebuildManaged)
                ProcessDeferredLoadRebuildBatchStep();
        }

        private static void StartDeferredLoadRebuildBatch()
        {
            if (CrossingPlacementRegistry.Count == 0
                || !CrossingPlacementRegistry.AutoRebuildBuiltStructures)
            {
                _deferredLoadRebuildPending = false;
                if (CrossingPlacementRegistry.Count > 0)
                {
                    Debug.Log("[PedestrianCrossingToolkit] Skipped built structure rebuild on load: autoRebuildBuiltStructures=false pending="
                              + CrossingPlacementRegistry.Count);
                }

                return;
            }

            _deferredLoadRebuildAssetCount = CrossingPlacementRegistry.CopyTo(ValidationAssetBuffer);
            _deferredLoadRebuildAssetIndex = 0;
            _deferredLoadRebuildBuilt = 0;
            _deferredLoadRebuildSkipped = 0;
            _deferredLoadRebuildWorkStartedAt = Time.realtimeSinceStartup;
            _deferredLoadRebuildBatchActive = true;
            CrossingPathBuilder.BeginBuildBatch(false);
        }

        private static bool ProcessDeferredLoadRebuildBatchStep()
        {
            if (!_deferredLoadRebuildBatchActive)
                return true;

            try
            {
                if (_deferredLoadRebuildAssetIndex < _deferredLoadRebuildAssetCount)
                {
                    CrossingPlacementAsset asset = ValidationAssetBuffer[_deferredLoadRebuildAssetIndex++];
                    if (asset.Id > 0)
                    {
                        float assetStartedAt = Time.realtimeSinceStartup;
                        int skipped;
                        _deferredLoadRebuildBuilt += CrossingPathBuilder.BuildPathsForAsset(asset.Id, out skipped);
                        _deferredLoadRebuildSkipped += skipped;
                        float assetElapsedMs = (Time.realtimeSinceStartup - assetStartedAt) * 1000f;
                        _deferredLoadRebuildWorkElapsedMs += assetElapsedMs;
                        if (assetElapsedMs >= LoadRebuildSlowAssetWarningMs)
                        {
                            Debug.LogWarning("[PedestrianCrossingToolkit] Slow staged crossing rebuild slice: asset="
                                             + asset.Id
                                             + " elapsedMs="
                                             + assetElapsedMs.ToString("0.0")
                                             + " cursor="
                                             + _deferredLoadRebuildAssetIndex
                                             + "/"
                                             + _deferredLoadRebuildAssetCount);
                        }
                    }

                    return false;
                }

                CrossingPathBuilder.EndBuildBatch();
                float wallElapsedMs = (Time.realtimeSinceStartup - _deferredLoadRebuildWorkStartedAt) * 1000f;
                Debug.Log("[PedestrianCrossingToolkit] Rehydrated persistent crossing structures on load: built="
                          + _deferredLoadRebuildBuilt
                          + " skipped="
                          + _deferredLoadRebuildSkipped
                          + " assets="
                          + _deferredLoadRebuildAssetCount
                          + " schedulingDelaySeconds="
                          + _deferredLoadRebuildElapsed.ToString("0.00")
                          + " workElapsedMs="
                          + _deferredLoadRebuildWorkElapsedMs.ToString("0.0")
                          + " stagedWallElapsedMs="
                          + wallElapsedMs.ToString("0.0")
                          + " readyFrames="
                          + _deferredLoadRebuildReadyFrames);
                _deferredLoadRebuildPending = false;
                ResetDeferredLoadRebuildBatch();
                return true;
            }
            catch (System.Exception e)
            {
                CrossingPathBuilder.EndBuildBatch();
                _deferredLoadRebuildPending = false;
                ResetDeferredLoadRebuildBatch();
                Debug.LogError("[PedestrianCrossingToolkit] Built structure rebuild on load failed: " + e);
                return true;
            }
        }

        private static void CompleteManagedLoadRebuild()
        {
            _deferredLoadRebuildManaged = false;
        }

        private static void FailManagedLoadRebuild(System.Exception exception)
        {
            _deferredLoadRebuildManaged = false;
            Debug.LogWarning(
                "[PedestrianCrossingToolkit] Scratchy's Scan Manager saved"
                + "-crossing rehydration request failed; resuming the same"
                + " batch through PCT's local scheduler. exception="
                + exception);
        }

        private static void ResetDeferredLoadRebuildBatch()
        {
            if (_deferredLoadRebuildBatchActive)
                CrossingPathBuilder.CancelBuildBatch();

            _deferredLoadRebuildAssetCount = 0;
            _deferredLoadRebuildAssetIndex = 0;
            _deferredLoadRebuildBuilt = 0;
            _deferredLoadRebuildSkipped = 0;
            _deferredLoadRebuildWorkStartedAt = 0f;
            _deferredLoadRebuildWorkElapsedMs = 0f;
            _deferredLoadRebuildBatchActive = false;
            _deferredLoadRebuildManaged = false;
        }

        public static void RebuildBuiltStructuresOnLoad()
        {
            try
            {
                if (CrossingPlacementRegistry.Count == 0)
                    return;

                if (!CrossingPlacementRegistry.AutoRebuildBuiltStructures)
                {
                    Debug.Log("[PedestrianCrossingToolkit] Skipped built structure rebuild on load: autoRebuildBuiltStructures=false pending="
                              + CrossingPlacementRegistry.Count);
                    return;
                }

                int skipped;
                int built = CrossingPathBuilder.BuildPaths(out skipped);
                if (built > 0 || skipped > 0)
                {
                    Debug.Log("[PedestrianCrossingToolkit] Rehydrated persistent crossing structures on load: built="
                              + built
                              + " skipped="
                              + skipped
                              + " elapsed="
                              + _deferredLoadRebuildElapsed.ToString("0.00")
                              + " readyFrames="
                              + _deferredLoadRebuildReadyFrames);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PedestrianCrossingToolkit] Built structure rebuild on load failed: " + e);
            }
        }

        private static void SyncBuiltStructures(string reason)
        {
            SyncBuiltStructures(reason, true);
        }

        private static void SyncBuiltStructures(string reason, bool rebuildExisting)
        {
            SyncBuiltStructures(reason, rebuildExisting, 0);
        }

        private static void SyncBuiltStructuresForChangedAssets(string reason, int[] changedAssetIds, int changedAssetCount)
        {
            float startedAt = Time.realtimeSinceStartup;
            CrossingApplicationEngine.Refresh(reason);
            SyncPathExecutionBoundary(reason);
            try
            {
                CrossingPathBuilder.BeginBuildBatch(false);
                int built = 0;
                int skipped = 0;
                int missingAssets = 0;
                for (int i = 0; i < changedAssetCount; i++)
                {
                    int assetId = changedAssetIds[i];
                    changedAssetIds[i] = 0;
                    if (assetId <= 0)
                        continue;

                    CrossingPlacementAsset asset;
                    if (!CrossingPlacementRegistry.TryGetAssetById(assetId, out asset))
                    {
                        missingAssets++;
                        continue;
                    }

                    int assetSkipped;
                    built += CrossingPathBuilder.BuildPathsForAsset(assetId, out assetSkipped);
                    skipped += assetSkipped;
                }

                CrossingPlacementRegistry.SetAutoRebuildBuiltStructures(CrossingPlacementRegistry.Count > 0);
                Debug.Log("[PedestrianCrossingToolkit] Synced built structures: reason="
                          + reason
                          + " mode=incremental-batch"
                          + " pruned=0"
                          + " removed=0"
                          + " built="
                          + built
                          + " skipped="
                          + skipped
                          + " changedAssets="
                          + changedAssetCount
                          + " missingAssets="
                          + missingAssets
                          + " pathExecution="
                          + CrossingPathExecutionBoundary.LastSummary.ToLogString()
                          + " pending="
                          + CrossingPlacementRegistry.Count
                          + " elapsedMs="
                          + ((Time.realtimeSinceStartup - startedAt) * 1000f).ToString("0.0"));
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PedestrianCrossingToolkit] Incremental batch built structure sync failed: reason="
                               + reason
                               + " changedAssets="
                               + changedAssetCount
                               + " error="
                               + e);
            }
            finally
            {
                CrossingPathBuilder.EndBuildBatch();
                for (int i = 0; i < changedAssetCount && i < changedAssetIds.Length; i++)
                    changedAssetIds[i] = 0;
            }
        }

        private static void SyncBuiltStructures(string reason, bool rebuildExisting, int changedAssetId)
        {
            float startedAt = Time.realtimeSinceStartup;
            int pruned = CrossingPlacementRegistry.PruneDuplicateAssets();
            CrossingApplicationEngine.Refresh(reason);
            SyncPathExecutionBoundary(reason);
            try
            {
                bool fullRebuild = rebuildExisting || pruned > 0 || changedAssetId <= 0;
                int removed = fullRebuild ? CrossingPathBuilder.ClearBuiltPaths("sync-" + reason) : 0;
                int skipped;
                int built = fullRebuild
                    ? CrossingPathBuilder.BuildPaths(out skipped)
                    : CrossingPathBuilder.BuildPathsForAsset(changedAssetId, out skipped);
                CrossingPlacementRegistry.SetAutoRebuildBuiltStructures(CrossingPlacementRegistry.Count > 0);
                Debug.Log("[PedestrianCrossingToolkit] Synced built structures: reason="
                          + reason
                          + " mode="
                          + (fullRebuild ? "full-rebuild" : "incremental")
                          + " pruned="
                          + pruned
                          + " removed="
                          + removed
                          + " built="
                          + built
                          + " skipped="
                          + skipped
                          + " pathExecution="
                          + CrossingPathExecutionBoundary.LastSummary.ToLogString()
                          + " pending="
                          + CrossingPlacementRegistry.Count
                          + " elapsedMs="
                          + ((Time.realtimeSinceStartup - startedAt) * 1000f).ToString("0.0"));
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PedestrianCrossingToolkit] Built structure sync failed: reason="
                               + reason
                               + " error="
                               + e);
            }
        }

        public static string GetModeLabel(PedestrianToolMode mode)
        {
            switch (mode)
            {
                case PedestrianToolMode.MidBlockCrossing:
                    return "mid-block crossing";
                case PedestrianToolMode.SignalCrossing:
                    return "signal crossing";
                case PedestrianToolMode.SubwayLink:
                    return "subway link";
                case PedestrianToolMode.SubwayPointToPoint:
                    return "point-to-point subway";
                case PedestrianToolMode.PedestrianBridge:
                    return "pedestrian bridge";
                case PedestrianToolMode.AutoScanReject:
                    return "Auto Scan proposal";
                case PedestrianToolMode.RemoveCrossing:
                    return "remove crossing";
                default:
                    return "none";
            }
        }

        public static string FormatPlacementLocation(CrossingPlacementRecord record)
        {
            return RoadSnapResolver.FormatSlot(record.SlotNumber);
        }

        private static string FormatPlacementSlot(CrossingPlacementRecord record)
        {
            return FormatPlacementLocation(record);
        }

        private static bool IsSamePreviewPanelState(CrossingPlacementRecord existing, CrossingPlacementRecord preview)
        {
            return existing.Mode == preview.Mode
                   && existing.SegmentId == preview.SegmentId
                   && existing.IsValid == preview.IsValid
                   && existing.NearNode == preview.NearNode
                   && existing.SlotNumber == preview.SlotNumber
                   && existing.IsEndSegmentSlot == preview.IsEndSegmentSlot
                   && existing.TargetNodeId == preview.TargetNodeId
                   && existing.Message == preview.Message
                   && existing.HasSecondaryPoint == preview.HasSecondaryPoint
                   && existing.SecondarySegmentId == preview.SecondarySegmentId
                   && existing.SecondaryNearNode == preview.SecondaryNearNode
                   && existing.SecondarySlotNumber == preview.SecondarySlotNumber
                   && existing.SecondaryIsEndSegmentSlot == preview.SecondaryIsEndSegmentSlot
                   && existing.SecondaryTargetNodeId == preview.SecondaryTargetNodeId;
        }

        private static string FormatSurfaceSuppression(CrossingPlacementPlan plan)
        {
            return plan.SuppressSurfaceCrossing ? " with the existing road crossing reused" : string.Empty;
        }

        private static string FormatLastValidationForUserInfo()
        {
            if (!_hasLastValidationSummary)
                return "not run";

            return _lastValidationSummary.HasIssues
                ? "actionableIssues=" + _lastValidationSummary.IssueCount + " " + _lastValidationSummary.ToShortIssueStringForUserInfo()
                : "noPlayerActionNeeded diagnosticNotes=" + _lastValidationSummary.DiagnosticNoteCount;
        }

        private static string FormatAutoScanForUserInfo()
        {
            if (_autoScanObservation != null)
                return _autoScanObservation.ToStatusString();

            if (HasAutoScanPreviewPlan)
            {
                return "preview pending accepted="
                       + _autoScanPreviewAcceptedCount
                       + " rejected="
                       + _autoScanPreviewRejectedCount
                       + " total="
                       + _autoScanPreviewProposalCount
                       + " instructionsSuppressed="
                       + FormatBool(AutoScanPreviewInstructionsSuppressed);
            }

            return (_autoScanPreviewConfirmEnabled ? "idle previewConfirm=on" : "idle")
                   + " instructionsSuppressed="
                   + FormatBool(AutoScanPreviewInstructionsSuppressed);
        }

        private static void EnsureAutoScanPreviewInstructionsPreferenceLoaded()
        {
            if (_autoScanPreviewInstructionsSuppressedLoaded)
                return;

            _autoScanPreviewInstructionsSuppressed = PlayerPrefs.GetInt(AutoScanPreviewInstructionsSuppressedKey, 0) != 0;
            _autoScanPreviewInstructionsSuppressedLoaded = true;
        }

        private static string FormatPrefabName(NetInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.name))
                return "none";

            return info.name;
        }

        private static string FormatBool(bool value)
        {
            return value ? "yes" : "no";
        }

        private static void RefreshLastAssetAfterRemoval()
        {
            CrossingPlacementAsset last;
            if (CrossingPlacementRegistry.TryGetLast(out last))
            {
                LastAsset = last;
                LastPlacement = last.Placement;
                LastPlacementPlan = last.Plan;
            }
            else
            {
                LastAsset = CrossingPlacementAsset.None;
                LastPlacement = CrossingPlacementRecord.None;
                LastPlacementPlan = CrossingPlacementPlan.Invalid;
            }
        }

        private static void SyncPathExecutionBoundary(string reason)
        {
            if (CrossingPathExecutionBoundary.LivePathCreationEnabled || PedestrianCrossingLog.VerboseDiagnostics)
                CrossingPathExecutionBoundary.Sync(reason);
            else
                CrossingPathExecutionBoundary.Reset();
        }

        private static int RestoreSignalRoadStatesForClear(string reason)
        {
            int count = CrossingPlacementRegistry.CopyTo(ClearPlacementAssets);
            int restored = 0;
            for (int i = 0; i < count; i++)
            {
                CrossingPlacementAsset asset = ClearPlacementAssets[i];
                ClearPlacementAssets[i] = CrossingPlacementAsset.None;
                if (asset.Placement.Mode != PedestrianToolMode.SignalCrossing)
                    continue;

                restored += CrossingPathBuilder.ClearSignalRoadStateForAsset(asset, reason);
            }

            return restored;
        }

        private static bool IsReadyForDeferredLoadRebuild()
        {
            LoadingManager loadingManager = LoadingManager.instance;
            if (loadingManager == null
                || !loadingManager.m_simulationDataLoaded
                || !loadingManager.m_loadingComplete)
            {
                return false;
            }

            return NetManager.instance != null
                   && SimulationManager.instance != null
                   && PedestrianCrossingPrefabCatalog.HasPathAssets;
        }

        private static bool IsReadyForDeferredNetworkDependencyCleanup()
        {
            if (!IsReadyForDeferredLoadRebuild())
                return false;

            return GetCurrentBuildIndex() == _deferredNetworkDependencyLastBuildIndex;
        }

        private static uint GetCurrentBuildIndex()
        {
            SimulationManager simulationManager = SimulationManager.instance;
            return simulationManager == null ? 0u : simulationManager.m_currentBuildIndex;
        }

        private static void ClearDeferredNetworkDependencyCleanup()
        {
            for (int i = 0; i < _deferredNetworkDependencyRemovalCount; i++)
                DeferredNetworkDependencyRemovalIds[i] = 0;

            _deferredNetworkDependencyCleanupPending = false;
            _deferredNetworkDependencyCleanupElapsed = 0f;
            _deferredNetworkDependencyCleanupReadyFrames = 0;
            _deferredNetworkDependencyLastBuildIndex = 0u;
            _deferredNetworkDependencyRemovalCount = 0;
        }
    }
}
