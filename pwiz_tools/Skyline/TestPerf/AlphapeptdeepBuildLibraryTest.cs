/*
 * Author: David Shteynberg <dshteyn .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace TestPerf
{

    [TestClass]
    public class AlphapeptdeepBuildLibraryTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestAlphaPeptDeepBuildLibrary()
        {
            AssertEx.IsTrue(PythonInstaller.DeleteToolsPythonDirectory());
            TestFilesZip = "TestPerf/AlphapeptdeepBuildLibraryTest.zip";
            RunFunctionalTest();
        }

        private string _pythonVersion = BuildLibraryDlg.ALPHAPEPTDEEP_PYTHON_VERSION;
        private string _toolName = @"AlphaPeptDeep";
        private bool _undoRegistry;

        private string LibraryPathWithoutIrt =>
            TestFilesDir.GetTestPath("LibraryWithoutIrt.blib");

        private string LibraryPathWithIrt =>
            TestFilesDir.GetTestPath("LibraryWithoutIrt.blib");

        protected override void DoTest()
        {
            RunUI(() => OpenDocument(TestFilesDir.GetTestPath(@"Rat_plasma.sky")));

            const string answerWithoutIrt = "predict_transformed.speclib.tsv";
            const string libraryWithoutIrt = "AlphaPeptDeepLibraryWithoutIrt";
            const string libraryWithIrt = "AlphaPeptDeepLibraryWithIrt";

            PeptideSettingsUI peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);

            AlphapeptdeepBuildLibrary(peptideSettings,libraryWithoutIrt, LibraryPathWithoutIrt, answerWithoutIrt);
       
            var fileHash = PythonInstallerUtil.GetFileHash(PythonInstaller.PythonEmbeddablePackageDownloadPath);
            Console.WriteLine($@"Computed PythonEmbeddableHash: {fileHash}");
            Assert.AreEqual(Settings.Default.PythonEmbeddableHash, fileHash);

            fileHash = PythonInstallerUtil.GetFilesArrayHash(Directory.GetFiles( PythonInstaller.PythonEmbeddablePackageExtractDir, @"python*.pth"));
            Console.WriteLine($@"Computed SearchPathInPythonEmbeddableHash: {fileHash}");
            Assert.AreEqual(Settings.Default.SearchPathInPythonEmbeddableHash, fileHash);

            OkDialog(peptideSettings, peptideSettings.OkDialog);

            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                spectralLibraryViewer.ChangeSelectedLibrary(libraryWithoutIrt);
                //spectralLibraryViewer.ChangeSelectedLibrary(libraryWithIrt);
            });
            
            OkDialog(spectralLibraryViewer, spectralLibraryViewer.Close);

            MultiButtonMsgDlg saveChangesDlg = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.NewDocument(), WAIT_TIME);
            AssertEx.AreComparableStrings(SkylineResources.SkylineWindow_CheckSaveDocument_Do_you_want_to_save_changes, saveChangesDlg.Message);
            OkDialog(saveChangesDlg, saveChangesDlg.ClickNo);

            TestFilesDir.CheckForFileLocks(TestFilesDir.FullPath);

        }

        /// <summary>
        /// Test goes through building of a Library by AlphaPeptDeep with or without iRT
        /// </summary>
        /// <param name="peptideSettings">Open PeptideSettingsUI Dialog object</param>
        /// <param name="libraryName">Name of the library to build</param>
        /// <param name="libraryPath">Path of the library to build</param>
        /// <param name="answerFile">Path to library answersheet</param>
        /// <param name="iRTtype">iRT standard type</param>
        private void AlphapeptdeepBuildLibrary(PeptideSettingsUI peptideSettings, string libraryName, string libraryPath, string answerFile, IrtStandard iRTtype = null)
        {
            string builtLibraryPath = null;
            RunLongDlg<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg, buildLibraryDlg =>
            {
                RunUI(() =>
                {
                    buildLibraryDlg.LibraryName = libraryName;
                    buildLibraryDlg.LibraryPath = libraryPath;
                    buildLibraryDlg.AlphaPeptDeep = true;
                });

                if (!HavePythonPrerequisite(buildLibraryDlg))
                {
                    CancelPython(buildLibraryDlg);

                    InstallPythonTestNvidia(buildLibraryDlg);
                }
                else
                {
                    RunUI(() => { buildLibraryDlg.OkWizardPage(); });
                    WaitForClosedForm<BuildLibraryDlg>();
                }

                builtLibraryPath = buildLibraryDlg.BuilderLibFilepath;
            }, _ => { });

            TestResultingLibByValues(builtLibraryPath, TestFilesDir.GetTestPath(answerFile));
        }
        
        private void TestResultingLibByValues(string product, string answer)
        {
            using (var answerReader = new StreamReader(answer))
            {
                using (var productReader = new StreamReader(product))
                {
                    AssertEx.FieldsEqual(productReader, answerReader, 13, null, true, 0, 1);
                }
            }
        }


        /// <summary>
        /// Pretends Python needs installation then Cancels Python install
        /// </summary>
        /// <param name="buildLibraryDlg">Build Library dialog</param>
        public void CancelPython(BuildLibraryDlg buildLibraryDlg)
        {
            // Test the control path where Python is not installed, and the user is prompted to deal with admin access
            PythonInstaller.SimulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NAIVE; // Simulates not having the needed registry settings
            MultiButtonMsgDlg installPythonDlg = ShowDialog<MultiButtonMsgDlg>(() => buildLibraryDlg.OkWizardPage()); // Expect the offer to install Python
            //PauseTest("install offer");
            CancelDialog(installPythonDlg, installPythonDlg.CancelDialog); // Cancel it immediately
            //PauseTest("back to wizard");
            installPythonDlg = ShowDialog<MultiButtonMsgDlg>(() => buildLibraryDlg.OkWizardPage()); // Expect the offer to install Python
            // PauseTest("install offer again");
            AssertEx.AreComparableStrings(ToolsUIResources.PythonInstaller_BuildPrecursorTable_Python_0_installation_is_required, installPythonDlg.Message);

            OkDialog(installPythonDlg, installPythonDlg.OkDialog);
            var needAdminDlg = WaitForOpenForm<MessageDlg>(); 
            AssertEx.AreComparableStrings(ToolsUIResources.PythonInstaller_Requesting_Administrator_elevation, needAdminDlg.Message);
            CancelDialog(needAdminDlg, needAdminDlg.CancelDialog);


            // PauseTest("need admin msg");
            // PauseTest("back to wizard");
        }

        public void InstallPythonTestNvidia(BuildLibraryDlg buildLibraryDlg)
        {
            // Test the control path where Nvidia Card is Available and Nvidia Libraries are not installed, and the user is prompted to deal with Nvidia
            PythonInstaller.SimulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT; // Simulates not having Nvidia library but having the GPU
            //Test for LongPaths not set and admin
            if (PythonInstaller.IsRunningElevated() && !PythonInstaller.ValidateEnableLongpaths())
            {
                MessageDlg adminDlg = ShowDialog<MessageDlg>(() => buildLibraryDlg.OkWizardPage(), WAIT_TIME); // Expect request for elevated privileges 
                AssertEx.AreComparableStrings(ToolsUIResources.PythonInstaller_Requesting_Administrator_elevation, adminDlg.Message);
                OkDialog(adminDlg, adminDlg.OkDialog);
            }
            else if (!PythonInstaller.ValidateEnableLongpaths())
            {
                Assert.Fail($@"Error: Cannot finish {_toolName}BuildLibraryTest because {PythonInstaller.REG_FILESYSTEM_KEY}\{PythonInstaller.REG_LONGPATHS_ENABLED} is not set and have insufficient permissions to set it");
            }

            MessageDlg installNvidiaDlg = ShowDialog<MessageDlg>(() => buildLibraryDlg.OkWizardPage(), WAIT_TIME); // Expect the offer to installNvidia
            AssertEx.AreComparableStrings(ToolsUIResources.PythonInstaller_Install_Cuda_Library,
                installNvidiaDlg.Message);
            CancelDialog(installNvidiaDlg, installNvidiaDlg.CancelDialog);
            installNvidiaDlg = ShowDialog<MessageDlg>(() => buildLibraryDlg.OkWizardPage(), WAIT_TIME);
            AssertEx.AreComparableStrings(ToolsUIResources.PythonInstaller_Install_Cuda_Library,
                installNvidiaDlg.Message);
            OkDialog(installNvidiaDlg, installNvidiaDlg.ClickYes);
            var needAdminDlg = WaitForOpenForm<MessageDlg>();
            AssertEx.AreComparableStrings(ModelResources.NvidiaInstaller_Requesting_Administrator_elevation,
                needAdminDlg.Message);
            CancelDialog(needAdminDlg, () => needAdminDlg.CancelDialog()); // Expect the offer to installNvidia
            installNvidiaDlg = ShowDialog<MessageDlg>(() => buildLibraryDlg.OkWizardPage(), WAIT_TIME);
            AssertEx.AreComparableStrings(ToolsUIResources.PythonInstaller_Install_Cuda_Library,
                installNvidiaDlg.Message);
            OkDialog(installNvidiaDlg, installNvidiaDlg.ClickNo);
        }

        /// <summary>
        /// Pretends no NVIDIA hardware then Installs Python, returns true if Python installer ran (successful or not), false otherwise
        /// </summary>
        /// <param name="buildLibraryDlg">Build Library</param>
        /// <returns></returns>
        public bool InstallPython(BuildLibraryDlg buildLibraryDlg)
        {
            PythonInstaller.SimulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD; // Normal tests systems will have registry set suitably

            bool havePythonPrerequisite = false;

            RunUI(() => { havePythonPrerequisite = buildLibraryDlg.PythonRequirementMet(); });
            if (!havePythonPrerequisite)
            {
                MessageDlg confirmDlg = null;
                RunLongDlg<MultiButtonMsgDlg>(buildLibraryDlg.OkWizardPage, pythonDlg =>
                {
                    Assert.AreEqual(string.Format(ToolsUIResources.PythonInstaller_BuildPrecursorTable_Python_0_installation_is_required,
                        _pythonVersion, _toolName), pythonDlg.Message);

                    if (!PythonInstaller.ValidateEnableLongpaths())
                    {
                        MessageDlg longPathDlg = ShowDialog<MessageDlg>(pythonDlg.OkDialog);

                        Assert.AreEqual(string.Format(ToolsUIResources.PythonInstaller_Requesting_Administrator_elevation),
                            longPathDlg.Message);

                        if (PythonInstaller.IsRunningElevated())
                        {
                            confirmDlg = ShowDialog<MessageDlg>(pythonDlg.OkDialog, WAIT_TIME);
                            ConfirmPythonSuccess(confirmDlg);

                        }
                        else
                        {
                            Assert.Fail($@"Error: Cannot finish {_toolName}BuildLibraryTest because {PythonInstaller.REG_FILESYSTEM_KEY}\{PythonInstaller.REG_LONGPATHS_ENABLED} is not set and have insufficient permissions to set it");
                        }
                    }
                    else
                    {
                        Console.WriteLine(@"Info: LongPathsEnabled registry key is already set to 1");
                        OkDialog(pythonDlg, pythonDlg.OkDialog);
                        confirmDlg = WaitForOpenForm<MessageDlg>();
                        ConfirmPythonSuccess(confirmDlg);

                    }


                }, dlg => {
                    dlg.Close();
                });
                if (_undoRegistry)
                {
                    PythonInstaller.DisableWindowsLongPaths();
                }

                return true;
            }

            return false;
        }
        /// <summary>
        /// Checks for Python prerequisite
        /// </summary>
        /// <param name="buildLibraryDlg">Build Library dialog</param>
        /// <returns></returns>
        public bool HavePythonPrerequisite(BuildLibraryDlg buildLibraryDlg)
        {
            bool havePythonPrerequisite = false;
            RunUI(() => { havePythonPrerequisite = buildLibraryDlg.PythonRequirementMet(); });
            return havePythonPrerequisite;
        }

        public bool HaveNvidiaSoftware()
        {
            return PythonInstaller.NvidiaLibrariesInstalled();
        }

        public bool HaveNvidiaHardware()
        {
            return PythonInstaller.TestForNvidiaGPU() == true;
        }
        /// <summary>
        /// Runs Nvidia Dialog
        /// </summary>
        /// <param name="nvidiaDlg">Nvidia Detected Dialog</param>
        /// <param name="pythonDlg">Python Installer Dialog</param>
        /// <param name="clickNo">true clicks No, false clicks Yes, null clicks Cancel to Nvidia Detected Dialog</param>
        private void RunNvidiaDialog(MultiButtonMsgDlg nvidiaDlg, MultiButtonMsgDlg pythonDlg, bool? clickNo = true)
        {
            if (clickNo == true)
            {

                // Say 'No'
                RunDlg<AlertDlg>(nvidiaDlg.ClickNo, confirmDlg => { ConfirmPythonSuccess(confirmDlg); });
            }
            else if (clickNo == false)
            {
                // Say 'Yes'
                RunDlg<AlertDlg>(nvidiaDlg.ClickYes, confirmDlg => { ConfirmPythonSuccess(confirmDlg); });
            }
            else // clickNo == null
            {
                // Say 'Cancel'
                RunDlg<AlertDlg>(nvidiaDlg.ClickCancel, confirmDlg => { ConfirmPythonSuccess(confirmDlg);
                });

            }

            if (!nvidiaDlg.IsDisposed)
                nvidiaDlg.Dispose();
        }

        /// <summary>
        /// Helps with Nvidia GPU Detections
        /// </summary>
        /// <param name="pythonDlg">Python set up is required dialog</param>
        /// <param name="nvidiaClickNo">What to tell Nvidia Dialog: Yes=install, No=don't install, null=cancel operation</param>
        private void NvidiaTestHelper(MultiButtonMsgDlg pythonDlg, bool? nvidiaClickNo)
        {
            PythonInstaller.SimulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT;
            if (PythonInstaller.TestForNvidiaGPU() == true && !PythonInstaller.NvidiaLibrariesInstalled())
            {
                Console.WriteLine(@"Info: NVIDIA GPU DETECTED on test node");

                MultiButtonMsgDlg nvidiaDlg = ShowMultiButtonMsgDlg(pythonDlg.OkDialog, ToolsUIResources.PythonInstaller_Install_Cuda_Library);

                RunNvidiaDialog(nvidiaDlg, pythonDlg, nvidiaClickNo);

            }
            else
            {
                if (PythonInstaller.TestForNvidiaGPU() != true)
                    Console.WriteLine(@"Info: NVIDIA GPU *NOT* DETECTED on test node");
                else
                    Console.WriteLine(@"Info: Nvidia libraries already installed");
                OkDialog(pythonDlg, pythonDlg.OkDialog);
                //Not cancelled
                var confirmDlg = ShowDialog<AlertDlg>(pythonDlg.OkDialog, WAIT_TIME);
                ConfirmPythonSuccess(confirmDlg);

            }
        }

        /// <summary>
        /// Tries to set EnableLongPaths
        /// </summary>
        /// <param name="longPathDlg">EnableLongPaths registry dialog</param>
        /// <returns></returns>
        private MessageDlg RunLongPathsDialog(MessageDlg longPathDlg)
        {
            Console.WriteLine(@"Info: Trying to set LongPathsEnabled registry key to 1");
            OkDialog(longPathDlg, longPathDlg.OkDialog);

            MessageDlg okDlg = ShowDialog<MessageDlg>(longPathDlg.OkDialog);

            Console.WriteLine(@"Info: Successfully set LongPathsEnabled registry key to 1");
            _undoRegistry = true;

            if (!longPathDlg.IsDisposed)
                longPathDlg.Dispose();

            return okDlg;
        }

        /// <summary>
        /// Confirms Python installation success
        /// </summary>
        /// <param name="confirmDlg">Message dialog success</param>
        private void ConfirmPythonSuccess(AlertDlg confirmDlg)
        {
            ConfirmPython(confirmDlg);
        }
        /// <summary>
        /// Confirms Python installation failure
        /// </summary>
        /// <param name="confirmDlg">Message dialog failed</param>
        private void ConfirmPythonFailed(AlertDlg confirmDlg)
        {
            ConfirmPython(confirmDlg, false);
        }

        /// <summary>
        /// Confirms Python installation
        /// </summary>
        /// <param name="confirmDlg">Alert dialog </param>
        /// <param name="confirmSuccess">true for success, false for failure</param>
        private void ConfirmPython(AlertDlg confirmDlg, bool confirmSuccess = true)
        {
            var expectMsg = string.Format(ToolsUIResources
                .PythonInstaller_OkDialog_Failed_to_set_up_Python_virtual_environment);
            if (confirmSuccess)
                expectMsg = string.Format(ToolsUIResources
                    .PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment);

            Assert.AreEqual(expectMsg, confirmDlg.Message);
            confirmDlg.OkDialog();
        }

        /// <summary>
        /// Second dialog after Nvidia is detected to direct user to admin instructions for setting up Nvidia
        /// </summary>
        /// <param name="confirmDlg">Message dialog to the user with admin instructions</param>
        private void ConfirmInstallNvidiaBatMessage(MessageDlg confirmDlg)
        {
            AssertEx.AreComparableStrings(string.Format(ModelResources.NvidiaInstaller_Requesting_Administrator_elevation, PythonInstaller.GetInstallNvidiaLibrariesBat()),
                confirmDlg.Message);
            OkDialog(confirmDlg, confirmDlg.OkDialog);
        }
    }
}
