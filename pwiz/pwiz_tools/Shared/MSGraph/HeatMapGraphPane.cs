/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using ZedGraph;

namespace pwiz.MSGraph
{
    /// <summary>
    /// A graph pane optimized for showing large heat maps using dynamically filtered data.
    /// </summary>
    public class HeatMapGraphPane : MSGraphPane
    {
        public bool ShowHeatMap { get; set; }
        public int MinDotRadius { get; set; }
        public int MaxDotRadius { get; set; }

        private HeatMapData _heatMapData;
        private float _yMin;
        private float _yMax;

        public override void SetScale(Graphics g)
        {
            if (!ShowHeatMap || _heatMapData == null)
            {
                base.SetScale(g);
                return;
            }

            CurveList.Clear();
            XAxis.Scale.SetupScaleData(this,  XAxis);
            YAxis.Scale.SetupScaleData(this,  YAxis);
            double cellWidth = Math.Abs(XAxis.Scale.ReverseTransform(MinDotRadius) - XAxis.Scale.ReverseTransform(0));
            double cellHeight = Math.Abs(YAxis.Scale.ReverseTransform(MinDotRadius) - YAxis.Scale.ReverseTransform(0));
            if (cellWidth <= 0 || double.IsNaN(cellWidth) || cellHeight <= 0 || double.IsNaN(cellHeight))
                return;

            // Use log scale for heat intensity.
            double scale = (_heatMapColors.Length - 1) / Math.Log(_heatMapData.MaxPoint.Z);

            // Create curves for each intensity color.
            var curves = new LineItem[_heatMapColors.Length];
            for (int i = 0; i < curves.Length; i++)
            {
                var color = _heatMapColors[i];
                curves[i] = new LineItem(string.Empty)
                {
                    Line = new Line { IsVisible = false },
                    Symbol = new Symbol
                    {
                        Border = new Border { IsVisible = false },
                        Size = (int)(MinDotRadius + i/(double)(curves.Length-1)*(MaxDotRadius-MinDotRadius)),
                        Fill = new Fill(color),
                        Type = SymbolType.Circle,
                        IsAntiAlias = true
                    }
                };
                if ((i + 1) % (_heatMapColors.Length / 4) == 0)
                {
                    double intensity = Math.Pow(Math.E, i / scale);
                    curves[i].Label.Text = intensity.ToString("F0"); // Not L10N
                }
                CurveList.Insert(0, curves[i]);
            }

            // Get points within bounds of graph/filter, with density appropriate for the current display resolution.
            var points = _heatMapData.GetPoints(
                XAxis.Scale.Min,
                XAxis.Scale.Max,
                Math.Max(YAxis.Scale.Min, _yMin),
                Math.Min(YAxis.Scale.Max, _yMax),
                cellWidth,
                cellHeight);

            foreach (var heatPoint in points)
            {
                // A log scale produces a better visual display.
                int intensity = (int)(Math.Log(heatPoint.Z) * scale);
                if (intensity >= 0)
                    curves[intensity].AddPoint(heatPoint.X, heatPoint.Y);
            }
        }

        public void SetPoints(HeatMapData heatMapData, double yMin, double yMax)
        {
            _heatMapData = heatMapData;
            _yMin = (float) yMin;
            _yMax = (float) Math.Min(yMax, float.MaxValue);
        }

