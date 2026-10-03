/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Lets the user pick which other installed Skyline to take settings from. Only installed
    /// Skylines are offered: the settings file is tied to the Tools folder beside it, and an
    /// arbitrary user.config from somewhere on disk would name tools that are not there.
    /// </summary>
    public partial class ImportSettingsDlg : FormEx
    {
        public ImportSettingsDlg(IEnumerable<SkylineInstallation> installations)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            foreach (var installation in installations)
                listInstallations.Items.Add(installation);
            if (listInstallations.Items.Count > 0)
                listInstallations.SelectedIndex = 0;
            UpdateControls();
        }

        /// <summary>
        /// What to do once the user clicks OK, or null while the dialog is still open.
        /// </summary>
        public SettingsImporter Importer { get; private set; }

        public SkylineInstallation SelectedInstallation
        {
            get { return listInstallations.SelectedItem as SkylineInstallation; }
            set { listInstallations.SelectedItem = value; }
        }

        public bool UninstallSelected
        {
            get { return cbUninstall.Checked; }
            set { cbUninstall.Checked = value; }
        }

        public bool TrackChanges
        {
            get { return cbTrackChanges.Checked; }
            set { cbTrackChanges.Checked = value; }
        }

        public void OkDialog()
        {
            var installation = SelectedInstallation;
            if (installation == null)
            {
                MessageDlg.Show(this, ToolsUIResources.ImportSettingsDlg_OkDialog_Choose_the_installation_to_import_settings_from_);
                return;
            }
            bool uninstall = UninstallSelected && installation.CanUninstall;
            Importer = new SettingsImporter(installation.UserConfigFile)
            {
                // An installation that is going away hands its identity on.
                KeepInstallationId = !uninstall,
                TrackChanges = TrackChanges,
                UninstallCommand = uninstall ? installation.UninstallCommand : null
            };
            DialogResult = DialogResult.OK;
        }

        private void UpdateControls()
        {
            var installation = SelectedInstallation;
            bool canUninstall = installation != null && installation.CanUninstall;
            cbUninstall.Enabled = canUninstall;
            if (!canUninstall)
                cbUninstall.Checked = false;
            // Uninstalling the source is the end of keeping up with it.
            cbTrackChanges.Enabled = installation != null && !cbUninstall.Checked;
            if (!cbTrackChanges.Enabled)
                cbTrackChanges.Checked = false;
            btnOk.Enabled = installation != null;
        }

        private void listInstallations_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateControls();
        }

        private void cbUninstall_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControls();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        #region Functional testing support

        public IList<SkylineInstallation> Installations
        {
            get { return listInstallations.Items.Cast<SkylineInstallation>().ToList(); }
        }

        public bool UninstallEnabled
        {
            get { return cbUninstall.Enabled; }
        }

        public bool TrackChangesEnabled
        {
            get { return cbTrackChanges.Enabled; }
        }

        #endregion
    }
}
