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
                // node = one task); it maps to the internal
                // (noJoinFlag, joinOnlyFlag, joinAtPass) tuple that the
                // mode-routing wiring (NormalizeHpcArgs + the four tasks'
                // IsIncluded) already reads. Default (no --task) runs the
                // full straight-through pipeline.
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

                bool joinOnlyFlag = false;
                bool noJoinFlag = false;
                int? joinAtPass = null;
                if (taskName != null)
                {
                    string taskErr = ResolveTask(taskName, out noJoinFlag, out joinOnlyFlag, out joinAtPass);
                    if (taskErr != null)
                    {
                        LogError(taskErr);
                        return 1;
                    }
                }

                string normErr = NormalizeHpcArgs(joinAtPass, ref noJoinFlag, ref joinOnlyFlag, out bool joinOnlyModifier);
                if (normErr != null)
                {
                    LogError(normErr);
                    return 1;
                }

                OspreyConfig config = ParseArgs(args);
                // --task PerFileScoring / PerFileRescore set noJoinFlag; the
                // old --no-join CLI flag used to set config.NoJoin directly in
                // ParseArgs. Carry it across from the resolved tuple now.
                config.NoJoin = noJoinFlag;
                config.StopAfterStage5 = joinOnlyModifier;
                // --join-at-pass=2 sets the strict-reconciled-input gate;
                // the pipeline asserts every --input-scores parquet has
                // osprey.reconciled = "true" via ParquetScoreCache
                // validation. Mirrors Rust's main.rs:613 wiring.
                config.ExpectReconciledInput = joinAtPass.HasValue && joinAtPass.Value == 2;
                string err = ValidateArgs(config, noJoinFlag, joinOnlyFlag, joinOnlyModifier);
                if (err != null)
                {
                    LogError(err);
                    return 1;
                }
                bool joinOnly = joinOnlyFlag
                                || (config.InputScores != null && config.InputScores.Count > 0);

                // Non-fatal warning: --task PerFileScoring with --output
                // supplied — that Stage 1-4 worker mode ignores --output. The
                // rescore worker (--task PerFileRescore, identified by
                // --input-scores) requires --output, so the warning would be
                // incorrect/confusing there.
                if (config.NoJoin && !joinOnly && !string.IsNullOrEmpty(config.OutputBlib))
                {
                    LogWarning("--task PerFileScoring: --output is ignored (no blib is written). " +
                               "Per-file `.scores.parquet` files will be written next to each input mzML.");
                }

                // Validate input files exist on disk (skip in --join-only mode
                // where there are no mzML inputs; --input-scores paths were
                // already validated by ResolveInputScores during parsing).
                if (!joinOnly)
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
                        i++;
                        if (i < args.Length)
                        {
                            libraryPath = args[i];
                            i++;
                        }
                        break;

                    case "-o":
                    case "--output":
                        i++;
                        if (i < args.Length)
                        {
                            outputPath = args[i];
                            i++;
                        }
                        break;

                    case "--resolution":
                        i++;
                        if (i < args.Length)
                        {
                            resolution = args[i].ToLowerInvariant();
                            i++;
                        }
                        break;

                    case "--run-fdr":
                        i++;
                        if (i < args.Length)
                        {
                            config.RunFdr = ParseDouble(args[i], "--run-fdr");
                            i++;
                        }
                        break;

                    case "--experiment-fdr":
                        i++;
                        if (i < args.Length)
                        {
                            config.ExperimentFdr = ParseDouble(args[i], "--experiment-fdr");
                            i++;
                        }
                        break;

                    case "--protein-fdr":
                        i++;
                        if (i < args.Length)
                        {
                            config.ProteinFdr = ParseDouble(args[i], "--protein-fdr");
                            i++;
                        }
                        break;

                    case "--threads":
                        i++;
                        if (i < args.Length)
                        {
                            config.NThreads = int.Parse(args[i]);
                            i++;
                        }
                        break;

                    case "--fragment-tolerance":
                        i++;
                        if (i < args.Length)
                        {
                            fragmentTolerance = ParseDouble(args[i], "--fragment-tolerance");
                            i++;
                        }
                        break;

                    case "--fragment-unit":
                        i++;
                        if (i < args.Length)
                        {
                            fragmentUnit = args[i].ToLowerInvariant();
                            i++;
                        }
                        break;

                    case "--report":
                        i++;
                        if (i < args.Length)
                        {
                            config.OutputReport = args[i];
                            i++;
                        }
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
                        i++;
                        // Reject a missing value (end of args) AND reject
                        // the next token starting with `--` (i.e. the next
                        // option), which would otherwise silently consume
                        // a sibling flag like --decoys-in-library as the
                        // manifest path. Both produce the same usage
                        // error so the user knows the option needs a path.
                        if (i >= args.Length || args[i].StartsWith(@"--", StringComparison.Ordinal))
                        {
                            throw new ArgumentException(
                                @"--decoy-pairing-manifest requires a path argument.");
                        }
                        config.DecoyPairingManifestPath = args[i];
                        i++;
                        break;

                    case "--write-pin":
                        config.WritePin = true;
                        i++;
                        break;

                    case "--task":
                        // The HPC task selector is resolved in Main's pre-scan
                        // (ResolveTask -> the join tuple -> NormalizeHpcArgs).
                        // Consume the flag + its value here so ParseArgs
                        // doesn't fall through to the unknown-flag warning.
                        i++; // consume flag
                        if (i < args.Length && !args[i].StartsWith("-"))
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
                        i++;
                        if (i < args.Length)
                        {
                            switch (args[i].ToLowerInvariant())
                            {
                                case "percolator":
                                    config.FdrMethod = FdrMethod.Percolator;
                                    break;
                                case "simple":
                                    config.FdrMethod = FdrMethod.Simple;
                                    break;
                                default:
                                    LogWarning(string.Format(
                                        "Unknown FDR method '{0}', defaulting to percolator", args[i]));
                                    config.FdrMethod = FdrMethod.Percolator;
                                    break;
                            }
                            i++;
                        }
                        break;

                    case "--fdr-level":
                        i++;
                        if (i < args.Length)
                        {
                            switch (args[i].ToLowerInvariant())
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
                                        "Unknown FDR level '{0}', defaulting to both", args[i]));
                                    break;
                            }
                            i++;
                        }
                        break;

                    case "--shared-peptides":
                        i++;
                        if (i < args.Length)
                        {
                            switch (args[i].ToLowerInvariant())
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
                                        args[i]));
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
                        // Unknown flag -- could be a positional input file
                        if (!arg.StartsWith("-") && File.Exists(arg))
                        {
                            inputFiles.Add(arg);
                        }
                        else
                        {
                            LogWarning(string.Format("Unknown argument: {0}", arg));
                        }
                        i++;
                        break;
                }
            }

            // Apply parsed values to config
            config.InputFiles = inputFiles;

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
        /// Resolve a <c>--task &lt;Name&gt;</c> selector (case-insensitive,
        /// matched against each task's stable <c>Name</c>) into the internal
        /// (<paramref name="noJoinFlag"/>, <paramref name="joinOnlyFlag"/>,
        /// <paramref name="joinAtPass"/>) tuple that <see cref="NormalizeHpcArgs"/>
        /// and the four tasks' <c>IsIncluded</c> already read. One node = one
        /// task on HPC. The mapping is the 1:1 collapse of the retired
        /// <c>--no-join</c> / <c>--join-only</c> / <c>--join-at-pass</c> mode
        /// flags:
        ///   PerFileScoring -> --no-join            (Stages 1-4 per file)
        ///   FirstJoin      -> --join-at-pass=1 --join-only (Stage 5)
        ///   PerFileRescore -> --join-at-pass=1 --no-join   (Stage 6 rescore)
        ///   MergeNode      -> --join-at-pass=2             (Stages 7-8)
        /// <c>--input-scores</c> is an input specifier, not a mode flag, and
        /// is validated separately by <see cref="ValidateArgs"/>.
        ///
        /// Returns null on success, or an error message string for an unknown
        /// task name. Internal so OspreySharp.Test can exercise it.
        /// </summary>
        internal static string ResolveTask(string taskName, out bool noJoinFlag, out bool joinOnlyFlag,
            out int? joinAtPass)
        {
            noJoinFlag = false;
            joinOnlyFlag = false;
            joinAtPass = null;

            if (string.Equals(taskName, "PerFileScoring", StringComparison.OrdinalIgnoreCase))
            {
                noJoinFlag = true;
                return null;
            }
            if (string.Equals(taskName, "FirstJoin", StringComparison.OrdinalIgnoreCase))
            {
                joinOnlyFlag = true;
                joinAtPass = 1;
                return null;
            }
            if (string.Equals(taskName, "PerFileRescore", StringComparison.OrdinalIgnoreCase))
            {
                noJoinFlag = true;
                joinAtPass = 1;
                return null;
            }
            if (string.Equals(taskName, "MergeNode", StringComparison.OrdinalIgnoreCase))
            {
                joinAtPass = 2;
                return null;
            }
            return string.Format(
                "--task: unknown task '{0}'. Valid tasks: PerFileScoring, FirstJoin, PerFileRescore, MergeNode.",
                taskName);
        }

        /// <summary>
        /// Normalize the HPC entry-point + modifier flags before
        /// <see cref="ValidateArgs"/>. Resolves the
        /// (<c>--join-at-pass=&lt;N&gt;</c>, <c>--join-only</c>,
        /// <c>--no-join</c>) triple into the same
        /// (<paramref name="noJoinFlag"/>, <paramref name="joinOnlyFlag"/>)
        /// tuple downstream code already reads.
        ///
        /// Modifier semantics: <c>--join-only</c> runs only the next join
        /// from the entry point; <c>--no-join</c> runs only the per-file
        /// fan-out. <c>--join-at-pass=1</c> selects the post-Stage-4 entry
        /// point; <c>--join-at-pass=2</c> the post-Stage-6 entry point.
        ///
        /// Status (post-PR-2): --join-at-pass=1 with --join-only is now
        /// supported and writes the Stage 5 → Stage 6 boundary file pair
        /// before exiting. Plain --join-at-pass=1 (no modifier) runs
        /// Stages 5-8 from a Stage-4-parquet entry point. The remaining
        /// "not yet implemented" combinations are --join-at-pass=1 with
        /// --no-join (Stage 6 worker mode) and --join-at-pass=2. Mirrors
        /// normalize_hpc_args() in osprey/src/main.rs.
        ///
        /// Returns null on success, or an error message string on failure.
        /// Internal so OspreySharp.Test can exercise it.
        /// </summary>
        internal static string NormalizeHpcArgs(int? joinAtPass, ref bool noJoinFlag, ref bool joinOnlyFlag, out bool joinOnlyModifier)
        {
            joinOnlyModifier = false;

            // Modifiers are mutually exclusive: can't be both per-file-only
            // and join-only simultaneously.
            if (noJoinFlag && joinOnlyFlag)
                return "--no-join and --join-only are mutually exclusive modifiers.";

            // --join-only is a modifier of --join-at-pass=<N>; standalone
            // use has no entry point to modify. The old standalone spelling
            // that meant "run Stages 5-8 from Stage 4 parquets" is now
            // --join-at-pass=1.
            if (joinOnlyFlag && !joinAtPass.HasValue)
            {
                return "--join-only is a modifier and requires --join-at-pass=<N>. " +
                       "To run Stages 5-8 from Stage 4 parquets, use --join-at-pass=1 --input-scores ...";
            }

            if (!joinAtPass.HasValue)
            {
                // Stage 1 entry point (with -i ...). --no-join keeps its
                // existing meaning: do per-file work only = Stages 1-4.
                return null;
            }

            switch (joinAtPass.Value)
            {
                case 1:
                    if (noJoinFlag)
                    {
                        // `--join-at-pass=1 --no-join` is the per-file
                        // rescore worker entry point. noJoinFlag stays
                        // true; joinOnlyFlag stays false. Dispatch in
                        // Main routes to RescoreWorker.Run instead of
                        // the in-process AnalysisPipeline.Run.
                        return null;
                    }
                    // `--join-at-pass=1 --join-only` (modifier present) means
                    // "run only the Stage 5 join phase, write boundary
                    // files, exit before Stage 6 rescore." Plain
                    // `--join-at-pass=1` (no modifier) runs Stages 5-8.
                    // In both cases joinOnlyFlag drives the existing Stage
                    // 5+ entry path; the modifier-vs-plain distinction is
                    // captured separately for the post-planning early
                    // exit decision.
                    joinOnlyModifier = joinOnlyFlag;
                    joinOnlyFlag = true;
                    return null;
                case 2:
                    if (noJoinFlag)
                    {
                        return "--join-at-pass=2 --no-join: per-file Stage 7 worker mode is not implemented (reconciled input + Stage 7-8 in-process is the supported path).";
                    }
                    // `--join-at-pass=2` is the post-Stage-6 entry point.
                    // The pipeline reads reconciled .scores.parquet via
                    // --input-scores plus the per-file
                    // .{1st,2nd}-pass.fdr_scores.bin sidecars, skips
                    // Stages 1-6, and runs Stages 7-8. ExpectReconciledInput
                    // is set on the config after NormalizeHpcArgs by the
                    // caller (Main needs the joinAtPass value to be visible
                    // there). Routes through the same joinOnly path Stage 5+
                    // input-scores uses; the in-pipeline reconciled-parquet
                    // gate enforces the strict input contract.
                    joinOnlyFlag = true;
                    return null;
                default:
                    return string.Format("--join-at-pass must be 1 or 2 (got {0}).", joinAtPass.Value);
            }
        }

        /// <summary>
        /// Validate the resolved HPC task selection (<see cref="ResolveTask"/>
        /// sets the noJoin/joinOnly/joinAtPass tuple; <c>--input-scores</c> is
        /// the input specifier) against the parsed config. Returns null on
        /// success or an error message string on failure. Does not log
        /// warnings (those stay in <see cref="Main"/>). Internal so
        /// OspreySharp.Test can exercise it.
        /// </summary>
        internal static string ValidateArgs(OspreyConfig config, bool noJoinFlag, bool joinOnlyFlag,
            bool joinOnlyModifier)
        {
            if (noJoinFlag && joinOnlyFlag)
                return "--task PerFileScoring and --task FirstJoin are mutually exclusive.";

            bool hasInputScores = config.InputScores != null && config.InputScores.Count > 0;
            if (joinOnlyFlag && !hasInputScores)
                return "--task FirstJoin requires --input-scores <path...>.";

            bool joinOnly = joinOnlyFlag || hasInputScores;

            if (config.NoJoin)
            {
                // Two distinct --no-join task modes (mutually exclusive):
                //   - --task PerFileScoring: Stage 1-4 worker (mzML in,
                //     per-file .scores.parquet out).
                //   - --task PerFileRescore: Stage 6 worker (Stage 4
                //     parquets + boundary files in, reconciled per-file
                //     parquets out).
                // The rescore worker mode is identified by the presence of
                // --input-scores; the Stage 1-4 worker mode is identified
                // by --input. Reject the cross.
                if (hasInputScores)
                {
                    if (config.InputFiles.Count > 0)
                        return "--task PerFileRescore cannot be combined with --input. " +
                               "Use --input-scores; mzML paths are derived from the parquet stems.";
                    if (config.LibrarySource == null || string.IsNullOrEmpty(config.OutputBlib))
                        return "--task PerFileRescore requires --library and --output.";
                    return null;
                }
                if (config.InputFiles.Count == 0)
                    return "--task PerFileScoring requires --input <mzML...>.";
                if (config.LibrarySource == null)
                    return "--task PerFileScoring requires --library.";
                return null;
            }
            if (joinOnly)
            {
                if (config.InputFiles.Count > 0)
                    return "--task FirstJoin cannot be combined with --input. Use --input-scores instead.";
                if (config.LibrarySource == null || string.IsNullOrEmpty(config.OutputBlib))
                    return "--task FirstJoin requires --library and --output.";
                // --task FirstJoin (joinOnlyModifier present) writes the
                // Stage 5 → Stage 6 boundary file pair, which is only
                // meaningful when (a) there are siblings to reconcile against
                // and (b) reconciliation is enabled. Reject early — running
                // Stages 1-5 only to silently produce nothing useful (or to
                // fall through to Stage 8 in single-file misconfigurations)
                // is worse than failing fast with a clear message.
                if (joinOnlyModifier)
                {
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
                }
                return null;
            }

            // Default mode: original required-args checks.
            if (config.InputFiles.Count == 0)
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
        /// is returned (the authoritative later pass; the <c>--join-at-pass=2</c>
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
