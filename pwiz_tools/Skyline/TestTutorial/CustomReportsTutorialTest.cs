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
using System.Globalization;
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
using pwiz.Skyline.Util.Extensions;
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
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath(@"CustomReports\Study7_example.sky"))); // Not L10N
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findPeptideDlg =>
            {
                findPeptideDlg.SearchString = "HGFLPR"; // Not L10N
                findPeptideDlg.FindNext();
                findPeptideDlg.Close();
            });
            RunUI(() =>
            {
                Assert.AreEqual("HGFLPR", SkylineWindow.SequenceTree.SelectedNode.Text); // Not L10N
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });
            WaitForCondition(() => !SkylineWindow.GraphPeakArea.IsHidden);
            RunUI(() => SkylineWindow.ShowGraphPeakArea(false));
            WaitForCondition(() => SkylineWindow.GraphPeakArea.IsHidden);

            // Creating a Simple Custom Report, p. 3
            var exportReportDlg = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);
            PauseForScreenShot();

            // p. 4
            var editReportListDlg = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg.EditList);
            var pivotReportDlg = ShowDialog<PivotReportDlg>(editReportListDlg.AddItem);
            const string customReportName = "Overview";
            RunUI(() => pivotReportDlg.ReportName = customReportName);
            PauseForScreenShot();

            // p. 5
            RunUI(() =>
            {
                Assert.IsTrue(pivotReportDlg.TrySelect(new Identifier("Peptides", "Sequence"))); // Not L10N
                pivotReportDlg.AddSelectedColumn();
                Assert.AreEqual(1, pivotReportDlg.ColumnCount);
                var expectedFields = new[]
                {
                    // Not L10N
                     new Identifier("ProteinName"), new Identifier("ProteinDescription"), 
                     new Identifier("ProteinSequence"), new Identifier("ProteinNote"),
                     new Identifier("Results")
                };
                foreach (Identifier id in expectedFields)
                {
                    Assert.IsTrue(pivotReportDlg.TrySelect(id));
                }
            });
            PauseForScreenShot();

            // p. 6
            RunUI(() =>
            {
                var columnsToAdd = new[]
                { 
                    // Not L10N
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
            PauseForScreenShot();

            // p. 7
            {
                var previewReportDlg = ShowDialog<PreviewReportDlg>(pivotReportDlg.ShowPreview);
                RunUI(() =>
                {
                    Assert.AreEqual(20, previewReportDlg.RowCount);
                    Assert.AreEqual(58, previewReportDlg.ColumnCount);
                });
                PauseForScreenShot();

                OkDialog(previewReportDlg, previewReportDlg.OkDialog);
            }

            // p. 8
            OkDialog(pivotReportDlg, pivotReportDlg.OkDialog);
            PauseForScreenShot();

            OkDialog(editReportListDlg, editReportListDlg.OkDialog);
            PauseForScreenShot();

            OkDialog(exportReportDlg, exportReportDlg.CancelClick);

            // Exporting Report Data to a File, p. 9
            RunDlg<ExportReportDlg>(SkylineWindow.ShowExportReportDialog, exportReportDlg0 =>
            {
                exportReportDlg0.ReportName = customReportName; // Not L10N
                exportReportDlg0.OkDialog(TestFilesDir.GetTestPath("Overview_Study7_example.csv"), TextUtil.SEPARATOR_CSV); // Not L10N
            });
           
            // Sharing Report Templates, p. 9
            var exportReportDlg1 = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);

            // p. 10
            {
                var shareListDlg = ShowDialog<ShareListDlg<ReportSpecList, ReportSpec>>(exportReportDlg1.ShowShare);
                PauseForScreenShot();

                RunUI(() => shareListDlg.ChosenNames = new[] { customReportName }); // Not L10N
                OkDialog(shareListDlg, () => shareListDlg.OkDialog(TestFilesDir.GetTestPath(@"CustomReports\Overview.skyr"))); // Not L10N
            }

            // Managing Report Templayes in Skyline, p. 10
            var editReportListDlg0 = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg1.EditList);
            RunUI(() => editReportListDlg0.SelectItem(customReportName));
            PauseForScreenShot();   // p. 11

            RunUI(() =>
            {
                editReportListDlg0.MoveItemDown();
                editReportListDlg0.MoveItemDown();
                var listReportSpecs = new List<ReportSpec>(editReportListDlg0.GetAllEdited());
                Assert.AreEqual(3, listReportSpecs.IndexOf(spec => spec.Name == customReportName)); // Not L10N
                editReportListDlg0.MoveItemUp();
                editReportListDlg0.MoveItemUp();
                editReportListDlg0.MoveItemUp();
                listReportSpecs = new List<ReportSpec>(editReportListDlg0.GetAllEdited());
                Assert.AreEqual(0, listReportSpecs.IndexOf(spec => spec.Name == customReportName)); // Not L10N
                editReportListDlg0.RemoveItem();
            });
            OkDialog(editReportListDlg0, editReportListDlg0.OkDialog);
            PauseForScreenShot();   // p. 12

            RunUI(() =>
            {
                exportReportDlg1.Import(TestFilesDir.GetTestPath(@"CustomReports\Overview.skyr")); // Not L10N
                exportReportDlg1.ReportName = customReportName; // Not L10N
            });
            RunDlg<PreviewReportDlg>(exportReportDlg1.ShowPreview, previewReportDlg =>
            {
                Assert.AreEqual(20, previewReportDlg.RowCount);
                Assert.AreEqual(58, previewReportDlg.ColumnCount);
                previewReportDlg.OkDialog();
            });

            // Modifying Existing Report Templates, p. 13
            var editReportListDlg1 = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg1.EditList);
            RunUI(() => editReportListDlg1.SelectItem(customReportName)); // Not L10N
            var pivotReportDlg0 = ShowDialog<PivotReportDlg>(editReportListDlg1.CopyItem);
            PauseForScreenShot();

            RunUI(() =>
            {
                pivotReportDlg0.ReportName = "Study 7"; // Not L10N
                // Not L10N
                var columnsToAdd = new[]
                                       {
                                           // Not L10N
                                           new Identifier("Results", "FileName"),
                                           new Identifier("Results", "SampleName"),
                                           new Identifier("Results", "ReplicateName"),
                                           new Identifier("ProteinName"),
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
            PauseForScreenShot();   // p. 15

            int columnCount = 0;
            int rowCount = 0;
            {
                var previewReportDlg = ShowDialog<PreviewReportDlg>(pivotReportDlg0.ShowPreview);
                RunUI(() =>
                {
                    columnCount = previewReportDlg.ColumnCount;
                    rowCount = previewReportDlg.RowCount;
                });
                OkDialog(previewReportDlg, previewReportDlg.OkDialog);
            }
            RunUI(() => pivotReportDlg0.PivotIsotopeLabelType = true);
            {
                var previewReportDlg = ShowDialog<PreviewReportDlg>(pivotReportDlg0.ShowPreview);
                RunUI(() =>
                {
                    Assert.IsTrue(previewReportDlg.ColumnCount > columnCount);
                    Assert.AreEqual(rowCount / 2, previewReportDlg.RowCount);
                });
                PauseForScreenShot();
                OkDialog(previewReportDlg, previewReportDlg.OkDialog);
            }
            RunUI(() =>
            {
                pivotReportDlg0.RemoveColumn("IsotopeLabelType"); // Not L10N
                pivotReportDlg0.OkDialog();
                editReportListDlg1.OkDialog();
            });
            PauseForScreenShot();   // p. 17

            OkDialog(exportReportDlg1, exportReportDlg1.CancelClick);

            // Quality Control Summary Reports, p. 18
            RunUI(() =>
                      {
                          SkylineWindow.OpenFile(TestFilesDir.GetTestPath(@"CustomReports\study9pilot.sky")); // Not L10N
                          SkylineWindow.ExpandPeptides();
                      });
            var exportReportDlg2 = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(() => exportReportDlg2.Import(TestFilesDir.GetTestPath(@"CustomReports\Summary_stats.skyr"))); // Not L10N
            PauseForScreenShot();

            var editReportListDlg2 = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg2.EditList);
            RunUI(() => editReportListDlg2.SelectItem("Summary Statistics")); // Not L10N
            var pivotReportDlg1 = ShowDialog<PivotReportDlg>(editReportListDlg2.EditItem);
            RunUI(() => Assert.AreEqual(11, pivotReportDlg1.ColumnCount));
            PauseForScreenShot();   // p. 19

            {
                var previewReportDlg = ShowDialog<PreviewReportDlg>(pivotReportDlg1.ShowPreview);
                PauseForScreenShot();   // p. 20

                OkDialog(previewReportDlg, previewReportDlg.OkDialog);
            }
            RunUI(() =>
            {
                pivotReportDlg1.OkDialog();
                editReportListDlg2.OkDialog();
            });
            OkDialog(exportReportDlg2, exportReportDlg2.CancelClick);
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findPeptideDlg =>
            {
                findPeptideDlg.SearchString = "INDISHTQSVSAK"; // Not L10N
                findPeptideDlg.FindNext();
                findPeptideDlg.Close();
            });
            RunUI(SkylineWindow.ShowPeakAreaReplicateComparison);
            WaitForGraphs();
            PauseForScreenShot();   // p. 21

            // Results Grid View, p. 22
            var resultsGridForm = ShowDialog<ResultsGridForm>(() => SkylineWindow.ShowResultsGrid(true));
            PauseForScreenShot();

            ResultsGrid resultsGrid = null;
            RunUI(() =>
            {
                resultsGrid = resultsGridForm.ResultsGrid;
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.SelectedPath = ((SrmTreeNode)SkylineWindow.SequenceTree.SelectedNode.Nodes[0]).Path;
            });
            WaitForGraphs();
            PauseForScreenShot();   // p. 23

            RunUI(() =>
                      {
                          resultsGrid.CurrentCell = resultsGrid.Rows[0].Cells["PrecursorReplicateNote"];
                          resultsGrid.BeginEdit(true);
                          resultsGrid.EditingControl.Text = "Low signal";
                          resultsGrid.EndEdit();
                          resultsGrid.CurrentCell = resultsGrid.Rows[1].Cells[resultsGrid.CurrentCell.ColumnIndex];
                      });
            WaitForGraphs();
            RunUI(() => SkylineWindow.SelectedResultsIndex = 1);
            WaitForGraphs();

            RunDlg<ColumnChooser>(resultsGridForm.ChooseColumns, columnChooser =>
            {
                columnChooser.SetChecked(new Dictionary<string, bool>
                                             {
                                                 // Not L10N
                                                 {"Min Start Time", false},
                                                 {"Max End Time", false},
                                                 {"Library Dot Product", false},
                                                 {"Total Background", false},
                                                 {"Total Area Ratio", false}
                                             });
                columnChooser.DialogResult = DialogResult.OK;
            });
            PauseForScreenShot();   // p. 24

            RunUI(() => SkylineWindow.SelectedNode.Expand());

            // Custom Annotations, p. 25
            var chooseAnnotationsDlg = ShowDialog<ChooseAnnotationsDlg>(SkylineWindow.ShowAnnotationsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(chooseAnnotationsDlg.EditList);
            var defineAnnotationDlg = ShowDialog<DefineAnnotationDlg>(editListDlg.AddItem);
            RunUI(() =>
            {
                defineAnnotationDlg.AnnotationName = "Tailing"; // Not L10N
                defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.true_false;
                defineAnnotationDlg.AnnotationTargets = AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.precursor_result);
            });
            PauseForScreenShot();   // p. 25

            OkDialog(defineAnnotationDlg, defineAnnotationDlg.OkDialog);
            OkDialog(editListDlg, editListDlg.OkDialog);
            RunUI(() => chooseAnnotationsDlg.AnnotationsCheckedListBox.SetItemChecked(0, true));
            PauseForScreenShot();   // p. 26

            OkDialog(chooseAnnotationsDlg, chooseAnnotationsDlg.OkDialog);

            FindNode((564.7746).ToString(CultureInfo.CurrentCulture) + "++");
            WaitForGraphs();
            RunUI(() =>
                      {
                          var colTailing = resultsGrid.Columns[AnnotationDef.GetColumnName("Tailing")];
                          Assert.IsNotNull(colTailing);
                          Assert.AreEqual(typeof(bool), colTailing.ValueType);
                          var colNote = resultsGrid.Columns["PrecursorReplicateNote"];
                          Assert.IsNotNull(colNote);
                          colTailing.DisplayIndex = colNote.DisplayIndex + 1;
                      });
            PauseForScreenShot();   // p. 27
        }
    }
}
