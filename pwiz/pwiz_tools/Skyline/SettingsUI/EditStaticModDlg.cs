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
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
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

        private readonly bool _heavy;
        private bool _showLoss = true; // Design mode with loss UI showing

        public EditStaticModDlg(IEnumerable<StaticMod> existing, bool heavy)
        {
            _existing = existing;
            _heavy = heavy;

            InitializeComponent();

            cbVariableMod.Visible = !heavy;
            labelChemicalFormula.Visible = !heavy;
            cbChemicalFormula.Visible = heavy;
            cbChemicalFormula.Checked = !heavy || Settings.Default.ShowHeavyFormula;

            Bitmap bm = Resources.PopupBtn;
            bm.MakeTransparent(Color.Fuchsia);
            btnFormulaPopup.Image = bm;

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
            if (heavy)
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
                    comboAA.Text = "";
                    comboTerm.SelectedIndex = 0;
                    cbVariableMod.Checked = false;
                    textFormula.Text = "";
                    textMonoMass.Text = "";
                    textAverageMass.Text = "";
                    cb13C.Checked = false;
                    cb15N.Checked = false;
                    cb18O.Checked = false;
                    cb2H.Checked = false;
                    listNeutralLosses.Items.Clear();
                    if (comboRelativeRT.Items.Count > 0)
                        comboRelativeRT.SelectedIndex = 0;
                }
                else
                {
                    textName.Text = _modification.Name;
                    comboAA.Text = _modification.AAs ?? "";
                    if (_modification.Terminus == null)
                        comboTerm.SelectedIndex = 0;
                    else
                        comboTerm.SelectedItem = _modification.Terminus.ToString();
                    cbVariableMod.Checked = _modification.IsVariable;
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

                    if (_modification.HasLoss)
                    {
                        foreach (var loss in _modification.Losses)
                            listNeutralLosses.Items.Add(loss);
                    }
                    // Make sure loss values are showing, if they are present
                    if (listNeutralLosses.Items.Count > 0)
                        ShowLoss = true;
                }                
            }
        }

        public IEnumerable<FragmentLoss> Losses
        {
            get
            {
                foreach (FragmentLoss loss in listNeutralLosses.Items)
                    yield return loss;
            }

            set
            {
                var losses = FragmentLoss.SortByMz(value.ToArray());
                listNeutralLosses.Items.Clear();
                listNeutralLosses.Items.AddRange(losses.ToArray());
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
                panelLoss.Visible =
                    labelLoss.Visible = _showLoss;

                string btnText = btnLoss.Text;
                btnLoss.Text = btnText.Substring(0, btnText.Length - 2) +
                    (_showLoss ? "<<" : ">>");

                ResizeForLoss();
            }
        }

        private void ResizeForLoss()
        {
            int bottomControl = _heavy ? comboRelativeRT.Bottom : btnLoss.Bottom;

            int delta = panelLoss.Bottom - bottomControl;
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
                        helper.ShowTextBoxError(e, textName, "The modification '{0}' already exists.", name);
                        return;
                    }
                }
            }

            ValidateAACombo();
            string aas = comboAA.Text;
            if (string.IsNullOrEmpty(aas))
                aas = null;
            else
            {
                // Use the cleanest possible format.
                var sb = new StringBuilder();
                foreach (string aaPart in aas.Split(SEPARATOR_AA))
                {
                    string aa = aaPart.Trim();
                    if (aa.Length == 0)
                        continue;
                    if (sb.Length > 0)
                        sb.Append(", ");
                    sb.Append(aa);
                }
            }

            string termString = comboTerm.SelectedItem.ToString();
            ModTerminus? term = null;
            if (!string.IsNullOrEmpty(termString))
                term = (ModTerminus) Enum.Parse(typeof (ModTerminus), termString);

            if (cbVariableMod.Checked && aas == null && term == null)
            {
                MessageBox.Show("Variable modifications must specify amino acid or terminus.", Program.Name);
                comboAA.Focus();
                return;
            }

            string formula = null;
            double? monoMass = null;
            double? avgMass = null;
            LabelAtoms labelAtoms = LabelAtoms.None;
            if (cbChemicalFormula.Checked)
                formula = textFormula.Text;
            else
                labelAtoms = LabelAtoms;

            // Get the losses to know whether any exist below
            IList<FragmentLoss> losses = null;
            if (listNeutralLosses.Items.Count > 0)
            {
                losses = Losses.ToArray();
            }

            if (!string.IsNullOrEmpty(formula))
            {
                try
                {
                    SequenceMassCalc.ParseModMass(BioMassCalc.MONOISOTOPIC, formula);
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

                // Allow formula and both masses to be empty, if losses are present
                if (!string.IsNullOrEmpty(textMonoMass.Text) || !string.IsNullOrEmpty(textAverageMass.Text) || losses == null)
                {
                    double mass;
                    if (!helper.ValidateDecimalTextBox(e, textMonoMass, -1500, 1500, out mass))
                        return;
                    monoMass = mass;
                    if (!helper.ValidateDecimalTextBox(e, textAverageMass, -1500, 1500, out mass))
                        return;
                    avgMass = mass;
                }
                // Loss-only modifications may not be variable
                else if (cbVariableMod.Checked)
                {
                    MessageDlg.Show(this, "The variable checkbox only applies to precursor modification.  Product ion losses are inherently variable.");
                    cbVariableMod.Focus();
                    return;
                }
            }
            else if (aas == null && term.HasValue)
            {
                MessageBox.Show(this, "Labeled atoms on terminal modification are not valid.", Program.Name);
                e.Cancel = true;
                return;
            }

            RelativeRT relativeRT = RelativeRT.Matching;
            if (comboRelativeRT.Visible && comboRelativeRT.SelectedItem != null)
            {
                relativeRT = (RelativeRT)Enum.Parse(typeof(RelativeRT),
                    comboRelativeRT.SelectedItem.ToString());
            }

            Modification = new StaticMod(name,
                                         aas,
                                         term,
                                         cbVariableMod.Checked,
                                         formula,
                                         labelAtoms,
                                         relativeRT,
                                         monoMass,
                                         avgMass,
                                         losses);

            // Store state of the chemical formula checkbox for next use.
            if (cbChemicalFormula.Visible)
                Settings.Default.ShowHeavyFormula = panelFormula.Visible;

            DialogResult = DialogResult.OK;
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
                string aaString = comboAA.Text;
                if (!string.IsNullOrEmpty(aaString) && aaString.Length == 1 &&
                        AminoAcid.IsAA(aaString[0])&& labelAtoms != LabelAtoms.None)
                    formula = SequenceMassCalc.GetHeavyFormula(aaString[0], labelAtoms);
            }

            if (string.IsNullOrEmpty(formula))
            {
                // If the mass edit boxes are already enabled, don't clear what a user
                // may have typed in them.
                if (!textMonoMass.Enabled)
                    textMonoMass.Text = "";
                if (!textAverageMass.Enabled)
                    textAverageMass.Text = "";
                textMonoMass.Enabled = textAverageMass.Enabled = (labelAtoms == LabelAtoms.None);
            }
            else
            {
                textMonoMass.Enabled = textAverageMass.Enabled = false;
                try
                {
                    textMonoMass.Text = SequenceMassCalc.ParseModMass(BioMassCalc.MONOISOTOPIC, formula).ToString();
                    textAverageMass.Text = SequenceMassCalc.ParseModMass(BioMassCalc.AVERAGE, formula).ToString();
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
            if (ShowLoss)
                listNeutralLosses.Focus();
        }

        private void btnFormulaPopup_Click(object sender, EventArgs e)
        {
            contextFormula.Show(this, panelFormula.Left + btnFormulaPopup.Right + 1,
                panelFormula.Top + btnFormulaPopup.Top);
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

        private const char SEPARATOR_AA = ',';

        private void comboAA_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Force uppercase in this control.
            e.KeyChar = char.ToUpper(e.KeyChar);
            // Only allow amino acid characters space, comma and backspace
            if (!AminoAcid.IsAA(e.KeyChar) && " ,\b".IndexOf(e.KeyChar) == -1)
                e.Handled = true;
        }

        private void comboAA_Leave(object sender, EventArgs e)
        {
            ValidateAACombo();
        }

        private void ValidateAACombo()
        {
            // Force proper format
            string aas = comboAA.Text.ToUpper();

            var sb = new StringBuilder();
            var seenAas = new bool[128];
            for (int i = 0; i < aas.Length; i++)
            {
                char c = aas[i];
                // Ignore all non-amino acid characters and repeats
                if (!AminoAcid.IsAA(c) || seenAas[c])
                    continue;

                if (sb.Length > 0)
                    sb.Append(SEPARATOR_AA).Append(' ');

                sb.Append(c);

                // Mark this amino acid seen
                seenAas[c] = true;
            }
            comboAA.Text = sb.ToString();
            UpdateMasses();
        }

        private void tbbAddLoss_Click(object sender, EventArgs e)
        {
            AddLoss();
        }

        public void AddLoss()
        {
            var dlg = new EditFragmentLossDlg(Losses.ToArray());
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                Losses = new List<FragmentLoss>(Losses) {dlg.Loss};
                listNeutralLosses.SelectedItem = dlg.Loss;
            }
        }

        private void tbbEditLoss_Click(object sender, EventArgs e)
        {
            EditLoss();
        }

        public void EditLoss()
        {
            var lossEdit = (FragmentLoss) listNeutralLosses.SelectedItem;
            var listLosses = new List<FragmentLoss>(Losses);
            listLosses.Remove(lossEdit);

            var dlg = new EditFragmentLossDlg(listLosses) { Loss = lossEdit };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                listLosses.Add(dlg.Loss);
                Losses = listLosses;
                listNeutralLosses.SelectedItem = dlg.Loss;
            }
        }

        private void tbbDeleteLoss_Click(object sender, EventArgs e)
        {
            DeleteLoss();
        }

        public void DeleteLoss()
        {
            int indexSelected = listNeutralLosses.SelectedIndex;
            listNeutralLosses.Items.Remove(listNeutralLosses.SelectedItem);
            listNeutralLosses.SelectedIndex = Math.Min(indexSelected, listNeutralLosses.Items.Count - 1);
        }

        private void listNeutralLosses_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool enable = (listNeutralLosses.SelectedIndex != -1);
            tbbEditLoss.Enabled = enable;
            tbbDeleteLoss.Enabled = enable;
        }

        #region Functional test support

        public int LossSelectedIndex
        {
            get { return listNeutralLosses.SelectedIndex; }
            set { listNeutralLosses.SelectedIndex = value; }
        }

        public string GetLossText(int indexLoss)
        {
            return listNeutralLosses.Items[indexLoss].ToString();
        }

        #endregion
    }
}
