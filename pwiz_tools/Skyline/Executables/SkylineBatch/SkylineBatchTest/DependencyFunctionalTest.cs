using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;

namespace SkylineBatchTest
{
    [TestClass]
    public class DependencyFunctionalTest : AbstractSkylineBatchFunctionalTest
    {
        public static string CONFIG_FOLDER;
        public static string BCFG_FOLDER;

        [TestMethod]
        public void DependencyTest()
        {
            TestFilesZipPaths = new[]
                {@"SkylineBatchTest\DependencyFunctionalTest.zip", @"SkylineBatchTest\TestConfigurationFiles.zip"};

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            BCFG_FOLDER = TestFilesDirs[0].FullPath;
            CONFIG_FOLDER = TestFilesDirs[1].FullPath;
            var mainWindow = MainFormWindow();
            var mainForm = mainWindow as MainForm;
            WaitForShownForm(mainForm);
            Assert.IsNotNull(mainForm, "Main program window is not an instance of MainForm.");
            RunUI(() => { FunctionalTestUtil.CheckConfigs(0, 0, mainForm); });

            RInstallations.AddRDirectory(Path.Combine(CONFIG_FOLDER, "R"));

            TestCreateDependency(mainForm);

            TestImportDependency(mainForm);

            TestUpdateDependent(mainForm);

            TestRunDependency(mainForm);

        }

        public void TestCreateDependency(MainForm mainForm)
        {
            var baseConfigFile = Path.Combine(BCFG_FOLDER, "BaseConfiguration.bcfg");
            mainForm.DoImport(baseConfigFile);
            RunUI(() => { FunctionalTestUtil.CheckConfigs(1, 0, mainForm); });

            RunUI(() => { mainForm.ClickConfig(0); });
            var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            RunUI(() => { editConfigForm.tabsConfig.SelectedIndex = 0; });
            Assert.AreEqual(false, editConfigForm.comboTemplateFile.Visible);
            RunUI(() => { editConfigForm.CancelButton.PerformClick(); });
            WaitForClosedForm(editConfigForm);

            var addConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            WaitForShownForm(addConfigForm);
            RunUI(() => { editConfigForm.tabsConfig.SelectedIndex = 0; });
            Assert.AreEqual(true, addConfigForm.comboTemplateFile.Visible);
            RunUI(() =>
            {
                FunctionalTestUtil.PopulateConfigForm(addConfigForm, "DependentConfig", CONFIG_FOLDER, this);
                addConfigForm.templateControl.SetPath(Path.Combine(CONFIG_FOLDER, "RefinedOutput.sky"));
                addConfigForm.btnSaveConfig.PerformClick();
            });
            WaitForClosedForm(addConfigForm);
            RunUI(() => { FunctionalTestUtil.CheckConfigs(2, 0, mainForm); });

            RunUI(() => { mainForm.ClickConfig(0); });
            RunDlg<AlertDlg>(() => mainForm.ClickDelete(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.SkylineBatchConfigManager_UserRemoveSelected_Deleting___0___may_impact_the_template_files_of_the_following_configurations_, "BaseConfiguration") + Environment.NewLine +
                                    "DependentConfig" + Environment.NewLine +
                                    string.Format(SkylineBatch.Properties.Resources.SkylineBatchConfigManager_UserRemoveSelected_Are_you_sure_you_want_to_delete___0___, "BaseConfiguration"),
                        dlg.Message);
                    dlg.ClickNo();
                });

            RunUI(() => { FunctionalTestUtil.CheckConfigs(2, 0, mainForm); });

