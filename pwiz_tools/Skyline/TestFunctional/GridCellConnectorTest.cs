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

using System;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises acting on a particular grid cell through the current cell:
    ///   * <see cref="JsonToolServer.SetFormValue"/> with a "grid[column,row]" controlId sets a cell;
    ///   * <see cref="JsonToolServer.SetCurrentCellAddress"/> moves to a cell, then a <see cref="UiElementPath"/>
    ///     whose Type is "ContextMenu" on the grid invokes that cell's right-click context menu (here,
    ///     sorting a Document Grid column descending).
    /// Menu items are matched by their visible text.
    /// </summary>
    [TestClass]
    public class GridCellConnectorTest : McpConnectorTest
    {
        [TestMethod]
        public void TestGridCellConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Drive the inlined verb(s) through the running JSON tool server (torn down with the window).
            StartToolService();

            SetFormValueIntoGridCell();
            InvokeGridCellContextMenu();
        }

        // SetFormValue with a "grid[column,row]" controlId sets that cell (plain Rule Set Editor grid).
        private void SetFormValueIntoGridCell()
        {
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            RunUI(() => documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.metadata_rules));
            var ruleSetEditor = ShowDialog<MetadataRuleSetEditor>(documentSettingsDlg.AddMetadataRule);
            string editorId = GetOpenFormId<MetadataRuleSetEditor>();

            var rulesGrid = (DataGridView)ruleSetEditor.Controls.Find(@"dataGridViewRules", true).First();
            int patternColumn = -1;
            RunUI(() =>
            {
                var visibleColumns = rulesGrid.Columns.Cast<DataGridViewColumn>()
                    .Where(col => col.Visible).OrderBy(col => col.DisplayIndex).ToList();
                patternColumn = visibleColumns.FindIndex(col => col.Name == @"colPattern");
            });
            Assert.IsTrue(patternColumn >= 0);

            Connector.SetFormValue(editorId, $@"dataGridViewRules[{patternColumn},0]", @"D");
            RunUI(() => Assert.AreEqual(@"D", rulesGrid.Rows[0].Cells[@"colPattern"].Value?.ToString(),
                @"SetFormValue did not set the grid cell named by the locator."));

            OkDialog(ruleSetEditor, () => ruleSetEditor.DialogResult = DialogResult.Cancel);
            OkDialog(documentSettingsDlg, () => documentSettingsDlg.DialogResult = DialogResult.Cancel);
        }

        // Moving to a Document Grid cell and invoking its right-click context menu (Type "ContextMenu" on
        // the inner grid, walked into via the DataboundGridControl) sorts that column descending.
        private void InvokeGridCellContextMenu()
        {
            RunUI(() => SkylineWindow.NewDocument());
            RunUI(() => SkylineWindow.SequenceTree.SelectPath(new IdentityPath(SequenceTree.NODE_INSERT_ID)));
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg =>
            {
                SetClipboardText(TextUtil.LineSeparate("RPKPQQFFGLM\tSubstance P", "DVPKSDQFVGLM\tKassinin"));
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });

            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = WaitForOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides));
            WaitForConditionUI(() => documentGrid.IsComplete);
            string gridId = GetOpenFormId<DocumentGridForm>();

            // Move to the first column (row 0), then choose Sort Descending from that cell's context menu
            // -- the grid's context menu acts on the current cell. The DataboundGridControl is a container;
            // its inner grid (a DataGridView) owns the context menu, so the path walks into it.
            Connector.SetCurrentCellAddress(gridId, string.Empty, 0, 0);
            var gridContextMenu = new UiElementPath(
                new UiElementPath(
                    new UiElementPath(
                        new UiElementPath(null, gridId, null, @"Form"), null, null, @"DataboundGridControl"),
                    null, null, @"DataGridView"),
                null, null, @"ContextMenu");
            var sortDescending = new UiElementPath(gridContextMenu, @"Sort Descending", null, null);
            Connector.PerformAction(sortDescending, @"click", null);
            WaitForConditionUI(() => documentGrid.IsComplete);

            // The first column is now sorted descending: read it back and check the order.
            string gridText = Connector.GetGridText(gridId, string.Empty);
            var firstColumn = gridText
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1) // header
                .Select(line => line.Split('\t')[0])
                .ToList();
            Assert.IsTrue(firstColumn.Count >= 2, gridText);
            for (int i = 1; i < firstColumn.Count; i++)
                Assert.IsTrue(string.CompareOrdinal(firstColumn[i - 1], firstColumn[i]) >= 0,
                    @"The grid cell context menu did not sort the column descending: " + gridText);

            RunUI(() => SkylineWindow.ShowDocumentGrid(false));
        }
    }
}
