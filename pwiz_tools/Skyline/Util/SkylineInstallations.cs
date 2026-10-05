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
using System.IO;
using System.Linq;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// The other installations of this Skyline whose settings a user could import: the same
    /// product, so that Skyline and Skyline-daily never take each other's settings, and no newer
    /// than this one, whose settings could hold what this version does not understand. Both kinds
    /// of installation are searched, ClickOnce and installer, since a machine can have either or
    /// both.
    /// </summary>
    public class SkylineInstallations
    {
        /// <summary>
        /// The product to look for, which is the name of its assembly: Skyline or Skyline-daily.
        /// </summary>
        public string ProductName { get; set; } = typeof(Program).Assembly.GetName().Name;

        /// <summary>
        /// The newest version offered. Null when the running version cannot be read, which
        /// leaves every version on offer.
        /// </summary>
        public Version CurrentVersion { get; set; } = ParseVersion(Install.BareVersion);

        /// <summary>
        /// The running program's own settings file, which is the one installation never worth
        /// offering. Defaults to where <see cref="UserConfigSettingsProvider"/> keeps it.
        /// </summary>
        public string OwnUserConfigFile { get; set; } = Path.Combine(
            UserConfigSettingsProvider.GetDefaultConfigFolder(), UserConfigSettingsProvider.CONFIG_FILE_NAME);

        /// <summary>
        /// The installations on offer, the ones Programs and Features lists first, then the
        /// newest first.
        /// </summary>
        public IEnumerable<SkylineInstallation> ListOtherInstallations()
        {
            return FindInstallations()
                .Where(installation => !IsOwnInstallation(installation) && IsNoNewerThanThis(installation))
                .OrderByDescending(installation => installation.IsCurrentlyInstalled)
                .ThenByDescending(installation => ParseVersion(installation.Version));
        }

        /// <summary>
        /// Whether the installation's version is known and no newer than this one's.
        /// </summary>
        private bool IsNoNewerThanThis(SkylineInstallation installation)
        {
            var version = ParseVersion(installation.Version);
            return version != null && (CurrentVersion == null || version <= CurrentVersion);
        }

        /// <summary>
        /// Every installation of <see cref="ProductName"/> with settings, this one included.
        /// Overridable so a test can say what is installed.
        /// </summary>
        protected virtual IEnumerable<SkylineInstallation> FindInstallations()
        {
            return new ClickOnceInstallations(ProductName).ListCandidates()
                .Concat(new RegisteredInstallations(ProductName).ListInstallations());
        }

        private bool IsOwnInstallation(SkylineInstallation installation)
        {
            return string.Equals(Path.GetFullPath(installation.UserConfigFile), Path.GetFullPath(OwnUserConfigFile),
                StringComparison.OrdinalIgnoreCase);
        }

        private static Version ParseVersion(string version)
        {
            return Version.TryParse(version, out var parsed) ? parsed : null;
        }
    }
}
