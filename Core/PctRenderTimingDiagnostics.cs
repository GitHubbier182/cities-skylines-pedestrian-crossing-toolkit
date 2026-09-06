using System;
using System.Diagnostics;

namespace PedestrianCrossingToolkit
{
    // Optional read-only timing bridge for the separate FPS Monitor. No Harmony ownership.
    public static class PctRenderTimingDiagnostics
    {
        private static readonly object Sync = new object();
        private static readonly double[] Counters = new double[15];
        private static volatile bool _enabled;
        // Triples: calls, total milliseconds, maximum milliseconds.
        // Prefix, postfix, default hover, pointer terrain query, pointer spatial query.
        public static void SetMonitoring(bool enabled)
        {
            lock (Sync)
            {
                _enabled = enabled;
                Array.Clear(Counters, 0, Counters.Length);
            }
        }
        public static void CopyAndReset(double[] destination)
        {
            if (destination == null || destination.Length < Counters.Length) return;
            lock (Sync)
            {
                Array.Copy(Counters, destination, Counters.Length);
                Array.Clear(Counters, 0, Counters.Length);
            }
        }
        internal static long Begin() { return _enabled ? Stopwatch.GetTimestamp() : 0L; }
        internal static void End(int scope, long started)
        {
            if (started == 0 || !_enabled) return;
            double milliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            lock (Sync)
            {
                if (!_enabled) return;
                int offset = scope * 3;
                Counters[offset]++;
                Counters[offset + 1] += milliseconds;
                Counters[offset + 2] = Math.Max(Counters[offset + 2], milliseconds);
            }
        }
    }
}
