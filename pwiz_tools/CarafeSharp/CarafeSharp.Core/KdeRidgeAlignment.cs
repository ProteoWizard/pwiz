/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Chronologer (https://github.com/searlelab/chronologer)
 *   src/chronologer/chronologer_utils/kde_alignment.py (KDE_align), Apache-2.0
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
using System.Threading.Tasks;

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// Chronologer's KDE ridge alignment (<c>KDE_align</c>), which maps one retention scale onto another from paired
    /// values that include wrong matches: the pairs are binned onto an n x n grid, each spread by a cosine-Gaussian
    /// kernel of Silverman's bandwidth, and the ridge of that density is walked from its apex up and down, one grid
    /// step in x, in y or in both at a time, towards the larger density. The ridge points, scaled back, are the knots of
    /// a monotone piecewise-linear map, padded flat to both ends of the x range. The arithmetic follows Chronologer's,
    /// so the knots are its own.
    /// </summary>
    public static class KdeRidgeAlignment
    {
        /// <summary>Chronologer's grid size (<c>--kde_n</c>).</summary>
        public const int DEFAULT_GRID = 3000;

        /// <summary>
        /// The map from <paramref name="x"/> to <paramref name="y"/>, fitted on a grid of <paramref name="grid"/> points
        /// per axis.
        /// </summary>
        public static MonotoneMap Fit(IReadOnlyList<double> x, IReadOnlyList<double> y, int grid = DEFAULT_GRID)
        {
            if (x.Count != y.Count)
                throw new ArgumentException(string.Format(@"{0} x values for {1} y values.", x.Count, y.Count));
            double minX = x.Min(), maxX = x.Max(), rangeX = maxX - minX;
            double minY = y.Min(), maxY = y.Max(), rangeY = maxY - minY;
            if (!(rangeX > 0) || !(rangeY > 0))
                throw new ArgumentException(@"A KDE alignment needs values that vary on both axes.");
            int n = grid;
            int count = x.Count;
            var xs = new int[count];
            var ys = new int[count];
            for (int k = 0; k < count; k++)
            {
                xs[k] = (int)Math.Round((n - 1) * (x[k] - minX) / rangeX);
                ys[k] = (int)Math.Round((n - 1) * (y[k] - minY) / rangeY);
            }

            // Silverman's bandwidth on the grid positions, and the cosine-Gaussian kernel's "stamp".
            double bandwidth = Math.Pow(count, -1.0 / 6) * (PopulationStd(xs) + PopulationStd(ys)) / 2;
            double kernelSd = bandwidth / 2 / Math.Sqrt(2 * Math.Log(2));
            double[] stamp = Stamp(kernelSd, out int radius);
            int width = 2 * radius + 1;

            // The density: every point's stamp added, clipped at the grid's edges. Rows are filled in parallel, each
            // cell taking its points in input order, so the sums are the same as in sequence.
            var density = new double[n * n];
            int stripes = Math.Max(1, Math.Min(Environment.ProcessorCount, n / Math.Max(1, width)));
            Parallel.For(0, stripes, stripe =>
            {
                int rowFrom = stripe * n / stripes, rowTo = (stripe + 1) * n / stripes;
                for (int k = 0; k < count; k++)
                {
                    int i = xs[k], j = ys[k];
                    int top = Math.Max(Math.Max(0, i - radius), rowFrom), bottom = Math.Min(Math.Min(n - 1, i + radius), rowTo - 1);
                    if (top > bottom)
                        continue;
                    int left = Math.Max(0, j - radius), right = Math.Min(n - 1, j + radius);
                    for (int row = top; row <= bottom; row++)
                    {
                        int cell = row * n;
                        int stampRow = (row - i + radius) * width - j + radius;
                        for (int col = left; col <= right; col++)
                            density[cell + col] += stamp[stampRow + col];
                    }
                }
            });

            // The apex (numpy's argmax: the first maximum in row-major order), then the ridge walks.
            int apex = 0;
            for (int cell = 1; cell < density.Length; cell++)
            {
                if (density[cell] > density[apex])
                    apex = cell;
            }
            int apexI = apex / n, apexJ = apex % n;
            var points = new List<(int I, int J)> { (apexI, apexJ) };
            Walk(density, n, apexI, apexJ, 1, points);
            Walk(density, n, apexI, apexJ, -1, points);
            points.Sort((a, b) => a.I.CompareTo(b.I));
            if (points[0].I != 0)
                points.Insert(0, (0, points[0].J));
            if (points[points.Count - 1].I != n - 1)
                points.Add((n - 1, points[points.Count - 1].J));

            var knotX = new double[points.Count];
            var knotY = new double[points.Count];
            for (int k = 0; k < points.Count; k++)
            {
                knotX[k] = points[k].I / (double)(n - 1) * rangeX + minX;
                knotY[k] = points[k].J / (double)(n - 1) * rangeY + minY;
            }
            return new MonotoneMap(knotX, knotY);
        }

        /// <summary>
        /// One ridge walk from the apex: up (<paramref name="direction"/> 1) or down (-1), a step at a time to the
        /// densest of the cells one along y, along x, or along both (the first of equals, as numpy's argmax), adding a
        /// point each time x moves, until an edge.
        /// </summary>
        private static void Walk(double[] density, int n, int i, int j, int direction, List<(int I, int J)> points)
        {
            while (direction > 0 ? i < n - 1 && j < n - 1 : i > 0 && j > 0)
            {
                double alongY = density[i * n + j + direction];
                double alongX = density[(i + direction) * n + j];
                double alongBoth = density[(i + direction) * n + j + direction];
                int best = 0;
                double bestValue = alongY;
                if (alongX > bestValue)
                {
                    best = 1;
                    bestValue = alongX;
                }
                if (alongBoth > bestValue)
                    best = 2;
                if (best != 1)
                    j += direction;
                if (best != 0)
                {
                    i += direction;
                    points.Add((i, j));
                }
            }
        }

        /// <summary>
        /// Chronologer's <c>define_stamp</c>: the cosine-Gaussian density <c>pi / (8 sd) x cos(d pi / (4 sd))</c> at
        /// each cell's distance d from the centre, 0 beyond 2 sd, over a square of radius <c>round(2 sd)</c>.
        /// </summary>
        private static double[] Stamp(double sd, out int radius)
        {
            double a = Math.PI / (8.0 * sd);
            double m = Math.PI / (4.0 * sd);
            double max = 2.0 * sd;
            radius = (int)Math.Round(2.0 * sd);
            int width = 2 * radius + 1;
            var stamp = new double[width * width];
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    double distance = Math.Sqrt((double)(i - radius) * (i - radius) + (double)(j - radius) * (j - radius));
                    stamp[i * width + j] = distance > max ? 0.0 : a * Math.Cos((0.0 - distance) * m);
                }
            }
            return stamp;
        }

        /// <summary>numpy's <c>np.std</c> of grid positions: the population standard deviation.</summary>
        private static double PopulationStd(IReadOnlyList<int> values)
        {
            double mean = values.Sum(v => (double)v) / values.Count;
            double squares = values.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(squares / values.Count);
        }
    }
}
