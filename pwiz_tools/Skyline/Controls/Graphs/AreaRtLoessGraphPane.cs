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

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    internal class AreaRtLoessGraphPane : SummaryGraphPane
    {
        public AreaRtLoessGraphPane(GraphSummary graphSummary)
            : base(graphSummary)
        {
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

            var normalizationData = NormalizationData.GetNormalizationData(document, false, null);
            if (!normalizationData.HasRtLoessCurves)
            {
                Title.Text = GraphsResources.AreaRtLoessGraphPane_No_RT_LOESS_Data;
                return;
            }

            bool showNormalized = Settings.Default.RtLoessShowNormalized;
            var globalRtGrid = normalizationData.GetGlobalMedianRtGrid();
            var globalFittedValues = normalizationData.GetGlobalMedianFittedValues();

            var colors = GraphChromatogram.COLORS_GROUPS;
            var curvesByReplicate = new Dictionary<int, List<NormalizationData.RtLoessCurveInfo>>();
            foreach (var curveInfo in normalizationData.GetRtLoessCurves())
            {
                if (!curvesByReplicate.TryGetValue(curveInfo.ReplicateIndex, out var list))
                {
                    list = new List<NormalizationData.RtLoessCurveInfo>();
                    curvesByReplicate[curveInfo.ReplicateIndex] = list;
                }
                list.Add(curveInfo);
            }

            int iColor = 0;
            var measuredResults = document.MeasuredResults;
            if (measuredResults != null)
            {
                for (int i = 0; i < measuredResults.Chromatograms.Count; i++)
                {
                    if (!curvesByReplicate.TryGetValue(i, out var curves))
                        continue;

                    string replicateName = measuredResults.Chromatograms[i].Name;
                    var color = colors[iColor++ % colors.Count];

                    foreach (var curveInfo in curves)
                    {
                        var pointPairList = new PointPairList();
                        for (int j = 0; j < curveInfo.RtGrid.Length; j++)
                        {
                            double y = curveInfo.FittedValues[j];
                            if (showNormalized && globalFittedValues != null)
                                y -= globalFittedValues[j];
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
                        curve.Label.IsVisible = false;
                    }
                }
            }

            if (!showNormalized && globalRtGrid != null && globalFittedValues != null)
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
            YAxis.Title.Text = showNormalized
                ? GraphsResources.AreaRtLoessGraphPane_Log2_Adjustment
                : GraphsResources.AreaRtLoessGraphPane_Log2_Abundance;
            Title.Text = showNormalized
                ? GraphsResources.AreaRtLoessGraphPane_RT_LOESS_Curves_Normalized
                : GraphsResources.AreaRtLoessGraphPane_RT_LOESS_Curves;

            Legend.IsVisible = !showNormalized && CurveList.Any(c => c.Label.IsVisible);

            AxisChange();
        }
    }
}
