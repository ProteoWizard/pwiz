/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.IO;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditCustomMoleculeDlg : FormEx
    {
        private DocNodeCustomIon _resultCustomIon;
        private readonly FormulaBox _formulaBox;
        private readonly IEnumerable<CustomIon> _existing;
        private readonly int _minCharge;
        private readonly int _maxCharge;
        private readonly TransitionSettings _settings;

        public EditCustomMoleculeDlg(String title, IEnumerable<CustomIon> existing, int minCharge, int maxCharge, 
            TransitionSettings settings, int defaultCharge, bool isPrecursor, ExplicitTransitionGroupValues explicitAttributes, ExplicitRetentionTimeInfo explicitRetentionTime)
        {
            Text = title;
            _existing = existing;
            _minCharge = minCharge;
            _maxCharge = maxCharge;
            _settings = settings;

            InitializeComponent();
            ResultExplicitTransitionGroupValues = new ExplicitTransitionGroupValues(explicitAttributes);

            _formulaBox =
                new FormulaBox(Resources.EditMeasuredIonDlg_EditMeasuredIonDlg_Ion__chemical_formula_,
                    Resources.EditCustomMoleculeDlg_EditCustomMoleculeDlg_A_verage_m_z_,
                    Resources.EditCustomMoleculeDlg_EditCustomMoleculeDlg__Monoisotopic_m_z_, 
                    defaultCharge)
                {
                    Location = new Point(textName.Left, textName.Bottom + 12)
                };
            Controls.Add(_formulaBox);
            _formulaBox.TabIndex = 2;
            textCharge.Visible = labelCharge.Visible = true;
            Charge = defaultCharge;
            if (explicitRetentionTime == null)
            {
                RetentionTime = null;
                RetentionTimeWindow = null;
            }
            else
            {
                RetentionTime = explicitRetentionTime.RetentionTime;
                RetentionTimeWindow = explicitRetentionTime.RetentionTimeWindow;
            }
            if (!isPrecursor)
            {
                groupBoxOptionalValues.Visible = false;
                Height -= groupBoxOptionalValues.Height;
            }
        }

        public DocNodeCustomIon ResultCustomIon
        {
            get
            {
                return _resultCustomIon;
            }
            set
            {
                _resultCustomIon = value;
                SetNameAndFormulaBoxText();
            }
        }

        public ExplicitTransitionGroupValues ResultExplicitTransitionGroupValues
        {
            get
            {
                return new ExplicitTransitionGroupValues(CollisionEnergy, DriftTimeMsec, DriftTimeHighEnergyOffsetMsec);
            }
            set
            {
                // Use constructor to handle value == null
                var resultExplicitTransitionGroupValues = new ExplicitTransitionGroupValues(value);
                CollisionEnergy = resultExplicitTransitionGroupValues.CollisionEnergy;
                DriftTimeMsec = resultExplicitTransitionGroupValues.DriftTimeMsec;
                DriftTimeHighEnergyOffsetMsec = resultExplicitTransitionGroupValues.DriftTimeHighEnergyOffsetMsec;
            }
        }

        public ExplicitRetentionTimeInfo ResultRetentionTimeInfo
        {
            get
            {
                return RetentionTime.HasValue
                    ? new ExplicitRetentionTimeInfo(RetentionTime.Value, RetentionTimeWindow)
                    : null;
            }
            set
            {
                if (value != null)
                {
                    RetentionTime = value.RetentionTime;
                    RetentionTimeWindow = value.RetentionTimeWindow; 
                }
                else
                {
                    RetentionTime = null;
                    RetentionTimeWindow = null; 
                }
            }
        }

        public int Charge
        {
            get
            {
                int val;
                if (int.TryParse(textCharge.Text, out val))
                    return val;
                return 1;
            }
            set
            {
                if (value == 0)
                {
                    textCharge.Text = string.Empty;
                    _formulaBox.Charge = null;
                }
                else
                {
                    textCharge.Text = value.ToString(LocalizationHelper.CurrentCulture);
                    _formulaBox.Charge = value;
                }
            }
        }

        private static double? NullForEmpty(string text)
        {
            double val;
            if (double.TryParse(text, out val))
                return val;
            return null;
        }

        private static string EmptyForNullOrNonPositive(double? value)
        {
            double dval = (value ?? 0);
            return (dval <= 0) ? string.Empty : dval.ToString(LocalizationHelper.CurrentCulture);
        }

        public double? CollisionEnergy
        {
            get { return NullForEmpty(textCollisionEnergy.Text); }
            set { textCollisionEnergy.Text = EmptyForNullOrNonPositive(value); }
        }

        public double? RetentionTime
        {
            get { return NullForEmpty(textRetentionTime.Text); }
            set { textRetentionTime.Text = EmptyForNullOrNonPositive(value); }
        }

        public double? RetentionTimeWindow
        {
            get { return NullForEmpty(textRetentionTimeWindow.Text); }
            set { textRetentionTimeWindow.Text = EmptyForNullOrNonPositive(value); }
        }

        public double? DriftTimeMsec
        {
            get { return NullForEmpty(textDriftTimeMsec.Text); }
            set { textDriftTimeMsec.Text = EmptyForNullOrNonPositive(value); }
        }

        public double? DriftTimeHighEnergyOffsetMsec
        {
            get { return NullForEmpty(textDriftTimeHighEnergyOffsetMsec.Text); }
            set { textDriftTimeHighEnergyOffsetMsec.Text = value == null ? string.Empty : value.Value.ToString(LocalizationHelper.CurrentCulture); } // Negative values are normal here
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);
            if (
                _existing.Contains(
                    c =>
                        !ReferenceEquals(_resultCustomIon, c) && (string.IsNullOrEmpty(textName.Text)
                            ? (Equals(_formulaBox.Formula, c.Formula) && Equals(_formulaBox.AverageMass, c.AverageMass) &&
                               (Equals(_formulaBox.MonoMass, c.MonoisotopicMass)))
                            : Equals(textName.Text, c.Name))))
            {
                helper.ShowTextBoxError(textName,
                    Resources.EditCustomMoleculeDlg_OkDialog_The_custom_molecule_already_exists_, textName.Text);
                return;
            }
            int charge;
            if (!helper.ValidateSignedNumberTextBox(textCharge, _minCharge, _maxCharge, out charge))
                return;
            if (RetentionTimeWindow.HasValue && !RetentionTime.HasValue)
            {
                helper.ShowTextBoxError(textRetentionTimeWindow,
                    Resources.Peptide_ExplicitRetentionTimeWindow_Explicit_retention_time_window_requires_an_explicit_retention_time_value_);
                return;
            }
            Charge = charge; // Note: order matters here, this settor indirectly updates _formulaBox.MonoMass when formula is empty
            if (string.IsNullOrEmpty(_formulaBox.Formula))
            {
                // Can the text fields be understood as mz?
                if (!_formulaBox.ValidateAverageText(helper))
                    return;
                if (!_formulaBox.ValidateMonoText(helper))
                    return;
            }
            var formula = _formulaBox.Formula;
            var monoMass = _formulaBox.MonoMass ?? 0;
            var averageMass = _formulaBox.AverageMass ?? 0;
            if (monoMass < CustomIon.MIN_MASS || averageMass < CustomIon.MIN_MASS)
            {
                _formulaBox.ShowTextBoxErrorFormula(helper,
                    string.Format(Resources.EditCustomMoleculeDlg_OkDialog_Custom_molecules_must_have_a_mass_greater_than_or_equal_to__0__,
                        CustomIon.MIN_MASS));
                return;
            }
            if (monoMass > CustomIon.MAX_MASS || averageMass > CustomIon.MAX_MASS)
            {
                _formulaBox.ShowTextBoxErrorFormula(helper,
                    string.Format(Resources.EditCustomMoleculeDlg_OkDialog_Custom_molecules_must_have_a_mass_less_than_or_equal_to__0__, CustomIon.MAX_MASS));
                return;
            }

            if (!_settings.IsMeasurablePrecursor(BioMassCalc.CalculateIonMz(monoMass, charge)) ||
                !_settings.IsMeasurablePrecursor(BioMassCalc.CalculateIonMz(averageMass, charge)))
            {
                _formulaBox.ShowTextBoxErrorFormula(helper, Resources.SkylineWindow_AddMolecule_The_precursor_m_z_for_this_molecule_is_out_of_range_for_your_instrument_settings_);
                return;
            }
            if (!string.IsNullOrEmpty(_formulaBox.Formula))
            {
                try
                {
                    ResultCustomIon = new DocNodeCustomIon(formula, textName.Text);
                }
                catch (InvalidDataException x)
                {
                    _formulaBox.ShowTextBoxErrorFormula(helper, x.Message);
                    return;
                }
            }
            else
            {
                ResultCustomIon = new DocNodeCustomIon(monoMass, averageMass, textName.Text);
            }
            DialogResult = DialogResult.OK;
        }

        private void SetNameAndFormulaBoxText()
        {
            if (ResultCustomIon == null)
            {
                _formulaBox.Formula = string.Empty;
                _formulaBox.AverageMass = null;
                _formulaBox.MonoMass = null;
                textName.Text = string.Empty;
            }
            else
            {
                textName.Text = ResultCustomIon.Name ?? string.Empty;
                _formulaBox.Formula = ResultCustomIon.Formula ?? string.Empty;
                if (ResultCustomIon.Formula == null)
                {
                    _formulaBox.AverageMass = ResultCustomIon.AverageMass;
                    _formulaBox.MonoMass = ResultCustomIon.MonoisotopicMass;
                }
            }
        }

        private void textCharge_TextChanged(object sender, EventArgs e)
        {
            var helper = new MessageBoxHelper(this, false);
            int charge;
            if (!helper.ValidateSignedNumberTextBox(textCharge, _minCharge, _maxCharge, out charge))
                return;
            Charge = charge;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        #region For Testing

        public String NameText
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public FormulaBox FormulaBox
        {
            get { return _formulaBox; }
        }

        #endregion
    }
}
