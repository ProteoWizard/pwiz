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
        private enum Args { samples, peptides, transitions, power, fdr, lower_fold, upper_fold }

        public string[] Arguments { get; private set; }

        public SampleSizeUi(string[] oldArgs)
        {
            InitializeComponent();

            if (oldArgs != null && oldArgs.Length == 7)
                Arguments = oldArgs;

            // set shift distance based on ititial form layout
            SampleShift = numberSamples.Top - rBtnPeptides.Top;
            PeptideShift = numberPeptides.Top - rBtnTransitions.Top;
            TransitionShift = numberTransitions.Top - rBtnPower.Top;

            RestoreValues();
        }

        private const string TRUESTRING = "TRUE";

        private void RestoreValues()
        {
            if (Arguments != null)
            {
                if (Arguments[(int) Args.samples].Equals(TRUESTRING))
                {
                    rBtnSamples.Checked = true;
                }
                else
                {
                    numberSamples.Value = decimal.Parse(Arguments[(int) Args.samples]);
                }

                if (Arguments[(int) Args.peptides].Equals(TRUESTRING))
                {
                    rBtnPeptides.Checked = true;
                }
                else
                {
                    numberPeptides.Value = decimal.Parse(Arguments[(int) Args.peptides]);
                }

                if (Arguments[(int) Args.transitions].Equals(TRUESTRING))
                {
                    rBtnTransitions.Checked = true;
                }
                else
                {
                    numberTransitions.Value = decimal.Parse(Arguments[2]);
                }

                if (Arguments[(int) Args.power].Equals(TRUESTRING))
                {
                    rBtnPower.Checked = true;
                }
                else
                {
                    numberPower.Text = Arguments[(int) Args.power];
                }

                numberFDR.Text = Arguments[(int) Args.fdr];
                numberLDFC.Text = Arguments[(int) Args.lower_fold];
                numberUDFC.Text = Arguments[(int) Args.upper_fold];
            }

            if (!rBtnSamples.Checked && !rBtnPeptides.Checked && !rBtnTransitions.Checked && !rBtnPower.Checked)
            {
                rBtnSamples.Checked = true;
            }

            Height += Math.Min(Math.Min(SampleShift, PeptideShift), TransitionShift) + 5;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private const string PERIOD_STRING = ".";

        private void OkDialog()
        {
            if (!rBtnPower.Checked && string.IsNullOrEmpty(numberPower.Text))
            {
                MessageBox.Show(this, "Please enter a value for Power.");
            }
            else if (string.IsNullOrEmpty(numberFDR.Text) || numberFDR.Text.Equals(PERIOD_STRING))
            {
                MessageBox.Show(this, "Please enter a value for FDR.");
            }
            else if (string.IsNullOrEmpty(numberLDFC.Text) || numberLDFC.Text.Equals(PERIOD_STRING))
            {
                MessageBox.Show(this, "Please enter a value for the lower desired fold change.");
            }
            else if (string.IsNullOrEmpty(numberUDFC.Text) || numberLDFC.Text.Equals(PERIOD_STRING))
            {
                MessageBox.Show(this, "Please enter a value for the upper desired fold change.");
            }
            else if (decimal.Parse(numberUDFC.Text) <= decimal.Parse(numberLDFC.Text))
            {
                MessageBox.Show(this, "The upper desired fold change must be greater than lower desired fold change.");
            } else if ((rBtnPower.Enabled && decimal.Parse(numberPower.Text) < 0) || decimal.Parse(numberFDR.Text) < 0 || 
                       decimal.Parse(numberLDFC.Text) < 0 || decimal.Parse(numberUDFC.Text) < 0)
            {
                MessageBox.Show(this, "Negative values are not valid.");
            }
            else
            {
                GenerateArguments();
                DialogResult = DialogResult.OK;
            }
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
        private void GenerateArguments()
        {
            Arguments = Arguments ?? new string[7];
            
            Arguments[(int) Args.samples] = (rBtnSamples.Checked) ? TRUESTRING : numberSamples.Value.ToString(CultureInfo.InvariantCulture);
            Arguments[(int) Args.peptides] = (rBtnPeptides.Checked) ? TRUESTRING : numberPeptides.Value.ToString(CultureInfo.InvariantCulture);
            Arguments[(int) Args.transitions] = (rBtnTransitions.Checked) ? TRUESTRING : numberTransitions.Value.ToString(CultureInfo.InvariantCulture); 
            Arguments[(int) Args.power] = (rBtnPower.Checked) ? TRUESTRING : numberPower.Text;

            Arguments[(int) Args.fdr] = numberFDR.Text;
            Arguments[(int) Args.lower_fold] = numberLDFC.Text;
            Arguments[(int) Args.upper_fold] = numberUDFC.Text;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void numericTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsDigit(e.KeyChar) && e.KeyChar != '.' && e.KeyChar != (char)Keys.Back)
            {
                e.Handled = true;
            }
            if (e.KeyChar == '.')
            {
                var source = (TextBox) sender;
                if (source.Text.Contains("."))
                    e.Handled = true;
            }
        }

        // defaults
        private const decimal SAMPLES = 2m;
        private const decimal PEPTIDES = 1m;
        private const decimal TRANSITIONS = 1m;
        private const string POWER = "0.80";
        private const string FDR = "0.05";
        private const string LDFC = "1.25";
        private const string UDFC = "1.75";

        private void btnDefault_Click(object sender, EventArgs e)
        {
            RestoreDefaults();
        }

        private void RestoreDefaults()
        {
            rBtnSamples.Checked = true;
            numberSamples.Value = SAMPLES;
            numberPeptides.Value = PEPTIDES;
            numberTransitions.Value = TRANSITIONS;
            numberPower.Text = POWER;
            numberFDR.Text = FDR;
            numberLDFC.Text = LDFC;
            numberUDFC.Text = UDFC;
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
