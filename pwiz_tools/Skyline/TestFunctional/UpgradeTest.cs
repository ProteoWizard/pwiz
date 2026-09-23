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
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// The startup check finds a newer version, the user accepts, and the download opens.
    /// </summary>
    [TestClass]
    public class UpgradeBasicTest : AbstractFunctionalTest
    {
        private TestUpdateChecker _checker;

        [TestMethod]
        public void UpgradeBasicFunctionalTest()
        {
            using (_checker = new TestUpdateChecker())
            using (_checker.Publish(TestUpdateChecker.NEWER_VERSION))
            {
                RunFunctionalTest();
            }
        }

        protected override void InitializeSkylineSettings()
        {
            base.InitializeSkylineSettings();
            UpgradeManager.CheckAtStartup = true;
        }

        protected override void DoTest()
        {
            var upgradeDlg = WaitForOpenForm<UpgradeDlg>();
            AssertEx.IsTrue(upgradeDlg.UpdateFound);
            AssertEx.AreEqual(TestUpdateChecker.NEWER_VERSION.ToString(), upgradeDlg.VersionText);
            RunUI(() =>
            {
                // Changing check at startup should update immediately
                AssertEx.IsTrue(UpgradeManager.CheckAtStartup);
                AssertEx.IsTrue(upgradeDlg.CheckAtStartup);
                upgradeDlg.CheckAtStartup = false;
                AssertEx.IsFalse(UpgradeManager.CheckAtStartup);
            });
            OkDialog(upgradeDlg, upgradeDlg.AcceptButton.PerformClick);
            // The download opens on the UI thread as soon as the dialog closes, so a round
            // trip through that thread is enough to know it has happened.
            RunUI(() => AssertEx.AreEqual(1, _checker.DownloadsOpened));

            // The published version is this one: a manual check finds nothing.
            using (_checker.Publish(TestUpdateChecker.CURRENT_VERSION))
            {
                var noUpgradeDlg = ShowDialog<UpgradeDlg>(SkylineWindow.CheckForUpdate);
                AssertEx.IsFalse(noUpgradeDlg.UpdateFound);
                AssertEx.IsFalse(noUpgradeDlg.CheckAtStartup);
                OkDialog(noUpgradeDlg, noUpgradeDlg.AcceptButton.PerformClick);
            }
            RunUI(() => AssertEx.AreEqual(1, _checker.DownloadsOpened));
        }
    }

    /// <summary>
    /// Declining the startup offer opens nothing and is not repeated; a manual check offers it again.
    /// </summary>
    [TestClass]
    public class UpgradeCancelTest : AbstractFunctionalTest
    {
        private TestUpdateChecker _checker;

        [TestMethod]
        public void UpgradeCancelFunctionalTest()
        {
            using (_checker = new TestUpdateChecker())
            using (_checker.Publish(TestUpdateChecker.NEWER_VERSION))
            {
                RunFunctionalTest();
            }
        }

        protected override void InitializeSkylineSettings()
        {
            base.InitializeSkylineSettings();
            UpgradeManager.CheckAtStartup = true;
        }

        protected override void DoTest()
        {
            var upgradeDlg = WaitForOpenForm<UpgradeDlg>();
            OkDialog(upgradeDlg, upgradeDlg.CancelButton.PerformClick);
            RunUI(() => AssertEx.AreEqual(0, _checker.DownloadsOpened));

            // The startup check runs once per session, not once per window
            var startPage = ShowDialog<StartPage>(SkylineWindow.OpenStartPage);
            upgradeDlg = TryWaitForOpenForm<UpgradeDlg>(200);
            AssertEx.IsNull(upgradeDlg);
            OkDialog(startPage, startPage.Close);

            upgradeDlg = ShowDialog<UpgradeDlg>(SkylineWindow.CheckForUpdate);
            AssertEx.IsTrue(upgradeDlg.UpdateFound);
            AssertEx.AreEqual(TestUpdateChecker.NEWER_VERSION.ToString(), upgradeDlg.VersionText);
            OkDialog(upgradeDlg, upgradeDlg.AcceptButton.PerformClick);
            RunUI(() => AssertEx.AreEqual(1, _checker.DownloadsOpened));

            // A new release is announced by its release number alone
            using (_checker.Publish(TestUpdateChecker.NEWER_RELEASE_VERSION))
            {
                upgradeDlg = ShowDialog<UpgradeDlg>(SkylineWindow.CheckForUpdate);
                AssertEx.IsTrue(upgradeDlg.UpdateFound);
                AssertEx.AreEqual(TestUpdateChecker.NEWER_RELEASE_TEXT, upgradeDlg.VersionText);
                OkDialog(upgradeDlg, upgradeDlg.CancelButton.PerformClick);
            }
            RunUI(() => AssertEx.AreEqual(1, _checker.DownloadsOpened));
        }
    }

    /// <summary>
    /// A failed startup check is silent; a failed manual check says why and still offers the setting.
    /// </summary>
    [TestClass]
    public class UpgradeErrorsTest : AbstractFunctionalTest
    {
        private TestUpdateChecker _checker;

        [TestMethod]
        public void UpgradeErrorsFunctionalTest()
        {
            using (_checker = new TestUpdateChecker())
            using (HttpClientTestHelper.SimulateHttp404())
            {
                RunFunctionalTest();
            }
        }

        protected override void InitializeSkylineSettings()
        {
            base.InitializeSkylineSettings();
            UpgradeManager.CheckAtStartup = true;
        }

        protected override void DoTest()
        {
            AssertEx.IsNull(TryWaitForOpenForm<MessageDlg>(200));
            AssertEx.IsNull(TryWaitForOpenForm<UpgradeDlg>(200));

            // The manifest cannot be downloaded
            using (var helper = HttpClientTestHelper.SimulateHttp404())
            {
                var errorDlg = ShowDialog<MessageDlg>(SkylineWindow.CheckForUpdate);
                AssertEx.AreEqual(Skyline.Properties.Resources.UpgradeManager_updateCheck_Complete_Failed_attempting_to_check_for_an_upgrade_, errorDlg.Message);
                AssertEx.Contains(errorDlg.DetailMessage, helper.GetExpectedMessage(_checker.ManifestUri));
                RunDlg<UpgradeDlg>(errorDlg.OkDialog, noUpdateDlg =>
                {
                    AssertEx.IsFalse(noUpdateDlg.UpdateFound);
                    noUpdateDlg.AcceptButton.PerformClick();
                });
            }

            // The manifest is not what the installer build writes
            using (_checker.PublishManifest("not json"))
            {
                var errorDlg = ShowDialog<MessageDlg>(SkylineWindow.CheckForUpdate);
                AssertEx.AreEqual(Skyline.Properties.Resources.UpgradeManager_updateCheck_Complete_Failed_attempting_to_check_for_an_upgrade_, errorDlg.Message);
                AssertEx.Contains(errorDlg.DetailMessage, string.Format(
                    SkylineResources.UpdateChecker_DownloadPublishedVersion_The_update_information_at__0__could_not_be_read_,
                    _checker.ManifestUri));
                RunDlg<UpgradeDlg>(errorDlg.OkDialog, noUpdateDlg =>
                {
                    AssertEx.IsFalse(noUpdateDlg.UpdateFound);
                    noUpdateDlg.AcceptButton.PerformClick();
                });
            }
            RunUI(() => AssertEx.AreEqual(0, _checker.DownloadsOpened));
        }
    }

    /// <summary>
    /// The checker Skyline consults while this exists: a fixed current version, a download
    /// that is counted instead of opened, and manifests served by <see cref="HttpClientTestHelper"/>.
    /// </summary>
    internal class TestUpdateChecker : UpdateChecker, IDisposable
    {
        public const string INSTALL_URL = "https://skyline.example.org/software/Skyline-Setup.exe";
        public const string NEWER_RELEASE_TEXT = "3.7";
        public static readonly Version CURRENT_VERSION = new Version(3, 6, 1, 10171);
        public static readonly Version NEWER_VERSION = new Version(3, 6, 1, 10172);
        public static readonly Version NEWER_RELEASE_VERSION = new Version(3, 7, 0, 10173);

        public TestUpdateChecker()
        {
            Enabled = true;
            CurrentVersion = CURRENT_VERSION;
            InstallUrl = INSTALL_URL;
            UpgradeManager.Checker = this;
        }

        public int DownloadsOpened { get; private set; }

        public override void OpenDownload(IWin32Window parent)
        {
            DownloadsOpened++;
        }

        public HttpClientTestHelper Publish(Version version)
        {
            return PublishManifest("{ \"" + VERSION_PROPERTY + "\": \"" + version + "\" }");
        }

        /// <summary>
        /// Serves the manifest to every request until the helper is disposed. A fresh stream
        /// per request, because the download closes the one it read.
        /// </summary>
        public HttpClientTestHelper PublishManifest(string json)
        {
            var manifestUri = ManifestUri;
            return new HttpClientTestHelper(new HttpClientTestBehavior
            {
                ResponseFactory = uri => Equals(uri, manifestUri) ? new MemoryStream(Encoding.UTF8.GetBytes(json)) : null
            });
        }

        public void Dispose()
        {
            UpgradeManager.Checker = null;
        }
    }
}
