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
using pwiz.Common.CommandLine;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp
{
    /// <summary>
    /// Declarative command-line model for OspreySharp, built on the shared
    /// <see cref="Argument{TContext}"/> framework in PortableUtil. The argument set is
    /// declared once here (replacing Program.cs's hand-rolled switch and the separately
    /// hand-maintained PrintUsage), and the help text is generated from the declarations
    /// so it cannot drift from the parser.
    ///
    /// OspreySharp keeps its own <see cref="TokenizeAndDispatch"/> rather than adopting the
    /// framework's strict <c>--name=value</c> grammar: it needs short aliases (<c>-i</c>),
    /// space-separated values (<c>--name value</c>), variadic consumption (<c>-i a b c</c>),
    /// and a positional-file fallback. The framework is reused for argument declaration,
    /// grouping, and ascii/unicode/HTML help rendering only. Value coercion and the exact
    /// warning strings stay in the per-argument ProcessValue handlers so the parsed
    /// <see cref="OspreyConfig"/> stays byte-identical with the former switch.
    /// </summary>
    internal class OspreyCommandArgs
    {
        private const int USAGE_WIDTH = 78;

        // Install the host text the PortableUtil help renderer needs (descriptions + table
        // headers). OspreySharp's tokenizer throws its own ArgumentExceptions for value
        // errors, so the framework value-exception message methods are never reached.
        static OspreyCommandArgs()
        {
            ArgUsage.Provider = new OspreyArgUsageProvider();
            // OspreySharp's grammar is space-separated (--name value), not --name=value, so the
            // generated help must render "--name <value>" to match what the tokenizer accepts.
            ArgUsage.ArgumentValueSeparator = @" ";
        }

        // --- Raw parse sinks (applied to the config in ToConfig) ---------------------------
        private readonly List<string> _inputFiles = new List<string>();
        private string _libraryPath;
        private string _outputPath;
        private string _workDir;
        private string _outputDir;
        private string _cacheDir;
        private string _resolution = @"auto";
        private double? _fragmentTolerance;
        private string _fragmentUnit;
        private readonly OspreyConfig _config = new OspreyConfig();

        // --- General I/O ------------------------------------------------------------------
        public static readonly OspreyArgument ARG_INPUT = new OspreyArgument(@"input",
            () => @"<file1.mzML ...>", (c, p) => true) { ShortName = @"i", Variadic = true,
            ProcessVariadic = (c, toks) => { c._inputFiles.AddRange(toks); return true; } };
        public static readonly OspreyArgument ARG_LIBRARY = new OspreyArgument(@"library",
            () => @"<library.tsv|.blib>", (c, p) => c._libraryPath = p.Value) { ShortName = @"l" };
        public static readonly OspreyArgument ARG_OUTPUT = new OspreyArgument(@"output",
            () => @"<output.blib>", (c, p) => c._outputPath = p.Value) { ShortName = @"o" };
        public static readonly OspreyArgument ARG_WORK_DIR = new OspreyArgument(@"work-dir",
            () => @"<dir>", (c, p) => c._workDir = p.Value);
        public static readonly OspreyArgument ARG_OUTPUT_DIR = new OspreyArgument(@"output-dir",
            () => @"<dir>", (c, p) => c._outputDir = p.Value);
        public static readonly OspreyArgument ARG_CACHE_DIR = new OspreyArgument(@"cache-dir",
            () => @"<dir>", (c, p) => c._cacheDir = p.Value);
        public static readonly OspreyArgument ARG_REPORT = new OspreyArgument(@"report",
            () => @"<report.tsv>", (c, p) => c._config.OutputReport = p.Value);

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_GENERAL_IO =
            new ArgumentGroup<OspreyCommandArgs>(() => @"General I/O", true,
                ARG_INPUT, ARG_LIBRARY, ARG_OUTPUT, ARG_WORK_DIR, ARG_OUTPUT_DIR, ARG_CACHE_DIR, ARG_REPORT);

        // --- Scoring & Tolerance ----------------------------------------------------------
        public static readonly OspreyArgument ARG_RESOLUTION = new OspreyArgument(@"resolution",
            new[] { @"unit", @"hram", @"auto" }, (c, p) => c._resolution = p.Value.ToLowerInvariant());
        public static readonly OspreyArgument ARG_FRAGMENT_TOLERANCE = new OspreyArgument(@"fragment-tolerance",
            () => @"<value>", (c, p) => c._fragmentTolerance = ParseDouble(p.Value, @"--fragment-tolerance"));
        public static readonly OspreyArgument ARG_FRAGMENT_UNIT = new OspreyArgument(@"fragment-unit",
            new[] { @"ppm", @"mz" }, (c, p) => c._fragmentUnit = p.Value.ToLowerInvariant());
        public static readonly OspreyArgument ARG_NO_PREFILTER = new OspreyArgument(@"no-prefilter",
            (c, p) => c._config.PrefilterEnabled = false);

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_SCORING =
            new ArgumentGroup<OspreyCommandArgs>(() => @"Scoring & Tolerance", true,
                ARG_RESOLUTION, ARG_FRAGMENT_TOLERANCE, ARG_FRAGMENT_UNIT, ARG_NO_PREFILTER);

        // --- FDR & Protein Inference ------------------------------------------------------
        public static readonly OspreyArgument ARG_RUN_FDR = new OspreyArgument(@"run-fdr",
            () => @"<threshold>", (c, p) => c._config.RunFdr = ParseDouble(p.Value, @"--run-fdr"));
        public static readonly OspreyArgument ARG_EXPERIMENT_FDR = new OspreyArgument(@"experiment-fdr",
            () => @"<threshold>", (c, p) => c._config.ExperimentFdr = ParseDouble(p.Value, @"--experiment-fdr"));
        public static readonly OspreyArgument ARG_PROTEIN_FDR = new OspreyArgument(@"protein-fdr",
            () => @"<threshold>", (c, p) => c._config.ProteinFdr = ParseDouble(p.Value, @"--protein-fdr"));
        public static readonly OspreyArgument ARG_FDR_METHOD = new OspreyArgument(@"fdr-method",
            new[] { @"percolator", @"simple" }, (c, p) =>
            {
                switch (p.Value.ToLowerInvariant())
                {
                    case @"percolator":
                        c._config.FdrMethod = FdrMethod.Percolator;
                        break;
                    case @"simple":
                        c._config.FdrMethod = FdrMethod.Simple;
                        break;
                    default:
                        Program.LogWarning(string.Format(
                            @"Unknown FDR method '{0}', defaulting to percolator", p.Value));
                        c._config.FdrMethod = FdrMethod.Percolator;
                        break;
                }
            });
        public static readonly OspreyArgument ARG_FDR_LEVEL = new OspreyArgument(@"fdr-level",
            new[] { @"precursor", @"peptide", @"both" }, (c, p) =>
            {
                switch (p.Value.ToLowerInvariant())
                {
                    case @"precursor":
                        c._config.FdrLevel = FdrLevel.Precursor;
                        break;
                    case @"peptide":
                        c._config.FdrLevel = FdrLevel.Peptide;
                        break;
                    case @"both":
                        c._config.FdrLevel = FdrLevel.Both;
                        break;
                    default:
                        Program.LogWarning(string.Format(
                            @"Unknown FDR level '{0}', defaulting to both", p.Value));
                        break;
                }
            });
        public static readonly OspreyArgument ARG_SHARED_PEPTIDES = new OspreyArgument(@"shared-peptides",
            new[] { @"all", @"razor", @"unique" }, (c, p) =>
            {
                switch (p.Value.ToLowerInvariant())
                {
                    case @"all":
                        c._config.SharedPeptides = SharedPeptideMode.All;
                        break;
                    case @"razor":
                        c._config.SharedPeptides = SharedPeptideMode.Razor;
                        break;
                    case @"unique":
                        c._config.SharedPeptides = SharedPeptideMode.Unique;
                        break;
                    default:
                        Program.LogWarning(string.Format(
                            @"Unknown shared peptides mode '{0}', defaulting to all", p.Value));
                        break;
                }
            });

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_FDR =
            new ArgumentGroup<OspreyCommandArgs>(() => @"FDR & Protein Inference", true,
                ARG_RUN_FDR, ARG_EXPERIMENT_FDR, ARG_PROTEIN_FDR, ARG_FDR_METHOD, ARG_FDR_LEVEL, ARG_SHARED_PEPTIDES);

        // --- Decoys -----------------------------------------------------------------------
        public static readonly OspreyArgument ARG_DECOYS_IN_LIBRARY = new OspreyArgument(@"decoys-in-library",
            (c, p) => c._config.DecoysInLibrary = true);
        public static readonly OspreyArgument ARG_DECOY_PAIRING_MANIFEST = new OspreyArgument(@"decoy-pairing-manifest",
            () => @"<manifest.tsv>", (c, p) => c._config.DecoyPairingManifestPath = p.Value);
        public static readonly OspreyArgument ARG_WRITE_PIN = new OspreyArgument(@"write-pin",
            (c, p) => c._config.WritePin = true);

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_DECOYS =
            new ArgumentGroup<OspreyCommandArgs>(() => @"Decoys", true,
                ARG_DECOYS_IN_LIBRARY, ARG_DECOY_PAIRING_MANIFEST, ARG_WRITE_PIN);

        // --- Distributed / HPC ------------------------------------------------------------
        // --task is resolved + validated in Program.Main's pre-scan; the tokenizer here only
        // consumes its value (and rejects a missing one). Declared so it appears in help.
        public static readonly OspreyArgument ARG_TASK = new OspreyArgument(@"task",
            new[] { @"PerFileScoring", @"FirstPassFDR", @"PerFileRescoring", @"SecondPassFDR" }, (c, p) => true);
        public static readonly OspreyArgument ARG_INPUT_SCORES = new OspreyArgument(@"input-scores",
            () => @"<paths|dir>", (c, p) => true) { Variadic = true, ProcessVariadic = (c, toks) =>
            {
                // Accumulate across repeated --input-scores flags and re-resolve, matching the
                // former switch exactly (Rust clap Vec<PathBuf>). ResolveInputScores expands a
                // single directory and validates explicit paths.
                var scorePaths = new List<string>();
                if (c._config.InputScores != null)
                    scorePaths.AddRange(c._config.InputScores);
                scorePaths.AddRange(toks);
                c._config.InputScores = Program.ResolveInputScores(scorePaths);
                return true;
            } };
        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_HPC =
            new ArgumentGroup<OspreyCommandArgs>(() => @"Distributed / HPC", true,
                ARG_TASK, ARG_INPUT_SCORES);

        // --- Performance ------------------------------------------------------------------
        // OUTER vs INNER parallelism, kept deliberately separate. --parallel-files is the
        // number of input files scored at once (each file's Stage 1-4 work is independent);
        // --threads is the per-file main-search thread budget, divided across whatever files
        // run concurrently. --parallel-files takes an OPTIONAL value (handled specially in
        // TokenizeAndDispatch): absent = one file at a time, no value = RAM/CPU-aware auto,
        // <N> = exactly N. Unlike the rest of OspreySharp's in-process work this is not HPC
        // (the Rust HPC split fans files across nodes, one file per process), so it gets its
        // own group rather than sitting under Distributed / HPC.
        public static readonly OspreyArgument ARG_PARALLEL_FILES = new OspreyArgument(@"parallel-files",
            () => @"[<N>]", (c, p) =>
            {
                if (string.IsNullOrEmpty(p.Value))
                {
                    c._config.FileParallelism = FileParallelism.Auto;
                }
                else
                {
                    // 0 is the value a user most naturally types to mean "off" --
                    // map it to sequential rather than silently falling through to
                    // auto. Positive N is an explicit concurrent-file count.
                    int n = int.Parse(p.Value);
                    c._config.FileParallelism = n <= 0
                        ? FileParallelism.Sequential
                        : FileParallelism.Explicit(n);
                }
            });
        public static readonly OspreyArgument ARG_THREADS = new OspreyArgument(@"threads",
            () => @"<count>", (c, p) => c._config.NThreads = int.Parse(p.Value));

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_PERFORMANCE =
            new ArgumentGroup<OspreyCommandArgs>(() => @"Performance", true,
                ARG_PARALLEL_FILES, ARG_THREADS);

        // --- Logging ----------------------------------------------------------------------
        // Per-line output decoration and redirection. --timestamp / --memstamp prefix each
        // line written through Program._out (see CommandStatusWriter); --log-file redirects
        // that writer to a file. The "[date]\t{managed}\t{total}\t{msg}" stamp format is
        // consumed by ai/scripts/OspreySharp/perfviz.html.
        public static readonly OspreyArgument ARG_TIMESTAMP = new OspreyArgument(@"timestamp",
            (c, p) => c._config.IsTimeStamped = true);
        public static readonly OspreyArgument ARG_MEMSTAMP = new OspreyArgument(@"memstamp",
            (c, p) => c._config.IsMemStamped = true);
        public static readonly OspreyArgument ARG_LOG_FILE = new OspreyArgument(@"log-file",
            () => @"<path>", (c, p) => c._config.LogFilePath = p.Value);
        public static readonly OspreyArgument ARG_PERF_STATS = new OspreyArgument(@"perf-stats",
            (c, p) => c._config.PerfStats = true);
        public static readonly OspreyArgument ARG_VERBOSE = new OspreyArgument(@"verbose",
            (c, p) => c._config.Verbose = true);

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_LOGGING =
            new ArgumentGroup<OspreyCommandArgs>(() => @"Logging", true,
                ARG_TIMESTAMP, ARG_MEMSTAMP, ARG_LOG_FILE, ARG_PERF_STATS, ARG_VERBOSE);

        // --- Diagnostics & Info -----------------------------------------------------------
        // -h/--help and -v/--version are terminal: the tokenizer renders help / prints the
        // version and exits 0. Their ProcessValue is never invoked. --help accepts an optional
        // format/section value (ascii | unicode | sections | html | <Section>).
        public static readonly OspreyArgument ARG_DIAGNOSTICS = new OspreyArgument(@"diagnostics",
            (c, p) => c._config.Diagnostics = true) { ShortName = @"d" };
        public static readonly OspreyArgument ARG_HELP = new OspreyArgument(@"help",
            (c, p) => true) { ShortName = @"h" };
        public static readonly OspreyArgument ARG_VERSION = new OspreyArgument(@"version",
            (c, p) => true) { ShortName = @"v" };

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_INFO =
            new ArgumentGroup<OspreyCommandArgs>(() => @"Diagnostics & Info", true,
                ARG_DIAGNOSTICS, ARG_HELP, ARG_VERSION);

        public static IEnumerable<IUsageBlock> UsageBlocks
        {
            get
            {
                return new IUsageBlock[]
                {
                    new ParaUsageBlock(@"OspreySharp - Peptide-centric DIA analysis (.NET port of Osprey)"),
                    new ParaUsageBlock(@"USAGE: osprey -i <file1.mzML> [file2.mzML ...] -l <library.tsv> -o <output.blib>"),
                    GROUP_GENERAL_IO,
                    GROUP_SCORING,
                    GROUP_FDR,
                    GROUP_DECOYS,
                    GROUP_PERFORMANCE,
                    GROUP_HPC,
                    GROUP_LOGGING,
                    GROUP_INFO,
                    new ParaUsageBlock(@"EXAMPLES:"),
                    new ParaUsageBlock(@"  osprey -i sample.mzML -l library.tsv -o results.blib"),
                    new ParaUsageBlock(@"  osprey -i *.mzML -l library.tsv -o results.blib --resolution hram"),
                    new ParaUsageBlock(@"HPC SPLIT (one node = one --task): see --task / --input-scores above."),
                };
            }
        }

        public static IEnumerable<OspreyArgument> AllArguments
        {
            get
            {
                return UsageBlocks.OfType<ArgumentGroup<OspreyCommandArgs>>()
                    .SelectMany(g => g.Args).Cast<OspreyArgument>();
            }
        }

        /// <summary>
        /// Parse command-line arguments into an <see cref="OspreyConfig"/>. Same signature
        /// the former Program.ParseArgs exposed, so the existing tests keep working.
        /// </summary>
        internal static OspreyConfig ParseArgs(string[] args)
        {
            var parser = new OspreyCommandArgs();
            parser.TokenizeAndDispatch(args);
            return parser.ToConfig();
        }

        private void TokenizeAndDispatch(string[] args)
        {
            int i = 0;
            while (i < args.Length)
            {
                string arg = args[i];

                // The single-token `--task=Name` form is resolved in Program.Main's pre-scan.
                if (arg.StartsWith(@"--task=", StringComparison.Ordinal))
                {
                    i++;
                    continue;
                }

                OspreyArgument matched = FindByToken(arg);
                if (matched == null)
                {
                    // A non-flag token that exists on disk is a positional input file. Anything
                    // else starting with '-' is unknown and fails fast (caught by Main).
                    if (!arg.StartsWith(@"-") && File.Exists(arg))
                    {
                        _inputFiles.Add(arg);
                        i++;
                        continue;
                    }
                    if (arg.StartsWith(@"-"))
                        throw new ArgumentException(string.Format(
                            @"Unknown argument: {0}. Run with --help to see valid options.", arg));
                    Program.LogWarning(string.Format(@"Unknown argument: {0}", arg));
                    i++;
                    continue;
                }

                if (ReferenceEquals(matched, ARG_HELP))
                {
                    i++;
                    string fmt = i < args.Length && !args[i].StartsWith(@"-") ? args[i] : null;
                    PrintUsage(fmt);
                    Environment.Exit(0);
                    return;
                }
                if (ReferenceEquals(matched, ARG_VERSION))
                {
                    Console.WriteLine(@"OspreySharp v{0}", OspreyVersion.Current);
                    Environment.Exit(0);
                    return;
                }
                if (ReferenceEquals(matched, ARG_TASK))
                {
                    // Consume + require the value; the selector itself is resolved in Main.
                    i++;
                    if (i >= args.Length || args[i].StartsWith(@"-"))
                        throw new ArgumentException(
                            @"--task requires a task name (PerFileScoring, FirstPassFDR, PerFileRescoring, or SecondPassFDR).");
                    i++;
                    continue;
                }
                if (ReferenceEquals(matched, ARG_PARALLEL_FILES))
                {
                    // Optional value: consume the next token as the count ONLY when
                    // it is a non-flag non-negative integer (0 = sequential, N = N
                    // files); otherwise this is auto mode and the token is left for
                    // normal processing (e.g. a trailing positional mzML). Mirrors
                    // the --help [fmt] lookahead.
                    i++;
                    string parallelValue = null;
                    if (i < args.Length && IsNonNegativeInteger(args[i]))
                    {
                        parallelValue = args[i];
                        i++;
                    }
                    matched.ProcessValue(this, new NameValuePair(matched.Name, parallelValue));
                    continue;
                }

                if (matched.Variadic)
                {
                    i++;
                    var toks = new List<string>();
                    while (i < args.Length && !args[i].StartsWith(@"-"))
                    {
                        toks.Add(args[i]);
                        i++;
                    }
                    matched.ProcessVariadic(this, toks);
                    continue;
                }

                if (matched.ValueExample != null)
                {
                    string value = RequireValue(args, ref i, arg);
                    i++;
                    matched.ProcessValue(this, new NameValuePair(matched.Name, value));
                    continue;
                }

                // Pure flag (no value).
                matched.ProcessValue(this, new NameValuePair(matched.Name, null));
                i++;
            }
        }

        private OspreyConfig ToConfig()
        {
            _config.InputFiles = _inputFiles;

            // --work-dir sets both the derived-artifact output directory and the spectra-cache
            // directory; an explicit --output-dir / --cache-dir overrides the matching one.
            _config.OutputDir = _outputDir ?? _workDir;
            _config.CacheDir = _cacheDir ?? _workDir;

            if (!string.IsNullOrEmpty(_libraryPath))
                _config.LibrarySource = LibrarySource.FromPath(_libraryPath);

            if (!string.IsNullOrEmpty(_outputPath))
                _config.OutputBlib = _outputPath;

            switch (_resolution)
            {
                case @"unit":
                    _config.ResolutionMode = ResolutionMode.UnitResolution;
                    break;
                case @"hram":
                    _config.ResolutionMode = ResolutionMode.HRAM;
                    break;
                default:
                    _config.ResolutionMode = ResolutionMode.Auto;
                    break;
            }

            if (_config.ResolutionMode == ResolutionMode.UnitResolution)
            {
                if (_fragmentUnit == null)
                {
                    _config.FragmentTolerance.Unit = ToleranceUnit.Mz;
                    if (!_fragmentTolerance.HasValue)
                        _config.FragmentTolerance.Tolerance = 0.5;
                }
                _config.PrecursorTolerance.Unit = ToleranceUnit.Mz;
                _config.PrecursorTolerance.Tolerance = 1.0;
            }

            if (_fragmentTolerance.HasValue)
                _config.FragmentTolerance.Tolerance = _fragmentTolerance.Value;

            if (_fragmentUnit != null)
            {
                switch (_fragmentUnit)
                {
                    case @"ppm":
                        _config.FragmentTolerance.Unit = ToleranceUnit.Ppm;
                        break;
                    case @"mz":
                    case @"th":
                    case @"da":
                        _config.FragmentTolerance.Unit = ToleranceUnit.Mz;
                        break;
                    default:
                        Program.LogWarning(string.Format(
                            @"Unknown fragment unit '{0}', defaulting to ppm", _fragmentUnit));
                        break;
                }
            }

            if (!_config.DecoysInLibrary &&
                !string.IsNullOrEmpty(_config.DecoyPairingManifestPath))
            {
                Program.LogWarning(
                    @"--decoy-pairing-manifest is set without --decoys-in-library; " +
                    @"the manifest will NOT be consulted by the pipeline, but it " +
                    @"still contributes to the search-parameter hash and will " +
                    @"invalidate cached .scores.parquet files. Pass " +
                    @"--decoys-in-library to actually enable library-decoy mode.");
            }

            return _config;
        }

        private static OspreyArgument FindByToken(string token)
        {
            foreach (var arg in AllArguments)
            {
                if (string.Equals(token, ArgumentBase.ARG_PREFIX + arg.Name, StringComparison.Ordinal))
                    return arg;
                if (arg.ShortName != null && string.Equals(token, @"-" + arg.ShortName, StringComparison.Ordinal))
                    return arg;
            }
            return null;
        }

        /// <summary>
        /// Consumes the value token following a single-value option flag. Advances
        /// <paramref name="i"/> to the value and returns it; throws if the value is missing or
        /// looks like the next option (starts with '-'), so e.g. <c>-o -l x</c> fails fast.
        /// </summary>
        private static string RequireValue(string[] args, ref int i, string flag)
        {
            i++;
            if (i >= args.Length || args[i].StartsWith(@"-"))
                throw new ArgumentException(string.Format(@"{0} requires a value.", flag));
            return args[i];
        }

        /// <summary>
        /// True when <paramref name="token"/> is a plain non-negative integer (no
        /// sign, digits only). Used by the <c>--parallel-files</c> optional-value
        /// lookahead to tell an explicit count (including <c>0</c> = sequential) from
        /// auto-mode-plus-trailing-token; a leading '-' is therefore correctly treated
        /// as the next flag, not a value.
        /// </summary>
        private static bool IsNonNegativeInteger(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;
            foreach (char c in token)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return int.TryParse(token, out int n) && n >= 0;
        }

        private static double ParseDouble(string value, string flagName)
        {
            double result;
            if (!double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out result))
            {
                throw new ArgumentException(string.Format(
                    @"Invalid value '{0}' for {1}", value, flagName));
            }
            return result;
        }

        // --- Help rendering (generated from the declarations; cannot drift) ---------------

        /// <summary>
        /// Writes generated usage help to <paramref name="writer"/> (default stdout, so an explicit
        /// `--help [html]` can be captured with a plain `>` redirect; Main passes stderr for the
        /// no-args usage-error path). <paramref name="formatType"/>: null/"unicode" = unicode tables
        /// (default, like Skyline), "ascii" = lower-128 ascii tables, "sections" = section names,
        /// "html" = HTML, anything else = a section filter.
        /// </summary>
        internal static void PrintUsage(string formatType, TextWriter writer = null)
        {
            writer = writer ?? Console.Out;
            if (string.Equals(formatType, @"html", StringComparison.OrdinalIgnoreCase))
                writer.Write(GenerateUsageHtml());
            else
                writer.Write(BuildUsage(formatType));
        }

        internal static string BuildUsage(string formatType)
        {
            var sb = new StringBuilder();
            if (string.Equals(formatType, @"sections", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var group in UsageBlocks.OfType<ArgumentGroup<OspreyCommandArgs>>())
                    sb.AppendLine(group.Title);
                return sb.ToString();
            }

            string renderFormat;
            if (formatType == null || string.Equals(formatType, @"unicode", StringComparison.OrdinalIgnoreCase))
                renderFormat = null;                            // unicode bordered tables (default, like Skyline)
            else if (string.Equals(formatType, ArgUsage.FORMAT_ASCII, StringComparison.OrdinalIgnoreCase))
                renderFormat = ArgUsage.FORMAT_ASCII;          // ascii (lower-128) tables on request
            else
            {
                // Treat as a section-name filter.
                var group = UsageBlocks.OfType<ArgumentGroup<OspreyCommandArgs>>().FirstOrDefault(
                    g => g.Title.IndexOf(formatType, StringComparison.OrdinalIgnoreCase) >= 0);
                if (group == null)
                    return string.Format(
                        @"No help section matching '{0}' found. Use --help sections to list available sections.{1}",
                        formatType, Environment.NewLine);
                return group.ToString(USAGE_WIDTH, ArgUsage.FORMAT_NO_BORDERS);
            }

            foreach (var block in UsageBlocks)
                sb.Append(block.ToString(USAGE_WIDTH, renderFormat));
            return sb.ToString();
        }

        internal static string GenerateUsageHtml()
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"<html><head>");
            sb.AppendLine(@"<meta charset=""utf-8"">");
            // Self-contained stylesheet (OspreySharp does not reference Skyline, so it cannot call
            // DocumentationGenerator.GetStyleSheetHtml). The rules are copied from that Skyline
            // stylesheet so OspreySharp's generated help matches Skyline's look (cell padding,
            // header shading, section-title size).
            sb.AppendLine(@"<style>");
            sb.AppendLine(@"body { font: .875em/1.35 'Segoe UI','Lucida Grande',Verdana,Arial,Helvetica,sans-serif; }");
            sb.AppendLine(@".RowType { font-size: 1.769em; line-height: 1.3em; font-family: 'Segoe UI Semibold','Segoe UI','Lucida Grande',Verdana,Arial,Helvetica,sans-serif; color: #000; }");
            sb.AppendLine(@"table { border: 1px solid #bbb; border-collapse: collapse; margin-top: 20px; margin-bottom: 20px; }");
            sb.AppendLine(@"th { background-color: #ededed; color: #636363; text-align: left; padding: 10px 8px; font-weight: bold; border: 1px solid #bbb; }");
            sb.AppendLine(@"td { color: #2a2a2a; vertical-align: top; padding: 10px 8px; border: 1px solid #bbb; }");
            sb.AppendLine(@"</style>");
            sb.AppendLine(@"</head><body>");
            foreach (var block in UsageBlocks)
                sb.Append(block.ToHtmlString());
            sb.Append(@"</body></html>");
            return sb.ToString();
        }

        /// <summary>
        /// Inline (not yet localized) description + header provider for OspreySharp. Routing
        /// through the seam means swapping in a .resx later is a one-line change with no edits
        /// to the declarations. OspreySharp's tokenizer raises its own value errors, so the
        /// value-exception message members are never reached.
        /// </summary>
        private class OspreyArgUsageProvider : IArgUsageProvider
        {
            private static readonly Dictionary<string, string> DESCRIPTIONS = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { @"input", @"Input mzML file(s)" },
                { @"library", @"Spectral library (.tsv, .blib)" },
                { @"output", @"Output blib file" },
                { @"work-dir", @"Write derived artifacts AND the spectra cache here (so input data can be read-only); default: beside input" },
                { @"output-dir", @"Directory for derived artifacts (overrides --work-dir)" },
                { @"cache-dir", @"Directory for the .spectra.bin cache (overrides --work-dir)" },
                { @"report", @"Write TSV report to file" },
                { @"resolution", @"Resolution mode (default: auto)" },
                { @"fragment-tolerance", @"Fragment m/z tolerance (default: 10)" },
                { @"fragment-unit", @"Fragment tolerance unit (default: ppm)" },
                { @"no-prefilter", @"Disable coelution signal pre-filter" },
                { @"run-fdr", @"Run-level FDR threshold (default: 0.01)" },
                { @"experiment-fdr", @"Experiment-level FDR threshold (default: 0.01)" },
                { @"protein-fdr", @"Protein-level FDR threshold (optional)" },
                { @"fdr-method", @"FDR method (default: percolator)" },
                { @"fdr-level", @"FDR level (default: precursor)" },
                { @"shared-peptides", @"Shared peptide handling (default: all)" },
                { @"decoys-in-library", @"Trust decoys already in the spectral library instead of generating reverse decoys. Hard error if none are recognised." },
                { @"decoy-pairing-manifest", @"FDRBench 5-column pairing manifest (TSV), used with --decoys-in-library" },
                { @"write-pin", @"Write PIN files for external tools" },
                { @"task", @"HPC: run exactly one pipeline task (one node = one task). Omit for the full pipeline." },
                { @"input-scores", @"HPC: one or more .scores.parquet files, or a single directory (non-recursive). Mutex with --input." },
                { @"parallel-files", @"Input files scored concurrently (OUTER). Absent: one at a time (default). No value: auto from free RAM and cores. <N>: exactly N regardless of RAM/cores. Distinct from --threads." },
                { @"threads", @"Per-file main-search threads (INNER; default: all cores), divided across files run concurrently by --parallel-files" },
                { @"timestamp", @"Prefix each output line with [yyyy/MM/dd HH:mm:ss]" },
                { @"memstamp", @"Prefix each output line with managed and private memory in MB (pair with --timestamp for perfviz)" },
                { @"log-file", @"Write all output to this file instead of stderr" },
                { @"perf-stats", @"Emit machine-parseable [COUNT]/[TIMING]/[STAGE-WALL] lines for perf tools (off by default)" },
                { @"verbose", @"Show implementer-grade detail (e.g. per-fold Percolator iterations) hidden by default" },
                { @"diagnostics", @"Write cross-impl bisection dumps (OSPREY_DUMP_* bundle)" },
                { @"help", @"Show this help message ([ascii|unicode|sections|html|<Section>])" },
                { @"version", @"Show version" },
            };

            public string GetDescription(string argName)
            {
                return DESCRIPTIONS.TryGetValue(argName, out var d) ? d : null;
            }

            public string AppliesToHeader { get { return @"Applies To"; } }
            public string ArgumentHeader { get { return @"Argument"; } }
            public string DescriptionHeader { get { return @"Description"; } }

            // OspreySharp's tokenizer never raises framework value exceptions; these are
            // required by the interface but unreached.
            public string ValueMissingMessage(string argText) { return string.Format(@"{0} requires a value.", argText); }
            public string ValueUnexpectedMessage(string argText) { return string.Format(@"{0} does not take a value.", argText); }
            public string ValueInvalidMessage(string argText, string value, string[] argValues) { return string.Format(@"Invalid value '{0}' for {1}.", value, argText); }
            public string ValueInvalidBoolMessage(string argText, string value) { return string.Format(@"Invalid value '{0}' for {1}.", value, argText); }
            public string ValueInvalidIntMessage(string argText, string value) { return string.Format(@"Invalid value '{0}' for {1}.", value, argText); }
            public string ValueOutOfRangeIntMessage(string argText, int value, int minVal, int maxVal) { return string.Format(@"Value {0} for {1} out of range [{2}, {3}].", value, argText, minVal, maxVal); }
            public string ValueInvalidDoubleMessage(string argText, string value) { return string.Format(@"Invalid value '{0}' for {1}.", value, argText); }
            public string ValueOutOfRangeDoubleMessage(string argText, double value, double minVal, double maxVal) { return string.Format(@"Value {0} for {1} out of range [{2}, {3}].", value, argText, minVal, maxVal); }
            public string ValueInvalidDateMessage(string argText, string value) { return string.Format(@"Invalid value '{0}' for {1}.", value, argText); }
            public string ValueInvalidPathMessage(string argText, string value) { return string.Format(@"Invalid value '{0}' for {1}.", value, argText); }
        }
    }

    /// <summary>
    /// OspreySharp-local <see cref="Argument{TContext}"/> extension carrying the
    /// <see cref="Variadic"/> quirk (greedy multi-token consumption like <c>-i a b c</c>),
    /// which the shared grammar deliberately does not carry. <see cref="ProcessVariadic"/>
    /// receives the whole run of consumed tokens for one flag occurrence.
    /// </summary>
    internal class OspreyArgument : Argument<OspreyCommandArgs>
    {
        public OspreyArgument(string name, Func<OspreyCommandArgs, NameValuePair, bool> processValue)
            : base(name, processValue)
        {
        }

        public OspreyArgument(string name, Action<OspreyCommandArgs, NameValuePair> processValue)
            : base(name, processValue)
        {
        }

        public OspreyArgument(string name, Func<string> valueExample, Func<OspreyCommandArgs, NameValuePair, bool> processValue)
            : base(name, valueExample, processValue)
        {
        }

        public OspreyArgument(string name, Func<string> valueExample, Action<OspreyCommandArgs, NameValuePair> processValue)
            : base(name, valueExample, processValue)
        {
        }

        public OspreyArgument(string name, string[] values, Func<OspreyCommandArgs, NameValuePair, bool> processValue)
            : base(name, values, processValue)
        {
        }

        public OspreyArgument(string name, string[] values, Action<OspreyCommandArgs, NameValuePair> processValue)
            : base(name, values, processValue)
        {
        }

        public bool Variadic { get; set; }
        public Func<OspreyCommandArgs, IReadOnlyList<string>, bool> ProcessVariadic { get; set; }
    }
}
