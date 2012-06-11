/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class SaveSettingsDlg : FormEx
    {
        private bool _clickedOk;

        private readonly MessageBoxHelper _helper;

        public SaveSettingsDlg()
        {
            InitializeComponent();

            _helper = new MessageBoxHelper(this);
        }

        public string SaveName { get; set; }

        protected override void OnClosing(CancelEventArgs e)
        {
            // If the user is accepting these settings, then validate
            // and hold them in dialog member variables.
            if (_clickedOk)
            {
                _clickedOk = false; // Reset in case of failure

                string name;
                if (!_helper.ValidateNameTextBox(e, textName, out name))
                    return;

                foreach (SrmSettings settings in Settings.Default.SrmSettingsList)
                {
                    if (name == settings.Name)
                    {
                        var result = MessageBox.Show(string.Format("The name {0} already exists.\nDo you want to overwrite the existing settings?", name),
                            Program.Name, MessageBoxButtons.OKCancel, MessageBoxIcon.Question,
                            MessageBoxDefaultButton.Button2);
                        if (result == DialogResult.OK)
                            break;

                        textName.Focus();
                        e.Cancel = true;
                        return;
                    }
                }

                SaveName = name;
            }

            base.OnClosing(e);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            _clickedOk = true;
        }
    }
}
