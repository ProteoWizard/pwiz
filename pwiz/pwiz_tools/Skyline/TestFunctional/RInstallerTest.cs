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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

        private static readonly ProgramPathContainer PPC = new ProgramPathContainer(R, R_VERSION);

        private const string R = "R";
        private const string R_VERSION = "2.15.2";
        private static readonly ToolPackage PACKAGE_1 = new ToolPackage { Name = "Package1", Version = null};
        private static readonly ToolPackage PACKAGE_2 = new ToolPackage { Name = "Package2", Version = null };
        private static readonly ToolPackage PACKAGE_3 = new ToolPackage { Name = "Package3", Version = null };
        private static readonly ToolPackage PACKAGE_4 = new ToolPackage { Name = "Package4", Version = null };

        protected override void DoTest()
        {
            try
            {
                TestDlgLoad();
                TestProperPopulation();
                TestInstallR();
                TestInstallPackages();
                TestStartToFinish();
            }
            catch (Exception)
            {
                DebugLog.Info("Issue");
                throw;
            }
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
            var packages = new Collection<ToolPackage> { PACKAGE_1, PACKAGE_2, PACKAGE_3, PACKAGE_4 };

            var rInstaller = ShowDialog<RInstaller>(() => InstallProgram(PPC, packages, false));
            WaitForConditionUI(10 * 1000, () => rInstaller.IsLoaded);
            RunUI(() => Assert.AreEqual(string.Format(Resources.RInstaller_RInstaller_Load_This_tool_requires_the_use_of_R__0__and_the_following_packages_, PPC.ProgramVersion), rInstaller.Message));
            OkDialog(rInstaller, () => Cancel(rInstaller));
        }

        // R is installed, and there are packages to install
        private static void TestDlgLoadOnlyR()
        {
            var packages = new Collection<ToolPackage> { PACKAGE_1, PACKAGE_2, PACKAGE_3, PACKAGE_4 };
            var rInstaller = ShowDialog<RInstaller>(() => InstallProgram(PPC, packages, true));
            WaitForConditionUI(10 * 1000, () => rInstaller.IsLoaded);
            RunUI(() => Assert.AreEqual(Resources.RInstaller_RInstaller_Load_This_Tool_requires_the_use_of_the_following_R_Packages_, rInstaller.Message));
            OkDialog(rInstaller, () => Cancel(rInstaller));
        }

        // R is not installed, and there are no packages to install
        private static void TestDlgLoadOnlyPackages()
        {
            var packages = new Collection<ToolPackage>();
            var rInstaller = ShowDialog<RInstaller>(() => InstallProgram(PPC, packages, false));
            WaitForConditionUI(10 * 1000, () => rInstaller.IsLoaded);
            RunUI(() => Assert.AreEqual(string.Format(Resources.RInstaller_RInstaller_Load_This_tool_requires_the_use_of_R__0___Click_Install_to_begin_the_installation_process_, PPC.ProgramVersion), rInstaller.Message));
            OkDialog(rInstaller, () => Cancel(rInstaller));
        }

        // Tests that the form properly populates the checkbox
        private static void TestProperPopulation()
        {
            var packages = new Collection<ToolPackage> { PACKAGE_1, PACKAGE_2, PACKAGE_3, PACKAGE_4 };
            var rInstaller = ShowDialog<RInstaller>(() => InstallProgram(PPC, packages, false));
            WaitForConditionUI(10 * 1000, () => rInstaller.IsLoaded);
            RunUI(() => Assert.AreEqual(packages.Count, rInstaller.PackagesListCount));
            OkDialog(rInstaller, () => Cancel(rInstaller));
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
            var messageDlg = ShowDialog<MessageDlg>(rInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.MultiFileAsynchronousDownloadClient_DownloadFileAsyncWithBroker_Download_canceled_, messageDlg.Message));
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(rInstaller);
        }

        // Test R download success && install success
        private static void TestRDownloadAndInstallSuccess()
        {
            var rInstaller = FormatRInstaller(false, true, true);
            var messageDlg = ShowDialog<MessageDlg>(rInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.RInstaller_GetR_R_installation_complete_, messageDlg.Message));
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(rInstaller);
        }

        // Test R download success && install failure
        private static void TestRDownloadSuccessInstallFailure()
        {
            var rInstaller = FormatRInstaller(false, true, false);
            var messageDlg = ShowDialog<MessageDlg>(rInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.RInstaller_InstallR_R_installation_was_not_completed__Cancelling_tool_installation_, messageDlg.Message));
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(rInstaller);
        }

        // Test R download failure
        private static void TestRDownloadFailure()
        {
            var rInstaller = FormatRInstaller(false, false, false);
            var messageDlg = ShowDialog<MessageDlg>(rInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(TextUtil.LineSeparate(Resources.RInstaller_DownloadR_Download_failed_, Resources.RInstaller_DownloadPackages_Check_your_network_connection_or_contact_the_tool_provider_for_installation_support_), messageDlg.Message));
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(rInstaller);
        }

        // helper method for setting up the R installer form to support a number of possible installation outcomes
        private static RInstaller FormatRInstaller(bool cancelDownload, bool downloadSuccess, bool installSuccess)
        {
            var packages = new Collection<ToolPackage>();
            var rInstaller = ShowDialog<RInstaller>(() => InstallProgram(PPC, packages, false));
            WaitForConditionUI(10 * 1000, () => rInstaller.IsLoaded);
            RunUI(() =>
                {
                    rInstaller.TestDownloadClient = new TestAsynchronousDownloadClient { DownloadSuccess = downloadSuccess, CancelDownload = cancelDownload };
                    rInstaller.TestRunProcess = new TestRunProcess { ExitCode = installSuccess ? 0 : 1 };
                });
            return rInstaller;
        }

        private static void TestInstallPackages()
        {
            TestNoAdminPrivledges();
            TestExitBoxBeforeCompletion();
            TestUnknownError();
            TestNamePipeFailure();
            TestPackageInstallFailure();
            TestInternetConnectionFailure();
            TestPackageInstallSuccess();
        }

        private static void TestNamePipeFailure()
        {
            using (var textWriter = new StringWriter())
            {
                var rInstaller = FormatPackageInstaller(writer: textWriter, connectionSuccess: false);
                var messageDlg = ShowDialog<MessageDlg>(rInstaller.OkDialog);
                RunUI(() => Assert.AreEqual(TextUtil.LineSeparate(Resources.RInstaller_InstallPackages_Unknown_error_installing_packages__Tool_Installation_Failed_,
                                                                  string.Empty,
                                                                  Resources.TestNamedPipeProcessRunner_RunProcess_Error_running_process), messageDlg.Message));
                OkDialog(messageDlg, messageDlg.OkDialog);
                WaitForClosedForm(rInstaller);
            }
        }

        private static void TestUnknownError()
        {
            const string textToWrite = "This is a test"; //not L10N
            using (var textWriter = new StringWriter())
            {
                var rInstaller = FormatPackageInstaller(packageInstallerExitCode: -3, writer: textWriter, stringToWrite: textToWrite);
                var messageDlg = ShowDialog<MessageDlg>(rInstaller.OkDialog);
                RunUI(() => Assert.AreEqual(Resources.RInstaller_InstallPackages_Unknown_Error_installing_packages__Output_logged_to_the_Immediate_Window_, messageDlg.Message));
                OkDialog(messageDlg, messageDlg.OkDialog);
                Assert.IsTrue(textWriter.ToString().Contains(textToWrite));
                WaitForClosedForm(rInstaller);
            }
        }

        private static void TestExitBoxBeforeCompletion()
        {
            const string textToWrite = "This is a test"; //not L10N
            using (var textWriter = new StringWriter())
            {
                var rInstaller = FormatPackageInstaller(packageInstallerExitCode: RInstaller.EXIT_EARLY_CODE, writer: textWriter, stringToWrite: textToWrite);
                var messageDlg = ShowDialog<MessageDlg>(rInstaller.OkDialog);
                RunUI(() => Assert.AreEqual(Resources.RInstaller_InstallPackages_Error__Package_installation_did_not_complete__Output_logged_to_the_Immediate_Window_, messageDlg.Message));
                OkDialog(messageDlg, messageDlg.OkDialog);
                Assert.IsTrue(textWriter.ToString().Contains(textToWrite));
                WaitForClosedForm(rInstaller);
            }
        }

        private static void TestNoAdminPrivledges()
        {
            var rInstaller = FormatPackageInstaller(okAdminPrivledges: false);
            var messageDlg = ShowDialog<MessageDlg>(rInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.TestSkylineProcessRunner_RunProcess_The_operation_was_canceled_by_the_user_, messageDlg.Message));
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(rInstaller);
        }

        // Test for package install failure
        private static void TestPackageInstallFailure()
        {
            var failedPackages = new Collection<ToolPackage> { PACKAGE_1, PACKAGE_4 };
            var stringWriter = new StringWriter();
            const string errorText = "This is the Tool Error Text!"; // Not L10N
            var rInstaller = FormatPackageInstaller(stringToWrite: errorText, missingPackages: failedPackages,
                                                    writer: stringWriter);
            var messageDlg = ShowDialog<MessageDlg>(rInstaller.OkDialog);
            List<string> failedPackagesTitles = failedPackages.Select(f => f.Name).ToList();
            RunUI(() => Assert.AreEqual(TextUtil.LineSeparate(Resources.RInstaller_InstallPackages_The_following_packages_failed_to_install_,
                                                            string.Empty,
                                                            TextUtil.LineSeparate(failedPackagesTitles),
                                                            string.Empty,
                                                            Resources.RInstaller_InstallPackages_Output_logged_to_the_Immediate_Window_),
                                         messageDlg.Message));
            string outText = stringWriter.ToString();
            Assert.IsTrue(outText.Contains(errorText));
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(rInstaller);
        }

        // Test no internet connection.
        private static void TestInternetConnectionFailure()
        {
            var rInstaller = FormatPackageInstaller(true);
            var messageDlg = ShowDialog<MessageDlg>(rInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(TextUtil.LineSeparate(Resources.RInstaller_InstallPackages_Error__No_internet_connection_, string.Empty, Resources.RInstaller_InstallPackages_Installing_R_packages_requires_an_internet_connection__Please_check_your_connection_and_try_again), messageDlg.Message));
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(rInstaller);
        }

        // Test package install success
        private static void TestPackageInstallSuccess()
        {
            var rInstaller = FormatPackageInstaller();
            OkDialog(rInstaller, rInstaller.OkDialog);
        }

        // helper method for setting up the R installer form to support a number of possible package installation outcomes
        private static RInstaller FormatPackageInstaller(bool cutoffInternet = false, string stringToWrite = null, ICollection<ToolPackage> missingPackages = null, int packageInstallerExitCode = 0, TextWriter writer = null, bool okAdminPrivledges = true, bool connectionSuccess = true)
        {
            var packages = new Collection<ToolPackage> { PACKAGE_1, PACKAGE_2, PACKAGE_3, PACKAGE_4 };
            var rInstaller = ShowDialog<RInstaller>(() => InstallProgram(PPC, packages, true, writer));
            WaitForConditionUI(10 * 1000, () => rInstaller.IsLoaded);
            RunUI(() =>
            {
                // R Installation
                rInstaller.TestDownloadClient = new TestAsynchronousDownloadClient { DownloadSuccess = true, CancelDownload = false };
                // Package Installation
                rInstaller.PackageInstallHelpers = new TestPackageInstallationHelper { PackagesToInstall = missingPackages ?? new List<ToolPackage>(), RProgramPath = "testPath.exe", InternetConnectionDoesNotExists = cutoffInternet };
                rInstaller.TestSkylineProcessRunnerWrapper = new TestSkylineProcessRunner { stringToWriteToWriter = stringToWrite, ExitCode = packageInstallerExitCode, UserOkRunAsAdministrator = okAdminPrivledges, ConnectSuccess = connectionSuccess };
            });
            return rInstaller;
        }

        // Tests the start to finish process of installing both R and associated packages
        private static void TestStartToFinish()
        {
            var packages = new Collection<ToolPackage> { PACKAGE_1, PACKAGE_2, PACKAGE_3, PACKAGE_4 };
            var rInstaller = ShowDialog<RInstaller>(() => InstallProgram(PPC, packages, false));
            WaitForConditionUI(10 * 1000, () => rInstaller.IsLoaded);
            RunUI(() =>
                {
                    rInstaller.PackageInstallHelpers = new TestPackageInstallationHelper { PackagesToInstall = new List<ToolPackage>() };
                    rInstaller.TestDownloadClient = new TestAsynchronousDownloadClient
                        {
                            CancelDownload = false,
                            DownloadSuccess = true
                        };
                    rInstaller.TestRunProcess = new TestRunProcess { ExitCode = 0 };
                    rInstaller.TestSkylineProcessRunnerWrapper = new TestSkylineProcessRunner { ConnectSuccess = true, ExitCode = 0, stringToWriteToWriter = string.Empty, UserOkRunAsAdministrator = true };
                });
            var downloadRDlg = ShowDialog<MessageDlg>(rInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.RInstaller_GetR_R_installation_complete_, downloadRDlg.Message));
            OkDialog(downloadRDlg, downloadRDlg.OkDialog);
            WaitForClosedForm(rInstaller);
        }

        // helper method to simulate the creation of the InstallR dialog, so we can use our test installer
        private static void InstallProgram(ProgramPathContainer ppc, ICollection<ToolPackage> packages, bool installed, TextWriter writer = null)
        {
            using (var dlg = new RInstaller(ppc, packages, installed, writer, null))
            {
                // Keep OK button from doing anything ever
                dlg.TestRunProcess = new TestRunProcess { ExitCode = 0 };
                dlg.ShowDialog();
            }
        }

        private static void Cancel(Form form)
        {
            WaitForConditionUI(5000, () => form.CancelButton != null);
            form.CancelButton.PerformClick();
        }

        private class TestPackageInstallationHelper : RInstaller.IPackageInstallHelpers
        {
            public ICollection<ToolPackage> PackagesToInstall { private get; set; }
            public string RProgramPath { private get; set; }
            public bool InternetConnectionDoesNotExists { private get; set; }

            public ICollection<ToolPackage> WhichPackagesToInstall(ICollection<ToolPackage> packages, string pathToR)
            {
                if (PackagesToInstall != null)
                    return PackagesToInstall;
                else return new List<ToolPackage>();
            }

            public string FindRProgramPath(string rVersion)
            {
                return RProgramPath ?? string.Empty;
            }

            public bool CheckForInternetConnection()
            {
                return !InternetConnectionDoesNotExists;
            }
        }
    }
}
