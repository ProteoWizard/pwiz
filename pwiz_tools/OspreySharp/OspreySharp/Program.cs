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
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.IO;

namespace pwiz.OspreySharp
{
    /// <summary>
    /// Command-line entry point for OspreySharp.
    /// Parses CLI arguments and launches the analysis pipeline.
    /// Port of osprey/src/main.rs.
    /// </summary>
    static class Program
    {
        // Tracks the Rust Osprey upstream version this OspreySharp port
        // is aligned with. Used in parquet footer metadata; the Phase 3
        // validator requires same major.minor across cross-impl handoff.
        // TODO: 26.6.1 bumped the version string but the algorithmic
        // payload of v26.6.1 (reconciliation pairing library-supplied
        // decoys by base_id instead of stripping a DECOY_ prefix in
        // compute_consensus_rts + plan_reconciliation) is NOT yet
        // ported to this side. It does not affect reverse-decoy mode
        // (Stellar, DecoysInLibrary=false), but it WILL affect any
        // dataset run with --decoys-in-library. See osprey
        // release-notes/RELEASE_NOTES_v26.6.1.md and the
        // test_consensus_rts_pairs_library_decoy_by_base_id +
        // test_plan_reconciliation_includes_library_decoy_via_base_id
        // regression tests on the Rust side.
        internal const string VERSION = "26.6.1";
        internal const string VERSION_STRING = VERSION;

