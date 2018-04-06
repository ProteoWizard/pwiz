//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Jay Holman
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace IDPicker.Forms
{
    public partial class RTGroupingForm : Form
    {
        private List<string> _sourceList;

        public RTGroupingForm(IEnumerable<string> sources)
        {
            _sourceList = new List<string>(sources);
            InitializeComponent();
        }

        private void RTGroupingForm_Load(object sender, EventArgs e)
        {
            foreach (var item in _sourceList)
                fileGroupingBox.Rows.Add(item, "");
        }

        private void autoDetectButton_Click(object sender, EventArgs e)
        {
            var minLength = _sourceList.Min(x => x.Length);
            var targetGroups = groupCountBox.Value;
            var inverseSearch = backRadio.Checked;

            for (var x = 1; x <= minLength; x++)
            {
                var groupSet = new HashSet<string>();
                var groupDict = new Dictionary<string, string>();

                foreach (var item in _sourceList)
                {
                    var groupName = inverseSearch
                                        ? item.Substring(item.Length - x)
                                        : item.Substring(0, x);
                    groupSet.Add(groupName);
                    groupDict[item] = groupName;
                }
                if (groupSet.Count != targetGroups) continue;

                fileGroupingBox.Rows.Clear();
                foreach (var item in _sourceList)
                    fileGroupingBox.Rows.Add(item, groupDict[item]);
                MessageBox.Show("Succesfully auto-detected groups");
                return;
            }
            MessageBox.Show("Could not auto-detect groups. Check number of expected groups");
        }

        public List<List<string>> GetRTGroups()
        {
            var groupList = new List<List<string>>();
            var groupDict = new Dictionary<string, List<string>>();
            foreach (DataGridViewRow row in fileGroupingBox.Rows)
            {
                var group = row.Cells[RTGroupColumn.Index].Value.ToString();
                var source = row.Cells[sourceColumn.Index].Value.ToString();
                if (!groupDict.ContainsKey(group))
                    groupDict[group] = new List<string>{source};
                else
                    groupDict[group].Add(source);
            }
            foreach (var kvp in groupDict)
            {
                var subList = kvp.Value.ToList();
                groupList.Add(subList);
            }
            return groupList;
        }
    }
}
