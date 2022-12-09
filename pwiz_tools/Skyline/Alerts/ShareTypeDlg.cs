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
        private readonly SrmDocument _document; // Document from which replicate files and information can be extracted
        private readonly DocumentFormat? _savedFileFormat;
        private readonly bool _allowDataSources; // For now at least we never offer to include data sources in Panorama uploads
        private ShareResultsFilesDlg.AuxiliaryFiles _auxiliaryFiles; // Auxiliary files to be included (e.g. replicate files)

        public ShareTypeDlg(SrmDocument document, DocumentFormat? savedFileFormat): this(document, savedFileFormat, SkylineVersion.CURRENT)
        {
        }

        public ShareTypeDlg(SrmDocument document, DocumentFormat? savedFileFormat, SkylineVersion maxSupportedVersion, bool allowDataSources = true)
        {
            InitializeComponent();
            _skylineVersionOptions = new List<SkylineVersion>();
            _document = document;
            _savedFileFormat = savedFileFormat;
            _allowDataSources = allowDataSources;

            if (!_allowDataSources)
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
            UpdateDataSourceSharing(); // Don't offer to include results files when there are none, or when document format is too old

        }

        public ShareType ShareType { get; private set; }

        public void OkDialog()
        {
            ShareType = new ShareType(radioComplete.Checked, _skylineVersionOptions[comboSkylineVersion.SelectedIndex], GetIncludedAuxiliaryFiles()); // Pass in all the checked auxiliary files
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

        #endregion

        /// <summary>
        /// Path on disk to the current document.
        /// CONSIDER: Pass in the document file path rather than relying on Program.MainWindow
        /// </summary>
        private string _documentFilePath => Program.MainWindow.DocumentFilePath;

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
                    if (_auxiliaryFiles.IncludeFiles.Any())
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
            if (includeFiles)
            {
                var formatVersion = SelectedSkylineVersion?.SrmDocumentVersion ?? _savedFileFormat;
                if (formatVersion == null || formatVersion < DocumentFormat.SHARE_REPLICATE_FILES)
                {
                    MessageDlg.Show(this, Resources.ShareTypeDlg_cbIncludeFiles_CheckedChanged_Including_results_files_is_not_supported_by_the_currently_selected_version_);
                    cbIncludeReplicateFiles.CheckedChanged -= cbIncludeFiles_CheckedChanged;    // Avoid calling this function again
                    cbIncludeReplicateFiles.Checked = false;
                    cbIncludeReplicateFiles.CheckedChanged += cbIncludeFiles_CheckedChanged;
                    return;
                }
            }
            btnSelectReplicateFiles.Enabled = includeFiles;
            if (includeFiles)
            {
                if (_auxiliaryFiles == null)
                {
                    // TODO: Need LongWaitDlg support for this with the ability to cancel
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

        private void UpdateDataSourceSharing()
        {
            if (!_allowDataSources) // e.g. Panorama uploads
            {
                cbIncludeReplicateFiles.Visible = cbIncludeReplicateFiles.Enabled = cbIncludeReplicateFiles.Checked =
                    btnSelectReplicateFiles.Visible = labelFileStatus.Visible = false; // Don't offer to include results files for any formats
            }
            else
            {
                cbIncludeReplicateFiles.Enabled = _document.Settings.HasResults; // Don't offer to include results files when there are none
            }
        }

        /// <summary>
        /// Get all selected files
        /// </summary>
        private IEnumerable<string> GetIncludedAuxiliaryFiles()
        { 
            if (cbIncludeReplicateFiles.Checked && _auxiliaryFiles != null)
                return _auxiliaryFiles.IncludeFiles;
            return Array.Empty<string>();
        }
    }
}
