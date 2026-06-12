using System;
using System.IO;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public static class PedestrianCrossingLog
    {
        public static readonly bool VerboseDiagnostics = false;
        private const string Prefix = "[PedestrianCrossingToolkit]";
        private const string FileName = "PedestrianCrossingToolkit.log";
        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static string _logPath;

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
                    File.WriteAllText(
                        _logPath,
                        "Pedestrian Crossing Toolkit log started "
                        + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        + Environment.NewLine);
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
                _initialized = false;
            }
        }

        public static void Info(string message)
        {
            AppendLine("Info", FormatMessage(message));
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
            try
            {
                EnsurePath();
                string line = DateTime.Now.ToString("HH:mm:ss.fff")
                              + " "
                              + level
                              + ": "
                              + message
                              + Environment.NewLine;
                File.AppendAllText(_logPath, line);
            }
            catch
            {
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
