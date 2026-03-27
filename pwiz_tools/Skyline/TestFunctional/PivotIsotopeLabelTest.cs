/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.SkylineTestUtil;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;
using Transition = pwiz.Skyline.Model.Databinding.Entities.Transition;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PivotIsotopeLabelTest : AbstractFunctionalTest
    {
        const string REPORT_NAME = "PivotIsotopeLabelTestReport";
        [TestMethod]
        public void TestPivotIsotopeLabelExport()
        {
            TestFilesZip = @"TestFunctional\PivotIsotopeLabelTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PivotIsotopeLabelTest.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunDlg<ViewEditor>(() => documentGrid.NavBar.CustomizeView(), viewEditor =>
            {
                var ppPeptides = PropertyPath.Root.Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems();
                var ppPrecursors = ppPeptides.Property(nameof(Peptide.Precursors)).LookupAllItems();
                viewEditor.ChooseColumnsTab.AddColumn(ppPeptides.Property(nameof(Peptide.ModifiedSequence)));
                viewEditor.ChooseColumnsTab.AddColumn(ppPrecursors.Property(nameof(Precursor.Mz)));
                viewEditor.ChooseColumnsTab.AddColumn(ppPrecursors.Property(nameof(Precursor.Transitions))
                    .LookupAllItems().Property(nameof(Transition.Results)).DictionaryValues()
                    .Property(nameof(TransitionResult.Area)));
                viewEditor.ViewName = REPORT_NAME;
                viewEditor.OkDialog();
            });
            WaitForDocumentLoaded();
            VerifyDocumentGridReport();
            VerifyPivot(false, false);
            VerifyPivot(false, true);
            VerifyPivot(true, false);
            VerifyPivot(true, true);
        }

        /// <summary>
        /// Verify that the data shown in the Document Grid is the same as what you would get if you exported the report to a file
        /// when the specified pivoting options are applied.
        /// </summary>
        private void VerifyPivot(bool pivotReplicate, bool pivotIsotopeLabel)
        {
            var documentGridForm = FindOpenForm<DocumentGridForm>();
            RunDlg<ViewEditor>(()=>documentGridForm.NavBar.CustomizeView(), viewEditor =>
            {
                var pivotWidget = GetPivotWidget(viewEditor);
                pivotWidget.SetPivotReplicate(pivotReplicate);
                pivotWidget.SetPivotIsotopeLabel(pivotIsotopeLabel);
                viewEditor.OkDialog();
            });
            VerifyDocumentGridReport();
        }

        private void VerifyDocumentGridReport()
        {
            var documentGrid = FindOpenForm<DocumentGridForm>();
            Assert.IsNotNull(documentGrid);
            WaitForConditionUI(() => documentGrid.IsComplete);
            var viewName = documentGrid.GetViewName();
            Assert.IsNotNull(viewName);
            Assert.AreNotEqual(ViewGroup.BUILT_IN.Id, viewName.Value.GroupId);
            var userInterfaceRows = CallUI(()=>GetDsvRows(documentGrid.DataGridView, '\t').ToList());
            var reportName = viewName.Value.Name;
            var outFile = TestFilesDir.GetTestPath(reportName + ".tsv");
            RunDlg<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog, exportLiveReportDlg =>
            {
                exportLiveReportDlg.ReportName = reportName;
                exportLiveReportDlg.OkDialog(outFile, '\t');
            });
            var exportedRows = File.ReadAllLines(outFile);
            Assert.AreEqual(userInterfaceRows.Count, exportedRows.Length);
            for (int i = 0; i < userInterfaceRows.Count; i++)
            {
                Assert.AreEqual(userInterfaceRows[i], exportedRows[i]);
            }
        }

        private IEnumerable<string> GetDsvRows(DataGridView dataGridView, char separator)
        {
            yield return MakeDsvRow(separator,
                dataGridView.Columns.OfType<DataGridViewColumn>().Select(col => col.HeaderText));
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                yield return MakeDsvRow(separator, row.Cells.OfType<DataGridViewCell>()
                    .Select(cell => cell.FormattedValue?.ToString() ?? string.Empty));
            }
        }

        private static string MakeDsvRow(char separator, IEnumerable<object> values)
        {
            return string.Join(separator.ToString(),
                values.Select(value => DsvWriter.ToDsvField(separator, value?.ToString() ?? string.Empty)));
        }

        private PivotReplicateAndIsotopeLabelWidget GetPivotWidget(ViewEditor viewEditor)
        {
            return viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>().First();
        }
    }
}
