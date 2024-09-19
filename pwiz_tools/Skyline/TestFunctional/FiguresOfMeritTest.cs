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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    // ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
    // ReSharper disable AccessToForEachVariableInClosure
    [TestClass]
    public class FiguresOfMeritTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestFiguresOfMerit()
        {
            TestFilesZip = @"TestFunctional\FiguresOfMeritTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            int seed = (int) DateTime.Now.Ticks;
            // Console.WriteLine("FiguresOfMeritTest: using random seed {0}", seed);
            var random = new Random(seed);
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("FiguresOfMeritTest.sky")));
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(() =>
            {
                documentGrid.ChooseView("FiguresOfMerit");
            });
            var calibrationForm = ShowDialog<CalibrationForm>(()=>SkylineWindow.ShowCalibrationForm());
            Assert.IsNotNull(calibrationForm);
            var results = new List<Tuple<FiguresOfMeritOptions, ProteomicSequence, FiguresOfMerit>>();
            int count = 0;
            foreach (var options in EnumerateFiguresOfMeritOptions().OrderBy(x=>random.Next()).Take(10))
            {
                count++;
                bool doFullTest = count < 5;
                var newQuantification = SkylineWindow.Document.Settings.PeptideSettings.Quantification;
                // ReSharper disable once PossibleNullReferenceException
                newQuantification = newQuantification
                        .ChangeRegressionFit(options.RegressionFit)
                        .ChangeLodCalculation(options.LodCalculation)
                        .ChangeMaxLoqCv(options.MaxLoqCv)
                        .ChangeMaxLoqBias(options.MaxLoqBias);
                if (doFullTest)
                {
                    var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                    RunUI(() =>
                    {
                        peptideSettingsUi.QuantRegressionFit = options.RegressionFit;
                        peptideSettingsUi.QuantLodMethod = options.LodCalculation;
                        peptideSettingsUi.QuantMaxLoqBias = options.MaxLoqBias;
                        peptideSettingsUi.QuantMaxLoqCv = options.MaxLoqCv;
                    });
                    OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
                    if (!Equals(newQuantification, SkylineWindow.Document.Settings.PeptideSettings.Quantification))
                    {
                        Assert.AreEqual(newQuantification, SkylineWindow.Document.Settings.PeptideSettings.Quantification);
                    }
                }
                else
                {
                    RunUI(() =>
                    {
                        SkylineWindow.ModifyDocument("Test changed settings",
                            doc => doc.ChangeSettings(doc.Settings.ChangePeptideSettings(
                                doc.Settings.PeptideSettings.ChangeAbsoluteQuantification(newQuantification))));
                    });
                }
                WaitForConditionUI(() => documentGrid.IsComplete);
                var colPeptideModifiedSequence = documentGrid.DataGridView.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(col => col.HeaderText == ColumnCaptions.PeptideModifiedSequence);
                Assert.IsNotNull(colPeptideModifiedSequence);
                var colFiguresOfMerit = documentGrid.DataGridView.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(col => col.HeaderText == ColumnCaptions.FiguresOfMerit);
                Assert.IsNotNull(colFiguresOfMerit);
                var docContainer = new MemoryDocumentContainer();
                Assert.IsTrue(docContainer.SetDocument(SkylineWindow.Document, docContainer.Document));
                var dataSchema = new SkylineDataSchema(docContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer());
                foreach (var group in SkylineWindow.Document.MoleculeGroups)
                {
                    foreach (var peptide in group.Molecules)
                    {
                        var identityPath = new IdentityPath(group.Id, peptide.Id);
                        var peptideEntity = new Skyline.Model.Databinding.Entities.Peptide(dataSchema, identityPath);
                        VerifyFiguresOfMeritValues(options, peptideEntity);
                        ValidateFiguresOfMerit(options, peptideEntity.FiguresOfMerit);
                        results.Add(Tuple.Create(options, peptideEntity.ModifiedSequence, peptideEntity.FiguresOfMerit));
                        if (doFullTest)
                        {
                            RunUI(()=>SkylineWindow.SelectedPath = identityPath);
                            WaitForGraphs();
                        }
                    }
                }
            }
            foreach (var result in results)
            {
                foreach (var resultCompare in results)
                {
                    if (!Equals(result.Item2, resultCompare.Item2))
                    {
                        continue;
                    }
                    var options1 = result.Item1;
                    var options2 = resultCompare.Item1;
                    if (!Equals(options1.RegressionFit, options2.RegressionFit))
                    {
                        continue;
                    }
                    CompareLoq(result.Item1, result.Item3, resultCompare.Item1, resultCompare.Item3);
                }
            }
        }

        private IEnumerable<FiguresOfMeritOptions> EnumerateFiguresOfMeritOptions()
        {
            foreach (var regressionFit in new[] {RegressionFit.NONE, RegressionFit.LINEAR, RegressionFit.BILINEAR})
            {
                foreach (var lodCalculation in new[]
                {
                    LodCalculation.NONE, LodCalculation.TURNING_POINT, LodCalculation.BLANK_PLUS_2SD,
                    LodCalculation.BLANK_PLUS_3SD
                })
                {
                    if (lodCalculation == LodCalculation.TURNING_POINT && regressionFit != RegressionFit.BILINEAR)
                    {
                        continue;
                    }
                    foreach (var maxLoqBias in new double?[] {null, 0, 20, 1e6})
                    {
                        foreach (var maxLoqCv in new double?[] {null, 0, 20, 1e6})
                        {
                            yield return new FiguresOfMeritOptions
                            {
                                RegressionFit = regressionFit,
                                LodCalculation = lodCalculation,
                                MaxLoqBias = maxLoqBias,
                                MaxLoqCv = maxLoqCv
                            };
                        }
                    }
                }
            }
        }

        private void ValidateFiguresOfMerit(FiguresOfMeritOptions options, FiguresOfMerit figuresOfMerit)
        {
            if (options.MaxLoqBias.HasValue || options.MaxLoqCv.HasValue)
            {
                double min = Math.Min(options.MaxLoqBias.GetValueOrDefault(1000),
                    options.MaxLoqCv.GetValueOrDefault(1000));
                if (min <= 0)
                {
                    Assert.IsNull(figuresOfMerit.LimitOfQuantification);
                }
                if (min >= 1000)
                {
                    if (!Equals(RegressionFit.NONE, options.RegressionFit) || !options.MaxLoqBias.HasValue)
                    {
                        Assert.IsNotNull(figuresOfMerit.LimitOfQuantification);
                    }
                }
            }
            else
            {
                Assert.IsNull(figuresOfMerit.LimitOfQuantification);
            }
            if (options.LodCalculation == LodCalculation.TURNING_POINT)
            {
                if (options.RegressionFit == RegressionFit.BILINEAR)
                {
                    Assert.IsNotNull(figuresOfMerit.LimitOfDetection);
                }
                else
                {
                    Assert.IsNull(figuresOfMerit.LimitOfDetection);
                }
            }
        }

        private void VerifyFiguresOfMeritValues(FiguresOfMeritOptions options,
            Skyline.Model.Databinding.Entities.Peptide peptideEntity)
        {
            double? expectedLoq = GetLoq(options, peptideEntity);
            var actualLoq = peptideEntity.FiguresOfMerit.LimitOfQuantification;
            if (expectedLoq != actualLoq)
            {
                Assert.AreEqual(expectedLoq, actualLoq, "Options: {0}", options);
            }
        }

        private double? GetLoq(FiguresOfMeritOptions options, Skyline.Model.Databinding.Entities.Peptide peptideEntity)
        {
            var peptideResults = peptideEntity.Results.Values
                .Where(result => Equals(result.ResultFile.Replicate.SampleType, SampleType.STANDARD)
                                 && result.ResultFile.Replicate.AnalyteConcentration.HasValue)
                .ToLookup(result => result.ResultFile.Replicate.AnalyteConcentration.Value);
            if (!options.MaxLoqBias.HasValue && !options.MaxLoqCv.HasValue)
            {
                return null;
            }

            var calibrationCurveFitter = peptideEntity.GetCalibrationCurveFitter();
            var calibrationCurve = calibrationCurveFitter.GetCalibrationCurve();
            if (calibrationCurveFitter.FiguresOfMeritCalculator is BootstrapFiguresOfMeritCalculator bootstrapFiguresOfMeritCalculator)
            {
                return calibrationCurveFitter.GetFiguresOfMerit(calibrationCurve).LimitOfQuantification;
            }
            var concentrationMultiplier = peptideEntity.ConcentrationMultiplier.GetValueOrDefault(1);
            double? bestLoq = null;
            foreach (var grouping in peptideResults.OrderByDescending(g => g.Key))
            {
                if (options.MaxLoqBias.HasValue)
                {
                    var areas = grouping
                        .Select(peptideResult => peptideResult.Quantification.Value.NormalizedArea.Strict)
                        .Where(area => area.HasValue).Cast<double>().ToArray();
                    if (areas.Length == 0)
                    {
                        continue;
                    }
                    var meanArea = areas.Average();
                    var backCalculatedConcentration = calibrationCurve.GetXValueForLimitOfDetection(meanArea);
                    if (!backCalculatedConcentration.HasValue)
                    {
                        break;
                    }
                    var expectedConcentration = grouping.Key * concentrationMultiplier;
                    var error = Math.Abs(1.0 - backCalculatedConcentration.Value / expectedConcentration) * 100;
                    if (error > options.MaxLoqBias)
                    {
                        break;
                    }
                }

                if (options.MaxLoqCv.HasValue)
                {
                    var stats = new Statistics(grouping.Select(peptideResult =>
                        peptideResult.Quantification.Value.NormalizedArea.Strict).OfType<double>());
                    if (stats.Length > 1)
                    {
                        var cv = stats.StdDev() / stats.Mean();
                        if (double.IsNaN(cv) || cv * 100 > options.MaxLoqCv.Value)
                        {
                            break;
                        }
                    }
                }

                bestLoq = grouping.Key;
            }
            return bestLoq * concentrationMultiplier;
        }

        /// <summary>
        /// Asserts that if options1 uses less stringent criteria for calculating LOQ, then that
        /// must results in a lower resulting LOQ value.
        /// </summary>
        private void CompareLoq(FiguresOfMeritOptions options1, FiguresOfMerit result1, FiguresOfMeritOptions options2,
            FiguresOfMerit result2)
        {
            if (options1.MaxLoqCv.HasValue != options2.MaxLoqCv.HasValue)
            {
                return;
            }
            if (options1.MaxLoqBias.HasValue != options2.MaxLoqBias.HasValue)
            {
                return;
            }
            if (options1.MaxLoqCv < options2.MaxLoqCv || options1.MaxLoqBias < options2.MaxLoqBias)
            {
                return;
            }
            // we have determined that options1 is more lenient than options2
            Assert.IsTrue(result1.LimitOfQuantification.GetValueOrDefault(double.MaxValue) 
                <= result2.LimitOfQuantification.GetValueOrDefault(double.MaxValue));
        }

        struct FiguresOfMeritOptions
        {
            public RegressionFit RegressionFit;
            public LodCalculation LodCalculation;
            public double? MaxLoqBias;
            public double? MaxLoqCv;

            public override string ToString()
            {
                return string.Format("RegressionFit: {0} LodCalculation: {1} MaxLoqBias: {2} MaxLoqCv: {3}",
                    RegressionFit, LodCalculation, MaxLoqBias, MaxLoqCv);
            }
        }
    }
}
