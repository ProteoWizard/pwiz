using System;
using System.Collections.Generic;
using System.Globalization;
using SharedBatch;

namespace SkylineBatch
{
    public class RefineInputObject : GlobalizedObject
    {
        // prefix for all refine labels in CommandArgName.resx and refine descriptions in CommandArgUsage.resx
        public static readonly string REFINE_RESOURCE_KEY_PREFIX = "_refine_";


        public static RefineInputObject FromInvariantCommandList(List<Tuple<RefineVariable, string>> initialValues)
        {
            var refineInputObject = new RefineInputObject();
            refineInputObject.ReadCommandList(initialValues, CultureInfo.InvariantCulture);
            return refineInputObject;
        }

        #region Variables

        // Variable names correspond to values in resource files and batch command names:
        //      Displayed name = CommandArgName.(REFINE_RESOURCE_KEY_PREFIX + variableName)
        //      Displayed description = CommandArgUsage.(REFINE_RESOURCE_KEY_PREFIX + variableName)
        //      Batch command = "-" + (REFINE_RESOURCE_KEY_PREFIX + variableName).Replace('_', '-')

        public int? min_peptides { get; set; }
        public bool remove_repeats { get; set; }
        public bool remove_duplicates { get; set; }
        public bool  missing_library { get; set; }
        public int? min_transitions { get; set; }
        public string label_type { get; set; }
        public bool add_label_type { get; set; }
        public bool auto_select_peptides { get; set; }
        public bool auto_select_precursors { get; set; }
        public bool auto_select_transitions { get; set; }
        
        public double? min_peak_found_ratio { get; set; }
        public double? max_peak_found_ratio { get; set; }
        public int? max_peptide_peak_rank { get; set; }
        public int? max_transition_peak_rank { get; set; }
        public bool max_precursor_only { get; set; }
        public bool  prefer_larger_products { get; set; }
        public bool  missing_results { get; set; }
        public double?  min_time_correlation { get; set; }
        public double?  min_dotp { get; set; }
        public double?  min_idotp { get; set; }
        public bool  use_best_result { get; set; }
        public double?  cv_remove_above_cutoff { get; set; }
        public CvGlobalNormalizeValues?  cv_global_normalize { get; set; }
        public string  cv_reference_normalize { get; set; }
        public CvTransitionsValues?  cv_transitions { get; set; }
        public int?  cv_transitions_count { get; set; }
        public CvMsLevelValues?  cv_ms_level { get; set; }
        public double?  qvalue_cutoff { get; set; }
        public int?  minimum_detections { get; set; }
        public double?  gc_p_value_cutoff { get; set; }
        public double?  gc_fold_change_cutoff { get; set; }
        public double?  gc_ms_level { get; set; }
        public string  gc_name { get; set; }

        #endregion

        #region Values for enum variables

        public enum CvGlobalNormalizeValues
        {
            global_standards,
            equalize_medians
        }

        public enum CvTransitionsValues
        {
            all,
            best
        }

        public enum CvMsLevelValues
        {
            precursors,
            products
        }

        #endregion

        #region Read Command List

        private void ReadCommandList(List<Tuple<RefineVariable, string>> initialValues, CultureInfo culture)
        {
            foreach (var variableAndValue in initialValues)
                SetValue(variableAndValue.Item1, variableAndValue.Item2, culture);
        }

