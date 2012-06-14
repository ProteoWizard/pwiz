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
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
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
        }

        private void btnEditServers_Click(object sender, EventArgs e)
        {
            EditServers();
        }

        public void EditServers()
        {
            _driverServers.EditList();
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        
        public SettingsList<Server> GetServers()
        {
            return _driverServers.List;
        }
    }
}
