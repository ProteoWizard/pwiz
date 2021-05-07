﻿using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;

namespace SkylineBatchTest
{
    [TestClass]
    public class ImportFunctionalTest : AbstractSkylineBatchFunctionalTest
    {
        public static string CONFIG_FOLDER;
        public static string TEST_FOLDER;

        [TestMethod]
        public void ImportTest()
        {
            TestFilesZipPaths = new[]
                {@"SkylineBatchTest\ImportFunctionalTest.zip", @"SkylineBatchTest\TestConfigurationFiles.zip"};

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TEST_FOLDER = TestFilesDirs[0].FullPath;
            CONFIG_FOLDER = TestFilesDirs[1].FullPath;

            var mainWindow = MainFormWindow();
            var mainForm = mainWindow as MainForm;
            Assert.IsNotNull(mainForm, "Main program window is not an instance of MainForm.");
            Assert.AreEqual(0, mainForm.ConfigCount());

            RInstallations.AddRDirectory(Path.Combine(CONFIG_FOLDER, "R"));

            TestImportValidConfigurations(mainForm);

            TestImportDuplicateConfigurations(mainForm);

            TestImportInvalidConfigurations(mainForm);

            TestImportInvalidRConfigurations(mainForm);

            TestImportInvalidDependentConfigurations(mainForm);

            TestImportInvalidSkylineConfigurations(mainForm);

            TestRootReplacement(mainForm);

            TestDriveRootReplacement(mainForm);

        }

