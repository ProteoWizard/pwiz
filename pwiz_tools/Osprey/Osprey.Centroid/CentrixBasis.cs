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

namespace pwiz.Osprey.Centroid
{
    /// <summary>
    /// Gaussian peaks of one sigma on a uniform m/z grid: the basis Centrix fits a profile with. Column j of the design
    /// matrix is the peak at grid position j sampled at the region's points,
    /// <c>A[i, j] = exp(-(mz[i] - grid[j])^2 / (2 sigma^2))</c>, and on a uniform grid the Gram matrix A'A is
    /// Toeplitz, so only its first row is kept: <c>G[i, j] = GramRow[|i - j|]</c>.
    /// <para>The basis positions are <c>GridOffset + k * GridSpacing</c>, the spacing the profile's own; Centrix uses an
    /// offset of 0, so the positions need not fall on the data points (the Stellar's are at 200.2 + k * 0.2, which
    /// they do). A coefficient's amplitude is the peak's height; <see cref="AreaScale"/> makes it the summed intensity
    /// a centroid reports.</para>
    /// </summary>
    public sealed class CentrixBasis
    {
        /// <summary>
        /// Rust's f64::EPSILON, the machine epsilon. Not .NET's double.Epsilon, which is the smallest subnormal and
        /// would keep thousands of underflowing Gram entries.
        /// </summary>
        private const double MACHINE_EPSILON = 2.220446049250313e-16;

        public CentrixBasis(double sigma, double gridSpacing, double gridOffset)
        {
            if (!(sigma > 0) || !(gridSpacing > 0))
                throw new ArgumentException(@"The peak sigma and grid spacing must be positive.");
            Sigma = sigma;
            GridSpacing = gridSpacing;
            GridOffset = gridOffset;
            GramRow = ComputeGramRow(sigma, gridSpacing);
            AreaScale = sigma * Math.Sqrt(2 * Math.PI) / gridSpacing;
        }

        /// <summary>The peak sigma, Th.</summary>
        public double Sigma { get; }

        /// <summary>The spacing of the basis positions, Th: the profile's point spacing.</summary>
        public double GridSpacing { get; }

        /// <summary>The m/z of basis position 0.</summary>
        public double GridOffset { get; }

        /// <summary>
        /// The first row of the Gram matrix, <c>(sigma sqrt(pi) / h) exp(-(k h)^2 / (4 sigma^2))</c>: the discrete sum
        /// of two sampled peaks k positions apart, so <c>GramRow[0]</c> is a column's squared norm, which the
        /// coordinate descent update divides by. Truncated where it falls below machine epsilon of its first entry.
        /// </summary>
        public double[] GramRow { get; }

        /// <summary>
        /// A coefficient's integrated intensity per unit: <c>sigma sqrt(2 pi) / h</c>, the sum of its peak's samples,
        /// as the vendor's centroider sums them.
        /// </summary>
        public double AreaScale { get; }

        /// <summary>
        /// The basis positions covering [mzStart, mzEnd] with a position to spare on each side: those of
        /// <c>GridOffset + k * GridSpacing</c> within one spacing of the range.
        /// </summary>
        public double[] GridPositions(double mzStart, double mzEnd)
        {
            long k = (long)Math.Floor((mzStart - GridOffset) / GridSpacing) - 1;
            var positions = new List<double>();
            while (true)
            {
                double position = GridOffset + k * GridSpacing;
                if (position > mzEnd + GridSpacing)
                    break;
                if (position >= mzStart - GridSpacing)
                    positions.Add(position);
                k++;
            }
            return positions.ToArray();
        }

        /// <summary>The design matrix of the points mz[start..start+count) on these positions, row-major.</summary>
        public double[] DesignMatrix(double[] mz, int start, int count, double[] grid)
        {
            int columns = grid.Length;
            double scale = -1 / (2 * Sigma * Sigma);
            var a = new double[count * columns];
            for (int i = 0; i < count; i++)
            {
                double x = mz[start + i];
                int row = i * columns;
                for (int j = 0; j < columns; j++)
                {
                    double d = x - grid[j];
                    a[row + j] = Math.Exp(d * d * scale);
                }
            }
            return a;
        }

        public static double[] ComputeGramRow(double sigma, double gridSpacing)
        {
            double norm = sigma * Math.Sqrt(Math.PI) / gridSpacing;
            double decay = -gridSpacing * gridSpacing / (4 * sigma * sigma);
            double threshold = norm * MACHINE_EPSILON;
            var row = new List<double>(32);
            for (long k = 0; ; k++)
            {
                double value = norm * Math.Exp(decay * (k * k));
                if (k > 0 && value < threshold)
                    break;
                row.Add(value);
            }
            return row.ToArray();
        }
    }
}
