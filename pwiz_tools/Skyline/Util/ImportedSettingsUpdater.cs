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
    /// Keeps settings that were imported with "keep up to date" in step with the file they
    /// came from. <see cref="SettingsImporter"/> leaves a copy of the imported file beside
    /// user.config; a source file that no longer matches that copy has changed since the import.
    /// </summary>
    public class ImportedSettingsUpdater
    {
        public ImportedSettingsUpdater()
        {
            ConfigFilePath = Settings.Default.SettingsFilePath;
            SourcePath = Settings.Default.ImportedSettingsPath;
        }

        /// <summary>
        /// This program's own user.config, beside which the base copy sits.
        /// </summary>
        public string ConfigFilePath { get; set; }

        /// <summary>
        /// The file the settings were imported from, or empty when they were not imported with
        /// "keep up to date".
        /// </summary>
        public string SourcePath { get; set; }

        public string BaseConfigFilePath
        {
            get { return SettingsImporter.GetBaseConfigPath(ConfigFilePath); }
        }

        public bool IsTracking
        {
            get { return !string.IsNullOrEmpty(SourcePath); }
        }

        /// <summary>
        /// Whether the source file differs from the copy taken when it was imported. A source or
        /// copy that has gone missing counts as unchanged, since there is nothing to compare.
        /// </summary>
        public bool HasSourceChanged()
        {
            if (!IsTracking || !File.Exists(SourcePath) || !File.Exists(BaseConfigFilePath))
                return false;
            return !File.ReadAllBytes(SourcePath).SequenceEqual(File.ReadAllBytes(BaseConfigFilePath));
        }

        public void UpdateIfChanged()
        {
            if (!HasSourceChanged())
                return;
            MergeChanges();
        }

        private void MergeChanges()
        {
            // The settings that differ between the base copy and the source are the ones to bring
            // across, leaving alone whatever the user changed here since importing, and the base
            // copy is then refreshed. That three way merge is not written yet: a changed source
            // is noticed and left as it is, so the base copy still records what was imported.
        }
    }
}
