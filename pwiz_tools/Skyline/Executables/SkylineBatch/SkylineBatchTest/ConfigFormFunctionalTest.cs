using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SharedBatchTest;
using SkylineBatch;
using SkylineBatchTest;

namespace SkylineBatchTest
{
    [TestClass]
    public class ConfigFormFunctionalTest : AbstractSkylineBatchFunctionalTest
    {
        [TestMethod]
        public void AddConfigErrorsTest()
        {
            TestFilesZip = @"SkylineBatchTest\TestConfigurationFiles.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var mainWindow = MainFormWindow();
            var mainForm = mainWindow as MainForm;
            WaitForShownForm(mainForm);
            Assert.IsNotNull(mainForm, "Main program window is not an instance of MainForm.");
            Assert.AreEqual(0, mainForm.ConfigCount());

            TestAddInvalidConfiguration(mainForm);
            
        }

        public void TestAddInvalidConfiguration(MainForm mainForm)
        {
            var newConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            RunUI(() =>
            {
                FunctionalTestUtil.PopulateConfigForm(newConfigForm, string.Empty, TestFilesDirs[0].FullPath, this);
            });

            RunDlg<AlertDlg>(() => newConfigForm.btnSaveConfig.PerformClick(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.SkylineBatchConfig_SkylineBatchConfig___0___is_not_a_valid_name_for_the_configuration_, string.Empty) + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.SkylineBatchConfig_SkylineBatchConfig_Please_enter_a_name_,
                        dlg.Message);
                    dlg.ClickOk();
                });

            var nonexistentTemplate = Path.Combine(TestFilesDirs[0].FullPath, "nonexistent.sky");
            RunUI(() =>
            {
                FunctionalTestUtil.PopulateConfigForm(newConfigForm, @"TestConfig", TestFilesDirs[0].FullPath, this);
                newConfigForm.templateFileControl.Text = nonexistentTemplate;
            });

            RunDlg<AlertDlg>(() => newConfigForm.btnSaveConfig.PerformClick(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.MainSettings_ValidateSkylineFile_The_Skyline_template_file__0__does_not_exist_, nonexistentTemplate) + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.MainSettings_ValidateSkylineFile_Please_provide_a_valid_file_,
                        dlg.Message);
                    dlg.ClickOk();
                });

            var nonexistentData = Path.Combine(TestFilesDirs[0].FullPath, "nonexistentData");
            RunUI(() =>
            {
                FunctionalTestUtil.PopulateConfigForm(newConfigForm, @"TestConfig", TestFilesDirs[0].FullPath, this);
                newConfigForm.textDataPath.Text = Path.Combine(TestFilesDirs[0].FullPath, "nonexistentData");
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
                FunctionalTestUtil.PopulateConfigForm(newConfigForm, @"TestConfig", TestFilesDirs[0].FullPath, this);
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
                FunctionalTestUtil.PopulateConfigForm(newConfigForm, @"TestConfig", TestFilesDirs[0].FullPath, this);
                newConfigForm.textRefinedFilePath.Text = Path.Combine(TestFilesDirs[0].FullPath, "refinedOutput.sky");
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
