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

using System.IO;
using System.Linq;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Brings an administrator's changes to the shared settings of an installation to an ordinary
    /// user of it. When Skyline is installed for all users, the administrator's user.config sits
    /// beside the executable and each other user keeps settings of their own, so external tools
    /// the administrator installs, and other settings they change, would never reach anyone else.
    /// At startup this merges whatever changed in the shared file into the user's own settings,
    /// keeping the user's own changes.
    ///
    /// A copy of the shared file, the base, is kept beside the user's user.config. A shared file
    /// that no longer matches it has changed since the last merge.
    /// </summary>
    public class SharedSettingsMerger
    {
        /// <summary>
        /// Name of the base copy of the administrator's user.config.
        /// </summary>
        public const string SHARED_BASE_CONFIG_FILE_NAME = @"shared.base.user.config";

        /// <summary>
        /// Follows the administrator's user.config, or returns null when the user's settings are
        /// that file.
        /// </summary>
        public static SharedSettingsMerger ForSharedSettings()
        {
            var sharedConfigFile = PortableSettingsProvider.GetSharedConfigFile();
            if (sharedConfigFile == null)
                return null;
            return new SharedSettingsMerger
            {
                SourcePath = sharedConfigFile,
                BaseConfigFilePath = Path.Combine(PortableSettingsProvider.GetDefaultConfigFolder(),
                    SHARED_BASE_CONFIG_FILE_NAME)
            };
        }

        /// <summary>
        /// The administrator's user.config.
        /// </summary>
        public string SourcePath { get; set; }

        /// <summary>
        /// The copy of <see cref="SourcePath"/> as it was when last brought across.
        /// </summary>
        public string BaseConfigFilePath { get; set; }

        /// <summary>
        /// Whether the source file differs from the copy taken when it was last brought across.
        /// A missing source counts as unchanged, since there is nothing to bring. A missing copy
        /// counts as one holding only defaults, so that everything the source changed from those
        /// gets brought across.
        /// </summary>
        public bool HasSourceChanged()
        {
            if (!File.Exists(SourcePath))
                return false;
            if (!File.Exists(BaseConfigFilePath))
                return true;
            return !File.ReadAllBytes(SourcePath).SequenceEqual(File.ReadAllBytes(BaseConfigFilePath));
        }

        public void MergeIfChanged()
        {
            if (!HasSourceChanged())
                return;
            MergeChanges();
        }

        /// <summary>
        /// Brings across the settings that differ between the base copy and the source, leaving
        /// alone whatever the user changed here since, and then refreshes the base copy.
        /// </summary>
        private void MergeChanges()
        {
            var settings = Settings.Default;
            settings.MergeChanges(ReadSettings(BaseConfigFilePath), ReadSettings(SourcePath));
            settings.Save();
            File.Copy(SourcePath, BaseConfigFilePath, true);
        }

        /// <summary>
        /// Settings read from a file other than this program's own, which a file that does not
        /// exist leaves at their defaults.
        /// </summary>
        private static Settings ReadSettings(string configFilePath)
        {
            var settings = new Settings();
            settings.PortableProvider.ConfigFilePath = configFilePath;
            return settings;
        }
    }
}
