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
    [TestClass]
    public class SequenceTreeRatioTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSequenceTreeRatio()
        {
            TestFilesZip = @"TestFunctional\SequenceTreeRatioTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SequenceTreeRatioTest.sky"));
                SkylineWindow.ExpandPrecursors();
            });
            VerifyDisplayText(NodeTextWithNoRatio);
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 1);
                SkylineWindow.SetStandardType(StandardType.GLOBAL_STANDARD);
                SkylineWindow.SetNormalizationMethod(NormalizeOption.GLOBAL_STANDARDS);
            });
            VerifyDisplayText(NodeTextRatioToGlobalStandards);
            RunUI(()=>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 1);
                SkylineWindow.SetStandardType(StandardType.QC);
                // TODO(nicksh): removing the global standard should cause the ratios to disappear from the Targets tree,
                // but for now we collapse all and expand all to make the text update
                SkylineWindow.CollapseProteins();
                SkylineWindow.ExpandPrecursors();
            });
            VerifyDisplayText(NodeTextWithNoRatio);
        }

        private void VerifyDisplayText(Func<TransitionTreeNode, string> getTextFunc)
        {
            RunUI(() =>
            {
                foreach (var peptideGroupTreeNode in SkylineWindow.SequenceTree.GetSequenceNodes())
                {
                    foreach (PeptideTreeNode peptideTreeNode in peptideGroupTreeNode.Nodes)
                    {
                        foreach (TransitionGroupTreeNode transitionGroupTreeNode in peptideTreeNode.Nodes)
                        {
                            foreach (TransitionTreeNode transitionTreeNode in transitionGroupTreeNode.Nodes)
                            {
                                string expected = getTextFunc(transitionTreeNode);
                                string actual = transitionTreeNode.Text;
                                if (expected != actual)
                                {
                                    AssertEx.AreEqual(getTextFunc(transitionTreeNode), transitionTreeNode.Text);
                                }
                            }
                        }
                    }
                }
            });
        }

        private string NodeTextWithNoRatio(TransitionTreeNode transitionTreeNode)
        {
            return TransitionTreeNode.GetLabel(transitionTreeNode.DocNode, GetRankText(transitionTreeNode));
        }

        private string NodeTextRatioToGlobalStandards(TransitionTreeNode transitionTreeNode)
        {
            var normalizedValueCalculator = new NormalizedValueCalculator(SkylineWindow.DocumentUI);
            var ratioToGlobalStandards = normalizedValueCalculator.GetTransitionValue(
                NormalizationMethod.GLOBAL_STANDARDS, transitionTreeNode.PepNode,
                transitionTreeNode.TransitionGroupNode, transitionTreeNode.DocNode,
                transitionTreeNode.DocNode.GetSafeChromInfo(0).FirstOrDefault());
            Assert.IsNotNull(ratioToGlobalStandards);
            string resultsText = string.Format(Resources.TransitionTreeNode_GetResultsText__0__ratio__1__, 
                GetRankText(transitionTreeNode), MathEx.RoundAboveZero((float) ratioToGlobalStandards.Value, 2, 4));
            return TransitionTreeNode.GetLabel(transitionTreeNode.DocNode, resultsText);
        }

        private string GetRankText(TransitionTreeNode transitionTreeNode)
        {
            int? rank = transitionTreeNode.DocNode.GetPeakRankByLevel(0);
            if (!rank.HasValue)
            {
                return string.Empty;
            }
            string rankText = rank.ToString();
            if (transitionTreeNode.DocNode.IsMs1)
            {
                rankText = "i " + rankText;
            }

            return string.Format(Resources.TransitionTreeNode_GetResultsText__0__, rankText);
        }
    }
}
