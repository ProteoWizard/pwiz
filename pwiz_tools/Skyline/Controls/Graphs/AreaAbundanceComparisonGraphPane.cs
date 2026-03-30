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
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
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
    public class AreaAbundanceComparisonGraphPane : SummaryGraphPane, IDisposable
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
        private readonly AxisLabelScaler _axisLabelScaler;
        private NodeTip _toolTip;

        public AreaAbundanceComparisonGraphPane(GraphSummary graphSummary) : base(graphSummary)
        {
            XAxis.Title.Text = GraphsResources.AreaAbundanceComparisonGraphPane_XAxis_Replicate;
            XAxis.Type = AxisType.Text;

            YAxis.Title.Text = GraphsResources.AreaPeptideGraphPane_UpdateAxes_Peak_Area;

            X2Axis.IsVisible = false;
            Y2Axis.IsVisible = false;
            Border.IsVisible = false;
            Chart.Border.IsVisible = false;
            Title.IsVisible = false;
            IsFontsScaled = false;

            BarSettings.MinClusterGap = 3f;
            BarSettings.Type = BarType.Overlay;

            _axisLabelScaler = new AxisLabelScaler(this) { IsRepeatRemovalAllowed = true };

            // Register on the shared Producer used by the RA dot-plot
            var receiver = AreaRelativeAbundanceGraphPane.SharedProducer
                .RegisterCustomer(graphSummary, ProductAvailableAction);
            _graphDataReceiver = new ReplicateCachingReceiver<GraphDataParameters, GraphData>(
                receiver,
                SummaryRelativeAbundanceGraphPane.CleanCacheForIncrementalUpdates);
            _graphDataReceiver.ProgressChange += UpdateProgressHandler;
        }

        /// <summary>
        /// True when the graph has finished computing and displaying data for the current document.
        /// </summary>
        public bool IsComplete
        {
            get
            {
                if (_graphDataReceiver.HasError)
                    return true;

                if (!_graphDataReceiver.TryGetCurrentProduct(out var product))
                    return false;

                var currentDoc = GraphSummary.DocumentUIContainer.DocumentUI;
                var currentSettings = GraphSettings.FromSettings();

                if (!ReferenceEquals(product.Document, currentDoc) ||
                    !Equals(product.GraphSettings, currentSettings))
                    return false;

                return _graphData != null &&
                       ReferenceEquals(_graphData.Document, currentDoc) &&
                       Equals(_graphData.GraphSettings, currentSettings);
            }
        }

        public override void HandleResizeEvent()
        {
            _axisLabelScaler.ScaleAxisLabels();
        }

        public override void Draw(Graphics g)
        {
            HandleResizeEvent();
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
            UpdateYAxisTitle(document, isLogScale);

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

            if (FindNearestPoint(new PointF(e.X, e.Y), out var nearestCurve, out var iNearest))
            {
                sender.Cursor = Cursors.Hand;
                var point = nearestCurve.Points[iNearest];
                if (point.Tag is OutlierInfo outlier)
                {
                    int xPosition = (int)Math.Round(point.X);
                    var labels = _axisLabelScaler.OriginalTextLabels ?? XAxis.Scale.TextLabels;
                    string replicateName = labels != null && xPosition >= 0 && xPosition < labels.Length
                        ? labels[xPosition]
                        : null;
                    var gpd = FindGraphPointData(outlier.IdentityPath);
                    _toolTip ??= new NodeTip(this) { Parent = GraphSummary.GraphControl };
                    _toolTip.SetTipProvider(
                        new OutlierTipProvider(outlier, gpd, replicateName),
                        new Rectangle(e.Location, new Size()), e.Location);
                }
                return true;
            }

            _toolTip?.HideTip();

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
            _toolTip?.Dispose();
            _progressBar?.Dispose();
            _graphDataReceiver?.Dispose();
        }

        public void Dispose()
        {
            _toolTip?.Dispose();
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
                YAxis.Scale.Min = yMin / 2;
                YAxis.Scale.Max = yMax * 2;
            }
            else
            {
                YAxis.Scale.Min = 0;
                YAxis.Scale.Max = yMax * 1.05;
            }
        }

        private void UpdateYAxisTitle(SrmDocument document, bool isLogScale)
        {
            string yTitle = GraphsResources.AreaPeptideGraphPane_UpdateAxes_Peak_Area;
            var normMethod = document.Settings.PeptideSettings.Quantification.NormalizationMethod;
            if (normMethod != null && !Equals(normMethod, NormalizationMethod.NONE))
                yTitle = normMethod.GetAxisTitle(yTitle);
            YAxis.Title.Text = isLogScale
                ? GraphValues.AnnotateLogAxisTitle(yTitle)
                : yTitle;
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
            var groupByAnnotation = Settings.Default.AbundanceComparisonGroupByAnnotation;
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

        private GraphPointData FindGraphPointData(IdentityPath identityPath)
        {
            if (_graphData == null || identityPath == null)
                return null;
            return _graphData.PointPairList
                .Select(pp => pp.Tag as GraphPointData)
                .FirstOrDefault(gpd => gpd != null && Equals(gpd.IdentityPath, identityPath));
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

        private class OutlierTipProvider : ITipProvider
        {
            private readonly OutlierInfo _outlier;
            private readonly GraphPointData _gpd;
            private readonly string _replicateName;

            public OutlierTipProvider(OutlierInfo outlier, GraphPointData gpd, string replicateName)
            {
                _outlier = outlier;
                _gpd = gpd;
                _replicateName = replicateName;
            }

            public bool HasTip => _outlier != null;

            public Size RenderTip(Graphics g, Size sizeMax, bool draw)
            {
                if (!HasTip)
                    return Size.Empty;

                var table = new TableDesc();
                using (var rt = new RenderTools())
                {
                    if (_gpd?.Peptide != null)
                    {
                        table.AddDetailRow(
                            Helpers.PeptideToMoleculeTextMapper.Translate(
                                GroupComparisonStrings.FoldChangeRowTipProvider_RenderTip_Peptide,
                                _gpd.Peptide.IsSmallMolecule()),
                            _gpd.Peptide.ModifiedSequence?.ToString() ?? _gpd.Peptide.ToString(), rt);
                    }
                    if (_gpd?.Protein != null)
                    {
                        table.AddDetailRow(
                            Helpers.PeptideToMoleculeTextMapper.Translate(
                                GroupComparisonStrings.FoldChangeRowTipProvider_RenderTip_Protein,
                                _gpd.Protein.IsNonProteomic()),
                            ProteinMetadataManager.ProteinModalDisplayText(_gpd.Protein.DocNode), rt);
                    }
                    if (_replicateName != null)
                    {
                        table.AddDetailRow(
                            GraphsResources.SummaryReplicateGraphPane_SummaryReplicateGraphPane_Replicate,
                            _replicateName, rt);
                    }
                    table.AddDetailRow(
                        GraphsResources.RelativeAbundanceGraph_ToolTip_PeakArea,
                        _outlier.Value.ToString(Formats.CalibrationCurve, CultureInfo.CurrentCulture), rt);
                    table.AddDetailRow(
                        GraphsResources.RelativeAbundanceGraph_ToolTip_LogPeakArea,
                        Math.Log10(_outlier.Value).ToString(Formats.FoldChange, CultureInfo.CurrentCulture), rt);

                    var size = table.CalcDimensions(g);
                    if (draw)
                        table.Draw(g);
                    return new Size((int)size.Width + 2, (int)size.Height + 2);
                }
            }
        }
    }
}
