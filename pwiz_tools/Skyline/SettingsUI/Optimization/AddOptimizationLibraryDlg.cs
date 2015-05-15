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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI.Optimization
{
    public enum OptimizationLibrarySource { settings, file }

    public partial class AddOptimizationLibraryDlg : FormEx
    {
        public AddOptimizationLibraryDlg(IEnumerable<OptimizationLibrary> libraries)
        {
            InitializeComponent();

            comboLibrary.Items.AddRange(libraries.Cast<object>().ToArray());
            ComboHelper.AutoSizeDropDown(comboLibrary);

            if (comboLibrary.Items.Count > 0)
                comboLibrary.SelectedIndex = 0;
        }

        public OptimizationLibrarySource Source
        {
            get { return radioSettings.Checked ? OptimizationLibrarySource.settings : OptimizationLibrarySource.file; }

            set
            {
                if (value == OptimizationLibrarySource.settings)
                    radioSettings.Checked = true;
                else
                    radioFile.Checked = true;
            }
        }

        public string FilePath
        {
            get { return radioFile.Checked ? textFilePath.Text : string.Empty; }
            set { if (radioFile.Checked) textFilePath.Text = value; }
        }

        public OptimizationLibrary Library
        {
            get
            {
                if (Source == OptimizationLibrarySource.settings)
                    return (OptimizationLibrary)comboLibrary.SelectedItem;
                return new OptimizationLibrary("Add", textFilePath.Text);  // Not L10N
            }
        }

        public void SetLibrary(string libName)
        {
            if (string.IsNullOrEmpty(libName))
                return;
            int index = comboLibrary.FindStringExact(libName);
            if (index >= 0)
                comboLibrary.SelectedIndex = index;
        }

        public void OkDialog()
        {
            if (Source == OptimizationLibrarySource.file)
            {
                string path = textFilePath.Text;
                string message = null;
                if (string.IsNullOrEmpty(path))
                    message = Resources.AddOptimizationDlg_OkDialog_Please_specify_a_path_to_an_existing_optimization_library_;
                else if (!path.EndsWith(OptimizationDb.EXT))
                    message = string.Format(Resources.AddOptimizationDlg_OkDialog_The_file__0__is_not_an_optimization_library_, path);
                else if (!File.Exists(path))
                {
                    message = TextUtil.LineSeparate(string.Format(Resources.AddOptimizationDlg_OkDialog_The_file__0__does_not_exist_, path),
                                                    Resources.AddOptimizationDlg_OkDialog_Please_specify_a_path_to_an_existing_optimization_library_);
                }
                if (message != null)
                {
                    MessageDlg.Show(this, message);
                    textFilePath.Focus();
                    return;
                }
            }
            var library = Library;
            if (library == null)
            {
                MessageDlg.Show(this, Resources.AddOptimizationDlg_OkDialog_Please_choose_the_optimization_library_you_would_like_to_add_);
                return;
            }

            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void radioSettings_CheckedChanged(object sender, EventArgs e)
        {
            SourceChanged();
        }

        private void radioFile_CheckedChanged(object sender, EventArgs e)
        {
            SourceChanged();
        }

        private void SourceChanged()
        {
            if (Source == OptimizationLibrarySource.settings)
            {
                comboLibrary.Enabled = true;
                if (comboLibrary.Items.Count > 0)
                    comboLibrary.SelectedIndex = 0;
                textFilePath.Enabled = false;
                textFilePath.Text = string.Empty;
                btnBrowseFile.Enabled = false;
            }
            else
            {
                comboLibrary.SelectedIndex = -1;
                comboLibrary.Enabled = false;
                textFilePath.Enabled = true;
                btnBrowseFile.Enabled = true;
            }
        }

        private void btnBrowseFile_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                CheckPathExists = true,
                DefaultExt = OptimizationDb.FILTER_OPTDB,
                Filter = TextUtil.FileDialogFiltersAll(OptimizationDb.FILTER_OPTDB)
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                textFilePath.Text = dlg.FileName;
            }
        }
    }
}
