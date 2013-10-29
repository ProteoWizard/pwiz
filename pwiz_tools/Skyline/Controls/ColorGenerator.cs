/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Drawing;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// Generate random colors that are maximally distinguishable from each other,
    /// while conforming to other constraints (like not too dark, not too light, etc.)
    /// </summary>
    public class ColorGenerator
    {
        private const int ColorSectors = 5;     // easily distinguishable divisions of hue dimension
        private const int Trials = 200;         // how many trials to generate random color

        // Color constraints.
        private const double MinSaturation = 0.5;
        private const double MaxSaturation = 1.0;
        private const double MinValue = 0.6;
        private const double MaxValue = 1.0;

        private readonly List<HsvColor>[] _sectorColors;
        private readonly List<int> _colorCountPerProtein = new List<int>();
        private readonly Random _random;

        /// <summary>
        /// Generate random colors for peptides, aiming for maximum color variety within
        /// a particular protein.
        /// </summary>
        public ColorGenerator()
        {
            _sectorColors = new List<HsvColor>[ColorSectors];
            for (int i = 0; i < ColorSectors; i++)
                _sectorColors[i] = new List<HsvColor>();
            _random = new Random(397);  // fixed random sequence generates the same colors each time
        }

        /// <summary>
        /// Generate another color for the given protein index.  We try especially hard to make
        /// colors within a protein distinguishable, worrying less about differentiation between
        /// colors from different proteins.
        /// </summary>
        public Color GenerateColor(int protein)
        {
            // Increment number of colors within this protein.
            while (protein >= _colorCountPerProtein.Count)
                _colorCountPerProtein.Add(0);
            _colorCountPerProtein[protein]++;

            // Pick a random color within the next color sector.  The following statement starts
            // with a different color sector for each protein, so the first peptide within each
            // protein isn't always a shade of red, for example.
            int sector = (protein + _colorCountPerProtein[protein] - 1) % ColorSectors;
            var newColor = new HsvColor(_random);

            // If there are previous colors in this color sector, generate multiple random colors,
            // and choose the one that is most distinguishable from the existing colors.
            if (_sectorColors[sector].Count > 0)
            {
                var sectorColors = _sectorColors[sector];
                double maxHDistance = double.MinValue;
                double maxSvDistance = double.MinValue;
                for (int i = 0; i < Trials; i++)
                {
                    var testColor = new HsvColor(_random);
                    for (int j = 0; j < sectorColors.Count; j++)
                    {
                        // Try to maximize hue, saturation, and value differences.
                        double hDistance = sectorColors[j].HueDifference(testColor.H);
                        if (maxHDistance < hDistance)
                        {
                            maxHDistance = hDistance;
                            newColor.H = testColor.H;
                        }

                        double svDistance = sectorColors[j].SvDifference(testColor.S, testColor.V);
                        if (maxSvDistance < svDistance)
                        {
                            maxSvDistance = svDistance;
                            newColor.S = testColor.S;
                            newColor.V = testColor.V;
                        }
                    }
                }
            }

            _sectorColors[sector].Add(newColor);

            return newColor.GetColor(sector, ColorSectors, MinSaturation, MaxSaturation, MinValue, MaxValue);
        }
    }

    /// <summary>
    /// A color represented using Hue, Saturation, and Value components.
    /// </summary>
    public struct HsvColor
    {
        // Range [0.0..1.0) for H, S, V
        public float H;
        public float S;
        public float V;

        /// <summary>
        /// Generate a random color in HSV space.
        /// </summary>
        public HsvColor(Random random)
        {
            H = (float)random.NextDouble();
            S = (float)random.NextDouble();
            V = (float)random.NextDouble();
        }

        /// <summary>
        /// Generate an RGB color from this HSV color.  This will scale the color into a particular
        /// hue sector, and also scale S and V into desired ranges.
        /// </summary>
        public Color GetColor(
            int sector, int totalSectors,
            double minSaturation, double maxSaturation,
            double minValue, double maxValue)
        {
            double sectorSize = 1.0/totalSectors;
            double h = H*sectorSize + (double) sector/totalSectors;
            double s = minSaturation + S*(maxSaturation - minSaturation);
            double v = minValue + V*(maxValue - minValue);
            return ColorFromHsv(h*360, s, v);
        }

        /// <summary>
        /// Translate HSV values to RGB color.
        /// </summary>
        /// <param name="hue">Range [0.. 360)</param>
        /// <param name="saturation">Range [0..1)</param>
        /// <param name="value">Range [0..1)</param>
        private static Color ColorFromHsv(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            switch (hi)
            {
                case 0:
                    return Color.FromArgb(v, t, p);
                case 1:
                    return Color.FromArgb(q, v, p);
                case 2:
                    return Color.FromArgb(p, v, t);
                case 3:
                    return Color.FromArgb(p, q, v);
                case 4:
                    return Color.FromArgb(t, p, v);
                default:
                    return Color.FromArgb(v, p, q);
            }
        }

        /// <summary>
        /// Return the perceptual difference between this color's hue and
        /// another hue, values with range [0..1.0).
        /// </summary>
        public float HueDifference(float hue)
        {
            float difference = Math.Abs(H - hue);
            return (difference <= 0.5f) ? difference : 1.0f - difference;
        }

        /// <summary>
        /// Return the difference between this color and the given
        /// saturation and value, range [0..1.0)
        /// </summary>
        public float SvDifference(float s, float v)
        {
            var sDifference = S - s;
            var vDifference = V - v;
            return sDifference * sDifference + vDifference * vDifference;
        }

        public override string ToString()
        {
            return string.Format("HSV: {0:0.0},{1:0.0},{2:0.0}", H, S, V);
        }
    }
}
