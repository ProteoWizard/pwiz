/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Serialization
{
    public abstract class DocumentSerializer
    {
        public SrmSettings Settings
        {
            get; protected set;
        }

        public DocumentFormat DocumentFormat { get; protected set; }

        public static class EL
        {
            // v0.1 lists
            // ReSharper disable LocalizableElement
            public const string selected_proteins = "selected_proteins";
            public const string selected_peptides = "selected_peptides";
            public const string selected_transitions = "selected_transitions";

            public const string protein = "protein";
            public const string note = "note";
            public const string annotation = "annotation";
            public const string alternatives = "alternatives";
            public const string alternative_protein = "alternative_protein";
            public const string sequence = "sequence";
            public const string peptide_list = "peptide_list";
            public const string peptide = "peptide";
            public const string explicit_modifications = "explicit_modifications";
            public const string explicit_static_modifications = "explicit_static_modifications";
            public const string explicit_heavy_modifications = "explicit_heavy_modifications";
            public const string explicit_modification = "explicit_modification";
            public const string variable_modifications = "variable_modifications";
            public const string variable_modification = "variable_modification";
            public const string implicit_modifications = "implicit_modifications";
            public const string implicit_modification = "implicit_modification";
            public const string implicit_static_modifications = "implicit_static_modifications";
            public const string implicit_heavy_modifications = "implicit_heavy_modifications";
            public const string lookup_modifications = "lookup_modifications";
            public const string losses = "losses";
            public const string neutral_loss = "neutral_loss";
            public const string peptide_results = "peptide_results";
            public const string peptide_result = "peptide_result";
            public const string precursor = "precursor";
            public const string precursor_results = "precursor_results";
            public const string precursor_peak = "precursor_peak";
            public const string transition = "transition";
            public const string transition_results = "transition_results";
            public const string transition_peak = "transition_peak";
            public const string transition_lib_info = "transition_lib_info";
            public const string precursor_mz = "precursor_mz";
            public const string product_mz = "product_mz";
            public const string collision_energy = "collision_energy";
            public const string declustering_potential = "declustering_potential";
            public const string start_rt = "start_rt";
            public const string stop_rt = "stop_rt";
            public const string molecule = "molecule";
            public const string transition_data = "transition_data";
            public const string results_data = "results_data";
            // ReSharper restore LocalizableElement
        }

        public static class ATTR
        {
            // ReSharper disable LocalizableElement
            public const string format_version = "format_version";
            public const string software_version = "software_version";
            public const string name = "name";
            public const string category = "category";
            public const string description = "description";
            public const string label_name = "label_name";  
            public const string label_description = "label_description";  
            public const string accession = "accession";
            public const string gene = "gene";
            public const string species = "species";
            public const string websearch_status = "websearch_status";
            public const string preferred_name = "preferred_name";
            public const string peptide_list = "peptide_list";
            public const string start = "start";
            public const string end = "end";
            public const string sequence = "sequence";
            public const string prev_aa = "prev_aa";
            public const string next_aa = "next_aa";
            public const string index_aa = "index_aa";
            public const string modification_name = "modification_name";
            public const string mass_diff = "mass_diff";
            public const string loss_index = "loss_index";
            public const string calc_neutral_pep_mass = "calc_neutral_pep_mass";
            public const string num_missed_cleavages = "num_missed_cleavages";
            public const string rt_calculator_score = "rt_calculator_score";
            public const string predicted_retention_time = "predicted_retention_time";
            public const string explicit_retention_time = "explicit_retention_time";
            public const string explicit_retention_time_window = "explicit_retention_time_window";
            public const string explicit_drift_time_msec = "explicit_drift_time_msec"; // obsolete, replaced by the more general exolicit_ion_mobility_*
            public const string explicit_drift_time_high_energy_offset_msec = "explicit_drift_time_high_energy_offset_msec"; // obsolete, replaced by the more general exolicit_ion_mobility_*
            public const string explicit_ion_mobility = "explicit_ion_mobility";
            public const string explicit_ion_mobility_units = "explicit_ion_mobility_units";
            public const string explicit_ion_mobility_high_energy_offset = "explicit_ion_mobility_high_energy_offset";
            public const string explicit_ccs_sqa = "explicit_ccs_sqa";
            public const string drift_time_ms1 = "drift_time_ms1"; // Obsolete, replaced by ion_mobility_*
            public const string drift_time_fragment = "drift_time_fragment";  // Obsolete, replaced by ion_mobility_*
            public const string drift_time = "drift_time";  // Obsolete, replaced by ion_mobility_*
            public const string drift_time_window = "drift_time_window";  // Obsolete, replaced by ion_mobility_*
            public const string ion_mobility_ms1 = "ion_mobility_ms1";
            public const string ion_mobility_fragment = "ion_mobility_fragment"; 
            public const string ion_mobility = "ion_mobility"; 
            public const string ion_mobility_type = "ion_mobility_type"; 
            public const string ion_mobility_window = "ion_mobility_window"; 
            public const string ccs = "ccs";
            public const string avg_measured_retention_time = "avg_measured_retention_time";
            public const string isotope_label = "isotope_label";
            public const string fragment_type = "fragment_type";
            public const string fragment_ordinal = "fragment_ordinal";
            public const string mass_index = "mass_index";
            public const string calc_neutral_mass = "calc_neutral_mass";
            public const string precursor_mz = "precursor_mz";
            public const string charge = "charge";
            public const string precursor_charge = "precursor_charge";   // backward compatibility with v0.1
            public const string product_charge = "product_charge";
            public const string rank = "rank";
            public const string rank_by_level = "rank_by_level";
            public const string intensity = "intensity";
            public const string auto_manage_children = "auto_manage_children";
            public const string decoy = "decoy";
            public const string decoy_mass_shift = "decoy_mass_shift";
            public const string isotope_dist_rank = "isotope_dist_rank";
            public const string isotope_dist_proportion = "isotope_dist_proportion";
            public const string ion_formula = "ion_formula";
            public const string custom_ion_name = "custom_ion_name";
            public const string mass_monoisotopic = "mass_monoisotopic";
            public const string mass_average = "mass_average";
            public const string modified_sequence = "modified_sequence";
            public const string lookup_sequence = "lookup_sequence";
            public const string cleavage_aa = "cleavage_aa";
            public const string loss_neutral_mass = "loss_neutral_mass";
            public const string collision_energy = "collision_energy";
            public const string explicit_collision_energy = "explicit_collision_energy";
            public const string explicit_s_lens = "explicit_s_lens";
            public const string s_lens_obsolete = "s_lens"; // Really should have been explicit_s_lens
            public const string explicit_cone_voltage = "explicit_cone_voltage";
            public const string cone_voltage_obsolete = "cone_voltage"; // Really should have been explicit_cone_voltage
            public const string declustering_potential = "declustering_potential";
            public const string explicit_declustering_potential = "explicit_declustering_potential";
            public const string explicit_compensation_voltage = "explicit_compensation_voltage";
            public const string standard_type = "standard_type";
            public const string measured_ion_name = "measured_ion_name";
            public const string concentration_multiplier = "concentration_multiplier";
            public const string internal_standard_concentration = "internal_standard_concentration";
            public const string normalization_method = "normalization_method";
            public const string quantitative = "quantitative";
            public const string precursor_concentration = "precursor_concentration";
            public const string attribute_group_id = "attribute_group_id";

            // Results
            public const string replicate = "replicate";
            public const string file = "file";
            public const string step = "step";
            public const string mass_error_ppm = "mass_error_ppm";
            public const string retention_time = "retention_time";
            public const string start_time = "start_time";
            public const string end_time = "end_time";
            public const string area = "area";
            public const string background = "background";
            public const string height = "height";
            public const string fwhm = "fwhm";
            public const string fwhm_degenerate = "fwhm_degenerate";
            public const string truncated = "truncated";
            public const string identified = "identified";
            public const string user_set = "user_set";
            public const string peak_count_ratio = "peak_count_ratio";
            public const string library_dotp = "library_dotp";
            public const string isotope_dotp = "isotope_dotp";
            public const string qvalue = "qvalue";
            public const string zscore = "zscore";
            public const string exclude_from_calibration = "exclude_from_calibration";
            public const string points_across = "points_across";

            public const string forced_integration = "forced_integration";
            // ReSharper restore LocalizableElement
        }
    }
}
