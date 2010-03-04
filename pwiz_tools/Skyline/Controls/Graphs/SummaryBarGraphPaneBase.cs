using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    internal abstract class SummaryBarGraphPaneBase : SummaryGraphPane
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

        protected SummaryBarGraphPaneBase(GraphSummary graphSummary)
            : base(graphSummary)
        {
        }

        public void Clear()
        {
            CurveList.Clear();
            GraphObjList.Clear();
        }

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

        public override void HandleResizeEvent()
        {
            ScaleAxisLabels();
        }

        protected void ScaleAxisLabels()
        {
            int dyAvailable = (int) Rect.Height/4;
            int countLabels = (XAxis.Scale.TextLabels != null ? XAxis.Scale.TextLabels.Length : 0);
            int dxAvailable = (int) Rect.Width/Math.Max(1, countLabels);
            int dpAvailable;
            if (dyAvailable > dxAvailable)
            {
                dpAvailable = dyAvailable;
                XAxis.Scale.FontSpec.Angle = 90;
                XAxis.Scale.Align = AlignP.Inside;
            }
            else
            {
                dpAvailable = dxAvailable;
                XAxis.Scale.FontSpec.Angle = 0;
                XAxis.Scale.Align = AlignP.Center;
            }
            var fontSpec = XAxis.Scale.FontSpec;
            int pointSize;
            for (pointSize = 12; pointSize > 4; pointSize--)
            {
                using (var font = new Font(fontSpec.Family, pointSize))
                {
                    var maxWidth = MaxWidth(font, XAxis.Scale.TextLabels);
                    if (maxWidth <= dpAvailable)
                    {
                        break;
                    }
                }
            }
            XAxis.Scale.FontSpec.Size = pointSize;
        }

        private static int MaxWidth(Font font, IEnumerable<String> labels)
        {
            var result = 0;
            if (labels != null)
            {
                foreach (var label in labels)
                    result = Math.Max(result, SystemMetrics.GetTextWidth(font, label));
            }
            return result;
        }
    }
}