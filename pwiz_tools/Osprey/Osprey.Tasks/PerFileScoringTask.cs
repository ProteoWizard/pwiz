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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.FDR.ModelDiagnostics;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Tasks
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
    /// <c>internal</c>) methods (LoadLibrary,
    /// ProcessFile) plus the --task FirstPassFDR / --task SecondPassFDR input
    /// loading paths that share the same per-file collection layout.
    /// The inline Stage 1-4 block from <c>AnalysisPipeline.Run</c>
    /// moved here verbatim; the only changes are LogInfo / LogWarning
    /// / LogError -> ctx.LogInfo etc. and a return-false / set
    /// ctx.ExitCode flow for the early-exit paths the original block
    /// had as <c>return 0</c> / <c>return 1</c>.
    ///
    /// Outputs (FullLibrary, LibraryById, PerFileEntries,
    /// PerFileCalibrations, PerFileIsolationMz, PerFileParquetPaths) are exposed
    /// as instance properties for FirstJoinTask + downstream tasks to
    /// consume after this one completes successfully.
    /// </summary>
    internal sealed class PerFileScoringTask : OspreyTask
    {
        // Equal-weight progress segments one input file's work is divided into for
        // the --parallel-files "[i] p%" aggregate line: read spectra, calibrate RT/
        // mass, score, write the scores parquet. ProcessFile advances them through
        // MultiProgressReporter.BeginSegment at each phase boundary; off the parallel
        // path those calls are no-ops.
        private const int PROCESS_FILE_SEGMENTS = 4;

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
            typeof(PerFileCalibrationDiagnostics),
            typeof(PerFileIsolationMz), typeof(PerFileParquetPaths),
            typeof(RescoreBundle), typeof(ScoredEntries),
            // Must be declared, not just published: PipelineContext builds its
            // producer registry from this list, so an undeclared byproduct cannot be
            // lazily materialized and ctx.Get<T> throws UnknownByproductException on a
            // cache miss. Without this entry FdrProjections only resolves because
            // FirstJoinTask happens to read ScoredEntries first, which materializes
            // this task and co-publishes both -- an ordering coupling, not a contract.
            typeof(FdrProjections)
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
        // Per-file CAL-view calibration diagnostics for the --model-diagnostics HTML
        // report, keyed by file name in input order (mirrors _perFileCalibrations).
        // Empty on a normal run: only populated on the compute path when
        // config.ModelDiagnostics is on. Published as PerFileCalibrationDiagnostics.
        private ConcurrentDictionary<string, ModelDiagnosticsData.CalFileRow> _perFileCalibrationDiagnostics
            = new ConcurrentDictionary<string, ModelDiagnosticsData.CalFileRow>();
        // The per-run mass-error unit ("ppm" / "Th") for the CAL view, recorded from the
        // first calibrated file (identical across files in a run). Null until then;
        // published on PerFileCalibrationDiagnostics. Volatile: written under the parallel
        // per-file fan-out, read once at publish.
        private volatile string _calibrationMassUnit;
        private ConcurrentDictionary<string, IReadOnlyList<(double Lo, double Hi)>> _perFileIsolationMz
            = new ConcurrentDictionary<string, IReadOnlyList<(double Lo, double Hi)>>();
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
                yield return CalibrationIO.CalibrationPathForInput(input, ArtifactPaths.ResolveOutputDir(input));
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
            var config = ctx.Config;

            // Stage 1: Load library + generate/pair decoys, then build the
            // full target+decoy library and its by-id lookup.
            if (!LoadLibraryAndDecoys(config, out var fullLibrary, ctx))
                return false;

            // Stage 2-4: Per-file calibration + coelution scoring
            // Process files in parallel when multiple files are provided.
            var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>();
            // Per-file RT calibration handles harvested by ProcessFile so
            // Stage 6 reconciliation has the live RTCalibration objects.
            var perFileCalibrations = new ConcurrentDictionary<string, RTCalibration>();
            // Per-file isolation-window m/z intervals harvested alongside the
            // calibration so Stage 6 gap-fill filters candidates by isolation
            // coverage (essential for GPF datasets with disjoint m/z ranges).
            var perFileIsolationMz = new ConcurrentDictionary<string, IReadOnlyList<(double Lo, double Hi)>>();
            // fileName -> .scores.parquet path, populated below so Stage 6
            // reconciliation can lazily load CWT candidates per file via
            // ParquetScoreCache.LoadCwtCandidatesFromParquet.
            var perFileParquetPaths = new Dictionary<string, string>();
            // Decouple scoring from the join (issue #4355): during the scoring
            // loop below we record ONLY each scored file's name, in original
            // input order (by fileIdx -- the parallel branch re-collects by
            // input index, the sequential branch appends in that same order),
            // and defer materializing its FdrEntry stubs
            // until after the loop -- reloading them from the just-written
            // .scores.parquet. Retaining every file's stub buffer while the
            // ~20 GB per-file scoring transient is live is what OOMs a large
            // straight-through run; deferring holds the scoring peak to
            // library + one file's transient (flat in file count).
            var scoredFileNames = new List<string>();

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
            // of --task PerFileScoring.
            var parquetFooterMetadata = new Dictionary<string, string>
            {
                { @"osprey.version", OspreyVersion.Current },
                { @"osprey.search_hash", config.Identity.SearchParameterHash() },
                { @"osprey.library_hash", config.Identity.LibraryIdentityHash() },
                { @"osprey.reconciled", @"false" },
            };

            // Resolve how many input files run concurrently for this invocation
            // (--parallel-files, the OSPREY_MAX_PARALLEL_FILES back-compat cap,
            // free RAM, and core count) in the one shared place. Stored on the
            // per-run RunPlan (driver-owned run state), not on the parsed
            // OspreyConfig; ProcessFile reads it to divide the inner main-search
            // thread budget and avoid oversubscription.
            int effectiveParallelism = ResolveFileParallelism(config, nFiles, ctx.LogInfo);
            ctx.RunPlan.EffectiveFileParallelism = effectiveParallelism;

            var swAllFiles = Stopwatch.StartNew();
            if (nFiles == 1)
            {
                // Single file: process directly (no parallel overhead)
                string inputFile = config.InputFiles[0];
                string fileName = Path.GetFileNameWithoutExtension(inputFile);
                string validityKey = ValidityKey(ctx);
                var fileResult = ScoreOrLoadForFile(
                    inputFile, fileName, 0, 1,
                    fullLibrary, config, parquetFooterMetadata,
                    perFileCalibrations, perFileIsolationMz, validityKey, ctx);
                if (fileResult != null)
                    scoredFileNames.Add(fileName);
                // Single-file scoring memory boundary. The pre-GC line's working_set
                // peak is the in-scoring high-water mark (the ~tens-of-GB envelope one
                // file's Stage 1-4 needs); the forced-GC line is the PERSISTENT set that
                // survives -- the two together separate transient scoring buffers from
                // genuinely retained structure ("why does one file need so much, and
                // what is actually held"). When a dotMemory session is attached
                // (Profile-Osprey.ps1 -MemoryProfile) the forced-GC probe also captures a
                // retention snapshot here. Zero cost when OSPREY_LOG_MEMORY is unset; the
                // multi-file batch never takes this single-file branch.
                ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"single file scored (pre-GC)");
                ProfilerHooks.LogManagedHeapAfterGcIfEnabled(ctx.LogInfo, @"perfile-scored-live",
                    string.Format(@"(post-GC, after scoring {0})", fileName));
            }
            else if (effectiveParallelism == 1)
            {
                // Strictly sequential: one file at a time (the default, or an
                // explicit --parallel-files 1 / OSPREY_MAX_PARALLEL_FILES=1).
                // Matches the memory envelope of the single-file path while
                // still sharing the library load -- the safe choice for 3-file
                // Astral runs that would OOM in parallel.
                string validityKey = ValidityKey(ctx);
                for (int fileIdx = 0; fileIdx < config.InputFiles.Count; fileIdx++)
                {
                    string inputFile = config.InputFiles[fileIdx];
                    string fileName = Path.GetFileNameWithoutExtension(inputFile);
                    var fileResult = ScoreOrLoadForFile(
                        inputFile, fileName, fileIdx, config.InputFiles.Count,
                        fullLibrary, config, parquetFooterMetadata,
                        perFileCalibrations, perFileIsolationMz, validityKey, ctx);
                    if (fileResult != null)
                        scoredFileNames.Add(fileName);
                    ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo,
                        string.Format(@"scored file {0}/{1}", fileIdx + 1, config.InputFiles.Count));
                }
            }
            else
            {
                // Multiple files in parallel, bounded by the resolved
                // concurrent-file count (see ResolveFileParallelism).
                var parallelOpts = new ParallelOptions
                {
                    MaxDegreeOfParallelism = effectiveParallelism
                };
                string validityKey = ValidityKey(ctx);
                var fileResults = new ConcurrentDictionary<int, string>();
                // Legend mapping each aggregate-line slot to its input file, printed once
                // before the concurrent "[i] p%" line starts -- mirrors Skyline's numbered
                // file list above its multi-file import progress, so a reader can tell which
                // file each [i] is. Uses the same [i] token as the aggregate line.
                ctx.LogInfo(string.Format(@"Scoring {0} files in parallel:", config.InputFiles.Count));
                for (int legendIdx = 0; legendIdx < config.InputFiles.Count; legendIdx++)
                    ctx.LogInfo(string.Format(@"  {0}. {1}", legendIdx + 1, config.InputFiles[legendIdx]));

                // Collapse the concurrent per-file progress onto a single throttled
                // "[i] p%" aggregate line, and buffer each file's narrative so its
                // block flushes contiguously on completion rather than interleaving
                // with the other files' lines. Each file is one BeginFile scope; the
                // PROCESS_FILE_SEGMENTS phases inside ProcessFile drive its percent.
                var multi = new MultiProgressReporter();
                Parallel.For(0, config.InputFiles.Count, parallelOpts, fileIdx =>
                {
                    string inputFile = config.InputFiles[fileIdx];
                    string fileName = Path.GetFileNameWithoutExtension(inputFile);
                    using (multi.BeginFile(fileIdx, PROCESS_FILE_SEGMENTS))
                    {
                        var fileResult = ScoreOrLoadForFile(
                            inputFile, fileName, fileIdx, config.InputFiles.Count,
                            fullLibrary, config, parquetFooterMetadata,
                            perFileCalibrations, perFileIsolationMz, validityKey, ctx);
                        if (fileResult != null)
                            fileResults[fileIdx] = fileName;
                    }
                });
                // Collect in original order
                for (int i = 0; i < config.InputFiles.Count; i++)
                {
                    if (fileResults.TryGetValue(i, out string scoredName))
                        scoredFileNames.Add(scoredName);
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

            // Now that every per-file scoring transient has been released,
            // rematerialize the cold FdrEntry stubs the join needs by reloading
            // them from each scored file's just-written .scores.parquet, in the
            // same order scoring produced (issue #4355). Only the scalar stub
            // fields are read back -- no PIN features / CWT / fragment arrays --
            // which is exactly the cold shape the straight-through path already
            // left here after ProcessFile spilled every file's full results and
            // nulled those arrays (FirstJoin streams features back per file). The
            // live per-file RTCalibration objects in perFileCalibrations are NOT
            // reloaded: they are not stored in .scores.parquet, so they must stay
            // the live objects harvested during scoring.
            // The lean path is valid only where FirstJoinTask actually consumes a
            // projection. It must mirror that task's dispatch exactly (FirstJoinTask.cs:
            // UseFdrProjection && Percolator && !needsResidentFirstPassPool): any other
            // combination -- a non-Percolator FdrMethod, OSPREY_FDR_PROJECTION=0, or the
            // resident-pool consumers (--model-diagnostics / FDRBench pass 1, which walk
            // the full pre-compaction FdrEntry pool) -- still needs the fat stubs here.
            bool needsResidentPool = NeedsResidentPool(ctx.Config);
            GuardResidentPool(ctx.Config, needsResidentPool);

            FdrProjectionSet projections = null;
            int totalScored = 0;

            if (needsResidentPool)
            {
                // Per-file progress: loading every file's fat FdrEntry stubs from parquet
                // ran ~15 min silent (~53 GB) at the 82-file join. Console-only, never
                // touches the stubs, so the loaded pool is byte-identical.
                using (var loadProgress = new ProgressReporter(
                    string.Format(@"Loading scored entries from {0} file(s)", scoredFileNames.Count),
                    scoredFileNames.Count))
                {
                    int loadDone = 0;
                    foreach (string fileName in scoredFileNames)
                    {
                        loadProgress.Report(++loadDone);
                        string parquetPath = perFileParquetPaths[fileName];
                        // A scored file always has a parquet here; the sole exception is
                        // the OSPREY_EXIT_AFTER_CALIBRATION bench short-circuit, which
                        // returns an empty result without writing one. Skip that case so
                        // the run still stops cleanly at the "no scored entries" gate
                        // below rather than throwing on a missing file.
                        if (!File.Exists(parquetPath))
                            continue;
                        perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(
                            fileName, ParquetScoreCache.LoadFdrStubsFromParquet(parquetPath)));
                    }
                }
                foreach (var kvp in perFileEntries)
                    totalScored += kvp.Value.Count;
            }
            else
            {
                // Issue #4397: rematerializing every file's FdrEntry stubs here cost
                // ~53 GB on an 82-file Astral run (191M x ~280 B) purely so FirstJoin
                // could convert them into 32 B FdrProjection rows and drop them. Stream
                // the projection rows straight out of each .scores.parquet instead --
                // no FdrEntry is ever allocated. Peptide ids arrive in insertion order
                // and are remapped to the global Ordinal rank by Build(), so the result
                // is element-for-element identical to BuildFromEntries (pinned by
                // TestFdrProjectionBuilderMatchesBuildFromEntries). Counts-only (issue #4355
                // struct-shrink S3, Stage B): the projection carries per-file row counts only --
                // no 32 B rows -- because the 1st-pass streaming score path re-reads every row's
                // identity + features from parquet, so the resident FdrProjection[] buffer that
                // grew O(files) is never allocated.
                var builder = new FdrProjectionSet.Builder(countsOnly: true);
                // Per-file progress: streaming 32 B projection rows from each parquet is
                // the lean path, but reading 82 files still ran minutes silent. Console-only,
                // never touches the streamed rows, so the projection is byte-identical.
                using (var streamProgress = new ProgressReporter(
                    string.Format(@"Streaming projection from {0} file(s)", scoredFileNames.Count),
                    scoredFileNames.Count))
                {
                    int streamDone = 0;
                    foreach (string fileName in scoredFileNames)
                    {
                        streamProgress.Report(++streamDone);
                        string parquetPath = perFileParquetPaths[fileName];
                        if (!File.Exists(parquetPath))
                            continue;
                        builder.BeginFile(fileName);
                        ParquetScoreCache.ReadFdrStubScalars(parquetPath,
                            (entryId, charge, isDecoy, coelutionSum, modseq) =>
                                builder.AddRow(entryId, charge, isDecoy, coelutionSum, modseq));
                        builder.EndFile();
                        // Keep the per-file key and ordering so ScoredEntries consumers and
                        // the file-count guard below still see one entry per scored file;
                        // the stub lists themselves stay empty on this path.
                        perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(
                            fileName, new List<FdrEntry>()));
                    }
                }
                projections = builder.Build();
                totalScored = projections.TotalRows;
            }

            ctx.LogInfo(string.Empty);
            ctx.LogInfo(string.Format(
                @"Coelution analysis complete. {0} total scored entries across {1} files",
                totalScored, nFiles));

            return FinalizeAndCheck(ctx, perFileEntries, perFileCalibrations,
                perFileIsolationMz, perFileParquetPaths, nFiles, totalScored, projections);
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
            var config = ctx.Config;

            // Stage 1: library + decoys (needed by Stage 5+ even though the
            // per-file scores are loaded rather than computed).
            if (!LoadLibraryAndDecoys(config, out _, ctx))
                return false;

            var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>();
            var perFileCalibrations = new ConcurrentDictionary<string, RTCalibration>();
            // Rehydrated from calibration.json's isolation_scheme by
            // LoadJoinOnlyScores (the merge node has no mzML to extract from).
            var perFileIsolationMz = new ConcurrentDictionary<string, IReadOnlyList<(double Lo, double Hi)>>();
            // fileName -> .scores.parquet path; LoadJoinOnlyScores already
            // knows each input parquet path and fills this in.
            var perFileParquetPaths = new Dictionary<string, string>();

            int nFiles = config.InputScores.Count;

            // InputFiles is synthesized from the --input-scores parquet stems
            // once at pipeline entry (AnalysisPipeline.Run), so downstream code
            // (Stage 6 rescore's fileNameToIdx in particular) already has the
            // synthetic input paths by the time this load runs.

            // Mirror Run's EffectiveFileParallelism bookkeeping via the shared
            // resolver (unused by the disk-load path, which never calls
            // ProcessFile, but kept so the RunPlan reflects the same per-run
            // state either way). No log callback: this path does not parallelize,
            // so it should not emit a file-parallelism decision line.
            ctx.RunPlan.EffectiveFileParallelism = ResolveFileParallelism(config, nFiles, null);

            // Compute the reconciled-2nd-pass-bundle predicate ONCE here and thread it to
            // both the loader (lean/fat choice) and the hydrator, so a sidecar appearing
            // between two separate disk reads cannot make them disagree (lean empty stubs
            // + a firing bundle hydrator). Static on a real merge node; belt-and-suspenders.
            bool hasReconSidecars = AllHaveReconSidecars(config);
            var swAllFiles = Stopwatch.StartNew();
            var projections = LoadJoinOnlyScores(config, perFileEntries, perFileParquetPaths,
                perFileCalibrations, perFileIsolationMz, hasReconSidecars, ctx);
            swAllFiles.Stop();
            ctx.LogInfo(string.Format(@"[TIMING] All files processed: {0:F1}s",
                swAllFiles.Elapsed.TotalSeconds));

            int totalScored;
            if (projections != null)
            {
                totalScored = projections.TotalRows;
            }
            else
            {
                totalScored = 0;
                foreach (var kvp in perFileEntries)
                    totalScored += kvp.Value.Count;
            }

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
            if (!HydrateRescoreBundleIfPresent(config, perFileEntries, hasReconSidecars, ctx))
                return false;

            return FinalizeAndCheck(ctx, perFileEntries, perFileCalibrations,
                perFileIsolationMz, perFileParquetPaths, nFiles, totalScored, projections);
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
            var config = ctx.Config;

            // Stage 1: library + decoys (needed by Stage 5+ even though the
            // per-file scores are loaded rather than computed).
            if (!LoadLibraryAndDecoys(config, out _, ctx))
                return false;

            var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>();
            var perFileCalibrations = new ConcurrentDictionary<string, RTCalibration>();
            // Rehydrated per file from calibration.json's isolation_scheme by
            // TryLoadStubsAndCalibration (this pure-load path never re-extracts
            // isolation windows from mzML).
            var perFileIsolationMz = new ConcurrentDictionary<string, IReadOnlyList<(double Lo, double Hi)>>();
            var perFileParquetPaths = new Dictionary<string, string>();

            int nFiles = config.InputFiles?.Count ?? 0;

            // Mirror Run's EffectiveFileParallelism bookkeeping via the shared
            // resolver (unused by the disk-load path, which never calls
            // ProcessFile, but kept so the RunPlan reflects the same per-run
            // state either way). No log callback: this path does not parallelize,
            // so it should not emit a file-parallelism decision line.
            ctx.RunPlan.EffectiveFileParallelism = ResolveFileParallelism(config, nFiles, null);

            // Lean on resume too (#4400): a pure straight-through resume (all files
            // skipped) used to rematerialize the full fat FdrEntry stub buffer + PIN
            // features here -- ~53 GB across 82 files, the exact cost #4400 removed for
            // the Run path -- because this path had no lean branch. Mirror Run: stream
            // 32 B FdrProjection rows from each parquet unless an opt-in output genuinely
            // needs the resident pool. FirstJoin consumes the projection set identically
            // whether Run or this path produced it.
            //
            // --model-diagnostics forces the fat pool ONLY for a FULL resume, where FirstJoin
            // ALSO skips the first-pass score pass (every 1st-pass sidecar already on disk) and
            // emits the report via the batch ModelDiagnosticsReport.Write, which reads the
            // RESIDENT per-file entries. On a Stage-1-4 (-LinkFrom) resume the 1st-pass sidecars
            // are absent, so FirstPassFDR RE-RUNS and streams the report off its score pass
            // (ModelDiagnosticsData.Accumulator) exactly like a compute run -- no resident pool
            // needed. Probing the sidecars keeps the lean counts-only path for the common
            // -LinkFrom A/B resume (whose forced fat pool OOM'd an 82-file mdiag run) while
            // preserving the batch-write path's resident entries for the full-resume re-report,
            // so the fat pool is never on the row-count scaling path. The full elimination (stream
            // the batch report from the sidecar+parquet too) is a documented follow-up.
            // See TODO-20260720_osprey_pass2_per_run_qvalue.
            bool needsResidentPool = NeedsResidentPool(config) ||
                                     (config.ModelDiagnostics && FirstPassSidecarsPresent(config));
            GuardResidentPool(config, needsResidentPool);
            FdrProjectionSet projections = null;

            var swAllFiles = Stopwatch.StartNew();
            if (config.InputFiles != null)
            {
                // Counts-only (issue #4355 struct-shrink S3, Stage B): the resume lean path builds
                // only per-file row counts; the 1st-pass streaming score path re-reads identity +
                // features from parquet, so no resident FdrProjection[] buffer is allocated.
                var builder = needsResidentPool ? null : new FdrProjectionSet.Builder(countsOnly: true);
                // Per-file progress so this all-files load is not a silent multi-minute
                // stall on a large resume (the phase that looked hung on the 82-file run).
                using (var loadProgress = new ProgressReporter(@"Loading scored entries", config.InputFiles.Count))
                {
                    int fileIdx = 0;
                    // Sequential in InputFiles order to match Run's "collect in original
                    // order" -- downstream FirstJoin iterates perFileEntries.
                    foreach (string inputFile in config.InputFiles)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(inputFile);
                        string scoresPath = ParquetScoreCache.GetScoresPath(inputFile);
                        if (needsResidentPool)
                        {
                            // Fat path: an opt-in output reads every entry's resident
                            // features. Strict load: CanRehydrate already certified these
                            // outputs valid, so a null is a genuine fault, not a "fall back
                            // to rescore" case. Fail loudly; Rehydrate must not compute.
                            var stubs = TryLoadStubsAndCalibration(scoresPath, fileName, perFileCalibrations, perFileIsolationMz, ctx, resumeStrict: true);
                            if (stubs == null)
                            {
                                ctx.ExitCode = 1;
                                return false;
                            }
                            perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, stubs));
                        }
                        else
                        {
                            // Lean path: calibration + isolation still load (cheap); the
                            // scores stream straight into the projection, no fat stub or
                            // feature vector allocated. Byte-identical to the fat path
                            // (TestFdrProjectionBuilderMatchesBuildFromEntries + mode2).
                            // Fail-fast corruption guard: the fat path threw on
                            // features.Count != stubs.Count; streaming scalars never loads
                            // features, so restore that check up front via a footer-only
                            // probe (no feature memory). A foreign/truncated parquet missing
                            // the feature schema stops the run here rather than surfacing at
                            // a murkier point downstream. NOT applied to Run's fresh-compute
                            // lean path (#4400), whose parquets this same run just wrote.
                            if (!ParquetScoreCache.HasPinFeatureColumns(scoresPath))
                            {
                                ctx.LogError(string.Format(
                                    @"  Resume rehydrate: {0} is missing the PIN feature columns -- it is not a valid Osprey scores parquet. Delete it and re-run so it is regenerated.",
                                    scoresPath));
                                ctx.ExitCode = 1;
                                return false;
                            }
                            LoadCalibrationAndIsolation(scoresPath, fileName, perFileCalibrations, perFileIsolationMz, ctx);
                            try
                            {
                                builder.BeginFile(fileName);
                                ParquetScoreCache.ReadFdrStubScalars(scoresPath,
                                    (entryId, charge, isDecoy, coelutionSum, modseq) =>
                                        builder.AddRow(entryId, charge, isDecoy, coelutionSum, modseq));
                                builder.EndFile();
                            }
                            catch (Exception ex)
                            {
                                ctx.LogError(string.Format(
                                    @"  Resume rehydrate: failed to load valid-on-disk scores from {0}: {1}",
                                    scoresPath, ex.Message));
                                ctx.ExitCode = 1;
                                return false;
                            }
                            // Empty stub list on the lean path (mirrors Run), so the file-
                            // count guard + ScoredEntries consumers still see one entry per
                            // scored file.
                            perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, new List<FdrEntry>()));
                        }
                        perFileParquetPaths[fileName] = scoresPath;
                        loadProgress.Report(++fileIdx);
                    }
                }
                if (builder != null)
                    projections = builder.Build();
            }
            swAllFiles.Stop();
            ctx.LogInfo(string.Format(@"[TIMING] All files processed: {0:F1}s",
                swAllFiles.Elapsed.TotalSeconds));

            int totalScored;
            if (projections != null)
            {
                totalScored = projections.TotalRows;
            }
            else
            {
                totalScored = 0;
                foreach (var kvp in perFileEntries)
                    totalScored += kvp.Value.Count;
            }

            ctx.LogInfo(string.Empty);
            ctx.LogInfo(string.Format(
                @"Coelution analysis complete. {0} total scored entries across {1} files",
                totalScored, nFiles));

            return FinalizeAndCheck(ctx, perFileEntries, perFileCalibrations,
                perFileIsolationMz, perFileParquetPaths, nFiles, totalScored, projections);
        }

        /// <summary>
        /// Shared tail for <see cref="Run"/> and <see cref="Rehydrate"/>:
        /// surface the per-file outputs for downstream tasks (before any
        /// early-exit, so a partial-success caller still sees the populated
        /// collections), then apply the two success-but-stop boundaries --
        /// an empty score set (cannot run FDR) and <c>--task PerFileScoring</c> (Stage
        /// 1-4 only). Returns <c>true</c> to continue the pipeline, or
        /// <c>false</c> with <see cref="PipelineContext.ExitCode"/> = 0 at
        /// either boundary.
        /// </summary>
        private bool FinalizeAndCheck(PipelineContext ctx,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            ConcurrentDictionary<string, RTCalibration> perFileCalibrations,
            ConcurrentDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz,
            Dictionary<string, string> perFileParquetPaths,
            int nFiles, int totalScored, FdrProjectionSet projections = null)
        {
            _perFileEntries = perFileEntries;
            _perFileCalibrations = perFileCalibrations;
            _perFileIsolationMz = perFileIsolationMz;
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
            // CAL-view per-file diagnostics for --model-diagnostics. Harvested directly
            // onto the instance field by ProcessFile (compute path only), so it is
            // published from the field rather than a Finalize local. Empty on a normal
            // run and on the rehydrate/resume paths (no calibration matches to shape) --
            // FirstJoinTask reads it only under config.ModelDiagnostics and tolerates
            // empty. See TODO-20260712 for the HPC-split persistence caveat.
            ctx.Publish(new PerFileCalibrationDiagnostics(_perFileCalibrationDiagnostics, _calibrationMassUnit));
            ctx.Publish(new PerFileIsolationMz(_perFileIsolationMz));
            ctx.Publish(new PerFileParquetPaths(_perFileParquetPaths));
            ctx.Publish(new ScoredEntries(_perFileEntries));
            // Lean first-pass rows (issue #4397). Null on the rehydrate/merge paths (including
            // a --model-diagnostics resume, which needs resident entries for the batch report)
            // and on FDRBench pass 1 / OSPREY_PASS2_QVALUE=transfer, which publish fat stubs
            // above; FirstJoinTask falls back to ScoredEntries whenever this is null.
            ctx.Publish(new FdrProjections(projections));
            ctx.Publish(new RescoreBundle(_rescoreInputs));

            if (perFileEntries.Count == 0 || totalScored == 0)
            {
                ctx.LogWarning(@"No scored entries found. Cannot perform FDR control.");
                ctx.ExitCode = 0;
                return false;
            }

            // --task PerFileScoring: stop here. Per-file `.scores.parquet` files are
            // now on disk; a separate `--task FirstPassFDR` invocation (typically
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
        /// Resolve the effective concurrent-file count for this invocation via the
        /// shared <see cref="FileParallelismResolver"/> -- the single owner of the
        /// precedence between <c>--parallel-files</c>, the
        /// <c>OSPREY_MAX_PARALLEL_FILES</c> back-compat cap, free RAM, and the core
        /// count. The memory probe and per-file footprint estimate are evaluated
        /// lazily (auto mode only), so the common sequential / explicit paths do no
        /// I/O. <paramref name="log"/> is null on the disk-load bookkeeping paths
        /// that never actually parallelize, so they compute the same number without
        /// emitting a misleading decision line.
        /// </summary>
        private static int ResolveFileParallelism(OspreyConfig config, int nFiles, Action<string> log)
        {
            return FileParallelismResolver.Resolve(
                config.FileParallelism, nFiles, OspreyEnvironment.MaxParallelFiles,
                Environment.ProcessorCount,
                SystemMemory.AvailablePhysicalBytes,
                () => FileParallelismResolver.EstimatePerFileBytes(config.InputFiles),
                log);
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
        private bool LoadLibraryAndDecoys(OspreyConfig config, out List<LibraryEntry> fullLibrary, PipelineContext ctx)
        {
            fullLibrary = null;

            // A StopAfterStage5 worker (--task FirstPassFDR) reads only the six
            // identity scalars per library entry (the FDR stages never touch
            // fragments), so skip retaining the fragment peaks -- ~3.2 GB at
            // SEA-AD scale of otherwise-resident dead weight. Gate strictly on
            // StopAfterStage5: a straight-through run (or any run that later
            // writes the .blib) still needs the fragments. Decoy generation and
            // the fragment diagnostic below are told about the omission so they
            // stay byte-identical (see LibraryLoadOptions / DecoyGenerator).
            bool omitFragments = config.StopAfterStage5;
            var loadOptions = new LibraryLoadOptions { OmitFragments = omitFragments };

            var swLibrary = Stopwatch.StartNew();
            var library = LibraryLoader.Load(config, loadOptions, ctx.LogInfo, ctx.LogWarning);
            if (library == null || library.Count == 0)
            {
                ctx.LogError(@"Library is empty after loading");
                ctx.ExitCode = 1;
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
                MarkSuppliedDecoys(library, config, ctx);

            int nLibraryTargets = 0;
            foreach (var entry in library)
            {
                if (!entry.IsDecoy)
                    nLibraryTargets++;
            }
            double libLoadSec = swLibrary.Elapsed.TotalSeconds;
            ctx.LogInfo(string.Format(@"[COUNT] Library targets loaded: {0}", nLibraryTargets));

            List<LibraryEntry> decoys;
            if (config.ExpectReconciledInput)
            {
                // --task SecondPassFDR: decoy LibraryEntries are unused
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
                // GenerateAllWithCollisionDetection interns the freshly-minted
                // decoy strings ("DECOY_"+accession / modified sequence) through
                // its own pool and logs the collapse summary; no post-pass
                // interning is needed here.
                decoys = DecoyGenerator.GenerateAllWithCollisionDetection(
                    library, config, ctx.LogInfo, omitFragments, out List<LibraryEntry> validTargets);
                library = validTargets;
            }
            else
            {
                decoys = new List<LibraryEntry>();
                if (!TryPairSuppliedDecoys(library, config, nLibraryTargets, ctx))
                    return false;
            }
            swLibrary.Stop();
            double totalSec = swLibrary.Elapsed.TotalSeconds;
            ctx.LogInfo(string.Format(@"[TIMING] Library loading + decoys: {0:F1}s (load: {1:F1}s, decoys: {2:F1}s)",
                totalSec, libLoadSec, totalSec - libLoadSec));

            ctx.LogInfo(string.Format(@"[COUNT] Library decoys generated: {0}", decoys.Count));

            fullLibrary = new List<LibraryEntry>(library.Count + decoys.Count);
            fullLibrary.AddRange(library);
            fullLibrary.AddRange(decoys);

            ctx.LogInfo(string.Format(@"Full library: {0} entries ({1} targets + {2} decoys)",
                fullLibrary.Count, library.Count, decoys.Count));
            ctx.LogInfo(string.Format(@"[COUNT] Full library: {0} ({1} targets + {2} decoys)",
                fullLibrary.Count, library.Count, decoys.Count));

            // Count entries with few fragments (diagnostic for entry count
            // parity). Skipped when fragments were omitted: the entries carry no
            // fragment arrays by design, so every count would read 0 and the line
            // would misreport the whole library as sub-3-fragment.
            if (!omitFragments)
            {
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
                    ctx.LogInfo(string.Format(@"[COUNT] Entries with <3 fragments: {0} (0={1}, 1={2}, 2={3})",
                        nZeroFrag + nOneFrag + nTwoFrag, nZeroFrag, nOneFrag, nTwoFrag));
            }

            // Build library lookup by ID for fast access
            var libraryById = new Dictionary<uint, LibraryEntry>(fullLibrary.Count);
            foreach (var entry in fullLibrary)
                libraryById[entry.Id] = entry;

            _fullLibrary = fullLibrary;
            _libraryById = libraryById;

            // Diagnostic: the true resident-library managed heap. The working set
            // at this point still holds the one-time TSV/cache read buffers and freed
            // load garbage, so the settled managed heap is the clean resident number.
            // Collect/WaitForPendingFinalizers/Collect settles finalizable objects,
            // then GetTotalMemory(false) reads the result WITHOUT forcing a further
            // collection. Zero-cost when OSPREY_LOG_MEMORY is unset.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(@"OSPREY_LOG_MEMORY")))
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long managedBytes = GC.GetTotalMemory(false);
                ctx.LogInfo(string.Format(CultureInfo.InvariantCulture,
                    @"[MEM library-resident] managed_heap={0:F2} GB ({1} entries)",
                    managedBytes / (1024.0 * 1024.0 * 1024.0), fullLibrary.Count));
            }

            return true;
        }

        /// <summary>
        /// When the library supplies its own decoys (DIA-NN / EncyclopeDIA
        /// output with rev_ / DECOY_ prefixes), mark them in place by Decoy
        /// column / protein-accession prefix. DecoyMethod.FromLibrary is treated
        /// as a synonym for DecoysInLibrary -- historically it silently fell
        /// through to Reverse generation, the bug behind v26.5.3's library-decoy
        /// mode being effectively unusable. Marking runs BEFORE the target count
        /// is taken so the count reflects post-marking state and matches Rust
        /// pipeline.rs.
        /// </summary>
        private void MarkSuppliedDecoys(List<LibraryEntry> library, OspreyConfig config, PipelineContext ctx)
        {
            LibraryDecoyMarker.ApplyLibraryDecoyMarking(
                library, config.DecoyPrefixes, out var markingStats);
            ctx.LogInfo(string.Format(
                @"Library-decoy mode: matched prefixes {0}",
                FormatPrefixList(config.DecoyPrefixes)));
            ctx.LogInfo(string.Format(
                @"[COUNT] Library-decoy mode: {0} flagged ({1} via Decoy column, {2} via protein-accession prefix)",
                markingStats.NMarked, markingStats.NViaColumn, markingStats.NViaPrefix));
        }

        /// <summary>
        /// Library-supplied-decoy path: confirm the library actually contains
        /// decoys, then pair each decoy with its target so their base_ids match
        /// -- required for SVM target-decoy competition, LDA calibration, and CV
        /// fold grouping. Hybrid: manifest first when provided (exact pairs from
        /// FDRBench), amino-acid composition fallback for the remainder. Returns
        /// false (with <see cref="PipelineContext.ExitCode"/> set) when there
        /// are no decoys at all, the pairing manifest is unreadable, or the
        /// paired fraction is below <c>config.DecoyPairMinFraction</c>.
        /// </summary>
        private bool TryPairSuppliedDecoys(
            List<LibraryEntry> library, OspreyConfig config, int nLibraryTargets, PipelineContext ctx)
        {
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
                ctx.LogError(string.Format(
                    @"decoys_in_library mode requested but no library entries match prefixes {0}. " +
                    @"Check that the library actually contains decoys with one of these prefixes on " +
                    @"a protein accession, or unset decoys_in_library so Osprey generates decoys.",
                    FormatPrefixList(config.DecoyPrefixes)));
                ctx.ExitCode = 1;
                return false;
            }

            // Hybrid pairing. Net result on real Carafe-generated entrapment
            // libraries: ~30% via manifest, ~70% via composition, >99% total.
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
                ctx.LogInfo(string.Format(
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
                    ctx.LogError(string.Format(
                        @"Failed to read decoy pairing manifest {0}: {1}",
                        config.DecoyPairingManifestPath, ex.Message));
                    ctx.ExitCode = 1;
                    return false;
                }
                var manifestStats = manifest.ApplyToLibrary(library, pairingState, ctx.LogInfo);
                pairingStats.NPairedViaManifest = manifestStats.NPaired;
                if (manifestStats.NProteinsReplaced > 0)
                {
                    ctx.LogInfo(string.Format(
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
                    ctx.LogInfo(string.Format(
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
                ctx.LogInfo(
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
            ctx.LogInfo(string.Format(
                @"Library-decoy pairing: paired {0}/{1} decoys ({2:F1}%); " +
                @"manifest={3}, composition={4}; {5} unpaired decoys, {6} unpaired targets",
                pairingStats.NPaired, pairingStats.NDecoys,
                pairingStats.PairedFraction * 100.0,
                pairingStats.NPairedViaManifest, pairingStats.NPairedViaComposition,
                pairingStats.NUnpairedDecoys, pairingStats.NUnpairedTargets));
            if (pairingStats.PairedFraction < config.DecoyPairMinFraction)
            {
                ctx.LogError(string.Format(
                    @"Library-decoy pairing failed: only {0:F1}% of decoys paired with a target " +
                    @"(threshold: {1:F0}%). FDR estimates would be unreliable without proper " +
                    @"target-decoy competition. Either supply a pairing manifest, ensure the " +
                    @"library uses matching protein accessions with one of `decoy_prefixes` " +
                    @"({2}), or unset `decoys_in_library` so Osprey generates its own decoys.",
                    pairingStats.PairedFraction * 100.0,
                    config.DecoyPairMinFraction * 100.0,
                    FormatPrefixList(config.DecoyPrefixes)));
                ctx.ExitCode = 1;
                return false;
            }
            return true;
        }

        /// <summary>
        /// --task FirstPassFDR: load per-file FdrEntry stubs + PIN features directly
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
        private FdrProjectionSet LoadJoinOnlyScores(
            OspreyConfig config,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            Dictionary<string, string> perFileParquetPaths,
            ConcurrentDictionary<string, RTCalibration> perFileCalibrations,
            ConcurrentDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz,
            bool hasReconSidecars,
            PipelineContext ctx)
        {
            // --task FirstPassFDR: load per-file FdrEntry stubs directly from
            // each .scores.parquet listed via --input-scores. Skips the
            // per-file Stage 2-4 scoring (Stage 1 library load already ran
            // in Run). Also loads a best-effort calibration JSON sibling
            // per file (the loop below) for Stage 6 reconciliation, like
            // the Rust impl.
            // Guard: hash check against current --library and search params.
            // Aborts with a clear, file-named error if the operator points
            // the merge node at parquets from a different scoring run.
            string validationError = ParquetScoreCache.ValidateScoresParquetGroup(
                config.InputScores, config, OspreyVersion.Current);
            if (validationError != null)
                throw new InvalidDataException(validationError);

            ctx.LogInfo(string.Format(
                @"--input-scores: loading {0} per-file score parquet(s)",
                config.InputScores.Count));
            // Lean on the HPC merge/join too (#4400): a large first-pass merge node
            // loading every worker's .scores.parquet used to rebuild the full fat
            // FdrEntry stubs + PIN features (~53 GB at 82 files) -- the same Stage-5
            // blowup the resume path had. Stream 32 B FdrProjection rows instead, unless
            // the merge needs the resident pool (an opt-in feature output) or is a
            // reconciled 2nd-pass bundle hydration (AllHaveReconSidecars -- FirstJoin
            // skips Percolator there and HydrateReconciliationOverlay reads the fat stubs).
            // Counts-only (issue #4355 struct-shrink S3, Stage B): the merge-node lean path builds
            // only per-file row counts; the 1st-pass streaming score path re-reads identity +
            // features from parquet, so the resident FdrProjection[] buffer is never allocated.
            var builder = (!NeedsResidentPool(config) && !hasReconSidecars)
                ? new FdrProjectionSet.Builder(countsOnly: true)
                : null;
            for (int fileIdx = 0; fileIdx < config.InputScores.Count; fileIdx++)
            {
                string parquetPath = config.InputScores[fileIdx];
                // Derive the bare input stem via the single shared suffix-strip
                // helper so a .scores-reconciled.parquet input maps to the same
                // fileName key as its .scores.parquet sibling (a naive trailing
                // ".scores" strip would leave the bogus key "<stem>.reconciled").
                string fileName = Path.GetFileNameWithoutExtension(
                    RescoreHydration.SyntheticInputFromParquet(parquetPath)) ?? string.Empty;
                ctx.LogInfo(string.Format(@"===== Loading file {0}/{1}: {2} (from {3}) =====",
                    fileIdx + 1, config.InputScores.Count, fileName, parquetPath));
                if (builder != null)
                {
                    // Lean: stream 32 B projection rows straight from the parquet; no
                    // fat FdrEntry stub or 21-float feature vector is ever allocated.
                    // Fail-fast corruption guard: the fat branch below throws on
                    // features.Count != stubs.Count; streaming scalars never loads
                    // features, so restore that check up front via a footer-only probe
                    // (no feature memory). A merge node pointed at a foreign/truncated
                    // parquet missing the feature schema stops here rather than surfacing
                    // downstream. The scores-group hash check above (ValidateScoresParquetGroup)
                    // catches a wrong-library parquet; this catches a same-library corrupt one.
                    if (!ParquetScoreCache.HasPinFeatureColumns(parquetPath))
                        throw new InvalidDataException(string.Format(
                            @"--input-scores: parquet {0} is missing the PIN feature columns -- it is not a valid Osprey scores parquet. Delete it and re-run so it is regenerated.",
                            parquetPath));
                    // Pre-size the per-file projection list to the parquet row count (footer
                    // NumRows, no column data) so the 32 B rows fill one right-sized backing
                    // array instead of the List's doubling growth -- byte-neutral, ~5.4 GB less
                    // resident at 82 files (issue #4355 Part B; the pre-size was measured on the
                    // memory branch). A 0/missing count just falls back to an un-hinted list.
                    long rowCountHint = ParquetScoreCache.ProbeCwtRowMetadata(parquetPath).RowCount;
                    builder.BeginFile(fileName, checked((int)rowCountHint));
                    ParquetScoreCache.ReadFdrStubScalars(parquetPath,
                        (entryId, charge, isDecoy, coelutionSum, modseq) =>
                            builder.AddRow(entryId, charge, isDecoy, coelutionSum, modseq));
                    builder.EndFile();
                    perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, new List<FdrEntry>()));
                }
                else
                {
                    // Fat: Stage 5+ (Percolator SVM) requires the 21 PIN features on each
                    // FdrEntry (or the reconciled-bundle overlay reads the stubs). Load
                    // them in lockstep with the stubs and bind by row index (rows stable).
                    var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(parquetPath);
                    var features = ParquetScoreCache.LoadPinFeaturesFromParquet(parquetPath);
                    if (features.Count != stubs.Count)
                    {
                        throw new InvalidDataException(string.Format(
                            @"--input-scores: parquet {0} has {1} stubs but {2} feature rows",
                            parquetPath, stubs.Count, features.Count));
                    }
                    for (int j = 0; j < stubs.Count; j++)
                        stubs[j].Features = features[j];
                    ctx.LogInfo(string.Format(@"  Loaded {0} FDR stubs + features", stubs.Count));
                    perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, stubs));
                }
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
                                if (ctx.Diagnostics?.DumpCalibration ?? false)
                                {
                                    ctx.Diagnostics?.WriteStage6CalibrationDump(
                                        fileName, mp.LibraryRts, mp.FittedRts);
                                }
                                var rtCal = RTCalibration.FromModelParams(
                                    mp.LibraryRts, mp.FittedRts, mp.AbsResiduals,
                                    calParams.RtCalibration.ResidualSD);
                                perFileCalibrations[fileName] = rtCal;
                            }

                            // Isolation-window coverage for the gap-fill m/z
                            // filter -- read independent of RT calibration from
                            // the isolation_scheme block, so a merge node with no
                            // mzML still gets per-file coverage. Mirrors Rust's
                            // isolation_intervals_from_cal (pipeline.rs).
                            var isoIntervals = IsolationIntervalsFromWindows(
                                calParams.Metadata?.IsolationScheme?.Windows);
                            if (isoIntervals != null)
                                perFileIsolationMz[fileName] = isoIntervals;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ctx.LogWarning(string.Format(@"  Failed to load calibration for {0}: {1}", fileName, ex.Message));
                }
            }
            if (ctx.Diagnostics?.CalibrationOnly ?? false)
                OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_CALIBRATION_ONLY");
            return builder?.Build();
        }

        /// <summary>
        /// Convert calibration.json isolation-scheme windows (<c>[center, width]</c>
        /// pairs) into the half-open <c>[Lo, Hi)</c> gap-fill m/z intervals
        /// (<c>center +/- width/2</c>) the Stage 6 filter consumes. Ports Rust's
        /// <c>isolation_intervals_from_cal</c> (osprey/src/pipeline.rs). Returns
        /// <c>null</c> when no windows are present so the caller records nothing
        /// for that file (an absent entry disables the filter there, as intended).
        /// </summary>
        private static IReadOnlyList<(double Lo, double Hi)> IsolationIntervalsFromWindows(double[][] windows)
        {
            if (windows == null || windows.Length == 0)
                return null;
            var intervals = new List<(double Lo, double Hi)>(windows.Length);
            foreach (var w in windows)
            {
                if (w == null || w.Length < 2)
                    continue;
                double half = w[1] / 2.0;
                intervals.Add((w[0] - half, w[0] + half));
            }
            return intervals.Count > 0 ? intervals : null;
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
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            bool hasReconSidecars,
            PipelineContext ctx)
        {
            if (hasReconSidecars)
            {
                try
                {
                    _rescoreInputs = RescoreHydration.HydrateReconciliationOverlay(
                        perFileEntries, config.InputScores);
                }
                catch (InvalidDataException ex)
                {
                    ctx.LogError(string.Format(
                        @"--input-scores hydration failed: {0}", ex.Message));
                    ctx.ExitCode = 1;
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
                ctx.LogInfo(string.Format(
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
        /// True when EVERY <c>--input-scores</c> parquet has both its first-pass
        /// <c>.fdr_scores.bin</c> and its <c>.reconciliation.json</c> sidecar -- the
        /// signature of a reconciled 2nd-pass merge whose <see cref="HydrateRescoreBundleIfPresent"/>
        /// overlays q-values onto the resident stubs (FirstJoin skips Percolator there).
        /// <see cref="LoadJoinOnlyScores"/> reads this to keep the fat pool on that path,
        /// since the lean projection would drop the stubs the overlay needs.
        /// </summary>
        private static bool AllHaveReconSidecars(OspreyConfig config)
        {
            foreach (var parquetPath in config.InputScores)
            {
                string syntheticInput = RescoreHydration.SyntheticInputFromParquet(parquetPath);
                if (!File.Exists(FdrScoresSidecar.Pass1Path(syntheticInput))
                    || !File.Exists(ReconciliationFile.PathForInput(syntheticInput)))
                    return false;
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
            ConcurrentDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz,
            string validityKey,
            PipelineContext ctx)
        {
            string scoresPath = ParquetScoreCache.GetScoresPath(inputFile);
            if (PerFileResumeDriver.IsCurrent(scoresPath, Name, validityKey))
            {
                var loaded = TryLoadStubsAndCalibration(scoresPath, fileName, perFileCalibrations, perFileIsolationMz, ctx);
                if (loaded != null)
                {
                    ctx.LogInfo(string.Format(
                        @"[file] {0}/{1} {2}: skipping (outputs valid)",
                        fileIdx + 1, totalFiles, fileName));
                    return loaded;
                }
                // load failed -- fall through and rescore the file.
            }

            ctx.LogInfo(string.Format(@"Scoring file {0}/{1}: {2}",
                fileIdx + 1, totalFiles, inputFile));
            // Clear stale sidecar so a mid-ProcessFile crash leaves no
            // false-positive sidecar on the next invocation.
            PerFileResumeDriver.ClearStale(scoresPath, Name);
            var fileResult = ProcessFile(inputFile, fileName, fullLibrary, config, parquetFooterMetadata, perFileCalibrations, perFileIsolationMz, ctx);
            if (fileResult != null)
            {
                PerFileResumeDriver.Stamp(scoresPath, Name, OspreyVersion.Current,
                    validityKey, new[] { inputFile }, ctx.LogWarning);
            }
            return fileResult;
        }

        /// <summary>
        /// Whether Stage 5 needs the resident fat-stub first-pass pool rather than the
        /// lean streamed <see cref="FdrProjection"/> set (#4400). True when an opt-in
        /// output reads every entry's in-memory features/scores (FDRBench pass 1),
        /// when the projection path is off (OSPREY_FDR_PROJECTION=0 / non-Percolator FDR),
        /// or on the reconciled-input worker join. OSPREY_PASS2_QVALUE=transfer no longer
        /// forces the resident pool -- the per-run-only redesign maps each adjusted peak
        /// through that file's own 1st-pass (score -> run q) sidecar table at pass 2, so
        /// transfer takes the SAME lean projection first-pass path as the default (see
        /// TODO-osprey_pass2_per_run_only_qvalue). Shared by <see cref="Run"/> and
        /// <see cref="RehydrateFromOwnOutputs"/>
        /// so the compute and resume paths make the identical lean/fat choice -- otherwise a
        /// pure resume rebuilds the ~53 GB fat buffer #4400 dropped for straight-through.
        ///
        /// --model-diagnostics is NOT here: it streams its pass-1 report off the projection
        /// path via a ModelDiagnosticsData.Accumulator fed by the score-pass sink (mirrors the
        /// FirstJoinTask join-gate that already dropped it), folding each pre-compaction row into
        /// the reduced report rather than holding the whole-run pool resident -- which peaked
        /// ~100 GB on an 82-file mdiag run. The reductions are order-independent, so the streamed
        /// report is byte-identical to the resident build.
        /// </summary>
        private static bool NeedsResidentPool(OspreyConfig config)
        {
            return config.ExpectReconciledInput ||
                   !OspreyEnvironment.UseFdrProjection ||
                   config.FdrMethod != FdrMethod.Percolator ||
                   (!string.IsNullOrEmpty(config.OutputFdrBench) && config.FdrBenchPass == 1);
        }

        /// <summary>
        /// True when every input file already has its <c>.1st-pass.fdr_scores.bin</c> sidecar on
        /// disk, so FirstJoin will REHYDRATE first-pass FDR (skip its score pass) rather than
        /// recompute. Under <c>--model-diagnostics</c> that skip routes the report to the batch
        /// <c>ModelDiagnosticsReport.Write</c>, which reads the RESIDENT per-file entries -- the
        /// one case a resume still needs the fat pool. A Stage-1-4 (<c>-LinkFrom</c>) resume links
        /// only the Stage-4 <c>.scores.parquet</c>, so the 1st-pass sidecars are ABSENT, FirstPassFDR
        /// re-runs, and its score pass streams the report -- no resident pool needed. Absent sidecars
        /// therefore mean "stay lean" (correct); present sidecars mean "keep fat" (conservative,
        /// matching the batch-write path). Mirrors <see cref="FdrScoresSidecar.Pass1Path"/> as used by
        /// <c>FirstJoinTask</c>'s rehydrate-output enumeration.
        /// </summary>
        private static bool FirstPassSidecarsPresent(OspreyConfig config)
        {
            if (config.InputFiles == null || config.InputFiles.Count == 0)
                return false;
            foreach (var inputFile in config.InputFiles)
            {
                if (!File.Exists(FdrScoresSidecar.Pass1Path(inputFile)))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Fail fast when a run would build the RESIDENT first-pass pool -- an O(files) memory
        /// path (the fat <see cref="FdrEntry"/> stub buffer, and the <c>FirstJoin.Rehydrate</c>
        /// pre-compaction load it feeds) that does not scale to large file counts. Unless the
        /// operator explicitly accepted unbounded memory (<c>OSPREY_ALLOW_UNBOUNDED_MEMORY</c>, or
        /// the <c>OSPREY_FDR_PROJECTION=0</c> A/B-oracle switch which is itself an explicit resident
        /// opt-in), throw with the trigger named so the failure is actionable rather than an opaque
        /// OOM at scale. Triggers: the HPC reconciled-input merge, <c>--fdrbench-pass 1</c>, a
        /// non-Percolator FdrMethod, and <c>--model-diagnostics</c> on a full resume. Streaming the
        /// remaining resident paths is tracked in
        /// <c>TODO-osprey_stage6_rescored_buffer_streaming.md</c>.
        /// </summary>
        private static void GuardResidentPool(OspreyConfig config, bool needsResidentPool)
        {
            string error = ResidentPoolGuardError(config, needsResidentPool,
                OspreyEnvironment.AllowUnboundedMemory, OspreyEnvironment.UseFdrProjection);
            if (error != null)
                throw new InvalidOperationException(error);
        }

        /// <summary>
        /// Pure core of <see cref="GuardResidentPool"/> (env statics passed in so it is unit
        /// testable): returns the actionable error message when the run would take the resident
        /// first-pass pool without an explicit unbounded-memory opt-in, or <c>null</c> when the
        /// pool is not needed or the operator opted in. <paramref name="useFdrProjection"/> == false
        /// is the <c>OSPREY_FDR_PROJECTION=0</c> A/B-oracle switch, itself an explicit resident opt-in.
        /// </summary>
        internal static string ResidentPoolGuardError(
            OspreyConfig config, bool needsResidentPool, bool allowUnbounded, bool useFdrProjection)
        {
            if (!needsResidentPool || allowUnbounded || !useFdrProjection)
                return null;
            string trigger =
                config.ExpectReconciledInput ? @"the reconciled-input merge (HPC --task SecondPassFDR)"
                : config.FdrMethod != FdrMethod.Percolator ? string.Format(@"{0} FDR (non-Percolator)", config.FdrMethod)
                : (!string.IsNullOrEmpty(config.OutputFdrBench) && config.FdrBenchPass == 1) ? @"--fdrbench-pass 1"
                : config.ModelDiagnostics ? @"--model-diagnostics on a full resume"
                : @"this configuration";
            return string.Format(
                @"{0} requires the RESIDENT first-pass pool, which holds every entry in memory and " +
                @"grows O(files) -- it does not scale to large file counts and can exhaust memory. " +
                @"Set OSPREY_ALLOW_UNBOUNDED_MEMORY=1 to accept unbounded memory and proceed " +
                @"(intended for testing / small runs); otherwise this path is unavailable.", trigger);
        }

        /// <summary>
        /// Load a resumed file's RT calibration + gap-fill isolation m/z coverage from its
        /// sibling <c>calibration.json</c> into the shared maps, so a cached-calibration
        /// file gets the same filter input a fresh ProcessFile computes (mirrors Rust's
        /// isolation_intervals_from_cal). Independent of the stub/projection load, so both
        /// the fat (<see cref="TryLoadStubsAndCalibration"/>) and lean resume paths reuse it.
        /// A failure is logged and swallowed -- calibration is an enrichment, not a gate.
        /// </summary>
        private static void LoadCalibrationAndIsolation(
            string scoresPath,
            string fileName,
            ConcurrentDictionary<string, RTCalibration> perFileCalibrations,
            ConcurrentDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz,
            PipelineContext ctx)
        {
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

                        // Rehydrate the gap-fill m/z coverage from the isolation_scheme
                        // block too (independent of RT cal), so a resumed / cached-
                        // calibration file gets the same filter input a fresh ProcessFile
                        // computes. Mirrors Rust's isolation_intervals_from_cal (pipeline.rs).
                        var isoIntervals = IsolationIntervalsFromWindows(
                            calParams.Metadata?.IsolationScheme?.Windows);
                        if (isoIntervals != null)
                            perFileIsolationMz[fileName] = isoIntervals;
                    }
                }
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format(@"  Failed to load calibration for {0}: {1}", fileName, ex.Message));
            }
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
        /// <c>--task FirstPassFDR</c> branch above.
        /// </summary>
        private static List<FdrEntry> TryLoadStubsAndCalibration(
            string scoresPath,
            string fileName,
            ConcurrentDictionary<string, RTCalibration> perFileCalibrations,
            ConcurrentDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz,
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

            LoadCalibrationAndIsolation(scoresPath, fileName, perFileCalibrations, perFileIsolationMz, ctx);
            return stubs;
        }

        /// <summary>
        /// Process a single mzML file: load spectra, calibrate RT, score coelution.
        /// </summary>
        private List<FdrEntry> ProcessFile(
            string inputFile, string fileName,
            List<LibraryEntry> fullLibrary, OspreyConfig config,
            Dictionary<string, string> parquetFooterMetadata,
            ConcurrentDictionary<string, RTCalibration> perFileCalibrationsOut,
            ConcurrentDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMzOut,
            PipelineContext ctx)
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
            if (ctx.RunPlan.EffectiveFileParallelism > 1)
            {
                int perFileThreads = Math.Max(1, config.NThreads / ctx.RunPlan.EffectiveFileParallelism);
                ctx.LogInfo(string.Format(
                    "[BENCH] Per-file thread cap: {0} ({1} total / {2} files in parallel)",
                    perFileThreads, config.NThreads, ctx.RunPlan.EffectiveFileParallelism));
                config.NThreads = perFileThreads;
            }

            var context = new ScoringContext(config, fileName);

            // Load the per-file spectra as a STREAMING index over the .spectra.bin cache,
            // never materializing the full ~6 GB MS2 List<Spectrum>: Stages 1-4 (calibration
            // + scoring) stream each isolation window on demand. Segment 1/4 (read): the
            // mzML/cache read reporter inside EnsureSpectraCache feeds this file's first
            // progress slice under --parallel-files.
            MultiProgressReporter.Current?.BeginSegment();
            var swParse = Stopwatch.StartNew();
            SpectraWindowIndex windowIndex = EnsureSpectraCache(
                inputFile, ctx.RunPlan.EffectiveFileParallelism > 1, out int unsortedCount, ctx);
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
                ctx.LogInfo(string.Format("[TIMING] mzML parsing: {0:F1}s ({1:F1} MB/s)",
                    parseSeconds, mbPerSec));
            }
            else
            {
                ctx.LogInfo(string.Format("[TIMING] mzML parsing: {0:F1}s", parseSeconds));
            }

            if (windowIndex == null || windowIndex.Ms2Count == 0)
            {
                ctx.LogWarning(string.Format("No spectra found in {0}", inputFile));
                return null;
            }

            // MS1 and the isolation windows come straight from the streaming index (copies of
            // its own small lists); the full MS2 list is never materialized here.
            var ms1Spectra = windowIndex.Ms1Spectra.ToList();
            var isolationWindows = windowIndex.IsolationWindows.ToList();
            ctx.LogInfo(string.Format(
                "Loaded {0} MS1 and {1} MS/MS spectra with {2} unique isolation windows{3}",
                ms1Spectra.Count, windowIndex.Ms2Count, isolationWindows.Count,
                unsortedCount > 0
                    ? string.Format(" ({0} had unsorted peaks, re-sorted; use --verbose for detail)", unsortedCount)
                    : string.Empty));
            ctx.LogInfo(string.Format("[COUNT] mzML spectra loaded [{0}]: {1} MS2 + {2} MS1",
                fileName, windowIndex.Ms2Count, ms1Spectra.Count));
            ctx.LogInfo(string.Format("[COUNT] Isolation windows [{0}]: {1}",
                fileName, isolationWindows.Count));

            // Resolve the per-file calibration (load a cached/Rust JSON or
            // compute via Calibrator) and persist the calibration JSON.
            // Segment 2/4 (calibrate): no inner reporter today, so this slice
            // carries the file forward as a step when scoring begins.
            MultiProgressReporter.Current?.BeginSegment();
            RTCalibration rtCalibration = ResolveCalibration(
                inputFile, fileName, fullLibrary, windowIndex, ms1Spectra, isolationWindows,
                context, config, out MzCalibrationResult ms2Cal, out MzCalibrationResult ms1Cal,
                out ModelDiagnosticsData.CalFileRow calDiagnostics, ctx);

            // Harvest the CAL-view per-file diagnostics for the --model-diagnostics
            // HTML report. Only non-null on the compute path under config.ModelDiagnostics;
            // FirstJoinTask assembles these into ModelDiagnosticsData.Cal. Keyed by file
            // name in input order, mirroring the per-file calibration harvest below. The
            // ConcurrentDictionary field tolerates the parallel per-file fan-out.
            if (calDiagnostics != null)
            {
                _perFileCalibrationDiagnostics[fileName] = calDiagnostics;
                // Record the per-run mass-error unit from the mass calibration (the
                // CalFileRow does not carry it -- it is a per-run scalar). ms1 is
                // authoritative; ms2 covers an uncalibrated ms1 that defaulted its unit.
                if (_calibrationMassUnit == null)
                    _calibrationMassUnit = !string.IsNullOrEmpty(ms1Cal?.Unit) ? ms1Cal.Unit : ms2Cal?.Unit;
            }

            // Calibration-phase memory boundary (companion to the perfile-scoring-peak
            // capture below). The [MEM] line's working_set peak is the calibration
            // high-water mark before scoring pushes it higher; its managed_heap is taken
            // WITHOUT a forced GC, so the transient per-window float[][] XCorr caches the
            // calibration scoring builds and releases per isolation window -- not yet
            // collected here -- are still counted, sizing the calibration peak.
            // The paired retention snapshot forces its own GC first (dotMemory), so it
            // captures the post-calibration FLOOR that scoring inherits (library + spectra)
            // rather than the transient cache. Both are no-ops off a profiling run
            // (OSPREY_LOG_MEMORY unset / no dotMemory attached), so the batch and the
            // regression golden are unaffected; the per-file fan-out reaches this per file.
            ProfilerHooks.LogMemoryStatsIfEnabled(ctx.LogInfo, @"post-calibration");
            ProfilerHooks.CaptureRetentionSnapshot(@"post-calibration");

            // Optional early exit after Stage 3 (calibration only, no main search).
            // Used for Stage 1-3 perf benchmarking and walking up to the main
            // search incrementally without paying the Stage 4 cost.
            if (OspreyEnvironment.ExitAfterCalibration)
            {
                ctx.LogInfo("[BENCH] OSPREY_EXIT_AFTER_CALIBRATION set - exiting after Stage 3 (calibration done)");
                return new List<FdrEntry>();
            }

            // Surface the per-file calibration to Stage 6 reconciliation
            // (multi-file runs only). Threaded calls share a
            // ConcurrentDictionary; null on single-file paths that don't
            // need cross-file consensus.
            if (perFileCalibrationsOut != null && rtCalibration != null)
                perFileCalibrationsOut[fileName] = rtCalibration;

            // Surface the per-file isolation-window m/z coverage alongside the
            // calibration so Stage 6 gap-fill can filter candidates to the m/z
            // ranges this file actually isolated (center +/- width/2). Matches
            // Rust pipeline.rs:4248-4255 / reconciliation.rs; inert for a single
            // sDIA window covering the whole range.
            if (perFileIsolationMzOut != null && isolationWindows.Count > 0)
                perFileIsolationMzOut[fileName] = isolationWindows
                    .Select(w => (w.Center - w.Width / 2.0, w.Center + w.Width / 2.0))
                    .ToList();

            // Run coelution scoring, then drop double-counted features and
            // deduplicate target/decoy pairs per base_id. Extracted to
            // ScoreAndDeduplicate so ProcessFile reads as a sequencer.
            // Segment 3/4 (score): the inner isolation-window Parallel.For reporter
            // in ScoringPipeline feeds this slice -- the long phase that shows the
            // bulk of a file's motion on the aggregate line.
            MultiProgressReporter.Current?.BeginSegment();

            // Scoring streams each isolation window's peaks from the same index built at load
            // (Stage 3 calibration used it too), so the full MS2 list is never resident.
            IWindowSpectraProvider spectraProvider =
                new StreamingWindowSpectraProvider(windowIndex, ms2Cal);

            var scoredEntries = ScoreAndDeduplicate(
                fullLibrary, spectraProvider, ms1Spectra, isolationWindows,
                rtCalibration, ms2Cal, ms1Cal, context, config, fileName, ctx);

            // Retention snapshot at the in-scoring PEAK -- this is the moment the memory
            // work targets. Here scoredEntries still hold every heavy per-entry array
            // (Features / CwtCandidates / FragmentMzs / FragmentIntensities /
            // ReferenceXic*) and the library is still resident, but the resident MS2 is
            // gone on the streaming path (dropped above), which is the reduction this
            // snapshot exists to confirm. Those per-entry arrays are dropped a few lines
            // below (the #4355 write-then-null), so the later
            // perfile-scored-live probe captures only the post-release floor and CANNOT
            // show them -- a forced-GC snapshot never captures unreferenced objects.
            // Deliberately a direct SnapshotReady-gated capture (NOT via a forced-GC [MEM]
            // boundary): a no-op on the batch (no profiler attached), fires only under
            // Profile-Osprey.ps1 -MemoryProfile, and dotMemory forces its own GC so the
            // captured live set is the true retained peak (arrays are live here, so kept).
            ProfilerHooks.CaptureRetentionSnapshot(@"perfile-scoring-peak");

            // Optional: write per-entry feature TSV for comparison against Rust's PIN output
            if (config.WritePin)
            {
                WriteFeatureDump(inputFile, fileName, scoredEntries, ctx);
            }

            // Persist the full FdrEntry results (with features) to
            // {stem}.scores.parquet so (a) Stage 6 reconciliation can lazy-load
            // CWT candidates per file, and (b) a subsequent --task FirstPassFDR
            // invocation can pick them up without re-running Stages 1-4.
            // Same path convention as Rust (`scores_path_for_input`).
            // Snappy-compressed; cross-impl ZSTD/Snappy compatibility tracked
            // as a Phase 4 follow-up. The metadata dictionary is precomputed
            // in Run() against the original (un-mutated) outer config — see
            // Run() for why. Skipped only in --task FirstPassFDR mode (no Stages 1-4
            // ran here, so there is nothing fresh to persist).
            // Segment 4/4 (write): the parquet build/write reporters in
            // ParquetScoreCache feed this final slice.
            MultiProgressReporter.Current?.BeginSegment();
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
                ctx.LogInfo(string.Format(
                    "Wrote {0} scored entries to {1} ({2:F1}s)",
                    scoredEntries.Count, parquetPath, swParquet.Elapsed.TotalSeconds));

                // Phase 1 (issue #4355): the heavy per-entry arrays are now persisted in
                // the parquet above and are reloadable by ParquetIndex, so drop them from
                // the retained buffer to bound memory -- all N files' entries are held at
                // once for the join, and these arrays dominate. Features is reloaded before
                // first-pass Percolator (FirstJoinTask); CWT / fragments / ref-XIC are
                // reloaded from parquet in Stage 6 / 7. This brings the cold buffer to the
                // same stub shape LoadFdrStubsFromParquet produces (see the FdrEntry field
                // docs, which already document these as null on parquet-loaded stubs).
                foreach (var entry in scoredEntries)
                {
                    entry.Features = null;
                    entry.CwtCandidates = null;
                    entry.FragmentMzs = null;
                    entry.FragmentIntensities = null;
                    entry.ReferenceXicRts = null;
                    entry.ReferenceXicIntensities = null;
                }
            }

            return scoredEntries;
        }

        /// <summary>
        /// Run coelution scoring across the isolation windows, then apply the two
        /// dedup passes -- double-counting removal (different precursors latched
        /// onto one chromatographic feature) and target/decoy pair-dedup per
        /// base_id. Extracted from <see cref="ProcessFile"/> as pure code motion;
        /// the parity-locked scoring + dedup cores are invoked whole.
        /// </summary>
        private List<FdrEntry> ScoreAndDeduplicate(
            List<LibraryEntry> fullLibrary,
            IWindowSpectraProvider spectraProvider, List<MS1Spectrum> ms1Spectra,
            List<IsolationWindow> isolationWindows,
            RTCalibration rtCalibration,
            MzCalibrationResult ms2Cal, MzCalibrationResult ms1Cal,
            ScoringContext context, OspreyConfig config,
            string fileName, PipelineContext ctx)
        {
            // Run coelution scoring across all isolation windows from the caller's
            // window-spectra source (streaming per window, or the resident fallback).
            // Stage-4 scores a file once; then only DeduplicateDoubleCounting touches
            // the spectra, and it needs only the MS2 RTs the provider carries.
            var swScoring = Stopwatch.StartNew();
            var scoredEntries = ScoringTaskShared.Pipeline(ctx).RunCoelutionScoring(
                fullLibrary, spectraProvider, ms1Spectra,
                isolationWindows, rtCalibration,
                ms2Cal, ms1Cal,
                context);
            swScoring.Stop();
            double scoringSeconds = swScoring.Elapsed.TotalSeconds;
            double ratePerSec = scoringSeconds > 0.001
                ? scoredEntries.Count / scoringSeconds
                : 0.0;
            ctx.LogInfo(string.Format(
                "[TIMING] Coelution scoring: {0:F1}s ({1} candidates, {2:F0} cand/s)",
                scoringSeconds, scoredEntries.Count, ratePerSec));

            int nScoredTargets = scoredEntries.Count(e => !e.IsDecoy);
            int nScoredDecoys = scoredEntries.Count(e => e.IsDecoy);
            ctx.LogInfo(string.Format("Scored {0} entries ({1} targets, {2} decoys) for {3}",
                scoredEntries.Count,
                nScoredTargets,
                nScoredDecoys,
                fileName));

            // Drop double-counted entries (different precursors that latch
            // onto the same chromatographic feature within an isolation
            // window). Mirrors osprey/crates/osprey/src/pipeline.rs at the
            // same call site, between scoring and pair-deduplication.
            scoredEntries = ScoringTaskShared.Pipeline(ctx).DeduplicateDoubleCounting(
                scoredEntries, fullLibrary, spectraProvider.Ms2RetentionTimes, ms2Cal,
                isolationWindows, config);
            nScoredTargets = scoredEntries.Count(e => !e.IsDecoy);
            nScoredDecoys = scoredEntries.Count(e => e.IsDecoy);
            ctx.LogInfo(string.Format(
                "[COUNT] Coelution scored [{0}]: {1} entries ({2} targets, {3} decoys)",
                fileName, scoredEntries.Count, nScoredTargets, nScoredDecoys));

            // Deduplicate: keep best target and best decoy per base_id
            int nBeforeDedup = scoredEntries.Count;
            scoredEntries = ScoringTaskShared.Pipeline(ctx).DeduplicatePairs(scoredEntries);
            int nAfterDedup = scoredEntries.Count;
            ctx.LogInfo(string.Format(
                "[COUNT] Deduplication [{0}]: {1} -> {2} ({3} removed)",
                fileName, nBeforeDedup, nAfterDedup, nBeforeDedup - nAfterDedup));

            return scoredEntries;
        }

        /// <summary>
        /// Resolve the per-file RT/MS1/MS2 calibration: load a cached/Rust
        /// calibration JSON when OSPREY_LOAD_CALIBRATION points at one, else
        /// compute it via <see cref="Calibrator"/>; then persist the full
        /// calibration JSON next to the input. Returns the RT calibration (null
        /// when disabled / not computed); <paramref name="ms2Cal"/> and
        /// <paramref name="ms1Cal"/> receive the mass calibrations (Uncalibrated
        /// when none). Extracted from ProcessFile (pure code motion).
        /// </summary>
        private RTCalibration ResolveCalibration(
            string inputFile, string fileName,
            List<LibraryEntry> fullLibrary, SpectraWindowIndex windowIndex,
            List<MS1Spectrum> ms1Spectra,
            List<IsolationWindow> isolationWindows,
            ScoringContext context, OspreyConfig config,
            out MzCalibrationResult ms2Cal, out MzCalibrationResult ms1Cal,
            out ModelDiagnosticsData.CalFileRow calDiagnostics,
            PipelineContext ctx)
        {
            RTCalibration rtCalibration = null;
            ms2Cal = MzCalibrationResult.Uncalibrated();
            ms1Cal = MzCalibrationResult.Uncalibrated();
            // CAL-view per-file diagnostics for --model-diagnostics; null on the cached-JSON
            // load path (no calibration matches available) and on a normal run.
            calDiagnostics = null;
            // The wide pre-calibration RT tolerance (the "before" number in the
            // console calibration summary). Set by the compute path below; stays 0
            // when calibration is loaded from a cached JSON (no summary emitted then).
            double calInitialRtTolerance = 0.0;
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
                ctx.LogInfo(string.Format("[BISECT] Loading calibration from: {0}", loadCalPath));
                var calParams = CalibrationIO.LoadCalibration(loadCalPath);
                if (calParams.RtCalibration != null && calParams.RtCalibration.ModelParams != null)
                {
                    var mp = calParams.RtCalibration.ModelParams;
                    rtCalibration = RTCalibration.FromModelParams(
                        mp.LibraryRts, mp.FittedRts, mp.AbsResiduals,
                        calParams.RtCalibration.ResidualSD);
                    ctx.LogInfo(string.Format("Loaded RT calibration: {0} points, R2={1:F4}",
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
                    ctx.LogInfo(string.Format("Loaded MS2 calibration: mean={0:F4} {1}, SD={2:F4}",
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
                    ctx.LogInfo(string.Format("Loaded MS1 calibration: mean={0:F4} {1}, SD={2:F4}",
                        ms1Cal.Mean, ms1Cal.Unit, ms1Cal.SD));
                }
            }
            else if (config.RtCalibration.Enabled)
            {
                var swCal = Stopwatch.StartNew();
                rtCalibration = new Calibrator(ctx).RunCalibration(
                    fullLibrary, windowIndex, ms1Spectra, context,
                    out ms1Cal, out ms2Cal, out numSampledPrecursorsForMetadata,
                    out calInitialRtTolerance, out calDiagnostics);
                swCal.Stop();
                int nPoints = rtCalibration != null ? rtCalibration.Stats().NPoints : 0;
                ctx.LogInfo(string.Format(
                    "[TIMING] RT calibration: {0:F1}s ({1} calibration points)",
                    swCal.Elapsed.TotalSeconds, nPoints));

                // Curated calibration summary on the DEFAULT console (issue #4364):
                // the RT window before vs. after, the final search-window half-width
                // (and how fast/usable that makes the search), and the MS1/MS2 mass
                // corrections + applied tolerances. The detailed per-pass lines stay
                // at --verbose inside the Calibrator.
                EmitCalibrationSummary(ctx, config, fileName,
                    rtCalibration, ms1Cal, ms2Cal, calInitialRtTolerance);
            }

            // Dump 11 calibration summary scalars (MS1/MS2 mean/sd/count/
            // tolerance + RT n_points/r_squared/residual_sd) so the final
            // calibration state can be diff'd against Rust's cal JSON.
            ctx.Diagnostics?.WriteCalibrationSummary(rtCalibration, ms1Cal, ms2Cal);

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
                        Timestamp = DateTime.UtcNow.ToString("o"),
                        // DIA isolation scheme (from the first MS2 cycle) so an HPC
                        // merge node with no mzML can rehydrate the gap-fill m/z
                        // filter's per-file coverage. Mirrors Rust's
                        // CalibrationMetadata.isolation_scheme (pipeline.rs).
                        IsolationScheme = BuildIsolationScheme(isolationWindows)
                    },
                    Ms1Calibration = MzCalibrationJson.FromResult(ms1Cal),
                    Ms2Calibration = MzCalibrationJson.FromResult(ms2Cal),
                    // Persist the final RT search-window half-width too (issue #4364)
                    // so the JSON records the value the console highlights; pass the
                    // config's RT-tolerance clamps used at scoring time.
                    RtCalibration = RTCalibrationJson.FromRTCalibration(rtCalibration,
                        config.RtCalibration.MinRtTolerance, config.RtCalibration.MaxRtTolerance,
                        config.RtCalibration.MinCalibrationPoints),
                    SecondPassRt = null
                };
                // ArtifactPaths.ResolveOutputDir routes the calibration JSON to
                // the configured output dir (or the input's own directory by
                // default), matching where the resume-existence check looks.
                string calPath = CalibrationIO.CalibrationPathForInput(inputFile, ArtifactPaths.ResolveOutputDir(inputFile));
                CalibrationIO.SaveCalibration(calParams, calPath);
                ctx.LogInfo(string.Format("Saved calibration to {0}", calPath));
            }
            catch (Exception ex)
            {
                ctx.LogInfo("Warning: failed to save calibration JSON: " + ex.Message);
            }

            return rtCalibration;
        }

        /// <summary>
        /// Build the DIA <see cref="IsolationSchemeJson"/> block persisted in
        /// calibration.json from a file's first-cycle isolation windows, so an HPC
        /// merge node (which has no mzML) can rehydrate the gap-fill m/z filter's
        /// coverage. Windows are stored as <c>[center, width]</c> pairs; the scalar
        /// summary fields (num_windows / mz_min / mz_max / typical_width /
        /// uniform_width) mirror Rust's IsolationScheme (osprey/src/pipeline.rs
        /// extract_isolation_scheme). Returns <c>null</c> for an empty window list
        /// so the metadata omits the block (matching Rust's <c>Option::None</c>).
        /// </summary>
        private static IsolationSchemeJson BuildIsolationScheme(List<IsolationWindow> isolationWindows)
        {
            if (isolationWindows == null || isolationWindows.Count == 0)
                return null;
            var windows = new double[isolationWindows.Count][];
            double widthSum = 0.0;
            for (int i = 0; i < isolationWindows.Count; i++)
            {
                var w = isolationWindows[i];
                windows[i] = new[] { w.Center, w.Width };
                widthSum += w.Width;
            }
            double typicalWidth = widthSum / isolationWindows.Count;
            bool uniformWidth = true;
            for (int i = 0; i < isolationWindows.Count; i++)
            {
                if (Math.Abs(isolationWindows[i].Width - typicalWidth) >= 0.5)
                {
                    uniformWidth = false;
                    break;
                }
            }
            // ExtractIsolationWindows sorts by center, so first/last are min/max
            // -- matches Rust's mz_min/mz_max taken from the sorted window list.
            return new IsolationSchemeJson
            {
                NumWindows = isolationWindows.Count,
                MzMin = isolationWindows[0].Center,
                MzMax = isolationWindows[isolationWindows.Count - 1].Center,
                TypicalWidth = typicalWidth,
                UniformWidth = uniformWidth,
                Windows = windows,
            };
        }

        /// <summary>
        /// Emit the curated per-file calibration summary on the DEFAULT console
        /// (issue #4364). One compact block flagging the RT window before vs. after
        /// calibration (initial wide tolerance -> final search-window half-width),
        /// a fit-quality number (MAD / residual SD / R^2 / n), and the MS1/MS2
        /// systematic mass corrections with their applied tolerance windows and
        /// units. This is Mike's "is the AI library usable / how fast will the
        /// search be" sanity check. The detailed per-pass lines stay at --verbose
        /// inside the Calibrator; this promotes only the summary. Numeric values are
        /// formatted with the invariant culture (fixed decimals), not localizable
        /// text. Called only on the compute path (a loaded cal JSON is silent).
        /// </summary>
        private static void EmitCalibrationSummary(
            PipelineContext ctx, OspreyConfig config, string fileName,
            RTCalibration rtCalibration,
            MzCalibrationResult ms1Cal, MzCalibrationResult ms2Cal,
            double initialRtTolerance)
        {
            var ic = CultureInfo.InvariantCulture;
            ctx.LogInfo(string.Format(ic, "Calibration summary [{0}]:", fileName));

            if (rtCalibration == null)
            {
                ctx.LogInfo("  RT: calibration failed - using fallback RT tolerance");
            }
            else
            {
                var stats = rtCalibration.Stats();
                double rawTolerance = RTCalibration.SearchWindowRaw(stats.MAD);
                double finalTolerance = RTCalibration.SearchWindowHalfWidth(
                    stats.MAD, stats.NPoints,
                    config.RtCalibration.MinRtTolerance, config.RtCalibration.MaxRtTolerance,
                    config.RtCalibration.MinCalibrationPoints);
                string beforeStr = initialRtTolerance.ToString("F2", ic);
                string rawStr = rawTolerance.ToString("F2", ic);
                string finalStr = finalTolerance.ToString("F2", ic);
                string rtToleranceLine;
                if (double.IsNaN(finalTolerance))
                {
                    // Degenerate calibration (e.g. NaN MAD): no usable spread to report.
                    rtToleranceLine = string.Format(ic,
                        "  RT tolerance: +/-{0} min before -> undetermined after calibration (no usable RT spread)",
                        beforeStr);
                }
                else if (rawStr == finalStr)
                {
                    // In range, or a clamp too small to show at this precision: a single
                    // value is unambiguous, so skip the computed-vs-clamp call-out.
                    rtToleranceLine = string.Format(ic,
                        "  RT tolerance: +/-{0} min before -> +/-{1} min after calibration",
                        beforeStr, finalStr);
                }
                else if (finalTolerance > rawTolerance)
                {
                    // The computed 3*MAD*1.4826 was tighter than the floor: show the
                    // computed tolerance and the floor actually in use.
                    rtToleranceLine = string.Format(ic,
                        "  RT tolerance: +/-{0} min before -> +/-{1} min computed (3*MAD*1.4826), using +/-{2} min floor, after calibration",
                        beforeStr, rawStr, finalStr);
                }
                else
                {
                    // finalTolerance < rawTolerance: the computed value exceeded the
                    // ceiling, so show the computed tolerance and the cap in use.
                    rtToleranceLine = string.Format(ic,
                        "  RT tolerance: +/-{0} min before -> +/-{1} min computed (3*MAD*1.4826), capped at +/-{2} min, after calibration",
                        beforeStr, rawStr, finalStr);
                }
                ctx.LogInfo(rtToleranceLine);
                ctx.LogInfo(string.Format(ic,
                    "  RT fit: MAD={0:F3} min, residual SD={1:F3} min, R^2={2:F4}, n={3} points",
                    stats.MAD, stats.ResidualSD, stats.RSquared, stats.NPoints));
            }

            EmitMassCalibrationLine(ctx, "MS1", "precursor", ms1Cal);
            EmitMassCalibrationLine(ctx, "MS2", "fragment", ms2Cal);
        }

        /// <summary>
        /// One console line for an MS1/MS2 mass calibration in the summary block:
        /// systematic correction (mean offset), SD, and the applied tolerance window
        /// (|mean| + 3*SD), all in the calibration's unit. Reports "not calibrated"
        /// when the level had no usable errors.
        /// </summary>
        private static void EmitMassCalibrationLine(
            PipelineContext ctx, string level, string matchNoun, MzCalibrationResult cal)
        {
            var ic = CultureInfo.InvariantCulture;
            if (cal == null || !cal.Calibrated)
            {
                ctx.LogInfo(string.Format(ic, "  {0} mass: not calibrated", level));
                return;
            }
            double tolerance = cal.AdjustedTolerance ?? (Math.Abs(cal.Mean) + 3.0 * cal.SD);
            ctx.LogInfo(string.Format(ic,
                "  {0} mass: correction={1:F2} {2}, SD={3:F2} {2}, tolerance=+/-{4:F2} {2} (n={5} {6} matches)",
                level, cal.Mean, cal.Unit, cal.SD, tolerance, cal.Count, matchNoun));
        }

        /// <summary>
        /// Write a TSV dump of per-entry feature values for direct comparison with
        /// the Rust implementation's PIN output. Format matches Rust's PIN columns:
        /// psm_id, label, scan, + 21 features. Sorted by (modified_sequence, charge,
        /// scan_number) for stable diffing against Rust's output.
        /// </summary>
        private void WriteFeatureDump(
            string inputFile, string fileName,
            List<FdrEntry> scoredEntries, PipelineContext ctx)
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
                .Where(e => e.Features != null && e.Features.Length == ScoringTaskShared.NUM_PIN_FEATURES)
                .OrderBy(e => e.ModifiedSequence, StringComparer.Ordinal)
                .ThenBy(e => e.Charge)
                .ThenBy(e => e.ScanNumber)
                .ToList();

            using (var writer = new StreamWriter(dumpPath))
            {
                // LF newlines so the dump is byte-stable across Windows and
                // Linux for cross-impl diffing against Rust's PIN output;
                // matches the convention used by OspreyDiagnosticsLog.
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
                    for (int i = 0; i < ScoringTaskShared.NUM_PIN_FEATURES; i++)
                        cols.Add(e.Features[i].ToString("G17"));
                    cols.Add(e.ModifiedSequence ?? "");
                    writer.WriteLine(string.Join("\t", cols));
                }
            }

            ctx.LogInfo(string.Format("[COUNT] Wrote feature dump: {0} ({1} entries)",
                dumpPath, sorted.Count));
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
        /// (<see cref="PerFileRescoreTask"/>.LoadSpectraForRescore), a separate follow-up.
        /// </summary>
        private SpectraWindowIndex EnsureSpectraCache(string inputFile, bool serializeMzmlRead,
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
                ScoringTaskShared.s_mzmlReadGate.Wait();
            try
            {
                mzmlResult = MzmlReader.LoadAllSpectra(inputFile);
            }
            finally
            {
                if (serializeMzmlRead)
                    ScoringTaskShared.s_mzmlReadGate.Release();
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
