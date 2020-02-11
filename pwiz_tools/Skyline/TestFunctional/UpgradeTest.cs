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
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Startup;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class UpgradeBasicTest : AbstractFunctionalTest
    {
        private TestDeployment _deployment;

        internal static TestDeployment CreateDeployment()
        {
            var testDeployment = new TestDeployment
            {
                IsNetworkDeployed = true,
                CurrentVersion = new Version(3, 6, 1, 10171),
                UpdateVersion = new Version(3, 6, 1, 10172),
                UpdateBytes = 34 * 1024 * 1024,
                UpdateDurationMillis = 1500 // Show LongWaitDlg
            };
            UpgradeManager.AppDeployment = testDeployment;
            return testDeployment;
        }

        [TestMethod]
        public void UpgradeBasicFunctionalTest()
        {
            _deployment = CreateDeployment();

            RunFunctionalTest();
        }

        protected override void InitializeSkylineSettings()
        {
            base.InitializeSkylineSettings();
            UpgradeManager.CheckAtStartup = true;
        }

        protected override void DoTest()
        {
            var upgradeDlg = WaitForOpenForm<UpgradeDlg>();
            Assert.IsTrue(upgradeDlg.UpdateFound && upgradeDlg.UpdateAutomatic);
            Assert.AreEqual(_deployment.UpdateVersion.ToString(), upgradeDlg.VersionText);
            RunUI(() =>
            {
                // Changing check at startup should update immediately
                Assert.IsTrue(UpgradeManager.CheckAtStartup);
                Assert.IsTrue(upgradeDlg.CheckAtStartup);
                upgradeDlg.CheckAtStartup = false;
                Assert.IsFalse(UpgradeManager.CheckAtStartup);
            });
            OkDialog(upgradeDlg, upgradeDlg.AcceptButton.PerformClick);
            var longWaitDlg = WaitForOpenForm<LongWaitDlg>();
            OkDialog(longWaitDlg, () => { });   // Do nothing and the long wait should go away.
            WaitForCondition(() => Equals(_deployment.CurrentVersion, _deployment.UpdateVersion));
            var noUpgradeDlg = ShowDialog<UpgradeDlg>(SkylineWindow.CheckForUpdate);
            Assert.IsFalse(noUpgradeDlg.UpdateFound);
            Assert.IsFalse(noUpgradeDlg.CheckAtStartup);
            OkDialog(upgradeDlg, noUpgradeDlg.AcceptButton.PerformClick);
        }
    }

    [TestClass]
    public class UpgradeCancelTest : AbstractFunctionalTest
    {
        private TestDeployment _deployment;

        [TestMethod]
        public void UpgradeCancelFunctionalTest()
        {
            _deployment = UpgradeBasicTest.CreateDeployment();

            RunFunctionalTest();
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
            var longWaitDlg = TryWaitForOpenForm<LongWaitDlg>(800);
            Assert.IsNull(longWaitDlg);
            var startPage = ShowDialog<StartPage>(SkylineWindow.OpenStartPage);
            upgradeDlg = TryWaitForOpenForm<UpgradeDlg>(200);
            Assert.IsNull(upgradeDlg);
            OkDialog(startPage, startPage.Close);
            upgradeDlg = ShowDialog<UpgradeDlg>(SkylineWindow.CheckForUpdate);
            Assert.IsTrue(upgradeDlg.UpdateFound);
            OkDialog(upgradeDlg, upgradeDlg.AcceptButton.PerformClick);
            longWaitDlg = WaitForOpenForm<LongWaitDlg>();
            OkDialog(longWaitDlg, () => { });   // Do nothing and the long wait should go away.
            WaitForCondition(() => Equals(_deployment.CurrentVersion, _deployment.UpdateVersion));

            _deployment.UpdateVersion = new Version(3, 7, 1, 10173);
            upgradeDlg = ShowDialog<UpgradeDlg>(SkylineWindow.CheckForUpdate);
            Assert.IsTrue(upgradeDlg.UpdateFound);
            Assert.AreEqual("3.7", upgradeDlg.VersionText);
            OkDialog(upgradeDlg, upgradeDlg.AcceptButton.PerformClick);
            longWaitDlg = WaitForOpenForm<LongWaitDlg>();
            OkDialog(longWaitDlg, longWaitDlg.CancelDialog);
            Assert.IsFalse(TryWaitForCondition(100, () => Equals(_deployment.CurrentVersion, _deployment.UpdateVersion)));
        }
    }

    [TestClass]
    public class UpgradeErrorsTest : AbstractFunctionalTest
    {
        private TestDeployment _deployment;

        [TestMethod]
        public void UpgradeErrorsFunctionalTest()
        {
            _deployment = UpgradeBasicTest.CreateDeployment();

            RunFunctionalTest();
        }

        protected override void InitializeSkylineSettings()
        {
            base.InitializeSkylineSettings();
            UpgradeManager.CheckAtStartup = false;
        }

        private const string errorText = "Update error text";

        protected override void DoTest()
        {
            // Trust exception
            var upgradeDlg = TryWaitForOpenForm<UpgradeDlg>(200);
            Assert.IsNull(upgradeDlg);
            _deployment.UpdateCheckError = new TrustNotGrantedException();
            upgradeDlg = ShowDialog<UpgradeDlg>(SkylineWindow.CheckForUpdate);
            Assert.IsTrue(upgradeDlg.UpdateFound);
            Assert.IsFalse(upgradeDlg.UpdateAutomatic);
            Assert.AreEqual(_deployment.UpdateVersion.ToString(), upgradeDlg.VersionText);
            RunDlg<MessageDlg>(upgradeDlg.AcceptButton.PerformClick, dlg =>
            {
                Assert.AreEqual(TestDeployment.INSTALL_LINK_TEXT, dlg.Message);
                dlg.OkDialog();
            });

            // Any other exception
            _deployment.UpdateCheckError = new Exception(errorText);
            var errorDlg = ShowDialog<MessageDlg>(SkylineWindow.CheckForUpdate);
            Assert.AreEqual(Skyline.Properties.Resources.UpgradeManager_updateCheck_Complete_Failed_attempting_to_check_for_an_upgrade_, errorDlg.Message);
            Assert.AreEqual(_deployment.UpdateCheckError.ToString(), errorDlg.DetailMessage);
            RunDlg<UpgradeDlg>(errorDlg.OkDialog, noUpdateDlg =>
            {
                Assert.IsFalse(noUpdateDlg.UpdateFound);
                noUpdateDlg.AcceptButton.PerformClick();
            });

            // Exception during update
            _deployment.UpdateError = _deployment.UpdateCheckError;
            _deployment.UpdateCheckError = null;
            upgradeDlg = ShowDialog<UpgradeDlg>(SkylineWindow.CheckForUpdate);
            Assert.IsTrue(upgradeDlg.UpdateFound && upgradeDlg.UpdateAutomatic);
            Assert.AreEqual(_deployment.UpdateVersion.ToString(), upgradeDlg.VersionText);
            RunDlg<MessageDlg>(upgradeDlg.AcceptButton.PerformClick, dlg =>
            {
                Assert.AreEqual(Skyline.Properties.Resources.UpgradeManager_updateCheck_Complete_Failed_attempting_to_upgrade_, dlg.Message);
                Assert.AreEqual(_deployment.UpdateError.ToString(), errorDlg.DetailMessage);
                dlg.OkDialog();
            });
            upgradeDlg = WaitForOpenForm<UpgradeDlg>();
            Assert.IsTrue(upgradeDlg.UpdateFound);
            Assert.IsFalse(upgradeDlg.UpdateAutomatic);
            Assert.IsNull(upgradeDlg.VersionText);
            RunDlg<MessageDlg>(upgradeDlg.AcceptButton.PerformClick, dlg =>
            {
                Assert.AreEqual(TestDeployment.INSTALL_LINK_TEXT, dlg.Message);
                dlg.OkDialog();
            });
        }
    }

    internal class TestDeployment : UpgradeManager.IDeployment
    {
        public const string INSTALL_LINK_TEXT = "Install link";

        private bool _isCanceled;
        private Action<UpgradeManager.UpdateProgress> _progress;
        private Action<UpgradeManager.UpdateCompletedDetails> _completed;

        public bool IsNetworkDeployed { get; set; }
        public Version CurrentVersion { get; set; }
        public Version UpdateVersion { get; set; }
        public long UpdateBytes { get; set; }
        public int UpdateDurationMillis { get; set; }
        public Exception UpdateCheckError { get; set; }
        public Exception UpdateError { get; set; }

        public UpgradeManager.UpdateCheckDetails CheckForDetailedUpdate()
        {
            if (UpdateCheckError != null)
                throw UpdateCheckError;

            return new UpgradeManager.UpdateCheckDetails(!Equals(CurrentVersion, UpdateVersion), UpdateVersion, UpdateBytes);
        }

        public void UpdateAsync(Action<UpgradeManager.UpdateProgress> updateProgress,
                                Action<UpgradeManager.UpdateCompletedDetails> updateComplete)
        {
            _progress = updateProgress;
            _completed = updateComplete;

            var worker = new BackgroundWorker();
            worker.DoWork += update_DoWork;
            worker.RunWorkerCompleted += update_Complete;
            worker.RunWorkerAsync();
        }

        private void update_DoWork(object sender, DoWorkEventArgs e)
        {
            const int cycles = 10;
            long totalBytes = UpdateBytes;
            for (int i = 0; i < cycles; i++)
            {
                if (_isCanceled || UpdateError != null)
                    return;
                Thread.Sleep(UpdateDurationMillis / cycles);
                _progress(new UpgradeManager.UpdateProgress((i + 1) * totalBytes / cycles, totalBytes));
            }
            CurrentVersion = UpdateVersion;
        }

        private void update_Complete(object sender, RunWorkerCompletedEventArgs e)
        {
            _completed(new UpgradeManager.UpdateCompletedDetails(_isCanceled, UpdateError));
        }

        public void UpdateAsyncCancel()
        {
            _isCanceled = true;
        }

        public void Restart()
        {
            // Do nothing
        }

        public Version GetVersionFromUpdateLocation()
        {
            return UpdateVersion;
        }

        public void OpenInstallLink(Control parentWindow)
        {
            MessageDlg.Show(parentWindow, INSTALL_LINK_TEXT);
        }
    }
}
