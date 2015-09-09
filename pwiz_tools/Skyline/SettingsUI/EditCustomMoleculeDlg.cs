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
using System.Linq;
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
        private readonly Identity _initialId;
        private readonly IEnumerable<Identity> _existingIds;
        private readonly int _minCharge;
        private readonly int _maxCharge;
        private readonly TransitionSettings _transitionSettings;
        private PeptideSettings _peptideSettings;
        private readonly PeptideSettingsUI.LabelTypeComboDriver _driverLabelType;
        private readonly SkylineWindow _parent;

        /// <summary>
        /// For modifying at the Molecule level
        /// </summary>
        public EditCustomMoleculeDlg(SkylineWindow parent, string title,
            SrmSettings settings, string defaultName, string defaultFormula, ExplicitRetentionTimeInfo explicitRetentionTime) :
            this(parent, title, null, null, 0, 0, null, defaultName, defaultFormula, null, null, explicitRetentionTime, null, false)
        {
        }

        /// <summary>
        /// For creating at the Molecule level (create molecule and first transition group) or modifying at the transition level
        /// Null values imply "don't ask user for this"
        /// </summary>
        public EditCustomMoleculeDlg(SkylineWindow parent, string title, Identity initialId, IEnumerable<Identity> existingIds, int minCharge, int maxCharge,
            SrmSettings settings, string defaultName, string defaultFormula, int? defaultCharge, ExplicitTransitionGroupValues explicitAttributes, 
            ExplicitRetentionTimeInfo explicitRetentionTime,
            IsotopeLabelType defaultIsotopeLabelType, bool enableFormulaEditing = true)
        {
            Text = title;
            _parent = parent;
            _initialId = initialId;
            _existingIds = existingIds;
            _minCharge = minCharge;
            _maxCharge = maxCharge;
            _transitionSettings = settings != null ? settings.TransitionSettings : null;
            _peptideSettings = settings != null ? settings.PeptideSettings : null;

            InitializeComponent();

            NameText = defaultName;
            var needOptionalValuesBox = explicitRetentionTime != null || explicitAttributes != null;
            var heightDelta = 0;

            if (explicitAttributes == null)
            {
                ResultExplicitTransitionGroupValues = null;
                labelCollisionEnergy.Visible = false;
                textCollisionEnergy.Visible = false;
                labelSLens.Visible = false;
                textSLens.Visible = false;
                labelCompensationVoltage.Visible = false;
                textCompensationVoltage.Visible = false;
                labelConeVoltage.Visible = false;
                textConeVoltage.Visible = false;
                labelDriftTimeHighEnergyOffsetMsec.Visible = false;
                textDriftTimeHighEnergyOffsetMsec.Visible = false;
                labelDriftTimeMsec.Visible = false;
                textDriftTimeMsec.Visible = false;
                if (needOptionalValuesBox)
                {
                    // We blanked out everything but the retention time
                    var vmargin = labelRetentionTime.Location.Y;
                    var newHeight = textRetentionTime.Location.Y + textRetentionTime.Height +  vmargin;
                    heightDelta = groupBoxOptionalValues.Height - newHeight;
                    groupBoxOptionalValues.Height = newHeight;
                }
            }
            else
            {
                ResultExplicitTransitionGroupValues = new ExplicitTransitionGroupValues(explicitAttributes);
            }
            
            string labelAverage = defaultCharge.HasValue
                ? Resources.EditCustomMoleculeDlg_EditCustomMoleculeDlg_A_verage_m_z_
                : Resources.EditCustomMoleculeDlg_EditCustomMoleculeDlg_A_verage_mass_;
            string labelMono = defaultCharge.HasValue
                ? Resources.EditCustomMoleculeDlg_EditCustomMoleculeDlg__Monoisotopic_m_z_
                : Resources.EditCustomMoleculeDlg_EditCustomMoleculeDlg__Monoisotopic_mass_;

            _formulaBox =
                new FormulaBox(Resources.EditMeasuredIonDlg_EditMeasuredIonDlg_Ion__chemical_formula_,
                    labelAverage,
                    labelMono, 
                    defaultCharge)
                {
                    Formula = defaultFormula,
                    Location = new Point(textName.Left, textName.Bottom + 12)
                };
            Controls.Add(_formulaBox);
            _formulaBox.TabIndex = 2;
            _formulaBox.Enabled = enableFormulaEditing;
            bool needCharge = defaultCharge.HasValue;
            textCharge.Visible = labelCharge.Visible = needCharge;
            Charge = defaultCharge ?? 0;
            if (needOptionalValuesBox && !needCharge)
            {
                heightDelta += groupBoxOptionalValues.Location.Y - labelCharge.Location.Y;
                groupBoxOptionalValues.Location = new Point(groupBoxOptionalValues.Location.X, labelCharge.Location.Y);
            }
            if (explicitRetentionTime == null)
            {
                // Don't ask user for retetention times
                RetentionTime = null;
                RetentionTimeWindow = null;
                labelRetentionTime.Visible = false;
                labelRetentionTimeWindow.Visible = false;
                textRetentionTime.Visible = false;
                textRetentionTimeWindow.Visible = false;
                if (needOptionalValuesBox)
                {
                    var rtHeight = labelCollisionEnergy.Location.Y - labelRetentionTimeWindow.Location.Y;
                    groupBoxOptionalValues.Height -= rtHeight;
                    heightDelta += rtHeight;
                }
            }
            else
            {
                RetentionTime = explicitRetentionTime.RetentionTime;
                RetentionTimeWindow = explicitRetentionTime.RetentionTimeWindow;
            }
            if (!needOptionalValuesBox)
            {
                groupBoxOptionalValues.Visible = false;
                heightDelta = groupBoxOptionalValues.Height;
            }
            // Initialize label
            if (settings != null && defaultIsotopeLabelType != null)
            {
                _driverLabelType = new PeptideSettingsUI.LabelTypeComboDriver(comboIsotopeLabelType,
                    settings.PeptideSettings.Modifications, null, null, null, null)
                {
                    SelectedName = defaultIsotopeLabelType.Name
                };
            }
            else
            {
                comboIsotopeLabelType.Visible = false;
                labelIsotopeLabelType.Visible = false;
            }
            Height -= heightDelta;
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
                return new ExplicitTransitionGroupValues(CollisionEnergy, DriftTimeMsec, DriftTimeHighEnergyOffsetMsec, SLens, ConeVoltage, DeclusteringPotential, CompensationVoltage);
            }
            set
            {
                // Use constructor to handle value == null
                var resultExplicitTransitionGroupValues = new ExplicitTransitionGroupValues(value);
                CollisionEnergy = resultExplicitTransitionGroupValues.CollisionEnergy;
                DriftTimeMsec = resultExplicitTransitionGroupValues.DriftTimeMsec;
                DriftTimeHighEnergyOffsetMsec = resultExplicitTransitionGroupValues.DriftTimeHighEnergyOffsetMsec;
                SLens = resultExplicitTransitionGroupValues.SLens;
                ConeVoltage = resultExplicitTransitionGroupValues.ConeVoltage;
                DeclusteringPotential = resultExplicitTransitionGroupValues.DeclusteringPotential;
                CompensationVoltage = resultExplicitTransitionGroupValues.CompensationVoltage;
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

        public double? DeclusteringPotential
        {
            get { return NullForEmpty(textDeclusteringPotential.Text); }
            set { textDeclusteringPotential.Text = EmptyForNullOrNonPositive(value); }
        }

        public double? CompensationVoltage
        {
            get { return NullForEmpty(textCompensationVoltage.Text); }
            set { textCompensationVoltage.Text = EmptyForNullOrNonPositive(value); }
        }

        public double? SLens
        {
            get { return NullForEmpty(textSLens.Text); }
            set { textSLens.Text = EmptyForNullOrNonPositive(value); }
        }

        public double? ConeVoltage
        {
            get { return NullForEmpty(textConeVoltage.Text); }
            set { textConeVoltage.Text = EmptyForNullOrNonPositive(value); }
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

        public ExplicitRetentionTimeInfo ExplicitRetentionTimeInfo
        {
            get
            {
                return RetentionTime.HasValue
                    ? new ExplicitRetentionTimeInfo(RetentionTime.Value, RetentionTimeWindow)
                    : null;
            }
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

        public IsotopeLabelType IsotopeLabelType
        {
            get { return (_driverLabelType == null) ? null :_driverLabelType.SelectedMods.LabelType; }
            set { if (_driverLabelType != null) _driverLabelType.SelectedName = value.Name; }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);
            var charge = 0;
            if (textCharge.Visible && !helper.ValidateSignedNumberTextBox(textCharge, _minCharge, _maxCharge, out charge))
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

            if ((_transitionSettings != null) &&
                (!_transitionSettings.IsMeasurablePrecursor(BioMassCalc.CalculateIonMz(monoMass, charge)) ||
                !_transitionSettings.IsMeasurablePrecursor(BioMassCalc.CalculateIonMz(averageMass, charge))))
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
            // Did user change the list of heavy labels?
            if (_driverLabelType != null)
            {
                PeptideModifications modifications = new PeptideModifications(
                    _peptideSettings.Modifications.StaticModifications,
                    _peptideSettings.Modifications.MaxVariableMods,  
                    _peptideSettings.Modifications.MaxNeutralLosses,
                    _driverLabelType.GetHeavyModifications(), // This is the only thing the user may have altered
                    _peptideSettings.Modifications.InternalStandardTypes);
                var settings = _peptideSettings.ChangeModifications(modifications);
                // Only update if anything changed
                if (!Equals(settings, _peptideSettings))
                {
                    SrmSettings newSettings = _parent.DocumentUI.Settings.ChangePeptideSettings(settings);
                    if (!_parent.ChangeSettings(newSettings, true))
                    {
                        return;
                    }
                    _peptideSettings = newSettings.PeptideSettings;
                }
            }

            // See if this combination of charge and label would conflict with any existing transition groups
            if (_existingIds != null && _existingIds.Any(t =>
                {
                    var transitionGroup = t as TransitionGroup;
                    return transitionGroup != null && Equals(transitionGroup.LabelType, IsotopeLabelType) &&
                           Equals(transitionGroup.PrecursorCharge, Charge) && !ReferenceEquals(t, _initialId);
                }))
            {
                helper.ShowTextBoxError(textName,
                    Resources.EditCustomMoleculeDlg_OkDialog_A_precursor_with_that_charge_and_label_type_already_exists_, textName.Text);
                return;
            }

            // See if this would conflict with any existing transitions
            if (_existingIds != null && (_existingIds.Any(t =>
                {
                    var transition = t as Transition;
                    return transition != null && ((transition.Charge == Charge) && Equals(transition.CustomIon, ResultCustomIon)) && !ReferenceEquals(t, _initialId);
                })))
            {
                helper.ShowTextBoxError(textName,
                    Resources.EditCustomMoleculeDlg_OkDialog_A_similar_transition_already_exists_, textName.Text);
                return;
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

        private void comboLabelType_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Handle label type selection events, like <Edit list...>
            if (_driverLabelType != null)
                _driverLabelType.SelectedIndexChangedEvent();
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
