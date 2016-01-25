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
using System.Globalization;
using System.Windows.Forms;

namespace MSStatArgsCollector
{
    public partial class SampleSizeUi : Form
    {
        private enum Args { normalize_to, samples, 
            power, fdr, lower_fold, upper_fold, allow_missing_peaks,
            feature_selection,remove_interfered_proteins,max_arg
        }

        public string[] Arguments { get; private set; }

        public SampleSizeUi(string[] oldArgs)
        {
            InitializeComponent();
            comboNormalizeTo.SelectedIndex = 1;

            if (oldArgs != null && oldArgs.Length == (int) Args.max_arg)
                Arguments = oldArgs;

            RestoreValues();
        }

        private const string Truestring = "TRUE"; // Not L10N
        private const string Falsestring = "FALSE"; // Not L10N

        private void RestoreValues()
        {
            if (Arguments == null)
                RestoreDefaults();
            else
            {
                RestoreCountValue(Args.samples, rBtnSamples, numberSamples, Samples);

                string valueText = Arguments[(int) Args.power];
                if (valueText.Equals(Truestring))
                {
                    rBtnPower.Checked = true;
                }
                else
                {
                    RestoreDecimalText(valueText, numberPower, Power);
                }

                RestoreDecimalText(Arguments[(int)Args.fdr], numberFDR, Fdr);
                RestoreDecimalText(Arguments[(int)Args.lower_fold], numberLDFC, Ldfc);
                RestoreDecimalText(Arguments[(int)Args.upper_fold], numberUDFC, Udfc);
                cbxSelectHighQualityFeatures.Checked = Truestring == Arguments[(int) Args.feature_selection];
                cbxRemoveInterferedProteins.Checked = Truestring == Arguments[(int) Args.remove_interfered_proteins];
            }

            if (!rBtnSamples.Checked && !rBtnPower.Checked)
            {
                rBtnSamples.Checked = true;
            }
        }

        private void RestoreCountValue(Args argument, RadioButton radio, NumericUpDown numeric, decimal defaultValue)
        {
            int count;
            string argText = Arguments[(int) argument];
            if (argText.Equals(Truestring))
            {
                radio.Checked = true;
            }
            else if (int.TryParse(argText, NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
            {
                numeric.Value = count;
            }
            else
            {
                numeric.Value = defaultValue;
            }
        }

        private void RestoreDecimalText(string valueText, TextBox textBox, decimal defaultValue)
        {
            decimal decimalValue;
            if (!decimal.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out decimalValue))
                decimalValue = defaultValue;
            textBox.Text = decimalValue.ToString(CultureInfo.CurrentCulture);
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
            Arguments = new string[(int) Args.max_arg];

            Arguments[(int) Args.normalize_to] = (comboNormalizeTo.SelectedIndex).ToString(CultureInfo.InvariantCulture);
            Arguments[(int) Args.samples] = (rBtnSamples.Checked) ? Truestring : numberSamples.Value.ToString(CultureInfo.InvariantCulture);
            Arguments[(int) Args.power] = (rBtnPower.Checked) ? Truestring : power.ToString(CultureInfo.InvariantCulture);

            Arguments[(int) Args.fdr] = fdr.ToString(CultureInfo.InvariantCulture);
            Arguments[(int) Args.lower_fold] = ldfc.ToString(CultureInfo.InvariantCulture);
            Arguments[(int) Args.upper_fold] = udfc.ToString(CultureInfo.InvariantCulture);
            Arguments[(int) Args.allow_missing_peaks] = (cboxAllowMissingPeaks.Checked) ? Truestring : Falsestring;
            Arguments[(int) Args.feature_selection] =
                cbxSelectHighQualityFeatures.Checked ? Truestring : Falsestring;
            Arguments[(int) Args.remove_interfered_proteins] =
                cbxRemoveInterferedProteins.Checked ? Truestring : Falsestring;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
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

        // defaults
        private const int Normalize = 1;
        private const decimal Samples = 2m;
        private const decimal Power = 0.80m;
        private const decimal Fdr = 0.05m;
        private const decimal Ldfc = 1.25m;
        private const decimal Udfc = 1.75m;

        private void btnDefault_Click(object sender, EventArgs e)
        {
            RestoreDefaults();
        }

        private void RestoreDefaults()
        {
            comboNormalizeTo.SelectedIndex = Normalize;
            rBtnSamples.Checked = true;
            numberSamples.Value = Samples;
            numberPower.Text = Power.ToString(CultureInfo.CurrentCulture);
            numberFDR.Text = Fdr.ToString(CultureInfo.CurrentCulture);
            numberLDFC.Text = Ldfc.ToString(CultureInfo.CurrentCulture);
            numberUDFC.Text = Udfc.ToString(CultureInfo.CurrentCulture);
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

        private void cbxSelectHighQualityFeatures_CheckedChanged(object sender, EventArgs e)
        {
            cbxRemoveInterferedProteins.Enabled = cbxSelectHighQualityFeatures.Checked;
        }
    }

    public class MSstatsSampleSizeCollector
    {
        public static string[] CollectArgs(IWin32Window parent, string report, string[] args)
        {
            using (var dlg = new SampleSizeUi(args))
            {
                if (parent != null)
                {
                    return (dlg.ShowDialog(parent) == DialogResult.OK) ? dlg.Arguments : null;
                }
                return (dlg.ShowDialog() == DialogResult.OK) ? dlg.Arguments : null;
            }
        }
    }
}
