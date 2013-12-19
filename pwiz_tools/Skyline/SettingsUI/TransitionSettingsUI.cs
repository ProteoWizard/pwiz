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
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class TransitionSettingsUI : FormEx
    {
// ReSharper disable InconsistentNaming
        public enum TABS { Prediction, Filter, Library, Instrument, FullScan, Peaks } // Not L10N       
// ReSharper restore InconsistentNaming

        private readonly SkylineWindow _parent;
        private TransitionSettings _transitionSettings;

        private readonly SettingsListComboDriver<CollisionEnergyRegression> _driverCE;
        private readonly SettingsListComboDriver<DeclusteringPotentialRegression> _driverDP;
        private readonly SettingsListBoxDriver<MeasuredIon> _driverIons;
        public const double DEFAULT_TIME_AROUND_MS2_IDS = 5;
        public const double DEFAULT_TIME_AROUND_PREDICTION = 5;

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

            if (Prediction.OptimizedMethodType == OptimizedMethodType.None)
                comboOptimizeType.SelectedIndex = 0;
            else
            {
                cbUseOptimized.Checked = true;
                comboOptimizeType.SelectedItem = Prediction.OptimizedMethodType.GetLocalizedString();
            }

            // Initialize filter settings
            textPrecursorCharges.Text = Filter.PrecursorCharges.ToArray().ToString(", "); // Not L10N
            textIonCharges.Text = Filter.ProductCharges.ToArray().ToString(", ");
            textIonTypes.Text = Filter.ToStringIonTypes(true);
            comboRangeFrom.SelectedItem = Filter.FragmentRangeFirst.Label;
            comboRangeTo.SelectedItem = Filter.FragmentRangeLast.Label;
            textExclusionWindow.Text = Filter.PrecursorMzWindow != 0
                                           ? Filter.PrecursorMzWindow.ToString(LocalizationHelper.CurrentCulture)
                                           : string.Empty;
            cbAutoSelect.Checked = Filter.AutoSelect;

            _driverIons = new SettingsListBoxDriver<MeasuredIon>(listAlwaysAdd, Settings.Default.MeasuredIonList);
            _driverIons.LoadList(Filter.MeasuredIons);

            // Initialize library settings
            cbLibraryPick.Checked = (Libraries.Pick != TransitionLibraryPick.none);
            panelPick.Visible = cbLibraryPick.Checked;
            textTolerance.Text = Libraries.IonMatchTolerance.ToString(LocalizationHelper.CurrentCulture);
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
            tabFullScan.Controls.Add(FullScanSettingsControl);
        }

        private FullScanSettingsControl FullScanSettingsControl { get; set; }

        public TransitionPrediction Prediction { get { return _transitionSettings.Prediction; } }
        public TransitionFilter Filter { get { return _transitionSettings.Filter; } }
        public TransitionLibraries Libraries { get { return _transitionSettings.Libraries; } }
        public TransitionInstrument Instrument { get { return _transitionSettings.Instrument; } }
        public TransitionFullScan FullScan { get { return _transitionSettings.FullScan; } }
        public TABS? TabControlSel { get; set; }

        public FullScanAcquisitionMethod AcquisitionMethod
        {
            get { return FullScanSettingsControl.AcquisitionMethod; }
            set { FullScanSettingsControl.AcquisitionMethod = value; }
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

        protected override void OnShown(EventArgs e)
        {
            if (TabControlSel != null)
                tabControl1.SelectedIndex = (int)TabControlSel;
            tabControl1.FocusFirstTabStop();
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
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
            OptimizedMethodType optimizedMethodType = OptimizedMethodType.None;
            if (cbUseOptimized.Checked)
            {
                optimizedMethodType = OptimizedMethodTypeExtension.GetEnum(comboOptimizeType.SelectedItem.ToString());
            }
            TransitionPrediction prediction = new TransitionPrediction(precursorMassType,
                                                                       fragmentMassType, collisionEnergy,
                                                                       declusteringPotential,
                                                                       optimizedMethodType);
            Helpers.AssignIfEquals(ref prediction, Prediction);

            // Validate and store filter settings
            int[] precursorCharges;
            int min = TransitionGroup.MIN_PRECURSOR_CHARGE;
            int max = TransitionGroup.MAX_PRECURSOR_CHARGE;
            if (!helper.ValidateNumberListTextBox(e, tabControl1, (int) TABS.Filter, textPrecursorCharges,
                    min, max, out precursorCharges))
                return;
            precursorCharges = precursorCharges.Distinct().ToArray();

            int[] productCharges;
            min = Transition.MIN_PRODUCT_CHARGE;
            max = Transition.MAX_PRODUCT_CHARGE;
            if (!helper.ValidateNumberListTextBox(e, tabControl1, (int) TABS.Filter, textIonCharges,
                    min, max, out productCharges))
                return;
            productCharges = productCharges.Distinct().ToArray();

            IonType[] types = TransitionFilter.ParseTypes(textIonTypes.Text, new IonType[0]);
            if (types.Length == 0)
            {
                helper.ShowTextBoxError(tabControl1, (int) TABS.Filter, textIonTypes,
                                        Resources.TransitionSettingsUI_OkDialog_Ion_types_must_contain_a_comma_separated_list_of_ion_types_a_b_c_x_y_z_and_p_for_precursor);
                return;
            }
            types = types.Distinct().ToArray();

            double exclusionWindow = 0;
            if (!string.IsNullOrEmpty(textExclusionWindow.Text) &&
                !Equals(textExclusionWindow.Text, exclusionWindow.ToString(LocalizationHelper.CurrentCulture)))
            {
                if (!helper.ValidateDecimalTextBox(e, tabControl1, (int)TABS.Filter, textExclusionWindow,
                        TransitionFilter.MIN_EXCLUSION_WINDOW, TransitionFilter.MAX_EXCLUSION_WINDOW, out exclusionWindow))
                {
                    return;
                }
            }

            string fragmentRangeFirst = TransitionFilter.GetStartFragmentNameFromLabel(comboRangeFrom.SelectedItem.ToString());
            string fragmentRangeLast = TransitionFilter.GetEndFragmentNameFromLabel(comboRangeTo.SelectedItem.ToString());
           
            var measuredIons = _driverIons.Chosen;
            bool autoSelect = cbAutoSelect.Checked;
            var filter = new TransitionFilter(precursorCharges, productCharges, types,
                                              fragmentRangeFirst, fragmentRangeLast, measuredIons,
                                              exclusionWindow, autoSelect);
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
            if (!helper.ValidateDecimalTextBox(e, tabControl1, (int) TABS.Library, textTolerance,
                    minTol, maxTol, out ionMatchTolerance))
                return;

            int ionCount = Libraries.IonCount;

            if (pick != TransitionLibraryPick.none)
            {
                min = TransitionLibraries.MIN_ION_COUNT;
                max = TransitionLibraries.MAX_ION_COUNT;
                if (!helper.ValidateNumberTextBox(e, tabControl1, (int) TABS.Library, textIonCount,
                        min, max, out ionCount))
                    return;
            }

            TransitionLibraries libraries = new TransitionLibraries(ionMatchTolerance, ionCount, pick);
            Helpers.AssignIfEquals(ref libraries, Libraries);

            // This dialog does not yet change integration settings
            TransitionIntegration integration = _transitionSettings.Integration;

            // Validate and store instrument settings
            int minMz;
            min = TransitionInstrument.MIN_MEASUREABLE_MZ;
            max = TransitionInstrument.MAX_MEASURABLE_MZ - TransitionInstrument.MIN_MZ_RANGE;
            if (!helper.ValidateNumberTextBox(e, tabControl1, (int) TABS.Instrument, textMinMz, min, max, out minMz))
                return;
            int maxMz;
            min = minMz + TransitionInstrument.MIN_MZ_RANGE;
            max = TransitionInstrument.MAX_MEASURABLE_MZ;
            if (!helper.ValidateNumberTextBox(e, tabControl1, (int) TABS.Instrument, textMaxMz, min, max, out maxMz))
                return;
            bool isDynamicMin = cbDynamicMinimum.Checked;
            double mzMatchTolerance;
            minTol = TransitionInstrument.MIN_MZ_MATCH_TOLERANCE;
            maxTol = TransitionInstrument.MAX_MZ_MATCH_TOLERANCE;
            if (!helper.ValidateDecimalTextBox(e, tabControl1, (int) TABS.Instrument, textMzMatchTolerance,
                    minTol, maxTol, out mzMatchTolerance))
                return;
            int? maxTrans = null;
            if (!string.IsNullOrEmpty(textMaxTrans.Text))
            {
                int maxTransTemp;
                min = TransitionInstrument.MIN_TRANSITION_MAX;
                max = TransitionInstrument.MAX_TRANSITION_MAX;
                if (!helper.ValidateNumberTextBox(e, tabControl1, (int) TABS.Instrument, textMaxTrans,
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
                if (!helper.ValidateNumberTextBox(e, tabControl1, (int) TABS.Instrument, textMaxInclusions,
                        min, max, out maxInclusionsTemp))
                    return;
                maxInclusions = maxInclusionsTemp;
            }
            int? minTime = null, maxTime = null;
            min = TransitionInstrument.MIN_TIME;
            max = TransitionInstrument.MAX_TIME;
            if (!string.IsNullOrEmpty(textMinTime.Text))
            {
                int minTimeTemp;
                if (!helper.ValidateNumberTextBox(e, tabControl1, (int)TABS.Instrument, textMinTime,
                        min, max, out minTimeTemp))
                    return;
                minTime = minTimeTemp;
            }
            if (!string.IsNullOrEmpty(textMaxTime.Text))
            {
                int maxTimeTemp;
                if (!helper.ValidateNumberTextBox(e, tabControl1, (int)TABS.Instrument, textMaxTime,
                        min, max, out maxTimeTemp))
                    return;
                maxTime = maxTimeTemp;
            }
            if (minTime.HasValue && maxTime.HasValue && maxTime.Value - minTime.Value < TransitionInstrument.MIN_TIME_RANGE)
            {
                helper.ShowTextBoxError(tabControl1, (int) TABS.Instrument, textMaxTime,
                                        string.Format(Resources.TransitionSettingsUI_OkDialog_The_allowable_retention_time_range__0__to__1__must_be_at_least__2__minutes_apart,
                                                      minTime, maxTime, TransitionInstrument.MIN_TIME_RANGE));
                return;
            }

            TransitionInstrument instrument = new TransitionInstrument(minMz,
                maxMz, isDynamicMin, mzMatchTolerance, maxTrans, maxInclusions, minTime, maxTime);
            Helpers.AssignIfEquals(ref instrument, Instrument);

            // Validate and store full-scan settings

            // If high resolution MS1 filtering is enabled, make sure precursor m/z type
            // is monoisotopic and isotope enrichments are set
            FullScanPrecursorIsotopes precursorIsotopes = PrecursorIsotopesCurrent;
            FullScanMassAnalyzerType precursorAnalyzerType = PrecursorMassAnalyzer;
            if (precursorIsotopes != FullScanPrecursorIsotopes.None &&
                    precursorAnalyzerType != FullScanMassAnalyzerType.qit)
            {
                if (precursorMassType != MassType.Monoisotopic)
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

            TransitionFullScan fullScan;
            if (!FullScanSettingsControl.ValidateFullScanSettings(e, helper, out fullScan, tabControl1, (int)TABS.FullScan))
                return;

            Helpers.AssignIfEquals(ref fullScan, FullScan);

            TransitionSettings settings = new TransitionSettings(prediction,
                filter, libraries, integration, instrument, fullScan);

            // Only update, if anything changed
            if (!Equals(settings, _transitionSettings))
            {
                SrmSettings newSettings = _parent.DocumentUI.Settings.ChangeTransitionSettings(settings);
                if (!_parent.ChangeSettings(newSettings, true))
                {
                    e.Cancel = true;
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

        public TABS SelectedTab
        {
            get { return (TABS)tabControl1.SelectedIndex; }
            set { tabControl1.SelectedIndex = (int)value; }
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

        public string PrecursorCharges
        {
            get { return textPrecursorCharges.Text; }
            set { textPrecursorCharges.Text = value; }
        }

        public string ProductCharges
        {
            get { return textIonCharges.Text; }
            set { textIonCharges.Text = value; }
        }

        public string FragmentTypes
        {
            get { return textIonTypes.Text; }
            set { textIonTypes.Text = value; }
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

        public void EditCEList()
        {
            CheckDisposed();
            _driverCE.EditList();
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

        public double MinTime
        {
            get { return double.Parse(textMinTime.Text); }
            set { textMinTime.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public double MaxTime
        {
            get { return double.Parse(textMaxTime.Text); }
            set { textMaxTime.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public int MaxInclusions
        {
            set { textMaxInclusions.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public void AddIsolationScheme()
        {
            FullScanSettingsControl.AddIsolationScheme();
        }

        public void EditIsolationScheme()
        {
            FullScanSettingsControl.EditIsolationScheme();
        }

        #endregion
    }
}
