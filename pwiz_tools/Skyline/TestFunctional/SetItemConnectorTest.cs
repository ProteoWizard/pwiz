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

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises <see cref="JsonUiService.SetItemChecked"/> and <see cref="JsonUiService.SetItemSelected"/>
    /// against both kinds of control they support:
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

        // SetItemChecked / SetItemSelected on the "Applies to" CheckedListBox.
        private void TestCheckedListBox()
        {
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(
                documentSettingsDlg.EditAnnotationList);
            var defineAnnotationDlg = ShowDialog<DefineAnnotationDlg>(editListDlg.AddItem);
            string dlgId = JsonUiService.GetOpenForms()
                .First(form => form.Type == nameof(DefineAnnotationDlg)).Id;

            // SetItemChecked sets the "Replicates" item's check explicitly (idempotent, unlike a toggle).
            JsonUiService.SetItemChecked(dlgId, @"checkedListBoxAppliesTo", @"Replicates", true);
            RunUI(() => Assert.IsTrue(
                defineAnnotationDlg.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate),
                @"SetItemChecked did not check the Replicates item."));
            JsonUiService.SetItemChecked(dlgId, @"checkedListBoxAppliesTo", @"Replicates", false);
            RunUI(() => Assert.IsFalse(
                defineAnnotationDlg.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate),
                @"SetItemChecked did not uncheck the Replicates item."));

            // SetItemSelected highlights an item (separate from checking it).
            JsonUiService.SetItemSelected(dlgId, @"checkedListBoxAppliesTo", @"Peptides", true);
            RunUI(() =>
            {
                var checkedListBox = (CheckedListBox)defineAnnotationDlg.Controls
                    .Find(@"checkedListBoxAppliesTo", true).First();
                Assert.AreEqual(@"Peptides", checkedListBox.GetItemText(checkedListBox.SelectedItem),
                    @"SetItemSelected did not select the Peptides item.");
            });

            OkDialog(defineAnnotationDlg, () => defineAnnotationDlg.DialogResult = DialogResult.Cancel);
            OkDialog(editListDlg, () => editListDlg.DialogResult = DialogResult.Cancel);
            OkDialog(documentSettingsDlg, () => documentSettingsDlg.DialogResult = DialogResult.Cancel);
        }

        // SetItemChecked / SetItemSelected on the Customize Report field tree, addressed by node path.
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
            string treeName = null, parentText = null, childText = null;
            RunUI(() =>
            {
                treeName = tree.Name;
                var root = tree.Nodes[0];
                root.Expand(); // populate the lazily-built children
                parentText = root.Text;
                childText = root.Nodes[0].Text;
            });
            string nodePath = parentText + @" > " + childText;

            JsonUiService.SetItemChecked(editorId, treeName, nodePath, true);
            RunUI(() =>
            {
                var node = tree.Nodes[0].Nodes.Cast<TreeNode>().First(n => n.Text == childText);
                Assert.IsTrue(node.Checked, @"SetItemChecked did not check the tree node " + nodePath);
            });

            JsonUiService.SetItemSelected(editorId, treeName, nodePath, true);
            RunUI(() => Assert.AreEqual(childText, tree.SelectedNode?.Text,
                @"SetItemSelected did not select the tree node."));

            OkDialog(viewEditor, () => viewEditor.DialogResult = DialogResult.Cancel);
            RunUI(() => SkylineWindow.ShowDocumentGrid(false));
        }
    }
}
