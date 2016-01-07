/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Results
{
    /// <summary>
    /// Test for fix of problem handling chromatograms with identical Q1>Q3 pairs but different RT values
    /// </summary>
    [TestClass]
    public class ExplicitRTTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"Test\Results\ExplicitRTTest.zip";

        [TestMethod]
        public void TestExplicitRT()
        {

            TestSmallMolecules = false;  // Don't need the magic test node

            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            // Verify our handling of two Q1>Q3 transitions with no RT overlap - formerly we just ignored one or the other though both are needed
            // As in https://skyline.gs.washington.edu/labkey/announcements/home/support/thread.view?entityId=924e3c51-7c00-1033-9ff1-da202582a252&_anchor=24723
            doTest(testFilesDir, "lysine.sky", 13.8, new[] { "ESvKprosp_20151120_035", "ESvKprosp_20151120_036" }, .5);

            // Verify our use of explict RT in peak picking where the correct peak isn't the largest 
            // As in https://skyline.gs.washington.edu/labkey/announcements/home/support/thread.view?entityId=273ccc30-8258-1033-9ff1-da202582a252&_anchor=24774
            doTest(testFilesDir, "test_b.sky", 9.1, new[] { "120315_120", "120315_121", "120315_125", "120315_126" }, null);

        }

        private static void doTest(TestFilesDir testFilesDir, string skyFile, double expectedRT, string[] filenames, double? expectedRatio)
        {
            string docPath;
            var document = InitExplicitRTDocument(testFilesDir, skyFile, out docPath);
            var docContainer = new ResultsTestDocumentContainer(document, docPath);

            var doc = docContainer.Document;
            var listChromatograms = new List<ChromatogramSet>();
            foreach (var filename in filenames)
            {
                var path = MsDataFileUri.Parse(filename + ExtensionTestContext.ExtWatersRaw);
                listChromatograms.Add(AssertResult.FindChromatogramSet(doc, path) ??
                                      new ChromatogramSet(path.GetFileName().Replace('.', '_'), new[] { path }));
            }
            var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
            Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
            docContainer.AssertComplete();
            document = docContainer.Document;

            float tolerance = (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var pair in document.MoleculePrecursorPairs)
            {
                ChromatogramGroupInfo[] chromGroupInfo;
                Assert.IsTrue(document.Settings.MeasuredResults.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup, tolerance,
                    true, out chromGroupInfo));
                Assert.IsTrue(document.Settings.MeasuredResults.TryLoadChromatogram(1, pair.NodePep, pair.NodeGroup, tolerance,
                    true, out chromGroupInfo));
            }
            var nResults = 0;
            foreach (var nodePep in document.Molecules)
            {
                foreach (var results in nodePep.Results)
                {
                    foreach (var result in results)
                    {
                        Assert.AreEqual(expectedRT, result.RetentionTime ?? 0, .1); // We should pick peaks based on explicit RT
                        if (expectedRatio.HasValue) // If we didn't, ratios won't be right
                        {
                            Assert.IsNotNull(result.LabelRatios[0].Ratio);
                            Assert.AreEqual(expectedRatio.Value, result.LabelRatios[0].Ratio.Ratio, .1);
                        }
                        nResults++;
                    }
                }
            }
            Assert.AreEqual(filenames.Length*document.MoleculeGroupCount, nResults);

            // Release file handles
            docContainer.Release();
        }

        private static SrmDocument InitExplicitRTDocument(TestFilesDir testFilesDir, string fileName, out string docPath)
        {
            docPath = testFilesDir.GetTestPath(fileName);
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            return doc;
        }
    }
}