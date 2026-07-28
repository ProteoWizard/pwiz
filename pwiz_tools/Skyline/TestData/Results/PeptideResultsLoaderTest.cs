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
    /// out of the chromatogram cache by <see cref="PeptideResultsLoader"/>, at the same flat
    /// positions the document uses. That has to be true before the document can stop holding
    /// those values.
    /// </summary>
    [TestClass]
    public class PeptideResultsLoaderTest : AbstractUnitTest
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
                foreach (var nodePep in docResults.Peptides)
                {
                    var loaded = new PeptideResultsLoader
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

                            positionsChecked += CheckTransition(loaded, nodeTran);
                        }
                    }
                }

                Assert.AreNotEqual(0, positionsChecked);
            }
        }

        /// <summary>
        /// Walks the document's results as one flat sequence of positions and checks that the
        /// loader produced the same positions holding the same values.
        /// </summary>
        private static int CheckTransition(LoadedPeptideResults loaded, TransitionDocNode nodeTran)
        {
            var documentChromInfos = new List<TransitionChromInfo>();
            var countsPerReplicate = new List<int>();
            foreach (var chromInfoList in nodeTran.Results)
            {
                countsPerReplicate.Add(chromInfoList.Count);
                documentChromInfos.AddRange(chromInfoList);
            }

            var loadedTransition = loaded.GetTransition(nodeTran);
            Assert.IsNotNull(loadedTransition);
            Assert.AreEqual(documentChromInfos.Count, loadedTransition.PositionCount);

            // The loader has to agree with the document about which replicate each position
            // belongs to, not merely about how many positions there are.
            Assert.AreEqual(ReplicatePositions.FromCounts(countsPerReplicate),
                loadedTransition.ChromFileIds.ReplicatePositions);

            int positionsChecked = 0;
            for (int position = 0; position < documentChromInfos.Count; position++)
            {
                var chromInfo = documentChromInfos[position];
                Assert.AreSame(chromInfo.FileId, loadedTransition.ChromFileIds.FileIds[position].Value);
                Assert.AreEqual(chromInfo.OptimizationStep, loadedTransition.OptimizationSteps[position]);
                if (chromInfo.IsEmpty)
                {
                    continue;
                }

                int candidatePeakIndex = loaded.FindCandidatePeakIndex(nodeTran, position, chromInfo);
                Assert.AreNotEqual(-1, candidatePeakIndex);

                var rebuilt = loaded.MakeTransitionChromInfo(nodeTran, position, candidatePeakIndex,
                    chromInfo.UserSet, chromInfo.Annotations);
                Assert.IsNotNull(rebuilt);
                Assert.AreNotSame(chromInfo, rebuilt);

                // Rank is not peak data and is not something the loader can know, so compare
                // everything else by rebuilding with the ranks the document recorded.
                Assert.AreEqual(chromInfo, rebuilt.ChangeRank(true, chromInfo.Rank, chromInfo.RankByLevel));
                positionsChecked++;
            }

            return positionsChecked;
        }
    }
}