        /// <summary>
        /// Fixed array of heat map colors.
        /// </summary>
        private static readonly Color[] _heatMapColors =
        {
            Color.FromArgb(0, 0, 255),
            Color.FromArgb(0, 1, 255),
            Color.FromArgb(0, 2, 255),
            Color.FromArgb(0, 4, 255),
            Color.FromArgb(0, 5, 255),
            Color.FromArgb(0, 7, 255),
            Color.FromArgb(0, 9, 255),
            Color.FromArgb(0, 11, 255),
            Color.FromArgb(0, 13, 255),
            Color.FromArgb(0, 15, 255),
            Color.FromArgb(0, 18, 253),
            Color.FromArgb(0, 21, 251),
            Color.FromArgb(0, 24, 250),
            Color.FromArgb(0, 27, 248),
            Color.FromArgb(0, 30, 245),
            Color.FromArgb(0, 34, 243),
            Color.FromArgb(0, 37, 240),
            Color.FromArgb(0, 41, 237),
            Color.FromArgb(0, 45, 234),
            Color.FromArgb(0, 49, 230),
            Color.FromArgb(0, 53, 226),
            Color.FromArgb(0, 57, 222),
            Color.FromArgb(0, 62, 218),
            Color.FromArgb(0, 67, 214),
            Color.FromArgb(0, 71, 209),
            Color.FromArgb(0, 76, 204),
            Color.FromArgb(0, 82, 199),
            Color.FromArgb(0, 87, 193),
            Color.FromArgb(0, 93, 188),
            Color.FromArgb(0, 98, 182),
            Color.FromArgb(0, 104, 175),
            Color.FromArgb(0, 110, 169),
            Color.FromArgb(0, 116, 162),
            Color.FromArgb(7, 123, 155),
            Color.FromArgb(21, 129, 148),
            Color.FromArgb(34, 136, 141),
            Color.FromArgb(47, 142, 133),
            Color.FromArgb(60, 149, 125),
            Color.FromArgb(71, 157, 117),
            Color.FromArgb(83, 164, 109),
            Color.FromArgb(93, 171, 100),
            Color.FromArgb(104, 179, 91),
            Color.FromArgb(113, 187, 92),
            Color.FromArgb(123, 195, 73),
            Color.FromArgb(132, 203, 63),
            Color.FromArgb(140, 211, 53),
            Color.FromArgb(148, 220, 43),
            Color.FromArgb(156, 228, 33),
            Color.FromArgb(163, 237, 22),
            Color.FromArgb(170, 246, 11),
            Color.FromArgb(176, 255, 0),
            Color.FromArgb(183, 248, 0),
            Color.FromArgb(188, 241, 0),
            Color.FromArgb(194, 234, 0),
            Color.FromArgb(199, 227, 0),
            Color.FromArgb(204, 220, 0),
            Color.FromArgb(209, 214, 0),
            Color.FromArgb(213, 207, 0),
            Color.FromArgb(217, 200, 0),
            Color.FromArgb(221, 194, 0),
            Color.FromArgb(224, 188, 0),
            Color.FromArgb(227, 181, 0),
            Color.FromArgb(230, 175, 0),
            Color.FromArgb(233, 169, 0),
            Color.FromArgb(236, 163, 0),
            Color.FromArgb(238, 157, 0),
            Color.FromArgb(240, 151, 0),
            Color.FromArgb(243, 145, 0),
            Color.FromArgb(244, 140, 0),
            Color.FromArgb(246, 134, 0),
            Color.FromArgb(248, 129, 0),
            Color.FromArgb(249, 123, 0),
            Color.FromArgb(250, 118, 0),
            Color.FromArgb(251, 112, 0),
            Color.FromArgb(252, 107, 0),
            Color.FromArgb(253, 102, 0),
            Color.FromArgb(254, 97, 0),
            Color.FromArgb(255, 92, 0),
            Color.FromArgb(255, 87, 0),
            Color.FromArgb(255, 82, 0),
            Color.FromArgb(255, 78, 0),
            Color.FromArgb(255, 73, 0),
            Color.FromArgb(255, 68, 0),
            Color.FromArgb(255, 64, 0),
            Color.FromArgb(255, 59, 0),
            Color.FromArgb(255, 55, 0),
            Color.FromArgb(255, 51, 0),
            Color.FromArgb(255, 47, 0),
            Color.FromArgb(255, 43, 0),
            Color.FromArgb(255, 39, 0),
            Color.FromArgb(255, 35, 0),
            Color.FromArgb(255, 31, 0),
            Color.FromArgb(255, 27, 0),
            Color.FromArgb(255, 23, 0),
            Color.FromArgb(255, 20, 0),
            Color.FromArgb(255, 16, 0),
            Color.FromArgb(255, 13, 0),
            Color.FromArgb(255, 10, 0),
            Color.FromArgb(255, 8, 0),
            Color.FromArgb(255, 3, 0)
        };
    }

    public class Point3D
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }

        public Point3D(double x, double y, double z)
        {
            X = (float)x;
            Y = (float)y;
            Z = (float)z;
        }
    }
}
