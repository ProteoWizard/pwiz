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
using System;
using System.Globalization;
using System.Windows.Forms;

namespace QuaSAR
{
// ReSharper disable InconsistentNaming
    public partial class QuaSARUI : Form
// ReSharper restore InconsistentNaming
    {
        public string[] Arguments { get; private set; }
        private int CalCurveShift { get; set; }
        private int AuDITShift { get; set; }
        private int EndoConfShift { get; set; }
        
        public QuaSARUI(string[] oldArguments)
        {
            Arguments = oldArguments;
            InitializeComponent();
            
            CalCurveShift = gboxGenerate.Top - calcurvePanel.Top;
            AuDITShift = cboxEndogenousCalc.Top - tboxAuDITCVThreshold.Top;
            EndoConfShift = btnDefault.Top - tboxEndoConf.Top;
        }

        private void QuaSAR_Load(object sender, EventArgs e)
        {
            InitializeHelpTip();
            ReorganizeComponentsEndoCalc();
            RestorePreviousValues();
        }

        private const string TRUE_STRING = "1";     // Not L10N
        private const string FALSE_STRING = "0";    // Not L10N
        
        private void RestorePreviousValues()
        {
            if (Arguments != null && Arguments.Length == ARGUMENT_COUNT)
            {
                tboxTitle.Text = Arguments[(int) ArgumentIndices.title];
                cboxStandardPresent.Checked = Arguments[(int) ArgumentIndices.standard].Equals(TRUE_STRING);
                cboxCVTable.Checked = Arguments[(int) ArgumentIndices.cv_table].Equals(TRUE_STRING);
                cboxCalCurves.Checked = Arguments[(int) ArgumentIndices.calcurves].Equals(TRUE_STRING);
                numberTransitions.Value = decimal.Parse(Arguments[(int) ArgumentIndices.ntransitions]);
                cboxLODLOQTable.Checked = Arguments[(int) ArgumentIndices.lodloq_table].Equals(TRUE_STRING);
                cboxPeakAreaPlots.Checked = Arguments[(int) ArgumentIndices.peakplots].Equals(TRUE_STRING);
                cboxLODLOQComp.Checked = Arguments[(int) ArgumentIndices.lodloq_comp].Equals(TRUE_STRING);
                cboxPAR.Checked = Arguments[(int) ArgumentIndices.par].Equals(TRUE_STRING);
                tboxLinearScale.Text = Arguments[(int) ArgumentIndices.max_linear];
                tboxLogScale.Text = Arguments[(int) ArgumentIndices.max_log];
                cboxAuDIT.Checked = Arguments[(int) ArgumentIndices.perform_audit].Equals(TRUE_STRING);
                tboxAuDITCVThreshold.Text = Arguments[(int) ArgumentIndices.audit_threshold];
                cboxEndogenousCalc.Checked = Arguments[(int) ArgumentIndices.perform_endocalc].Equals(TRUE_STRING);
                tboxEndoConf.Text = Arguments[(int) ArgumentIndices.endo_ci];
            }
        }

        private void InitializeHelpTip()
        {
            helpTip.SetToolTip(tboxTitle, DOCUMENTATION_TITLE);
            helpTip.SetToolTip(cboxStandardPresent, DOCUMENTATION_STD_PRESENT);
            helpTip.SetToolTip(cboxCVTable, DOCUMENTATION_CVTABLE);
            helpTip.SetToolTip(cboxCalCurves, DOCUMENTATION_CALCURVES);
            helpTip.SetToolTip(numberTransitions, DOCUMENTATION_NUMBER_TRANSITIONS);
            helpTip.SetToolTip(tboxLinearScale, DOCUMENTATION_MAXLINEAR);
            helpTip.SetToolTip(tboxLogScale, DOCUMENTATION_MAXLOG);
            helpTip.SetToolTip(cboxLODLOQTable, DOCUMENTATION_LODLOQTABLE);
            helpTip.SetToolTip(cboxPeakAreaPlots, DOCUMENTATION_PEAKPLOTS);
            helpTip.SetToolTip(cboxLODLOQComp, DOCUMENTATION_LODLOQCOMP);
            helpTip.SetToolTip(cboxPAR, DOCUMENTATION_PAR);
            helpTip.SetToolTip(cboxAuDIT, DOCUMENTATION_AUDIT);
            helpTip.SetToolTip(tboxAuDITCVThreshold, DOCUMENTATION_AUDITCVTHRESHOLD);
            helpTip.SetToolTip(cboxEndogenousCalc, DOCUMENTATION_PERFORMENDOCALC);
            helpTip.SetToolTip(tboxEndoConf, DOCUMENTATION_ENDOCONF);
        }

        #region Argument documentation

        // titles

        private const string DOCUMENTATION_TITLE = "The title to be displayed on each calibration plot";

        // things to generate

        private const string DOCUMENTATION_CVTABLE = "If checked then QuaSAR generates a CV (coefficient of variation) table";
        private const string DOCUMENTATION_CALCURVES = "If checked then QuaSAR generates calibration curves";
        private const string DOCUMENTATION_LODLOQTABLE = "If checked then QuaSAR generates a LOD/LOQ table";
        private const string DOCUMENTATION_PEAKPLOTS =
            "If checked then QuaSAR generates peak area plots with Peak Area units on the y-axis and analyte concentration on the x-axis";

        private const string DOCUMENTATION_LODLOQCOMP = "If checked then QuaSAR generates a table comparing multiple methods of calculating LOD/LOQ";

        // settings

        private const string DOCUMENTATION_STD_PRESENT = "If there is no standard present, peak area plots will be generated instead of calibration curves";
        private const string DOCUMENTATION_NUMBER_TRANSITIONS = "Max number of transitions to be plotted on the calibration curves";
        private const string DOCUMENTATION_PAR =
            "If checked then use peak area ratio (PAR) for analysis (instead of concentration) with PAR on the y-axis and analyte concentration on the x-axis";

        private const string DOCUMENTATION_MAXLINEAR = "The maximum value for linear scale in fmols/ul";
        private const string DOCUMENTATION_MAXLOG = "The maximum value for log scale in fmols/ul";
        private const string DOCUMENTATION_AUDIT = "If checked then QuaSAR will perform AuDIT for interference detection";
        private const string DOCUMENTATION_AUDITCVTHRESHOLD = "For AuDIT the threshold for coefficient of variation below which transition is quantification-worthy";
        private const string DOCUMENTATION_PERFORMENDOCALC = "If checked then QuaSAR will determine if the peptide has endogenous levels and will provide an estimate.";
        private const string DOCUMENTATION_ENDOCONF = "Confidence level for endogenous determination (between 0 and 1, typically 0.95 or 0.99)";

        #endregion

        private void btnOK_Click(object sender, EventArgs e)
        {
            OKDialog();
        }

        private void OKDialog()
        {
            if (VerifyArguments())
            {
                GenerateArguments();
                DialogResult = DialogResult.OK;
            }
        }

        private bool VerifyArguments()
        {
            if (string.IsNullOrEmpty(tboxTitle.Text))
            {
                MessageBox.Show(this, "Please enter a title");
                return false;
            } else if (string.IsNullOrEmpty(tboxLinearScale.Text))
            {
                MessageBox.Show(this, "Please enter a value for the maximum linear scale");
                return false;
            } else if (string.IsNullOrEmpty(tboxLogScale.Text))
            {
                MessageBox.Show(this, "Please enter a value for the maximum log scale");
                return false;
            } else if (string.IsNullOrEmpty(tboxAuDITCVThreshold.Text))
            {
                MessageBox.Show(this, "Please enter a value for the AuDIT CV threshold");
                return false;
            } else if (string.IsNullOrEmpty(tboxEndoConf.Text))
            {
                MessageBox.Show(this, "Please enter a value for the endogenous confidence level");
                return false;
            }
            return true;
        }

        private const int ARGUMENT_COUNT = 20;

        /*  The arguments generated for QuaSAR consists of the following, in the specified order:
         * 
         * 0. A null value for the concentration report (not needed)
         * 1. The title -> e.g. "Test"
         * 2. The analyte Name -> "light Area"
         * 3. Is there a standard present? -> TRUE or FALSE
         * 4. The standard Name -> "heavy Area"
         * 5. Units label -> "fmol/ul"
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

        private enum ArgumentIndices
        {
            concentration_report,
            title,
            analyte,
            standard_present,
            standard,
            units_label,
            cv_table,
            calcurves,
            ntransitions,
            lodloq_table,
            lodloq_comp,
            peakplots,
            par,
            max_linear,
            max_log,
            perform_audit,
            audit_threshold,
            perform_endocalc,
            endo_ci,
            output_prefix
        }

        public void GenerateArguments()
        {
            const string analyte = "light Area";  // Not L10N
            const string standard = "heavy Area"; // Not L10N
            const string units = "fmol/ul";       // Not L10N
            const string nullString = "NULL";     // Not L10N

            Arguments = new string[ARGUMENT_COUNT];

            Arguments[(int) ArgumentIndices.concentration_report] = nullString;
            Arguments[(int) ArgumentIndices.title] = tboxTitle.Text;
            Arguments[(int) ArgumentIndices.analyte] = analyte;
            Arguments[(int) ArgumentIndices.standard_present] = cboxStandardPresent.Checked ? TRUE_STRING : FALSE_STRING; 
            Arguments[(int) ArgumentIndices.standard] = standard;
            Arguments[(int) ArgumentIndices.units_label] = units;
            Arguments[(int) ArgumentIndices.cv_table] = cboxCVTable.Checked ? TRUE_STRING : FALSE_STRING;
            Arguments[(int) ArgumentIndices.calcurves] = cboxCalCurves.Checked ? TRUE_STRING : FALSE_STRING;
            Arguments[(int) ArgumentIndices.ntransitions] = numberTransitions.Value.ToString(CultureInfo.InvariantCulture);
            Arguments[(int) ArgumentIndices.lodloq_table] = cboxLODLOQTable.Checked ? TRUE_STRING : FALSE_STRING;
            Arguments[(int) ArgumentIndices.lodloq_comp] = cboxLODLOQComp.Checked ? TRUE_STRING : FALSE_STRING;
            Arguments[(int) ArgumentIndices.peakplots] = cboxPeakAreaPlots.Checked ? TRUE_STRING : FALSE_STRING;
            Arguments[(int) ArgumentIndices.par] = cboxPAR.Checked ? TRUE_STRING : FALSE_STRING;
            Arguments[(int) ArgumentIndices.max_linear] = tboxLinearScale.Text;
            Arguments[(int) ArgumentIndices.max_log] = tboxLogScale.Text;
            Arguments[(int) ArgumentIndices.perform_audit] = cboxAuDIT.Checked ? TRUE_STRING : FALSE_STRING;
            Arguments[(int) ArgumentIndices.audit_threshold] = tboxAuDITCVThreshold.Text;
            Arguments[(int) ArgumentIndices.perform_endocalc] = cboxEndogenousCalc.Checked ? TRUE_STRING : FALSE_STRING;
            Arguments[(int) ArgumentIndices.endo_ci] = tboxEndoConf.Text;
            Arguments[(int) ArgumentIndices.output_prefix] = tboxTitle.Text;
        }
        
        private void btnDefault_Click(object sender, EventArgs e)
        {
            RestoreDefaultValues();
        }

        private void RestoreDefaultValues()
        {
            cboxStandardPresent.Checked = false;
            cboxCVTable.Checked = true;
            cboxCalCurves.Checked = true;
            cboxLODLOQTable.Checked = true;
            cboxPeakAreaPlots.Checked = false;
            cboxLODLOQComp.Checked = false;
            cboxPAR.Checked = false;
            cboxAuDIT.Checked = true;
            cboxEndogenousCalc.Checked = false;

            numberTransitions.Value = 3;
            tboxLinearScale.Text = "150";
            tboxLogScale.Text = "150";
            tboxAuDITCVThreshold.Text = "0.2";
            tboxEndoConf.Text = "0.95";
        }

        private void NumericTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            // only support numerical values
            if (!char.IsDigit(e.KeyChar) && e.KeyChar != '.' && e.KeyChar != (char)Keys.Back)
            {
                e.Handled = true;
            }
            if (e.KeyChar == '.')
            {
                var source = (TextBox)sender;
                if (source.Text.Contains("."))
                    e.Handled = true;
            }
        }

        private void cboxStandardPresent_CheckedChanged(object sender, EventArgs e)
        {
            if (cboxStandardPresent.Checked)
            {
                cboxCalCurves.Checked = false;
                cboxCalCurves.Enabled = false;
                cboxPeakAreaPlots.Checked = true;
            }
            else
            {
                cboxCalCurves.Enabled = true;
            }
        }

        private void cboxCalCurves_CheckedChanged(object sender, EventArgs e)
        {
            ReorganizeComponentsCalCurves();
        }

        private void ReorganizeComponentsCalCurves()
        {
            calcurvePanel.Enabled = calcurvePanel.Visible = cboxCalCurves.Checked;
            int shift = cboxCalCurves.Checked ? CalCurveShift : -CalCurveShift;

            gboxGenerate.Top += shift;
            cboxPAR.Top += shift;
            cboxAuDIT.Top += shift;
            labelAuDITCV.Top += shift;
            tboxAuDITCVThreshold.Top += shift;
            cboxEndogenousCalc.Top += shift;
            labelEndogenousConfidence.Top += shift;
            tboxEndoConf.Top += shift;
            Height += shift;
        }

        private void cboxAuDIT_CheckedChanged(object sender, EventArgs e)
        {
            ReorganizeComponentsAuDIT();
        }

        private void ReorganizeComponentsAuDIT()
        {
            tboxAuDITCVThreshold.Enabled = tboxAuDITCVThreshold.Visible = labelAuDITCV.Enabled = labelAuDITCV.Visible = cboxAuDIT.Checked;
            int shift = tboxAuDITCVThreshold.Enabled ? AuDITShift : -AuDITShift;
            
            cboxEndogenousCalc.Top += shift;
            labelEndogenousConfidence.Top += shift;
            tboxEndoConf.Top += shift;
            Height += shift;
        }

        private void cboxEndogenousCalc_CheckedChanged(object sender, EventArgs e)
        {
            ReorganizeComponentsEndoCalc();
        }

        private void ReorganizeComponentsEndoCalc()
        {
            tboxEndoConf.Enabled =
                tboxEndoConf.Visible =
                labelEndogenousConfidence.Enabled = labelEndogenousConfidence.Visible = cboxEndogenousCalc.Checked;
            
            Height += cboxEndogenousCalc.Checked ? EndoConfShift : -EndoConfShift;
        }
    }

// ReSharper disable InconsistentNaming
    public class QuaSARCollector
// ReSharper restore InconsistentNaming
    {
        public static string[] CollectArgs(IWin32Window parent, string report, string[] oldArgs)
        {
            using (var dlg = new QuaSARUI(oldArgs))
            {
                if (parent != null)
                {
                    return (dlg.ShowDialog(parent) == DialogResult.OK) ? dlg.Arguments : null;
                }
                else
                {
                    return (dlg.ShowDialog() == DialogResult.OK) ? dlg.Arguments : null;
                }
            }
        }
    }
}
