using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using ZedGraph;
using pwiz.Skyline.Properties;
using Settings = pwiz.Skyline.Controls.Graphs.DetectionsGraphController.Settings;

namespace pwiz.Skyline.Controls.Graphs
{
    public abstract class DetectionsPlotPane : SummaryReplicateGraphPane, IDisposable, ITipDisplayer
    {
        protected class ToolTipImplementation : ITipProvider, IDisposable
        {

            private DetectionsPlotPane _parent;
            // ReSharper disable once RedundantDefaultMemberInitializer
            private bool _isVisible = false;
            private NodeTip _tip;
            private TableDesc _table;
            private readonly RenderTools _rt = new RenderTools();

            //Location is in user coordinates. 
            public int ReplicateIndex { get; private set; }

            public ToolTipImplementation(DetectionsPlotPane parent)
            {
                _parent = parent;
            }

            public ITipProvider TipProvider { get { return this; } }

            bool ITipProvider.HasTip => true;

            Size ITipProvider.RenderTip(Graphics g, Size sizeMax, bool draw)
            {
                var size = _table.CalcDimensions(g);
                if (draw)
                    _table.Draw(g);
                return new Size((int)size.Width + 2, (int)size.Height + 2);
            }

            public void AddLine(string description, string data)
            {
                if(_table == null) _table = new TableDesc();
                _table.AddDetailRow(description,data, _rt);
            }

            public void ClearData()
            {
                _table?.Clear();
            }

            public void Draw(int dataIndex, Point cursorPos)
            {
                if (_isVisible && ReplicateIndex != dataIndex) Hide();
                if (_isVisible && ReplicateIndex == dataIndex) return;
                if (_table == null || _table.Count == 0) return;

                ReplicateIndex = dataIndex;
                var basePoint = new UserPoint(dataIndex + 1, 
                    _parent.GetDataSeries()[ReplicateIndex] / _parent.YScale, _parent);

                using (var g = _parent.GraphSummary.GraphControl.CreateGraphics())
                {
                    var size = _table.CalcDimensions(g);
                    var offset = new Size(0, -(int)(size.Height + size.Height/_table.Count));
                    if (_tip == null)
                        _tip = new NodeTip(_parent);
                    _tip.SetTipProvider(TipProvider, new Rectangle(basePoint.Screen(offset), new Size()), cursorPos);
                }
                _isVisible = true;
            }

            public void Hide()
            {
                if (_isVisible)
                {
                    _tip?.HideTip();
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
        protected DetectionPlotData _detectionData = DetectionPlotData.INVALID;
        public int MaxRepCount { get; private set; }
        protected DetectionPlotData.DataSet TargetData => _detectionData.GetData(Settings.TargetType);
        protected float YScale {
            get
            {
                if (Settings.YScaleFactor == DetectionsGraphController.YScaleFactorType.PERCENT)
                    return (float)TargetData.MaxCount / 100;
                else
                    return Settings.YScaleFactor.Value;
            }
        } 


        protected ToolTipImplementation ToolTip { get; private set; }

        protected DetectionsPlotPane(GraphSummary graphSummary) : base(graphSummary)
        {
            MaxRepCount = graphSummary.DocumentUIContainer.DocumentUI.MeasuredResults.Chromatograms.Count;

            Settings.RepCount = MaxRepCount / 2;
            if (GraphSummary.Toolbar is DetectionsToolbar toolbar)
                toolbar.UpdateUI();

            if (GraphSummary.DocumentUIContainer.DocumentUI.Settings.HasResults)
            {
                _detectionData = DetectionPlotData.DataCache.Get(graphSummary.DocumentUIContainer.DocumentUI);
            }

            XAxis.Scale.Min = YAxis.Scale.Min = 0;
            XAxis.Scale.MinAuto = XAxis.Scale.MaxAuto = YAxis.Scale.MinAuto = YAxis.Scale.MaxAuto = false;
            ToolTip = new ToolTipImplementation(this);
        }

        public override bool HasToolbar { get { return true; } }

        public abstract ImmutableList<int> GetDataSeries();

        public override void OnClose(EventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            _detectionData.Dispose();
            ToolTip?.Dispose();
        }

        protected abstract void PopulateTooltip(int index);

        public override bool HandleMouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            using (var g = sender.CreateGraphics())
            {
                if (FindNearestObject(e.Location, g, out var nearestObject, out var index) && nearestObject is BarItem)
                {
                    PopulateTooltip(index);
                    ToolTip?.Draw(index, e.Location);

                    sender.Cursor = Cursors.Hand;
                    return true;
                }
                else
                {
                    ToolTip?.Hide();
                    sender.Cursor = Cursors.Cross;
                    return base.HandleMouseMoveEvent(sender, e);
                }
            }
        }

        protected abstract void HandleMouseClick(int index);

        public override void HandleMouseClick(object sender, MouseEventArgs e)
        {
            if (sender is Control ctx)
            {
                using (var g = ctx.CreateGraphics())
                {
                    if (FindNearestObject(e.Location, g, out var nearestObject, out var index) && nearestObject is BarItem)
                    {
                        HandleMouseClick(index);
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

        public void DataCallback(AreaCVGraphData data)
        {
            GraphSummary.GraphControl.BeginInvoke((Action)(() => { GraphSummary.UpdateUI(); }));
        }

        protected BarItem MakeBarItem(PointPairList points, Color color)
        {
            return new BarItem(null, points, color)
            {
                Bar =
                {
                    Fill = { Type = FillType.Solid }, Border = {InflateFactor = 0.7F}
                }
            };
        }

        protected virtual void AddLabels(Graphics g)
        {
            if (!_detectionData.IsValid)
                Title.Text = Resources.DetectionPlotPane_EmptyPlot_Label;
            else
                Title.Text = string.Empty;
        }


        protected override IdentityPath GetIdentityPath(CurveItem curveItem, int barIndex)
        {
            return null;
        }

        protected override void ChangeSelection(int selectedIndex, IdentityPath identityPath)
        {
        }

        protected override int SelectedIndex => GraphSummary.StateProvider.SelectedResultsIndex;

        Rectangle ITipDisplayer.RectToScreen(Rectangle r)
        {
            return GraphSummary.GraphControl.RectangleToScreen(r);
        }

        Rectangle ITipDisplayer.ScreenRect { get { return Screen.GetBounds(GraphSummary.GraphControl); } }

        bool ITipDisplayer.AllowDisplayTip => true;


    }

}
