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
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises the check_item / uncheck_item / select_item PerformAction actions against both kinds of
    /// control that support them:
    ///   * a CheckedListBox -- the Define Annotation "Applies to" list (item matched by text);
    ///   * a TreeView -- the Customize Report field tree (item is a '>'-separated node path).
    /// Matched by the English item/node text, so the test runs in en.
    /// </summary>
    [TestClass]
    public class SetItemConnectorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSetItemConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestCheckedListBox();
            TestTreeView();
        }

        // check_item / uncheck_item / select_item on the "Applies to" CheckedListBox, addressed by label.
        private void TestCheckedListBox()
        {
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(
                documentSettingsDlg.EditAnnotationList);
            var defineAnnotationDlg = ShowDialog<DefineAnnotationDlg>(editListDlg.AddItem);
            string dlgId = JsonUiService.GetOpenForms()
                .First(form => form.Type == nameof(DefineAnnotationDlg)).Id;
            var appliesTo = new ControlId
            {
                Parent = new ControlId { Type = @"Form", Name = dlgId },
                Label = @"Applies to",
            };

            // check_item / uncheck_item set the "Replicates" item's check explicitly (idempotent).
            JsonUiService.PerformAction(appliesTo, @"check_item", @"Replicates");
            RunUI(() => Assert.IsTrue(
                defineAnnotationDlg.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate),
                @"check_item did not check the Replicates item."));
            JsonUiService.PerformAction(appliesTo, @"uncheck_item", @"Replicates");
            RunUI(() => Assert.IsFalse(
                defineAnnotationDlg.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate),
                @"uncheck_item did not uncheck the Replicates item."));

            // select_item highlights an item (separate from checking it).
            JsonUiService.PerformAction(appliesTo, @"select_item", @"Peptides");
            RunUI(() =>
            {
                var checkedListBox = (CheckedListBox)defineAnnotationDlg.Controls
                    .Find(@"checkedListBoxAppliesTo", true).First();
                Assert.AreEqual(@"Peptides", checkedListBox.GetItemText(checkedListBox.SelectedItem),
                    @"select_item did not select the Peptides item.");
            });

            OkDialog(defineAnnotationDlg, () => defineAnnotationDlg.DialogResult = DialogResult.Cancel);
            OkDialog(editListDlg, () => editListDlg.DialogResult = DialogResult.Cancel);
            OkDialog(documentSettingsDlg, () => documentSettingsDlg.DialogResult = DialogResult.Cancel);
        }

        // check_item / select_item on the Customize Report field tree, addressed by node path.
        private void TestTreeView()
        {
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = WaitForOpenForm<DocumentGridForm>();
            WaitForConditionUI(() => documentGrid.IsComplete);

            var viewEditor = ShowDialog<pwiz.Common.DataBinding.Controls.Editor.ViewEditor>(
                documentGrid.NavBar.CustomizeView);
            string editorId = JsonUiService.GetOpenForms()
                .First(form => form.Type == nameof(pwiz.Common.DataBinding.Controls.Editor.ViewEditor)).Id;

            var tree = viewEditor.ChooseColumnsTab.AvailableFieldsTree;
            string parentText = null, childText = null;
            RunUI(() =>
            {
                var root = tree.Nodes[0];
                root.Expand(); // populate the lazily-built children
                parentText = root.Text;
                childText = root.Nodes[0].Text;
            });
            string nodePath = parentText + @" > " + childText;

            // The field tree has no caption and no label -- the caption-less edge case -- so the typed
            // verbs (which match a Label) cannot reach it. It is addressed instead through a ControlId by
            // its Type ("TreeView", the only tree on the form) via the general PerformAction path; the
            // value is the '>'-separated node path, and the action checks/selects it.
            var treeId = new ControlId
            {
                Parent = new ControlId { Type = @"Form", Name = editorId },
                Type = @"TreeView",
            };
            JsonUiService.PerformAction(treeId, @"check_item", nodePath);
            RunUI(() =>
            {
                var node = tree.Nodes[0].Nodes.Cast<TreeNode>().First(n => n.Text == childText);
                Assert.IsTrue(node.Checked, @"check_item did not check the tree node " + nodePath);
            });

            JsonUiService.PerformAction(treeId, @"select_item", nodePath);
            RunUI(() => Assert.AreEqual(childText, tree.SelectedNode?.Text,
                @"select_item did not select the tree node."));

            // expand / collapse a node by a path whose segments are a child's text or its index. Collapse
            // the root by its index (0), then expand it again by its text.
            RunUI(() => tree.Nodes[0].Expand());
            JsonUiService.PerformAction(treeId, @"collapse", new object[] { 0 });
            RunUI(() => Assert.IsFalse(tree.Nodes[0].IsExpanded,
                @"collapse did not collapse the root node addressed by index."));
            JsonUiService.PerformAction(treeId, @"expand", new object[] { parentText });
            RunUI(() => Assert.IsTrue(tree.Nodes[0].IsExpanded,
                @"expand did not expand the root node addressed by text."));

            OkDialog(viewEditor, () => viewEditor.DialogResult = DialogResult.Cancel);
            RunUI(() => SkylineWindow.ShowDocumentGrid(false));
        }
    }
}
