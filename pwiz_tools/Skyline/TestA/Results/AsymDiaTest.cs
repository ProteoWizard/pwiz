/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA.Results
{
    /// <summary>
    /// Summary description for AsymDiaTest
    /// </summary>
    [TestClass]
    public class AsymDiaTest
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

        private const string ZIP_FILE = @"TestA\Results\AsymDIA.zip";

        [TestMethod]
        public void AsymmetricIsolationTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = testFilesDir.GetTestPath("Asym_DIA.sky");
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, null, 1, 1, 2, 4);
            var fullScanInitial = doc.Settings.TransitionSettings.FullScan;
            Assert.IsTrue(fullScanInitial.IsEnabledMsMs);
            Assert.AreEqual(FullScanPrecursorFilterType.Multiple, fullScanInitial.PrecursorFilterType);
            Assert.AreEqual(25, fullScanInitial.PrecursorFilter);
            AssertEx.Serializable(doc);
            var docContainer = new ResultsTestDocumentContainer(doc, docPath);

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("Asym_DIA_data.mzML");
            var measuredResults = new MeasuredResults(new[] { new ChromatogramSet("Single", new[] { rawPath }) });

            SrmDocument docResults = docContainer.ChangeMeasuredResults(measuredResults, 1, 1, 1, 2, 2);
            TransitionGroupDocNode nodeGroup = docResults.TransitionGroups.First();
            double ratio = nodeGroup.Results[0][0].Ratio ?? 0;
            // The expecte ratio is 1.0, but the symmetric isolation window should produce poor results
            Assert.AreEqual(0.25, ratio, 0.05);

            // Revert to original document, and get rid of results cache
            Assert.IsTrue(docContainer.SetDocument(doc, docResults, false));
            File.Delete(testFilesDir.GetTestPath("Asym_DIA.skyd"));

            // Try again with asymmetric isolation window
            SrmDocument docAsym = doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fullScan =>
                fullScan.ChangePrecursorFilter(fullScan.PrecursorFilterType, 5, 20)));
            AssertEx.Serializable(docAsym);
            Assert.IsTrue(docContainer.SetDocument(docAsym, doc, false));

            SrmDocument docAsymResults = docContainer.ChangeMeasuredResults(measuredResults, 1, 1, 1, 2, 2);
            nodeGroup = docAsymResults.TransitionGroups.First();
            ratio = nodeGroup.Results[0][0].Ratio ?? 0;
            // Asymmetric should be a lot closer to 1.0
            Assert.AreEqual(1.05, ratio, 0.05);
        }
    }
}