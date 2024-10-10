using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using AutoQC;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using pwiz.PanoramaClient;
using SharedBatch;

namespace AutoQCTest
{
    [TestClass]
    public class PanoramaWebFunctionalTest : AutoQcBaseFunctionalTest
    {
        /// <summary>
        /// This test uploads the QC document to a test folder on PanoramaWeb. The test user (skyline_tester_admin@proteinms.net)
        /// must have access to the folder https://panoramaweb.org/SkylineTest/AutoQcTest/project-begin.view
        /// for this test to run successfully. The test user's password must be saved in the environment variable PANORAMAWEB_PASSWORD
        /// This test does the following:
        /// 1. Create a test subfolder under SkylineTest/AutoQCTest folder on PanoramaWeb.  Set the folder type to "QC".
        /// 2. Create a new AutoQC Loader configuration
        ///    - Provide path to an annotations file
        ///    - check upload to Panorama checkbox
        /// 3. Start configuration
        ///    - Since the template Skyline document already contains a replicate, it should uploaded to PanoramaWeb when the config starts
        ///    - Annotations file should get imported
        ///    - On PanoramaWeb verify:
        ///      1. The document got uploaded
        ///      2. Verify replicate annotation values
        ///    - Verify that AutoQC.log contains log messages in the expected order
        /// 4. Copy a raw file to the raw data folder being monitored by AutoQC Loader
        ///    - Verify the raw file got imported, and document uploaded to PanoramaWeb
        ///    - Verify that AutoQC.log contains log messages in the expected order
        /// 5. Add a line to the annotations file. This line contains replicate annotation values for the newly imported replicate
        ///    - Verify expected replicate annotation values on PanoramaWeb after document is uploaded and imported
        ///    - Verify that AutoQC.log contains log messages in the expected order
        /// 6. Verify that the version of AutoQC Loader is being sent to PanoramaWeb by the PanoramaPinger
        /// 7. Stop the configuration, and edit it to use a Skyline document with a different set of peptides
        ///    This document will fail to import on PanoramaWeb since QC folders expect Skyline documents to have the same set
        ///    of peptides.
        ///    - Verify that the configuration stops automatically in an "Error" state to this error
        ///    - Verify that AutoQC.log contains log messages in the expected order
        /// 8. Delete the test folder on PanoramaWeb
        /// </summary>

        private WebPanoramaClient _panoramaClient;
        private string _panoramaWebTestFolder;
        private string _panoramaWebPassword;

        [TestMethod]
        public void TestPanoramaWebInteraction()
        {
            TestFilesZipPaths = new[] { @"Executables\AutoQC\AutoQCTest\AutoQCTest.zip" };
            Assert.IsNotNull(TestUtils.GetTestSkylineSettings(), "Skyline or Skyline-daily is required to run the test.");

            _panoramaWebPassword = TestUtils.GetPanoramaWebPassword();

            RunFunctionalTest();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Delete the PanoramaWeb test folder
            TestUtils.DeletePanoramaWebTestFolder(_panoramaClient, _panoramaWebTestFolder);
        }

