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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Verifies that <see cref="MoleculeResults"/> reproduces every result value a document
    /// holds, reading them back out of the chromatogram cache. That has to be true before the
    /// document can stop holding them.
    /// <para>
    /// Deliberately two tests, one per peak selection path, while the design is still moving.
    /// What they do NOT cover: the optimization step positions (AgilentCEOpt.zip), the dot
    /// products (BlibDriftTimeTest.zip for a library, FullScan.zip for isotope distributions),
    /// and reading the chosen peak index out of
    /// <see cref="TransitionGroupResults.ChosenPeakIndexes"/> rather than searching for it,
    /// which nothing populates yet. Each of those was covered by a test of its own and is worth
    /// covering again once this settles.
    /// </para>
    /// </summary>
    [TestClass]
    public class MoleculeResultsTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\AgilentMix.zip";

        [TestMethod]
        public void TestMoleculeResultsMatchTransitionChromInfo()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("Bovine_std_curated_seq_small2.sky");
            var doc = ResultsUtil.DeserializeDocument(docPath);
            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                // Two replicates, so that the flat position layout spans more than one of them
                // and ReplicatePositions is actually exercised.
                var rawPath = new MsDataFilePath(TestFilesDir.GetTestPath(
                    "081809_100fmol-MichromMix-05" + ExtensionTestContext.ExtAgilentRaw));
                var chromSets = new[]
                {
                    new ChromatogramSet(@"AgilentTest", new[] {rawPath}),
                    new ChromatogramSet(@"AgilentTest2", new[] {rawPath})
                };
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                docContainer.AssertComplete();

                CheckDocument(docContainer.Document);
            }
        }

        /// <summary>
        /// A peak whose boundaries the user set is not one of the candidate peaks in the .skyd,
        /// so it can only be reproduced by integrating the chromatogram again. Moving the
        /// boundaries of every peak in one replicate makes that the only path this exercises.
        /// </summary>
        [TestMethod]
        public void TestMoleculeResultsWithUserSetPeakBounds()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("Bovine_std_curated_seq_small2.sky");
            var doc = ResultsUtil.DeserializeDocument(docPath);
            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                var rawPath = new MsDataFilePath(TestFilesDir.GetTestPath(
                    "081809_100fmol-MichromMix-05" + ExtensionTestContext.ExtAgilentRaw));
                var chromSets = new[] {new ChromatogramSet(@"AgilentTest", new[] {rawPath})};
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                docContainer.AssertComplete();

                int peaksMoved = MoveEveryPeak(docContainer);
                Assert.AreNotEqual(0, peaksMoved);

                // Every moved peak has to end up integrated again rather than found among the
                // candidate peaks, otherwise this is the same test as the one above.
                int reintegrated = CheckDocument(docContainer.Document, false);
                Assert.AreEqual(peaksMoved, reintegrated);
            }
        }

        /// <summary>
        /// Narrows the boundaries of every peak in the first replicate, so that none of them is
        /// one of the candidate peaks any more. Returns how many were moved.
        /// </summary>
        private static int MoveEveryPeak(ResultsTestDocumentContainer docContainer)
        {
            var doc = docContainer.Document;
            var chromatograms = doc.Settings.MeasuredResults.Chromatograms[0];
            var fileInfo = chromatograms.MSDataFileInfos[0];
            int peaksChanged = 0;
            foreach (var peptideGroup in doc.MoleculeGroups)
            {
                foreach (var nodePep in peptideGroup.Molecules)
                {
                    foreach (var nodeGroup in nodePep.TransitionGroups)
                    {
                        var chromInfo = nodeGroup.Results[0].FirstOrDefault();
                        if (chromInfo?.StartRetentionTime == null || chromInfo.EndRetentionTime == null)
                        {
                            continue;
                        }

                        // A tenth off each end keeps the peak within the same candidate peak's
                        // span without matching its boundaries.
                        double width = chromInfo.EndRetentionTime.Value - chromInfo.StartRetentionTime.Value;
                        var identityPath = new IdentityPath(peptideGroup.Id, nodePep.Id, nodeGroup.Id);
                        var docNew = doc.ChangePeak(identityPath, chromatograms.Name, fileInfo.FilePath, null,
                            chromInfo.StartRetentionTime.Value + width / 10,
                            chromInfo.EndRetentionTime.Value - width / 10, UserSet.TRUE, PeakIdentification.FALSE,
                            false);
                        if (!ReferenceEquals(docNew, doc))
                        {
                            doc = docNew;
                            peaksChanged += nodeGroup.TransitionCount;
                        }
                    }
                }
            }

            Assert.IsTrue(docContainer.SetDocument(doc, docContainer.Document));
            return peaksChanged;
        }

        /// <summary>
        /// Returns how many peaks had to be integrated again because they were not among the
        /// candidate peaks.
        /// </summary>
        private static int CheckDocument(SrmDocument docResults, bool expectChosenPeakIndexes = true)
        {
            int positionsChecked = 0;
            int groupsChecked = 0;
            int peptidesChecked = 0;
            int originalPeaksChecked = 0;
            int chosenPeakIndexesFound = 0;
            int reintegrated = 0;
            foreach (var nodePep in docResults.Peptides)
            {
                var moleculeResults = new MoleculeResults(docResults.Settings, nodePep);
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    foreach (var nodeTran in nodeGroup.Transitions)
                    {
                        positionsChecked += CheckTransition(moleculeResults, nodeGroup, nodeTran, ref reintegrated);
                    }

                    groupsChecked += CheckTransitionGroup(moleculeResults, nodeGroup, ref originalPeaksChecked);
                    chosenPeakIndexesFound += CountChosenPeakIndexes(nodeGroup);
                }

                peptidesChecked += CheckPeptide(moleculeResults, nodePep);
            }

            Assert.AreNotEqual(0, positionsChecked);
            Assert.AreNotEqual(0, groupsChecked);
            Assert.AreNotEqual(0, peptidesChecked);
            Assert.AreNotEqual(0, originalPeaksChecked);

            // Converting has to have left the chosen peak indexes behind, except where every peak
            // is one the user set, which has no candidate peak to point at.
            if (expectChosenPeakIndexes)
            {
                Assert.AreNotEqual(0, chosenPeakIndexesFound);
            }
            else
            {
                Assert.AreEqual(0, chosenPeakIndexesFound);
            }
            return reintegrated;
        }

        /// <summary>
        /// The chrom infos rebuilt for a transition have to agree with the columnar results the
        /// transition keeps, which is the only thing left to compare them against: the document
        /// does not hold the chrom infos any more.
        /// <para>
        /// Not circular. The areas came from the import, while the rebuilt chrom infos come from
        /// working out which candidate peak in the .skyd each peak is and reading it, or from
        /// integrating between boundaries the user set. Picking the wrong peak shows up here.
        /// </para>
        /// </summary>
        private static int CheckTransition(MoleculeResults moleculeResults, TransitionGroupDocNode nodeGroup,
            TransitionDocNode nodeTran, ref int reintegrated)
        {
            var abbreviated = nodeTran.AbbreviatedResults;
            if (abbreviated == null)
            {
                return 0;
            }

            var results = moleculeResults.GetTransitionResults(nodeGroup.TransitionGroup, nodeTran.Transition);
            Assert.IsNotNull(results);

            // Asking again has to give back what was worked out the first time, rather than
            // reading and rebuilding it all over.
            Assert.AreSame(results,
                moleculeResults.GetTransitionResults(nodeGroup.TransitionGroup, nodeTran.Transition));

            CheckFromChromInfos(nodeTran, results);

            int positionsChecked = 0;
            for (int replicateIndex = 0; replicateIndex < results.Count; replicateIndex++)
            {
                // The same values have to come back whether asked for one replicate at a time
                // or all at once.
                var oneReplicate =
                    moleculeResults.GetTransitionChromInfos(nodeGroup.TransitionGroup, nodeTran.Transition,
                        replicateIndex);
                Assert.AreEqual(results[replicateIndex].Count, oneReplicate.Count);

                for (int i = 0; i < results[replicateIndex].Count; i++)
                {
                    var chromInfo = results[replicateIndex][i];
                    Assert.AreEqual(chromInfo, oneReplicate[i]);
                    if (chromInfo.OptimizationStep != 0)
                    {
                        continue;
                    }

                    int position = abbreviated.IndexOfFile(replicateIndex, chromInfo.FileId);
                    Assert.AreNotEqual(-1, position);
                    Assert.AreEqual(abbreviated.Areas[position], chromInfo.Area);
                    Assert.AreEqual(abbreviated.GetUserSet(position), chromInfo.UserSet);

                    var customPeak = abbreviated.GetCustomPeak(position);
                    if (customPeak?.HasPeakBounds == true)
                    {
                        // Reproduced by integrating between them rather than by finding a
                        // candidate peak, so the boundaries have to be the ones kept.
                        Assert.AreEqual(customPeak.StartTime.Value, chromInfo.StartRetentionTime);
                        Assert.AreEqual(customPeak.EndTime.Value, chromInfo.EndRetentionTime);
                        reintegrated++;
                    }

                    positionsChecked++;
                }
            }

            return positionsChecked;
        }

        /// <summary>
        /// How many of the precursor's files have a chosen peak index, which is what
        /// <see cref="TransitionGroupDocNode.UpdateResults"/> works out from the chromatograms.
        /// </summary>
        private static int CountChosenPeakIndexes(TransitionGroupDocNode nodeGroup)
        {
            if (!nodeGroup.HasResults)
            {
                return 0;
            }

            var results = nodeGroup.AbbreviatedResults;
            int count = 0;
            for (int replicateIndex = 0; replicateIndex < nodeGroup.Results.Count; replicateIndex++)
            {
                foreach (var chromInfo in nodeGroup.Results[replicateIndex])
                {
                    int position = results.IndexOfFile(replicateIndex, chromInfo.FileId);
                    if (position >= 0 && results.GetChosenPeakIndex(position).HasValue)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// The rebuilt molecule results have to equal the document's, which covers the aggregation
        /// from the precursor level values.
        /// </summary>
        private static int CheckPeptide(MoleculeResults moleculeResults, PeptideDocNode nodePep)
        {
            if (!nodePep.HasResults)
            {
                return 0;
            }

            var results = moleculeResults.GetPeptideResults();
            Assert.IsNotNull(results);
            Assert.AreEqual(nodePep.Results.Count, results.Count);

            int chromInfosChecked = 0;
            for (int replicateIndex = 0; replicateIndex < nodePep.Results.Count; replicateIndex++)
            {
                var expectedList = nodePep.Results[replicateIndex];
                var actualList = results[replicateIndex];
                Assert.AreEqual(expectedList.Count, actualList.Count);

                var oneReplicate = moleculeResults.GetPeptideChromInfos(replicateIndex);
                Assert.AreEqual(expectedList.Count, oneReplicate.Count);

                for (int i = 0; i < expectedList.Count; i++)
                {
                    var expected = expectedList[i];
                    Assert.AreSame(expected.FileId, actualList[i].FileId);
                    Assert.AreEqual(expected.PeakCountRatio, actualList[i].PeakCountRatio);
                    Assert.AreEqual(expected.RetentionTime, actualList[i].RetentionTime);
                    Assert.AreEqual(expected.ExcludeFromCalibration, actualList[i].ExcludeFromCalibration);
                    Assert.AreEqual(expected.AnalyteConcentration, actualList[i].AnalyteConcentration);
                    Assert.AreEqual(expected, actualList[i]);
                    Assert.AreEqual(expected, oneReplicate[i]);
                    chromInfosChecked++;
                }
            }

            return chromInfosChecked;
        }

        /// <summary>
        /// The rebuilt precursor results have to equal the document's, which covers the
        /// aggregation, the ranks and the dot products at once.
        /// </summary>
        private static int CheckTransitionGroup(MoleculeResults moleculeResults, TransitionGroupDocNode nodeGroup,
            ref int originalPeaksChecked)
        {
            if (!nodeGroup.HasResults)
            {
                return 0;
            }

            var results = moleculeResults.GetTransitionGroupResults(nodeGroup.TransitionGroup);
            Assert.IsNotNull(results);
            Assert.AreEqual(nodeGroup.Results.Count, results.Count);
            Assert.AreSame(results, moleculeResults.GetTransitionGroupResults(nodeGroup.TransitionGroup));

            int groupsChecked = 0;
            for (int replicateIndex = 0; replicateIndex < nodeGroup.Results.Count; replicateIndex++)
            {
                var expectedList = nodeGroup.Results[replicateIndex];
                var actualList = results[replicateIndex];
                Assert.AreEqual(expectedList.Count, actualList.Count);

                var oneReplicate =
                    moleculeResults.GetTransitionGroupChromInfos(nodeGroup.TransitionGroup, replicateIndex);
                Assert.AreEqual(expectedList.Count, oneReplicate.Count);

                for (int i = 0; i < expectedList.Count; i++)
                {
                    AssertGroupValuesEqual(expectedList[i], actualList[i]);
                    AssertGroupValuesEqual(expectedList[i], oneReplicate[i]);
                    if (expectedList[i].OriginalPeak != null)
                    {
                        originalPeaksChecked++;
                    }

                    groupsChecked++;
                }
            }

            return groupsChecked;
        }

        /// <summary>
        /// Turning a document's chrom infos into columnar results, which is what reading one
        /// written the old way does. Nothing is kept but the columnar results afterwards, so this
        /// works from chrom infos <see cref="MoleculeResults"/> rebuilt rather than from any the
        /// document holds.
        /// </summary>
        private static void CheckFromChromInfos(TransitionDocNode nodeTran,
            Results<TransitionChromInfo> rebuilt)
        {
            var unconverted = TransitionResults.FromChromInfos(rebuilt);
            Assert.IsFalse(unconverted.IsConverted);
            Assert.AreEqual(rebuilt.Sum(chromInfoList => chromInfoList.Count), unconverted.ChromInfos.Count);
            foreach (var chromInfo in rebuilt[0])
            {
                Assert.AreSame(chromInfo, unconverted.FindChromInfo(chromInfo.FileId, chromInfo.OptimizationStep));
            }

            // What the document keeps has been converted: which candidate peak each peak is has
            // been worked out, so the chrom infos are not needed any more.
            Assert.IsTrue(nodeTran.AbbreviatedResults.IsConverted);

            // Not derived from the chrom infos, so replacing those leaves them alone.
            var cleared = (TransitionDocNode) nodeTran.ChangeResults(null);
            Assert.AreSame(nodeTran.AbbreviatedResults, cleared.AbbreviatedResults);
        }

        private static void AssertGroupValuesEqual(TransitionGroupChromInfo expected,
            TransitionGroupChromInfo actual)
        {
            Assert.AreSame(expected.FileId, actual.FileId);
            Assert.AreEqual(expected.OptimizationStep, actual.OptimizationStep);
            Assert.AreEqual(expected.PeakCountRatio, actual.PeakCountRatio);
            Assert.AreEqual(expected.RetentionTime, actual.RetentionTime);
            Assert.AreEqual(expected.StartRetentionTime, actual.StartRetentionTime);
            Assert.AreEqual(expected.EndRetentionTime, actual.EndRetentionTime);
            Assert.AreEqual(expected.Area, actual.Area);
            Assert.AreEqual(expected.BackgroundArea, actual.BackgroundArea);
            Assert.AreEqual(expected.Fwhm, actual.Fwhm);
            Assert.AreEqual(expected.MassError, actual.MassError);
            Assert.AreEqual(expected.Truncated, actual.Truncated);
            Assert.AreEqual(expected.Identified, actual.Identified);
            Assert.AreEqual(expected.UserSet, actual.UserSet);
            Assert.AreEqual(expected.QValue, actual.QValue);
            Assert.AreEqual(expected.ZScore, actual.ZScore);
            Assert.AreEqual(expected.LibraryDotProduct, actual.LibraryDotProduct);
            Assert.AreEqual(expected.IsotopeDotProduct, actual.IsotopeDotProduct);

            // Derived from the chromatogram rather than carried forward, so this checks the
            // derivation and not just that the value was copied.
            Assert.AreEqual(expected.OriginalPeak, actual.OriginalPeak);
        }
    }
}
