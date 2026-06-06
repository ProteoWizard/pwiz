/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.IO;
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// Per-file first-pass scoring phase: load the spectral library
    /// and generate decoys (Stage 1), then for each input mzML run
    /// the parse + RT calibration + main-search scoring pipeline
    /// (Stages 2-4) to produce a per-file
    /// <c>.scores.parquet</c>. The HPC "first per-file fan-out"
    /// boundary in the <c>Osprey-workflow.html</c> view -- each input
    /// file's Stage 1-4 work is independent of every other file's,
    /// so an HPC scheduler can fan this task out across N nodes and
    /// the merge node only needs the resulting parquet sidecars.
    ///
    /// Phase A scope: this task is a thin orchestration wrapper that
    /// delegates to AnalysisPipeline's existing private (now
    /// <c>internal</c>) methods (LoadLibrary, GenerateDecoys,
    /// ProcessFile) plus the --join-only / --join-at-pass=2 input
    /// loading paths that share the same per-file collection layout.
    /// The inline Stage 1-4 block from <c>AnalysisPipeline.Run</c>
    /// moved here verbatim; the only changes are LogInfo / LogWarning
    /// / LogError -> ctx.LogInfo etc. and a return-false / set
    /// ctx.ExitCode flow for the early-exit paths the original block
    /// had as <c>return 0</c> / <c>return 1</c>.
    ///
    /// Outputs (FullLibrary, LibraryById, PerFileEntries,
    /// PerFileCalibrations, PerFileParquetPaths) are exposed as
    /// instance properties for FirstJoinTask + downstream tasks to
    /// consume after this one completes successfully.
    /// </summary>
    internal sealed class PerFileScoringTask : AbstractScoringTask
    {
        private const double MIN_SNR_FOR_RT_CAL = 5.0;
        private const double MIN_COELUTION_CORR_SCORE = 0.5;
        private const int MIN_COELUTION_SPECTRA = 3;
        private const double CAL_FDR_THRESHOLD = 0.01;
        // Hard floor for LOESS refit in the two-pass calibration
        // refinement. Matches Rust's ABSOLUTE_MIN_CALIBRATION_POINTS
        // in pipeline.rs:652.
        private const int ABSOLUTE_MIN_CALIBRATION_POINTS = 50;

        /// <summary>
        /// Result of one calibration scoring pass (scoring + LDA +
        /// S/N filter + LOESS fit).
        /// </summary>
        private class CalibrationPassResult
        {
            public RTCalibration Calibration;
            public RTCalibrationStats Stats;
            public MzCalibrationResult Ms1Calibration;
            public MzCalibrationResult Ms2Calibration;
            // Total matches scored in this pass (before any q-value or S/N
            // filtering). Plumbed into CalibrationMetadata.NumSampledPrecursors
            // for parity with Rust's accumulated_matches.len().
            public int MatchCount;
            // (lib_rt, measured_rt) pairs that were actually fed to the LOESS
            // fit for this pass. Exposed so the caller can emit the
            // OSPREY_DUMP_LOESS_INPUT diagnostic only for the pass whose
            // calibration is actually used (pass 1 always; pass 2 only on
            // acceptance) -- mirroring Rust pipeline.rs's "dump reflects
            // the calibration actually used" semantics.
            public double[] LibRts;
            public double[] MeasuredRts;
        }

        public override string Name => @"PerFileScoring";

        /// <summary>
        /// Computes per-file scores from spectra only when no per-file scores
        /// were supplied via --input-scores. Under --input-scores it is
        /// excluded: a downstream task lazy-rehydrates the supplied scores
        /// through <c>ctx.Demand&lt;PerFileScoringTask&gt;()</c>.
        /// </summary>
        public override bool IsIncluded(PipelineContext ctx)
        {
            bool inputs = ctx.Config.InputScores != null && ctx.Config.InputScores.Count > 0;
            return !inputs;
        }

        // Stage 1-4 byproducts this task publishes for downstream consumers to
        // pull by type. ScoredEntries is the first milestone of the shared
        // mutable entry buffer (FirstJoin and PerFileRescore publish the later
        // CompactedEntries / RescoredEntries milestones of the same backing
        // list); see PipelineByproducts.cs.
        public override IEnumerable<Type> Publishes => new[]
        {
            typeof(FullLibrary), typeof(LibraryById), typeof(PerFileCalibrations),
            typeof(PerFileParquetPaths), typeof(RescoreBundle), typeof(ScoredEntries)
        };

        // Outputs reached by downstream tasks through ctx.Demand<PerFileScoringTask>().
        // Defaults are non-null empty collections so callers querying
        // outputs from a not-yet-run task never NPE on the accessor.
        private List<LibraryEntry> _fullLibrary = new List<LibraryEntry>();
        private Dictionary<uint, LibraryEntry> _libraryById = new Dictionary<uint, LibraryEntry>();
        private List<KeyValuePair<string, List<FdrEntry>>> _perFileEntries
            = new List<KeyValuePair<string, List<FdrEntry>>>();
        private ConcurrentDictionary<string, RTCalibration> _perFileCalibrations
            = new ConcurrentDictionary<string, RTCalibration>();
        private Dictionary<string, string> _perFileParquetPaths
            = new Dictionary<string, string>();
        // Probe-the-disk hydration bundle: populated when the joinOnly
        // dispatch finds every parquet's sibling .1st-pass.fdr_scores.bin
        // sidecar already on disk. Null otherwise. Carries the reconciliation
        // state that the worker-mode RescoreHydration.HydrateForRescore
        // produces, sharing it with FirstJoinTask's reconciliation accessors
        // so the worker entry-path collapse (next commit) does not need
        // a separate code path.
        private RescoreInputs _rescoreInputs;

        // The backing fields above are built and mutated ONLY inside this task
        // (during Run / hydration) and published once in FinalizeAndCheck as the
        // FullLibrary / LibraryById / PerFileCalibrations / PerFileParquetPaths /
        // ScoredEntries / RescoreBundle byproducts; downstream tasks pull them by
        // type via ctx.Get<T>() rather than through producer-typed getters.
        //
        // _perFileEntries stays a live, mutable, shared
        // List<KeyValuePair<string, List<FdrEntry>>>: FirstJoin compacts it and
        // PerFileRescore overlays it in place on this one instance (the no-copy
        // hand-off is load-bearing at Astral scale). Its three in-place
        // milestones are the ScoredEntries / CompactedEntries / RescoredEntries
        // byproduct types -- see PipelineByproducts.cs.
        //
        // The FullLibrary byproduct wraps List<LibraryEntry> rather than
        // IReadOnlyList: tightening it would cascade through RunCoelutionScoring /
        // RunFdr / ProteinFdr signatures, out of proportion to the value.
        // _rescoreInputs is the probe-the-disk reconciliation bundle (null at a
        // Stage-5 entry / any non-joinOnly run), published wrapped in RescoreBundle.

        // Phase B resume surface: the library and every input mzML are
        // read; per-file .scores.parquet + .calibration.json are written.
        // ValidityKey is the default (search + library hashes) -- those
        // are the only parameters that affect per-file scoring output.
        public override IEnumerable<string> Inputs(PipelineContext ctx)
        {
            if (ctx.Config.LibrarySource != null && !string.IsNullOrEmpty(ctx.Config.LibrarySource.Path))
                yield return ctx.Config.LibrarySource.Path;
            if (ctx.Config.InputFiles != null)
                foreach (var input in ctx.Config.InputFiles)
                    yield return input;
        }

        public override IEnumerable<string> Outputs(PipelineContext ctx)
        {
            if (ctx.Config.InputFiles == null) yield break;
            foreach (var input in ctx.Config.InputFiles)
            {
                yield return ParquetScoreCache.GetScoresPath(input);
                string calDir = Path.GetDirectoryName(Path.GetFullPath(input)) ?? @".";
                yield return CalibrationIO.CalibrationPathForInput(input, calDir);
            }
        }

        public override bool Run(PipelineContext ctx)
        {
            // Compute path (Stages 1-4): load the library and score every
            // input mzML from spectra. The worker-mode disk-load counterpart
            // (--input-scores) lives in Rehydrate; the driver reaches this task
            // here only in the non---input-scores modes (where computing from
            // spectra is right), and a worker-mode consumer materializes it via
            // ctx.Demand, which routes to Rehydrate.
            _ctx = ctx;
            var config = ctx.Config;

            // Stage 1: Load library + generate/pair decoys, then build the
            // full target+decoy library and its by-id lookup.
            if (!LoadLibraryAndDecoys(config, out var fullLibrary))
                return false;

            // Stage 2-4: Per-file calibration + coelution scoring
            // Process files in parallel when multiple files are provided.
            var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>();
            // Per-file RT calibration handles harvested by ProcessFile so
            // Stage 6 reconciliation has the live RTCalibration objects.
            var perFileCalibrations = new ConcurrentDictionary<string, RTCalibration>();
            // fileName -> .scores.parquet path, populated below so Stage 6
            // reconciliation can lazily load CWT candidates per file via
            // ParquetScoreCache.LoadCwtCandidatesFromParquet.
            var perFileParquetPaths = new Dictionary<string, string>();

            int nFiles = config.InputFiles.Count;

            // Pre-compute the parquet footer metadata ONCE, against the
            // unmutated outer config. ProcessFile clones the config per
            // file and mutates FragmentTolerance during MS2 calibration,
            // so reading config.Identity.SearchParameterHash() inside ProcessFile
            // would produce a hash that the join-only validator would
            // not recognize. Built unconditionally because Stage 6
            // reconciliation needs the per-file .scores.parquet on disk to
            // lazily load CWT candidates -- matches Rust's end-to-end
            // behavior, which always writes the parquet sidecar regardless
            // of --no-join.
            var parquetFooterMetadata = new Dictionary<string, string>
            {
                { @"osprey.version", Program.VERSION },
                { @"osprey.search_hash", config.Identity.SearchParameterHash() },
                { @"osprey.library_hash", config.Identity.LibraryIdentityHash() },
                { @"osprey.reconciled", @"false" },
            };

            // File-level parallelism is configurable via
            // OSPREY_MAX_PARALLEL_FILES. See AnalysisPipeline (and
            // Osprey-workflow.html) for the policy.
            int maxParallelFiles = OspreyEnvironment.MaxParallelFiles;

            // Determine how many files will actually run concurrently so
            // ProcessFile can divide the inner main-search thread budget
            // and avoid oversubscription. Stored on the per-run RunPlan
            // (driver-owned run state), not on the parsed OspreyConfig.
            if (nFiles == 1 || maxParallelFiles == 1)
                ctx.RunPlan.EffectiveFileParallelism = 1;
            else if (maxParallelFiles > 1)
                ctx.RunPlan.EffectiveFileParallelism = Math.Min(maxParallelFiles, nFiles);
            else
                ctx.RunPlan.EffectiveFileParallelism = Math.Min(nFiles, Environment.ProcessorCount);

            var swAllFiles = Stopwatch.StartNew();
            if (config.InputFiles.Count == 1)
            {
                // Single file: process directly (no parallel overhead)
                string inputFile = config.InputFiles[0];
                string fileName = Path.GetFileNameWithoutExtension(inputFile);
                string validityKey = ValidityKey(ctx);
                var fileResult = ScoreOrLoadForFile(
                    inputFile, fileName, 0, 1,
                    fullLibrary, config, parquetFooterMetadata,
                    perFileCalibrations, validityKey, ctx);
                if (fileResult != null)
                    perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, fileResult));
            }
            else if (maxParallelFiles == 1)
            {
                // Strictly sequential: one file at a time. Matches the
                // memory envelope of the single-file path while still
                // sharing the library load. Useful for 3-file Astral
                // runs that would OOM in parallel.
                ctx.LogInfo(string.Format(
                    @"[BENCH] OSPREY_MAX_PARALLEL_FILES=1 - processing {0} files sequentially",
                    config.InputFiles.Count));
                string validityKey = ValidityKey(ctx);
                for (int fileIdx = 0; fileIdx < config.InputFiles.Count; fileIdx++)
                {
                    string inputFile = config.InputFiles[fileIdx];
                    string fileName = Path.GetFileNameWithoutExtension(inputFile);
                    var fileResult = ScoreOrLoadForFile(
                        inputFile, fileName, fileIdx, config.InputFiles.Count,
                        fullLibrary, config, parquetFooterMetadata,
                        perFileCalibrations, validityKey, ctx);
                    if (fileResult != null)
                        perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, fileResult));
                }
            }
            else
            {
                // Multiple files: process in parallel, optionally
                // capped via OSPREY_MAX_PARALLEL_FILES.
                var parallelOpts = new ParallelOptions();
                if (maxParallelFiles > 1)
                {
                    parallelOpts.MaxDegreeOfParallelism = maxParallelFiles;
                    ctx.LogInfo(string.Format(
                        @"[BENCH] OSPREY_MAX_PARALLEL_FILES={0} - capping parallel file count",
                        maxParallelFiles));
                }
                string validityKey = ValidityKey(ctx);
                var fileResults = new ConcurrentDictionary<int, KeyValuePair<string, List<FdrEntry>>>();
                Parallel.For(0, config.InputFiles.Count, parallelOpts, fileIdx =>
                {
                    string inputFile = config.InputFiles[fileIdx];
                    string fileName = Path.GetFileNameWithoutExtension(inputFile);
                    var fileResult = ScoreOrLoadForFile(
                        inputFile, fileName, fileIdx, config.InputFiles.Count,
                        fullLibrary, config, parquetFooterMetadata,
                        perFileCalibrations, validityKey, ctx);
                    if (fileResult != null)
                        fileResults[fileIdx] = new KeyValuePair<string, List<FdrEntry>>(fileName, fileResult);
                });
                // Collect in original order
                for (int i = 0; i < config.InputFiles.Count; i++)
                {
                    if (fileResults.TryGetValue(i, out KeyValuePair<string, List<FdrEntry>> result))
                        perFileEntries.Add(result);
                }
            }
            swAllFiles.Stop();
            ctx.LogInfo(string.Format(@"[TIMING] All files processed: {0:F1}s",
                swAllFiles.Elapsed.TotalSeconds));

            // Populate perFileParquetPaths from config.InputFiles so Stage 6
            // reconciliation can locate each file's freshly-written
            // .scores.parquet to lazy-load CWT candidates from. ProcessFile
            // always writes the parquet (parquetFooterMetadata is non-null).
            foreach (string inputFile in config.InputFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(inputFile);
                perFileParquetPaths[fileName] = ParquetScoreCache.GetScoresPath(inputFile);
            }

            int totalScored = 0;
            foreach (var kvp in perFileEntries)
                totalScored += kvp.Value.Count;

            ctx.LogInfo(string.Empty);
            ctx.LogInfo(string.Format(
                @"Coelution analysis complete. {0} total scored entries across {1} files",
                totalScored, nFiles));

            return FinalizeAndCheck(ctx, perFileEntries, perFileCalibrations,
                perFileParquetPaths, nFiles, totalScored);
        }

        public override bool Rehydrate(PipelineContext ctx)
        {
            // Without --input-scores there are no worker-supplied per-file
            // scores to load. A Demand still reaches this task here on a
            // straight-through resume: the driver skipped its Run because its
            // own .scores.parquet outputs were already valid on disk
            // (CanRehydrate), and a downstream task is the first to touch its
            // state. Load those valid parquets straight from disk (never
            // compute) so Rehydrate stays pure -- Run is outer-loop-only. The
            // worker-mode join-only disk-load below applies only when
            // --input-scores actually supplied the per-file scores.
            if (ctx.Config.InputScores == null || ctx.Config.InputScores.Count == 0)
                return RehydrateFromOwnOutputs(ctx);

            // Disk-load path for worker-mode entry (--input-scores): the
            // per-file Stage 2-4 scores already exist on disk, so load the
            // FdrEntry stubs + PIN features straight from the parquets
            // (Stage 1 library still loads -- Stage 5+ needs it) instead of
            // recomputing them from spectra, then adopt any reconciliation
            // bundle that the merge node will read. The compute-from-spectra
            // counterpart is Run.
            _ctx = ctx;
            var config = ctx.Config;

            // Stage 1: library + decoys (needed by Stage 5+ even though the
            // per-file scores are loaded rather than computed).
            if (!LoadLibraryAndDecoys(config, out _))
                return false;

            var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>();
            var perFileCalibrations = new ConcurrentDictionary<string, RTCalibration>();
            // fileName -> .scores.parquet path; LoadJoinOnlyScores already
            // knows each input parquet path and fills this in.
            var perFileParquetPaths = new Dictionary<string, string>();

            int nFiles = config.InputScores.Count;

            // InputFiles is synthesized from the --input-scores parquet stems
            // once at pipeline entry (AnalysisPipeline.Run), so downstream code
            // (Stage 6 rescore's fileNameToIdx in particular) already has the
            // synthetic input paths by the time this load runs.

            // Mirror Run's EffectiveFileParallelism bookkeeping (unused by the
            // disk-load path, which never calls ProcessFile, but kept so the
            // RunPlan reflects the same per-run state either way).
            int maxParallelFiles = OspreyEnvironment.MaxParallelFiles;
            if (nFiles == 1 || maxParallelFiles == 1)
                ctx.RunPlan.EffectiveFileParallelism = 1;
            else if (maxParallelFiles > 1)
                ctx.RunPlan.EffectiveFileParallelism = Math.Min(maxParallelFiles, nFiles);
            else
                ctx.RunPlan.EffectiveFileParallelism = Math.Min(nFiles, Environment.ProcessorCount);

            var swAllFiles = Stopwatch.StartNew();
            LoadJoinOnlyScores(config, perFileEntries, perFileParquetPaths, perFileCalibrations);
            swAllFiles.Stop();
            ctx.LogInfo(string.Format(@"[TIMING] All files processed: {0:F1}s",
                swAllFiles.Elapsed.TotalSeconds));

            int totalScored = 0;
            foreach (var kvp in perFileEntries)
                totalScored += kvp.Value.Count;

            ctx.LogInfo(string.Empty);
            ctx.LogInfo(string.Format(
                @"Coelution analysis complete. {0} total scored entries across {1} files",
                totalScored, nFiles));

            // Probe-the-disk reconciliation hydration: when every parquet
            // already has a sibling .1st-pass.fdr_scores.bin sidecar, load
            // the rescore bundle (1st-pass q-values overlay + reconciliation
            // actions + refined RT calibration + gap-fill targets) so the
            // worker hydration path and the in-pipeline path produce
            // identical post-Stage-5 state. Mirrors the worker's
            // RescoreHydration.HydrateForRescore but reuses the stubs +
            // PIN features already loaded above (so PIN features survive
            // for stage7's Percolator skip path). Stage 5 entry (no
            // sidecars present yet) skips this block and _rescoreInputs
            // stays null.
            //
            // The disk state determines the hydration shape, not the CLI
            // flag (Phase C principle: mechanism-driven, not flag-driven).
            if (!HydrateRescoreBundleIfPresent(config, perFileEntries))
                return false;

            return FinalizeAndCheck(ctx, perFileEntries, perFileCalibrations,
                perFileParquetPaths, nFiles, totalScored);
        }

        /// <summary>
        /// Pure load-from-own-outputs rehydrate for a straight-through resume:
        /// the driver skipped this task's <see cref="Run"/> because its per-file
        /// <c>.scores.parquet</c> outputs were already valid on disk
        /// (<see cref="PipelineContext.CanRehydrate"/>), and a downstream task is
        /// the first to touch its state. Load the library and each file's stubs +
        /// PIN features + calibration straight from those valid parquets -- the
        /// same disk-load <see cref="ScoreOrLoadForFile"/> takes on its fast-skip
        /// arm, but with no <see cref="ProcessFile"/> compute fallback, so a lazy
        /// Demand never triggers scoring (Run stays outer-loop-only). Produces
        /// the identical post-Stage-4 state Run leaves on a resume:
        /// <see cref="_rescoreInputs"/> stays null (a straight-through run wrote
        /// no reconciliation bundle), and the same byproducts publish through the
        /// shared <see cref="FinalizeAndCheck"/> tail. Because CanRehydrate gated
        /// the outputs as valid, a load failure here is a genuine fault (not a
        /// "fall back to rescore" case) and stops the pipeline.
        /// </summary>
        private bool RehydrateFromOwnOutputs(PipelineContext ctx)
        {
            _ctx = ctx;
            var config = ctx.Config;

            // Stage 1: library + decoys (needed by Stage 5+ even though the
            // per-file scores are loaded rather than computed).
            if (!LoadLibraryAndDecoys(config, out _))
                return false;

            var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>();
            var perFileCalibrations = new ConcurrentDictionary<string, RTCalibration>();
            var perFileParquetPaths = new Dictionary<string, string>();

            int nFiles = config.InputFiles?.Count ?? 0;

            // Mirror Run's EffectiveFileParallelism bookkeeping (unused by the
            // disk-load path, which never calls ProcessFile, but kept so the
            // RunPlan reflects the same per-run state either way).
            int maxParallelFiles = OspreyEnvironment.MaxParallelFiles;
            if (nFiles == 1 || maxParallelFiles == 1)
                ctx.RunPlan.EffectiveFileParallelism = 1;
            else if (maxParallelFiles > 1)
                ctx.RunPlan.EffectiveFileParallelism = Math.Min(maxParallelFiles, nFiles);
            else
                ctx.RunPlan.EffectiveFileParallelism = Math.Min(nFiles, Environment.ProcessorCount);

            var swAllFiles = Stopwatch.StartNew();
            if (config.InputFiles != null)
            {
                // Sequential in InputFiles order to match Run's "collect in
                // original order" -- downstream FirstJoin iterates perFileEntries.
                foreach (string inputFile in config.InputFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(inputFile);
                    string scoresPath = ParquetScoreCache.GetScoresPath(inputFile);
                    // Strict load: CanRehydrate already certified these outputs
                    // valid, so a failure is a genuine fault, not a "fall back to
                    // rescore" case -- TryLoadStubsAndCalibration logs it as an
                    // error (no misleading "will rescore" warning). Fail loudly;
                    // Rehydrate must not compute.
                    var stubs = TryLoadStubsAndCalibration(scoresPath, fileName, perFileCalibrations, ctx, resumeStrict: true);
                    if (stubs == null)
                    {
                        ctx.ExitCode = 1;
                        return false;
                    }
                    perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, stubs));
                    perFileParquetPaths[fileName] = scoresPath;
                }
            }
            swAllFiles.Stop();
            ctx.LogInfo(string.Format(@"[TIMING] All files processed: {0:F1}s",
                swAllFiles.Elapsed.TotalSeconds));

            int totalScored = 0;
            foreach (var kvp in perFileEntries)
                totalScored += kvp.Value.Count;

            ctx.LogInfo(string.Empty);
            ctx.LogInfo(string.Format(
                @"Coelution analysis complete. {0} total scored entries across {1} files",
                totalScored, nFiles));

            return FinalizeAndCheck(ctx, perFileEntries, perFileCalibrations,
                perFileParquetPaths, nFiles, totalScored);
        }

        /// <summary>
        /// Shared tail for <see cref="Run"/> and <see cref="Rehydrate"/>:
        /// surface the per-file outputs for downstream tasks (before any
        /// early-exit, so a partial-success caller still sees the populated
        /// collections), then apply the two success-but-stop boundaries --
        /// an empty score set (cannot run FDR) and <c>--no-join</c> (Stage
        /// 1-4 only). Returns <c>true</c> to continue the pipeline, or
        /// <c>false</c> with <see cref="PipelineContext.ExitCode"/> = 0 at
        /// either boundary.
        /// </summary>
        private bool FinalizeAndCheck(PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            ConcurrentDictionary<string, RTCalibration> perFileCalibrations,
            Dictionary<string, string> perFileParquetPaths,
            int nFiles, int totalScored)
        {
            _perFileEntries = perFileEntries;
            _perFileCalibrations = perFileCalibrations;
            _perFileParquetPaths = perFileParquetPaths;

            // Publish the Stage 1-4 byproducts once, in the shared Run/Rehydrate
            // tail, BEFORE the success-but-stop early exits below -- so a
            // downstream consumer pulling them by type (ctx.Get<T>) sees the
            // same values regardless of which path materialized this task, even
            // when this task then stops the pipeline. Because those stops keep
            // ExitCode == 0, a lazy Demand that drove this Rehydrate still gets
            // the published state and PipelineContext.DemandByType's
            // failure-throw (which fires only on a false return WITH ExitCode != 0)
            // correctly treats them as benign. RescoreBundle wraps the nullable
            // bundle (null at a Stage-5 entry / straight-through run).
            ctx.Publish(new FullLibrary(_fullLibrary));
            ctx.Publish(new LibraryById(_libraryById));
            ctx.Publish(new PerFileCalibrations(_perFileCalibrations));
            ctx.Publish(new PerFileParquetPaths(_perFileParquetPaths));
            ctx.Publish(new ScoredEntries(_perFileEntries));
            ctx.Publish(new RescoreBundle(_rescoreInputs));

            if (perFileEntries.Count == 0 || totalScored == 0)
            {
                ctx.LogWarning(@"No scored entries found. Cannot perform FDR control.");
                ctx.ExitCode = 0;
                return false;
            }

            // --no-join: stop here. Per-file `.scores.parquet` files are
            // now on disk; a separate `--join-only` invocation (typically
            // on a merge node) will pick them up and run Stage 5+.
            if (ctx.Config.NoJoin)
            {
                ctx.LogInfo(string.Format(
                    @"--task PerFileScoring: Stage 1-4 complete. {0} entries scored across {1} file(s). " +
                    @"Per-file `.scores.parquet` written next to each input mzML. " +
                    @"Skipping FDR and blib output.",
                    totalScored, nFiles));
                ctx.ExitCode = 0;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Stage 1: load the spectral library, then either accept
        /// library-supplied decoys (marking + target pairing) or generate
        /// decoys from the targets, and assemble the full target+decoy
        /// library plus its by-id lookup. Sets the <see cref="_fullLibrary"/>
        /// / <see cref="_libraryById"/> output fields and returns the full
        /// library via <paramref name="fullLibrary"/>. Returns false (with
        /// <see cref="PipelineContext.ExitCode"/> set) on an empty load,
        /// missing library decoys, an unreadable pairing manifest, or a
        /// pairing fraction below the configured threshold.
        /// </summary>
        private bool LoadLibraryAndDecoys(OspreyConfig config, out List<LibraryEntry> fullLibrary)
        {
            fullLibrary = null;

            var swLibrary = Stopwatch.StartNew();
            var library = LibraryLoader.Load(config, _ctx.LogInfo, _ctx.LogWarning);
            if (library == null || library.Count == 0)
            {
                _ctx.LogError(@"Library is empty after loading");
                _ctx.ExitCode = 1;
                return false;
            }

            // Decoys: either supplied by the library (DIA-NN / EncyclopeDIA
            // output with rev_ / DECOY_ prefixes) or generated by Osprey
            // from the targets. DecoyMethod.FromLibrary is treated as a
            // synonym for DecoysInLibrary -- historically it silently fell
            // through to Reverse generation, which was the bug behind
            // v26.5.3's library-decoy mode being effectively unusable.
            // Mark BEFORE counting targets so the count reflects post-
            // marking state and matches Rust pipeline.rs.
            bool librarySuppliesDecoys = config.DecoysInLibrary ||
                config.DecoyMethod == DecoyMethod.FromLibrary;
            if (librarySuppliesDecoys)
            {
                LibraryDecoyMarker.ApplyLibraryDecoyMarking(
                    library, config.DecoyPrefixes, out var markingStats);
                _ctx.LogInfo(string.Format(
                    @"Library-decoy mode: matched prefixes {0}",
                    FormatPrefixList(config.DecoyPrefixes)));
                _ctx.LogInfo(string.Format(
                    @"[COUNT] Library-decoy mode: {0} flagged ({1} via Decoy column, {2} via protein-accession prefix)",
                    markingStats.NMarked, markingStats.NViaColumn, markingStats.NViaPrefix));
            }

            int nLibraryTargets = 0;
            foreach (var entry in library)
            {
                if (!entry.IsDecoy)
                    nLibraryTargets++;
            }
            double libLoadSec = swLibrary.Elapsed.TotalSeconds;
            _ctx.LogInfo(string.Format(@"[COUNT] Library targets loaded: {0}", nLibraryTargets));

            List<LibraryEntry> decoys;
            if (config.ExpectReconciledInput)
            {
                // --join-at-pass=2: decoy LibraryEntries are unused
                // downstream. The reconciled parquet already carries
                // both target and decoy FDR rows with their stage-1-4
                // scores; Stage 5 is skipped, Stage 6 is skipped, and
                // the protein-parsimony / blib write paths both filter
                // on `entry.IsDecoy` (only target LibraryEntries get
                // looked up by entry_id). Skipping the rebuild saves
                // ~45s on Astral 1-file (BuildDecoyFromSequence +
                // RecalculateFragments dominated the Stage 7+blib
                // hotspot list). dotTrace OWN-time on Astral 1-file
                // Stage 7 cs run before this fix:
                //   BuildDecoyFromSequence  total=45665 ms (89% wall)
                //   GenerateDecoys.<>b__0   total=46792 ms
                decoys = new List<LibraryEntry>();
            }
            else if (!librarySuppliesDecoys)
            {
                decoys = GenerateDecoys(library, config, out List<LibraryEntry> validTargets);
                library = validTargets;
            }
            else
            {
                decoys = new List<LibraryEntry>();

                // Match Rust pipeline.rs at v26.6.0 (bcd7249): the
                // "no decoys at all" check runs BEFORE manifest
                // application. The manifest CAN flip predictor-stripped
                // entries to IsDecoy=true (the Carafe failure mode commit
                // d23d496 was built for), so this ordering means a
                // manifest cannot rescue a load that the prefix scan
                // misses entirely. TODO(brendanmaclean,maccoss): discuss
                // with Mike whether this should be relaxed to defer the
                // check until after manifest application; current C#
                // ordering matches Rust v26.6.0 for byte parity on the
                // cross-impl Test-Regression gate.
                int nLibraryDecoys = library.Count - nLibraryTargets;
                if (nLibraryDecoys == 0)
                {
                    _ctx.LogError(string.Format(
                        @"decoys_in_library mode requested but no library entries match prefixes {0}. " +
                        @"Check that the library actually contains decoys with one of these prefixes on " +
                        @"a protein accession, or unset decoys_in_library so Osprey generates decoys.",
                        FormatPrefixList(config.DecoyPrefixes)));
                    _ctx.ExitCode = 1;
                    return false;
                }

                // Pair each decoy with its target so their base_ids match
                // -- required for SVM target-decoy competition, LDA
                // calibration, and CV fold grouping. Hybrid path:
                // manifest first when provided (exact pairs from
                // FDRBench), composition fallback for whatever the
                // manifest doesn't cover. Net result on real Carafe-
                // generated entrapment libraries: ~30% via manifest,
                // ~70% via composition, >99% total.
                var pairingState = new PairingState();
                LibraryDecoyPairing.CountTargetsAndDecoys(library,
                    out int nTargetsForStats, out int nDecoysForStats);
                var pairingStats = new PairingStats
                {
                    NTargets = nTargetsForStats,
                    NDecoys = nDecoysForStats,
                };
                if (!string.IsNullOrEmpty(config.DecoyPairingManifestPath))
                {
                    _ctx.LogInfo(string.Format(
                        @"Loading decoy pairing manifest from {0}",
                        config.DecoyPairingManifestPath));
                    DecoyPairingManifest manifest;
                    try
                    {
                        manifest = DecoyPairingManifest.FromTsv(
                            config.DecoyPairingManifestPath);
                    }
                    catch (Exception ex)
                    {
                        _ctx.LogError(string.Format(
                            @"Failed to read decoy pairing manifest {0}: {1}",
                            config.DecoyPairingManifestPath, ex.Message));
                        _ctx.ExitCode = 1;
                        return false;
                    }
                    var manifestStats = manifest.ApplyToLibrary(library, pairingState);
                    pairingStats.NPairedViaManifest = manifestStats.NPaired;
                    if (manifestStats.NProteinsReplaced > 0)
                    {
                        _ctx.LogInfo(string.Format(
                            @"Library-decoy mode: manifest replaced protein_ids on {0} library " +
                            @"entries (clean source-protein accessions from the manifest's " +
                            @"`proteins` column)",
                            manifestStats.NProteinsReplaced));
                    }
                    if (manifestStats.NNewlyMarkedDecoy > 0)
                    {
                        // Manifest classified entries as decoy that were
                        // loaded as targets (the predictor stripped the
                        // decoy prefix). Update the decoy count so the
                        // pairing fraction is honest.
                        _ctx.LogInfo(string.Format(
                            @"Library-decoy mode: manifest classified {0} additional library " +
                            @"entries as decoys (their protein accessions lacked a decoy prefix)",
                            manifestStats.NNewlyMarkedDecoy));
                        LibraryDecoyPairing.CountTargetsAndDecoys(library,
                            out nTargetsForStats, out nDecoysForStats);
                        pairingStats.NTargets = nTargetsForStats;
                        pairingStats.NDecoys = nDecoysForStats;
                    }
                }
                else
                {
                    _ctx.LogInfo(
                        @"Pairing library decoys to targets by amino-acid composition " +
                        @"(no manifest provided).");
                }
                pairingStats.NPairedViaComposition =
                    LibraryDecoyPairing.PairLibraryDecoysByComposition(
                        library, config.DecoyPrefixes, pairingState);
                pairingStats.NPaired = pairingStats.NPairedViaManifest +
                    pairingStats.NPairedViaComposition;
                // Defense-in-depth saturating subtract (matches Rust's
                // saturating_sub intent; not load-bearing).
                pairingStats.NUnpairedDecoys = Math.Max(0,
                    pairingStats.NDecoys - pairingStats.NPaired);
                pairingStats.NUnpairedTargets = Math.Max(0,
                    pairingStats.NTargets - pairingState.ClaimedTargets.Count);
                _ctx.LogInfo(string.Format(
                    @"Library-decoy pairing: paired {0}/{1} decoys ({2:F1}%); " +
                    @"manifest={3}, composition={4}; {5} unpaired decoys, {6} unpaired targets",
                    pairingStats.NPaired, pairingStats.NDecoys,
                    pairingStats.PairedFraction * 100.0,
                    pairingStats.NPairedViaManifest, pairingStats.NPairedViaComposition,
                    pairingStats.NUnpairedDecoys, pairingStats.NUnpairedTargets));
                if (pairingStats.PairedFraction < config.DecoyPairMinFraction)
                {
                    _ctx.LogError(string.Format(
                        @"Library-decoy pairing failed: only {0:F1}% of decoys paired with a target " +
                        @"(threshold: {1:F0}%). FDR estimates would be unreliable without proper " +
                        @"target-decoy competition. Either supply a pairing manifest, ensure the " +
                        @"library uses matching protein accessions with one of `decoy_prefixes` " +
                        @"({2}), or unset `decoys_in_library` so Osprey generates its own decoys.",
                        pairingStats.PairedFraction * 100.0,
                        config.DecoyPairMinFraction * 100.0,
                        FormatPrefixList(config.DecoyPrefixes)));
                    _ctx.ExitCode = 1;
                    return false;
                }
            }
            swLibrary.Stop();
            double totalSec = swLibrary.Elapsed.TotalSeconds;
            _ctx.LogInfo(string.Format(@"[TIMING] Library loading + decoys: {0:F1}s (load: {1:F1}s, decoys: {2:F1}s)",
                totalSec, libLoadSec, totalSec - libLoadSec));

            _ctx.LogInfo(string.Format(@"[COUNT] Library decoys generated: {0}", decoys.Count));

            fullLibrary = new List<LibraryEntry>(library.Count + decoys.Count);
            fullLibrary.AddRange(library);
            fullLibrary.AddRange(decoys);

            _ctx.LogInfo(string.Format(@"Full library: {0} entries ({1} targets + {2} decoys)",
                fullLibrary.Count, library.Count, decoys.Count));
            _ctx.LogInfo(string.Format(@"[COUNT] Full library: {0} ({1} targets + {2} decoys)",
                fullLibrary.Count, library.Count, decoys.Count));

            // Count entries with few fragments (diagnostic for entry count parity)
            int nZeroFrag = 0, nOneFrag = 0, nTwoFrag = 0;
            foreach (var entry in fullLibrary)
            {
                int fc = entry.Fragments != null ? entry.Fragments.Count : 0;
                if (fc == 0)
                    nZeroFrag++;
                else if (fc == 1)
                    nOneFrag++;
                else if (fc == 2)
                    nTwoFrag++;
            }
            if (nZeroFrag + nOneFrag + nTwoFrag > 0)
                _ctx.LogInfo(string.Format(@"[COUNT] Entries with <3 fragments: {0} (0={1}, 1={2}, 2={3})",
                    nZeroFrag + nOneFrag + nTwoFrag, nZeroFrag, nOneFrag, nTwoFrag));

            // Build library lookup by ID for fast access
            var libraryById = new Dictionary<uint, LibraryEntry>(fullLibrary.Count);
            foreach (var entry in fullLibrary)
                libraryById[entry.Id] = entry;

            _fullLibrary = fullLibrary;
            _libraryById = libraryById;
            return true;
        }

        /// <summary>
        /// --join-only: load per-file FdrEntry stubs + PIN features directly
        /// from each <c>.scores.parquet</c> listed via <c>--input-scores</c>
        /// (skips the per-file Stage 2-4 scoring; Stage 1 library load
        /// already ran in <see cref="Run"/>), plus a best-effort
        /// calibration-JSON load per file for Stage 6 reconciliation.
        /// Validates the parquet group's
        /// hashes against the current library/search params first (throws
        /// <see cref="InvalidDataException"/> on mismatch). Populates
        /// <paramref name="perFileEntries"/>, <paramref name="perFileParquetPaths"/>,
        /// and <paramref name="perFileCalibrations"/>.
        /// </summary>
        private void LoadJoinOnlyScores(
            OspreyConfig config,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            Dictionary<string, string> perFileParquetPaths,
            ConcurrentDictionary<string, RTCalibration> perFileCalibrations)
        {
            // --join-only: load per-file FdrEntry stubs directly from
            // each .scores.parquet listed via --input-scores. Skips the
            // per-file Stage 2-4 scoring (Stage 1 library load already ran
            // in Run). Also loads a best-effort calibration JSON sibling
            // per file (the loop below) for Stage 6 reconciliation, like
            // the Rust impl.
            // Guard: hash check against current --library and search params.
            // Aborts with a clear, file-named error if the operator points
            // the merge node at parquets from a different scoring run.
            string validationError = ParquetScoreCache.ValidateScoresParquetGroup(
                config.InputScores, config, Program.VERSION, _ctx.LogWarning);
            if (validationError != null)
                throw new InvalidDataException(validationError);

            _ctx.LogInfo(string.Format(
                @"--input-scores: loading {0} per-file score parquet(s)",
                config.InputScores.Count));
            for (int fileIdx = 0; fileIdx < config.InputScores.Count; fileIdx++)
            {
                string parquetPath = config.InputScores[fileIdx];
                // Derive the bare input stem via the single shared suffix-strip
                // helper so a .scores-reconciled.parquet input maps to the same
                // fileName key as its .scores.parquet sibling (a naive trailing
                // ".scores" strip would leave the bogus key "<stem>.reconciled").
                string fileName = Path.GetFileNameWithoutExtension(
                    RescoreHydration.SyntheticInputFromParquet(parquetPath)) ?? string.Empty;
                _ctx.LogInfo(string.Format(@"===== Loading file {0}/{1}: {2} (from {3}) =====",
                    fileIdx + 1, config.InputScores.Count, fileName, parquetPath));
                var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(parquetPath);
                // Stage 5+ (Percolator SVM) requires the 21 PIN features
                // on each FdrEntry. Load them in lockstep with the stubs
                // and bind by row index (parquet rows are stable).
                var features = ParquetScoreCache.LoadPinFeaturesFromParquet(parquetPath);
                if (features.Count != stubs.Count)
                {
                    throw new InvalidDataException(string.Format(
                        @"--input-scores: parquet {0} has {1} stubs but {2} feature rows",
                        parquetPath, stubs.Count, features.Count));
                }
                for (int j = 0; j < stubs.Count; j++)
                    stubs[j].Features = features[j];
                _ctx.LogInfo(string.Format(@"  Loaded {0} FDR stubs + features", stubs.Count));
                perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, stubs));
                perFileParquetPaths[fileName] = parquetPath;

                // Best-effort calibration JSON load for Stage 6
                // reconciliation. Mirrors osprey/src/pipeline.rs:2573-2588.
                try
                {
                    string parquetDir = Path.GetDirectoryName(Path.GetFullPath(parquetPath));
                    if (parquetDir != null)
                    {
                        // fileName is the bare input stem (the scores /
                        // reconciled-scores suffix was stripped above via
                        // SyntheticInputFromParquet), so combining it with
                        // parquetDir yields the same input-stem path
                        // ProcessFile passes to CalibrationPathForInput.
                        string calStemPath = Path.Combine(parquetDir, fileName);
                        string calPath = CalibrationIO.CalibrationPathForInput(calStemPath, parquetDir);
                        if (File.Exists(calPath))
                        {
                            var calParams = CalibrationIO.LoadCalibration(calPath);
                            if (calParams.RtCalibration != null && calParams.RtCalibration.ModelParams != null)
                            {
                                var mp = calParams.RtCalibration.ModelParams;
                                if (OspreyDiagnostics.DumpCalibration)
                                {
                                    OspreyDiagnostics.WriteStage6CalibrationDump(
                                        fileName, mp.LibraryRts, mp.FittedRts);
                                }
                                var rtCal = RTCalibration.FromModelParams(
                                    mp.LibraryRts, mp.FittedRts, mp.AbsResiduals,
                                    calParams.RtCalibration.ResidualSD);
                                perFileCalibrations[fileName] = rtCal;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ctx.LogWarning(string.Format(@"  Failed to load calibration for {0}: {1}", fileName, ex.Message));
                }
            }
            if (OspreyDiagnostics.CalibrationOnly)
                OspreyDiagnostics.ExitAfterDump(@"OSPREY_CALIBRATION_ONLY");
        }

        /// <summary>
        /// Probe-the-disk reconciliation hydration: when every <c>--input-scores</c>
        /// parquet already has both a sibling .1st-pass.fdr_scores.bin and a
        /// .reconciliation.json, load the rescore bundle (1st-pass q-value
        /// overlay + reconciliation actions + refined RT calibration +
        /// gap-fill targets) into <see cref="_rescoreInputs"/> so the worker
        /// hydration path and the in-pipeline path produce identical
        /// post-Stage-5 state, and clear PIN features on the hydrated stubs.
        /// Returns false (with <see cref="PipelineContext.ExitCode"/> set) if
        /// hydration throws <see cref="InvalidDataException"/>; true otherwise
        /// (including when no sidecars are present yet and
        /// <see cref="_rescoreInputs"/> stays null). Other exception types
        /// propagate uncaught, matching the original inline behavior.
        /// </summary>
        private bool HydrateRescoreBundleIfPresent(
            OspreyConfig config,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries)
        {
            bool allHave1stPassAndRecon = true;
            foreach (var parquetPath in config.InputScores)
            {
                string syntheticInput = RescoreHydration.SyntheticInputFromParquet(parquetPath);
                if (!File.Exists(FdrScoresSidecar.Pass1Path(syntheticInput))
                    || !File.Exists(ReconciliationFile.PathForInput(syntheticInput)))
                {
                    allHave1stPassAndRecon = false;
                    break;
                }
            }
            if (allHave1stPassAndRecon)
            {
                try
                {
                    _rescoreInputs = RescoreHydration.HydrateReconciliationOverlay(
                        perFileEntries, config.InputScores);
                }
                catch (InvalidDataException ex)
                {
                    _ctx.LogError(string.Format(
                        @"--input-scores hydration failed: {0}", ex.Message));
                    _ctx.ExitCode = 1;
                    return false;
                }
                // Clear PIN features on bundle-hydrated stubs so
                // PerFileRescoreTask's WriteReconciledParquet can keep
                // its "Features != null means this entry was rescored"
                // criterion -- with features pre-populated from the
                // parquet, every entry would otherwise look rescored
                // and overwrite the original parquet row's binary
                // blob columns (fragment_mzs, ref_xic_*, bounds_*).
                // Bundle path doesn't need PIN features downstream:
                // FirstJoinTask skips Percolator on this path, so the
                // SVM training input is irrelevant.
                foreach (var kvp in perFileEntries)
                    foreach (var entry in kvp.Value)
                        entry.Features = null;
                _ctx.LogInfo(string.Format(
                    @"Hydrated rescore bundle for {0} file(s) ({1} reconciliation actions, " +
                    @"{2} refined RT calibration(s), {3} gap-fill target(s))",
                    perFileEntries.Count,
                    _rescoreInputs.TotalActions,
                    _rescoreInputs.RefinedCalibrations.Count,
                    _rescoreInputs.TotalGapFillTargets));
            }
            return true;
        }

        /// <summary>
        /// Phase B per-file resume: if the file's <c>.scores.parquet</c>
        /// already exists with a matching <c>.PerFileScoring.osprey.task</c>
        /// sidecar (validity key matches the current config), load the
        /// stubs + PIN features + best-effort calibration from disk and
        /// skip <see cref="ProcessFile"/>. Otherwise clear any stale
        /// sidecar, run <see cref="ProcessFile"/>, and on success write
        /// a fresh sidecar. The pre-Run delete is the per-file analogue
        /// of the task-level safety net other tasks use: a mid-Run crash
        /// leaves no sidecar pointing at the partial parquet, so the
        /// resume invocation reprocesses that file.
        ///
        /// Returns the per-file <see cref="FdrEntry"/> list (from the
        /// disk load or <see cref="ProcessFile"/>), or <c>null</c> on
        /// <see cref="ProcessFile"/> failure.
        /// </summary>
        private List<FdrEntry> ScoreOrLoadForFile(
            string inputFile,
            string fileName,
            int fileIdx,
            int totalFiles,
            List<LibraryEntry> fullLibrary,
            OspreyConfig config,
            Dictionary<string, string> parquetFooterMetadata,
            ConcurrentDictionary<string, RTCalibration> perFileCalibrations,
            string validityKey,
            PipelineContext ctx)
        {
            string scoresPath = ParquetScoreCache.GetScoresPath(inputFile);
            if (File.Exists(scoresPath)
                && TaskValiditySidecar.IsValid(scoresPath, Name, validityKey))
            {
                var loaded = TryLoadStubsAndCalibration(scoresPath, fileName, perFileCalibrations, ctx);
                if (loaded != null)
                {
                    ctx.LogInfo(string.Format(
                        @"[file] {0}/{1} {2}: skipping (outputs valid)",
                        fileIdx + 1, totalFiles, fileName));
                    return loaded;
                }
                // load failed -- fall through and rescore the file.
            }

            ctx.LogInfo(string.Empty);
            ctx.LogInfo(string.Format(@"===== Processing file {0}/{1}: {2} =====",
                fileIdx + 1, totalFiles, inputFile));
            // Clear stale sidecar so a mid-ProcessFile crash leaves no
            // false-positive sidecar on the next invocation.
            TaskValiditySidecar.Delete(scoresPath, Name);
            var fileResult = ProcessFile(inputFile, fileName, fullLibrary, config, parquetFooterMetadata, perFileCalibrations);
            if (fileResult != null)
            {
                try
                {
                    TaskValiditySidecar.Write(scoresPath, Name, Program.VERSION,
                        validityKey, new[] { inputFile });
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(
                        @"  Failed to write {0} sidecar for {1}: {2}",
                        Name, scoresPath, ex.Message));
                }
            }
            return fileResult;
        }

        /// <summary>
        /// Best-effort load of one file's stubs + PIN features from a
        /// <c>.scores.parquet</c> plus the calibration sibling. Returns
        /// the stub list on success or <c>null</c> on a stub/feature read
        /// failure. With <paramref name="resumeStrict"/> = <c>false</c> (the
        /// default, used by <see cref="ScoreOrLoadForFile"/>) the caller falls
        /// back to rescoring, so a failure logs a "will rescore" warning. With
        /// <paramref name="resumeStrict"/> = <c>true</c> (the pure-load resume
        /// path, where <see cref="PipelineContext.CanRehydrate"/> already
        /// certified the outputs valid and there is NO rescore fallback) a
        /// failure is a genuine fault and logs an error instead -- no misleading
        /// "will rescore" messaging. Mirrors the load logic in the
        /// <c>--join-only</c> branch above.
        /// </summary>
        private static List<FdrEntry> TryLoadStubsAndCalibration(
            string scoresPath,
            string fileName,
            ConcurrentDictionary<string, RTCalibration> perFileCalibrations,
            PipelineContext ctx,
            bool resumeStrict = false)
        {
            List<FdrEntry> stubs;
            try
            {
                stubs = ParquetScoreCache.LoadFdrStubsFromParquet(scoresPath);
                var features = ParquetScoreCache.LoadPinFeaturesFromParquet(scoresPath);
                if (features.Count != stubs.Count)
                {
                    if (resumeStrict)
                        ctx.LogError(string.Format(
                            @"  Resume rehydrate: {0} has {1} stubs but {2} feature rows; cannot load valid-on-disk scores.",
                            scoresPath, stubs.Count, features.Count));
                    else
                        ctx.LogWarning(string.Format(
                            @"  Per-file resume: {0} has {1} stubs but {2} feature rows; will rescore.",
                            scoresPath, stubs.Count, features.Count));
                    return null;
                }
                for (int j = 0; j < stubs.Count; j++)
                    stubs[j].Features = features[j];
            }
            catch (Exception ex)
            {
                if (resumeStrict)
                    ctx.LogError(string.Format(
                        @"  Resume rehydrate: failed to load valid-on-disk scores from {0}: {1}",
                        scoresPath, ex.Message));
                else
                    ctx.LogWarning(string.Format(
                        @"  Per-file resume: failed to load {0}: {1}; will rescore.",
                        scoresPath, ex.Message));
                return null;
            }

            try
            {
                string parquetDir = Path.GetDirectoryName(Path.GetFullPath(scoresPath));
                if (parquetDir != null)
                {
                    string calStemPath = Path.Combine(parquetDir, fileName);
                    string calPath = CalibrationIO.CalibrationPathForInput(calStemPath, parquetDir);
                    if (File.Exists(calPath))
                    {
                        var calParams = CalibrationIO.LoadCalibration(calPath);
                        if (calParams.RtCalibration != null && calParams.RtCalibration.ModelParams != null)
                        {
                            var mp = calParams.RtCalibration.ModelParams;
                            var rtCal = RTCalibration.FromModelParams(
                                mp.LibraryRts, mp.FittedRts, mp.AbsResiduals,
                                calParams.RtCalibration.ResidualSD);
                            perFileCalibrations[fileName] = rtCal;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format(@"  Failed to load calibration for {0}: {1}", fileName, ex.Message));
            }
            return stubs;
        }

        /// <summary>
        /// Process a single mzML file: load spectra, calibrate RT, score coelution.
        /// </summary>
        private List<FdrEntry> ProcessFile(
            string inputFile, string fileName,
            List<LibraryEntry> fullLibrary, OspreyConfig config,
            Dictionary<string, string> parquetFooterMetadata,
            ConcurrentDictionary<string, RTCalibration> perFileCalibrationsOut)
        {
            if (inputFile == null)
                throw new ArgumentNullException(nameof(inputFile));

            // Per-file shallow clone so MS2 calibration's mutation of
            // FragmentTolerance (and any future per-file overrides) does
            // not leak between parallel ProcessFile() calls. Without this
            // the shared OspreyConfig.FragmentTolerance gets clobbered
            // by whichever calibration completes second, causing
            // ~1000-2000 entries per file to score with the wrong
            // (foreign-file-calibrated) tolerance under -MaxParallelFiles>1.
            config = config.ShallowClone();

            // Divide the inner main-search thread budget across concurrent
            // files so total demand stays near core count. With 3 files x
            // 32 threads on a 32-core box, the prior 96-way oversubscription
            // produced 45-95s wall-time variance on Stellar; a fair share
            // (10 threads each) holds the run near steady-state.
            if (_ctx.RunPlan.EffectiveFileParallelism > 1)
            {
                int perFileThreads = Math.Max(1, config.NThreads / _ctx.RunPlan.EffectiveFileParallelism);
                _ctx.LogInfo(string.Format(
                    "[BENCH] Per-file thread cap: {0} ({1} total / {2} files in parallel)",
                    perFileThreads, config.NThreads, _ctx.RunPlan.EffectiveFileParallelism));
                config.NThreads = perFileThreads;
            }

            var context = new ScoringContext(config, fileName);

            // Load spectra (from mzML or .spectra.bin cache)
            List<Spectrum> spectra;
            List<MS1Spectrum> ms1Spectra;
            var swParse = Stopwatch.StartNew();
            LoadSpectra(inputFile, _ctx.RunPlan.EffectiveFileParallelism > 1,
                out spectra, out ms1Spectra);
            swParse.Stop();

            long inputBytes = 0;
            try
            {
                if (File.Exists(inputFile))
                    inputBytes = new FileInfo(inputFile).Length;
            }
            catch
            {
                inputBytes = 0;
            }

            double parseSeconds = swParse.Elapsed.TotalSeconds;
            if (inputBytes > 0 && parseSeconds > 0.001)
            {
                double mbPerSec = (inputBytes / 1024.0 / 1024.0) / parseSeconds;
                _ctx.LogInfo(string.Format("[TIMING] mzML parsing: {0:F1}s ({1:F1} MB/s)",
                    parseSeconds, mbPerSec));
            }
            else
            {
                _ctx.LogInfo(string.Format("[TIMING] mzML parsing: {0:F1}s", parseSeconds));
            }

            if (spectra == null || spectra.Count == 0)
            {
                _ctx.LogWarning(string.Format("No spectra found in {0}", inputFile));
                return null;
            }

            _ctx.LogInfo(string.Format("Loaded {0} MS2 spectra and {1} MS1 spectra",
                spectra.Count, ms1Spectra != null ? ms1Spectra.Count : 0));
            _ctx.LogInfo(string.Format("[COUNT] mzML spectra loaded [{0}]: {1} MS2 + {2} MS1",
                fileName, spectra.Count, ms1Spectra != null ? ms1Spectra.Count : 0));

            // Extract isolation windows from spectra
            var isolationWindows = ExtractIsolationWindows(spectra);
            _ctx.LogInfo(string.Format("Found {0} unique isolation windows", isolationWindows.Count));
            _ctx.LogInfo(string.Format("[COUNT] Isolation windows [{0}]: {1}",
                fileName, isolationWindows.Count));

            // RT calibration
            RTCalibration rtCalibration = null;
            MzCalibrationResult ms2Cal = MzCalibrationResult.Uncalibrated();
            MzCalibrationResult ms1Cal = MzCalibrationResult.Uncalibrated();
            // Total matches scored during pass 1 of calibration; threaded
            // into CalibrationMetadata.NumSampledPrecursors to match Rust's
            // accumulated_matches.len() (Stellar Single: 192289). Stays 0
            // when calibration is loaded from a cached JSON.
            int numSampledPrecursorsForMetadata = 0;

            // BISECT: load Rust's calibration JSON instead of computing our own.
            // This eliminates calibration noise from the feature comparison.
            // Usage: OSPREY_LOAD_CALIBRATION=Ste-...20.calibration.json
            string loadCalPath = OspreyEnvironment.LoadCalibrationPath;
            if (!string.IsNullOrEmpty(loadCalPath) && File.Exists(loadCalPath))
            {
                _ctx.LogInfo(string.Format("[BISECT] Loading calibration from: {0}", loadCalPath));
                var calParams = CalibrationIO.LoadCalibration(loadCalPath);
                if (calParams.RtCalibration != null && calParams.RtCalibration.ModelParams != null)
                {
                    var mp = calParams.RtCalibration.ModelParams;
                    rtCalibration = RTCalibration.FromModelParams(
                        mp.LibraryRts, mp.FittedRts, mp.AbsResiduals,
                        calParams.RtCalibration.ResidualSD);
                    _ctx.LogInfo(string.Format("Loaded RT calibration: {0} points, R2={1:F4}",
                        calParams.RtCalibration.NPoints, calParams.RtCalibration.RSquared));
                }
                if (calParams.Ms2Calibration != null && calParams.Ms2Calibration.Calibrated)
                {
                    ms2Cal = new MzCalibrationResult
                    {
                        Mean = calParams.Ms2Calibration.Mean,
                        Median = calParams.Ms2Calibration.Median,
                        SD = calParams.Ms2Calibration.SD,
                        Count = calParams.Ms2Calibration.Count,
                        Unit = calParams.Ms2Calibration.Unit,
                        AdjustedTolerance = calParams.Ms2Calibration.AdjustedTolerance,
                        Calibrated = true
                    };
                    _ctx.LogInfo(string.Format("Loaded MS2 calibration: mean={0:F4} {1}, SD={2:F4}",
                        ms2Cal.Mean, ms2Cal.Unit, ms2Cal.SD));
                }
                if (calParams.Ms1Calibration != null && calParams.Ms1Calibration.Calibrated)
                {
                    ms1Cal = new MzCalibrationResult
                    {
                        Mean = calParams.Ms1Calibration.Mean,
                        Median = calParams.Ms1Calibration.Median,
                        SD = calParams.Ms1Calibration.SD,
                        Count = calParams.Ms1Calibration.Count,
                        Unit = calParams.Ms1Calibration.Unit,
                        AdjustedTolerance = calParams.Ms1Calibration.AdjustedTolerance,
                        Calibrated = true
                    };
                    _ctx.LogInfo(string.Format("Loaded MS1 calibration: mean={0:F4} {1}, SD={2:F4}",
                        ms1Cal.Mean, ms1Cal.Unit, ms1Cal.SD));
                }
            }
            else if (config.RtCalibration.Enabled)
            {
                var swCal = Stopwatch.StartNew();
                rtCalibration = RunCalibration(
                    fullLibrary, spectra, ms1Spectra, context,
                    out ms1Cal, out ms2Cal, out numSampledPrecursorsForMetadata);
                swCal.Stop();
                int nPoints = rtCalibration != null ? rtCalibration.Stats().NPoints : 0;
                _ctx.LogInfo(string.Format(
                    "[TIMING] RT calibration: {0:F1}s ({1} calibration points)",
                    swCal.Elapsed.TotalSeconds, nPoints));
            }

            // Dump 11 calibration summary scalars (MS1/MS2 mean/sd/count/
            // tolerance + RT n_points/r_squared/residual_sd) so the final
            // calibration state can be diff'd against Rust's cal JSON.
            OspreyDiagnostics.WriteCalibrationSummary(rtCalibration, ms1Cal, ms2Cal);

            // Save the full calibration state to {inputStem}.calibration.json
            // in the same directory as the mzML input. Same schema as Rust
            // so the file is round-trippable via OSPREY_LOAD_CALIBRATION in
            // either tool. Enables HPC "compute cal once, reuse on another
            // node" and the cross-runtime cal-swap bisection.
            try
            {
                var calParams = new CalibrationParams
                {
                    Metadata = new CalibrationMetadata
                    {
                        CalibrationSuccessful = rtCalibration != null,
                        // Match Rust's CalibrationMetadata field semantics
                        // (osprey/src/pipeline.rs:1144-1145):
                        //   num_confident_peptides = LOESS points used by the
                        //     fit actually saved (post-S/N filter)
                        //   num_sampled_precursors = total matches scored
                        //     during sampling (pre any q-value / S/N filter)
                        NumConfidentPeptides = rtCalibration != null
                            ? rtCalibration.Stats().NPoints
                            : 0,
                        NumSampledPrecursors = numSampledPrecursorsForMetadata,
                        Timestamp = DateTime.UtcNow.ToString("o")
                    },
                    Ms1Calibration = MzCalibrationJson.FromResult(ms1Cal),
                    Ms2Calibration = MzCalibrationJson.FromResult(ms2Cal),
                    RtCalibration = RTCalibrationJson.FromRTCalibration(rtCalibration),
                    SecondPassRt = null
                };
                // Path.GetDirectoryName can return null for a root path; default
                // to the current dir so the calibration JSON still has a home.
                string calDir = Path.GetDirectoryName(Path.GetFullPath(inputFile)) ?? ".";
                string calPath = CalibrationIO.CalibrationPathForInput(inputFile, calDir);
                CalibrationIO.SaveCalibration(calParams, calPath);
                _ctx.LogInfo(string.Format("Saved calibration to {0}", calPath));
            }
            catch (Exception ex)
            {
                _ctx.LogInfo("Warning: failed to save calibration JSON: " + ex.Message);
            }

            // Optional early exit after Stage 3 (calibration only, no main search).
            // Used for Stage 1-3 perf benchmarking and walking up to the main
            // search incrementally without paying the Stage 4 cost.
            if (OspreyEnvironment.ExitAfterCalibration)
            {
                _ctx.LogInfo("[BENCH] OSPREY_EXIT_AFTER_CALIBRATION set - exiting after Stage 3 (calibration done)");
                return new List<FdrEntry>();
            }

            // Surface the per-file calibration to Stage 6 reconciliation
            // (multi-file runs only). Threaded calls share a
            // ConcurrentDictionary; null on single-file paths that don't
            // need cross-file consensus.
            if (perFileCalibrationsOut != null && rtCalibration != null)
                perFileCalibrationsOut[fileName] = rtCalibration;

            // Run coelution scoring across all isolation windows
            var swScoring = Stopwatch.StartNew();
            var scoredEntries = RunCoelutionScoring(
                fullLibrary, spectra, ms1Spectra,
                isolationWindows, rtCalibration,
                ms2Cal, ms1Cal,
                context);
            swScoring.Stop();
            double scoringSeconds = swScoring.Elapsed.TotalSeconds;
            double ratePerSec = scoringSeconds > 0.001
                ? scoredEntries.Count / scoringSeconds
                : 0.0;
            _ctx.LogInfo(string.Format(
                "[TIMING] Coelution scoring: {0:F1}s ({1} candidates, {2:F0} cand/s)",
                scoringSeconds, scoredEntries.Count, ratePerSec));

            int nScoredTargets = scoredEntries.Count(e => !e.IsDecoy);
            int nScoredDecoys = scoredEntries.Count(e => e.IsDecoy);
            _ctx.LogInfo(string.Format("Scored {0} entries ({1} targets, {2} decoys) for {3}",
                scoredEntries.Count,
                nScoredTargets,
                nScoredDecoys,
                fileName));

            // Drop double-counted entries (different precursors that latch
            // onto the same chromatographic feature within an isolation
            // window). Mirrors osprey/crates/osprey/src/pipeline.rs at the
            // same call site, between scoring and pair-deduplication.
            scoredEntries = DeduplicateDoubleCounting(
                scoredEntries, fullLibrary, spectra, ms2Cal,
                isolationWindows, config);
            nScoredTargets = scoredEntries.Count(e => !e.IsDecoy);
            nScoredDecoys = scoredEntries.Count(e => e.IsDecoy);
            _ctx.LogInfo(string.Format(
                "[COUNT] Coelution scored [{0}]: {1} entries ({2} targets, {3} decoys)",
                fileName, scoredEntries.Count, nScoredTargets, nScoredDecoys));

            // Deduplicate: keep best target and best decoy per base_id
            int nBeforeDedup = scoredEntries.Count;
            scoredEntries = DeduplicatePairs(scoredEntries);
            int nAfterDedup = scoredEntries.Count;
            _ctx.LogInfo(string.Format(
                "[COUNT] Deduplication [{0}]: {1} -> {2} ({3} removed)",
                fileName, nBeforeDedup, nAfterDedup, nBeforeDedup - nAfterDedup));

            // Optional: write per-entry feature TSV for comparison against Rust's PIN output
            if (config.WritePin)
            {
                WriteFeatureDump(inputFile, fileName, scoredEntries);
            }

            // Persist the full FdrEntry results (with features) to
            // {stem}.scores.parquet so (a) Stage 6 reconciliation can lazy-load
            // CWT candidates per file, and (b) a subsequent --join-only
            // invocation can pick them up without re-running Stages 1-4.
            // Same path convention as Rust (`scores_path_for_input`).
            // Snappy-compressed; cross-impl ZSTD/Snappy compatibility tracked
            // as a Phase 4 follow-up. The metadata dictionary is precomputed
            // in Run() against the original (un-mutated) outer config — see
            // Run() for why. Skipped only in --join-only mode (no Stages 1-4
            // ran here, so there is nothing fresh to persist).
            if (parquetFooterMetadata != null)
            {
                string parquetPath = ParquetScoreCache.GetScoresPath(inputFile);
                // Reuse the entry-id -> LibraryEntry map Run() built once
                // before the per-file fan-out (WriteScoresParquet needs
                // it to pull sequence / precursor_mz / protein_ids from
                // the library since FdrEntry doesn't carry these).
                var swParquet = Stopwatch.StartNew();
                ParquetScoreCache.WriteScoresParquet(
                    parquetPath, scoredEntries, parquetFooterMetadata, _libraryById, fileName);
                swParquet.Stop();
                _ctx.LogInfo(string.Format(
                    "Wrote {0} scored entries to {1} ({2:F1}s)",
                    scoredEntries.Count, parquetPath, swParquet.Elapsed.TotalSeconds));
            }

            return scoredEntries;
        }

        /// <summary>
        /// Write a TSV dump of per-entry feature values for direct comparison with
        /// the Rust implementation's PIN output. Format matches Rust's PIN columns:
        /// psm_id, label, scan, + 21 features. Sorted by (modified_sequence, charge,
        /// scan_number) for stable diffing against Rust's output.
        /// </summary>
        private void WriteFeatureDump(
            string inputFile, string fileName,
            List<FdrEntry> scoredEntries)
        {
            string dumpPath = Path.Combine(
                Path.GetDirectoryName(inputFile) ?? ".",
                fileName + ".cs_features.tsv");

            var header = new[]
            {
                "SpecId", "Label", "ScanNr", "Charge",
                "fragment_coelution_sum", "fragment_coelution_max", "n_coeluting_fragments",
                "peak_apex", "peak_area", "peak_sharpness",
                "xcorr", "consecutive_ions", "explained_intensity",
                "mass_accuracy_deviation_mean", "abs_mass_accuracy_deviation_mean",
                "rt_deviation", "abs_rt_deviation",
                "ms1_precursor_coelution", "ms1_isotope_cosine",
                "median_polish_cosine", "median_polish_residual_ratio",
                "sg_weighted_xcorr", "sg_weighted_cosine",
                "median_polish_min_fragment_r2", "median_polish_residual_correlation",
                "Peptide"
            };

            var sorted = scoredEntries
                .Where(e => e.Features != null && e.Features.Length == NUM_PIN_FEATURES)
                .OrderBy(e => e.ModifiedSequence, StringComparer.Ordinal)
                .ThenBy(e => e.Charge)
                .ThenBy(e => e.ScanNumber)
                .ToList();

            using (var writer = new StreamWriter(dumpPath))
            {
                // LF newlines so the dump is byte-stable across Windows and
                // Linux for cross-impl diffing against Rust's PIN output;
                // matches the convention used by OspreyDiagnostics.
                writer.NewLine = "\n";
                writer.WriteLine(string.Join("\t", header));
                foreach (var e in sorted)
                {
                    string psmId = string.Format("{0}_{1}_{2}_{3}",
                        fileName, e.ModifiedSequence, e.Charge, e.ScanNumber);
                    int label = e.IsDecoy ? -1 : 1;
                    var cols = new List<string>(26)
                    {
                        psmId,
                        label.ToString(),
                        e.ScanNumber.ToString(),
                        e.Charge.ToString()
                    };
                    for (int i = 0; i < NUM_PIN_FEATURES; i++)
                        cols.Add(e.Features[i].ToString("G17"));
                    cols.Add(e.ModifiedSequence ?? "");
                    writer.WriteLine(string.Join("\t", cols));
                }
            }

            _ctx.LogInfo(string.Format("[COUNT] Wrote feature dump: {0} ({1} entries)",
                dumpPath, sorted.Count));
        }

        /// <summary>
        /// Load spectra from mzML file or spectra cache. When multiple
        /// files are processed in parallel, the mzML parse is gated so
        /// only one disk scan runs at a time (see s_mzmlReadGate).
        /// </summary>
        private void LoadSpectra(string inputFile, bool serializeMzmlRead,
            out List<Spectrum> ms2Spectra, out List<MS1Spectrum> ms1Spectra)
        {
            // Check for binary spectra cache
            string cachePath = inputFile + ".spectra.bin";
            if (File.Exists(cachePath))
            {
                _ctx.LogInfo(string.Format("Loading spectra from cache: {0}", cachePath));
                try
                {
                    var cacheResult = SpectraCache.LoadSpectraCache(cachePath);
                    ms2Spectra = cacheResult.Ms2Spectra;
                    ms1Spectra = cacheResult.Ms1Spectra;
                    return;
                }
                catch (Exception ex)
                {
                    _ctx.LogWarning(string.Format(
                        "Failed to load spectra cache: {0}. Falling back to mzML.", ex.Message));
                }
            }

            // Parse mzML directly, optionally serialized across files.
            _ctx.LogInfo(string.Format("Parsing mzML: {0}", inputFile));
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
            ms2Spectra = mzmlResult.Ms2Spectra;
            ms1Spectra = mzmlResult.Ms1Spectra;
            _ctx.LogInfo(string.Format("Loaded {0} MS2 + {1} MS1 spectra",
                ms2Spectra.Count, ms1Spectra.Count));

            // Save to cache for next run
            try
            {
                SpectraCache.SaveSpectraCache(cachePath, ms2Spectra, ms1Spectra);
            }
            catch (Exception ex)
            {
                _ctx.LogWarning(string.Format("Failed to save spectra cache: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Run RT calibration using calibration discovery scoring.
        /// Ports osprey/src/pipeline.rs run_calibration_discovery_windowed:
        ///   1. Sample target + paired decoy library entries (stratified by RT/m/z).
        ///   2. For each sample, extract fragment XICs, detect the best co-eluting
        ///      peak, then compute 4 features at the apex: mean pairwise correlation,
        ///      LibCosine, top-6 matched, and XCorr.
        ///   3. Train LDA with non-negative weights + 1% FDR target-decoy competition.
        ///   4. Apply S/N >= 5.0 quality filter on surviving targets.
        ///   5. Fit LOESS on the (libRt, measuredRt) pairs.
        /// </summary>
        private RTCalibration RunCalibration(
            List<LibraryEntry> library,
            List<Spectrum> spectra,
            List<MS1Spectrum> ms1Spectra,
            ScoringContext context,
            out MzCalibrationResult ms1Calibration,
            out MzCalibrationResult ms2Calibration,
            out int numSampledPrecursors)
        {
            var config = context.Config;
            // Default to 0 so early returns / exception paths leave the
            // metadata caller in a known state. Overwritten on success.
            numSampledPrecursors = 0;
            _ctx.LogInfo("Running RT calibration...");

            // Calculate library and mzML RT ranges
            double libMinRt = double.MaxValue, libMaxRt = double.MinValue;
            double mzmlMinRt = double.MaxValue, mzmlMaxRt = double.MinValue;

            foreach (var entry in library)
            {
                if (!entry.IsDecoy)
                {
                    if (entry.RetentionTime < libMinRt)
                        libMinRt = entry.RetentionTime;
                    if (entry.RetentionTime > libMaxRt)
                        libMaxRt = entry.RetentionTime;
                }
            }

            foreach (var spectrum in spectra)
            {
                if (spectrum.RetentionTime < mzmlMinRt)
                    mzmlMinRt = spectrum.RetentionTime;
                if (spectrum.RetentionTime > mzmlMaxRt)
                    mzmlMaxRt = spectrum.RetentionTime;
            }

            double libRtRange = libMaxRt - libMinRt;
            double mzmlRtRange = mzmlMaxRt - mzmlMinRt;

            _ctx.LogInfo(string.Format(
                "Library RT range: {0:F1}-{1:F1}, mzML RT range: {2:F1}-{3:F1} min",
                libMinRt, libMaxRt, mzmlMinRt, mzmlMaxRt));

            // Linear RT mapping when library and mzML scales differ significantly.
            bool rangesSimilar = libRtRange > 0 && mzmlRtRange > 0 &&
                Math.Max(libRtRange / mzmlRtRange, mzmlRtRange / libRtRange) < 2.0 &&
                Math.Abs(libMinRt - mzmlMinRt) < libRtRange * 0.5;

            double rtSlope = 1.0;
            double rtIntercept = 0.0;
            if (!rangesSimilar && libRtRange > 0)
            {
                rtSlope = mzmlRtRange / libRtRange;
                rtIntercept = mzmlMinRt - rtSlope * libMinRt;
                _ctx.LogInfo(string.Format("RT mapping: slope={0:F4}, intercept={1:F4}",
                    rtSlope, rtIntercept));
            }

            double toleranceFraction = rangesSimilar ? 0.2 : 0.5;
            double initialTolerance = mzmlRtRange * toleranceFraction;

            _ctx.LogInfo(string.Format("Initial RT tolerance: {0:F1} min", initialTolerance));

            // Sample library entries (paired target+decoy). Use seed 43 to match
            // Rust's sample_library_for_calibration(..., 42 + attempt=1) on the
            // first calibration attempt.
            var swSample = Stopwatch.StartNew();
            var sampledEntries = SampleLibraryForCalibration(
                library, config.RtCalibration.CalibrationSampleSize, 43UL);
            swSample.Stop();

            // Diagnostic: dump sorted sampled entry IDs + (modseq, charge) for
            // direct comparison with Rust. Abort after dump if CAL_SAMPLE_ONLY
            // env var is set (bisection mode - stop once we agree here).
            if (config.WritePin || OspreyDiagnostics.DumpCalSample)
            {
                OspreyDiagnostics.WriteCalSampleDump(context.FileName, sampledEntries);
                if (OspreyDiagnostics.CalSampleOnly)
                    OspreyDiagnostics.ExitAfterDump("OSPREY_CAL_SAMPLE_ONLY");
            }
            int nSampledTargets = 0;
            int nSampledDecoys = 0;
            foreach (var e in sampledEntries)
            {
                if (e.IsDecoy)
                    nSampledDecoys++;
                else nSampledTargets++;
            }
            _ctx.LogInfo(string.Format(
                "[TIMING] Calibration sampling: {0:F2}s ({1} targets + {2} decoys)",
                swSample.Elapsed.TotalSeconds, nSampledTargets, nSampledDecoys));

            if (nSampledTargets == 0)
            {
                _ctx.LogWarning("No target entries available for calibration sampling.");
                ms1Calibration = MzCalibrationResult.Uncalibrated();
                ms2Calibration = MzCalibrationResult.Uncalibrated();
                return null;
            }

            // Group spectra by isolation window center for O(1) window lookup per candidate.
            var spectraByWindowKey = new Dictionary<int, List<Spectrum>>();
            foreach (var spectrum in spectra)
            {
                int key = (int)Math.Round(spectrum.IsolationWindow.Center * 10.0);
                List<Spectrum> list;
                if (!spectraByWindowKey.TryGetValue(key, out list))
                {
                    list = new List<Spectrum>();
                    spectraByWindowKey[key] = list;
                }
                list.Add(spectrum);
            }
            // Sort each window's spectra by RT for deterministic XIC extraction.
            foreach (var list in spectraByWindowKey.Values)
                list.Sort((a, b) => a.RetentionTime.CompareTo(b.RetentionTime));

            // Pass 1: score all sampled entries with the linear pre-fit RT mapping
            // and the wide initial tolerance. Fits a LOESS RTCalibration from the
            // LDA + S/N surviving targets.
            var pass1 = RunCalibrationScoringPass(
                1,
                sampledEntries, spectraByWindowKey, ms1Spectra, context,
                rtSlope, rtIntercept, initialTolerance,
                null /* calibrationModel: pass 1 uses linear mapping */,
                config.RtCalibration.MinCalibrationPoints);

            if (pass1 == null)
            {
                _ctx.LogWarning("Calibration pass 1 failed. Using fallback tolerance.");
                ms1Calibration = MzCalibrationResult.Uncalibrated();
                ms2Calibration = MzCalibrationResult.Uncalibrated();
                return null;
            }

            // Pass 1 succeeded -- emit the OSPREY_DUMP_LOESS_INPUT diagnostic
            // for the pass-1 fit unconditionally. If pass 2 is later accepted
            // it will overwrite this with the pass-2 pairs; if pass 2 is
            // rejected (or never runs) the pass-1 dump stays, matching the
            // calibration actually used. Mirrors Rust pipeline.rs.
            if (OspreyDiagnostics.DumpLoessInput)
            {
                OspreyDiagnostics.WriteLoessInputDump(1, pass1.LibRts, pass1.MeasuredRts);
                if (OspreyDiagnostics.LoessInputOnly)
                    OspreyDiagnostics.ExitAfterDump("OSPREY_LOESS_INPUT_ONLY");
            }

            // Match Rust accumulated_matches.len() semantics: report pass 1's
            // total scored matches (before any q-value / S/N filtering).
            // Pass 2 is a refinement using narrowed RT tolerance and does not
            // change the sampled-precursor count.
            numSampledPrecursors = pass1.MatchCount;

            // === Iterative calibration refinement (2-pass) ===
            // Mirrors Rust pipeline.rs:714-839.
            // MAD * 1.4826 ~ SD for a normal distribution; 3* that covers ~99.7%.
            double madTolerance = pass1.Stats.MAD * 1.4826 * 3.0;
            double pass1Tolerance = Math.Max(
                config.RtCalibration.MinRtTolerance,
                Math.Min(config.RtCalibration.MaxRtTolerance, madTolerance));

            _ctx.LogInfo(string.Format(
                "First-pass RT tolerance: {0:F2} min (MAD={1:F3}, robust_SD={2:F3}, residual_SD={3:F3}, {4} points, R^2={5:F4})",
                pass1Tolerance,
                pass1.Stats.MAD,
                pass1.Stats.MAD * 1.4826,
                pass1.Stats.ResidualSD,
                pass1.Stats.NPoints,
                pass1.Stats.RSquared));

            // Only refine if the tolerance narrowed at least 2* tighter than the
            // initial wide window.
            if (pass1Tolerance < initialTolerance * 0.5)
            {
                _ctx.LogInfo(string.Format(
                    "Calibration refinement: re-scoring with {0:F2} min tolerance (was {1:F1} min)",
                    pass1Tolerance, initialTolerance));

                var pass2 = RunCalibrationScoringPass(
                    2,
                    sampledEntries, spectraByWindowKey, ms1Spectra, context,
                    rtSlope, rtIntercept, pass1Tolerance,
                    pass1.Calibration /* pass 2 predicts RT via the LOESS fit */,
                    ABSOLUTE_MIN_CALIBRATION_POINTS);

                if (pass2 != null)
                {
                    double refinedMadTolerance = pass2.Stats.MAD * 1.4826 * 3.0;
                    double refinedTolerance = Math.Max(
                        config.RtCalibration.MinRtTolerance,
                        Math.Min(config.RtCalibration.MaxRtTolerance, refinedMadTolerance));

                    _ctx.LogInfo(string.Format(
                        "Refined RT tolerance: {0:F2} min (MAD={1:F3}, robust_SD={2:F3}, residual_SD={3:F3}, {4} points, R^2={5:F4})",
                        refinedTolerance,
                        pass2.Stats.MAD,
                        pass2.Stats.MAD * 1.4826,
                        pass2.Stats.ResidualSD,
                        pass2.Stats.NPoints,
                        pass2.Stats.RSquared));

                    // Accept the refined calibration only if R^2 didn't degrade
                    // by more than 1% (matches Rust pipeline.rs:811).
                    if (pass2.Stats.RSquared >= pass1.Stats.RSquared * 0.99)
                    {
                        // Overwrite the OSPREY_DUMP_LOESS_INPUT dump with
                        // pass 2's points so the diagnostic reflects the
                        // calibration actually being used. Mirrors Rust
                        // pipeline.rs; only fires on acceptance.
                        if (OspreyDiagnostics.DumpLoessInput)
                        {
                            OspreyDiagnostics.WriteLoessInputDump(
                                2, pass2.LibRts, pass2.MeasuredRts);
                        }
                        ms1Calibration = pass2.Ms1Calibration;
                        ms2Calibration = pass2.Ms2Calibration;
                        return pass2.Calibration;
                    }
                    _ctx.LogInfo(string.Format(
                        "Refined calibration not better (R^2 {0:F4} vs {1:F4}), keeping original",
                        pass2.Stats.RSquared, pass1.Stats.RSquared));
                }
                else
                {
                    _ctx.LogInfo(string.Format(
                        "Refinement pass: insufficient points (need {0}), keeping original calibration",
                        ABSOLUTE_MIN_CALIBRATION_POINTS));
                }
            }

            ms1Calibration = pass1.Ms1Calibration;
            ms2Calibration = pass1.Ms2Calibration;
            return pass1.Calibration;
        }

        /// <summary>
        /// Run one calibration scoring pass: score each sampled entry, train LDA,
        /// apply S/N filter, and fit LOESS on the surviving (libRt, measuredRt) pairs.
        /// Returns null if the pass has fewer than minLoessPoints survivors or the
        /// LOESS fit fails. This helper is called twice by RunCalibration to
        /// implement the two-pass refinement (pipeline.rs:714-839).
        /// </summary>
        private CalibrationPassResult RunCalibrationScoringPass(
            int passNumber,
            List<LibraryEntry> sampledEntries,
            Dictionary<int, List<Spectrum>> spectraByWindowKey,
            List<MS1Spectrum> ms1Spectra,
            ScoringContext context,
            double rtSlope, double rtIntercept, double tolerance,
            RTCalibration calibrationModel,
            int minLoessPoints)
        {
            var config = context.Config;
            var fileName = context.FileName;
            // Activate per-entry window dump if requested. Cleared after the
            // matching loop completes (file written below).
            OspreyDiagnostics.StartCalWindowCollection();

            // Pre-preprocess all window spectra for XCorr (f32 unit-bin scorer).
            var preprocessedByWindowKey = PreprocessWindowsForXcorr(spectraByWindowKey);

            // Parallel score each sampled entry (timing + match-count logs inside).
            var (matches, snrByEntryId, matchRts) = ScoreCalibrationMatches(
                passNumber, sampledEntries, spectraByWindowKey, preprocessedByWindowKey,
                ms1Spectra, context, rtSlope, rtIntercept, tolerance, calibrationModel);

            // Write per-entry window dump if requested. When two passes run, the
            // pass 2 dump overwrites pass 1 - same behaviour as Rust's
            // run_coelution_calibration_scoring dumping on every invocation.
            if (OspreyDiagnostics.CalWindowsCollecting)
            {
                OspreyDiagnostics.WriteCalWindowsDump(passNumber);
                if (OspreyDiagnostics.CalWindowsOnly)
                    OspreyDiagnostics.ExitAfterDump("OSPREY_CAL_WINDOWS_ONLY");
            }

            // Cross-implementation diagnostic: dump per-entry calibration match info
            // for direct diff with Rust. Writes a row for EVERY sampled entry
            // (matched or not), sorted by entry_id for stable diff.
            if (OspreyDiagnostics.DumpCalMatch)
            {
                OspreyDiagnostics.WriteCalMatchDump(passNumber, matches, sampledEntries, matchRts, snrByEntryId);
                if (OspreyDiagnostics.CalMatchOnly)
                    OspreyDiagnostics.ExitAfterDump("OSPREY_CAL_MATCH_ONLY");
            }

            if (matches.Count == 0)
            {
                _ctx.LogWarning(string.Format(
                    "No calibration matches could be scored in pass {0}.", passNumber));
                return null;
            }

            // Train LDA + 1% FDR target-decoy competition.
            var swLda = Stopwatch.StartNew();
            var matchArray = matches.ToArray();
            // Sort deterministically by (base_id, entry_id) so LDA sees a stable order.
            Array.Sort(matchArray, (a, b) => // Array.Sort OK: comparator's secondary key is the unique EntryId, so no ties
            {
                uint baseA = a.EntryId & 0x7FFFFFFF;
                uint baseB = b.EntryId & 0x7FFFFFFF;
                int cmp = baseA.CompareTo(baseB);
                if (cmp != 0)
                    return cmp;
                return a.EntryId.CompareTo(b.EntryId);
            });
            int nPassing = CalibrationScorer.TrainAndScoreCalibration(matchArray, false);
            swLda.Stop();

            int nTargetWins = 0;
            int nDecoyWins = 0;
            foreach (var m in matchArray)
            {
                if (m.QValue <= CAL_FDR_THRESHOLD)
                {
                    if (m.IsDecoy)
                        nDecoyWins++;
                    else nTargetWins++;
                }
            }
            _ctx.LogInfo(string.Format(
                "[TIMING] Calibration pass {0} LDA: {1:F2}s ({2} target wins, {3} decoy wins at 1% FDR)",
                passNumber, swLda.Elapsed.TotalSeconds, nTargetWins, nDecoyWins));
            _ctx.LogInfo(string.Format(
                "[COUNT] Calibration pass {0} LDA winners [{1}]: {2} target wins, {3} decoy wins at 1% FDR",
                passNumber, fileName, nTargetWins, nDecoyWins));
            _ctx.LogInfo(string.Format(
                "Calibration pass {0} LDA passing count: {1} (returned by TrainAndScoreCalibration)",
                passNumber, nPassing));

            // Cross-implementation diagnostic: dump per-entry LDA discriminant + q-value
            // sorted by entry_id for stable diff with rust_lda_scores.txt. Gated by
            // OSPREY_DUMP_LDA_SCORES; exits after write when OSPREY_LDA_SCORES_ONLY is set.
            if (OspreyDiagnostics.DumpLdaScores)
            {
                OspreyDiagnostics.WriteLdaScoresDump(passNumber, matchArray);
                if (OspreyDiagnostics.LdaScoresOnly)
                    OspreyDiagnostics.ExitAfterDump("OSPREY_LDA_SCORES_ONLY");
            }

            // Collect high-confidence target matches that also meet the S/N
            // quality gate (logs the S/N filter + calibration-point counts).
            var (libRtsDetected, measuredRtsDetected) = CollectCalibrationPoints(
                matchArray, matchRts, snrByEntryId, passNumber, fileName, nTargetWins);

            if (libRtsDetected.Count < minLoessPoints)
            {
                _ctx.LogWarning(string.Format(
                    "Insufficient calibration points in pass {0} ({1} < {2}).",
                    passNumber, libRtsDetected.Count, minLoessPoints));
                return null;
            }

            // Aggregate MS1 + MS2 mass errors from passing targets only
            // (same LDA + competition + S/N survivors as the RT points;
            // emits the MS2 cal-errors dump + the MS1/MS2 calibration logs).
            AggregateMassCalibrations(
                matchArray, snrByEntryId, config, passNumber,
                out var ms1Cal, out var ms2Cal);

            // Fit LOESS calibration.
            var swLoess = Stopwatch.StartNew();
            try
            {
                double[] libRts = libRtsDetected.ToArray();
                double[] measuredRts = measuredRtsDetected.ToArray();

                var calibratorConfig = new RTCalibratorConfig
                {
                    Bandwidth = config.RtCalibration.LoessBandwidth,
                    Degree = 1,
                    MinPoints = Math.Min(20, libRts.Length),
                    RobustnessIterations = 2,
                    OutlierRetention = 1.0, // LDA + S/N already filtered
                    ClassicalRobustIterations = OspreyEnvironment.LoessClassicalRobust
                };

                // NOTE: the OSPREY_DUMP_LOESS_INPUT diagnostic is emitted by
                // the caller (RunCalibration) so that pass 2 only dumps when
                // it is actually accepted -- mirrors Rust pipeline.rs (and
                // matches the PR #42 semantics of "dump reflects the
                // calibration actually used"). The pairs are returned via
                // CalibrationPassResult.LibRts/MeasuredRts.

                var calibrator = new RTCalibrator(calibratorConfig);
                var rtCal = calibrator.Fit(libRts, measuredRts);
                swLoess.Stop();

                var stats = rtCal.Stats();
                _ctx.LogInfo(string.Format("[TIMING] Calibration pass {0} LOESS fit: {1:F2}s",
                    passNumber, swLoess.Elapsed.TotalSeconds));
                _ctx.LogInfo(string.Format(
                    "RT calibration pass {0}: {1} points, R2={2:F4}, residual SD={3:F3} min, MAD={4:F3}",
                    passNumber, stats.NPoints, stats.RSquared, stats.ResidualSD, stats.MAD));

                return new CalibrationPassResult
                {
                    Calibration = rtCal,
                    Stats = stats,
                    Ms1Calibration = ms1Cal,
                    Ms2Calibration = ms2Cal,
                    MatchCount = matchArray.Length,
                    LibRts = libRts,
                    MeasuredRts = measuredRts,
                };
            }
            catch (Exception ex)
            {
                swLoess.Stop();
                _ctx.LogWarning(string.Format("RT calibration pass {0} failed: {1}",
                    passNumber, ex.Message));
                return null;
            }
        }

        /// <summary>
        /// Pre-preprocess all window spectra for XCorr using the calibration
        /// unit-bin scorer (~2K bins per spectrum, ~16 KB per array).
        /// Independent of resolution mode -- HRAM 100K-bin arrays would
        /// consume ~160 GB of LOH for 204K Astral spectra, so we use the
        /// small unit-bin form for calibration regardless. Main search
        /// still uses the resolution-mode bins.
        /// Calibration preprocess runs in pure f32 to match Rust upstream
        /// maccoss/osprey's native f32 XCorr path (cross-impl parity at
        /// F10 rounding noise, vs ~4e-6 drift under f64). f32 values are
        /// widened to double[] here so the downstream XcorrFromPreprocessed
        /// path is unchanged; the widening is lossless (f32 is a subset of
        /// f64) and preserves the f32 bit pattern for the final sum.
        /// </summary>
        private Dictionary<int, double[][]> PreprocessWindowsForXcorr(
            Dictionary<int, List<Spectrum>> spectraByWindowKey)
        {
            var preprocessedByWindowKey = new Dictionary<int, double[][]>();
            foreach (var kvp in spectraByWindowKey)
            {
                var pp = new double[kvp.Value.Count][];
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    float[] f32pp = s_calXcorrScorer.PreprocessSpectrumForXcorrF32(kvp.Value[i]);
                    var widened = new double[f32pp.Length];
                    for (int k = 0; k < f32pp.Length; k++)
                        widened[k] = f32pp[k];
                    pp[i] = widened;
                }
                preprocessedByWindowKey[kvp.Key] = pp;
            }
            return preprocessedByWindowKey;
        }

        /// <summary>
        /// Parallel-score each sampled calibration entry against its isolation
        /// window, returning the successful matches plus the per-entry S/N and
        /// (libRt, measuredRt) maps. Emits the pass timing + match-count logs.
        /// </summary>
        private (ConcurrentBag<CalibrationMatch> Matches,
            ConcurrentDictionary<uint, double> SnrByEntryId,
            ConcurrentDictionary<uint, KeyValuePair<double, double>> MatchRts) ScoreCalibrationMatches(
                int passNumber,
                List<LibraryEntry> sampledEntries,
                Dictionary<int, List<Spectrum>> spectraByWindowKey,
                Dictionary<int, double[][]> preprocessedByWindowKey,
                List<MS1Spectrum> ms1Spectra,
                ScoringContext context,
                double rtSlope, double rtIntercept, double tolerance,
                RTCalibration calibrationModel)
        {
            var config = context.Config;
            var resolution = context.Resolution;
            var fileName = context.FileName;

            var swScoring = Stopwatch.StartNew();
            var matches = new ConcurrentBag<CalibrationMatch>();
            var snrByEntryId = new ConcurrentDictionary<uint, double>();
            var matchRts = new ConcurrentDictionary<uint, KeyValuePair<double, double>>();

            Parallel.ForEach(sampledEntries, new ParallelOptions
            {
                MaxDegreeOfParallelism = config.NThreads
            },
            () => resolution.CreateScorer(),
            (entry, loopState, localScorer) =>
            {
                double entrySnr;
                double entryLibRt;
                double entryMeasuredRt;
                var match = ScoreCalibrationEntry(
                    entry, spectraByWindowKey, preprocessedByWindowKey, ms1Spectra, context,
                    rtSlope, rtIntercept, tolerance,
                    calibrationModel,
                    localScorer,
                    out entrySnr, out entryLibRt, out entryMeasuredRt);
                if (match != null)
                {
                    matches.Add(match);
                    snrByEntryId[entry.Id] = entrySnr;
                    matchRts[entry.Id] = new KeyValuePair<double, double>(
                        entryLibRt, entryMeasuredRt);
                }
                return localScorer;
            },
            localScorer => { });
            swScoring.Stop();
            _ctx.LogInfo(string.Format(
                "[TIMING] Calibration pass {0} scoring: {1:F2}s ({2} matches)",
                passNumber, swScoring.Elapsed.TotalSeconds, matches.Count));
            _ctx.LogInfo(string.Format(
                "[COUNT] Calibration pass {0} matches scored [{1}]: {2}",
                passNumber, fileName, matches.Count));
            return (matches, snrByEntryId, matchRts);
        }

        /// <summary>
        /// Collect the high-confidence target matches that pass the 1% FDR gate
        /// and the S/N >= <see cref="MIN_SNR_FOR_RT_CAL"/> quality filter,
        /// returning their (libRt, measuredRt) pairs as the LOESS fit input.
        /// Emits the S/N-filter and calibration-point-count logs.
        /// </summary>
        private (List<double> LibRts, List<double> MeasuredRts) CollectCalibrationPoints(
            CalibrationMatch[] matchArray,
            ConcurrentDictionary<uint, KeyValuePair<double, double>> matchRts,
            ConcurrentDictionary<uint, double> snrByEntryId,
            int passNumber, string fileName, int nTargetWins)
        {
            var libRtsDetected = new List<double>();
            var measuredRtsDetected = new List<double>();
            int nSnrFiltered = 0;
            foreach (var m in matchArray)
            {
                if (m.IsDecoy)
                    continue;
                if (m.QValue > CAL_FDR_THRESHOLD)
                    continue;

                KeyValuePair<double, double> rtPair;
                if (!matchRts.TryGetValue(m.EntryId, out rtPair))
                    continue;

                double snr;
                if (!snrByEntryId.TryGetValue(m.EntryId, out snr))
                    snr = 0.0;

                if (snr < MIN_SNR_FOR_RT_CAL)
                {
                    nSnrFiltered++;
                    continue;
                }

                libRtsDetected.Add(rtPair.Key);
                measuredRtsDetected.Add(rtPair.Value);
            }

            if (nSnrFiltered > 0)
            {
                _ctx.LogInfo(string.Format(
                    "  RT quality filter (pass {0}): {1} -> {2} peptides (removed {3} with S/N < {4:F1})",
                    passNumber, nTargetWins, libRtsDetected.Count, nSnrFiltered, MIN_SNR_FOR_RT_CAL));
            }

            _ctx.LogInfo(string.Format(
                "[COUNT] Calibration pass {0} high-quality (S/N>=5) [{1}]: {2}",
                passNumber, fileName, libRtsDetected.Count));
            _ctx.LogInfo(string.Format("Pass {0} found {1} calibration points",
                passNumber, libRtsDetected.Count));

            return (libRtsDetected, measuredRtsDetected);
        }

        /// <summary>
        /// Aggregate MS1 + MS2 mass errors from passing targets only (those
        /// surviving LDA + competition + the S/N >= 5.0 filter -- the same set
        /// used for RT calibration points), compute the single-level MS1/MS2
        /// mass calibrations, and emit the MS2 cal-errors bisection dump plus
        /// the MS1/MS2 calibration logs. Mirrors Rust pipeline.rs:610-619.
        /// </summary>
        private void AggregateMassCalibrations(
            CalibrationMatch[] matchArray,
            ConcurrentDictionary<uint, double> snrByEntryId,
            OspreyConfig config, int passNumber,
            out MzCalibrationResult ms1Calibration,
            out MzCalibrationResult ms2Calibration)
        {
            var allMs1Errors = new List<double>();
            var allMs2Errors = new List<double>();
            // Track contributing matches in a stable list so a cross-impl
            // bisection dump can replay them in the same order the
            // calibration accumulator sees their errors.
            var contributingMatches = new List<CalibrationMatch>();
            foreach (var m in matchArray)
            {
                if (m.Ms2MassErrors == null || m.IsDecoy || m.QValue > CAL_FDR_THRESHOLD)
                    continue;
                double snr;
                if (!snrByEntryId.TryGetValue(m.EntryId, out snr) || snr < MIN_SNR_FOR_RT_CAL)
                    continue;
                if (m.Ms1Error.HasValue)
                    allMs1Errors.Add(m.Ms1Error.Value);
                allMs2Errors.AddRange(m.Ms2MassErrors);
                contributingMatches.Add(m);
            }
            if (OspreyDiagnostics.DumpMs2CalErrors)
            {
                OspreyDiagnostics.WriteMs2CalErrorsDump(contributingMatches);
                if (OspreyDiagnostics.Ms2CalErrorsOnly)
                    OspreyDiagnostics.ExitAfterDump("OSPREY_MS2_CAL_ERRORS_ONLY");
            }
            string unitStr = config.FragmentTolerance.Unit == ToleranceUnit.Ppm ? "ppm" : "Th";
            ms1Calibration = MzCalibration.CalculateSingleLevel(allMs1Errors.ToArray(), unitStr);
            ms2Calibration = MzCalibration.CalculateSingleLevel(allMs2Errors.ToArray(), unitStr);
            _ctx.LogInfo(string.Format(
                "MS1 calibration (pass {0}): mean={1:F4} {2}, SD={3:F4} {2}, 3*SD={4:F4} {2} ({5} errors)",
                passNumber, ms1Calibration.Mean, unitStr, ms1Calibration.SD, 3.0 * ms1Calibration.SD, allMs1Errors.Count));
            _ctx.LogInfo(string.Format(
                "MS2 calibration (pass {0}): mean={1:F4} {2}, SD={3:F4} {2}, 3*SD={4:F4} {2} ({5} errors)",
                passNumber, ms2Calibration.Mean, unitStr, ms2Calibration.SD, 3.0 * ms2Calibration.SD, allMs2Errors.Count));
        }

        /// <summary>
        /// Sample library entries for calibration discovery, keeping paired target-decoy
        /// pairs together (matched by base_id = entry_id &amp; 0x7FFFFFFF).
        /// Direct port of Rust sample_library_for_calibration (osprey-scoring/src/batch.rs:1450).
        /// Uses a 2D (RT x m/z) stratified grid with deterministic stride sampling.
        /// This is the first randomized/selected step in the pipeline, so it must match
        /// Rust exactly for the two tools to process the same calibration peptides.
        /// </summary>
        private static List<LibraryEntry> SampleLibraryForCalibration(
            List<LibraryEntry> library, int sampleSize, ulong seed)
        {
            if (sampleSize == 0)
                return new List<LibraryEntry>(library);

            var targets = new List<LibraryEntry>();
            var decoys = new List<LibraryEntry>();
            foreach (var entry in library)
            {
                if (entry.IsDecoy)
                    decoys.Add(entry);
                else targets.Add(entry);
            }

            if (targets.Count <= sampleSize)
                return new List<LibraryEntry>(library);

            // Build target_id -> decoy map (decoy_id = target_id | 0x80000000)
            var decoyMap = new Dictionary<uint, LibraryEntry>(decoys.Count);
            foreach (var d in decoys)
                decoyMap[d.Id & 0x7FFFFFFF] = d;

            // 2D stratified sampling: divide RT x m/z space into a grid.
            // ~sqrt(sample_size)/2 bins per axis for good 2D coverage.
            int binsPerAxis = (int)Math.Max(5, Math.Ceiling(Math.Sqrt(sampleSize) / 2.0));

            double rtMin = double.MaxValue, rtMax = double.MinValue;
            double mzMin = double.MaxValue, mzMax = double.MinValue;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t.RetentionTime < rtMin)
                    rtMin = t.RetentionTime;
                if (t.RetentionTime > rtMax)
                    rtMax = t.RetentionTime;
                if (t.PrecursorMz < mzMin)
                    mzMin = t.PrecursorMz;
                if (t.PrecursorMz > mzMax)
                    mzMax = t.PrecursorMz;
            }
            double rtRange = Math.Max(1e-6, rtMax - rtMin);
            double mzRange = Math.Max(1e-6, mzMax - mzMin);
            double rtBinWidth = rtRange / binsPerAxis;
            double mzBinWidth = mzRange / binsPerAxis;

            // Assign each target to a 2D grid cell
            var grid = new List<int>[binsPerAxis, binsPerAxis];
            for (int i = 0; i < binsPerAxis; i++)
                for (int j = 0; j < binsPerAxis; j++)
                    grid[i, j] = new List<int>();

            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                int rtBin = (int)Math.Floor((t.RetentionTime - rtMin) / rtBinWidth);
                int mzBin = (int)Math.Floor((t.PrecursorMz - mzMin) / mzBinWidth);
                if (rtBin >= binsPerAxis)
                    rtBin = binsPerAxis - 1;
                if (mzBin >= binsPerAxis)
                    mzBin = binsPerAxis - 1;
                grid[rtBin, mzBin].Add(i);
            }

            // Count non-empty cells and compute per-cell quota
            int nOccupied = 0;
            for (int i = 0; i < binsPerAxis; i++)
                for (int j = 0; j < binsPerAxis; j++)
                    if (grid[i, j].Count > 0)
                        nOccupied++;

            int perCell = nOccupied > 0 ? sampleSize / nOccupied : 1;
            if (perCell < 1)
                perCell = 1;

            // Diagnostic dump: scalar parameters + full grid contents,
            // matching Rust's dump format for direct diff.
            if (OspreyDiagnostics.DumpCalSample)
            {
                OspreyDiagnostics.WriteCalScalarsAndGridDump(
                    targets, decoys, binsPerAxis,
                    rtMin, rtMax, mzMin, mzMax,
                    rtRange, mzRange, rtBinWidth, mzBinWidth,
                    nOccupied, perCell, seed, grid);
            }

            // Deterministic stride sampling from each cell.
            int offset = (int)(seed & 0x7FFFFFFF);
            var sampledIds = new HashSet<uint>();
            var sampled = new List<LibraryEntry>(sampleSize * 2);

            for (int ri = 0; ri < binsPerAxis; ri++)
            {
                for (int ci = 0; ci < binsPerAxis; ci++)
                {
                    var cell = grid[ri, ci];
                    if (cell.Count == 0)
                        continue;

                    int nTake = Math.Min(cell.Count, perCell);
                    int stride = Math.Max(1, cell.Count / nTake);
                    int cellOffset = offset % Math.Max(1, cell.Count);

                    for (int j = 0; j < nTake; j++)
                    {
                        int idx = (cellOffset + j * stride) % cell.Count;
                        var target = targets[cell[idx]];

                        if (sampledIds.Contains(target.Id))
                            continue;
                        sampledIds.Add(target.Id);
                        sampled.Add(target);

                        LibraryEntry decoy;
                        if (decoyMap.TryGetValue(target.Id, out decoy))
                            sampled.Add(decoy);
                    }
                }
            }

            // Second pass: if under-sampled, add more from occupied cells.
            if (sampledIds.Count < sampleSize)
            {
                int remaining = sampleSize - sampledIds.Count;
                int extraPerCell = Math.Max(1, remaining / Math.Max(1, nOccupied));

                bool done = false;
                for (int ri = 0; ri < binsPerAxis && !done; ri++)
                {
                    for (int ci = 0; ci < binsPerAxis; ci++)
                    {
                        var cell = grid[ri, ci];
                        if (cell.Count == 0)
                            continue;

                        int added = 0;
                        foreach (int targetIdx in cell)
                        {
                            if (added >= extraPerCell)
                                break;
                            var target = targets[targetIdx];
                            if (sampledIds.Contains(target.Id))
                                continue;
                            sampledIds.Add(target.Id);
                            sampled.Add(target);
                            LibraryEntry decoy;
                            if (decoyMap.TryGetValue(target.Id, out decoy))
                                sampled.Add(decoy);
                            added++;
                        }

                        if (sampledIds.Count >= sampleSize) { done = true; break; }
                    }
                }
            }

            return sampled;
        }

        /// <summary>
        /// Score a single library entry for calibration: extract fragment XICs across
        /// spectra in the entry's isolation window that fall within the initial RT
        /// tolerance, detect the best co-eluting peak, and compute the four LDA
        /// features at the apex (correlation, LibCosine, top-6 matched, XCorr).
        /// Returns null if the entry has no viable peak.
        /// On pass 1 (calibrationModel == null), expectedRt is computed from the
        /// linear (rtSlope * library_rt + rtIntercept) mapping. On pass 2, the
        /// LOESS-fitted RTCalibration is used to predict expected_rt and the
        /// (refined) tolerance is much tighter.
        /// </summary>
        private CalibrationMatch ScoreCalibrationEntry(
            LibraryEntry entry,
            Dictionary<int, List<Spectrum>> spectraByWindowKey,
            Dictionary<int, double[][]> preprocessedByWindowKey,
            List<MS1Spectrum> ms1Spectra,
            ScoringContext context,
            double rtSlope, double rtIntercept, double initialTolerance,
            RTCalibration calibrationModel,
            SpectralScorer scorer,
            out double signalToNoise,
            out double libraryRt,
            out double measuredRt)
        {
            var config = context.Config;
            var resolution = context.Resolution;
            signalToNoise = 0.0;
            libraryRt = entry.RetentionTime;
            measuredRt = 0.0;

            if (entry.Fragments == null || entry.Fragments.Count < 2)
                return null;

            // Find spectra in the entry's isolation window.
            int windowKey = (int)Math.Round(entry.PrecursorMz * 10.0);
            List<Spectrum> windowSpectra;
            if (!spectraByWindowKey.TryGetValue(windowKey, out windowSpectra))
            {
                // Try neighbouring window keys (handles off-by-one due to rounding).
                if (!spectraByWindowKey.TryGetValue(windowKey - 1, out windowSpectra) &&
                    !spectraByWindowKey.TryGetValue(windowKey + 1, out windowSpectra))
                {
                    // Fall back to linear scan across windows that contain this precursor.
                    windowSpectra = null;
                    foreach (var kvp in spectraByWindowKey)
                    {
                        var first = kvp.Value[0];
                        if (first.IsolationWindow.Contains(entry.PrecursorMz))
                        {
                            windowSpectra = kvp.Value;
                            break;
                        }
                    }
                    if (windowSpectra == null)
                        return null;
                }
            }
            else if (!windowSpectra[0].IsolationWindow.Contains(entry.PrecursorMz))
            {
                // Key collision where the actual isolation window doesn't contain this precursor.
                return null;
            }

            // Compute expected RT for this library entry.
            // Pass 1 (calibrationModel == null): use the linear pre-fit mapping
            //     rtSlope * library_rt + rtIntercept
            // Pass 2 (calibrationModel != null): use the LOESS-fitted prediction
            //     rtCalibration.Predict(library_rt)
            // Matches Rust's predict_fn pattern in pipeline.rs:740.
            double expectedRt = calibrationModel != null
                ? calibrationModel.Predict(entry.RetentionTime)
                : entry.RetentionTime * rtSlope + rtIntercept;

            // Diagnostic: record per-entry m/z + RT window selection.
            // C# selects ONE window per entry (the first match in dictionary order),
            // unlike Rust which scores in ALL matching windows. Capturing this here
            // before the RT/2-of-6 filter so it matches Rust's pre-filter dump.
            if (OspreyDiagnostics.CalWindowsCollecting)
            {
                OspreyDiagnostics.AddCalWindowRow(
                    entry, windowSpectra[0].IsolationWindow,
                    expectedRt,
                    expectedRt - initialTolerance,
                    expectedRt + initialTolerance);
            }

            // Resolve the actual window key that was used (may differ from primary
            // due to neighbour-key or linear-scan fallback).
            int resolvedWindowKey = windowKey;
            if (!spectraByWindowKey.ContainsKey(windowKey))
            {
                if (spectraByWindowKey.ContainsKey(windowKey - 1))
                    resolvedWindowKey = windowKey - 1;
                else if (spectraByWindowKey.ContainsKey(windowKey + 1))
                    resolvedWindowKey = windowKey + 1;
                else
                {
                    // Linear scan fallback - find the key that matched
                    foreach (var kvp in spectraByWindowKey)
                    {
                        if (kvp.Value == windowSpectra)
                        {
                            resolvedWindowKey = kvp.Key;
                            break;
                        }
                    }
                }
            }

            // Filter by RT tolerance and top-6 fragment prefilter.
            // Track window indices for preprocessed XCorr lookup.
            var candidateSpectra = new List<Spectrum>();
            var candidateWindowIndices = new List<int>();
            double[][] windowPreprocessed = null;
            preprocessedByWindowKey.TryGetValue(resolvedWindowKey, out windowPreprocessed);
            for (int si = 0; si < windowSpectra.Count; si++)
            {
                var spec = windowSpectra[si];
                if (Math.Abs(spec.RetentionTime - expectedRt) > initialTolerance)
                    continue;
                if (!FragmentMath.HasTopNFragmentMatch(entry, spec.Mzs, config.FragmentTolerance))
                    continue;
                candidateSpectra.Add(spec);
                candidateWindowIndices.Add(si);
            }

            if (candidateSpectra.Count < MIN_COELUTION_SPECTRA)
                return null;

            // Build shared RT axis for XIC extraction.
            int nScans = candidateSpectra.Count;
            double[] rts = new double[nScans];
            for (int i = 0; i < nScans; i++)
                rts[i] = candidateSpectra[i].RetentionTime;

            // Per-entry chromatogram diagnostic. Dump candidates + extracted XICs.
            // We default to pass 1 in OspreyDiagnostics because the cross-tool
            // bisection walks downstream: until pass 1 chromatograms match,
            // there's no point comparing pass 2 (which depends on pass 1's
            // LOESS fit).
            int currentPass = calibrationModel != null ? 2 : 1;

            // Extract XICs for the top-N most intense library fragments.
            var xics = ExtractTopNFragmentXics(
                entry, candidateSpectra, rts, CAL_TOP_N_FRAGMENTS, config);

            if (OspreyDiagnostics.ShouldDumpCalXicFor(entry.Id, currentPass))
            {
                OspreyDiagnostics.WriteCalXicEntryDumpAndExit(
                    entry, currentPass, calibrationModel,
                    expectedRt, initialTolerance, rtSlope, rtIntercept,
                    candidateSpectra, xics);
            }

            if (xics.Count < 2)
                return null;

            // Detect consensus CWT peaks and score by pairwise correlation sum.
            var peaks = CwtPeakDetector.DetectConsensusPeaks(xics, 0.0);

            // Fallback: when CWT returns no consensus peaks, run DetectAllXicPeaks
            // on the reference fragment XIC alone. This rescues entries where
            // cross-fragment consensus is weak (one dominant fragment, noisy
            // others) but the reference has a clean peak shape. Matches Rust
            // batch.rs:2744-2751: the `cwt_candidates.is_empty()` branch.
            if (peaks.Count == 0)
            {
                // Pick the reference fragment (highest total intensity). Uses `>=`
                // so ties resolve to the LAST fragment, matching Rust's max_by.
                int refFallbackIdx = 0;
                double refTotal = -1.0;
                for (int i = 0; i < xics.Count; i++)
                {
                    double total = 0.0;
                    double[] inten = xics[i].Intensities;
                    for (int k = 0; k < inten.Length; k++) total += inten[k];
                    if (total >= refTotal)
                    {
                        refTotal = total;
                        refFallbackIdx = i;
                    }
                }
                var fallbackXic = xics[refFallbackIdx];
                peaks = PeakDetector.DetectAllXicPeaks(
                    fallbackXic.RetentionTimes,
                    fallbackXic.Intensities,
                    0.01, // min_height
                    5.0); // peak_boundary
            }

            if (peaks.Count == 0)
                return null;

            // Score each candidate peak by its summed pairwise fragment-XIC
            // correlation; keep the highest (ties -> last, matching Rust max_by).
            var (bestPeak, bestCorrSum) = ScorePeaksByCorrelation(peaks, xics);

            if (bestPeak == null || bestCorrSum < MIN_COELUTION_CORR_SCORE)
                return null;

            // Identify the reference XIC - the single fragment with the highest
            // total intensity across the extracted XICs. This is the signal that
            // feeds SNR computation and the apex selection. Direct port of
            // Rust's `ref_idx = xics.max_by(total intensity)` in batch.rs:~2718.
            //
            // Note: Rust's `Iterator::max_by` returns the LAST element on ties,
            // so we use `>=` (not `>`) here to match. Without this, fragments
            // with identical total intensities would select different reference
            // XICs between the two tools, causing downstream apex divergence.
            int refIdx = 0;
            double bestTotalIntensity = -1.0;
            for (int i = 0; i < xics.Count; i++)
            {
                double total = 0.0;
                double[] inten = xics[i].Intensities;
                for (int k = 0; k < inten.Length; k++)
                    total += inten[k];
                if (total >= bestTotalIntensity)
                {
                    bestTotalIntensity = total;
                    refIdx = i;
                }
            }
            var refXic = xics[refIdx];
            double[] refIntensities = refXic.Intensities;

            // Apex is the highest-intensity point within the peak boundaries of
            // the reference XIC. No top-6 constraint: Rust's batch.rs:2797-2802
            // is a straight argmax over `ref_xic[ref_start..=ref_end]`. Uses `>=`
            // so ties resolve to the LAST index, matching Rust `max_by`.
            int apexLocalIdx = bestPeak.StartIndex;
            double apexVal = refIntensities[Math.Min(apexLocalIdx, refIntensities.Length - 1)];
            for (int scan = bestPeak.StartIndex; scan <= bestPeak.EndIndex; scan++)
            {
                if (scan >= refIntensities.Length)
                    break;
                if (refIntensities[scan] >= apexVal)
                {
                    apexVal = refIntensities[scan];
                    apexLocalIdx = scan;
                }
            }

            // Apex RT from the reference XIC (shared time axis across fragments).
            double apexRt = refXic.RetentionTimes[apexLocalIdx];
            measuredRt = apexRt;

            // SNR is computed on the reference fragment's raw intensities
            // (NOT the composite sum). Direct port of Rust batch.rs:2803-2806.
            signalToNoise = PeakDetector.ComputeSnr(
                refIntensities, apexLocalIdx, bestPeak.StartIndex, bestPeak.EndIndex);

            // Map the apex RT to the candidate spectrum with the closest RT
            // for feature computation. In practice this returns apexLocalIdx
            // since the XIC time axis is built directly from candidate spectrum
            // RTs, but we port Rust's lookup verbatim for parity.
            int apexSpecLocalIdx = 0;
            double bestDt = Math.Abs(candidateSpectra[0].RetentionTime - apexRt);
            for (int i = 1; i < candidateSpectra.Count; i++)
            {
                double dt = Math.Abs(candidateSpectra[i].RetentionTime - apexRt);
                if (dt < bestDt)
                {
                    bestDt = dt;
                    apexSpecLocalIdx = i;
                }
            }
            var apexSpectrum = candidateSpectra[apexSpecLocalIdx];

            // Compute the four LDA features at the apex.
            double libCosineApex = scorer.LibCosine(
                apexSpectrum, entry, config.FragmentTolerance);
            // XCorr at apex always uses the calibration unit-bin scorer
            // (matches the pre-preprocessed arrays built with s_calXcorrScorer).
            int apexWindowIdx = candidateWindowIndices[apexSpecLocalIdx];
            double xcorrApex = (windowPreprocessed != null && apexWindowIdx < windowPreprocessed.Length)
                ? s_calXcorrScorer.XcorrFromPreprocessed(windowPreprocessed[apexWindowIdx], entry)
                : s_calXcorrScorer.XcorrAtScan(apexSpectrum, entry);
            byte top6Matched = CountTop6Matches(entry, apexSpectrum, config);

            // Collect MS2 fragment mass errors at apex for m/z calibration.
            var ms2Errors = CollectMs2FragmentErrors(entry, apexSpectrum, config);

            // MS1 precursor mass error at apex for MS1 mass calibration.
            double? ms1Error = ComputeMs1MassError(entry, ms1Spectra, apexRt, config);

            return new CalibrationMatch
            {
                EntryId = entry.Id,
                IsDecoy = entry.IsDecoy,
                Sequence = entry.Sequence,
                ScanNumber = apexSpectrum.ScanNumber,
                CorrelationScore = bestCorrSum,
                LibcosineApex = libCosineApex,
                Top6MatchedApex = top6Matched,
                XcorrScore = xcorrApex,
                IsotopeCosine = 0.0,
                DiscriminantScore = bestCorrSum,
                QValue = 1.0,
                Ms2MassErrors = ms2Errors.ToArray(),
                Ms1Error = ms1Error
            };
        }

        /// <summary>
        /// Score each candidate CWT peak by the sum of pairwise Pearson
        /// correlations between fragment XICs over the peak's index range,
        /// and return the highest-scoring peak (ties resolve to the last,
        /// matching Rust's max_by) with its correlation sum. bestPeak is null
        /// when no peak spans at least three points.
        /// </summary>
        private static (XICPeakBounds BestPeak, double BestCorrSum) ScorePeaksByCorrelation(
            List<XICPeakBounds> peaks, List<XicData> xics)
        {
            XICPeakBounds bestPeak = null;
            double bestCorrSum = double.NegativeInfinity;

            foreach (var peak in peaks)
            {
                int si = peak.StartIndex;
                int ei = peak.EndIndex;
                if (ei - si + 1 < 3)
                    continue;

                double corrSum = 0.0;
                for (int i = 0; i < xics.Count; i++)
                {
                    double[] inti = xics[i].Intensities;
                    for (int j = i + 1; j < xics.Count; j++)
                    {
                        double[] intj = xics[j].Intensities;
                        double corr = ScoringMath.PearsonOverRange(inti, intj, si, ei);
                        if (!double.IsNaN(corr))
                            corrSum += corr;
                    }
                }

                // Use >= to match Rust's max_by tie-break (last wins on ties).
                if (corrSum >= bestCorrSum)
                {
                    bestCorrSum = corrSum;
                    bestPeak = peak;
                }
            }
            return (bestPeak, bestCorrSum);
        }

        /// <summary>
        /// Collect MS2 fragment mass errors at the apex spectrum for m/z
        /// calibration. Matches Rust topn_fragment_match_with_errors: uses the
        /// TOP-6 fragments by intensity (stable sort to match Rust's tie-break),
        /// matches each within the fragment tolerance, and reports the mass
        /// error of the closest peak in fragment-tolerance units.
        /// </summary>
        private static List<double> CollectMs2FragmentErrors(
            LibraryEntry entry, Spectrum apexSpectrum, OspreyConfig config)
        {
            var ms2Errors = new List<double>();
            // Select top-6 fragment indices by descending intensity
            int nFragsForErrors = entry.Fragments.Count;
            int nTopForErrors = Math.Min(nFragsForErrors, CAL_TOP_N_FRAGMENTS);
            int[] topErrorIndices;
            if (nFragsForErrors <= CAL_TOP_N_FRAGMENTS)
            {
                topErrorIndices = new int[nFragsForErrors];
                for (int ti = 0; ti < nFragsForErrors; ti++)
                    topErrorIndices[ti] = ti;
            }
            else
            {
                // LINQ OrderByDescending is stable per .NET contract;
                // List<T>.Sort with Comparison<T> is unstable
                // (introsort) and produces different top-N picks
                // than Rust's stable slice::sort_by on RelativeIntensity
                // ties. Match Rust by using the stable sort.
                topErrorIndices = Enumerable.Range(0, nFragsForErrors)
                    .OrderByDescending(ti => entry.Fragments[ti].RelativeIntensity)
                    .Take(nTopForErrors)
                    .ToArray();
            }

            foreach (int fragIdx in topErrorIndices)
            {
                var frag = entry.Fragments[fragIdx];
                double tolDa = config.FragmentTolerance.ToleranceDa(frag.Mz);
                double lower = frag.Mz - tolDa;
                double upper = frag.Mz + tolDa;

                int lo = ScoringMath.BinarySearchLowerBound(apexSpectrum.Mzs, lower);
                if (lo < apexSpectrum.Mzs.Length && apexSpectrum.Mzs[lo] <= upper)
                {
                    double bestMz = apexSpectrum.Mzs[lo];
                    double bestDiff = Math.Abs(bestMz - frag.Mz);
                    for (int k = lo + 1; k < apexSpectrum.Mzs.Length && apexSpectrum.Mzs[k] <= upper; k++)
                    {
                        double diff = Math.Abs(apexSpectrum.Mzs[k] - frag.Mz);
                        if (diff < bestDiff)
                        {
                            bestDiff = diff;
                            bestMz = apexSpectrum.Mzs[k];
                        }
                    }
                    ms2Errors.Add(config.FragmentTolerance.MassError(frag.Mz, bestMz));
                }
            }
            return ms2Errors;
        }

        /// <summary>
        /// MS1 precursor mass error at the apex RT for MS1 mass calibration.
        /// Port of osprey-scoring/src/batch.rs:2912-2940 -- extract M+0 from the
        /// MS1 scan closest to the apex RT using the config precursor tolerance;
        /// report the error in fragment-tolerance units so MS1 + MS2 errors
        /// share the same unit for the MzQCData aggregator. Returns null when
        /// there are no MS1 spectra or no M+0 isotope peak is found.
        /// </summary>
        private double? ComputeMs1MassError(
            LibraryEntry entry, List<MS1Spectrum> ms1Spectra, double apexRt, OspreyConfig config)
        {
            double? ms1Error = null;
            if (ms1Spectra != null && ms1Spectra.Count > 0)
            {
                int charge = entry.Charge > 0 ? entry.Charge : 1;
                double precursorTolPpm = config.PrecursorTolerance != null
                    && config.PrecursorTolerance.Unit == ToleranceUnit.Ppm
                    ? config.PrecursorTolerance.Tolerance
                    : 10.0;
                var apexMs1 = FindNearestMs1(ms1Spectra, apexRt);
                if (apexMs1 != null)
                {
                    var envelope = IsotopeEnvelope.Extract(
                        apexMs1, entry.PrecursorMz, charge, precursorTolPpm);
                    if (envelope.HasM0 && envelope.M0ObservedMz.HasValue)
                    {
                        ms1Error = config.FragmentTolerance.MassError(
                            entry.PrecursorMz, envelope.M0ObservedMz.Value);
                    }
                }
            }
            return ms1Error;
        }

        /// <summary>
        /// Render a prefix list in Rust's <c>{:?}</c> debug format
        /// (<c>["DECOY_", "rev_", "decoy_"]</c>) for log messages and
        /// error reports, so cross-impl messages compare consistently.
        /// </summary>
        private static string FormatPrefixList(IList<string> prefixes)
        {
            var sb = new System.Text.StringBuilder("[");
            if (prefixes != null)
            {
                for (int i = 0; i < prefixes.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append('"').Append(prefixes[i] ?? string.Empty).Append('"');
                }
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
