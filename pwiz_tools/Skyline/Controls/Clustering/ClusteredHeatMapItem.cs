using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ZedGraph;

namespace pwiz.Skyline.Controls.Clustering
{
    public class ClusteredHeatMapItem : LineItem
    {
        private List<IGrouping<double, PointPair>> _pointsByX;
        public ClusteredHeatMapItem(string label, IPointList points) : base(label, points, Color.Black, SymbolType.None)
        {
            _pointsByX = Enumerable.Range(0, Points.Count).Select(i => Points[i]).ToLookup(point => point.X)
                .OrderBy(grouping => grouping.Key).ToList();
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
                double x1 = xScale.Transform(column.Key - .5);
                double x2 = xScale.Transform(column.Key + .5);
                if (Math.Min(x1, x2) < pane.Chart.Rect.Left || Math.Max(x1, x2) > pane.Chart.Rect.Right)
                {
                    continue;
                }
                float xMin = (float) Math.Min(x1, x2);
                float xWidth = (float) Math.Abs(x2 - x1);

                var intervals = new List<Tuple<double, double, Color>>();
                foreach (var point in column)
                {
                    double y1 = yScale.Transform(point.Y - .5);
                    double y2 = yScale.Transform(point.Y + .5);
                    intervals.Add(Tuple.Create(y1, y2, (Color) point.Tag));
                }
                FillIntervals(g, xMin, xWidth, intervals);
            }
        }

        public static void FillIntervals(Graphics g, float xMin, float xWidth, IEnumerable<Tuple<double, double, Color>> intervals)
        {
            int? lastY = null;
            ColorAccumulator colorAccumulator = new ColorAccumulator();
            foreach (var interval in intervals.OrderBy(i => i.Item1))
            {
                int yStart = (int) Math.Floor(interval.Item1);
                //int yEnd = (int) Math.Ceiling(interval.Item2);
                lastY = lastY ?? yStart;
                if (yStart == lastY)
                {
                    double weight = Math.Min(yStart + 1, interval.Item2) - interval.Item1;
                    colorAccumulator.AddColor(interval.Item3, weight);
                }
                int yEnd = (int) Math.Floor(interval.Item2);
                if (yEnd != lastY.Value)
                {
                    var color = colorAccumulator.GetAverageColor();
                    g.FillRectangle(new SolidBrush(color), xMin, lastY.Value, xWidth, 1);
                    if (yEnd > lastY.Value + 1)
                    {
                        g.FillRectangle(new SolidBrush(interval.Item3), xMin, lastY.Value + 1, xWidth, yEnd - lastY.Value - 1);
                    }
                    colorAccumulator = new ColorAccumulator();
                    lastY = yEnd;
                    if (interval.Item2 > yEnd)
                    {
                        colorAccumulator.AddColor(interval.Item3, interval.Item2 - yEnd);
                    }
                }
            }

            if (lastY.HasValue)
            {
                g.FillRectangle(new SolidBrush(colorAccumulator.GetAverageColor()), xMin, lastY.Value, xWidth, 1);
            }
        }

        public class ColorAccumulator
        {
            private double _totalWeight;
            private double _totalR2;
            private double _totalG2;
            private double _totalB2;

            public void AddColor(Color color, double weight)
            {
                _totalWeight += weight;
                _totalR2 += color.R * color.R * weight;
                _totalG2 += color.G * color.G * weight;
                _totalB2 += color.B * color.B * weight;
            }

            public Color GetAverageColor()
            {
                if (_totalWeight == 0)
                {
                    return Color.Transparent;
                }

                var alpha = _totalWeight * 255;
                var r = Math.Sqrt(_totalR2 / _totalWeight);
                var g = Math.Sqrt(_totalG2 / _totalWeight);
                var b = Math.Sqrt(_totalB2 / _totalWeight);
                return Color.FromArgb(ToByte(alpha), ToByte(r), ToByte(g), ToByte(b));
            }

            private static byte ToByte(double value)
            {
                return (byte) Math.Min((int) value, 255);
            }
        }
    }
}
