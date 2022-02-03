/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public abstract class SummaryBarGraphPaneBase : SummaryGraphPane
    {
        public class ToolTipImplementation : ITipProvider
        {
            public class TargetCurveList : List<CurveItem>
            {
                private SummaryBarGraphPaneBase _parent;

                public TargetCurveList(SummaryBarGraphPaneBase parent)
                {
                    _parent = parent;
                }
                public new void Add(CurveItem curve)
                {
                    //all targets must be on the same axis
                    if (Count > 0)
                        Assume.AreEqual(base[0].GetYAxis(_parent), curve.GetYAxis(_parent), @"All target curves for a tooltip must be on the same axis.");
                    base.Add(curve);
                }

                public CurveItem ClearAndAdd(CurveItem curve)
                {
                    Clear();
                    Add(curve);
                    return curve;
                }

                public Axis GetYAxis()
                {
                    if (Count == 0)
                        return null;
                    return base[0].GetYAxis(_parent);
                }

                public bool IsTarget(CurveItem curve)
                {
                    return this.Any(c => ReferenceEquals(c, curve));
                }
            }

            private SummaryBarGraphPaneBase _parent;
            private bool _isVisible;
            private NodeTip _tip;
            private TableDesc _table;
            internal RenderTools RenderTools = new RenderTools();

            public int ReplicateIndex { get; private set; }
            public TargetCurveList TargetCurves {  get; private set; }

            public ToolTipImplementation(SummaryBarGraphPaneBase parent)
            {
                _parent = parent;
                TargetCurves = new TargetCurveList(parent);
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
                if (_table == null)
                    _table = new TableDesc();
                _table.AddDetailRow(description, data, RenderTools);
            }

            public void ClearData()
            {
                _table?.Clear();
            }

            public void Draw(int dataIndex, Point cursorPos)
            {
                if (_isVisible)
                {
                    if (ReplicateIndex == dataIndex)
                        return;
                    Hide();
                }
                if (_table == null || _table.Count == 0 || !TargetCurves.Any()) return;

                ReplicateIndex = dataIndex;
                var basePoint = new UserPoint(dataIndex + 1,
                    _parent.GetToolTipDataSeries()[ReplicateIndex] / _parent.YScale, _parent, TargetCurves.GetYAxis() ?? _parent.YAxis);

                using (var g = _parent.GraphSummary.GraphControl.CreateGraphics())
                {
                    var size = _table.CalcDimensions(g);
                    var offset = new Size(0, -(int)(size.Height + size.Height / _table.Count));
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

            #region Test Methods
            public List<string> TipLines
            {
                get
                {
                    return _table.Select((rowDesc) =>
                        string.Join(TextUtil.SEPARATOR_TSV_STR, rowDesc.Select(cell => cell.Text))
                    ).ToList();
                }
            }

            #endregion
            private class UserPoint
            {
                private GraphPane _graph;
                private Axis _yAxis;
                public int X { get; private set; }
                public float Y { get; private set; }

                public UserPoint(int x, float y, GraphPane graph)
                {
                    X = x;
                    Y = y;
                    _graph = graph;
                    _yAxis = graph.YAxis;
                }
                public UserPoint(int x, float y, GraphPane graph, Axis yAxis) : this(x, y, graph)
                {
                    if(yAxis is Y2Axis)
                        _yAxis = yAxis;
                }

                public PointF User()
                {
                    return new PointF(X, Y);
                }

                public Point Screen()
                {
                    return new Point(
                        (int)_graph.XAxis.Scale.Transform(X),
                        (int)_yAxis.Scale.Transform(Y));
                }
                public Point Screen(Size OffsetScreen)
                {
                    return new Point(
                        (int)(_graph.XAxis.Scale.Transform(X) + OffsetScreen.Width),
                        (int)(_yAxis.Scale.Transform(Y) + OffsetScreen.Height));
                }

                public PointF PF()
                {
                    return new PointF(
                        _graph.XAxis.Scale.Transform(X) / _graph.Rect.Width,
                        _yAxis.Scale.Transform(Y) / _graph.Rect.Height);
                }
                public PointD PF(SizeF OffsetPF)
                {
                    return new PointD(
                        _graph.XAxis.Scale.Transform(X) / _graph.Rect.Width + OffsetPF.Width,
                        _yAxis.Scale.Transform(Y) / _graph.Rect.Height + OffsetPF.Height);
                }
            }
        }

        public virtual void PopulateTooltip(int index){}

        /// <summary>
        /// Override if you need to implement tooltips in your graph.
        /// </summary>
        /// <returns>A list of y-coordinates where tooltips should be displayed.
        /// List index is the replicate index.</returns>
        public virtual ImmutableList<float> GetToolTipDataSeries()
        {
            //This provides a clear error message if this method is invoked by mistake in a class that doesn't implement tooltips.
            throw new NotImplementedException(@"Method GetToolTipDataSeries is not implemented.");
        }
        /// <summary>
        /// Additional scaling factor for tooltip's vertical position.
        /// </summary>
        public virtual float YScale
        {
            get { return 1.0f; }
        }
        /// <summary>
        /// Create a new tooltip instance in the child class constructor if you
        /// want to show thw tooltips.
        /// </summary>
        public ToolTipImplementation ToolTip { get; protected set; }

        protected static bool ShowSelection
        {
            get
            {
                return Settings.Default.ShowReplicateSelection;
            }
        }

        protected static IList<Color> COLORS_TRANSITION {get { return GraphChromatogram.COLORS_LIBRARY; }}
        protected static IList<Color> COLORS_GROUPS {get { return GraphChromatogram.COLORS_GROUPS; }}

        protected static int GetColorIndex(TransitionGroupDocNode nodeGroup, int countLabelTypes, ref Adduct charge, ref int iCharge)
        {
            return GraphChromatogram.GetColorIndex(nodeGroup, countLabelTypes, ref charge, ref iCharge);
        }

        protected SummaryBarGraphPaneBase(GraphSummary graphSummary)
            : base(graphSummary)
        {
            _axisLabelScaler = new AxisLabelScaler(this);
        }

        public void Clear()
        {
            CurveList.Clear();
            GraphObjList.Clear();
            ToolTip?.Hide();
        }

        protected virtual int FirstDataIndex { get { return 0; } }

        protected abstract int SelectedIndex { get; }

        protected abstract IdentityPath GetIdentityPath(CurveItem curveItem, int barIndex);

        public override bool HandleKeyDownEvent(object sender, KeyEventArgs keyEventArgs)
        {
            switch (keyEventArgs.KeyCode)
            {
                case Keys.Left:
                case Keys.Up:
                    ChangeSelection(SelectedIndex - 1, null);
                    return true;
                case Keys.Right:
                case Keys.Down:
                    ChangeSelection(SelectedIndex + 1, null);
                    return true;
            }
            return false;
        }

        protected abstract void ChangeSelection(int selectedIndex, IdentityPath identityPath);

        public override bool HandleMouseMoveEvent(ZedGraphControl sender, MouseEventArgs mouseEventArgs)
        {
            if (mouseEventArgs.Button != MouseButtons.None)
                return base.HandleMouseMoveEvent(sender, mouseEventArgs);

            CurveItem nearestCurve;
            int iNearest;
            if (!FindNearestPoint(new PointF(mouseEventArgs.X, mouseEventArgs.Y), out nearestCurve, out iNearest))
            {
                ToolTip?.Hide();
                var axis = GetNearestXAxis(sender, mouseEventArgs);
                if (axis != null)
                {
                    GraphSummary.Cursor = Cursors.Hand;
                    return true;
                }
                return false;
            }

            if (ToolTip != null && ToolTip.TargetCurves.IsTarget(nearestCurve))
            {
                PopulateTooltip(iNearest);
                ToolTip.Draw(iNearest, mouseEventArgs.Location);
                sender.Cursor = Cursors.Hand;
                return true;
            }
            else
                ToolTip?.Hide();

            IdentityPath identityPath = GetIdentityPath(nearestCurve, iNearest);
            if (identityPath == null)
            {
                return false;
            }
            GraphSummary.Cursor = Cursors.Hand;
            return true;
        }

        public override void HandleMouseOutEvent(object sender, EventArgs e)
        {
            ToolTip?.Hide();
        }

        private XAxis GetNearestXAxis(ZedGraphControl sender, MouseEventArgs mouseEventArgs)
        {
            using (Graphics g = sender.CreateGraphics())
            {
                object nearestObject;
                int index;
                if (FindNearestObject(new PointF(mouseEventArgs.X, mouseEventArgs.Y), g, out nearestObject, out index))
                {
                    var axis = nearestObject as XAxis;
                    if (axis != null)
                        return axis;
                }
            }

            return null;
        }

        public override bool HandleMouseDownEvent(ZedGraphControl sender, MouseEventArgs mouseEventArgs)
        {
            CurveItem nearestCurve;
            int iNearest;
            var axis = GetNearestXAxis(sender, mouseEventArgs);
            if (axis != null)
            {
                iNearest = (int)axis.Scale.ReverseTransform(mouseEventArgs.X - axis.MajorTic.Size);
                if (iNearest < 0)
                    return false;
                ChangeSelection(iNearest, GraphSummary.StateProvider.SelectedPath);
                return true;
            }
            if (!FindNearestPoint(new PointF(mouseEventArgs.X, mouseEventArgs.Y), out nearestCurve, out iNearest))
            {
                return false;
            }
            IdentityPath identityPath = GetIdentityPath(nearestCurve, iNearest);
            if (identityPath == null)
            {
                return false;
            }

            ChangeSelection(iNearest, identityPath);
            return true;
        }

        public override void OnClose(EventArgs e)
        {
            if (ToolTip != null)
            {
                ToolTip.Hide();
                ToolTip.RenderTools.Dispose();
            }
        }

        public override void Draw(Graphics g)
        {
            _chartBottom = Chart.Rect.Bottom;
            HandleResizeEvent();
            base.Draw(g);
            if (IsRedrawRequired(g))
                base.Draw(g);

        }

        public override void HandleResizeEvent()
        {
            ScaleAxisLabels();
        }

        protected virtual bool IsRedrawRequired(Graphics g)
        {
            // Have to call HandleResizeEvent twice, since the X-scale may not be up
            // to date before calling Draw.  If nothing changes, this will be a no-op
            HandleResizeEvent();
            return (Chart.Rect.Bottom != _chartBottom);
        }

        private float _chartBottom;
        protected AxisLabelScaler _axisLabelScaler;

        protected string[] OriginalXAxisLabels
        {
            get { return _axisLabelScaler.OriginalTextLabels ?? XAxis.Scale.TextLabels; }
            set { _axisLabelScaler.OriginalTextLabels = value; }
        }

        protected void ScaleAxisLabels()
        {
            _axisLabelScaler.FirstDataIndex = FirstDataIndex;
            _axisLabelScaler.ScaleAxisLabels();
        }

        protected bool IsRepeatRemovalAllowed
        {
            get { return _axisLabelScaler.IsRepeatRemovalAllowed; }
            set { _axisLabelScaler.IsRepeatRemovalAllowed = value; }
        }

        #region Test Support Methods
        public string[] GetOriginalXAxisLabels()
        {
            return _axisLabelScaler.OriginalTextLabels ?? XAxis.Scale.TextLabels;
        }

        #endregion

    }
}