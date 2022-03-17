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
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class TransitionSettingsUI : FormEx, IMultipleViewProvider
    {
// ReSharper disable InconsistentNaming
        public enum TABS { Prediction, Filter, Library, Instrument, FullScan, IonMobility }
// ReSharper restore InconsistentNaming

        public class PredictionTab : IFormView {}
        public class FilterTab : IFormView {}
        public class IonMobilityTab : IFormView { }
        public class LibraryTab : IFormView {}
        public class InstrumentTab : IFormView {}
        public class FullScanTab : IFormView {}

        private static readonly IFormView[] TAB_PAGES =
        {
            new PredictionTab(), new FilterTab(), new LibraryTab(), new InstrumentTab(), new FullScanTab(), new IonMobilityTab()
        };

        private readonly SkylineWindow _parent;
        private TransitionSettings _transitionSettings;

        private readonly SettingsListComboDriver<CollisionEnergyRegression> _driverCE;
        private readonly SettingsListComboDriver<DeclusteringPotentialRegression> _driverDP;
        private readonly SettingsListComboDriver<CompensationVoltageParameters> _driverCoV;
        private readonly SettingsListComboDriver<OptimizationLibrary> _driverOptimizationLibrary;
        private readonly SettingsListBoxDriver<MeasuredIon> _driverIons;
        public const double DEFAULT_TIME_AROUND_MS2_IDS = 5;
        public const double DEFAULT_TIME_AROUND_PREDICTION = 5;
        private readonly int _lower_margin;
        private IonType[] InitialPeptideIonTypes;
        private IonType[] InitialSmallMoleculeIonTypes;

        public TransitionSettingsUI(SkylineWindow parent)
        {
            InitializeComponent();

            // Populate the fragment finder combo boxes
            foreach (string item in TransitionFilter.GetStartFragmentFinderLabels())
                comboRangeFrom.Items.Add(item);
            foreach (string item in TransitionFilter.GetEndFragmentFinderLabels())
                comboRangeTo.Items.Add(item);

            _parent = parent;
            _transitionSettings = _parent.DocumentUI.Settings.TransitionSettings;

            // Populate the small mol adduct filter helper menu
            AppendAdductMenus(contextMenuStripPrecursorAdduct, precursorAdductStripMenuItem_Click);
            AppendAdductMenus(contextMenuStripFragmentAdduct, fragmentAdductStripMenuItem_Click);
            Bitmap bm = Resources.PopupBtn;
            bm.MakeTransparent(Color.Fuchsia);
            btnPrecursorAdduct.Image = bm;
            btnFragmentAdduct.Image = bm;

            // Initialize prediction settings
            comboPrecursorMass.SelectedItem = Prediction.PrecursorMassType.GetLocalizedString();
            comboIonMass.SelectedItem = Prediction.FragmentMassType.GetLocalizedString();

            _driverCE = new SettingsListComboDriver<CollisionEnergyRegression>(comboCollisionEnergy,
                                                                               Settings.Default.CollisionEnergyList);
            string sel = (Prediction.CollisionEnergy == null ? null : Prediction.CollisionEnergy.Name);
            _driverCE.LoadList(sel);

            _driverDP = new SettingsListComboDriver<DeclusteringPotentialRegression>(comboDeclusterPotential,
                                                                                     Settings.Default.DeclusterPotentialList);
            sel = (Prediction.DeclusteringPotential == null ? null : Prediction.DeclusteringPotential.Name);
            _driverDP.LoadList(sel);

            _driverCoV = new SettingsListComboDriver<CompensationVoltageParameters>(comboCompensationVoltage,
                                                                                    Settings.Default.CompensationVoltageList);
            _driverCoV.LoadList(Prediction.CompensationVoltage == null ? null : Prediction.CompensationVoltage.Name);

            _driverOptimizationLibrary = new SettingsListComboDriver<OptimizationLibrary>(comboOptimizationLibrary,
                Settings.Default.OptimizationLibraryList);
            _driverOptimizationLibrary.LoadList(Prediction.OptimizedLibrary == null ? null : Prediction.OptimizedLibrary.Name);

            if (Prediction.OptimizedMethodType == OptimizedMethodType.None)
                comboOptimizeType.SelectedIndex = 0;
            else
            {
                cbUseOptimized.Checked = true;
                comboOptimizeType.SelectedItem = Prediction.OptimizedMethodType.GetLocalizedString();
            }

            // Initialize filter settings
            textPeptidePrecursorCharges.Text = Filter.PeptidePrecursorChargesString;
            textPeptideIonCharges.Text = Filter.PeptideProductChargesString;
            textPeptideIonTypes.Text = Filter.PeptideIonTypesString;
            InitialPeptideIonTypes = Filter.PeptideIonTypes.ToArray();
            textSmallMoleculeIonTypes.Text = Filter.SmallMoleculeIonTypesString;
            InitialSmallMoleculeIonTypes = Filter.SmallMoleculeIonTypes.ToArray();
            textSmallMoleculePrecursorAdducts.Text = Filter.SmallMoleculePrecursorAdductsString;
            textSmallMoleculeFragmentAdducts.Text = Filter.SmallMoleculeFragmentAdductsString;
            comboRangeFrom.SelectedItem = Filter.FragmentRangeFirst.Label;
            comboRangeTo.SelectedItem = Filter.FragmentRangeLast.Label;
            textExclusionWindow.Text = Filter.PrecursorMzWindow != 0
                                           ? Filter.PrecursorMzWindow.ToString(LocalizationHelper.CurrentCulture)
                                           : string.Empty;
            cbExclusionUseDIAWindow.Checked = Filter.ExclusionUseDIAWindow;
            cbAutoSelect.Checked = Filter.AutoSelect;

            _driverIons = new MeasuredIonListBoxDriver(listAlwaysAdd, Settings.Default.MeasuredIonList);
            _driverIons.LoadList(Filter.MeasuredIons);

            // Initialize library settings
            cbLibraryPick.Checked = (Libraries.Pick != TransitionLibraryPick.none);
            panelPick.Visible = cbLibraryPick.Checked;
            textTolerance.Text = Libraries.IonMatchTolerance.ToString(LocalizationHelper.CurrentCulture);
            textMinIonCount.Text = Libraries.MinIonCount != 0 ? Libraries.MinIonCount.ToString(LocalizationHelper.CurrentCulture) : string.Empty;
            textIonCount.Text = Libraries.IonCount.ToString(LocalizationHelper.CurrentCulture);
            if (Libraries.Pick == TransitionLibraryPick.filter)
                radioFiltered.Checked = true;
            else if (Libraries.Pick == TransitionLibraryPick.all_plus)
                radioAllAndFiltered.Checked = true;

            // Initialize instrument settings
            textMinMz.Text = Instrument.MinMz.ToString(LocalizationHelper.CurrentCulture);
            textMaxMz.Text = Instrument.MaxMz.ToString(LocalizationHelper.CurrentCulture);
            cbDynamicMinimum.Checked = Instrument.IsDynamicMin;
            textMzMatchTolerance.Text = Instrument.MzMatchTolerance.ToString(LocalizationHelper.CurrentCulture);
            if (Instrument.MaxTransitions.HasValue)
                textMaxTrans.Text = Instrument.MaxTransitions.Value.ToString(LocalizationHelper.CurrentCulture);
            if (Instrument.MaxInclusions.HasValue)
                textMaxInclusions.Text = Instrument.MaxInclusions.Value.ToString(LocalizationHelper.CurrentCulture);
            if (Instrument.MinTime.HasValue)
                textMinTime.Text = Instrument.MinTime.Value.ToString(LocalizationHelper.CurrentCulture);
            if (Instrument.MaxTime.HasValue)
                textMaxTime.Text = Instrument.MaxTime.Value.ToString(LocalizationHelper.CurrentCulture);

            // Initialize full-scan settings
            FullScanSettingsControl = new FullScanSettingsControl(_parent)
                                          {
                                              Anchor = (AnchorStyles.Top | AnchorStyles.Left),
                                              Location = new Point(0, 0),
                                              Size = new Size(363, 491)
                                          };
            FullScanSettingsControl.IsolationSchemeChangedEvent += IsolationSchemeChanged;
            FullScanSettingsControl.FullScanEnabledChanged += OnFullScanEnabledChanged; // Adjusts small molecule ion settings when full scan settings change
            FullScanSettingsControl.AcquisitionMethodChanged += FullScanSettingsControl_OnAcquisitionMethodChanged;
            tabFullScan.Controls.Add(FullScanSettingsControl);

            // VISUAL:
            // - Store the distance between the cbExclusionDIAWindow and the lower end of the box (to use as margin later)
            // - Shift the cbExclusionDIAWindow down (it is only higher in the designer for better manipulation)
            _lower_margin = groupBox1.Height - textExclusionWindow.Location.Y - textExclusionWindow.Height;
            int pixelShift = cbExclusionUseDIAWindow.Location.Y - lbPrecursorMzWindow.Location.Y;
            cbExclusionUseDIAWindow.Location = new Point(cbExclusionUseDIAWindow.Location.X, cbExclusionUseDIAWindow.Location.Y - pixelShift);

            // Declare list of controls that are inherently proteomic and should not receive the "peptide"->"molecule" treatment in small molecule UI mode
            if (ModeUI == SrmDocument.DOCUMENT_TYPE.proteomic)
                tabControlPeptidesSmallMols.SelectedIndex = 0;
            else if (ModeUI == SrmDocument.DOCUMENT_TYPE.small_molecules)
                tabControlPeptidesSmallMols.SelectedIndex = 1;

            // Initialise ion mobility filtering settings
            ionMobilityFilteringControl.InitializeSettings(_parent);

            DoIsolationSchemeChanged();
            cbxTriggeredAcquisition.Checked = Instrument.TriggeredAcquisition;
        }

        public const double SureQuantMzMatchTolerance = 0.007;
        private void FullScanSettingsControl_OnAcquisitionMethodChanged()
        {
            if (FullScanSettingsControl.AcquisitionMethod == FullScanAcquisitionMethod.SureQuant)
            {
                MZMatchTolerance = SureQuantMzMatchTolerance;
                TriggeredAcquisition = true;
            }
        }

        /// <summary>
        /// Callback function which gets called when the user changes the isolation scheme in the full scan tab
        /// Note: this function should only be called iff the user manually changed the Isolation Scheme
        /// </summary>
        private void IsolationSchemeChanged(object sender, EventArgs e)
        {
            DoIsolationSchemeChanged();
            // Setting default value (true) for ExclusionUseDIAWindow if the setting was changed
            // and if preselected windows are enabled
            // (but not if the user simply loaded settings)
            cbExclusionUseDIAWindow.Checked = FullScanSettingsControl.IsDIAAndPreselectedWindows();
        }

        /// <summary>
        /// Perform the visual changes in the Filter tab depending on which acquisition method is selected in the FullScan tab
        /// - hides the precursor exclusion field and shows the exclusion DIA checkbox for DIA methods 
        /// - shows the precursor exclusion field and hides the exclusion DIA checkbox for other methods
        /// </summary>
        private void DoIsolationSchemeChanged()
        {
            if (FullScanSettingsControl.IsDIA())
            {
                // VISUAL - Show GUI elements relevant to DIA
                cbExclusionUseDIAWindow.Visible = true;
                textExclusionWindow.Visible = false;
                lbMZ.Visible = false;
                lbPrecursorMzWindow.Visible = false;

                // VISUAL - Move the lower boundary of the box and the cbAutoSelect up
                int oldLowerMargin = groupBox1.Height - cbExclusionUseDIAWindow.Location.Y - cbExclusionUseDIAWindow.Height;
                int pixelShift = oldLowerMargin - _lower_margin;
                cbAutoSelect.Location = new Point(cbAutoSelect.Location.X, cbAutoSelect.Location.Y - pixelShift);
                groupBox1.Size = new Size(groupBox1.Size.Width, groupBox1.Size.Height - pixelShift);

                textExclusionWindow.Text = string.Empty; // reset data field
            }
            else
            {
                // VISUAL - Show GUI elements relevant to non-DIA methods or DIA methods not from results
                cbExclusionUseDIAWindow.Visible = false;
                textExclusionWindow.Visible = true;
                lbMZ.Visible = true;
                lbPrecursorMzWindow.Visible = true;

                // VISUAL - Move the lower boundary of the box and the cbAutoSelect down
                int oldLowerMargin = groupBox1.Height - textExclusionWindow.Location.Y - textExclusionWindow.Height;
                int pixelShift = oldLowerMargin - _lower_margin;
                cbAutoSelect.Location = new Point(cbAutoSelect.Location.X, cbAutoSelect.Location.Y - pixelShift);
                groupBox1.Size = new Size(groupBox1.Size.Width, groupBox1.Size.Height - pixelShift);

                cbExclusionUseDIAWindow.Checked = false; // reset data field
            }
        }

        private FullScanSettingsControl FullScanSettingsControl { get; set; }
        public IonMobilityFilteringUserControl IonMobilityControl { get { return ionMobilityFilteringControl; } }

        public TransitionPrediction Prediction { get { return _transitionSettings.Prediction; } }
        public TransitionFilter Filter { get { return _transitionSettings.Filter; } }
        public TransitionLibraries Libraries { get { return _transitionSettings.Libraries; } }
        public TransitionInstrument Instrument { get { return _transitionSettings.Instrument; } }
        public TransitionFullScan FullScan { get { return _transitionSettings.FullScan; } }
        public TransitionIonMobilityFiltering IonMobility { get { return _transitionSettings.IonMobilityFiltering; } }
        public TABS? TabControlSel { get; set; }

        public FullScanAcquisitionMethod AcquisitionMethod
        {
            get { return FullScanSettingsControl.AcquisitionMethod; }
            set { FullScanSettingsControl.AcquisitionMethod = value; }
        }

        public ComboBox ComboAcquisitionMethod
        {
            get { return FullScanSettingsControl.ComboAcquisitionMethod; }
        }

        public FullScanMassAnalyzerType ProductMassAnalyzer
        {
            get { return FullScanSettingsControl.ProductMassAnalyzer; }
            set { FullScanSettingsControl.ProductMassAnalyzer = value; }
        }

        public FullScanPrecursorIsotopes PrecursorIsotopesCurrent
        {
            get { return FullScanSettingsControl.PrecursorIsotopesCurrent; }
            set { FullScanSettingsControl.PrecursorIsotopesCurrent = value; }
        }

        public FullScanMassAnalyzerType PrecursorMassAnalyzer
        {
            get { return FullScanSettingsControl.PrecursorMassAnalyzer; }
            set { FullScanSettingsControl.PrecursorMassAnalyzer = value; }
        }

        public RetentionTimeFilterType RetentionTimeFilterType
        {
            get { return FullScanSettingsControl.RetentionTimeFilterType; }
            set { FullScanSettingsControl.RetentionTimeFilterType = value; }
        }

        public double? ProductRes
        {
            get { return FullScanSettingsControl.ProductRes; }
            set { FullScanSettingsControl.ProductRes = value; }
        }

        public double? ProductResMz
        {
            get { return FullScanSettingsControl.ProductResMz; }
            set { FullScanSettingsControl.ProductResMz = value; }
        }

        public double? PrecursorRes
        {
            get { return FullScanSettingsControl.PrecursorRes; }
            set { FullScanSettingsControl.PrecursorRes = value; }
        }

        public double? PrecursorResMz
        {
            get { return FullScanSettingsControl.PrecursorResMz; }
            set { FullScanSettingsControl.PrecursorResMz = value; }
        }

        public bool IgnoreSimScans
        {
            get { return FullScanSettingsControl.IgnoreSimScans; }
            set { FullScanSettingsControl.IgnoreSimScans = value; }
        }

        public bool UseSelectiveExtraction
        {
            get { return FullScanSettingsControl.UseSelectiveExtraction; }
            set { FullScanSettingsControl.UseSelectiveExtraction = value; }
        }

        protected override void OnShown(EventArgs e)
        {
            if (TabControlSel != null)
            {
                tabControl1.SelectedIndex = (int)TabControlSel;
            }
            tabControl1.FocusFirstTabStop();
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            // Validate and store prediction settings
            string massType = comboPrecursorMass.SelectedItem.ToString();
            MassType precursorMassType = MassTypeExtension.GetEnum(massType);
            massType = comboIonMass.SelectedItem.ToString();
            MassType fragmentMassType = MassTypeExtension.GetEnum(massType);
            string nameCE = comboCollisionEnergy.SelectedItem.ToString();
            CollisionEnergyRegression collisionEnergy =
                Settings.Default.GetCollisionEnergyByName(nameCE);
            string nameDP = comboDeclusterPotential.SelectedItem.ToString();
            DeclusteringPotentialRegression declusteringPotential =
                Settings.Default.GetDeclusterPotentialByName(nameDP);
            string nameCoV = comboCompensationVoltage.SelectedItem.ToString();
            CompensationVoltageParameters compensationVoltage =
                Settings.Default.GetCompensationVoltageByName(nameCoV);
            string nameOptLib = comboOptimizationLibrary.SelectedItem.ToString();
            OptimizationLibrary optimizationLibrary =
                Settings.Default.GetOptimizationLibraryByName(nameOptLib);
            OptimizedMethodType optimizedMethodType = OptimizedMethodType.None;
            if (cbUseOptimized.Checked)
            {
                optimizedMethodType = OptimizedMethodTypeExtension.GetEnum(comboOptimizeType.SelectedItem.ToString());
            }
            TransitionPrediction prediction = new TransitionPrediction(precursorMassType,
                                                                       fragmentMassType, collisionEnergy,
                                                                       declusteringPotential,
                                                                       compensationVoltage,
                                                                       optimizationLibrary,
                                                                       optimizedMethodType);
            Helpers.AssignIfEquals(ref prediction, Prediction);

            // Validate and store filter settings
            Adduct[] peptidePrecursorCharges;
            int min = TransitionGroup.MIN_PRECURSOR_CHARGE;
            int max = TransitionGroup.MAX_PRECURSOR_CHARGE;
            if (!TransitionSettingsControl.ValidateAdductListTextBox(helper, textPeptidePrecursorCharges, true,
                    min, max, out peptidePrecursorCharges))
                return;
            peptidePrecursorCharges = peptidePrecursorCharges.Distinct().ToArray();

            Adduct[] peptideProductCharges;
            min = Transition.MIN_PRODUCT_CHARGE;
            max = Transition.MAX_PRODUCT_CHARGE;
            if (!TransitionSettingsControl.ValidateAdductListTextBox(helper, textPeptideIonCharges, true,
                    min, max, out peptideProductCharges))
                return;
            peptideProductCharges = peptideProductCharges.Distinct().ToArray();

            Adduct[] smallMoleculeProductCharges;
            min = Transition.MIN_PRODUCT_CHARGE;
            max = Transition.MAX_PRODUCT_CHARGE;
            if (!TransitionSettingsControl.ValidateAdductListTextBox(helper, textSmallMoleculeFragmentAdducts, false,
                min, max, out smallMoleculeProductCharges))
                return;
            smallMoleculeProductCharges = smallMoleculeProductCharges.Distinct().ToArray();

            IonType[] peptideIonTypes = TransitionFilter.ParseTypes(textPeptideIonTypes.Text, new IonType[0]);
            if (peptideIonTypes.Length == 0)
            {
                helper.ShowTextBoxError(textPeptideIonTypes,
                                        Resources.TransitionSettingsUI_OkDialog_Ion_types_must_contain_a_comma_separated_list_of_ion_types_a_b_c_x_y_z_and_p_for_precursor);
                return;
            }
            peptideIonTypes = peptideIonTypes.Distinct().ToArray();

            Adduct[] smallMoleculePrecursorAdducts;
            if (!TransitionSettingsControl.ValidateAdductListTextBox(helper, textSmallMoleculePrecursorAdducts, false,
                    min, max, out smallMoleculePrecursorAdducts))
                return;
            smallMoleculePrecursorAdducts = smallMoleculePrecursorAdducts.Distinct().ToArray();
            IonType[] smallMoleculeIonTypes = TransitionFilter.ParseSmallMoleculeTypes(textSmallMoleculeIonTypes.Text, new IonType[0]);
            if (smallMoleculeIonTypes.Length == 0)
            {
                helper.ShowTextBoxError(textSmallMoleculeIonTypes,
                    Resources.TransitionSettingsUI_OkDialog_Small_molecule_ion_types_must_contain_a_comma_separated_list_of_ion_types__Valid_types_are__f___for_fragment__and_or__p___for_precursor_);
                return;
            }
            smallMoleculeIonTypes = smallMoleculeIonTypes.Distinct().ToArray();

            double exclusionWindow = 0;
            if (!string.IsNullOrEmpty(textExclusionWindow.Text) &&
                !Equals(textExclusionWindow.Text, exclusionWindow.ToString(LocalizationHelper.CurrentCulture)))
            {
                if (!helper.ValidateDecimalTextBox(textExclusionWindow,
                        TransitionFilter.MIN_EXCLUSION_WINDOW, TransitionFilter.MAX_EXCLUSION_WINDOW, out exclusionWindow))
                {
                    return;
                }
            }

            string fragmentRangeFirst = TransitionFilter.GetStartFragmentNameFromLabel(comboRangeFrom.SelectedItem.ToString());
            string fragmentRangeLast = TransitionFilter.GetEndFragmentNameFromLabel(comboRangeTo.SelectedItem.ToString());
           
            var measuredIons = _driverIons.Chosen;
            bool autoSelect = cbAutoSelect.Checked;
            bool exclusionUseDIAWindow = FullScanSettingsControl.IsDIA() && cbExclusionUseDIAWindow.Checked;
            var filter = new TransitionFilter(peptidePrecursorCharges, peptideProductCharges, peptideIonTypes,
                smallMoleculePrecursorAdducts, smallMoleculeProductCharges,smallMoleculeIonTypes,
                                              fragmentRangeFirst, fragmentRangeLast, measuredIons,
                                              exclusionWindow, exclusionUseDIAWindow, autoSelect);
            
            Helpers.AssignIfEquals(ref filter, Filter);

            // Validate and store library settings
            TransitionLibraryPick pick = TransitionLibraryPick.none;
            if (cbLibraryPick.Checked)
            {
                if (radioAll.Checked)
                    pick = TransitionLibraryPick.all;
                else if (radioAllAndFiltered.Checked)
                    pick = TransitionLibraryPick.all_plus;
                else
                    pick = TransitionLibraryPick.filter;
            }

            double ionMatchTolerance;

            double minTol = TransitionLibraries.MIN_MATCH_TOLERANCE;
            double maxTol = TransitionLibraries.MAX_MATCH_TOLERANCE;
            if (!helper.ValidateDecimalTextBox(textTolerance,
                    minTol, maxTol, out ionMatchTolerance))
                return;

            int minIonCount = Libraries.MinIonCount;
            int ionCount = Libraries.IonCount;

            if (pick != TransitionLibraryPick.none)
            {
                min = TransitionLibraries.MIN_ION_COUNT;
                max = TransitionLibraries.MAX_ION_COUNT;
                if (string.IsNullOrEmpty(textMinIonCount.Text))
                    minIonCount = 0;
                else if (!helper.ValidateNumberTextBox(textMinIonCount, 0, max, out minIonCount))
                    return;
                if (!helper.ValidateNumberTextBox(textIonCount,
                        min, max, out ionCount))
                    return;
                if (minIonCount > ionCount)
                {
                    helper.ShowTextBoxError(textIonCount, string.Format(Resources.TransitionLibraries_DoValidate_Library_ion_count_value__0__must_not_be_less_than_min_ion_count_value__1__,
                                                                        ionCount, minIonCount));
                    return;
                }
            }

            TransitionLibraries libraries = new TransitionLibraries(ionMatchTolerance, minIonCount, ionCount, pick);
            Helpers.AssignIfEquals(ref libraries, Libraries);

            // This dialog does not yet change integration settings
            TransitionIntegration integration = _transitionSettings.Integration;

            // Validate and store instrument settings
            int minMz;
            min = TransitionInstrument.MIN_MEASUREABLE_MZ;
            max = TransitionInstrument.MAX_MEASURABLE_MZ - TransitionInstrument.MIN_MZ_RANGE;
            if (!helper.ValidateNumberTextBox(textMinMz, min, max, out minMz))
                return;
            int maxMz;
            min = minMz + TransitionInstrument.MIN_MZ_RANGE;
            max = TransitionInstrument.MAX_MEASURABLE_MZ;
            if (!helper.ValidateNumberTextBox(textMaxMz, min, max, out maxMz))
                return;
            bool isDynamicMin = cbDynamicMinimum.Checked;
            double mzMatchTolerance;
            minTol = TransitionInstrument.MIN_MZ_MATCH_TOLERANCE;
            maxTol = TransitionInstrument.MAX_MZ_MATCH_TOLERANCE;
            if (!helper.ValidateDecimalTextBox(textMzMatchTolerance,
                    minTol, maxTol, out mzMatchTolerance))
                return;
            int? maxTrans = null;
            if (!string.IsNullOrEmpty(textMaxTrans.Text))
            {
                int maxTransTemp;
                min = TransitionInstrument.MIN_TRANSITION_MAX;
                max = TransitionInstrument.MAX_TRANSITION_MAX;
                if (!helper.ValidateNumberTextBox(textMaxTrans,
                        min, max, out maxTransTemp))
                    return;
                maxTrans = maxTransTemp;
            }
            int? maxInclusions = null;
            if (!string.IsNullOrEmpty(textMaxInclusions.Text))
            {
                int maxInclusionsTemp;
                min = TransitionInstrument.MIN_INCLUSION_MAX;
                max = TransitionInstrument.MAX_INCLUSION_MAX;
                if (!helper.ValidateNumberTextBox(textMaxInclusions, min, max, out maxInclusionsTemp))
                    return;
                maxInclusions = maxInclusionsTemp;
            }
            int? minTime = null, maxTime = null;
            min = TransitionInstrument.MIN_TIME;
            max = TransitionInstrument.MAX_TIME;
            if (!string.IsNullOrEmpty(textMinTime.Text))
            {
                int minTimeTemp;
                if (!helper.ValidateNumberTextBox(textMinTime, min, max, out minTimeTemp))
                    return;
                minTime = minTimeTemp;
            }
            if (!string.IsNullOrEmpty(textMaxTime.Text))
            {
                int maxTimeTemp;
                if (!helper.ValidateNumberTextBox(textMaxTime, min, max, out maxTimeTemp))
                    return;
                maxTime = maxTimeTemp;
            }
            if (minTime.HasValue && maxTime.HasValue && maxTime.Value - minTime.Value < TransitionInstrument.MIN_TIME_RANGE)
            {
                helper.ShowTextBoxError(textMaxTime,
                                        string.Format(Resources.TransitionSettingsUI_OkDialog_The_allowable_retention_time_range__0__to__1__must_be_at_least__2__minutes_apart,
                                                      minTime, maxTime, TransitionInstrument.MIN_TIME_RANGE));
                return;
            }

            TransitionInstrument instrument = new TransitionInstrument(minMz,
                    maxMz, isDynamicMin, mzMatchTolerance, maxTrans, maxInclusions, minTime, maxTime)
                .ChangeTriggeredAcquisition(cbxTriggeredAcquisition.Checked);
            Helpers.AssignIfEquals(ref instrument, Instrument);

            // Validate and store full-scan settings

            // If high resolution MS1 filtering is enabled, make sure precursor m/z type
            // is monoisotopic and isotope enrichments are set
            FullScanPrecursorIsotopes precursorIsotopes = PrecursorIsotopesCurrent;
            FullScanMassAnalyzerType precursorAnalyzerType = PrecursorMassAnalyzer;
            if (precursorIsotopes != FullScanPrecursorIsotopes.None &&
                    precursorAnalyzerType != FullScanMassAnalyzerType.qit)
            {
                if (!precursorMassType.IsMonoisotopic())
                {
                    MessageDlg.Show(this, Resources.TransitionSettingsUI_OkDialog_High_resolution_MS1_filtering_requires_use_of_monoisotopic_precursor_masses);
                    tabControl1.SelectedIndex = (int)TABS.Prediction;
                    comboPrecursorMass.Focus();
                    return;
                }

                if (FullScanSettingsControl.Enrichments == null)
                {
                    tabControl1.SelectedIndex = (int) TABS.FullScan;
                    MessageDlg.Show(GetParentForm(this), Resources.TransitionSettingsUI_OkDialog_Isotope_enrichment_settings_are_required_for_MS1_filtering_on_high_resolution_mass_spectrometers);
                    FullScanSettingsControl.ComboEnrichmentsSetFocus();
                    return;
                }
            }

            IsolationScheme isolationScheme = FullScanSettingsControl.IsolationScheme;
            FullScanAcquisitionMethod acquisitionMethod = AcquisitionMethod;
            if (isolationScheme == null && acquisitionMethod == FullScanAcquisitionMethod.DIA)
            {
                tabControl1.SelectedIndex = (int)TABS.FullScan;
                MessageDlg.Show(this, Resources.TransitionSettingsUI_OkDialog_An_isolation_scheme_is_required_to_match_multiple_precursors);
                FullScanSettingsControl.ComboIsolationSchemeSetFocus();
                return;
            }

            if (isolationScheme != null && isolationScheme.WindowsPerScan.HasValue && !maxInclusions.HasValue)
            {
                MessageDlg.Show(this, Resources.TransitionSettingsUI_OkDialog_Before_performing_a_multiplexed_DIA_scan_the_instrument_s_firmware_inclusion_limit_must_be_specified);
                tabControl1.SelectedIndex = (int)TABS.Instrument;
                textMaxInclusions.Focus();
                return;
            }

            if (FullScanSettingsControl.IsDIA() && cbExclusionUseDIAWindow.Checked)
            {
                if (FullScanSettingsControl.IsolationScheme.IsAllIons)
                {
                    MessageDlg.Show(this, Resources.TransitionSettingsUI_OkDialog_Cannot_use_DIA_window_for_precusor_exclusion_when__All_Ions__is_selected_as_the_isolation_scheme___To_use_the_DIA_window_for_precusor_exclusion__change_the_isolation_scheme_in_the_Full_Scan_settings_);
                    tabControl1.SelectedIndex = (int)TABS.Filter;
                    cbExclusionUseDIAWindow.Focus();
                    return;
                }
                if (FullScanSettingsControl.IsolationScheme.FromResults)
                {
                    MessageDlg.Show(this, Resources.TransitionSettingsUI_OkDialog_Cannot_use_DIA_window_for_precursor_exclusion_when_isolation_scheme_does_not_contain_prespecified_windows___Please_select_an_isolation_scheme_with_prespecified_windows_);
                    tabControl1.SelectedIndex = (int)TABS.Filter;
                    cbExclusionUseDIAWindow.Focus();
                    return;
                }
            }

            TransitionFullScan fullScan;
            if (!FullScanSettingsControl.ValidateFullScanSettings(helper, out fullScan))
                return;

            Helpers.AssignIfEquals(ref fullScan, FullScan);

            if (!IonMobilityControl.ValidateIonMobilitySettings(helper, out var ionMobilityFiltering))
                return;

            Helpers.AssignIfEquals(ref ionMobilityFiltering, IonMobility);

            TransitionSettings settings = new TransitionSettings(prediction,
                filter, libraries, integration, instrument, fullScan, ionMobilityFiltering);

            // Only update, if anything changed
            if (!Equals(settings, _transitionSettings))
            {
                if (!_parent.ChangeSettingsMonitored(this, Resources.TransitionSettingsUI_OkDialog_Changing_transition_settings,
                                                     s => s.ChangeTransitionSettings(settings)))
                {
                    return;
                }
                _transitionSettings = settings;
            }

            DialogResult = DialogResult.OK;
        }

        private void comboRangeTo_SelectedIndexChanged(object sender, EventArgs e)
        {
            // If nothing is checked yet, start with what is in the settings
            if (!radioAll.Checked && !radioAllAndFiltered.Checked && !radioFiltered.Checked)
            {
                switch (Libraries.Pick)
                {
                    case TransitionLibraryPick.all:
                        radioAll.Checked = true;
                        break;
                    case TransitionLibraryPick.all_plus:
                        radioAllAndFiltered.Checked = true;
                        break;
                    default:
                        radioFiltered.Checked = true;
                        break;
                }               
            }

            string fragmentRangeLastLabel = comboRangeTo.SelectedItem.ToString();
            string fragmentRangeLastName = TransitionFilter.GetEndFragmentNameFromLabel(fragmentRangeLastLabel);
            var countFinder = TransitionFilter.GetEndFragmentFinder(fragmentRangeLastName) as IEndCountFragmentFinder;
            if (countFinder != null)
            {
                textIonCount.Text = countFinder.Count.ToString(LocalizationHelper.CurrentCulture);
                if (!radioAllAndFiltered.Checked)
                    radioAll.Checked = true;
                radioFiltered.Enabled = false;
            }
            else
            {
                textIonCount.Text = Libraries.IonCount.ToString(LocalizationHelper.CurrentCulture);
                radioFiltered.Enabled = true;
            }
        }

        private void cbLibraryPick_CheckedChanged(object sender, EventArgs e)
        {
            panelPick.Visible = cbLibraryPick.Checked;
        }

        private void comboCollisionEnergy_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverCE.SelectedIndexChangedEvent(sender, e);
        }

        private void comboDeclusterPotential_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverDP.SelectedIndexChangedEvent(sender, e);
        }

        private void comboCompensationVoltage_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverCoV.SelectedIndexChangedEvent(sender, e);
        }

        private void comboOptimizationLibrary_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverOptimizationLibrary.SelectedIndexChangedEvent(sender, e);
        }

        private void cbUseOptimized_CheckedChanged(object sender, EventArgs e)
        {
            labelOptimizeType.Visible = comboOptimizeType.Visible = cbUseOptimized.Checked;
        }

        private void btnEditSpecialTransitions_Click(object sender, EventArgs e)
        {
            _driverIons.EditList();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        #region Functional testing support

        public IFormView ShowingFormView
        {
            get
            {
                int selectedIndex = 0;
                Invoke(new Action(() => selectedIndex = tabControl1.SelectedIndex));
                return TAB_PAGES[selectedIndex];
            }
        }

        public TABS SelectedTab
        {
            get { return (TABS)tabControl1.SelectedIndex; }
            set { tabControl1.SelectedIndex = (int)value; }
        }

        // Chooses between "Peptides" and "Small Molecules" filter sub-tab
        public int SelectedPeptidesSmallMolsSubTab
        {
            get { return tabControlPeptidesSmallMols.SelectedIndex; }
            set { tabControlPeptidesSmallMols.SelectedIndex = value; }
        }

        public MassType PrecursorMassType
        {
            get
            {
                return MassTypeExtension.GetEnum(comboPrecursorMass.SelectedItem.ToString());
            }
            set
            {
                comboPrecursorMass.SelectedItem = value.GetLocalizedString();
            }
        }

        public MassType FragmentMassType
        {
            get
            {
                return MassTypeExtension.GetEnum(comboIonMass.SelectedItem.ToString());
            }
            set
            {
                comboIonMass.SelectedItem = value.GetLocalizedString();
            }
        }

        public double? ExclusionWindow
        {
            get
            {
                if (string.IsNullOrEmpty(textExclusionWindow.Text))
                    return null;
                return double.Parse(textExclusionWindow.Text);
            }
            set { textExclusionWindow.Text = value.HasValue ? value.Value.ToString(CultureInfo.CurrentCulture) : string.Empty; }
        }

        public string PrecursorCharges
        {
            get { return textPeptidePrecursorCharges.Text; }
            set { textPeptidePrecursorCharges.Text = value; }
        }

        public string ProductCharges
        {
            get { return textPeptideIonCharges.Text; }
            set { textPeptideIonCharges.Text = value; }
        }

        public string FragmentTypes
        {
            get { return textPeptideIonTypes.Text; }
            set { textPeptideIonTypes.Text = value; }
        }

        public string SmallMoleculePrecursorAdducts
        {
            get { return textSmallMoleculePrecursorAdducts.Text; }
            set { textSmallMoleculePrecursorAdducts.Text = value; }
        }

        public string SmallMoleculeFragmentAdducts
        {
            get { return textSmallMoleculeFragmentAdducts.Text; }
            set { textSmallMoleculeFragmentAdducts.Text = value; }
        }

        public string SmallMoleculeFragmentTypes
        {
            get { return textSmallMoleculeIonTypes.Text; }
            set { textSmallMoleculeIonTypes.Text = value; }
        }

        public string RangeFrom
        {
            get { return comboRangeFrom.SelectedItem.ToString(); }
            set { comboRangeFrom.SelectedItem = value; }
        }

        public string RangeTo
        {
            get { return comboRangeTo.SelectedItem.ToString(); }
            set { comboRangeTo.SelectedItem = value; }
        }

        public string[] SpecialIons
        {
            get { return _driverIons.CheckedNames; }
            set { _driverIons.CheckedNames = value; }
        }

        public int InstrumentMaxMz
        {
            get { return Int32.Parse(textMaxMz.Text); }
            set { textMaxMz.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public void SetRetentionTimeFilter(RetentionTimeFilterType retentionTimeFilterType, double length)
        {
            FullScanSettingsControl.SetRetentionTimeFilter(retentionTimeFilterType, length);
        }

        public double MZMatchTolerance
        {
            get { return Double.Parse(textMzMatchTolerance.Text); }
            set { textMzMatchTolerance.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public CollisionEnergyRegression RegressionCE
        {
            get { return _driverCE.SelectedItem; }
            set { comboCollisionEnergy.SelectedItem = value.Name; }
        }

        public string RegressionCEName
        {
            get { return comboCollisionEnergy.SelectedItem.ToString(); }
            set { comboCollisionEnergy.SelectedItem = value; }
        }

        public DeclusteringPotentialRegression RegressionDP
        {
            get { return _driverDP.SelectedItem; }
            set { comboDeclusterPotential.SelectedItem = value.Name; }
        }

        public string RegressionDPName
        {
            get { return comboDeclusterPotential.SelectedItem.ToString(); }
            set { comboDeclusterPotential.SelectedItem = value; }
        }

        public string OptimizationLibraryName
        {
            get { return comboOptimizationLibrary.SelectedItem.ToString(); }
            set { comboOptimizationLibrary.SelectedItem = value; }
        }

        public CompensationVoltageParameters RegressionCOV
        {
            get { return _driverCoV.SelectedItem; }
            set { comboCompensationVoltage.SelectedItem = value.Name; }
        }

        public string RegressionCOVName
        {
            get { return comboCompensationVoltage.SelectedItem.ToString(); }
            set { comboCompensationVoltage.SelectedItem = value; }
        }

        public void EditSpecialTransitionsList()
        {
            _driverIons.EditList();
        }

        public void EditCEList()
        {
            CheckDisposed();
            _driverCE.EditList();
        }

        public void EditCECurrent()
        {
            CheckDisposed();
            _driverCE.EditCurrent();
        }

        public void AddToCEList()
        {
            _driverCE.AddItem();
        }

        public void EditDPList()
        {
            _driverDP.EditList();
        }

        public void AddToDPList()
        {
            _driverDP.AddItem();
        }

        public void EditCoVList()
        {
            _driverCoV.EditList();
        }

        public void EditOptimizationLibraryList()
        {
            _driverOptimizationLibrary.EditList();
        }

        public void AddToOptimizationLibraryList()
        {
            _driverOptimizationLibrary.AddItem();
        }

        public void EditEnrichmentsList()
        {
            FullScanSettingsControl.EditEnrichmentsList();
        }

        public void AddToEnrichmentsList()
        {
            FullScanSettingsControl.AddToEnrichmentsList();
        }

        public bool UseOptimized
        {
            get { return cbUseOptimized.Checked; }
            set { cbUseOptimized.Checked = value; }
        }

        public string OptimizeType
        {
            get
            {
                return comboOptimizeType.SelectedIndex != -1 ?
                    comboOptimizeType.SelectedItem.ToString() : null;
            }
            set
            {
                comboOptimizeType.SelectedItem = value;
            }
        }

        public int MinIonCount
        {
            get { return Convert.ToInt32(textMinIonCount.Text); }
            set { textMinIonCount.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public int IonCount
        {
            get { return Convert.ToInt32(textIonCount.Text); }
            set { textIonCount.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public bool UseLibraryPick
        {
            get { return cbLibraryPick.Checked; }
            set { cbLibraryPick.Checked = value; }
        }

        public bool SetAutoSelect
        {
            get { return cbAutoSelect.Checked; }
            set { cbAutoSelect.Checked = value; }
        }

        public bool SetDIAExclusionWindow
        {
            get { return cbExclusionUseDIAWindow.Checked; }
            set
            {
                if (!FullScanSettingsControl.IsDIA())
                    throw new InvalidOperationException();
                cbExclusionUseDIAWindow.Checked = value;
            }
        }

        public double IonMatchTolerance
        {
            get { return double.Parse(textTolerance.Text); }
            set { textTolerance.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public int Peaks
        {
            get { return FullScanSettingsControl.Peaks; }
            set { FullScanSettingsControl.Peaks = value; }
        }

        public double? MinTime
        {
            get { return string.IsNullOrEmpty(textMinTime.Text) ? (double?)null : double.Parse(textMinTime.Text); }
            set { textMinTime.Text = value.HasValue ? value.Value.ToString(CultureInfo.CurrentCulture) : string.Empty; }
        }

        public double? MaxTime
        {
            get { return string.IsNullOrEmpty(textMaxTime.Text) ? (double?)null : double.Parse(textMaxTime.Text); }
            set { textMaxTime.Text = value.HasValue ? value.Value.ToString(CultureInfo.CurrentCulture) : string.Empty; }
        }

        public int MaxInclusions
        {
            get { return int.Parse(textMaxInclusions.Text); }
            set { textMaxInclusions.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public bool Filtered
        {
            get { return radioFiltered.Checked;  }
            set { radioFiltered.Checked = value; }
        }

        public double MinMz
        {
            get { return double.Parse(textMinMz.Text); }
            set { textMinMz.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public double MaxMz
        {
            get { return double.Parse(textMaxMz.Text); }
            set { textMaxMz.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public void AddIsolationScheme()
        {
            FullScanSettingsControl.AddIsolationScheme();
        }

        public void EditCurrentIsolationScheme()
        {
            FullScanSettingsControl.EditCurrentIsolationScheme();
        }

        public void EditIsolationScheme()
        {
            FullScanSettingsControl.EditIsolationScheme();
        }

        public void SetListAlwaysAdd(int index, bool check)
        {
            listAlwaysAdd.SetItemChecked(index, check);
        }

        public string IsolationSchemeName
        {
            get { return FullScanSettingsControl.IsolationSchemeName; }
            set { FullScanSettingsControl.IsolationSchemeName = value; }
        }

        public bool ValidateIonCheckBoxes(IList<CheckState> predictedValues)
        {
            return !predictedValues.Where((t, i) => _driverIons.CheckedListBox.GetItemCheckState(i) != t).Any();
        }
        #endregion

        private void listAlwaysAdd_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            var measuredIon = _driverIons.List[e.Index];
            if (! measuredIon.IsCustom)
                return;

            switch (e.CurrentValue)
            {
                case CheckState.Checked:
                    e.NewValue = CheckState.Unchecked;
                    break;

                case CheckState.Indeterminate:
                    e.NewValue = CheckState.Checked;
                    break;

                case CheckState.Unchecked:
                    e.NewValue = CheckState.Indeterminate;
                    break;
            }
        }

        // Sort the list of known adducts, filtered by charge, in order of liklihood
        private static Adduct[] AdductMenuOrder(IEnumerable<String> adducts, int charge)
        {
            var sorted = adducts.Select(a => Adduct.FromString(a, Adduct.ADDUCT_TYPE.non_proteomic, null)).Where(a => a.AdductCharge == charge).ToArray();
            Array.Sort(sorted,
                delegate(Adduct a1, Adduct that)
                {
                    // Show the [2M... stuff after the [M... stuff
                    var comp = Math.Abs(a1.GetMassMultiplier()).CompareTo(Math.Abs(that.GetMassMultiplier()));
                    if (comp != 0)
                    {
                        return comp;  
                    }
                    // Simplest first - lowest unique element count
                    comp = Math.Abs(a1.GetComposition().Count).CompareTo(Math.Abs(that.GetComposition().Count));
                    if (comp != 0)
                    {
                        return comp;  
                    }
                    // Simplest first - shortest text description
                    comp = a1.ToString().Length.CompareTo(that.ToString().Length);
                    if (comp != 0)
                    {
                        return comp; 
                    }
                    // Alpha sort
                    return string.Compare(a1.ToString(), that.ToString(), StringComparison.Ordinal);
                });
            return sorted;
        }

        // Append adduct picker submenus, one per charge 1,2,3,-1,-2,-3
        public static void AppendAdductMenus(ContextMenuStrip menuParent, EventHandler adductStripMenuItem_Click)
        {
            var insertOffset = menuParent.Items.Count == 0 ? 0 : 1; // Leave Help as last item

            var adductsInMenu = new HashSet<Adduct>();

            foreach (var charge in new []{1,2,3,-1,-2,-3})
            {
                // Create a cascading menu item on parent menu
                var text = charge > 0
                    ? Resources.TransitionSettingsUI_PopulateAdductMenu_Adducts_plusplusplus
                    : Resources.TransitionSettingsUI_PopulateAdductMenu_Adducts_minusminusminus;
                var menuItem = new ToolStripMenuItem()
                {
                    Text = text.Substring(0, text.Length - (3 - Math.Abs(charge)))  // Trim "Adducts +++" or "Adducts ---" as needed
                };
                menuParent.Items.Insert(menuParent.Items.Count - insertOffset, menuItem);
                var cascadeTop = menuItem;
                // Charge-only adducts first
                foreach (var adduct in AdductMenuOrder(Adduct.COMMON_CHARGEONLY_ADDUCTS, charge))
                {
                    AddAdductMenuItem(menuItem, adduct.ToString(), adductStripMenuItem_Click);
                    adductsInMenu.Add(adduct);
                }
                // All the adducts from Fiehn lab list
                foreach (var adduct in AdductMenuOrder(Adduct.DEFACTO_STANDARD_ADDUCTS, charge))
                {
                    if (!adductsInMenu.Contains(adduct))
                    {
                        if (ReferenceEquals(menuItem, cascadeTop) && adduct.GetComposition().Count > 2)
                        {
                            // Start another cascade level for the more exotic adducts
                            var menuItemMore = new ToolStripMenuItem()
                            {
                                Text = Resources.TransitionSettingsUI_PopulateAdductMenu_More
                            };
                            menuItem.DropDownItems.Add(menuItemMore);
                            menuItem = menuItemMore;
                        }
                        AddAdductMenuItem(menuItem, adduct.ToString(), adductStripMenuItem_Click);
                        adductsInMenu.Add(adduct);
                    }
                }
            }
        }

        private static void AddAdductMenuItem(ToolStripMenuItem menu, string adduct, EventHandler handler)
        {
            var menuItem = new ToolStripMenuItem()
            {
                Text = adduct
            };
            menuItem.Click += handler;
            menu.DropDownItems.Add(menuItem);
        }


        private void AddAdduct(TextBox textBox, string adduct)
        {
            if (!string.IsNullOrEmpty(textBox.Text))
            {
                textBox.Text += @", ";
            }
            textBox.Text += adduct;
        }


        private void precursorAdductStripMenuItem_Click(object sender, EventArgs e)
        {
            var mi = sender as ToolStripMenuItem;
            if (mi != null)
                AddAdduct(textSmallMoleculePrecursorAdducts, mi.Text);
        }

        private void fragmentAdductStripMenuItem_Click(object sender, EventArgs e)
        {
            var mi = sender as ToolStripMenuItem;
            if (mi != null)
                AddAdduct(textSmallMoleculeFragmentAdducts, mi.Text);
        }

        private void btnPrecursorAdductClick(object sender, EventArgs e)
        {
            contextMenuStripPrecursorAdduct.Show(this, btnPrecursorAdduct.Right + 1, btnPrecursorAdduct.Top);
        }
        private void btnFragmentAdductClick(object sender, EventArgs e)
        {
            contextMenuStripFragmentAdduct.Show(this, btnFragmentAdduct.Right + 1, btnFragmentAdduct.Top);
        }

        //
        // Changes to Full Scan MS1 and/or MS2 settings may require changes in Filter iontypes settings
        //
        private void OnFullScanEnabledChanged(FullScanSettingsControl.FullScanEnabledChangeEventArgs e)
        {

            var peptideIonTypes = TransitionFilter.ParseTypes(textPeptideIonTypes.Text, new IonType[0]).ToList();

            if (e.MS1Enabled.HasValue && (peptideIonTypes.Contains(IonType.precursor) != e.MS1Enabled.Value)) // Full-Scan settings adjusted ion types to include or exclude "p"
            {
                var ions = peptideIonTypes.ToList();
                if (e.MS1Enabled.Value)
                    ions.Add(IonType.precursor);
                else if (!InitialPeptideIonTypes.Contains(IonType.precursor))
                    ions.Remove(IonType.precursor); // Don't remove this if it was there at the start
                if (ions.Count > 0)
                    textPeptideIonTypes.Text = TransitionFilter.ToStringIonTypes(ions, true);
            }

            var smallMoleculeIonTypes = TransitionFilter.ParseSmallMoleculeTypes(textSmallMoleculeIonTypes.Text, new IonType[0]).ToList();
            var smallMolIons = smallMoleculeIonTypes.ToList();

            if (e.MS1Enabled.HasValue && smallMoleculeIonTypes.Contains(IonType.precursor) != e.MS1Enabled.Value) // Full-Scan settings adjusted ion types to include or exclude "f"
            {
                if (e.MS1Enabled.Value)
                    smallMolIons.Add(IonType.precursor);
                else if (!InitialSmallMoleculeIonTypes.Contains(IonType.precursor))
                    smallMolIons.Remove(IonType.precursor);  // Don't remove this if it was there at the start
            }

            if (e.MSMSEnabled.HasValue && smallMoleculeIonTypes.Contains(IonType.custom) != e.MSMSEnabled.Value) // Full-Scan settings adjusted ion types to include or exclude "f"
            {
                if (e.MSMSEnabled.Value)
                    smallMolIons.Insert(0, IonType.custom);
                else if (!InitialSmallMoleculeIonTypes.Contains(IonType.custom))
                    smallMolIons.Remove(IonType.custom);  // Don't remove this if it was there at the start
            }

            if (smallMolIons.Count > 0)
                textSmallMoleculeIonTypes.Text = TransitionFilter.ToStringSmallMoleculeIonTypes(smallMolIons, true);
        }

        public bool TriggeredAcquisition
        {
            get { return cbxTriggeredAcquisition.Checked; }
            set { cbxTriggeredAcquisition.Checked = value; }
        }

        private void cbxTriggeredAcquisition_CheckedChanged(object sender, EventArgs e)
        {
            if (AcquisitionMethod == FullScanAcquisitionMethod.SureQuant && !cbxTriggeredAcquisition.Checked)
            {
                var message =
                    Resources.TransitionSettingsUI_cbxTriggeredAcquisition_CheckedChanged_The_SureQuant_acquisition_method_requires__Triggered_Chromatogram_Extraction___Unchecking_this_option_will_switch_to_the_PRM_acquisition_method__Do_you_want_to_continue_;
                switch (MultiButtonMsgDlg.Show(this, message, MultiButtonMsgDlg.BUTTON_OK))
                {
                    case DialogResult.Cancel:
                        cbxTriggeredAcquisition.Checked = true;
                        break;
                    default:
                        AcquisitionMethod = FullScanAcquisitionMethod.PRM;
                        break;
                }
            }
        }
    }
}
