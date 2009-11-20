using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    internal abstract class SummaryBarGraphPaneBase : SummaryGraphPane
    {
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

        /// <summary>
        /// This works around a issue in Zedgraph's ValueHandler.BarCenterValue
        /// (it incorrectly assumes that HiLowBarItems do place themselves 
        /// next to each other like all other BarItems).
        /// </summary>
        /// <param name="curve">The BarItem</param>
        /// <param name="barWidth">The width of the bar</param>
        /// <param name="iCluster">The index of the point in CurveItem.Points</param>
        /// <param name="val">The x-value of the point.</param>
        /// <param name="iOrdinal">The index of the BarItem in the CurveList</param>
        private double BarCenterValue(CurveItem curve, float barWidth, int iCluster,
                                      double val, int iOrdinal)
        {
            float clusterWidth = BarSettings.GetClusterWidth();
            float clusterGap = BarSettings.MinClusterGap * barWidth;
            float barGap = barWidth * BarSettings.MinBarGap;

            if (curve.IsBar && BarSettings.Type != BarType.Cluster)
                iOrdinal = 0;

            float centerPix = XAxis.Scale.Transform(curve.IsOverrideOrdinal, iCluster, val)
                              - clusterWidth / 2.0F + clusterGap / 2.0F +
                              iOrdinal * (barWidth + barGap) + 0.5F * barWidth;
            return XAxis.Scale.ReverseTransform(centerPix);
        }

        /// <summary>
        /// Works around a issue in ValueHandler.BarCenterValue
        /// </summary>
        private bool FindNearestBar(PointF point, out CurveItem nearestCurve, out int iNearest)
        {
            double x, y;
            ReverseTransform(point, out x, out y);
            PointF pointCenter = new PointF(XAxis.Scale.Transform(Math.Round(x)), point.Y);
            if (!FindNearestPoint(pointCenter, out nearestCurve, out iNearest))
            {
                return false;
            }
            double minDist = double.MaxValue;
            for (int iCurve = 0; iCurve < CurveList.Count; iCurve ++)
            {
                CurveItem curve = CurveList[iCurve];
                double barCenter = BarCenterValue(curve, curve.GetBarWidth(this), iNearest, Math.Round(x), iCurve);
                double dist = Math.Abs(barCenter - x);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestCurve = curve;
                }
            }
            return true;
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
                return false;

            CurveItem nearestCurve;
            int iNearest;
            if (!FindNearestBar(new PointF(mouseEventArgs.X, mouseEventArgs.Y), out nearestCurve, out iNearest))
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
            if (!FindNearestBar(new PointF(mouseEventArgs.X, mouseEventArgs.Y), out nearestCurve, out iNearest))
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