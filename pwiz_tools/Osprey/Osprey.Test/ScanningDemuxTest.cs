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
    /// Tests for <see cref="ScanningDemultiplexer"/>, <see cref="FragmentChannelFinder"/> and
    /// <see cref="ScanningLayout"/> against synthetic sweeps simulated through the same
    /// transmission the solve uses: a trapezoid like the one measured on a ZenoTOF 8600, flat
    /// within 2 Th of the precursor and falling to zero at 9.5 Th, stepped 1.18 Th.
    /// </summary>
    [TestClass]
    public class ScanningDemuxTest
    {
        private const double STEP = 1.18;
        private const double FIRST_CENTER = 500.0;
        private const int BINS = 60;
        private const int CYCLES = 9;

        /// <summary>
        /// Channels: a fragment with a few ppm of jitter across its cells is one channel; two
        /// fragments 30 ppm apart are two; two within the tolerance are one; a weak lone peak is in
        /// none; and shuffling the peaks does not change which peaks share a channel.
        /// </summary>
        [TestMethod]
        public void TestScanningChannelFinder()
        {
            var random = new Random(3);
            var mz = new List<double>();
            var ions = new List<double>();
            var source = new List<int>();
            void AddFragment(double center, double amount, int id)
            {
                for (int cell = 0; cell < 16; cell++)
                {
                    mz.Add(center * (1 + (random.NextDouble() - 0.5) * 4e-6));
                    ions.Add(amount);
                    source.Add(id);
                }
            }
            AddFragment(400.2, 20, 0);
            AddFragment(400.2 * (1 + 30e-6), 20, 1);
            AddFragment(700.3, 20, 2);
            AddFragment(700.3 * (1 + 6e-6), 5, 3);  // within 10 ppm of fragment 2
            mz.Add(900.9);  // one weak lone peak
            ions.Add(1);
            source.Add(4);

            var channel = new int[mz.Count];
            int channels = FragmentChannelFinder.Find(mz.ToArray(), ions.ToArray(), 10, 8, channel);
            Assert.AreEqual(3, channels);
            AssertOneChannelPerSource(channel, source, 0);
            AssertOneChannelPerSource(channel, source, 1);
            AssertOneChannelPerSource(channel, source, 2);
            Assert.AreNotEqual(ChannelOf(channel, source, 0), ChannelOf(channel, source, 1));
            Assert.AreEqual(ChannelOf(channel, source, 2), ChannelOf(channel, source, 3));
            Assert.AreEqual(-1, ChannelOf(channel, source, 4));

            // The same peaks in reverse order: the same grouping and channel numbering.
            var reversed = new int[mz.Count];
            FragmentChannelFinder.Find(Enumerable.Reverse(mz).ToArray(), Enumerable.Reverse(ions).ToArray(), 10, 8,
                reversed);
            for (int i = 0; i < mz.Count; i++)
                Assert.AreEqual(channel[i], reversed[mz.Count - 1 - i]);
        }

        /// <summary>
        /// Demultiplexing sweeps simulated through the solve's own transmission: noiseless, every
        /// fragment returns exactly to its precursor's bin, including a fragment two precursors
        /// share; with counting noise, the three bins centered on each precursor hold its
        /// intensity; a lone weak peak passes through; and the result is the same twice.
        /// </summary>
        [TestMethod]
        public void TestScanningDemuxRecovers()
        {
            var centers = Enumerable.Range(0, BINS).Select(b => FIRST_CENTER + b * STEP).ToArray();
            var a = ScanningDemultiplexer.TransmissionMatrix(TrapezoidKernel(), centers, centers, STEP);
            // A precursor at the center of its own bin is transmitted at 1 there.
            Assert.AreEqual(1.0, a[30, 30], 1e-9);

            var sources = new[]
            {
                (Bin: 20, Mz: 300.1234, Amount: 1000.0),
                (Bin: 20, Mz: 450.2345, Amount: 400.0),
                (Bin: 34, Mz: 520.3456, Amount: 800.0),
                (Bin: 34, Mz: 701.5678, Amount: 500.0),
                (Bin: 40, Mz: 701.5678, Amount: 250.0),  // shared with bin 34
            };
            var parameters = new ScanningDemuxParams();

            var exact = ScanningDemultiplexer.DemuxUnit(Simulate(a, sources, null), parameters);
            foreach (var s in sources.GroupBy(s => s.Mz))
            {
                for (int bin = 10; bin < 50; bin++)
                {
                    double expected = s.Where(t => t.Bin == bin).Sum(t => t.Amount) * TotalElution();
                    Assert.AreEqual(expected, Sum(exact.Demultiplexed, bin, s.Key), 1e-6 * s.Max(t => t.Amount),
                        string.Format(@"m/z {0}, bin {1}", s.Key, bin));
                }
            }
            Assert.AreEqual(1, exact.PassedThrough.Count);  // the lone weak peak only

            // Apportioned: an isolated source's own bin keeps its observed peaks whole; a neighbor
            // that only transmits it keeps nothing.
            var apportioned = ScanningDemultiplexer.DemuxUnit(Simulate(a, sources, null),
                new ScanningDemuxParams { ApportionHalfWidth = 0 });
            Assert.AreEqual(1000 * TotalElution(), Sum(apportioned.Demultiplexed, 20, 300.1234), 1e-6 * 1000);
            Assert.AreEqual(0, Sum(apportioned.Demultiplexed, 21, 300.1234), 1e-6 * 1000);

            var noisy = Simulate(a, sources, new Random(11));
            var first = ScanningDemultiplexer.DemuxUnit(noisy, parameters);
            foreach (var s in sources.Where(t => t.Mz != 701.5678))
            {
                double expected = s.Amount * TotalElution();
                double found = Enumerable.Range(s.Bin - 1, 3).Sum(bin => Sum(first.Demultiplexed, bin, s.Mz));
                Assert.AreEqual(expected, found, 0.15 * expected, string.Format(@"m/z {0}, 3 bins", s.Mz));
            }
            // The lone peak is too weak for a channel and passes through as acquired.
            Assert.IsTrue(first.PassedThrough.Any(p => Math.Abs(p.Mz - 999.999) < 1e-9 && p.Ions == 3));

            var second = ScanningDemultiplexer.DemuxUnit(Simulate(a, sources, new Random(11)), parameters);
            CollectionAssert.AreEqual(first.Demultiplexed, second.Demultiplexed);
            CollectionAssert.AreEqual(first.PassedThrough, second.PassedThrough);
        }

        /// <summary>
        /// The same channels and weighted solve on a stepped staggered acquisition: twelve bins,
        /// windows of two bins in one set and offset by one bin in the other, the sets acquired
        /// half a cycle apart. With a constant elution the interpolation is exact, so each
        /// spectrum's observed peaks are apportioned exactly to their sources' bins, including a
        /// fragment two bins share.
        /// </summary>
        [TestMethod]
        public void TestStaggeredDemuxRecovers()
        {
            const int bins = 12, cycles = 10;
            var windows = new List<(int First, int Last, double Offset)>();
            for (int b = 0; b < bins; b += 2)
                windows.Add((b, b + 1, 0.0));
            for (int b = 1; b + 1 < bins; b += 2)
                windows.Add((b, b + 1, 0.5));
            var a = new double[windows.Count, bins];
            for (int r = 0; r < windows.Count; r++)
            {
                for (int j = windows[r].First; j <= windows[r].Last; j++)
                    a[r, j] = 1;
            }
            var sources = new[] { (Bin: 3, Mz: 500.1, Amount: 100.0), (Bin: 6, Mz: 500.1, Amount: 60.0), (Bin: 5, Mz: 610.2, Amount: 80.0) };

            var rowTimes = new double[windows.Count][];
            var rowSpectra = new int[windows.Count][];
            var outputRow = new List<int>();
            var outputAcquisition = new List<int>();
            var outputTime = new List<double>();
            var outputSpectrum = new List<int>();
            var mz = new List<double>();
            var ions = new List<double>();
            var row = new List<int>();
            var acquisition = new List<int>();
            for (int r = 0; r < windows.Count; r++)
            {
                rowTimes[r] = new double[cycles];
                rowSpectra[r] = new int[cycles];
                for (int c = 0; c < cycles; c++)
                {
                    rowTimes[r][c] = c + windows[r].Offset + 0.01 * r;
                    rowSpectra[r][c] = r * cycles + c;
                    if (c >= 2 && c <= 7)
                    {
                        outputRow.Add(r);
                        outputAcquisition.Add(c);
                        outputTime.Add(rowTimes[r][c]);
                        outputSpectrum.Add(rowSpectra[r][c]);
                    }
                    foreach (var fragment in sources.GroupBy(s => s.Mz))
                    {
                        double value = fragment.Where(s => a[r, s.Bin] > 0).Sum(s => s.Amount);
                        if (value <= 0)
                            continue;
                        mz.Add(fragment.Key);
                        ions.Add(value);
                        row.Add(r);
                        acquisition.Add(c);
                    }
                }
            }
            var unit = new InterpolatedUnit(a, Enumerable.Range(0, bins).ToArray(), rowTimes, rowSpectra,
                outputRow.ToArray(), outputAcquisition.ToArray(), outputTime.ToArray(), outputSpectrum.ToArray(), 0, bins - 1)
            {
                Mz = mz.ToArray(),
                Ions = ions.ToArray(),
                Row = row.ToArray(),
                Acquisition = acquisition.ToArray(),
            };

            var result = ScanningDemultiplexer.DemuxInterpolatedUnit(unit, new ScanningDemuxParams());
            Assert.AreEqual(0, result.PassedThrough.Count);
            for (int s = 0; s < outputRow.Count; s++)
            {
                var w = windows[outputRow[s]];
                for (int bin = w.First; bin <= w.Last; bin++)
                {
                    foreach (var fragment in sources.GroupBy(t => t.Mz))
                    {
                        double expected = fragment.Where(t => t.Bin == bin).Sum(t => t.Amount);
                        double found = result.Demultiplexed
                            .Where(p => p.Cycle == outputSpectrum[s] && p.Bin == bin && Math.Abs(p.Mz - fragment.Key) < 1e-6)
                            .Sum(p => p.Ions);
                        Assert.AreEqual(expected, found, 1e-6 * 100,
                            string.Format(@"spectrum {0}, bin {1}, m/z {2}", outputSpectrum[s], bin, fragment.Key));
                    }
                }
            }
            // Each spectrum keeps only its own window's bins.
            Assert.IsTrue(result.Demultiplexed.All(p =>
                windows[outputRow[outputSpectrum.IndexOf(p.Cycle)]].First <= p.Bin &&
                p.Bin <= windows[outputRow[outputSpectrum.IndexOf(p.Cycle)]].Last));
        }

        /// <summary>
        /// Layouts: the spectra a centered and a tiled layout plan for a run of bins; merging of
        /// one channel's peaks from neighboring positions; and parsing.
        /// </summary>
        [TestMethod]
        public void TestScanningLayout()
        {
            var centered = ScanningLayout.Parse(@"centered:5");
            var planCentered = centered.Plan(100, 112);
            Assert.AreEqual(13, planCentered.Count);
            Assert.AreEqual(new ScanningOutputSpectrum(100, 100, 98, 102), planCentered[0]);
            Assert.AreEqual(@"centered5", centered.Name);

            var tiled = ScanningLayout.Parse(@"tiled:5");
            var planTiled = tiled.Plan(100, 112);
            Assert.AreEqual(3, planTiled.Count);
            Assert.AreEqual(new ScanningOutputSpectrum(100, 104, 100, 104), planTiled[0]);
            Assert.AreEqual(new ScanningOutputSpectrum(110, 112, 110, 112), planTiled[2]);

            var framed = ScanningLayout.Parse(@"framed:3:1");
            var planFramed = framed.Plan(100, 112);
            Assert.AreEqual(5, planFramed.Count);
            Assert.AreEqual(new ScanningOutputSpectrum(100, 102, 99, 103), planFramed[0]);
            Assert.AreEqual(new ScanningOutputSpectrum(112, 112, 111, 113), planFramed[4]);
            Assert.AreEqual(@"framed3m1", framed.Name);

            Assert.ThrowsException<ArgumentException>(() => ScanningLayout.Parse(@"centered:4"));
            Assert.ThrowsException<FormatException>(() => ScanningLayout.Parse(@"stacked:5"));
            Assert.ThrowsException<FormatException>(() => ScanningLayout.Parse(@"framed:3"));

            // One channel from three positions (the third 2 ppm off) merges; a pass-through peak at
            // the same m/z does not.
            var demultiplexed = new[]
            {
                new ScanningPeak(100, 7, 600.0, 10),
                new ScanningPeak(101, 7, 600.0, 30),
                new ScanningPeak(102, 7, 600.0012, 10),
                new ScanningPeak(101, 7, 650.0, 5),
            };
            var passedThrough = new[] { new ScanningPeak(101, 7, 600.0, 1) };
            ScanningLayout.Assemble(passedThrough, demultiplexed, out double[] mz, out double[] ions);
            CollectionAssert.AreEqual(new[] { 1.0, 50.0, 5.0 }, ions);
            Assert.AreEqual(600.0, mz[0], 1e-12);
            Assert.AreEqual((600.0 * 40 + 600.0012 * 10) / 50, mz[1], 1e-9);
            Assert.AreEqual(650.0, mz[2], 1e-12);
        }

        /// <summary>
        /// One block of <see cref="BINS"/> bins by <see cref="CYCLES"/> sweeps, the core the middle
        /// 40 bins and sweeps 2 to 6. Each source's fragment is transmitted through its bin's
        /// column of <paramref name="a"/>, with a Gaussian elution over the sweeps, and Poisson
        /// counts when <paramref name="noise"/> is given. A lone 3-ion peak is added in the core.
        /// </summary>
        private static ScanningUnit Simulate(double[,] a, (int Bin, double Mz, double Amount)[] sources, Random noise)
        {
            var bins = Enumerable.Range(0, BINS).ToArray();
            var cycles = Enumerable.Range(0, CYCLES).ToArray();
            var mz = new List<double>();
            var ions = new List<double>();
            var row = new List<int>();
            var cycle = new List<int>();
            for (int c = 0; c < CYCLES; c++)
            {
                for (int r = 0; r < BINS; r++)
                {
                    foreach (var fragment in sources.GroupBy(s => s.Mz))
                    {
                        double expected = fragment.Sum(s => s.Amount * a[r, s.Bin]) * Elution(c);
                        double value = noise == null ? expected : Poisson(noise, expected);
                        if (value <= 0)
                            continue;
                        mz.Add(fragment.Key);
                        ions.Add(value);
                        row.Add(r);
                        cycle.Add(c);
                    }
                }
            }
            mz.Add(999.999);
            ions.Add(3);
            row.Add(30);
            cycle.Add(4);
            return new ScanningUnit(a, bins, bins, cycles, 10, 49, 2, 6)
            {
                Mz = mz.ToArray(),
                Ions = ions.ToArray(),
                Row = row.ToArray(),
                Cycle = cycle.ToArray(),
            };
        }

        private static double Elution(int cycle)
        {
            return Math.Exp(-0.5 * Math.Pow((cycle - 4) / 1.2, 2));
        }

        /// <summary>The elution summed over the core sweeps 2 to 6.</summary>
        private static double TotalElution()
        {
            return Enumerable.Range(2, 5).Sum(Elution);
        }

        private static ScanningKernel TrapezoidKernel()
        {
            var offsets = new List<double>();
            var values = new List<double>();
            for (double d = -12; d <= 12.001; d += 0.25)
            {
                double abs = Math.Abs(d);
                offsets.Add(d);
                values.Add(abs <= 2 ? 1 : abs >= 9.5 ? 0 : (9.5 - abs) / 7.5);
            }
            return ScanningKernel.FromSamples(offsets, values);
        }

        private static double Sum(IEnumerable<ScanningPeak> peaks, int bin, double mz)
        {
            return peaks.Where(p => p.Bin == bin && Math.Abs(p.Mz - mz) < 1e-6).Sum(p => p.Ions);
        }

        private static int ChannelOf(int[] channel, List<int> source, int id)
        {
            return channel[source.IndexOf(id)];
        }

        private static void AssertOneChannelPerSource(int[] channel, List<int> source, int id)
        {
            var assigned = Enumerable.Range(0, source.Count).Where(i => source[i] == id).Select(i => channel[i]).Distinct()
                .ToList();
            Assert.AreEqual(1, assigned.Count, @"source " + id);
            Assert.AreNotEqual(-1, assigned[0], @"source " + id);
        }

        /// <summary>A Poisson draw: Knuth's method for small means, a rounded normal for large ones.</summary>
        private static double Poisson(Random random, double mean)
        {
            if (mean > 50)
            {
                double u1 = 1 - random.NextDouble(), u2 = random.NextDouble();
                double z = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
                return Math.Max(0, Math.Round(mean + Math.Sqrt(mean) * z));
            }
            double limit = Math.Exp(-mean), product = 1;
            int k = 0;
            do
            {
                k++;
                product *= random.NextDouble();
            }
            while (product > limit);
            return k - 1;
        }
    }
}
