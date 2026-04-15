using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace pwiz.OspreySharp
{
    /// <summary>
    /// Thin wrapper around JetBrains.Profiler.Api so the OspreySharp main-
    /// search stage can be profiled on its own without the calibration
    /// noise. Matches the pattern in
    /// <c>Skyline/TestRunnerLib/MemoryProfiler.cs</c>: the JetBrains call is
    /// isolated in a non-inlineable method so a missing assembly fails the
    /// try/catch instead of tripping JIT of the caller.
    ///
    /// Usage (from AnalysisPipeline):
    ///   ProfilerHooks.StartMeasure();
    ///   ...  Parallel.ForEach main search  ...
    ///   ProfilerHooks.SaveAndStopMeasure();
    ///
    /// When the binary is launched with <c>dottrace attach ... --profiling-
    /// api</c> or equivalent, these calls bracket the captured trace; in
    /// ordinary runs they are inexpensive no-ops.
    /// </summary>
    public static class ProfilerHooks
    {
        public static bool MeasureReady
        {
            get
            {
                try { return MeasureReadyInternal(); }
                catch { return false; }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool MeasureReadyInternal()
        {
            return 0 != (JetBrains.Profiler.Api.MeasureProfiler.GetFeatures()
                         & JetBrains.Profiler.Api.MeasureFeatures.Ready);
        }

        public static void StartMeasure()
        {
            try { StartMeasureInternal(); }
            catch { /* profiler not attached or API not available */ }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StartMeasureInternal()
        {
            JetBrains.Profiler.Api.MeasureProfiler.StartCollectingData();
        }

        /// <summary>
        /// Stop collecting data, flush the snapshot, detach. Intended to
        /// bracket Stage 4 so the .dtp snapshot contains only the main-
        /// search hot paths.
        /// </summary>
        public static void SaveAndStopMeasure()
        {
            try { SaveAndStopMeasureInternal(); }
            catch { /* profiler not attached or API not available */ }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SaveAndStopMeasureInternal()
        {
            JetBrains.Profiler.Api.MeasureProfiler.StopCollectingData();
            JetBrains.Profiler.Api.MeasureProfiler.SaveData();
            JetBrains.Profiler.Api.MeasureProfiler.Detach();
        }

        /// <summary>
        /// Log peak working set and managed heap size to the given
        /// writer. Cheap to call; suitable for per-stage and end-of-run
        /// snapshots.
        /// </summary>
        public static void LogMemoryStats(Action<string> log, string label)
        {
            if (log == null) return;
            var proc = Process.GetCurrentProcess();
            long peakWs = proc.PeakWorkingSet64;
            long peakPaged = proc.PeakPagedMemorySize64;
            long curWs = proc.WorkingSet64;
            long managed = GC.GetTotalMemory(false);

            log(string.Format(CultureInfo.InvariantCulture,
                "[MEM {0}] working_set={1:F2} GB (peak={2:F2} GB), managed_heap={3:F2} GB, peak_paged={4:F2} GB, gen2_count={5}, loh_count={6}",
                label,
                curWs / (1024.0 * 1024.0 * 1024.0),
                peakWs / (1024.0 * 1024.0 * 1024.0),
                managed / (1024.0 * 1024.0 * 1024.0),
                peakPaged / (1024.0 * 1024.0 * 1024.0),
                GC.CollectionCount(2),
                // Large Object Heap collection count is same as gen-2 in
                // standard GC; report it explicitly to document intent.
                GC.CollectionCount(2)));
        }
    }
}
