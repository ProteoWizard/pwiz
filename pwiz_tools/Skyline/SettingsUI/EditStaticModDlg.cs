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
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditStaticModDlg : FormEx, IMultipleViewProvider
    {
        private StaticMod _modification;
        private readonly StaticMod _originalModification;
        private readonly IEnumerable<StaticMod> _existing;

        private readonly bool _editing;
        private readonly bool _heavy;
        private bool _showLoss = true; // Design mode with loss UI showing
        private readonly FormulaBox _formulaBox;

        public EditStaticModDlg(StaticMod modEditing, IEnumerable<StaticMod> existing, bool heavy)
        {
            _existing = existing;
            _editing = modEditing != null;
            _heavy = heavy;

            InitializeComponent();

            // Amino acid characters and termini should not be localized
            comboAA.Items.Add(string.Empty);
            foreach (char aa in AminoAcid.All)
                comboAA.Items.Add(aa.ToString(CultureInfo.InvariantCulture));
            comboTerm.Items.Add(string.Empty);
            comboTerm.Items.Add(ModTerminus.N.ToString());
            comboTerm.Items.Add(ModTerminus.C.ToString());

            //Formula Box
            var location = heavy
                ? new Point(panelAtoms.Location.X + cb13C.Location.X, panelAtoms.Location.Y + cb13C.Location.Y)
                : cbChemicalFormula.Location;
            _formulaBox = new FormulaBox(Resources.EditStaticModDlg_EditStaticModDlg_Chemical_formula_,
                Resources.EditMeasuredIonDlg_EditMeasuredIonDlg_A_verage_mass_,
                Resources.EditMeasuredIonDlg_EditMeasuredIonDlg__Monoisotopic_mass_)
            {
                Location = location
            };
            Controls.Add(_formulaBox);

            ComboNameVisible = !_editing;
            TextNameVisible = _editing;

            UpdateListAvailableMods();

            cbVariableMod.Visible = !heavy;
            _formulaBox.FormulaVisible = !heavy;
            cbChemicalFormula.Visible = heavy;
            cbChemicalFormula.Checked = !heavy || Settings.Default.ShowHeavyFormula;

            if (heavy)
            {                
                labelRelativeRT.Left = labelAA.Left;
                comboRelativeRT.Left = comboAA.Left;
                comboRelativeRT.Items.Add(RelativeRT.Matching.GetLocalizedString());
                comboRelativeRT.Items.Add(RelativeRT.Overlapping.GetLocalizedString());
                comboRelativeRT.Items.Add(RelativeRT.Preceding.GetLocalizedString());
                comboRelativeRT.Items.Add(RelativeRT.Unknown.GetLocalizedString());
                comboRelativeRT.SelectedIndex = 0;
            }
            else
            {
                labelRelativeRT.Visible = false;
                comboRelativeRT.Visible = false;
            }

            ShowLoss = false;
            if (heavy)
                btnLoss.Visible = false;


            Modification = _originalModification = modEditing;
        }

        public StaticMod Modification
        {
            get { return _modification;  }
            set
            {
                var modification = value;

                // Update the dialog.
                if (modification == null)
                {
                    if (_editing) 
                        textName.Text = string.Empty;
                    else
                        comboMod.Text = string.Empty;
                    comboAA.Text = string.Empty;
                    comboTerm.SelectedIndex = 0;
                    cbVariableMod.Checked = false;
                    Formula = string.Empty;
                    _formulaBox.MonoMass = null;
                    _formulaBox.AverageMass = null;
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
                    if (_editing)
                        textName.Text = modification.Name;
                    else
                        comboMod.Text = modification.Name;
                    comboAA.Text = modification.AAs ?? string.Empty;
                    if (modification.Terminus == null)
                        comboTerm.SelectedIndex = 0;
                    else
                        comboTerm.SelectedItem = modification.Terminus.Value.ToString();
                    cbVariableMod.Checked = modification.IsVariable;
                    if (modification.Formula != null)
                    {
                        Formula = modification.Formula;
                        // Make sure the formula is showing
                        cbChemicalFormula.Checked = true;
                    }
                    else
                    {
                        Formula = string.Empty;
                        _formulaBox.MonoMass = (modification.MonoisotopicMass.HasValue ?
                            modification.MonoisotopicMass.Value: (double?)null);
                        _formulaBox.AverageMass = (modification.AverageMass.HasValue ?
                            modification.AverageMass.Value: (double?)null);
                        // Force the label atom check boxes to show, if any are checked
                        if (modification.LabelAtoms != LabelAtoms.None)
                            cbChemicalFormula.Checked = false;
                    }

                    cb13C.Checked = modification.Label13C;
                    cb15N.Checked = modification.Label15N;
                    cb18O.Checked = modification.Label18O;
                    cb2H.Checked = modification.Label2H;

                    if (comboRelativeRT.Items.Count > 0)
                        comboRelativeRT.SelectedItem = modification.RelativeRT.ToString();

                    listNeutralLosses.Items.Clear();
                    if (modification.HasLoss)
                    {
                        foreach (var loss in modification.Losses)
                            listNeutralLosses.Items.Add(loss);
                    }
                    ShowLoss = listNeutralLosses.Items.Count > 0;
                    UpdateMasses();
                }
                _modification = modification;
            }
        }

        public string Formula
        {
            get { return _formulaBox.Formula; }
            set { _formulaBox.Formula = value; }
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
                listNeutralLosses.Items.AddRange(losses.Cast<object>().ToArray());
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
                    (_showLoss ? "<<" : ">>"); // Not L10N

                ResizeForLoss();
            }
        }

        private bool IsStructural
        {
            get { return !_heavy; }
        }

        private void ResizeForLoss()
        {
            int bottomControl = _heavy ? comboRelativeRT.Bottom : btnLoss.Bottom;

            int delta = panelLoss.Bottom - bottomControl;
            Height += (ShowLoss ? delta : -delta);
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(_editing ? (Control) textName : comboMod, out name))
                return;

            // Allow updating the original modification
            if (!_editing || !Equals(name, Modification.Name))
            {
                if(!ModNameAvailable(name))
                {
                    helper.ShowTextBoxError(_editing ? (Control)textName : comboMod, 
                        Resources.EditStaticModDlg_OkDialog_The_modification__0__already_exists, name);
                    return;
                }
            }

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
                        sb.Append(", "); // Not L10N
                    sb.Append(aa);
                }
            }

            string termString = comboTerm.SelectedItem.ToString();
            ModTerminus? term = null;
            if (!string.IsNullOrEmpty(termString))
                term = (ModTerminus) Enum.Parse(typeof (ModTerminus), termString);

            if (cbVariableMod.Checked && aas == null && term == null)
            {
                MessageDlg.Show(this, Resources.EditStaticModDlg_OkDialog_Variable_modifications_must_specify_amino_acid_or_terminus);
                comboAA.Focus();
                return;
            }

            string formula = null;
            double? monoMass = null;
            double? avgMass = null;
            LabelAtoms labelAtoms = LabelAtoms.None;
            if (cbChemicalFormula.Checked)
                formula = Formula;
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
                    _formulaBox.ShowTextBoxErrorFormula(helper, x.Message);
                    return;
                }
            }
            else if (labelAtoms == LabelAtoms.None)
            {
                formula = null;

                // Allow formula and both masses to be empty, if losses are present
                if ( NotZero(_formulaBox.MonoMass)  || NotZero(_formulaBox.AverageMass)|| losses == null)
                {
                    // TODO: Maximum and minimum masses should be formalized and applied everywhere
                    double mass;
                    if (!_formulaBox.ValidateMonoText(helper, -1500, 5000, out mass))
                        return;
                    monoMass = mass;
                    if (!_formulaBox.ValidateAverageText(helper, -1500, 5000, out mass))
                        return;
                    avgMass = mass;
                }
                // Loss-only modifications may not be variable
                else if (cbVariableMod.Checked)
                {
                    MessageDlg.Show(this, Resources.EditStaticModDlg_OkDialog_The_variable_checkbox_only_applies_to_precursor_modification_Product_ion_losses_are_inherently_variable);
                    cbVariableMod.Focus();
                    return;
                }
            }
            else if (aas == null && term.HasValue)
            {
                MessageDlg.Show(this, Resources.EditStaticModDlg_OkDialog_Labeled_atoms_on_terminal_modification_are_not_valid);
                return;
            }

            RelativeRT relativeRT = RelativeRT.Matching;
            if (comboRelativeRT.Visible && comboRelativeRT.SelectedItem != null)
            {
                relativeRT = RelativeRTExtension.GetEnum(comboRelativeRT.SelectedItem.ToString());
            }
            
            // Store state of the chemical formula checkbox for next use.
            if (cbChemicalFormula.Visible)
                Settings.Default.ShowHeavyFormula = _formulaBox.FormulaVisible;

            var newMod = new StaticMod(name,
                                         aas,
                                         term,
                                         cbVariableMod.Checked,
                                         formula,
                                         labelAtoms,
                                         relativeRT,
                                         monoMass,
                                         avgMass,
                                         losses);

            foreach (StaticMod mod in _existing)
            {
                if (newMod.Equivalent(mod) && !(_editing && mod.Equals(_originalModification)))
                {
                    if (DialogResult.OK == MultiButtonMsgDlg.Show(
                        this,
                        TextUtil.LineSeparate(Resources.EditStaticModDlg_OkDialog_There_is_an_existing_modification_with_the_same_settings,
                                              string.Format("'{0}'.", mod.Name), // Not L10N
                                              string.Empty,
                                              Resources.EditStaticModDlg_OkDialog_Continue),
                        MultiButtonMsgDlg.BUTTON_OK))
                    {
                        Modification = newMod;
                        DialogResult = DialogResult.OK;
                    }
                    return;
                }
            }
            
            var uniMod = UniMod.GetModification(name, IsStructural);
            // If the modification name is not found in Unimod, check if there exists a modification in Unimod that matches
            // the dialog modification, and prompt the user to to use the Unimod modification instead.
            if (uniMod == null)
            {
                var matchingMod = UniMod.FindMatchingStaticMod(newMod, IsStructural);
                if (matchingMod != null &&
                    (ModNameAvailable(matchingMod.Name) ||
                    (_editing && Equals(matchingMod.Name, Modification.Name))))
                {
                    var result = MultiButtonMsgDlg.Show(
                        this,
                        TextUtil.LineSeparate(Resources.EditStaticModDlg_OkDialog_There_is_a_Unimod_modification_with_the_same_settings,
                                                string.Empty,
                                                string.Format(Resources.EditStaticModDlg_OkDialog_Click__Unimod__to_use_the_name___0___, matchingMod.Name),
                                                string.Format(Resources.EditStaticModDlg_OkDialog_Click__Custom__to_use_the_name___0___, name)),
                        Resources.EditStaticModDlg_OkDialog_Unimod,
                        Resources.EditStaticModDlg_OkDialog_Custom,
                        true);
                    if (result == DialogResult.Yes)
                        newMod = matchingMod.MatchVariableAndLossInclusion(newMod);   // Unimod
                    if (result == DialogResult.Cancel)
                        return;
                }
            }
            else
            {
                // If the dialog modification matches the modification of the same name in Unimod, 
                // use the UnimodId.
                if (newMod.Equivalent(uniMod))
                    newMod = uniMod.MatchVariableAndLossInclusion(newMod);
                else
                {
                    // Finally, if the modification name is found in Unimod, but the modification in Unimod does not 
                    // match the dialog modification, prompt the user to use the Unimod modification definition instead.
                    if (DialogResult.OK != MultiButtonMsgDlg.Show(
                        this,
                        TextUtil.LineSeparate(string.Format(Resources.EditStaticModDlg_OkDialog_This_modification_does_not_match_the_Unimod_specifications_for___0___, name),
                                                string.Empty,
                                                Resources.EditStaticModDlg_OkDialog_Use_non_standard_settings_for_this_name),
                        MultiButtonMsgDlg.BUTTON_OK))
                    {
                        return;
                    }
                }
            }

            _modification = newMod;

            DialogResult = DialogResult.OK;
        }

        private bool NotZero(double? val)
        {
            return val != null && val != 0;
        }
        
        private bool ModNameAvailable(string name)
        {
            // But not any other existing modification
            foreach (StaticMod mod in _existing)
            {
                if (Equals(name, mod.Name))
                {
                    return false;
                }
            }
            return true;
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
            _formulaBox.FormulaVisible = cbChemicalFormula.Checked;
            panelAtoms.Visible = !cbChemicalFormula.Checked;
            UpdateMasses();
        }

        private void UpdateMasses()
        {
            string formula = null;
            LabelAtoms labelAtoms = LabelAtoms.None;
            if (cbChemicalFormula.Checked)
                formula = Formula;
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
                if (!_formulaBox.MassEnabled)
                {
                    _formulaBox.MonoMass = null;
                    _formulaBox.AverageMass = null;
                }
                _formulaBox.MassEnabled = (labelAtoms == LabelAtoms.None);
            }
            else
            {
                _formulaBox.Formula = formula;
            }
        }

