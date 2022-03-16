/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;
using Settings = pwiz.Skyline.Controls.Graphs.DetectionsGraphController.Settings;

namespace pwiz.Skyline.Controls.Graphs
{

    public class DetectionsByReplicatePane : DetectionsPlotPane

    {

        public DetectionsByReplicatePane(GraphSummary graphSummary) : base(graphSummary)
        {
            XAxis.Type = AxisType.Text;
            XAxis.Title.Text = Resources.DetectionPlotPane_XAxis_Name;
        }

        public override void PopulateTooltip(int index)
        {
            ToolTip.ClearData();
            var targetData = _detectionData.GetTargetData(Settings.TargetType);
            ToolTip.AddLine(Resources.DetectionPlotPane_Tooltip_Replicate,
                _detectionData.ReplicateNames[index]);
            ToolTip.AddLine(string.Format(Resources.DetectionPlotPane_Tooltip_Count, Settings.TargetType.Label),
                targetData.TargetsCount[index].ToString(CultureInfo.CurrentCulture));
            ToolTip.AddLine(Resources.DetectionPlotPane_Tooltip_CumulativeCount,
                targetData.TargetsCumulative[index].ToString(CultureInfo.CurrentCulture));
            ToolTip.AddLine(Resources.DetectionPlotPane_Tooltip_AllCount,
                targetData.TargetsAll[index].ToString(CultureInfo.CurrentCulture));
            var qString = @"-Inf";
            if (targetData.QMedians[index] > 0)
                qString = (-Math.Log10(targetData.QMedians[index])).ToString(@"F1",
                    CultureInfo.CurrentCulture);
            ToolTip.AddLine(Resources.DetectionPlotPane_Tooltip_QMedian, qString);
        }

        protected override void HandleMouseClick(int index)
        {
            ChangeSelectedIndex(index);
        }

        public override ImmutableList<float> GetToolTipDataSeries()
        {
            return ImmutableList.ValueOf(TargetData.TargetsCount.Select(n => (float)n));
        }


        public override void UpdateGraph(bool selectionChanged)
        {
            GraphObjList.Clear();
            CurveList.Clear();
            Legend.IsVisible = false;
            if (!DetectionPlotData.GetDataCache().TryGet(
                GraphSummary.DocumentUIContainer.DocumentUI, Settings.QValueCutoff, this.DataCallback,
                out _detectionData))
                return;

            AddLabels();
            BarSettings.Type = BarType.SortedOverlay;
            BarSettings.MinClusterGap = 0.3f;
            Legend.IsVisible = Settings.ShowLegend;

            var emptySymbol = new Symbol(SymbolType.None, Color.Transparent);
            //draw bars
            var counts = TargetData.TargetsCount;
            var countPoints = new PointPairList(Enumerable.Range(0, _detectionData.ReplicateCount)
                .Select(i => new PointPair(i, counts[i]/ YScale)).ToList());
            ToolTip.TargetCurves.ClearAndAdd(MakeBarItem(countPoints, Color.FromArgb(180, 220, 255)));
            CurveList.Insert(0, ToolTip.TargetCurves[0]);
            //draw cumulative curve
            counts = TargetData.TargetsCumulative;
            var cumulativePoints = new PointPairList(Enumerable.Range(0, _detectionData.ReplicateCount)
                .Select(i => new PointPair(i, counts[i] / YScale)).ToList());
            CurveList.Insert(1, 
                new LineItem(Resources.DetectionPlotPane_CumulativeLine_Name)
                {   Points = cumulativePoints,
                    Symbol = emptySymbol,
                    Line = new Line() { Color = Color.Coral, Width = 2}

                });
            //draw inclusive curve
            counts = TargetData.TargetsAll;
            var allPoints = new PointPairList(Enumerable.Range(0, _detectionData.ReplicateCount)
                .Select(i => new PointPair(i, counts[i] / YScale)).ToList());
            CurveList.Insert(2, 
                new LineItem(Resources.DetectionPlotPane_AllRunsLine_Name)
                { Symbol = emptySymbol,
                    Points = allPoints,
                    Line = new Line() { Color = Color.Black, Width = 2}
                });

            //axes formatting
            XAxis.Scale.Max = _detectionData.ReplicateCount + 1;
            YAxis.Scale.Max = _detectionData.GetTargetData(Settings.TargetType).MaxCount/ YScale * 1.15;
            if (Settings.ShowAtLeastN)
            {
                double lineY = TargetData.getCountForMinReplicates(Settings.RepCount);
                var atLeastLine = new Line() {Width = 1, Color = Color.Blue, Style = DashStyle.Dash};
                var dummyPoints = new PointPairList( new[] {new PointPair(0, 0)} );
                var line = new LineObj(Color.Blue, 0, lineY / YScale, XAxis.Scale.Max, lineY / YScale)
                {
                    IsClippedToChartRect = true,
                    Line = atLeastLine
                };
                GraphObjList.Add(line);

                //This is a placeholder to make sure the line shows in the legend.
                CurveList.Insert(3,
                    new LineItem(String.Format(CultureInfo.CurrentCulture, 
                        Resources.DetectionPlotPane_AtLeastLine_Name, 
                        Settings.RepCount, _detectionData.ReplicateCount, lineY))
                    {
                        Symbol = emptySymbol,
                        Points = dummyPoints,
                        Line = atLeastLine
                    });
            }

            if (Settings.ShowSelection)
            {
                var selectedIndex = GraphSummary.StateProvider.SelectedResultsIndex;
                var lineLength = TargetData.TargetsCount[selectedIndex] / YScale + YAxis.Scale.Max * 0.05;
                GraphObjList.Add(
                    new LineObj(Color.Black, selectedIndex + 1, 0, selectedIndex + 1, lineLength)
                    {
                        IsClippedToChartRect = true,
                        Line = new Line() { Width = 1, Color = Color.Black, Style = DashStyle.Dash }
                    });
            }

            if (Settings.ShowMean)
            {
                var stats = new Statistics(TargetData.TargetsCount.Select((x) => (double)x));
                var labelText = String.Format(CultureInfo.CurrentCulture, 
                    TextUtil.LineSeparate(new[]
                        {
                            Resources.DetectionPlotPane_Label_Mean ,
                            Resources.DetectionPlotPane_Label_Stddev
                        }
                    ), 
                    stats.Mean(), stats.StdDev());
                GraphObjList.Add(new TextObj(labelText, 0.1, YAxis.Scale.Max,
                    CoordType.AxisXYScale, AlignH.Left, AlignV.Top)
                {
                    IsClippedToChartRect = true,
                    ZOrder = ZOrder.E_BehindCurves,
                    FontSpec = GraphSummary.CreateFontSpec(Color.Black),
                });
            }
        }

        protected override void AddLabels()
        {
            if (_detectionData.IsValid)
            {
                XAxis.Scale.TextLabels = _detectionData.ReplicateNames.ToArray();
            }
            base.AddLabels();
        }
    }
}
