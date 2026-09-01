/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Reflection;
using Microsoft.Win32;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Finds the settings left behind by a ClickOnce installed Skyline, so that a newly
    /// installed one can inherit them.
    ///
    /// Through version 26.1 user scoped settings lived in the per user, per version folder that
    /// LocalFileSettingsProvider makes up a name for:
    ///
    ///     %LOCALAPPDATA%\University_of_Washington\Skyline-daily.exe_Url_(hash)\(version)\user.config
    ///
    /// The hash covers the evidence the program was launched with, so every folder Skyline has
    /// ever run from has one of its own. A developer machine accumulates hundreds, and the
    /// newest is usually a developer build rather than an installation, so neither the highest
    /// version nor the most recently written file picks one out.
    ///
    /// The search is driven from the ClickOnce store instead:
    ///
    ///     %LOCALAPPDATA%\Apps\2.0\(random)\(random)\(installation folder)\Skyline-daily.exe
    ///
    /// Everything there is a real installation, which is the filter, and it also answers the
    /// question the settings folders cannot: where the executable was installed, and so where
    /// its Tools folder is. The executable's own file version then names the settings folder
    /// that goes with it.
    ///
    /// Programs and Features is read only to note which installation is the current one. It is
    /// not the search: it lists a single version, so it misses installations that are still on
    /// disk with settings worth inheriting.
    /// </summary>
    public class ClickOnceInstallations
    {
        private const string UNINSTALL_KEY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string DISPLAY_VERSION = @"DisplayVersion";
        private const string UNINSTALL_STRING = @"UninstallString";
        private const string DEPLOYMENT_MANIFEST_EXTENSION = @".application";
        // ClickOnce uninstalls run through the deployment shim, which is how its entry is told
        // apart from an ordinary installer's.
        private const string CLICK_ONCE_UNINSTALL_HANDLER = @"dfshim.dll";
        private const string CLICK_ONCE_STORE_FOLDER = @"Apps\2.0";

        /// <summary>
        /// One installation the settings could come from. Both halves are needed: the settings
        /// are in the user.config, and the external tools they name are under the Tools folder of
        /// the installation that wrote them, which is why the executable folder travels with it.
        /// </summary>
        public class Candidate
        {
            /// <summary>
            /// Version of the installed executable, which is also the name of the folder holding
            /// its user.config.
            /// </summary>
            public string Version { get; set; }

            /// <summary>
            /// Folder the installed executable is in, and so the folder its Tools folder is in.
            /// </summary>
            public string ExecutableFolder { get; set; }

            public string UserConfigFile { get; set; }

            /// <summary>
            /// Whether Programs and Features still lists this version. False for an installation
            /// that was uninstalled but left its folders behind, whose settings are older news
            /// than a listed one's, though not necessarily less complete.
            /// </summary>
            public bool IsCurrentlyInstalled { get; set; }

            public override string ToString()
            {
                return $@"{Version} {ExecutableFolder}";
            }
        }

        /// <summary>
        /// The deployment manifest name in a ClickOnce uninstall command, for example
        /// "Skyline-daily.application" out of:
        ///
        ///     rundll32.exe dfshim.dll,ShArpMaintain Skyline-daily.application, Culture=neutral, ...
        ///
        /// Null for anything that is not a ClickOnce uninstall. ClickOnce names the deployment
        /// after the assembly, which is what makes this the field to match on.
        /// </summary>
        public static string GetDeploymentName(string uninstallString)
        {
            if (uninstallString == null)
                return null;
            var parts = uninstallString.Split(',');
            if (parts.Length < 2 ||
                parts[0].IndexOf(CLICK_ONCE_UNINSTALL_HANDLER, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return null;
            }
            // The maintenance verb and the name are separated by a space, as in
            // "ShArpMaintain Skyline-daily.application".
            var maintenanceCommand = parts[1].Trim();
            int nameStart = maintenanceCommand.LastIndexOf(' ');
            return nameStart < 0 ? null : maintenanceCommand.Substring(nameStart + 1);
        }

        /// <param name="assembly">The product's own assembly, whose name is both what ClickOnce
        /// named its deployment after and what the old settings folder was named after. Pass
        /// typeof(Program).Assembly rather than the entry assembly: SkylineCmd.exe and
        /// Skyline-daily.exe start different entry assemblies but are the same product, and all
        /// of them should inherit that product's old settings.</param>
        public ClickOnceInstallations(Assembly assembly)
        {
            AssemblyName = assembly.GetName().Name;
            LocalApplicationDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        /// <summary>
        /// Name of the assembly to look for, for example "Skyline-daily". Both things this
        /// searches are named after it: the ClickOnce deployment, as
        /// "&lt;AssemblyName&gt;.application", and the old settings folder, as
        /// "&lt;AssemblyName&gt;.exe_(hash)".
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// Folder holding the per user settings folders, %LOCALAPPDATA%. Settable so a test can
        /// point at a tree of its own.
        /// </summary>
        public string LocalApplicationDataFolder { get; set; }

        /// <summary>
        /// Versions of this assembly that Programs and Features currently lists, used only to set
        /// <see cref="Candidate.IsCurrentlyInstalled"/>. Left null to read the registry; a test
        /// sets it to say what it wants found.
        /// </summary>
        public ICollection<string> InstalledVersions { get; set; }

        /// <summary>
        /// Every installation whose settings could be inherited, in no particular order. The
        /// caller chooses; see <see cref="Candidate"/> for what it has to choose on.
        ///
        /// An installation only counts when both halves are present, since one is no use without
        /// the other: a folder whose version was never run has no settings to take, and settings
        /// whose installation folder the store has since cleaned up have no Tools to go with them.
        /// </summary>
        public IEnumerable<Candidate> ListCandidates()
        {
            if (string.IsNullOrEmpty(AssemblyName) || string.IsNullOrEmpty(LocalApplicationDataFolder))
                yield break;
            if (!Directory.Exists(LocalApplicationDataFolder))
                yield break;

            var installedVersions = InstalledVersions ?? ReadInstalledClickOnceVersions();
            foreach (var executableFolder in EnumerateClickOnceFolders())
            {
                var version = ReadExecutableVersion(executableFolder);
                if (string.IsNullOrEmpty(version))
                    continue;
                var userConfigFile = FindUserConfigFile(version);
                if (userConfigFile == null)
                    continue;
                yield return new Candidate
                {
                    Version = version,
                    ExecutableFolder = executableFolder,
                    UserConfigFile = userConfigFile,
                    IsCurrentlyInstalled = installedVersions.Contains(version)
                };
            }
        }

        /// <summary>
        /// The installation folders ClickOnce has put this assembly in. Every one of them is by
        /// definition a ClickOnce installation, which is what keeps the ordinary run-from-a-folder
        /// copies out of the results.
        /// </summary>
        private IEnumerable<string> EnumerateClickOnceFolders()
        {
            var storeFolder = Path.Combine(LocalApplicationDataFolder, CLICK_ONCE_STORE_FOLDER);
            if (!Directory.Exists(storeFolder))
                yield break;
            var executableName = AssemblyName + @".exe";
            // The store nests as Apps\2.0\(random)\(random)\(installation folder), so the
            // executables sit exactly three levels down. Recursing the whole store instead would
            // walk every installed application's files, including the Data folders beside them.
            foreach (var firstLevel in SafeEnumerateDirectories(storeFolder))
            {
                foreach (var secondLevel in SafeEnumerateDirectories(firstLevel))
                {
                    foreach (var executableFolder in SafeEnumerateDirectories(secondLevel))
                    {
                        if (File.Exists(Path.Combine(executableFolder, executableName)))
                            yield return executableFolder;
                    }
                }
            }
        }

        /// <summary>
        /// The version of the installed executable, which is the link between an installation
        /// folder and the settings folder that goes with it. Overridable because a test cannot
        /// give a file it made up a version resource to read.
        /// </summary>
        protected virtual string ReadExecutableVersion(string executableFolder)
        {
            try
            {
                return FileVersionInfo.GetVersionInfo(Path.Combine(executableFolder, AssemblyName + @".exe"))
                    .FileVersion;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The user.config a given version of this assembly wrote, or null when it never wrote one.
        /// </summary>
        private string FindUserConfigFile(string version)
        {
            foreach (var settingsFolder in EnumerateSettingsFolders())
            {
                var configFile = Path.Combine(settingsFolder, version, UserConfigSettingsProvider.CONFIG_FILE_NAME);
                if (File.Exists(configFile))
                    return configFile;
            }
            return null;
        }

        /// <summary>
        /// Versions of this assembly that Programs and Features lists. ClickOnce registers per
        /// user, so only the current user's hive is worth reading. This is a hint and not the
        /// search itself: an installation that was removed from Programs and Features can still
        /// have both a store folder and settings, and is still worth offering to the caller.
        /// </summary>
        private ICollection<string> ReadInstalledClickOnceVersions()
        {
            var versions = new HashSet<string>();
            try
            {
                using (var uninstallKey = Registry.CurrentUser.OpenSubKey(UNINSTALL_KEY_PATH))
                {
                    if (uninstallKey == null)
                        return versions;
                    foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                    {
                        var version = ReadClickOnceVersion(uninstallKey, subKeyName);
                        if (version != null)
                            versions.Add(version);
                    }
                }
            }
            catch (Exception)
            {
                // An unreadable registry is not worth failing a start over. It costs the caller
                // nothing but the note of which installation is the current one.
            }
            return versions;
        }

        private string ReadClickOnceVersion(RegistryKey uninstallKey, string subKeyName)
        {
            using (var subKey = uninstallKey.OpenSubKey(subKeyName))
            {
                var uninstallString = subKey?.GetValue(UNINSTALL_STRING) as string;
                if (uninstallString == null)
                    return null;
                if (!string.Equals(AssemblyName + DEPLOYMENT_MANIFEST_EXTENSION,
                        GetDeploymentName(uninstallString), StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
                return subKey.GetValue(DISPLAY_VERSION) as string;
            }
        }

        private static IEnumerable<string> SafeEnumerateDirectories(string folder)
        {
            try
            {
                return Directory.EnumerateDirectories(folder);
            }
            catch (Exception)
            {
                return Array.Empty<string>();   // Unreadable folder; keep looking
            }
        }

        /// <summary>
        /// The settings folders belonging to this application, across every company folder. The
        /// company folder name is the old assembly's company attribute with its spaces replaced,
        /// which is not worth reproducing, so every folder under %LOCALAPPDATA% gets a look and
        /// only those named for this application can match.
        /// </summary>
        private IEnumerable<string> EnumerateSettingsFolders()
        {
            var pattern = AssemblyName + @".exe_*";
            foreach (var companyFolder in Directory.EnumerateDirectories(LocalApplicationDataFolder))
            {
                string[] settingsFolders;
                try
                {
                    settingsFolders = Directory.GetDirectories(companyFolder, pattern);
                }
                catch (Exception)
                {
                    continue;   // Unreadable folder under %LOCALAPPDATA%; keep looking
                }
                foreach (var settingsFolder in settingsFolders)
                    yield return settingsFolder;
            }
        }
    }
}