        protected override void DoTest()
        {
            base.DoTest();
            
            var skylineDocPath = Path.Combine(TestFolder, "test-with-results.sky");
            var annotationsFilePath = Path.Combine(TestFolder, "ReplicateAnnotations.csv");

            // Create the directory that AutoQC Loader will monitor for raw files
            var folderToWatch = CreateRawDataDir();

            _panoramaClient = new WebPanoramaClient(new Uri(TestUtils.PANORAMAWEB), TestUtils.PANORAMAWEB_USER, _panoramaWebPassword);

            // Create a test folder on PanoramaWeb
            _panoramaWebTestFolder = TestUtils.CreatePanoramaWebTestFolder(_panoramaClient, TestUtils.PANORAMAWEB_TEST_FOLDER, "Test");
            SetQcFolderType(_panoramaClient, _panoramaWebTestFolder);


            // Create a configuration
            const string configName = "Configuration One";
            var newConfigForm = ShowDialog<AutoQcConfigForm>(MainForm.ClickAdd);
            RunUI(() =>
            {
                WaitForShownForm(newConfigForm);
                newConfigForm.SetConfigName(configName);
                newConfigForm.SetSkylineDocPath(skylineDocPath);
                newConfigForm.SetFolderToWatch(folderToWatch);
                newConfigForm.UncheckRemoveResults(); // Otherwise, imported results older than 30 days will be removed from the document.
                newConfigForm.SetAnnotationsFilePath(annotationsFilePath); // Import annotations.
                newConfigForm.SelectPanoramaTab();
                newConfigForm.CheckUploadToPanorama(); // Enable upload to Panorama
                newConfigForm.SetPanoramaServer(TestUtils.PANORAMAWEB);
                newConfigForm.SetPanoramaUser(TestUtils.PANORAMAWEB_USER);
                newConfigForm.SetPanoramaPassword(_panoramaWebPassword);
                newConfigForm.SetPanoramaFolder(_panoramaWebTestFolder);
                newConfigForm.ClickSave();
            });
            WaitForClosedForm(newConfigForm);
            Assert.AreEqual(1, MainForm.ConfigCount()); // One saved configuration

            // Start the configuration
            var configIndex = StartConfig(MainForm, configName);

            // Skyline document has replicates. It should be uploaded to PanoramaWeb after config starts.
            WaitForConfigRunnerWaiting(MainForm, configIndex);
            // Verify expected status on PanoramaWeb. One row each in targetedms.Runs and pipeline.Job.
            Assert.AreEqual(1, GetJobCountInFolder(_panoramaClient, _panoramaWebTestFolder),
                $"Unexpected number of pipeline jobs in the PanoramaWeb folder {_panoramaWebTestFolder}");
            Assert.AreEqual(1, GetRunCountInFolder(_panoramaClient, _panoramaWebTestFolder),
                $"Unexpected number of TargetedMS runs in the PanoramaWeb folder {_panoramaWebTestFolder}");
           

            // Verify replicate annotations on PanoramaWeb
            // Replicate:/CE_Vantage_15mTorr_0001_REP1_01,30000,4000
            var precursorCounts = new Dictionary<string, int> { { "CE_Vantage_15mTorr_0001_REP1_01", 30000 } };
            var proteinCounts = new Dictionary<string, int> { { "CE_Vantage_15mTorr_0001_REP1_01", 4000 } };
            VerifyReplicateAnnotationsOnPanoramaWeb(_panoramaClient, _panoramaWebTestFolder, 2, precursorCounts, proteinCounts);


            var logFilePath = MainForm.GetLogFilePath(configIndex);

            var logText = File.ReadAllText(logFilePath);
            TestUtils.AssertTextsInThisOrder(logText,
                "Running configuration",
                "Importing existing files",
                "No existing files found",
                "Importing annotations file",
                "Annotations file was imported",
                "Uploading Skyline document to Panorama",
                "Running SkylineRunner.exe with args",
                "Importing new files",
                "Waiting for files");

            // Move a raw file to the raw files folder, wait for it to be imported and uploaded to PanoramaWeb.
            const string rawFile = "CE_Vantage_15mTorr_0001_REP1_02.raw";
            var rawFilePath = Path.Combine(TestFolder, rawFile);
            var rawFileTestPath = Path.Combine(folderToWatch, rawFile);
            File.Copy(rawFilePath, rawFileTestPath);
            Assert.IsTrue(File.Exists(rawFileTestPath));
            Thread.Sleep(ConfigRunner.WAIT_FOR_NEW_FILE); // Pause for the file to be detected

            // Wait for the file to be imported into the document, and document uploaded to PanoramaWeb
            WaitForConfigRunnerWaiting(MainForm, configIndex);
            // Verify expected status on PanoramaWeb. Two rows in pipeline.Job. 
            // We expect to see only one row in the targetedms.Runs table since the previous run becomes redundant and is deleted.
            Assert.AreEqual(2, GetJobCountInFolder(_panoramaClient, _panoramaWebTestFolder),
                $"Unexpected number of pipeline jobs in the PanoramaWeb folder {_panoramaWebTestFolder}");
            Assert.AreEqual(1, GetRunCountInFolder(_panoramaClient, _panoramaWebTestFolder),
                $"Unexpected number of TargetedMS runs in the PanoramaWeb folder {_panoramaWebTestFolder}");

            logText = File.ReadAllText(logFilePath);
            TestUtils.AssertTextsInThisOrder(logText,
                "Importing new files",
                rawFile + " added to directory",
                "Running SkylineRunner.exe with args",
                "--import-file=\"" + rawFileTestPath + "\"",
                "Uploading Skyline document to Panorama",
                "Waiting for files");

            // Update the annotations file
            WriteToAnnotationsFile(annotationsFilePath, rawFile);
            // Wait for the updated annotations file to be imported
            WaitForAnnotationsFileImported(MainForm, configIndex);

            // Verify expected status on PanoramaWeb. Three rows in pipeline.Job. 
            // We expect to see only one row in the targetedms.Runs table since the previous run becomes redundant and is deleted.
            Assert.AreEqual(3, GetJobCountInFolder(_panoramaClient, _panoramaWebTestFolder),
                $"Unexpected number of pipeline jobs in the PanoramaWeb folder {_panoramaWebTestFolder}");
            Assert.AreEqual(1, GetRunCountInFolder(_panoramaClient, _panoramaWebTestFolder),
                $"Unexpected number of TargetedMS runs in the PanoramaWeb folder {_panoramaWebTestFolder}");

            // Verify replicate annotations on PanoramaWeb
            // Replicate:/CE_Vantage_15mTorr_0001_REP1_01,30000,4000
            // Replicate:/CE_Vantage_15mTorr_0001_REP1_02,50000,6000
            precursorCounts.Add("CE_Vantage_15mTorr_0001_REP1_02", 50000);
            proteinCounts.Add("CE_Vantage_15mTorr_0001_REP1_02", 6000);
            VerifyReplicateAnnotationsOnPanoramaWeb(_panoramaClient, _panoramaWebTestFolder, 4, precursorCounts, proteinCounts);

            // Check the log messages
            logText = File.ReadAllText(logFilePath);
            TestUtils.AssertTextsInThisOrder(logText,
                "Annotations file was updated",
                "Importing annotations file",
                "Annotations file was imported",
                "Uploading Skyline document to Panorama",
                "Waiting for files");

            // Check that the app version was communicated to PanoramaWeb by the Panorama pinger.
            VerifyAutoQcPingPopulated(_panoramaClient, _panoramaWebTestFolder, Program.Version());

            // Stop the configuration
            StopConfig(MainForm, configIndex, configName);

            // Edit the configuration. Switch to a Skyline document that has a different set of targets. 
            // This document will fail to import on Panorama with the error: 
            // "QC folders allow new imports to add or remove peptides, but not completely change the list..."
            var editConfigForm = ShowDialog<AutoQcConfigForm>(MainForm.ClickEdit);
            skylineDocPath = Path.Combine(TestFolder, "test-different-peptides.sky");
            RunUI(() =>
            {
                WaitForShownForm(editConfigForm);
                editConfigForm.SetSkylineDocPath(skylineDocPath);
                editConfigForm.SetAnnotationsFilePath(string.Empty);
                editConfigForm.ClickSave();
            });
            WaitForClosedForm(editConfigForm);
            Assert.AreEqual(1, MainForm.ConfigCount());

            // Start the configuration. The document does not have any replicate.  The raw file in the 
            // folder being watched will be imported into the document and it will be uploaded to PanoramaWeb
            // Document import on PanoramaWeb will fail with the error:
            // "QC folders allow new imports to add or remove peptides, but not completely change the list...".
            // The configuration should stop due to this error with an "Error" state.
            configIndex = StartConfig(MainForm, configName);
            WaitForConfigState(MainForm, configIndex, RunnerStatus.Error);
            RunUI(() => Assert.IsFalse(MainForm.IsConfigEnabled(configIndex)));

            // Check the log messages
            logText = File.ReadAllText(logFilePath);
            TestUtils.AssertTextsInThisOrder(logText,
                "Starting configuration",
                $"Skyline file: {skylineDocPath}",
                "Uploading Skyline document to Panorama",
                "An import error occurred on the Panorama server",
                "QC folders allow new imports to add or remove peptides, but not completely change the list",
                "Document could not be imported on Panorama. Stopping configuration",
                "Finished running configuration");
        }

