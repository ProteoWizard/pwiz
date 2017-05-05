/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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

using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class MultiSelectPeakAreaGraphTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMultiSelectPeakAreaGraph()
        {
            TestFilesZip = @"TestFunctional\MultiSelectPeakAreaGraphTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ABSciex4000_Study9-1_Site19_CalCurves only.sky")));
            // Test select all
            RunUI(() =>
            {
                SkylineWindow.SelectAll();
                SkylineWindow.ShowGraphPeakArea(true);
            });
            WaitForGraphs();
            Assert.AreEqual(6, SkylineWindow.GraphPeakArea.CurveCount);

            // Test selecting each node down to the peptide/precursor level
            foreach (var node in SkylineWindow.SequenceTree.Nodes)
            {
                if (!(node is TreeNode) || node is EmptyNode)
                    continue;
                var peptideGroupTreeNode = node as TreeNode;
                SelectNode(peptideGroupTreeNode);
                int curveCount = 0;
                switch (peptideGroupTreeNode.Text)
                {
                    case "sp|P09972|ALDOC_HUMAN":
                        curveCount = 5;
                        break;
                    case "sp|P04083|ANXA1_HUMAN":
                        curveCount = 1;
                        break;
                }
                Assert.AreEqual(curveCount, SkylineWindow.GraphPeakArea.CurveCount);
                SummaryReplicateGraphPane pane;
                Assert.IsTrue(SkylineWindow.GraphPeakArea.TryGetGraphPane(out pane));
                Assert.IsFalse(pane.Legend.IsVisible);
                // Select indavidual peptides
                foreach (TreeNode peptide in peptideGroupTreeNode.Nodes)
                {
                    SelectNode(peptide);
                    Assert.AreNotEqual(0, SkylineWindow.GraphRetentionTime.CurveCount);
                }
            }
        }
        private void SelectNode(TreeNode node)
        {
            RunUI(() =>
            {
                // Clears selection
                SkylineWindow.SequenceTree.KeysOverride = Keys.None;
                SkylineWindow.SequenceTree.SelectedNode = null;
                // Selects node
                SkylineWindow.SequenceTree.KeysOverride = Keys.None;
                SkylineWindow.SequenceTree.SelectedNode = node;
            });
            WaitForGraphs();
        }
    }
}
