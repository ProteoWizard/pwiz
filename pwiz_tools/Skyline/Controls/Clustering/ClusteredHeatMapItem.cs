using System;
using System.Drawing;
using pwiz.Common.DataAnalysis.Clustering;
using ZedGraph;

namespace pwiz.Skyline.Controls.Clustering
{
    public class ClusteredHeatMapItem : LineItem
    {
        public ClusteredHeatMapItem(string label, IPointList points) : base(label, points, Color.Black, SymbolType.None)
        {
        }

        public override void Draw(Graphics g, GraphPane pane, int pos, float scaleFactor)
        {
            IPointList points = Points;

            if (points != null)
            {
                Scale xScale = GetXAxis(pane).Scale;
                Scale yScale = GetYAxis(pane).Scale;

                // Loop over each defined point							
                for (int i = 0; i < points.Count; i++)
                {
                    var point = points[i];
                    var zScore = point.Z;
                    if (zScore.Equals(PointPairBase.Missing))
                    {
                        continue;
                    }

                    Color backColor = ZScores.ZScoreToColor(zScore);
                    var brush = new SolidBrush(backColor);

                    double x1 = xScale.Transform(point.X - .5);
                    double x2 = xScale.Transform(point.X + .5);
                    double y1 = yScale.Transform(point.Y - .5);
                    double y2 = yScale.Transform(point.Y + .5);
                    g.FillRectangle(brush, (float) Math.Min(x1, x2), (float) Math.Min(y1, y2), (float) Math.Abs(x2 - x1), (float) Math.Abs((y2 - y1)));
                }
            }
        }

    }
}
