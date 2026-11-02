/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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

namespace pwiz.CarafeSharp.Contracts
{
    /// <summary>
    /// The footer keys of <c>&lt;stem&gt;.training.parquet</c>, as Osprey's
    /// docs/22-training-export.md defines them: the writer (Osprey's TrainingExportWriter)
    /// stamps them and <see cref="TrainingExport"/> reads them back, through these names, so
    /// neither side can misspell one. The three stamp keys (<see cref="VERSION"/>,
    /// <see cref="SEARCH_HASH"/>, <see cref="LIBRARY_HASH"/>) are the same keys Osprey's scores
    /// parquet carries.
    /// </summary>
    public static class TrainingExportKeys
    {
        public const string FORMAT_VERSION = @"osprey.training_export.format_version";

        public const string VERSION = @"osprey.version";
        public const string SEARCH_HASH = @"osprey.search_hash";
        public const string LIBRARY_HASH = @"osprey.library_hash";
        public const string FILE_NAME = @"osprey.file_name";

        public const string ROWS = @"osprey.training_export.rows";
        public const string MAX_Q = @"osprey.training_export.max_q";
        public const string CLAIMANT_Q = @"osprey.training_export.claimant_q";
        /// <summary><c>2</c> or <c>1</c>: the pass whose run q-values the rows were selected on.</summary>
        public const string RUN_Q_PASS = @"osprey.training_export.run_q_pass";
        public const string XICS = @"osprey.training_export.xics";
        public const string SLOT_ORDER = @"osprey.training_export.slot_order";
        public const string MP_COSINE_PARITY = @"osprey.training_export.mp_cosine_parity";
        public const string MP_COSINE_PARITY_FITTED = @"osprey.training_export.mp_cosine_parity_fitted";

        public const string RT_MIN = @"osprey.rt_min";
        public const string RT_MAX = @"osprey.rt_max";
        public const string ISOLATION_MZ_MIN = @"osprey.isolation_mz_min";
        public const string ISOLATION_MZ_MAX = @"osprey.isolation_mz_max";
        /// <summary><c>lower,upper</c>: the union of every isolation window's measured m/z range, or empty.</summary>
        public const string MS2_SCAN_WINDOW = @"osprey.ms2_scan_window";
        public const string FRAGMENT_TOLERANCE = @"osprey.fragment_tolerance";
        public const string FRAGMENT_TOLERANCE_UNIT = @"osprey.fragment_tolerance_unit";
        public const string MS2_CALIBRATION_CALIBRATED = @"osprey.ms2_calibration.calibrated";
        public const string MS2_CALIBRATION_MEAN = @"osprey.ms2_calibration.mean";
        public const string MS2_CALIBRATION_SD = @"osprey.ms2_calibration.sd";
        public const string MS2_CALIBRATION_UNIT = @"osprey.ms2_calibration.unit";
        public const string DDC_TOLERANCE = @"osprey.ddc.tolerance";
        public const string DDC_TOLERANCE_UNIT = @"osprey.ddc.tolerance_unit";
        public const string DDC_RT_NEIGHBORHOOD = @"osprey.ddc.rt_neighborhood";

        public const string INSTRUMENT_VENDOR = @"osprey.instrument_vendor";
        public const string INSTRUMENT_MODEL = @"osprey.instrument_model";
        public const string SOURCE_MS2_SAMPLED = @"osprey.source_ms2_sampled";
        /// <summary>JSON histogram, dissociation method (pwiz's short name) to MS2 spectrum count.</summary>
        public const string DISSOCIATION_METHODS = @"osprey.dissociation_methods";
        /// <summary>JSON histogram, collision energy as the file reports it to MS2 spectrum count.</summary>
        public const string COLLISION_ENERGIES = @"osprey.collision_energies";
        /// <summary>JSON histogram, mass analyzer configuration (pwiz's names) to MS2 spectrum count.</summary>
        public const string MS2_MASS_ANALYZERS = @"osprey.ms2_mass_analyzers";
    }
}
