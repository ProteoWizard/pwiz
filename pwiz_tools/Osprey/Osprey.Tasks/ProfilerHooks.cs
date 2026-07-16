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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using pwiz.Osprey.Core;

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
        /// True when a dotMemory session is attached and ready to accept an
        /// API-triggered snapshot -- i.e. the binary was launched under
        /// <c>dotMemory start --use-api</c> (Profile-Osprey.ps1 -MemoryProfile).
        /// The dotMemory analogue of <see cref="MeasureReady"/>: false, a caught
        /// no-op, on ordinary and headless-batch runs where nothing is attached,
        /// so the retention capture never fires outside a deliberate profiling run.
        /// </summary>
        public static bool SnapshotReady
        {
            get
            {
                try { return SnapshotReadyInternal(); }
                catch { return false; }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool SnapshotReadyInternal()
        {
            return 0 != (JetBrains.Profiler.Api.MemoryProfiler.GetFeatures()
                         & JetBrains.Profiler.Api.MemoryFeatures.Ready);
        }

        /// <summary>
        /// Capture a named dotMemory retention snapshot, but ONLY when a dotMemory
        /// session is attached (<see cref="SnapshotReady"/>). This is the "who holds
        /// it" companion to the forced-GC <c>[MEM ...] managed_heap</c> probe: call it
        /// at the same post-GC boundary and the snapshot's live set is the same
        /// category of number as the logged floor (dotMemory forces its own full GC
        /// before a snapshot), so the retention paths / dominators reconcile with the
        /// number just logged. A no-op when no profiler is attached -- the printf
        /// [MEM ...] layer and the real 82-file batch run are unaffected. Isolated in a
        /// non-inlineable method so a missing JetBrains assembly is caught here rather
        /// than tripping JIT of the caller (same shape as the MeasureProfiler wrappers).
        ///
        /// This guards ONLY on <see cref="SnapshotReady"/> (profiler attached), NOT on
        /// <see cref="MemoryLoggingEnabled"/>: that is what keeps it safe on normal runs
        /// (nothing is attached there). Callers that want the snapshot to line up with a
        /// <c>[MEM ...]</c> line must themselves gate on <see cref="MemoryLoggingEnabled"/>
        /// and call at a post-GC point. The sole caller,
        /// <see cref="LogManagedHeapAfterGcIfEnabled"/>, does both -- and its own
        /// <c>GC.Collect()</c> pair (not dotMemory's implicit pre-snapshot GC) is what
        /// makes the captured live set reconcile with the logged floor.
        /// </summary>
        public static void CaptureRetentionSnapshot(string name)
        {
            if (!SnapshotReady)
                return;
            try { CaptureRetentionSnapshotInternal(name); }
            catch { /* profiler detached mid-run or API not available */ }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CaptureRetentionSnapshotInternal(string name)
        {
            JetBrains.Profiler.Api.MemoryProfiler.GetSnapshot(name);
        }

        /// <summary>
        /// Log peak working set and managed heap size to the given
        /// writer. Cheap to call; suitable for per-stage and end-of-run
        /// snapshots.
        ///
        /// NONE OF THESE NUMBERS IS A LIVE SET. Read them with the caveats below,
        /// or use <see cref="LogManagedHeapAfterGcIfEnabled"/>, which forces a
        /// collection first and is the only probe here that answers "will this fit".
        ///
        /// <c>managed_heap</c> is <c>GC.GetTotalMemory(false)</c>: bytes allocated
        /// since the last collection, WITHOUT forcing one. It therefore includes
        /// uncollected garbage, and two runs doing identical work can differ by tens
        /// of GB purely on whether a gen-2 happened to land before the probe.
        ///
        /// <c>working_set</c> is resident memory, but Server GC expands heaps until it
        /// nears the high-memory-load threshold (~90% of physical) before collecting,
        /// so on a large box the working set reflects available RAM at least as much
        /// as demand. Peak working set is close to useless for sizing.
        ///
        /// The net8.0 <c>GCMemoryInfo</c> triple is measured AS OF THE LAST GC,
        /// not now -- <c>GC.GetGCMemoryInfo()</c> with no argument returns the most
        /// recent collection's snapshot. Hence the <c>_last_gc</c> suffixes. Do NOT
        /// subtract them from the live figures above: <c>working_set - gc_committed</c>
        /// mixes a current value with a stale one and can go negative (it did, by
        /// 10 GB, on an 82-file run).
        ///   gc_committed_last_gc  - bytes the GC had committed from the OS.
        ///   gc_heap_last_gc       - heap size, including fragmentation.
        ///   gc_fragmented_last_gc - free bytes stranded between live objects.
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
                ", gc_committed_last_gc={0:F2} GB, gc_heap_last_gc={1:F2} GB, gc_fragmented_last_gc={2:F2} GB",
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
        ///
        /// When a dotMemory session is also attached (Profile-Osprey.ps1
        /// -MemoryProfile), this additionally captures a retention snapshot named
        /// <paramref name="label"/> at this same post-GC boundary via
        /// <see cref="CaptureRetentionSnapshot"/>, so the "who holds this live set"
        /// view reconciles with the <c>managed_heap</c> number just logged. That
        /// capture is a no-op when no profiler is attached, so the batch path is
        /// unchanged.
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
            CaptureRetentionSnapshot(label);
        }

        /// <summary>
        /// True when OSPREY_TRACK_RELEASE is set. Gates the prove-from-inside resident-MS2
        /// retention check (<see cref="TrackResidentSpectra"/> / <see cref="ReportResidentSpectraReleased"/>).
        /// Byte-inert when unset: no WeakReferences captured, no forced GC, the pipeline path
        /// is unchanged -- so the regression golden is unaffected.
        /// </summary>
        public static readonly bool TrackResidentReleaseEnabled =
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(@"OSPREY_TRACK_RELEASE"));

        /// <summary>
        /// Capture WeakReferences to the resident MS2 list, a spread of its <see cref="Spectrum"/>
        /// elements, and their peak arrays, so <see cref="ReportResidentSpectraReleased"/> can
        /// confirm -- after the caller nulls its strong reference and a full GC runs -- that
        /// nothing else still roots them. This answers the retention question the forced-GC
        /// <c>[MEM ...] managed_heap</c> aggregates cannot: they show HOW MUCH is live, not
        /// whether THIS list is still held by an unanticipated root (an index, a provider, a
        /// closure). Returns null (capturing nothing) unless <see cref="TrackResidentReleaseEnabled"/>.
        /// The returned token holds ONLY WeakReferences, so it never itself keeps the spectra alive.
        /// </summary>
        public static object TrackResidentSpectra(IReadOnlyList<Spectrum> spectra)
        {
            if (!TrackResidentReleaseEnabled || spectra == null)
                return null;
            return new ResidentReleaseProbe(spectra);
        }

        /// <summary>
        /// After the caller has nulled its strong reference to the resident MS2 list (and built
        /// any downstream streaming provider), force a full blocking GC and log whether the
        /// tracked list / sample spectra / peak arrays survived. All-dead == the streaming
        /// rearchitecture genuinely releases the resident MS2; ANY survivor == an unanticipated
        /// root still pins it (the retention bug this probe exists to catch). A no-op when
        /// <paramref name="token"/> is null (tracking disabled), so it is byte-inert by default.
        /// </summary>
        public static void ReportResidentSpectraReleased(object token, Action<string> log)
        {
            var probe = token as ResidentReleaseProbe;
            if (probe == null || log == null)
                return;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            log(@"[RETENTION] resident MS2 after null+GC: " + probe.Report());
        }

        /// <summary>
        /// Holds ONLY WeakReferences to the resident MS2 list, a spread of its elements, and
        /// their peak arrays -- the arrays are the bulk of the ~6 GB, so they are tracked
        /// explicitly in case an element is collected but its arrays are separately rooted.
        /// </summary>
        private sealed class ResidentReleaseProbe
        {
            private readonly WeakReference _list;
            private readonly WeakReference[] _spectra;
            private readonly WeakReference[] _mzs;
            private readonly WeakReference[] _intensities;
            private readonly int _totalCount;

            internal ResidentReleaseProbe(IReadOnlyList<Spectrum> spectra)
            {
                _list = new WeakReference(spectra);
                _totalCount = spectra.Count;
                int n = Math.Min(16, spectra.Count);
                _spectra = new WeakReference[n];
                _mzs = new WeakReference[n];
                _intensities = new WeakReference[n];
                for (int i = 0; i < n; i++)
                {
                    // Spread the samples evenly across the list (first .. last) so a partial
                    // leak (e.g. one window still rooted) is more likely to be caught.
                    int idx = n == 1 ? 0 : (int)((long)i * (spectra.Count - 1) / (n - 1));
                    var s = spectra[idx];
                    _spectra[i] = new WeakReference(s);
                    _mzs[i] = new WeakReference(s?.Mzs);
                    _intensities[i] = new WeakReference(s?.Intensities);
                }
            }

            internal string Report()
            {
                int spectraAlive = 0, mzAlive = 0, intAlive = 0;
                for (int i = 0; i < _spectra.Length; i++)
                {
                    if (_spectra[i].IsAlive) spectraAlive++;
                    if (_mzs[i].IsAlive) mzAlive++;
                    if (_intensities[i].IsAlive) intAlive++;
                }
                return string.Format(CultureInfo.InvariantCulture,
                    "list_alive={0}, sample_spectra_alive={1}/{2}, mz_arrays_alive={3}/{2}, intensity_arrays_alive={4}/{2} (of {5} resident MS2)",
                    _list.IsAlive, spectraAlive, _spectra.Length, mzAlive, intAlive, _totalCount);
            }
        }
    }
}
