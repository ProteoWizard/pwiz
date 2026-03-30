/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakAreaAbundanceComparisonGraphTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestPeakAreaAbundanceComparisonGraph()
        {
            TestFilesZip = @"TestFunctional\PeakAreaRelativeAbundanceGraphTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument(TestFilesDir.GetTestPath("Rat_plasma.sky"));

            RunUI(() =>
            {
                SkylineWindow.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.ShowPeakAreaAbundanceComparisonGraph();
            });
            WaitForGraphs();

            VerifyBasicGraphDisplay();
            VerifyBoxPlotDataCorrectness();
            VerifyOutliers();
            VerifyLogLinearScaleToggle();
            VerifyOrderBy();
            VerifyGroupBy();
            VerifyCopyData();
        }

        private void VerifyBasicGraphDisplay()
        {
            RunUI(() =>
            {
                var graphSummary = FindBoxPlotGraphSummary();
                AssertEx.IsNotNull(graphSummary);
                AssertEx.AreEqual(GraphTypeSummary.abundance_comparison, graphSummary.Type);

                var pane = FindBoxPlotPane();
                AssertEx.IsNotNull(pane);

                AssertEx.AreEqual(
                    GraphsResources.AreaAbundanceComparisonGraphPane_XAxis_Replicate,
                    pane.XAxis.Title.Text);

                // Default is log scale (RelativeAbundanceLogScale = True)
                AssertEx.AreEqual(AxisType.Log, pane.YAxis.Type);
                var expectedLogTitle = TextUtil.SpaceSeparate(
                    GraphsResources.SummaryPeptideGraphPane_UpdateAxes_Log,
                    GraphsResources.AreaPeptideGraphPane_UpdateAxes_Peak_Area);
                AssertEx.AreEqual(expectedLogTitle, pane.YAxis.Title.Text);

                // Legend not visible when ungrouped
                AssertEx.IsFalse(pane.Legend.IsVisible);
            });
        }

        private void VerifyBoxPlotDataCorrectness()
        {
            RunUI(() =>
            {
                var pane = FindBoxPlotPane();
                AssertEx.IsNotNull(pane);

                // Ungrouped: exactly 1 BoxPlotBarItem curve
                var boxPlotCurves = pane.CurveList.OfType<BoxPlotBarItem>().ToList();
                AssertEx.AreEqual(1, boxPlotCurves.Count);

                var curve = boxPlotCurves[0];
                int replicateCount = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count;
                AssertEx.AreEqual(replicateCount, curve.Points.Count);
                AssertEx.AreEqual(replicateCount, pane.XAxis.Scale.TextLabels.Length);

                // Verify each box's statistics are internally consistent
                int validBoxCount = 0;
                for (int i = 0; i < curve.Points.Count; i++)
                {
                    var point = curve.Points[i];
                    if (point.IsMissing)
                        continue;

                    var tag = point.Tag as BoxPlotTag;
                    Assert.IsNotNull(tag);
                    AssertEx.IsTrue(tag.Min <= tag.Q1, "Min should be <= Q1");
                    AssertEx.IsTrue(tag.Q1 <= tag.Median, "Q1 should be <= Median");
                    AssertEx.IsTrue(tag.Median <= tag.Q3, "Median should be <= Q3");
                    AssertEx.IsTrue(tag.Q3 <= tag.Max, "Q3 should be <= Max");
                    AssertEx.IsTrue(tag.Min > 0, "Min should be positive");
                    validBoxCount++;
                }

                AssertEx.IsTrue(validBoxCount > 0, "Should have at least one valid box");
            });
        }

        private void VerifyOutliers()
        {
            RunUI(() =>
            {
                var pane = FindBoxPlotPane();
                AssertEx.IsNotNull(pane);

                var outlierCurve = pane.CurveList.OfType<LineItem>().FirstOrDefault(c =>
                    c.Label.Text == GraphsResources.AreaAbundanceComparisonGraphPane_Outliers);
                Assert.IsNotNull(outlierCurve, "Outlier curve should be present");
                AssertEx.AreEqual(SymbolType.Circle, outlierCurve.Symbol.Type);
                AssertEx.IsFalse(outlierCurve.Line.IsVisible);
                AssertEx.IsTrue(outlierCurve.IsX2Axis);
                AssertEx.IsTrue(outlierCurve.Points.Count > 20, "Should have outlier points");

                for (int i = 0; i < outlierCurve.Points.Count; i++)
                    AssertEx.IsTrue(outlierCurve.Points[i].Y > 0, "Outlier value should be positive");
            });
        }

        private void VerifyLogLinearScaleToggle()
        {
            // Switch to linear
            RunUI(() => SkylineWindow.ShowRelativeAbundanceLogScale(false));
            WaitForGraphs();

            RunUI(() =>
            {
                var pane = FindBoxPlotPane();
                AssertEx.AreEqual(AxisType.Linear, pane.YAxis.Type);
                AssertEx.AreEqual(
                    GraphsResources.AreaPeptideGraphPane_UpdateAxes_Peak_Area,
                    pane.YAxis.Title.Text);
            });

            // Switch back to log
            RunUI(() => SkylineWindow.ShowRelativeAbundanceLogScale(true));
            WaitForGraphs();

            RunUI(() =>
            {
                var pane = FindBoxPlotPane();
                AssertEx.AreEqual(AxisType.Log, pane.YAxis.Type);
                var expectedLogTitle = TextUtil.SpaceSeparate(
                    GraphsResources.SummaryPeptideGraphPane_UpdateAxes_Log,
                    GraphsResources.AreaPeptideGraphPane_UpdateAxes_Peak_Area);
                AssertEx.AreEqual(expectedLogTitle, pane.YAxis.Title.Text);
            });
        }

        private void VerifyOrderBy()
        {
            // Collect outliers by replicate name in document order
            Dictionary<string, List<double>> documentOrderOutliers = null;
            RunUI(() => documentOrderOutliers = CollectOutliersByReplicate());
            AssertEx.IsTrue(documentOrderOutliers.Count > 0, "Should have outliers in at least one replicate");

            // Switch to acquisition time order
            RunUI(() => SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.time));
            WaitForGraphs();

            // Verify labels changed order (time order should differ from document order)
            string[] documentLabels = null;
            string[] timeLabels = null;
            RunUI(() =>
            {
                var pane = FindBoxPlotPane();
                timeLabels = pane.XAxis.Scale.TextLabels;
            });

            // Switch back to document order to capture labels
            RunUI(() => SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.document));
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = FindBoxPlotPane();
                documentLabels = pane.XAxis.Scale.TextLabels;
            });
            // Labels should be the same set but in different order
            CollectionAssert.AreNotEqual(documentLabels, timeLabels,
                "Time order should differ from document order");
            CollectionAssert.AreEquivalent(documentLabels, timeLabels,
                "Both orderings should have the same replicate names");

            // Switch back to time order and collect outliers again
            RunUI(() => SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.time));
            WaitForGraphs();

            Dictionary<string, List<double>> timeOrderOutliers = null;
            RunUI(() => timeOrderOutliers = CollectOutliersByReplicate());

            // Outlier sets per replicate name should be identical regardless of ordering
            AssertEx.AreEqual(documentOrderOutliers.Count, timeOrderOutliers.Count,
                "Same number of replicates should have outliers");
            foreach (var kvp in documentOrderOutliers)
            {
                AssertEx.IsTrue(timeOrderOutliers.ContainsKey(kvp.Key),
                    $"Replicate {kvp.Key} should have outliers in both orderings");
                var docValues = kvp.Value;
                var timeValues = timeOrderOutliers[kvp.Key];
                AssertEx.AreEqual(docValues.Count, timeValues.Count,
                    $"Replicate {kvp.Key} should have same outlier count");
                for (int i = 0; i < docValues.Count; i++)
                {
                    AssertEx.AreEqual(docValues[i], timeValues[i],
                        $"Replicate {kvp.Key} outlier {i} should match");
                }
            }

            // Reset to document order
            RunUI(() => SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.document));
            WaitForGraphs();
        }

        private void VerifyGroupBy()
        {
            // Group by Condition: 2 groups (Diseased, Healthy)
            RunUI(() => SkylineWindow.GroupByReplicateAnnotation("Condition"));
            WaitForGraphs();

            RunUI(() =>
            {
                var pane = FindBoxPlotPane();
                AssertEx.IsNotNull(pane);
                AssertEx.IsTrue(pane.Legend.IsVisible);

                var boxPlotCurves = pane.CurveList.OfType<BoxPlotBarItem>().ToList();
                AssertEx.AreEqual(2, boxPlotCurves.Count);

                // Different colors
                AssertEx.AreNotEqual(
                    boxPlotCurves[0].Bar.Fill.Color,
                    boxPlotCurves[1].Bar.Fill.Color,
                    "Group curves should have different colors");

                // Each curve has all replicate slots; non-missing across both = total replicates
                int replicateCount = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count;
                int nonMissingCount = 0;
                foreach (var curve in boxPlotCurves)
                {
                    AssertEx.AreEqual(replicateCount, curve.Points.Count);
                    for (int i = 0; i < curve.Points.Count; i++)
                    {
                        if (!curve.Points[i].IsMissing)
                            nonMissingCount++;
                    }
                }
                AssertEx.AreEqual(replicateCount, nonMissingCount);
            });

            // Group by SubjectId: 14 groups (7 Diseased + 7 Healthy subjects, 3 tech reps each)
            RunUI(() => SkylineWindow.GroupByReplicateAnnotation("SubjectId"));
            WaitForGraphs();

            RunUI(() =>
            {
                var pane = FindBoxPlotPane();
                AssertEx.IsTrue(pane.Legend.IsVisible);

                var boxPlotCurves = pane.CurveList.OfType<BoxPlotBarItem>().ToList();
                AssertEx.AreEqual(14, boxPlotCurves.Count);

                // All unique colors
                var colors = boxPlotCurves.Select(c => c.Bar.Fill.Color).Distinct().ToList();
                AssertEx.AreEqual(14, colors.Count, "Each subject should have a unique color");
            });

            // Clear grouping
            RunUI(() => SkylineWindow.GroupByReplicateValue(null));
            WaitForGraphs();

            RunUI(() =>
            {
                var pane = FindBoxPlotPane();
                AssertEx.IsFalse(pane.Legend.IsVisible);
                AssertEx.AreEqual(1, pane.CurveList.OfType<BoxPlotBarItem>().Count());
            });
        }

        private void VerifyCopyData()
        {
            RunUI(() =>
            {
                var graphSummary = FindBoxPlotGraphSummary();
                AssertEx.IsNotNull(graphSummary);

                var graphData = CopyGraphDataToolStripMenuItem.GetGraphData(
                    graphSummary.GraphControl.MasterPane);

                AssertEx.AreEqual(1, graphData.Panes.Count);
                var paneData = graphData.Panes[0];
                AssertEx.IsTrue(paneData.DataFrames.Count >= 1);

                var dataFrame = paneData.DataFrames[0];
                int replicateCount = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count;
                AssertEx.AreEqual(replicateCount, dataFrame.RowCount);

                // Spot-check first row for valid box plot statistics
                var row = dataFrame.GetRow(0);
                AssertEx.IsTrue(row.Length >= 6, "Should have replicate + 5 stat columns");

                // Columns: Replicate, Min, Q1, Median, Q3, Max
                var min = (double?)row[1];
                var q1 = (double?)row[2];
                var median = (double?)row[3];
                var q3 = (double?)row[4];
                var max = (double?)row[5];

                AssertEx.IsNotNull(min);
                AssertEx.IsNotNull(q1);
                AssertEx.IsNotNull(median);
                AssertEx.IsNotNull(q3);
                AssertEx.IsNotNull(max);

                AssertEx.IsTrue(min.Value <= q1.Value, "Copy Data: Min <= Q1");
                AssertEx.IsTrue(q1.Value <= median.Value, "Copy Data: Q1 <= Median");
                AssertEx.IsTrue(median.Value <= q3.Value, "Copy Data: Median <= Q3");
                AssertEx.IsTrue(q3.Value <= max.Value, "Copy Data: Q3 <= Max");
            });
        }

        /// <summary>
        /// Collect outlier Y values grouped by replicate name, sorted within each replicate.
        /// </summary>
        private Dictionary<string, List<double>> CollectOutliersByReplicate()
        {
            var pane = FindBoxPlotPane();
            var labels = pane.XAxis.Scale.TextLabels;
            var outlierCurve = pane.CurveList.OfType<LineItem>()
                .FirstOrDefault(c => c.Label.Text == GraphsResources.AreaAbundanceComparisonGraphPane_Outliers);
            var result = new Dictionary<string, List<double>>();
            if (outlierCurve == null)
                return result;
            for (int i = 0; i < outlierCurve.Points.Count; i++)
            {
                int x = (int)Math.Round(outlierCurve.Points[i].X);
                string repName = labels[x];
                if (!result.TryGetValue(repName, out var list))
                {
                    list = new List<double>();
                    result[repName] = list;
                }
                list.Add(outlierCurve.Points[i].Y);
            }
            // Sort within each replicate for order-independent comparison
            foreach (var list in result.Values)
                list.Sort();
            return result;
        }

        private AreaAbundanceComparisonGraphPane FindBoxPlotPane()
        {
            foreach (var graphSummary in SkylineWindow.ListGraphPeakArea)
            {
                if (graphSummary.TryGetGraphPane<AreaAbundanceComparisonGraphPane>(out var pane))
                    return pane;
            }
            return null;
        }

        private GraphSummary FindBoxPlotGraphSummary()
        {
            return FormUtil.OpenForms.OfType<GraphSummary>().FirstOrDefault(graph =>
                graph.Type == GraphTypeSummary.abundance_comparison &&
                graph.Controller is AreaGraphController);
        }
    }
}
