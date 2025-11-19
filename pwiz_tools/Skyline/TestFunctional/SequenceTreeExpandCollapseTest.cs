using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SequenceTreeExpandCollapseTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSequenceTreeExpansion()
        {
            TestFilesZip = @"TestFunctional\SequenceTreeExpandCollapseTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunDlg<AlertDlg>(() =>
                {
                    SkylineWindow.OpenFile(TestFilesDir.GetTestPath("DIA-TTOF-tutorial-large-test.sky"));
                },
            alertDlg => { alertDlg.OkDialog();});
            RunUI(() =>     // Make sure all nodes in the expanded tree is selected
            {
                for (int i = 0; i < 10; i++)
                    SkylineWindow.SequenceTree.SelectNode((TreeNodeMS)SkylineWindow.SequenceTree.Nodes[i], true);
                var selectedNodes = SkylineWindow.SequenceTree.SelectedNodes.ToList();
                SkylineWindow.SequenceTree.ExpandSelectionBulk(typeof(PeptideTreeNode));
                var level = SkylineWindow.SequenceTree.GetNodeTypeLevel(typeof(PeptideTreeNode));
                Assert.IsTrue(selectedNodes.All(node => NodeSelectionRecursive(node, true, level)));
            });
            RunUI(() =>     // Collapse the tree
            {
                SkylineWindow.SequenceTree.ExpandSelectionBulk(typeof(SrmTreeNodeParent));
                Assert.IsTrue(SkylineWindow.SequenceTree.Nodes.OfType<TreeNodeMS>().All(node => !node.IsExpanded));
            });

            // Check that the too large expansion warning dialog is shown
            var alertDlg = ShowDialog<AlertDlg>(() =>     
                {
                    for (var i = 0; i < 500; i++)
                        SkylineWindow.SequenceTree.SelectNode((TreeNodeMS)SkylineWindow.SequenceTree.Nodes[i], true);

                    SkylineWindow.SequenceTree.ExpandSelectionBulk(typeof(TransitionGroupTreeNode));
                }, 
                1000);
            RunUI(()=>{ alertDlg.ClickOk(); });
            
        }

        /// <summary>
        /// a method that recursively checks all children of the given TreeNodeMS node
        /// down to the given level (inclusive) 
        /// and returns true if all of them have selection status (selected/not selected)
        /// specified as the method parameter
        /// </summary>
        private bool NodeSelectionRecursive(TreeNodeMS node, bool selected, int level)
        {
            if (node == null)
                return false;
            // Do not check child nodes if the current node is below the specified level
            if (SkylineWindow.SequenceTree.GetNodeLevel(node) > level)
                return true;
            // Check if the current node's selection status matches the specified status
            if (node.IsInSelection != selected)
                return false;
            // Recursively check all child nodes
            if (node.Nodes.OfType<TreeNodeMS>().Any(childNode => !NodeSelectionRecursive(childNode, selected, level)))
                return false;

            return true;
        }
    }
}