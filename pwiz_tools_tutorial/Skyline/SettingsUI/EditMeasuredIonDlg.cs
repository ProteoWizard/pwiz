/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditMeasuredIonDlg : FormEx
    {
        private MeasuredIon _measuredIon;
        private readonly IEnumerable<MeasuredIon> _existing;

        public EditMeasuredIonDlg(IEnumerable<MeasuredIon> existing)
        {
            _existing = existing;

            InitializeComponent();

            // Seems like there should be a way to set this in the properties.
            comboDirection.SelectedIndex = 0;

            Bitmap bm = Resources.PopupBtn;
            bm.MakeTransparent(Color.Fuchsia);
            btnFormulaPopup.Image = bm;

            Height -= ClientRectangle.Height - comboDirection.Bottom - 16;
        }

        public MeasuredIon MeasuredIon
        {
            get { return _measuredIon; }

            set
            {
                _measuredIon = value;
                if (_measuredIon == null)
                {
                    radioFragment.Checked = true;
                    radioReporter.Checked = false;
                    textName.Text = "";
                    textFragment.Text = "";
                    textRestrict.Text = "";
                    comboDirection.SelectedIndex = 0;
                    textMinAas.Text = MeasuredIon.DEFAULT_MIN_FRAGMENT_LENGTH.ToString(CultureInfo.CurrentCulture);
                    textFormula.Text = "";
                    textMonoMass.Text = "";
                    textAverageMass.Text = "";
                }
                else
                {
                    textName.Text = _measuredIon.Name;
                    radioReporter.Checked = !(radioFragment.Checked = _measuredIon.IsFragment);                    
                    SetTextFields();
                }
            }
        }

        private void SetTextFields()
        {
            if (_measuredIon.IsFragment)
            {
                textFragment.Text = _measuredIon.Fragment;
                textRestrict.Text = _measuredIon.Restrict;
                comboDirection.SelectedIndex = (_measuredIon.Terminus == SequenceTerminus.C ? 0 : 1);
                textMinAas.Text = _measuredIon.MinFragmentLength.HasValue
                                      ? _measuredIon.MinFragmentLength.Value.ToString(CultureInfo.CurrentCulture)
                                      : "";
            }
            else if (!string.IsNullOrEmpty(_measuredIon.Formula))
            {
                textFormula.Text = _measuredIon.Formula;
            }
            else
            {
                textFormula.Text = "";
                textMonoMass.Text = (_measuredIon.MonoisotopicMass.HasValue ?
                    _measuredIon.MonoisotopicMass.Value.ToString(CultureInfo.CurrentCulture) : "");
                textAverageMass.Text = (_measuredIon.AverageMass.HasValue ?
                    _measuredIon.AverageMass.Value.ToString(CultureInfo.CurrentCulture) : "");
            }
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(e, textName, out name))
                return;

            if (_existing.Contains(m => !ReferenceEquals(_measuredIon, m) && Equals(name, m.Name)))
            {
                helper.ShowTextBoxError(textName, "The special ion '{0}' already exists.", name);
                return;
            }

            if (radioFragment.Checked)
            {
                string cleavage;
                if (!ValidateAATextBox(helper, textFragment, false, out cleavage))
                    return;
                string restrict;
                if (!ValidateAATextBox(helper, textRestrict, true, out restrict))
                    return;

                SequenceTerminus direction = (comboDirection.SelectedIndex == 0 ?
                    SequenceTerminus.C : SequenceTerminus.N);

                int minAas;
                if (!helper.ValidateNumberTextBox(e, textMinAas, MeasuredIon.MIN_MIN_FRAGMENT_LENGTH,
                        MeasuredIon.MAX_MIN_FRAGMENT_LENGTH, out minAas))
                    return;

                _measuredIon = new MeasuredIon(name, cleavage, restrict, direction, minAas);
            }
            else
            {
                string formula = textFormula.Text;
                double? monoMass = null;
                double? avgMass = null;
                if (!string.IsNullOrEmpty(formula))
                {
                    try
                    {
                        double massMono = SequenceMassCalc.ParseModMass(BioMassCalc.MONOISOTOPIC, formula);
                        double massAverage = SequenceMassCalc.ParseModMass(BioMassCalc.AVERAGE, formula);
                        if (MeasuredIon.MIN_REPORTER_MASS > massMono || MeasuredIon.MIN_REPORTER_MASS > massAverage)
                        {
                            helper.ShowTextBoxError(textFormula, string.Format("Reporter ion masses must be greater than or equal to {0}.", MeasuredIon.MIN_REPORTER_MASS));
                            return;
                        }
                        if (massMono > MeasuredIon.MAX_REPORTER_MASS || massAverage > MeasuredIon.MAX_REPORTER_MASS)
                        {
                            helper.ShowTextBoxError(textFormula, string.Format("Reporter ion masses must be less than or equal to {0}.", MeasuredIon.MAX_REPORTER_MASS));
                            return;
                        }
                    }
                    catch (ArgumentException x)
                    {
                        helper.ShowTextBoxError(textFormula, x.Message);
                        return;
                    }
                }
                else if (!string.IsNullOrEmpty(textMonoMass.Text) ||
                        !string.IsNullOrEmpty(textAverageMass.Text))
                {
                    formula = null;
                    double mass;
                    if (!helper.ValidateDecimalTextBox(e, textMonoMass, MeasuredIon.MIN_REPORTER_MASS, MeasuredIon.MAX_REPORTER_MASS, out mass))
                        return;
                    monoMass = mass;
                    if (!helper.ValidateDecimalTextBox(e, textAverageMass, MeasuredIon.MIN_REPORTER_MASS, MeasuredIon.MAX_REPORTER_MASS, out mass))
                        return;
                    avgMass = mass;
                }
                else
                {
                    helper.ShowTextBoxError(textFormula, "Please specify a formula or constant masses.");
                }
                _measuredIon = new MeasuredIon(name, formula, monoMass, avgMass);
            }

            DialogResult = DialogResult.OK;
        }

        private static bool ValidateAATextBox(MessageBoxHelper helper, TextBox control, bool allowEmpty, out string aaText)
        {
            aaText = control.Text.Trim().ToUpper();
            if (aaText.Length == 0)
            {
                if (!allowEmpty)
                {
                    helper.ShowTextBoxError(control, "{0} must contain at least one amino acid.");
                    return false;
                }
            }
            else
            {
                StringBuilder aaBuilder = new StringBuilder();
                HashSet<char> setAA = new HashSet<char>();
                foreach (char c in aaText)
                {
                    if (!AminoAcid.IsAA(c))
                    {
                        helper.ShowTextBoxError(control, "The character '{0}' is not a valid amino acid.", c);
                        return false;
                    }
                    // Silently strip duplicates.
                    if (!setAA.Contains(c))
                    {
                        aaBuilder.Append(c);
                        setAA.Add(c);
                    }
                }
                aaText = aaBuilder.ToString();
            }
            return true;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void radioFragment_CheckedChanged(object sender, EventArgs e)
        {
            UpdateIonType();
        }

        private void UpdateIonType()
        {
            bool isFragment = radioFragment.Checked;
            textFragment.Enabled = isFragment;
            textRestrict.Enabled = isFragment;
            comboDirection.Enabled = isFragment;
            textMinAas.Enabled = isFragment;
            label1.Enabled = label2.Enabled = label3.Enabled = label4.Enabled = isFragment;
            textFormula.Enabled = !isFragment;
            btnFormulaPopup.Enabled = !isFragment;
            textMonoMass.Enabled = !isFragment;
            textAverageMass.Enabled = !isFragment;
            labelFormula.Enabled = label7.Enabled = label8.Enabled = !isFragment;
            if (isFragment)
            {
                textFormula.Text = textMonoMass.Text = textAverageMass.Text = "";
                if (MeasuredIon != null && MeasuredIon.IsFragment)
                    SetTextFields();
                else
                {
                    comboDirection.SelectedIndex = 0;
                    textMinAas.Text = MeasuredIon.DEFAULT_MIN_FRAGMENT_LENGTH.ToString(CultureInfo.CurrentCulture);
                }
            }
            else
            {
                textFragment.Text = textRestrict.Text = textMinAas.Text = "";
                comboDirection.SelectedIndex = -1;
                if (MeasuredIon != null && !MeasuredIon.IsFragment)
                    SetTextFields();
            }
        }

        private void textAa_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Force uppercase in this control.
            e.KeyChar = char.ToUpper(e.KeyChar);
        }

        private void textFormula_TextChanged(object sender, EventArgs e)
        {
            UpdateMasses();
        }

        private void UpdateMasses()
        {
            string formula = textFormula.Text;
            if (string.IsNullOrEmpty(formula))
            {
                textMonoMass.Text = textAverageMass.Text = "";
                textMonoMass.Enabled = textAverageMass.Enabled = true;
            }
            else
            {
                textMonoMass.Enabled = textAverageMass.Enabled = false;
                try
                {
                    textMonoMass.Text = SequenceMassCalc.ParseModMass(BioMassCalc.MONOISOTOPIC,
                        formula).ToString(CultureInfo.CurrentCulture);
                    textAverageMass.Text = SequenceMassCalc.ParseModMass(BioMassCalc.AVERAGE,
                        formula).ToString(CultureInfo.CurrentCulture);
                    textFormula.ForeColor = Color.Black;
                }
                catch (ArgumentException)
                {
                    textFormula.ForeColor = Color.Red;
                    textMonoMass.Text = textAverageMass.Text = "";
                }
            }
        }

        private void textFormula_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Force uppercase in this control.
            e.KeyChar = char.ToUpper(e.KeyChar);
        }

        private void btnFormulaPopup_Click(object sender, EventArgs e)
        {
            contextFormula.Show(this, panelLossFormula.Left + btnFormulaPopup.Right + 1,
                panelLossFormula.Top + btnFormulaPopup.Top);
        }

        private void AddFormulaSymbol(string symbol)
        {
            textFormula.Text += symbol;
            textFormula.Focus();
            textFormula.SelectionLength = 0;
            textFormula.SelectionStart = textFormula.Text.Length;
        }

        private void hContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.H);
        }

        private void h2ContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.H2);
        }

        private void cContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.C);
        }

        private void c13ContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.C13);
        }

        private void nContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.N);
        }

        private void n15ContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.N15);
        }

        private void oContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.O);
        }

        private void o18ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.O18);
        }

        private void pContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.P);
        }

        private void sContextMenuItem_Click(object sender, EventArgs e)
        {
            AddFormulaSymbol(BioMassCalc.S);
        }
    }
}