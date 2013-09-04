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
        public const int ARGUMENT_COUNT = 20;
    }

    static class ArgumentDocumentation
    {
         // titles

        public const string TITLE = "The title to be displayed on each calibration plot";

        // things to generate

        public const string CVTABLE = "If checked then QuaSAR generates a CV (coefficient of variation) table";
        public const string CALCURVES = "If checked then QuaSAR generates calibration curves";
        public const string LODLOQTABLE = "If checked then QuaSAR generates a LOD/LOQ table";
        public const string PEAKAREAPLOTS =
            "If checked then QuaSAR generates peak area plots with Peak Area units on the y-axis and analyte concentration on the x-axis";

        public const string LODLOQCOMP = "If checked then QuaSAR generates a table comparing multiple methods of calculating LOD/LOQ";

        // settings

        public const string ANALYTE = "";
        public const string STANDARD = "";
        public const string UNITS = "";
        public const string NUMBER_TRANSITIONS = "Max number of transitions to be plotted on the calibration curves";
        public const string PAR =
            "If checked then use peak area ratio (PAR) for analysis (instead of concentration) with PAR on the y-axis and analyte concentration on the x-axis";

        public const string REVERSE_CURVES = "";

        public const string MAXLINEAR = "The maximum value for linear scale in fmols/ul";
        public const string MAXLOG = "The maximum value for log scale in fmols/ul";
        public const string AUDIT = "If checked then QuaSAR will perform AuDIT for interference detection";
        public const string AUDITCVTHRESHOLD = "For AuDIT the threshold for coefficient of variation below which transition is quantification-worthy";
        public const string PERFORMENDOCALC = "If checked then QuaSAR will determine if the peptide has endogenous levels and will provide an estimate.";
        public const string ENDOCONF = "Confidence level for endogenous determination (between 0 and 1, typically 0.95 or 0.99)";
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
        output_prefix
    }

    static class Defaults
    {
        public const string CONCENTRATION_REPORT = Constants.NULL_STRING;                               
        public const bool STANDARD_PRESENT = true;
        public const string UNITS = "fmol/ul";                                      // Not L10N
        public const bool CV_TABLE = true;
        public const bool CALIBRATION_CURVES = true;
        public const decimal NUMBER_TRANSITIONS = 3;
        public const bool LODLOQ_TABLE = true;
        public const bool LODLOQ_COMPARISON = false;
        public const bool PEAK_AREA_PLOTS = false;
        public const bool USE_PAR = false;
        public const string MAX_LINEAR = "150";                                     // Not L10N
        public const string MAX_LOG = "150";                                        // Not L10N
        public const bool PERFORM_AUDIT = true;
        public const string AUDIT_CV_THRESHOLD = "0.2";                             // Not L10N
        public const bool PERFORM_ENDOCALC = false;
        public const string ENDOGENOUS_CI = "0.95";                                 // Not L10N
    }

    public static class TextUtil
    {
        public const char SEPARATOR_CSV = ','; // Not L10N

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
