/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests the <see cref="DataboundGridControl.ReplicatePivotDataGridView"/> when the same peptide can be found
    /// in multiple result files in a multi-inject replicate.
    /// </summary>
    [TestClass]
    public class MultiInjectReplicatePivotTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMultiInjectReplicatePivot()
        {
            TestFilesZip = @"TestFunctional\MultiInjectReplicatePivotTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Open the document which has a single replicate with two result files.
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MultiInjectReplicatePivotTest.sky"));
                var measuredResults = SkylineWindow.Document.MeasuredResults;
                Assert.IsNotNull(measuredResults);
                Assert.AreEqual(1, measuredResults.Chromatograms.Count);
                Assert.AreEqual(2, measuredResults.MSDataFileInfos.Count());

                SkylineWindow.ShowDocumentGrid(true);
            });
            
            // Pivot the "Transition Results" report, and add the column "Batch Name".
            var documentGridForm = FindOpenForm<DocumentGridForm>();
            RunUI(() =>
            {
                documentGridForm.ChooseView(Resources.ReportSpecList_GetDefaults_Transition_Results);
            });
            RunDlg<ViewEditor>(documentGridForm.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ChooseColumnsTab.AddColumn(PropertyPath.Root.Property(nameof(SkylineDocument.Replicates))
                    .LookupAllItems().Property(nameof(Replicate.BatchName)));
                var pivotWidget = viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>()
                    .FirstOrDefault();
                Assert.IsNotNull(pivotWidget);
                pivotWidget.SetPivotReplicate(true);
                viewEditor.OkDialog();
            });
            WaitForCondition(() => documentGridForm.IsComplete);

            const string batchNameText = "BatchNameText";
            RunUI(() =>
            {
                Assert.AreNotEqual(0, documentGridForm.RowCount);

                // Verify that the replicate pivot grid has a column for each of the injections
                var pivotGrid = documentGridForm.DataboundGridControl.ReplicatePivotDataGridView;
                Assert.AreEqual(3, pivotGrid.ColumnCount);
                var replicateRow = pivotGrid.Rows.Cast<DataGridViewRow>()
                    .First(row => ColumnCaptions.Replicate.Equals(row.Cells[0].Value));
                var replicate = (Replicate)replicateRow.Cells[1].Value;
                Assert.IsNotNull(replicate);
                Assert.AreSame(replicate, replicateRow.Cells[2].Value);
                
                // Set the Batch Name in the cell in the first pivot column
                var batchNameRow = pivotGrid.Rows.Cast<DataGridViewRow>()
                    .First(row => ColumnCaptions.BatchName.Equals(row.Cells[0].Value));
                pivotGrid.CurrentCell = batchNameRow.Cells[1];
                SetClipboardText(batchNameText);
                pivotGrid.SendPaste();
                Assert.AreEqual(batchNameText, pivotGrid.CurrentCell.Value);
            });
            WaitForCondition(() => documentGridForm.IsComplete);
            // Verify that the new Batch Name value has propagated to all the replicate pivot columns
            RunUI(() =>
            {
                var pivotGrid = documentGridForm.DataboundGridControl.ReplicatePivotDataGridView;
                Assert.AreEqual(3, pivotGrid.ColumnCount);
                var batchNameRow = pivotGrid.Rows.Cast<DataGridViewRow>()
                    .First(row => ColumnCaptions.BatchName.Equals(row.Cells[0].Value));
                for (int iCol = 1; iCol < pivotGrid.ColumnCount; iCol++)
                {
                    Assert.AreEqual(batchNameText, batchNameRow.Cells[iCol].Value);
                }
            });
        }
    }
}
