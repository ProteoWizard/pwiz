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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Centroid;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for Centrix in <c>Osprey.Centroid</c> and its use by Osprey (<c>docs/24-profile-centroiding.md</c>):
    /// <list type="bullet">
    /// <item><see cref="TestCentrixBasis"/>, <see cref="TestCentrixLasso"/>, <see cref="TestCentrixNoiseAndRegions"/>,
    /// <see cref="TestCentrixCentroider"/>: Rust Centrix's own unit tests, ported (the sigma and spacing of a
    /// 125 kTh/s ion trap MS2 scan, about two points per sigma), plus checks a port needs (the Gram row's truncation).</item>
    /// <item><see cref="TestCentrixActiveSet"/>: the exact solver meets the lasso's optimality conditions where
    /// coordinate descent only approaches them.</item>
    /// <item><see cref="TestCentrixCalibration"/>: the scan rate from the point spacing, the peak-width measurement,
    /// and the sigma measured on real Stellar spectra.</item>
    /// <item><see cref="TestCentrixRustGolden"/>: the port against Rust Centrix's centroids of real Stellar spectra,
    /// the test that keeps it a port (<c>Data/Centrix/README.md</c>).</item>
    /// <item><see cref="TestCentrixInOsprey"/>: <c>--centroid centrix</c> through the CLI, the search hash, the task
    /// keys, the spectra cache's name and the reader.</item>
    /// </list>
    /// </summary>
    [TestClass]
    public class CentrixTest
    {
        private const double SIGMA = 0.340;
        private const double SPACING = 0.125;

        // Rust Centrix's centroids of real Stellar profile spectra (Data/Centrix/README.md), and the settings it ran with.
        private const string GOLDEN_FOLDER = @"pwiz_tools/Osprey/Osprey.Test/Data/Centrix";
        private const string GOLDEN_FILE = @"stellar-centrix-rust.golden.tsv";
        private static readonly string[] GOLDEN_INPUTS = { @"stellar-2th-profile-slice.mzML", @"stellar-joint-split-slice.mzML" };
        private const double GOLDEN_SIGMA_MS1 = 0.2;
        private const double GOLDEN_SIGMA_MS2 = 0.33;
        // On these spectra the port agrees with Rust to 1e-11 Th and to float precision in intensity. Rust sums A'y with
        // BLAS and the port in index order, and over 6,000 such spectra 0.02% of centroids (solves stopped by the 1e-6
        // absolute tolerance a sweep apart) differ by up to 4e-4 Th and 0.4%, which these tolerances allow; a change
        // to the algorithm moves far more of them, by far more.
        private const double GOLDEN_MZ_TOLERANCE = 1e-3;
        private const double GOLDEN_INTENSITY_TOLERANCE = 0.01;
        private const double GOLDEN_CLOSE = 1e-4;
        private const double GOLDEN_MIN_CLOSE_FRACTION = 0.999;

        [TestMethod]
        public void TestCentrixBasis()
        {
            // The Gram diagonal is the discrete column norm, sigma sqrt(pi) / h, and the row decays strictly.
            var row = CentrixBasis.ComputeGramRow(0.25, 0.125);
            Assert.AreEqual(0.25 * Math.Sqrt(Math.PI) / 0.125, row[0], 1e-12);
            for (int k = 1; k < row.Length; k++)
                Assert.IsTrue(row[k] < row[k - 1], "The Gram row must decrease strictly");
            // Truncated at machine epsilon of the diagonal, a couple of dozen entries, not at the smallest double.
            Assert.IsTrue(row.Length >= 3 && row.Length < 40, "Gram row of " + row.Length + " entries");
            Assert.IsTrue(row[row.Length - 1] >= row[0] * 2.220446049250313e-16);

            // The analytic row matches the discrete A'A of a long uniform region within 5%.
            var basis = new CentrixBasis(0.25, 0.125, 0);
            const int points = 200;
            var mz = Enumerable.Range(0, points).Select(i => 400.0 + i * 0.125).ToArray();
            var a = basis.DesignMatrix(mz, 0, points, mz);
            double Gram(int i, int j)
            {
                double sum = 0;
                for (int r = 0; r < points; r++)
                    sum += a[r * points + i] * a[r * points + j];
                return sum;
            }
            Assert.AreEqual(1, Gram(points / 2, points / 2) / basis.GramRow[0], 0.05);
            Assert.AreEqual(1, Gram(points / 2, points / 2 + 1) / basis.GramRow[1], 0.05);

            // A column is 1 at its own position and exp(-1/2) one sigma away.
            var column = basis.DesignMatrix(new[] { 499.75, 500.0, 500.25 }, 0, 3, new[] { 500.0 });
            Assert.AreEqual(1, column[1], 1e-12);
            Assert.AreEqual(Math.Exp(-0.5), column[0], 1e-12);
            Assert.AreEqual(Math.Exp(-0.5), column[2], 1e-12);

            // The positions cover the range with one to spare on each side, all on the grid.
            var grid = basis.GridPositions(500.0, 501.0);
            Assert.IsTrue(grid[0] <= 500.0 - 0.125 + 1e-9 && grid[grid.Length - 1] >= 501.0 + 0.125 - 1e-9);
            foreach (double position in grid)
                Assert.AreEqual(Math.Round(position / 0.125) * 0.125, position, 1e-9);
        }

        [TestMethod]
        public void TestCentrixLasso()
        {
            // One Gaussian: one to three coefficients, the largest within 1.5 spacings of the center.
            var mz = Enumerable.Range(0, 32).Select(i => 498.0 + i * SPACING).ToArray();
            var single = Profile(mz, (500.0, 10000.0));
            var beta = Solve(mz, single, 100, null, out int coldIterations, out bool converged);
            Assert.IsTrue(converged);
            int nonzero = beta.Count(b => b > 0);
            Assert.IsTrue(nonzero >= 1 && nonzero <= 3, nonzero + " coefficients for one peak");
            var refined = NonNegativeLasso.RefineSubgrid(beta, mz);
            Assert.AreEqual(500.0, refined.OrderByDescending(c => c.Amplitude).First().Mz, 1.5 * SPACING);

            // A warm start from that solution, at a slightly smaller penalty, takes no more sweeps.
            var shifted = Profile(mz, (500.375, 5000.0));
            var cold = Solve(mz, shifted, 50, null, out coldIterations, out _);
            Solve(mz, shifted, 45, cold, out int warmIterations, out _);
            Assert.IsTrue(warmIterations <= coldIterations, "warm " + warmIterations + ", cold " + coldIterations);

            // Two peaks 2.2 sigma apart are both kept.
            var wide = Enumerable.Range(0, 48).Select(i => 497.0 + i * SPACING).ToArray();
            var pair = Solve(wide, Profile(wide, (500.0, 10000.0), (500.75, 10000.0)), 50, null, out _, out converged);
            Assert.IsTrue(converged);
            Assert.IsTrue(pair.Count(b => b > 0) >= 2);

            // A penalty above the signal keeps nothing; any signal keeps no negative coefficient.
            var flat = mz.Select(_ => 1.0).ToArray();
            Assert.AreEqual(0, Solve(mz, flat, 1000, null, out _, out _).Count(b => b > 0));
            var alternating = mz.Select((_, i) => i % 2 == 0 ? 1000.0 : 0.0).ToArray();
            Assert.IsTrue(Solve(mz, alternating, 1, null, out _, out _).All(b => b >= 0));

            // With no coefficients, rho is A'y.
            var aty = new[] { 100.0, 200.0, 150.0, 80.0 };
            var gramRow = new[] { 4.821, 4.662, 4.192, 3.537 };
            for (int j = 0; j < aty.Length; j++)
                Assert.AreEqual(aty[j], NonNegativeLasso.Rho(j, new double[4], aty, gramRow), 1e-10);

            // The parabola through the logarithms moves a centroid toward a peak between grid points.
            var points = new[] { 499.875, 500.0, 500.125, 500.25, 500.375 };
            var gaussian = points.Select(g => 1000 * Math.Exp(-(g - 500.05) * (g - 500.05) / (2 * SIGMA * SIGMA))).ToArray();
            double best = NonNegativeLasso.RefineSubgrid(gaussian, points).OrderByDescending(c => c.Amplitude).First().Mz;
            Assert.IsTrue(Math.Abs(best - 500.05) < Math.Abs(500.0 - 500.05), "refined to " + best);
        }

        [TestMethod]
        public void TestCentrixNoiseAndRegions()
        {
            // The IQR sigma of a uniform 0-1000 is 500 / 1.349, about 370.
            var uniform = Enumerable.Range(0, 10000).Select(i => (double)(float)(i * 1000.0 / 10000)).ToArray();
            double sigma = NoiseProfile.Rough(uniform).NoiseSigma;
            Assert.IsTrue(sigma > 280 && sigma < 450, "uniform sigma " + sigma);
            // All zeros still gives a positive sigma, the floor of 1.
            Assert.AreEqual(1, NoiseProfile.Rough(new double[100]).NoiseSigma);

            // Unrefined noise is the rough sigma everywhere; refined noise interpolates and holds its ends.
            var rough = NoiseProfile.Rough(new double[100]);
            Assert.AreEqual(1, rough.NoiseAt(500));
            var refined = new NoiseProfile(5, 30, new[] { 300.0, 500.0, 700.0 }, new[] { 20.0, 40.0, 30.0 });
            Assert.AreEqual(20, refined.NoiseAt(300), 1e-9);
            Assert.AreEqual(30, refined.NoiseAt(700), 1e-9);
            Assert.AreEqual(30, refined.NoiseAt(400), 1e-9);
            Assert.AreEqual(20, refined.NoiseAt(100), 1e-9);
            Assert.AreEqual(30, refined.NoiseAt(900), 1e-9);

            // One peak is one region, two separated peaks two, two overlapping peaks one, flat noise none.
            var mz = Enumerable.Range(0, 80).Select(i => 200.0 + i * SPACING).ToArray();
            Assert.AreEqual(1, Regions(mz, Profile(mz, (202.5, 1000.0))).Count);
            Assert.AreEqual(2, Regions(mz, Profile(mz, (202.5, 1000.0), (207.5, 1000.0))).Count);
            Assert.AreEqual(1, Regions(mz, Profile(mz, (202.0, 1000.0), (202.75, 1000.0))).Count);
            Assert.AreEqual(0, Regions(mz, mz.Select(_ => 1.0).ToArray()).Count);
            // Extension stops at the spectrum's ends.
            var atEdge = Regions(mz, Profile(mz, (200.0, 1000.0)));
            Assert.AreEqual(0, atEdge[0].Start);
        }

        [TestMethod]
        public void TestCentrixCentroider()
        {
            var centroider = new CentrixCentroider(new CentrixParams { SigmaTh = SIGMA, GridSpacingTh = SPACING });

            // One peak: the most intense centroid within two spacings of it.
            var single = Centroids(centroider, 490.0, 160, (500.25, 10000.0));
            Assert.IsTrue(single.Count >= 1);
            Assert.AreEqual(500.25, single.OrderByDescending(c => c.Intensity).First().Mz, 2 * SPACING);

            // Two peaks 0.625 Th (1.84 sigma) apart, the case Centrix exists for: both found.
            var pair = Centroids(centroider, 494.0, 160, (500.0, 10000.0), (500.625, 10000.0));
            Assert.IsTrue(pair.Count(c => Math.Abs(c.Mz - 500.0) < 0.2) >= 1, Describe(pair));
            Assert.IsTrue(pair.Count(c => Math.Abs(c.Mz - 500.625) < 0.2) >= 1, Describe(pair));

            // A peak midway between grid points gives one centroid, not a doublet, within a spacing of it.
            var midway = Centroids(centroider, 494.0, 160, (500.0625, 10000.0)).Where(c => Math.Abs(c.Mz - 500.0625) < 1).ToList();
            Assert.AreEqual(1, midway.Count, Describe(midway));
            Assert.AreEqual(500.0625, midway[0].Mz, SPACING);

            // Its intensity is the summed profile.
            var mz = Enumerable.Range(0, 160).Select(i => 494.0 + i * SPACING).ToArray();
            double summed = Profile(mz, (500.0625, 10000.0)).Sum();
            Assert.AreEqual(1, midway[0].Intensity / summed, 0.05);

            // Pure noise gives few centroids; no points give none.
            Assert.IsTrue(Centroids(centroider, 490.0, 160).Count <= 3);
            var none = new List<double>();
            centroider.Centroid(Array.Empty<double>(), Array.Empty<double>(), none, new List<double>());
            Assert.AreEqual(0, none.Count);

            // The spacing detected from the points is the spacing given.
            Assert.AreEqual(SPACING, CentrixCentroider.DetectGridSpacing(mz), 1e-9);
            var detecting = new CentrixCentroider(new CentrixParams { SigmaTh = SIGMA });
            var detected = Centroids(detecting, 494.0, 160, (500.0, 10000.0), (500.625, 10000.0));
            Assert.AreEqual(Describe(pair), Describe(detected));
        }

        [TestMethod]
        public void TestCentrixActiveSet()
        {
            // On random regions of Stellar-like peaks (sigma 0.33 Th on a 0.2 Th grid, neighbors correlated 0.91), the
            // active-set solution meets the lasso's optimality conditions, which coordinate descent only approaches: no
            // negative coefficient, rho equal to lambda wherever a coefficient is above zero, and no more than lambda
            // where it is zero. Its objective is never worse than coordinate descent's at Centrix's settings.
            const double sigma = 0.33, spacing = 0.2;
            var random = new Random(4711);
            var gramRow = CentrixBasis.ComputeGramRow(sigma, spacing);
            int capped = 0;
            for (int trial = 0; trial < 300; trial++)
            {
                int n = random.Next(5, 41);
                var mz = Enumerable.Range(0, n).Select(i => 500 + i * spacing).ToArray();
                var peaks = Enumerable.Range(0, random.Next(1, 5))
                    .Select(_ => (500 + random.NextDouble() * (n - 1) * spacing, Math.Pow(10, 2 + 3 * random.NextDouble())))
                    .ToArray();
                double noise = 5 + 50 * random.NextDouble();
                var y = GaussianProfile(mz, sigma, peaks).Select(v => Math.Max(0, v + noise * (random.NextDouble() - 0.5))).ToArray();
                var basis = new CentrixBasis(sigma, spacing, 0);
                var a = basis.DesignMatrix(mz, 0, n, mz);
                var aty = new double[n];
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                        aty[j] += a[i * n + j] * y[i];
                }
                double lambda = 3 * noise;

                var exact = NonNegativeLasso.SolveActiveSet(aty, gramRow, lambda, null);
                Assert.IsNotNull(exact, "trial " + trial);
                double scale = aty.Max(v => Math.Abs(v));
                for (int j = 0; j < n; j++)
                {
                    Assert.IsTrue(exact[j] >= 0);
                    double excess = NonNegativeLasso.Rho(j, exact, aty, gramRow) - gramRow[0] * exact[j] - lambda;
                    if (exact[j] > 0)
                        Assert.AreEqual(0, excess / scale, 1e-9, "trial " + trial + " coefficient " + j);
                    else
                        Assert.IsTrue(excess / scale <= 1e-9, "trial " + trial + " coefficient " + j);
                }

                var descent = NonNegativeLasso.Solve(aty, gramRow, lambda, null, 2000, 1e-6, out _, out bool converged);
                if (!converged)
                    capped++;
                Assert.IsTrue(Objective(exact, aty, gramRow, lambda) <= Objective(descent, aty, gramRow, lambda) + 1e-9 * scale * scale,
                    "trial " + trial);

                // From a warm start (here the coordinate descent solution at a larger penalty) it reaches the same optimum.
                var warm = NonNegativeLasso.Solve(aty, gramRow, 1.3 * lambda, null, 2000, 1e-6, out _, out _);
                var fromWarm = NonNegativeLasso.SolveActiveSet(aty, gramRow, lambda, warm);
                Assert.IsNotNull(fromWarm);
                for (int j = 0; j < n; j++)
                    Assert.AreEqual(exact[j], fromWarm[j], 1e-7 * scale, "trial " + trial + " warm " + j);
            }
            // The regime the active set exists for: some of these coordinate descent leaves at its cap.
            Assert.IsTrue(capped > 0, "no trial reached the coordinate descent cap");
        }

        [TestMethod]
        public void TestCentrixCalibration()
        {
            // The point spacing identifies a linear ion trap's scan rate.
            Assert.AreEqual(200, LinearIonTrapScanRate.FromSpacing(0.2).KThPerSecond);
            Assert.AreEqual(67, LinearIonTrapScanRate.FromSpacing(1.0 / 15).KThPerSecond);
            Assert.AreEqual(125, LinearIonTrapScanRate.FromSpacing(0.125).KThPerSecond);
            Assert.AreEqual(33, LinearIonTrapScanRate.FromSpacing(1.0 / 30).KThPerSecond);
            Assert.IsNull(LinearIonTrapScanRate.FromSpacing(0.1));

            // An isolated Gaussian's width is recovered wherever it falls between grid points; two species 0.4 Th apart
            // read as one wider peak; a peak with a point missing is not measured.
            const double sigma = 0.3, spacing = 0.2;
            var mz = Enumerable.Range(0, 200).Select(i => 400 + i * spacing).ToArray();
            for (double center = 410; center < 410.2; center += 0.05)
            {
                var widths = new List<double>();
                CentrixCalibration.AddPeakWidths(mz, GaussianProfile(mz, sigma, new[] { (center, 1000.0) }), widths);
                Assert.AreEqual(1, widths.Count);
                Assert.AreEqual(sigma * CentrixCalibration.FWHM_PER_SIGMA, widths[0], 0.03 * sigma * CentrixCalibration.FWHM_PER_SIGMA);
            }
            var doublet = new List<double>();
            CentrixCalibration.AddPeakWidths(mz, GaussianProfile(mz, sigma, new[] { (420.0, 1000.0), (420.4, 1000.0) }), doublet);
            Assert.AreEqual(1, doublet.Count);
            Assert.IsTrue(doublet[0] > 1.2 * sigma * CentrixCalibration.FWHM_PER_SIGMA);
            var gapped = GaussianProfile(mz, sigma, new[] { (430.0, 1000.0) });
            var withGap = new List<double>();
            CentrixCalibration.AddPeakWidths(mz.Where((_, i) => i != 149).ToArray(), gapped.Where((_, i) => i != 149).ToArray(), withGap);
            Assert.AreEqual(0, withGap.Count);
            // A few ions' spike, falling to zero before half its apex, is not measured, with its zeros present or not.
            var spike = new double[mz.Length];
            spike[99] = 80;
            spike[100] = 100;
            spike[101] = 70;
            var spikeWidths = new List<double>();
            CentrixCalibration.AddPeakWidths(mz, spike, spikeWidths);
            CentrixCalibration.AddPeakWidths(mz.Where((_, i) => spike[i] > 0).ToArray(), spike.Where(v => v > 0).ToArray(), spikeWidths);
            Assert.AreEqual(0, spikeWidths.Count);
            // A flat top whose log-parabola apex is twice the sampled maximum has no half-maximum crossing to
            // interpolate; it is not measured, rather than giving an infinite width.
            var flatTop = new double[mz.Length];
            flatTop[119] = 1;
            flatTop[120] = 300;
            flatTop[121] = 300;
            flatTop[122] = 150;
            flatTop[123] = 40;
            flatTop[124] = 5;
            var flatWidths = new List<double>();
            CentrixCalibration.AddPeakWidths(mz, flatTop, flatWidths);
            Assert.AreEqual(0, flatWidths.Count);

            // A linear ion trap's sigma follows its point spacing, whatever the peaks measure. Without a scan rate, the
            // narrow side of the peaks' widths; with too few peaks, Centrix's fallback.
            var rate = LinearIonTrapScanRate.FromSpacing(0.2);
            var many = Enumerable.Range(0, 100).Select(i => 0.6 + 0.01 * i).ToList();
            Assert.AreEqual(CentrixCalibration.SIGMA_PER_SPACING * 0.2, CentrixCalibration.EstimateSigma(many, rate), 1e-12);
            Assert.AreEqual(CentrixCalibration.SIGMA_PER_SPACING * 0.2, CentrixCalibration.EstimateSigma(new List<double>(), rate), 1e-12);
            Assert.AreEqual(many[(int)(CentrixCalibration.WIDTH_QUANTILE * 99)] / CentrixCalibration.FWHM_PER_SIGMA,
                CentrixCalibration.EstimateSigma(many, null), 1e-12);
            Assert.AreEqual(CentrixCalibration.FALLBACK_SIGMA_TH, CentrixCalibration.EstimateSigma(new List<double> { 0.7 }, null));

            // The calibrator settles a linear ion trap's level from its first spectrum; any other level takes every sample.
            var calibrator = new CentrixSigmaCalibrator();
            Assert.IsFalse(calibrator.IsSettled(2));
            calibrator.Add(2, mz, GaussianProfile(mz, sigma, new[] { (410.0, 1000.0) }));
            Assert.IsTrue(calibrator.IsSettled(2));
            Assert.IsFalse(calibrator.IsSettled(1));
            Assert.AreEqual(CentrixCalibration.SIGMA_PER_SPACING * 0.2, calibrator.Sigma(2), 1e-12);
            var orbitrapLike = Enumerable.Range(0, 200).Select(i => 400 + i * 0.01).ToArray();
            calibrator.Add(1, orbitrapLike, GaussianProfile(orbitrapLike, 0.02, new[] { (401.0, 1000.0) }));
            Assert.IsFalse(calibrator.IsSettled(1));
            Assert.AreEqual(CentrixCalibration.FALLBACK_SIGMA_TH, calibrator.Sigma(1));

            // On real Stellar profile spectra, read by Osprey with no sigma given: MS2 (200 kTh/s) and MS1 (67 kTh/s)
            // each at its own point spacing.
            string folder = Path.Combine(IOTest.FindPwizRoot(), GOLDEN_FOLDER);
            var read = SpectrumFileReader.LoadAllSpectra(Path.Combine(folder, GOLDEN_INPUTS[0]),
                new CentroidingOptions(new CentrixParams { SigmaTh = 0 }, new CentrixParams { SigmaTh = 0 }, 4));
            Assert.AreEqual(CentrixCalibration.SIGMA_PER_SPACING * 0.2, read.CentroidSigmaMs2, 1e-12);
            Assert.AreEqual(CentrixCalibration.SIGMA_PER_SPACING / 15, read.CentroidSigmaMs1, 1e-12);
        }

        [TestMethod]
        public void TestCentrixRustGolden()
        {
            // Real profile spectra, read by Osprey's reader, centroided as Rust Centrix centroided them: the same number
            // of centroids in every spectrum, each at Rust's m/z and intensity within the summation-order tolerance.
            string folder = Path.Combine(IOTest.FindPwizRoot(), GOLDEN_FOLDER);
            var golden = ReadGolden(Path.Combine(folder, GOLDEN_FILE));
            var centroiders = new Dictionary<int, CentrixCentroider>
            {
                { 1, new CentrixCentroider(new CentrixParams { SigmaTh = GOLDEN_SIGMA_MS1 }) },
                { 2, new CentrixCentroider(new CentrixParams { SigmaTh = GOLDEN_SIGMA_MS2 }) },
            };
            int spectra = 0, compared = 0, close = 0;
            double worstMz = 0, worstIntensity = 0;
            foreach (string input in GOLDEN_INPUTS)
            {
                var read = SpectrumFileReader.LoadAllSpectra(Path.Combine(folder, input));
                var profiles = read.Ms1Spectra.Select((s, i) => (Level: 1, Ordinal: i, s.Mzs, s.Intensities))
                    .Concat(read.Ms2Spectra.Select((s, i) => (Level: 2, Ordinal: i, s.Mzs, s.Intensities)));
                foreach (var profile in profiles)
                {
                    var key = (input, profile.Level, profile.Ordinal);
                    var expected = golden.TryGetValue(key, out var list) ? list : new List<(double Mz, double Intensity)>();
                    golden.Remove(key);
                    var actual = centroiders[profile.Level].Centroid(profile.Mzs,
                        profile.Intensities.Select(v => (double)v).ToArray());
                    string where = string.Format(CultureInfo.InvariantCulture, @"{0} MS{1} spectrum {2}", input,
                        profile.Level, profile.Ordinal);
                    Assert.AreEqual(expected.Count, actual.Count, where + @" centroid count");
                    for (int k = 0; k < actual.Count; k++)
                    {
                        double mzError = Math.Abs(actual[k].Mz - expected[k].Mz);
                        double intensityError = Math.Abs(actual[k].Intensity - expected[k].Intensity) / expected[k].Intensity;
                        worstMz = Math.Max(worstMz, mzError);
                        worstIntensity = Math.Max(worstIntensity, intensityError);
                        Assert.IsTrue(mzError <= GOLDEN_MZ_TOLERANCE && intensityError <= GOLDEN_INTENSITY_TOLERANCE,
                            string.Format(CultureInfo.InvariantCulture, @"{0} centroid {1}: {2:F5}:{3:F0} against Rust's {4:F5}:{5:F0}",
                                where, k, actual[k].Mz, actual[k].Intensity, expected[k].Mz, expected[k].Intensity));
                        if (mzError <= GOLDEN_CLOSE && intensityError <= GOLDEN_CLOSE)
                            close++;
                        compared++;
                    }
                    spectra++;
                }
            }
            Assert.AreEqual(0, golden.Count, @"Golden spectra the fixture did not have: " + string.Join(@", ", golden.Keys));
            Assert.AreEqual(30, spectra);
            double closeFraction = (double)close / compared;
            Console.WriteLine(@"{0} centroids of {1} spectra; {2:P3} within {3} of Rust; worst {4:E2} Th, {5:P3}", compared, spectra,
                closeFraction, GOLDEN_CLOSE, worstMz, worstIntensity);
            Assert.IsTrue(closeFraction >= GOLDEN_MIN_CLOSE_FRACTION, closeFraction.ToString(CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void TestCentrixInOsprey()
        {
            // Vendor centroiding is the default and leaves the search hash, the task keys and the cache name unchanged.
            var config = new OspreyConfig();
            Assert.AreEqual(CentroidMode.vendor, config.CentroidMode);
            string vendorHash = config.Identity.SearchParameterHash();
            Assert.IsNull(ProfileCentroiding.CreateOptions(config));
            Assert.IsNull(ProfileCentroiding.CacheTag(config));
            Assert.AreEqual(string.Empty, ProfileCentroiding.ValidityKeySuffix(config));

            // --centroid centrix and its settings; each setting changes the hash and the cache tag.
            var parsed = OspreyCommandArgs.ParseArgs(ArgTokens.Split(OspreyCommandArgs.ARG_CENTROID + @"centrix",
                OspreyCommandArgs.ARG_CENTRIX_SIGMA_MS2 + @"0.33", OspreyCommandArgs.ARG_CENTRIX_SOLVER + @"active-set"));
            Assert.AreEqual(CentroidMode.centrix, parsed.CentroidMode);
            Assert.AreEqual(0.33, parsed.CentrixSigmaMs2);
            Assert.AreEqual(0, parsed.CentrixSigmaMs1);
            Assert.IsTrue(parsed.CentrixActiveSet);
            config.CentroidMode = CentroidMode.centrix;
            string centrixHash = config.Identity.SearchParameterHash();
            string centrixTag = ProfileCentroiding.CacheTag(config);
            Assert.AreNotEqual(vendorHash, centrixHash);
            StringAssert.Matches(centrixTag, new System.Text.RegularExpressions.Regex(@"^centrix-[0-9a-f]{8}$"));
            StringAssert.Contains(ProfileCentroiding.ValidityKeySuffix(config), ProfileCentroiding.Descriptor(config));
            config.CentrixActiveSet = true;
            Assert.AreNotEqual(centrixHash, config.Identity.SearchParameterHash());
            Assert.AreNotEqual(centrixTag, ProfileCentroiding.CacheTag(config));
            config.CentrixActiveSet = false;
            config.CentrixSigmaMs2 = 0.3;
            Assert.AreNotEqual(centrixTag, ProfileCentroiding.CacheTag(config));
            config.CentrixSigmaMs2 = 0;
            config.CentroidMode = CentroidMode.vendor;
            Assert.AreEqual(vendorHash, config.Identity.SearchParameterHash());

            // The tag names the spectra cache, and the demultiplexed one, apart from the vendor-centroided ones.
            string source = Path.Combine(Path.GetTempPath(), @"run.raw");
            string vendorCache = SpectraCache.GetCachePath(source);
            try
            {
                ArtifactPaths.SpectraCacheTag = centrixTag;
                string centrixCache = SpectraCache.GetCachePath(source);
                Assert.AreEqual(Path.GetDirectoryName(vendorCache), Path.GetDirectoryName(centrixCache));
                Assert.AreEqual(@"run." + centrixTag + SpectraCache.EXT, Path.GetFileName(centrixCache));
                StringAssert.EndsWith(SpectraCache.GetDemuxCachePath(source), @"run." + centrixTag + @".demux" + SpectraCache.EXT);
            }
            finally
            {
                ArtifactPaths.SpectraCacheTag = null;
            }
            Assert.AreEqual(vendorCache, SpectraCache.GetCachePath(source));

            // The reader centroids the profile it reads into Rust Centrix's centroids, in parallel.
            string folder = Path.Combine(IOTest.FindPwizRoot(), GOLDEN_FOLDER);
            var golden = ReadGolden(Path.Combine(folder, GOLDEN_FILE));
            string input = GOLDEN_INPUTS[0];
            var options = new CentroidingOptions(new CentrixParams { SigmaTh = GOLDEN_SIGMA_MS1 },
                new CentrixParams { SigmaTh = GOLDEN_SIGMA_MS2 }, 4);
            var read = SpectrumFileReader.LoadAllSpectra(Path.Combine(folder, input), options);
            Assert.AreEqual(GOLDEN_SIGMA_MS2, read.CentroidSigmaMs2);
            var spectra = read.Ms1Spectra.Select((s, i) => (Level: 1, Ordinal: i, s.Mzs, s.Intensities))
                .Concat(read.Ms2Spectra.Select((s, i) => (Level: 2, Ordinal: i, s.Mzs, s.Intensities))).ToList();
            Assert.AreEqual(16, spectra.Count);
            foreach (var spectrum in spectra)
            {
                var expected = golden[(input, spectrum.Level, spectrum.Ordinal)];
                Assert.AreEqual(expected.Count, spectrum.Mzs.Length);
                for (int k = 0; k < expected.Count; k++)
                {
                    Assert.AreEqual(expected[k].Mz, spectrum.Mzs[k], GOLDEN_MZ_TOLERANCE);
                    Assert.AreEqual(expected[k].Intensity, spectrum.Intensities[k], GOLDEN_INTENSITY_TOLERANCE * expected[k].Intensity);
                }
            }
        }

        /// <summary>The golden's centroids by (input file, MS level, ordinal among that level's spectra), in file order.</summary>
        private static Dictionary<(string, int, int), List<(double Mz, double Intensity)>> ReadGolden(string path)
        {
            var golden = new Dictionary<(string, int, int), List<(double Mz, double Intensity)>>();
            foreach (string line in File.ReadLines(path).Skip(1))
            {
                var fields = line.Split('\t');
                var key = (fields[0], int.Parse(fields[1], CultureInfo.InvariantCulture), int.Parse(fields[2], CultureInfo.InvariantCulture));
                if (!golden.TryGetValue(key, out var list))
                {
                    list = new List<(double Mz, double Intensity)>();
                    golden[key] = list;
                }
                list.Add((double.Parse(fields[3], CultureInfo.InvariantCulture), double.Parse(fields[4], CultureInfo.InvariantCulture)));
            }
            return golden;
        }

        /// <summary>The lasso objective in Gram form, <c>1/2 b'Gb - b'A'y + lambda sum(b)</c>.</summary>
        private static double Objective(double[] beta, double[] aty, double[] gramRow, double lambda)
        {
            double value = 0;
            for (int j = 0; j < beta.Length; j++)
            {
                double gb = aty[j] - NonNegativeLasso.Rho(j, beta, aty, gramRow) + gramRow[0] * beta[j];
                value += 0.5 * beta[j] * gb - beta[j] * aty[j] + lambda * beta[j];
            }
            return value;
        }

        /// <summary>Gaussian peaks of a sigma, in double precision.</summary>
        private static double[] GaussianProfile(double[] mz, double sigma, (double Center, double Amplitude)[] peaks)
        {
            return mz.Select(m => peaks.Sum(p => p.Amplitude * Math.Exp(-(m - p.Center) * (m - p.Center) / (2 * sigma * sigma))))
                .ToArray();
        }

        /// <summary>Gaussian peaks of the test sigma, summed in single precision as a profile's intensities are.</summary>
        private static double[] Profile(double[] mz, params (double Center, double Amplitude)[] peaks)
        {
            return mz.Select(m => (double)(float)peaks.Sum(p => p.Amplitude * Math.Exp(-(m - p.Center) * (m - p.Center) / (2 * SIGMA * SIGMA))))
                .ToArray();
        }

        private static double[] Solve(double[] mz, double[] y, double lambda, double[] warmStart, out int iterations,
            out bool converged)
        {
            var basis = new CentrixBasis(SIGMA, mz[1] - mz[0], 0);
            var a = basis.DesignMatrix(mz, 0, mz.Length, mz);
            var aty = new double[mz.Length];
            for (int i = 0; i < mz.Length; i++)
            {
                for (int j = 0; j < mz.Length; j++)
                    aty[j] += a[i * mz.Length + j] * y[i];
            }
            // Highly correlated neighbors (G[1] / G[0] about 0.97) can take many sweeps.
            return NonNegativeLasso.Solve(aty, basis.GramRow, lambda, warmStart, 10000, 1e-6, out iterations, out converged);
        }

        private static List<SignalRegion> Regions(double[] mz, double[] intensity)
        {
            return SignalRegion.Detect(mz, intensity, 0, 1, 50, 2, 2, 3);
        }

        /// <summary>The centroids of a spectrum of these peaks over a flat level of 5, as Centrix's tests build it.</summary>
        private static List<(double Mz, double Intensity)> Centroids(CentrixCentroider centroider, double start, int count,
            params (double Center, double Amplitude)[] peaks)
        {
            var mz = Enumerable.Range(0, count).Select(i => start + i * SPACING).ToArray();
            var intensity = mz.Select(m => (double)Math.Max((float)peaks.Sum(p =>
                p.Amplitude * Math.Exp(-(m - p.Center) * (m - p.Center) / (2 * SIGMA * SIGMA))) + 5f, 0f)).ToArray();
            var centroidMz = new List<double>();
            var centroidIntensity = new List<double>();
            centroider.Centroid(mz, intensity, centroidMz, centroidIntensity);
            return centroidMz.Zip(centroidIntensity, (m, v) => (m, v)).ToList();
        }

        private static string Describe(IEnumerable<(double Mz, double Intensity)> centroids)
        {
            return string.Join(@", ", centroids.Select(c => c.Mz.ToString(@"F4", CultureInfo.InvariantCulture) + @":" + c.Intensity.ToString(@"F0", CultureInfo.InvariantCulture)));
        }
    }
}
