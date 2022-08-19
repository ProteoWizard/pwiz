using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SharedBatch.Properties;
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
            WaitForShownForm(mainForm);
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

            TestVersionNumbers(mainForm);
        }

        public void TestVersionNumbers(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var validConfigFile1 = Path.Combine(TEST_FOLDER, "ValidConfigurationsVersion1.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(validConfigFile1);
                FunctionalTestUtil.CheckConfigs(3, 0, mainForm);
            });

            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var validConfigFile2 = Path.Combine(TEST_FOLDER, "ValidConfigurationsVersion2.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(validConfigFile2);
                FunctionalTestUtil.CheckConfigs(3, 0, mainForm);
            });

            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var validConfigFile3 = Path.Combine(TEST_FOLDER, "ValidConfigurationsVersion3.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(validConfigFile3);
                FunctionalTestUtil.CheckConfigs(3, 0, mainForm);
            });

            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var invalidConfigFile1 = Path.Combine(TEST_FOLDER, "InvalidConfigurationsVersion1.bcfg");
            RunDlg<AlertDlg>(() => mainForm.DoImport(invalidConfigFile1),
                dlg =>
                {
                    Assert.AreEqual(string.Format(
                                        Resources.ConfigManager_Import_An_error_occurred_while_importing_configurations_from__0__,
                                        invalidConfigFile1) + Environment.NewLine +
                                    string.Format(Resources
                                            .ConfigManager_ImportFrom_The_version_of_the_file_to_import_from__0__is_newer_than_the_version_of_the_program__1___Please_update_the_program_to_import_configurations_from_this_file_,
                                        "100.1", SkylineBatch.Properties.Settings.Default.XmlVersion), dlg.Message);
                    dlg.ClickOk();
                });
        }

        public void TestImportValidConfigurations(MainForm mainForm)
        {
            var validConfigFile = Path.Combine(TEST_FOLDER, "ValidConfigurations.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(validConfigFile);
                FunctionalTestUtil.CheckConfigs(3, 0, mainForm);
            });


            RunUI(() => { mainForm.ClickConfig(0); });
            var newConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            RunUI(() => { newConfigForm.CancelButton.PerformClick(); });
            WaitForClosedForm(newConfigForm);
        }

        public void TestImportDuplicateConfigurations(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var validConfigFile = Path.Combine(TEST_FOLDER, "ValidConfigurations.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(validConfigFile);
                FunctionalTestUtil.CheckConfigs(3, 0, mainForm);
            });

            RunDlg<AlertDlg>(() => mainForm.DoImport(validConfigFile),
                dlg =>
                {
                    Assert.AreEqual(Resources.ConfigManager_ImportFrom_The_following_configurations_already_exist_ + Environment.NewLine +
                                    "\"RefineEmptyTemplate\"" + Environment.NewLine +
                                    "\"EmptyTemplate\"" + Environment.NewLine +
                                    "\"Bruderer\"" + Environment.NewLine +
                                    Resources.ConfigManager_ImportFrom_Do_you_want_to_overwrite_these_configurations_,
                        dlg.Message);
                    dlg.ClickNo();
                });

            RunUI(() => { FunctionalTestUtil.CheckConfigs(3, 0, mainForm); });

        }

        public void TestImportInvalidConfigurations(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var invalidPathsFile = Path.Combine(TEST_FOLDER, "InvalidPathConfigurations.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(invalidPathsFile);
                FunctionalTestUtil.CheckConfigs(3, 3, mainForm);
            });


            RunUI(() => { mainForm.ClickConfig(0); });
            var invalidConfigForm = ShowDialog<InvalidConfigSetupForm>(() => mainForm.ClickEdit());
            var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => invalidConfigForm.btnSkip.PerformClick());

            RunUI(() => { editConfigForm.CancelButton.PerformClick(); });
            WaitForClosedForm(invalidConfigForm);

            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var invalidConfigFile = Path.Combine(TEST_FOLDER, "InvalidSkylineConfigurations.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(invalidConfigFile);
                FunctionalTestUtil.CheckConfigs(3, 3, mainForm);
            });


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
            RunUI(() =>
            {
                mainForm.DoImport(invalidConfigFile);
                FunctionalTestUtil.CheckConfigs(3, 3, mainForm);
                mainForm.ClickConfig(0);
            });
            var invalidConfigForm = ShowDialog<InvalidConfigSetupForm>(() => {mainForm.ClickEdit();});
            RunUI(() => invalidConfigForm.CurrentControl.SetInput(TestUtils.GetSkylineDir()) );
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
            var invalidConfigFormTwo = ShowDialog<InvalidConfigSetupForm>(() =>
            {
                mainForm.ClickEdit();
            });
            RunUI(() => invalidConfigFormTwo.btnSkip.PerformClick());
            WaitForClosedForm(invalidConfigFormTwo);
            var editConfigFormTwo = FindOpenForm<SkylineBatchConfigForm>();
            
            //RunUI(() => { invalidConfigForm.btnSkip.PerformClick(); });
            //WaitForClosedForm(invalidConfigForm);
            //var newEditConfigForm = ShowDialog<SkylineBatchConfigForm>(() => { });
            RunUI(() =>
            {
                editConfigFormTwo.tabsConfig.SelectedIndex = 4;
                editConfigFormTwo.SkylineTypeControl.SetInput(TestUtils.GetSkylineDir());
            });
            RunDlg<AlertDlg>(() => editConfigFormTwo.btnSaveConfig.PerformClick(),
                dlg => { dlg.ClickYes(); });
            WaitForClosedForm(editConfigFormTwo);
            RunUI(() => FunctionalTestUtil.CheckConfigs(3, 0, mainForm));
        }

        public void TestRootReplacement(MainForm mainForm)
        {
            // Remove existing configurations and import from InvalidPathConfigurations.bcfg
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var invalidConfigFile = Path.Combine(TEST_FOLDER, "InvalidPathConfigurations.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(invalidConfigFile);
                FunctionalTestUtil.CheckConfigs(3, 3, mainForm);
            });
            // Bring up the Configuration Set Up Manager (invalidConfigForm) by editing an invalid configuration at index 0
            RunUI(() => { mainForm.ClickConfig(0); });
            var invalidConfigForm = ShowDialog<InvalidConfigSetupForm>(() => mainForm.ClickEdit());
            // Change the template file path in the Configuration Set Up Manager
            RunUI(() =>
            {
                invalidConfigForm.CurrentControl.SetInput(
                    Path.Combine(CONFIG_FOLDER, "emptyTemplate.sky"));
            });
            // Click next to bring up the alert asking if you want to do path replacement. Click yes.
             RunDlg<AlertDlg>(() => invalidConfigForm.btnNext.PerformClick(),
                 dlg =>
                 {
                     Assert.AreEqual(string.Format(
                             Resources
                                 .InvalidConfigSetupForm_GetValidPath_Would_you_like_to_replace__0__with__1___,
                             "C:\\nonexistentFolder\\nonexistentFolderTwo",
                             Path.GetDirectoryName(CONFIG_FOLDER)),
                         dlg.Message);
                     dlg.ClickYes();
                 });
             // Get the edit config form that appears after the Configuration Set Up Manager closes
             var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => { });
             // Click cancel and wait for close.
             RunUI(() => { editConfigForm.CancelButton.PerformClick(); });
             WaitForClosedForm(editConfigForm);
             // Check that the configurations are all valid now.
             RunUI(() =>
             {
                 FunctionalTestUtil.CheckConfigs(3, 0, mainForm);
                 FunctionalTestUtil.ClearConfigs(mainForm);
                mainForm.DoImport(invalidConfigFile);
                FunctionalTestUtil.CheckConfigs(3, 0, mainForm, "Expected 3 imported configs", "Expected configs to be valid from root replacement.");
            });
        }

        public void TestDriveRootReplacement(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var invalidConfigFile = Path.Combine(TEST_FOLDER, "InvalidRootConfigurations.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(invalidConfigFile);
                FunctionalTestUtil.CheckConfigs(3, 3, mainForm);
            });

            RunUI(() => { mainForm.ClickConfig(0); });
            var invalidConfigForm = ShowDialog<InvalidConfigSetupForm>(() => mainForm.ClickEdit());
            RunUI(() =>
            {
                invalidConfigForm.CurrentControl.SetInput(
                    Path.Combine(CONFIG_FOLDER, "emptyTemplate.sky"));
            });

            RunDlg<AlertDlg>(() => invalidConfigForm.btnNext.PerformClick(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(
                            Resources
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
            RunUI(() =>
            {
                mainForm.DoImport(invalidConfigFile);
                FunctionalTestUtil.CheckConfigs(3, 0, mainForm);
            });
        }

    }
}
