using System;
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
        private string TEST_FOLDER;
        private string CONFIG_FOLDER;

        [TestMethod]
        public void ManipulateListViewTest()
        {
            TestFilesZipPaths = new[]
                {@"SkylineBatchTest\MainFormFunctionalTest.zip", @"SkylineBatchTest\TestConfigurationFiles.zip"};
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

            TestAddValidConfigurations(mainForm);

            TestReorderConfigurations(mainForm);

            TestDeleteConfigurations(mainForm);

            TestEnableConfigs(mainForm);

        }

        public void TestAddValidConfigurations(MainForm mainForm)
        {
            var newConfigForm = ShowDialog<SkylineBatchConfigForm>(() => mainForm.ClickAdd());
            RunUI(() =>
            {
                FunctionalTestUtil.PopulateConfigForm(newConfigForm, @"One", CONFIG_FOLDER, this);
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

        public void TestEnableConfigs(MainForm mainForm)
        {
            RunUI(() => FunctionalTestUtil.ClearConfigs(mainForm));
            var validConfigFile = Path.Combine(TEST_FOLDER, "SevenConfigurations.bcfg");
            RunUI(() =>
            {
                mainForm.DoImport(validConfigFile);
                FunctionalTestUtil.CheckConfigs(7, 0, mainForm);
            });
            var checkState = new[] {false, false, false, false, false, false, false};
            var random = new Random();
            for (int i = 0; i < 100; i++)
            {
                VerifyCheckState(mainForm, checkState);
                var randomIndex = random.Next(0, checkState.Length);
                checkState[randomIndex] = !checkState[randomIndex];
                RunUI(() =>
                {
                    mainForm.SetConfigEnabled(randomIndex, checkState[randomIndex]);
                });
            }
        }

        private void VerifyCheckState(MainForm mainForm, bool [] checkState)
        {
            for (int i = 0; i < checkState.Length; i++)
            {
                RunUI(() =>
                {
                    var actualChecked = mainForm.IsConfigEnabled(i);
                    Assert.AreEqual(checkState[i], actualChecked,
                        $"Expected index {i} to have state enabled {checkState[i]} but was {actualChecked}.");
                    //mainForm.AllowUpdate();
                });
            }
        }
    }
}
