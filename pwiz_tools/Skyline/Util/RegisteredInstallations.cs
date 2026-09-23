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
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Finds the Skyline installations that an installer, rather than ClickOnce, put on the
    /// machine. Those keep their settings in a user.config beside the executable, and Programs
    /// and Features records where the executable is, so the registry's Uninstall entries are the
    /// whole search. See <see cref="ClickOnceInstallations"/> for the older kind.
    /// </summary>
    public class RegisteredInstallations
    {
        private const string UNINSTALL_KEY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string DISPLAY_VERSION = @"DisplayVersion";
        private const string INSTALL_LOCATION = @"InstallLocation";
        private const string UNINSTALL_STRING = @"UninstallString";
        private const string QUIET_UNINSTALL_STRING = @"QuietUninstallString";

        /// <summary>
        /// What one Uninstall registry entry says, which is all this class needs from the
        /// registry. A test hands these in directly.
        /// </summary>
        public class UninstallEntry
        {
            public string DisplayVersion { get; set; }
            public string InstallLocation { get; set; }
            public string UninstallCommand { get; set; }
        }

        public RegisteredInstallations(string productName)
        {
            ProductName = productName;
        }

        /// <summary>
        /// Name of the executable to look for, without its extension, for example
        /// "Skyline-daily". An Uninstall entry is only for this product when the folder it
        /// names holds that executable, which is a firmer test than the display name.
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// Every registered installation of the product that has settings to offer, in no
        /// particular order.
        /// </summary>
        public IEnumerable<SkylineInstallation> ListInstallations()
        {
            if (string.IsNullOrEmpty(ProductName))
                yield break;
            // The same installation is registered in more than one place when it is visible
            // from both registry views, and it should be offered once.
            var foldersSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in ReadUninstallEntries())
            {
                var folder = NormalizeFolder(entry.InstallLocation);
                if (folder == null || !foldersSeen.Add(folder))
                    continue;
                var executable = Path.Combine(folder, ProductName + @".exe");
                var userConfigFile = Path.Combine(folder, UserConfigSettingsProvider.CONFIG_FILE_NAME);
                if (!File.Exists(executable) || !File.Exists(userConfigFile))
                    continue;
                yield return new SkylineInstallation
                {
                    ProductName = ProductName,
                    Version = entry.DisplayVersion ?? ReadExecutableVersion(executable),
                    ExecutableFolder = folder,
                    UserConfigFile = userConfigFile,
                    IsCurrentlyInstalled = true,
                    UninstallCommand = entry.UninstallCommand
                };
            }
        }

        /// <summary>
        /// The Uninstall entries of every program on the machine, from both hives and both
        /// registry views, since an installer can register in any of them. Overridable so a test
        /// can say what is installed without touching the registry.
        /// </summary>
        protected virtual IEnumerable<UninstallEntry> ReadUninstallEntries()
        {
            var entries = new List<UninstallEntry>();
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            {
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                    ReadUninstallEntries(hive, view, entries);
            }
            return entries;
        }

        private static void ReadUninstallEntries(RegistryHive hive, RegistryView view, ICollection<UninstallEntry> entries)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (var uninstallKey = baseKey.OpenSubKey(UNINSTALL_KEY_PATH))
                {
                    if (uninstallKey == null)
                        return;
                    foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                    {
                        using (var subKey = uninstallKey.OpenSubKey(subKeyName))
                        {
                            if (subKey != null)
                                entries.Add(ReadUninstallEntry(subKey));
                        }
                    }
                }
            }
            catch (Exception)
            {
                // An unreadable part of the registry costs the caller nothing but the
                // installations registered there.
            }
        }

        private static UninstallEntry ReadUninstallEntry(RegistryKey subKey)
        {
            return new UninstallEntry
            {
                DisplayVersion = subKey.GetValue(DISPLAY_VERSION) as string,
                InstallLocation = subKey.GetValue(INSTALL_LOCATION) as string,
                // The interactive command, so that the user gets to confirm, and the quiet one
                // when that is all the installer registered.
                UninstallCommand = subKey.GetValue(UNINSTALL_STRING) as string ??
                                   subKey.GetValue(QUIET_UNINSTALL_STRING) as string
            };
        }

        private static string NormalizeFolder(string installLocation)
        {
            if (string.IsNullOrWhiteSpace(installLocation))
                return null;
            var folder = installLocation.Trim().Trim('"')
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return folder.Length == 0 ? null : folder;
        }

        private static string ReadExecutableVersion(string executable)
        {
            try
            {
                return FileVersionInfo.GetVersionInfo(executable).FileVersion;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
