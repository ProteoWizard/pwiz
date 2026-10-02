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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using pwiz.Common.CommandLine;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey
{
    /// <summary>
    /// Declarative command-line model for Osprey, built on the shared
    /// <see cref="Argument{TContext}"/> framework in PortableUtil. The argument set is
    /// declared once here (replacing Program.cs's hand-rolled switch and the separately
    /// hand-maintained PrintUsage), and the help text is generated from the declarations
    /// so it cannot drift from the parser.
    ///
    /// Osprey keeps its own <see cref="TokenizeAndDispatch"/> rather than adopting the
    /// framework's strict <c>--name=value</c> grammar: it needs short aliases (<c>-i</c>),
    /// space-separated values (<c>--name value</c>), variadic consumption (<c>-i a b c</c>),
    /// and a positional-file fallback. It also accepts the framework's <c>--name=value</c> for
    /// any argument that takes a value (see <see cref="MatchToken"/>). The framework is
    /// reused for argument declaration, grouping, ascii/unicode/HTML help rendering, and
    /// building tokens from the declared
    /// instances (<c>ARG_THREADS + 8</c>, which the tests use). Value coercion and the exact
    /// warning strings stay in the per-argument ProcessValue handlers so the parsed
    /// <see cref="OspreyConfig"/> stays byte-identical with the former switch.
    /// </summary>
    internal class OspreyCommandArgs
    {
        private const int USAGE_WIDTH = 78;

        // Install the host text the PortableUtil help renderer needs (descriptions + table
        // headers). Osprey's tokenizer throws its own ArgumentExceptions for value errors, so
        // at parse time the framework value-exception message methods are not reached; the
        // token BUILDER (ArgumentBase.operator +) does reach ValueUnexpected / ValueInvalid
        // when a test hands a flag a value or a fixed-list argument a value it does not list.
        static OspreyCommandArgs()
        {
            ArgUsage.Provider = new OspreyArgUsageProvider();
            // Osprey's grammar is space-separated (--name value); --name=value is accepted too, but
            // the generated help renders "--name <value>", the form every example uses, and a
            // token built from an instance is "--name value" for the same reason.
            ArgUsage.ArgumentValueSeparator = @" ";
            // ParseInt / ParseDouble read numbers in the invariant culture, so a number a
            // test joins to an argument must render that way too. When Osprey's locale
            // handling is designed, this moves with the parsers.
            ArgUsage.ValueFormatProvider = CultureInfo.InvariantCulture;
        }

        // --- Raw parse sinks (applied to the config in ToConfig) ---------------------------
        private readonly List<string> _inputFiles = new List<string>();
        private readonly List<string> _inputListPaths = new List<string>();
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
        public static readonly OspreyArgument ARG_INPUT = new OspreyArgument(OspreyArgNames.INPUT,
            () => @"<file1.mzML ...>", (c, p) => true) { ShortName = @"i", Variadic = true,
            ProcessVariadic = (c, toks) => { c._inputFiles.AddRange(toks); return true; } };
        // The command line is a BOUNDED resource and -i consumes it at O(files). Measured on the
        // 446-run CHS cohort: 28,621 characters of the 32,767 a Windows CreateProcess accepts -
        // 87% of the limit, at ~62 characters per input path with a deliberately SHORT raw
        // directory. That leaves room for about 66 more files, so the wall is near 512 and a
        // deeper path tree reaches it sooner. Past it the failure is a CreateProcess error or a
        // truncated argument list, neither of which says "too many inputs".
        //
        // --input-list is the answer, and since --input-scores retired it is the ONLY one:
        // that flag used to accept a directory, which is how the HPC tasks avoided the wall.
        // One path per line, blank lines and #-comments ignored, composable with -i and with
        // itself (both append, exactly as repeated -i does).
        public static readonly OspreyArgument ARG_INPUT_LIST = new OspreyArgument(@"input-list",
            () => @"<list.txt>", (c, p) => c._inputListPaths.Add(p.Value)) { DescriptionArgs = () => new object[] { ARG_INPUT.ShortArgumentText } };
        public static readonly OspreyArgument ARG_LIBRARY = new OspreyArgument(OspreyArgNames.LIBRARY,
            () => @"<library.tsv|.blib>", (c, p) => c._libraryPath = p.Value) { ShortName = @"l", DescriptionArgs = () => new object[] { TextUtil.EXT_TSV, LibrarySource.EXT_BLIB } };
        public static readonly OspreyArgument ARG_OUTPUT = new OspreyArgument(OspreyArgNames.OUTPUT,
            () => @"<output.blib>", (c, p) => c._outputPath = p.Value) { ShortName = @"o" };
        public static readonly OspreyArgument ARG_WORK_DIR = new OspreyArgument(@"work-dir",
            () => @"<dir>", (c, p) => c._workDir = p.Value);
        public static readonly OspreyArgument ARG_OUTPUT_DIR = new OspreyArgument(OspreyArgNames.OUTPUT_DIR,
            () => @"<dir>", (c, p) => c._outputDir = p.Value) { DescriptionArgs = () => new object[] { ARG_WORK_DIR.ArgumentText } };
        public static readonly OspreyArgument ARG_CACHE_DIR = new OspreyArgument(OspreyArgNames.CACHE_DIR,
            () => @"<dir>", (c, p) => c._cacheDir = p.Value) { DescriptionArgs = () => new object[] { SpectraCache.EXT, ARG_WORK_DIR.ArgumentText } };
        public static readonly OspreyArgument ARG_REPORT = new OspreyArgument(@"report",
            () => @"<report.tsv>", (c, p) => c._config.OutputReport = p.Value);
        public static readonly OspreyArgument ARG_EXPORT_LIBRARY = new OspreyArgument(@"export-library",
            () => @"<library.blib>", (c, p) => c._config.ExportLibraryBlib = p.Value) { DescriptionArgs = () => new object[] { ARG_LIBRARY.ArgumentText, LibrarySource.EXT_BLIB } };

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_GENERAL_IO =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_General_IO, true,
                ARG_INPUT, ARG_INPUT_LIST, ARG_LIBRARY, ARG_OUTPUT, ARG_WORK_DIR, ARG_OUTPUT_DIR, ARG_CACHE_DIR, ARG_REPORT,
                ARG_EXPORT_LIBRARY);

        // --- Scoring & Tolerance ----------------------------------------------------------
        public static readonly OspreyArgument ARG_RESOLUTION = new OspreyArgument(OspreyArgNames.RESOLUTION,
            new[] { @"unit", @"hram", @"auto" }, (c, p) => c._resolution = p.Value.ToLowerInvariant()) { DescriptionArgs = () => new object[] { @"auto" } };
        public static readonly OspreyArgument ARG_FRAGMENT_TOLERANCE = new OspreyArgument(OspreyArgNames.FRAGMENT_TOLERANCE,
            () => @"<value>", (c, p) => c._fragmentTolerance = ParseDouble(p));
        public static readonly OspreyArgument ARG_FRAGMENT_UNIT = new OspreyArgument(@"fragment-unit",
            new[] { @"ppm", @"mz" }, (c, p) => c._fragmentUnit = p.Value.ToLowerInvariant()) { DescriptionArgs = () => new object[] { @"ppm" } };
        public static readonly OspreyArgument ARG_NO_PREFILTER = new OspreyArgument(@"no-prefilter",
            (c, p) => c._config.PrefilterEnabled = false);

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_SCORING =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Scoring_Tolerance, true,
                ARG_RESOLUTION, ARG_FRAGMENT_TOLERANCE, ARG_FRAGMENT_UNIT, ARG_NO_PREFILTER);

        // --- FDR & Protein Inference ------------------------------------------------------
        public static readonly OspreyArgument ARG_RUN_FDR = new OspreyArgument(@"run-fdr",
            () => @"<threshold>", (c, p) => c._config.RunFdr = ParseDouble(p));
        public static readonly OspreyArgument ARG_EXPERIMENT_FDR = new OspreyArgument(@"experiment-fdr",
            () => @"<threshold>", (c, p) => c._config.ExperimentFdr = ParseDouble(p));
        public static readonly OspreyArgument ARG_RECONCILIATION_COMPACTION_FDR = new OspreyArgument(@"reconciliation-compaction-fdr",
            () => @"<threshold>", (c, p) => c._config.ReconciliationCompactionFdr = ParseDouble(p)) { DescriptionArgs = () => new object[] { ARG_RUN_FDR.ArgumentText } };
        public static readonly OspreyArgument ARG_PROTEIN_FDR = new OspreyArgument(@"protein-fdr",
            () => @"<threshold>", (c, p) => c._config.ProteinFdr = ParseDouble(p));
        public static readonly OspreyArgument ARG_FDR_METHOD = new OspreyArgument(OspreyArgNames.FDR_METHOD,
            new[] { @"percolator", @"gbdt", @"simple" }, (c, p) =>
            {
                switch (p.Value.ToLowerInvariant())
                {
                    case @"percolator":
                        c._config.FdrMethod = FdrMethod.Percolator;
                        break;
                    case @"gbdt":
                    case @"fasttree": // deprecated alias for gbdt (gradient-boosted decision trees)
                        c._config.FdrMethod = FdrMethod.Gbdt;
                        break;
                    case @"simple":
                        c._config.FdrMethod = FdrMethod.Simple;
                        break;
                    default:
                        Program.LogWarning(string.Format(
                            OspreyResources.OspreyCommandArgs_Unknown_FDR_method___0____defaulting_to__1_, p.Value, @"percolator"));
                        c._config.FdrMethod = FdrMethod.Percolator;
                        break;
                }
            }) { DescriptionArgs = () => new object[] { @"percolator" } };
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
                            OspreyResources.OspreyCommandArgs_Unknown_FDR_level___0____defaulting_to__1_, p.Value, @"precursor"));
                        break;
                }
            }) { DescriptionArgs = () => new object[] { @"precursor" } };
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
                            OspreyResources.OspreyCommandArgs_Unknown_shared_peptides_mode___0____defaulting_to__1_, p.Value, @"all"));
                        break;
                }
            }) { DescriptionArgs = () => new object[] { @"all" } };

        public static readonly OspreyArgument ARG_FDRBENCH = new OspreyArgument(@"fdrbench",
            () => @"<input.tsv>", (c, p) => c._config.OutputFdrBench = p.Value) { DescriptionArgs = () => new object[] { ARG_FDR_LEVEL.ArgumentText, FdrBenchInputWriter.COLUMN_SCORE, @"peptide", @"precursor", @"both" } };
        public static readonly OspreyArgument ARG_FDRBENCH_PER_RUN = new OspreyArgument(@"fdrbench-per-run",
            (c, p) => c._config.FdrBenchPerRun = true) { DescriptionArgs = () => new object[] { ARG_FDRBENCH.ArgumentText, FdrBenchInputWriter.COLUMN_RUN } };
        public static readonly OspreyArgument ARG_FDRBENCH_PASS = new OspreyArgument(@"fdrbench-pass",
            new[] { @"1", @"2", @"both" }, (c, p) => c._config.FdrBenchPass = ParseFdrBenchPass(p)) { DescriptionArgs = () => new object[] { ARG_FDRBENCH.ArgumentText,
                FdrBenchInputWriter.PassSuffix(OspreyConfig.FDRBENCH_PASS_1), FdrBenchInputWriter.PassSuffix(OspreyConfig.FDRBENCH_PASS_2), @"both" } };

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_FDR =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_FDR_Protein_Inference, true,
                ARG_RUN_FDR, ARG_EXPERIMENT_FDR, ARG_RECONCILIATION_COMPACTION_FDR, ARG_PROTEIN_FDR, ARG_FDR_METHOD, ARG_FDR_LEVEL, ARG_SHARED_PEPTIDES,
                ARG_FDRBENCH, ARG_FDRBENCH_PER_RUN, ARG_FDRBENCH_PASS);

        // --- Decoys -----------------------------------------------------------------------
        public static readonly OspreyArgument ARG_DECOYS_IN_LIBRARY = new OspreyArgument(OspreyArgNames.DECOYS_IN_LIBRARY,
            (c, p) => c._config.DecoysInLibrary = true);
        public static readonly OspreyArgument ARG_DECOY_PAIRING_MANIFEST = new OspreyArgument(OspreyArgNames.DECOY_PAIRING_MANIFEST,
            () => @"<manifest.tsv>", (c, p) => c._config.DecoyPairingManifestPath = p.Value) { DescriptionArgs = () => new object[] { ARG_DECOYS_IN_LIBRARY.ArgumentText } };
        public static readonly OspreyArgument ARG_WRITE_PIN = new OspreyArgument(@"write-pin",
            (c, p) => c._config.WritePin = true);

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_DECOYS =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Decoys, true,
                ARG_DECOYS_IN_LIBRARY, ARG_DECOY_PAIRING_MANIFEST, ARG_WRITE_PIN);

        // --- Training Export ---------------------------------------------------------------
        // A PerFileRescoring product (docs/22-training-export.md; P17 in
        // docs/00-pipeline-architecture.md). Off, nothing about the run changes; on, it adds
        // one <stem>.training.parquet per run, and adding it to a finished directory writes
        // only the exports and re-scores nothing.
        public static readonly OspreyArgument ARG_TRAINING_EXPORT = new OspreyArgument(OspreyArgNames.TRAINING_EXPORT,
            (c, p) => c._config.TrainingExport.Enabled = true) { DescriptionArgs = () => new object[] { @"<stem>" + TrainingExportParquet.EXT, ARG_TRAINING_EXPORT_MAX_Q.ArgumentText } };
        public static readonly OspreyArgument ARG_TRAINING_EXPORT_MAX_Q = new OspreyArgument(@"training-export-max-q",
            () => @"<q>", (c, p) => c._config.TrainingExport.MaxQ = ParseDouble(p)) { DescriptionArgs = () => new object[] { ARG_TRAINING_EXPORT.ArgumentText, ARG_RUN_FDR.ArgumentText } };
        public static readonly OspreyArgument ARG_TRAINING_EXPORT_CLAIMANT_Q = new OspreyArgument(@"training-export-claimant-q",
            () => @"<q>", (c, p) => c._config.TrainingExport.ClaimantQ = ParseDouble(p)) { DescriptionArgs = () => new object[] { ARG_TRAINING_EXPORT.ArgumentText, TrainingExportConfig.DEFAULT_CLAIMANT_Q } };
        public static readonly OspreyArgument ARG_TRAINING_EXPORT_XICS = new OspreyArgument(@"training-export-xics",
            (c, p) => c._config.TrainingExport.WriteXics = true) { DescriptionArgs = () => new object[] { ARG_TRAINING_EXPORT.ArgumentText } };

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_TRAINING_EXPORT =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Training_Export, true,
                ARG_TRAINING_EXPORT, ARG_TRAINING_EXPORT_MAX_Q, ARG_TRAINING_EXPORT_CLAIMANT_Q, ARG_TRAINING_EXPORT_XICS);

        // --- Distributed / HPC ------------------------------------------------------------
        // --task is resolved + validated by Program, which reads it with FindValue before the
        // full parse; the tokenizer here only consumes its value (and rejects a missing one).
        // Declared so it appears in help.
        // The value list IS the task list, in its --help order, so the help and the
        // resolution cannot disagree; six trivial constructions, once, at type init.
        public static readonly OspreyArgument ARG_TASK = new OspreyArgument(OspreyArgNames.TASK,
            OspreyTasks.Create().All.Select(t => t.Name).ToArray(), (c, p) => true) { DescriptionArgs = () => new object[] { SpectraCacheTask.TASK_NAME, SpectraCache.EXT, ModelDiagnosticsTask.TASK_NAME, ARG_MODEL_DIAGNOSTICS.ArgumentText, TrainingExportTask.TASK_NAME, ARG_TRAINING_EXPORT.ArgumentText } };
        // --input-scores is GONE. It named an input KIND - "you handed me parquets" - which is
        // how the Rust pipeline said "Stage 1-4 is already done"; the C# port says that with
        // --task plus the per-run validity sidecars, and two seams answering one question is
        // what let --task ModelDiagnostics join the pipeline and demand state a diagnostics
        // fold never publishes. Every task now takes -i and derives its parquets from the
        // input stem, which is the direction every other sidecar already derives in.
        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_HPC =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Distributed_HPC, true,
                ARG_TASK);

        // --- Performance ------------------------------------------------------------------
        // OUTER vs INNER parallelism, kept deliberately separate. --parallel-files is the
        // number of input files scored at once (each file's Stage 1-4 work is independent);
        // --threads is the per-file main-search thread budget, divided across whatever files
        // run concurrently. --parallel-files takes an OPTIONAL value (handled specially in
        // TokenizeAndDispatch): absent = one file at a time, no value = RAM/CPU-aware auto,
        // <N> = exactly N. Unlike the rest of Osprey's in-process work this is not HPC
        // (the Rust HPC split fans files across nodes, one file per process), so it gets its
        // own group rather than sitting under Distributed / HPC.
        public static readonly OspreyArgument ARG_PARALLEL_FILES = new OspreyArgument(OspreyArgNames.PARALLEL_FILES,
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
                    int n = ParseInt(p);
                    c._config.FileParallelism = n <= 0
                        ? FileParallelism.Sequential
                        : FileParallelism.Explicit(n);
                }
            }) { DescriptionArgs = () => new object[] { ARG_THREADS.ArgumentText } };
        public static readonly OspreyArgument ARG_THREADS = new OspreyArgument(@"threads",
            () => @"<count>", (c, p) => c._config.NThreads = ParseInt(p)) { DescriptionArgs = () => new object[] { ARG_PARALLEL_FILES.ArgumentText } };

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_PERFORMANCE =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Performance, true,
                ARG_PARALLEL_FILES, ARG_THREADS);

        // --- Logging ----------------------------------------------------------------------
        // Per-line output decoration and redirection. --timestamp / --memstamp prefix each
        // line written through Program._out (see CommandStatusWriter); --log-file redirects
        // that writer to a file. The "[date]\t{managed}\t{total}\t{msg}" stamp format is
        // consumed by ai/scripts/Osprey/perfviz.html.
        public static readonly OspreyArgument ARG_TIMESTAMP = new OspreyArgument(@"timestamp",
            (c, p) => c._config.IsTimeStamped = true) { DescriptionArgs = () => new object[] { @"[yyyy/MM/dd HH:mm:ss]" } };
        public static readonly OspreyArgument ARG_MEMSTAMP = new OspreyArgument(@"memstamp",
            (c, p) => c._config.IsMemStamped = true) { DescriptionArgs = () => new object[] { ARG_TIMESTAMP.ArgumentText } };
        public static readonly OspreyArgument ARG_LOG_FILE = new OspreyArgument(@"log-file",
            () => @"<path>", (c, p) => c._config.LogFilePath = p.Value);
        public static readonly OspreyArgument ARG_PERF_STATS = new OspreyArgument(@"perf-stats",
            (c, p) => c._config.PerfStats = true) { DescriptionArgs = () => new object[] { @"[COUNT], [TIMING], [BENCH], [STAGE-WALL], [PATH], [TRAIN]" } }; // Log tag OK: help text naming the tags
        public static readonly OspreyArgument ARG_VERBOSE = new OspreyArgument(OspreyArgNames.VERBOSE,
            (c, p) => c._config.Verbose = true);

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_LOGGING =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Logging, true,
                ARG_TIMESTAMP, ARG_MEMSTAMP, ARG_LOG_FILE, ARG_PERF_STATS, ARG_VERBOSE);

        // --- Diagnostics & Info -----------------------------------------------------------
        // -h/--help and -v/--version are terminal: the tokenizer renders help / prints the
        // version and exits 0. Their ProcessValue is never invoked. --help accepts an optional
        // format/section value (ascii | unicode | sections | html | <Section>).
        public static readonly OspreyArgument ARG_DIAGNOSTICS = new OspreyArgument(@"diagnostics",
            (c, p) => c._config.Diagnostics = true) { ShortName = @"d" };
        // One flag, everything we know how to show. An opt-in token per expensive panel was built
        // and removed (#4522): the peak co-assignment panel measured 7.3M rows/s, i.e. ~46s on an
        // 82-file Astral run against a 10-hour search, so the cost never justified making anyone
        // choose. Someone who asks for --model-diagnostics wants the diagnostics, not a decision
        // about which ones they can afford - and a panel behind a token nobody remembers is a
        // panel nobody sees, which defeats a diagnostic whose whole purpose is surfacing an effect
        // users do not know to look for.
        public static readonly OspreyArgument ARG_MODEL_DIAGNOSTICS = new OspreyArgument(OspreyArgNames.MODEL_DIAGNOSTICS,
            (c, p) => c._config.ModelDiagnostics = true);
        public static readonly OspreyArgument ARG_HELP = new OspreyArgument(@"help",
            (c, p) => true) { ShortName = @"h", DescriptionArgs = () => new object[] { @"[ascii|unicode|sections|html|<Section>]" } };
        public static readonly OspreyArgument ARG_VERSION = new OspreyArgument(@"version",
            (c, p) => true) { ShortName = @"v" };
        // Skyline's internal --culture: run under a named culture instead of the OS one, for
        // formatting and for resource lookup. Program applies it before anything is written
        // (see Program.RunCommand), which also reports a name .NET does not know; the parser
        // only consumes the value.
        public static readonly OspreyArgument ARG_INTERNAL_CULTURE = new OspreyArgument(@"culture",
            () => @"en|fr|ja|zh-Hans...", (c, p) => true) { InternalUse = true };

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_INFO =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Diagnostics_Info, true,
                ARG_DIAGNOSTICS, ARG_MODEL_DIAGNOSTICS, ARG_HELP, ARG_VERSION, ARG_INTERNAL_CULTURE);

        public static IEnumerable<IUsageBlock> UsageBlocks
        {
            get
            {
                return new IUsageBlock[]
                {
                    new ParaUsageBlock(OspreyResources.OspreyCommandArgs_UsageBlocks_Osprey___Peptide_centric_DIA_analysis__NET_port_of_Osprey_),
                    new ParaUsageBlock(string.Format(OspreyResources.OspreyCommandArgs_UsageBlocks_USAGE___0_,
                        @"osprey -i <file1.mzML> [file2.mzML ...] -l <library.tsv> -o <output.blib>")),
                    GROUP_GENERAL_IO,
                    GROUP_SCORING,
                    GROUP_FDR,
                    GROUP_DECOYS,
                    GROUP_TRAINING_EXPORT,
                    GROUP_PERFORMANCE,
                    GROUP_HPC,
                    GROUP_LOGGING,
                    GROUP_INFO,
                    new ParaUsageBlock(OspreyResources.OspreyCommandArgs_UsageBlocks_EXAMPLES_),
                    new ParaUsageBlock(@"  osprey -i sample.mzML -l library.tsv -o results.blib"),
                    new ParaUsageBlock(@"  osprey -i *.mzML -l library.tsv -o results.blib " + (ARG_RESOLUTION + @"hram")),
                    new ParaUsageBlock(string.Format(OspreyResources.OspreyCommandArgs_UsageBlocks_HPC_SPLIT__one_node___one__0____see__0__above_,
                        ARG_TASK.ArgumentText)),
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

        /// <summary>
        /// The value given to <paramref name="arg"/> on the command line (the last one, if it is
        /// repeated), or null when the argument is absent. Found the way
        /// <see cref="TokenizeAndDispatch"/> finds it - both value forms, and the same
        /// missing-value error - so Program can read <see cref="ARG_TASK"/> and
        /// <see cref="ARG_INTERNAL_CULTURE"/> before the full parse without a second grammar.
        /// </summary>
        internal static string FindValue(string[] args, OspreyArgument arg)
        {
            string value = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (ReferenceEquals(MatchToken(args[i], out string flag, out string inlineValue), arg))
                    value = TakeValue(args, ref i, arg, flag, inlineValue);
            }
            return value;
        }

        /// <summary>
        /// The argument <paramref name="token"/> names, or null. Osprey's own grammar is
        /// <c>--name value</c>; Skyline's <c>--name=value</c> is accepted too, for every argument
        /// that takes a value, and then <paramref name="inlineValue"/> is the text after the
        /// first '=' (possibly empty) - an explicit value, which the caller hands to the
        /// argument and never re-reads as a token of its own. Otherwise it is null.
        /// <paramref name="flag"/> is the argument as typed (<c>-o</c>, <c>--output</c>), for
        /// messages. A flag written with a value (<c>--verbose=1</c>) matches nothing, so it is
        /// reported as an unknown argument.
        /// </summary>
        private static OspreyArgument MatchToken(string token, out string flag, out string inlineValue)
        {
            flag = token;
            inlineValue = null;
            var arg = FindByToken(token);
            if (arg != null)
                return arg;
            int separator = token.StartsWith(ArgumentBase.ARG_PREFIX, StringComparison.Ordinal)
                ? token.IndexOf('=')
                : -1;
            if (separator <= 0)
                return null;
            arg = FindByToken(token.Substring(0, separator));
            if (arg == null || !TakesValue(arg))
                return null;
            flag = token.Substring(0, separator);
            inlineValue = token.Substring(separator + 1);
            return arg;
        }

        /// <summary>
        /// True for an argument the tokenizer reads a value for: every declared value, plus the
        /// optional help format (<c>--help ascii</c>).
        /// </summary>
        private static bool TakesValue(OspreyArgument arg)
        {
            return arg.ValueExample != null || ReferenceEquals(arg, ARG_HELP);
        }

        private void TokenizeAndDispatch(string[] args)
        {
            int i = 0;
            while (i < args.Length)
            {
                string arg = args[i];

                OspreyArgument matched = MatchToken(arg, out string flag, out string inlineValue);
                if (matched == null)
                {
                    // A non-flag token that exists on disk is a positional input file. Anything
                    // else starting with '-' is unknown and fails fast (caught by Main).
                    // Directory.Exists matters as much as File.Exists here: the vendor formats
                    // that are DIRECTORIES (Agilent .d, Bruker .d, Waters .raw) would otherwise
                    // fall through to "Unknown argument" and be silently dropped from the run,
                    // while the same path passed with -i was accepted.
                    if (!arg.StartsWith(@"-") && (File.Exists(arg) || Directory.Exists(arg)))
                    {
                        _inputFiles.Add(arg);
                        i++;
                        continue;
                    }
                    if (arg.StartsWith(@"-"))
                        throw new ArgumentException(string.Format(
                            OspreyResources.OspreyCommandArgs_TokenizeAndDispatch_Unknown_argument___0___Run_with__1__to_see_valid_options_, arg,
                            ARG_HELP.ArgumentText));
                    Program.LogWarning(string.Format(OspreyResources.OspreyCommandArgs_TokenizeAndDispatch_Unknown_argument___0_, arg));
                    i++;
                    continue;
                }

                if (ReferenceEquals(matched, ARG_HELP))
                {
                    i++;
                    string fmt;
                    if (inlineValue != null)
                        fmt = inlineValue.Length > 0 ? inlineValue : null;
                    else
                        fmt = i < args.Length && !args[i].StartsWith(@"-") ? args[i] : null;
                    PrintUsage(fmt);
                    Environment.Exit(0);
                    return;
                }
                if (ReferenceEquals(matched, ARG_VERSION))
                {
                    Console.WriteLine(OspreyResources.Program_Run_Osprey_v_0_, OspreyVersion.DisplayVersion);
                    Environment.Exit(0);
                    return;
                }
                if (ReferenceEquals(matched, ARG_TASK))
                {
                    // Consume + require the value; the selector itself is resolved in Program
                    // (through FindValue, which reads it with this same TakeValue).
                    TakeValue(args, ref i, matched, flag, inlineValue);
                    i++;
                    continue;
                }
                if (ReferenceEquals(matched, ARG_PARALLEL_FILES))
                {
                    // Optional value: consume the next token as the count ONLY when
                    // it is a non-flag non-negative integer (0 = sequential, N = N
                    // files); otherwise this is auto mode and the token is left for
                    // normal processing (e.g. a trailing positional mzML). Mirrors
                    // the --help [fmt] lookahead. A value given inline (--parallel-files=N) is
                    // explicit, so it must BE a count: anything else is an invalid value.
                    i++;
                    string parallelValue = null;
                    if (inlineValue != null)
                    {
                        parallelValue = RequireInlineValue(matched, flag, inlineValue);
                        if (!IsNonNegativeInteger(parallelValue))
                            throw new ArgumentException(InvalidValueMessage(new NameValuePair(matched.Name, parallelValue)));
                    }
                    else if (i < args.Length && IsNonNegativeInteger(args[i]))
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
                    if (inlineValue != null)
                        toks.Add(RequireInlineValue(matched, flag, inlineValue));
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
                    string value = TakeValue(args, ref i, matched, flag, inlineValue);
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
            // Expand --input-list BEFORE normalization, so a listed path is indistinguishable
            // from one given with -i from here on. Appended in the order the lists were given,
            // after any -i, because input ORDER is not decorative: FirstJoin is order-sensitive
            // and the run's file indices follow this list.
            foreach (string listPath in _inputListPaths)
                _inputFiles.AddRange(ReadInputList(listPath));

            for (int i = 0; i < _inputFiles.Count; i++)
                _inputFiles[i] = NormalizeInputPath(_inputFiles[i]);
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
                            OspreyResources.OspreyCommandArgs_Unknown_fragment_unit___0____defaulting_to__1_, _fragmentUnit, @"ppm"));
                        break;
                }
            }

            if (!_config.LibrarySuppliesDecoys &&
                !string.IsNullOrEmpty(_config.DecoyPairingManifestPath))
            {
                Program.LogWarning(string.Format(
                    OspreyResources.OspreyCommandArgs_ToConfig__0__is_set_without__1___so_the_manifest_will_not_be_used,
                    ARG_DECOY_PAIRING_MANIFEST.ArgumentText, ARG_DECOYS_IN_LIBRARY.ArgumentText, ParquetScoreCache.EXT_SCORES));
            }

            if (_config.FdrBenchPerRun && string.IsNullOrEmpty(_config.OutputFdrBench))
            {
                Program.LogWarning(FdrBenchMissingMessage(ARG_FDRBENCH_PER_RUN));
            }

            if (_config.FdrBenchPass != OspreyConfig.FDRBENCH_PASS_2 && string.IsNullOrEmpty(_config.OutputFdrBench))
            {
                Program.LogWarning(FdrBenchMissingMessage(ARG_FDRBENCH_PASS));
            }

            return _config;
        }

        /// <summary>The warning for an FDRBench option given without --fdrbench, which it needs.</summary>
        internal static string FdrBenchMissingMessage(OspreyArgument arg)
        {
            return string.Format(OspreyResources.OspreyCommandArgs_FdrBenchMissingMessage__0__is_set_without__1___no_FDRBench_input_will_be_written__Pass__1___2__to_enable_FDRBench_output_,
                arg.ArgumentText, ARG_FDRBENCH.ArgumentText, ARG_FDRBENCH.ValueExample());
        }

        /// <summary>
        /// Strip a trailing directory separator from an input path. Shell tab completion
        /// adds one for a directory, and the vendor formats this build can read ARE
        /// directories (Agilent .d, Bruker .d, Waters .raw). Left on, the path has no
        /// filename component, so every derived artifact - the .spectra.bin, the
        /// .scores.parquet, the FDR sidecars - loses its stem and is written INSIDE the
        /// bundle. For the cache that is self-defeating as well as untidy: the artifact
        /// then counts toward the bundle's own fingerprint, so the cache never matches
        /// the source it was built from and every run re-parses.
        /// </summary>
        /// <summary>
        /// Read one input path per line from <paramref name="listPath"/>, ignoring blank lines
        /// and <c>#</c> comments. The bounded-command-line alternative to a 446-element
        /// <c>-i</c>; see <see cref="ARG_INPUT_LIST"/>.
        ///
        /// <para>A missing or empty list is FATAL rather than an empty input set. Silently
        /// searching zero files would look like a fast successful run and produce an empty
        /// blib - the shape this codebase refuses everywhere else, and worse here because the
        /// operator's whole cohort is named in the file that was not read.</para>
        /// </summary>
        private static IEnumerable<string> ReadInputList(string listPath)
        {
            if (string.IsNullOrEmpty(listPath) || !File.Exists(listPath))
            {
                throw new FileNotFoundException(string.Format(
                    OspreyResources.OspreyCommandArgs_ReadInputList__0__file_not_found___1_, ARG_INPUT_LIST.ArgumentText, listPath), listPath);
            }
            var paths = new List<string>();
            foreach (string rawLine in File.ReadAllLines(listPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;
                paths.Add(line);
            }
            if (paths.Count == 0)
            {
                throw new InvalidDataException(string.Format(
                    OspreyResources.OspreyCommandArgs_ReadInputList_The__0__file_lists_no_input_files___1_, ARG_INPUT_LIST.ArgumentText, listPath));
            }
            return paths;
        }

        private static string NormalizeInputPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // A bare root ("C:\", "/") trims to something that means a different place, and
            // is never a real input, so leave it exactly as given.
            return string.IsNullOrEmpty(Path.GetFileName(trimmed)) ? path : trimmed;
        }

        private static OspreyArgument FindByToken(string token)
        {
            foreach (var arg in AllArguments)
            {
                if (string.Equals(token, arg.ArgumentText, StringComparison.Ordinal))
                    return arg;
                if (arg.ShortName != null && string.Equals(token, arg.ShortArgumentText, StringComparison.Ordinal))
                    return arg;
            }
            return null;
        }

        /// <summary>
        /// The value of a single-value option: <paramref name="inlineValue"/> when it was given
        /// as <c>--name=value</c>, else the next token. For the next token, advances
        /// <paramref name="i"/> to it, and throws if it is missing or looks like the next option
        /// (starts with '-'), so e.g. <c>-o -l x</c> fails fast.
        /// </summary>
        private static string TakeValue(string[] args, ref int i, OspreyArgument arg, string flag, string inlineValue)
        {
            if (inlineValue != null)
                return RequireInlineValue(arg, flag, inlineValue);
            i++;
            if (i >= args.Length || args[i].StartsWith(@"-", StringComparison.Ordinal))
                throw new ArgumentException(ValueMissingMessage(arg, flag));
            return args[i];
        }

        /// <summary>An inline value, which must not be empty (<c>--output=</c>).</summary>
        private static string RequireInlineValue(OspreyArgument arg, string flag, string inlineValue)
        {
            if (inlineValue.Length == 0)
                throw new ArgumentException(ValueMissingMessage(arg, flag));
            return inlineValue;
        }

        /// <summary>
        /// The missing-value error, naming <paramref name="flag"/> as typed (<c>-o</c> or
        /// <c>--output</c>); a missing <see cref="ARG_TASK"/> value also lists the tasks.
        /// </summary>
        private static string ValueMissingMessage(OspreyArgument arg, string flag)
        {
            return ReferenceEquals(arg, ARG_TASK)
                ? string.Format(OspreyResources.Program_Run__0__requires_a_task_name___1___,
                    ARG_TASK.ArgumentText, string.Join(@", ", ARG_TASK.Values))
                : ArgUsage.Provider.ValueMissingMessage(flag);
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

        /// <summary>
        /// An integer option's value, or an <see cref="ArgumentException"/> naming the flag.
        /// The int.Parse this replaced threw FormatException (or OverflowException), which is
        /// neither caught as a usage error nor legible: `--threads bad` reported "Input string
        /// was not in a correct format." with a stack through the parser, for a typo. Mirrors
        /// <see cref="ParseDouble"/>, which has always done this. The flag named in the
        /// message comes from the pair itself, so no call site spells an option name twice.
        /// </summary>
        private static int ParseInt(NameValuePair p)
        {
            int result;
            if (!int.TryParse(p.Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out result))
            {
                throw new ArgumentException(InvalidValueMessage(p));
            }
            return result;
        }

        /// <summary>
        /// Invariant-culture only, deliberately: <see cref="NameValuePair.ValueDouble"/> tries the
        /// current culture first, and under a locale whose group separator is '.' that reads
        /// <c>0.01</c> as 1. An FDR threshold cannot afford that until Osprey's locale handling
        /// is designed as a whole.
        /// </summary>
        private static double ParseDouble(NameValuePair p)
        {
            double result;
            if (!double.TryParse(p.Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out result))
            {
                throw new ArgumentException(InvalidValueMessage(p));
            }
            return result;
        }

        /// <summary>
        /// The one source of "Invalid value ... for --name" text: the same provider method the
        /// framework's own <see cref="ValueInvalidException"/> uses, with the flag spelled from
        /// the pair, so no handler spells an option name or its value list a second time.
        /// </summary>
        private static string InvalidValueMessage(NameValuePair p, string[] expectedValues = null)
        {
            return ArgUsage.Provider.ValueInvalidMessage(ArgumentBase.ARG_PREFIX + p.Name, p.Value, expectedValues);
        }

        private static int ParseFdrBenchPass(NameValuePair p)
        {
            if (string.Equals(p.Value, @"1", StringComparison.Ordinal))
                return OspreyConfig.FDRBENCH_PASS_1;
            if (string.Equals(p.Value, @"2", StringComparison.Ordinal))
                return OspreyConfig.FDRBENCH_PASS_2;
            if (string.Equals(p.Value, @"both", StringComparison.OrdinalIgnoreCase))
                return OspreyConfig.FDRBENCH_PASS_1 | OspreyConfig.FDRBENCH_PASS_2;
            throw new ArgumentException(InvalidValueMessage(p, ARG_FDRBENCH_PASS.Values));
        }

        // --- Help rendering (generated from the declarations; cannot drift) ---------------

        /// <summary>
        /// The <see cref="OspreyCommandArgUsage"/> key holding an argument's usage text, derived
        /// from its name the way Skyline's CommandArgUsage keys are: <c>input-list</c> -&gt;
        /// <c>_input_list</c>.
        /// </summary>
        internal static string UsageKey(string argName)
        {
            return @"_" + argName.Replace('-', '_');
        }

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
                    return string.Format(OspreyResources.OspreyCommandArgs_BuildUsage_No_help_section_matching___0___found__Use__1__to_list_available_sections_,
                        formatType, ARG_HELP.ArgumentText + @" sections") + Environment.NewLine;
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
            sb.AppendLine(@"<title>" + WebUtility.HtmlEncode(OspreyResources.OspreyCommandArgs_GenerateUsageHtml_Osprey_command_line_usage) + @"</title>");
            sb.AppendLine(@"<meta name=""description"" content=""" + WebUtility.HtmlEncode(string.Format(
                OspreyResources.OspreyCommandArgs_GenerateUsageHtml_Command_line_usage_for_Osprey__the_peptide_centric_DIA_search_tool_from_the_MacCoss_lab,
                SpectraCacheTask.TASK_NAME, ModelDiagnosticsTask.TASK_NAME, ARG_TASK.ArgumentText,
                string.Join(@", ", PerFileScoringTask.TASK_NAME, FirstPassFdrTask.TASK_NAME, PerFileRescoreTask.TASK_NAME,
                    SecondPassFdrTask.TASK_NAME))) + @""">");
            // Self-contained stylesheet (Osprey does not reference Skyline, so it cannot call
            // DocumentationGenerator.GetStyleSheetHtml). The table rules are copied from that Skyline
            // stylesheet so Osprey's generated help matches Skyline's look (cell padding,
            // header shading, section-title size); the heading / pre / link rules are Osprey's
            // own additions for the standalone web page (intro + worked HPC example below).
            sb.AppendLine(@"<style>");
            sb.AppendLine(@"body { font: .875em/1.35 'Segoe UI','Lucida Grande',Verdana,Arial,Helvetica,sans-serif; max-width: 70em; }");
            sb.AppendLine(@"h1 { font-size: 1.6em; font-weight: 600; color: #000; margin: 0 0 .3em; }");
            sb.AppendLine(@".RowType { font-size: 1.769em; line-height: 1.3em; font-family: 'Segoe UI Semibold','Segoe UI','Lucida Grande',Verdana,Arial,Helvetica,sans-serif; color: #000; }");
            sb.AppendLine(@"table { border: 1px solid #bbb; border-collapse: collapse; margin-top: 20px; margin-bottom: 20px; }");
            sb.AppendLine(@"th { background-color: #ededed; color: #636363; text-align: left; padding: 10px 8px; font-weight: bold; border: 1px solid #bbb; }");
            sb.AppendLine(@"td { color: #2a2a2a; vertical-align: top; padding: 10px 8px; border: 1px solid #bbb; }");
            sb.AppendLine(@"pre { background: #f5f5f5; border: 1px solid #e0e0e0; border-radius: 4px; padding: 10px; font-family: Consolas,'Courier New',monospace; font-size: .92em; white-space: pre-wrap; }");
            sb.AppendLine(@"code { font-family: Consolas,'Courier New',monospace; }");
            sb.AppendLine(@"a { color: #1565c0; }");
            sb.AppendLine(@"</style>");
            sb.AppendLine(@"</head><body>");
            AppendUsageHtmlIntro(sb);
            foreach (var block in UsageBlocks)
                sb.Append(block.ToHtmlString());
            AppendUsageHtmlHpcExamples(sb);
            sb.Append(@"</body></html>");
            return sb.ToString();
        }

        // Intro prose for the standalone web page (not part of the terminal --help text, which stays a
        // terse flag reference). As in Skyline's CommandLine.html, the prose comes from resources so the
        // ja and zh-Hans pages are fully translated; markup, argument text and file names are passed in
        // as arguments. Locked against drift by TestCommandLineHelpDocumentation, which regenerates each
        // page and diffs it against the committed Documentation copy.
        private static void AppendUsageHtmlIntro(StringBuilder sb)
        {
            sb.AppendLine(@"<h1>" + WebUtility.HtmlEncode(OspreyResources.OspreyCommandArgs_GenerateUsageHtml_Osprey_command_line_usage) + @"</h1>");
            sb.AppendLine(@"<p>" + Prose(
                OspreyResources.OspreyCommandArgs_AppendUsageHtmlIntro_Osprey_is_a_peptide_centric_DIA_search_tool_from_the_MacCoss_lab,
                Code(LibrarySource.EXT_BLIB)) + @"</p>");
            sb.AppendLine(@"<p>" + Prose(
                OspreyResources.OspreyCommandArgs_AppendUsageHtmlIntro_For_the_pipeline_overview__per_stage_detail__and_how_the_four_distributed_HPC_,
                @"<a href=""https://raw.githack.com/ProteoWizard/pwiz/master/pwiz_tools/Osprey/Osprey-workflow.html"">Osprey-workflow.html</a>",
                Code(@"Osprey " + ARG_HELP.ArgumentText)) + @"</p>");
        }

        // Worked distributed-execution example. Like the intro, this is web-page-only content held in
        // sync with the code by TestCommandLineHelpDocumentation.
        private static void AppendUsageHtmlHpcExamples(StringBuilder sb)
        {
            sb.AppendLine(@"<div class=""RowType"">" + WebUtility.HtmlEncode(
                OspreyResources.OspreyCommandArgs_AppendUsageHtmlHpcExamples_Distributed_execution__HPC_) + @"</div>");
            sb.AppendLine(@"<p>" + Prose(
                OspreyResources.OspreyCommandArgs_AppendUsageHtmlHpcExamples_Run_with_no__0__for_the_whole_pipeline_in_one_process,
                Code(ARG_TASK.ArgumentText), Code(ARG_LIBRARY.ArgumentText), Code(PerFileScoringTask.TASK_NAME),
                Code(FirstPassFdrTask.TASK_NAME), Code(PerFileRescoreTask.TASK_NAME), Code(SecondPassFdrTask.TASK_NAME),
                @"&rarr;") + @"</p>");
            sb.AppendLine(@"<pre>");
            AppendExampleComment(sb, Prose(OspreyResources.OspreyCommandArgs_AppendUsageHtmlHpcExamples_split_1___one_process_per_mzML,
                StemFile(ParquetScoreCache.EXT_SCORES), StemFile(CalibrationIO.EXT)));
            sb.AppendLine(HpcExampleCommandLine(PerFileScoringTask.TASK_NAME, ARG_INPUT.ShortArgumentText, @"s1.mzML"));
            sb.AppendLine();
            AppendExampleComment(sb, ArgUsage.HtmlEncode(OspreyResources.OspreyCommandArgs_AppendUsageHtmlHpcExamples_join_1___one_process_over_ALL_runs));
            sb.AppendLine(HpcExampleCommandLine(FirstPassFdrTask.TASK_NAME, ARG_INPUT_LIST.ArgumentText, @"runs.txt"));
            AppendExampleComment(sb, @"  " + Prose(OspreyResources.OspreyCommandArgs_AppendUsageHtmlHpcExamples_writes_beside_each_parquet___0____1_,
                StemFile(FdrScoresSidecar.EXT_FIRST_PASS), StemFile(ReconciliationFile.EXT)));
            sb.AppendLine();
            AppendExampleComment(sb, ArgUsage.HtmlEncode(OspreyResources.OspreyCommandArgs_AppendUsageHtmlHpcExamples_split_2___one_process_per_file));
            sb.AppendLine(HpcExampleCommandLine(PerFileRescoreTask.TASK_NAME, ARG_INPUT.ShortArgumentText, @"s1.mzML"));
            AppendExampleComment(sb, @"  " + Prose(OspreyResources.OspreyCommandArgs_AppendUsageHtmlHpcExamples_writes___0___and__1__with__2_,
                StemFile(ParquetScoreCache.EXT_SCORES_RECONCILED), StemFile(TrainingExportParquet.EXT), ARG_TRAINING_EXPORT.ArgumentText));
            sb.AppendLine();
            AppendExampleComment(sb, Prose(OspreyResources.OspreyCommandArgs_AppendUsageHtmlHpcExamples_join_2___one_process_over_ALL_runs,
                @"out" + LibrarySource.EXT_BLIB));
            sb.AppendLine(HpcExampleCommandLine(SecondPassFdrTask.TASK_NAME, ARG_INPUT_LIST.ArgumentText, @"runs.txt"));
            sb.AppendLine(@"</pre>");
            sb.AppendLine(@"<p>" + Prose(
                OspreyResources.OspreyCommandArgs_AppendUsageHtmlHpcExamples_EVERY_task_takes__0___naming_the_DATA_files,
                Code(ARG_INPUT.ShortArgumentText), Code(ARG_OUTPUT_DIR.ArgumentText), Code(ARG_INPUT_LIST.ArgumentText),
                Code(ARG_PARALLEL_FILES.ArgumentText), FirstPassFdrTask.TASK_NAME) + @"</p>");
        }

        /// <summary>
        /// A help-page sentence: the translated format string is HTML-encoded before the arguments,
        /// which carry the intentional markup (<c>&lt;code&gt;</c>, links), are substituted.
        /// </summary>
        private static string Prose(string format, params object[] args)
        {
            return string.Format(ArgUsage.HtmlEncode(format), args);
        }

        private static string Code(string text)
        {
            return @"<code>" + WebUtility.HtmlEncode(text) + @"</code>";
        }

        /// <summary>
        /// A per-input file name in the worked example: "&lt;stem&gt;" plus the extension, HTML-encoded.
        /// </summary>
        private static string StemFile(string extension)
        {
            return WebUtility.HtmlEncode(@"<stem>" + extension);
        }

        private static void AppendExampleComment(StringBuilder sb, string comment)
        {
            sb.AppendLine(@"# " + comment);
        }

        /// <summary>
        /// One worker command line of the HPC example, built from the argument declarations so a
        /// renamed argument cannot leave the example stale.
        /// </summary>
        private static string HpcExampleCommandLine(string taskName, string inputArgText, string input)
        {
            return string.Join(@" ", @"Osprey", ARG_TASK + taskName, inputArgText, input,
                ARG_LIBRARY.ShortArgumentText, @"hela.tsv", ARG_OUTPUT.ShortArgumentText, @"out.blib",
                ARG_RESOLUTION + @"unit", ARG_PROTEIN_FDR + @"0.01");
        }

        /// <summary>
        /// Description + header provider for Osprey: argument descriptions from
        /// <see cref="OspreyCommandArgUsage"/>, headers and value errors from OspreyResources.
        /// Osprey's tokenizer raises its own value errors, but it formats them through <see cref="ValueInvalidMessage"/> (see
        /// <see cref="OspreyCommandArgs.InvalidValueMessage"/>), and the token builder
        /// <c>ArgumentBase.operator +</c> raises the framework's ValueUnexpected /
        /// ValueInvalid exceptions, so these message members are live text.
        /// </summary>
        private class OspreyArgUsageProvider : IArgUsageProvider
        {
            /// <summary>
            /// The usage text for <paramref name="argName"/>, looked up at call time (so it follows
            /// the current UI culture) in <see cref="OspreyCommandArgUsage"/> under the key Skyline's
            /// CommandArgUsage uses (see <see cref="UsageKey"/>). An argument whose text names another
            /// argument, a default value or a file extension supplies those through
            /// <see cref="OspreyArgument.DescriptionArgs"/>, so no argument text is translated.
            /// </summary>
            public string GetDescription(string argName)
            {
                string description = OspreyCommandArgUsage.ResourceManager.GetString(UsageKey(argName));
                if (description == null)
                    return null;
                var formatArgs = AllArguments.FirstOrDefault(a => a.Name == argName)?.DescriptionArgs?.Invoke();
                return formatArgs == null ? description : string.Format(description, formatArgs);
            }

            public string AppliesToHeader { get { return OspreyResources.OspreyArgUsageProvider_AppliesToHeader_Applies_To; } }
            public string ArgumentHeader { get { return OspreyResources.OspreyArgUsageProvider_ArgumentHeader_Argument; } }
            public string DescriptionHeader { get { return OspreyResources.OspreyArgUsageProvider_DescriptionHeader_Description; } }

            // ValueInvalidMessage is the text every "Invalid value" error in this file shows;
            // ValueUnexpected / ValueInvalid are what the token builder throws.
            public string ValueMissingMessage(string argText) { return string.Format(OspreyResources.OspreyArgUsageProvider_ValueMissingMessage__0__requires_a_value_, argText); }
            public string ValueUnexpectedMessage(string argText) { return string.Format(OspreyResources.OspreyArgUsageProvider_ValueUnexpectedMessage__0__does_not_take_a_value_, argText); }
            public string ValueInvalidMessage(string argText, string value, string[] argValues)
            {
                return argValues == null
                    ? string.Format(OspreyResources.OspreyArgUsageProvider_ValueInvalidMessage_Invalid_value___0___for__1__, value, argText)
                    : string.Format(OspreyResources.OspreyArgUsageProvider_ValueInvalidMessage_Invalid_value___0___for__1___expected__2___, value, argText, string.Join(@", ", argValues));
            }
            public string ValueInvalidBoolMessage(string argText, string value) { return string.Format(OspreyResources.OspreyArgUsageProvider_ValueInvalidMessage_Invalid_value___0___for__1__, value, argText); }
            public string ValueInvalidIntMessage(string argText, string value) { return string.Format(OspreyResources.OspreyArgUsageProvider_ValueInvalidMessage_Invalid_value___0___for__1__, value, argText); }
            public string ValueOutOfRangeIntMessage(string argText, int value, int minVal, int maxVal) { return string.Format(OspreyResources.OspreyArgUsageProvider_ValueOutOfRangeMessage_Value__0__for__1__out_of_range___2____3___, value, argText, minVal, maxVal); }
            public string ValueInvalidDoubleMessage(string argText, string value) { return string.Format(OspreyResources.OspreyArgUsageProvider_ValueInvalidMessage_Invalid_value___0___for__1__, value, argText); }
            public string ValueOutOfRangeDoubleMessage(string argText, double value, double minVal, double maxVal) { return string.Format(OspreyResources.OspreyArgUsageProvider_ValueOutOfRangeMessage_Value__0__for__1__out_of_range___2____3___, value, argText, minVal, maxVal); }
            public string ValueInvalidDateMessage(string argText, string value) { return string.Format(OspreyResources.OspreyArgUsageProvider_ValueInvalidMessage_Invalid_value___0___for__1__, value, argText); }
            public string ValueInvalidPathMessage(string argText, string value) { return string.Format(OspreyResources.OspreyArgUsageProvider_ValueInvalidMessage_Invalid_value___0___for__1__, value, argText); }
        }
    }

    /// <summary>
    /// Osprey-local <see cref="Argument{TContext}"/> extension carrying the
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

        /// <summary>
        /// Values for the <c>{N}</c> placeholders in this argument's usage text in
        /// <see cref="OspreyCommandArgUsage"/>: other arguments' text, default values, file
        /// extensions - anything that must not be translated. Null when the text has none.
        /// </summary>
        public Func<object[]> DescriptionArgs { get; set; }

        public bool Variadic { get; set; }
        public Func<OspreyCommandArgs, IReadOnlyList<string>, bool> ProcessVariadic { get; set; }
    }
}
