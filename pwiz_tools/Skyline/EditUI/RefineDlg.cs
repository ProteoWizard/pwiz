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

namespace pwiz.Skyline.EditUI
{
    public partial class RefineDlg : FormEx
    {
        private readonly SrmDocument _document;
        private readonly SrmSettings _settings;

        public RefineDlg(SrmDocument document)
        {
            _document = document;
            _settings = document.Settings;

            InitializeComponent();

            Icon = Resources.Skyline;

            // Fill label type combo box
            comboRefineLabelType.Items.Add(string.Empty);
            comboRefineLabelType.Items.Add(IsotopeLabelType.LIGHT_NAME);
            foreach (var typedMods in _settings.PeptideSettings.Modifications.GetHeavyModifications())
                comboRefineLabelType.Items.Add(typedMods.LabelType.Name);
            comboRefineLabelType.SelectedIndex = 0;
            comboReplicateUse.SelectedIndex = 0;

            var settings = document.Settings;
            if (!settings.HasResults)
            {
                tabControl1.TabPages.Remove(tabResults);
            }

            if (settings.PeptideSettings.Libraries.HasLibraries)
            {
                labelMinDotProduct.Enabled = textMinDotProduct.Enabled = groupLibCorr.Enabled = true;
            }
            if (settings.TransitionSettings.FullScan.IsHighResPrecursor)
            {
                labelMinIdotProduct.Enabled = textMinIdotProduct.Enabled = groupLibCorr.Enabled = true;
            }
        }

        public RefinementSettings RefinementSettings { get; private set; }

