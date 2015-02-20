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
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class GroupComparisonSettingsForm : BaseGroupComparisonSettings
    {
        public GroupComparisonSettingsForm(FoldChangeBindingSource foldChangeBindingSource) : base (foldChangeBindingSource.GroupComparisonModel)
        {
            InitializeComponent();
            Text = string.Format(Text, foldChangeBindingSource.GroupComparisonModel.GroupComparisonName);
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
                return base.GroupComparisonDef;
            }
            set
            {
                Program.MainWindow.ModifyDocument(GroupComparisonStrings.GroupComparisonSettingsForm_GroupComparisonDef_Change_Group_Comparison,
                    doc =>
                    {
                        var groupComparisons = doc.Settings.DataSettings.GroupComparisonDefs.ToList();
                        int index =
                            groupComparisons.FindIndex(def => def.Name == GroupComparisonModel.GroupComparisonName);
                        if (index < 0)
                        {
                            groupComparisons.Add(value);
                        }
                        else
                        {
                            groupComparisons[index] = value;
                        }
                        doc =
                            doc.ChangeSettings(
                                doc.Settings.ChangeDataSettings(
                                    doc.Settings.DataSettings.ChangeGroupComparisonDefs(groupComparisons)));
                        return doc;
                    }
                    );
                Settings.Default.GroupComparisonDefList.Add(value);
            }
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

        private void radioScope_CheckedChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            GroupComparisonDef = GroupComparisonDef.ChangePerProtein(radioScopeProtein.Checked);
        }

        public override ComboBox ComboControlAnnotation { get { return comboControlAnnotation; } }
        public override ComboBox ComboControlValue { get { return comboControlValue; } }
        public override ComboBox ComboNormalizationMethod { get { return comboNormalizationMethod; } }
        public override ComboBox ComboCaseValue { get { return comboCaseValue; } }
        public override ComboBox ComboIdentityAnnotation { get { return comboIdentityAnnotation; } }
        public override TextBox TextBoxConfidenceLevel { get { return tbxConfidenceLevel; } }
        public override RadioButton RadioScopePerProtein { get { return radioScopeProtein; } }
        public override RadioButton RadioScopePerPeptide { get { return radioScopePeptide; } }

    }
}
