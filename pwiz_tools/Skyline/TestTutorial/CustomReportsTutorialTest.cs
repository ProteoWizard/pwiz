/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Skyline Custom Reports and Results Grid
    /// </summary>
    [TestClass]
    public class CustomReportsTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestCustomReportsTutorial()
        {
            TestFilesZip = @"https://skyline.gs.washington.edu/tutorials/CustomReports.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Skyline Custom Reports and Results Grid

            // Data Overview, p. 2
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath(@"CustomReports\Study7_example.sky")));
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findPeptideDlg =>
            {
                findPeptideDlg.SearchString = "HGFLPR";
                findPeptideDlg.FindNext();
                findPeptideDlg.Close();
            });
            RunUI(() =>
            {
                Assert.AreEqual("HGFLPR", SkylineWindow.SequenceTree.SelectedNode.Text);
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });
            WaitForCondition(() => !SkylineWindow.GraphPeakArea.IsHidden);
            RunUI(() => SkylineWindow.ShowGraphPeakArea(false));
            WaitForCondition(() => SkylineWindow.GraphPeakArea.IsHidden);

            // Creating a Simple Custom Report, p. 3
            var exportReportDlg = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);
            var editReportListDlg = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg.EditList);
            var pivotReportDlg = ShowDialog<PivotReportDlg>(editReportListDlg.AddItem);
            RunUI(() =>
            {
                pivotReportDlg.ReportName = "Overview";
                Assert.IsTrue(pivotReportDlg.TrySelect(new Identifier("Peptides", "Sequence")));
                pivotReportDlg.AddSelectedColumn();
                Assert.AreEqual(1, pivotReportDlg.ColumnCount);
                var expectedFields = new[]
                {
                     new Identifier("ProteinName"), new Identifier("ProteinDescription"),
                     new Identifier("ProteinSequence"), new Identifier("ProteinNote"),
                     new Identifier("Results")
                };
                foreach(Identifier id in expectedFields)
                {
                   Assert.IsTrue(pivotReportDlg.TrySelect(id));
                }
                var columnsToAdd = new[] 
                { 
                    new Identifier("Peptides", "Precursors", "IsotopeLabelType"),
                    new Identifier("Peptides", "Precursors", "PrecursorResults", "BestRetentionTime"),
                    new Identifier("Peptides", "Precursors", "PrecursorResults", "TotalArea") 
                };
                foreach(Identifier id in columnsToAdd)
                {
                    Assert.IsTrue(pivotReportDlg.TrySelect(id));
                    pivotReportDlg.AddSelectedColumn();
                }
                Assert.AreEqual(4, pivotReportDlg.ColumnCount);
                pivotReportDlg.PivotReplicate = true;
            });
            RunDlg<PreviewReportDlg>(pivotReportDlg.ShowPreview, previewReportDlg =>
            {
                Assert.AreEqual(20, previewReportDlg.RowCount);
                Assert.AreEqual(58, previewReportDlg.ColumnCount);
                previewReportDlg.OkDialog();
            });
            RunUI(() =>
            {
                pivotReportDlg.OkDialog();
                editReportListDlg.OkDialog();
                exportReportDlg.CancelClick();
            });
            WaitForClosedForm(exportReportDlg);

            // Exporting Report Data to a File, p. 9
            RunDlg<ExportReportDlg>(SkylineWindow.ShowExportReportDialog, exportReportDlg0 =>
            {
                exportReportDlg0.ReportName = "Overview";
                exportReportDlg0.OkDialog(TestFilesDir.GetTestPath("Overview_Study7_example.csv"), ',');
            });
           
            // Sharing Report Templates, p. 9
            var exportReportDlg1 = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunDlg<ShareListDlg<ReportSpecList, ReportSpec>>(exportReportDlg1.ShowShare, shareListDlg =>
            {
                shareListDlg.ChosenNames = new[] { "Overview" };
                shareListDlg.OkDialog(TestFilesDir.GetTestPath(@"CustomReports\Overview.skyr"));
            });

            // Managing Report Templayes in Skyline, p. 10
            var editReportListDlg0 = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg1.EditList);
            RunUI(() =>
            {
                editReportListDlg0.SelectItem("Overview");
                editReportListDlg0.MoveItemDown();
                editReportListDlg0.MoveItemDown();
                var listReportSpecs = new List<ReportSpec>(editReportListDlg0.GetAllEdited());
                Assert.AreEqual(listReportSpecs.Count - 1,
                              listReportSpecs.IndexOf(spec => spec.Name == "Overview"));
                editReportListDlg0.MoveItemUp();
                editReportListDlg0.MoveItemUp();
                editReportListDlg0.MoveItemUp();
                listReportSpecs = new List<ReportSpec>(editReportListDlg0.GetAllEdited());
                Assert.AreEqual(0, listReportSpecs.IndexOf(spec => spec.Name == "Overview"));
                editReportListDlg0.RemoveItem();
                editReportListDlg0.OkDialog();
            });
            WaitForClosedForm(editReportListDlg0);
            RunUI(() =>
            {
                exportReportDlg1.Import(TestFilesDir.GetTestPath(@"CustomReports\Overview.skyr"));
                exportReportDlg1.ReportName = "Overview";
            });
            RunDlg<PreviewReportDlg>(exportReportDlg1.ShowPreview, previewReportDlg =>
            {
                Assert.AreEqual(20, previewReportDlg.RowCount);
                Assert.AreEqual(58, previewReportDlg.ColumnCount);
                previewReportDlg.OkDialog();
            });

            // Modifying Existing Report Templates, p. 13
            var editReportListDlg1 = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg1.EditList);
            RunUI(() => editReportListDlg1.SelectItem(@"CustomReports\Overview"));
            var pivotReportDlg0 = ShowDialog<PivotReportDlg>(editReportListDlg1.CopyItem);
            RunUI(() =>
            {
                pivotReportDlg0.ReportName = "Study 7";
                var columnsToAdd = new[] 
                { 
                    new Identifier("ProteinName"),
                    new Identifier("Results", "FileName"),
                    new Identifier("Results", "SampleName"),
                    new Identifier("Results", "ReplicateName"),
                    new Identifier("Peptides", "AverageMeasuredRetentionTime"),
                    new Identifier("Peptides", "PeptideResults", "PeptideRetentionTime"),
                    new Identifier("Peptides", "PeptideResults", "RatioToStandard"),
                    new Identifier("Peptides", "Precursors", "Charge"),
                    new Identifier("Peptides", "Precursors", "Mz"),
                    new Identifier("Peptides", "Precursors", "Transitions", "ProductCharge"),
                    new Identifier("Peptides", "Precursors", "Transitions", "ProductMz"),
                    new Identifier("Peptides", "Precursors", "Transitions", "FragmentIon"),
                    new Identifier("Peptides", "Precursors", "PrecursorResults", "MaxFwhm"),
                    new Identifier("Peptides", "Precursors", "PrecursorResults", "MinStartTime"),
                    new Identifier("Peptides", "Precursors", "PrecursorResults", "MaxEndTime"),
                    new Identifier("Peptides", "Precursors", "Transitions", "TransitionResults", "RetentionTime"),
                    new Identifier("Peptides", "Precursors", "Transitions", "TransitionResults", "Fwhm"),
                    new Identifier("Peptides", "Precursors", "Transitions", "TransitionResults", "StartTime"),
                    new Identifier("Peptides", "Precursors", "Transitions", "TransitionResults", "EndTime"),
                    new Identifier("Peptides", "Precursors", "Transitions", "TransitionResults", "Area"),
                    new Identifier("Peptides", "Precursors", "Transitions", "TransitionResults", "Height"),
                    new Identifier("Peptides", "Precursors", "Transitions", "TransitionResults", "UserSetPeak")
                };
                foreach (Identifier id in columnsToAdd)
                {
                    Assert.IsTrue(pivotReportDlg0.TrySelect(id));
                    pivotReportDlg0.AddSelectedColumn();
                }
                pivotReportDlg0.PivotReplicate = false;
            });
            int columnCount = 0;
            int rowCount = 0;
            RunDlg<PreviewReportDlg>(pivotReportDlg0.ShowPreview, previewReportDlg =>
            {
                columnCount = previewReportDlg.ColumnCount;
                rowCount = previewReportDlg.RowCount;
                previewReportDlg.OkDialog();
            });
            RunUI(() => pivotReportDlg0.PivotIsotopeLabelType = true);
            RunDlg<PreviewReportDlg>(pivotReportDlg0.ShowPreview, previewReportDlg =>
            {
                Assert.IsTrue(previewReportDlg.ColumnCount > columnCount);
                Assert.AreEqual(rowCount / 2, previewReportDlg.RowCount);
                previewReportDlg.OkDialog();
            });
            RunUI(() =>
            {
                pivotReportDlg0.RemoveColumn("ProteinName");
                pivotReportDlg0.OkDialog();
                editReportListDlg1.OkDialog();
                exportReportDlg1.CancelClick();
            });
            WaitForClosedForm(exportReportDlg1);

            // Quality Control Summary Reports, p. 18
            RunUI(() =>
                      {
                          SkylineWindow.OpenFile(TestFilesDir.GetTestPath(@"CustomReports\study9pilot.sky"));
                          SkylineWindow.ExpandPeptides();
                      });
            var exportReportDlg2 = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(() => exportReportDlg2.Import(TestFilesDir.GetTestPath(@"CustomReports\Summary_stats.skyr")));
            var editReportListDlg2 = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg2.EditList);
            RunUI(() => editReportListDlg2.SelectItem("Summary Statistics"));
            var pivotReportDlg1 = ShowDialog<PivotReportDlg>(editReportListDlg2.EditItem);
            RunUI(() => Assert.AreEqual(11, pivotReportDlg1.ColumnCount));
            RunDlg<PreviewReportDlg>(pivotReportDlg1.ShowPreview, previewReportDlg => previewReportDlg.OkDialog());
            RunUI(() =>
            {
                pivotReportDlg1.OkDialog();
                editReportListDlg2.OkDialog();
                exportReportDlg2.CancelClick();
            });
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findPeptideDlg =>
            {
                findPeptideDlg.SearchString = "INDISHTQSVSAK";
                findPeptideDlg.FindNext();
                findPeptideDlg.Close();
            });
            RunUI(SkylineWindow.ShowPeakAreaReplicateComparison);
            WaitForGraphs();

            // Results Grid View, p. 22
            RunUI(() => SkylineWindow.ShowGraphPeakArea(false));
            var resultsGridForm = ShowDialog<ResultsGridForm>(() => SkylineWindow.ShowResultsGrid(true));
            ResultsGrid resultsGrid = null;
            RunUI(() =>
            {
                resultsGrid = resultsGridForm.ResultsGrid;
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.SelectedPath = ((SrmTreeNode)SkylineWindow.SequenceTree.SelectedNode.Nodes[0]).Path;
            });
            WaitForGraphs();
            RunUI(() => resultsGrid.CurrentCell = resultsGrid.Rows[1].Cells[resultsGrid.CurrentCell.ColumnIndex]);
            WaitForGraphs();
            RunUI(() => SkylineWindow.SelectedResultsIndex = 1);
            WaitForGraphs();
            RunDlg<ColumnChooser>(resultsGridForm.ChooseColumns, columnChooser =>
            {
                columnChooser.SetChecked(new Dictionary<string, bool>
                                             {
                                                 {"Min Start Time", false},
                                                 {"Max End Time", false},
                                                 {"Library Dot Product", false}
                                             });
                columnChooser.DialogResult = DialogResult.OK;
            });
            RunUI(() => SkylineWindow.SelectedNode.Expand());

            // Custom Annotations, p. 25
            var chooseAnnotationsDlg = ShowDialog<ChooseAnnotationsDlg>(SkylineWindow.ShowAnnotationsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(chooseAnnotationsDlg.EditList);
            RunDlg<DefineAnnotationDlg>(editListDlg.AddItem, defineAnnotationDlg =>
            {
                defineAnnotationDlg.AnnotationName = "Trailing";
                defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.true_false;
                defineAnnotationDlg.AnnotationTargets = AnnotationDef.AnnotationTarget.precursor_result;
                defineAnnotationDlg.OkDialog();
            });
            RunUI(editListDlg.OkDialog);
            WaitForClosedForm(editListDlg);
            RunUI(() => 
            {
                chooseAnnotationsDlg.AnnotationsCheckedListBox.SetItemChecked(0, true);
                chooseAnnotationsDlg.OkDialog();
            });
            WaitForClosedForm(chooseAnnotationsDlg);
        }
    }
}
