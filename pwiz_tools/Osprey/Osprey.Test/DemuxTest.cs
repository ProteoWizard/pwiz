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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.Demux;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for <see cref="Demultiplexer"/> and its parts, against exact synthetic truth:
    /// narrow-bin spectra are re-multiplexed into staggered windows, demultiplexed, and
    /// compared with the bins they came from.
    /// </summary>
    [TestClass]
    public class DemuxTest
    {
        private const double BIN_WIDTH = 4.0;
        private const double WINDOW_WIDTH = 8.0;
        private const double RANGE_LOW = 500.0;
        private const double RANGE_HIGH = 540.0;
        private const double SCAN_MINUTES = 0.1 / 60.0;

        // A fragment shared by four consecutive bins. A target whose seven-bin slice stops
        // inside this run sees the outside bin's signal through pwiz's cut edge row, and with
        // every bin of the run positive, nothing clips the error before it reaches the target.
        // Two adjacent bins without the fragment still make the exact solution unique.
        private const double SHARED_FRAGMENT_MZ = 650.321;
        private static readonly int[] SHARED_FRAGMENT_BINS = { 4, 5, 6, 7 };
        private static readonly float[] SHARED_FRAGMENT_INTENSITIES = { 1000f, 2000f, 2000f, 300f };

        /// <summary>
        /// NNLS: exact and constrained cases, then agreement with an independent brute-force
        /// active-set enumeration over random small systems (spec gate G1.3).
        /// </summary>
        [TestMethod]
        public void TestDemuxNnlsSolver()
        {
            // Full rank, feasible: the unconstrained path returns the exact solution.
            var identityLike = new double[,] { { 1, 1, 0 }, { 0, 1, 1 }, { 0, 0, 1 } };
            var solver = new NnlsSolver(identityLike);
            Assert.IsTrue(solver.IsFullColumnRank);
            var x = new double[3];
            var path = solver.Solve(new[] { 3.0, 5.0, 2.0 }, x, new NnlsSolver.Workspace(3));
            Assert.AreEqual(NnlsPath.unconstrained, path);
            AssertArrayNear(new[] { 0.0, 3.0, 2.0 }, x, 1e-12);

            // The constraint binds: the unconstrained answer has a negative component.
            path = solver.Solve(new[] { 1.0, 5.0, 2.0 }, x, new NnlsSolver.Workspace(3));
            Assert.AreEqual(NnlsPath.active_set, path);
            Assert.IsTrue(x.All(v => v >= 0));

            // All-zero right-hand side.
            path = solver.Solve(new double[3], x, new NnlsSolver.Workspace(3));
            Assert.AreEqual(NnlsPath.zero, path);
            AssertArrayNear(new double[3], x, 0);

            // Underdetermined (the covered-bins block shape): no pseudo-inverse, still solvable.
            var ladder = new double[,] { { 1, 1, 0 }, { 0, 1, 1 } };
            var ladderSolver = new NnlsSolver(ladder);
            Assert.IsFalse(ladderSolver.IsFullColumnRank);
            ladderSolver.Solve(new[] { 2.0, 0.0 }, x, new NnlsSolver.Workspace(3));
            // Only the first bin can explain signal in window 1 and none in window 2.
            AssertArrayNear(new[] { 2.0, 0.0, 0.0 }, x, 1e-12);

            CompareWithBruteForce(3000, 12345);
        }

        /// <summary>
        /// The RT interpolants: exactness where they must be exact, holding at the ends, and the
        /// apex-bias ordering behind the choice of makima as the default.
        /// </summary>
        [TestMethod]
        public void TestDemuxInterpolation()
        {
            var methods = new[]
            {
                RtInterpolation.makima, RtInterpolation.pchip,
                RtInterpolation.natural_three_point, RtInterpolation.linear,
            };
            var times = new[] { 1.0, 2.0, 3.5, 4.0, 5.0, 6.5 };
            var linearValues = times.Select(t => 3 * t + 2).ToArray();
            foreach (var method in methods)
            {
                // Linear data is reproduced exactly between the samples.
                foreach (double t in new[] { 1.5, 2.7, 3.9, 4.4, 6.0 })
                    Assert.AreEqual(3 * t + 2, RtInterpolator.Interpolate(method, times, linearValues, times.Length, t), 1e-9);
                // Nodes are reproduced exactly, and the ends are held, not extrapolated.
                Assert.AreEqual(linearValues[2], RtInterpolator.Interpolate(method, times, linearValues, times.Length, times[2]), 1e-12);
                Assert.AreEqual(linearValues[0], RtInterpolator.Interpolate(method, times, linearValues, times.Length, 0.0));
                Assert.AreEqual(linearValues[5], RtInterpolator.Interpolate(method, times, linearValues, times.Length, 9.0));
            }

            // PCHIP never overshoots a step.
            var step = new[] { 0.0, 0.0, 0.0, 10.0, 10.0, 10.0 };
            for (double t = 1.0; t <= 6.5; t += 0.05)
            {
                double v = RtInterpolator.Interpolate(RtInterpolation.pchip, times, step, times.Length, t);
                Assert.IsTrue(v >= -1e-12 && v <= 10 + 1e-12);
            }

            // Apex bias on a Gaussian sampled 4 times per FWHM, evaluated half a cycle off the
            // samples: makima beats PCHIP and the pwiz 3-point natural spline.
            double makima = MeanApexError(RtInterpolation.makima, 4.0);
            double pchip = MeanApexError(RtInterpolation.pchip, 4.0);
            double natural = MeanApexError(RtInterpolation.natural_three_point, 4.0);
            double linear = MeanApexError(RtInterpolation.linear, 4.0);
            Assert.IsTrue(makima < pchip, string.Format(@"makima {0} vs pchip {1}", makima, pchip));
            Assert.IsTrue(makima < natural, string.Format(@"makima {0} vs natural {1}", makima, natural));
            Assert.IsTrue(pchip < linear, string.Format(@"pchip {0} vs linear {1}", pchip, linear));
        }

        /// <summary>
        /// Scheme detection: k=2 and k=3 staggers, boundary jitter, variable widths, a
        /// non-overlapping scheme, and a window that first appears late in the run.
        /// </summary>
        [TestMethod]
        public void TestDemuxSchemeDetection()
        {
            // k=2 staggered: set A tiles 500-540, set B is offset by half a window and extends
            // one half-window past each end, so the two edge bins are covered once.
            var staggered = StaggeredCycle(RANGE_LOW, RANGE_HIGH, WINDOW_WIDTH, 2);
            var scheme = DemuxSchemeDetector.Detect(Repeat(staggered, 3));
            Assert.AreEqual(DemuxSchemeKind.overlapping, scheme.Kind);
            Assert.AreEqual(2, scheme.OverlapFactor);
            Assert.AreEqual(11, scheme.Windows.Count);
            Assert.AreEqual(12, scheme.Bins.Count);
            Assert.AreEqual(496.0, scheme.Bins[0].LowerBound, 1e-9);
            Assert.AreEqual(544.0, scheme.Bins[11].UpperBound, 1e-9);
            foreach (var bin in scheme.Bins)
                Assert.AreEqual(BIN_WIDTH, bin.Width, 1e-9);
            foreach (var window in scheme.Windows)
                Assert.AreEqual(2, window.BinCount);
            // Window indices follow m/z, whatever the acquisition order.
            Assert.AreEqual(3 * staggered.Count, scheme.WindowOfSpectrum.Length);
            Assert.AreEqual(496.0, scheme.Windows[scheme.WindowOfSpectrum[5]].LowerBound, 1e-9);

            // Reported edges jittered by 10 mTh give the same bins.
            var jittered = Repeat(staggered, 3).Select((w, i) =>
                new IsolationWindow(w.Center + ((i % 3) - 1) * 0.01, w.LowerOffset, w.UpperOffset)).ToList();
            var jitterScheme = DemuxSchemeDetector.Detect(jittered);
            Assert.AreEqual(12, jitterScheme.Bins.Count);
            Assert.AreEqual(2, jitterScheme.OverlapFactor);
            Assert.AreEqual(11, jitterScheme.Windows.Count);

            // k=3: 12 Th windows stepped by 4 Th.
            var k3 = StaggeredCycle(RANGE_LOW, RANGE_HIGH, 12.0, 3);
            var k3Scheme = DemuxSchemeDetector.Detect(Repeat(k3, 2));
            Assert.AreEqual(3, k3Scheme.OverlapFactor);
            Assert.IsTrue(k3Scheme.Bins.All(b => Math.Abs(b.Width - BIN_WIDTH) < 1e-9));

            // Variable widths (spec G4.3): no zero or negative width bins.
            var variable = new List<IsolationWindow>();
            var edges = new[] { 400.0, 406.0, 414.0, 424.0, 436.0 };
            for (int i = 0; i + 1 < edges.Length; i++)
                variable.Add(Window(edges[i], edges[i + 1]));
            for (int i = 0; i + 1 < edges.Length; i++)
            {
                double half = (edges[i + 1] - edges[i]) / 2;
                variable.Add(Window(edges[i] + half, edges[i + 1] + half));
            }
            var variableScheme = DemuxSchemeDetector.Detect(Repeat(variable, 2));
            Assert.AreEqual(2, variableScheme.OverlapFactor);
            Assert.IsTrue(variableScheme.Bins.All(b => b.Width > DemuxSchemeDetector.DEFAULT_MINIMUM_BIN_WIDTH));

            // Non-overlapping: nothing to do, and the input comes back unchanged.
            var plain = new List<IsolationWindow>();
            for (double lo = RANGE_LOW; lo < RANGE_HIGH; lo += WINDOW_WIDTH)
                plain.Add(Window(lo, lo + WINDOW_WIDTH));
            Assert.AreEqual(DemuxSchemeKind.non_overlapping, DemuxSchemeDetector.Detect(Repeat(plain, 2)).Kind);
            var plainSpectra = Repeat(plain, 2).Select((w, i) => MakeSpectrum((uint)i, i * SCAN_MINUTES, w,
                new[] { 300.0 }, new[] { 1.0f })).ToList();
            var passthrough = Demultiplexer.Demultiplex(plainSpectra, new DemuxParams());
            Assert.AreSame(plainSpectra, passthrough.Spectra);

            // Ordinary DIA whose neighbors overlap by a 1 Th margin is not a stagger.
            var margins = new List<IsolationWindow>();
            for (double lo = 400; lo < 500; lo += 11)
                margins.Add(Window(lo, lo + 12));
            var marginScheme = DemuxSchemeDetector.Detect(Repeat(margins, 2));
            Assert.AreEqual(DemuxSchemeKind.non_overlapping, marginScheme.Kind);
            Assert.AreEqual(1, marginScheme.OverlapFactor);
            Assert.AreEqual(2, marginScheme.MaxCoverage);

            // A window seen only after several cycles is still a window.
            var late = Repeat(plain, 4);
            late.Add(Window(RANGE_HIGH, RANGE_HIGH + WINDOW_WIDTH));
            Assert.AreEqual(plain.Count + 1, DemuxSchemeDetector.Detect(late).Windows.Count);
        }

        /// <summary>
        /// End-to-end on re-multiplexed synthetic data with constant elution, where every
        /// interpolant is exact, so any error is the block and the solver (spec G1.1, G1.2,
        /// G1.4). Also checks thread-count independence.
        /// </summary>
        [TestMethod]
        public void TestDemuxStaggeredRoundTrip()
        {
            var run = BuildStaggeredRun(20);

            foreach (var outputMode in new[] { DemuxOutputMode.apportioned, DemuxOutputMode.solution })
            {
                var parameters = new DemuxParams { OutputMode = outputMode };
                var result = Demultiplexer.Demultiplex(run.Spectra, parameters);
                Assert.AreEqual(2 * run.Spectra.Count, result.Spectra.Count);
                Assert.AreEqual(0, result.Statistics.IterationCapSolves);
                double maxError = MaxRelativeError(result, run, out int missing, out int spurious);
                Assert.IsTrue(maxError < 1e-5, string.Format(@"{0}: max relative error {1}", outputMode, maxError));
                Assert.AreEqual(0, missing);
                Assert.AreEqual(0, spurious);
                Assert.IsTrue(result.Spectra.All(s => s.Intensities.All(v => v > 0)));
                AssertStructure(result, run);
            }

            // pwiz's truncated block gets the shared fragment wrong. Apportioning then hides
            // it wherever the target's other bin solved to zero, so look at the solution itself.
            var truncated = Demultiplexer.Demultiplex(run.Spectra, new DemuxParams
            {
                BlockMode = DemuxBlockMode.truncated_slice,
                OutputMode = DemuxOutputMode.solution,
            });
            double truncatedError = MaxRelativeError(truncated, run, out _, out _);
            Assert.IsTrue(truncatedError > 0.01, string.Format(@"truncated error {0}", truncatedError));

            // Thread count changes scheduling only, never the output.
            var single = Demultiplexer.Demultiplex(run.Spectra, new DemuxParams { Threads = 1 });
            var multi = Demultiplexer.Demultiplex(run.Spectra, new DemuxParams { Threads = 4 });
            AssertIdentical(single, multi);
        }

        /// <summary>
        /// The demultiplexed cache: round trip, refusal on any mismatch between the cache kind or
        /// descriptor and what the reader expects, and window detection over every bin. Also
        /// the settings that select demux: the CLI switch and the search hash.
        /// </summary>
        [TestMethod]
        public void TestDemuxCache()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"OspreyDemuxCache" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(dir);
            try
            {
                var run = BuildStaggeredRun(6);
                var ms1 = new List<MS1Spectrum>();
                string rawPath = Path.Combine(dir, @"run.spectra.bin");
                string demuxPath = Path.Combine(dir, @"run.demux.spectra.bin");
                var parameters = new DemuxParams();
                string descriptor = parameters.Descriptor;
                var result = Demultiplexer.Demultiplex(run.Spectra, parameters);
                SpectraCache.SaveSpectraCache(rawPath, run.Spectra, ms1);
                SpectraCache.SaveSpectraCache(demuxPath, result.Spectra, ms1, null, descriptor);

                var index = SpectraWindowIndex.BuildFromCache(demuxPath, null, descriptor);
                Assert.IsNotNull(index);
                Assert.AreEqual(result.Spectra.Count, index.Ms2Count);
                Assert.AreEqual(demuxPath, index.CachePath);
                // Every bin is a window, including the top edge bin that only the offset set
                // covers and that first appears after other bins have repeated.
                Assert.AreEqual(run.Bins.Count, index.IsolationWindows.Count);
                var loaded = SpectraCache.LoadSpectraCache(demuxPath, null, descriptor);
                Assert.IsNotNull(loaded);
                AssertIdentical(result, new DemuxResult(result.Scheme, loaded.Ms2Spectra, result.Statistics));

                // The raw cache keeps first-cycle detection, and its window list is unchanged.
                var rawIndex = SpectraWindowIndex.BuildFromCache(rawPath);
                Assert.IsNotNull(rawIndex);
                Assert.AreEqual(run.Spectra.Count, rawIndex.Ms2Count);

                // Refusals: a demultiplexed cache read as plain, a plain one read as
                // demultiplexed, and a descriptor from other settings.
                AssertRefused(demuxPath, null);
                AssertRefused(rawPath, descriptor);
                AssertRefused(demuxPath, new DemuxParams { Interpolation = RtInterpolation.pchip }.Descriptor);
                Assert.IsNull(SpectraCache.LoadSpectraCache(demuxPath));
            }
            finally
            {
                Directory.Delete(dir, true);
            }

            // Descriptors are stable and distinguish every output-changing setting, but not threads.
            Assert.AreEqual(new DemuxParams().Descriptor, new DemuxParams { Threads = 8 }.Descriptor);
            Assert.AreNotEqual(new DemuxParams().Descriptor,
                new DemuxParams { BlockMode = DemuxBlockMode.truncated_slice }.Descriptor);
            Assert.AreNotEqual(new DemuxParams().Descriptor,
                new DemuxParams { OutputMode = DemuxOutputMode.solution }.Descriptor);

            // --demux selects the mode, and only an enabled mode enters the search hash, so every
            // existing hash is unchanged while it is off.
            Assert.AreEqual(DemuxMode.off, new OspreyConfig().DemuxMode);
            Assert.AreEqual(DemuxMode.auto,
                OspreyCommandArgs.ParseArgs(ArgTokens.Split(OspreyCommandArgs.ARG_DEMUX + @"auto")).DemuxMode);
            Assert.AreEqual(DemuxMode.off,
                OspreyCommandArgs.ParseArgs(ArgTokens.Split(OspreyCommandArgs.ARG_DEMUX + @"off")).DemuxMode);
            var config = new OspreyConfig();
            string offHash = config.Identity.SearchParameterHash();
            config.DemuxMode = DemuxMode.auto;
            Assert.AreNotEqual(offHash, config.Identity.SearchParameterHash());
            config.DemuxMode = DemuxMode.off;
            Assert.AreEqual(offHash, config.Identity.SearchParameterHash());
        }

        private static void AssertRefused(string cachePath, string descriptor)
        {
            var index = SpectraWindowIndex.BuildFromCache(cachePath, null, out var reason, descriptor);
            Assert.IsNull(index);
            Assert.AreEqual(SpectraCacheRejection.DemuxSettingsChanged, reason);
        }

        private static void CompareWithBruteForce(int systems, int seed)
        {
            var random = new Random(seed);
            var workspace = new NnlsSolver.Workspace(8);
            for (int s = 0; s < systems; s++)
            {
                int rows = random.Next(2, 10);
                int columns = random.Next(1, 8);
                bool mask = random.Next(2) == 0;
                var a = new double[rows, columns];
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < columns; c++)
                        a[r, c] = mask ? random.Next(2) : random.NextDouble();
                }
                var b = new double[rows];
                for (int r = 0; r < rows; r++)
                    b[r] = random.NextDouble() * 2 - 0.5;

                var solver = new NnlsSolver(a);
                var x = new double[columns];
                var path = solver.Solve(b, x, workspace);
                Assert.AreNotEqual(NnlsPath.iteration_cap, path);
                Assert.IsTrue(x.All(v => v >= 0));
                double objective = Objective(a, b, x);
                double best = BruteForceObjective(a, b);
                Assert.AreEqual(best, objective, 1e-9 * Math.Max(1, best),
                    string.Format(@"system {0}: {1} x {2}", s, rows, columns));
            }
        }

        /// <summary>
        /// The NNLS optimum by exhaustion: the best non-negative unconstrained least-squares
        /// solution over every column subset, solved by Gaussian elimination with partial
        /// pivoting. Deliberately shares no code with <see cref="NnlsSolver"/>.
        /// </summary>
        private static double BruteForceObjective(double[,] a, double[] b)
        {
            int rows = a.GetLength(0), columns = a.GetLength(1);
            double best = Objective(a, b, new double[columns]);
            for (int subset = 1; subset < 1 << columns; subset++)
            {
                var index = Enumerable.Range(0, columns).Where(c => (subset & (1 << c)) != 0).ToArray();
                int p = index.Length;
                var m = new double[p, p + 1];
                for (int i = 0; i < p; i++)
                {
                    for (int j = 0; j < p; j++)
                    {
                        for (int r = 0; r < rows; r++)
                            m[i, j] += a[r, index[i]] * a[r, index[j]];
                    }
                    for (int r = 0; r < rows; r++)
                        m[i, p] += a[r, index[i]] * b[r];
                }
                var z = SolveGaussian(m, p);
                if (z == null || z.Any(v => v < 0))
                    continue;
                var x = new double[columns];
                for (int i = 0; i < p; i++)
                    x[index[i]] = z[i];
                best = Math.Min(best, Objective(a, b, x));
            }
            return best;
        }

        private static double[] SolveGaussian(double[,] m, int p)
        {
            for (int col = 0; col < p; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < p; r++)
                {
                    if (Math.Abs(m[r, col]) > Math.Abs(m[pivot, col]))
                        pivot = r;
                }
                if (Math.Abs(m[pivot, col]) < 1e-9)
                    return null;
                for (int j = 0; j <= p; j++)
                {
                    double tmp = m[col, j];
                    m[col, j] = m[pivot, j];
                    m[pivot, j] = tmp;
                }
                for (int r = 0; r < p; r++)
                {
                    if (r == col)
                        continue;
                    double f = m[r, col] / m[col, col];
                    for (int j = col; j <= p; j++)
                        m[r, j] -= f * m[col, j];
                }
            }
            var z = new double[p];
            for (int i = 0; i < p; i++)
                z[i] = m[i, p] / m[i, i];
            return z;
        }

        private static double Objective(double[,] a, double[] b, double[] x)
        {
            double sum = 0;
            for (int r = 0; r < a.GetLength(0); r++)
            {
                double residual = -b[r];
                for (int c = 0; c < a.GetLength(1); c++)
                    residual += a[r, c] * x[c];
                sum += residual * residual;
            }
            return sum;
        }

        /// <summary>
        /// Mean absolute error at the apex region, as a fraction of peak height, for a Gaussian
        /// sampled <paramref name="pointsPerFwhm"/> times per FWHM and evaluated at the
        /// half-cycle offsets, over evenly spaced apex phases.
        /// </summary>
        private static double MeanApexError(RtInterpolation method, double pointsPerFwhm)
        {
            const double fwhm = 1.0;
            double sigma = fwhm / 2.3548;
            double cycle = fwhm / pointsPerFwhm;
            int perSide = RtInterpolator.SamplesPerSide(method);
            double total = 0;
            int count = 0;
            for (int phase = 0; phase < 20; phase++)
            {
                double apex = phase * cycle / 20.0;
                // The sample interval containing the apex, target at its midpoint.
                double target = Math.Floor(apex / cycle) * cycle + cycle / 2;
                var times = new double[2 * perSide];
                var values = new double[2 * perSide];
                for (int s = 0; s < 2 * perSide; s++)
                {
                    times[s] = target - cycle / 2 + (s - perSide + 1) * cycle;
                    values[s] = Math.Exp(-0.5 * Math.Pow((times[s] - apex) / sigma, 2));
                }
                double truth = Math.Exp(-0.5 * Math.Pow((target - apex) / sigma, 2));
                total += Math.Abs(RtInterpolator.Interpolate(method, times, values, times.Length, target) - truth);
                count++;
            }
            return total / count;
        }

        private static List<IsolationWindow> StaggeredCycle(double low, double high, double width, int k)
        {
            // Set j is offset by j*width/k and shifted down so that every bin in [low, high]
            // is covered k times; windows beyond the range edges supply the edge coverage.
            var cycle = new List<IsolationWindow>();
            double step = width / k;
            for (int set = 0; set < k; set++)
            {
                for (double lo = low - set * step; lo < high; lo += width)
                {
                    if (lo + width <= low)
                        continue;
                    cycle.Add(Window(lo, lo + width));
                }
            }
            return cycle;
        }

        private static List<IsolationWindow> Repeat(List<IsolationWindow> cycle, int times)
        {
            var result = new List<IsolationWindow>();
            for (int i = 0; i < times; i++)
                result.AddRange(cycle);
            return result;
        }

        private static IsolationWindow Window(double low, double high)
        {
            return IsolationWindow.Symmetric((low + high) / 2, (high - low) / 2);
        }

        private static Spectrum MakeSpectrum(uint scan, double rt, IsolationWindow window,
            double[] mzs, float[] intensities)
        {
            return new Spectrum
            {
                ScanNumber = scan,
                RetentionTime = rt,
                PrecursorMz = window.Center,
                IsolationWindow = window,
                Mzs = mzs,
                Intensities = intensities,
            };
        }

        /// <summary>
        /// A staggered run in which each narrow bin holds one precursor with three unique
        /// fragments, plus one fragment shared between two bins. Elution is constant, so the
        /// acquired spectra are exact sums of their bins' truth.
        /// </summary>
        private static SyntheticRun BuildStaggeredRun(int cycles)
        {
            var cycle = StaggeredCycle(RANGE_LOW, RANGE_HIGH, WINDOW_WIDTH, 2);
            var bins = DemuxSchemeDetector.Detect(cycle).Bins;
            var truth = new Dictionary<double, float>[bins.Count];
            for (int j = 0; j < bins.Count; j++)
            {
                truth[j] = new Dictionary<double, float>();
                foreach (double offset in new[] { 0.0, 5.13, 11.71 })
                    truth[j][Math.Round(300 + 37.1 * j + offset, 4)] = 1000f * (j + 1) + (float)(offset * 10);
            }
            for (int i = 0; i < SHARED_FRAGMENT_BINS.Length; i++)
                truth[SHARED_FRAGMENT_BINS[i]][SHARED_FRAGMENT_MZ] = SHARED_FRAGMENT_INTENSITIES[i];

            var spectra = new List<Spectrum>();
            uint scan = 0;
            for (int c = 0; c < cycles; c++)
            {
                foreach (var window in cycle)
                {
                    var peaks = new SortedDictionary<double, float>();
                    for (int j = 0; j < bins.Count; j++)
                    {
                        if (bins[j].Center <= window.LowerBound || bins[j].Center >= window.UpperBound)
                            continue;
                        foreach (var peak in truth[j])
                        {
                            peaks.TryGetValue(peak.Key, out float existing);
                            peaks[peak.Key] = existing + peak.Value;
                        }
                    }
                    spectra.Add(MakeSpectrum(scan, scan * SCAN_MINUTES, window,
                        peaks.Keys.ToArray(), peaks.Values.ToArray()));
                    scan++;
                }
            }
            return new SyntheticRun(spectra, bins, truth);
        }

        /// <summary>
        /// Largest relative error of any output peak against its bin's truth. Also counts truth
        /// peaks missing from an output spectrum whose parent measured them, and output peaks
        /// with no truth at all.
        /// </summary>
        private static double MaxRelativeError(DemuxResult result, SyntheticRun run,
            out int missing, out int spurious)
        {
            double maxError = 0;
            missing = 0;
            spurious = 0;
            foreach (var spectrum in result.Spectra)
            {
                int bin = BinOf(run.Bins, spectrum.IsolationWindow.Center);
                var expected = run.Truth[bin];
                var seen = new HashSet<double>();
                for (int p = 0; p < spectrum.Mzs.Length; p++)
                {
                    if (!expected.TryGetValue(spectrum.Mzs[p], out float truth))
                    {
                        spurious++;
                        continue;
                    }
                    seen.Add(spectrum.Mzs[p]);
                    maxError = Math.Max(maxError, Math.Abs(spectrum.Intensities[p] - truth) / truth);
                }
                foreach (var peak in expected)
                {
                    if (!seen.Contains(peak.Key))
                    {
                        missing++;
                        maxError = Math.Max(maxError, 1.0);
                    }
                }
            }
            return maxError;
        }

        private static void AssertStructure(DemuxResult result, SyntheticRun run)
        {
            // Each parent yields its two bins, in bin order, carrying its scan number and time.
            for (int i = 0; i < run.Spectra.Count; i++)
            {
                var parent = run.Spectra[i];
                var first = result.Spectra[2 * i];
                var second = result.Spectra[2 * i + 1];
                Assert.AreEqual(parent.ScanNumber, first.ScanNumber);
                Assert.AreEqual(parent.ScanNumber, second.ScanNumber);
                Assert.AreEqual(parent.RetentionTime, first.RetentionTime);
                Assert.AreEqual(parent.IsolationWindow.LowerBound, first.IsolationWindow.LowerBound, 1e-9);
                Assert.AreEqual(first.IsolationWindow.UpperBound, second.IsolationWindow.LowerBound, 1e-9);
                Assert.AreEqual(parent.IsolationWindow.UpperBound, second.IsolationWindow.UpperBound, 1e-9);
                Assert.AreEqual(BIN_WIDTH, first.IsolationWindow.Width, 1e-9);
            }
        }

        private static void AssertIdentical(DemuxResult expected, DemuxResult actual)
        {
            Assert.AreEqual(expected.Spectra.Count, actual.Spectra.Count);
            for (int i = 0; i < expected.Spectra.Count; i++)
            {
                CollectionAssert.AreEqual(expected.Spectra[i].Mzs, actual.Spectra[i].Mzs);
                CollectionAssert.AreEqual(expected.Spectra[i].Intensities, actual.Spectra[i].Intensities);
            }
        }

        private static int BinOf(IReadOnlyList<DemuxBin> bins, double mz)
        {
            for (int j = 0; j < bins.Count; j++)
            {
                if (bins[j].LowerBound < mz && mz < bins[j].UpperBound)
                    return j;
            }
            throw new AssertFailedException(string.Format(@"No bin contains {0}", mz));
        }

        private static void AssertArrayNear(double[] expected, double[] actual, double tolerance)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i], tolerance);
        }

        private sealed class SyntheticRun
        {
            public SyntheticRun(List<Spectrum> spectra, IReadOnlyList<DemuxBin> bins,
                Dictionary<double, float>[] truth)
            {
                Spectra = spectra;
                Bins = bins;
                Truth = truth;
            }

            public List<Spectrum> Spectra { get; }
            public IReadOnlyList<DemuxBin> Bins { get; }
            public Dictionary<double, float>[] Truth { get; }
        }
    }
}
