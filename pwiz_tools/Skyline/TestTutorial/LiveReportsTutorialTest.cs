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

using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;

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
            PauseForScreenShot<AuditLogForm>("Audit log before enabling audit logging");
            RunUI(()=>auditLogForm.EnableAuditLogging(true));
            PauseForScreenShot<AuditLogForm>("Audit log after enabling audit logging");
            OkDialog(auditLogForm, auditLogForm.Close);
            PauseForScreenShot("Status bar", null, ClipSelectionStatus);
            RunUI(() =>
            {
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            ShowReportsDropdown(Resources.SkylineViewContext_GetDocumentGridRowSources_Proteins);
            PauseForScreenShot<DocumentGridForm>("Document Grid Reports Menu");
            HideReportsDropdown();
            RunUI(()=>
            {
                documentGrid.DataboundGridControl.ChooseView(
                    ViewGroup.BUILT_IN.Id.ViewName(Resources
                        .SkylineViewContext_GetDocumentGridRowSources_Proteins));
            });
            WaitForCondition(() => documentGrid.IsComplete);
            PauseForScreenShot<DocumentGridForm>("Proteins report");
            RunUI(() =>
            {
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[3].Cells[0];
                documentGrid.DataGridView.ClickCurrentCell();
                Assert.AreEqual(SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.MoleculeGroups, 3), SkylineWindow.SelectedPath);
            });
            PauseForScreenShot<SequenceTreeForm>("Targets view");
            PauseForScreenShot("Status bar", null, ClipSelectionStatus);
            PauseForScreenShot(documentGrid.NavBar, processShot:bmp=>ClipControl(documentGrid.NavBar, bmp));
            RunUI(()=>
            {
                documentGrid.DataboundGridControl.ChooseView(
                    ViewGroup.BUILT_IN.Id.ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            });
            WaitForCondition(() => documentGrid.IsComplete);
            PauseForScreenShot<DocumentGridForm>("Replicates report");
            RunLongDlg<DocumentSettingsDlg>(()=>SkylineWindow.ShowDocumentSettingsDialog(), documentSettingsDlg =>
            {
                RunUI(()=>documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.annotations));
                PauseForScreenShot<DocumentSettingsDlg>("Initial document settings dialog");
                RunLongDlg<DefineAnnotationDlg>(documentSettingsDlg.AddAnnotation, defineAnnotationDlg =>
                {
                    PauseForScreenShot<DefineAnnotationDlg>("Blank define annotation dialog");
                    RunUI(()=>
                    {
                        defineAnnotationDlg.AnnotationName = "Cohort";
                        defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.value_list;
                        defineAnnotationDlg.Items = new[] { "Healthy", "Diseased" };
                        defineAnnotationDlg.AnnotationTargets =
                            AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate);
                    });
                    PauseForScreenShot<DefineAnnotationDlg>("Cohort annotation");
                }, defineAnnotationDlg=>defineAnnotationDlg.OkDialog());
                PauseForScreenShot<DocumentSettingsDlg>("Document settings dialog with one annotation");
                RunLongDlg<DefineAnnotationDlg>(documentSettingsDlg.AddAnnotation, defineAnnotationDlg =>
                {
                    RunUI(() =>
                    {
                        defineAnnotationDlg.AnnotationName = "SubjectID";
                        defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.text;
                        defineAnnotationDlg.AnnotationTargets =
                            AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate);
                    });
                    PauseForScreenShot<DefineAnnotationDlg>("SubjectID annotation");
                }, defineAnnotationDlg => defineAnnotationDlg.OkDialog());
                PauseForScreenShot<DocumentSettingsDlg>("Document settings dialog with two annotations");
            }, documentSettingsDlg=>documentSettingsDlg.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(()=>
            {
                documentGrid.Activate();
                var cohortColumn = documentGrid.DataboundGridControl.FindColumn(
                    PropertyPath.Root.Property(AnnotationDef.ANNOTATION_PREFIX + "Cohort"));
                Assert.IsNotNull(cohortColumn);
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[cohortColumn.Index];
            });
            using (new GridTester(documentGrid.DataGridView).ShowComboBox())
            {
                PauseForScreenShot<DocumentGridForm>("Document grid with two annotations");
            }
            RunLongDlg<DocumentSettingsDlg>(()=>SkylineWindow.ShowDocumentSettingsDialog(), documentSettingsDlg =>
            {
                RunUI(()=>documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.metadata_rules));
                RunLongDlg<MetadataRuleSetEditor>(documentSettingsDlg.AddMetadataRule, metadataRuleSetEditor =>
                {
                    RunLongDlg<MetadataRuleEditor>(()=>metadataRuleSetEditor.EditRule(0), metadataRuleEditor =>
                    {
                        PauseForScreenShot<MetadataRuleEditor>("Rule Editor");
                        RunUI(() =>
                        {
                            metadataRuleEditor.MetadataRule = metadataRuleEditor.MetadataRule.ChangePattern("D")
                                .ChangeReplacement("Diseased").ChangeTarget(PropertyPath.Root
                                    .Property(nameof(ResultFile.Replicate))
                                    .Property(AnnotationDef.ANNOTATION_PREFIX + "Cohort"));
                        });
                        PauseForScreenShot<MetadataRuleEditor>("Diseased rule");
                        RunUI(() =>metadataRuleEditor.PreviewGrid.FirstDisplayedScrollingRowIndex = 17);
                        PauseForScreenShot<MetadataRuleEditor>("Diseased rule scrolled down");

                    }, metadataRuleEditor=>metadataRuleEditor.OkDialog());
                    RunUI(()=>metadataRuleSetEditor.DataGridViewSteps.CurrentCell = metadataRuleSetEditor.DataGridViewSteps.Rows[1].Cells[0]);
                    PauseForScreenShot<MetadataRuleSetEditor>("Rule Set Editor with one rule");
                    RunLongDlg<MetadataRuleEditor>(() => metadataRuleSetEditor.EditRule(1), metadataRuleEditor =>
                    {
                        RunUI(() =>
                        {
                            metadataRuleEditor.MetadataRule = metadataRuleEditor.MetadataRule.ChangePattern("H")
                                .ChangeReplacement("Healthy").ChangeTarget(PropertyPath.Root
                                    .Property(nameof(ResultFile.Replicate))
                                    .Property(AnnotationDef.ANNOTATION_PREFIX + "Cohort"));
                        });
                        PauseForScreenShot<MetadataRuleEditor>("Healthy rule");
                    }, metadataRuleEditor => metadataRuleEditor.OkDialog());
                    RunUI(() => metadataRuleSetEditor.DataGridViewSteps.CurrentCell = metadataRuleSetEditor.DataGridViewSteps.Rows[2].Cells[0]);
                    PauseForScreenShot<MetadataRuleSetEditor>("Rule Set Editor with two rules");
                    RunUI(() =>
                    {
                        metadataRuleSetEditor.RuleName = "Cohort and SubjectID";
                    });
                }, metadataRuleSetEditor => metadataRuleSetEditor.OkDialog());
            }, documentSettingsDlg=>documentSettingsDlg.OkDialog());
            WaitForCondition(() => documentGrid.IsComplete);
            PauseForScreenShot<DocumentGridForm>("Document Grid with populated Cohort and SubjectID");
            RunLongDlg<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog, documentSettingsDlg =>
            {
                RunUI(() =>
                {
                    documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.lists);
                });
                RunLongDlg<ListDesigner>(documentSettingsDlg.AddList, listDesigner =>
                {
                    PauseForScreenShot<ListDesigner>("Blank list designer");
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
                        listDesigner.IdProperty = "SubjectID";
                        listDesigner.DisplayProperty = "Name";
                    });
                    PauseForScreenShot<ListDesigner>("Completed Samples list definition");
                }, listDesigner => listDesigner.OkDialog());
            }, documentSettingsDlg=>documentSettingsDlg.OkDialog());
            RunUI(() => SkylineWindow.ShowList("Samples"));
            var listGridForm = FindOpenForm<ListGridForm>();
            Assert.IsNotNull(listGridForm);
            PauseForScreenShot<ListGridForm>("Empty samples list");
            SetClipboardText(TextUtil.LineSeparate(File.ReadAllLines(TestFilesDir.GetTestPath("SampleInfo.txt")).Skip(1)));
            RunUI(()=>listGridForm.DataGridView.SendPaste());
            PauseForScreenShot<ListGridForm>("Completed samples list");
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
    }
}
