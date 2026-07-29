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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Verifies that everything a <see cref="TransitionChromInfo"/> holds can be read back
    /// out of the chromatogram cache by <see cref="MoleculeResults"/>, at the same flat
    /// positions the document uses. That has to be true before the document can stop holding
    /// those values.
    /// </summary>
    [TestClass]
    public class MoleculeResultsTest : AbstractUnitTest
    {
        // This document has neither a spectral library nor an isotope distribution, and no
        // optimization function, so it does NOT cover the dot products or the optimization
        // step positions. Both sides of those assertions are null here. Covering them needs a
        // second document: BlibDriftTimeTest.zip has a library, FullScan.zip has isotope
        // distributions, and AgilentCEOpt.zip has optimization steps.
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
                docResults = docContainer.Document;

                CheckDocument(docResults);
            }
        }

        /// <summary>
        /// The same checks against a collision energy optimized document, where each
        /// optimization step is a separate chromatogram contributing its own positions.
        /// </summary>
        [TestMethod]
        public void TestMoleculeResultsWithOptimizationSteps()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\Results\AgilentCEOpt.zip");
            string docPath = TestFilesDir.GetTestPath("AgilentCE.sky");
            var doc = ResultsUtil.DeserializeDocument(docPath);
            using (var docContainer = new ResultsTestDocumentContainer(doc, docPath))
            {
                var optRegression = doc.Settings.TransitionSettings.Prediction.CollisionEnergy;
                int optSteps = optRegression.StepCount * 2 + 1;
                var chromSet = new ChromatogramSet(@"Optimize",
                    new[] {new MsDataFilePath(TestFilesDir.GetTestPath("BisMet-1pgul-opt-01.d"))},
                    Annotations.EMPTY, optRegression);
                docContainer.ChangeMeasuredResults(new MeasuredResults(new[] {chromSet}), 1, 1 * optSteps,
                    3 * optSteps);
                var docResults = docContainer.Document;

                // Confirm the document really does have a position per optimization step,
                // otherwise this covers no more than the test above.
                Assert.IsTrue(optSteps > 1);
                foreach (var nodeTran in docResults.MoleculeTransitions)
                {
                    Assert.AreEqual(optSteps, nodeTran.Results[0].Count);
                }

                CheckDocument(docResults);
            }
        }

        /// <summary>
        /// The same checks against a document with a spectral library, so that the dot
        /// products are actually calculated rather than being null on both sides.
        /// </summary>
        [TestMethod]
        public void TestMoleculeResultsWithDotProducts()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\Results\BlibDriftTimeTest.zip");
            string docPath = TestFilesDir.GetTestPath("BlibDriftTimeTest.sky");
            var docOriginal = ResultsUtil.DeserializeDocument(docPath);
            using (var docContainer = new ResultsTestDocumentContainer(docOriginal, docPath))
            {
                var librarySpec = new BiblioSpecLiteSpec(@"drift test",
                    TestFilesDir.GetTestPath("BlibDriftTimeTest.blib"));
                var doc = docContainer.Document.ChangeSettings(docContainer.Document.Settings
                    .ChangePeptideLibraries(lib => lib.ChangeLibrarySpecs(new[] {librarySpec})));
                var chromSets = new[]
                {
                    new ChromatogramSet(@"ID12692_01_UCA168_3727_040714", new[]
                    {
                        new MsDataFilePath(TestFilesDir.GetTestPath(
                            "ID12692_01_UCA168_3727_040714" + ExtensionTestContext.ExtMz5))
                    })
                };
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                Assert.IsTrue(docContainer.SetDocument(docResults, docOriginal, true));
                docContainer.AssertComplete();

                CheckDocument(docContainer.Document, true);
            }
        }

        /// <summary>
        /// <paramref name="requireDotProducts"/> guards against the dot product assertions
        /// passing because both sides are null, which is what happens on a document with no
        /// spectral library. OriginalPeak is guarded unconditionally, because it is derived
        /// rather than copied and every one of these documents has it.
        /// </summary>
        private static void CheckDocument(SrmDocument docResults, bool requireDotProducts = false)
        {
            int positionsChecked = 0;
            int groupsChecked = 0;
            int dotProductsChecked = 0;
            int originalPeaksChecked = 0;
            int singleReplicateChecked = 0;
            int replicateToCheck = Math.Min(1, docResults.Settings.MeasuredResults.Chromatograms.Count - 1);
            foreach (var nodePep in docResults.Peptides)
            {
                var moleculeResults = new MoleculeResults(docResults.Settings, nodePep);

                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    foreach (var nodeTran in nodeGroup.Transitions)
                    {
                        if (!nodeTran.HasResults)
                        {
                            continue;
                        }

                        positionsChecked += CheckTransition(moleculeResults, nodeTran);
                    }

                    groupsChecked += CheckTransitionGroup(moleculeResults, nodeGroup, ref dotProductsChecked, ref originalPeaksChecked);
                }

                singleReplicateChecked +=
                    CheckSingleReplicate(docResults.Settings, nodePep, moleculeResults, replicateToCheck);
            }

            Assert.AreNotEqual(0, positionsChecked);
            Assert.AreNotEqual(0, groupsChecked);
            Assert.AreNotEqual(0, singleReplicateChecked);
            Assert.AreNotEqual(0, originalPeaksChecked);
            if (requireDotProducts)
            {
                Assert.AreNotEqual(0, dotProductsChecked);
            }
        }

        /// <summary>
        /// Reading one replicate has to produce the same positions and peaks for that
        /// replicate as reading all of them.
        /// </summary>
        private static int CheckSingleReplicate(SrmSettings settings, PeptideDocNode nodePep,
            MoleculeResults allReplicates, int replicateIndex)
        {
            var oneReplicate = new MoleculeResults(settings, nodePep) {ReplicateIndex = replicateIndex};
            int checkedCount = 0;
            foreach (var nodeGroup in nodePep.TransitionGroups)
            {
                foreach (var nodeTran in nodeGroup.Transitions)
                {
                    var expectedPositions = allReplicates.GetPositions(nodeTran, replicateIndex).ToList();
                    var actualPositions = oneReplicate.GetPositions(nodeTran, replicateIndex).ToList();
                    Assert.AreEqual(expectedPositions.Count, actualPositions.Count);
                    for (int i = 0; i < expectedPositions.Count; i++)
                    {
                        Assert.AreEqual(allReplicates.GetPeak(nodeTran, expectedPositions[i], 0),
                            oneReplicate.GetPeak(nodeTran, actualPositions[i], 0));
                        checkedCount++;
                    }
                }
            }

            return checkedCount;
        }

        /// <summary>
        /// Checks that driving the aggregation from MoleculeResults reproduces the group level
        /// values the document holds. The ranks and the dot products are not compared, because
        /// they come from the ranking pass which MoleculeResults does not drive yet.
        /// </summary>
        private static int CheckTransitionGroup(MoleculeResults moleculeResults,
            TransitionGroupDocNode nodeGroup, ref int dotProductsChecked, ref int originalPeaksChecked)
        {
            if (!nodeGroup.HasResults)
            {
                return 0;
            }

            int groupsChecked = 0;
            for (int replicateIndex = 0; replicateIndex < nodeGroup.Results.Count; replicateIndex++)
            {
                var expected = nodeGroup.Results[replicateIndex];
                if (expected.IsEmpty)
                {
                    continue;
                }

                var rebuilt = moleculeResults.MakeTransitionGroupChromInfos(nodeGroup, replicateIndex, expected,
                    nodeTran => RebuildTransitionChromInfos(moleculeResults, nodeTran, replicateIndex));
                Assert.IsNotNull(rebuilt.ChromInfos);
                Assert.AreEqual(expected.Count, rebuilt.ChromInfos.Count);
                for (int i = 0; i < rebuilt.ChromInfos.Count; i++)
                {
                    if (AssertGroupValuesEqual(expected[i], rebuilt.ChromInfos[i]))
                    {
                        dotProductsChecked++;
                    }

                    if (expected[i].OriginalPeak != null)
                    {
                        originalPeaksChecked++;
                    }

                    groupsChecked++;
                }

                // The ranks are assigned while reading, so the rebuilt transition chrom
                // infos should now match the document's completely.
                foreach (var nodeTran in nodeGroup.Transitions)
                {
                    if (!nodeTran.HasResults || replicateIndex >= nodeTran.Results.Count)
                    {
                        continue;
                    }

                    var expectedChromInfos = nodeTran.Results[replicateIndex];
                    var rebuiltChromInfos = rebuilt.GetTransitionChromInfos(nodeTran);
                    Assert.AreEqual(expectedChromInfos.Count, rebuiltChromInfos.Count);
                    for (int i = 0; i < rebuiltChromInfos.Count; i++)
                    {
                        Assert.AreEqual(expectedChromInfos[i].Rank, rebuiltChromInfos[i].Rank);
                        Assert.AreEqual(expectedChromInfos[i].RankByLevel, rebuiltChromInfos[i].RankByLevel);
                        Assert.AreEqual(expectedChromInfos[i], rebuiltChromInfos[i]);
                    }
                }
            }

            return groupsChecked;
        }

        /// <summary>
        /// The transition chrom infos for one replicate, rebuilt entirely from what MoleculeResults
        /// read back out of the .skyd.
        /// </summary>
        private static IList<TransitionChromInfo> RebuildTransitionChromInfos(MoleculeResults moleculeResults,
            TransitionDocNode nodeTran, int replicateIndex)
        {
            if (!nodeTran.HasResults || replicateIndex >= nodeTran.Results.Count)
            {
                return null;
            }

            var documentChromInfos = nodeTran.Results[replicateIndex];
            var result = new List<TransitionChromInfo>();
            int i = 0;
            foreach (int position in moleculeResults.GetPositions(nodeTran, replicateIndex))
            {
                var chromInfo = documentChromInfos[i++];
                int candidatePeakIndex = moleculeResults.FindCandidatePeakIndex(nodeTran, position, chromInfo);
                result.Add(moleculeResults.MakeTransitionChromInfo(nodeTran, position, candidatePeakIndex,
                    chromInfo.UserSet, chromInfo.Annotations));
            }

            return result;
        }

        /// <summary>
        /// The columnar form the document now carries alongside its chrom infos has to agree
        /// with them position for position.
        /// </summary>
        private static void CheckAbbreviatedResults(TransitionDocNode nodeTran,
            IList<TransitionChromInfo> documentChromInfos, IList<int> countsPerReplicate)
        {
            var abbreviated = nodeTran.AbbreviatedResults;
            Assert.IsNotNull(abbreviated);
            Assert.AreEqual(documentChromInfos.Count, abbreviated.Areas.Count);
            Assert.AreEqual(ReplicatePositions.FromCounts(countsPerReplicate),
                abbreviated.ChromFileIds.ReplicatePositions);
            for (int position = 0; position < documentChromInfos.Count; position++)
            {
                var chromInfo = documentChromInfos[position];
                Assert.AreEqual(chromInfo.Area, abbreviated.Areas[position]);
                Assert.AreEqual(chromInfo.UserSet, abbreviated.GetUserSet(position));
                Assert.AreSame(chromInfo.FileId, abbreviated.ChromFileIds.FileIds[position].Value);
            }

            // Replacing the results has to discard the derived form, including on the copy
            // that ImClone makes, which would otherwise keep the one it was cloned with.
            var cleared = (TransitionDocNode) nodeTran.ChangeResults(null);
            Assert.IsNull(cleared.AbbreviatedResults);
        }

        /// <summary>
        /// Returns whether there was a library dot product to compare, so that the caller can
        /// tell an agreeing comparison from one where both sides were null.
        /// </summary>
        private static bool AssertGroupValuesEqual(TransitionGroupChromInfo expected,
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
            return expected.LibraryDotProduct.HasValue || expected.IsotopeDotProduct.HasValue;
        }

        /// <summary>
        /// Walks the document's results as one flat sequence of positions and checks that the
        /// same positions hold the same values.
        /// </summary>
        private static int CheckTransition(MoleculeResults moleculeResults, TransitionDocNode nodeTran)
        {
            var documentChromInfos = new List<TransitionChromInfo>();
            var countsPerReplicate = new List<int>();
            foreach (var chromInfoList in nodeTran.Results)
            {
                countsPerReplicate.Add(chromInfoList.Count);
                documentChromInfos.AddRange(chromInfoList);
            }

            CheckAbbreviatedResults(nodeTran, documentChromInfos, countsPerReplicate);

            var transitionPeaks = moleculeResults.GetTransitionPeaks(nodeTran);
            Assert.IsNotNull(transitionPeaks);
            Assert.AreEqual(documentChromInfos.Count, transitionPeaks.PositionCount);

            // MoleculeResults has to agree with the document about which replicate each position
            // belongs to, not merely about how many positions there are.
            Assert.AreEqual(ReplicatePositions.FromCounts(countsPerReplicate),
                transitionPeaks.ChromFileIds.ReplicatePositions);

            int positionsChecked = 0;
            for (int position = 0; position < documentChromInfos.Count; position++)
            {
                var chromInfo = documentChromInfos[position];
                Assert.AreSame(chromInfo.FileId, transitionPeaks.ChromFileIds.FileIds[position].Value);
                Assert.AreEqual(chromInfo.OptimizationStep, transitionPeaks.OptimizationSteps[position]);
                if (chromInfo.IsEmpty)
                {
                    continue;
                }

                int candidatePeakIndex = moleculeResults.FindCandidatePeakIndex(nodeTran, position, chromInfo);
                Assert.AreNotEqual(-1, candidatePeakIndex);

                var rebuilt = moleculeResults.MakeTransitionChromInfo(nodeTran, position, candidatePeakIndex,
                    chromInfo.UserSet, chromInfo.Annotations);
                Assert.IsNotNull(rebuilt);
                Assert.AreNotSame(chromInfo, rebuilt);

                // Rank is not peak data and is not something MoleculeResults can know, so compare
                // everything else by rebuilding with the ranks the document recorded.
                Assert.AreEqual(chromInfo, rebuilt.ChangeRank(true, chromInfo.Rank, chromInfo.RankByLevel));
                positionsChecked++;
            }

            return positionsChecked;
        }
    }
}
