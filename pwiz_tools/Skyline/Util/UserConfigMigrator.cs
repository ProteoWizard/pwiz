/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Copies the previous version's user.config forward when the .NET settings upgrade
    /// cannot find it.
    ///
    /// On .NET Framework, ConfigurationManager knows about ClickOnce and keeps user.config in
    /// the deployment's ClickOnce data directory, which ClickOnce itself copies from the
    /// previous version on the first run after an update. On .NET 8 that awareness is gone and
    /// user.config lands in
    ///     %LOCALAPPDATA%\[company]\[product]_Url_[hash]\[assembly version]\user.config
    /// where the hash is derived from the directory the application was installed into. A
    /// ClickOnce update installs into a new directory, so the hash AND the version both change,
    /// while Settings.Default.Upgrade() only scans sibling version folders under the SAME hash.
    /// It therefore finds nothing, and every update starts with default settings.
    ///
    /// Two places can hold the settings of the version being upgraded from:
    ///   1. This deployment's own ClickOnce data directory, which ClickOnce migrated forward
    ///      from the previous version. This is where a .NET Framework install's settings are.
    ///   2. A sibling [product]_Url_[other hash] folder written by a previous .NET 8 version.
    /// The highest version below the running one wins, which is what Upgrade() would have done.
    ///
    /// Only a ClickOnce-installed application migrates. A build started from its own output
    /// directory keeps its own settings rather than inheriting an installed Skyline's.
    /// </summary>
    public class UserConfigMigrator
    {
        private const string USER_CONFIG_FILE = @"user.config";
        private const string CLICKONCE_DATA_FOLDER = @"Data";
        private const string CLICKONCE_STORE_FOLDER = @"Apps\2.0";

        public UserConfigMigrator()
        {
            SectionName = typeof(Settings).FullName;
            ApplicationDirectory = AppContext.BaseDirectory;
            ClickOnceStoreRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                CLICKONCE_STORE_FOLDER);
            try
            {
                CurrentConfigPath = ConfigurationManager
                    .OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
            }
            catch (Exception e)
            {
                Error = e;
            }
        }

        /// <summary>
        /// Full path of the user.config the running version will read.
        /// </summary>
        public string CurrentConfigPath { get; set; }

        /// <summary>
        /// Directory the running application was loaded from.
        /// </summary>
        public string ApplicationDirectory { get; set; }

        /// <summary>
        /// Root of the per-user ClickOnce store, normally %LOCALAPPDATA%\Apps\2.0.
        /// </summary>
        public string ClickOnceStoreRoot { get; set; }

        /// <summary>
        /// Name of the settings section a candidate file must contain to be usable.
        /// </summary>
        public string SectionName { get; set; }

        /// <summary>
        /// The file <see cref="Migrate"/> copied, or null when nothing was migrated.
        /// </summary>
        public string SourceConfigPath { get; private set; }

        /// <summary>
        /// Whatever went wrong, for diagnosis. Migration must never keep Skyline from starting,
        /// so a failure leaves the application on default settings instead of throwing.
        /// </summary>
        public Exception Error { get; private set; }

        /// <summary>
        /// Copies the newest older user.config into place, if the running version does not have
        /// one yet. Returns true when a file was copied.
        /// </summary>
        public bool Migrate()
        {
            try
            {
                if (string.IsNullOrEmpty(CurrentConfigPath) || File.Exists(CurrentConfigPath))
                    return false;
                // Gate BOTH sources, not just the ClickOnce one: a build started from its own
                // output directory must keep its own settings rather than inherit an installed
                // Skyline sibling settings folder, which lives under the same company directory.
                if (GetClickOnceStoreFolderName() == null)
                    return false;
                string previous = FindPreviousConfigPath();
                if (previous == null)
                    return false;
                string directory = Path.GetDirectoryName(CurrentConfigPath);
                if (directory == null)
                    return false;
                Directory.CreateDirectory(directory);
                File.Copy(previous, CurrentConfigPath);
                SourceConfigPath = previous;
                return true;
            }
            catch (Exception e)
            {
                Error = e;
                return false;
            }
        }

        /// <summary>
        /// The user.config of the highest version below the running one, or null when there is
        /// no usable older file.
        /// </summary>
        public string FindPreviousConfigPath()
        {
            var currentVersion = GetVersion(Path.GetDirectoryName(CurrentConfigPath));
            if (currentVersion == null)
                return null;
            var candidates = new List<Tuple<Version, DateTime, string>>();
            foreach (string file in ClickOnceDataConfigs().Concat(SiblingProductConfigs()))
            {
                var version = GetVersion(Path.GetDirectoryName(file));
                if (version == null || version >= currentVersion)
                    continue;
                if (!ContainsSettingsSection(file))
                    continue;
                candidates.Add(new Tuple<Version, DateTime, string>(
                    version, File.GetLastWriteTimeUtc(file), file));
            }
            return candidates.OrderByDescending(c => c.Item1)
                .ThenByDescending(c => c.Item2)
                .Select(c => c.Item3)
                .FirstOrDefault();
        }

        /// <summary>
        /// user.config files in this deployment's own ClickOnce data directory. ClickOnce copies
        /// that directory forward from the previous version on the first run after an update,
        /// which is how a .NET Framework install's settings reach a .NET 8 one.
        /// </summary>
        private IEnumerable<string> ClickOnceDataConfigs()
        {
            string storeFolderName = GetClickOnceStoreFolderName();
            if (storeFolderName == null)
                return new string[0];
            // The data tree and the application tree share the deployment folder name but sit
            // under differently named random parents, so walk those two levels rather than
            // guessing at them.
            var files = new List<string>();
            string dataRoot = Path.Combine(ClickOnceStoreRoot, CLICKONCE_DATA_FOLDER);
            foreach (string outer in SafeEnumerateDirectories(dataRoot))
            {
                foreach (string inner in SafeEnumerateDirectories(outer))
                {
                    string dataDirectory = Path.Combine(inner, storeFolderName, CLICKONCE_DATA_FOLDER);
                    if (Directory.Exists(dataDirectory))
                        files.AddRange(VersionFolderConfigs(dataDirectory));
                }
            }
            return files;
        }

        /// <summary>
        /// user.config files written by other .NET 8 versions of this same application. They are
        /// in sibling folders rather than sibling version subfolders because the folder name
        /// hashes the install directory, which every ClickOnce version changes.
        /// </summary>
        private IEnumerable<string> SiblingProductConfigs()
        {
            string productDirectory = Path.GetDirectoryName(Path.GetDirectoryName(CurrentConfigPath));
            string companyDirectory = Path.GetDirectoryName(productDirectory);
            if (companyDirectory == null)
                return new string[0];
            string productName = GetProductName(Path.GetFileName(productDirectory));
            if (productName == null)
                return new string[0];
            var files = new List<string>();
            foreach (string directory in SafeEnumerateDirectories(companyDirectory))
            {
                if (!Equals(GetProductName(Path.GetFileName(directory)), productName))
                    continue;
                files.AddRange(VersionFolderConfigs(directory));
            }
            return files;
        }

        /// <summary>
        /// Name of this deployment's folder in the ClickOnce store, or null when the application
        /// was not started from the store and so must not inherit an installed app's settings.
        /// </summary>
        private string GetClickOnceStoreFolderName()
        {
            if (string.IsNullOrEmpty(ApplicationDirectory) || string.IsNullOrEmpty(ClickOnceStoreRoot))
                return null;
            string directory = Path.GetFullPath(ApplicationDirectory).TrimEnd(Path.DirectorySeparatorChar);
            string root = Path.GetFullPath(ClickOnceStoreRoot).TrimEnd(Path.DirectorySeparatorChar);
            if (!directory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return null;
            return Path.GetFileName(directory);
        }

        /// <summary>
        /// Every user.config one level below the given directory, in folders named for a version.
        /// </summary>
        private IEnumerable<string> VersionFolderConfigs(string parentDirectory)
        {
            var files = new List<string>();
            foreach (string directory in SafeEnumerateDirectories(parentDirectory))
            {
                string file = Path.Combine(directory, USER_CONFIG_FILE);
                if (File.Exists(file))
                    files.Add(file);
            }
            return files;
        }

        /// <summary>
        /// True when the file really is one of this application's settings files. Guards against
        /// copying an unrelated or truncated user.config over the top of the defaults.
        /// </summary>
        private bool ContainsSettingsSection(string path)
        {
            try
            {
                using (var reader = XmlReader.Create(path))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && Equals(reader.Name, SectionName))
                            return true;
                    }
                }
            }
            catch (Exception)
            {
                // A file that will not parse is simply not a usable source
            }
            return false;
        }

        /// <summary>
        /// The application name from a settings folder named "[name]_[evidence]_[hash]", e.g.
        /// "Skyline-daily" from "Skyline-daily_Url_ch4sk0htgwpz2pau3ozx5hl5zfuzgo4q". The name
        /// itself may contain underscores, so the two trailing parts are removed rather than
        /// splitting on the first underscore.
        /// </summary>
        private static string GetProductName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
                return null;
            int hash = folderName.LastIndexOf('_');
            if (hash <= 0)
                return null;
            int evidence = folderName.LastIndexOf('_', hash - 1);
            if (evidence <= 0)
                return null;
            return folderName.Substring(0, evidence);
        }

        private static Version GetVersion(string directory)
        {
            Version version;
            return Version.TryParse(Path.GetFileName(directory ?? string.Empty), out version) ? version : null;
        }

        private static IEnumerable<string> SafeEnumerateDirectories(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                    return Directory.EnumerateDirectories(directory).ToArray();
            }
            catch (Exception)
            {
                // A directory that cannot be read just contributes no candidates
            }
            return new string[0];
        }
    }
}
