/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;
using GraphData = pwiz.Skyline.Controls.Graphs.SummaryRelativeAbundanceGraphPane.GraphData;
using GraphDataParameters = pwiz.Skyline.Controls.Graphs.SummaryRelativeAbundanceGraphPane.GraphDataParameters;
using GraphPointData = pwiz.Skyline.Controls.Graphs.SummaryRelativeAbundanceGraphPane.GraphPointData;
using GraphSettings = pwiz.Skyline.Controls.Graphs.SummaryRelativeAbundanceGraphPane.GraphSettings;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Box plot pane showing protein/peptide abundance distributions per replicate.
    /// Shares the same background data pipeline as the Relative Abundance dot-plot
    /// via the shared Producer in AreaRelativeAbundanceGraphPane.
    /// </summary>
    internal class AreaAbundanceComparisonGraphPane : SummaryGraphPane, IDisposable
    {
        private static readonly Color DEFAULT_BAR_COLOR = Color.LightGreen;
        private const int PROGRESS_INITIAL_DELAY_MS = 300;
        private const int PROGRESS_UPDATE_INTERVAL_MS = 100;

        // Shared data pipeline
        private readonly ReplicateCachingReceiver<GraphDataParameters, GraphData> _graphDataReceiver;
        private GraphData _graphData;

        // Progress tracking
        private PaneProgressBar _progressBar;
        private int _progressValue = -1;
        private Stopwatch _progressStopwatch;

        // Maps X position back to replicate index for click handling
        private int[] _xToReplicateIndex;

        public AreaAbundanceComparisonGraphPane(GraphSummary graphSummary) : base(graphSummary)
        {
            XAxis.Title.Text = GraphsResources.AreaAbundanceComparisonGraphPane_XAxis_Replicate;
            XAxis.Type = AxisType.Text;
            XAxis.Scale.FontSpec = GraphSummary.CreateFontSpec(Color.Black);
            XAxis.Scale.FontSpec.Angle = 90;
            XAxis.MinorTic.Size = 0;
            XAxis.MajorTic.IsOpposite = false;
            XAxis.MajorTic.Size = 2;

            YAxis.Title.Text = GraphsResources.AreaPeptideGraphPane_UpdateAxes_Peak_Area;
            YAxis.MajorTic.IsOpposite = false;

            X2Axis.IsVisible = false;
            Y2Axis.IsVisible = false;
            Border.IsVisible = false;
            Chart.Border.IsVisible = false;
            Title.IsVisible = false;
            IsFontsScaled = false;

            BarSettings.MinClusterGap = 3f;
            BarSettings.Type = BarType.Overlay;

            // Register on the shared Producer used by the RA dot-plot
            var receiver = AreaRelativeAbundanceGraphPane.SharedProducer
                .RegisterCustomer(graphSummary, ProductAvailableAction);
            _graphDataReceiver = new ReplicateCachingReceiver<GraphDataParameters, GraphData>(
                receiver,
                SummaryRelativeAbundanceGraphPane.CleanCacheForIncrementalUpdates);
            _graphDataReceiver.ProgressChange += UpdateProgressHandler;
        }

        public override void Draw(Graphics g)
        {
            if (!Settings.Default.RelativeAbundanceLogScale)
            {
                YAxis.Scale.Min = 0;
                AxisChange(g);
            }
            base.Draw(g);
        }

        public override void UpdateGraph(bool selectionChanged)
        {
            if (Program.MainWindow == null)
                return;

            var document = GraphSummary.DocumentUIContainer.DocumentUI;
            if (!document.Settings.HasResults)
                return;

            var measuredResults = document.Settings.MeasuredResults;
            if (measuredResults == null)
                return;

            bool isLogScale = Settings.Default.RelativeAbundanceLogScale;
            YAxis.Type = isLogScale ? AxisType.Log : AxisType.Linear;
            UpdateYAxisTitle(isLogScale);

            var graphSettings = GraphSettings.FromSettings();

            // Request data from the shared pipeline (all replicates mode, cache key = -1)
            _graphDataReceiver.CleanStaleEntries(document);
            _graphDataReceiver.TryGetCachedResult(-1, out var priorGraphData);

            GraphData newGraphData;
            try
            {
                if (!_graphDataReceiver.TryGetProduct(
                        new GraphDataParameters(document, graphSettings, ReplicateDisplay.best, -1, priorGraphData),
                        out newGraphData))
                {
                    // Background computation in progress. Show replicate labels and
                    // a reasonable default axis range while data is computing.
                    if (_graphData == null)
                    {
                        InitializeEmptyGraph(measuredResults, isLogScale);
                        GraphSummary.GraphControl.Invalidate();
                    }
                    return;
                }
            }
            catch (Exception e)
            {
                ExceptionUtil.DisplayOrReportException(Program.MainWindow, e);
                return;
            }

            _graphData = newGraphData;

            // Extract per-replicate abundance values from the shared GraphData
            var replicateCount = measuredResults.Chromatograms.Count;
            var replicateValues = ExtractReplicateValues(_graphData, replicateCount);

            // Determine replicate ordering
            var orderedIndices = GetOrderedReplicateIndices(document, measuredResults);

            CurveList.Clear();
            int displayCount = orderedIndices.Length;
            var labels = new string[displayCount];
            _xToReplicateIndex = new int[displayCount];
            double yMin = double.MaxValue;
            double yMax = double.MinValue;

            // Compute box plot data for each replicate, tracking outlier identities
            var replicateTags = new BoxPlotTag[replicateCount];
            var replicateOutlierInfos = new List<OutlierInfo>[replicateCount];
            for (int i = 0; i < replicateCount; i++)
            {
                replicateTags[i] = ComputeBoxPlotForReplicate(replicateValues[i], out var outlierInfos);
                replicateOutlierInfos[i] = outlierInfos;
            }

            // Build labels in display order
            for (int x = 0; x < displayCount; x++)
            {
                int repIndex = orderedIndices[x];
                labels[x] = measuredResults.Chromatograms[repIndex].Name;
                _xToReplicateIndex[x] = repIndex;
            }

            // Determine grouping
            var groupByValue = GetCurrentGroupByValue(document);
            bool isGrouped = groupByValue != null;

            if (!isGrouped)
            {
                var points = new PointPairList();
                for (int x = 0; x < displayCount; x++)
                {
                    var tag = replicateTags[orderedIndices[x]];
                    if (tag == null)
                    {
                        points.Add(new PointPair(x, PointPairBase.Missing));
                        continue;
                    }
                    points.Add(BoxPlotBarItem.MakePointPair(x, tag.Q3, tag.Q1,
                        tag.Median, tag.Max, tag.Min, tag.Outliers));
                    UpdateYRange(tag, ref yMin, ref yMax);
                }
                CurveList.Add(new BoxPlotBarItem(string.Empty, points, DEFAULT_BAR_COLOR, Color.Black));
            }
            else
            {
                // Group replicates by annotation value, one BoxPlotBarItem per group
                var calculator = new AnnotationCalculator(document);
                var groupMap = new Dictionary<string, List<int>>();
                for (int x = 0; x < displayCount; x++)
                {
                    int repIndex = orderedIndices[x];
                    var chromSet = measuredResults.Chromatograms[repIndex];
                    var groupName = groupByValue.GetValue(calculator, chromSet)?.ToString() ?? string.Empty;
                    if (!groupMap.TryGetValue(groupName, out var xPositions))
                    {
                        xPositions = new List<int>();
                        groupMap[groupName] = xPositions;
                    }
                    xPositions.Add(x);
                }

                var usedColors = new List<Color>();
                foreach (var group in groupMap)
                {
                    var color = ColorGenerator.GetColor(group.Key, usedColors);
                    usedColors.Add(color);
                    var points = new PointPair[displayCount];
                    for (int x = 0; x < displayCount; x++)
                        points[x] = new PointPair(x, PointPairBase.Missing);

                    foreach (int x in group.Value)
                    {
                        var tag = replicateTags[orderedIndices[x]];
                        if (tag == null)
                            continue;
                        points[x] = BoxPlotBarItem.MakePointPair(x, tag.Q3, tag.Q1,
                            tag.Median, tag.Max, tag.Min, tag.Outliers);
                        UpdateYRange(tag, ref yMin, ref yMax);
                    }
                    CurveList.Add(new BoxPlotBarItem(group.Key, new PointPairList(points), color, Color.Black));
                }
            }

            // Use the invisible X2Axis (linear) for outlier points, since the
            // primary XAxis (text) forces ordinal positioning on LineItems.
            X2Axis.IsVisible = false;
            X2Axis.Scale.Min = -0.5;
            X2Axis.Scale.Max = displayCount - 0.5;

            var outlierPoints = new PointPairList();
            for (int x = 0; x < displayCount; x++)
            {
                var outliers = replicateOutlierInfos[orderedIndices[x]];
                if (outliers == null)
                    continue;
                foreach (var outlier in outliers)
                    outlierPoints.Add(new PointPair(x, outlier.Value) { Tag = outlier });
            }
            if (outlierPoints.Count > 0)
            {
                var outlierCurve = new LineItem(GraphsResources.AreaAbundanceComparisonGraphPane_Outliers,
                    outlierPoints, Color.Black, SymbolType.Circle)
                {
                    Line = { IsVisible = false },
                    IsX2Axis = true,
                    Symbol =
                    {
                        Size = 5,
                        Border = { IsVisible = true, Color = Color.Black, Width = 1.5f },
                        Fill = { Type = FillType.None },
                        IsAntiAlias = true
                    }
                };
                CurveList.Add(outlierCurve);
            }

            Legend.IsVisible = isGrouped;

            XAxis.Scale.TextLabels = labels;
            SetYAxisRange(isLogScale, yMin, yMax);
            AxisChange();
            GraphSummary.GraphControl.Invalidate();
        }

        public override bool HandleMouseDownEvent(ZedGraphControl sender, MouseEventArgs mouseEventArgs)
        {
            // Check for X-axis label click
            using (Graphics g = sender.CreateGraphics())
            {
                object nearestObject;
                if (FindNearestObject(new PointF(mouseEventArgs.X, mouseEventArgs.Y), g, out nearestObject, out _))
                {
                    if (nearestObject is XAxis axis)
                    {
                        int xPos = (int)axis.Scale.ReverseTransform(mouseEventArgs.X - axis.MajorTic.Size);
                        return NavigateToReplicate(xPos);
                    }
                }
            }

            // Check for point click (outlier dots or bar nearest points)
            if (!FindNearestPoint(new PointF(mouseEventArgs.X, mouseEventArgs.Y), out var nearestCurve, out var iNearest))
                return false;

            // If the clicked point is an outlier, select its protein/peptide
            var point = nearestCurve.Points[iNearest];
            if (point.Tag is OutlierInfo outlier)
                GraphSummary.StateProvider.SelectedPath = outlier.IdentityPath;
            int xPosition = point.Tag is OutlierInfo
                ? (int)Math.Round(point.X)
                : iNearest;
            return NavigateToReplicate(xPosition);
        }

        public override bool HandleMouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.None)
                return base.HandleMouseMoveEvent(sender, e);

            if (FindNearestPoint(new PointF(e.X, e.Y), out _, out _))
            {
                sender.Cursor = Cursors.Hand;
                return true;
            }

            // Check if hovering over X-axis label
            using (Graphics g = sender.CreateGraphics())
            {
                object nearestObject;
                if (FindNearestObject(new PointF(e.X, e.Y), g, out nearestObject, out _))
                {
                    if (nearestObject is XAxis)
                    {
                        sender.Cursor = Cursors.Hand;
                        return true;
                    }
                }
            }

            return false;
        }

        public override void OnClose(EventArgs e)
        {
            base.OnClose(e);
            _progressBar?.Dispose();
            _graphDataReceiver?.Dispose();
        }

        public void Dispose()
        {
            _progressBar?.Dispose();
            _graphDataReceiver?.Dispose();
        }

        private void ProductAvailableAction()
        {
            UpdateProgressHandler();
            UpdateGraph(false);
        }

        private void UpdateProgressHandler()
        {
            if (_graphDataReceiver.IsProcessing())
            {
                var newProgressValue = _graphDataReceiver.GetProgressValue();
                if (newProgressValue != _progressValue)
                {
                    if (_progressStopwatch == null)
                    {
                        _progressStopwatch = Stopwatch.StartNew();
                        _progressValue = newProgressValue;
                        return;
                    }

                    bool progressBarShowing = _progressBar != null;
                    int throttleMs = progressBarShowing ? PROGRESS_UPDATE_INTERVAL_MS : PROGRESS_INITIAL_DELAY_MS;

                    if (_progressStopwatch.ElapsedMilliseconds < throttleMs)
                        return;

                    _progressStopwatch.Restart();
                    _progressBar ??= new PaneProgressBar(this);
                    _progressBar.UpdateProgress(newProgressValue);
                    _progressValue = newProgressValue;
                }
            }
            else
            {
                _progressBar?.Dispose();
                _progressBar = null;
                _progressValue = -1;
                _progressStopwatch = null;
            }
        }

        /// <summary>
        /// Extract per-replicate abundance values from the shared GraphData.
        /// Each GraphPointData.ReplicateAreas contains values keyed by replicate index.
        /// </summary>
        private static List<AbundancePoint>[] ExtractReplicateValues(GraphData graphData, int replicateCount)
        {
            var replicateValues = new List<AbundancePoint>[replicateCount];
            for (int i = 0; i < replicateCount; i++)
                replicateValues[i] = new List<AbundancePoint>();

            foreach (var point in graphData.PointPairList)
            {
                var gpd = (GraphPointData)point.Tag;
                if (gpd == null)
                    continue;
                foreach (var replicateGroup in gpd.ReplicateAreas)
                {
                    int repIndex = replicateGroup.Key;
                    if (repIndex >= 0 && repIndex < replicateCount)
                    {
                        foreach (var value in replicateGroup)
                        {
                            if (value > 0)
                                replicateValues[repIndex].Add(new AbundancePoint(value, gpd.IdentityPath));
                        }
                    }
                }
            }

            return replicateValues;
        }

        private bool NavigateToReplicate(int xPosition)
        {
            if (_xToReplicateIndex == null || xPosition < 0 || xPosition >= _xToReplicateIndex.Length)
                return false;
            GraphSummary.StateProvider.SelectedResultsIndex = _xToReplicateIndex[xPosition];
            return true;
        }

        private static int[] GetOrderedReplicateIndices(SrmDocument document, MeasuredResults measuredResults)
        {
            var chromatograms = measuredResults.Chromatograms;
            int count = chromatograms.Count;
            var indices = Enumerable.Range(0, count).ToArray();

            if (SummaryReplicateGraphPane.ReplicateOrder == SummaryReplicateOrder.time)
            {
                Array.Sort(indices, (a, b) =>
                {
                    var timeA = chromatograms[a].MSDataFileInfos.FirstOrDefault()?.RunStartTime ?? DateTime.MaxValue;
                    var timeB = chromatograms[b].MSDataFileInfos.FirstOrDefault()?.RunStartTime ?? DateTime.MaxValue;
                    return timeA.CompareTo(timeB);
                });
            }

            var orderByAnnotation = SummaryReplicateGraphPane.OrderByReplicateAnnotation;
            if (!string.IsNullOrEmpty(orderByAnnotation))
            {
                var orderByValue = ReplicateValue.FromPersistedString(document.Settings, orderByAnnotation);
                if (orderByValue != null)
                {
                    var calculator = new AnnotationCalculator(document);
                    Array.Sort(indices, (a, b) =>
                    {
                        var valA = orderByValue.GetValue(calculator, chromatograms[a]);
                        var valB = orderByValue.GetValue(calculator, chromatograms[b]);
                        return CollectionUtil.ColumnValueComparer.Compare(valA, valB);
                    });
                }
            }

            return indices;
        }

        /// <summary>
        /// Compute box plot statistics for a replicate, identifying which proteins/peptides
        /// are outliers so they can be selected on click.
        /// </summary>
        private static BoxPlotTag ComputeBoxPlotForReplicate(List<AbundancePoint> values,
            out List<OutlierInfo> outlierInfos)
        {
            outlierInfos = null;
            if (values.Count == 0)
                return null;

            // Values already filtered for > 0 during extraction
            var sortedByLog = values.OrderBy(v => Math.Log10(v.Value)).ToArray();
            var sortedLog = sortedByLog.Select(v => Math.Log10(v.Value)).ToArray();
            var logTag = BoxPlotStatistics.ComputeBoxPlot(sortedLog);
            if (logTag == null)
                return null;

            // Find the raw whisker endpoint values directly from the sorted array
            // instead of round-tripping through log10 -> pow10 (which can shift values).
            double rawLowerWhisker = sortedByLog.First(v => Math.Log10(v.Value) >= logTag.Min).Value;
            double rawUpperWhisker = sortedByLog.Last(v => Math.Log10(v.Value) <= logTag.Max).Value;

            // Outliers are values strictly beyond the raw whisker endpoints
            outlierInfos = sortedByLog
                .Where(v => v.Value < rawLowerWhisker || v.Value > rawUpperWhisker)
                .Select(v => new OutlierInfo(v.Value, v.IdentityPath))
                .ToList();

            return new BoxPlotTag(
                Math.Pow(10, logTag.Q1),
                Math.Pow(10, logTag.Median),
                Math.Pow(10, logTag.Q3),
                rawLowerWhisker,
                rawUpperWhisker,
                outlierInfos.Select(o => o.Value).ToArray());
        }

        private static void UpdateYRange(BoxPlotTag tag, ref double yMin, ref double yMax)
        {
            double localMin = tag.Outliers.Length > 0 ? Math.Min(tag.Min, tag.Outliers.Min()) : tag.Min;
            double localMax = tag.Outliers.Length > 0 ? Math.Max(tag.Max, tag.Outliers.Max()) : tag.Max;
            yMin = Math.Min(yMin, localMin);
            yMax = Math.Max(yMax, localMax);
        }

        private void SetYAxisRange(bool isLogScale, double yMin, double yMax)
        {
            if (yMin >= double.MaxValue || yMax <= double.MinValue)
                return;
            YAxis.Scale.MinAuto = false;
            YAxis.Scale.MaxAuto = false;
            if (isLogScale)
            {
                YAxis.Scale.Min = Math.Max(1, yMin / 2);
                YAxis.Scale.Max = yMax * 2;
            }
            else
            {
                YAxis.Scale.Min = 0;
                YAxis.Scale.Max = yMax * 1.05;
            }
        }

        private void UpdateYAxisTitle(bool isLogScale)
        {
            YAxis.Title.Text = isLogScale
                ? TextUtil.SpaceSeparate(GraphsResources.SummaryPeptideGraphPane_UpdateAxes_Log,
                    GraphsResources.AreaPeptideGraphPane_UpdateAxes_Peak_Area)
                : GraphsResources.AreaPeptideGraphPane_UpdateAxes_Peak_Area;
        }

        /// <summary>
        /// Set up reasonable defaults before data arrives: replicate labels on x-axis,
        /// sensible y-axis range, and no data curves.
        /// </summary>
        private void InitializeEmptyGraph(MeasuredResults measuredResults, bool isLogScale)
        {
            var replicateCount = measuredResults.Chromatograms.Count;
            var labels = new string[replicateCount];
            _xToReplicateIndex = new int[replicateCount];
            for (int i = 0; i < replicateCount; i++)
            {
                labels[i] = measuredResults.Chromatograms[i].Name;
                _xToReplicateIndex[i] = i;
            }
            XAxis.Scale.TextLabels = labels;

            YAxis.Scale.MinAuto = false;
            YAxis.Scale.MaxAuto = false;
            if (isLogScale)
            {
                YAxis.Scale.Min = 1e2;
                YAxis.Scale.Max = 1e9;
            }
            else
            {
                YAxis.Scale.Min = 0;
                YAxis.Scale.Max = 1e6;
            }
            AxisChange();
        }

        private static ReplicateValue GetCurrentGroupByValue(SrmDocument document)
        {
            var groupByAnnotation = SummaryReplicateGraphPane.GroupByReplicateAnnotation;
            if (string.IsNullOrEmpty(groupByAnnotation))
                return null;
            return ReplicateValue.FromPersistedString(document.Settings, groupByAnnotation);
        }

        private struct AbundancePoint
        {
            public AbundancePoint(double value, IdentityPath identityPath)
            {
                Value = value;
                IdentityPath = identityPath;
            }
            public double Value { get; }
            public IdentityPath IdentityPath { get; }
        }

        internal class OutlierInfo
        {
            public OutlierInfo(double value, IdentityPath identityPath)
            {
                Value = value;
                IdentityPath = identityPath;
            }
            public double Value { get; }
            public IdentityPath IdentityPath { get; }
        }
    }
}
