/*
 * Original author: Tahmina Jahan <tabaker .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class FullScanSettingsControl : UserControl
    {
        private SettingsListComboDriver<IsotopeEnrichments> _driverEnrichments;
        private SettingsListComboDriver<IsolationScheme> _driverIsolationScheme;

        /// <summary>
        /// Previous value of the Acquisition Method combo box
        /// </summary>
        private IsolationScheme _prevval_comboIsolationScheme;

        public FullScanSettingsControl(SkylineWindow skylineWindow)
        {
            SkylineWindow = skylineWindow;

            InitializeComponent();

            InitializeMs1FilterUI();
            InitializeMsMsFilterUI();
            InitializeRetentionTimeFilterUI();

            // Update the precursor analyzer type in case the SelectedIndex is still -1
            UpdatePrecursorAnalyzerType();
            UpdateProductAnalyzerType();

            PrecursorIsotopesCurrent = FullScan.PrecursorIsotopes;
            PrecursorMassAnalyzer = FullScan.PrecursorMassAnalyzer;

            _prevval_comboIsolationScheme = IsolationScheme; // initialize previous value to initial value
        }

        private SkylineWindow SkylineWindow { get; set; }
        private TransitionSettings TransitionSettings { get { return SkylineWindow.DocumentUI.Settings.TransitionSettings; } }
        public TransitionFullScan FullScan { get { return TransitionSettings.FullScan; } }

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
                return TransitionFullScan.ParseMassAnalyzer((string)comboPrecursorAnalyzerType.SelectedItem);
            }

            set { comboPrecursorAnalyzerType.SelectedItem = TransitionFullScan.MassAnalyzerToString(value); }
        }

        public FullScanAcquisitionMethod AcquisitionMethod
        {
            get
            {
                if (null == comboAcquisitionMethod.SelectedItem)
                {
                    return FullScanAcquisitionMethod.None;
                }
                return FullScanAcquisitionExtension.GetEnum(comboAcquisitionMethod.SelectedItem.ToString(),
                    FullScanAcquisitionMethod.None);
            }

            set { comboAcquisitionMethod.SelectedItem = value.GetLocalizedString(); }
        }

        public FullScanMassAnalyzerType ProductMassAnalyzer
        {
            get
            {
                return TransitionFullScan.ParseMassAnalyzer((string)comboProductAnalyzerType.SelectedItem);
            }

            set { comboProductAnalyzerType.SelectedItem = TransitionFullScan.MassAnalyzerToString(value); }
        }

        public IsotopeEnrichments Enrichments
        {
            get
            {
                return _driverEnrichments.SelectedItem;
            }
        }

        public IsolationScheme IsolationScheme
        {
            get
            {
                return _driverIsolationScheme.SelectedItem;
            }
        }

        public int Peaks
        {
            get { return int.Parse(textPrecursorIsotopeFilter.Text); }
            set { textPrecursorIsotopeFilter.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public double? PrecursorRes
        {
            get
            {
                double precursorRes;
                return double.TryParse(textPrecursorRes.Text, out precursorRes) ? (double?)precursorRes : null;
            }
            set { textPrecursorRes.Text = value.ToString(); }
        }

        public double? PrecursorResMz
        {
            get
            {
                double precursorResMz;
                return double.TryParse(textPrecursorAt.Text, out precursorResMz) ? (double?)precursorResMz : null;
            }
            set { textPrecursorAt.Text = value.ToString(); }
        }

        public double? ProductRes
        {
            get
            {
                double productRes;
                return double.TryParse(textProductRes.Text, out productRes) ? (double?)productRes : null;
            }
            set { textProductRes.Text = value.ToString(); }
        }

        public double? ProductResMz
        {
            get
            {
                double productResMz;
                return double.TryParse(textProductAt.Text, out productResMz) ? (double?)productResMz : null;
            }
            set { textProductAt.Text = value.ToString(); }
        }

        public RetentionTimeFilterType RetentionTimeFilterType
        {
            get
            {
                RetentionTimeFilterType retentionTimeFilterType;
                if (radioUseSchedulingWindow.Checked)
                {
                    retentionTimeFilterType = RetentionTimeFilterType.scheduling_windows;
                }
                else if (radioTimeAroundMs2Ids.Checked)
                {
                    retentionTimeFilterType = RetentionTimeFilterType.ms2_ids;
                }
                else
                {
                    retentionTimeFilterType = RetentionTimeFilterType.none;
                }

                return retentionTimeFilterType;
            }
            set
            {
                switch (value)
                {
                    case RetentionTimeFilterType.scheduling_windows:
                        radioUseSchedulingWindow.Checked = true;
                        break;
                    case RetentionTimeFilterType.ms2_ids:
                        radioTimeAroundMs2Ids.Checked = true;
                        break;
                    default:
                        radioKeepAllTime.Checked = true;
                        break;
                }
            }
        }

        public TextBox PrecursorChargesTextBox
        {
            get { return textPrecursorCharges; }
        }

        public int[] PrecursorCharges
        {
            set { textPrecursorCharges.Text = value.ToArray().ToString(", "); } // Not L10N
        }

        private void InitializeMs1FilterUI()
        {
            _driverEnrichments = new SettingsListComboDriver<IsotopeEnrichments>(comboEnrichments,
                                                                     Settings.Default.IsotopeEnrichmentsList);
            var sel = (FullScan.IsotopeEnrichments != null ? FullScan.IsotopeEnrichments.Name : null);
            _driverEnrichments.LoadList(sel);

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
        }

        public void UpdatePrecursorAnalyzerType()
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
                labelPrecursorTh,
                labelPrecursorPPM);

            // For QIT, only 1 isotope peak is allowed
            if (precursorMassAnalyzer == FullScanMassAnalyzerType.qit)
            {
                comboPrecursorIsotopes.SelectedItem = FullScanPrecursorIsotopes.Count.GetLocalizedString();
                textPrecursorIsotopeFilter.Text = 1.ToString(LocalizationHelper.CurrentCulture);
                comboEnrichments.SelectedIndex = -1;
                comboEnrichments.Enabled = false;
            }
            else if (precursorMassAnalyzer != FullScanMassAnalyzerType.none && !comboEnrichments.Enabled)
            {
                comboEnrichments.SelectedIndex = 0;
                comboEnrichments.Enabled = true;
            }
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
                                                          ? FullScan.PrecursorIsotopeFilter.Value.ToString(LocalizationHelper.CurrentCulture)
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
                                                           : TransitionFullScan.DEFAULT_ISOTOPE_COUNT).ToString(LocalizationHelper.CurrentCulture);

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
            UpdateRetentionTimeFilterUi();
        }

        private void comboPrecursorAnalyzerType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePrecursorAnalyzerType();
        }

        private void comboEnrichments_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverEnrichments.SelectedIndexChangedEvent(sender, e);
        }

        public bool ValidateFullScanSettings(MessageBoxHelper helper, out TransitionFullScan fullScanSettings, TabControl tabControl = null, int tabIndex = -1)
        {
            fullScanSettings = null;

            double? precursorIsotopeFilter;
            if (!ValidatePrecursorIsotopeFilter(helper, out precursorIsotopeFilter, tabControl, tabIndex))
                return false;

            double? precursorRes;
            if (!ValidatePrecursorRes(helper, precursorIsotopeFilter, out precursorRes, tabControl, tabIndex))
                return false;

            double? precursorResMz;
            if (!ValidatePrecursorResMz(helper, out precursorResMz, tabControl, tabIndex))
                return false;

            double? productRes;
            if (!ValidateProductRes(helper, out productRes, tabControl, tabIndex))
                return false;

            double? productResMz;
            if (!ValidateProductResMz(helper, out productResMz, tabControl, tabIndex))
                return false;

            RetentionTimeFilterType retentionTimeFilterType = RetentionTimeFilterType;
            double retentionTimeFilterLength;
            if (!ValidateRetentionTimeFilterLength(out retentionTimeFilterLength))
                return false;

            fullScanSettings = new TransitionFullScan(AcquisitionMethod,
                                                  IsolationScheme,
                                                  ProductMassAnalyzer,
                                                  productRes,
                                                  productResMz,
                                                  PrecursorIsotopesCurrent,
                                                  precursorIsotopeFilter,
                                                  PrecursorMassAnalyzer,
                                                  precursorRes,
                                                  precursorResMz,
                                                  Enrichments,
                                                  retentionTimeFilterType,
                                                  retentionTimeFilterLength);
            return true;
        }

        public bool ValidatePrecursorIsotopeFilter(MessageBoxHelper helper, out double? precursorIsotopeFilter, TabControl tabControl = null, int tabIndex = -1)
        {
            precursorIsotopeFilter = null;
            FullScanPrecursorIsotopes precursorIsotopes = PrecursorIsotopesCurrent;
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
                bool valid;
                if (null != tabControl)
                {
                    valid = helper.ValidateDecimalTextBox(tabControl, tabIndex, textPrecursorIsotopeFilter,
                                                          minFilt, maxFilt, out precIsotopeFilt);
                }
                else
                {
                    valid = helper.ValidateDecimalTextBox(textPrecursorIsotopeFilter,
                                                          minFilt, maxFilt, out precIsotopeFilt);
                }

                if (!valid)
                    return false;

                precursorIsotopeFilter = precIsotopeFilt;
            }

            return true;
        }

        public bool ValidatePrecursorRes(MessageBoxHelper helper, double? precursorIsotopeFilter, out double? precursorRes, TabControl tabControl = null, int tabIndex = -1)
        {
            precursorRes = null;
            FullScanPrecursorIsotopes precursorIsotopes = PrecursorIsotopesCurrent;
            FullScanMassAnalyzerType precursorAnalyzerType = PrecursorMassAnalyzer;
            if (precursorIsotopes != FullScanPrecursorIsotopes.None)
            {
                if (precursorAnalyzerType == FullScanMassAnalyzerType.qit)
                {
                    if (precursorIsotopes != FullScanPrecursorIsotopes.Count || precursorIsotopeFilter != 1)
                    {
                        if (null != tabControl)
                        {
                            helper.ShowTextBoxError(tabControl, tabIndex, textPrecursorIsotopeFilter,
                                                    Resources.
                                                        TransitionSettingsUI_OkDialog_For_MS1_filtering_with_a_QIT_mass_analyzer_only_1_isotope_peak_is_supported);
                        }
                        else
                        {
                            helper.ShowTextBoxError(textPrecursorIsotopeFilter,
                                                    Resources.
                                                        TransitionSettingsUI_OkDialog_For_MS1_filtering_with_a_QIT_mass_analyzer_only_1_isotope_peak_is_supported);

                        }

                        return false;
                    }
                }
                double minFilt, maxFilt;
                GetFilterMinMax(PrecursorMassAnalyzer, out minFilt, out maxFilt);

                double precRes;
                bool valid;
                if (null != tabControl)
                {
                    valid = helper.ValidateDecimalTextBox(tabControl, tabIndex, textPrecursorRes,
                                                          minFilt, maxFilt, out precRes);
                }
                else
                {
                    valid = helper.ValidateDecimalTextBox(textPrecursorRes,
                                                          minFilt, maxFilt, out precRes);
                }
                if (!valid)
                    return false;

                precursorRes = precRes;
            }

            return true;
        }

        public bool ValidatePrecursorResMz(MessageBoxHelper helper, out double? precursorResMz, TabControl tabControl = null, int tabIndex = -1)
        {
            precursorResMz = null;
            FullScanPrecursorIsotopes precursorIsotopes = PrecursorIsotopesCurrent;
            if (precursorIsotopes != FullScanPrecursorIsotopes.None)
            {
                if (IsResMzAnalyzer(PrecursorMassAnalyzer))
                {
                    double precResMz;
                    bool valid;
                    if (null != tabControl)
                    {
                        valid = helper.ValidateDecimalTextBox(tabControl, tabIndex, textPrecursorAt,
                                                              TransitionFullScan.MIN_RES_MZ,
                                                              TransitionFullScan.MAX_RES_MZ, out precResMz);
                    }
                    else
                    {
                        valid = helper.ValidateDecimalTextBox(textPrecursorAt,
                                                              TransitionFullScan.MIN_RES_MZ,
                                                              TransitionFullScan.MAX_RES_MZ, out precResMz);
                    }

                    if (!valid)
                        return false;

                    precursorResMz = precResMz;
                }
            }

            return true;
        }

        private static bool IsResMzAnalyzer(FullScanMassAnalyzerType precursorAnalyzerType)
        {
            return precursorAnalyzerType == FullScanMassAnalyzerType.orbitrap ||
                   precursorAnalyzerType == FullScanMassAnalyzerType.ft_icr;
        }

        public void EditEnrichmentsList()
        {
            _driverEnrichments.EditList();
        }

        public void AddToEnrichmentsList()
        {
            _driverEnrichments.AddItem();
        }

        public void ComboEnrichmentsSetFocus()
        {
            comboEnrichments.Focus();
        }

        private void InitializeMsMsFilterUI()
        {
            _driverIsolationScheme = new SettingsListComboDriver<IsolationScheme>(comboIsolationScheme,
                                                                                  Settings.Default.IsolationSchemeList);

            string sel = (FullScan.IsolationScheme != null ? FullScan.IsolationScheme.Name : null);
            _driverIsolationScheme.LoadList(sel);

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
        }

        /// <summary>
        /// Callback event handler that will get called if the Acquisition method gets changed
        /// </summary>
        public EventHandler IsolationSchemeChangedEvent { get; set; }

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
            UpdateRetentionTimeFilterUi();
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

        public void UpdateProductAnalyzerType()
        {
            SetAnalyzerType(ProductMassAnalyzer,
                            FullScan.ProductMassAnalyzer,
                            FullScan.ProductRes,
                            FullScan.ProductResMz,
                            labelProductRes,
                            textProductRes,
                            labelProductAt,
                            textProductAt,
                            labelProductTh,
                            labelProductPPM);
        }

        private void comboIsolationScheme_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverIsolationScheme.SelectedIndexChangedEvent(sender, e);
            
            // If we have a callback function and the isolation scheme _did_ really change its value, we invoke the handler
            if (IsolationSchemeChangedEvent != null && !Equals(_prevval_comboIsolationScheme, IsolationScheme))
            {
                IsolationSchemeChangedEvent.Invoke(sender, e);
            }
            _prevval_comboIsolationScheme = IsolationScheme; //update previous isolation scheme
        }

        public void AddIsolationScheme()
        {
            _driverIsolationScheme.AddItem();
        }

        public void EditIsolationScheme()
        {
            _driverIsolationScheme.EditList();
        }

        public void ComboIsolationSchemeSetFocus()
        {
            comboIsolationScheme.Focus();
        }

        public string IsolationSchemeName
        {
            get { return _driverIsolationScheme.Combo.SelectedItem.ToString(); }
            set { _driverIsolationScheme.Combo.SelectedItem = value; }
        }

        public bool ValidateProductRes(MessageBoxHelper helper, out double? productRes, TabControl tabControl = null, int tabIndex = -1)
        {
            FullScanAcquisitionMethod acquisitionMethod = AcquisitionMethod;
            productRes = null;

            if (acquisitionMethod != FullScanAcquisitionMethod.None)
            {
                double minFilt, maxFilt;

                GetFilterMinMax(ProductMassAnalyzer, out minFilt, out maxFilt);

                double prodRes;
                bool valid;
                if (null != tabControl)
                {
                    valid = helper.ValidateDecimalTextBox(tabControl, (int) TransitionSettingsUI.TABS.FullScan,
                                                          textProductRes, minFilt, maxFilt, out prodRes);
                }
                else
                {
                    valid = helper.ValidateDecimalTextBox(textProductRes, minFilt, maxFilt, out prodRes);
                }

                if (!valid)
                    return false;

                productRes = prodRes;
            }

            return true;
        }

        private void GetFilterMinMax(FullScanMassAnalyzerType analyzerType, out double minFilt, out double maxFilt)
        {
            if (analyzerType == FullScanMassAnalyzerType.qit)
            {
                minFilt = TransitionFullScan.MIN_LO_RES;
                maxFilt = TransitionFullScan.MAX_LO_RES;
            }
            else if (analyzerType == FullScanMassAnalyzerType.centroided)
            {
                minFilt = TransitionFullScan.MIN_CENTROID_PPM;
                maxFilt = TransitionFullScan.MAX_CENTROID_PPM;
            }
            else
            {
                minFilt = TransitionFullScan.MIN_HI_RES;
                maxFilt = TransitionFullScan.MAX_HI_RES;
            }
        }

        public bool ValidateProductResMz(MessageBoxHelper helper, out double? productResMz, TabControl tabControl = null, int tabIndex = -1)
        {
            FullScanAcquisitionMethod acquisitionMethod = AcquisitionMethod;
            productResMz = null;

            if (acquisitionMethod != FullScanAcquisitionMethod.None)
            {
                if (IsResMzAnalyzer(ProductMassAnalyzer))
                {
                    double prodResMz;
                    bool valid;
                    if (null != tabControl)
                    {
                        valid = helper.ValidateDecimalTextBox(tabControl, (int) TransitionSettingsUI.TABS.FullScan,
                                                              textProductAt,
                                                              TransitionFullScan.MIN_RES_MZ,
                                                              TransitionFullScan.MAX_RES_MZ, out prodResMz);

                    }
                    else
                    {
                        valid = helper.ValidateDecimalTextBox(textProductAt,
                                                              TransitionFullScan.MIN_RES_MZ,
                                                              TransitionFullScan.MAX_RES_MZ, out prodResMz);


                    }

                    if (!valid)
                    {
                        return false;
                    }

                    productResMz = prodResMz;
                }
            }

            return true;
        }

        private void InitializeRetentionTimeFilterUI()
        {
            tbxTimeAroundMs2Ids.Text = TransitionSettingsUI.DEFAULT_TIME_AROUND_MS2_IDS
                .ToString(CultureInfo.CurrentCulture);
            tbxTimeAroundMs2Ids.Enabled = false;
            tbxTimeAroundPrediction.Text = TransitionSettingsUI.DEFAULT_TIME_AROUND_PREDICTION
                .ToString(CultureInfo.CurrentCulture);
            tbxTimeAroundPrediction.Enabled = false;
            if (FullScan.RetentionTimeFilterType == RetentionTimeFilterType.scheduling_windows)
            {
                radioUseSchedulingWindow.Checked = true;
                tbxTimeAroundPrediction.Text = FullScan.RetentionTimeFilterLength.ToString(CultureInfo.CurrentCulture);
            }
            else if (FullScan.RetentionTimeFilterType == RetentionTimeFilterType.ms2_ids)
            {
                radioTimeAroundMs2Ids.Checked = true;
                tbxTimeAroundMs2Ids.Text = FullScan.RetentionTimeFilterLength.ToString(CultureInfo.CurrentCulture);
            }
            else
            {
                radioKeepAllTime.Checked = true;
            }
        }

        public bool ValidateRetentionTimeFilterLength(out double retentionTimeFilterLength)
        {
            retentionTimeFilterLength = 0;
            if (radioTimeAroundMs2Ids.Checked)
            {
                if (!double.TryParse(tbxTimeAroundMs2Ids.Text, out retentionTimeFilterLength) || retentionTimeFilterLength < 0)
                {
                    MessageDlg.Show(this, Resources.TransitionSettingsUI_OkDialog_This_is_not_a_valid_number_of_minutes);
                    tbxTimeAroundMs2Ids.Focus();
                    return false;
                }
            }
            else if (radioUseSchedulingWindow.Checked)
            {
                if (!double.TryParse(tbxTimeAroundPrediction.Text, out retentionTimeFilterLength) || retentionTimeFilterLength < 0)
                {
                    tbxTimeAroundPrediction.Focus();
                    return false;
                }
            }

            return true;
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
                    tbxTimeAroundPrediction.Text = length.ToString(CultureInfo.CurrentCulture);
                    break;
                case RetentionTimeFilterType.ms2_ids:
                    radioTimeAroundMs2Ids.Checked = true;
                    tbxTimeAroundMs2Ids.Text = length.ToString(CultureInfo.CurrentCulture);
                    break;
                default:
                    // ReSharper disable LocalizableElement
                    throw new ArgumentException("Invalid RetentionTimeFilterType", "retentionTimeFilterType"); // Not L10N
                    // ReSharper restore LocalizableElement
            }
        }

        public double TimeAroundMs2Ids
        {
            get { return double.Parse(tbxTimeAroundMs2Ids.Text); }
        }

        public static void SetAnalyzerType(FullScanMassAnalyzerType analyzerTypeNew,
                                    FullScanMassAnalyzerType analyzerTypeCurrent,
                                    double? resCurrent,
                                    double? resMzCurrent,
                                    Label label,
                                    TextBox textRes,
                                    Label labelAt,
                                    TextBox textAt,
                                    Label labelTh,
                                    Label labelPPM)
        {
            string labelText = Resources.TransitionSettingsUI_SetAnalyzerType_Resolution;
            labelPPM.Visible = false;
            if (analyzerTypeNew == FullScanMassAnalyzerType.none)
            {
                textRes.Enabled = false;
                textRes.Text = string.Empty;
                labelAt.Visible = false;
                textAt.Visible = false;
                labelTh.Left = textRes.Right;
            }
            else if (analyzerTypeNew == FullScanMassAnalyzerType.centroided)
            {
                labelAt.Visible = false;
                labelTh.Visible = false;
                textAt.Visible = false;
                textRes.Enabled = true;
                textRes.Text = resCurrent.HasValue && (analyzerTypeCurrent == analyzerTypeNew)
                                  ? resCurrent.Value.ToString(LocalizationHelper.CurrentCulture)
                                  : TransitionFullScan.DEFAULT_CENTROIDED_PPM.ToString(LocalizationHelper.CurrentCulture);
                labelText = Resources.FullScanSettingsControl_SetAnalyzerType_Mass__Accuracy_;
                labelPPM.Visible = true;
                labelPPM.Left = textRes.Right;
                labelPPM.Top = textRes.Top;
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
                    textRes.Text = TransitionFullScan.DEFAULT_RES_VALUES[(int)analyzerTypeNew].ToString(resolvingPowerFormat);

                labelAt.Visible = variableRes;
                textAt.Visible = variableRes;
                textAt.Text = resMzCurrent.HasValue
                                  ? resMzCurrent.Value.ToString(LocalizationHelper.CurrentCulture)
                                  : TransitionFullScan.DEFAULT_RES_MZ.ToString(LocalizationHelper.CurrentCulture);

                labelTh.Visible = (textMz != null);
                if (textMz != null)
                    labelTh.Left = textMz.Right;
            }
            label.Text = labelText;
        }

        public void ModifyOptionsForImportPeptideSearchWizard(ImportPeptideSearchDlg.Workflow workflow)
        {
            // Reduce MS1 filtering groupbox
            int precursorChargesShift = groupBoxMS2.Top - groupBoxMS1.Bottom;
            labelEnrichments.Hide();
            comboEnrichments.Hide();
            groupBoxMS1.Height = textPrecursorIsotopeFilter.Bottom + groupBoxMS1.Height - comboEnrichments.Bottom;

            if (workflow == ImportPeptideSearchDlg.Workflow.dda)
            {
                // Set up precursor charges input
                textPrecursorCharges.Text = SkylineWindow.Document.Settings.TransitionSettings.Filter.PrecursorCharges.ToArray().ToString(", "); // Not L10N
                int precursorChargesTopDifference = lblPrecursorCharges.Top - groupBoxMS1.Top;
                lblPrecursorCharges.Top = groupBoxMS1.Top;
                textPrecursorCharges.Top -= precursorChargesTopDifference;
                textPrecursorCharges.Show();
                lblPrecursorCharges.Show();

                // Reposition MS1 filtering groupbox
                groupBoxMS1.Top = textPrecursorCharges.Bottom + precursorChargesShift;
            }

            int newRadioTimeAroundTop = radioUseSchedulingWindow.Top;
            int radioTimeAroundTopDifference = radioKeepAllTime.Top - newRadioTimeAroundTop;
            radioUseSchedulingWindow.Hide();
            flowLayoutPanelUseSchedulingWindow.Hide();
            radioKeepAllTime.Top = newRadioTimeAroundTop;
            groupBoxRetentionTimeToKeep.Height -= radioTimeAroundTopDifference;

            // Select defaults
            PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
            radioTimeAroundMs2Ids.Checked = true;

            int nextGroupBoxTop = groupBoxMS1.Bottom + precursorChargesShift;
            if (workflow != ImportPeptideSearchDlg.Workflow.dda)
            {
                groupBoxMS2.Top = nextGroupBoxTop;
                groupBoxRetentionTimeToKeep.Top = groupBoxMS2.Bottom + precursorChargesShift;

                AcquisitionMethod = (workflow == ImportPeptideSearchDlg.Workflow.dia)
                    ? FullScanAcquisitionMethod.DIA
                    : FullScanAcquisitionMethod.Targeted;

                ProductMassAnalyzer = PrecursorMassAnalyzer;

                if (workflow == ImportPeptideSearchDlg.Workflow.dia && Settings.Default.IsolationSchemeList.Count > 1)
                {
                    comboIsolationScheme.SelectedIndex = 1;
                }
            }
            else
            {
                // Hide MS/MS filtering groupbox entirely.
                groupBoxMS2.Hide();

                // Reduce and reposition Retention time filtering groupbox.
                groupBoxRetentionTimeToKeep.Top = nextGroupBoxTop;
            }
        }

        private void radioTimeAroundMs2Ids_CheckedChanged(object sender, EventArgs e)
        {
            UpdateRetentionTimeFilterUi();
        }

        private void radioKeepAllTime_CheckedChanged(object sender, EventArgs e)
        {
            UpdateRetentionTimeFilterUi();
        }

        private void radioUseSchedulingWindow_CheckedChanged(object sender, EventArgs e)
        {
            UpdateRetentionTimeFilterUi();
        }

        private void UpdateRetentionTimeFilterUi()
        {
            bool disabled = AcquisitionMethod == FullScanAcquisitionMethod.None &&
                            PrecursorIsotopesCurrent == FullScanPrecursorIsotopes.None;
            if (disabled)
            {
                groupBoxRetentionTimeToKeep.Enabled = false;
            }
            else
            {
                groupBoxRetentionTimeToKeep.Enabled = true;
            }
            if (radioKeepAllTime.Checked && !disabled)
            {
                radioKeepAllTime.ForeColor = Color.Red;
                toolTip.SetToolTip(radioKeepAllTime,
                    Resources.FullScanSettingsControl_UpdateRetentionTimeFilterUi_Full_gradient_chromatograms_will_take_longer_to_import__consume_more_disk_space__and_may_make_peak_picking_less_effective_);
            }
            else
            {
                radioKeepAllTime.ForeColor = DefaultForeColor;
                toolTip.SetToolTip(radioKeepAllTime, null);
            }
            var timeAroundMs2IdsControls = new List<Control> {radioTimeAroundMs2Ids};
            timeAroundMs2IdsControls.AddRange(flowLayoutPanelTimeAroundMs2Ids.Controls.Cast<Control>());
            string strWarning = null;
            if (radioTimeAroundMs2Ids.Checked && !disabled)
            {
                tbxTimeAroundMs2Ids.Enabled = true;

                var document = SkylineWindow.DocumentUI;
                if (document.PeptideCount > 0)
                {
                    if (!document.Settings.HasLibraries)
                    {
                        strWarning = Resources.FullScanSettingsControl_UpdateRetentionTimeFilterUi_This_document_does_not_contain_any_spectral_libraries_;
                    }
                    else if (document.Peptides.All(
                        peptide => document.Settings.GetUnalignedRetentionTimes(peptide.SourceUnmodifiedTextId, peptide.SourceExplicitMods).Length == 0))
                    {
                        strWarning = Resources.FullScanSettingsControl_UpdateRetentionTimeFilterUi_None_of_the_spectral_libraries_in_this_document_contain_any_retention_times_for_any_of_the_peptides_in_this_document_;
                    }
                }
            }
            else
            {
                tbxTimeAroundMs2Ids.Enabled = false;
            }

            Color foreColor = strWarning == null ? DefaultForeColor : Color.Red;
            foreach (var control in timeAroundMs2IdsControls)
            {
                control.ForeColor = foreColor;
                toolTip.SetToolTip(control, strWarning);
            }

            if (radioUseSchedulingWindow.Checked && !disabled)
            {
                tbxTimeAroundPrediction.Enabled = true;
            }
            else
            {
                tbxTimeAroundPrediction.Enabled = false;
            }
        }

        /// <summary>
        /// Returns true if the user selected DIA as acquisition method and used an 
        /// isolation scheme with predefined windows (not taken from the results),
        /// and did not use the AllIons isolation scheme. This is used to determine 
        /// whether to show the DIA exclusion checkbox or not.
        /// </summary>
        public bool IsDIAAndPreselectedWindows()
        {
            return (IsDIA() && !IsolationScheme.FromResults && !IsolationScheme.IsAllIons);
        }

        /// <summary>
        /// Returns true if the user selected DIA as acquisition method 
        /// </summary>
        /// <returns></returns>
        public bool IsDIA()
        {
            return AcquisitionMethod == FullScanAcquisitionMethod.DIA;
        }

    }
}
