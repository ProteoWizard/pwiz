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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    public class GridCellMcpConnectorTest : McpConnectorTest
    {
        [TestMethod]
        public void TestGridCellMcpConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Drive the inlined verb(s) through the running JSON tool server (torn down with the window).
            StartToolService();

            SetFormValueIntoGridCell();
            AddressGridByItsLabel();
            InvokeGridCellContextMenu();
        }

        // SetFormValue with a "grid[column,row]" controlId sets that cell (plain Rule Set Editor grid),
        // naming the column either by its zero-based visible index or by its column HEADER -- the name the
        // user reads off the grid, and the one a tutorial step names.
        private void SetFormValueIntoGridCell()
        {
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            RunUI(() => documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.metadata_rules));
            var ruleSetEditor = ShowDialog<MetadataRuleSetEditor>(documentSettingsDlg.AddMetadataRule);
            string editorId = GetOpenFormId<MetadataRuleSetEditor>();

            var rulesGrid = (DataGridView)ruleSetEditor.Controls.Find(@"dataGridViewRules", true).First();
            int patternColumn = -1;
            string patternHeader = null;
            RunUI(() =>
            {
                var visibleColumns = rulesGrid.Columns.Cast<DataGridViewColumn>()
                    .Where(col => col.Visible).OrderBy(col => col.DisplayIndex).ToList();
                patternColumn = visibleColumns.FindIndex(col => col.Name == @"colPattern");
                // The header as the user sees it, so the by-name case below is not keyed on an English literal.
                patternHeader = patternColumn < 0 ? null : visibleColumns[patternColumn].HeaderText;
            });
            Assert.IsTrue(patternColumn >= 0);
            Assert.IsFalse(string.IsNullOrEmpty(patternHeader));

            McpConnector.SetFormValue(editorId, $@"dataGridViewRules[{patternColumn},0]", @"D");
            RunUI(() => Assert.AreEqual(@"D", rulesGrid.Rows[0].Cells[@"colPattern"].Value?.ToString(),
                @"SetFormValue did not set the grid cell named by the locator."));

            // The same cell by its column HEADER rather than its index -- what a caller reading the grid has.
            McpConnector.SetFormValue(editorId, $@"dataGridViewRules[{patternHeader},0]", @"E");
            RunUI(() => Assert.AreEqual(@"E", rulesGrid.Rows[0].Cells[@"colPattern"].Value?.ToString(),
                @"SetFormValue did not set the grid cell whose column the locator named by its header."));

            // A column that is neither an index nor a header says so, and names the columns there are.
            AssertEx.ThrowsException<Exception>(
                () => { McpConnector.SetFormValue(editorId, @"dataGridViewRules[No Such Column,0]", @"F"); },
                (Exception x) => AssertEx.Contains(x.Message, patternHeader));

            OkDialog(ruleSetEditor, () => ruleSetEditor.DialogResult = DialogResult.Cancel);
            OkDialog(documentSettingsDlg, () => documentSettingsDlg.DialogResult = DialogResult.Cancel);
        }

        // A grid named by an adjacent label -- the Build Library form's "Input Files:" over its input-file
        // grid -- resolves by the Label that GetControls reports for it, not only by its control Name.
        // Whatever the enumeration prints as a control's address has to work as one.
        private void AddressGridByItsLabel()
        {
            var peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettings.SelectedTab = PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            string buildId = GetOpenFormId<BuildLibraryDlg>();

            // The grid lives on the wizard's second page, which needs a name and a writable output path to
            // reach. Nothing is built: the point is only to have the grid on screen.
            AssertComplete(McpConnector.SetFormValue(buildId, @"textName", @"Test Library"));
            // A directory that certainly exists and is writable: the page will not advance otherwise, and
            // nothing is ever written there because the wizard is cancelled below.
            AssertComplete(McpConnector.SetFormValue(buildId, @"textPath",
                Path.Combine(Path.GetTempPath(), @"McpConnectorTestLibrary.blib")));
            RunUI(buildLibraryDlg.OkWizardPage);

            // The Label GetControls reports for the grid -- the address it advertises.
            var gridControl = McpConnector.GetControls(buildId)
                .FirstOrDefault(control => control.Name == @"gridInputFiles");
            Assert.IsNotNull(gridControl, @"The Build Library form did not report its input-file grid.");
            string gridLabel = gridControl.Path?.Text;
            Assert.IsFalse(string.IsNullOrEmpty(gridLabel),
                @"The input-file grid reported no Label for GetControls to advertise as its address.");

            // That Label resolves the grid: reading it back gives the grid's column headers.
            var gridPath = new UiElementPath(new UiElementPath(null, buildId, null, @"Form"), gridLabel, null, null);
            string headers = McpConnector.PerformAction(gridPath, @"get_grid_text", null)?.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(headers),
                @"Addressing the input-file grid by its reported Label returned nothing.");

            OkDialog(buildLibraryDlg, buildLibraryDlg.CancelDialog);
            OkDialog(peptideSettings, peptideSettings.CancelDialog);
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
            McpConnector.SetCurrentCellAddress(gridId, string.Empty, 0, 0);
            var gridContextMenu = new UiElementPath(
                new UiElementPath(
                    new UiElementPath(
                        new UiElementPath(null, gridId, null, @"Form"), null, null, @"DataboundGridControl"),
                    null, null, @"DataGridView"),
                null, null, @"ContextMenu");
            var sortDescending = new UiElementPath(gridContextMenu,
                GetLocalizedText<DataboundGridControl>(@"sortDescendingToolStripMenuItem"), null, null);
            McpConnector.PerformAction(sortDescending, @"click", null);
            WaitForConditionUI(() => documentGrid.IsComplete);

            // The first column is now sorted descending: read it back and check the order.
            string gridText = McpConnector.GetGridText(gridId, string.Empty);
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
