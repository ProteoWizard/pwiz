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
using System.Text;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
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
        private readonly FormulaBox _formulaBox;

        public EditMeasuredIonDlg(IEnumerable<MeasuredIon> existing)
        {
            _existing = existing;

            InitializeComponent();

            _formulaBox =
                new FormulaBox(Resources.EditMeasuredIonDlg_EditMeasuredIonDlg_Ion__chemical_formula_,
                    Resources.EditCustomMoleculeDlg_EditCustomMoleculeDlg_A_verage_m_z_,
                    Resources.EditCustomMoleculeDlg_EditCustomMoleculeDlg__Monoisotopic_m_z_)
                {
                    Location = new Point(textFragment.Left, radioReporter.Top + 30),
                    Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
                };

            Controls.Add(_formulaBox);

            // Seems like there should be a way to set this in the properties.
            comboDirection.SelectedIndex = 0;

           
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
                    textName.Text = string.Empty;
                    textFragment.Text = string.Empty;
                    textRestrict.Text = string.Empty;
                    comboDirection.SelectedIndex = 0;
                    textMinAas.Text = MeasuredIon.DEFAULT_MIN_FRAGMENT_LENGTH.ToString(LocalizationHelper.CurrentCulture);
                    _formulaBox.Formula = string.Empty;
                    _formulaBox.MonoMass = null;
                    _formulaBox.AverageMass = null;
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
                                      ? _measuredIon.MinFragmentLength.Value.ToString(LocalizationHelper.CurrentCulture)
                                      : string.Empty;
            }
            else if (!string.IsNullOrEmpty(_measuredIon.CustomIon.Formula))
            {
                _formulaBox.Formula = _measuredIon.CustomIon.Formula;
                textCharge.Text = _measuredIon.Charge.ToString(LocalizationHelper.CurrentCulture);
                _formulaBox.Charge = _measuredIon.Charge;
            }
            else
            {
                _formulaBox.Formula = string.Empty;
                _formulaBox.Charge = _measuredIon.Charge;
                _formulaBox.MonoMass = _measuredIon.CustomIon.MonoisotopicMass;
                _formulaBox.AverageMass = _measuredIon.CustomIon.AverageMass;
                textCharge.Text = _measuredIon.Charge.ToString(LocalizationHelper.CurrentCulture);
            }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(textName, out name))
                return;

            if (_existing.Contains(m => !ReferenceEquals(_measuredIon, m) && Equals(name, m.Name)))
            {
                helper.ShowTextBoxError(textName, Resources.EditMeasuredIonDlg_OkDialog_The_special_ion__0__already_exists, name);
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
                if (!helper.ValidateNumberTextBox(textMinAas, MeasuredIon.MIN_MIN_FRAGMENT_LENGTH,
                        MeasuredIon.MAX_MIN_FRAGMENT_LENGTH, out minAas))
                    return;

                _measuredIon = new MeasuredIon(name, cleavage, restrict, direction, minAas);
            }
            else
            {
                var customIon = ValidateCustomIon(name);
                if (customIon == null)
                    return;
                _measuredIon = customIon;
            }

            DialogResult = DialogResult.OK;
        }

        private int? ValidateCharge()
        {
            var helper = new MessageBoxHelper(this);
            int charge;
            const int min = Transition.MIN_PRODUCT_CHARGE;
            const int max = Transition.MAX_PRODUCT_CHARGE;
            if (!helper.ValidateNumberTextBox(textCharge, min, max, out charge))
            {
                return null;
            }
            _formulaBox.Charge = charge;
            return charge;
        }

        private MeasuredIon ValidateCustomIon(string name)
        {
            var helper = new MessageBoxHelper(this);
            string formula = _formulaBox.Formula.ToString(LocalizationHelper.CurrentCulture);
            var charge = ValidateCharge();
            if (!charge.HasValue)
            {
                return null;
            }
            double monoMass;
            double avgMass;
            if (!string.IsNullOrEmpty(formula))
            {
                // Mass is specified by chemical formula
                try
                {
                    monoMass = SequenceMassCalc.ParseModMass(BioMassCalc.MONOISOTOPIC, formula);
                    avgMass = SequenceMassCalc.ParseModMass(BioMassCalc.AVERAGE, formula);
                }
                catch (ArgumentException x)
                {
                    helper.ShowTextBoxError(_formulaBox, x.Message);
                    return null;
                }
            }
            else if (_formulaBox.MonoMass != null ||
                     _formulaBox.AverageMass != null)
            {
                // Mass is specified by combination of mz and charge
                formula = null;
                if (!_formulaBox.ValidateMonoText(helper))
                    return null;
                if (!_formulaBox.ValidateAverageText(helper))
                    return null;
                _formulaBox.Charge = charge; // This provokes calculation of mass from displayed mz values
                monoMass = _formulaBox.MonoMass.Value; 
                avgMass = _formulaBox.AverageMass.Value;
            }
            else
            {
                // User hasn't fully specified either way
                _formulaBox.ShowTextBoxErrorFormula(helper,
                    Resources.EditMeasuredIonDlg_OkDialog_Please_specify_a_formula_or_constant_masses);
                return null;
            }
            if (MeasuredIon.MIN_REPORTER_MASS > monoMass || MeasuredIon.MIN_REPORTER_MASS > avgMass)
            {
                _formulaBox.ShowTextBoxErrorMonoMass(helper, string.Format(Resources.EditMeasuredIonDlg_OkDialog_Reporter_ion_masses_must_be_less_than_or_equal_to__0__,
                                                                 MeasuredIon.MAX_REPORTER_MASS));
                return null;
            }
            if (monoMass > MeasuredIon.MAX_REPORTER_MASS || avgMass > MeasuredIon.MAX_REPORTER_MASS)
            {
                _formulaBox.ShowTextBoxErrorAverageMass(helper, string.Format(Resources.EditMeasuredIonDlg_OkDialog_Reporter_ion_masses_must_be_less_than_or_equal_to__0__,
                                                                   MeasuredIon.MAX_REPORTER_MASS));
                return null;
            }

            return new MeasuredIon(name, formula, monoMass, avgMass, charge.Value);
        }

        private static bool ValidateAATextBox(MessageBoxHelper helper, TextBox control, bool allowEmpty, out string aaText)
        {
            aaText = control.Text.Trim().ToUpperInvariant();
            if (aaText.Length == 0)
            {
                if (!allowEmpty)
                {
                    helper.ShowTextBoxError(control, Resources.EditMeasuredIonDlg_ValidateAATextBox__0__must_contain_at_least_one_amino_acid);
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
                        helper.ShowTextBoxError(control, Resources.EditMeasuredIonDlg_ValidateAATextBox_The_character__0__is_not_a_valid_amino_acid, c);
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

        private void textCharge_TextChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textCharge.Text))
                return; // Don't complain about empty field, user may be mid-edit
            var charge = ValidateCharge();
            if (!charge.HasValue)
                return;
            Charge = charge.Value;
        }

        private void UpdateIonType()
        {
            bool isFragment = radioFragment.Checked;
            textFragment.Enabled = isFragment;
            textRestrict.Enabled = isFragment;
            comboDirection.Enabled = isFragment;
            textMinAas.Enabled = isFragment;
            label1.Enabled = label2.Enabled = label3.Enabled = label4.Enabled = isFragment;
            _formulaBox.Enabled = !isFragment;
            textCharge.Enabled = !isFragment;
            labelCharge.Enabled = !isFragment;
            if (isFragment)
            {
                _formulaBox.MonoMass = null;
                _formulaBox.AverageMass = null;
                _formulaBox.Formula = string.Empty;
                if (MeasuredIon != null && MeasuredIon.IsFragment)
                    SetTextFields();
                else
                {
                    comboDirection.SelectedIndex = 0;
                    textMinAas.Text = MeasuredIon.DEFAULT_MIN_FRAGMENT_LENGTH.ToString(LocalizationHelper.CurrentCulture);
                }
            }
            else
            {
                textFragment.Text = textRestrict.Text = textMinAas.Text = string.Empty;
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

        #region test functional support

        public string TextName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public int Charge
        {
            get
            {
                var helper = new MessageBoxHelper(this);
                int charge;
                helper.ValidateNumberTextBox(textCharge, Transition.MAX_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE, out charge);
                return charge;
            }
            set { textCharge.Text = value.ToString(CultureInfo.InvariantCulture); } 
        }

        public double MonoIsotopicMass
        {
            get { return _formulaBox.MonoMass ?? 0; }
            set { _formulaBox.MonoMass = value; }
        }

        public double AverageMass
        {
            get { return _formulaBox.AverageMass ?? 0; }
            set { _formulaBox.AverageMass = value; }
        }

        public string Formula
        {
            get { return _formulaBox.Formula; }
            set { _formulaBox.Formula = value; }
        }

        public void SwitchToCustom()
        {
            radioReporter.Checked = true;
        }
        #endregion
    }
}