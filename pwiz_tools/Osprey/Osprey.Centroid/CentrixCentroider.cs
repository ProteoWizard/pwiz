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
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace pwiz.Osprey.Centroid
{
    /// <summary>
    /// Centroids a profile spectrum sampled on a uniform m/z grid (a linear ion trap's) as Centrix does: each region of
    /// signal is fitted with Gaussian peaks of a known sigma, one candidate per grid point, by a non-negative lasso,
    /// which separates peaks closer than one Th that a one-peak-per-maximum centroider (the vendor's) merges into one.
    /// <list type="number">
    /// <item>Pass 1: a rough noise estimate sets the penalty; each signal region is solved, keeping A'y, the solution
    /// and its residuals.</item>
    /// <item>The noise is refined along m/z from those residuals and the points outside every region.</item>
    /// <item>Pass 2: the regions whose penalty the refined noise changes by more than 20% are solved again from their
    /// pass-1 solution, and their centroids replace pass 1's.</item>
    /// <item>Each coefficient above zero is a centroid, placed between grid points by a parabola through its
    /// neighbors' logarithms, with intensity the sum of its peak's samples; centroids closer than sigma merge.</item>
    /// </list>
    /// <para><b>Origin and fidelity.</b> A port of Rust Centrix (maccoss/centrix, at 39370f1): with the same settings
    /// and the coordinate-descent solver it gives Rust's centroids, which
    /// <c>CentrixTest.TestCentrixRustGolden</c> pins on real Stellar spectra
    /// (<c>Osprey.Test/Data/Centrix/README.md</c>). Two deliberate differences, neither visible in that test: A'y is
    /// summed in index order where Rust calls BLAS, which moves 0.02% of centroids by up to 4e-4 Th when a solve
    /// stops at its tolerance a sweep apart; and the grid spacing is detected per spectrum (a basis kept per spacing)
    /// where Rust takes the first spectrum's of each MS level. Not ported: Centrix's sigma calibration (filter-string
    /// scan rate, wavelet fit), which misreads the Stellar; <see cref="CentrixCalibration"/> replaces it.</para>
    /// <para><b>Settings that matter.</b> The peak sigma (<see cref="CentrixParams.SigmaTh"/>), which
    /// <see cref="CentrixCalibration"/> gives a run: on a linear ion trap 1.1 point spacings, else one species' width
    /// measured from the run. The solver
    /// (<see cref="CentrixParams.Solver"/>): Centrix's coordinate descent, or the exact active-set solve, several times
    /// faster (6,000 Stellar spectra: 18-31 s against 64-169 s, file I/O included) and equal on the truth benchmark. Intensities must be in the instrument's units (the noise
    /// estimate floors its sigma at 1, which suits an ion trap's rates and not a count of ions).</para>
    /// <para><b>Users.</b> Osprey's reader with <c>--centroid centrix</c> (<c>SpectrumFileReader</c>), the staggered
    /// joint demultiplexer, on each bin's share of the measured profile (<c>StaggeredDemuxPlan</c>), and
    /// <c>Osprey.DemuxTool --centroid centrix</c>.</para>
    /// <para><b>A sigma that varies along the axis</b> (the constructor's <c>sigmaAt</c>): each region is fitted with the
    /// basis of the sigma at its center, and centroids merge within the sigma where they lie. For a grid uniform in some
    /// other coordinate than m/z, centroided in that coordinate (a TOF's sample index, where its peak is 1.2 to 1.5
    /// samples wide across the m/z range). Without it, one sigma, as Rust Centrix.</para>
    /// <para>Thread-safe: an instance holds only its settings and the bases it has built, replaced by reference or
    /// kept in a concurrent map. Raise <see cref="ALGORITHM_VERSION"/> with any change that moves a centroid.</para>
    /// </summary>
    public sealed class CentrixCentroider
    {
        /// <summary>
        /// The version of this centroider's output, part of the name of the spectra caches Osprey writes with it
        /// (<c>ProfileCentroiding.CacheTag</c>) and of every task's validity key. Raise it with any change that moves a
        /// centroid, so the caches and scores computed from the previous version's centroids are rebuilt rather than
        /// reused; a change to <see cref="CentrixParams"/> defaults counts, and so does one to the sigma
        /// <see cref="CentrixCalibration"/> gives a run when none is set (2: a linear ion trap's follows its point spacing).
        /// The Rust golden test must still pass, or the change must be one Rust Centrix made too, with the golden
        /// regenerated from it.
        /// </summary>
        public const int ALGORITHM_VERSION = 2;

        /// <summary>A varying sigma is rounded to this step, so a few bases serve every region.</summary>
        public const double SIGMA_STEP = 0.01;

        private readonly CentrixParams _parameters;
        private readonly Func<double, double> _sigmaAt;
        private readonly ConcurrentDictionary<long, CentrixBasis> _sigmaBases = new ConcurrentDictionary<long, CentrixBasis>();
        private CentrixBasis _basis;

        public CentrixCentroider(CentrixParams parameters) : this(parameters, null)
        {
        }

        /// <summary>
        /// A centroider whose peak sigma varies along the axis: <paramref name="sigmaAt"/> gives it at a coordinate, in
        /// the axis's units, and <see cref="CentrixParams.SigmaTh"/> is then only the sigma of the fixed basis. Needs a
        /// fixed <see cref="CentrixParams.GridSpacingTh"/>. Null: <see cref="CentrixParams.SigmaTh"/> everywhere.
        /// </summary>
        public CentrixCentroider(CentrixParams parameters, Func<double, double> sigmaAt)
        {
            if (!(parameters.SigmaTh > 0) || double.IsInfinity(parameters.SigmaTh))
                throw new ArgumentException(@"The peak sigma must be positive and finite.");
            if (sigmaAt != null && !(parameters.GridSpacingTh > 0))
                throw new ArgumentException(@"A varying peak sigma needs a fixed grid spacing.");
            _parameters = parameters.Copy();
            _sigmaAt = sigmaAt;
            if (_parameters.GridSpacingTh > 0)
                _basis = new CentrixBasis(_parameters.SigmaTh, _parameters.GridSpacingTh, _parameters.GridOffsetTh);
        }

        /// <summary>
        /// The spacing of a profile spectrum's points: the median gap (0.125 for fewer than two points, Centrix's default).
        /// A linear ion trap's firmware fixes it per scan rate (<see cref="LinearIonTrapScanRate"/>), so it also
        /// identifies the scan rate. Centrix takes it from the first spectrum of each MS level; here a basis is built per
        /// spacing and reused while the spacing stays the same.
        /// </summary>
        public static double DetectGridSpacing(IReadOnlyList<double> mz)
        {
            if (mz.Count < 2)
                return 0.125;
            var gaps = new double[mz.Count - 1];
            for (int i = 1; i < mz.Count; i++)
                gaps[i - 1] = mz[i] - mz[i - 1];
            Array.Sort(gaps); // Array.Sort OK: equal doubles are indistinguishable
            return gaps[gaps.Length / 2];
        }

        /// <summary>
        /// The centroids of one profile spectrum (m/z ascending, zero points present or not), appended to the lists in
        /// ascending m/z. The intensity of a centroid is the integrated intensity of its peak, the sum of its profile
        /// samples in the input's units (about 3.4 times the peak height a vendor centroid reports on the Stellar).
        /// This overload copies the lists, for callers holding pwiz-sharp's (<c>IPeakDetector</c>).
        /// </summary>
        public void Centroid(IReadOnlyList<double> mz, IReadOnlyList<double> intensity, List<double> centroidMz,
            List<double> centroidIntensity)
        {
            int n = Math.Min(mz.Count, intensity.Count);
            if (n < 3)
                return;
            var x = new double[n];
            var y = new double[n];
            for (int i = 0; i < n; i++)
            {
                x[i] = mz[i];
                y[i] = intensity[i];
            }
            foreach (var (m, v) in Centroid(x, y))
            {
                centroidMz.Add(m);
                centroidIntensity.Add(v);
            }
        }

        /// <summary>The centroids of one profile spectrum (as above, the arrays the same length), ascending in m/z.</summary>
        public List<(double Mz, double Intensity)> Centroid(double[] mz, double[] intensity)
        {
            var basis = mz.Length >= 3 ? BasisFor(mz) : null;
            return basis != null ? Centroid(mz, intensity, basis) : new List<(double Mz, double Intensity)>();
        }

        /// <summary>The two passes of the class summary on one spectrum, with its basis.</summary>
        private List<(double Mz, double Intensity)> Centroid(double[] mz, double[] intensity, CentrixBasis basis)
        {
            var p = _parameters;
            int n = mz.Length;
            var centroids = new List<(double Mz, double Intensity)>();
            if (n < 3)
                return centroids;

            var noise = NoiseProfile.Rough(intensity, p.MinNoiseSigma);
            double roughLambda = p.LambdaFactor * noise.NoiseSigma;
            var regions = SignalRegion.Detect(mz, intensity, noise.Baseline, noise.NoiseSigma, p.SignalThresholdSigma,
                p.MergeGapPoints, p.ExtensionPoints, p.MinRegionWidth);
            if (regions.Count == 0)
                return centroids;

            // Pass 1.
            var fits = new List<RegionFit>();
            var residuals = new List<(double CenterMz, double[] Residuals)>();
            for (int r = 0; r < regions.Count; r++)
            {
                var fit = FitRegion(r, regions[r], mz, intensity, RegionBasis(regions[r], basis), roughLambda);
                if (fit == null)
                    continue;
                fits.Add(fit);
                residuals.Add((fit.CenterMz, fit.Residuals));
                AddCentroids(fit.Beta, fit.Grid, fit.Basis, centroids);
            }

            // Pass 2: each region the refined noise changes enough solved again from its pass-1 solution, and its
            // centroids replacing every centroid within its m/z range, in region order, as Centrix replaces them.
            var refined = noise.Refine(mz, intensity, regions, residuals, p.NoiseWindowTh, p.NoiseStepTh);
            foreach (var fit in fits)
            {
                var region = regions[fit.Region];
                double lambda = p.LambdaFactor * refined.NoiseAt(region.CenterMz);
                if (!NeedsRefit(fit.Lambda, lambda, p.LambdaChangeThreshold))
                    continue;
                var beta = Solve(fit.Aty, fit.Basis.GramRow, lambda, fit.Beta);
                centroids.RemoveAll(c => c.Mz >= region.MzStart && c.Mz <= region.MzEnd);
                AddCentroids(beta, fit.Grid, fit.Basis, centroids);
            }

            if (p.MinCentroidSeparationTh > 0 || _sigmaAt == null)
                return Merge(SortByMz(centroids), p.MinCentroidSeparationTh > 0 ? p.MinCentroidSeparationTh : basis.Sigma);
            return Merge(SortByMz(centroids), _sigmaAt);
        }

        /// <summary>The basis a region is fitted with: the spectrum's, or with a varying sigma, the sigma at its center's.</summary>
        private CentrixBasis RegionBasis(SignalRegion region, CentrixBasis basis)
        {
            if (_sigmaAt == null)
                return basis;
            double sigma = _sigmaAt(region.CenterMz);
            if (!(sigma > 0) || double.IsInfinity(sigma))
                return basis;
            long key = (long)Math.Round(sigma / SIGMA_STEP);
            return _sigmaBases.GetOrAdd(key, k =>
                new CentrixBasis(k * SIGMA_STEP, _parameters.GridSpacingTh, _parameters.GridOffsetTh));
        }

        /// <summary>
        /// The basis for a spectrum: the fixed one when the spacing is set, else the one for the spectrum's own
        /// spacing, cached while the spacing stays the same. A spectrum's basis therefore depends on that spectrum
        /// alone, whichever thread or spectrum filled the cache. A spectrum with no spacing of its own (half its gaps
        /// zero) has none, and gives no centroids.
        /// </summary>
        private CentrixBasis BasisFor(double[] mz)
        {
            var basis = _basis;
            if (_parameters.GridSpacingTh > 0)
                return basis;
            double spacing = DetectGridSpacing(mz);
            if (!(spacing > 0))
                return null;
            if (basis != null && basis.GridSpacing == spacing)
                return basis;
            basis = new CentrixBasis(_parameters.SigmaTh, spacing, _parameters.GridOffsetTh);
            _basis = basis;
            return basis;
        }

        /// <summary>
        /// The pass-1 solve of a region on the basis positions covering it, with A'y kept for pass 2; null when the
        /// lasso keeps nothing, so the region gives no centroids and no residuals.
        /// </summary>
        private RegionFit FitRegion(int index, SignalRegion region, double[] mz, double[] intensity, CentrixBasis basis,
            double lambda)
        {
            int count = region.Count;
            if (count < 2)
                return null;
            var grid = basis.GridPositions(mz[region.Start], mz[region.End]);
            if (grid.Length == 0)
                return null;
            int columns = grid.Length;
            var a = basis.DesignMatrix(mz, region.Start, count, grid);
            var aty = new double[columns];
            for (int i = 0; i < count; i++)
            {
                double y = intensity[region.Start + i];
                int row = i * columns;
                for (int j = 0; j < columns; j++)
                    aty[j] += a[row + j] * y;
            }
            var beta = Solve(aty, basis.GramRow, lambda, null);
            bool any = false;
            foreach (double b in beta)
                any |= b > 0;
            if (!any)
                return null;

            var residuals = new double[count];
            for (int i = 0; i < count; i++)
            {
                double fitted = 0;
                int row = i * columns;
                for (int j = 0; j < columns; j++)
                    fitted += a[row + j] * beta[j];
                residuals[i] = intensity[region.Start + i] - fitted;
            }
            return new RegionFit(index, basis, grid, aty, beta, lambda, (mz[region.Start] + mz[region.End]) / 2, residuals);
        }

        /// <summary>
        /// A region's lasso by the chosen solver; an active-set solve that fails numerically falls back to coordinate
        /// descent.
        /// </summary>
        private double[] Solve(double[] aty, double[] gramRow, double lambda, double[] warmStart)
        {
            if (_parameters.Solver == CentrixSolver.active_set)
            {
                var beta = NonNegativeLasso.SolveActiveSet(aty, gramRow, lambda, warmStart);
                if (beta != null)
                    return beta;
            }
            return NonNegativeLasso.Solve(aty, gramRow, lambda, warmStart, _parameters.MaxIterations, _parameters.Tolerance,
                out _, out _);
        }

        /// <summary>A solution's centroids, each coefficient's amplitude made an integrated intensity.</summary>
        private static void AddCentroids(double[] beta, double[] grid, CentrixBasis basis,
            List<(double Mz, double Intensity)> centroids)
        {
            foreach (var (mz, amplitude) in NonNegativeLasso.RefineSubgrid(beta, grid))
                centroids.Add((mz, amplitude * basis.AreaScale));
        }

        /// <summary>Whether the penalty changed by more than the threshold fraction of the rough one.</summary>
        private static bool NeedsRefit(double roughLambda, double refinedLambda, double threshold)
        {
            if (roughLambda <= 0)
                return false;
            return Math.Abs(refinedLambda - roughLambda) / roughLambda > threshold;
        }

        /// <summary>Stably sorted by m/z, ties kept in the order found, as Rust's sort is stable.</summary>
        private static List<(double Mz, double Intensity)> SortByMz(List<(double Mz, double Intensity)> centroids)
        {
            var order = new int[centroids.Count];
            var keys = new double[centroids.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
                keys[i] = centroids[i].Mz;
            }
            Array.Sort(order, (i, j) => // Array.Sort OK: the index breaks ties
            {
                int byMz = keys[i].CompareTo(keys[j]);
                return byMz != 0 ? byMz : i.CompareTo(j);
            });
            var sorted = new List<(double Mz, double Intensity)>(order.Length);
            foreach (int i in order)
                sorted.Add(centroids[i]);
            return sorted;
        }

        /// <summary>
        /// Centroids closer than minSeparation to the one before (as merged so far) joined into it: intensities summed,
        /// m/z their intensity-weighted mean. Two Gaussians closer than sigma cannot be told apart at two points per
        /// sigma, and the lasso does split one peak between neighboring grid points; Centrix merges at sigma for that.
        /// </summary>
        private static List<(double Mz, double Intensity)> Merge(List<(double Mz, double Intensity)> sorted,
            double minSeparation)
        {
            var merged = new List<(double Mz, double Intensity)>(sorted.Count);
            if (sorted.Count == 0)
                return merged;
            var current = sorted[0];
            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];
                if (next.Mz - current.Mz < minSeparation)
                {
                    double total = current.Intensity + next.Intensity;
                    if (total > 0)
                        current = ((current.Mz * current.Intensity + next.Mz * next.Intensity) / total, total);
                }
                else
                {
                    merged.Add(current);
                    current = next;
                }
            }
            merged.Add(current);
            return merged;
        }

        /// <summary>
        /// As above, with the separation the sigma where each pair lies, at the centroid merged so far: a varying
        /// sigma's.
        /// </summary>
        private static List<(double Mz, double Intensity)> Merge(List<(double Mz, double Intensity)> sorted,
            Func<double, double> separationAt)
        {
            var merged = new List<(double Mz, double Intensity)>(sorted.Count);
            if (sorted.Count == 0)
                return merged;
            var current = sorted[0];
            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];
                if (next.Mz - current.Mz < separationAt(current.Mz))
                {
                    double total = current.Intensity + next.Intensity;
                    if (total > 0)
                        current = ((current.Mz * current.Intensity + next.Mz * next.Intensity) / total, total);
                }
                else
                {
                    merged.Add(current);
                    current = next;
                }
            }
            merged.Add(current);
            return merged;
        }

        /// <summary>A region's pass-1 solve, what pass 2 needs to solve it again.</summary>
        private sealed class RegionFit
        {
            public RegionFit(int region, CentrixBasis basis, double[] grid, double[] aty, double[] beta, double lambda,
                double centerMz, double[] residuals)
            {
                Region = region;
                Basis = basis;
                Grid = grid;
                Aty = aty;
                Beta = beta;
                Lambda = lambda;
                CenterMz = centerMz;
                Residuals = residuals;
            }

            public int Region { get; }
            public CentrixBasis Basis { get; }
            public double[] Grid { get; }
            public double[] Aty { get; }
            public double[] Beta { get; }
            public double Lambda { get; }
            public double CenterMz { get; }
            public double[] Residuals { get; }
        }
    }
}
