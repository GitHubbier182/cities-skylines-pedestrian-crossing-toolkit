using System;
using System.IO;
using UnityEngine;

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

        public static bool AdvancedDiagnostics
        {
            get { return PlayerPrefs.GetInt(AdvancedDiagnosticsKey, 0) != 0; }
            set
            {
                bool changed = AdvancedDiagnostics != value;
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
}
