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

        [TestMethod, NoParallelTesting]
        public void TestRunToRunRegression()
        {
            TestFilesZip = @"TestFunctional\RunToRunRegressionTest.zip";
            RunFunctionalTest();
        }

        /// <summary>
        /// Change to true to write annotation value arrays to console
        /// </summary>
        private bool IsRecordMode
        {
            get { return false; }
        }

        // BEGIN GENERATED CODE: Use IsRecordeMode = true to regenerat
        private static readonly int[,] TARGET_PEPTIDE_COUNTS =
        {
            {0, 22, 22, 22, 22},
            {22, 0, 22, 22, 22},
            {22, 22, 0, 22, 22},
            {22, 22, 22, 0, 22},
            {22, 22, 22, 22, 0}
        };
        private static readonly int[,] ORIGINAL_PEPTIDE_COUNTS =
        {
            {0, 22, 22, 22, 22},
            {22, 0, 22, 22, 22},
            {22, 22, 0, 22, 22},
            {22, 22, 22, 0, 22},
            {22, 22, 22, 22, 0}
        };
        private static readonly double[,] R_VALUES =
        {
            {0, 0.999993931908946, 0.999843506239357, 0.999839232254387, 0.999834925925893},
            {0.999993931908947, 0, 0.999843746240661, 0.999850167871986, 0.999845222422893},
            {0.999843506239357, 0.99984374624066, 0, 0.999994147903928, 0.999995277589245},
            {0.999839232254387, 0.999850167871986, 0.999994147903929, 0, 0.999998880406438},
            {0.999834925925894, 0.999845222422893, 0.999995277589245, 0.999998880406438, 0}
        };
        private static readonly double[,] SLOPES =
        {
            {0, 0.999707475441337, 1.00017889353965, 1.0006184891116, 1.00154337723693},
            {1.00028047045787, 0, 1.00046572523814, 1.00091615389789, 1.00184067681074},
            {0.999508231403589, 0.999222153920973, 0, 1.00043793880301, 1.00136810418741},
            {0.999060582263086, 0.998785317132195, 0.999550553868999, 0, 1.00092750683297},
            {0.998129388922859, 0.997853742557447, 0.998624333069067, 0.999071115527851, 0}
        };
        private static readonly double[,] INTERCEPTS =
        {
            {0, 0.0133084134167909, 0.056519079598754, 0.0422773381099439, 0.034396123070028},
            {-0.0130393823593842, 0, 0.0433540454886234, 0.0288681051767803, 0.0209988978057503},
            {-0.0494741792022388, -0.0363139773301882, 0, -0.0142038029294511, -0.0222056158333466},
            {-0.0350274788274447, -0.0221133023480569, 0.014459895361707, 0, -0.00794784994821995},
            {-0.0269327618715351, -0.0140162875262924, 0.0223867551509969, 0.00799065432053325, 0}
        };
        private static readonly double[,] WINDOWS =
        {
            {0, 0.5, 0.913480355580205, 0.925869324756884, 0.938186549030894},
            {0.5, 0, 0.913041225784304, 0.894083995681225, 0.908718421411426},
            {0.913174040667638, 0.912473597676775, 0, 0.5, 0.5},
            {0.925148280694587, 0.893131787005536, 0.5, 0, 0.5},
            {0.936586172975179, 0.906908446828345, 0.5, 0.5, 0}
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
                    WaitForConditionUI(() => regressionPane._progressBar == null);
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
                    WaitForConditionUI(() => regressionPane._progressBar == null);
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
                WaitForConditionUI(() => regressionPane._progressBar == null);
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
            WaitForConditionUI(() => regressionPaneScore._progressBar == null);

            Assert.IsFalse(regressionPaneScore.HasToolbar);
            var regressionScoreToRun = regressionPaneScore.RegressionRefined;
            var statisticsScoreToRun = regressionPaneScore.StatisticsRefined;
            var regressionScoreToRunLine = (RegressionLineElement) regressionScoreToRun.Conversion;
            Assert.AreEqual(1.01, regressionScoreToRunLine.Slope, 10e-3);
            Assert.AreEqual(0.32, regressionScoreToRunLine.Intercept, 10e-3);
            Assert.AreEqual(15.2, regressionScoreToRun.TimeWindow, 10e-2);
            Assert.AreEqual(0.9483, statisticsScoreToRun.R, 10e-3);

            RunUI(SkylineWindow.ShowRTRegressionGraphRunToRun);
            WaitForGraphs();
            graphSummary = SkylineWindow.GraphRetentionTime;
            if (!graphSummary.TryGetGraphPane(out regressionPane))
                Assert.Fail("First graph pane was not RTLinearRegressionGraphPane");
            Assert.IsTrue(regressionPane.HasToolbar);
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
                Assert.AreEqual(rValue, R_VALUES[j, i], 0.00001);
                Assert.AreEqual(rValue, R_VALUES[i, j], 0.00001);
                Assert.AreEqual(originalPeptideCount, TARGET_PEPTIDE_COUNTS[j, i]);
                Assert.AreEqual(targetPeptideCount, ORIGINAL_PEPTIDE_COUNTS[j, i]);
                Assert.AreEqual(originalPeptideCount, ORIGINAL_PEPTIDE_COUNTS[i, j]);
                Assert.AreEqual(targetPeptideCount, TARGET_PEPTIDE_COUNTS[i, j]);
                Assert.AreEqual(slope, SLOPES[i, j], 0.00001);
                Assert.AreEqual(intercept, INTERCEPTS[i, j], 0.00001);
                Assert.AreEqual(window, WINDOWS[i, j], 0.00001);
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
