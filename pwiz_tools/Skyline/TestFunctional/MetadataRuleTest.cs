/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.MetadataExtraction;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
// ReSharper disable LocalizableElement

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class MetadataRuleTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMetadataRules()
        {
            TestFilesZip = @"TestFunctional\MetadataRuleTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_plasma.sky")));
            RunLongDlg<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog, documentSettingsDlg =>
            {
                RunUI(() => documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.annotations));
                // Define some replicate annotations
                RunDlg<DefineAnnotationDlg>(documentSettingsDlg.AddAnnotation, defineAnnotationDlg =>
                {
                    defineAnnotationDlg.AnnotationName = "SubjectId";
                    defineAnnotationDlg.AnnotationTargets =
                        AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate);
                    defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.text;
                    defineAnnotationDlg.OkDialog();
                });
                RunDlg<DefineAnnotationDlg>(documentSettingsDlg.AddAnnotation, defineAnnotationDlg =>
                {
                    defineAnnotationDlg.AnnotationName = "BioReplicate";
                    defineAnnotationDlg.AnnotationTargets =
                        AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate);
                    defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.number;
                    defineAnnotationDlg.OkDialog();
                });
                RunDlg<DefineAnnotationDlg>(documentSettingsDlg.AddAnnotation, defineAnnotationDlg =>
                {
                    defineAnnotationDlg.AnnotationName = "Condition";
                    defineAnnotationDlg.AnnotationTargets =
                        AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate);
                    defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.value_list;
                    defineAnnotationDlg.Items = new[] { "Healthy", "Diseased" };
                    defineAnnotationDlg.OkDialog();
                });
                // Define a rule which sets SubjectId for the samples to "D" or "H" followed by "_" and the bioreplicate number.
                RunUI(() => { documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.metadata_rules); });

                RunLongDlg<MetadataRuleSetEditor>(documentSettingsDlg.AddMetadataRule, metadataRuleEditor =>
                {
                    RunUI(() => { metadataRuleEditor.RuleName = "SubjectId"; });
                    RunDlg<MetadataRuleEditor>(() => metadataRuleEditor.EditRule(0),
                        metadataRuleStepEditor =>
                        {
                            metadataRuleStepEditor.MetadataRule = new MetadataRule()
                                .ChangeSource(PropertyPath.Root.Property(nameof(ResultFile.FileName)))
                                .ChangePattern("[DH]_[0-9]+")
                                .ChangeTarget(PropertyPathForAnnotation("SubjectId"));
                            metadataRuleStepEditor.OkDialog();
                        });
                }, metadataRuleEditor => metadataRuleEditor.OkDialog());
            }, documentSettingsDlg => documentSettingsDlg.OkDialog());

            // Verify that newly imported files get the correct SubjectId
            ImportResultsFiles(new[]
            {
                new MsDataFilePath(TestFilesDir.GetTestPath("D_102_REP1.mzML")),
                new MsDataFilePath(TestFilesDir.GetTestPath("H_146_Rep1.mzML"))
            });
            Assert.AreEqual(2, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count);
            CollectionAssert.AreEqual(new[]{"D_102", "H_146"}, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms
                .Select(chrom=>chrom.Annotations.GetAnnotation("SubjectId")).ToList());

            // Add a BioReplicate rule which sets the BioReplicate to the number between the underscores in the filename.
            RunLongDlg<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog, documentSettingsDlg =>
            {
                RunUI(() => { documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.metadata_rules); });
                RunLongDlg<MetadataRuleSetEditor>(documentSettingsDlg.AddMetadataRule, metadataRuleEditor =>
                {
                    RunUI(() => metadataRuleEditor.RuleName = "BioReplicate");
                    RunLongDlg<MetadataRuleEditor>(() => metadataRuleEditor.EditRule(0), metadataRuleStepEditor =>
                    {
                        RunUI(() =>
                        {
                            metadataRuleStepEditor.MetadataRule = new MetadataRule()
                                .ChangeSource(PropertyPath.Root.Property(nameof(ResultFile.FileName)))
                                .ChangePattern("_([0-9]+)")
                                .ChangeReplacement("$1")
                                .ChangeTarget(PropertyPathForAnnotation("BioReplicate"));
                        });
                        WaitForConditionUI(() =>
                            ((BindingListSource)metadataRuleStepEditor.PreviewGrid.DataSource).IsComplete);
                        Assert.AreEqual(2, metadataRuleStepEditor.PreviewGrid.RowCount);
                    }, metadataRuleStepEditor => metadataRuleStepEditor.OkDialog());
                }, metadataRuleEditor => metadataRuleEditor.OkDialog());

                // Change the "SubjectId" rule so that it has some regular expressions groups in it
                RunLongDlg<EditListDlg<SettingsListBase<MetadataRuleSet>, MetadataRuleSet>>
                (documentSettingsDlg.EditMetadataRuleList, metadataRuleListEditor =>
                {
                    RunUI(() => metadataRuleListEditor.SelectItem("SubjectId"));
                    RunLongDlg<MetadataRuleSetEditor>(metadataRuleListEditor.EditItem,
                        metadataRuleEditor =>
                        {
                            RunUI(() =>
                            {
                                var grid = metadataRuleEditor.DataGridViewSteps;
                                grid.CurrentCell = grid.Rows[0].Cells[metadataRuleEditor.ColumnPattern.Index];
                                SetCurrentCellValue(grid, "([DH])_([0-9]+)");
                            });
                            WaitForConditionUI(() =>
                                ((BindingListSource)metadataRuleEditor.PreviewGrid.DataSource).IsComplete);
                            RunUI(() =>
                            {
                                var grid = metadataRuleEditor.PreviewGrid;
                                var colSubjectId = grid.Columns.OfType<DataGridViewColumn>()
                                    .FirstOrDefault(col => col.HeaderText == "SubjectId");
                                Assert.IsNotNull(colSubjectId);
                                Assert.AreEqual("D_102", grid.Rows[0].Cells[colSubjectId.Index].Value);
                            });
                            // Change the replacement value so that the "_" is removed from the SubjectId
                            RunUI(() =>
                            {
                                var grid = metadataRuleEditor.DataGridViewSteps;
                                grid.CurrentCell = grid.Rows[0].Cells[metadataRuleEditor.ColumnReplacement.Index];
                                SetCurrentCellValue(grid, "$1$2");
                                grid.CurrentCell = grid.Rows[0].Cells[metadataRuleEditor.ColumnPattern.Index];
                            });
                            WaitForConditionUI(() =>
                                ((BindingListSource)metadataRuleEditor.PreviewGrid.DataSource).IsComplete);
                            RunUI(() =>
                            {
                                var grid = metadataRuleEditor.PreviewGrid;
                                var colSubjectId = grid.Columns.OfType<DataGridViewColumn>()
                                    .FirstOrDefault(col => col.HeaderText == "SubjectId");
                                Assert.IsNotNull(colSubjectId);
                                Assert.AreEqual("D102", grid.Rows[0].Cells[colSubjectId.Index].Value);
                            });
                        }, metadataRuleEditor => metadataRuleEditor.OkDialog());
                }, metadataRuleListEditor => metadataRuleListEditor.OkDialog());
            }, documentSettingsDlg => documentSettingsDlg.OkDialog());

            // Verify the "SubjectId" and "BioReplicate" values on the replicates
            CollectionAssert.AreEqual(new[] { "D102", "H146" }, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms
                .Select(chrom => chrom.Annotations.GetAnnotation("SubjectId")).ToList());
            var annotationDefBioReplicate =
                SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.FirstOrDefault(def =>
                    def.Name == "BioReplicate");
            Assert.IsNotNull(annotationDefBioReplicate);
            CollectionAssert.AreEqual(new[] { 102.0, 146.0 }, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms
                .Select(chrom => chrom.Annotations.GetAnnotation(annotationDefBioReplicate)).ToList());

            // Modify the "SubjectId" rule so that it also sets "Condition" to either "Diseased" or "Healthy"
            RunLongDlg<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog, documentSettingsDlg =>
            {
                RunUI(() => { documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.metadata_rules); });
                RunLongDlg<EditListDlg<SettingsListBase<MetadataRuleSet>, MetadataRuleSet>>
                (documentSettingsDlg.EditMetadataRuleList, metadataRuleListEditor =>
                {
                    RunUI(() => { metadataRuleListEditor.SelectItem("SubjectId"); });
                    RunDlg<MetadataRuleSetEditor>(metadataRuleListEditor.EditItem, metadataRuleEditor =>
                    {
                        var grid = metadataRuleEditor.DataGridViewSteps;
                        var newRow = grid.Rows[grid.RowCount - 1];
                        Assert.IsTrue(newRow.IsNewRow);
                        grid.CurrentCell = newRow.Cells[metadataRuleEditor.ColumnPattern.Index];
                        SetCurrentCellValue(grid, "D_");
                        grid.CurrentCell = newRow.Cells[metadataRuleEditor.ColumnReplacement.Index];
                        SetCurrentCellValue(grid, "Diseased");
                        grid.CurrentCell = newRow.Cells[metadataRuleEditor.ColumnTarget.Index];
                        SetCurrentCellValue(grid, "Condition");
                        newRow = grid.Rows[grid.RowCount - 1];
                        Assert.IsTrue(newRow.IsNewRow);
                        grid.CurrentCell = newRow.Cells[metadataRuleEditor.ColumnPattern.Index];
                        SetCurrentCellValue(grid, "H_");
                        grid.CurrentCell = newRow.Cells[metadataRuleEditor.ColumnReplacement.Index];
                        SetCurrentCellValue(grid, "Healthy");
                        grid.CurrentCell = newRow.Cells[metadataRuleEditor.ColumnTarget.Index];
                        SetCurrentCellValue(grid, "Condition");
                        grid.CurrentCell = newRow.Cells[metadataRuleEditor.ColumnSource.Index];
                        metadataRuleEditor.OkDialog();
                    });
                }, metadataRuleListEditor => metadataRuleListEditor.OkDialog());
            }, documentSettingsDlg=> documentSettingsDlg.OkDialog());
            CollectionAssert.AreEqual(new[] { "Diseased", "Healthy" }, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms
                .Select(chrom => chrom.Annotations.GetAnnotation("Condition")).ToList());
            // Import some more result files
            ImportResultsFiles(new[]
            {
                new MsDataFilePath(TestFilesDir.GetTestPath("D_102_REP2.mzML")),
                new MsDataFilePath(TestFilesDir.GetTestPath("H_146_Rep2.mzML"))
            });
            CollectionAssert.AreEqual(new[] {"Diseased", "Healthy", "Diseased", "Healthy"},
                SkylineWindow.Document.Settings.MeasuredResults.Chromatograms
                    .Select(chrom => chrom.Annotations.GetAnnotation("Condition")).ToList());
        }

        private void SetCurrentCellValue(DataGridView grid, object value)
        {
            IDataGridViewEditingControl editingControl = null;
            DataGridViewEditingControlShowingEventHandler onEditingControlShowing =
                (sender, args) =>
                {
                    Assume.IsNull(editingControl);
                    editingControl = args.Control as IDataGridViewEditingControl;
                };
            try
            {
                grid.EditingControlShowing += onEditingControlShowing;
                grid.BeginEdit(true);
                if (null != editingControl)
                {
                    editingControl.EditingControlFormattedValue = value;
                }
                else
                {
                    grid.CurrentCell.Value = value;
                }
            }
            finally
            {
                grid.EditingControlShowing -= onEditingControlShowing;
            }
        }

        private PropertyPath PropertyPathForAnnotation(string annotationName)
        {
            return PropertyPath.Root.Property(nameof(ResultFile.Replicate))
                .Property(AnnotationDef.ANNOTATION_PREFIX + annotationName);
        }
    }
}
