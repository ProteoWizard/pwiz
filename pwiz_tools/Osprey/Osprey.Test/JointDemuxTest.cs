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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Demux;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for <see cref="TofGrid"/>, <see cref="IonCalibration"/> and <see cref="JointDemultiplexer"/>, the
    /// last against profile sweeps simulated through the solve's own model: the trapezoid transmission of
    /// <see cref="ScanningDemuxTest"/> over 60 encoded bins 1.18 Th apart, and a Gaussian TOF peak on a
    /// ZenoTOF-like grid.
    /// </summary>
    [TestClass]
    public class JointDemuxTest
    {
        private const double STEP = 1.18;
        private const double FIRST_CENTER = 500.0;
        private const int BINS = 60;
        private const int CYCLES = 9;
        private const double ROOT_STEP = 9.786595e-05;

        /// <summary>
        /// The grid of a profile spectrum: found from its points, with or without its zero samples; a
        /// sample's index and m/z round-trip; and points off one uniform grid give no grid.
        /// </summary>
        [TestMethod]
        public void TestTofGrid()
        {
            var truth = new TofGrid(Math.Sqrt(400.0) - 3 * ROOT_STEP, ROOT_STEP);
            var all = Enumerable.Range(0, 2000).Select(k => truth.Mz(k)).ToList();
            var grid = TofGrid.Detect(all);
            Assert.IsNotNull(grid);
            Assert.AreEqual(ROOT_STEP, grid.Step, 1e-12);
            // Only every third run of samples present, as in a spectrum with its zero points dropped.
            var sparse = Enumerable.Range(0, 6000).Where(k => k % 30 < 8).Select(k => truth.Mz(k)).ToList();
            var fromSparse = TofGrid.Detect(sparse);
            Assert.IsNotNull(fromSparse);
            Assert.AreEqual(ROOT_STEP, fromSparse.Step, 1e-12);
            for (int k = 0; k < 6000; k += 997)
            {
                Assert.AreEqual(k, fromSparse.Index(truth.Mz(k)));
                Assert.AreEqual(truth.Mz(k), fromSparse.Mz(k), 1e-9);
            }
            var shifted = sparse.Select((mz, i) => i == 100 ? truth.Mz(100.3) : mz).ToList();
            Assert.IsNull(TofGrid.Detect(shifted));
        }

        /// <summary>
        /// Counts per ion from a profile spectrum's lowest levels: whole numbers of ions of q counts, rounded to
        /// integers, give q back at an MS2-like and an MS1-like scale, with every low point on a multiple;
        /// centroid-like continuous intensities do not fit; too few points give no estimate.
        /// </summary>
        [TestMethod]
        public void TestIonCalibration()
        {
            var random = new Random(3);
            foreach (double q in new[] { 99.665, 7.27 })
            {
                // Mostly one or two ions, a few strong peaks, and zeros between them.
                var intensities = Enumerable.Range(0, 5000).Select(i =>
                {
                    int ions = i % 7 == 0 ? 0 : 1 + (int)Math.Floor(-Math.Log(1 - random.NextDouble()) * 2);
                    if (i % 97 == 0)
                        ions += 500;
                    return Math.Round(ions * q);
                }).ToList();
                double found = IonCalibration.CountsPerIon(intensities, out double fit);
                Assert.AreEqual(q, found, 0.002 * q, string.Format(@"counts per ion {0}", q));
                Assert.AreEqual(1.0, fit, 1e-12, string.Format(@"fit at {0}", q));
            }
            var continuous = Enumerable.Range(0, 5000).Select(i => 1 + 10000 * random.NextDouble()).ToList();
            IonCalibration.CountsPerIon(continuous, out double continuousFit);
            Assert.IsTrue(continuousFit < IonCalibration.MIN_FIT, @"continuous intensities fit");
            Assert.IsTrue(double.IsNaN(IonCalibration.CountsPerIon(new[] { 100.0, 200.0 }, out _)));
        }

        /// <summary>
        /// The TOF peak's core width from a profile spectrum: exact Gaussian peaks of known sigma, their tops a
        /// random fraction of a sample off the grid, give it back through the log-parabola of their top three
        /// samples; a peak with a close neighbour is not isolated and is left out; the calibration takes each m/z
        /// bin's median.
        /// </summary>
        [TestMethod]
        public void TestTofPeakWidth()
        {
            var grid = new TofGrid(Math.Sqrt(300.0), ROOT_STEP);
            var random = new Random(7);
            var centers = new List<double>();
            for (long k = 2000; k < 120000; k += 400)
                centers.Add(k + random.NextDouble() - 0.5);
            // A second peak 4 samples after one: the pair is not isolated.
            centers.Add(centers[10] + 4);
            double SigmaOf(double mz) => mz < 600 ? 0.9 : 1.3;
            var points = new SortedDictionary<long, double>();
            foreach (double c in centers)
            {
                double sigma = SigmaOf(grid.Mz(c));
                for (long k = (long)Math.Round(c) - 7; k <= (long)Math.Round(c) + 7; k++)
                {
                    points.TryGetValue(k, out double v);
                    points[k] = v + 1000 * Math.Exp(-0.5 * (k - c) * (k - c) / (sigma * sigma));
                }
            }
            var mz = points.Keys.Select(k => grid.Mz(k)).ToList();
            var intensity = points.Values.ToList();
            var peaks = TofPeakWidth.Measure(mz, intensity, grid, 100);
            Assert.AreEqual(centers.Count - 2, peaks.Count);
            foreach (var peak in peaks)
                Assert.AreEqual(SigmaOf(peak.Mz), peak.Sigma, 1e-6, string.Format(@"sigma at {0:F1}", peak.Mz));
            var table = TofPeakWidth.Calibrate(peaks, new[] { 300.0, 600.0, 1000.0 }, 20);
            Assert.IsTrue(table.HasValue);
            CollectionAssert.AreEqual(new[] { 0.9, 1.3 }, table.Value.Sigma.Select(s => Math.Round(s, 6)).ToArray());
            Assert.IsNull(TofPeakWidth.Calibrate(peaks, new[] { 300.0, 600.0, 1000.0 }, 1000));
        }

        /// <summary>
        /// The joint solve with one bin and one position, as it centroids MS1: noiseless peaks on the grid come
        /// back one centroid each, at their own m/z and intensity.
        /// </summary>
        [TestMethod]
        public void TestJointCentroidsOneBin()
        {
            var grid = new TofGrid(Math.Sqrt(400.0), ROOT_STEP);
            var parameters = new JointDemuxParams { L1Z = 0, ChunkSamples = 256 };
            int half = parameters.PeakHalfWidth;
            var sources = new[] { (Sample: 5000L, Ions: 500.0), (Sample: 5030L, Ions: 80.0), (Sample: 9000L, Ions: 30.0) };
            var mz = new List<double>();
            var ions = new List<double>();
            foreach (var s in sources)
            {
                for (int d = -half; d <= half; d++)
                {
                    mz.Add(grid.Mz(s.Sample + d));
                    ions.Add(s.Ions * Peak(parameters.SigmaAt(grid.Mz(s.Sample)), d, half));
                }
            }
            var unit = new ScanningUnit(new double[,] { { 1 } }, new[] { 0 }, new[] { 0 }, new[] { 0 }, 0, 0, 0, 0)
            {
                Mz = mz.ToArray(),
                Ions = ions.ToArray(),
                Row = new int[mz.Count],
                Cycle = new int[mz.Count],
            };
            var result = JointDemultiplexer.DemuxUnit(unit, parameters, grid);
            Assert.AreEqual(sources.Length, result.Demultiplexed.Count);
            foreach (var s in sources)
            {
                var peak = result.Demultiplexed.Single(p => Math.Abs(grid.Position(p.Mz) - s.Sample) < 1);
                Assert.AreEqual(s.Ions, peak.Ions, 0.005 * s.Ions, string.Format(@"ions at sample {0}", s.Sample));
                Assert.AreEqual(s.Sample, grid.Position(peak.Mz), 0.05, string.Format(@"position of sample {0}", s.Sample));
            }
        }

        /// <summary>
        /// Demultiplexing and centroiding profile sweeps in one solve: noiseless, every fragment returns to
        /// its precursor's position at its own m/z and intensity, with and without the relaxed lasso; two
        /// fragments of different precursors two samples apart, within one TOF peak, keep their own
        /// positions, m/z and intensities; with counting noise, the three positions centered on each
        /// precursor hold its intensity; and the result is the same twice.
        /// </summary>
        [TestMethod]
        public void TestJointDemuxRecovers()
        {
            var centers = Enumerable.Range(0, BINS).Select(b => FIRST_CENTER + b * STEP).ToArray();
            var a = ScanningDemultiplexer.TransmissionMatrix(ScanningDemuxTest.TrapezoidKernel(), centers, centers, STEP);
            var grid = new TofGrid(Math.Sqrt(300.0), ROOT_STEP);
            long Sample(double mz) => grid.Index(mz);
            var sources = new[]
            {
                (Bin: 20, Sample: Sample(412.2345), Amount: 1000.0),
                (Bin: 20, Sample: Sample(655.4321), Amount: 400.0),
                (Bin: 34, Sample: Sample(520.3456), Amount: 800.0),
                (Bin: 28, Sample: Sample(733.1111), Amount: 500.0),
            };
            var exactParams = new JointDemuxParams { L1Z = 0, ChunkSamples = 256 };
            AssertRecovered(Demux(a, grid, sources, null, exactParams), grid, sources, 0.01, @"unpenalized");
            var relaxedParams = new JointDemuxParams { L1Z = 2, Relaxed = true, ChunkSamples = 256 };
            AssertRecovered(Demux(a, grid, sources, null, relaxedParams), grid, sources, 0.01, @"relaxed lasso");

            // Near-isobaric: two precursors' fragments two samples apart (about 15 ppm), inside one peak.
            long shared = Sample(610.5);
            var close = new[] { (Bin: 20, Sample: shared, Amount: 1000.0), (Bin: 34, Sample: shared + 2, Amount: 600.0) };
            AssertRecovered(Demux(a, grid, close, null, exactParams), grid, close, 0.02, @"near-isobaric");

            var noisyParams = new JointDemuxParams { ChunkSamples = 256 };
            var noisy = Demux(a, grid, sources, new Random(5), noisyParams);
            foreach (var s in sources)
            {
                double expected = s.Amount * ScanningDemuxTest.TotalElution();
                double found = Enumerable.Range(s.Bin - 1, 3).Sum(bin => Sum(noisy.Demultiplexed, bin, grid, s.Sample, 3));
                Assert.AreEqual(expected, found, 0.15 * expected, string.Format(@"noisy, sample {0}, 3 bins", s.Sample));
            }
            var again = Demux(a, grid, sources, new Random(5), noisyParams);
            CollectionAssert.AreEqual(noisy.Demultiplexed, again.Demultiplexed);
        }

        /// <summary>
        /// Each source's intensity, over the core sweeps, in its own position within half a sample of its
        /// m/z, within a relative tolerance; and each centroid there within 0.2 samples of the source.
        /// </summary>
        private static void AssertRecovered(ScanningUnitResult result, TofGrid grid,
            (int Bin, long Sample, double Amount)[] sources, double tolerance, string label)
        {
            foreach (var s in sources)
            {
                double expected = s.Amount * ScanningDemuxTest.TotalElution();
                Assert.AreEqual(expected, Sum(result.Demultiplexed, s.Bin, grid, s.Sample, 3), tolerance * expected,
                    string.Format(@"{0}, bin {1}, sample {2}", label, s.Bin, s.Sample));
                foreach (var peak in result.Demultiplexed.Where(p => p.Bin == s.Bin && Math.Abs(grid.Position(p.Mz) - s.Sample) < 3))
                    Assert.AreEqual(s.Sample, grid.Position(peak.Mz), 0.2, string.Format(@"{0}, bin {1} centroid", label, s.Bin));
            }
        }

        private static ScanningUnitResult Demux(double[,] a, TofGrid grid, (int Bin, long Sample, double Amount)[] sources,
            Random noise, JointDemuxParams parameters)
        {
            return JointDemultiplexer.DemuxUnit(SimulateProfile(a, grid, sources, noise, parameters), parameters, grid);
        }

        /// <summary>
        /// One block of <see cref="BINS"/> bins by <see cref="CYCLES"/> sweeps, the core the middle 40 bins and
        /// sweeps 2 to 6: each source's fragment transmitted through its bin's column of <paramref name="a"/>,
        /// spread over the grid by the solve's own TOF peak, with the elution of <see cref="ScanningDemuxTest"/>,
        /// and Poisson counts when <paramref name="noise"/> is given.
        /// </summary>
        private static ScanningUnit SimulateProfile(double[,] a, TofGrid grid, (int Bin, long Sample, double Amount)[] sources,
            Random noise, JointDemuxParams parameters)
        {
            int half = parameters.PeakHalfWidth;
            var mz = new List<double>();
            var ions = new List<double>();
            var row = new List<int>();
            var cycle = new List<int>();
            var samples = sources.SelectMany(s => Enumerable.Range((int)(s.Sample - half), 2 * half + 1).Select(k => (long)k))
                .Distinct().OrderBy(k => k).ToList();
            for (int c = 0; c < CYCLES; c++)
            {
                for (int r = 0; r < BINS; r++)
                {
                    foreach (long k in samples)
                    {
                        double expected = 0;
                        foreach (var s in sources)
                        {
                            long d = k - s.Sample;
                            if (Math.Abs(d) <= half)
                                expected += s.Amount * a[r, s.Bin] * Peak(parameters.SigmaAt(grid.Mz(s.Sample)), (int)d, half);
                        }
                        expected *= ScanningDemuxTest.Elution(c);
                        double value = noise == null ? expected : ScanningDemuxTest.Poisson(noise, expected);
                        if (value <= 0)
                            continue;
                        mz.Add(grid.Mz(k));
                        ions.Add(value);
                        row.Add(r);
                        cycle.Add(c);
                    }
                }
            }
            var bins = Enumerable.Range(0, BINS).ToArray();
            return new ScanningUnit(a, bins, bins, Enumerable.Range(0, CYCLES).ToArray(), 10, 49, 2, 6)
            {
                Mz = mz.ToArray(),
                Ions = ions.ToArray(),
                Row = row.ToArray(),
                Cycle = cycle.ToArray(),
            };
        }

        /// <summary>The Gaussian TOF peak at offset d, unit area over its support of +/- half samples.</summary>
        private static double Peak(double sigma, int d, int half)
        {
            double sum = 0;
            for (int e = -half; e <= half; e++)
                sum += Math.Exp(-0.5 * e * e / (sigma * sigma));
            return Math.Exp(-0.5 * d * d / (sigma * sigma)) / sum;
        }

        /// <summary>The ions of a bin's peaks within a number of grid samples of a sample.</summary>
        private static double Sum(IEnumerable<ScanningPeak> peaks, int bin, TofGrid grid, long sample, double withinSamples)
        {
            return peaks.Where(p => p.Bin == bin && Math.Abs(grid.Position(p.Mz) - sample) <= withinSamples).Sum(p => p.Ions);
        }
    }
}
