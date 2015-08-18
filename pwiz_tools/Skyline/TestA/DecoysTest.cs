/*
 * Original author: Lucia Espona <espona .at. imsb.biol.ethz.ch>,
 *                  IMSB, ETHZ
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for RefineTest
    /// </summary>
    [TestClass]
    public class DecoysTest : AbstractUnitTest
    {
        [TestMethod]
        public void GenerateDecoysTest()
        {
            TestFilesDir testFilesDir = new TestFilesDir(TestContext, @"TestA\SimpleDecoys.zip");

            string docPath = testFilesDir.GetTestPath("SimpleDecoys.sky");
            SrmDocument simpleDecoysDoc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(simpleDecoysDoc, 0, 1, 18, 18, 56);

            // First check right number of decoy peptide groups and transtions are generated
            var refineSettings = new RefinementSettings();
            int numDecoys = simpleDecoysDoc.PeptideCount;
            var decoysDoc = refineSettings.GenerateDecoys(simpleDecoysDoc, numDecoys, DecoyGeneration.ADD_RANDOM);
            ValidateDecoys(simpleDecoysDoc, decoysDoc, false);

            // Second call to generate decoys to make sure that it removes the original Decoys group and generates a completely new one.
            var newDecoysDoc = refineSettings.GenerateDecoys(decoysDoc, numDecoys, DecoyGeneration.ADD_RANDOM);
            Assert.AreNotEqual(decoysDoc.PeptideGroups.First(nodePeptideGroup => nodePeptideGroup.IsDecoy),
                newDecoysDoc.PeptideGroups.First(nodePeptideGroup => nodePeptideGroup.IsDecoy));

            // MS1 document with precursors and variable modifications, shuffled
            docPath = testFilesDir.GetTestPath("Ms1FilterTutorial.sky");
            SrmDocument variableDecoysDoc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(variableDecoysDoc, 0, 11, 50, 51, 153);
            numDecoys = variableDecoysDoc.PeptideCount;
            var decoysVariableShuffle = refineSettings.GenerateDecoys(variableDecoysDoc, numDecoys, DecoyGeneration.SHUFFLE_SEQUENCE);
            ValidateDecoys(variableDecoysDoc, decoysVariableShuffle, true);

            // Document with explicit modifications, reversed
            SrmDocument docStudy7 = ResultsUtil.DeserializeDocument("Study7.sky", GetType());
            AssertEx.IsDocumentState(docStudy7, 0, 7, 11, 22, 66);
            numDecoys = docStudy7.PeptideCount;
            var decoysExplicitReverse = refineSettings.GenerateDecoys(docStudy7, numDecoys, DecoyGeneration.REVERSE_SEQUENCE);
            ValidateDecoys(docStudy7, decoysExplicitReverse, true);

            // Random mass shifts with precursors (used to throw an exception due to bad range check that assumed product ions only)
            while (true)
            {
                // As this is random we may need to make a few attempts before we get the values we need
                SrmDocument doc = ResultsUtil.DeserializeDocument(testFilesDir.GetTestPath("precursor_decoy_test.sky"));
                AssertEx.IsDocumentState(doc, null, 1, 1, 2, 6);
                numDecoys = 2;
                var decoysRandom = refineSettings.GenerateDecoys(doc, numDecoys, DecoyGeneration.ADD_RANDOM);
                // Verify that we successfully set a precursor transition decoy mass outside the allowed range of product transitions
                if (decoysRandom.PeptideTransitions.Any(x => x.IsDecoy &&
                                                    (x.Transition.DecoyMassShift > Transition.MAX_PRODUCT_DECOY_MASS_SHIFT ||
                                                     x.Transition.DecoyMassShift < Transition.MIN_PRODUCT_DECOY_MASS_SHIFT)))
                    break;
            }

        }

        private static void ValidateDecoys(SrmDocument document, SrmDocument decoysDoc, bool modifiesSequences)
        {
            AssertEx.IsDocumentState(decoysDoc, 1, document.PeptideGroupCount + 1, document.PeptideCount*2,
                document.PeptideTransitionGroupCount*2, document.PeptideTransitionCount*2);

            // Check for the existence of the Decoys peptide group and that everything under it is marked as a decoy. 
            var nodePeptideGroupDecoy = decoysDoc.PeptideGroups.Single(nodePeptideGroup => nodePeptideGroup.IsDecoy);
            var dictModsToPep = document.Peptides.ToDictionary(nodePep => nodePep.ModifiedSequence);
            foreach (var nodePep in nodePeptideGroupDecoy.Peptides)
            {
                Assert.AreEqual(true, nodePep.IsDecoy);
                PeptideDocNode nodePepSource = null;
                if (!modifiesSequences)
                    Assert.IsNull(nodePep.SourceKey);
                else
                {
                    Assert.IsNotNull(nodePep.SourceKey, string.Format("Source key for {0}{1} is null", nodePep.ModifiedSequence,
                        nodePep.IsDecoy ? " - decoy" : string.Empty));
                    Assert.IsTrue(FastaSequence.IsExSequence(nodePep.SourceKey.Sequence));
                    Assert.AreEqual(nodePep.SourceKey.ModifiedSequence,
                        SequenceMassCalc.NormalizeModifiedSequence(nodePep.SourceKey.ModifiedSequence));
                    if (nodePep.HasExplicitMods)
                        Assert.IsNotNull(nodePep.SourceKey.ExplicitMods);
                    Assert.IsTrue(dictModsToPep.TryGetValue(nodePep.SourceTextId, out nodePepSource));
                    var sourceKey = new ModifiedSequenceMods(nodePepSource.Peptide.Sequence, nodePepSource.ExplicitMods);
                    Assert.AreEqual(sourceKey.ExplicitMods, nodePep.SourceExplicitMods);
                }
                for (int i = 0; i < nodePep.TransitionGroupCount; i++)
                {
                    var nodeGroup = nodePep.TransitionGroups.ElementAt(i);
                    Assert.AreEqual(true, nodeGroup.IsDecoy);
                    TransitionGroupDocNode nodeGroupSource = null;
                    double shift = SequenceMassCalc.GetPeptideInterval(nodeGroup.TransitionGroup.DecoyMassShift);
                    if (nodePepSource != null && nodeGroup.TransitionGroup.DecoyMassShift.HasValue)
                    {
                        nodeGroupSource = nodePepSource.TransitionGroups.ElementAt(i);
                        Assert.AreEqual(nodeGroupSource.PrecursorMz + shift, nodeGroup.PrecursorMz, SequenceMassCalc.MassTolerance);
                    }
                    for (int j = 0; j < nodeGroup.TransitionCount; j++)
                    {
                        var nodeTran = nodeGroup.Transitions.ElementAt(j);
                        Assert.IsTrue(nodeTran.IsDecoy);
                        if (nodeTran.Transition.IsPrecursor())
                        {
                            Assert.AreEqual(nodeGroup.TransitionGroup.DecoyMassShift, nodeTran.Transition.DecoyMassShift);
                            if (nodeGroupSource != null)
                            {
                                Assert.AreEqual(nodeGroupSource.Transitions.ElementAt(j).Mz + shift, nodeTran.Mz, SequenceMassCalc.MassTolerance);
                            }
                        }
                    }
                }
            }

            // Check that the resulting document persists correctly by passing the SrmDocument to AssertEx.IsSerializable().
            AssertEx.Serializable(decoysDoc);
        }
    }
}