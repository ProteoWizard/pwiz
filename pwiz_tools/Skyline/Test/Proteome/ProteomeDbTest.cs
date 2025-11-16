/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Proteome
{
    /// <summary>
    /// Summary description for ProteomeDbTest
    /// </summary>
    [TestClass]
    public class ProteomeDbTest : AbstractUnitTestEx
    {
        private const string ZIP_FILE = @"Test\Proteome\ProteomeDbTest.zip";
        private const string PROTDB_EXPECTED_JSON_RELATIVE_PATH = @"Test\Proteome\ProteomeDbWebData.json";

        protected override bool IsRecordMode => false;

        private ProteomeDbExpectedData _cachedExpectedData;

        /// <summary>
        /// Tests creating a proteome database, adding a FASTA file to it, and digesting it with trypsin.
        /// </summary>
        [TestMethod]
        public void TestProteomeDb()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string fastaPath = TestFilesDir.GetTestPath("high_ipi.Human.20060111.fasta");
            string protDbPath = TestFilesDir.GetTestPath("test.protdb");

            using (ProteomeDb proteomeDb = ProteomeDb.CreateProteomeDb(protDbPath))
            {
                IProgressStatus status = new ProgressStatus(string.Empty);
                using (var reader = new StreamReader(fastaPath))
                {
                    proteomeDb.AddFastaFile(reader, new SilentProgressMonitor(), ref status, true); // Delay indexing
                }
                // perform digestion
                Digestion digestion = proteomeDb.GetDigestion();
                var digestedProteins0 = digestion.GetProteinsWithSequence("EDGWVK");
                Assert.IsTrue(digestedProteins0.Count >= 1);
            }
        }

        /// <summary>
        /// Tests reading an older-version proteome database, adding a FASTA file to it.
        /// </summary>
        [TestMethod]
        public void TestOlderProteomeDb()
        {
            DoTestOlderProteomeDb(TestContext, false); // don't actually go to the web for protein metadata
        }

        [TestMethod]
        public void TestWebProteomeDb()
        {
            // Only run this if SkylineTester has enabled web access or web responses are being recorded
            if (AllowInternetAccess || IsRecordMode)
            {
                DoTestOlderProteomeDb(TestContext, true); // actually go to the web for protein metadata
            }

            CheckRecordMode();
        }

        /// <summary>
        /// Loads recorded HTTP interactions for playback during offline tests.
        /// Returns empty data if the recording file doesn't exist.
        /// </summary>
        private ProteomeDbExpectedData LoadHttpInteractions()
        {
            if (_cachedExpectedData != null)
                return _cachedExpectedData;

            var jsonPath = TestContext.GetProjectDirectory(PROTDB_EXPECTED_JSON_RELATIVE_PATH);
            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
            {
                _cachedExpectedData = new ProteomeDbExpectedData();
                return _cachedExpectedData;
            }

            var json = File.ReadAllText(jsonPath, Encoding.UTF8);
            _cachedExpectedData = JsonConvert.DeserializeObject<ProteomeDbExpectedData>(json) ?? new ProteomeDbExpectedData();
            _cachedExpectedData.HttpInteractions ??= new List<HttpInteraction>();
            return _cachedExpectedData;
        }

        /// <summary>
        /// Records HTTP interactions for playback during offline tests.
        /// This enables tests to run without network access by replaying recorded request/response pairs.
        /// </summary>
        private void RecordHttpInteractions(IReadOnlyList<HttpInteraction> interactions)
        {
            if (interactions == null || interactions.Count == 0)
                return;

            var jsonPath = TestContext.GetProjectDirectory(PROTDB_EXPECTED_JSON_RELATIVE_PATH);
            Assert.IsNotNull(jsonPath, @"Unable to locate project-relative path for ProteomeDb HTTP interactions.");

            var data = new ProteomeDbExpectedData
            {
                HttpInteractions = interactions.ToList()
            };

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(jsonPath, json, new UTF8Encoding(false));
            Console.Out.WriteLine(@"Recorded ProteomeDb HTTP interactions to: " + jsonPath);
        }

        public void DoTestOlderProteomeDb(TestContext testContext, bool doActualWebAccess)
        {
            TestFilesDir = new TestFilesDir(testContext, ZIP_FILE);

            string fastaPath = TestFilesDir.GetTestPath("tiny.fasta");
            string protDbPath = TestFilesDir.GetTestPath("celegans_mini.protdb"); // a version 0 protdb file
            string blibPath = TestFilesDir.GetTestPath("random.blib"); // a bibliospec file

            // What happens when you try to open a random file as a protdb file?
            AssertEx.ThrowsException<DbException>(() => ProteomeDb.OpenProteomeDb(fastaPath));

            // What happens when you try to open a non-protdb database file as a protdb file?
            AssertEx.ThrowsException<FileLoadException>(() => ProteomeDb.OpenProteomeDb(blibPath));

            var expectedData = LoadHttpInteractions();
            HttpInteractionRecorder recorder = null;
            if (doActualWebAccess && IsRecordMode)
                recorder = new HttpInteractionRecorder();

            using var helper = CreateHttpClientHelper(doActualWebAccess, expectedData, recorder);
            using (ProteomeDb proteomeDb = ProteomeDb.OpenProteomeDb(protDbPath))
            {
                Assert.IsTrue(proteomeDb.GetSchemaVersionMajor() == 0); // the initial db from our zipfile should be ancient
                Assert.IsTrue(proteomeDb.GetSchemaVersionMinor() == 0); // the initial db from our zipfile should be ancient
                Assert.AreEqual(9, proteomeDb.GetProteinCount());

                var protein = proteomeDb.GetProteinByName("Y18D10A.20");
                Assert.IsNotNull(protein);
                Assert.IsTrue(String.IsNullOrEmpty(protein.Accession)); // old db won't have this populated

                if (!doActualWebAccess)
                    Assert.IsNotNull(helper, "No longer support non-web access without recorded data");
                var searcher = new WebEnabledFastaImporter();
                bool searchComplete;
                IProgressStatus status = new ProgressStatus(string.Empty);
                Assert.IsTrue(proteomeDb.LookupProteinMetadata(new SilentProgressMonitor(), ref status, searcher, false, out searchComplete)); // add any missing protein metadata
                Assert.IsTrue(searchComplete);

                protein = proteomeDb.GetProteinByName("Y18D10A.20");
                Assert.IsNotNull(protein);
                Assert.AreEqual("Q9XW16", protein.Accession);

                using (var reader = new StreamReader(fastaPath))
                {
                    proteomeDb.AddFastaFile(reader, new SilentProgressMonitor(), ref status, false);
                }
                // the act of writing should update to the current version
                Assert.AreEqual(ProteomeDb.SCHEMA_VERSION_MAJOR_CURRENT, proteomeDb.GetSchemaVersionMajor());
                Assert.AreEqual(ProteomeDb.SCHEMA_VERSION_MINOR_CURRENT, proteomeDb.GetSchemaVersionMinor());
                Assert.AreEqual(19, proteomeDb.GetProteinCount());

                // check for propery processed protein metadata
                Assert.IsTrue(proteomeDb.LookupProteinMetadata(new SilentProgressMonitor(), ref status, searcher, false, out searchComplete));
                Assert.IsTrue(searchComplete);
                protein = proteomeDb.GetProteinByName("IPI00000044");
                Assert.IsNotNull(protein);
                Assert.AreEqual("P01127", protein.Accession); // We get this offline with our ipi->uniprot mapper
                Assert.AreEqual("PDGFB_HUMAN", protein.PreferredName); // But this we get only with web access
/*
                // TODO(bspratt): fix  "GetDigestion has no notion of a Db that has been added to, doesn't digest the new proteins and returns immediately (issue #304)"
                Enzyme trypsin = EnzymeList.GetDefault();
                proteomeDb.Digest(trypsin,  new SilentProgressMonitor());
                Digestion digestion = proteomeDb.GetDigestion(trypsin.Name);
                var digestedProteins0 = digestion.GetProteinsWithSequencePrefix("EDGWVK", 100);
                Assert.IsTrue(digestedProteins0.Count >= 1);
//*/
            }

            if (IsRecordMode && doActualWebAccess)
            {
                var recordedInteractions = recorder?.Interactions?.ToList();
                RecordHttpInteractions(recordedInteractions);
            }
        }

        /// <summary>
        /// Tests reading an newer-version proteome database, and failing gracefully.
        /// </summary>
        [TestMethod]
        public void TestNewerProteomeDb()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string protDbPath = TestFilesDir.GetTestPath("testv9999.protdb"); // a version 9999(!) protdb file
            try
            {
                using (var db = ProteomeDb.OpenProteomeDb(protDbPath))
                {
                    using (var session = db.OpenSession())
                        session.Close();
                    Assert.Fail("should not be able to open a version 9999 protdb file."); // Not L10N
                }
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }
        }

        private HttpClientTestHelper CreateHttpClientHelper(bool useNetAccess, ProteomeDbExpectedData expectedData, HttpInteractionRecorder recorder)
        {
            if (useNetAccess)
            {
                if (recorder != null)
                    return HttpClientTestHelper.BeginRecording(recorder);
                return null; // use real network access when no recorder is supplied
            }
            // Use playback if we have recorded interactions
            if (expectedData?.HttpInteractions != null && expectedData.HttpInteractions.Count > 0)
            {
                return HttpClientTestHelper.PlaybackFromInteractions(expectedData.HttpInteractions);
            }
            // Fallback to FakeWebSearchProvider if no recorded interactions available (handled in DoTestOlderProteomeDb)
            return null;
        }

        private class ProteomeDbExpectedData
        {
            public List<HttpInteraction> HttpInteractions { get; set; } = new List<HttpInteraction>();
        }
    }
}
