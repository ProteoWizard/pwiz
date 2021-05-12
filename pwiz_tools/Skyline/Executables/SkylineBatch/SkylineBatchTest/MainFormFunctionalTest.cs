using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatchTest;
using SkylineBatch;
using SkylineBatchTest;

namespace SkylineBatchTest
{
    [TestClass]
    public class MainFormFunctionalTest : AbstractSkylineBatchFunctionalTest
    {
        [TestMethod]
        public void ManipulateListViewTest()
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

            TestAddValidConfigurations(mainForm);

            TestReorderConfigurations(mainForm);

            TestDeleteConfigurations(mainForm);

        }

        public void TestAddValidConfigurations(MainForm mainForm)
        {
            var newConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            RunUI(() =>
            {
                FunctionalTestUtil.PopulateConfigForm(newConfigForm, @"One", TestFilesDirs[0].FullPath, this);
                newConfigForm.btnSaveConfig.PerformClick();
            });

            WaitForClosedForm(newConfigForm);
            Assert.AreEqual(1, mainForm.ConfigCount());
            Assert.AreEqual("One", mainForm.ConfigName(0));

            Assert.AreEqual("One", mainForm.SelectedConfigName());

            newConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickCopy());
            RunUI(() =>
            {
                WaitForShownForm(newConfigForm);
                newConfigForm.textConfigName.Text = @"Two";
                newConfigForm.btnSaveConfig.PerformClick();
            });
            WaitForClosedForm(newConfigForm);
            Assert.AreEqual(2, mainForm.ConfigCount());
            Assert.AreEqual("One", mainForm.ConfigName(0));
            Assert.AreEqual("Two", mainForm.ConfigName(1));

            Assert.AreEqual("Two", mainForm.SelectedConfigName());

            newConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickCopy());
            RunUI(() =>
            {
                WaitForShownForm(newConfigForm);
                newConfigForm.textConfigName.Text = @"Three";
                newConfigForm.btnSaveConfig.PerformClick();
            });
            WaitForClosedForm(newConfigForm);
            Assert.AreEqual(3, mainForm.ConfigCount());
            Assert.AreEqual("One", mainForm.ConfigName(0));
            Assert.AreEqual("Two", mainForm.ConfigName(1));
            Assert.AreEqual("Three", mainForm.ConfigName(2));

            Assert.AreEqual("Three", mainForm.SelectedConfigName());
        }

        public void TestReorderConfigurations(MainForm mainForm)
        {
            Assert.AreEqual(3, mainForm.ConfigCount());

            RunUI(() =>
            {
                mainForm.ClickConfig(2);
                mainForm.ClickUp();
                mainForm.ClickUp();
            });
            Assert.AreEqual("Three", mainForm.SelectedConfigName());
            Assert.AreEqual("Three", mainForm.ConfigName(0));
            Assert.AreEqual("One", mainForm.ConfigName(1));
            Assert.AreEqual("Two", mainForm.ConfigName(2));

            RunUI(() =>
            {
                mainForm.ClickConfig(1);
                mainForm.ClickDown();
            });
            Assert.AreEqual("One", mainForm.SelectedConfigName());
            Assert.AreEqual("Three", mainForm.ConfigName(0));
            Assert.AreEqual("Two", mainForm.ConfigName(1));
            Assert.AreEqual("One", mainForm.ConfigName(2));
        }


        public void TestDeleteConfigurations(MainForm mainForm)
        {
            RunUI(() =>
            {
                mainForm.ClickDelete();
            });
            Assert.AreEqual(2, mainForm.ConfigCount());
            Assert.AreEqual("Two", mainForm.SelectedConfigName());

            RunUI(() =>
            {
                mainForm.ClickConfig(0);
                mainForm.ClickDelete();
            });
            Assert.AreEqual(1, mainForm.ConfigCount());
            Assert.AreEqual("Two", mainForm.SelectedConfigName());
        }

    }
}
