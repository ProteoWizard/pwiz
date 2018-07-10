/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SummaryGraphVisibilityTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestSummaryGraphVisibility()
        {
            TestFilesZip = "TestFunctional/SummaryGraphVisibilityTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Test no summary graphs open by default
            OpenDocument(@"small.sky");
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsFalse(SkylineWindow.GraphRetentionTime != null && !SkylineWindow.GraphRetentionTime.IsHidden);
                Assert.IsFalse(SkylineWindow.GraphPeakArea != null && !SkylineWindow.GraphPeakArea.IsHidden);
                Assert.IsFalse(SkylineWindow.GraphMassError != null && !SkylineWindow.GraphMassError.IsHidden);
            });

            // Show some graphs and verify that they appear
            RunUI(SkylineWindow.ShowRTReplicateGraph);
            RunUI(SkylineWindow.ShowPeakAreaReplicateComparison);
            RunUI(SkylineWindow.ShowMassErrorReplicateComparison);
            RunUI(SkylineWindow.ShowRTRegressionGraphRunToRun);
            RunUI(SkylineWindow.ShowRTSchedulingGraph);
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.replicate));
                Assert.IsTrue(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.run_to_run_regression));
                Assert.IsTrue(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.schedule));
                Assert.IsTrue(!SkylineWindow.GraphPeakArea.IsHidden);
                Assert.IsTrue(!SkylineWindow.GraphMassError.IsHidden);
            });

            // Switch to a blank document (without saving) and verify that results-dependent graphs disappear
            RunUI(SkylineWindow.NewDocument);
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsFalse(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.replicate));
                Assert.IsFalse(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.run_to_run_regression));
                Assert.IsFalse(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.schedule));
                Assert.IsFalse(!SkylineWindow.GraphPeakArea.IsHidden);
                Assert.IsFalse(!SkylineWindow.GraphMassError.IsHidden);
            });

            // Re-open the document (still without .sky.view) and verify the same graphs become visible
            OpenDocument(@"small.sky");
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.replicate));
                Assert.IsTrue(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.run_to_run_regression));
                Assert.IsTrue(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.schedule));
                Assert.IsTrue(!SkylineWindow.GraphPeakArea.IsHidden);
                Assert.IsTrue(!SkylineWindow.GraphMassError.IsHidden);
            });

            // Remove one replicate and verify the run-to-run graph is hidden
            RunUI(() => SkylineWindow.ModifyDocument("Remove first replicate", doc =>
                doc.ChangeMeasuredResults(doc.MeasuredResults.ChangeChromatograms(doc.MeasuredResults.Chromatograms.Skip(1).ToArray()))));
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.replicate));
                Assert.IsFalse(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.run_to_run_regression));
                Assert.IsTrue(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.schedule));
                Assert.IsTrue(!SkylineWindow.GraphPeakArea.IsHidden);
                Assert.IsTrue(!SkylineWindow.GraphMassError.IsHidden);
            });
            // Modify the document and verify run-to-run graph does not come back, as it once did
            RemovePeptide("VQVTRPDQAR");
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsFalse(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.run_to_run_regression));
            });
            // Undo back twice to regain the second replicate and verify the run-to-run graph comes back
            RunUI(() =>
            {
                SkylineWindow.Undo();
                SkylineWindow.Undo();
            });
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.run_to_run_regression));
            });
            // Remove results and verify all the graphs go away
            RunUI(() => SkylineWindow.ModifyDocument("Remove results", doc => doc.ChangeMeasuredResults(null)));
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsFalse(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.replicate));
                Assert.IsFalse(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.run_to_run_regression));
                Assert.IsFalse(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.schedule));
                Assert.IsFalse(!SkylineWindow.GraphPeakArea.IsHidden);
                Assert.IsFalse(!SkylineWindow.GraphMassError.IsHidden);
            });
            // Add a retention time predictor and verify that the scheduling window comes back
            var ssrCalc = new RetentionScoreCalculator(RetentionTimeRegression.SSRCALC_100_A);
            RunUI(() => SkylineWindow.ModifyDocument("Add predictor", doc =>
                doc.ChangeSettings(doc.Settings.ChangePeptidePrediction(pred =>
                    pred.ChangeRetentionTime(new RetentionTimeRegression("Simple Predictor", ssrCalc,1, 0, 10, new MeasuredRetentionTime[0]))))));
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.schedule));
            });
            // Undo twice again to get everything back
            RunUI(() =>
            {
                SkylineWindow.Undo();
                SkylineWindow.Undo();
            });
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.replicate));
                Assert.IsTrue(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.run_to_run_regression));
                Assert.IsTrue(SkylineWindow.IsGraphRetentionTimeShown(GraphTypeSummary.schedule));
                Assert.IsTrue(!SkylineWindow.GraphPeakArea.IsHidden);
                Assert.IsTrue(!SkylineWindow.GraphMassError.IsHidden);
            });
            // Close everything
            RunUI(() =>
            {
                SkylineWindow.GraphRetentionTime.Close();
                SkylineWindow.GraphRetentionTime.Close();
                SkylineWindow.GraphRetentionTime.Close();
                SkylineWindow.GraphPeakArea.Close();
                SkylineWindow.GraphMassError.Close();
            });
            // New and then reopen the document and make sure the graphs stay closed
            RunUI(() =>
            {
                Assert.IsFalse(SkylineWindow.GraphRetentionTime != null && !SkylineWindow.GraphRetentionTime.IsHidden);
                Assert.IsFalse(SkylineWindow.GraphPeakArea != null && !SkylineWindow.GraphPeakArea.IsHidden);
                Assert.IsFalse(SkylineWindow.GraphMassError != null && !SkylineWindow.GraphMassError.IsHidden);
            });
            RunUI(SkylineWindow.NewDocument);
            WaitForGraphs();
            OpenDocument("small.sky");
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsFalse(SkylineWindow.GraphRetentionTime != null && !SkylineWindow.GraphRetentionTime.IsHidden);
                Assert.IsFalse(SkylineWindow.GraphPeakArea != null && !SkylineWindow.GraphPeakArea.IsHidden);
                Assert.IsFalse(SkylineWindow.GraphMassError != null && !SkylineWindow.GraphMassError.IsHidden);
            });
        }
    }
}