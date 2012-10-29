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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
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
        public enum TABS { Prediction, Filter, Library, Instrument, FullScan } // Not L10N       
// ReSharper restore InconsistentNaming

        private readonly SkylineWindow _parent;
        private TransitionSettings _transitionSettings;

        private readonly SettingsListComboDriver<CollisionEnergyRegression> _driverCE;
        private readonly SettingsListComboDriver<DeclusteringPotentialRegression> _driverDP;
        private readonly SettingsListBoxDriver<MeasuredIon> _driverIons;
        private readonly SettingsListComboDriver<IsotopeEnrichments> _driverEnrichments;
        private readonly SettingsListComboDriver<IsolationScheme> _driverIsolationScheme;
        public const double DEFAULT_TIME_AROUND_MS2_IDS = 5;

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
                                           ? Filter.PrecursorMzWindow.ToString(CultureInfo.CurrentCulture)
                                           : string.Empty;
            cbAutoSelect.Checked = Filter.AutoSelect;

            _driverIons = new SettingsListBoxDriver<MeasuredIon>(listAlwaysAdd, Settings.Default.MeasuredIonList);
            _driverIons.LoadList(Filter.MeasuredIons);

            // Initialize library settings
            cbLibraryPick.Checked = (Libraries.Pick != TransitionLibraryPick.none);
            panelPick.Visible = cbLibraryPick.Checked;
            textTolerance.Text = Libraries.IonMatchTolerance.ToString(CultureInfo.CurrentCulture);
            textIonCount.Text = Libraries.IonCount.ToString(CultureInfo.CurrentCulture);
            if (Libraries.Pick == TransitionLibraryPick.filter)
                radioFiltered.Checked = true;
            else if (Libraries.Pick == TransitionLibraryPick.all_plus)
                radioAllAndFiltered.Checked = true;

            // Initialize instrument settings
            textMinMz.Text = Instrument.MinMz.ToString(CultureInfo.CurrentCulture);
            textMaxMz.Text = Instrument.MaxMz.ToString(CultureInfo.CurrentCulture);
            cbDynamicMinimum.Checked = Instrument.IsDynamicMin;
            textMzMatchTolerance.Text = Instrument.MzMatchTolerance.ToString(CultureInfo.CurrentCulture);
            if (Instrument.MaxTransitions.HasValue)
                textMaxTrans.Text = Instrument.MaxTransitions.Value.ToString(CultureInfo.CurrentCulture);
            if (Instrument.MaxInclusions.HasValue)
                textMaxInclusions.Text = Instrument.MaxInclusions.Value.ToString(CultureInfo.CurrentCulture);
            if (Instrument.MinTime.HasValue)
                textMinTime.Text = Instrument.MinTime.Value.ToString(CultureInfo.CurrentCulture);
            if (Instrument.MaxTime.HasValue)
                textMaxTime.Text = Instrument.MaxTime.Value.ToString(CultureInfo.CurrentCulture);

            // Initialize full-scan settings
            _driverEnrichments = new SettingsListComboDriver<IsotopeEnrichments>(comboEnrichments,
                                                                                 Settings.Default.IsotopeEnrichmentsList);
            sel = (FullScan.IsotopeEnrichments != null ? FullScan.IsotopeEnrichments.Name : null);
            _driverEnrichments.LoadList(sel);

            _driverIsolationScheme = new SettingsListComboDriver<IsolationScheme>(comboIsolationScheme,
                                                                                  Settings.Default.IsolationSchemeList);
            sel = (FullScan.IsolationScheme != null ? FullScan.IsolationScheme.Name : null);
            _driverIsolationScheme.LoadList(sel);

            comboPrecursorIsotopes.Items.AddRange(
                new object[]
                    {
                        FullScanPrecursorIsotopes.None.GetLocalizedString(),
                        FullScanPrecursorIsotopes.Count.GetLocalizedString(),
                        FullScanPrecursorIsotopes.Percent.GetLocalizedString()
                    });
            comboPrecursorAnalyzerType.Items.AddRange(TransitionFullScan.MASS_ANALYZERS.Cast<object>().ToArray());
            comboPrecursorIsotopes.SelectedItem = FullScan.PrecursorIsotopes.GetLocalizedString();

            // Update the precursor analyzer type in case the SelectedIndex is still -1
            UpdatePrecursorAnalyzerType();


            comboAcquisitionMethod.Items.AddRange(
                new object[]
                    {
                        FullScanAcquisitionMethod.None.GetLocalizedString(),
                        FullScanAcquisitionMethod.Targeted.GetLocalizedString(),
                        FullScanAcquisitionMethod.DIA.GetLocalizedString()
                    });
            comboProductAnalyzerType.Items.AddRange(TransitionFullScan.MASS_ANALYZERS.Cast<object>().ToArray());
            comboAcquisitionMethod.SelectedItem = FullScan.AcquisitionMethod.GetLocalizedString();

            // Update the product analyzer type in case the SelectedIndex is still -1
            UpdateProductAnalyzerType();
            tbxTimeAroundMs2Ids.Text = DEFAULT_TIME_AROUND_MS2_IDS.ToString(CultureInfo.CurrentUICulture);
            tbxTimeAroundMs2Ids.Enabled = false;
            if (FullScan.RetentionTimeFilterType == RetentionTimeFilterType.scheduling_windows)
            {
                radioUseSchedulingWindow.Checked = true;
            }
            else if (FullScan.RetentionTimeFilterType == RetentionTimeFilterType.ms2_ids)
            {
                radioTimeAroundMs2Ids.Checked = true;
                tbxTimeAroundMs2Ids.Text =
                    FullScan.RetentionTimeFilterLength.ToString(CultureInfo.CurrentUICulture);
                tbxTimeAroundMs2Ids.Enabled = true;
            }
            else
            {
                radioKeepAllTime.Checked = true;
            }
        }

        public TransitionPrediction Prediction { get { return _transitionSettings.Prediction; } }
        public TransitionFilter Filter { get { return _transitionSettings.Filter; } }
        public TransitionLibraries Libraries { get { return _transitionSettings.Libraries; } }
        public TransitionInstrument Instrument { get { return _transitionSettings.Instrument; } }
        public TransitionFullScan FullScan { get { return _transitionSettings.FullScan; } }
        public TABS? TabControlSel { get; set; }

        public FullScanAcquisitionMethod AcquisitionMethod
        {
            get
            {
                return FullScanAcquisitionExtension.GetEnum(comboAcquisitionMethod.SelectedItem.ToString(),
                    FullScanAcquisitionMethod.None);
            }

            set { comboAcquisitionMethod.SelectedItem = value.GetLocalizedString(); }
        }

        public FullScanMassAnalyzerType ProductMassAnalyzer
        {
            get
            {
                return TransitionFullScan.ParseMassAnalyzer((string) comboProductAnalyzerType.SelectedItem);
            }

            set { comboProductAnalyzerType.SelectedItem = TransitionFullScan.MassAnalyzerToString(value); }
        }

        public FullScanPrecursorIsotopes PrecursorIsotopesCurrent
        {
            get
            {
                return FullScanPrecursorIsotopesExtension.GetEnum(comboPrecursorIsotopes.SelectedItem.ToString(),
                    FullScanPrecursorIsotopes.None);
            }

            set { comboPrecursorIsotopes.SelectedItem = value.GetLocalizedString(); }
        }

        public FullScanMassAnalyzerType PrecursorMassAnalyzer
        {
            get
            {
                return TransitionFullScan.ParseMassAnalyzer((string) comboPrecursorAnalyzerType.SelectedItem);
            }

            set { comboPrecursorAnalyzerType.SelectedItem = TransitionFullScan.MassAnalyzerToString(value); }
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
                !Equals(textExclusionWindow.Text, exclusionWindow.ToString(CultureInfo.CurrentCulture)))
            {
                if (!helper.ValidateDecimalTextBox(e, tabControl1, (int)TABS.Filter, textExclusionWindow,
                        TransitionFilter.MIN_EXCLUSION_WINDOW, TransitionFilter.MAX_EXCLUSION_WINDOW, out exclusionWindow))
                {
                    return;
                }
            }

            string fragmentRangeFirst = TransitionFilter.GetStartFragmentIndex(comboRangeFrom.SelectedItem.ToString());
            string fragmentRangeLast = TransitionFilter.GetEndFragmentIndex(comboRangeTo.SelectedItem.ToString());
           
            if (fragmentRangeFirst == null)
                fragmentRangeFirst = comboRangeFrom.SelectedItem.ToString();
            if (fragmentRangeLast == null)
                fragmentRangeLast = comboRangeTo.SelectedItem.ToString();

            var measuredIons = _driverIons.Chosen;
            bool autoSelect = cbAutoSelect.Checked;
            TransitionFilter filter = new TransitionFilter(precursorCharges, productCharges, types,
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
            FullScanAcquisitionMethod acquisitionMethod = AcquisitionMethod;
            FullScanMassAnalyzerType productAnalyzerType = FullScanMassAnalyzerType.none;
            double? productRes = null;
            double? productResMz = null;

            FullScanPrecursorIsotopes precursorIsotopes = PrecursorIsotopesCurrent;
            double? precursorIsotopeFilter = null;
            FullScanMassAnalyzerType precursorAnalyzerType = FullScanMassAnalyzerType.none;
            double? precursorRes = null;
            double? precursorResMz = null;
            if (precursorIsotopes != FullScanPrecursorIsotopes.None)
            {
                double minFilt, maxFilt;
                if (precursorIsotopes == FullScanPrecursorIsotopes.Count)
                {
                    minFilt = TransitionFullScan.MIN_ISOTOPE_COUNT;
                    maxFilt = TransitionFullScan.MAX_ISOTOPE_COUNT;
                }
                else
                {
                    minFilt = TransitionFullScan.MIN_ISOTOPE_PERCENT;
                    maxFilt = TransitionFullScan.MAX_ISOTOPE_PERCENT;
                }
                double precIsotopeFilt;
                if (!helper.ValidateDecimalTextBox(e, tabControl1, (int)TABS.FullScan, textPrecursorIsotopeFilter,
                                                   minFilt, maxFilt, out precIsotopeFilt))
                    return;
                precursorIsotopeFilter = precIsotopeFilt;

                precursorAnalyzerType = PrecursorMassAnalyzer;
                if (precursorAnalyzerType == FullScanMassAnalyzerType.qit)
                {
                    if (precursorIsotopes != FullScanPrecursorIsotopes.Count || precursorIsotopeFilter != 1)
                    {
                        helper.ShowTextBoxError(tabControl1, (int)TABS.FullScan, textPrecursorIsotopeFilter,
                                                Resources.TransitionSettingsUI_OkDialog_For_MS1_filtering_with_a_QIT_mass_analyzer_only_1_isotope_peak_is_supported);
                        return;
                    }
                    minFilt = TransitionFullScan.MIN_LO_RES;
                    maxFilt = TransitionFullScan.MAX_LO_RES;
                }
                else
                {
                    minFilt = TransitionFullScan.MIN_HI_RES;
                    maxFilt = TransitionFullScan.MAX_HI_RES;
                }
                double precRes;
                if (!helper.ValidateDecimalTextBox(e, tabControl1, (int)TABS.FullScan, textPrecursorRes,
                        minFilt, maxFilt, out precRes))
                    return;
                precursorRes = precRes;
                if (precursorAnalyzerType != FullScanMassAnalyzerType.qit &&
                    precursorAnalyzerType != FullScanMassAnalyzerType.tof)
                {
                    double precResMz;
                    if (!helper.ValidateDecimalTextBox(e, tabControl1, (int)TABS.FullScan, textPrecursorAt,
                            TransitionFullScan.MIN_RES_MZ, TransitionFullScan.MAX_RES_MZ, out precResMz))
                        return;
                    precursorResMz = precResMz;
                }
            }

            if (acquisitionMethod != FullScanAcquisitionMethod.None)
            {
                double minFilt, maxFilt;

                productAnalyzerType = ProductMassAnalyzer;
                if (productAnalyzerType == FullScanMassAnalyzerType.qit)
                {
                    minFilt = TransitionFullScan.MIN_LO_RES;
                    maxFilt = TransitionFullScan.MAX_LO_RES;
                }
                else
                {
                    minFilt = TransitionFullScan.MIN_HI_RES;
                    maxFilt = TransitionFullScan.MAX_HI_RES;
                }
                double prodRes;
                if (!helper.ValidateDecimalTextBox(e, tabControl1, (int)TABS.FullScan, textProductRes,
                        minFilt, maxFilt, out prodRes))
                    return;

                productRes = prodRes;

                if (productAnalyzerType != FullScanMassAnalyzerType.qit &&
                    productAnalyzerType != FullScanMassAnalyzerType.tof)
                {
                    double prodResMz;
                    if (!helper.ValidateDecimalTextBox(e, tabControl1, (int)TABS.FullScan, textProductAt,
                            TransitionFullScan.MIN_RES_MZ, TransitionFullScan.MAX_RES_MZ, out prodResMz))
                        return;
                    productResMz = prodResMz;
                }
            }

            // If high resolution MS1 filtering is enabled, make sure precursor m/z type
            // is monoisotopic and isotope enrichments are set
            IsotopeEnrichments enrichments = null;
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

                enrichments = _driverEnrichments.SelectedItem;
                if (enrichments == null)
                {
                    MessageDlg.Show(this, Resources.TransitionSettingsUI_OkDialog_Isotope_enrichment_settings_are_required_for_MS1_filtering_on_high_resolution_mass_spectrometers);
                    tabControl1.SelectedIndex = (int) TABS.FullScan;
                    comboEnrichments.Focus();
                    return;
                }
            }

            IsolationScheme isolationScheme = _driverIsolationScheme.SelectedItem;
            if (isolationScheme == null && acquisitionMethod == FullScanAcquisitionMethod.DIA)
            {
                MessageDlg.Show(this, Resources.TransitionSettingsUI_OkDialog_An_isolation_scheme_is_required_to_match_multiple_precursors);
                tabControl1.SelectedIndex = (int) TABS.FullScan;
                comboIsolationScheme.Focus();
                return;
            }
            if (isolationScheme != null && isolationScheme.WindowsPerScan.HasValue && !maxInclusions.HasValue)
            {
                MessageDlg.Show(this, Resources.TransitionSettingsUI_OkDialog_Before_performing_a_multiplexed_DIA_scan_the_instrument_s_firmware_inclusion_limit_must_be_specified);
                tabControl1.SelectedIndex = (int)TABS.Instrument;
                textMaxInclusions.Focus();
                return;
            }

            RetentionTimeFilterType retentionTimeFilterType;
            double timeAroundMs2Ids = 0;
            if (radioUseSchedulingWindow.Checked)
            {
                retentionTimeFilterType = RetentionTimeFilterType.scheduling_windows;
            }
            else if (radioTimeAroundMs2Ids.Checked)
            {
                retentionTimeFilterType = RetentionTimeFilterType.ms2_ids;
                if (!double.TryParse(tbxTimeAroundMs2Ids.Text, out timeAroundMs2Ids) || timeAroundMs2Ids < 0)
                {
                    MessageDlg.Show(this, Resources.TransitionSettingsUI_OkDialog_This_is_not_a_valid_number_of_minutes);
                    tabControl1.SelectedIndex = (int) TABS.FullScan;
                    tbxTimeAroundMs2Ids.Focus();
                    return;
                }
            }
            else
            {
                retentionTimeFilterType = RetentionTimeFilterType.none;
            }
            var fullScan = new TransitionFullScan(acquisitionMethod,
                                                  isolationScheme,
                                                  productAnalyzerType,
                                                  productRes,
                                                  productResMz,
                                                  precursorIsotopes,
                                                  precursorIsotopeFilter,
                                                  precursorAnalyzerType,
                                                  precursorRes,
                                                  precursorResMz,
                                                  enrichments,
                                                  retentionTimeFilterType,
                                                  timeAroundMs2Ids);

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

            string fragmentRangeLastName = comboRangeTo.SelectedItem.ToString();
            var countFinder = TransitionFilter.GetEndFragmentFinder(fragmentRangeLastName) as IEndCountFragmentFinder;
            if (countFinder != null)
            {
                textIonCount.Text = countFinder.Count.ToString(CultureInfo.CurrentCulture);
                if (!radioAllAndFiltered.Checked)
                    radioAll.Checked = true;
                radioFiltered.Enabled = false;
            }
            else
            {
                textIonCount.Text = Libraries.IonCount.ToString(CultureInfo.CurrentCulture);
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

        private void comboEnrichments_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverEnrichments.SelectedIndexChangedEvent(sender, e);
        }

        private void comboIsolationScheme_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverIsolationScheme.SelectedIndexChangedEvent(sender, e);
        }

        private void cbUseOptimized_CheckedChanged(object sender, EventArgs e)
        {
            labelOptimizeType.Visible = comboOptimizeType.Visible = cbUseOptimized.Checked;
        }

        private void btnEditSpecialTransitions_Click(object sender, EventArgs e)
        {
            _driverIons.EditList();
        }

        private void comboAcquisitionMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            var acquisitionMethod = AcquisitionMethod;
            if (acquisitionMethod == FullScanAcquisitionMethod.None)
            {
                EnableIsolationScheme(false);
                // Selection change should set filter m/z textbox correctly
                comboProductAnalyzerType.SelectedIndex = -1;
                comboProductAnalyzerType.Enabled = false;
                comboIsolationScheme.SelectedIndex = -1;
                comboIsolationScheme.Enabled = false;
            }
            else
            {
                EnableIsolationScheme(acquisitionMethod == FullScanAcquisitionMethod.DIA);

                // If the combo is being set to the type it started with, use the starting values
                if (acquisitionMethod == FullScan.AcquisitionMethod)
                {
                    if (!comboProductAnalyzerType.Enabled)
                        comboProductAnalyzerType.SelectedItem = TransitionFullScan.MassAnalyzerToString(FullScan.ProductMassAnalyzer);
                }
                else
                {
                    if (!comboProductAnalyzerType.Enabled)
                    {
                        string tofAnalyzer = TransitionFullScan.MassAnalyzerToString(FullScanMassAnalyzerType.tof);
                        comboProductAnalyzerType.SelectedItem =
                            comboPrecursorAnalyzerType.SelectedItem != null &&
                                Equals(comboPrecursorAnalyzerType.SelectedItem.ToString(), tofAnalyzer)
                            ? tofAnalyzer
                            : TransitionFullScan.MassAnalyzerToString(FullScanMassAnalyzerType.qit);
                    }
                }
                comboProductAnalyzerType.Enabled = true;
            }            
        }

        private void EnableIsolationScheme(bool enable)
        {
            comboIsolationScheme.Enabled = enable;
            if (!enable)
            {
                comboIsolationScheme.SelectedIndex = -1;
            }
        }

        private void comboProductAnalyzerType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateProductAnalyzerType();
        }

        private void UpdateProductAnalyzerType()
        {
            SetAnalyzerType(ProductMassAnalyzer,
                            FullScan.ProductMassAnalyzer,
                            FullScan.ProductRes,
                            FullScan.ProductResMz,
                            labelProductRes,
                            textProductRes,
                            labelProductAt,
                            textProductAt,
                            labelProductTh);
        }

        private void comboPrecursorIsotopes_SelectedIndexChanged(object sender, EventArgs e)
        {
            var precursorIsotopes = PrecursorIsotopesCurrent;

            bool percentType = (precursorIsotopes == FullScanPrecursorIsotopes.Percent);
            labelPrecursorIsotopeFilter.Text = percentType
                                                   ? Resources.TransitionSettingsUI_comboPrecursorIsotopes_SelectedIndexChanged_Min_percent_of_base_peak
                                                   : Resources.TransitionSettingsUI_comboPrecursorIsotopes_SelectedIndexChanged_Peaks;
            labelPrecursorIsotopeFilterPercent.Visible = percentType;
            
            if (precursorIsotopes == FullScanPrecursorIsotopes.None)
            {
                textPrecursorIsotopeFilter.Text = string.Empty;
                textPrecursorIsotopeFilter.Enabled = false;
                comboEnrichments.SelectedIndex = -1;
                comboEnrichments.Enabled = false;
                // Selection change should set filter m/z textbox correctly
                comboPrecursorAnalyzerType.SelectedIndex = -1;
                comboPrecursorAnalyzerType.Enabled = false;
            }
            else
            {
                // If the combo is being set to the type it started with, use the starting values
                if (precursorIsotopes == FullScan.PrecursorIsotopes)
                {
                    textPrecursorIsotopeFilter.Text = FullScan.PrecursorIsotopeFilter.HasValue
                                                          ? FullScan.PrecursorIsotopeFilter.Value.ToString(CultureInfo.CurrentCulture)
                                                          : string.Empty;
                    if (FullScan.IsotopeEnrichments != null)
                        comboEnrichments.SelectedItem = FullScan.IsotopeEnrichments.Name;
                    if (!comboPrecursorAnalyzerType.Enabled)
                        comboPrecursorAnalyzerType.SelectedItem = TransitionFullScan.MassAnalyzerToString(FullScan.PrecursorMassAnalyzer);
                }
                else
                {
                    textPrecursorIsotopeFilter.Text = (percentType
                                                           ? TransitionFullScan.DEFAULT_ISOTOPE_PERCENT
                                                           : TransitionFullScan.DEFAULT_ISOTOPE_COUNT).ToString(CultureInfo.CurrentCulture);
                    
                    var precursorMassAnalyzer = PrecursorMassAnalyzer;
                    if (!comboPrecursorAnalyzerType.Enabled || (percentType && precursorMassAnalyzer == FullScanMassAnalyzerType.qit))
                    {
                        comboPrecursorAnalyzerType.SelectedItem = TransitionFullScan.MassAnalyzerToString(FullScanMassAnalyzerType.tof);
                        comboEnrichments.SelectedItem = IsotopeEnrichmentsList.GetDefault().Name;
                    }
                }

                comboEnrichments.Enabled = (comboEnrichments.SelectedIndex != -1);
                textPrecursorIsotopeFilter.Enabled = true;
                comboPrecursorAnalyzerType.Enabled = true;
            }
        }

        private void comboPrecursorAnalyzerType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePrecursorAnalyzerType();
        }

        private void UpdatePrecursorAnalyzerType()
        {
            var precursorMassAnalyzer = PrecursorMassAnalyzer;
            SetAnalyzerType(PrecursorMassAnalyzer,
                            FullScan.PrecursorMassAnalyzer,
                            FullScan.PrecursorRes,
                            FullScan.PrecursorResMz,
                            labelPrecursorRes,
                            textPrecursorRes,
                            labelPrecursorAt,
                            textPrecursorAt,
                            labelPrecursorTh);

            // For QIT, only 1 isotope peak is allowed
            if (precursorMassAnalyzer == FullScanMassAnalyzerType.qit)
            {
                comboPrecursorIsotopes.SelectedItem = FullScanPrecursorIsotopes.Count.GetLocalizedString();
                textPrecursorIsotopeFilter.Text = 1.ToString(CultureInfo.CurrentCulture);
                comboEnrichments.SelectedIndex = -1;
                comboEnrichments.Enabled = false;
            }
            else if (precursorMassAnalyzer != FullScanMassAnalyzerType.none && !comboEnrichments.Enabled)
            {
                comboEnrichments.SelectedIndex = 0;
                comboEnrichments.Enabled = true;
            }
        }

        public static void SetAnalyzerType(FullScanMassAnalyzerType analyzerTypeNew,
                                            FullScanMassAnalyzerType analyzerTypeCurrent,
                                            double? resCurrent,
                                            double? resMzCurrent,
                                            Label label,
                                            TextBox textRes,
                                            Label labelAt,
                                            TextBox textAt,
                                            Label labelTh)
        {
            string labelText = Resources.TransitionSettingsUI_SetAnalyzerType_Resolution;
            if (analyzerTypeNew == FullScanMassAnalyzerType.none)
            {
                textRes.Enabled = false;
                textRes.Text = string.Empty;
                labelAt.Visible = false;
                textAt.Visible = false;
                labelTh.Left = textRes.Right;
            }
            else
            {
                textRes.Enabled = true;
                bool variableRes = false;
                TextBox textMz = null;
                if (analyzerTypeNew == FullScanMassAnalyzerType.qit)
                {
                    textMz = textRes;
                }
                else
                {
                    labelText = Resources.TransitionSettingsUI_SetAnalyzerType_Resolving_power;
                    if (analyzerTypeNew != FullScanMassAnalyzerType.tof)
                    {
                        variableRes = true;
                        textMz = textAt;
                    }
                }

                const string resolvingPowerFormat = "#,0.####"; // Not L10N
                if (analyzerTypeNew == analyzerTypeCurrent && resCurrent.HasValue)
                    textRes.Text = resCurrent.Value.ToString(resolvingPowerFormat);
                else
                    textRes.Text = TransitionFullScan.DEFAULT_RES_VALUES[(int) analyzerTypeNew].ToString(resolvingPowerFormat);

                labelAt.Visible = variableRes;
                textAt.Visible = variableRes;
                textAt.Text = resMzCurrent.HasValue
                                  ? resMzCurrent.Value.ToString(CultureInfo.CurrentCulture)
                                  : TransitionFullScan.DEFAULT_RES_MZ.ToString(CultureInfo.CurrentCulture);

                labelTh.Visible = (textMz != null);
                if (textMz != null)
                    labelTh.Left = textMz.Right;
            }
            label.Text = labelText;
        }

        private void RadioNoiseAroundMs2IdsCheckedChanged(object sender, EventArgs e)
        {
            tbxTimeAroundMs2Ids.Enabled = radioTimeAroundMs2Ids.Checked;
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
            set { textMaxMz.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public void SetRetentionTimeFilter(RetentionTimeFilterType retentionTimeFilterType, double length)
        {
            switch (retentionTimeFilterType)
            {
                case RetentionTimeFilterType.none:
                    radioKeepAllTime.Checked = true;
                    break;
                case RetentionTimeFilterType.scheduling_windows:
                    radioUseSchedulingWindow.Checked = true;
                    break;
                case RetentionTimeFilterType.ms2_ids:
                    radioTimeAroundMs2Ids.Checked = true;
                    break;
                default:
                    throw new ArgumentException("Invalid RetentionTimeFilterType", "retentionTimeFilterType"); // Not L10N
            }
            tbxTimeAroundMs2Ids.Text = length.ToString(CultureInfo.CurrentUICulture);
        }

        public double MZMatchTolerance
        {
            get { return Double.Parse(textMzMatchTolerance.Text); }
            set { textMzMatchTolerance.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public CollisionEnergyRegression RegressionCE
        {
            get { return (CollisionEnergyRegression) comboCollisionEnergy.SelectedItem; }
            set { comboCollisionEnergy.SelectedItem = value; }
        }

        public string RegressionCEName
        {
            get { return comboCollisionEnergy.SelectedItem.ToString(); }
            set { comboCollisionEnergy.SelectedItem = value; }
        }

        public DeclusteringPotentialRegression RegressionDP
        {
            get { return (DeclusteringPotentialRegression) comboDeclusterPotential.SelectedItem; }
            set { comboDeclusterPotential.SelectedItem = value; }
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
            _driverEnrichments.EditList();
        }

        public void AddToEnrichmentsList()
        {
            _driverEnrichments.AddItem();
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
            set { textIonCount.Text = value.ToString(CultureInfo.CurrentCulture); }
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

        public string Peaks
        {
            get { return textPrecursorIsotopeFilter.Text; }
            set { textPrecursorIsotopeFilter.Text = value; }
        }

        public string MinTime
        {
            get { return textMinTime.Text; }
            set { textMinTime.Text = value; }
        }

        public string MaxTime
        {
            get { return textMaxTime.Text; }
            set { textMaxTime.Text = value; }
        }

        public int MaxInclusions
        {
            set { textMaxInclusions.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public void AddIsolationScheme()
        {
            _driverIsolationScheme.AddItem();
        }

        public void EditIsolationScheme()
        {
            _driverIsolationScheme.EditList();
        }

        #endregion
    }
}
