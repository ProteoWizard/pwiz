/*
 * Original author: Eduardo Armendariz <wardough .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Databinding;
using pwiz.SkylineTestUtil;
using System.Windows.Forms;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ReplicatePivotGridTest : AbstractFunctionalTest
    {
        private const string replicatePivotViewName = "Replicate Pivot";
        private const string replicatePivotNoVariablePropertyViewName = "Replicate Pivot No Variable Property";
        private const string replicatePivotNoConstantPropertyViewName = "Replicate Pivot No Constant Property";
        private const int expectedReplicateGridRowCount = 5;
        private const int expectedFreezeEligibleColumns = 6;
        private const int expectedDefaultFrozenColumns = 1;
        private const string expectedDefaultFrozenColumn = "COLUMN_Peptide.Protein";
        private const int randomSeed = 1000;

        // TODO(wardough): Fix the issue or switch to a generalized string in TestExclusionReason
        [TestMethod, NoParallelTesting("VerifyColumnSizesAligned failing in Docker Desktop")]
        public void TestReplicatePivotGrid()
        {
            TestFilesZip = @"TestFunctional\ReplicatePivotGridTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_plasma.sky")));
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            var replicatePivotDataGridView = documentGrid.DataboundGridControl.ReplicatePivotDataGridView;

            // Verify pivot by replicate pivot grid is shown and populated.
            RunUI(() => documentGrid.ChooseView(replicatePivotViewName));
            WaitForConditionUI(() => documentGrid.IsComplete);
            
            Assert.IsTrue(replicatePivotDataGridView.Visible);
            Assert.AreEqual(expectedReplicateGridRowCount, replicatePivotDataGridView.RowCount);

            // Verify pivot by replicate pivot grid is hidden if no replicate variable properties.
            RunUI(() => documentGrid.ChooseView(replicatePivotNoVariablePropertyViewName));
            WaitForConditionUI(() => documentGrid.IsComplete);
            Assert.IsFalse(replicatePivotDataGridView.Visible);

            // Verify pivot by replicate pivot grid is hidden if no replicate constant properties.
            RunUI(() => documentGrid.ChooseView(replicatePivotNoConstantPropertyViewName));
            WaitForConditionUI(() => documentGrid.IsComplete);
            Assert.IsFalse(replicatePivotDataGridView.Visible);

            // Verify that column sizes are aligned for each replicate.
            RunUI(() => documentGrid.ChooseView(replicatePivotViewName));
            WaitForConditionUI(() => documentGrid.IsComplete);
            VerifyColumnSizesAligned(documentGrid);

            // Resize replicate pivot grid and verify main grid widths are aligned.
            ResizeGridColumns(documentGrid.DataboundGridControl.ReplicatePivotDataGridView);
            VerifyColumnSizesAligned(documentGrid);

            // Resize main grid and verify replicate pivot grid widths are aligned.
            ResizeGridColumns(documentGrid.DataboundGridControl.DataGridView);
            VerifyColumnSizesAligned(documentGrid);

            // Verify columns begin frozen by default, then scroll and verify alignment
            var mainGridFrozenColumns = GetMainGridFrozenColumns(documentGrid);
            Assert.AreEqual(expectedDefaultFrozenColumns, mainGridFrozenColumns.Count);
            Assert.AreEqual(expectedDefaultFrozenColumn, mainGridFrozenColumns.Last().DataPropertyName);
            Assert.IsTrue(GetReplicateGridPropertyColumn(documentGrid).Frozen);
            RunUI(() => documentGrid.DataboundGridControl.DataGridView.HorizontalScrollingOffset = 10);
            VerifyColumnSizesAligned(documentGrid);

            // Select frozen option to disable freezing and verify columns are not frozen
            RunUI(() => documentGrid.NavBar.GroupButton.ShowDropDown());
            RunUI(() => documentGrid.NavBar.FreezeColumnsMenuItem.ShowDropDown());
            RunUI(() => documentGrid.NavBar.FreezeColumnsMenuItem.DropDownItems[0].PerformClick());
            mainGridFrozenColumns = GetMainGridFrozenColumns(documentGrid);
            Assert.AreEqual(0, mainGridFrozenColumns.Count);
            Assert.IsFalse(GetReplicateGridPropertyColumn(documentGrid).Frozen);

            // Freeze all eligible columns and verify alignment
            RunUI(() => documentGrid.NavBar.GroupButton.ShowDropDown());
            RunUI(() => documentGrid.NavBar.FreezeColumnsMenuItem.ShowDropDown());
            // We add one as the dropdown starts with the default option
            RunUI(() => documentGrid.NavBar.FreezeColumnsMenuItem.DropDownItems[expectedFreezeEligibleColumns + 1].PerformClick());
            mainGridFrozenColumns = GetMainGridFrozenColumns(documentGrid);
            Assert.AreEqual(expectedFreezeEligibleColumns, mainGridFrozenColumns.Count);
            Assert.IsTrue(GetReplicateGridPropertyColumn(documentGrid).Frozen);


            // Verify link cell can be clicked and updates replicate selection
            Assert.AreEqual("H_146_REP1", GetCurrentReplicateSelection());
            var cellToClick = GetReplicateGridCell(documentGrid, ColumnCaptions.Replicate, "D_102_REP3") as DataGridViewLinkCell;
            Assert.IsNotNull(cellToClick);
            RunUI(() => documentGrid.DataboundGridControl.replicatePivotDataGridView_OnCellContentClick(null, new DataGridViewCellEventArgs(cellToClick.ColumnIndex, cellToClick.RowIndex)));
            Assert.AreEqual("D_102_REP3", GetCurrentReplicateSelection());

            // Verify Export generates expected file, use invariant to avoid language and timezone discrepancies across environments
            var outputCsvFile = TestContext.GetTestResultsPath("DocumentGridExportTest.csv");
            var expectedCsvFile = TestFilesDir.GetTestPath("Replicate Pivot.csv");
            var exportReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(() =>
            {
                exportReportDlg.ReportName = replicatePivotViewName;
                exportReportDlg.SetUseInvariantLanguage(true);
            });
            OkDialog(exportReportDlg, () => exportReportDlg.OkDialog(outputCsvFile, ','));
            Assert.AreEqual(File.ReadAllText(expectedCsvFile), File.ReadAllText(outputCsvFile));
        }

        private string GetCurrentReplicateSelection()
        {
            return CallUI(() => SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[SkylineWindow.SelectedResultsIndex]).ToString();
        }

        private DataGridViewCell GetReplicateGridCell(DocumentGridForm documentGrid, string rowName, string columnName)
        {
            var row = FindReplicateGridRow(documentGrid, rowName);
            return row.Cells[columnName];
        }
        private DataGridViewRow FindReplicateGridRow(DocumentGridForm documentGrid, string rowName)
        {
            return documentGrid.DataboundGridControl.ReplicatePivotDataGridView.Rows
                .Cast<DataGridViewRow>()
                .First(row => rowName.Equals(row.Cells[0].Value?.ToString()));
        }

        private void VerifyColumnSizesAligned(DocumentGridForm documentGrid)
        {
            // Verify replicate columns are aligned.
            var mainGridColumnWidths = TextUtil.SpaceSeparate(GetMainGridColumnWidths(documentGrid)
                .OrderBy(kvp => kvp.Key).Select(kvp => kvp.Key + "=" + kvp.Value));
            var replicateGridColumnWidths = TextUtil.SpaceSeparate(GetReplicateGridColumnWidths(documentGrid)
                .OrderBy(kvp => kvp.Key).Select(kvp => kvp.Key + "=" + kvp.Value));
            Assert.AreEqual(mainGridColumnWidths, replicateGridColumnWidths);

            // Verify property column is aligned.
            var replicateGridPropertyColumnWidth = GetReplicateGridPropertyWidth(documentGrid);
            var mainGridPropertyColumnsWidth = GetMainGridPropertyColumnsWidth(documentGrid);

            Assert.AreEqual(replicateGridPropertyColumnWidth, mainGridPropertyColumnsWidth);
        }

        private Dictionary<string, int> GetReplicateGridColumnWidths(DocumentGridForm documentGrid)
        {
            var replicateGridView = documentGrid.DataboundGridControl.ReplicatePivotDataGridView;
            var originalScrollBars = documentGrid.DataboundGridControl.ReplicatePivotDataGridView.ScrollBars;
            RunUI(() => documentGrid.DataboundGridControl.ReplicatePivotDataGridView.ScrollBars = ScrollBars.None);

            var propertyColumnHeaderText = documentGrid.DataboundGridControl.ReplicatePropertyColumn.HeaderText;
            Assert.AreEqual(Skyline.Model.Databinding.DatabindingResources.SkylineViewContext_WriteDataWithStatus_Property, propertyColumnHeaderText);

            var columnWidths = replicateGridView.Columns.Cast<DataGridViewColumn>()
                .Where(col => !propertyColumnHeaderText.Equals(col.HeaderText) && col.Visible)
                // Use col.Width for non-freezable columns as they should always align.
                .ToDictionary(col => col.HeaderText, col => col.Width);
            RunUI(() => documentGrid.DataboundGridControl.ReplicatePivotDataGridView.ScrollBars = originalScrollBars);
            return columnWidths;
        }

        private Dictionary<string, int> GetMainGridColumnWidths(DocumentGridForm documentGrid)
        {
            var itemProperties = documentGrid.DataboundGridControl.BindingListSource.ItemProperties;
            var boundDataGridView = documentGrid.DataboundGridControl.DataGridView;
            var replicatePivotColumns = ReplicatePivotColumns.FromItemProperties(itemProperties);
            var originalScrollBars = documentGrid.DataboundGridControl.DataGridView.ScrollBars;
            RunUI(() => documentGrid.DataboundGridControl.DataGridView.ScrollBars = ScrollBars.None);
            var columnWidths = replicatePivotColumns.GetReplicateColumnGroups()
                .ToDictionary(
                    kvp => kvp.Key.ReplicateName,
                    kvp => kvp.Sum(column =>
                        boundDataGridView.Columns
                            .Cast<DataGridViewColumn>()
                            .Where(col => col.Visible && col.DataPropertyName == column.Name)
                            // Use col.Width for non-freezable columns as they should always align.
                            .Sum(col => col.Width)
                    )
                );
            RunUI(() => documentGrid.DataboundGridControl.DataGridView.ScrollBars = originalScrollBars);
            return columnWidths;
        }

        private int GetReplicateGridPropertyWidth(DocumentGridForm documentGrid)
        {
            var replicateGridView = documentGrid.DataboundGridControl.ReplicatePivotDataGridView;
            return CalculateColumnVisibleWidth(replicateGridView, GetReplicateGridPropertyColumn(documentGrid));
        }

        private int GetMainGridPropertyColumnsWidth(DocumentGridForm documentGrid)
        {
            var itemProperties = documentGrid.DataboundGridControl.BindingListSource.ItemProperties;
            var boundDataGridView = documentGrid.DataboundGridControl.DataGridView;
            var replicatePivotColumns = ReplicatePivotColumns.FromItemProperties(itemProperties);
            var replicateColumnNames = replicatePivotColumns.GetReplicateColumnGroups()
                .SelectMany(group => group.Select(col => col.Name)).ToHashSet();
            return boundDataGridView.Columns.Cast<DataGridViewColumn>()
                .Where(col => !replicateColumnNames.Contains(col.DataPropertyName))
                .Sum(col => CalculateColumnVisibleWidth(boundDataGridView, col));

        }

        private DataGridViewColumn GetReplicateGridPropertyColumn(DocumentGridForm documentGrid)
        {
            return documentGrid.DataboundGridControl.ReplicatePivotDataGridView.Columns[@"colReplicateProperty"];
        }

        private List<DataGridViewColumn> GetMainGridFrozenColumns(DocumentGridForm documentGrid)
        {
            return documentGrid.DataboundGridControl.DataGridView.Columns.Cast<DataGridViewColumn>()
                .Where(col => col.Frozen).ToList();
        }

        private void ResizeGridColumns(DataGridView dataGridView)
        {
            var random = new Random(randomSeed);
            foreach (DataGridViewColumn column in dataGridView.Columns)
            {
                var originalWidth = column.Width;
                var newWidth = (int)(originalWidth * (.9 + (random.NextDouble() * .1)));
                RunUI(() => column.Width = Math.Max(newWidth, 10));
            }
        }

        private int CalculateColumnVisibleWidth(DataGridView view, DataGridViewColumn column)
        {
            Rectangle columnRect = view.GetColumnDisplayRectangle(column.Index, false);
            var visibleLeft = Math.Max(columnRect.Left, view.DisplayRectangle.Left);
            var visibleRight = Math.Min(columnRect.Right, view.DisplayRectangle.Right);
            var visibleWidth = Math.Max(0, visibleRight - visibleLeft);
            return visibleWidth;
        }
    }
}
