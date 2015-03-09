/*
 * Original author: Trevor Killeen <killeent .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{   
    /// <summary>
    /// Functional test for tree state restoration from a persistent string.
    /// </summary>
    [TestClass]
    public class TreeRestorationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestTreeRestoration()
        {
            TestFilesZip = @"TestFunctional\TreeRestorationTest.zip";
            RunFunctionalTest();
        }

        // collections for comparison
        private string _selectedNode { get; set; }
        private string _topNode { get; set; }
        private ICollection<string> _selectedNodes { get; set; }
        private ICollection<string> _expandedNodes { get; set; }

        // file for majority of computations
        private string _documentFile { get; set; }

        /// <summary>
        /// Test tree state restoration from a persistent string. Tests for proper expansion and
        /// selection of nodes, and correct vertical scrolling
        /// </summary>
        protected override void DoTest()
        {
            // This test makes lots of assumptions about the shape of the document, so don't alter it with our small molecule test node
            TestSmallMolecules = false;

            // tests for a blank document
            RunUI(() =>
                {
                    SkylineWindow.NewDocument();
                    SetCurrentState();
                    SkylineWindow.SaveDocument(TestContext.GetTestPath("blank.sky"));
                    // reload file from persistent string
                    SkylineWindow.OpenFile(TestContext.GetTestPath("blank.sky"));
                    CompareStates();
                });

            _documentFile = TestFilesDir.GetTestPath("Study7_0-7.sky");
            
            // tests for a fully collapsed tree
            RunUI(() =>
                {
                    SkylineWindow.OpenFile(_documentFile);
                    SkylineWindow.CollapseProteins();
                    CheckStateMaintained();
                });

            // tests for a fully expanded tree and scrolling
            RunUI(() =>
                {
                    SkylineWindow.OpenFile(_documentFile);
                    SkylineWindow.ExpandPrecursors();
                    CheckStateMaintained();
                    SkylineWindow.SelectAll();
                    CheckStateMaintained();
                    // select last node in tree for scrolling test
                    SelectNode(SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1], null);
                    CheckStateMaintained();
                });

            // tests for expansion
            RunUI(() =>
                {
                    SkylineWindow.OpenFile(_documentFile);
                    SkylineWindow.CollapseProteins();
                    SelectNode(SkylineWindow.SequenceTree.TopNode, null);
                    CheckStateMaintained();
                    TestExpansion();
                });

            // tests for selection
            RunUI(() =>
                {
                    SkylineWindow.OpenFile(_documentFile);
                    TestSingleSelect();
                    TestSelectLastNode();
                    TestRangeSelect();
                    TestDisjointSelect();
                    TestCombinedRangeDisjoint();
                });

            // tests that Skyline will not crash when there is a mismatch between stored data in the
            // .sky.view file and that in the tree
            RunUI(() =>
                {
                    SelectNode(SkylineWindow.SequenceTree.Nodes[1], null);
                    SkylineWindow.SaveDocument(_documentFile);
                    System.IO.File.Copy(SkylineWindow.GetViewFile(_documentFile), _documentFile + ".copy");
                    SkylineWindow.EditDelete();
                    CheckStateMaintained();
                    System.IO.File.Delete(SkylineWindow.GetViewFile(_documentFile));
                    System.IO.File.Copy(_documentFile + ".copy", SkylineWindow.GetViewFile(_documentFile));
                    SkylineWindow.OpenFile(_documentFile);
                });

        }

        private void TestExpansion()
        {
            SkylineWindow.ExpandProteins();
            CheckStateMaintained();

            SkylineWindow.SequenceTree.Nodes[0].Collapse();
            CheckStateMaintained();

            SkylineWindow.SequenceTree.Nodes[1].Nodes[0].Expand();
            CheckStateMaintained();

            SkylineWindow.SequenceTree.Nodes[1].Nodes[0].Nodes[1].Expand();
            CheckStateMaintained();

            SkylineWindow.SequenceTree.Nodes[2].Collapse();
            CheckStateMaintained();

            SkylineWindow.SequenceTree.Nodes[4].Nodes[0].ExpandAll();
            CheckStateMaintained();

            SkylineWindow.SequenceTree.Nodes[6].Nodes[1].Expand();
            CheckStateMaintained();
        }

        private void TestSingleSelect()
        {
            SelectNode(SkylineWindow.SequenceTree.Nodes[1], null);
            CheckStateMaintained();

            SelectNode(SkylineWindow.SequenceTree.Nodes[1].Nodes[0], null);
            CheckStateMaintained();

            SelectNode(SkylineWindow.SequenceTree.Nodes[1].Nodes[0].Nodes[1], null);
            CheckStateMaintained();

            SelectNode(SkylineWindow.SequenceTree.Nodes[1].Nodes[0].Nodes[1].Nodes[2], null);
            CheckStateMaintained();
        }

        private void TestSelectLastNode()
        {
            TreeNode node = SkylineWindow.SequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1];
            SelectNode(node, null);
            CheckStateMaintained();
        }

        private void TestRangeSelect()
        {
            TreeNode node = SkylineWindow.SequenceTree.Nodes[1];
            SelectNode(node, null);
            SelectRange(node, 2);
            CheckStateMaintained();
            node = SkylineWindow.SequenceTree.Nodes[1];
            SelectNode(node, null);
            SelectRange(node, 7);
            CheckStateMaintained();
        }

        private void TestDisjointSelect()
        {
            TreeNode node = SkylineWindow.SequenceTree.Nodes[0];
            SelectNode(node, null);
            SelectEveryOther(node);
            CheckStateMaintained();
        }

        private void TestCombinedRangeDisjoint()
        {
            TreeNode node = SkylineWindow.SequenceTree.Nodes[0];
            SelectNode(node, null);
            SelectRange(node, 3);
            while(((TreeNodeMS)node.NextVisibleNode).IsInSelection)
            {
                node = node.NextVisibleNode;
            }
            SelectEveryOther(node);
            CheckStateMaintained();
        }

        private static void SelectEveryOther(TreeNode node)
        {
            while (node != null)
            {
                SelectNode(node, Keys.Control);
                node = node.NextVisibleNode != null ? node.NextVisibleNode.NextVisibleNode : null;
            }
        }

        private static void SelectRange(TreeNode node, int count)
        {
            while (count > 0)
            {
                count--;
                TreeNode next = node.NextVisibleNode;
                node = next;
                SelectNode(node, Keys.Shift);
            }
        }

        private static void SelectNode(TreeNode node, Keys? keys)
        {
            SkylineWindow.SequenceTree.KeysOverride = keys ?? Keys.None;
            SkylineWindow.SequenceTree.SelectedNode = node;
        }


        private void CheckStateMaintained()
        {
            SetCurrentState();
            SkylineWindow.SaveDocument(_documentFile);
            SkylineWindow.OpenFile(_documentFile);
            CompareStates();
        }

        private void CompareStates()
        {
            // selected node of the TreeView should be part of the TreeViewMS' selected nodes
            Assert.IsTrue(SkylineWindow.SelectedNode == null || SkylineWindow.SelectedNode.IsInSelection);

            // proper scrolling
            Assert.AreEqual(SkylineWindow.SequenceTree.TopNode.Text, _topNode);

            // expansion is equal
            Assert.AreEqual(GetExpandedNodes(SkylineWindow.SequenceTree.Nodes).Count, _expandedNodes.Count);
            CollectionAssert.AreEqual(GetExpandedNodes(SkylineWindow.SequenceTree.Nodes).ToArray(), _expandedNodes.ToArray());

            // selections are equal (equivalency is used because the order of selected nodes in the list may differ when restored)
            if (SkylineWindow.SelectedNode != null && _selectedNode != null)
                Assert.AreEqual(SkylineWindow.SelectedNode.Text, _selectedNode);
            Assert.AreEqual(GetSelectedNodes().Count, _selectedNodes.Count);
            CollectionAssert.AreEquivalent(GetSelectedNodes().ToArray(), _selectedNodes.ToArray());

            // auto-expand single nodes should be enabled
            Assert.IsTrue(SkylineWindow.SequenceTree.AutoExpandSingleNodes);
        }

        private void SetCurrentState()
        {
            _selectedNode = SkylineWindow.SelectedNode != null ? SkylineWindow.SelectedNode.Text : null;
            _topNode = SkylineWindow.SequenceTree.TopNode.Text;
            _selectedNodes = GetSelectedNodes();
            _expandedNodes = GetExpandedNodes(SkylineWindow.SequenceTree.Nodes);
        }

        private static ICollection<string> GetSelectedNodes()
        {
            IList<string> result = new List<string>();
            foreach (TreeNodeMS n in SkylineWindow.SelectedNodes)
            {
                result.Add(n.Text);
            }
            return result;
        }

        private static ICollection<string> GetExpandedNodes(IEnumerable nodes)
        {
            List<string> result = new List<string>();
            foreach (TreeNode parent in nodes)
            {
                if (parent.IsExpanded)
                {
                    result.Add(parent.Text);
                    result.AddRange(GetExpandedNodes(parent.Nodes));
                }
            }
            return result;
        }
    }
}
