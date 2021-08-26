/*
 * Original author: Trevor Killeen <killeent .at. uw.edu>,
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
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MSStatArgsCollector
{
    public partial class SampleSizeUi : ArgsCollectorForm
    {
        private enum Args { normalize_to, samples, feature_selection,
            power, fdr, lower_fold, upper_fold, max_arg
        }

        public string[] Arguments { get; private set; }

        public SampleSizeUi(string[] oldArgs)
        {
            InitializeComponent();
            comboNormalizeTo.Items.AddRange(GetNormalizationOptionLabels().Cast<object>().ToArray());
            RestoreDefaults();
            RestoreArguments(oldArgs);
        }

        private const string Truestring = "TRUE"; // Not L10N
        
        private void RestoreArguments(IList<string> arguments)
        {
            if (arguments == null || arguments.Count != (int) Args.max_arg)
            {
                return;
            }
            SelectComboBoxValue(comboNormalizeTo, arguments[(int) Args.normalize_to], _normalizationOptionValues);
            RestoreCountValue(arguments[(int) Args.samples], rBtnSamples, numberSamples);

            string valueText = arguments[(int) Args.power];
            if (valueText.Equals(Truestring))
            {
                rBtnPower.Checked = true;
            }
            else
            {
                RestoreDecimalText(valueText, numberPower);
            }

            RestoreDecimalText(arguments[(int)Args.fdr], numberFDR);
            RestoreDecimalText(arguments[(int)Args.lower_fold], numberLDFC);
            RestoreDecimalText(arguments[(int)Args.upper_fold], numberUDFC);
            cbxSelectHighQualityFeatures.Checked = FeatureSubsetHighQuality== arguments[(int) Args.feature_selection];
        }

        private void RestoreCountValue(string argText, RadioButton radio, NumericUpDown numeric)
        {
            int count;
            if (argText.Equals(Truestring))
            {
                radio.Checked = true;
            }
            else if (int.TryParse(argText, NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
            {
                numeric.Value = count;
            }
        }

        private void RestoreDecimalText(string valueText, TextBox textBox)
        {
            if (decimal.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decimalValue))
            {
                textBox.Text = decimalValue.ToString(CultureInfo.CurrentCulture);
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void OkDialog()
        {
            var decimalPoint = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            if (!rBtnPower.Checked && string.IsNullOrEmpty(numberPower.Text))
            {
                MessageBox.Show(this, MSstatsResources.SampleSizeUi_OkDialog_Please_enter_a_value_for_Power_);
            }
            else if (string.IsNullOrEmpty(numberFDR.Text) || numberFDR.Text.Equals(decimalPoint))
            {
                MessageBox.Show(this, MSstatsResources.SampleSizeUi_OkDialog_Please_enter_a_value_for_FDR_);
            }
            else if (string.IsNullOrEmpty(numberLDFC.Text) || numberLDFC.Text.Equals(decimalPoint))
            {
                MessageBox.Show(this, MSstatsResources.SampleSizeUi_OkDialog_Please_enter_a_value_for_the_lower_desired_fold_change_);
            }
            else if (string.IsNullOrEmpty(numberUDFC.Text) || numberLDFC.Text.Equals(decimalPoint))
            {
                MessageBox.Show(this, MSstatsResources.SampleSizeUi_OkDialog_Please_enter_a_value_for_the_upper_desired_fold_change_);
            }
            else
            {
                decimal decimalPower, decimalFdr, decimalLdfc, decimalUdfc;
                if (!ValidateNumber(numberPower, out decimalPower) ||
                    !ValidateNumber(numberFDR, out decimalFdr) ||
                    !ValidateNumber(numberLDFC, out decimalLdfc) ||
                    !ValidateNumber(numberUDFC, out decimalUdfc))
                {
                    return;
                }
                if (decimalUdfc <= decimalLdfc)
                {
                    MessageBox.Show(this, MSstatsResources.SampleSizeUi_OkDialog_The_upper_desired_fold_change_must_be_greater_than_lower_desired_fold_change_);
                }
                else
                {
                    GenerateArguments(decimalPower, decimalFdr, decimalLdfc, decimalUdfc);
                    DialogResult = DialogResult.OK;
                }
            }
        }

        private bool ValidateNumber(TextBox textBox, out decimal decimalPower)
        {
            if (!decimal.TryParse(textBox.Text, out decimalPower))
            {
                MessageBox.Show(this, string.Format(MSstatsResources.SampleSizeUi_ValidateNumber_The_number___0___is_not_valid_, textBox.Text));
                numberPower.Focus();
                return false;
            }
            if (decimalPower < 0)
            {
                MessageBox.Show(this, MSstatsResources.SampleSizeUi_OkDialog_Negative_values_are_not_valid_);
                numberPower.Focus();
                return false;
            }
            return true;
        }

        // The command line arguments generated by the Sample Size UI is a 7 element array
        // consisting of:
        //
        // 0. The integer number of samples desired (or "TRUE" if we want this to be calculated)
        // 1. The integer number of peptides per protein (or "TRUE")
        // 2. The integer number of transition per peptides (or "TRUE")
        // 3. The power (or "TRUE")
        // 4. The FDR
        // 5. The lower desired fold change 
        // 6. The upper desired fold change
        //
        private void GenerateArguments(decimal power, decimal fdr, decimal ldfc, decimal udfc)
        {
            var arguments = new List<string>();
            arguments.Add(_normalizationOptionValues[comboNormalizeTo.SelectedIndex]);
            arguments.Add(cbxSelectHighQualityFeatures.Checked ? FeatureSubsetHighQuality : FeatureSubsetAll);
            arguments.Add(rBtnSamples.Checked ? Truestring : numberSamples.Value.ToString(CultureInfo.InvariantCulture));
            arguments.Add(rBtnPower.Checked ? Truestring : power.ToString(CultureInfo.InvariantCulture));
            arguments.Add(fdr.ToString(CultureInfo.InvariantCulture));
            arguments.Add(ldfc.ToString(CultureInfo.InvariantCulture));
            arguments.Add(udfc.ToString(CultureInfo.InvariantCulture));
            Arguments = arguments.ToArray();
        }

        private void numericTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            var decimalPoint = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            bool decimalKey = decimalPoint.IndexOf(e.KeyChar) != -1;
            if (!char.IsDigit(e.KeyChar) && !decimalKey && e.KeyChar != (char)Keys.Back)
            {
                e.Handled = true;
            }
            if (decimalKey)
            {
                var source = (TextBox) sender;
                if (source.Text.Contains(decimalPoint)) // Not L10N
                    e.Handled = true;
            }
        }

        private void btnDefault_Click(object sender, EventArgs e)
        {
            RestoreDefaults();
        }

        private void RestoreDefaults()
        {
            comboNormalizeTo.SelectedIndex = 1;
            rBtnSamples.Checked = true;
            numberSamples.Value = 2m;
            numberPower.Text = 0.80m.ToString(CultureInfo.CurrentCulture);
            numberFDR.Text = 0.05m.ToString(CultureInfo.CurrentCulture);
            numberLDFC.Text = 1.25m.ToString(CultureInfo.CurrentCulture);
            numberUDFC.Text = 1.75m.ToString(CultureInfo.CurrentCulture);
        }

        private void rBtnSamples_CheckedChanged(object sender, EventArgs e)
        {
            if (rBtnSamples.Checked)
            {
                numberSamples.Enabled = numberSamples.Visible = false;
            }
            else
            {
                numberSamples.Enabled = numberSamples.Visible = true;
            }
        }

        private void rBtnPower_CheckedChanged(object sender, EventArgs e)
        {
            numberPower.Enabled = numberPower.Visible = !rBtnPower.Checked;
        }
    }

    public class MSstatsSampleSizeCollector
    {
        public static string[] CollectArgs(IWin32Window parent, TextReader report, string[] args)
        {
            using (var dlg = new SampleSizeUi(args))
            {
                if (dlg.ShowDialog(parent) == DialogResult.OK)
                {
                    return dlg.Arguments;
                }

                return null;
            }
        }
    }
}
