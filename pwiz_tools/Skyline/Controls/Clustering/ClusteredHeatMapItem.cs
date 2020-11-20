using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
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
                    Color backColor;
                    if (zScore <= -4)
                    {
                        backColor = Color.FromArgb(255, 255, 0);
                    }
                    else if (zScore >= 4)
                    {
                        backColor = Color.FromArgb(255, 0, 255);
                    }
                    else
                    {
                        var blue = (int) ((zScore + 4) * 255 / 8);
                        backColor = Color.FromArgb(255, 255 - blue, blue);
                    }

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
