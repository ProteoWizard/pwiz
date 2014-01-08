/*
 * Original author: Daniel Broudy <daniel.broudy .at. gmail.com>,
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
using System.IO;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using System.Windows.Forms;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Alerts
{
    public partial class LocateFileDlg : FormEx
    {
        public LocateFileDlg(ProgramPathContainer ppc)
        {
            InitializeComponent();
            string programName = ppc.ProgramName;
            string programVersion = ppc.ProgramVersion;
            if (Settings.Default.ToolFilePaths.ContainsKey(ppc))
            {
                labelMessage.Text = programVersion != null
                                          ? string.Format(TextUtil.LineSeparate(
                                              Resources.LocateFileDlg_LocateFileDlg_This_tool_requires_0_version_1,
                                              Resources.LocateFileDlg_LocateFileDlg_Below_is_the_saved_value_for_the_path_to_the_executable,
                                              Resources.LocateFileDlg_LocateFileDlg_Please_verify_and_update_if_incorrect),
                                              programName, programVersion)
                                          : string.Format(TextUtil.LineSeparate(
                                              Resources.LocateFileDlg_LocateFileDlg_This_tool_requires_0,
                                              Resources.LocateFileDlg_LocateFileDlg_Below_is_the_saved_value_for_the_path_to_the_executable,
                                              Resources.LocateFileDlg_LocateFileDlg_Please_verify_and_update_if_incorrect),
                                              programName);
                textPath.Text = Settings.Default.ToolFilePaths[ppc];
            }
            else
            {
                labelMessage.Text = programVersion != null
                                          ? string.Format(TextUtil.LineSeparate(
                                              Resources.LocateFileDlg_LocateFileDlg_This_tool_requires_0_version_1,
                                              Resources.LocateFileDlg_LocateFileDlg_If_you_have_it_installed_please_provide_the_path_below,
                                              Resources.LocateFileDlg_LocateFileDlg_Otherwise__please_cancel_and_install__0__version__1__first,
                                              Resources.LocateFileDlg_LocateFileDlg_then_run_the_tool_again),
                                              programName, programVersion)
                                          : string.Format(TextUtil.LineSeparate(
                                              Resources.LocateFileDlg_LocateFileDlg_This_tool_requires_0,
                                              Resources.LocateFileDlg_LocateFileDlg_If_you_have_it_installed_please_provide_the_path_below,
                                              Resources.LocateFileDlg_LocateFileDlg_Otherwise__please_cancel_and_install__0__first,
                                              Resources.LocateFileDlg_LocateFileDlg_then_run_the_tool_again),
                                              programName);                
            }
            _pathContainer = ppc;
        }

        public string Message
        {
            get { return labelMessage.Text; }
        }

        public string Path
        {
            get { return textPath.Text; }
            set { textPath.Text = value; }
        }

        private readonly ProgramPathContainer _pathContainer;

        private void btnFindPath_Click(object sender, EventArgs e)
        {
            FindPath();
        }

        private void FindPath()
        {
            using (var openFileDialog = new OpenFileDialog
            {
                Multiselect = false
            })
            {
                if (openFileDialog.ShowDialog(this) == DialogResult.OK)
                {
                    textPath.Text = openFileDialog.FileName;                    
                }
            }
        }

        public void OkDialog()
        {
            if (ValidatePath())
            {
                // Add the path to user settings!
                var filePaths = Settings.Default.ToolFilePaths;
                if (String.IsNullOrEmpty(textPath.Text))
                {
                    //Remove the key if it exists
                    if (filePaths.ContainsKey(_pathContainer))
                    {
                        filePaths.Remove(_pathContainer);
                    }
                }

                else
                {
                    if (!filePaths.ContainsKey(_pathContainer))
                        filePaths.Add(_pathContainer, textPath.Text);
                    else
                    {
                        filePaths[_pathContainer] = textPath.Text;
                    }
                }

                Settings.Default.ToolFilePaths = CopyFilePaths(filePaths);
                DialogResult = DialogResult.OK;
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private static SerializableDictionary<ProgramPathContainer, string> CopyFilePaths(Dictionary<ProgramPathContainer, string> list)
        {
            var dictionary = new SerializableDictionary<ProgramPathContainer, string>();
            foreach (ProgramPathContainer key in list.Keys)
                dictionary.Add(key, list[key]);
            return dictionary;
        }

        private bool ValidatePath()
        {
            string path = textPath.Text;
            if (string.IsNullOrEmpty(path))
            {                
                return true;
            }

            if (File.Exists(path))
            {
                return true;
            }

            MessageDlg.Show(this, Resources.LocateFileDlg_PathPasses_You_have_not_provided_a_valid_path_);
            textPath.Focus();
            return false;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
