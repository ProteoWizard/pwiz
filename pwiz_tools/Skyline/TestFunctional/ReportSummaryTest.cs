/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test of summary reports.
    /// </summary>
    [TestClass]
    public class ReportSummaryTest : AbstractFunctionalTest
    {
        private ExportReportDlg ExportReport { get; set; }
        private EditListDlg<SettingsListBase<ReportSpec>, ReportSpec> EditReportList { get; set; }
        private PivotReportDlg PivotReport { get; set; }

        [TestMethod]
        public void TestReportSummary()
        {
            TestFilesZip = @"TestFunctional\ReportSummaryTest.zip";
            RunFunctionalTest();
        }

        private const string DOCUMENT_NAME = "160109_Mix1_calcurve.sky";

        /// <summary>
        /// Test Skyline document sharing with libraries.
        /// </summary>
        protected override void DoTest()
        {
            // Open the .sky file
            string documentPath = TestFilesDir.GetTestPath(DOCUMENT_NAME);
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            ExportReport = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);
            EditReportList = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(ExportReport.EditList);
            PivotReport = ShowDialog<PivotReportDlg>(EditReportList.AddItem);

            // Simple protein name report
            AddColumns(new Identifier("ProteinName"));
            CheckPreview((preview, document) =>
                             {
                                 Assert.AreEqual(document.PeptideGroupCount, preview.RowCount);
                                 var columnHeaderNames = new List<string>(preview.ColumnHeaderNames);
                                 Assert.AreEqual(1, columnHeaderNames.Count);
                                 Assert.AreEqual("ProteinName", columnHeaderNames[0]);
                             });

            // Add precursor information
            AddColumns(new Identifier("Peptides", "Sequence"),
                       new Identifier("Peptides", "Precursors", "Charge"));
            CheckPreview((preview, document) =>
                             {
                                 Assert.AreEqual(document.TransitionGroupCount, preview.RowCount);
                                 VerifyNoPivotedColumns(preview);
                             });

            // Add precursor results and results summary information
            AddColumns(new Identifier("Peptides", "Precursors", "PrecursorResultsSummary", "MeanBestRetentionTime"),
                       new Identifier("Peptides", "Precursors", "PrecursorResultsSummary", "CvBestRetentionTime"),
                       new Identifier("Peptides", "Precursors", "PrecursorResults", "BestRetentionTime"));
            CheckPreview((preview, document) =>
                             {
                                 int expectedRows = document.TransitionGroupCount*
                                                    document.Settings.MeasuredResults.Chromatograms.Count;
                                 Assert.AreEqual(expectedRows, preview.RowCount);
                                 VerifyNoPivotedColumns(preview);
                             });

            // Pivot by replicate
            RunUI(() => PivotReport.PivotReplicate = true);
            string[] precursorColumnNames =
                CheckPreview((preview, document) =>
                             {
                                 Assert.AreEqual(document.TransitionGroupCount, preview.RowCount);
                                 VerifyPivotedColumns(document, preview.ColumnHeaderNames, "BestRetentionTime");
                             });

            const string precursorReportName = "Precursor RT Summary";
            RunUI(() => PivotReport.ReportName = precursorReportName);
            OkDialog(PivotReport, PivotReport.OkDialog);

            PivotReport = ShowDialog<PivotReportDlg>(EditReportList.CopyItem);

            // Add transition results summary column
            AddColumns(new Identifier("Peptides", "Precursors", "Transitions", "TransitionResultsSummary", "CvArea"));
            CheckPreview((preview, document) =>
                             {
                                 Assert.AreEqual(document.TransitionCount, preview.RowCount);
                                 VerifyPivotedColumns(document, preview.ColumnHeaderNames, "BestRetentionTime");
                             });

            // Add transition results column
            AddColumns(new Identifier("Peptides", "Precursors", "Transitions", "TransitionResults", "Area"));
            CheckPreview((preview, document) =>
                             {
                                 Assert.AreEqual(document.TransitionCount, preview.RowCount);
                                 VerifyPivotedColumns(document, preview.ColumnHeaderNames, "BestRetentionTime", "Area");
                             });

            // Turn off pivot
            RunUI(() => PivotReport.PivotReplicate = false);
            string[] transitionColumnNames =
                CheckPreview((preview, document) =>
                             {
                                 int replicateCount = document.Settings.MeasuredResults.Chromatograms.Count;
                                 Assert.AreEqual(document.TransitionCount*replicateCount, preview.RowCount);
                                 VerifyNoPivotedColumns(preview);
                             });

            const string transitionReportName = "Transition RT-Area Summary";
            RunUI(() => PivotReport.ReportName = transitionReportName);
            OkDialog(PivotReport, PivotReport.OkDialog);

            OkDialog(EditReportList, EditReportList.OkDialog);

            // Save report templates to .skyr file
            var shareReports = ShowDialog<ShareListDlg<ReportSpecList, ReportSpec>>(ExportReport.ShowShare);
            RunUI(() => shareReports.ChosenNames = new[] {precursorReportName, transitionReportName});
            string reportTemplateName = TestFilesDir.GetTestPath("TestReports.skyr");
            OkDialog(shareReports, () => shareReports.OkDialog(reportTemplateName));

            // Remove the in-memory reports
            EditReportList = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(ExportReport.EditList);
            RunUI(() =>
                      {
                          EditReportList.SelectItem(precursorReportName);
                          EditReportList.RemoveItem();
                          EditReportList.SelectItem(transitionReportName);
                          EditReportList.RemoveItem();
                      });
            OkDialog(EditReportList, EditReportList.OkDialog);

            // Import the saved reports
            RunUI(() => ExportReport.Import(reportTemplateName));

            // Export the reports to files
            var docFinal = SkylineWindow.Document;
            RunUI(() => ExportReport.ReportName = precursorReportName);
            string precursorReport = TestFilesDir.GetTestPath("PrecursorRTSummary.csv");
            OkDialog(ExportReport, () => ExportReport.OkDialog(precursorReport, ','));
            VerifyReportFile(precursorReport, docFinal.TransitionGroupCount, precursorColumnNames);

            ExportReport = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);

            RunUI(() => ExportReport.ReportName = transitionReportName);
            string transitionReport = TestFilesDir.GetTestPath("TransitionRTAreaSummary.csv");
            OkDialog(ExportReport, () => ExportReport.OkDialog(transitionReport, ','));
            VerifyReportFile(transitionReport,
                             docFinal.TransitionCount*docFinal.Settings.MeasuredResults.Chromatograms.Count,
                             transitionColumnNames);
        }

        private static void VerifyReportFile(string fileName, int rowCount, IList<string> columnNames)
        {
            string[] lines = File.ReadAllLines(fileName);
            Assert.AreEqual(rowCount, lines.Length - 1);
            string[] columnHeaderNames = lines[0].ParseCsvFields();
            Assert.IsTrue(ArrayUtil.EqualsDeep(columnNames, columnHeaderNames));

            for (int i = 1; i < lines.Length; i++)
            {
                string[] values = lines[i].ParseCsvFields();
                for (int j = 0; j < columnHeaderNames.Length; j++)
                {
                    string columnName = columnNames[j];
                    string value = values[j];

                    if (columnName.Equals("PeptideSequence"))
                        Assert.IsTrue(FastaSequence.IsSequence(value));
                    else if (columnName.StartsWith("Cv"))
                    {
                        Assert.IsTrue(value.EndsWith("%"));
                        Assert.IsTrue(double.Parse(value.Substring(0, value.Length - 1)) > 0);
                    }
                    else if (!columnName.Equals("ProteinName"))
                    {
                        double valueParsed;
                        if (!double.TryParse(value, out valueParsed))
                            Assert.Fail(string.Format("Failed parsing {0} as a double", value));
                        Assert.IsTrue(valueParsed > 0);
                    }
                }
            }
        }

        private void VerifyNoPivotedColumns(PreviewReportDlg preview)
        {
            var columnHeaderNames = new List<string>(preview.ColumnHeaderNames);
            var columnNames = new List<string>(PivotReport.ColumnNames);
            Assert.AreEqual(columnNames.Count, columnHeaderNames.Count);
            Assert.IsTrue(ArrayUtil.EqualsDeep(columnNames, columnHeaderNames));
        }

        private void VerifyPivotedColumns(SrmDocument document,
            IEnumerable<string> columnHeaderNames, params string[] pivotNames)
        {
            var columnNames = new List<string>(PivotReport.ColumnNames);
            foreach (var pivotName in pivotNames)
                columnNames.Remove(pivotName);

            var listColumnHeaderNames = new List<string>(columnHeaderNames);
            int replicateCount = document.Settings.MeasuredResults.Chromatograms.Count;
            Assert.AreEqual(columnNames.Count + pivotNames.Length*replicateCount, listColumnHeaderNames.Count);
            foreach (var chromSet in document.Settings.MeasuredResults.Chromatograms)
            {
                foreach (var pivotName in pivotNames)
                    columnNames.Add(chromSet.Name + " " + pivotName);
            }
            Assert.IsTrue(ArrayUtil.EqualsDeep(columnNames, listColumnHeaderNames));
        }

        private void AddColumns(params Identifier[] columnIds)
        {
            RunUI(() =>
                      {
                          foreach (var columnId in columnIds)
                          {
                              PivotReport.Select(columnId);
                              PivotReport.AddSelectedColumn();
                          }
                      });
        }

        private string[] CheckPreview(Action<PreviewReportDlg, SrmDocument> checkPreview)
        {
            string[] columnNames = null;

            RunDlg<PreviewReportDlg>(PivotReport.ShowPreview, dlg =>
            {
                checkPreview(dlg, SkylineWindow.DocumentUI);
                columnNames = dlg.ColumnHeaderNames.ToArray();
                dlg.OkDialog();
            });

            return columnNames;
        }
    }
}