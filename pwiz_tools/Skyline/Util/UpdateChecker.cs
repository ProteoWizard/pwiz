/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Finds out whether a newer Skyline than this one has been published. The InstallUrl
    /// application setting, with the product name (Skyline or Skyline-daily) substituted for
    /// its {0}, is where this Skyline was installed from and where a newer one comes from. It
    /// has no extension: with .json appended it is the manifest the installer build writes,
    /// <code>{ "version": "26.1.1.260" }</code>, and with -version.exe appended it is that
    /// version's installer, so the installer is published under the name the build gave it.
    /// </summary>
    public class UpdateChecker
    {
        public const string MANIFEST_EXTENSION = ".json";
        public const string INSTALLER_EXTENSION = ".exe";
        public const string VERSION_PROPERTY = "version";

        public UpdateChecker()
        {
            Version.TryParse(Install.BareVersion, out var currentVersion);
            CurrentVersion = currentVersion;
            // Only a Skyline that the installer put where it runs from is offered the published
            // one; a build output or a copied folder has no installation to upgrade.
            string exeFolder = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            Enabled = currentVersion != null && new RegisteredInstallations(Program.Name).IsInstallationFolder(exeFolder);
            InstallUrl = string.Format(Settings.Default.InstallUrl, Program.Name);
        }

        /// <summary>
        /// Whether the startup check runs. A check from the Help menu always does.
        /// </summary>
        public bool Enabled { get; set; }

        public Version CurrentVersion { get; set; }

        public string InstallUrl { get; set; }

        public Uri ManifestUri
        {
            get { return new Uri(InstallUrl + MANIFEST_EXTENSION); }
        }

        public Uri GetInstallerUri(Version version)
        {
            return new Uri(InstallUrl + @"-" + version + INSTALLER_EXTENSION);
        }

        /// <summary>
        /// The published version when it is newer than this one, otherwise null.
        /// </summary>
        public Version CheckForNewerVersion()
        {
            var publishedVersion = DownloadPublishedVersion();
            if (CurrentVersion == null || publishedVersion.CompareTo(CurrentVersion) > 0)
                return publishedVersion;
            return null;
        }

        public Version DownloadPublishedVersion()
        {
            var manifestUri = ManifestUri;
            string json;
            using (var client = new HttpClientWithProgress())
            {
                json = client.DownloadString(manifestUri);
            }
            try
            {
                return Version.Parse(JObject.Parse(json).Value<string>(VERSION_PROPERTY));
            }
            catch (Exception x)
            {
                throw new InvalidDataException(string.Format(
                    SkylineResources.UpdateChecker_DownloadPublishedVersion_The_update_information_at__0__could_not_be_read_,
                    manifestUri), x);
            }
        }

        /// <summary>
        /// Opens the download of the given version's installer in the browser.
        /// </summary>
        public virtual void OpenDownload(IWin32Window parent, Version version)
        {
            WebHelpers.OpenLink(parent, GetInstallerUri(version).ToString());
        }
    }
}