        private void WaitForAnnotationsFileImported(MainForm mainForm, int configIndex)
        {
            WaitForAnnotationsFileUpdated(mainForm, configIndex);
            Thread.Sleep(ConfigRunner.WAIT_FOR_NEW_FILE); // Pause for the file to be detected
            WaitForConfigRunnerWaiting(mainForm, configIndex);
        }

        private void VerifyReplicateAnnotationsOnPanoramaWeb(IPanoramaClient panoramaClient, string folderPath, int expectedRowCount, 
            Dictionary<string, int> expectedPrecursorCount, Dictionary<string, int> expectedProteinCount)
        {
            var rowId = GetLastDocUploadedRowId(panoramaClient, folderPath);

            var json = DoQuery(panoramaClient, folderPath, "targetedms", "ReplicateAnnotation", 
                new[] { "ReplicateId/Name", "Name", "Value" }, null, $"&query.ReplicateId%2FRunId%2FId~eq={rowId}");

            var rowCount = json["rowCount"].ToObject<int>();
            Assert.AreEqual(expectedRowCount, rowCount, "Unexpected ReplicateAnnotation row count");

            var precursorCount = new Dictionary<string, int>();
            var proteinCount = new Dictionary<string, int>();
            var rows = JArray.Parse(json["rows"].ToString());
            foreach (var jObject in rows.Children<JObject>())
            {
                var replicate = GetProperty(jObject, "ReplicateId/Name").ToObject<string>();
                var annotationName = GetProperty(jObject, "Name").ToObject<string>();
                var value = GetProperty(jObject, "Value").ToObject<int>();
                switch (annotationName)
                {
                    case "PrecursorCount":
                        precursorCount.Add(replicate, value);
                        break;
                    case "ProteinCount":
                        proteinCount.Add(replicate, value);
                        break;
                }
            }

            Assert.AreEqual(expectedPrecursorCount.Count, precursorCount.Count, "Unexpected row count for PrecursorCount annotation");
            Assert.AreEqual(expectedProteinCount.Count, proteinCount.Count, "Unexpected row count for ProteinCount annotation");

            foreach (var replicateName in expectedPrecursorCount.Keys)
            {
                Assert.IsTrue(precursorCount.TryGetValue(replicateName, out var foundValue));
                expectedPrecursorCount.TryGetValue(replicateName, out var expectedValue);
                Assert.AreEqual(expectedValue, foundValue, $"Unexpected value for PrecursorCount annotation for replicate {replicateName}");
            }

            foreach (var replicateName in expectedProteinCount.Keys)
            {
                Assert.IsTrue(proteinCount.TryGetValue(replicateName, out var foundValue));
                expectedProteinCount.TryGetValue(replicateName, out var expectedValue);
                Assert.AreEqual(expectedValue, foundValue,
                    $"Unexpected value for ProteinCount annotation for replicate {replicateName}");
            }
        }

