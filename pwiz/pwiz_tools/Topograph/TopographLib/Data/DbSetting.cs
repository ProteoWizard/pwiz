/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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

namespace pwiz.Topograph.Data
{
    public class DbSetting : DbEntity<DbSetting>
    {
        public virtual DbWorkspace Workspace { get; set; }
        public virtual string Name { get; set; }
        public virtual string Value { get; set; }
    }

    // ReSharper disable InconsistentNaming
    public enum SettingEnum
    {
        min_tracer_count,
        exclude_aas,
        mass_accuracy,
        default_peptide_quantity,
        data_directory,
        err_on_side_of_lower_abundance,
        protein_description_key,
        max_isotope_retention_time_shift,
        min_correlation_coeff,
        min_deconvolution_score_for_avg_precursor_pool,
        accept_samples_without_ms2_id,
        accept_min_deconvolution_score,
        accept_integration_notes,
        accept_min_auc,
        accept_min_turnover_score,
        chrom_time_around_ms2_id,
        extra_chrom_time_without_ms2_id,
    }
    // ReSharper restore InconsistentNaming

}
