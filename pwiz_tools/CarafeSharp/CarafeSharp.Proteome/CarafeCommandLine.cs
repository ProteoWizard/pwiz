/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using pwiz.CarafeSharp.Core;

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
    /// Parses a Carafe command line for the modes CarafeSharp ports (the stage-1 FASTA modes and
    /// library prediction from <c>-db</c>), so CarafeSharp accepts exactly what Carafe's GUI
    /// passes. Options are Carafe's single-dash names, taking a value as the next
    /// token (or after '='), unless that token is itself an option; a repeated option keeps its
    /// first value; an unknown option, or a flag given a value with '=', is an error. Every
    /// Carafe option is recognized, and the ones these modes do not read are ignored, as Carafe
    /// ignores them.
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

        /// <summary>
        /// The file suffix of Osprey's training export (CarafeSharp.IO's
        /// <c>OspreyTrainingExport.FILE_SUFFIX</c>, which this project does not reference).
        /// </summary>
        public const string TRAINING_EXPORT_SUFFIX = @".training.parquet";

        /// <summary>Carafe's options: name to whether it takes a value.</summary>
        private static readonly Dictionary<string, bool> OPTIONS = BuildOptionTable();

        /// <summary>The <c>-device</c> values libtorch can be asked for.</summary>
        private static readonly string[] DEVICES = { @"cpu", @"gpu", @"cuda" };

        private readonly Dictionary<string, string> _values;
        private readonly HashSet<string> _flags;

        private CarafeCommandLine(Dictionary<string, string> values, HashSet<string> flags)
        {
            _values = values;
            _flags = flags;
        }

        /// <summary>
        /// Parses and interprets <paramref name="args"/>; throws <see cref="ArgumentException"/>
        /// wherever Carafe would stop with an error.
        /// </summary>
        public static CarafeCommandLine Parse(IReadOnlyList<string> args)
        {
            var values = new Dictionary<string, string>();
            var flags = new HashSet<string>();
            for (int i = 0; i < args.Count; i++)
            {
                if (!TryGetOptionName(args[i], out string name, out string inlineValue))
                {
                    if (args[i].StartsWith(@"-", StringComparison.Ordinal) && args[i].Length > 1)
                        throw new ArgumentException(@"Unrecognized option: " + args[i]);
                    // A stray argument, which Carafe also ignores.
                    continue;
                }
                flags.Add(name);
                if (!OPTIONS[name])
                {
                    // Carafe's option parser does not read "-flag=value" as the flag.
                    if (inlineValue != null)
                        throw new ArgumentException(@"Unrecognized option: " + args[i]);
                    continue;
                }
                string value = inlineValue;
                if (value == null)
                {
                    if (i + 1 >= args.Count || TryGetOptionName(args[i + 1], out _, out _))
                        throw new ArgumentException(@"Missing argument for option: " + name);
                    value = args[++i];
                }
                if (!values.ContainsKey(name))
                    values.Add(name, value);
            }
            var commandLine = new CarafeCommandLine(values, flags);
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

        /// <summary>A usage summary of the options these modes read.</summary>
        public static string Usage
        {
            get
            {
                return string.Join(Environment.NewLine,
                    @"Usage: CarafeSharp -build_entrapment_fasta <peptides.fasta> -db <proteins.fasta> [options]",
                    @"       CarafeSharp -reconcile_manifest <out.tsv> -manifest <in.tsv> -predicted_library <library.tsv|.blib>",
                    @"       CarafeSharp -db <peptides.fasta|proteins.fasta> -o <folder> [options]   (library prediction)",
                    @"       CarafeSharp -i <osprey.blib> -ms <runs> -o <folder> [-db <fasta>] [options]",
                    @"       CarafeSharp -i <x.training.parquet|folder> [-ms <runs>] -o <folder> [-db <fasta>] [options]",
                    @"                   (fine-tuning on Osprey's --training-export, then the library from -db;",
                    @"                   -i with a blib and no -ms is library prediction, as in Carafe)",
                    @"Build options (Carafe's): -manifest <tsv> -entrapment -no_decoys -decoy_prefix <p> -mz_filter",
                    @"  -min_pep_mz <mz> -max_pep_mz <mz> -min_pep_charge <z> -max_pep_charge <z> -entrapment_seed <n>",
                    @"  -entrapment_db <fasta> -entrapment_ratio <r> -no_similarity_gate -ignore_pairing_errors",
                    @"  -enzyme <index|NoCut> -miss_c <n> -minLength <n> -maxLength <n> -clip_n_m",
                    @"  -fixMod <ids> -varMod <ids> -maxVar <n>",
                    @"Library options (Carafe's): -lf_type DIA-NN|EncyclopeDIA|Skyline|blib -fast -decoy_prefix <p>",
                    @"  -min_pep_mz <mz> -max_pep_mz <mz> -min_pep_charge <z> -max_pep_charge <z> -I2L",
                    @"  -lf_frag_mz_min <mz> -lf_frag_mz_max <mz> -lf_top_n_frag <n> -lf_min_n_frag <n> -lf_frag_n_min <n>",
                    @"  -nce <nce> -ms_instrument <name> -rt_max <min> -model_dir <folder> -tf all|ms2|rt",
                    @"  -device cpu|gpu -pairing_manifest <tsv> -ccs (timsTOF ion mobility, 1/K0); CarafeSharp only:",
                    @"  -pretrained <pretrained_models.zip>,",
                    @"  -model <file.carafemodel> (a saved fine-tuned model, which every training run writes into -o),",
                    @"  -activation beam-CID|reCID -analyzer Orbitrap|LIT|ToF (else the training run's),",
                    @"  -rt_model alphapeptdeep|chronologer (default: the RT model of -model or -model_dir, else chronologer;",
                    @"  one that differs from their fine-tuned RT model replaces it with its pretrained model, Chronologer's in iRT),",
                    @"  -rt_reference <run> (with a model trained with -rt_align kde: the library in that training run's minutes,",
                    @"  else the model's; iRT for a model of runs on different gradients)",
                    @"Saved models: CarafeSharp -model_info <file.carafemodel> (what the model was trained on)",
                    @"Training options (Carafe's): -se Osprey -fdr <q> -cor <r> -n_ion_min <n> -c_ion_min <n> -lf_frag_n_min <n>",
                    @"  -nf <n> -min_n <n> -valid -no_masking -tf all|ms2|rt -seed <n> -nce <nce> -ms_instrument <name>",
                    @"  -rt_max <min> -ms2_model <model> -device cpu|gpu; CarafeSharp only: -pretrained <pretrained_models.zip>,",
                    @"  -activation beam-CID|reCID -analyzer Orbitrap|LIT|ToF (else each run's, from Osprey's export),",
                    @"  -model <file.carafemodel> (fine-tune a saved model further, instead of the pretrained models),",
                    @"  -rt_model alphapeptdeep|chronologer (the RT model to fine-tune, default: -model's, else chronologer;",
                    @"  with -tf ms2 the pretrained one the saved model predicts with),",
                    @"  -rt_align kde|none (default kde: each run's minutes mapped onto Chronologer's hydrophobic index first, by a KDE",
                    @"  ridge on 1000 peptide forms or more, else a LOESS or a line; runs on one gradient train a model of their",
                    @"  aligned minutes, runs on different gradients a Chronologer of the hydrophobic index; none: Carafe's one rt_max),",
                    @"  -rt_select best|median (aligned: a form's best run, or the runs' median)");
            }
        }

        private void Interpret(int argCount)
        {
            if (Has(@"h") || argCount == 0)
            {
                Mode = CarafeCommandMode.help;
                return;
            }
            if (Has(@"printPTM"))
                throw new ArgumentException(@"-printPTM is not supported by CarafeSharp");

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
                CheckIonMobilityTrainingType(settings);
            }
            if (TryGet(@"device", out string device))
                settings.Device = ParseDevice(device);
            if (TryGet(@"pairing_manifest", out string manifest))
                settings.PairingManifest = manifest;
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
                       input.EndsWith(TRAINING_EXPORT_SUFFIX, StringComparison.OrdinalIgnoreCase) || Directory.Exists(input));
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

        /// <summary>A known option token, "-name" or "-name=value".</summary>
        private static bool TryGetOptionName(string token, out string name, out string inlineValue)
        {
            name = null;
            inlineValue = null;
            if (token.Length < 2 || token[0] != '-')
                return false;
            string body = token.Substring(1);
            int equals = body.IndexOf('=');
            string candidate = equals < 0 ? body : body.Substring(0, equals);
            if (!OPTIONS.ContainsKey(candidate))
                return false;
            name = candidate;
            if (equals >= 0)
                inlineValue = body.Substring(equals + 1);
            return true;
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

        private static Dictionary<string, bool> BuildOptionTable()
        {
            // Carafe 2.2.0 (origin/main) AIGear options, plus CarafeSharp's -pretrained; true when
            // the option takes a value.
            const string withValue = @"i ms fixMod varMod maxVar db o pairing_manifest itol itolu sg nf na fdr cor " +
                                     @"ptm_site_prob ptm_site_qvalue min_mz min_n enzyme decoy_prefix miss_c minLength " +
                                     @"maxLength min_pep_mz max_pep_mz min_pep_charge max_pep_charge lf_type lf_format " +
                                     @"lf_frag_mz_min lf_frag_mz_max lf_top_n_frag lf_min_n_frag lf_frag_n_min rf_rt_win " +
                                     @"rt_win_offset rt_max data_type build_entrapment_fasta manifest entrapment_db " +
                                     @"entrapment_ratio entrapment_seed decoy_seed reconcile_manifest predicted_library " +
                                     @"build_koina_library koina_url koina_ms2_model koina_rt_model nce_ms n_ion_min " +
                                     @"c_ion_min nce ms_instrument device se mode tf seed python mod2mass user_var_mods " +
                                     @"model_dir ms2_model verbose ai_version pretrained model model_info activation analyzer rt_model rt_align " +
                                     @"rt_select rt_reference";
            const string flagsOnly = @"printPTM nm cs ez skyline valid use_all_peaks I2L clip_n_m rf xic export_mgf " +
                                     @"no_masking no_similarity_gate ignore_pairing_errors entrapment no_decoys mz_filter " +
                                     @"y1 fast ccs torch_compile h";
            var table = new Dictionary<string, bool>();
            foreach (string name in withValue.Split(' '))
                table.Add(name, true);
            foreach (string name in flagsOnly.Split(' '))
                table.Add(name, false);
            return table;
        }
    }
}
