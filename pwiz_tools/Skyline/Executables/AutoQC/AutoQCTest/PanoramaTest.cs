using System;
using System.IO;
using System.Threading;
using AutoQC;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using pwiz.PanoramaClient;
using SharedBatch;
using SharedBatchTest;


namespace AutoQCTest
{
    [TestClass]
   public class PanoramaTest: AbstractUnitTest
    {
        public const string PANORAMA_FOLDER_PREFIX = "AutoQcTest";
        private const int WAIT_3SEC = 3000;
        private const int TIMEOUT_80SEC = 80000;

        [TestMethod]
        [TestCategory("Connected")]
        [DeploymentItem(@"..\AutoQC\FileAcquisitionTime.skyr")]
        [DeploymentItem(@"..\AutoQC\SkylineRunner.exe")]
        [DeploymentItem(@"..\AutoQC\SkylineDailyRunner.exe")]
        public void TestPublishToPanorama()
        {
            if (!AllowInternetAccess)
                return;

            var panoramaServerUri = new Uri(TestUtils.PANORAMAWEB);
            var panoramaClient = new WebPanoramaClient(panoramaServerUri, TestUtils.GetPanoramaWebUsername(),
                TestUtils.GetPanoramaWebPassword());
            var testPanoramaFolder = TestUtils.CreatePanoramaWebTestFolder(panoramaClient, TestUtils.PANORAMAWEB_TEST_FOLDER,
                PANORAMA_FOLDER_PREFIX);
            try
            {
                DoTestPublishToPanorama(testPanoramaFolder);
            }
            finally
            {
                TestUtils.DeletePanoramaWebTestFolder(panoramaClient, testPanoramaFolder);
            }
        }

        private void DoTestPublishToPanorama(string testPanoramaFolder)
        {
            var testFilesDir = new TestFilesDir(TestContext, TestUtils.GetTestFilePath("PanoramaPublishTest.zip"));
            var skyFileName = "QEP_2015_0424_RJ.sky";
            var rawFileName = "CE_Vantage_15mTorr_0001_REP1_01.raw";

            Assert.IsTrue(File.Exists(testFilesDir.GetTestPath(skyFileName)),
                "Could not find Skyline file, nothing to import data into.");
            Assert.IsTrue(File.Exists(testFilesDir.GetTestPath(rawFileName)),
                "Could not find data file, nothing to upload.");

            var skylineSettings = TestUtils.GetTestSkylineSettings();
            Assert.IsNotNull(skylineSettings, "Test cannot run without an installation of Skyline or Skyline-daily.");

            var config = new AutoQcConfig("PanoramaTestConfig", false, DateTime.MinValue, DateTime.MinValue,
                TestUtils.GetTestMainSettings(testFilesDir.GetTestPath(skyFileName), "folderToWatch", testFilesDir.FullPath),
                new PanoramaSettings(true, TestUtils.PANORAMAWEB, TestUtils.GetPanoramaWebUsername(),
                    TestUtils.GetPanoramaWebPassword(), testPanoramaFolder),
                skylineSettings);

            // Validate the configuration
            try
            {
                config.Validate(true);
            }
            catch (Exception e)
            {
                Assert.Fail($"Expected configuration to be valid. Validation failed with error '{e.Message}'");
            }

            var runner = new ConfigRunner(config, TestUtils.GetTestLogger(config));
            Assert.IsTrue(runner.CanStart());
            runner.Start();
            Assert.IsTrue(WaitForConfigRunning(runner), $"Expected configuration to be running. Status was {runner.GetStatus()}.");

            var success = SuccessfulPanoramaUpload(testPanoramaFolder);
            Assert.IsTrue(success, "File was not uploaded to panorama.");

            runner.Stop();
        }

        private bool WaitForConfigRunning(ConfigRunner runner)
        {
            var start = DateTime.Now;
            while (!RunnerStatus.Running.Equals(runner.GetStatus()))
            {
                Thread.Sleep(1000);
                if (DateTime.Now > start.AddSeconds(60))
                {
                    return false;
                }
            }

            return true;
        }


        private bool SuccessfulPanoramaUpload(string uniqueFolder)
        {
            var panoramaServerUri = PanoramaUtil.ServerNameToUri(TestUtils.PANORAMAWEB);
            var labKeyQuery = PanoramaUtil.CallNewInterface(panoramaServerUri, "query", $"{uniqueFolder}",
                "selectRows", "schemaName=targetedms&queryName=runs", true);
            var requestHelper =
                new HttpPanoramaRequestHelper(new PanoramaServer(panoramaServerUri, TestUtils.GetPanoramaWebUsername(),
                    TestUtils.GetPanoramaWebPassword()));
            var startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            var x = startTime;
            while (x < startTime + TIMEOUT_80SEC)
            {
                var jsonAsString = requestHelper.DoGet(labKeyQuery);
                var json = JsonConvert.DeserializeObject<PanoramaJsonObject>(jsonAsString);
                if (json.rowCount > 0) return true;
                Thread.Sleep(WAIT_3SEC);
                x = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            }

            return false;
        }
    }
}
