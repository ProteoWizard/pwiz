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

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for the staggered joint solve on linear ion trap profile data (<see cref="StaggeredDemuxPipeline"/> with
    /// <see cref="ScanningDemuxOptions.Joint"/>), the Thermo Stellar path: a simulated acquisition of 4 Th windows in two
    /// offset passes over 400-440 m/z (20 bins of 2 Th), its profile on the Stellar's 0.2 Th grid with zeros, each bin's
    /// precursor three Gaussian fragments eluting over several passes, intensities in ions per second as an ion trap
    /// reports them, and fill times that vary 100-fold between scans.
    /// </summary>
    [TestClass]
    public class StaggeredJointDemuxTest
    {
        private const double SPACING = 0.2;
        private const double SIGMA = 0.33;
        private const double GRID_START = 300;
        private const int GRID_POINTS = 2251;
        private const double FIRST_EDGE = 400;
        private const double WINDOW_WIDTH = 4;
        private const double BIN_WIDTH = 2;
        private const int BINS = 20;
        private const int CYCLES = 30;
        private const double MINUTES_PER_SPECTRUM = 0.001;
        private const double UNIFORM_FILL_MS = 5;

        /// <summary>
        /// Each output spectrum of an interior bin holds its own precursor's fragments, at their m/z and rates, and not
        /// its neighbors', with fill times varying 100-fold between scans. Measured: m/z within 0.0005 Th, rates within
        /// 1.2% (median) and 1.8% (worst) on 819 fragments, no crosstalk; the bounds are about twice that.
        /// </summary>
        [TestMethod]
        public void TestStaggeredJointRecovers()
        {
            var source = new StaggeredProfileRun(true, -1);
            var pipeline = new StaggeredDemuxPipeline(source, Options(), TextWriter.Null);
            var rateErrors = new List<double>();
            double worstMz = 0, worstCrosstalk = 0;
            foreach (int output in DemultiplexedOutputs(pipeline, source))
            {
                int bin = BinIndex(pipeline.BinOf(output));
                double time = source.TimeOf(pipeline.BaseIndex(output));
                var (mz, rates) = Rates(pipeline, output);
                foreach (var fragment in StaggeredProfileRun.Fragments(bin))
                {
                    double truth = StaggeredProfileRun.Area(fragment.Height * StaggeredProfileRun.Elution(bin, time));
                    if (truth < 0.1 * StaggeredProfileRun.Area(fragment.Height))
                        continue;
                    int nearest = Nearest(mz, fragment.Mz);
                    Assert.IsTrue(nearest >= 0, string.Format(@"bin {0}: no centroid near {1}", bin, fragment.Mz));
                    worstMz = Math.Max(worstMz, Math.Abs(mz[nearest] - fragment.Mz));
                    rateErrors.Add(Math.Abs(rates[nearest] / truth - 1));
                }
                // Another bin's fragment written into this one: its rate against the fragment's own.
                for (int other = 0; other < BINS; other++)
                {
                    if (other == bin)
                        continue;
                    foreach (var fragment in StaggeredProfileRun.Fragments(other))
                    {
                        int nearest = Nearest(mz, fragment.Mz);
                        double own = StaggeredProfileRun.Area(fragment.Height * StaggeredProfileRun.Elution(other, time));
                        if (nearest >= 0 && own > 0.1 * StaggeredProfileRun.Area(fragment.Height))
                            worstCrosstalk = Math.Max(worstCrosstalk, rates[nearest] / own);
                    }
                }
            }
            rateErrors.Sort(); // Array.Sort OK: equal doubles are indistinguishable
            double medianRate = rateErrors[rateErrors.Count / 2], worstRate = rateErrors[rateErrors.Count - 1];
            Console.WriteLine(@"{0} fragments: worst m/z error {1:F4} Th; rate error median {2:P2}, worst {3:P2}; crosstalk {4:P2}",
                rateErrors.Count, worstMz, medianRate, worstRate, worstCrosstalk);
            Assert.IsTrue(rateErrors.Count > 500, rateErrors.Count.ToString());
            Assert.IsTrue(worstMz < 0.001, worstMz.ToString(@"F4"));
            Assert.IsTrue(medianRate < 0.025, medianRate.ToString(@"P2"));
            Assert.IsTrue(worstRate < 0.04, worstRate.ToString(@"P2"));
            Assert.IsTrue(worstCrosstalk < 0.02, worstCrosstalk.ToString(@"P2"));
        }

        /// <summary>
        /// Spectra are combined in ions per second: the same rates acquired at fill times varying 100-fold give the
        /// output a constant fill time gives. Without the lasso penalty and the Poisson weights, which rightly depend on
        /// the ions counted, so differ with the fill times, and compared as the
        /// apportioned profile, before Centrix, whose split-or-merge decisions can turn on a small difference. Interior
        /// bins only: a bin at either end of the range is seen through one window, so its split with its neighbor is
        /// not determined and the solve's choice follows its path, which the scale changes.
        /// <para>What remains is not exact: 4.6e-4 of the largest rate, all in the tails of elutions, where which
        /// coefficients the solve takes up, and the equal split of a sample no bin is fitted at, are discrete choices
        /// the scale can tip. Combining spectra in ions instead would err at the peaks by the ratio of fill times, here
        /// 0.04 to 4.</para>
        /// <para>The same with the staggered defaults, each pass started from the previous pass's solve.</para>
        /// </summary>
        [TestMethod]
        public void TestStaggeredJointFillTimeInvariance()
        {
            AssertSameInteriorRates(Demultiplex(new StaggeredProfileRun(false, -1), Unweighted()),
                Demultiplex(new StaggeredProfileRun(true, -1), Unweighted()));
            // The staggered defaults, each pass started from the previous pass's solve carried to its own scale.
            AssertSameInteriorRates(Demultiplex(new StaggeredProfileRun(false, -1), Unweighted(true)),
                Demultiplex(new StaggeredProfileRun(true, -1), Unweighted(true)));
        }

        /// <summary>
        /// The per-channel solve (makima interpolation of each window's channels, not the joint solve) combines spectra
        /// in ions per second too: the same rates at fill times varying 100-fold give the same output, its profile
        /// channels matched within 0.09 Th as on the Stellar.
        /// </summary>
        [TestMethod]
        public void TestStaggeredChannelFillTimeInvariance()
        {
            // Without the baseline, which only the joint solve needs (its grid is found from a spectrum's points), and
            // which the per-channel solve would take as thousands of channels.
            AssertSameInteriorRates(Demultiplex(new StaggeredProfileRun(false, -1, false), Channels()),
                Demultiplex(new StaggeredProfileRun(true, -1, false), Channels()));
        }

        /// <summary>
        /// One output spectrum per bin of every acquired spectrum, in order, whatever it holds: an acquired spectrum of
        /// zeros gives empty spectra in place, not missing ones. And the output does not depend on the thread count,
        /// also for a run that starts mid-pass, whose passes the window-repeat rule alone would split between cycles
        /// and so between two blocks of one batch (at 4 threads, not at 1).
        /// </summary>
        [TestMethod]
        public void TestStaggeredJointThreadsAndEmptySpectra()
        {
            var source = new StaggeredProfileRun(true, StaggeredProfileRun.SpectrumIndex(12, 0, 5));
            var single = Options();
            single.Threads = 1;
            var many = Options();
            many.Threads = 4;
            var one = Demultiplex(source, single);
            Assert.AreEqual(CYCLES + CYCLES * StaggeredProfileRun.MS2_PER_CYCLE * 2, one.Count);
            AssertSameOutput(one, Demultiplex(source, many));
            var midPass = new SkipFirst(source, StaggeredProfileRun.SpectrumIndex(0, 0, 5));
            AssertSameOutput(Demultiplex(midPass, single), Demultiplex(midPass, many));
            // With the staggered defaults, each pass started from the previous one's solve within its block.
            single.JointParameters.UseStaggeredDefaults();
            many.JointParameters.UseStaggeredDefaults();
            AssertSameOutput(Demultiplex(source, single), Demultiplex(source, many));

            var pipeline = new StaggeredDemuxPipeline(source, single, TextWriter.Null);
            var ofEmpty = Enumerable.Range(0, pipeline.OutputCount).Where(o => pipeline.BaseIndex(o) == source.EmptyIndex).ToArray();
            Assert.AreEqual(2, ofEmpty.Length);
            foreach (int output in ofEmpty)
                Assert.AreEqual(0, pipeline.Peaks(output).Mz.Length, string.Format(@"output {0}", output));
            for (int output = 1; output < pipeline.OutputCount; output++)
                Assert.IsTrue(pipeline.BaseIndex(output) >= pipeline.BaseIndex(output - 1), string.Format(@"output {0}", output));
        }

        /// <summary>
        /// The ion response: each analyzer's alpha from Table 1 of Hsu et al., J. Proteome Res. 2025, 24, 5742-5754
        /// (doi:10.1021/acs.jproteome.5c00593), found from its PSI-MS term names; one ion's intensity units from alpha
        /// and a fill time; each output spectrum's ion scale from its acquired spectrum's alpha and fill time, or the
        /// default alpha when the source names no analyzer; and exposures interpolated linearly between acquisitions.
        /// </summary>
        [TestMethod]
        public void TestIonResponse()
        {
            Assert.AreEqual(MassAnalyzerKind.astral, IonResponse.KindOf(@"quadrupole asymmetric track lossless time-of-flight analyzer"));
            Assert.AreEqual(MassAnalyzerKind.orbitrap, IonResponse.KindOf(@"quadrupole orbitrap"));
            Assert.AreEqual(MassAnalyzerKind.ion_trap, IonResponse.KindOf(@"quadrupole radial ejection linear ion trap"));
            Assert.AreEqual(MassAnalyzerKind.unknown, IonResponse.KindOf(@"time-of-flight"));
            Assert.AreEqual(MassAnalyzerKind.unknown, IonResponse.KindOf(null));
            Assert.AreEqual(1.53, IonResponse.Of(MassAnalyzerKind.astral));
            Assert.AreEqual(10.62, IonResponse.Of(MassAnalyzerKind.orbitrap));
            Assert.AreEqual(0.98, IonResponse.Of(MassAnalyzerKind.ion_trap));
            Assert.IsTrue(double.IsNaN(IonResponse.Of(MassAnalyzerKind.unknown)));
            // An Astral spectrum filled for 10 ms: one ion is 153 intensity units.
            Assert.AreEqual(153, IonResponse.IntensityPerIon(IonResponse.ASTRAL, 10), 1e-9);
            Assert.IsTrue(double.IsNaN(IonResponse.IntensityPerIon(IonResponse.ASTRAL, double.NaN)));

            foreach (double alpha in new[] { IonResponse.ASTRAL, double.NaN })
            {
                var source = new StaggeredProfileRun(true, -1, false, alpha);
                var pipeline = new StaggeredDemuxPipeline(source, Channels(), TextWriter.Null);
                double expectedAlpha = double.IsNaN(alpha) ? 1 : alpha;
                for (int output = 0; output < pipeline.OutputCount; output++)
                {
                    double fill = source.FillTime(pipeline.BaseIndex(output));
                    Assert.AreEqual(expectedAlpha * 1000 / fill, pipeline.IntensityPerIon(output), 1e-9 * expectedAlpha * 1000 / fill);
                }
            }

            var times = new[] { 1.0, 2.0, 4.0 };
            var exposures = new[] { 0.5, 1.5, 0.75 };
            Assert.AreEqual(0.5, ScanningDemultiplexer.InterpolateLinear(times, exposures, 0.5));
            Assert.AreEqual(1.5, ScanningDemultiplexer.InterpolateLinear(times, exposures, 2.0));
            Assert.AreEqual(1.0, ScanningDemultiplexer.InterpolateLinear(times, exposures, 1.5), 1e-12);
            Assert.AreEqual(0.75, ScanningDemultiplexer.InterpolateLinear(times, exposures, 9.0));
            Assert.AreEqual(1.0, ScanningDemultiplexer.InterpolateLinear(new double[0], new double[0], 1.0));
        }

        private static ScanningDemuxOptions Options(double l1Z = 2)
        {
            var options = new ScanningDemuxOptions
            {
                Joint = true,
                IonsFromFillTime = true,
                LinearPeakSigmaTh = SIGMA,
                Threads = 2,
            };
            options.JointParameters.L1Z = l1Z;
            return options;
        }

        private static ScanningDemuxOptions Channels()
        {
            var options = new ScanningDemuxOptions
            {
                IonsFromFillTime = true,
                Threads = 2,
            };
            options.Parameters.ChannelToleranceTh = 0.09;
            return options;
        }

        private static ScanningDemuxOptions Unweighted(bool staggeredDefaults = false)
        {
            var options = Options(0);
            options.JointParameters.Reweight = false;
            options.JointParameters.PoissonWeights = false;
            options.JointCentroid = false;
            if (staggeredDefaults)
                options.JointParameters.UseStaggeredDefaults();
            return options;
        }

        private static Dictionary<long, double> BySample((double[] Mz, double[] Rates, int Bin) spectrum)
        {
            var bySample = new Dictionary<long, double>();
            for (int i = 0; i < spectrum.Mz.Length; i++)
                bySample[(long)Math.Round((spectrum.Mz[i] - GRID_START) / SPACING)] = spectrum.Rates[i];
            return bySample;
        }

        /// <summary>
        /// Two runs of the same rates give the same interior bins' rates, sample by sample on the grid (a sample missing
        /// from one side counting as zero), within 2e-3 of the largest rate.
        /// </summary>
        private static void AssertSameInteriorRates(List<(double[] Mz, double[] Rates, int Bin)> uniform,
            List<(double[] Mz, double[] Rates, int Bin)> varied)
        {
            Assert.AreEqual(uniform.Count, varied.Count);
            double scale = uniform.Where(o => o.Rates.Length > 0).Max(o => o.Rates.Max());
            double worst = 0;
            for (int output = 0; output < uniform.Count; output++)
            {
                if (uniform[output].Bin < 2 || uniform[output].Bin >= BINS - 2)
                    continue;
                var a = BySample(uniform[output]);
                var b = BySample(varied[output]);
                foreach (long sample in a.Keys.Union(b.Keys))
                {
                    a.TryGetValue(sample, out double rateA);
                    b.TryGetValue(sample, out double rateB);
                    worst = Math.Max(worst, Math.Abs(rateA - rateB) / scale);
                }
            }
            Console.WriteLine(@"worst rate difference {0:E2} of the largest", worst);
            Assert.IsTrue(worst < 2e-3, worst.ToString(@"E2"));
        }

        /// <summary>Two demultiplexings give the same output spectra, peak for peak.</summary>
        private static void AssertSameOutput(List<(double[] Mz, double[] Rates, int Bin)> expected,
            List<(double[] Mz, double[] Rates, int Bin)> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int output = 0; output < expected.Count; output++)
            {
                CollectionAssert.AreEqual(expected[output].Mz, actual[output].Mz, string.Format(@"output {0}", output));
                CollectionAssert.AreEqual(expected[output].Rates, actual[output].Rates, string.Format(@"output {0}", output));
            }
        }

        /// <summary>Each output spectrum's peaks in ions per second and its bin, -1 for a survey scan.</summary>
        private static List<(double[] Mz, double[] Rates, int Bin)> Demultiplex(IDemuxSource source,
            ScanningDemuxOptions options)
        {
            var pipeline = new StaggeredDemuxPipeline(source, options, TextWriter.Null);
            var outputs = new List<(double[] Mz, double[] Rates, int Bin)>();
            for (int output = 0; output < pipeline.OutputCount; output++)
            {
                // A survey scan passes through as read; it has no demultiplexed peaks.
                if (pipeline.IsPassThrough(output))
                {
                    outputs.Add((new double[0], new double[0], -1));
                    continue;
                }
                var (mz, rates) = Rates(pipeline, output);
                outputs.Add((mz, rates, BinIndex(pipeline.BinOf(output))));
            }
            return outputs;
        }

        /// <summary>An output spectrum's peaks in the input's units, ions per second.</summary>
        private static (double[] Mz, double[] Rates) Rates(StaggeredDemuxPipeline pipeline, int output)
        {
            var (mz, ions) = pipeline.Peaks(output);
            double perIon = pipeline.IntensityPerIon(output);
            return (mz, ions.Select(v => v * perIon).ToArray());
        }

        /// <summary>
        /// The demultiplexed outputs of the run's middle cycles, for bins two or more from the ends of the range: the
        /// first and last bins are each seen through one window only, so they and their neighbors are split less surely.
        /// </summary>
        private static IEnumerable<int> DemultiplexedOutputs(StaggeredDemuxPipeline pipeline, StaggeredProfileRun source)
        {
            for (int output = 0; output < pipeline.OutputCount; output++)
            {
                if (pipeline.IsPassThrough(output))
                    continue;
                int bin = BinIndex(pipeline.BinOf(output));
                int cycle = source.CycleOf(pipeline.BaseIndex(output));
                if (bin >= 2 && bin < BINS - 2 && cycle >= 5 && cycle < CYCLES - 5)
                    yield return output;
            }
        }

        private static int BinIndex(DemuxBin bin)
        {
            return (int)Math.Round((bin.Center - FIRST_EDGE - BIN_WIDTH / 2) / BIN_WIDTH);
        }

        /// <summary>The peak within 0.1 Th of an m/z, or -1.</summary>
        private static int Nearest(double[] mz, double target)
        {
            int best = -1;
            for (int i = 0; i < mz.Length; i++)
            {
                if (Math.Abs(mz[i] - target) < 0.1 && (best < 0 || Math.Abs(mz[i] - target) < Math.Abs(mz[best] - target)))
                    best = i;
            }
            return best;
        }

        /// <summary>
        /// Cycles of a survey scan, a pass of the ten 4 Th windows from 400 m/z, and a pass of the nine offset by 2 Th,
        /// each spectrum its windows' precursors' fragments as profile on the 0.2 Th grid over a low baseline, a quarter of
        /// the points zero. Intensities
        /// are rates (ions per second); the fill time is constant or varies from 0.2 to 20 ms between spectra, which
        /// changes the ions counted but not the rates. One MS2 spectrum may be all zeros, baseline included; the baseline
        /// may be left out.
        /// </summary>
        private sealed class StaggeredProfileRun : IDemuxSource
        {
            public const int MS2_PER_CYCLE = 19;
            private const int SPECTRA_PER_CYCLE = MS2_PER_CYCLE + 1;

            private readonly bool _variedFill;
            private readonly bool _baseline;
            private readonly double _ionResponse;

            public StaggeredProfileRun(bool variedFill, int emptyIndex, bool baseline = true, double ionResponse = double.NaN)
            {
                _variedFill = variedFill;
                _baseline = baseline;
                _ionResponse = ionResponse;
                EmptyIndex = emptyIndex;
            }

            public int Count => CYCLES * SPECTRA_PER_CYCLE;

            public int EmptyIndex { get; }

            /// <summary>The index of a cycle's MS2 spectrum: pass 0 or 1, and its place in the pass.</summary>
            public static int SpectrumIndex(int cycle, int pass, int window)
            {
                return cycle * SPECTRA_PER_CYCLE + 1 + pass * 10 + window;
            }

            public int CycleOf(int index)
            {
                return index / SPECTRA_PER_CYCLE;
            }

            public double TimeOf(int index)
            {
                return index * MINUTES_PER_SPECTRUM;
            }

            public string NativeId(int index)
            {
                return string.Format(@"scan={0}", index + 1);
            }

            public DemuxSpectrumInfo Describe(int index)
            {
                double fill = FillTime(index);
                if (!Window(index, out var window))
                    return new DemuxSpectrumInfo(1, false, default, TimeOf(index), fill, _ionResponse);
                return new DemuxSpectrumInfo(2, true, window, TimeOf(index), fill, _ionResponse);
            }

            /// <summary>A spectrum's fill time, ms: constant, or from 0.2 to 20 ms.</summary>
            public double FillTime(int index)
            {
                return _variedFill ? 0.2 * Math.Pow(100, Fraction(index * 0.6180339887)) : UNIFORM_FILL_MS;
            }

            public void Read(int index, out IReadOnlyList<double> mz, out IReadOnlyList<double> intensity)
            {
                var mzs = new double[GRID_POINTS];
                var values = new double[GRID_POINTS];
                for (int i = 0; i < GRID_POINTS; i++)
                {
                    mzs[i] = GRID_START + i * SPACING;
                    values[i] = index == EmptyIndex || !_baseline ? 0 : Baseline(i);
                }
                if (Window(index, out var window) && index != EmptyIndex)
                {
                    for (int bin = 0; bin < BINS; bin++)
                    {
                        double center = FIRST_EDGE + BIN_WIDTH * (bin + 0.5);
                        if (!window.Contains(center))
                            continue;
                        double elution = Elution(bin, TimeOf(index));
                        foreach (var fragment in Fragments(bin))
                            AddPeak(mzs, values, fragment.Mz, fragment.Height * elution);
                    }
                }
                else if (!Window(index, out _))
                {
                    AddPeak(mzs, values, 420.5, 1e6);
                }
                mz = mzs;
                intensity = values;
            }

            public void Release(int index)
            {
            }

            public void Forget(int index)
            {
            }

            /// <summary>A bin's precursor's fragments: m/z at least 3 Th from every other fragment, and peak heights.</summary>
            public static IEnumerable<(double Mz, double Height)> Fragments(int bin)
            {
                for (int f = 0; f < 3; f++)
                    yield return (305.07 + 7.9 * bin + 131.3 * f, 1e5 * (1 + bin % 3) * (1 + 0.3 * f));
            }

            /// <summary>A bin's precursor's elution at a time: Gaussian, its apex at a cycle from 12 to 16.</summary>
            public static double Elution(int bin, double minutes)
            {
                double apex = (12 + bin % 5) * SPECTRA_PER_CYCLE * MINUTES_PER_SPECTRUM;
                double sigma = 2 * SPECTRA_PER_CYCLE * MINUTES_PER_SPECTRUM;
                return Math.Exp(-0.5 * Math.Pow((minutes - apex) / sigma, 2));
            }

            /// <summary>A Gaussian peak's summed samples on the grid, as Centrix reports a centroid's intensity.</summary>
            public static double Area(double height)
            {
                return height * SIGMA * Math.Sqrt(2 * Math.PI) / SPACING;
            }

            private static bool Window(int index, out IsolationWindow window)
            {
                window = default;
                int place = index % SPECTRA_PER_CYCLE - 1;
                if (place < 0)
                    return false;
                double lower = place < 10 ? FIRST_EDGE + WINDOW_WIDTH * place : FIRST_EDGE + BIN_WIDTH + WINDOW_WIDTH * (place - 10);
                window = IsolationWindow.Symmetric(lower + WINDOW_WIDTH / 2, WINDOW_WIDTH / 2);
                return true;
            }

            private static void AddPeak(double[] mz, double[] values, double center, double height)
            {
                for (int i = 0; i < mz.Length; i++)
                {
                    double z = (mz[i] - center) / SIGMA;
                    if (Math.Abs(z) < 8)
                        values[i] += height * Math.Exp(-0.5 * z * z);
                }
            }

            /// <summary>
            /// The baseline at a grid point, the same in every spectrum: a quarter of the points zero, as on the Stellar,
            /// the rest 20 to 60 ions per second, a ten-thousandth of the fragments.
            /// </summary>
            private static double Baseline(int point)
            {
                return Fraction(point * 0.7548776662) < 0.25 ? 0 : 20 + 40 * Fraction(point * 0.5698402910);
            }

            private static double Fraction(double value)
            {
                return value - Math.Floor(value);
            }
        }

        /// <summary>A run without its first spectra, as an mzML sliced from the middle of a cycle would hold it.</summary>
        private sealed class SkipFirst : IDemuxSource
        {
            private readonly IDemuxSource _source;
            private readonly int _skip;

            public SkipFirst(IDemuxSource source, int skip)
            {
                _source = source;
                _skip = skip;
            }

            public int Count => _source.Count - _skip;

            public string NativeId(int index)
            {
                return _source.NativeId(index + _skip);
            }

            public DemuxSpectrumInfo Describe(int index)
            {
                return _source.Describe(index + _skip);
            }

            public void Read(int index, out IReadOnlyList<double> mz, out IReadOnlyList<double> intensity)
            {
                _source.Read(index + _skip, out mz, out intensity);
            }

            public void Release(int index)
            {
                _source.Release(index + _skip);
            }

            public void Forget(int index)
            {
                _source.Forget(index + _skip);
            }
        }
    }
}
