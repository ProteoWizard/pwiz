/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Results
{
    /// <summary>
    /// Load a small Waters results file with ion mobility data and check against curated results.
    /// Actually it's an mz5 file of the midsection in a larger Waters file
    /// but it still tests the ion mobility code.
    /// 
    /// </summary>
    [TestClass]
    public class WatersImsMseTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"Test\Results\WatersImsMseTest.zip";



        [TestMethod]
        public void WatersImsMseChromatogramTest()
        {
            for (int loop = 0; loop < 2; loop++)
            {
                var pop = Directory.GetCurrentDirectory();

                var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

                bool withDriftTime = (loop == 0);
                string docPath;
                SrmDocument document = InitWatersImsMseDocument(testFilesDir, withDriftTime ? "single_with_driftinfo.sky" : "single_no_driftinfo.sky", out docPath);
                var docContainer = new ResultsTestDocumentContainer(document, docPath);

                var doc = docContainer.Document;
                var listChromatograms = new List<ChromatogramSet>();
                const string path = @"QC_HDMSE_02_UCA168_3495_082213-timerange21.5to22.5.mz5";
                listChromatograms.Add(AssertResult.FindChromatogramSet(doc, path) ??
                                      new ChromatogramSet(Path.GetFileName(path).Replace('.', '_'), new[] { path }));
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                docContainer.AssertComplete();
                document = docContainer.Document;

                float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                double maxHeight = 0;
                var results = document.Settings.MeasuredResults;
                Assert.AreEqual(1, document.PeptidePrecursorPairs.Count());
                foreach (var pair in document.PeptidePrecursorPairs)
                {
                    ChromatogramGroupInfo[] chromGroupInfo;
                    Assert.IsTrue(results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                        tolerance, true, out chromGroupInfo));
                    Assert.AreEqual(1, chromGroupInfo.Length);
                    var chromGroup = chromGroupInfo[0];
                    Assert.AreEqual(withDriftTime ? 3 : 5, chromGroup.NumPeaks); // This will be higher if we don't filter on DT
                    foreach (var tranInfo in chromGroup.TransitionPointSets)
                    {
                        maxHeight = Math.Max(maxHeight, tranInfo.MaxIntensity);
                    }
                }
                Assert.AreEqual(withDriftTime? 5226 : 20075 , maxHeight, 1);  // Without DT filtering, this will be much greater

                // now drill down for specific values
                int nPeptides = 0;
                foreach (var nodePep in document.Peptides.Where(nodePep => nodePep.Results[0] != null))
                {
                    // expecting just one peptide result in this small data set
                    if (nodePep.Results[0].Sum(chromInfo => chromInfo.PeakCountRatio > 0 ? 1 : 0) > 0)
                    {
                        Assert.AreEqual(21.94865, (double)nodePep.GetMeasuredRetentionTime(0), .0001);
                        Assert.AreEqual(1.0, (double)nodePep.GetPeakCountRatio(0), 0.0001);
                        nPeptides++;
                    }
                }
                Assert.AreEqual(1, nPeptides);

                // Release file handles
                Directory.SetCurrentDirectory(pop);
                docContainer.Release();
                testFilesDir.Dispose();
                
            }
        }

        private static SrmDocument InitWatersImsMseDocument(TestFilesDir testFilesDir, string skyFile, out string docPath)
        {
            docPath = testFilesDir.GetTestPath(skyFile);
            Directory.SetCurrentDirectory(testFilesDir.FullPath);
            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(doc, 0, 1, 1, 1, 8); 
            return doc;
        }
    }
}