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
        public enum Page { Prediction, Filter }
// ReSharper restore InconsistentNaming

        private readonly SkylineWindow _parent;
        private TransitionSettings _transitionSettings;

        private readonly SettingsListComboDriver<CollisionEnergyRegression> _driverCE;
        private readonly SettingsListComboDriver<DeclusteringPotentialRegression> _driverDP;
        private readonly MessageBoxHelper _helper;

        public TransitionSettingsUI(SkylineWindow parent)
        {
            InitializeComponent();

            // Populate the fragment finder combo boxes
            foreach (string item in TransitionFilter.GetStartFragmentFinderNames())
                comboRangeFrom.Items.Add(item);
            foreach (string item in TransitionFilter.GetEndFragmentFinderNames())
                comboRangeTo.Items.Add(item);

            _helper = new MessageBoxHelper(this);

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
            textIonTypes.Text = Filter.IonTypes.ToArray().ToString(", ");
            comboRangeFrom.SelectedItem = Filter.FragmentRangeFirst.GetKey();
            comboRangeTo.SelectedItem = Filter.FragmentRangeLast.GetKey();
            cbProlene.Checked = Filter.IncludeNProline;
            cbGluAsp.Checked = Filter.IncludeCGluAsp;            
            cbAutoSelect.Checked = Filter.AutoSelect;

            // Initialize library settings
            cbLibraryPick.Checked = (Libraries.Pick != TransitionLibraryPick.none);
            panelPick.Visible = cbLibraryPick.Checked; 
            textTolerance.Text = Libraries.IonMatchTolerance.ToString();
            textIonCount.Text = Libraries.IonCount.ToString();
            if (Libraries.Pick == TransitionLibraryPick.filter)
                radioFiltered.Checked = true;

            // Initialize instrument settings
            textMinMz.Text = Instrument.MinMz.ToString();
            textMaxMz.Text = Instrument.MaxMz.ToString();
            textMzMatchTolerance.Text = Instrument.MzMatchTolerance.ToString();
            if (Instrument.MaxTransitions.HasValue)
                textMaxTrans.Text = Instrument.MaxTransitions.Value.ToString();
        }

        public TransitionPrediction Prediction { get { return _transitionSettings.Prediction; } }
        public TransitionFilter Filter { get { return _transitionSettings.Filter; } }
        public TransitionLibraries Libraries { get { return _transitionSettings.Libraries; } }
        public TransitionInstrument Instrument { get { return _transitionSettings.Instrument; } }

        protected override void OnShown(EventArgs e)
        {
            tabControl1.FocusFirstTabStop();
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();

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
            if (!_helper.ValidateNumberListTextBox(e, textPrecursorCharges, min, max, out precursorCharges))
                return;
            precursorCharges = precursorCharges.Distinct().ToArray();

            int[] productCharges;
            min = Transition.MIN_PRODUCT_CHARGE;
            max = Transition.MAX_PRODUCT_CHARGE;
            if (!_helper.ValidateNumberListTextBox(e, textIonCharges, min, max, out productCharges))
                return;
            productCharges = productCharges.Distinct().ToArray();

            IonType[] types = ArrayUtil.Parse(textIonTypes.Text.ToLower(),
                                              v => (IonType) Enum.Parse(typeof (IonType), v), ',', new IonType[0]);
            if (types.Length == 0)
            {
                _helper.ShowTextBoxError(textIonTypes,
                                         "Ion types must contain a comma separated list of ion types a, b, c, x, y and z.");
                e.Cancel = true;
                return;
            }
            types = types.Distinct().ToArray();

            string fragmentRangeFirst = comboRangeFrom.SelectedItem.ToString();
            string fragmentRangeLast = comboRangeTo.SelectedItem.ToString();
            bool includeNProline = cbProlene.Checked;
            bool includeCGluAsp = cbGluAsp.Checked;
            bool autoSelect = cbAutoSelect.Checked;
            TransitionFilter filter = new TransitionFilter(precursorCharges, productCharges, types,
                                                           fragmentRangeFirst, fragmentRangeLast, includeNProline,
                                                           includeCGluAsp, autoSelect);
            Helpers.AssignIfEquals(ref filter, Filter);

            // Validate and store library settings
            TransitionLibraryPick pick = TransitionLibraryPick.none;
            if (cbLibraryPick.Checked)
            {
                if (radioAll.Checked)
                    pick = TransitionLibraryPick.all;
                else
                    pick = TransitionLibraryPick.filter;
            }

            double ionMatchTolerance;

            double minTol = TransitionLibraries.MIN_MATCH_TOLERANCE;
            double maxTol = TransitionLibraries.MAX_MATCH_TOLERANCE;
            if (!_helper.ValidateDecimalTextBox(e, textTolerance, minTol, maxTol, out ionMatchTolerance))
                return;

            int ionCount = Libraries.IonCount;

            if (pick != TransitionLibraryPick.none)
            {
                min = TransitionLibraries.MIN_ION_COUNT;
                max = TransitionLibraries.MAX_ION_COUNT;
                if (!_helper.ValidateNumberTextBox(e, textIonCount, min, max, out ionCount))
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
            if (!_helper.ValidateNumberTextBox(e, textMinMz, min, max, out minMz))
                return;
            int maxMz;
            min = minMz + TransitionInstrument.MIN_MZ_RANGE;
            max = TransitionInstrument.MAX_MEASURABLE_MZ;
            if (!_helper.ValidateNumberTextBox(e, textMaxMz, min, max, out maxMz))
                return;
            double mzMatchTolerance;
            minTol = TransitionInstrument.MIN_MZ_MATCH_TOLERANCE;
            maxTol = TransitionInstrument.MAX_MZ_MATCH_TOLERANCE;
            if (!_helper.ValidateDecimalTextBox(e, textMzMatchTolerance, minTol, maxTol, out mzMatchTolerance))
                return;
            int? maxTrans = null;
            if (!string.IsNullOrEmpty(textMaxTrans.Text))
            {
                int maxTransTemp;
                min = TransitionInstrument.MIN_TRANSITION_MAX;
                max = TransitionInstrument.MAX_TRANSITION_MAX;
                if (!_helper.ValidateNumberTextBox(e, textMaxTrans, min, max, out maxTransTemp))
                    return;
                maxTrans = maxTransTemp;
            }

            TransitionInstrument instrument = new TransitionInstrument(minMz, maxMz, mzMatchTolerance, maxTrans);
            Helpers.AssignIfEquals(ref instrument, Instrument);

            TransitionSettings settings = new TransitionSettings(prediction,
                filter, libraries, integration, instrument);

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
            Close();
        }

        private void comboRangeTo_SelectedIndexChanged(object sender, EventArgs e)
        {
            string fragmentRangeLastName = comboRangeTo.SelectedItem.ToString();
            var countFinder = TransitionFilter.GetEndFragmentFinder(fragmentRangeLastName) as IEndCountFragmentFinder;
            if (countFinder != null)
            {
                textIonCount.Text = countFinder.Count.ToString();
                radioAll.Checked = true;
                radioAll.Enabled = false;
                radioFiltered.Enabled = false;
            }
            else
            {
                textIonCount.Text = Libraries.IonCount.ToString();
                radioAll.Enabled = true;
                radioFiltered.Enabled = true;
                bool pickAll = Libraries.Pick == TransitionLibraryPick.all;
                radioAll.Checked = pickAll;
                radioFiltered.Checked = !pickAll;
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

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        #region Functional testing support

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

        public string InstrumentMaxMz
        {
            get { return textMaxMz.Text; }
            set { textMaxMz.Text = value; }
        }

        public string RegressionCE
        {
            get { return comboCollisionEnergy.SelectedItem.ToString(); }
            set { comboCollisionEnergy.SelectedItem = value; }
        }

        public string RegressionDP
        {
            get { return comboDeclusterPotential.SelectedItem.ToString(); }
            set { comboDeclusterPotential.SelectedItem = value; }
        }

        public void EditCEList()
        {
            _driverCE.EditList();
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

        #endregion
    }
}