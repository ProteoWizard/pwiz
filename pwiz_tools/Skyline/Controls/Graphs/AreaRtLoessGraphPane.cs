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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Dapper;
using pwiz.Common.Collections;
using pwiz.Common.Colors;
using pwiz.Common.DataAnalysis;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Themes;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    internal class AreaRtLoessGraphPane : SummaryGraphPane
    {
        private readonly Receiver<ReferenceValue<SrmDocument>, RtLoessCurves> _calcListener;
        private PaneProgressBar _progressBar;
        // The scatter curve of individual peptide points for the selected replicate (null when
        // "Peptides" is off). Each point's Tag is the peptide's IdentityPath, used to select the
        // peptide in the Targets tree when clicked.
        private CurveItem _peptidesCurve;

        public AreaRtLoessGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
            _calcListener = RtLoessCurves.PRODUCER.RegisterCustomer(graphSummary,
                () => GraphSummary.UpdateUI(false));
            _calcListener.ProgressChange += UpdateProgressHandler;
        }

        private void UpdateProgressHandler()
        {
            if (_calcListener.IsProcessing())
            {
                _progressBar ??= new PaneProgressBar(this);
                _progressBar.UpdateProgress(_calcListener.GetProgressValue());
            }
            else
            {
                _progressBar?.Dispose();
                _progressBar = null;
            }
        }

        public override void UpdateGraph(bool selectionChanged)
        {
            CurveList.Clear();
            GraphObjList.Clear();

            var document = GraphSummary.DocumentUIContainer.DocumentUI;
            if (!document.Settings.HasResults)
            {
                Title.Text = GraphsResources.AreaRtLoessGraphPane_No_RT_LOESS_Data;
                return;
            }

            try
            {
                if (!_calcListener.TryGetProduct(document, out var rtLoessCurves))
                {
                    return;
                }

                UpdateGraph(document, rtLoessCurves);
            }
            catch (Exception ex)
            {
                Title.Text = ex.Message;
            }
        }

        private void UpdateGraph(SrmDocument document, RtLoessCurves rtLoessCurves)
        {
            _peptidesCurve = null;
            if (!rtLoessCurves.HasCurves)
            {
                Title.Text = GraphsResources.AreaRtLoessGraphPane_No_RT_LOESS_Data;
                return;
            }

            var showValue = AreaGraphController.RtLoessShowValue;
            var globalRtGrid = rtLoessCurves.GetGlobalMedianRtGrid();
            var globalFittedValues = rtLoessCurves.GetGlobalMedianFittedValues();

            var colors = ColorPalettes.LARGE_PALETTE;
            var curvesByReplicate = new Dictionary<int, List<RtLoessCurves.RtLoessCurveInfo>>();
            foreach (var curveInfo in rtLoessCurves.GetCurves())
            {
                if (!curvesByReplicate.TryGetValue(curveInfo.ReplicateIndex, out var list))
                {
                    list = new List<RtLoessCurves.RtLoessCurveInfo>();
                    curvesByReplicate[curveInfo.ReplicateIndex] = list;
                }
                list.Add(curveInfo);
            }

            int iColor = 1;
            var measuredResults = document.MeasuredResults;
            if (measuredResults != null)
            {
                for (int i = 0; i < measuredResults.Chromatograms.Count; i++)
                {
                    if (!curvesByReplicate.TryGetValue(i, out var curves))
                        continue;

                    string replicateName = measuredResults.Chromatograms[i].Name;
                    var color = colors[iColor++ % colors.Count];
                    var width = 1.5f;
                    if (i == GraphSummary.ResultsIndex && Settings.Default.ShowReplicateSelection)
                    {
                        color = ColorScheme.ChromGraphItemSelected;
                        width = 3;
                    }

                    foreach (var curveInfo in curves)
                    {
                        var pointPairList = new PointPairList();
                        for (int j = 0; j < curveInfo.RtGrid.Length; j++)
                        {
                            // FittedValues and globalFittedValues are in log2 space, so
                            // "median divided by global median" becomes a subtraction.
                            double y;
                            switch (showValue)
                            {
                                case RtLoessShowValue.NormalizedMedian:
                                    y = globalFittedValues?[j] ?? curveInfo.FittedValues[j];
                                    break;
                                case RtLoessShowValue.NormalizationFactor:
                                    y = curveInfo.FittedValues[j] - (globalFittedValues?[j] ?? 0);
                                    break;
                                default:
                                    y = curveInfo.FittedValues[j];
                                    break;
                            }
                            pointPairList.Add(curveInfo.RtGrid[j], y);
                        }

                        string label = curves.Count > 1
                            ? replicateName + @" (" + (curves.IndexOf(curveInfo) + 1) + @")"
                            : replicateName;

                        var curve = AddCurve(label, pointPairList, color, SymbolType.None);
                        curve.Line.IsAntiAlias = true;
                        curve.Line.IsOptimizedDraw = true;
                        curve.Symbol.IsVisible = false;
                        curve.Line.Width = width;
                        curve.Label.IsVisible = true;
                        curve.Tag = curveInfo.ReplicateIndex;
                    }
                }
            }

            if (showValue != RtLoessShowValue.NormalizationFactor &&
                globalRtGrid != null && globalFittedValues != null)
            {
                var medianPoints = new PointPairList();
                for (int j = 0; j < globalRtGrid.Length; j++)
                {
                    medianPoints.Add(globalRtGrid[j], globalFittedValues[j]);
                }

                var medianCurve = AddCurve(GraphsResources.AreaRtLoessGraphPane_Global_Median,
                    medianPoints, Color.Black, SymbolType.None);
                medianCurve.Line.IsAntiAlias = true;
                medianCurve.Line.IsOptimizedDraw = true;
                medianCurve.Symbol.IsVisible = false;
                medianCurve.Line.Width = 3f;
                medianCurve.Label.IsVisible = true;
            }

            if (AreaGraphController.RtLoessShowPeptides)
            {
                AddPeptidePoints(document, rtLoessCurves, showValue, globalRtGrid, globalFittedValues);
            }

            XAxis.Title.Text = GraphsResources.AreaRtLoessGraphPane_Retention_Time__min_;
            YAxis.Title.Text = showValue == RtLoessShowValue.NormalizationFactor
                ? GraphsResources.AreaRtLoessGraphPane_Log2_Adjustment
                : GraphsResources.AreaRtLoessGraphPane_Log2_Abundance;
            switch (showValue)
            {
                case RtLoessShowValue.NormalizedMedian:
                    Title.Text = GraphsResources.AreaRtLoessGraphPane_RT_LOESS_Curves_Normalized;
                    break;
                case RtLoessShowValue.NormalizationFactor:
                    Title.Text = GraphsResources.AreaRtLoessGraphPane_RT_LOESS_Normalization_Factors;
                    break;
                default:
                    Title.Text = GraphsResources.AreaRtLoessGraphPane_RT_LOESS_Curves;
                    break;
            }

            Legend.IsVisible = AreaGraphController.RtLoessShowLegend &&
                               CurveList.Any(c => c.Label.IsVisible);

            AxisChange();
        }

        /// <summary>
        /// Adds a scatter curve of the individual per-precursor (RT, log2 area) points for the
        /// currently selected replicate. Each point is shifted by the same transform that maps a
        /// replicate's raw fitted curve to the curve currently displayed, so the points always
        /// scatter around the visible curve:
        ///   Median: the raw log2 area.
        ///   NormalizationFactor: log2 area minus the global median curve (centers on the factor).
        ///   NormalizedMedian: log2 area minus the RT LOESS adjustment (the normalized quantity).
        /// Each point's Tag is the peptide's IdentityPath so a click can select it in the tree.
        /// </summary>
        private void AddPeptidePoints(SrmDocument document, RtLoessCurves rtLoessCurves,
            RtLoessShowValue showValue, double[] globalRtGrid, double[] globalFittedValues)
        {
            var measuredResults = document.MeasuredResults;
            int replicateIndex = GraphSummary.ResultsIndex;
            if (measuredResults == null || replicateIndex < 0 ||
                replicateIndex >= measuredResults.Chromatograms.Count)
            {
                return;
            }
            // The Normalization Factor and Normalized Median transforms need the global median
            // curve; without it there is nothing meaningful to plot.
            if (showValue != RtLoessShowValue.Median && (globalRtGrid == null || globalFittedValues == null))
            {
                return;
            }

            var pointList = new PointPairList();
            foreach (var moleculeGroup in document.MoleculeGroups)
            {
                foreach (var peptide in moleculeGroup.Molecules)
                {
                    if (peptide.IsDecoy || PeptideDocNode.STANDARD_TYPE_IRT == peptide.GlobalStandardType)
                    {
                        continue;
                    }
                    var identityPath = new IdentityPath(moleculeGroup.PeptideGroup, peptide.Peptide);
                    foreach (var transitionGroup in peptide.TransitionGroups)
                    {
                        if (transitionGroup.Results == null || replicateIndex >= transitionGroup.Results.Count)
                        {
                            continue;
                        }
                        var chromInfoList = transitionGroup.Results[replicateIndex];
                        if (chromInfoList.IsEmpty)
                        {
                            continue;
                        }
                        foreach (var chromInfo in chromInfoList)
                        {
                            if (chromInfo == null || chromInfo.OptimizationStep != 0)
                            {
                                continue;
                            }
                            if (!chromInfo.RetentionTime.HasValue || !chromInfo.Area.HasValue ||
                                chromInfo.Area.Value <= 0)
                            {
                                continue;
                            }
                            double rt = chromInfo.RetentionTime.Value;
                            double log2Area = Math.Log(chromInfo.Area.Value, 2);
                            double? y = TransformPeptidePoint(showValue, log2Area, rt, replicateIndex,
                                chromInfo.FileId, rtLoessCurves, globalRtGrid, globalFittedValues);
                            if (y.HasValue)
                            {
                                pointList.Add(new PointPair(rt, y.Value) { Tag = identityPath });
                            }
                        }
                    }
                }
            }

            if (pointList.Count == 0)
            {
                return;
            }

            // Draw the points as hollow circles so dense clusters stay legible (overlapping rings)
            // instead of merging into a solid block. PeptidePointsCurve adapts the ring transparency
            // to how many points are currently visible: it lightens when zoomed out (many points)
            // and darkens when zoomed in (few points), unless Adaptive Alpha is turned off.
            var curve = new PeptidePointsCurve(GraphsResources.AreaRtLoessGraphPane_Peptides,
                pointList, ColorScheme.ChromGraphItemSelected);
            CurveList.Add(curve);
            _peptidesCurve = curve;
        }

        private static double? TransformPeptidePoint(RtLoessShowValue showValue, double log2Area, double rt,
            int replicateIndex, ChromFileInfoId fileId, RtLoessCurves rtLoessCurves,
            double[] globalRtGrid, double[] globalFittedValues)
        {
            switch (showValue)
            {
                case RtLoessShowValue.NormalizationFactor:
                    // Raw area divided by the global median value at this RT, so the points
                    // scatter around the normalization factor curve (sample - global).
                    return log2Area - LoessInterpolator.Interpolate(rt, globalRtGrid, globalFittedValues);
                case RtLoessShowValue.NormalizedMedian:
                    // The normalized quantity: subtract this file's RT LOESS adjustment (sample -
                    // global), so the points scatter around the global median curve.
                    var adjustment = rtLoessCurves.GetAdjustment(replicateIndex, fileId, rt);
                    return adjustment.HasValue ? log2Area - adjustment.Value : (double?) null;
                default:
                    return log2Area;
            }
        }

        /// <summary>
        /// A <see cref="LineItem"/> for the peptide scatter whose hollow-circle transparency adapts
        /// to the number of points currently visible within the axis bounds. Zooming in (fewer
        /// visible points) makes the rings more opaque; zooming out (many points) makes them more
        /// transparent so dense regions stay legible. When Adaptive Alpha is off the points are
        /// fully opaque. The alpha is recomputed on every <see cref="Draw"/>, so it tracks zoom and
        /// pan as well as the document.
        /// </summary>
        internal sealed class PeptidePointsCurve : LineItem
        {
            // alpha = ALPHA_BUDGET / (visible point count), clamped to [MIN, MAX] as a fraction of
            // opaque. The budget is the rough number of points at which the cloud starts to fill in.
            private const double ALPHA_BUDGET = 5000.0;
            private const double ALPHA_MIN = 0.08;
            private const double ALPHA_MAX = 0.5;

            private readonly Color _baseColor;

            public PeptidePointsCurve(string label, IPointList points, Color baseColor)
                : base(label)
            {
                _baseColor = baseColor;
                Points = points;
                Line.IsVisible = false;
                Symbol.Type = SymbolType.Circle;
                Symbol.Size = 6f;
                Symbol.IsVisible = true;
                Symbol.Fill = new Fill { Type = FillType.None };
                // Seed with the alpha for the full point set; Draw refines it for the current zoom.
                Symbol.Border = new Border(true, AlphaColor(GetAlpha(points.Count)), 1.5f) { IsAntiAlias = true };
                Label.IsVisible = true;
            }

            public override void Draw(Graphics g, GraphPane pane, int pos, float scaleFactor)
            {
                Symbol.Border.Color = AlphaColor(GetAlphaForPane(pane));
                base.Draw(g, pane, pos, scaleFactor);
            }

            internal int GetAlphaForPane(GraphPane pane)
            {
                return GetAlpha(CountVisiblePoints(pane));
            }

            internal int CountVisiblePoints(GraphPane pane)
            {
                var xScale = pane.XAxis.Scale;
                var yScale = pane.YAxis.Scale;
                int count = 0;
                for (int i = 0; i < Points.Count; i++)
                {
                    var pt = Points[i];
                    if (pt.X >= xScale.Min && pt.X <= xScale.Max &&
                        pt.Y >= yScale.Min && pt.Y <= yScale.Max)
                    {
                        count++;
                    }
                }
                return count;
            }

            private Color AlphaColor(int alpha)
            {
                return Color.FromArgb(alpha, _baseColor);
            }

            private static int GetAlpha(int visiblePointCount)
            {
                if (!AreaGraphController.RtLoessAdaptiveAlpha || visiblePointCount <= 0)
                {
                    return 255;
                }
                double fraction = ALPHA_BUDGET / visiblePointCount;
                fraction = Math.Max(ALPHA_MIN, Math.Min(ALPHA_MAX, fraction));
                return (int) Math.Round(fraction * 255);
            }
        }

        public override bool HandleMouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (TryFindPeptideAt(e.Location, out _) || TryFindReplicateIndexAt(sender, e.Location, out _))
            {
                sender.Cursor = Cursors.Hand;
                return true;
            }

            return base.HandleMouseMoveEvent(sender, e);
        }

        public override void HandleMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            if (!(sender is ZedGraphControl ctx))
                return;

            // A click on an individual peptide point selects that peptide in the Targets tree.
            if (TryFindPeptideAt(e.Location, out var identityPath))
            {
                GraphSummary.StateProvider.SelectPath(identityPath);
                GraphSummary.Focus();
                return;
            }

            if (!TryFindReplicateIndexAt(ctx, e.Location, out var replicateIndex))
                return;

            var document = GraphSummary.DocumentUIContainer.DocumentUI;
            if (!document.Settings.HasResults ||
                replicateIndex < 0 ||
                replicateIndex >= document.Settings.MeasuredResults.Chromatograms.Count)
            {
                return;
            }
            GraphSummary.StateProvider.SelectedResultsIndex = replicateIndex;
            GraphSummary.Focus();
        }

        private bool TryFindReplicateIndexAt(ZedGraphControl sender, PointF mousePt, out int replicateIndex)
        {
            replicateIndex = -1;
            using (var g = sender.CreateGraphics())
            {
                if (FindNearestObject(mousePt, g, out var nearestObj, out _) &&
                    nearestObj is CurveItem curveItem && curveItem.Tag is int tagIndex)
                {
                    replicateIndex = tagIndex;
                    return true;
                }
            }

            if (FindNearestPoint(mousePt, out var nearestCurve, out _) &&
                nearestCurve?.Tag is int pointIndex)
            {
                replicateIndex = pointIndex;
                return true;
            }

            return false;
        }

        private bool TryFindPeptideAt(PointF mousePt, out IdentityPath identityPath)
        {
            identityPath = null;
            if (_peptidesCurve == null)
            {
                return false;
            }
            if (FindNearestPoint(mousePt, out var nearestCurve, out var iNearest) &&
                ReferenceEquals(nearestCurve, _peptidesCurve) &&
                iNearest >= 0 && iNearest < _peptidesCurve.Points.Count)
            {
                identityPath = _peptidesCurve.Points[iNearest].Tag as IdentityPath;
                return identityPath != null;
            }

            return false;
        }

        public override void OnClose(EventArgs e)
        {
            base.OnClose(e);
            _calcListener.Dispose();
            _progressBar?.Dispose();
            _progressBar = null;
        }
    }
}
