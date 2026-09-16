/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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

using System;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises the keyboard on the Targets tree through the AI connector, the way the MethodEdit tutorial's
    /// "press the down-arrow key", "press the Delete key" and "type into the blank node" steps need it. A plain
    /// key press reaches the tree's own navigation (Down moves the selection) and the menu bar's shortcuts
    /// (Delete is Edit &gt; Delete); begin_edit opens the in-place edit of the selected node, text sent to the tree
    /// lands in that one edit box in order, and Enter commits it while Esc cancels it.
    /// </summary>
    [TestClass]
    public class TreeEditMcpConnectorTest : McpConnectorTest
    {
        private const string TREE_CONTROL = @"SequenceTree";
        private const string NEW_LIST_NAME = @"Peptide List 2";

        [TestMethod]
        public void TestTreeEditMcpConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            StartToolService();

            // Two peptides under one protein, so Down has somewhere to go and Delete something to remove.
            RunUI(() => SkylineWindow.SequenceTree.SelectPath(new IdentityPath(SequenceTree.NODE_INSERT_ID)));
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg =>
            {
                SetClipboardText(TextUtil.LineSeparate("ELVISLIVESK\tsp|P12345|TEST_PROT",
                    "PEPTIDEK\tsp|P12345|TEST_PROT"));
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });
            string treeFormId = GetOpenFormId<SequenceTreeForm>();
            var targetsTree = new UiElementPath(
                new UiElementPath(null, treeFormId, null, @"Form"), null, null, TREE_CONTROL);

            TestNavigationKeys(treeFormId, targetsTree);
            TestInPlaceEdit(treeFormId, targetsTree);
        }

        // Down moves the selection from the protein to its first peptide; Delete removes that peptide, through
        // the Edit > Delete shortcut the tree itself does not handle.
        private void TestNavigationKeys(string treeFormId, UiElementPath targetsTree)
        {
            string groupText = null;
            TreeNode firstPeptide = null;
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.ExpandAll();
                var groupNode = SkylineWindow.SequenceTree.Nodes[0];
                groupText = groupNode.Text;
                firstPeptide = groupNode.Nodes[0];
            });
            McpConnector.PerformAction(targetsTree, @"select_item", groupText);

            McpConnector.SendKeyStroke(treeFormId, TREE_CONTROL, @"Down");
            RunUI(() => Assert.AreSame(firstPeptide, SkylineWindow.SequenceTree.SelectedNode,
                @"Down did not move the selection to the first peptide."));

            int moleculesBefore = SkylineWindow.Document.MoleculeCount;
            McpConnector.SendKeyStroke(treeFormId, TREE_CONTROL, @"Delete");
            AssertEx.AreEqual(moleculesBefore - 1, SkylineWindow.Document.MoleculeCount,
                "Delete did not remove the selected peptide.");
        }

        private void TestInPlaceEdit(string treeFormId, UiElementPath targetsTree)
        {
            // begin_edit on the blank node at the end opens the in-place edit box, a TextBox on the Targets form.
            RunUI(() => SkylineWindow.SequenceTree.SelectPath(new IdentityPath(SequenceTree.NODE_INSERT_ID)));
            McpConnector.PerformAction(targetsTree, @"begin_edit", null);
            RunUI(() => Assert.IsNotNull(SkylineWindow.SequenceTree.StatementCompletionEditBox,
                @"begin_edit did not start an in-place edit."));
            Assert.IsTrue(McpConnector.GetControls(treeFormId).Any(control => control.Path?.Type == nameof(TextBox)),
                @"The in-place edit box is not listed among the Targets form's controls.");

            // Text sent to the tree lands in that edit box, in order, and there is still exactly one box.
            McpConnector.SendText(treeFormId, TREE_CONTROL, NEW_LIST_NAME);
            RunUI(() =>
            {
                Assert.AreEqual(NEW_LIST_NAME, SkylineWindow.SequenceTree.StatementCompletionEditBox.TextBox.Text,
                    @"The text sent to the tree did not land in its edit box.");
                Assert.AreEqual(1, SkylineWindow.SequenceTree.Parent.Controls.OfType<TextBox>().Count(),
                    @"Typing into the tree left more than one edit box.");
            });

            // Enter commits: the text becomes a new peptide list.
            int groupsBefore = SkylineWindow.Document.MoleculeGroupCount;
            McpConnector.SendKeyStroke(treeFormId, TREE_CONTROL, @"Enter");
            RunUI(() => Assert.IsNull(SkylineWindow.SequenceTree.StatementCompletionEditBox, @"Enter did not commit the edit."));
            AssertEx.AreEqual(groupsBefore + 1, SkylineWindow.Document.MoleculeGroupCount,
                "Committing the edit did not add the peptide list.");
            AssertEx.AreEqual(NEW_LIST_NAME, SkylineWindow.Document.MoleculeGroups.Last().Name);

            // Esc cancels: typing begins the edit by itself, and nothing is added.
            RunUI(() => SkylineWindow.SequenceTree.SelectPath(new IdentityPath(SequenceTree.NODE_INSERT_ID)));
            McpConnector.SendText(treeFormId, TREE_CONTROL, @"Discarded");
            RunUI(() => Assert.IsNotNull(SkylineWindow.SequenceTree.StatementCompletionEditBox,
                @"Sending text to the tree did not begin an edit."));
            McpConnector.SendKeyStroke(treeFormId, TREE_CONTROL, @"Esc");
            RunUI(() => Assert.IsNull(SkylineWindow.SequenceTree.StatementCompletionEditBox, @"Esc did not cancel the edit."));
            AssertEx.AreEqual(groupsBefore + 1, SkylineWindow.Document.MoleculeGroupCount,
                "Cancelling the edit changed the document.");

            // A node that cannot be renamed (a peptide) is refused.
            string peptidePath = null;
            RunUI(() =>
            {
                var groupNode = SkylineWindow.SequenceTree.Nodes[0];
                peptidePath = groupNode.Text + @" > " + groupNode.Nodes[0].Text;
            });
            McpConnector.PerformAction(targetsTree, @"select_item", peptidePath);
            AssertEx.ThrowsException<ArgumentException>(() =>
            {
                McpConnector.PerformAction(targetsTree, @"begin_edit", null);
            });
        }
    }
}
