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
using pwiz.Skyline.Model.Lib.ChromLib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

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
                
                if (Path.GetExtension(textFilePath.Text) == ChromatogramLibrarySpec.EXT)
                    return new ChromatogramLibrarySpec("__internal__", textFilePath.Text); // Not L10N

                return new BiblioSpecLiteSpec("__internal__", textFilePath.Text); // Not L10N
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
                    message = Resources.AddIrtSpectralLibrary_OkDialog_Please_specify_a_path_to_an_existing_spectral_library;
                else if (path.EndsWith(BiblioSpecLiteSpec.EXT_REDUNDANT))
                {
                    message = TextUtil.LineSeparate(string.Format(Resources.AddIrtSpectralLibrary_OkDialog_The_file__0__appears_to_be_a_redundant_library, path),
                                                    Resources.AddIrtSpectralLibrary_OkDialog_Please_choose_a_non_redundant_library);
                }
                else if (!path.EndsWith(BiblioSpecLiteSpec.EXT) && !path.EndsWith(ChromatogramLibrarySpec.EXT))
                {
                    message = TextUtil.LineSeparate(string.Format(Resources.AddIrtSpectralLibrary_OkDialog_The_file__0__is_not_a_BiblioSpec_or_Chromatogram_library, path),
                                                    Resources.AddIrtSpectralLibrary_OkDialog_Only_BiblioSpec_and_Chromatogram_libraries_contain_enough_retention_time_information_to_support_this_operation);
                }
                else if (!File.Exists(path))
                {
                    message = TextUtil.LineSeparate(string.Format(Resources.AddIrtSpectralLibrary_OkDialog_The_file__0__does_not_exist, path),
                                                    Resources.AddIrtSpectralLibrary_OkDialog_Please_specify_a_path_to_an_existing_spectral_library); 
                }
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
                MessageDlg.Show(this, Resources.AddIrtSpectralLibrary_OkDialog_Please_choose_the_library_you_would_like_to_add);
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
                InitialDirectory = Settings.Default.LibraryDirectory,
                CheckPathExists = true,
                DefaultExt = BiblioSpecLiteSpec.EXT,
                Filter = TextUtil.FileDialogFiltersAll(
                    TextUtil.FileDialogFilter(Resources.AddIrtSpectralLibrary_btnBrowseFile_Click_Spectral_Libraries, BiblioSpecLiteSpec.EXT, ChromatogramLibrarySpec.EXT)
                )
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
