using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SharedBatchTest;
using SkylineBatch;
using SkylineBatch.Properties;

namespace SkylineBatchTest
{
    [TestClass]
    public class FunctionalTest : AbstractSkylineBatchFunctionalTest
    {
        [TestMethod]
        public void CreateConfigsTest()
        {
            TestFilesZip = TestUtils.GetTestFilePath("testZip.zip");
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var mainWindow = MainFormWindow();
            var mainForm = mainWindow as MainForm;
            Assert.IsNotNull(mainForm, "Main program window is not an instance of MainForm.");
            Assert.AreEqual(0, mainForm.ConfigCount());

            var newConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.HandleNewConfigClick());

            RunUI(() =>
            {
                PopulateNewConfigForm(newConfigForm);
                newConfigForm.Save();
            });

            WaitForClosedForm(newConfigForm);
            Assert.AreEqual(1, mainForm.ConfigCount());

            var newConfigForm2 = ShowDialog<SkylineBatchConfigForm>(() => Program.MainWindow.HandleNewConfigClick());
            RunUI(() =>
            {
                PopulateNewConfigForm(newConfigForm2);
            });

            RunDlg<AlertDlg>(() => newConfigForm2.Save(),
                dlg =>
                {
                    Assert.AreEqual(string.Format(SharedBatch.Properties.Resources.ConfigManager_InsertConfiguration_Configuration___0___already_exists_, @"TestName") + Environment.NewLine +
                                    SharedBatch.Properties.Resources.ConfigManager_InsertConfiguration_Please_enter_a_unique_name_for_the_configuration_,
                        dlg.Message);
                    dlg.ClickOk();
                });

            RunUI(() => { newConfigForm2.CancelButton.PerformClick(); });
            WaitForClosedForm(newConfigForm2);
        }


        private void PopulateNewConfigForm(SkylineBatchConfigForm newConfigForm)
        {
            WaitForShownForm(newConfigForm);
            var testFilesDir = new TestFilesDir(TestContext, TestFilesZip);
            newConfigForm.textConfigName.Text = @"TestName";
            newConfigForm.textTemplateFile.Text = TestUtils.GetTestFilePath(@"emptyTemplate.sky");
            newConfigForm.textAnalysisPath.Text = TestUtils.GetTestFilePath("");
            newConfigForm.textDataPath.Text = TestUtils.GetTestFilePath("");
        }
    }
}
