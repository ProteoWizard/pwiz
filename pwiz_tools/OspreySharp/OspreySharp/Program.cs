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

namespace pwiz.OspreySharp
{
    /// <summary>
    /// Command-line entry point for OspreySharp.
    /// Parses CLI arguments and launches the analysis pipeline.
    /// Port of osprey/src/main.rs.
    /// </summary>
    class Program
    {
        // Tracks the Rust Osprey upstream version this OspreySharp port
        // is aligned with. Used in parquet footer metadata; the Phase 3
        // validator requires same major.minor across cross-impl handoff.
        internal const string VERSION = "26.4.0";
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
                // Scan args for the HPC mode flags up front so the
                // mutex error is reported even when --input-scores is missing.
                bool joinOnlyFlag = false;
                bool noJoinFlag = false;
                foreach (string a in args)
                {
                    if (a == "--no-join")
                        noJoinFlag = true;
                    else if (a == "--join-only")
                        joinOnlyFlag = true;
                }
                if (noJoinFlag && joinOnlyFlag)
                {
                    LogError("--no-join and --join-only are mutually exclusive.");
                    return 1;
                }

                OspreyConfig config = ParseArgs(args);
                string err = ValidateArgs(config, noJoinFlag, joinOnlyFlag);
                if (err != null)
                {
                    LogError(err);
                    return 1;
                }
                bool joinOnly = joinOnlyFlag
                                || (config.InputScores != null && config.InputScores.Count > 0);

                // Non-fatal warning: --no-join with --output supplied.
                if (config.NoJoin && !string.IsNullOrEmpty(config.OutputBlib))
                {
                    LogWarning("--no-join: --output is ignored (no blib is written). " +
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

                // Run the pipeline
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
        private static OspreyConfig ParseArgs(string[] args)
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

                    case "--write-pin":
                        config.WritePin = true;
                        i++;
                        break;

                    case "--no-join":
                        config.NoJoin = true;
                        i++;
                        break;

                    case "--join-only":
                        // Sentinel: --input-scores must follow (validated in Main).
                        // No-op here; presence of InputScores is the actual switch.
                        i++;
                        break;

                    case "--input-scores":
                        i++;
                        // Consume all following non-flag arguments as parquet
                        // paths or a single directory. Mirrors the -i/--input
                        // pattern.
                        var scorePaths = new List<string>();
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

            return config;
        }

        /// <summary>
        /// Validate the HPC mode flags (--no-join, --join-only, --input-scores)
        /// against the parsed config. Returns null on success or an error
        /// message string on failure. Does not log warnings (those stay in
        /// <see cref="Main"/>). Internal so OspreySharp.Test can exercise it.
        /// </summary>
        internal static string ValidateArgs(OspreyConfig config, bool noJoinFlag, bool joinOnlyFlag)
        {
            if (noJoinFlag && joinOnlyFlag)
                return "--no-join and --join-only are mutually exclusive.";

            bool hasInputScores = config.InputScores != null && config.InputScores.Count > 0;
            if (joinOnlyFlag && !hasInputScores)
                return "--join-only requires --input-scores <path...>.";

            bool joinOnly = joinOnlyFlag || hasInputScores;

            if (config.NoJoin)
            {
                if (joinOnly)
                    return "--no-join cannot be combined with --input-scores.";
                if (config.InputFiles.Count == 0)
                    return "--no-join requires --input <mzML...>.";
                if (config.LibrarySource == null)
                    return "--no-join requires --library.";
                return null;
            }
            if (joinOnly)
            {
                if (config.InputFiles.Count > 0)
                    return "--join-only (--input-scores) cannot be combined with --input.";
                if (config.LibrarySource == null || string.IsNullOrEmpty(config.OutputBlib))
                    return "--join-only requires --library and --output.";
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
        /// </summary>
        internal static List<string> ResolveInputScores(List<string> paths)
        {
            if (paths == null || paths.Count == 0)
                throw new ArgumentException("--input-scores requires at least one path.");

            if (paths.Count == 1 && Directory.Exists(paths[0]))
            {
                string dir = paths[0];
                string[] found = Directory.GetFiles(dir, "*.scores.parquet", SearchOption.TopDirectoryOnly);
                if (found.Length == 0)
                    throw new ArgumentException(string.Format(
                        "No *.scores.parquet files found in --input-scores directory: {0}", dir));
                Array.Sort(found, StringComparer.Ordinal);
                return new List<string>(found);
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
            Console.Error.WriteLine("    --fdr-level <level>           FDR level: precursor, peptide, both (default: both)");
            Console.Error.WriteLine("    --shared-peptides <mode>      Shared peptide handling: all, razor, unique (default: all)");
            Console.Error.WriteLine("    --report <file>               Write TSV report to file");
            Console.Error.WriteLine("    --no-prefilter                Disable coelution signal pre-filter");
            Console.Error.WriteLine("    --write-pin                   Write PIN files for external tools");
            Console.Error.WriteLine("    --no-join                     HPC: run Stages 1-4 only, write per-file");
            Console.Error.WriteLine("                                    .scores.parquet, no FDR or blib");
            Console.Error.WriteLine("    --join-only                   HPC: skip Stages 1-4, run Stage 5+ from");
            Console.Error.WriteLine("                                    --input-scores parquets");
            Console.Error.WriteLine("    --input-scores <paths>        HPC: one or more .scores.parquet files,");
            Console.Error.WriteLine("                                    or a single directory (non-recursive scan)");
            Console.Error.WriteLine("    -h, --help                    Show this help message");
            Console.Error.WriteLine("    -v, --version                 Show version");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("EXAMPLES:");
            Console.Error.WriteLine("    osprey -i sample.mzML -l library.tsv -o results.blib");
            Console.Error.WriteLine("    osprey -i *.mzML -l library.tsv -o results.blib --resolution hram");
            Console.Error.WriteLine("    osprey -i data1.mzML data2.mzML -l lib.tsv -o out.blib --protein-fdr 0.01");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("HPC SPLIT (per-node scoring + central merge):");
            Console.Error.WriteLine("    # Worker node (one per mzML, in parallel on a cluster):");
            Console.Error.WriteLine("    osprey --no-join -i data/file_N.mzML -l ref.blib --resolution hram");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("    # Merge node (after all workers succeed):");
            Console.Error.WriteLine("    osprey --join-only --input-scores data/*.scores.parquet \\");
            Console.Error.WriteLine("           -l ref.blib -o experiment.blib --protein-fdr 0.01");
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
