/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RetentionTimeRegressionExceptionTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRetentionTimeRegressionException()
        {
            TestFilesZip = @"TestFunctional\RetentionTimeRegressionExceptionTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RtRegressionBugSmall.sky"));
                SkylineWindow.SelectedResultsIndex = 1;
                SkylineWindow.ShowSingleReplicate();
                Settings.Default.RTCalculatorName = "TTOF_64w_iRT-C18";
            });
            WaitForDocumentLoaded();
            // Verify the score to run regression graph
            RunLongDlg<GraphSummary>(SkylineWindow.ShowRTRegressionGraphScoreToRun, _ =>
            {
                WaitForRegression();
                var outliers = SkylineWindow.RTGraphController.Outliers;
                // There are two outlier PeptideDocNode's with the same peptide sequence.
                // One has a chosen peak boundary far from its predicted time, and the other
                // has a missing peak
                Assert.AreEqual(2, outliers.Length);
                Assert.AreEqual("SVDKTEK", outliers[0].Peptide.Sequence);
                Assert.AreEqual("SVDKTEK", outliers[1].Peptide.Sequence);
            }, graphSummary => graphSummary.Close());

            // Verify the run to run regression graph
            RunLongDlg<GraphSummary>(SkylineWindow.ShowRTRegressionGraphRunToRun, graphSummary =>
            {
                RunUI(() =>
                {
                    SkylineWindow.SelectedResultsIndex = 0;
                    graphSummary.SetResultIndexes(0, 1);
                });
                WaitForRegression();
                RunUI(() =>
                {
                    SkylineWindow.SelectedResultsIndex = 1;
                    graphSummary.SetResultIndexes(1, 0);
                });
                WaitForRegression();
            }, graphSummary => graphSummary.Close());
        }
    }
}
