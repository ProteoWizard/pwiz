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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace QuaSAR
{
// ReSharper disable InconsistentNaming
    public partial class QuaSARUI : Form
// ReSharper restore InconsistentNaming
    {
        public string[] Arguments { get; private set; }
        private IEnumerable<string> Areas { get; set; } 
        
        public QuaSARUI(string[] oldArguments, IEnumerable<string> areas)
        {
            Arguments = oldArguments;
            Areas = areas;
            InitializeComponent();
        }

        private void QuaSAR_Load(object sender, EventArgs e)
        {
            InitializeHelpTip();
            PopulateAnalyteAndStandard();
            RestorePreviousValues();
        }

        private void InitializeHelpTip()
        {
            helpTip.SetToolTip(tboxTitle, ArgumentDocumentation.TITLE);

            // generate
            helpTip.SetToolTip(tboxTitle, ArgumentDocumentation.TITLE);
            helpTip.SetToolTip(cboxCalCurves, ArgumentDocumentation.CALCURVES);
            helpTip.SetToolTip(cboxCVTable, ArgumentDocumentation.CVTABLE);
            helpTip.SetToolTip(cboxLODLOQTable, ArgumentDocumentation.LODLOQTABLE);
            helpTip.SetToolTip(cboxLODLOQComp, ArgumentDocumentation.LODLOQCOMP);
            helpTip.SetToolTip(cboxPeakAreaPlots, ArgumentDocumentation.PEAKAREAPLOTS);

            // options
            helpTip.SetToolTip(cboxStandardPresent, ArgumentDocumentation.STANDARD_PRESENT);
            helpTip.SetToolTip(cboxPAR, ArgumentDocumentation.PAR);
            helpTip.SetToolTip(comboBoxAnalyte, ArgumentDocumentation.ANALYTE);
            helpTip.SetToolTip(comboBoxStandard, ArgumentDocumentation.STANDARD);
            helpTip.SetToolTip(tboxUnits, ArgumentDocumentation.UNITS);

            // plots
            helpTip.SetToolTip(numberTransitions, ArgumentDocumentation.NUMBER_TRANSITIONS);
            helpTip.SetToolTip(tboxLinearScale, ArgumentDocumentation.MAXLINEAR);
            helpTip.SetToolTip(tboxLogScale, ArgumentDocumentation.MAXLOG);

            // AuDIT
            helpTip.SetToolTip(cboxAuDIT, ArgumentDocumentation.AUDIT);
            helpTip.SetToolTip(tboxAuDITCVThreshold, ArgumentDocumentation.AUDITCVTHRESHOLD);

            // endogenous estimation
            helpTip.SetToolTip(cboxEndogenousCalc, ArgumentDocumentation.PERFORMENDOCALC);
            helpTip.SetToolTip(tboxEndoConf, ArgumentDocumentation.ENDOCONF);
        }

        private void PopulateAnalyteAndStandard()
        {
            comboBoxAnalyte.DataSource = new List<string>(Areas);
            comboBoxStandard.DataSource = new List<string>(Areas);
            // make the standard a different value than the analyte so that the tool can run without the user
            // having to make any changes at all
            comboBoxStandard.SelectedIndex = 1;
        }

        private void RestorePreviousValues()
        {
            if (Arguments != null && Arguments.Length == Constants.ARGUMENT_COUNT)
            {
                tboxTitle.Text = Arguments[(int) ArgumentIndices.title];

                // generate
                cboxCalCurves.Checked = Arguments[(int) ArgumentIndices.calcurves].Equals(Constants.TRUE_STRING);
                cboxCVTable.Checked = Arguments[(int) ArgumentIndices.cv_table].Equals(Constants.TRUE_STRING);
                cboxLODLOQTable.Checked = Arguments[(int) ArgumentIndices.lodloq_table].Equals(Constants.TRUE_STRING);
                cboxLODLOQComp.Checked = Arguments[(int) ArgumentIndices.lodloq_comp].Equals(Constants.TRUE_STRING);
                cboxPeakAreaPlots.Checked = Arguments[(int) ArgumentIndices.peakplots].Equals(Constants.TRUE_STRING);

                // options
                cboxStandardPresent.Checked = Arguments[(int) ArgumentIndices.standard_present].Equals(Constants.TRUE_STRING);
                cboxPAR.Checked = Arguments[(int) ArgumentIndices.use_par].Equals(Constants.TRUE_STRING);
                comboBoxAnalyte.SelectedItem = comboBoxAnalyte.Items.Contains(Arguments[(int) ArgumentIndices.analyte])
                                                   ? Arguments[(int) ArgumentIndices.analyte]
                                                   : comboBoxAnalyte.Items[0];
                comboBoxStandard.SelectedItem = comboBoxStandard.Items.Contains(Arguments[(int)ArgumentIndices.standard])
                                                   ? Arguments[(int)ArgumentIndices.standard]
                                                   : comboBoxStandard.Items[0];
                tboxUnits.Text = Arguments[(int) ArgumentIndices.units];

                // plots
                numberTransitions.Value = decimal.Parse(Arguments[(int)ArgumentIndices.ntransitions]);
                tboxLinearScale.Text = Arguments[(int)ArgumentIndices.max_linear];
                tboxLogScale.Text = Arguments[(int)ArgumentIndices.max_log];

                // AuDIT
                cboxAuDIT.Checked = Arguments[(int)ArgumentIndices.perform_audit].Equals(Constants.TRUE_STRING);
                tboxAuDITCVThreshold.Text =
                    Arguments[(int) ArgumentIndices.audit_threshold].Equals(Constants.NULL_STRING)
                        ? Defaults.AUDIT_CV_THRESHOLD
                        : Arguments[(int) ArgumentIndices.audit_threshold];

                // endogenous estimation
                cboxEndogenousCalc.Checked = Arguments[(int)ArgumentIndices.perform_endocalc].Equals(Constants.TRUE_STRING);
                tboxEndoConf.Text = Arguments[(int) ArgumentIndices.endo_ci].Equals(Constants.NULL_STRING)
                                        ? Defaults.ENDOGENOUS_CI
                                        : Arguments[(int) ArgumentIndices.endo_ci];
            }
        }

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
            if (string.IsNullOrWhiteSpace(tboxTitle.Text))
            {
                MessageBox.Show(this, "Please enter a title");
                return false;
            } else if (comboBoxAnalyte.SelectedItem.ToString().Equals(comboBoxStandard.SelectedItem.ToString()))
            {
                MessageBox.Show(this, "The analyte and standard cannot be the same");
                return false;
            }
            else if (string.IsNullOrWhiteSpace(tboxUnits.Text))
            {
                MessageBox.Show(this, "Please enter the units label");
                return false;
            }
            else if (string.IsNullOrWhiteSpace(tboxLinearScale.Text))
            {
                MessageBox.Show(this, "Please enter a value for the maximum linear scale");
                return false;
            }
            else if (string.IsNullOrWhiteSpace(tboxLogScale.Text))
            {
                MessageBox.Show(this, "Please enter a value for the maximum log scale");
                return false;
            }
            else if (cboxAuDIT.Checked && string.IsNullOrWhiteSpace(tboxAuDITCVThreshold.Text))
            {
                MessageBox.Show(this, "Please enter a value for the AuDIT CV threshold");
                return false;
            }
            else if (cboxEndogenousCalc.Checked && string.IsNullOrWhiteSpace(tboxEndoConf.Text))
            {
                MessageBox.Show(this, "Please enter a value for the endogenous confidence level");
                return false;
            }
            else if (double.Parse(tboxEndoConf.Text) < 0 || (double.Parse(tboxEndoConf.Text) > 1))
            {
                MessageBox.Show(this, "The endogenous confidence interval must be between 0 and 1");
                return false;
            }
            return true;
        }

        public void GenerateArguments()
        {
            Arguments = new string[Constants.ARGUMENT_COUNT];

            Arguments[(int) ArgumentIndices.concentration_report] = Constants.NULL_STRING;
            Arguments[(int) ArgumentIndices.title] = tboxTitle.Text;
            Arguments[(int) ArgumentIndices.analyte] = comboBoxAnalyte.SelectedItem.ToString();
            Arguments[(int) ArgumentIndices.standard_present] = cboxStandardPresent.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.standard] = comboBoxStandard.SelectedItem.ToString();
            Arguments[(int) ArgumentIndices.units] = tboxUnits.Text;
            Arguments[(int) ArgumentIndices.cv_table] = cboxCVTable.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.calcurves] = cboxCalCurves.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.ntransitions] = numberTransitions.Value.ToString(CultureInfo.InvariantCulture);
            Arguments[(int) ArgumentIndices.lodloq_table] = cboxLODLOQTable.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.lodloq_comp] = cboxLODLOQComp.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.peakplots] = cboxPeakAreaPlots.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.use_par] = cboxPAR.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.max_linear] = tboxLinearScale.Text;
            Arguments[(int) ArgumentIndices.max_log] = tboxLogScale.Text;
            Arguments[(int) ArgumentIndices.perform_audit] = cboxAuDIT.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.audit_threshold] = cboxAuDIT.Checked ? tboxAuDITCVThreshold.Text : Constants.NULL_STRING;
            Arguments[(int) ArgumentIndices.perform_endocalc] = cboxEndogenousCalc.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.endo_ci] = cboxEndogenousCalc.Checked ? tboxEndoConf.Text : Constants.NULL_STRING;
            Arguments[(int) ArgumentIndices.output_prefix] = tboxTitle.Text;
        }
        
        private void btnDefault_Click(object sender, EventArgs e)
        {
            RestoreDefaultValues();
        }

        private void RestoreDefaultValues()
        {
            // generate group box
            cboxCalCurves.Checked = Defaults.CALIBRATION_CURVES;
            cboxCVTable.Checked = Defaults.CV_TABLE;
            cboxLODLOQTable.Checked = Defaults.LODLOQ_TABLE;
            cboxLODLOQComp.Checked = Defaults.LODLOQ_COMPARISON;
            cboxPeakAreaPlots.Checked = Defaults.PEAK_AREA_PLOTS;

            // options group box
            cboxStandardPresent.Checked = Defaults.STANDARD_PRESENT;
            cboxPAR.Checked = Defaults.USE_PAR;
            tboxUnits.Text = Defaults.UNITS;

            // plots group box
            numberTransitions.Value = Defaults.NUMBER_TRANSITIONS;
            tboxLinearScale.Text = Defaults.MAX_LINEAR;
            tboxLogScale.Text = Defaults.MAX_LOG;

            // AuDIT group box
            cboxAuDIT.Checked = Defaults.PERFORM_AUDIT;
            tboxAuDITCVThreshold.Text = Defaults.AUDIT_CV_THRESHOLD;

            // endogenous estimation group box
            cboxEndogenousCalc.Checked = Defaults.PERFORM_ENDOCALC;
            tboxEndoConf.Text = Defaults.ENDOGENOUS_CI;
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

        private void cboxLODLOQTable_CheckedChanged(object sender, EventArgs e)
        {
            if (cboxLODLOQTable.Checked)
            {
                cboxLODLOQComp.Enabled = true;
            }
            else
            {
                cboxLODLOQComp.Enabled = cboxLODLOQComp.Checked = false;
            }
        }

        private void cboxStandardPresent_CheckedChanged(object sender, EventArgs e)
        {
            if (cboxStandardPresent.Checked)
            {
                cboxCalCurves.Enabled = true;
            }
            else
            {
                cboxCalCurves.Enabled = cboxCalCurves.Checked = false;
            }
        }

        private void cboxAuDIT_CheckedChanged(object sender, EventArgs e)
        {
            tboxAuDITCVThreshold.Enabled = cboxAuDIT.Checked;
        }

        private void cboxEndogenousCalc_CheckedChanged(object sender, EventArgs e)
        {
            tboxEndoConf.Enabled = cboxEndogenousCalc.Checked;
        }

    }

    public class QuaSARCollector
    {
        public static string[] CollectArgs(IWin32Window parent, string report, string[] oldArgs)
        {
            // Split report (.csv file) by lines
            string[] lines = report.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            List<string> fields = lines[0].ParseCsvFields().ToList();

            using (var dlg = new QuaSARUI(oldArgs, fields.Where(s => s.EndsWith("Area")))) // Not L10N
            {
                if (parent != null)
                {
                    return (dlg.ShowDialog(parent) == DialogResult.OK) ? dlg.Arguments : null;
                }
                else
                {
                    dlg.StartPosition = FormStartPosition.WindowsDefaultLocation;
                    return (dlg.ShowDialog() == DialogResult.OK) ? dlg.Arguments : null;
                }
            }
        }
    }
}
