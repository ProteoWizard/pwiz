/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests exporting from the ExportReportDialog and the DocumentGrid, and tries to cancel
    /// the export by closing the LongWaitDlg.
    /// We can never be 100% sure that it is possible to cancel an operation (the operation might
    /// complete so quickly that the LongWaitDlg never gets shown) so all we can assert is
    /// that if the file was exported, it should be complete.Col
    /// </summary>
    [TestClass]
    public class CancelExportReportTest : AbstractFunctionalTestEx
    {
        const char SEPARATOR_TO_USE = '\t';
        [TestMethod]
        public void TestCancelExportReport()
        {
            TestFilesZip = @"TestFunctional\CancelExportReportTest.zip";
            RunFunctionalTest();
        }

        // ReSharper disable AccessToModifiedClosure
        protected override void DoTest()
        {
            const string reportName = "ChromatogramData";
            string successfulPath = TestFilesDir.GetTestPath("successful.txt");
            string cancellablePath = TestFilesDir.GetTestPath("cancellable.txt");
            OpenDocument(TestFilesDir.GetTestPath("CancelExportReportTest.sky"));
            var exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(()=>exportLiveReportDlg.ReportName = reportName);
           
            Assert.IsTrue(ExportAndMaybeTryToCancel(exportLiveReportDlg, TestFilesDir.GetTestPath("successful.txt"), false));
            Assert.IsTrue(File.Exists(successfulPath));
            RunUI(exportLiveReportDlg.Close);
            exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(() => exportLiveReportDlg.ReportName = reportName);
            bool exported = ExportAndMaybeTryToCancel(exportLiveReportDlg, cancellablePath, true);
            if (exported)
            {
                Assert.IsTrue(File.Exists(cancellablePath));
                AssertEx.FileEquals(successfulPath, cancellablePath);
            }
            else
            {
                Assert.IsFalse(File.Exists(cancellablePath));
                RunUI(exportLiveReportDlg.Close);
            }
            RunUI(()=>SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(()=>documentGrid.DataboundGridControl.ChooseView(reportName));
            WaitForConditionUI(() => documentGrid.IsComplete);
            string fromDocumentGridPath = TestFilesDir.GetTestPath("fromDocumentGrid.txt");
            bool exportedFromDocumentGrid = ExportFromDatagridAndMaybeTryToCancel(documentGrid.DataboundGridControl, fromDocumentGridPath, true);
            if (exportedFromDocumentGrid)
            {
                AssertEx.FileEquals(successfulPath, fromDocumentGridPath);
            }
            else
            {
                Assert.IsFalse(File.Exists(fromDocumentGridPath));
            }
        }
        // ReSharper restore AccessToModifiedClosure

        protected bool ExportAndMaybeTryToCancel(ExportLiveReportDlg exportLiveReportDlg, string filename, bool tryToCancel)
        {
            Assert.AreSame(exportLiveReportDlg, FindOpenForm<ExportLiveReportDlg>());
            bool triedToCancel = RunAndMaybeTryCancel(() => exportLiveReportDlg.OkDialog(filename, SEPARATOR_TO_USE), tryToCancel);
            if (!triedToCancel)
            {
                return true;
            }
            var remainingForm = FindOpenForm<ExportLiveReportDlg>();
            if (remainingForm != null)
            {
                Assert.AreSame(exportLiveReportDlg, remainingForm);
                return remainingForm.DialogResult != DialogResult.None;
            }
            return true;
        }

        protected bool ExportFromDatagridAndMaybeTryToCancel(DataboundGridControl databoundGridControl,
            string filename, bool maybeTryToCancel)
        {
            Assert.IsFalse(File.Exists(filename));
            bool triedToCancel = RunAndMaybeTryCancel(() =>
            {
                var skylineViewContext = (SkylineViewContext)databoundGridControl.NavBar.ViewContext;
                skylineViewContext.ExportToFile(databoundGridControl, databoundGridControl.BindingListSource.ViewInfo, filename,
                    new DsvWriter(SEPARATOR_TO_USE));
            }, true);
            if (!triedToCancel)
            {
                Assert.IsTrue(File.Exists(filename));
            }
            return File.Exists(filename);
        }

        protected bool RunAndMaybeTryCancel(Action action, bool tryToCancel)
        {
            bool finished = false;
            SkylineWindow.BeginInvoke(new Action(() =>
            {
                action();
                finished = true;
            }));
            bool triedToCancel = false;
            while (!finished)
            {
                if (tryToCancel)
                {
                    var longWaitDlg = FindOpenForm<LongWaitDlg>();
                    if (longWaitDlg != null)
                    {
                        RunUI(longWaitDlg.Close);
                        triedToCancel = true;
                    }
                }
                Thread.Sleep(0);
            }
            return triedToCancel;
        }
    }
}
