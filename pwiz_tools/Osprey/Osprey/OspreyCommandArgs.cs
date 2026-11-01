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
using pwiz.CarafeSharp.Contracts;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Proteome;
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
    /// Osprey parses with the shared <see cref="CommandLineParser{TContext}"/> under its own
    /// <see cref="CommandLineGrammar"/> (<see cref="GRAMMAR"/>): short aliases (<c>-i</c>),
    /// values as the next token (<c>--name value</c>) as well as the framework's
    /// <c>--name=value</c>, variadic consumption (<c>-i a b c</c>), optional counts
    /// (<c>--parallel-files [N]</c>) and a positional-file fallback. The framework is also
    /// reused for argument declaration, grouping, ascii/unicode/HTML help rendering, and
    /// building tokens from the declared instances (<c>ARG_THREADS + 8</c>, which the tests
    /// use). Value coercion and the exact warning strings stay in the per-argument
    /// ProcessValue handlers so the parsed <see cref="OspreyConfig"/> stays byte-identical
    /// with the former switch.
    /// </summary>
    internal class OspreyCommandArgs
    {
        private const int USAGE_WIDTH = 78;

        /// <summary>
        /// Osprey's grammar: the standard <c>--name</c> with <c>-x</c> short aliases, a value
        /// as the next token, any dash-prefixed token ending a value (<c>-o -l x</c> fails
        /// fast), the last of a repeated argument winning, and listed values left to the
        /// handlers, which warn and default rather than refuse.
        /// </summary>
        private static readonly CommandLineGrammar GRAMMAR = new CommandLineGrammar
        {
            ShortPrefix = @"-",
            ValueMayFollow = true,
            ValidatesListedValues = false,
            // The library-build options are CarafeSharp's declarations, spelled with hyphens as
            // Osprey's are; Carafe's underscore spelling (--miss_c) names the same declaration.
            NameSeparatorsEquivalent = true,
        };

        // Install the host text the help renderer and the parser's errors need (descriptions,
        // table headers, value and unknown-argument messages). The token BUILDER
        // (ArgumentBase.operator +) reaches ValueUnexpected / ValueInvalid too, when a test
        // hands a flag a value or a fixed-list argument a value it does not list.
        static OspreyCommandArgs()
        {
            // The settings are process-wide, and Osprey owns its process. The CarafeSharp
            // declarations Osprey hosts parse under Osprey's grammar; Carafe's own is installed
            // only by CarafeSharp.exe.
            ArgUsage.Install(new ArgUsageSettings
            {
                Grammar = GRAMMAR,
                Provider = new OspreyArgUsageProvider(),
                // Osprey's grammar is space-separated (--name value); --name=value is accepted too,
                // but the generated help renders "--name <value>", the form every example uses, and
                // a token built from an instance is "--name value" for the same reason.
                ArgumentValueSeparator = @" ",
                // ParseInt / ParseDouble read numbers in the invariant culture, so a number a test
                // joins to an argument must render that way too. When Osprey's locale handling is
                // designed, this moves with the parsers.
                ValueFormatProvider = CultureInfo.InvariantCulture,
            });
        }

        // --- Raw parse sinks (applied to the config in ToConfig) ---------------------------
        private readonly List<string> _inputFiles = new List<string>();
        private readonly List<string> _inputListPaths = new List<string>();
        private readonly List<string> _trainingInputListPaths = new List<string>();
        private string _libraryPath;
        private string _outputPath;
        private string _workDir;
        private string _outputDir;
        private string _cacheDir;
        private string _resolution = @"hram";
        private double? _fragmentTolerance;
        private string _fragmentUnit;
        private readonly OspreyConfig _config = new OspreyConfig();
        // The library-build options CarafeSharp declares, recorded as a CarafeSharp command line
        // records them and interpreted after the parse (InterpretLibraryBuild), and every
        // library-build option seen, as typed, for the refusal of one given without --fasta.
        private readonly CarafeCommandLine _carafe = CarafeCommandLine.CreateHosted();
        private readonly List<string> _libraryBuildOptionsSeen = new List<string>();
        private readonly List<string> _tuningOptionsSeen = new List<string>();

        // --- Argument value descriptions, as Skyline's CommandArgs writes them -------------
        // Read through these on every call, never captured: tests switch the UI language in-process.
        private const string EXT_FASTA = @".fasta";
        private const string EXT_TXT = @".txt";

        private static string GetPathToFile(string ext)
        {
            return OspreyResources.OspreyCommandArgs_PATH_TO_FILE_path_to_file + ext;
        }

        private static readonly Func<string> PATH_TO_FOLDER = () => OspreyResources.OspreyCommandArgs_PATH_TO_FOLDER;
        private static readonly Func<string> INT_VALUE = () => OspreyResources.OspreyCommandArgs_INT_VALUE;
        private static readonly Func<string> NUM_VALUE = () => OspreyResources.OspreyCommandArgs_NUM_VALUE;
        private static readonly Func<string> THRESHOLD_VALUE = () => OspreyResources.OspreyCommandArgs_THRESHOLD_VALUE;
        private static readonly Func<string> RATIO_VALUE = () => OspreyResources.OspreyCommandArgs_RATIO_VALUE;

        // --- General I/O ------------------------------------------------------------------
        public static readonly OspreyArgument ARG_INPUT = new OspreyArgument(OspreyArgNames.INPUT,
            () => GetPathToFile(string.Empty) + @" ...", (c, p) => c._inputFiles.Add(p.Value)) { ShortName = @"i", Variadic = true };
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
            () => GetPathToFile(EXT_TXT), (c, p) => c._inputListPaths.Add(p.Value)) { DescriptionArgs = () => new object[] { ARG_INPUT.ShortArgumentText } };
        public static readonly OspreyArgument ARG_LIBRARY = new OspreyArgument(OspreyArgNames.LIBRARY,
            () => GetPathToFile(LibrarySource.EXT_BLIB) + @"|" + TextUtil.EXT_TSV, (c, p) => c._libraryPath = p.Value)
            {
                ShortName = @"l",
                DescriptionArgs = () => new object[] { LibrarySource.EXT_BLIB, TextUtil.EXT_TSV, ARG_DECOYS_IN_LIBRARY.ArgumentText,
                    ARG_DECOY_PAIRING_MANIFEST.ArgumentText, ARG_FASTA.ArgumentText }
            };
        public static readonly OspreyArgument ARG_OUTPUT = new OspreyArgument(OspreyArgNames.OUTPUT,
            () => GetPathToFile(LibrarySource.EXT_BLIB), (c, p) => c._outputPath = p.Value)
            {
                ShortName = @"o",
                DescriptionArgs = () => new object[] { LibrarySource.EXT_BLIB,
                    ARG_FASTA.ArgumentText }
            };
        public static readonly OspreyArgument ARG_WORK_DIR = new OspreyArgument(@"work-dir",
            PATH_TO_FOLDER, (c, p) => c._workDir = p.Value)
            { DescriptionArgs = () => new object[] { ARG_OUTPUT_DIR.ArgumentText, ARG_CACHE_DIR.ArgumentText } };
        public static readonly OspreyArgument ARG_OUTPUT_DIR = new OspreyArgument(OspreyArgNames.OUTPUT_DIR,
            PATH_TO_FOLDER, (c, p) => c._outputDir = p.Value)
            { DescriptionArgs = () => new object[] { ARG_WORK_DIR.ArgumentText, ARG_OUTPUT.ShortArgumentText } };
        public static readonly OspreyArgument ARG_CACHE_DIR = new OspreyArgument(OspreyArgNames.CACHE_DIR,
            PATH_TO_FOLDER, (c, p) => c._cacheDir = p.Value)
            {
                Scope = OspreyPropertyScope.search, PropertyType = ArgumentPropertyType.path,
                DescriptionArgs = () => new object[] { SpectraCache.EXT, ARG_WORK_DIR.ArgumentText, TextUtil.EXT_TSV,
                    ARG_TASK.ArgumentText, ARG_OUTPUT_DIR.ArgumentText, LibrarySource.EXT_BLIB }
            };
        public static readonly OspreyArgument ARG_EXPORT_LIBRARY = new OspreyArgument(@"export-library",
            () => GetPathToFile(LibrarySource.EXT_BLIB), (c, p) => c._config.ExportLibraryBlib = p.Value)
            {
                DescriptionArgs = () => new object[] { ARG_LIBRARY.ArgumentText, LibrarySource.EXT_BLIB,
                    ARG_DECOYS_IN_LIBRARY.ArgumentText, ARG_DECOY_PAIRING_MANIFEST.ArgumentText, TextUtil.EXT_TSV }
            };

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_GENERAL_IO =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_General_IO, true,
                ARG_INPUT, ARG_INPUT_LIST, ARG_LIBRARY, ARG_OUTPUT, ARG_WORK_DIR, ARG_OUTPUT_DIR, ARG_CACHE_DIR,
                ARG_EXPORT_LIBRARY);

        // --- Library Build ----------------------------------------------------------------
        // The one command from a protein FASTA to a .blib: --fasta predicts the library with
        // CarafeSharp's models before the search (LibraryBuildWorkflow), in place of -l. Osprey's
        // own options here are what Osprey decides; the library-build parameters - the digest,
        // the modifications, the fragments, the acquisition, the RT model - are CarafeSharp's
        // declarations, presented in the two sections after this one under Osprey's grammar and
        // recorded into a CarafeSharp command line the parser owns. One declaration set, two front
        // ends. The device names are literals rather than TorchDevice's constants, so declaring the
        // option touches no type that loads the torch runtime.
        public static readonly OspreyArgument ARG_FASTA = new OspreyArgument(OspreyArgNames.FASTA,
            () => GetPathToFile(EXT_FASTA), (c, p) => c._config.LibraryBuild.FastaPath = p.Value)
            { DescriptionArgs = () => new object[] { ARG_LIBRARY.ShortArgumentText, LibraryExample, LibraryBuildWorkflow.PREDICTED_SUFFIX_EXAMPLE, ARG_TRAINING_INPUT.ArgumentText } };
        // Skyline's Build Library: the document's precursors as they are, no digest, the output
        // .blib itself. A phase of its own and nothing else, so the search options never apply.
        public static readonly OspreyArgument ARG_PRECURSORS = new OspreyArgument(OspreyArgNames.PRECURSORS,
            () => GetPathToFile(TextUtil.EXT_TSV), (c, p) => c._config.LibraryBuild.PrecursorTablePath = p.Value)
            { DescriptionArgs = () => new object[] { OspreyArgNames.TaskText(LibraryBuildConfig.PREDICT_LIBRARY), ARG_OUTPUT.ShortArgumentText,
                string.Join(@", ", PrecursorTable.COLUMNS), LibrarySource.EXT_BLIB, ARG_MODEL.ArgumentText,
                ARG_TRAINING_INPUT.ArgumentText, ARG_FASTA.ArgumentText } };
        public static readonly OspreyArgument ARG_TRAINING_INPUT = new OspreyArgument(OspreyArgNames.TRAINING_INPUT,
            () => GetPathToFile(string.Empty) + @" ...", (c, p) => c.SetLibraryBuildOption(p, b => b.TrainingInputs.Add(p.Value)))
            { Variadic = true, DescriptionArgs = () => new object[] { ARG_FASTA.ArgumentText, ARG_INPUT.ShortArgumentText, ModelExample, ARG_MODEL.ArgumentText } };
        public static readonly OspreyArgument ARG_TRAINING_INPUT_LIST = new OspreyArgument(OspreyArgNames.TRAINING_INPUT_LIST,
            () => GetPathToFile(EXT_TXT), (c, p) => c.SetLibraryBuildOption(p, b => c._trainingInputListPaths.Add(p.Value)))
            { DescriptionArgs = () => new object[] { ARG_TRAINING_INPUT.ArgumentText, ARG_INPUT_LIST.ArgumentText, ARG_INPUT.ShortArgumentText, ARG_FASTA.ArgumentText } };
        public static readonly OspreyArgument ARG_MODEL = new OspreyArgument(OspreyArgNames.MODEL,
            () => GetPathToFile(CarafeModelFile.EXTENSION), (c, p) => c.SetLibraryBuildOption(p, b => b.ModelPath = p.Value))
            { DescriptionArgs = () => new object[] { ARG_FASTA.ArgumentText, CarafeModelFile.EXTENSION, ARG_TRAINING_INPUT.ArgumentText } };
        public static readonly OspreyArgument ARG_DEVICE = new OspreyArgument(OspreyArgNames.DEVICE,
            new[] { @"auto", @"gpu", @"cpu" }, (c, p) => c.SetLibraryBuildOption(p, b => b.Device = p.Value.ToLowerInvariant()))
            {
                Scope = OspreyPropertyScope.library, DefaultValue = Default(() => OspreyTorchRuntimes.DEVICE_AUTO),
                DescriptionArgs = () => new object[] { ARG_FASTA.ArgumentText, @"auto", @"gpu", @"cpu", ARG_INSTALL_CUDA_RUNTIME.ArgumentText }
            };
        // Where the CUDA runtime lives and how it gets there. Neither needs --fasta: the install is
        // a command of its own, and the folder is where it installs to as well as where a library
        // phase looks. The help names neither the default folder (the user's) nor the download's
        // size (the build's: the Linux package set is a gigabyte larger than Windows'), since the
        // generated help pages are checked in; both are in the log when they matter.
        public static readonly OspreyArgument ARG_TORCH_RUNTIMES = new OspreyArgument(OspreyArgNames.TORCH_RUNTIMES,
            PATH_TO_FOLDER, (c, p) => c._config.LibraryBuild.TorchRuntimesDirectory = p.Value)
            { DescriptionArgs = () => new object[] { ARG_INSTALL_CUDA_RUNTIME.ArgumentText, ARG_DEVICE.ArgumentText } };
        public static readonly OspreyArgument ARG_INSTALL_CUDA_RUNTIME = new OspreyArgument(OspreyArgNames.INSTALL_CUDA_RUNTIME,
            (c, p) => c._config.LibraryBuild.InstallCudaRuntime = true)
            { DescriptionArgs = () => new object[] { ARG_TORCH_RUNTIMES.ArgumentText, ARG_DEVICE.ArgumentText } };
        public static readonly OspreyArgument ARG_ENTRAPMENT = new OspreyArgument(OspreyArgNames.ENTRAPMENT,
            RATIO_VALUE, (c, p) => c.SetLibraryBuildOption(p, b =>
            {
                b.Entrapment = true;
                if (p.Value != null)
                    b.EntrapmentRatio = ParseDouble(p);
            }))
            { OptionalValue = true, OptionalValueFilter = IsNumber, DescriptionArgs = () => new object[] { ARG_FASTA.ArgumentText,
                EntrapmentFastaSettings.MIN_ENTRAPMENT_RATIO, ARG_ENTRAPMENT_FASTA.ArgumentText, LibraryBuildWorkflow.PAIRING_FILE_EXAMPLE } };
        public static readonly OspreyArgument ARG_ENTRAPMENT_FASTA = new OspreyArgument(OspreyArgNames.ENTRAPMENT_FASTA,
            () => GetPathToFile(EXT_FASTA), (c, p) => c.SetLibraryBuildOption(p, b => b.EntrapmentFastaPath = p.Value))
            { DescriptionArgs = () => new object[] { ARG_ENTRAPMENT.ArgumentText } };

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_LIBRARY_BUILD =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Library_Build, true,
                ARG_FASTA, ARG_PRECURSORS, ARG_TRAINING_INPUT, ARG_TRAINING_INPUT_LIST, ARG_MODEL, ARG_DEVICE, ARG_TORCH_RUNTIMES,
                ARG_INSTALL_CUDA_RUNTIME, ARG_ENTRAPMENT, ARG_ENTRAPMENT_FASTA);

        // CarafeSharp's declarations, declared again for Osprey's parser: the name, the value and
        // its listed values come from CarafeSharp, and the handler records the value into the
        // CarafeSharp command line this parser owns. The sections are three, so the help reads as
        // Osprey's does, and their text comes from CarafeSharp's usage resources.
        private static readonly OspreyArgument[] HOSTED_DIGEST_ARGUMENTS = Host(CarafeCommandLine.HostedDigestArguments, OspreyPropertyScope.digest);
        private static readonly OspreyArgument[] HOSTED_PREDICTION_ARGUMENTS = Host(CarafeCommandLine.HostedPredictionArguments, OspreyPropertyScope.library);
        private static readonly OspreyArgument[] HOSTED_TRAINING_ARGUMENTS = Host(CarafeCommandLine.HostedTrainingArguments, OspreyPropertyScope.tuning);

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_LIBRARY_DIGEST =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Library_Digest, true, HOSTED_DIGEST_ARGUMENTS)
            { Preamble = () => string.Format(OspreyResources.OspreyCommandArgs_GROUP_LIBRARY_DIGEST_With__0___what_the_library_holds, ARG_FASTA.ArgumentText) };
        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_LIBRARY_PREDICTION =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Library_Prediction, true, HOSTED_PREDICTION_ARGUMENTS)
            { Preamble = () => string.Format(OspreyResources.OspreyCommandArgs_GROUP_LIBRARY_PREDICTION_With__0___how_the_library_is_predicted, ARG_FASTA.ArgumentText) };
        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_LIBRARY_TUNING =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Library_Tuning, true, HOSTED_TRAINING_ARGUMENTS)
            { Preamble = () => string.Format(OspreyResources.OspreyCommandArgs_GROUP_LIBRARY_TUNING_With__0___how_the_models_are_tuned_on_its_runs, ARG_TRAINING_INPUT.ArgumentText) };

        /// <summary>The input argument as the usage lines and the missing-argument errors show it.</summary>
        internal static string UsageInput
        {
            get { return ARG_INPUT.ShortArgumentText + @" " + GetPathToFile(string.Empty) + @" ..."; }
        }

        /// <summary>The library argument as the usage lines and the missing-argument errors show it.</summary>
        internal static string UsageLibrary
        {
            get { return ARG_LIBRARY.ShortArgumentText + @" " + GetPathToFile(LibrarySource.EXT_BLIB) + @"|" + TextUtil.EXT_TSV; }
        }

        /// <summary>The output argument as the usage lines and the missing-argument errors show it.</summary>
        internal static string UsageOutput
        {
            get { return ARG_OUTPUT.ShortArgumentText + @" " + GetPathToFile(LibrarySource.EXT_BLIB); }
        }

        /// <summary>The library's other form, the one command's FASTA, as the usage lines and errors show it.</summary>
        internal static string UsageFasta
        {
            get { return ARG_FASTA.ArgumentText + @" " + GetPathToFile(EXT_FASTA); }
        }

        /// <summary>The library the one command writes beside its output, in the help's spelling.</summary>
        private static string LibraryExample
        {
            get { return @"<output>" + LibraryBuildWorkflow.PREDICTED_SUFFIX + LibrarySource.EXT_BLIB; }
        }

        /// <summary>The tuned model the one command writes beside its output, in the help's spelling.</summary>
        private static string ModelExample
        {
            get { return @"<output>" + CarafeModelFile.EXTENSION; }
        }

        /// <summary>Every CarafeSharp declaration Osprey hosts, in help order.</summary>
        public static IEnumerable<OspreyArgument> HostedArguments
        {
            get { return HOSTED_DIGEST_ARGUMENTS.Concat(HOSTED_PREDICTION_ARGUMENTS).Concat(HOSTED_TRAINING_ARGUMENTS); }
        }

        /// <summary>
        /// The <paramref name="declarations"/> as Osprey's arguments, properties of <paramref name="scope"/>:
        /// the tuning ones need <see cref="ARG_TRAINING_INPUT"/>, the rest <see cref="ARG_FASTA"/>.
        /// </summary>
        private static OspreyArgument[] Host(IEnumerable<ArgumentBase> declarations, OspreyPropertyScope scope)
        {
            bool tuning = scope == OspreyPropertyScope.tuning;
            return declarations.Select(d => new OspreyArgument(d, (c, p) =>
            {
                c._carafe.Record(p);
                c._libraryBuildOptionsSeen.Add(p.Match.ArgumentText);
                if (tuning)
                    c._tuningOptionsSeen.Add(p.Match.ArgumentText);
            }) { Scope = scope }).ToArray();
        }

        /// <summary>
        /// Records one of Osprey's own library-build options, and that it was given; <paramref name="tuning"/>
        /// for one that needs <see cref="ARG_TRAINING_INPUT"/>.
        /// </summary>
        private void SetLibraryBuildOption(NameValuePair p, Action<LibraryBuildConfig> set, bool tuning = false)
        {
            set(_config.LibraryBuild);
            _libraryBuildOptionsSeen.Add(p.Match.ArgumentText);
            if (tuning)
                _tuningOptionsSeen.Add(p.Match.ArgumentText);
        }

        // --- Scoring & Tolerance ----------------------------------------------------------
        public static readonly OspreyArgument ARG_RESOLUTION = new OspreyArgument(OspreyArgNames.RESOLUTION,
            new[] { @"hram", @"unit" }, (c, p) => c._resolution = p.Value.ToLowerInvariant())
            { Scope = OspreyPropertyScope.search, DefaultValue = Default(() => @"hram"), DescriptionArgs = () => new object[] { @"hram", @"unit" } };
        public static readonly OspreyArgument ARG_FRAGMENT_TOLERANCE = new OspreyArgument(OspreyArgNames.FRAGMENT_TOLERANCE,
            NUM_VALUE, (c, p) => c._fragmentTolerance = ParseDouble(p))
            { Scope = OspreyPropertyScope.search, PropertyType = ArgumentPropertyType.number, MinValue = 0, DescriptionArgs = () => new object[] { ARG_FRAGMENT_UNIT.ArgumentText, ARG_RESOLUTION.ArgumentText, @"unit" } };
        public static readonly OspreyArgument ARG_FRAGMENT_UNIT = new OspreyArgument(@"fragment-unit",
            new[] { @"ppm", @"mz" }, (c, p) => c._fragmentUnit = p.Value.ToLowerInvariant())
            { Scope = OspreyPropertyScope.search, DescriptionArgs = () => new object[] { ARG_FRAGMENT_TOLERANCE.ArgumentText, ARG_RESOLUTION.ArgumentText, @"unit" } };
        public static readonly OspreyArgument ARG_NO_PREFILTER = new OspreyArgument(@"no-prefilter",
            (c, p) => c._config.PrefilterEnabled = false) { Scope = OspreyPropertyScope.search };
        public static readonly OspreyArgument ARG_DEMUX = new OspreyArgument(OspreyArgNames.DEMUX,
            new[] { @"off", @"auto" }, (c, p) => c._config.DemuxMode =
                p.Value.ToLowerInvariant() == @"auto" ? DemuxMode.auto : DemuxMode.off)
            { Scope = OspreyPropertyScope.search, DefaultValue = Default(() => new OspreyConfig().DemuxMode), DescriptionArgs = () => new object[] { @"off" } };
        public static readonly OspreyArgument ARG_CENTROID = new OspreyArgument(OspreyArgNames.CENTROID,
            new[] { @"vendor", @"centrix" }, (c, p) => c._config.CentroidMode =
                p.Value.ToLowerInvariant() == @"centrix" ? CentroidMode.centrix : CentroidMode.vendor) { DescriptionArgs = () => new object[] { @"vendor" } };
        public static readonly OspreyArgument ARG_CENTRIX_SIGMA_MS1 = new OspreyArgument(OspreyArgNames.CENTRIX_SIGMA_MS1,
            () => @"<Th>", (c, p) => c._config.CentrixSigmaMs1 = ParseDouble(p)) { DescriptionArgs = () => new object[] { ARG_CENTROID.ArgumentText } };
        public static readonly OspreyArgument ARG_CENTRIX_SIGMA_MS2 = new OspreyArgument(OspreyArgNames.CENTRIX_SIGMA_MS2,
            () => @"<Th>", (c, p) => c._config.CentrixSigmaMs2 = ParseDouble(p)) { DescriptionArgs = () => new object[] { ARG_CENTROID.ArgumentText } };
        public static readonly OspreyArgument ARG_CENTRIX_SOLVER = new OspreyArgument(OspreyArgNames.CENTRIX_SOLVER,
            new[] { @"descent", @"active-set" }, (c, p) => c._config.CentrixActiveSet = p.Value.ToLowerInvariant() == @"active-set")
            { DescriptionArgs = () => new object[] { ARG_CENTROID.ArgumentText, @"descent" } };

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_SCORING =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Scoring_Tolerance, true,
                ARG_RESOLUTION, ARG_FRAGMENT_TOLERANCE, ARG_FRAGMENT_UNIT, ARG_NO_PREFILTER, ARG_DEMUX, ARG_CENTROID,
                ARG_CENTRIX_SIGMA_MS1, ARG_CENTRIX_SIGMA_MS2, ARG_CENTRIX_SOLVER);

        // --- FDR & Protein Inference ------------------------------------------------------
        public static readonly OspreyArgument ARG_RUN_FDR = new OspreyArgument(@"run-fdr",
            THRESHOLD_VALUE, (c, p) => c._config.RunFdr = ParseDouble(p))
            { Scope = OspreyPropertyScope.search, PropertyType = ArgumentPropertyType.number, DefaultValue = Default(() => new OspreyConfig().RunFdr), MinValue = 0, MaxValue = 1 };
        public static readonly OspreyArgument ARG_EXPERIMENT_FDR = new OspreyArgument(OspreyArgNames.EXPERIMENT_FDR,
            THRESHOLD_VALUE, (c, p) => c._config.ExperimentFdr = ParseDouble(p))
            {
                Scope = OspreyPropertyScope.search, PropertyType = ArgumentPropertyType.number, DefaultValue = Default(() => new OspreyConfig().ExperimentFdr), MinValue = 0, MaxValue = 1,
                DescriptionArgs = () => new object[] { ARG_FDR_LEVEL.ArgumentText }
            };
        public static readonly OspreyArgument ARG_RECONCILIATION_COMPACTION_FDR = new OspreyArgument(OspreyArgNames.RECONCILIATION_COMPACTION_FDR,
            THRESHOLD_VALUE, (c, p) => c._config.ReconciliationCompactionFdr = ParseDouble(p))
            {
                Scope = OspreyPropertyScope.search, PropertyType = ArgumentPropertyType.number, DefaultValue = Default(() => new OspreyConfig().ReconciliationCompactionFdr), MinValue = 0, MaxValue = 1,
                DescriptionArgs = () => new object[] { ARG_RUN_FDR.ArgumentText }
            };
        public static readonly OspreyArgument ARG_PROTEIN_FDR = new OspreyArgument(OspreyArgNames.PROTEIN_FDR,
            THRESHOLD_VALUE, (c, p) => c._config.ProteinFdr = ParseDouble(p))
            { Scope = OspreyPropertyScope.search, PropertyType = ArgumentPropertyType.number, DefaultValue = Default(() => OspreyConfig.DefaultProteinFdr), MinValue = 0, MaxValue = 1 };
        // --fdr-method is GONE (#4543), with no alias: it is rejected as an unknown argument.
        // Its percolator and gbdt values chose the classifier inside the one Percolator
        // framework, a developer lever that belongs in an environment variable, so the choice
        // moved to OSPREY_FDR_MODEL (see ToConfig). Its third value, simple, was deleted.
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
            }) { Scope = OspreyPropertyScope.search, DefaultValue = Default(() => new OspreyConfig().FdrLevel), DescriptionArgs = () => new object[] { @"precursor", ARG_EXPERIMENT_FDR.ArgumentText } };
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
            }) { Scope = OspreyPropertyScope.search, DefaultValue = Default(() => new OspreyConfig().SharedPeptides), DescriptionArgs = () => new object[] { @"all" } };

        public static readonly OspreyArgument ARG_FDRBENCH = new OspreyArgument(@"fdrbench",
            () => GetPathToFile(TextUtil.EXT_TSV), (c, p) => c._config.OutputFdrBench = p.Value)
            {
                Scope = OspreyPropertyScope.search, PropertyType = ArgumentPropertyType.path,
                DescriptionArgs = () => new object[] { ARG_FDR_LEVEL.ArgumentText, FdrBenchInputWriter.COLUMN_SCORE, @"peptide", @"precursor", @"both" }
            };
        public static readonly OspreyArgument ARG_FDRBENCH_PER_RUN = new OspreyArgument(@"fdrbench-per-run",
            (c, p) => c._config.FdrBenchPerRun = true) { Scope = OspreyPropertyScope.search, DescriptionArgs = () => new object[] { ARG_FDRBENCH.ArgumentText, FdrBenchInputWriter.COLUMN_RUN } };
        public static readonly OspreyArgument ARG_FDRBENCH_PASS = new OspreyArgument(@"fdrbench-pass",
            new[] { @"1", @"2", @"both" }, (c, p) => c._config.FdrBenchPass = ParseFdrBenchPass(p))
            {
                Scope = OspreyPropertyScope.search, DefaultValue = Default(() => new OspreyConfig().FdrBenchPass),
                DescriptionArgs = () => new object[] { ARG_FDRBENCH.ArgumentText,
                    FdrBenchInputWriter.PassSuffix(OspreyConfig.FDRBENCH_PASS_1), FdrBenchInputWriter.PassSuffix(OspreyConfig.FDRBENCH_PASS_2), @"both" }
            };

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_FDR =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_FDR_Protein_Inference, true,
                ARG_RUN_FDR, ARG_EXPERIMENT_FDR, ARG_RECONCILIATION_COMPACTION_FDR, ARG_PROTEIN_FDR, ARG_FDR_LEVEL, ARG_SHARED_PEPTIDES,
                ARG_FDRBENCH, ARG_FDRBENCH_PER_RUN, ARG_FDRBENCH_PASS);

        // --- Decoys -----------------------------------------------------------------------
        public static readonly OspreyArgument ARG_DECOYS_IN_LIBRARY = new OspreyArgument(OspreyArgNames.DECOYS_IN_LIBRARY,
            (c, p) => c._config.DecoysInLibrary = true)
            { Scope = OspreyPropertyScope.search, DescriptionArgs = () => new object[] { TextUtil.EXT_TSV, string.Join(@", ", new OspreyConfig().DecoyPrefixes), LibrarySource.EXT_BLIB } };
        public static readonly OspreyArgument ARG_DECOY_PAIRING_MANIFEST = new OspreyArgument(OspreyArgNames.DECOY_PAIRING_MANIFEST,
            () => GetPathToFile(TextUtil.EXT_TSV), (c, p) => c._config.DecoyPairingManifestPath = p.Value)
            { DescriptionArgs = () => new object[] { ARG_DECOYS_IN_LIBRARY.ArgumentText, TextUtil.EXT_TSV, LibrarySource.EXT_BLIB } };

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_DECOYS =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Decoys, true,
                ARG_DECOYS_IN_LIBRARY, ARG_DECOY_PAIRING_MANIFEST);

        // --- Training Export ---------------------------------------------------------------
        // A PerFileRescoring product (docs/22-training-export.md; P17 in
        // docs/00-pipeline-architecture.md). Off, nothing about the run changes; on, it adds
        // one <stem>.training.parquet per run, and adding it to a finished directory writes
        // only the exports and re-scores nothing.
        public static readonly OspreyArgument ARG_TRAINING_EXPORT = new OspreyArgument(OspreyArgNames.TRAINING_EXPORT,
            (c, p) => c._config.TrainingExport.Enabled = true)
            { DescriptionArgs = () => new object[] { @"<stem>" + TrainingExportParquet.EXT, ARG_TRAINING_EXPORT_MAX_Q.ArgumentText } };
        public static readonly OspreyArgument ARG_TRAINING_EXPORT_MAX_Q = new OspreyArgument(@"training-export-max-q",
            () => @"<q>", (c, p) => c._config.TrainingExport.MaxQ = ParseDouble(p))
            {
                Scope = OspreyPropertyScope.tuning, PropertyType = ArgumentPropertyType.number, MinValue = 0, MaxValue = 1,
                DescriptionArgs = () => new object[] { ARG_TRAINING_EXPORT.ArgumentText, ARG_RUN_FDR.ArgumentText }
            };
        public static readonly OspreyArgument ARG_TRAINING_EXPORT_CLAIMANT_Q = new OspreyArgument(@"training-export-claimant-q",
            () => @"<q>", (c, p) => c._config.TrainingExport.ClaimantQ = ParseDouble(p))
            { DescriptionArgs = () => new object[] { ARG_TRAINING_EXPORT.ArgumentText, TrainingExportConfig.DEFAULT_CLAIMANT_Q } };
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
        // resolution cannot disagree; seven trivial constructions, once, at type init. The one
        // command's library phases come after the data-staging step, in the order the one command
        // runs them: before the search stages, which with --fasta search the library they predict.
        // A missing task name lists the tasks: Program reads this argument first, so that error is
        // the user's only guide to the names.
        public static readonly OspreyArgument ARG_TASK = new OspreyArgument(OspreyArgNames.TASK,
            TaskNames(), (c, p) => true)
            {
                DescriptionArgs = () => new object[] { SpectraCacheTask.TASK_NAME, SpectraCache.EXT, ModelDiagnosticsTask.TASK_NAME, ARG_MODEL_DIAGNOSTICS.ArgumentText, TrainingExportTask.TASK_NAME, ARG_TRAINING_EXPORT.ArgumentText,
                    ARG_FASTA.ArgumentText, LibraryBuildConfig.PREDICT_TRAINING_LIBRARY, LibraryBuildConfig.TRAIN_MODEL, LibraryBuildConfig.PREDICT_LIBRARY, ARG_TRAINING_SEARCH.ArgumentText },
                MissingValueMessage = argText => string.Format(OspreyResources.Program_Run__0__requires_a_task_name___1___,
                    ARG_TASK.ArgumentText, string.Join(@", ", ARG_TASK.Values))
            };
        // --input-scores is GONE. It named an input KIND - "you handed me parquets" - which is
        // how the Rust pipeline said "Stage 1-4 is already done"; the C# port says that with
        // --task plus each artifact's embedded validity stamp, and two seams answering one question is
        // what let --task ModelDiagnostics join the pipeline and demand state a diagnostics
        // fold never publishes. Every task now takes -i and derives its parquets from the
        // input stem, which is the direction every other sidecar already derives in.
        // The tuning runs' search, addressed on a cluster: the stage --task selects is that
        // search's, which the one command derives from --training-input as it does in one process.
        public static readonly OspreyArgument ARG_TRAINING_SEARCH = new OspreyArgument(OspreyArgNames.TRAINING_SEARCH,
            (c, p) => c.SetLibraryBuildOption(p, b => b.TrainingSearch = true, true))
            {
                DescriptionArgs = () => new object[] { ARG_FASTA.ArgumentText, ARG_TRAINING_INPUT.ArgumentText, ARG_TASK.ArgumentText,
                    LibraryBuildConfig.PREDICT_TRAINING_LIBRARY, LibraryBuildWorkflow.TRAINING_FOLDER_EXAMPLE, LibraryBuildConfig.TRAIN_MODEL }
            };
        // The selected task's file contract, as it declares it, for whatever stages a node's files:
        // a cluster's workflow manager, or the regression's own chain.
        public static readonly OspreyArgument ARG_TASK_FILES = new OspreyArgument(OspreyArgNames.TASK_FILES,
            (c, p) => c._config.PrintTaskFiles = true)
            { DescriptionArgs = () => new object[] { ARG_TASK.ArgumentText } };
        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_HPC =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Distributed_HPC, true,
                ARG_TASK, ARG_TRAINING_SEARCH, ARG_TASK_FILES)
            {
                Preamble = () => string.Format(OspreyResources.OspreyCommandArgs_GROUP_HPC__0__runs_as_one_process_or_task_by_task,
                    ARG_FASTA.ArgumentText, ARG_TASK.ArgumentText, LibraryBuildConfig.PREDICT_TRAINING_LIBRARY,
                    ARG_TRAINING_SEARCH.ArgumentText, LibraryBuildConfig.TRAIN_MODEL, LibraryBuildConfig.PREDICT_LIBRARY)
            };

        /// <summary>
        /// The <c>--task</c> values in <c>--help</c> order: the data-staging step, the one command's
        /// library phases, then the rest of the task set.
        /// </summary>
        private static string[] TaskNames()
        {
            var names = OspreyTasks.Create().All.Select(t => t.Name).ToList();
            names.InsertRange(names.IndexOf(SpectraCacheTask.TASK_NAME) + 1, LibraryBuildConfig.PHASES);
            return names.ToArray();
        }

        // --- Performance ------------------------------------------------------------------
        // OUTER vs INNER parallelism, kept deliberately separate. --parallel-files is the
        // number of input files scored at once (each file's Stage 1-4 work is independent);
        // --threads is the per-file main-search thread budget, divided across whatever files
        // run concurrently. --parallel-files takes an OPTIONAL value, and only a non-negative
        // integer counts as one (OptionalValueFilter), so a trailing positional mzML stays a
        // file: absent = one file at a time, no value = RAM/CPU-aware auto, <N> = exactly N.
        // Unlike the rest of Osprey's in-process work this is not HPC (the Rust HPC split fans
        // files across nodes, one file per process), so it gets its own group rather than
        // sitting under Distributed / HPC.
        public static readonly OspreyArgument ARG_PARALLEL_FILES = new OspreyArgument(OspreyArgNames.PARALLEL_FILES,
            () => @"<N>", (c, p) => c._config.FileParallelism = ParseFileParallelism(p))
            { Scope = OspreyPropertyScope.search, PropertyType = ArgumentPropertyType.integer, MinValue = 0, OptionalValue = true, OptionalValueFilter = IsNonNegativeInteger, DescriptionArgs = () => new object[] { ARG_THREADS.ArgumentText } };
        // Per-stage overrides of --parallel-files, in the same three forms. The per-file stages
        // do not scale alike (caching is bound by a single-threaded vendor decode, scoring by
        // CPU and a large per-file working set), so one count cannot suit all of them. Each
        // applies to its stage only and falls back to --parallel-files when absent.
        public static readonly OspreyArgument ARG_PARALLEL_FILES_CACHING = new OspreyArgument(OspreyArgNames.PARALLEL_FILES_CACHING,
            () => @"<N>", (c, p) => c._config.CachingFileParallelism = ParseFileParallelism(p))
            { Scope = OspreyPropertyScope.search, PropertyType = ArgumentPropertyType.integer, MinValue = 0, OptionalValue = true, OptionalValueFilter = IsNonNegativeInteger, DescriptionArgs = () => new object[] { ARG_PARALLEL_FILES.ArgumentText, OspreyArgNames.TaskText(SpectraCacheTask.TASK_NAME) } };
        public static readonly OspreyArgument ARG_PARALLEL_FILES_SCORING = new OspreyArgument(OspreyArgNames.PARALLEL_FILES_SCORING,
            () => @"<N>", (c, p) => c._config.ScoringFileParallelism = ParseFileParallelism(p))
            { Scope = OspreyPropertyScope.search, PropertyType = ArgumentPropertyType.integer, MinValue = 0, OptionalValue = true, OptionalValueFilter = IsNonNegativeInteger, DescriptionArgs = () => new object[] { ARG_PARALLEL_FILES.ArgumentText, PerFileScoringTask.TASK_NAME } };
        public static readonly OspreyArgument ARG_PARALLEL_FILES_RESCORING = new OspreyArgument(OspreyArgNames.PARALLEL_FILES_RESCORING,
            () => @"<N>", (c, p) => c._config.RescoringFileParallelism = ParseFileParallelism(p))
            { Scope = OspreyPropertyScope.search, PropertyType = ArgumentPropertyType.integer, MinValue = 0, OptionalValue = true, OptionalValueFilter = IsNonNegativeInteger, DescriptionArgs = () => new object[] { ARG_PARALLEL_FILES.ArgumentText, PerFileRescoreTask.TASK_NAME } };
        public static readonly OspreyArgument ARG_THREADS = new OspreyArgument(@"threads",
            INT_VALUE, (c, p) => c._config.NThreads = ParseInt(p))
            {
                Scope = OspreyPropertyScope.search, PropertyType = ArgumentPropertyType.integer, DefaultValue = Default(() => new OspreyConfig().NThreads), MinValue = 1,
                DescriptionArgs = () => new object[] { ARG_PARALLEL_FILES.ArgumentText }
            };

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_PERFORMANCE =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Performance, true,
                ARG_THREADS, ARG_PARALLEL_FILES, ARG_PARALLEL_FILES_CACHING, ARG_PARALLEL_FILES_SCORING,
                ARG_PARALLEL_FILES_RESCORING);

        // --- Logging ----------------------------------------------------------------------
        // Per-line output decoration and redirection. --timestamp / --memstamp prefix each
        // line written through Program._out (see CommandStatusWriter); --log-file redirects
        // that writer to a file. The "[date]\t{managed}\t{total}\t{msg}" stamp format is
        // consumed by ai/scripts/Osprey/perfviz.html.
        public static readonly OspreyArgument ARG_TIMESTAMP = new OspreyArgument(@"timestamp",
            (c, p) => c._config.IsTimeStamped = true) { DescriptionArgs = () => new object[] { @"[yyyy/MM/dd HH:mm:ss]" } };
        public static readonly OspreyArgument ARG_MEMSTAMP = new OspreyArgument(@"memstamp",
            (c, p) => c._config.IsMemStamped = true);
        public static readonly OspreyArgument ARG_LOG_FILE = new OspreyArgument(@"log-file",
            () => GetPathToFile(string.Empty), (c, p) => c._config.LogFilePath = p.Value);
        public static readonly OspreyArgument ARG_PERF_STATS = new OspreyArgument(@"perf-stats",
            (c, p) => c._config.PerfStats = true) { DescriptionArgs = () => new object[] { @"[COUNT], [TIMING], [BENCH], [STAGE-WALL], [PATH], [TRAIN]" } }; // Log tag OK: help text naming the tags
        public static readonly OspreyArgument ARG_VERBOSE = new OspreyArgument(OspreyArgNames.VERBOSE,
            (c, p) => c._config.Verbose = true) { Scope = OspreyPropertyScope.search };

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_LOGGING =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Logging, true,
                ARG_TIMESTAMP, ARG_MEMSTAMP, ARG_LOG_FILE, ARG_PERF_STATS, ARG_VERBOSE);

        // --- Diagnostics & Info -----------------------------------------------------------
        // -h/--help and -v/--version are terminal: their handlers render help / print the
        // version and exit 0. --help accepts an optional format/section value
        // (ascii | unicode | sections | html | <Section>); the description names the values.
        // The developer dump bundle that -d / --diagnostics used to turn on is the
        // OSPREY_DIAGNOSTICS variable (OspreyEnvironment.Diagnostics): the help is for users.
        // One flag, everything we know how to show. An opt-in token per expensive panel was built
        // and removed (#4522): the peak co-assignment panel measured 7.3M rows/s, i.e. ~46s on an
        // 82-file Astral run against a 10-hour search, so the cost never justified making anyone
        // choose. Someone who asks for --model-diagnostics wants the diagnostics, not a decision
        // about which ones they can afford - and a panel behind a token nobody remembers is a
        // panel nobody sees, which defeats a diagnostic whose whole purpose is surfacing an effect
        // users do not know to look for.
        public static readonly OspreyArgument ARG_MODEL_DIAGNOSTICS = new OspreyArgument(OspreyArgNames.MODEL_DIAGNOSTICS,
            (c, p) => c._config.ModelDiagnostics = true) { Scope = OspreyPropertyScope.search };
        public static readonly OspreyArgument ARG_HELP = new OspreyArgument(@"help",
            () => @"<format|Section>", (c, p) =>
            {
                PrintUsage(p.Value);
                Environment.Exit(0);
            }) { ShortName = @"h", OptionalValue = true, DescriptionArgs = () => new object[] { @"[ascii|unicode|sections|html|properties|<Section>]" } };
        public static readonly OspreyArgument ARG_VERSION = new OspreyArgument(@"version",
            (c, p) =>
            {
                Console.WriteLine(OspreyResources.Program_Run_Osprey_v_0_, OspreyVersion.DisplayVersion);
                Environment.Exit(0);
            }) { ShortName = @"v" };
        // Skyline's --culture: run under a named culture instead of the OS one, for resource
        // lookup. Program applies it before anything is written (see Program.RunCommand), which
        // also reports a name .NET does not know; the parser only consumes the value. Shown in the
        // help, as Skyline's is since a user in a translated locale asked how to get English
        // output; numbers are read in the invariant culture whatever the language, so the argument
        // changes only the messages, and can go anywhere on the command line.
        public static readonly OspreyArgument ARG_INTERNAL_CULTURE = new OspreyArgument(@"culture",
            () => @"en|ja|zh-Hans...", (c, p) => true);

        private static readonly ArgumentGroup<OspreyCommandArgs> GROUP_INFO =
            new ArgumentGroup<OspreyCommandArgs>(() => OspreyResources.OspreyCommandArgs_Group_Diagnostics_Info, true,
                ARG_MODEL_DIAGNOSTICS, ARG_HELP, ARG_VERSION, ARG_INTERNAL_CULTURE);

        public static IEnumerable<IUsageBlock> UsageBlocks
        {
            get
            {
                return new[]
                {
                    new ParaUsageBlock(OspreyResources.OspreyCommandArgs_UsageBlocks_Osprey___Peptide_centric_DIA_analysis__NET_port_of_Osprey_),
                    new ParaUsageBlock(string.Format(OspreyResources.OspreyCommandArgs_UsageBlocks_USAGE___0_,
                        string.Join(@" ", @"Osprey", UsageInput, UsageLibrary, UsageOutput))),
                    new ParaUsageBlock(string.Join(@" ", @"      ", @"Osprey", UsageInput, UsageFasta, UsageOutput)),
                    GROUP_GENERAL_IO,
                    GROUP_LIBRARY_BUILD,
                    GROUP_LIBRARY_DIGEST,
                    CarafeCommandLine.EnzymeTableBlock,
                    CarafeCommandLine.ModificationTableBlock,
                    GROUP_LIBRARY_PREDICTION,
                    GROUP_LIBRARY_TUNING,
                    GROUP_SCORING,
                    GROUP_FDR,
                    GROUP_DECOYS,
                    GROUP_TRAINING_EXPORT,
                    GROUP_PERFORMANCE,
                    GROUP_HPC,
                    GROUP_LOGGING,
                    GROUP_INFO,
                    new ParaUsageBlock(OspreyResources.OspreyCommandArgs_UsageBlocks_EXAMPLES_),
                    new ParaUsageBlock(@"  Osprey -i *.raw " + (ARG_FASTA + @"human.fasta") + @" -o results.blib"),
                    new ParaUsageBlock(@"  Osprey -i *.raw " + (ARG_FASTA + @"human.fasta") + @" " +
                                       (ARG_TRAINING_INPUT_LIST + @"tuning.txt") + @" -o results.blib"),
                    new ParaUsageBlock(@"  Osprey -i *.raw -l results.predicted.blib -o results2.blib"),
                    new ParaUsageBlock(@"  Osprey -i *.mzML -l library.blib -o results.blib " + (ARG_RESOLUTION + @"unit")),
                    new ParaUsageBlock(@"  Osprey -i *.d -l library.blib -o results.blib"),
                    new ParaUsageBlock(@"  Osprey -i *.mzML -l diann_library.tsv -o results.blib"),
                    new ParaUsageBlock(string.Format(OspreyResources.OspreyCommandArgs_UsageBlocks_HPC_SPLIT__one_node___one__0____see__0__above_,
                        ARG_TASK.ArgumentText)),
                    // The other forms the parser accepts, after the reference, as CarafeSharp's help ends.
                    new ParaUsageBlock(string.Format(OspreyResources.OspreyCommandArgs_UsageBlocks_OTHER_FORMS_ACCEPTED__a_value_may_also_be_joined_to_its_option___0___and_a_file_that_exists_may_be_given_without__1__,
                        ARG_OUTPUT.ArgumentText + @"=results.blib", ARG_INPUT.ShortArgumentText) + @" " + string.Format(
                        OspreyResources.OspreyCommandArgs_UsageBlocks_The_library_build_options_are_listed_in_the_standard_form___0___and__for_compatibility_with_Java_Carafe_s_command_lines__also_accepted_with_its_underscores___1___,
                        CarafeCommandLine.ARG_MISS_C + @"1", @"--miss_c 1, --minLength 7")),
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
            return ParseArgs(args, OspreyEnvironment.FdrModel, out _);
        }

        /// <summary>
        /// <see cref="ParseArgs(string[])"/> with the library the run builds (<c>--fasta</c>) as
        /// CarafeSharp's settings, or null when the run searches a given library. The settings
        /// are CarafeSharp's type, which the config in Osprey.Core cannot hold; the executable's
        /// workflow takes them beside the config.
        /// </summary>
        internal static OspreyConfig ParseArgs(string[] args, out LibraryBuildSettings librarySettings)
        {
            return ParseArgs(args, OspreyEnvironment.FdrModel, out librarySettings);
        }

        /// <summary>
        /// <see cref="ParseArgs(string[])"/> with the classifier supplied rather than read from
        /// OSPREY_FDR_MODEL, which is read once at process start and so cannot be varied by a
        /// test. Exists so a test can prove that gbdt reaches the config: #4491 was the
        /// classifier silently not reaching training.
        /// </summary>
        internal static OspreyConfig ParseArgs(string[] args, FdrClassifier fdrModel)
        {
            return ParseArgs(args, fdrModel, out _);
        }

        private static OspreyConfig ParseArgs(string[] args, FdrClassifier fdrModel, out LibraryBuildSettings librarySettings)
        {
            var parser = new OspreyCommandArgs();
            CreateParser().Parse(parser, args);
            var config = parser.ToConfig(fdrModel);
            config.CommandLine = CommandLineText.Format(@"Osprey", args);
            librarySettings = parser.InterpretLibraryBuild(config);
            return config;
        }

        /// <summary>
        /// The library a <c>--fasta</c> run predicts, and with <c>--training-input</c> the tuning
        /// before it, read from the CarafeSharp declarations the parse recorded with the values Osprey
        /// derives: the FASTA, the saved model, the device, the run q-value a training precursor needs
        /// (the training export's), and the run's own command line for the libraries' metadata.
        /// CarafeSharp's refusals of a bad combination are its own <see cref="ArgumentException"/> and
        /// <see cref="NotSupportedException"/>, which Program reports as usage errors. Null without
        /// <c>--fasta</c>; the options then given are reported by <c>ValidateArgs</c>, as are tuning
        /// options without <c>--training-input</c>.
        /// </summary>
        private LibraryBuildSettings InterpretLibraryBuild(OspreyConfig config)
        {
            var build = config.LibraryBuild;
            if (!build.Enabled)
            {
                build.OptionsWithoutFasta.AddRange(_libraryBuildOptionsSeen.Distinct());
                return null;
            }
            // The device the phases run on is settled when the libtorch runtime is loaded
            // (LibraryBuildWorkflow): auto is Osprey's word, not CarafeSharp's, and gpu or cpu is
            // set there from the runtime loaded, so CarafeSharp gets its default here.
            string device = string.Equals(build.Device, OspreyTorchRuntimes.DEVICE_AUTO, StringComparison.OrdinalIgnoreCase) ? null : build.Device;
            LibrarySettings library;
            if (build.PrecursorTablePath != null && build.FastaPath == null)
            {
                // The table alone digests nothing, so the digest options are refused by name
                build.DigestOptionsWithPrecursors.AddRange(_carafe.HostedDigestOptionsGiven());
                library = _carafe.InterpretHostedPrecursorTable(build.ModelPath, device, config.CommandLine);
            }
            else
            {
                // With a FASTA and a table both, the FASTA is the training library the tuning runs
                // are searched against (Program refuses the two without the tuning runs), and the
                // tuned models then predict the table.
                library = _carafe.InterpretHostedLibrary(build.FastaPath, build.ModelPath, device, config.CommandLine);
            }
            // A precursor table without a FASTA tunes nothing: Program refuses --training-input with it, after the parse
            if (!build.Tuning || build.FastaPath == null)
            {
                build.OptionsWithoutTrainingInput.AddRange(_tuningOptionsSeen.Distinct());
                return new LibraryBuildSettings(library, null);
            }
            // The library the tuning runs are searched with is predicted as a mode-1 or mode-3 library
            // is; the one the output is searched with comes from the tuned models.
            var training = _carafe.InterpretHostedTraining(build.FastaPath, build.ModelPath, device,
                config.TrainingExport.EffectiveMaxQ(config.RunFdr), config.CommandLine);
            return new LibraryBuildSettings(library, training);
        }

        /// <summary>
        /// The value given to <paramref name="arg"/> on the command line (the last one, if it is
        /// repeated), or null when the argument is absent. Found the way the parser finds it -
        /// both value forms, and the same missing-value error - so Program can read
        /// <see cref="ARG_TASK"/> and <see cref="ARG_INTERNAL_CULTURE"/> before the full parse
        /// without a second grammar.
        /// </summary>
        internal static string FindValue(string[] args, OspreyArgument arg)
        {
            return CreateParser().FindValue(args, arg);
        }

        /// <summary>
        /// The shared parser over Osprey's declarations, with the positional fallback: a bare
        /// token that exists on disk is an input file, and any other is reported and skipped.
        /// A token starting with '-' that names no argument is an unknown argument, which the
        /// parser refuses (caught by Main).
        /// </summary>
        private static CommandLineParser<OspreyCommandArgs> CreateParser()
        {
            return new CommandLineParser<OspreyCommandArgs>(AllArguments) { PositionalHandler = TakePositional };
        }

        private static bool TakePositional(OspreyCommandArgs c, string token)
        {
            // Directory.Exists matters as much as File.Exists here: the vendor formats that are
            // DIRECTORIES (Agilent .d, Bruker .d, Waters .raw) would otherwise fall through to
            // "Unknown argument" and be silently dropped from the run, while the same path passed
            // with -i was accepted.
            if (File.Exists(token) || Directory.Exists(token))
                c._inputFiles.Add(token);
            else
                Program.LogWarning(string.Format(OspreyResources.OspreyCommandArgs_TokenizeAndDispatch_Unknown_argument___0_, token));
            return true;
        }

        private OspreyConfig ToConfig(FdrClassifier fdrModel)
        {
            // Expand --input-list BEFORE normalization, so a listed path is indistinguishable
            // from one given with -i from here on. Appended in the order the lists were given,
            // after any -i, because input ORDER is not decorative: FirstJoin is order-sensitive
            // and the run's file indices follow this list.
            foreach (string listPath in _inputListPaths)
                _inputFiles.AddRange(ReadInputList(listPath, ARG_INPUT_LIST));
            // --training-input-list the same way, after any --training-input. Not read without
            // --fasta, where ValidateArgs reports the option as needing it.
            if (_config.LibraryBuild.Enabled)
            {
                foreach (string listPath in _trainingInputListPaths)
                    _config.LibraryBuild.TrainingInputs.AddRange(ReadInputList(listPath, ARG_TRAINING_INPUT_LIST));
            }

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

            // The classifier comes from OSPREY_FDR_MODEL, read once at process start, and is set
            // here, where --fdr-method used to set it, so the rest of the pipeline reads the
            // config and never the environment. An unrecognized value parses to the default only
            // so the config is well-formed; Program aborts on OspreyEnvironment.FdrModelError
            // before the pipeline runs.
            _config.FdrClassifier = fdrModel;

            switch (_resolution)
            {
                case @"unit":
                    _config.ResolutionMode = ResolutionMode.UnitResolution;
                    break;
                default:
                    _config.ResolutionMode = ResolutionMode.HRAM;
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
        private static IEnumerable<string> ReadInputList(string listPath, OspreyArgument listArg)
        {
            if (string.IsNullOrEmpty(listPath) || !File.Exists(listPath))
            {
                throw new FileNotFoundException(string.Format(
                    OspreyResources.OspreyCommandArgs_ReadInputList__0__file_not_found___1_, listArg.ArgumentText, listPath), listPath);
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
                    OspreyResources.OspreyCommandArgs_ReadInputList_The__0__file_lists_no_input_files___1_, listArg.ArgumentText, listPath));
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

        /// <summary>
        /// True when <paramref name="token"/> is a plain non-negative integer (no
        /// sign, digits only). The <c>--parallel-files</c> optional-value filter: it tells
        /// an explicit count (including <c>0</c> = sequential) from auto-mode-plus-trailing-token,
        /// and a leading '-' is therefore correctly treated as the next flag, not a value.
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
        /// True when <paramref name="token"/> is a plain non-negative number in the invariant
        /// culture: the <c>--entrapment [ratio]</c> optional-value filter, which tells a ratio from
        /// a trailing positional file.
        /// </summary>
        private static bool IsNumber(string token)
        {
            return !string.IsNullOrEmpty(token) && token[0] != '-' &&
                   double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
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
        /// The request a <c>--parallel-files</c>-style argument carries: no value is auto, and a
        /// count is explicit. <c>0</c> - the value a user most naturally types to mean "off" -
        /// is an explicit ONE file at a time, not the sequential default: the default yields to
        /// the <c>OSPREY_MAX_PARALLEL_FILES</c> cap, and a stage the user turned off must not.
        /// </summary>
        private static FileParallelism ParseFileParallelism(NameValuePair p)
        {
            if (string.IsNullOrEmpty(p.Value))
                return FileParallelism.Auto;
            // A spaced value reaches here only through the IsNonNegativeInteger filter; an inline
            // one (--parallel-files=N) is explicit, so it must BE a count, a negative one included.
            if (!IsNonNegativeInteger(p.Value))
                throw new ArgumentException(InvalidValueMessage(p));
            return FileParallelism.Explicit(Math.Max(1, ParseInt(p)));
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
        /// the argument the parser bound the pair to, so no handler spells an option name or its
        /// value list a second time.
        /// </summary>
        private static string InvalidValueMessage(NameValuePair p, string[] expectedValues = null)
        {
            return ArgUsage.Provider.ValueInvalidMessage(p.Match.ArgumentText, p.Value, expectedValues);
        }

        /// <summary>
        /// A property's default as the command line spells it (<see cref="ArgumentBase.DefaultValue"/>):
        /// a number in the invariant culture, an enum member in lower case as the listed values are
        /// spelled, read when asked so a default that depends on the computer (the thread count)
        /// is current; null for a setting that is off until given (a protein FDR).
        /// </summary>
        private static Func<string> Default(Func<object> value)
        {
            return () =>
            {
                object v = value();
                if (v == null)
                    return null;
                return v is Enum e ? e.ToString().ToLowerInvariant() : Convert.ToString(v, CultureInfo.InvariantCulture);
            };
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
        /// The label of one of Osprey's own arguments as a property of a settings page: the
        /// <see cref="OspreyCommandArgUsage"/> text under its usage key with CarafeSharp's property
        /// suffix (<c>_run_fdr_property</c>), read at call time so it follows the UI culture. A
        /// hosted argument is labeled by CarafeSharp's text instead.
        /// </summary>
        internal static string GetPropertyLabel(string argName)
        {
            return OspreyCommandArgUsage.ResourceManager.GetString(UsageKey(argName) + CarafeCommandLine.PROPERTY_SUFFIX);
        }

        /// <summary>
        /// Writes generated usage help to <paramref name="writer"/> (default stdout, so an explicit
        /// `--help [html]` can be captured with a plain `>` redirect; Main passes stderr for the
        /// no-args usage-error path). <paramref name="formatType"/>: null/"unicode" = unicode tables
        /// (default, like Skyline), "ascii" = lower-128 ascii tables, "sections" = section names,
        /// "html" = HTML, "properties" = the arguments that are settings, as JSON for a host
        /// (<see cref="OspreyProperties"/>), anything else = a section filter.
        /// </summary>
        internal static void PrintUsage(string formatType, TextWriter writer = null)
        {
            writer = writer ?? Console.Out;
            if (string.Equals(formatType, @"html", StringComparison.OrdinalIgnoreCase))
                writer.Write(GenerateUsageHtml());
            else if (string.Equals(formatType, OspreyProperties.HELP_FORMAT, StringComparison.OrdinalIgnoreCase))
                writer.Write(OspreyProperties.Generate());
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
                // Treat as a section-name filter. The tables that follow a section (the enzymes
                // and modifications after the digest options) belong to it.
                var blocks = UsageBlocks.ToList();
                int start = blocks.FindIndex(b => b is ArgumentGroup<OspreyCommandArgs> g &&
                                                  g.Title.IndexOf(formatType, StringComparison.OrdinalIgnoreCase) >= 0);
                if (start < 0)
                    return string.Format(OspreyResources.OspreyCommandArgs_BuildUsage_No_help_section_matching___0___found__Use__1__to_list_available_sections_,
                        formatType, ARG_HELP.ArgumentText + @" sections") + Environment.NewLine;
                for (int i = start; i < blocks.Count && (i == start || blocks[i] is TableUsageBlock); i++)
                    sb.Append(blocks[i].ToString(USAGE_WIDTH, ArgUsage.FORMAT_NO_BORDERS));
                return sb.ToString();
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
                StemFile(FdrScoresSidecar.EXT_FIRST_PASS), @"out." + FdrScoresSidecar.LABEL_FIRST_PASS + @".*"));
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
        /// <see cref="OspreyCommandArgUsage"/>, headers and the parser's errors from
        /// OspreyResources. The handlers' own value errors go through
        /// <see cref="OspreyCommandArgs.InvalidValueMessage"/> to the same text.
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
                {
                    // A hosted CarafeSharp declaration is described by CarafeSharp's text.
                    return HostedArguments.Any(a => a.Name == argName) ? CarafeCommandLine.GetUsageText(argName) : null;
                }
                var formatArgs = AllArguments.FirstOrDefault(a => a.Name == argName)?.DescriptionArgs?.Invoke();
                return formatArgs == null ? description : string.Format(description, formatArgs);
            }

            public string AppliesToHeader { get { return OspreyResources.OspreyArgUsageProvider_AppliesToHeader_Applies_To; } }
            public string ArgumentHeader { get { return OspreyResources.OspreyArgUsageProvider_ArgumentHeader_Argument; } }
            public string DescriptionHeader { get { return OspreyResources.OspreyArgUsageProvider_DescriptionHeader_Description; } }

            // ValueInvalidMessage is the text every "Invalid value" error in this file shows;
            // ValueUnexpected / ValueInvalid are what the token builder throws.
            public string ValueMissingMessage(string argText) { return string.Format(OspreyResources.OspreyArgUsageProvider_ValueMissingMessage__0__requires_a_value_, argText); }
            public string UnknownArgumentMessage(string argText)
            {
                return string.Format(OspreyResources.OspreyCommandArgs_TokenizeAndDispatch_Unknown_argument___0___Run_with__1__to_see_valid_options_,
                    argText, ARG_HELP.ArgumentText);
            }
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
    /// When an argument that is a setting applies, so a host shows it only for a run it applies
    /// to: every run, a run that predicts its library (<c>--fasta</c> or <c>--precursors</c>), one
    /// that digests a FASTA, or one that tunes the models on <c>--training-input</c> runs.
    /// </summary>
    internal enum OspreyPropertyScope
    {
        search,
        library,
        digest,
        tuning,
    }

    /// <summary>
    /// Osprey-local <see cref="Argument{TContext}"/> extension carrying the values its usage
    /// text is formatted with (<see cref="DescriptionArgs"/>) and the scope that makes it a
    /// property of a host's settings page (<see cref="Scope"/>).
    /// </summary>
    internal class OspreyArgument : Argument<OspreyCommandArgs>
    {
        private OspreyPropertyScope? _scope;

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
        /// Another tool's declaration, hosted on Osprey's command line (CarafeSharp's library-build
        /// options): what it says about its name and value is copied, the handler is Osprey's, and
        /// its usage text is the other tool's.
        /// </summary>
        public OspreyArgument(ArgumentBase template, Action<OspreyCommandArgs, NameValuePair> processValue)
            : base(template, processValue)
        {
            IsHosted = true;
        }

        /// <summary>True for a declaration hosted from CarafeSharp, whose usage text is CarafeSharp's.</summary>
        public bool IsHosted { get; }

        /// <summary>
        /// The scope in which the argument is a setting of a run, which makes it a property of a
        /// host's settings page (<see cref="ArgumentBase.PropertyName"/>); null for an argument
        /// that is none: an input, an output, a command or an internal switch. Setting it labels one
        /// of Osprey's own arguments through <see cref="OspreyCommandArgs.GetPropertyLabel"/>; a
        /// hosted one keeps CarafeSharp's label, and is a property only when CarafeSharp labels it.
        /// </summary>
        public OspreyPropertyScope? Scope
        {
            get { return _scope; }
            set
            {
                _scope = value;
                if (!IsHosted)
                    PropertyName = value.HasValue ? () => OspreyCommandArgs.GetPropertyLabel(Name) : null;
            }
        }

        /// <summary>
        /// Values for the <c>{N}</c> placeholders in this argument's usage text in
        /// <see cref="OspreyCommandArgUsage"/>: other arguments' text, default values, file
        /// extensions - anything that must not be translated. Null when the text has none.
        /// </summary>
        public Func<object[]> DescriptionArgs { get; set; }
    }
}
