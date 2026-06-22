/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises the Targets-tree "Pick Children" path through the AI connector verbs: select a
    /// precursor node (<see cref="JsonUiService.SetItemSelected"/>), open its pick-list with
    /// <see cref="JsonUiService.InvokeContextMenuItem"/> ("Pick Children"), toggle a transition in the
    /// popup with <see cref="JsonUiService.SetItemChecked"/>, and commit with the green-check "OK"
    /// button via <see cref="JsonUiService.ClickFormButton"/> (an image-only ToolStrip item matched by
    /// its tooltip).
    /// </summary>
    [TestClass]
    public class PickChildrenConnectorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPickChildrenConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Insert a single peptide so the Targets tree has a precursor with child transitions. The
            // protein name contains '|' (a UniProt-style "sp|ACC|NAME") so the test also exercises that
            // a '>'-separated node path is split only on '>', not on the '|' in the node text.
            RunUI(() => SkylineWindow.SequenceTree.SelectPath(new IdentityPath(SequenceTree.NODE_INSERT_ID)));
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg =>
            {
                SetClipboardText(TextUtil.LineSeparate("ELVISLIVESK\tsp|P12345|TEST_PROT"));
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });

            // Build the '>'-separated node path to the precursor from the live node text.
            string precursorPath = null;
            TreeNodeMS precursorNode = null;
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.ExpandAll();
                var groupNode = SkylineWindow.SequenceTree.Nodes[0];
                var peptideNode = groupNode.Nodes[0];
                precursorNode = (TreeNodeMS) peptideNode.Nodes[0];
                Assert.IsTrue(groupNode.Text.Contains("|"),
                    @"Expected the protein node text to contain '|' to exercise tree-path splitting.");
                precursorPath = string.Format("{0} > {1} > {2}",
                    groupNode.Text, peptideNode.Text, precursorNode.Text);
            });

            string treeFormId = JsonUiService.GetOpenForms()
                .First(form => form.Type == nameof(SequenceTreeForm)).Id;

            // Select the precursor node; Pick Children acts on the selection, so the node must be in it.
            JsonUiService.SetItemSelected(treeFormId, string.Empty, precursorPath, true);
            RunUI(() =>
            {
                Assert.IsTrue(precursorNode.IsInSelection, @"Precursor node was not put into the selection.");
                Assert.IsTrue(SequenceTree.CanPickChildren(SkylineWindow.SequenceTree.SelectedNode),
                    @"Pick Children is not available for the selected node.");
            });

            int transitionsBefore = 0;
            RunUI(() => transitionsBefore = SkylineWindow.Document.MoleculeTransitionCount);

            // Right-click > Pick Children opens the (modeless) pick-list popup.
            JsonUiService.InvokeContextMenuItem(treeFormId, string.Empty, @"Pick Children");
            var popup = WaitForOpenForm<PopupPickList>();
            string popupId = JsonUiService.GetOpenForms()
                .First(form => form.Type == nameof(PopupPickList)).Id;

            // Toggle the checked state of the first transition by its visible label.
            string label = null;
            bool wasChecked = false;
            RunUI(() =>
            {
                label = popup.GetItemLabel(0);
                wasChecked = popup.GetItemChecked(0);
            });
            Assert.IsFalse(string.IsNullOrEmpty(label));

            JsonUiService.SetItemChecked(popupId, string.Empty, label, !wasChecked);
            RunUI(() => Assert.AreEqual(!wasChecked, popup.GetItemChecked(0),
                @"SetItemChecked did not toggle the pick-list item."));

            // Commit with the green-check "OK" button (image-only ToolStrip item, matched by tooltip).
            JsonUiService.ClickFormButton(popupId, @"OK");
            WaitForClosedForm(popup);

            int transitionsAfter = 0;
            RunUI(() => transitionsAfter = SkylineWindow.Document.MoleculeTransitionCount);
            Assert.AreEqual(transitionsBefore + (wasChecked ? -1 : 1), transitionsAfter,
                @"Committing the pick list did not change the transition count as expected.");
        }
    }
}
