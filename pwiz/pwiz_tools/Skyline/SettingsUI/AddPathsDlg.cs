/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
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
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public partial class AddPathsDlg : FormEx
    {
        public AddPathsDlg()
        {
            InitializeComponent();

            Icon = Resources.Skyline;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            string error = CheckForError();
            if (error == string.Empty)
                DialogResult = DialogResult.OK;
            else
                MessageDlg.Show(this, error);
        }

        public string CheckForError()
        {
            var fileNames = FileNames;
            List<string> badFiles = CheckFileExistence(fileNames);
            if (badFiles.Count != 0)
            {
                return string.Format(TextUtil.LineSeparate(Resources.AddPathsDlg_OkDialog_The_following_files_could_not_be_found_, string.Empty, "    {0}"), // Not L10N
                       string.Join("\n    ", badFiles.ToArray())); // Not L10N
            }

            badFiles = CheckFileTypes(fileNames);
            if (badFiles.Count != 0)
            {
                return string.Format(TextUtil.LineSeparate(Resources.AddPathsDlg_OkDialog_The_following_files_are_not_valid_library_input_files_, string.Empty, "    {0}"), // Not L10N
                       string.Join("\n    ", badFiles.ToArray())); // Not L10N
            }

            return string.Empty;
        }

        public string[] FileNames
        {
            get
            {
                var paths = new List<string>();
                foreach (string line in textPaths.Lines)
                {
                    string path = line.Trim();
                    if (path != string.Empty)
                        paths.Add(path);
                }
                return paths.ToArray();
            }
            set { textPaths.Lines = value; }
        }

        private static List<string> CheckFileExistence(IEnumerable<string> fileNames)
        {
            var missingFiles = new List<string>();
            foreach (string path in fileNames)
            {
                if (!File.Exists(path))
                    missingFiles.Add(path);
            }
            return missingFiles;
        }

        private static List<string> CheckFileTypes(IEnumerable<string> fileNames)
        {
            var invalidFiles = new List<string>();
            foreach (string path in fileNames)
            {
                bool validExtension = false;
                foreach (string extResult in BuildLibraryDlg.RESULTS_EXTS)
                {
                    if (PathEx.HasExtension(path, extResult))
                    {
                        validExtension = true;
                        break;
                    }
                }

                if (!validExtension)
                    invalidFiles.Add(path);
            }
            return invalidFiles;
        }
    }
}
