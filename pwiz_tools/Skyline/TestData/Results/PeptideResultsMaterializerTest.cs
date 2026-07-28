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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Verifies that everything a <see cref="TransitionChromInfo"/> holds can be read back
    /// out of the chromatogram cache by <see cref="PeptideResultsMaterializer"/>, at the same flat
    /// positions the document uses. That has to be true before the document can stop holding
    /// those values.
    /// </summary>
    [TestClass]
    public class PeptideResultsMaterializerTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestData\Results\AgilentMix.zip";

        [TestMethod]
        public void TestMaterializedPeaksMatchTransitionChromInfo()
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

                int positionsChecked = 0;
                int groupsChecked = 0;
                foreach (var nodePep in docResults.Peptides)
                {
                    var materialized = new PeptideResultsMaterializer
                    {
                        Settings = docResults.Settings,
                        PeptideDocNode = nodePep
                    }.Materialize();

                    foreach (var nodeGroup in nodePep.TransitionGroups)
                    {
                        foreach (var nodeTran in nodeGroup.Transitions)
                        {
                            if (!nodeTran.HasResults)
                            {
                                continue;
                            }

                            positionsChecked += CheckTransition(materialized, nodeTran);
                        }

                        groupsChecked += CheckTransitionGroup(materialized, nodeGroup);
                    }
                }

                Assert.AreNotEqual(0, positionsChecked);
                Assert.AreNotEqual(0, groupsChecked);
            }
        }

        /// <summary>
        /// Checks that driving the aggregation from the materializer reproduces the group level
        /// values the document holds. The ranks and the dot products are not compared, because
        /// they come from the ranking pass which the materializer does not drive yet.
        /// </summary>
        private static int CheckTransitionGroup(MaterializedPeptideResults materialized, TransitionGroupDocNode nodeGroup)
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

                var rebuilt = materialized.MakeTransitionGroupChromInfos(nodeGroup, replicateIndex, expected,
                    nodeTran => RebuildTransitionChromInfos(materialized, nodeTran, replicateIndex));
                Assert.IsNotNull(rebuilt);
                Assert.AreEqual(expected.Count, rebuilt.Count);
                for (int i = 0; i < rebuilt.Count; i++)
                {
                    AssertGroupValuesEqual(expected[i], rebuilt[i]);
                    groupsChecked++;
                }
            }

            return groupsChecked;
        }

        /// <summary>
        /// The transition chrom infos for one replicate, rebuilt entirely from what the materializer
        /// read back out of the .skyd.
        /// </summary>
        private static IList<TransitionChromInfo> RebuildTransitionChromInfos(MaterializedPeptideResults materialized,
            TransitionDocNode nodeTran, int replicateIndex)
        {
            if (!nodeTran.HasResults || replicateIndex >= nodeTran.Results.Count)
            {
                return null;
            }

            var documentChromInfos = nodeTran.Results[replicateIndex];
            var result = new List<TransitionChromInfo>();
            int i = 0;
            foreach (int position in materialized.GetPositions(nodeTran, replicateIndex))
            {
                var chromInfo = documentChromInfos[i++];
                int candidatePeakIndex = materialized.FindCandidatePeakIndex(nodeTran, position, chromInfo);
                result.Add(materialized.MakeTransitionChromInfo(nodeTran, position, candidatePeakIndex,
                    chromInfo.UserSet, chromInfo.Annotations));
            }

            return result;
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
        }

        /// <summary>
        /// Walks the document's results as one flat sequence of positions and checks that the
        /// materializer produced the same positions holding the same values.
        /// </summary>
        private static int CheckTransition(MaterializedPeptideResults materialized, TransitionDocNode nodeTran)
        {
            var documentChromInfos = new List<TransitionChromInfo>();
            var countsPerReplicate = new List<int>();
            foreach (var chromInfoList in nodeTran.Results)
            {
                countsPerReplicate.Add(chromInfoList.Count);
                documentChromInfos.AddRange(chromInfoList);
            }

            var materializedTransition = materialized.GetTransition(nodeTran);
            Assert.IsNotNull(materializedTransition);
            Assert.AreEqual(documentChromInfos.Count, materializedTransition.PositionCount);

            // The materializer has to agree with the document about which replicate each position
            // belongs to, not merely about how many positions there are.
            Assert.AreEqual(ReplicatePositions.FromCounts(countsPerReplicate),
                materializedTransition.ChromFileIds.ReplicatePositions);

            int positionsChecked = 0;
            for (int position = 0; position < documentChromInfos.Count; position++)
            {
                var chromInfo = documentChromInfos[position];
                Assert.AreSame(chromInfo.FileId, materializedTransition.ChromFileIds.FileIds[position].Value);
                Assert.AreEqual(chromInfo.OptimizationStep, materializedTransition.OptimizationSteps[position]);
                if (chromInfo.IsEmpty)
                {
                    continue;
                }

                int candidatePeakIndex = materialized.FindCandidatePeakIndex(nodeTran, position, chromInfo);
                Assert.AreNotEqual(-1, candidatePeakIndex);

                var rebuilt = materialized.MakeTransitionChromInfo(nodeTran, position, candidatePeakIndex,
                    chromInfo.UserSet, chromInfo.Annotations);
                Assert.IsNotNull(rebuilt);
                Assert.AreNotSame(chromInfo, rebuilt);

                // Rank is not peak data and is not something the materializer can know, so compare
                // everything else by rebuilding with the ranks the document recorded.
                Assert.AreEqual(chromInfo, rebuilt.ChangeRank(true, chromInfo.Rank, chromInfo.RankByLevel));
                positionsChecked++;
            }

            return positionsChecked;
        }
    }
}
