/*
 * Original author: Shannon Joyner <saj9191 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.ToolsUI
{
    public partial class ToolOptionsUI : FormEx
    {
        private readonly SettingsListBoxDriver<Server> _driverServers;

        public ToolOptionsUI()
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _driverServers = new SettingsListBoxDriver<Server>(listboxServers, Settings.Default.ServerList);
            _driverServers.LoadList();
            checkBoxLiveReports.Checked = Settings.Default.EnableLiveReports;

            // Hide ability to turn off live reports
            //tabControl.TabPages.Remove(tabMisc);

            // Populate the languages list with the languages that Skyline has been localized to
            string defaultDisplayName = string.Format(Resources.ToolOptionsUI_ToolOptionsUI_Default___0__,
                CultureUtil.GetDisplayLanguage(CultureInfo.InstalledUICulture).DisplayName);
            listBoxLanguages.Items.Add(new DisplayLanguageItem(string.Empty, defaultDisplayName));
            foreach (var culture in CultureUtil.AvailableDisplayLanguages())
            {
                listBoxLanguages.Items.Add(new DisplayLanguageItem(culture.Name, culture.DisplayName));
            }
            for (int i = 0; i < listBoxLanguages.Items.Count; i++)
            {
                var displayLanguageItem = (DisplayLanguageItem) listBoxLanguages.Items[i];
                if (Equals(displayLanguageItem.Key, Settings.Default.DisplayLanguage))
                {
                    listBoxLanguages.SelectedIndex = i;
                }
            }
        }

        private void btnEditServers_Click(object sender, EventArgs e)
        {
            EditServers();
        }

        public void EditServers()
        {
            _driverServers.EditList();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                Program.MainWindow.SetEnableLiveReports(checkBoxLiveReports.Checked);
                var displayLanguageItem = listBoxLanguages.SelectedItem as DisplayLanguageItem;
                if (null != displayLanguageItem)
                {
                    Settings.Default.DisplayLanguage = displayLanguageItem.Key;
                }
            }
            base.OnClosed(e);
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private class DisplayLanguageItem
        {
            public DisplayLanguageItem(string key, string displayName)
            {
                Key = key;
                DisplayName = displayName;
            }
            public string DisplayName { get; private set; }
            public string Key { get; private set; }
            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}
