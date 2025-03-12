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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Databinding;
using pwiz.SkylineTestUtil;
using System.Windows.Forms;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ReplicatePivotGridTest : AbstractFunctionalTest
    {
        private const string replicatePivotViewName = "Replicate Pivot";
        private const string replicatePivotNoVariablePropertyViewName = "Replicate Pivot No Variable Property";
        private const string replicatePivotNoConstantPropertyViewName = "Replicate Pivot No Constant Property";
       
        [TestMethod]
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
            Assert.AreNotEqual(0, replicatePivotDataGridView.RowCount);

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
            verifyColumnSizesAligned(documentGrid);

            // Resize replicate pivot grid and verify main grid widths are aligned.
            ResizeGridColumns(documentGrid.DataboundGridControl.ReplicatePivotDataGridView);
            verifyColumnSizesAligned(documentGrid);

            // Resize main grid and verify replicate pivot grid widths are aligned.
            ResizeGridColumns(documentGrid.DataboundGridControl.DataGridView);
            verifyColumnSizesAligned(documentGrid);

            // Verify columns begin frozen by default, since partial freezing the scroll offset shrinks replicate grid column by scroll offset.
            var scrollOffset = 10;
            RunUI(() => documentGrid.DataboundGridControl.DataGridView.HorizontalScrollingOffset = scrollOffset);
            Assert.AreEqual(0, documentGrid.DataboundGridControl.ReplicatePivotDataGridView.HorizontalScrollingOffset);
            Assert.AreEqual(GetMainGridPropertyColumnsWidth(documentGrid), GetMainGridPropertyColumnsWidth(documentGrid, true) + scrollOffset);
            Assert.AreEqual(GetReplicateGridPropertyWidth(documentGrid, true), GetMainGridPropertyColumnsWidth(documentGrid, true));

            // Select frozen column do disable freezing and verify columns are not frozen
            RunUI(() => documentGrid.NavBar.GroupButton.ShowDropDown());
            RunUI(() => documentGrid.NavBar.FreezeColumnsMenuItem.ShowDropDown());
            RunUI(() => documentGrid.NavBar.FreezeColumnsMenuItem.DropDownItems[0].PerformClick());
            RunUI(() => documentGrid.DataboundGridControl.DataGridView.HorizontalScrollingOffset = GetReplicateGridPropertyWidth(documentGrid));
            Assert.AreEqual(0, GetReplicateGridPropertyWidth(documentGrid, true));
            Assert.AreEqual(0, GetMainGridPropertyColumnsWidth(documentGrid, true));

            // Freeze all eligible columns and verify they remain visible after scrolling.
            RunUI(() => documentGrid.NavBar.GroupButton.ShowDropDown());
            RunUI(() => documentGrid.NavBar.FreezeColumnsMenuItem.ShowDropDown());
            RunUI(() => documentGrid.NavBar.FreezeColumnsMenuItem.DropDownItems[6].PerformClick());
            RunUI(() => documentGrid.DataboundGridControl.DataGridView.HorizontalScrollingOffset = GetReplicateGridPropertyWidth(documentGrid));
            Assert.AreEqual(GetReplicateGridPropertyWidth(documentGrid), GetReplicateGridPropertyWidth(documentGrid, true));
            Assert.AreEqual(GetMainGridPropertyColumnsWidth(documentGrid), GetMainGridPropertyColumnsWidth(documentGrid, true));


            // Verify link cell can be clicked and updates replicate selection
            Assert.AreEqual("H_146_REP1", GetCurrentReplicateSelection());
            var cellToClick = GetReplicateGridCell(documentGrid, "Replicate", "D_102_REP3") as DataGridViewLinkCell;
            Assert.IsNotNull(cellToClick);
            RunUI(() => documentGrid.DataboundGridControl.replicatePivotDataGridView_OnCellContentClick(null, new DataGridViewCellEventArgs(cellToClick.ColumnIndex, cellToClick.RowIndex)));
            Assert.AreEqual("D_102_REP3", GetCurrentReplicateSelection());
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

        private void verifyColumnSizesAligned(DocumentGridForm documentGrid)
        {
            // Verify replicate columns are aligned.
            var mainGridColumnWidths = GetMainGridColumnWidths(documentGrid);
            var replicateGridColumnWidths = GetReplicateGridColumnWidths(documentGrid);

            CollectionAssert.AreEqual(
                mainGridColumnWidths.OrderBy(kv => kv.Key).ToList(),
                replicateGridColumnWidths.OrderBy(kv => kv.Key).ToList()
            );

            // Verify property column is aligned.
            var replicateGridPropertyColumnWidth = GetReplicateGridPropertyWidth(documentGrid);
            var mainGridPropertyColumnsWidth = GetMainGridPropertyColumnsWidth(documentGrid);

            Assert.AreEqual(replicateGridPropertyColumnWidth, mainGridPropertyColumnsWidth);
        }

        private Dictionary<string, int> GetReplicateGridColumnWidths(DocumentGridForm documentGrid, bool onlyVisibleWidth = false)
        {
            var replicateGridView = documentGrid.DataboundGridControl.ReplicatePivotDataGridView;

            return replicateGridView.Columns
                .Cast<DataGridViewColumn>()
                .Where(col => !"colReplicateProperty".Equals(col.Name))
                .ToDictionary(col => col.Name, col => onlyVisibleWidth ? CalculateColumnVisibleWidth(replicateGridView, col) : col.Width);
        }

        private Dictionary<string, int> GetMainGridColumnWidths(DocumentGridForm documentGrid, bool onlyVisibleWidth = false)
        {
            var itemProperties = documentGrid.DataboundGridControl.BindingListSource.ItemProperties;
            var boundDataGridView = documentGrid.DataboundGridControl.DataGridView;
            var replicatePivotColumns = ReplicatePivotColumns.FromItemProperties(itemProperties);

            return replicatePivotColumns.GetReplicateColumnGroups()
                .ToDictionary(
                    kvp => kvp.Key.ReplicateName,
                    kvp => kvp.Sum(column =>
                        boundDataGridView.Columns
                            .Cast<DataGridViewColumn>()
                            .Where(col => col.Visible && col.DataPropertyName == column.Name)
                            .Sum(col => onlyVisibleWidth ? CalculateColumnVisibleWidth(boundDataGridView, col) : col.Width)
                    )
                );
        }

        private int GetReplicateGridPropertyWidth(DocumentGridForm documentGrid, bool onlyVisibleWidth = false)
        {
            var replicateGridView = documentGrid.DataboundGridControl.ReplicatePivotDataGridView;
            return onlyVisibleWidth ? 
                CalculateColumnVisibleWidth(replicateGridView, documentGrid.DataboundGridControl.ReplicatePivotDataGridView.Columns[@"colReplicateProperty"]!) 
                : documentGrid.DataboundGridControl.ReplicatePivotDataGridView.Columns[@"colReplicateProperty"]!.Width;
        }

        private int GetMainGridPropertyColumnsWidth(DocumentGridForm documentGrid, bool onlyVisibleWidth = false)
        {
            var itemProperties = documentGrid.DataboundGridControl.BindingListSource.ItemProperties;
            var boundDataGridView = documentGrid.DataboundGridControl.DataGridView;
            var replicatePivotColumns = ReplicatePivotColumns.FromItemProperties(itemProperties);
            var replicateColumnNames = replicatePivotColumns.GetReplicateColumnGroups()
                .SelectMany(group => group.Select(col => col.Name)).ToHashSet();
            return boundDataGridView.Columns.Cast<DataGridViewColumn>()
                .Where(col => !replicateColumnNames.Contains(col.DataPropertyName))
                .Sum(col => onlyVisibleWidth ? CalculateColumnVisibleWidth(boundDataGridView, col) : col.Width);

        }
        
        private void ResizeGridColumns(DataGridView dataGridView)
        {
            var random = new Random();
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
