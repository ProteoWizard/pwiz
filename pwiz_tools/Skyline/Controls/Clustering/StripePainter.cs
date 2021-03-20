using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Skyline.Controls.Clustering
{
    public class StripePainter
    {
        private double _totalWeight;
        private double _totalR2;
        private double _totalG2;
        private double _totalB2;
        private int? _yLast;


        public StripePainter(Graphics graphics, float x, float width)
        {
            Graphics = graphics;
            X = x;
            Width = width;
        }

        public void PaintStripe(double y1, double y2, Color color)
        {
            int yStart = (int)Math.Floor(y1);
            //int yEnd = (int) Math.Ceiling(interval.Item2);
            _yLast = _yLast ?? yStart;
            if (yStart == _yLast)
            {
                double weight = Math.Min(yStart + 1, y2) - y1;
                AddColor(color, weight);
            }
            int yEnd = (int)Math.Floor(y2);
            if (yEnd == _yLast.Value)
            {
                return;
            }
            PaintLastStripe();

            if (yEnd > yStart + 1)
            {
                Graphics.FillRectangle(new SolidBrush(color), X, yStart + 1, Width, yEnd - yStart - 1);
            }
            AddColor(color, y2 - yEnd);
        }

        private void AddColor(Color color, double weight)
        {
            _totalR2 += color.R * color.R * weight;
            _totalG2 += color.B * color.B * weight;
            _totalB2 += color.B * color.B * weight;
            _totalWeight += weight;
        }

        public void PaintLastStripe()
        {
            if (_yLast.HasValue)
            {
                if (_totalWeight > 0)
                {
                    Graphics.FillRectangle(new SolidBrush(GetAverageColor()), X, _yLast.Value, Width, _yLast.Value + 1);
                }
                _yLast = null;
                _totalWeight = 0;
                _totalR2 = 0;
                _totalG2 = 0;
                _totalB2 = 0;
            }
        }

        public Color GetAverageColor()
        {
            var alpha = _totalWeight * 255;
            var r = Math.Sqrt(_totalR2 / _totalWeight);
            var g = Math.Sqrt(_totalG2 / _totalWeight);
            var b = Math.Sqrt(_totalB2 / _totalWeight);
            return Color.FromArgb(ToByte(alpha), ToByte(r), ToByte(g), ToByte(b));
        }

        private static int ToByte(double value)
        {
            return (int) Math.Min(value, 255);
        }

        public Graphics Graphics { get; private set; }
        public float X { get; private set; }
        public float Width { get; private set; } }
}
