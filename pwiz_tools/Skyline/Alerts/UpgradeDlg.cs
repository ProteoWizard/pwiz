/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public partial class UpgradeDlg : ModeUIInvariantFormEx // This dialog is neither proteomic nor small mol
    {
        private readonly string _defaultButtonText;

        public UpgradeDlg(string versionText, bool automatic, bool updateFound)
        {
            InitializeComponent();

            _defaultButtonText = btnLater.Text;
            VersionText = versionText;
            UpdateAutomatic = automatic;
            UpdateFound = updateFound;

            // Designer has problems with getting images from resources
            pictureSkyline.Image = Resources.SkylineImg;

            if (!updateFound)
            {
                labelRelease.Text = Resources.UpgradeDlg_UpgradeDlg_No_update_was_found_;
                labelDetail.Visible = labelDirections.Visible = linkReleaseNotes.Visible = false;
                btnLater.Visible = false;
                btnInstall.Text = MultiButtonMsgDlg.BUTTON_OK;
            }
            else
            {
                labelRelease.Text = string.Format(labelRelease.Text, Program.Name, versionText ?? string.Empty);
                if (automatic)
                    labelDetail.Text = labelDetailAutomatic.Text;
            }

            cbAtStartup.Checked = UpgradeManager.CheckAtStartup;
        }

        public string VersionText { get; private set; }
        public bool UpdateAutomatic { get; private set; }
        public bool UpdateFound { get; private set; }

        public bool CheckAtStartup
        {
            get { return cbAtStartup.Checked; }
            set { cbAtStartup.Checked = value; }
        }

        private void cbAtStartup_CheckedChanged(object sender, EventArgs e)
        {
            btnLater.Text = cbAtStartup.Checked ? _defaultButtonText : Resources.UpgradeDlg_cbAtStartup_CheckedChanged_Maybe__Later;

            // Make this change immediate and persistent.
            UpgradeManager.CheckAtStartup = cbAtStartup.Checked;
            Settings.Default.SaveWithoutExceptions();
        }

        private void linkReleaseNotes_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string notes = Install.Type == Install.InstallType.release ? @"notes" : @"notes-daily";
            WebHelpers.OpenSkylineShortLink(this, notes);
        }
    }
}
