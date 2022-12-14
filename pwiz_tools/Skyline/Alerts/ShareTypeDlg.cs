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
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using System.Linq;

namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// Support for File > Share to create a .sky.zip file.
    /// CONSIDER: Seems more appropriate to move this to the FileUI namespace
    /// </summary>
    public partial class ShareTypeDlg : FormEx
    {
        private List<SkylineVersion> _skylineVersionOptions;
        private readonly SrmDocument _document;
        private string _documentFilePath;
        private readonly DocumentFormat? _savedFileFormat;
        private ShareResultsFilesDlg.AuxiliaryFiles _auxiliaryFiles; // Mass spec data files/folders to be included

        public ShareTypeDlg(SrmDocument document, string documentFilePath, DocumentFormat? savedFileFormat)
            : this(document, documentFilePath, savedFileFormat, SkylineVersion.CURRENT)
        {
        }

        public ShareTypeDlg(SrmDocument document, string documentFilePath, DocumentFormat? savedFileFormat, SkylineVersion maxSupportedVersion, bool allowDataSources = true)
        {
            InitializeComponent();
            _skylineVersionOptions = new List<SkylineVersion>();
            _document = document;
            _documentFilePath = documentFilePath;
            _savedFileFormat = savedFileFormat;

            if (!allowDataSources)
            {
                // Get rid of the area where we would have asked about data sources
                Height -= (btnSelectReplicateFiles.Height + (radioMinimal.Top - radioComplete.Bottom));
            }

            if (savedFileFormat.HasValue && maxSupportedVersion.SrmDocumentVersion.CompareTo(savedFileFormat.Value) >= 0)
            {
                _skylineVersionOptions.Add(null);
                comboSkylineVersion.Items.Add(string.Format(Resources.ShareTypeDlg_ShareTypeDlg_Current_saved_file___0__, savedFileFormat.Value.GetDescription()));
            }

            foreach (var skylineVersion in SkylineVersion.SupportedForSharing())
            {
                // Show only those versions supported by the Panorama server.
                if (skylineVersion.CompareTo(maxSupportedVersion) <= 0)
                {
                    _skylineVersionOptions.Add(skylineVersion);
                    comboSkylineVersion.Items.Add(skylineVersion.ToString());
                }
            }
            comboSkylineVersion.SelectedIndex = 0;
            radioComplete.Checked = true;

            UpdateDataSourceSharing(allowDataSources);
        }

        public ShareType ShareType { get; private set; }

        public void OkDialog()
        {
            var auxiliaryFiles = GetIncludedAuxiliaryFiles().ToArray();
            var formatVersion = SelectedSkylineVersion?.SrmDocumentVersion ?? _savedFileFormat;
            if (formatVersion == null || formatVersion < DocumentFormat.SHARE_DATA_FOLDERS)
            {
                if (auxiliaryFiles.Any(Directory.Exists))
                {
                    MessageDlg.Show(this, Resources.ShareTypeDlg_OkDialog_Including_data_folders_is_not_supported_by_the_currently_selected_version_);
                    comboSkylineVersion.Focus();
                    return;
                }
            }
            ShareType = new ShareType(radioComplete.Checked, _skylineVersionOptions[comboSkylineVersion.SelectedIndex], auxiliaryFiles); // Pass in all the checked auxiliary files
            DialogResult = DialogResult.OK;
        }

        private void btnShare_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public SkylineVersion SelectedSkylineVersion
        {
            get
            {
                return _skylineVersionOptions[comboSkylineVersion.SelectedIndex];
            }
            set
            {
                int index = _skylineVersionOptions.IndexOf(value);
                if (index < 0)
                {
                    throw new ArgumentException();
                }

                comboSkylineVersion.SelectedIndex = index;
            }
        }

        public bool ShareTypeComplete
        {
            get
            {
                return radioComplete.Checked;
            }
            set
            {
                if (value)
                {
                    radioComplete.Checked = true;
                }
                else
                {
                    radioMinimal.Checked = true;
                }
            }
        }

        #region Functional testing support

        public bool IncludeReplicateFiles
        {
            get { return cbIncludeReplicateFiles.Checked; }
            set { cbIncludeReplicateFiles.Checked = value; }
        }

        public IList<string> GetAvailableVersionItems()
        {
            return comboSkylineVersion.Items.OfType<string>().ToList();
        }

        public void ShowSelectReplicatesDialog()
        {
            btnSelectReplicateFiles_Click(null, null);
        }

        public string FileStatusText => labelFileStatus.Text;

        #endregion

        /// <summary>
        /// Creates and collects user information on present raw files
        /// </summary>
        private void btnSelectReplicateFiles_Click(object sender, EventArgs e)
        {
            using (var replicateSelectDlg = new ShareResultsFilesDlg(_document, _documentFilePath, _auxiliaryFiles))
            {
                if (replicateSelectDlg.ShowDialog(this) == DialogResult.OK)
                {
                    // Pass along any extra files the user may have selected
                    _auxiliaryFiles = replicateSelectDlg.FilesInfo; // Pass auxiliary file information back
                    if (_auxiliaryFiles.IncludeFilesCount > 0)
                        labelFileStatus.Text = _auxiliaryFiles.ToString();
                    else
                        cbIncludeReplicateFiles.Checked = false;
                }
            }
        }

        /// <summary>
        /// Handle status updates based on <see cref="cbIncludeReplicateFiles"/> checked state.
        /// Also, creates a default <see cref="ShareResultsFilesDlg.AuxiliaryFiles"/> instance,
        /// based on what can be found from the <see cref="SrmDocument"/>
        /// </summary>
        private void cbIncludeFiles_CheckedChanged(object sender, EventArgs e)
        {
            bool includeFiles = cbIncludeReplicateFiles.Checked;
            btnSelectReplicateFiles.Enabled = includeFiles;
            if (includeFiles)
            {
                if (_auxiliaryFiles == null)
                {
                    // CONSIDER: Need LongWaitDlg support for this with the ability to cancel
                    // Leaving for now because testing with 200 files was fast enough
                    _auxiliaryFiles = new ShareResultsFilesDlg.AuxiliaryFiles(_document, _documentFilePath);
                }
                labelFileStatus.Text = _auxiliaryFiles.ToString();
            }
            else
            {
                var totalFileCount = _document.Settings.MeasuredResults?.Chromatograms
                    .SelectMany(c => c.MSDataFileInfos).Distinct().Count() ?? 0;
                labelFileStatus.Text = ShareResultsFilesDlg.AuxiliaryFiles.GetStatusText(0, totalFileCount, 0);
            }
        }

        /// <summary>
        /// Don't offer to include results files when there are none, or when document format is too old.
        /// </summary>
        private void UpdateDataSourceSharing(bool allowDataSources)
        {
            if (!allowDataSources) // e.g. Panorama uploads
            {
                // Don't offer to include results files for any formats
                cbIncludeReplicateFiles.Visible = cbIncludeReplicateFiles.Enabled = cbIncludeReplicateFiles.Checked =
                    btnSelectReplicateFiles.Visible = labelFileStatus.Visible = false;
            }
            else
            {
                // Don't offer to include results files when there are none
                cbIncludeReplicateFiles.Enabled = _document.Settings.HasResults;
            }
        }

        /// <summary>
        /// Get all selected files
        /// </summary>
        public IEnumerable<string> GetIncludedAuxiliaryFiles()
        { 
            if (cbIncludeReplicateFiles.Checked && _auxiliaryFiles != null)
                return _auxiliaryFiles.FilesToIncludeInZip;
            return Array.Empty<string>();
        }
    }
}
