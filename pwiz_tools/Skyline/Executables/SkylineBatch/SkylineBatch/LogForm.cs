/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Windows.Forms;

namespace SkylineBatch
{
    public partial class LogForm : Form
    {
        // Delete Logs Form
        // User selects logs to delete using checkboxes

        private readonly SkylineBatchConfigManager _configManager;
        public LogForm(SkylineBatchConfigManager configManager)
        {
            InitializeComponent();
            Icon = Program.Icon();
            _configManager = configManager;
            if (_configManager.HasOldLogs())
                checkedListLogs.Items.AddRange(_configManager.GetOldLogFiles());
        }
        

        private void btnOk_Click(object sender, EventArgs e)
        {
            var deletingLogs = new object[checkedListLogs.CheckedItems.Count];
            checkedListLogs.CheckedItems.CopyTo(deletingLogs, 0);
            _configManager.DeleteLogs(deletingLogs);
            Close();
        }

        private void checkBoxSelectAll_Click(object sender, EventArgs e)
        {
            checkedListLogs.ItemCheck -= checkedListLogs_ItemCheck;
            for (int i = 0; i < checkedListLogs.Items.Count; i++)
            {
                checkedListLogs.SetItemChecked(i, checkBoxSelectAll.Checked);
            }
            checkedListLogs.ItemCheck += checkedListLogs_ItemCheck;
        }

        private void checkedListLogs_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.NewValue == CheckState.Unchecked)
            {
                checkBoxSelectAll.Checked = false;
                return;
            }
            var finalLogChecked = checkedListLogs.CheckedItems.Count == checkedListLogs.Items.Count - 1;
            checkBoxSelectAll.Checked = finalLogChecked;
        }
    }
}
