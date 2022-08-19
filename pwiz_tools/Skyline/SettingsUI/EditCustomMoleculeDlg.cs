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
using pwiz.Common.Chemistry;
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
        private CustomMolecule _resultCustomMolecule;
        private Adduct _resultAdduct;
        private readonly FormulaBox _formulaBox;
        private readonly Identity _initialId;
        private readonly IEnumerable<Identity> _existingIds;
        private readonly int _minCharge;
        private readonly int _maxCharge;
        private readonly TransitionSettings _transitionSettings;
        private PeptideSettings _peptideSettings;
        private readonly PeptideSettingsUI.LabelTypeComboDriver _driverLabelType;
        private readonly SkylineWindow _parent;
        private readonly UsageMode _usageMode;

        public enum UsageMode
        {
            moleculeNew,
            moleculeEdit,
            precursor,
            fragment
        }

        /// <summary>
        /// For modifying at the Molecule level
        /// </summary>
        public EditCustomMoleculeDlg(SkylineWindow parent, string title,
            SrmSettings settings, CustomMolecule molecule, ExplicitRetentionTimeInfo explicitRetentionTime) :
            this(parent, UsageMode.moleculeEdit, title, null, null, 0, 0, null, molecule, Adduct.EMPTY, null, null,
                explicitRetentionTime, null)
        {
        }

        /// <summary>
        /// For creating at the Molecule level (create molecule and first transition group) or modifying at the transition level
        /// Null values imply "don't ask user for this"
        /// </summary>
        public EditCustomMoleculeDlg(SkylineWindow parent, UsageMode usageMode, string title, Identity initialId,
            IEnumerable<Identity> existingIds, int minCharge, int maxCharge,
            SrmSettings settings, CustomMolecule molecule, Adduct defaultCharge,
            ExplicitTransitionGroupValues explicitTransitionGroupAttributes,
            ExplicitTransitionValues explicitTransitionAttributes,
            ExplicitRetentionTimeInfo explicitRetentionTime,
            IsotopeLabelType defaultIsotopeLabelType)
        {
            Text = title;
            _parent = parent;
            _initialId = initialId;
            _existingIds = existingIds;
            _minCharge = minCharge;
            _maxCharge = maxCharge;
            _transitionSettings = settings != null ? settings.TransitionSettings : null;
            _peptideSettings = settings != null ? settings.PeptideSettings : null;
            _resultAdduct = Adduct.EMPTY;
            _resultCustomMolecule = molecule;
            _usageMode = usageMode;

            var enableFormulaEditing = usageMode == UsageMode.moleculeNew || usageMode == UsageMode.moleculeEdit ||
                                       usageMode == UsageMode.fragment;
            var enableAdductEditing = usageMode == UsageMode.moleculeNew || usageMode == UsageMode.precursor ||
                                      usageMode == UsageMode.fragment;
            var needExplicitTransitionValues = usageMode == UsageMode.fragment;
            var needExplicitTransitionGroupValues = usageMode == UsageMode.moleculeNew || usageMode == UsageMode.precursor;

            InitializeComponent();

            NameText = molecule == null ? String.Empty : molecule.Name;
            textName.Enabled = usageMode == UsageMode.moleculeNew || usageMode == UsageMode.moleculeEdit ||
                               usageMode == UsageMode.fragment; // Can user edit name?

            var needOptionalValuesBox = explicitRetentionTime != null || explicitTransitionGroupAttributes != null || explicitTransitionAttributes != null;

            if (!needExplicitTransitionValues)
            {
                labelCollisionEnergy.Visible = false;
                textCollisionEnergy.Visible = false;
                labelSLens.Visible = false;
                textSLens.Visible = false;
                labelConeVoltage.Visible = false;
                textConeVoltage.Visible = false;
                labelIonMobilityHighEnergyOffset.Visible = false;
                textIonMobilityHighEnergyOffset.Visible = false;
                labelDeclusteringPotential.Visible = false;
                textDeclusteringPotential.Visible = false;
            }

            if (!needExplicitTransitionGroupValues)
            {
                labelPrecursorCollisionEnergy.Visible = false;
                textBoxPrecursorCollisionEnergy.Visible = false;
                labelCCS.Visible = false;
                textBoxCCS.Visible = false;
                labelIonMobility.Visible = false;
                textIonMobility.Visible = false;
                labelIonMobilityUnits.Visible = false;
                comboBoxIonMobilityUnits.Visible = false;
            }

            var heightDelta = 0;

            // Initialise the ion mobility units dropdown with L10N values
            foreach (eIonMobilityUnits t in Enum.GetValues(typeof(eIonMobilityUnits)))
            {
                var displayString = IonMobilityFilter.IonMobilityUnitsL10NString(t);
                if (displayString != null) // Special value eIonMobilityUnits.unknown must not appear in list
                {
                    comboBoxIonMobilityUnits.Items.Add(displayString);
                }
            }

            if (needOptionalValuesBox)
            {
                var newHeight = groupBoxOptionalValues.Height;
                var movers = new List<Control>();
                int offset = 0;
                if (!needExplicitTransitionGroupValues && !needExplicitTransitionValues)
                {
                    // We blanked out everything but the retention time
                    newHeight = labelCollisionEnergy.Location.Y;
                }
                else if (!needExplicitTransitionGroupValues)
                {
                    // We need to shift transition-level items up to where retention time was
                    movers.AddRange(new Control[]{
                        textCollisionEnergy, labelCollisionEnergy, textDeclusteringPotential, labelDeclusteringPotential, textSLens,
                        labelSLens, textConeVoltage, labelConeVoltage, textIonMobilityHighEnergyOffset, labelIonMobilityHighEnergyOffset
                    });
                    labelIonMobilityHighEnergyOffset.Location = labelIonMobility.Location;
                    textIonMobilityHighEnergyOffset.Location = textIonMobility.Location;
                    offset = labelCollisionEnergy.Location.Y - labelRetentionTime.Location.Y;
                    newHeight = textBoxCCS.Location.Y;
                }
                else if (!needExplicitTransitionValues)
                {
                    // We need to shift precursor-level items up to where retention time was
                    movers.AddRange(new Control[]{textBoxCCS, labelCCS, textIonMobility,
                        labelIonMobility, comboBoxIonMobilityUnits, labelIonMobilityUnits, labelPrecursorCollisionEnergy, textBoxPrecursorCollisionEnergy
                    });
                    offset = labelIonMobility.Location.Y - (explicitRetentionTime == null ? labelRetentionTime.Location.Y : labelCollisionEnergy.Location.Y);
                    newHeight = explicitRetentionTime == null ? textSLens.Location.Y : textIonMobility.Location.Y;
                }

                foreach (var mover in movers)
                {
                    mover.Anchor = AnchorStyles.Left | AnchorStyles.Top;
                    mover.Location = new Point(mover.Location.X, mover.Location.Y - offset);
                }

                heightDelta = groupBoxOptionalValues.Height - newHeight;
                groupBoxOptionalValues.Height = newHeight;
            }

            ResultExplicitTransitionGroupValues = explicitTransitionGroupAttributes ?? ExplicitTransitionGroupValues.EMPTY;
            ResultExplicitTransitionValues = new ExplicitTransitionValues(explicitTransitionAttributes);

            string labelAverage = !defaultCharge.IsEmpty
                ? Resources.EditCustomMoleculeDlg_EditCustomMoleculeDlg_A_verage_m_z_
                : Resources.EditCustomMoleculeDlg_EditCustomMoleculeDlg_A_verage_mass_;
            string labelMono = !defaultCharge.IsEmpty
                ? Resources.EditCustomMoleculeDlg_EditCustomMoleculeDlg__Monoisotopic_m_z_
                : Resources.EditCustomMoleculeDlg_EditCustomMoleculeDlg__Monoisotopic_mass_;
            var defaultFormula = molecule == null ? string.Empty : molecule.Formula;
            var transition = initialId as Transition;

            FormulaBox.EditMode editMode;
            if (enableAdductEditing && !enableFormulaEditing)
                editMode = FormulaBox.EditMode.adduct_only;
            else if (!enableAdductEditing && enableFormulaEditing)
                editMode = FormulaBox.EditMode.formula_only;
            else
                editMode = FormulaBox.EditMode.formula_and_adduct;
            string formulaBoxLabel;
            if (defaultCharge.IsEmpty)
            {
                formulaBoxLabel = Resources.EditCustomMoleculeDlg_EditCustomMoleculeDlg_Chemi_cal_formula_;
            }
            else if (editMode == FormulaBox.EditMode.adduct_only)
            {
                var prompt = defaultFormula;
                if (string.IsNullOrEmpty(defaultFormula) && molecule != null)
                {
                    // Defined by mass only
                    prompt = molecule.ToString();
                }
                formulaBoxLabel = string.Format(Resources.EditCustomMoleculeDlg_EditCustomMoleculeDlg_Addu_ct_for__0__,
                    prompt);
            }
            else
            {
                formulaBoxLabel = Resources.EditMeasuredIonDlg_EditMeasuredIonDlg_Ion__chemical_formula_;
            }

            double? averageMass = null;
            double? monoMass = null;
            if (transition != null && string.IsNullOrEmpty(defaultFormula) && transition.IsCustom())
            {
                averageMass = transition.CustomIon.AverageMass;
                monoMass = transition.CustomIon.MonoisotopicMass;
            }
            else if (molecule != null)
            {
                averageMass = molecule.AverageMass;
                monoMass = molecule.MonoisotopicMass;
            }

            _formulaBox =
                new FormulaBox(false, // Not proteomic, so offer Cl and Br in atoms popup
                    formulaBoxLabel,
                    labelAverage,
                    labelMono,
                    defaultCharge,
                    editMode)
                {
                    NeutralFormula = defaultFormula,
                    AverageMass = averageMass,
                    MonoMass = monoMass,
                    Location = new Point(textName.Left, textName.Bottom + 12)
                };
            _formulaBox.ChargeChange += (sender, args) =>
            {
                if (!_formulaBox.Adduct.IsEmpty)
                {
                    Adduct = _formulaBox.Adduct;
                    var revisedFormula = _formulaBox.NeutralFormula + Adduct.AdductFormula;
                    if (!Equals(revisedFormula, _formulaBox.Formula))
                    {
                        _formulaBox.Formula = revisedFormula;
                    }
                    if (string.IsNullOrEmpty(_formulaBox.NeutralFormula) && averageMass.HasValue)
                    {
                        _formulaBox.AverageMass = averageMass;
                        _formulaBox.MonoMass = monoMass;
                    }
                }
            };
            Controls.Add(_formulaBox);
            _formulaBox.TabIndex = 2;
            _formulaBox.Enabled = enableFormulaEditing || enableAdductEditing;
            Adduct = defaultCharge;
            var needCharge = !Adduct.IsEmpty;
            textCharge.Visible = labelCharge.Visible = needCharge;
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
                _driverLabelType = new PeptideSettingsUI.LabelTypeComboDriver(PeptideSettingsUI.LabelTypeComboDriver.UsageType.InternalStandardPicker, comboIsotopeLabelType,
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

        public EditCustomMoleculeSettings CustomMoleculeSettings
        {
            get { return new EditCustomMoleculeSettings(this); }
        }

        public class EditCustomMoleculeSettings
        {
            public EditCustomMoleculeSettings(EditCustomMoleculeDlg dlg) : this(
                dlg.NameText, dlg.FormulaBox.DisplayFormula, dlg.FormulaBox.MonoMass, dlg.FormulaBox.AverageMass,
                int.Parse(dlg.textCharge.Text), dlg.IsotopeLabelType, new ExplicitValues(dlg))
            {
            }

            public EditCustomMoleculeSettings(string name, string formula, double? monoisotopicMz, double? averageMz,
                int charge, IsotopeLabelType labelType, ExplicitValues optionalExplicitValues)
            {
                Name = name;
                Formula = formula;
                MonoisotopicMz = monoisotopicMz;
                AverageMz = averageMz;
                Charge = charge;
                LabelType = labelType;
                OptionalExplicitValues = optionalExplicitValues;
            }

            [Track]
            public string Name { get; private set; }
            // TODO: custom localizer
            [Track]
            public string Formula { get; private set; }
            [Track]
            public double? MonoisotopicMz { get; private set; }
            [Track]
            public double? AverageMz { get; private set; }
            [Track]
            public int Charge { get; private set; }
            [Track]
            public IsotopeLabelType LabelType { get; private set; }

            [TrackChildren]
            public ExplicitValues OptionalExplicitValues { get; private set; }

            public class ExplicitValues
            {
                public ExplicitValues(EditCustomMoleculeDlg dlg) : this(dlg.ResultRetentionTimeInfo,
                    dlg.ResultExplicitTransitionGroupValues, dlg.ResultExplicitTransitionValues)
                {
                }
                

                public ExplicitValues(ExplicitRetentionTimeInfo resultRetentionTimeInfo,
                    ExplicitTransitionGroupValues resultExplicitTransitionGroupValues,
                    ExplicitTransitionValues resultExplicitTransitionValues)
                {
                    ResultRetentionTimeInfo = resultRetentionTimeInfo;
                    ResultExplicitTransitionGroupValues = resultExplicitTransitionGroupValues;
                    ResultExplicitTransitionValues = resultExplicitTransitionValues;
                }

                [TrackChildren(ignoreName:true)]
                public ExplicitRetentionTimeInfo ResultRetentionTimeInfo { get; private set; }
                [TrackChildren(ignoreName: true)]
                public ExplicitTransitionGroupValues ResultExplicitTransitionGroupValues { get; private set; }
                [TrackChildren(ignoreName: true)]
                public ExplicitTransitionValues ResultExplicitTransitionValues { get; private set; }
            }
        }

        public CustomMolecule ResultCustomMolecule
        {
            get { return _resultCustomMolecule; }
        }

        public Adduct ResultAdduct
        {
            get { return _resultAdduct; }
        }

        public void SetResult(CustomMolecule mol, Adduct adduct)
        {
            _resultCustomMolecule = mol;
            _resultAdduct = adduct;
            SetNameAndFormulaBoxText();
        }

        public ExplicitTransitionGroupValues ResultExplicitTransitionGroupValues
        {
            get
            {
                var val = ExplicitTransitionGroupValues.Create(PrecursorCollisionEnergy, 
                    IonMobility,
                    IonMobilityUnits,
                    CollisionalCrossSectionSqA);
                return val;
            }
            set
            {
                var resultExplicitTransitionGroupValues = value ?? ExplicitTransitionGroupValues.EMPTY;
                PrecursorCollisionEnergy = resultExplicitTransitionGroupValues.CollisionEnergy;
                IonMobility = resultExplicitTransitionGroupValues.IonMobility;
                IonMobilityUnits = resultExplicitTransitionGroupValues.IonMobilityUnits;
                CollisionalCrossSectionSqA = resultExplicitTransitionGroupValues.CollisionalCrossSectionSqA;
            }
        }

        public ExplicitTransitionValues ResultExplicitTransitionValues
        {
            get
            {
                return ExplicitTransitionValues.Create(CollisionEnergy, IonMobilityHighEnergyOffset, SLens, ConeVoltage, DeclusteringPotential);
            }
            set
            {
                // Use constructor to handle value == null
                var resultExplicitTransitionValues = new ExplicitTransitionValues(value);
                CollisionEnergy = resultExplicitTransitionValues.CollisionEnergy;
                IonMobilityHighEnergyOffset = resultExplicitTransitionValues.IonMobilityHighEnergyOffset;
                SLens = resultExplicitTransitionValues.SLens;
                ConeVoltage = resultExplicitTransitionValues.ConeVoltage;
                DeclusteringPotential = resultExplicitTransitionValues.DeclusteringPotential;
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

        public Adduct Adduct
        {
            get
            {
                if (!_formulaBox.Adduct.IsEmpty)
                    return _formulaBox.Adduct;
                Adduct val;
                if (Adduct.TryParse(textCharge.Text, out val))
                    return val;
                return Adduct.EMPTY;
            }
            set
            {
                _formulaBox.Adduct = value;
                if (value.IsEmpty)
                {
                    textCharge.Text = string.Empty;
                }
                else
                {
                    textCharge.Text =
                        value.AdductCharge.ToString(LocalizationHelper
                            .CurrentCulture); // If adduct is "M+Na", show charge as "1"
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

        private static string EmptyForNullOrZero(double? value)
        {
            double dval = (value ?? 0);
            return (dval == 0) ? string.Empty : dval.ToString(LocalizationHelper.CurrentCulture);
        }

        public double? CollisionEnergy
        {
            get { return NullForEmpty(textCollisionEnergy.Text); }
            set
            {
                Assume.IsTrue(_usageMode == UsageMode.fragment || value == null); // Make sure tests are testing the proper UI
                textCollisionEnergy.Text = EmptyForNullOrNonPositive(value);
            }
        }

        public double? DeclusteringPotential
        {
            get { return NullForEmpty(textDeclusteringPotential.Text); }
            set
            {
                Assume.IsTrue(_usageMode == UsageMode.fragment || value == null); // Make sure tests are testing the proper UI
                textDeclusteringPotential.Text = EmptyForNullOrNonPositive(value);
            }
        }

        public double? SLens
        {
            get { return NullForEmpty(textSLens.Text); }
            set
            {
                Assume.IsTrue(_usageMode == UsageMode.fragment || value == null); // Make sure tests are testing the proper UI
                textSLens.Text = EmptyForNullOrNonPositive(value);
            }
        }

        public double? ConeVoltage
        {
            get { return NullForEmpty(textConeVoltage.Text); }
            set
            {
                Assume.IsTrue(_usageMode == UsageMode.fragment || value == null); // Make sure tests are testing the proper UI
                textConeVoltage.Text = EmptyForNullOrNonPositive(value);
            }
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

        public double? IonMobility
        {
            get { return NullForEmpty(textIonMobility.Text); }
            set { textIonMobility.Text = EmptyForNullOrZero(value); }
        }

        private void PopulateIonMobilityUnits()
        {
            if (!string.IsNullOrEmpty(textIonMobility.Text) && Equals(IonMobilityUnits, eIonMobilityUnits.none))
            {
                // Try to set a reasonable value for ion mobility units

                // First look for any other explicit ion mobility values in the document
                var doc = _parent?.Document;
                var node =
                    doc?.MoleculeTransitionGroups.FirstOrDefault(n =>
                        n.ExplicitValues.IonMobilityUnits != eIonMobilityUnits.none);
                if (node != null)
                {
                    IonMobilityUnits = node.ExplicitValues.IonMobilityUnits;
                    return;
                }

                // Then try the ion mobility library if any
                var filters = doc?.Settings.TransitionSettings.IonMobilityFiltering;
                if (filters != null)
                {
                    IonMobilityUnits = filters.GetFirstSeenIonMobilityUnits();
                }
            }
        }

        public double? IonMobilityHighEnergyOffset
        {
            get { return NullForEmpty(textIonMobilityHighEnergyOffset.Text); }
            set
            {
                Assume.IsTrue(_usageMode == UsageMode.fragment || value == null); // Make sure tests are testing the proper UI
                textIonMobilityHighEnergyOffset.Text = value == null
                    ? string.Empty
                    : value.Value.ToString(LocalizationHelper.CurrentCulture);
            } // Negative values are normal here
        }

        public eIonMobilityUnits IonMobilityUnits
        {
            get
            {
                return comboBoxIonMobilityUnits.SelectedIndex >= 0
                    ? (eIonMobilityUnits) comboBoxIonMobilityUnits.SelectedIndex
                    : eIonMobilityUnits.none;
            }
            set { comboBoxIonMobilityUnits.SelectedIndex = (int) value; }
        }

        public double? PrecursorCollisionEnergy
        {
            get { return NullForEmpty(textBoxPrecursorCollisionEnergy.Text); }
            set
            {
                textBoxPrecursorCollisionEnergy.Text = value.HasValue && !value.Value.Equals(0)
                    ? value.Value.ToString(LocalizationHelper.CurrentCulture)
                    : string.Empty;
            }
        }

        public double? CollisionalCrossSectionSqA
        {
            get { return NullForEmpty(textBoxCCS.Text); }
            set { textBoxCCS.Text = EmptyForNullOrNonPositive(value); }
        }

        public IsotopeLabelType IsotopeLabelType
        {
            get { return (_driverLabelType == null) ? null : _driverLabelType.SelectedMods.LabelType; }
            set
            {
                if (_driverLabelType != null) _driverLabelType.SelectedName = value.Name;
            }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);
            var charge = 0;
            if (textCharge.Visible &&
                !helper.ValidateSignedNumberTextBox(textCharge, _minCharge, _maxCharge, out charge))
                return;
            var adduct = Adduct.NonProteomicProtonatedFromCharge(charge);
            if (RetentionTimeWindow.HasValue && !RetentionTime.HasValue)
            {
                helper.ShowTextBoxError(textRetentionTimeWindow,
                    Resources
                        .Peptide_ExplicitRetentionTimeWindow_Explicit_retention_time_window_requires_an_explicit_retention_time_value_);
                return;
            }
            if (Adduct.IsEmpty || Adduct.AdductCharge != adduct.AdductCharge)
                Adduct =
                    adduct; // Note: order matters here, this settor indirectly updates _formulaBox.MonoMass when formula is empty
            if (string.IsNullOrEmpty(_formulaBox.NeutralFormula))
            {
                // Can the text fields be understood as mz?
                if (!_formulaBox.ValidateAverageText(helper))
                    return;
                if (!_formulaBox.ValidateMonoText(helper))
                    return;
            }
            var monoMass = new TypedMass(_formulaBox.MonoMass ?? 0, MassType.Monoisotopic);
            var averageMass = new TypedMass(_formulaBox.AverageMass ?? 0, MassType.Average);
            if (monoMass < CustomMolecule.MIN_MASS || averageMass < CustomMolecule.MIN_MASS)
            {
                _formulaBox.ShowTextBoxErrorFormula(helper,
                    string.Format(
                        Resources
                            .EditCustomMoleculeDlg_OkDialog_Custom_molecules_must_have_a_mass_greater_than_or_equal_to__0__,
                        CustomMolecule.MIN_MASS));
                return;
            }
            if (monoMass > CustomMolecule.MAX_MASS || averageMass > CustomMolecule.MAX_MASS)
            {
                _formulaBox.ShowTextBoxErrorFormula(helper,
                    string.Format(
                        Resources
                            .EditCustomMoleculeDlg_OkDialog_Custom_molecules_must_have_a_mass_less_than_or_equal_to__0__,
                        CustomMolecule.MAX_MASS));
                return;
            }

            if ((_transitionSettings != null) &&
                (!_transitionSettings.IsMeasurablePrecursor(
                     adduct.MzFromNeutralMass(monoMass, MassType.Monoisotopic)) ||
                 !_transitionSettings.IsMeasurablePrecursor(adduct.MzFromNeutralMass(averageMass, MassType.Average))))
            {
                _formulaBox.ShowTextBoxErrorFormula(helper,
                    Resources
                        .SkylineWindow_AddMolecule_The_precursor_m_z_for_this_molecule_is_out_of_range_for_your_instrument_settings_);
                return;
            }

            // Ion mobility value must have ion mobility units
            if (textIonMobility.Visible && IonMobility.HasValue)
            {
                if (IonMobilityUnits == eIonMobilityUnits.none)
                {
                    helper.ShowTextBoxError(textIonMobility, Resources.EditCustomMoleculeDlg_OkDialog_Please_specify_the_ion_mobility_units_);
                    comboBoxIonMobilityUnits.Focus();
                    return;
                }

                if (IonMobility.Value == 0 ||
                    (IonMobility.Value < 0 && !IonMobilityFilter.AcceptNegativeMobilityValues(IonMobilityUnits)))
                {
                    helper.ShowTextBoxError(textIonMobility, 
                        string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_value__0_, IonMobility));
                    textIonMobility.Focus();
                    return;
                }
            }
            if (_usageMode == UsageMode.precursor)
            {
                // Only the adduct should be changing
                SetResult(_resultCustomMolecule, Adduct);
            }
            else if (!string.IsNullOrEmpty(_formulaBox.NeutralFormula))
            {
                try
                {
                    var name = textName.Text;
                    if (string.IsNullOrEmpty(name))
                    {
                        name = _formulaBox.NeutralFormula; // Clip off any adduct description
                    }
                    SetResult(new CustomMolecule(_formulaBox.NeutralFormula, name), Adduct);
                }
                catch (InvalidDataException x)
                {
                    _formulaBox.ShowTextBoxErrorFormula(helper, x.Message);
                    return;
                }
            }
            else
            {
                SetResult(new CustomMolecule(monoMass, averageMass, textName.Text), Adduct);
            }
            // Did user change the list of heavy labels?
            if (_driverLabelType != null)
            {
                // This is the only thing the user may have altered
                var newHeavyMods = _driverLabelType.GetHeavyModifications().ToArray();
                if (!ArrayUtil.EqualsDeep(newHeavyMods, _peptideSettings.Modifications.HeavyModifications))
                {
                    var labelTypes = _peptideSettings.Modifications.InternalStandardTypes.Where(t =>
                        newHeavyMods.Any(m => Equals(m.LabelType, t))).ToArray();
                    if (labelTypes.Length == 0)
                        labelTypes = new[] {newHeavyMods.First().LabelType};

                    PeptideModifications modifications = new PeptideModifications(
                        _peptideSettings.Modifications.StaticModifications,
                        _peptideSettings.Modifications.MaxVariableMods,
                        _peptideSettings.Modifications.MaxNeutralLosses,
                        newHeavyMods,
                        labelTypes);
                    var settings = _peptideSettings.ChangeModifications(modifications);
                    SrmSettings newSettings = _parent.DocumentUI.Settings.ChangePeptideSettings(settings);
                    if (!_parent.ChangeSettings(newSettings, true))
                    {
                        // Not expected, since we checked for a change before calling
                        // Otherwise, this is very confusing. The form just refuses to go away
                        // We would prefer to get an unhandled exception and fix this
                        Assume.Fail();
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
                       Equals(transitionGroup.PrecursorAdduct.AsFormula(),
                           Adduct
                               .AsFormula()) && // Compare AsFormula so proteomic and non-proteomic protonation are seen as same thing
                       !ReferenceEquals(t, _initialId);
            }))
            {
                helper.ShowTextBoxError(textName,
                    Resources
                        .EditCustomMoleculeDlg_OkDialog_A_precursor_with_that_adduct_and_label_type_already_exists_,
                    textName.Text);
                return;
            }

            // See if this would conflict with any existing transitions
            if (_existingIds != null && (_existingIds.Any(t =>
            {
                var transition = t as Transition;
                return transition != null && (Equals(transition.Adduct.AsFormula(), Adduct.AsFormula()) &&
                                              Equals(transition.CustomIon, ResultCustomMolecule)) &&
                       !ReferenceEquals(t, _initialId);
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
            if (ResultCustomMolecule == null)
            {
                _formulaBox.Formula = string.Empty;
                _formulaBox.AverageMass = null;
                _formulaBox.MonoMass = null;
                textName.Text = string.Empty;
            }
            else
            {
                textName.Text = ResultCustomMolecule.Name ?? string.Empty;
                var displayFormula = ResultCustomMolecule.Formula ?? string.Empty;
                _formulaBox.Formula = displayFormula + (ResultAdduct.IsEmpty || ResultAdduct.IsProteomic
                                          ? string.Empty
                                          : ResultAdduct.AdductFormula);
                if (ResultCustomMolecule.Formula == null)
                {
                    _formulaBox.AverageMass = ResultCustomMolecule.AverageMass;
                    _formulaBox.MonoMass = ResultCustomMolecule.MonoisotopicMass;
                }
            }
        }

        private void textCharge_TextChanged(object sender, EventArgs e)
        {
            var helper = new MessageBoxHelper(this, false);
            int charge;
            if (!helper.ValidateSignedNumberTextBox(textCharge, _minCharge, _maxCharge, out charge))
            {
                return; // Not yet clear what the user has in mind
            }
            if (Adduct.IsEmpty || Adduct.AdductCharge != charge)
            {
                Adduct =
                    Adduct
                        .ChangeCharge(
                            charge); // Update the adduct with this new charge - eg for new charge 2, [M+Na] -> [M+2Na] 
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void comboLabelType_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Handle label type selection events, like <Edit list...>
            if (_driverLabelType != null)
            {
                _driverLabelType.SelectedIndexChangedEvent();
                if (_driverLabelType.SelectedMods.Modifications.Any(m => m.LabelAtoms != LabelAtoms.None))
                {
                    var dict = new Dictionary<string, string>();
                    foreach (var m in _driverLabelType.SelectedMods.Modifications.Where(
                        m => m.LabelAtoms != LabelAtoms.None))
                    {
                        foreach (var l in m.LabelNames)
                        {
                            string formulaStripped = BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(l);
                            if (!dict.ContainsKey(formulaStripped))
                            {
                                dict.Add(formulaStripped, l);
                            }
                        }
                    }
                    FormulaBox.IsotopeLabelsForMassCalc = dict;
                }
                else
                {
                    FormulaBox.IsotopeLabelsForMassCalc = null;
                }
            }
        }

        private void textIonMobility_TextChanged(object sender, EventArgs e)
        {
            PopulateIonMobilityUnits(); // Try to set reasonable ion mobility units if user is adding an ion mobility value
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