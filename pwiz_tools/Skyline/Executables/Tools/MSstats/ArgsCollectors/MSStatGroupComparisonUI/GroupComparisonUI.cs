/*
 * Original author: Trevor Killeen <killeent .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace MSStatArgsCollector
{    
    public partial class GroupComparisonUi : Form
    {
        // Groups for comparison
        private string[] ControlGroupList { get; set; }
        private string[] ComparisonGroupList { get; set; }

        // Argument array
        public string[] Arguments { get; private set; }
        
        public GroupComparisonUi(string[] groups, string[] oldArgs)
        {
            InitializeComponent();

            comboBoxNormalizeTo.SelectedIndex = 1;
            Array.Sort(groups);
            ControlGroup.DataSource = ControlGroupList = groups;
            Arguments = oldArgs;

            FormatComparisonGroupDisplay();
            try
            {
                RestoreSettings();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Exception restoring settings {0}", ex);
            }
        }

        private void MSstatsUI_Load(object sender, EventArgs e)
        {
            Show();
            textBoxName.Focus();
        }

        private void FormatComparisonGroupDisplay()
        {
            // Display comparison group checklistbox if there are 3+ groups
            if (ControlGroupList.Length > 2)
            {
                // Listbox appropriately sizes itself to fit the number of groups to compare against without scrolling,
                // for 2-5 groups (3-6 total groups in the data set). In the case of 6+ groups, there will be scrolling
                ComparisonGroups.Height = ComparisonGroups.ItemHeight * Math.Min(5, ControlGroupList.Length - 1) + 5;
                ComparisonGroups.Visible = labelComparisonGroups.Visible = true;
            }
        }

        // Constants
        private const string TRUESTRING = "TRUE"; // Not L10N
        private const string FALSESTRING = "FALSE"; // Not L10N

        // ReSharper disable InconsistentNaming
        private enum Args { name, normalize_to, allow_missing_peaks, feature_selection, remove_interfered_proteins, fixed_argument_count }
        // ReSharper restore InconsistentNaming


        // If there is no stored argument string, or if the number of groups has changed, the UI loads the
        // default settings for group comparisons
        private void RestoreSettings()
        {
            const int fixedArgumentCount = (int) Args.fixed_argument_count;

            // Restore view only if there are the same number of groups as before 
            if (Arguments != null && (Arguments.Length == ControlGroupList.Length + fixedArgumentCount))
            {
                var variableArguments = Arguments.Skip(fixedArgumentCount).ToArray();
                var fixedArguments = Arguments.Take(fixedArgumentCount).ToArray();
                // Restore the selected control group 
                ControlGroup.SelectedIndex = Array.IndexOf(variableArguments, "-1"); // Not L10N

                // Restore the selection of comparison groups (if necessary)
                if (ControlGroupList.Length > 2)
                {
                    for (int i = 0; i < variableArguments.Length; i++)
                    {
                        double groupConstant;
                        if (!double.TryParse(variableArguments[i], NumberStyles.Float, CultureInfo.InvariantCulture, out groupConstant))
                            continue;
// ReSharper disable CompareOfFloatsByEqualityOperator
                        if (groupConstant != 1.0 && groupConstant != 0.0)
// ReSharper restore CompareOfFloatsByEqualityOperator
                        {
                            // if the group being selected comes after the control group lexicographically, we must
                            // decrement the index by 1 
                            ComparisonGroups.SelectedIndices.Add(i - (i < ControlGroup.SelectedIndex ? 0 : 1));
                        }
                    }
                }

                // Restore name
                textBoxName.Text = fixedArguments[(int) Args.name];
                comboBoxNormalizeTo.SelectedIndex = int.Parse(fixedArguments[(int)Args.normalize_to], CultureInfo.InvariantCulture);
                cboxAllowMissingPeaks.Checked = TRUESTRING == fixedArguments[(int) Args.allow_missing_peaks];
                cboxSelectHighQualityFeatures.Checked = TRUESTRING == fixedArguments[(int) Args.feature_selection];
                cboxRemoveInterferedProteins.Checked = TRUESTRING == fixedArguments[(int) Args.remove_interfered_proteins];
            }
            else
            {
                // If any of the groups begin with "control" or "healthy" make the first the default
                // control group.
                for (int i = 0; i < ControlGroupList.Length; i++)
                {
                    string group = ControlGroupList[i].ToLower();
                    if (group.StartsWith("control") || group.StartsWith("healthy")) // Not L10N
                    {
                        ControlGroup.SelectedIndex = i;
                        break;
                    }
                }
            }            
        }

        private void ControlGroup_SelectedIndexChanged(object sender, EventArgs e)
        {        
            if (ControlGroupList.Length > 2) 
                PopulateComparisonGroups();
        }

        // Simply populates the checklistbox with all but the selected group
        private void PopulateComparisonGroups()
        {
            var comparisonGroups = new Collection<string>();
            foreach (string group in ControlGroupList)
            {
                if (!group.Equals(ControlGroup.SelectedItem.ToString()))
                    comparisonGroups.Add(group);
            }
            
            ComparisonGroups.DataSource = ComparisonGroupList = comparisonGroups.ToArray();
            ComparisonGroups.ClearSelected();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (ComparisonGroups.Visible && ComparisonGroups.SelectedIndices.Count == 0)
            {
                MessageBox.Show(this, MSstatsResources.GroupComparisonUi_OkDialog_Please_select_at_least_one_comparison_group_);
            }
            else if (string.IsNullOrWhiteSpace(textBoxName.Text))
            {
                MessageBox.Show(this, MSstatsResources.GroupComparisonUi_OkDialog_Please_enter_a_name_for_this_comparison_);
            }
            else
            {
                GenerateArguments();
                DialogResult = DialogResult.OK;
            }
        }


        /// <summary>
        /// The first arguments that get passed to the R script are the ones specified in the enum <see cref="Args"/>.
        /// The next n elements is a series of n doubles that represent the constants that will be applied 
        /// to each group, where n is the (2+) total number of groups in the data source. There will
        /// be a single "-1" in this series, which represents the constant applied to the
        /// control group. If there is only one other group for the control to be compared against, it will
        /// have a value of 1, while all other groups will have constants of 0. An example
        /// subarray that might be generated would be: [1 , 0 , -1 , 0]
        ///
        /// In the case that there are k>1 groups to be compared against, each group that the control will
        /// be compared against adopts a value of 1.0/k, while any groups not being compared against the control
        /// again adopt a constant of 0. An example subarray that might be generated would be: [0 , 0 , 0.5 , -1 , 0 , 0.5] 
        /// where k = 2
        /// </summary>
        private void GenerateArguments()
        {
            ICollection<string> commandLineArguments = new Collection<string>();

            // Add fixed arguments
            commandLineArguments.Add(textBoxName.Text);
            commandLineArguments.Add(comboBoxNormalizeTo.SelectedIndex.ToString(CultureInfo.InvariantCulture));
            commandLineArguments.Add(cboxAllowMissingPeaks.Checked ? TRUESTRING : FALSESTRING);
            commandLineArguments.Add(cboxSelectHighQualityFeatures.Checked ? TRUESTRING : FALSESTRING);
            commandLineArguments.Add(cboxRemoveInterferedProteins.Checked ? TRUESTRING : FALSESTRING);
            
            // Generate constants for comparisons
            var constants = new double[ControlGroupList.Length];
            constants[ControlGroup.SelectedIndex] = -1.0;
            if (ControlGroupList.Length == 2)
            {
                constants[1 - ControlGroup.SelectedIndex] = 1.0;
            }
            else
            {
                double comparisonConstant = 1.0 / ComparisonGroups.SelectedItems.Count;
                foreach (string group in ComparisonGroups.SelectedItems)
                {
                    constants[Array.IndexOf(ControlGroupList, group)] = comparisonConstant;
                }
            }
            
            // Add to the string
            foreach (double value in constants)
            {
                commandLineArguments.Add(value.ToString(CultureInfo.InvariantCulture));
            }

            Arguments = commandLineArguments.ToArray();
        }

        private void cboxSelectHighQualityFeatures_CheckedChanged(object sender, EventArgs e)
        {
            cboxRemoveInterferedProteins.Enabled = cboxSelectHighQualityFeatures.Checked;
        }
    }

    public class MSstatsGroupComparisonCollector
    {
        public static string[] CollectArgs(IWin32Window parent, string report, string[] oldArgs)
        {
            const string conditionColumnName = "Condition"; // Not L10N
            // Split report (.csv file) by lines
            string[] lines = report.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            string[] fields = lines[0].ParseCsvFields();
            int groupIndex = Array.IndexOf(fields, conditionColumnName);
            if (groupIndex < 0)
            {
                MessageBox.Show(parent, 
                    string.Format(MSstatsResources.MSstatsGroupComparisonCollector_CollectArgs_Unable_to_find_a_column_named___0__,
                    conditionColumnName));
                return null;
            }

            ICollection<string> groups = new HashSet<string>();
            // The last line in the CSV file is empty, thus we compare length - 1 
            for (int i = 1; i < lines.Length - 1; i++)
            {
                try
                {
                    groups.Add(lines[i].ParseCsvFields()[groupIndex]);
                }
                catch
                {
                    // ignore
                }
            }

            using (var dlg = new GroupComparisonUi(groups.ToArray(), oldArgs))
            {
                var result = parent != null ? dlg.ShowDialog(parent) : dlg.ShowDialog();
                if (result != DialogResult.OK)
                    return null;
                return dlg.Arguments;
            }
        }
    }
}