            RunUI(() => { mainForm.ClickConfig(0); });
            RunDlg<AlertDlg>(() => mainForm.ClickDelete(),
                dlg => { dlg.ClickYes(); });
            RunUI(() => { FunctionalTestUtil.CheckConfigs(1, 1, mainForm); });
        }

        public void TestImportDependency(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var baseConfigFile = Path.Combine(BCFG_FOLDER, "BaseConfiguration.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(baseConfigFile);
                FunctionalTestUtil.CheckConfigs(1, 0, mainForm);
            });

            var validDependentConfigFile = Path.Combine(BCFG_FOLDER, "ValidDependentConfiguration.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(validDependentConfigFile);
                FunctionalTestUtil.CheckConfigs(2, 0, mainForm);
            });
            AssertDependentMatches(mainForm, Path.Combine(CONFIG_FOLDER, "RefinedOutput.sky"), 1);

            RunUI(() => { mainForm.ClickConfig(0); });
            RunDlg<AlertDlg>(() => mainForm.ClickDelete(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.SkylineBatchConfigManager_UserRemoveSelected_Deleting___0___may_impact_the_template_files_of_the_following_configurations_, "BaseConfiguration") + Environment.NewLine +
                                    "ValidDependent" + Environment.NewLine +
                                    string.Format(SkylineBatch.Properties.Resources.SkylineBatchConfigManager_UserRemoveSelected_Are_you_sure_you_want_to_delete___0___, "BaseConfiguration"),
                        dlg.Message);
                    dlg.ClickYes();
                });
            RunUI(() => { FunctionalTestUtil.CheckConfigs(1, 1, mainForm); });
            
            RunUI(() =>
            {
                FunctionalTestUtil.ClearConfigs(mainForm);
                mainForm.DoImport(baseConfigFile);
                FunctionalTestUtil.CheckConfigs(1, 0, mainForm);
            });
            var invalidDependentConfigsFile = Path.Combine(BCFG_FOLDER, "InvalidDependentConfigurations.bcfg");
            RunDlg<AlertDlg>(() => { mainForm.DoImport(invalidDependentConfigsFile);},
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.SkylineBatchConfigManager_AssignDependencies_The_following_configurations_use_refined_template_files_from_other_configurations_that_do_not_exist_, "BaseConfiguration") + Environment.NewLine +
                                    "ValidPathInvalidDependent" + Environment.NewLine +
                                    "InvalidPathInvalidDependent" + Environment.NewLine +
                                    string.Format(SkylineBatch.Properties.Resources.SkylineBatchConfigManager_AssignDependencies_You_may_want_to_update_the_template_file_paths_, "BaseConfiguration"),
                        dlg.Message);
                    dlg.ClickOk();
                });
            //Thread.SpinWait(100000);
            RunUI(() =>
            {
                FunctionalTestUtil.CheckConfigs(3, 1, mainForm);
                mainForm.ClickConfig(0);
                mainForm.ClickDelete();
                FunctionalTestUtil.CheckConfigs(2, 1, mainForm);
                mainForm.ClickConfig(1);
                mainForm.ClickDelete();
                FunctionalTestUtil.CheckConfigs(1, 0, mainForm);
            });
        }

        public void TestUpdateDependent(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var baseConfigFile = Path.Combine(BCFG_FOLDER, "BaseConfiguration.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(baseConfigFile);
                FunctionalTestUtil.CheckConfigs(1, 0, mainForm);
            });

            var validDependentConfigFile = Path.Combine(BCFG_FOLDER, "ValidDependentConfiguration.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(validDependentConfigFile);
                FunctionalTestUtil.CheckConfigs(2, 0, mainForm);
            });

            var newTemplate = Path.Combine(CONFIG_FOLDER, "NewRefinedOutput.sky");
            ChangePath(mainForm, 0, newTemplate, false, false);
            RunUI(() => { FunctionalTestUtil.CheckConfigs(2, 0, mainForm); });
            AssertDependentMatches(mainForm, newTemplate, 1);

            var independentTemplate = Path.Combine(CONFIG_FOLDER, "emptyTemplate.sky");
            ChangePath(mainForm, 1, independentTemplate, true, true);
            ChangePath(mainForm, 0, Path.Combine(CONFIG_FOLDER, "Nonexistent.sky"), false, false);
            AssertDependentMatches(mainForm, independentTemplate, 1);
        }

        public void TestRunDependency(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var baseConfigFile = Path.Combine(BCFG_FOLDER, "BaseConfiguration.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(baseConfigFile);
                FunctionalTestUtil.CheckConfigs(1, 0, mainForm);
            });

            var validDependentConfigFile = Path.Combine(BCFG_FOLDER, "ValidDependentConfiguration.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(validDependentConfigFile);
                FunctionalTestUtil.CheckConfigs(2, 0, mainForm);
            });
            RunUI(() =>
            {
                mainForm.SetConfigEnabled(1, true);
            });
            RunDlg<AlertDlg>(() => mainForm.ClickRun(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.SkylineBatchConfigManager_StartBatchRun_Configuration__0__must_be_run_before__1__to_generate_its_template_file_, "BaseConfiguration", "ValidDependent") + Environment.NewLine +
                                    string.Format(SkylineBatch.Properties.Resources.SkylineBatchConfigManager_StartBatchRun_Please_reorder_the_configurations_so__0__runs_first_, "BaseConfiguration"),
                        dlg.Message);
                    dlg.ClickOk();
                });
            RunUI(() =>
            {
                mainForm.ClickConfig(0);
                mainForm.SetConfigEnabled(0, true);
                mainForm.ClickDown();
            });
            RunDlg<AlertDlg>(() => mainForm.ClickRun(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SkylineBatch.Properties.Resources.SkylineBatchConfigManager_StartBatchRun_Configuration__0__must_be_run_before__1__to_generate_its_template_file_, "BaseConfiguration", "ValidDependent") + Environment.NewLine +
                                    string.Format(SkylineBatch.Properties.Resources.SkylineBatchConfigManager_StartBatchRun_Please_reorder_the_configurations_so__0__runs_first_, "BaseConfiguration"),
                        dlg.Message);
                    dlg.ClickOk();
                });
            RunUI(() =>
            {
                mainForm.ClickConfig(1);
                mainForm.ClickUp();
                mainForm.ClickRun();
            });
        }

        private void AssertDependentMatches(MainForm mainForm, string expectedTemplate, int dependentIndex)
        {
            RunUI(() => mainForm.ClickConfig(dependentIndex));
            var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            RunUI(() =>
            {
                editConfigForm.tabsConfig.SelectedIndex = 0;
                Assert.AreEqual(true, editConfigForm.comboTemplateFile.Visible);
                Assert.AreEqual(expectedTemplate, editConfigForm.comboTemplateFile.Text);
                editConfigForm.CancelButton.PerformClick();
            });
            WaitForClosedForm(editConfigForm);
        }

        private void ChangePath(MainForm mainForm, int configIndex, string newPath, bool templatePath, bool comboVisible)
        {
            RunUI(() => mainForm.ClickConfig(configIndex));
            var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            RunUI(() =>
            {
                editConfigForm.tabsConfig.SelectedIndex = 0;
                Assert.AreEqual(comboVisible, editConfigForm.comboTemplateFile.Visible);
                if (templatePath)
                {
                    editConfigForm.templateControl.SetPath(newPath);
                }
                else
                {
                    editConfigForm.textRefinedFilePath.Text = newPath;
                }
                editConfigForm.btnSaveConfig.PerformClick();
            });
            WaitForClosedForm(editConfigForm);
        }
    }
}
