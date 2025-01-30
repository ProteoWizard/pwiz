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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Results.Imputation;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakImputationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakImputation()
        {
            TestFilesZip = @"TestFunctional\PeakImputationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PeakImputationTest.sky"));
                SkylineWindow.ShowPeakImputation();
            });
            WaitForDocumentLoaded();
            var peakImputationForm = FindOpenForm<PeakImputationForm>();
            RunUI(() =>
            {
                peakImputationForm.RtCalculatorName = RtValueType.PEAK_APEXES.Name;
            });
            WaitForCondition(() => peakImputationForm.IsComplete);
            GraphSummary graphSummaryScoreToRunRegression = null;
            RunUI(()=>
            {
                peakImputationForm.DisplayRetentionTimeRegression();
                graphSummaryScoreToRunRegression =
                    SkylineWindow.ListGraphRetentionTime.FirstOrDefault(graph =>
                        graph.Type == GraphTypeSummary.score_to_run_regression);
            });
            Assert.IsNotNull(graphSummaryScoreToRunRegression);
            WaitForGraphs();
            WaitForConditionUI(() =>
            {
                Assert.IsTrue(graphSummaryScoreToRunRegression.TryGetGraphPane(
                    out RTLinearRegressionGraphPane linearRegressionGraphPane));
                return !linearRegressionGraphPane.IsCalculating;
            });
            RunUI(() =>
            {
                Assert.IsTrue(graphSummaryScoreToRunRegression.TryGetGraphPane(
                    out RTLinearRegressionGraphPane linearRegressionGraphPane));
                Assert.IsFalse(linearRegressionGraphPane.IsCalculating);
                Assert.AreEqual(RtValueType.PEAK_APEXES.ToString(), linearRegressionGraphPane.XAxis.Title.Text);
            });
            RunUI(()=>
            {
                peakImputationForm.AlignAllGraphs = true;
            });
            WaitForGraphs();
            RunUI(() =>
            {
                peakImputationForm.RtCalculatorName = RtValueType.PSM_TIMES.Name;
                peakImputationForm.DocumentWide = true;
                peakImputationForm.MaxRTDeviation = null;
                peakImputationForm.MaxPeakWidthVariation = null;
            });
            WaitForConditionUI(() => peakImputationForm.IsComplete);
            RunUI(() =>
            {
                Assert.AreEqual(SkylineWindow.Document.MoleculeCount, peakImputationForm.DataboundGridControl.RowCount);
                Assert.AreEqual(SkylineWindow.Document.MoleculeCount, peakImputationForm.ExemplaryCount);
                Assert.AreEqual(0, peakImputationForm.NeedAdjustmentCount);
                Assert.AreEqual(0, peakImputationForm.NeedRemovalCount);

                peakImputationForm.MaxRTDeviation = 1;
                Assert.AreEqual(1.0, SkylineWindow.Document.Settings.PeptideSettings.Imputation.MaxRtShift);
            });
            WaitForConditionUI(() => peakImputationForm.IsComplete);
            RunUI(() =>
            {
                Assert.AreNotEqual(0, peakImputationForm.NeedAdjustmentCount);
                peakImputationForm.ImputeBoundariesForAllRows();
            });
            WaitForConditionUI(() => peakImputationForm.IsComplete);
            RunUI(() =>
            {
                Assert.AreEqual(0, peakImputationForm.NeedAdjustmentCount);
            });
        }
    }
}
