/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public enum SpectralLibrarySource { settings, file }

    public partial class AddIrtSpectralLibrary : FormEx
    {
        public AddIrtSpectralLibrary(IEnumerable<LibrarySpec> librarySpecs)
        {
            InitializeComponent();

            comboLibrary.Items.AddRange(librarySpecs.Cast<object>().ToArray());
            ComboHelper.AutoSizeDropDown(comboLibrary);
        }

        public SpectralLibrarySource Source
        {
            get { return radioSettings.Checked ? SpectralLibrarySource.settings : SpectralLibrarySource.file; }

            set
            {
                if (value == SpectralLibrarySource.settings)
                    radioSettings.Checked = true;
                else
                    radioFile.Checked = true;
            }
        }

        public LibrarySpec Library
        {
            get
            {
                if (Source == SpectralLibrarySource.settings)
                    return (LibrarySpec)comboLibrary.SelectedItem;
                return new BiblioSpecLiteSpec("__internal__", textFilePath.Text);
            }
        }

        public string FilePath
        {
            get { return textFilePath.Text; }
            set { textFilePath.Text = value; }
        }

        public void OkDialog()
        {
            if (Source == SpectralLibrarySource.file)
            {
                string path = textFilePath.Text;
                string message = null;
                if (string.IsNullOrEmpty(path))
                    message = "Please specify a path to an existing spectral library.";
                else if (path.EndsWith(BiblioSpecLiteSpec.EXT_REDUNDANT))
                    message = string.Format("The file {0} appears to be a redundant library.\nPlease choose a  non-redundant library.", path);
                else if (!path.EndsWith(BiblioSpecLiteSpec.EXT))
                    message = string.Format("The file {0} is not a BiblioSpec library.\nOnly BiblioSpec libraries contain enough retention time information to support this operation.", path);
                else if (!File.Exists(path))
                    message = string.Format("The file {0} does not exist.\nPlease specify a path to an existing spectral library.", path);
                if (message != null)
                {
                    MessageDlg.Show(this, message);
                    textFilePath.Focus();
                    return;                    
                }
            }
            var librarySpec = Library;
            if (librarySpec == null)
            {
                MessageDlg.Show(this, "Please choose the library you would like to add.");
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
            if (Source == SpectralLibrarySource.settings)
            {
                comboLibrary.Enabled = true;
                textFilePath.Enabled = false;
                textFilePath.Text = "";
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
                InitialDirectory = Settings.Default.LibraryDirectory,
                CheckPathExists = true,
                DefaultExt = BiblioSpecLibSpec.EXT,
                Filter = string.Join("|", new[]
                    {
                        "BiblioSpec Libraries (*" + BiblioSpecLiteSpec.EXT + ")|*" + BiblioSpecLiteSpec.EXT,
                        "All Files (*.*)|*.*"
                    })
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                Settings.Default.LibraryDirectory = Path.GetDirectoryName(dlg.FileName);
                textFilePath.Text = dlg.FileName;
            }
        }
    }
}
