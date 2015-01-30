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
    /// Functional test for the Python & Python package installer
    /// </summary>
    [TestClass]
    public class PythonInstallerTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void  TestPythonInstaller()
        {
            RunFunctionalTest();
        }

        private const string PYTHON = "Python";
        private const string VERSION_27 = "2.7";
        private const string EXE_PACKAGE = "http://test.com/package.exe";
        private const string TARGZ_PACKAGE = "http://test.com/package.tar.gz";
        private const string ZIP_PACKAGE = "http://test.com/package.zip";
        private const string LOCAL_ZIP_PACKAGE = "C:\\localpackage.zip";
        private const string LOCAL_TARGZ_PACKAGE = "C:\\localpackage.tar.gz";
        private const string LOCAL_EXE_PACKAGE = "C:\\localpackage.exe";
        
        protected override void DoTest()
        {
            TestDlgLoad();
            TestProperPopulation();
            TestGetPython();
            TestGetPackages();
            TestStartToFinish();
        }

        // Tests that the form loads with the proper display based on whether Python is installed or not, as
        // well as if there are packages to download
        private static void TestDlgLoad()
        {
            TestDlgLoadBoth();
            TestDlgLoadNotInstalled();
            TestDlgLoadPackagesOnly();
        }

        // Python is not installed and there are packages to install
        private static void TestDlgLoadBoth()
        {
            var ppc = new ProgramPathContainer(PYTHON, VERSION_27);
            var packageUris = new Collection<string> {EXE_PACKAGE, TARGZ_PACKAGE, ZIP_PACKAGE, LOCAL_EXE_PACKAGE, LOCAL_TARGZ_PACKAGE, LOCAL_ZIP_PACKAGE};
            var pythonInstaller = ShowDialog<PythonInstaller>(() => InstallProgram(ppc, packageUris, false));
            WaitForConditionUI(5*1000, () => pythonInstaller.IsLoaded);
            RunUI(() => Assert.AreEqual(string.Format(Resources.PythonInstaller_PythonInstaller_Load_This_tool_requires_Python__0__and_the_following_packages__Select_packages_to_install_and_then_click_Install_to_begin_the_installation_process_, VERSION_27), pythonInstaller.Message));
            OkDialog(pythonInstaller, () => Cancel(pythonInstaller));
        }

        // Python is not installed and there are no packages to install
        public static void TestDlgLoadNotInstalled()
        {
            var ppc = new ProgramPathContainer(PYTHON, VERSION_27);
            var pythonInstaller = ShowDialog<PythonInstaller>(() => InstallProgram(ppc, new String[0], false));
            WaitForConditionUI(5 * 1000, () => pythonInstaller.IsLoaded);
            RunUI(() => Assert.AreEqual(string.Format(Resources.PythonInstaller_PythonInstaller_Load_This_tool_requires_Python__0___Click_install_to_begin_the_installation_process_, VERSION_27), pythonInstaller.Message));
            OkDialog(pythonInstaller, () => Cancel(pythonInstaller));
        }

        // Python is installed and there are packages to install
        private static void TestDlgLoadPackagesOnly()
        {
            var ppc = new ProgramPathContainer(PYTHON, VERSION_27);
            var packageUris = new Collection<string> { EXE_PACKAGE, TARGZ_PACKAGE, ZIP_PACKAGE, LOCAL_EXE_PACKAGE, LOCAL_TARGZ_PACKAGE, LOCAL_ZIP_PACKAGE };
            var pythonInstaller = ShowDialog<PythonInstaller>(() => InstallProgram(ppc, packageUris, true));
            WaitForConditionUI(5 * 1000, () => pythonInstaller.IsLoaded);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_PythonInstaller_Load_This_tool_requires_the_following_Python_packages__Select_packages_to_install_and_then_click_Install_to_begin_the_installation_process_, pythonInstaller.Message));
            OkDialog(pythonInstaller, () => Cancel(pythonInstaller));
        }

        // Tests that the form properly populates the checkbox
        private static void TestProperPopulation()
        {
            var PPC = new ProgramPathContainer(PYTHON, VERSION_27);
            var PackageUris = new Collection<string> { EXE_PACKAGE, TARGZ_PACKAGE, ZIP_PACKAGE, LOCAL_EXE_PACKAGE, LOCAL_TARGZ_PACKAGE, LOCAL_ZIP_PACKAGE };
            var pythonInstaller = ShowDialog<PythonInstaller>(() => InstallProgram(PPC, PackageUris, false));
            WaitForConditionUI(5 * 1000, () => pythonInstaller.IsLoaded);

            RunUI(() => Assert.AreEqual(PackageUris.Count, pythonInstaller.PackagesListCount));

            // ensure that packages default to checked upon load
            RunUI(() => Assert.AreEqual(PackageUris.Count, pythonInstaller.CheckedPackagesCount));
            OkDialog(pythonInstaller, () => Cancel(pythonInstaller));
        }

        private static void TestGetPython()
        {
            TestPythonDownloadCanceled();
            TestPythonDownloadFailed();
            TestPythonInstallFailed();
            TestPythonInstallSucceeded();
        }

        // Test canceling the Python download
        private static void TestPythonDownloadCanceled()
        {
            var pythonInstaller = FormatPythonInstaller(true, false, false);
            var messageDlg = ShowDialog<MessageDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.MultiFileAsynchronousDownloadClient_DownloadFileAsyncWithBroker_Download_canceled_, messageDlg.Message));
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        // Test Python download failure
        private static void TestPythonDownloadFailed()
        {
            var pythonInstaller = FormatPythonInstaller(false, false, false);
            var messageDlg = ShowDialog<MessageDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(TextUtil.LineSeparate(
                Resources.PythonInstaller_DownloadPython_Download_failed_, 
                Resources.PythonInstaller_DownloadPython_Check_your_network_connection_or_contact_the_tool_provider_for_installation_support_), messageDlg.Message));
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        // Test Python installation failure
        private static void TestPythonInstallFailed()
        {
            var pythonInstaller = FormatPythonInstaller(false, true, false);
            var messageDlg = ShowDialog<MessageDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPython_Python_installation_failed__Canceling_tool_installation_, messageDlg.Message));
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        // Test Python installation success
        private static void TestPythonInstallSucceeded()
        {
            var pythonInstaller = FormatPythonInstaller(false, true, true);
            var messageDlg = ShowDialog<MessageDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_GetPython_Python_installation_completed_, messageDlg.Message));
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        private static PythonInstaller FormatPythonInstaller(bool cancelDownload, bool downloadSuccess,
                                                             bool installSuccess)
        {
            var ppc = new ProgramPathContainer(PYTHON, VERSION_27);
            // ReSharper disable once CollectionNeverUpdated.Local
            var packageUris = new Collection<string>();
            var pythonInstaller = ShowDialog<PythonInstaller>(() => InstallProgram(ppc, packageUris, false));
            WaitForConditionUI(() => pythonInstaller.IsLoaded);
            RunUI(() =>
                {
                    pythonInstaller.TestDownloadClient = new TestAsynchronousDownloadClient
                        {
                            CancelDownload = cancelDownload,
                            DownloadSuccess = downloadSuccess
                        };
                    pythonInstaller.TestRunProcess = new TestRunProcess {ExitCode = installSuccess ? 0 : 1};
                });
            return pythonInstaller;
        }

        private static void TestGetPackages()
        {
            TestPackagesDownloadCanceled();
            TestPackagesDownloadFailed();
            TestPackagesUnselected();
            TestPackagesSourceOnly();
            TestPackagesExecutablesOnly();
            TestPackagesBothFileTypes();
        }

        // Test canceling the package download
        private static void TestPackagesDownloadCanceled()
        {
            var pythonInstaller = FormatPackageInstallerBothTypes(true, false, false, false);
            var messageDlg = ShowDialog<MessageDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.MultiFileAsynchronousDownloadClient_DownloadFileAsyncWithBroker_Download_canceled_, messageDlg.Message));
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        // Test failing the package download
        private static void TestPackagesDownloadFailed()
        {
            var pythonInstaller = FormatPackageInstallerOnlyExes(false, false, false);
            var messageDlg = ShowDialog<MessageDlg>(pythonInstaller.OkDialog);
            var packages = new Collection<string> {EXE_PACKAGE};
            RunUI(() => Assert.AreEqual(TextUtil.LineSeparate(Resources.PythonInstaller_DownloadPackages_Failed_to_download_the_following_packages_, 
                                                  string.Empty, 
                                                  TextUtil.LineSeparate(packages), 
                                                  string.Empty, 
                                                  Resources.PythonInstaller_DownloadPython_Check_your_network_connection_or_contact_the_tool_provider_for_installation_support_), messageDlg.Message));
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        // Test that tool installation works properly when no packages are selected
        private static void TestPackagesUnselected()
        {
            var pythonInstaller = FormatPackageInstallerBothTypes(false, true, true, true);
            RunUI(() =>
                {
                    pythonInstaller.UncheckAllPackages();
                    pythonInstaller.OkDialog();
                });
            WaitForClosedForm(pythonInstaller);
        }

        private static void TestPackagesSourceOnly()
        {
            TestGetPipCancel();
            TestGetPipCancelDownload();
            TestGetPipFailed();
            TestInstallPipFailed();
            TestInstallPipConnectFailed();
            TestSourceConnectFailure();
            TestSourceInstallFailure();
            TestSourceInstallSuccess();
        }

        // Test canceling the pip installation dialog
        private static void TestGetPipCancel()
        {
            var pythonInstaller = FormatPackageInstallerOnlySources(false, true, false, false);
            SetPipInstallResults(pythonInstaller, true, false, false, false);
            var messageDlg = ShowDialog<MultiButtonMsgDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Skyline_uses_the_Python_tool_setuptools_and_the_Python_package_manager_Pip_to_install_packages_from_source__Click_install_to_begin_the_installation_process_, messageDlg.Message));
            var errorDlg = ShowDialog<MessageDlg>(messageDlg.BtnCancelClick);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Python_package_installation_cannot_continue__Canceling_tool_installation_, errorDlg.Message));
            OkDialog(errorDlg, errorDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        // Test canceling the pip download
        private static void TestGetPipCancelDownload()
        {
            var pythonInstaller = FormatPackageInstallerOnlySources(false, true, false, false);
            SetPipInstallResults(pythonInstaller, true, false, false, false);
            var messageDlg = ShowDialog<MultiButtonMsgDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Skyline_uses_the_Python_tool_setuptools_and_the_Python_package_manager_Pip_to_install_packages_from_source__Click_install_to_begin_the_installation_process_, messageDlg.Message));
            var pipErrorDlg = ShowDialog<MessageDlg>(() => Accept(messageDlg));
            RunUI(() => Assert.AreEqual(Resources.MultiFileAsynchronousDownloadClient_DownloadFileAsyncWithBroker_Download_canceled_, pipErrorDlg.Message));
            OkDialog(pipErrorDlg, pipErrorDlg.OkDialog);
            var packageErrorDlg = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Python_package_installation_cannot_continue__Canceling_tool_installation_, packageErrorDlg.Message));
            OkDialog(packageErrorDlg, packageErrorDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        // Test failing the pip download
        private static void TestGetPipFailed()
        {
            var pythonInstaller = FormatPackageInstallerOnlySources(false, true, false, false);
            SetPipInstallResults(pythonInstaller, false, false, false, false);
            var messageDlg = ShowDialog<MultiButtonMsgDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Skyline_uses_the_Python_tool_setuptools_and_the_Python_package_manager_Pip_to_install_packages_from_source__Click_install_to_begin_the_installation_process_, messageDlg.Message));
            var pipErrorDlg = ShowDialog<MessageDlg>(() => Accept(messageDlg));
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_DownloadPip_Download_failed__Check_your_network_connection_or_contact_Skyline_developers_, pipErrorDlg.Message));
            OkDialog(pipErrorDlg, pipErrorDlg.OkDialog);
            var packageErrorDlg = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Python_package_installation_cannot_continue__Canceling_tool_installation_, packageErrorDlg.Message));
            OkDialog(packageErrorDlg, packageErrorDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        // Test failing to connect in the pip installation
        private static void TestInstallPipConnectFailed()
        {
            var pythonInstaller = FormatPackageInstallerOnlySources(false, true, false, false);
            SetPipInstallResults(pythonInstaller, false, true, false, false);
            var messageDlg = ShowDialog<MultiButtonMsgDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Skyline_uses_the_Python_tool_setuptools_and_the_Python_package_manager_Pip_to_install_packages_from_source__Click_install_to_begin_the_installation_process_, messageDlg.Message));
            var pipErrorDlg = ShowDialog<MessageDlg>(() => Accept(messageDlg));
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPip_Unknown_error_installing_pip_, pipErrorDlg.Message));
            OkDialog(pipErrorDlg, pipErrorDlg.OkDialog);
            var packageErrorDlg = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Python_package_installation_cannot_continue__Canceling_tool_installation_, packageErrorDlg.Message));
            OkDialog(packageErrorDlg, packageErrorDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        // Test failing the pip installation
        private static void TestInstallPipFailed()
        {
            var pythonInstaller = FormatPackageInstallerOnlySources(false, true, false, false);
            SetPipInstallResults(pythonInstaller, false, true, true, false);
            var messageDlg = ShowDialog<MultiButtonMsgDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Skyline_uses_the_Python_tool_setuptools_and_the_Python_package_manager_Pip_to_install_packages_from_source__Click_install_to_begin_the_installation_process_, messageDlg.Message));
            var pipErrorDlg = ShowDialog<MessageDlg>(() => Accept(messageDlg));
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPip_Pip_installation_failed__Error_log_output_in_immediate_window__, pipErrorDlg.Message));
            OkDialog(pipErrorDlg, pipErrorDlg.OkDialog);
            var packageErrorDlg = FindOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Python_package_installation_cannot_continue__Canceling_tool_installation_, packageErrorDlg.Message));
            OkDialog(packageErrorDlg, packageErrorDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        private static void TestSourceConnectFailure()
        {
            var pythonInstaller = FormatPackageInstallerOnlySources(false, true, false, false);
            SetPipInstallResults(pythonInstaller, false, true, true, true);
            var messageDlg = ShowDialog<MultiButtonMsgDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Skyline_uses_the_Python_tool_setuptools_and_the_Python_package_manager_Pip_to_install_packages_from_source__Click_install_to_begin_the_installation_process_, messageDlg.Message));
            var pipMessageDlg = ShowDialog<MessageDlg>(() => Accept(messageDlg));
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Pip_installation_complete_, pipMessageDlg.Message));
            OkDialog(pipMessageDlg, pipMessageDlg.OkDialog);
            var errorDlg = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Unknown_error_installing_packages_, errorDlg.Message));
            OkDialog(errorDlg, errorDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        // Test failing to install the packages from source
        private static void TestSourceInstallFailure()
        {
            var pythonInstaller = FormatPackageInstallerOnlySources(false, true, true, false);
            SetPipInstallResults(pythonInstaller, false, true, true, true);
            var messageDlg = ShowDialog<MultiButtonMsgDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Skyline_uses_the_Python_tool_setuptools_and_the_Python_package_manager_Pip_to_install_packages_from_source__Click_install_to_begin_the_installation_process_, messageDlg.Message));
            var pipMessageDlg = ShowDialog<MessageDlg>(() => Accept(messageDlg));
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Pip_installation_complete_, pipMessageDlg.Message));
            OkDialog(pipMessageDlg, pipMessageDlg.OkDialog);
            var errorDlg = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Package_installation_failed__Error_log_output_in_immediate_window_, errorDlg.Message));
            OkDialog(errorDlg, errorDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        // Test successfully installing packages from source only
        private static void TestSourceInstallSuccess()
        {
            var pythonInstaller = FormatPackageInstallerOnlySources(false, true, true, true);
            SetPipInstallResults(pythonInstaller, false, true, true, true);
            var messageDlg = ShowDialog<MultiButtonMsgDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Skyline_uses_the_Python_tool_setuptools_and_the_Python_package_manager_Pip_to_install_packages_from_source__Click_install_to_begin_the_installation_process_, messageDlg.Message));
            var pipMessageDlg = ShowDialog<MessageDlg>(() => Accept(messageDlg));
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Pip_installation_complete_, pipMessageDlg.Message));
            OkDialog(pipMessageDlg, pipMessageDlg.OkDialog);
            var installSuccessDlg = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_GetPackages_Package_installation_completed_, installSuccessDlg.Message));
            OkDialog(installSuccessDlg, installSuccessDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        private static void SetPipInstallResults(PythonInstaller pythonInstaller, bool cancelDownload,
                                                 bool downloadSuccess, bool connectSuccess, bool installSuccess)
        {
            RunUI(() =>
                {
                    pythonInstaller.TestPipDownloadClient = new TestAsynchronousDownloadClient
                        {
                            CancelDownload = cancelDownload,
                            DownloadSuccess = downloadSuccess
                        };
                    pythonInstaller.TestPipeSkylineProcessRunner = new TestSkylineProcessRunner
                        {
                            ConnectSuccess = connectSuccess,
                            ExitCode = installSuccess ? 0 : 1
                        };
                    pythonInstaller.TestingPip = true;
                });
        }

        private static void TestPackagesExecutablesOnly()
        {
            TestExecutableInstallFailure();
            TestExecutableInstallSuccess();
        }

        // Test failing to install executable packages
        private static void TestExecutableInstallFailure()
        {
            var pythonInstaller = FormatPackageInstallerOnlyExes(false, true, false);
            RunUI(() => pythonInstaller.TestRunProcess = new TestRunProcess {ExitCode = 1});
            var messageDlg = ShowDialog<MessageDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Package_Installation_was_not_completed__Canceling_tool_installation_, messageDlg.Message));
            OkDialog(messageDlg, messageDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        // Test successfully installing packages from executables only
        private static void TestExecutableInstallSuccess()
        {
            var pythonInstaller = FormatPackageInstallerOnlyExes(false, true, true);
            RunUI(() => pythonInstaller.TestRunProcess = new TestRunProcess {ExitCode = 0});
            var installSuccessDlg = ShowDialog<MessageDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_GetPackages_Package_installation_completed_, installSuccessDlg.Message));
            OkDialog(installSuccessDlg, installSuccessDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        // Test successfully installing packages from both source and executables
        private static void TestPackagesBothFileTypes()
        {
            var pythonInstaller = FormatPackageInstallerBothTypes(false, true, true, true);
            SetPipInstallResults(pythonInstaller, false, true, true, true);
            RunUI(() => pythonInstaller.TestRunProcess = new TestRunProcess {ExitCode = 0});
            var messageDlg = ShowDialog<MultiButtonMsgDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Skyline_uses_the_Python_tool_setuptools_and_the_Python_package_manager_Pip_to_install_packages_from_source__Click_install_to_begin_the_installation_process_, messageDlg.Message));
            var pipMessageDlg = ShowDialog<MessageDlg>(() => Accept(messageDlg));
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Pip_installation_complete_, pipMessageDlg.Message));
            OkDialog(pipMessageDlg, pipMessageDlg.OkDialog);
            var installSuccessDlg = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_GetPackages_Package_installation_completed_, installSuccessDlg.Message));
            OkDialog(installSuccessDlg, installSuccessDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        private static PythonInstaller FormatPackageInstallerOnlyExes(bool cancelDownload, bool downloadSuccess, bool installSuccess)
        {
            return FormatPackageInstaller(cancelDownload, downloadSuccess, true, installSuccess,
                                          new Collection<string> {EXE_PACKAGE, LOCAL_EXE_PACKAGE});
        }

        private static PythonInstaller FormatPackageInstallerOnlySources(bool cancelDownload, bool downloadSuccess,
                                                                         bool connectSuccess, bool installSuccess)
        {
            return FormatPackageInstaller(cancelDownload, downloadSuccess, connectSuccess, installSuccess,
                                          new Collection<string> {TARGZ_PACKAGE, ZIP_PACKAGE, LOCAL_TARGZ_PACKAGE, LOCAL_ZIP_PACKAGE});
        }

        private static PythonInstaller FormatPackageInstallerBothTypes(bool cancelDownload, bool downloadSuccess,
                                                                       bool connectSuccess, bool installSuccess)
        {
            return FormatPackageInstaller(cancelDownload, downloadSuccess, connectSuccess, installSuccess,
                                          new Collection<string> {ZIP_PACKAGE, TARGZ_PACKAGE, EXE_PACKAGE, LOCAL_ZIP_PACKAGE, LOCAL_TARGZ_PACKAGE, LOCAL_EXE_PACKAGE});
        }

        private static PythonInstaller FormatPackageInstaller(bool cancelDownload, bool downloadSuccess,
                                                              bool connectSuccess, bool installSuccess, IEnumerable<string> packageUris)
        {
            var ppc = new ProgramPathContainer(PYTHON, VERSION_27);
            var pythonInstaller = ShowDialog<PythonInstaller>(() => InstallProgram(ppc, packageUris, true));
            WaitForConditionUI(() => pythonInstaller.IsLoaded);
            RunUI(() =>
                {
                    pythonInstaller.TestDownloadClient = new TestAsynchronousDownloadClient
                        {
                            CancelDownload = cancelDownload,
                            DownloadSuccess = downloadSuccess
                        };
                    pythonInstaller.TestSkylineProcessRunner = new TestSkylineProcessRunner
                        {
                            ConnectSuccess = connectSuccess,
                            ExitCode = installSuccess ? 0 : 1
                        };
                });
            return pythonInstaller;
        }

        // Tests a start to finish installation of Python, and both executable and source packages
        private static void TestStartToFinish()
        {
            var pythonInstaller =
                ShowDialog<PythonInstaller>(
                    () =>
                    InstallProgram(new ProgramPathContainer(PYTHON, VERSION_27),
                                   new Collection<string> {EXE_PACKAGE, TARGZ_PACKAGE, ZIP_PACKAGE, LOCAL_EXE_PACKAGE, LOCAL_TARGZ_PACKAGE, LOCAL_ZIP_PACKAGE}, false));
            
            RunUI(() =>
                {
                    pythonInstaller.TestDownloadClient = pythonInstaller.TestPipDownloadClient = new TestAsynchronousDownloadClient
                        {
                            CancelDownload = false,
                            DownloadSuccess = true
                        };
                    pythonInstaller.TestRunProcess = new TestRunProcess {ExitCode = 0};
                    pythonInstaller.TestPipDownloadClient = new TestAsynchronousDownloadClient
                        {
                            CancelDownload = false,
                            DownloadSuccess = true
                        };
                    pythonInstaller.TestPipeSkylineProcessRunner = pythonInstaller.TestSkylineProcessRunner = new TestSkylineProcessRunner
                        {
                            ConnectSuccess = true,
                            ExitCode = 0
                        };
                    pythonInstaller.TestingPip = true;
                });

            var pythonInstallSuccessDlg = ShowDialog<MessageDlg>(pythonInstaller.OkDialog);
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_GetPython_Python_installation_completed_, pythonInstallSuccessDlg.Message));
            OkDialog(pythonInstallSuccessDlg, pythonInstallSuccessDlg.OkDialog);
            var pipPromptDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Skyline_uses_the_Python_tool_setuptools_and_the_Python_package_manager_Pip_to_install_packages_from_source__Click_install_to_begin_the_installation_process_, pipPromptDlg.Message));
            OkDialog(pipPromptDlg, () => Accept(pipPromptDlg));
            var pipInstallSuccessDlg = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_InstallPackages_Pip_installation_complete_, pipInstallSuccessDlg.Message));
            OkDialog(pipInstallSuccessDlg, pipInstallSuccessDlg.OkDialog);
            var packageInstallSuccessDlg = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(Resources.PythonInstaller_GetPackages_Package_installation_completed_, packageInstallSuccessDlg.Message));
            OkDialog(packageInstallSuccessDlg, packageInstallSuccessDlg.OkDialog);
            WaitForClosedForm(pythonInstaller);
        }

        private static void InstallProgram(ProgramPathContainer ppc, IEnumerable<string> packageUris, bool installed)
        {
            using (var dlg = new PythonInstaller(ppc, packageUris, installed, null))
            {
                // Keep OK button from doing anything ever
                dlg.TestRunProcess = new TestRunProcess { ExitCode = 0 }; 
                dlg.ShowDialog();
            }
        }

        private static void Accept(Form form)
        {
            WaitForConditionUI(5000, () => form.AcceptButton != null);
            form.AcceptButton.PerformClick();
        }

        private static void Cancel(Form form)
        {
            WaitForConditionUI(5000, () => form.CancelButton != null);
            form.CancelButton.PerformClick();
        }
    }
}
