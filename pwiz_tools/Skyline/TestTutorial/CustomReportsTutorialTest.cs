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

using System;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Skyline Custom Reports and Results Grid
    /// </summary>
    // ReSharper disable LocalizableElement
    [TestClass]
    public class CustomReportsTutorialTest : AbstractFunctionalTest
    {
        const string customReportName = "Overview";

        [TestMethod]
        public void TestCustomReportsTutorial()
        {
            // Set true to look at tutorial screenshots.
            // IsPauseForScreenShots = true;

            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/CustomReports-2_5.pdf";

            TestFilesZipPaths = new[]
            {
                @"https://skyline.gs.washington.edu/tutorials/CustomReports.zip",
                @"TestTutorial\CustomReportsViews.zip"
            };
            RunFunctionalTest();
        }

        private new TestFilesDir TestFilesDir
        {
            get { return TestFilesDirs[0]; }
        }

        protected override void DoTest()
        {
            // Data Overview, p. 2
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath(@"CustomReports\Study7_example.sky")));
                // Not L10N
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

            DoLiveReportsTest();
        }

        protected void DoLiveReportsTest()
        {
            DoCreatingASimpleReport();
            DoExportingReportDataToAFile();
            DoSharingManagingModifyingReportTemplates();
            DoQualityControlSummaryReports();
            DoResultsGridView();
        }


        protected void DoCreatingASimpleReport()
        {
            // Creating a Simple Custom Report, p. 3
            var exportReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            PauseForScreenShot<ExportLiveReportDlg>("Export Report form", 3);

            // p. 4
            var editReportListDlg = ShowDialog<ManageViewsForm>(exportReportDlg.EditList);
            var viewEditor = ShowDialog<ViewEditor>(editReportListDlg.AddView);
            RunUI(() => viewEditor.ViewName = customReportName);
            PauseForScreenShot<ViewEditor.ChooseColumnsView>("Edit Report form", 4);

            // p. 5
            RunUI(() =>
            {
                viewEditor.ChooseColumnsTab.ExpandPropertyPath(PropertyPath.Parse("Proteins!*.Peptides!*"), true);
                viewEditor.ChooseColumnsTab.ExpandPropertyPath(PropertyPath.Parse("Replicates!*"), true);
                // Make the view editor bigger so that these expanded nodes can be seen in the next screenshot
                viewEditor.Height = Math.Max(viewEditor.Height, 600);
                Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(PropertyPath.Parse("Proteins!*.Peptides!*.Sequence"))); // Not L10N
                viewEditor.ChooseColumnsTab.AddSelectedColumn();
            });
            PauseForScreenShot<ViewEditor.ChooseColumnsView>("Edit Report form", 5);

            // p. 6
            RunUI(() =>
            {
                var columnsToAdd = new[]
                { 
                    // Not L10N
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.IsotopeLabelType"),
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value.BestRetentionTime"),
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value.TotalArea"),
                };
                foreach (var id in columnsToAdd)
                {
                    Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(id), "Unable to select {0}", id);
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                }
                Assert.AreEqual(4, viewEditor.ChooseColumnsTab.ColumnCount);
                viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>().First().SetPivotReplicate(true);
            });
            PauseForScreenShot<ViewEditor.ChooseColumnsView>("Edit Report form", 7);
            // p. 7
            {
                var previewReportDlg = ShowDialog<DocumentGridForm>(viewEditor.ShowPreview);
                WaitForConditionUI(() => previewReportDlg.IsComplete);
                RunUI(() =>
                {
                    Assert.AreEqual(20 + (TestSmallMolecules ? 1 : 0), previewReportDlg.RowCount);
                    Assert.AreEqual(58, previewReportDlg.ColumnCount);
                });
                PauseForScreenShot<DocumentGridForm>("Preview form", 8);

                OkDialog(previewReportDlg, previewReportDlg.Close);
            }

            // p. 8
            OkDialog(viewEditor, viewEditor.OkDialog);
            PauseForScreenShot<ManageViewsForm>("Edit Reports form", 9);

            OkDialog(editReportListDlg, editReportListDlg.OkDialog);
            PauseForScreenShot<ExportLiveReportDlg>("Export Report form", 9);

            OkDialog(exportReportDlg, exportReportDlg.CancelClick);
        }

        protected void DoExportingReportDataToAFile()
        {
            // Exporting Report Data to a File, p. 9
            RunDlg<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog, exportReportDlg0 =>
            {
                exportReportDlg0.ReportName = customReportName; // Not L10N
                exportReportDlg0.OkDialog(TestFilesDir.GetTestPath("Overview_Study7_example.csv"), TextUtil.SEPARATOR_CSV); // Not L10N
            });
        }

        protected void DoSharingManagingModifyingReportTemplates()
        {
            // Sharing Report Templates, p. 9
            var exportReportDlg1 = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);

            // p. 10
            {
                var manageViewsForm = ShowDialog<ManageViewsForm>(exportReportDlg1.EditList);
                PauseForScreenShot<ManageViewsForm>("Save Report Definitions form", 11);

                RunUI(() =>
                {
                    manageViewsForm.SelectView(customReportName);
                    manageViewsForm.ExportViews(TestFilesDir.GetTestPath(@"CustomReports\Overview.skyr"));
                }); // Not L10N
                
                OkDialog(manageViewsForm, manageViewsForm.Close); // Not L10N
            }

            // Managing Report Templayes in Skyline, p. 10
            var editReportListDlg0 = ShowDialog<ManageViewsForm>(exportReportDlg1.EditList);
            RunUI(() => editReportListDlg0.SelectView(customReportName));
            PauseForScreenShot<EditListDlg<SettingsListBase<ReportOrViewSpec>, ReportOrViewSpec>>("Edit Reports form", 12);   // p. 11

            RunUI(() =>
            {
                // TODO
//                editReportListDlg0.MoveItemDown();
//                editReportListDlg0.MoveItemDown();
//                var listReportSpecs = new List<ReportOrViewSpec>(editReportListDlg0.GetAllEdited());
//                Assert.AreEqual(3, listReportSpecs.IndexOf(spec => spec.Name == customReportName)); // Not L10N
//                editReportListDlg0.MoveItemUp();
//                editReportListDlg0.MoveItemUp();
//                editReportListDlg0.MoveItemUp();
//                listReportSpecs = new List<ReportOrViewSpec>(editReportListDlg0.GetAllEdited());
//                Assert.AreEqual(0, listReportSpecs.IndexOf(spec => spec.Name == customReportName)); // Not L10N
                editReportListDlg0.Remove(false);
            });
            OkDialog(editReportListDlg0, editReportListDlg0.OkDialog);
            PauseForScreenShot<ExportLiveReportDlg>("Export Report form", 13);

            {
                var manageViewsForm = ShowDialog<ManageViewsForm>(exportReportDlg1.EditList);

                RunUI(() =>
                    manageViewsForm.ImportViews(TestFilesDir.GetTestPath(@"CustomReports\Overview.skyr"))
                );
                OkDialog(manageViewsForm, manageViewsForm.Close);
                RunUI(()=>{exportReportDlg1.ReportName = customReportName;});
            }
            var previewDlg = ShowDialog<DocumentGridForm>(exportReportDlg1.ShowPreview);
            var expectedRows = 20 + (TestSmallMolecules ? 1 : 0);
            WaitForCondition(() => previewDlg.RowCount == expectedRows);
            Assert.AreEqual(expectedRows, previewDlg.RowCount);
            Assert.AreEqual(58, previewDlg.ColumnCount);
            RunUI(previewDlg.Close);

            // Modifying Existing Report Templates, p. 13
            var editReportListDlg1 = ShowDialog<ManageViewsForm>(exportReportDlg1.EditList);
            RunUI(() => editReportListDlg1.SelectView(customReportName)); // Not L10N
            var viewEditor = ShowDialog<ViewEditor>(editReportListDlg1.CopyView);
            PauseForScreenShot<ViewEditor.ChooseColumnsView>("Edit Report form", 14);

            RunUI(() =>
            {
                viewEditor.ViewName = "Study 7"; // Not L10N
                // Not L10N
                var columnsToAdd = new[]
                                       {
                                           // Not L10N
                                           PropertyPath.Parse("Replicates!*.Files!*.FileName"),
                                           PropertyPath.Parse("Replicates!*.Files!*.SampleName"),
                                           PropertyPath.Parse("Replicates!*.Name"),
                                           PropertyPath.Parse("Proteins!*.Name"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.AverageMeasuredRetentionTime"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Results!*.Value.PeptideRetentionTime"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Results!*.Value.RatioToStandard"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Charge"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Mz"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Transitions!*.ProductCharge"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Transitions!*.ProductMz"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Transitions!*.FragmentIon"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value.MaxFwhm"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value.MinStartTime"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value.MaxEndTime"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Transitions!*.Results!*.Value.RetentionTime"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Transitions!*.Results!*.Value.Fwhm"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Transitions!*.Results!*.Value.StartTime"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Transitions!*.Results!*.Value.EndTime"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Transitions!*.Results!*.Value.Area"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Transitions!*.Results!*.Value.Height"),
                                           PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Transitions!*.Results!*.Value.UserSetPeak"),
                                       };
                foreach (var id in columnsToAdd)
                {
                    Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(id), "Unable to select {0}", id);
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                }
                var pivotWidget = viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>().First();
                pivotWidget.SetPivotReplicate(false);
                viewEditor.Height = Math.Max(viewEditor.Height, 610);
            });
            PauseForScreenShot<ViewEditor.ChooseColumnsView>("Edit Report form expanded to show selected columns", 16);

            int columnCount = 0;
            int rowCount = 0;
            {
                var previewReportDlg = ShowDialog<DocumentGridForm>(viewEditor.ShowPreview);
                WaitForCondition(() => previewReportDlg.ColumnCount > 0);
                RunUI(() =>
                {
                    columnCount = previewReportDlg.ColumnCount;
                    rowCount = previewReportDlg.RowCount;
                });
                OkDialog(previewReportDlg, previewReportDlg.Close);
            }
            RunUI(() => viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>().First().SetPivotIsotopeLabel(true));
            {
                var previewReportDlg = ShowDialog<DocumentGridForm>(viewEditor.ShowPreview);
                WaitForCondition(() => previewReportDlg.ColumnCount > 0);
                RunUI(() =>
                {
                    Assert.IsTrue(previewReportDlg.ColumnCount > columnCount);
                    Assert.AreEqual((rowCount / 2) + (TestSmallMolecules ? 1 : 0), previewReportDlg.RowCount);
                });
                PauseForScreenShot<DocumentGridForm>("Adjust the scrollbar so that the first displayed column is \"light Height\" and the last displayed column is \"heavy Product Mz\"", 17);
                OkDialog(previewReportDlg, previewReportDlg.Close);
            }
            RunUI(() =>
            {
                viewEditor.ChooseColumnsTab.RemoveColumn(PropertyPath.Parse("IsotopeLabelType")); // Not L10N
                viewEditor.OkDialog();
                editReportListDlg1.OkDialog();
            });
            PauseForScreenShot<ExportLiveReportDlg>("Export Report form", 18);

            OkDialog(exportReportDlg1, exportReportDlg1.CancelClick);
        }
        protected void DoQualityControlSummaryReports()
        {
            // Quality Control Summary Reports, p. 18
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath(@"CustomReports\study9pilot.sky")); // Not L10N
                SkylineWindow.ExpandPeptides();
            });
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            RestoreViewOnScreen(19);
            var documentGridForm = FindOpenForm<DocumentGridForm>();
            var manageViewsForm = ShowDialog<ManageViewsForm>(documentGridForm.ManageViews);
            RunUI(() =>
                manageViewsForm.ImportViews(TestFilesDir.GetTestPath(@"CustomReports\Summary_stats.skyr"))
            );
            PauseForScreenShot<ManageViewsForm>("Manage Views form", 19);
            OkDialog(manageViewsForm, manageViewsForm.Close);
            PauseForScreenShot<DocumentGridForm>("Click the views dropdown and highlight 'Summary_stats'", 20);

            RunUI(() => documentGridForm.ChooseView("Summary Statistics"));
            WaitForConditionUI(() => documentGridForm.IsComplete);
            PauseForScreenShot<DocumentGridForm>("Document Grid with summary statistics", 20);

            var viewEditor = ShowDialog<ViewEditor>(documentGridForm.NavBar.CustomizeView);
            RunUI(() => Assert.AreEqual(11, viewEditor.ChooseColumnsTab.ColumnCount));
            RunUI(() =>
            {
                int indexCvTotalArea =
                    viewEditor.ChooseColumnsTab.ColumnNames.ToList().IndexOf(GetLocalizedCaption("CvTotalArea"));
                Assert.IsFalse(indexCvTotalArea < 0, "{0} < 0", indexCvTotalArea);
                viewEditor.ChooseColumnsTab.ActivateColumn(indexCvTotalArea);
            });
            PauseForScreenShot<ViewEditor.ChooseColumnsView>("Customize View form", 21);
            RunUI(()=>viewEditor.TabControl.SelectTab(1));
            RunUI(() =>
            {
                viewEditor.FilterTab.AddSelectedColumn();
                Assert.IsTrue(viewEditor.FilterTab.SetFilterOperation(0, FilterOperations.OP_IS_GREATER_THAN));
                viewEditor.FilterTab.SetFilterOperand(0, ".2");
            });
            PauseForScreenShot<ViewEditor.FilterView>("Customize View - Filter tab", 22);
            RunUI(viewEditor.OkDialog);
            PauseForScreenShot<DocumentGridForm>("Document Grid filtered", 23);
            RunUI(documentGridForm.Close);
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findPeptideDlg =>
            {
                findPeptideDlg.SearchString = "INDISHTQSVSAK"; // Not L10N
                findPeptideDlg.FindNext();
                findPeptideDlg.Close();
            });
            PauseForScreenShot("Highlight the menu item 'View>Peak Areas>Replicate Comparison'", 23);
            RunUI(SkylineWindow.ShowPeakAreaReplicateComparison);
            WaitForGraphs();
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas view", 24);
        }

        protected void DoResultsGridView()
        {
            // Results Grid View
            RestoreViewOnScreen(25);
            PauseForScreenShot<LiveResultsGrid>("Take full screen capture of floating windows", 25);
            RestoreViewOnScreen(26);
            PauseForScreenShot("Main window layout", 26);

            // Not understood: WaitForOpenForm occasionally hangs in nightly test runs. Fixed it by calling
            // ShowDialog when LiveResultsGrid cannot be found.
            //var resultsGridForm = WaitForOpenForm<LiveResultsGrid>();
            var resultsGridForm = FindOpenForm<LiveResultsGrid>() ??
                ShowDialog<LiveResultsGrid>(() => SkylineWindow.ShowResultsGrid(true));
            BoundDataGridView resultsGrid = null;
            RunUI(() =>
            {
                resultsGrid = resultsGridForm.DataGridView;
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.SelectedPath = ((SrmTreeNode)SkylineWindow.SequenceTree.SelectedNode.Nodes[0]).Path;
            });
            WaitForGraphs();
            PauseForScreenShot("Results Grid view subsection", 27);

            RunUI(() =>
            {
                var precursorNoteColumn =
                    resultsGrid.Columns.Cast<DataGridViewColumn>()
                        .First(col => GetLocalizedCaption("PrecursorReplicateNote") == col.HeaderText);
                resultsGrid.CurrentCell = resultsGrid.Rows[0].Cells[precursorNoteColumn.Index];
                resultsGrid.BeginEdit(true);
                // ReSharper disable LocalizableElement
                resultsGrid.EditingControl.Text = "Low signal";   // Not L10N
                // ReSharper restore LocalizableElement
                resultsGrid.EndEdit();
                resultsGrid.CurrentCell = resultsGrid.Rows[1].Cells[resultsGrid.CurrentCell.ColumnIndex];
            });
            WaitForGraphs();
            RunUI(() => SkylineWindow.SelectedResultsIndex = 1);
            WaitForGraphs();

            RunDlg<ViewEditor>(resultsGridForm.NavBar.CustomizeView, resultsGridViewEditor =>
            {
                // TODO: Update tutorial instructions with new name.
                resultsGridViewEditor.ViewName = "NewResultsGridView";
                var chooseColumnTab = resultsGridViewEditor.ChooseColumnsTab;
                foreach (
                    var column in
                        new[]
                        {
                            PropertyPath.Parse("MinStartTime"), PropertyPath.Parse("MaxEndTime"),
                            PropertyPath.Parse("LibraryDotProduct"), PropertyPath.Parse("TotalBackground"),
                            PropertyPath.Parse("TotalAreaRatio")
                        })
                {
                    Assert.IsTrue(chooseColumnTab.ColumnNames.Contains(GetLocalizedCaption(column.Name)));
                    Assert.IsTrue(chooseColumnTab.TrySelect(column), "Unable to select {0}", column);
                    chooseColumnTab.RemoveColumn(column);
                    Assert.IsFalse(chooseColumnTab.ColumnNames.Contains(GetLocalizedCaption(column.Name)));
                }
                resultsGridViewEditor.OkDialog();
            });
            PauseForScreenShot("Results grid with fewer columns (missing?)");  // No longer in tutorial?

            RunUI(() => SkylineWindow.SelectedNode.Expand());

            // Custom Annotations, p. 25
            var chooseAnnotationsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(chooseAnnotationsDlg.EditAnnotationList);
            var defineAnnotationDlg = ShowDialog<DefineAnnotationDlg>(editListDlg.AddItem);
            RunUI(() =>
            {
                defineAnnotationDlg.AnnotationName = "Tailing"; // Not L10N
                defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.true_false;
                defineAnnotationDlg.AnnotationTargets = AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.precursor_result);
            });
            PauseForScreenShot<DefineAnnotationDlg>("Define Annotation form", 28);

            OkDialog(defineAnnotationDlg, defineAnnotationDlg.OkDialog);
            OkDialog(editListDlg, editListDlg.OkDialog);
            RunUI(() => chooseAnnotationsDlg.AnnotationsCheckedListBox.SetItemChecked(0, true));
            PauseForScreenShot<DocumentSettingsDlg>("Annotation Settings form", 29);   // p. 26

            OkDialog(chooseAnnotationsDlg, chooseAnnotationsDlg.OkDialog);

            FindNode((564.7746).ToString(LocalizationHelper.CurrentCulture) + "++");
            var liveResultsGrid = FindOpenForm<LiveResultsGrid>();
            ViewEditor viewEditor = ShowDialog<ViewEditor>(liveResultsGrid.NavBar.CustomizeView);
            RunUI(() =>
            {
                viewEditor.ChooseColumnsTab.ActivateColumn(2);
                Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(
                    PropertyPath.Root.Property(AnnotationDef.ANNOTATION_PREFIX + "Tailing")));
                viewEditor.ChooseColumnsTab.AddSelectedColumn();
            });
            PauseForScreenShot<ViewEditor.ChooseColumnsView>("Customize View form showing Tailing annotation checked", 30);
            OkDialog(viewEditor, viewEditor.OkDialog);
            PauseForScreenShot("Main window with Tailing column added to Results Grid");   // p. 27
        }
        private string GetLocalizedCaption(string caption)
        {
            return SkylineDataSchema.GetLocalizedSchemaLocalizer().LookupColumnCaption(new ColumnCaption(caption));
        }
    }
}
