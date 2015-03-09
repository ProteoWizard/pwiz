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
            RestoreValues();
        }

        private void RestoreValues()
        {
            numberTransitions.SelectedIndex = Defaults.NUMBER_TRANSITIONS - 1;
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
            if (Areas.Count() == 1)
            {
                // if there is only area available, make it the analyte and set the standard to "None" and
                // disable its combobox
                comboBoxAnalyte.DataSource = new List<string>(Areas);
                comboBoxStandard.DataSource = new[] {Constants.NONE_STRING};
                comboBoxStandard.Enabled = false;
            }
            else
            {
                comboBoxAnalyte.DataSource = new List<string>(Areas);
                comboBoxStandard.DataSource = new List<string>(Areas.Concat(new [] {Constants.NONE_STRING}));
                // make the standard a different value than the analyte so that the tool can run without the user
                // having to make any changes at all
                comboBoxStandard.SelectedIndex = 1;
            }
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
                cboxPAR.Checked = Arguments[(int) ArgumentIndices.use_par].Equals(Constants.TRUE_STRING);
                comboBoxAnalyte.SelectedItem = comboBoxAnalyte.Items.Contains(Arguments[(int) ArgumentIndices.analyte])
                                                   ? Arguments[(int) ArgumentIndices.analyte]
                                                   : comboBoxAnalyte.Items[0];
                comboBoxStandard.SelectedItem = comboBoxStandard.Enabled && comboBoxStandard.Items.Contains(Arguments[(int)ArgumentIndices.standard])
                                                   ? Arguments[(int)ArgumentIndices.standard]
                                                   : comboBoxStandard.Items[0];

                tboxUnits.Text = Arguments[(int) ArgumentIndices.units];

                // plots
                numberTransitions.SelectedItem = int.Parse(Arguments[(int)ArgumentIndices.ntransitions]);
                tboxLinearScale.Text = Arguments[(int)ArgumentIndices.max_linear];
                tboxLogScale.Text = Arguments[(int)ArgumentIndices.max_log];

                // AuDIT
                cboxAuDIT.Checked = Arguments[(int)ArgumentIndices.perform_audit].Equals(Constants.TRUE_STRING);
                tboxAuDITCVThreshold.Text =
                    Arguments[(int) ArgumentIndices.audit_threshold].Equals(Constants.NULL_STRING)
                        ? Defaults.AUDIT_CV_THRESHOLD.ToString(CultureInfo.CurrentCulture)
                        : InvariantDecimalToLocal(Arguments[(int) ArgumentIndices.audit_threshold]);

                // endogenous estimation
                cboxEndogenousCalc.Checked = Arguments[(int)ArgumentIndices.perform_endocalc].Equals(Constants.TRUE_STRING);
                tboxEndoConf.Text = Arguments[(int) ArgumentIndices.endo_ci].Equals(Constants.NULL_STRING)
                                        ? Defaults.ENDOGENOUS_CI.ToString(CultureInfo.CurrentCulture)
                                        : InvariantDecimalToLocal(Arguments[(int) ArgumentIndices.endo_ci]);
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
                MessageBox.Show(this, QuaSARResources.QuaSARUI_VerifyArguments_Please_enter_a_title);
                return false;
            } else if (comboBoxAnalyte.SelectedItem.ToString().Equals(comboBoxStandard.SelectedItem.ToString()))
            {
                MessageBox.Show(this, QuaSARResources.QuaSARUI_VerifyArguments_The_analyte_and_standard_cannot_be_the_same);
                return false;
            }
            else if (string.IsNullOrWhiteSpace(tboxUnits.Text))
            {
                MessageBox.Show(this, QuaSARResources.QuaSARUI_VerifyArguments_Please_enter_the_units_label);
                return false;
            }
            else if (string.IsNullOrWhiteSpace(tboxLinearScale.Text))
            {
                MessageBox.Show(this, QuaSARResources.QuaSARUI_VerifyArguments_Please_enter_a_value_for_the_maximum_linear_scale);
                return false;
            }
            else if (string.IsNullOrWhiteSpace(tboxLogScale.Text))
            {
                MessageBox.Show(this, QuaSARResources.QuaSARUI_VerifyArguments_Please_enter_a_value_for_the_maximum_log_scale);
                return false;
            }
            else if (cboxAuDIT.Checked && string.IsNullOrWhiteSpace(tboxAuDITCVThreshold.Text))
            {
                MessageBox.Show(this, QuaSARResources.QuaSARUI_VerifyArguments_Please_enter_a_value_for_the_AuDIT_CV_threshold);
                return false;
            }
            else if (cboxEndogenousCalc.Checked && string.IsNullOrWhiteSpace(tboxEndoConf.Text))
            {
                MessageBox.Show(this, QuaSARResources.QuaSARUI_VerifyArguments_Please_enter_a_value_for_the_endogenous_confidence_level);
                return false;
            }
            else if (Decimal.Parse(tboxEndoConf.Text, NumberStyles.Float, CultureInfo.CurrentCulture) < 0 || (Decimal.Parse(tboxEndoConf.Text, NumberStyles.Float, CultureInfo.CurrentCulture) > 1))
            {
                MessageBox.Show(this, QuaSARResources.QuaSARUI_VerifyArguments_The_endogenous_confidence_interval_must_be_between_0_and_1);
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
            Arguments[(int) ArgumentIndices.standard_present] = comboBoxStandard.SelectedItem.Equals(Constants.NONE_STRING) ? Constants.FALSE_STRING : Constants.TRUE_STRING;
            Arguments[(int) ArgumentIndices.standard] = comboBoxStandard.SelectedItem.Equals(Constants.NONE_STRING) ? Constants.NULL_STRING : comboBoxStandard.SelectedItem.ToString();
            Arguments[(int) ArgumentIndices.units] = tboxUnits.Text;
            Arguments[(int) ArgumentIndices.cv_table] = cboxCVTable.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.calcurves] = cboxCalCurves.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.ntransitions] = numberTransitions.SelectedIndex.ToString(CultureInfo.InvariantCulture) + 1;
            Arguments[(int) ArgumentIndices.lodloq_table] = cboxLODLOQTable.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.lodloq_comp] = cboxLODLOQComp.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.peakplots] = cboxPeakAreaPlots.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.use_par] = cboxPAR.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int) ArgumentIndices.max_linear] = tboxLinearScale.Text;
            Arguments[(int) ArgumentIndices.max_log] = tboxLogScale.Text;
            Arguments[(int) ArgumentIndices.perform_audit] = cboxAuDIT.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int)ArgumentIndices.audit_threshold] = cboxAuDIT.Checked ? Decimal.Parse(tboxAuDITCVThreshold.Text, CultureInfo.CurrentCulture).ToString(CultureInfo.InvariantCulture) : Constants.NULL_STRING;
            Arguments[(int) ArgumentIndices.perform_endocalc] = cboxEndogenousCalc.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
            Arguments[(int)ArgumentIndices.endo_ci] = cboxEndogenousCalc.Checked ? Decimal.Parse(tboxEndoConf.Text.ToString(CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture) : Constants.NULL_STRING;
            Arguments[(int) ArgumentIndices.output_prefix] = tboxTitle.Text;
            Arguments[(int)ArgumentIndices.create_individual_plots] = cboxGraphPlot.Checked ? Constants.TRUE_STRING : Constants.FALSE_STRING;
        }

        private string LocalDecimalToInvariant(string text)
        {
            return Decimal.Parse(text, CultureInfo.CurrentCulture).ToString(CultureInfo.InvariantCulture);
        }

        private string InvariantDecimalToLocal(string text)
        {
            return Decimal.Parse(text, CultureInfo.InvariantCulture).ToString(CultureInfo.CurrentCulture);
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
            cboxPAR.Checked = Defaults.USE_PAR;
            tboxUnits.Text = Defaults.UNITS;
            PopulateAnalyteAndStandard();

            // plots group box
            numberTransitions.SelectedIndex = Defaults.NUMBER_TRANSITIONS - 1;
            cboxGraphPlot.Checked = Defaults.GRAPH_PLOT;
            tboxLinearScale.Text = Defaults.MAX_LINEAR.ToString(CultureInfo.CurrentCulture);
            tboxLogScale.Text = Defaults.MAX_LOG.ToString(CultureInfo.CurrentCulture);

            // AuDIT group box
            cboxAuDIT.Checked = Defaults.PERFORM_AUDIT;
            tboxAuDITCVThreshold.Text = Defaults.AUDIT_CV_THRESHOLD.ToString(CultureInfo.CurrentCulture);

            // endogenous estimation group box
            cboxEndogenousCalc.Checked = Defaults.PERFORM_ENDOCALC;
            tboxEndoConf.Text = Defaults.ENDOGENOUS_CI.ToString(CultureInfo.CurrentCulture);
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
                if (source.Text.Contains(".")) // Not L10N
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
            if (lines.Length < 2)
            {
                MessageBox.Show(QuaSARResources.QuaSARCollector_CollectArgs_QuaSAR_requires_peak_area_values___The_document_must_have_imported_data_);
                return null;
            }
            var fields = lines[0].ParseCsvFields().ToList();
            var areas = fields.Where(s => s.EndsWith("Area")).ToList(); // Not L10N
            if (areas.Count == 0)
            {
                MessageBox.Show(QuaSARResources.QuaSARCollector_CollectArgs_QuaSAR_requires_peak_area_values___Input_report_format_may_be_incorrect_);
                return null;
            }
            if (areas.Count < 2)
            {
                MessageBox.Show(QuaSARResources.QuaSARCollector_CollectArgs_QuaSAR_requires_peak_areas_for_multiple_label_types_);
                return null;
            }

            using (var dlg = new QuaSARUI(oldArgs, areas))
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
