/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{

    public class DetectionsPlotPane : SummaryReplicateGraphPane, IDisposable, ITipDisplayer    // CONSIDER: Base class instead?

    {
        public class IntLabeledValue : LabeledValues<int>
        {
            protected IntLabeledValue(int value, Func<string> getLabelFunc) : base(value, getLabelFunc)
            {
                Value = value;
            }
            public float Value { get; private set; }

            public override string ToString()
            {
                return Label;
            }

            public static IEnumerable<T> GetValues<T>() where T : IntLabeledValue
            {
                return (IEnumerable<T>)typeof(T).InvokeMember("GetValues", BindingFlags.InvokeMethod, 
                    null, null, new object[0]);
            }

            public static T GetDefaultValue<T>() where T : IntLabeledValue
            {
                return (T)typeof(T).InvokeMember("GetDefaultValue", BindingFlags.InvokeMethod,
                    null, null, new object[0]);
            }

            public static T GetFromString<T>(string str) where T : IntLabeledValue
            {
                var res = GetValues<T>().FirstOrDefault(
                    (t) => t.Label.Equals(str));
                if (res == default(T))
                    return GetDefaultValue<T>();
                else return res;
            }

            public static void PopulateCombo<T>(ComboBox comboBox, T currentValue) where T : IntLabeledValue
            {
                comboBox.Items.Clear();
                foreach (var val in GetValues<T>())
                {
                    comboBox.Items.Add(val);
                    if (Equals(val, currentValue))
                    {
                        comboBox.SelectedIndex = comboBox.Items.Count - 1;
                    }
                }
            }
            public static void PopulateCombo<T>(ToolStripComboBox comboBox, T currentValue) where T : IntLabeledValue
            {
                comboBox.Items.Clear();
                foreach (var val in GetValues<T>())
                {
                    comboBox.Items.Add(val);
                    if (Equals(val, currentValue))
                    {
                        comboBox.SelectedIndex = comboBox.Items.Count - 1;
                    }
                }
            }

            public static T GetValue<T>(ComboBox comboBox, T defaultVal) where T : IntLabeledValue
            {
                return comboBox.SelectedItem as T ?? defaultVal;
            }
            public static T GetValue<T>(ToolStripComboBox comboBox, T defaultVal) where T : IntLabeledValue
            {
                return comboBox.SelectedItem as T ?? defaultVal;
            }
        }

        public class TargetType : IntLabeledValue
        {
            private TargetType(int value, Func<string> getLabelFunc) : base(value, getLabelFunc){}

            public static readonly TargetType PRECURSOR = new TargetType(0, () => Resources.DetectionPlot_TargetType_Precursor);
            public static readonly TargetType PEPTIDE = new TargetType(1, () => Resources.DetectionPlot_TargetType_Peptide);

            public static IEnumerable<TargetType> GetValues()
            {
                return new[] {PRECURSOR, PEPTIDE};
            }

            public static TargetType GetDefaultValue()
            {
                return PRECURSOR;
            }
        }

        public class YScaleFactorType : IntLabeledValue
        {
            private YScaleFactorType(int value, Func<string> getLabelFunc) : base(value, getLabelFunc) { }

            public static readonly YScaleFactorType ONE = new YScaleFactorType(1, () => Resources.DetectionPlot_YScale_One);
            public static readonly YScaleFactorType HUNDRED = new YScaleFactorType(100, () => Resources.DetectionPlot_YScale_Hundred);
            public static readonly YScaleFactorType THOUSAND = new YScaleFactorType(1000, () => Resources.DetectionPlot_YScale_Thousand);

            public static IEnumerable<YScaleFactorType> GetValues()
            {
                return new[] { ONE, HUNDRED, THOUSAND };
            }
            public static YScaleFactorType GetDefaultValue()
            {
                return THOUSAND;
            }
        }


        //        public enum TargetType { precursor, peptide }
        //public enum YScaleFactorType { one = 1, hundreds = 100, thousands = 1000}

        public class Settings
        {
            public static float QValueCutoff
            {
                get => pwiz.Skyline.Properties.Settings.Default.DetectionsQValueCutoff; 
                set => pwiz.Skyline.Properties.Settings.Default.DetectionsQValueCutoff = value; 
            }

            public static TargetType TargetType
            {
                get => IntLabeledValue.GetFromString<TargetType>(
                        pwiz.Skyline.Properties.Settings.Default.DetectionsTargetType);
                set => pwiz.Skyline.Properties.Settings.Default.DetectionsTargetType = value.ToString(); 
            }
            public static YScaleFactorType YScaleFactor
            {
                get => IntLabeledValue.GetFromString<YScaleFactorType>(
                        pwiz.Skyline.Properties.Settings.Default.DetectionsYScaleFactor);
                set => pwiz.Skyline.Properties.Settings.Default.DetectionsYScaleFactor = value.ToString(); 
            }

            public static int RepCount
            {
                get => pwiz.Skyline.Properties.Settings.Default.DetectionsRepCount; 
                set => pwiz.Skyline.Properties.Settings.Default.DetectionsRepCount = value; 
            }

            public static float FontSize
            {
                get => pwiz.Skyline.Properties.Settings.Default.AreaFontSize; 
                set => pwiz.Skyline.Properties.Settings.Default.AreaFontSize = value; 
            }

            public static bool ShowAtLeastN
            {
                get => pwiz.Skyline.Properties.Settings.Default.DetectionsShowAtLeastN; 
                set => pwiz.Skyline.Properties.Settings.Default.DetectionsShowAtLeastN = value; 
            }

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public static bool ShowSelection
            {
                get => pwiz.Skyline.Properties.Settings.Default.DetectionsShowSelection;
                set => pwiz.Skyline.Properties.Settings.Default.DetectionsShowSelection = value;
            }

            public static bool ShowMean
            {
                get => pwiz.Skyline.Properties.Settings.Default.DetectionsShowMean; 
                set => pwiz.Skyline.Properties.Settings.Default.DetectionsShowMean = value; 
            }
            public static bool ShowLegend
            {
                get => pwiz.Skyline.Properties.Settings.Default.DetectionsShowLegend; 
                set => pwiz.Skyline.Properties.Settings.Default.DetectionsShowLegend = value; 
            }
        }

        public class ToolTip : ITipProvider, IDisposable
        {

            private DetectionsPlotPane _parent;
            // ReSharper disable once RedundantDefaultMemberInitializer
            private bool _isVisible = false;
            private UserPoint _basePoint;
            private NodeTip _tip;
            private TableDesc _table;
            private readonly RenderTools _rt = new RenderTools();

            //Location is in user coordinates. 
            public int ReplicateIndex { get; private set; }

            public ToolTip(DetectionsPlotPane parent)
            {
                _parent = parent;
            }

            public ITipProvider TipProvider { get { return this;} }

            bool ITipProvider.HasTip => true;

            Size ITipProvider.RenderTip(Graphics g, Size sizeMax, bool draw)
            {
                var size = _table.CalcDimensions(g);
                if (draw)
                    _table.Draw(g);
                return new Size((int)size.Width + 2, (int)size.Height + 2);
            }


            public void Draw(int dataIndex, Point cursorPos)
            {
                if(_isVisible && ReplicateIndex != dataIndex) Hide();
                if (_isVisible && ReplicateIndex == dataIndex) return;

                ReplicateIndex = dataIndex;
                DetectionPlotData.DataSet targetData = _parent._detectionData.GetData(Settings.TargetType);
                float yScale = Settings.YScaleFactor.Value;
                _basePoint = new UserPoint(dataIndex + 1, targetData.TargetsCount.ToList()[ReplicateIndex] / yScale, _parent);

                _table = new TableDesc();
                _table.AddDetailRow(Resources.DetectionPlotPane_Tooltip_Replicate, 
                    _parent._detectionData.ReplicateNames[ReplicateIndex], _rt);
                _table.AddDetailRow( String.Format(Resources.DetectionPlotPane_Tooltip_Count, Settings.TargetType.Label), 
                    targetData.TargetsCount[ReplicateIndex].ToString(CultureInfo.CurrentCulture), _rt);
                _table.AddDetailRow(Resources.DetectionPlotPane_Tooltip_CumulativeCount,
                    targetData.TargetsCumulative[ReplicateIndex].ToString(CultureInfo.CurrentCulture), _rt);
                _table.AddDetailRow(Resources.DetectionPlotPane_Tooltip_AllCount,
                    targetData.TargetsAll[ReplicateIndex].ToString(CultureInfo.CurrentCulture), _rt);
                var qString = @"-Inf";
                if (targetData.QMedians[ReplicateIndex] > 0)
                    qString = (-Math.Log10(targetData.QMedians[ReplicateIndex])).ToString(@"F1",
                        CultureInfo.CurrentCulture);
                _table.AddDetailRow(Resources.DetectionPlotPane_Tooltip_QMedian, qString, _rt);

                using (var g = _parent.GraphSummary.GraphControl.CreateGraphics())
                {
                    var size = _table.CalcDimensions(g);
                    var offset = new Size(0, -(int)(1.3 * size.Height));
                    if (_tip == null)
                        _tip = new NodeTip(_parent);
                    _tip.SetTipProvider(TipProvider, new Rectangle(_basePoint.Screen(offset), new Size()), cursorPos);
                }
                _isVisible = true;
            }

            public void Hide()
            {
                if (_isVisible)
                {
                    if (_tip != null)
                        _tip.HideTip();
                    _isVisible = false;
                }
            }

            public void Dispose()
            {
                _rt.Dispose();
            }

            private class UserPoint
            {
                private GraphPane _graph;
                public int X { get; private set; }
                public float Y { get; private set; }

                public UserPoint(int x, float y, GraphPane graph)
                {
                    X = x;
                    Y = y;
                    _graph = graph;
                }

                public PointF User()
                {
                    return new PointF(X, Y);
                }

                public Point Screen()
                {
                    return new Point(
                        (int)_graph.XAxis.Scale.Transform(X),
                        (int)_graph.YAxis.Scale.Transform(Y));
                }
                public Point Screen(Size OffsetScreen)
                {
                    return new Point(
                        (int)(_graph.XAxis.Scale.Transform(X) + OffsetScreen.Width),
                        (int)(_graph.YAxis.Scale.Transform(Y) + OffsetScreen.Height));
                }

                public PointF PF()
                {
                    return new PointF(
                        _graph.XAxis.Scale.Transform(X) / _graph.Rect.Width,
                        _graph.YAxis.Scale.Transform(Y) / _graph.Rect.Height);
                }
                public PointD PF(SizeF OffsetPF)
                {
                    return new PointD(
                        _graph.XAxis.Scale.Transform(X) / _graph.Rect.Width + OffsetPF.Width,
                        _graph.YAxis.Scale.Transform(Y) / _graph.Rect.Height + OffsetPF.Height);
                }
            }
        }

        private DetectionPlotData _detectionData = DetectionPlotData.INVALID;
        public int MaxRepCount { get; private set; }

        private ToolTip _toolTip;

        public DetectionsPlotPane(GraphSummary graphSummary) : base(graphSummary)
        {
            MaxRepCount = graphSummary.DocumentUIContainer.DocumentUI.MeasuredResults.Chromatograms.Count;

            Settings.RepCount = MaxRepCount / 2;
            if (GraphSummary.Toolbar is DetectionsToolbar toolbar)
                toolbar.UpdateUI();

            if (GraphSummary.DocumentUIContainer.DocumentUI.Settings.HasResults)
            {
                _detectionData = DetectionPlotData.DataCache.Get(graphSummary.DocumentUIContainer.DocumentUI);
            }
            XAxis.Type = AxisType.Text;
            XAxis.Title.Text = Resources.DetectionPlotPane_XAxis_Name;

            XAxis.Scale.Min = YAxis.Scale.Min = 0;
            XAxis.Scale.MinAuto = XAxis.Scale.MaxAuto = YAxis.Scale.MinAuto = YAxis.Scale.MaxAuto = false;
            _toolTip = new ToolTip(this);
        }

        public override bool HasToolbar { get { return true; } }

        public override void OnClose(EventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            _detectionData.Dispose();
        }

        public override bool HandleMouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            using (var g = sender.CreateGraphics())
            {
                object nearestObject;
                int index;
                DetectionPlotData.DataSet targetData = _detectionData.GetData(Settings.TargetType);
                if (FindNearestObject(e.Location, g, out nearestObject, out index) && nearestObject is BarItem)
                {
                    _toolTip.Draw(index, e.Location);

                    sender.Cursor = Cursors.Hand;
                    return true;
                }
                else
                {
                    if (_toolTip != null)
                    {
                        _toolTip.Hide();
                    }
                    sender.Cursor = Cursors.Cross;
                    return base.HandleMouseMoveEvent(sender, e);
                }
            }
        }

        public override void HandleMouseClick(object sender, MouseEventArgs e)
        {
            //if (_selectedData != null && e.Button == MouseButtons.Left)
            //{
            //    HistogramHelper.CreateAndShowFindResults((ZedGraphControl) sender, GraphSummary, _document, _selectedData);
            //    _selectedData = null;
            //}
            if (sender is Control ctx)
            {
                object nearestObject;
                int index;
                DetectionPlotData.DataSet targetData = _detectionData.GetData(Settings.TargetType);
                using (var g = ctx.CreateGraphics())
                {
                    if (FindNearestObject(e.Location, g, out nearestObject, out index) && nearestObject is BarItem)
                    {
                        ChangeSelectedIndex(index);

                        //                var selectedTreeNode = GraphSummary.StateProvider.SelectedNodes.OfType<SrmTreeNode>().FirstOrDefault();
                    }
                }
            }
        }

        public override void Draw(Graphics g)
        {
            AxisChange(g);
            AddLabels(g);

            base.Draw(g);
        } 

        private double PaneHeightToYValue(double height)
        {
            return height * (YAxis.Scale.Max - YAxis.Scale.Min) / Rect.Height;
        }

        private void DataCallback(AreaCVGraphData data)
        {
            GraphSummary.GraphControl.BeginInvoke((Action) (() => { GraphSummary.UpdateUI(); }));
        }

        public override void UpdateGraph(bool selectionChanged)
        {
            if (!_detectionData.IsValid)
                return;
            if (GraphSummary.Toolbar is DetectionsToolbar toolbar)
            {
                _detectionData = DetectionPlotData.DataCache.Get(GraphSummary.DocumentUIContainer.DocumentUI);
            }
            else
                return;

            BarSettings.Type = BarType.SortedOverlay;
            BarSettings.MinClusterGap = 0.3f;

            GraphObjList.Clear();
            CurveList.Clear();
            Legend.IsVisible = Settings.ShowLegend;

            var emptySymbol = new Symbol(SymbolType.None, Color.Transparent);
            DetectionPlotData.DataSet targetData = _detectionData.GetData(Settings.TargetType);
            //draw bars
            float yScale = Settings.YScaleFactor.Value;
            var counts = targetData.TargetsCount;
            var countPoints = new PointPairList(Enumerable.Range(0, _detectionData.ReplicateCount)
                .Select(i => new PointPair(i, counts[i]/yScale)).ToList());
            CurveList.Insert(0, MakeBarItem(countPoints, Color.FromArgb(180, 220, 255)));
            //draw cumulative curve
            counts = targetData.TargetsCumulative;
            var cumulativePoints = new PointPairList(Enumerable.Range(0, _detectionData.ReplicateCount)
                .Select(i => new PointPair(i, counts[i] / yScale)).ToList());
            CurveList.Insert(1, 
                new LineItem(Resources.DetectionPlotPane_CumulativeLine_Name)
                {  Points = cumulativePoints,
                   Symbol = emptySymbol,
                   Line = new Line() { Color = Color.Coral, Width = 2}

                });
            //draw inclusive curve
            counts = targetData.TargetsAll;
            var allPoints = new PointPairList(Enumerable.Range(0, _detectionData.ReplicateCount)
                .Select(i => new PointPair(i, counts[i] / yScale)).ToList());
            CurveList.Insert(2, 
                new LineItem(Resources.DetectionPlotPane_AllRunsLine_Name)
                    { Symbol = emptySymbol,
                      Points = allPoints,
                      Line = new Line() { Color = Color.Black, Width = 2}
                    });

            //axes formatting
            var fontHeight = GraphSummary.CreateFontSpec(Color.Black).GetHeight(CalcScaleFactor());
            var height = PaneHeightToYValue(fontHeight);

            XAxis.Scale.Max = _detectionData.ReplicateCount + 1;
            XAxis.Scale.TextLabels = _detectionData.ReplicateNames.ToArray();

            YAxis.Scale.Max = _detectionData.GetData(Settings.TargetType).MaxCount/yScale * 1.15;
            YAxis.Title.Text = Resources.DetectionPlotPane_YAxis_Name;
            if (Settings.YScaleFactor != YScaleFactorType.ONE)
                YAxis.Title.Text += @" " + Settings.YScaleFactor.Value.ToString(CultureInfo.CurrentCulture);

            if (Settings.ShowAtLeastN)
            {
                double lineY = targetData.getCountForMinReplicates(Settings.RepCount);
                var atLeastLine = new Line() {Width = 1, Color = Color.Blue, Style = DashStyle.Dash};
                var dummyPoints = new PointPairList( new[] {new PointPair(0, 0)} );
                var line = new LineObj(Color.Blue, 0, lineY / yScale, XAxis.Scale.Max, lineY / yScale)
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
                var lineLength = targetData.TargetsCount[selectedIndex] / yScale + YAxis.Scale.Max * 0.05;
                GraphObjList.Add(
                    new LineObj(Color.Black, selectedIndex + 1, 0, selectedIndex + 1, lineLength)
                    {
                        IsClippedToChartRect = true,
                        Line = new Line() { Width = 1, Color = Color.Black, Style = DashStyle.Dash }
                    });
            }

            if (Settings.ShowMean)
            {
                var stats = new Statistics(targetData.TargetsCount.Select((x) => (double)x));
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

        private BarItem MakeBarItem(PointPairList points, Color color)
        {
            return new BarItem(null, points, color) { Bar =
            {
                Fill = { Type = FillType.Solid }, Border = {InflateFactor = 0.7F}
            } };
        }

        private void AddLabels(Graphics g)
        {
            if (!_detectionData.IsValid)
                Title.Text = Resources.DetectionPlotPane_EmptyPlot_Label;
            else
                Title.Text = string.Empty;
        }

        private StickItem AddStickItem(double fromX, double fromY, double toX, double toY, Color color)
        {
            return new StickItem(null,
                new[] { fromX, fromY },
                new[] { toX, toY }, color, 2.0f) { Line = { Style = DashStyle.Dash } };
        }

        protected override IdentityPath GetIdentityPath(CurveItem curveItem, int barIndex)
        {
            return null;
        }

        protected override void ChangeSelection(int selectedIndex, IdentityPath identityPath)
        {
        }

        protected override int SelectedIndex => throw new NotImplementedException();

        Rectangle ITipDisplayer.RectToScreen(Rectangle r)
        {
            return GraphSummary.GraphControl.RectangleToScreen(r);
        }

        Rectangle ITipDisplayer.ScreenRect { get { return Screen.GetBounds(GraphSummary.GraphControl); } }

        bool ITipDisplayer.AllowDisplayTip => true;


        #region Functional Test Support

        public DetectionPlotData CurrentData { get { return _detectionData; } }


        #endregion
    }
}
