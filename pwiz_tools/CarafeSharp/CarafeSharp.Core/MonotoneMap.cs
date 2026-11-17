/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Linq;

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// A monotone piecewise-linear map: knots whose x increase and whose y never decrease, as Chronologer's KDE ridge
    /// alignment (<see cref="KdeRidgeAlignment"/>) fits them, from a run's minutes to the hydrophobic index or back.
    /// </summary>
    public sealed class MonotoneMap
    {
        /// <summary>The fraction of the knots at each end whose least-squares slope extends the map past that end.</summary>
        public const double END_FRACTION = 0.2;

        /// <summary>The points a median of maps is taken at, by default.</summary>
        public const int MEDIAN_POINTS = 1000;

        /// <summary>
        /// The pointwise median of <paramref name="maps"/>, each extended past its knots (<see cref="Extend"/>), at
        /// <paramref name="points"/> x values evenly spread from the lowest first knot to the highest last one. A median
        /// of maps that never decrease never decreases, so the result is a map of the same kind; it is the middle of the
        /// maps at every x, which no single one of them need be.
        /// </summary>
        public static MonotoneMap PointwiseMedian(IReadOnlyList<MonotoneMap> maps, int points = MEDIAN_POINTS)
        {
            if (maps.Count == 0)
                throw new ArgumentException(@"No maps to take the median of.");
            if (maps.Count == 1)
                return maps[0];
            double from = maps.Min(m => m._x[0]), to = maps.Max(m => m._x[m._x.Length - 1]);
            var x = new double[points];
            var y = new double[points];
            var values = new double[maps.Count];
            for (int k = 0; k < points; k++)
            {
                x[k] = from + (to - from) * k / (points - 1);
                for (int m = 0; m < maps.Count; m++)
                    values[m] = maps[m].Extend(x[k]);
                y[k] = Statistics.Median(values);
                // Each map's ends extend with their own slopes, which can cross; the median stays monotone in exact
                // arithmetic, and this keeps it so in floating point.
                if (k > 0 && y[k] < y[k - 1])
                    y[k] = y[k - 1];
            }
            return new MonotoneMap(x, y);
        }

        private readonly double[] _x;
        private readonly double[] _y;

        public MonotoneMap(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            if (x.Count != y.Count || x.Count < 2)
                throw new ArgumentException(string.Format(@"A map needs at least two knots, each with x and y ({0} x, {1} y).", x.Count, y.Count));
            for (int k = 1; k < x.Count; k++)
            {
                if (!(x[k] > x[k - 1]) || !(y[k] >= y[k - 1]))
                    throw new ArgumentException(string.Format(@"Knot {0} ({1}, {2}) does not follow ({3}, {4}) monotonically.", k, x[k], y[k], x[k - 1], y[k - 1]));
            }
            _x = x.ToArray();
            _y = y.ToArray();
        }

        public IReadOnlyList<double> X
        {
            get { return _x; }
        }

        public IReadOnlyList<double> Y
        {
            get { return _y; }
        }

        /// <summary>
        /// The map at <paramref name="x"/> clipped to the knots' range, as Chronologer applies an alignment
        /// (<c>np.clip</c>, then its linear spline).
        /// </summary>
        public double Map(double x)
        {
            return Interpolate(Math.Min(Math.Max(x, _x[0]), _x[_x.Length - 1]));
        }

        /// <summary>
        /// The map at <paramref name="x"/>, continued past the knots by a line with the least-squares slope of the
        /// outer <see cref="END_FRACTION"/> of the knots, so it neither stops nor flattens beyond the data.
        /// </summary>
        public double Extend(double x)
        {
            if (x < _x[0])
                return _y[0] + EndSlope(false) * (x - _x[0]);
            if (x > _x[_x.Length - 1])
                return _y[_y.Length - 1] + EndSlope(true) * (x - _x[_x.Length - 1]);
            return Interpolate(x);
        }

        /// <summary>
        /// The map from y back to x. Where the map is flat (one y over a stretch of x) the inverse takes the middle of
        /// the stretch, except at the two ends, where a flat stretch is padding past the data (the KDE fit pads to the
        /// ends of x) and the inverse takes the end the data reached.
        /// </summary>
        public MonotoneMap Invert()
        {
            var x = new List<double>();
            var y = new List<double>();
            int start = 0;
            while (start < _x.Length)
            {
                int end = start;
                while (end + 1 < _x.Length && _y[end + 1] == _y[start])
                    end++;
                double at = start == 0 && end > start ? _x[end]
                    : end == _x.Length - 1 && end > start ? _x[start]
                    : (_x[start] + _x[end]) / 2;
                x.Add(_y[start]);
                y.Add(at);
                start = end + 1;
            }
            if (x.Count < 2)
                throw new InvalidOperationException(@"A flat map has no inverse.");
            return new MonotoneMap(x, y);
        }

        private double Interpolate(double x)
        {
            int hi = Array.BinarySearch(_x, x);
            if (hi >= 0)
                return _y[hi];
            hi = ~hi;
            if (hi == 0)
                return _y[0];
            if (hi >= _x.Length)
                return _y[_y.Length - 1];
            int lo = hi - 1;
            return _y[lo] + (_y[hi] - _y[lo]) * (x - _x[lo]) / (_x[hi] - _x[lo]);
        }

        /// <summary>The least-squares slope of the outer <see cref="END_FRACTION"/> of the knots (at least two) at one end.</summary>
        private double EndSlope(bool upper)
        {
            int count = Math.Max(2, (int)Math.Ceiling(_x.Length * END_FRACTION));
            int first = upper ? _x.Length - count : 0;
            double meanX = 0, meanY = 0;
            for (int k = first; k < first + count; k++)
            {
                meanX += _x[k];
                meanY += _y[k];
            }
            meanX /= count;
            meanY /= count;
            double sxy = 0, sxx = 0;
            for (int k = first; k < first + count; k++)
            {
                sxy += (_x[k] - meanX) * (_y[k] - meanY);
                sxx += (_x[k] - meanX) * (_x[k] - meanX);
            }
            return sxx > 0 ? Math.Max(0, sxy / sxx) : 0;
        }
    }
}
