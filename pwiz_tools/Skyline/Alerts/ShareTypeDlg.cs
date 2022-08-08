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
        private SrmDocument _document; // Global document from which replicate files and information can be extracted
        private List<string> _auxiliaryFiles; // List of extra files to add, usually mass spec data files
        public ShareTypeDlg(SrmDocument document, DocumentFormat? savedFileFormat): this(document, savedFileFormat, SkylineVersion.CURRENT)
        {
        }

        public ShareTypeDlg(SrmDocument document, DocumentFormat? savedFileFormat, SkylineVersion maxSupportedVersion)
        {
            InitializeComponent();
            _skylineVersionOptions = new List<SkylineVersion>();
            _document = document;

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
        }

        public ShareType ShareType { get; private set; }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;

            ShareType = new ShareType(radioComplete.Checked, _skylineVersionOptions[comboSkylineVersion.SelectedIndex], _auxiliaryFiles);

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

        // Added for functional tests
        public IList<string> GetAvailableVersionItems()
        {
            return comboSkylineVersion.Items.OfType<string>().ToList();
        }

        /// <summary>
        /// Creates and collects user information on present raw files
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Select_Rep_Files_Click(object sender, EventArgs e)
        {
            using (ShareResultsFilesDlg replicateSelectDlg = new ShareResultsFilesDlg(_document))
            {
                DialogResult result = replicateSelectDlg.ShowDialog();
                if (result == DialogResult.OK)
                {
                    // Pass along any extra files the user may have selected
                    _auxiliaryFiles = replicateSelectDlg.ReplicateFilesToInclude;

                }
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            Btn_SelectFiles.Enabled = checkBox.Checked;
        }
    }
}