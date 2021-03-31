/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Drawing;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Clustering
{
    /// <summary>
    /// Draws a series of non-overlapping rectangles along a vertical stripe.
    /// If two rectangles share the same pixel, then the colors of those two rectangles are combined
    /// by taking the square root of the weighted average of the square of the RGB components.
    /// </summary>
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
            Assume.IsTrue(y1 <= y2);
            int yStart = (int)Math.Floor(y1);
            _yLast = _yLast ?? yStart;
            Assume.IsTrue(_yLast <= y1);
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
            _yLast = yEnd;
        }

        private void AddColor(Color color, double weight)
        {
            _totalR2 += color.R * color.R * weight;
            _totalG2 += color.G * color.G * weight;
            _totalB2 += color.B * color.B * weight;
            _totalWeight += weight;
        }

        public void PaintLastStripe()
        {
            if (_yLast.HasValue)
            {
                if (_totalWeight > 0)
                {
                    Graphics.FillRectangle(new SolidBrush(GetAverageColor()), X, _yLast.Value, Width, 1);
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

        public Graphics Graphics { get; }
        public float X { get; }
        public float Width { get; }
    }
}
