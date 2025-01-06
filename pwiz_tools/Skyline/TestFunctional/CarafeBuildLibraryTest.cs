using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
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

        private string LibraryPathWithoutIrt => TestContext.GetTestPath("TestCarafeBuildLibrary\\LibraryWithoutIrt.blib");

        private void InstallPython(BuildLibraryDlg buildLibraryDlg)
        {
            bool havePythonPrerequisite = false;

            RunUI(() => { havePythonPrerequisite = buildLibraryDlg.PythonRequirementMet(); });

            if (!havePythonPrerequisite)
            {
                MessageDlg confirmDlg = null;

                RunLongDlg<MultiButtonMsgDlg>(buildLibraryDlg.OkWizardPage, pythonDlg =>
                {
                    Assert.AreEqual(pythonDlg.Message,
                        string.Format(
                            ToolsUIResources.PythonInstaller_BuildPrecursorTable_Python_0_installation_is_required,
                            BuildLibraryDlg.CARAFE_PYTHON_VERSION, @"Carafe"));

                    OkDialog(pythonDlg, pythonDlg.OkDialog);
                    if (!PythonInstallerTaskValidator.ValidateEnableLongpaths())
                    {
                        MultiButtonMsgDlg longPathDlg = null;

                        longPathDlg = WaitForOpenForm<MultiButtonMsgDlg>();
                        Assert.AreEqual(longPathDlg.Message,
                            string.Format(ToolsUIResources.PythonInstaller_Enable_Windows_Long_Paths));
                        RunDlg<MessageDlg>(longPathDlg.ClickNo, okDlg =>
                        {
                            confirmDlg = okDlg;
                            Assert.AreEqual(okDlg.Message,
                                ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment);
                            okDlg.OkDialog();
                        });
                        longPathDlg.Close();
                    }
                    else
                    {
                        confirmDlg = WaitForOpenForm<MessageDlg>();
                        Assert.AreEqual(confirmDlg.Message,
                            ToolsUIResources
                                .PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment);
                        OkDialog(confirmDlg, confirmDlg.OkDialog);
                    }

                }, dlg => {
                    dlg.Close();
                });

            }
        }
        protected override void DoTest()
        {

            OpenDocument(TestFilesDir.GetTestPath(@"Test-imported.sky/Test-imported.sky"));
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            const string libraryWithoutIrt = @"CarafeLibraryWithoutIrt";
            string fineTuningFile = TestFilesDir.GetTestPath(@"report.tsv");
            string mzMLFile = TestFilesDir.GetTestPath(@"LFQ_Orbitrap_AIF_Human_01.mzML");
   
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryWithoutIrt;
                buildLibraryDlg.LibraryPath = LibraryPathWithoutIrt;
                buildLibraryDlg.Carafe = true;
                buildLibraryDlg.OkWizardPage();
                buildLibraryDlg.TextBoxMsMsDataFile = mzMLFile;
                buildLibraryDlg.TextBoxTrainingDataFile = fineTuningFile;
            });

            InstallPython(buildLibraryDlg);
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

