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
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RunToRunRegressionTest : AbstractFunctionalTest
    {
        private const int REPLICATES = 5; 

        [TestMethod]
        public void TestRunToRunRegression()
        {
            TestFilesZip = @"TestFunctional\RunToRunRegressionTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var targetPeptideCounts = new[,]
            {
                {0, 22, 22, 22, 22}, 
                {22, 0, 22, 22, 22}, 
                {22, 22, 0, 22, 22}, 
                {22, 22, 22, 0, 22}, 
                {22, 22, 22, 22, 0}
            };


            var originalPeptideCounts = new[,]
            {
                {0, 22, 22, 22, 22}, 
                {22, 0, 22, 22, 22}, 
                {22, 22, 0, 22, 22}, 
                {22, 22, 22, 0, 22}, 
                {22, 22, 22, 22, 0}
            };


            var rValues = new[,]
            {
                {0, 0.999993931908946, 0.999843506239357, 0.999839232254387, 0.999834925925893},
                {0.999993931908947, 0, 0.999843746240661, 0.999850167871986, 0.999845222422893},
                {0.999843506239357, 0.99984374624066, 0, 0.999994147903928, 0.999995277589245},
                {0.999839232254387, 0.999850167871986, 0.999994147903929, 0, 0.999998880406438},
                {0.999834925925894, 0.999845222422893, 0.999995277589245, 0.999998880406438, 0}
            };

            var slopes = new[,]
            {
                {0, 0.999707475441337, 1.00017889353965, 1.0006184891116, 1.00154337723693},
                {1.00028047045787, 0, 1.00046572523814, 1.00091615389789, 1.00184067681074},
                {0.999508231403589, 0.999222153920973, 0, 1.00043793880301, 1.00136810418741},
                {0.999060582263086, 0.998785317132195, 0.999550553868999, 0, 1.00092750683297},
                {0.998129388922859, 0.997853742557447, 0.998624333069067, 0.999071115527851, 0}
            };


            var intercepts = new[,]
            {
                {0, 0.0133084134167909, 0.056519079598754, 0.0422773381099439, 0.034396123070028},
                {-0.0130393823593842, 0, 0.0433540454886234, 0.0288681051767803, 0.0209988978057503},
                {-0.0494741792022388, -0.0363139773301882, 0, -0.0142038029294511, -0.0222056158333466},
                {-0.0350274788274447, -0.0221133023480569, 0.014459895361707, 0, -0.00794784994821995},
                {-0.0269327618715351, -0.0140162875262924, 0.0223867551509969, 0.00799065432053325, 0}
            };


            var windows = new[,]
            {
                {0, 0.5, 0.913480355580205, 0.925869324756884, 0.938186549030894},
                {0.5, 0, 0.913041225784304, 0.894083995681225, 0.908718421411426},
                {0.913174040667638, 0.912473597676775, 0, 0.5, 0.5}, {0.925148280694587, 0.893131787005536, 0.5, 0, 0.5},
                {0.936586172975179, 0.906908446828345, 0.5, 0.5, 0}
            };

            var documentPath = TestFilesDir.GetTestPath("alpha-crystallin_data.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            WaitForDocumentLoaded();

            var graphSummary = ShowDialog<GraphSummary>(SkylineWindow.ShowRTLinearRegressionGraphRunToRun);

            WaitForGraphs();

            Assert.IsTrue(graphSummary.IsRunToRun);
            RTLinearRegressionGraphPane regressionPane;
            if (!graphSummary.TryGetGraphPane(out regressionPane))
                Assert.Fail("First graph pane was not RTLinearRegressionGraphPane");
            
            //Assert all values in regression match
            for (var i = 0; i < REPLICATES; i++)
            {
                for (var j = 0; j < REPLICATES; j++)
                {
                    if (i == j)
                        continue;
                    int targetIndex = i, originalIndex = j;
                    RunUI(() =>
                    {
                        graphSummary.RunToRunTargetReplicate.SelectedIndex = targetIndex;
                        graphSummary.RunToRunOriginalReplicate.SelectedIndex = originalIndex;

                        Assert.AreEqual(targetIndex, graphSummary.StateProvider.SelectedResultsIndex);
                    });
                    WaitForGraphs();

                    var window = testRegressionStatisitcs(regressionPane, rValues, i, j, targetPeptideCounts, originalPeptideCounts, slopes, intercepts, windows);

                    RunUI(() =>
                    {
                        SkylineWindow.ShowPlotType(PlotTypeRT.residuals);
                    });

                    //Check that residual graph makes sense
                    var pointList = regressionPane.CurveList.First().Points;
                    var yList = new List<double>();
                    for(var p = 0; p < pointList.Count; p ++)
                    {
                        var point = pointList[p];
                        yList.Add(point.Y);
                    }

                    var residualStat = new Statistics(yList);

                    //Value taken from Prediction.CalcRegression
                    var windowFromResidualGraph = Math.Max(0.5, 4 * residualStat.StdDev());

                    Assert.AreEqual(window, windowFromResidualGraph, 0.001);

                    //Go back to correlation graph and make sure everything is still right
                    RunUI(() =>
                    {
                        SkylineWindow.ShowPlotType(PlotTypeRT.correlation);
                    });

                    testRegressionStatisitcs(regressionPane, rValues, i, j, targetPeptideCounts, originalPeptideCounts, slopes, intercepts, windows);
                }
            }

            //Make sure comparing a run to itself works as expected
            for (var i = 0; i < REPLICATES; i++)
            {
                int selfIndex = i;
                RunUI(() =>
                {
                    SkylineWindow.ShowPlotType(PlotTypeRT.correlation);
                    graphSummary.RunToRunTargetReplicate.SelectedIndex = selfIndex;
                    graphSummary.RunToRunOriginalReplicate.SelectedIndex = selfIndex;
                });
                WaitForGraphs();
                var regression = regressionPane.RegressionRefined;
                var statistics = regressionPane.StatisticsRefined;
                Assert.AreEqual(1, statistics.R, 10e-3);
                Assert.AreEqual(1, regression.Conversion.Slope, 10e-3);
                Assert.AreEqual(0, regression.Conversion.Intercept, 10e-3);
                Assert.AreEqual(0.5, regression.TimeWindow, 0.00001);

                RunUI(() =>
                {
                    SkylineWindow.ShowPlotType(PlotTypeRT.residuals);
                });

                //All residuals should be zero
                var pointList = regressionPane.CurveList.First().Points;
                for (var p = 0; p < pointList.Count; p++)
                {
                    Assert.AreEqual(0, pointList[p].Y);
                }
            }

            //Make sure switching to score to run works correctly
            RunUI(() =>
            {
                SkylineWindow.ShowPlotType(PlotTypeRT.correlation);
                SkylineWindow.ShowRTLinearRegressionGraphScoreToRun();
            });
            if (!graphSummary.TryGetGraphPane(out regressionPane))
                Assert.Fail("First graph pane was not RTLinearRegressionGraphPane");
            WaitForCondition(() => regressionPane.IsRefined);
      
            Assert.IsFalse(graphSummary.IsRunToRun);
            var regressionScoreToRun = regressionPane.RegressionRefined;
            var statisticsScoreToRun = regressionPane.StatisticsRefined;
            Assert.AreEqual(1.01, regressionScoreToRun.Conversion.Slope, 10e-3);
            Assert.AreEqual(0.32, regressionScoreToRun.Conversion.Intercept, 10e-3);
            Assert.AreEqual(15.2, regressionScoreToRun.TimeWindow, 10e-2);
            Assert.AreEqual(0.9483, statisticsScoreToRun.R, 10e-3);

            RunUI(() =>
            {
                SkylineWindow.ShowRTLinearRegressionGraphRunToRun();
            });
            Assert.IsTrue(graphSummary.IsRunToRun);
        }

        private double testRegressionStatisitcs(RTLinearRegressionGraphPane regressionPane, double[,] rValues, int i, int j,
            int[,] targetPeptideCounts, int[,] originalPeptideCounts, double[,] slopes, double[,] intercepts, double[,] windows)
        {
            var regression = regressionPane.RegressionRefined;
            var statistics = regressionPane.StatisticsRefined;

            var rValue = statistics.R;

            var targetPeptideCount = statistics.ListRetentionTimes.Count(p => p != 0);
            var originalPeptideCount = statistics.ListHydroScores.Count(p => p != 0);
            var intercept = regression.Conversion.Intercept;
            var slope = regression.Conversion.Slope;
            var window = regression.TimeWindow;
            //RValue is the same in both directions
            Assert.AreEqual(rValue, rValues[i, j], 0.00001);
            Assert.AreEqual(originalPeptideCount, targetPeptideCounts[j, i]);
            Assert.AreEqual(targetPeptideCount, originalPeptideCounts[j, i]);
            Assert.AreEqual(rValue, rValues[i, j],0.00001);
            Assert.AreEqual(slope, slopes[i, j], 0.00001);
            Assert.AreEqual(intercept, intercepts[i, j], 0.00001);
            Assert.AreEqual(originalPeptideCount, originalPeptideCounts[i, j]);
            Assert.AreEqual(targetPeptideCount, targetPeptideCounts[i, j]);
            Assert.AreEqual(window, windows[i, j], 0.00001);
            return window;
        }
    }
}
