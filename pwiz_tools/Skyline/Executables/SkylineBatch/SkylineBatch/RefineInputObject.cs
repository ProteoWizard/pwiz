using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SkylineBatch
{
    public class RefineInputObject : GlobalizedObject
    {
        // prefix for all refine labels in CommandArgName.resx and refine descriptions in CommandArgUsage.resx
        public static readonly string REFINE_RESOURCE_KEY_PREFIX = "_refine_";

        public RefineInputObject()
        {
            InitializeEnumVariables();
        }

        public RefineInputObject(ImmutableList<Tuple<RefineVariable, string>> initialValues)
        {
            InitializeEnumVariables();
            ReadCommandList(initialValues);
        }

        private void InitializeEnumVariables()
        {
            // Default for enum variables is none (command not used)
            cv_global_normalize = CvGlobalNormalizeValues.none;
            cv_transitions = CvTransitionsValues.none;
            cv_ms_level = CvMsLevelValues.none;
        }

        #region Variables

        // Variable names correspond to values in resource files and batch command names:
        //      Displayed name = CommandArgName.(REFINE_RESOURCE_KEY_PREFIX + variableName)
        //      Displayed description = CommandArgUsage.(REFINE_RESOURCE_KEY_PREFIX + variableName)
        //      Batch command = "-" + (REFINE_RESOURCE_KEY_PREFIX + variableName).Replace('_', '-')

        public int min_peptides { get; set; }
        public bool remove_repeats { get; set; }
        public bool remove_duplicates { get; set; }
        public bool  missing_library { get; set; }
        public int min_transitions { get; set; }
        public string label_type { get; set; }
        public bool add_label_type { get; set; }
        public bool auto_select_peptides { get; set; }
        public bool auto_select_precursors { get; set; }
        public bool auto_select_transitions { get; set; }
        
        public double min_peak_found_ratio { get; set; }
        public double max_peak_found_ratio { get; set; }
        public int max_peptide_peak_rank { get; set; }
        public int max_transition_peak_rank { get; set; }
        public bool max_precursor_only { get; set; }
        public bool  prefer_larger_products { get; set; }
        public bool  missing_results { get; set; }
        public double  min_time_correlation { get; set; }
        public double  min_dotp { get; set; }
        public double  min_idotp { get; set; }
        public bool  use_best_result { get; set; }
        public double  cv_remove_above_cutoff { get; set; }
        public CvGlobalNormalizeValues  cv_global_normalize { get; set; }
        public string  cv_reference_normalize { get; set; }
        public CvTransitionsValues  cv_transitions { get; set; }
        public int  cv_transitions_count { get; set; }
        public CvMsLevelValues  cv_ms_level { get; set; }
        public double  qvalue_cutoff { get; set; }
        public int  minimum_detections { get; set; }
        public double  gc_p_value_cutoff { get; set; }
        public double  gc_fold_change_cutoff { get; set; }
        public double  gc_ms_level { get; set; }
        public string  gc_name { get; set; }

        #endregion

        #region Values for enum variables

        // Enum variables must have a "none" option. When "none" is selected, the command is not used
        public enum CvGlobalNormalizeValues
        {
            global_standards,
            equalize_medians,
            none
        }

        public enum CvTransitionsValues
        {
            all,
            best,
            none
        }

        public enum CvMsLevelValues
        {
            precursors,
            products,
            none
        }

        #endregion

        #region Read Command List

        private void ReadCommandList(IImmutableList<Tuple<RefineVariable, string>> initialValues)
        {
            foreach (var variableAndValue in initialValues)
                SetValue(variableAndValue.Item1, variableAndValue.Item2);
        }

        private void SetValue(RefineVariable variableName, string value)
        {
            switch (variableName)
            {
                case RefineVariable.min_peptides:
                    min_peptides = Int32.Parse(value);
                    break;
                case RefineVariable.remove_repeats:
                    remove_repeats = true;
                    break;
                case RefineVariable.remove_duplicates:
                    remove_duplicates = true;
                    break;
                case RefineVariable.missing_library:
                    missing_library = true;
                    break;
                case RefineVariable.min_transitions:
                    min_transitions = Int32.Parse(value);
                    break;
                case RefineVariable.label_type:
                    label_type = value;
                    break;
                case RefineVariable.add_label_type:
                    add_label_type = true;
                    break;
                case RefineVariable.auto_select_peptides:
                    auto_select_peptides = true;
                    break;
                case RefineVariable.auto_select_precursors:
                    auto_select_precursors = true;
                    break;
                case RefineVariable.auto_select_transitions:
                    auto_select_transitions = true;
                    break;

                case RefineVariable.min_peak_found_ratio:
                    min_peak_found_ratio = Double.Parse(value);
                    break;
                case RefineVariable.max_peak_found_ratio:
                    max_peak_found_ratio = Double.Parse(value);
                    break;
                case RefineVariable.max_peptide_peak_rank:
                    max_peptide_peak_rank = Int32.Parse(value);
                    break;
                case RefineVariable.max_transition_peak_rank:
                    max_transition_peak_rank = Int32.Parse(value);
                    break;
                case RefineVariable.max_precursor_only:
                    max_precursor_only = true;
                    break;
                case RefineVariable.prefer_larger_products:
                    prefer_larger_products = true;
                    break;
                case RefineVariable.missing_results:
                    missing_results = true;
                    break;
                case RefineVariable.min_time_correlation:
                    min_time_correlation = Double.Parse(value);
                    break;
                case RefineVariable.min_dotp:
                    min_dotp = Double.Parse(value);
                    break;
                case RefineVariable.min_idotp:
                    min_idotp = Double.Parse(value);
                    break;
                case RefineVariable.use_best_result:
                    use_best_result = true;
                    break;
                case RefineVariable.cv_remove_above_cutoff:
                    cv_remove_above_cutoff = Double.Parse(value);
                    break;
                case RefineVariable.cv_reference_normalize:
                    cv_reference_normalize = value;
                    break;
                case RefineVariable.cv_transitions_count:
                    cv_transitions_count = Int32.Parse(value);
                    break;
                case RefineVariable.qvalue_cutoff:
                    qvalue_cutoff = Double.Parse(value);
                    break;
                case RefineVariable.minimum_detections:
                    minimum_detections = Int32.Parse(value);
                    break;
                case RefineVariable.gc_p_value_cutoff:
                    gc_p_value_cutoff = Double.Parse(value);
                    break;
                case RefineVariable.gc_fold_change_cutoff:
                    gc_fold_change_cutoff = Double.Parse(value);
                    break;
                case RefineVariable.gc_ms_level:
                    gc_ms_level = Int32.Parse(value);
                    break;
                case RefineVariable.gc_name:
                    gc_name = value;
                    break;
                case RefineVariable.cv_global_normalize:
                    cv_global_normalize = (CvGlobalNormalizeValues)Enum.Parse(typeof(CvGlobalNormalizeValues), value);
                    break;
                case RefineVariable.cv_transitions:
                    cv_transitions = (CvTransitionsValues)Enum.Parse(typeof(CvTransitionsValues), value);
                    break;
                case RefineVariable.cv_ms_level:
                    cv_ms_level = (CvMsLevelValues) Enum.Parse(typeof(CvMsLevelValues), value);
                    break;
            }
        }

        #endregion

        #region Generate Command List

        public List<Tuple<RefineVariable, string>> AsCommandList()
        {
            var commands = new List<Tuple<RefineVariable, string>>();
            
            AddCommand(commands, RefineVariable.min_peptides, min_peptides);
            AddCommand(commands, RefineVariable.remove_repeats, remove_repeats);
            AddCommand(commands, RefineVariable.remove_duplicates, remove_duplicates);
            AddCommand(commands, RefineVariable.missing_library, missing_library);
            AddCommand(commands, RefineVariable.min_transitions, min_transitions);
            AddCommand(commands, RefineVariable.label_type, label_type);
            AddCommand(commands, RefineVariable.add_label_type, add_label_type);
            AddCommand(commands, RefineVariable.auto_select_peptides, auto_select_peptides);
            AddCommand(commands, RefineVariable.auto_select_precursors, auto_select_precursors);
            AddCommand(commands, RefineVariable.auto_select_transitions, auto_select_transitions);

            AddCommand(commands, RefineVariable.min_peak_found_ratio, min_peak_found_ratio);
            AddCommand(commands, RefineVariable.max_peak_found_ratio, max_peak_found_ratio);
            AddCommand(commands, RefineVariable.max_peptide_peak_rank, max_peptide_peak_rank);
            AddCommand(commands, RefineVariable.max_transition_peak_rank, max_transition_peak_rank);
            AddCommand(commands, RefineVariable.max_precursor_only, max_precursor_only);
            AddCommand(commands, RefineVariable.prefer_larger_products, prefer_larger_products);
            AddCommand(commands, RefineVariable.missing_results, missing_results);
            AddCommand(commands, RefineVariable.min_time_correlation, min_time_correlation);
            AddCommand(commands, RefineVariable.min_dotp, min_dotp);
            AddCommand(commands, RefineVariable.min_idotp, min_idotp);
            AddCommand(commands, RefineVariable.use_best_result, use_best_result);
            AddCommand(commands, RefineVariable.cv_remove_above_cutoff, cv_remove_above_cutoff);
            AddCommand(commands, RefineVariable.cv_reference_normalize, cv_reference_normalize);
            AddCommand(commands, RefineVariable.cv_transitions_count, cv_transitions_count);
            AddCommand(commands, RefineVariable.qvalue_cutoff, qvalue_cutoff);
            AddCommand(commands, RefineVariable.minimum_detections, minimum_detections);
            AddCommand(commands, RefineVariable.gc_p_value_cutoff, gc_p_value_cutoff);
            AddCommand(commands, RefineVariable.gc_fold_change_cutoff, gc_fold_change_cutoff);
            AddCommand(commands, RefineVariable.gc_ms_level, gc_ms_level);
            AddCommand(commands, RefineVariable.gc_name, gc_name);

            AddEnumCommand(commands, RefineVariable.cv_global_normalize,  cv_global_normalize.ToString());
            AddEnumCommand(commands, RefineVariable.cv_transitions,  cv_transitions.ToString());
            AddEnumCommand(commands, RefineVariable.cv_ms_level,  cv_ms_level.ToString());
            return commands;
        }


        private void AddCommand(List<Tuple<RefineVariable, string>> commands, RefineVariable variable, int input)
        {
            if (input == 0) return;
            Add(commands, variable, input.ToString());
        }

        private void AddCommand(List<Tuple<RefineVariable, string>> commands, RefineVariable variable, double input)
        {
            if (Math.Abs(input) < 0.00000000001) return;
            Add(commands, variable, input.ToString());
        }

        private void AddCommand(List<Tuple<RefineVariable, string>> commands, RefineVariable variable, string input)
        {
            if (string.IsNullOrEmpty(input)) return;
            Add(commands, variable, input);
        }

        private void AddCommand(List<Tuple<RefineVariable, string>> commands, RefineVariable variable, bool input)
        {
            if (!input) return;
            Add(commands, variable, string.Empty);
        }

        private void AddEnumCommand(List<Tuple<RefineVariable, string>> commands, RefineVariable variable, string input)
        {
            if (input.Equals("none")) return;
            Add(commands, variable, input);
        }

        private void Add(List<Tuple<RefineVariable, string>> commands, RefineVariable variable, string value)
        {
            commands.Add(new Tuple<RefineVariable, string>(variable, value));
        }

        #endregion

    }

    #region Refine Variable Enum

    public enum RefineVariable
    {
        // Group - Refine
        min_peptides,
        remove_repeats,
        remove_duplicates,
        missing_library,
        min_transitions,
        label_type,
        add_label_type,
        auto_select_peptides,
        auto_select_precursors,
        auto_select_transitions,

        // Group - Refine with results
        min_peak_found_ratio,
        max_peak_found_ratio,
        max_peptide_peak_rank,
        max_transition_peak_rank,
        max_precursor_only,
        prefer_larger_products,
        missing_results,
        min_time_correlation,
        min_dotp,
        min_idotp,
        use_best_result,
        cv_remove_above_cutoff,
        cv_reference_normalize,
        cv_transitions_count,
        qvalue_cutoff,
        minimum_detections,
        gc_p_value_cutoff,
        gc_fold_change_cutoff,
        gc_ms_level,
        gc_name,
        cv_global_normalize,
        cv_transitions,
        cv_ms_level
    }

    #endregion
}

    