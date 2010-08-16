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
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.EditUI
{
    public partial class RefineDlg : Form
    {
        private readonly SrmSettings _settings;

        public RefineDlg(SrmDocument document)
        {
            _settings = document.Settings;

            InitializeComponent();

            Icon = Resources.Skyline;

            // Fill label type combo box
            comboRefineLabelType.Items.Add("");
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
                groupLibCorr.Enabled = true;
                labelMinDotProduct.Enabled = true;
                textMinDotProduct.Enabled = true;
            }
        }

        public RefinementSettings RefinementSettings { get; private set; }

        public string MaxTransitionPeakRank
        { 
            get { return textMaxPeakRank.Text;}
            set { textMaxPeakRank.Text = value; }
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

        public string RTRegressionThreshold
        {
            get { return textRTRegressionThreshold.Text; }
            set { textRTRegressionThreshold.Text = value; }
        }

        public string DotProductThreshold
        {
            get { return textMinDotProduct.Text; }
            set { textMinDotProduct.Text = value; }
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            int? minPeptidesPerProtein = null;
            if (!string.IsNullOrEmpty(textMinPeptides.Text))
            {
                int minVal;
                if (!helper.ValidateNumberTextBox(e, tabControl1, 0, textMinPeptides, 0, 10, out minVal))
                    return;
                minPeptidesPerProtein = minVal;
            }
            int? minTransitionsPerPrecursor = null;
            if (!string.IsNullOrEmpty(textMinTransitions.Text))
            {
                int minVal;
                if (!helper.ValidateNumberTextBox(e, tabControl1, 0, textMinTransitions, 0, 100, out minVal))
                    return;
                minTransitionsPerPrecursor = minVal;
            }
            bool removeDuplicatePeptides = cbRemoveDuplicatePeptides.Checked;
            bool removeRepeatedPeptides = cbRemoveRepeatedPeptides.Checked;

            IsotopeLabelType refineLabelType = null;
            string refineTypeName = comboRefineLabelType.SelectedItem.ToString();
            if (!string.IsNullOrEmpty(refineTypeName))
            {
                var typedMods = _settings.PeptideSettings.Modifications.GetModificationsByName(refineTypeName);
                refineLabelType = typedMods.LabelType;
            }

            bool addLabelType = cbAdd.Checked;

            double? minPeakFoundRatio = null, maxPeakFoundRatio = null;
            if (!string.IsNullOrEmpty(textMinPeakFoundRatio.Text))
            {
                double minVal;
                if (!helper.ValidateDecimalTextBox(e, tabControl1, 1, textMinPeakFoundRatio, 0, 1, out minVal))
                    return;
                minPeakFoundRatio = minVal;
            }
            if (!string.IsNullOrEmpty(textMaxPeakFoundRatio.Text))
            {
                double maxVal;
                if (!helper.ValidateDecimalTextBox(e, tabControl1, 1, textMaxPeakFoundRatio, 0, 1, out maxVal))
                    return;
                maxPeakFoundRatio = maxVal;
            }
            if (minPeakFoundRatio.HasValue && maxPeakFoundRatio.HasValue &&
                    minPeakFoundRatio.Value > maxPeakFoundRatio.Value)
            {
                helper.ShowTextBoxError(textMaxPeakFoundRatio, "{0} must be less than min peak found ratio.");
                return;
            }

            int? maxPeakRank = null;
            if (!string.IsNullOrEmpty(textMaxPeakRank.Text))
            {
                int maxVal;
                if (!helper.ValidateNumberTextBox(e, tabControl1, 1, textMaxPeakRank, 2, 10, out maxVal))
                    return;
                maxPeakRank = maxVal;
            }

            bool removeMissingResults = radioRemoveMissing.Checked;

            double? rtRegressionThreshold = null;
            if (!string.IsNullOrEmpty(textRTRegressionThreshold.Text))
            {
                double minVal;
                if (!helper.ValidateDecimalTextBox(e, tabControl1, 1, textRTRegressionThreshold, 0, 1, out minVal))
                    return;
                rtRegressionThreshold = minVal;
            }

            double? dotProductThreshold = null;
            if (!string.IsNullOrEmpty(textMinDotProduct.Text))
            {
                double minVal;
                if (!helper.ValidateDecimalTextBox(e, tabControl1, 1, textMinDotProduct, 0, 1, out minVal))
                    return;
                dotProductThreshold = minVal;
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
                                         MaxPeakRank = maxPeakRank,
                                         PreferLargeIons = cbPreferLarger.Checked,
                                         RemoveMissingResults = removeMissingResults,
                                         RTRegressionThreshold = rtRegressionThreshold,
                                         DotProductThreshold = dotProductThreshold,
                                         UseBestResult = useBestResult
                                     };

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void cbAdd_CheckedChanged(object sender, EventArgs e)
        {
            labelLabelType.Text = (cbAdd.Checked ? "Add la&bel type:" : "Remove la&bel type:");
        }

        private void textMaxPeakRank_TextChanged(object sender, EventArgs e)
        {
            cbPreferLarger.Enabled = !string.IsNullOrEmpty(textMaxPeakRank.Text);
            if (!cbPreferLarger.Enabled)
                cbPreferLarger.Checked = false;
        }

        public void SetMinTransitions(int minTransitions)
        {
            textMinTransitions.Text = minTransitions.ToString();
        }
    }
}
