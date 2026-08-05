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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Saving a document writes the columnar results instead of the chrom infos, so opening it
    /// again has to give back the peaks it was saved with, including the ones whose boundaries the
    /// user set, which are not any of the candidate peaks in the .skyd.
    /// </summary>
    [TestClass]
    public class SaveColumnarResultsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSaveColumnarResults()
        {
            TestFilesZip = @"TestFunctional\RescoreTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_plasma.sky")));
            WaitForDocumentLoaded();

            int peaksMoved = MoveFirstPeaks();
            Assert.AreNotEqual(0, peaksMoved);
            var expectedPeaks = GetPeaks(SkylineWindow.Document);

            string savedPath = TestFilesDir.GetTestPath("Rat_plasma_resaved.sky");
            RunUI(() => SkylineWindow.SaveDocument(savedPath));
            WaitForConditionUI(() => !SkylineWindow.Dirty);

            RunUI(() => SkylineWindow.OpenFile(savedPath));
            WaitForDocumentLoaded();

            // Otherwise nothing would have recalculated results, and the peaks would have survived
            // only because nothing looked at a chromatogram.
            Assert.IsTrue(SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);

            var actualPeaks = GetPeaks(SkylineWindow.Document);
            Assert.AreEqual(expectedPeaks.Count, actualPeaks.Count);
            for (int i = 0; i < expectedPeaks.Count; i++)
            {
                Assert.AreEqual(expectedPeaks[i], actualPeaks[i], @"peak " + i);
            }
        }

        /// <summary>
        /// Narrows the boundaries of the first few peaks, so that they are not any of the candidate
        /// peaks and can only come back by integrating between the boundaries the document keeps.
        /// </summary>
        private int MoveFirstPeaks()
        {
            int peaksMoved = 0;
            RunUI(() =>
            {
                var doc = SkylineWindow.Document;
                var chromatograms = doc.Settings.MeasuredResults.Chromatograms[0];
                var fileInfo = chromatograms.MSDataFileInfos[0];
                foreach (var peptideGroup in doc.MoleculeGroups.Take(2))
                {
                    foreach (var nodePep in peptideGroup.Molecules.Take(2))
                    {
                        foreach (var nodeGroup in nodePep.TransitionGroups)
                        {
                            // Where the peak is now, from the columnar results. EmptyResults holds
                            // nothing: a precursor does not keep its chrom infos any more.
                            var peakBounds = nodeGroup.AbbreviatedResults?.FindPrecursorPeakBounds(0,
                                fileInfo.FileId);
                            if (!peakBounds.HasValue)
                            {
                                continue;
                            }

                            double width = peakBounds.Value.EndTime - peakBounds.Value.StartTime;
                            var identityPath = new IdentityPath(peptideGroup.Id, nodePep.Id, nodeGroup.Id);
                            var docNew = doc.ChangePeak(identityPath, chromatograms.Name, fileInfo.FilePath, null,
                                peakBounds.Value.StartTime + width / 10,
                                peakBounds.Value.EndTime - width / 10, UserSet.TRUE,
                                PeakIdentification.FALSE, false);
                            // A new instance is not enough: what has to change is the peak in the
                            // columnar results, which is the only place a peak lives now.
                            var movedGroup = (TransitionGroupDocNode) docNew.FindNode(identityPath);
                            var movedBounds = movedGroup.AbbreviatedResults?.FindPrecursorPeakBounds(0,
                                fileInfo.FileId);
                            if (!Equals(peakBounds, movedBounds))
                            {
                                doc = docNew;
                                peaksMoved += nodeGroup.TransitionCount;
                            }
                        }
                    }
                }

                SkylineWindow.ModifyDocument("Move peaks", d => doc);
            });

            return peaksMoved;
        }

        /// <summary>
        /// What a transition keeps: the columnar results. Its chrom infos are not stored, so there
        /// is nothing to read there.
        /// </summary>
        private static List<string> GetPeaks(SrmDocument document)
        {
            var peaks = new List<string>();
            foreach (var results in ResultsUtil.EnumerateTransitionResults(document))
            {
                if (!results.HasResults)
                {
                    continue;
                }

                foreach (var file in results.Files)
                {
                    var peak = results.GetPeak(file.Key, file.Value);
                    // The boundaries a transition kept for itself, which is nothing when its peak
                    // was integrated between the same two times as the rest of the precursor's.
                    var peakBounds = results.Results.FindTransitionCustomPeakBounds(results.Transition,
                                         file.Key, file.Value) ??
                                     results.Results.FindPrecursorPeakBounds(file.Key, file.Value);
                    peaks.Add(string.Format(@"{0} {1} {2} {3}", peak.Area, peak.UserSet,
                        peakBounds?.StartTime, peakBounds?.EndTime));
                }
            }

            return peaks;
        }
    }
}
