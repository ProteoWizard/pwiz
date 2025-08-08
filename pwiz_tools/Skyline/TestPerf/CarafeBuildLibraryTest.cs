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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
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

namespace TestPerf
{
    [TestClass]
    public class CarafeBuildLibraryTest : AbstractFunctionalTestEx
    {
        private double MZ_TOLERANCE = 1e-4; 
        private double INTENSITY_TOLERANCE = 1.5e-1;
        /// <summary>
        /// When true Python installation is forced by deleting any old installation
        /// </summary>
        private bool IsCleanPythonMode => true;

        private bool RunExtendedTest => true;

        /// <summary>
        /// When true console output is added to clarify what the test has accomplished
        /// </summary>
        public bool IsVerboseMode => false;

        /// <summary>
        /// When true the test write the Python hash value for <see cref="Settings.PythonEmbeddableHash"/>
        /// </summary>
        protected override bool IsRecordMode => false;

        public string LogOutput => TestContext.GetTestResultsPath("TestConsole.log");

        public IAsynchronousDownloadClient TestDownloadClient { get; set; }
        private const string TESTDATA_URL = @"https://skyline.ms/tutorials/CarafeTestData/";
        private const string TESTDATA_FILE = @"CarafeBuildLibraryTestSmall.zip";
        private const string TESTDATA_DIR = @"TestPerf";

        private string _toolName = CarafeLibraryBuilder.CARAFE;
        private string _pythonVersion = CarafeLibraryBuilder.PythonVersion;

        private bool _undoRegistry;
        public Uri TestDataPackageUri => new Uri($@"{TESTDATA_URL}{TESTDATA_FILE}");

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
            if (IsRecordMode)
            {
                TestContext.EnsureTestResultsDir();
                PrintEnvironment();
            }

            if (IsCleanPythonMode)
                AssertEx.IsTrue(PythonInstaller.DeleteToolsPythonDirectory());

            string currentDirectory = Directory.GetCurrentDirectory();
            Directory.CreateDirectory(TESTDATA_DIR);
            TestFilesZipPaths = new[]
            {
                GetPerfTestDataURL($@"{TESTDATA_FILE}")
            };

            FreshenTestDataDownloads();

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

        private void DownloadTestDataPackage(IProgressMonitor progressMonitor)
        {
            using var webClient = TestDownloadClient ?? new MultiFileAsynchronousDownloadClient(progressMonitor, 1);

            Console.WriteLine("");
            Console.WriteLine(@"TestCarafeBuildLibrary: Attempting TestData package download ...");
            TryHelper.TryTwice(() =>
            {
                if (!webClient.DownloadFileAsync(TestDataPackageUri, TestFilesZip, out var downloadException))
                    throw new ToolExecutionException(@"TestData package download failed...", downloadException);
            });
            Console.WriteLine(@"TestCarafeBuildLibrary: TestData package download success!");
        }


        string LibraryTunedByDiann => @"CarafeLibraryTunedByDiann";
        string LibraryTunedBySky => @"CarafeLibraryTunedBySkyline";
        string LibraryTunedBySkyIrt => @"CarafeLibraryTunedBySkylineIrt";
        string LibraryTunedByThis => @"CarafeLibraryTunedByThis";
        private string LibraryPathTunedByDiann => TestContext.GetTestPath(@"TestCarafeBuildLibrary\LibraryTunedByDiann.blib");
        private string LibraryPathTunedBySky => TestContext.GetTestPath(@"TestCarafeBuildLibrary\LibraryTunedBySkyline.blib");
        private string LibraryPathTunedBySkyIrt => TestContext.GetTestPath(@"TestCarafeBuildLibrary\LibraryTunedBySkylineIrt.blib");

        private string LibraryPathTunedByThis => TestContext.GetTestPath(@"TestCarafeBuildLibrary\LibraryTunedByThis.blib");
        private string openSkyDoc = "";

        private string LibraryPathWithoutIrt =>
            TestFilesDir.GetTestPath("LibraryWithoutIrt.blib");

        private string LibraryPathWithIrt =>
            TestFilesDir.GetTestPath("LibraryWithIrt.blib");

        string DiannFineTuneFile => TestFilesDir.GetTestPath(@"report.tsv");
        string MzMLFile => TestFilesDir.GetTestPath(@"Lumos_8mz_staggered_reCID_human_small\Crucios_20240320_CH_15_HeLa_CID_27NCE_01.mzML");
        private string SkyFineTuneFile => SkyTestFile;
        private string SkyTestFile => TestFilesDir.GetTestPath(@"Lumos_8mz_staggered_reCID_human_small\Lumos_8mz_staggered_reCID_human.sky");
        string ProteinDatabase => TestFilesDir.GetTestPath(@"UP000005640_9606_small.fasta");