        private static JToken GetProperty(JObject jObject, string property)
        {
            var value = jObject[property];
            Assert.IsNotNull(value, $"Expected to find property {property} in json {jObject}.");
            return value;
        }

        private int GetLastDocUploadedRowId(IPanoramaClient panoramaClient, string folderPath)
        {
            var json = DoQuery(panoramaClient, folderPath, "targetedms", "Runs", new[] { "Id" }, new[] { "-Id" });
            var rows = JArray.Parse(json["rows"].ToString());
            Assert.IsTrue(rows.Count > 0, "Expected more than one row in the Runs table");
            var runId = rows[0]["Id"]; // First row has the database Id of the last imported Skyline document
            Assert.IsNotNull(runId, $"Did not find Id property in json {json}");
            return runId.ToObject<int>();
        }

        private void WriteToAnnotationsFile(string annotationsFilePath, string rawFile)
        {
            var replicateName = Path.GetFileNameWithoutExtension(rawFile);
            // Annotations file has two replicate annotations - PrecursorCount and ProteinCount
            using (var writer = File.AppendText(annotationsFilePath))
            {
                writer.WriteLine($"Replicate:/{replicateName},50000,6000");
            }
        }

        private void VerifyAutoQcPingPopulated(IPanoramaClient panoramaClient, string folderPath, string version)
        {
            var json = DoQuery(panoramaClient, folderPath, "targetedms", "AutoQCPing", null, null);
            var rowCount = json["rowCount"].ToObject<int>();
            Assert.AreEqual(1, rowCount, "Unexpected AutoQCPing row count");
            var softwareVersion = json["rows"].First?["SoftwareVersion"].ToString();
            Assert.AreEqual(version, softwareVersion, "Unexpected software version in AutoQCPing table");
        }

