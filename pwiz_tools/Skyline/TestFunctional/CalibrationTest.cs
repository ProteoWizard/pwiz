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
using pwiz.Skyline.SettingsUI;
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
            RunUI(() => SkylineWindow.ShowCalibrationForm());
            var calibrationForm = FindOpenForm<CalibrationForm>();
            RunDlg<CalibrationCurveOptionsDlg>(calibrationForm.CalibrationGraphControl.ShowCalibrationCurveOptions,
                calibrationCurveOptionsDlg =>
                {
                    calibrationCurveOptionsDlg.DisplaySampleTypes = new[]
                        { SampleType.UNKNOWN, SampleType.QC, SampleType.STANDARD };
                    calibrationCurveOptionsDlg.OkDialog();
                });

            Assert.AreEqual(QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_No_results_available,
                calibrationForm.ZedGraphControl.GraphPane.Title.Text);
            PauseForScreenShot("Blank document");
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CalibrationTest.sky"));
            });
            Assert.AreEqual(QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_Select_a_peptide_to_see_its_calibration_curve,
                GetGraphTitle(calibrationForm));
            PauseForScreenShot("No peptide selected");

            RunUI(() => SkylineWindow.SequenceTree.SelectedPath = SkylineWindow.DocumentUI.GetPathTo(
                    (int)SrmDocument.Level.Molecules, 0));
            WaitForGraphs();
            AssertEx.AreEqual(QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_Use_the_Quantification_tab_on_the_Peptide_Settings_dialog_to_control_the_conversion_of_peak_areas_to_concentrations_,
                GetGraphTitle(calibrationForm));
            PauseForScreenShot("Quantification not configured");
            {
                var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() =>
                {
                    peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Quantification;
                    peptideSettingsUi.QuantNormalizationMethod = NormalizationMethod.GetNormalizationMethod(IsotopeLabelType.heavy);
                    peptideSettingsUi.QuantUnits = "ng/mL";
                });
                PauseForScreenShot("Peptide Settings - Quantification tab");
                OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            }
            PauseForScreenShot("Normalization without Internal Standard Concentration");
            WaitForOpenForm<CalibrationForm>();

            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            // Now, specify an internal standard concentration.
            RunUI(() => documentGrid.ChooseView("PeptidesWithCalibration"));
            WaitForConditionUI(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                var colInternalStandardQuantification =
                    documentGrid.FindColumn(PropertyPath.Root.Property("InternalStandardConcentration"));
                documentGrid.DataGridView.Rows[0].Cells[colInternalStandardQuantification.Index].Value = 80.0;
                SkylineWindow.ShowCalibrationForm();
            });
            Assert.IsNull(GetGraphTitle(calibrationForm));
            PauseForScreenShot("Internal calibration only");
            RunUI(() =>
            {
                var colInternalStandardQuantification =
                    documentGrid.FindColumn(PropertyPath.Root.Property("InternalStandardConcentration"));
                documentGrid.DataGridView.Rows[0].Cells[colInternalStandardQuantification.Index].Value = null;
            });
            FillInSampleTypesAndConcentrations();
            WaitForGraphs();
            {
                var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() =>
                {
                    peptideSettingsUi.QuantNormalizationMethod = NormalizationMethod.NONE;
                    peptideSettingsUi.QuantRegressionFit = RegressionFit.LINEAR;
                });
                OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
                RunUI(() => SkylineWindow.ShowCalibrationForm());
            }
            PauseForScreenShot("External Calibration Without Internal Standard");

            {
                var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() => peptideSettingsUi.QuantNormalizationMethod = NormalizationMethod.GetNormalizationMethod(IsotopeLabelType.heavy));
                OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            }
            PauseForScreenShot("External Calibration normalized to heavy");

            RunUI(() => documentGrid.ChooseView("PeptidesWithCalibration"));
            WaitForConditionUI(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                var colInternalStandardQuantification =
                    documentGrid.FindColumn(PropertyPath.Root.Property("InternalStandardConcentration"));
                documentGrid.DataGridView.Rows[0].Cells[colInternalStandardQuantification.Index].Value = 80.0;
                SkylineWindow.ShowCalibrationForm();
            });
            PauseForScreenShot("External calibration with internal standard concentration specified");
            TestAllQuantificationSettings();
        }

        private void TestAllQuantificationSettings()
        {
            RunUI(() => SkylineWindow.ShowCalibrationForm());
            var calibrationForm = FindOpenForm<CalibrationForm>();
            foreach (var quant in ListAllQuantificationSettings())
            {
                var quantValue = quant; // For ReSharper
                RunUI(() => SkylineWindow.ModifyDocument("Change Quantification Settings", doc => doc.ChangeSettings(
                    doc.Settings.ChangePeptideSettings(
                        doc.Settings.PeptideSettings.ChangeAbsoluteQuantification(quantValue)))));
                WaitForGraphs();
                var calibrationCurve = calibrationForm.CalibrationCurveMetrics;
                if (quant.MsLevel == 1 && quant.RegressionFit != RegressionFit.NONE)
                {
                    Assert.IsNotNull(calibrationCurve.ErrorMessage);
                }
                else
                {
                    Assert.IsNull(calibrationCurve.ErrorMessage);
                    if (quant.RegressionFit == RegressionFit.NONE)
                    {
                        Assert.AreEqual(0, calibrationCurve.PointCount);
                    }
                    else if (quant.RegressionFit == RegressionFit.LINEAR_THROUGH_ZERO)
                    {
                        Assert.IsNull(calibrationCurve.Intercept);
                        Assert.IsNotNull(calibrationCurve.Slope);
                        Assert.IsNull(calibrationCurve.QuadraticCoefficient);
                        Assert.IsNull(calibrationCurve.TurningPoint);
                    }
                    else if (quant.RegressionFit == RegressionFit.LINEAR)
                    {
                        Assert.IsNotNull(calibrationCurve.Intercept);
                        Assert.IsNotNull(calibrationCurve.Slope);
                        Assert.IsNull(calibrationCurve.QuadraticCoefficient);
                        Assert.IsNull(calibrationCurve.TurningPoint);
                    }
                    else if (quant.RegressionFit == RegressionFit.BILINEAR)
                    {
                        Assert.IsNotNull(calibrationCurve.Intercept);
                        Assert.IsNotNull(calibrationCurve.Slope);
                        Assert.IsNull(calibrationCurve.QuadraticCoefficient);
                        Assert.IsNotNull(calibrationCurve.TurningPoint);
                    }
                    else if (quant.RegressionFit == RegressionFit.QUADRATIC)
                    {
                        Assert.IsNotNull(calibrationCurve.Intercept);
                        Assert.IsNotNull(calibrationCurve.Slope);
                        Assert.IsNotNull(calibrationCurve.QuadraticCoefficient);
                        Assert.IsNull(calibrationCurve.TurningPoint);
                    }
                    else
                    {
                        Assert.AreEqual(RegressionFit.LINEAR_IN_LOG_SPACE, quant.RegressionFit);
                        Assert.IsNotNull(calibrationCurve.Intercept);
                        Assert.IsNotNull(calibrationCurve.Slope);
                        Assert.IsNull(calibrationCurve.QuadraticCoefficient);
                        Assert.IsNull(calibrationCurve.TurningPoint);
                    }
                }
            }
        }

        private void FillInSampleTypesAndConcentrations()
        {
            IDictionary<string, Tuple<SampleType, double?>> sampleTypes =
                new Dictionary<string, Tuple<SampleType, double?>>
                {
                    {"Solvent_", new Tuple<SampleType, double?>(SampleType.SOLVENT, null)},
                    {"Double Blank__VIFonly", new Tuple<SampleType, double?>(SampleType.DOUBLE_BLANK, null)},
                    {"Blank+IS__VIFonly", new Tuple<SampleType, double?>(SampleType.BLANK, null)},
                    {"Cal 1_0_20 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, .02)},
                    {"Cal 2_0_5 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, .05)},
                    {"Cal 3_1 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, .1)},
                    {"Cal 4_2 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, .2)},
                    {"Cal 5_5 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, .5)},
                    {"Cal 6_10 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, 1)},
                    {"Cal 7_25 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, 2.5)},
                    {"Cal 8_100 ng_mL_VIFonly", new Tuple<SampleType, double?>(SampleType.STANDARD, 10)},
                    {"Blank+IS__VIFonly (2)", new Tuple<SampleType, double?>(SampleType.BLANK, null)},
                    {"QC1__VIFonly", new Tuple<SampleType, double?>(SampleType.QC, null)},
                    {"QC2__VIFonly", new Tuple<SampleType, double?>(SampleType.QC, null)},
                    {"QC3__VIFonly", new Tuple<SampleType, double?>(SampleType.QC, null)},
                    {"QC4__VIFonly", new Tuple<SampleType, double?>(SampleType.QC, null)},
                };
            SetDocumentGridSampleTypesAndConcentrations(sampleTypes);
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
        private string GetGraphTitle(CalibrationForm calibrationForm)
        {
            return calibrationForm.ZedGraphControl.GraphPane.Title.Text;
        }
    }
}
