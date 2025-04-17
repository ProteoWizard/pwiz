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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
   
    public class CarafeBuildLibraryTest : AbstractFunctionalTestEx
    {
       
        public IAsynchronousDownloadClient TestDownloadClient { get; set; }
        private const string TESTDATA_URL = @"https://skyline.ms/_webdav/home/support/file%20sharing/%40files/";
        private const string TESTDATA_FILE = @"CarafeBuildLibraryTest.zip";
        private const string TESTDATA_DIR = @"TestPerf";


        public Uri TestDataPackageUri => new Uri($@"{TESTDATA_URL}{TESTDATA_FILE}");
        
        [TestMethod]
        public void TestCarafeBuildLibrary()
        {
            Directory.CreateDirectory(TESTDATA_DIR);
            string currentDirectory = Directory.GetCurrentDirectory();
            TestFilesZip = $@"{currentDirectory}\\{TESTDATA_DIR}\\{TESTDATA_FILE}";
            IProgressMonitor progressMonitor = new SilentProgressMonitor();
            DownloadTestDataPackage(progressMonitor);
            RunFunctionalTest();
        }

        private void DownloadTestDataPackage(IProgressMonitor progressMonitor)
        {
            using var webClient = TestDownloadClient ?? new MultiFileAsynchronousDownloadClient(progressMonitor, 1);
            if (!webClient.DownloadFileAsync(TestDataPackageUri, TestFilesZip, out var downloadException))
                throw new ToolExecutionException(@"TestData package download failed...", downloadException);
        }

        private PythonTestUtil _pythonTestUtil;
        private PeptideSettingsUI _peptideSettings;
        private BuildLibraryDlg _buildLibraryDlg;

        private string LibraryTunedByDiann => TestContext.GetTestPath(@"TestCarafeBuildLibrary\LibraryTunedByDiann.blib");
        private string LibraryTunedBySky => TestContext.GetTestPath(@"TestCarafeBuildLibrary\LibraryTunedBySkyline.blib");
        private string LibraryTunedByThis => TestContext.GetTestPath(@"TestCarafeBuildLibrary\LibraryTunedByThis.blib");
        private string openSkyDoc = "";
        protected override void DoTest()
        {
            openSkyDoc = TestFilesDir.GetTestPath(@"Lumos_8mz_staggered_reCID_human\Lumos_8mz_staggered_reCID_human.sky"); //(@"Test-imported\Test-imported.sky");
            OpenDocument(openSkyDoc);

            _peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            _buildLibraryDlg = ShowDialog<BuildLibraryDlg>(_peptideSettings.ShowBuildLibraryDlg);
            _pythonTestUtil = new PythonTestUtil(BuildLibraryDlg.CARAFE_PYTHON_VERSION, @"Carafe", false);

            const string libraryTunedByDiann = @"CarafeLibraryTunedByDiann";
            const string libraryTunedBySky = @"CarafeLibraryTunedBySkyline";
            const string libraryTunedByThis = @"CarafeLibraryTunedByThis";
            string diannFineTuneFile = TestFilesDir.GetTestPath(@"report.tsv");
            string mzMLFile = TestFilesDir.GetTestPath(@"Crucios_20240320_CH_15_HeLa_CID_27NCE_01.mzML");
            string skyFineTuneFile = TestFilesDir.GetTestPath(@"Lumos_8mz_staggered_reCID_human\Lumos_8mz_staggered_reCID_human.sky");
            string proteinDatabase = TestFilesDir.GetTestPath(@"UP000005640_9606.fasta");

           // _pythonTestUtil.InstallPython(_buildLibraryDlg);
           // OkDialog(_buildLibraryDlg, _buildLibraryDlg.OkWizardPage);
           // OkDialog(_peptideSettings, _peptideSettings.OkDialog);

           // var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);


            CarafeBuildLibrary(libraryTunedBySky, LibraryTunedBySky, mzMLFile, "", skyFineTuneFile, BuildLibraryDlg.LearningOptions.another_doc, @"test_res_fine_tuned_bySky.csv");
            OkDialog(_peptideSettings, _peptideSettings.OkDialog);

            _peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            _buildLibraryDlg = ShowDialog<BuildLibraryDlg>(_peptideSettings.ShowBuildLibraryDlg);

            CarafeBuildLibrary(libraryTunedByDiann, LibraryTunedByDiann, mzMLFile, "", diannFineTuneFile, BuildLibraryDlg.LearningOptions.diann_report,@"test_res_fine_tuned_byDiann.csv");
            OkDialog(_peptideSettings, _peptideSettings.OkDialog);

            _peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            _buildLibraryDlg = ShowDialog<BuildLibraryDlg>(_peptideSettings.ShowBuildLibraryDlg);

            CarafeBuildLibrary(libraryTunedByThis, LibraryTunedByThis, mzMLFile, proteinDatabase, "", BuildLibraryDlg.LearningOptions.this_doc, @"test_res_fine_tuned_bySky.csv");
            OkDialog(_peptideSettings, _peptideSettings.OkDialog);

            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                spectralLibraryViewer.ChangeSelectedLibrary(libraryTunedBySky);
                spectralLibraryViewer.ChangeSelectedLibrary(libraryTunedByDiann);
                spectralLibraryViewer.ChangeSelectedLibrary(libraryTunedByThis);
            });
            OkDialog(spectralLibraryViewer, spectralLibraryViewer.Close);

            // test with iRT
            _peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            _buildLibraryDlg = ShowDialog<BuildLibraryDlg>(_peptideSettings.ShowBuildLibraryDlg);

            //OkDialog(spectralLibraryViewer, spectralLibraryViewer.Close);
        }
        protected override void Cleanup()
        {
            DirectoryEx.SafeDelete("TestCarafeBuildLibrary");
        }


        /// <summary>
        /// Test goes through building of a Library by Carafe with or without iRT
        /// </summary>
        /// <param name="libraryName">Name of the library to build</param>
        /// <param name="libraryPath">Path of the library to build</param>
        /// <param name="learnFrom">Source of fine tuning document</param>
        /// <param name="answerFile">Answer sheet in the test</param>
        /// <param name="iRTtype">iRT standard type</param>
        /// <param name="mzMLFile">MS/MS Data file path</param>
        /// <param name="proteinDatabase">Protein FASTA database</param>
        /// <param name="fineTuneFile">fine tuning file path</param>
        private void CarafeBuildLibrary(string libraryName, string libraryPath, string mzMLFile, string proteinDatabase, string fineTuneFile, BuildLibraryDlg.LearningOptions learnFrom, string answerFile, IrtStandard iRTtype = null)
        {

            RunUI(() =>
            {
                _buildLibraryDlg.LibraryName = libraryName;
                _buildLibraryDlg.LibraryPath = libraryPath;
                _buildLibraryDlg.ComboLearnFrom = learnFrom;
                _buildLibraryDlg.Carafe = true;
                _buildLibraryDlg.TextBoxMsMsDataFile = mzMLFile;
                _buildLibraryDlg.TextBoxProteinDatabase = proteinDatabase;
                _buildLibraryDlg.TextBoxTrainingDoc = fineTuneFile;

                if (iRTtype != null) _buildLibraryDlg.IrtStandard = iRTtype;
            });
            _buildLibraryDlg.UnloadTrainingDocument();
            if (proteinDatabase == "" && learnFrom == BuildLibraryDlg.LearningOptions.another_doc)
                _buildLibraryDlg.LoadTrainingDocument(fineTuneFile);
            // Test the control path where Python needs installation and is

            if (!_pythonTestUtil.HavePythonPrerequisite(_buildLibraryDlg))
            {
                //PauseTest();

                // Test the control path where Python is installable
                if (!_pythonTestUtil.InstallPython(_buildLibraryDlg))
                {
                    OkDialog(_buildLibraryDlg, _buildLibraryDlg.OkWizardPage);
                    AbstractFunctionalTest.WaitForClosedForm<LongWaitDlg>();
                }

                //PauseTest();
                //TestResultingLibByHash(storedHash);
                TestResultingLibByValues(TestFilesDir.GetTestPath(answerFile));

            }
            else
            {
                RunUI(() => { _buildLibraryDlg.OkWizardPage(); });
                AbstractFunctionalTest.RunLongDlg<LongWaitDlg>(_buildLibraryDlg.OkWizardPage, WaitForClosedForm, dlg => {
                });

                //TestResultingLibByHash(storedHash);
                TestResultingLibByValues(TestFilesDir.GetTestPath(answerFile));
            }

        }
        private void TestResultingLibByValues(string answer)
        {
            string product = _buildLibraryDlg.TestLibFilepath;
            //string answer = TestFilesDir.GetTestPath(@"predict.speclib.tsv");
            using (var answerReader = new StreamReader(answer))
            {
                using (var productReader = new StreamReader(product))
                {
                    AssertEx.FieldsEqual(productReader, answerReader, 24, null, true, 0, 1e-3);
                }
            }
        }

    }
}

