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
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Every Skyline on the machine, other than the running one, whose settings a user could
    /// import. Both products are searched, so that Skyline can take its settings from a
    /// Skyline-daily and the other way round, and both kinds of installation, ClickOnce and
    /// installer, since a machine can have either or both.
    /// </summary>
    public class SkylineInstallations
    {
        public static readonly IList<string> PRODUCT_NAMES = new[] { @"Skyline", @"Skyline-daily" };

        /// <summary>
        /// The running program's own settings file, which is the one installation never worth
        /// offering. Defaults to where <see cref="UserConfigSettingsProvider"/> keeps it.
        /// </summary>
        public string OwnUserConfigFile { get; set; } = Path.Combine(
            UserConfigSettingsProvider.GetDefaultConfigFolder(), UserConfigSettingsProvider.CONFIG_FILE_NAME);

        public IEnumerable<SkylineInstallation> ListOtherInstallations()
        {
            return PRODUCT_NAMES.SelectMany(ListInstallations)
                .Where(installation => !IsOwnInstallation(installation))
                .OrderBy(installation => installation.ProductName)
                .ThenByDescending(installation => installation.IsCurrentlyInstalled)
                .ThenByDescending(installation => installation.Version);
        }

        private static IEnumerable<SkylineInstallation> ListInstallations(string productName)
        {
            return new ClickOnceInstallations(productName).ListCandidates()
                .Concat(new RegisteredInstallations(productName).ListInstallations());
        }

        private bool IsOwnInstallation(SkylineInstallation installation)
        {
            return string.Equals(Path.GetFullPath(installation.UserConfigFile), Path.GetFullPath(OwnUserConfigFile),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
