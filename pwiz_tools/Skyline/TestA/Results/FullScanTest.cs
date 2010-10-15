/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA.Results
{
    /// <summary>
    /// Summary description for FullScanTest
    /// </summary>
    [TestClass]
    public class FullScanTest
    {
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        #region Additional test attributes

        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //

        #endregion

        private const string ZIP_FILE = @"TestA\Results\FullScan.zip";

        [TestMethod]
        public void FullScanFilterTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath;
            SrmDocument doc = InitFullScanDocument(testFilesDir, out docPath);
            var docContainer = new ResultsTestDocumentContainer(doc, docPath);

            // Thermo RAW files were way too slow
//            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
//                ExtensionTestContext.ExtThermoRaw);
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2.mzML");
            const string replicateName = "Single";
            var measuredResults = new MeasuredResults(new[]
                {new ChromatogramSet(replicateName, new[] {rawPath})});

            var docResults = doc.ChangeMeasuredResults(measuredResults);
            Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
            docContainer.AssertComplete();
            docResults = docContainer.Document;

            AssertResult.IsDocumentResultsState(docResults, replicateName, 3, 3, 0, 21, 0);

            // Refilter allowing multiple precursors per spectrum
            SrmDocument docMulti = doc.ChangeSettings(doc.Settings.ChangeTransitionInstrument(
                inst => inst.ChangePrecursorFilter(FullScanPrecursorFilterType.Multiple, 2)));
            // Release data cache file
            Assert.IsTrue(docContainer.SetDocument(docMulti, docResults));
            // And remove it
            File.Delete(Path.ChangeExtension(docPath, ChromatogramCache.EXT));

            docResults = docMulti.ChangeMeasuredResults(measuredResults);
            Assert.IsTrue(docContainer.SetDocument(docResults, docMulti, true));
            docContainer.AssertComplete();
            docResults = docContainer.Document;

            AssertResult.IsDocumentResultsState(docResults, replicateName, 6, 6, 0, 38, 0);
        }

        private static SrmDocument InitFullScanDocument(TestFilesDir testFilesDir, out string docPath)
        {
            docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");

            SrmDocument doc;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(SrmDocument));
            try
            {
                using (var stream = new FileStream(docPath, FileMode.Open))
                {
                    doc = (SrmDocument)xmlSerializer.Deserialize(stream);
                }
            }
            catch (Exception x)
            {
                Assert.Fail("Exception thrown: " + x.Message);
                // ReSharper disable HeuristicUnreachableCode
                throw;  // Will never happen
                // ReSharper restore HeuristicUnreachableCode
            }

            AssertEx.IsDocumentState(doc, 0, 2, 7, 7, 49);
            return doc;
        }
    }
}