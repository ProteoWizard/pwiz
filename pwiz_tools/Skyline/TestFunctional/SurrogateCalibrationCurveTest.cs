/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using ZedGraph;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;
using SampleType = pwiz.Skyline.Model.DocSettings.AbsoluteQuantification.SampleType;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SurrogateCalibrationCurveTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSurrogateCalibrationCurve()
        {
            TestFilesZip = @"TestFunctional\SurrogateCalibrationCurveTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SurrogateCalibrationCurveTest.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ChooseColumnsTab.RemoveColumns(0, viewEditor.ChooseColumnsTab.ColumnCount);
                var ppPeptide = PropertyPath.Root
                    .Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems();
                viewEditor.ChooseColumnsTab.AddColumn(ppPeptide);
                viewEditor.ChooseColumnsTab.AddColumn(ppPeptide.Property(nameof(Peptide.StandardType)));
                viewEditor.ChooseColumnsTab.AddColumn(ppPeptide.Property(nameof(Peptide.SurrogateExternalStandard)));
                viewEditor.ChooseColumnsTab.AddColumn(ppPeptide.Property(nameof(Peptide.CalibrationCurve)));
                viewEditor.ViewName = "Surrogate Calibration Curves";
                viewEditor.OkDialog();
            });
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                Assert.AreEqual(2, documentGrid.RowCount);
                var colStandardType = documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Peptide.StandardType)));
                Assert.IsNotNull(colStandardType);
                var colSurrogateCalibrationCurve =
                    documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Peptide.SurrogateExternalStandard)));
                Assert.IsNotNull(colSurrogateCalibrationCurve);
                var dataGrid = documentGrid.DataGridView;
                SetCellValue(dataGrid.Rows[0].Cells[colStandardType.Index], StandardType.SURROGATE_STANDARD.ToString());
                SetCellValue(dataGrid.Rows[1].Cells[colSurrogateCalibrationCurve.Index], SkylineWindow.Document.Molecules.First().ModifiedTarget.InvariantName);
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 1);
                SkylineWindow.ShowCalibrationForm();
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
            int calibrationPointCount = 0;
            RunUI(() =>
            {
                var colCalibrationCurve =
                    documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Peptide.CalibrationCurve)));
                Assert.IsNotNull(colCalibrationCurve);
                var dataGrid = documentGrid.DataGridView;
                var calCurve1 =
                    ((LinkValue<CalibrationCurveMetrics>)dataGrid.Rows[0].Cells[colCalibrationCurve.Index].Value).Value;
                var calCurve2 = ((LinkValue<CalibrationCurveMetrics>)dataGrid.Rows[1].Cells[colCalibrationCurve.Index].Value).Value;
                Assert.AreEqual(calCurve1.Slope, calCurve2.Slope);
                Assert.AreEqual(calCurve1.Intercept, calCurve2.Intercept);
                Assert.IsNotNull(calCurve1.PointCount);
                calibrationPointCount = calCurve1.PointCount.Value;
                Assert.AreEqual(calCurve1.PointCount, calCurve2.PointCount);
            });
            WaitForGraphs();
            RunUI(() =>
            {
                var calibrationForm = FindOpenForm<CalibrationForm>();
                Assert.IsNotNull(calibrationForm);
                var surrogateStandard = SkylineWindow.Document.Molecules.First();
                Assert.AreEqual(StandardType.SURROGATE_STANDARD, surrogateStandard.GlobalStandardType);
                var unknownCurve = FindCurve(calibrationForm.ZedGraphControl, SampleType.UNKNOWN.ToString());
                Assert.IsNotNull(unknownCurve);
                var standardCurve = FindCurve(calibrationForm.ZedGraphControl,
                    CalibrationGraphControl.QualifyCurveNameWithSurrogate(SampleType.STANDARD.ToString(), surrogateStandard));
                Assert.IsNotNull(standardCurve);
                int indexFirstStandard = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.IndexOf(
                    chromatogramSet =>
                        SampleType.STANDARD.Equals(chromatogramSet.SampleType));
                calibrationForm.MakeExcludeStandardMenuItem(indexFirstStandard).PerformClick();
            });
            WaitForGraphs();
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                var colCalibrationCurve =
                    documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Peptide.CalibrationCurve)));
                Assert.IsNotNull(colCalibrationCurve);
                var dataGrid = documentGrid.DataGridView;
                var calCurve1 =
                    ((LinkValue<CalibrationCurveMetrics>)dataGrid.Rows[0].Cells[colCalibrationCurve.Index].Value).Value;
                var calCurve2 = ((LinkValue<CalibrationCurveMetrics>)dataGrid.Rows[1].Cells[colCalibrationCurve.Index].Value).Value;
                Assert.AreEqual(calibrationPointCount - 1, calCurve1.PointCount);
                Assert.AreEqual(calibrationPointCount - 1, calCurve2.PointCount);
            });
        }

        private void SetCellValue(DataGridViewCell cell, object value)
        {
            var dataGridView = cell.DataGridView;
            IDataGridViewEditingControl editingControl = null;
            DataGridViewEditingControlShowingEventHandler onEditingControlShowing =
                (sender, args) =>
                {
                    Assume.IsNull(editingControl);
                    editingControl = args.Control as IDataGridViewEditingControl;
                };
            try
            {
                dataGridView.EditingControlShowing += onEditingControlShowing;
                dataGridView.CurrentCell = cell;
                dataGridView.BeginEdit(true);
                Assert.IsNotNull(editingControl);
                editingControl.EditingControlFormattedValue = value;
                Assert.IsTrue(dataGridView.EndEdit());
            }
            finally
            {
                dataGridView.EditingControlShowing -= onEditingControlShowing;
            }
        }

        private CurveItem FindCurve(ZedGraphControl graph, string labelText)
        {
            return graph.GraphPane.CurveList.FirstOrDefault(curve => curve.Label.Text == labelText);
        }
    }
}
