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
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
   
    [TestClass]
    public class CarafeBuildLibraryTest : AbstractFunctionalTestEx
    {
       
        public IAsynchronousDownloadClient TestDownloadClient { get; set; }
        private const string TESTDATA_URL = @"https://skyline.ms/_webdav/home/support/file%20sharing/%40files/";
        private const string TESTDATA_FILE = @"CarafeBuildLibraryTest.zip";
        private const string TESTDATA_DIR = @"TestFunctional";


        public Uri TestDataPackageUri => new Uri($@"{TESTDATA_URL}{TESTDATA_FILE}");
        //[TestMethod]
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

        private string LibraryPathWithoutIrt => TestContext.GetTestPath("TestCarafeBuildLibrary\\LibraryWithoutIrt.blib");
        protected override void DoTest()
        {
            PythonTestUtil pythonUtil = new PythonTestUtil(BuildLibraryDlg.CARAFE_PYTHON_VERSION, @"Carafe", true);
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            const string libraryWithoutIrt = @"CarafeLibraryWithoutIrt";
            string fineTuningFile = TestFilesDir.GetTestPath(@"report.tsv");
            string mzMLFile = TestFilesDir.GetTestPath(@"LFQ_Orbitrap_AIF_Human_01.mzML");

            OpenDocument(TestFilesDir.GetTestPath(@"Test-imported.sky/Test-imported.sky"));
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryWithoutIrt;
                buildLibraryDlg.LibraryPath = LibraryPathWithoutIrt;
                buildLibraryDlg.Carafe = true;
                buildLibraryDlg.OkWizardPage();
                buildLibraryDlg.TextBoxMsMsDataFile = mzMLFile;
                buildLibraryDlg.TextBoxTrainingDataFile = fineTuningFile;
            });
            pythonUtil.InstallPython(buildLibraryDlg);
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);
            OkDialog(peptideSettings, peptideSettings.OkDialog);

            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                spectralLibraryViewer.ChangeSelectedLibrary(libraryWithoutIrt);
            });
            OkDialog(spectralLibraryViewer, spectralLibraryViewer.Close);
        }
        protected override void Cleanup()
        {
            DirectoryEx.SafeDelete("TestCarafeBuildLibrary");
        }
    }
}

