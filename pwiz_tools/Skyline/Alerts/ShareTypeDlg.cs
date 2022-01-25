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
        public ShareTypeDlg(SrmDocument document, DocumentFormat? savedFileFormat): this(document, savedFileFormat, SkylineVersion.CURRENT)
        {
        }

        public ShareTypeDlg(SrmDocument document, DocumentFormat? savedFileFormat, SkylineVersion maxSupportedVersion)
        {
            InitializeComponent();
            _skylineVersionOptions = new List<SkylineVersion>();
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
            ShareType = new ShareType(radioComplete.Checked, _skylineVersionOptions[comboSkylineVersion.SelectedIndex]);
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
    }
}
