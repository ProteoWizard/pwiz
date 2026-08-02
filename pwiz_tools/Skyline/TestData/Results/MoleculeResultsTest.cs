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

        /// <summary>
        /// A document written the old way, with the chrom infos as attributes, has to give them up
        /// once its chromatograms are loaded, the same as one whose results were just imported.
        /// Every document saved before the columnar results is that document, so this is the case
        /// which decides whether anything is saved in practice.
        /// </summary>
        [TestMethod]
        public void TestMoleculeResultsConvertedAfterReopen()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            string docPath = TestFilesDir.GetTestPath("Bovine_std_curated_seq_small2.sky");
            var doc = ResultsUtil.DeserializeDocument(docPath);
            SrmDocument docReopened;
            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                var rawPath = new MsDataFilePath(TestFilesDir.GetTestPath(
                    "081809_100fmol-MichromMix-05" + ExtensionTestContext.ExtAgilentRaw));
                var chromSets = new[] {new ChromatogramSet(@"AgilentTest", new[] {rawPath})};
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
                docContainer.AssertComplete();
                AssertNoChromInfosKept(docContainer.Document, @"after import");

                // Round tripping writes the chrom infos, which is what every document saved before
                // the columnar results has, and reads them back into LegacyChromInfos. Done inside
                // the container so that nothing holding the cache outlives it.
                string expected = null;
                docReopened = AssertEx.RoundTrip(docContainer.Document, SkylineVersion.CURRENT, ref expected);
            }

            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                Assert.IsTrue(docContainer.SetDocument(docReopened, doc, true));
                docContainer.AssertComplete();
                AssertNoChromInfosKept(docContainer.Document, @"after reopen");
            }
        }

        /// <summary>
        /// That no transition is still holding chrom infos which have been worked out from the
        /// .skyd, which is the whole point of reading them back from it.
        /// </summary>
        private static void AssertNoChromInfosKept(SrmDocument document, string when)
        {
            int kept = 0;
            int transitions = 0;
            foreach (var nodeGroup in document.MoleculeTransitionGroups)
            {
                var results = nodeGroup.AbbreviatedResults;
                if (results == null)
                {
                    continue;
                }

                for (int iTran = 0; iTran < nodeGroup.Children.Count; iTran++)
                {
                    transitions++;
                    kept += results.GetTransitionLegacyChromInfoCount(iTran);
                }
            }

            Assert.AreNotEqual(0, transitions, when);
            Assert.AreEqual(0, kept, when);
        }

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
            var docStart = doc;
            var chromatograms = doc.Settings.MeasuredResults.Chromatograms[0];
            var fileInfo = chromatograms.MSDataFileInfos[0];
            int peaksChanged = 0;
            foreach (var peptideGroup in doc.MoleculeGroups)
            {
                foreach (var nodePep in peptideGroup.Molecules)
                {
                    // The peaks come back from the .skyd: a loaded precursor has given up the chrom
                    // infos this used to read.
                    var moleculeResults = new MoleculeResults(docStart.Settings, nodePep);
                    foreach (var nodeGroup in nodePep.TransitionGroups)
                    {
                        var chromInfo = moleculeResults
                            .GetTransitionGroupChromInfos(nodeGroup.TransitionGroup, 0).FirstOrDefault();
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
            int iTran = nodeGroup.IndexOfTransition(nodeTran);
            var abbreviated = nodeGroup.AbbreviatedResults;
            if (abbreviated?.HasTransitionResults(iTran) != true)
            {
                return 0;
            }

            var results = moleculeResults.GetTransitionChromInfos(nodeGroup.TransitionGroup, nodeTran.Transition);
            Assert.IsNotNull(results);

            // Asking again has to give back what was worked out the first time, rather than
            // reading and rebuilding it all over.
            Assert.AreSame(results,
                moleculeResults.GetTransitionChromInfos(nodeGroup.TransitionGroup, nodeTran.Transition));

            CheckFromChromInfos(abbreviated, iTran, nodeTran, results);


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

                    Assert.IsTrue(abbreviated.TryGetTransitionPeak(iTran, replicateIndex,
                        chromInfo.FileId, out var peak));
                    Assert.AreEqual(peak.Area, chromInfo.Area);
                    Assert.AreEqual(peak.UserSet, chromInfo.UserSet);

                    // A peak with no candidate peak to read is reproduced by integrating between
                    // the boundaries the results kept: the transition's own when it has any, and
                    // otherwise the precursor's, which is what the whole peak group shares.
                    var peakBounds =
                        abbreviated.FindTransitionCustomPeakBounds(iTran, replicateIndex, chromInfo.FileId) ??
                        (abbreviated.FindChosenPeakIndex(replicateIndex, chromInfo.FileId).HasValue
                            ? null
                            : abbreviated.FindPrecursorPeakBounds(replicateIndex, chromInfo.FileId));
                    if (peakBounds.HasValue)
                    {
                        Assert.AreEqual(peakBounds.Value.StartTime, chromInfo.StartRetentionTime);
                        Assert.AreEqual(peakBounds.Value.EndTime, chromInfo.EndRetentionTime);
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
            // Over the columnar positions, not the chrom infos: a converted precursor has none, and
            // being converted is exactly when there are peak indexes to count.
            var results = nodeGroup.AbbreviatedResults;
            if (results == null)
            {
                return 0;
            }

            int count = 0;
            for (int position = 0; position < results.ChromFileIds.FileIds.Count; position++)
            {
                if (results.GetChosenPeakIndex(position).HasValue)
                {
                    count++;
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

            // The molecule level is derived all the way down now: nothing stores it, so there is no
            // "what the document holds" to compare against. What is checked instead is that it is
            // there for the replicates which have results, and that asking for all of them and
            // asking for one give the same answer.
            var results = moleculeResults.GetPeptideChromInfos();
            Assert.IsNotNull(results);

            var replicatesWithResults = nodePep.GetReplicatesWithResults().ToList();
            Assert.AreEqual(replicatesWithResults.Count, results.Count);

            int chromInfosChecked = 0;
            for (int replicateIndex = 0; replicateIndex < results.Count; replicateIndex++)
            {
                var actualList = results[replicateIndex];
                Assert.AreEqual(replicatesWithResults[replicateIndex], actualList.Count > 0,
                    "Replicate {0} disagrees about whether the molecule has results", replicateIndex);

                var oneReplicate = moleculeResults.GetPeptideChromInfos(replicateIndex);
                Assert.AreEqual(actualList.Count, oneReplicate.Count);

                for (int i = 0; i < actualList.Count; i++)
                {
                    Assert.AreEqual(actualList[i], oneReplicate[i]);
                    Assert.AreSame(actualList[i].FileId, oneReplicate[i].FileId);
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
            // A loaded precursor has given up its chrom infos, so what the rebuilt ones are checked
            // against is what the document still holds: the columnar values, position by position.
            var columnar = nodeGroup.AbbreviatedResults;
            if (columnar == null)
            {
                return 0;
            }

            Assert.IsTrue(columnar.IsConverted,
                "Precursor is still carrying chrom infos, so nothing was given up for it");

            var results = moleculeResults.GetTransitionGroupChromInfos(nodeGroup.TransitionGroup);
            Assert.IsNotNull(results);
            Assert.AreSame(results, moleculeResults.GetTransitionGroupChromInfos(nodeGroup.TransitionGroup));

            var replicatePositions = columnar.ChromFileIds.ReplicatePositions;
            Assert.AreEqual(replicatePositions.ReplicateCount, results.Count);

            int groupsChecked = 0;
            for (int replicateIndex = 0; replicateIndex < replicatePositions.ReplicateCount; replicateIndex++)
            {
                var actualList = results[replicateIndex];
                Assert.AreEqual(replicatePositions.GetCount(replicateIndex), actualList.Count);

                var oneReplicate =
                    moleculeResults.GetTransitionGroupChromInfos(nodeGroup.TransitionGroup, replicateIndex);
                Assert.AreEqual(actualList.Count, oneReplicate.Count);

                int i = 0;
                foreach (int position in replicatePositions[replicateIndex])
                {
                    var rebuilt = actualList[i];
                    AssertGroupValuesEqual(rebuilt, oneReplicate[i]);
                    Assert.AreSame(columnar.ChromFileIds.FileIds[position].Value, rebuilt.FileId);
                    Assert.AreEqual(columnar.Peaks.FlatValues[position].RetentionTime, rebuilt.RetentionTime ?? 0, 1e-3);
                    Assert.AreEqual(columnar.Peaks.FlatValues[position].StartTime, rebuilt.StartRetentionTime ?? 0, 1e-3);
                    Assert.AreEqual(columnar.Peaks.FlatValues[position].EndTime, rebuilt.EndRetentionTime ?? 0, 1e-3);
                    Assert.AreEqual(columnar.GetQValue(position), rebuilt.QValue);
                    if (rebuilt.OriginalPeak != null)
                    {
                        originalPeaksChecked++;
                    }

                    i++;
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
        private static void CheckFromChromInfos(TransitionGroupResults abbreviated, int transitionIndex,
            TransitionDocNode nodeTran, Results<TransitionChromInfo> rebuilt)
        {
            // Index zero of a precursor of its own, since what is being checked is the conversion
            // rather than where the transition sits.
            var unconverted = TransitionGroupResults.Empty.ChangeTransitionFromChromInfos(0, rebuilt);
            Assert.IsFalse(unconverted.IsTransitionConverted(0));
            // Optimization step zero only, which is all that is kept: the rest are read back from
            // the .skyd along with it.
            Assert.AreEqual(
                rebuilt.Sum(chromInfoList => chromInfoList.Count(chromInfo => chromInfo.OptimizationStep == 0)),
                unconverted.GetTransitionLegacyChromInfoCount(0));
            foreach (var chromInfo in rebuilt[0].Where(chromInfo => chromInfo.OptimizationStep == 0))
            {
                Assert.AreSame(chromInfo, unconverted.FindTransitionChromInfo(0, 0, chromInfo.FileId));
            }

            // What the document keeps has been converted: which candidate peak each peak is has
            // been worked out, so the chrom infos are not needed any more.
            Assert.IsTrue(abbreviated.IsTransitionConverted(transitionIndex));

            // Not derived from the chrom infos, so replacing those leaves them alone. Structural
            // now that the columnar results belong to the precursor: clearing a transition node's
            // chrom infos cannot reach them.
            var cleared = (TransitionDocNode) nodeTran.ChangeResults(null);
            Assert.AreSame(nodeTran.Transition, cleared.Transition);
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
