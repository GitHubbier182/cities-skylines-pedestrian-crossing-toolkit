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

        public void OnSettingsUI(UIHelperBase helper)
        {
            UIHelperBase management = helper.AddGroup("Crossing Management");
            management.AddButton(
                "Clear All Crossings",
                PedestrianCrossingToolkitPanel.RequestClearAllCrossingsFromOptions);

            UIHelperBase diagnostics = helper.AddGroup("Diagnostics");
            diagnostics.AddCheckbox(
                "Enable advanced logs",
                PedestrianCrossingLog.AdvancedDiagnostics,
                delegate(bool value)
                {
                    PedestrianCrossingLog.AdvancedDiagnostics = value;
                });

        }
    }

    public class PedestrianCrossingToolkitLoading : LoadingExtensionBase
    {
        private static bool _releaseNoticePending;

        internal static void ProcessReleaseNotice()
        {
            if (!_releaseNoticePending)
                return;
            UIView view = UIView.GetAView();
            if (view == null)
                return;
            Transform root = view.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                UIPanel panel = root.GetChild(i).GetComponent<UIPanel>();
                if (panel != null && panel.name == "ScratchyBaldOneTimeUpdateNoticePanel")
                    return;
            }
            _releaseNoticePending = false;
            OneTimeUpdateNoticePanel.ShowIfNeeded(view, ReleaseNotice);
        }

        private static readonly ReleaseNoticeContent ReleaseNotice = new ReleaseNoticeContent(
            "PedestrianCrossingToolkit.ShownReleaseNoticeId",
            "v2.1.1",
            "Pedestrian Crossing Toolkit 2.1.1",
            "Lower crossing-management overhead",
            string.Empty,
            "PCT",
            new[]
            {
                "Removes a repeated UI search from normal crossing hover checks, targeting the frame-rate loss that continued even away from crossings.",
                "Combines compatible opaque crossing details into fewer local renderers while retaining the complete structures, transparent pieces and weather-responsive roofs.",
                "Reduces background crossing-check overhead in cities with many PCT crossings, while keeping road-change detection and crossing protection active.",
                "Streamlines signal scheduling while retaining demand priority and waiting for pedestrians to clear before releasing vehicles.",
                "Reuses crossing-highlight drawing buffers while you move the camera and skips unchanged weather-surface updates.",
                "Keeps large marker displays complete, releases temporary drawing meshes and avoids rebuilding unchanged subway route previews when the camera moves.",
                "Preserves bridge access orientation, pending crossing cleanup and original crossing permissions across new saves, with safer road-replacement and signal-state recovery.",
                "Corrects road-join traversal after road edits, refreshes placement guides reliably and reports incomplete crossing construction.",
                "Uses local searches for nearby subway entrances and continues long pedestrian-route snapshots in small Auto Scan steps."
            },
            true,
            string.Empty,
            null,
            new[]
            {
                new ReleaseNoticeVersion("v2.1.0", "3 September 2026, 19:28 BST", new[]
                {
                    "Hover over a PCT crossing with the normal city tool to see its cyan outline, then click it to open that crossing's details—even when Roads > Crossing is closed and no information view is active.",
                    "Only the crossing you select keeps a detail panel, avoiding the clutter and frame-rate cost of automatically updating panels for every visible crossing.",
                    "Selection clears naturally when you click elsewhere, right-click, press Escape, choose another tool or remove the crossing.",
                    "Auto Scan now reads active walking routes once and gives busy junction arms first consideration for pedestrian bridges or subways before it considers other crossing improvements.",
                    "Every eligible arm can qualify even without a vanilla crossing marker. Different arms at the same junction can be chosen, while duplicate arms remain blocked.",
                    "Signals do not use up the junction bridge/subway allowance, and standard crossings stay at least 250 units from vanilla crossings, existing PCT crossings and earlier suggestions.",
                    "Auto Scan no longer waits for a one-minute observation period. It prepares candidates and current demand in small batches, then immediately shows the complete preview or applies the result."
                }, true),
                new ReleaseNoticeVersion("v2.0.3", "12 August 2026, 21:29 BST", new[]
                {
                    "Improves support logging, protected Bulldoze selection, Crossing-tab lifecycle boundaries and paced signal processing.",
                    "Adds advanced-only frame-rate and callback timing evidence for Crossing-tab performance reports."
                }, true),
                new ReleaseNoticeVersion("v2.0.2", "10 August 2026, 16:49 BST", new[]
                {
                    "Generated crossing presentation and automatic network checks remain lightweight as the number of saved crossings grows."
                }, true),
                new ReleaseNoticeVersion("v2.0.1", "6 August 2026, 22:49 BST", new[]
                {
                    "Roads > Crossing now stays responsive in heavily modded cities and remains confined to its selected Roads tab."
                }, true),
                new ReleaseNoticeVersion("v2.0.0", "2 August 2026, 16:50 BST", new[]
                {
                    "The floating PCT Tool and its launcher are removed: Standard, Signalled, Auto Subway, Manual Subway, Bridge and Auto Scan are now in Roads > Crossing.",
                    "Inspect Crossing is now automatic while Roads > Crossing is open, showing crossing types from city scale and details or live signal phases when zoomed in.",
                    "Use vanilla Bulldoze to remove one PCT crossing without demolishing its road; confirmed Clear All Crossings is now in PCT Options.",
                    "The old manual validation action is replaced by scheduled read-only checks that warn you and mark crossings needing attention without changing them.",
                    "Auto Scan asks whether to preview: Reject, Apply and Cancel appear for a preview, while direct apply needs no separate PCT Tool.",
                    "Auto Scan significantly improved so it's faster, more accurate and creates more crossings in one pass."
                }, true),
                new ReleaseNoticeVersion("v1.3.0", "29 July 2026, 02:06 BST", new[]
                {
                    "Expanded-capacity cities no longer become stuck during load or crossing scans.",
                    "Auto Scan observes real crossings, prioritises busy junctions and keeps automatic crossings spaced.",
                    "Supported road upgrades preserve standard, signal, subway and bridge crossings."
                }, true),
                new ReleaseNoticeVersion("v1.2.0", "10 July 2026, 22:51 BST", new[]
                {
                    "Improves bridge, subway entrance and standard crossing visuals with more detailed structures and markings."
                }, true),
                new ReleaseNoticeVersion("v1.1.1", "16 June 2026, 23:03 BST", new string[0], true),
                new ReleaseNoticeVersion("v1.1.0", "12 June 2026, 22:57 BST", new[]
                {
                    "Adds optional Auto Scan preview, review, reject, apply and cancel controls.",
                    "Improves suggestions for long roads, busy crossings and junction throats."
                }, true),
                new ReleaseNoticeVersion("v1.0.4", "7 June 2026, 23:53 BST", new[]
                {
                    "Improves UnifiedUI launcher compatibility and surface markings.",
                    "Hardens unload, reset and Clear All cleanup."
                }, true),
                new ReleaseNoticeVersion("v1.0.3", "7 June 2026, 02:13 BST", new[]
                {
                    "Standard zebra crossings use worn, semi-transparent road-marking paint.",
                    "Generated crossing cleanup is more reliable."
                }, true),
                new ReleaseNoticeVersion("v1.0.2", "4 June 2026, 13:15 BST", new[]
                {
                    "Road upgrades remove only crossings on the road actually replaced.",
                    "Improves large-city placement and deletion responsiveness.",
                    "Manual Subway routes can share an existing entrance."
                }, true),
                new ReleaseNoticeVersion("v1.0.1", "3 June 2026, 00:37 BST", new[]
                {
                    "Avoids Network Anarchy side effects and fixes junction crossing placement."
                }, true),
                new ReleaseNoticeVersion("v1.0.0", "29 May 2026, 14:04 BST", new[]
                {
                    "Initial release: place surface, signal, subway and bridge pedestrian crossings."
                }, false)
            });

        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);

            if (mode != LoadMode.LoadGame && mode != LoadMode.NewGame)
                return;

            PedestrianCrossingLog.Initialize();
            PedestrianCrossingToolkitState.Enabled = true;
            PedestrianCrossingBulldozeHarmony.Apply();
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

            // The Toolkit now lives entirely in Roads > Crossing and PCT Options.
            // Release any launcher or legacy floating panel left by an earlier load.
            PedestrianCrossingToolkitPanel.DestroyInstance();
            PedestrianCrossingToolkitLauncherButton.DestroyInstance();

            UIView view = UIView.GetAView();
            if (view != null)
            {
                PedestrianCrossingRoadsTab.CreateIfNeeded(view);
                CrossingAppliedOverlay.CreateIfNeeded(view);
                _releaseNoticePending = true;
            }

            PedestrianCrossingLog.UnityInfo(
                "Enabled. Connector-based crossing tools loaded; advancedDiagnostics=" +
                PedestrianCrossingLog.AdvancedDiagnostics + ".");
        }

        public override void OnLevelUnloading()
        {
            base.OnLevelUnloading();

            _releaseNoticePending = false;
            PedestrianCrossingScanCoordinator.Shutdown();
            PedestrianCrossingBulldozeHarmony.Unpatch();
            PedestrianCrossingToolkitThreading.ClearMainThreadActions();
            PedestrianCrossingToolkitState.ResetForLevelUnload();
            OneTimeUpdateNoticePanel.DestroyInstance();
            PedestrianCrossingAutoScanProgressPanel.DestroyInstance();
            PedestrianCrossingAutoScanInstructionsPanel.DestroyInstance();
            CrossingAppliedOverlay.DestroyInstance();
            PedestrianCrossingRoadsTab.DestroyInstance();
            PedestrianCrossingPerformanceDiagnostics.Reset();
            PedestrianCrossingToolkitPanel.DestroyInstance();
            PedestrianCrossingToolkitLauncherButton.DestroyInstance();

            PedestrianCrossingLog.UnityInfo("Disabled.");
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

            long diagnosticsStartedAt =
                PedestrianCrossingPerformanceDiagnostics.BeginCallbackSample();
            try
            {
                ProcessMainThreadActions();
                if (!PedestrianCrossingToolkitState.Enabled)
                    return;

                PedestrianCrossingToolkitLoading.ProcessReleaseNotice();
                PedestrianCrossingToolkitState.ProcessDeferredLoadWork(realTimeDelta);
                PedestrianCrossingToolkitState.ProcessAutoScanObservation(realTimeDelta);
                PedestrianCrossingToolkitState.ProcessNetworkDependencyChanges(realTimeDelta);
                PedestrianCrossingToolkitState.ProcessScheduledCrossingValidation();
                RoadPlacementRules.UpdateVanillaCrossingCache(realTimeDelta);
                CrossingPathBuilder.UpdateGeneratedSurfacePresentation();
                CrossingPathBuilder.UpdateSignalControllers(GetSignalControllerDelta(realTimeDelta));
            }
            finally
            {
                PedestrianCrossingPerformanceDiagnostics.EndThreadingUpdate(
                    diagnosticsStartedAt);
            }
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

            CrossingPathBuilder.ProcessDeferredNetworkRelease();
            CrossingPathBuilder.ReapplySignalControllerStates();
        }
    }

    public class PedestrianCrossingToolkitSerializable : SerializableDataExtensionBase
    {
        private const string DataId = "PedestrianCrossingToolkit.PendingAssets.v1";
        private const string SuppressionDataId = "PedestrianCrossingToolkit.CrossingPermissions.v1";
        private const string CleanupDataId = "PedestrianCrossingToolkit.PendingPathCleanup.v1";

        public override void OnLoadData()
        {
            base.OnLoadData();

            PedestrianCrossingLog.Initialize();
            try
            {
                byte[] data = serializableDataManager.LoadData(DataId);
                if (data == null || data.Length == 0)
                {
                    PedestrianCrossingLog.Advanced("[PedestrianCrossingToolkit] No saved pending crossings found.");
                }
                else
                {
                    int count = CrossingPlacementRegistry.Restore(data);
                    PedestrianCrossingLog.Advanced("[PedestrianCrossingToolkit] Restored pending crossings: count=" + count);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[PedestrianCrossingToolkit] Failed to restore pending crossings: " + e);
            }
            try
            {
                CrossingPathBuilder.RestoreDeferredNetworkReleases(
                    serializableDataManager.LoadData(CleanupDataId));
            }
            catch (Exception e)
            {
                Debug.LogError("[PedestrianCrossingToolkit] Failed to restore pending path cleanup; native records retained: " + e);
            }
            try
            {
                GradeSeparatedVanillaCrossingSuppression.RestoreSavedPermissions(
                    serializableDataManager.LoadData(SuppressionDataId));
            }
            catch (Exception e)
            {
                Debug.LogError("[PedestrianCrossingToolkit] Failed to restore original crossing permissions: " + e);
            }
        }

        public override void OnSaveData()
        {
            base.OnSaveData();

            try
            {
                serializableDataManager.SaveData(SuppressionDataId,
                    GradeSeparatedVanillaCrossingSuppression.SerializePermissions());
                byte[] data = CrossingPlacementRegistry.Serialize();
                serializableDataManager.SaveData(DataId, data);
                serializableDataManager.SaveData(CleanupDataId,
                    CrossingPathBuilder.SerializeDeferredNetworkReleases());
                PedestrianCrossingLog.Advanced("[PedestrianCrossingToolkit] Saved pending crossings: count="
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
