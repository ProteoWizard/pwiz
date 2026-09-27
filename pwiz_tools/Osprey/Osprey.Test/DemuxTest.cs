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
using pwiz.Osprey.Core;
using pwiz.Osprey.Demux;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

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

        // The real-data fixture and its golden (Data/Demux/README.md).
        private const string FIXTURE_FOLDER = @"pwiz_tools/Osprey/Osprey.Test/Data/Demux";
        private const string FIXTURE_STAGGERED = @"eclipse-ev13-staggered-slice.mzML";
        private const string FIXTURE_MSCONVERT = @"eclipse-ev13-msconvert-demux-slice.mzML";
        private const string FIXTURE_GOLDEN = @"eclipse-ev13-osprey-demux.golden.tsv";
        private const string REBLESS_VARIABLE = @"OSPREY_REBLESS_DEMUX_FIXTURE";

        // Where a demultiplexed cache's descriptor length sits: after the magic (8), version (4),
        // source size and mtime (8 each), and the MS2 and MS1 counts (4 each).
        private const int DESCRIPTOR_LENGTH_OFFSET = 36;

        // Agreement floors against msconvert's demultiplexing of the fixture. Measured on
        // 2026-09-25: default median cosine 0.9987 with 93.6% of spectra at 0.95 or better;
        // msconvert-like settings median 0.9991 (93.4%). The full runs agree similarly (median
        // 0.997 / 0.999). A drop below these means Osprey moved away from msconvert, which is
        // either a regression or a deliberate change that should say so.
        private const double FIXTURE_MIN_MEDIAN_COSINE = 0.995;
        private const double FIXTURE_MIN_FRACTION_95 = 0.90;
        private const double FIXTURE_MIN_MEDIAN_COSINE_MSCONVERT_LIKE = 0.997;

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
        /// Channel extraction: two split channels share an edge, and a peak exactly on it counts
        /// in one of them only (the upper), while the last channel keeps its upper edge.
        /// </summary>
        [TestMethod]
        public void TestDemuxChannelEdges()
        {
            var low = new[] { 99.0, 100.0 };
            var high = new[] { 100.0, 101.0 };
            var spectrum = MakeSpectrum(0, 1, Window(90, 110),
                new[] { 98.0, 99.0, 99.5, 100.0, 101.0, 102.0 },
                new[] { 1f, 2f, 4f, 8f, 16f, 32f });
            var channels = new double[2];
            OverlapDemultiplexer.ExtractChannels(spectrum, low, high, 2, channels, 0);
            Assert.AreEqual(2 + 4, channels[0]);
            Assert.AreEqual(8 + 16, channels[1]);
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
        /// The conditions the constant-elution round trip cannot see: centroid m/z jitter (so
        /// channels must be matched within the ppm tolerance, not by equality), k=3 and variable
        /// window widths end to end, and elution that changes between acquisitions (so every
        /// neighboring window's value must be interpolated from the right samples of the right
        /// window). A stencil that read the wrong cycle, or the wrong side of the target time,
        /// passes constant elution and fails here.
        /// </summary>
        [TestMethod]
        public void TestDemuxRealisticSynthetic()
        {
            const double jitterPpm = 3;

            // Jittered m/z, constant elution: still exact, in both output modes.
            var k2 = BuildRun(new RunOptions
            {
                Cycle = StaggeredCycle(RANGE_LOW, RANGE_HIGH, WINDOW_WIDTH, 2),
                JitterPpm = jitterPpm,
            });
            AssertExact(@"k=2 jittered", k2, 2);

            // k=3: 12 Th windows stepped by 4 Th, each spectrum splitting into three bins.
            var k3 = BuildRun(new RunOptions
            {
                Cycle = StaggeredCycle(RANGE_LOW, RANGE_HIGH, 12.0, 3),
                JitterPpm = jitterPpm,
            });
            Assert.AreEqual(3, Demultiplexer.DetectScheme(k3.Spectra).OverlapFactor);
            AssertExact(@"k=3 jittered", k3, 3);

            // Variable widths: each window offset by half its own width, so bins range from 1 to
            // 6 Th and windows cover two or three of them.
            var variable = new List<IsolationWindow>();
            var edges = new[] { 500.0, 506.0, 514.0, 524.0, 536.0, 550.0 };
            for (int i = 0; i + 1 < edges.Length; i++)
                variable.Add(Window(edges[i], edges[i + 1]));
            for (int i = 0; i + 1 < edges.Length; i++)
            {
                double half = (edges[i + 1] - edges[i]) / 2;
                variable.Add(Window(edges[i] + half, edges[i + 1] + half));
            }
            var variableRun = BuildRun(new RunOptions
            {
                Cycle = variable,
                SharedFragment = false,
                JitterPpm = jitterPpm,
            });
            AssertExact(@"variable width jittered", variableRun, 0);

            // Elution that moves: sigma of 2.5 cycles is about 6 samples per FWHM per window, the
            // regime DIA methods are designed for. In solution mode a unique fragment's value is
            // the fit of the target's own measurement and its partner window's interpolated one,
            // so any stencil error shows directly.
            var eluting = BuildRun(new RunOptions
            {
                Cycle = StaggeredCycle(RANGE_LOW, RANGE_HIGH, WINDOW_WIDTH, 2),
                Cycles = 30,
                ElutionSigmaCycles = 2.5,
                JitterPpm = jitterPpm,
            });
            // Measured: makima max 0.77% / median 0.008% / leak 0.033%; PCHIP 1.48% / 0.006% /
            // 0.072%. The pwiz 3-point natural spline and linear interpolation each lose a few
            // peaks on elution tails (the spline undershoots to zero, or the chord cuts the
            // apex), so their bound is on the median and the leak only. The leak, intensity put
            // in a bin that has no such fragment, must rank makima < PCHIP < natural < linear:
            // that ordering is why makima is the default.
            var bounds = new[]
            {
                (RtInterpolation.makima, 0.02, 0.0005, 0.001),
                (RtInterpolation.pchip, 0.03, 0.0005, 0.002),
                (RtInterpolation.natural_three_point, double.NaN, 0.001, 0.003),
                (RtInterpolation.linear, double.NaN, 0.003, 0.01),
            };
            double previousLeak = 0;
            foreach (var (interpolation, maxError, medianError, maxLeak) in bounds)
            {
                var errors = MeasureErrors(Demultiplexer.Demultiplex(eluting.Spectra, new DemuxParams
                {
                    Interpolation = interpolation,
                    OutputMode = DemuxOutputMode.solution,
                }), eluting);
                if (!double.IsNaN(maxError))
                {
                    Assert.IsTrue(errors.Max < maxError,
                        string.Format(@"{0}: max error {1:P2} of apex", interpolation, errors.Max));
                    Assert.AreEqual(0, errors.Missing, interpolation + @": missing");
                }
                Assert.IsTrue(errors.Median < medianError,
                    string.Format(@"{0}: median error {1:P3} of apex", interpolation, errors.Median));
                Assert.IsTrue(errors.SpuriousFraction < maxLeak,
                    string.Format(@"{0}: leak {1:P3} of intensity", interpolation, errors.SpuriousFraction));
                Assert.IsTrue(errors.SpuriousFraction > previousLeak,
                    string.Format(@"{0}: leak {1:P3} does not rank after {2:P3}", interpolation,
                        errors.SpuriousFraction, previousLeak));
                previousLeak = errors.SpuriousFraction;
            }

            // The default configuration end to end, apportioned output: no peak lost.
            var defaults = MeasureErrors(Demultiplexer.Demultiplex(eluting.Spectra, new DemuxParams()), eluting);
            Assert.AreEqual(0, defaults.Missing, @"default apportioned: missing");
            Assert.IsTrue(defaults.Max < 0.02, string.Format(@"default apportioned: max error {0:P2}", defaults.Max));
        }

        /// <summary>
        /// Osprey's demultiplexing of a real staggered acquisition: a slice of an Orbitrap
        /// Eclipse run (see Data/Demux/README.md). Pins the output against a committed golden
        /// summary, checks it against msconvert's demultiplexing of the same slice, and checks
        /// that the msconvert-like settings reproduce msconvert more closely still.
        /// </summary>
        [TestMethod]
        public void TestDemuxEclipseFixture()
        {
            string folder = Path.Combine(IOTest.FindPwizRoot(), FIXTURE_FOLDER);
            var staggered = SpectrumFileReader.LoadAllSpectra(Path.Combine(folder, FIXTURE_STAGGERED)).Ms2Spectra;
            var msconvert = SpectrumFileReader.LoadAllSpectra(Path.Combine(folder, FIXTURE_MSCONVERT)).Ms2Spectra;
            Assert.AreEqual(204, staggered.Count);
            Assert.AreEqual(408, msconvert.Count);

            // The real geometry: 12 Th windows staggered by 6, reported edges a few mTh apart.
            var scheme = Demultiplexer.DetectScheme(staggered);
            Assert.AreEqual(DemuxSchemeKind.overlapping, scheme.Kind);
            Assert.AreEqual(2, scheme.OverlapFactor);
            Assert.AreEqual(8, scheme.Windows.Count);
            Assert.AreEqual(9, scheme.Bins.Count);
            foreach (var bin in scheme.Bins)
                Assert.AreEqual(6.0, bin.Width, 0.01);

            var result = Demultiplexer.Demultiplex(staggered, new DemuxParams { Threads = 4 });
            Assert.AreEqual(408, result.Spectra.Count);
            Assert.AreEqual(0, result.Statistics.IterationCapSolves);
            AssertIdentical(result, Demultiplexer.Demultiplex(staggered, new DemuxParams { Threads = 1 }));
            CheckGolden(Path.Combine(folder, FIXTURE_GOLDEN), result);

            // Against msconvert: close, by design not identical (block layout and interpolant).
            var agreement = MeasureAgreement(msconvert, result.Spectra);
            Assert.AreEqual(408, agreement.Paired);
            Assert.IsTrue(agreement.MedianCosine >= FIXTURE_MIN_MEDIAN_COSINE,
                string.Format(@"median cosine vs msconvert {0:F4}", agreement.MedianCosine));
            Assert.IsTrue(agreement.FractionAbove95 >= FIXTURE_MIN_FRACTION_95,
                string.Format(@"fraction >= 0.95 vs msconvert {0:F3}", agreement.FractionAbove95));
            Assert.AreEqual(1.0, agreement.IntensityRatio, 0.01);

            // msconvert's own settings bring Osprey's typical spectrum closer to it.
            var msconvertLike = Demultiplexer.Demultiplex(staggered, new DemuxParams
            {
                BlockMode = DemuxBlockMode.truncated_slice,
                Interpolation = RtInterpolation.natural_three_point,
                Threads = 4,
            });
            var likeAgreement = MeasureAgreement(msconvert, msconvertLike.Spectra);
            Assert.IsTrue(likeAgreement.MedianCosine >= FIXTURE_MIN_MEDIAN_COSINE_MSCONVERT_LIKE,
                string.Format(@"msconvert-like median cosine {0:F4}", likeAgreement.MedianCosine));
            Assert.IsTrue(likeAgreement.MedianCosine >= agreement.MedianCosine,
                string.Format(@"msconvert-like median {0:F4} vs default {1:F4}",
                    likeAgreement.MedianCosine, agreement.MedianCosine));
        }

        /// <summary>
        /// The pipeline wiring around the demultiplexer, through the same entry point Stages 1-4
        /// and --task SpectraCache use: the demux-off guard, building and then reusing the
        /// demultiplexed cache, searching from it alone, rebuilding it when its settings change,
        /// leaving a non-overlapping run alone, and the Stage-6 rule for a missing demux cache.
        /// </summary>
        [TestMethod]
        public void TestDemuxPipelineWiring()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"OspreyDemuxWiring" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(dir);
            try
            {
                // Stand-in sources: a cache hit precedes the reader, so the bytes are never read,
                // and saving with the source path makes the fingerprint check real.
                string staggeredSource = Path.Combine(dir, @"staggered.raw");
                File.WriteAllBytes(staggeredSource, new byte[] { 1, 2, 3, 4 });
                var run = BuildStaggeredRun(6);
                string rawCache = SpectraCache.GetCachePath(staggeredSource);
                string demuxCache = SpectraCache.GetDemuxCachePath(staggeredSource);
                SpectraCache.SaveSpectraCache(rawCache, run.Spectra, new List<MS1Spectrum>(), staggeredSource);

                // Demux off: an overlapping run is refused rather than searched as acquired.
                var off = WiringContext(DemuxMode.off);
                Assert.ThrowsException<InvalidOperationException>(() =>
                    ScoringTaskShared.EnsureSpectraCache(staggeredSource, false, out _, off));
                Assert.IsFalse(File.Exists(demuxCache));

                // Demux auto: the demultiplexed cache is built and is what gets searched.
                var auto = WiringContext(DemuxMode.auto);
                var index = ScoringTaskShared.EnsureSpectraCache(staggeredSource, false, out _, auto);
                Assert.AreEqual(demuxCache, index.CachePath);
                Assert.AreEqual(2 * run.Spectra.Count, index.Ms2Count);
                Assert.AreEqual(run.Bins.Count, index.IsolationWindows.Count);
                Assert.IsTrue(File.Exists(demuxCache));
                DateTime built = File.GetLastWriteTimeUtc(demuxCache);

                // A second run reuses it rather than demultiplexing again.
                index = ScoringTaskShared.EnsureSpectraCache(staggeredSource, false, out _, auto);
                Assert.AreEqual(demuxCache, index.CachePath);
                Assert.AreEqual(built, File.GetLastWriteTimeUtc(demuxCache));

                // The demultiplexed cache alone is enough, as for a cohort staged with demux.
                File.Delete(rawCache);
                index = ScoringTaskShared.EnsureSpectraCache(staggeredSource, false, out _, auto);
                Assert.AreEqual(demuxCache, index.CachePath);

                // A demultiplexed cache written with other settings is rebuilt from .spectra.bin.
                SpectraCache.SaveSpectraCache(rawCache, run.Spectra, new List<MS1Spectrum>(), staggeredSource);
                SpectraCache.SaveSpectraCache(demuxCache, run.Spectra, new List<MS1Spectrum>(), staggeredSource,
                    @"osprey-demux/0;stale");
                index = ScoringTaskShared.EnsureSpectraCache(staggeredSource, false, out _, auto);
                Assert.AreEqual(2 * run.Spectra.Count, index.Ms2Count);
                Assert.IsNotNull(SpectraWindowIndex.BuildFromCache(demuxCache, staggeredSource,
                    DemuxCacheBuilder.CreateParams(auto.Config).Descriptor));

                // Stage 6: with only the .spectra.bin of an overlapping run, demux on is an error
                // (the earlier stages searched the demultiplexed cache) and demux off is the guard.
                var rawIndex = SpectraWindowIndex.BuildFromCache(rawCache, staggeredSource);
                Assert.IsNotNull(rawIndex);
                Assert.ThrowsException<SpectraCacheException>(() =>
                    DemuxCacheBuilder.ThrowIfDemuxCacheMissing(staggeredSource, rawIndex, auto));
                Assert.ThrowsException<InvalidOperationException>(() =>
                    DemuxCacheBuilder.ThrowIfDemuxCacheMissing(staggeredSource, rawIndex, off));

                // A run whose windows do not overlap is searched as acquired, even with demux on.
                string plainSource = Path.Combine(dir, @"plain.raw");
                File.WriteAllBytes(plainSource, new byte[] { 5, 6, 7, 8 });
                var plain = new List<Spectrum>();
                for (int c = 0; c < 6; c++)
                {
                    for (double lo = RANGE_LOW; lo < RANGE_HIGH; lo += WINDOW_WIDTH)
                    {
                        plain.Add(MakeSpectrum((uint)plain.Count, plain.Count * SCAN_MINUTES,
                            Window(lo, lo + WINDOW_WIDTH), new[] { 300.0 + lo }, new[] { 10.0f }));
                    }
                }
                SpectraCache.SaveSpectraCache(SpectraCache.GetCachePath(plainSource), plain,
                    new List<MS1Spectrum>(), plainSource);
                index = ScoringTaskShared.EnsureSpectraCache(plainSource, false, out _, auto);
                Assert.AreEqual(SpectraCache.GetCachePath(plainSource), index.CachePath);
                Assert.IsFalse(File.Exists(SpectraCache.GetDemuxCachePath(plainSource)));
                DemuxCacheBuilder.ThrowIfDemuxCacheMissing(plainSource, index, auto);
                ScoringTaskShared.EnsureSpectraCache(plainSource, false, out _, off);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
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

                // The undemultiplexed cache lists its acquisition windows.
                var rawIndex = SpectraWindowIndex.BuildFromCache(rawPath);
                Assert.IsNotNull(rawIndex);
                Assert.AreEqual(run.Spectra.Count, rawIndex.Ms2Count);
                Assert.AreEqual(result.Scheme.Windows.Count, rawIndex.IsolationWindows.Count);

                // A PLAIN cache of already-demultiplexed spectra is what searching an
                // msconvert-demultiplexed mzML produces. Its top bin also first appears after
                // other bins repeat, and must still be a window (it once was silently dropped).
                string plainDemuxPath = Path.Combine(dir, @"msconvert.spectra.bin");
                SpectraCache.SaveSpectraCache(plainDemuxPath, result.Spectra, ms1);
                var plainDemuxIndex = SpectraWindowIndex.BuildFromCache(plainDemuxPath);
                Assert.IsNotNull(plainDemuxIndex);
                Assert.AreEqual(run.Bins.Count, plainDemuxIndex.IsolationWindows.Count);

                // Refusals: a demultiplexed cache read as plain, a plain one read as
                // demultiplexed, and a descriptor from other settings.
                AssertRefused(demuxPath, null);
                AssertRefused(rawPath, descriptor);
                AssertRefused(demuxPath, new DemuxParams { Interpolation = RtInterpolation.pchip }.Descriptor);
                Assert.IsNull(SpectraCache.LoadSpectraCache(demuxPath));

                // The same rules from the header alone, as the start-up input check reads it.
                string otherDescriptor = new DemuxParams { Interpolation = RtInterpolation.pchip }.Descriptor;
                Assert.AreEqual(SpectraCacheRejection.None, SpectraCache.CheckHeader(demuxPath, null, descriptor));
                Assert.AreEqual(SpectraCacheRejection.DemuxSettingsChanged,
                    SpectraCache.CheckHeader(demuxPath, null, otherDescriptor));
                Assert.AreEqual(SpectraCacheRejection.DemuxSettingsChanged, SpectraCache.CheckHeader(demuxPath, null, null));
                Assert.AreEqual(SpectraCacheRejection.Absent,
                    SpectraCache.CheckHeader(Path.Combine(dir, @"missing.demux.spectra.bin"), null, descriptor));

                // A corrupt descriptor length, or a header that ends inside the descriptor, is a
                // truncated header: neither a descriptor to compare nor a crash.
                byte[] bytes = File.ReadAllBytes(demuxPath);
                string corruptPath = Path.Combine(dir, @"corrupt.demux.spectra.bin");
                var corrupt = (byte[])bytes.Clone();
                BitConverter.GetBytes(uint.MaxValue).CopyTo(corrupt, DESCRIPTOR_LENGTH_OFFSET);
                File.WriteAllBytes(corruptPath, corrupt);
                Assert.AreEqual(SpectraCacheRejection.TruncatedHeader, SpectraCache.CheckHeader(corruptPath, null, descriptor));
                Assert.IsNull(SpectraWindowIndex.BuildFromCache(corruptPath, null, out var corruptReason, descriptor));
                Assert.AreEqual(SpectraCacheRejection.TruncatedHeader, corruptReason);
                File.WriteAllBytes(corruptPath, bytes.Take(DESCRIPTOR_LENGTH_OFFSET + 4 + 5).ToArray());
                Assert.AreEqual(SpectraCacheRejection.TruncatedHeader, SpectraCache.CheckHeader(corruptPath, null, descriptor));

                // The start-up check of an input whose source and .spectra.bin are gone: its
                // demultiplexed cache stands in while it matches the current settings, and is
                // refused, with the reason, once a demux version or override has changed.
                var auto = new OspreyConfig { DemuxMode = DemuxMode.auto };
                string goneSource = Path.Combine(dir, @"run.raw");
                Assert.AreEqual(demuxPath, SpectraCache.GetDemuxCachePath(goneSource));
                Assert.AreEqual(SpectraCacheRejection.None, DemuxCacheBuilder.CheckDemuxCache(goneSource, auto));
                SpectraCache.SaveSpectraCache(demuxPath, result.Spectra, ms1, null, otherDescriptor);
                Assert.AreEqual(SpectraCacheRejection.DemuxSettingsChanged,
                    DemuxCacheBuilder.CheckDemuxCache(goneSource, auto));
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

            // The task validity key carries the settings descriptor the search hash cannot see,
            // so a new algorithm version or an override re-scores along with the rebuilt cache.
            Assert.AreEqual(string.Empty, DemuxCacheBuilder.ValidityKeySuffix(config));
            config.DemuxMode = DemuxMode.auto;
            StringAssert.Contains(DemuxCacheBuilder.ValidityKeySuffix(config), new DemuxParams().Descriptor);
            Assert.AreNotEqual(DemuxCacheBuilder.ValidityKeySuffix(DemuxMode.auto, new DemuxParams().Descriptor),
                DemuxCacheBuilder.ValidityKeySuffix(DemuxMode.auto,
                    new DemuxParams { Interpolation = RtInterpolation.natural_three_point }.Descriptor));
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
        /// The k=2, 8 Th staggered run the exactness tests use: constant elution, exact m/z, and
        /// the fragment shared by four consecutive bins.
        /// </summary>
        private static SyntheticRun BuildStaggeredRun(int cycles)
        {
            return BuildRun(new RunOptions
            {
                Cycle = StaggeredCycle(RANGE_LOW, RANGE_HIGH, WINDOW_WIDTH, 2),
                Cycles = cycles,
            });
        }

        /// <summary>
        /// A synthetic run re-multiplexed from narrow-bin truth. Each bin holds one precursor with
        /// three unique fragments, optionally plus the fragment shared by four consecutive bins.
        /// With a non-zero elution width every bin elutes as a Gaussian, with apexes offset from
        /// bin to bin; with jitter every acquired peak's m/z is perturbed, as real centroids are.
        /// </summary>
        private static SyntheticRun BuildRun(RunOptions options)
        {
            var cycle = options.Cycle;
            var bins = DemuxSchemeDetector.Detect(cycle).Bins;
            var truth = new Dictionary<double, float>[bins.Count];
            for (int j = 0; j < bins.Count; j++)
            {
                truth[j] = new Dictionary<double, float>();
                foreach (double offset in new[] { 0.0, 5.13, 11.71 })
                    truth[j][Math.Round(300 + 37.1 * j + offset, 4)] = 1000f * (j + 1) + (float)(offset * 10);
            }
            if (options.SharedFragment)
            {
                for (int i = 0; i < SHARED_FRAGMENT_BINS.Length; i++)
                    truth[SHARED_FRAGMENT_BINS[i]][SHARED_FRAGMENT_MZ] = SHARED_FRAGMENT_INTENSITIES[i];
            }

            double cycleMinutes = cycle.Count * SCAN_MINUTES;
            double middle = options.Cycles * cycleMinutes / 2;
            double sigma = options.ElutionSigmaCycles * cycleMinutes;
            Func<int, double, double> profile = (j, t) =>
            {
                if (sigma <= 0)
                    return 1.0;
                double apex = middle + ((j % 3) - 1) * 1.5 * cycleMinutes;
                double value = Math.Exp(-0.5 * Math.Pow((t - apex) / sigma, 2));
                return value < 1e-4 ? 0 : value;
            };

            var random = new Random(options.Seed);
            var spectra = new List<Spectrum>();
            uint scan = 0;
            for (int c = 0; c < options.Cycles; c++)
            {
                foreach (var window in cycle)
                {
                    double t = scan * SCAN_MINUTES;
                    var peaks = new SortedDictionary<double, double>();
                    for (int j = 0; j < bins.Count; j++)
                    {
                        if (bins[j].Center <= window.LowerBound || bins[j].Center >= window.UpperBound)
                            continue;
                        double level = profile(j, t);
                        foreach (var peak in truth[j])
                        {
                            peaks.TryGetValue(peak.Key, out double existing);
                            peaks[peak.Key] = existing + peak.Value * level;
                        }
                    }
                    var mzs = new List<double>();
                    var intensities = new List<float>();
                    foreach (var peak in peaks)
                    {
                        if (peak.Value <= 0)
                            continue;
                        double jitter = (2 * random.NextDouble() - 1) * options.JitterPpm * 1e-6;
                        mzs.Add(peak.Key * (1 + jitter));
                        intensities.Add((float)peak.Value);
                    }
                    spectra.Add(MakeSpectrum(scan, t, window, mzs.ToArray(), intensities.ToArray()));
                    scan++;
                }
            }
            return new SyntheticRun(spectra, bins, truth, profile);
        }

        private static double MaxRelativeError(DemuxResult result, SyntheticRun run,
            out int missing, out int spurious)
        {
            var errors = MeasureErrors(result, run);
            missing = errors.Missing;
            spurious = errors.Spurious;
            return errors.Max;
        }

        /// <summary>
        /// Error of every output peak against its bin's truth at the parent's time, as a fraction
        /// of that fragment's apex intensity. Output peaks are matched to truth fragments within
        /// 10 ppm, since they carry the parent's (possibly jittered) m/z. Also counts truth peaks
        /// above 0.1% of apex that an output spectrum lacks, and output peaks with no truth.
        /// </summary>
        private static RunErrors MeasureErrors(DemuxResult result, SyntheticRun run)
        {
            var errors = new List<double>();
            int missing = 0, spurious = 0;
            double spuriousIntensity = 0, totalIntensity = 0;
            foreach (var spectrum in result.Spectra)
            {
                int bin = BinOf(run.Bins, spectrum.IsolationWindow.Center);
                double level = run.Profile(bin, spectrum.RetentionTime);
                var expected = run.Truth[bin];
                var seen = new HashSet<double>();
                for (int p = 0; p < spectrum.Mzs.Length; p++)
                {
                    totalIntensity += spectrum.Intensities[p];
                    double nominal = expected.Keys.FirstOrDefault(mz =>
                        Math.Abs(mz - spectrum.Mzs[p]) <= mz * 10e-6);
                    if (nominal == 0)
                    {
                        spurious++;
                        spuriousIntensity += spectrum.Intensities[p];
                        continue;
                    }
                    seen.Add(nominal);
                    double apex = expected[nominal];
                    errors.Add(Math.Abs(spectrum.Intensities[p] - apex * level) / apex);
                }
                foreach (var peak in expected)
                {
                    if (!seen.Contains(peak.Key) && level >= 1e-3)
                    {
                        missing++;
                        errors.Add(1.0);
                    }
                }
            }
            errors.Sort();
            return new RunErrors
            {
                Max = errors.Count > 0 ? errors[errors.Count - 1] : 0,
                Median = errors.Count > 0 ? errors[errors.Count / 2] : 0,
                Missing = missing,
                Spurious = spurious,
                SpuriousFraction = totalIntensity > 0 ? spuriousIntensity / totalIntensity : 0,
            };
        }

        /// <summary>
        /// Constant elution: both output modes recover the truth exactly, with no peak missing or
        /// invented, and each parent yields <paramref name="binsPerSpectrum"/> spectra (0 skips
        /// that count, for layouts where it varies by window).
        /// </summary>
        private static void AssertExact(string name, SyntheticRun run, int binsPerSpectrum)
        {
            foreach (var outputMode in new[] { DemuxOutputMode.apportioned, DemuxOutputMode.solution })
            {
                string label = string.Format(@"{0}, {1}", name, outputMode);
                var result = Demultiplexer.Demultiplex(run.Spectra, new DemuxParams { OutputMode = outputMode });
                if (binsPerSpectrum > 0)
                    Assert.AreEqual(binsPerSpectrum * run.Spectra.Count, result.Spectra.Count, label);
                var errors = MeasureErrors(result, run);
                Assert.IsTrue(errors.Max < 1e-5, string.Format(@"{0}: max relative error {1}", label, errors.Max));
                Assert.AreEqual(0, errors.Missing, label + @": missing");
                Assert.AreEqual(0, errors.Spurious, label + @": spurious");
            }
        }

        private static PipelineContext WiringContext(DemuxMode mode)
        {
            var config = new OspreyConfig { DemuxMode = mode, NThreads = 2 };
            return new PipelineContext(config, new OspreyTask[0], null, null, null);
        }

        /// <summary>
        /// Per-bin summary of a demultiplexing (spectra, peaks, summed intensity) against the
        /// committed golden. With the rebless variable set, rewrites the golden and fails, so a
        /// changed output is never accepted silently.
        /// </summary>
        private static void CheckGolden(string path, DemuxResult result)
        {
            var ic = CultureInfo.InvariantCulture;
            const string tab = "\t";
            var lines = new List<string> { string.Join(tab, @"bin_lower", @"bin_upper", @"spectra", @"peaks", @"intensity") };
            foreach (var group in result.Spectra.GroupBy(s => s.IsolationWindow.LowerBound).OrderBy(g => g.Key))
            {
                var first = group.First().IsolationWindow;
                lines.Add(string.Join(tab,
                    first.LowerBound.ToString(@"F4", ic), first.UpperBound.ToString(@"F4", ic),
                    group.Count().ToString(ic), group.Sum(s => s.Mzs.Length).ToString(ic),
                    group.Sum(s => s.Intensities.Sum(v => (double)v)).ToString(@"R", ic)));
            }
            if (Environment.GetEnvironmentVariable(REBLESS_VARIABLE) == @"1")
            {
                File.WriteAllLines(path, lines);
                Assert.Fail(@"Reblessed {0} ({1} was set); review the diff and commit it.",
                    path, REBLESS_VARIABLE);
            }
            Assert.IsTrue(File.Exists(path), path);
            var expected = File.ReadAllLines(path).Where(l => l.Length > 0).ToArray();
            Assert.AreEqual(expected.Length, lines.Count, @"bin count");
            for (int i = 1; i < lines.Count; i++)
            {
                var want = expected[i].Split('\t');
                var have = lines[i].Split('\t');
                for (int f = 0; f < 4; f++)
                    Assert.AreEqual(want[f], have[f], string.Format(@"bin {0} field {1}", i, f));
                double wantIntensity = double.Parse(want[4], ic);
                double haveIntensity = double.Parse(have[4], ic);
                Assert.AreEqual(wantIntensity, haveIntensity, Math.Abs(wantIntensity) * 1e-9,
                    string.Format(@"bin {0} intensity", i));
            }
        }

        /// <summary>
        /// Pairs two demultiplexings by parent retention time and nearest bin center, and compares
        /// each pair's peaks (matched within 1 ppm) by cosine similarity over their union.
        /// </summary>
        private static Agreement MeasureAgreement(IReadOnlyList<Spectrum> reference, IReadOnlyList<Spectrum> test)
        {
            var byTime = test.GroupBy(s => Math.Round(s.RetentionTime, 6)).ToDictionary(g => g.Key, g => g.ToList());
            var cosines = new List<double>();
            double referenceTotal = 0, testTotal = 0;
            foreach (var spectrum in reference)
            {
                if (!byTime.TryGetValue(Math.Round(spectrum.RetentionTime, 6), out var candidates))
                    continue;
                var partner = candidates.OrderBy(s => Math.Abs(s.IsolationWindow.Center - spectrum.IsolationWindow.Center)).First();
                if (Math.Abs(partner.IsolationWindow.Center - spectrum.IsolationWindow.Center) > 0.5)
                    continue;
                cosines.Add(Cosine(spectrum, partner));
                referenceTotal += spectrum.Intensities.Sum(v => (double)v);
                testTotal += partner.Intensities.Sum(v => (double)v);
            }
            cosines.Sort();
            return new Agreement
            {
                Paired = cosines.Count,
                MedianCosine = cosines.Count > 0 ? cosines[cosines.Count / 2] : 0,
                FractionAbove95 = cosines.Count > 0 ? cosines.Count(c => c >= 0.95) / (double)cosines.Count : 0,
                IntensityRatio = referenceTotal > 0 ? testTotal / referenceTotal : 0,
            };
        }

        private static double Cosine(Spectrum a, Spectrum b)
        {
            double dot = 0, aa = 0, bb = 0;
            int j = 0;
            for (int i = 0; i < a.Mzs.Length; i++)
            {
                double x = a.Intensities[i];
                aa += x * x;
                while (j < b.Mzs.Length && b.Mzs[j] < a.Mzs[i] * (1 - 1e-6))
                    j++;
                if (j < b.Mzs.Length && Math.Abs(b.Mzs[j] - a.Mzs[i]) <= a.Mzs[i] * 1e-6)
                    dot += x * b.Intensities[j];
            }
            foreach (float y in b.Intensities)
                bb += (double)y * y;
            if (aa == 0 && bb == 0)
                return 1;
            return aa > 0 && bb > 0 ? dot / Math.Sqrt(aa * bb) : 0;
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

        private sealed class RunOptions
        {
            public List<IsolationWindow> Cycle { get; set; }
            public int Cycles { get; set; } = 20;
            public bool SharedFragment { get; set; } = true;

            /// <summary>Gaussian elution width in cycles; 0 means constant elution.</summary>
            public double ElutionSigmaCycles { get; set; }

            /// <summary>Uniform per-peak m/z jitter, +/- this many ppm.</summary>
            public double JitterPpm { get; set; }

            public int Seed { get; set; } = 7;
        }

        private sealed class Agreement
        {
            public int Paired { get; set; }
            public double MedianCosine { get; set; }
            public double FractionAbove95 { get; set; }
            public double IntensityRatio { get; set; }
        }

        private sealed class RunErrors
        {
            public double Max { get; set; }
            public double Median { get; set; }
            public int Missing { get; set; }
            public int Spurious { get; set; }

            /// <summary>Intensity in peaks placed in a bin that has no such fragment, of all output.</summary>
            public double SpuriousFraction { get; set; }
        }

        private sealed class SyntheticRun
        {
            public SyntheticRun(List<Spectrum> spectra, IReadOnlyList<DemuxBin> bins,
                Dictionary<double, float>[] truth, Func<int, double, double> profile)
            {
                Spectra = spectra;
                Bins = bins;
                Truth = truth;
                Profile = profile;
            }

            public List<Spectrum> Spectra { get; }
            public IReadOnlyList<DemuxBin> Bins { get; }

            /// <summary>Per bin: fragment m/z -> intensity at the elution apex.</summary>
            public Dictionary<double, float>[] Truth { get; }

            /// <summary>Elution level of a bin at a time, 1 at its apex.</summary>
            public Func<int, double, double> Profile { get; }
        }
    }
}
