using System;
using System.IO;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PedestrianCrossingToolkit
{
    public static class PedestrianCrossingLog
    {
        // Development-only execution boundary; never bind this to a player setting.
        public static readonly bool VerboseDiagnostics = false;
        private const string AdvancedDiagnosticsKey =
            "PedestrianCrossingToolkit.AdvancedDiagnostics";
        private const string Prefix = "[PedestrianCrossingToolkit]";
        private const string FileName = "PedestrianCrossingToolkit.log";
        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static string _logPath;
        private static StreamWriter _writer;
        private static int _linesSinceFlush;
        private static DateTime _lastFlushUtc;
        private static bool? _advancedDiagnostics;

        public static bool AdvancedDiagnostics
        {
            get
            {
                if (!_advancedDiagnostics.HasValue)
                {
                    _advancedDiagnostics =
                        PlayerPrefs.GetInt(AdvancedDiagnosticsKey, 0) != 0;
                }

                return _advancedDiagnostics.Value;
            }
            set
            {
                bool changed = AdvancedDiagnostics != value;
                _advancedDiagnostics = value;
                PlayerPrefs.SetInt(AdvancedDiagnosticsKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (changed)
                {
                    UnityInfo(
                        "Advanced diagnostics " +
                        (value ? "enabled." : "disabled."));
                }
            }
        }

        public static string LogPath
        {
            get
            {
                EnsurePath();
                return _logPath;
            }
        }

        public static void Initialize()
        {
            lock (SyncRoot)
            {
                if (_initialized)
                    return;

                EnsurePath();
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_logPath));
                    FileStream stream = new FileStream(_logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    _writer = new StreamWriter(stream);
                    _writer.WriteLine(
                        "Pedestrian Crossing Toolkit log started "
                        + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    _writer.Flush();
                    _lastFlushUtc = DateTime.UtcNow;
                    _linesSinceFlush = 0;
                    Application.logMessageReceived += OnUnityLogMessage;
                    _initialized = true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning(Prefix + " Dedicated log unavailable: " + e.Message);
                }
            }
        }

        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                if (!_initialized)
                    return;

                Application.logMessageReceived -= OnUnityLogMessage;
                AppendLine("Info", "Dedicated log closed.");
                if (_writer != null)
                {
                    _writer.Flush();
                    _writer.Dispose();
                    _writer = null;
                }
                _initialized = false;
            }
        }

        public static void Info(string message)
        {
            AppendLine("Info", FormatMessage(message));
        }

        public static void Advanced(string message)
        {
            if (AdvancedDiagnostics)
                Debug.Log(FormatMessage(message));
        }

        public static void AdvancedWarning(string message)
        {
            if (AdvancedDiagnostics)
                Debug.LogWarning(FormatMessage(message));
        }

        public static void Warning(string message)
        {
            Debug.LogWarning(FormatMessage(message));
        }

        public static void UnityInfo(string message)
        {
            Debug.Log(FormatMessage(message));
        }

        private static void OnUnityLogMessage(string condition, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(condition) || !condition.StartsWith(Prefix, StringComparison.Ordinal))
                return;

            AppendLine(type.ToString(), condition);
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                && !string.IsNullOrEmpty(stackTrace))
            {
                AppendLine("Stack", stackTrace);
            }
        }

        private static void AppendLine(string level, string message)
        {
            lock (SyncRoot)
            {
                try
                {
                    if (_writer == null)
                        return;

                    _writer.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff")
                                      + " "
                                      + level
                                      + ": "
                                      + message);
                    _linesSinceFlush++;
                    DateTime now = DateTime.UtcNow;
                    if (_linesSinceFlush >= 32
                        || (now - _lastFlushUtc).TotalSeconds >= 1d
                        || string.Equals(level, LogType.Error.ToString(), StringComparison.Ordinal)
                        || string.Equals(level, LogType.Exception.ToString(), StringComparison.Ordinal))
                    {
                        _writer.Flush();
                        _linesSinceFlush = 0;
                        _lastFlushUtc = now;
                    }
                }
                catch
                {
                }
            }
        }

        private static string FormatMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return Prefix;

            return message.StartsWith(Prefix, StringComparison.Ordinal)
                ? message
                : Prefix + " " + message;
        }

        private static void EnsurePath()
        {
            if (!string.IsNullOrEmpty(_logPath))
                return;

            string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            string logsPath = Path.Combine(Path.Combine(Path.Combine(home, "Library"), "Logs"), "Unity");
            _logPath = Path.Combine(logsPath, FileName);
        }
    }

    internal static class PedestrianCrossingPerformanceDiagnostics
    {
        private const float OpenWindowSeconds = 5f;
        private const float PostCloseWindowSeconds = 5f;
        private const float ClosedBaselineSeconds = 10f;
        private const float MinimumCurrentBaselineSeconds = 1f;

        private struct TimingAccumulator
        {
            internal int Count;
            internal double TotalMs;
            internal double WorstMs;

            internal void Record(double elapsedMs)
            {
                Count++;
                TotalMs += elapsedMs;
                if (elapsedMs > WorstMs)
                    WorstMs = elapsedMs;
            }
        }

        private struct WindowSnapshot
        {
            internal float ElapsedSeconds;
            internal int FrameCount;
            internal double FrameTotalMs;
            internal double WorstFrameMs;
            internal TimingAccumulator RoadsTabUpdate;
            internal TimingAccumulator OverlayUpdate;
            internal TimingAccumulator OverlayRepaint;
            internal TimingAccumulator ThreadingUpdate;
        }

        private static bool _armed;
        private static bool _tabOpen;
        private static float _windowElapsedSeconds;
        private static int _frameCount;
        private static double _frameTotalMs;
        private static double _worstFrameMs;
        private static TimingAccumulator _roadsTabUpdate;
        private static TimingAccumulator _overlayUpdate;
        private static TimingAccumulator _overlayRepaint;
        private static TimingAccumulator _threadingUpdate;
        private static WindowSnapshot _lastClosedBaseline;
        private static bool _hasLastClosedBaseline;
        private static bool _awaitingPostCloseSample;

        internal static long BeginCallbackSample()
        {
            return PedestrianCrossingLog.AdvancedDiagnostics
                ? Stopwatch.GetTimestamp()
                : 0L;
        }

        internal static void EndRoadsTabUpdate(long startedAt)
        {
            RecordTiming(ref _roadsTabUpdate, startedAt);
        }

        internal static void EndOverlayUpdate(long startedAt)
        {
            RecordTiming(ref _overlayUpdate, startedAt);
        }

        internal static void EndOverlayRepaint(long startedAt)
        {
            RecordTiming(ref _overlayRepaint, startedAt);
        }

        internal static void EndThreadingUpdate(long startedAt)
        {
            RecordTiming(ref _threadingUpdate, startedAt);
        }

        internal static void RecordCrossingTabClick(int tabIndex)
        {
            if (!PedestrianCrossingLog.AdvancedDiagnostics)
                return;

            PedestrianCrossingLog.Advanced(
                "[PedestrianCrossingToolkit] Crossing-tab performance event: click tabIndex="
                + tabIndex
                + " selectedIndex="
                + GetSelectedRoadTabIndex()
                + " crossings="
                + CrossingPlacementRegistry.Count
                + " activeMode="
                + PedestrianCrossingToolkitState.ActiveMode
                + ".");
        }

        internal static void ObserveRenderedFrame(bool tabOpen)
        {
            if (!PedestrianCrossingLog.AdvancedDiagnostics)
            {
                if (_armed)
                    ResetAll();
                return;
            }

            if (!_armed)
            {
                _armed = true;
                _tabOpen = tabOpen;
                ResetWindow();
                PedestrianCrossingLog.Advanced(
                    "[PedestrianCrossingToolkit] Crossing-tab performance diagnostics armed: initialOpen="
                    + tabOpen
                    + " closedBaselineSeconds="
                    + ClosedBaselineSeconds.ToString("0")
                    + " openWindowSeconds="
                    + OpenWindowSeconds.ToString("0")
                    + ".");
            }

            if (_tabOpen != tabOpen)
            {
                if (_tabOpen)
                {
                    LogSnapshot("open-final", CaptureWindow(), true);
                    _awaitingPostCloseSample = true;
                }
                else
                {
                    WindowSnapshot baseline = _windowElapsedSeconds >= MinimumCurrentBaselineSeconds
                        ? CaptureWindow()
                        : (_hasLastClosedBaseline ? _lastClosedBaseline : CaptureWindow());
                    LogSnapshot("pre-open-baseline", baseline, false);
                }

                PedestrianCrossingLog.Advanced(
                    "[PedestrianCrossingToolkit] Crossing-tab performance transition: open="
                    + tabOpen
                    + " crossings="
                    + CrossingPlacementRegistry.Count
                    + " activeMode="
                    + PedestrianCrossingToolkitState.ActiveMode
                    + ".");
                _tabOpen = tabOpen;
                ResetWindow();
            }

            float frameSeconds = Time.unscaledDeltaTime;
            if (frameSeconds > 0f
                && !float.IsNaN(frameSeconds)
                && !float.IsInfinity(frameSeconds))
            {
                double frameMs = frameSeconds * 1000d;
                _windowElapsedSeconds += frameSeconds;
                _frameCount++;
                _frameTotalMs += frameMs;
                if (frameMs > _worstFrameMs)
                    _worstFrameMs = frameMs;
            }

            if (_tabOpen && _windowElapsedSeconds >= OpenWindowSeconds)
            {
                LogSnapshot("open-window", CaptureWindow(), true);
                ResetWindow();
            }
            else if (!_tabOpen
                     && _awaitingPostCloseSample
                     && _windowElapsedSeconds >= PostCloseWindowSeconds)
            {
                WindowSnapshot postClose = CaptureWindow();
                LogSnapshot("post-close-window", postClose, false);
                _lastClosedBaseline = postClose;
                _hasLastClosedBaseline = true;
                _awaitingPostCloseSample = false;
                ResetWindow();
            }
            else if (!_tabOpen && _windowElapsedSeconds >= ClosedBaselineSeconds)
            {
                _lastClosedBaseline = CaptureWindow();
                _hasLastClosedBaseline = true;
                ResetWindow();
            }
        }

        internal static void Reset()
        {
            ResetAll();
        }

        private static void RecordTiming(ref TimingAccumulator accumulator, long startedAt)
        {
            if (startedAt == 0L || !PedestrianCrossingLog.AdvancedDiagnostics)
                return;

            long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
            if (elapsedTicks < 0L)
                return;

            accumulator.Record(elapsedTicks * 1000d / Stopwatch.Frequency);
        }

        private static WindowSnapshot CaptureWindow()
        {
            return new WindowSnapshot
            {
                ElapsedSeconds = _windowElapsedSeconds,
                FrameCount = _frameCount,
                FrameTotalMs = _frameTotalMs,
                WorstFrameMs = _worstFrameMs,
                RoadsTabUpdate = _roadsTabUpdate,
                OverlayUpdate = _overlayUpdate,
                OverlayRepaint = _overlayRepaint,
                ThreadingUpdate = _threadingUpdate
            };
        }

        private static void LogSnapshot(string label, WindowSnapshot snapshot, bool tabOpen)
        {
            double averageFrameMs = snapshot.FrameCount > 0
                ? snapshot.FrameTotalMs / snapshot.FrameCount
                : 0d;
            double approximateFps = averageFrameMs > 0.001d
                ? 1000d / averageFrameMs
                : 0d;
            double pctMeasuredMsPerFrame = snapshot.FrameCount > 0
                ? (snapshot.RoadsTabUpdate.TotalMs
                   + snapshot.OverlayUpdate.TotalMs
                   + snapshot.OverlayRepaint.TotalMs
                   + snapshot.ThreadingUpdate.TotalMs) / snapshot.FrameCount
                : 0d;

            PedestrianCrossingLog.Advanced(
                "[PedestrianCrossingToolkit] Crossing-tab performance sample: label="
                + label
                + " open="
                + tabOpen
                + " seconds="
                + snapshot.ElapsedSeconds.ToString("0.00")
                + " frames="
                + snapshot.FrameCount
                + " avgFrameMs="
                + averageFrameMs.ToString("0.00")
                + " approxFps="
                + approximateFps.ToString("0.0")
                + " worstFrameMs="
                + snapshot.WorstFrameMs.ToString("0.00")
                + " pctMeasuredMsPerFrame="
                + pctMeasuredMsPerFrame.ToString("0.000")
                + " roadsTab="
                + FormatTiming(snapshot.RoadsTabUpdate)
                + " overlayUpdate="
                + FormatTiming(snapshot.OverlayUpdate)
                + " overlayRepaint="
                + FormatTiming(snapshot.OverlayRepaint)
                + " threadingUpdate="
                + FormatTiming(snapshot.ThreadingUpdate)
                + " crossings="
                + CrossingPlacementRegistry.Count
                + " activeMode="
                + PedestrianCrossingToolkitState.ActiveMode
                + ".");
        }

        private static string FormatTiming(TimingAccumulator timing)
        {
            double averageMs = timing.Count > 0 ? timing.TotalMs / timing.Count : 0d;
            return "count:"
                   + timing.Count
                   + ",avgMs:"
                   + averageMs.ToString("0.000")
                   + ",worstMs:"
                   + timing.WorstMs.ToString("0.000");
        }

        private static int GetSelectedRoadTabIndex()
        {
            PedestrianCrossingRoadsTab instance = PedestrianCrossingRoadsTab.Instance;
            return instance == null ? -1 : instance.SelectedTabIndexForDiagnostics;
        }

        private static void ResetWindow()
        {
            _windowElapsedSeconds = 0f;
            _frameCount = 0;
            _frameTotalMs = 0d;
            _worstFrameMs = 0d;
            _roadsTabUpdate = default(TimingAccumulator);
            _overlayUpdate = default(TimingAccumulator);
            _overlayRepaint = default(TimingAccumulator);
            _threadingUpdate = default(TimingAccumulator);
        }

        private static void ResetAll()
        {
            _armed = false;
            _tabOpen = false;
            _lastClosedBaseline = default(WindowSnapshot);
            _hasLastClosedBaseline = false;
            _awaitingPostCloseSample = false;
            ResetWindow();
        }
    }
}
