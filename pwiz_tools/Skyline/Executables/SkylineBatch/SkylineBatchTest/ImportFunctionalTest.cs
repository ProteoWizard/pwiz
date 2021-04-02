using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;
using SkylineBatchTest;

namespace SkylineBatchTest
{
    [TestClass]
    public class ImportFunctionalTest : AbstractSkylineBatchFunctionalTest
    {
        [TestMethod]
        public void ImportTest()
        {
            TestFilesZip = @"SkylineBatchTest\ImportFunctionalTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var mainWindow = MainFormWindow();
            var mainForm = mainWindow as MainForm;
            Assert.IsNotNull(mainForm, "Main program window is not an instance of MainForm.");
            Assert.AreEqual(0, mainForm.ConfigCount());

            TestImportValidConfigurations(mainForm);

            TestImportDuplicateConfigurations(mainForm);

            TestImportInvalidConfigurations(mainForm);

            TestImportInvalidRConfigurations(mainForm);

            TestRootReplacement(mainForm);

        }

        public void ClearConfigs(MainForm mainForm)
        {
            while (mainForm.ConfigCount() > 0)
            {
                RunUI(() =>
                {
                    mainForm.ClickConfig(0);
                    mainForm.ClickDelete();
                });
            }
            Assert.AreEqual(0, mainForm.ConfigCount());
        }

        public void TestImportValidConfigurations(MainForm mainForm)
        {
            var validConfigFile = Path.Combine(TestFilesDirs[0].FullPath, "ValidConfigurations.bcfg");
            mainForm.DoImport(validConfigFile);
            Assert.AreEqual(3, mainForm.ConfigCount());
            Assert.AreEqual(0, mainForm.InvalidConfigCount());

            RunUI(() => { mainForm.ClickConfig(0); });
            var newConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            RunUI(() => { newConfigForm.CancelButton.PerformClick(); });
            WaitForClosedForm(newConfigForm);
        }

        public void TestImportDuplicateConfigurations(MainForm mainForm)
        {
            ClearConfigs(mainForm);
            var validConfigFile = Path.Combine(TestFilesDirs[0].FullPath, "ValidConfigurations.bcfg");
            mainForm.DoImport(validConfigFile);
            Assert.AreEqual(3, mainForm.ConfigCount());
            Assert.AreEqual(0, mainForm.InvalidConfigCount());
            RunDlg<AlertDlg>(() => mainForm.DoImport(validConfigFile),
                dlg =>
                {
                    Assert.AreEqual(SharedBatch.Properties.Resources.ConfigManager_Import_These_configurations_already_exist_and_could_not_be_imported_ + Environment.NewLine +
                                    "\"Selevsek-reps\"" + Environment.NewLine +
                                    "\"Selevsek-all\"" + Environment.NewLine +
                                    "\"Bruderer\"" + Environment.NewLine +
                                    SharedBatch.Properties.Resources.ConfigManager_Import_Please_remove_the_configurations_you_would_like_to_import_,
                        dlg.Message);
                    dlg.ClickOk();
                });

            Assert.AreEqual(3, mainForm.ConfigCount());
            Assert.AreEqual(0, mainForm.InvalidConfigCount());
            RunUI(() => { mainForm.ClickConfig(0); });
        }

        public void TestImportInvalidConfigurations(MainForm mainForm)
        {
            ClearConfigs(mainForm);
            var invalidPathsFile = Path.Combine(TestFilesDirs[0].FullPath, "InvalidPathConfigurations.bcfg");
            mainForm.DoImport(invalidPathsFile);
            Assert.AreEqual(3, mainForm.ConfigCount());
            Assert.AreEqual(3, mainForm.InvalidConfigCount());

            RunUI(() => { mainForm.ClickConfig(0); });
            var invalidConfigForm = ShowDialog<InvalidConfigSetupForm>(() => mainForm.ClickEdit());
            var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => invalidConfigForm.btnSkip.PerformClick());

