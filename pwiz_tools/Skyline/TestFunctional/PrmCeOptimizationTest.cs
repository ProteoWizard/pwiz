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
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PrmCeOptimizationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPrmCeOptimization()
        {
            TestFilesZip = @"TestFunctional\PrmCeOptimizationTest.zip";
            RunFunctionalTest();
        }
        protected override void DoTest()
        {
            var dir = TestFilesDir.GetTestPath(".");

            // Open the document.
            RunUI(() => SkylineWindow.OpenFile(Path.Combine(dir, "doc.sky")));

            var doc = SkylineWindow.Document;

            // Import results.
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() =>
            {
                importResultsDlg.NamedPathSets = DataSourceUtil.GetDataSources(dir).ToArray();
                importResultsDlg.OptimizationName = ExportOptimize.CE;
            });
            RunDlg<ImportResultsNameDlg>(importResultsDlg.OkDialog, dlg => dlg.OkDialog());
            doc = WaitForDocumentChangeLoaded(doc);

            var expected = new[]
            {
                new { Seq = "IGDYAGIK", Mz = 418.7293, Charge = 2, BestStep = -3 },
                new { Seq = "SISIVGSYVGNR", Mz = 626.3382, Charge = 2, BestStep = -1 },
                new { Seq = "VVGLSTLPEIYEK", Mz = 724.4058, Charge = 2, BestStep = -2 },
                new { Seq = "YVVDTSK", Mz = 406.2134, Charge = 2, BestStep = -3 }
            };


            // Verify results.
            const int replicateCount = 2;
            var chroms = doc.MeasuredResults.Chromatograms;
            Assert.AreEqual(replicateCount, chroms.Count);
            var optFunc = chroms[0].OptimizationFunction;
            Assert.AreEqual(OptimizationType.collision_energy, optFunc.OptType);
            Assert.AreEqual(optFunc, chroms[1].OptimizationFunction);
            var ceRegression = optFunc as CollisionEnergyRegression;
            Assert.IsNotNull(ceRegression);
            float tolerance = (float)doc.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var peptideDocNode in doc.Molecules)
            {
                foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
                {
                    foreach (var chromatogramSet in doc.MeasuredResults.Chromatograms)
                    {
                        Assert.IsTrue(doc.MeasuredResults.TryLoadChromatogram(chromatogramSet, peptideDocNode,
                            transitionGroupDocNode, tolerance,
                            out var chromatogramGroupInfos));
                        Assert.AreEqual(1, chromatogramGroupInfos.Length);
                        var chromatogramGroupInfo = chromatogramGroupInfos[0];
                        foreach (var transitionDocNode in transitionGroupDocNode.Transitions)
                        {
                            var optStepChromatograms = chromatogramGroupInfo.GetAllTransitionInfo(transitionDocNode,
                                tolerance, chromatogramSet.OptimizationFunction, TransformChrom.raw);
                            AssertEx.AreEqual(7, CountChromatograms(optStepChromatograms));
                        }
                    }
                }
            }
            foreach (var expect in expected)
            {
                Find(doc, expect.Seq, expect.Mz, expect.Charge, out _, out var nodeGroup);

                var results = nodeGroup.Results;
                Assert.IsNotNull(results);
                Assert.AreEqual(replicateCount, results.Count);
                foreach (var result in results)
                {
                    // Verify results go from step -3 to 3.
                    Assert.IsTrue(Enumerable.Range(-3, 7).SequenceEqual(result.Select(r => (int)r.OptimizationStep)));
                    // Verify best step.
                    Assert.AreEqual(expect.BestStep, result.OrderByDescending(r => r.Area).First().OptimizationStep);
                }
            }

            // Enable optimize by precursor.
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, dlg =>
            {
                dlg.UseOptimized = true;
                dlg.OptimizeType = OptimizedMethodType.Precursor.GetLocalizedString();
                dlg.OkDialog();
            });
            doc = WaitForDocumentChange(doc);

            // Verify that SrmDocument.GetOptimizedCollisionEnergy returns the expected CE values.
            foreach (var expect in expected)
            {
                Find(doc, expect.Seq, expect.Mz, expect.Charge, out var nodePep, out var nodeGroup);
                var optCe = doc.GetOptimizedCollisionEnergy(nodePep, nodeGroup, null);
                var expectCe = ceRegression.GetCollisionEnergy(
                    Adduct.FromCharge(nodeGroup.PrecursorCharge, Adduct.ADDUCT_TYPE.proteomic), nodeGroup.PrecursorMz,
                    expect.BestStep);
                Assert.AreEqual(expectCe, optCe);
            }

            RunUI(() => SkylineWindow.SaveDocument());
        }

        void Find(SrmDocument document, string seq, double mz, int charge, out PeptideDocNode nodePep,
            out TransitionGroupDocNode nodeGroup)
        {
            Assert.AreEqual(1, document.PeptideGroupCount);
            nodePep = document.Peptides.FirstOrDefault(pep => pep.ModifiedSequence.Equals(seq));
            Assert.IsNotNull(nodePep, $"Peptide {seq} not found.");
            nodeGroup = nodePep.TransitionGroups.FirstOrDefault(nodeTranGroup =>
                nodeTranGroup.PrecursorCharge == charge && Math.Abs(nodeTranGroup.PrecursorMz - mz) < 0.001);
            Assert.IsNotNull(nodeGroup, $"Precursor with charge {charge} and m/z {mz} not found for peptide {seq}.");
        }

        private int CountChromatograms(OptStepChromatograms optStepChromatograms)
        {
            return Enumerable.Range(-optStepChromatograms.StepCount, optStepChromatograms.StepCount * 2 + 1)
                .Count(step => null != optStepChromatograms.GetChromatogramForStep(step));
        }
    }
}
