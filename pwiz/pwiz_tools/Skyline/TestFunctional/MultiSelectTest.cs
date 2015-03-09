/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for MultiSelect.
    /// </summary>
    [TestClass]
    public class MultiSelectTest : AbstractFunctionalTest
    {
        private IList<Identity> _expectedSelNodes;
        private Identity _expectedSelNode;

        [TestMethod]
        public void TestMultiSelect()
        {
            TestFilesZip = @"TestFunctional\AnnotationTest.zip";
            RunFunctionalTest();
        }

        /// <summary>
        /// Tests multiple selection in the peptide tree view.
        /// </summary>
        protected override void DoTest()
        {
            TestSmallMolecules = false; // Adding that extra node violates some assumptions about document shape in this test

            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CE_Vantage_15mTorr_scheduled_mini.sky")));
            
            _expectedSelNodes = new List<Identity>();

            // Three main multi-select tests. Testing for delete/undo occurs within these tests.
            TestRangeSelect();
            TestInsertNode();
            TestDisjointSelect();            
        }

        private void TestRangeSelect()
        {
            TreeNode node = null;
            RunUI(() => {
                SkylineWindow.SequenceTree.ExpandAll();
                node = SkylineWindow.SequenceTree.Nodes[1];
                SkylineWindow.SequenceTree.SelectedNode = node;
                });

            // Range select below.
            TestAddRange(node, true, 2);
            
            // Range select chaining.
            TestAddRange(node, true, 4);
            
            // Range select reduce selection.
            ClearExpectedSelection();
            TestAddRange(node, true, 3);
            
            // Range select reverse selection.
            ClearExpectedSelection();
            TestAddRange(node, false, 2);
            
            // Disjoint select following range selection.
            ExpectedSelNodesAddRemove(node, false);
            TestSelectNode(node, Keys.Control);

            ClearExpectedSelection();
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.KeysOverride = Keys.None;
                SkylineWindow.SequenceTree.SelectedNode = null;
            });

        }

        private void TestInsertNode()
        {
            // Insert node cannot be deleted. 
            // Insert node becomes new anchor for range select following delete.

            TreeNode node = null;
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.CollapseAll();
                node = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 2];
            });
            ExpectedSelNodesAddRemove(node, true);
            TestSelectNode(node, null);
            RunUI(() => node = node.NextNode);
            ExpectedSelNodesAddRemove(node, true);
            TestSelectNode(node, Keys.Shift);
            ClearExpectedSelection();


            ExpectedSelNodesAddRemove(node, true);
            RunUI(() =>
                      {
                          SkylineWindow.SequenceTree.KeysOverride = Keys.None;
                          SkylineWindow.EditDelete();
                          node = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 2];
                      });
            ExpectedSelNodesAddRemove(node, true);
            TestSelectNode(node, Keys.Shift);


            ClearExpectedSelection();
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.KeysOverride = Keys.None;
                SkylineWindow.SequenceTree.SelectedNode = null;
            });
        }
        
        private void TestDisjointSelect()
        {
            RunUI(() =>
                      {
                          SkylineWindow.SequenceTree.ExpandAll();
                          SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0];
                      });
            
            // Select every other node.
            DisjointSelectEveryOther(true);

            // Deselect every other node.
            DisjointSelectEveryOther(false);

            // Test range select following disjoint select.
            TreeNodeCollection nodes = null;
            RunUI(() =>
                      {
                          nodes = SkylineWindow.SequenceTree.Nodes;
                          SkylineWindow.SequenceTree.CollapseAll();
                      });
            foreach(TreeNode node in nodes)
                ExpectedSelNodesAddRemove(node, true);
            _expectedSelNode = _expectedSelNodes[0];
            TestSelectNode(nodes[0], Keys.Shift);
            RunUI(() => SkylineWindow.SequenceTree.KeysOverride = Keys.None);
       }

        private void TestDeleteUndo(TreeNode expectedSelNode)
        {
            if (_expectedSelNodes.Count > 0)
            {
                // Test delete nodes, copying the exepcted selection to temporary variables.
                RunUI(() => SkylineWindow.EditDelete());
                var tempSelNodes = new Identity[_expectedSelNodes.Count];
                _expectedSelNodes.CopyTo(tempSelNodes, 0);
                ClearExpectedSelection();
                Identity tempSelNode = _expectedSelNode;
                var srmNode = expectedSelNode as SrmTreeNode;
                _expectedSelNode = srmNode != null ? srmNode.Model.Id : SequenceTree.NODE_INSERT_ID;
                ExpectedSelNodesAddRemove(expectedSelNode, true);
                CheckSelectedNodes();

                // Testing Undo, check that selection has been properly restored, including expansion.
                RunUI(() =>
                          {
                              SkylineWindow.SequenceTree.CollapseAll();
                              SkylineWindow.Undo();
                          });
                ClearExpectedSelection();
                foreach (Identity i in tempSelNodes)
                    _expectedSelNodes.Add(i);
                ICollection<TreeNodeMS> seqTreeNodes = null;
                RunUI(()=> seqTreeNodes = SkylineWindow.SequenceTree.SelectedNodes);
                foreach (TreeNodeMS node in seqTreeNodes)
                {
                    bool expanded = false;
                    TreeNodeMS ms = node;
                    RunUI(() => expanded = ms.IsExpanded || ms.Nodes.Count == 0);
                    Assert.IsTrue(expanded);
                }
                _expectedSelNode = tempSelNode;
                CheckSelectedNodes();
            }
        }

        private void DisjointSelectEveryOther(bool selecting)
        {
            TreeNode node = null;
            RunUI(() => node = SkylineWindow.SequenceTree.Nodes[0]);
            while (node != null)
            {
                ExpectedSelNodesAddRemove(node, selecting);
                TestSelectNode(node, Keys.Control);
                RunUI(() =>
                          {
                              node = node.NextVisibleNode != null ? node.NextVisibleNode.NextVisibleNode : null;
                          });
            }
            CheckSelectedNodes();
            TestDeleteUndo(node);
        }

        private void TestAddRange(TreeNode node, bool down, int numToAdd)
        {
            ExpectedSelNodesAddRemove(node, true);
            while(numToAdd > 0)
            {
                numToAdd--;
                TreeNode nodeNext = node;
                RunUI(() =>
                {
                    nodeNext = down ? nodeNext.NextVisibleNode : nodeNext.PrevVisibleNode;
                });
                ExpectedSelNodesAddRemove(nodeNext, true);
                node = nodeNext;
            }
            TestSelectNode(node, Keys.Shift);
        }

        private void ClearExpectedSelection()
        {
            _expectedSelNodes.Clear();
        }


       private void ExpectedSelNodesAddRemove(TreeNode n, bool add)
        {
            var node = n as SrmTreeNode;
            Identity id = node != null ? node.Model.Id : SequenceTree.NODE_INSERT_ID;
            if(add && !_expectedSelNodes.Contains(id))
                _expectedSelNodes.Add(id);
            else if(!add)
                _expectedSelNodes.Remove(id);
            _expectedSelNode = id;
        }

        private void TestSelectNode(TreeNode node, Keys? keys)
        {
            RunUI(() => 
                {
                    SkylineWindow.SequenceTree.KeysOverride = keys ?? Keys.None;
                    SkylineWindow.SequenceTree.SelectedNode = node;
                });
            CheckSelectedNodes();
        }

        private void CheckSelectedNodes()
        {
            var seqTree = SkylineWindow.SequenceTree;
            
            // Check number of expected selected nodes equals actual number of selected nodes.
            Assert.AreEqual(_expectedSelNodes.Count, seqTree.SelectedNodes.Count);
            
            foreach(TreeNodeMS node in seqTree.SelectedNodes)
            {
                var srmTreeNode = node as SrmTreeNode;
                Identity id = srmTreeNode == null ? SequenceTree.NODE_INSERT_ID : srmTreeNode.Model.Id;
                
                // Check expected selected nodes match actual selected nodes.
                Assert.IsTrue(ReferenceEquals(id, SequenceTree.NODE_INSERT_ID) || _expectedSelNodes.Contains(id));
            }
            SrmTreeNode selNode = null;
            RunUI(() => selNode = seqTree.SelectedNode as SrmTreeNode);
            
            // Check expected selected node matches actual selected node.
            if (selNode == null)
                Assert.AreEqual(_expectedSelNode, SequenceTree.NODE_INSERT_ID);
            else
                Assert.AreEqual(_expectedSelNode, selNode.Model.Id);
        }
    }
}