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
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using ZedGraph;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using Settings = pwiz.Skyline.Controls.Graphs.DetectionsGraphController.Settings;

namespace pwiz.Skyline.Controls.Graphs
{
    public abstract class DetectionsPlotPane : SummaryReplicateGraphPane, IDisposable, ITipDisplayer
    {
        public class ToolTipImplementation : ITipProvider, IDisposable
        {

            private DetectionsPlotPane _parent;
            // ReSharper disable once RedundantDefaultMemberInitializer
            private bool _isVisible = false;
            private NodeTip _tip;
            private TableDesc _table;
            private readonly RenderTools _rt = new RenderTools();

            //Location is in user coordinates. 
            public int ReplicateIndex { get; private set; }
            public bool IsVisible => _isVisible;

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

            //used for testing only
            public List<string> TipLines
            {
                get
                {
                    return _table.Select((rowDesc) =>
                        string.Join(TextUtil.SEPARATOR_TSV_STR, rowDesc.Select(cell => cell.Text))
                    ).ToList();
                }
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

        protected class ProgressBarImplementation : IDisposable
        {
            readonly LineObj _left = new LineObj()
            {
                IsClippedToChartRect = true,
                Line = new Line() { Width = 4, Color = Color.Green, Style = DashStyle.Solid },
                Location = new Location(0,0, CoordType.PaneFraction)
            };
            readonly LineObj _right = new LineObj()
            {
                IsClippedToChartRect = true,
                Line = new Line() { Width = 4, Color = Color.LightGreen, Style = DashStyle.Solid },
                Location = new Location(0, 0, CoordType.PaneFraction)
            };
            private SizeF _titleSize;
            private PointF _barLocation;
            private readonly DetectionsPlotPane _parent;

            public ProgressBarImplementation(DetectionsPlotPane parent)
            {
                _parent = parent;
                var scaleFactor = parent.CalcScaleFactor();
                using (var g = parent.GraphSummary.CreateGraphics())
                {
                    _titleSize = parent.Title.FontSpec.BoundingBox(g, parent.Title.Text, scaleFactor);
                }
                _barLocation = new PointF(
                    (parent.Rect.Left + parent.Rect.Right - _titleSize.Width) / (2 * parent.Rect.Width),
                    (parent.Rect.Top + parent.Margin.Top * (1+scaleFactor) + _titleSize.Height)/ parent.Rect.Height);

                _left.Location.X = _barLocation.X;
                _left.Location.Y = _barLocation.Y;
                _left.Location.Width = 0;
                _left.Location.Height = 0;
                _right.Location.X = _barLocation.X;
                _right.Location.Y = _barLocation.Y;
                _right.Location.Width = _titleSize.Width/ parent.Rect.Width;
                _right.Location.Height = 0;
                parent.GraphObjList.Add(_left);
                parent.GraphObjList.Add(_right);
            }

            public void Dispose()
            {
                _parent.GraphObjList.Remove(_left);
                _parent.GraphObjList.Remove(_right);
            }

            public void UpdateBar(int progress)
            {
                if(_parent.GraphObjList.FirstOrDefault((obj) => ReferenceEquals(obj, _left)) == null)
                    _parent.GraphObjList.Add(_left);
                if (_parent.GraphObjList.FirstOrDefault((obj) => ReferenceEquals(obj, _right)) == null)
                    _parent.GraphObjList.Add(_right);

                var len1 = _titleSize.Width * progress / 100 / _parent.Rect.Width;

                _left.Location.X = _barLocation.X;
                _left.Location.Y = _barLocation.Y;
                _left.Location.Width = len1;
                _left.Location.Height = 0;
                _right.Location.X = _barLocation.X + len1;
                _right.Location.Y = _barLocation.Y;
                _right.Location.Width = _titleSize.Width/ _parent.Rect.Width - len1;
                _right.Location.Height = 0;

                
                _parent.GraphSummary.GraphControl.Invalidate();
                _parent.GraphSummary.GraphControl.Update();
            }
        }

        protected DetectionPlotData _detectionData = DetectionPlotData.INVALID;
        public int MaxRepCount { get; private set; }
        protected DetectionPlotData.DataSet TargetData => _detectionData.GetTargetData(Settings.TargetType);
        protected float YScale {
            get
            {
                if (Settings.YScaleFactor == DetectionsGraphController.YScaleFactorType.PERCENT)
                    return (float)TargetData.MaxCount / 100;
                else
                    return Settings.YScaleFactor.Value;
            }
        } 


        public ToolTipImplementation ToolTip { get; private set; }
        protected ProgressBarImplementation ProgressBar { get; private set; }

        protected DetectionsPlotPane(GraphSummary graphSummary) : base(graphSummary)
        {
            MaxRepCount = graphSummary.DocumentUIContainer.DocumentUI.MeasuredResults.Chromatograms.Count;

            Settings.RepCount = MaxRepCount / 2;
            if (GraphSummary.Toolbar is DetectionsToolbar toolbar)
                toolbar.UpdateUI();

            XAxis.Scale.Min = YAxis.Scale.Min = 0;
            XAxis.Scale.MinAuto = XAxis.Scale.MaxAuto = YAxis.Scale.MinAuto = YAxis.Scale.MaxAuto = false;
            ToolTip = new ToolTipImplementation(this);

            DetectionPlotData.GetDataCache().ReportProgress += UpdateProgressHandler;
            DetectionPlotData.GetDataCache().StatusChange += UpdateStatusHandler;
        }

        public void UpdateProgressHandler(int progress)
        {
            if(GraphSummary.GraphControl.IsHandleCreated)
                GraphSummary.GraphControl.Invoke((Action) (() => { ProgressBar?.UpdateBar(progress); }));
        }

        public void UpdateStatusHandler(DetectionPlotData.DetectionDataCache.CacheStatus status, string message)
        {
            if (GraphSummary.GraphControl.IsHandleCreated)
                GraphSummary.GraphControl.Invoke((Action) (() =>
                {
                    AddLabels(status, message);
                    if (status == DetectionPlotData.DetectionDataCache.CacheStatus.processing)
                        ProgressBar = new ProgressBarImplementation(this);
                    else
                    {
                        ProgressBar?.Dispose();
                        ProgressBar = null;
                    }

                    GraphSummary.GraphControl.Invalidate();
                    GraphSummary.GraphControl.Update();
                    GraphSummary.Toolbar.UpdateUI();
                }));
        }

        public override bool HasToolbar { get { return true; } }

        public abstract ImmutableList<int> GetDataSeries();

        public override void OnClose(EventArgs e)
        {
            DetectionPlotData.GetDataCache().ReportProgress -= UpdateProgressHandler;
            DetectionPlotData.GetDataCache().StatusChange -= UpdateStatusHandler;
            Dispose();
        }

        public void Dispose()
        {
            ToolTip?.Dispose();
        }

        public abstract void PopulateTooltip(int index);

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

            base.Draw(g);
        }