        public int MaxTransitionPeakRank
        { 
            get { return Convert.ToInt32(textMaxPeakRank.Text);}
            set { textMaxPeakRank.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public bool PreferLargerIons
        {
            get { return cbPreferLarger.Checked;  }
            set { cbPreferLarger.Checked = value; }
        }

        public bool RemoveMissingResults
        {
            get { return radioRemoveMissing.Checked; }
            set { radioRemoveMissing.Checked = value; }
        }

        public double RTRegressionThreshold
        {
            get { return Convert.ToDouble(textRTRegressionThreshold.Text); }
            set { textRTRegressionThreshold.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public double DotProductThreshold
        {
            get { return Convert.ToDouble(textMinDotProduct.Text); }
            set { textMinDotProduct.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public IsotopeLabelType RefineLabelType
        {
            get
            {
                string refineTypeName = comboRefineLabelType.SelectedItem.ToString();
                if (string.IsNullOrEmpty(refineTypeName))
                    return null;
                var typedMods = _settings.PeptideSettings.Modifications.GetModificationsByName(refineTypeName);
                return typedMods.LabelType;
            }

            set { comboRefineLabelType.SelectedItem = value.ToString(); }
        }

        public bool AddLabelType
        {
            get { return cbAdd.Checked; }
            set { cbAdd.Checked = value; }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            int? minPeptidesPerProtein = null;
            if (!string.IsNullOrEmpty(textMinPeptides.Text))
            {
                int minVal;
                if (!helper.ValidateNumberTextBox(tabControl1, 0, textMinPeptides, 0, 10, out minVal))
                    return;
                minPeptidesPerProtein = minVal;
            }
            int? minTransitionsPerPrecursor = null;
            if (!string.IsNullOrEmpty(textMinTransitions.Text))
            {
                int minVal;
                if (!helper.ValidateNumberTextBox(tabControl1, 0, textMinTransitions, 0, 100, out minVal))
                    return;
                minTransitionsPerPrecursor = minVal;
            }
            bool removeDuplicatePeptides = cbRemoveDuplicatePeptides.Checked;
            bool removeRepeatedPeptides = cbRemoveRepeatedPeptides.Checked;

            IsotopeLabelType refineLabelType = RefineLabelType;

            bool addLabelType = cbAdd.Checked;
            // If adding, make sure there is something to add
            if (addLabelType && refineLabelType != null && !CanAddLabelType(refineLabelType))
            {
                MessageDlg.Show(this, string.Format(Resources.RefineDlg_OkDialog_The_label_type__0__cannot_be_added_There_are_no_modifications_for_this_type,
                                                    refineLabelType.Name));
                tabControl1.SelectedIndex = 0;
                comboRefineLabelType.Focus();
                return;
            }

            double? minPeakFoundRatio = null, maxPeakFoundRatio = null;
            if (!string.IsNullOrEmpty(textMinPeakFoundRatio.Text))
            {
                double minVal;
                if (!helper.ValidateDecimalTextBox(tabControl1, 1, textMinPeakFoundRatio, 0, 1, out minVal))
                    return;
                minPeakFoundRatio = minVal;
            }
            if (!string.IsNullOrEmpty(textMaxPeakFoundRatio.Text))
            {
                double maxVal;
                if (!helper.ValidateDecimalTextBox(tabControl1, 1, textMaxPeakFoundRatio, 0, 1, out maxVal))
                    return;
                maxPeakFoundRatio = maxVal;
            }
            if (minPeakFoundRatio.HasValue && maxPeakFoundRatio.HasValue &&
                    minPeakFoundRatio.Value > maxPeakFoundRatio.Value)
            {
                helper.ShowTextBoxError(textMaxPeakFoundRatio,
                                        Resources.RefineDlg_OkDialog__0__must_be_less_than_min_peak_found_ratio);
                return;
            }

            int? maxPepPeakRank = null;
            if (!string.IsNullOrEmpty(textMaxPepPeakRank.Text))
            {
                int maxVal;
                if (!helper.ValidateNumberTextBox(tabControl1, 1, textMaxPepPeakRank, 1, 10, out maxVal))
                    return;
                maxPepPeakRank = maxVal;
            }
            int? maxPeakRank = null;
            if (!string.IsNullOrEmpty(textMaxPeakRank.Text))
            {
                int maxVal;
                if (!helper.ValidateNumberTextBox(tabControl1, 1, textMaxPeakRank, 1, 10, out maxVal))
                    return;
                maxPeakRank = maxVal;
            }

            bool removeMissingResults = radioRemoveMissing.Checked;

            double? rtRegressionThreshold = null;
            if (!string.IsNullOrEmpty(textRTRegressionThreshold.Text))
            {
                double minVal;
                if (!helper.ValidateDecimalTextBox(tabControl1, 1, textRTRegressionThreshold, 0, 1, out minVal))
                    return;
                rtRegressionThreshold = minVal;
            }

            double? dotProductThreshold = null;
            if (!string.IsNullOrEmpty(textMinDotProduct.Text))
            {
                double minVal;
                if (!helper.ValidateDecimalTextBox(tabControl1, 1, textMinDotProduct, 0, 1, out minVal))
                    return;
                dotProductThreshold = minVal;
            }

            double? idotProductThreshold = null;
            if (!string.IsNullOrEmpty(textMinIdotProduct.Text))
            {
                double minVal;
                if (!helper.ValidateDecimalTextBox(tabControl1, 1, textMinIdotProduct, 0, 1, out minVal))
                    return;
                idotProductThreshold = minVal;
            }

            bool useBestResult = comboReplicateUse.SelectedIndex > 0;

            RefinementSettings = new RefinementSettings
                                     {
                                         MinPeptidesPerProtein = minPeptidesPerProtein,
                                         RemoveDuplicatePeptides = removeDuplicatePeptides,
                                         RemoveRepeatedPeptides = removeRepeatedPeptides,
                                         MinTransitionsPepPrecursor = minTransitionsPerPrecursor,
                                         RefineLabelType = refineLabelType,
                                         AddLabelType = addLabelType,
                                         MinPeakFoundRatio = minPeakFoundRatio,
                                         MaxPeakFoundRatio = maxPeakFoundRatio,
                                         MaxPepPeakRank = maxPepPeakRank,
                                         MaxPeakRank = maxPeakRank,
                                         PreferLargeIons = cbPreferLarger.Checked,
                                         RemoveMissingResults = removeMissingResults,
                                         RTRegressionThreshold = rtRegressionThreshold,
                                         DotProductThreshold = dotProductThreshold,
                                         IdotProductThreshold = idotProductThreshold,
                                         UseBestResult = useBestResult,
                                         AutoPickChildrenAll = (cbAutoPeptides.Checked ? PickLevel.peptides : 0) |
                                                               (cbAutoPrecursors.Checked ? PickLevel.precursors : 0) |
                                                               (cbAutoTransitions.Checked ? PickLevel.transitions : 0)
                                     };

            DialogResult = DialogResult.OK;
            Close();
        }

        private bool CanAddLabelType(IsotopeLabelType labelType)
        {
            if (_settings.TryGetPrecursorCalc(labelType, null) != null)
                return true;

            return _document.Molecules.Any(nodePep =>
                _settings.TryGetPrecursorCalc(labelType, nodePep.ExplicitMods) != null);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void cbAdd_CheckedChanged(object sender, EventArgs e)
        {
            labelLabelType.Text = cbAdd.Checked
                                      ? Resources.RefineDlg_cbAdd_CheckedChanged_Add_label_type
                                      : Resources.RefineDlg_cbAdd_CheckedChanged_Remove_label_type;
        }

        private void textMaxPeakRank_TextChanged(object sender, EventArgs e)
        {
            cbPreferLarger.Enabled = !string.IsNullOrEmpty(textMaxPeakRank.Text);
            if (!cbPreferLarger.Enabled)
                cbPreferLarger.Checked = false;
        }

        #region Functional Test Support

        public int MinTransitions
        {
            get { return Convert.ToInt32(textMinTransitions.Text); }
            set { textMinTransitions.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public int MinPeptides
        {
            get { return int.Parse(textMinPeptides.Text); }
            set { textMinPeptides.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public double MinPeakFoundRatio
        {
            get { return double.Parse(textMinPeakFoundRatio.Text); }
            set { textMinPeakFoundRatio.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        #endregion
    }
}
