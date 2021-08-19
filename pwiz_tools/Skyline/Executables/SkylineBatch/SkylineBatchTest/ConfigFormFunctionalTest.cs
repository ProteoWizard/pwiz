﻿using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;

namespace SkylineBatchTest
{
    [TestClass]
    public class ConfigFormFunctionalTest : AbstractSkylineBatchFunctionalTest
    {
        public static string CONFIG_FOLDER; // folder containing template file, data, reports, etc used by test configs
        public static string TEST_FOLDER;  // folder containing bcfg file(s)

        [TestMethod]
        public void AddConfigErrorsTest()
        {
            TestFilesZipPaths = new[]
                {@"SkylineBatchTest\ConfigFormFunctionalTest.zip", @"SkylineBatchTest\TestConfigurationFiles.zip"};
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

            TestAddInvalidConfiguration(mainForm);

            TestEditInvalidDownloadingFolderPath(mainForm);

            TestZipFiles(mainForm);
        }
        private bool ConfigRunning(MainForm mainForm, bool expectedAnswer)
        {
            bool worked = false;
            RunUI(() =>
            {
                worked = expectedAnswer == mainForm.ConfigRunning("Bruderer");
            });
            return worked;
        }

        public void TestZipFiles(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var zipPathFile = Path.Combine(TEST_FOLDER, "zip_path_test_config.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(zipPathFile);
                FunctionalTestUtil.CheckConfigs(1, 0, mainForm);
            });

            RunUI(() => { mainForm.ClickRun(1); });
            //var longWaitDialog = ShowDialog<LongWaitDlg>(() => mainForm.ClickRun(1));
            //WaitForClosedForm(longWaitDialog);
            var tenSeconds = new TimeSpan(0, 0, 10);
            FunctionalTestUtil.WaitForCondition(ConfigRunning, mainForm, true, tenSeconds, 200,
                "Config did not start");
            RunUI(() => { mainForm.tabMain.SelectedIndex = 0; });
            var oneMinute = new TimeSpan(0, 1, 0);
            FunctionalTestUtil.WaitForCondition(ConfigRunning, mainForm, false, oneMinute, 1000,
                "Config ran past timeout");
            
            RunUI(() => { mainForm.ClickRun(); });
            var longWaitDialog2 = FindOpenForm<LongWaitDlg>();
            WaitForClosedForm(longWaitDialog2);
            FunctionalTestUtil.WaitForCondition(ConfigRunning, mainForm, true, tenSeconds, 200,
                "Config did not start");
            FunctionalTestUtil.WaitForCondition(ConfigRunning, mainForm, false, oneMinute, 1000,
                "Config ran past timeout");
            var alertDlg = FindOpenForm<AlertDlg>();
            if (alertDlg != null)
            {
                alertDlg.ClickOk();
                WaitForClosedForm(alertDlg);
                Assert.Fail("An unexpected alert appeared with message: " + alertDlg.Message);
            }

        }

        public void TestEditInvalidDownloadingFolderPath(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var invalidConfigFile = Path.Combine(TEST_FOLDER, "InvalidPathDownloadingConfigurations.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(invalidConfigFile);
                FunctionalTestUtil.CheckConfigs(1, 1, mainForm);
            });

