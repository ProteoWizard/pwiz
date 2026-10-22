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
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for <see cref="CentrixCentroider"/> and its parts, ported from Rust Centrix's own unit tests (the sigma
    /// and spacing of a 125 kTh/s ion trap MS2 scan, about two points per sigma), plus the checks a port needs.
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
