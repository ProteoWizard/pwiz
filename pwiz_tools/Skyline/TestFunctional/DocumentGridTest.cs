/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for DocumentGridTest
    /// </summary>
    [TestClass]
    public class DocumentGridTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDocumentGrid()
        {
            TestFilesZip = @"TestFunctional\DocumentGridTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestImportResults();
        }
        
        /// <summary>
        /// Tests that when importing results, the DocumentGrid and the LiveResults grid both update to show
        /// the correct number of rows.
        /// </summary>
        private void TestImportResults() 
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("DocumentGridTest.sky")));
            var exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);

            // Show a DocumentGridForm for the "PeptideReplicates" view.
            DocumentGridForm peptideReplicatesForm = null;
            var manageViewsForm = ShowDialog<ManageViewsForm>(exportLiveReportDlg.EditList);
            RunUI(() =>
            {
                manageViewsForm.ImportViews(TestFilesDir.GetTestPath("PeptideReplicates.skyr"));
            });
            OkDialog(manageViewsForm, manageViewsForm.Close);
            RunUI(() =>
            {
                exportLiveReportDlg.ReportName = "PeptideReplicates";
                Assert.IsNull(FindOpenForm<DocumentGridForm>());
                exportLiveReportDlg.ShowPreview();
                peptideReplicatesForm = FindOpenForm<DocumentGridForm>();
                Assert.IsNotNull(peptideReplicatesForm);
            });
            OkDialog(exportLiveReportDlg, exportLiveReportDlg.CancelClick);
            AssertRowCount(SkylineWindow.Document.MoleculeCount, peptideReplicatesForm);
            Assert.IsFalse(SkylineWindow.Document.Settings.HasResults);

            // Import one replicate
            using (new WaitDocumentChange())
            {
                var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
                RunUI(() => openDataSourceDialog.SelectFile(TestFilesDir.GetTestPath("Replicate1.mz5")));
                OkDialog(openDataSourceDialog, openDataSourceDialog.Open);
            }
            WaitForResultsImport();
            Assert.AreEqual(SkylineWindow.Document.MoleculeCount, peptideReplicatesForm.RowCount);

            // Now that we have one replicate in the document, we can show the Results Grid.  It should have one row
            var liveResultsGrid = ShowDialog<LiveResultsGrid>(() => SkylineWindow.ShowResultsGrid(true));
            WaitForConditionUI(() => liveResultsGrid.IsComplete);
            Assert.AreEqual(1, liveResultsGrid.RowCount);

            // Import a second replicate
            using (new WaitDocumentChange())
            {
                var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
                RunUI(() => openDataSourceDialog.SelectFile(TestFilesDir.GetTestPath("Replicate2.mz5")));
                OkDialog(openDataSourceDialog, openDataSourceDialog.Open);
            }
            WaitForResultsImport();
            Assert.AreEqual(2, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count);

            // The DocumentGrid which is showing "PeptideReplicates" should be showing the Cartesian product 
            // of peptides and replicates
            AssertRowCount(SkylineWindow.Document.PeptideCount * 2 + (TestSmallMolecules ? 1 : 0), peptideReplicatesForm);

            // The Results Grid should show the two replicates
            WaitForConditionUI(() => liveResultsGrid.IsComplete);
            Assert.AreEqual(2, liveResultsGrid.RowCount);

            OkDialog(peptideReplicatesForm, peptideReplicatesForm.Close);
        }

        private void WaitForResultsImport()
        {
            WaitForConditionUI(() =>
            {
                SrmDocument document = SkylineWindow.DocumentUI;
                return document.Settings.HasResults && document.Settings.MeasuredResults.IsLoaded;
            });
        }

        private void AssertRowCount(int expectedRowCount, DataboundGridForm databoundGridForm)
        {
            if (!TryWaitForCondition(() =>databoundGridForm.IsComplete && (expectedRowCount == databoundGridForm.BindingListSource.Count)))
                Assert.AreEqual(expectedRowCount, databoundGridForm.BindingListSource.Count, "wrong row count in databoundGridForm");
        }
    }
}
