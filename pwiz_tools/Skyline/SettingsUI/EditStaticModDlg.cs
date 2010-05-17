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
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditStaticModDlg : Form
    {
        private StaticMod _modification;
        private readonly IEnumerable<StaticMod> _existing;

        private readonly BioMassCalc _monoMassCalc = new BioMassCalc(MassType.Monoisotopic);
        private readonly BioMassCalc _averageMassCalc = new BioMassCalc(MassType.Average);

        private readonly bool _heavy;
        private bool _showLoss = true; // Design mode with loss UI showing
        private TextBox _textFormulaActive;

        public EditStaticModDlg(IEnumerable<StaticMod> existing, bool heavy)
        {
            _existing = existing;
            _heavy = heavy;

            InitializeComponent();

            labelChemicalFormula.Visible = !heavy;
            cbChemicalFormula.Visible = heavy;
            cbChemicalFormula.Checked = !heavy || Settings.Default.ShowHeavyFormula;

            Bitmap bm = Resources.PopupBtn;
            bm.MakeTransparent(Color.Fuchsia);
            btnFormulaPopup.Image = bm;
            btnLossFormulaPopup.Image = bm;

            if (heavy)
            {
                
                labelRelativeRT.Left = labelAA.Left;
                comboRelativeRT.Left = comboAA.Left;
                comboRelativeRT.Items.Add(RelativeRT.Matching.ToString());
                comboRelativeRT.Items.Add(RelativeRT.Overlapping.ToString());
                comboRelativeRT.Items.Add(RelativeRT.Preceding.ToString());
                comboRelativeRT.Items.Add(RelativeRT.Unknown.ToString());
                comboRelativeRT.SelectedIndex = 0;
            }
            else
            {
                labelRelativeRT.Visible = false;
                comboRelativeRT.Visible = false;
            }

            ShowLoss = false;
            // TODO: Implement handling of modifications with neutral loss
//            if (heavy)
                btnLoss.Visible = false;
        }

        public StaticMod Modification
        {
            get { return _modification;  }
            set
            {
                _modification = value;
                if (_modification == null)
                {
                    textName.Text = "";
                    comboAA.SelectedIndex = 0;
                    comboTerm.SelectedIndex = 0;
                    textFormula.Text = "";
                    textMonoMass.Text = "";
                    textAverageMass.Text = "";
                    cb13C.Checked = false;
                    cb15N.Checked = false;
                    cb18O.Checked = false;
                    cb2H.Checked = false;
                    textLossFormula.Text = "";
                    textLossMonoMass.Text = "";
                    textLossAverageMass.Text = "";
                    if (comboRelativeRT.Items.Count > 0)
                        comboRelativeRT.SelectedIndex = 0;
                }
                else
                {
                    textName.Text = _modification.Name;
                    if (_modification.AA == null)
                        comboAA.SelectedIndex = 0;
                    else
                        comboAA.SelectedItem = _modification.AA.ToString();
                    if (_modification.Terminus == null)
                        comboTerm.SelectedIndex = 0;
                    else
                        comboTerm.SelectedItem = _modification.Terminus.ToString();
                    if (_modification.Formula != null)
                    {
                        textFormula.Text = _modification.Formula;
                        // Make sure the formula is showing
                        cbChemicalFormula.Checked = true;
                    }
                    else
                    {
                        textFormula.Text = "";
                        textMonoMass.Text = (_modification.MonoisotopicMass == null ?
                            "" : _modification.MonoisotopicMass.ToString());
                        textAverageMass.Text = (_modification.AverageMass == null ?
                            "" : _modification.AverageMass.ToString());
                        // Force the label atom check boxes to show, if any are checked
                        if (_modification.LabelAtoms != LabelAtoms.None)
                            cbChemicalFormula.Checked = false;
                    }

                    cb13C.Checked = _modification.Label13C;
                    cb15N.Checked = _modification.Label15N;
                    cb18O.Checked = _modification.Label18O;
                    cb2H.Checked = _modification.Label2H;

                    if (comboRelativeRT.Items.Count > 0)
                        comboRelativeRT.SelectedItem = _modification.RelativeRT.ToString();

                    if (_modification.FormulaLoss != null)
                    {
                        textLossFormula.Text = _modification.FormulaLoss;
                    }
                    else
                    {
                        textLossFormula.Text = "";
                        textLossMonoMass.Text = (_modification.MonoisotopicLoss == null ?
                            "" : _modification.MonoisotopicLoss.ToString());
                        textLossAverageMass.Text = (_modification.AverageLoss == null ?
                            "" : _modification.AverageLoss.ToString());
                    }
                    // Make sure loss values are showing, if they are present
                    if (!string.IsNullOrEmpty(textLossFormula.Text) ||
                            !string.IsNullOrEmpty(textLossMonoMass.Text) ||
                            !string.IsNullOrEmpty(textLossAverageMass.Text))
                        ShowLoss = true;
                }                
            }
        }

        private LabelAtoms LabelAtoms
        {
            get
            {
                LabelAtoms labelAtoms = LabelAtoms.None;
                if (cb13C.Checked)
                    labelAtoms |= LabelAtoms.C13;
                if (cb15N.Checked)
                    labelAtoms |= LabelAtoms.N15;
                if (cb18O.Checked)
                    labelAtoms |= LabelAtoms.O18;
                if (cb2H.Checked)
                    labelAtoms |= LabelAtoms.H2;
                return labelAtoms;
            }
        }

        public bool ShowLoss
        {
            get { return _showLoss; }
            set
            {
                if (_showLoss == value)
                    return;

                _showLoss = value;

                // Update UI
                panelLossFormula.Visible =
                    labelLossFormula.Visible = 
                    textLossMonoMass.Visible =
                    textLossAverageMass.Visible = _showLoss;

                string btnText = btnLoss.Text;
                btnLoss.Text = btnText.Substring(0, btnText.Length - 2) +
                    (_showLoss ? "<<" : ">>");

                ResizeForLoss();
            }
        }

        private void ResizeForLoss()
        {
            int bottomControl = _heavy ? comboRelativeRT.Bottom : btnLoss.Bottom;

            int delta = textLossMonoMass.Bottom - bottomControl;
            Height += (ShowLoss ? delta : -delta);
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(e, textName, out name))
                return;

            // Allow updating the original modification
            if (Modification == null || !Equals(name, Modification.Name))
            {
                // But not any other existing modification
                foreach (StaticMod mod in _existing)
                {
                    if (Equals(name, mod.Name))
                    {
                        helper.ShowTextBoxError(textName, "The modification '{0}' already exists.", name);
                        e.Cancel = true;
                        return;
                    }
                }
            }

            string aaString = comboAA.SelectedItem.ToString();
            char? aa = null;
            if (!string.IsNullOrEmpty(aaString))
                aa = aaString[0];

            string termString = comboTerm.SelectedItem.ToString();
            ModTerminus? term = null;
            if (!string.IsNullOrEmpty(termString))
                term = (ModTerminus) Enum.Parse(typeof (ModTerminus), termString);

            string formula = null;
            double? monoMass = null;
            double? avgMass = null;
            LabelAtoms labelAtoms = LabelAtoms.None;
            if (cbChemicalFormula.Checked)
                formula = textFormula.Text;
            else
                labelAtoms = LabelAtoms;

            if (!string.IsNullOrEmpty(formula))
            {
                try
                {
                    SequenceMassCalc.ParseModMass(_monoMassCalc, formula);
                }
                catch (ArgumentException x)
                {
                    helper.ShowTextBoxError(textFormula, x.Message);
                    e.Cancel = true;
                    return;
                }
            }
            else if (labelAtoms == LabelAtoms.None)
            {
                formula = null;
                double mass;
                if (!helper.ValidateDecimalTextBox(e, textMonoMass, -1500, 1500, out mass))
                    return;
                monoMass = mass;
                if (!helper.ValidateDecimalTextBox(e, textAverageMass, -1500, 1500, out mass))
                    return;
                avgMass = mass;
            }
            else if (!aa.HasValue && term.HasValue)
            {
                MessageBox.Show(this, "Labeled atoms on terminal modification are not valid.", Program.Name);
                e.Cancel = true;
                return;
            }

            string formulaLoss = null;
            double? monoLoss = null;
            double? avgLoss = null;
            if (textLossFormula.Visible)
            {
                formulaLoss = textLossFormula.Text;
                if (!string.IsNullOrEmpty(formulaLoss))
                {
                    try
                    {
                        SequenceMassCalc.ParseModMass(_monoMassCalc, formulaLoss);
                    }
                    catch (ArgumentException x)
                    {
                        helper.ShowTextBoxError(textLossFormula, x.Message);
                        e.Cancel = true;
                        return;
                    }                    
                }
                else if (!string.IsNullOrEmpty(textLossMonoMass.Text) ||
                        !string.IsNullOrEmpty(textLossAverageMass.Text))
                {
                    formulaLoss = null;
                    double mass;
                    if (!helper.ValidateDecimalTextBox(e, textLossMonoMass, -200, 0, out mass))
                        return;
                    monoLoss = mass;
                    if (!helper.ValidateDecimalTextBox(e, textAverageMass, -200, 0, out mass))
                        return;
                    avgLoss = mass;
                }
            }

            RelativeRT relativeRT = RelativeRT.Matching;
            if (comboRelativeRT.Visible && comboRelativeRT.SelectedItem != null)
            {
                relativeRT = (RelativeRT)Enum.Parse(typeof(RelativeRT),
                    comboRelativeRT.SelectedItem.ToString());
            }

            Modification = new StaticMod(name,
                                         aa,
                                         term,
                                         formula,
                                         labelAtoms,
                                         relativeRT,
                                         monoMass,
                                         avgMass,
                                         formulaLoss,
                                         monoLoss,
                                         avgLoss);

            // Store state of the chemical formula checkbox for next use.
            if (cbChemicalFormula.Visible)
                Settings.Default.ShowHeavyFormula = panelFormula.Visible;

            DialogResult = DialogResult.OK;
            Close();            
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void textFormula_TextChanged(object sender, EventArgs e)
        {
            UpdateMasses();
        }

        private void cb13C_CheckedChanged(object sender, EventArgs e)
        {
            UpdateMasses();
        }

        private void cb15N_CheckedChanged(object sender, EventArgs e)
        {
            UpdateMasses();
        }

        private void cb18O_CheckedChanged(object sender, EventArgs e)
        {
            UpdateMasses();
        }

        private void cb2H_CheckedChanged(object sender, EventArgs e)
        {
            UpdateMasses();
        }

        private void comboAA_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateMasses();
        }

        private void cbChemicalFormula_CheckedChanged(object sender, EventArgs e)
        {
            panelFormula.Visible = cbChemicalFormula.Checked;
            panelAtoms.Visible = !cbChemicalFormula.Checked;
            UpdateMasses();
        }

        private void UpdateMasses()
        {
            string formula = null;
            LabelAtoms labelAtoms = LabelAtoms.None;
            if (cbChemicalFormula.Checked)
                formula = textFormula.Text;
            else
            {
                labelAtoms = LabelAtoms;
                string aaString = comboAA.SelectedItem.ToString();
                if (!string.IsNullOrEmpty(aaString) && labelAtoms != LabelAtoms.None)
                    formula = SequenceMassCalc.GetHeavyFormula(aaString[0], labelAtoms);
            }

            if (string.IsNullOrEmpty(formula))
            {
                textMonoMass.Text = textAverageMass.Text = "";
                textMonoMass.Enabled = textAverageMass.Enabled = (labelAtoms == LabelAtoms.None);
            }
            else
            {
                textMonoMass.Enabled = textAverageMass.Enabled = false;
                try
                {
                    textMonoMass.Text = SequenceMassCalc.ParseModMass(_monoMassCalc, formula).ToString();
                    textAverageMass.Text = SequenceMassCalc.ParseModMass(_averageMassCalc, formula).ToString();
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

        private void btnLoss_Click(object sender, EventArgs e)
        {
            ShowLoss = !ShowLoss;
        }

        private void textLossFormula_TextChanged(object sender, EventArgs e)
        {
            UpdateLosses();
        }

        private void UpdateLosses()
        {
            string formula = textLossFormula.Text;
            if (string.IsNullOrEmpty(formula))
            {
                textLossMonoMass.Text = textLossAverageMass.Text = "";
                textLossMonoMass.Enabled = textLossAverageMass.Enabled = true;
            }
            else
            {
                textLossMonoMass.Enabled = textLossAverageMass.Enabled = false;
                try
                {
                    textLossMonoMass.Text = "-" + SequenceMassCalc.ParseModMass(_monoMassCalc, formula);
                    textLossAverageMass.Text = "-" + SequenceMassCalc.ParseModMass(_averageMassCalc, formula);
                    textLossFormula.ForeColor = Color.Black;
                }
                catch (ArgumentException)
                {
                    textLossFormula.ForeColor = Color.Red;
                    textLossMonoMass.Text = textLossAverageMass.Text = "";
                }
            }
        }

        private void textLossFormula_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Force uppercase in this control.
            e.KeyChar = char.ToUpper(e.KeyChar);
        }

        private void btnFormulaPopup_Click(object sender, EventArgs e)
        {
            _textFormulaActive = textFormula;
            contextFormula.Show(this, panelFormula.Left + btnFormulaPopup.Right + 1,
                panelFormula.Top + btnFormulaPopup.Top);
        }

        private void btnLossFormulaPopup_Click(object sender, EventArgs e)
        {
            _textFormulaActive = textLossFormula;
            contextFormula.Show(this, panelLossFormula.Left + btnLossFormulaPopup.Right + 1,
                panelLossFormula.Top + btnLossFormulaPopup.Top);
        }

        private void AddFormulaSymbol(string symbol)
        {
            if (_textFormulaActive == null)
                return;
            _textFormulaActive.Text += symbol;
            _textFormulaActive.Focus();
            _textFormulaActive.SelectionLength = 0;
            _textFormulaActive.SelectionStart = textFormula.Text.Length;
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

        private void textLossMonoMass_TextChanged(object sender, EventArgs e)
        {
            ForceNegativeValue(textLossMonoMass);
        }

        private void textLossAverageMass_TextChanged(object sender, EventArgs e)
        {
            ForceNegativeValue(textLossAverageMass);
        }

        private static void ForceNegativeValue(Control textMassValue)
        {
            string massText = textMassValue.Text;
            if (!string.IsNullOrEmpty(massText) && massText[0] != '-')
                textMassValue.Text = "-" + massText;            
        }
    }
}
