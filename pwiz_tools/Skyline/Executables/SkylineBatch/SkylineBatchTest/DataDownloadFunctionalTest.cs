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
            var dataDirectory = Path.Combine(CONFIG_FOLDER, "emptyData");
            Assert.AreEqual(1, Directory.GetFiles(dataDirectory).Length);
                      var configFile = Path.Combine(TEST_FOLDER, "DownloadingConfiguration.bcfg");
            
            RunUI(() =>
            {
                mainForm.DoImport(configFile);
                FunctionalTestUtil.CheckConfigs(1, 0, mainForm);
                mainForm.ClickRun();
            });
            var tenSeconds = new TimeSpan(0,0,10);
            FunctionalTestUtil.WaitForCondition(ConfigStopped, mainForm, true, tenSeconds, 1000,
                "Config did not start");
            var oneMinute = new TimeSpan(0, 1, 0);
            FunctionalTestUtil.WaitForCondition(ConfigStopped, mainForm, false, oneMinute, 1000,
                "Config ran past timeout");
            Assert.AreEqual(true, File.Exists(Path.Combine(dataDirectory, "nselevse_L120412_003_SW.wiff")));
            Assert.AreEqual(12427264, new FileInfo(Path.Combine(dataDirectory, "nselevse_L120412_003_SW.wiff")).Length);

        }

    }
}
