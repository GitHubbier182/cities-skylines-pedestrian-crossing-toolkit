using System;
using ColossalFramework.UI;
using ICities;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public class PedestrianCrossingToolkitMod : IUserMod
    {
        public string Name => "Pedestrian Crossing Toolkit";
        public string Description => "Adds a foundation for mid-block crossings, controlled pedestrian crossings, and compact subway links.";
    }

    public class PedestrianCrossingToolkitLoading : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);

            if (mode != LoadMode.LoadGame && mode != LoadMode.NewGame)
                return;

            PedestrianCrossingLog.Initialize();
            PedestrianCrossingToolkitState.Enabled = true;
            PedestrianCrossingPrefabCatalog.Refresh("level-loaded");
            CrossingPlacementRegistry.RebuildPlans();
            CrossingApplicationEngine.Refresh("level-loaded");
            RoadPlacementRules.ForceRefreshVanillaCrossingCache("level-loaded");
            if (CrossingPathExecutionBoundary.LivePathCreationEnabled || PedestrianCrossingLog.VerboseDiagnostics)
                CrossingPathExecutionBoundary.Sync("level-loaded");
            else
                CrossingPathExecutionBoundary.Reset();
            PedestrianCrossingToolkitState.ScheduleBuiltStructureRebuildOnLoad();

            UIView view = UIView.GetAView();
            if (view != null)
            {
                PedestrianCrossingToolkitPanel.CreateIfNeeded(view);
                PedestrianCrossingToolkitLauncherButton.CreateIfNeeded(view);
                CrossingAppliedOverlay.CreateIfNeeded(view);
            }

            Debug.Log("[PedestrianCrossingToolkit] Enabled. Connector-based crossing tools loaded.");
        }

        public override void OnLevelUnloading()
        {
            base.OnLevelUnloading();

            PedestrianCrossingToolkitState.ResetForLevelUnload();
            CrossingAppliedOverlay.DestroyInstance();
            PedestrianCrossingToolkitPanel.DestroyInstance();
            PedestrianCrossingToolkitLauncherButton.DestroyInstance();

            Debug.Log("[PedestrianCrossingToolkit] Disabled.");
            PedestrianCrossingLog.Shutdown();
        }
    }

    public class PedestrianCrossingToolkitThreading : ThreadingExtensionBase
    {
        private const float MaxSignalControllerRealDelta = 0.25f;
        private const float SuppressionRefreshRealSeconds = 10f;
        private float _suppressionRefreshTimer;

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            base.OnUpdate(realTimeDelta, simulationTimeDelta);

            if (!PedestrianCrossingToolkitState.Enabled)
                return;

            PedestrianCrossingToolkitState.ProcessDeferredLoadWork(realTimeDelta);
            PedestrianCrossingToolkitState.ProcessAutoScanObservation(realTimeDelta);
            PedestrianCrossingToolkitState.ProcessNetworkDependencyChanges(realTimeDelta);
            RoadPlacementRules.UpdateVanillaCrossingCache(realTimeDelta);
            CrossingPathBuilder.UpdateSignalControllers(GetSignalControllerDelta(realTimeDelta));
            _suppressionRefreshTimer += realTimeDelta;
            if (_suppressionRefreshTimer >= SuppressionRefreshRealSeconds)
            {
                _suppressionRefreshTimer = 0f;
                CrossingPathBuilder.MaintainSuppressedSurfaceCrossings();
            }
        }

        private static float GetSignalControllerDelta(float realTimeDelta)
        {
            SimulationManager simulationManager = SimulationManager.instance;
            if (simulationManager != null && simulationManager.SimulationPaused)
                return 0f;

            return Mathf.Clamp(realTimeDelta, 0f, MaxSignalControllerRealDelta);
        }

        public override void OnAfterSimulationFrame()
        {
            base.OnAfterSimulationFrame();

            if (!PedestrianCrossingToolkitState.Enabled)
                return;

            CrossingPathBuilder.ReapplySignalControllerStates();
        }
    }

    public class PedestrianCrossingToolkitSerializable : SerializableDataExtensionBase
    {
        private const string DataId = "PedestrianCrossingToolkit.PendingAssets.v1";

        public override void OnLoadData()
        {
            base.OnLoadData();

            PedestrianCrossingLog.Initialize();
            try
            {
                byte[] data = serializableDataManager.LoadData(DataId);
                if (data == null || data.Length == 0)
                {
                    Debug.Log("[PedestrianCrossingToolkit] No saved pending crossings found.");
                    return;
                }

                int count = CrossingPlacementRegistry.Restore(data);
                Debug.Log("[PedestrianCrossingToolkit] Restored pending crossings: count=" + count);
            }
            catch (Exception e)
            {
                Debug.LogError("[PedestrianCrossingToolkit] Failed to restore pending crossings: " + e);
            }
        }

        public override void OnSaveData()
        {
            base.OnSaveData();

            try
            {
                byte[] data = CrossingPlacementRegistry.Serialize();
                serializableDataManager.SaveData(DataId, data);
                Debug.Log("[PedestrianCrossingToolkit] Saved pending crossings: count="
                          + CrossingPlacementRegistry.Count
                          + " autoRebuildBuiltStructures="
                          + CrossingPlacementRegistry.AutoRebuildBuiltStructures);
            }
            catch (Exception e)
            {
                Debug.LogError("[PedestrianCrossingToolkit] Failed to save pending crossings: " + e);
            }
        }
    }
}
