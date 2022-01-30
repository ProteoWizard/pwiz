/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class TargetRatiosTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestTargetRatios()
        {
            TestFilesZip = @"TestFunctional\TargetRatiosTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            NormalizationMethod ratioToHeavy = new NormalizationMethod.RatioToLabel(IsotopeLabelType.heavy);
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Human_plasma.sky")));
            VerifyNormalizationMethodDisplayed(ratioToHeavy);
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_plasma.sky"));
            });
            // Consider(nicksh): When we first open "Rat_plasma.sky", Skyline is displaying ratios to heavy
            // even though this document has no heavy peptides. Maybe this behavior should be changed to show ratios to global
            // standards
            VerifyNormalizationMethodDisplayed(ratioToHeavy);
            RunUI(()=>SkylineWindow.AreaNormalizeOption = NormalizeOption.GLOBAL_STANDARDS);
            VerifyNormalizationMethodDisplayed(NormalizationMethod.GLOBAL_STANDARDS);
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Human_plasma.sky")));
            VerifyNormalizationMethodDisplayed(ratioToHeavy);
        }

        private void VerifyNormalizationMethodDisplayed(NormalizationMethod normalizationMethod)
        {
            var normalizedValueCalculator = new NormalizedValueCalculator(SkylineWindow.Document);
            int transitionGroupCount = 0;
            string strTotalRatio = string.Format(Resources.TransitionGroupTreeNode_GetResultsText_total_ratio__0__,
                string.Empty);
            RunUI(() =>
            {
                SkylineWindow.ExpandPeptides();
                foreach (var peptideGroupTreeNode in SkylineWindow.SequenceTree.Nodes.OfType<PeptideGroupTreeNode>())
                {
                    foreach (var peptideTreeNode in peptideGroupTreeNode.Nodes.OfType<PeptideTreeNode>())
                    {
                        foreach (var transitionGroupTreeNode in peptideTreeNode.Nodes.OfType<TransitionGroupTreeNode>())
                        {
                            transitionGroupCount++;
                            var transitionGroup = transitionGroupTreeNode.DocNode;
                            var transitionGroupChromInfo =
                                transitionGroup.GetChromInfo(SkylineWindow.SelectedResultsIndex, null);
                            RatioValue expectedRatio = null;
                            if (NormalizationMethod.GLOBAL_STANDARDS.Equals(normalizationMethod))
                            {
                                var ratioToGlobalStandards =
                                    normalizedValueCalculator.GetTransitionGroupValue(
                                        NormalizationMethod.GLOBAL_STANDARDS, peptideTreeNode.DocNode, transitionGroup,
                                        transitionGroupChromInfo);
                                if (ratioToGlobalStandards.HasValue)
                                {
                                    expectedRatio = new RatioValue(ratioToGlobalStandards.Value);
                                }
                            }
                            else if (normalizationMethod is NormalizationMethod.RatioToLabel ratioToLabel)
                            {
                                if (!Equals(ratioToLabel.IsotopeLabelTypeName, transitionGroup.LabelType.Name))
                                {
                                    expectedRatio = normalizedValueCalculator.GetTransitionGroupRatioValue(ratioToLabel,
                                        peptideTreeNode.DocNode, transitionGroupTreeNode.DocNode, transitionGroupChromInfo);
                                }
                            }

                            string nodeText = transitionGroupTreeNode.Text;
                            if (expectedRatio == null)
                            {
                                Assert.IsFalse(nodeText.Contains(strTotalRatio), "{0} should not contains {1}", nodeText, strTotalRatio);
                            }
                            else
                            {
                                Assert.IsTrue(nodeText.Contains(strTotalRatio), "{0} should contain {1}", nodeText, strTotalRatio);
                                string ratioText = TransitionGroupTreeNode.FormatRatioValue(expectedRatio);
                                Assert.IsTrue(nodeText.Contains(ratioText));
                            }
                        }
                    }
                }
            });
            Assert.AreEqual(transitionGroupCount, SkylineWindow.Document.MoleculeTransitionGroupCount);
        }
    }
}
