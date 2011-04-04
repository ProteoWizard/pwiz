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
using System.Collections.Generic;
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
            string docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_multi.sky");
            SrmDocument doc = InitFullScanDocument(docPath, 2, 7, 7, 49);
            var docContainer = new ResultsTestDocumentContainer(doc, docPath);

            // Import the first RAW file (or mzML for international)
            string rawPath = testFilesDir.GetTestPath("ah_20101011y_BSA_MS-MS_only_5-2" +
                ExtensionTestContext.ExtThermoRaw);
            var measuredResults = new MeasuredResults(new[]
                {new ChromatogramSet("Single", new[] {rawPath})});

            SrmDocument docResults = ChangeMeasuredResults(docContainer, measuredResults, 3, 3, 21);

            // Refilter allowing multiple precursors per spectrum
            SrmDocument docMulti = doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(
                fs => fs.ChangePrecursorFilter(FullScanPrecursorFilterType.Multiple, 2)));
            AssertEx.Serializable(docMulti, AssertEx.DocumentCloned);
            // Release data cache file
            Assert.IsTrue(docContainer.SetDocument(docMulti, docResults));
            // And remove it
            File.Delete(Path.ChangeExtension(docPath, ChromatogramCache.EXT));

            ChangeMeasuredResults(docContainer, measuredResults, 6, 6, 38);

            // Import full scan Orbi-Velos data
            docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_long_acc_template.sky");
            doc = InitFullScanDocument(docPath, 1, 3, 3, 21);
            Assert.AreEqual(FullScanMassAnalyzerType.orbitrap, doc.Settings.TransitionSettings.FullScan.ProductMassAnalyzer);
            // Make sure saving this type of document works
            AssertEx.Serializable(doc, AssertEx.DocumentCloned);
            Assert.IsTrue(docContainer.SetDocument(doc, docContainer.Document));
            rawPath = testFilesDir.GetTestPath("ah_20101029r_BSA_CID_FT_centroid_3uscan_3" +
                ExtensionTestContext.ExtThermoRaw);
            measuredResults = new MeasuredResults(new[] { new ChromatogramSet("Accurate", new[] { rawPath }) });
            
            ChangeMeasuredResults(docContainer, measuredResults, 3, 3, 21);

            // Import LTQ data with MS1 and MS/MS
            docPath = testFilesDir.GetTestPath("BSA_Protea_label_free_20100323_meth3_test4.sky");
            doc = InitFullScanDocument(docPath, 3, 3, 4, 32);
            Assert.AreEqual(FullScanMassAnalyzerType.none, doc.Settings.TransitionSettings.FullScan.ProductMassAnalyzer);
            Assert.AreEqual(FullScanMassAnalyzerType.none, doc.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer);
            var docBoth = doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs =>
                fs.ChangePrecursorFilter(FullScanPrecursorFilterType.Single, null)
                  .ChangePrecursorResolution(FullScanMassAnalyzerType.qit, TransitionFullScan.DEFAULT_RES_QIT, null)));
            AssertEx.Serializable(docBoth, AssertEx.DocumentCloned);
            Assert.IsTrue(docContainer.SetDocument(docBoth, docContainer.Document));

            string dataPath = testFilesDir.GetTestPath("klc_20100329v_Protea_Peptide_Curve_200fmol_uL_tech1.mzML");
            var listResults = new List<ChromatogramSet>
                                  {
                                      new ChromatogramSet("MS1 and MS/MS", new[] { dataPath }),
                                  };
            measuredResults = new MeasuredResults(listResults.ToArray());

            ChangeMeasuredResults(docContainer, measuredResults, 3, 4, 26);
            // The mzML was filtered for the m/z range 410 to 910.
            foreach (var nodeTran in docContainer.Document.Transitions)
            {
                Assert.IsTrue(nodeTran.HasResults);
                Assert.IsNotNull(nodeTran.Results[0]);
                if (410 > nodeTran.Mz || nodeTran.Mz > 910)
                    Assert.IsTrue(nodeTran.Results[0][0].IsEmpty);
                else
                    Assert.IsFalse(nodeTran.Results[0][0].IsEmpty);
            }

            // Import LTQ data with MS1 and MS/MS using multiple files for a single replicate
            listResults.Add(new ChromatogramSet("Multi-file", new[]
                                                                  {
                                                                      testFilesDir.GetTestPath("both_DRV.mzML"),
                                                                      testFilesDir.GetTestPath("both_KVP.mzML"),
                                                                  }));
            measuredResults = new MeasuredResults(listResults.ToArray());
            ChangeMeasuredResults(docContainer, measuredResults, 2, 3, 25);
            int indexResults = listResults.Count - 1;
            foreach (var nodeTran in docContainer.Document.Transitions)
            {
                Assert.IsTrue(nodeTran.HasResults);
                Assert.AreEqual(listResults.Count, nodeTran.Results.Count);
                var peptide = nodeTran.Transition.Group.Peptide;
                // DRV without FASTA sequence should not have data for non-precursor transitions
                if (!peptide.Sequence.StartsWith("DRV") || !peptide.Begin.HasValue)
                    Assert.IsFalse(nodeTran.Results[indexResults][0].IsEmpty);
                else if (nodeTran.Transition.IonType != IonType.precursor)
                    Assert.IsNull(nodeTran.Results[indexResults]);
                else
                {
                    // Random, bogus peaks chosen in both files
                    Assert.AreEqual(2, nodeTran.Results[indexResults].Count);
                    Assert.IsFalse(nodeTran.Results[indexResults][0].IsEmpty);
                    Assert.IsFalse(nodeTran.Results[indexResults][1].IsEmpty);
                }
            }


            // Import FT data with only MS1
            docPath = testFilesDir.GetTestPath("Yeast_HI3 Peptides_test.sky");
            doc = InitFullScanDocument(docPath, 2, 2, 2, 2);
            Assert.AreEqual(FullScanMassAnalyzerType.none, doc.Settings.TransitionSettings.FullScan.ProductMassAnalyzer);
            Assert.AreEqual(FullScanMassAnalyzerType.none, doc.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer);
            var docMs1 = doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs =>
                fs.ChangePrecursorResolution(FullScanMassAnalyzerType.ft_icr, 50*1000, 400)));
            AssertEx.Serializable(docMs1, AssertEx.DocumentCloned);
            Assert.IsTrue(docContainer.SetDocument(docMs1, docContainer.Document));
            listResults = new List<ChromatogramSet>
                                  {
                                      new ChromatogramSet("rep1", new[] {testFilesDir.GetTestPath("S_2_LVN.mzML")}),
                                  };
            measuredResults = new MeasuredResults(listResults.ToArray());
            ChangeMeasuredResults(docContainer, measuredResults, 1, 1, 1);
            // Because of the way the mzML files were filtered, all of the LVN peaks should be present
            // in the first replicate, and all of the NVN peaks should be present in the other.
            foreach (var nodeTran in docContainer.Document.Transitions)
            {
                Assert.IsTrue(nodeTran.HasResults);
                Assert.AreEqual(1, nodeTran.Results.Count);
                if (nodeTran.Transition.Group.Peptide.Sequence.StartsWith("LVN"))
                    Assert.IsFalse(nodeTran.Results[0][0].IsEmpty);
                else
                    Assert.IsTrue(nodeTran.Results[0][0].IsEmpty);
            }

            listResults.Add(new ChromatogramSet("rep2", new[] {testFilesDir.GetTestPath("S_2_NVN.mzML")}));
            measuredResults = new MeasuredResults(listResults.ToArray());
            ChangeMeasuredResults(docContainer, measuredResults, 1, 1, 1);
            // Because of the way the mzML files were filtered, all of the LVN peaks should be present
            // in the first replicate, and all of the NVN peaks should be present in the other.
            foreach (var nodeTran in docContainer.Document.Transitions)
            {
                Assert.IsTrue(nodeTran.HasResults);
                Assert.AreEqual(2, nodeTran.Results.Count);
                if (nodeTran.Transition.Group.Peptide.Sequence.StartsWith("LVN"))
                    Assert.IsTrue(nodeTran.Results[1][0].IsEmpty);
                else
                    Assert.IsFalse(nodeTran.Results[1][0].IsEmpty);
            }
        }

        private static SrmDocument ChangeMeasuredResults(TestDocumentContainer docContainer, MeasuredResults measuredResults,
            int peptides, int tranGroups, int transitions)
        {
            var doc = docContainer.Document;
            var docResults = doc.ChangeMeasuredResults(measuredResults);
            Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
            docContainer.AssertComplete();
            docResults = docContainer.Document;

            // Check the result state of the most recently added chromatogram set.
            var chroms = measuredResults.Chromatograms;
            AssertResult.IsDocumentResultsState(docResults, chroms[chroms.Count - 1].Name, peptides, tranGroups, 0, transitions, 0);

            return docResults;
        }

        private static SrmDocument InitFullScanDocument(string docPath, int prot, int pep, int prec, int tran)
        {
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
                throw;  // Will never happen
            }

            AssertEx.IsDocumentState(doc, 0, prot, pep, prec, tran);
            return doc;
        }
    }
}