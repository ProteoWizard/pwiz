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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.DocSettings;
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

        /// <summary>
        /// Change to true to write annotation value arrays to console
        /// </summary>
        protected override bool IsRecordMode => false;

        // BEGIN GENERATED CODE: Use IsRecordMode = true to regenerate
        private static readonly int[,] TARGET_PEPTIDE_COUNTS =
        {
            { 0, 22, 22, 22, 22 },
            { 22, 0, 22, 22, 22 },
            { 22, 22, 0, 22, 22 },
            { 22, 22, 22, 0, 22 },
            { 22, 22, 22, 22, 0 }
        };

        private static readonly int[,] ORIGINAL_PEPTIDE_COUNTS =
        {
            { 0, 22, 22, 22, 22 },
            { 22, 0, 22, 22, 22 },
            { 22, 22, 0, 22, 22 },
            { 22, 22, 22, 0, 22 },
            { 22, 22, 22, 22, 0 }
        };

        private static readonly double[,] R_VALUES =
        {
            { 0, 0.999992944457394, 0.999817770399884, 0.999812502870249, 0.999807353633302 },
            { 0.999992944457394, 0, 0.999817830781769, 0.999825129251812, 0.999819275656542 },
            { 0.999817770399884, 0.99981783078177, 0, 0.999993207021838, 0.999994582592321 },
            { 0.99981250287025, 0.999825129251812, 0.999993207021838, 0, 0.999998704916427 },
            { 0.999807353633302, 0.999819275656543, 0.999994582592321, 0.999998704916427, 0 }
        };

        private static readonly double[,] SLOPES =
        {
            { 0, 0.999607891519822, 0.999756712491327, 1.0003022798358, 1.00128575356905 },
            { 1.00037814571889, 0, 1.00014188315679, 1.00070023768746, 1.00168339707711 },
            { 0.999878832037415, 0.999493883401799, 0, 1.00054417477809, 1.00153442212351 },
            { 0.999322965713689, 0.998950786095061, 0.999442542665955, 0, 1.00098703546797 },
            { 0.998331136557306, 0.997958623344754, 0.998457110534213, 0.999011350198982, 0 }
        };

        private static readonly double[,] INTERCEPTS =
        {
            { 0, 0.0162532566394056, 0.0689843384404689, 0.0516126437507367, 0.0419961785381027 },
            { -0.0159278368819393, 0, 0.052915761685842, 0.0352425033092878, 0.02563874595862 },
            { -0.0604335876103832, -0.0443494182710644, 0, -0.0173401603146601, -0.0271120964763334 },
            { -0.0427866868055879, -0.0270064632764146, 0.0176490191016505, 0, -0.00970398101961933 },
            { -0.0328988464366269, -0.0171177668713902, 0.0273241444284622, 0.00975507951111254, 0 }
        };

        private static readonly double[,] WINDOWS =
        {
            { 0, 0.5, 0.933386997257438, 0.94677989146463, 0.959691317575048 },
            { 0.5, 0, 0.9335918441867, 0.914700525039467, 0.929882418592967 },
            { 0.933444001783852, 0.933289354450495, 0, 0.5, 0.5 },
            { 0.946316320610471, 0.913900623015458, 0.5, 0, 0.5 },
            { 0.958274331887216, 0.928151917977531, 0.5, 0.5, 0 }
        };
        // END GENERATED CODE

        protected override void DoTest()
        {
            var documentPath = TestFilesDir.GetTestPath("alpha-crystallin_data.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            WaitForDocumentLoaded();

            var graphSummary = ShowDialog<GraphSummary>(SkylineWindow.ShowRTRegressionGraphRunToRun);

            WaitForGraphs();

            RTLinearRegressionGraphPane regressionPane;
            if (!graphSummary.TryGetGraphPane(out regressionPane))
                Assert.Fail("First graph pane was not RTLinearRegressionGraphPane");
            Assert.IsTrue(regressionPane.HasToolbar);

            //Assert all values in regression match
            for (var i = 0; i < REPLICATES; i++)
            {
                for (var j = 0; j < REPLICATES; j++)
                {
                    if (i == j)
                        continue;
                    int targetIndex = i, originalIndex = j;
                    var summary = graphSummary;
                    RunUI(() =>
                    {
                        RunToRunTargetReplicate(summary).SelectedIndex = targetIndex;
                        RunToRunOriginalReplicate(summary).SelectedIndex = originalIndex;

                        Assert.AreEqual(targetIndex, summary.StateProvider.SelectedResultsIndex);
                    });
                    WaitForGraphs();
                    WaitForConditionUI(() => RegressionPaneReady(regressionPane, SLOPES[i, j], INTERCEPTS[i, j]));
                    var window = TestRegressionStatistics(regressionPane, i, j);

                    RunUI(() => SkylineWindow.ShowPlotType(PlotTypeRT.residuals));
                    WaitForGraphs();
                    RunUI(() =>
                    {
                        // Check that residual graph makes sense
                        var pointList = regressionPane.CurveList.First().Points;
                        var yList = new List<double>();
                        for (var p = 0; p < pointList.Count; p++)
                        {
                            var point = pointList[p];
                            yList.Add(point.Y);
                        }

                        var residualStat = new Statistics(yList);

                        // Value taken from Prediction.CalcRegression
                        var windowFromResidualGraph = Math.Max(0.5, 4 * residualStat.StdDev());

                        if (!IsRecordMode)
                            Assert.AreEqual(window, windowFromResidualGraph, 0.001);
                    });

                    // Go back to correlation graph and make sure everything is still right
                    RunUI(() => SkylineWindow.ShowPlotType(PlotTypeRT.correlation));
                    WaitForGraphs();
                    WaitForConditionUI(() => RegressionPaneReady(regressionPane, SLOPES[i,j], INTERCEPTS[i,j]));
                    TestRegressionStatistics(regressionPane, i, j);
                }
            }

            if (IsRecordMode)
                PrintMatrices();

            // Make sure comparing a run to itself works as expected
            for (var i = 0; i < REPLICATES; i++)
            {
                int selfIndex = i;
                var summary = graphSummary;
                RunUI(() =>
                {
                    SkylineWindow.ShowPlotType(PlotTypeRT.correlation);
                    RunToRunTargetReplicate(summary).SelectedIndex = selfIndex;
                    RunToRunOriginalReplicate(summary).SelectedIndex = selfIndex;
                });
                WaitForGraphs();
                WaitForConditionUI(() => RegressionPaneReady(regressionPane, 1.0, 0.0, 10e-3));
                RunUI(() =>
                {
                    var regression = regressionPane.RegressionRefined;
                    var statistics = regressionPane.StatisticsRefined;
                    Assert.AreEqual(1, statistics.R, 10e-3);
                    var regressionLine = (RegressionLineElement)regression.Conversion;
                    Assert.AreEqual(1, regressionLine.Slope, 10e-3);
                    Assert.AreEqual(0, regressionLine.Intercept, 10e-3);
                    Assert.AreEqual(0.5, regression.TimeWindow, 0.00001);
                });

                RunUI(() => SkylineWindow.ShowPlotType(PlotTypeRT.residuals));
                WaitForGraphs();
                RunUI(() =>
                {
                    //All residuals should be zero
                    var pointList = regressionPane.CurveList.First().Points;
                    for (var p = 0; p < pointList.Count; p++)
                    {
                        Assert.AreEqual(0, pointList[p].Y);
                    }
                });
            }

            // Make sure switching to score to run works correctly
            RunUI(() =>
            {
                SkylineWindow.ShowPlotType(PlotTypeRT.correlation);
                SkylineWindow.ShowRTRegressionGraphScoreToRun();
            });
            graphSummary = SkylineWindow.GraphRetentionTime;
            RTLinearRegressionGraphPane regressionPaneScore;           
            if (!graphSummary.TryGetGraphPane(out regressionPaneScore))
                Assert.Fail("First graph pane was not RTLinearRegressionGraphPane");
            WaitForCondition(() => regressionPaneScore.IsRefined);
            double expectedSlope = 1.01, expectedIntercept = 0.32;
            WaitForConditionUI(() => RegressionPaneReady(regressionPaneScore, expectedSlope, expectedIntercept, 10e-3));

            Assert.IsFalse(regressionPaneScore.HasToolbar);
            var regressionScoreToRun = regressionPaneScore.RegressionRefined;
            var statisticsScoreToRun = regressionPaneScore.StatisticsRefined;
            var regressionScoreToRunLine = (RegressionLineElement) regressionScoreToRun.Conversion;
            Assert.AreEqual(expectedSlope, regressionScoreToRunLine.Slope, 10e-3);
            Assert.AreEqual(expectedIntercept, regressionScoreToRunLine.Intercept, 10e-3);
            Assert.AreEqual(15.2, regressionScoreToRun.TimeWindow, 10e-2);
            Assert.AreEqual(0.9483, statisticsScoreToRun.R, 10e-3);

            RunUI(SkylineWindow.ShowRTRegressionGraphRunToRun);
            WaitForGraphs();
            graphSummary = SkylineWindow.GraphRetentionTime;
            if (!graphSummary.TryGetGraphPane(out regressionPane))
                Assert.Fail("First graph pane was not RTLinearRegressionGraphPane");
            Assert.IsTrue(regressionPane.HasToolbar);
        }

        private bool RegressionPaneReady(RTLinearRegressionGraphPane regressionPane, double slope, double intercept, double tolerance = 10e-5)
        {
            if (regressionPane.IsCalculating)
                return false;
            if (regressionPane._progressBar != null)
                return false;
            var regressionLine = regressionPane.RegressionRefined?.Conversion;
            if (regressionLine == null)
                return false;
            if (IsRecordMode)
                return true;
            return Math.Abs(slope - regressionLine.Slope) < tolerance &&
                   Math.Abs(intercept - regressionLine.Intercept) < tolerance;

        }

        public RunToRunRegressionToolbar RegressionToolbar(GraphSummary graphSummary)
        {
            return graphSummary.Toolbar as RunToRunRegressionToolbar;
        }

        public ToolStripComboBox RunToRunTargetReplicate(GraphSummary graphSummary)
        {
            return RegressionToolbar(graphSummary).RunToRunTargetReplicate;
        }

        public ToolStripComboBox RunToRunOriginalReplicate(GraphSummary graphSummary)
        {
            return RegressionToolbar(graphSummary).RunToRunOriginalReplicate;
        }

        private double TestRegressionStatistics(RTLinearRegressionGraphPane regressionPane, int i, int j)
        {
            var regression = regressionPane.RegressionRefined;
            var statistics = regressionPane.StatisticsRefined;

            var rValue = statistics.R;

            var targetPeptideCount = statistics.ListRetentionTimes.Count(p => p != 0);
            var originalPeptideCount = statistics.ListHydroScores.Count(p => p != 0);
            var regressionLine = (RegressionLineElement) regression.Conversion;
            var intercept = regressionLine.Intercept;
            var slope = regressionLine.Slope;
            var window = regression.TimeWindow;
            if (!IsRecordMode)
            {
                //RValue is the same in both directions
                Assert.AreEqual(rValue, R_VALUES[j, i], 10e-5);
                Assert.AreEqual(rValue, R_VALUES[i, j], 10e-5);
                Assert.AreEqual(originalPeptideCount, TARGET_PEPTIDE_COUNTS[j, i]);
                Assert.AreEqual(targetPeptideCount, ORIGINAL_PEPTIDE_COUNTS[j, i]);
                Assert.AreEqual(originalPeptideCount, ORIGINAL_PEPTIDE_COUNTS[i, j]);
                Assert.AreEqual(targetPeptideCount, TARGET_PEPTIDE_COUNTS[i, j]);
                Assert.AreEqual(slope, SLOPES[i, j], 10e-5);
                Assert.AreEqual(intercept, INTERCEPTS[i, j], 10e-5);
                Assert.AreEqual(window, WINDOWS[i, j], 10e-5);
            }
            else
            {
                _collectRValues[i, j] = rValue;
                _collectOriginalPeptideCounts[i, j] = originalPeptideCount;
                _collectTargetPeptideCounts[i, j] = targetPeptideCount;
                _collectSlopes[i, j] = slope;
                _collectIntercepts[i, j] = intercept;
                _collectWindows[i, j] = window;
            }
            return window;
        }

        private void PrintMatrices()
        {
            PrintMatrix("TARGET_PEPTIDE_COUNTS", _collectTargetPeptideCounts);
            PrintMatrix("ORIGINAL_PEPTIDE_COUNTS", _collectOriginalPeptideCounts);
            PrintMatrix("R_VALUES", _collectRValues);
            PrintMatrix("SLOPES", _collectSlopes);
            PrintMatrix("INTERCEPTS", _collectIntercepts);
            PrintMatrix("WINDOWS", _collectWindows);
        }

        private void PrintMatrix(string matrixName, int[,] matrix)
        {
            PrintMatrix(matrixName, "int", matrix.GetLength(0), matrix.GetLength(1), (i, j) => Console.Write(matrix[i, j]));
        }

        private void PrintMatrix(string matrixName, double[,] matrix)
        {
            PrintMatrix(matrixName, "double", matrix.GetLength(0), matrix.GetLength(1), (i, j) => Console.Write(matrix[i, j]));
        }

        private void PrintMatrix(string matrixName, string typeName, int len, long longLen, Action<int, int> printPosition)
        {
            Console.WriteLine(@"        private static readonly {0}[,] {1} =", typeName, matrixName);
            Console.WriteLine(@"        {");
            for (int i = 0; i < len; i++)
            {
                if (i > 0)
                    Console.WriteLine(@",");
                Console.Write(@"            {");
                for (int j = 0; j < longLen; j++)
                {
                    if (j > 0)
                        Console.Write(@", ");
                    printPosition(i, j);
                }
                Console.Write(@"}");
            }
            Console.WriteLine();
            Console.WriteLine(@"        };");
        }

        private static readonly int[,] _collectTargetPeptideCounts =
            new int[TARGET_PEPTIDE_COUNTS.GetLength(0), TARGET_PEPTIDE_COUNTS.GetLength(1)];
        private static readonly int[,] _collectOriginalPeptideCounts =
            new int[ORIGINAL_PEPTIDE_COUNTS.GetLength(0), ORIGINAL_PEPTIDE_COUNTS.GetLength(1)];
        private static readonly double[,] _collectRValues =
            new double[R_VALUES.GetLength(0), R_VALUES.GetLength(1)];
        private static readonly double[,] _collectSlopes =
            new double[SLOPES.GetLength(0), SLOPES.GetLength(1)];
        private static readonly double[,] _collectIntercepts =
            new double[INTERCEPTS.GetLength(0), INTERCEPTS.GetLength(1)];
        private static readonly double[,] _collectWindows =
            new double[WINDOWS.GetLength(0), WINDOWS.GetLength(1)];
    }
}
