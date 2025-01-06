using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AlphapeptDeepBuildLibraryTest : AbstractFunctionalTestEx
    {
        //[TestMethod]
        public void TestAlphapeptDeepBuildLibrary()
        {
            TestFilesZip = "TestFunctional/AlphapeptDeepBuildLibraryTest.zip";
            RunFunctionalTest();
        }

        private string LibraryPathWithoutIrt =>
            TestContext.GetTestPath("TestAlphapeptDeepBuildLibrary\\LibraryWithoutIrt.blib");

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
                            BuildLibraryDlg.ALPHAPEPTDEEP_PYTHON_VERSION, @"AlphapeptDeep"));
 
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
            OpenDocument(TestFilesDir.GetTestPath(@"Rat_plasma.sky"));
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            const string libraryWithoutIrt = "AlphapeptDeepLibraryWithoutIrt";
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryWithoutIrt;
                buildLibraryDlg.LibraryPath = LibraryPathWithoutIrt;
                buildLibraryDlg.AlphapeptDeep = true;
            });
            InstallPython(buildLibraryDlg);
            
            OkDialog(buildLibraryDlg,buildLibraryDlg.OkWizardPage);
            OkDialog(peptideSettings, peptideSettings.OkDialog);



            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                spectralLibraryViewer.ChangeSelectedLibrary(libraryWithoutIrt);
            });
            
            OkDialog(spectralLibraryViewer, spectralLibraryViewer.Close);
           // SkylineWindow.Close();
        }
        protected override void Cleanup()
        {
            DirectoryEx.SafeDelete("TestAlphapeptDeepBuildLibrary");
        }
    }
}
