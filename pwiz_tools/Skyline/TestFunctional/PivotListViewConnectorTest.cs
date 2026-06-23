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
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises the select_item PerformAction action on a ListView -- the available-columns list in the
    /// Pivot Editor (a ColumnListView). The item is matched by text, like a ListBox.
    /// </summary>
    [TestClass]
    public class PivotListViewConnectorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPivotListViewConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.SequenceTree.SelectPath(new IdentityPath(SequenceTree.NODE_INSERT_ID)));
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg =>
            {
                SetClipboardText(TextUtil.LineSeparate("RPKPQQFFGLM\tSubstance P"));
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });

            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = WaitForOpenForm<DocumentGridForm>();
            WaitForConditionUI(() => documentGrid.IsComplete);

            // The Pivot Editor's available-columns list is a ListView (ColumnListView).
            var pivotEditor = ShowDialog<PivotEditor>(() => documentGrid.NavBar.ShowPivotDialog(true));
            string pivotId = JsonUiService.GetOpenForms()
                .First(form => form.Type == nameof(PivotEditor)).Id;

            string columnText = null;
            RunUI(() => columnText = pivotEditor.AvailableColumnList.Items[0].Text);
            Assert.IsFalse(string.IsNullOrEmpty(columnText));

            // The available-columns list has no caption; address it by its type ("ListView").
            var listView = new UiElementPath(
                new UiElementPath(null, pivotId, null, @"Form"), null, null, @"ListView");
            JsonUiService.PerformAction(listView, @"select_item", columnText);
            RunUI(() => Assert.IsTrue(
                pivotEditor.AvailableColumnList.SelectedItems.Cast<ListViewItem>().Any(i => i.Text == columnText),
                @"select_item did not select the list-view item."));

            OkDialog(pivotEditor, () => pivotEditor.DialogResult = DialogResult.Cancel);
            RunUI(() => SkylineWindow.ShowDocumentGrid(false));
        }
    }
}
