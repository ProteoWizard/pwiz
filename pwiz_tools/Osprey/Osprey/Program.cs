/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Globalization;
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

        // Exit codes, as Skyline's command line defines them.
        internal const int EXIT_CODE_SUCCESS = 0;
        internal const int EXIT_CODE_FAILURE_TO_START = 1;
        internal const int EXIT_CODE_RAN_WITH_ERRORS = 2;

        // The command-line text the "not specified" errors in ValidateArgs tell the user to add.
        // Argument names and example values, so not translated.
        internal const string USAGE_INPUT = @"-i <file1.mzML> [file2.mzML ...]";
        internal const string USAGE_LIBRARY = @"-l <library.tsv>";
        internal const string USAGE_OUTPUT = @"-o <output.blib>";

        // The caller's writer (stderr from Main), kept after a --log-file swap replaces _out,
        // so an error reported before the swap still counts when the exit code is reconciled.
        private static CommandStatusWriter _consoleOut = _out;

        static int Main(string[] args)
        {
            // Japanese and Chinese text written to a console in a code page that cannot hold it
            // (the OEM code page of an English-locale Windows, or Shift-JIS / GBK where the reader
            // expects UTF-8, as in "--help html > help.html") arrives as '?' or mojibake. As in
            // SkylineCmd's EncodingManager, switch the console to UTF-8 for the run and put it back.
            Encoding startEncoding = UsesTranslatedText(args) ? SwitchConsoleEncoding(new UTF8Encoding(false)) : null;
            try
            {
                return RunCommand(args, new CommandStatusWriter(Console.Error));
            }
            finally
            {
                if (startEncoding != null)
                    SwitchConsoleEncoding(startEncoding);
            }
        }

        /// <summary>
        /// True when this run may write Japanese or Chinese: an explicit <c>--culture</c> (SkylineCmd's
        /// rule), or a UI language Osprey ships translations for.
        /// </summary>
        private static bool UsesTranslatedText(string[] args)
        {
            try
            {
                if (OspreyCommandArgs.FindValue(args, OspreyCommandArgs.ARG_INTERNAL_CULTURE) != null)
                    return true;
            }
            catch (Exception)
            {
                // A malformed --culture is reported by RunCommand; decide from the UI language.
            }
            string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return language == @"ja" || language == @"zh";
        }

        /// <summary>
        /// Sets the console output encoding, returning the one it replaced, or null when there is
        /// no console to change (output redirected by a host that refuses it).
        /// </summary>
        private static Encoding SwitchConsoleEncoding(Encoding encoding)
        {
            try
            {
                Encoding previous = Console.OutputEncoding;
                Console.OutputEncoding = encoding;
                return previous;
            }
            catch (IOException)
            {
                return null;
            }
            catch (PlatformNotSupportedException)
            {
                return null;
            }
        }

        /// <summary>
        /// Run one command line in this process, writing to <paramref name="consoleOut"/>, and
        /// return the exit code - Skyline's <c>CommandLineRunner.RunCommand</c>. Tests call it so
        /// a failing command line can be debugged in place instead of in a child process.
        /// </summary>
        internal static int RunCommand(string[] args, CommandStatusWriter consoleOut)
        {
            _out = _consoleOut = consoleOut;
            // Before parsing, so a warning OspreyCommandArgs raises while parsing reaches the
            // caller's writer too; the --log-file swap later re-points it.
            OspreyOutput.Out = _out;
            // One command line is one run, however many a test runs in this process: nothing
            // keyed by library entry id may carry over, since the next run's library reuses them.
            FdrScoresSidecar.BeginRun();
            FragmentMath.ClearTop6MzCache();
            // Before anything is written, so every line - a parse error included - is in the
            // requested culture. A test runs a command line in process, so the scope also puts
            // the caller's culture back.
            using (CreateCultureScope(args, out string cultureError))
            {
                try
                {
                    if (cultureError != null)
                    {
                        LogError(cultureError);
                        return ReconcileExitCode(EXIT_CODE_FAILURE_TO_START);
                    }
                    return ReconcileExitCode(Run(args));
                }
                finally
                {
                    // A --log-file swap replaced _out; flush and close that writer, never the caller's.
                    if (!ReferenceEquals(_out, _consoleOut))
                    {
                        _out.Flush();
                        _out.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// A scope for the culture named by <c>--culture</c>, or null when there is none. A name
        /// .NET does not know, or no name at all, also gives null, with <paramref name="error"/>
        /// set to the message (.NET's own, already localized, for an unknown name), and the run
        /// stops there.
        /// </summary>
        internal static CultureScope CreateCultureScope(string[] args, out string error)
        {
            error = null;
            try
            {
                string cultureName = OspreyCommandArgs.FindValue(args, OspreyCommandArgs.ARG_INTERNAL_CULTURE);
                return cultureName == null ? null : new CultureScope(CultureInfo.GetCultureInfo(cultureName));
            }
            catch (ArgumentException ex)
            {
                // A CultureNotFoundException (a name .NET does not know), or --culture with no value.
                error = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Make the exit code and the log agree, as Skyline's <c>CommandLine.Run</c> does: an
        /// "Error:" line under a success code becomes <see cref="EXIT_CODE_RAN_WITH_ERRORS"/>,
        /// and a failure code with no "Error:" line gets one. Either case also writes a
        /// <c>[PATH] exit-reconciled</c> line, because it means some code path reported an error
        /// without failing, or failed without saying why - which the regression gate fails on.
        /// </summary>
        private static int ReconcileExitCode(int exitCode)
        {
            bool errorReported = _consoleOut.IsErrorReported || _out.IsErrorReported;
            var agreement = GetExitAgreement(exitCode, errorReported);
            if (agreement.Mismatch != null)
                LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_EXIT_RECONCILED, agreement.Mismatch));
            if (agreement.NeedsErrorLine)
                LogError(OspreyResources.Program_ReconcileExitCode_Failure_occurred__Exiting___);
            return agreement.ExitCode;
        }

        /// <summary>
        /// The decision behind <see cref="ReconcileExitCode"/>, free of process state so it can be
        /// tested: the exit code to return, whether an "Error:" line must still be written, and the
        /// mismatch that forced either (null when the code and the log already agree).
        /// </summary>
        internal static (int ExitCode, bool NeedsErrorLine, string Mismatch) GetExitAgreement(
            int exitCode, bool errorReported)
        {
            if (exitCode == EXIT_CODE_SUCCESS && errorReported)
                return (EXIT_CODE_RAN_WITH_ERRORS, false, @"error-with-success");
            if (exitCode != EXIT_CODE_SUCCESS && !errorReported)
                return (exitCode, true, @"failure-without-error");
            return (exitCode, false, null);
        }

        private static int Run(string[] args)
        {
            // Route OspreyDiagnostics dump messages through the same logging
            // channel as the rest of the pipeline so bisection logs appear
            // alongside normal output.
            OspreyDiagnosticsLog.Log = OspreyLog.Out;

            if (args.Length == 0)
            {
                // No args is a usage error, so the prompt goes to stderr (_out wraps
                // Console.Error); an explicit --help instead writes to stdout (see
                // OspreyCommandArgs.PrintUsage).
                OspreyCommandArgs.PrintUsage(null, _out);
                LogError(OspreyResources.Program_Run_No_arguments_were_given__see_the_usage_above_);
                return EXIT_CODE_FAILURE_TO_START;
            }

            try
            {
                // Scan args for the HPC task selector up front so error
                // messages name the task before any other argument is parsed. A single
                // `--task <Name>` runs exactly one pipeline stage (HPC: one
                // node = one task) - the selected stage is the only one the driver
                // includes (OspreyConfig.Includes) - and the task sets whatever behavior
                // flag its selection implies. Default (no --task) runs the full
                // straight-through pipeline. Any unrecognized flag (including
                // the retired --no-join / --join-only / --join-at-pass) fails
                // fast in ParseArgs. FindValue reads the argument the way the parser does,
                // so the two cannot disagree about what selected the task.
                string taskName;
                try
                {
                    taskName = OspreyCommandArgs.FindValue(args, OspreyCommandArgs.ARG_TASK);
                }
                catch (ArgumentException ex)
                {
                    LogError(ex.Message);
                    return EXIT_CODE_FAILURE_TO_START;
                }

                // The one task set for this run. The selection is looked up in it and the
                // pipeline comes from it, so the two hold the same instances and a stage
                // can ask whether it IS the selection by reference.
                var tasks = OspreyTasks.Create();
                OspreyTask selectedTask = null;
                if (taskName != null)
                {
                    string taskErr = ResolveTask(taskName, tasks, out selectedTask);
                    if (taskErr != null)
                    {
                        LogError(taskErr);
                        return EXIT_CODE_FAILURE_TO_START;
                    }
                }

                OspreyConfig config;
                try
                {
                    config = ParseArgs(args);
                    CanonicalizeOutputPaths(config);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is FileNotFoundException || ex is InvalidDataException)
                {
                    // A usage error, not a failure: the parser threw it to name the argument,
                    // so the message IS the diagnosis, and a type name plus a stack through
                    // the parser would only bury it. Reported the way the ValidateArgs errors
                    // below are. Anything else the parser throws is a defect and falls through
                    // to the sink at the bottom of Main with its frames intact.
                    //
                    // These three are what the parser raises ON PURPOSE, and the list is
                    // deliberately narrower than it reads: FileNotFoundException, not
                    // IOException, because --input-list throws the former for the missing-file
                    // case a user can fix, while File.ReadAllLines can throw a sharing or
                    // device IOException that is NOT a usage error and needs its type, inner
                    // exception and stack. Numeric values reach ParseInt / ParseDouble, which
                    // convert FormatException into an ArgumentException naming the flag, so
                    // no parse failure needs an entry of its own here.
                    LogError(ex.Message);
                    return EXIT_CODE_FAILURE_TO_START;
                }
                // --task selects one task and, with it, the pipeline it runs - the canonical
                // stages, or a selector's own list (OspreyTasks.PipelineFor). Membership is
                // one rule over the two (OspreyConfig.Includes); the task sets whatever
                // behavior flag its selection implies (OspreyTask.ApplySelection) - a stop
                // boundary, the reconciled-footer gate, the report flag the diagnostics
                // selector stands for. All of it derives from --task and from nothing else,
                // which is what let the input KIND retire: it was the OTHER seam saying the
                // same thing.
                var pipeline = tasks.PipelineFor(selectedTask);
                config.SelectTask(selectedTask, pipeline);

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
                    return EXIT_CODE_FAILURE_TO_START;
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
                    }
                    catch (Exception ex)
                    {
                        LogError(string.Format(OspreyResources.Program_Run_Failed_to_open_log_file__0____1_, config.LogFilePath, ex.Message));
                        return EXIT_CODE_FAILURE_TO_START;
                    }
                }

                // Point the Core output seam at _out: below-exe layers (FDR, IO) and LogInfo emit
                // through the same CommandStatusWriter (stamps + --log-file). Whether a tagged
                // line is written at all is decided where it is emitted (OspreyLog.Write), which
                // reads PerfStats here.
                OspreyOutput.PerfStats = config.PerfStats;
                OspreyOutput.Verbose = config.Verbose;
                OspreyOutput.Out = _out;

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

                // --export-library reads the library and nothing else, so it is settled before the
                // inputs are checked: a reused search command line may name inputs that moved.
                if (!string.IsNullOrEmpty(config.ExportLibraryBlib))
                {
                    if (!File.Exists(config.LibrarySource.Path))
                    {
                        LogError(string.Format(OspreyResources.Program_Run_Library_file_not_found___0_, config.LibrarySource.Path));
                        return EXIT_CODE_FAILURE_TO_START;
                    }
                    LogInfo(string.Format(OspreyResources.Program_Run_Osprey_v_0_, OspreyVersion.DisplayVersion));
                    LogInfo(string.Format(OspreyResources.Program_Run_Command___0_, string.Join(@" ", args)));
                    return RunExportLibrary(config);
                }

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
                        OspreyResources.Program_Run_Input_file_not_found__and_no_spectra_cache_or_intermediate_file_exists_to_stand_in_for_it_, inputFile));
                    return EXIT_CODE_FAILURE_TO_START;
                }
                // Announced, not silent: a run whose sources are gone cannot rebuild a
                // cache that turns out to be wrong, so the log is the only provenance.
                if (cacheOnlyInputs > 0)
                {
                    LogInfo(CountText.Format(cacheOnlyInputs, config.InputFiles.Count == 1
                            ? OspreyResources.Program_Run_The_input_file_is_not_present_but_has_a_spectra_cache__reading_it_from_the_cache_
                            : OspreyResources.Program_Run_1_of__1__input_files_is_not_present_but_has_a_spectra_cache__reading_it_from_the_cache_,
                        OspreyResources.Program_Run__0__of__1__input_files_are_not_present_but_have_a_spectra_cache__reading_those_from_the_,
                        config.InputFiles.Count));
                    LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_INPUT_SOURCE, @"spectra-cache {0}/{1}",
                        cacheOnlyInputs, config.InputFiles.Count));
                }
                if (artifactOnlyInputs > 0)
                {
                    LogInfo(CountText.Format(artifactOnlyInputs, config.InputFiles.Count == 1
                            ? OspreyResources.Program_Run_The_input_file_is_not_present__using_the_intermediate_scores_file_written_for_it_
                            : OspreyResources.Program_Run_1_of__1__input_files_is_not_present__using_the_intermediate_scores_file_written_for_it_,
                        OspreyResources.Program_Run__0__of__1__input_files_are_not_present__using_the_intermediate_scores_file_written_for_,
                        config.InputFiles.Count));
                    LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_INPUT_SOURCE, @"scores-parquet {0}/{1}",
                        artifactOnlyInputs, config.InputFiles.Count));
                }
                if (config.LibrarySource != null && !File.Exists(config.LibrarySource.Path))
                {
                    LogError(string.Format(OspreyResources.Program_Run_Library_file_not_found___0_, config.LibrarySource.Path));
                    return EXIT_CODE_FAILURE_TO_START;
                }

                // Log startup info
                LogInfo(string.Format(OspreyResources.Program_Run_Osprey_v_0_, OspreyVersion.DisplayVersion));
                LogInfo(string.Format(OspreyResources.Program_Run_Command___0_, string.Join(@" ", args)));
                LogInfo(string.Format(OspreyResources.Program_Run_Input_files___0_, config.InputFiles.Count));
                LogInfo(string.Format(OspreyResources.Program_Run_Library___0____1__,
                    config.LibrarySource?.Path ?? OspreyResources.Program_Run__none_,
                    config.LibrarySource?.Format.GetLocalizedString() ?? @"?"));
                // A --task run executes one HPC stage rather than the full pipeline;
                // name it so the log says which single task ran (no --task = full
                // pipeline, no line). A selector that is not a stage of the pipeline it
                // runs (ModelDiagnostics, TrainingExport) runs every stage the analysis
                // still needs, and says so. The Name is the canonical spelling, whatever
                // case the operator typed.
                if (config.SelectedTask != null)
                {
                    LogInfo(string.Format(config.Pipeline.Contains(config.SelectedTask)
                        ? OspreyResources.Program_Run_Task___0___single_task_run_
                        : OspreyResources.Program_Run_Task___0___runs_every_stage_the_analysis_still_needs_,
                        config.SelectedTask.Name));
                }
                // A task that writes something other than the blib - per-file parquets,
                // per-file spectra caches, the diagnostics report alone - names its real
                // output, so the log does not read as if the --output blib were being
                // rebuilt. SecondPassFDR writes the blib, and TrainingExport is the full run with
                // the export on, whose parquets the training export line names; every other
                // selectable task describes its own output.
                LogInfo(string.Format(OspreyResources.Program_Run_Output___0_,
                    config.SelectedTask?.DescribeOutput(config) ?? config.OutputBlib));
                LogInfo(string.Format(OspreyResources.Program_Run_Resolution___0_, config.ResolutionMode.GetLocalizedString()));
                LogInfo(string.Format(OspreyResources.Program_Run_Fragment_tolerance___0___1_,
                    config.FragmentTolerance.Tolerance,
                    config.FragmentTolerance.Unit.GetLocalizedString()));
                LogInfo(string.Format(OspreyResources.Program_Run_Run_FDR___0_, config.RunFdr));
                LogInfo(string.Format(OspreyResources.Program_Run_Experiment_FDR___0_, config.ExperimentFdr));
                // Named only when on: with the option off the run, its log included, is the
                // run it was before the option existed.
                string trainingExport = DescribeTrainingExport(config);
                if (trainingExport != null)
                    LogInfo(trainingExport);
                // Always print which experiment-wide aggregation is in force, active or not.
                // Reported HERE and not from Stage 5 because FirstPassFdrTask.Run is skipped on
                // --task SecondPassFDR, on a Rehydrate, and on any warm resume - exactly the runs
                // whose q-values an operator is most likely to attribute to the wrong arm.
                LogInfo(OspreyEnvironment.DescribeExperimentAgg());
                if (OspreyEnvironment.ExperimentAggUnrecognized)
                {
                    LogWarning(string.Format(
                        @"OSPREY_EXPERIMENT_AGG was set to an unrecognized value; using the default " +
                        @"'{0}'. Recognized values: '{0}', or '{1}<N>' with N in [2, {2}] (e.g. '{1}2').",
                        OspreyEnvironment.EXPERIMENT_AGG_MAX,
                        OspreyEnvironment.EXPERIMENT_AGG_MEAN_BEST_PREFIX,
                        OspreyEnvironment.MEAN_BEST_N_MAX));
                }
                // OSPREY_FDR_MODEL: abort on an unrecognized value, do not fall back. A run that
                // asked for trees and trained the linear SVM is the #4491 defect, and its output
                // reads exactly like a tree result. The classifier is named only when it is not
                // the default, so the linear SVM's log is unchanged; here rather than at Stage 5
                // for the same reason as the aggregation line above.
                if (OspreyEnvironment.FdrModelError != null)
                {
                    LogError(OspreyEnvironment.FdrModelError);
                    return 1;
                }
                string fdrModelLine = OspreyEnvironment.DescribeFdrModel(config.FdrClassifier);
                if (fdrModelLine != null)
                    LogInfo(fdrModelLine);
                // Abort, do not fall back. A run that asked for a mode it did not get would
                // report q-values the caller never requested, under whatever output name the
                // caller chose - and 'percolator' was removed, so existing sweep scripts still
                // pass it. Checked here rather than at SecondPassFDR so it costs seconds
                // instead of a full Stage 1-5.
                if (OspreyEnvironment.Pass2QValueUnrecognized)
                {
                    // Why 'percolator' and 'transfer-compete' were removed, with the entrapment
                    // numbers, is in docs/12-second-pass-fdr.md and on OspreyEnvironment.Pass2QValue.
                    LogError(string.Format(
                        @"OSPREY_PASS2_QVALUE='{2}' is not recognized. Use '{0}' or '{1}', or unset it " +
                        @"for the default ('{1}'). The 'percolator' and 'transfer-compete' modes were removed.",
                        OspreyEnvironment.PASS2_QVALUE_TRANSFER,
                        OspreyEnvironment.PASS2_QVALUE_PROTEIN_COMPACT,
                        OspreyEnvironment.Pass2QValueSetting));
                    return EXIT_CODE_FAILURE_TO_START;
                }
                // Abort for the same reason: a tolerance that does not parse would train under
                // the default rule and key its directories as the default, so a parity arm
                // (OSPREY_SVM_C_TOLERANCE=0) would report the default arm's numbers under its
                // own name. Checked here so a mistyped value costs seconds, not Stages 1-4.
                if (OspreyEnvironment.SvmCSelectionToleranceUnrecognized)
                {
                    LogError(string.Format(
                        @"OSPREY_SVM_C_TOLERANCE must be a number in [0, 1) such as '0.01', not '{0}'. " +
                        @"Unset it for the default ({1}); 0 selects the strict maximum the Rust implementation uses.",
                        OspreyEnvironment.SvmCSelectionToleranceSetting,
                        OspreyEnvironment.DEFAULT_SVM_C_SELECTION_TOLERANCE.ToString(CultureInfo.InvariantCulture)));
                    return 1;
                }
                if (!string.IsNullOrEmpty(OspreyEnvironment.SvmCSelectionToleranceSetting))
                {
                    LogInfo(string.Format(@"First-pass SVM C selection: OSPREY_SVM_C_TOLERANCE = {0}",
                        OspreyEnvironment.SvmCSelectionTolerance.ToString(CultureInfo.InvariantCulture)));
                }
                // OSPREY_STAGE7_STREAM was REMOVED (2026-09-10): the streamed Stage-7 join is the
                // only arm there is. Setting it to 0 used to select the RESIDENT join, so a sweep
                // script still passing it would measure the streamed arm and file the numbers
                // under the resident one - the misattribution case these variables have to be
                // strict about, and the reason this is an error rather than a warning. Checked at
                // startup so a stale script dies in seconds instead of after Stage 1-5.
                // It kept the RESIDENT Stage-7 join as an A/B byte-identity oracle for the
                // streamed default; that A/B was banked (the resident arm matched the committed
                // golden at 1e-9 with a byte-identical diagnostics report). The configurations
                // that still take the resident arm do so by their own declaration (ResidentPaths).
                if (OspreyEnvironment.Stage7StreamRetiredSet)
                {
                    LogError(
                        @"OSPREY_STAGE7_STREAM was removed and has no effect. Unset it. Second-pass " +
                        @"FDR now always processes one run at a time where the analysis allows it.");
                    return EXIT_CODE_FAILURE_TO_START;
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
                    // 'hpc-merge' and 'fdrbench-pass1' were retired: the --task SecondPassFDR
                    // reconciled-input load and the pass-1 FDRBench emitter both stream and need
                    // no allowance. 'non-percolator-fdr' was retired with the simple FDR method
                    // it named.
                    LogWarning(string.Format(
                        @"OSPREY_ALLOW_UNFIXED_RESIDENT contains tokens that are not recognized and " +
                        @"have no effect: {0}. Recognized: {1}. Any recognized token in the same value " +
                        @"is still honored.",
                        OspreyEnvironment.UnrecognizedResidentTokens,
                        string.Join(@", ", ResidentPaths.KNOWN_UNFIXED)));
                }
                LogInfo(string.Format(OspreyResources.Program_Run_Protein_FDR___0_, config.EffectiveProteinFdr));
                LogInfo(string.Format(OspreyResources.Program_Run_Threads___0_, config.NThreads));
                // Machine twins of the banner: the liveness anchor a route assertion needs
                // before it can trust an absence, and the aggregation arm, which the prose
                // above states for a person.
                LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_EXPERIMENT_AGG, OspreyEnvironment.ExperimentAgg));
                LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_STARTUP, @"threads={0}", config.NThreads));
                LogInfo(string.Empty);

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
                // PerFileRescoring) includes only PerFileRescoreTask
                // (OspreyConfig.Includes); PerFileScoring's lazy-rehydrate (via
                // ctx.Demand) populates the upstream state from the boundary files
                // on disk.
                return new AnalysisPipeline().Run(config, pipeline);
            }
            catch (Exception ex)
            {
                // As in AnalysisPipeline.Run: a user-actionable failure is its message, and a
                // defect is the whole exception, so an empty message or a wrapper's
                // InnerException cannot hide the cause. Usage errors do not reach here; the
                // parser's catch above reports them as the one-line messages they are.
                LogError(DescribeFailure(ex, OspreyResources.Program_Run_Fatal_error___0_));
                return EXIT_CODE_FAILURE_TO_START;
            }
        }

        /// <summary>
        /// The Error: text for an exception that reached a top-level sink, following Skyline:
        /// an exception whose message is written for the user (file not found, access denied,
        /// a damaged or incompatible file - see <see cref="CommonExceptionUtil.IsProgrammingDefect"/>)
        /// is reported as that message alone; a programming defect is reported whole, type and
        /// stack included, through <paramref name="defectFormat"/>, so it can be fixed. A user
        /// exception with no message is treated as a defect: there is nothing else to show.
        /// </summary>
        internal static string DescribeFailure(Exception ex, string defectFormat)
        {
            var reported = CommonExceptionUtil.UnwrapUserException(ex);
            if (!CommonExceptionUtil.IsProgrammingDefect(reported) && !string.IsNullOrEmpty(reported.Message))
                return reported.Message;
            return string.Format(defectFormat, ex);
        }

        /// <summary>
        /// <c>--export-library</c>: load the library as a search would - deduplicated, and with
        /// supplied decoys marked when the command line says the library has them - and write it
        /// as a .blib (<see cref="LibraryBlibWriter"/>). No search runs.
        /// </summary>
        private static int RunExportLibrary(OspreyConfig config)
        {
            var library = LibraryLoader.Load(config, LibraryLoadOptions.Default, OspreyLog.Out, LogWarning,
                out string loadError);
            if (loadError != null)
            {
                LogError(loadError);
                return EXIT_CODE_FAILURE_TO_START;
            }
            if (library == null || library.Count == 0)
            {
                LogError(OspreyTasksResources.PerFileScoringTask_LoadLibraryAndDecoys_The_spectral_library_is_empty_after_loading_);
                return EXIT_CODE_FAILURE_TO_START;
            }
            string path = Path.GetFullPath(config.ExportLibraryBlib);
            int written;
            try
            {
                written = LibraryBlibWriter.Write(path, library, config.LibrarySource.Path, config.NThreads,
                    config.DecoyPrefixes);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Data.Common.DbException)
            {
                // A locked or unwritable output is the user's to fix: the message the search output
                // gives for the same condition, not a stack trace. Only file writing happens here, so
                // a database failure is the file, not a defect in the schema.
                throw new BlibOutputException(path, ex);
            }
            LogInfo(CountText.Format(written,
                OspreyResources.Program_RunExportLibrary_Saved_1_library_precursor_to__1_,
                OspreyResources.Program_RunExportLibrary_Saved__0_N0__library_precursors_to__1_,
                path));
            return EXIT_CODE_SUCCESS;
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
                LogError(string.Format(OspreyResources.Program_RunModelDiagnosticsTask__0___there_is_no_completed_first_pass_to_describe__no_first_pass_intermediate_file_for_,
                    ModelDiagnosticsTaskText, FirstPassFdrTask.TASK_NAME));
                return EXIT_CODE_FAILURE_TO_START;
            }
            // Everything this analysis can have is on disk: a pure render, seconds, no pipeline.
            if (ModelDiagnosticsReport.AllProductsCurrent(config))
            {
                if (ModelDiagnosticsReport.TryRenderFromProducts(config, OspreyLog.Out))
                    return EXIT_CODE_SUCCESS;
                LogError(string.Format(OspreyResources.Program_RunModelDiagnosticsTask__0___the_saved_model_diagnostics_data_for_this_analysis_could_not_be_read__so_the_report_,
                    ModelDiagnosticsTaskText));
                return EXIT_CODE_FAILURE_TO_START;
            }

            // A product is outstanding. Say so before the pipeline banner, because the next
            // thing the log shows is task machinery and an operator needs to know it is a fold
            // rather than the re-analysis this task used to refuse to start.
            LogInfo(string.Format(OspreyResources.Program_RunModelDiagnosticsTask__0___building_the_report_from_the_completed_analysis__Nothing_is_re_run_and_no_other_,
                ModelDiagnosticsTaskText));
            return -1;
        }

        /// <summary>The command-line text that selects this task, which its messages start with.</summary>
        internal static string ModelDiagnosticsTaskText
        {
            get { return OspreyCommandArgs.ARG_TASK + ModelDiagnosticsTask.TASK_NAME; }
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
        /// Resolve a <c>--task &lt;Name&gt;</c> selector (case-insensitive, matched
        /// against each task's stable <c>Name</c>) to the task instance in
        /// <paramref name="tasks"/> that bears it. One node = one task on HPC. The caller
        /// hands the instance and the pipeline it runs to <see cref="OspreyConfig.SelectTask"/>,
        /// and the same instance then appears in that pipeline.
        ///
        /// Returns null on success, or an error message string listing every valid name
        /// for an unknown one. Internal so Osprey.Test can exercise it.
        /// </summary>
        internal static string ResolveTask(string taskName, OspreyTasks tasks, out OspreyTask task)
        {
            task = tasks.FindByName(taskName);
            if (task != null)
                return null;
            return string.Format(OspreyResources.Program_ResolveTask__0___unknown_task___1____Valid_tasks___2__,
                OspreyCommandArgs.ARG_TASK.ArgumentText, taskName, string.Join(@", ", tasks.All.Select(t => t.Name)));
        }

        /// <summary>
        /// Validate the parsed config against the selected
        /// <see cref="OspreyConfig.SelectedTask"/> (or the default full pipeline
        /// when none was given). Every task takes the SAME input kind now - the data
        /// files, named with <c>-i</c> or <c>--input-list</c> - so what is validated is
        /// presence and count, not kind. The cross this used to reject
        /// (<c>--task PerFileScoring --input-scores</c>) cannot be expressed any more,
        /// which is the point of retiring the second seam rather than teaching a third
        /// predicate about it. What each task requires is the task's own answer
        /// (<see cref="ISelectableTask.ValidateSelection"/>), so there is no per-task
        /// switch here to keep in step with the task list. Returns null on success or an
        /// error message string on failure. Does not log warnings (those stay in
        /// <see cref="Main"/>). Internal so Osprey.Test can exercise it.
        /// </summary>
        internal static string ValidateArgs(OspreyConfig config)
        {
            // --export-library converts the library and exits: it needs the library and nothing
            // else, whatever else the command line holds, so nothing below may refuse it.
            if (!string.IsNullOrEmpty(config.ExportLibraryBlib))
            {
                if (config.LibrarySource == null)
                    return string.Format(OspreyResources.Program_ValidateArgs_No_spectral_library_specified__Use__0_, USAGE_LIBRARY);
                if (string.IsNullOrWhiteSpace(config.ExportLibraryBlib))
                {
                    return string.Format(OspreyResources.Program_ValidateArgs_No_path_given_for__0__,
                        OspreyCommandArgs.ARG_EXPORT_LIBRARY.ArgumentText);
                }
                // The loader has closed the library by the time the export replaces its output,
                // so the same path would silently overwrite the library with the export.
                if (IsSamePath(config.ExportLibraryBlib, config.LibrarySource.Path))
                {
                    return string.Format(OspreyResources.Program_ValidateArgs_The__0__path_is_the_library_it_reads___1_,
                        OspreyCommandArgs.ARG_EXPORT_LIBRARY.ArgumentText, config.LibrarySource.Path);
                }
                return null;
            }

            // The search replaces its output blib at the end, after the library was read, so the
            // library's own path would silently replace the library with this run's results.
            if (config.LibrarySource != null && !string.IsNullOrWhiteSpace(config.OutputBlib) &&
                IsSamePath(config.OutputBlib, config.LibrarySource.Path))
            {
                return string.Format(OspreyResources.Program_ValidateArgs_The__0__path_is_the_library_the_search_reads___1_,
                    OspreyCommandArgs.ARG_OUTPUT.ArgumentText, config.LibrarySource.Path);
            }

            bool hasInputFiles = config.HasInputFiles;

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

            string exportErr = TrainingExportError(config.TrainingExport);
            if (exportErr != null)
                return exportErr;

            // A --task run: the task states what it needs, naming itself in the message.
            if (config.SelectedTask != null)
                return config.SelectedTask.ValidateSelection(config);

            // No --task: the full pipeline. A cold run scores from Stage 1; a resume over a
            // directory that already holds each run's artifacts skips to whichever stage is
            // outstanding, which the per-task validity sidecars decide - not the input kind.
            if (!hasInputFiles)
                return string.Format(OspreyResources.Program_ValidateArgs_No_input_files_specified__Use__0_, USAGE_INPUT);
            if (config.LibrarySource == null)
                return string.Format(OspreyResources.Program_ValidateArgs_No_spectral_library_specified__Use__0_, USAGE_LIBRARY);
            if (string.IsNullOrEmpty(config.OutputBlib))
                return string.Format(OspreyResources.Program_ValidateArgs_No_output_path_specified__Use__0_, USAGE_OUTPUT);
            return null;
        }

        /// <summary>
        /// Whether two paths name the same file once made absolute: ignoring case except on
        /// Linux, whose file systems are case-sensitive.
        /// </summary>
        private static bool IsSamePath(string path1, string path2)
        {
            return string.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2),
                OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The startup line naming the training export, or null when <c>--training-export</c>
        /// is off. The export is a product of PerFileRescoring, so it is described only where
        /// that task runs - a straight-through run, <c>--task PerFileRescoring</c> or
        /// <c>--task TrainingExport</c> - by the same membership rule the driver applies.
        /// </summary>
        internal static string DescribeTrainingExport(OspreyConfig config)
        {
            if (!config.TrainingExport.Enabled)
                return null;
            if (!ScoringTaskShared.Includes<PerFileRescoreTask>(config) || config.DiagnosticsOnly)
            {
                return string.Format(OspreyResources.Program_DescribeTrainingExport_Training_export__not_written_by_this_run___0__writes_it_under__1___2___1___3__or_a_run_without__1__, OspreyCommandArgs.ARG_TRAINING_EXPORT.ArgumentText,
                    OspreyCommandArgs.ARG_TASK.ArgumentText, TrainingExportTask.TASK_NAME, PerFileRescoreTask.TASK_NAME);
            }
            return string.Format(OspreyResources.Program_DescribeTrainingExport_Training_export___0__per_run__run_q_____1___claimant_q_____2___XICs__3__,
                @"<stem>" + TrainingExportParquet.EXT, config.TrainingExport.EffectiveMaxQ(config.RunFdr),
                config.TrainingExport.EffectiveClaimantQ,
                config.TrainingExport.WriteXics ? OspreyResources.Program_DescribeTrainingExport_on : OspreyResources.Program_DescribeTrainingExport_off);
        }

        /// <summary>
        /// The training-export settings are refused rather than ignored when they cannot apply:
        /// given without <c>--training-export</c> (or <c>--task TrainingExport</c>) they would
        /// change nothing, and a q threshold outside (0, 1] selects nothing or everything.
        /// </summary>
        private static string TrainingExportError(TrainingExportConfig export)
        {
            if (export.HasSettingsWithoutExport)
            {
                return string.Format(OspreyResources.Program_TrainingExportError__0____1__and__2__apply_only_with__3__,
                    OspreyCommandArgs.ARG_TRAINING_EXPORT_MAX_Q.ArgumentText,
                    OspreyCommandArgs.ARG_TRAINING_EXPORT_CLAIMANT_Q.ArgumentText,
                    OspreyCommandArgs.ARG_TRAINING_EXPORT_XICS.ArgumentText,
                    OspreyCommandArgs.ARG_TRAINING_EXPORT.ArgumentText);
            }
            if (export.MaxQ.HasValue && !(export.MaxQ.Value > 0 && export.MaxQ.Value <= 1))
                return string.Format(OspreyResources.Program_TrainingExportError__0__must_be_in__0__1__, OspreyCommandArgs.ARG_TRAINING_EXPORT_MAX_Q.ArgumentText);
            if (export.ClaimantQ.HasValue && !(export.ClaimantQ.Value > 0 && export.ClaimantQ.Value <= 1))
                return string.Format(OspreyResources.Program_TrainingExportError__0__must_be_in__0__1__, OspreyCommandArgs.ARG_TRAINING_EXPORT_CLAIMANT_Q.ArgumentText);
            // The transfer arm computes every run's second-pass q in SecondPassFDR, after
            // PerFileRescoring has written the export, so the export would have no second-pass
            // values for any run to select on.
            if (export.Enabled && OspreyEnvironment.Pass2TransferQ)
            {
                return string.Format(OspreyResources.Program_TrainingExportError__0__cannot_run_with__1___that_mode_computes_the_run_q_values_in__2__after_the_per_run_export_,
                    OspreyCommandArgs.ARG_TRAINING_EXPORT.ArgumentText, @"OSPREY_PASS2_QVALUE=" + OspreyEnvironment.PASS2_QVALUE_TRANSFER,
                    SecondPassFdrTask.TASK_NAME);
            }
            return null;
        }

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
                collisions.Count == 1
                    ? OspreyResources.Program_DuplicateInputStemError_1_input_file_name_appears_more_than_once_
                    : OspreyResources.Program_DuplicateInputStemError__0__input_file_names_appear_more_than_once_, collisions.Count);
            sb.Append(' ').Append(OspreyResources.Program_DuplicateInputStemError_Every_file_Osprey_writes_for_an_input_is_named_after_the_input_without_its_extension__so_);
            foreach (var kv in collisions)
                sb.Append('\n').AppendFormat(@"  '{0}': {1}", kv.Key, string.Join(@", ", kv.Value));
            return sb.ToString();
        }

        /// <summary>
        /// How many runs this invocation will aggregate across for the experiment-wide
        /// competition, or 0 when that is not a property of this invocation. The per-file HPC
        /// workers (SpectraCache / PerFileScoring / PerFileRescoring) each see ONE input and
        /// never compute an experiment-wide score, so reporting their input count would refuse
        /// every worker of a legitimate distributed mean(best-N) run. Which tasks those are
        /// is the task's own fact (<see cref="ISelectableTask.IsPerFileWorker"/>).
        /// </summary>
        private static int ExperimentAggFileCount(OspreyConfig config, bool hasInputFiles)
        {
            if (config.SelectedTask?.IsPerFileWorker == true)
                return 0;
            return hasInputFiles ? config.InputFiles.Count : 0;
        }

        internal static void LogInfo(string message)
        {
            OspreyOutput.Out.WriteLine(message);
        }

        internal static void LogInfo(LogTag tag, string text)
        {
            OspreyLog.Out.LogInfo(tag, text);
        }

        /// <summary>A tagged line formatted with the invariant culture (see <see cref="OspreyLog"/>).</summary>
        internal static void LogInfo(LogTag tag, string format, params object[] args)
        {
            OspreyLog.Out.LogInfo(tag, format, args);
        }

        internal static void LogWarning(string message)
        {
            // Through OspreyOutput.Out (not _out directly) so a warning emitted
            // while a file runs in a MultiProgressReporter per-file scope
            // (--parallel-files) lands in that file's buffered block, in context,
            // instead of interleaving with the live "[i] p%" aggregate line. Off
            // the parallel path OspreyOutput.Out is the same CommandStatusWriter,
            // so the output is unchanged.
            //
            // "Warning:" / "Error:" rather than a [TAG]: these lines are written for the
            // user, not for a tool, and follow Skyline's command-line convention (translated
            // with the rest of the text; CommandStatusWriter recognizes "Error:" in every
            // language Skyline ships).
            OspreyOutput.Out.WriteLine(WarningPrefix + @" " + message);
        }

        internal static void LogError(string message)
        {
            // Errors go straight to the process writer (NOT the per-file buffer):
            // surface immediately rather than waiting for the file's block to flush
            // on completion, so a failing run reports the cause right away.
            _out.WriteLine(ErrorPrefix + @" " + message);
        }

        /// <summary>
        /// The "Warning:" a warning line starts with, in the current UI language. Read at each
        /// call, never cached, because tests switch the language in process.
        /// </summary>
        internal static string WarningPrefix
        {
            get { return OspreyResources.Program_LogWarning_Warning_; }
        }

        /// <summary>
        /// The "Error:" an error line starts with, in the current UI language. Every translation
        /// must be one <see cref="CommandStatusWriter.ERROR_PREFIXES"/> lists, or the exit code
        /// stops agreeing with the log.
        /// </summary>
        internal static string ErrorPrefix
        {
            get { return OspreyResources.Program_LogError_Error_; }
        }

        /// <summary>
        /// Make the paths Osprey builds artifact paths FROM absolute, with the platform
        /// separator: --output-dir, --cache-dir (both set by --work-dir) and -o. Given as
        /// D:/runs/x, a work directory was joined with '\' into D:/runs/x\file.spectra.bin in
        /// every artifact path and every log line that names one. The input and library paths
        /// stay as typed: they are echoed back to the user, and the input names are written
        /// into the blib. No hash or resume stamp reads a directory, so this changes no cache
        /// decision (<see cref="SearchIdentity"/> hashes file names only).
        /// </summary>
        private static void CanonicalizeOutputPaths(OspreyConfig config)
        {
            config.OutputDir = FullPathOrEmpty(config.OutputDir);
            config.CacheDir = FullPathOrEmpty(config.CacheDir);
            config.OutputBlib = FullPathOrEmpty(config.OutputBlib);
        }

        private static string FullPathOrEmpty(string path)
        {
            return string.IsNullOrEmpty(path) ? path : Path.GetFullPath(path);
        }
    }
}
