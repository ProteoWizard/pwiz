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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf
{

    [TestClass]
    public class AlphapeptdeepBuildLibraryTest : AbstractFunctionalTestEx
    {
        /// <summary>
        /// When true Python installation is forced by deleting any old installation
        /// </summary>
        private bool IsCleanPythonMode => true;

        /// <summary>
        /// When true console output is added to clarify what the test has accomplished
        /// </summary>
        public bool IsVerboseMode => false;

        /// <summary>
        /// When true the test write the Python hash value for <see cref="Settings.PythonEmbeddableHash"/>
        /// </summary>
        protected override bool IsRecordMode => false;

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)] // Maybe all the setup dependencies also
        public void TestAlphaPeptDeepBuildLibrary()
        {
            if (IsCleanPythonMode)
                AssertEx.IsTrue(PythonInstaller.DeleteToolsPythonDirectory());

            TestFilesZip = "TestPerf/AlphapeptdeepBuildLibraryTest.zip";
            var originalInstallationState = PythonInstaller.SimulatedInstallationState;
            try
            {
                PythonInstaller.SimulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD;
                RunFunctionalTest();
            }
            finally
            {
                PythonInstaller.SimulatedInstallationState = originalInstallationState;
            }
        }

        private string _toolName = AlphapeptdeepLibraryBuilder.ALPHAPEPTDEEP;
        private string _pythonVersion = AlphapeptdeepLibraryBuilder.PythonVersion;

        private bool _undoRegistry;

        private string LibraryPathWithoutIrt =>
            TestFilesDir.GetTestPath("LibraryWithoutIrt.blib");

        private string LibraryPathWithIrt =>
            TestFilesDir.GetTestPath("LibraryWithIrt.blib");

        protected override void DoTest()
        {
            TestEmptyDocumentMessage();
            
            RunUI(() => OpenDocument(TestFilesDir.GetTestPath(@"Rat_plasma.sky")));

            const string answerWithoutIrt = "without_iRT/predict_transformed.speclib.tsv";
            const string libraryWithoutIrt = "AlphaPeptDeepLibraryWithoutIrt";

            const string libraryWithIrt = "AlphaPeptDeepLibraryWithIrt";
            const string answerWithIrt = "with_iRT/predict_transformed.speclib.tsv";

            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);

            var simulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT; // Simulates not having Nvidia library but having the GPU
            AlphapeptdeepBuildLibrary(peptideSettings, libraryWithIrt, LibraryPathWithIrt, answerWithIrt, 
                simulatedInstallationState, IrtStandard.BIOGNOSYS_11);

            simulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD; // Simulates not having Nvidia GPU
            AlphapeptdeepBuildLibrary(peptideSettings, libraryWithoutIrt, LibraryPathWithoutIrt, answerWithoutIrt, 
                simulatedInstallationState);

            var fileHash = PythonInstallerUtil.GetMD5FileHash(PythonInstaller.PythonEmbeddablePackageDownloadPath);
            if (IsRecordMode)
                Console.WriteLine($@"Computed PythonEmbeddableHash: {fileHash}");
            Assert.AreEqual(Settings.Default.PythonEmbeddableHash, fileHash);

            OkDialog(peptideSettings, peptideSettings.OkDialog);

            var addRtStdDlg = WaitForOpenForm<AddIrtStandardsToDocumentDlg>();
            OkDialog(addRtStdDlg, addRtStdDlg.CancelDialog);

            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                spectralLibraryViewer.ChangeSelectedLibrary(libraryWithoutIrt);
                spectralLibraryViewer.ChangeSelectedLibrary(libraryWithIrt);
            });

            OkDialog(spectralLibraryViewer, spectralLibraryViewer.Close);

            var saveChangesDlg =
                ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.NewDocument(), WAIT_TIME);
            AssertEx.AreComparableStrings(SkylineResources.SkylineWindow_CheckSaveDocument_Do_you_want_to_save_changes,
                saveChangesDlg.Message);
            OkDialog(saveChangesDlg, saveChangesDlg.ClickNo);

            TestFilesDir.CheckForFileLocks(TestFilesDir.FullPath);
        }

        private void TestEmptyDocumentMessage()
        {
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);

            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = "No peptides prediction";
                buildLibraryDlg.LibraryPath = LibraryPathWithoutIrt;
                buildLibraryDlg.AlphaPeptDeep = true;
            });

            RunDlg<MessageDlg>(buildLibraryDlg.OkWizardPage, dlg =>
            {
                Assert.AreEqual(SettingsUIResources.BuildLibraryDlg_CreateAlphaBuilder_Add_peptide_precursors_to_the_document_to_build_a_library_from_AlphaPeptDeep_predictions_,
                    dlg.Message);
                dlg.OkDialog();
            });

            OkDialog(buildLibraryDlg, buildLibraryDlg.CancelDialog);
            OkDialog(peptideSettings, peptideSettings.OkDialog);
        }

        /// <summary>
        /// Test goes through building of a Library by AlphaPeptDeep with or without iRT
        /// </summary>
        /// <param name="peptideSettings">Open PeptideSettingsUI Dialog object</param>
        /// <param name="libraryName">Name of the library to build</param>
        /// <param name="libraryPath">Path of the library to build</param>
        /// <param name="answerFile">Path to library answersheet</param>
        /// <param name="simulatedInstallationState">Python Simulated State helps determine whether user is offered Nvidia install</param>
        /// <param name="iRTtype">iRT standard type</param>
        private void AlphapeptdeepBuildLibrary(PeptideSettingsUI peptideSettings, string libraryName,
            string libraryPath, string answerFile, 
            PythonInstaller.eSimulatedInstallationState simulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT,
            IrtStandard iRTtype = null)
        {
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryName;
                buildLibraryDlg.LibraryPath = libraryPath;
                buildLibraryDlg.AlphaPeptDeep = true;
                if (iRTtype != null)
                    buildLibraryDlg.IrtStandard = iRTtype;
            });

            if (simulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT) 
            {
                // TestCancelPython always uses NAIVE state so must reset state
                TestCancelPython(buildLibraryDlg);
                PythonInstaller.SimulatedInstallationState = simulatedInstallationState;
                var confirmDlg = TestNvidiaInstallPython(buildLibraryDlg);
                if (confirmDlg != null)
                    OkDialog(confirmDlg, confirmDlg.OkDialog);
            }
            else
            {
                PythonInstaller.SimulatedInstallationState = simulatedInstallationState;
                RunUI(buildLibraryDlg.OkWizardPage);
            }

            if (buildLibraryDlg.Builder == null)
            {
                // Recreate builder when running the test multiple times (e.g. in different lanquages)
                RunUI(() =>
                {
                    PythonInstaller.SimulatedInstallationState =
                        PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD;
                    buildLibraryDlg.ValidateBuilder(true);
                });
                PythonInstaller.SimulatedInstallationState = simulatedInstallationState;
            }

            WaitForCondition(() => buildLibraryDlg.Builder != null);

            var alphaPeptDeepBuilder = (AlphapeptdeepLibraryBuilder) buildLibraryDlg.Builder;
            Assert.IsNotNull(alphaPeptDeepBuilder);
            string builtLibraryPath = alphaPeptDeepBuilder.TransformedOutputSpectraLibFilepath;

            var limitedModelAlert = WaitForOpenForm<AlertDlg>();
            Assert.AreEqual(string.Format(ModelResources.Alphapeptdeep_Warn_limited_modification, @"Phospho (ST)".Indent(1)), limitedModelAlert.Message);
            OkDialog(limitedModelAlert, limitedModelAlert.OkDialog);
            if (iRTtype != null)
            {
                VerifyAddIrts(WaitForOpenForm<AddIrtPeptidesDlg>());
                var recalibrateIrtDlg = WaitForOpenForm<MultiButtonMsgDlg>();
                StringAssert.StartsWith(recalibrateIrtDlg.Message,
                    Resources
                        .LibraryGridViewDriver_AddToLibrary_Do_you_want_to_recalibrate_the_iRT_standard_values_relative_to_the_peptides_being_added_);
                OkDialog(recalibrateIrtDlg, recalibrateIrtDlg.ClickNo);
                var addRtPredDlg = WaitForOpenForm<AddRetentionTimePredictorDlg>();
                OkDialog(addRtPredDlg, addRtPredDlg.OkDialog);
            }

            WaitForClosedForm<BuildLibraryDlg>();
            WaitForCondition(() => File.Exists(builtLibraryPath));

            AssertEx.IsFalse(alphaPeptDeepBuilder.FractionOfExpectedOutputLinesGenerated > 2,
                @"TestAlphaPeptDeepBuildLibrary: Total count of generated output is more than twice of the expected count ... ");
            AssertEx.IsFalse(alphaPeptDeepBuilder.FractionOfExpectedOutputLinesGenerated < 0.5,
                @"TestAlphaPeptDeepBuildLibrary: Total count of generated output is less than half of the expected count ... ");
            
            TestResultingLibByValues(TestFilesDir.GetTestPath(answerFile), builtLibraryPath);
        }

        private static void VerifyAddIrts(AddIrtPeptidesDlg dlg)
        {
            RunUI(() =>
            {
                Assert.AreEqual(6, dlg.PeptidesCount);
                Assert.AreEqual(1, dlg.RunsConvertedCount); // Libraries now convert through internal alignment to single RT scale
                Assert.AreEqual(0, dlg.RunsFailedCount);
            });

            VerifyRegression(dlg, 0, true, 11, 0, 0);

            OkDialog(dlg, dlg.OkDialog);
        }

        private static void VerifyRegression(AddIrtPeptidesDlg dlg, int index, bool converted, int numPoints,
            int numMissing, int numOutliers)
        {
            RunUI(() => Assert.AreEqual(converted, dlg.IsConverted(index)));
            var regression = ShowDialog<GraphRegression>(() => dlg.ShowRegression(index));
            RunUI(() =>
            {
                Assert.AreEqual(1, regression.RegressionGraphDatas.Count);
                var data = regression.RegressionGraphDatas.First();
                Assert.IsTrue(data.XValues.Length == data.YValues.Length);
                Assert.AreEqual(numPoints, data.XValues.Length);
                Assert.AreEqual(numMissing, data.MissingIndices.Count);
                Assert.AreEqual(numOutliers, data.OutlierIndices.Count);
            });
            OkDialog(regression, regression.CloseDialog);
        }

        private void TestResultingLibByValues(string answer, string product)
        {
            var sortFields = new List<(int FieldIndex, bool IsAscending)>
            {
                (0, true),  // ModifiedPeptideSequence 
                (7, true),  // FragmentType
                (10, true)  // FragmentCharge
            };
            var product_sorted = product + ".sorted";
            var answer_sorted = answer + ".sorted";

            DelimitedFileSorter.SortDelimitedFile(
                inputFilePath: product,
                outputFilePath: product_sorted,
                delimiter: TextUtil.SEPARATOR_TSV,
                sortFields: sortFields,
                hasHeader: true);

            DelimitedFileSorter.SortDelimitedFile(
                inputFilePath: answer,
                outputFilePath: answer_sorted,
                delimiter: TextUtil.SEPARATOR_TSV,
                sortFields: sortFields,
                hasHeader: true);

            using (var answerReader = new StreamReader(answer_sorted))
            using (var productReader = new StreamReader(product_sorted))
            {
                AssertEx.FieldsEqual(answerReader, productReader, 13, null, true, 0, 1);
            }
        }

        /// <summary>
        /// Pretends Python needs installation then Cancels Python install
        /// </summary>
        /// <param name="buildLibraryDlg">Build Library dialog</param>
        public void TestCancelPython(BuildLibraryDlg buildLibraryDlg)
        {
            if (IsVerboseMode)
            {
                Console.WriteLine();
                Console.WriteLine(@"TestAlphaPeptDeepBuildLibrary: Start TestCancelPython() test ... ");
            }
            // Test the control path where Python is not installed, and the user is prompted to deal with admin access
            PythonInstaller.SimulatedInstallationState =
                PythonInstaller.eSimulatedInstallationState.NAIVE; // Simulates not having the needed registry settings
            var installPythonDlg = ShowDialog<MultiButtonMsgDlg>(buildLibraryDlg.OkWizardPage); // Expect the offer to install Python

            CancelDialog(installPythonDlg, installPythonDlg.CancelDialog); // Cancel it immediately

            installPythonDlg =
                ShowDialog<MultiButtonMsgDlg>(buildLibraryDlg.OkWizardPage); // Expect the offer to install Python

            AssertEx.AreComparableStrings(
                ToolsUIResources.PythonInstaller_BuildPrecursorTable_Python_0_installation_is_required,
                installPythonDlg.Message);

            var needAdminDlg = ShowDialog<MessageDlg>(installPythonDlg.OkDialog);

            AssertEx.AreComparableStrings(ToolsUIResources.PythonInstaller_Requesting_Administrator_elevation,
                needAdminDlg.Message);

            CancelDialog(needAdminDlg, needAdminDlg.CancelDialog);
            if (IsVerboseMode)
                Console.WriteLine(@"TestAlphaPeptDeepBuildLibrary: Finish TestCancelPython() test ... ");
        }

        public MessageDlg TestNvidiaInstallPython(BuildLibraryDlg buildLibraryDlg)
        {
            if (IsVerboseMode)
                Console.WriteLine(@"TestAlphaPeptDeepBuildLibrary: Start TestNvidiaInstallPython() test ... ");
            // Test the control path where Nvidia Card is Available and Nvidia Libraries are not installed, and the user is prompted to deal with Nvidia
            // Test for LongPaths not set and admin
            if (PythonInstaller.IsRunningElevated() && !PythonInstaller.ValidateEnableLongpaths())
            {
                var adminDlg = ShowDialog<MessageDlg>(buildLibraryDlg.OkWizardPage,
                    WAIT_TIME); // Expect request for elevated privileges 
                // var adminDlg = WaitForOpenForm<MessageDlg>();
                AssertEx.AreComparableStrings(ToolsUIResources.PythonInstaller_Requesting_Administrator_elevation,
                    adminDlg.Message);
                OkDialog(adminDlg, adminDlg.OkDialog);
            }
            else if (!PythonInstaller.ValidateEnableLongpaths())
            {
                Assert.Fail($@"Error: Cannot finish {_toolName}BuildLibraryTest because {PythonInstaller.REG_FILESYSTEM_KEY}\{PythonInstaller.REG_LONGPATHS_ENABLED} is not set and have insufficient permissions to set it");
            }
            else
            {
                ShowDialog<MessageDlg>(buildLibraryDlg.OkWizardPage, WAIT_TIME); // Expect the offer to installNvidia
            }

            var installNvidiaDlg = WaitForOpenForm<MessageDlg>();

            AssertEx.AreComparableStrings(ToolsUIResources.PythonInstaller_Install_Nvidia_Library,
                installNvidiaDlg.Message);

            CancelDialog(installNvidiaDlg, installNvidiaDlg.CancelDialog);

            installNvidiaDlg = ShowDialog<MessageDlg>(buildLibraryDlg.OkWizardPage, WAIT_TIME);
            AssertEx.AreComparableStrings(ToolsUIResources.PythonInstaller_Install_Nvidia_Library,
                installNvidiaDlg.Message);
            
            OkDialog(installNvidiaDlg, installNvidiaDlg.ClickYes);

            var needAdminDlg = WaitForOpenForm<MessageDlg>();
            
            AssertEx.AreComparableStrings(ModelResources.NvidiaInstaller_Requesting_Administrator_elevation,
                needAdminDlg.Message);

            CancelDialog(needAdminDlg, needAdminDlg.CancelDialog); // Expect the offer to installNvidia
            installNvidiaDlg = ShowDialog<MessageDlg>(buildLibraryDlg.OkWizardPage, WAIT_TIME); // 3 minutes 
            AssertEx.AreComparableStrings(ToolsUIResources.PythonInstaller_Install_Nvidia_Library,
                installNvidiaDlg.Message);
            
            // Python installation begins when user ClickNo
            OkDialog(installNvidiaDlg, installNvidiaDlg.ClickNo);
            
            if (!IsCleanPythonMode)
                return null;

            var pythonConfirm = WaitForOpenForm<MessageDlg>(WAIT_TIME * 4); // 12 minutes - successful completion message
            if (IsVerboseMode)
                Console.WriteLine(@"TestAlphaPeptDeepBuildLibrary: Finish TestNvidiaInstallPython() test ... ");
            return pythonConfirm;
        }

        /// <summary>
        /// Pretends no NVIDIA hardware then Installs Python, returns true if Python installer ran (successful or not), false otherwise
        /// </summary>
        /// <param name="buildLibraryDlg">Build Library</param>
        public bool InstallPython(BuildLibraryDlg buildLibraryDlg)
        {
            PythonInstaller.SimulatedInstallationState =
                PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD; // Normal tests systems will have registry set suitably

            MessageDlg confirmDlg = null;
            RunLongDlg<MultiButtonMsgDlg>(buildLibraryDlg.OkWizardPage, pythonDlg =>
            {
                Assert.AreEqual(string.Format(
                    ToolsUIResources.PythonInstaller_BuildPrecursorTable_Python_0_installation_is_required,
                    _pythonVersion, _toolName), pythonDlg.Message);

                if (!PythonInstaller.ValidateEnableLongpaths())
                {
                    var longPathDlg = ShowDialog<MessageDlg>(pythonDlg.OkDialog);

                    Assert.AreEqual(
                        string.Format(ToolsUIResources.PythonInstaller_Requesting_Administrator_elevation),
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
                    confirmDlg = ShowDialog<MessageDlg>(pythonDlg.OkDialog, WAIT_TIME);
                    ConfirmPythonSuccess(confirmDlg);
                }


            }, dlg => dlg.Close());
            if (_undoRegistry)
            {
                PythonInstaller.EnableWindowsLongPaths(false);
            }

            return true;
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
        private void RunNvidiaDialog(MessageDlg nvidiaDlg, MessageDlg pythonDlg, bool? clickNo = true)
        {
            if (clickNo == true)
            {
                RunDlg<AlertDlg>(nvidiaDlg.ClickNo, ConfirmPythonSuccess);
            }
            else if (clickNo == false)
            {
                RunDlg<AlertDlg>(nvidiaDlg.ClickYes, ConfirmPythonSuccess);
            }
            else // clickNo == null
            {
                RunDlg<AlertDlg>(nvidiaDlg.ClickCancel, ConfirmPythonSuccess);
            }

            if (!nvidiaDlg.IsDisposed)
                nvidiaDlg.Dispose();
        }

        /// <summary>
        /// Helps with Nvidia GPU Detections
        /// </summary>
        /// <param name="pythonDlg">Python set up is required dialog</param>
        /// <param name="nvidiaClickNo">What to tell Nvidia Dialog: Yes=install, No=don't install, null=cancel operation</param>
        private void NvidiaTestHelper(MessageDlg pythonDlg, bool? nvidiaClickNo)
        {
            PythonInstaller.SimulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT;
            if (PythonInstaller.TestForNvidiaGPU() == true && !PythonInstaller.NvidiaLibrariesInstalled())
            {
                Console.WriteLine(@"Info: NVIDIA GPU DETECTED on test node");

                MessageDlg nvidiaDlg = ShowDialog<MessageDlg>(pythonDlg.OkDialog, WAIT_TIME);

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
        public void ConfirmPython(AlertDlg confirmDlg, bool confirmSuccess = true)
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
            AssertEx.AreComparableStrings(
                string.Format(ModelResources.NvidiaInstaller_Requesting_Administrator_elevation,
                    PythonInstaller.InstallNvidiaLibrariesBat),
                confirmDlg.Message);
            OkDialog(confirmDlg, confirmDlg.OkDialog);
        }
    }
}

public class DelimitedFileSorter
{
    public static void SortDelimitedFile(
        string inputFilePath,
        string outputFilePath,
        char delimiter,
        List<(int FieldIndex, bool IsAscending)> sortFields,
        bool hasHeader = true)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrEmpty(inputFilePath) || !File.Exists(inputFilePath))
                throw new ArgumentException("Input file does not exist or is invalid.");
            if (string.IsNullOrEmpty(outputFilePath))
                throw new ArgumentException("Output file path is invalid.");
            if (sortFields == null || !sortFields.Any())
                throw new ArgumentException("At least one sort field must be specified.");
            if (sortFields.Any(sf => sf.FieldIndex < 0))
                throw new ArgumentException("Field indices must be non-negative.");

            // Read all lines
            var lines = File.ReadAllLines(inputFilePath);
            if (lines.Length == 0)
                throw new InvalidOperationException("Input file is empty.");

            // Store header if present
            string header = hasHeader ? lines[0] : null;
            var dataLines = hasHeader ? lines.Skip(1).ToList() : lines.ToList();

            // Sort data
            var sortedLines = Enumerable.OrderBy(
                dataLines.Select(line => line.Split(delimiter)),
                fields => fields, // Use fields as the key
                new FieldComparer(sortFields)).Select(fields => string.Join(delimiter.ToString(), fields));

            // Write to output file
            using (var writer = new StreamWriter(outputFilePath))
            {
                if (hasHeader)
                    writer.WriteLine(header);
                foreach (var line in sortedLines)
                    writer.WriteLine(line);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sorting file: {ex}");
            throw;
        }
    }

    private class FieldComparer : IComparer<string[]>
    {
        private readonly List<(int FieldIndex, bool IsAscending)> _sortFields;

        public FieldComparer(List<(int FieldIndex, bool IsAscending)> sortFields)
        {
            _sortFields = sortFields;
        }

        public int Compare(string[] x, string[] y)
        {
            foreach ((int fieldIndex, bool isAscending) in _sortFields)
            {
                string xValue = x != null && fieldIndex < x.Length ? x[fieldIndex] : string.Empty;
                string yValue = y != null && fieldIndex < y.Length ? y[fieldIndex] : string.Empty;

                if (double.TryParse(xValue, out double xNum) && double.TryParse(yValue, out double yNum))
                {
                    int comparison = xNum.CompareTo(yNum);
                    if (comparison != 0)
                        return isAscending ? comparison : -comparison;
                }
                else
                {
                    int comparison = string.Compare(xValue, yValue, StringComparison.OrdinalIgnoreCase);
                    if (comparison != 0)
                        return isAscending ? comparison : -comparison;
                }
            }
            return 0;
        }
    }
}