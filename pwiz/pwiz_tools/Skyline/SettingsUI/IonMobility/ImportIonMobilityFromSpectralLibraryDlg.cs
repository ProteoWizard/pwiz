/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI.IonMobility
{
    public enum SpectralLibrarySource { settings, file }

    public partial class ImportIonMobilityFromSpectralLibraryDlg : FormEx
    {
        private CollisionalCrossSectionGridViewDriver _gridViewDriver;

        public ImportIonMobilityFromSpectralLibraryDlg(IEnumerable<LibrarySpec> librarySpecs, CollisionalCrossSectionGridViewDriver gridViewDriver)
        {
            InitializeComponent();

            comboLibrary.Items.AddRange(librarySpecs.Cast<object>().ToArray());
            if ((comboLibrary.Items.Count == 1) &&  (Source == SpectralLibrarySource.settings))
                comboLibrary.SelectedIndex = 0; // The obvious choice
            ComboHelper.AutoSizeDropDown(comboLibrary);
            _gridViewDriver = gridViewDriver;
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
                return new BiblioSpecLiteSpec("__internal__", textFilePath.Text); // Not L10N
            }
        }

        public Dictionary<int, RegressionLine> ChargeRegressionsLines { get; private set; }

        public string FilePath
        {
            get { return textFilePath.Text; }
            set { textFilePath.Text = value; }
        }

        public static string ValidateSpectralLibraryPath(string path)
        {
            string message = null;
            if (string.IsNullOrEmpty(path))
                message = Resources.ImportIonMobilityFromSpectralLibrary_ValidateSpectralLibraryPath_Please_specify_a_path_to_an_existing_spectral_library;
            else if (path.EndsWith(BiblioSpecLiteSpec.EXT_REDUNDANT))
            {
                message = TextUtil.LineSeparate(string.Format(Resources.ImportIonMobilityFromSpectralLibrary_ValidateSpectralLibraryPath_The_file__0__appears_to_be_a_redundant_library_, path),
                                                Resources.ImportIonMobilityFromSpectralLibrary_ValidateSpectralLibraryPath_Please_choose_a_non_redundant_library_);
            }
            else if (!path.EndsWith(BiblioSpecLiteSpec.EXT))
            {
                message = TextUtil.LineSeparate(string.Format(Resources.ImportIonMobilityFromSpectralLibrary_ValidateSpectralLibraryPath_The_file__0__is_not_a_BiblioSpec_library_, path),
                                                Resources.ImportIonMobilityFromSpectralLibrary_ValidateSpectralLibraryPath_Only_BiblioSpec_libraries_contain_enough_ion_mobility_information_to_support_this_operation_);
            }
            else if (!File.Exists(path))
            {
                message = TextUtil.LineSeparate(string.Format(Resources.ImportIonMobilityFromSpectralLibrary_ValidateSpectralLibraryPath_The_file__0__does_not_exist_, path),
                                                Resources.ImportIonMobilityFromSpectralLibrary_ValidateSpectralLibraryPath_Please_specify_a_path_to_an_existing_spectral_library_);
            }
            return message;
        }

        public void OkDialog()
        {
            string message;
            if (Source == SpectralLibrarySource.file)
            {
                string path = textFilePath.Text;
                message = ValidateSpectralLibraryPath(path);
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
                MessageDlg.Show(this, Resources.ImportIonMobilityFromSpectralLibrary_OkDialog_Please_choose_the_library_you_would_like_to_add_);
                return;
            }

            var table = new ChargeRegressionTable(gridRegression);
            var chargeRegressionLinesList = table.GetTableChargeRegressionLines();
            if (chargeRegressionLinesList == null) // Some error detected in the charged regression lines table
                return;
            ChargeRegressionsLines = chargeRegressionLinesList.ToDictionary(x => x.Charge, x => x.RegressionLine);

            // Have we got everything we need to populate the caller's grid view?
            message = _gridViewDriver.ImportFromSpectralLibrary(librarySpec, ChargeRegressionsLines);
            if (message == null)
            {
                DialogResult = DialogResult.OK;
            }
            else
            {
                MessageDlg.Show(this, message);
            }
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
                Filter = TextUtil.FileDialogFiltersAll(BiblioSpecLiteSpec.FILTER_BLIB)
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                Settings.Default.LibraryDirectory = Path.GetDirectoryName(dlg.FileName);
                textFilePath.Text = dlg.FileName;
            }
        }

        #region Functional test support

        public void PasteRegressionValues()
        {
            gridRegression.DoPaste(this, ChargeRegressionTable.ValidateRegressionCellValues);
        }

        #endregion

        private void gridRegression_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl + V for paste
            if (e.KeyCode == Keys.V && e.Control)
            {
                PasteRegressionValues();
            }
            else if (e.KeyCode == Keys.Delete)
            {
                gridRegression.DoDelete();
            }
        }

    }
}
