using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.GUI;
using SharedBatch;
using SkylineBatch;

namespace SkylineBatchTest
{
    [TestClass]
    public class OpenFileFunctionalTest : AbstractSkylineBatchFunctionalTest
    {

        public static string CONFIG_FOLDER;
        public static string TEST_FOLDER;


        [TestMethod]
        public void OpenFileTest()
        {
            TestFilesZipPaths = new[]
                {@"SkylineBatchTest\OpenFileFunctionalTest.zip", @"SkylineBatchTest\TestConfigurationFiles.zip"};

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TEST_FOLDER = TestFilesDirs[0].FullPath;
            CONFIG_FOLDER = TestFilesDirs[1].FullPath;
            var mainWindow = MainFormWindow();
            var mainForm = mainWindow as MainForm;
            WaitForShownForm(mainForm);
            Assert.IsNotNull(mainForm, "Main program window is not an instance of MainForm.");
            Assert.AreEqual(0, mainForm.ConfigCount());

            RInstallations.AddRDirectory(Path.Combine(TestFilesDirs[1].FullPath, "R"));

            TestFileExistingPaths(mainForm);

            TestFileAutomaticReplace(mainForm);

        }

        public void TestFileExistingPaths(MainForm mainForm)
        {
            RunDlg<CommonAlertDlg>(() =>  mainForm.FileOpened(Path.Combine(TEST_FOLDER, "ValidConfigurations.bcfg")),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.MainForm_FileOpenedImport_Do_you_want_to_import_configurations_from__0__, "ValidConfigurations.bcfg"),
                        dlg.Message);
                    dlg.ClickNo();
                });
            
            RunUI(() => { FunctionalTestUtil.CheckConfigs(0, 0, mainForm); });

            RunDlg<CommonAlertDlg>(() => mainForm.FileOpened(Path.Combine(TEST_FOLDER, "ValidConfigurations.bcfg")),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.MainForm_FileOpenedImport_Do_you_want_to_import_configurations_from__0__, "ValidConfigurations.bcfg"),
                        dlg.Message);
                    dlg.ClickYes();
                });
            RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 0, mainForm); });
        }

        public void TestFileAutomaticReplace(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            RunDlg<CommonAlertDlg>(() => mainForm.FileOpened(Path.Combine(TEST_FOLDER, "AutomaticPathReplaceOldVersion.bcfg")),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.MainForm_FileOpenedImport_Do_you_want_to_import_configurations_from__0__, "AutomaticPathReplaceOldVersion.bcfg"),
                        dlg.Message);
                    dlg.ClickYes();
                });

            RunUI(() => { FunctionalTestUtil.CheckConfigs(1, 0, mainForm); });

            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            RunDlg<CommonAlertDlg>(() => mainForm.FileOpened(Path.Combine(TEST_FOLDER, "AutomaticPathReplaceNewVersion.bcfg")),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.MainForm_FileOpenedImport_Do_you_want_to_import_configurations_from__0__, "AutomaticPathReplaceNewVersion.bcfg"),
                        dlg.Message);
                    dlg.ClickYes();
                });
            
            RunUI(() => { FunctionalTestUtil.CheckConfigs(1, 0, mainForm); });

        }

        /*public async Task TestDownloadedConfiguration(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var openedFileForm = ShowDialog<FileOpenedForm>(() => mainForm.FileOpened(Path.Combine(TEST_FOLDER, "Downloads\\DownloadedConfig.bcfg")));
            RunUI(() =>
            {
                openedFileForm.SetPath(TEST_FOLDER);
                openedFileForm.btnCancel.PerformClick();
            });
            RunUI(() => { FunctionalTestUtil.CheckConfigs(0, 0, mainForm); });
            Assert.AreEqual(false, File.Exists(Path.Combine(TEST_FOLDER, "DownloadedConfig.bcfg")));

            openedFileForm = ShowDialog<FileOpenedForm>(() => mainForm.FileOpened(Path.Combine(TEST_FOLDER, "Downloads\\Folder\\DownloadedConfig.bcfg")));
            RunUI(() =>
            {
                openedFileForm.SetPath(TEST_FOLDER);
                openedFileForm.btnOK.PerformClick();
            });
            await Task.Delay(5000);
            RunUI(() => { FunctionalTestUtil.CheckConfigs(1, 0, mainForm); });
            Assert.AreEqual(true, File.Exists(Path.Combine(TEST_FOLDER, "DownloadedConfig.bcfg")));
        }*/
        
    }
}