        public void DataCallback(DetectionPlotData data)
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

        protected virtual void AddLabels()
        {
            AddLabels(DetectionPlotData.GetDataCache().Status, "");
        }

        protected virtual void AddLabels(DetectionPlotData.DetectionDataCache.CacheStatus status, string message)
        {
            if (!_detectionData.IsValid)
            {
                GraphObjList.Clear();
                switch (status)
                {
                    case DetectionPlotData.DetectionDataCache.CacheStatus.processing:
                        Title.Text = Resources.DetectionPlotPane_WaitingForData_Label;
                        break;
                    case DetectionPlotData.DetectionDataCache.CacheStatus.idle:
                        Title.Text = Resources.DetectionPlotPane_EmptyPlot_Label;
                        break;
                    case DetectionPlotData.DetectionDataCache.CacheStatus.canceled:
                        Title.Text = Resources.DetectionPlotPane_EmptyPlotCanceled_Label;
                        break;
                    case DetectionPlotData.DetectionDataCache.CacheStatus.error:
                        Title.Text = Resources.DetectionPlotPane_EmptyPlotError_Label;
                        break;
                }
                var scaleFactor = CalcScaleFactor();
                SizeF titleSize;
                using (var g = GraphSummary.CreateGraphics())
                {
                    titleSize = Title.FontSpec.BoundingBox(g, Title.Text, scaleFactor);
                }
                var subtitleLocation = new PointF(
                    (Rect.Left + Rect.Right) / (2 * Rect.Width),
                    (Rect.Top + Margin.Top * (1 + scaleFactor) + 2*titleSize.Height) / Rect.Height);

                var subtitle = new TextObj(message, subtitleLocation.X, subtitleLocation.Y,
                    CoordType.PaneFraction, AlignH.Center, AlignV.Center)
                {
                    IsClippedToChartRect = true,
                    ZOrder = ZOrder.E_BehindCurves,
                    FontSpec = GraphSummary.CreateFontSpec(Color.Black),
                };
                subtitle.FontSpec.Size = Title.FontSpec.Size * 0.75f;
                GraphObjList.Add(subtitle);
            }
            else
            {
                Title.Text = string.Empty;
                YAxis.Title.Text = string.Format(CultureInfo.CurrentCulture, 
                    Resources.DetectionPlotPane_YAxis_Name, 
                    string.Format(CultureInfo.CurrentCulture, Settings.YScaleFactor.Label, Settings.TargetType.Label) );
            }
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
            if(!GraphSummary.GraphControl.IsDisposed)
                return GraphSummary.GraphControl.RectangleToScreen(r);
            else return new Rectangle();
        }

        Rectangle ITipDisplayer.ScreenRect
        {
            get
            {
                if (!GraphSummary.GraphControl.IsDisposed)
                    return Screen.GetBounds(GraphSummary.GraphControl);
                else return new Rectangle();
            }
        }

        bool ITipDisplayer.AllowDisplayTip => true;

        #region Functional Test Support

        public DetectionPlotData CurrentData { get { return _detectionData; } }


        #endregion

    }

}
