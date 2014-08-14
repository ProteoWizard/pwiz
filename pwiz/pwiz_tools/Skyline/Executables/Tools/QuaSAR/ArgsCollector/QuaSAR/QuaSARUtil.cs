/*
 * Original author: Trevor Killeen <killeent .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Text;

namespace QuaSAR
{
    static class Constants
    {
        public const string TRUE_STRING = "1";              // Not L10N
        public const string FALSE_STRING = "0";             // Not L10N
        public const string NULL_STRING = "NULL";           // Not L10N
        public const string NONE_STRING = "None";           // Not L10N
        public const int ARGUMENT_COUNT = 21;
    }

    class ArgumentDocumentation
    {
         // titles

        public static readonly string TITLE = QuaSARResources.ArgumentDocumentation_TITLE_The_title_to_be_displayed_on_each_calibration_plot;

        // things to generate

        public static readonly string CVTABLE = QuaSARResources.ArgumentDocumentation_CVTABLE_If_checked_then_QuaSAR_generates_a_CV__coefficient_of_variation__table;
        public static readonly string CALCURVES = QuaSARResources.ArgumentDocumentation_CALCURVES_If_checked_then_QuaSAR_generates_calibration_curves;
        public static readonly string LODLOQTABLE = QuaSARResources.ArgumentDocumentation_LODLOQTABLE_If_checked_then_QuaSAR_generates_a_LOD_LOQ_table;
        public static readonly string PEAKAREAPLOTS =
            QuaSARResources.ArgumentDocumentation_PEAKAREAPLOTS_If_checked_then_QuaSAR_generates_peak_area_plots_with_Peak_Area_units_on_the_y_axis_and_analyte_concentration_on_the_x_axis;

        public static string LODLOQCOMP = QuaSARResources.ArgumentDocumentation_LODLOQCOMP_If_checked_then_QuaSAR_generates_a_table_comparing_multiple_methods_of_calculating_LOD_LOQ;

        // settings

        public static readonly string ANALYTE = string.Empty;
        public static readonly string STANDARD = string.Empty;
        public static readonly string UNITS = string.Empty;
        public static readonly string NUMBER_TRANSITIONS = QuaSARResources.ArgumentDocumentation_NUMBER_TRANSITIONS_Max_number_of_transitions_to_be_plotted_on_the_calibration_curves;
        public static readonly string PAR =
            QuaSARResources.ArgumentDocumentation_PAR_If_checked_then_use_peak_area_ratio__PAR__for_analysis__instead_of_concentration__with_PAR_on_the_y_axis_and_analyte_concentration_on_the_x_axis;

        public static readonly string REVERSE_CURVES = string.Empty;

        public static readonly string MAXLINEAR = QuaSARResources.ArgumentDocumentation_MAXLINEAR_The_maximum_value_for_linear_scale_in_fmols_ul;
        public static readonly string MAXLOG = QuaSARResources.ArgumentDocumentation_MAXLOG_The_maximum_value_for_log_scale_in_fmols_ul;
        public static readonly string AUDIT = QuaSARResources.ArgumentDocumentation_AUDIT_If_checked_then_QuaSAR_will_perform_AuDIT_for_interference_detection;
        public static readonly string AUDITCVTHRESHOLD = QuaSARResources.ArgumentDocumentation_AUDITCVTHRESHOLD_For_AuDIT_the_threshold_for_coefficient_of_variation_below_which_transition_is_quantification_worthy;
        public static readonly string PERFORMENDOCALC = QuaSARResources.ArgumentDocumentation_PERFORMENDOCALC_If_checked_then_QuaSAR_will_determine_if_the_peptide_has_endogenous_levels_and_will_provide_an_estimate_;
        public static readonly string ENDOCONF = QuaSARResources.ArgumentDocumentation_ENDOCONF_Confidence_level_for_endogenous_determination__between_0_and_1__typically_0_95_or_0_99_;
    }

    /*  The arguments generated for QuaSAR consists of the following, in the specified order:
     * 
     * 0. A null value for the concentration report (not needed)
     * 1. The title -> e.g. "Test"
     * 2. The analyte Name -> e.g. "light Area"
     * 3. Is there a standard present? -> TRUE or FALSE
     * 4. The standard Name -> e.g. "heavy Area"
     * 5. Units label -> e.g. "fmol/ul"
     * 6. Generate a CV Table -> TRUE or FALSE
     * 7. Generate Calibration Curves -> TRUE or FALSE
     * 8. n Transitions Plot -> a numeric value, e.g. "3"
     * 9. Generate a LOD/LOQ Table -> TRUE or FALSE
     * 10. Generate a LOD/LOQ Comparison Table -> TRUE or FALSE
     * 11. Generate Peak Area Plots -> TRUE or FALSE
     * 12. Use PAR -> TRUE or FALSE
     * 13. The maximum linear scale -> a numeric value, e.g. "150"
     * 14. The maximum log scale -> a numeric value, e.g. "150"
     * 15. Perform AuDIT -> TRUE or FALSE
     * 16. The AuDIT CV threshold -> a numeric value, e.g. "0.2"
     * 17. Perform endogenous calculation -> TRUE or FALSE
     * 18. The endogenous confidence level -> a numeric value, e.g. "0.95"
     * 19. The output prefix -> e.g "Test" (same as the title above)
     * */

    public enum ArgumentIndices
    {
        concentration_report,
        title,
        analyte,
        standard_present,
        standard,
        units,
        cv_table,
        calcurves,
        ntransitions,
        lodloq_table,
        lodloq_comp,
        peakplots,
        use_par,
        max_linear,
        max_log,
        perform_audit,
        audit_threshold,
        perform_endocalc,
        endo_ci,
        output_prefix,
        create_individual_plots
    }

    static class Defaults
    {
        public const string CONCENTRATION_REPORT = Constants.NULL_STRING;                               
        public const bool STANDARD_PRESENT = true;
        public const string UNITS = "fmol/ul";                                      // Not L10N
        public const bool CV_TABLE = true;
        public const bool CALIBRATION_CURVES = true;
        public const int NUMBER_TRANSITIONS = 3;
        public const bool GRAPH_PLOT = false;
        public const bool LODLOQ_TABLE = true;
        public const bool LODLOQ_COMPARISON = false;
        public const bool PEAK_AREA_PLOTS = false;
        public const bool USE_PAR = false;
        public const int MAX_LINEAR = 150;
        public const int MAX_LOG = 150;
        public const bool PERFORM_AUDIT = true;
        public const decimal AUDIT_CV_THRESHOLD = 0.2m;
        public const bool PERFORM_ENDOCALC = false;
        public const decimal ENDOGENOUS_CI = 0.95m;
    }

    public static class TextUtil
    {
        public const char SEPARATOR_CSV = ','; // Not L10N
        public const char SEPARATOR_DSV = ';'; // Not L10N

        /// <summary>
        /// Splits a line of text in comma-separated value format into an array of fields.
        /// The function correctly handles quotation marks.
        /// </summary>
        /// <param name="line">The line to be split into fields</param>
        /// <returns>An array of field strings</returns>
        public static string[] ParseCsvFields(this string line)
        {
            return line.ParseDsvFields(SEPARATOR_CSV);
        }

        /// <summary>
        /// Splits a line of text in delimiter-separated value format into an array of fields.
        /// The function correctly handles quotation marks.
        /// </summary>
        /// <param name="line">The line to be split into fields</param>
        /// <param name="separator">The separator being used</param>
        /// <returns>An array of field strings</returns>
        public static string[] ParseDsvFields(this string line, char separator)
        {
            var listFields = new List<string>();
            var sbField = new StringBuilder();
            bool inQuotes = false;
            char chLast = '\0';  // Not L10N
            foreach (char ch in line)
            {
                if (inQuotes)
                {
                    if (ch == '"')
                        inQuotes = false;
                    else
                        sbField.Append(ch);
                }
                else if (ch == '"')  // Not L10N
                {
                    inQuotes = true;
                    // Add quote character, for "" inside quotes
                    if (chLast == '"')  // Not L10N
                        sbField.Append(ch);
                }
                else if (ch == separator)
                {
                    listFields.Add(sbField.ToString());
                    sbField.Remove(0, sbField.Length);
                }
                else
                {
                    sbField.Append(ch);
                }
                chLast = ch;
            }
            listFields.Add(sbField.ToString());
            return listFields.ToArray();
        }

    }
}
