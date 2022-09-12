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
    /// Use for a <see cref="MessageBox"/> substitute that can be
    /// detected and closed by automated functional tests.
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
            DialogResult = DialogResult.OK;
            ShareType = new ShareType(radioComplete.Checked, _skylineVersionOptions[comboSkylineVersion.SelectedIndex], GetCheckedAuxiliaryFiles()); // Pass in all the checked auxiliary files
            Close();
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
        /// Creates and collects user information on present raw files
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSelectReplicateFiles_Click(object sender, EventArgs e)
        {
            using (var replicateSelectDlg = new ShareResultsFilesDlg(_document, _auxiliaryFiles))
            {
                if (replicateSelectDlg.ShowDialog(this) == DialogResult.OK)
                {
                    // Pass along any extra files the user may have selected
                    _auxiliaryFiles = replicateSelectDlg._auxiliaryFiles; // Pass auxiliary file information back
                    labelFileStatus.Text = replicateSelectDlg.UpdateLabel();
                }
            }
        }

        /// <summary>
        /// Upon checking the box the current file information is saved and file status is displayed
        /// The check box also allows the user access to more specific file selection and the ability
        /// to locate missing files and add them to the zip. Should the check box be unchecked the
        /// file selection info will be removed and the user will no longer be able to select files
        /// unless they re-check the box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbIncludeFiles_CheckedChanged(object sender, EventArgs e)
        {
            using var replicateSelectDlg = new ShareResultsFilesDlg(_document, _auxiliaryFiles);
            labelFileStatus.Text = replicateSelectDlg.UpdateLabel(); // Update label
            replicateSelectDlg.OkDialog(); // Update file selection
            _auxiliaryFiles = replicateSelectDlg._auxiliaryFiles; // Update files

            btnSelectReplicateFiles.Enabled = cbIncludeReplicateFiles.Checked;
            labelFileStatus.Visible = cbIncludeReplicateFiles.Checked;
        }

        ///
        private void UpdateDataSourceSharing()
        {
            var formatVersion = SelectedSkylineVersion?.SrmDocumentVersion ?? _savedFileFormat;
            if (!_allowDataSources) // e.g. Panorama uploads
            {
                cbIncludeReplicateFiles.Visible = cbIncludeReplicateFiles.Enabled = cbIncludeReplicateFiles.Checked =
                    btnSelectReplicateFiles.Visible = labelFileStatus.Visible = false; // Don't offer to include results files for any formats
            }
            else if (formatVersion == null || formatVersion < DocumentFormat.SHARE_REPLICATE_FILES)
            {
                cbIncludeReplicateFiles.Enabled = cbIncludeReplicateFiles.Checked = false; // Don't offer to include results files for older save formats
            }
            else
            {
                cbIncludeReplicateFiles.Enabled = _document.Settings.HasResults; // Don't offer to include results files when there are none
            }
        }

        /// <summary>
        /// Get all selected files
        /// </summary>
        /// <returns></returns>
        private List<string> GetCheckedAuxiliaryFiles()
        { 
            var auxiliaryFiles = new List<string>(); 

            // Collect files to be selected
            if (cbIncludeReplicateFiles.Checked)
            {
                foreach (var checkedAuxFiles in _auxiliaryFiles._checkBoxFiles)
                {
                    if (checkedAuxFiles.CheckedState)
                    {
                        auxiliaryFiles.Add(checkedAuxFiles.Filename);
                    }
                }
            }
            return auxiliaryFiles;
        }

        private void comboSkylineVersion_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateDataSourceSharing(); // Only allow data sources to be included with modern formats
        }
    }
}
