using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using pwiz.Common.Collections;
using ZedGraph;

namespace pwiz.Skyline.Controls.Clustering
{
    public class ClusteredHeatMapItem : LineItem
    {
        private ImmutableList<ImmutableList<PointPair>> _pointsByX;
        public ClusteredHeatMapItem(string label, IPointList points) : base(label, points, Color.Black, SymbolType.None)
        {
            _pointsByX = ImmutableList.ValueOf(Enumerable.Range(0, Points.Count).Select(i => Points[i])
                .ToLookup(point => point.X)
                .OrderBy(grouping => grouping.Key)
                .Select(grouping => ImmutableList.ValueOf(grouping.OrderBy(point => point.Y))));
        }

        public override void Draw(Graphics g, GraphPane pane, int pos, float scaleFactor)
        {
            IPointList points = Points;
            if (points == null)
            {
                return;
            }

            Scale xScale = GetXAxis(pane).Scale;
            Scale yScale = GetYAxis(pane).Scale;
            foreach (var column in _pointsByX)
            {
                double x = column[0].X;
                double x1 = xScale.Transform(x - .5);
                double x2 = xScale.Transform(x + .5);
                if (Math.Max(x1, x2) < pane.Chart.Rect.Left || Math.Min(x1, x2) > pane.Chart.Rect.Right)
                {
                    continue;
                }
                var intervals = new List<Tuple<double, double, Color>>(column.Count);
                foreach (var point in column)
                {
                    double y1 = yScale.Transform(point.Y - .5);
                    double y2 = yScale.Transform(point.Y + .5);
                    intervals.Add(Tuple.Create(Math.Min(y1, y2), Math.Max(y1, y2), (Color) point.Tag));
                }

                if (intervals[intervals.Count - 1].Item1 < intervals[0].Item1)
                {
                    intervals.Reverse();
                }
                var stripePainter = new StripePainter(g, (float)Math.Min(x1, x2), (float)Math.Abs(x2 - x1));
                foreach (var interval in intervals)
                {
                    stripePainter.PaintStripe(interval.Item1, interval.Item2, interval.Item3);
                }
            }
        }
    }
}
