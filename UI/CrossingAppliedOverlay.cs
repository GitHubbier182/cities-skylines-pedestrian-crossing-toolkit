using System.Collections.Generic;
using ColossalFramework.UI;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public class CrossingAppliedOverlay : MonoBehaviour
    {
        public static CrossingAppliedOverlay Instance;
        private static readonly CrossingConnectivityLink[] LinkRenderBuffer = new CrossingConnectivityLink[512];
        private static readonly CrossingPathWorkOrder[] PathWorkOrderRenderBuffer = new CrossingPathWorkOrder[1024];
        private static readonly CrossingLandingConnectorWorkOrder[] ConnectorRenderBuffer = new CrossingLandingConnectorWorkOrder[512];
        private static readonly CrossingLandingAccessAssetWorkOrder[] AccessAssetRenderBuffer = new CrossingLandingAccessAssetWorkOrder[512];
        private static readonly CrossingPathBuilder.SignalControllerDebugSnapshot[] SignalDebugRenderBuffer = new CrossingPathBuilder.SignalControllerDebugSnapshot[128];
        private static readonly CrossingPlacementAsset[] CrossingHighlightRenderBuffer = new CrossingPlacementAsset[2048];
        private static readonly CrossingPlacementAsset[] ValidationProblemRenderBuffer = new CrossingPlacementAsset[512];
        private static readonly List<string> ConnectorRenderKeys = new List<string>();
        private static readonly List<string> ConnectivityRenderKeys = new List<string>();
        private static readonly List<GameObject> RouteWorldVisuals = new List<GameObject>();
        private static readonly List<GameObject> CrossingHighlightWorldVisuals = new List<GameObject>();
        private static readonly Color SubwayRoutePreviewColor = new Color(0.48f, 0.88f, 1f, 0.76f);
        private static readonly Color SubwayRouteMutedColor = new Color(0.48f, 0.88f, 1f, 0.08f);
        private static readonly Color SubwayRouteWorldColor = new Color(0.48f, 0.88f, 1f, 0.52f);
        private static readonly Color HighlightShadowColor = new Color(0f, 0f, 0f, 0.34f);
        private static readonly Color HighlightShadowMutedColor = new Color(0f, 0f, 0f, 0.04f);
        private const float RouteClipChunkPixels = 2f;
        private const float RouteWorldVisualWidth = 0.22f;
        private const float RouteWorldVisualHeight = 0.045f;
        private const float RouteWorldVisualRebuildSeconds = 0.35f;
        private const float SubwayConnectorDisplayLength = 2f;
        private const float HighlightWorldLift = 0.35f;
        private const float HighlightWorldLineWidth = 0.75f;
        private const float HighlightWorldMarkerWidth = 0.45f;
        private const int HighlightWorldCircleSegments = 24;
        private const float HighlightLineWidth = 4.5f;
        private const float HighlightCircleRadius = 13f;
        private const float CrossingHighlightMinCameraSize = 300f;
        private const float SignalStatusMaxCameraSize = 250f;
        private const float SignalStatusBackgroundAlpha = 0.84f;
        private const float SignalStatusRefreshSeconds = 1f;
        private const float SignalStatusRoadEdgePadding = 8f;
        private const float SignalStatusUiPadding = 8f;
        private const float RoadToolWarningWidth = 310f;
        private const float RoadToolWarningMinHeight = 70f;
        private const float RoadToolWarningCursorOffsetX = 22f;
        private const float RoadToolWarningCursorOffsetY = 28f;
        private const int HighlightCircleSegments = 32;
        private const float HighlightRefreshSeconds = 0.35f;
        private static Material _subwayRouteWorldMaterial;
        private static Material _midBlockHighlightWorldMaterial;
        private static Material _signalHighlightWorldMaterial;
        private static Material _subwayLinkHighlightWorldMaterial;
        private static Material _subwayPointHighlightWorldMaterial;
        private static Material _bridgeHighlightWorldMaterial;
        private static Material _validationProblemWorldMaterial;
        private static Texture2D _midBlockCrossingLocationIconTexture;
        private static Texture2D _signalCrossingLocationIconTexture;
        private static Texture2D _subwayLinkCrossingLocationIconTexture;
        private static Texture2D _subwayPointCrossingLocationIconTexture;
        private static Texture2D _bridgeCrossingLocationIconTexture;
        private static Texture2D _validationProblemIconTexture;
        private const int CrossingLocationIconTextureSize = 96;
        private const float CrossingLocationIconMinWorldSize = 14f;
        private const float CrossingLocationIconMaxWorldSize = 38f;
        private const float CrossingLocationIconWorldScale = 0.043f;
        private const int ValidationProblemIconTextureSize = 96;
        private const float ValidationProblemIconMinWorldSize = 22f;
        private const float ValidationProblemIconMaxWorldSize = 64f;
        private const float ValidationProblemIconWorldScale = 0.075f;
        private float _routeWorldVisualTimer;
        private float _highlightWorldVisualTimer;
        private float _highlightRefreshTime = -100f;
        private static float _signalDebugRefreshTime = -100f;
        private PedestrianToolMode _highlightCachedMode = PedestrianToolMode.None;
        private int _highlightCachedCount;
        private int _validationProblemCachedRevision = -1;
        private static int _signalDebugCachedCount;

        public static void CreateIfNeeded(UIView view)
        {
            if (view == null || Instance != null)
                return;

            Instance = view.gameObject.AddComponent<CrossingAppliedOverlay>();
            Debug.Log("[PedestrianCrossingToolkit] Applied overlay attached.");
        }

        public static void DestroyInstance()
        {
            if (Instance == null)
                return;

            Instance.ClearRouteWorldVisuals();
            Instance.ClearCrossingHighlightWorldVisuals();
            UnityEngine.Object.Destroy(Instance);
            Instance = null;
        }

        private void Update()
        {
            if (!PedestrianCrossingToolkitState.Enabled || !PedestrianCrossingToolkitPanel.IsOpen)
            {
                ClearRouteWorldVisuals();
                ClearCrossingHighlightWorldVisuals();
                _routeWorldVisualTimer = 0f;
                _highlightWorldVisualTimer = 0f;
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                ClearRouteWorldVisuals();
                ClearCrossingHighlightWorldVisuals();
                _routeWorldVisualTimer = 0f;
                _highlightWorldVisualTimer = 0f;
                return;
            }

            if (ShouldShowRouteWorldVisuals())
            {
                _routeWorldVisualTimer += Time.unscaledDeltaTime;
                if (_routeWorldVisualTimer >= RouteWorldVisualRebuildSeconds)
                {
                    _routeWorldVisualTimer = 0f;
                    RebuildRouteWorldVisuals();
                }
            }
            else
            {
                ClearRouteWorldVisuals();
                _routeWorldVisualTimer = 0f;
            }

            bool showCrossingHighlights = ShouldShowCrossingHighlights(camera, PedestrianCrossingToolkitState.ActiveMode);
            bool showValidationProblems = ShouldShowValidationProblemHighlights(camera);
            int validationProblemRevision = PedestrianCrossingToolkitState.ValidationProblemRevision;
            if (showCrossingHighlights || showValidationProblems)
            {
                _highlightWorldVisualTimer += Time.unscaledDeltaTime;
                if (_highlightWorldVisualTimer >= HighlightRefreshSeconds
                    || _validationProblemCachedRevision != validationProblemRevision)
                {
                    _highlightWorldVisualTimer = 0f;
                    RebuildCrossingHighlightWorldVisuals(camera, showCrossingHighlights, showValidationProblems);
                }
            }
            else
            {
                ClearCrossingHighlightWorldVisuals();
                _highlightWorldVisualTimer = 0f;
                _validationProblemCachedRevision = validationProblemRevision;
            }
        }

        private static bool ShouldShowRouteWorldVisuals()
        {
            switch (PedestrianCrossingToolkitState.ActiveMode)
            {
                case PedestrianToolMode.SubwayLink:
                case PedestrianToolMode.SubwayPointToPoint:
                case PedestrianToolMode.PedestrianBridge:
                    return true;
                default:
                    return false;
            }
        }

        private void OnGUI()
        {
            if (!PedestrianCrossingToolkitState.Enabled)
                return;

            Event e = Event.current;
            if (e == null || e.type != EventType.Repaint)
                return;

            Camera camera = Camera.main;
            if (camera == null)
                return;

            Color oldColor = GUI.color;
            DrawRoadToolCrossingWarning(camera);

            if (!PedestrianCrossingToolkitPanel.IsOpen)
            {
                GUI.color = oldColor;
                return;
            }

            if (PedestrianCrossingLog.VerboseDiagnostics)
                DrawSignalControllerDebug(camera);
            GUI.color = oldColor;
            return;

#pragma warning disable 162
            DrawConnectivityLinks(camera);
            DrawUndergroundSubwayPathWorkOrders(camera);
            DrawLandingConnectors(camera);
            DrawLandingAccessAssets(camera);
            GUI.color = oldColor;
#pragma warning restore 162
        }

        private void DrawCrossingHighlightsCached(Camera camera)
        {
            PedestrianToolMode activeMode = PedestrianCrossingToolkitState.ActiveMode;
            if (!ShouldShowCrossingHighlights(camera, activeMode))
                return;

            float now = Time.unscaledTime;
            if (activeMode != _highlightCachedMode || now - _highlightRefreshTime >= HighlightRefreshSeconds)
                RefreshCrossingHighlightCache(camera, activeMode, now);

            if (_highlightCachedCount <= 0)
                return;

            Rect[] hideRects = GetRouteHideRects();
            Rect panelRect;
            bool hasPanel = PedestrianCrossingToolkitPanel.TryGetPanelScreenRect(out panelRect);

            for (int i = 0; i < _highlightCachedCount; i++)
                DrawCrossingHighlight(camera, CrossingHighlightRenderBuffer[i], hideRects, hasPanel, panelRect);
        }

        private void RefreshCrossingHighlightCache(Camera camera, PedestrianToolMode activeMode, float now)
        {
            _highlightCachedMode = activeMode;
            _highlightRefreshTime = now;
            _highlightCachedCount = 0;

            int count = CrossingPlacementRegistry.CopyTo(CrossingHighlightRenderBuffer);
            for (int i = 0; i < count; i++)
            {
                CrossingPlacementAsset asset = CrossingHighlightRenderBuffer[i];
                if (!ShouldHighlightAsset(asset, activeMode) || !IsHighlightInCameraView(camera, asset))
                    continue;

                CrossingHighlightRenderBuffer[_highlightCachedCount++] = asset;
            }
        }

        private static void DrawRoadToolCrossingWarning(Camera camera)
        {
            ushort segmentId;
            if (!TryGetHoveredRoadToolSegment(camera, out segmentId))
                return;

            int crossingCount = CrossingPlacementRegistry.CountAssetsTouchingSegment(segmentId);
            if (crossingCount <= 0)
                return;

            string plural = crossingCount == 1 ? string.Empty : "s";
            string text = "PCT crossing on this road\nUpgrading or replacing this segment will remove "
                          + crossingCount
                          + " PCT crossing"
                          + plural
                          + ".";

            Vector2 size = CalculateRoadToolWarningSize(text);
            Rect rect = GetRoadToolWarningRect(size);
            DrawRoadToolWarningBox(rect, text);
        }

        private static bool TryGetHoveredRoadToolSegment(Camera camera, out ushort segmentId)
        {
            segmentId = 0;
            ToolController controller = ToolsModifierControl.toolController;
            if (camera == null
                || controller == null
                || !(controller.CurrentTool is NetTool)
                || PedestrianCrossingToolkitState.ActiveMode != PedestrianToolMode.None
                || PedestrianCrossingToolkitPanel.IsMouseOverAnyBlockingUi())
            {
                return false;
            }

            ushort hoveredSegmentId;
            if (!PedestrianCrossingInteractionTool.TryRaycastRoadSegmentForOverlay(camera, out hoveredSegmentId))
                return false;

            if (CrossingPathBuilder.IsBuiltCrossingSegment(hoveredSegmentId))
                return false;

            NetManager netManager = NetManager.instance;
            if (netManager == null || hoveredSegmentId >= netManager.m_segments.m_size)
                return false;

            ref NetSegment segment = ref netManager.m_segments.m_buffer[hoveredSegmentId];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0
                || segment.Info == null
                || !(segment.Info.m_netAI is RoadBaseAI))
            {
                return false;
            }

            segmentId = hoveredSegmentId;
            return true;
        }

        private static Vector2 CalculateRoadToolWarningSize(string text)
        {
            string[] lines = string.IsNullOrEmpty(text) ? new string[0] : text.Split('\n');
            float height = Mathf.Max(RoadToolWarningMinHeight, lines.Length * 18f + 22f);
            return new Vector2(RoadToolWarningWidth, height);
        }

        private static Rect GetRoadToolWarningRect(Vector2 size)
        {
            Vector3 mouse = Input.mousePosition;
            float x = mouse.x + RoadToolWarningCursorOffsetX;
            float y = Screen.height - mouse.y + RoadToolWarningCursorOffsetY;
            if (x + size.x > Screen.width - 8f)
                x = Mathf.Max(8f, mouse.x - size.x - RoadToolWarningCursorOffsetX);
            if (y + size.y > Screen.height - 8f)
                y = Mathf.Max(8f, y - size.y - RoadToolWarningCursorOffsetY * 2f);

            return new Rect(x, y, size.x, size.y);
        }

        private static void DrawRoadToolWarningBox(Rect rect, string text)
        {
            GUI.color = new Color(0.07f, 0.07f, 0.07f, 0.88f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.72f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 5f, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 16f, rect.y + 10f, rect.width - 24f, rect.height - 16f), text);
        }

        private static bool ShouldHighlightAsset(CrossingPlacementAsset asset, PedestrianToolMode activeMode)
        {
            if (asset.Id == 0 || !asset.Plan.IsValid)
                return false;

            if (activeMode == PedestrianToolMode.None || activeMode == PedestrianToolMode.RemoveCrossing)
                return true;

            return asset.Placement.Mode == activeMode;
        }

        private static bool ShouldShowCrossingHighlights(Camera camera, PedestrianToolMode activeMode)
        {
            return GetOverlayCameraSize(camera) >= CrossingHighlightMinCameraSize;
        }

        private static bool ShouldShowValidationProblemHighlights(Camera camera)
        {
            return camera != null && PedestrianCrossingToolkitState.HasValidationProblemAssets;
        }

        private static void DrawCrossingHighlight(Camera camera, CrossingPlacementAsset asset, Rect[] hideRects, bool hasPanel, Rect panelRect)
        {
            Vector3 leftWorld = asset.Plan.LeftEdge + Vector3.up * HighlightWorldLift;
            Vector3 rightWorld = asset.Plan.RightEdge + Vector3.up * HighlightWorldLift;
            Vector3 centerWorld = asset.Plan.Center + Vector3.up * HighlightWorldLift;
            Vector2 left;
            Vector2 right;
            Vector2 center;
            bool hasLeft = WorldToGuiPoint(camera, leftWorld, out left);
            bool hasRight = WorldToGuiPoint(camera, rightWorld, out right);
            bool hasCenter = WorldToGuiPoint(camera, centerWorld, out center);

            Color color = GetHighlightColor(asset.Placement.Mode);
            Color mutedColor = GetHighlightMutedColor(color);
            float lineWidth = HighlightLineWidth;

            if (hasLeft && hasRight && Vector2.Distance(left, right) > 1f)
            {
                DrawClippedLine(left, right, lineWidth + 3f, HighlightShadowColor, HighlightShadowMutedColor, hideRects, hasPanel, panelRect);
                DrawClippedLine(left, right, lineWidth, color, mutedColor, hideRects, hasPanel, panelRect);
            }

            if (!hasCenter)
                return;

            float radius = HighlightCircleRadius;
            DrawClippedCircle(center, radius + 3f, 3f, HighlightShadowColor, HighlightShadowMutedColor, hideRects, hasPanel, panelRect);
            DrawClippedCircle(center, radius, 2.4f, color, mutedColor, hideRects, hasPanel, panelRect);
            DrawClippedLine(new Vector2(center.x - radius * 0.55f, center.y), new Vector2(center.x + radius * 0.55f, center.y), 2.2f, color, mutedColor, hideRects, hasPanel, panelRect);
            DrawClippedLine(new Vector2(center.x, center.y - radius * 0.55f), new Vector2(center.x, center.y + radius * 0.55f), 2.2f, color, mutedColor, hideRects, hasPanel, panelRect);
        }

        private static bool IsHighlightInCameraView(Camera camera, CrossingPlacementAsset asset)
        {
            return IsWorldPointInCameraView(camera, asset.Plan.Center)
                   || IsWorldPointInCameraView(camera, asset.Plan.LeftEdge)
                   || IsWorldPointInCameraView(camera, asset.Plan.RightEdge)
                   || (asset.Placement.HasSecondaryPoint && IsWorldPointInCameraView(camera, asset.Placement.SecondaryWorldPosition));
        }

        private static Color GetHighlightColor(PedestrianToolMode mode)
        {
            switch (mode)
            {
                case PedestrianToolMode.SignalCrossing:
                    return new Color(1f, 0.18f, 0.12f, 0.94f);
                case PedestrianToolMode.SubwayLink:
                    return new Color(0.05f, 0.82f, 1f, 0.92f);
                case PedestrianToolMode.SubwayPointToPoint:
                    return new Color(0.62f, 0.32f, 1f, 0.92f);
                case PedestrianToolMode.PedestrianBridge:
                    return new Color(0.32f, 1f, 0.24f, 0.92f);
                case PedestrianToolMode.MidBlockCrossing:
                    return new Color(1f, 1f, 1f, 0.94f);
                default:
                    return new Color(1f, 1f, 1f, 0.9f);
            }
        }

        private static Color GetHighlightMutedColor(Color color)
        {
            return new Color(color.r, color.g, color.b, Mathf.Min(color.a, 0.1f));
        }

        private static void DrawSignalControllerDebug(Camera camera)
        {
            if (camera == null || GetOverlayCameraSize(camera) >= SignalStatusMaxCameraSize)
                return;

            float now = Time.unscaledTime;
            if (now - _signalDebugRefreshTime >= SignalStatusRefreshSeconds)
            {
                _signalDebugRefreshTime = now;
                _signalDebugCachedCount = CrossingPathBuilder.CopySignalControllerDebugSnapshotsTo(SignalDebugRenderBuffer);
            }

            int count = _signalDebugCachedCount;
            for (int i = 0; i < count; i++)
            {
                CrossingPathBuilder.SignalControllerDebugSnapshot snapshot = SignalDebugRenderBuffer[i];
                if (!snapshot.IsValid)
                    continue;

                if (!IsWorldPointInCameraView(camera, snapshot.Center))
                    continue;

                Vector2 markerPoint;
                if (!WorldToGuiPoint(camera, snapshot.Center + Vector3.up * HighlightWorldLift, out markerPoint))
                    continue;

                Vector2 firstPoint;
                Vector2 secondPoint;
                if (!WorldToGuiPoint(camera, snapshot.FirstPosition + Vector3.up * HighlightWorldLift, out firstPoint)
                    || !WorldToGuiPoint(camera, snapshot.SecondPosition + Vector3.up * HighlightWorldLift, out secondPoint))
                {
                    firstPoint = markerPoint;
                    secondPoint = markerPoint;
                }

                DrawSignalDebugBadge(markerPoint, firstPoint, secondPoint, snapshot);
            }
        }

        private static void DrawSignalDebugBadge(Vector2 markerPoint, Vector2 firstPoint, Vector2 secondPoint, CrossingPathBuilder.SignalControllerDebugSnapshot snapshot)
        {
            string text = BuildSignalStatusText(snapshot);

            Vector2 size = CalculateLabelSize(text);
            float width = size.x + 16f;
            float height = size.y + 10f;
            Rect[] avoidRects = GetSignalStatusAvoidRects();
            Rect rect;
            if (!TryGetSignalStatusRect(markerPoint, firstPoint, secondPoint, width, height, avoidRects, out rect))
                return;

            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, SignalStatusBackgroundAlpha);
            DrawSolidRect(rect);
            GUI.color = GetSignalStatusColor(snapshot);
            DrawSolidRect(new Rect(rect.x, rect.y, 5f, rect.height));
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 5f, rect.width - 12f, rect.height - 8f), text);
            GUI.color = oldColor;
        }

        private static bool TryGetSignalStatusRect(Vector2 markerPoint, Vector2 firstPoint, Vector2 secondPoint, float width, float height, Rect[] avoidRects, out Rect rect)
        {
            Vector2 direction = GetSignalStatusLowerDirection(markerPoint, firstPoint, secondPoint);
            float halfRoadWidth = Mathf.Max(Vector2.Distance(markerPoint, firstPoint), Vector2.Distance(markerPoint, secondPoint));
            float halfBadgeDepth = (Mathf.Abs(direction.x) * width + Mathf.Abs(direction.y) * height) * 0.5f;
            float distanceFromCenter = halfRoadWidth + halfBadgeDepth + SignalStatusRoadEdgePadding;

            Rect lowerRect = MakeCenteredRect(markerPoint + direction * distanceFromCenter, width, height);
            if (IsSignalStatusRectUsable(lowerRect, avoidRects))
            {
                rect = lowerRect;
                return true;
            }

            Rect upperRect = MakeCenteredRect(markerPoint - direction * distanceFromCenter, width, height);
            if (IsSignalStatusRectUsable(upperRect, avoidRects))
            {
                rect = upperRect;
                return true;
            }

            rect = default(Rect);
            return false;
        }

        private static Vector2 GetSignalStatusLowerDirection(Vector2 markerPoint, Vector2 firstPoint, Vector2 secondPoint)
        {
            Vector2 firstOffset = firstPoint - markerPoint;
            Vector2 secondOffset = secondPoint - markerPoint;
            Vector2 lowerOffset = firstOffset.y >= secondOffset.y ? firstOffset : secondOffset;
            if (Mathf.Abs(firstOffset.y - secondOffset.y) <= 1f || lowerOffset.sqrMagnitude <= 1f)
                return new Vector2(0f, 1f);

            return lowerOffset.normalized;
        }

        private static Rect MakeCenteredRect(Vector2 center, float width, float height)
        {
            return new Rect(center.x - (width * 0.5f), center.y - (height * 0.5f), width, height);
        }

        private static bool IsSignalStatusRectUsable(Rect rect, Rect[] avoidRects)
        {
            if (rect.xMin < SignalStatusUiPadding
                || rect.yMin < SignalStatusUiPadding
                || rect.xMax > Screen.width - SignalStatusUiPadding
                || rect.yMax > Screen.height - SignalStatusUiPadding)
            {
                return false;
            }

            Rect paddedRect = ExpandRect(rect, SignalStatusUiPadding);
            for (int i = 0; i < avoidRects.Length; i++)
            {
                if (paddedRect.Overlaps(avoidRects[i]))
                    return false;
            }

            return true;
        }

        private static Rect[] GetSignalStatusAvoidRects()
        {
            List<Rect> rects = new List<Rect>();
            Rect panelRect;
            if (PedestrianCrossingToolkitPanel.TryGetPanelScreenRect(out panelRect))
                rects.Add(ExpandRect(panelRect, SignalStatusUiPadding));

            Rect toolTipRect;
            if (PedestrianCrossingInteractionTool.TryGetToolInfoScreenRect(out toolTipRect))
                rects.Add(ExpandRect(toolTipRect, SignalStatusUiPadding));

            return rects.ToArray();
        }

        private static Rect ExpandRect(Rect rect, float padding)
        {
            return new Rect(rect.x - padding, rect.y - padding, rect.width + padding * 2f, rect.height + padding * 2f);
        }

        private static float GetOverlayCameraSize(Camera camera)
        {
            CameraController controller = ToolsModifierControl.cameraController;
            if (controller != null && controller.m_currentSize > 0f)
                return controller.m_currentSize;

            return camera != null ? camera.transform.position.y : 0f;
        }

        private static Vector2 CalculateLabelSize(string text)
        {
            string[] lines = text.Split('\n');
            float width = 0f;
            float height = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                Vector2 lineSize = GUI.skin.label.CalcSize(new GUIContent(lines[i]));
                width = Mathf.Max(width, lineSize.x);
                height += lineSize.y;
            }

            return new Vector2(width, height);
        }

        private static string BuildSignalStatusText(CrossingPathBuilder.SignalControllerDebugSnapshot snapshot)
        {
            string text = "Signal crossing"
                          + "\nTraffic: " + FormatSignalUserAction(snapshot.VehicleState, "Go", "Stop")
                          + "  Pedestrians: " + FormatSignalUserAction(snapshot.PedestrianState, "Cross", "Wait")
                          + "\n" + FormatSignalPhase(snapshot);

            if (snapshot.HasPedestriansOnCrossing)
                text += " - people crossing";
            else if (snapshot.HasPedestriansWaitingAtEntrance)
                text += " - waiting for pedestrians";

            return text;
        }

        private static string FormatSignalPhase(CrossingPathBuilder.SignalControllerDebugSnapshot snapshot)
        {
            if (snapshot.Phase == "Idle")
                return "Traffic green";
            if (snapshot.Phase == "Crossing")
                return "Pedestrians crossing";
            if (snapshot.Phase == "Clearance")
                return "Clearing crossing";

            return snapshot.Phase;
        }

        private static string FormatSignalUserAction(RoadBaseAI.TrafficLightState state, string greenText, string redText)
        {
            switch (state)
            {
                case RoadBaseAI.TrafficLightState.Green:
                    return greenText;
                case RoadBaseAI.TrafficLightState.GreenToRed:
                case RoadBaseAI.TrafficLightState.RedToGreen:
                    return "Changing";
                default:
                    return redText;
            }
        }

        private static Color GetSignalStatusColor(CrossingPathBuilder.SignalControllerDebugSnapshot snapshot)
        {
            if (snapshot.PedestrianState == RoadBaseAI.TrafficLightState.Green)
                return new Color(0.1f, 0.9f, 0.25f, 1f);

            switch (snapshot.VehicleState)
            {
                case RoadBaseAI.TrafficLightState.Green:
                    return new Color(0.1f, 0.9f, 0.25f, 1f);
                case RoadBaseAI.TrafficLightState.GreenToRed:
                case RoadBaseAI.TrafficLightState.RedToGreen:
                    return new Color(1f, 0.75f, 0.1f, 1f);
                default:
                    return new Color(1f, 0.12f, 0.08f, 1f);
            }
        }

        private void RebuildCrossingHighlightWorldVisuals(Camera camera, bool includeCrossingHighlights, bool includeValidationProblems)
        {
            ClearCrossingHighlightWorldVisuals();

            PedestrianToolMode activeMode = PedestrianCrossingToolkitState.ActiveMode;
            List<Vector3> midBlockVertices = new List<Vector3>();
            List<Vector2> midBlockUvs = new List<Vector2>();
            List<int> midBlockTriangles = new List<int>();
            List<Vector3> signalVertices = new List<Vector3>();
            List<Vector2> signalUvs = new List<Vector2>();
            List<int> signalTriangles = new List<int>();
            List<Vector3> subwayLinkVertices = new List<Vector3>();
            List<Vector2> subwayLinkUvs = new List<Vector2>();
            List<int> subwayLinkTriangles = new List<int>();
            List<Vector3> subwayPointVertices = new List<Vector3>();
            List<Vector2> subwayPointUvs = new List<Vector2>();
            List<int> subwayPointTriangles = new List<int>();
            List<Vector3> bridgeVertices = new List<Vector3>();
            List<Vector2> bridgeUvs = new List<Vector2>();
            List<int> bridgeTriangles = new List<int>();
            List<Vector3> validationProblemVertices = new List<Vector3>();
            List<Vector2> validationProblemUvs = new List<Vector2>();
            List<int> validationProblemTriangles = new List<int>();

            if (includeCrossingHighlights)
            {
                int count = CrossingPlacementRegistry.CopyTo(CrossingHighlightRenderBuffer);
                for (int i = 0; i < count; i++)
                {
                    CrossingPlacementAsset asset = CrossingHighlightRenderBuffer[i];
                    if (!ShouldHighlightAsset(asset, activeMode) || !IsHighlightInCameraView(camera, asset))
                        continue;

                    List<Vector3> vertices;
                    List<Vector2> uvs;
                    List<int> triangles;
                    if (!TryGetHighlightWorldMeshLists(
                        asset.Placement.Mode,
                        midBlockVertices,
                        midBlockUvs,
                        midBlockTriangles,
                        signalVertices,
                        signalUvs,
                        signalTriangles,
                        subwayLinkVertices,
                        subwayLinkUvs,
                        subwayLinkTriangles,
                        subwayPointVertices,
                        subwayPointUvs,
                        subwayPointTriangles,
                        bridgeVertices,
                        bridgeUvs,
                        bridgeTriangles,
                        out vertices,
                        out uvs,
                        out triangles))
                    {
                        continue;
                    }

                    AddCrossingHighlightIconGeometry(camera, asset, vertices, uvs, triangles);
                }
            }

            AddCrossingHighlightWorldVisual(PedestrianToolMode.MidBlockCrossing, midBlockVertices, midBlockUvs, midBlockTriangles);
            AddCrossingHighlightWorldVisual(PedestrianToolMode.SignalCrossing, signalVertices, signalUvs, signalTriangles);
            AddCrossingHighlightWorldVisual(PedestrianToolMode.SubwayLink, subwayLinkVertices, subwayLinkUvs, subwayLinkTriangles);
            AddCrossingHighlightWorldVisual(PedestrianToolMode.SubwayPointToPoint, subwayPointVertices, subwayPointUvs, subwayPointTriangles);
            AddCrossingHighlightWorldVisual(PedestrianToolMode.PedestrianBridge, bridgeVertices, bridgeUvs, bridgeTriangles);

            if (includeValidationProblems)
            {
                int problemCount = PedestrianCrossingToolkitState.CopyValidationProblemAssetsTo(ValidationProblemRenderBuffer);
                for (int i = 0; i < problemCount; i++)
                {
                    CrossingPlacementAsset asset = ValidationProblemRenderBuffer[i];
                    ValidationProblemRenderBuffer[i] = CrossingPlacementAsset.None;
                    if (asset.Id == 0 || !IsValidationProblemInCameraView(camera, asset))
                        continue;

                    AddValidationProblemIconGeometry(camera, asset, validationProblemVertices, validationProblemUvs, validationProblemTriangles);
                }
            }

            AddValidationProblemWorldVisual(validationProblemVertices, validationProblemUvs, validationProblemTriangles);
            _validationProblemCachedRevision = PedestrianCrossingToolkitState.ValidationProblemRevision;
        }

        private static bool TryGetHighlightWorldMeshLists(
            PedestrianToolMode mode,
            List<Vector3> midBlockVertices,
            List<Vector2> midBlockUvs,
            List<int> midBlockTriangles,
            List<Vector3> signalVertices,
            List<Vector2> signalUvs,
            List<int> signalTriangles,
            List<Vector3> subwayLinkVertices,
            List<Vector2> subwayLinkUvs,
            List<int> subwayLinkTriangles,
            List<Vector3> subwayPointVertices,
            List<Vector2> subwayPointUvs,
            List<int> subwayPointTriangles,
            List<Vector3> bridgeVertices,
            List<Vector2> bridgeUvs,
            List<int> bridgeTriangles,
            out List<Vector3> vertices,
            out List<Vector2> uvs,
            out List<int> triangles)
        {
            switch (mode)
            {
                case PedestrianToolMode.MidBlockCrossing:
                    vertices = midBlockVertices;
                    uvs = midBlockUvs;
                    triangles = midBlockTriangles;
                    return true;
                case PedestrianToolMode.SignalCrossing:
                    vertices = signalVertices;
                    uvs = signalUvs;
                    triangles = signalTriangles;
                    return true;
                case PedestrianToolMode.SubwayLink:
                    vertices = subwayLinkVertices;
                    uvs = subwayLinkUvs;
                    triangles = subwayLinkTriangles;
                    return true;
                case PedestrianToolMode.SubwayPointToPoint:
                    vertices = subwayPointVertices;
                    uvs = subwayPointUvs;
                    triangles = subwayPointTriangles;
                    return true;
                case PedestrianToolMode.PedestrianBridge:
                    vertices = bridgeVertices;
                    uvs = bridgeUvs;
                    triangles = bridgeTriangles;
                    return true;
                default:
                    vertices = null;
                    uvs = null;
                    triangles = null;
                    return false;
            }
        }

        private static void AddCrossingHighlightIconGeometry(Camera camera, CrossingPlacementAsset asset, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            CrossingPlacementPlan plan = asset.Plan;
            if (!plan.IsValid || camera == null)
                return;

            float size = GetCrossingLocationIconWorldSize(camera);
            Vector3 center = ToStreetPreview(plan.Center) + Vector3.up * Mathf.Clamp(size * 0.7f, 7f, 18f);
            AddWorldBillboardIcon(vertices, uvs, triangles, center, camera.transform.right, camera.transform.up, size);
        }

        private static void AddValidationProblemIconGeometry(Camera camera, CrossingPlacementAsset asset, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            if (camera == null)
                return;

            float size = GetValidationProblemIconWorldSize(camera);
            Vector3 center = ToStreetPreview(GetValidationProblemBasePosition(asset)) + Vector3.up * Mathf.Clamp(size * 0.75f, 14f, 30f);
            AddWorldBillboardIcon(vertices, uvs, triangles, center, camera.transform.right, camera.transform.up, size);
        }

        private static float GetCrossingLocationIconWorldSize(Camera camera)
        {
            return Mathf.Clamp(GetOverlayCameraSize(camera) * CrossingLocationIconWorldScale, CrossingLocationIconMinWorldSize, CrossingLocationIconMaxWorldSize);
        }

        private static float GetValidationProblemIconWorldSize(Camera camera)
        {
            return Mathf.Clamp(GetOverlayCameraSize(camera) * ValidationProblemIconWorldScale, ValidationProblemIconMinWorldSize, ValidationProblemIconMaxWorldSize);
        }

        private static bool IsValidationProblemInCameraView(Camera camera, CrossingPlacementAsset asset)
        {
            return IsWorldPointInCameraView(camera, GetValidationProblemBasePosition(asset));
        }

        private static Vector3 GetValidationProblemBasePosition(CrossingPlacementAsset asset)
        {
            if (asset.Plan.IsValid)
                return asset.Plan.Center;

            if (asset.Placement.HasSecondaryPoint)
                return (asset.Placement.WorldPosition + asset.Placement.SecondaryWorldPosition) * 0.5f;

            return asset.Placement.WorldPosition;
        }

        private static void AddWorldBillboardIcon(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 center, Vector3 cameraRight, Vector3 cameraUp, float size)
        {
            if (vertices == null || uvs == null || triangles == null || size <= 0.01f)
                return;

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

        private static void AddCrossingHighlightWorldGeometry(CrossingPlacementAsset asset, List<Vector3> vertices, List<int> triangles)
        {
            CrossingPlacementPlan plan = asset.Plan;
            if (!plan.IsValid)
                return;

            Vector3 lift = Vector3.up * HighlightWorldLift;
            Vector3 left = ToStreetPreview(plan.LeftEdge) + lift;
            Vector3 right = ToStreetPreview(plan.RightEdge) + lift;
            Vector3 center = ToStreetPreview(plan.Center) + lift;

            Vector3 direction = right - left;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
                direction = Vector3.right;
            else
                direction.Normalize();

            Vector3 normal = new Vector3(-direction.z, 0f, direction.x);
            float radius = GetHighlightWorldRadius(plan);

            AddWorldLineQuad(vertices, triangles, left, right, HighlightWorldLineWidth);
            AddWorldCircle(vertices, triangles, center, direction, normal, radius, HighlightWorldMarkerWidth);
            AddWorldLineQuad(vertices, triangles, center - direction * radius * 0.55f, center + direction * radius * 0.55f, HighlightWorldMarkerWidth);
            AddWorldLineQuad(vertices, triangles, center - normal * radius * 0.55f, center + normal * radius * 0.55f, HighlightWorldMarkerWidth);
        }

        private static float GetHighlightWorldRadius(CrossingPlacementPlan plan)
        {
            float span = Vector3.Distance(plan.LeftEdge, plan.RightEdge);
            return Mathf.Clamp(span * 0.14f, 2.1f, 4.4f);
        }

        private static void AddWorldCircle(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 direction, Vector3 normal, float radius, float width)
        {
            Vector3 previous = center + direction * radius;
            for (int i = 1; i <= HighlightWorldCircleSegments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / HighlightWorldCircleSegments;
                Vector3 next = center + direction * (Mathf.Cos(angle) * radius) + normal * (Mathf.Sin(angle) * radius);
                AddWorldLineQuad(vertices, triangles, previous, next, width);
                previous = next;
            }
        }

        private static void AddWorldLineQuad(List<Vector3> vertices, List<int> triangles, Vector3 start, Vector3 end, float width)
        {
            Vector3 span = end - start;
            span.y = 0f;
            if (span.sqrMagnitude <= 0.001f)
                return;

            Vector3 direction = span.normalized;
            Vector3 normal = new Vector3(-direction.z, 0f, direction.x) * (width * 0.5f);
            int index = vertices.Count;
            vertices.Add(start - normal);
            vertices.Add(start + normal);
            vertices.Add(end + normal);
            vertices.Add(end - normal);
            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
            triangles.Add(index);
            triangles.Add(index + 2);
            triangles.Add(index + 3);
        }

        private void AddCrossingHighlightWorldVisual(PedestrianToolMode mode, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            if (vertices == null || uvs == null || triangles == null || vertices.Count == 0 || uvs.Count != vertices.Count || triangles.Count == 0)
                return;

            GameObject visual = new GameObject("PCT Crossing Highlight " + mode);
            visual.hideFlags = HideFlags.HideAndDontSave;
            Mesh mesh = new Mesh();
            mesh.name = visual.name + " Mesh";
            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();

            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.mesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.material = GetCrossingHighlightWorldMaterial(mode);
            CrossingHighlightWorldVisuals.Add(visual);
        }

        private void AddValidationProblemWorldVisual(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            if (vertices == null || uvs == null || triangles == null || vertices.Count == 0 || uvs.Count != vertices.Count || triangles.Count == 0)
                return;

            GameObject visual = new GameObject("PCT Validation Problem Highlight");
            visual.hideFlags = HideFlags.HideAndDontSave;
            Mesh mesh = new Mesh();
            mesh.name = visual.name + " Mesh";
            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();

            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.mesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.material = GetValidationProblemWorldMaterial();
            CrossingHighlightWorldVisuals.Add(visual);
        }

        private void RebuildRouteWorldVisuals()
        {
            ClearRouteWorldVisuals();
            int linkCount = CrossingConnectivityPlanner.CopyLinksTo(LinkRenderBuffer);
            ConnectivityRenderKeys.Clear();
            for (int i = 0; i < linkCount; i++)
            {
                CrossingConnectivityLink link = LinkRenderBuffer[i];
                if (!IsSubwayLinkKind(link.Kind))
                    continue;

                Vector3 firstWorld;
                Vector3 secondWorld;
                GetDisplaySpan(link, out firstWorld, out secondWorld);
                ConnectivityRenderKeys.Add(CrossingConnectorKey.Make(firstWorld, secondWorld));
                AddRouteWorldVisual(firstWorld, secondWorld, link.UsesLaneTargets ? RouteWorldVisualWidth : RouteWorldVisualWidth * 1.4f);
            }

            int pathCount = CrossingPathWorkOrderPlanner.CopyWorkOrdersTo(PathWorkOrderRenderBuffer);
            for (int i = 0; i < pathCount; i++)
            {
                CrossingPathWorkOrder order = PathWorkOrderRenderBuffer[i];
                if (order.Kind != CrossingPathWorkOrderKind.SubwayPath)
                    continue;

                string key = CrossingConnectorKey.Make(order.FirstPosition, order.SecondPosition);
                if (ConnectivityRenderKeys.Contains(key))
                    continue;

                AddRouteWorldVisual(order.FirstPosition, order.SecondPosition, RouteWorldVisualWidth);
            }

            int connectorCount = CrossingLandingConnectorPlanner.CopyWorkOrdersTo(ConnectorRenderBuffer);
            ConnectorRenderKeys.Clear();
            for (int i = 0; i < connectorCount; i++)
            {
                CrossingLandingConnectorWorkOrder order = ConnectorRenderBuffer[i];
                if (!ShouldDisplayLandingConnector(order))
                    continue;

                string key = CrossingConnectorKey.Make(order.FromPosition, order.ToPosition);
                if (ConnectorRenderKeys.Contains(key))
                    continue;

                ConnectorRenderKeys.Add(key);
                AddRouteWorldVisual(order.FromPosition, GetLandingConnectorDisplayTo(order), RouteWorldVisualWidth);
            }

            int accessCount = CrossingLandingConnectorPlanner.CopyAccessAssetsTo(AccessAssetRenderBuffer);
            for (int i = 0; i < accessCount; i++)
            {
                CrossingLandingAccessAssetWorkOrder order = AccessAssetRenderBuffer[i];
                if (!ShouldDisplayLandingAccessAsset(order))
                    continue;

                AddRouteWorldVisual(order.DeckPosition, order.Position, RouteWorldVisualWidth);
            }
        }

        private void AddRouteWorldVisual(Vector3 start, Vector3 end, float width)
        {
            start = ToStreetPreview(start);
            end = ToStreetPreview(end);
            Vector3 span = end - start;
            if (span.sqrMagnitude <= 0.01f)
                return;

            float length = span.magnitude;
            Vector3 direction = span / length;
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "PCT Subway Route Preview";
            visual.transform.position = (start + end) * 0.5f;
            visual.transform.rotation = Quaternion.FromToRotation(Vector3.forward, direction);
            visual.transform.localScale = new Vector3(width, RouteWorldVisualHeight, length);
            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = GetRouteWorldMaterial();

            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);

            RouteWorldVisuals.Add(visual);
        }

        private void ClearRouteWorldVisuals()
        {
            for (int i = RouteWorldVisuals.Count - 1; i >= 0; i--)
            {
                GameObject visual = RouteWorldVisuals[i];
                if (visual != null)
                    UnityEngine.Object.Destroy(visual);
            }

            RouteWorldVisuals.Clear();
        }

        private void ClearCrossingHighlightWorldVisuals()
        {
            for (int i = CrossingHighlightWorldVisuals.Count - 1; i >= 0; i--)
            {
                GameObject visual = CrossingHighlightWorldVisuals[i];
                if (visual != null)
                    UnityEngine.Object.Destroy(visual);
            }

            CrossingHighlightWorldVisuals.Clear();
        }

        private static Material GetRouteWorldMaterial()
        {
            if (_subwayRouteWorldMaterial != null)
                return _subwayRouteWorldMaterial;

            Shader shader = Shader.Find("Transparent/Diffuse") ?? Shader.Find("Diffuse");
            _subwayRouteWorldMaterial = new Material(shader);
            _subwayRouteWorldMaterial.color = SubwayRouteWorldColor;
            return _subwayRouteWorldMaterial;
        }

        private static Material GetCrossingHighlightWorldMaterial(PedestrianToolMode mode)
        {
            switch (mode)
            {
                case PedestrianToolMode.MidBlockCrossing:
                    if (_midBlockHighlightWorldMaterial == null)
                        _midBlockHighlightWorldMaterial = CreateCrossingHighlightWorldMaterial(mode);
                    return _midBlockHighlightWorldMaterial;
                case PedestrianToolMode.SignalCrossing:
                    if (_signalHighlightWorldMaterial == null)
                        _signalHighlightWorldMaterial = CreateCrossingHighlightWorldMaterial(mode);
                    return _signalHighlightWorldMaterial;
                case PedestrianToolMode.SubwayLink:
                    if (_subwayLinkHighlightWorldMaterial == null)
                        _subwayLinkHighlightWorldMaterial = CreateCrossingHighlightWorldMaterial(mode);
                    return _subwayLinkHighlightWorldMaterial;
                case PedestrianToolMode.SubwayPointToPoint:
                    if (_subwayPointHighlightWorldMaterial == null)
                        _subwayPointHighlightWorldMaterial = CreateCrossingHighlightWorldMaterial(mode);
                    return _subwayPointHighlightWorldMaterial;
                case PedestrianToolMode.PedestrianBridge:
                    if (_bridgeHighlightWorldMaterial == null)
                        _bridgeHighlightWorldMaterial = CreateCrossingHighlightWorldMaterial(mode);
                    return _bridgeHighlightWorldMaterial;
                default:
                    return GetRouteWorldMaterial();
            }
        }

        private static Material CreateCrossingHighlightWorldMaterial(PedestrianToolMode mode)
        {
            Shader shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Transparent/Diffuse") ?? Shader.Find("Diffuse");
            Material material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
            material.color = Color.white;
            material.mainTexture = GetCrossingLocationIconTexture(mode);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            material.renderQueue = 3000;
            return material;
        }

        private static Material GetValidationProblemWorldMaterial()
        {
            if (_validationProblemWorldMaterial != null)
                return _validationProblemWorldMaterial;

            Shader shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Transparent/Diffuse") ?? Shader.Find("Diffuse");
            _validationProblemWorldMaterial = new Material(shader);
            _validationProblemWorldMaterial.hideFlags = HideFlags.HideAndDontSave;
            _validationProblemWorldMaterial.color = Color.white;
            _validationProblemWorldMaterial.mainTexture = GetValidationProblemIconTexture();
            _validationProblemWorldMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _validationProblemWorldMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _validationProblemWorldMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _validationProblemWorldMaterial.SetInt("_ZWrite", 0);
            _validationProblemWorldMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            _validationProblemWorldMaterial.renderQueue = 3100;
            return _validationProblemWorldMaterial;
        }

        private static Texture2D GetCrossingLocationIconTexture(PedestrianToolMode mode)
        {
            Texture2D existing;
            switch (mode)
            {
                case PedestrianToolMode.MidBlockCrossing:
                    existing = _midBlockCrossingLocationIconTexture;
                    break;
                case PedestrianToolMode.SignalCrossing:
                    existing = _signalCrossingLocationIconTexture;
                    break;
                case PedestrianToolMode.SubwayLink:
                    existing = _subwayLinkCrossingLocationIconTexture;
                    break;
                case PedestrianToolMode.SubwayPointToPoint:
                    existing = _subwayPointCrossingLocationIconTexture;
                    break;
                case PedestrianToolMode.PedestrianBridge:
                    existing = _bridgeCrossingLocationIconTexture;
                    break;
                default:
                    existing = _midBlockCrossingLocationIconTexture;
                    break;
            }

            if (existing != null)
                return existing;

            Texture2D texture = new Texture2D(CrossingLocationIconTextureSize, CrossingLocationIconTextureSize, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float edge = 7f;
            float radius = 13f;
            for (int y = 0; y < CrossingLocationIconTextureSize; y++)
            {
                for (int x = 0; x < CrossingLocationIconTextureSize; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    Color pixel = new Color(0f, 0f, 0f, 0f);

                    if (IsInsideRoundedRect(px, py, edge, edge, CrossingLocationIconTextureSize - edge, CrossingLocationIconTextureSize - edge, radius))
                    {
                        bool inner = IsInsideRoundedRect(px, py, edge + 4f, edge + 4f, CrossingLocationIconTextureSize - edge - 4f, CrossingLocationIconTextureSize - edge - 4f, radius - 4f);
                        pixel = inner
                            ? new Color(0.21f, 0.24f, 0.27f, 0.90f)
                            : new Color(0.48f, 0.53f, 0.58f, 0.96f);

                        Color iconPixel = GetCrossingLocationIconPixel(mode, px - CrossingLocationIconTextureSize * 0.5f, py - CrossingLocationIconTextureSize * 0.5f);
                        if (iconPixel.a > 0f)
                            pixel = AlphaOver(pixel, iconPixel);
                    }

                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply(false, true);
            switch (mode)
            {
                case PedestrianToolMode.MidBlockCrossing:
                    _midBlockCrossingLocationIconTexture = texture;
                    break;
                case PedestrianToolMode.SignalCrossing:
                    _signalCrossingLocationIconTexture = texture;
                    break;
                case PedestrianToolMode.SubwayLink:
                    _subwayLinkCrossingLocationIconTexture = texture;
                    break;
                case PedestrianToolMode.SubwayPointToPoint:
                    _subwayPointCrossingLocationIconTexture = texture;
                    break;
                case PedestrianToolMode.PedestrianBridge:
                    _bridgeCrossingLocationIconTexture = texture;
                    break;
                default:
                    _midBlockCrossingLocationIconTexture = texture;
                    break;
            }

            return texture;
        }

        private static Texture2D GetValidationProblemIconTexture()
        {
            if (_validationProblemIconTexture != null)
                return _validationProblemIconTexture;

            Texture2D texture = new Texture2D(ValidationProblemIconTextureSize, ValidationProblemIconTextureSize, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < ValidationProblemIconTextureSize; y++)
            {
                for (int x = 0; x < ValidationProblemIconTextureSize; x++)
                {
                    float px = x + 0.5f - ValidationProblemIconTextureSize * 0.5f;
                    float py = y + 0.5f - ValidationProblemIconTextureSize * 0.5f;
                    Color pixel = new Color(0f, 0f, 0f, 0f);

                    float radius = Mathf.Sqrt((px * px) + (py * py));
                    if (radius <= 42f)
                        pixel = new Color(0f, 0f, 0f, 0.42f);

                    Vector2 point = new Vector2(px, py);
                    float firstStroke = DistanceToSegment(point, new Vector2(-30f, -30f), new Vector2(30f, 30f));
                    float secondStroke = DistanceToSegment(point, new Vector2(-30f, 30f), new Vector2(30f, -30f));
                    float strokeDistance = Mathf.Min(firstStroke, secondStroke);
                    if (strokeDistance <= 10f)
                        pixel = AlphaOver(pixel, new Color(0f, 0f, 0f, 0.9f));
                    if (strokeDistance <= 6f)
                        pixel = AlphaOver(pixel, new Color(1f, 0.02f, 0f, 1f));

                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply(false, true);
            _validationProblemIconTexture = texture;
            return texture;
        }

        private static Color GetCrossingLocationIconPixel(PedestrianToolMode mode, float x, float y)
        {
            Color color = GetCrossingIconColor(mode);
            switch (mode)
            {
                case PedestrianToolMode.SignalCrossing:
                    return GetSignalCrossingIconPixel(x, y);
                case PedestrianToolMode.SubwayLink:
                    return GetAutoSubwayIconPixel(x, y, color);
                case PedestrianToolMode.SubwayPointToPoint:
                    return GetManualSubwayIconPixel(x, y, color);
                case PedestrianToolMode.PedestrianBridge:
                    return GetBridgeIconPixel(x, y, color);
                case PedestrianToolMode.MidBlockCrossing:
                default:
                    return GetStandardCrossingIconPixel(x, y, color);
            }
        }

        private static Color GetCrossingIconColor(PedestrianToolMode mode)
        {
            switch (mode)
            {
                case PedestrianToolMode.SignalCrossing:
                    return new Color(1f, 0.12f, 0.08f, 1f);
                case PedestrianToolMode.SubwayLink:
                    return new Color(0.02f, 0.92f, 1f, 1f);
                case PedestrianToolMode.SubwayPointToPoint:
                    return new Color(0.72f, 0.36f, 1f, 1f);
                case PedestrianToolMode.PedestrianBridge:
                    return new Color(0.28f, 1f, 0.24f, 1f);
                case PedestrianToolMode.MidBlockCrossing:
                default:
                    return new Color(1f, 1f, 1f, 1f);
            }
        }

        private static Color GetStandardCrossingIconPixel(float x, float y, Color color)
        {
            bool border = Mathf.Abs(x) >= 24f && Mathf.Abs(x) <= 27f && Mathf.Abs(y) <= 25f;
            bool stripe = Mathf.Abs(x) <= 21f
                          && (Mathf.Abs(y + 18f) <= 2.2f
                              || Mathf.Abs(y + 6f) <= 2.2f
                              || Mathf.Abs(y - 6f) <= 2.2f
                              || Mathf.Abs(y - 18f) <= 2.2f);

            if (border || stripe)
                return color;

            return new Color(0f, 0f, 0f, 0f);
        }

        private static Color GetSignalCrossingIconPixel(float x, float y)
        {
            bool body = Mathf.Abs(x) <= 11f && Mathf.Abs(y) <= 28f;
            if (!body)
                return new Color(0f, 0f, 0f, 0f);

            bool edge = Mathf.Abs(x) >= 8.5f || Mathf.Abs(y) >= 25.5f;
            if (edge)
                return new Color(0.92f, 0.95f, 0.98f, 1f);

            if (DistanceSquared(x, y - 15f) <= 25f)
                return new Color(1f, 0.08f, 0.04f, 1f);
            if (DistanceSquared(x, y) <= 25f)
                return new Color(1f, 0.72f, 0.06f, 1f);
            if (DistanceSquared(x, y + 15f) <= 25f)
                return new Color(0.12f, 1f, 0.26f, 1f);

            return new Color(0.02f, 0.025f, 0.03f, 1f);
        }

        private static Color GetAutoSubwayIconPixel(float x, float y, Color color)
        {
            float archDistance = Mathf.Sqrt((x * x) + ((y + 12f) * (y + 12f)));
            bool arch = y >= -12f && Mathf.Abs(archDistance - 24f) <= 2.8f;
            bool side = Mathf.Abs(Mathf.Abs(x) - 24f) <= 2.8f && y >= -25f && y <= -10f;
            bool steps = Mathf.Abs(x) <= 17f
                         && (Mathf.Abs(y + 22f) <= 1.8f
                             || Mathf.Abs(y + 15f) <= 1.8f
                             || Mathf.Abs(y + 8f) <= 1.8f);

            if (arch || side || steps)
                return color;

            return new Color(0f, 0f, 0f, 0f);
        }

        private static Color GetManualSubwayIconPixel(float x, float y, Color color)
        {
            Vector2 point = new Vector2(x, y);
            bool link = DistanceToSegment(point, new Vector2(-22f, -14f), new Vector2(22f, 14f)) <= 3.2f;
            bool firstNode = DistanceSquared(x + 22f, y + 14f) <= 64f;
            bool secondNode = DistanceSquared(x - 22f, y - 14f) <= 64f;

            if (link || firstNode || secondNode)
                return color;

            return new Color(0f, 0f, 0f, 0f);
        }

        private static Color GetBridgeIconPixel(float x, float y, Color color)
        {
            bool deck = Mathf.Abs(y + 2f) <= 2.8f && Mathf.Abs(x) <= 27f;
            float archDistance = Mathf.Sqrt((x * x) + ((y + 22f) * (y + 22f)));
            bool arch = y >= -22f && y <= 11f && Mathf.Abs(archDistance - 28f) <= 2.7f;
            bool support = (Mathf.Abs(x + 18f) <= 2f || Mathf.Abs(x - 18f) <= 2f) && y <= -2f && y >= -18f;

            if (deck || arch || support)
                return color;

            return new Color(0f, 0f, 0f, 0f);
        }

        private static bool IsInsideRoundedRect(float x, float y, float left, float bottom, float right, float top, float radius)
        {
            if (x < left || x > right || y < bottom || y > top)
                return false;

            float cornerX = x < left + radius ? left + radius : x > right - radius ? right - radius : x;
            float cornerY = y < bottom + radius ? bottom + radius : y > top - radius ? top - radius : y;
            float dx = x - cornerX;
            float dy = y - cornerY;
            return (dx * dx) + (dy * dy) <= radius * radius;
        }

        private static Color AlphaOver(Color background, Color foreground)
        {
            float alpha = foreground.a + background.a * (1f - foreground.a);
            if (alpha <= 0.001f)
                return new Color(0f, 0f, 0f, 0f);

            return new Color(
                ((foreground.r * foreground.a) + (background.r * background.a * (1f - foreground.a))) / alpha,
                ((foreground.g * foreground.a) + (background.g * background.a * (1f - foreground.a))) / alpha,
                ((foreground.b * foreground.a) + (background.b * background.a * (1f - foreground.a))) / alpha,
                alpha);
        }

        private static float DistanceSquared(float x, float y)
        {
            return (x * x) + (y * y);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.001f)
                return Vector2.Distance(point, start);

            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private static void DrawConnectivityLinks(Camera camera)
        {
            int count = CrossingConnectivityPlanner.CopyLinksTo(LinkRenderBuffer);
            ConnectivityRenderKeys.Clear();
            for (int i = 0; i < count; i++)
            {
                CrossingConnectivityLink link = LinkRenderBuffer[i];
                if (!IsSubwayLinkKind(link.Kind))
                    continue;

                Vector3 firstWorld;
                Vector3 secondWorld;
                GetDisplaySpan(link, out firstWorld, out secondWorld);
                ConnectivityRenderKeys.Add(CrossingConnectorKey.Make(firstWorld, secondWorld));
                Vector2 first;
                Vector2 second;
                if (!WorldToGuiPoint(camera, ToStreetPreview(firstWorld), out first)
                    || !WorldToGuiPoint(camera, ToStreetPreview(secondWorld), out second))
                    continue;

                DrawRouteLine(first, second, link.UsesLaneTargets ? 4f : 7f);
            }
        }

        private static void DrawUndergroundSubwayPathWorkOrders(Camera camera)
        {
            int count = CrossingPathWorkOrderPlanner.CopyWorkOrdersTo(PathWorkOrderRenderBuffer);
            Color oldColor = GUI.color;
            for (int i = 0; i < count; i++)
            {
                CrossingPathWorkOrder order = PathWorkOrderRenderBuffer[i];
                if (order.Kind != CrossingPathWorkOrderKind.SubwayPath)
                    continue;

                string key = CrossingConnectorKey.Make(order.FirstPosition, order.SecondPosition);
                if (ConnectivityRenderKeys.Contains(key))
                    continue;

                Vector2 first;
                Vector2 second;
                if (!WorldToGuiPoint(camera, ToStreetPreview(order.FirstPosition), out first)
                    || !WorldToGuiPoint(camera, ToStreetPreview(order.SecondPosition), out second))
                    continue;

                DrawRouteLine(first, second, 5f);
            }

            GUI.color = oldColor;
        }

        private static void GetDisplaySpan(CrossingConnectivityLink link, out Vector3 firstWorld, out Vector3 secondWorld)
        {
            firstWorld = link.FirstPosition;
            secondWorld = link.SecondPosition;
            if (IsSubwayLinkKind(link.Kind))
            {
                Vector3 resolved;
                if (CrossingLandingConnectorPlanner.TryGetAccessAssetPosition(link.AssetId, "A", CrossingLandingAccessAssetKind.SubwayEntrance, out resolved))
                    firstWorld = resolved;

                if (CrossingLandingConnectorPlanner.TryGetAccessAssetPosition(link.AssetId, "B", CrossingLandingAccessAssetKind.SubwayEntrance, out resolved))
                    secondWorld = resolved;
                return;
            }

            if (link.Kind != CrossingConnectivityLinkKind.JunctionBridgeApproach)
                return;

            CrossingPlacementAsset asset;
            if (!CrossingPlacementRegistry.TryGetAssetById(link.AssetId, out asset) || !asset.Plan.IsValid)
                return;

            firstWorld = (link.FirstPosition + link.SecondPosition) * 0.5f;
            secondWorld = asset.Plan.Center;
        }

        private static void DrawLandingAccessAssets(Camera camera)
        {
            int count = CrossingLandingConnectorPlanner.CopyAccessAssetsTo(AccessAssetRenderBuffer);
            for (int i = 0; i < count; i++)
            {
                CrossingLandingAccessAssetWorkOrder order = AccessAssetRenderBuffer[i];
                if (!ShouldDisplayLandingAccessAsset(order))
                    continue;

                Vector2 point;
                Vector2 deck;
                if (!WorldToGuiPoint(camera, ToStreetPreview(order.Position), out point)
                    || !WorldToGuiPoint(camera, ToStreetPreview(order.DeckPosition), out deck))
                    continue;

                Color routeColor = order.ConnectorTargetKind == CrossingLandingConnectorTargetKind.None
                    ? new Color(1f, 0.35f, 0.18f, 0.82f)
                    : SubwayRoutePreviewColor;
                Color mutedColor = order.ConnectorTargetKind == CrossingLandingConnectorTargetKind.None
                    ? new Color(1f, 0.35f, 0.18f, 0.08f)
                    : SubwayRouteMutedColor;

                DrawAccessLeg(deck, point, order, 3f, routeColor, mutedColor);
            }
        }

        private static void DrawAccessLeg(Vector2 deck, Vector2 point, CrossingLandingAccessAssetWorkOrder order, float width, Color routeColor, Color mutedColor)
        {
            if (!IsBentAccess(order.AccessKind))
            {
                DrawClippedLine(deck, point, width, routeColor, mutedColor);
                return;
            }

            Vector2 delta = point - deck;
            if (delta.sqrMagnitude < 1f)
                return;

            Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
            float bend = GetAccessBendPixels(order.AccessKind);
            Vector2 firstBend = deck + normal * bend;
            Vector2 secondBend = point + normal * bend;

            if (IsYAccess(order.AccessKind))
            {
                Vector2 mid = (deck + point) * 0.5f + normal * (bend * 0.7f);
                DrawClippedLine(deck, mid, width, routeColor, mutedColor);
                DrawClippedLine(mid, point, width, routeColor, mutedColor);
                return;
            }

            DrawClippedLine(deck, firstBend, width, routeColor, mutedColor);
            DrawClippedLine(firstBend, secondBend, width, routeColor, mutedColor);
            DrawClippedLine(secondBend, point, width, routeColor, mutedColor);
        }

        private static bool IsBentAccess(CrossingLandingAccessKind accessKind)
        {
            return accessKind == CrossingLandingAccessKind.BridgeUShapedStairs
                   || accessKind == CrossingLandingAccessKind.BridgeZShapedStairs
                   || accessKind == CrossingLandingAccessKind.BridgeXShapedStairs
                   || accessKind == CrossingLandingAccessKind.BridgeYShapedStairs
                   || accessKind == CrossingLandingAccessKind.SubwayUShapedEntrance
                   || accessKind == CrossingLandingAccessKind.SubwayZShapedEntrance
                   || accessKind == CrossingLandingAccessKind.SubwayXShapedEntrance
                   || accessKind == CrossingLandingAccessKind.SubwayYShapedEntrance;
        }

        private static bool IsYAccess(CrossingLandingAccessKind accessKind)
        {
            return accessKind == CrossingLandingAccessKind.BridgeYShapedStairs
                   || accessKind == CrossingLandingAccessKind.SubwayYShapedEntrance;
        }

        private static float GetAccessBendPixels(CrossingLandingAccessKind accessKind)
        {
            if (accessKind == CrossingLandingAccessKind.BridgeUShapedStairs
                || accessKind == CrossingLandingAccessKind.SubwayUShapedEntrance)
                return 34f;

            if (accessKind == CrossingLandingAccessKind.BridgeXShapedStairs
                || accessKind == CrossingLandingAccessKind.SubwayXShapedEntrance)
                return 28f;

            return 24f;
        }

        private static void DrawLandingConnectors(Camera camera)
        {
            int count = CrossingLandingConnectorPlanner.CopyWorkOrdersTo(ConnectorRenderBuffer);
            ConnectorRenderKeys.Clear();
            for (int i = 0; i < count; i++)
            {
                CrossingLandingConnectorWorkOrder order = ConnectorRenderBuffer[i];
                if (!ShouldDisplayLandingConnector(order))
                    continue;

                string key = CrossingConnectorKey.Make(order.FromPosition, order.ToPosition);
                if (ConnectorRenderKeys.Contains(key))
                    continue;

                ConnectorRenderKeys.Add(key);
                Vector3 displayTo = GetLandingConnectorDisplayTo(order);
                Vector2 from;
                Vector2 to;
                if (!WorldToGuiPoint(camera, ToStreetPreview(order.FromPosition), out from)
                    || !WorldToGuiPoint(camera, ToStreetPreview(displayTo), out to))
                    continue;

                DrawRouteLine(from, to, 4f);
            }
        }

        private static Vector3 GetLandingConnectorDisplayTo(CrossingLandingConnectorWorkOrder order)
        {
            if (!IsSubwayLinkKind(order.CrossingKind))
                return order.ToPosition;

            Vector3 direction = order.ToPosition - order.FromPosition;
            direction.y = 0f;
            float distance = direction.magnitude;
            if (distance <= SubwayConnectorDisplayLength || distance <= 0.01f)
                return order.ToPosition;

            direction /= distance;
            Vector3 displayTo = order.FromPosition + direction * SubwayConnectorDisplayLength;
            displayTo.y = order.ToPosition.y;
            return displayTo;
        }

        private static bool ShouldDisplayLandingConnector(CrossingLandingConnectorWorkOrder order)
        {
            if (IsBridgeConnector(order))
                return false;

            return order.CrossingKind != CrossingConnectivityLinkKind.SubwaySpan;
        }

        private static bool ShouldDisplayLandingAccessAsset(CrossingLandingAccessAssetWorkOrder order)
        {
            return order.AssetKind != CrossingLandingAccessAssetKind.BridgeStairRampLanding;
        }

        private static void DrawRouteLine(Vector2 start, Vector2 end, float width)
        {
            DrawClippedLine(start, end, width, SubwayRoutePreviewColor, SubwayRouteMutedColor);
        }

        private static void DrawClippedLine(Vector2 start, Vector2 end, float width, Color routeColor, Color mutedColor)
        {
            Rect[] hideRects = GetRouteHideRects();
            Rect panelRect;
            bool hasPanel = PedestrianCrossingToolkitPanel.TryGetPanelScreenRect(out panelRect);
            DrawClippedLine(start, end, width, routeColor, mutedColor, hideRects, hasPanel, panelRect);
        }

        private static void DrawClippedLine(Vector2 start, Vector2 end, float width, Color routeColor, Color mutedColor, Rect[] hideRects, bool hasPanel, Rect panelRect)
        {
            float length = Vector2.Distance(start, end);
            if (length <= 0.001f)
                return;

            int chunks = Mathf.Max(1, Mathf.CeilToInt(length / RouteClipChunkPixels));
            Color oldColor = GUI.color;
            for (int i = 0; i < chunks; i++)
            {
                float fromT = i / (float)chunks;
                float toT = (i + 1) / (float)chunks;
                float midT = (fromT + toT) * 0.5f;
                Vector2 mid = Vector2.Lerp(start, end, midT);
                if (IsInsideAny(mid, hideRects))
                    continue;

                GUI.color = hasPanel && panelRect.Contains(mid) ? mutedColor : routeColor;
                DrawGlowLine(Vector2.Lerp(start, end, fromT), Vector2.Lerp(start, end, toT), width);
            }

            GUI.color = oldColor;
        }

        private static void DrawClippedCircle(Vector2 center, float radius, float width, Color routeColor, Color mutedColor, Rect[] hideRects, bool hasPanel, Rect panelRect)
        {
            if (radius <= 0.1f)
                return;

            Vector2 previous = center + new Vector2(radius, 0f);
            for (int i = 1; i <= HighlightCircleSegments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / HighlightCircleSegments;
                Vector2 next = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                DrawClippedLine(previous, next, width, routeColor, mutedColor, hideRects, hasPanel, panelRect);
                previous = next;
            }
        }

        private static Rect[] GetRouteHideRects()
        {
            List<Rect> rects = new List<Rect>();
            Rect toolTipRect;
            if (PedestrianCrossingInteractionTool.TryGetToolInfoScreenRect(out toolTipRect))
                rects.Add(toolTipRect);

            rects.Add(new Rect(0f, Mathf.Max(0f, Screen.height - 230f), Screen.width, 230f));
            return rects.ToArray();
        }

        private static bool IsInsideAny(Vector2 point, Rect[] rects)
        {
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].Contains(point))
                    return true;
            }

            return false;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float abC = Cross(b - a, c - a);
            float abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c);
            float cdB = Cross(d - c, b - c);

            if (Mathf.Abs(abC) <= 0.001f && IsBetween(a, b, c))
                return true;
            if (Mathf.Abs(abD) <= 0.001f && IsBetween(a, b, d))
                return true;
            if (Mathf.Abs(cdA) <= 0.001f && IsBetween(c, d, a))
                return true;
            if (Mathf.Abs(cdB) <= 0.001f && IsBetween(c, d, b))
                return true;

            return (abC > 0f) != (abD > 0f) && (cdA > 0f) != (cdB > 0f);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
        }

        private static bool IsBetween(Vector2 a, Vector2 b, Vector2 point)
        {
            return point.x >= Mathf.Min(a.x, b.x) - 0.001f
                   && point.x <= Mathf.Max(a.x, b.x) + 0.001f
                   && point.y >= Mathf.Min(a.y, b.y) - 0.001f
                   && point.y <= Mathf.Max(a.y, b.y) + 0.001f;
        }

        private static bool WorldToGuiPoint(Camera camera, Vector3 world, out Vector2 guiPoint)
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

        private static bool IsWorldPointInCameraView(Camera camera, Vector3 world)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            return viewport.z > 0f
                   && viewport.x >= 0f
                   && viewport.x <= 1f
                   && viewport.y >= 0f
                   && viewport.y <= 1f;
        }

        private static void DrawLine(Vector2 start, Vector2 end, float width)
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(start, end);
            GUIUtility.RotateAroundPivot(angle, start);
            DrawSolidRect(new Rect(start.x, start.y - (width * 0.5f), length, width));
            GUI.matrix = oldMatrix;
        }

        private static void DrawGlowLine(Vector2 start, Vector2 end, float width)
        {
            Color color = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * 0.28f);
            DrawLine(start, end, width + 5f);
            GUI.color = color;
            DrawLine(start, end, width);
        }

        private static void DrawSolidRect(Rect rect)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        private static Vector3 ToStreetPreview(Vector3 position)
        {
            return new Vector3(position.x, position.y + 0.12f, position.z);
        }

        private static bool IsSubwayLinkKind(CrossingConnectivityLinkKind kind)
        {
            return kind == CrossingConnectivityLinkKind.SubwaySpan
                   || kind == CrossingConnectivityLinkKind.JunctionSubwayApproach;
        }

        private static bool IsBridgeConnector(CrossingLandingConnectorWorkOrder order)
        {
            return order.CrossingKind == CrossingConnectivityLinkKind.PedestrianBridgeSpan
                   || order.CrossingKind == CrossingConnectivityLinkKind.JunctionBridgeApproach;
        }

    }
}