        private void SetValue(RefineVariable variableName, string value, CultureInfo culture)
        {
            switch (variableName)
            {
                case RefineVariable.min_peptides:
                    min_peptides = TextUtil.GetNullableIntFromString(value, culture);
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
                    min_transitions = TextUtil.GetNullableIntFromString(value, culture);
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
                    min_peak_found_ratio = TextUtil.GetNullableDoubleFromString(value, culture);
                    break;
                case RefineVariable.max_peak_found_ratio:
                    max_peak_found_ratio = TextUtil.GetNullableDoubleFromString(value, culture);
                    break;
                case RefineVariable.max_peptide_peak_rank:
                    max_peptide_peak_rank = TextUtil.GetNullableIntFromString(value, culture);
                    break;
                case RefineVariable.max_transition_peak_rank:
                    max_transition_peak_rank = TextUtil.GetNullableIntFromString(value, culture);
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
                    min_time_correlation = TextUtil.GetNullableDoubleFromString(value, culture);
                    break;
                case RefineVariable.min_dotp:
                    min_dotp = TextUtil.GetNullableDoubleFromString(value, culture);
                    break;
                case RefineVariable.min_idotp:
                    min_idotp = TextUtil.GetNullableDoubleFromString(value, culture);
                    break;
                case RefineVariable.use_best_result:
                    use_best_result = true;
                    break;
                case RefineVariable.cv_remove_above_cutoff:
                    cv_remove_above_cutoff = TextUtil.GetNullableDoubleFromString(value, culture);
                    break;
                case RefineVariable.cv_reference_normalize:
                    cv_reference_normalize = value;
                    break;
                case RefineVariable.cv_transitions_count:
                    cv_transitions_count = TextUtil.GetNullableIntFromString(value, culture);
                    break;
                case RefineVariable.qvalue_cutoff:
                    qvalue_cutoff = TextUtil.GetNullableDoubleFromString(value, culture);
                    break;
                case RefineVariable.minimum_detections:
                    minimum_detections = TextUtil.GetNullableIntFromString(value, culture);
                    break;
                case RefineVariable.gc_p_value_cutoff:
                    gc_p_value_cutoff = TextUtil.GetNullableDoubleFromString(value, culture);
                    break;
                case RefineVariable.gc_fold_change_cutoff:
                    gc_fold_change_cutoff = TextUtil.GetNullableDoubleFromString(value, culture);
                    break;
                case RefineVariable.gc_ms_level:
                    gc_ms_level = TextUtil.GetNullableIntFromString(value, culture);
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

        public List<Tuple<RefineVariable, string>> AsCommandList(CultureInfo culture)
        {
            var commands = new List<Tuple<RefineVariable, string>>();
            
            AddCommand(commands, RefineVariable.min_peptides, min_peptides, culture);
            AddCommand(commands, RefineVariable.remove_repeats, remove_repeats);
            AddCommand(commands, RefineVariable.remove_duplicates, remove_duplicates);
            AddCommand(commands, RefineVariable.missing_library, missing_library);
            AddCommand(commands, RefineVariable.min_transitions, min_transitions, culture);
            AddCommand(commands, RefineVariable.label_type, label_type);
            AddCommand(commands, RefineVariable.add_label_type, add_label_type);
            AddCommand(commands, RefineVariable.auto_select_peptides, auto_select_peptides);
            AddCommand(commands, RefineVariable.auto_select_precursors, auto_select_precursors);
            AddCommand(commands, RefineVariable.auto_select_transitions, auto_select_transitions);

            AddCommand(commands, RefineVariable.min_peak_found_ratio, min_peak_found_ratio, culture);
            AddCommand(commands, RefineVariable.max_peak_found_ratio, max_peak_found_ratio, culture);
            AddCommand(commands, RefineVariable.max_peptide_peak_rank, max_peptide_peak_rank, culture);
            AddCommand(commands, RefineVariable.max_transition_peak_rank, max_transition_peak_rank, culture);
            AddCommand(commands, RefineVariable.max_precursor_only, max_precursor_only);
            AddCommand(commands, RefineVariable.prefer_larger_products, prefer_larger_products);
            AddCommand(commands, RefineVariable.missing_results, missing_results);
            AddCommand(commands, RefineVariable.min_time_correlation, min_time_correlation);
            AddCommand(commands, RefineVariable.min_dotp, min_dotp, culture);
            AddCommand(commands, RefineVariable.min_idotp, min_idotp, culture);
            AddCommand(commands, RefineVariable.use_best_result, use_best_result);
            AddCommand(commands, RefineVariable.cv_remove_above_cutoff, cv_remove_above_cutoff);
            AddCommand(commands, RefineVariable.cv_reference_normalize, cv_reference_normalize);
            AddCommand(commands, RefineVariable.cv_transitions_count, cv_transitions_count, culture);
            AddCommand(commands, RefineVariable.qvalue_cutoff, qvalue_cutoff, culture);
            AddCommand(commands, RefineVariable.minimum_detections, minimum_detections, culture);
            AddCommand(commands, RefineVariable.gc_p_value_cutoff, gc_p_value_cutoff, culture);
            AddCommand(commands, RefineVariable.gc_fold_change_cutoff, gc_fold_change_cutoff, culture);
            AddCommand(commands, RefineVariable.gc_ms_level, gc_ms_level, culture);
            AddCommand(commands, RefineVariable.gc_name, gc_name);

            AddCommand(commands, RefineVariable.cv_global_normalize,  cv_global_normalize);
            AddCommand(commands, RefineVariable.cv_transitions,  cv_transitions);
            AddCommand(commands, RefineVariable.cv_ms_level,  cv_ms_level);
            return commands;
        }

        // Add enum value
        private void AddCommand(List<Tuple<RefineVariable, string>> commands, RefineVariable variable, object input)
        {
            if (input == null) return;
            commands.Add(new Tuple<RefineVariable, string>(variable, input.ToString()));
        }

        // Add integer
        private void AddCommand(List<Tuple<RefineVariable, string>> commands, RefineVariable variable, int? input, CultureInfo culture)
        {
            if (input == null) return;
            commands.Add(new Tuple<RefineVariable, string>(variable, ((int)input).ToString(culture)));
        }

        // Add double
        private void AddCommand(List<Tuple<RefineVariable, string>> commands, RefineVariable variable, Double? input, CultureInfo culture)
        {
            if (input == null) return;
            commands.Add(new Tuple<RefineVariable, string>(variable, ((Double)input).ToString(culture)));
        }

        // Add string
        private void AddCommand(List<Tuple<RefineVariable, string>> commands, RefineVariable variable, string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            commands.Add(new Tuple<RefineVariable, string>(variable, input));
        }

        // Add boolean
        private void AddCommand(List<Tuple<RefineVariable, string>> commands, RefineVariable variable, bool input)
        {
            if (!input) return;
            commands.Add(new Tuple<RefineVariable, string>(variable, string.Empty));
        }

        #endregion

        public RefineInputObject Copy()
        {
            return new RefineInputObject()
            {
                min_peptides = this.min_peptides,
                remove_repeats = this.remove_repeats,
                remove_duplicates = this.remove_duplicates,
                missing_library = this.missing_library,
                min_transitions = this.min_transitions,
                label_type = this.label_type,
                add_label_type = this.add_label_type,
                auto_select_peptides = this.auto_select_peptides,
                auto_select_precursors = this.auto_select_precursors,
                auto_select_transitions = this.auto_select_transitions,
                min_peak_found_ratio = this.min_peak_found_ratio,
                max_peak_found_ratio = this.max_peak_found_ratio,
                max_peptide_peak_rank = this.max_peptide_peak_rank,
                max_transition_peak_rank = this.max_transition_peak_rank,
                max_precursor_only = this.max_precursor_only,
                prefer_larger_products = this.prefer_larger_products,
                missing_results = this.missing_results,
                min_time_correlation = this.min_time_correlation,
                min_dotp = this.min_dotp,
                min_idotp = this.min_idotp,
                use_best_result = this.use_best_result,
                cv_remove_above_cutoff = this.cv_remove_above_cutoff,
                cv_reference_normalize = this.cv_reference_normalize,
                cv_transitions_count = this.cv_transitions_count,
                qvalue_cutoff = this.qvalue_cutoff,
                minimum_detections = this.minimum_detections,
                gc_p_value_cutoff = this.gc_p_value_cutoff,
                gc_fold_change_cutoff = this.gc_fold_change_cutoff,
                gc_ms_level = this.gc_ms_level,
                gc_name = this.gc_name,
                cv_global_normalize = this.cv_global_normalize,
                cv_transitions = this.cv_transitions,
                cv_ms_level = this.cv_ms_level
            };
        }

        public bool NoCommands()
        {
            return this.Equals(new RefineInputObject());
        }

        private bool NullableDoubleEquals(double? a, double? b)
        {
            if (a == null || b == null)
                return a == null && b == null;
            return Math.Abs((double) a - (double) b) < 0.000000001;
        }

        public bool Equals(RefineInputObject other)
        {
            return min_peptides == other.min_peptides &&
                   remove_repeats == other.remove_repeats &&
                   remove_duplicates == other.remove_duplicates &&
                   missing_library == other.missing_library &&
                   min_transitions == other.min_transitions &&
                   label_type == other.label_type &&
                   add_label_type == other.add_label_type &&
                   auto_select_peptides == other.auto_select_peptides &&
                   auto_select_precursors == other.auto_select_precursors &&
                   auto_select_transitions == other.auto_select_transitions &&
                   NullableDoubleEquals(min_peak_found_ratio,other.min_peak_found_ratio) &&
                   NullableDoubleEquals(max_peak_found_ratio, other.max_peak_found_ratio) &&
                   max_peptide_peak_rank == other.max_peptide_peak_rank &&
                   max_transition_peak_rank == other.max_transition_peak_rank &&
                   max_precursor_only == other.max_precursor_only &&
                   prefer_larger_products == other.prefer_larger_products &&
                   missing_results == other.missing_results &&
                   NullableDoubleEquals(min_time_correlation, other.min_time_correlation) &&
                   NullableDoubleEquals(min_dotp, other.min_dotp) &&
                   NullableDoubleEquals(min_idotp, other.min_idotp) &&
                   use_best_result == other.use_best_result &&
                   NullableDoubleEquals(cv_remove_above_cutoff, other.cv_remove_above_cutoff) &&
                   cv_reference_normalize == other.cv_reference_normalize &&
                   cv_transitions_count == other.cv_transitions_count &&
                   NullableDoubleEquals(qvalue_cutoff, other.qvalue_cutoff) &&
                   minimum_detections == other.minimum_detections &&
                   NullableDoubleEquals(gc_p_value_cutoff, other.gc_p_value_cutoff) &&
                   NullableDoubleEquals(gc_fold_change_cutoff, other.gc_fold_change_cutoff) &&
                   NullableDoubleEquals(gc_ms_level, other.gc_ms_level) &&
                   gc_name == other.gc_name &&
                   cv_global_normalize == other.cv_global_normalize &&
                   cv_transitions == other.cv_transitions &&
                   cv_ms_level == other.cv_ms_level;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RefineInputObject)obj);
        }

        public override int GetHashCode()
        {
            return NoCommands().GetHashCode();
        }

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

    