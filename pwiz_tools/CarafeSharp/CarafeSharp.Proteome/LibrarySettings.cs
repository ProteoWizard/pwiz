/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.ai.AIGear and
 *   main.java.input.CParameter (library-generation fields and their defaults)
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

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// What a Carafe library-generation run (<c>-db</c> without <c>-ms</c>) predicts and writes.
    /// Defaults are Carafe's code defaults, which are not all its help text's: precursor
    /// charges 2 to 4, precursor m/z 300 to 2000, fragment m/z 200 to 1800, missed cleavages 2,
    /// decoy prefix <c>rev_</c>, NCE 27 and instrument Eclipse, device gpu (falling back to
    /// the CPU).
    /// </summary>
    public sealed class LibrarySettings
    {
        public const int DEFAULT_MIN_CHARGE = 2;
        public const int DEFAULT_MAX_CHARGE = 4;
        public const double DEFAULT_MIN_FRAGMENT_MZ = 200.0;
        public const double DEFAULT_MAX_FRAGMENT_MZ = 1800.0;
        public const int DEFAULT_TOP_FRAGMENTS = 20;
        public const int DEFAULT_MIN_FRAGMENTS = 2;
        public const int DEFAULT_MIN_FRAGMENT_NUMBER = 2;
        public const double DEFAULT_NCE = 27.0;
        public const string DEFAULT_INSTRUMENT = @"Eclipse";
        public const string DEFAULT_DECOY_PREFIX = @"rev_";
        public const string DEFAULT_LIBRARY_FORMAT = @"DIA-NN";
        public const string DEFAULT_TRAINING_TYPE = @"all";
        public const string DEFAULT_DEVICE = @"gpu";
        public const string DEFAULT_OUTPUT_DIRECTORY = @"./";

        /// <summary>Carafe's <c>n_peptides_per_batch</c>: peptidoforms per prediction batch.</summary>
        public const int DEFAULT_PEPTIDES_PER_BATCH = 200000;

        /// <summary>The FASTA to predict (<c>-db</c>): proteins, or peptides under NoCut.</summary>
        public string Database { get; set; }

        /// <summary><c>-o</c>; also where Carafe looks for models when no <c>-model_dir</c> is given.</summary>
        public string OutputDirectory { get; set; } = DEFAULT_OUTPUT_DIRECTORY;

        public DigestSettings Digest { get; set; } = new DigestSettings();

        public ModificationSettings Modifications { get; set; } = new ModificationSettings();

        /// <summary><c>-min_pep_mz</c>, inclusive.</summary>
        public double MinPrecursorMz { get; set; } = CarafeCommandLine.DEFAULT_MIN_PEPTIDE_MZ;

        /// <summary><c>-max_pep_mz</c>, inclusive.</summary>
        public double MaxPrecursorMz { get; set; } = CarafeCommandLine.DEFAULT_MAX_PEPTIDE_MZ;

        /// <summary><c>-min_pep_charge</c> to <c>-max_pep_charge</c>.</summary>
        public int[] Charges { get; set; } = { 2, 3, 4 };

        /// <summary><c>-lf_frag_mz_min</c>, inclusive.</summary>
        public double MinFragmentMz { get; set; } = DEFAULT_MIN_FRAGMENT_MZ;

        /// <summary><c>-lf_frag_mz_max</c>, inclusive.</summary>
        public double MaxFragmentMz { get; set; } = DEFAULT_MAX_FRAGMENT_MZ;

        /// <summary><c>-lf_top_n_frag</c>.</summary>
        public int TopFragments { get; set; } = DEFAULT_TOP_FRAGMENTS;

        /// <summary><c>-lf_min_n_frag</c>: precursors with fewer fragments are left out.</summary>
        public int MinFragments { get; set; } = DEFAULT_MIN_FRAGMENTS;

        /// <summary><c>-lf_frag_n_min</c>: the smallest b or y ion number kept.</summary>
        public int MinFragmentNumber { get; set; } = DEFAULT_MIN_FRAGMENT_NUMBER;

        /// <summary><c>-lf_type</c>.</summary>
        public string LibraryFormat { get; set; } = DEFAULT_LIBRARY_FORMAT;

        /// <summary><c>-decoy_prefix</c>: marks decoy proteins, for the TSV Decoy column without <c>-fast</c>.</summary>
        public string DecoyPrefix { get; set; } = DEFAULT_DECOY_PREFIX;

        /// <summary>
        /// <c>-fast</c>: Carafe's parquet path, which the GUI always uses. It writes 0 in every
        /// TSV Decoy cell and is the only path that writes a Skyline .blib.
        /// </summary>
        public bool Fast { get; set; }

        /// <summary><c>-nce</c>.</summary>
        public double Nce { get; set; } = DEFAULT_NCE;

        /// <summary>The instrument name predicted for: <c>-ms_instrument</c>, else Carafe's default.</summary>
        public string Instrument { get; set; } = DEFAULT_INSTRUMENT;

        /// <summary>True when <c>-ms_instrument</c> was given, which a model folder's meta.json does not override.</summary>
        public bool UserInstrument { get; set; }

        /// <summary>
        /// <c>-rt_max</c>, the training gradient length: above 0 the library RT is
        /// <c>rt_max * rt_pred</c> in minutes, else iRT.
        /// </summary>
        public double RtMax { get; set; }

        /// <summary>
        /// The Carafe model folder to predict with: <c>-model_dir</c>. Without it Carafe looks in
        /// the output folder, so fine-tuned models found there are used either way.
        /// </summary>
        public string ModelDirectory { get; set; }

        /// <summary>
        /// Apply the model folder's meta.json as Carafe's <c>-model_dir</c> branch does
        /// (<see cref="CarafeModelDirectory.ApplyModelDirectoryOverrides"/>); true for
        /// <c>-model_dir</c>.
        /// </summary>
        public bool ApplyModelDirectoryMeta { get; set; }

        /// <summary>
        /// Apply the state a training run leaves for the library predicted right after it
        /// (<see cref="CarafeModelDirectory.ApplyTrainingRunOverrides"/>); true when the library
        /// follows training in the same run.
        /// </summary>
        public bool ApplyTrainingRunMeta { get; set; }

        /// <summary>
        /// Predict with the model folder's safetensors models when a Carafe checkpoint is beside
        /// them: set for the library a training run predicts, so it uses the models that run
        /// wrote, not checkpoints an earlier Carafe run left in the same folder. Without it a
        /// folder's Carafe checkpoints come first, as <c>-model_dir</c> reads them.
        /// </summary>
        public bool PreferSafetensors { get; set; }

        /// <summary><c>-tf</c>, read only with <c>-model_dir</c>: which of its models to use.</summary>
        public string TrainingType { get; set; } = DEFAULT_TRAINING_TYPE;

        /// <summary><c>-device</c>: cpu or gpu.</summary>
        public string Device { get; set; } = DEFAULT_DEVICE;

        /// <summary><c>-pairing_manifest</c>: adds a DecoyPairs table to a .blib.</summary>
        public string PairingManifest { get; set; }

        /// <summary><c>-pretrained</c> (CarafeSharp only): the AlphaPeptDeep pretrained_models.zip.</summary>
        public string PretrainedModels { get; set; }

        /// <summary>Peptidoforms per prediction batch.</summary>
        public int PeptidesPerBatch { get; set; } = DEFAULT_PEPTIDES_PER_BATCH;
    }
}
