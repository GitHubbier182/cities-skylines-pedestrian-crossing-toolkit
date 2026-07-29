using System;
using System.Collections.Generic;
using ColossalFramework.UI;
using ICities;
using ScratchyBald.CitiesSkylines.UI;
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
        private static readonly ReleaseNoticeContent ReleaseNotice = new ReleaseNoticeContent(
            "PedestrianCrossingToolkit.ShownReleaseNoticeId",
            "v1.3.0",
            "Pedestrian Crossing Toolkit 1.3.0",
            "Faster large cities and safe road upgrades",
            "Everything added since the released v1.2.0:",
            "PCT",
            new[]
            {
                "Expanded-capacity cities remain responsive during loading and crossing scans.",
                "Auto Scan watches pedestrians for one minute, prioritises busy junctions and useful upgrades, keeps straight-road crossings sparse and can preview up to 50 suggestions.",
                "The Toolkit minimises to 'Monitoring your city' during a scan, then returns with results or preview guidance and can recommend another pass.",
                "Supported road-upgrade mods can preserve standard, signalled, subway and bridge crossings when replacing roads.",
                "Under the hood improvements for better reliability and maintainability.",
                "Significantly improved game lag / slowness issues with a new shared resource for Scratchy's mods."
            },
            true,
            string.Empty,
            null);

        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);

            if (mode != LoadMode.LoadGame && mode != LoadMode.NewGame)
                return;

            PedestrianCrossingLog.Initialize();
            PedestrianCrossingToolkitState.Enabled = true;
            PedestrianCrossingScanCoordinator.Initialize();
            PedestrianCrossingPrefabCatalog.Refresh("level-loaded");
            CrossingPlacementRegistry.RebuildPlans();
            CrossingApplicationEngine.Refresh("level-loaded");
            RoadPlacementRules.RequestVanillaCrossingCacheRefresh("level-loaded");
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
                OneTimeUpdateNoticePanel.ShowIfNeeded(view, ReleaseNotice);
            }

            Debug.Log("[PedestrianCrossingToolkit] Enabled. Connector-based crossing tools loaded.");
        }

        public override void OnLevelUnloading()
        {
            base.OnLevelUnloading();

            PedestrianCrossingScanCoordinator.Shutdown();
            PedestrianCrossingToolkitThreading.ClearMainThreadActions();
            PedestrianCrossingToolkitState.ResetForLevelUnload();
            OneTimeUpdateNoticePanel.DestroyInstance();
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
        private static readonly Queue<Action> MainThreadActions = new Queue<Action>();

        internal static void QueueMainThreadAction(Action action)
        {
            if (action == null)
                return;

            lock (MainThreadActions)
                MainThreadActions.Enqueue(action);
        }

        internal static void ClearMainThreadActions()
        {
            lock (MainThreadActions)
                MainThreadActions.Clear();
        }

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            base.OnUpdate(realTimeDelta, simulationTimeDelta);

            ProcessMainThreadActions();
            if (!PedestrianCrossingToolkitState.Enabled)
                return;

            PedestrianCrossingToolkitState.ProcessDeferredLoadWork(realTimeDelta);
            PedestrianCrossingToolkitState.ProcessAutoScanObservation(realTimeDelta);
            PedestrianCrossingToolkitState.ProcessNetworkDependencyChanges(realTimeDelta);
            RoadPlacementRules.UpdateVanillaCrossingCache(realTimeDelta);
            CrossingPathBuilder.UpdateSignalControllers(GetSignalControllerDelta(realTimeDelta));
        }

        private static void ProcessMainThreadActions()
        {
            while (true)
            {
                Action action;
                lock (MainThreadActions)
                {
                    if (MainThreadActions.Count == 0)
                        return;

                    action = MainThreadActions.Dequeue();
                }

                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogError("[PedestrianCrossingToolkit] Main-thread action failed: " + e);
                }
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