        protected override void DoTest()
        {
            TestEmptyNameMessage();
            TestEmptyPathMessage();

            if (RunExtendedTest)
                LongTest();
            else
                ShortTest();

            if (IsCleanPythonMode)
                AssertEx.IsTrue(PythonInstaller.DeleteToolsPythonDirectory());
        }
        private void TestEmptyNameMessage()
        {
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);

            RunUI(() =>
            {
                buildLibraryDlg.Carafe = true;
            });

            RunDlg<AlertDlg>(buildLibraryDlg.OkWizardPage, dlg =>
            {
                Assert.AreEqual(String.Format(Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty, "Name"),
                    dlg.Message);
                dlg.OkDialog();
            });

            OkDialog(buildLibraryDlg, buildLibraryDlg.CancelDialog);
            OkDialog(peptideSettings, peptideSettings.OkDialog);
        }

        private void TestEmptyPathMessage()
        {
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);

            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = "No peptides prediction";
                buildLibraryDlg.Carafe = true;
            });

            RunDlg<MessageDlg>(buildLibraryDlg.OkWizardPage, dlg =>
            {
                Assert.AreEqual(SettingsUIResources.BuildLibraryDlg_ValidateBuilder_You_must_specify_an_output_file_path,
                    dlg.Message);
                dlg.OkDialog();
            });

            OkDialog(buildLibraryDlg, buildLibraryDlg.CancelDialog);
            OkDialog(peptideSettings, peptideSettings.OkDialog);
        }
        private void ShortTest()
        {
            DirectoryEx.SafeDelete(TestContext.GetTestPath(@"TestCarafeBuildLibrary\"));
            Directory.CreateDirectory(TestContext.GetTestPath(@"TestCarafeBuildLibrary\"));

            OpenDocument(TestFilesDir.GetTestPath(SkyTestFile));

            const string answerWithoutIrt = "without_iRT/predict_transformed.speclib.tsv";
            const string libraryWithoutIrt = "CarafeLibraryWithoutIrt";

            const string libraryWithIrt = "CarafeLibraryWithIrt";
            const string answerWithIrt = "with_iRT/predict_transformed.speclib.tsv";

            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);

            var simulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT; // Simulates not having Nvidia library but having the GPU

            var builtLibraryBySky = CarafeBuildLibrary(peptideSettings, LibraryTunedBySky, LibraryPathTunedBySky, MzMLFile, "", SkyFineTuneFile, BuildLibraryDlg.BuildLibraryTargetOptions.currentSkylineDocument, BuildLibraryDlg.LearningOptions.another_doc, TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_bySky.blib"), simulatedInstallationState);

            // var builtLibraryBySkyIrt = CarafeBuildLibrary(peptideSettings, LibraryTunedBySkyIrt, LibraryPathTunedBySkyIrt, MzMLFile, "", SkyFineTuneFile, BuildLibraryDlg.BuildLibraryTargetOptions.currentSkylineDocument, BuildLibraryDlg.LearningOptions.another_doc, TestFilesDir.GetTestPath(@"test_res_fine_tuned_bySky_iRT.blib"), simulatedInstallationState, IrtStandard.BIOGNOSYS_11);

            //var builtLibraryByDiann = CarafeBuildLibrary(peptideSettings, LibraryTunedByDiann, LibraryPathTunedByDiann, MzMLFile, "", DiannFineTuneFile, BuildLibraryDlg.BuildLibraryTargetOptions.currentSkylineDocument, BuildLibraryDlg.LearningOptions.diann_report, TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_byDiann.blib"), simulatedInstallationState);

            var fileHash = PythonInstallerUtil.GetMD5FileHash(PythonInstaller.PythonEmbeddablePackageDownloadPath);

            if (IsRecordMode)
                Console.WriteLine($@"Computed PythonEmbeddableHash: {fileHash}");
            Assert.AreEqual(Settings.Default.PythonEmbeddableHash, fileHash);

            var addRtStdDlg = WaitForOpenForm<AddIrtStandardsToDocumentDlg>();
            OkDialog(addRtStdDlg, addRtStdDlg.CancelDialog);

            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);

            RunUI(() =>
            {
                spectralLibraryViewer.ChangeSelectedLibrary(LibraryPathTunedBySkyIrt);
                spectralLibraryViewer.Close();
            });

            var saveChangesDlg =
                ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.NewDocument(), WAIT_TIME);
            AssertEx.AreComparableStrings(SkylineResources.SkylineWindow_CheckSaveDocument_Do_you_want_to_save_changes,
                saveChangesDlg.Message);

            RunUI(() =>
            {
                saveChangesDlg.ClickNo();
                FileStreamManager.Default.CloseAllStreams();
            });

            WaitForCondition(() => !FileStreamManager.Default.HasPooledStreams);
            TestFilesDir.CheckForFileLocks(TestFilesDir.FullPath);

            // var expected = LibrarySpec.CreateFromPath("answer", TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_bySky_iRT.blib"));
            // var result = LibrarySpec.CreateFromPath("testBuilt", builtLibraryBySkyIrt);
            // AssertEx.LibraryEquivalent(expected, result, MZ_TOLERANCE, INTENSITY_TOLERANCE, MINIMUM_INTENSITY, TOP_N);


            //var expected = LibrarySpec.CreateFromPath("answer", TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_byDiann.blib"));
            //var result = LibrarySpec.CreateFromPath("testBuilt", builtLibraryByDiann);
            //AssertEx.LibraryEquivalent(expected, result, MZ_TOLERANCE, INTENSITY_TOLERANCE, MINIMUM_INTENSITY, TOP_N);

            var expected = LibrarySpec.CreateFromPath("answer", TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_bySky.blib"));
            var result = LibrarySpec.CreateFromPath("testBuilt", builtLibraryBySky);
            AssertEx.LibraryEquivalent(expected, result, MZ_TOLERANCE, INTENSITY_TOLERANCE);
            TestFilesDir.CheckForFileLocks(TestFilesDir.FullPath);
        }
        private void LongTest() 
        {
            DirectoryEx.SafeDelete(TestContext.GetTestPath(@"TestCarafeBuildLibrary\"));
            Directory.CreateDirectory(TestContext.GetTestPath(@"TestCarafeBuildLibrary\"));

            OpenDocument(TestFilesDir.GetTestPath(SkyTestFile));

            const string answerWithoutIrt = "without_iRT/predict_transformed.speclib.tsv";
            const string libraryWithoutIrt = "CarafeLibraryWithoutIrt";

            const string libraryWithIrt = "CarafeLibraryWithIrt";
            const string answerWithIrt = "with_iRT/predict_transformed.speclib.tsv";

            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            
            var simulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT; // Simulates not having Nvidia library but having the GPU
            var builtLibraryBySky = CarafeBuildLibrary(peptideSettings, LibraryTunedBySky, LibraryPathTunedBySky, MzMLFile, "", SkyFineTuneFile, BuildLibraryDlg.BuildLibraryTargetOptions.currentSkylineDocument, BuildLibraryDlg.LearningOptions.another_doc, TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_bySky.blib"), simulatedInstallationState);
        
            peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            
            simulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD; // Simulates not having Nvidia GPU
            var builtLibraryByDiann = CarafeBuildLibrary(peptideSettings, LibraryTunedByDiann, LibraryPathTunedByDiann, MzMLFile, "", DiannFineTuneFile, BuildLibraryDlg.BuildLibraryTargetOptions.currentSkylineDocument,BuildLibraryDlg.LearningOptions.diann_report, TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_byDiann.blib"), simulatedInstallationState);

            peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);

            var builtLibraryBySkyIrt = CarafeBuildLibrary(peptideSettings, LibraryTunedBySkyIrt, LibraryPathTunedBySkyIrt, MzMLFile, "", SkyFineTuneFile, BuildLibraryDlg.BuildLibraryTargetOptions.currentSkylineDocument, BuildLibraryDlg.LearningOptions.another_doc, TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_bySky_iRT.blib"), simulatedInstallationState, IrtStandard.BIOGNOSYS_11);
            
            var fileHash = PythonInstallerUtil.GetMD5FileHash(PythonInstaller.PythonEmbeddablePackageDownloadPath);

            if (IsRecordMode)
                Console.WriteLine($@"Computed PythonEmbeddableHash: {fileHash}");
            Assert.AreEqual(Settings.Default.PythonEmbeddableHash, fileHash);

            var addRtStdDlg = WaitForOpenForm<AddIrtStandardsToDocumentDlg>();
            OkDialog(addRtStdDlg, addRtStdDlg.CancelDialog);

            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                spectralLibraryViewer.ChangeSelectedLibrary(LibraryPathTunedBySky);
                spectralLibraryViewer.ChangeSelectedLibrary(LibraryPathTunedByDiann);
                spectralLibraryViewer.ChangeSelectedLibrary(LibraryPathTunedBySkyIrt);
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
            
            var expected = LibrarySpec.CreateFromPath("answer", TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_bySky_iRT.blib")); 
            var result = LibrarySpec.CreateFromPath("testBuilt", builtLibraryBySkyIrt); 
            AssertEx.LibraryEquivalent(expected, result, MZ_TOLERANCE, INTENSITY_TOLERANCE); 
            
            expected = LibrarySpec.CreateFromPath("answer", TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_byDiann.blib"));
            result = LibrarySpec.CreateFromPath("testBuilt", builtLibraryByDiann);
            AssertEx.LibraryEquivalent(expected, result, MZ_TOLERANCE, INTENSITY_TOLERANCE);

            expected = LibrarySpec.CreateFromPath("answer", TestFilesDir.GetTestPath(@"cpu_test_res_fine_tuned_bySky.blib"));
            result = LibrarySpec.CreateFromPath("testBuilt", builtLibraryBySky);
            AssertEx.LibraryEquivalent(expected, result, MZ_TOLERANCE, INTENSITY_TOLERANCE);
            TestFilesDir.CheckForFileLocks(TestFilesDir.FullPath);
        }

        protected void DoTestBACK()
        {
            openSkyDoc = TestFilesDir.GetTestPath(SkyTestFile); //(@"Test-imported\Test-imported.sky");
            OpenDocument(openSkyDoc);

            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);

            const string libraryTunedByDiann = @"CarafeLibraryTunedByDiann";
            const string libraryTunedBySky = @"CarafeLibraryTunedBySkyline";
            const string libraryTunedByThis = @"CarafeLibraryTunedByThis";
            string diannFineTuneFile = DiannFineTuneFile;
            string mzMLFile = MzMLFile;
            string skyFineTuneFile = SkyTestFile;
            string proteinDatabase = ProteinDatabase;


            CarafeBuildLibrary(peptideSettings, libraryTunedBySky, LibraryTunedBySky, mzMLFile, "", skyFineTuneFile, BuildLibraryDlg.BuildLibraryTargetOptions.currentSkylineDocument, BuildLibraryDlg.LearningOptions.another_doc, @"test_res_fine_tuned_bySky.csv");
            OkDialog(peptideSettings, peptideSettings.OkDialog);

            peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);

            CarafeBuildLibrary(peptideSettings, libraryTunedByDiann, LibraryTunedByDiann, mzMLFile, "", diannFineTuneFile, BuildLibraryDlg.BuildLibraryTargetOptions.currentSkylineDocument, BuildLibraryDlg.LearningOptions.diann_report, @"test_res_fine_tuned_byDiann.csv");
            OkDialog(peptideSettings, peptideSettings.OkDialog);

            peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);

            CarafeBuildLibrary(peptideSettings, libraryTunedByThis, LibraryTunedByThis, mzMLFile, proteinDatabase, "", BuildLibraryDlg.BuildLibraryTargetOptions.currentSkylineDocument, BuildLibraryDlg.LearningOptions.this_doc, @"test_res_fine_tuned_bySky.csv");
            OkDialog(peptideSettings, peptideSettings.OkDialog);

            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                spectralLibraryViewer.ChangeSelectedLibrary(libraryTunedBySky);
                spectralLibraryViewer.ChangeSelectedLibrary(libraryTunedByDiann);
                spectralLibraryViewer.ChangeSelectedLibrary(libraryTunedByThis);
            });
            OkDialog(spectralLibraryViewer, spectralLibraryViewer.Close);

            // test with iRT
            peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            CarafeBuildLibrary(peptideSettings, libraryTunedByThis, LibraryTunedByThis, mzMLFile, proteinDatabase, "", BuildLibraryDlg.BuildLibraryTargetOptions.currentSkylineDocument, BuildLibraryDlg.LearningOptions.this_doc, "test_res_fine_tuned_bySky_iRT.csv", PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD, IrtStandard.BIOGNOSYS_11);
        }

        /// <summary>
        /// Test goes through building of a Library by Carafe with or without iRT.  Returns path to library built/
        /// </summary>
        /// <param name="peptideSettings">Open PeptideSettingsUI Dialog object</param>
        /// <param name="libraryName">Name of the library to build</param>
        /// <param name="libraryPath">Path of the library to build</param>
        /// <param name="buildTarget">Build library target peptides, current document peptides (or FASTA database if current doc is blank)</param>
        /// <param name="learnFrom">Source of fine tuning document</param>
        /// <param name="answerFile">Answer sheet in the test</param>
        /// <param name="simulatedInstallationState">Python Simulated State helps determine whether user is offered Nvidia install</param>
        /// <param name="iRTtype">iRT standard type</param>
        /// <param name="mzMLFile">MS/MS Data file path</param>
        /// <param name="proteinDatabase">Protein FASTA database</param>
        /// <param name="fineTuneFile">fine tuning file path</param>
        private string CarafeBuildLibrary(PeptideSettingsUI peptideSettings, string libraryName,
            string libraryPath, string mzMLFile, string proteinDatabase, string fineTuneFile,
            BuildLibraryDlg.BuildLibraryTargetOptions buildTarget, BuildLibraryDlg.LearningOptions learnFrom,
            string answerFile,
            PythonInstaller.eSimulatedInstallationState simulatedInstallationState =
                PythonInstaller.eSimulatedInstallationState.NONVIDIASOFT, IrtStandard iRTtype = null)
        {
            bool buildLibraryDlgFinished = false;
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg> (() =>
            {
                peptideSettings.ShowBuildLibraryDlg();
                buildLibraryDlgFinished = true;
            });
            
            //Test unfilled Library Name
            RunUI(() =>
            {
                buildLibraryDlg.ComboBuildLibraryTarget = buildTarget;
                buildLibraryDlg.ComboLearnFrom = learnFrom;
                buildLibraryDlg.Carafe = true;
                if (iRTtype != null)
                    buildLibraryDlg.IrtStandard = iRTtype;

                buildLibraryDlg.OkWizardPage();

                buildLibraryDlg.TextBoxMsMsDataFile = mzMLFile;
                buildLibraryDlg.TextBoxProteinDatabase = proteinDatabase;
                buildLibraryDlg.TextBoxTrainingDoc = fineTuneFile;

                buildLibraryDlg.UnloadTrainingDocument();
                if (proteinDatabase == "" && learnFrom == BuildLibraryDlg.LearningOptions.another_doc)
                    buildLibraryDlg.LoadTrainingDocument(fineTuneFile);
            });

            var nameEmptyAlert = WaitForOpenForm<MessageDlg>();
            Assert.AreEqual(String.Format(Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty, "Name"), nameEmptyAlert.Text);
            OkDialog(nameEmptyAlert, nameEmptyAlert.OkDialog);

            //Test unfilled directory
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryName;
                buildLibraryDlg.ComboBuildLibraryTarget = buildTarget;
                buildLibraryDlg.ComboLearnFrom = learnFrom;
                buildLibraryDlg.Carafe = true;
                if (iRTtype != null)
                    buildLibraryDlg.IrtStandard = iRTtype;

                buildLibraryDlg.OkWizardPage();

                buildLibraryDlg.TextBoxMsMsDataFile = mzMLFile;
                buildLibraryDlg.TextBoxProteinDatabase = proteinDatabase;
                buildLibraryDlg.TextBoxTrainingDoc = fineTuneFile;

                buildLibraryDlg.UnloadTrainingDocument();
                if (proteinDatabase == "" && learnFrom == BuildLibraryDlg.LearningOptions.another_doc)
                    buildLibraryDlg.LoadTrainingDocument(fineTuneFile);
            });

            var dirEmptyAlert = WaitForOpenForm<MessageDlg>();
            Assert.AreEqual(SettingsUIResources.BuildLibraryDlg_ValidateBuilder_You_must_specify_an_output_file_path, dirEmptyAlert.Text);
            OkDialog(dirEmptyAlert, dirEmptyAlert.OkDialog);

            // PauseTest();
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryName;
                buildLibraryDlg.LibraryPath = libraryPath;
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
            WaitForClosedForm<BuildLibraryDlg>();

            //Wait up to 30 minutes because this is slow on CPU
            WaitForCondition(30 * 60 * 1000, () => buildLibraryDlgFinished);

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
        private void TestResultingLibByValues(BuildLibraryDlg buildLibraryDlg, string answer)
        {
            string product = buildLibraryDlg.TestLibFilepath;
            using (var answerReader = new StreamReader(answer))
            {
                using (var productReader = new StreamReader(product))
                {
                    AssertEx.FieldsEqual(productReader, answerReader, 24, null, true, 0, 1e-1);
                }
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



