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

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that ratios displayed on the Sequence Tree are correct.
    /// There are two different variants of this test: one of which uses the Expand Peptides
    /// menu item so that the precursors are displayed, and one which uses the Expand Precursors
    /// menu item so that the ratios on TransitionTreeNodes can be verified.
    /// </summary>
    [TestClass]
    public class TargetRatiosTest : AbstractFunctionalTest
    {
        private bool _testingTransitions;
        /// <summary>
        /// Tests that the ratios displayed on TransitionGroupTreeNodes are correct.
        /// </summary>
        [TestMethod]
        public void TestTargetRatiosOnPrecursors()
        {
            _testingTransitions = false;
            TestFilesZip = @"TestFunctional\TargetRatiosTest.zip";
            RunFunctionalTest();
        }

        /// <summary>
        /// Tests that the ratios displayed on TransitionTreeNodes are correct.
        /// </summary>
        [TestMethod]
        public void TestTargetRatiosOnTransitions()
        {
            _testingTransitions = true;
            TestFilesZip = @"TestFunctional\TargetRatiosTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            NormalizationMethod ratioToHeavy = new NormalizationMethod.RatioToLabel(IsotopeLabelType.heavy);
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Human_plasma.sky")));
            VerifyNormalizationMethodDisplayed(ratioToHeavy);
            RunUI(()=> SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_plasma.sky")));
            // Consider(nicksh): When we first open "Rat_plasma.sky", Skyline is displaying ratios to heavy
            // even though this document has no heavy peptides. Maybe this behavior should be changed to show ratios to global
            // standards
            VerifyNormalizationMethodDisplayed(ratioToHeavy);
            RunUI(()=>SkylineWindow.AreaNormalizeOption = NormalizeOption.GLOBAL_STANDARDS);
            VerifyNormalizationMethodDisplayed(NormalizationMethod.GLOBAL_STANDARDS);
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Human_plasma.sky")));
            VerifyNormalizationMethodDisplayed(ratioToHeavy);
            RunUI(()=>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.SetStandardType(StandardType.GLOBAL_STANDARD);
            });
            VerifyNormalizationMethodDisplayed(ratioToHeavy);
            RunUI(()=>
            {
                SkylineWindow.AreaNormalizeOption = NormalizeOption.GLOBAL_STANDARDS;
                Assert.AreEqual(NormalizeOption.GLOBAL_STANDARDS, SkylineWindow.SequenceTree.NormalizeOption);
            });
            VerifyNormalizationMethodDisplayed(NormalizationMethod.GLOBAL_STANDARDS);
            RunUI(()=>
            {
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.FromNormalizationMethod(NormalizationMethod.EQUALIZE_MEDIANS));
            });
            VerifyNormalizationMethodDisplayed(NormalizationMethod.GLOBAL_STANDARDS);
            RunUI(()=>SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.FromNormalizationMethod(ratioToHeavy)));
            VerifyNormalizationMethodDisplayed(ratioToHeavy);
        }

        private void VerifyNormalizationMethodDisplayed(NormalizationMethod normalizationMethod)
        {
            Assert.AreEqual(normalizationMethod, SkylineWindow.SequenceTree.NormalizeOption?.NormalizationMethod);
            var normalizedValueCalculator = new NormalizedValueCalculator(SkylineWindow.Document);
            int transitionGroupCount = 0;
            int transitionCount = 0;
            string strTotalRatio = string.Format(Resources.TransitionGroupTreeNode_GetResultsText_total_ratio__0__,
                string.Empty);
            string strTransitionRatio = GetUnchangingTransitionRatioText();
            RunUI(() =>
            {
                if (_testingTransitions)
                {
                    SkylineWindow.ExpandPrecursors();
                }
                else
                {
                    SkylineWindow.ExpandPeptides();
                }
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

                            string transitionGroupNodeText = transitionGroupTreeNode.Text;
                            if (expectedRatio == null)
                            {
                                AssertNotContains(transitionGroupNodeText, strTotalRatio);
                            }
                            else
                            {
                                StringAssert.Contains(transitionGroupNodeText, strTotalRatio);
                                string ratioText = TransitionGroupTreeNode.FormatRatioValue(expectedRatio);
                                StringAssert.Contains(transitionGroupNodeText, ratioText);
                            }

                            if (!_testingTransitions)
                            {
                                continue;
                            }

                            foreach (var transitionTreeNode in
                                     transitionGroupTreeNode.Nodes.OfType<TransitionTreeNode>())
                            {
                                transitionCount++;
                                var transitionChromInfo = transitionTreeNode.DocNode
                                    .GetChromInfo(SkylineWindow.SelectedResultsIndex, null);
                                double? expectedRatioValue = null;
                                if (NormalizationMethod.GLOBAL_STANDARDS.Equals(normalizationMethod))
                                {
                                    expectedRatioValue =
                                        normalizedValueCalculator.GetTransitionValue(
                                            NormalizationMethod.GLOBAL_STANDARDS, peptideTreeNode.DocNode, transitionTreeNode.DocNode,
                                            transitionChromInfo);
                                }
                                else if (normalizationMethod is NormalizationMethod.RatioToLabel ratioToLabel)
                                {
                                    if (!Equals(ratioToLabel.IsotopeLabelTypeName, transitionGroup.LabelType.Name))
                                    {
                                        expectedRatioValue = normalizedValueCalculator.GetTransitionValue(ratioToLabel,
                                            peptideTreeNode.DocNode, transitionTreeNode.DocNode, transitionChromInfo);
                                    }
                                }

                                string transitionNodeText = transitionTreeNode.Text;
                                if (expectedRatioValue.HasValue)
                                {
                                    StringAssert.Contains(transitionNodeText, strTransitionRatio);
                                    string expectedRatioText =
                                        string.Format(Resources.TransitionTreeNode_GetResultsText__0__ratio__1__,
                                            string.Empty, MathEx.RoundAboveZero((float) expectedRatioValue, 2, 4));
                                    StringAssert.Contains(transitionNodeText, expectedRatioText);
                                }
                                else
                                {
                                    AssertNotContains(transitionNodeText, strTransitionRatio);
                                }
                            }
                        }
                    }
                }
            });
            Assert.AreEqual(transitionGroupCount, SkylineWindow.Document.MoleculeTransitionGroupCount);
            if (_testingTransitions)
            {
                Assert.AreEqual(transitionCount, SkylineWindow.Document.MoleculeTransitionCount);
            }
        }

        private void AssertNotContains(string value, string substring)
        {
            Assert.IsFalse(value.Contains(substring), "{0} should not contain {1}", value, substring);
        }

        /// <summary>
        /// Returns the text which is expected to be found on a Transition Tree Node whenever it is displaying
        /// a ratio.
        /// </summary>
        private string GetUnchangingTransitionRatioText()
        {
            string templateText = Resources.TransitionTreeNode_GetResultsText__0__ratio__1__;
            int firstCloseBrace = templateText.IndexOf("}", StringComparison.Ordinal) + 1;
            int lastOpenBrace = templateText.LastIndexOf("{", StringComparison.Ordinal);
            return templateText.Substring(firstCloseBrace, lastOpenBrace - firstCloseBrace);
        }
    }
}
