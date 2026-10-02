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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Keeps this program's settings in step with another settings file they were copied from:
    /// one imported with "keep up to date", or the administrator's user.config beside the
    /// executable of an installation the user does not own. A copy of the other file, the base,
    /// is kept beside this program's user.config, and a source that no longer matches it has
    /// changed since the copy was taken.
    /// </summary>
    public class ImportedSettingsUpdater
    {
        /// <summary>
        /// Name of the base copy of the administrator's user.config, kept apart from the one
        /// for an imported file since a user can have both.
        /// </summary>
        public const string SHARED_BASE_CONFIG_FILE_NAME = @"shared.base.user.config";

        /// <summary>
        /// Tracks the file the settings were imported from, if they were imported with "keep up
        /// to date".
        /// </summary>
        public ImportedSettingsUpdater()
        {
            SourcePath = Settings.Default.ImportedSettingsPath;
            BaseConfigFilePath = SettingsImporter.GetBaseConfigPath(Settings.Default.SettingsFilePath);
        }

        /// <summary>
        /// Tracks the administrator's user.config, or returns null when the user's settings are
        /// that file.
        /// </summary>
        public static ImportedSettingsUpdater ForSharedSettings()
        {
            var sharedConfigFile = UserConfigSettingsProvider.GetSharedConfigFile();
            if (sharedConfigFile == null)
                return null;
            return new ImportedSettingsUpdater
            {
                SourcePath = sharedConfigFile,
                BaseConfigFilePath = Path.Combine(UserConfigSettingsProvider.GetDefaultConfigFolder(),
                    SHARED_BASE_CONFIG_FILE_NAME)
            };
        }

        /// <summary>
        /// The file the settings follow, or empty when they follow none.
        /// </summary>
        public string SourcePath { get; set; }

        /// <summary>
        /// The copy of <see cref="SourcePath"/> as it was when last brought across.
        /// </summary>
        public string BaseConfigFilePath { get; set; }

        public bool IsTracking
        {
            get { return !string.IsNullOrEmpty(SourcePath); }
        }

        /// <summary>
        /// Whether the source file differs from the copy taken when it was last brought across.
        /// A missing source counts as unchanged, since there is nothing to bring. A missing copy
        /// counts as one holding only defaults, so that everything the source changed from those
        /// gets brought across.
        /// </summary>
        public bool HasSourceChanged()
        {
            if (!IsTracking || !File.Exists(SourcePath))
                return false;
            if (!File.Exists(BaseConfigFilePath))
                return true;
            return !File.ReadAllBytes(SourcePath).SequenceEqual(File.ReadAllBytes(BaseConfigFilePath));
        }

        public void UpdateIfChanged()
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
            var provider = settings.UserConfigProvider;
            provider.ConfigFolder = Path.GetDirectoryName(configFilePath);
            provider.ConfigFileName = Path.GetFileName(configFilePath);
            return settings;
        }
    }
}
