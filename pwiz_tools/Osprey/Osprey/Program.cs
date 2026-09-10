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
using System.Linq;
using System.Text;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;
using pwiz.Osprey.Tasks.ModelDiagnostics;

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
                // messages name the task before any other argument is parsed. A single
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
                            LogError("--task requires a task name (SpectraCache, PerFileScoring, FirstPassFDR, " +
                                     "PerFileRescoring, SecondPassFDR, or ModelDiagnostics).");
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
                // arms the strict-reconciled-input gate (every run's reconciled
                // parquet must carry osprey.reconciled = "true"). All three are
                // derived from --task and from nothing else, which is what let the
                // input KIND retire: it was the OTHER seam saying the same thing.
                config.SelectedTask = selectedTask;
                // --task ModelDiagnostics IS the request for the report; without the flag the
                // run would recompute the pass-2 view and write nothing, a silent no-op.
                if (selectedTask == HpcTask.ModelDiagnostics)
                    config.ModelDiagnostics = true;
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
                // --task PerFileScoring ignores --output (it writes per-file
                // .scores.parquet, not a blib), but that is expected single-task /
                // HPC-worker behavior -- wrapper scripts routinely pass a placeholder
                // --output -- so it is NOT warned about. The settings block below
                // reports the real per-file parquet output for this task instead.

                // Validate input files exist on disk. EVERY run reaches this now: a task
                // that starts after Stage 4 used to be handed parquets and skipped the
                // check entirely, and it is handed the same data-file names as every other
                // task instead.
                int cacheOnlyInputs = 0;
                int artifactOnlyInputs = 0;
                foreach (string inputFile in config.InputFiles)
                {
                    // A directory counts as present. Several vendor formats ARE
                    // directories (Agilent .d, Bruker .d, Waters .raw), so testing
                    // File.Exists alone rejected every one of them here, before any
                    // reader was consulted, on builds with and without the vendor
                    // reader. It also blocked reusing a raw-derived .spectra.bin,
                    // which must work on a build that cannot read the raw itself.
                    if (File.Exists(inputFile) || Directory.Exists(inputFile))
                        continue;
                    // An absent source is fine once its cache is built: Stage 1 is
                    // the only stage that reads a source, and SpectraCache already
                    // treats a missing one as "trust the cache". That makes
                    // delete-the-sources-after-caching a supported way to halve the
                    // disk a large cohort needs.
                    if (File.Exists(SpectraCache.GetCachePath(inputFile)))
                    {
                        cacheOnlyInputs++;
                        continue;
                    }
                    // ...and so is an absent source with no cache, once its SCORES exist.
                    // A join node is shipped parquets and sidecars and nothing else - that
                    // is the whole point of the split - so demanding the data file back
                    // would refuse the configuration the HPC chain is built on. This is
                    // what --input-scores used to say by naming a different input KIND;
                    // said here it is one input kind and one question about it.
                    // ...and only for a task that STARTS AFTER Stage 4. A scores parquet
                    // stands an input in because such a task never opens the data file; it
                    // stands in for nothing at all for --task SpectraCache or PerFileScoring,
                    // whose whole product is decoded FROM that file. Without this term a
                    // mistyped or moved input on those tasks proceeds on a leftover parquet
                    // and logs that the run will be read from its scores parquet, "which is
                    // what a task after Stage 4 needs" - false for exactly the two tasks that
                    // could reach it. The old `if (!fromInputScores)` wrapper could not reach
                    // them structurally; nothing re-established that scoping when it went.
                    //
                    // EITHER parquet, named. WHICH one a task reads is that task's question
                    // (ScoringTaskShared.ReadsReconciledScores) and this runs before dispatch:
                    // a FirstPassFDR node is shipped <stem>.scores.parquet, a SecondPassFDR
                    // node only <stem>.scores-reconciled.parquet.
                    if (ScoringTaskShared.StartsAfterPerFileScoring(config) &&
                        (File.Exists(ParquetScoreCache.GetScoresPath(inputFile)) ||
                         File.Exists(ParquetScoreCache.GetReconciledScoresPath(inputFile))))
                    {
                        artifactOnlyInputs++;
                        continue;
                    }
                    LogError(string.Format(
                        "Input file not found, and it has neither a spectra cache nor a scores " +
                        "parquet to stand in for it: {0}", inputFile));
                    return 1;
                }
                // Announced, not silent: a run whose sources are gone cannot rebuild a
                // cache that turns out to be wrong, so the log is the only provenance.
                if (cacheOnlyInputs > 0)
                {
                    LogInfo(string.Format(
                        "{0} of {1} input(s) are absent but have a spectra cache; reading those from the cache.",
                        cacheOnlyInputs, config.InputFiles.Count));
                }
                if (artifactOnlyInputs > 0)
                {
                    LogInfo(string.Format(
                        "{0} of {1} input(s) are absent and have no spectra cache; reading those from " +
                        "their scores parquet, which is what a task after Stage 4 needs.",
                        artifactOnlyInputs, config.InputFiles.Count));
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
                else if (config.NoJoin && config.SelectedTask == HpcTask.PerFileScoring)
                    LogInfo("Output: per-file .scores.parquet (next to each input file)");
                else if (config.DiagnosticsOnly)
                {
                    // --task ModelDiagnostics regenerates the report for a COMPLETED run and
                    // declares no other output; naming the blib here reads as "the blib is being
                    // rebuilt", and an operator who then sees its timestamp unchanged concludes
                    // the run failed. Same reason SpectraCache has its own branch above.
                    LogInfo(string.Format("Output: {0} (report only; no other artifact is written)",
                        ModelDiagnosticsReport.ReportPath(config)));
                }
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
                        "OSPREY_PASS2_QVALUE is not a recognized mode. Recognized: '{0}', '{1}'. " +
                        "Unset it for the default ('{1}'). 'percolator' was REMOVED: it retrained " +
                        "the 2nd-pass SVM on a compaction-depleted decoy pool, which reports " +
                        "anti-conservative q-values. 'transfer-compete' was REMOVED for a related " +
                        "reason: it selected survivors by TARGET per-run q and admitted decoys only " +
                        "by pairing, stripping decoys that won the 1st-pass competition, so its q " +
                        "improved with no added evidence - 1.96% true FDP at a nominal 1% on 82-file " +
                        "SEA-AD, against 1.53% for the default, and with FEWER ids.",
                        OspreyEnvironment.PASS2_QVALUE_TRANSFER,
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

                // --task ModelDiagnostics is a RENDER over completed analysis state, not a run.
                // Settled before the pipeline is built, because the whole point is that most
                // invocations never build one.
                if (config.DiagnosticsOnly)
                {
                    int diagnosticsExit = RunModelDiagnosticsTask(config);
                    if (diagnosticsExit >= 0)
                        return diagnosticsExit;
                }

                // Single entry point. The rescore worker (--task
                // PerFileRescoring) includes only
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
        /// <c>--task ModelDiagnostics</c>: produce the report from COMPLETED analysis state and
        /// never re-run the analysis. Returns the process exit code when the task is finished,
        /// or -1 to fall through to the pipeline when a diagnostics product still has to be
        /// folded.
        ///
        /// <para>The three states are the developer's contract. No first-pass state is an ERROR
        /// rather than a partial page; every product present is a re-render, in seconds; and a
        /// missing product is FOLDED by the pass that owns it. It used to REFUSE that third
        /// case and name the command the operator should run instead, which made the report an
        /// output only its producing phase could make - the thing P16 forbids.</para>
        ///
        /// <para>Falling through is not the old behavior returning. The task used to reach
        /// <see cref="AnalysisPipeline"/> and execute Stages 1-7 with every write suppressed by
        /// <see cref="OspreyConfig.DiagnosticsOnly"/>; suppressing the WRITES does not suppress
        /// the WORK, so asking a 446-run analysis to describe itself cost as much as running it
        /// and met the same memory wall. What changed is the other end: both FDR tasks now
        /// recognise "the diagnostics product is my only outstanding output" and fold it from
        /// their own completed artifacts, so the pipeline this falls into runs two bounded
        /// folds and skips everything else on its validity stamps. That is P15's ordinary
        /// resume applied to the diagnostics outputs, which is what P16 says this should have
        /// been all along - not a special mode, and not a special task.</para>
        /// </summary>
        private static int RunModelDiagnosticsTask(OspreyConfig config)
        {
            // Nothing to describe. An ERROR rather than an empty page, and the doc-00 precedent
            // for a missing relay input: fail with the reason, do not continue into a wrong
            // answer that looks like a right one.
            if (!ModelDiagnosticsReport.HasCompletedFirstPass(config))
            {
                LogError("--task ModelDiagnostics: no completed first-pass FDR state to " +
                         "describe (no analysis-wide 1st-pass experiment sidecar beside the " +
                         "output). Run the analysis at least as far as FirstPassFDR first.");
                return 1;
            }
            // Everything this analysis can have is on disk: a pure render, seconds, no pipeline.
            if (ModelDiagnosticsReport.AllProductsCurrent(config))
                return ModelDiagnosticsReport.TryRenderFromProducts(config, LogInfo) ? 0 : 1;

            // A product is outstanding. Say so before the pipeline banner, because the next
            // thing the log shows is task machinery and an operator needs to know it is a fold
            // rather than the re-analysis this task used to refuse to start.
            LogInfo("--task ModelDiagnostics: a diagnostics product is missing for this " +
                    "analysis; folding it from the completed artifacts. No analysis is re-run - " +
                    "each pass produces its own report from its own sidecars, and every other " +
                    "output is left as it stands.");
            return -1;
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
            if (string.Equals(taskName, "ModelDiagnostics", StringComparison.OrdinalIgnoreCase))
            {
                // The selector IS the request for the report, so it implies the flag rather than
                // requiring both. Without --model-diagnostics the task would run the pass-2
                // compute and write nothing at all, which reads as a silent no-op.
                task = HpcTask.ModelDiagnostics;
                return null;
            }
            task = default;
            return string.Format(
                "--task: unknown task '{0}'. Valid tasks: SpectraCache, PerFileScoring, FirstPassFDR, PerFileRescoring, SecondPassFDR, ModelDiagnostics.",
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
                case HpcTask.ModelDiagnostics: return "ModelDiagnostics";
                default: return task.ToString();
            }
        }

        /// <summary>
        /// Validate the parsed config against the selected
        /// <see cref="OspreyConfig.SelectedTask"/> (or the default full pipeline
        /// when none was given). Every task takes the SAME input kind now - the data
        /// files, named with <c>-i</c> or <c>--input-list</c> - so what is validated is
        /// presence and count, not kind. The cross this used to reject
        /// (<c>--task PerFileScoring --input-scores</c>) cannot be expressed any more,
        /// which is the point of retiring the second seam rather than teaching a third
        /// predicate about it. Returns null on success or an error message string on
        /// failure. Does not log warnings (those stay in <see cref="Main"/>). Internal so
        /// Osprey.Test can exercise it.
        /// </summary>
        /// <summary>
        /// An error naming every input stem that appears more than once, with the paths that
        /// collide, or null when all stems are distinct. Ordinal comparison, matching the
        /// per-run maps this protects.
        /// </summary>
        private static string DuplicateInputStemError(IReadOnlyList<string> inputFiles)
        {
            var byStem = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string input in inputFiles)
            {
                string stem = Path.GetFileNameWithoutExtension(input) ?? string.Empty;
                if (!byStem.TryGetValue(stem, out var paths))
                {
                    paths = new List<string>();
                    byStem[stem] = paths;
                }
                paths.Add(input);
            }
            var collisions = byStem.Where(kv => kv.Value.Count > 1).ToList();
            if (collisions.Count == 0)
                return null;
            var sb = new StringBuilder();
            sb.AppendFormat(
                "{0} input stem(s) appear more than once. Every per-run artifact is named " +
                "<stem>.<suffix>, so runs sharing a stem cannot be told apart and would " +
                "overwrite each other's parquets and sidecars. Rename or stage them so each " +
                "run has a distinct file name:", collisions.Count);
            foreach (var kv in collisions)
                sb.AppendFormat("\n  '{0}': {1}", kv.Key, string.Join(", ", kv.Value));
            return sb.ToString();
        }

        internal static string ValidateArgs(OspreyConfig config)
        {
            bool hasInputFiles = config.InputFiles != null && config.InputFiles.Count > 0;

            // OSPREY_EXPERIMENT_AGG family, before any I/O. Checked here rather than at the
            // Stage-5 consuming site so a bad combination costs a second instead of the hours a
            // large run spends reaching FirstPassFDR, and so a warm resume - which skips
            // FirstPassFdrTask.Run entirely - is still checked.
            string aggErr = OspreyEnvironment.ValidateExperimentAggSettings(
                ExperimentAggFileCount(config, hasInputFiles));
            if (aggErr != null)
                return aggErr;

            // Every run is keyed on its input STEM - the per-file artifacts are
            // <stem>.<suffix>, and every per-run map in the pipeline is keyed the same way -
            // so two inputs sharing a stem are two runs the pipeline cannot tell apart. It is
            // not exotic: --input-list makes it routine at cohort scale, where the same
            // acquisition name recurs under different directories.
            //
            // Refused here rather than surviving to be discovered downstream, where it takes
            // two shapes and neither says what happened. Without --output-dir the join appends
            // two rows under one key while the parquet map keeps only the second, and
            // CurrentReconciledPaths dies with "An item with the same key has already been
            // added" mid-Stage-6/7. WITH --output-dir it is worse and silent: both stems
            // resolve into the same directory, so the two runs share one .scores.parquet and
            // one .scores-reconciled.parquet, each overwriting the other, with no error at all.
            // The retired --input-scores form made stems unique by construction.
            if (hasInputFiles)
            {
                string dupErr = DuplicateInputStemError(config.InputFiles);
                if (dupErr != null)
                    return dupErr;
            }

            if (config.SelectedTask.HasValue)
            {
                switch (config.SelectedTask.Value)
                {
                    case HpcTask.SpectraCache:
                        // Stage 1 alone: inputs in, .spectra.bin out. Deliberately
                        // does NOT require --library: caching depends only on the
                        // input file, and demanding one would make staging a dataset
                        // wait on a library that is often chosen later.
                        if (!hasInputFiles)
                            return "--task SpectraCache requires --input <file...>.";
                        return null;

                    case HpcTask.PerFileScoring:
                        // Stage 1-4 worker: mzML in, per-file .scores.parquet out.
                        if (!hasInputFiles)
                            return "--task PerFileScoring requires --input <mzML...>.";
                        if (config.LibrarySource == null)
                            return "--task PerFileScoring requires --library.";
                        return null;

                    case HpcTask.PerFileRescore:
                        // Stage 6 worker: one run's scores in, its reconciled parquet out.
                        // Named by its DATA file like every other task; the parquet and
                        // sidecars are derived from the stem, and the data file itself need
                        // not exist (Main's input check accepts a run whose scores are on
                        // disk).
                        if (!hasInputFiles)
                            return "--task PerFileRescoring requires --input <file...>.";
                        if (config.LibrarySource == null || string.IsNullOrEmpty(config.OutputBlib))
                            return "--task PerFileRescoring requires --library and --output.";
                        return null;

                    case HpcTask.FirstPassFdr:
                        if (!hasInputFiles)
                            return "--task FirstPassFDR requires --input <file...>.";
                        if (config.LibrarySource == null || string.IsNullOrEmpty(config.OutputBlib))
                            return "--task FirstPassFDR requires --library and --output.";
                        // FirstPassFDR writes the Stage 5 → Stage 6 boundary file
                        // pair, only meaningful with 2+ siblings to reconcile
                        // against and reconciliation enabled. Reject early.
                        if (config.InputFiles.Count < 2)
                            return string.Format(
                                "--task FirstPassFDR requires --input with 2+ files " +
                                "(got {0}). The Stage 5 -> Stage 6 boundary file pair is only meaningful for " +
                                "multi-file fan-back-in.",
                                config.InputFiles.Count);
                        if (!config.Reconciliation.Enabled)
                            return "--task FirstPassFDR requires Reconciliation.Enabled = true " +
                                   "(got false from config). The Stage 5 → Stage 6 boundary file pair is " +
                                   "only meaningful when reconciliation runs.";
                        return null;

                    case HpcTask.SecondPassFdr:
                        if (!hasInputFiles)
                            return "--task SecondPassFDR requires --input <file...>.";
                        if (config.LibrarySource == null || string.IsNullOrEmpty(config.OutputBlib))
                            return "--task SecondPassFDR requires --library and --output.";
                        return null;
                }
            }

            // No --task: the full pipeline. A cold run scores from Stage 1; a resume over a
            // directory that already holds each run's artifacts skips to whichever stage is
            // outstanding, which the per-task validity sidecars decide - not the input kind.
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
        private static int ExperimentAggFileCount(OspreyConfig config, bool hasInputFiles)
        {
            switch (config.SelectedTask)
            {
                case HpcTask.SpectraCache:
                case HpcTask.PerFileScoring:
                case HpcTask.PerFileRescore:
                    return 0;
            }
            return hasInputFiles ? config.InputFiles.Count : 0;
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