        public void TestImportValidConfigurations(MainForm mainForm)
        {
            var validConfigFile = Path.Combine(TEST_FOLDER, "ValidConfigurations.bcfg");
            mainForm.DoImport(validConfigFile);
            RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 0, mainForm); });


            RunUI(() => { mainForm.ClickConfig(0); });
            var newConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            RunUI(() => { newConfigForm.CancelButton.PerformClick(); });
            WaitForClosedForm(newConfigForm);
        }

        public void TestImportDuplicateConfigurations(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var validConfigFile = Path.Combine(TEST_FOLDER, "ValidConfigurations.bcfg");
            mainForm.DoImport(validConfigFile);
            RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 0, mainForm); });

            RunDlg<AlertDlg>(() => mainForm.DoImport(validConfigFile),
                dlg =>
                {
                    Assert.AreEqual(SharedBatch.Properties.Resources.ConfigManager_ImportFrom_The_following_configurations_already_exist_ + Environment.NewLine +
                                    "\"RefineEmptyTemplate\"" + Environment.NewLine +
                                    "\"EmptyTemplate\"" + Environment.NewLine +
                                    "\"Bruderer\"" + Environment.NewLine +
                                    SharedBatch.Properties.Resources.ConfigManager_ImportFrom_Do_you_want_to_overwrite_these_configurations_,
                        dlg.Message);
                    dlg.ClickNo();
                });

            RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 0, mainForm); });

        }

        public void TestImportInvalidConfigurations(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var invalidPathsFile = Path.Combine(TEST_FOLDER, "InvalidPathConfigurations.bcfg");
            mainForm.DoImport(invalidPathsFile);
            RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 3, mainForm); });


            RunUI(() => { mainForm.ClickConfig(0); });
            var invalidConfigForm = ShowDialog<InvalidConfigSetupForm>(() => mainForm.ClickEdit());
            var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => invalidConfigForm.btnSkip.PerformClick());

            RunUI(() => { editConfigForm.CancelButton.PerformClick(); });
            WaitForClosedForm(invalidConfigForm);

            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var invalidConfigFile = Path.Combine(TEST_FOLDER, "InvalidSkylineConfigurations.bcfg");
            mainForm.DoImport(invalidConfigFile);
            RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 3, mainForm); });


            RunUI(() => { mainForm.ClickConfig(0); });
            var invalidConfigForm2 = ShowDialog<InvalidConfigSetupForm>(() => mainForm.ClickEdit());
            var editConfigForm2 = ShowDialog<SkylineBatchConfigForm>(() => invalidConfigForm2.btnSkip.PerformClick());

            RunUI(() => { editConfigForm2.CancelButton.PerformClick(); });
            WaitForClosedForm(invalidConfigForm);
        }

        public void TestImportInvalidRConfigurations(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var invalidConfigFile = Path.Combine(TEST_FOLDER, "InvalidRConfigurations.bcfg");

            RunDlg<AlertDlg>(() => mainForm.DoImport(invalidConfigFile),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.RDirectorySelector_AddIfNecassary_The_following_R_installations_were_not_found_by__0__, Program.AppName()) + Environment.NewLine +
                                    "400.0.3" + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.RDirectorySelector_AddIfNecassary_Would_you_like_to_add_an_R_installation_directory_,
                        dlg.Message);
                    dlg.ClickNo();
                });
            RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 3, mainForm); });


            RunUI(() => { mainForm.ClickConfig(0); });
            var invalidConfigForm = ShowDialog<InvalidConfigSetupForm>(() => mainForm.ClickEdit());
            var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => invalidConfigForm.btnSkip.PerformClick());

            RunUI(() => { editConfigForm.CancelButton.PerformClick(); });
            WaitForClosedForm(editConfigForm);
        }

        public void TestImportInvalidDependentConfigurations(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var invalidConfigFile = Path.Combine(TEST_FOLDER, "InvalidDependentConfigurations.bcfg");
            RunDlg<AlertDlg>(() => mainForm.DoImport(invalidConfigFile),
                dlg =>
                {
                    Assert.AreEqual(SkylineBatch.Properties.Resources.SkylineBatchConfigManager_AssignDependencies_The_following_configurations_use_refined_template_files_from_other_configurations_that_do_not_exist_ + Environment.NewLine +
                                    "EmptyTemplate" + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.SkylineBatchConfigManager_AssignDependencies_You_may_want_to_update_the_template_file_paths_,
                        dlg.Message);
                    dlg.ClickOk();
                });

            RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 1, mainForm); });

        }

        public void TestImportInvalidSkylineConfigurations(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var invalidConfigFile = Path.Combine(TEST_FOLDER, "InvalidSkylineConfigurations.bcfg");
            mainForm.DoImport(invalidConfigFile);
            RunUI(() =>
            {
                FunctionalTestUtil.CheckConfigs(3, 3, mainForm);
                mainForm.ClickConfig(0);
            });
            var invalidConfigForm = ShowDialog<InvalidConfigSetupForm>(() => {mainForm.ClickEdit();});
            RunUI(() => invalidConfigForm.CurrentControl.SetText(TestUtils.GetSkylineDir()) );
            RunDlg<AlertDlg>(() => invalidConfigForm.btnNext.PerformClick(),
                dlg =>
                {
                    Assert.AreEqual(SkylineBatch.Properties.Resources.MainForm_ReplaceAllSkylineVersions_Do_you_want_to_use_this_Skyline_version_for_all_configurations_,
                        dlg.Message);
                    dlg.ClickNo();
                });

            var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => { });
            RunUI(() => editConfigForm.btnSaveConfig.PerformClick());
            WaitForClosedForm(editConfigForm);

            RunUI(() =>
            {
                FunctionalTestUtil.CheckConfigs(3, 2, mainForm);
                mainForm.ClickConfig(1);
            });
            invalidConfigForm = ShowDialog<InvalidConfigSetupForm>(() => { mainForm.ClickEdit(); });
            editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => { invalidConfigForm.btnSkip.PerformClick(); });
            RunUI(() =>
            {
                editConfigForm.tabsConfig.SelectedIndex = 4;
                editConfigForm.SkylineTypeControl.SetText(TestUtils.GetSkylineDir());
            });
            RunDlg<AlertDlg>(() => editConfigForm.btnSaveConfig.PerformClick(),
                dlg => { dlg.ClickYes(); });
            WaitForClosedForm(editConfigForm);
            RunUI(() => FunctionalTestUtil.CheckConfigs(3, 0, mainForm));
        }

        public void TestRootReplacement(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var invalidConfigFile = Path.Combine(TEST_FOLDER, "InvalidPathConfigurations.bcfg");
            mainForm.DoImport(invalidConfigFile);
            RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 3, mainForm); });

            RunUI(() => { mainForm.ClickConfig(0); });
            var invalidConfigForm = ShowDialog<InvalidConfigSetupForm>(() => mainForm.ClickEdit());
            RunUI(() =>
            {
                invalidConfigForm.CurrentControl.SetText(
                    Path.Combine(CONFIG_FOLDER, "emptyTemplate.sky"));
            });
            
             RunDlg<AlertDlg>(() => invalidConfigForm.btnNext.PerformClick(),
                 dlg =>
                 {
                     Assert.AreEqual(string.Format(
                             SharedBatch.Properties.Resources
                                 .InvalidConfigSetupForm_GetValidPath_Would_you_like_to_replace__0__with__1___,
                             Path.GetDirectoryName(CONFIG_FOLDER) + "\\nonexistentFolder\\nonexistentFolderTwo",
                             Path.GetDirectoryName(CONFIG_FOLDER)),
                         dlg.Message);
                     dlg.ClickYes();
                 });
             var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => { });
             RunUI(() => { editConfigForm.CancelButton.PerformClick(); });
             WaitForClosedForm(editConfigForm);
             RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 0, mainForm); });

             RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
             mainForm.DoImport(invalidConfigFile);
             RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 0, mainForm); });
        }

        public void TestDriveRootReplacement(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var invalidConfigFile = Path.Combine(TEST_FOLDER, "InvalidRootConfigurations.bcfg");
            mainForm.DoImport(invalidConfigFile);
            RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 3, mainForm); });

            RunUI(() => { mainForm.ClickConfig(0); });
            var invalidConfigForm = ShowDialog<InvalidConfigSetupForm>(() => mainForm.ClickEdit());
            RunUI(() =>
            {
                invalidConfigForm.CurrentControl.SetText(
                    Path.Combine(CONFIG_FOLDER, "emptyTemplate.sky"));
            });

            RunDlg<AlertDlg>(() => invalidConfigForm.btnNext.PerformClick(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(
                            SharedBatch.Properties.Resources
                                .InvalidConfigSetupForm_GetValidPath_Would_you_like_to_replace__0__with__1___,
                            "Z:",
                            Path.GetDirectoryName(CONFIG_FOLDER)),
                        dlg.Message);
                    dlg.ClickYes();
                });
            var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => { });
            RunUI(() => { editConfigForm.CancelButton.PerformClick(); });
            WaitForClosedForm(editConfigForm);
            RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 0, mainForm); });

            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            mainForm.DoImport(invalidConfigFile);
            RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 0, mainForm); });
        }



    }
}
