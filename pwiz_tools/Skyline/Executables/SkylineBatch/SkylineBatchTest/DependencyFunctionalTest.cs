using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;
using SkylineBatchTest;

namespace SkylineBatchTest
{
    [TestClass]
    public class DependencyFunctionalTest : AbstractSkylineBatchFunctionalTest
    {
        [TestMethod]
        public void DependencyTest()
        {
            TestFilesZipPaths = new[]
                {@"SkylineBatchTest\DependencyFunctionalTest.zip", @"SkylineBatchTest\TestConfigurationFiles.zip"};

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var mainWindow = MainFormWindow();
            var mainForm = mainWindow as MainForm;
            Assert.IsNotNull(mainForm, "Main program window is not an instance of MainForm.");
            RunUI(() => { FunctionalTestUtil.CheckConfigs(0, 0, mainForm); });

            RInstallations.AddRDirectory(Path.Combine(TestFilesDirs[1].FullPath, "R"));

            TestCreateDependency(mainForm);

            TestImportDependency(mainForm);

            //TestRunDependency(mainForm);*/

        }

        public void TestCreateDependency(MainForm mainForm)
        {
            var baseConfigFile = Path.Combine(TestFilesDirs[0].FullPath, "BaseConfiguration.bcfg");
            mainForm.DoImport(baseConfigFile);

            RunUI(() => { FunctionalTestUtil.CheckConfigs(1, 0, mainForm); });

            RunUI(() => { mainForm.ClickConfig(0); });
            var editConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickEdit());
            Assert.AreEqual(false, editConfigForm.comboTemplateFile.Visible);
            RunUI(() => { editConfigForm.CancelButton.PerformClick(); });
            WaitForClosedForm(editConfigForm);

            var addConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            WaitForShownForm(addConfigForm);
            Assert.AreEqual(true, addConfigForm.comboTemplateFile.Visible);
            RunUI(() =>
            {
                FunctionalTestUtil.PopulateConfigForm(addConfigForm, "DependentConfig", TestFilesDirs[1].FullPath, this);
                addConfigForm.comboTemplateFile.Text = Path.Combine(TestFilesDirs[1].FullPath, "RefinedOutput.sky");
                addConfigForm.Save();
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
            var baseConfigFile = Path.Combine(TestFilesDirs[0].FullPath, "BaseConfiguration.bcfg");
            mainForm.DoImport(baseConfigFile);
            RunUI(() => { FunctionalTestUtil.CheckConfigs(1, 0, mainForm); });

            var validDependentConfigFile = Path.Combine(TestFilesDirs[0].FullPath, "ValidDependentConfiguration.bcfg");
            mainForm.DoImport(validDependentConfigFile);
            RunUI(() => { FunctionalTestUtil.CheckConfigs(2, 0, mainForm); });


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
        }
    }
}
