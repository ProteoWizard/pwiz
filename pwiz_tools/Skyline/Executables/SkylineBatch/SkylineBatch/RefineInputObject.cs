using System;
using System.Collections.Generic;

namespace SkylineBatch
{
    public class RefineInputObject : GlobalizedObject
    {
        public RefineInputObject()
        {
            _refine_cv_global_normalize = cv_global_normalize_options.none;
            _refine_cv_transitions = cv_transitions_options.none;
            _refine_cv_ms_level = cv_ms_level_options.none;
        }

        #region Variables

        // Variable names correspond to a label in CommandArgName.resx and a description in CommandArgUsage.resx
        // Category attribute resourceKeys correspond to a group name in CommandArgUsage.resx
        
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT")]
        public int _refine_min_peptides { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT")]
        public bool _refine_remove_repeats { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT")]
        public bool _refine_remove_duplicates { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT")]
        public bool _refine_missing_library { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT")]
        public int _refine_min_transitions { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT")]
        public string _refine_label_type { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT")]
        public bool _refine_add_label_type { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT")]
        public bool _refine_auto_select_peptides { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT")]
        public bool _refine_auto_select_precursors { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT")]
        public bool _refine_auto_select_transitions { get; set; }

        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public double _refine_min_peak_found_ratio { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public double _refine_max_peak_found_ratio { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public int _refine_max_peptide_peak_rank { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public int _refine_max_transition_peak_rank { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public bool _refine_max_precursor_only { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public bool _refine_prefer_larger_products { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public bool _refine_missing_results { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public double _refine_min_time_correlation { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public double _refine_min_dotp { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public double _refine_min_idotp { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public bool _refine_use_best_result { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public double _refine_cv_remove_above_cutoff { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public cv_global_normalize_options _refine_cv_global_normalize { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public string _refine_cv_reference_normalize { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public cv_transitions_options _refine_cv_transitions { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public int _refine_cv_transitions_count { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public cv_ms_level_options _refine_cv_ms_level { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public double _refine_qvalue_cutoff { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public int _refine_minimum_detections { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public double _refine_gc_p_value_cutoff { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public double _refine_gc_fold_change_cutoff { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public double _refine_gc_ms_level { get; set; }
        [LocalizedCategoryAttribute("CommandArgs_GROUP_REFINEMENT_W_RESULTS")]
        public string _refine_gc_name { get; set; }

        #endregion

        #region Options for enum variables

        public enum cv_global_normalize_options
        {
            global_standards,
            equalize_medians,
            none
        }

        public enum cv_transitions_options
        {
            all,
            best,
            none
        }

        public enum cv_ms_level_options
        {
            precursors,
            products,
            none
        }

        #endregion

        #region Generate Command Dictionary

        public List<Tuple<string, string>> AsCommandList()
        {
            var commands = new List<Tuple<string, string>>();
            AddCommand(commands, nameof(_refine_min_peptides), _refine_min_peptides);
            AddCommand(commands, nameof(_refine_remove_repeats), _refine_remove_repeats);
            AddCommand(commands, nameof(_refine_remove_duplicates), _refine_remove_duplicates);
            AddCommand(commands, nameof(_refine_missing_library), _refine_missing_library);
            AddCommand(commands, nameof(_refine_min_transitions), _refine_min_transitions);
            AddCommand(commands, nameof(_refine_label_type), _refine_label_type);
            AddCommand(commands, nameof(_refine_add_label_type), _refine_add_label_type);
            AddCommand(commands, nameof(_refine_auto_select_peptides), _refine_auto_select_peptides);
            AddCommand(commands, nameof(_refine_auto_select_precursors), _refine_auto_select_precursors);
            AddCommand(commands, nameof(_refine_auto_select_transitions), _refine_auto_select_transitions);

            AddCommand(commands, nameof(_refine_min_peak_found_ratio), _refine_min_peak_found_ratio);
            AddCommand(commands, nameof(_refine_max_peak_found_ratio), _refine_max_peak_found_ratio);
            AddCommand(commands, nameof(_refine_max_peptide_peak_rank), _refine_max_peptide_peak_rank);
            AddCommand(commands, nameof(_refine_max_transition_peak_rank), _refine_max_transition_peak_rank);
            AddCommand(commands, nameof(_refine_max_precursor_only), _refine_max_precursor_only);
            AddCommand(commands, nameof(_refine_prefer_larger_products), _refine_prefer_larger_products);
            AddCommand(commands, nameof(_refine_missing_results), _refine_missing_results);
            AddCommand(commands, nameof(_refine_min_time_correlation), _refine_min_time_correlation);
            AddCommand(commands, nameof(_refine_min_dotp), _refine_min_dotp);
            AddCommand(commands, nameof(_refine_min_idotp), _refine_min_idotp);
            AddCommand(commands, nameof(_refine_use_best_result), _refine_use_best_result);
            AddCommand(commands, nameof(_refine_cv_remove_above_cutoff), _refine_cv_remove_above_cutoff);
            AddCommand(commands, nameof(_refine_cv_reference_normalize), _refine_cv_reference_normalize);
            AddCommand(commands, nameof(_refine_cv_transitions_count), _refine_cv_transitions_count);
            AddCommand(commands, nameof(_refine_qvalue_cutoff), _refine_qvalue_cutoff);
            AddCommand(commands, nameof(_refine_minimum_detections), _refine_minimum_detections);
            AddCommand(commands, nameof(_refine_gc_p_value_cutoff), _refine_gc_p_value_cutoff);
            AddCommand(commands, nameof(_refine_gc_fold_change_cutoff), _refine_gc_fold_change_cutoff);
            AddCommand(commands, nameof(_refine_gc_ms_level), _refine_gc_ms_level);
            AddCommand(commands, nameof(_refine_gc_name), _refine_gc_name);

            AddEnumCommand(commands, nameof(_refine_cv_global_normalize), _refine_cv_global_normalize.ToString());
            AddEnumCommand(commands, nameof(_refine_cv_transitions), _refine_cv_transitions.ToString());
            AddEnumCommand(commands, nameof(_refine_cv_ms_level), _refine_cv_ms_level.ToString());
            return commands;
        }


        private void AddCommand(List<Tuple<string, string>> commands, string variableName, int input)
        {
            if (input == 0) return;
            Add(commands, variableName, input.ToString());
        }

        private void AddCommand(List<Tuple<string, string>> commands, string variableName, double input)
        {
            if (Math.Abs(input) < 0.00000000001) return;
            Add(commands, variableName, input.ToString());
        }

        private void AddCommand(List<Tuple<string, string>> commands, string variableName, string input)
        {
            if (string.IsNullOrEmpty(input)) return;
            Add(commands, variableName, input);
        }

        private void AddCommand(List<Tuple<string, string>> commands, string variableName, bool input)
        {
            if (!input) return;
            Add(commands, variableName, string.Empty);
        }

        private void AddEnumCommand(List<Tuple<string, string>> commands, string variableName, string input)
        {
            if (input.Equals("none")) return;
            Add(commands, variableName, input);
        }

        private void Add(List<Tuple<string, string>> commands, string variableName, string value)
        {
            var command = "-" + variableName.Replace('_', '-');
            commands.Add(new Tuple<string, string>(command, value));
        }

        #endregion

        
    }
}

    