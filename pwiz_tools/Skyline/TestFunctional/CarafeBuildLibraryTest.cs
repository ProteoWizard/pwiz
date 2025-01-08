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
            PythonTestUtil pythonUtil = new PythonTestUtil(BuildLibraryDlg.CARAFE_PYTHON_VERSION, @"Carafe");
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

