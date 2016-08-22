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
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class MultiSelectRetentionTimeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMultiSelectRetentionTime()
        {
            TestFilesZip = @"TestFunctional\MultiSelectRetentionTimeTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MultiSelectRetentionTimeTest.sky")));
            Assert.AreEqual(BarType.Cluster, SkylineWindow.GraphRetentionTime.GraphControl.GraphPane.BarSettings.Type);
            // Test select all
            RunUI(() =>
            {
                SkylineWindow.SelectAll();
                SkylineWindow.ShowGraphRetentionTime(true);
            });
            WaitForGraphs();
            Assert.AreEqual(BarType.Overlay, SkylineWindow.GraphRetentionTime.GraphControl.GraphPane.BarSettings.Type);
            Assert.AreEqual(18, SkylineWindow.GraphRetentionTime.CurveCount);

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
                    case "sp|P02647|APOA1_HUMAN":
                        curveCount = 8;
                        break;
                    case "GST_SCHJA_Fusion_Peptide":
                        curveCount = 2;
                        break;
                    case "PRTC peptides":
                        curveCount = 8;
                        break;
                }
                Assert.AreEqual(curveCount, SkylineWindow.GraphRetentionTime.CurveCount);
                Assert.AreEqual(BarType.Overlay, SkylineWindow.GraphRetentionTime.GraphControl.GraphPane.BarSettings.Type);
                SummaryReplicateGraphPane pane;
                Assert.IsTrue(SkylineWindow.GraphRetentionTime.TryGetGraphPane(out pane));
                
                // Select indavidual peptides
                foreach (TreeNode peptide in peptideGroupTreeNode.Nodes)
                {
                    SelectNode(peptide);
                    Assert.AreEqual(4, SkylineWindow.GraphRetentionTime.CurveCount);  // All peptides have 4 precursor ions
                    Assert.AreEqual(BarType.Cluster, SkylineWindow.GraphRetentionTime.GraphControl.GraphPane.BarSettings.Type);
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
