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
using System.Linq;
using System.Windows.Forms;
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
            textExclusionWindow.Text = Filter.PrecursorMzWindow != 0 ? Filter.PrecursorMzWindow.ToString() : "";            
            cbAutoSelect.Checked = Filter.AutoSelect;

            _driverIons = new SettingsListBoxDriver<MeasuredIon>(listAlwaysAdd, Settings.Default.MeasuredIonList);
            _driverIons.LoadList(Filter.MeasuredIons);

            // Initialize library settings
            cbLibraryPick.Checked = (Libraries.Pick != TransitionLibraryPick.none);
            panelPick.Visible = cbLibraryPick.Checked; 
            textTolerance.Text = Libraries.IonMatchTolerance.ToString();
            textIonCount.Text = Libraries.IonCount.ToString();
            if (Libraries.Pick == TransitionLibraryPick.filter)
                radioFiltered.Checked = true;
            else if (Libraries.Pick == TransitionLibraryPick.all_plus)
                radioAllAndFiltered.Checked = true;

            // Initialize instrument settings
            textMinMz.Text = Instrument.MinMz.ToString();
            textMaxMz.Text = Instrument.MaxMz.ToString();
            cbDynamicMinimum.Checked = Instrument.IsDynamicMin;
            textMzMatchTolerance.Text = Instrument.MzMatchTolerance.ToString();
            if (Instrument.MaxTransitions.HasValue)
                textMaxTrans.Text = Instrument.MaxTransitions.Value.ToString();

            comboPrecursorAnalyzerType.Items.Add("None");
            comboPrecursorAnalyzerType.Items.AddRange(TransitionFullScan.MASS_ANALYZERS);
            var precursorAnalyzerType = FullScan.PrecursorMassAnalyzer;
            if (precursorAnalyzerType == FullScanMassAnalyzerType.none)
                comboPrecursorAnalyzerType.SelectedIndex = 0;
            else
                comboPrecursorAnalyzerType.SelectedItem = TransitionFullScan.MassAnalyzerToString(precursorAnalyzerType);

            comboPrecursorFilterType.Items.AddRange(
                new[]
                    {
                        FullScanPrecursorFilterType.None.ToString(),
                        FullScanPrecursorFilterType.Single.ToString(),
                        FullScanPrecursorFilterType.Multiple.ToString()
                    });
            comboProductAnalyzerType.Items.AddRange(TransitionFullScan.MASS_ANALYZERS);            
            comboPrecursorFilterType.SelectedItem = FullScan.PrecursorFilterType.ToString();

            // Update the product analyzer type in case the SelectedIndex is still -1
            UpdateProductAnalyzerType();
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
                e.Cancel = true;
                return;
            }
            types = types.Distinct().ToArray();

            double exclusionWindow = 0;
            if (!string.IsNullOrEmpty(textExclusionWindow.Text) && !Equals(textExclusionWindow.Text, exclusionWindow.ToString()))
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

            TransitionInstrument instrument = new TransitionInstrument(minMz, maxMz, isDynamicMin, mzMatchTolerance, maxTrans);
            Helpers.AssignIfEquals(ref instrument, Instrument);

            FullScanPrecursorFilterType precursorFilterType = PrecursorFilterTypeCurrent;
            double? precursorFilter = null;
            FullScanMassAnalyzerType productAnalyzerType = FullScanMassAnalyzerType.none;
            double? productRes = null;
            double? productResMz = null;

            FullScanMassAnalyzerType precursorAnalyzerType = TransitionFullScan.ParseMassAnalyzer(
                comboPrecursorAnalyzerType.SelectedItem.ToString());
            double? precursorRes = null;
            double? precursorResMz = null;
            if (precursorAnalyzerType != FullScanMassAnalyzerType.none)
            {
                double minFilt, maxFilt;
                if (precursorAnalyzerType == FullScanMassAnalyzerType.qit)
                {
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
                if (precursorFilterType == FullScanPrecursorFilterType.Single)
                {
                    minFilt = TransitionFullScan.MIN_PRECURSOR_SINGLE_FILTER;
                    maxFilt = TransitionFullScan.MAX_PRECURSOR_SINGLE_FILTER;                    
                }
                else
                {
                    minFilt = TransitionFullScan.MIN_PRECURSOR_MULTI_FILTER;
                    maxFilt = TransitionFullScan.MAX_PRECURSOR_MULTI_FILTER;
                }
                double precFilt;
                if (!helper.ValidateDecimalTextBox(e, tabControl1, (int)TABS.FullScan, textPrecursorFilterMz,
                        minFilt, maxFilt, out precFilt))
                    return;
                precursorFilter = precFilt;

                productAnalyzerType = TransitionFullScan.ParseMassAnalyzer(
                    comboProductAnalyzerType.SelectedItem.ToString());
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

            var fullScan = new TransitionFullScan(precursorFilterType,
                                                  precursorFilter,
                                                  productAnalyzerType,
                                                  productRes,
                                                  productResMz,
                                                  precursorAnalyzerType,
                                                  precursorRes,
                                                  precursorResMz);

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
                textIonCount.Text = countFinder.Count.ToString();
                if (!radioAllAndFiltered.Checked)
                    radioAll.Checked = true;
                radioFiltered.Enabled = false;
            }
            else
            {
                textIonCount.Text = Libraries.IonCount.ToString();
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

        private void comboPrecursorFilterType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var precursorFilterType = PrecursorFilterTypeCurrent;
            if (precursorFilterType == FullScanPrecursorFilterType.None)
            {
                textPrecursorFilterMz.Text = "";
                textPrecursorFilterMz.Enabled = false;
                // Selection change should set filter m/z textbox correctly
                comboProductAnalyzerType.SelectedIndex = -1;
                comboProductAnalyzerType.Enabled = false;
            }
            else
            {
                if (precursorFilterType == FullScan.PrecursorFilterType)
                {
                    textPrecursorFilterMz.Text = FullScan.PrecursorFilter.ToString();
                    if (!textPrecursorFilterMz.Enabled)
                        comboProductAnalyzerType.SelectedItem = TransitionFullScan.MassAnalyzerToString(FullScan.ProductMassAnalyzer);
                }
                else
                {
                    if (precursorFilterType == FullScanPrecursorFilterType.Multiple)
                        textPrecursorFilterMz.Text = TransitionFullScan.DEFAULT_PRECURSOR_MULTI_FILTER.ToString();
                    else
                    {
                        double precursorFilter;
                        if (double.TryParse(textMzMatchTolerance.Text, out precursorFilter))
                            precursorFilter *= 2;
                        else
                            precursorFilter = TransitionFullScan.DEFAULT_PRECURSOR_SINGLE_FILTER;
                        textPrecursorFilterMz.Text = precursorFilter.ToString();
                    }
                    if (!textPrecursorFilterMz.Enabled)
                        comboProductAnalyzerType.SelectedItem = TransitionFullScan.MassAnalyzerToString(FullScanMassAnalyzerType.qit);
                }
                textPrecursorFilterMz.Enabled = true;
                comboProductAnalyzerType.Enabled = true;
            }            
        }

        private void comboProductAnalyzerType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateProductAnalyzerType();
        }

        private void UpdateProductAnalyzerType()
        {
            var productAnalyzerType = TransitionFullScan.ParseMassAnalyzer((string) comboProductAnalyzerType.SelectedItem);
            SetAnalyzerType(productAnalyzerType,
                            FullScan.ProductMassAnalyzer,
                            FullScan.ProductRes,
                            FullScan.ProductResMz,
                            labelProductRes,
                            textProductRes,
                            labelProductAt,
                            textProductAt,
                            labelProductTh);
        }

        private void comboPrecursorAnalyzerType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePrecursorAnalyzerType();
        }

        private void UpdatePrecursorAnalyzerType()
        {
            var precursorMassAnalyzer = TransitionFullScan.ParseMassAnalyzer((string)comboPrecursorAnalyzerType.SelectedItem);
            SetAnalyzerType(precursorMassAnalyzer,
                            FullScan.PrecursorMassAnalyzer,
                            FullScan.PrecursorRes,
                            FullScan.PrecursorResMz,
                            labelPrecursorRes,
                            textPrecursorRes,
                            labelPrecursorAt,
                            textPrecursorAt,
                            labelPrecursorTh);
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
                if (resMzCurrent.HasValue)
                    textAt.Text = resMzCurrent.ToString();
                else
                    textAt.Text = TransitionFullScan.DEFAULT_RES_MZ.ToString();

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
            set { textMaxMz.Text = value.ToString(); }
        }

        public double MZMatchTolerance
        {
            get { return Double.Parse(textMzMatchTolerance.Text); }
            set { textMzMatchTolerance.Text = value.ToString(); }
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
            set { textIonCount.Text = value.ToString(); }
        }

        #endregion
    }
}