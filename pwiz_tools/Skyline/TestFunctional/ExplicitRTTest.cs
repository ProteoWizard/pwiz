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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;
using System.IO;
using pwiz.Skyline.Util;


namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test for fix of problem handling chromatograms with identical Q1>Q3 pairs but different RT values
    /// </summary>
    [TestClass]
    public class ExplicitRTTest : AbstractFunctionalTestEx
    {
        private const string ZIP_FILE = @"TestFunctional\ExplicitRTTest.zip";

        [TestMethod]
        public void TestExplicitRT()
        {
            TestFilesZip = ZIP_FILE;
            RunFunctionalTest();
        }

        protected override void DoTest()
        {

            var testFilesDir = TestFilesDir;

            // Verify our use of explict RT where multiple nodes and multiple chromatograms all have same Q1>Q3
            // This data set has three chromatograms with Q1=150 Q3=150 (one in negative ion mode), and 
            // three transition nodes with that Q1>Q3 but different RTs (one with neg charge)
            // Expected alignment: 
            // function 65/ index 242 : RT 6.95 glutamate
            // function 66/ index 243 : RT 7.95 glutamine (peak found at 8.1)
            // function 197/ index 117 (neg ion mode): RT 6.4 alpha_ketogluterate
            DoSubTest(testFilesDir, "glutes.sky", new[] { 6.95, 8.1, 6.4 } , new[] { "090215_033" }, null,
                new[] { Adduct.M_PLUS_H, Adduct.M_PLUS_H, Adduct.M_MINUS_H });

            // Verify our handling of two Q1>Q3 transitions with no RT overlap - formerly we just ignored one or the other though both are needed
            // As in https://skyline.gs.washington.edu/labkey/announcements/home/support/thread.view?entityId=924e3c51-7c00-1033-9ff1-da202582a252&_anchor=24723
            DoSubTest(testFilesDir, "lysine.sky", new[] { 13.8, 13.8, 13.8, 13.8 }, new[] { "ESvKprosp_20151120_035", "ESvKprosp_20151120_036" }, .5,
                new[] { Adduct.M_PLUS_H, Adduct.FromStringAssumeChargeOnly("[M6C132N15+H]")});

            // Verify our use of explict RT in peak picking where the correct peak isn't the largest 
            // As in https://skyline.gs.washington.edu/labkey/announcements/home/support/thread.view?entityId=273ccc30-8258-1033-9ff1-da202582a252&_anchor=24774
            DoSubTest(testFilesDir, "test_b.sky", new[] { 9.1, 9.1, 9.1, 9.1, 9.1, 9.1, 9.1, 9.1 }, new[] { "120315_120", "120315_121", "120315_125", "120315_126" }, null,
                new[] { Adduct.M_PLUS_H, Adduct.M_PLUS_H });

        }

        private void DoSubTest(TestFilesDir testFilesDir, string skyFile, double[] expectedRTs, string[] filenames, double? expectedRatio,
            Adduct[] expectedAdducts)
        {
            string docPath;
            var doc = InitExplicitRTDocument(testFilesDir, skyFile, out docPath);
            
            // These test files are some of our oldest small molecule docs, let's see if we can roundtrip them
            AssertEx.Serializable(doc, 3, AssertEx.DocumentCloned);

            var listChromatograms = new List<ChromatogramSet>();
            foreach (var filename in filenames)
            {
                var path = MsDataFileUri.Parse(filename + ExtensionTestContext.ExtMzml);
                listChromatograms.Add(AssertResult.FindChromatogramSet(doc, path) ??
                                      new ChromatogramSet(path.GetFileName().Replace('.', '_'), new[] { path }));
            }
            var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
            Assert.IsTrue(SkylineWindow.SetDocument(docResults, doc));
            var document = WaitForDocumentLoaded();
            float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var infos = new List<ChromatogramGroupInfo[]>();
            int adductIndex = 0;
            foreach (var pair in document.MoleculePrecursorPairs)
            {
                if (!pair.NodeGroup.PrecursorAdduct.Equals(expectedAdducts[adductIndex++]))
                {
                    // These test files are some of our oldest small molecule docs, let's see if we can pull adducts from ion formulas
                    Assert.Fail("Expected adduct {0}, read as {1} instead", expectedAdducts[adductIndex - 1], pair.NodeGroup.PrecursorAdduct);
                }
                for (var f = 0; f < filenames.Length; f++)
                {
                    ChromatogramGroupInfo[] chromGroupInfo;
                    Assert.IsTrue(document.Settings.MeasuredResults.TryLoadChromatogram(f, pair.NodePep, pair.NodeGroup, tolerance,
                        true, out chromGroupInfo));
                    infos.Add(chromGroupInfo);
                }
            }

            for (int i = 0; i < infos.Count; i++)
            {
                if (infos[i].Length == 0)
                {
                    Assert.AreEqual(0, expectedRTs[i], "expected to find a peak at " + expectedRTs[i]);
                    continue;
                }
                var chromatogramInfo = infos[i][0].GetTransitionInfo(0);
                var chromPeaks = chromatogramInfo.Peaks.ToList();
                if (expectedRTs[i] == 0) // Not expecting to find a good peak
                {
                    var errmsg = chromatogramInfo.BestPeakIndex >= 0 ? string.Format("Did not expect to find a peak at RT {0} in {1} ", chromPeaks[chromatogramInfo.BestPeakIndex].RetentionTime, skyFile) : string.Empty;
                    Assert.AreEqual(-1, chromatogramInfo.BestPeakIndex, errmsg);
                }
                else if (chromatogramInfo.BestPeakIndex == -1)
                {
                    Assert.Fail("expected to find a peak at {0} in {1}", expectedRTs[i], skyFile);
                }
                else
                {
                    var peakRT = chromPeaks[chromatogramInfo.BestPeakIndex].RetentionTime;
                    Assert.AreEqual(expectedRTs[i], peakRT, 0.1, string.Format("Expected to find a peak at RT {0} in {1}", expectedRTs[i], skyFile));
                }
            }
            if (expectedRatio.HasValue)
            {
                var nResults = 0;
                foreach (var nodePep in document.Molecules)
                {
                    foreach (var results in nodePep.Results)
                    {
                        foreach (var result in results)
                        {
                            Assert.IsNotNull(result.LabelRatios[0].Ratio);
                            Assert.AreEqual(expectedRatio.Value, result.LabelRatios[0].Ratio.Ratio, .1);
                            nResults++;
                        }
                    }
                }
                Assert.AreEqual(filenames.Length * document.MoleculeCount, nResults);
            }
            WaitForClosedAllChromatogramsGraph();

        }

        private SrmDocument InitExplicitRTDocument(TestFilesDir testFilesDir, string fileName, out string docPath)
        {
            docPath = testFilesDir.GetTestPath(fileName);

            var documentFile = TestFilesDir.GetTestPath(docPath);
            WaitForCondition(() => File.Exists(documentFile));
            RunUI(() =>
            {
                SkylineWindow.OpenFile(documentFile+"d"); //Highjacking this existing test to also test our handling of user trying to open a Skyline-related file (like .skyd) instead of actual Skyline file
                SkylineWindow.SaveDocument(documentFile.Replace(".sky", "_updated.sky")); // These are some of our oldest small mol docs, save an updated version for debug ease
            });
            
            OpenDocument(docPath);
            return SkylineWindow.Document;
        }

    }
}