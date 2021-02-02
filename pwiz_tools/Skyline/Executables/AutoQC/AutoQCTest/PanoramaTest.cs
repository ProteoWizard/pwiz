using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AutoQC;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace AutoQCTest
{
    [TestClass]
    public class PanoramaTest
    {
        public const string SERVER_URL = "https://panoramaweb.org/";
        public const string PANORAMA_FOLDER_PATH = "SkylineTest";
        public const string PANORAMA_FOLDER_NAME = "AutoQcTest";
        public const string PANORAMA_USER_NAME = "skyline_tester@proteinms.net";
        public const string PANORAMA_PASSWORD = "lclcmsms";
        private const int WAIT_3SEC = 3000;
        private const int TIMEOUT_80SEC = 80000;

        [TestMethod]
        public async Task TestPublishToPanorama()
        {
            File.Copy(TestUtils.GetTestFilePath("QEP_2015_0424_RJ_2015_04\\QEP_2015_0424_RJ.sky"), TestUtils.GetTestFilePath("QEP_2015_0424_RJ.sky"), true);
            File.Copy(TestUtils.GetTestFilePath("QEP_2015_0424_RJ_2015_04\\QEP_2015_0424_RJ.skyd"), TestUtils.GetTestFilePath("QEP_2015_0424_RJ.skyd"), true);

            var panoramaServerUri = new Uri(PanoramaUtil.ServerNameToUrl(SERVER_URL));
            var panoramaClient = new WebPanoramaClient(panoramaServerUri);

            //create folder
            var random = new Random();
            var uniqueFolderName = PANORAMA_FOLDER_NAME + random.Next(1000000000);
            var status = panoramaClient.CreateFolder(PANORAMA_FOLDER_PATH, uniqueFolderName, PANORAMA_USER_NAME, PANORAMA_PASSWORD);
            if (status == FolderOperationStatus.alreadyexists)
            {
                uniqueFolderName += random.Next(1000000000);
                status = panoramaClient.CreateFolder(PANORAMA_FOLDER_PATH, uniqueFolderName, PANORAMA_USER_NAME, PANORAMA_PASSWORD);
            }
            Assert.AreEqual(FolderOperationStatus.OK, status, "Expected folder to be successfully created");

            
            var config = new AutoQcConfig("PanoramaTestConfig", false, DateTime.MinValue, DateTime.MinValue,
                TestUtils.GetTestMainSettings("folderToWatch", "PanoramaTestConfig"),
                new PanoramaSettings(true, SERVER_URL, PANORAMA_USER_NAME, PANORAMA_PASSWORD, $"{PANORAMA_FOLDER_PATH}/{uniqueFolderName}"), 
                TestUtils.GetTestSkylineSettings());
            var runner = new ConfigRunner(config, new AutoQcLogger(config, null));
            Assert.IsTrue(runner.CanStart());
            runner.Start();

            
            // delete folder
            var success = await SuccessfulPanoramaUpload(uniqueFolderName);
            Assert.AreEqual(FolderOperationStatus.OK, panoramaClient.DeleteFolder($"{PANORAMA_FOLDER_PATH}/{uniqueFolderName}/", PANORAMA_USER_NAME, PANORAMA_PASSWORD));

            
            Assert.IsTrue(success, "File was not uploaded to panorama.");
        }

       
        private async Task<bool> SuccessfulPanoramaUpload(string uniqueFolder)
        {
            var panoramaServerUri = new Uri(PanoramaUtil.ServerNameToUrl(SERVER_URL));
            var labKeyQuery = PanoramaUtil.CallNewInterface(panoramaServerUri, "query", $"{PANORAMA_FOLDER_PATH}/{uniqueFolder}",
                "selectRows", "schemaName=targetedms&queryName=runs", true);
            var webClient = new WebPanoramaClient(panoramaServerUri);
            var startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            var x = startTime;
            var notFound = true;
            while (notFound && x < startTime + TIMEOUT_80SEC)
            {
                var jsonAsString = webClient.DownloadString(labKeyQuery, PANORAMA_USER_NAME, PANORAMA_PASSWORD);
                var json = JsonConvert.DeserializeObject<RootObject>(jsonAsString);
                notFound = json.rowCount == 0;
                if (!notFound) return true;
                await Task.Delay(WAIT_3SEC);
                x = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            }

            return false;
        }
    }
}
