/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// A fine-tuning run (Carafe's <c>-ms</c> mode), read from Osprey's training exports
    /// instead of the raw data: which exports, the training-data rules, and the library to
    /// predict afterwards. Defaults are Carafe's code defaults; its Osprey workflow passes
    /// <c>-cor 0.8 -n_ion_min 2 -c_ion_min 2 -valid</c>.
    /// </summary>
    public sealed class TrainingSettings
    {
        public const double DEFAULT_FDR = 0.01;
        public const double DEFAULT_CORRELATION = 0.75;
        public const int DEFAULT_MIN_MATCHED_IONS = 4;
        public const int DEFAULT_MIN_VALID_IONS = 4;
        public const uint DEFAULT_SEED = 2024;

        /// <summary><c>-i</c>: Osprey's result blib, a <c>.training.parquet</c> export, or a folder of exports (comma-separated).</summary>
        public string Identifications { get; set; }

        /// <summary><c>-ms</c>: the runs to train on (files or folders, comma-separated); their exports are found by file stem.</summary>
        public IReadOnlyList<string> MsFiles { get; set; } = Array.Empty<string>();

        /// <summary><c>-o</c>: where the models, their metrics and the library go.</summary>
        public string OutputDirectory { get; set; } = LibrarySettings.DEFAULT_OUTPUT_DIRECTORY;

        /// <summary><c>-fdr</c>: the run precursor q-value a training precursor needs.</summary>
        public double Fdr { get; set; } = DEFAULT_FDR;

        /// <summary><c>-cor</c>: the elution-profile correlation a matched ion needs.</summary>
        public double MinCorrelation { get; set; } = DEFAULT_CORRELATION;

        /// <summary><c>-n_ion_min</c>: b ions up to this ordinal must be clean when intense (0 = off).</summary>
        public int LowOrdinalB { get; set; }

        /// <summary><c>-c_ion_min</c>: the same for y ions.</summary>
        public int LowOrdinalY { get; set; }

        /// <summary><c>-lf_frag_n_min</c>: ions below this ordinal are always masked.</summary>
        public int MinFragmentOrdinal { get; set; } = LibrarySettings.DEFAULT_MIN_FRAGMENT_NUMBER;

        /// <summary><c>-nf</c>: matched ions a spectrum needs.</summary>
        public int MinMatchedIons { get; set; } = DEFAULT_MIN_MATCHED_IONS;

        /// <summary><c>-min_n</c>: valid ions a spectrum needs.</summary>
        public int MinValidIons { get; set; } = DEFAULT_MIN_VALID_IONS;

        /// <summary><c>-valid</c>: a spectrum's top ion must be valid.</summary>
        public bool RequireTopIonValid { get; set; }

        /// <summary><c>-no_masking</c>: train on every ion of the kept spectra.</summary>
        public bool NoMasking { get; set; }

        /// <summary><c>-tf</c>: all, ms2 or rt.</summary>
        public string TrainingType { get; set; } = LibrarySettings.DEFAULT_TRAINING_TYPE;

        /// <summary><c>-seed</c>.</summary>
        public uint Seed { get; set; } = DEFAULT_SEED;

        /// <summary><c>-device</c>: cpu or gpu (falls back to the CPU).</summary>
        public string Device { get; set; } = LibrarySettings.DEFAULT_DEVICE;

        /// <summary>
        /// <c>-nce</c>: the collision energy of a run whose export records none, as Carafe uses
        /// it (a run's own collision energy comes first); null for Carafe's default of 27.
        /// </summary>
        public double? Nce { get; set; }

        /// <summary><c>-ms_instrument</c>, or null to take each run's instrument model by Carafe's name for it.</summary>
        public string Instrument { get; set; }

        /// <summary><c>-rt_max</c>, a floor on the RT normalizer (0 = none).</summary>
        public double RtMax { get; set; }

        /// <summary>
        /// <c>-ms2_model</c>: the MS2 model (a Carafe checkpoint or CarafeSharp safetensors) to
        /// fine-tune instead of the pretrained one, and the baseline the fine-tuned model must
        /// beat; null for the pretrained model.
        /// </summary>
        public string Ms2Model { get; set; }

        /// <summary>CarafeSharp's <c>-pretrained</c> models zip, or null for the default.</summary>
        public string PretrainedModels { get; set; }

        /// <summary>The library to predict with the fine-tuned models (<c>-db</c>), or null.</summary>
        public LibrarySettings Library { get; set; }

        public bool TrainMs2
        {
            get { return IsType(@"all") || IsType(@"ms2"); }
        }

        public bool TrainRt
        {
            get { return IsType(@"all") || IsType(@"rt"); }
        }

        private bool IsType(string type)
        {
            return string.Equals(TrainingType, type, StringComparison.OrdinalIgnoreCase);
        }
    }
}
