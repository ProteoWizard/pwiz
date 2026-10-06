/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Gives a newly installed Skyline the settings of an older one, once, the first time it
    /// starts.
    ///
    /// An older Skyline that installs this one records the path to its own user.config in the
    /// registry before starting the installer: under HKEY_CURRENT_USER\<see cref="HANDOFF_KEY_PATH"/>,
    /// in a value named for the product, Skyline or Skyline-daily, so each takes only its own.
    /// When that value is there, its settings are imported without asking: this installation
    /// takes over the older one's installation id, and the older one is uninstalled with the
    /// command it recorded in that user.config (see <see cref="SettingsImporter.ReadUninstallCommand"/>).
    ///
    /// Otherwise, if older installations of this product are found, the user is offered their
    /// settings in <see cref="ImportSettingsDlg"/>. Either way, the check is not repeated.
    /// </summary>
    public class FirstLaunchImport
    {
        /// <summary>
        /// The registry key, under HKEY_CURRENT_USER, of the handoff. Not the key the installer
        /// records an installation under, which its uninstaller deletes.
        /// </summary>
        public const string HANDOFF_KEY_PATH = @"Software\MacCossLabUW\ImportSettingsFrom";

        /// <summary>
        /// The key holding the handoff. A test uses one of its own.
        /// </summary>
        public string HandoffKeyPath { get; set; } = HANDOFF_KEY_PATH;

        /// <summary>
        /// The name of the value holding the path to the older Skyline's user.config: the
        /// product's assembly name.
        /// </summary>
        public string HandoffValueName { get; set; } = typeof(Program).Assembly.GetName().Name;

        /// <summary>
        /// The installations to offer when there is no handoff. A test replaces this.
        /// </summary>
        public Func<IEnumerable<SkylineInstallation>> FindInstallations { get; set; } =
            () => new SkylineInstallations().ListOtherInstallations();

        /// <summary>
        /// How an uninstall command gets run. A test replaces this to see the command without
        /// running anything.
        /// </summary>
        public Action<string> RunUninstall { get; set; } = SettingsImporter.RunCommand;

        /// <summary>
        /// Imports from the older Skyline that installed this one, or offers the older ones
        /// found, unless that was done at an earlier start.
        /// </summary>
        public void Run(Control parent)
        {
            if (Settings.Default.CheckedForSettingsToImport)
                return;
            var handoffConfigFile = TakeHandoff();
            if (handoffConfigFile != null)
                ImportFromInstallingSkyline(parent, handoffConfigFile);
            else
                OfferOlderInstallations(parent);
            Settings.Default.CheckedForSettingsToImport = true;
            Settings.Default.Save();
        }

        private void ImportFromInstallingSkyline(Control parent, string configFile)
        {
            var importer = new SettingsImporter(configFile)
            {
                KeepInstallationId = false,
                UninstallCommand = SettingsImporter.ReadUninstallCommand(configFile),
                RunUninstall = RunUninstall
            };
            importer.Import(parent);
        }

        private void OfferOlderInstallations(Control parent)
        {
            var installations = FindInstallations().ToList();
            if (installations.Count == 0)
                return;
            SettingsImporter importer;
            using (var dlg = new ImportSettingsDlg(installations))
            {
                var result = parent == null ? dlg.ShowParentlessDialog() : dlg.ShowDialog(parent);
                if (result != DialogResult.OK)
                    return;
                importer = dlg.Importer;
            }
            importer.RunUninstall = RunUninstall;
            importer.Import(parent);
        }

        /// <summary>
        /// The user.config the installing Skyline named, or null when there is none or it is
        /// gone. The value is removed as it is read, so it is used once, and the key with it once
        /// it holds no other product's.
        /// </summary>
        private string TakeHandoff()
        {
            try
            {
                string configFile;
                bool keyEmpty;
                // Closed before the key itself can be deleted.
                using (var key = Registry.CurrentUser.OpenSubKey(HandoffKeyPath, true))
                {
                    if (key == null)
                        return null;
                    configFile = key.GetValue(HandoffValueName) as string;
                    key.DeleteValue(HandoffValueName, false);
                    keyEmpty = key.ValueCount == 0 && key.SubKeyCount == 0;
                }
                if (keyEmpty)
                    Registry.CurrentUser.DeleteSubKey(HandoffKeyPath, false);
                return !string.IsNullOrEmpty(configFile) && File.Exists(configFile) ? configFile : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
