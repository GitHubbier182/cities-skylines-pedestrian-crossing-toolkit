using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    internal static class PedestrianCrossingBulldozeHarmony
    {
        private const string HarmonyId = "ScratchyBald.PedestrianCrossingToolkit.Bulldoze";
        private const float PickRadiusPixels = 20f;

        private static Harmony _harmony;
        private static FieldInfo _hoverInstanceField;
        private static FieldInfo _hoverInstance2Field;
        private static FieldInfo _lastInstanceField;
        private static FieldInfo _lastInstance2Field;
        private static MethodInfo _getToolColorMethod;
        private static GameObject _selectorPrefabObject;
        private static BuildingInfo _selectorPrefab;
        private static bool _operational;
        private static bool _captureUntilMouseUp;
        private static int _hoveredAssetId;
        private static CrossingLandingAccessAssetWorkOrder[] _accessPickBuffer = new CrossingLandingAccessAssetWorkOrder[32];

        internal static bool Apply()
        {
            if (_harmony != null)
                return _operational;

            try
            {
                MethodInfo target = AccessTools.Method(typeof(BulldozeTool), "OnToolUpdate");
                MethodInfo renderOverlayTarget = AccessTools.Method(typeof(DefaultTool), "RenderOverlay");
                _hoverInstanceField = AccessTools.Field(typeof(DefaultTool), "m_hoverInstance");
                _hoverInstance2Field = AccessTools.Field(typeof(DefaultTool), "m_hoverInstance2");
                _lastInstanceField = AccessTools.Field(typeof(BulldozeTool), "m_lastInstance");
                _lastInstance2Field = AccessTools.Field(typeof(BulldozeTool), "m_lastInstance2");
                _getToolColorMethod = AccessTools.Method(
                    typeof(ToolBase),
                    "GetToolColor",
                    new[] { typeof(bool), typeof(bool) });
                if (target == null
                    || renderOverlayTarget == null
                    || _hoverInstanceField == null
                    || _hoverInstance2Field == null
                    || _lastInstanceField == null
                    || _lastInstance2Field == null
                    || _getToolColorMethod == null)
                {
                    throw new MissingMemberException(
                        "BulldozeTool.OnToolUpdate or its vanilla target fields were not found.");
                }

                _harmony = new Harmony(HarmonyId);
                _harmony.Patch(
                    target,
                    prefix: new HarmonyMethod(
                        typeof(PedestrianCrossingBulldozeHarmony),
                        nameof(OnToolUpdatePrefix)));
                _harmony.Patch(
                    renderOverlayTarget,
                    prefix: new HarmonyMethod(
                        typeof(PedestrianCrossingBulldozeHarmony),
                        nameof(RenderOverlayPrefix)),
                    postfix: new HarmonyMethod(
                        typeof(PedestrianCrossingBulldozeHarmony),
                        nameof(RenderOverlayPostfix)));
                _operational = true;
                PedestrianCrossingLog.UnityInfo(
                    "Vanilla Bulldoze crossing-only removal boundary enabled.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[PedestrianCrossingToolkit] Vanilla Bulldoze integration failed; vanilla remains unmodified: "
                    + exception);
                Unpatch();
                return false;
            }
        }

        internal static void Unpatch()
        {
            _operational = false;
            _captureUntilMouseUp = false;
            _hoveredAssetId = 0;
            DestroySelectorPrefab();
            if (_harmony == null)
                return;

            try
            {
                _harmony.UnpatchAll(HarmonyId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[PedestrianCrossingToolkit] Vanilla Bulldoze integration could not be removed cleanly: "
                    + exception.Message);
            }
            finally
            {
                _harmony = null;
            }
        }

        internal static bool TryGetHoveredAsset(out CrossingPlacementAsset asset)
        {
            asset = CrossingPlacementAsset.None;
            return _hoveredAssetId != 0
                   && CrossingPlacementRegistry.TryGetAssetById(_hoveredAssetId, out asset);
        }

        private static bool OnToolUpdatePrefix(BulldozeTool __instance)
        {
            if (!_operational || !PedestrianCrossingToolkitState.Enabled || __instance == null)
            {
                _hoveredAssetId = 0;
                return true;
            }

            try
            {
                if (_captureUntilMouseUp)
                {
                    ClearVanillaTargets(__instance);
                    _hoveredAssetId = 0;
                    if (!Input.GetMouseButton(0))
                        _captureUntilMouseUp = false;
                    return false;
                }

                ToolController controller = ToolsModifierControl.toolController;
                Camera camera = Camera.main;
                CrossingPlacementAsset asset = CrossingPlacementAsset.None;
                bool overCrossing = controller != null
                                    && controller.CurrentTool == __instance
                                    && !controller.IsInsideUI
                                    && Cursor.visible
                                    && camera != null
                                    && !PedestrianCrossingToolkitPanel.IsMouseOverAnyBlockingUi()
                                    && TryGetCrossingUnderPointer(
                                        camera,
                                        Input.mousePosition,
                                        out asset);
                if (!overCrossing)
                {
                    _hoveredAssetId = 0;
                    return true;
                }

                _hoveredAssetId = asset.Id;
                ClearVanillaTargets(__instance);
                if (Input.GetMouseButtonDown(0))
                {
                    _captureUntilMouseUp = true;
                    PedestrianCrossingToolkitState.ConfirmRemovalByAssetId(asset.Id);
                    ClearVanillaTargets(__instance);
                    _hoveredAssetId = 0;
                }

                return false;
            }
            catch (Exception exception)
            {
                bool suppressCurrentGesture = _captureUntilMouseUp;
                _operational = false;
                _captureUntilMouseUp = false;
                _hoveredAssetId = 0;
                Debug.LogError(
                    "[PedestrianCrossingToolkit] Vanilla Bulldoze crossing boundary disabled after an update failure; vanilla handling resumed: "
                    + exception);
                return !suppressCurrentGesture;
            }
        }

        private static bool TryGetCrossingUnderPointer(
            Camera camera,
            Vector2 screenPosition,
            out CrossingPlacementAsset asset)
        {
            if (CrossingPlacementRegistry.TryGetAssetNearScreen(
                    camera,
                    screenPosition,
                    PickRadiusPixels,
                    out asset))
            {
                return true;
            }

            ManagerCapacity.EnsureArrayCapacity(
                ref _accessPickBuffer,
                CrossingLandingConnectorPlanner.AccessAssetCount);
            int accessCount = CrossingLandingConnectorPlanner.CopyAccessAssetsTo(_accessPickBuffer);
            float bestDistanceSqr = PickRadiusPixels * PickRadiusPixels;
            int bestAssetId = 0;
            int max = Mathf.Min(accessCount, _accessPickBuffer.Length);
            for (int i = 0; i < max; i++)
            {
                CrossingLandingAccessAssetWorkOrder access = _accessPickBuffer[i];
                Vector3 first;
                Vector3 second;
                GetAccessFootprintSpan(access, out first, out second);
                float distanceSqr = CrossingPlacementRegistry.GetScreenSegmentDistanceSqr(
                    camera,
                    screenPosition,
                    first,
                    second);
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                bestAssetId = access.AssetId;
            }

            return bestAssetId != 0
                   && CrossingPlacementRegistry.TryGetAssetById(bestAssetId, out asset);
        }

        private static void ClearVanillaTargets(BulldozeTool tool)
        {
            _hoverInstanceField.SetValue(tool, InstanceID.Empty);
            _hoverInstance2Field.SetValue(tool, InstanceID.Empty);
            _lastInstanceField.SetValue(tool, InstanceID.Empty);
            _lastInstance2Field.SetValue(tool, InstanceID.Empty);
        }

        private static bool RenderOverlayPrefix(DefaultTool __instance)
        {
            if (!_operational || !(__instance is BulldozeTool))
                return true;

            if (!_captureUntilMouseUp && _hoveredAssetId == 0)
                return true;

            try
            {
                ClearVanillaTargets((BulldozeTool)__instance);
                return false;
            }
            catch (Exception exception)
            {
                _operational = false;
                _captureUntilMouseUp = false;
                _hoveredAssetId = 0;
                Debug.LogError(
                    "[PedestrianCrossingToolkit] Vanilla Bulldoze overlay boundary disabled after a render failure; vanilla rendering resumed: "
                    + exception);
                return true;
            }
        }

        private static void RenderOverlayPostfix(DefaultTool __instance, RenderManager.CameraInfo cameraInfo)
        {
            if (!_operational || !(__instance is BulldozeTool) || cameraInfo == null)
                return;

            CrossingPlacementAsset asset;
            if (!TryGetHoveredAsset(out asset))
                return;

            BuildingInfo selector = GetOrCreateSelectorPrefab();
            if (selector == null)
                return;

            Vector3 center;
            float angle;
            int width;
            ResolveSelectorFootprint(asset, out center, out angle, out width);
            selector.m_cellWidth = width;
            selector.m_cellLength = 1;
            selector.m_size = new Vector3(width * 8f, 1f, 8f);

            Color color = (Color)_getToolColorMethod.Invoke(
                __instance,
                new object[] { false, true });
            BuildingTool.RenderOverlay(cameraInfo, selector, 0, center, angle, color, false);
            RenderAccessFootprintOverlays(cameraInfo, selector, asset.Id, color);
        }

        private static void RenderAccessFootprintOverlays(
            RenderManager.CameraInfo cameraInfo,
            BuildingInfo selector,
            int assetId,
            Color color)
        {
            ManagerCapacity.EnsureArrayCapacity(
                ref _accessPickBuffer,
                CrossingLandingConnectorPlanner.AccessAssetCount);
            int accessCount = CrossingLandingConnectorPlanner.CopyAccessAssetsTo(_accessPickBuffer);
            int max = Mathf.Min(accessCount, _accessPickBuffer.Length);
            for (int i = 0; i < max; i++)
            {
                CrossingLandingAccessAssetWorkOrder access = _accessPickBuffer[i];
                if (access.AssetId != assetId)
                    continue;

                Vector3 first;
                Vector3 second;
                GetAccessFootprintSpan(access, out first, out second);
                RenderSelectorSpan(
                    cameraInfo,
                    selector,
                    first,
                    second,
                    Mathf.Max(1f, access.FootprintWidth),
                    color);
            }
        }

        private static void GetAccessFootprintSpan(
            CrossingLandingAccessAssetWorkOrder access,
            out Vector3 first,
            out Vector3 second)
        {
            first = access.DeckPosition;
            second = access.Position;
            Vector3 horizontal = second - first;
            horizontal.y = 0f;
            if (horizontal.sqrMagnitude > 0.25f)
                return;

            Vector3 direction = access.FacingDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
                direction = Vector3.forward;
            else
                direction.Normalize();

            first = access.Position;
            second = access.Position + direction * Mathf.Max(2f, access.FootprintLength);
        }

        private static void RenderSelectorSpan(
            RenderManager.CameraInfo cameraInfo,
            BuildingInfo selector,
            Vector3 first,
            Vector3 second,
            float footprintWidth,
            Color color)
        {
            Vector3 direction = second - first;
            direction.y = 0f;
            float length = direction.magnitude;
            if (length <= 0.1f)
                return;

            direction /= length;
            int cellsWide = Mathf.Clamp(Mathf.CeilToInt(length / 8f), 1, 16);
            int cellsLong = Mathf.Clamp(Mathf.CeilToInt(footprintWidth / 8f), 1, 16);
            selector.m_cellWidth = cellsWide;
            selector.m_cellLength = cellsLong;
            selector.m_size = new Vector3(cellsWide * 8f, 1f, cellsLong * 8f);
            Vector3 center = (first + second) * 0.5f;
            float angle = -Mathf.Atan2(direction.z, direction.x);
            BuildingTool.RenderOverlay(cameraInfo, selector, 0, center, angle, color, false);
        }

        private static BuildingInfo GetOrCreateSelectorPrefab()
        {
            if (_selectorPrefab != null)
                return _selectorPrefab;

            _selectorPrefabObject = new GameObject("PCT Bulldoze Selector");
            _selectorPrefabObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(_selectorPrefabObject);

            _selectorPrefab = _selectorPrefabObject.AddComponent<BuildingInfo>();
            BuildingAI ai = _selectorPrefabObject.AddComponent<BuildingAI>();
            _selectorPrefab.name = "PCT Bulldoze Selector";
            _selectorPrefab.m_cellWidth = 1;
            _selectorPrefab.m_cellLength = 1;
            _selectorPrefab.m_placementMode = BuildingInfo.PlacementMode.Roadside;
            _selectorPrefab.m_props = new BuildingInfo.Prop[0];
            _selectorPrefab.m_subMeshes = new BuildingInfo.MeshInfo[0];
            _selectorPrefab.m_subBuildings = new BuildingInfo.SubInfo[0];
            _selectorPrefab.m_paths = new BuildingInfo.PathInfo[0];
            _selectorPrefab.m_buildingAI = ai;
            ai.m_info = _selectorPrefab;
            return _selectorPrefab;
        }

        private static void ResolveSelectorFootprint(
            CrossingPlacementAsset asset,
            out Vector3 center,
            out float angle,
            out int width)
        {
            center = asset.Plan.IsValid
                ? asset.Plan.Center
                : asset.Placement.WorldPosition;
            Vector3 direction = Vector3.right;
            float span = 8f;
            if (asset.Plan.IsValid)
            {
                Vector3 across = asset.Plan.RightEdge - asset.Plan.LeftEdge;
                across.y = 0f;
                if (across.sqrMagnitude > 1f)
                {
                    span = across.magnitude;
                    direction = across / span;
                }
            }

            width = Mathf.Clamp(Mathf.CeilToInt(span / 8f), 1, 16);
            angle = -Mathf.Atan2(direction.z, direction.x);
        }

        private static void DestroySelectorPrefab()
        {
            _selectorPrefab = null;
            if (_selectorPrefabObject == null)
                return;

            UnityEngine.Object.Destroy(_selectorPrefabObject);
            _selectorPrefabObject = null;
        }
    }
}
