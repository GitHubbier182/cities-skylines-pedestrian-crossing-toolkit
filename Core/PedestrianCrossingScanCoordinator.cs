using System;
using ScratchyBald.CitiesSkylines.Shared;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    internal static class PedestrianCrossingScanCoordinator
    {
        public const string OwnerId = "PedestrianCrossingToolkit";

        private const string AutoScanRequestId = "auto-scan-observation";
        private const string LoadRebuildRequestId = "saved-crossing-rehydration";
        private const string ValidationRequestId = "scheduled-crossing-validation";

        private static string _autoScanTicket;
        private static string _loadRebuildTicket;
        private static string _validationTicket;
        private static bool _available;
        private static bool _failureLogged;

        public static void Initialize()
        {
            Shutdown();
            _failureLogged = false;
            try
            {
                ScratchysScanManager.Initialize(
                    OwnerId,
                    delegate
                    {
                        return PedestrianCrossingLog.AdvancedDiagnostics;
                    });
                _available = true;
                PedestrianCrossingLog.UnityInfo(
                    "Scratchy's Scan Manager"
                    + " registered; Auto Scan observation, saved-crossing"
                    + " rehydration and scheduled read-only validation will"
                    + " use cooperative main-thread requests.");
            }
            catch (Exception exception)
            {
                _available = false;
                LogFallback("initialization failed", exception);
            }
        }

        public static bool TryQueueAutoScan(
            Func<bool> step,
            Action completed,
            Action<Exception> failed)
        {
            if (!_available || step == null)
                return false;

            Cancel(ref _autoScanTicket, "Auto Scan");
            try
            {
                _autoScanTicket = ScratchysScanManager.QueueMainThreadScan(
                    OwnerId,
                    AutoScanRequestId,
                    ScratchysScanManager.PlayerRequestedPriority,
                    step,
                    delegate
                    {
                        _autoScanTicket = null;
                        if (completed != null)
                            completed();
                    },
                    delegate(Exception exception)
                    {
                        _autoScanTicket = null;
                        if (failed != null)
                            failed(exception);
                    });
                return !string.IsNullOrEmpty(_autoScanTicket);
            }
            catch (Exception exception)
            {
                _available = false;
                LogFallback("Auto Scan request submission failed", exception);
                return false;
            }
        }

        public static bool TryQueueLoadRebuild(
            Func<bool> step,
            Action completed,
            Action<Exception> failed)
        {
            if (!_available || step == null)
                return false;

            Cancel(ref _loadRebuildTicket, "saved-crossing rehydration");
            try
            {
                _loadRebuildTicket = ScratchysScanManager.QueueMainThreadScan(
                    OwnerId,
                    LoadRebuildRequestId,
                    ScratchysScanManager.StartupPriority,
                    step,
                    delegate
                    {
                        _loadRebuildTicket = null;
                        if (completed != null)
                            completed();
                    },
                    delegate(Exception exception)
                    {
                        _loadRebuildTicket = null;
                        if (failed != null)
                            failed(exception);
                    });
                return !string.IsNullOrEmpty(_loadRebuildTicket);
            }
            catch (Exception exception)
            {
                _available = false;
                LogFallback(
                    "saved-crossing rehydration request submission failed",
                    exception);
                return false;
            }
        }

        public static bool TryQueueScheduledValidation(
            Func<bool> step,
            Action completed,
            Action<Exception> failed)
        {
            if (!_available || step == null)
                return false;

            Cancel(ref _validationTicket, "scheduled crossing validation");
            try
            {
                _validationTicket = ScratchysScanManager.QueueMainThreadScan(
                    OwnerId,
                    ValidationRequestId,
                    ScratchysScanManager.BackgroundPriority,
                    step,
                    delegate
                    {
                        _validationTicket = null;
                        if (completed != null)
                            completed();
                    },
                    delegate(Exception exception)
                    {
                        _validationTicket = null;
                        if (failed != null)
                            failed(exception);
                    });
                return !string.IsNullOrEmpty(_validationTicket);
            }
            catch (Exception exception)
            {
                _available = false;
                LogFallback("scheduled validation request submission failed", exception);
                return false;
            }
        }

        public static void Shutdown()
        {
            if (_available)
            {
                try
                {
                    ScratchysScanManager.CancelOwner(OwnerId);
                }
                catch (Exception exception)
                {
                    LogFallback("level-unload cancellation failed", exception);
                }
            }

            _autoScanTicket = null;
            _loadRebuildTicket = null;
            _validationTicket = null;
            _available = false;
        }

        private static void Cancel(ref string ticket, string operation)
        {
            if (string.IsNullOrEmpty(ticket))
                return;

            try
            {
                ScratchysScanManager.Cancel(ticket);
            }
            catch (Exception exception)
            {
                LogFallback(operation + " cancellation failed", exception);
            }

            ticket = null;
        }

        private static void LogFallback(string operation, Exception exception)
        {
            if (_failureLogged)
                return;

            _failureLogged = true;
            Debug.LogWarning(
                "[PedestrianCrossingToolkit] Scratchy's Scan Manager "
                + operation
                + "; PCT will preserve its existing local main-thread"
                + " scheduler. exception="
                + exception);
        }
    }
}
