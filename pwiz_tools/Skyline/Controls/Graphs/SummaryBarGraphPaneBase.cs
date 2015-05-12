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
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public abstract class SummaryBarGraphPaneBase : SummaryGraphPane
    {
        protected static bool ShowSelection
        {
            get
            {
                return Settings.Default.ShowReplicateSelection;
            }
        }

        protected static readonly Color[] COLORS_TRANSITION = GraphChromatogram.COLORS_LIBRARY;
        protected static readonly Color[] COLORS_GROUPS = GraphChromatogram.COLORS_GROUPS;

        protected static int GetColorIndex(TransitionGroupDocNode nodeGroup, int countLabelTypes, ref int? charge, ref int iCharge)
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
                return false;
            }
            IdentityPath identityPath = GetIdentityPath(nearestCurve, iNearest);
            if (identityPath == null)
            {
                return false;
            }
            GraphSummary.Cursor = Cursors.Hand;
            return true;
        }

        public override bool HandleMouseDownEvent(ZedGraphControl sender, MouseEventArgs mouseEventArgs)
        {
            CurveItem nearestCurve;
            int iNearest;
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
    }
}