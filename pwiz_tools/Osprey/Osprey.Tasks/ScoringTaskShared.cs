/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.IO;
using System.Threading;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The shared plumbing the scoring tasks (<see cref="PerFileScoringTask"/>,
    /// <see cref="PerFileRescoreTask"/>, <see cref="FirstJoinTask"/>) once
    /// inherited from the retired <c>AbstractScoringTask</c> base: the mzML read
    /// gate, the PIN feature width + base-id mask constants, the isolation-window
    /// extractor, and the nearest-MS1 lookup. None of it needs instance state, so
    /// it lives here as <c>internal static</c> rather than in a base class --
    /// removing the inheritance edge that invited feature-envy. The actual scoring
    /// BEHAVIOR lives in <see cref="ScoringPipeline"/> (composition, debt-paydown
    /// PR 7); tasks reach it through <see cref="Pipeline"/> and call its methods
    /// directly.
    /// </summary>
    internal static class ScoringTaskShared
    {
        // Internal so the scoring tasks share the single PIN feature width without
        // redeclaring it. Derives from the single source of truth in
        // Osprey.Scoring so the two cannot drift.
        internal const int NUM_PIN_FEATURES = OspreyFeatureCalculators.FeatureCount;

        // EntryId encodes target/decoy in the high bit; base_id is the lower 31
        // bits, shared by a target and its paired decoy.
        internal const uint BASE_ID_MASK = 0x7FFFFFFFu;

        // Serializes mzML reads across concurrent ProcessFile() calls. The
        // producer inside MzmlReader.LoadAllSpectra is a sequential XmlReader over
        // a FileStream, so 3 files parsing in parallel means 3 sequential disk
        // scans fighting for the same head/cache. Gating the parse step funnels
        // the disk-bound work into one stream at a time while leaving the
        // subsequent main-search phase free to run in parallel across files.
        internal static readonly SemaphoreSlim s_mzmlReadGate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Build the <see cref="ScoringPipeline"/> the tasks score through, wired
        /// to the run's log sink and (optional) diagnostics seam. Cheap: the
        /// pipeline only carries those two references, so constructing one per
        /// scoring call matches the former per-call construction inside the base
        /// class forwarders exactly.
        /// </summary>
        internal static ScoringPipeline Pipeline(PipelineContext ctx)
        {
            return new ScoringPipeline(ctx.LogInfo, ctx.Diagnostics as IScoringDiagnostics);
        }

        /// <summary>
        /// Extract unique isolation windows from the first cycle of MS2 spectra.
        /// </summary>
        internal static List<IsolationWindow> ExtractIsolationWindows(List<Spectrum> spectra)
        {
            var windows = new List<IsolationWindow>();
            var seenCenters = new HashSet<int>();

            foreach (var spectrum in spectra)
            {
                int centerKey = (int)Math.Round(spectrum.IsolationWindow.Center * 10.0);
                if (seenCenters.Contains(centerKey))
                    break;
                seenCenters.Add(centerKey);
                windows.Add(spectrum.IsolationWindow);
            }

            // Sort by center m/z. Array.Sort OK: windows were deduplicated above on the
            // rounded center key (Round(Center*10)); because that key is a function of
            // Center, two windows sharing a Center share a key and one is dropped, so
            // every retained window has a distinct Center and the comparator never
            // returns 0. (This guarantees distinct Centers, not any minimum spacing --
            // centers on either side of a rounding boundary can be arbitrarily close.)
            windows.Sort((a, b) => a.Center.CompareTo(b.Center)); // Array.Sort OK: (see above) dedup on the rounded center key leaves distinct Centers, so the comparator never ties
            return windows;
        }

        /// <summary>
        /// Find the MS1 spectrum with retention time closest to the given RT.
        /// Assumes MS1 spectra are sorted by RT. Thin forwarder to the single
        /// implementation in Core (<see cref="MS1Spectrum.FindNearest"/>) so the
        /// scoring harness and the <c>Calibrator</c> share one binary search /
        /// tie-break and cannot drift.
        /// </summary>
        internal static MS1Spectrum FindNearestMs1(List<MS1Spectrum> ms1Spectra, double rt)
        {
            return MS1Spectrum.FindNearest(ms1Spectra, rt);
        }

        /// <summary>
        /// Ensure a valid <c>.spectra.bin</c> cache exists for the input and return a
        /// streaming <see cref="SpectraWindowIndex"/> over it (per-window MS2 offsets, plus
        /// MS1 and the first-cycle isolation windows) WITHOUT materializing the full MS2
        /// <c>List&lt;Spectrum&gt;</c>. On a cache hit (the common re-run path) the file is only
        /// header-indexed; on a miss the mzML is parsed once (gated across parallel files),
        /// written to the cache, then indexed and the parsed list dropped. Stages 1-4
        /// (calibration + scoring) stream each isolation window from the returned index. The
        /// full resident load survives only in Stage-6 rescore
        /// (<c>PerFileRescoreTask.LoadSpectraForRescore</c>, a separate follow-up).
        ///
        /// Shared here rather than owned by <see cref="PerFileScoringTask"/> because
        /// <see cref="SpectraCacheTask"/> (<c>--task SpectraCache</c>) builds exactly the
        /// same caches. Two copies would let the staging path and the scoring path drift,
        /// which is precisely what a raw-vs-mzML parity check must be able to rule out.
        /// </summary>
        internal static SpectraWindowIndex EnsureSpectraCache(string inputFile, bool serializeMzmlRead,
            out int unsortedCount, PipelineContext ctx)
        {
            unsortedCount = 0;
            // Shared GetCachePath so the write and the rescore read (PerFileRescoreTask)
            // derive an identical filename + directory (ArtifactPaths redirects the dir).
            string cachePath = SpectraCache.GetCachePath(inputFile);
            if (File.Exists(cachePath))
            {
                try
                {
                    // Cache hit: index the file directly (header pass only) -- never build the
                    // full MS2 list. Returns null when stale/invalid (bad magic/version or the
                    // source fingerprint changed), which falls through to a re-parse below.
                    var hit = SpectraWindowIndex.BuildFromCache(cachePath, inputFile);
                    if (hit != null)
                    {
                        ctx.LogInfo(string.Format("Streaming spectra from cache: {0}", cachePath));
                        return hit;
                    }
                    ctx.LogInfo("Spectra cache stale or invalid; re-parsing mzML.");
                }
                catch (Exception ex)
                {
                    // A present-but-corrupt/truncated cache body (intact header, e.g. an
                    // interrupted write) throws during the index pass; re-parse the mzML and
                    // rewrite the cache rather than faulting the file. Matches the old
                    // LoadSpectra fallback. Only the miss-path re-index below stays a hard
                    // error, since that indexes a cache we just wrote.
                    ctx.LogWarning(string.Format(
                        "Failed to index spectra cache: {0}. Re-parsing mzML.", ex.Message));
                }
            }

            // Miss/stale/absent: parse the mzML once (materialized only transiently here),
            // optionally serialized across files, write the cache, then index it and drop the
            // parsed list. The "Processing file N/M: <path>" banner already named the file.
            MzmlResult mzmlResult;
            if (serializeMzmlRead)
                s_mzmlReadGate.Wait();
            try
            {
                mzmlResult = MzmlReader.LoadAllSpectra(inputFile);
            }
            finally
            {
                if (serializeMzmlRead)
                    s_mzmlReadGate.Release();
            }
            unsortedCount = mzmlResult.UnsortedSpectrumCount;

            try
            {
                SpectraCache.SaveSpectraCache(cachePath, mzmlResult.Ms2Spectra, mzmlResult.Ms1Spectra, inputFile);
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format("Failed to save spectra cache: {0}", ex.Message));
            }

            // Index the just-written cache and stream from it (the parsed MS2 list drops when
            // this method returns). Per-file scoring REQUIRES the cache; if it could not be
            // written/indexed (e.g. a read-only or full output directory, or a failed write),
            // fail clearly -- preserving the underlying error -- rather than silently fall back
            // to a resident load that would OOM a large run.
            SpectraWindowIndex index = null;
            Exception indexError = null;
            try
            {
                index = SpectraWindowIndex.BuildFromCache(cachePath, inputFile);
            }
            catch (Exception ex)
            {
                indexError = ex;
            }
            if (index == null)
                throw new IOException(string.Format(
                    "Could not index the spectra cache for '{0}'. Per-file scoring streams MS2 from " +
                    "'{1}'; ensure that directory is writable (the .scores.parquet and .calibration.json " +
                    "outputs are written to the same place).", inputFile, cachePath), indexError);
            return index;
        }
    }
}
