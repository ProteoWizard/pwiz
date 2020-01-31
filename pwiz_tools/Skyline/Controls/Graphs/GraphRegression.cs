/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class GraphRegression : FormEx
    {
        public GraphRegression(ICollection<RegressionGraphData> regressionGraphDatas)
        {
            InitializeComponent();

            RegressionGraphDatas = regressionGraphDatas;

            Icon = Resources.Skyline;

            if (regressionGraphDatas.Count > 1)
            {
                // Add extra height, if their will be more than one pane.
                Height += graphControl.Height + 10;
            }

            var masterPane = graphControl.MasterPane;
            masterPane.PaneList.Clear();
            masterPane.Border.IsVisible = false;

            foreach (var graphData in regressionGraphDatas)
            {
                masterPane.PaneList.Add(new RegressionGraphPane(graphData));
            }
        }

        protected override void OnShown(EventArgs e)
        {
            // Tell ZedGraph to auto layout all the panes
            using (Graphics g = CreateGraphics())
            {
                graphControl.MasterPane.SetLayout(g, PaneLayout.SingleColumn);
                graphControl.AxisChange();
            }
        }

        public ICollection<RegressionGraphData> RegressionGraphDatas { get; private set; }

        private void btnClose_Click(object sender, EventArgs e)
        {
            CloseDialog();
        }

        public void CloseDialog()
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void graphControl_ContextMenuBuilder(ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip,
            Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            for (int i = menuStrip.Items.Count - 1; i >= 0; i--)
            {
                string tag = (string)menuStrip.Items[i].Tag;
                if (tag == @"unzoom")
                    menuStrip.Items.Insert(i, new ToolStripSeparator());
                if (tag == @"set_default" || tag == @"show_val")
                    menuStrip.Items.RemoveAt(i);
            }
            CopyEmfToolStripMenuItem.AddToContextMenu(zedGraphControl, menuStrip);
        }
    }

    public sealed class RegressionGraphPane : GraphPane
    {
        public static readonly Color COLOR_REGRESSION = Color.DarkBlue;
        public static readonly Color COLOR_LINE_REGRESSION = Color.DarkBlue;
        public static readonly Color COLOR_LINE_REGRESSION_CURRENT = Color.Black;
        private static readonly Color COLOR_MISSING = Color.Red;
        private static readonly Color COLOR_OUTLIERS = Color.BlueViolet;

        private readonly RegressionGraphData _graphData;
        private readonly string _labelRegression;
        private readonly string _labelRegressionCurrent;

        public RegressionGraphPane(RegressionGraphData graphData)
        {
            _graphData = graphData;

            Title.Text = graphData.Title;
            XAxis.Title.Text = graphData.LabelX;
            YAxis.Title.Text = graphData.LabelY;
            Border.IsVisible = false;
            Title.IsVisible = true;
            Chart.Border.IsVisible = false;
            XAxis.Scale.MaxAuto = true;
            XAxis.Scale.MinAuto = true;
            YAxis.Scale.MaxAuto = true;
            YAxis.Scale.MinAuto = true;
            Y2Axis.IsVisible = false;
            X2Axis.IsVisible = false;
            XAxis.MajorTic.IsOpposite = false;
            YAxis.MajorTic.IsOpposite = false;
            XAxis.MinorTic.IsOpposite = false;
            YAxis.MinorTic.IsOpposite = false;
            IsFontsScaled = false;
            YAxis.Scale.MaxGrace = 0.1;

//            Legend.FontSpec.Size = 12;

            var curve = AddCurve(Resources.RegressionGraphPane_RegressionGraphPane_Values, graphData.RegularPoints, Color.Black, SymbolType.Diamond);
            curve.Line.IsVisible = false;
            curve.Symbol.Border.IsVisible = false;
            curve.Symbol.Fill = new Fill(COLOR_REGRESSION);

            if (graphData.MissingPoints.Any())
            {
                var curveMissing = AddCurve(Resources.RegressionGraphPane_RegressionGraphPane_Missing, graphData.MissingPoints, COLOR_MISSING, SymbolType.Diamond);
                curveMissing.Line.IsVisible = false;
                curveMissing.Symbol.Border.IsVisible = false;
                curveMissing.Symbol.Fill = new Fill(COLOR_MISSING);
                curveMissing.Symbol.Size = 12;
            }

            if (graphData.OutlierPoints.Any())
            {
                var curveOutliers = AddCurve(Resources.RegressionGraphPane_RegressionGraphPane_Outliers, graphData.OutlierPoints, COLOR_OUTLIERS, SymbolType.Diamond);
                curveOutliers.Line.IsVisible = false;
                curveOutliers.Symbol.Border.IsVisible = false;
                curveOutliers.Symbol.Fill = new Fill(COLOR_OUTLIERS);
            }

            var regressionLine = graphData.RegressionLine;
            if (graphData.RegressionLine != null)
            {
                graphData.GetCurvePoints(true, regressionLine is RegressionLine ? 2 : 1000, out var lineX, out var lineY);

                curve = AddCurve(
                    !string.IsNullOrEmpty(graphData.RegressionName)
                        ? graphData.RegressionName
                        : Resources.RegressionGraphPane_RegressionGraphPane_Regression, lineX, lineY,
                    COLOR_LINE_REGRESSION,
                    SymbolType.None);
                curve.Line.IsAntiAlias = true;
                curve.Line.IsOptimizedDraw = true;

                if (!Equals(regressionLine.DisplayEquation, Resources.DisplayEquation_N_A))
                    _labelRegression = regressionLine.DisplayEquation + Environment.NewLine;
                _labelRegression += string.Format(@"r = {0:F03}", graphData.R);
                if (graphData.R < graphData.MinCorrelation)
                {
                    _labelRegression += string.Format(@" < {0:F03}", graphData.MinCorrelation);
                    if (graphData.MinPoints.HasValue)
                        _labelRegression = string.Format(Resources.RegressionGraphPane_RegressionGraphPane__0___at__1__points_minimum_, _labelRegression, graphData.MinPoints.Value);
                }
            }

            var regressionLineCurrent = graphData.RegressionLineCurrent;
            if (regressionLineCurrent != null)
            {
                graphData.GetCurvePoints(false, regressionLineCurrent is RegressionLine ? 2 : 1000, out var lineX, out var lineY);

                curve = AddCurve(Resources.RegressionGraphPane_RegressionGraphPane_Current, lineX, lineY, COLOR_LINE_REGRESSION_CURRENT, SymbolType.None);
                curve.Line.IsAntiAlias = true;
                curve.Line.IsOptimizedDraw = true;
                curve.Line.Style = DashStyle.Dash;

                if (!ReferenceEquals(regressionLineCurrent.DisplayEquation, Resources.DisplayEquation_N_A))
                    _labelRegressionCurrent = regressionLineCurrent.DisplayEquation + Environment.NewLine;
                if (graphData.ShowCurrentCorrelation)
                    _labelRegressionCurrent += string.Format(@"r = {0:F03}", graphData.RCurrent);
            }
        }

        public override void Draw(Graphics g)
        {
            GraphObjList.Clear();

            if (_graphData != null)
            {
                // Force Axes to recalculate to ensure proper layout of labels
                AxisChange(g);

                // Reposition the regression label.
                RectangleF rectChart = Chart.Rect;
                PointF ptTop = rectChart.Location;

                // Setup axes scales to enable the ReverseTransform method
                XAxis.Scale.SetupScaleData(this, XAxis);
                YAxis.Scale.SetupScaleData(this, YAxis);

                float yNext = ptTop.Y;
                double left = XAxis.Scale.ReverseTransform(ptTop.X + 8);
                FontSpec fontSpec = GraphSummary.CreateFontSpec(COLOR_LINE_REGRESSION);
                if (_labelRegression != null)
                {
                    // Add regression text
                    double top = YAxis.Scale.ReverseTransform(yNext);
                    TextObj text = new TextObj(_labelRegression, left, top,
                                               CoordType.AxisXYScale, AlignH.Left, AlignV.Top)
                    {
                        IsClippedToChartRect = true,
                        ZOrder = ZOrder.E_BehindCurves,
                        FontSpec = fontSpec,
                    };
                    //                text.FontSpec.Size = 12;
                    GraphObjList.Add(text);
                }

                if (_labelRegressionCurrent != null)
                {
                    // Add text for current regression
                    SizeF sizeLabel = fontSpec.MeasureString(g, _labelRegression, CalcScaleFactor());
                    yNext += sizeLabel.Height + 3;
                    double top = YAxis.Scale.ReverseTransform(yNext);
                    TextObj text = new TextObj(_labelRegressionCurrent, left, top,
                                               CoordType.AxisXYScale, AlignH.Left, AlignV.Top)
                    {
                        IsClippedToChartRect = true,
                        ZOrder = ZOrder.E_BehindCurves,
                        FontSpec = GraphSummary.CreateFontSpec(COLOR_LINE_REGRESSION_CURRENT),
                    };
//                    text.FontSpec.Size = 12;
                    GraphObjList.Add(text);
                }
            }

            base.Draw(g);
        }
    }

    public sealed class RegressionGraphData
    {
        public string Title { get; set; }
        public string LabelX { get; set; }
        public string LabelY { get; set; }
        public double[] XValues { get; set; }
        public double[] YValues { get; set; }
        public Dictionary<int, string> Tooltips { get; set; }
        public HashSet<int> MissingIndices { get; set; }
        public HashSet<int> OutlierIndices { get; set; }
        public IIrtRegression RegressionLine { get; set; } // after removing points
        public IIrtRegression RegressionLineCurrent { get; set; } // before removing points
        public string RegressionName { get; set; }
        public double? MinCorrelation { get; set; }
        public int? MinPoints { get; set; }
        public bool ShowCurrentCorrelation { get; set; }

        private PointPairList _regularPoints;
        public PointPairList RegularPoints
        {
            get
            {
                if (_regularPoints == null)
                {
                    _regularPoints = new PointPairList();
                    for (var i = 0; i < XValues.Length; i++)
                    {
                        if ((MissingIndices == null || !MissingIndices.Contains(i)) &&
                            (OutlierIndices == null || !OutlierIndices.Contains(i)))
                        {
                            var point = new PointPair(XValues[i], YValues[i]);
                            string tooltip;
                            if (Tooltips != null && Tooltips.TryGetValue(i, out tooltip))
                                point.Tag = tooltip;
                            _regularPoints.Add(point);
                        }
                    }
                }
                return _regularPoints;
            }
        }
        
        private PointPairList _missingPoints;
        public PointPairList MissingPoints
        {
            get
            {
                if (_missingPoints == null)
                {
                    _missingPoints = new PointPairList();
                    if (MissingIndices != null)
                        foreach (var i in MissingIndices)
                        {
                            var point = new PointPair(0, YValues[i]);
                            string tooltip;
                            if (Tooltips != null && Tooltips.TryGetValue(i, out tooltip))
                                point.Tag = tooltip;
                            _missingPoints.Add(point);
                        }
                }
                return _missingPoints;
            }
        }
        
        private PointPairList _outlierPoints;
        public PointPairList OutlierPoints
        {
            get
            {
                if (_outlierPoints == null)
                {
                    _outlierPoints = new PointPairList();
                    if (OutlierIndices != null)
                        foreach (var i in OutlierIndices)
                        {
                            var point = new PointPair(XValues[i], YValues[i]);
                            string tooltip;
                            if (Tooltips != null && Tooltips.TryGetValue(i, out tooltip))
                                point.Tag = tooltip;
                            _outlierPoints.Add(point);
                        }
                }
                return _outlierPoints;
            }
        }

        public double R => GetR(RegressionLine, false);

        public double RCurrent => GetR(RegressionLineCurrent, true);

        private double GetR(IIrtRegression regression, bool includeOutliers)
        {
            var r = IrtRegression.R(regression);
            if (!double.IsNaN(r))
                return r;
            var pointSet = !includeOutliers ? RegularPoints.ToArray() : RegularPoints.Concat(OutlierPoints).ToArray();
            var statsX = new Statistics(pointSet.Select(point => point.X));
            var statsY = new Statistics(pointSet.Select(point => point.Y));
            return statsY.R(statsX);
        }

        public void GetCurvePoints(bool refined, int numPoints, out double[] x, out double[] y)
        {
            if (numPoints < 2)
                throw new ArgumentOutOfRangeException();

            var min = double.MaxValue;
            var max = double.MinValue;
            foreach (var point in RegularPoints)
            {
                if (point.X < min)
                    min = point.X;
                if (point.X > max)
                    max = point.X;
            }

            if (!refined)
            {
                foreach (var point in OutlierPoints)
                {
                    if (point.X < min)
                        min = point.X;
                    if (point.X > max)
                        max = point.X;
                }
            }

            x = new double[numPoints];
            x[0] = min;
            var interval = (max - min) / (numPoints - 1);
            for (var i = 1; i < numPoints - 1; i++)
                x[i] = min + i * interval;
            x[x.Length - 1] = max;
            y = refined
                ? x.Select(xValue => RegressionLine.GetY(xValue)).ToArray()
                : x.Select(xValue => RegressionLineCurrent.GetY(xValue)).ToArray();
        }
    }
}
