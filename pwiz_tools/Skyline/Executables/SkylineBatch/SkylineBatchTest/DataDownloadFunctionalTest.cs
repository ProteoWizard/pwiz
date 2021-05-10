using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SharedBatchTest;
using SkylineBatch;
using SkylineBatchTest;

namespace SkylineBatchTest
{
    [TestClass]
    public class DataDownloadFunctionalTest : AbstractSkylineBatchFunctionalTest
    {
        public static string CONFIG_FOLDER;
        public static string TEST_FOLDER;

        [TestMethod]
        public void DataDownloadTest()
        {
            TestFilesZipPaths = new[]
                {@"SkylineBatchTest\DataDownloadFunctionalTest.zip", @"SkylineBatchTest\TestConfigurationFiles.zip"};

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

            TestSmallDataDownload(mainForm);

            TestNoSpaceDataDownload(mainForm);

        }

        private bool ConfigStopped(MainForm mainForm, bool expectedAnswer)
        {
            bool worked = false;
            RunUI(() =>
            {
                worked = expectedAnswer == mainForm.ConfigRunning("EmptyTemplate");
            });
            return worked;
        }

        public void TestSmallDataDownload(MainForm mainForm)
        {
            WaitForShownForm(mainForm);
            var dataDirectory = Path.Combine(CONFIG_FOLDER, "emptyData");
            var configFile = Path.Combine(TEST_FOLDER, "DownloadingConfiguration.bcfg");
            
            var longImportDlg = ShowDialog<LongWaitDlg>(() => mainForm.DoImport(configFile));
            WaitForClosedForm(longImportDlg);

            RunUI(() =>
            {
                FunctionalTestUtil.CheckConfigs(1, 0, mainForm);
                mainForm.ClickRun(1);
            });
            var tenSeconds = new TimeSpan(0,0,10);
            FunctionalTestUtil.WaitForCondition(ConfigStopped, mainForm, true, tenSeconds, 200,
                "Config did not start");
            var oneMinute = new TimeSpan(0, 1, 0);
            FunctionalTestUtil.WaitForCondition(ConfigStopped, mainForm, false, oneMinute, 1000,
                "Config ran past timeout");
            Assert.AreEqual(true, File.Exists(Path.Combine(dataDirectory, "nselevse_L120412_003_SW.wiff")));
            Assert.AreEqual(12427264, new FileInfo(Path.Combine(dataDirectory, "nselevse_L120412_003_SW.wiff")).Length);

        }

        public void TestNoSpaceDataDownload(MainForm mainForm)
        {
            WaitForShownForm(mainForm);
            FunctionalTestUtil.ClearConfigs(mainForm);
            var dataDirectory = Path.Combine(CONFIG_FOLDER, "bigData");
            Assert.AreEqual(false, Directory.Exists(dataDirectory));
            var configFile = Path.Combine(TEST_FOLDER, "DownloadingConfigurationBigData.bcfg");

            var longImportDlg = ShowDialog<LongWaitDlg>(() => mainForm.DoImport(configFile));
            WaitForClosedForm(longImportDlg);
            //TODO uncomment this when fix
            //Assert.AreEqual(false, Directory.Exists(dataDirectory));

            RunUI(() =>
            {
                FunctionalTestUtil.CheckConfigs(1, 0, mainForm);
            });
            RunDlg<AlertDlg>(() => mainForm.ClickRun(1),
                dlg =>
                {
                    Assert.AreEqual(SkylineBatch.Properties.Resources.SkylineBatchConfigManager_StartBatchRun_There_is_not_enough_space_on_this_computer_to_download_the_data_for_these_configurations__You_need_an_additional_ + Environment.NewLine + Environment.NewLine +
                                    string.Format(SkylineBatch.Properties.Resources.SkylineBatchConfigManager_StartBatchRun__0__GB_on_the__1__drive, 125, "C:\\") + Environment.NewLine + Environment.NewLine +
                                    SkylineBatch.Properties.Resources.SkylineBatchConfigManager_StartBatchRun_Please_free_up_some_space_to_download_the_data_,
                        dlg.Message);
                    dlg.ClickOk();
                });
            RunUI(() =>
            {
                Assert.AreEqual(false, mainForm.ConfigRunning("EmptyTemplate"));
                Assert.AreEqual(0, mainForm.tabMain.SelectedIndex);
            });
            
        }

    }
}
