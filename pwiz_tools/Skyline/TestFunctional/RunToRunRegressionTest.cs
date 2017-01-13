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
        private const int REPLICATES = 18; 
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
            var document = WaitForDocumentLoaded();

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
                    RunUI(() =>
                    {
                        graphSummary.RunToRunTargetReplicate.SelectedIndex = i;
                        graphSummary.RunToRunOriginalReplicate.SelectedIndex = j;
                    });

                    Assert.AreEqual(i, graphSummary.StateProvider.SelectedResultsIndex);
                    
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

                    Assert.IsTrue(Math.Abs(window -  windowFromResidualGraph) < 0.001);

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
                RunUI(() =>
                {
                    SkylineWindow.ShowPlotType(PlotTypeRT.correlation);
                    graphSummary.RunToRunTargetReplicate.SelectedIndex = i;
                    graphSummary.RunToRunOriginalReplicate.SelectedIndex = i;
                });
                var regression = regressionPane.RegressionRefined;
                var statistics = regressionPane.StatisticsRefined;
                Assert.IsTrue(Math.Abs(statistics.R -1) <10e-3);
                Assert.IsTrue(Math.Abs(regression.Conversion.Slope- 1) < 10e-3);
                Assert.IsTrue(Math.Abs(regression.Conversion.Intercept - 0) < 10e-3);
                Assert.AreEqual(regression.TimeWindow, 0.5);

                RunUI(() =>
                {
                    SkylineWindow.ShowPlotType(PlotTypeRT.residuals);
                });

                //All residuals should be zero
                var pointList = regressionPane.CurveList.First().Points;
                for (var p = 0; p < pointList.Count; p++)
                {
                    Assert.AreEqual(pointList[p].Y,0);
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
            Assert.IsTrue(Math.Abs(regressionScoreToRun.Conversion.Slope - 1.01) < 10e-3);
            Assert.IsTrue(Math.Abs(regressionScoreToRun.Conversion.Intercept - 0.32) < 10e-3);
            Assert.IsTrue(Math.Abs(regressionScoreToRun.TimeWindow - 15.2) < 10e-2);
            Assert.IsTrue(Math.Abs(statisticsScoreToRun.R - 0.9483) < 10e-3);

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
            Assert.IsTrue(Math.Abs(rValue - rValues[i, j]) < 0.00001);
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
