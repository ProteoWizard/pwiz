/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// The one install of Osprey that Skyline's two library predictions share: the Osprey Search
    /// wizard, and Build Library's AlphaPeptDeep source, which has Osprey predict the document's
    /// precursors. Osprey is Skyline's own tool - one zip, one pinned version, no licence - so the
    /// gate is a confirmation naming the download size and a progress dialog, not a dialog of its own.
    /// </summary>
    public static class OspreyInstaller
    {
        /// <summary>
        /// Returns true once Osprey is installed: already present (the pinned package in the Tools
        /// folder, or a registered search tool path), or downloaded now with the user's consent.
        /// </summary>
        public static bool EnsureInstalled(Control parent)
        {
            DropStaleRegistration();
            if (File.Exists(OspreyHelpers.OspreyBinary))
                return true;

            // The package this build installs: the pinned one, or for a daily or developer build the
            // last successful master build, which is a request to TeamCity (OspreyHelpers.UsesPinnedPackage)
            var package = default(FileDownloadInfo);
            try
            {
                using (var longWaitDlg = new LongWaitDlg(allowCancel: false))
                {
                    longWaitDlg.Message = SkylineResources.OspreyInstaller_EnsureInstalled_Looking_for_the_latest_Osprey_build;
                    longWaitDlg.PerformWork(parent, 500, () => package = OspreyHelpers.GetPackageDownloadInfo());
                }
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(parent, SkylineResources.SkylineWindow_EnsureOspreyInstalled_Failed_to_download_Osprey_, x);
                return false;
            }
            // Installed already with its registration lost: registered again rather than downloaded again
            if (File.Exists(package.ToolPath))
            {
                OspreyHelpers.RegisterPackage(package);
                return true;
            }

            var choice = MultiButtonMsgDlg.Show(parent,
                string.Format(SkylineResources.SkylineWindow_EnsureOspreyInstalled_Osprey__0__is_not_installed__Skyline_can_download_it__about__1__MB__to_its_Tools_folder_,
                    OspreyHelpers.GetPackageVersion(package), OspreyHelpers.OSPREY_DOWNLOAD_SIZE_MB),
                SkylineResources.SkylineWindow_EnsureOspreyInstalled_Download);
            if (choice != DialogResult.OK)
                return false;

            try
            {
                using (var longWaitDlg = new LongWaitDlg())
                {
                    longWaitDlg.Text = SkylineResources.SkylineWindow_EnsureOspreyInstalled_Downloading_Osprey;
                    longWaitDlg.PerformWork(parent, 500, broker =>
                        SimpleFileDownloader.DownloadRequiredFiles(new[] { package }, broker));
                }
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(parent, SkylineResources.SkylineWindow_EnsureOspreyInstalled_Failed_to_download_Osprey_, x);
                return false;
            }
            return File.Exists(OspreyHelpers.OspreyBinary);
        }

        /// <summary>
        /// Forgets a registered Osprey whose executable is gone: a developer build that moved, or a
        /// package deleted from the Tools folder. Left in place it would hide the pinned package,
        /// installed or downloadable; dropped, the pinned package takes over. A registration whose
        /// file exists is kept whatever its version, since a test or a developer registers a build
        /// of their own on purpose.
        /// </summary>
        private static void DropStaleRegistration()
        {
            var tools = Settings.Default.SearchToolList;
            if (!tools.ContainsKey(SearchToolType.Osprey))
                return;
            var registered = tools[SearchToolType.Osprey];
            if (!File.Exists(registered.Path))
                tools.Remove(registered);
        }
    }
}
