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
using System.IO;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey
{
    /// <summary>
    /// Command-line entry point for Osprey.
    /// Parses CLI arguments and launches the analysis pipeline.
    /// Port of osprey/src/main.rs.
    /// </summary>
    static class Program
    {
        // The logical osprey version (stamped into the blib + score caches and
        // used for cache-compat) is OspreyVersion.Current in Osprey.Core,
        // derived from the build version (Skyline YEAR.ORDINAL.BRANCH.DOY scheme).
        //
        // Known limitation: the --decoys-in-library reconciliation path (pairing
        // library-supplied decoys by base_id rather than stripping a DECOY_ prefix
        // in consensus-RT + reconciliation planning) is not yet ported. Reverse-
        // decoy mode (Stellar, DecoysInLibrary=false) is unaffected; datasets run
        // with --decoys-in-library are.

        // All user-visible output funnels through one CommandStatusWriter so the
        // --timestamp / --memstamp / --log-file options (added in later commits) apply
        // uniformly. Defaults to stderr; --version / --help stay on stdout.
        private static CommandStatusWriter _out = new CommandStatusWriter(Console.Error);

        static int Main(string[] args)
        {
            // Route OspreyDiagnostics dump messages through the same logging
            // channel as the rest of the pipeline so bisection logs appear
            // alongside normal output.
            OspreyDiagnosticsLog.LogAction = LogInfo;

            if (args.Length == 0)
            {
                // No args is a usage error (exit 1), so the prompt goes to stderr (_out
                // wraps Console.Error); an explicit --help instead writes to stdout
                // (see OspreyCommandArgs.PrintUsage).
                OspreyCommandArgs.PrintUsage(null, _out);
                return 1;
            }

            // Tracks whether _out was swapped to a --log-file StreamWriter we must
            // flush and dispose (never dispose the shared Console.Error writer).
            bool loggingToFile = false;

            try
            {
                // Scan args for the HPC task selector up front so error
                // messages fire before --input-scores resolution. A single
                // `--task <Name>` runs exactly one pipeline task (HPC: one
                // node = one task) by setting the (NoJoin, StopAfterStage5,
                // ExpectReconciledInput) config flags the four tasks'
                // IsIncluded methods read. Default (no --task) runs the full
                // straight-through pipeline. Any unrecognized flag (including
                // the retired --no-join / --join-only / --join-at-pass) fails
                // fast in ParseArgs.
                string taskName = null;
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i];
                    if (a == "--task")
                    {
                        if (i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                        {
                            LogError("--task requires a task name (SpectraCache, PerFileScoring, FirstPassFDR, PerFileRescoring, or SecondPassFDR).");
                            return 1;
                        }
                        taskName = args[i + 1];
                        i++; // consume value
                    }
                    else if (a.StartsWith("--task=", StringComparison.Ordinal))
                    {
                        taskName = a.Substring("--task=".Length);
                    }
                }

                HpcTask? selectedTask = null;
                if (taskName != null)
                {
                    string taskErr = ResolveTask(taskName, out HpcTask resolved);
                    if (taskErr != null)
                    {
                        LogError(taskErr);
                        return 1;
                    }
                    selectedTask = resolved;
                }

                OspreyConfig config = ParseArgs(args);
                // --task selects one pipeline task; derive the membership flags
                // the tasks' IsIncluded methods read. ExpectReconciledInput also
                // arms the strict-reconciled-input gate (every --input-scores
                // parquet must carry osprey.reconciled = "true"). Mirrors Rust's
                // main.rs wiring. SelectedTask is kept so ValidateArgs can enforce
                // the task<->input-type contract and name the typed task.
                config.SelectedTask = selectedTask;
                config.NoJoin = selectedTask == HpcTask.PerFileScoring || selectedTask == HpcTask.PerFileRescore;
                config.StopAfterStage5 = selectedTask == HpcTask.FirstPassFdr;
                config.ExpectReconciledInput = selectedTask == HpcTask.SecondPassFdr;

                // Apply the output / cache directory overrides process-wide so
                // every per-file artifact path helper (scores parquet, spectra
                // cache, calibration JSON, FDR / reconciliation sidecars) writes
                // to the configured location. Null leaves the historical behavior
                // (each artifact in its input file's own directory).
                ArtifactPaths.OutputDir = config.OutputDir;
                ArtifactPaths.CacheDir = config.CacheDir;

                string err = ValidateArgs(config);
                if (err != null)
                {
                    LogError(err);
                    return 1;
                }

                // Apply per-line output decoration and optional log-file redirection now
                // that args have validated (an invalid command line stays on stderr and
                // creates no file). All logging funnels through _out (see Log* methods).
                _out.IsTimeStamped = config.IsTimeStamped;
                _out.IsMemStamped = config.IsMemStamped;
                if (!string.IsNullOrEmpty(config.LogFilePath))
                {
                    try
                    {
                        _out = new CommandStatusWriter(new StreamWriter(config.LogFilePath))
                        {
                            IsTimeStamped = config.IsTimeStamped,
                            IsMemStamped = config.IsMemStamped
                        };
                        loggingToFile = true;
                    }
                    catch (Exception ex)
                    {
                        LogError(string.Format("Failed to open log file {0}: {1}", config.LogFilePath, ex.Message));
                        return 1;
                    }
                }

                // Point the Core output seam at a stat-filtering wrapper over _out: below-exe
                // layers (FDR, IO) and LogInfo emit through the same CommandStatusWriter (stamps
                // + --log-file), with machine [COUNT]/[TIMING]/[STAGE-WALL] lines dropped unless
                // --perf-stats is set (perf tools pass it; default human log stays clean).
                OspreyOutput.PerfStats = config.PerfStats;
                OspreyOutput.Verbose = config.Verbose;
                OspreyOutput.Out = new StatFilteringTextWriter(_out);

                // Create the configured directories only after args validate, so
                // an invalid command line surfaces the validation message instead
                // of a Directory.CreateDirectory side effect / generic error.
                if (!string.IsNullOrEmpty(config.OutputDir))
                    Directory.CreateDirectory(config.OutputDir);
                if (!string.IsNullOrEmpty(config.CacheDir))
                    Directory.CreateDirectory(config.CacheDir);
                // Runs that consume --input-scores (FirstPassFDR, PerFileRescore,
                // SecondPassFDR, or the default full pipeline started from scores)
                // have no mzML inputs to validate and ignore --output handling
                // differently from per-file scoring.
                bool fromInputScores = config.InputScores != null && config.InputScores.Count > 0;

                // --task PerFileScoring ignores --output (it writes per-file
                // .scores.parquet, not a blib), but that is expected single-task /
                // HPC-worker behavior -- wrapper scripts routinely pass a placeholder
                // --output -- so it is NOT warned about. The settings block below
                // reports the real per-file parquet output for this task instead.

                // Validate input files exist on disk (skip when consuming
                // --input-scores, where there are no mzML inputs; --input-scores
                // paths were already validated by ResolveInputScores during parsing).
                if (!fromInputScores)
                {
                    foreach (string inputFile in config.InputFiles)
                    {
                        // A directory counts as present. Several vendor formats ARE
                        // directories (Agilent .d, Bruker .d, Waters .raw), so testing
                        // File.Exists alone rejected every one of them here, before any
                        // reader was consulted, on builds with and without the vendor
                        // reader. It also blocked reusing a raw-derived .spectra.bin,
                        // which must work on a build that cannot read the raw itself.
                        if (!File.Exists(inputFile) && !Directory.Exists(inputFile))
                        {
                            LogError(string.Format("Input file not found: {0}", inputFile));
                            return 1;
                        }
                    }
                }
                if (config.LibrarySource != null && !File.Exists(config.LibrarySource.Path))
                {
                    LogError(string.Format("Library file not found: {0}", config.LibrarySource.Path));
                    return 1;
                }

                // Log startup info
                LogInfo(string.Format("Osprey v{0}", OspreyVersion.DisplayVersion));
                LogInfo(string.Format("Command: {0}", string.Join(" ", args)));
                LogInfo(string.Format("Input files: {0}", config.InputFiles.Count));
                LogInfo(string.Format("Library: {0} ({1})",
                    config.LibrarySource?.Path ?? "(none)",
                    config.LibrarySource?.Format.ToString() ?? "?"));
                // A --task run executes one HPC stage rather than the full pipeline;
                // name it so the log says which single task ran (no --task = full
                // pipeline, no line).
                if (config.SelectedTask.HasValue)
                    LogInfo(string.Format("Task: {0} (single-task run)",
                        TaskCliName(config.SelectedTask.Value)));
                // --task PerFileScoring writes per-file .scores.parquet next to each
                // input file, mzML or vendor raw, not a blib - report the real output
                // rather than the ignored --output blib path. (PerFileRescoring still
                // writes --output.)
                if (config.SelectedTask == HpcTask.SpectraCache)
                    LogInfo("Output: per-file .spectra.bin (no scoring; --output and --library are not used)");
                else if (config.NoJoin && !fromInputScores)
                    LogInfo("Output: per-file .scores.parquet (next to each input file)");
                else
                    LogInfo(string.Format("Output: {0}", config.OutputBlib));
                LogInfo(string.Format("Resolution: {0}", config.ResolutionMode));
                LogInfo(string.Format("Fragment tolerance: {0} {1}",
                    config.FragmentTolerance.Tolerance,
                    config.FragmentTolerance.Unit == ToleranceUnit.Ppm ? "ppm" : "Th"));
                LogInfo(string.Format("Run FDR: {0:P1}", config.RunFdr));
                LogInfo(string.Format("Experiment FDR: {0:P1}", config.ExperimentFdr));
                // Always print which experiment-wide aggregation is in force, active or not.
                // Reported HERE and not from Stage 5 because FirstPassFdrTask.Run is skipped on
                // --task SecondPassFDR, on a Rehydrate, and on any warm resume - exactly the runs
                // whose q-values an operator is most likely to attribute to the wrong arm.
                LogInfo(OspreyEnvironment.DescribeExperimentAgg());
                if (OspreyEnvironment.ExperimentAggUnrecognized)
                {
                    LogWarning(string.Format(
                        "OSPREY_EXPERIMENT_AGG was set to an unrecognized value; using the default " +
                        "'{0}'. Recognized values: '{0}', or '{1}<N>' with N in [2, {2}] (e.g. '{1}2').",
                        OspreyEnvironment.EXPERIMENT_AGG_MAX,
                        OspreyEnvironment.EXPERIMENT_AGG_MEAN_BEST_PREFIX,
                        OspreyEnvironment.MEAN_BEST_N_MAX));
                }
                // Abort, do not fall back. A run that asked for a mode it did not get would
                // report q-values the caller never requested, under whatever output name the
                // caller chose - and 'percolator' was removed, so existing sweep scripts still
                // pass it. Checked here rather than at SecondPassFDR so it costs seconds
                // instead of a full Stage 1-5.
                if (OspreyEnvironment.Pass2QValueUnrecognized)
                {
                    LogError(string.Format(
                        "OSPREY_PASS2_QVALUE is not a recognized mode. Recognized: '{0}', '{1}', " +
                        "'{2}'. Unset it for the default ('{2}'). The 'percolator' mode was " +
                        "REMOVED: it retrained the 2nd-pass SVM on a compaction-depleted decoy " +
                        "pool, which reports anti-conservative q-values.",
                        OspreyEnvironment.PASS2_QVALUE_TRANSFER,
                        OspreyEnvironment.PASS2_QVALUE_TRANSFER_COMPETE,
                        OspreyEnvironment.PASS2_QVALUE_PROTEIN_COMPACT));
                    return 1;
                }
                // A token that names nothing admits nothing, so the run proceeds - but say so
                // (#4486). 'hpc-merge' was retired when --task SecondPassFDR started streaming
                // its reconciled-input load, making it the first previously-VALID token to
                // become invalid, and committed automation still passes it. Silence there is
                // the bad outcome: the operator believes they granted an allowance, and if the
                // run later needs a real one the guard says only "does not name this path",
                // which reads as a typo rather than a retirement. A warning, not an error -
                // unlike OSPREY_PASS2_QVALUE above, a stale allowance cannot change any
                // reported number, it can only fail to permit something.
                if (OspreyEnvironment.AllowUnfixedResidentUnrecognized)
                {
                    // Name the UNRECOGNIZED tokens, not the whole value: the flag is a comma
                    // separated list and NamesResidentPath tests each token independently, so
                    // 'projection-off,hpc-merge' still grants projection-off. Condemning the
                    // whole value would push the operator to rewrite or unset a variable whose
                    // valid half the run still needs, and the run then aborts on a guard the
                    // warning said was not engaged.
                    LogWarning(string.Format(
                        "OSPREY_ALLOW_UNFIXED_RESIDENT contains unrecognized token(s) that grant " +
                        "nothing: {0}. Recognized: {1}. ('hpc-merge' was retired - the " +
                        "--task SecondPassFDR reconciled-input load streams and needs no allowance.) " +
                        "Any recognized token in the same value is still honored.",
                        OspreyEnvironment.UnrecognizedResidentTokens,
                        string.Join(", ", ResidentPaths.KNOWN_UNFIXED)));
                }
                LogInfo(string.Format("Protein FDR: {0:P1}", config.EffectiveProteinFdr));
                LogInfo(string.Format("Threads: {0}", config.NThreads));
                LogInfo("");

                // Single entry point. The rescore worker (--task
                // PerFileRescore, with --input-scores) includes only
                // PerFileRescoreTask (OspreyTask.IsIncluded); PerFileScoring's
                // lazy-rehydrate (via ctx.Demand) populates the upstream state
                // from the boundary files on disk.
                var pipeline = new AnalysisPipeline();
                return pipeline.Run(config);
            }
            catch (Exception ex)
            {
                LogError(string.Format("Fatal error: {0}", ex.Message));
                return 1;
            }
            finally
            {
                // Flush and close the --log-file writer (never the shared Console.Error).
                if (loggingToFile)
                {
                    _out.Flush();
                    _out.Dispose();
                }
            }
        }

        /// <summary>
        /// Parse command-line arguments into an OspreyConfig. Thin facade over the
        /// declarative OspreyCommandArgs model (built on the PortableUtil framework). Kept
        /// here with the original signature so existing tests calling Program.ParseArgs work.
        /// </summary>
        internal static OspreyConfig ParseArgs(string[] args)
        {
            return OspreyCommandArgs.ParseArgs(args);
        }

        /// <summary>
        /// Resolve a <c>--task &lt;Name&gt;</c> selector (case-insensitive,
        /// matched against each task's stable <c>Name</c>) to its
        /// <see cref="HpcTask"/>. One node = one task on HPC. The caller derives
        /// the pipeline-membership flags (<c>NoJoin</c>, <c>StopAfterStage5</c>,
        /// <c>ExpectReconciledInput</c>) from the result and keeps the
        /// <see cref="HpcTask"/> on the config so <see cref="ValidateArgs"/> can
        /// enforce the task&#8596;input-type contract.
        ///
        /// Returns null on success, or an error message string for an unknown
        /// task name. Internal so Osprey.Test can exercise it.
        /// </summary>
        internal static string ResolveTask(string taskName, out HpcTask task)
        {
            if (string.Equals(taskName, "PerFileScoring", StringComparison.OrdinalIgnoreCase))
            {
                task = HpcTask.PerFileScoring;
                return null;
            }
            if (string.Equals(taskName, "FirstPassFDR", StringComparison.OrdinalIgnoreCase))
            {
                task = HpcTask.FirstPassFdr;
                return null;
            }
            if (string.Equals(taskName, "PerFileRescoring", StringComparison.OrdinalIgnoreCase))
            {
                task = HpcTask.PerFileRescore;
                return null;
            }
            if (string.Equals(taskName, "SecondPassFDR", StringComparison.OrdinalIgnoreCase))
            {
                task = HpcTask.SecondPassFdr;
                return null;
            }
            if (string.Equals(taskName, "SpectraCache", StringComparison.OrdinalIgnoreCase))
            {
                task = HpcTask.SpectraCache;
                return null;
            }
            task = default;
            return string.Format(
                "--task: unknown task '{0}'. Valid tasks: SpectraCache, PerFileScoring, FirstPassFDR, PerFileRescoring, SecondPassFDR.",
                taskName);
        }

        /// <summary>
        /// The canonical CLI <c>--task</c> token for an <see cref="HpcTask"/> - the
        /// inverse of <see cref="ResolveTask"/>, used to echo the selected task in the
        /// startup settings block. Not necessarily the spelling the operator typed:
        /// <see cref="ResolveTask"/> matches case-insensitively, and only the resolved
        /// enum value reaches this method, so <c>--task firstpassfdr</c> echoes as
        /// <c>FirstPassFDR</c>. The members now spell their own CLI token, so the only
        /// differences left are the FDR casing (<c>FirstPassFdr</c> vs the all-caps
        /// acronym the CLI takes) and PerFileRescore vs PerFileRescoring - which is why
        /// <c>task.ToString()</c> is still not a substitute for this switch.
        /// </summary>
        private static string TaskCliName(HpcTask task)
        {
            switch (task)
            {
                case HpcTask.PerFileScoring: return "PerFileScoring";
                case HpcTask.FirstPassFdr: return "FirstPassFDR";
                case HpcTask.PerFileRescore: return "PerFileRescoring";
                case HpcTask.SecondPassFdr: return "SecondPassFDR";
                case HpcTask.SpectraCache: return "SpectraCache";
                default: return task.ToString();
            }
        }

        /// <summary>
        /// Validate the parsed config against the selected
        /// <see cref="OspreyConfig.SelectedTask"/> (or the default full pipeline
        /// when none was given). When a <c>--task</c> is selected the task is
        /// authoritative: it dictates the input type, and the cross
        /// (e.g. <c>--task PerFileScoring --input-scores</c>) is rejected rather
        /// than silently dispatching the other task. Returns null on success or
        /// an error message string on failure. Does not log warnings (those stay
        /// in <see cref="Main"/>). Internal so Osprey.Test can exercise it.
        /// </summary>
        internal static string ValidateArgs(OspreyConfig config)
        {
            bool hasInputScores = config.InputScores != null && config.InputScores.Count > 0;
            bool hasInputFiles = config.InputFiles != null && config.InputFiles.Count > 0;

            // OSPREY_EXPERIMENT_AGG family, before any I/O. Checked here rather than at the
            // Stage-5 consuming site so a bad combination costs a second instead of the hours a
            // large run spends reaching FirstPassFDR, and so a warm resume - which skips
            // FirstPassFdrTask.Run entirely - is still checked.
            string aggErr = OspreyEnvironment.ValidateExperimentAggSettings(
                ExperimentAggFileCount(config, hasInputScores, hasInputFiles));
            if (aggErr != null)
                return aggErr;

            if (config.SelectedTask.HasValue)
            {
                switch (config.SelectedTask.Value)
                {
                    case HpcTask.SpectraCache:
                        // Stage 1 alone: inputs in, .spectra.bin out. Deliberately
                        // does NOT require --library: caching depends only on the
                        // input file, and demanding one would make staging a dataset
                        // wait on a library that is often chosen later.
                        if (hasInputScores)
                        {
                            return "--task SpectraCache takes -i <file>, not --input-scores " +
                                   "(it builds spectra caches from raw inputs, not from scores).";
                        }
                        if (!hasInputFiles)
                            return "--task SpectraCache requires --input <file...>.";
                        return null;

                    case HpcTask.PerFileScoring:
                        // Stage 1-4 worker: mzML in, per-file .scores.parquet out.
                        if (hasInputScores)
                            return "--task PerFileScoring takes -i <mzML>, not --input-scores " +
                                   "(did you mean --task PerFileRescoring?).";
                        if (!hasInputFiles)
                            return "--task PerFileScoring requires --input <mzML...>.";
                        if (config.LibrarySource == null)
                            return "--task PerFileScoring requires --library.";
                        return null;

                    case HpcTask.PerFileRescore:
                        // Stage 6 worker: --input-scores in, reconciled per-file out.
                        if (hasInputFiles)
                            return "--task PerFileRescoring takes --input-scores, not -i <mzML> " +
                                   "(mzML paths are derived from the parquet stems).";
                        if (!hasInputScores)
                            return "--task PerFileRescoring requires --input-scores <path...>.";
                        if (config.LibrarySource == null || string.IsNullOrEmpty(config.OutputBlib))
                            return "--task PerFileRescoring requires --library and --output.";
                        return null;

                    case HpcTask.FirstPassFdr:
                        if (hasInputFiles)
                            return "--task FirstPassFDR cannot be combined with --input. Use --input-scores instead.";
                        if (!hasInputScores)
                            return "--task FirstPassFDR requires --input-scores <path...>.";
                        if (config.LibrarySource == null || string.IsNullOrEmpty(config.OutputBlib))
                            return "--task FirstPassFDR requires --library and --output.";
                        // FirstPassFDR writes the Stage 5 → Stage 6 boundary file
                        // pair, only meaningful with 2+ siblings to reconcile
                        // against and reconciliation enabled. Reject early.
                        if (config.InputScores.Count < 2)
                            return string.Format(
                                "--task FirstPassFDR requires --input-scores with 2+ parquet files " +
                                "(got {0}). The Stage 5 → Stage 6 boundary file pair is only meaningful for " +
                                "multi-file fan-back-in.",
                                config.InputScores.Count);
                        if (!config.Reconciliation.Enabled)
                            return "--task FirstPassFDR requires Reconciliation.Enabled = true " +
                                   "(got false from config). The Stage 5 → Stage 6 boundary file pair is " +
                                   "only meaningful when reconciliation runs.";
                        return null;

                    case HpcTask.SecondPassFdr:
                        if (hasInputFiles)
                            return "--task SecondPassFDR cannot be combined with --input. Use --input-scores instead.";
                        if (!hasInputScores)
                            return "--task SecondPassFDR requires --input-scores <path...>.";
                        if (config.LibrarySource == null || string.IsNullOrEmpty(config.OutputBlib))
                            return "--task SecondPassFDR requires --library and --output.";
                        return null;
                }
            }

            // No --task: the full pipeline, started from either -i mzML or
            // --input-scores (PerFileScoring lazy-rehydrates the supplied scores).
            if (hasInputScores)
            {
                if (hasInputFiles)
                    return "--input-scores cannot be combined with --input. Use one or the other.";
                if (config.LibrarySource == null || string.IsNullOrEmpty(config.OutputBlib))
                    return "--input-scores requires --library and --output.";
                return null;
            }
            if (!hasInputFiles)
                return "No input files specified. Use -i <file1.mzML> [file2.mzML ...]";
            if (config.LibrarySource == null)
                return "No spectral library specified. Use -l <library.tsv>";
            if (string.IsNullOrEmpty(config.OutputBlib))
                return "No output path specified. Use -o <output.blib>";
            return null;
        }

        /// <summary>
        /// How many runs this invocation will aggregate across for the experiment-wide
        /// competition, or 0 when that is not a property of this invocation. The per-file HPC
        /// workers (SpectraCache / PerFileScoring / PerFileRescoring) each see ONE input and
        /// never compute an experiment-wide score, so reporting their input count would refuse
        /// every worker of a legitimate distributed mean(best-N) run.
        /// </summary>
        private static int ExperimentAggFileCount(
            OspreyConfig config, bool hasInputScores, bool hasInputFiles)
        {
            switch (config.SelectedTask)
            {
                case HpcTask.SpectraCache:
                case HpcTask.PerFileScoring:
                case HpcTask.PerFileRescore:
                    return 0;
            }
            if (hasInputScores)
                return config.InputScores.Count;
            return hasInputFiles ? config.InputFiles.Count : 0;
        }

        /// <summary>
        /// Expand --input-scores arguments: a single directory becomes the
        /// non-recursive list of *.scores.parquet files in it; explicit file
        /// paths are passed through unchanged. Throws if the directory is
        /// empty or any explicit path doesn't exist.
        ///
        /// Directory mode collects both the Stage 4 <c>*.scores.parquet</c> files
        /// and the Stage 6 <c>*.scores-reconciled.parquet</c> siblings, then
        /// dedupes per stem: for any stem that has both, only the reconciled file
        /// is returned (the authoritative later pass; the <c>--task SecondPassFDR</c>
        /// reconciled-input gate expects reconciled parquets). A stem with only an
        /// original is returned as-is. The two suffixes are unambiguous, so this
        /// never returns both files for one stem (see
        /// <see cref="ParquetScoreCache.ReconciledScoresParquetSuffix"/>).
        /// </summary>
        internal static List<string> ResolveInputScores(List<string> paths)
        {
            if (paths == null || paths.Count == 0)
                throw new ArgumentException("--input-scores requires at least one path.");

            if (paths.Count == 1 && Directory.Exists(paths[0]))
            {
                string dir = paths[0];
                // Glob *.parquet and classify by suffix in code rather than
                // relying on multi-dot search-pattern matching (which differs
                // across platforms). Keep only the two known scores suffixes.
                var originals = new List<string>();
                var reconciledSet = new HashSet<string>(StringComparer.Ordinal);
                foreach (string f in Directory.GetFiles(dir, "*.parquet", SearchOption.TopDirectoryOnly))
                {
                    if (ParquetScoreCache.IsReconciledScoresPath(f))
                        reconciledSet.Add(f);
                    else if (f.EndsWith(ParquetScoreCache.ScoresParquetSuffix, StringComparison.Ordinal))
                        originals.Add(f);
                }
                if (originals.Count == 0 && reconciledSet.Count == 0)
                    throw new ArgumentException(string.Format(
                        "No *.scores.parquet files found in --input-scores directory: {0}", dir));
                var result = new List<string>(reconciledSet);            // reconciled: authoritative
                foreach (string f in originals)
                    if (!reconciledSet.Contains(ParquetScoreCache.ReconciledPathFromScoresPath(f)))
                        result.Add(f);                                   // original with no reconciled sibling
                result.Sort(StringComparer.Ordinal); // Array.Sort OK: unique filenames, so the comparator never ties
                return result;
            }

            foreach (string p in paths)
            {
                if (!File.Exists(p))
                    throw new ArgumentException(string.Format("--input-scores path not found: {0}", p));
            }
            return paths;
        }

        internal static void LogInfo(string message)
        {
            OspreyOutput.Out.WriteLine(message);
        }

        internal static void LogWarning(string message)
        {
            // Through OspreyOutput.Out (not _out directly) so a warning emitted
            // while a file runs in a MultiProgressReporter per-file scope
            // (--parallel-files) lands in that file's buffered block, in context,
            // instead of interleaving with the live "[i] p%" aggregate line. Off
            // the parallel path OspreyOutput.Out is the same CommandStatusWriter
            // (wrapped for stat-filtering), so the output is unchanged.
            OspreyOutput.Out.WriteLine("[WARN] {0}", message);
        }

        internal static void LogError(string message)
        {
            // Errors go straight to the process writer (NOT the per-file buffer):
            // surface immediately rather than waiting for the file's block to flush
            // on completion, so a failing run reports the cause right away.
            _out.WriteLine("[ERROR] {0}", message);
        }
    }
}
