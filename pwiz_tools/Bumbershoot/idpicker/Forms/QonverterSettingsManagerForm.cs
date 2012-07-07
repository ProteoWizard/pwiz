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
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
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
using IDPicker.Controls;
using IDPicker.DataModel;

namespace IDPicker.Forms
{
    /// <summary>
    /// Allows viewing and manipulation of the QonverterSettings presets stored in user settings.
    /// </summary>
    public partial class QonverterSettingsManagerForm : Form
    {
        public QonverterSettingsManagerForm ()
        {
            InitializeComponent();

            // load qonverter settings into listView
            IDictionary<string, QonverterSettings> qonverterSettingsByName = QonverterSettings.LoadQonverterSettings();
            foreach (var kvp in qonverterSettingsByName)
                listView.Items.Add(new ListViewItem(kvp.Key) { Tag = kvp.Value });

            nameColumn.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);

            listView.Items[0].Selected = true;
        }

        protected override void OnFormClosing (FormClosingEventArgs e)
        {
            // immediate cancel
            if (DialogResult == DialogResult.Cancel)
            {
                base.OnFormClosing(e);
                return;
            }

            var buttons = MessageBoxButtons.YesNo;
            if (e.CloseReason == CloseReason.UserClosing)
                buttons = MessageBoxButtons.YesNoCancel;

            // save current item if changes have been made necessary
            if (lastSelectedItem != null && qonverterSettingsControl.IsDirty)
            {
                var result = MessageBox.Show("Do you want to save your changes to '" + lastSelectedItem.Text + "'?",
                                             "Save changes?",
                                             buttons,
                                             MessageBoxIcon.Question,
                                             MessageBoxDefaultButton.Button1);
                if (result == DialogResult.Cancel)
                    e.Cancel = true;
                else if (result == DialogResult.Yes)
                    lastSelectedItem.Tag = qonverterSettingsControl.EditedQonverterSettings;
            }

            // save all items
            var qonverterSettingsByName = new Dictionary<string, QonverterSettings>();
            foreach (ListViewItem item in listView.Items)
                qonverterSettingsByName[item.Text] = item.Tag as QonverterSettings;

            QonverterSettings.SaveQonverterSettings(qonverterSettingsByName);

            base.OnFormClosing(e);
        }

        private void okButton_Click (object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void cancelButton_Click (object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        ListViewItem lastSelectedItem = null;
        private void listView_SelectedIndexChanged (object sender, EventArgs e)
        {
            if (lastSelectedItem != null &&
                qonverterSettingsControl.IsDirty &&
                MessageBox.Show("Do you want to save your changes to '" + lastSelectedItem.Text + "'?",
                                "Save changes?",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question,
                                MessageBoxDefaultButton.Button1) == DialogResult.Yes)
            {
                qonverterSettingsControl.CommitChanges();
                lastSelectedItem.Tag = qonverterSettingsControl.QonverterSettings;
            }

            if(listView.SelectedIndices.Count == 1)
            {
                qonverterSettingsControl.QonverterSettings = listView.SelectedItems[0].Tag as QonverterSettings;
                lastSelectedItem = listView.SelectedItems[0];
            }
        }

        private void listView_KeyPress (object sender, KeyPressEventArgs e)
        {
            // delete the selected row
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            var textBox = new TextInputPrompt("Setting Name", false, string.Empty);
            if (textBox.ShowDialog() == DialogResult.OK)
            {
                var newSettings = new QonverterSettings()
                                      {
                                          QonverterMethod = Qonverter.QonverterMethod.StaticWeighted,
                                          RerankMatches = false,
                                          ScoreInfoByName = new Dictionary<string, Qonverter.Settings.ScoreInfo>()
                                      };
                listView.Items.Add(new ListViewItem(textBox.GetText()) {Tag = newSettings});
                listView.Items[listView.Items.Count - 1].Selected = true;
            }
        }

        private void RenameButton_Click(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count != 1)
            {
                MessageBox.Show("Please select an item to rename");
                return;
            }

            var textBox = new TextInputPrompt("Setting Name", false, listView.SelectedItems[0].Text);
            if (textBox.ShowDialog() == DialogResult.OK)
                listView.SelectedItems[0].Text = textBox.GetText();
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count == 1
                && MessageBox.Show(
                    "Are you sure you want to delete the currently selected setting?",
                    "Delete setting",
                    MessageBoxButtons.YesNo) == DialogResult.Yes)
                listView.Items.RemoveAt(listView.SelectedIndices[0]);
        }
    }
}
