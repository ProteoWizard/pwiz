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
    /// SecondPassFDR only needs the resulting parquet sidecars.
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
    /// as instance properties for FirstPassFdrTask + downstream tasks to
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
        // mutable entry buffer (FirstPassFDR and PerFileRescore publish the later
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
            // FirstPassFdrTask happens to read ScoredEntries first, which materializes
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
        // produces, sharing it with FirstPassFdrTask's reconciliation accessors
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
        // List<KeyValuePair<string, List<FdrEntry>>>: FirstPassFDR compacts it and
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
        // ValidityKey is the default: the search and library hashes, plus
        // the peak-pick arm the base key carries. The pick belongs to THIS
        // stage - it chooses which candidate peak each precursor's row
        // describes - so a re-run under a different pick model must not
        // adopt these parquets.
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
            // Decouple scoring from FirstPassFDR (issue #4355): during the scoring
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
            // would produce a hash that the --input-scores validator would
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
            // rematerialize the cold FdrEntry stubs FirstPassFDR needs by reloading
            // them from each scored file's just-written .scores.parquet, in the
            // same order scoring produced (issue #4355). Only the scalar stub
            // fields are read back -- no PIN features / CWT / fragment arrays --
            // which is exactly the cold shape the straight-through path already
            // left here after ProcessFile spilled every file's full results and
            // nulled those arrays (FirstPassFDR streams features back per file). The
            // live per-file RTCalibration objects in perFileCalibrations are NOT
            // reloaded: they are not stored in .scores.parquet, so they must stay
            // the live objects harvested during scoring.
            // The lean path is valid only where FirstPassFdrTask actually consumes a
            // projection. It must mirror that task's dispatch exactly (FirstPassFdrTask.cs:
            // UseFdrProjection && Percolator && !needsResidentFirstPassPool): any other
            // combination -- a non-Percolator FdrMethod, OSPREY_FDR_PROJECTION=0, or the
            // resident-pool consumer FDRBench pass 1, which walks the full pre-compaction
            // FdrEntry pool -- still needs the fat stubs here. --model-diagnostics is NOT
            // one of them any more (#4505): it streams its report on every path.
            // The fat/lean decision, and the guard that checks it, key off CanUseLeanProjection -
            // the same predicate the two sibling sites use. Bare NeedsResidentPool no longer
            // excludes ExpectReconciledInput (#4486), so a config with it set reached here with
            // needsResidentPool == false: GuardResidentPool was handed false and refused nothing,
            // and the lean branch streamed projection rows with no FdrEntry allocated, handing
            // Stage 7 empty per-file lists. That is precisely what ResidentPoolGuardTest asserts
            // must never happen.
            //
            // NOT reachable today, and the claim that it was is wrong: Program.cs rejects
            // --task SecondPassFDR combined with --input and requires --input-scores, so
            // ExpectReconciledInput implies InputScores.Count > 0 and IsIncluded returns false -
            // Run is never entered on that config. This is aligned with its two siblings so the
            // one decision has one predicate, not so that a live defect is closed.
            bool needsResidentPool = !CanUseLeanProjection(ctx.Config, hasReconSidecars: false,
                                                           OspreyEnvironment.UseFdrProjection);
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
                // ~53 GB on an 82-file Astral run (191M x ~280 B) purely so FirstPassFDR
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
            // bundle that SecondPassFDR will read. The compute-from-spectra
            // counterpart is Run.
            var config = ctx.Config;

            // Stage 1: library + decoys (needed by Stage 5+ even though the
            // per-file scores are loaded rather than computed).
            if (!LoadLibraryAndDecoys(config, out _, ctx))
                return false;

            var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>();
            var perFileCalibrations = new ConcurrentDictionary<string, RTCalibration>();
            // Rehydrated from calibration.json's isolation_scheme by
            // LoadJoinOnlyScores (SecondPassFDR has no mzML to extract from).
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
            // + a firing bundle hydrator). Static on a real SecondPassFDR node; belt-and-suspenders.
            bool hasReconSidecars = AllHaveReconSidecars(config);
            bool streamCompaction = ShouldStreamCompaction(config, hasReconSidecars, ctx);
            var swAllFiles = Stopwatch.StartNew();
            var projections = LoadJoinOnlyScores(config, perFileEntries, perFileParquetPaths,
                perFileCalibrations, perFileIsolationMz, hasReconSidecars, streamCompaction,
                out bool hydrationFailed, ctx);
            swAllFiles.Stop();
            if (hydrationFailed)
                return false;  // Error already logged and ExitCode set by the hydrate.
            ctx.LogInfo(string.Format(@"[TIMING] All files processed: {0:F1}s",
                swAllFiles.Elapsed.TotalSeconds));

            // long: TotalPreCompactionStubs below is ~4.2 M per file and overflows an int
            // past ~505 files.
            long totalScored;
            if (projections != null)
            {
                totalScored = projections.TotalRows;
            }
            else if (_rescoreInputs?.PreCompactionTallies != null)
            {
                // Streamed hydrate: perFileEntries already holds only the compaction
                // survivors, so summing it here would report the post-compaction count and
                // could trip the "no scored entries" guard below on a join whose survivor
                // set is empty. The per-file tally captured while each file's full stub
                // list was briefly resident is the same number the resident sum produced.
                totalScored = _rescoreInputs.TotalPreCompactionStubs;
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
            // needs the resident pool. FirstPassFDR consumes the projection set identically
            // whether Run or this path produced it.
            //
            // --model-diagnostics is NOT a trigger, on a full resume or any other (#4505).
            // It used to be: with every 1st-pass sidecar already on disk FirstPassFDR skips its
            // score pass, so the streaming accumulator was never fed and the report fell back
            // to the batch ModelDiagnosticsReport.Write over the RESIDENT entries. FirstPassFDR's
            // rehydrate now feeds that accumulator from the per-file load it already performs
            // (FirstPassFdrTask.StreamOwnReconciliationBundle), off the same PRE-compaction rows,
            // so the report emits from this lean load at any file count.
            FdrProjectionSet projections = null;

            var swAllFiles = Stopwatch.StartNew();
            if (config.InputFiles != null)
            {
                // Counts-only (issue #4355 struct-shrink S3, Stage B): the resume lean path builds
                // only per-file row counts; the 1st-pass streaming score path re-reads identity +
                // features from parquet, so no resident FdrProjection[] buffer is allocated.
                // Same predicate as the --input-scores loader, not a re-derivation of it (#4486).
                // This site gated on bare NeedsResidentPool, which no longer excludes
                // ExpectReconciledInput, so an ExpectReconciledInput config arriving here would
                // take the lean branch and add an EMPTY entry list per file. Not reachable today
                // (Run routes InputScores runs away from Rehydrate), but re-deriving the rule at
                // one call site and importing it at the other is precisely the drift this change
                // exists to remove. hasReconSidecars is false here: this path has no bundle.
                // ARM THE GUARD ON THE SAME DECISION, for the reason the branch below states: the
                // fat path IS the resident pool here, so guarding on a separate NeedsResidentPool
                // let a config with needsResidentPool false but builder null (ExpectReconciledInput)
                // take the O(files) fat load with the guard disarmed and no warning. Two predicates
                // that can disagree about one choice is the drift this change removes; the guard is
                // the third reader of that choice and has to key off it too. Placed inside the
                // InputFiles block because with no input files nothing loads and a guard here would
                // only produce a false positive.
                var builder = CanUseLeanProjection(config, hasReconSidecars: false,
                                                   OspreyEnvironment.UseFdrProjection)
                    ? new FdrProjectionSet.Builder(countsOnly: true)
                    : null;
                GuardResidentPool(config, needsResidentPool: builder == null);

                // Per-file progress so this all-files load is not a silent multi-minute
                // stall on a large resume (the phase that looked hung on the 82-file run).
                using (var loadProgress = new ProgressReporter(@"Loading scored entries", config.InputFiles.Count))
                {
                    int fileIdx = 0;
                    // Sequential in InputFiles order to match Run's "collect in original
                    // order" -- downstream FirstPassFDR iterates perFileEntries.
                    foreach (string inputFile in config.InputFiles)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(inputFile);
                        string scoresPath = ParquetScoreCache.GetScoresPath(inputFile);
                        // Branch on the BUILDER, not on needsResidentPool. The two used to be
                        // exact complements here; CanUseLeanProjection has a third term, so a
                        // config with needsResidentPool false that still may not go lean
                        // (ExpectReconciledInput) would otherwise take the lean branch with a
                        // null builder and throw. Keying both off one value is what makes the
                        // fat/lean choice a single decision rather than two that can disagree.
                        if (builder == null)
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
            int nFiles, long totalScored, FdrProjectionSet projections = null)
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
            // FirstPassFdrTask reads it only under config.ModelDiagnostics and tolerates
            // empty. See TODO-20260712 for the HPC-split persistence caveat.
            ctx.Publish(new PerFileCalibrationDiagnostics(_perFileCalibrationDiagnostics, _calibrationMassUnit));
            ctx.Publish(new PerFileIsolationMz(_perFileIsolationMz));
            ctx.Publish(new PerFileParquetPaths(_perFileParquetPaths));
            ctx.Publish(new ScoredEntries(_perFileEntries));
            // Lean first-pass rows (issue #4397). Null on the resident paths and on FDRBench
            // pass 1, which publish fat stubs above; FirstPassFdrTask falls back to ScoredEntries
            // whenever this is null. A --model-diagnostics resume publishes a NON-null
            // projection since #4505 - it no longer needs resident entries, because
            // FirstPassFDR's rehydrate streams the report off its own per-file load.
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
            // on a SecondPassFDR node) will pick them up and run Stage 5+.
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
            // ORDER MATTERS. A library that supplies its own decoys is handled FIRST,
            // before the SecondPassFDR skip, because that arm is not only about decoys:
            // TryPairSuppliedDecoys is the sole caller of DecoyPairingManifest.ApplyToLibrary,
            // which rewrites ProteinIds from the manifest's clean accessions. Testing
            // ExpectReconciledInput first made this arm unreachable in SecondPassFDR - the one
            // phase that writes the blib - so a distributed run with --decoy-pairing-manifest
            // emitted the library's per-peptide "sp|P12345_pep00001|GENE" accessions instead of
            // the clean ones, and computed protein parsimony and picked-protein FDR on them.
            // Straight-through and resume never hit it, which is why only the HPC chain diverged.
            // Rust gates on library_supplies_decoys alone and was always correct here, so this
            // restores cross-impl parity rather than changing behavior away from it.
            if (librarySuppliesDecoys)
            {
                decoys = new List<LibraryEntry>();
                if (!TryPairSuppliedDecoys(library, config, nLibraryTargets, ctx))
                    return false;
            }
            else if (config.ExpectReconciledInput)
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
                // The ~45s saved is DecoyGenerator work, done only by the generated-decoy
                // arm below, so ordering the supplied-decoy arm ahead of this costs nothing.
                decoys = new List<LibraryEntry>();
            }
            else
            {
                // GenerateAllWithCollisionDetection interns the freshly-minted
                // decoy strings ("DECOY_"+accession / modified sequence) through
                // its own pool and logs the collapse summary; no post-pass
                // interning is needed here.
                decoys = DecoyGenerator.GenerateAllWithCollisionDetection(
                    library, config, ctx.LogInfo, omitFragments, out List<LibraryEntry> validTargets);
                library = validTargets;
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
        /// <paramref name="hydrationFailed"/> is set when the streaming reconciled-bundle
        /// hydrate failed on a corrupt sidecar: it already logged the error and set
        /// <see cref="PipelineContext.ExitCode"/>, so the caller only has to stop.
        /// </summary>
        private FdrProjectionSet LoadJoinOnlyScores(
            OspreyConfig config,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            Dictionary<string, string> perFileParquetPaths,
            ConcurrentDictionary<string, RTCalibration> perFileCalibrations,
            ConcurrentDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz,
            bool hasReconSidecars,
            bool streamCompaction,
            out bool hydrationFailed,
            PipelineContext ctx)
        {
            hydrationFailed = false;
            // --task FirstPassFDR: load per-file FdrEntry stubs directly from
            // each .scores.parquet listed via --input-scores. Skips the
            // per-file Stage 2-4 scoring (Stage 1 library load already ran
            // in Run). Also loads a best-effort calibration JSON sibling
            // per file (the loop below) for Stage 6 reconciliation, like
            // the Rust impl.
            // Guard: hash check against current --library and search params.
            // Aborts with a clear, file-named error if the operator points
            // SecondPassFDR at parquets from a different scoring run.
            string validationError = ParquetScoreCache.ValidateScoresParquetGroup(
                config.InputScores, config, OspreyVersion.Current);
            if (validationError != null)
                throw new InvalidDataException(validationError);

            ctx.LogInfo(string.Format(
                @"--input-scores: loading {0} per-file score parquet(s)",
                config.InputScores.Count));
            // Lean on the HPC merge/join too (#4400): a large FirstPassFDR node
            // loading every worker's .scores.parquet used to rebuild the full fat
            // FdrEntry stubs + PIN features (~53 GB at 82 files) -- the same Stage-5
            // blowup the resume path had. Stream 32 B FdrProjection rows instead, unless
            // this run needs the resident pool (an opt-in feature output) or is a
            // reconciled 2nd-pass bundle hydration (AllHaveReconSidecars -- FirstPassFDR
            // skips Percolator there and HydrateReconciliationOverlay reads the fat stubs).
            // Counts-only (issue #4355 struct-shrink S3, Stage B): the lean path builds
            // only per-file row counts; the 1st-pass streaming score path re-reads identity +
            // features from parquet, so the resident FdrProjection[] buffer is never allocated.
            bool needsResidentPool = NeedsResidentPool(config);
            var builder = CanUseLeanProjection(config, hasReconSidecars, OspreyEnvironment.UseFdrProjection)
                ? new FdrProjectionSet.Builder(countsOnly: true)
                : null;
            // The reconciled-bundle hydration (hasReconSidecars) needs the STUBS but not
            // their PIN features, so only a genuine resident pool loads features. See the
            // stubs-only branch below for why that is safe.
            //
            // This one KEEPS NeedsResidentPool deliberately, where the fat/lean choice above
            // moved to the builder. They answer different questions and the predicates diverged
            // when ExpectReconciledInput left NeedsResidentPool (#4486): "does a consumer read
            // Features off THESE stubs" (fdrbench-pass1, a non-Percolator FdrMethod,
            // OSPREY_FDR_PROJECTION=0) is still exactly NeedsResidentPool, while "may this load
            // go lean" additionally excludes the reconciled-input merge. Do not "fix" this by
            // copying the builder decision: the merge does not read Features off these stubs -
            // both pass-2 shapes reload them per file from the reconciled parquet
            // (ComputePass2TransferCompeteFull's own read, or ComputePass2Resident's), and
            // EffectiveScoresPathFromScoresPath falls back to the original parquet when no
            // reconciled one exists, so the reload does not depend on hasReconSidecars either.
            bool loadFeatures = needsResidentPool;

            // The --input-files paths at :381 and :644 THROW on the same O(files) situation.
            // This one must not: every configuration that still reaches the resident branches
            // worked before the streaming hydrate landed - a --task FirstPassFDR re-run over
            // parquets whose sidecars are already on disk, and the OSPREY_DUMP_PERCOLATOR
            // bisection dump, which by design keeps the resident path. (The --task
            // SecondPassFDR merge was the third of these until #4486 streamed it; it is listed
            // here only because that is what this justification used to rest on.) Throwing
            // would break the rest. Warn with the consumer named instead, so an operator who meets the
            // O(files) peak at scale knows which knob put them on it. When the streaming
            // hydrate below takes the load the pool is bounded and there is nothing to say.
            // Warn on exactly the condition that SELECTS the resident loop below, rather than
            // on a hand-listed set of configs that can drift from it (#4486). The old test
            // was `needsResidentPool || (hasReconSidecars && !streamCompaction)`, and the new
            // CanUseLeanProjection term opened a case it does not cover: a reconciled-input
            // merge whose inputs are missing a .reconciliation.json takes the resident loop
            // with needsResidentPool false, hasReconSidecars false and streamCompaction
            // false, so every term was false and the O(files) load happened SILENTLY. The
            // resident loop runs iff it neither streams nor takes the lean builder, so that
            // is what this asks.
            if (!streamCompaction && builder == null)
                WarnPreCompactionPool(config, hasReconSidecars, ctx);

            // Bounded reconciled-bundle rehydrate: hand the per-file stub load to the
            // streaming hydrate, which compacts each file before touching the next, so the
            // pre-compaction pool (~1.19 GB per file) is never resident for more than one
            // file. The loop below is the resident twin - it materializes EVERY file's
            // pre-compaction pool and lets RescoreCompaction discard the ~52x non-survivors
            // afterwards, which is O(files) and does not fit at 82.
            if (streamCompaction)
            {
                // --model-diagnostics: the report needs the PRE-compaction entries (compaction
                // discards ~52x of them, mostly the decoys and entrapment its FDP and
                // calibration views are built from), and this load is the last place they all
                // exist. Fold each file's rows into the same streaming accumulator the
                // projection path uses, one file at a time, so FirstPassFDR's rehydrate can emit
                // the identical report without the O(files) resident pool. Null (nothing
                // constructed, nothing folded) off the report path.
                var mdiagAccumulator = config.ModelDiagnostics
                    ? FirstPassFdrTask.BuildModelDiagnosticsAccumulator(
                        JoinOnlyFileNames(config), _libraryById, config, ctx.LogInfo)
                    : null;
                // Same graceful handling as the batch twin in HydrateRescoreBundleIfPresent:
                // a corrupt or mismatched sidecar is an operator-facing error line and a
                // non-zero exit code, not an unhandled stack trace.
                _rescoreInputs = HydrateRescoreBundleOrNull(
                    () => RescoreHydration.HydrateCompactedStreaming(
                        perFileEntries, config.InputScores,
                        (fileIdx, fileName, parquetPath) => LoadJoinOnlyScoresForFile(
                            config, fileIdx, fileName, parquetPath, perFileParquetPaths,
                            perFileCalibrations, perFileIsolationMz, ctx),
                        (fileIdx, fileName, stubs, tally) =>
                        {
                            ScoringTaskShared.TallyPreCompaction(config, stubs, tally);
                            if (mdiagAccumulator != null)
                                ScoringTaskShared.FeedModelDiagnostics(mdiagAccumulator, fileIdx, stubs);
                        }), ctx);
                if (_rescoreInputs == null)
                {
                    hydrationFailed = true;
                    return null;
                }
                _rescoreInputs.ModelDiagnosticsAccumulator = mdiagAccumulator;
                if (ctx.Diagnostics?.CalibrationOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_CALIBRATION_ONLY");
                return null;
            }

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
                    // (no feature memory). A SecondPassFDR node pointed at a foreign/truncated
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
                else if (loadFeatures)
                {
                    // Fat: Stage 5+ (Percolator SVM) requires the 21 PIN features on each
                    // FdrEntry. Load them in lockstep with the stubs and bind by row index
                    // (rows stable).
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
                else
                {
                    // Stubs only. This branch is the reconciled-bundle hydration
                    // (hasReconSidecars) where HydrateReconciliationOverlay reads the STUBS
                    // but never the features - RescoreHydration has no reference to
                    // FdrEntry.Features at all - and FirstPassFdrTask nulls Features on every
                    // hydrated entry before CompactFirstPass anyway, to keep the
                    // "Features != null means rescored" sentinel honest for Stage 6. Loading
                    // 21 doubles per row here only to null them cost ~800 MB per file at
                    // ~4.2M rows, the dominant term in the O(files) rehydrate peak.
                    //
                    // Safe because nothing on this path reads entry.Features afterwards. That
                    // USED to be phrased as "NeedsResidentPool is false says so", which stopped
                    // being true when #4486 took ExpectReconciledInput out of that predicate:
                    // the merge node now has NeedsResidentPool false AND does run a 2nd-pass
                    // Percolator. It is still safe there, for a different reason - Pass2FdrSidecar
                    // reloads the features it needs from each reconciled parquet rather than
                    // reading them off these stubs - so state the reason rather than a predicate
                    // that no longer implies it.
                    var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(parquetPath);
                    // Keep the fail-fast the feature load used to provide: a foreign or
                    // truncated parquet missing the PIN schema must stop here, not surface
                    // downstream. Footer-only probe, no feature memory (same guard the lean
                    // streaming branch above uses).
                    if (!ParquetScoreCache.HasPinFeatureColumns(parquetPath))
                    {
                        throw new InvalidDataException(string.Format(
                            @"--input-scores: parquet {0} is missing the PIN feature columns -- it is not a valid Osprey scores parquet. Delete it and re-run so it is regenerated.",
                            parquetPath));
                    }
                    ctx.LogInfo(string.Format(
                        @"  Loaded {0} FDR stubs (features not loaded - not read on this path)", stubs.Count));
                    perFileEntries.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, stubs));
                }
                perFileParquetPaths[fileName] = parquetPath;
                LoadJoinOnlyCalibration(fileName, parquetPath, perFileCalibrations,
                    perFileIsolationMz, ctx);
            }
            if (ctx.Diagnostics?.CalibrationOnly ?? false)
                OspreyDiagnosticsLog.ExitAfterDump(@"OSPREY_CALIBRATION_ONLY");
            return builder?.Build();
        }

        /// <summary>
        /// Whether the reconciled-bundle rehydrate can take the file-count-bounded
        /// <see cref="RescoreHydration.HydrateCompactedStreaming"/> path, which compacts each
        /// file's stubs as it loads them instead of materializing every file's pre-compaction
        /// pool and compacting afterwards. Two conditions, both necessary:
        ///
        /// 1. The reconciled bundle exists (<paramref name="hasReconSidecars"/>) - it is the
        ///    envelope that carries the compaction predicate, so without it there is nothing
        ///    to compact against at load time.
        /// 2. Nothing in the run reads the PRE-compaction pool
        ///    (<see cref="PreCompactionPoolReason"/> finds no consumer).
        ///
        /// Term 2 asks <see cref="FirstPassFdrTask.IsIncludedFor"/> directly: FirstPassFDR must
        /// be EXCLUDED from this pipeline and reachable only through its bundle-adopt
        /// Rehydrate, because a FirstPassFDR that Ran would train first-pass Percolator on
        /// whatever <c>ScoredEntries</c> holds - which must be the full pre-compaction pool.
        /// It used to test <c>!NoJoin</c> as a stand-in for that, which was right for every
        /// task except <c>--task SecondPassFDR</c> - NoJoin false, ExpectReconciledInput true,
        /// FirstPassFdrTask excluded - so the proxy forced an O(files) resident pool for a
        /// consumer that does not exist (#4486).
        ///
        /// What the resident-pool terms still filter that <c>NoJoin</c> does not: the
        /// OSPREY_DUMP_PERCOLATOR bisection dump (emitted by FirstPassFDR's rehydrate before it
        /// compacts, so it genuinely needs the all-files pre-compaction pool rather than a
        /// silently post-compaction one), OSPREY_FDR_PROJECTION=0, a non-Percolator
        /// FdrMethod, and --fdrbench-pass 1. OSPREY_PASS2_QVALUE=transfer is NOT among them:
        /// the per-run-only redesign (#4438) resolves each adjusted peak against that file's
        /// own on-disk sidecar. <c>--task SecondPassFDR</c> is not among them either, and
        /// since #4486 that is the point rather than an aside: it is the one task that
        /// reaches here with FirstPassFdrTask excluded, so it STREAMS.
        ///
        /// --model-diagnostics needs no exclusion at all any more. It needs the same
        /// pre-compaction rows, but it consumes them one at a time:
        /// <see cref="ScoringTaskShared.FeedModelDiagnostics"/> folds every row into a
        /// ModelDiagnosticsData.Accumulator during this load, after the 1st-pass sidecar
        /// overlay and before compaction drops the non-survivors, and FirstPassFDR reports from
        /// that reduction instead of from the pool. Same rows in, same report out, bounded
        /// memory - so the report no longer forces O(files).
        /// </summary>
        private static bool ShouldStreamCompaction(
            OspreyConfig config, bool hasReconSidecars, PipelineContext ctx)
        {
            return PreCompactionPoolReason(config, hasReconSidecars, ctx) == null;
        }

        /// <summary>
        /// Tell the operator that this run is taking the RESIDENT pre-compaction first-pass
        /// pool and which consumer put it there. A warning, not the throw the
        /// <c>--input-files</c> paths use: every configuration that reaches
        /// <see cref="LoadJoinOnlyScores"/>'s resident branches worked before the bounded
        /// streaming hydrate existed, so failing them would be a regression, not a guard.
        /// </summary>
        private static void WarnPreCompactionPool(
            OspreyConfig config, bool hasReconSidecars, PipelineContext ctx)
        {
            string reason = PreCompactionPoolReason(config, hasReconSidecars, ctx)
                            ?? @"This configuration";
            ctx.LogWarning(string.Format(
                @"{0} requires the RESIDENT pre-compaction first-pass pool: every " +
                @"--input-scores file's full stub list is held in memory at once, so memory " +
                @"here grows O(files) and can exhaust RAM at large file counts. The bounded " +
                @"per-file streaming hydrate cannot serve that consumer.", reason));
        }

        /// <summary>
        /// The single consumer that forces the RESIDENT pre-compaction first-pass pool on the
        /// <c>--input-scores</c> load, named for a human, or <c>null</c> when nothing needs it
        /// and <see cref="RescoreHydration.HydrateCompactedStreaming"/> can compact one file
        /// at a time. Computed once and used for both the streaming decision
        /// (<see cref="ShouldStreamCompaction"/>) and the warning text
        /// (<see cref="WarnPreCompactionPool"/>), so the two can never disagree about why.
        /// Order is most-specific-first; only the reported string depends on it.
        /// </summary>
        private static string PreCompactionPoolReason(
            OspreyConfig config, bool hasReconSidecars, PipelineContext ctx)
        {
            if (ctx.Diagnostics?.DumpPercolator ?? false)
                return @"OSPREY_DUMP_PERCOLATOR";
            if (!string.IsNullOrEmpty(config.OutputFdrBench) && config.FdrBenchPass == 1)
                return @"--fdrbench-pass 1";
            // OSPREY_PASS2_QVALUE=transfer is deliberately NOT a reason here, and must not
            // become one again: the per-run-only redesign maps each adjusted peak through
            // that file's own 1st-pass (score -> run q) sidecar, one file at a time, so it
            // needs no pre-compaction pool. See NeedsResidentPool.
            if (!config.FdrMethod.UsesPercolatorFramework())
                return @"A non-Percolator FDR method";
            if (!OspreyEnvironment.UseFdrProjection)
                return @"OSPREY_FDR_PROJECTION=0";
            // FirstPassFDR is IN this pipeline, so it will Run and train first-pass Percolator
            // off ScoredEntries - which has to be the full pre-compaction pool, not the
            // survivors the streaming hydrate would leave. Unconditional on
            // hasReconSidecars: every reason above is a resident-pool consumer and returns
            // first, so reaching here means none of them applies.
            //
            // Ask the task's own membership predicate rather than the former `!NoJoin` proxy
            // (#4486). The two agree on every task but --task SecondPassFDR, which leaves
            // NoJoin false while setting ExpectReconciledInput: FirstPassFdrTask is excluded
            // there, so nothing trains, and the proxy was forcing an O(files) resident pool
            // for a consumer that does not exist. Re-deriving membership here is what let
            // them drift, so this defers to the one definition.
            if (FirstPassFdrTask.IsIncludedFor(config))
                return @"First-pass Percolator training in this process";
            // No bundle at all. The reconciliation envelope is what carries the compaction
            // predicate, so without it there is nothing to compact against at load time and
            // streaming cannot run. Last because every reason above names a real consumer,
            // and this one is a missing input rather than a consumer.
            if (!hasReconSidecars)
                return @"No reconciled bundle on the --input-scores inputs";
            return null;
        }

        /// <summary>
        /// One file's stub load for the streaming reconciled-bundle rehydrate: the same
        /// per-file work <see cref="LoadJoinOnlyScores"/>'s resident loop does (header line,
        /// stubs-only parquet read with the PIN-schema fail-fast, parquet-path map,
        /// best-effort calibration sibling), minus the append - the streaming hydrate
        /// appends the compaction survivors itself once it has overlaid the 1st-pass sidecar.
        /// PIN features are deliberately not loaded: nothing on this path reads them (the
        /// bundle hydration nulls them anyway to keep the "Features != null means rescored"
        /// sentinel honest), and at ~4.2 M rows they cost ~800 MB per file.
        /// </summary>
        private static List<FdrEntry> LoadJoinOnlyScoresForFile(
            OspreyConfig config,
            int fileIdx,
            string fileName,
            string parquetPath,
            Dictionary<string, string> perFileParquetPaths,
            ConcurrentDictionary<string, RTCalibration> perFileCalibrations,
            ConcurrentDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz,
            PipelineContext ctx)
        {
            ctx.LogInfo(string.Format(@"===== Loading file {0}/{1}: {2} (from {3}) =====",
                fileIdx + 1, config.InputScores.Count, fileName, parquetPath));
            var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(parquetPath);
            // Keep the fail-fast the feature load used to provide: a foreign or truncated
            // parquet missing the PIN schema must stop here, not surface downstream.
            // Footer-only probe, no feature memory.
            if (!ParquetScoreCache.HasPinFeatureColumns(parquetPath))
            {
                throw new InvalidDataException(string.Format(
                    @"--input-scores: parquet {0} is missing the PIN feature columns -- it is not a valid Osprey scores parquet. Delete it and re-run so it is regenerated.",
                    parquetPath));
            }
            ctx.LogInfo(string.Format(
                @"  Loaded {0} FDR stubs (features not loaded - not read on this path)", stubs.Count));
            perFileParquetPaths[fileName] = parquetPath;
            LoadJoinOnlyCalibration(fileName, parquetPath, perFileCalibrations,
                perFileIsolationMz, ctx);
            return stubs;
        }

        /// <summary>
        /// The <c>--input-scores</c> per-file names in input order, each derived from its
        /// parquet stem through the same shared suffix-strip helper
        /// <see cref="LoadJoinOnlyScores"/>'s resident loop and
        /// <see cref="RescoreHydration.HydrateCompactedStreaming"/> use, so index i here names
        /// the file the streaming hydrate reports at index i.
        /// </summary>
        private static string[] JoinOnlyFileNames(OspreyConfig config)
        {
            var fileNames = new string[config.InputScores.Count];
            for (int i = 0; i < fileNames.Length; i++)
            {
                fileNames[i] = Path.GetFileNameWithoutExtension(
                    RescoreHydration.SyntheticInputFromParquet(config.InputScores[i])) ?? string.Empty;
            }
            return fileNames;
        }

        /// <summary>
        /// Best-effort per-file calibration JSON load for Stage 6 reconciliation: the refined
        /// RT calibration plus the isolation-window coverage the gap-fill m/z filter needs.
        /// Mirrors osprey/src/pipeline.rs:2573-2588. A read failure is logged and swallowed -
        /// a SecondPassFDR node with no calibration sibling still runs, with the filter disabled for
        /// that file.
        /// </summary>
        private static void LoadJoinOnlyCalibration(
            string fileName,
            string parquetPath,
            ConcurrentDictionary<string, RTCalibration> perFileCalibrations,
            ConcurrentDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz,
            PipelineContext ctx)
        {
            try
            {
                string parquetDir = Path.GetDirectoryName(Path.GetFullPath(parquetPath));
                if (parquetDir != null)
                {
                    // fileName is the bare input stem (the scores / reconciled-scores suffix
                    // was stripped by SyntheticInputFromParquet), so combining it with
                    // parquetDir yields the same input-stem path ProcessFile passes to
                    // CalibrationPathForInput.
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

                        // Isolation-window coverage for the gap-fill m/z filter -- read
                        // independent of RT calibration from the isolation_scheme block, so
                        // a SecondPassFDR node with no mzML still gets per-file coverage. Mirrors
                        // Rust's isolation_intervals_from_cal (pipeline.rs).
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
                // Already hydrated when the loader took the file-count-bounded streaming
                // path (ShouldStreamCompaction): it has to own the hydrate, because the
                // sidecar overlay and the compaction have to happen inside its per-file
                // loop for the pre-compaction pool to stay one-file-at-a-time. The summary
                // line below is shared by both paths; the feature null-out is NOT - see
                // where it is guarded.
                bool streamed = _rescoreInputs != null;
                if (_rescoreInputs == null)
                {
                    _rescoreInputs = HydrateRescoreBundleOrNull(
                        () => RescoreHydration.HydrateReconciliationOverlay(
                            perFileEntries, config.InputScores), ctx);
                }
                if (_rescoreInputs == null)
                    return false;
                // Clear PIN features on bundle-hydrated stubs so
                // PerFileRescoreTask's WriteReconciledParquet can keep
                // its "Features != null means this entry was rescored"
                // criterion -- with features pre-populated from the
                // parquet, every entry would otherwise look rescored
                // and overwrite the original parquet row's binary
                // blob columns (fragment_mzs, ref_xic_*, bounds_*).
                // Bundle path doesn't need PIN features downstream:
                // FirstPassFdrTask skips Percolator on this path, so the
                // SVM training input is irrelevant.
                // Skipped on the streaming arm, where it is provably a no-op (#4486):
                // LoadJoinOnlyScoresForFile never loads features and OverlayFirstPassSidecar
                // writes only scores / q-values, so Features is already null on every entry.
                // It is not free to run anyway - FirstPassFdrTask guards the identical loop
                // for the same reason, at "~88.9 M reference writes at 163 files, each
                // tripping the GC write barrier and dirtying the card table over the whole
                // survivor buffer" - and here it would run immediately before Stage 7 adopts
                // that buffer and the stage7-inherited probe tries to measure it.
                if (!streamed)
                {
                    foreach (var kvp in perFileEntries)
                    {
                        foreach (var entry in kvp.Value)
                            entry.Features = null;
                    }
                }
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
        /// Run one of the two reconciled-bundle hydrates - the file-count-bounded
        /// <see cref="RescoreHydration.HydrateCompactedStreaming"/> or the resident batch
        /// <see cref="RescoreHydration.HydrateReconciliationOverlay"/> - under one
        /// graceful-failure policy, so a corrupt or mismatched boundary file fails the same
        /// actionable way whichever path the run took. Returns null with the error logged and
        /// <see cref="PipelineContext.ExitCode"/> set when the hydrate threw
        /// <see cref="InvalidDataException"/>; other exception types propagate uncaught,
        /// matching the original inline behavior.
        ///
        /// Returns the bundle rather than assigning <see cref="_rescoreInputs"/> itself so
        /// that the null test sits at the call site, immediately before the caller
        /// dereferences it - a bool-returning version left every caller dereferencing a field
        /// whose non-nullness could only be established inside this method.
        /// </summary>
        private static RescoreInputs HydrateRescoreBundleOrNull(
            Func<RescoreInputs> hydrate, PipelineContext ctx)
        {
            try
            {
                return hydrate();
            }
            catch (InvalidDataException ex)
            {
                ctx.LogError(string.Format(@"--input-scores hydration failed: {0}", ex.Message));
                ctx.ExitCode = 1;
                return null;
            }
        }

        /// <summary>
        /// True when EVERY <c>--input-scores</c> parquet has both its first-pass
        /// <c>.fdr_scores.bin</c> and its <c>.reconciliation.json</c> sidecar -- the
        /// signature of a reconciled 2nd-pass merge whose <see cref="HydrateRescoreBundleIfPresent"/>
        /// overlays q-values onto the resident stubs (FirstPassFDR skips Percolator there).
        /// <see cref="LoadJoinOnlyScores"/> reads this to keep the fat pool on that path,
        /// since the lean projection would drop the stubs the overlay needs.
        /// </summary>
        private static bool AllHaveReconSidecars(OspreyConfig config)
        {
            foreach (var parquetPath in config.InputScores)
            {
                string syntheticInput = RescoreHydration.SyntheticInputFromParquet(parquetPath);
                // Version-fenced like every other sidecar gate: a v3 file left by an older build
                // is present but unreadable, and answering "yes, all sidecars are here" off
                // File.Exists keeps the fat pool on a path whose overlay then cannot load it.
                if (!FdrScoresSidecar.IsCurrentFormat(FdrScoresSidecar.Pass1Path(syntheticInput),
                                                     FdrScoresSidecar.Pass.FirstPass)
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
        /// or when the projection path is off (OSPREY_FDR_PROJECTION=0 / non-Percolator
        /// FDR). The reconciled-input worker join is NO LONGER one of them (#4486): it
        /// takes the streaming compacted hydrate, one file's pool resident at a time.
        /// OSPREY_PASS2_QVALUE=transfer no longer
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
        /// FirstPassFdrTask join-gate that already dropped it), folding each pre-compaction row into
        /// the reduced report rather than holding the whole-run pool resident -- which peaked
        /// ~100 GB on an 82-file mdiag run. The reductions are order-independent, so the streamed
        /// report is byte-identical to the resident build.
        /// </summary>
        private static bool NeedsResidentPool(OspreyConfig config)
        {
            return NeedsResidentPool(config, OspreyEnvironment.UseFdrProjection);
        }

        /// <summary>
        /// Pure core of <see cref="NeedsResidentPool(OspreyConfig)"/> (the env static is passed
        /// in so it is unit testable), and the single definition of the trigger set.
        /// <c>OSPREY_PASS2_QVALUE=transfer</c> is NOT a trigger and must not become one again:
        /// #4438 removed it when the per-run-only redesign dropped the full pre-compaction
        /// score-&gt;q table, and a #4446 merge artifact silently restored it, re-breaking
        /// 82-file transfer runs on <see cref="GuardResidentPool"/> with the memory bounding
        /// #4438 had shipped. Deliberately reads no <see cref="OspreyEnvironment"/> state other
        /// than the passed-in projection switch: the trigger set is then enumerable, and
        /// <c>ResidentPoolGuardTest</c> pins it. Note the test cannot by itself catch a
        /// re-added env read (the var is unset in a test process), so a pass-2 mode arriving
        /// in this list is a review-caught error - it contradicts #4438's invariant that
        /// transfer resolves run q per file, from data already on disk.
        /// </summary>
        internal static bool NeedsResidentPool(OspreyConfig config, bool useFdrProjection)
        {
            // ExpectReconciledInput (--task SecondPassFDR) was the first entry here and is
            // GONE (#4486). It never named a consumer: FirstPassFdrTask is excluded on that
            // node so nothing trains, and every pass-2 consumer streams from disk one file at
            // a time - the frozen transfer-compete / protein-compact competition off each
            // file's .1st-pass.fdr_scores.bin scalars, the feature reload off each file's
            // reconciled parquet. It forced the fat load whose features
            // HydrateRescoreBundleIfPresent then nulls unread, so the node paid ~800 MB per
            // file to throw it away, on top of holding every file's pre-compaction stubs.
            return !useFdrProjection ||
                   !config.FdrMethod.UsesPercolatorFramework() ||
                   (!string.IsNullOrEmpty(config.OutputFdrBench) && config.FdrBenchPass == 1);
        }

        /// <summary>
        /// Whether the <c>--input-scores</c> load may take the LEAN counts-only
        /// <see cref="FdrProjection"/> path, which streams 32 B rows and adds an EMPTY
        /// <see cref="FdrEntry"/> list per file. Two consumers need real stubs and so exclude it:
        /// a reconciled bundle (<paramref name="hasReconSidecars"/>), whose overlay reads them,
        /// and the reconciled-input merge, whose Stage 7 IS the consumer of the entry lists.
        ///
        /// <para>The <c>ExpectReconciledInput</c> term looks redundant with
        /// <paramref name="hasReconSidecars"/> - a <c>--task SecondPassFDR</c> node normally has
        /// both sidecars per input - and it is not. <see cref="AllHaveReconSidecars"/> is false if
        /// even ONE input is missing a <c>.reconciliation.json</c>, e.g. a partially copied HPC
        /// chain. Before #4486 that case was covered accidentally, because
        /// <see cref="NeedsResidentPool(OspreyConfig, bool)"/> returned true for the merge and
        /// suppressed the lean path; taking <c>ExpectReconciledInput</c> out of it made the lean
        /// newly reachable there, and it would have handed Stage 7 empty per-file lists - a
        /// near-empty <c>.blib</c>, written with no error. Stated explicitly here so the
        /// exclusion survives on its own reasoning rather than as a side effect of another
        /// predicate.</para>
        /// </summary>
        internal static bool CanUseLeanProjection(
            OspreyConfig config, bool hasReconSidecars, bool useFdrProjection)
        {
            return !NeedsResidentPool(config, useFdrProjection)
                   && !hasReconSidecars
                   && !config.ExpectReconciledInput;
        }

        /// <summary>
        /// Fail fast when a run would build the RESIDENT first-pass pool -- an O(files) memory
        /// path (the fat <see cref="FdrEntry"/> stub buffer, and the <c>FirstPassFdrTask.Rehydrate</c>
        /// pre-compaction load it feeds) that does not scale to large file counts. Unless the
        /// operator named THIS path via <c>OSPREY_ALLOW_UNFIXED_RESIDENT</c>, throw with the token
        /// named so the failure is actionable rather than an opaque OOM at scale. Triggers: the
        /// HPC reconciled-input merge (#4486), <c>--fdrbench-pass 1</c> (#4507), a non-Percolator
        /// FdrMethod, and <c>OSPREY_FDR_PROJECTION=0</c>, which requests the legacy resident
        /// implementation outright and so must be named like any other.
        /// </summary>
        private static void GuardResidentPool(OspreyConfig config, bool needsResidentPool)
        {
            string error = ResidentPoolGuardError(config, needsResidentPool,
                OspreyEnvironment.AllowUnfixedResident, OspreyEnvironment.UseFdrProjection);
            if (error != null)
                throw new InvalidOperationException(error);
        }

        /// <summary>
        /// Pure core of <see cref="GuardResidentPool"/> (env statics passed in so it is unit
        /// testable): returns the actionable error message when the run would take the resident
        /// first-pass pool, or <c>null</c> when the pool is not needed or this exact path was
        /// named. <paramref name="useFdrProjection"/> == false is the <c>OSPREY_FDR_PROJECTION=0</c>
        /// A/B-oracle switch, which is no longer an automatic exemption: it is its own token, so
        /// forcing the legacy implementation is stated rather than inferred.
        ///
        /// <para>The allowance is a TOKEN, not a boolean, and it must match the path this run
        /// actually takes. A resident path with no token in <see cref="ResidentPaths.KNOWN_UNFIXED"/>
        /// is refused unconditionally - no environment variable can admit it - because reaching
        /// here off the known list means something we believed bounded became resident again.
        /// That is the case the former blanket <c>OSPREY_ALLOW_UNBOUNDED_MEMORY=1</c> could not
        /// distinguish, and is how transfer regressed unnoticed.</para>
        /// </summary>
        internal static string ResidentPoolGuardError(
            OspreyConfig config, bool needsResidentPool, string allowUnfixedResident,
            bool useFdrProjection)
        {
            if (!needsResidentPool)
                return null;
            string trigger = ResidentPoolTrigger(config, useFdrProjection);
            // Membership in the committed list is what makes it the high-water mark rather than
            // documentation: a token the trigger chain invents but the list does not carry is
            // refused here, so re-admitting a path really does require editing the list (and
            // failing its pinning test), not just adding a branch above.
            if (trigger != null && !ResidentPaths.KNOWN_UNFIXED.Contains(trigger))
                trigger = null;
            if (trigger == null)
            {
                return
                    @"This configuration requires the RESIDENT first-pass pool, which holds every " +
                    @"entry in memory and grows O(files), but it is not one of the paths known to " +
                    @"be unfixed. Something that was streamed is resident again - fix that rather " +
                    @"than allowing it. OSPREY_ALLOW_UNFIXED_RESIDENT cannot admit this path.";
            }
            // Membership, not equality: the setting may name SEVERAL paths, because a run can
            // legitimately trip more than one at once. Case-insensitive, matching how the rest of
            // the CLI parses tokens (ParseFdrBenchPass): the error names the exact token to set,
            // so rejecting it for capitalization would read as the guard ignoring what the
            // operator just did.
            if (OspreyEnvironment.NamesResidentPath(allowUnfixedResident, trigger))
                return null;
            // Name the offending value when one was supplied, so a typo or a shell-quoted token
            // does not produce the identical message the unset case produces.
            string supplied = string.IsNullOrEmpty(allowUnfixedResident)
                ? string.Empty
                : string.Format(@" OSPREY_ALLOW_UNFIXED_RESIDENT is currently '{0}', which does " +
                                @"not name this path.", allowUnfixedResident);
            return string.Format(
                @"This run takes the '{0}' RESIDENT first-pass pool path, which holds every entry " +
                @"in memory and grows O(files) - it does not scale to large file counts and can " +
                @"exhaust memory. Set OSPREY_ALLOW_UNFIXED_RESIDENT={0} to accept it and proceed " +
                @"(intended for local testing / small runs); otherwise this path is unavailable.{1}",
                trigger, supplied);
        }

        /// <summary>
        /// The same refusal, one stage later: the guard above stops at the compaction line, and
        /// the POST-compaction survivor handoff is O(files) too - 88.9 M entries / 28 GB at 163
        /// files, live for the whole Stage 6 rescore (issue #4526). Stage 6 streams that buffer
        /// by default; <c>OSPREY_STAGE6_STREAM_SURVIVORS=0</c> restores the resident handoff as
        /// the A/B byte-identity oracle, and like every other resident path it has to be NAMED
        /// to be available. Returns the actionable error, or <c>null</c> when the run streams.
        ///
        /// <para><paramref name="streamingAvailable"/> is false when this run could not stream
        /// whatever the flag says - the legacy resident path never computes the passing base_id
        /// set the per-file loader needs. Those runs are exempt HERE because they are already
        /// resident for a reason with its own token
        /// (<see cref="ResidentPaths.PROJECTION_OFF"/> above all), so demanding a second token
        /// would mean two variables for one decision. The example this clause used to give -
        /// the plain <c>--task SecondPassFDR</c> run, covered by the former hpc-merge token -
        /// is gone with #4486: that node streams its reconciled-input load, so it needs no
        /// token at all and never reaches this exemption as a resident run.</para>
        ///
        /// <para>The exemption used to leave a gap it could not see: a lean straight-through
        /// resume needs no first-pass token at all since #4505, so "already resident under
        /// another token" was false for it, yet it took the handoff anyway because only a
        /// computed Stage 5 built the loader. #4536 closed that by giving the rehydrate its own
        /// loader, so a resume now reaches this guard with streaming AVAILABLE and is covered by
        /// the same call Run makes - which is what retired the interim resume-side guard and its
        /// dedicated token.</para>
        /// </summary>
        internal static string Stage6ResidentHandoffGuardError(
            bool streamingAvailable, bool streamingEnabled, string allowUnfixedResident)
        {
            if (!streamingAvailable || streamingEnabled)
                return null;
            if (OspreyEnvironment.NamesResidentPath(
                    allowUnfixedResident, ResidentPaths.COMPACTED_ENTRIES_BUFFER))
            {
                return null;
            }
            string supplied = string.IsNullOrEmpty(allowUnfixedResident)
                ? string.Empty
                : string.Format(@" OSPREY_ALLOW_UNFIXED_RESIDENT is currently '{0}', which does " +
                                @"not name this path.", allowUnfixedResident);
            return string.Format(
                @"OSPREY_STAGE6_STREAM_SURVIVORS=0 takes the '{0}' RESIDENT path, which holds " +
                @"every file's post-compaction survivors in memory across the whole Stage 6 " +
                @"rescore and grows O(files) - 28 GB at 163 files. Set " +
                @"OSPREY_ALLOW_UNFIXED_RESIDENT={0} to accept it and proceed (it is the A/B " +
                @"byte-identity oracle for the streamed default); otherwise this path is " +
                @"unavailable.{1}",
                ResidentPaths.COMPACTED_ENTRIES_BUFFER, supplied);
        }

        /// <summary>
        /// The <see cref="ResidentPaths"/> token for the known-unfixed resident path this config
        /// takes, or <c>null</c> when it needs the resident pool for a reason NOT on that list -
        /// which the caller refuses outright, since reaching that state means something we
        /// believed bounded is resident again.
        ///
        /// <para>Order here is BEHAVIORAL, unlike <see cref="PreCompactionPoolReason"/> where only
        /// the reported string depends on it: this decides which token the operator must set, so
        /// a config matching two triggers is attributable to exactly one.
        /// <c>OSPREY_FDR_PROJECTION=0</c> is checked FIRST and deliberately outranks the
        /// config-driven reasons, because it selects the legacy implementation for the WHOLE run
        /// and is therefore the honest description even when another trigger also applies.</para>
        ///
        /// <para><c>--model-diagnostics</c> is deliberately NOT a branch here, and must not
        /// become one again. It was, for the full-resume case only (#4505), and the arming
        /// condition had to be passed in rather than read off <paramref name="config"/> so that
        /// testing <c>config.ModelDiagnostics</c> alone could not make mdiag an unconditional
        /// catch-all absorbing any FUTURE arming condition - handing it a token CI exports.
        /// FirstPassFDR's rehydrate now streams that report, so the flag arms nothing.</para>
        /// </summary>
        private static string ResidentPoolTrigger(OspreyConfig config, bool useFdrProjection)
        {
            if (!useFdrProjection)
                return ResidentPaths.PROJECTION_OFF;
            // ExpectReconciledInput -> HPC_MERGE was here and is GONE with the token (#4486):
            // --task SecondPassFDR now streams its load, so it never reaches this method at
            // all. See NeedsResidentPool for why nothing on that node reads the pool.
            if (!config.FdrMethod.UsesPercolatorFramework())
                return ResidentPaths.NON_PERCOLATOR_FDR;
            if (!string.IsNullOrEmpty(config.OutputFdrBench) && config.FdrBenchPass == 1)
                return ResidentPaths.FDRBENCH_PASS1;
            return null;
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

            // Per-candidate rank-term capture (OSPREY_PICK_DUMP_CANDIDATES): attach a
            // thread-safe collector the scorer appends to under the per-window Parallel.For.
            // Flushed once below, after scoring, to a per-file TSV. Null (zero overhead) when
            // the flag is unset.
            if (OspreyEnvironment.PickDumpCandidates)
                context.PickDump = new PickCandidateDump();

            // Load the per-file spectra as a STREAMING index over the .spectra.bin cache,
            // never materializing the full ~6 GB MS2 List<Spectrum>: Stages 1-4 (calibration
            // + scoring) stream each isolation window on demand. Segment 1/4 (read): the
            // mzML/cache read reporter inside EnsureSpectraCache feeds this file's first
            // progress slice under --parallel-files.
            MultiProgressReporter.Current?.BeginSegment();
            var swParse = Stopwatch.StartNew();
            SpectraWindowIndex windowIndex = ScoringTaskShared.EnsureSpectraCache(
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
            // FirstPassFdrTask assembles these into ModelDiagnosticsData.Cal. Keyed by file
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

            // Per-candidate rank-term dump (OSPREY_PICK_DUMP_CANDIDATES): the parallel per-window
            // scoring above accumulated one row per CWT candidate into context.PickDump; write it
            // out now, once, next to the other per-file artifacts. No-op when the flag is unset.
            if (context.PickDump != null)
            {
                string pickPath = Path.Combine(
                    ArtifactPaths.ResolveOutputDir(inputFile),
                    fileName + @".pick_candidates.tsv");
                context.PickDump.Flush(pickPath);
                ctx.LogInfo(string.Format(@"Wrote per-candidate pick dump to {0}", pickPath));
            }

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
                // once for FirstPassFDR, and these arrays dominate. Features is reloaded before
                // first-pass Percolator (FirstPassFdrTask); CWT / fragments / ref-XIC are
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
                        // SecondPassFDR node with no mzML can rehydrate the gap-fill m/z
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
        /// SecondPassFDR node (which has no mzML) can rehydrate the gap-fill m/z filter's
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
