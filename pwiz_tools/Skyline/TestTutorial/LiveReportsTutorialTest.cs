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
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using System.Windows.Forms;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.MetadataExtraction;
using pwiz.Skyline.SettingsUI;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class LiveReportsTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLiveReportsTutorial()
        {
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
            RunUI(() =>
            {
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            var rectReportsDropDown = ShowReportsDropdown(Resources.SkylineViewContext_GetDocumentGridRowSources_Proteins);
            PauseForScreenShot<DocumentGridForm>("Document Grid Reports Menu", null, bmp=>ClipBitmap(bmp.CleanupBorder(true), rectReportsDropDown));
            HideReportsDropdown();
            RunUI(()=>
            {
                documentGrid.DataboundGridControl.ChooseView(
                    ViewGroup.BUILT_IN.Id.ViewName(Resources
                        .SkylineViewContext_GetDocumentGridRowSources_Proteins));
            });
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[3].Cells[0];
                documentGrid.DataGridView.ClickCurrentCell();
                Assert.AreEqual(SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.MoleculeGroups, 3), SkylineWindow.SelectedPath);
                documentGrid.DataboundGridControl.ChooseView(
                    ViewGroup.BUILT_IN.Id.ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            });
            WaitForCondition(() => documentGrid.IsComplete);
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
            RunUI(()=>
            {
                documentGrid.Activate();
                var cohortColumn = documentGrid.DataboundGridControl.FindColumn(
                    PropertyPath.Root.Property(AnnotationDef.ANNOTATION_PREFIX + "Cohort"));
                Assert.IsNotNull(cohortColumn);
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[cohortColumn.Index];
                documentGrid.DataGridView.ClickCurrentCell();

            });
            PauseForScreenShot<DocumentGridForm>("Document grid with two annotations");
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
                    PauseForScreenShot<MetadataRuleSetEditor>("Rule Set Editor with one rule");
                    RunLongDlg<MetadataRuleEditor>(() => metadataRuleSetEditor.EditRule(0), metadataRuleEditor =>
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
                    PauseForScreenShot<MetadataRuleSetEditor>("Rule Set Editor with two rules");

                }, metadataRuleSetEditor => metadataRuleSetEditor.OkDialog());
            }, documentSettingsDlg=>documentSettingsDlg.OkDialog());
        }
    }
}