            RunUI(() => { editConfigForm.CancelButton.PerformClick(); });
            WaitForClosedForm(invalidConfigForm);

            ClearConfigs(mainForm);
            var invalidConfigFile = Path.Combine(TestFilesDirs[0].FullPath, "InvalidSkylineConfigurations.bcfg");
            mainForm.DoImport(invalidConfigFile);
            Assert.AreEqual(3, mainForm.ConfigCount());
            Assert.AreEqual(3, mainForm.InvalidConfigCount());

            RunUI(() => { mainForm.ClickConfig(0); });
            var invalidConfigForm2 = ShowDialog<InvalidConfigSetupForm>(() => mainForm.ClickEdit());
            var editConfigForm2 = ShowDialog<SkylineBatchConfigForm>(() => invalidConfigForm2.btnSkip.PerformClick());

            RunUI(() => { editConfigForm2.CancelButton.PerformClick(); });
            WaitForClosedForm(invalidConfigForm);
        }

        public void TestImportInvalidRConfigurations(MainForm mainForm)
        {
            ClearConfigs(mainForm);
            var invalidConfigFile = Path.Combine(TestFilesDirs[0].FullPath, "InvalidRConfigurations.bcfg");

            RunDlg<AlertDlg>(() => mainForm.DoImport(invalidConfigFile),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.RDirectorySelector_AddIfNecassary_The_following_R_installations_were_not_found_by__0__, Program.AppName()) + Environment.NewLine +
                                    "400.0.3" + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.RDirectorySelector_AddIfNecassary_Would_you_like_to_add_an_R_installation_directory_,
                        dlg.Message);
                    dlg.ClickNo();
                });
            Assert.AreEqual(3, mainForm.ConfigCount());
            Assert.AreEqual(3, mainForm.InvalidConfigCount());

            RunUI(() => { mainForm.ClickConfig(0); });
            var invalidConfigForm = ShowDialog<InvalidConfigSetupForm>(() => mainForm.ClickEdit());
            var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => invalidConfigForm.btnSkip.PerformClick());

            RunUI(() => { editConfigForm.CancelButton.PerformClick(); });
            WaitForClosedForm(editConfigForm);
        }

        public void TestRootReplacement(MainForm mainForm)
        {
            ClearConfigs(mainForm);
            var invalidConfigFile = Path.Combine(TestFilesDirs[0].FullPath, "InvalidPathConfigurations.bcfg");
            mainForm.DoImport(invalidConfigFile);
            Assert.AreEqual(3, mainForm.ConfigCount());
            Assert.AreEqual(3, mainForm.InvalidConfigCount());
            RunUI(() => { mainForm.ClickConfig(0); });
            var invalidConfigForm = ShowDialog<InvalidConfigSetupForm>(() => mainForm.ClickEdit());
            RunUI(() =>
            {
                invalidConfigForm.CurrentControl.SetText(
                    "D:\\Users\\alimarsh\\NEU19_SkylineBatch\\Selevsek\\Selevsek.sky");
            });
            
            //var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => { 
             RunDlg<AlertDlg>(() => invalidConfigForm.btnNext.PerformClick(),
                 dlg =>
                 {
                     Assert.AreEqual(string.Format(
                             SkylineBatch.Properties.Resources
                                 .InvalidConfigSetupForm_GetValidPath_Would_you_like_to_replace__0__with__1___,
                             "D:\\Users\\nonexistent", "D:\\Users\\alimarsh"),
                         dlg.Message);
                     dlg.ClickYes();
                 });
             var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => { });
             RunUI(() => { editConfigForm.CancelButton.PerformClick(); });
             WaitForClosedForm(editConfigForm);
             Assert.AreEqual(3, mainForm.ConfigCount());
             Assert.AreEqual(0, mainForm.InvalidConfigCount());

             ClearConfigs(mainForm);
             mainForm.DoImport(invalidConfigFile);
             Assert.AreEqual(3, mainForm.ConfigCount());
             Assert.AreEqual(0, mainForm.InvalidConfigCount());

        }



    }
}
