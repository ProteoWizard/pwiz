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

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class LiveReportsTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLiveReportsTutorial()
        {
            CoverShotName = "LiveReports";
            TestFilesZip =
                @"TestTutorial\LiveReportsTutorial.zip";
            AuditLogList.IgnoreTestChecks = true;
            RunFunctionalTest();
            AuditLogList.IgnoreTestChecks = false;
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_plasma.sky"));
                SkylineWindow.ShowAuditLog();
            });
            var auditLogForm = FindOpenForm<AuditLogForm>();
            Assert.IsNotNull(auditLogForm);
            PauseForScreenShot(auditLogForm);
            RunUI(()=>auditLogForm.EnableAuditLogging(true));
            PauseForScreenShot(auditLogForm);
            OkDialog(auditLogForm, auditLogForm.Close);
            PauseForScreenShot("Status bar", null, ClipSelectionStatus);
            RunUI(() =>
            {
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            ShowReportsDropdown(Resources.SkylineViewContext_GetDocumentGridRowSources_Proteins);
            PauseForScreenShot(documentGrid);
            HideReportsDropdown();
            RunUI(()=>
            {
                documentGrid.DataboundGridControl.ChooseView(
                    ViewGroup.BUILT_IN.Id.ViewName(Resources
                        .SkylineViewContext_GetDocumentGridRowSources_Proteins));
            });
            WaitForCondition(() => documentGrid.IsComplete);
            PauseForScreenShot(documentGrid);
            RunUI(() =>
            {
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[3].Cells[0];
                documentGrid.DataGridView.ClickCurrentCell();
                Assert.AreEqual(SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.MoleculeGroups, 3), SkylineWindow.SelectedPath);
            });
            PauseForScreenShot(SkylineWindow.SequenceTree);
            PauseForScreenShot("Status bar", null, ClipSelectionStatus);
            PauseForScreenShot(documentGrid.NavBar, processShot:bmp=>ClipControl(documentGrid.NavBar, bmp));
            RunUI(()=>
            {
                documentGrid.DataboundGridControl.ChooseView(
                    ViewGroup.BUILT_IN.Id.ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            });
            WaitForCondition(() => documentGrid.IsComplete);
            PauseForScreenShot(documentGrid);
            RunLongDlg<DocumentSettingsDlg>(()=>SkylineWindow.ShowDocumentSettingsDialog(), documentSettingsDlg =>
            {
                RunUI(()=>documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.annotations));
                PauseForScreenShot(documentSettingsDlg);
                RunLongDlg<DefineAnnotationDlg>(documentSettingsDlg.AddAnnotation, defineAnnotationDlg =>
                {
                    PauseForScreenShot(defineAnnotationDlg);
                    RunUI(()=>
                    {
                        defineAnnotationDlg.AnnotationName = "Cohort";
                        defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.value_list;
                        defineAnnotationDlg.Items = new[] { "Healthy", "Diseased" };
                        defineAnnotationDlg.AnnotationTargets =
                            AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate);
                    });
                    PauseForScreenShot(defineAnnotationDlg);
                }, defineAnnotationDlg=>defineAnnotationDlg.OkDialog());
                PauseForScreenShot(documentSettingsDlg);
                RunLongDlg<DefineAnnotationDlg>(documentSettingsDlg.AddAnnotation, defineAnnotationDlg =>
                {
                    RunUI(() =>
                    {
                        defineAnnotationDlg.AnnotationName = "SubjectID";
                        defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.text;
                        defineAnnotationDlg.AnnotationTargets =
                            AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate);
                    });
                    PauseForScreenShot(defineAnnotationDlg);
                }, defineAnnotationDlg => defineAnnotationDlg.OkDialog());
                PauseForScreenShot(documentSettingsDlg);
            }, documentSettingsDlg=>documentSettingsDlg.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(()=>
            {
                documentGrid.Activate();
                var cohortColumn = documentGrid.DataboundGridControl.FindColumn(
                    PropertyPath.Root.Property(AnnotationDef.GetColumnName("Cohort")));
                Assert.IsNotNull(cohortColumn);
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[cohortColumn.Index];
            });
            using (new GridTester(documentGrid.DataGridView).ShowComboBox())
            {
                PauseForScreenShot(documentGrid);
            }
            RunLongDlg<DocumentSettingsDlg>(()=>SkylineWindow.ShowDocumentSettingsDialog(), documentSettingsDlg =>
            {
                RunUI(()=>documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.metadata_rules));
                RunLongDlg<MetadataRuleSetEditor>(documentSettingsDlg.AddMetadataRule, metadataRuleSetEditor =>
                {
                    RunLongDlg<MetadataRuleEditor>(()=>metadataRuleSetEditor.EditRule(0), metadataRuleEditor =>
                    {
                        PauseForScreenShot(metadataRuleEditor);
                        RunUI(() =>
                        {
                            metadataRuleEditor.MetadataRule = metadataRuleEditor.MetadataRule.ChangePattern("D")
                                .ChangeReplacement("Diseased").ChangeTarget(PropertyPath.Root
                                    .Property(nameof(ResultFile.Replicate))
                                    .Property(AnnotationDef.GetColumnName("Cohort")));
                        });
                        PauseForScreenShot(metadataRuleEditor);
                        RunUI(() =>metadataRuleEditor.PreviewGrid.FirstDisplayedScrollingRowIndex = 17);
                        PauseForScreenShot(metadataRuleEditor);

                    }, metadataRuleEditor=>metadataRuleEditor.OkDialog());
                    RunUI(()=>metadataRuleSetEditor.DataGridViewSteps.CurrentCell = metadataRuleSetEditor.DataGridViewSteps.Rows[1].Cells[0]);
                    PauseForScreenShot(metadataRuleSetEditor);
                    RunLongDlg<MetadataRuleEditor>(() => metadataRuleSetEditor.EditRule(1), metadataRuleEditor =>
                    {
                        RunUI(() =>
                        {
                            metadataRuleEditor.MetadataRule = metadataRuleEditor.MetadataRule.ChangePattern("H")
                                .ChangeReplacement("Healthy").ChangeTarget(PropertyPath.Root
                                    .Property(nameof(ResultFile.Replicate))
                                    .Property(AnnotationDef.GetColumnName("Cohort")));
                        });
                        PauseForScreenShot(metadataRuleEditor);
                    }, metadataRuleEditor => metadataRuleEditor.OkDialog());
                    RunUI(() => metadataRuleSetEditor.DataGridViewSteps.CurrentCell = metadataRuleSetEditor.DataGridViewSteps.Rows[2].Cells[0]);
                    PauseForScreenShot(metadataRuleSetEditor);
                    RunLongDlg<MetadataRuleEditor>(()=>metadataRuleSetEditor.EditRule(2), metadataRuleEditor =>
                    {
                        RunUI(() =>
                        {
                            metadataRuleEditor.MetadataRule = metadataRuleEditor.MetadataRule.ChangePattern("(.)_(...)").ChangeReplacement("$1$2").ChangeTarget(PropertyPath.Root
                                .Property(nameof(ResultFile.Replicate))
                                .Property(AnnotationDef.GetColumnName("SubjectID")));
                        });
                        PauseForScreenShot(metadataRuleEditor);
                    }, metadataRuleEditor=>metadataRuleEditor.OkDialog());
                    PauseForScreenShot(metadataRuleSetEditor);
                    RunUI(() =>
                    {
                        metadataRuleSetEditor.RuleName = "Cohort and SubjectID";
                    });
                }, metadataRuleSetEditor => metadataRuleSetEditor.OkDialog());
            }, documentSettingsDlg=>documentSettingsDlg.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            PauseForScreenShot(documentGrid);
            RunLongDlg<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog, documentSettingsDlg =>
            {
                RunUI(() =>
                {
                    documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.lists);
                });
                RunLongDlg<ListDesigner>(documentSettingsDlg.AddList, listDesigner =>
                {
                    PauseForScreenShot(listDesigner);
                    RunUI(() =>
                    {
                        listDesigner.ListName = "Samples";

                    });
                    var gridTester = new GridTester(listDesigner.ListPropertiesGrid);
                    gridTester.SetCellValue(0, 0, "SubjectID");
                    gridTester.SetCellValue(0, 1, ListPropertyType.GetAnnotationTypeName(AnnotationDef.AnnotationType.text));
                    gridTester.SetCellValue(1, 0, "Sex");
                    gridTester.SetCellValue(1, 1, ListPropertyType.GetAnnotationTypeName(AnnotationDef.AnnotationType.text));
                    gridTester.SetCellValue(2, 0, "Weight");
                    gridTester.SetCellValue(2, 1, ListPropertyType.GetAnnotationTypeName(AnnotationDef.AnnotationType.number));
                    gridTester.SetCellValue(3, 0, "Name");
                    gridTester.SetCellValue(3, 1, ListPropertyType.GetAnnotationTypeName(AnnotationDef.AnnotationType.text));
                    RunUI(() =>
                    {
                        listDesigner.ListPropertiesGrid.CurrentCell = listDesigner.ListPropertiesGrid.Rows[4].Cells[0];
                        listDesigner.IdProperty = "SubjectID";
                        listDesigner.DisplayProperty = "Name";
                    });
                    PauseForScreenShot(listDesigner);
                }, listDesigner => listDesigner.OkDialog());
            }, documentSettingsDlg=>documentSettingsDlg.OkDialog());
            var listData = SkylineWindow.Document.Settings.DataSettings.Lists.FirstOrDefault();
            Assert.IsNotNull(listData);
            Assert.AreEqual("SubjectID", listData.ListDef.IdProperty);
            Assert.AreEqual("Name", listData.ListDef.DisplayProperty);
            Assert.AreEqual("SubjectID,Sex,Weight,Name", String.Join(",", listData.ListDef.Properties.Select(p=>p.Name)));
            RunUI(() => SkylineWindow.ShowList("Samples"));
            var listGridForm = FindOpenForm<ListGridForm>();
            Assert.IsNotNull(listGridForm);
            WaitForConditionUI(() => listGridForm.IsComplete && listGridForm.DataGridView.Rows.Count > 0);
            PauseForScreenShot(listGridForm);
            var sampleInfoTsvLines = File.ReadAllLines(TestFilesDir.GetTestPath("SampleInfo.txt")).Skip(1).ToList();
            Assert.AreEqual(14, sampleInfoTsvLines.Count);
            SetClipboardText(TextUtil.LineSeparate(sampleInfoTsvLines));
            RunUI(() =>
            {
                listGridForm.DataGridView.CurrentCell = listGridForm.DataGridView.Rows[0].Cells[0];
                listGridForm.DataGridView.SendPaste();
                Assert.AreEqual(15, listGridForm.RowCount);
            });
            PauseForScreenShot(listGridForm);
            RunLongDlg<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog, documentSettingsDlg =>
            {
                RunUI(()=>
                {
                    documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.annotations);
                });
                RunLongDlg<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(
                    documentSettingsDlg.EditAnnotationList,
                    ediListDlg =>
                    {
                        RunUI(() =>
                        {
                            ediListDlg.SelectItem("SubjectID");
                        });
                        RunLongDlg<DefineAnnotationDlg>(ediListDlg.EditItem, defineAnnotationDlg =>
                        {
                            RunUI(()=>defineAnnotationDlg.ListPropertyType = new ListPropertyType(AnnotationDef.AnnotationType.text, "Samples"));
                            PauseForScreenShot(defineAnnotationDlg);
                        }, defineAnnotationDlg=>defineAnnotationDlg.OkDialog());
                    }, editListDlg => editListDlg.OkDialog());
            }, documentSettingsDlg=>documentSettingsDlg.OkDialog());
            RunUI(()=>
            {
                documentGrid.Activate();
                documentGrid.DataboundGridControl.ChooseView(ViewGroup.BUILT_IN.Id.ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides));
            });
            WaitForCondition(() => documentGrid.IsComplete);
            RunLongDlg<ViewEditor>(documentGrid.DataboundGridControl.NavBar.CustomizeView, viewEditor =>
            {
                RunUI(()=>
                {
                    viewEditor.ChooseColumnsTab.RemoveColumns(4, viewEditor.ChooseColumnsTab.ColumnCount);
                    viewEditor.ChooseColumnsTab.SelectColumn(3);
                    Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(PropertyPath.Root.Property(nameof(SkylineDocument.Proteins))
                        .LookupAllItems().Property(nameof(Protein.Peptides))
                        .LookupAllItems().Property(nameof(Peptide.Precursors))
                        .LookupAllItems().Property(nameof(Precursor.Results))
                        .DictionaryValues().Property(nameof(PrecursorResult.TotalArea))));
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                });
                PauseForScreenShot(viewEditor);
                RunUI(() =>
                {
                    viewEditor.ChooseColumnsTab.MoveColumnsUp(); // Move "Standard Type" to before "Total Area"
                    viewEditor.ChooseColumnsTab.SelectColumn(4); // Select "Total Area" column
                    Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(PropertyPath.Root.Property(nameof(SkylineDocument.Replicates)).LookupAllItems()));
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                });
                PauseForScreenShot(viewEditor);
                RunUI(()=>viewEditor.ViewName = "Peptide Areas");
            }, viewEditor=>viewEditor.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            PauseForScreenShot(documentGrid);
            RunLongDlg<ViewEditor>(documentGrid.DataboundGridControl.NavBar.CustomizeView, viewEditor =>
            {
                PauseForScreenShot(viewEditor);
                RunUI(()=> {
                    var pivotWidget = viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>()
                        .FirstOrDefault();
                    Assert.IsNotNull(pivotWidget);
                    pivotWidget.SetPivotReplicate(true);
                });
            }, viewEditor => viewEditor.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                documentGrid.FloatingPane.FloatingWindow.Width = 1500;
            });
            PauseForScreenShot(documentGrid);
            RunUI(() =>
            {
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[0];
                documentGrid.DataGridView.ClickCurrentCell();
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });
            WaitForGraphs();

            var peakAreaReplicateComparisonGraphSummary = FindGraphSummaryByGraphType<AreaReplicateGraphPane>();
            PauseForScreenShot(peakAreaReplicateComparisonGraphSummary);
            RunLongDlg<ViewEditor>(documentGrid.DataboundGridControl.NavBar.CustomizeView, viewEditor =>
            {
                RunLongDlg<FindColumnDlg>(viewEditor.ShowFindDialog, findColumnDlg =>
                {
                    RunUI(() =>
                    {
                        findColumnDlg.FindText = ColumnCaptions.NormalizedArea;
                    });
                    WaitForConditionUI(findColumnDlg.IsReadyToSearch);
                    RunUI(findColumnDlg.SearchForward);
                }, findColumnDlg=> findColumnDlg.Close());
                PauseForScreenShot(viewEditor);
                RunUI(()=>viewEditor.ChooseColumnsTab.AddSelectedColumn());
                PauseForScreenShot(viewEditor);
            }, viewEditor=>viewEditor.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(()=>documentGrid.Activate());
            PauseForScreenShot(documentGrid);
            RunDlg<ChooseFormatDlg>(
                () => documentGrid.DataboundGridControl.ShowFormatDialog(documentGrid.DataGridView.Columns[4]),
                chooseFormatDlg =>
                {
                    chooseFormatDlg.FormatText = FormatSuggestion.Scientific.FormatString;
                    chooseFormatDlg.DialogResult = DialogResult.OK;
                });
            PauseForScreenShot(documentGrid);

            RunUI(() =>
            {
                var columnCaption = TextUtil.SpaceSeparate("D_102_REP2", ColumnCaptions.NormalizedArea);
                var column = documentGrid.DataGridView.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(col => col.HeaderText == columnCaption);
                Assert.IsNotNull(column, "Unable to find column {0}", columnCaption);
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[9].Cells[column.Index];
            });
            PauseForScreenShot(documentGrid, "Hover over the cell so that the tooltip is displayed");
            RunUI(()=>
            {
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[0];
                documentGrid.DataGridView.ClickCurrentCell();
                documentGrid.DataboundGridControl.ReplicatePivotDataGridView.CurrentCell =
                    documentGrid.DataboundGridControl.ReplicatePivotDataGridView.Rows[0].Cells[2];
                documentGrid.DataboundGridControl.ReplicatePivotDataGridView.ClickCurrentCell();
            });
            PauseForScreenShot(SkylineWindow);
            RunLongDlg<ViewEditor>(documentGrid.DataboundGridControl.NavBar.CustomizeView, viewEditor =>
            {
                RunUI(() =>
                {
                    viewEditor.ChooseColumnsTab.ActivateColumn(6);
                    viewEditor.ActiveAvailableFieldsTree.SelectedNode.Expand();
                    foreach (TreeNode node in viewEditor.ActiveAvailableFieldsTree.SelectedNode.Nodes)
                    {
                        viewEditor.ActiveAvailableFieldsTree.SelectedNode = node;
                        viewEditor.ChooseColumnsTab.AddSelectedColumn();
                    }
                });
            }, viewEditor => viewEditor.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(() => documentGrid.Activate());
            PauseForScreenShot(documentGrid);
            RunDlg<ViewEditor>(documentGrid.DataboundGridControl.NavBar.CustomizeView, viewEditor =>
            {
                var pivotWidget = viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>()
                    .FirstOrDefault();
                Assert.IsNotNull(pivotWidget);
                pivotWidget.SetPivotReplicate(false);
                viewEditor.OkDialog();
            });
            WaitForCondition(() => documentGrid.IsComplete);
            RunDlg<PivotEditor>(()=>documentGrid.DataboundGridControl.NavBar.ShowPivotDialog(true), pivotEditor =>
            {
                SelectPivotEditorColumns(pivotEditor, nameof(ColumnCaptions.NormalizedAreaRaw),
                    nameof(ColumnCaptions.NormalizedAreaStrict));
                Assert.AreEqual(2, pivotEditor.AvailableColumnList.SelectedIndices.Count);
                pivotEditor.SelectAggregateOperation(AggregateOperation.Mean);
                pivotEditor.AddValue();
                pivotEditor.OkDialog();
            });
            PauseForScreenShot(documentGrid);
            RunUI(() =>
            {
                documentGrid.NavBar.GroupButton.DropDown.Closing += DenyMenuClosing;
                documentGrid.NavBar.GroupButton.ShowDropDown();
                var transformMenuItem = documentGrid.NavBar.GroupButton.DropDown.Items
                    .OfType<ToolStripMenuItem>().FirstOrDefault(item =>
                        item.Text == Common.Properties.Resources.NavBar_UpdateGroupTotalDropdown_Transforms);
                Assert.IsNotNull(transformMenuItem);
                transformMenuItem.DropDown.Closing += DenyMenuClosing;
            });
            PauseForScreenShot(documentGrid);

            RunUI(() => {
                var transformMenuItem = documentGrid.NavBar.GroupButton.DropDown.Items
                    .OfType<ToolStripMenuItem>().FirstOrDefault(item =>
                        item.Text == Common.Properties.Resources.NavBar_UpdateGroupTotalDropdown_Transforms);
                Assert.IsNotNull(transformMenuItem);
                transformMenuItem.DropDown.Items[transformMenuItem.DropDown.Items.Count - 1].PerformClick();
                documentGrid.NavBar.GroupButton.DropDown.Closing -= DenyMenuClosing;
            });
            PauseForScreenShot(documentGrid);
            RunLongDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                var ppSubjectID = PropertyPath.Root.Property(nameof(SkylineDocument.Replicates)).LookupAllItems()
                    .Property(AnnotationDef.GetColumnName("SubjectID"));
                RunUI(()=>
                {
                    viewEditor.ChooseColumnsTab.AvailableFieldsTree.SelectColumn(ppSubjectID);
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                });
                PauseForScreenShot(viewEditor);
            }, viewEditor=>viewEditor.OkDialog());
            PauseForScreenShot(documentGrid);
            RunLongDlg<PivotEditor>(()=>documentGrid.NavBar.ShowPivotDialog(true), pivotEditor =>
            {
                PauseForScreenShot(pivotEditor);
                RunUI(()=>
                {
                    SelectPivotEditorColumns(pivotEditor, nameof(ColumnCaptions.Peptide));
                    pivotEditor.AddRowHeader();
                    SelectPivotEditorColumns(pivotEditor, "SubjectID");
                    pivotEditor.AddColumnHeader();
                    SelectPivotEditorColumns(pivotEditor, nameof(ColumnCaptions.NormalizedAreaRaw));
                    pivotEditor.SelectAggregateOperation(AggregateOperation.Cv);
                    pivotEditor.AddValue();
                });
                PauseForScreenShot(pivotEditor);
            }, pivotEditor=>pivotEditor.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            PauseForScreenShot(documentGrid);
            RunUI(() =>
            {
                var captionDrizzleNormalizedArea =
                    TextUtil.SpaceSeparate("Drizzle", AggregateOperation.Cv.QualifyColumnCaption(new ColumnCaption(ColumnCaptions.NormalizedAreaRaw)).GetCaption(SkylineDataSchema.GetLocalizedSchemaLocalizer()));
                var colDrizzleNormalizedArea = documentGrid.DataGridView.Columns.OfType<DataGridViewColumn>()
                    .FirstOrDefault(col => col.HeaderText == captionDrizzleNormalizedArea);
                Assert.IsNotNull(colDrizzleNormalizedArea, "Unable to find column named {0}", captionDrizzleNormalizedArea);

            });
        }

        public Bitmap ClipControl(Control control, Bitmap bmp)
        {
            return CallUI(() =>
            {
                var parentWindowRect = ScreenshotManager.GetFramedWindowBounds(control);
                var controlScreenRect = control.RectangleToScreen(new Rectangle(0, 0, control.Width, control.Height));
                return ClipBitmap(bmp,
                    new Rectangle(controlScreenRect.Left - parentWindowRect.Left,
                        controlScreenRect.Top - parentWindowRect.Top, controlScreenRect.Width,
                        controlScreenRect.Height));
            });
        }

        private void SelectPivotEditorColumns(PivotEditor pivotEditor, params string[] invariantCaptionNames)
        {
            for (int i = 0; i < pivotEditor.AvailableProperties.Count; i++)
            {
                var invariantCaption = pivotEditor.AvailableProperties[i].ColumnCaption
                    .GetCaption(DataSchemaLocalizer.INVARIANT);
                bool selected = invariantCaptionNames.Contains(invariantCaption);
                pivotEditor.AvailableColumnList.Items[i].Selected = selected;
            }
        }
    }
}
