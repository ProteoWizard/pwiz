/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Verifies that the values which <see cref="TransitionChromInfo"/> holds can be read
    /// back out of the chromatogram cache by <see cref="PeptidePeakLoader"/>. That has to
    /// be true before those values can stop being held in memory.
    /// </summary>
    [TestClass]
    public class PeptidePeakLoaderTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\AgilentMix.zip";

        [TestMethod]
        public void TestLoadedPeaksMatchTransitionChromInfo()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("Bovine_std_curated_seq_small2.sky");
            var doc = ResultsUtil.DeserializeDocument(docPath);
            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                var chromSets = new[]
                {
                    new ChromatogramSet(@"AgilentTest", new[]
                    {
                        new MsDataFilePath(TestFilesDir.GetTestPath(
                            "081809_100fmol-MichromMix-05" + ExtensionTestContext.ExtAgilentRaw))
                    })
                };
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                docContainer.AssertComplete();
                docResults = docContainer.Document;

                int peaksChecked = 0;
                foreach (var nodePep in docResults.Peptides)
                {
                    var loaded = new PeptidePeakLoader
                    {
                        Settings = docResults.Settings,
                        PeptideDocNode = nodePep
                    }.Load();

                    foreach (var nodeGroup in nodePep.TransitionGroups)
                    {
                        foreach (var nodeTran in nodeGroup.Transitions)
                        {
                            if (!nodeTran.HasResults)
                            {
                                continue;
                            }

                            for (int iReplicate = 0; iReplicate < nodeTran.Results.Count; iReplicate++)
                            {
                                foreach (var chromInfo in nodeTran.Results[iReplicate])
                                {
                                    if (chromInfo.IsEmpty)
                                    {
                                        continue;
                                    }

                                    int peakIndex = loaded.FindPeakIndex(nodeTran, iReplicate, chromInfo);
                                    Assert.AreNotEqual(-1, peakIndex);
                                    var peak = loaded.GetPeak(nodeTran, iReplicate, chromInfo.FileId,
                                        chromInfo.OptimizationStep, peakIndex);
                                    Assert.IsTrue(peak.HasValue);
                                    Assert.AreEqual(chromInfo.RetentionTime, peak.Value.RetentionTime);
                                    Assert.AreEqual(chromInfo.StartRetentionTime, peak.Value.StartTime);
                                    Assert.AreEqual(chromInfo.EndRetentionTime, peak.Value.EndTime);
                                    Assert.AreEqual(chromInfo.Area, peak.Value.Area);
                                    Assert.AreEqual(chromInfo.BackgroundArea, peak.Value.BackgroundArea);
                                    Assert.AreEqual(chromInfo.Height, peak.Value.Height);
                                    Assert.AreEqual(chromInfo.Fwhm, peak.Value.Fwhm);
                                    Assert.AreEqual(chromInfo.MassError, peak.Value.MassError);
                                    peaksChecked++;
                                }
                            }
                        }
                    }
                }

                Assert.AreNotEqual(0, peaksChecked);
            }
        }
    }
}
