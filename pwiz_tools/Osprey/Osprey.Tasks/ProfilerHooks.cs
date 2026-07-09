/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Thin wrapper around JetBrains.Profiler.Api so the Osprey main-
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
        ///
        /// On net8.0 this also emits the <see cref="GCMemoryInfo"/> triple that
        /// decomposes resident memory. <c>managed_heap</c>
        /// (<c>GC.GetTotalMemory(false)</c>) counts only bytes the GC considers
        /// *in use*, so it silently omits two large quantities:
        ///   gc_committed - what the GC has committed from the OS, including
        ///                  free space it is holding rather than decommitting.
        ///                  With Server GC (one heap per core) this can exceed
        ///                  the in-use heap by many GB.
        ///   gc_fragmented - free bytes stranded *between* live objects.
        /// Then working_set - gc_committed approximates genuinely native memory
        /// (Parquet/mzML/SQLite buffers, runtime, JIT). Without these three,
        /// "working set minus managed heap" conflates GC slack with native
        /// allocation and cannot tell you which one to go after.
        /// </summary>
        public static void LogMemoryStats(Action<string> log, string label)
        {
            if (log == null)
                return;
            var proc = Process.GetCurrentProcess();
            long peakWs = proc.PeakWorkingSet64;
            long peakPaged = proc.PeakPagedMemorySize64;
            long curWs = proc.WorkingSet64;
            long managed = GC.GetTotalMemory(false);

            const double gb = 1024.0 * 1024.0 * 1024.0;
            string gcDetail = string.Empty;
#if NETCOREAPP || NET5_0_OR_GREATER
            var gcInfo = GC.GetGCMemoryInfo();
            gcDetail = string.Format(CultureInfo.InvariantCulture,
                ", gc_committed={0:F2} GB, gc_heap={1:F2} GB, gc_fragmented={2:F2} GB",
                gcInfo.TotalCommittedBytes / gb,
                gcInfo.HeapSizeBytes / gb,
                gcInfo.FragmentedBytes / gb);
#endif

            log(string.Format(CultureInfo.InvariantCulture,
                "[MEM {0}] working_set={1:F2} GB (peak={2:F2} GB), managed_heap={3:F2} GB, peak_paged={4:F2} GB, gen2_count={5}, loh_count={6}{7}",
                label,
                curWs / gb,
                peakWs / gb,
                managed / gb,
                peakPaged / gb,
                GC.CollectionCount(2),
                // Large Object Heap collection count is same as gen-2 in
                // standard GC; report it explicitly to document intent.
                GC.CollectionCount(2),
                gcDetail));
        }

        /// <summary>
        /// True when the OSPREY_LOG_MEMORY environment variable is set (any non-empty
        /// value). Gates the per-stage [MEM ...] snapshots so ordinary runs stay quiet;
        /// set it for a memory-profiling run (issue #4355).
        /// </summary>
        public static readonly bool MemoryLoggingEnabled =
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(@"OSPREY_LOG_MEMORY"));

        /// <summary>
        /// <see cref="LogMemoryStats"/> guarded by <see cref="MemoryLoggingEnabled"/> so
        /// stage-boundary probes can stay in the pipeline at zero cost when disabled.
        /// </summary>
        public static void LogMemoryStatsIfEnabled(Action<string> log, string label)
        {
            if (MemoryLoggingEnabled)
                LogMemoryStats(log, label);
        }

        /// <summary>
        /// Force a full blocking collection, then log the resulting PERSISTENT managed
        /// heap size -- the post-GC floor with transient garbage reclaimed -- as a
        /// <c>[MEM {label}]</c> line. Guarded by <see cref="MemoryLoggingEnabled"/> so it
        /// is a zero-cost no-op INCLUDING the collection on ordinary runs.
        /// <paramref name="detail"/> is appended verbatim for run context (e.g.
        /// <c>"(files=82, file_parallelism=1)"</c>).
        /// </summary>
        public static void LogManagedHeapAfterGcIfEnabled(Action<string> log, string label, string detail)
        {
            if (!MemoryLoggingEnabled || log == null)
                return;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            log(string.Format(CultureInfo.InvariantCulture,
                "[MEM {0}] managed_heap={1:F2} GB {2}",
                label, GC.GetTotalMemory(false) / (1024.0 * 1024.0 * 1024.0), detail));
        }
    }
}
