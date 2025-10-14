/*
 * Author: David Shteynberg <david.shteynberg at proton.me>,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.Carafe;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TestPerf
{
    [TestClass]
    public class CarafeBuildLibraryTest : AbstractFunctionalTestEx
    {
        private double MZ_TOLERANCE = 1e-4; 
        // private double INTENSITY_TOLERANCE = 1.5e-1;
        private double MIN_COSINE_ANGLE = 0.9;

        /// <summary>
        /// When true Python installation is forced by deleting any old installation
        /// </summary>
        private bool IsCleanPythonMode => false;

        private bool RunExtendedTest => true;

        private bool DisableLongPathsRegistry => false;

        /// <summary>
        /// When true console output is added to clarify what the test has accomplished
        /// </summary>
        public bool IsVerboseMode => false;

        /// <summary>
        /// When true the test write the Python hash value for <see cref="Settings.PythonEmbeddableHash"/>
        /// </summary>
        protected override bool IsRecordMode => false;

        public string LogOutput => TestContext.GetTestResultsPath("TestConsole.log");
        private const string TESTDATA_FILE = @"CarafeBuildLibraryTestSmall.zip";

        private string _toolName = CarafeLibraryBuilder.CARAFE;
        private string _pythonVersion = CarafeLibraryBuilder.PythonVersion;

        private void PrintEnvironment()
        {
            string lines = "";
            var environmentVars = Environment.GetEnvironmentVariables();
            foreach (System.Collections.DictionaryEntry env in environmentVars)
            {
                lines += $"Key: {env.Key}, Value: {env.Value}\n";
            }
            File.WriteAllText(LogOutput, lines);
        }

        [TestMethod]
        public void TestCarafeBuildLibrary()
        {
            TestCarafeBuildLibrary(Enum.GetValues(typeof(TestLibrary)).Cast<TestLibrary>().ToArray());
        }

        [TestMethod]
        public void TestCarafeBuildLibraryShort()
        {
            TestCarafeBuildLibrary(TestLibrary.LibraryTunedBySkyline);
        }

        private void TestCarafeBuildLibrary(params TestLibrary[] libraryNames)
        {
            _testLibraries = libraryNames.ToHashSet();
            if (IsRecordMode)
            {
                TestContext.EnsureTestResultsDir();
                PrintEnvironment();
            }

            if (IsCleanPythonMode)
                AssertEx.IsTrue(PythonInstaller.DeleteToolsPythonDirectory());

            TestFilesZip = GetPerfTestDataURL(TESTDATA_FILE);
            TestFilesPersistent = new[] { "Lumos_8mz_staggered_reCID_human_small" };

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

        private enum TestLibrary
        {
            LibraryTunedBySkyline,
            LibraryTunedByDiann,
            LibraryTunedBySkylineIrt
        }
        private HashSet<TestLibrary> _testLibraries;
        string DiannFineTuneFile => TestFilesDir.GetTestPath(@"report.tsv");
        string MzMLFile => TestFilesDir.GetTestPath(@"Lumos_8mz_staggered_reCID_human_small\Crucios_20240320_CH_15_HeLa_CID_27NCE_01.mzML");
        private string SkyFineTuneFile => SkyTestFile;
        private string SkyTestFile => TestFilesDir.GetTestPath(@"Lumos_8mz_staggered_reCID_human_small\Lumos_8mz_staggered_reCID_human.sky");

        protected override void DoTest()
        {
            LongTest();

            if (IsCleanPythonMode)
                AssertEx.IsTrue(PythonInstaller.DeleteToolsPythonDirectory());
        }
        private void LongTest() 
        {
            // DirectoryEx.SafeDelete(TestContext.GetTestPath(@"TestCarafeBuildLibrary\"));
            Directory.CreateDirectory(TestFilesDir.GetTestPath(@"TestCarafeBuildLibrary\"));

            OpenDocument(TestFilesDir.GetTestPath(SkyTestFile));

            string builtLibraryBySky = null;
            string builtLibraryByDiann = null;
            string builtLibraryBySkyIrt = null;
            if (_testLibraries.Contains(TestLibrary.LibraryTunedBySkyline))
            {
                builtLibraryBySky = CarafeBuildLibrary(TestLibrary.LibraryTunedBySkyline, MzMLFile, "", SkyFineTuneFile, BuildLibraryDlg.BuildLibraryTargetOptions.currentSkylineDocument, BuildLibraryDlg.LearningOptions.another_doc, TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_bySky.blib"), PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT);
            }

            if (_testLibraries.Contains(TestLibrary.LibraryTunedByDiann))
            {
                builtLibraryByDiann = CarafeBuildLibrary(TestLibrary.LibraryTunedByDiann, MzMLFile, "", DiannFineTuneFile, BuildLibraryDlg.BuildLibraryTargetOptions.currentSkylineDocument, BuildLibraryDlg.LearningOptions.diann_report, TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_byDiann.blib"), PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD);
            }

            if (_testLibraries.Contains(TestLibrary.LibraryTunedBySkylineIrt))
            {
                builtLibraryBySkyIrt = CarafeBuildLibrary(TestLibrary.LibraryTunedBySkylineIrt, MzMLFile, "", SkyFineTuneFile, BuildLibraryDlg.BuildLibraryTargetOptions.currentSkylineDocument, BuildLibraryDlg.LearningOptions.another_doc, TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_bySky_iRT.blib"), PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD, IrtStandard.BIOGNOSYS_11);
                var addRtStdDlg = WaitForOpenForm<AddIrtStandardsToDocumentDlg>();
                OkDialog(addRtStdDlg, addRtStdDlg.CancelDialog);
            }

            var fileHash = PythonInstallerUtil.GetMD5FileHash(PythonInstaller.PythonEmbeddablePackageDownloadPath);

            if (IsRecordMode)
                Console.WriteLine($@"Computed PythonEmbeddableHash: {fileHash}");
            Assert.AreEqual(Settings.Default.PythonEmbeddableHash, fileHash);

            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                foreach (var testLibrary in _testLibraries)
                {
                    spectralLibraryViewer.ChangeSelectedLibrary("Carafe" + testLibrary);
                }
                spectralLibraryViewer.Close();
            });

            var saveChangesDlg =
                ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.NewDocument(), WAIT_TIME);
            AssertEx.AreComparableStrings(SkylineResources.SkylineWindow_CheckSaveDocument_Do_you_want_to_save_changes,
                saveChangesDlg.Message);
            RunUI( () =>
            {
                saveChangesDlg.ClickNo();
                FileStreamManager.Default.CloseAllStreams();
            });
            WaitForCondition(() => !FileStreamManager.Default.HasPooledStreams);

            if (_testLibraries.Contains(TestLibrary.LibraryTunedBySkylineIrt))
            {
                var expected = LibrarySpec.CreateFromPath("answer", TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_bySky_iRT.blib"));
                var result = LibrarySpec.CreateFromPath("testBuilt", builtLibraryBySkyIrt);
                //            AssertEx.LibraryEquivalent(expected, result, MZ_TOLERANCE, INTENSITY_TOLERANCE);
                AssertEx.LibraryEquivalentCosineAngle(expected, result, MZ_TOLERANCE, MIN_COSINE_ANGLE);
            }

            if (_testLibraries.Contains(TestLibrary.LibraryTunedByDiann))
            {
                var expected = LibrarySpec.CreateFromPath("answer", TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_byDiann.blib"));
                var result = LibrarySpec.CreateFromPath("testBuilt", builtLibraryByDiann);
                //            AssertEx.LibraryEquivalent(expected, result, MZ_TOLERANCE, INTENSITY_TOLERANCE);
                AssertEx.LibraryEquivalentCosineAngle(expected, result, MZ_TOLERANCE, MIN_COSINE_ANGLE);
            }

            if (_testLibraries.Contains(TestLibrary.LibraryTunedBySkyline))
            {
                var expected = LibrarySpec.CreateFromPath("answer", TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_bySky.blib"));
                var result = LibrarySpec.CreateFromPath("testBuilt", builtLibraryBySky);
                //            AssertEx.LibraryEquivalent(expected, result, MZ_TOLERANCE, INTENSITY_TOLERANCE);
                AssertEx.LibraryEquivalentCosineAngle(expected, result, MZ_TOLERANCE, MIN_COSINE_ANGLE);
            }

            TestFilesDir.CheckForFileLocks(TestFilesDir.FullPath);
        }

        /// <summary>
        /// Test goes through building of a Library by Carafe with or without iRT.  Returns path to library built/
        /// </summary>
        /// <param name="testLibrary">Enum value for library</param>
        /// <param name="buildTarget">Build library target peptides, current document peptides (or FASTA database if current doc is blank)</param>
        /// <param name="learnFrom">Source of fine tuning document</param>
        /// <param name="answerFile">Answer sheet in the test</param>
        /// <param name="simulatedInstallationState">Python Simulated State helps determine whether user is offered Nvidia install</param>
        /// <param name="iRTtype">iRT standard type</param>
        /// <param name="mzMLFile">MS/MS Data file path</param>
        /// <param name="proteinDatabase">Protein FASTA database</param>
        /// <param name="fineTuneFile">fine tuning file path</param>
        private string CarafeBuildLibrary(TestLibrary testLibrary,
            string mzMLFile, string proteinDatabase, string fineTuneFile,
            BuildLibraryDlg.BuildLibraryTargetOptions buildTarget, BuildLibraryDlg.LearningOptions learnFrom,
            string answerFile,
            PythonInstaller.eSimulatedInstallationState simulatedInstallationState, IrtStandard iRTtype = null)
        {
            var peptideSettings = ShowDialog<PeptideSettingsUI>(() =>
                SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Library));
            bool buildLibraryDlgFinished = false;
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg> (() =>
            {
                peptideSettings.ShowBuildLibraryDlg();
                buildLibraryDlgFinished = true;
            });
            // PauseTest();
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = "Carafe" + testLibrary;
                buildLibraryDlg.LibraryPath = TestFilesDir.GetTestPath(@"TestCarafeBuildLibrary\" + testLibrary + ".blib");
                buildLibraryDlg.ComboBuildLibraryTarget = buildTarget;
                buildLibraryDlg.ComboLearnFrom = learnFrom;
                buildLibraryDlg.Carafe = true;
                if (iRTtype != null) 
                    buildLibraryDlg.IrtStandard = iRTtype;

                buildLibraryDlg.OkWizardPage();
                buildLibraryDlg.OkWizardPage();

                buildLibraryDlg.TextBoxMsMsDataFile = mzMLFile;
                buildLibraryDlg.TextBoxProteinDatabase = proteinDatabase;
                buildLibraryDlg.TextBoxTrainingDoc = fineTuneFile;

                buildLibraryDlg.UnloadTrainingDocument();
                if (proteinDatabase == "" && learnFrom == BuildLibraryDlg.LearningOptions.another_doc)
                    buildLibraryDlg.LoadTrainingDocument(fineTuneFile);
            });
            //PauseTest();
            Assert.AreEqual(buildLibraryDlg.ButtonNextText, @"Finish");
            Assert.IsTrue(buildLibraryDlg.ButtonNextEnabled);
            // Test the control path where Python needs installation and is
            if (simulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT)
            {
                // TestCancelPython always uses NAIVE state so must reset state
                TestCancelPython(buildLibraryDlg);
                PythonInstaller.SimulatedInstallationState = simulatedInstallationState;
                var confirmDlg = TestNvidiaInstallPython(buildLibraryDlg);
                if (confirmDlg != null)
                {
                    OkDialog(confirmDlg, confirmDlg.OkDialog);
                }
                else
                {
                    PythonInstaller.SimulatedInstallationState =
                        PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD;
                    //RunUI(buildLibraryDlg.OkWizardPage);
                }
            }
            else
            {
                PythonInstaller.SimulatedInstallationState = simulatedInstallationState;
                RunUI(buildLibraryDlg.OkWizardPage);
            }

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

            const int waitTime = 30 * 60 * 1000;
            WaitForCondition(waitTime, () => buildLibraryDlgFinished || null != FindOpenForm<MessageDlg>());
            var messageDlg = FindOpenForm<MessageDlg>();
            if (messageDlg != null)
            {
                Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment, messageDlg.Message);
                OkDialog(messageDlg, messageDlg.OkDialog);
            }
            WaitForCondition(waitTime, () => buildLibraryDlgFinished);

            var carafeLibraryBuilder = (CarafeLibraryBuilder)buildLibraryDlg.Builder;
            string builtLibraryPath = carafeLibraryBuilder.CarafeOutputLibraryFilePath;

            WaitForCondition(() => File.Exists(builtLibraryPath));
            WaitForCondition(() =>
            {
                try
                {
                    using (FileStream fs = File.Open(builtLibraryPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        return true; // File is accessible and not locked
                    }
                }
                catch (Exception)
                {
                    // ignored
                }

                return false;
            });
            WaitForClosedForm<LongWaitDlg>();

            OkDialog(peptideSettings, peptideSettings.OkDialog);

            return builtLibraryPath;
        }



        private static void VerifyAddIrts(AddIrtPeptidesDlg dlg)
        {
            RunUI(() =>
            {
                Assert.AreEqual(100, dlg.PeptidesCount);
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
 
        /// <summary>
        /// Pretends Python needs installation then Cancels Python install
        /// </summary>
        /// <param name="buildLibraryDlg">Build Library dialog</param>
        public void TestCancelPython(BuildLibraryDlg buildLibraryDlg)
        {
            if (IsVerboseMode)
            {
                Console.WriteLine();
                Console.WriteLine(@"TestCarafeBuildLibrary: Start TestCancelPython() test ... ");
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
                Console.WriteLine(@"TestCarafeBuildLibrary: Finish TestCancelPython() test ... ");
        }

        public MessageDlg TestNvidiaInstallPython(BuildLibraryDlg buildLibraryDlg)
        {
            if (IsVerboseMode)
                Console.WriteLine(@"TestCarafeBuildLibrary: Start TestNvidiaInstallPython() test ... ");
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
                Console.WriteLine(@"TestCarafeBuildLibrary: Finish TestNvidiaInstallPython() test ... ");
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
            if (DisableLongPathsRegistry)
            {
                PythonInstaller.EnableWindowsLongPaths(false);
            }

            return true;
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
    }
}