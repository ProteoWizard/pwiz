/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestConnected
{
    /// <summary>
    /// Verifies that waters_connect data can be imported through the command-line interface, resolving
    /// against a Waters_Connect account saved in the settings: single injections named by a friendly path
    /// (waters_connect:&lt;account alias&gt;/Path/To/Injection) or by ids
    /// (waters_connect:sampleSetId=...&amp;injectionId=...), and whole sample sets named by id or path
    /// with --import-all-files and --import-all. Enabled only when the WC_PASSWORD environment variable
    /// is set (see WatersConnectTestUtil).
    /// </summary>
    [TestClass]
    public class WatersConnectCommandLineTest : AbstractUnitTestEx
    {
        private const string ACCOUNT_ALIAS = "wc-test";
        private const string FOLDER_PATH = "Company/Skyline/SmallMolOptimization";

        // Ids the devconnect server assigned to the SmallMolOptimization sample sets and to the
        // Scheduled/ID33140_03a_WAA253_4814_092017 injection
        private const string SCHEDULED_SAMPLE_SET_ID = "ad119a0d-c4b8-4f35-8dcf-c519d56b815b";
        private const string UNSCHEDULED_SAMPLE_SET_ID = "b089b188-2c7d-47d8-a8aa-29f2e8ee811b";
        private const string INJECTION_ID = "23679e9b-6d43-4ccc-a437-e1d28c23a349";

        [TestMethod]
        public void TestWatersConnectCommandLineImport()
        {
            if (!WatersConnectTestUtil.EnableWatersConnectTests)
                return;

            var account = (WatersConnectAccount) WatersConnectTestUtil.GetTestAccount().ChangeAccountAlias(ACCOUNT_ALIAS);

            var savedAccountList = Settings.Default.RemoteAccountList;
            var savedStorage = RemoteUrl.RemoteAccountStorage;
            try
            {
                // Make the aliased account resolvable in headless mode, the same way Program.Main
                // wires it up for the real command-line interface.
                var accountList = new RemoteAccountList();
                accountList.Add(account);
                Settings.Default.RemoteAccountList = accountList;
                RemoteUrl.RemoteAccountStorage = SkylineRemoteAccountServices.INSTANCE;

                TestFilesDir = new TestFilesDir(TestContext, @"TestConnected\RemoteApiFunctionalTest.data");
                string docPath = TestFilesDir.GetTestPath("SmallMolOptimization.sky");

                TestImportFile(docPath);
                TestImportAllFiles(docPath);
            }
            finally
            {
                RemoteUrl.RemoteAccountStorage = savedStorage;
                Settings.Default.RemoteAccountList = savedAccountList;
            }
        }

        /// <summary>
        /// Imports one injection named two ways, by friendly path and by sample set and injection ids,
        /// which must resolve to the same URL. Also checks that an injection id without its sample set
        /// id, or a sample set id without an injection id, is reported as an error.
        /// </summary>
        private static void TestImportFile(string docPath)
        {
            // The friendly path identifies the account by its alias and the injection by its
            // folder/sample-set/injection names; the command-line interface resolves it on the server.
            string friendlyUrl = WatersConnectUrl.UrlPrefix + ACCOUNT_ALIAS + @"/" + FOLDER_PATH +
                                 @"/Scheduled/ID33140_03a_WAA253_4814_092017";
            string idUrl = WatersConnectUrl.UrlPrefix + @"sampleSetId=" + SCHEDULED_SAMPLE_SET_ID +
                           @"&injectionId=" + INJECTION_ID;
            var replicateNames = new[] { "wc_cli", "wc_cli_ids" };

            ImportFile(docPath, friendlyUrl, replicateNames[0]);
            ImportFile(docPath, idUrl, replicateNames[1]);

            // The server cannot find an injection by its id alone, and a sample set id alone names no injection
            foreach (var incompleteIdUrl in new[] { @"injectionId=" + INJECTION_ID, @"sampleSetId=" + SCHEDULED_SAMPLE_SET_ID })
            {
                var output = RunCommand(false,
                    "--in=" + docPath,
                    "--import-file=" + WatersConnectUrl.UrlPrefix + incompleteIdUrl);
                AssertEx.Contains(output,
                    WatersConnectResources.WatersConnectUrl_ResolveInjection_Name_a_waters_connect_injection_by_its_path_or_its_ids_);
            }

            var chromatograms = ResultsUtil.DeserializeDocument(docPath).Settings.MeasuredResults.Chromatograms;
            AssertEx.AreEqual(replicateNames.Length, chromatograms.Count);
            var expectedFilePath = chromatograms[0].MSDataFileInfos.Single().FilePath;
            var injectionUrl = (WatersConnectUrl) expectedFilePath;
            Assert.IsTrue(WatersConnectObject.IdEquals(INJECTION_ID, injectionUrl.InjectionId));
            Assert.IsTrue(WatersConnectObject.IdEquals(SCHEDULED_SAMPLE_SET_ID, injectionUrl.FolderOrSampleSetId));
            for (int i = 0; i < replicateNames.Length; i++)
            {
                AssertEx.AreEqual(replicateNames[i], chromatograms[i].Name);
                AssertEx.AreEqual(expectedFilePath, chromatograms[i].MSDataFileInfos.Single().FilePath);
            }
        }

        private static void ImportFile(string docPath, string url, string replicateName)
        {
            RunCommand(true,
                "--in=" + docPath,
                "--import-file=" + url,
                "--import-replicate-name=" + replicateName,
                "--save");
        }

        /// <summary>
        /// Imports whole sample sets, one replicate per injection, after <see cref="TestImportFile"/>
        /// has imported one injection of the Scheduled sample set. Also checks that a path to a folder,
        /// rather than a sample set, is reported as an error.
        /// </summary>
        private static void TestImportAllFiles(string docPath)
        {
            // By sample set id
            RunCommand(true,
                "--in=" + docPath,
                "--import-all-files=" + WatersConnectUrl.UrlPrefix + @"sampleSetId=" + UNSCHEDULED_SAMPLE_SET_ID,
                "--save");
            var doc = ResultsUtil.DeserializeDocument(docPath);
            VerifySampleSetReplicates(doc, "Unscheduled", UNSCHEDULED_SAMPLE_SET_ID,
                "ID33140_01a_WAA253_4814_092017", "ID33140_02a_WAA253_4814_092017");
            AssertEx.AreEqual(4, doc.Settings.MeasuredResults.Chromatograms.Count);

            // By friendly path, with --import-all. ID33140_03a was already imported by TestImportFile,
            // so it must be skipped, which holds only if listing the sample set produces the same URL
            // as resolving the injection did.
            RunCommand(true,
                "--in=" + docPath,
                "--import-all=" + WatersConnectUrl.UrlPrefix + ACCOUNT_ALIAS + @"/" + FOLDER_PATH + @"/Scheduled",
                "--save");
            doc = ResultsUtil.DeserializeDocument(docPath);
            VerifySampleSetReplicates(doc, "Scheduled", SCHEDULED_SAMPLE_SET_ID,
                "ID33141_03a_WAA253_4814_092017", "ID33142_03a_WAA253_4814_092017");
            AssertEx.AreEqual(6, doc.Settings.MeasuredResults.Chromatograms.Count);

            // A folder is not a sample set
            string folderPath = ACCOUNT_ALIAS + @"/" + FOLDER_PATH;
            var output = RunCommand(false,
                "--in=" + docPath,
                "--import-all-files=" + WatersConnectUrl.UrlPrefix + folderPath);
            AssertEx.Contains(output, string.Format(
                WatersConnectResources.WatersConnectUrl_ListSampleSetInjections_The_waters_connect_path___0___is_not_a_sample_set_,
                folderPath));
        }

        /// <summary>
        /// Checks that each injection of a sample set was imported into a replicate of its own, named after
        /// the injection, with a URL carrying the sample set's full path and id.
        /// </summary>
        private static void VerifySampleSetReplicates(SrmDocument doc, string sampleSetName, string sampleSetId,
            params string[] injectionNames)
        {
            var chromatograms = doc.Settings.MeasuredResults.Chromatograms;
            foreach (var injectionName in injectionNames)
            {
                var chromatogramSet = chromatograms.Single(c => c.Name == injectionName);
                var injectionUrl = (WatersConnectUrl) chromatogramSet.MSDataFileInfos.Single().FilePath;
                Assert.IsNotNull(injectionUrl.InjectionId);
                Assert.IsTrue(WatersConnectObject.IdEquals(sampleSetId, injectionUrl.FolderOrSampleSetId));
                AssertEx.AreEqual(FOLDER_PATH + @"/" + sampleSetName + @"/" + injectionName,
                    string.Join(@"/", injectionUrl.GetPathParts()));
            }
        }
    }
}
