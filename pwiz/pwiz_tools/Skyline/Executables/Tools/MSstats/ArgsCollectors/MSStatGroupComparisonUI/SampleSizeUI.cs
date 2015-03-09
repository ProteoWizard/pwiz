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
        private enum Args { normalize_to, samples, peptides, transitions, power, fdr, lower_fold, upper_fold, allow_missing_peaks }

        public string[] Arguments { get; private set; }

        public SampleSizeUi(string[] oldArgs)
        {
            InitializeComponent();

            comboBoxNoramilzeTo.SelectedIndex = 1;

            if (oldArgs != null && oldArgs.Length == 8)
                Arguments = oldArgs;

            // set shift distance based on ititial form layout
            SampleShift = numberSamples.Top - rBtnPeptides.Top;
            PeptideShift = numberPeptides.Top - rBtnTransitions.Top;
            TransitionShift = numberTransitions.Top - rBtnPower.Top;

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
                RestoreCountValue(Args.peptides, rBtnPeptides, numberPeptides, Peptides);
                RestoreCountValue(Args.transitions, rBtnTransitions, numberTransitions, Transitions);

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
            }

            if (!rBtnSamples.Checked && !rBtnPeptides.Checked && !rBtnTransitions.Checked && !rBtnPower.Checked)
            {
                rBtnSamples.Checked = true;
            }

            Height += Math.Min(Math.Min(SampleShift, PeptideShift), TransitionShift) + 5;
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
            Arguments = Arguments ?? new string[9];

            Arguments[(int) Args.normalize_to] = (comboBoxNoramilzeTo.SelectedIndex).ToString(CultureInfo.InvariantCulture);
            Arguments[(int) Args.samples] = (rBtnSamples.Checked) ? Truestring : numberSamples.Value.ToString(CultureInfo.InvariantCulture);
            Arguments[(int) Args.peptides] = (rBtnPeptides.Checked) ? Truestring : numberPeptides.Value.ToString(CultureInfo.InvariantCulture);
            Arguments[(int) Args.transitions] = (rBtnTransitions.Checked) ? Truestring : numberTransitions.Value.ToString(CultureInfo.InvariantCulture); 
            Arguments[(int) Args.power] = (rBtnPower.Checked) ? Truestring : power.ToString(CultureInfo.InvariantCulture);

            Arguments[(int) Args.fdr] = fdr.ToString(CultureInfo.InvariantCulture);
            Arguments[(int) Args.lower_fold] = ldfc.ToString(CultureInfo.InvariantCulture);
            Arguments[(int) Args.upper_fold] = udfc.ToString(CultureInfo.InvariantCulture);
            Arguments[(int) Args.allow_missing_peaks] = (cboxAllowMissingPeaks.Checked) ? Truestring : Falsestring; 
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
        private const decimal Peptides = 1m;
        private const decimal Transitions = 1m;
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
            comboBoxNoramilzeTo.SelectedIndex = Normalize;
            rBtnSamples.Checked = true;
            numberSamples.Value = Samples;
            numberPeptides.Value = Peptides;
            numberTransitions.Value = Transitions;
            numberPower.Text = Power.ToString(CultureInfo.CurrentCulture);
            numberFDR.Text = Fdr.ToString(CultureInfo.CurrentCulture);
            numberLDFC.Text = Ldfc.ToString(CultureInfo.CurrentCulture);
            numberUDFC.Text = Udfc.ToString(CultureInfo.CurrentCulture);
        }

        private int SampleShift { get; set; }
        private int PeptideShift { get; set; }
        private int TransitionShift { get; set; }

        private void rBtnSamples_CheckedChanged(object sender, EventArgs e)
        {
            
            if (rBtnSamples.Checked)
            {
                numberSamples.Enabled = numberSamples.Visible = false;
                rBtnPeptides.Top += SampleShift;
                numberPeptides.Top += SampleShift;
                rBtnTransitions.Top += SampleShift;
                numberTransitions.Top += SampleShift;
                rBtnPower.Top += SampleShift;
                numberPower.Top += SampleShift;
            }
            else
            {
                rBtnPeptides.Top -= SampleShift;
                numberPeptides.Top -= SampleShift;
                rBtnTransitions.Top -= SampleShift;
                numberTransitions.Top -= SampleShift;
                rBtnPower.Top -= SampleShift;
                numberPower.Top -= SampleShift;
                numberSamples.Enabled = numberSamples.Visible = true;
            }
        }

        private void rBtnPeptides_CheckedChanged(object sender, EventArgs e)
        {
            if (rBtnPeptides.Checked)
            {
                numberPeptides.Enabled = numberPeptides.Visible = false;
                rBtnTransitions.Top += PeptideShift;
                numberTransitions.Top += PeptideShift;
                rBtnPower.Top += PeptideShift;
                numberPower.Top += PeptideShift;
            }
            else
            {
                rBtnTransitions.Top -= PeptideShift;
                numberTransitions.Top -= PeptideShift;
                rBtnPower.Top -= PeptideShift;
                numberPower.Top -= PeptideShift;
                numberPeptides.Enabled = numberPeptides.Visible = true;
            }
        }

        private void rBtnTransitions_CheckedChanged(object sender, EventArgs e)
        {
            if (rBtnTransitions.Checked)
            {
                numberTransitions.Enabled = numberTransitions.Visible = false;
                rBtnPower.Top += TransitionShift;
                numberPower.Top += TransitionShift;
            }
            else
            {
                rBtnPower.Top -= TransitionShift;
                numberPower.Top -= TransitionShift;
                numberTransitions.Enabled = numberTransitions.Visible = true;
            }
        }

        private void rBtnPower_CheckedChanged(object sender, EventArgs e)
        {
            numberPower.Enabled = numberPower.Visible = !rBtnPower.Checked;
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
