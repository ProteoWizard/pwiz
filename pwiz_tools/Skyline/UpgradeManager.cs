/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline
{
    /// <summary>
    /// Asks the <see cref="UpdateChecker"/> whether a newer Skyline has been published, once when
    /// the first window comes up and whenever the user asks from the Help menu, and offers to
    /// open the download when there is one.
    /// </summary>
    public sealed class UpgradeManager
    {
        private static bool _checkedAtStartup;
        private static UpdateChecker _checker;

        public static UpdateChecker Checker
        {
            get { return _checker ??= new UpdateChecker(); }
            set
            {
                _checkedAtStartup = false;
                _checker = value;
            }
        }

        public static bool CheckAtStartup
        {
            get { return Settings.Default.UpdateCheckAtStartup; }
            set { Settings.Default.UpdateCheckAtStartup = value; }
        }

        private readonly Control _parentWindow;
        private readonly bool _startup;

        public static void CheckForUpdateAsync(Control parentWindow, bool startup = true)
        {
            if (startup)
            {
                if (_checkedAtStartup)
                    return;
                _checkedAtStartup = true;
                if (!Checker.Enabled || !CheckAtStartup)
                    return;
            }
            new UpgradeManager(parentWindow, startup).BeginCheck();
        }

        private UpgradeManager(Control parentWindow, bool startup)
        {
            _parentWindow = parentWindow;
            _startup = startup;
        }

        private Control ParentWindow
        {
            get { return FormUtil.FindTopLevelOpenForm() ?? _parentWindow; }
        }

        private void BeginCheck()
        {
            var worker = new BackgroundWorker();
            worker.DoWork += updateCheck_DoWork;
            worker.RunWorkerCompleted += updateCheck_Complete;
            worker.RunWorkerAsync();
        }

        private void updateCheck_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                e.Result = Checker.CheckForNewerVersion();
            }
            catch (Exception x)
            {
                e.Result = x;
            }
        }

        private void updateCheck_Complete(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is Exception ex)
            {
                // Nobody asked for the startup check, and being offline is an ordinary way for
                // it to fail, so only a check the user requested reports the failure.
                if (_startup)
                {
                    Debug.WriteLine($@"Failed to check for an update: {ex.Message}");
                    return;
                }
                MessageDlg.ShowWithException(ParentWindow,
                    Resources.UpgradeManager_updateCheck_Complete_Failed_attempting_to_check_for_an_upgrade_, ex);
                // Show no upgrade found message to allow a user to turn off or on this checking
                ShowUpgradeForm(null, false);
                return;
            }
            var newerVersion = e.Result as Version;
            if (newerVersion != null)
            {
                if (ShowUpgradeForm(newerVersion, true))
                    Checker.OpenDownload(ParentWindow, newerVersion);
            }
            else if (!_startup)
            {
                ShowUpgradeForm(null, false);
            }
        }

        private bool ShowUpgradeForm(Version availableVersion, bool updateFound)
        {
            string versionText = availableVersion != null
                ? GetVersionDiff(Checker.CurrentVersion, availableVersion)
                : null;

            using (var dlgUpgrade = new UpgradeDlg(versionText, updateFound))
            {
                dlgUpgrade.Text = Program.Name;
                try
                {
                    return dlgUpgrade.ShowDialog(ParentWindow) == DialogResult.OK;
                }
                catch (ObjectDisposedException)
                {
                    try
                    {
                        return dlgUpgrade.ShowDialog(ParentWindow) == DialogResult.OK;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }
        }

        private static string GetVersionDiff(Version versionCurrent, Version versionAvailable)
        {
            // For Skyline versioning: Major.Minor.Build.Revision = YY.N.B.DDD
            // B=0 is release, B=1 is daily, B=9 is feature complete
            // Only show abbreviated version (YY.N) for actual releases (Build=0)
            // when the release number (YY.N) has changed
            bool majorUpgrade = versionCurrent == null ||
                                versionCurrent.Major != versionAvailable.Major ||
                                versionCurrent.Minor != versionAvailable.Minor;
            bool isRelease = versionAvailable.Build == 0;

            if (majorUpgrade && isRelease)
                return string.Format(@"{0}.{1}", versionAvailable.Major, versionAvailable.Minor);
            return versionAvailable.ToString();
        }
    }
}
