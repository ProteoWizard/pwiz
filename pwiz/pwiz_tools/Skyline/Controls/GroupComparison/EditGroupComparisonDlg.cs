/*
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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class EditGroupComparisonDlg : BaseGroupComparisonSettings
    {
        private GroupComparisonDef _originalGroupComparisonDef;
        private IEnumerable<GroupComparisonDef> _existingGroupComparisons;
        public EditGroupComparisonDlg(IDocumentContainer documentContainer,
            GroupComparisonDef groupComparisonDef, IEnumerable<GroupComparisonDef> existingGroupComparisons)
            : base(new GroupComparisonModel(documentContainer, null) { GroupComparisonDef = groupComparisonDef})
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            _originalGroupComparisonDef = groupComparisonDef;
            _existingGroupComparisons = existingGroupComparisons;
            if (documentContainer == null)
            {
                btnPreview.Visible = false;
            }
            tbxName.Text = groupComparisonDef.Name ?? string.Empty;
            tbxConfidenceLevel.TextChanged += tbxConfidenceLevel_TextChanged;
            comboControlAnnotation.SelectedIndexChanged += comboControlAnnotation_SelectedIndexChanged;
            comboCaseValue.SelectedIndexChanged += comboCaseValue_SelectedIndexChanged;
            comboControlValue.SelectedIndexChanged += comboControlValue_SelectedIndexChanged;
            comboIdentityAnnotation.SelectedIndexChanged += comboIdentityAnnotation_SelectedIndexChanged;
            comboNormalizationMethod.SelectedIndexChanged += comboNormalizationMethod_SelectedIndexChanged;
            radioScopeProtein.CheckedChanged += radioScope_CheckedChanged;
            radioScopePeptide.CheckedChanged += radioScope_CheckedChanged;
        }

        public override GroupComparisonDef GroupComparisonDef
        {
            get
            {
                return GroupComparisonModel.GroupComparisonDef.ChangeName(tbxName.Text);
            }
            set
            {
                GroupComparisonModel.GroupComparisonDef = value;
            }
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

        protected override void UpdateSettings()
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
            ReplaceComboItems(comboNormalizationMethod, NormalizationMethod.ListNormalizationMethods(GroupComparisonModel.Document), groupComparisonDef.NormalizationMethod);
            tbxConfidenceLevel.Text = groupComparisonDef.ConfidenceLevelTimes100.ToString(CultureInfo.CurrentCulture);
            radioScopeProtein.Checked = groupComparisonDef.PerProtein;
            radioScopePeptide.Checked = !groupComparisonDef.PerProtein;
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

        public override ComboBox ComboNormalizationMethod
        {
            get { return comboNormalizationMethod; }
        }

        public override ComboBox ComboIdentityAnnotation
        {
            get { return comboIdentityAnnotation; }
        }

        public override ComboBox ComboControlValue
        {
            get { return comboControlValue; }
        }

        public override ComboBox ComboControlAnnotation
        {
            get { return comboControlAnnotation; }
        }

        public override ComboBox ComboCaseValue
        {
            get { return comboCaseValue; }
        }

        public override TextBox TextBoxConfidenceLevel
        {
            get { return tbxConfidenceLevel; }
        }
        public override RadioButton RadioScopePerProtein { get { return radioScopeProtein; } }
        public override RadioButton RadioScopePerPeptide { get { return radioScopePeptide; } }

        public TextBox TextBoxName { get { return tbxName; } }

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
            double confidenceLevel;
            if (!helper.ValidateDecimalTextBox(tbxConfidenceLevel, 0, 100, out confidenceLevel))
            {
                return;
            }
            DialogResult = DialogResult.OK;
        }
    }
}
