using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AlphapeptDeepBuildLibraryTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestAlphapeptDeepBuildLibrary()
        {
            TestFilesZip = "TestFunctional/AlphapeptDeepBuildLibraryTest.zip";
            RunFunctionalTest();
        }

        private string LibraryPathWithoutIrt =>
            TestContext.GetTestPath("TestAlphapeptDeepBuildLibrary\\LibraryWithoutIrt.blib");

        
      
        protected override void DoTest()
        {
            PythonTestUtil pythonUtil = new PythonTestUtil(BuildLibraryDlg.ALPHAPEPTDEEP_PYTHON_VERSION, @"AlphapeptDeep");
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            const string libraryWithoutIrt = "AlphapeptDeepLibraryWithoutIrt";

            OpenDocument(TestFilesDir.GetTestPath(@"Rat_plasma.sky"));
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryWithoutIrt;
                buildLibraryDlg.LibraryPath = LibraryPathWithoutIrt;
                buildLibraryDlg.AlphapeptDeep = true;
            });

            if (!pythonUtil.InstallPython(buildLibraryDlg)) 
                OkDialog(buildLibraryDlg,buildLibraryDlg.OkWizardPage);
            //PauseTest();
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
            DirectoryEx.SafeDelete("TestAlphapeptDeepBuildLibrary");
        }
    }
}
