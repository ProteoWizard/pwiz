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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.EditUI
{
    public partial class RefineDlg : FormEx, IMultipleViewProvider, IAuditLogModifier<RefinementSettings>
    {
        // ReSharper disable InconsistentNaming
        public enum TABS { Document, Results, Consistency }
        // ReSharper restore InconsistentNaming
        public class DocumentTab : IFormView { }
        public class ResultsTab : IFormView { }
        public class ConsistencyTab : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new DocumentTab(), new ResultsTab(), new ConsistencyTab(),
        };

        private readonly SrmDocument _document;
        private readonly SrmSettings _settings;

        private readonly string _removeLabelText;
        private readonly string _removeTipText;

        private readonly SettingsListBoxDriver<GroupComparisonDef> _groupComparisonsListBoxDriver;

        private int _standardTypeCount;

        public RefineDlg(IDocumentUIContainer documentContainer)
        {
            _document = documentContainer.DocumentUI;
            _settings = documentContainer.DocumentUI.Settings;
            DocumentContainer = documentContainer;

            InitializeComponent();

            Icon = Resources.Skyline;

            // Save text for later use
            _removeLabelText = labelLabelType.Text;
            _removeTipText = helpTip.GetToolTip(comboRefineLabelType);

            // Fill label type comb_o box
            comboRefineLabelType.Items.Add(string.Empty);
            comboRefineLabelType.Items.Add(IsotopeLabelType.LIGHT_NAME);
            foreach (var typedMods in _settings.PeptideSettings.Modifications.GetHeavyModifications())
                comboRefineLabelType.Items.Add(typedMods.LabelType.Name);
            comboRefineLabelType.SelectedIndex = 0;
            comboReplicateUse.SelectedIndex = 0;

            if (!_settings.HasResults)
            {
                // For some reason we need to preserve and then restore all the tool tips
                // to keep them working in this case. Not sure why.
                var listTips = new List<string>();
                foreach (Control control in tabControl1.TabPages[0].Controls)
                    listTips.Add(helpTip.GetToolTip(control));

                tabControl1.TabPages.Remove(tabResults);

                helpTip.RemoveAll();
                foreach (Control control in tabControl1.TabPages[0].Controls)
                {
                    helpTip.SetToolTip(control, listTips[0]);
                    listTips.RemoveAt(0);
                }
            }

            if (!_settings.HasResults || _settings.MeasuredResults.Chromatograms.Count < 2)
            {
                tabControl1.TabPages.Remove(tabConsistency);
            }
            else
            {
                // Consistency tab
                textQVal.Enabled = _settings.PeptideSettings.Integration.PeakScoringModel.IsTrained;
                numericUpDownDetections.Enabled = textQVal.Enabled;
                if (numericUpDownDetections.Enabled)
                {
                    numericUpDownDetections.Minimum = 1;
                    numericUpDownDetections.Maximum = _document.MeasuredResults.Chromatograms.Count;
                    numericUpDownDetections.Value = 1;
                }

                var mods = _document.Settings.PeptideSettings.Modifications;
                var standardTypes = mods.RatioInternalStandardTypes;
                comboNormalizeTo.Items.Clear();

                if (mods.HasHeavyModifications)
                {
                    comboNormalizeTo.Items.AddRange(standardTypes.Select((s) => s.Title).ToArray());
                    _standardTypeCount = standardTypes.Count;
                }

                var hasGlobalStandard = _document.Settings.HasGlobalStandardArea;
                if (hasGlobalStandard)
                    comboNormalizeTo.Items.Add(Resources.RefineDlg_NormalizationMethod_Global_standards);
                comboNormalizeTo.Items.Add(Resources.RefineDlg_NormalizationMethod_Medians);
                comboNormalizeTo.Items.Add(Resources.RefineDlg_NormalizationMethod_None);
                comboNormalizeTo.SelectedIndex = comboNormalizeTo.Items.Count - 1;

                comboTransitions.Items.Add(Resources.RefineDlg_RefineDlg_all);
                comboTransitions.Items.Add(Resources.RefineDlg_RefineDlg_best);
                comboTransitions.SelectedIndex = 0;

                var maxTrans = _document.MoleculeTransitionGroups.Select(g => g.TransitionCount).DefaultIfEmpty().Max();
                for (int i = 1; i <= maxTrans; i++)
                {
                    comboTransitions.Items.Add(i);
                }

                if (_document.MoleculeTransitions.Any(t => t.IsMs1))
                {
                    comboTransType.Items.Add(Resources.RefineDlg_RefineDlg_Precursors);
                    comboTransType.SelectedIndex = comboTransType.Items.Count - 1;
                }

                if (_document.MoleculeTransitions.Any(t => !t.IsMs1))
                {
                    comboTransType.Items.Add(Resources.RefineDlg_RefineDlg_Products);
                    comboTransType.SelectedIndex = comboTransType.Items.Count - 1;
                }

                if (comboTransType.Items.Count == 1)
                    comboTransType.Enabled = false;
            }

            if (_settings.PeptideSettings.Libraries.HasLibraries)
            {
                labelMinDotProduct.Enabled = textMinDotProduct.Enabled = groupLibCorr.Enabled = true;
            }
            if (_settings.TransitionSettings.FullScan.IsHighResPrecursor)
            {
                labelMinIdotProduct.Enabled = textMinIdotProduct.Enabled = groupLibCorr.Enabled = true;
            }

            // Group Comparisons
            _groupComparisonsListBoxDriver = new SettingsListBoxDriver<GroupComparisonDef>(
                checkedListBoxGroupComparisons, Settings.Default.GroupComparisonDefList);
            _groupComparisonsListBoxDriver.LoadList(
                _document.Settings.DataSettings.GroupComparisonDefs);

            if (_document.PeptideTransitions.Any(t => t.IsMs1))
            {
                comboMSGroupComparisons.Items.Add(Resources.RefineDlg_MSLevel_1);
                comboMSGroupComparisons.SelectedIndex = comboMSGroupComparisons.Items.Count - 1;
            }

            if (_document.PeptideTransitions.Any(t => !t.IsMs1))
            {
                comboMSGroupComparisons.Items.Add(Resources.RefineDlg_MSLevel_2);
                comboMSGroupComparisons.SelectedIndex = comboMSGroupComparisons.Items.Count - 1;
            }

            if (comboMSGroupComparisons.Items.Count == 1)
            {
                comboMSGroupComparisons.Enabled = false;
            }
        }

        private AreaCVNormalizationMethod GetNormalizationMethod(int idx)
        {
            if (idx < 0)
                return AreaCVNormalizationMethod.none;
            if (idx < _standardTypeCount)
            {
                return AreaCVNormalizationMethod.ratio;
            }
            idx -= _standardTypeCount;
            if (!_document.Settings.HasGlobalStandardArea)
                idx++;

            var normalizationMethod = AreaCVNormalizationMethod.none;
            switch (idx)
            {
                case 0:
                    normalizationMethod =
                        _document.Settings.HasGlobalStandardArea
                            ? AreaCVNormalizationMethod.global_standards
                            : AreaCVNormalizationMethod.medians;
                    break;
                case 1:
                    normalizationMethod = AreaCVNormalizationMethod.medians;
                    break;
                case 2:
                    normalizationMethod = AreaCVNormalizationMethod.none;
                    break;
            }

            return normalizationMethod;
        }

        protected override void OnShown(EventArgs e)
        {
            tabControl1.FocusFirstTabStop();
        }

        public IDocumentUIContainer DocumentContainer { get; private set; }

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

        public bool MaxPrecursorPeakOnly
        {
            get { return cbMaxPrecursorOnly.Checked; }
            set { cbMaxPrecursorOnly.Checked = value; }
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

        public int CVCutoff
        {
            get { return Convert.ToInt32(textCVCutoff.Text); }
            set { textCVCutoff.Text = value.ToString(LocalizationHelper.CurrentCulture); }
        }

        public AreaCVTransitions Transition
        {
            get { return GetTransitionFromIdx(comboTransitions.SelectedIndex); }
            set { SetTransitionIdx(value); }
        }

        public int TransitionCount
        {
            get { return comboTransitions.SelectedIndex - 1; }
            set { comboTransitions.SelectedIndex = value + 1; }
        }

        public AreaCVMsLevel MSLevel
        {
            get
            {
                return AreCVMsLevelExtension.GetEnum(comboTransType.SelectedItem.ToString());
            }
            set { comboTransType.SelectedItem = value.ToString(); }
        }

        public double QValueCutoff
        {
            get
            {
                double result;
                return double.TryParse(textQVal.Text, out result) ? result : double.NaN;
            }
            set { textQVal.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public int MinimumDetections
        {
            get { return (int) numericUpDownDetections.Value; }
            set { numericUpDownDetections.Value = value; }
        }

        public AreaCVNormalizationMethod NormalizationMethod
        {
            get
            {
                var selected = comboNormalizeTo.SelectedItem.ToString();
                
                if (Equals(selected, Resources.RefineDlg_NormalizationMethod_None))
                    return AreaCVNormalizationMethod.none;
                else if (Equals(selected, Resources.RefineDlg_NormalizationMethod_Medians))
                    return AreaCVNormalizationMethod.medians;
                else if (Equals(selected, Resources.RefineDlg_NormalizationMethod_Global_standards))
                    return AreaCVNormalizationMethod.global_standards;
                else
                    return AreaCVNormalizationMethod.ratio;
            }
            set
            {
                if (!Equals(value, AreaCVNormalizationMethod.ratio))
                    if (value == AreaCVNormalizationMethod.global_standards)
                        comboNormalizeTo.SelectedItem = Resources.RefineDlg_NormalizationMethod_Global_standards;
                    else if (value == AreaCVNormalizationMethod.medians)
                        comboNormalizeTo.SelectedItem = Resources.RefineDlg_NormalizationMethod_Medians;
                    else
                        comboNormalizeTo.SelectedItem = Resources.RefineDlg_NormalizationMethod_None;
            }
        }

        public void SetTransitionIdx(AreaCVTransitions transitions)
        {
            if (transitions == AreaCVTransitions.all)
            {
                comboTransitions.SelectedIndex = 0;
            }
            else if (transitions == AreaCVTransitions.best)
            {
                comboTransitions.SelectedIndex = 1;
            }
        }

        public IsotopeLabelType CVRefineLabelType
        {
            get
            {
                if (comboNormalizeTo.Items.Count == 0) return null;
                string cvRefineTypeName = comboNormalizeTo.SelectedItem.ToString();
                if (string.IsNullOrEmpty(cvRefineTypeName) || Equals(cvRefineTypeName, Resources.RefineDlg_NormalizationMethod_None)
                    || Equals(cvRefineTypeName, Resources.RefineDlg_NormalizationMethod_Medians) || Equals(cvRefineTypeName, Resources.RefineDlg_NormalizationMethod_Global_standards))
                    return null;
                cvRefineTypeName = char.ToLowerInvariant(cvRefineTypeName[0]) + cvRefineTypeName.Substring(1);
                var typedMods = _settings.PeptideSettings.Modifications.GetModificationsByName(cvRefineTypeName);
                return typedMods.LabelType;
            }

            set { comboNormalizeTo.SelectedItem = value.Title; }
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

        public bool Log
        {
            get { return checkBoxLog.Checked; }
            set { checkBoxLog.Checked = value; }
        }

        public double AdjustedPValueCutoff
        {
            get { return Convert.ToDouble(textPValue.Text); }
            set { textPValue.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public double FoldChangeCutoff
        {
            get { return Convert.ToDouble(textFoldChange.Text); }
            set { textFoldChange.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            int? minPeptidesPerProtein = null;
            if (!string.IsNullOrEmpty(textMinPeptides.Text))
            {
                int minVal;
                if (!helper.ValidateNumberTextBox(textMinPeptides, 0, 10, out minVal))
                    return;
                minPeptidesPerProtein = minVal;
            }
            int? minTransitionsPerPrecursor = null;
            if (!string.IsNullOrEmpty(textMinTransitions.Text))
            {
                int minVal;
                if (!helper.ValidateNumberTextBox(textMinTransitions, 0, 100, out minVal))
                    return;
                minTransitionsPerPrecursor = minVal;
            }
            bool removeDuplicatePeptides = cbRemoveDuplicatePeptides.Checked;
            bool removeRepeatedPeptides = cbRemoveRepeatedPeptides.Checked;
            bool removeMissingLibrary = cbRemovePeptidesMissingLibrary.Checked;

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
                if (!helper.ValidateDecimalTextBox(textMinPeakFoundRatio, 0, 1, out minVal))
                    return;
                minPeakFoundRatio = minVal;
            }
            if (!string.IsNullOrEmpty(textMaxPeakFoundRatio.Text))
            {
                double maxVal;
                if (!helper.ValidateDecimalTextBox(textMaxPeakFoundRatio, 0, 1, out maxVal))
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
                if (!helper.ValidateNumberTextBox(textMaxPepPeakRank, 1, 20, out maxVal))
                    return;
                maxPepPeakRank = maxVal;
            }

            int? maxPeakRank = null;
            if (!string.IsNullOrEmpty(textMaxPeakRank.Text))
            {
                int maxVal;
                if (!helper.ValidateNumberTextBox(textMaxPeakRank, 1, 20, out maxVal))
                    return;
                maxPeakRank = maxVal;
            }

            bool removeMissingResults = radioRemoveMissing.Checked;

            double? rtRegressionThreshold = null;
            if (!string.IsNullOrEmpty(textRTRegressionThreshold.Text))
            {
                double minVal;
                if (!helper.ValidateDecimalTextBox(textRTRegressionThreshold, 0, 1, out minVal))
                    return;
                rtRegressionThreshold = minVal;
            }

            double? dotProductThreshold = null;
            if (!string.IsNullOrEmpty(textMinDotProduct.Text))
            {
                double minVal;
                if (!helper.ValidateDecimalTextBox(textMinDotProduct, 0, 1, out minVal))
                    return;
                dotProductThreshold = minVal;
            }

            double? idotProductThreshold = null;
            if (!string.IsNullOrEmpty(textMinIdotProduct.Text))
            {
                double minVal;
                if (!helper.ValidateDecimalTextBox(textMinIdotProduct, 0, 1, out minVal))
                    return;
                idotProductThreshold = minVal;
            }

            bool useBestResult = comboReplicateUse.SelectedIndex > 0;

            double? cvCutoff = null;
            if (!string.IsNullOrEmpty(textCVCutoff.Text))
            {
                double cutoffVal;
                if (!helper.ValidateDecimalTextBox(textCVCutoff, 0, null, out cutoffVal))
                    return;
                cvCutoff = cutoffVal;
            }

            double? qvalueCutoff = null;
            if (!string.IsNullOrWhiteSpace(textQVal.Text))
            {
                double qvalue;
                if (!helper.ValidateDecimalTextBox(textQVal, 0.0, 1.0, out qvalue))
                    return;
                qvalueCutoff = qvalue;
            }

            int? minimumDetections = null;
            if (numericUpDownDetections.Enabled)
            {
                minimumDetections = (int) numericUpDownDetections.Value;
            }

            var normIdx = comboNormalizeTo.SelectedIndex;
            var normMethod = GetNormalizationMethod(normIdx);

            IsotopeLabelType referenceType = CVRefineLabelType;

            var transitionsSelection = GetTransitionFromIdx(comboTransitions.SelectedIndex);
            int? numTransitions = null;
            if (transitionsSelection == AreaCVTransitions.count)
            {
                numTransitions = comboTransitions.SelectedIndex - 1;
            }

            var msLevel = AreaCVMsLevel.products;
            if (comboTransitions.Items.Count > 0)
            {
                var selectedMs = comboTransType.SelectedItem.ToString();
                msLevel = AreCVMsLevelExtension.GetEnum(selectedMs);
            }

            double? adjustedPValueCutoff = null;
            if (!string.IsNullOrEmpty(textPValue.Text))
            {
                double adjustedPval;
                if (!helper.ValidateDecimalTextBox(textPValue, 0.0, checkBoxLog.Checked ? (double?) null : 1.0, out adjustedPval, checkBoxLog.Checked))
                    return;
                adjustedPValueCutoff = checkBoxLog.Checked ? Math.Pow(10, -adjustedPval) : adjustedPval;
            }

            double? foldChangeCutoff = null;
            if (!string.IsNullOrEmpty(textFoldChange.Text))
            {
                double foldChange;
                if (!helper.ValidateDecimalTextBox(textFoldChange, checkBoxLog.Checked ? (double?) null : 0.0, null, out foldChange, false))
                    return;
                foldChangeCutoff = Math.Abs(checkBoxLog.Checked ? foldChange : Math.Log(foldChange, 2));
            }

            var groupComparisonDefs = new List<GroupComparisonDef>();
            if (_groupComparisonsListBoxDriver.Chosen.Length > 0)
            {
                groupComparisonDefs = _groupComparisonsListBoxDriver.Chosen.ToList();
            }

            int? msLevelGroupComparison = null;
            if (adjustedPValueCutoff.HasValue || foldChangeCutoff.HasValue)
            {
                msLevelGroupComparison = int.Parse(comboMSGroupComparisons.SelectedItem.ToString());
            }

            RefinementSettings = new RefinementSettings
                                     {
                                         MinPeptidesPerProtein = minPeptidesPerProtein,
                                         RemoveRepeatedPeptides = removeRepeatedPeptides,
                                         RemoveDuplicatePeptides = removeDuplicatePeptides,
                                         RemoveMissingLibrary = removeMissingLibrary,
                                         MinTransitionsPepPrecursor = minTransitionsPerPrecursor,
                                         RefineLabelType = refineLabelType,
                                         AddLabelType = addLabelType,
                                         MinPeakFoundRatio = minPeakFoundRatio,
                                         MaxPeakFoundRatio = maxPeakFoundRatio,
                                         MaxPepPeakRank = maxPepPeakRank,
                                         MaxPrecursorPeakOnly = cbMaxPrecursorOnly.Checked,
                                         MaxPeakRank = maxPeakRank,
                                         PreferLargeIons = cbPreferLarger.Checked,
                                         RemoveMissingResults = removeMissingResults,
                                         RTRegressionThreshold = rtRegressionThreshold,
                                         DotProductThreshold = dotProductThreshold,
                                         IdotProductThreshold = idotProductThreshold,
                                         UseBestResult = useBestResult,
                                         AutoPickChildrenAll = (cbAutoPeptides.Checked ? PickLevel.peptides : 0) |
                                                               (cbAutoPrecursors.Checked ? PickLevel.precursors : 0) |
                                                               (cbAutoTransitions.Checked ? PickLevel.transitions : 0),
                                         CVCutoff = cvCutoff,
                                         QValueCutoff = qvalueCutoff,
                                         MinimumDetections =  minimumDetections,
                                         NormalizationMethod = normMethod,
                                         NormalizationLabelType = referenceType,
                                         Transitions = transitionsSelection,
                                         CountTransitions = numTransitions,
                                         MSLevel = msLevel,
                                         AdjustedPValueCutoff = adjustedPValueCutoff,
                                         FoldChangeCutoff = foldChangeCutoff,
                                         MSLevelGroupComparison = msLevelGroupComparison,
                                         GroupComparisonDefs = groupComparisonDefs
                                     };

            DialogResult = DialogResult.OK;
            Close();
        }

        private AreaCVTransitions GetTransitionFromIdx(int idx)
        {
            if (idx == comboTransitions.Items.Count - 1)
                return AreaCVTransitions.all;
            if (idx > 2)
                return AreaCVTransitions.count;

            var transition = AreaCVTransitions.all;
            switch (idx)
            {
                case 0:
                    transition = AreaCVTransitions.all;
                    break;
                case 1:
                    transition = AreaCVTransitions.best;
                    break;
                case 2:
                    transition = AreaCVTransitions.best;
                    break;
            }

            return transition;
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
                                      : _removeLabelText;
            helpTip.SetToolTip(comboRefineLabelType, cbAdd.Checked
                    ? Resources.RefineDlg_cbAdd_CheckedChanged_Precursors_of_the_chosen_isotope_label_type_will_be_added_if_they_are_missing
                    : _removeTipText);
        }

        private void textMaxPeakRank_TextChanged(object sender, EventArgs e)
        {
            cbPreferLarger.Enabled = !string.IsNullOrEmpty(textMaxPeakRank.Text);
            if (!cbPreferLarger.Enabled)
                cbPreferLarger.Checked = false;
        }

        #region Functional Test Support

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

        public RefinementSettings FormSettings
        {
            get { return RefinementSettings; }
        }

        private void btnEditGroupComparisons_Click(object sender, EventArgs e)
        {
            EditGroupComparisonList();
        }

        public void EditGroupComparisonList()
        {
            _groupComparisonsListBoxDriver.EditList(DocumentContainer);
        }

        private void checkBoxLog_CheckedChanged(object sender, EventArgs e)
        {
            var log = checkBoxLog.Checked;
            VolcanoPlotPropertiesDlg.UpdateTextBoxAndLabel(textFoldChange, labelFoldChangeUnit, log, 2);
            VolcanoPlotPropertiesDlg.UpdateTextBoxAndLabel(textPValue, labelPValueUnit, log, 10, true);
        }
    }
}
