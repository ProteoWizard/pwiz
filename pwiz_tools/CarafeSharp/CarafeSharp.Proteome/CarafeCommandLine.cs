/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/ai/AIGear.java (main: the
 *   options and their handling up to the -build_entrapment_fasta and -reconcile_manifest modes)
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
using pwiz.Common.CommandLine;
using CarafeArgument = pwiz.Common.CommandLine.Argument<pwiz.CarafeSharp.Proteome.CarafeCommandLine>;
using CarafeArgumentGroup = pwiz.Common.CommandLine.ArgumentGroup<pwiz.CarafeSharp.Proteome.CarafeCommandLine>;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>What a Carafe command line asks for, among the modes CarafeSharp ports.</summary>
    public enum CarafeCommandMode
    {
        help,
        build_entrapment_fasta,
        reconcile_manifest,
        predict_library,
        train,
        model_info,
    }

    /// <summary>
    /// Parses a Carafe command line for the modes CarafeSharp ports (the stage-1 FASTA modes,
    /// library prediction from <c>-db</c> and training on Osprey's training exports), so
    /// CarafeSharp accepts exactly what Carafe's GUI passes. The options are declared once, on
    /// the shared <see cref="Argument{TContext}"/> framework, and parsed by the shared
    /// <see cref="CommandLineParser{TContext}"/> under Carafe's grammar (<see cref="GRAMMAR"/>):
    /// the names spelled the standard pwiz way in the help (<c>--miss-c</c>), with Carafe's own
    /// spelling accepted for every option - its single dash and its underscores (<c>-miss_c</c>) -
    /// so a recorded Carafe command line runs unchanged; a value as the next token or after '=',
    /// a value ended only by a declared option (so <c>-seed -5</c> and <c>-mode -</c> are
    /// values), a repeated option keeping its first value, an unknown option or a flag given a
    /// value an error, and a stray word ignored. Every Carafe 2.2.0 option is declared; the ones
    /// these modes do not read are accepted and ignored, as Carafe ignores them. The help is
    /// generated from the declarations (<see cref="BuildUsage"/>), so it cannot drift from the parser.
    /// <para>
    /// A host may present the library-build options as sections of its own command line: Osprey
    /// declares <see cref="HostedDigestArguments"/> and <see cref="HostedPredictionArguments"/>
    /// again for its own parser, records what it is given through <see cref="Record"/>, describes
    /// them with <see cref="GetUsageText"/>, and after its parse calls
    /// <see cref="InterpretHostedLibrary"/> with the values it derives itself. One declaration
    /// set, two front ends.
    /// </para>
    /// <para>
    /// Defaults are Carafe's effective ones, which are not all its help text's: without the
    /// options, missed cleavages are 2 (help: 1), the precursor m/z window is 300-2000 (help:
    /// 400-1000), charges are 2-3 (help: 2-4), and methionine clipping is off. The
    /// non-specific enzyme forces 100 missed cleavages. Library prediction has its own charge
    /// default, 2-4 (see <see cref="LibrarySettings"/>).
    /// </para>
    /// </summary>
    public sealed class CarafeCommandLine
    {
        /// <summary>CParameter's precursor m/z window, used when -min_pep_mz / -max_pep_mz are absent.</summary>
        public const double DEFAULT_MIN_PEPTIDE_MZ = 300.0;
        public const double DEFAULT_MAX_PEPTIDE_MZ = 2000.0;
        public const int DEFAULT_MIN_CHARGE = 2;
        public const int DEFAULT_MAX_CHARGE = 3;

        private const int USAGE_WIDTH = 78;

        /// <summary>
        /// Carafe's grammar on the standard spelling: <c>--name</c> as the help shows it, with
        /// Carafe's single dash accepted for every option and an underscore read as a hyphen
        /// (<c>-lf_type</c> names <c>--lf-type</c>), a value as the next token, a prefixed token a
        /// value unless it names a declared option, the first of a repeated option winning, and
        /// listed values left to the settings, which refuse a bad one with Carafe's own message.
        /// </summary>
        private static readonly CommandLineGrammar GRAMMAR = new CommandLineGrammar
        {
            AcceptedPrefixes = new[] { ArgumentBase.ARG_PREFIX, @"-" },
            ShortPrefix = @"-",
            ValueMayFollow = true,
            PrefixedTokenEndsValue = false,
            FirstValueWins = true,
            ValidatesListedValues = false,
            NameSeparatorsEquivalent = true,
        };

        /// <summary>The <c>-device</c> values libtorch can be asked for.</summary>
        private static readonly string[] DEVICES = { @"cpu", @"gpu", @"cuda" };

        // Path values as Skyline's command line writes them (--in=path/to/file.sky). CarafeSharp has
        // no translated text, so these are not resources; Osprey's own placeholders are.
        private const string PATH_TO_FILE = @"path/to/file";
        private const string PATH_TO_FOLDER = @"path/to/folder";
        private const string EXT_FASTA = @".fasta";
        private const string EXT_TSV = @".tsv";

        /// <summary>
        /// Carafe's grammar and the text the help renderer and the parser's errors need. Carafe's
        /// grammar is space-separated (--name value), so the help renders "--name &lt;value&gt;", and
        /// a token built from an instance (ARG_DB + "x.fasta") is too; the settings read numbers
        /// as Java does, in the invariant culture.
        /// </summary>
        private static readonly ArgUsageSettings ARG_USAGE = new ArgUsageSettings
        {
            Grammar = GRAMMAR,
            Provider = new CarafeArgUsageProvider(),
            ArgumentValueSeparator = @" ",
            ValueFormatProvider = CultureInfo.InvariantCulture,
        };

        /// <summary>
        /// Installs <see cref="ARG_USAGE"/> on the process-wide seams, which only CarafeSharp.exe
        /// (its Main) and CarafeSharp's tests do: the program that owns the process installs the
        /// grammar its command line parses under. Hosted in Skyline and Osprey, these declarations
        /// run under the host's settings; Osprey parses the options it hosts under its own grammar.
        /// Installed once at this type's initialization, as it was, Carafe's grammar decided how
        /// Skyline parsed its next command line as soon as Skyline had read a model file, and kept
        /// the first of a repeated argument where Skyline keeps the last.
        /// </summary>
        public static void InstallGrammar()
        {
            ArgUsage.Install(ARG_USAGE);
        }

        // The declarations are fields, so what makes one a property of a host's settings page
        // (DeclareProperties) is set once they all exist.
        static CarafeCommandLine()
        {
            DeclareProperties();
        }

        // --- Modes and files ---------------------------------------------------------------
        // Carafe's names, one declaration each; the parser records what was given (Record), and
        // Interpret below reads the records as it always has.
        public static readonly CarafeArgument ARG_BUILD_ENTRAPMENT_FASTA = Value(@"build-entrapment-fasta", PATH_TO_FILE + EXT_FASTA);
        public static readonly CarafeArgument ARG_RECONCILE_MANIFEST = Value(@"reconcile-manifest", PATH_TO_FILE + EXT_TSV);
        public static readonly CarafeArgument ARG_MANIFEST = Value(@"manifest", PATH_TO_FILE + EXT_TSV);
        public static readonly CarafeArgument ARG_PREDICTED_LIBRARY = Value(@"predicted-library", PATH_TO_FILE + EXT_TSV + @"|.blib");
        public static readonly CarafeArgument ARG_MODEL_INFO = Value(@"model-info", PATH_TO_FILE + CarafeModelFile.EXTENSION);
        public static readonly CarafeArgument ARG_DB = Value(@"db", PATH_TO_FILE + EXT_FASTA);
        public static readonly CarafeArgument ARG_I = Value(@"i", PATH_TO_FILE + @".blib|" + TrainingExportParquet.EXT + @"|" + PATH_TO_FOLDER);
        public static readonly CarafeArgument ARG_MS = Value(@"ms", @"<run1,run2,...>");
        public static readonly CarafeArgument ARG_O = Value(@"o", PATH_TO_FOLDER);
        // -h is Carafe's and Osprey's; -help and --help are the same declaration. The optional
        // value picks the help format (ascii | unicode | sections | html | <Section>).
        public static readonly CarafeArgument ARG_HELP = new CarafeArgument(@"help", () => @"<format|Section>", (c, p) => c.Record(p))
            { ShortName = @"h", OptionalValue = true };

        private static readonly CarafeArgumentGroup GROUP_MODES = new CarafeArgumentGroup(() => @"Modes and files", true,
            ARG_BUILD_ENTRAPMENT_FASTA, ARG_RECONCILE_MANIFEST, ARG_MANIFEST, ARG_PREDICTED_LIBRARY, ARG_MODEL_INFO,
            ARG_DB, ARG_I, ARG_MS, ARG_O, ARG_HELP);

        // --- Digest and modifications ------------------------------------------------------
        public static readonly CarafeArgument ARG_ENZYME = Value(@"enzyme", @"<index|name>");
        public static readonly CarafeArgument ARG_MISS_C = Value(@"miss-c", @"<n>");
        public static readonly CarafeArgument ARG_MIN_LENGTH = Value(@"min-length", @"<n>");
        public static readonly CarafeArgument ARG_MAX_LENGTH = Value(@"max-length", @"<n>");
        public static readonly CarafeArgument ARG_CLIP_N_M = Flag(@"clip-n-m");
        public static readonly CarafeArgument ARG_FIX_MOD = Value(@"fix-mod", @"<ids>");
        public static readonly CarafeArgument ARG_VAR_MOD = Value(@"var-mod", @"<ids>");
        public static readonly CarafeArgument ARG_MAX_VAR = Value(@"max-var", @"<n>");
        public static readonly CarafeArgument ARG_MIN_PEP_MZ = Value(@"min-pep-mz", @"<mz>");
        public static readonly CarafeArgument ARG_MAX_PEP_MZ = Value(@"max-pep-mz", @"<mz>");
        public static readonly CarafeArgument ARG_MIN_PEP_CHARGE = Value(@"min-pep-charge", @"<z>");
        public static readonly CarafeArgument ARG_MAX_PEP_CHARGE = Value(@"max-pep-charge", @"<z>");
        public static readonly CarafeArgument ARG_I2L = Flag(@"i2l");

        private static readonly CarafeArgumentGroup GROUP_DIGEST = new CarafeArgumentGroup(() => @"Digest and modifications", true,
            ARG_ENZYME, ARG_MISS_C, ARG_MIN_LENGTH, ARG_MAX_LENGTH, ARG_CLIP_N_M, ARG_FIX_MOD, ARG_VAR_MOD, ARG_MAX_VAR,
            ARG_MIN_PEP_MZ, ARG_MAX_PEP_MZ, ARG_MIN_PEP_CHARGE, ARG_MAX_PEP_CHARGE, ARG_I2L);

        // --- Entrapment FASTA build --------------------------------------------------------
        public static readonly CarafeArgument ARG_ENTRAPMENT = Flag(@"entrapment");
        public static readonly CarafeArgument ARG_NO_DECOYS = Flag(@"no-decoys");
        public static readonly CarafeArgument ARG_DECOY_PREFIX = Value(@"decoy-prefix", @"<prefix>");
        public static readonly CarafeArgument ARG_MZ_FILTER = Flag(@"mz-filter");
        public static readonly CarafeArgument ARG_ENTRAPMENT_SEED = Value(@"entrapment-seed", @"<n>");
        public static readonly CarafeArgument ARG_DECOY_SEED = Value(@"decoy-seed", @"<n>");
        public static readonly CarafeArgument ARG_ENTRAPMENT_DB = Value(@"entrapment-db", PATH_TO_FILE + EXT_FASTA);
        public static readonly CarafeArgument ARG_ENTRAPMENT_RATIO = Value(@"entrapment-ratio", @"<ratio>");
        public static readonly CarafeArgument ARG_NO_SIMILARITY_GATE = Flag(@"no-similarity-gate");
        public static readonly CarafeArgument ARG_IGNORE_PAIRING_ERRORS = Flag(@"ignore-pairing-errors");

        private static readonly CarafeArgumentGroup GROUP_ENTRAPMENT = new CarafeArgumentGroup(() => @"Entrapment FASTA build", true,
            ARG_ENTRAPMENT, ARG_NO_DECOYS, ARG_DECOY_PREFIX, ARG_MZ_FILTER, ARG_ENTRAPMENT_SEED, ARG_DECOY_SEED,
            ARG_ENTRAPMENT_DB, ARG_ENTRAPMENT_RATIO, ARG_NO_SIMILARITY_GATE, ARG_IGNORE_PAIRING_ERRORS);

        // --- Library prediction ------------------------------------------------------------
        public static readonly CarafeArgument ARG_LF_TYPE = Value(@"lf-type", @"<DIA-NN|EncyclopeDIA|Skyline|blib>");
        public static readonly CarafeArgument ARG_FAST = Flag(@"fast");
        public static readonly CarafeArgument ARG_LF_FRAG_MZ_MIN = Value(@"lf-frag-mz-min", @"<mz>");
        public static readonly CarafeArgument ARG_LF_FRAG_MZ_MAX = Value(@"lf-frag-mz-max", @"<mz>");
        public static readonly CarafeArgument ARG_LF_TOP_N_FRAG = Value(@"lf-top-n-frag", @"<n>");
        public static readonly CarafeArgument ARG_LF_MIN_N_FRAG = Value(@"lf-min-n-frag", @"<n>");
        public static readonly CarafeArgument ARG_LF_FRAG_N_MIN = Value(@"lf-frag-n-min", @"<n>");
        public static readonly CarafeArgument ARG_NCE = Value(@"nce", @"<nce>");
        public static readonly CarafeArgument ARG_MS_INSTRUMENT = Value(@"ms-instrument", @"<name>");
        public static readonly CarafeArgument ARG_ACTIVATION = Choice(@"activation", AcquisitionVocabulary.KNOWN_ACTIVATIONS.ToArray());
        public static readonly CarafeArgument ARG_ANALYZER = Choice(@"analyzer", AcquisitionVocabulary.KNOWN_ANALYZERS.ToArray());
        public static readonly CarafeArgument ARG_RT_MAX = Value(@"rt-max", @"<minutes>");
        public static readonly CarafeArgument ARG_CCS = Flag(@"ccs");
        public static readonly CarafeArgument ARG_PAIRING_MANIFEST = Value(@"pairing-manifest", PATH_TO_FILE + EXT_TSV);
        public static readonly CarafeArgument ARG_LIBRARY_CONTRACT = Flag(@"library-contract");
        public static readonly CarafeArgument ARG_RT_REFERENCE = Value(@"rt-reference", @"<run>");

        private static readonly CarafeArgumentGroup GROUP_LIBRARY = new CarafeArgumentGroup(() => @"Library prediction", true,
            ARG_LF_TYPE, ARG_FAST, ARG_LF_FRAG_MZ_MIN, ARG_LF_FRAG_MZ_MAX, ARG_LF_TOP_N_FRAG, ARG_LF_MIN_N_FRAG, ARG_LF_FRAG_N_MIN,
            ARG_NCE, ARG_MS_INSTRUMENT, ARG_ACTIVATION, ARG_ANALYZER, ARG_RT_MAX, ARG_CCS, ARG_PAIRING_MANIFEST, ARG_LIBRARY_CONTRACT,
            ARG_RT_REFERENCE);

        // --- Models ------------------------------------------------------------------------
        public static readonly CarafeArgument ARG_MODEL_DIR = Value(@"model-dir", PATH_TO_FOLDER);
        public static readonly CarafeArgument ARG_MODEL = Value(@"model", PATH_TO_FILE + CarafeModelFile.EXTENSION);
        public static readonly CarafeArgument ARG_PRETRAINED = Value(@"pretrained", PATH_TO_FILE + @".zip");
        public static readonly CarafeArgument ARG_TF = Choice(@"tf", @"all", @"ms2", @"rt");
        public static readonly CarafeArgument ARG_RT_MODEL = Choice(@"rt-model", Enum.GetNames(typeof(RtModelType)));
        public static readonly CarafeArgument ARG_DEVICE = Choice(@"device", DEVICES);
        public static readonly CarafeArgument ARG_MS2_MODEL = Value(@"ms2-model", @"<model>");

        private static readonly CarafeArgumentGroup GROUP_MODELS = new CarafeArgumentGroup(() => @"Models", true,
            ARG_MODEL_DIR, ARG_MODEL, ARG_PRETRAINED, ARG_TF, ARG_RT_MODEL, ARG_DEVICE, ARG_MS2_MODEL);

        // --- Training ----------------------------------------------------------------------
        public static readonly CarafeArgument ARG_SE = Value(@"se", @"<Osprey>");
        public static readonly CarafeArgument ARG_FDR = Value(@"fdr", @"<q>");
        public static readonly CarafeArgument ARG_COR = Value(@"cor", @"<r>");
        public static readonly CarafeArgument ARG_N_ION_MIN = Value(@"n-ion-min", @"<n>");
        public static readonly CarafeArgument ARG_C_ION_MIN = Value(@"c-ion-min", @"<n>");
        public static readonly CarafeArgument ARG_NF = Value(@"nf", @"<n>");
        public static readonly CarafeArgument ARG_MIN_N = Value(@"min-n", @"<n>");
        public static readonly CarafeArgument ARG_VALID = Flag(@"valid");
        public static readonly CarafeArgument ARG_NO_MASKING = Flag(@"no-masking");
        public static readonly CarafeArgument ARG_SEED = Value(@"seed", @"<n>");
        public static readonly CarafeArgument ARG_RT_ALIGN = Choice(@"rt-align", Enum.GetNames(typeof(RtAlignmentType)));
        public static readonly CarafeArgument ARG_RT_SELECT = Choice(@"rt-select", Enum.GetNames(typeof(RtSelectionType)));

        private static readonly CarafeArgumentGroup GROUP_TRAINING = new CarafeArgumentGroup(() => @"Training", true,
            ARG_SE, ARG_FDR, ARG_COR, ARG_N_ION_MIN, ARG_C_ION_MIN, ARG_NF, ARG_MIN_N, ARG_VALID, ARG_NO_MASKING, ARG_SEED,
            ARG_RT_ALIGN, ARG_RT_SELECT);

        // --- Output (CarafeSharp's, as Osprey's --log-file / --timestamp / --memstamp) --------
        public static readonly CarafeArgument ARG_LOG_FILE = Value(@"log-file", PATH_TO_FILE);
        public static readonly CarafeArgument ARG_TIMESTAMP = Flag(@"timestamp");
        public static readonly CarafeArgument ARG_MEMSTAMP = Flag(@"memstamp");

        private static readonly CarafeArgumentGroup GROUP_OUTPUT = new CarafeArgumentGroup(() => @"Output", true,
            ARG_LOG_FILE, ARG_TIMESTAMP, ARG_MEMSTAMP);

        // --- Carafe options accepted for compatibility -------------------------------------
        // The rest of Carafe 2.2.0's options, so CarafeSharp accepts exactly what Carafe's GUI
        // passes. Most are ignored, as Carafe ignores them in these modes; a few are checked
        // (only one value is supported) or refused (not ported), with the message Interpret gives.
        public static readonly CarafeArgument ARG_ITOL = Value(@"itol", @"<tolerance>");
        public static readonly CarafeArgument ARG_ITOLU = Value(@"itolu", @"<unit>");
        public static readonly CarafeArgument ARG_SG = Value(@"sg", @"<n>");
        public static readonly CarafeArgument ARG_NA = Value(@"na", @"<n>");
        public static readonly CarafeArgument ARG_PTM_SITE_PROB = Value(@"ptm-site-prob", @"<p>");
        public static readonly CarafeArgument ARG_PTM_SITE_QVALUE = Value(@"ptm-site-qvalue", @"<q>");
        public static readonly CarafeArgument ARG_MIN_MZ = Value(@"min-mz", @"<mz>");
        public static readonly CarafeArgument ARG_LF_FORMAT = Value(@"lf-format", @"<format>");
        public static readonly CarafeArgument ARG_RF_RT_WIN = Value(@"rf-rt-win", @"<window>");
        public static readonly CarafeArgument ARG_RT_WIN_OFFSET = Value(@"rt-win-offset", @"<minutes>");
        public static readonly CarafeArgument ARG_DATA_TYPE = Value(@"data-type", @"<type>");
        public static readonly CarafeArgument ARG_BUILD_KOINA_LIBRARY = Value(@"build-koina-library", @"<out>");
        public static readonly CarafeArgument ARG_KOINA_URL = Value(@"koina-url", @"<url>");
        public static readonly CarafeArgument ARG_KOINA_MS2_MODEL = Value(@"koina-ms2-model", @"<name>");
        public static readonly CarafeArgument ARG_KOINA_RT_MODEL = Value(@"koina-rt-model", @"<name>");
        public static readonly CarafeArgument ARG_NCE_MS = Value(@"nce-ms", @"<nce>");
        public static readonly CarafeArgument ARG_MODE = Value(@"mode", @"<mode>");
        public static readonly CarafeArgument ARG_PYTHON = Value(@"python", PATH_TO_FILE);
        public static readonly CarafeArgument ARG_MOD2MASS = Value(@"mod2mass", @"<mapping>");
        public static readonly CarafeArgument ARG_USER_VAR_MODS = Value(@"user-var-mods", @"<mods>");
        public static readonly CarafeArgument ARG_VERBOSE = Value(@"verbose", @"<level>");
        public static readonly CarafeArgument ARG_AI_VERSION = Value(@"ai-version", @"<version>");
        public static readonly CarafeArgument ARG_PRINT_PTM = Flag(@"print-ptm");
        public static readonly CarafeArgument ARG_NM = Flag(@"nm");
        public static readonly CarafeArgument ARG_CS = Flag(@"cs");
        public static readonly CarafeArgument ARG_EZ = Flag(@"ez");
        public static readonly CarafeArgument ARG_SKYLINE = Flag(@"skyline");
        public static readonly CarafeArgument ARG_USE_ALL_PEAKS = Flag(@"use-all-peaks");
        public static readonly CarafeArgument ARG_RF = Flag(@"rf");
        public static readonly CarafeArgument ARG_XIC = Flag(@"xic");
        public static readonly CarafeArgument ARG_EXPORT_MGF = Flag(@"export-mgf");
        public static readonly CarafeArgument ARG_Y1 = Flag(@"y1");
        public static readonly CarafeArgument ARG_TORCH_COMPILE = Flag(@"torch-compile");

        private static readonly CarafeArgumentGroup GROUP_COMPATIBILITY = new CarafeArgumentGroup(() => @"Carafe options accepted for compatibility", true,
            ARG_ITOL, ARG_ITOLU, ARG_SG, ARG_NA, ARG_PTM_SITE_PROB, ARG_PTM_SITE_QVALUE, ARG_MIN_MZ, ARG_LF_FORMAT, ARG_RF_RT_WIN,
            ARG_RT_WIN_OFFSET, ARG_DATA_TYPE, ARG_BUILD_KOINA_LIBRARY, ARG_KOINA_URL, ARG_KOINA_MS2_MODEL, ARG_KOINA_RT_MODEL,
            ARG_NCE_MS, ARG_MODE, ARG_PYTHON, ARG_MOD2MASS, ARG_USER_VAR_MODS, ARG_VERBOSE, ARG_AI_VERSION, ARG_PRINT_PTM, ARG_NM,
            ARG_CS, ARG_EZ, ARG_SKYLINE, ARG_USE_ALL_PEAKS, ARG_RF, ARG_XIC, ARG_EXPORT_MGF, ARG_Y1, ARG_TORCH_COMPILE)
        {
            Preamble = () => @"Carafe 2.2.0 options these modes do not read. Each is accepted so a Carafe command line " +
                             @"runs unchanged; one that CarafeSharp cannot honor is refused, and the rest are ignored, as Carafe ignores them."
        };

        /// <summary>
        /// The usage, as blocks, laid out as Osprey's: the command lines of each mode, the
        /// argument groups, then the other forms the parser accepts.
        /// </summary>
        public static IEnumerable<IUsageBlock> UsageBlocks
        {
            get
            {
                return new[]
                {
                    new ParaUsageBlock(@"CarafeSharp - spectral library prediction and fine-tuning for Osprey (C# port of Carafe)"),
                    new ParaUsageBlock(@"USAGE: CarafeSharp " + ARG_BUILD_ENTRAPMENT_FASTA.ArgumentText + @" " + PATH_TO_FILE + EXT_FASTA + @" " +
                                       ARG_DB.ArgumentText + @" " + PATH_TO_FILE + EXT_FASTA + @" [options]"),
                    new ParaUsageBlock(@"       CarafeSharp " + ARG_RECONCILE_MANIFEST.ArgumentText + @" " + PATH_TO_FILE + EXT_TSV + @" " +
                                       ARG_MANIFEST.ArgumentText + @" " + PATH_TO_FILE + EXT_TSV + @" " +
                                       ARG_PREDICTED_LIBRARY.ArgumentText + @" " + PATH_TO_FILE + EXT_TSV + @"|.blib"),
                    new ParaUsageBlock(@"       CarafeSharp " + ARG_DB.ArgumentText + @" " + PATH_TO_FILE + EXT_FASTA + @" " + ARG_O.ArgumentText +
                                       @" " + PATH_TO_FOLDER + @" [options]   (library prediction)"),
                    new ParaUsageBlock(@"       CarafeSharp " + ARG_I.ArgumentText + @" " + PATH_TO_FILE + @".blib " + ARG_MS.ArgumentText + @" <runs> " +
                                       ARG_O.ArgumentText + @" " + PATH_TO_FOLDER + @" [" + ARG_DB.ArgumentText + @" " + PATH_TO_FILE + EXT_FASTA +
                                       @"] [options]   (training)"),
                    new ParaUsageBlock(@"       CarafeSharp " + ARG_I.ArgumentText + @" " + PATH_TO_FILE + TrainingExportParquet.EXT + @"|" + PATH_TO_FOLDER +
                                       @" [" + ARG_MS.ArgumentText + @" <runs>] " + ARG_O.ArgumentText + @" " + PATH_TO_FOLDER + @" [" +
                                       ARG_DB.ArgumentText + @" " + PATH_TO_FILE + EXT_FASTA + @"] [options]"),
                    new ParaUsageBlock(@"       CarafeSharp " + ARG_MODEL_INFO.ArgumentText + @" " + PATH_TO_FILE + CarafeModelFile.EXTENSION),
                    GROUP_MODES,
                    GROUP_DIGEST,
                    EnzymeTableBlock,
                    ModificationTableBlock,
                    GROUP_ENTRAPMENT,
                    GROUP_LIBRARY,
                    GROUP_MODELS,
                    GROUP_TRAINING,
                    GROUP_OUTPUT,
                    GROUP_COMPATIBILITY,
                    new ParaUsageBlock(@"OTHER FORMS ACCEPTED: the options are listed in the standard form (" + ARG_LF_TYPE.ArgumentText +
                                       @" blib). A value may follow its option or be joined to it (" + ARG_LF_TYPE.ArgumentText + @"=blib), " +
                                       @"and for compatibility with Java Carafe every option is also accepted in Carafe's form, with a single " +
                                       @"dash, underscores and camel case (-lf_type blib, -lf_type=blib, -minLength 7), so a Carafe 2.2.0 command " +
                                       @"line runs unchanged. " +
                                       @"A repeated option keeps its first value, as in Carafe."),
                };
            }
        }

        /// <summary>
        /// The digest and modification options a host presents in a library-build section of its
        /// own command line: what decides which peptides a library holds.
        /// </summary>
        public static IReadOnlyList<CarafeArgument> HostedDigestArguments
        {
            get
            {
                return new[]
                {
                    ARG_ENZYME, ARG_MISS_C, ARG_MIN_LENGTH, ARG_MAX_LENGTH, ARG_CLIP_N_M, ARG_FIX_MOD, ARG_VAR_MOD, ARG_MAX_VAR,
                    ARG_MIN_PEP_MZ, ARG_MAX_PEP_MZ, ARG_MIN_PEP_CHARGE, ARG_MAX_PEP_CHARGE, ARG_I2L,
                };
            }
        }

        /// <summary>
        /// Carafe's enzymes, by the index <see cref="ARG_ENZYME"/> takes, and (the next block)
        /// its modifications, by the id
        /// <see cref="ARG_FIX_MOD"/> and <see cref="ARG_VAR_MOD"/> take, generated from the tables
        /// the digest reads so the help cannot drift from them. The help is the only place a user
        /// can learn what <c>--enzyme 7</c> or <c>--var-mod 2,7</c> means, so a host that presents
        /// the digest options presents this after them too.
        /// </summary>
        public static IUsageBlock EnzymeTableBlock
        {
            get
            {
                return new TableUsageBlock(@"Enzymes", new[] { @"#", @"Enzyme" },
                    EnzymeTable.All.Select((enzyme, index) => new[]
                    {
                        index.ToString(CultureInfo.InvariantCulture), enzyme.Name
                    }))
                {
                    Preamble = ARG_ENZYME.ArgumentText + @" takes the number or the name",
                    ColumnGroups = 3,
                    RightAlignedColumns = new[] { 0 }
                };
            }
        }

        /// <summary>The modifications half of the digest tables: see <see cref="EnzymeTableBlock"/>.</summary>
        public static IUsageBlock ModificationTableBlock
        {
            get
            {
                return new TableUsageBlock(@"Modifications", new[] { @"#", @"Modification", @"Mass" },
                    CarafeModification.TopModifications.Select(modification => new[]
                    {
                        modification.Id.ToString(CultureInfo.InvariantCulture), modification.Name,
                        modification.Mass.ToString(@"+0.0000;-0.0000", CultureInfo.InvariantCulture)
                    }))
                {
                    Preamble = ARG_FIX_MOD.ArgumentText + @" and " + ARG_VAR_MOD.ArgumentText + @" take the numbers, comma-separated",
                    ColumnGroups = 2,
                    RightAlignedColumns = new[] { 0, 2 }
                };
            }
        }

        /// <summary>
        /// The prediction options a host presents: the fragments kept, the acquisition the models
        /// predict for, the RT model and the pretrained archive. Not among them: what the host
        /// derives (the FASTA, the output, the format, the pairing manifest), the saved model and
        /// the device, which the host declares under its own names, training, and the training-run
        /// RT reference, which only a model trained with aligned runs can honor and so belongs
        /// with the tuning options.
        /// </summary>
        public static IReadOnlyList<CarafeArgument> HostedPredictionArguments
        {
            get
            {
                return new[]
                {
                    ARG_LF_FRAG_MZ_MIN, ARG_LF_FRAG_MZ_MAX, ARG_LF_TOP_N_FRAG, ARG_LF_MIN_N_FRAG, ARG_LF_FRAG_N_MIN, ARG_NCE,
                    ARG_MS_INSTRUMENT, ARG_ACTIVATION, ARG_ANALYZER, ARG_RT_MAX, ARG_CCS, ARG_RT_MODEL, ARG_PRETRAINED,
                };
            }
        }

        /// <summary>
        /// The tuning options a host presents: Carafe's rules for which matched ions train the MS2
        /// model, which models train, the seed, and how the training runs' retention times are
        /// aligned - with the training-run RT reference, which only a model trained on aligned runs
        /// can honor. Not among them: what the host derives (the exports, the runs, the output, the
        /// q-value a training precursor needs, the saved model to start from and the device).
        /// </summary>
        public static IReadOnlyList<CarafeArgument> HostedTrainingArguments
        {
            get
            {
                return new[]
                {
                    ARG_TF, ARG_COR, ARG_N_ION_MIN, ARG_C_ION_MIN, ARG_NF, ARG_MIN_N, ARG_VALID, ARG_NO_MASKING, ARG_SEED,
                    ARG_RT_ALIGN, ARG_RT_SELECT, ARG_RT_REFERENCE,
                };
            }
        }

        /// <summary>
        /// The usage text of the argument named <paramref name="argName"/>: its own, or the one
        /// line every compatibility option shares; null for a name that is not declared here. The
        /// provider installed by CarafeSharp.exe reads it, and so does a host's provider for the
        /// hosted arguments.
        /// </summary>
        public static string GetUsageText(string argName)
        {
            string description = CarafeCommandArgUsage.ResourceManager.GetString(UsageKey(argName));
            if (description != null)
                return description;
            return CompatibilityArguments.Any(a => a.Name == argName)
                ? @"Accepted for Carafe compatibility; not used by CarafeSharp."
                : null;
        }

        /// <summary>
        /// The suffix on a usage key under which an argument's name as a property of a settings
        /// page is held (<c>_nce_property</c>), in <see cref="CarafeCommandArgUsage"/> and in a
        /// host's usage resources for its own arguments.
        /// </summary>
        public const string PROPERTY_SUFFIX = @"_property";

        /// <summary>
        /// The name of the argument <paramref name="argName"/> as a property of a settings page
        /// (the label of its grid row), or null for an argument that is none.
        /// </summary>
        public static string GetPropertyName(string argName)
        {
            return CarafeCommandArgUsage.ResourceManager.GetString(UsageKey(argName) + PROPERTY_SUFFIX);
        }

        /// <summary>
        /// The hosted arguments as properties of a host's settings page (Skyline's Additional
        /// settings grid, through Osprey): what each takes, its default, and its range. The
        /// defaults are the effective ones (see the class comment), as the help text gives them.
        /// </summary>
        private static void DeclareProperties()
        {
            // Digest
            Property(ARG_ENZYME, ArgumentPropertyType.text, EnzymeTable.DEFAULT_ENZYME_INDEX);
            Property(ARG_MISS_C, ArgumentPropertyType.integer, DigestSettings.DEFAULT_MAX_MISSED_CLEAVAGES, 0);
            Property(ARG_MIN_LENGTH, ArgumentPropertyType.integer, DigestSettings.DEFAULT_MIN_LENGTH, 1);
            Property(ARG_MAX_LENGTH, ArgumentPropertyType.integer, DigestSettings.DEFAULT_MAX_LENGTH, 1);
            Property(ARG_CLIP_N_M);
            Property(ARG_FIX_MOD, ArgumentPropertyType.text, ModificationSettings.DEFAULT_FIXED_MODIFICATIONS);
            Property(ARG_VAR_MOD, ArgumentPropertyType.text, ModificationSettings.DEFAULT_VARIABLE_MODIFICATIONS);
            Property(ARG_MAX_VAR, ArgumentPropertyType.integer, new ModificationSettings().MaxVariableModifications, 0);
            Property(ARG_MIN_PEP_MZ, ArgumentPropertyType.number, DEFAULT_MIN_PEPTIDE_MZ, 0);
            Property(ARG_MAX_PEP_MZ, ArgumentPropertyType.number, DEFAULT_MAX_PEPTIDE_MZ, 0);
            Property(ARG_MIN_PEP_CHARGE, ArgumentPropertyType.integer, LibrarySettings.DEFAULT_MIN_CHARGE, 1);
            Property(ARG_MAX_PEP_CHARGE, ArgumentPropertyType.integer, LibrarySettings.DEFAULT_MAX_CHARGE, 1);
            Property(ARG_I2L);
            // Library prediction
            Property(ARG_LF_FRAG_MZ_MIN, ArgumentPropertyType.number, LibrarySettings.DEFAULT_MIN_FRAGMENT_MZ, 0);
            Property(ARG_LF_FRAG_MZ_MAX, ArgumentPropertyType.number, LibrarySettings.DEFAULT_MAX_FRAGMENT_MZ, 0);
            Property(ARG_LF_TOP_N_FRAG, ArgumentPropertyType.integer, LibrarySettings.DEFAULT_TOP_FRAGMENTS, 1);
            Property(ARG_LF_MIN_N_FRAG, ArgumentPropertyType.integer, LibrarySettings.DEFAULT_MIN_FRAGMENTS, 1);
            Property(ARG_LF_FRAG_N_MIN, ArgumentPropertyType.integer, LibrarySettings.DEFAULT_MIN_FRAGMENT_NUMBER, 1);
            Property(ARG_NCE, ArgumentPropertyType.number, LibrarySettings.DEFAULT_NCE, 0);
            Property(ARG_MS_INSTRUMENT, ArgumentPropertyType.text, LibrarySettings.DEFAULT_INSTRUMENT);
            Property(ARG_ACTIVATION);
            Property(ARG_ANALYZER);
            Property(ARG_RT_MAX, ArgumentPropertyType.number, null, 0);
            Property(ARG_CCS);
            Property(ARG_RT_MODEL);
            Property(ARG_PRETRAINED, ArgumentPropertyType.path);
            // Training
            Property(ARG_TF, null, LibrarySettings.DEFAULT_TRAINING_TYPE);
            Property(ARG_COR, ArgumentPropertyType.number, TrainingSettings.DEFAULT_CORRELATION, 0, 1);
            Property(ARG_N_ION_MIN, ArgumentPropertyType.integer, 0, 0);
            Property(ARG_C_ION_MIN, ArgumentPropertyType.integer, 0, 0);
            Property(ARG_NF, ArgumentPropertyType.integer, TrainingSettings.DEFAULT_MIN_MATCHED_IONS, 0);
            Property(ARG_MIN_N, ArgumentPropertyType.integer, TrainingSettings.DEFAULT_MIN_VALID_IONS, 0);
            Property(ARG_VALID);
            Property(ARG_NO_MASKING);
            Property(ARG_SEED, ArgumentPropertyType.integer, TrainingSettings.DEFAULT_SEED, 0);
            Property(ARG_RT_ALIGN, null, TrainingSettings.DEFAULT_RT_ALIGNMENT);
            Property(ARG_RT_SELECT, null, new TrainingSettings().RtSelection);
            Property(ARG_RT_REFERENCE, ArgumentPropertyType.text);
        }

        /// <summary>
        /// Makes <paramref name="arg"/> a property, named by <see cref="GetPropertyName"/>, of
        /// <paramref name="type"/> (null for the type its declaration implies: a flag, a list or
        /// text) with <paramref name="defaultValue"/> spelled as the command line takes it, within
        /// <paramref name="min"/> and <paramref name="max"/>.
        /// </summary>
        private static void Property(CarafeArgument arg, ArgumentPropertyType? type = null, object defaultValue = null,
            double? min = null, double? max = null)
        {
            arg.PropertyName = () => GetPropertyName(arg.Name);
            arg.PropertyType = type;
            if (defaultValue != null)
            {
                string text = defaultValue is Enum e
                    ? e.ToString().ToLowerInvariant()
                    : Convert.ToString(defaultValue, CultureInfo.InvariantCulture);
                arg.DefaultValue = () => text;
            }
            arg.MinValue = min;
            arg.MaxValue = max;
        }

        /// <summary>An empty command line for a host to <see cref="Record"/> into and then interpret.</summary>
        public static CarafeCommandLine CreateHosted()
        {
            return new CarafeCommandLine();
        }

        /// <summary>Every declared argument, in help order.</summary>
        public static IEnumerable<CarafeArgument> AllArguments
        {
            get { return UsageBlocks.OfType<CarafeArgumentGroup>().SelectMany(g => g.Args); }
        }

        /// <summary>The arguments Carafe accepts that these modes do not read (<see cref="GROUP_COMPATIBILITY"/>).</summary>
        public static IEnumerable<CarafeArgument> CompatibilityArguments
        {
            get { return GROUP_COMPATIBILITY.Args; }
        }

        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        private readonly HashSet<string> _flags = new HashSet<string>();

        private CarafeCommandLine()
        {
        }

        /// <summary>
        /// Parses and interprets <paramref name="args"/>; throws <see cref="ArgumentException"/>
        /// wherever Carafe would stop with an error.
        /// </summary>
        public static CarafeCommandLine Parse(IReadOnlyList<string> args)
        {
            var commandLine = new CarafeCommandLine();
            new CommandLineParser<CarafeCommandLine>(AllArguments).Parse(commandLine, args);
            commandLine.Interpret(args.Count);
            if (commandLine.LibrarySettings != null)
                commandLine.LibrarySettings.CommandLine = CommandLineText.Format(@"CarafeSharp", args);
            return commandLine;
        }

        public CarafeCommandMode Mode { get; private set; }

        /// <summary>The build settings, in <see cref="CarafeCommandMode.build_entrapment_fasta"/> mode.</summary>
        public EntrapmentFastaSettings BuildSettings { get; private set; }

        /// <summary>The manifest to reconcile (<c>-manifest</c>), in reconcile mode.</summary>
        public string ReconcileManifestIn { get; private set; }

        /// <summary>The saved model <c>-model_info</c> describes.</summary>
        public string ModelInfoPath { get; private set; }

        /// <summary>The predicted library (<c>-predicted_library</c>), in reconcile mode.</summary>
        public string ReconcileLibrary { get; private set; }

        /// <summary>Where to write the reconciled manifest (<c>-reconcile_manifest</c>).</summary>
        public string ReconcileManifestOut { get; private set; }

        /// <summary>The library to predict, in <see cref="CarafeCommandMode.predict_library"/> mode.</summary>
        public LibrarySettings LibrarySettings { get; private set; }

        /// <summary>The fine-tuning run for <see cref="CarafeCommandMode.train"/>.</summary>
        public TrainingSettings TrainingSettings { get; private set; }

        /// <summary>Warnings Carafe prints for audit switches.</summary>
        public IList<string> Warnings { get; } = new List<string>();

        /// <summary>The file every line of output and error goes to (<c>-log_file</c>), or null for the console.</summary>
        public string LogFile { get; private set; }

        /// <summary>Prefix each line with the time (<c>-timestamp</c>).</summary>
        public bool TimeStamp { get; private set; }

        /// <summary>Prefix each line with the managed and private memory in use (<c>-memstamp</c>).</summary>
        public bool MemStamp { get; private set; }

        /// <summary>The help format <c>-h</c> was given (ascii, unicode, sections, html or a section name), or null for the default.</summary>
        public string HelpFormat { get; private set; }

        /// <summary>The one-line pointer a usage error ends with, in place of the whole usage.</summary>
        public static string UsageHint
        {
            get { return @"Run CarafeSharp " + ARG_HELP.ArgumentText + @" for the options."; }
        }

        private void Interpret(int argCount)
        {
            if (Has(ARG_HELP) || argCount == 0)
            {
                Mode = CarafeCommandMode.help;
                HelpFormat = TryGet(ARG_HELP, out string helpFormat) ? helpFormat : null;
                return;
            }
            if (Has(ARG_PRINT_PTM))
                throw new ArgumentException(ARG_PRINT_PTM.ArgumentText + @" is not supported by CarafeSharp");
            // Output options, read in every mode (CarafeSharp's, as Osprey's --log-file / --timestamp / --memstamp).
            if (TryGet(ARG_LOG_FILE, out string logFile))
                LogFile = logFile;
            TimeStamp = Has(ARG_TIMESTAMP);
            MemStamp = Has(ARG_MEMSTAMP);

            var digest = InterpretDigest(out var modifications, out double minMz, out double maxMz);

            if (Has(ARG_BUILD_ENTRAPMENT_FASTA))
            {
                Mode = CarafeCommandMode.build_entrapment_fasta;
                BuildSettings = InterpretBuild(digest, modifications, minMz, maxMz);
                return;
            }
            if (TryGet(ARG_MODEL_INFO, out string modelInfo))
            {
                Mode = CarafeCommandMode.model_info;
                ModelInfoPath = modelInfo;
                return;
            }
            if (Has(ARG_RECONCILE_MANIFEST))
            {
                Mode = CarafeCommandMode.reconcile_manifest;
                ReconcileManifestOut = _values[ARG_RECONCILE_MANIFEST.Name];
                if (!TryGet(ARG_MANIFEST, out string manifestIn) || manifestIn.Length == 0)
                    throw new ArgumentException(ARG_RECONCILE_MANIFEST.ArgumentText + @" requires the input manifest via " + ARG_MANIFEST.ArgumentText);
                if (!TryGet(ARG_PREDICTED_LIBRARY, out string library) || library.Length == 0)
                    throw new ArgumentException(ARG_RECONCILE_MANIFEST.ArgumentText + @" requires the predicted library TSV via " + ARG_PREDICTED_LIBRARY.ArgumentText);
                ReconcileManifestIn = manifestIn;
                ReconcileLibrary = library;
                return;
            }
            if (Has(ARG_BUILD_KOINA_LIBRARY))
                throw new NotSupportedException(ARG_BUILD_KOINA_LIBRARY.ArgumentText + @" is not supported by CarafeSharp");
            if (Has(ARG_MODEL))
            {
                if (Has(ARG_MODEL_DIR))
                    throw new ArgumentException(ARG_MODEL.ArgumentText + @" and " + ARG_MODEL_DIR.ArgumentText + @" both name the models to predict with; give one");
                if (Has(ARG_MS2_MODEL))
                    throw new ArgumentException(ARG_MODEL.ArgumentText + @" and " + ARG_MS2_MODEL.ArgumentText + @" both name the MS2 model to start from; give one");
                bool training = Has(ARG_MS) || NamesTrainingExports();
                if (training)
                    CheckModelTrainingType();
                if (!training && !Has(ARG_DB))
                    throw new ArgumentException(ARG_MODEL.ArgumentText + @" predicts a library, which needs the FASTA to predict via " + ARG_DB.ArgumentText);
            }
            // Carafe trains when -ms is given. CarafeSharp also trains on Osprey's training exports
            // named by -i directly, which Carafe could never have read; -i alone is otherwise
            // ignored, as Carafe ignores it.
            if ((Has(ARG_MS) || NamesTrainingExports()) && !Has(ARG_MODEL_DIR))
            {
                Mode = CarafeCommandMode.train;
                TrainingSettings = InterpretTraining(digest, modifications, minMz, maxMz);
                return;
            }
            if (Has(ARG_DB))
            {
                Mode = CarafeCommandMode.predict_library;
                LibrarySettings = InterpretLibrary(digest, modifications, minMz, maxMz);
                var trainingOnly = new[] { ARG_RT_ALIGN, ARG_RT_SELECT }.Where(Has).ToArray();
                if (trainingOnly.Length > 0)
                {
                    Warnings.Add(@"Ignored " + string.Join(@" ", trainingOnly.Select(o => o.ArgumentText)) +
                                 @": they align training runs, and this run only predicts a library.");
                }
                return;
            }
            throw new ArgumentException(string.Format(@"CarafeSharp supports {0}, {1}, library prediction from {2} " +
                                        @"and training from {3} with {4} (or {3} naming Osprey's training exports)",
                                        ARG_BUILD_ENTRAPMENT_FASTA.ArgumentText, ARG_RECONCILE_MANIFEST.ArgumentText, ARG_DB.ArgumentText, ARG_I.ArgumentText, ARG_MS.ArgumentText));
        }

        /// <summary>
        /// The library a host builds from <paramref name="database"/> with the options it recorded
        /// (<see cref="Record"/>) from <see cref="HostedDigestArguments"/> and
        /// <see cref="HostedPredictionArguments"/>. The values the host derives are filled in here -
        /// the FASTA, a .blib output, and the saved model and device when it gives them - and the
        /// rest is read as a CarafeSharp command line's is, with the same refusals.
        /// <paramref name="commandLine"/> is the host's own command line, recorded in the library's
        /// metadata as how it was made.
        /// </summary>
        public LibrarySettings InterpretHostedLibrary(string database, string modelFile, string device, string commandLine)
        {
            Set(ARG_DB, database);
            Set(ARG_LF_TYPE, LibraryOutputs.BLIB_FORMAT);
            if (modelFile != null)
                Set(ARG_MODEL, modelFile);
            if (device != null)
                Set(ARG_DEVICE, device);
            var digest = InterpretDigest(out var modifications, out double minMz, out double maxMz);
            Mode = CarafeCommandMode.predict_library;
            LibrarySettings = InterpretLibrary(digest, modifications, minMz, maxMz);
            LibrarySettings.CommandLine = commandLine;
            return LibrarySettings;
        }

        /// <summary>
        /// The library a host predicts for a precursor table (Osprey's <c>--precursors</c>, which
        /// Skyline's Build Library runs): no FASTA and no digest, the saved model and device when the
        /// host gives them, and the prediction options it recorded read as a CarafeSharp command
        /// line's are, with the same refusals.
        /// </summary>
        public LibrarySettings InterpretHostedPrecursorTable(string modelFile, string device, string commandLine)
        {
            if (modelFile != null)
                Set(ARG_MODEL, modelFile);
            if (device != null)
                Set(ARG_DEVICE, device);
            var digest = InterpretDigest(out var modifications, out double minMz, out double maxMz);
            Mode = CarafeCommandMode.predict_library;
            LibrarySettings = InterpretLibrarySettings(null, digest, modifications, minMz, maxMz);
            LibrarySettings.CommandLine = commandLine;
            return LibrarySettings;
        }

        /// <summary>
        /// The <see cref="HostedDigestArguments"/> the host recorded, as typed: a host predicting a
        /// precursor table, which digests nothing, refuses them by name rather than ignoring them.
        /// </summary>
        public IEnumerable<string> HostedDigestOptionsGiven()
        {
            return HostedDigestArguments.Where(Has).Select(arg => arg.ArgumentText);
        }

        /// <summary>
        /// The fine-tuning a host runs on Osprey's training exports, with the options it recorded
        /// from <see cref="HostedTrainingArguments"/> and the library sections. The rest is derived as
        /// <see cref="InterpretHostedLibrary"/> derives it: <paramref name="database"/>, the saved model
        /// to start from (<paramref name="modelFile"/>, or null for the pretrained models), the device,
        /// and <paramref name="fdr"/>, the run q-value a training precursor needs. The files are the
        /// host's to fill in before the run - the exports and their runs
        /// (<see cref="TrainingSettings.Identifications"/>, <see cref="TrainingSettings.MsFiles"/>), the
        /// model folder and file - since it places them by paths it settles after the parse. The
        /// settings' <see cref="TrainingSettings.Library"/> is the library predicted with the
        /// fine-tuned models from the folder they are written to, as CarafeSharp's training predicts it
        /// after the fine-tune; a host may predict it as a task of its own.
        /// </summary>
        public TrainingSettings InterpretHostedTraining(string database, string modelFile, string device, double fdr, string commandLine)
        {
            Set(ARG_DB, database);
            Set(ARG_LF_TYPE, LibraryOutputs.BLIB_FORMAT);
            Set(ARG_FDR, fdr.ToString(@"R", CultureInfo.InvariantCulture));
            if (modelFile != null)
            {
                Set(ARG_MODEL, modelFile);
                CheckModelTrainingType();
            }
            if (device != null)
                Set(ARG_DEVICE, device);
            var digest = InterpretDigest(out var modifications, out double minMz, out double maxMz);
            Mode = CarafeCommandMode.train;
            TrainingSettings = InterpretTraining(digest, modifications, minMz, maxMz, true);
            TrainingSettings.Library.CommandLine = commandLine;
            return TrainingSettings;
        }

        /// <summary>The digest, the modifications and the precursor m/z window every mode reads first.</summary>
        private DigestSettings InterpretDigest(out ModificationSettings modifications, out double minMz, out double maxMz)
        {
            var digest = new DigestSettings { ClipNTermMethionine = Has(ARG_CLIP_N_M) };
            modifications = new ModificationSettings();
            if (TryGet(ARG_FIX_MOD, out string fixMod))
                modifications.FixedModifications = fixMod;
            if (TryGet(ARG_VAR_MOD, out string varMod))
                modifications.VariableModifications = varMod;
            if (TryGet(ARG_MISS_C, out string missedCleavages))
                digest.MaxMissedCleavages = ParseInt(ARG_MISS_C, missedCleavages);
            if (TryGet(ARG_ENZYME, out string enzyme))
            {
                digest.EnzymeIndex = ParseEnzyme(enzyme);
            }
            // Carafe validates the index here, through its non-specific check.
            if (EnzymeTable.IsNonSpecific(EnzymeTable.GetByIndex(digest.EnzymeIndex)))
                digest.MaxMissedCleavages = EnzymeTable.NON_SPECIFIC_MISSED_CLEAVAGES;
            if (TryGet(ARG_MAX_VAR, out string maxVar))
                modifications.MaxVariableModifications = ParseInt(ARG_MAX_VAR, maxVar);
            if (TryGet(ARG_MIN_LENGTH, out string minLength))
                digest.MinLength = ParseInt(ARG_MIN_LENGTH, minLength);
            if (TryGet(ARG_MAX_LENGTH, out string maxLength))
                digest.MaxLength = ParseInt(ARG_MAX_LENGTH, maxLength);
            minMz = TryGet(ARG_MIN_PEP_MZ, out string minMzText) ? ParseDouble(ARG_MIN_PEP_MZ, minMzText) : DEFAULT_MIN_PEPTIDE_MZ;
            maxMz = TryGet(ARG_MAX_PEP_MZ, out string maxMzText) ? ParseDouble(ARG_MAX_PEP_MZ, maxMzText) : DEFAULT_MAX_PEPTIDE_MZ;
            return digest;
        }

        /// <summary>
        /// Carafe's training mode (<c>-ms</c>), reading Osprey's training exports: CarafeSharp
        /// reads no spectra, so <c>-ms</c> only names the runs, and the options that shape
        /// Carafe's own XIC extraction are Osprey's to decide.
        /// </summary>
        private TrainingSettings InterpretTraining(DigestSettings digest, ModificationSettings modifications, double minMz, double maxMz,
            bool hosted = false)
        {
            // A host names the exports itself, after the parse.
            if ((!TryGet(ARG_I, out string identifications) || identifications.Length == 0) && !hosted)
                throw new ArgumentException(@"Training requires Osprey's results via " + ARG_I.ArgumentText + @" (its blib, or its .training.parquet exports)");
            if (TryGet(ARG_SE, out string searchEngine) && !string.Equals(searchEngine, @"Osprey", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(ARG_SE.ArgumentText + @" " + searchEngine + @" is not supported by CarafeSharp, which trains on Osprey's training export (" + ARG_SE.ArgumentText + @" Osprey)");
            if (TryGet(ARG_MODE, out string mode) && mode != @"-" && !string.Equals(mode, @"general", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(ARG_MODE.ArgumentText + @" " + mode + @" is not supported by CarafeSharp; only general");
            CheckAiVersion();
            if (Has(ARG_USER_VAR_MODS) || Has(ARG_MOD2MASS))
                throw new NotSupportedException(ARG_USER_VAR_MODS.ArgumentText + @" and " + ARG_MOD2MASS.ArgumentText + @" are not supported by CarafeSharp");
            foreach (var option in new[] { ARG_CS, ARG_Y1, ARG_USE_ALL_PEAKS })
            {
                if (Has(option))
                    throw new NotSupportedException(option.ArgumentText + @" is not supported by CarafeSharp training");
            }
            if (Has(ARG_CCS))
            {
                // Carafe does the same with Osprey's results, which carry no ion mobility to train on.
                Warnings.Add(Has(ARG_DB)
                    ? ARG_CCS.ArgumentText + @": the CCS model is not fine-tuned (Osprey's results carry no ion mobility); the library's 1/K0 is predicted " +
                      @"with the pretrained CCS model, or a Carafe ccs_model.pt already in " + ARG_O.ArgumentText + @"."
                    : @"Ignored " + ARG_CCS.ArgumentText + @": the CCS model is not fine-tuned, and without " + ARG_DB.ArgumentText + @" no library is predicted.");
            }
            if (Has(ARG_RT_REFERENCE) && !Has(ARG_DB))
                Warnings.Add(@"Ignored " + ARG_RT_REFERENCE.ArgumentText + @": it names the training run whose minutes a library takes, and without " + ARG_DB.ArgumentText + @" no library is predicted.");
            if (TryGet(ARG_NA, out string flanking) && ParseInt(ARG_NA, flanking) != 0)
                throw new NotSupportedException(ARG_NA.ArgumentText + @" (flanking spectra) is not supported by CarafeSharp training");
            var fromOsprey = new[] { ARG_ITOL, ARG_ITOLU, ARG_RF, ARG_RF_RT_WIN, ARG_RT_WIN_OFFSET, ARG_SG, ARG_MIN_MZ }.Where(Has).ToArray();
            if (fromOsprey.Length > 0)
            {
                Warnings.Add(@"Ignored " + string.Join(@" ", fromOsprey.Select(o => o.ArgumentText)) +
                             @": the fragment matches, XICs and peak boundaries come from Osprey's training export.");
            }
            var notWritten = new[] { ARG_EZ, ARG_XIC, ARG_EXPORT_MGF, ARG_SKYLINE }.Where(Has).ToArray();
            if (notWritten.Length > 0)
                Warnings.Add(@"Ignored " + string.Join(@" ", notWritten.Select(o => o.ArgumentText)) + @": CarafeSharp does not write those diagnostic files.");

            var settings = new TrainingSettings
            {
                Identifications = identifications,
                MsFiles = TryGet(ARG_MS, out string msFiles)
                    ? msFiles.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    : Array.Empty<string>(),
                RequireTopIonValid = Has(ARG_VALID),
                NoMasking = Has(ARG_NO_MASKING),
            };
            if (TryGet(ARG_O, out string output))
                settings.OutputDirectory = output;
            if (TryGet(ARG_FDR, out string fdr))
                settings.Fdr = ParseDouble(ARG_FDR, fdr);
            if (TryGet(ARG_COR, out string correlation))
                settings.MinCorrelation = ParseDouble(ARG_COR, correlation);
            if (TryGet(ARG_N_ION_MIN, out string nIonMin))
                settings.LowOrdinalB = ParseInt(ARG_N_ION_MIN, nIonMin);
            if (TryGet(ARG_C_ION_MIN, out string cIonMin))
                settings.LowOrdinalY = ParseInt(ARG_C_ION_MIN, cIonMin);
            if (TryGet(ARG_LF_FRAG_N_MIN, out string minOrdinal))
                settings.MinFragmentOrdinal = ParseInt(ARG_LF_FRAG_N_MIN, minOrdinal);
            if (TryGet(ARG_NF, out string minMatched))
                settings.MinMatchedIons = ParseInt(ARG_NF, minMatched);
            if (TryGet(ARG_MIN_N, out string minValid))
                settings.MinValidIons = ParseInt(ARG_MIN_N, minValid);
            if (TryGet(ARG_TF, out string trainingType))
            {
                if (!new[] { @"all", @"ms2", @"rt" }.Contains(trainingType, StringComparer.OrdinalIgnoreCase))
                    throw new NotSupportedException(ARG_TF.ArgumentText + @" " + trainingType + @" is not supported by CarafeSharp; all, ms2 or rt");
                settings.TrainingType = trainingType.ToLowerInvariant();
            }
            if (TryGet(ARG_SEED, out string seed))
            {
                // Carafe's Integer.parseInt, and numpy's seed must not be negative.
                int value = ParseInt(ARG_SEED, seed);
                if (value < 0)
                    throw new ArgumentException(ARG_SEED.ArgumentText + @" must not be negative: " + seed);
                settings.Seed = (uint)value;
            }
            if (TryGet(ARG_DEVICE, out string device))
                settings.Device = ParseDevice(device);
            if (TryGet(ARG_NCE, out string nce))
                settings.Nce = ParseDouble(ARG_NCE, nce);
            if (TryGet(ARG_MS_INSTRUMENT, out string instrument))
                settings.Instrument = instrument;
            if (TryGet(ARG_ACTIVATION, out string activation))
                settings.Activation = ParseActivation(activation);
            if (TryGet(ARG_ANALYZER, out string analyzer))
                settings.Analyzer = ParseAnalyzer(analyzer);
            if (TryGet(ARG_RT_MAX, out string rtMax))
                settings.RtMax = ParseDouble(ARG_RT_MAX, rtMax);
            if (TryGet(ARG_MS2_MODEL, out string ms2Model))
                settings.Ms2Model = ms2Model;
            if (TryGet(ARG_MODEL, out string baseModel))
                settings.BaseModel = baseModel;
            if (TryGet(ARG_PRETRAINED, out string pretrained))
                settings.PretrainedModels = pretrained;
            if (TryGet(ARG_RT_MODEL, out string rtModel))
                settings.RtModelType = ParseRtModel(rtModel);
            if (TryGet(ARG_RT_ALIGN, out string rtAlign))
                settings.RtAlignment = ParseName<RtAlignmentType>(ARG_RT_ALIGN, rtAlign);
            if (TryGet(ARG_RT_SELECT, out string rtSelect))
                settings.RtSelection = ParseName<RtSelectionType>(ARG_RT_SELECT, rtSelect);
            if (Has(ARG_DB))
            {
                // The library predicted right after training, with the fine-tuned models in -o,
                // not the -model they were fine-tuned from.
                var library = InterpretLibrary(digest, modifications, minMz, maxMz);
                library.ApplyTrainingRunMeta = true;
                library.TrainingType = settings.TrainingType;
                library.ModelFile = null;
                CheckIonMobilityTrainingType(library);
                settings.Library = library;
            }
            return settings;
        }

        private LibrarySettings InterpretLibrary(DigestSettings digest, ModificationSettings modifications, double minMz, double maxMz)
        {
            string database = _values[ARG_DB.Name];
            if (database.Length == 0)
                throw new ArgumentException(@"Library prediction requires a FASTA via " + ARG_DB.ArgumentText);
            string extension = Path.GetExtension(database).ToLowerInvariant();
            if (extension != @".fa" && extension != @".fasta")
                throw new NotSupportedException(@"CarafeSharp predicts libraries from a FASTA (.fa or .fasta) only: " + database);
            return InterpretLibrarySettings(database, digest, modifications, minMz, maxMz);
        }

        /// <summary>
        /// The library settings over <paramref name="database"/> (null for a host's precursor table,
        /// which digests nothing) and the prediction options as given.
        /// </summary>
        private LibrarySettings InterpretLibrarySettings(string database, DigestSettings digest, ModificationSettings modifications, double minMz, double maxMz)
        {
            if (Has(ARG_USER_VAR_MODS) || Has(ARG_MOD2MASS))
                throw new NotSupportedException(ARG_USER_VAR_MODS.ArgumentText + @" and " + ARG_MOD2MASS.ArgumentText + @" are not supported by CarafeSharp");
            if (TryGet(ARG_MODE, out string mode) && mode != @"-" && !string.Equals(mode, @"general", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(ARG_MODE.ArgumentText + @" " + mode + @" is not supported by CarafeSharp; only general");
            if (TryGet(ARG_LF_FORMAT, out string fileFormat) && string.Equals(fileFormat, @"parquet", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(ARG_LF_FORMAT.ArgumentText + @" parquet is not supported by CarafeSharp");
            CheckAiVersion();
            // Parsed here, not only after the digest, so a bad id stops the run before any work.
            // Stage 1 does not come here: its m/z filter reads ids 11 to 26 too.
            var fixedModifications = GetModifications(ARG_FIX_MOD, modifications.FixedModifications, modifications.GetFixedModifications);
            var variableModifications = GetModifications(ARG_VAR_MOD, modifications.VariableModifications, modifications.GetVariableModifications);

            digest.ConvertIToL = Has(ARG_I2L);
            var settings = new LibrarySettings
            {
                Database = database,
                Digest = digest,
                Modifications = modifications,
                MinPrecursorMz = minMz,
                MaxPrecursorMz = maxMz,
                Fast = Has(ARG_FAST),
                PredictIonMobility = Has(ARG_CCS),
            };
            if (TryGet(ARG_O, out string output))
                settings.OutputDirectory = output;
            int minCharge = TryGet(ARG_MIN_PEP_CHARGE, out string minZ) ? ParseInt(ARG_MIN_PEP_CHARGE, minZ) : LibrarySettings.DEFAULT_MIN_CHARGE;
            int maxCharge = TryGet(ARG_MAX_PEP_CHARGE, out string maxZ) ? ParseInt(ARG_MAX_PEP_CHARGE, maxZ) : LibrarySettings.DEFAULT_MAX_CHARGE;
            if (maxCharge < minCharge)
                throw new ArgumentException(string.Format(@"{0} {1} is below {2} {3}", ARG_MAX_PEP_CHARGE.ArgumentText, maxCharge, ARG_MIN_PEP_CHARGE.ArgumentText, minCharge));
            settings.Charges = Enumerable.Range(minCharge, maxCharge - minCharge + 1).ToArray();
            if (TryGet(ARG_LF_FRAG_MZ_MIN, out string fragMin))
                settings.MinFragmentMz = ParseDouble(ARG_LF_FRAG_MZ_MIN, fragMin);
            if (TryGet(ARG_LF_FRAG_MZ_MAX, out string fragMax))
                settings.MaxFragmentMz = ParseDouble(ARG_LF_FRAG_MZ_MAX, fragMax);
            if (TryGet(ARG_LF_TOP_N_FRAG, out string topN))
                settings.TopFragments = ParseInt(ARG_LF_TOP_N_FRAG, topN);
            if (TryGet(ARG_LF_MIN_N_FRAG, out string minFragments))
                settings.MinFragments = ParseInt(ARG_LF_MIN_N_FRAG, minFragments);
            if (TryGet(ARG_LF_FRAG_N_MIN, out string minNumber))
                settings.MinFragmentNumber = ParseInt(ARG_LF_FRAG_N_MIN, minNumber);
            if (TryGet(ARG_LF_TYPE, out string libraryFormat))
                settings.LibraryFormat = libraryFormat;
            if (TryGet(ARG_DECOY_PREFIX, out string decoyPrefix))
                settings.DecoyPrefix = decoyPrefix;
            if (TryGet(ARG_NCE, out string nce))
            {
                settings.Nce = ParseDouble(ARG_NCE, nce);
                settings.UserNce = true;
            }
            if (TryGet(ARG_MS_INSTRUMENT, out string instrument))
            {
                settings.Instrument = instrument;
                settings.UserInstrument = true;
            }
            if (TryGet(ARG_ACTIVATION, out string activation))
            {
                settings.Activation = ParseActivation(activation);
                settings.UserActivation = true;
            }
            if (TryGet(ARG_ANALYZER, out string analyzer))
            {
                settings.Analyzer = ParseAnalyzer(analyzer);
                settings.UserAnalyzer = true;
            }
            if (TryGet(ARG_RT_MAX, out string rtMax))
            {
                settings.RtMax = ParseDouble(ARG_RT_MAX, rtMax);
                settings.UserRtMax = true;
            }
            bool hasModelDir = TryGet(ARG_MODEL_DIR, out string modelDir);
            bool hasModelFile = TryGet(ARG_MODEL, out string modelFile);
            if (hasModelDir)
            {
                settings.ModelDirectory = modelDir;
                settings.ApplyModelDirectoryMeta = true;
            }
            if (hasModelFile)
                settings.ModelFile = modelFile;
            if (hasModelDir || hasModelFile)
            {
                // Carafe reads -tf only in its -model_dir branch.
                if (TryGet(ARG_TF, out string trainingType))
                    settings.TrainingType = trainingType;
                if (string.Equals(settings.TrainingType, @"test", StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException(ARG_TF.ArgumentText + @" test is not supported by CarafeSharp");
                if (!new[] { @"all", @"ms2", @"rt" }.Contains(settings.TrainingType, StringComparer.Ordinal))
                {
                    // -model is CarafeSharp's, so it takes only the values training does; -model_dir keeps
                    // Carafe's reading, where any other value predicts with neither fine-tuned model.
                    if (hasModelFile)
                    {
                        if (!new[] { @"all", @"ms2", @"rt" }.Contains(settings.TrainingType, StringComparer.OrdinalIgnoreCase))
                            throw new NotSupportedException(ARG_TF.ArgumentText + @" " + settings.TrainingType + @" is not supported by CarafeSharp; all, ms2 or rt");
                        settings.TrainingType = settings.TrainingType.ToLowerInvariant();
                    }
                    else
                    {
                        Warnings.Add(ARG_TF.ArgumentText + @" " + settings.TrainingType + @" is not all, ms2 or rt: as in Carafe, the library predicts with neither fine-tuned model.");
                    }
                }
                CheckIonMobilityTrainingType(settings);
            }
            if (TryGet(ARG_DEVICE, out string device))
                settings.Device = ParseDevice(device);
            if (TryGet(ARG_PAIRING_MANIFEST, out string manifest))
                settings.PairingManifest = manifest;
            settings.LibraryContract = Has(ARG_LIBRARY_CONTRACT);
            if (settings.LibraryContract)
            {
                // The contract is written into the .blib and checked against the manifest, so a
                // run without either has nothing to mark or nothing to check against.
                if (!LibraryOutputs.FromFormat(settings.LibraryFormat, settings.Fast).WritesBlib)
                    throw new ArgumentException(ARG_LIBRARY_CONTRACT.ArgumentText + @" marks a .blib as meeting the Osprey library contract, so it needs " + ARG_LF_TYPE.ArgumentText + @" blib");
                if (string.IsNullOrEmpty(settings.PairingManifest))
                    throw new ArgumentException(ARG_LIBRARY_CONTRACT.ArgumentText + @" checks the library against its pairing manifest, so it needs " + ARG_PAIRING_MANIFEST.ArgumentText);
            }
            if (TryGet(ARG_PRETRAINED, out string pretrained))
                settings.PretrainedModels = pretrained;
            if (TryGet(ARG_RT_MODEL, out string rtModel))
                settings.RtModelType = ParseRtModel(rtModel);
            if (TryGet(ARG_RT_REFERENCE, out string rtReference))
                settings.RtReference = rtReference;
            // Fails here for -lf_type mzSpecLib, before any prediction.
            var outputs = LibraryOutputs.FromFormat(settings.LibraryFormat, settings.Fast);
            // Carafe fails on the first such peptide, part way through writing the library.
            if (outputs.WritesTsv && outputs.TsvStyle != ModifiedPeptideStyle.dia_nn &&
                fixedModifications.Concat(variableModifications).Any(m => m.Type == CarafeModificationType.protein_n_term))
            {
                throw new NotSupportedException(string.Format(
                    @"{0} {1} writes a TSV in the {2} notation, which cannot write a protein N-term modification; " +
                    @"use {0} DIA-NN, or a .blib ({0} Skyline {3})", ARG_LF_TYPE.ArgumentText, settings.LibraryFormat, outputs.TsvStyle, ARG_FAST.ArgumentText));
            }
            // Carafe's TSV notations have no place for an N-terminal modification of one residue
            // (pyro-Glu); Carafe 2.2.0 fails on it at site -1.
            var nTermResidue = fixedModifications.Concat(variableModifications)
                .FirstOrDefault(m => m.Type == CarafeModificationType.peptide_n_term_residue);
            if (outputs.WritesTsv && nTermResidue != null)
            {
                throw new NotSupportedException(string.Format(
                    @"{0} {1} writes a TSV, and Carafe's TSV notations cannot write {2}; use a .blib ({0} blib, " +
                    @"or {0} Skyline {3})", ARG_LF_TYPE.ArgumentText, settings.LibraryFormat, nTermResidue.Name, ARG_FAST.ArgumentText));
            }
            return settings;
        }

        /// <summary>Carafe's Python v2 is the only one ported.</summary>
        private void CheckAiVersion()
        {
            if (TryGet(ARG_AI_VERSION, out string aiVersion) && !string.Equals(aiVersion, @"v2", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(ARG_AI_VERSION.ArgumentText + @" " + aiVersion + @" is not supported by CarafeSharp; only v2");
        }

        /// <summary>With training, <c>-model</c> is the model to fine-tune further, and the new file holds both of its models.</summary>
        private void CheckModelTrainingType()
        {
            if (TryGet(ARG_TF, out string trainingType) && !string.Equals(trainingType, @"all", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(ARG_MODEL.ArgumentText + @" with training fine-tunes both of the saved model's models further; " + ARG_TF.ArgumentText + @" " +
                                            trainingType + @" would leave the other one pretrained in the new model");
            }
        }

        /// <summary>
        /// True when <c>-i</c> names an Osprey training export or a folder of them, input Carafe
        /// could not read, so CarafeSharp trains on it without <c>-ms</c>.
        /// </summary>
        private bool NamesTrainingExports()
        {
            return TryGet(ARG_I, out string identifications) &&
                   identifications.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Any(input =>
                       TrainingExportParquet.IsExport(input) || Directory.Exists(input));
        }

        private EntrapmentFastaSettings InterpretBuild(DigestSettings digest, ModificationSettings modifications,
            double minMz, double maxMz)
        {
            if (!TryGet(ARG_DB, out string inputFasta) || inputFasta.Length == 0)
                throw new ArgumentException(ARG_BUILD_ENTRAPMENT_FASTA.ArgumentText + @" requires an input protein FASTA via " + ARG_DB.ArgumentText);
            var settings = new EntrapmentFastaSettings
            {
                InputFasta = inputFasta,
                OutputFasta = _values[ARG_BUILD_ENTRAPMENT_FASTA.Name],
                Manifest = TryGet(ARG_MANIFEST, out string manifest) ? manifest : null,
                AddEntrapment = Has(ARG_ENTRAPMENT),
                AddDecoys = !Has(ARG_NO_DECOYS),
                ApplyMzFilter = Has(ARG_MZ_FILTER),
                MinMz = minMz,
                MaxMz = maxMz,
                Digest = digest,
                Modifications = modifications,
            };
            if (TryGet(ARG_DECOY_PREFIX, out string decoyPrefix))
                settings.DecoyPrefix = decoyPrefix;
            if (settings.ApplyMzFilter && (Has(ARG_USER_VAR_MODS) || Has(ARG_MOD2MASS)))
                throw new ArgumentException(ARG_USER_VAR_MODS.ArgumentText + @" and " + ARG_MOD2MASS.ArgumentText + @" are not supported with " + ARG_MZ_FILTER.ArgumentText);

            int minCharge = TryGet(ARG_MIN_PEP_CHARGE, out string minZ) ? ParseInt(ARG_MIN_PEP_CHARGE, minZ) : DEFAULT_MIN_CHARGE;
            int maxCharge = TryGet(ARG_MAX_PEP_CHARGE, out string maxZ) ? ParseInt(ARG_MAX_PEP_CHARGE, maxZ) : DEFAULT_MAX_CHARGE;
            if (maxCharge < minCharge)
                maxCharge = minCharge;
            settings.Charges = Enumerable.Range(minCharge, maxCharge - minCharge + 1).ToArray();

            if (TryGet(ARG_ENTRAPMENT_SEED, out string entrapmentSeed))
                settings.EntrapmentSeed = ParseLong(ARG_ENTRAPMENT_SEED, entrapmentSeed);
            if (TryGet(ARG_DECOY_SEED, out string decoySeed))
                settings.DecoySeed = ParseLong(ARG_DECOY_SEED, decoySeed);
            if (TryGet(ARG_ENTRAPMENT_DB, out string entrapmentDb))
            {
                settings.EntrapmentSourceFasta = entrapmentDb;
                if (!settings.AddEntrapment)
                    throw new ArgumentException(ARG_ENTRAPMENT_DB.ArgumentText + @" has no effect without " + ARG_ENTRAPMENT.ArgumentText);
            }
            if (Has(ARG_NO_SIMILARITY_GATE))
            {
                settings.SimilarityGate = false;
                Warnings.Add(@"WARNING: " + ARG_NO_SIMILARITY_GATE.ArgumentText + @" reproduces the pre-gate behavior for audit comparison. " +
                             @"The resulting library contains entrapment near-copies of their own targets and should not be searched.");
            }
            if (Has(ARG_IGNORE_PAIRING_ERRORS))
            {
                settings.FailOnPairingViolation = false;
                Warnings.Add(@"WARNING: " + ARG_IGNORE_PAIRING_ERRORS.ArgumentText + @" will write the pairing manifest even if it fails its integrity checks. " +
                             @"A paired FDP estimator may crash or report a mis-scaled FDP on the result.");
            }
            if (TryGet(ARG_ENTRAPMENT_RATIO, out string ratioText))
            {
                // The ratio only means anything when entrapment is generated.
                if (!settings.AddEntrapment)
                    throw new ArgumentException(ARG_ENTRAPMENT_RATIO.ArgumentText + @" has no effect without " + ARG_ENTRAPMENT.ArgumentText);
                settings.EntrapmentRatio = ParseDouble(ARG_ENTRAPMENT_RATIO, ratioText);
                if (settings.EntrapmentRatio > 1.0)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                        @"{0} cannot exceed 1.0 (one entrapment peptide per target): {1}", ARG_ENTRAPMENT_RATIO.ArgumentText, settings.EntrapmentRatio));
                }
                if (settings.EntrapmentRatio < EntrapmentFastaSettings.MIN_ENTRAPMENT_RATIO)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                        @"{0} {1:F4} is below the tested minimum of {2:F2}", ARG_ENTRAPMENT_RATIO.ArgumentText, settings.EntrapmentRatio,
                        EntrapmentFastaSettings.MIN_ENTRAPMENT_RATIO));
                }
            }
            return settings;
        }

        private bool Has(CarafeArgument arg)
        {
            return _flags.Contains(arg.Name);
        }

        private bool TryGet(CarafeArgument arg, out string value)
        {
            return _values.TryGetValue(arg.Name, out value);
        }

        /// <summary>
        /// The parser's sink: what was given, by the declaration's name, for Interpret to read. A
        /// host's parser records its hosted arguments here too.
        /// </summary>
        public void Record(NameValuePair p)
        {
            _flags.Add(p.Name);
            if (p.Value != null)
                _values[p.Name] = p.Value;
        }

        /// <summary>A value a host derives, recorded as if the option had been typed.</summary>
        private void Set(CarafeArgument arg, string value)
        {
            _flags.Add(arg.Name);
            if (value != null)
                _values[arg.Name] = value;
        }

        /// <summary>
        /// The modifications of <c>-fixMod</c> or <c>-varMod</c>: an id that is not a number is
        /// an <see cref="ArgumentException"/> naming the option, and one Carafe's library
        /// prediction has no alphabase name for is not supported.
        /// </summary>
        private static IReadOnlyList<CarafeModification> GetModifications(CarafeArgument option, string ids,
            Func<IReadOnlyList<CarafeModification>> parse)
        {
            IReadOnlyList<CarafeModification> modifications;
            try
            {
                modifications = parse();
            }
            catch (Exception e) when (e is FormatException || e is OverflowException)
            {
                throw new ArgumentException(string.Format(@"Invalid {0} {1}: {2}", option.ArgumentText, ids, e.Message), e);
            }
            var unsupported = modifications.FirstOrDefault(m => m.AlphabaseName == null);
            if (unsupported != null)
            {
                throw new NotSupportedException(string.Format(@"{0}: Carafe's library generation does not support the modification {1} ({2})",
                    option.ArgumentText, unsupported.Id, unsupported.Name));
            }
            return modifications;
        }

        /// <summary>A <c>-device</c> CarafeSharp can run on: cpu, or gpu / cuda (the CPU when there is no CUDA device).</summary>
        private static string ParseDevice(string value)
        {
            if (!DEVICES.Contains(value, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException(string.Format(@"Unknown {0} {1} (expected {2})", ARG_DEVICE.ArgumentText, value, string.Join(@", ", DEVICES)));
            return value;
        }

        /// <summary><c>-rt_model</c>'s value: alphapeptdeep or chronologer (case-insensitive).</summary>
        private static RtModelType ParseRtModel(string value)
        {
            return ParseName<RtModelType>(ARG_RT_MODEL, value);
        }

        /// <summary>An option's value naming one of <typeparamref name="T"/>'s members (case-insensitive; not a number).</summary>
        private static T ParseName<T>(CarafeArgument option, string value) where T : struct, Enum
        {
            string name = Enum.GetNames(typeof(T)).FirstOrDefault(n => string.Equals(n, value, StringComparison.OrdinalIgnoreCase));
            if (name == null)
            {
                throw new ArgumentException(string.Format(@"Unknown {0} {1} (expected {2})", option.ArgumentText, value,
                    string.Join(@", ", Enum.GetNames(typeof(T)))));
            }
            return Enum.Parse<T>(name);
        }

        /// <summary>Java's <c>Integer.parseInt</c>: an optional sign and digits, nothing else.</summary>
        /// <summary><c>-activation</c>'s value, a known activation (case-insensitive).</summary>
        private static string ParseActivation(string value)
        {
            return AcquisitionVocabulary.ParseActivation(value) ?? throw new ArgumentException(string.Format(
                @"{0} {1} is not an activation CarafeSharp knows; use {2}", ARG_ACTIVATION.ArgumentText, value, string.Join(@" or ", AcquisitionVocabulary.KNOWN_ACTIVATIONS)));
        }

        /// <summary><c>-analyzer</c>'s value, a known analyzer (case-insensitive).</summary>
        private static string ParseAnalyzer(string value)
        {
            return AcquisitionVocabulary.ParseAnalyzer(value) ?? throw new ArgumentException(string.Format(
                @"{0} {1} is not an analyzer CarafeSharp knows; use {2}", ARG_ANALYZER.ArgumentText, value, string.Join(@", ", AcquisitionVocabulary.KNOWN_ANALYZERS)));
        }

        private static int ParseInt(CarafeArgument option, string value)
        {
            if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int result))
                throw new ArgumentException(string.Format(@"Invalid integer for {0}: {1}", option.ArgumentText, value));
            return result;
        }

        /// <summary>
        /// An <see cref="ARG_ENZYME"/> value: an index into the table, as Carafe takes it, or any
        /// name in it (Carafe takes NoCut). Anything else names the option and the table.
        /// </summary>
        private static int ParseEnzyme(string value)
        {
            var enzymes = EnzymeTable.All;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) &&
                index >= 0 && index < enzymes.Count)
            {
                return index;
            }
            for (int i = 0; i < enzymes.Count; i++)
            {
                if (string.Equals(enzymes[i].Name, value, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            throw new ArgumentException(string.Format(@"Invalid value for {0}: {1}. Use a number or a name from the Enzymes table.",
                ARG_ENZYME.ArgumentText, value));
        }

        private static long ParseLong(CarafeArgument option, string value)
        {
            if (!long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long result))
                throw new ArgumentException(string.Format(@"Invalid integer for {0}: {1}", option.ArgumentText, value));
            return result;
        }

        /// <summary>
        /// Java's <c>Double.parseDouble</c> for decimal text: surrounding control characters and
        /// spaces trimmed, an optional d or f suffix.
        /// </summary>
        private static double ParseDouble(CarafeArgument option, string value)
        {
            string text = JavaText.Trim(value);
            if (text.Length > 1 && @"dDfF".IndexOf(text[text.Length - 1]) >= 0 && char.IsDigit(text[text.Length - 2]))
                text = text.Substring(0, text.Length - 1);
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                throw new ArgumentException(string.Format(@"Invalid number for {0}: {1}", option.ArgumentText, value));
            return result;
        }

        /// <summary>
        /// Carafe's Python predicts no CCS for <c>-tf rt</c> or <c>ms2</c>, and its Java then fails
        /// reading the missing predictions: refused here, before any work.
        /// </summary>
        private static void CheckIonMobilityTrainingType(LibrarySettings settings)
        {
            if (settings.PredictIonMobility &&
                (string.Equals(settings.TrainingType, @"rt", StringComparison.Ordinal) ||
                 string.Equals(settings.TrainingType, @"ms2", StringComparison.Ordinal)))
            {
                throw new NotSupportedException(ARG_CCS.ArgumentText + @" with " + ARG_TF.ArgumentText + @" " + settings.TrainingType +
                                                @": Carafe predicts no ion mobility for that training type; use " + ARG_TF.ArgumentText + @" all");
            }
        }

        // --- Help rendering (generated from the declarations; cannot drift) ---------------

        /// <summary>
        /// Writes the generated usage to <paramref name="writer"/> (default stdout).
        /// <paramref name="formatType"/>: null/"unicode" = unicode tables (default), "ascii" =
        /// lower-128 ascii tables, "sections" = section names, "html" = HTML, anything else =
        /// a section filter.
        /// </summary>
        public static void PrintUsage(string formatType, TextWriter writer = null)
        {
            writer = writer ?? Console.Out;
            if (string.Equals(formatType, @"html", StringComparison.OrdinalIgnoreCase))
                writer.Write(GenerateUsageHtml());
            else
                writer.Write(BuildUsage(formatType));
        }

        public static string BuildUsage(string formatType)
        {
            var sb = new StringBuilder();
            if (string.Equals(formatType, @"sections", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var group in UsageBlocks.OfType<CarafeArgumentGroup>())
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
                var group = UsageBlocks.OfType<CarafeArgumentGroup>().FirstOrDefault(
                    g => g.Title.IndexOf(formatType, StringComparison.OrdinalIgnoreCase) >= 0);
                if (group == null)
                    return string.Format(@"No help section matching '{0}' found. Use {1} to list available sections.",
                        formatType, ARG_HELP.ArgumentText + @" sections") + Environment.NewLine;
                return group.ToString(USAGE_WIDTH, ArgUsage.FORMAT_NO_BORDERS);
            }

            foreach (var block in UsageBlocks)
                sb.Append(block.ToString(USAGE_WIDTH, renderFormat));
            return sb.ToString();
        }

        public static string GenerateUsageHtml()
        {
            const string title = @"CarafeSharp command-line usage";
            var sb = new StringBuilder();
            sb.AppendLine(@"<html><head>");
            sb.AppendLine(@"<meta charset=""utf-8"">");
            sb.AppendLine(@"<title>" + WebUtility.HtmlEncode(title) + @"</title>");
            sb.AppendLine(@"<meta name=""description"" content=""" + WebUtility.HtmlEncode(
                @"Command-line usage for CarafeSharp, the C# port of Carafe that predicts and fine-tunes spectral libraries for Osprey: " +
                @"the entrapment FASTA build, manifest reconciliation, library prediction and training, with every Carafe 2.2.0 option.") + @""">");
            // The stylesheet Osprey's generated help uses, so the two pages look alike.
            sb.AppendLine(@"<style>");
            sb.AppendLine(@"body { font: .875em/1.35 'Segoe UI','Lucida Grande',Verdana,Arial,Helvetica,sans-serif; max-width: 70em; }");
            sb.AppendLine(@"h1 { font-size: 1.6em; font-weight: 600; color: #000; margin: 0 0 .3em; }");
            sb.AppendLine(@".RowType { font-size: 1.769em; line-height: 1.3em; font-family: 'Segoe UI Semibold','Segoe UI','Lucida Grande',Verdana,Arial,Helvetica,sans-serif; color: #000; }");
            sb.AppendLine(@"table { border: 1px solid #bbb; border-collapse: collapse; margin-top: 20px; margin-bottom: 20px; }");
            sb.AppendLine(@"th { background-color: #ededed; color: #636363; text-align: left; padding: 10px 8px; font-weight: bold; border: 1px solid #bbb; }");
            sb.AppendLine(@"td { color: #2a2a2a; vertical-align: top; padding: 10px 8px; border: 1px solid #bbb; }");
            sb.AppendLine(@"code { font-family: Consolas,'Courier New',monospace; }");
            sb.AppendLine(@"</style>");
            sb.AppendLine(@"</head><body>");
            sb.AppendLine(@"<h1>" + WebUtility.HtmlEncode(title) + @"</h1>");
            foreach (var block in UsageBlocks)
                sb.Append(block.ToHtmlString());
            sb.Append(@"</body></html>");
            return sb.ToString();
        }

        /// <summary>
        /// The <see cref="CarafeCommandArgUsage"/> key holding an argument's usage text:
        /// <c>lf_type</c> -&gt; <c>_lf_type</c>, as Skyline's and Osprey's keys are derived.
        /// </summary>
        internal static string UsageKey(string argName)
        {
            return @"_" + argName.Replace('-', '_');
        }

        private static CarafeArgument Value(string name, string valueExample)
        {
            return new CarafeArgument(name, () => valueExample, (c, p) => c.Record(p));
        }

        private static CarafeArgument Choice(string name, params string[] values)
        {
            return new CarafeArgument(name, values, (c, p) => c.Record(p));
        }

        private static CarafeArgument Flag(string name)
        {
            return new CarafeArgument(name, (c, p) => c.Record(p));
        }

        /// <summary>
        /// Description + header provider for CarafeSharp: argument descriptions from
        /// <see cref="CarafeCommandArgUsage"/>, one shared line for a compatibility option with
        /// no text of its own, and the parser's error messages. English only, as the rest of
        /// CarafeSharp's text is for now.
        /// </summary>
        private class CarafeArgUsageProvider : IArgUsageProvider
        {
            public string GetDescription(string argName)
            {
                return GetUsageText(argName);
            }

            public string AppliesToHeader { get { return @"Applies To"; } }
            public string ArgumentHeader { get { return @"Argument"; } }
            public string DescriptionHeader { get { return @"Description"; } }

            public string ValueMissingMessage(string argText) { return argText + @" requires a value."; }
            public string ValueUnexpectedMessage(string argText) { return argText + @" does not take a value."; }
            public string ValueInvalidMessage(string argText, string value, string[] argValues)
            {
                return argValues == null
                    ? string.Format(@"Invalid value '{0}' for {1}", value, argText)
                    : string.Format(@"Invalid value '{0}' for {1} (expected {2})", value, argText, string.Join(@", ", argValues));
            }
            public string ValueInvalidBoolMessage(string argText, string value) { return ValueInvalidMessage(argText, value, null); }
            public string ValueInvalidIntMessage(string argText, string value) { return ValueInvalidMessage(argText, value, null); }
            public string ValueOutOfRangeIntMessage(string argText, int value, int minVal, int maxVal) { return string.Format(@"Value {0} for {1} out of range ({2}-{3})", value, argText, minVal, maxVal); }
            public string ValueInvalidDoubleMessage(string argText, string value) { return ValueInvalidMessage(argText, value, null); }
            public string ValueOutOfRangeDoubleMessage(string argText, double value, double minVal, double maxVal) { return string.Format(@"Value {0} for {1} out of range ({2}-{3})", value, argText, minVal, maxVal); }
            public string ValueInvalidDateMessage(string argText, string value) { return ValueInvalidMessage(argText, value, null); }
            public string ValueInvalidPathMessage(string argText, string value) { return ValueInvalidMessage(argText, value, null); }
            // Carafe's own wording for an option it does not know.
            public string UnknownArgumentMessage(string argText) { return @"Unrecognized option: " + argText; }
        }
    }
}
