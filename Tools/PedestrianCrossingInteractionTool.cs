using System;
using System.Collections.Generic;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public class PedestrianCrossingInteractionTool : ToolBase
    {
        private static readonly Color ExclusionOverlayColor = new Color(1f, 0.02f, 0.02f, 0.40f);
        private static readonly Color ExclusionOverlayMutedColor = new Color(1f, 0.02f, 0.02f, 0.04f);
        private static Material _exclusionZoneWorldMaterial;
        private static Material _signalGuideWorldMaterial;
        private const float HoverLineTotalPixels = 76f;
        private const float PlacementBlockHoldSeconds = 3f;
        private const float ToolTipBelowCursorPixels = 86f;
        private const float SignalGuideRadiusPixels = 380f;
        private const float SignalGuideMaxCameraHeight = 1040f;
        private const float SignalJoinMarkerOuterRadiusPixels = 14f;
        private const float SignalJoinMarkerInnerRadiusPixels = 5f;
        private const float SignalJoinMarkerWorldLift = 0.35f;
        private const float SignalStatusPanelWidth = 286f;
        private const float SignalStatusPanelHeight = 126f;
        private const float SignalStatusPanelBelowCrossingPixels = 124f;
        private const float ToolInfoMinWidth = 220f;
        private const float ToolInfoMaxWidthPadding = 48f;
        private const float ToolInfoLineHeight = 18f;
        private const float ToolInfoMinHeight = 72f;
        private const float ToolInfoPadding = 18f;
        private const int SignalLampTextureSize = 32;
        private const int SignalGuideIconTextureSize = 64;
        private const float SignalGuideWorldVisualRefreshSeconds = 0.16f;
        private const float SignalGuideWorldVisualRefreshPixels = 10f;
        private const float SignalGuideIconWorldScale = 0.026f;
        private const float SignalGuideIconMinWorldSize = 8f;
        private const float SignalGuideIconMaxWorldSize = 24f;
        private const float JunctionExclusionOverlayMaxCameraHeight = 1040f;
        private const float JunctionExclusionOverlayHeight = 0.12f;
        private const float JunctionExclusionWorldUnionStripDepth = 0.25f;
        private const float ExclusionOverlayClipChunkPixels = 2f;
        private const float ExclusionOverlayFillLineWidth = 2.4f;
        private const float ExclusionZoneRefreshSeconds = 0.25f;
        private const float ExclusionZoneRevealRefreshPixels = 18f;
        private const float ExclusionZoneCameraRefreshDistance = 5f;
        private const float ExclusionZoneCameraRefreshAngle = 2f;
        private const int ExclusionZoneCacheBuildNodeBatchSize = 512;
        private const int SignalJoinCacheBuildNodeBatchSize = 512;
        private static readonly Color SignalJoinMarkerColor = new Color(1f, 0.86f, 0.04f, 0.95f);
        private static readonly CrossingPlacementAsset[] ExclusionZoneSignalAssetBuffer = new CrossingPlacementAsset[2048];
        private static Texture2D _signalLampCircleTexture;
        private static Texture2D _signalGuideIconTexture;
        private static Rect _toolInfoScreenRect = default(Rect);
        private static bool _hasToolInfoScreenRect;
        private CrossingPlacementRecord _preview = CrossingPlacementRecord.None;
        private CrossingPlacementPlan _previewPlan = CrossingPlacementPlan.Invalid;
        private CrossingPlacementAsset _previewTouchedAsset = CrossingPlacementAsset.None;
        private CrossingPlacementAsset _hoverSignalAsset = CrossingPlacementAsset.None;
        private CrossingPlacementAsset _hoverCrossingInfoAsset = CrossingPlacementAsset.None;
        private CrossingPlacementRecord _hoverAutoScanPreviewPlacement = CrossingPlacementRecord.None;
        private CrossingPlacementPlan _hoverAutoScanPreviewPlan = CrossingPlacementPlan.Invalid;
        private CrossingPathBuilder.SignalControllerDebugSnapshot _hoverSignalStatus;
        private Vector3 _hoverSignalStatusWorldPosition = Vector3.zero;
        private bool _previewTouchesExisting;
        private bool _hasHoverSignalStatus;
        private bool _hasHoverCrossingInfo;
        private int _hoverAutoScanPreviewProposalIndex = -1;
        private Color _previewHoverColor;
        private float _placementBlockUntil;
        private string _placementBlockMessage = string.Empty;
        private Vector3 _placementBlockPosition = Vector3.zero;
        private GameObject _exclusionZoneWorldVisual;
        private Mesh _exclusionZoneWorldMesh;
        private GameObject _signalGuideWorldVisual;
        private Mesh _signalGuideWorldMesh;
        private float _signalGuideWorldRefreshTime = -100f;
        private Vector2 _signalGuideWorldRevealCenter = Vector2.zero;
        private Vector3 _signalGuideWorldCameraPosition = Vector3.zero;
        private Quaternion _signalGuideWorldCameraRotation = Quaternion.identity;
        private float _exclusionZoneRefreshTime = -100f;
        private PedestrianToolMode _exclusionZoneCachedMode = PedestrianToolMode.None;
        private Vector2 _exclusionZoneCachedRevealCenter = Vector2.zero;
        private Vector3 _exclusionZoneCachedCameraPosition = Vector3.zero;
        private Quaternion _exclusionZoneCachedCameraRotation = Quaternion.identity;
        private readonly List<CachedJunctionExclusionZone> _exclusionZoneCache = new List<CachedJunctionExclusionZone>();
        private readonly List<CachedJunctionExclusionZone> _exclusionZoneBuildBuffer = new List<CachedJunctionExclusionZone>();
        private readonly List<Vector3[]> _exclusionZoneVisiblePolygons = new List<Vector3[]>();
        private readonly List<ushort> _pctSignalExclusionNodes = new List<ushort>();
        private readonly List<ushort> _signalGuideJoinCache = new List<ushort>();
        private readonly List<ushort> _signalGuideJoinBuildBuffer = new List<ushort>();
        private ExclusionZoneCacheKind _exclusionZoneCacheKind = ExclusionZoneCacheKind.None;
        private ExclusionZoneCacheKind _exclusionZoneBuildKind = ExclusionZoneCacheKind.None;
        private bool _exclusionZoneCacheReady;
        private bool _exclusionZoneCacheBuildInProgress;
        private ushort _exclusionZoneBuildNextNodeId;
        private int _exclusionZoneCachedNodeCount;
        private int _exclusionZoneCachedSegmentCount;
        private int _exclusionZoneCachedRegistryRevision;
        private bool _signalGuideJoinCacheReady;
        private bool _signalGuideJoinCacheBuildInProgress;
        private ushort _signalGuideJoinBuildNextNodeId;
        private int _signalGuideJoinCachedNodeCount;
        private int _signalGuideJoinCachedSegmentCount;
        private int _signalGuideJoinCachedRegistryRevision;
        private int _pctSignalExclusionNodesRevision = -1;

        private enum ExclusionZoneCacheKind
        {
            None,
            SurfaceAndSignal,
            GradeSeparated
        }

        private struct CachedJunctionExclusionZone
        {
            public readonly ushort NodeId;
            public readonly RoadPlacementRules.JunctionExclusionZone Zone;

            public CachedJunctionExclusionZone(ushort nodeId, RoadPlacementRules.JunctionExclusionZone zone)
            {
                NodeId = nodeId;
                Zone = zone;
            }
        }

        private struct ExclusionInterval
        {
            public float Start;
            public float End;
            public float StartY;
            public float EndY;

            public ExclusionInterval(float start, float end, float startY, float endY)
            {
                Start = start;
                End = end;
                StartY = startY;
                EndY = endY;
            }
        }

        private struct ExclusionIntersection
        {
            public float X;
            public float Y;

            public ExclusionIntersection(float x, float y)
            {
                X = x;
                Y = y;
            }
        }

        public static PedestrianCrossingInteractionTool EnsureOnToolController()
        {
            ToolController controller = ToolsModifierControl.toolController;
            if (controller == null)
            {
                Debug.LogError("[PedestrianCrossingToolkit] Cannot create interaction tool: ToolController is unavailable.");
                return null;
            }

            PedestrianCrossingInteractionTool tool = controller.GetComponent<PedestrianCrossingInteractionTool>();
            if (tool == null)
            {
                tool = controller.gameObject.AddComponent<PedestrianCrossingInteractionTool>();
                Debug.Log("[PedestrianCrossingToolkit] Interaction tool attached to ToolController.");
            }

            return tool;
        }

        protected override void OnToolUpdate()
        {
            base.OnToolUpdate();

            if (!PedestrianCrossingToolkitState.Enabled || PedestrianCrossingToolkitState.ActiveMode == PedestrianToolMode.None)
            {
                ClearExclusionZoneWorldVisual();
                ClearSignalGuideWorldVisual();
                ClearSignalGuideJoinCache();
                _hasToolInfoScreenRect = false;
                _hasHoverSignalStatus = false;
                _hoverSignalAsset = CrossingPlacementAsset.None;
                _hasHoverCrossingInfo = false;
                _hoverCrossingInfoAsset = CrossingPlacementAsset.None;
                _hoverAutoScanPreviewProposalIndex = -1;
                _hoverAutoScanPreviewPlacement = CrossingPlacementRecord.None;
                _hoverAutoScanPreviewPlan = CrossingPlacementPlan.Invalid;
                ShowToolInfo(false, null, Vector3.zero);
                return;
            }

            if (PedestrianCrossingToolkitPanel.IsMouseOverExternalBlockingUi())
            {
                _hasToolInfoScreenRect = false;
                _hasHoverSignalStatus = false;
                _hoverSignalAsset = CrossingPlacementAsset.None;
                _hasHoverCrossingInfo = false;
                _hoverCrossingInfoAsset = CrossingPlacementAsset.None;
                _hoverAutoScanPreviewProposalIndex = -1;
                _hoverAutoScanPreviewPlacement = CrossingPlacementRecord.None;
                _hoverAutoScanPreviewPlan = CrossingPlacementPlan.Invalid;
                ClearSignalGuideWorldVisual();
                ShowToolInfo(false, null, Vector3.zero);

                if (Input.GetMouseButtonDown(0))
                {
                    PedestrianCrossingToolkitPanel.CloseForExternalUiSelection();
                    Debug.Log("[PedestrianCrossingToolkit] Toolkit closed: external UI selected.");
                }

                return;
            }

            if (PedestrianCrossingToolkitPanel.IsMouseOverAnyBlockingUi())
            {
                _hasToolInfoScreenRect = false;
                _hasHoverSignalStatus = false;
                _hoverSignalAsset = CrossingPlacementAsset.None;
                _hasHoverCrossingInfo = false;
                _hoverCrossingInfoAsset = CrossingPlacementAsset.None;
                _hoverAutoScanPreviewProposalIndex = -1;
                _hoverAutoScanPreviewPlacement = CrossingPlacementRecord.None;
                _hoverAutoScanPreviewPlan = CrossingPlacementPlan.Invalid;
                ClearSignalGuideWorldVisual();
                ShowToolInfo(false, null, Vector3.zero);

                return;
            }

            PedestrianToolMode activeMode = PedestrianCrossingToolkitState.ActiveMode;
            if (activeMode == PedestrianToolMode.AutoScanReject)
            {
                UpdateAutoScanRejectHover();
                if (Input.GetMouseButtonDown(0))
                {
                    if (_hoverAutoScanPreviewProposalIndex >= 0)
                    {
                        PedestrianCrossingToolkitState.RejectAutoScanPreviewProposal(_hoverAutoScanPreviewProposalIndex);
                        ClearPlacementBlockFeedback();
                    }
                    else
                    {
                        PedestrianCrossingToolkitState.RejectAutoScanPreviewProposal(-1);
                    }
                }

                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    if (Input.GetMouseButtonDown(1))
                        PedestrianCrossingToolkitPanel.NotifyToolClearedByRightClick();

                    PedestrianCrossingToolkitState.SetActiveMode(PedestrianToolMode.None);
                    ToolsModifierControl.SetTool<DefaultTool>();
                }

                return;
            }

            UpdateSignalGuideJoinCache(activeMode);
            ClearSignalGuideWorldVisual();
            UpdateExclusionZoneCache(activeMode);
            UpdatePreview();

            if (Input.GetMouseButtonDown(0))
            {
                if (_preview.IsValid)
                {
                    if (PedestrianCrossingToolkitState.ActiveMode == PedestrianToolMode.RemoveCrossing)
                        PedestrianCrossingToolkitState.ConfirmRemoval(_preview);
                    else if (PedestrianCrossingToolkitState.ActiveMode == PedestrianToolMode.SubwayPointToPoint)
                    {
                        string blockedMessage;
                        if (!PedestrianCrossingToolkitState.ConfirmSubwayPointToPointClick(_preview, out blockedMessage))
                            HoldPlacementBlockFeedback(_preview, blockedMessage);
                        else
                            ClearPlacementBlockFeedback();
                    }
                    else
                    {
                        string blockedMessage;
                        if (!PedestrianCrossingToolkitState.ConfirmPlacement(_preview, out blockedMessage))
                        {
                            HoldPlacementBlockFeedback(_preview, blockedMessage);
                        }
                        else
                        {
                            ClearPlacementBlockFeedback();
                        }
                    }
                }
                else
                {
                    HoldPlacementBlockFeedback(_preview, _preview.Message);
                    Debug.Log("[PedestrianCrossingToolkit] Placement click ignored: "
                              + _preview.Message
                              + " mode="
                              + _preview.Mode
                              + " segment="
                              + _preview.SegmentId
                              + " position="
                              + _preview.SegmentPosition.ToString("0.000")
                              + " nearNode="
                              + _preview.NearNode
                              + " slot="
                              + _preview.SlotNumber
                              + " world="
                              + _preview.WorldPosition);
                }
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                if (Input.GetMouseButtonDown(1))
                    PedestrianCrossingToolkitPanel.NotifyToolClearedByRightClick();

                PedestrianCrossingToolkitState.SetActiveMode(PedestrianToolMode.None);
                ToolsModifierControl.SetTool<DefaultTool>();
            }
        }

        protected override void OnToolGUI(Event e)
        {
            if (e.type != EventType.Repaint)
                return;

            Camera camera = Camera.main;
            if (camera == null)
                return;

            Color oldColor = GUI.color;
            if (PedestrianCrossingToolkitPanel.IsMouseOverAnyBlockingUi())
            {
                ClearExclusionZoneWorldVisual();
                InvalidateExclusionZoneCache();
                GUI.color = oldColor;
                return;
            }

            if (PedestrianCrossingToolkitState.ActiveMode == PedestrianToolMode.AutoScanReject)
            {
                GUI.color = oldColor;
                return;
            }

            DrawSignalPlacementGuide(camera);
            DrawPlacementExclusionZones(camera);
            DrawHoverSignalStatus();
            DrawHoverCrossingInfo();

            if (_preview.SegmentId != 0)
            {
                if (_preview.Mode == PedestrianToolMode.SubwayPointToPoint)
                    DrawPointToPointSubwayPreview(camera, _preview, _previewHoverColor);
                else
                    DrawPreviewPlacementLine(camera, _preview, _previewPlan, _previewHoverColor);
                DrawSnapCursor(camera, _preview, _previewHoverColor);
            }

            GUI.color = oldColor;
        }

        private new void OnGUI()
        {
            if (ToolsModifierControl.toolController == null
                || ToolsModifierControl.toolController.CurrentTool != this
                || !PedestrianCrossingToolkitState.Enabled
                || PedestrianCrossingToolkitState.ActiveMode == PedestrianToolMode.None)
            {
                return;
            }

            try
            {
                OnToolGUI(Event.current);
            }
            catch (Exception e)
            {
                Debug.LogError("[PedestrianCrossingToolkit] Interaction tool GUI disabled after runtime error: " + e);
                PedestrianCrossingToolkitState.SetActiveMode(PedestrianToolMode.None);
                ToolsModifierControl.SetTool<DefaultTool>();
            }
        }

        protected override void OnDisable()
        {
            _hasToolInfoScreenRect = false;
            _preview = CrossingPlacementRecord.None;
            _previewPlan = CrossingPlacementPlan.Invalid;
            _previewTouchedAsset = CrossingPlacementAsset.None;
            _hoverCrossingInfoAsset = CrossingPlacementAsset.None;
            _previewTouchesExisting = false;
            _hoverAutoScanPreviewProposalIndex = -1;
            _hoverAutoScanPreviewPlacement = CrossingPlacementRecord.None;
            _hoverAutoScanPreviewPlan = CrossingPlacementPlan.Invalid;
            ClearSignalGuideJoinCache();
            ClearSignalGuideWorldVisual();
            ClearExclusionZoneCache();
            ClearExclusionZoneWorldVisual();
            _hasHoverSignalStatus = false;
            _hoverSignalAsset = CrossingPlacementAsset.None;
            _hasHoverCrossingInfo = false;
            ShowToolInfo(false, null, Vector3.zero);

            if (PedestrianCrossingToolkitState.ActiveMode != PedestrianToolMode.None)
            {
                PedestrianCrossingToolkitPanel.CloseForExternalUiSelection();
                Debug.Log("[PedestrianCrossingToolkit] Interaction tool disabled; toolkit closed.");
            }
        }

        private void UpdatePreview()
        {
            CrossingPlacementRecord preview;
            if (!TryBuildPreview(out preview))
                preview = CrossingPlacementRecord.None;

            _preview = preview;
            PedestrianCrossingToolkitState.SetPreview(preview);
            RefreshPreviewRenderState(preview);
            Camera camera = Camera.main;
            RefreshHoverSignalStatus(preview);
            if (_hasHoverSignalStatus)
            {
                StoreSignalStatusScreenRect(camera, _hoverSignalStatusWorldPosition);
                ShowToolInfo(false, null, Vector3.zero);
                return;
            }

            RefreshHoverCrossingInfo(preview);
            if (_hasHoverCrossingInfo)
            {
                Vector3 crossingInfoAnchor = _hoverCrossingInfoAsset.Plan.IsValid
                    ? _hoverCrossingInfoAsset.Plan.Center
                    : _hoverCrossingInfoAsset.Placement.WorldPosition;
                StoreCrossingInfoScreenRect(camera, crossingInfoAnchor);
                ShowToolInfo(false, null, Vector3.zero);
                return;
            }

            string text = GetToolInfoText(preview);
            Vector3 anchor = IsHoldingPlacementBlockFeedback() ? _placementBlockPosition : preview.WorldPosition;
            Vector3 toolInfoWorld = GetToolInfoPosition(camera, anchor, text);
            StoreToolInfoScreenRect(camera, toolInfoWorld, text);
            ShowToolInfo(true, text, toolInfoWorld);
        }

        private void UpdateAutoScanRejectHover()
        {
            Camera camera = Camera.main;
            _hoverAutoScanPreviewProposalIndex = -1;
            _hoverAutoScanPreviewPlacement = CrossingPlacementRecord.None;
            _hoverAutoScanPreviewPlan = CrossingPlacementPlan.Invalid;
            _hasToolInfoScreenRect = false;

            if (camera == null || !PedestrianCrossingToolkitState.HasAutoScanPreviewPlan)
            {
                ShowToolInfo(false, null, Vector3.zero);
                return;
            }

            int proposalIndex;
            CrossingPlacementRecord placement;
            CrossingPlacementPlan plan;
            if (!PedestrianCrossingToolkitState.TryGetAutoScanPreviewProposalNearScreen(camera, Input.mousePosition, out proposalIndex, out placement, out plan))
            {
                ShowToolInfo(false, null, Vector3.zero);
                return;
            }

            _hoverAutoScanPreviewProposalIndex = proposalIndex;
            _hoverAutoScanPreviewPlacement = placement;
            _hoverAutoScanPreviewPlan = plan;
            string text = "Click to reject this Auto Scan " + PedestrianCrossingToolkitState.GetModeLabel(placement.Mode) + ".";
            Vector3 anchor = plan.Center + Vector3.up * 12f;
            Vector3 toolInfoWorld = GetToolInfoPosition(camera, anchor, text);
            StoreToolInfoScreenRect(camera, toolInfoWorld, text);
            ShowToolInfo(true, text, toolInfoWorld);
        }

        private void RefreshPreviewRenderState(CrossingPlacementRecord preview)
        {
            _previewPlan = CrossingPlacementPlan.Invalid;
            _previewTouchedAsset = CrossingPlacementAsset.None;
            _previewTouchesExisting = false;
            _hasHoverSignalStatus = false;
            _hoverSignalAsset = CrossingPlacementAsset.None;
            _hasHoverCrossingInfo = false;
            _hoverCrossingInfoAsset = CrossingPlacementAsset.None;
            _previewHoverColor = GetHoverColor(preview, _previewTouchedAsset, false);

            if (!preview.IsValid)
                return;

            if (preview.Mode == PedestrianToolMode.RemoveCrossing)
            {
                _previewTouchesExisting = CrossingPlacementRegistry.TryGetAssetAt(preview, out _previewTouchedAsset);
                _previewHoverColor = GetHoverColor(preview, _previewTouchedAsset, _previewTouchesExisting);
                return;
            }

            _previewPlan = CrossingPlacementPlanner.Build(preview);
            if (!_previewPlan.IsValid)
                return;

            _previewTouchesExisting = CrossingPlacementRegistry.TryGetAssetAt(preview, _previewPlan, out _previewTouchedAsset);
            _previewHoverColor = GetHoverColor(preview, _previewTouchedAsset, _previewTouchesExisting);
        }

        private void RefreshHoverSignalStatus(CrossingPlacementRecord preview)
        {
            _hasHoverSignalStatus = false;
            _hoverSignalStatus = default(CrossingPathBuilder.SignalControllerDebugSnapshot);
            _hoverSignalAsset = CrossingPlacementAsset.None;
            _hoverSignalStatusWorldPosition = Vector3.zero;

            if (IsHoldingPlacementBlockFeedback() || preview.SegmentId == 0)
                return;

            CrossingPlacementRecord probe = new CrossingPlacementRecord(
                PedestrianToolMode.RemoveCrossing,
                preview.SegmentId,
                preview.SegmentPosition,
                preview.WorldPosition,
                true,
                string.Empty,
                preview.NearNode,
                preview.SlotNumber,
                preview.IsEndSegmentSlot,
                preview.TargetNodeId);
            CrossingPlacementAsset asset;
            if (!CrossingPlacementRegistry.TryGetAssetAt(probe, out asset)
                || asset.Placement.Mode != PedestrianToolMode.SignalCrossing)
            {
                return;
            }

            CrossingPathBuilder.SignalControllerDebugSnapshot snapshot;
            if (!CrossingPathBuilder.TryGetSignalControllerDebugSnapshot(asset.Id, out snapshot) || !snapshot.IsValid)
                return;

            _hoverSignalStatus = snapshot;
            _hoverSignalAsset = asset;
            _hoverSignalStatusWorldPosition = snapshot.Center;
            _hasHoverSignalStatus = true;
        }

        private void RefreshHoverCrossingInfo(CrossingPlacementRecord preview)
        {
            _hasHoverCrossingInfo = false;
            _hoverCrossingInfoAsset = CrossingPlacementAsset.None;

            if (IsHoldingPlacementBlockFeedback()
                || !preview.IsValid
                || preview.Mode != PedestrianToolMode.RemoveCrossing)
            {
                return;
            }

            CrossingPlacementAsset asset;
            if (_previewTouchesExisting && _previewTouchedAsset.Id != 0)
                asset = _previewTouchedAsset;
            else if (!CrossingPlacementRegistry.TryGetAssetAt(preview, out asset))
                return;

            _hoverCrossingInfoAsset = asset;
            _hasHoverCrossingInfo = asset.Id != 0;
        }

        public static bool TryGetToolInfoScreenRect(out Rect rect)
        {
            rect = _toolInfoScreenRect;
            return _hasToolInfoScreenRect;
        }

        private bool TryBuildPreview(out CrossingPlacementRecord preview)
        {
            preview = CrossingPlacementRecord.None;

            Camera camera = Camera.main;
            if (camera == null)
            {
                preview = InvalidPreview("No active camera.", Vector3.zero);
                return false;
            }

            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            ToolBase.RaycastOutput output;
            PedestrianToolMode mode = PedestrianCrossingToolkitState.ActiveMode;
            bool hasRaycast = TryRaycastPlacementTarget(ray, camera.farClipPlane, mode, out output);

            if (!hasRaycast || output.m_netSegment == 0)
            {
                RoadSnapResult signalJoinSnap;
                if (mode == PedestrianToolMode.SignalCrossing
                    && RoadSnapResolver.TryResolveSignalJoinNode(output.m_netNode, out signalJoinSnap))
                {
                    return TryBuildPreviewFromSnap(mode, output.m_hitPos, signalJoinSnap, out preview);
                }

                preview = InvalidPreview(GetHoverTargetMessage(mode), output.m_hitPos);
                return true;
            }

            ushort segmentId = output.m_netSegment;
            if (CrossingPathBuilder.IsBuiltCrossingSegment(segmentId))
            {
                preview = InvalidPreview("Hover over the road surface, not an existing crossing structure.", output.m_hitPos);
                return true;
            }

            NetManager netManager = NetManager.instance;
            if (segmentId >= netManager.m_segments.m_size)
            {
                preview = InvalidPreview("Invalid road segment.", output.m_hitPos);
                return true;
            }

            ref NetSegment segment = ref netManager.m_segments.m_buffer[segmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0
                || segment.Info == null
                || !IsValidPlacementTarget(mode, segmentId, ref segment))
            {
                preview = InvalidPreview(GetInvalidTargetMessage(mode), output.m_hitPos);
                return true;
            }

            RoadSnapResult moduleSnap;
            if (!RoadSnapResolver.TryResolve(segmentId, output.m_hitPos, mode, out moduleSnap))
            {
                preview = InvalidPreview("Unable to resolve a road placement slot.", output.m_hitPos);
                return true;
            }

            return TryBuildPreviewFromSnap(mode, output.m_hitPos, moduleSnap, out preview);
        }

        private static bool TryRaycastPlacementTarget(Ray ray, float length, PedestrianToolMode mode, out ToolBase.RaycastOutput output)
        {
            if (TryRaycastNet(ray, length, ItemClass.Service.Road, ItemClass.SubService.None, ItemClass.Layer.Default, out output))
                return true;

            if (!CanSelectNonRoadTargets(mode))
                return false;

            return TryRaycastNet(ray, length, ItemClass.Service.PublicTransport, ItemClass.SubService.PublicTransportTrain, ItemClass.Layer.Default, out output)
                   || TryRaycastNet(ray, length, ItemClass.Service.PublicTransport, ItemClass.SubService.PublicTransportTrain, ItemClass.Layer.PublicTransport, out output)
                   || TryRaycastNet(ray, length, ItemClass.Service.PublicTransport, ItemClass.SubService.PublicTransportMetro, ItemClass.Layer.Default, out output)
                   || TryRaycastNet(ray, length, ItemClass.Service.PublicTransport, ItemClass.SubService.PublicTransportMetro, ItemClass.Layer.PublicTransport, out output);
        }

        public static bool TryRaycastRoadSegmentForOverlay(Camera camera, out ushort segmentId)
        {
            segmentId = 0;
            if (camera == null)
                return false;

            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            ToolBase.RaycastOutput output;
            if (!TryRaycastNet(ray, camera.farClipPlane, ItemClass.Service.Road, ItemClass.SubService.None, ItemClass.Layer.Default, out output)
                || output.m_netSegment == 0)
            {
                return false;
            }

            segmentId = output.m_netSegment;
            return true;
        }

        private static bool TryRaycastNet(Ray ray, float length, ItemClass.Service service, ItemClass.SubService subService, ItemClass.Layer layer, out ToolBase.RaycastOutput output)
        {
            ToolBase.RaycastInput input = new ToolBase.RaycastInput(ray, length);
            input.m_netService = new ToolBase.RaycastService(service, subService, layer);
            input.m_ignoreNodeFlags = NetNode.Flags.None;
            input.m_ignoreSegmentFlags = NetSegment.Flags.None;
            return ToolBase.RayCast(input, out output);
        }

        private static bool IsGradeSeparatedMode(PedestrianToolMode mode)
        {
            return mode == PedestrianToolMode.SubwayLink
                   || mode == PedestrianToolMode.SubwayPointToPoint
                   || mode == PedestrianToolMode.PedestrianBridge;
        }

        private static bool CanSelectNonRoadTargets(PedestrianToolMode mode)
        {
            return mode == PedestrianToolMode.SubwayLink
                   || mode == PedestrianToolMode.PedestrianBridge
                   || mode == PedestrianToolMode.RemoveCrossing;
        }

        private static bool IsValidPlacementTarget(PedestrianToolMode mode, ushort segmentId, ref NetSegment segment)
        {
            switch (mode)
            {
                case PedestrianToolMode.PedestrianBridge:
                    return RoadPlacementRules.AllowsPedestrianBridgePlacementTarget(segmentId);
                case PedestrianToolMode.SubwayLink:
                    return RoadPlacementRules.AllowsSubwayPlacementTarget(segmentId);
                case PedestrianToolMode.SubwayPointToPoint:
                    return RoadPlacementRules.AllowsManualSubwayPlacementTarget(segmentId);
            }

            if (mode == PedestrianToolMode.RemoveCrossing)
            {
                return segment.Info != null
                       && (segment.Info.m_netAI is RoadBaseAI
                           || RoadPlacementRules.AllowsGradeSeparatedPlacementTarget(segmentId));
            }

            return segment.Info != null && segment.Info.m_netAI is RoadBaseAI;
        }

        private static string GetHoverTargetMessage(PedestrianToolMode mode)
        {
            switch (mode)
            {
                case PedestrianToolMode.PedestrianBridge:
                    return "Hover over a road, surface train track, or terrain-level metro track.";
                case PedestrianToolMode.SubwayLink:
                    return "Hover over a road, surface/elevated train track, or terrain-level/elevated metro track.";
                case PedestrianToolMode.RemoveCrossing:
                    return "Hover over an existing crossing target.";
                default:
                    return "Hover over a road segment.";
            }
        }

        private static string GetInvalidTargetMessage(PedestrianToolMode mode)
        {
            switch (mode)
            {
                case PedestrianToolMode.PedestrianBridge:
                    return "Selected object is not a normal surface road, surface train track, or terrain-level metro track.";
                case PedestrianToolMode.SubwayLink:
                    return "Selected object is not a normal surface road, surface/elevated train track, or terrain-level/elevated metro track.";
                case PedestrianToolMode.SubwayPointToPoint:
                    return "Selected object is not a road segment.";
                case PedestrianToolMode.RemoveCrossing:
                    return "Selected object is not a supported crossing target.";
                default:
                    return "Selected object is not a road segment.";
            }
        }

        private bool TryBuildPreviewFromSnap(PedestrianToolMode mode, Vector3 rawHitPosition, RoadSnapResult moduleSnap, out CrossingPlacementRecord preview)
        {
            preview = CrossingPlacementRecord.None;
            ushort segmentId = moduleSnap.SegmentId;
            Vector3 closest = moduleSnap.WorldPosition;

            float position = moduleSnap.SegmentPosition;
            bool nearNode = moduleSnap.IsEndpointSlot;
            if (mode == PedestrianToolMode.SubwayPointToPoint)
            {
                CrossingPlacementRecord endpoint = new CrossingPlacementRecord(
                    mode,
                    segmentId,
                    position,
                    closest,
                    true,
                    string.Empty,
                    nearNode,
                    moduleSnap.SlotNumber,
                    moduleSnap.IsEndSegmentSlot,
                    moduleSnap.TargetNodeId);
                CrossingPlacementRecord previewEndpoint;
                CrossingPlacementResult endpointResult = SubwayPointToPointTool.PreviewEndpoint(endpoint, out previewEndpoint);
                preview = new CrossingPlacementRecord(
                    mode,
                    segmentId,
                    position,
                    previewEndpoint.WorldPosition,
                    endpointResult.Success,
                    endpointResult.Message,
                    nearNode,
                    moduleSnap.SlotNumber,
                    moduleSnap.IsEndSegmentSlot,
                    moduleSnap.TargetNodeId);
                return true;
            }

            CrossingPlacementResult result = ValidatePreview(mode, segmentId, position, closest, nearNode, moduleSnap.SlotNumber, moduleSnap.IsEndSegmentSlot, moduleSnap.TargetNodeId);

            preview = new CrossingPlacementRecord(
                mode,
                segmentId,
                position,
                closest,
                result.Success,
                result.Message,
                nearNode,
                moduleSnap.SlotNumber,
                moduleSnap.IsEndSegmentSlot,
                moduleSnap.TargetNodeId);
            return true;
        }

        private CrossingPlacementRecord InvalidPreview(string message, Vector3 position)
        {
            return new CrossingPlacementRecord(PedestrianCrossingToolkitState.ActiveMode, 0, 0f, position, false, message);
        }

        private CrossingPlacementResult ValidatePreview(PedestrianToolMode mode, ushort segmentId, float segmentPosition, Vector3 worldPosition, bool nearNode, int slotNumber, bool isEndSegmentSlot, ushort targetNodeId)
        {
            if (mode == PedestrianToolMode.RemoveCrossing)
                return RemovalCrossingTool.PreviewRemoval(segmentId, segmentPosition, worldPosition, nearNode, slotNumber, isEndSegmentSlot);

            if (mode == PedestrianToolMode.SubwayPointToPoint)
            {
                CrossingPlacementRecord endpoint = new CrossingPlacementRecord(
                    mode,
                    segmentId,
                    segmentPosition,
                    worldPosition,
                    true,
                    string.Empty,
                    nearNode,
                    slotNumber,
                    isEndSegmentSlot,
                    targetNodeId);
                return SubwayPointToPointTool.PreviewEndpoint(endpoint);
            }

            CrossingPlacementRecord placement = new CrossingPlacementRecord(
                mode,
                segmentId,
                segmentPosition,
                worldPosition,
                true,
                string.Empty,
                nearNode,
                slotNumber,
                isEndSegmentSlot,
                targetNodeId);
            return CrossingPlacementPolicy.Evaluate(placement).ToPlacementResult();
        }

        private void HoldPlacementBlockFeedback(CrossingPlacementRecord preview, string message)
        {
            _placementBlockUntil = Time.realtimeSinceStartup + PlacementBlockHoldSeconds;
            _placementBlockMessage = string.IsNullOrEmpty(message) ? "Placement blocked." : message;
            _placementBlockPosition = preview.WorldPosition;
        }

        private void ClearPlacementBlockFeedback()
        {
            _placementBlockUntil = 0f;
            _placementBlockMessage = string.Empty;
            _placementBlockPosition = Vector3.zero;
        }

        private bool IsHoldingPlacementBlockFeedback()
        {
            return _placementBlockUntil > Time.realtimeSinceStartup;
        }

        private string GetToolInfoText(CrossingPlacementRecord preview)
        {
            if (IsHoldingPlacementBlockFeedback())
                return "Cannot place here: " + _placementBlockMessage;

            if (!preview.IsValid)
                return preview.Message;

            if (preview.Mode == PedestrianToolMode.RemoveCrossing)
            {
                CrossingPlacementAsset asset;
                if (_previewTouchesExisting && _previewTouchedAsset.Id != 0)
                    asset = _previewTouchedAsset;
                else if (!CrossingPlacementRegistry.TryGetAssetAt(preview, out asset))
                    return "No crossing at this location.";

                return BuildCrossingQueryInfoText(asset, true);
            }

            if (_previewTouchesExisting)
            {
                return _previewTouchedAsset.Placement.Mode == preview.Mode
                    ? "update this " + GetExistingSameTypeLabel(_previewTouchedAsset.Placement.Mode)
                    : "replace this " + GetExistingReplacementLabel(_previewTouchedAsset.Placement.Mode);
            }

            if (preview.Mode == PedestrianToolMode.SubwayPointToPoint && SubwayPointToPointTool.HasStartEndpoint)
                return "Click to place the end subway entrance.";

            return "Click to " + (preview.Mode == PedestrianToolMode.RemoveCrossing
                ? "remove crossing"
                : "place " + PedestrianCrossingToolkitState.GetModeLabel(preview.Mode));
        }

        private static string BuildCrossingQueryInfoText(CrossingPlacementAsset asset, bool includeAction)
        {
            string text = ToTitleCase(PedestrianCrossingToolkitState.GetModeLabel(asset.Placement.Mode))
                          + " #" + asset.Id
                          + "\nPath: " + FormatPathQueryState(asset)
                          + "\nSuppression: " + FormatSuppressionQueryState(asset)
                          + "\nSignal: " + FormatSignalQueryState(asset)
                          + "\nOwned assets: " + FormatOwnedAssetQueryState(asset);

            if (includeAction)
                text += "\nClick to remove.";

            return text;
        }

        private static string FormatPathQueryState(CrossingPlacementAsset asset)
        {
            if (!asset.Plan.IsValid)
                return "needs rebuild";

            if (CrossingConnectivityPlanner.HasLinkForAsset(asset.Id))
                return "connected";

            switch (asset.Plan.ApplicationKind)
            {
                case CrossingApplicationKind.SurfaceCrossing:
                case CrossingApplicationKind.SignalizedSurfaceCrossing:
                    return "surface only";
                default:
                    return "not connected";
            }
        }

        private static string FormatSuppressionQueryState(CrossingPlacementAsset asset)
        {
            if (!asset.Plan.IsValid)
                return "unknown";

            if (!GradeSeparatedPlacementGeometryResolver.IsGradeSeparated(asset.Plan.ApplicationKind)
                || !RoadPlacementRules.IsRoadGradeSeparatedPlacementTarget(asset.Placement.SegmentId))
            {
                return "n/a";
            }

            return GradeSeparatedVanillaCrossingSuppression.HasActiveSuppressionForAsset(asset)
                ? "active"
                : "not active";
        }

        private static string FormatSignalQueryState(CrossingPlacementAsset asset)
        {
            if (asset.Placement.Mode != PedestrianToolMode.SignalCrossing)
                return "n/a";

            CrossingPathBuilder.SignalControllerDebugSnapshot snapshot;
            if (!CrossingPathBuilder.TryGetSignalControllerDebugSnapshot(asset.Id, out snapshot) || !snapshot.IsValid)
                return "controller missing";

            return FormatSignalPhase(snapshot)
                   + ", wait " + (snapshot.HasPedestriansWaitingAtEntrance ? "yes" : "no")
                   + ", crossing " + (snapshot.HasPedestriansOnCrossing ? "yes" : "no");
        }

        private static string FormatSignalPhase(CrossingPathBuilder.SignalControllerDebugSnapshot snapshot)
        {
            if (snapshot.Phase == "Idle")
                return "idle";
            if (snapshot.Phase == "Crossing")
                return "crossing";
            if (snapshot.Phase == "Clearance")
                return "clearance";

            return string.IsNullOrEmpty(snapshot.Phase) ? "unknown" : snapshot.Phase;
        }

        private static string FormatOwnedAssetQueryState(CrossingPlacementAsset asset)
        {
            if (!asset.Plan.IsValid)
                return "needs rebuild";

            int ownedCount = CrossingPathBuilder.CountBuiltOwnedItemsForAsset(asset.Id);
            return ownedCount > 0 ? "healthy (" + ownedCount + ")" : "missing";
        }

        private static string ToTitleCase(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string[] words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (string.IsNullOrEmpty(words[i]))
                    continue;

                words[i] = char.ToUpper(words[i][0]) + (words[i].Length > 1 ? words[i].Substring(1) : string.Empty);
            }

            return string.Join(" ", words);
        }

        private static string GetExistingSameTypeLabel(PedestrianToolMode mode)
        {
            return PedestrianCrossingToolkitState.GetModeLabel(mode);
        }

        private static string GetExistingReplacementLabel(PedestrianToolMode mode)
        {
            string label = PedestrianCrossingToolkitState.GetModeLabel(mode);
            return label.EndsWith("crossing")
                ? label
                : label + " crossing";
        }

        private Vector3 GetToolInfoPosition(Camera camera, Vector3 anchor, string text)
        {
            if (camera == null || string.IsNullOrEmpty(text))
                return anchor;

            Vector3 screen = camera.WorldToScreenPoint(anchor);
            if (screen.z <= 0f)
                return anchor;

            Vector2 size = CalculateToolInfoSize(text);
            float estimatedWidth = size.x;
            bool wouldOverflowRight = screen.x + estimatedWidth > Screen.width - 24f;
            bool wouldOverflowLeft = screen.x < 24f;
            if (!wouldOverflowRight && !wouldOverflowLeft)
                return anchor;

            float clampedX = Mathf.Clamp(screen.x, 24f, Mathf.Max(24f, Screen.width - estimatedWidth - 24f));
            Vector3 loweredScreen = new Vector3(
                clampedX,
                Mathf.Max(24f, screen.y - ToolTipBelowCursorPixels),
                screen.z);
            return camera.ScreenToWorldPoint(loweredScreen);
        }

        private void StoreToolInfoScreenRect(Camera camera, Vector3 worldPosition, string text)
        {
            _hasToolInfoScreenRect = false;
            if (camera == null || string.IsNullOrEmpty(text) || Screen.width <= 0 || Screen.height <= 0)
                return;

            Vector3 screen = camera.WorldToScreenPoint(worldPosition);
            if (screen.z <= 0f)
                return;

            Vector2 size = CalculateToolInfoSize(text);
            float width = size.x;
            float height = size.y;
            float guiX = Mathf.Clamp(screen.x - 24f, 0f, Mathf.Max(0f, Screen.width - width));
            float guiY = Mathf.Clamp(Screen.height - screen.y - 36f, 0f, Mathf.Max(0f, Screen.height - height));
            _toolInfoScreenRect = new Rect(guiX, guiY, width, height);
            _hasToolInfoScreenRect = true;
        }

        private static Vector2 CalculateToolInfoSize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new Vector2(ToolInfoMinWidth, ToolInfoMinHeight);

            string[] lines = text.Split('\n');
            float maxLineWidth = 0f;
            for (int i = 0; i < lines.Length; i++)
                maxLineWidth = Mathf.Max(maxLineWidth, (lines[i] == null ? 0 : lines[i].Length) * 7.2f);

            float maxWidth = Mathf.Max(ToolInfoMinWidth, Screen.width - ToolInfoMaxWidthPadding);
            float width = Mathf.Clamp(maxLineWidth + ToolInfoPadding * 2f, ToolInfoMinWidth, maxWidth);
            float height = Mathf.Max(ToolInfoMinHeight, lines.Length * ToolInfoLineHeight + ToolInfoPadding);
            return new Vector2(width, height);
        }

        private void StoreSignalStatusScreenRect(Camera camera, Vector3 worldPosition)
        {
            _hasToolInfoScreenRect = false;
            if (camera == null || Screen.width <= 0 || Screen.height <= 0)
            {
                _hasHoverSignalStatus = false;
                return;
            }

            Vector3 screen = camera.WorldToScreenPoint(worldPosition);
            if (screen.z <= 0f)
            {
                _hasHoverSignalStatus = false;
                return;
            }

            float guiX = Mathf.Clamp(screen.x - (SignalStatusPanelWidth * 0.5f), 0f, Mathf.Max(0f, Screen.width - SignalStatusPanelWidth));
            float guiY = Mathf.Clamp(Screen.height - screen.y + SignalStatusPanelBelowCrossingPixels, 0f, Mathf.Max(0f, Screen.height - SignalStatusPanelHeight));
            _toolInfoScreenRect = new Rect(guiX, guiY, SignalStatusPanelWidth, SignalStatusPanelHeight);
            _hasToolInfoScreenRect = true;
        }

        private void StoreCrossingInfoScreenRect(Camera camera, Vector3 worldPosition)
        {
            _hasToolInfoScreenRect = false;
            if (camera == null || Screen.width <= 0 || Screen.height <= 0)
            {
                _hasHoverCrossingInfo = false;
                return;
            }

            Vector3 screen = camera.WorldToScreenPoint(worldPosition);
            if (screen.z <= 0f)
            {
                _hasHoverCrossingInfo = false;
                return;
            }

            float guiX = Mathf.Clamp(screen.x - (SignalStatusPanelWidth * 0.5f), 0f, Mathf.Max(0f, Screen.width - SignalStatusPanelWidth));
            float guiY = Mathf.Clamp(Screen.height - screen.y + SignalStatusPanelBelowCrossingPixels, 0f, Mathf.Max(0f, Screen.height - SignalStatusPanelHeight));
            _toolInfoScreenRect = new Rect(guiX, guiY, SignalStatusPanelWidth, SignalStatusPanelHeight);
            _hasToolInfoScreenRect = true;
        }

        private void DrawHoverSignalStatus()
        {
            if (!_hasHoverSignalStatus || !_hasToolInfoScreenRect)
                return;

            Rect rect = _toolInfoScreenRect;
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            DrawSolidRect(rect);
            DrawSignalStatusBody(new Rect(rect.x + 12f, rect.y + 12f, 33f, 51f), _hoverSignalStatus.VehicleState, true);
            DrawSignalStatusBody(new Rect(rect.x + 54f, rect.y + 18f, 33f, 39f), _hoverSignalStatus.PedestrianState, false);
            DrawSignalStatusText(new Rect(rect.x + 98f, rect.y + 10f, rect.width - 108f, rect.height - 18f));
            GUI.color = oldColor;
        }

        private void DrawHoverCrossingInfo()
        {
            if (!_hasHoverCrossingInfo || !_hasToolInfoScreenRect || _hoverCrossingInfoAsset.Id == 0)
                return;

            Rect rect = _toolInfoScreenRect;
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            DrawSolidRect(rect);
            DrawCrossingInfoAccent(new Rect(rect.x + 12f, rect.y + 12f, 34f, rect.height - 24f), _hoverCrossingInfoAsset.Placement.Mode);
            DrawCrossingInfoText(new Rect(rect.x + 58f, rect.y + 10f, rect.width - 68f, rect.height - 18f), _hoverCrossingInfoAsset);
            GUI.color = oldColor;
        }

        private void DrawCrossingInfoAccent(Rect rect, PedestrianToolMode mode)
        {
            Color accent = GetHoverColor(new CrossingPlacementRecord(mode, 0, 0f, Vector3.zero, true, string.Empty), CrossingPlacementAsset.None, false);
            GUI.color = new Color(0.92f, 0.96f, 1f, 0.94f);
            DrawSolidRect(rect);
            Rect inner = InsetRect(rect, 2f);
            GUI.color = new Color(0.055f, 0.065f, 0.075f, 0.98f);
            DrawSolidRect(inner);
            GUI.color = accent;
            DrawSolidRect(new Rect(inner.x + 5f, inner.y + 8f, inner.width - 10f, 5f));
            DrawSolidRect(new Rect(inner.x + 5f, inner.y + 23f, inner.width - 10f, 5f));
            DrawSolidRect(new Rect(inner.x + 5f, inner.y + 38f, inner.width - 10f, 5f));
            GUI.color = new Color(1f, 1f, 1f, 0.34f);
            DrawSolidRect(new Rect(inner.x + 2f, inner.y + 2f, inner.width - 4f, 1f));
        }

        private void DrawCrossingInfoText(Rect rect, CrossingPlacementAsset asset)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.96f);
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 18f), ToTitleCase(PedestrianCrossingToolkitState.GetModeLabel(asset.Placement.Mode)) + " #" + asset.Id);
            GUI.color = new Color(0.86f, 0.92f, 1f, 0.92f);
            GUI.Label(new Rect(rect.x, rect.y + 19f, rect.width, 18f), "Path: " + FormatPathQueryState(asset));
            GUI.Label(new Rect(rect.x, rect.y + 38f, rect.width, 18f), "Suppression: " + FormatSuppressionQueryState(asset));
            GUI.Label(new Rect(rect.x, rect.y + 57f, rect.width, 18f), "Signal: " + FormatSignalQueryState(asset));
            GUI.Label(new Rect(rect.x, rect.y + 76f, rect.width, 18f), "Owned assets: " + FormatOwnedAssetQueryState(asset));
            GUI.Label(new Rect(rect.x, rect.y + 95f, rect.width, 18f), "Click to remove.");
        }

        private void DrawSignalStatusText(Rect rect)
        {
            CrossingPlacementAsset asset = _hoverSignalAsset;
            if (asset.Id == 0)
                CrossingPlacementRegistry.TryGetAssetById(_hoverSignalStatus.AssetId, out asset);

            string title = asset.Id == 0
                ? "Signal Crossing"
                : "Signal Crossing #" + asset.Id;
            string path = asset.Id == 0 ? "unknown" : FormatPathQueryState(asset);
            string owned = asset.Id == 0 ? "unknown" : FormatOwnedAssetQueryState(asset);

            GUI.color = new Color(1f, 1f, 1f, 0.96f);
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 18f), title);
            GUI.color = new Color(0.86f, 0.92f, 1f, 0.92f);
            GUI.Label(new Rect(rect.x, rect.y + 19f, rect.width, 18f), "Signal: " + FormatSignalPhase(_hoverSignalStatus));
            GUI.Label(new Rect(rect.x, rect.y + 38f, rect.width, 18f), "Waiting: " + (_hoverSignalStatus.HasPedestriansWaitingAtEntrance ? "yes" : "no")
                                                                  + "  Crossing: " + (_hoverSignalStatus.HasPedestriansOnCrossing ? "yes" : "no"));
            GUI.Label(new Rect(rect.x, rect.y + 57f, rect.width, 18f), "Path: " + path);
            GUI.Label(new Rect(rect.x, rect.y + 76f, rect.width, 18f), "Owned assets: " + owned);
            GUI.Label(new Rect(rect.x, rect.y + 95f, rect.width, 18f), "Click remove mode to edit.");
        }

        private void DrawSignalStatusBody(Rect body, RoadBaseAI.TrafficLightState state, bool vehicle)
        {
            GUI.color = new Color(0.92f, 0.96f, 1f, 0.94f);
            DrawSolidRect(body);
            Rect inner = InsetRect(body, 2f);
            GUI.color = new Color(0.055f, 0.065f, 0.075f, 0.98f);
            DrawSolidRect(inner);
            GUI.color = new Color(0.22f, 0.25f, 0.28f, 0.75f);
            DrawSolidRect(new Rect(inner.x + 1f, inner.y + 1f, inner.width - 2f, 1f));

            if (vehicle)
            {
                float lampSize = 13f;
                float lampX = inner.x + ((inner.width - lampSize) * 0.5f);
                bool red = state != RoadBaseAI.TrafficLightState.Green
                           && state != RoadBaseAI.TrafficLightState.RedToGreen
                           && state != RoadBaseAI.TrafficLightState.GreenToRed;
                bool amber = state == RoadBaseAI.TrafficLightState.RedToGreen
                             || state == RoadBaseAI.TrafficLightState.GreenToRed;
                bool green = state == RoadBaseAI.TrafficLightState.Green;
                DrawSignalStatusLamp(new Rect(lampX, inner.y + 3f, lampSize, lampSize), new Color(1f, 0.18f, 0.14f, 1f), new Color(0.24f, 0.03f, 0.025f, 1f), red);
                DrawSignalStatusLamp(new Rect(lampX, inner.y + 17f, lampSize, lampSize), new Color(1f, 0.64f, 0.08f, 1f), new Color(0.25f, 0.12f, 0.02f, 1f), amber);
                DrawSignalStatusLamp(new Rect(lampX, inner.y + 31f, lampSize, lampSize), new Color(0.18f, 0.96f, 0.32f, 1f), new Color(0.02f, 0.18f, 0.05f, 1f), green);
                return;
            }

            float pedestrianLampSize = 13f;
            float pedestrianLampX = inner.x + ((inner.width - pedestrianLampSize) * 0.5f);
            bool pedestrianGreen = state == RoadBaseAI.TrafficLightState.Green;
            DrawSignalStatusLamp(new Rect(pedestrianLampX, inner.y + 5f, pedestrianLampSize, pedestrianLampSize), new Color(1f, 0.18f, 0.14f, 1f), new Color(0.24f, 0.03f, 0.025f, 1f), !pedestrianGreen);
            DrawSignalStatusLamp(new Rect(pedestrianLampX, inner.y + 20f, pedestrianLampSize, pedestrianLampSize), new Color(0.18f, 0.96f, 0.32f, 1f), new Color(0.02f, 0.18f, 0.05f, 1f), pedestrianGreen);
        }

        private void DrawSignalStatusLamp(Rect rect, Color activeColor, Color inactiveColor, bool active)
        {
            DrawCircleRect(rect, new Color(0.015f, 0.018f, 0.02f, 1f));
            Rect lens = InsetRect(rect, 2f);
            DrawCircleRect(lens, active ? activeColor : inactiveColor);
            if (active)
            {
                DrawCircleRect(new Rect(lens.x + 2f, lens.y + 2f, 4f, 4f), new Color(1f, 1f, 1f, 0.28f));
            }
            else
            {
                DrawCircleRect(new Rect(lens.x + 2f, lens.y + lens.height - 5f, lens.width - 4f, lens.width - 4f), new Color(0f, 0f, 0f, 0.25f));
            }
        }

        private void DrawCircleRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, GetSignalLampCircleTexture());
        }

        private static Texture2D GetSignalLampCircleTexture()
        {
            if (_signalLampCircleTexture != null)
                return _signalLampCircleTexture;

            Texture2D texture = new Texture2D(SignalLampTextureSize, SignalLampTextureSize, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (SignalLampTextureSize - 1) * 0.5f;
            float radius = center;
            for (int y = 0; y < SignalLampTextureSize; y++)
            {
                for (int x = 0; x < SignalLampTextureSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);
            _signalLampCircleTexture = texture;
            return _signalLampCircleTexture;
        }

        private static Rect InsetRect(Rect rect, float inset)
        {
            return new Rect(rect.x + inset, rect.y + inset, Mathf.Max(0f, rect.width - inset * 2f), Mathf.Max(0f, rect.height - inset * 2f));
        }

        private void DrawSignalPlacementGuide(Camera camera)
        {
            PedestrianToolMode mode = PedestrianCrossingToolkitState.ActiveMode;
            if (!IsCrossingPlacementGuideMode(mode))
                return;

            if (camera == null || camera.transform.position.y > SignalGuideMaxCameraHeight)
                return;

            NetManager netManager = NetManager.instance;
            if (netManager == null)
                return;

            Vector2 captureCenter = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            DrawScreenCircle(captureCenter, SignalGuideRadiusPixels, new Color(1f, 0.88f, 0.2f, 0.22f), 2f, 48);

            if (mode == PedestrianToolMode.SignalCrossing)
                DrawSignalJoinMarkers(camera, netManager, captureCenter);
        }

        private void UpdateSignalGuideJoinCache(PedestrianToolMode mode)
        {
            if (mode != PedestrianToolMode.SignalCrossing)
            {
                ClearSignalGuideJoinCache();
                return;
            }

            NetManager netManager = NetManager.instance;
            if (netManager == null)
            {
                ClearSignalGuideJoinCache();
                return;
            }

            if (!_signalGuideJoinCacheBuildInProgress && ShouldStartSignalGuideJoinCacheBuild(netManager))
                StartSignalGuideJoinCacheBuild();

            if (_signalGuideJoinCacheBuildInProgress)
                ProcessSignalGuideJoinCacheBuild(netManager);
        }

        private bool ShouldStartSignalGuideJoinCacheBuild(NetManager netManager)
        {
            if (!_signalGuideJoinCacheReady)
                return true;

            return _signalGuideJoinCachedNodeCount != netManager.m_nodeCount
                   || _signalGuideJoinCachedSegmentCount != netManager.m_segmentCount
                   || _signalGuideJoinCachedRegistryRevision != CrossingPlacementRegistry.Revision;
        }

        private void StartSignalGuideJoinCacheBuild()
        {
            _signalGuideJoinBuildBuffer.Clear();
            _signalGuideJoinBuildNextNodeId = 1;
            _signalGuideJoinCacheBuildInProgress = true;
        }

        private void ProcessSignalGuideJoinCacheBuild(NetManager netManager)
        {
            if (netManager == null)
            {
                ClearSignalGuideJoinCache();
                return;
            }

            ushort endNodeId = (ushort)Mathf.Min(
                netManager.m_nodes.m_size,
                _signalGuideJoinBuildNextNodeId + SignalJoinCacheBuildNodeBatchSize);

            EnsurePctSignalExclusionNodes();
            for (ushort nodeId = _signalGuideJoinBuildNextNodeId; nodeId < endNodeId; nodeId++)
            {
                if (IsPctSignalExclusionNode(nodeId))
                    continue;

                RoadSnapResult snap;
                if (RoadSnapResolver.TryResolveSignalJoinNode(nodeId, out snap))
                    _signalGuideJoinBuildBuffer.Add(nodeId);
            }

            _signalGuideJoinBuildNextNodeId = endNodeId;
            if (_signalGuideJoinBuildNextNodeId < netManager.m_nodes.m_size)
                return;

            _signalGuideJoinCache.Clear();
            _signalGuideJoinCache.AddRange(_signalGuideJoinBuildBuffer);
            _signalGuideJoinBuildBuffer.Clear();
            _signalGuideJoinCachedNodeCount = netManager.m_nodeCount;
            _signalGuideJoinCachedSegmentCount = netManager.m_segmentCount;
            _signalGuideJoinCachedRegistryRevision = CrossingPlacementRegistry.Revision;
            _signalGuideJoinCacheReady = true;
            _signalGuideJoinCacheBuildInProgress = false;
        }

        private void ClearSignalGuideJoinCache()
        {
            _signalGuideJoinCache.Clear();
            _signalGuideJoinBuildBuffer.Clear();
            _signalGuideJoinCacheReady = false;
            _signalGuideJoinCacheBuildInProgress = false;
            _signalGuideJoinBuildNextNodeId = 0;
            _signalGuideJoinCachedNodeCount = 0;
            _signalGuideJoinCachedSegmentCount = 0;
            _signalGuideJoinCachedRegistryRevision = 0;
        }

        private void UpdateSignalPlacementGuideWorldVisual(Camera camera, PedestrianToolMode mode)
        {
            if (mode != PedestrianToolMode.SignalCrossing
                || camera == null
                || camera.transform.position.y > SignalGuideMaxCameraHeight
                || !_signalGuideJoinCacheReady)
            {
                ClearSignalGuideWorldVisual();
                return;
            }

            NetManager netManager = NetManager.instance;
            if (netManager == null)
            {
                ClearSignalGuideWorldVisual();
                return;
            }

            Vector2 revealCenter = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            float now = Time.unscaledTime;
            if (!ShouldRefreshSignalGuideWorldVisual(camera, revealCenter, now))
                return;

            RebuildSignalGuideWorldVisual(camera, netManager, revealCenter);
            _signalGuideWorldRefreshTime = now;
            _signalGuideWorldRevealCenter = revealCenter;
            _signalGuideWorldCameraPosition = camera.transform.position;
            _signalGuideWorldCameraRotation = camera.transform.rotation;
        }

        private bool ShouldRefreshSignalGuideWorldVisual(Camera camera, Vector2 revealCenter, float now)
        {
            if (_signalGuideWorldVisual == null || _signalGuideWorldMesh == null)
                return true;

            if (now - _signalGuideWorldRefreshTime >= SignalGuideWorldVisualRefreshSeconds)
                return true;

            if ((revealCenter - _signalGuideWorldRevealCenter).sqrMagnitude >= SignalGuideWorldVisualRefreshPixels * SignalGuideWorldVisualRefreshPixels)
                return true;

            if ((camera.transform.position - _signalGuideWorldCameraPosition).sqrMagnitude >= 4f)
                return true;

            return Quaternion.Angle(camera.transform.rotation, _signalGuideWorldCameraRotation) >= 2f;
        }

        private void RebuildSignalGuideWorldVisual(Camera camera, NetManager netManager, Vector2 revealCenter)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            float revealRadiusSquared = SignalGuideRadiusPixels * SignalGuideRadiusPixels;
            float iconSize = GetSignalGuideIconWorldSize(camera);
            Vector3 cameraRight = camera.transform.right;
            Vector3 cameraUp = camera.transform.up;
            EnsurePctSignalExclusionNodes();

            for (int i = 0; i < _signalGuideJoinCache.Count; i++)
            {
                ushort nodeId = _signalGuideJoinCache[i];
                if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size || IsPctSignalExclusionNode(nodeId))
                    continue;

                ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
                if ((node.m_flags & NetNode.Flags.Created) == 0)
                    continue;

                Vector2 markerCenter;
                if (!WorldToGuiPoint(camera, node.m_position + Vector3.up * SignalJoinMarkerWorldLift, out markerCenter))
                    continue;

                if ((markerCenter - revealCenter).sqrMagnitude > revealRadiusSquared)
                    continue;

                Vector3 iconCenter = node.m_position + Vector3.up * Mathf.Clamp(iconSize * 0.7f, 7f, 18f);
                AddSignalGuideIconQuad(vertices, uvs, triangles, iconCenter, cameraRight, cameraUp, iconSize);
            }

            if (vertices.Count == 0)
            {
                ClearSignalGuideWorldVisual();
                return;
            }

            EnsureSignalGuideWorldVisual();
            if (_signalGuideWorldMesh == null)
                return;

            _signalGuideWorldMesh.Clear();
            _signalGuideWorldMesh.vertices = vertices.ToArray();
            _signalGuideWorldMesh.uv = uvs.ToArray();
            _signalGuideWorldMesh.triangles = triangles.ToArray();
            _signalGuideWorldMesh.RecalculateBounds();
        }

        private static float GetSignalGuideIconWorldSize(Camera camera)
        {
            float cameraSize = camera == null ? 0f : camera.transform.position.y;
            CameraController controller = ToolsModifierControl.cameraController;
            if (controller != null && controller.m_currentSize > 0f)
                cameraSize = controller.m_currentSize;

            return Mathf.Clamp(cameraSize * SignalGuideIconWorldScale, SignalGuideIconMinWorldSize, SignalGuideIconMaxWorldSize);
        }

        private static void AddSignalGuideIconQuad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 center, Vector3 cameraRight, Vector3 cameraUp, float size)
        {
            Vector3 right = cameraRight.normalized * (size * 0.5f);
            Vector3 up = cameraUp.normalized * (size * 0.5f);
            int index = vertices.Count;
            vertices.Add(center - right - up);
            vertices.Add(center - right + up);
            vertices.Add(center + right + up);
            vertices.Add(center + right - up);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
            triangles.Add(index);
            triangles.Add(index + 2);
            triangles.Add(index + 3);
        }

        private void EnsureSignalGuideWorldVisual()
        {
            if (_signalGuideWorldVisual != null && _signalGuideWorldMesh != null)
                return;

            _signalGuideWorldVisual = new GameObject("PCT Signal Placement Targets");
            _signalGuideWorldVisual.hideFlags = HideFlags.HideAndDontSave;
            _signalGuideWorldMesh = new Mesh();
            _signalGuideWorldMesh.name = "PCT Signal Placement Targets Mesh";

            MeshFilter filter = _signalGuideWorldVisual.AddComponent<MeshFilter>();
            filter.mesh = _signalGuideWorldMesh;
            MeshRenderer renderer = _signalGuideWorldVisual.AddComponent<MeshRenderer>();
            renderer.material = GetSignalGuideWorldMaterial();
        }

        private void ClearSignalGuideWorldVisual()
        {
            if (_signalGuideWorldVisual != null)
                UnityEngine.Object.Destroy(_signalGuideWorldVisual);

            _signalGuideWorldVisual = null;
            _signalGuideWorldMesh = null;
            _signalGuideWorldRefreshTime = -100f;
            _signalGuideWorldRevealCenter = Vector2.zero;
            _signalGuideWorldCameraPosition = Vector3.zero;
            _signalGuideWorldCameraRotation = Quaternion.identity;
        }

        private static Material GetSignalGuideWorldMaterial()
        {
            if (_signalGuideWorldMaterial != null)
                return _signalGuideWorldMaterial;

            Shader shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Transparent/Diffuse") ?? Shader.Find("Diffuse");
            _signalGuideWorldMaterial = new Material(shader);
            _signalGuideWorldMaterial.hideFlags = HideFlags.HideAndDontSave;
            _signalGuideWorldMaterial.color = SignalJoinMarkerColor;
            _signalGuideWorldMaterial.mainTexture = GetSignalGuideIconTexture();
            _signalGuideWorldMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _signalGuideWorldMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _signalGuideWorldMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _signalGuideWorldMaterial.SetInt("_ZWrite", 0);
            _signalGuideWorldMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            _signalGuideWorldMaterial.renderQueue = 3000;
            return _signalGuideWorldMaterial;
        }

        private static Texture2D GetSignalGuideIconTexture()
        {
            if (_signalGuideIconTexture != null)
                return _signalGuideIconTexture;

            Texture2D texture = new Texture2D(SignalGuideIconTextureSize, SignalGuideIconTextureSize, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (SignalGuideIconTextureSize - 1) * 0.5f;
            float outerRadius = center - 2f;
            float innerRadius = outerRadius * 0.48f;
            for (int y = 0; y < SignalGuideIconTextureSize; y++)
            {
                for (int x = 0; x < SignalGuideIconTextureSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float alpha = 0f;

                    if (distance <= outerRadius)
                    {
                        bool outerRing = distance >= outerRadius - 5f;
                        bool innerRing = distance >= innerRadius - 3f && distance <= innerRadius + 3f;
                        bool cross = (Mathf.Abs(dx) <= 2.6f && Mathf.Abs(dy) <= outerRadius - 7f)
                                     || (Mathf.Abs(dy) <= 2.6f && Mathf.Abs(dx) <= outerRadius - 7f);
                        bool centerDot = distance <= 4.5f;

                        if (outerRing || innerRing || cross || centerDot)
                            alpha = Mathf.Clamp01(outerRadius + 0.5f - distance);
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);
            _signalGuideIconTexture = texture;
            return _signalGuideIconTexture;
        }

        private void DrawSignalJoinMarkers(Camera camera, NetManager netManager, Vector2 captureCenter)
        {
            if (camera == null || netManager == null || !_signalGuideJoinCacheReady)
                return;

            float revealRadiusSquared = SignalGuideRadiusPixels * SignalGuideRadiusPixels;
            Vector3 lift = Vector3.up * SignalJoinMarkerWorldLift;
            EnsurePctSignalExclusionNodes();
            for (int i = 0; i < _signalGuideJoinCache.Count; i++)
            {
                ushort nodeId = _signalGuideJoinCache[i];
                if (nodeId == 0 || nodeId >= netManager.m_nodes.m_size)
                    continue;

                if (IsPctSignalExclusionNode(nodeId))
                    continue;

                ref NetNode node = ref netManager.m_nodes.m_buffer[nodeId];
                if ((node.m_flags & NetNode.Flags.Created) == 0)
                    continue;

                Vector2 markerCenter;
                if (!WorldToGuiPoint(camera, node.m_position + lift, out markerCenter))
                    continue;

                if ((markerCenter - captureCenter).sqrMagnitude > revealRadiusSquared)
                    continue;

                DrawScreenCircle(markerCenter, SignalJoinMarkerOuterRadiusPixels, SignalJoinMarkerColor, 3f, 18);
                DrawScreenCircle(markerCenter, SignalJoinMarkerInnerRadiusPixels, SignalJoinMarkerColor, 2f, 12);
            }
        }

        private static bool IsCrossingPlacementGuideMode(PedestrianToolMode mode)
        {
            return mode == PedestrianToolMode.MidBlockCrossing
                   || mode == PedestrianToolMode.SignalCrossing
                   || mode == PedestrianToolMode.SubwayLink
                   || mode == PedestrianToolMode.SubwayPointToPoint
                   || mode == PedestrianToolMode.PedestrianBridge;
        }

        private void DrawPlacementExclusionZones(Camera camera)
        {
            if (camera == null || camera.transform.position.y > JunctionExclusionOverlayMaxCameraHeight)
            {
                ClearExclusionZoneWorldVisual();
                InvalidateExclusionZoneCache();
                return;
            }

            Vector2 revealCenter = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            PedestrianToolMode mode = PedestrianCrossingToolkitState.ActiveMode;
            ExclusionZoneCacheKind cacheKind = GetExclusionZoneCacheKind(mode);
            if (cacheKind == ExclusionZoneCacheKind.None)
            {
                ClearExclusionZoneWorldVisual();
                InvalidateExclusionZoneCache();
                return;
            }

            float now = Time.unscaledTime;
            if (!ShouldRefreshExclusionZoneWorldVisual(camera, revealCenter, mode, now))
                return;

            bool rendered = _exclusionZoneCacheReady
                            && _exclusionZoneCacheKind == cacheKind
                            && DrawCachedJunctionExclusionZones(camera, revealCenter);

            if (!rendered)
                ClearExclusionZoneWorldVisual();

            StoreExclusionZoneCache(camera, revealCenter, mode, now);
        }

        private void UpdateExclusionZoneCache(PedestrianToolMode mode)
        {
            ExclusionZoneCacheKind kind = GetExclusionZoneCacheKind(mode);
            if (kind == ExclusionZoneCacheKind.None)
            {
                ClearExclusionZoneCache();
                return;
            }

            NetManager netManager = NetManager.instance;
            if (netManager == null)
            {
                ClearExclusionZoneCache();
                return;
            }

            if (!_exclusionZoneCacheBuildInProgress && ShouldStartExclusionZoneCacheBuild(netManager, kind))
                StartExclusionZoneCacheBuild(kind);

            if (_exclusionZoneCacheBuildInProgress)
                ProcessExclusionZoneCacheBuild(netManager);
        }

        private bool ShouldStartExclusionZoneCacheBuild(NetManager netManager, ExclusionZoneCacheKind kind)
        {
            if (!_exclusionZoneCacheReady)
                return true;

            return _exclusionZoneCacheKind != kind
                   || _exclusionZoneCachedNodeCount != netManager.m_nodeCount
                   || _exclusionZoneCachedSegmentCount != netManager.m_segmentCount
                   || _exclusionZoneCachedRegistryRevision != CrossingPlacementRegistry.Revision;
        }

        private void StartExclusionZoneCacheBuild(ExclusionZoneCacheKind kind)
        {
            _exclusionZoneBuildBuffer.Clear();
            _exclusionZoneBuildKind = kind;
            _exclusionZoneBuildNextNodeId = 1;
            _exclusionZoneCacheBuildInProgress = true;
            EnsurePctSignalExclusionNodes();
        }

        private void ProcessExclusionZoneCacheBuild(NetManager netManager)
        {
            if (netManager == null)
            {
                ClearExclusionZoneCache();
                return;
            }

            ushort endNodeId = (ushort)Mathf.Min(
                netManager.m_nodes.m_size,
                _exclusionZoneBuildNextNodeId + ExclusionZoneCacheBuildNodeBatchSize);

            for (ushort nodeId = _exclusionZoneBuildNextNodeId; nodeId < endNodeId; nodeId++)
            {
                if (IsPctSignalExclusionNode(nodeId))
                    continue;

                RoadPlacementRules.JunctionExclusionZone zone;
                bool hasZone = _exclusionZoneBuildKind == ExclusionZoneCacheKind.SurfaceAndSignal
                    ? RoadPlacementRules.TryGetSurfaceJunctionExclusionZone(nodeId, out zone)
                    : RoadPlacementRules.TryGetThreePlusJunctionExclusionZone(nodeId, out zone);

                if (hasZone)
                    _exclusionZoneBuildBuffer.Add(new CachedJunctionExclusionZone(nodeId, zone));
            }

            _exclusionZoneBuildNextNodeId = endNodeId;
            if (_exclusionZoneBuildNextNodeId < netManager.m_nodes.m_size)
                return;

            _exclusionZoneCache.Clear();
            _exclusionZoneCache.AddRange(_exclusionZoneBuildBuffer);
            _exclusionZoneBuildBuffer.Clear();
            _exclusionZoneCacheKind = _exclusionZoneBuildKind;
            _exclusionZoneCachedNodeCount = netManager.m_nodeCount;
            _exclusionZoneCachedSegmentCount = netManager.m_segmentCount;
            _exclusionZoneCachedRegistryRevision = CrossingPlacementRegistry.Revision;
            _exclusionZoneCacheReady = true;
            _exclusionZoneCacheBuildInProgress = false;
            InvalidateExclusionZoneCache();
        }

        private void CollectPctSignalExclusionNodes()
        {
            _pctSignalExclusionNodes.Clear();
            int count = CrossingPlacementRegistry.CopyTo(ExclusionZoneSignalAssetBuffer);
            for (int i = 0; i < count; i++)
            {
                CrossingPlacementAsset asset = ExclusionZoneSignalAssetBuffer[i];
                if (asset.Id == 0 || asset.Placement.Mode != PedestrianToolMode.SignalCrossing)
                    continue;

                ushort nodeId = asset.Plan.TargetNodeId != 0 ? asset.Plan.TargetNodeId : asset.Placement.TargetNodeId;
                if (nodeId == 0 || _pctSignalExclusionNodes.Contains(nodeId))
                    continue;

                _pctSignalExclusionNodes.Add(nodeId);
            }

            _pctSignalExclusionNodesRevision = CrossingPlacementRegistry.Revision;
        }

        private void EnsurePctSignalExclusionNodes()
        {
            if (_pctSignalExclusionNodesRevision == CrossingPlacementRegistry.Revision)
                return;

            CollectPctSignalExclusionNodes();
        }

        private bool IsPctSignalExclusionNode(ushort nodeId)
        {
            for (int i = 0; i < _pctSignalExclusionNodes.Count; i++)
                if (_pctSignalExclusionNodes[i] == nodeId)
                    return true;

            return false;
        }

        private void ClearExclusionZoneCache()
        {
            _exclusionZoneCache.Clear();
            _exclusionZoneBuildBuffer.Clear();
            _exclusionZoneVisiblePolygons.Clear();
            _pctSignalExclusionNodes.Clear();
            _exclusionZoneCacheKind = ExclusionZoneCacheKind.None;
            _exclusionZoneBuildKind = ExclusionZoneCacheKind.None;
            _exclusionZoneCacheReady = false;
            _exclusionZoneCacheBuildInProgress = false;
            _exclusionZoneBuildNextNodeId = 0;
            _exclusionZoneCachedNodeCount = 0;
            _exclusionZoneCachedSegmentCount = 0;
            _exclusionZoneCachedRegistryRevision = 0;
            InvalidateExclusionZoneCache();
        }

        private ExclusionZoneCacheKind GetExclusionZoneCacheKind(PedestrianToolMode mode)
        {
            if (mode == PedestrianToolMode.MidBlockCrossing || mode == PedestrianToolMode.SignalCrossing)
                return ExclusionZoneCacheKind.SurfaceAndSignal;

            return IsGradeSeparatedMode(mode)
                ? ExclusionZoneCacheKind.GradeSeparated
                : ExclusionZoneCacheKind.None;
        }

        private bool DrawCachedJunctionExclusionZones(Camera camera, Vector2 revealCenter)
        {
            _exclusionZoneVisiblePolygons.Clear();
            for (int i = 0; i < _exclusionZoneCache.Count; i++)
            {
                CachedJunctionExclusionZone cached = _exclusionZoneCache[i];
                AddRevealVisibleJunctionExclusionZonePolygons(camera, revealCenter, SignalGuideRadiusPixels, cached.Zone, _exclusionZoneVisiblePolygons);
            }

            return DrawJunctionExclusionPolygonsUnion(camera, _exclusionZoneVisiblePolygons, revealCenter, SignalGuideRadiusPixels);
        }

        private bool ShouldRefreshExclusionZoneWorldVisual(Camera camera, Vector2 revealCenter, PedestrianToolMode mode, float now)
        {
            if (camera == null)
                return true;

            if (mode != _exclusionZoneCachedMode)
                return true;

            if (now - _exclusionZoneRefreshTime >= ExclusionZoneRefreshSeconds)
                return true;

            if ((revealCenter - _exclusionZoneCachedRevealCenter).sqrMagnitude >= ExclusionZoneRevealRefreshPixels * ExclusionZoneRevealRefreshPixels)
                return true;

            if ((camera.transform.position - _exclusionZoneCachedCameraPosition).sqrMagnitude >= ExclusionZoneCameraRefreshDistance * ExclusionZoneCameraRefreshDistance)
                return true;

            return Quaternion.Angle(camera.transform.rotation, _exclusionZoneCachedCameraRotation) >= ExclusionZoneCameraRefreshAngle;
        }

        private void StoreExclusionZoneCache(Camera camera, Vector2 revealCenter, PedestrianToolMode mode, float now)
        {
            _exclusionZoneRefreshTime = now;
            _exclusionZoneCachedMode = mode;
            _exclusionZoneCachedRevealCenter = revealCenter;
            _exclusionZoneCachedCameraPosition = camera == null ? Vector3.zero : camera.transform.position;
            _exclusionZoneCachedCameraRotation = camera == null ? Quaternion.identity : camera.transform.rotation;
        }

        private void InvalidateExclusionZoneCache()
        {
            _exclusionZoneRefreshTime = -100f;
            _exclusionZoneCachedMode = PedestrianToolMode.None;
            _exclusionZoneCachedRevealCenter = Vector2.zero;
            _exclusionZoneCachedCameraPosition = Vector3.zero;
            _exclusionZoneCachedCameraRotation = Quaternion.identity;
        }

        private bool DrawGradeSeparatedJunctionExclusionZones(Camera camera, Vector2 revealCenter)
        {
            NetManager netManager = NetManager.instance;
            if (camera == null || netManager == null)
                return false;

            List<Vector3[]> polygons = new List<Vector3[]>();
            for (ushort nodeId = 1; nodeId < netManager.m_nodes.m_size; nodeId++)
            {
                RoadPlacementRules.JunctionExclusionZone zone;
                if (!RoadPlacementRules.TryGetThreePlusJunctionExclusionZone(nodeId, out zone))
                    continue;

                AddRevealVisibleJunctionExclusionZonePolygons(camera, revealCenter, SignalGuideRadiusPixels, zone, polygons);
            }

            return DrawJunctionExclusionPolygonsUnion(camera, polygons, revealCenter, SignalGuideRadiusPixels);
        }

        private bool DrawSurfaceJunctionExclusionZones(Camera camera, Vector2 revealCenter)
        {
            NetManager netManager = NetManager.instance;
            if (camera == null || netManager == null)
                return false;

            List<Vector3[]> polygons = new List<Vector3[]>();
            for (ushort nodeId = 1; nodeId < netManager.m_nodes.m_size; nodeId++)
            {
                RoadPlacementRules.JunctionExclusionZone zone;
                if (!RoadPlacementRules.TryGetSurfaceJunctionExclusionZone(nodeId, out zone))
                    continue;

                AddRevealVisibleJunctionExclusionZonePolygons(camera, revealCenter, SignalGuideRadiusPixels, zone, polygons);
            }

            return DrawJunctionExclusionPolygonsUnion(camera, polygons, revealCenter, SignalGuideRadiusPixels);
        }

        private void AddRevealVisibleJunctionExclusionZonePolygons(
            Camera camera,
            Vector2 revealCenter,
            float revealRadius,
            RoadPlacementRules.JunctionExclusionZone zone,
            List<Vector3[]> polygons)
        {
            if (camera == null || polygons == null)
                return;

            if (zone.SurfacePolygonCount > 0)
            {
                for (int i = 0; i < zone.SurfacePolygonCount; i++)
                    AddRevealVisibleExclusionPolygon(camera, revealCenter, revealRadius, zone.SurfacePolygons[i], polygons);

                return;
            }

            if (zone.PolygonPointCount > 0)
                AddRevealVisibleExclusionPolygon(camera, revealCenter, revealRadius, zone.PolygonPoints, polygons);
        }

        private void AddRevealVisibleExclusionPolygon(
            Camera camera,
            Vector2 revealCenter,
            float revealRadius,
            Vector3[] polygon,
            List<Vector3[]> polygons)
        {
            if (polygons == null || !DoesWorldPolygonTouchRevealCircle(camera, polygon, revealCenter, revealRadius))
                return;

            polygons.Add(polygon);
        }

        private bool DrawJunctionExclusionPolygonsUnion(Camera camera, List<Vector3[]> polygons, Vector2 revealCenter, float revealRadius)
        {
            if (polygons == null || polygons.Count <= 0)
                return false;

            List<Vector3[]> worldPolygons = new List<Vector3[]>(polygons.Count);
            Rect bounds = default(Rect);
            bool hasBounds = false;
            Vector3 lift = Vector3.up * JunctionExclusionOverlayHeight;

            for (int i = 0; i < polygons.Count; i++)
            {
                Vector3[] polygon = polygons[i];
                int pointCount = polygon == null ? 0 : polygon.Length;
                if (pointCount < 3)
                    continue;

                Rect polygonBounds;
                if (!TryGetWorldPolygonScreenBounds(camera, polygon, out polygonBounds))
                    continue;

                worldPolygons.Add(polygon);
                bounds = hasBounds ? UnionScreenBounds(bounds, polygonBounds) : polygonBounds;
                hasBounds = true;
            }

            if (!hasBounds || worldPolygons.Count <= 0 || !IsScreenBoundsVisible(bounds))
                return false;

            RebuildExclusionZoneWorldVisual(worldPolygons, lift);
            return true;
        }

        private bool DoesWorldPolygonTouchRevealCircle(Camera camera, Vector3[] polygon, Vector2 revealCenter, float revealRadius)
        {
            Rect ignored;
            return TryGetWorldPolygonRevealBounds(camera, polygon, revealCenter, revealRadius, out ignored);
        }

        private bool TryGetWorldPolygonRevealBounds(Camera camera, Vector3[] polygon, Vector2 revealCenter, float revealRadius, out Rect bounds)
        {
            bounds = default(Rect);
            int pointCount = polygon == null ? 0 : polygon.Length;
            if (camera == null || pointCount < 3)
                return false;

            Vector3 lift = Vector3.up * JunctionExclusionOverlayHeight;
            Vector2[] screenPoints = new Vector2[pointCount];
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                if (!WorldToGuiPoint(camera, polygon[pointIndex] + lift, out screenPoints[pointIndex]))
                    return false;
            }

            bounds = GetScreenBounds(screenPoints, pointCount);
            return IsScreenBoundsVisible(bounds) && DoesRevealCircleTouchPolygon(screenPoints, pointCount, revealCenter, revealRadius);
        }

        private bool TryGetWorldPolygonScreenBounds(Camera camera, Vector3[] polygon, out Rect bounds)
        {
            bounds = default(Rect);
            int pointCount = polygon == null ? 0 : polygon.Length;
            if (camera == null || pointCount < 3)
                return false;

            Vector3 lift = Vector3.up * JunctionExclusionOverlayHeight;
            Vector2[] screenPoints = new Vector2[pointCount];
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                if (!WorldToGuiPoint(camera, polygon[pointIndex] + lift, out screenPoints[pointIndex]))
                    return false;
            }

            bounds = GetScreenBounds(screenPoints, pointCount);
            return IsScreenBoundsVisible(bounds);
        }

        private void RebuildExclusionZoneWorldVisual(List<Vector3[]> polygons, Vector3 lift)
        {
            if (polygons == null || polygons.Count == 0)
            {
                ClearExclusionZoneWorldVisual();
                return;
            }

            EnsureExclusionZoneWorldVisual();
            if (_exclusionZoneWorldMesh == null)
                return;

            Vector3[] vertices;
            Color[] colors;
            int[] triangles;
            if (!TryBuildExclusionZoneUnionMesh(polygons, lift, out vertices, out colors, out triangles))
            {
                ClearExclusionZoneWorldVisual();
                return;
            }

            _exclusionZoneWorldMesh.Clear();
            _exclusionZoneWorldMesh.vertices = vertices;
            _exclusionZoneWorldMesh.colors = colors;
            _exclusionZoneWorldMesh.triangles = triangles;
            _exclusionZoneWorldMesh.RecalculateBounds();
        }

        private bool TryBuildExclusionZoneUnionMesh(List<Vector3[]> polygons, Vector3 lift, out Vector3[] vertices, out Color[] colors, out int[] triangles)
        {
            vertices = null;
            colors = null;
            triangles = null;

            float minZ = 0f;
            float maxZ = 0f;
            bool hasBounds = false;

            for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
            {
                Vector3[] polygon = polygons[polygonIndex];
                int pointCount = polygon == null ? 0 : polygon.Length;
                if (pointCount < 3)
                    continue;

                for (int i = 0; i < pointCount; i++)
                {
                    Vector3 point = polygon[i];
                    if (!hasBounds)
                    {
                        minZ = maxZ = point.z;
                        hasBounds = true;
                    }
                    else
                    {
                        minZ = Mathf.Min(minZ, point.z);
                        maxZ = Mathf.Max(maxZ, point.z);
                    }
                }
            }

            if (!hasBounds)
                return false;

            List<Vector3> vertexList = new List<Vector3>();
            List<Color> colorList = new List<Color>();
            List<int> triangleList = new List<int>();
            List<ExclusionIntersection> intersections = new List<ExclusionIntersection>(16);
            List<ExclusionInterval> intervals = new List<ExclusionInterval>(polygons.Count);

            float startZ = Mathf.Floor(minZ / JunctionExclusionWorldUnionStripDepth) * JunctionExclusionWorldUnionStripDepth;
            for (float z0 = startZ; z0 < maxZ; z0 += JunctionExclusionWorldUnionStripDepth)
            {
                float z1 = Mathf.Min(z0 + JunctionExclusionWorldUnionStripDepth, maxZ);
                float zMid = (z0 + z1) * 0.5f;
                intervals.Clear();

                for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
                {
                    Vector3[] polygon = polygons[polygonIndex];
                    int pointCount = polygon == null ? 0 : polygon.Length;
                    if (pointCount < 3)
                        continue;

                    intersections.Clear();
                    GetPolygonIntersectionsAtZ(polygon, pointCount, zMid, intersections);
                    SortIntersections(intersections);
                    for (int i = 0; i + 1 < intersections.Count; i += 2)
                        intervals.Add(new ExclusionInterval(
                            intersections[i].X,
                            intersections[i + 1].X,
                            intersections[i].Y,
                            intersections[i + 1].Y));
                }

                if (intervals.Count <= 0)
                    continue;

                SortIntervals(intervals);
                float mergedStart = intervals[0].Start;
                float mergedEnd = intervals[0].End;
                float mergedStartY = intervals[0].StartY;
                float mergedEndY = intervals[0].EndY;
                for (int i = 1; i < intervals.Count; i++)
                {
                    if (intervals[i].Start <= mergedEnd + 0.05f)
                    {
                        if (intervals[i].End > mergedEnd)
                        {
                            mergedEnd = intervals[i].End;
                            mergedEndY = intervals[i].EndY;
                        }

                        continue;
                    }

                    AddExclusionZoneUnionQuad(vertexList, colorList, triangleList, mergedStart, mergedEnd, z0, z1, mergedStartY, mergedEndY, lift.y);
                    mergedStart = intervals[i].Start;
                    mergedEnd = intervals[i].End;
                    mergedStartY = intervals[i].StartY;
                    mergedEndY = intervals[i].EndY;
                }

                AddExclusionZoneUnionQuad(vertexList, colorList, triangleList, mergedStart, mergedEnd, z0, z1, mergedStartY, mergedEndY, lift.y);
            }

            if (vertexList.Count <= 0 || triangleList.Count <= 0)
                return false;

            vertices = vertexList.ToArray();
            colors = colorList.ToArray();
            triangles = triangleList.ToArray();
            return true;
        }

        private static void GetPolygonIntersectionsAtZ(Vector3[] polygon, int pointCount, float z, List<ExclusionIntersection> intersections)
        {
            for (int i = 0; i < pointCount; i++)
            {
                Vector3 first = polygon[i];
                Vector3 second = polygon[(i + 1) % pointCount];
                if ((first.z <= z && second.z > z) || (second.z <= z && first.z > z))
                {
                    float t = (z - first.z) / (second.z - first.z);
                    intersections.Add(new ExclusionIntersection(
                        Mathf.Lerp(first.x, second.x, t),
                        Mathf.Lerp(first.y, second.y, t)));
                }
            }
        }

        private static void AddExclusionZoneUnionQuad(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            float x0,
            float x1,
            float z0,
            float z1,
            float y0,
            float y1,
            float liftY)
        {
            if (x1 <= x0 + 0.01f || z1 <= z0 + 0.01f)
                return;

            int offset = vertices.Count;
            vertices.Add(new Vector3(x0, y0 + liftY, z0));
            vertices.Add(new Vector3(x1, y1 + liftY, z0));
            vertices.Add(new Vector3(x1, y1 + liftY, z1));
            vertices.Add(new Vector3(x0, y0 + liftY, z1));
            colors.Add(ExclusionOverlayColor);
            colors.Add(ExclusionOverlayColor);
            colors.Add(ExclusionOverlayColor);
            colors.Add(ExclusionOverlayColor);
            triangles.Add(offset);
            triangles.Add(offset + 1);
            triangles.Add(offset + 2);
            triangles.Add(offset);
            triangles.Add(offset + 2);
            triangles.Add(offset + 3);
        }

        private void EnsureExclusionZoneWorldVisual()
        {
            if (_exclusionZoneWorldVisual != null && _exclusionZoneWorldMesh != null)
                return;

            _exclusionZoneWorldVisual = new GameObject("PCT Exclusion Zone Preview");
            _exclusionZoneWorldMesh = new Mesh();
            _exclusionZoneWorldMesh.name = "PCT Exclusion Zone Preview Mesh";

            MeshFilter filter = _exclusionZoneWorldVisual.AddComponent<MeshFilter>();
            filter.mesh = _exclusionZoneWorldMesh;

            MeshRenderer renderer = _exclusionZoneWorldVisual.AddComponent<MeshRenderer>();
            renderer.material = GetExclusionZoneWorldMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }

        private void ClearExclusionZoneWorldVisual()
        {
            if (_exclusionZoneWorldVisual != null)
                UnityEngine.Object.Destroy(_exclusionZoneWorldVisual);

            _exclusionZoneWorldVisual = null;
            _exclusionZoneWorldMesh = null;
        }

        private static Material GetExclusionZoneWorldMaterial()
        {
            if (_exclusionZoneWorldMaterial != null)
                return _exclusionZoneWorldMaterial;

            Shader shader = Shader.Find("Hidden/Internal-Colored")
                            ?? Shader.Find("Transparent/Diffuse")
                            ?? Shader.Find("Diffuse");
            if (shader == null)
                return null;

            _exclusionZoneWorldMaterial = new Material(shader);
            _exclusionZoneWorldMaterial.hideFlags = HideFlags.HideAndDontSave;
            _exclusionZoneWorldMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _exclusionZoneWorldMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _exclusionZoneWorldMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _exclusionZoneWorldMaterial.SetInt("_ZWrite", 0);
            _exclusionZoneWorldMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            _exclusionZoneWorldMaterial.color = ExclusionOverlayColor;
            _exclusionZoneWorldMaterial.renderQueue = 3000;
            return _exclusionZoneWorldMaterial;
        }

        private Rect GetScreenBounds(Vector2[] points, int pointCount)
        {
            float minX = points[0].x;
            float maxX = points[0].x;
            float minY = points[0].y;
            float maxY = points[0].y;
            for (int i = 1; i < pointCount; i++)
            {
                minX = Mathf.Min(minX, points[i].x);
                maxX = Mathf.Max(maxX, points[i].x);
                minY = Mathf.Min(minY, points[i].y);
                maxY = Mathf.Max(maxY, points[i].y);
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private Rect UnionScreenBounds(Rect first, Rect second)
        {
            float minX = Mathf.Min(first.xMin, second.xMin);
            float minY = Mathf.Min(first.yMin, second.yMin);
            float maxX = Mathf.Max(first.xMax, second.xMax);
            float maxY = Mathf.Max(first.yMax, second.yMax);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private bool IsScreenBoundsVisible(Rect bounds)
        {
            return bounds.xMax >= 0f
                   && bounds.xMin <= Screen.width
                   && bounds.yMax >= 0f
                   && bounds.yMin <= Screen.height;
        }

        private bool DoesRevealCircleTouchPolygon(Vector2[] points, int pointCount, Vector2 revealCenter, float revealRadius)
        {
            if (points == null || pointCount < 3)
                return false;

            float radiusSqr = revealRadius * revealRadius;
            if (IsPointInsideScreenPolygon(revealCenter, points, pointCount))
                return true;

            for (int i = 0; i < pointCount; i++)
            {
                Vector2 first = points[i];
                Vector2 second = points[(i + 1) % pointCount];
                if ((first - revealCenter).sqrMagnitude <= radiusSqr)
                    return true;

                if (DistanceToScreenSegmentSqr(revealCenter, first, second) <= radiusSqr)
                    return true;
            }

            return false;
        }

        private bool IsPointInsideScreenPolygon(Vector2 point, Vector2[] polygon, int pointCount)
        {
            bool inside = false;
            int j = pointCount - 1;
            for (int i = 0; i < pointCount; i++)
            {
                Vector2 current = polygon[i];
                Vector2 previous = polygon[j];
                if ((current.y > point.y) != (previous.y > point.y))
                {
                    float x = (previous.x - current.x) * (point.y - current.y) / (previous.y - current.y) + current.x;
                    if (point.x < x)
                        inside = !inside;
                }

                j = i;
            }

            return inside;
        }

        private float DistanceToScreenSegmentSqr(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSqr = segment.sqrMagnitude;
            if (lengthSqr <= 0.001f)
                return (point - start).sqrMagnitude;

            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSqr);
            Vector2 closest = start + segment * t;
            return (point - closest).sqrMagnitude;
        }

        private int GetPolygonIntersectionsAtY(Vector2[] points, int pointCount, float y, float[] intersections)
        {
            int intersectionCount = 0;
            for (int i = 0; i < pointCount; i++)
            {
                Vector2 first = points[i];
                Vector2 second = points[(i + 1) % pointCount];
                if ((first.y <= y && second.y > y) || (second.y <= y && first.y > y))
                {
                    float t = (y - first.y) / (second.y - first.y);
                    if (intersectionCount < intersections.Length)
                        intersections[intersectionCount++] = Mathf.Lerp(first.x, second.x, t);
                }
            }

            return intersectionCount;
        }

        private void SortIntervals(float[] starts, float[] ends, int count)
        {
            for (int i = 1; i < count; i++)
            {
                float currentStart = starts[i];
                float currentEnd = ends[i];
                int j = i - 1;
                while (j >= 0 && starts[j] > currentStart)
                {
                    starts[j + 1] = starts[j];
                    ends[j + 1] = ends[j];
                    j--;
                }

                starts[j + 1] = currentStart;
                ends[j + 1] = currentEnd;
            }
        }

        private void SortIntervals(List<ExclusionInterval> intervals)
        {
            if (intervals == null)
                return;

            for (int i = 1; i < intervals.Count; i++)
            {
                ExclusionInterval current = intervals[i];
                int j = i - 1;
                while (j >= 0 && intervals[j].Start > current.Start)
                {
                    intervals[j + 1] = intervals[j];
                    j--;
                }

                intervals[j + 1] = current;
            }
        }

        private void SortIntersections(float[] values, int count)
        {
            for (int i = 1; i < count; i++)
            {
                float current = values[i];
                int j = i - 1;
                while (j >= 0 && values[j] > current)
                {
                    values[j + 1] = values[j];
                    j--;
                }

                values[j + 1] = current;
            }
        }

        private void SortIntersections(List<ExclusionIntersection> values)
        {
            if (values == null)
                return;

            for (int i = 1; i < values.Count; i++)
            {
                ExclusionIntersection current = values[i];
                int j = i - 1;
                while (j >= 0 && values[j].X > current.X)
                {
                    values[j + 1] = values[j];
                    j--;
                }

                values[j + 1] = current;
            }
        }

        private void DrawClippedExclusionLine(Vector2 start, Vector2 end, float width, Rect[] hideRects, bool hasPanel, Rect panelRect)
        {
            float length = Vector2.Distance(start, end);
            if (length <= 0.001f)
                return;

            int chunks = Mathf.Max(1, Mathf.CeilToInt(length / ExclusionOverlayClipChunkPixels));
            for (int i = 0; i < chunks; i++)
            {
                float fromT = i / (float)chunks;
                float toT = (i + 1) / (float)chunks;
                float midT = (fromT + toT) * 0.5f;
                Vector2 mid = Vector2.Lerp(start, end, midT);
                if (!IsInsideAny(mid, hideRects))
                {
                    GUI.color = hasPanel && panelRect.Contains(mid) ? ExclusionOverlayMutedColor : ExclusionOverlayColor;
                    DrawLine(Vector2.Lerp(start, end, fromT), Vector2.Lerp(start, end, toT), width);
                }
            }
        }

        private Rect[] GetExclusionOverlayHideRects()
        {
            Rect[] rects = new Rect[2];
            int count = 0;

            Rect uiRect;
            if (TryGetToolInfoScreenRect(out uiRect))
                rects[count++] = uiRect;

            rects[count++] = new Rect(0f, Mathf.Max(0f, Screen.height - 230f), Screen.width, 230f);

            if (count == rects.Length)
                return rects;

            Rect[] trimmed = new Rect[count];
            for (int i = 0; i < count; i++)
                trimmed[i] = rects[i];

            return trimmed;
        }

        private bool IsInsideAny(Vector2 point, Rect[] rects)
        {
            if (rects == null)
                return false;

            for (int i = 0; i < rects.Length; i++)
                if (rects[i].Contains(point))
                    return true;

            return false;
        }

        private void DrawPreviewPlacementLine(Camera camera, CrossingPlacementRecord preview, CrossingPlacementPlan plan, Color color)
        {
            if (!preview.IsValid || preview.Mode == PedestrianToolMode.RemoveCrossing)
                return;

            if (!plan.IsValid)
                return;

            Vector2 center;
            Vector2 left;
            Vector2 right;
            if (!WorldToGuiPoint(camera, plan.Center + Vector3.up * 0.2f, out center)
                || !WorldToGuiPoint(camera, plan.LeftEdge + Vector3.up * 0.2f, out left)
                || !WorldToGuiPoint(camera, plan.RightEdge + Vector3.up * 0.2f, out right))
                return;

            Vector2 direction = right - left;
            if (direction.sqrMagnitude <= 0.001f)
                direction = Vector2.right;
            else
                direction.Normalize();

            Vector2 half = direction * (HoverLineTotalPixels * 0.5f);
            Color oldColor = GUI.color;
            GUI.color = color;
            DrawGlowLine(center - half, center + half, 7f);
            DrawSolidRect(new Rect(center.x - 3f, center.y - 3f, 6f, 6f));
            GUI.color = oldColor;
        }

        private void DrawPointToPointSubwayPreview(Camera camera, CrossingPlacementRecord preview, Color color)
        {
            if (!SubwayPointToPointTool.HasStartEndpoint)
                return;

            CrossingPlacementRecord start = SubwayPointToPointTool.StartEndpoint;
            Vector2 first;
            Vector2 second;
            if (!WorldToGuiPoint(camera, start.WorldPosition + Vector3.up * 0.2f, out first)
                || !WorldToGuiPoint(camera, preview.WorldPosition + Vector3.up * 0.2f, out second))
            {
                return;
            }

            Color oldColor = GUI.color;
            GUI.color = color;
            DrawGlowLine(first, second, preview.IsValid ? 6f : 4f);
            DrawSolidRect(new Rect(first.x - 4f, first.y - 4f, 8f, 8f));
            DrawSolidRect(new Rect(second.x - 4f, second.y - 4f, 8f, 8f));
            GUI.color = oldColor;
        }

        private bool TryGetPreviewTouchedAsset(CrossingPlacementRecord preview, out CrossingPlacementAsset touchedAsset)
        {
            touchedAsset = CrossingPlacementAsset.None;
            if (!preview.IsValid)
                return false;

            CrossingPlacementPlan previewPlan = CrossingPlacementPlanner.Build(preview);
            if (!previewPlan.IsValid)
                return false;

            return CrossingPlacementRegistry.TryGetAssetAt(preview, previewPlan, out touchedAsset);
        }

        private Color GetHoverColor(CrossingPlacementRecord preview, CrossingPlacementAsset touchedAsset, bool touchesExisting)
        {
            if (IsHoldingPlacementBlockFeedback())
                return GetBlockedHoverColor();

            if (!preview.IsValid)
                return GetBlockedHoverColor();

            if (touchesExisting)
            {
                if (touchedAsset.Placement.Mode == preview.Mode
                    && preview.Mode == PedestrianToolMode.SignalCrossing)
                {
                    return GetBlockedHoverColor();
                }

                return GetReplacementHoverColor();
            }

            return GetStandardHoverColor();
        }

        private Color GetStandardHoverColor()
        {
            return new Color(0.48f, 0.88f, 1f, 0.88f);
        }

        private Color GetReplacementHoverColor()
        {
            return new Color(1f, 0.68f, 0.22f, 0.92f);
        }

        private Color GetBlockedHoverColor()
        {
            return new Color(1f, 0.05f, 0.02f, 1f);
        }

        private void DrawSnapCursor(Camera camera, CrossingPlacementRecord preview, Color color)
        {
            Vector2 center;
            Vector3 cursorWorld = IsHoldingPlacementBlockFeedback() ? _placementBlockPosition : preview.WorldPosition;
            if (!WorldToGuiPoint(camera, cursorWorld + Vector3.up * 0.2f, out center))
                return;

            GUI.color = color;

            float radius = IsHoldingPlacementBlockFeedback() || !preview.IsValid ? 20f : 12f;
            float cursorWidth = IsHoldingPlacementBlockFeedback() || !preview.IsValid ? 4f : 2f;
            const int sides = 16;
            Vector2 previous = center + new Vector2(radius, 0f);
            for (int i = 1; i <= sides; i++)
            {
                float angle = (Mathf.PI * 2f * i) / sides;
                Vector2 next = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                DrawGlowLine(previous, next, cursorWidth);
                previous = next;
            }

            DrawGlowLine(center + new Vector2(-radius - 7f, 0f), center + new Vector2(-radius + 2f, 0f), cursorWidth);
            DrawGlowLine(center + new Vector2(radius - 2f, 0f), center + new Vector2(radius + 7f, 0f), cursorWidth);
            DrawGlowLine(center + new Vector2(0f, -radius - 7f), center + new Vector2(0f, -radius + 2f), cursorWidth);
            DrawGlowLine(center + new Vector2(0f, radius - 2f), center + new Vector2(0f, radius + 7f), cursorWidth);

            float centerSize = IsHoldingPlacementBlockFeedback() || !preview.IsValid ? 10f : 6f;
            DrawSolidRect(new Rect(center.x - centerSize * 0.5f, center.y - centerSize * 0.5f, centerSize, centerSize));
        }

        private void DrawScreenCircle(Vector2 center, float radius, Color color, float width, int sides)
        {
            Color oldColor = GUI.color;
            GUI.color = color;

            Vector2 previous = center + new Vector2(radius, 0f);
            for (int i = 1; i <= sides; i++)
            {
                float angle = (Mathf.PI * 2f * i) / sides;
                Vector2 next = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                DrawGlowLine(previous, next, width);
                previous = next;
            }

            GUI.color = oldColor;
        }

        private bool WorldToGuiPoint(Camera camera, Vector3 world, out Vector2 guiPoint)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                guiPoint = Vector2.zero;
                return false;
            }

            guiPoint = new Vector2(screen.x, Screen.height - screen.y);
            return true;
        }

        private void DrawLine(Vector2 start, Vector2 end, float width)
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(start, end);
            GUIUtility.RotateAroundPivot(angle, start);
            DrawSolidRect(new Rect(start.x, start.y - (width * 0.5f), length, width));
            GUI.matrix = oldMatrix;
        }

        private void DrawGlowLine(Vector2 start, Vector2 end, float width)
        {
            Color color = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * 0.28f);
            DrawLine(start, end, width + 5f);
            GUI.color = color;
            DrawLine(start, end, width);
        }

        private void DrawSolidRect(Rect rect)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

    }
}
