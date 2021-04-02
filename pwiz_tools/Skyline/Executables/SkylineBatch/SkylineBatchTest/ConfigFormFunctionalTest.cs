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
            TestFilesZip = @"SkylineBatchTest\ConfigFormFunctionalTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var mainWindow = MainFormWindow();
            var mainForm = mainWindow as MainForm;
            Assert.IsNotNull(mainForm, "Main program window is not an instance of MainForm.");
            Assert.AreEqual(0, mainForm.ConfigCount());

            TestAddInvalidConfiguration(mainForm);
            
        }


        private void PopulateNewConfigForm(SkylineBatchConfigForm newConfigForm, string name = "TestName")
        {
            WaitForShownForm(newConfigForm);
            newConfigForm.textConfigName.Text = name;
            newConfigForm.textTemplateFile.Text = Path.Combine(TestFilesDirs[0].FullPath, "emptyTemplate.sky");
            newConfigForm.textAnalysisPath.Text = Path.Combine(TestFilesDirs[0].FullPath, "analysisFolder");
            newConfigForm.textDataPath.Text = Path.Combine(TestFilesDirs[0].FullPath, "emptyData");
        }

        public void TestAddInvalidConfiguration(MainForm mainForm)
        {
            var newConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            RunUI(() =>
            {
                PopulateNewConfigForm(newConfigForm, string.Empty);
            });

            RunDlg<AlertDlg>(() => newConfigForm.Save(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.SkylineBatchConfig_SkylineBatchConfig___0___is_not_a_valid_name_for_the_configuration_, string.Empty) + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.SkylineBatchConfig_SkylineBatchConfig_Please_enter_a_name_,
                        dlg.Message);
                    dlg.ClickOk();
                });

            var nonexistentTemplate = TestUtils.GetTestFilePath("nonexistent.sky");
            RunUI(() =>
            {
                PopulateNewConfigForm(newConfigForm, @"TestConfig");
                newConfigForm.textTemplateFile.Text = nonexistentTemplate;
            });

            RunDlg<AlertDlg>(() => newConfigForm.Save(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.MainSettings_ValidateSkylineFile_The_Skyline_template_file__0__does_not_exist_, nonexistentTemplate) + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.MainSettings_ValidateSkylineFile_Please_provide_a_valid_file_,
                        dlg.Message);
                    dlg.ClickOk();
                });

            var nonexistentData = TestUtils.GetTestFilePath("nonexistentData");
            RunUI(() =>
            {
                PopulateNewConfigForm(newConfigForm, @"TestConfig");
                newConfigForm.textDataPath.Text = nonexistentData;
            });

            RunDlg<AlertDlg>(() => newConfigForm.Save(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.MainSettings_ValidateDataFolder_The_data_folder__0__does_not_exist_, nonexistentData) + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.MainSettings_ValidateAnalysisFolder_Please_provide_a_valid_folder_,
                        dlg.Message);
                    dlg.ClickOk();
                });

            var nonexistentAnalysis = TestUtils.GetTestFilePath("nonexistentFolderOne\\nonexistentFolderTwo");
            RunUI(() =>
            {
                PopulateNewConfigForm(newConfigForm, @"TestConfig");
                newConfigForm.textAnalysisPath.Text = nonexistentAnalysis;
            });

            RunDlg<AlertDlg>(() => newConfigForm.Save(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.MainSettings_ValidateAnalysisFolder_The__parent_directory_of_the_analysis_folder__0__does_not_exist_, Path.GetDirectoryName(nonexistentAnalysis)) + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.MainSettings_ValidateAnalysisFolder_Please_provide_a_valid_folder_,
                        dlg.Message);
                    dlg.ClickOk();
                });

            RunUI(() =>
            {
                PopulateNewConfigForm(newConfigForm, @"TestConfig");
                newConfigForm.textRefinedFilePath.Text = TestUtils.GetTestFilePath("refinedOutput.sky");
                newConfigForm.checkBoxRemoveData.Checked = false;
                newConfigForm.checkBoxRemoveDecoys.Checked = false;
            });

            RunDlg<AlertDlg>(() => newConfigForm.Save(),
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
