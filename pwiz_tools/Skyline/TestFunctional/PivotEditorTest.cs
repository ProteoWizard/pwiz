/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.DataBinding.Layout;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    // ReSharper disable LocalizableElement
    [TestClass]
    public class PivotEditorTest : AbstractFunctionalTest
    {
        private const string pivotTestViewName = "PivotTest";
        private const string meanCvByConditionNumber = "meanCvByConditionNumber";
        private const double conditionNumber1 = -1.34E-23;
        private const double conditionNumber2 = 1.27E+18;

        [TestMethod]
        public void TestPivotEditor()
        {
            TestFilesZip = @"TestFunctional\PivotEditorTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_plasma.sky")));
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            CreateBaseView(documentGrid);
            WaitForConditionUI(() => documentGrid.IsComplete);
            DoFirstPivot(documentGrid);
            WaitForConditionUI(() => documentGrid.IsComplete);
            int unfilteredRowCount = documentGrid.RowCount;
            Assert.AreNotEqual(0, unfilteredRowCount);
            FilterOutNaN(documentGrid);
            SortOnConditionNumber(documentGrid);
            WaitForConditionUI(() => documentGrid.IsComplete);
            int filteredRowCount = documentGrid.RowCount;
            Assert.AreNotEqual(0, filteredRowCount);
            Assert.AreNotEqual(unfilteredRowCount, filteredRowCount);
            Assert.IsTrue(unfilteredRowCount > filteredRowCount);
            DoSecondPivot(documentGrid);
            WaitForConditionUI(() => documentGrid.IsComplete);
            var nameLayoutDlg = ShowDialog<NameLayoutForm>(documentGrid.NavBar.RememberCurrentLayout);
            RunUI(()=>nameLayoutDlg.LayoutName = meanCvByConditionNumber);
            OkDialog(nameLayoutDlg, nameLayoutDlg.OkDialog);
            var manageLayoutsForm = ShowDialog<ManageLayoutsForm>(documentGrid.NavBar.ManageLayouts);
            RunUI(() =>
            {
                manageLayoutsForm.ListView.SelectedIndices.Clear();
                manageLayoutsForm.ListView.SelectedIndices.Add(0);
                manageLayoutsForm.ToggleDefault();
            });
            OkDialog(manageLayoutsForm, manageLayoutsForm.OkDialog);
            var exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(()=>
            {
                exportLiveReportDlg.SetUseInvariantLanguage(true);
                exportLiveReportDlg.ReportName = pivotTestViewName;
            });
            DocumentGridForm invariantPreviewForm = null;
            RunUI(() =>
            {
                exportLiveReportDlg.ShowPreview();
                invariantPreviewForm = Application.OpenForms.OfType<DocumentGridForm>()
                    .FirstOrDefault(form => !form.ShowViewsMenu);
            });
            Assert.IsNotNull(invariantPreviewForm);
            WaitForConditionUI(() => invariantPreviewForm.IsComplete);

            var meanCvCaption = AggregateOperation.Mean.QualifyColumnCaption(
                AggregateOperation.Cv.QualifyColumnCaption(new ColumnCaption("NormalizedArea")));
            var invariantMeanCvCaption = meanCvCaption.GetCaption(DataSchemaLocalizer.INVARIANT);
            Assert.AreEqual("Mean CV NormalizedArea", invariantMeanCvCaption);
            Assert.AreEqual("-1.34E-23", conditionNumber1.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual("1.27E+18", conditionNumber2.ToString(CultureInfo.InvariantCulture));
            CollectionAssert.AreEqual(new[]
            {
                "Peptide", "Protein", "-1.34E-23 Mean CV NormalizedArea", "1.27E+18 Mean CV NormalizedArea"
            }, GetColumnCaptions(invariantPreviewForm.DataGridView));
            DocumentGridForm localizedPreviewForm = null;
            RunUI(() =>
            {
                exportLiveReportDlg.SetUseInvariantLanguage(false);
                exportLiveReportDlg.ShowPreview();
                localizedPreviewForm = Application.OpenForms.OfType<DocumentGridForm>()
                    .FirstOrDefault(form => !form.ShowViewsMenu && form != invariantPreviewForm);
            });
            Assert.IsNotNull(localizedPreviewForm);
            WaitForConditionUI(() => localizedPreviewForm.IsComplete);
            var localizer = localizedPreviewForm.DataboundGridControl.BindingListSource.ViewInfo.DataSchema
                .DataSchemaLocalizer;
            var localizedMeanCvCaption = meanCvCaption.GetCaption(localizer);

            CollectionAssert.AreEqual(new[]
            {
                ColumnCaptions.Peptide, ColumnCaptions.Protein,
                TextUtil.SpaceSeparate(conditionNumber1.ToString(CultureInfo.CurrentCulture), localizedMeanCvCaption),
                TextUtil.SpaceSeparate(conditionNumber2.ToString(CultureInfo.CurrentCulture), localizedMeanCvCaption)
            }, GetColumnCaptions(localizedPreviewForm.DataGridView));
            OkDialog(exportLiveReportDlg, exportLiveReportDlg.CancelClick);
            OkDialog(localizedPreviewForm, localizedPreviewForm.Close);
            OkDialog(invariantPreviewForm, invariantPreviewForm.Close);
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            documentSettingsDlg.ChooseViewsControl.CheckedViews =
                new []{PersistedViews.MainGroup.Id.ViewName(pivotTestViewName)};
            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            RunUI(()=>SkylineWindow.SaveDocument());
        }

        private void CreateBaseView(DocumentGridForm documentGrid)
        {
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides));
            var viewEditor = ShowDialog<ViewEditor>(documentGrid.DataboundGridControl.NavBar.CustomizeView);
            var columnsToKeep = new[]
            {
                PropertyPath.Root.Property("Proteins").LookupAllItems(),
                PropertyPath.Root.Property("Proteins").LookupAllItems().Property("Peptides").LookupAllItems()
            };
            RunUI(() =>
            {
                foreach (var column in viewEditor.ChooseColumnsTab.ViewSpec.Columns)
                {
                    if (!columnsToKeep.Contains(column.PropertyPath))
                    {
                        viewEditor.ChooseColumnsTab.RemoveColumn(column.PropertyPath);
                    }
                }
            });
            Assert.AreEqual(columnsToKeep.Length, viewEditor.ChooseColumnsTab.ColumnCount);
            PropertyPath pathNormalizedArea = PropertyPath.Root.Property("Proteins").LookupAllItems()
                .Property("Peptides").LookupAllItems()
                .Property("Results").LookupAllItems().Property("Value").Property("Quantification")
                .Property("NormalizedArea");

            var columnsToAdd = new[]
            {
                PropertyPath.Root.Property("Replicates").LookupAllItems()
                    .Property(AnnotationDef.GetColumnName("BioReplicate")),
                PropertyPath.Root.Property("Replicates").LookupAllItems()
                    .Property(AnnotationDef.GetColumnName("ConditionNumber")),
                pathNormalizedArea
            };
            RunUI(() =>
            {
                foreach (var column in columnsToAdd)
                {
                    viewEditor.ChooseColumnsTab.AddColumn(column);
                }
            });
            RunUI(() =>
            {
                viewEditor.ViewName = pivotTestViewName;
            });
            OkDialog(viewEditor, viewEditor.OkDialog);
        }

        private void DoFirstPivot(DocumentGridForm documentGrid)
        {
            var pivotEditor = ShowDialog<PivotEditor>(() => documentGrid.NavBar.ShowPivotDialog(false));
            RunUI(() =>
            {
                Assert.AreEqual(5, pivotEditor.AvailableColumnList.Items.Count);
                pivotEditor.AvailableColumnList.SelectedIndices.Clear();
                for (int i = 0; i < 4; i++)
                {
                    pivotEditor.AvailableColumnList.SelectedIndices.Add(i);
                }
                pivotEditor.AddRowHeader();
                Assert.AreEqual(1, pivotEditor.AvailableColumnList.Items.Count);
                pivotEditor.AvailableColumnList.SelectedIndices.Clear();
                pivotEditor.AvailableColumnList.SelectedIndices.Add(0);
                pivotEditor.SelectAggregateOperation(AggregateOperation.Cv);
                pivotEditor.AddValue();
            });
            OkDialog(pivotEditor, pivotEditor.OkDialog);
        }

        private void FilterOutNaN(DocumentGridForm documentGrid)
        {
            var columnIdCv = new ColumnId(AggregateOperation.Cv.QualifyColumnCaption(new ColumnCaption("NormalizedArea")));
            DataGridViewColumn columnCv = null;
            RunUI(() =>
            {
                var pdCv = documentGrid.DataboundGridControl.BindingListSource.ItemProperties
                    .FirstOrDefault(pd => ColumnId.GetColumnId(pd).Equals(columnIdCv));
                Assert.IsNotNull(pdCv);
                columnCv = documentGrid.DataboundGridControl.DataGridView.Columns.OfType<DataGridViewColumn>()
                    .FirstOrDefault(col => col.DataPropertyName == pdCv.Name);
            });
            Assert.IsNotNull(columnCv);
            var quickFilterForm =
                ShowDialog<QuickFilterForm>(() => documentGrid.DataboundGridControl.QuickFilter(columnCv));
            RunUI(() =>
            {
                quickFilterForm.SetFilterOperation(0, FilterOperations.OP_NOT_EQUALS);
                quickFilterForm.SetFilterOperand(0, double.NaN.ToString(CultureInfo.CurrentCulture));
            });
            OkDialog(quickFilterForm, quickFilterForm.OkDialog);
        }

        private void SortOnConditionNumber(DocumentGridForm documentGrid)
        {
            RunUI(() =>
            {
                var columnConditionNumber = documentGrid.DataGridView.Columns.OfType<DataGridViewColumn>()
                    .FirstOrDefault(col => col.HeaderText == "ConditionNumber");
                Assert.IsNotNull(columnConditionNumber);
                documentGrid.DataGridView.Sort(columnConditionNumber, ListSortDirection.Ascending);
            });
        }

        private void DoSecondPivot(DocumentGridForm documentGrid)
        {
            var pivotEditor = ShowDialog<PivotEditor>(() => documentGrid.NavBar.ShowPivotDialog(false));
            RunUI(() =>
            {
                Assert.AreEqual(5, pivotEditor.AvailableColumnList.Items.Count);
                pivotEditor.AvailableColumnList.SelectedIndices.Clear();
                foreach (int i in Enumerable.Range(0, 2))
                {
                    pivotEditor.AvailableColumnList.SelectedIndices.Add(i);
                }
                pivotEditor.AddRowHeader();
                pivotEditor.AvailableColumnList.SelectedIndices.Clear();
                pivotEditor.AvailableColumnList.SelectedIndices.Add(1);
                pivotEditor.AddColumnHeader();
                pivotEditor.AvailableColumnList.SelectedIndices.Clear();
                pivotEditor.AvailableColumnList.SelectedIndices.Add(1);
                pivotEditor.SelectAggregateOperation(AggregateOperation.Mean);
                pivotEditor.AddValue();
            });
            OkDialog(pivotEditor, pivotEditor.OkDialog);
        }

        private string[] GetColumnCaptions(DataGridView dataGridView)
        {
            string[] result = null;
            RunUI(() =>
            {
                result = dataGridView.Columns.OfType<DataGridViewColumn>().Select(col => col.HeaderText).ToArray();
            });
            return result;
        }
    }
}
