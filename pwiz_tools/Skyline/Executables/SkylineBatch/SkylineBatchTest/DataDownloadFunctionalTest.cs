using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;

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
            WaitForShownForm(mainForm);
            Assert.IsNotNull(mainForm, "Main program window is not an instance of MainForm.");
            Assert.AreEqual(0, mainForm.ConfigCount());

            TestSmallDataDownload(mainForm);

            TestNoSpaceDataDownload(mainForm);

            TestPanoramaDataDownload(mainForm);

        }

        private bool ConfigRunning(MainForm mainForm, bool expectedAnswer)
        {
            return expectedAnswer == mainForm.ConfigRunning("EmptyTemplate");
        }

        public void TestSmallDataDownload(MainForm mainForm)
        {
            var dataDirectory = Path.Combine(CONFIG_FOLDER, "emptyData");
            var configFile = Path.Combine(TEST_FOLDER, "DownloadingConfigurationFTP.bcfg");
            
            RunUI(() =>
            {
                mainForm.DoImport(configFile);
                FunctionalTestUtil.CheckConfigs(1, 0, mainForm, "Config was not imported!", "Config was imported but invalid");
            });
            var longWaitDialog = ShowDialog<LongWaitDlg>(() => mainForm.ClickRun(1));
            WaitForClosedForm(longWaitDialog);
            var thirtySeconds = new TimeSpan(0,0,30);
            FunctionalTestUtil.WaitForCondition(ConfigRunning, mainForm, true, thirtySeconds, 200,
                "Config did not start");
            var oneMinute = new TimeSpan(0, 1, 0);
            FunctionalTestUtil.WaitForCondition(ConfigRunning, mainForm, false, oneMinute, 1000,
                "Config ran past timeout");
            Assert.AreEqual(true, File.Exists(Path.Combine(dataDirectory, "nselevse_L120412_003_SW.wiff")));
            Assert.AreEqual(12427264, new FileInfo(Path.Combine(dataDirectory, "nselevse_L120412_003_SW.wiff")).Length);

        }

        public void TestNoSpaceDataDownload(MainForm mainForm)
        {
            RunUI(() =>
            {
                mainForm.tabMain.SelectedIndex = 0;
                FunctionalTestUtil.ClearConfigs(mainForm);
            });
            var dataDirectory = Path.Combine(CONFIG_FOLDER, "bigData");
            Assert.AreEqual(false, Directory.Exists(dataDirectory));
            var configFile = Path.Combine(TEST_FOLDER, "DownloadingConfigurationBigData.bcfg");

            RunUI(() => mainForm.DoImport(configFile));
            Assert.AreEqual(false, Directory.Exists(dataDirectory));

            RunUI(() =>
            {
                FunctionalTestUtil.CheckConfigs(1, 0, mainForm, "Config was not imported!", "Config was imported but invalid");
            });
            FileUtil.SimulatedDriveSpace = 100 * FileUtil.ONE_GB;
            var longWaitDialog = ShowDialog<LongWaitDlg>(() => mainForm.ClickRun(1));
            WaitForClosedForm(longWaitDialog);
            var spaceErrorDlg = WaitForOpenForm<AlertDlg>();
            FileUtil.SimulatedDriveSpace = null;
            RunUI(() =>
            {
                Assert.IsTrue(spaceErrorDlg.Message.StartsWith(SkylineBatch.Properties.Resources.SkylineBatchConfigManager_StartBatchRun_There_is_not_enough_space_on_this_computer_to_download_the_data_for_these_configurations__You_need_an_additional_));
                spaceErrorDlg.ClickOk();
                Assert.AreEqual(0, mainForm.tabMain.SelectedIndex);
            });
            Assert.AreEqual(false, mainForm.ConfigRunning("EmptyTemplate"));
        }

        public void TestPanoramaDataDownload(MainForm mainForm)
        {
            var dataDirectory = Path.Combine(CONFIG_FOLDER, "emptyData");
            var configFile = Path.Combine(TEST_FOLDER, "DownloadingConfigurationPanorama.bcfg");

            RunUI(() =>
            {
                mainForm.tabMain.SelectedIndex = 0;
                FunctionalTestUtil.ClearConfigs(mainForm);
                mainForm.DoImport(configFile);
                FunctionalTestUtil.CheckConfigs(1, 0, mainForm, "Config was not imported!", "Config was imported but invalid");
            });
            var longWaitDialog = ShowDialog<LongWaitDlg>(() => mainForm.ClickRun(1));
            WaitForClosedForm(longWaitDialog);
            var tenSeconds = new TimeSpan(0, 0, 10);
            FunctionalTestUtil.WaitForCondition(ConfigRunning, mainForm, true, tenSeconds, 200,
                "Config did not start");
            var oneMinute = new TimeSpan(0, 1, 0);
            FunctionalTestUtil.WaitForCondition(ConfigRunning, mainForm, false, oneMinute, 1000,
                "Config ran past timeout");
            Assert.AreEqual(true, File.Exists(Path.Combine(dataDirectory, "nselevse_L120412_002_SW.wiff")));
            Assert.AreEqual(12427264, new FileInfo(Path.Combine(dataDirectory, "nselevse_L120412_002_SW.wiff")).Length);
        }

    }
}
