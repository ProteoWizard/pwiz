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
        private ImmutableList<double> _xValues;
        private ImmutableList<double> _yValues;
        private ImmutableList<ImmutableList<Color>> _pointsByX;

        public ClusteredHeatMapItem(string label, IPointList points) : base(label, points, Color.Black, SymbolType.None)
        {
            var pointsLookup = ImmutableList.ValueOf(Enumerable.Range(0, Points.Count).Select(i => Points[i])
                .ToLookup(point => point.X)).OrderBy(group => group.Key).ToList();
            _xValues = ImmutableList.ValueOf(pointsLookup.Select(group=>group.Key));
            var allYValues = new HashSet<double>();
            foreach (var pt in pointsLookup.SelectMany(group => group))
            {
                allYValues.Add(pt.Y);
            }
            _yValues = ImmutableList.ValueOf(allYValues.OrderBy(y => y));
            var pointsByX = new List<ImmutableList<Color>>();
            foreach (var column in pointsLookup)
            {
                var colorDict = column.ToDictionary(pt => pt.Y, pt => (Color) pt.Tag);
                var colors = new List<Color>(_yValues.Count);
                foreach (var y in _yValues)
                {
                    Color color;
                    if (!colorDict.TryGetValue(y, out color))
                    {
                        color = Color.Transparent;
                    }
                    colors.Add(color);
                }
                pointsByX.Add(ImmutableList.ValueOf(colors));
            }
            _pointsByX = ImmutableList.ValueOf(pointsByX);
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
            List<Tuple<double, double>> intervals = null;
            for (int iColumn = 0; iColumn < _xValues.Count; iColumn++)
            {
                double x = _xValues[iColumn];
                double x1 = xScale.Transform(x - .5);
                double x2 = xScale.Transform(x + .5);
                if (Math.Max(x1, x2) < pane.Chart.Rect.Left || Math.Min(x1, x2) > pane.Chart.Rect.Right)
                {
                    continue;
                }

                if (intervals == null)
                {
                    intervals = new List<Tuple<double, double>>(_yValues.Count);
                    foreach (var y in _yValues)
                    {
                        double y1 = yScale.Transform(y - .5);
                        double y2 = yScale.Transform(y + .5);
                        intervals.Add(Tuple.Create(Math.Min(y1, y2), Math.Max(y1, y2)));
                    }
                }

                IEnumerable<int> rowIndexes = Enumerable.Range(0, intervals.Count);
                if (intervals[intervals.Count - 1].Item1 < intervals[0].Item1)
                {
                    rowIndexes = rowIndexes.Reverse();
                }

                var stripePainter = new StripePainter(g, (float)Math.Min(x1, x2), (float)Math.Abs(x2 - x1));
                var column = _pointsByX[iColumn];
                foreach (int iRow in rowIndexes)
                {
                    var color = column[iRow];
                    if (color.A == 0)
                    {
                        continue;
                    }
                    stripePainter.PaintStripe(intervals[iRow].Item1, intervals[iRow].Item2, color);
                }
                stripePainter.PaintLastStripe();
            }
        }
    }
}