            RunUI(() => { mainForm.ClickConfig(0); });
            var invalidConfigForm = ShowDialog<InvalidConfigSetupForm>(() => mainForm.ClickEdit());
            var configDlg = ShowDialog<SkylineBatchConfigForm>(() => invalidConfigForm.btnSkip.PerformClick());
            var initialFilePath = configDlg.comboTemplateFile.Text;
            var downloadDlg = ShowDialog<PanoramaFileForm>(() => configDlg.templateControl.btnDownload.PerformClick());
            RunUI(() => { downloadDlg.btnSave.PerformClick(); });
            var currFilePath = configDlg.comboTemplateFile.Text;
            RunUI(() => { configDlg.CancelButton.PerformClick(); });
            WaitForClosedForm(configDlg);
            Assert.AreEqual(initialFilePath, currFilePath);
        }

        public void TestAddInvalidConfiguration(MainForm mainForm)
        {
            var newConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            RunUI(() =>
            {
                FunctionalTestUtil.PopulateConfigForm(newConfigForm, string.Empty, CONFIG_FOLDER, this);
            });

            RunDlg<AlertDlg>(() => newConfigForm.btnSaveConfig.PerformClick(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.SkylineBatchConfig_SkylineBatchConfig___0___is_not_a_valid_name_for_the_configuration_, string.Empty) + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.SkylineBatchConfig_SkylineBatchConfig_Please_enter_a_name_,
                        dlg.Message);
                    dlg.ClickOk();
                });

            var nonexistentTemplate = Path.Combine(CONFIG_FOLDER, "nonexistent.sky");
            RunUI(() =>
            {
                FunctionalTestUtil.PopulateConfigForm(newConfigForm, @"TestConfig", CONFIG_FOLDER, this);
                newConfigForm.templateControl.SetPath(nonexistentTemplate);
            });

            RunDlg<AlertDlg>(() => newConfigForm.btnSaveConfig.PerformClick(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.MainSettings_ValidateSkylineFile_The_Skyline_template_file__0__does_not_exist_, nonexistentTemplate) + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.MainSettings_ValidateSkylineFile_Please_provide_a_valid_file_,
                        dlg.Message);
                    dlg.ClickOk();
                });

            var nonexistentData = Path.Combine(CONFIG_FOLDER, "nonexistentData");
            RunUI(() =>
            {
                FunctionalTestUtil.PopulateConfigForm(newConfigForm, @"TestConfig", CONFIG_FOLDER, this);
                newConfigForm.dataControl.SetPath(Path.Combine(CONFIG_FOLDER, "nonexistentData"));
            });

            RunDlg<AlertDlg>(() => newConfigForm.btnSaveConfig.PerformClick(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.MainSettings_ValidateDataFolder_The_data_folder__0__does_not_exist_, nonexistentData) + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.MainSettings_ValidateAnalysisFolder_Please_provide_a_valid_folder_,
                        dlg.Message);
                    dlg.ClickOk();
                });

            var nonexistentAnalysis = Path.Combine(TestFilesDirs[0].FullPath, "nonexistentFolderOne\\nonexistentFolderTwo");
            RunUI(() =>
            {
                FunctionalTestUtil.PopulateConfigForm(newConfigForm, @"TestConfig", CONFIG_FOLDER, this);
                newConfigForm.textAnalysisPath.Text = nonexistentAnalysis;
            });

            RunDlg<AlertDlg>(() => newConfigForm.btnSaveConfig.PerformClick(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.MainSettings_ValidateAnalysisFolder_The__parent_directory_of_the_analysis_folder__0__does_not_exist_, Path.GetDirectoryName(nonexistentAnalysis)) + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.MainSettings_ValidateAnalysisFolder_Please_provide_a_valid_folder_,
                        dlg.Message);
                    dlg.ClickOk();
                });

            RunUI(() =>
            {
                FunctionalTestUtil.PopulateConfigForm(newConfigForm, @"TestConfig", CONFIG_FOLDER, this);
                newConfigForm.textRefinedFilePath.Text = Path.Combine(CONFIG_FOLDER, "refinedOutput.sky");
                newConfigForm.checkBoxRemoveData.Checked = false;
                newConfigForm.checkBoxRemoveDecoys.Checked = false;
            });

            RunDlg<AlertDlg>(() => newConfigForm.btnSaveConfig.PerformClick(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.RefineSettings_Validate_No_refine_commands_have_been_selected_, Path.GetDirectoryName(nonexistentAnalysis)) + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.RefineSettings_Validate_Please_enter_values_for_the_refine_commands_you_wish_to_use__or_skip_the_refinement_step_by_removing_the_file_path_on_the_refine_tab_,
                        dlg.Message);
                    dlg.ClickOk();
                });

            RunUI(() => { newConfigForm.CancelButton.PerformClick(); });
            WaitForClosedForm(newConfigForm);
        }

        

    }
}
