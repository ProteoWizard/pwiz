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

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// One installed Skyline whose settings could be imported. Both halves are needed: the
    /// settings are in the user.config, and the external tools they name are under the Tools
    /// folder of the installation that wrote them, which is why the executable folder travels
    /// with it.
    /// </summary>
    public class SkylineInstallation
    {
        /// <summary>
        /// Name of the product's assembly, "Skyline" or "Skyline-daily", which is also the name
        /// of its executable.
        /// </summary>
        public string ProductName { get; set; }

        public string Version { get; set; }

        /// <summary>
        /// Folder the installed executable is in, and so the folder its Tools folder is in.
        /// </summary>
        public string ExecutableFolder { get; set; }

        public string UserConfigFile { get; set; }

        /// <summary>
        /// Whether Programs and Features still lists this installation. False for one that was
        /// uninstalled but left its folders behind, whose settings are older news than a listed
        /// one's, though not necessarily less complete.
        /// </summary>
        public bool IsCurrentlyInstalled { get; set; }

        /// <summary>
        /// The command Programs and Features would run to uninstall this installation, or null
        /// when it is not listed there and so cannot be uninstalled.
        /// </summary>
        public string UninstallCommand { get; set; }

        public bool CanUninstall
        {
            get { return !string.IsNullOrEmpty(UninstallCommand); }
        }

        /// <summary>
        /// What the user sees in a list of installations. Product, version and folder are all
        /// data rather than text to translate.
        /// </summary>
        public override string ToString()
        {
            return $@"{ProductName} {Version} ({ExecutableFolder})";
        }
    }
}
