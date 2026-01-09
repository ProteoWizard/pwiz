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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DatabindingResources = pwiz.Skyline.Controls.Databinding.DatabindingResources;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class LiveReportsTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLiveReportsTutorial()
        {
            // Not yet translated
            if (IsTranslationRequired)
                return;

            CoverShotName = "LiveReports";
            TestFilesZipPaths = new []
            {
                "https://skyline.ms/tutorials/LiveReports.zip",
                @"TestTutorial\LiveReportsViews.zip"
            };

            using (new TestTimeProvider(new DateTime(2025, 1, 1, 10, 10, 0, DateTimeKind.Local)))
            using (new AuditLogList.IgnoreTestChecksScope())
                RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            return TestFilesDirs[0].GetTestPath(relativePath);
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(GetTestPath("Rat_plasma.sky"));
                SkylineWindow.ShowAuditLog();
            });
            var auditLogForm = FindOpenForm<AuditLogForm>();
            Assert.IsNotNull(auditLogForm);
            PauseForScreenShot(auditLogForm, "Audit log view empty");
            RunUI(()=>auditLogForm.EnableAuditLogging(true));
            PauseForScreenShot(auditLogForm, "Audit log view populated");
            OkDialog(auditLogForm, auditLogForm.Close);
            PauseForScreenShot("Status bar", null, ClipSelectionStatus);
            RunUI(() =>
            {
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            ShowReportsDropdown(Resources.SkylineViewContext_GetDocumentGridRowSources_Proteins);
            PauseForScreenShot<DocumentGridForm>("Document Grid showing Reports menu");
            HideReportsDropdown();
            RunUI(()=>
            {
                documentGrid.Parent.Parent.Height = 405; // Use 25.1 height which had a smaller menu
                documentGrid.DataboundGridControl.ChooseView(
                    ViewGroup.BUILT_IN.Id.ViewName(Resources
                        .SkylineViewContext_GetDocumentGridRowSources_Proteins));
            });
            WaitForCondition(() => documentGrid.IsComplete);
            PauseForScreenShot<DocumentGridForm>("Document Grid showing Proteins report");
            RunUI(() =>
            {
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[3].Cells[0];
                documentGrid.DataGridView.ClickCurrentCell();
                Assert.AreEqual(SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.MoleculeGroups, 3), SkylineWindow.SelectedPath);
            });
            var windowLocations = HideFloatingWindows();
            FocusDocument();
            PauseForScreenShot(SkylineWindow.SequenceTree, "Targets tree with protein selected");
            PauseForScreenShot("Status bar", null, ClipSelectionStatus);
            RestoreFloatingWindows(windowLocations);
            PauseForScreenShot(documentGrid.NavBar, "Document Grid nav bar", processShot:ClipControl(documentGrid.NavBar));
            RunUI(()=>
            {
                documentGrid.DataboundGridControl.ChooseView(
                    ViewGroup.BUILT_IN.Id.ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            });
            WaitForCondition(() => documentGrid.IsComplete);
            PauseForScreenShot<DocumentGridForm>("Document Grid showing Replicates report");
            RunLongDlg<DocumentSettingsDlg>(()=>SkylineWindow.ShowDocumentSettingsDialog(), documentSettingsDlg =>
            {
                RunUI(()=>documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.annotations));
                PauseForScreenShot(documentSettingsDlg, "Document Settings - Annotations empty");
                RunLongDlg<DefineAnnotationDlg>(documentSettingsDlg.AddAnnotation, defineAnnotationDlg =>
                {
                    PauseForScreenShot(defineAnnotationDlg, "Define Annotation form empty");
                    RunUI(()=>
                    {
                        defineAnnotationDlg.AnnotationName = "Cohort";
                        defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.value_list;
                        defineAnnotationDlg.Items = new[] { "Healthy", "Diseased" };
                        defineAnnotationDlg.AnnotationTargets =
                            AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate);
                    });
                    PauseForScreenShot(defineAnnotationDlg, "Define Annotation form for Cohort annotation");
                }, defineAnnotationDlg=>defineAnnotationDlg.OkDialog());
                PauseForScreenShot(documentSettingsDlg, "Document Settings - Annotations with Cohort annotation");
                RunLongDlg<DefineAnnotationDlg>(documentSettingsDlg.AddAnnotation, defineAnnotationDlg =>
                {
                    RunUI(() =>
                    {
                        defineAnnotationDlg.AnnotationName = "SubjectID";
                        defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.text;
                        defineAnnotationDlg.AnnotationTargets =
                            AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate);
                    });
                    PauseForScreenShot(defineAnnotationDlg, "Define Annotation form for SubjectID annotation");
                }, defineAnnotationDlg => defineAnnotationDlg.OkDialog());
                PauseForScreenShot(documentSettingsDlg, "Document Settings - Annotations with SubjectID annotation");
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
                PauseForScreenShot<DocumentGridForm>("Document Grid showing Cohort dropdown");
            }
            RunLongDlg<DocumentSettingsDlg>(()=>SkylineWindow.ShowDocumentSettingsDialog(), documentSettingsDlg =>
            {
                RunUI(()=>documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.metadata_rules));
                RunLongDlg<MetadataRuleSetEditor>(documentSettingsDlg.AddMetadataRule, metadataRuleSetEditor =>
                {
                    RunLongDlg<MetadataRuleEditor>(()=>metadataRuleSetEditor.EditRule(0), metadataRuleEditor =>
                    {
                        PauseForScreenShot(metadataRuleEditor, "Rule Editor form empty");
                        RunUI(() =>
                        {
                            metadataRuleEditor.MetadataRule = metadataRuleEditor.MetadataRule.ChangePattern("D")
                                .ChangeReplacement("Diseased").ChangeTarget(PropertyPath.Root
                                    .Property(nameof(ResultFile.Replicate))
                                    .Property(AnnotationDef.GetColumnName("Cohort")));
                        });
                        PauseForScreenShot(metadataRuleEditor, "Rule Editor form with Diseased rule");
                        RunUI(() =>metadataRuleEditor.PreviewGrid.FirstDisplayedScrollingRowIndex = 17);
                        PauseForScreenShot(metadataRuleEditor, "Rule Editor form with Diseased rule scrolled");

                    }, metadataRuleEditor=>metadataRuleEditor.OkDialog());
                    RunUI(()=>metadataRuleSetEditor.DataGridViewSteps.CurrentCell = metadataRuleSetEditor.DataGridViewSteps.Rows[1].Cells[0]);
                    PauseForScreenShot(metadataRuleSetEditor, "Rule Set Editor form with Diseased rule");
                    RunLongDlg<MetadataRuleEditor>(() => metadataRuleSetEditor.EditRule(1), metadataRuleEditor =>
                    {
                        RunUI(() =>
                        {
                            metadataRuleEditor.MetadataRule = metadataRuleEditor.MetadataRule.ChangePattern("H")
                                .ChangeReplacement("Healthy").ChangeTarget(PropertyPath.Root
                                    .Property(nameof(ResultFile.Replicate))
                                    .Property(AnnotationDef.GetColumnName("Cohort")));
                        });
                        PauseForScreenShot(metadataRuleEditor, "Rule Editor form with Healthy rule");
                    }, metadataRuleEditor => metadataRuleEditor.OkDialog());
                    RunUI(() => metadataRuleSetEditor.DataGridViewSteps.CurrentCell = metadataRuleSetEditor.DataGridViewSteps.Rows[2].Cells[0]);
                    PauseForScreenShot(metadataRuleSetEditor, "Rule Set Editor form with Diseased and Healthy rules");
                    RunLongDlg<MetadataRuleEditor>(()=>metadataRuleSetEditor.EditRule(2), metadataRuleEditor =>
                    {
                        RunUI(() =>
                        {
                            metadataRuleEditor.MetadataRule = metadataRuleEditor.MetadataRule.ChangePattern("(.)_(...)").ChangeReplacement("$1$2").ChangeTarget(PropertyPath.Root
                                .Property(nameof(ResultFile.Replicate))
                                .Property(AnnotationDef.GetColumnName("SubjectID")));
                        });
                        PauseForScreenShot(metadataRuleEditor, "Rule Editor form with SubjectID rule");
                    }, metadataRuleEditor=>metadataRuleEditor.OkDialog());
                    RunUI(()=>metadataRuleSetEditor.DataGridViewSteps.CurrentCell = metadataRuleSetEditor.DataGridViewSteps.Rows[3].Cells[0]);
                    PauseForScreenShot(metadataRuleSetEditor, "Rule Set Editor form with all 3 rules");
                    RunUI(() =>
                    {
                        metadataRuleSetEditor.RuleName = "Cohort and SubjectID";
                    });
                }, metadataRuleSetEditor => metadataRuleSetEditor.OkDialog());
            }, documentSettingsDlg=>documentSettingsDlg.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            PauseForScreenShot<DocumentGridForm>("Document Grid showing replicate annotations populated by rules");
            RunLongDlg<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog, documentSettingsDlg =>
            {
                RunUI(() =>
                {
                    documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.lists);
                });
                RunLongDlg<ListDesigner>(documentSettingsDlg.AddList, listDesigner =>
                {
                    PauseForScreenShot(listDesigner, "List Designer form empty");
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
                    PauseForScreenShot(listDesigner, "List Designer form showing Samples list");
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
            PauseForScreenShot(listGridForm, "List: Samples grid view");
            var sampleInfoTsvLines = File.ReadAllLines(GetTestPath("SampleInfo.txt")).Skip(1).ToList();
            Assert.AreEqual(14, sampleInfoTsvLines.Count);
            SetClipboardText(TextUtil.LineSeparate(sampleInfoTsvLines));
            RunUI(() =>
            {
                listGridForm.DataGridView.CurrentCell = listGridForm.DataGridView.Rows[0].Cells[0];
            });
            PauseForScreenShot(listGridForm, "List: Samples grid view - with selected cell");
            RunUI(() =>
            {
                listGridForm.DataGridView.SendPaste();
                Assert.AreEqual(15, listGridForm.RowCount);
            });
            PauseForScreenShot(listGridForm, "List: Samples grid view - with pasted values");
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
                            PauseForScreenShot(defineAnnotationDlg, "Define Annotation form with SubjectID as a lookup");
                        }, defineAnnotationDlg=>defineAnnotationDlg.OkDialog());
                    }, editListDlg => editListDlg.OkDialog());
            }, documentSettingsDlg=>documentSettingsDlg.OkDialog());
            RunUI(() =>
            {
                documentGrid.Activate();
                documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates);
            });
            WaitForCondition(() => documentGrid.IsComplete);
            PauseForScreenShot<DocumentGridForm>("Document Grid: Replicates showing SubjectID as rat names");
            RunUI(()=>
            {
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
                PauseForScreenShot<ViewEditor>("Customize Report form with TotalArea above Standard Type");
                RunUI(() =>
                {
                    viewEditor.ChooseColumnsTab.MoveColumnsUp(); // Move "Standard Type" to before "Total Area"
                    viewEditor.ChooseColumnsTab.SelectColumn(4); // Select "Total Area" column
                    Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(PropertyPath.Root.Property(nameof(SkylineDocument.Replicates)).LookupAllItems()));
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                });
                PauseForScreenShot<ViewEditor>("Customize Report form with TotalArea last");
                RunUI(()=>viewEditor.ViewName = "Peptide Areas");
            }, viewEditor=>viewEditor.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            RunUIForScreenShot(() => documentGrid.FloatingPane.FloatingWindow.Width += 100);    // Widen document grid
            PauseForScreenShot<DocumentGridForm>("Document Grid: Peptide Areas");
            RunLongDlg<ViewEditor>(documentGrid.DataboundGridControl.NavBar.CustomizeView, viewEditor =>
            {
                PauseForScreenShot<ViewEditor>("Customize Report form showing 'Peptide Areas' report");
                RunUI(()=> {
                    var pivotWidget = viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>()
                        .FirstOrDefault();
                    Assert.IsNotNull(pivotWidget);
                    pivotWidget.SetPivotReplicate(true);
                });
            }, viewEditor => viewEditor.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            RunUIForScreenShot(() =>
            {
                documentGrid.FloatingPane.FloatingWindow.Width = 1074;
            });
            PauseForScreenShot<DocumentGridForm>("Document Grid: Peptide Areas with Pivot Replicate Name");
            RunUI(() =>
            {
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[0];
                documentGrid.DataGridView.ClickCurrentCell();
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });
            WaitForGraphs();

            var peakAreaReplicateComparisonGraphSummary = FindGraphSummaryByGraphType<AreaReplicateGraphPane>();
            PauseForScreenShot(peakAreaReplicateComparisonGraphSummary, "Peak Areas - Replicate Comparison");
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
                PauseForScreenShot<ViewEditor>("Customize Report form showing 'Peptide Areas' with Normalized Area selected");
                RunUI(()=>viewEditor.ChooseColumnsTab.AddSelectedColumn());
                PauseForScreenShot<ViewEditor>("Customize Report form showing 'Peptide Areas' with Normalized Area checked");
            }, viewEditor=>viewEditor.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(()=>documentGrid.Activate());
            PauseForScreenShot<DocumentGridForm>("Document Grid: Peptide Areas with Pivot Replicate Name and Normalized Area");
            RunDlg<ChooseFormatDlg>(
                () => documentGrid.DataboundGridControl.ShowFormatDialog(documentGrid.DataGridView.Columns[5]), // Hidden Replicate column
                chooseFormatDlg =>
                {
                    chooseFormatDlg.FormatText = FormatSuggestion.Scientific.FormatString;
                    chooseFormatDlg.DialogResult = DialogResult.OK;
                });
            PauseForScreenShot<DocumentGridForm>("Document Grid: Peptide Areas with Pivot Replicate Name and Total Area in scientific notation",
                processShot: bmp =>
                {
                    // Clean-up the border in the normal way
                    bmp = bmp.CleanupBorder(true);

                    using var g = Graphics.FromImage(bmp);
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    double rows = LocalizationHelper.CurrentCulture.TwoLetterISOLanguageName.Equals("en") ? 10.6 : 11;
                    g.DrawBoxOnColumn(documentGrid, 5, rows);

                    return bmp;
                });

            RunUI(() =>
            {
                var columnCaption = TextUtil.SpaceSeparate("D_102_REP2", ColumnCaptions.NormalizedArea);
                var column = documentGrid.DataGridView.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(col => col.HeaderText == columnCaption);
                Assert.IsNotNull(column, "Unable to find column {0}", columnCaption);
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[9].Cells[column.Index];
            });
            RunUI(()=>
            {
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[9].Cells[0];
                documentGrid.DataGridView.ClickCurrentCell();
                AssertPeptideSelected("TGTNLMDFLSR");
                documentGrid.DataboundGridControl.ReplicatePivotDataGridView.CurrentCell =
                    documentGrid.DataboundGridControl.ReplicatePivotDataGridView.Rows[0].Cells[2];
                documentGrid.DataboundGridControl.ReplicatePivotDataGridView.ClickCurrentCell();
                Assert.AreEqual("D_102_REP2", SkylineWindow.ResultNameCurrent);
            });
            windowLocations = HideFloatingWindows();
            PauseForScreenShot( "Main window");
            RestoreFloatingWindows(windowLocations);
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

            RunUIForScreenShot(() =>
            {
                documentGrid.FloatingPane.FloatingWindow.Width = 1500;
            });
            PauseForScreenShot<DocumentGridForm>("Document Grid: Peptide Areas with more Normalized Areas fields");
            RunDlg<ViewEditor>(documentGrid.DataboundGridControl.NavBar.CustomizeView, viewEditor =>
            {
                var pivotWidget = viewEditor.ViewEditorWidgets.OfType<PivotReplicateAndIsotopeLabelWidget>()
                    .FirstOrDefault();
                Assert.IsNotNull(pivotWidget);
                pivotWidget.SetPivotReplicate(false);
                viewEditor.OkDialog();
            });
            RunUI(()=>documentGrid.FloatingPane.FloatingWindow.Size = new Size(450, 200));
            WaitForCondition(() => documentGrid.IsComplete);
            PauseForScreenShot<DocumentGridForm>("Document Grid toolbar Pivot button",
                processShot: ClipToolStripItem(documentGrid.DataboundGridControl.NavBar.GroupButton));
            RunDlg<PivotEditor>(()=>documentGrid.DataboundGridControl.NavBar.ShowPivotDialog(true), pivotEditor =>
            {
                SelectPivotEditorColumns(pivotEditor, nameof(ColumnCaptions.NormalizedAreaRaw),
                    nameof(ColumnCaptions.NormalizedAreaStrict));
                Assert.AreEqual(2, pivotEditor.AvailableColumnList.SelectedIndices.Count);
                pivotEditor.SelectAggregateOperation(AggregateOperation.Mean);
                pivotEditor.AddValue();
                pivotEditor.OkDialog();
            });
            PauseForScreenShot<DocumentGridForm>("Document Grid: Peptide Areas pivoted to 2 document-wide values");
            RunUI(() =>
            {
                documentGrid.NavBar.GroupButton.DropDown.Closing += DenyMenuClosing;
                documentGrid.NavBar.GroupButton.ShowDropDown();
                var transformMenuItem = documentGrid.NavBar.GroupButton.DropDown.Items
                    .OfType<ToolStripMenuItem>().FirstOrDefault(item =>
                        item.Text == Common.Properties.Resources.NavBar_UpdateGroupTotalDropdown_Transforms);
                Assert.IsNotNull(transformMenuItem);
                transformMenuItem.Select();
                transformMenuItem.DropDown.Closing += DenyMenuClosing;
                transformMenuItem.ShowDropDown();
            });
            PauseForScreenShot<DocumentGridForm>("Document Grid showing Pivot button menu");

            RunUI(() => {
                var transformMenuItem = documentGrid.NavBar.GroupButton.DropDown.Items
                    .OfType<ToolStripMenuItem>().FirstOrDefault(item =>
                        item.Text == Common.Properties.Resources.NavBar_UpdateGroupTotalDropdown_Transforms);
                Assert.IsNotNull(transformMenuItem);
                transformMenuItem.DropDown.Items[transformMenuItem.DropDown.Items.Count - 1].PerformClick();
                transformMenuItem.DropDown.Closing -= DenyMenuClosing;
                transformMenuItem.HideDropDown();
                documentGrid.NavBar.GroupButton.DropDown.Closing -= DenyMenuClosing;
                documentGrid.NavBar.GroupButton.HideDropDown();
            });
            RunUI(()=>
            {
                documentGrid.FloatingPane.FloatingWindow.Size = new Size(1100, 500);
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[0];
            });
            PauseForScreenShot<DocumentGridForm>("Document Grid: Peptide Areas without pivot");
            RunLongDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                var ppSubjectID = PropertyPath.Root.Property(nameof(SkylineDocument.Replicates)).LookupAllItems()
                    .Property(AnnotationDef.GetColumnName("SubjectID"));
                RunUI(()=>
                {
                    viewEditor.ChooseColumnsTab.AvailableFieldsTree.SelectColumn(ppSubjectID);
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                });
                PauseForScreenShot<ViewEditor>("Customize Report with SubjectID checked");
            }, viewEditor=>viewEditor.OkDialog());
            RunUIForScreenShot(() => documentGrid.FloatingPane.FloatingWindow.Width = 1200);
            WaitForCondition(() => documentGrid.IsComplete);
            PauseForScreenShot<DocumentGridForm>("Document Grid: Peptide Areas without pivot and SubjectID added");
            RunLongDlg<PivotEditor>(()=>documentGrid.NavBar.ShowPivotDialog(true), pivotEditor =>
            {
                PauseForScreenShot(pivotEditor, "Pivot Editor form blank");
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
                PauseForScreenShot(pivotEditor, "Pivot Editor form with pivot values");
            }, pivotEditor=>pivotEditor.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(()=>documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[0]);
            RunUIForScreenShot(() => documentGrid.FloatingPane.FloatingWindow.Width = 800);
            PauseForScreenShot<DocumentGridForm>("Document Grid: Peptide Areas pivoted on SubjectID");
            var captionDrizzleNormalizedArea =
                TextUtil.SpaceSeparate("Drizzle", AggregateOperation.Cv.QualifyColumnCaption(new ColumnCaption(ColumnCaptions.NormalizedAreaRaw)).GetCaption(SkylineDataSchema.GetLocalizedSchemaLocalizer()));
            SetSortDirection(documentGrid.DataboundGridControl, captionDrizzleNormalizedArea, ListSortDirection.Ascending);
            PauseForScreenShot<DocumentGridForm>("Document Grid: Peptide Areas pivoted on SubjectID and sorted");
            RunUI(() =>
            {
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[0];
                documentGrid.DataGridView.ClickCurrentCell();
                AssertPeptideSelected("SVVDIGLIK");
            });
            WaitForGraphs();
            peakAreaReplicateComparisonGraphSummary = FindGraphSummaryByGraphType<AreaReplicateGraphPane>();
            PauseForScreenShot(peakAreaReplicateComparisonGraphSummary, "Peak Areas - Replicate Comparison for SVVDIGLIK");
            SetSortDirection(documentGrid.DataboundGridControl, captionDrizzleNormalizedArea, ListSortDirection.Descending);
            RunUI(() =>
            {
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[0];
                documentGrid.DataGridView.ClickCurrentCell();
                AssertPeptideSelected("AGSWQITMK");
            });
            WaitForGraphs();
            PauseForScreenShot(peakAreaReplicateComparisonGraphSummary, "Peak Areas - Replicate Comparison for AGSWQITMK");
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                RunUI(() =>
                {
                    peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Quantification;
                    peptideSettingsUi.QuantNormalizationMethod = NormalizationMethod.GLOBAL_STANDARDS;
                });
                PauseForScreenShot(peptideSettingsUi, "Peptide Settings - Quantification");
            }, peptideSettingsUi=>peptideSettingsUi.OkDialog());
            SetSortDirection(documentGrid.DataboundGridControl, captionDrizzleNormalizedArea, ListSortDirection.Ascending);
            PauseForScreenShot<DocumentGridForm>("Document Grid: Peptide Areas pivoted on SubjectID and normalized to Global Standards");
            RunUI(() =>
            {
                var colCvNormalizedArea = documentGrid.DataGridView.Columns.OfType<DataGridViewColumn>()
                    .FirstOrDefault(col => col.HeaderText == captionDrizzleNormalizedArea);
                Assert.IsNotNull(colCvNormalizedArea);
                var cv = (double) documentGrid.DataGridView.Rows[0].Cells[colCvNormalizedArea.Index].Value;
                Assert.AreEqual(0.0, cv, 1e-6);
            });
            RunLongDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                RunUI(() =>
                {
                    int indexStandardType = viewEditor.ViewSpec.Columns
                        .IndexOf(col => col.PropertyPath.Name == nameof(Peptide.StandardType));
                    Assert.AreNotEqual(-1, indexStandardType);
                    viewEditor.ChooseColumnsTab.ActivateColumn(indexStandardType);
                    viewEditor.TabControl.SelectTab(1);
                    viewEditor.FilterTab.AddSelectedColumn();
                    viewEditor.FilterTab.SetFilterOperation(0, FilterOperations.OP_IS_BLANK);
                });
                PauseForScreenShot<ViewEditor>("Customize Report with filter");
            }, viewEditor=>viewEditor.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(() => SkylineWindow.ShowDocumentGrid(false)); // Done with Document Grid
            const string groupComparisonName = "Peptide Group Comparison";
            RunLongDlg<EditGroupComparisonDlg>(SkylineWindow.AddGroupComparison, editGroupComparisonDlg =>
            {
                RunUI(() =>
                {
                    editGroupComparisonDlg.GroupComparisonDef = GroupComparisonDef.EMPTY
                        .ChangeControlAnnotation("Cohort")
                        .ChangeControlValue("Healthy")
                        .ChangeCaseValue("Diseased")
                        .ChangeIdentityAnnotation("SubjectID")
                        .ChangeNormalizeOption(NormalizeOption.DEFAULT);
                    editGroupComparisonDlg.TextBoxName.Text = groupComparisonName;
                });
            }, editGroupComparisonDlg => editGroupComparisonDlg.OkDialog());
            RunUI(() =>
            {
                SkylineWindow.ShowGroupComparisonWindow(groupComparisonName);
            });
            var groupComparisonGrid = FindOpenForm<FoldChangeGrid>();
            Assert.IsNotNull(groupComparisonGrid);
            PauseForScreenShot(groupComparisonGrid, "Peptide Group Comparison:Grid");
            WaitForCondition(() => groupComparisonGrid.IsComplete);
            RunUI(() =>
            {
                groupComparisonGrid.ShowGraph();
                groupComparisonGrid.FloatingPane.FloatingWindow.Width = 1000;
            });
            SetSortDirection(groupComparisonGrid.DataboundGridControl, ColumnCaptions.FoldChangeResult, ListSortDirection.Ascending);
            PauseForScreenShot(groupComparisonGrid.FloatingPane, "Peptide Group Comparison Grid and Bar Graph");
            RunUI(() =>
            {
                groupComparisonGrid.DataboundGridControl.DataGridView.CurrentCell =
                    groupComparisonGrid.DataboundGridControl.DataGridView.Rows[0].Cells[1];
                groupComparisonGrid.DataboundGridControl.DataGridView.ClickCurrentCell();
                AssertPeptideSelected("WWGQEITELAQGPGR");
            });
            WaitForGraphs();
            PauseForScreenShot(peakAreaReplicateComparisonGraphSummary, "Peak Areas - Replicate Comparison for WWGQEITELAQGPGR");
            RunLongDlg<QuickFilterForm>(()=>groupComparisonGrid.DataboundGridControl.QuickFilter(groupComparisonGrid.DataboundGridControl.DataGridView.Columns[3]),
                quickFilterForm =>
                {
                    Assert.AreEqual(ColumnCaptions.AdjustedPValue, quickFilterForm.PropertyDescriptor.DisplayName);
                    RunUI(() =>
                    {
                        quickFilterForm.SetFilterOperation(0, FilterOperations.OP_IS_LESS_THAN);
                        quickFilterForm.SetFilterOperand(0, 0.05.ToString(CultureInfo.CurrentCulture));
                    });
                    PauseForScreenShot(quickFilterForm, "Grid filter form");
                }, quickFilterForm=>quickFilterForm.OkDialog());
            WaitForCondition(() => groupComparisonGrid.IsComplete);
            PauseForScreenShot(groupComparisonGrid.FloatingPane.FloatingWindow, "Peptide Group Comparison Grid and Bar Graph filtered p value < 0.05");
            RunUI(() =>
            {
                var barGraph = FindOpenForm<FoldChangeBarGraph>();
                Assert.IsNotNull(barGraph);
                barGraph.Close();
            });
            RunDlg<ViewEditor>(groupComparisonGrid.DataboundGridControl.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ViewName = "Replicate Abundances";
                viewEditor.ChooseColumnsTab.AddColumn(PropertyPath.Root
                    .Property(nameof(FoldChangeRow.ReplicateAbundances)).DictionaryValues()
                    .Property(nameof(ReplicateRow.Abundance)));
                viewEditor.OkDialog();
            });
            WaitForCondition(() => groupComparisonGrid.IsComplete);
            PauseForScreenShot(groupComparisonGrid, "Peptide Group Comparison Grid with Abundance values");
            RunUI(() =>
            {
                var clusterSplitButton = groupComparisonGrid.DataboundGridControl.NavBar.ClusterSplitButton;
                clusterSplitButton.DropDown.Closing += DenyMenuClosing;
                clusterSplitButton.ShowDropDown();
                var showHeatMapItem = clusterSplitButton.DropDown.Items.OfType<ToolStripMenuItem>()
                    .FirstOrDefault(item => item.Text == DatabindingResources.DataboundGridControl_DataboundGridControl_Show_Heat_Map);
                Assert.IsNotNull(showHeatMapItem);
                showHeatMapItem.Select();
            });
            PauseForScreenShot(groupComparisonGrid, "Peptide Group Comparison Grid showing heat map menu", processShot:
                bmp =>
                {
                    var rectBitmap = ScreenshotManager.GetFramedWindowBounds(groupComparisonGrid);
                    var clusterSplitButtonMenu = groupComparisonGrid.DataboundGridControl.NavBar.ClusterSplitButton.DropDown;
                    var ptMenuBottomRight = new Point(clusterSplitButtonMenu.Right - rectBitmap.Left,
                        clusterSplitButtonMenu.Bottom - rectBitmap.Top);

                    return ClipBitmap(bmp, new Rectangle(0, 0, ptMenuBottomRight.X + 14, ptMenuBottomRight.Y + 14));
                });
            RunUI(() =>
            {
                var clusterSplitButton = groupComparisonGrid.DataboundGridControl.NavBar.ClusterSplitButton;
                clusterSplitButton.DropDown.Closing -= DenyMenuClosing;
                clusterSplitButton.HideDropDown();
            });
            RunUI(() =>
            {
                groupComparisonGrid.DataboundGridControl.ShowHeatMap();
            });
            RunLongDlg<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog, documentSettingsDlg =>
            {
                RunUI(() =>
                {
                    documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.reports);
                });
                PauseForScreenShot(documentSettingsDlg, "Document Settings - Reports");
                
                RunUI(()=>
                {
                    var mainGroup = PersistedViews.MainGroup.Id;
                    documentSettingsDlg.ChooseViewsControl.CheckedViews = new[]
                    {
                        mainGroup.ViewName("Peptide Areas"),
                        mainGroup.ViewName("Replicate Abundances")
                    };
                });
            }, documentSettingsDlg=>documentSettingsDlg.OkDialog());
            RunUI(()=>SkylineWindow.ShowAuditLog());
            auditLogForm = FindOpenForm<AuditLogForm>();
            Assert.IsNotNull(auditLogForm);
            WaitForCondition(() => auditLogForm.IsComplete);
            PauseForScreenShot(auditLogForm, "Audit Log: All Info");
            RunUI(() =>
            {
                auditLogForm.ChooseView(AuditLogStrings.AuditLogForm_MakeAuditLogForm_Summary);
            });
            WaitForCondition(() => auditLogForm.IsComplete);
            PauseForScreenShot(auditLogForm, "Audit Log: Summary");
            if (IsCoverShotMode)
            {
                RestoreCoverViewOnScreen();
                TakeCoverShot();
            }
            RunLongDlg<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog, exportReportDlg =>
            {
                PauseForScreenShot(exportReportDlg, "Export Report");
            }, exportReportDlg=>exportReportDlg.Close());
            RunUI(() =>
            {
                documentGrid.Close();
                auditLogForm.Close();
                listGridForm.Close();
                groupComparisonGrid.Close();
            });
        }

        public Func<Bitmap, Bitmap> ClipToolStripItem(ToolStripItem menuItem)
        {
            return bmp => CallUI(() =>
            {
                var toolStrip = menuItem.GetCurrentParent();
                var parentWindowRect = ScreenshotManager.GetFramedWindowBounds(toolStrip);
                var menuItemScreenRect = toolStrip.RectangleToScreen(menuItem.Bounds);
                return ClipBitmap(bmp, new Rectangle(menuItemScreenRect.Left - parentWindowRect.Left,
                    menuItemScreenRect.Top - parentWindowRect.Top, menuItemScreenRect.Width,
                    menuItemScreenRect.Height));
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

        private void SetSortDirection(DataboundGridControl databoundGridControl, string columnCaption,
            ListSortDirection sortDirection)
        {
            RunUI(() =>
            {
                var column = databoundGridControl.DataGridView.Columns.OfType<DataGridViewColumn>()
                    .FirstOrDefault(col => col.HeaderText == columnCaption);
                Assert.IsNotNull(column, "Unable to find column named {0}", columnCaption);
                var propertyDescriptor =
                    databoundGridControl.BindingListSource.ItemProperties.FindByName(column.DataPropertyName);
                Assert.IsNotNull(propertyDescriptor);
                databoundGridControl.SetSortDirection(propertyDescriptor, sortDirection);
            });
            WaitForCondition(() => databoundGridControl.IsComplete);
        }

        private void AssertPeptideSelected(string expectedSequence)
        {
            var selectedPath = SkylineWindow.SelectedPath;
            Assert.IsInstanceOfType(selectedPath.Child, typeof(pwiz.Skyline.Model.Peptide));
            var peptide = (pwiz.Skyline.Model.Peptide)selectedPath.Child;
            Assert.AreEqual(expectedSequence, peptide.Sequence);
        }
    }
}
