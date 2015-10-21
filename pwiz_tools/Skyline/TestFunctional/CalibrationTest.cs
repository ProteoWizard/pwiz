/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class CalibrationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestCalibration()
        {
            TestFilesZip = @"TestFunctional\CalibrationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CalibrationTest.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            
            // First check that the "Quantification" column is all nulls before we have provided any way to do calibration
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() =>documentGrid.ChooseView("PeptideResultsWithQuantification"));
            WaitForConditionUI(() => documentGrid.IsComplete);
            PropertyPath ppQuantification =
                PropertyPath.Root.Property("Results").LookupAllItems().Property("Value").Property("Quantification");
            RunUI(() =>
            {
                var colQuantification = documentGrid.FindColumn(ppQuantification);
                for (int iRow = 0; iRow < documentGrid.RowCount; iRow++)
                {
                    QuantificationResult quantificationResult =
                        (QuantificationResult) documentGrid.DataGridView.Rows[iRow].Cells[colQuantification.Index].Value;
                    Assert.IsNotNull(quantificationResult);
                    Assert.AreEqual(quantificationResult.NormalizedIntensity, quantificationResult.CalculatedConcentration);
                    Assert.AreEqual(quantificationResult.ToString(), 
                        quantificationResult.CalculatedConcentration.Value.ToString(Formats.CalibrationCurve));
                    Assert.AreEqual(CalibrationCurve.NO_EXTERNAL_STANDARDS, quantificationResult.CalibrationCurve.Value);
                }
            });
            
            // Now, specify an internal standard concentration.
            RunUI(()=>documentGrid.ChooseView("PeptidesWithCalibration"));
            WaitForConditionUI(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                var colInternalStandardQuantification =
                    documentGrid.FindColumn(PropertyPath.Root.Property("InternalStandardConcentration"));
                documentGrid.DataGridView.Rows[0].Cells[colInternalStandardQuantification.Index].Value = 80.0;
                var colUnits = documentGrid.FindColumn(PropertyPath.Root.Property("ConcentrationUnits"));
                documentGrid.DataGridView.Rows[0].Cells[colUnits.Index].Value = "ng/mL";
            });

            // And specify that Quantification should use the ratio to heavy
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUI.QuantNormalizationMethod =
                    NormalizationMethod.GetNormalizationMethod(IsotopeLabelType.heavy);
                peptideSettingsUI.QuantRegressionFit = RegressionFit.LINEAR;
            });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            RunUI(() => documentGrid.ChooseView("PeptideResultsWithQuantification"));
            WaitForConditionUI(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                var colQuantification = documentGrid.FindColumn(ppQuantification);
                for (int iRow = 0; iRow < documentGrid.RowCount; iRow++)
                {
                    QuantificationResult quantificationResult =
                        (QuantificationResult)documentGrid.DataGridView.Rows[iRow].Cells[colQuantification.Index].Value;
                    Assert.IsNotNull(quantificationResult);
                    Assert.AreEqual(quantificationResult.NormalizedIntensity, quantificationResult.CalculatedConcentration);
                    Assert.AreEqual(quantificationResult.CalculatedConcentration.Value.ToString(Formats.Concentration) + " ng/mL", 
                        quantificationResult.ToString());
                    Assert.AreEqual(CalibrationCurve.NO_EXTERNAL_STANDARDS, quantificationResult.CalibrationCurve.Value);
                }
            });
            RunUI(() =>
                {
                    SkylineWindow.ShowCalibrationForm();
                }
            );
            var calibrationForm = FindOpenForm<CalibrationForm>();
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedPath = SkylineWindow.DocumentUI.GetPathTo(
                    (int) SrmDocument.Level.Molecules, 0);
            });
            WaitForGraphs();

            // Fill in the values for the external standards.
            FillInSampleTypesAndDilutionFactors();
            WaitForGraphs();
            CalibrationCurve calCurveWithoutStockConcentration = calibrationForm.CalibrationCurve;
            Assert.IsNull(calibrationForm.ZedGraphControl.GraphPane.Title.Text);
            Assert.AreEqual(QuantificationStrings.Calculated_Concentration, calibrationForm.ZedGraphControl.GraphPane.XAxis.Title.Text);
            RunUI(() =>
            {
                documentGrid.ChooseView("PeptidesWithCalibration");
            });
            WaitForConditionUI(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                var colStockConcentration =
                    documentGrid.FindColumn(PropertyPath.Root.Property("StockConcentration"));
                documentGrid.DataGridView.Rows[0].Cells[colStockConcentration.Index].Value = 100.0;
            });
            WaitForGraphs();
            Assert.AreEqual(TextUtil.SpaceSeparate(QuantificationStrings.Calculated_Concentration, "(ng/mL)"), 
                calibrationForm.ZedGraphControl.GraphPane.XAxis.Title.Text);
            Assert.AreEqual(calCurveWithoutStockConcentration.Slope.Value / 100, calibrationForm.CalibrationCurve.Slope.Value, 0.000001);
            TestAllQuantificationSettings();
        }

        private void TestAllQuantificationSettings()
        {
            RunUI(() => SkylineWindow.ShowCalibrationForm());
            var calibrationForm = FindOpenForm<CalibrationForm>();
            foreach (var quant in ListAllQuantificationSettings())
            {
                RunUI(() => SkylineWindow.ModifyDocument("Change Quantification Settings", doc => doc.ChangeSettings(
                    doc.Settings.ChangePeptideSettings(
                        doc.Settings.PeptideSettings.ChangeAbsoluteQuantification(quant)))));
                WaitForGraphs();
                CalibrationCurve calibrationCurve = calibrationForm.CalibrationCurve;
                if (quant.MsLevel == 1)
                {
                    Assert.IsNotNull(calibrationCurve.ErrorMessage);
                }
                else
                {
                    Assert.IsNull(calibrationCurve.ErrorMessage);
                    if (quant.RegressionFit == RegressionFit.LINEAR_THROUGH_ZERO)
                    {
                        Assert.IsNull(calibrationCurve.Intercept);
                        Assert.IsNotNull(calibrationCurve.Slope);
                        Assert.IsNull(calibrationCurve.QuadraticCoefficient);
                    }
                    else if (quant.RegressionFit == RegressionFit.LINEAR)
                    {
                        Assert.IsNotNull(calibrationCurve.Intercept);
                        Assert.IsNotNull(calibrationCurve.Slope);
                        Assert.IsNull(calibrationCurve.QuadraticCoefficient);
                    }
                    else
                    {
                        Assert.IsNotNull(calibrationCurve.Intercept);
                        Assert.IsNotNull(calibrationCurve.Slope);
                        Assert.IsNotNull(calibrationCurve.QuadraticCoefficient);
                    }
                }
            }
        }

        private void FillInSampleTypesAndDilutionFactors()
        {
            IDictionary<string, Tuple<SampleType, double?>> sampleTypes =
                new Dictionary<string, Tuple<SampleType, double?>>
                {
                    {"Solvent_", new Tuple<SampleType, double?>(SampleType.SOLVENT, null)},
                    {"Double Blank__VIFonly", new Tuple<SampleType, double?>(SampleType.DOUBLE_BLANK, null)},
                    {"Blank+IS__VIFonly", new Tuple<SampleType, double?>(SampleType.BLANK, null)},
                    {"Cal 1_0_20 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, 50)},
                    {"Cal 2_0_5 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, 20)},
                    {"Cal 3_1 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, 10)},
                    {"Cal 4_2 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, 5)},
                    {"Cal 5_5 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, 2)},
                    {"Cal 6_10 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, 1)},
                    {"Cal 7_25 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, 0.4)},
                    {"Cal 8_100 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, 0.1)},
                    {"Blank+IS__VIFonly (2)", new Tuple<SampleType, double?>(SampleType.BLANK, null)},
                    {"QC1__VIFonly", new Tuple<SampleType, double?>(SampleType.QC, null)},
                    {"QC2__VIFonly", new Tuple<SampleType, double?>(SampleType.QC, null)},
                    {"QC3__VIFonly", new Tuple<SampleType, double?>(SampleType.QC, null)},
                    {"QC4__VIFonly", new Tuple<SampleType, double?>(SampleType.QC, null)},
                };
            RunUI(()=>SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.DataboundGridControl.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                var colReplicate = documentGrid.FindColumn(PropertyPath.Root);
                var colSampleType = documentGrid.FindColumn(PropertyPath.Root.Property("SampleType"));
                var colDilutionFactor = documentGrid.FindColumn(PropertyPath.Root.Property("DilutionFactor"));
                for (int iRow = 0; iRow < documentGrid.RowCount; iRow++)
                {
                    var row = documentGrid.DataGridView.Rows[iRow];
                    var replicateName = row.Cells[colReplicate.Index].Value.ToString();
                    Tuple<SampleType, double?> tuple;
                    if (sampleTypes.TryGetValue(replicateName, out tuple))
                    {
                        row.Cells[colSampleType.Index].Value = tuple.Item1;
                        row.Cells[colDilutionFactor.Index].Value = tuple.Item2;
                    }
                }
            });
        }

        public static IEnumerable<QuantificationSettings> ListAllQuantificationSettings()
        {
            foreach (var normalizationMethod in
                new[] {NormalizationMethod.NONE, NormalizationMethod.GetNormalizationMethod(IsotopeLabelType.heavy),})
            {
                foreach (var regressionFit in RegressionFit.All)
                {
                    foreach (var weighting in RegressionWeighting.All)
                    {
                        foreach (var msLevel in new int?[] {1, 2, null})
                        {
                            yield return new QuantificationSettings(weighting)
                                .ChangeNormalizationMethod(normalizationMethod)
                                .ChangeRegressionFit(regressionFit)
                                .ChangeMsLevel(msLevel);
                        }
                    }
                }
            }
        }
    }
}
