/*
 * Original author: Trevor Killeen <killeent .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for the R & R-package installer
    /// </summary>
    [TestClass]
    public class RInstallerTest : AbstractFunctionalTest
    {
        
        [TestMethod]
        public void TestRInstaller()
        {
            RunFunctionalTest();
        }

        private static bool Installed { get; set; }
        private static ICollection<string> Packages { get; set; }
        private static readonly ProgramPathContainer PPC = new ProgramPathContainer(R, R_VERSION);

        private const string R = "R";
        private const string R_VERSION = "2.15.2";
        private const string PACKAGE_1 = "http://www.test.com/package1.zip";
        private const string PACKAGE_2 = "http://www.test.com/package2.tar.gz";
        private const string PACKAGE_3 = "C:\\localpackage1.zip";
        private const string PACKAGE_4 = "C:\\localpackage2.tar.gz";

        protected override void DoTest()
        {
            TestDlgLoad();
            TestProperPopulation();
            TestInstallR();
            TestInstallPackages();
            TestStartToFinish();
        }

        // Tests that the form loads with the proper display based on whether R is installed or not, as
        // well as if there are packages to download
        private static void TestDlgLoad()
        {
            TestDlgLoadBoth();
            TestDlgLoadOnlyR();
            TestDlgLoadOnlyPackages();
        }

        // R is not installed, and there are packages to install
        private static void TestDlgLoadBoth()
        {
            Installed = false;
            Packages = new Collection<string> { PACKAGE_1, PACKAGE_2, PACKAGE_3, PACKAGE_4 };
            var rInstaller = ShowDialog<RInstaller>(() => InstallProgram(PPC, Packages, Installed));
            WaitForConditionUI(10 * 1000, () => rInstaller.IsLoaded);
            Assert.AreEqual(string.Format(Resources.RInstaller_RInstaller_Load_This_tool_requires_the_use_of_R__0__and_the_following_packages__Select_packages_to_install_and_then_click_Install_to_begin_the_installation_process_, PPC.ProgramVersion), rInstaller.Message);
            OkDialog(rInstaller, rInstaller.CancelButton.PerformClick);
            Assert.AreEqual(DialogResult.Cancel, rInstaller.DialogResult);
        }

        // R is installed, and there are packages to install
        private static void TestDlgLoadOnlyR()
        {
            Installed = true;
            Packages = new Collection<string> { PACKAGE_1, PACKAGE_2, PACKAGE_3, PACKAGE_4 };
            var rInstaller = ShowDialog<RInstaller>(() => InstallProgram(PPC, Packages, Installed));
            WaitForConditionUI(10 * 1000, () => rInstaller.IsLoaded);
            Assert.AreEqual(Resources.RInstaller_RInstaller_Load_This_tool_requires_the_use_of_the_following_R_Packages__Select_packages_to_install_and_then_click_Install_to_begin_the_installation_process_, rInstaller.Message);
            OkDialog(rInstaller, rInstaller.CancelButton.PerformClick);
            Assert.AreEqual(DialogResult.Cancel, rInstaller.DialogResult);
        }

        // R is not installed, and there are no packages to install
        private static void TestDlgLoadOnlyPackages()
        {
            Installed = false;
            Packages = new Collection<string>();
            var rInstaller = ShowDialog<RInstaller>(() => InstallProgram(PPC, Packages, Installed));
            WaitForConditionUI(10 * 1000, () => rInstaller.IsLoaded);
            Assert.AreEqual(string.Format(Resources.RInstaller_RInstaller_Load_This_tool_requires_the_use_of_R__0___Click_Install_to_begin_the_installation_process_, PPC.ProgramVersion), rInstaller.Message);
            OkDialog(rInstaller, rInstaller.CancelButton.PerformClick);
            Assert.AreEqual(DialogResult.Cancel, rInstaller.DialogResult);
        }

        // Tests that the form properly populates the checkbox
        private static void TestProperPopulation()
        {
            Packages = new Collection<string> { PACKAGE_1, PACKAGE_2, PACKAGE_3, PACKAGE_4 };
            var rInstaller = ShowDialog<RInstaller>(() => InstallProgram(PPC, Packages, Installed));
            WaitForConditionUI(10 * 1000, () => rInstaller.IsLoaded);
            Assert.AreEqual(Packages.Count, rInstaller.PackagesListCount);

            // ensure that packages default to checked upon load
            Assert.AreEqual(Packages.Count, rInstaller.PackagesListCheckedCount);
            OkDialog(rInstaller, rInstaller.CancelButton.PerformClick);
        }

        private static void TestInstallR()
        {
            TestRDownloadAndInstallSuccess();
            TestRDownloadSuccessInstallFailure();
            TestRDownloadFailure();
            TestRDownloadCancel();
        }

        // Note: When testing the DialogResult of the rInstaller, it is important to perform to OKDialog of the 
        // message dialog first. Otherwise the DialogResult may not be set

        // Test cancelling the R download
        private static void TestRDownloadCancel()
        {
            var rInstaller = FormatRInstaller(true, false, false);
            var messageDlg = ShowDialog<MessageDlg>(() => rInstaller.AcceptButton.PerformClick());
            Assert.AreEqual(Resources.MultiFileAsynchronousDownloadClient_DownloadFileAsyncWithBroker_Download_canceled_, messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            Assert.AreEqual(DialogResult.No, rInstaller.DialogResult);
        }
        
        // Test R download success && install success
        private static void TestRDownloadAndInstallSuccess()
        {
            var rInstaller = FormatRInstaller(false, true, true);
            var messageDlg = ShowDialog<MessageDlg>(() => rInstaller.AcceptButton.PerformClick());
            Assert.AreEqual(Resources.RInstaller_GetR_R_installation_complete_, messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            Assert.AreEqual(DialogResult.Yes, rInstaller.DialogResult);
        }

        // Test R download success && install failure
        private static void TestRDownloadSuccessInstallFailure()
        {
            var rInstaller = FormatRInstaller(false, true, false);
            var messageDlg = ShowDialog<MessageDlg>(() => rInstaller.AcceptButton.PerformClick());
            Assert.AreEqual(Resources.RInstaller_InstallR_R_installation_was_not_completed__Cancelling_tool_installation_, messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            Assert.AreEqual(DialogResult.No, rInstaller.DialogResult);
        }

        // Test R download failure
        private static void TestRDownloadFailure()
        {
            var rInstaller = FormatRInstaller(false, false, false);
            var messageDlg = ShowDialog<MessageDlg>(() => rInstaller.AcceptButton.PerformClick()); 
            Assert.AreEqual(TextUtil.LineSeparate(Resources.RInstaller_DownloadR_Download_failed_, Resources.RInstaller_DownloadPackages_Check_your_network_connection_or_contact_the_tool_provider_for_installation_support_), messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            Assert.AreEqual(DialogResult.No, rInstaller.DialogResult);
        }

        // helper method for setting up the R installer form to support a number of possible installation outcomes
        private static RInstaller FormatRInstaller(bool cancelDownload, bool downloadSuccess, bool installSuccess)
        {
            Installed = false;
            Packages = new Collection<string>();
            var rInstaller = ShowDialog<RInstaller>(() => InstallProgram(PPC, Packages, Installed));
            WaitForConditionUI(10*1000, () => rInstaller.IsLoaded);
            RunUI(() =>
                {
                    rInstaller.TestDownloadClient = new TestAsynchronousDownloadClient {DownloadSuccess = downloadSuccess, CancelDownload = cancelDownload};
                    rInstaller.TestProcessRunner = new TestProcessRunner {ExitCode = installSuccess ? 0 : 1};
                });
            return rInstaller;
        }

        private static void TestInstallPackages()
        {
            TestNoPackagesDownload();
            TestPackageDownloadCancel();
            TestPackageDownloadFailure();
            TestPackageConnectFailure();
            TestPackageInstallFailure();
            TestPackageInstallSuccess();
        }

        // Tests that the tool will install if the user unselects all the packages to install
        private static void TestNoPackagesDownload()
        {
            var rInstaller = FormatPackageInstaller(false, false, false, false);
            WaitForConditionUI(10 * 1000, () => rInstaller.IsLoaded);
            RunUI(rInstaller.UncheckAllPackages);
            OkDialog(rInstaller, rInstaller.AcceptButton.PerformClick);
            Assert.AreEqual(DialogResult.Yes, rInstaller.DialogResult);
        }

        // Test for canceling the package download
        private static void TestPackageDownloadCancel()
        {
            var rInstaller = FormatPackageInstaller(true, false, false, false);
            var messageDlg = ShowDialog<MessageDlg>(() => rInstaller.AcceptButton.PerformClick());
            Assert.AreEqual(Resources.MultiFileAsynchronousDownloadClient_DownloadFileAsyncWithBroker_Download_canceled_, messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            Assert.AreEqual(DialogResult.No, rInstaller.DialogResult);
        }

        // Test for package download failure
        private static void TestPackageDownloadFailure()
        {
            var rInstaller = FormatPackageInstaller(false, false, false, false);
            var messageDlg = ShowDialog<MessageDlg>(() => rInstaller.AcceptButton.PerformClick()); 
            Assert.AreEqual(TextUtil.LineSeparate(
                            Resources.RInstaller_DownloadPackages_Failed_to_download_the_following_packages_,
                            string.Empty,
                            TextUtil.LineSeparate(new List<string>{PACKAGE_1,PACKAGE_2}),
                            string.Empty,
                            Resources
                                .RInstaller_DownloadPackages_Check_your_network_connection_or_contact_the_tool_provider_for_installation_support_), messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            Assert.AreEqual(DialogResult.No, rInstaller.DialogResult);
        }

        // Test for failure to connect to the named pipe
        private static void TestPackageConnectFailure()
        {
            var rInstaller = FormatPackageInstaller(false, true, false, false);
            var messageDlg = ShowDialog<MessageDlg>(() => rInstaller.AcceptButton.PerformClick()); 
            Assert.AreEqual(Resources.RInstaller_InstallPackages_Unknown_error_installing_packages_, messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            Assert.AreEqual(DialogResult.No, rInstaller.DialogResult);
        }

        // Test for package install failure
        private static void TestPackageInstallFailure()
        {
            var rInstaller = FormatPackageInstaller(false, true, true, false);
            var messageDlg = ShowDialog<MessageDlg>(() => rInstaller.AcceptButton.PerformClick());
            Assert.AreEqual(Resources.RInstaller_InstallPackages_Package_installation_failed__Error_log_output_in_immediate_window_, messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            Assert.AreEqual(DialogResult.No, rInstaller.DialogResult);
        }

        // Test package install success
        private static void TestPackageInstallSuccess()
        {
            var rInstaller = FormatPackageInstaller(false, true, true, true);
            var messageDlg = ShowDialog<MessageDlg>(() => rInstaller.AcceptButton.PerformClick());
            Assert.AreEqual(Resources.RInstaller_GetPackages_Package_installation_complete_, messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            Assert.AreEqual(DialogResult.Yes, rInstaller.DialogResult);
        }

        // helper method for setting up the R installer form to support a number of possible package installation outcomes
        private static RInstaller FormatPackageInstaller(bool cancelDownload, bool downloadSuccess, bool connectSuccess, bool installSuccess)
        {
            Installed = true;
            Packages = new Collection<string> {PACKAGE_1, PACKAGE_2, PACKAGE_3, PACKAGE_4};
            var rInstaller = ShowDialog<RInstaller>(() => InstallProgram(PPC, Packages, Installed));
            WaitForConditionUI(10 * 1000, () => rInstaller.IsLoaded);
            RunUI(() =>
            {
                rInstaller.TestDownloadClient = new TestAsynchronousDownloadClient { DownloadSuccess = downloadSuccess, CancelDownload = cancelDownload};
                rInstaller.TestConnectionSuccess = connectSuccess;
                rInstaller.TestNamedPipeProcessRunner = new TestNamedPipeProcessRunner {ConnectSuccess = connectSuccess, ExitCode = installSuccess ? 0 : 1};
                rInstaller.TestProgramPath = string.Empty;
            });
            return rInstaller;
        }

        // Tests the start to finish process of installing both R and associated packages
        private static void TestStartToFinish()
        {
            Installed = false;
            Packages = new Collection<string> {PACKAGE_1, PACKAGE_2, PACKAGE_3, PACKAGE_4};
            var rInstaller = ShowDialog<RInstaller>(() => InstallProgram(PPC, Packages, Installed));
            WaitForConditionUI(10*1000, () => rInstaller.IsLoaded);
            RunUI(() =>
                {
                    rInstaller.TestNamedPipeProcessRunner = new TestNamedPipeProcessRunner
                        {
                            ConnectSuccess = true,
                            ExitCode = 0
                        };
                    rInstaller.TestConnectionSuccess = true;
                    rInstaller.TestDownloadClient = new TestAsynchronousDownloadClient
                        {
                            CancelDownload = false,
                            DownloadSuccess = true
                        };
                    rInstaller.TestProcessRunner = new TestProcessRunner {ExitCode = 0};
                    rInstaller.TestProgramPath = string.Empty;
                });
            var downloadRDlg = ShowDialog<MessageDlg>(rInstaller.AcceptButton.PerformClick);
            Assert.AreEqual(Resources.RInstaller_GetR_R_installation_complete_, downloadRDlg.Message);
            OkDialog(downloadRDlg, downloadRDlg.OkDialog);
            var downloadPackagesDlg = WaitForOpenForm<MessageDlg>();
            Assert.AreEqual(Resources.RInstaller_GetPackages_Package_installation_complete_, downloadPackagesDlg.Message);
            OkDialog(downloadPackagesDlg, downloadPackagesDlg.OkDialog);
            Assert.AreEqual(DialogResult.Yes, rInstaller.DialogResult);
        }

        // helper method to simulate the creation of the InstallR dialog, so we can use our test installer
        private static void InstallProgram(ProgramPathContainer ppc, IEnumerable<string> packages, bool installed)
        {
            using (var dlg = new RInstaller(ppc, packages, installed, null))
            {
                dlg.ShowDialog();
            }
        }
    }

}
