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

            Array.Sort(groups);
            ControlGroup.DataSource = ControlGroupList = groups;
            Arguments = oldArgs;

            FormatComparisonGroupDisplay();
            RestoreSettings();
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
        private const string EXPANDED = "E";
        private const string RESTRICTED = "R";
        private const string TRUESTRING = "TRUE";
        private const string FALSESTRING = "FALSE";

        // If there is no stored argument string, or if the number of groups has changed, the UI loads the
        // default settings for group comparisons
        private void RestoreSettings()
        {
            const int nonIndexArguments = 5;

            // Restore view only if there are the same number of groups as before 
            if (Arguments != null && (Arguments.Length - nonIndexArguments == ControlGroupList.Length))
            {
                // Restore the selected control group 
                ControlGroup.SelectedIndex = Array.IndexOf(Arguments, "1");

                // Restore the selection of comparison groups (if necessary)
                if (ControlGroupList.Length > 2)
                {
                    for (int i = 0; i < Arguments.Length - nonIndexArguments; i++)
                    {
                        double groupConstant = Double.Parse(Arguments[i]);
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

                int lastArg = Arguments.Length - 1;
                cboxInterferenceTransitions.Checked = Arguments[lastArg--].Equals(TRUESTRING);

                if (Arguments[lastArg--].Equals(EXPANDED))
                    techRepExp.Checked = true;

                if (!Arguments[lastArg--].Equals(EXPANDED))
                    bioRepRes.Checked = true;

                // Restore settings 
                cboxLabelData.Checked = Arguments[lastArg--].Equals(TRUESTRING);

                // Restore name
                textBoxName.Text = Arguments[lastArg];
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
                MessageBox.Show(this, "Please select at least one comparison group.");
            }
            else if (string.IsNullOrWhiteSpace(textBoxName.Text))
            {
                MessageBox.Show(this, "Please enter a name for this comparison.");
            }
            else
            {
                GenerateArguments();
                DialogResult = DialogResult.OK;
            }
        }

        // The argument array for group comparisons is composed of the following elements:
        // [(-1 <= x <= 1){2,} , Comparison Name , TRUE|FALSE , E|R , E|R , TRUE|FALSE]
        //
        // The next n elements is a series of n doubles that represent the constants that will be applied 
        // to each group, where n is the (2+) total number of groups in the data source. There will
        // be a single "-1" in this series, which represents the constant applied to the
        // control group. If there is only one other group for the control to be compared against, it will
        // have a value of 1, while all other groups will have constants of 0. An example
        // subarray that might be generated would be: [1 , 0 , -1 , 0]
        //
        // In the case that there are k>1 groups to be compared against, each group that the control will
        // be compared against adopts a value of 1.0/k, while any groups not being compared against the control
        // again adopt a constant of 0. An example subarray that might be generated would be: [0 , 0 , 0.5 , -1 , 0 , 0.5] 
        // where k = 2
        //
        // The next element of the argument array is its name of the comparison 
        //
        // The last four elements of the argument array are as follows:
        // "TRUE" - the user wants to label data, otherwise "FALSE"
        // "E" - expanded: the user wants the scope of biological replicates to be expanded, otherwise "R" - Restricted
        // "E" - expanded: the user wants the scope of technical replicates to be expanded, otherwise "R" - Restricted
        // "TRUE" - the user wants to include inference transitions, otherwise "FALSE"
        //
        // An example of a complete array would be [0 , -1 , 0.5 , 0.5 , 0 , 0 , Disease-Healthy , TRUE , E , R , FALSE]
        private void GenerateArguments()
        {
            ICollection<string> commandLineArguments = new Collection<string>();
            
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

            // Add name
            commandLineArguments.Add(textBoxName.Text);

            // Add settings
            commandLineArguments.Add(cboxLabelData.Checked ? TRUESTRING : FALSESTRING);
            commandLineArguments.Add(bioRepExp.Checked ? EXPANDED : RESTRICTED);
            commandLineArguments.Add(techRepExp.Checked ? EXPANDED : RESTRICTED);
            commandLineArguments.Add(cboxInterferenceTransitions.Checked ? TRUESTRING : FALSESTRING);

            Arguments = commandLineArguments.ToArray();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }
    }

    public class MSstatsGroupComparisonCollector
    {
        public static string[] CollectArgs(IWin32Window parent, string report, string[] oldArgs)
        {
            // Split report (.csv file) by lines
            string[] lines = report.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            string[] fields = lines[0].ParseCsvFields();
            int groupIndex = Array.IndexOf(fields, "Condition");

            ICollection<string> groups = new HashSet<string>();
            try
            {
                // The last line in the CSV file is empty, thus we compare length - 1 
                for (int i = 1; i < lines.Length - 1; i++)
                {
                    groups.Add(lines[i].ParseCsvFields()[groupIndex]);
                }
            }
            catch
            {
                // File improperly formatted
                return null;
            }

            using (var dlg = new GroupComparisonUi(groups.ToArray(), oldArgs))
            {
                if (parent != null)
                {
                    return (dlg.ShowDialog(parent) == DialogResult.OK) ? dlg.Arguments : null;
                }
                return (dlg.ShowDialog() == DialogResult.OK) ? dlg.Arguments : null;
            }
        }
    }
}
