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
using pwiz.Common.Collections;
using pwiz.Common.Colors;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Themes;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    internal class AreaRtLoessGraphPane : SummaryGraphPane
    {
        private readonly Receiver<ReferenceValue<SrmDocument>, RtLoessCurves> _calcListener;
        private PaneProgressBar _progressBar;

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
                    if (i == GraphSummary.ResultsIndex)
                    {
                        color = ColorScheme.ChromGraphItemSelected;
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
                        curve.Line.Width = 1.5f;
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

            Legend.IsVisible = showValue != RtLoessShowValue.NormalizedMedian &&
                               CurveList.Any(c => c.Label.IsVisible);

            AxisChange();
        }

        public override bool HandleMouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (TryFindReplicateIndexAt(sender, e.Location, out _))
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

        public override void OnClose(EventArgs e)
        {
            base.OnClose(e);
            _calcListener.Dispose();
            _progressBar?.Dispose();
            _progressBar = null;
        }
    }
}
