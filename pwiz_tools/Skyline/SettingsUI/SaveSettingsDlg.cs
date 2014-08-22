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
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public partial class SaveSettingsDlg : FormEx
    {
        public SaveSettingsDlg()
        {
            InitializeComponent();
        }

        public string SaveName { get; set; }

        #region Test helpers

        public string SettingsName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        #endregion Test

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(textName, out name))
                return;

            foreach (SrmSettings settings in Settings.Default.SrmSettingsList)
            {
                if (name == settings.Name)
                {
                    var message = TextUtil.LineSeparate(String.Format(Resources.SaveSettingsDlg_OnClosing_The_name__0__already_exists, name),
                                                        Resources.SaveSettingsDlg_OnClosing_Do_you_want_to_overwrite_the_existing_settings);
                    var result = MessageBox.Show(this, message, Program.Name, MessageBoxButtons.OKCancel, MessageBoxIcon.Question,
                                                    MessageBoxDefaultButton.Button2);
                    if (result == DialogResult.OK)
                        break;

                    textName.Focus();
                    return;
                }
            }

            SaveName = name;

            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }
    }
}
