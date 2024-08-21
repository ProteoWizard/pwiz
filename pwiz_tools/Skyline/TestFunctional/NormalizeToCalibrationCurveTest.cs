/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA *
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class NormalizeToCalibrationCurveTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestNormalizeToCalibrationCurve()
        {
            TestFilesZip = @"TestFunctional\NormalizeToCalibrationCurveTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("p180test_calibration_DukeApril2016.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            WaitForDocumentLoaded();

            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.ChooseView("NormalizedAreas"));
            WaitForConditionUI(() => documentGrid.IsComplete);

            // Read the Normalized Area and Calculated Concentration for each peptide from the Document Grid

            // Dictionary of <IdentityPath, Replicate Name> to <Normalized Area, Calculated Concentration>
            var normalizedAreas = new Dictionary<Tuple<IdentityPath, string>, Tuple<double?, double?>>();
            RunUI(() =>
            {
                var colMolecule = documentGrid.FindColumn(PropertyPath.Root);
                PropertyPath ppResults = PropertyPath.Root
                    .Property(nameof(Skyline.Model.Databinding.Entities.Peptide.Results)).DictionaryValues();
                var colReplicate = documentGrid.FindColumn(ppResults.Property(nameof(PeptideResult.ResultFile))
                    .Property(nameof(ResultFile.Replicate)));

                PropertyPath ppQuantification = ppResults.Property(nameof(PeptideResult.Quantification));
                var colNormalizedArea =
                    documentGrid.FindColumn(ppQuantification.Property(nameof(QuantificationResult.NormalizedArea)));
                var colCalculatedConcentration =
                    documentGrid.FindColumn(
                        ppQuantification.Property(nameof(QuantificationResult.CalculatedConcentration)));

                for (int rowIndex = 0; rowIndex < documentGrid.RowCount; rowIndex++)
                {
                    var row = documentGrid.DataGridView.Rows[rowIndex];
                    Skyline.Model.Databinding.Entities.Peptide molecule =
                        (Skyline.Model.Databinding.Entities.Peptide) row.Cells[colMolecule.Index].Value;
                    Assert.IsNotNull(molecule);
                    Replicate replicate = (Replicate) row.Cells[colReplicate.Index].Value;
                    var normalizedArea = (AnnotatedDouble) row.Cells[colNormalizedArea.Index].Value;
                    var calculatedConcentration = (AnnotatedDouble) row.Cells[colCalculatedConcentration.Index].Value;
                    normalizedAreas.Add(Tuple.Create(molecule.IdentityPath, replicate.Name), Tuple.Create(normalizedArea?.Strict, calculatedConcentration?.Strict));
                }
            });

            // Show the PeakAreaCVHistogram graph
            RunUI(()=>SkylineWindow.ShowPeakAreaCVHistogram());
            WaitForConditionUI(() => null != FindGraphPane<AreaCVHistogramGraphPane>());
            AreaCVHistogramGraphPane areaCVHistogramGraphPane = FindGraphPane<AreaCVHistogramGraphPane>();

            // Change "Normalize To" to "Calibration Curve"
            RunUI(()=>SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.CALIBRATED));
            WaitForConditionUI(() =>
                areaCVHistogramGraphPane.CurrentData?.GraphSettings?.NormalizeOption == NormalizeOption.CALIBRATED);

            // Verify that the CVs that the histogram is displaying are correct for the calculated concentrations
            var graphData = areaCVHistogramGraphPane.CurrentData;
            foreach (var cvData in graphData.Data)
            {
                var peptideAnnotationPair = cvData.PeptideAnnotationPairs.First();
                var identityPath = new IdentityPath(peptideAnnotationPair.PeptideGroup.PeptideGroup, peptideAnnotationPair.Peptide.Peptide);
                var entries = normalizedAreas.Where(entry => Equals(entry.Key.Item1, identityPath)).ToList();
                var statistics = new Statistics(entries.Select(entry => entry.Value.Item2).OfType<double>());
                var cv = statistics.StdDev() / statistics.Mean();
                var cvBucketed = Math.Floor(cv / graphData.GraphSettings.BinWidth) * graphData.GraphSettings.BinWidth;
                AssertEx.AreEqual(cvBucketed, cvData.CV);
            }

            // Change "Normalize To" to "Default Normalization Method"
            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.DEFAULT));
            WaitForConditionUI(() =>
                areaCVHistogramGraphPane.CurrentData?.GraphSettings?.NormalizeOption == NormalizeOption.DEFAULT);
            graphData = areaCVHistogramGraphPane.CurrentData;

            // Verify that the CVs are correct for the Normalized Area values.
            foreach (var cvData in graphData.Data)
            {
                var peptideAnnotationPair = cvData.PeptideAnnotationPairs.First();
                var identityPath = new IdentityPath(peptideAnnotationPair.PeptideGroup.PeptideGroup, peptideAnnotationPair.Peptide.Peptide);
                var entries = normalizedAreas.Where(entry => Equals(entry.Key.Item1, identityPath)).ToList();
                var statistics = new Statistics(entries.Select(entry => entry.Value.Item1).OfType<double>());
                var cv = statistics.StdDev() / statistics.Mean();
                var cvBucketed = Math.Floor(cv / graphData.GraphSettings.BinWidth) * graphData.GraphSettings.BinWidth;
                AssertEx.AreEqual(cvBucketed, cvData.CV);
            }

            // Show the Peak Area Replicate Comparison graph
            RunUI(()=>
            {
                SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.CALIBRATED);
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });
            var peakAreaGraph = FindGraphPane<AreaReplicateGraphPane>();

            // Make sure that the calculated concentrations displayed in the graph are correct for each molecule in the document
            foreach (var moleculeGroup in SkylineWindow.Document.MoleculeGroups)
            {
                foreach (var molecule in moleculeGroup.Molecules)
                {
                    var idPath = new IdentityPath(moleculeGroup.Id, molecule.Id);
                    RunUI(()=>SkylineWindow.SelectedPath = idPath);
                    WaitForGraphs();
                    RunUI(() =>
                    {
                        var curve = peakAreaGraph.CurveList.First();
                        var textLabels = peakAreaGraph.XAxis.Scale.TextLabels;
                        Assert.IsNotNull(textLabels);
                        Assert.AreEqual(textLabels.Length, curve.Points.Count);
                        Assert.AreEqual(SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count, curve.Points.Count);
                        for (int i = 0; i < curve.Points.Count; i++)
                        {
                            var replicateName = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[i].Name;
                            var value = curve.Points[i].Y;
                            Tuple<double?, double?> expected;
                            Assert.IsTrue(normalizedAreas.TryGetValue(Tuple.Create(idPath, replicateName), out expected));
                            if (expected.Item2.HasValue && !double.IsInfinity(expected.Item2.Value))
                            {
                                AssertEx.AreEqual(expected.Item2.Value, value);
                            }
                            else
                            {
                                AssertEx.IsTrue(double.IsNaN(value) || Equals(value, PointPairBase.Missing));
                            }
                        }
                    });
                }
            }
        }

        private T FindGraphPane<T>() where T : class
        {
            foreach (var graphSummary in FormUtil.OpenForms.OfType<GraphSummary>())
            {
                T pane;
                if (graphSummary.TryGetGraphPane(out pane))
                {
                    return pane;
                }
            }

            return null;
        }
    }
}
