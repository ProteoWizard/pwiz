/*
 * 
 * Copyright 2025 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Tools;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.Text;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Util;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using System.IO;
using Ionic.Zip;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for PythonInstaller
    /// </summary>
    [TestClass]
    public class PythonInstallerTest : AbstractFunctionalTest
    {
        private const string PYTHON = @"Python";
        //private const string VERSION_312 = @"3.1.2";
        private const string VERSION = @"3.9.2";
        //private IPythonInstallerTaskValidator TaskValidator { get; }

        //[TestMethod]
        public void TestPythonInstaller()
        {
            //TestFilesZip = @"TestFunctional\PythonInstallerTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestDownloadPythonEmbeddablePackage_Cancel();
            TestDownloadPythonEmbeddablePackage_Fail();
            TestDownloadPythonEmbeddablePackage_Success();
            TestUnzipPythonEmbeddablePackage_Fail();
            TestUnzipPythonEmbeddablePackage_Success();
            TestEnableSearchPathInPythonEmbeddablePackage_NoSearchPathFile_Fail();
            TestEnableSearchPathInPythonEmbeddablePackage_MoreThanOneSearchPathFile_Fail();
            TestEnableSearchPathInPythonEmbeddablePackage_Success();
            TestDownloadGetPipScript_Cancel();
            TestDownloadGetPipScript_Fail();
            TestDownloadGetPipScript_Success();
            TestRunGetPipScript_Fail();
            TestRunGetPipScript_Success();
            TestPipInstallVirtualenv_Fail();
            TestPipInstallVirtualenv_Success();
            TestCreateVirtualEnvironment_Fail();
            TestCreateVirtualEnvironment_Success();
            TestPipInstallPackages_Fail();
            TestPipInstallPackages_Success();
            TestStartToFinishWithVirtualEnvironment_Success();
        }

        private static void TestDownloadPythonEmbeddablePackage_Cancel()
        {
            // Set up
            var pythonInstaller = GetPythonInstaller();
            pythonInstaller.TestDownloadClient = new TestAsynchronousDownloadClient
            {
                CancelDownload = true
            };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg1 = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_download_Python_embeddable_package, dlg1.Message));

            OkDialog(dlg1, dlg1.OkDialog);
            var dlg2 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(Resources.MultiFileAsynchronousDownloadClient_DownloadFileAsyncWithBroker_Download_canceled_, dlg2.Message));

            OkDialog(dlg2, dlg2.OkDialog);
            var dlg3 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment, dlg3.Message));

            OkDialog(dlg3, dlg3.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestDownloadPythonEmbeddablePackage_Fail()
        {
            // Set up
            var pythonInstaller = GetPythonInstaller();
            pythonInstaller.TestDownloadClient = new TestAsynchronousDownloadClient
            {
                CancelDownload = false,
                DownloadSuccess = false
            };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg1 = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_download_Python_embeddable_package, dlg1.Message));

            OkDialog(dlg1, dlg1.OkDialog);
            var dlg2 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsResources.PythonInstaller_Download_failed__Check_your_network_connection_or_contact_Skyline_team_for_help_, dlg2.Message));

            OkDialog(dlg2, dlg2.OkDialog);
            var dlg3 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment, dlg3.Message));

            OkDialog(dlg3, dlg3.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestDownloadPythonEmbeddablePackage_Success()
        {
            // Set up
            var pythonInstaller = GetPythonInstaller();
            pythonInstaller.TestDownloadClient = new TestAsynchronousDownloadClient
            {
                CancelDownload = false,
                DownloadSuccess = true
            };
            pythonInstaller.TestPythonVirtualEnvironmentTaskNames = new List<PythonTaskName>
                { PythonTaskName.download_python_embeddable_package };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment, dlg.Message));

            OkDialog(dlg, dlg.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestUnzipPythonEmbeddablePackage_Fail()
        {
            // Set up
            var taskValidator = new TestPythonInstallerTaskValidator();
            taskValidator.SetSuccessUntil(PythonTaskName.download_python_embeddable_package);
            var pythonInstaller = GetPythonInstaller(taskValidator);
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg1 = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_unzip_Python_embeddable_package, dlg1.Message));

            OkDialog(dlg1, dlg1.OkDialog);
            var dlg2 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.IsTrue(dlg2.Message.Contains($@"Could not find file")));

            OkDialog(dlg2, dlg2.OkDialog);
            var dlg3 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment, dlg3.Message));

            OkDialog(dlg3, dlg3.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestUnzipPythonEmbeddablePackage_Success()
        {
            // Set up
            var pythonInstaller = GetPythonInstaller();
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });
            try
            {
                var fileName = $@"test.txt";
                var filePath = CreateFile(PythonInstallerUtil.PythonRootDir, fileName);
                CreateZipFile(pythonInstaller.PythonEmbeddablePackageDownloadPath, (fileName, filePath));
            }
            catch (Exception)
            {
                CleanupDir(PythonInstallerUtil.PythonRootDir);
                throw;
            }

            RunUI(() =>
            {
                pythonInstaller.TestPythonVirtualEnvironmentTaskNames = new List<PythonTaskName>
                    { PythonTaskName.unzip_python_embeddable_package };
            });

            // Test
            var dlg = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment, dlg.Message));

            OkDialog(dlg, dlg.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }
        private static void TestEnableSearchPathInPythonEmbeddablePackage_NoSearchPathFile_Fail()
        {
            // Set up
            var taskValidator = new TestPythonInstallerTaskValidator();
            taskValidator.SetSuccessUntil(PythonTaskName.unzip_python_embeddable_package);
            var pythonInstaller = GetPythonInstaller(taskValidator);
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });
            try
            {
                CreateDirectory(pythonInstaller.PythonEmbeddablePackageExtractDir);
            }
            catch (Exception)
            {
                CleanupDir(PythonInstallerUtil.PythonRootDir);
                throw;
            }

            // Test
            var dlg1 = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_enable_search_path_in_Python_embeddable_package, dlg1.Message));

            OkDialog(dlg1, dlg1.OkDialog);
            var dlg2 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsResources.PythonInstaller_EnableSearchPathInPythonEmbeddablePackage_Found_0_or_more_than_one_files_with__pth_extension__this_is_unexpected, dlg2.Message));

            OkDialog(dlg2, dlg2.OkDialog);
            var dlg3 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment, dlg3.Message));

            OkDialog(dlg3, dlg3.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestEnableSearchPathInPythonEmbeddablePackage_MoreThanOneSearchPathFile_Fail()
        {
            // Set up
            var taskValidator = new TestPythonInstallerTaskValidator();
            taskValidator.SetSuccessUntil(PythonTaskName.unzip_python_embeddable_package);
            var pythonInstaller = GetPythonInstaller(taskValidator);
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            try
            {
                CreateDirectory(pythonInstaller.PythonEmbeddablePackageExtractDir);
                CreateFile(pythonInstaller.PythonEmbeddablePackageExtractDir, @"file1._pth");
                CreateFile(pythonInstaller.PythonEmbeddablePackageExtractDir, @"file2._pth");
            }
            catch (Exception)
            {
                CleanupDir(PythonInstallerUtil.PythonRootDir);
                throw;
            }

            // Test
            var dlg1 = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_enable_search_path_in_Python_embeddable_package, dlg1.Message));

            OkDialog(dlg1, dlg1.OkDialog);
            var dlg2 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsResources.PythonInstaller_EnableSearchPathInPythonEmbeddablePackage_Found_0_or_more_than_one_files_with__pth_extension__this_is_unexpected, dlg2.Message));

            OkDialog(dlg2, dlg2.OkDialog);
            var dlg3 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment, dlg3.Message));

            OkDialog(dlg3, dlg3.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestEnableSearchPathInPythonEmbeddablePackage_Success()
        {
            // Set up
            var pythonInstaller = GetPythonInstaller();
            pythonInstaller.TestPythonVirtualEnvironmentTaskNames = new List<PythonTaskName>
                { PythonTaskName.enable_search_path_in_python_embeddable_package };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });
            try
            {
                CreateDirectory(pythonInstaller.PythonEmbeddablePackageExtractDir);
                CreateFile(pythonInstaller.PythonEmbeddablePackageExtractDir, @"python312._pth");
            }
            catch (Exception)
            {
                CleanupDir(PythonInstallerUtil.PythonRootDir);
                throw;
            }

            // Test
            var dlg = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment, dlg.Message));
            RunUI(() => Assert.IsTrue(File.Exists(Path.Combine(pythonInstaller.PythonEmbeddablePackageExtractDir, @"python312.pth"))));

            OkDialog(dlg, dlg.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestDownloadGetPipScript_Cancel()
        {
            // Set up
            var taskValidator = new TestPythonInstallerTaskValidator();
            taskValidator.SetSuccessUntil(PythonTaskName.enable_search_path_in_python_embeddable_package);
            var pythonInstaller = GetPythonInstaller(taskValidator);
            pythonInstaller.TestPipDownloadClient = new TestAsynchronousDownloadClient
            {
                CancelDownload = true
            };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg1 = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_download_the_get_pip_py_script, dlg1.Message));

            OkDialog(dlg1, dlg1.OkDialog);
            var dlg2 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(Resources.MultiFileAsynchronousDownloadClient_DownloadFileAsyncWithBroker_Download_canceled_, dlg2.Message));

            OkDialog(dlg2, dlg2.OkDialog);
            var dlg3 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment, dlg3.Message));

            OkDialog(dlg3, dlg3.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestDownloadGetPipScript_Fail()
        {
            // Set up
            var taskValidator = new TestPythonInstallerTaskValidator();
            taskValidator.SetSuccessUntil(PythonTaskName.enable_search_path_in_python_embeddable_package);
            var pythonInstaller = GetPythonInstaller(taskValidator);
            pythonInstaller.TestPipDownloadClient = new TestAsynchronousDownloadClient
            {
                CancelDownload = false,
                DownloadSuccess = false
            };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg1 = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_download_the_get_pip_py_script, dlg1.Message));

            OkDialog(dlg1, dlg1.OkDialog);
            var dlg2 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsResources.PythonInstaller_Download_failed__Check_your_network_connection_or_contact_Skyline_team_for_help_, dlg2.Message));

            OkDialog(dlg2, dlg2.OkDialog);
            var dlg3 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment, dlg3.Message));

            OkDialog(dlg3, dlg3.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestDownloadGetPipScript_Success()
        {
            // Set up
            var pythonInstaller = GetPythonInstaller();
            pythonInstaller.TestDownloadClient = new TestAsynchronousDownloadClient
            {
                CancelDownload = false,
                DownloadSuccess = true
            };
            pythonInstaller.TestPythonVirtualEnvironmentTaskNames = new List<PythonTaskName>
                { PythonTaskName.download_getpip_script };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment, dlg.Message));

            OkDialog(dlg, dlg.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestRunGetPipScript_Fail()
        {
            // Set up
            var taskValidator = new TestPythonInstallerTaskValidator();
            taskValidator.SetSuccessUntil(PythonTaskName.download_getpip_script);
            var pythonInstaller = GetPythonInstaller(taskValidator);
            pythonInstaller.TestPipeSkylineProcessRunner = new TestSkylineProcessRunner
            {
                ConnectSuccess = true,
                ExitCode = 1
            };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg1 = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_run_the_get_pip_py_script, dlg1.Message));

            OkDialog(dlg1, dlg1.OkDialog);
            var dlg2 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.IsTrue(dlg2.Message.Contains($@"Failed to execute command")));

            OkDialog(dlg2, dlg2.OkDialog);
            var dlg3 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment, dlg3.Message));

            OkDialog(dlg3, dlg3.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestRunGetPipScript_Success()
        {
            // Set up
            var pythonInstaller = GetPythonInstaller();
            pythonInstaller.TestPipeSkylineProcessRunner = new TestSkylineProcessRunner
            {
                ConnectSuccess = true,
                ExitCode = 0
            };
            pythonInstaller.TestPythonVirtualEnvironmentTaskNames = new List<PythonTaskName>
                { PythonTaskName.run_getpip_script };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment, dlg.Message));

            OkDialog(dlg, dlg.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestPipInstallVirtualenv_Fail()
        {
            // Set up
            var taskValidator = new TestPythonInstallerTaskValidator();
            taskValidator.SetSuccessUntil(PythonTaskName.run_getpip_script);
            var pythonInstaller = GetPythonInstaller(taskValidator);
            pythonInstaller.TestPipeSkylineProcessRunner = new TestSkylineProcessRunner
            {
                ConnectSuccess = true,
                ExitCode = 1
            };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg1 = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(string.Format(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_run_pip_install__0_, @"virtualenv"), dlg1.Message));

            OkDialog(dlg1, dlg1.OkDialog);
            var dlg2 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.IsTrue(dlg2.Message.Contains($@"Failed to execute command")));

            OkDialog(dlg2, dlg2.OkDialog);
            var dlg3 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment, dlg3.Message));

            OkDialog(dlg3, dlg3.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestPipInstallVirtualenv_Success()
        {
            // Set up
            var pythonInstaller = GetPythonInstaller();
            pythonInstaller.TestPipeSkylineProcessRunner = new TestSkylineProcessRunner
            {
                ConnectSuccess = true,
                ExitCode = 0
            };
            pythonInstaller.TestPythonVirtualEnvironmentTaskNames = new List<PythonTaskName>
                { PythonTaskName.pip_install_virtualenv };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment, dlg.Message));

            OkDialog(dlg, dlg.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestCreateVirtualEnvironment_Fail()
        {
            // Set up
            var taskValidator = new TestPythonInstallerTaskValidator();
            taskValidator.SetSuccessUntil(PythonTaskName.pip_install_virtualenv);
            var pythonInstaller = GetPythonInstaller(taskValidator);
            pythonInstaller.TestPipeSkylineProcessRunner = new TestSkylineProcessRunner
            {
                ConnectSuccess = true,
                ExitCode = 1
            };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg1 = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(
                string.Format(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_create_virtual_environment__0_, @"test"), dlg1.Message));

            OkDialog(dlg1, dlg1.OkDialog);
            var dlg2 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.IsTrue(dlg2.Message.Contains($@"Failed to execute command")));

            OkDialog(dlg2, dlg2.OkDialog);
            var dlg3 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment, dlg3.Message));

            OkDialog(dlg3, dlg3.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestCreateVirtualEnvironment_Success()
        {
            // Set up
            var pythonInstaller = GetPythonInstaller();
            pythonInstaller.TestPipeSkylineProcessRunner = new TestSkylineProcessRunner
            {
                ConnectSuccess = true,
                ExitCode = 0
            };
            pythonInstaller.TestPythonVirtualEnvironmentTaskNames = new List<PythonTaskName>
                { PythonTaskName.create_virtual_environment };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment, dlg.Message));

            OkDialog(dlg, dlg.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestPipInstallPackages_Fail()
        {
            // Set up
            var taskValidator = new TestPythonInstallerTaskValidator();
            taskValidator.SetSuccessUntil(PythonTaskName.create_virtual_environment);
            var pythonInstaller = GetPythonInstaller(taskValidator);
            pythonInstaller.TestPipeSkylineProcessRunner = new TestSkylineProcessRunner
            {
                ConnectSuccess = true,
                ExitCode = 1
            };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg1 = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(
                string.Format(ToolsResources.PythonInstaller_GetPythonTask_Failed_to_install_Python_packages_in_virtual_environment__0_, @"test"), dlg1.Message));

            OkDialog(dlg1, dlg1.OkDialog);
            var dlg2 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.IsTrue(dlg2.Message.Contains($@"Failed to execute command")));

            OkDialog(dlg2, dlg2.OkDialog);
            var dlg3 = WaitForOpenForm<MessageDlg>();
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment, dlg3.Message));

            OkDialog(dlg3, dlg3.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestPipInstallPackages_Success()
        {
            // Set up
            var pythonInstaller = GetPythonInstaller();
            pythonInstaller.TestPipeSkylineProcessRunner = new TestSkylineProcessRunner
            {
                ConnectSuccess = true,
                ExitCode = 0
            };
            pythonInstaller.TestPythonVirtualEnvironmentTaskNames = new List<PythonTaskName>
                { PythonTaskName.pip_install_packages };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });

            // Test
            var dlg = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment, dlg.Message));

            OkDialog(dlg, dlg.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static void TestStartToFinishWithVirtualEnvironment_Success()
        {
            // Set up
            var pythonInstaller = GetPythonInstaller();
            pythonInstaller.TestDownloadClient = new TestAsynchronousDownloadClient
            {
                CancelDownload = false,
                DownloadSuccess = true
            };
            pythonInstaller.TestPipeSkylineProcessRunner = new TestSkylineProcessRunner
            {
                ConnectSuccess = true,
                ExitCode = 0
            };
            var pythonInstallerDlg = ShowDialog<PythonInstallerDlg>(
                () =>
                {
                    using var dlg = new PythonInstallerDlg(pythonInstaller);
                    dlg.ShowDialog(SkylineWindow);
                });
            try
            {
                var fileName = $@"python312._pth";
                var filePath = CreateFile(PythonInstallerUtil.PythonRootDir, fileName);
                CreateZipFile(pythonInstaller.PythonEmbeddablePackageDownloadPath, (fileName, filePath));
            }
            catch (Exception)
            {
                CleanupDir(PythonInstallerUtil.PythonRootDir);
                throw;
            }

            // Test
            var dlg = ShowDialog<MessageDlg>(pythonInstallerDlg.OkDialog);
            RunUI(() => Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment, dlg.Message));
            RunUI(() => Assert.AreEqual(8, pythonInstaller.NumTotalTasks));
            RunUI(() => Assert.AreEqual(8, pythonInstaller.NumCompletedTasks));

            OkDialog(dlg, dlg.OkDialog);
            WaitForClosedForm(pythonInstallerDlg);

            // Tear down
            CleanupDir(PythonInstallerUtil.PythonRootDir);
        }

        private static PythonInstaller GetPythonInstaller()
        {
            return GetPythonInstaller(new TestPythonInstallerTaskValidator());
        }

        private static PythonInstaller GetPythonInstaller(IPythonInstallerTaskValidator taskValidator)
        {
            var packages = new List<PythonPackage>()
            {
                new PythonPackage {Name = @"peptdeep", Version = null },
                new PythonPackage {Name = @"numpy", Version = @"1.26.4" }
            };
            var programContainer = new ProgramPathContainer(PYTHON, VERSION);
            return new PythonInstaller(programContainer, packages, new TextBoxStreamWriterHelper(), taskValidator, @"test");
        }

        private static string CreateFile(string dir, string fileName, string content = "")
        {
            var path = Path.Combine(dir, fileName);
            var buffer = new UTF8Encoding(true).GetBytes(content);
            using var fileStream = File.Create(path);
            fileStream.Write(buffer, 0, buffer.Length);
            return path;
        }

        private static void CreateZipFile(string outputFilePath, params (string entryName, string filePath)[] inputFileTuples)
        {
            using var zipFile = new ZipFile();
            foreach (var inputFileTuple in inputFileTuples)
            {
                var entry = zipFile.AddFile(inputFileTuple.filePath);
                entry.FileName = inputFileTuple.entryName;
            }
            zipFile.Save(outputFilePath);
        }

        private static void CreateDirectory(string dir)
        {
            if (Directory.Exists(dir))
            {
                return;
            }
            Directory.CreateDirectory(dir);
        }

        private static void CleanupDir(string dir)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
