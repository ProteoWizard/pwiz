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
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
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

            //Load serialized regression statistics
            IFormatter formatter = new BinaryFormatter();
            Stream targetStream = new FileStream(TestFilesDir.GetTestPath("targetPeptides.bin"),
                                      FileMode.Open,
                                      FileAccess.Read,
                                      FileShare.Read);

            var targetPeptideCounts = (int[,])formatter.Deserialize(targetStream);

            Stream originalStream = new FileStream(TestFilesDir.GetTestPath("originalPeptides.bin"),
                                      FileMode.Open,
                                      FileAccess.Read,
                                      FileShare.Read);
            var originalPeptideCounts = (int[,]) formatter.Deserialize(originalStream);

            Stream rStream = new FileStream(TestFilesDir.GetTestPath("rValues.bin"),
                                      FileMode.Open,
                                      FileAccess.Read,
                                      FileShare.Read);
            var rValues = (double[,]) formatter.Deserialize(rStream);

            Stream slopeStream = new FileStream(TestFilesDir.GetTestPath("slopes.bin"),
                                      FileMode.Open,
                                      FileAccess.Read,
                                      FileShare.Read);
            var slopes = (double[,]) formatter.Deserialize(slopeStream);

            Stream interceptStream = new FileStream(TestFilesDir.GetTestPath("intercepts.bin"),
                                      FileMode.Open,
                                      FileAccess.Read,
                                      FileShare.Read);
            var intercepts = (double[,]) formatter.Deserialize(interceptStream);

            Stream windowStream = new FileStream(TestFilesDir.GetTestPath("windows.bin"),
                                      FileMode.Open,
                                      FileAccess.Read,
                                      FileShare.Read);
            var windows = (double[,]) formatter.Deserialize(windowStream);

            targetStream.Close();
            originalStream.Close();
            rStream.Close();
            interceptStream.Close();
            slopeStream.Close();
            windowStream.Close();

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
                Assert.AreEqual(0.5, regression.TimeWindow);

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
            Assert.AreEqual(rValue, rValues[i, j]);
            Assert.AreEqual(slope, slopes[i, j]);
            Assert.AreEqual(intercept, intercepts[i, j]);
            Assert.AreEqual(originalPeptideCount, originalPeptideCounts[i, j]);
            Assert.AreEqual(targetPeptideCount, targetPeptideCounts[i, j]);
            Assert.AreEqual(window, windows[i, j]);
            return window;
        }
    }
}
