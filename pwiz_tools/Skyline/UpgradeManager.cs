/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Deployment.Application;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline
{
    public sealed class UpgradeManager
    {
        private static bool _checkedAtStartup;

        public static IDeployment _appDeployment = new AppDeploymentWrapper();

        public static IDeployment AppDeployment
        {
            get { return _appDeployment; }
            set
            {
                _checkedAtStartup = false;
                _appDeployment = value;
            }
        }

        public static bool CheckAtStartup
        {
            get { return Settings.Default.UpdateCheckAtStartup; }
            set { Settings.Default.UpdateCheckAtStartup = value; }
        }

        private readonly Control _parentWindow;
        private readonly bool _startup;
        private readonly AutoResetEvent _endUpdateEvent;
        private UpdateCheckDetails _updateInfo;
        private UpdateCompletedDetails _completeArgs;

        public static void CheckForUpdateAsync(Control parentWindow, bool startup = true)
        {
            if (startup)
            {
                if (_checkedAtStartup)
                    return;
                _checkedAtStartup = true;
            }

            if (AppDeployment.IsNetworkDeployed && (CheckAtStartup || !startup))
            {
                new UpgradeManager(parentWindow, startup).BeginCheck();
            }
        }

        private UpgradeManager(Control parentWindow, bool startup)
        {
            _parentWindow = parentWindow;
            _startup = startup;
            _endUpdateEvent = new AutoResetEvent(false);
        }

        private Control ParentWindow
        {
            get { return FormUtil.FindTopLevelOpenForm() ?? _parentWindow; }
        }

        private void BeginCheck()
        {
            // Use backround worker instead of CheckForUpdateAsync to avoid
            // saving state about update availability
            var worker = new BackgroundWorker();
            worker.DoWork += updateCheck_DoWork;
            worker.RunWorkerCompleted += updateCheck_Complete;
            worker.RunWorkerAsync();
        }

        private void updateCheck_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                e.Result = AppDeployment.CheckForDetailedUpdate();
            }
            catch (Exception x)
            {
                e.Result = x;
            }
        }

        private void updateCheck_Complete(object sender, RunWorkerCompletedEventArgs e)
        {
            var exTrust = e.Result as TrustNotGrantedException;
            if (exTrust != null)
            {
                if (ShowUpgradeForm(AppDeployment.GetVersionFromUpdateLocation(), false, true))
                    AppDeployment.OpenInstallLink(ParentWindow);
                return;
            }
            var ex = e.Result as Exception;
            if (ex != null)
            {
                // Show an error message box to allow a user to inspect the exception stack trace
                MessageDlg.ShowWithException(ParentWindow,
                    Resources.UpgradeManager_updateCheck_Complete_Failed_attempting_to_check_for_an_upgrade_, ex);
                // Show no upgrade found message to allow a user to turn off or on this checking
                ShowUpgradeForm(null, false, false);
                return;
            }
            _updateInfo = e.Result as UpdateCheckDetails;
            if (_updateInfo != null && _updateInfo.UpdateAvailable)
            {
                if (!ShowUpgradeForm(_updateInfo.AvailableVersion, true, true))
                    return;

                using (var longWaitUpdate = new LongWaitDlg
                {
                    Text = string.Format(Resources.UpgradeManager_updateCheck_Complete_Upgrading__0_, Program.Name),
                    Message = GetProgressMessage(0, _updateInfo.UpdateSizeBytes ?? 0),
                    ProgressValue = 0
                })
                {
                    longWaitUpdate.PerformWork(ParentWindow, 500, broker =>
                    {
                        BeginUpdate(broker);
                        _endUpdateEvent.WaitOne();
                        _endUpdateEvent.Dispose();
                        broker.ProgressValue = 100;
                    });
                }
                if (_completeArgs == null || _completeArgs.Cancelled)
                    return;

                if (_completeArgs.Error != null)
                {
                    MessageDlg.ShowWithException(ParentWindow,
                        Resources.UpgradeManager_updateCheck_Complete_Failed_attempting_to_upgrade_, _completeArgs.Error);
                    if (ShowUpgradeForm(null, false, true))
                        AppDeployment.OpenInstallLink(ParentWindow);
                    return;
                }

                AppDeployment.Restart();
            }
            else if (!_startup)
            {
                ShowUpgradeForm(null, false, false);
            }
        }

        private void BeginUpdate(ILongWaitBroker broker)
        {
            AppDeployment.UpdateAsync(ev => update_ProgressChanged(ev, broker), update_Complete);
        }

        private bool ShowUpgradeForm(Version availableVersion, bool automatic, bool updateFound)
        {
            string versionText = availableVersion != null
                ? GetVersionDiff(AppDeployment.CurrentVersion, availableVersion)
                : null;

            using (var dlgUpgrade = new UpgradeDlg(versionText, automatic, updateFound) {Text = Program.Name})
            {
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
            if (versionCurrent.Major != versionAvailable.Major || versionCurrent.Minor != versionAvailable.Minor)
                return string.Format("{0}.{1}", versionAvailable.Major, versionAvailable.Minor); // Not L10N
            return versionAvailable.ToString();
        }

        private void update_ProgressChanged(UpdateProgress e, ILongWaitBroker broker)
        {
            if (broker.IsCanceled)
                AppDeployment.UpdateAsyncCancel();
            else
            {
                long updateBytes = Math.Max(_updateInfo.UpdateSizeBytes ?? 0, e.BytesTotal);
                broker.Message = GetProgressMessage(e.BytesCompleted, updateBytes);
                broker.ProgressValue = updateBytes == 0
                    ? 0 : (int)Math.Min(99, e.BytesCompleted * 100 / updateBytes);
            }
        }

        private string GetProgressMessage(long bytesCompleted, long totalBytes)
        {
            return string.Format(Resources.UpgradeManager_GetProgressMessage_Upgrading_to__0___downloading__1__of__2__, _updateInfo.AvailableVersion,
                new FileSize(bytesCompleted), new FileSize(totalBytes));
        }

        private void update_Complete(UpdateCompletedDetails e)
        {
            _completeArgs = e;
            _endUpdateEvent.Set();
        }

        public interface IDeployment
        {
            bool IsNetworkDeployed { get; }
            Version CurrentVersion { get; }

            UpdateCheckDetails CheckForDetailedUpdate();
            void UpdateAsync(Action<UpdateProgress> updateProgress, Action<UpdateCompletedDetails> updateComplete);
            void UpdateAsyncCancel();
            void Restart();

            Version GetVersionFromUpdateLocation();
            void OpenInstallLink(Control parentWindow);
        }

        public sealed class UpdateCheckDetails
        {
            public UpdateCheckDetails(bool updateAvailable, Version availableVersion, long? updateSizeBytes)
            {
                UpdateAvailable = updateAvailable;
                AvailableVersion = availableVersion;
                UpdateSizeBytes = updateSizeBytes;
            }

            public bool UpdateAvailable { get; private set; }
            public Version AvailableVersion { get; private set; }
            public long? UpdateSizeBytes { get; private set; }
        }

        public sealed class UpdateProgress
        {
            public UpdateProgress(long bytesCompleted, long bytesTotal)
            {
                BytesCompleted = bytesCompleted;
                BytesTotal = bytesTotal;
            }

            public long BytesCompleted { get; private set; }
            public long BytesTotal { get; private set; }
        }

        public sealed class UpdateCompletedDetails
        {
            public UpdateCompletedDetails(bool cancelled, Exception error)
            {
                Cancelled = cancelled;
                Error = error;
            }

            public bool Cancelled { get; private set; }
            public Exception Error { get; private set; }
        }

        private sealed class AppDeploymentWrapper : IDeployment
        {
            private readonly ApplicationDeployment _applicationDeployment;

            public AppDeploymentWrapper()
            {
                if (ApplicationDeployment.IsNetworkDeployed)
                    _applicationDeployment = ApplicationDeployment.CurrentDeployment;
            }

            public bool IsNetworkDeployed
            {
                get { return _applicationDeployment != null; }
            }

            public Version CurrentVersion { get { return _applicationDeployment.CurrentVersion; } }

            public UpdateCheckDetails CheckForDetailedUpdate()
            {
                // CONSIDER: Some way to set trust to get this working? Below did not work
                // https://stackoverflow.com/questions/14688282/clickonce-full-trust-app-update-failing-with-trustnotgrantedexception-on-windows
//                var appId = new ApplicationIdentity(_applicationDeployment.UpdatedApplicationFullName);
//                var unrestrictedPerms = new PermissionSet(PermissionState.Unrestricted);
//                var appTrust = new ApplicationTrust(appId)
//                {
//                    DefaultGrantSet = new PolicyStatement(unrestrictedPerms),
//                    IsApplicationTrustedToRun = true,
//                    Persist = true
//                };
//                ApplicationSecurityManager.UserApplicationTrusts.Add(appTrust);
                var info = _applicationDeployment.CheckForDetailedUpdate(false);

                // Accessing version and size properties throw if no update is available
                if (!info.UpdateAvailable)
                    return new UpdateCheckDetails(false, null, null);

                return new UpdateCheckDetails(info.UpdateAvailable, info.AvailableVersion, info.UpdateSizeBytes);
            }

            public void UpdateAsync(Action<UpdateProgress> updateProgress,
                                    Action<UpdateCompletedDetails> updateComplete)
            {
                _applicationDeployment.UpdateProgressChanged += (s, e) =>
                {
                    updateProgress(new UpdateProgress(e.BytesCompleted, e.BytesTotal));
                };
                _applicationDeployment.UpdateCompleted += (s, e) =>
                {
                    updateComplete(new UpdateCompletedDetails(e.Cancelled, e.Error));
                };
                _applicationDeployment.UpdateAsync();
            }

            public void UpdateAsyncCancel()
            {
                _applicationDeployment.UpdateAsyncCancel();
            }

            public void Restart()
            {
                Application.Restart();
            }

            public Version GetVersionFromUpdateLocation()
            {
                try
                {
                    var webClient = new WebClient();
                    string applicationPage = webClient.DownloadString(_applicationDeployment.UpdateLocation);
                    Match match = Regex.Match(applicationPage, "<assemblyIdentity .*version=\"([^\"]*)\"");  // Not L10N
                    if (match.Success)
                        return new Version(match.Groups[1].Value);
                }
                catch (Exception)
                {
                    // Fall through to returning null
                }
                return null;
            }

            public void OpenInstallLink(Control parentWindow)
            {
                bool is64 = Environment.Is64BitOperatingSystem;
                string shorNameInstall = Install.Type == Install.InstallType.release
                    ? (is64 ? "skyline64" : "skyline32") // Not L10N
                    : (is64 ? "skyline-daily64" : "skyline-daily32"); // Not L10N : Keep -daily

                WebHelpers.OpenSkylineShortLink(parentWindow, shorNameInstall);
            }
        }
    }
}
