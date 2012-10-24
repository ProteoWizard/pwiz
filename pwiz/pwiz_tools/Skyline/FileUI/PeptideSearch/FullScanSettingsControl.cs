/*
 * Original author: Tahmina Jahan <tabaker .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class FullScanSettingsControl : UserControl
    {
        private SettingsListComboDriver<IsotopeEnrichments> _driverEnrichments;

        public FullScanSettingsControl(SkylineWindow skylineWindow)
        {
            SkylineWindow = skylineWindow;
            
            InitializeComponent();
        }

        private SkylineWindow SkylineWindow { get; set; }
        private Form WizardForm { get { return FormEx.GetParentForm(this); } }

        private TransitionSettings TransitionSettings { get { return SkylineWindow.DocumentUI.Settings.TransitionSettings; } }
        public TransitionFullScan FullScan { get { return TransitionSettings.FullScan; } }

        public FullScanPrecursorIsotopes PrecursorIsotopesCurrent
        {
            get
            {
                return Helpers.ParseEnum((string)comboPrecursorIsotopes.SelectedItem,
                    FullScanPrecursorIsotopes.None);
            }

            set { comboPrecursorIsotopes.SelectedItem = value.ToString(); }
        }

        public string Peaks
        {
            get { return textPrecursorIsotopeFilter.Text; }
            set { textPrecursorIsotopeFilter.Text = value; }
        }

        private FullScanMassAnalyzerType PrecursorMassAnalyzer
        {
            get
            {
                return TransitionFullScan.ParseMassAnalyzer((string)comboPrecursorAnalyzerType.SelectedItem);
            }

            set { comboPrecursorAnalyzerType.SelectedItem = TransitionFullScan.MassAnalyzerToString(value); }
        }

        public void InitializeMs1FullScanSettingsPage()
        {
            // TODO: Share more code.
            comboPrecursorIsotopes.Items.AddRange(
                new object[]
                    {
                        FullScanPrecursorIsotopes.None.ToString(),
                        FullScanPrecursorIsotopes.Count.ToString(),
                        FullScanPrecursorIsotopes.Percent.ToString()
                    });
            PrecursorIsotopesCurrent = FullScan.PrecursorIsotopes;

            comboPrecursorAnalyzerType.Items.AddRange(TransitionFullScan.MASS_ANALYZERS.Cast<object>().ToArray());

            _driverEnrichments = new SettingsListComboDriver<IsotopeEnrichments>(comboEnrichments,
                                                                     Settings.Default.IsotopeEnrichmentsList);
            var sel = (FullScan.IsotopeEnrichments != null ? FullScan.IsotopeEnrichments.Name : null);
            _driverEnrichments.LoadList(sel);

            UpdatePrecursorAnalyzerType();
            PrecursorMassAnalyzer = FullScan.PrecursorMassAnalyzer;

            tbxTimeAroundMs2Ids.Text = TransitionSettingsUI.DEFAULT_TIME_AROUND_MS2_IDS.ToString(CultureInfo.CurrentUICulture);
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

        private void UpdatePrecursorAnalyzerType()
        {
            var precursorMassAnalyzer = PrecursorMassAnalyzer;
            TransitionSettingsUI.SetAnalyzerType(PrecursorMassAnalyzer,
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

        private void comboEnrichments_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverEnrichments.SelectedIndexChangedEvent(sender, e);
        }

        private void RadioNoiseAroundMs2IdsCheckedChanged(object sender, EventArgs e)
        {
            tbxTimeAroundMs2Ids.Enabled = radioTimeAroundMs2Ids.Checked;
        }

        public bool UpdateMS1FullScanSettings()
        {
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(WizardForm);

            // Validate and store MS1 full-scan settings
            FullScanMassAnalyzerType productAnalyzerType = FullScanMassAnalyzerType.none;
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
                if (!helper.ValidateDecimalTextBox(e, textPrecursorIsotopeFilter,
                                                   minFilt, maxFilt, out precIsotopeFilt))
                    return false;
                precursorIsotopeFilter = precIsotopeFilt;

                precursorAnalyzerType = PrecursorMassAnalyzer;
                if (precursorAnalyzerType == FullScanMassAnalyzerType.qit)
                {
                    if (precursorIsotopes != FullScanPrecursorIsotopes.Count || precursorIsotopeFilter != 1)
                    {
                        helper.ShowTextBoxError(textPrecursorIsotopeFilter,
                                                 "For MS1 filtering with a QIT mass analyzer only 1 isotope peak is supported.");
                        return false;
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
                if (!helper.ValidateDecimalTextBox(e, textPrecursorRes,
                        minFilt, maxFilt, out precRes))
                    return false;
                precursorRes = precRes;
                if (precursorAnalyzerType != FullScanMassAnalyzerType.qit &&
                    precursorAnalyzerType != FullScanMassAnalyzerType.tof)
                {
                    double precResMz;
                    if (!helper.ValidateDecimalTextBox(e, textPrecursorAt,
                            TransitionFullScan.MIN_RES_MZ, TransitionFullScan.MAX_RES_MZ, out precResMz))
                        return false;
                    precursorResMz = precResMz;
                }
            }


            // If high resolution MS1 filtering is enabled, make sure precursor m/z type
            // is monoisotopic and isotope enrichments are set
            var precursorMassType = TransitionSettings.Prediction.PrecursorMassType;
            IsotopeEnrichments enrichments = null;
            if (precursorIsotopes != FullScanPrecursorIsotopes.None &&
                    precursorAnalyzerType != FullScanMassAnalyzerType.qit)
            {
                if (precursorMassType != MassType.Monoisotopic)
                {
                    precursorMassType = MassType.Monoisotopic;
                }

                enrichments = _driverEnrichments.SelectedItem;
                if (enrichments == null)
                {
                    MessageDlg.Show(WizardForm, "Isotope enrichment settings are required for MS1 filtering on high resolution mass spectrometers.");
                    comboEnrichments.Focus();
                    return false;
                }
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
                    MessageDlg.Show(WizardForm, "This is not a valid number of minutes.");
                    tbxTimeAroundMs2Ids.Focus();
                    return false;
                }
            }
            else
            {
                retentionTimeFilterType = RetentionTimeFilterType.none;
            }

            var fullScan = new TransitionFullScan(FullScan.AcquisitionMethod,
                                                  FullScan.IsolationScheme,
                                                  productAnalyzerType,
                                                  FullScan.ProductResMz,
                                                  FullScan.ProductResMz,
                                                  precursorIsotopes,
                                                  precursorIsotopeFilter,
                                                  precursorAnalyzerType,
                                                  precursorRes,
                                                  precursorResMz,
                                                  enrichments,
                                                  retentionTimeFilterType,
                                                  timeAroundMs2Ids);



            Helpers.AssignIfEquals(ref fullScan, FullScan);

            TransitionPrediction prediction = new TransitionPrediction(precursorMassType,
                                                           TransitionSettings.Prediction.FragmentMassType,
                                                           TransitionSettings.Prediction.CollisionEnergy,
                                                           TransitionSettings.Prediction.DeclusteringPotential,
                                                           TransitionSettings.Prediction.OptimizedMethodType);
            Helpers.AssignIfEquals(ref prediction, TransitionSettings.Prediction);

            TransitionSettings settings = new TransitionSettings(prediction, TransitionSettings.Filter,
                TransitionSettings.Libraries, TransitionSettings.Integration, TransitionSettings.Instrument, fullScan);

            // Only update, if anything changed
            if (!Equals(settings, TransitionSettings))
            {
                SrmSettings newSettings = SkylineWindow.DocumentUI.Settings.ChangeTransitionSettings(settings);
                if (!SkylineWindow.ChangeSettings(newSettings, true))
                {
                    e.Cancel = true;
                    return false;
                }
            }

            // MS1 filtering must be enabled
            if (!FullScan.IsEnabledMs)
            {
                MessageDlg.Show(WizardForm, "Full-scan MS1 filtering must be enabled in order to import peptide search.");
                return false;
            }

            return true;
        }
    }
}
