/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exporting a report starts in the folder the DOCUMENT is saved in, and remembers nothing: sending one
    /// report somewhere else does not make that folder the default for every report after it
    /// (https://skyline.ms/announcements/home/support/thread.view?rowId=72321). The folder Skyline does
    /// remember, Settings.Default.ExportDirectory, belongs to method export alone - report export must neither
    /// read it nor write it. Exporting and importing annotations go the same way, so they are covered here too.
    /// </summary>
    [TestClass]
    public class ExportReportDirectoryTest : AbstractFunctionalTest
    {
        private const string DOCUMENT_NAME = @"ExportReportDirectoryTest";

        private string _documentDir;
        private string _otherDir;
        private string _methodDir;

        [TestMethod]
        public void TestExportReportDirectory()
        {
            // An empty zip, for a folder that gets created fresh and cleaned up on every run. The test writes
            // a report, and re-running it over the leftovers would raise an overwrite prompt nothing dismisses.
            TestFilesZip = @"TestFunctional\ExportReportDirectoryTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            _documentDir = TestFilesDir.GetTestPath(@"Document");
            _otherDir = TestFilesDir.GetTestPath(@"Other");
            _methodDir = TestFilesDir.GetTestPath(@"Method");
            foreach (var dir in new[] { _documentDir, _otherDir, _methodDir })
                Directory.CreateDirectory(dir);

            RunUI(() =>
            {
                SkylineWindow.SaveDocument(Path.Combine(_documentDir, DOCUMENT_NAME + SrmDocument.EXT));
                // The folder method export remembers. Nothing exercised below may start here or write here.
                Settings.Default.ExportDirectory = _methodDir;
                // Saving the document just pointed ActiveDirectory at the document folder. Move it away, so a
                // dialog that starts in the document folder is doing so from the document path itself, and not
                // by happening to agree with ActiveDirectory.
                Settings.Default.ActiveDirectory = _otherDir;
            });

            TestExportReport();
            TestExportAnnotations();
            TestImportAnnotations();
            TestDocumentGridExport();
            TestUnsavedDocument();

            AssertEx.AreEqual(_methodDir, Settings.Default.ExportDirectory,
                @"Exporting a report or annotations moved the folder that method export remembers.");
        }

        /// <summary>
        /// File > Export > Report starts in the document folder. Exporting to a different folder does not move
        /// that default: shown again, the dialog starts in the document folder rather than where the report went.
        /// </summary>
        private void TestExportReport()
        {
            var reportPath = Path.Combine(_otherDir, @"PeptideRTResults.csv");
            var exportReportDlg = ShowExportReportDlg();
            RunLongNativeDlg<NativeSaveFileDialog>(exportReportDlg.OkDialog, dlg =>
            {
                AssertNativeDlgFolder(_documentDir, dlg,
                    @"Export Report did not start in the document folder.");
                dlg.EnterPath(reportPath);
                dlg.DismissWithAcceptButton();
            });
            WaitForClosedForm(exportReportDlg);
            Assert.IsTrue(File.Exists(reportPath), @"Export Report wrote nothing.");

            var exportReportDlgAgain = ShowExportReportDlg();
            RunLongNativeDlg<NativeSaveFileDialog>(exportReportDlgAgain.OkDialog, dlg =>
            {
                AssertNativeDlgFolder(_documentDir, dlg,
                    @"Export Report started where the last report was written instead of the document folder.");
                dlg.DismissWithCancelButton();
            });
            OkDialog(exportReportDlgAgain, exportReportDlgAgain.CancelClick);
        }

        /// <summary>
        /// Shows the Export Report form with a report selected, so that OkDialog raises the Save dialog. (With
        /// no report selected OkDialog returns without showing anything, which would hang the wait for it.)
        /// </summary>
        private static ExportLiveReportDlg ShowExportReportDlg()
        {
            var exportReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(() =>
            {
                exportReportDlg.ReportName = Resources.ReportSpecList_GetDefaults_Peptide_RT_Results;
                Assert.IsNotNull(exportReportDlg.SelectedViewName, @"The report to export was not found.");
            });
            return exportReportDlg;
        }

        /// <summary>
        /// File > Export > Annotations starts in the document folder, not in the folder method export remembers.
        /// </summary>
        private void TestExportAnnotations()
        {
            var exportAnnotationsDlg = ShowDialog<ExportAnnotationsDlg>(SkylineWindow.ShowExportAnnotationsDlg);
            RunLongNativeDlg<NativeSaveFileDialog>(exportAnnotationsDlg.OkDialog, dlg =>
            {
                AssertNativeDlgFolder(_documentDir, dlg,
                    @"Export Annotations did not start in the document folder.");
                // The default file name is built from the document name, so it comes from the same path and
                // goes missing in the same way if that path cannot be found.
                AssertEx.AreEqual(DOCUMENT_NAME + @"Annotations.csv",
                    dlg.GetFormValue(NativeFileDialog.FILE_NAME_FIELD),
                    @"Export Annotations did not name the file after the document.");
                dlg.DismissWithCancelButton();
            });
            OkDialog(exportAnnotationsDlg, exportAnnotationsDlg.Close);
        }

        /// <summary>
        /// File > Import > Annotations starts where Export Annotations writes, so a round trip stays in one place.
        /// </summary>
        private void TestImportAnnotations()
        {
            RunLongNativeDlg<NativeOpenFileDialog>(SkylineWindow.ShowImportAnnotationsDlg, dlg =>
            {
                AssertNativeDlgFolder(_documentDir, dlg,
                    @"Import Annotations did not start in the document folder.");
                dlg.DismissWithCancelButton();
            });
        }

        /// <summary>
        /// The Document Grid's Export button goes through the same view context, and starts in the same place.
        /// </summary>
        private void TestDocumentGridExport()
        {
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.DataboundGridControl.ChooseView(
                Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            WaitForCondition(() => documentGrid.IsComplete);
            var navBar = documentGrid.NavBar;
            RunLongNativeDlg<NativeSaveFileDialog>(navBar.ShowExportReportFileDialog, dlg =>
            {
                AssertNativeDlgFolder(_documentDir, dlg,
                    @"The Document Grid export did not start in the document folder.");
                dlg.DismissWithCancelButton();
            });
            RunUI(() => SkylineWindow.ShowDocumentGrid(false));
        }

        /// <summary>
        /// A document that has never been saved has no folder to prefer, so the export falls back to
        /// ActiveDirectory - the folder of the last document opened or saved, which is a guess at where the
        /// user is working. Nothing here writes ActiveDirectory, so it never becomes a remembered export
        /// folder. Left unset instead, the dialog would open wherever a Skyline file dialog was last used,
        /// which is the folder the Document Grid export above cancelled out of.
        /// </summary>
        private void TestUnsavedDocument()
        {
            string documentFilePath = null;
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
                Settings.Default.ActiveDirectory = _otherDir;
                documentFilePath = SkylineWindow.DocumentFilePath;
            });
            Assert.IsNull(documentFilePath, @"The new document is saved, so no fallback would be needed.");

            var exportReportDlg = ShowExportReportDlg();
            RunLongNativeDlg<NativeSaveFileDialog>(exportReportDlg.OkDialog, dlg =>
            {
                AssertNativeDlgFolder(_otherDir, dlg,
                    @"Export Report from an unsaved document did not fall back to the active directory.");
                dlg.DismissWithCancelButton();
            });
            OkDialog(exportReportDlg, exportReportDlg.CancelClick);
        }
    }
}
