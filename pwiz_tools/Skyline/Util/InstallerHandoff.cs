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
using System.Configuration;
using Microsoft.Win32;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// What this Skyline leaves for the installer-built Skyline that replaces it, before it
    /// starts that installer. The new Skyline, at its first start, finds the path to this one's
    /// user.config in the registry, imports the settings in it, carries on this installation's
    /// id, and uninstalls this one with the command recorded in those settings.
    ///
    /// The registry key and value name, and the UninstallCommand setting, are what the new
    /// Skyline's FirstLaunchImport reads; they must not change on either side alone.
    /// </summary>
    public class InstallerHandoff
    {
        /// <summary>
        /// The registry key, under HKEY_CURRENT_USER, of the handoff.
        /// </summary>
        public const string HANDOFF_KEY_PATH = @"Software\MacCossLabUW\ImportSettingsFrom";

        private const string UNINSTALL_KEY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string UNINSTALL_STRING = @"UninstallString";
        // ClickOnce uninstalls run through the deployment shim, as
        // "rundll32.exe dfshim.dll,ShArpMaintain Skyline-daily.application, Culture=neutral, ..."
        private const string CLICK_ONCE_UNINSTALL_HANDLER = @"dfshim.dll";

        /// <summary>
        /// The key holding the handoff. A test uses one of its own.
        /// </summary>
        public string HandoffKeyPath { get; set; } = HANDOFF_KEY_PATH;

        /// <summary>
        /// The product, which names both the handoff value and the ClickOnce deployment: the
        /// assembly name, Skyline or Skyline-daily.
        /// </summary>
        public string ProductName { get; set; } = typeof(Program).Assembly.GetName().Name;

        /// <summary>
        /// Saves the settings, with the command that uninstalls this installation among them,
        /// and records in the registry where they were saved.
        /// </summary>
        public void Record(string uninstallCommand)
        {
            Settings.Default.UninstallCommand = uninstallCommand ?? string.Empty;
            Settings.Default.Save();
            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal)
                .FilePath;
            using var key = Registry.CurrentUser.CreateSubKey(HandoffKeyPath);
            key?.SetValue(ProductName, configFile);
        }

        /// <summary>
        /// The command Programs and Features runs to uninstall this product's ClickOnce
        /// installation for the current user, or null when it lists none.
        /// </summary>
        public string FindClickOnceUninstallCommand()
        {
            try
            {
                using var uninstallKey = Registry.CurrentUser.OpenSubKey(UNINSTALL_KEY_PATH);
                if (uninstallKey == null)
                    return null;
                string deploymentName = ProductName + @".application";
                foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                {
                    using var subKey = uninstallKey.OpenSubKey(subKeyName);
                    if (subKey?.GetValue(UNINSTALL_STRING) is string uninstallString &&
                        uninstallString.IndexOf(CLICK_ONCE_UNINSTALL_HANDLER, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        uninstallString.IndexOf(@" " + deploymentName + @",", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return uninstallString;
                    }
                }
            }
            catch (Exception)
            {
                // An unreadable registry leaves the older installation for the user to remove.
            }
            return null;
        }
    }
}
