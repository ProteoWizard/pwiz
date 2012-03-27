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
    public partial class TransitionSettingsUI : Form
    {
// ReSharper disable InconsistentNaming
        public enum TABS { Prediction, Filter, Library, Instrument, FullScan }
// ReSharper restore InconsistentNaming

        private readonly SkylineWindow _parent;
        private TransitionSettings _transitionSettings;

        private readonly SettingsListComboDriver<CollisionEnergyRegression> _driverCE;
        private readonly SettingsListComboDriver<DeclusteringPotentialRegression> _driverDP;
        private readonly SettingsListBoxDriver<MeasuredIon> _driverIons;
        private readonly SettingsListComboDriver<IsotopeEnrichments> _driverEnrichments;

        public TransitionSettingsUI(SkylineWindow parent)
        {
            InitializeComponent();

            // Populate the fragment finder combo boxes
            foreach (string item in TransitionFilter.GetStartFragmentFinderNames())
                comboRangeFrom.Items.Add(item);
            foreach (string item in TransitionFilter.GetEndFragmentFinderNames())
                comboRangeTo.Items.Add(item);

            _parent = parent;
            _transitionSettings = _parent.DocumentUI.Settings.TransitionSettings;
        
            // Initialize prediction settings
            comboPrecursorMass.SelectedItem = Prediction.PrecursorMassType.ToString();
            comboIonMass.SelectedItem = Prediction.FragmentMassType.ToString();

            _driverCE = new SettingsListComboDriver<CollisionEnergyRegression>(comboCollisionEnergy, Settings.Default.CollisionEnergyList);
            string sel = (Prediction.CollisionEnergy == null ? null : Prediction.CollisionEnergy.Name);
            _driverCE.LoadList(sel);

            _driverDP = new SettingsListComboDriver<DeclusteringPotentialRegression>(comboDeclusterPotential, Settings.Default.DeclusterPotentialList);
            sel = (Prediction.DeclusteringPotential == null ? null : Prediction.DeclusteringPotential.Name);
            _driverDP.LoadList(sel);

            if (Prediction.OptimizedMethodType == OptimizedMethodType.None)
                comboOptimizeType.SelectedIndex = 0;
            else
            {
                cbUseOptimized.Checked = true;
                comboOptimizeType.SelectedItem = Prediction.OptimizedMethodType.ToString();
            }

            // Initialize filter settings
            textPrecursorCharges.Text = Filter.PrecursorCharges.ToArray().ToString(", ");
            textIonCharges.Text = Filter.ProductCharges.ToArray().ToString(", ");
            textIonTypes.Text = Filter.ToStringIonTypes(true);
            comboRangeFrom.SelectedItem = Filter.FragmentRangeFirst.GetKey();
            comboRangeTo.SelectedItem = Filter.FragmentRangeLast.GetKey();
            textExclusionWindow.Text = Filter.PrecursorMzWindow != 0
                ? Filter.PrecursorMzWindow.ToString(CultureInfo.CurrentCulture)
                : "";            
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
            if (Instrument.MinTime.HasValue)
                textMinTime.Text = Instrument.MinTime.Value.ToString(CultureInfo.CurrentCulture);
            if (Instrument.MaxTime.HasValue)
                textMaxTime.Text = Instrument.MaxTime.Value.ToString(CultureInfo.CurrentCulture);

            // Initialize full-scan settings
            _driverEnrichments = new SettingsListComboDriver<IsotopeEnrichments>(comboEnrichments, Settings.Default.IsotopeEnrichmentsList);
            sel = (FullScan.IsotopeEnrichments != null ? FullScan.IsotopeEnrichments.Name : null);
            _driverEnrichments.LoadList(sel);

            comboPrecursorIsotopes.Items.AddRange(
                new object[]
                    {
                        FullScanPrecursorIsotopes.None.ToString(),
                        FullScanPrecursorIsotopes.Count.ToString(),
                        FullScanPrecursorIsotopes.Percent.ToString()
                    });
            comboPrecursorAnalyzerType.Items.AddRange(TransitionFullScan.MASS_ANALYZERS.Cast<object>().ToArray());
            comboPrecursorIsotopes.SelectedItem = FullScan.PrecursorIsotopes.ToString();

            // Update the precursor analyzer type in case the SelectedIndex is still -1
            UpdatePrecursorAnalyzerType();

            UpdateIsolationWidths();

            comboPrecursorFilterType.Items.AddRange(
                new object[]
                    {
                        FullScanPrecursorFilterType.None.ToString(),
                        FullScanPrecursorFilterType.Single.ToString(),
                        FullScanPrecursorFilterType.Multiple.ToString()
                    });
            comboProductAnalyzerType.Items.AddRange(TransitionFullScan.MASS_ANALYZERS.Cast<object>().ToArray());            
            comboPrecursorFilterType.SelectedItem = FullScan.PrecursorFilterType.ToString();

            // Update the product analyzer type in case the SelectedIndex is still -1
            UpdateProductAnalyzerType();

            cbFilterScheduling.Checked = FullScan.IsScheduledFilter;
        }

        public TransitionPrediction Prediction { get { return _transitionSettings.Prediction; } }
        public TransitionFilter Filter { get { return _transitionSettings.Filter; } }
        public TransitionLibraries Libraries { get { return _transitionSettings.Libraries; } }
        public TransitionInstrument Instrument { get { return _transitionSettings.Instrument; } }
        public TransitionFullScan FullScan { get { return _transitionSettings.FullScan; } }

        public FullScanPrecursorFilterType PrecursorFilterTypeCurrent
        {
            get
            {
                return Helpers.ParseEnum((string)comboPrecursorFilterType.SelectedItem,
                    FullScanPrecursorFilterType.None);
            }
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
                return Helpers.ParseEnum((string)comboPrecursorIsotopes.SelectedItem,
                    FullScanPrecursorIsotopes.None);
            }

            set { comboPrecursorIsotopes.SelectedItem = value.ToString(); }
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
            tabControl1.FocusFirstTabStop();
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            // Validate and store prediction settings
            string massType = comboPrecursorMass.SelectedItem.ToString();
            MassType precursorMassType = (MassType)
                                         Enum.Parse(typeof (MassType), massType);
            massType = comboIonMass.SelectedItem.ToString();
            MassType fragmentMassType = (MassType)
                                        Enum.Parse(typeof (MassType), massType);
            string nameCE = comboCollisionEnergy.SelectedItem.ToString();
            CollisionEnergyRegression collisionEnergy =
                Settings.Default.GetCollisionEnergyByName(nameCE);
            string nameDP = comboDeclusterPotential.SelectedItem.ToString();
            DeclusteringPotentialRegression declusteringPotential =
                Settings.Default.GetDeclusterPotentialByName(nameDP);
            OptimizedMethodType optimizedMethodType = OptimizedMethodType.None;
            if (cbUseOptimized.Checked)
            {
                optimizedMethodType = (OptimizedMethodType) Enum.Parse(typeof(OptimizedMethodType),
                    comboOptimizeType.SelectedItem.ToString());
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
                                         "Ion types must contain a comma separated list of ion types a, b, c, x, y z and p (for precursor).");
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

            string fragmentRangeFirst = comboRangeFrom.SelectedItem.ToString();
            string fragmentRangeLast = comboRangeTo.SelectedItem.ToString();
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

            TransitionInstrument instrument = new TransitionInstrument(minMz,
                maxMz, isDynamicMin, mzMatchTolerance, maxTrans, minTime, maxTime);
            Helpers.AssignIfEquals(ref instrument, Instrument);

            // Validate and store full-scan settings
            FullScanPrecursorFilterType precursorFilterType = PrecursorFilterTypeCurrent;
            double? precursorFilter = null;
            double? precursorRightFilter = null;
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
                                                 "For MS1 filtering with a QIT mass analyzer only 1 isotope peak is supported.");
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

            if (precursorFilterType != FullScanPrecursorFilterType.None)
            {
                double minFilt, maxFilt;
                if (precursorFilterType == FullScanPrecursorFilterType.Multiple)
                {
                    double filterFactor = cbAsymIsolation.Checked ? 0.5 : 1;
                    minFilt = TransitionFullScan.MIN_PRECURSOR_MULTI_FILTER*filterFactor;
                    maxFilt = TransitionFullScan.MAX_PRECURSOR_MULTI_FILTER*filterFactor;
                    double precFilt;
                    if (!helper.ValidateDecimalTextBox(e, tabControl1, (int) TABS.FullScan, textPrecursorFilterMz,
                                                       minFilt, maxFilt, out precFilt))
                        return;
                    precursorFilter = precFilt;
                    if (cbAsymIsolation.Checked)
                    {
                        if (!helper.ValidateDecimalTextBox(e, tabControl1, (int)TABS.FullScan, textRightPrecursorFilterMz,
                                                           minFilt, maxFilt, out precFilt))
                            return;
                        precursorRightFilter = precFilt;
                    }
                }

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
                    MessageDlg.Show(this, "High resolution MS1 filtering requires use of monoisotopic precursor masses.");
                    tabControl1.SelectedIndex = (int)TABS.Prediction;
                    comboPrecursorMass.Focus();
                    return;
                }
                string nameEnrichments = comboEnrichments.SelectedItem.ToString();
                enrichments = Settings.Default.GetIsotopeEnrichmentsByName(nameEnrichments);
                if (enrichments == null)
                {
                    MessageDlg.Show(this, "Isotope enrichment settings are required for MS1 filtering on high resolution mass spectrometers.");
                    tabControl1.SelectedIndex = (int) TABS.FullScan;
                    comboEnrichments.Focus();
                    return;
                }
            }
            bool isScheduledFilter = cbFilterScheduling.Checked;

            var fullScan = new TransitionFullScan(precursorFilterType,
                                                  precursorFilter,
                                                  precursorRightFilter,
                                                  productAnalyzerType,
                                                  productRes,
                                                  productResMz,
                                                  precursorIsotopes,
                                                  precursorIsotopeFilter,
                                                  precursorAnalyzerType,
                                                  precursorRes,
                                                  precursorResMz,
                                                  enrichments,
                                                  isScheduledFilter);

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

        private void cbUseOptimized_CheckedChanged(object sender, EventArgs e)
        {
            labelOptimizeType.Visible = comboOptimizeType.Visible = cbUseOptimized.Checked;
        }

        private void btnEditSpecialTransitions_Click(object sender, EventArgs e)
        {
            _driverIons.EditList();
        }

        private void comboPrecursorFilterType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var precursorFilterType = PrecursorFilterTypeCurrent;
            if (precursorFilterType == FullScanPrecursorFilterType.None)
            {
                EnablePrecursorFilterMz(false, "", null);
                // Selection change should set filter m/z textbox correctly
                comboProductAnalyzerType.SelectedIndex = -1;
                comboProductAnalyzerType.Enabled = false;
            }
            else
            {
                // If the combo is being set to the type it started with, use the starting values
                if (precursorFilterType == FullScan.PrecursorFilterType)
                {
                    EnablePrecursorFilterMz(precursorFilterType == FullScanPrecursorFilterType.Multiple,
                        FullScan.PrecursorFilter.HasValue
                            ? FullScan.PrecursorFilter.Value.ToString(CultureInfo.CurrentCulture)
                            : null,
                        FullScan.PrecursorRightFilter.HasValue
                            ? FullScan.PrecursorRightFilter.Value.ToString(CultureInfo.CurrentCulture)
                            : null);
                    if (!comboProductAnalyzerType.Enabled)
                        comboProductAnalyzerType.SelectedItem = TransitionFullScan.MassAnalyzerToString(FullScan.ProductMassAnalyzer);
                }
                else
                {
                    EnablePrecursorFilterMz(precursorFilterType == FullScanPrecursorFilterType.Multiple,
                                            TransitionFullScan.DEFAULT_PRECURSOR_MULTI_FILTER.ToString(CultureInfo.CurrentCulture),
                                            null);
                    if (!comboProductAnalyzerType.Enabled)
                        comboProductAnalyzerType.SelectedItem = TransitionFullScan.MassAnalyzerToString(FullScanMassAnalyzerType.qit);
                }
                comboProductAnalyzerType.Enabled = true;
            }            
        }

        private void EnablePrecursorFilterMz(bool enable, string text, string textRight)
        {
            cbAsymIsolation.Checked = enable && textRight != null;
            cbAsymIsolation.Enabled = enable;
            textPrecursorFilterMz.Text = (enable ? text : "");
            textPrecursorFilterMz.Enabled = enable;
            if (cbAsymIsolation.Checked)
                textRightPrecursorFilterMz.Text = textRight;
        }

        private void cbAsymIsolation_CheckedChanged(object sender, EventArgs e)
        {
            UpdateIsolationWidths();
        }

        private void UpdateIsolationWidths()
        {
            if (cbAsymIsolation.Checked)
            {
                labelIsolationWidth.Text = "Isolation &widths:";
                textRightPrecursorFilterMz.Visible = true;
                textPrecursorFilterMz.Width = textRightPrecursorFilterMz.Width;
                double totalWidth;
                double? halfWidth = null;
                if (double.TryParse(textPrecursorFilterMz.Text, out totalWidth))
                    halfWidth = totalWidth/2;
                textPrecursorFilterMz.Text = textRightPrecursorFilterMz.Text = halfWidth.HasValue
                    ? halfWidth.Value.ToString(CultureInfo.CurrentCulture) : "";
            }
            else
            {
                labelIsolationWidth.Text = "Isolation &width:";
                textRightPrecursorFilterMz.Visible = false;
                textPrecursorFilterMz.Width = textRightPrecursorFilterMz.Right - textPrecursorFilterMz.Left;
                double leftWidth;
                double? totalWidth = null;
                if (double.TryParse(textPrecursorFilterMz.Text, out leftWidth))
                {
                    double rightWidth;
                    if (double.TryParse(textRightPrecursorFilterMz.Text, out rightWidth))
                        totalWidth = leftWidth + rightWidth;
                    else
                        totalWidth = leftWidth*2;
                }
                textPrecursorFilterMz.Text = totalWidth.HasValue
                    ? totalWidth.Value.ToString(CultureInfo.CurrentCulture) : "";
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
            labelPrecursorIsotopeFilter.Text = percentType ? "Min % of base pea&k:" : "Pea&ks:";
            labelPrecursorIsotopeFilterPercent.Visible = percentType;
            
            if (precursorIsotopes == FullScanPrecursorIsotopes.None)
            {
                textPrecursorIsotopeFilter.Text = "";
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
                                                          : "";
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
                comboPrecursorIsotopes.SelectedItem = FullScanPrecursorIsotopes.Count.ToString();
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

        private static void SetAnalyzerType(FullScanMassAnalyzerType analyzerTypeNew,
                                            FullScanMassAnalyzerType analyzerTypeCurrent,
                                            double? resCurrent,
                                            double? resMzCurrent,
                                            Label label,
                                            TextBox textRes,
                                            Label labelAt,
                                            TextBox textAt,
                                            Label labelTh)
        {
            string labelText = "Res&olution:";
            if (analyzerTypeNew == FullScanMassAnalyzerType.none)
            {
                textRes.Enabled = false;
                textRes.Text = "";
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
                    labelText = "Res&olving power:";
                    if (analyzerTypeNew != FullScanMassAnalyzerType.tof)
                    {
                        variableRes = true;
                        textMz = textAt;
                    }
                }

                const string resolvingPowerFormat = "#,0.####";
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

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        #region Functional testing support

        public MassType PrecursorMassType
        {
            get { return Helpers.ParseEnum(comboPrecursorMass.SelectedItem.ToString(), MassType.Monoisotopic); }
            set { comboPrecursorMass.SelectedItem = value.ToString(); }
        }

        public MassType FragmentMassType
        {
            get { return Helpers.ParseEnum(comboIonMass.SelectedItem.ToString(), MassType.Monoisotopic); }
            set { comboIonMass.SelectedItem = value.ToString(); }
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

        public int InstrumentMaxMz
        {
            get { return Int32.Parse(textMaxMz.Text); }
            set { textMaxMz.Text = value.ToString(CultureInfo.CurrentCulture); }
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

        public void EditCEList()
        {
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

        #endregion
    }
}
