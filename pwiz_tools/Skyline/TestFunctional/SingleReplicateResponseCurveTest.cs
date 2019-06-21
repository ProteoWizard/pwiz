/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using System;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests the Calibration Curve where the standards are different labeled peptides spiked 
    /// into the same replicate. The actual test file has two replicates in it, and so the
    /// CalibrationCurve on the Peptide uses both replicates, but the CalibrationCurve on the PeptideResult
    /// uses a single replicate.
    /// </summary>
    [TestClass]
    public class SingleReplicateResponseCurveTest : AbstractFunctionalTest
    {
        private const string PRECURSOR_CONCENTRATIONS_VIEW = "PrecursorConcentrations";
        private const string CALIBRATION_CURVES_VIEW = "CalibrationCurves";
        private const string PRECURSOR_QUANTIFICATION_VIEW = "PrecursorQuantifications";

        [TestMethod]
        public void TestSingleReplicateResponseCurve()
        {
            TestFilesZip = @"TestFunctional\SingleReplicateResponseCurveTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SingleReplicateResponseCurveTest.sky")));
            RunUI(()=>SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = WaitForOpenForm<DocumentGridForm>();
            CreateViews(documentGrid);
            SetPrecursorConcentrations(documentGrid);
            ShowReplicateCalibrationCurveColumns(documentGrid);
            VerifyPrecursorQuantifications(documentGrid);
        }

        private void CreateViews(DocumentGridForm documentGrid)
        {
            // Create view "PrecursorConcentrations"
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors));
            WaitForConditionUI(() => documentGrid.IsComplete);
            var viewEditor = ShowDialog<ViewEditor>(documentGrid.DataboundGridControl.NavBar.CustomizeView);
            RunUI(() =>
            {
                viewEditor.ChooseColumnsTab.AddColumn(PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*")
                    .Property(nameof(Precursor.PrecursorConcentration)));
                viewEditor.ViewName = PRECURSOR_CONCENTRATIONS_VIEW;
            });
            OkDialog(viewEditor, viewEditor.OkDialog);

            // Create view "CalibrationCurves"
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides));
            WaitForConditionUI(() => documentGrid.IsComplete);
            viewEditor = ShowDialog<ViewEditor>(documentGrid.DataboundGridControl.NavBar.CustomizeView);
            RunUI(() =>
            {
                viewEditor.ChooseColumnsTab.AddColumn(
                    PropertyPath.Parse("Proteins!*.Peptides!*.CalibrationCurve"));
                viewEditor.ChooseColumnsTab.AddColumn(
                    PropertyPath.Parse("Proteins!*.Peptides!*.CalibrationCurve.PointCount"));
                viewEditor.ChooseColumnsTab.AddColumn(PropertyPath.Parse("Proteins!*.Peptides!*.Results!*.Value")
                    .Property(nameof(PeptideResult.ReplicateCalibrationCurve)));
                viewEditor.ChooseColumnsTab.AddColumn(
                    PropertyPath.Parse("Proteins!*.Peptides!*.Results!*.Value.ReplicateCalibrationCurve.PointCount"));
                viewEditor.ViewName = CALIBRATION_CURVES_VIEW;
            });
            OkDialog(viewEditor, viewEditor.OkDialog);

            // Create view "PrecursorConcentrations"
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors));
            WaitForConditionUI(() => documentGrid.IsComplete);
            viewEditor = ShowDialog<ViewEditor>(documentGrid.DataboundGridControl.NavBar.CustomizeView);
            RunUI(() =>
            {
                viewEditor.ChooseColumnsTab.AddColumn(PropertyPath.Parse("Proteins!*.Peptides!*.Results!*.Value")
                    .Property(nameof(PeptideResult.ReplicateCalibrationCurve)));
                viewEditor.ChooseColumnsTab.AddColumn(
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value")
                        .Property(nameof(PrecursorResult.PrecursorQuantification)));
                viewEditor.ChooseColumnsTab.AddColumn(
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value"));
                viewEditor.ViewName = PRECURSOR_QUANTIFICATION_VIEW;
            });
            OkDialog(viewEditor, viewEditor.OkDialog);
        }

        /// <summary>
        /// Use the DocumentGrid to set the PrecursorConcentration values on all of the precursors in the document.
        /// </summary>
        private void SetPrecursorConcentrations(DocumentGridForm documentGrid)
        {
            RunUI(() => documentGrid.ChooseView(PRECURSOR_CONCENTRATIONS_VIEW));
            WaitForConditionUI(() => documentGrid.IsComplete);
            var colPrecursorConcentration = documentGrid.DataboundGridControl.FindColumn(
                PropertyPath.Root.Property(nameof(Precursor.PrecursorConcentration)));
            Assert.IsNotNull(colPrecursorConcentration);
            // These are the concentrations of the light, heavy4, heavy3, heavy2, heavy1
            double[] concentrations = { 0.125, 200, 20, 2, .5 };
            // Set the clipboard text to the list of concentrations repeated twice since there are two peptides
            ClipboardEx.SetText(string.Join(Environment.NewLine, concentrations.Concat(concentrations)));
            RunUI(() =>
            {
                documentGrid.DataGridView.CurrentCell =
                    documentGrid.DataGridView.Rows[0].Cells[colPrecursorConcentration.Index];
                documentGrid.DataGridView.SendPaste();
            });
        }

        /// <summary>
        /// Shows the Peptide.CalibrationCurve and PeptideResult.ReplicateCalibrationCurve columns, 
        /// and clicks on them to bring up the CalibrationForm.
        /// </summary>
        private void ShowReplicateCalibrationCurveColumns(DocumentGridForm documentGrid)
        {
            RunUI(() => documentGrid.ChooseView(CALIBRATION_CURVES_VIEW));
            WaitForConditionUI(() => documentGrid.IsComplete);
            var colCalibrationCurve = documentGrid.FindColumn(PropertyPath.Parse("CalibrationCurve"));
            var colPointCount = documentGrid.FindColumn(PropertyPath.Parse("CalibrationCurve.PointCount"));
            var colReplicateCalibrationCurve =
                documentGrid.FindColumn(PropertyPath.Parse("Results!*.Value.ReplicateCalibrationCurve"));
            var colReplicatePointCount 
                = documentGrid.FindColumn(PropertyPath.Parse("Results!*.Value.ReplicateCalibrationCurve.PointCount"));
            RunUI(() =>
            {
                foreach (var row in documentGrid.DataGridView.Rows.Cast<DataGridViewRow>())
                {
                    int pointCount = (int) row.Cells[colPointCount.Index].Value;
                    int replicatePointCount = (int) row.Cells[colReplicatePointCount.Index].Value;
                    Assert.IsTrue(pointCount > replicatePointCount);
                }
            });
            RunUI(()=>
            {
                documentGrid.DataGridView.CurrentCell =
                    documentGrid.DataGridView.Rows[0].Cells[colReplicateCalibrationCurve.Index];
                documentGrid.DataGridView.SendKeyDownUp(new KeyEventArgs(Keys.Space));
            });
            var calibrationForm = WaitForOpenForm<CalibrationForm>();
            Assert.IsNotNull(calibrationForm);
            Assert.IsTrue(Settings.Default.CalibrationCurveOptions.SingleBatch);
            RunUI(() =>
            {
                documentGrid.DataGridView.CurrentCell =
                    documentGrid.DataGridView.Rows[0].Cells[colCalibrationCurve.Index];
                documentGrid.DataGridView.SendKeyDownUp(new KeyEventArgs(Keys.Space));
            });
            Assert.IsFalse(Settings.Default.CalibrationCurveOptions.SingleBatch);
        }

        private void VerifyPrecursorQuantifications(DocumentGridForm documentGrid)
        {
            RunUI(() => documentGrid.ChooseView(PRECURSOR_QUANTIFICATION_VIEW));
            WaitForConditionUI(() => documentGrid.IsComplete);

            var pathPrecursorResult = PropertyPath.Parse("Results!*.Value");
            var colReplicateCalibrationCurve = documentGrid.FindColumn(pathPrecursorResult
                .Property(nameof(PrecursorResult.PeptideResult))
                .Property(nameof(PeptideResult.ReplicateCalibrationCurve)));
            var colQuantificationResult = documentGrid.FindColumn(pathPrecursorResult
                .Property(nameof(PrecursorResult.PrecursorQuantification)));
            var colPrecursorResult = documentGrid.FindColumn(pathPrecursorResult);
            var colPrecursor = documentGrid.FindColumn(PropertyPath.Root);
            RunUI(() =>
            {
                foreach (var row in documentGrid.DataGridView.Rows.Cast<DataGridViewRow>())
                {
                    var replicateCalibrationCurve =
                        (LinkValue<CalibrationCurve>) row.Cells[colReplicateCalibrationCurve.Index].Value;
                    var quantificationResult =
                        (LinkValue<QuantificationResult>) row.Cells[colQuantificationResult.Index].Value;
                    var precursor = (Precursor) row.Cells[colPrecursor.Index].Value;
                    var precursorResult = (PrecursorResult) row.Cells[colPrecursorResult.Index].Value;
                    var totalArea = precursorResult.TotalArea;
                    var calculatedConcentration = replicateCalibrationCurve.Value.GetFittedX(totalArea);
                    Assert.AreEqual(calculatedConcentration.Value, quantificationResult.Value.CalculatedConcentration.Value, .0001);
                    var expectedConcentration = precursorResult.Precursor.PrecursorConcentration;
                    var accuracy = calculatedConcentration / expectedConcentration;
                    Assert.AreEqual(accuracy.Value, quantificationResult.Value.Accuracy.Value, .0001);
                }
            });

        }
    }
}
