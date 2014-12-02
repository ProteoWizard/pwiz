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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
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
        [TestMethod]
        public void TestReportSummary()
        {
            TestFilesZip = @"TestFunctional\ReportSummaryTest.zip";
            RunFunctionalTest();
        }

        //[TestMethod]
        public void TestReportSummaryWithOldReports()
        {
            RunWithOldReports(TestReportSummary);
        }

        private const string DOCUMENT_NAME = "160109_Mix1_calcurve.sky";

        /// <summary>
        /// Test Skyline document sharing with libraries.
        /// </summary>
        protected override void DoTest()
        {
            if (IsEnableLiveReports)
            {
                DoLiveReportsTest();
            }
            else
            {
                DoCustomReportsTest();
            }
        }

        // ReSharper disable AccessToModifiedClosure
        private void DoLiveReportsTest()
        {
            // Open the .sky file
            string documentPath = TestFilesDir.GetTestPath(DOCUMENT_NAME);
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            var exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(()=>exportLiveReportDlg.SetUseInvariantLanguage(true));
            var editReportList = ShowDialog<EditListDlg<SettingsListBase<ReportOrViewSpec>, ReportOrViewSpec>>(exportLiveReportDlg.EditList);
            var viewEditor = ShowDialog<ViewEditor>(editReportList.AddItem);

            // Simple protein name report
            AddColumns(viewEditor, PropertyPath.Parse("Proteins!*.Name"));
            CheckPreview(viewEditor, (preview, document) =>
            {
                Assert.AreEqual(document.MoleculeGroupCount, preview.RowCount);
                var columnHeaderNames = new List<string>(preview.ColumnHeaderNames);
                Assert.AreEqual(1, columnHeaderNames.Count);
                Assert.AreEqual("ProteinName", columnHeaderNames[0]); // Not L10N
            });

            // Add precursor information
            AddColumns(viewEditor, PropertyPath.Parse("Proteins!*.Peptides!*.Sequence"),
                PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Charge"));
            CheckPreview(viewEditor, (preview, document) =>
            {
                Assert.AreEqual(document.MoleculeTransitionGroupCount, preview.RowCount);
                CollectionAssert.AreEqual(viewEditor.ChooseColumnsTab.ColumnNames, preview.ColumnHeaderNames);
            });

            // Add precursor results and results summary information
            AddColumns(viewEditor, PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.ResultSummary.BestRetentionTime.Mean"),
                PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.ResultSummary.BestRetentionTime.Cv"),
                PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value.BestRetentionTime"));
            CheckPreview(viewEditor, (preview, document) =>
            {
                int expectedRows = document.PeptideTransitionGroupCount *
                                   document.Settings.MeasuredResults.Chromatograms.Count +
                                   (TestSmallMolecules ? 1 : 0);
                Assert.AreEqual(expectedRows, preview.RowCount);
                CollectionAssert.AreEqual(viewEditor.ChooseColumnsTab.ColumnNames, preview.ColumnHeaderNames);
            });

            // Pivot by replicate
            RunUI(() => PivotReplicateAndIsotopeLabelWidget.SetPivotReplicate(viewEditor, true));
            string[] precursorColumnNames =
                CheckPreview(viewEditor, (preview, document) =>
                {
                    Assert.AreEqual(document.MoleculeTransitionGroupCount, preview.RowCount);
                    VerifyPivotedColumns(document, preview.ColumnHeaderNames, new List<string>(viewEditor.ChooseColumnsTab.ColumnNames), "BestRetentionTime"); // Not L10N
                });

            const string precursorReportName = "Precursor RT Summary";
            RunUI(() => viewEditor.ViewName = precursorReportName);
            OkDialog(viewEditor, viewEditor.OkDialog);

            viewEditor = ShowDialog<ViewEditor>(editReportList.CopyItem);

            // Add transition results summary column
            AddColumns(viewEditor, PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Transitions!*.ResultSummary.Area.Cv"));
            // Not L10N
            CheckPreview(viewEditor, (preview, document) =>
            {
                Assert.AreEqual(document.MoleculeTransitionCount, preview.RowCount);
                VerifyPivotedColumns(document, preview.ColumnHeaderNames, viewEditor.ChooseColumnsTab.ColumnNames.ToList(), "BestRetentionTime"); // Not L10N
            });

            // Add transition results column
            AddColumns(viewEditor, PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Transitions!*.Results!*.Value.Area"));
            // Not L10N
            CheckPreview(viewEditor, (preview, document) =>
            {
                Assert.AreEqual(document.MoleculeTransitionCount, preview.RowCount);
                VerifyPivotedColumns(document, preview.ColumnHeaderNames, viewEditor.ChooseColumnsTab.ColumnNames.ToList(), "BestRetentionTime", "Area"); // Not L10N
            });

            // Turn off pivot
            RunUI(() => PivotReplicateAndIsotopeLabelWidget.SetPivotReplicate(viewEditor, false));
            string[] transitionColumnNames =
                CheckPreview(viewEditor, (preview, document) =>
                {
                    int replicateCount = document.Settings.MeasuredResults.Chromatograms.Count;
                    Assert.AreEqual(document.PeptideTransitionCount * replicateCount + (TestSmallMolecules ? 2 : 0), preview.RowCount);
                    
                    CollectionAssert.AreEqual(viewEditor.ChooseColumnsTab.ColumnNames, preview.ColumnHeaderNames);
                });

            const string transitionReportName = "Transition RT-Area Summary";
            RunUI(() => viewEditor.ViewName = transitionReportName);
            OkDialog(viewEditor, viewEditor.OkDialog);

            OkDialog(editReportList, editReportList.OkDialog);

            // Save report templates to .skyr file
            var shareReports = ShowDialog<ShareListDlg<ReportOrViewSpecList, ReportOrViewSpec>>(exportLiveReportDlg.ShowShare);
            RunUI(() => shareReports.ChosenNames = new[] { precursorReportName, transitionReportName });
            string reportTemplateName = TestFilesDir.GetTestPath("TestReports.skyr");
            OkDialog(shareReports, () => shareReports.OkDialog(reportTemplateName));

            // Remove the in-memory reports
            editReportList = ShowDialog<EditListDlg<SettingsListBase<ReportOrViewSpec>, ReportOrViewSpec>>(exportLiveReportDlg.EditList);
            RunUI(() =>
            {
                editReportList.SelectItem(precursorReportName);
                editReportList.RemoveItem();
                editReportList.SelectItem(transitionReportName);
                editReportList.RemoveItem();
            });
            OkDialog(editReportList, editReportList.OkDialog);

            // Import the saved reports
            RunUI(() => exportLiveReportDlg.Import(reportTemplateName));

            // Export the reports to files
            var docFinal = SkylineWindow.Document;
            RunUI(() => exportLiveReportDlg.ReportName = precursorReportName);
            string precursorReport = TestFilesDir.GetTestPath("PrecursorRTSummary.csv");
            OkDialog(exportLiveReportDlg, () => exportLiveReportDlg.OkDialog(precursorReport, ','));
            VerifyReportFile(precursorReport, docFinal.MoleculeTransitionGroupCount, precursorColumnNames, true);

            exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);

            RunUI(() =>
            {
                exportLiveReportDlg.SetUseInvariantLanguage(true);
                exportLiveReportDlg.ReportName = transitionReportName;
            });
            string transitionReport = TestFilesDir.GetTestPath("TransitionRTAreaSummary.csv");
            OkDialog(exportLiveReportDlg, () => exportLiveReportDlg.OkDialog(transitionReport, ','));
            VerifyReportFile(transitionReport,
                docFinal.PeptideTransitionCount * docFinal.Settings.MeasuredResults.Chromatograms.Count + (TestSmallMolecules ? 2 : 0),
                transitionColumnNames, true);
        }


        private void DoCustomReportsTest()
        {
            // Open the .sky file
            string documentPath = TestFilesDir.GetTestPath(DOCUMENT_NAME);
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            var exportReportDlg = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);
            var editReportListDlg = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg.EditList);
            var pivotReportDlg = ShowDialog<PivotReportDlg>(editReportListDlg.AddItem);

            // Simple protein name report
            AddColumns(pivotReportDlg, new Identifier("ProteinName")); // Not L10N
            CheckPreview(pivotReportDlg, (preview, document) =>
                             {
                                 Assert.AreEqual(document.PeptideGroupCount, preview.RowCount);
                                 var columnHeaderNames = new List<string>(preview.ColumnHeaderNames);
                                 Assert.AreEqual(1, columnHeaderNames.Count);
                                 Assert.AreEqual("ProteinName", columnHeaderNames[0]); // Not L10N
                             });

            // Add precursor information
            AddColumns(pivotReportDlg, new Identifier("Peptides", "Sequence"), // Not L10N
                       new Identifier("Peptides", "Precursors", "Charge"));
            CheckPreview(pivotReportDlg, (preview, document) =>
                             {
                                 Assert.AreEqual(document.PeptideTransitionGroupCount, preview.RowCount);
                                 VerifyNoPivotedColumns(preview, new List<string>(pivotReportDlg.ColumnNames));
                             });

            // Add precursor results and results summary information
            AddColumns(pivotReportDlg, new Identifier("Peptides", "Precursors", "PrecursorResultsSummary", "MeanBestRetentionTime"), // Not L10N
                       new Identifier("Peptides", "Precursors", "PrecursorResultsSummary", "CvBestRetentionTime"),
                       new Identifier("Peptides", "Precursors", "PrecursorResults", "BestRetentionTime"));
            CheckPreview(pivotReportDlg, (preview, document) =>
                             {
                                 int expectedRows = document.PeptideTransitionGroupCount*
                                                    document.Settings.MeasuredResults.Chromatograms.Count;
                                 Assert.AreEqual(expectedRows, preview.RowCount);
                                 VerifyNoPivotedColumns(preview, new List<string>(pivotReportDlg.ColumnNames));
                             });

            // Pivot by replicate
            RunUI(() => pivotReportDlg.PivotReplicate = true);
            string[] precursorColumnNames =
                CheckPreview(pivotReportDlg, (preview, document) =>
                             {
                                 Assert.AreEqual(document.PeptideTransitionGroupCount, preview.RowCount);
                                 VerifyPivotedColumns(document, preview.ColumnHeaderNames, new List<string>(pivotReportDlg.ColumnNames), "BestRetentionTime"); // Not L10N
                             });

            const string precursorReportName = "Precursor RT Summary";
            RunUI(() => pivotReportDlg.ReportName = precursorReportName);
            OkDialog(pivotReportDlg, pivotReportDlg.OkDialog);

            pivotReportDlg = ShowDialog<PivotReportDlg>(editReportListDlg.CopyItem);

            // Add transition results summary column
            AddColumns(pivotReportDlg, new Identifier("Peptides", "Precursors", "Transitions", "TransitionResultsSummary", "CvArea")); // Not L10N
            CheckPreview(pivotReportDlg, (preview, document) =>
                             {
                                 Assert.AreEqual(document.PeptideTransitionCount, preview.RowCount);
                                 VerifyPivotedColumns(document, preview.ColumnHeaderNames, new List<string>(pivotReportDlg.ColumnNames), "BestRetentionTime"); // Not L10N
                             });

            // Add transition results column
            AddColumns(pivotReportDlg, new Identifier("Peptides", "Precursors", "Transitions", "TransitionResults", "Area")); // Not L10N
            CheckPreview(pivotReportDlg, (preview, document) =>
                             {
                                 Assert.AreEqual(document.PeptideTransitionCount, preview.RowCount);
                                 VerifyPivotedColumns(document, preview.ColumnHeaderNames, new List<string>(pivotReportDlg.ColumnNames), "BestRetentionTime", "Area"); // Not L10N
                             });

            // Turn off pivot
            RunUI(() => pivotReportDlg.PivotReplicate = false);
            string[] transitionColumnNames =
                CheckPreview(pivotReportDlg, (preview, document) =>
                             {
                                 int replicateCount = document.Settings.MeasuredResults.Chromatograms.Count;
                                 Assert.AreEqual(document.PeptideTransitionCount*replicateCount, preview.RowCount);
                                 VerifyNoPivotedColumns(preview, new List<string>(pivotReportDlg.ColumnNames));
                             });

            const string transitionReportName = "Transition RT-Area Summary";
            RunUI(() => pivotReportDlg.ReportName = transitionReportName);
            OkDialog(pivotReportDlg, pivotReportDlg.OkDialog);

            OkDialog(editReportListDlg, editReportListDlg.OkDialog);

            // Save report templates to .skyr file
            var shareReports = ShowDialog<ShareListDlg<ReportSpecList, ReportSpec>>(exportReportDlg.ShowShare);
            RunUI(() => shareReports.ChosenNames = new[] {precursorReportName, transitionReportName});
            string reportTemplateName = TestFilesDir.GetTestPath("TestReports.skyr");
            OkDialog(shareReports, () => shareReports.OkDialog(reportTemplateName));

            // Remove the in-memory reports
            editReportListDlg = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg.EditList);
            RunUI(() =>
                      {
                          editReportListDlg.SelectItem(precursorReportName);
                          editReportListDlg.RemoveItem();
                          editReportListDlg.SelectItem(transitionReportName);
                          editReportListDlg.RemoveItem();
                      });
            OkDialog(editReportListDlg, editReportListDlg.OkDialog);

            // Import the saved reports
            RunUI(() => exportReportDlg.Import(reportTemplateName));

            // Export the reports to files
            var docFinal = SkylineWindow.Document;
            RunUI(() => exportReportDlg.ReportName = precursorReportName);
            string precursorReport = TestFilesDir.GetTestPath("PrecursorRTSummary.csv");
            OkDialog(exportReportDlg, () => exportReportDlg.OkDialog(precursorReport, ','));
            VerifyReportFile(precursorReport, docFinal.PeptideTransitionGroupCount, precursorColumnNames);

            exportReportDlg = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);

            RunUI(() => exportReportDlg.ReportName = transitionReportName);
            string transitionReport = TestFilesDir.GetTestPath("TransitionRTAreaSummary.csv");
            OkDialog(exportReportDlg, () => exportReportDlg.OkDialog(transitionReport, ','));
            VerifyReportFile(transitionReport,
                             docFinal.PeptideTransitionCount*docFinal.Settings.MeasuredResults.Chromatograms.Count,
                             transitionColumnNames);
        }

        private void VerifyReportFile(string fileName, int rowCount, IList<string> columnNames, bool invariantFormat = false)
        {
            var formatProvider = invariantFormat ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
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
                        Assert.IsTrue(FastaSequence.IsSequence(value) || (TestSmallMolecules && value.StartsWith("zzz")));
                    else if (columnName.StartsWith("Cv"))
                    {
                        Assert.IsTrue(TestSmallMolecules || value.EndsWith("%"));
                        Assert.IsTrue(TestSmallMolecules || double.Parse(value.Substring(0, value.Length - 1), NumberStyles.Float, formatProvider) > 0);
                    }
                    else if (!columnName.Equals("ProteinName"))
                    {
                        double valueParsed;
                        if (!double.TryParse(value, NumberStyles.Float, formatProvider, out valueParsed))
                        {
                            if (!(TestSmallMolecules && value==TextUtil.EXCEL_NA))
                                Assert.Fail("Failed parsing {0} as a double", value);
                        }
                        else
                        {
                            Assert.IsTrue(valueParsed > 0);
                        }
                    }
                }
            }
        }

        private void VerifyNoPivotedColumns(PreviewReportDlg preview, List<string> columnNames)
        {
            var columnHeaderNames = new List<string>(preview.ColumnHeaderNames);
            Assert.AreEqual(columnNames.Count, columnHeaderNames.Count);
            Assert.IsTrue(ArrayUtil.EqualsDeep(columnNames, columnHeaderNames));
        }

        private void VerifyPivotedColumns(SrmDocument document,
            IEnumerable<string> columnHeaderNames, List<string> columnNames, params string[] pivotNames)
        {
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

        private void AddColumns(PivotReportDlg pivotReportDlg, params Identifier[] columnIds)
        {
            RunUI(() =>
                      {
                          foreach (var columnId in columnIds)
                          {
                              pivotReportDlg.Select(columnId);
                              pivotReportDlg.AddSelectedColumn();
                          }
                      });
        }

        private void AddColumns(ViewEditor viewEditor, params PropertyPath[] propertyPaths)
        {
            RunUI(() =>
            {
                foreach (var propertyPath in propertyPaths)
                {
                    Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(propertyPath), "Unable to select {0}",
                        propertyPath);
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                }
            });
        }

        private string[] CheckPreview(PivotReportDlg pivotReportDlg, Action<PreviewReportDlg, SrmDocument> checkPreview)
        {
            string[] columnNames = null;
            var dlg = ShowDialog<PreviewReportDlg>(pivotReportDlg.ShowPreview);
            RunUI(() =>
            {
                checkPreview(dlg, SkylineWindow.DocumentUI);
                columnNames = dlg.ColumnHeaderNames.ToArray();
            });
            OkDialog(dlg, dlg.Close);
            return columnNames;
        }

        private string[] CheckPreview(ViewEditor viewEditor, Action<DocumentGridForm, SrmDocument> checkPreview)
        {
            string[] columnNames = null;
            var dlg = ShowDialog<DocumentGridForm>(viewEditor.ShowPreview);
            WaitForConditionUI(() => dlg.IsComplete);
            RunUI(() =>
            {
                checkPreview(dlg, SkylineWindow.DocumentUI);
                columnNames = dlg.ColumnHeaderNames;
            });
            OkDialog(dlg, dlg.Close);
            return columnNames;
        }
    }
}