        static int Main(string[] args)
        {
            // Route OspreyDiagnostics dump messages through the same logging
            // channel as the rest of the pipeline so bisection logs appear
            // alongside normal output.
            OspreyDiagnostics.LogAction = LogInfo;

            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

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
                            LogError("--task requires a task name (PerFileScoring, FirstJoin, PerFileRescore, or MergeNode).");
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
                config.StopAfterStage5 = selectedTask == HpcTask.FirstJoin;
                config.ExpectReconciledInput = selectedTask == HpcTask.MergeNode;

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
                // Runs that consume --input-scores (FirstJoin, PerFileRescore,
                // MergeNode, or the default full pipeline started from scores)
                // have no mzML inputs to validate and ignore --output handling
                // differently from per-file scoring.
                bool fromInputScores = config.InputScores != null && config.InputScores.Count > 0;

                // Non-fatal warning: --task PerFileScoring with --output
                // supplied — that Stage 1-4 worker mode ignores --output. The
                // rescore worker (--task PerFileRescore, identified by
                // --input-scores) requires --output, so the warning would be
                // incorrect/confusing there.
                if (config.NoJoin && !fromInputScores && !string.IsNullOrEmpty(config.OutputBlib))
                {
                    LogWarning("--task PerFileScoring: --output is ignored (no blib is written). " +
                               "Per-file `.scores.parquet` files will be written next to each input mzML.");
                }

                // Validate input files exist on disk (skip when consuming
                // --input-scores, where there are no mzML inputs; --input-scores
                // paths were already validated by ResolveInputScores during parsing).
                if (!fromInputScores)
                {
                    foreach (string inputFile in config.InputFiles)
                    {
                        if (!File.Exists(inputFile))
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
                LogInfo(string.Format("OspreySharp v{0}", VERSION));
                LogInfo(string.Format("Command: {0}", string.Join(" ", args)));
                LogInfo(string.Format("Input files: {0}", config.InputFiles.Count));
                LogInfo(string.Format("Library: {0} ({1})",
                    config.LibrarySource?.Path ?? "(none)",
                    config.LibrarySource?.Format.ToString() ?? "?"));
                LogInfo(string.Format("Output: {0}", config.OutputBlib));
                LogInfo(string.Format("Resolution: {0}", config.ResolutionMode));
                LogInfo(string.Format("Fragment tolerance: {0} {1}",
                    config.FragmentTolerance.Tolerance,
                    config.FragmentTolerance.Unit == ToleranceUnit.Ppm ? "ppm" : "Th"));
                LogInfo(string.Format("Run FDR: {0:P1}", config.RunFdr));
                LogInfo(string.Format("Experiment FDR: {0:P1}", config.ExperimentFdr));
                if (config.ProteinFdr.HasValue)
                    LogInfo(string.Format("Protein FDR: {0:P1}", config.ProteinFdr.Value));
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
        }

        /// <summary>
        /// Parse command-line arguments into an OspreyConfig.
        /// Handles the same flags as the Rust CLI.
        /// </summary>
        internal static OspreyConfig ParseArgs(string[] args)
        {
            var config = new OspreyConfig();
            var inputFiles = new List<string>();
            string libraryPath = null;
            string outputPath = null;
            string workDir = null;
            string outputDir = null;
            string cacheDir = null;
            string resolution = "auto";
            double? fragmentTolerance = null;
            string fragmentUnit = null;

            int i = 0;
            while (i < args.Length)
            {
                string arg = args[i];

                // The single-token `--task=Name` form is resolved in Main's
                // pre-scan. Skip it here so the default branch doesn't flag
                // it as unknown.
                if (arg.StartsWith("--task=", StringComparison.Ordinal))
                {
                    i++;
                    continue;
                }

                switch (arg)
                {
                    case "-i":
                    case "--input":
                        i++;
                        // Consume all following non-flag arguments as input files
                        while (i < args.Length && !args[i].StartsWith("-"))
                        {
                            inputFiles.Add(args[i]);
                            i++;
                        }
                        break;

                    case "-l":
                    case "--library":
                        libraryPath = RequireValue(args, ref i, arg);
                        i++;
                        break;

                    case "-o":
                    case "--output":
                        outputPath = RequireValue(args, ref i, arg);
                        i++;
                        break;

                    case "--work-dir":
                        workDir = RequireValue(args, ref i, arg);
                        i++;
                        break;

                    case "--output-dir":
                        outputDir = RequireValue(args, ref i, arg);
                        i++;
                        break;

                    case "--cache-dir":
                        cacheDir = RequireValue(args, ref i, arg);
                        i++;
                        break;

                    case "--resolution":
                        resolution = RequireValue(args, ref i, arg).ToLowerInvariant();
                        i++;
                        break;

                    case "--run-fdr":
                        config.RunFdr = ParseDouble(RequireValue(args, ref i, arg), "--run-fdr");
                        i++;
                        break;

                    case "--experiment-fdr":
                        config.ExperimentFdr = ParseDouble(RequireValue(args, ref i, arg), "--experiment-fdr");
                        i++;
                        break;

                    case "--protein-fdr":
                        config.ProteinFdr = ParseDouble(RequireValue(args, ref i, arg), "--protein-fdr");
                        i++;
                        break;

                    case "--threads":
                        config.NThreads = int.Parse(RequireValue(args, ref i, arg));
                        i++;
                        break;

                    case "--fragment-tolerance":
                        fragmentTolerance = ParseDouble(RequireValue(args, ref i, arg), "--fragment-tolerance");
                        i++;
                        break;

                    case "--fragment-unit":
                        fragmentUnit = RequireValue(args, ref i, arg).ToLowerInvariant();
                        i++;
                        break;

                    case "--report":
                        config.OutputReport = RequireValue(args, ref i, arg);
                        i++;
                        break;

                    case "--no-prefilter":
                        config.PrefilterEnabled = false;
                        i++;
                        break;

                    case "--decoys-in-library":
                        // Flat boolean override matching Rust osprey's
                        // --decoys-in-library: flips true, never overrides
                        // a YAML `true` back to false. When set together
                        // with --decoy-pairing-manifest, the pipeline runs
                        // the FDRBench manifest pass first; composition-
                        // based pairing fills whatever the manifest didn't
                        // cover. Hard error if no library entries match
                        // any of DecoyPrefixes / Decoy column / manifest.
                        config.DecoysInLibrary = true;
                        i++;
                        break;

                    case "--decoy-pairing-manifest":
                        // Reject a missing value (end of args) AND reject a
                        // following option token (starts with '-'), which would
                        // otherwise silently consume a sibling flag like
                        // --decoys-in-library as the manifest path.
                        config.DecoyPairingManifestPath = RequireValue(args, ref i, arg);
                        i++;
                        break;

                    case "--write-pin":
                        config.WritePin = true;
                        i++;
                        break;

                    case "-d":
                    case "--diagnostics":
                        config.Diagnostics = true;
                        i++;
                        break;

                    case "--task":
                        // The HPC task selector is resolved + validated in Main's
                        // pre-scan (ResolveTask). Consume the flag + its required
                        // value here; throw on a missing value (mirrors the other
                        // required-value flags) so a bare --task fails fast even
                        // when ParseArgs is exercised directly, outside Main.
                        i++; // consume flag
                        if (i >= args.Length || args[i].StartsWith("-"))
                            throw new ArgumentException(
                                "--task requires a task name (PerFileScoring, FirstJoin, PerFileRescore, or MergeNode).");
                        i++; // consume value
                        break;

                    case "--input-scores":
                        i++;
                        // Consume all following non-flag arguments as parquet
                        // paths or a single directory. Mirrors the -i/--input
                        // pattern. Accumulates across repeated --input-scores
                        // flags so both `--input-scores X Y Z` and
                        // `--input-scores X --input-scores Y` work, matching
                        // Rust's clap Vec<PathBuf>.
                        var scorePaths = new List<string>();
                        if (config.InputScores != null)
                            scorePaths.AddRange(config.InputScores);
                        while (i < args.Length && !args[i].StartsWith("-"))
                        {
                            scorePaths.Add(args[i]);
                            i++;
                        }
                        config.InputScores = ResolveInputScores(scorePaths);
                        break;

                    case "--fdr-method":
                        {
                            string fdrMethodValue = RequireValue(args, ref i, arg);
                            switch (fdrMethodValue.ToLowerInvariant())
                            {
                                case "percolator":
                                    config.FdrMethod = FdrMethod.Percolator;
                                    break;
                                case "simple":
                                    config.FdrMethod = FdrMethod.Simple;
                                    break;
                                default:
                                    LogWarning(string.Format(
                                        "Unknown FDR method '{0}', defaulting to percolator", fdrMethodValue));
                                    config.FdrMethod = FdrMethod.Percolator;
                                    break;
                            }
                            i++;
                        }
                        break;

                    case "--fdr-level":
                        {
                            string fdrLevelValue = RequireValue(args, ref i, arg);
                            switch (fdrLevelValue.ToLowerInvariant())
                            {
                                case "precursor":
                                    config.FdrLevel = FdrLevel.Precursor;
                                    break;
                                case "peptide":
                                    config.FdrLevel = FdrLevel.Peptide;
                                    break;
                                case "both":
                                    config.FdrLevel = FdrLevel.Both;
                                    break;
                                default:
                                    LogWarning(string.Format(
                                        "Unknown FDR level '{0}', defaulting to both", fdrLevelValue));
                                    break;
                            }
                            i++;
                        }
                        break;

                    case "--shared-peptides":
                        {
                            string sharedPeptidesValue = RequireValue(args, ref i, arg);
                            switch (sharedPeptidesValue.ToLowerInvariant())
                            {
                                case "all":
                                    config.SharedPeptides = SharedPeptideMode.All;
                                    break;
                                case "razor":
                                    config.SharedPeptides = SharedPeptideMode.Razor;
                                    break;
                                case "unique":
                                    config.SharedPeptides = SharedPeptideMode.Unique;
                                    break;
                                default:
                                    LogWarning(string.Format(
                                        "Unknown shared peptides mode '{0}', defaulting to all",
                                        sharedPeptidesValue));
                                    break;
                            }
                            i++;
                        }
                        break;

                    case "-h":
                    case "--help":
                        PrintUsage();
                        Environment.Exit(0);
                        break;

                    case "-v":
                    case "--version":
                        Console.WriteLine("OspreySharp v{0}", VERSION);
                        Environment.Exit(0);
                        break;

                    default:
                        // A non-flag token that exists on disk is a positional
                        // input file. Anything else starting with '-' is an
                        // unrecognized option — fail fast (caught by Main) rather
                        // than silently ignoring it, which could run the wrong
                        // pipeline (e.g. a retired --no-join would otherwise be
                        // dropped and the full pipeline would run). The retired
                        // HPC mode flags have no special-case: they are simply
                        // unknown now, replaced by --task <Name>.
                        if (!arg.StartsWith("-") && File.Exists(arg))
                        {
                            inputFiles.Add(arg);
                            i++;
                            break;
                        }
                        if (arg.StartsWith("-"))
                            throw new ArgumentException(string.Format(
                                "Unknown argument: {0}. Run with --help to see valid options.", arg));
                        LogWarning(string.Format("Unknown argument: {0}", arg));
                        i++;
                        break;
                }
            }

            // Apply parsed values to config
            config.InputFiles = inputFiles;

            // --work-dir is a convenience that sets both the derived-artifact
            // output directory and the spectra-cache directory; an explicit
            // --output-dir / --cache-dir overrides the matching component.
            config.OutputDir = outputDir ?? workDir;
            config.CacheDir = cacheDir ?? workDir;

            if (!string.IsNullOrEmpty(libraryPath))
                config.LibrarySource = LibrarySource.FromPath(libraryPath);

            if (!string.IsNullOrEmpty(outputPath))
                config.OutputBlib = outputPath;

            // Resolution mode
            switch (resolution)
            {
                case "unit":
                    config.ResolutionMode = ResolutionMode.UnitResolution;
                    break;
                case "hram":
                    config.ResolutionMode = ResolutionMode.HRAM;
                    break;
                default:
                    config.ResolutionMode = ResolutionMode.Auto;
                    break;
            }

            // Apply unit resolution defaults
            if (config.ResolutionMode == ResolutionMode.UnitResolution)
            {
                if (fragmentUnit == null)
                {
                    config.FragmentTolerance.Unit = ToleranceUnit.Mz;
                    if (!fragmentTolerance.HasValue)
                        config.FragmentTolerance.Tolerance = 0.5;
                }
                config.PrecursorTolerance.Unit = ToleranceUnit.Mz;
                config.PrecursorTolerance.Tolerance = 1.0;
            }

            // Apply explicit fragment tolerance overrides
            if (fragmentTolerance.HasValue)
                config.FragmentTolerance.Tolerance = fragmentTolerance.Value;

            if (fragmentUnit != null)
            {
                switch (fragmentUnit)
                {
                    case "ppm":
                        config.FragmentTolerance.Unit = ToleranceUnit.Ppm;
                        break;
                    case "mz":
                    case "th":
                    case "da":
                        config.FragmentTolerance.Unit = ToleranceUnit.Mz;
                        break;
                    default:
                        LogWarning(string.Format(
                            "Unknown fragment unit '{0}', defaulting to ppm", fragmentUnit));
                        break;
                }
            }

            // Warn on the silent no-op combination `--decoy-pairing-manifest`
            // without `--decoys-in-library`. The manifest path is folded
            // into SearchParameterHash, so it busts the .scores.parquet
            // cache, but the pipeline only consults it inside the
            // library-supplies-decoys branch. Mirrors Rust v26.6.0 (which
            // is also silent here) -- the warning is a C#-only courtesy
            // and does not change the hash or the run behaviour, so it
            // preserves cross-impl byte parity.
            if (!config.DecoysInLibrary &&
                !string.IsNullOrEmpty(config.DecoyPairingManifestPath))
            {
                LogWarning(
                    @"--decoy-pairing-manifest is set without --decoys-in-library; " +
                    @"the manifest will NOT be consulted by the pipeline, but it " +
                    @"still contributes to the search-parameter hash and will " +
                    @"invalidate cached .scores.parquet files. Pass " +
                    @"--decoys-in-library to actually enable library-decoy mode.");
            }

            return config;
        }

        /// <summary>
        /// Consumes the value token following a single-value option flag.
        /// Advances <paramref name="i"/> to the value and returns it; throws if
        /// the value is missing or looks like the next option (starts with '-'),
        /// so e.g. <c>-o -l x</c> fails fast instead of swallowing <c>-l</c> as
        /// the value. Callers advance past the value with their own <c>i++</c>.
        /// </summary>
        private static string RequireValue(string[] args, ref int i, string flag)
        {
            i++;
            if (i >= args.Length || args[i].StartsWith("-"))
                throw new ArgumentException(string.Format("{0} requires a value.", flag));
            return args[i];
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
        /// task name. Internal so OspreySharp.Test can exercise it.
        /// </summary>
        internal static string ResolveTask(string taskName, out HpcTask task)
        {
            if (string.Equals(taskName, "PerFileScoring", StringComparison.OrdinalIgnoreCase))
            {
                task = HpcTask.PerFileScoring;
                return null;
            }
            if (string.Equals(taskName, "FirstJoin", StringComparison.OrdinalIgnoreCase))
            {
                task = HpcTask.FirstJoin;
                return null;
            }
            if (string.Equals(taskName, "PerFileRescore", StringComparison.OrdinalIgnoreCase))
            {
                task = HpcTask.PerFileRescore;
                return null;
            }
            if (string.Equals(taskName, "MergeNode", StringComparison.OrdinalIgnoreCase))
            {
                task = HpcTask.MergeNode;
                return null;
            }
            task = default;
            return string.Format(
                "--task: unknown task '{0}'. Valid tasks: PerFileScoring, FirstJoin, PerFileRescore, MergeNode.",
                taskName);
        }

        /// <summary>
        /// Validate the parsed config against the selected
        /// <see cref="OspreyConfig.SelectedTask"/> (or the default full pipeline
        /// when none was given). When a <c>--task</c> is selected the task is
        /// authoritative: it dictates the input type, and the cross
        /// (e.g. <c>--task PerFileScoring --input-scores</c>) is rejected rather
        /// than silently dispatching the other task. Returns null on success or
        /// an error message string on failure. Does not log warnings (those stay
        /// in <see cref="Main"/>). Internal so OspreySharp.Test can exercise it.
        /// </summary>
        internal static string ValidateArgs(OspreyConfig config)
        {
            bool hasInputScores = config.InputScores != null && config.InputScores.Count > 0;
            bool hasInputFiles = config.InputFiles != null && config.InputFiles.Count > 0;

            if (config.SelectedTask.HasValue)
            {
                switch (config.SelectedTask.Value)
                {
                    case HpcTask.PerFileScoring:
                        // Stage 1-4 worker: mzML in, per-file .scores.parquet out.
                        if (hasInputScores)
                            return "--task PerFileScoring takes -i <mzML>, not --input-scores " +
                                   "(did you mean --task PerFileRescore?).";
                        if (!hasInputFiles)
                            return "--task PerFileScoring requires --input <mzML...>.";
                        if (config.LibrarySource == null)
                            return "--task PerFileScoring requires --library.";
                        return null;

                    case HpcTask.PerFileRescore:
                        // Stage 6 worker: --input-scores in, reconciled per-file out.
                        if (hasInputFiles)
                            return "--task PerFileRescore takes --input-scores, not -i <mzML> " +
                                   "(mzML paths are derived from the parquet stems).";
                        if (!hasInputScores)
                            return "--task PerFileRescore requires --input-scores <path...>.";
                        if (config.LibrarySource == null || string.IsNullOrEmpty(config.OutputBlib))
                            return "--task PerFileRescore requires --library and --output.";
                        return null;

                    case HpcTask.FirstJoin:
                        if (hasInputFiles)
                            return "--task FirstJoin cannot be combined with --input. Use --input-scores instead.";
                        if (!hasInputScores)
                            return "--task FirstJoin requires --input-scores <path...>.";
                        if (config.LibrarySource == null || string.IsNullOrEmpty(config.OutputBlib))
                            return "--task FirstJoin requires --library and --output.";
                        // FirstJoin writes the Stage 5 → Stage 6 boundary file
                        // pair, only meaningful with 2+ siblings to reconcile
                        // against and reconciliation enabled. Reject early.
                        if (config.InputScores.Count < 2)
                            return string.Format(
                                "--task FirstJoin requires --input-scores with 2+ parquet files " +
                                "(got {0}). The Stage 5 → Stage 6 boundary file pair is only meaningful for " +
                                "multi-file fan-back-in.",
                                config.InputScores.Count);
                        if (!config.Reconciliation.Enabled)
                            return "--task FirstJoin requires Reconciliation.Enabled = true " +
                                   "(got false from config). The Stage 5 → Stage 6 boundary file pair is " +
                                   "only meaningful when reconciliation runs.";
                        return null;

                    case HpcTask.MergeNode:
                        if (hasInputFiles)
                            return "--task MergeNode cannot be combined with --input. Use --input-scores instead.";
                        if (!hasInputScores)
                            return "--task MergeNode requires --input-scores <path...>.";
                        if (config.LibrarySource == null || string.IsNullOrEmpty(config.OutputBlib))
                            return "--task MergeNode requires --library and --output.";
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
        /// Expand --input-scores arguments: a single directory becomes the
        /// non-recursive list of *.scores.parquet files in it; explicit file
        /// paths are passed through unchanged. Throws if the directory is
        /// empty or any explicit path doesn't exist.
        ///
        /// Directory mode collects both the Stage 4 <c>*.scores.parquet</c> files
        /// and the Stage 6 <c>*.scores-reconciled.parquet</c> siblings, then
        /// dedupes per stem: for any stem that has both, only the reconciled file
        /// is returned (the authoritative later pass; the <c>--task MergeNode</c>
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
                result.Sort(StringComparer.Ordinal); // unique filenames, no ties
                return result;
            }

            foreach (string p in paths)
            {
                if (!File.Exists(p))
                    throw new ArgumentException(string.Format("--input-scores path not found: {0}", p));
            }
            return paths;
        }

        private static double ParseDouble(string value, string flagName)
        {
            double result;
            if (!double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out result))
            {
                throw new ArgumentException(string.Format(
                    "Invalid value '{0}' for {1}", value, flagName));
            }
            return result;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("OspreySharp - Peptide-centric DIA analysis (.NET port of Osprey)");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("USAGE:");
            Console.Error.WriteLine("    osprey -i <file1.mzML> [file2.mzML ...] -l <library.tsv> -o <output.blib>");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("OPTIONS:");
            Console.Error.WriteLine("    -i, --input <files>           Input mzML file(s)");
            Console.Error.WriteLine("    -l, --library <file>          Spectral library (.tsv, .blib, .elib)");
            Console.Error.WriteLine("    -o, --output <file>           Output blib file");
            Console.Error.WriteLine("    --work-dir <dir>              Write derived artifacts AND the spectra cache here");
            Console.Error.WriteLine("                                 (so input data can be read-only); default: beside input");
            Console.Error.WriteLine("    --output-dir <dir>           Directory for derived artifacts (overrides --work-dir)");
            Console.Error.WriteLine("    --cache-dir <dir>            Directory for the .spectra.bin cache (overrides --work-dir)");
            Console.Error.WriteLine("    --resolution <mode>           Resolution mode: unit, hram, auto (default: auto)");
            Console.Error.WriteLine("    --fragment-tolerance <value>  Fragment m/z tolerance (default: 10)");
            Console.Error.WriteLine("    --fragment-unit <unit>        Fragment tolerance unit: ppm, mz (default: ppm)");
            Console.Error.WriteLine("    --run-fdr <threshold>         Run-level FDR threshold (default: 0.01)");
            Console.Error.WriteLine("    --experiment-fdr <threshold>  Experiment-level FDR threshold (default: 0.01)");
            Console.Error.WriteLine("    --protein-fdr <threshold>     Protein-level FDR threshold (optional)");
            Console.Error.WriteLine("    --threads <count>             Number of threads (default: all cores)");
            Console.Error.WriteLine("    --fdr-method <method>         FDR method: percolator, simple (default: percolator)");
            Console.Error.WriteLine("    --fdr-level <level>           FDR level: precursor, peptide, both (default: precursor)");
            Console.Error.WriteLine("    --shared-peptides <mode>      Shared peptide handling: all, razor, unique (default: all)");
            Console.Error.WriteLine("    --report <file>               Write TSV report to file");
            Console.Error.WriteLine("    --no-prefilter                Disable coelution signal pre-filter");
            Console.Error.WriteLine("    --write-pin                   Write PIN files for external tools");
            Console.Error.WriteLine("    -d, --diagnostics             Write cross-impl bisection dumps (OSPREY_DUMP_* bundle)");
            Console.Error.WriteLine("    --decoys-in-library           Trust decoys already in the spectral library");
            Console.Error.WriteLine("                                    (DIA-NN Decoy column / decoy_/rev_/DECOY_ protein");
            Console.Error.WriteLine("                                    prefix / manifest) instead of generating reverse");
            Console.Error.WriteLine("                                    decoys. Hard error if no decoys are recognised.");
            Console.Error.WriteLine("    --decoy-pairing-manifest <PATH>");
            Console.Error.WriteLine("                                  FDRBench 5-column pairing manifest (TSV) used");
            Console.Error.WriteLine("                                    with --decoys-in-library. Manifest is the");
            Console.Error.WriteLine("                                    authoritative source for peptide_type and");
            Console.Error.WriteLine("                                    (optional) clean protein accessions.");
            Console.Error.WriteLine("    --task <Name>                 HPC: run exactly one pipeline task (one node =");
            Console.Error.WriteLine("                                    one task). Omit for the full pipeline. Names:");
            Console.Error.WriteLine("                                    PerFileScoring  Stages 1-4 per file; -i mzML in,");
            Console.Error.WriteLine("                                                    per-file .scores.parquet out.");
            Console.Error.WriteLine("                                    FirstJoin       Stage 5 join; --input-scores in,");
            Console.Error.WriteLine("                                                    boundary sidecar pair out.");
            Console.Error.WriteLine("                                    PerFileRescore  Stage 6 rescore; --input-scores in,");
            Console.Error.WriteLine("                                                    reconciled per-file parquet out.");
            Console.Error.WriteLine("                                    MergeNode       Stages 7-8; reconciled --input-scores");
            Console.Error.WriteLine("                                                    in, blib out.");
            Console.Error.WriteLine("    --input-scores <paths>        HPC: one or more .scores.parquet files,");
            Console.Error.WriteLine("                                    or a single directory (non-recursive scan).");
            Console.Error.WriteLine("                                    Required for the FirstJoin/PerFileRescore/MergeNode");
            Console.Error.WriteLine("                                    tasks; mutex with --input there.");
            Console.Error.WriteLine("    -h, --help                    Show this help message");
            Console.Error.WriteLine("    -v, --version                 Show version");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("EXAMPLES:");
            Console.Error.WriteLine("    osprey -i sample.mzML -l library.tsv -o results.blib");
            Console.Error.WriteLine("    osprey -i *.mzML -l library.tsv -o results.blib --resolution hram");
            Console.Error.WriteLine("    osprey -i data1.mzML data2.mzML -l lib.tsv -o out.blib --protein-fdr 0.01");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("HPC SPLIT (one node = one --task):");
            Console.Error.WriteLine("    # Per-file scoring worker (one per mzML, in parallel on a cluster):");
            Console.Error.WriteLine("    osprey --task PerFileScoring -i data/file_N.mzML -l ref.blib --resolution hram");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("    # First-join node (Stage 5), then per-file rescore workers (Stage 6),");
            Console.Error.WriteLine("    # then the merge node (Stages 7-8):");
            Console.Error.WriteLine("    osprey --task FirstJoin      --input-scores data/*.scores.parquet -l ref.blib -o experiment.blib");
            Console.Error.WriteLine("    osprey --task PerFileRescore --input-scores data/file_N.scores.parquet -l ref.blib -o experiment.blib");
            Console.Error.WriteLine("    osprey --task MergeNode       --input-scores data/*.scores-reconciled.parquet -l ref.blib -o experiment.blib --protein-fdr 0.01");
        }

        internal static void LogInfo(string message)
        {
            Console.Error.WriteLine("[INFO] {0}", message);
        }

        internal static void LogWarning(string message)
        {
            Console.Error.WriteLine("[WARN] {0}", message);
        }

        internal static void LogError(string message)
        {
            Console.Error.WriteLine("[ERROR] {0}", message);
        }
    }
}
