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
using pwiz.Osprey.FDR;
using pwiz.Osprey.FDR.ModelDiagnostics;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The shared plumbing the scoring tasks (<see cref="PerFileScoringTask"/>,
    /// <see cref="PerFileRescoreTask"/>, <see cref="FirstPassFdrTask"/>) once
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

        // Serializes input parsing across concurrent ProcessFile() calls (mzML or
        // vendor raw; the name predates vendor reading). The
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
        /// header-indexed; on a miss the input is parsed once - mzML or vendor raw, whichever
        /// <see cref="SpectrumFileReader"/> selects - (gated across parallel files),
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
                    ctx.LogInfo("Spectra cache stale or invalid; re-parsing the input.");
                }
                catch (Exception ex)
                {
                    // A present-but-corrupt/truncated cache body (intact header, e.g. an
                    // interrupted write) throws during the index pass; re-parse the input and
                    // rewrite the cache rather than faulting the file. Matches the old
                    // LoadSpectra fallback. Only the miss-path re-index below stays a hard
                    // error, since that indexes a cache we just wrote.
                    ctx.LogWarning(string.Format(
                        "Failed to index spectra cache: {0}. Re-parsing the input.", ex.Message));
                }
            }

            // Miss/stale/absent: parse the input once (materialized only transiently here),
            // optionally serialized across files, write the cache, then index it and drop the
            // parsed list. The "Processing file N/M: <path>" banner already named the file.
            // SpectrumFileReader picks the mzML or vendor-raw reader by extension; both
            // return the same MzmlResult, so nothing below here knows the source format.
            MzmlResult mzmlResult;
            if (serializeMzmlRead)
                s_mzmlReadGate.Wait();
            try
            {
                mzmlResult = SpectrumFileReader.LoadAllSpectra(inputFile);
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
                // Name the file that was just written, the way the library cache does. Without
                // this the only evidence a multi-GB cache was produced is a silent gap in the
                // log, and nothing says WHERE it landed - which matters because --work-dir
                // redirects this path away from the data directory (ArtifactPaths.ResolveCacheDir),
                // so a run can rebuild caches that already exist beside the mzML.
                long cacheBytes = 0;
                try
                {
                    if (File.Exists(cachePath))
                        cacheBytes = new FileInfo(cachePath).Length;
                }
                catch
                {
                    cacheBytes = 0;
                }
                ctx.LogInfo(string.Format("Saved spectra cache ({0} MS2 + {1} MS1, {2:F2} GB) to '{3}'",
                    mzmlResult.Ms2Spectra.Count, mzmlResult.Ms1Spectra.Count,
                    cacheBytes / 1024.0 / 1024.0 / 1024.0, cachePath));
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

        /// <summary>
        /// Resolve a path whose stem matches <paramref name="fileName"/>, used
        /// only as the base for sidecar file naming (the path itself need
        /// not exist). In normal mode this is the input mzML; in
        /// --task FirstPassFDR mode where InputFiles is empty we synthesize the
        /// path from the matching .scores.parquet by replacing the
        /// `.scores.parquet` suffix with `.mzML`. Mirrors the Rust
        /// `synthetic_input_from_parquet` helper.
        ///
        /// <para>Lives here rather than on <see cref="FirstPassFdrTask"/> because
        /// <see cref="FirstPassSurvivorLoader"/> needs the same resolution to find a
        /// file's 1st-pass sidecar, and a loader reaching into a task class for it
        /// would be the wrong direction of dependency.</para>
        /// </summary>
        internal static string ResolveSidecarBasePath(
            string fileName,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config)
        {
            // Normal mode: prefer the actual input mzML path so sidecars
            // land next to the source mzML.
            if (config.InputFiles != null)
            {
                foreach (string inputPath in config.InputFiles)
                {
                    if (string.Equals(
                        Path.GetFileNameWithoutExtension(inputPath),
                        fileName,
                        StringComparison.Ordinal))
                    {
                        return inputPath;
                    }
                }
            }
            // --task FirstPassFDR fallback: derive a synthetic mzML path from the
            // matching parquet stem so all the existing sidecar path
            // helpers keep working without conditional branches.
            if (perFileParquetPaths != null
                && perFileParquetPaths.TryGetValue(fileName, out string parquetPath))
            {
                string parent = Path.GetDirectoryName(parquetPath) ?? ".";
                return Path.Combine(parent, fileName + ".mzML");
            }
            return null;
        }

        /// <summary>
        /// Reduce off one file's PRE-compaction stub pool everything a rehydrate used to
        /// read off the resident all-files pool. Only the run-level FDR passing-target count
        /// is needed: FirstPassFDR's Stage 5 result line reports it per file, and the streaming
        /// hydrate has already dropped the non-survivors by the time that line is written.
        /// Identical predicate to <c>FirstPassFdrTask.LogFirstPassResults</c>.
        ///
        /// <para>Shared by both callers of
        /// <see cref="RescoreHydration.HydrateCompactedStreaming"/> -- the
        /// <c>--task PerFileRescoring</c> worker load in <see cref="PerFileScoringTask"/>
        /// and the straight-through resume in <see cref="FirstPassFdrTask"/> - so the two
        /// cannot drift into reporting per-file counts under different predicates. The
        /// <c>--task SecondPassFDR</c> merge takes that SAME streaming hydrate since #4486
        /// (it no longer routes to the resident batch twin), but it skips this tally: the
        /// only reader of <c>PassingTargets</c> is FirstPassFDR's per-file Stage 5 result
        /// line, and that task is excluded on the merge node, so filling the field there
        /// would walk every stub to produce a number nothing reads.</para>
        /// </summary>
        internal static void TallyPreCompaction(
            OspreyConfig config, List<FdrEntry> stubs, PreCompactionTally tally)
        {
            int passing = 0;
            foreach (var entry in stubs)
            {
                if (!entry.IsDecoy && entry.EffectiveRunQvalue(config.FdrLevel) <= config.RunFdr)
                    passing++;
            }
            tally.PassingTargets = passing;
        }

        /// <summary>
        /// Fold one file's PRE-compaction stubs into the <c>--model-diagnostics</c> report
        /// accumulator, handing it exactly the scalars the batch
        /// <c>ModelDiagnosticsData.Build</c> reads off each <see cref="FdrEntry"/> - identity,
        /// is_decoy, SVM score, and the four first-pass q-values the
        /// <c>.1st-pass.fdr_scores.bin</c> overlay has just written onto these stubs. Rows
        /// arrive here in the same nested (file, row) order the batch build walks (input-file
        /// order, parquet row order within a file), which is what makes the streamed
        /// reductions reproduce the resident ones element for element.
        ///
        /// <para>Shared by the same two hydrate callers as
        /// <see cref="TallyPreCompaction"/>: the report must be identical whether the
        /// pre-compaction rows passed through the worker load or a resume's.</para>
        /// </summary>
        internal static void FeedModelDiagnostics(
            ModelDiagnosticsData.Accumulator accumulator, int fileIdx, List<FdrEntry> stubs)
        {
            foreach (var entry in stubs)
            {
                accumulator.Add(fileIdx, entry.ModifiedSequence, entry.Charge, entry.EntryId,
                    entry.IsDecoy, entry.Score,
                    new FdrQValues(entry.RunPrecursorQvalue, entry.RunPeptideQvalue,
                        entry.ExperimentPrecursorQvalue, entry.ExperimentPeptideQvalue, entry.Pep));
            }
        }
    }
}
