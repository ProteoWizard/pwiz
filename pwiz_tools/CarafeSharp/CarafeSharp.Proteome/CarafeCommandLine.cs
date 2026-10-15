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
    /// Carafe's single-dash names (<c>-db</c>) accepted alongside the standard <c>--db</c> that
    /// the help shows, as Osprey's does, a value as the next token or after '=', a value ended
    /// only by a declared option (so <c>-seed -5</c> and <c>-mode -</c> are values), a repeated
    /// option keeping its first value, an unknown option or a flag given a value an error, and
    /// a stray word ignored. Every Carafe 2.2.0 option is declared; the ones these modes do not
    /// read are accepted and ignored, as Carafe ignores them. The help is generated from the
    /// declarations (<see cref="BuildUsage"/>), so it cannot drift from the parser.
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
        /// Carafe's single dash accepted for every option, a value as the next token, a prefixed
        /// token a value unless it names a declared option, the first of a repeated option
        /// winning, and listed values left to the settings, which refuse a bad one with
        /// Carafe's own message.
        /// </summary>
        private static readonly CommandLineGrammar GRAMMAR = new CommandLineGrammar
        {
            AcceptedPrefixes = new[] { ArgumentBase.ARG_PREFIX, @"-" },
            ShortPrefix = @"-",
            ValueMayFollow = true,
            PrefixedTokenEndsValue = false,
            FirstValueWins = true,
            ValidatesListedValues = false,
        };

        /// <summary>The <c>-device</c> values libtorch can be asked for.</summary>
        private static readonly string[] DEVICES = { @"cpu", @"gpu", @"cuda" };

        // Install the grammar and the text the help renderer and the parser's errors need
        // before any argument text is built.
        static CarafeCommandLine()
        {
            ArgUsage.Grammar = GRAMMAR;
            ArgUsage.Provider = new CarafeArgUsageProvider();
            // Carafe's grammar is space-separated (--name value), so the help renders
            // "--name <value>", and a token built from an instance (ARG_DB + "x.fasta") is too.
            ArgUsage.ArgumentValueSeparator = @" ";
            // The settings read numbers as Java does, in the invariant culture.
            ArgUsage.ValueFormatProvider = CultureInfo.InvariantCulture;
        }

        // --- Modes and files ---------------------------------------------------------------
        // Carafe's names, one declaration each; the parser records what was given (Record), and
        // Interpret below reads the records as it always has.
        public static readonly CarafeArgument ARG_BUILD_ENTRAPMENT_FASTA = Value(@"build_entrapment_fasta", @"<out.fasta>");
        public static readonly CarafeArgument ARG_RECONCILE_MANIFEST = Value(@"reconcile_manifest", @"<out.tsv>");
        public static readonly CarafeArgument ARG_MANIFEST = Value(@"manifest", @"<manifest.tsv>");
        public static readonly CarafeArgument ARG_PREDICTED_LIBRARY = Value(@"predicted_library", @"<library.tsv|.blib>");
        public static readonly CarafeArgument ARG_MODEL_INFO = Value(@"model_info", @"<file" + CarafeModelFile.EXTENSION + @">");
        public static readonly CarafeArgument ARG_DB = Value(@"db", @"<proteins.fasta|peptides.fasta>");
        public static readonly CarafeArgument ARG_I = Value(@"i", @"<osprey.blib|run" + TrainingExportParquet.EXT + @"|folder>");
        public static readonly CarafeArgument ARG_MS = Value(@"ms", @"<run1,run2,...>");
        public static readonly CarafeArgument ARG_O = Value(@"o", @"<folder>");
        // -h is Carafe's and Osprey's; -help and --help are the same declaration. The optional
        // value picks the help format (ascii | unicode | sections | html | <Section>).
        public static readonly CarafeArgument ARG_HELP = new CarafeArgument(@"help", () => @"<format|Section>", (c, p) => c.Record(p))
            { ShortName = @"h", OptionalValue = true };

        private static readonly CarafeArgumentGroup GROUP_MODES = new CarafeArgumentGroup(() => @"Modes and files", true,
            ARG_BUILD_ENTRAPMENT_FASTA, ARG_RECONCILE_MANIFEST, ARG_MANIFEST, ARG_PREDICTED_LIBRARY, ARG_MODEL_INFO,
            ARG_DB, ARG_I, ARG_MS, ARG_O, ARG_HELP);

        // --- Digest and modifications ------------------------------------------------------
        public static readonly CarafeArgument ARG_ENZYME = Value(@"enzyme", @"<index|" + EnzymeTable.NO_CUT_ENZYME_NAME + @">");
        public static readonly CarafeArgument ARG_MISS_C = Value(@"miss_c", @"<n>");
        public static readonly CarafeArgument ARG_MIN_LENGTH = Value(@"minLength", @"<n>");
        public static readonly CarafeArgument ARG_MAX_LENGTH = Value(@"maxLength", @"<n>");
        public static readonly CarafeArgument ARG_CLIP_N_M = Flag(@"clip_n_m");
        public static readonly CarafeArgument ARG_FIX_MOD = Value(@"fixMod", @"<ids>");
        public static readonly CarafeArgument ARG_VAR_MOD = Value(@"varMod", @"<ids>");
        public static readonly CarafeArgument ARG_MAX_VAR = Value(@"maxVar", @"<n>");
        public static readonly CarafeArgument ARG_MIN_PEP_MZ = Value(@"min_pep_mz", @"<mz>");
        public static readonly CarafeArgument ARG_MAX_PEP_MZ = Value(@"max_pep_mz", @"<mz>");
        public static readonly CarafeArgument ARG_MIN_PEP_CHARGE = Value(@"min_pep_charge", @"<z>");
        public static readonly CarafeArgument ARG_MAX_PEP_CHARGE = Value(@"max_pep_charge", @"<z>");
        public static readonly CarafeArgument ARG_I2L = Flag(@"I2L");

        private static readonly CarafeArgumentGroup GROUP_DIGEST = new CarafeArgumentGroup(() => @"Digest and modifications", true,
            ARG_ENZYME, ARG_MISS_C, ARG_MIN_LENGTH, ARG_MAX_LENGTH, ARG_CLIP_N_M, ARG_FIX_MOD, ARG_VAR_MOD, ARG_MAX_VAR,
            ARG_MIN_PEP_MZ, ARG_MAX_PEP_MZ, ARG_MIN_PEP_CHARGE, ARG_MAX_PEP_CHARGE, ARG_I2L);

        // --- Entrapment FASTA build --------------------------------------------------------
        public static readonly CarafeArgument ARG_ENTRAPMENT = Flag(@"entrapment");
        public static readonly CarafeArgument ARG_NO_DECOYS = Flag(@"no_decoys");
        public static readonly CarafeArgument ARG_DECOY_PREFIX = Value(@"decoy_prefix", @"<prefix>");
        public static readonly CarafeArgument ARG_MZ_FILTER = Flag(@"mz_filter");
        public static readonly CarafeArgument ARG_ENTRAPMENT_SEED = Value(@"entrapment_seed", @"<n>");
        public static readonly CarafeArgument ARG_DECOY_SEED = Value(@"decoy_seed", @"<n>");
        public static readonly CarafeArgument ARG_ENTRAPMENT_DB = Value(@"entrapment_db", @"<proteins.fasta>");
        public static readonly CarafeArgument ARG_ENTRAPMENT_RATIO = Value(@"entrapment_ratio", @"<ratio>");
        public static readonly CarafeArgument ARG_NO_SIMILARITY_GATE = Flag(@"no_similarity_gate");
        public static readonly CarafeArgument ARG_IGNORE_PAIRING_ERRORS = Flag(@"ignore_pairing_errors");

        private static readonly CarafeArgumentGroup GROUP_ENTRAPMENT = new CarafeArgumentGroup(() => @"Entrapment FASTA build", true,
            ARG_ENTRAPMENT, ARG_NO_DECOYS, ARG_DECOY_PREFIX, ARG_MZ_FILTER, ARG_ENTRAPMENT_SEED, ARG_DECOY_SEED,
            ARG_ENTRAPMENT_DB, ARG_ENTRAPMENT_RATIO, ARG_NO_SIMILARITY_GATE, ARG_IGNORE_PAIRING_ERRORS);

        // --- Library prediction ------------------------------------------------------------
        public static readonly CarafeArgument ARG_LF_TYPE = Value(@"lf_type", @"<DIA-NN|EncyclopeDIA|Skyline|blib>");
        public static readonly CarafeArgument ARG_FAST = Flag(@"fast");
        public static readonly CarafeArgument ARG_LF_FRAG_MZ_MIN = Value(@"lf_frag_mz_min", @"<mz>");
        public static readonly CarafeArgument ARG_LF_FRAG_MZ_MAX = Value(@"lf_frag_mz_max", @"<mz>");
        public static readonly CarafeArgument ARG_LF_TOP_N_FRAG = Value(@"lf_top_n_frag", @"<n>");
        public static readonly CarafeArgument ARG_LF_MIN_N_FRAG = Value(@"lf_min_n_frag", @"<n>");
        public static readonly CarafeArgument ARG_LF_FRAG_N_MIN = Value(@"lf_frag_n_min", @"<n>");
        public static readonly CarafeArgument ARG_NCE = Value(@"nce", @"<nce>");
        public static readonly CarafeArgument ARG_MS_INSTRUMENT = Value(@"ms_instrument", @"<name>");
        public static readonly CarafeArgument ARG_ACTIVATION = Choice(@"activation", AcquisitionVocabulary.KNOWN_ACTIVATIONS.ToArray());
        public static readonly CarafeArgument ARG_ANALYZER = Choice(@"analyzer", AcquisitionVocabulary.KNOWN_ANALYZERS.ToArray());
        public static readonly CarafeArgument ARG_RT_MAX = Value(@"rt_max", @"<minutes>");
        public static readonly CarafeArgument ARG_CCS = Flag(@"ccs");
        public static readonly CarafeArgument ARG_PAIRING_MANIFEST = Value(@"pairing_manifest", @"<manifest.tsv>");
        public static readonly CarafeArgument ARG_LIBRARY_CONTRACT = Flag(@"library_contract");
        public static readonly CarafeArgument ARG_RT_REFERENCE = Value(@"rt_reference", @"<run>");

        private static readonly CarafeArgumentGroup GROUP_LIBRARY = new CarafeArgumentGroup(() => @"Library prediction", true,
            ARG_LF_TYPE, ARG_FAST, ARG_LF_FRAG_MZ_MIN, ARG_LF_FRAG_MZ_MAX, ARG_LF_TOP_N_FRAG, ARG_LF_MIN_N_FRAG, ARG_LF_FRAG_N_MIN,
            ARG_NCE, ARG_MS_INSTRUMENT, ARG_ACTIVATION, ARG_ANALYZER, ARG_RT_MAX, ARG_CCS, ARG_PAIRING_MANIFEST, ARG_LIBRARY_CONTRACT,
            ARG_RT_REFERENCE);

        // --- Models ------------------------------------------------------------------------
        public static readonly CarafeArgument ARG_MODEL_DIR = Value(@"model_dir", @"<folder>");
        public static readonly CarafeArgument ARG_MODEL = Value(@"model", @"<file" + CarafeModelFile.EXTENSION + @">");
        public static readonly CarafeArgument ARG_PRETRAINED = Value(@"pretrained", @"<pretrained_models.zip>");
        public static readonly CarafeArgument ARG_TF = Choice(@"tf", @"all", @"ms2", @"rt");
        public static readonly CarafeArgument ARG_RT_MODEL = Choice(@"rt_model", Enum.GetNames(typeof(RtModelType)));
        public static readonly CarafeArgument ARG_DEVICE = Choice(@"device", DEVICES);
        public static readonly CarafeArgument ARG_MS2_MODEL = Value(@"ms2_model", @"<model>");

        private static readonly CarafeArgumentGroup GROUP_MODELS = new CarafeArgumentGroup(() => @"Models", true,
            ARG_MODEL_DIR, ARG_MODEL, ARG_PRETRAINED, ARG_TF, ARG_RT_MODEL, ARG_DEVICE, ARG_MS2_MODEL);

        // --- Training ----------------------------------------------------------------------
        public static readonly CarafeArgument ARG_SE = Value(@"se", @"<Osprey>");
        public static readonly CarafeArgument ARG_FDR = Value(@"fdr", @"<q>");
        public static readonly CarafeArgument ARG_COR = Value(@"cor", @"<r>");
        public static readonly CarafeArgument ARG_N_ION_MIN = Value(@"n_ion_min", @"<n>");
        public static readonly CarafeArgument ARG_C_ION_MIN = Value(@"c_ion_min", @"<n>");
        public static readonly CarafeArgument ARG_NF = Value(@"nf", @"<n>");
        public static readonly CarafeArgument ARG_MIN_N = Value(@"min_n", @"<n>");
        public static readonly CarafeArgument ARG_VALID = Flag(@"valid");
        public static readonly CarafeArgument ARG_NO_MASKING = Flag(@"no_masking");
        public static readonly CarafeArgument ARG_SEED = Value(@"seed", @"<n>");
        public static readonly CarafeArgument ARG_RT_ALIGN = Choice(@"rt_align", Enum.GetNames(typeof(RtAlignmentType)));
        public static readonly CarafeArgument ARG_RT_SELECT = Choice(@"rt_select", Enum.GetNames(typeof(RtSelectionType)));

        private static readonly CarafeArgumentGroup GROUP_TRAINING = new CarafeArgumentGroup(() => @"Training", true,
            ARG_SE, ARG_FDR, ARG_COR, ARG_N_ION_MIN, ARG_C_ION_MIN, ARG_NF, ARG_MIN_N, ARG_VALID, ARG_NO_MASKING, ARG_SEED,
            ARG_RT_ALIGN, ARG_RT_SELECT);

        // --- Output (CarafeSharp's, as Osprey's --log-file / --timestamp / --memstamp) --------
        public static readonly CarafeArgument ARG_LOG_FILE = Value(@"log_file", @"<path>");
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
        public static readonly CarafeArgument ARG_PTM_SITE_PROB = Value(@"ptm_site_prob", @"<p>");
        public static readonly CarafeArgument ARG_PTM_SITE_QVALUE = Value(@"ptm_site_qvalue", @"<q>");
        public static readonly CarafeArgument ARG_MIN_MZ = Value(@"min_mz", @"<mz>");
        public static readonly CarafeArgument ARG_LF_FORMAT = Value(@"lf_format", @"<format>");
        public static readonly CarafeArgument ARG_RF_RT_WIN = Value(@"rf_rt_win", @"<window>");
        public static readonly CarafeArgument ARG_RT_WIN_OFFSET = Value(@"rt_win_offset", @"<minutes>");
        public static readonly CarafeArgument ARG_DATA_TYPE = Value(@"data_type", @"<type>");
        public static readonly CarafeArgument ARG_BUILD_KOINA_LIBRARY = Value(@"build_koina_library", @"<out>");
        public static readonly CarafeArgument ARG_KOINA_URL = Value(@"koina_url", @"<url>");
        public static readonly CarafeArgument ARG_KOINA_MS2_MODEL = Value(@"koina_ms2_model", @"<name>");
        public static readonly CarafeArgument ARG_KOINA_RT_MODEL = Value(@"koina_rt_model", @"<name>");
        public static readonly CarafeArgument ARG_NCE_MS = Value(@"nce_ms", @"<nce>");
        public static readonly CarafeArgument ARG_MODE = Value(@"mode", @"<mode>");
        public static readonly CarafeArgument ARG_PYTHON = Value(@"python", @"<path>");
        public static readonly CarafeArgument ARG_MOD2MASS = Value(@"mod2mass", @"<mapping>");
        public static readonly CarafeArgument ARG_USER_VAR_MODS = Value(@"user_var_mods", @"<mods>");
        public static readonly CarafeArgument ARG_VERBOSE = Value(@"verbose", @"<level>");
        public static readonly CarafeArgument ARG_AI_VERSION = Value(@"ai_version", @"<version>");
        public static readonly CarafeArgument ARG_PRINT_PTM = Flag(@"printPTM");
        public static readonly CarafeArgument ARG_NM = Flag(@"nm");
        public static readonly CarafeArgument ARG_CS = Flag(@"cs");
        public static readonly CarafeArgument ARG_EZ = Flag(@"ez");
        public static readonly CarafeArgument ARG_SKYLINE = Flag(@"skyline");
        public static readonly CarafeArgument ARG_USE_ALL_PEAKS = Flag(@"use_all_peaks");
        public static readonly CarafeArgument ARG_RF = Flag(@"rf");
        public static readonly CarafeArgument ARG_XIC = Flag(@"xic");
        public static readonly CarafeArgument ARG_EXPORT_MGF = Flag(@"export_mgf");
        public static readonly CarafeArgument ARG_Y1 = Flag(@"y1");
        public static readonly CarafeArgument ARG_TORCH_COMPILE = Flag(@"torch_compile");

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
                return new IUsageBlock[]
                {
                    new ParaUsageBlock(@"CarafeSharp - spectral library prediction and fine-tuning for Osprey (C# port of Carafe)"),
                    new ParaUsageBlock(@"USAGE: CarafeSharp --build_entrapment_fasta <out.fasta> --db <proteins.fasta> [options]"),
                    new ParaUsageBlock(@"       CarafeSharp --reconcile_manifest <out.tsv> --manifest <in.tsv> --predicted_library <library.tsv|.blib>"),
                    new ParaUsageBlock(@"       CarafeSharp --db <peptides.fasta|proteins.fasta> -o <folder> [options]   (library prediction)"),
                    new ParaUsageBlock(@"       CarafeSharp -i <osprey.blib> --ms <runs> -o <folder> [--db <fasta>] [options]   (training)"),
                    new ParaUsageBlock(@"       CarafeSharp -i <run" + TrainingExportParquet.EXT + @"|folder> [--ms <runs>] -o <folder> [--db <fasta>] [options]"),
                    new ParaUsageBlock(@"       CarafeSharp --model_info <file" + CarafeModelFile.EXTENSION + @">"),
                    GROUP_MODES,
                    GROUP_DIGEST,
                    GROUP_ENTRAPMENT,
                    GROUP_LIBRARY,
                    GROUP_MODELS,
                    GROUP_TRAINING,
                    GROUP_OUTPUT,
                    GROUP_COMPATIBILITY,
                    new ParaUsageBlock(@"OTHER FORMS ACCEPTED: a value may follow its option (--db x.fasta) or be joined to it " +
                                       @"(--db=x.fasta), and every option is also accepted with Carafe's single dash (-db x.fasta, " +
                                       @"-db=x.fasta), so a Carafe 2.2.0 command line runs unchanged. A repeated option keeps its " +
                                       @"first value, as in Carafe."),
                };
            }
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
            if (Has(ARG_HELP.Name) || argCount == 0)
            {
                Mode = CarafeCommandMode.help;
                HelpFormat = TryGet(ARG_HELP.Name, out string helpFormat) ? helpFormat : null;
                return;
            }
            if (Has(@"printPTM"))
                throw new ArgumentException(@"-printPTM is not supported by CarafeSharp");
            // Output options, read in every mode (CarafeSharp's, as Osprey's --log-file / --timestamp / --memstamp).
            if (TryGet(@"log_file", out string logFile))
                LogFile = logFile;
            TimeStamp = Has(@"timestamp");
            MemStamp = Has(@"memstamp");

            var digest = new DigestSettings { ClipNTermMethionine = Has(@"clip_n_m") };
            var modifications = new ModificationSettings();
            if (TryGet(@"fixMod", out string fixMod))
                modifications.FixedModifications = fixMod;
            if (TryGet(@"varMod", out string varMod))
                modifications.VariableModifications = varMod;
            if (TryGet(@"miss_c", out string missedCleavages))
                digest.MaxMissedCleavages = ParseInt(@"miss_c", missedCleavages);
            if (TryGet(@"enzyme", out string enzyme))
            {
                digest.EnzymeIndex = string.Equals(enzyme, EnzymeTable.NO_CUT_ENZYME_NAME, StringComparison.OrdinalIgnoreCase)
                    ? EnzymeTable.GetIndexByName(enzyme)
                    : ParseInt(@"enzyme", enzyme);
            }
            // Carafe validates the index here, through its non-specific check.
            if (EnzymeTable.IsNonSpecific(EnzymeTable.GetByIndex(digest.EnzymeIndex)))
                digest.MaxMissedCleavages = EnzymeTable.NON_SPECIFIC_MISSED_CLEAVAGES;
            if (TryGet(@"maxVar", out string maxVar))
                modifications.MaxVariableModifications = ParseInt(@"maxVar", maxVar);
            if (TryGet(@"minLength", out string minLength))
                digest.MinLength = ParseInt(@"minLength", minLength);
            if (TryGet(@"maxLength", out string maxLength))
                digest.MaxLength = ParseInt(@"maxLength", maxLength);
            double minMz = TryGet(@"min_pep_mz", out string minMzText) ? ParseDouble(@"min_pep_mz", minMzText) : DEFAULT_MIN_PEPTIDE_MZ;
            double maxMz = TryGet(@"max_pep_mz", out string maxMzText) ? ParseDouble(@"max_pep_mz", maxMzText) : DEFAULT_MAX_PEPTIDE_MZ;

            if (Has(@"build_entrapment_fasta"))
            {
                Mode = CarafeCommandMode.build_entrapment_fasta;
                BuildSettings = InterpretBuild(digest, modifications, minMz, maxMz);
                return;
            }
            if (TryGet(@"model_info", out string modelInfo))
            {
                Mode = CarafeCommandMode.model_info;
                ModelInfoPath = modelInfo;
                return;
            }
            if (Has(@"reconcile_manifest"))
            {
                Mode = CarafeCommandMode.reconcile_manifest;
                ReconcileManifestOut = _values[@"reconcile_manifest"];
                if (!TryGet(@"manifest", out string manifestIn) || manifestIn.Length == 0)
                    throw new ArgumentException(@"-reconcile_manifest requires the input manifest via -manifest");
                if (!TryGet(@"predicted_library", out string library) || library.Length == 0)
                    throw new ArgumentException(@"-reconcile_manifest requires the predicted library TSV via -predicted_library");
                ReconcileManifestIn = manifestIn;
                ReconcileLibrary = library;
                return;
            }
            if (Has(@"build_koina_library"))
                throw new NotSupportedException(@"-build_koina_library is not supported by CarafeSharp");
            if (Has(@"model"))
            {
                if (Has(@"model_dir"))
                    throw new ArgumentException(@"-model and -model_dir both name the models to predict with; give one");
                if (Has(@"ms2_model"))
                    throw new ArgumentException(@"-model and -ms2_model both name the MS2 model to start from; give one");
                bool training = Has(@"ms") || NamesTrainingExports();
                // With training, -model is the model to fine-tune further, and the new file holds both of its models.
                if (training && TryGet(@"tf", out string trainingType) && !string.Equals(trainingType, @"all", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(@"-model with training fine-tunes both of the saved model's models further; -tf " +
                                                trainingType + @" would leave the other one pretrained in the new model");
                }
                if (!training && !Has(@"db"))
                    throw new ArgumentException(@"-model predicts a library, which needs the FASTA to predict via -db");
            }
            // Carafe trains when -ms is given. CarafeSharp also trains on Osprey's training exports
            // named by -i directly, which Carafe could never have read; -i alone is otherwise
            // ignored, as Carafe ignores it.
            if ((Has(@"ms") || NamesTrainingExports()) && !Has(@"model_dir"))
            {
                Mode = CarafeCommandMode.train;
                TrainingSettings = InterpretTraining(digest, modifications, minMz, maxMz);
                return;
            }
            if (Has(@"db"))
            {
                Mode = CarafeCommandMode.predict_library;
                LibrarySettings = InterpretLibrary(digest, modifications, minMz, maxMz);
                var trainingOnly = new[] { @"rt_align", @"rt_select" }.Where(Has).ToArray();
                if (trainingOnly.Length > 0)
                {
                    Warnings.Add(@"Ignored " + string.Join(@" ", trainingOnly.Select(o => @"-" + o)) +
                                 @": they align training runs, and this run only predicts a library.");
                }
                return;
            }
            throw new ArgumentException(@"CarafeSharp supports -build_entrapment_fasta, -reconcile_manifest, library prediction from -db " +
                                        @"and training from -i with -ms (or -i naming Osprey's training exports)");
        }

        /// <summary>
        /// Carafe's training mode (<c>-ms</c>), reading Osprey's training exports: CarafeSharp
        /// reads no spectra, so <c>-ms</c> only names the runs, and the options that shape
        /// Carafe's own XIC extraction are Osprey's to decide.
        /// </summary>
        private TrainingSettings InterpretTraining(DigestSettings digest, ModificationSettings modifications, double minMz, double maxMz)
        {
            if (!TryGet(@"i", out string identifications) || identifications.Length == 0)
                throw new ArgumentException(@"Training requires Osprey's results via -i (its blib, or its .training.parquet exports)");
            if (TryGet(@"se", out string searchEngine) && !string.Equals(searchEngine, @"Osprey", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(@"-se " + searchEngine + @" is not supported by CarafeSharp, which trains on Osprey's training export (-se Osprey)");
            if (TryGet(@"mode", out string mode) && mode != @"-" && !string.Equals(mode, @"general", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(@"-mode " + mode + @" is not supported by CarafeSharp; only general");
            CheckAiVersion();
            if (Has(@"user_var_mods") || Has(@"mod2mass"))
                throw new NotSupportedException(@"-user_var_mods and -mod2mass are not supported by CarafeSharp");
            foreach (string option in new[] { @"cs", @"y1", @"use_all_peaks" })
            {
                if (Has(option))
                    throw new NotSupportedException(@"-" + option + @" is not supported by CarafeSharp training");
            }
            if (Has(@"ccs"))
            {
                // Carafe does the same with Osprey's results, which carry no ion mobility to train on.
                Warnings.Add(Has(@"db")
                    ? @"-ccs: the CCS model is not fine-tuned (Osprey's results carry no ion mobility); the library's 1/K0 is predicted " +
                      @"with the pretrained CCS model, or a Carafe ccs_model.pt already in -o."
                    : @"Ignored -ccs: the CCS model is not fine-tuned, and without -db no library is predicted.");
            }
            if (Has(@"rt_reference") && !Has(@"db"))
                Warnings.Add(@"Ignored -rt_reference: it names the training run whose minutes a library takes, and without -db no library is predicted.");
            if (TryGet(@"na", out string flanking) && ParseInt(@"na", flanking) != 0)
                throw new NotSupportedException(@"-na (flanking spectra) is not supported by CarafeSharp training");
            var fromOsprey = new[] { @"itol", @"itolu", @"rf", @"rf_rt_win", @"rt_win_offset", @"sg", @"min_mz" }.Where(Has).ToArray();
            if (fromOsprey.Length > 0)
            {
                Warnings.Add(@"Ignored " + string.Join(@" ", fromOsprey.Select(o => @"-" + o)) +
                             @": the fragment matches, XICs and peak boundaries come from Osprey's training export.");
            }
            var notWritten = new[] { @"ez", @"xic", @"export_mgf", @"skyline" }.Where(Has).ToArray();
            if (notWritten.Length > 0)
                Warnings.Add(@"Ignored " + string.Join(@" ", notWritten.Select(o => @"-" + o)) + @": CarafeSharp does not write those diagnostic files.");

            var settings = new TrainingSettings
            {
                Identifications = identifications,
                MsFiles = TryGet(@"ms", out string msFiles)
                    ? msFiles.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    : Array.Empty<string>(),
                RequireTopIonValid = Has(@"valid"),
                NoMasking = Has(@"no_masking"),
            };
            if (TryGet(@"o", out string output))
                settings.OutputDirectory = output;
            if (TryGet(@"fdr", out string fdr))
                settings.Fdr = ParseDouble(@"fdr", fdr);
            if (TryGet(@"cor", out string correlation))
                settings.MinCorrelation = ParseDouble(@"cor", correlation);
            if (TryGet(@"n_ion_min", out string nIonMin))
                settings.LowOrdinalB = ParseInt(@"n_ion_min", nIonMin);
            if (TryGet(@"c_ion_min", out string cIonMin))
                settings.LowOrdinalY = ParseInt(@"c_ion_min", cIonMin);
            if (TryGet(@"lf_frag_n_min", out string minOrdinal))
                settings.MinFragmentOrdinal = ParseInt(@"lf_frag_n_min", minOrdinal);
            if (TryGet(@"nf", out string minMatched))
                settings.MinMatchedIons = ParseInt(@"nf", minMatched);
            if (TryGet(@"min_n", out string minValid))
                settings.MinValidIons = ParseInt(@"min_n", minValid);
            if (TryGet(@"tf", out string trainingType))
            {
                if (!new[] { @"all", @"ms2", @"rt" }.Contains(trainingType, StringComparer.OrdinalIgnoreCase))
                    throw new NotSupportedException(@"-tf " + trainingType + @" is not supported by CarafeSharp; all, ms2 or rt");
                settings.TrainingType = trainingType.ToLowerInvariant();
            }
            if (TryGet(@"seed", out string seed))
            {
                // Carafe's Integer.parseInt, and numpy's seed must not be negative.
                int value = ParseInt(@"seed", seed);
                if (value < 0)
                    throw new ArgumentException(@"-seed must not be negative: " + seed);
                settings.Seed = (uint)value;
            }
            if (TryGet(@"device", out string device))
                settings.Device = ParseDevice(device);
            if (TryGet(@"nce", out string nce))
                settings.Nce = ParseDouble(@"nce", nce);
            if (TryGet(@"ms_instrument", out string instrument))
                settings.Instrument = instrument;
            if (TryGet(@"activation", out string activation))
                settings.Activation = ParseActivation(activation);
            if (TryGet(@"analyzer", out string analyzer))
                settings.Analyzer = ParseAnalyzer(analyzer);
            if (TryGet(@"rt_max", out string rtMax))
                settings.RtMax = ParseDouble(@"rt_max", rtMax);
            if (TryGet(@"ms2_model", out string ms2Model))
                settings.Ms2Model = ms2Model;
            if (TryGet(@"model", out string baseModel))
                settings.BaseModel = baseModel;
            if (TryGet(@"pretrained", out string pretrained))
                settings.PretrainedModels = pretrained;
            if (TryGet(@"rt_model", out string rtModel))
                settings.RtModelType = ParseRtModel(rtModel);
            if (TryGet(@"rt_align", out string rtAlign))
                settings.RtAlignment = ParseName<RtAlignmentType>(@"rt_align", rtAlign);
            if (TryGet(@"rt_select", out string rtSelect))
                settings.RtSelection = ParseName<RtSelectionType>(@"rt_select", rtSelect);
            if (Has(@"db"))
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
            string database = _values[@"db"];
            if (database.Length == 0)
                throw new ArgumentException(@"Library prediction requires a FASTA via -db");
            string extension = Path.GetExtension(database).ToLowerInvariant();
            if (extension != @".fa" && extension != @".fasta")
                throw new NotSupportedException(@"CarafeSharp predicts libraries from a FASTA (.fa or .fasta) only: " + database);
            if (Has(@"user_var_mods") || Has(@"mod2mass"))
                throw new NotSupportedException(@"-user_var_mods and -mod2mass are not supported by CarafeSharp");
            if (TryGet(@"mode", out string mode) && mode != @"-" && !string.Equals(mode, @"general", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(@"-mode " + mode + @" is not supported by CarafeSharp; only general");
            if (TryGet(@"lf_format", out string fileFormat) && string.Equals(fileFormat, @"parquet", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(@"-lf_format parquet is not supported by CarafeSharp");
            CheckAiVersion();
            // Parsed here, not only after the digest, so a bad id stops the run before any work.
            // Stage 1 does not come here: its m/z filter reads ids 11 to 26 too.
            var fixedModifications = GetModifications(@"fixMod", modifications.FixedModifications, modifications.GetFixedModifications);
            var variableModifications = GetModifications(@"varMod", modifications.VariableModifications, modifications.GetVariableModifications);

            digest.ConvertIToL = Has(@"I2L");
            var settings = new LibrarySettings
            {
                Database = database,
                Digest = digest,
                Modifications = modifications,
                MinPrecursorMz = minMz,
                MaxPrecursorMz = maxMz,
                Fast = Has(@"fast"),
                PredictIonMobility = Has(@"ccs"),
            };
            if (TryGet(@"o", out string output))
                settings.OutputDirectory = output;
            int minCharge = TryGet(@"min_pep_charge", out string minZ) ? ParseInt(@"min_pep_charge", minZ) : LibrarySettings.DEFAULT_MIN_CHARGE;
            int maxCharge = TryGet(@"max_pep_charge", out string maxZ) ? ParseInt(@"max_pep_charge", maxZ) : LibrarySettings.DEFAULT_MAX_CHARGE;
            if (maxCharge < minCharge)
                throw new ArgumentException(string.Format(@"-max_pep_charge {0} is below -min_pep_charge {1}", maxCharge, minCharge));
            settings.Charges = Enumerable.Range(minCharge, maxCharge - minCharge + 1).ToArray();
            if (TryGet(@"lf_frag_mz_min", out string fragMin))
                settings.MinFragmentMz = ParseDouble(@"lf_frag_mz_min", fragMin);
            if (TryGet(@"lf_frag_mz_max", out string fragMax))
                settings.MaxFragmentMz = ParseDouble(@"lf_frag_mz_max", fragMax);
            if (TryGet(@"lf_top_n_frag", out string topN))
                settings.TopFragments = ParseInt(@"lf_top_n_frag", topN);
            if (TryGet(@"lf_min_n_frag", out string minFragments))
                settings.MinFragments = ParseInt(@"lf_min_n_frag", minFragments);
            if (TryGet(@"lf_frag_n_min", out string minNumber))
                settings.MinFragmentNumber = ParseInt(@"lf_frag_n_min", minNumber);
            if (TryGet(@"lf_type", out string libraryFormat))
                settings.LibraryFormat = libraryFormat;
            if (TryGet(@"decoy_prefix", out string decoyPrefix))
                settings.DecoyPrefix = decoyPrefix;
            if (TryGet(@"nce", out string nce))
            {
                settings.Nce = ParseDouble(@"nce", nce);
                settings.UserNce = true;
            }
            if (TryGet(@"ms_instrument", out string instrument))
            {
                settings.Instrument = instrument;
                settings.UserInstrument = true;
            }
            if (TryGet(@"activation", out string activation))
            {
                settings.Activation = ParseActivation(activation);
                settings.UserActivation = true;
            }
            if (TryGet(@"analyzer", out string analyzer))
            {
                settings.Analyzer = ParseAnalyzer(analyzer);
                settings.UserAnalyzer = true;
            }
            if (TryGet(@"rt_max", out string rtMax))
            {
                settings.RtMax = ParseDouble(@"rt_max", rtMax);
                settings.UserRtMax = true;
            }
            bool hasModelDir = TryGet(@"model_dir", out string modelDir);
            bool hasModelFile = TryGet(@"model", out string modelFile);
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
                if (TryGet(@"tf", out string trainingType))
                    settings.TrainingType = trainingType;
                if (string.Equals(settings.TrainingType, @"test", StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException(@"-tf test is not supported by CarafeSharp");
                if (!new[] { @"all", @"ms2", @"rt" }.Contains(settings.TrainingType, StringComparer.Ordinal))
                {
                    // -model is CarafeSharp's, so it takes only the values training does; -model_dir keeps
                    // Carafe's reading, where any other value predicts with neither fine-tuned model.
                    if (hasModelFile)
                    {
                        if (!new[] { @"all", @"ms2", @"rt" }.Contains(settings.TrainingType, StringComparer.OrdinalIgnoreCase))
                            throw new NotSupportedException(@"-tf " + settings.TrainingType + @" is not supported by CarafeSharp; all, ms2 or rt");
                        settings.TrainingType = settings.TrainingType.ToLowerInvariant();
                    }
                    else
                    {
                        Warnings.Add(@"-tf " + settings.TrainingType + @" is not all, ms2 or rt: as in Carafe, the library predicts with neither fine-tuned model.");
                    }
                }
                CheckIonMobilityTrainingType(settings);
            }
            if (TryGet(@"device", out string device))
                settings.Device = ParseDevice(device);
            if (TryGet(@"pairing_manifest", out string manifest))
                settings.PairingManifest = manifest;
            settings.LibraryContract = Has(@"library_contract");
            if (settings.LibraryContract)
            {
                // The contract is written into the .blib and checked against the manifest, so a
                // run without either has nothing to mark or nothing to check against.
                if (!LibraryOutputs.FromFormat(settings.LibraryFormat, settings.Fast).WritesBlib)
                    throw new ArgumentException(@"-library_contract marks a .blib as meeting the Osprey library contract, so it needs -lf_type blib");
                if (string.IsNullOrEmpty(settings.PairingManifest))
                    throw new ArgumentException(@"-library_contract checks the library against its pairing manifest, so it needs -pairing_manifest");
            }
            if (TryGet(@"pretrained", out string pretrained))
                settings.PretrainedModels = pretrained;
            if (TryGet(@"rt_model", out string rtModel))
                settings.RtModelType = ParseRtModel(rtModel);
            if (TryGet(@"rt_reference", out string rtReference))
                settings.RtReference = rtReference;
            // Fails here for -lf_type mzSpecLib, before any prediction.
            var outputs = LibraryOutputs.FromFormat(settings.LibraryFormat, settings.Fast);
            // Carafe fails on the first such peptide, part way through writing the library.
            if (outputs.WritesTsv && outputs.TsvStyle != ModifiedPeptideStyle.dia_nn &&
                fixedModifications.Concat(variableModifications).Any(m => m.Type == CarafeModificationType.protein_n_term))
            {
                throw new NotSupportedException(string.Format(
                    @"-lf_type {0} writes a TSV in the {1} notation, which cannot write a protein N-term modification; " +
                    @"use -lf_type DIA-NN, or a .blib (-lf_type Skyline -fast)", settings.LibraryFormat, outputs.TsvStyle));
            }
            // Carafe's TSV notations have no place for an N-terminal modification of one residue
            // (pyro-Glu); Carafe 2.2.0 fails on it at site -1.
            var nTermResidue = fixedModifications.Concat(variableModifications)
                .FirstOrDefault(m => m.Type == CarafeModificationType.peptide_n_term_residue);
            if (outputs.WritesTsv && nTermResidue != null)
            {
                throw new NotSupportedException(string.Format(
                    @"-lf_type {0} writes a TSV, and Carafe's TSV notations cannot write {1}; use a .blib (-lf_type blib, " +
                    @"or -lf_type Skyline -fast)", settings.LibraryFormat, nTermResidue.Name));
            }
            return settings;
        }

        /// <summary>Carafe's Python v2 is the only one ported.</summary>
        private void CheckAiVersion()
        {
            if (TryGet(@"ai_version", out string aiVersion) && !string.Equals(aiVersion, @"v2", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(@"-ai_version " + aiVersion + @" is not supported by CarafeSharp; only v2");
        }

        /// <summary>
        /// True when <c>-i</c> names an Osprey training export or a folder of them, input Carafe
        /// could not read, so CarafeSharp trains on it without <c>-ms</c>.
        /// </summary>
        private bool NamesTrainingExports()
        {
            return TryGet(@"i", out string identifications) &&
                   identifications.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Any(input =>
                       TrainingExportParquet.IsExport(input) || Directory.Exists(input));
        }

        private EntrapmentFastaSettings InterpretBuild(DigestSettings digest, ModificationSettings modifications,
            double minMz, double maxMz)
        {
            if (!TryGet(@"db", out string inputFasta) || inputFasta.Length == 0)
                throw new ArgumentException(@"-build_entrapment_fasta requires an input protein FASTA via -db");
            var settings = new EntrapmentFastaSettings
            {
                InputFasta = inputFasta,
                OutputFasta = _values[@"build_entrapment_fasta"],
                Manifest = TryGet(@"manifest", out string manifest) ? manifest : null,
                AddEntrapment = Has(@"entrapment"),
                AddDecoys = !Has(@"no_decoys"),
                ApplyMzFilter = Has(@"mz_filter"),
                MinMz = minMz,
                MaxMz = maxMz,
                Digest = digest,
                Modifications = modifications,
            };
            if (TryGet(@"decoy_prefix", out string decoyPrefix))
                settings.DecoyPrefix = decoyPrefix;
            if (settings.ApplyMzFilter && (Has(@"user_var_mods") || Has(@"mod2mass")))
                throw new ArgumentException(@"-user_var_mods and -mod2mass are not supported with -mz_filter");

            int minCharge = TryGet(@"min_pep_charge", out string minZ) ? ParseInt(@"min_pep_charge", minZ) : DEFAULT_MIN_CHARGE;
            int maxCharge = TryGet(@"max_pep_charge", out string maxZ) ? ParseInt(@"max_pep_charge", maxZ) : DEFAULT_MAX_CHARGE;
            if (maxCharge < minCharge)
                maxCharge = minCharge;
            settings.Charges = Enumerable.Range(minCharge, maxCharge - minCharge + 1).ToArray();

            if (TryGet(@"entrapment_seed", out string entrapmentSeed))
                settings.EntrapmentSeed = ParseLong(@"entrapment_seed", entrapmentSeed);
            if (TryGet(@"decoy_seed", out string decoySeed))
                settings.DecoySeed = ParseLong(@"decoy_seed", decoySeed);
            if (TryGet(@"entrapment_db", out string entrapmentDb))
            {
                settings.EntrapmentSourceFasta = entrapmentDb;
                if (!settings.AddEntrapment)
                    throw new ArgumentException(@"-entrapment_db has no effect without -entrapment");
            }
            if (Has(@"no_similarity_gate"))
            {
                settings.SimilarityGate = false;
                Warnings.Add(@"WARNING: -no_similarity_gate reproduces the pre-gate behavior for audit comparison. " +
                             @"The resulting library contains entrapment near-copies of their own targets and should not be searched.");
            }
            if (Has(@"ignore_pairing_errors"))
            {
                settings.FailOnPairingViolation = false;
                Warnings.Add(@"WARNING: -ignore_pairing_errors will write the pairing manifest even if it fails its integrity checks. " +
                             @"A paired FDP estimator may crash or report a mis-scaled FDP on the result.");
            }
            if (TryGet(@"entrapment_ratio", out string ratioText))
            {
                // The ratio only means anything when entrapment is generated.
                if (!settings.AddEntrapment)
                    throw new ArgumentException(@"-entrapment_ratio has no effect without -entrapment");
                settings.EntrapmentRatio = ParseDouble(@"entrapment_ratio", ratioText);
                if (settings.EntrapmentRatio > 1.0)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                        @"-entrapment_ratio cannot exceed 1.0 (one entrapment peptide per target): {0}", settings.EntrapmentRatio));
                }
                if (settings.EntrapmentRatio < EntrapmentFastaSettings.MIN_ENTRAPMENT_RATIO)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                        @"-entrapment_ratio {0:F4} is below the tested minimum of {1:F2}", settings.EntrapmentRatio,
                        EntrapmentFastaSettings.MIN_ENTRAPMENT_RATIO));
                }
            }
            return settings;
        }

        private bool Has(string name)
        {
            return _flags.Contains(name);
        }

        private bool TryGet(string name, out string value)
        {
            return _values.TryGetValue(name, out value);
        }

        /// <summary>The parser's sink: what was given, by Carafe's name, for Interpret to read.</summary>
        private void Record(NameValuePair p)
        {
            _flags.Add(p.Name);
            if (p.Value != null)
                _values[p.Name] = p.Value;
        }

        /// <summary>
        /// The modifications of <c>-fixMod</c> or <c>-varMod</c>: an id that is not a number is
        /// an <see cref="ArgumentException"/> naming the option, and one Carafe's library
        /// prediction has no alphabase name for is not supported.
        /// </summary>
        private static IReadOnlyList<CarafeModification> GetModifications(string option, string ids,
            Func<IReadOnlyList<CarafeModification>> parse)
        {
            IReadOnlyList<CarafeModification> modifications;
            try
            {
                modifications = parse();
            }
            catch (Exception e) when (e is FormatException || e is OverflowException)
            {
                throw new ArgumentException(string.Format(@"Invalid -{0} {1}: {2}", option, ids, e.Message), e);
            }
            var unsupported = modifications.FirstOrDefault(m => m.AlphabaseName == null);
            if (unsupported != null)
            {
                throw new NotSupportedException(string.Format(@"-{0}: Carafe's library generation does not support the modification {1} ({2})",
                    option, unsupported.Id, unsupported.Name));
            }
            return modifications;
        }

        /// <summary>A <c>-device</c> CarafeSharp can run on: cpu, or gpu / cuda (the CPU when there is no CUDA device).</summary>
        private static string ParseDevice(string value)
        {
            if (!DEVICES.Contains(value, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException(string.Format(@"Unknown -device {0} (expected {1})", value, string.Join(@", ", DEVICES)));
            return value;
        }

        /// <summary><c>-rt_model</c>'s value: alphapeptdeep or chronologer (case-insensitive).</summary>
        private static RtModelType ParseRtModel(string value)
        {
            return ParseName<RtModelType>(@"rt_model", value);
        }

        /// <summary>An option's value naming one of <typeparamref name="T"/>'s members (case-insensitive; not a number).</summary>
        private static T ParseName<T>(string option, string value) where T : struct, Enum
        {
            string name = Enum.GetNames(typeof(T)).FirstOrDefault(n => string.Equals(n, value, StringComparison.OrdinalIgnoreCase));
            if (name == null)
            {
                throw new ArgumentException(string.Format(@"Unknown -{0} {1} (expected {2})", option, value,
                    string.Join(@", ", Enum.GetNames(typeof(T)))));
            }
            return Enum.Parse<T>(name);
        }

        /// <summary>Java's <c>Integer.parseInt</c>: an optional sign and digits, nothing else.</summary>
        /// <summary><c>-activation</c>'s value, a known activation (case-insensitive).</summary>
        private static string ParseActivation(string value)
        {
            return AcquisitionVocabulary.ParseActivation(value) ?? throw new ArgumentException(string.Format(
                @"-activation {0} is not an activation CarafeSharp knows; use {1}", value, string.Join(@" or ", AcquisitionVocabulary.KNOWN_ACTIVATIONS)));
        }

        /// <summary><c>-analyzer</c>'s value, a known analyzer (case-insensitive).</summary>
        private static string ParseAnalyzer(string value)
        {
            return AcquisitionVocabulary.ParseAnalyzer(value) ?? throw new ArgumentException(string.Format(
                @"-analyzer {0} is not an analyzer CarafeSharp knows; use {1}", value, string.Join(@", ", AcquisitionVocabulary.KNOWN_ANALYZERS)));
        }

        private static int ParseInt(string option, string value)
        {
            if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int result))
                throw new ArgumentException(string.Format(@"Invalid integer for -{0}: {1}", option, value));
            return result;
        }

        private static long ParseLong(string option, string value)
        {
            if (!long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long result))
                throw new ArgumentException(string.Format(@"Invalid integer for -{0}: {1}", option, value));
            return result;
        }

        /// <summary>
        /// Java's <c>Double.parseDouble</c> for decimal text: surrounding control characters and
        /// spaces trimmed, an optional d or f suffix.
        /// </summary>
        private static double ParseDouble(string option, string value)
        {
            string text = JavaText.Trim(value);
            if (text.Length > 1 && @"dDfF".IndexOf(text[text.Length - 1]) >= 0 && char.IsDigit(text[text.Length - 2]))
                text = text.Substring(0, text.Length - 1);
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                throw new ArgumentException(string.Format(@"Invalid number for -{0}: {1}", option, value));
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
                throw new NotSupportedException(@"-ccs with -tf " + settings.TrainingType +
                                                @": Carafe predicts no ion mobility for that training type; use -tf all");
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
                string description = CarafeCommandArgUsage.ResourceManager.GetString(UsageKey(argName));
                if (description != null)
                    return description;
                return CompatibilityArguments.Any(a => a.Name == argName)
                    ? @"Accepted for Carafe compatibility; not used by CarafeSharp."
                    : null;
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