        public int GetRunCountInFolder(IPanoramaClient panoramaClient, string folderPath)
        {
            var json = DoQuery(panoramaClient, folderPath, "targetedms", "Runs", null, null);
            var rowCount = json["rowCount"].ToObject<int>();
            return rowCount;
        }

        public int GetJobCountInFolder(IPanoramaClient panoramaClient, string folderPath)
        {
            var json = DoQuery(panoramaClient, folderPath, "pipeline", "Job", null, null);
            var rowCount = json["rowCount"].ToObject<int>();
            return rowCount;
        }

        private JObject DoQuery(IPanoramaClient panoramaClient, string folderPath, string schemaName, string queryName,
            string[] columns, string[] sort, string filter = null)
        {
            var requestUri = GetQueryUri(panoramaClient, folderPath, schemaName, queryName, columns, sort, filter);
            using (var requestHelper = panoramaClient.GetRequestHelper())
            {
                return requestHelper.Get(requestUri,
                    $"Unable to query the {schemaName}.{queryName} table in folder '{folderPath}'. Request was {requestUri}");
            }
        }

        private Uri GetQueryUri(IPanoramaClient panoramaClient, string folderPath, string schema, string query, string[] columns, string[] sort,
            string filter = null)
        {
            var columnStr = columns == null || columns.Length == 0
                ? string.Empty
                : "&query.columns=" + Uri.EscapeDataString(string.Join(",", columns));
            var sortStr = sort == null || sort.Length == 0
                ? string.Empty
                : "&query.sort=" + Uri.EscapeDataString(string.Join(",", sort));
            var filterStr = filter ?? string.Empty;

            return PanoramaUtil.CallNewInterface(panoramaClient.ServerUri, "query", folderPath,
                "selectRows", $"schemaName={schema}&query.queryName={query}{columnStr}{sortStr}{filterStr}", true);
        }

        public void SetQcFolderType(WebPanoramaClient panoramaClient, string panoramaFolder)
        {
            var uri = PanoramaUtil.Call(panoramaClient.ServerUri, @"targetedms", panoramaFolder, @"folderSetup", string.Empty);
            var postData = new NameValueCollection
            {
                [@"folderType"] = @"QC"
            };

            using (var requestHelper = panoramaClient.GetRequestHelper())
            {
                try
                {
                    requestHelper.Post(uri, postData);
                }
                catch (PanoramaServerException e)
                {
                    if (e.Message.StartsWith("Error parsing response as JSON")) return;
                    throw;
                }
            }
        }
    }
}
