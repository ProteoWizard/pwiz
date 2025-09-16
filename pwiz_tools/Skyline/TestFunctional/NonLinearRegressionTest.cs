/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class NonLinearRegressionTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestNonlinearRegression()
        {
            TestFilesZip = @"TestFunctional\RunToRunRegressionTest.zip";

            RunFunctionalTest();
        }

        public const string CALCULATOR_NAME = "SSRCalc 3.0 (300A)";

        protected override void DoTest()
        {
            TestFilesZip = @"TestFunctional\RunToRunRegressionTest.zip";

            var documentPath = TestFilesDir.GetTestPath("alpha-crystallin_data.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            WaitForDocumentLoaded();

            Settings.Default.RTRefinePeptides = false;

            var summary =  ShowDialog<GraphSummary>(() => SkylineWindow.ShowRTRegressionGraphScoreToRun());

            RunUI(() => SkylineWindow.ChooseCalculator(CALCULATOR_NAME));

            CheckNonlinearRegressionMethods(summary);
            
            //Check that Loess and KDE do not try to refine even if setting is true.
            Settings.Default.RTRefinePeptides = true;
            CheckNonlinearRegressionMethods(summary);
            TestRunToRunRegression();
        }

        private void CheckNonlinearRegressionMethods(GraphSummary summary)
        {
            //Check rmsd and number of linear functions for KDE
            RunUI(() => SkylineWindow.ShowRegressionMethod(RegressionMethodRT.kde));
            WaitForPaneCondition<RTLinearRegressionGraphPane>(summary, pane => !pane.IsCalculating);

            RTLinearRegressionGraphPane graphPane;
            summary.TryGetGraphPane(out graphPane);

            //KDE should never be refined. Too slow
            Assert.IsFalse(graphPane.IsRefined);

            var kdeFunction = (PiecewiseLinearRegressionFunction) graphPane.RegressionRefined.Conversion;

            Assert.AreEqual(5.7326, kdeFunction.RMSD, 0.0001);
            Assert.AreEqual(22, kdeFunction.LinearFunctionsCount);

            //Check for Loess

            RunUI(() => SkylineWindow.ShowRegressionMethod(RegressionMethodRT.loess));
            WaitForPaneCondition<RTLinearRegressionGraphPane>(summary, pane => !pane.IsCalculating);

            //Make sure Loess is not refined. Too slow
            Assert.IsTrue(graphPane.RegressionRefinedNull);

            // ReSharper disable once PossibleInvalidCastException
            var loessFunction = (LoessRegression) graphPane.RegressionRefined.Conversion;

            Assert.AreEqual(4.0552, loessFunction.Rmsd, 0.0001);
            Assert.AreEqual(22, kdeFunction.LinearFunctionsCount);
        }

        void TestRunToRunRegression()
        {
            RunUI(SkylineWindow.ShowRTRegressionGraphRunToRun);
            var runToRunGraphSummary = FormUtil.OpenForms.OfType<GraphSummary>()
                .FirstOrDefault(graph => graph.Type == GraphTypeSummary.run_to_run_regression);
            Assert.IsNotNull(runToRunGraphSummary, "Unable to find run_to_run_regression graph");

            Assert.IsTrue(runToRunGraphSummary.TryGetGraphPane(out RTLinearRegressionGraphPane regressionPane));
            Assert.IsTrue(regressionPane.HasToolbar);

            RunUI(() =>
            {
                SkylineWindow.ShowRegressionMethod(RegressionMethodRT.kde);
            });
            // Verify that no errors occur when doing a regression between the first
            // replicate and each of the next five
            for (int i = 1; i < 6; i++)
            {
                RunUI(() =>
                {
                    var toolbar = (RunToRunRegressionToolbar)runToRunGraphSummary.Toolbar;
                    toolbar.RunToRunOriginalReplicate.SelectedIndex = 0;
                    toolbar.RunToRunTargetReplicate.SelectedIndex = i;
                });
                WaitForGraphs();
                WaitForConditionUI(() => regressionPane._progressBar == null);
            }
        }
    }
}