// ReSharper disable MemberCanBeMadeStatic.Local
        private void textFormula_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Force uppercase in this control.
            // Atoms have been added containing lower case chars
            // e.KeyChar = char.ToUpper(e.KeyChar);
        }
// ReSharper restore MemberCanBeMadeStatic.Local

        private void btnLoss_Click(object sender, EventArgs e)
        {
            ShowLoss = !ShowLoss;
            if (ShowLoss)
                listNeutralLosses.Focus();
        }


        private const char SEPARATOR_AA = ','; // Not L10N

// ReSharper disable MemberCanBeMadeStatic.Local
        private void comboAA_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Force uppercase in this control.
            e.KeyChar = char.ToUpper(e.KeyChar);
            // Only allow amino acid characters space, comma and backspace
            if (!AminoAcid.IsAA(e.KeyChar) && " ,\b".IndexOf(e.KeyChar) == -1) // Not L10N
                e.Handled = true;
        }
// ReSharper restore MemberCanBeMadeStatic.Local

        private void comboAA_Leave(object sender, EventArgs e)
        {
            ValidateAACombo();
        }

        private void ValidateAACombo()
        {
            // Force proper format
            string aas = comboAA.Text.ToUpperInvariant();

            var sb = new StringBuilder();
            var seenAas = new bool[128];
            foreach (char c in aas)
            {
                // Ignore all non-amino acid characters and repeats
                if (!AminoAcid.IsAA(c) || seenAas[c])
                    continue;

                if (sb.Length > 0)
                    sb.Append(SEPARATOR_AA).Append(TextUtil.SEPARATOR_SPACE);

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
            using (var dlg = new EditFragmentLossDlg(Losses.ToArray()))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Losses = new List<FragmentLoss>(Losses) {dlg.Loss};
                    listNeutralLosses.SelectedItem = dlg.Loss;
                }
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

            using (var dlg = new EditFragmentLossDlg(listLosses) { Loss = lossEdit })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    listLosses.Add(dlg.Loss);
                    Losses = listLosses;
                    listNeutralLosses.SelectedItem = dlg.Loss;
                }
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

        public class StructuralModView : IFormView {}
        public class IsotopeModView : IFormView {}

        public IFormView ShowingFormView
        {
            get
            {
                if (_heavy)
                    return new IsotopeModView();
                return new StructuralModView();
            }
        }

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

        private void UpdateListAvailableMods()
        {
            comboMod.Items.Clear();
            comboMod.Items.Add(Settings.Default.StaticModsShowMore
                                   ? Resources.EditStaticModDlg_UpdateListAvailableMods_Show_common
                                   : Resources.EditStaticModDlg_UpdateListAvailableMods_Show_all);
            comboMod.Items.AddRange(ListAvailableMods().Cast<object>().ToArray());
        }

        public List<string> ListAvailableMods()
        {
            List<string> staticModsNames = new List<string>();
            staticModsNames.AddRange(_heavy ? UniMod.DictIsotopeModNames.Keys : UniMod.DictStructuralModNames.Keys);
            if (Settings.Default.StaticModsShowMore)
                staticModsNames.AddRange(_heavy ? UniMod.DictHiddenIsotopeModNames.Keys : UniMod.DictHiddenStructuralModNames.Keys);
            staticModsNames.Sort();
            return staticModsNames;
        }

        private void comboMod_DropDownClosed(object sender, EventArgs e)
        {
            if (comboMod.SelectedIndex == 0)
                ToggleLessMore();
        }

        public void SetModification(string modName)
        {
            // Make all but Cysteine modifications default to variable
            SetModification(modName, !modName.Contains("(C)")); // Not L10N
        }

        public void SetModification(string modName, bool isVariable)
        {
            var modification = UniMod.GetModification(modName, IsStructural);
            // Avoid setting loss-only modifications to variable, since losses themselves act as variable
            if (modification.HasLoss && !modification.HasMod)
                isVariable = false;
            if (IsStructural && isVariable)
                modification = modification.ChangeVariable(true);
            Modification = modification;
        }

        private void comboMod_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboMod.SelectedIndex == 0 && !comboMod.DroppedDown)
                comboMod.SelectedIndex = 1;
            else if (comboMod.SelectedIndex == -1)
                Modification = null;
            else
                SetModification(comboMod.SelectedItem.ToString());
        }

        public void ToggleLessMore()
        {
            Settings.Default.StaticModsShowMore = !Settings.Default.StaticModsShowMore;
            UpdateListAvailableMods();
            comboMod.DroppedDown = true;
        }

        public bool TextNameVisible
        {
            get { return textName.Visible; }
            private set { textName.Visible = value; }
        }

        public bool ComboNameVisible
        {
            get { return comboMod.Visible; }
            private set { comboMod.Visible = value; }
        }
    }
}
