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
        // vendor raw; the name predates vendor reading). Reading a spectrum file is
        // disk-bound and sequential, so 3 files parsing in parallel means 3
        // sequential scans fighting for the same head/cache. Gating the parse step
        // funnels the disk-bound work into one stream at a time while leaving the
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
            // SpectrumFileReader reads every format through ProteoWizard and returns the
            // same SpectrumFileResult, so nothing below here knows the source format.
            // Deleting the sources once the caches exist is supported, so reaching a re-parse
            // with no source is a real state, not a bad argument. Say which of the two is
            // wrong - the cache, and why - rather than failing inside the reader on a path
            // that is not there.
            // TODO: name WHY the cache was rejected - a version bump reads very differently
            // from a truncated body. The reader returns a bare bool from every rejection path,
            // so the reason has to be captured by the validity check itself and carried out;
            // re-deriving it here would duplicate that logic and drift from it.
            if (!File.Exists(inputFile) && !Directory.Exists(inputFile))
            {
                throw new InvalidDataException(string.Format(
                    @"Spectra cache '{0}' is not usable and cannot be rebuilt because the source '{1}' is missing. Restore the source and re-run.",
                    cachePath, inputFile));
            }
            SpectrumFileResult mzmlResult;
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
        /// not exist). This is the input data file, on every route.
        ///
        /// <para>The parquet-derived fallback below is UNREACHABLE now and is kept
        /// only because removing it is a behaviour change that belongs in its own
        /// commit. It existed for <c>--task FirstPassFDR</c>, which took
        /// <c>--input-scores</c> and so arrived with <c>InputFiles</c> empty; every
        /// task now requires <c>--input</c>, and the <c>fileName</c> keys are
        /// derived from those same inputs, so the loop always matches.</para>
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
            // KEPT, and the "unreachable since --input-scores retired" note that stood here
            // is not safe to act on. The argument for unreachability is that every task
            // requires --input and the fileName keys are derived from those same inputs, so
            // the loop above always matches. But PerFileRescoreTask documents a SUPPORTED
            // state in which a run's file_name has no input_files stem (WriteUnchangedReconciled
            // returns silently for it), and that is exactly the case this branch answers -
            // deleting it would turn a synthesized sidecar path into null for the one shape
            // that needs it. Establish which of the two is true before removing this; it is a
            // behaviour change either way, and it belongs with the finding that owns that
            // state rather than with the flag retirement that made it look dead.
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
        /// A path from this analysis that resolves to the directory its per-file artifacts go
        /// to, for naming the analysis-wide experiment-scope FDR sidecar
        /// (<see cref="FdrExperimentSidecar.PathFor"/>).
        ///
        /// <para>The FIRST input, on every route. It used to prefer <c>InputScores</c>,
        /// because a distributed <c>--task</c> node was given its inputs as scores parquets
        /// and might have had no data-file list at all; every node is given the same list
        /// now, and both forms resolved to the same directory anyway since the parquets are
        /// written beside the inputs. What matters is only that every phase of one analysis
        /// picks a path that resolves the SAME way - the blib's own directory does not, which
        /// is the bug this exists to avoid.</para>
        /// </summary>
        internal static string ArtifactSiblingPath(OspreyConfig config)
        {
            if (config == null)
                return null;
            if (config.InputFiles != null && config.InputFiles.Count > 0)
                return config.InputFiles[0];
            return null;
        }

        /// <summary>
        /// Whether this run's Stage-6 rescore hydrates each run from that run's own artifacts.
        /// TRUE means the <c>--input-scores</c> load must NOT build the all-runs bundle: the
        /// rescore loop hydrates per run, and anything the load pre-built would be discarded and
        /// re-read - which is precisely the 8m42s / 17.2 GB an 86-run plate spent before
        /// rescoring its first run.
        ///
        /// <para><b>ONE predicate, read by both sites.</b> The load
        /// (<c>PerFileScoringTask.LoadJoinOnlyScores</c>) and the loop
        /// (<c>PerFileRescoreTask.BuildPerRunHydrate</c>) must agree exactly: if the load skips
        /// the bundle and the loop then takes the join path, the rescore runs against empty
        /// per-file lists and silently produces nothing. Two copies of this test would be a
        /// drift hazard with that as its failure mode, so there is one.</para>
        ///
        /// <para>Two terms exclude a task that reaches this code for a different purpose.
        /// <c>StopAfterStage5</c> is <c>--task FirstPassFDR</c>, which computes rather than
        /// rescores. <c>ExpectReconciledInput</c> is <c>--task SecondPassFDR</c>, whose Stage 7
        /// consumes the whole-run pool. Everything else rescores per run - including the
        /// straight-through pipeline.</para>
        ///
        /// <para><b>The <c>--input-scores</c> term is a KNOWN WART, and it is here for a measured
        /// reason rather than a good one.</b> Keying a memory shape on how the run was invoked is
        /// wrong in principle - the per-run artifacts exist either way, and straight-through
        /// consequently still holds the planner's whole-experiment products (the reconciliation
        /// action map, 30.8 M entries at 446 runs; gap-fill targets, 8.85 M; per-run consensus
        /// targets and refined calibrations - order 13 GB) across the entire rescore.</para>
        ///
        /// <para>Removing the term was TRIED and reverted. Straight-through output stayed
        /// byte-identical (mode 1 green), but the Stage-5 rehydrate leg lost 77 of 27,321
        /// precursors a run apiece - <c>RefSpectra.copies</c> and <c>NRunsDetected</c> both
        /// 3 -> 2, 94 <c>RetentionTimes</c> keys missing. Cause: mode 5 SKIPS PerFileRescoring,
        /// so <c>PerFileRescoreTask.Rehydrate</c> runs and rebuilds the whole-run pool for
        /// Stage 7 via <c>OverlayReconciledIntoFiles(..., ctx.Get&lt;PerFileGapFillForRescore&gt;())</c>.
        /// That path needs gap-fill across ALL runs by design, and a per-run FirstPassFDR
        /// rehydrate has none to give it.</para>
        ///
        /// <para>So extending this to straight-through is NOT independent of Stage 7's own
        /// O(runs) pool - it is blocked behind the same lean-row work (#4486). The right signal
        /// remains the SUMMARY'S EXISTENCE rather than an argument, since it is written when
        /// planning ends and so answers "has the producing phase finished?" the way an artifact
        /// should; the flag term is what has to go when Stage 7 stops needing the pool.</para>
        ///
        /// <para>The summary is probed by header rather than read, so this stays cheap enough to
        /// call from either site. Its ABSENCE returns false rather than failing here: the run is
        /// then already failing for a named reason at <see cref="ReadRetainedBaseIds"/>, and
        /// duplicating that error at a second site would report it twice.</para>
        /// </summary>
        internal static bool CanHydratePerRun(OspreyConfig config)
        {
            // ADMIT, do not exclude. This listed the tasks to keep OUT and so admitted anything
            // unlisted - which is how --task ModelDiagnostics ended up routed down the per-run
            // rescore path and skipped its own regeneration (Astral mode 7: "regeneration
            // changed nothing at all"). It sets none of NoJoin / StopAfterStage5 /
            // ExpectReconciledInput, because it is neither a fan-out nor a join; it is a fifth
            // thing, and an exclusion list cannot know about the fifth thing.
            //
            // Naming what is admitted fails CLOSED: a task added later is excluded until someone
            // decides otherwise, which is the direction a predicate guarding a memory shape
            // should fail in.
            // ModelDiagnostics is admitted for the same reason PerFileRescore is: it consumes
            // the per-run survivor loader and nothing else. Admitting it is what stops it
            // falling to the all-runs bundle, which retains every run's survivors and grew
            // 0.10 GB/file on a 446-run cohort - past a 63.7 GB box by file ~310, measured
            // 2026-09-10. The report itself is unaffected either way, so the symptom was
            // memory alone and no gate could see it: mode 7 covers this task but runs 3 files,
            // where an O(files) bundle is free.
            //
            // Safe now for the reason the --model-diagnostics paragraph below gives: the report
            // is FirstPassFDR's DECLARED OUTPUT, folded by FoldDiagnosticsOnly BEFORE Rehydrate
            // is reached, so the per-run arm cannot skip a regeneration the way it did when the
            // report was a side effect of whichever hydrate ran (Astral mode 7).
            if (config.SelectedTask.HasValue &&
                config.SelectedTask != HpcTask.PerFileRescore &&
                config.SelectedTask != HpcTask.ModelDiagnostics)
            {
                return false;
            }
            if (config.StopAfterStage5 || config.ExpectReconciledInput)
                return false;
            // --model-diagnostics is NOT excluded any more, and what changed is where the report
            // comes from rather than anything about this predicate.
            //
            // The exclusion was correct when written: the report was folded from pre-compaction
            // rows DURING the all-runs hydrate, so a per-run hydrate produced no report at all -
            // observed on an 86-run plate as exit 0, 86/86 reconciled parquets, `mdiag=True`, and
            // no HTML. A requested output vanishing while nothing fails is what "never
            // conditionally write an output artifact" forbids, so the mode was held on the old
            // path deliberately, trading memory for correctness.
            //
            // The report is now FirstPassFDR's DECLARED OUTPUT, produced by its own bounded fold
            // (FoldDiagnosticsOnly) rather than as a side effect of whichever hydrate happened to
            // run. A cold analysis folds it in the score pass before this predicate is ever
            // consulted; a resume missing it enters Run and folds it there. Either way the report
            // no longer depends on this answer, so the mode stops paying an O(runs) startup for a
            // coupling that no longer exists.
            //
            // The gate check is mode 3's per-run-hydrate leg, which SKIPPED on all three
            // --model-diagnostics datasets for exactly this reason and must now run and pass.
            return PerRunSurvivorLoaderAvailable(config);
        }

        /// <summary>
        /// Whether the per-run survivor loader can be BUILT at all: the analysis-wide retained
        /// base_id summary is on disk in a shape this build reads. It is what the loader is
        /// assembled from, so its absence means the bounded route does not exist here - no
        /// output blib to name it after, or a summary written by a build with a different
        /// <c>FormatVersion</c>.
        ///
        /// <para>Split out of <see cref="CanHydratePerRun"/> because the two halves of that
        /// predicate answer different questions and one caller needs them apart. The terms above
        /// it are about the ROUTE - which task this is, what it consumes - and a false there
        /// means the run declined a bounded alternative that exists. This term is about DISK
        /// state, and a false here means there was nothing to decline. Only the first is a
        /// defect; see <see cref="AllRunsBundleGuardError"/>, which refuses one and not the
        /// other.</para>
        /// </summary>
        internal static bool PerRunSurvivorLoaderAvailable(OspreyConfig config)
        {
            string path = RetainedBaseIdSidecar.PathFor(config.OutputBlib, ArtifactSiblingPath(config));
            return !string.IsNullOrEmpty(path) && RetainedBaseIdSidecar.IsCurrentFormat(path);
        }

        /// <summary>
        /// Every task that starts AFTER Stage 4 - the two joins and the rescore worker. They
        /// are handed a directory of per-run artifacts rather than spectra, so
        /// <see cref="PerFileScoringTask"/> does not run for them; a consumer materializes
        /// its state through <c>ctx.Demand</c>, which routes to its disk load.
        ///
        /// <para>This used to be asked as "were parquets supplied on the command line", which
        /// is the INPUT KIND - the Rust pipeline's way of saying Stage 1-4 was done. The port
        /// says it with <c>--task</c>, and the two seams disagreeing is what let
        /// <c>--task ModelDiagnostics</c> join the pipeline and demand state a diagnostics
        /// fold never publishes. One question, asked of the task.</para>
        ///
        /// <para><c>ModelDiagnostics</c> is deliberately NOT here. It is neither a fan-out nor
        /// a join but a render over retained products, and it needs the per-file load to have
        /// happened - which it did by taking <c>-i</c> even while the others took parquets.
        /// That asymmetry was the first symptom of the two seams, and it survives the
        /// retirement as an ordinary membership fact rather than as an input-kind accident.</para>
        /// </summary>
        internal static bool StartsAfterPerFileScoring(OspreyConfig config)
        {
            switch (config.SelectedTask)
            {
                case HpcTask.FirstPassFdr:
                case HpcTask.PerFileRescore:
                case HpcTask.SecondPassFdr:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Which per-run parquet THIS task reads its rows from: the Stage 6
        /// <c>.scores-reconciled.parquet</c> for <c>SecondPassFDR</c>, the Stage 4
        /// <c>.scores.parquet</c> for the two tasks that run before Stage 6 has written one.
        ///
        /// <para>A property of the TASK, not of what happens to be on disk. It used to be
        /// decided by probing for the reconciled sibling and taking it where it existed,
        /// which gives the right answer only because the pipeline happens to run the stages
        /// in order - the file is absent before Stage 6 and present after. Re-run
        /// <c>--task FirstPassFDR</c> over a directory a previous run completed and the same
        /// probe hands the FIRST pass the survivor SUBSET, roughly 1/52 of its rows, with
        /// nothing to reject it: the version, search and library hashes all match. It then
        /// writes cohort-wide boundary artifacts from that subset and exits 0.</para>
        ///
        /// <para>The two answers are also what an HPC node is SHIPPED. Boundary 3 -&gt; 4 sends
        /// a <c>SecondPassFDR</c> node the reconciled parquets and not the Stage 4 originals -
        /// <c>regression.ps1</c>'s mode-3 chain deletes them from the worker directory before
        /// staging phase 4, so reaching for one fails on a missing file rather than passing
        /// quietly. A <c>FirstPassFDR</c> or <c>PerFileRescoring</c> node gets the originals
        /// and no reconciled sibling exists yet. So on a correct node each task has exactly
        /// one of the two present, and asking the disk cannot distinguish "the artifact for
        /// my pass" from "the only artifact here".</para>
        /// </summary>
        internal static bool ReadsReconciledScores(OspreyConfig config)
        {
            return config.SelectedTask == HpcTask.SecondPassFdr;
        }

        /// <summary>
        /// True when THIS process runs Stage 7's join, i.e. when a per-run source published for
        /// that join will actually be folded by something.
        ///
        /// <para>Names what is ADMITTED, so it fails closed: the straight-through pipeline (no
        /// <c>--task</c>, which runs every stage), the <c>SecondPassFDR</c> node, and
        /// <c>ModelDiagnostics</c> - which is not an HPC fan-out node but does let
        /// <c>SecondPassFDR</c> compute the pass-2 view, so it folds the same join and must not
        /// be pushed back onto the resident pool. A task added later is excluded until someone
        /// decides otherwise, which is the direction a predicate guarding a memory shape - and,
        /// since <see cref="Stage7StreamAdmittedBeforeRescore"/>, a correctness one - should
        /// fail in.</para>
        ///
        /// <para>The excluded tasks each have a consumer that never arrives.
        /// <c>PerFileScoring</c> and <c>SpectraCache</c> stop before Stage 5.
        /// <c>FirstPassFDR</c> would publish empty per-run lists for a fold that never runs.
        /// A <c>PerFileRescoring</c> worker exits after Stage 6, so a source built there is
        /// never pulled - it only pays for a retained-sidecar read the "entering
        /// PerFileRescoring must cost the same for 1 run as for 446" contract forbids, and
        /// emits a streamed-join marker into a log for a join that did not happen.</para>
        /// </summary>
        internal static bool RunsStage7Join(OspreyConfig config)
        {
            if (!config.SelectedTask.HasValue)
                return true;
            return config.SelectedTask == HpcTask.SecondPassFdr ||
                   config.SelectedTask == HpcTask.ModelDiagnostics;
        }

        /// <summary>
        /// Each input's scores parquet for THIS task, in input order - see
        /// <see cref="ReadsReconciledScores"/> for which one that is. Reached only by the
        /// three tasks <see cref="StartsAfterPerFileScoring"/> names, so every case has an
        /// answer.
        ///
        /// <para>The derivation <c>--input-scores</c> used to be handed ready-made. Its
        /// directory form globbed a directory and preferred the reconciled sibling per stem;
        /// this is the same list built from the runs the command line names instead of from
        /// whatever a directory happened to hold. The difference matters three times: a
        /// directory with a stray parquet no longer changes the cohort; ORDER is now the
        /// caller's (FirstPassFDR reconciliation is order-sensitive, so a chain must pass a
        /// deterministically sorted list - which is what it already did to get a stable
        /// directory sort); and the per-stem preference is no longer part of it.</para>
        /// </summary>
        internal static List<string> ScoresPathsForInputs(OspreyConfig config)
        {
            var paths = new List<string>(config.InputFiles?.Count ?? 0);
            if (config.InputFiles == null)
                return paths;
            bool reconciled = ReadsReconciledScores(config);
            foreach (string input in config.InputFiles)
            {
                paths.Add(reconciled
                    ? ParquetScoreCache.GetReconciledScoresPath(input)
                    : ParquetScoreCache.GetScoresPath(input));
            }
            return paths;
        }

        /// <summary>
        /// True when the <c>--task SecondPassFDR</c> merge may hand Stage 7 a per-run source
        /// instead of every run's survivors at once.
        ///
        /// <para>This is <see cref="CanHydratePerRun"/>'s answer for the OTHER leg, and the two
        /// are deliberately separate predicates rather than one with a wider admission. They
        /// name different consumers: that one asks whether the RESCORE can hydrate a run at a
        /// time, and excludes <c>ExpectReconciledInput</c> because a Stage 7 node runs no
        /// rescore; this one asks whether the JOIN can fold a run at a time, and admits only
        /// that leg. Widening the first would have told the rescore it may stream on a leg where
        /// it does not run at all.</para>
        ///
        /// <para>Three requirements, and the first is the one that is easy to miss. Every run's
        /// <c>.scores-reconciled.parquet</c> has to be on disk in the survivor-subset shape,
        /// because that parquet IS what a run is rebuilt from and dropped again. It used to be
        /// asked as <c>config.ExpectReconciledInput</c>, a PROXY for it: only
        /// <c>--task SecondPassFDR</c> sets that flag, so the one route the #4486 repro used
        /// became the only route that could stream - while a straight-through run, whose
        /// Stage 6 had just written those same parquets, met the requirement and was refused
        /// anyway (91.1 GB private, measured on a 446-run resume). Asked of the disk it is
        /// route-independent, which is also what lets <c>--input-scores</c> retire without
        /// taking the streamed join with it.</para>
        ///
        /// <para>No consumer may read PIN features off these stubs
        /// (<c>PerFileScoringTask.NeedsResidentPool</c>: <c>--fdrbench-pass 1</c>, a
        /// non-Percolator FDR method, <c>OSPREY_FDR_PROJECTION=0</c>) - a streamed pool drops
        /// the entries those consumers index. And the analysis-wide retained base_id summary has
        /// to be on disk, because it IS the compaction predicate every refill applies; without
        /// it a refilled run would carry the pre-compaction pool and the fold would run over a
        /// set ~52x too large. Its absence returns false here rather than failing, for the
        /// reason its sibling gives.</para>
        ///
        /// <para><b>What the streamed join is worth</b>, kept here because this is where the
        /// choice is made - it used to live on the <c>OSPREY_STAGE7_STREAM</c> switch and would
        /// have been deleted with it. The all-runs survivor pool is what a
        /// <c>--task SecondPassFDR</c> node spends its whole memory budget on before the join
        /// computes anything: at 446 CHS runs it reached 68.0 GB managed / 70.5 GB private and
        /// was killed at run 381 of 446 with 0.34 GB free, still inside the
        /// <c>--input-scores</c> load. It is the <c>O(runs x entries)</c> shape the architecture
        /// forbids a join to hold, and every consumer of it in Stage 7 - the fragment release,
        /// the pass-2 competition, protein FDR, the experiment-q re-clamp and all three blib
        /// gates - is a fold to <c>O(distinct)</c> that never needed the whole pool.</para>
        /// </summary>
        internal static bool CanStreamStage7Join(OspreyConfig config)
        {
            // LAST, because AllReconciledParquetsCurrent is the only term that opens a file per
            // run. Every cheaper disqualifier returns first, so a run that was never going to
            // stream does not pay 446 footer reads to be told so.
            return Stage7StreamAdmittedBeforeRescore(config) &&
                   AllReconciledParquetsCurrent(config);
        }

        /// <summary>
        /// Every <c>CanStreamStage7Join</c> term EXCEPT the reconciled parquets, i.e. the half a
        /// run can answer BEFORE Stage 6 has written them.
        ///
        /// <para>Split out for exactly one caller: the straight-through <c>Run</c> arm of
        /// <c>PerFileRescoreTask</c>, which decides whether to publish a per-run source at the
        /// TOP of Stage 6, hours before the rescore it is about to perform writes the parquets
        /// the full predicate asks about. Asking the full question there answers "no" on every
        /// cold run - not because the run cannot stream, but because it has not got there yet.
        /// It does not need the term either: by the time that arm's source is pulled, Stage 6
        /// has written a reconciled parquet for every run, so the question the full predicate
        /// asks has an answer - and a run still missing one fails there rather than falling
        /// back to its Stage 4 parquet.</para>
        ///
        /// <para>The retained base_id summary IS in this half even though it is an artifact:
        /// FirstPassFDR writes it before any caller of either form runs, so it is answerable on
        /// every route at every point either question is asked.</para>
        /// </summary>
        internal static bool Stage7StreamAdmittedBeforeRescore(OspreyConfig config)
        {
            // FIRST, and it is a correctness term rather than an optimisation. Every other term
            // here describes the SHAPE of a Stage 7 join; none of them asks whether this process
            // runs one. Without this, `--task FirstPassFDR` re-run over a COMPLETED directory
            // satisfies all of them - the reconciled parquets are present because a previous
            // pass wrote them - and PerFileScoringTask's `perRunJoin` branch then publishes one
            // EMPTY list per run for a fold that never comes. FirstPassFDR computes its pass over
            // nothing and rewrites both boundary sidecars and the retained base_id summary as
            // empty, exit 0. The `ExpectReconciledInput` term this predicate replaced made that
            // unreachable for anything but `--task SecondPassFDR`, so the hole opened when the
            // proxy went and nothing took over the question it had been answering incidentally.
            if (!RunsStage7Join(config))
                return false;
            // OSPREY_STAGE7_STREAM=0 was the second term and is GONE (2026-09-10). It let an
            // operator force the resident join as an A/B oracle; that A/B is banked and the
            // switch retired, so the streamed join is the only arm anyone can ask for. What is
            // left below is not a choice - it is the set of configurations that already hold a
            // resident pool for a declared reason, and they are named by token.
            if (PerFileScoringTask.NeedsResidentPool(config, OspreyEnvironment.UseFdrProjection))
                return false;
            // --model-diagnostics WAS the fourth requirement, and is no longer one. The pass-2
            // report is now folded run by run through ModelDiagnosticsData.Accumulator - the
            // same accumulator the pass-1 report uses - with the co-assignment panel's two
            // phases driven from the join's own stream passes, so the report no longer needs a
            // list it can index by position. Removing this term is also what lets the gate SEE
            // the streamed arm: --model-diagnostics is set on StellarLibDecoy,
            // StellarGenDecoyEntrap and Astral, so while it stood here mode 3's phase 4 took the
            // resident path on three of the four datasets and a streamed-arm defect needing
            // library decoys, entrapment or hram data passed the suite green.
            // The pass-2 mode has to be the one whose per-run half already ran in the
            // fan-out. protein-compact owns its whole per-file cycle in Stage 6 and Stage 7
            // folds the written answers; every other mode still computes the per-file half HERE,
            // over the whole pool - RestorePass1Scalars, the resident second pass and the
            // projection sink's per-file protein-q map all index it. Streaming underneath them
            // does not make them per-run, it just takes their input away: the fragment release
            // streams first and drops the pool, and ComputeAndPersist then throws
            // "Value was read after StreamFiles dropped the survivor pool" hours into Stage 7.
            //
            // Not a guess about which modes are safe - the same predicate ComputeAndPersist
            // itself branches on for `frozenCompetition`. When transfer's per-run half moves to
            // Pass2PerFileWorker this term becomes "any mode with a worker" and the two move
            // together.
            if (!OspreyEnvironment.Pass2ProteinCompact)
                return false;
            return PerRunSurvivorLoaderAvailable(config);
        }

        /// <summary>
        /// True when EVERY input has a <c>.scores-reconciled.parquet</c> on disk that this build
        /// can read in the survivor-subset shape - the disk-side question
        /// <c>config.ExpectReconciledInput</c> used to stand in for.
        ///
        /// <para>ALL, not any. The fold rebuilds each run from its own reconciled parquet, so
        /// one run without a readable one is a run the fold cannot produce - and admitting the
        /// stage on "some run has one" would fail at that run, hours in. The sibling question
        /// "did Stage 6 rescore anything", which decides whether a second Percolator pass is
        /// owed, is <c>SecondPassFdrTask.AnyReconciledParquet</c> and is deliberately not this:
        /// a file with no rescore work still gets a faithful copy written for it, which is what
        /// makes "all present" reachable on any route.</para>
        ///
        /// <para>Empty or absent inputs return FALSE rather than vacuously true. There is no
        /// pool to bound on a run with no inputs, so the streamed arm buys nothing there, and
        /// vacuous truth would hand the fold an empty file set on a configuration nothing else
        /// in this predicate examines.</para>
        /// </summary>
        internal static bool AllReconciledParquetsCurrent(OspreyConfig config)
        {
            if (config.InputFiles == null || config.InputFiles.Count == 0)
                return false;
            foreach (var input in config.InputFiles)
            {
                // From the INPUT stem, the same derivation AnyReconciledParquet uses, so this
                // reads identically in the in-process pipeline (Stage 6 has just written the
                // parquets) and on a --task SecondPassFDR node (a Stage 6 worker wrote them).
                if (!ParquetScoreCache.IsCurrentReconciledSurvivorSubset(
                        ParquetScoreCache.GetReconciledScoresPath(input)))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Refuse the ALL-RUNS reconciliation bundle, which retains every run's POST-compaction
        /// survivors at once and so grows O(files x entries) - 0.10 GB/file measured on a
        /// 446-run cohort, past a 63.7 GB box by file ~310.
        ///
        /// <para>This is the guard's invariant reaching PAST THE COMPACTION LINE.
        /// <see cref="PerFileScoringTask.ResidentPoolGuardError"/> enforces "no unnamed
        /// PRE-compaction pool" and stops there, which is why
        /// <see cref="ResidentPaths.COMPACTED_ENTRIES_BUFFER"/> had to be named rather than
        /// refused - no token could reach it. The bundle guarded here is on the same far side of
        /// that line, and it was reachable with no token and no disclosure at all: a run took an
        /// O(files x entries) route without declaring it, which is the one shape the named-token
        /// ratchet exists to make impossible.</para>
        ///
        /// <para>It takes NO token, deliberately. <see cref="ResidentPaths"/> may only shrink,
        /// and where this fires the bounded alternative is already on disk - the per-run
        /// survivor loader built from the analysis-wide retained base_id summary. A path that
        /// CAN stream and does not is a defect to fix, not a path to name, which is the
        /// disposition the hpc-merge and resume-survivor-handoff notes already record.</para>
        ///
        /// <para><b>Null where the bounded alternative does not exist</b>, which is the whole
        /// content of this guard. It first read <c>!CanHydratePerRun</c> as "an operator chose a
        /// resident route", and that predicate's false branch is half DISK STATE: no <c>-o</c>
        /// blib to name the summary after, or a summary this build cannot read. This guard has
        /// nothing to add there, on either load. Under a resident token master completes those
        /// runs through the overlay, which needs no summary, so refusing them is a regression;
        /// on the default lean load the streamed bundle needs the same summary and fails one
        /// call later with its own error naming the producer, so refusing first only puts a
        /// second, contradictory remedy above the real one - and this one named a remedy built
        /// FROM the very file whose absence triggered it. <c>WarnPreCompactionPool</c> records
        /// the disposition for the first shape: warn, because "every configuration that reaches
        /// here worked before the bounded hydrate existed, so failing them would be a
        /// regression, not a guard". What is left after the split is the route case - the
        /// loader is on disk and this run declined it - which is the
        /// <c>--task ModelDiagnostics</c> defect this branch fixes and is unreachable once it
        /// is fixed. That is the point: it fires only if a later edit re-opens the arm, which
        /// is the one thing no gate at 3 files can see.</para>
        /// </summary>
        internal static string AllRunsBundleGuardError(OspreyConfig config, string allowUnfixedResident)
        {
            // Disk state, not a choice: nothing to decline, so nothing to refuse. The caller
            // discloses the cost instead - it is about to take the O(files x entries) route
            // because this analysis has no bounded one, and that is worth saying out loud.
            if (!PerRunSurvivorLoaderAvailable(config))
                return null;
            // Named the same way the sibling guards name theirs, so a stale or misspelled token
            // cannot read as an unset one - even though no token can admit this path, an
            // operator who set one is owed the answer that it was not the problem.
            string supplied = string.IsNullOrWhiteSpace(allowUnfixedResident)
                ? string.Empty
                : string.Format(@" OSPREY_ALLOW_UNFIXED_RESIDENT is currently '{0}'; no token " +
                                @"admits this path.", allowUnfixedResident);
            return string.Format(
                @"This run is about to build the {0}, which holds every run's survivors at " +
                @"once and grows O(files x entries) - measured at 0.10 GB/file on a 446-run " +
                @"cohort, i.e. past a 63.7 GB box by file ~310. The bounded alternative " +
                @"exists: the per-run survivor loader, built from the analysis-wide retained " +
                @"base_id summary. Something that was streamed is resident again - fix that " +
                @"rather than allowing it. OSPREY_ALLOW_UNFIXED_RESIDENT cannot admit this " +
                @"path.{1}",
                RescoreHydration.ALL_RUNS_BUNDLE_MARKER, supplied);
        }

        /// <summary>
        /// The retained base_id set for the streamed second-pass join, or a hard failure.
        ///
        /// <para>Separate from <see cref="ReadRetainedBaseIds"/>'s null-returning form because
        /// the CALLER cannot degrade here. By the time Stage 7 asks, the <c>--input-scores</c>
        /// load has already published one EMPTY list per run on the strength of
        /// <c>CanStreamStage7Join</c> - which only header-probes the sidecar - so a null
        /// leaves the stage folding over 446 empty runs, logging "No entries pass FDR threshold.
        /// Creating empty blib." and exiting 0. An empty <c>.blib</c> from a successful-looking
        /// run is the worst outcome this pipeline can produce, and the sidecar's own reader
        /// documents its absence as FATAL.</para>
        /// </summary>
        internal static HashSet<uint> ReadRetainedBaseIdsOrFail(OspreyConfig config)
        {
            var retained = ReadRetainedBaseIds(config, out string error);
            if (retained != null)
                return retained;
            throw new InvalidDataException(string.Format(
                @"The second-pass join is streaming, which requires the analysis-wide retained " +
                @"base_id summary, and it could not be read: {0} Continuing would fold every run " +
                @"as empty and write an empty library.",
                error ?? @"(no reason reported)"));
        }

        /// <summary>
        /// Read the analysis-wide retained base_id summary FirstPassFDR left behind, or return
        /// null with <paramref name="error"/> set to an operator-facing message naming the
        /// producer.
        ///
        /// <para>Absence is FATAL to the caller and must not fall back to rebuilding the union
        /// from every run's <c>reconciliation.json</c>. That fallback is precisely the O(files)
        /// pre-pass this artifact exists to delete - 10.7 GB of envelope JSON on a 446-run
        /// cohort - so a quiet degradation to it would restore the behaviour without restoring
        /// any signal that it had happened. A directory written before this artifact existed is
        /// re-runnable, which costs a FirstPassFDR pass; a silent join is not detectable at
        /// all.</para>
        /// </summary>
        internal static HashSet<uint> ReadRetainedBaseIds(OspreyConfig config, out string error)
        {
            error = null;
            string path = RetainedBaseIdSidecar.PathFor(config.OutputBlib, ArtifactSiblingPath(config));
            if (string.IsNullOrEmpty(path))
            {
                error =
                    @"No output blib, so the analysis-wide retained base_id summary cannot be " +
                    @"located. It is written by FirstPassFDR and names itself after the blib.";
                return null;
            }
            var retained = RetainedBaseIdSidecar.Read(path);
            if (retained == null)
            {
                error = string.Format(
                    @"The analysis-wide retained base_id summary is missing or unreadable at {0}. " +
                    @"It is written by FirstPassFDR when Stage 6 planning ends, and every run's " +
                    @"compaction reads it; without it a run cannot be compacted without " +
                    @"re-reading every other run's reconciliation.json. Re-run the FirstPassFDR " +
                    @"phase for this analysis to produce it.", path);
                return null;
            }
            return retained;
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
