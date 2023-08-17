﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class EditGroupComparisonDlg : FormEx
    {
        private readonly GroupComparisonDef _originalGroupComparisonDef;
        private readonly IEnumerable<GroupComparisonDef> _existingGroupComparisons;
        protected bool _inChangeSettings;
        private readonly bool _pushChangesToDocument;

        public EditGroupComparisonDlg(IDocumentUIContainer documentContainer,
            GroupComparisonDef groupComparisonDef, IEnumerable<GroupComparisonDef> existingGroupComparisons)
            : this(new GroupComparisonModel(documentContainer, null) { GroupComparisonDef = groupComparisonDef})
        {
            _originalGroupComparisonDef = groupComparisonDef;
            _existingGroupComparisons = existingGroupComparisons;
            if (documentContainer == null)
            {
                btnPreview.Visible = false;
            }
            _pushChangesToDocument = false;
            tbxName.Text = groupComparisonDef.Name ?? string.Empty;
            comboMsLevel.Items.Clear();
            comboMsLevel.Items.AddRange(MsLevelOption.AllOptions.ToArray());
        }

        public EditGroupComparisonDlg(FoldChangeBindingSource foldChangeBindingSource)
            : this(foldChangeBindingSource.GroupComparisonModel)
        {
            // ReSharper disable VirtualMemberCallInConstructor
            Text = TextUtil.ColonSeparate(Text, foldChangeBindingSource.GroupComparisonModel.GroupComparisonName);
            // ReSharper restore VirtualMemberCallInConstructor
            panelName.Visible = false;
            panelButtons.Visible = false;
            Height -= panelName.Height + panelButtons.Height;
            _pushChangesToDocument = true;
        }

        public EditGroupComparisonDlg(GroupComparisonModel groupComparisonModel)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            GroupComparisonModel = groupComparisonModel;
            GroupComparisonModel.AddModelChanged(this, OnModelChanged);
            Height -= panelAdvanced.Height;
            panelAdvanced.Visible = false;
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            ShowPreview();
        }

        public void ShowPreview()
        {
            var foldChangeGrid = OwnedForms.OfType<FoldChangeGrid>().FirstOrDefault();
            if (null != foldChangeGrid)
            {
                foldChangeGrid.Activate();
                return;
            }
            foldChangeGrid = new FoldChangeGrid();
            foldChangeGrid.SetBindingSource(new FoldChangeBindingSource(GroupComparisonModel));
            foldChangeGrid.Show(this);
        }

        protected void UpdateSettings()
        {
            var groupComparisonDef = GroupComparisonModel.GroupComparisonDef;
            ReplaceComboItems(comboControlAnnotation, ListReplicateAnnotations(), groupComparisonDef.ControlAnnotation);
            string[] controlValues = ListControlValues();
            ReplaceComboItems(comboControlValue, controlValues, groupComparisonDef.ControlValue ?? string.Empty);
            var caseValues = new HashSet<string>(controlValues) { string.Empty };
            if (null != groupComparisonDef.ControlValue)
            {
                caseValues.Remove(groupComparisonDef.ControlValue);
            }
            var sortedCaseValues = caseValues.ToArray();
            Array.Sort(sortedCaseValues);
            ReplaceComboItems(comboCaseValue, sortedCaseValues, groupComparisonDef.CaseValue ?? string.Empty);
            ReplaceComboItems(comboIdentityAnnotation, new[] { string.Empty }.Concat(ListReplicateAnnotations()),
                groupComparisonDef.IdentityAnnotation);
            ReplaceComboItems(comboNormalizationMethod, ListNormalizeOptions(), MakeNormalizationItem(groupComparisonDef.NormalizationMethod));
            ReplaceComboItems(comboSummaryMethod, SummarizationMethod.ListSummarizationMethods(), groupComparisonDef.SummarizationMethod);
            tbxConfidenceLevel.Text = groupComparisonDef.ConfidenceLevelTimes100.ToString(CultureInfo.CurrentCulture);
            ReplaceComboItems(comboMsLevel, MsLevelOption.AllOptions, groupComparisonDef.MsLevel);
            radioScopeProtein.Checked = groupComparisonDef.PerProtein;
            radioScopePeptide.Checked = !groupComparisonDef.PerProtein;
            cbxUseZeroForMissingPeaks.Checked = groupComparisonDef.UseZeroForMissingPeaks;
            cbxUseZeroForMissingPeaks.Visible = GroupComparisonModel.Document.Settings.PeptideSettings.Integration.PeakScoringModel != null;
            if (GroupComparisonDef.QValueCutoff.HasValue)
            {
                tbxQValueCutoff.Text = groupComparisonDef.QValueCutoff.Value.ToString(CultureInfo.CurrentCulture);
                groupBoxQValueCutoff.Visible = true;
            }
            else
            {
                tbxQValueCutoff.Text = string.Empty;
            }
            if (RequiresAdvanced(groupComparisonDef))
            {
                ShowAdvanced(true);
            }
        }

        private IEnumerable<NormalizationItem> ListNormalizeOptions()
        {
            var options = NormalizationMethod.ListNormalizationMethods(GroupComparisonModel.Document)
                .Select(NormalizeOption.FromNormalizationMethod);
            if (null != GroupComparisonModel.Document.Settings.PeptideSettings.Quantification.RegressionFit 
                && CalibrationCurveFitter.AnyBatchNames(GroupComparisonModel.Document.Settings))
            {
                options = options.Prepend(NormalizeOption.CALIBRATED);
            }

            options = options.Prepend(NormalizeOption.DEFAULT);
            return options.Select(MakeNormalizationItem);
        }

        private bool RequiresAdvanced(GroupComparisonDef groupComparisonDef)
        {
            return groupComparisonDef.QValueCutoff.HasValue ||
                   groupComparisonDef.SummarizationMethod != SummarizationMethod.AVERAGING ||
                   groupComparisonDef.UseZeroForMissingPeaks;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void radioScope_CheckedChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            GroupComparisonDef = GroupComparisonDef.ChangePerProtein(radioScopeProtein.Checked);
        }

        public string GroupComparisonName
        {
            get { return tbxName.Text; }
        }

        public NormalizeOption NormalizeOption
        {
            get
            {
                return (comboNormalizationMethod.SelectedItem as NormalizationItem)?.NormalizeOption ??
                       NormalizeOption.NONE;
            }
            set
            {
                comboNormalizationMethod.SelectedItem = MakeNormalizationItem(value);
            }
        }
        public ComboBox ComboIdentityAnnotation
        {
            get { return comboIdentityAnnotation; }
        }

        public ComboBox ComboControlValue
        {
            get { return comboControlValue; }
        }

        public ComboBox ComboControlAnnotation
        {
            get { return comboControlAnnotation; }
        }

        public ComboBox ComboCaseValue
        {
            get { return comboCaseValue; }
        }

        public ComboBox ComboSummaryMethod
        {
            get { return comboSummaryMethod; }
        }

        public TextBox TextBoxConfidenceLevel
        {
            get { return tbxConfidenceLevel; }
        }

        public TextBox TextBoxQValueCutoff
        {
            get { return tbxQValueCutoff; }
        }

        public RadioButton RadioScopePerProtein { get { return radioScopeProtein; } }
        public RadioButton RadioScopePerPeptide { get { return radioScopePeptide; } }

        public TextBox TextBoxName { get { return tbxName; } }

        public static void ChangeGroupComparisonDef(bool pushChangesToDocument, GroupComparisonModel model, GroupComparisonDef groupDef)
        {
            if (pushChangesToDocument)
            {
                Program.MainWindow.ModifyDocument(
                    GroupComparisonStrings.GroupComparisonSettingsForm_GroupComparisonDef_Change_Group_Comparison,
                    doc => model.ApplyChangesToDocument(doc, groupDef),
                    AuditLogEntry.SettingsLogFunction
                );
                
                Settings.Default.GroupComparisonDefList.Add(groupDef);
            }
            else
            {
                model.GroupComparisonDef = groupDef;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public GroupComparisonDef GroupComparisonDef
        {
            get
            {
                GroupComparisonDef groupComparisonDef = GroupComparisonModel.GroupComparisonDef;
                if (!_pushChangesToDocument)
                {
                    groupComparisonDef = groupComparisonDef.ChangeName(tbxName.Text);
                }
                return groupComparisonDef;
            }
            set
            {
                var def = value;
                if(!_pushChangesToDocument)
                    def = value.ChangeName(tbxName.Text);
                ChangeGroupComparisonDef(_pushChangesToDocument, GroupComparisonModel, def);

                if (def.PerProtein && Equals(NormalizeOption.CALIBRATED, def.NormalizationMethod))
                {
                    radioScopeProtein.ForeColor = Color.Red;
                    toolTip1.SetToolTip(radioScopeProtein, GroupComparisonStrings
                        .EditGroupComparisonDlg_GroupComparisonDef_Group_comparisons_at_the_protein_level_using_calibrated_values_is_not_yet_supported_in_Skyline_);
                }
                else
                {
                    radioScopeProtein.ForeColor = radioScopePeptide.ForeColor;
                    toolTip1.SetToolTip(radioScopeProtein, null);
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public GroupComparisonModel GroupComparisonModel { get; private set; }

        public void OkDialog()
        {
            MessageBoxHelper helper = new MessageBoxHelper(this);
            string name;
            if (!helper.ValidateNameTextBox(tbxName, out name))
            {
                return;
            }
            if (name != _originalGroupComparisonDef.Name &&
                _existingGroupComparisons.Any(comparison => comparison.Name == name))
            {
                helper.ShowTextBoxError(tbxName, GroupComparisonStrings.EditGroupComparisonDlg_btnOK_Click_There_is_already_a_group_comparison_named__0__, name);
                return;
            }

            if (!helper.ValidateDecimalTextBox(tbxConfidenceLevel, 0, 100, out _))
            {
                return;
            }
            DialogResult = DialogResult.OK;
        }

        private void OnModelChanged(GroupComparisonModel model)
        {
            if (_inChangeSettings)
            {
                return;
            }
            try
            {
                _inChangeSettings = true;
                UpdateSettings();
            }
            finally
            {
                _inChangeSettings = false;
            }
        }

        protected void comboControlAnnotation_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            GroupComparisonDef = GroupComparisonDef.ChangeControlAnnotation(
                comboControlAnnotation.SelectedItem as string);
        }

        protected void comboControlValue_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            GroupComparisonDef = GroupComparisonDef.ChangeControlValue(comboControlValue.SelectedItem as string);
        }

        protected void comboNormalizationMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            GroupComparisonDef = GroupComparisonDef.ChangeNormalizeOption(NormalizeOption);
        }

        protected void comboCaseValue_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            string caseValue = comboCaseValue.SelectedItem as string;
            if (string.IsNullOrEmpty(caseValue))
            {
                caseValue = null;
            }
            GroupComparisonDef = GroupComparisonDef.ChangeCaseValue(caseValue);
        }

        protected void comboIdentityAnnotation_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            string identityAnnotation = comboIdentityAnnotation.SelectedItem as string;
            if (string.IsNullOrEmpty(identityAnnotation))
            {
                GroupComparisonDef = GroupComparisonDef
                    .ChangeIdentityAnnotation(null)
                    .ChangeAverageTechnicalReplicates(false);
            }
            else
            {
                GroupComparisonDef = GroupComparisonDef
                    .ChangeIdentityAnnotation(identityAnnotation)
                    .ChangeAverageTechnicalReplicates(true);
            }
        }

        protected void tbxConfidenceLevel_TextChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            double confidenceLevel;
            MessageBoxHelper helper = new MessageBoxHelper(this, false);

            if (!helper.ValidateDecimalTextBox(tbxConfidenceLevel, 0, 100, out confidenceLevel))
            {
                return;
            }
            GroupComparisonDef = GroupComparisonDef.ChangeConfidenceLevelTimes100(confidenceLevel);
        }


        private void tbxQValueCutoff_TextChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            if (string.IsNullOrEmpty(tbxQValueCutoff.Text.Trim()))
            {
                GroupComparisonDef = GroupComparisonDef.ChangeQValueCutoff(null);
            }
            else
            {
                MessageBoxHelper helper = new MessageBoxHelper(this, false);
                double qValueCutoff;
                if (!helper.ValidateDecimalTextBox(tbxQValueCutoff, 0, 1, out qValueCutoff))
                {
                    return;
                }
                GroupComparisonDef = GroupComparisonDef.ChangeQValueCutoff(qValueCutoff);
            }
        }


        protected void cbxTreatMissingAsZero_CheckedChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            GroupComparisonDef = GroupComparisonDef.ChangeUseZeroForMissingPeaks(((CheckBox) sender).Checked);
        }

        protected IEnumerable<string> ListReplicateAnnotations()
        {
            return GroupComparisonModel.Document.Settings.DataSettings.AnnotationDefs
                .Where(def => def.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate))
                .Select(def => def.Name);
        }

        protected string[] ListControlValues()
        {
            var newSettings = GroupComparisonModel.Document.Settings;
            var annotationDef = newSettings.DataSettings.AnnotationDefs.FirstOrDefault(
                def => def.Name == GroupComparisonDef.ControlAnnotation);
            if (null != annotationDef && newSettings.HasResults)
            {
                string[] controlValues = newSettings.MeasuredResults.Chromatograms.Select(
                    chromatogram => chromatogram.Annotations.GetAnnotation(annotationDef.Name) ?? string.Empty)
                    .Distinct()
                    .ToArray();
                Array.Sort(controlValues);
                return controlValues;
            }
            return new string[0];
        }

        protected void ReplaceComboItems<T>(ComboBox comboBox, IEnumerable<T> items, T selectedItem)
        {
            var itemObjects = items.Cast<object>().ToArray();
            int newSelectedIndex = -1;
            for (int i = 0; i < itemObjects.Length; i++)
            {
                if (Equals(selectedItem, itemObjects[i]))
                {
                    newSelectedIndex = i;
                    break;
                }
            }
            if (newSelectedIndex == comboBox.SelectedIndex && ArrayUtil.EqualsDeep(itemObjects, comboBox.Items.Cast<object>().ToArray()))
            {
                return;
            }
            comboBox.Items.Clear();
            comboBox.Items.AddRange(itemObjects);
            comboBox.SelectedIndex = newSelectedIndex;
        }

        private void comboSummaryMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            GroupComparisonDef = GroupComparisonDef.ChangeSummarizationMethod(
                comboSummaryMethod.SelectedItem as SummarizationMethod ?? SummarizationMethod.DEFAULT);
        }

        public void ShowAdvanced(bool show)
        {
            if (panelAdvanced.Visible == show)
            {
                return;
            }
            if (show)
            {
                Height += panelAdvanced.Height;
                panelAdvanced.Visible = true;
                btnAdvanced.Visible = false;
            }
            else
            {
                Height -= panelAdvanced.Height;
                panelAdvanced.Visible = false;
                panelAdvanced.Visible = true;
            }
        }

        private void btnAdvanced_Click(object sender, EventArgs e)
        {
            ShowAdvanced(true);
        }

        private void comboMsLevel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }

            var newMsLevelOption = comboMsLevel.SelectedItem as MsLevelOption;
            if (newMsLevelOption != null)
            {
                GroupComparisonDef = GroupComparisonDef.ChangeMsLevel(newMsLevelOption);
            }
        }

        public NormalizationItem MakeNormalizationItem(NormalizeOption normalizeOption)
        {
            return new NormalizationItem(normalizeOption, GetNormalizeOptionText(normalizeOption));
        }

        /// <summary>
        /// Returns the text to display in the combo box for the specified NormalizeOption.
        /// </summary>
        public string GetNormalizeOptionText(NormalizeOption normalizeOption)
        {
            var settings = GroupComparisonModel.Document.Settings;
            if (normalizeOption == NormalizeOption.DEFAULT)
            {
                // If the option is "default" then append in parentheses the normalization method from the Quantification Settings.
                return string.Format(QuantificationStrings.SkylineWindow_MakeNormalizeToMenuItem_Default___0__,
                    settings.PeptideSettings.Quantification.NormalizationMethod);
            }

            if (normalizeOption == NormalizeOption.CALIBRATED)
            {
                // If any replicates have a Batch Name set, then call this normalization technique "Batch Calibration Curve"
                if (CalibrationCurveFitter.AnyBatchNames(settings))
                {
                    return GroupComparisonStrings.EditGroupComparisonDlg_GetNormalizeOptionText_Batch_Calibration_Curve;
                }
                // If there are no batch names on any replicates, then this normalization method ends up being the same as "Default".
                // It will not be offered as an option this case, but it might appear in the dropdown if that's the option that was already chosen.
                return QuantificationStrings.Calibration_Curve;
            }

            return normalizeOption.ToString();
        }

        public class NormalizationItem
        {
            public NormalizationItem(NormalizeOption normalizeOption, string text)
            {
                NormalizeOption = normalizeOption;
                Text = text;
            }

            public NormalizeOption NormalizeOption {
                get;
            }
            public string Text { get; }
            public override string ToString()
            {
                return Text;
            }

            protected bool Equals(NormalizationItem other)
            {
                return NormalizeOption.Equals(other.NormalizeOption);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((NormalizationItem)obj);
            }

            public override int GetHashCode()
            {
                return NormalizeOption.GetHashCode();
            }
        }
    }
}
