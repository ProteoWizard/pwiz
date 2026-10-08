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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Training;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The alignment of training runs' retention times onto the hydrophobic index (<c>-rt_align kde</c>): Chronologer's
    /// KDE ridge alignment ported exactly, the LOESS or line that maps a run too sparse for it, the monotone maps and
    /// their median, and a training set whose runs drift taken back onto one scale before its RT rows are chosen.
    /// </summary>
    [TestClass]
    public class RtAlignmentTest
    {
        public TestContext TestContext { get; set; }

        /// <summary>The port's knots are the ones Chronologer's own <c>KDE_align</c> fitted (<see cref="KdeAlignmentReference"/>).</summary>
        [TestMethod]
        public void TestKdeAlignmentMatchesChronologer()
        {
            var map = KdeRidgeAlignment.Fit(KdeAlignmentReference.INPUT_RT, KdeAlignmentReference.INPUT_HI, KdeAlignmentReference.GRID);
            Assert.AreEqual(KdeAlignmentReference.FIT_RT.Length, map.X.Count);
            for (int k = 0; k < map.X.Count; k++)
            {
                Assert.AreEqual(KdeAlignmentReference.FIT_RT[k], map.X[k], 1e-9, @"knot " + k);
                Assert.AreEqual(KdeAlignmentReference.FIT_HI[k], map.Y[k], 1e-9, @"knot " + k);
            }

            // At Chronologer's grid of 3,000, on a run's worth of peptides with 5% wrong matches, the fit recovers the
            // curve the right matches follow, in seconds.
            var random = new Random(3);
            int count = 20000;
            var hi = Enumerable.Range(0, count).Select(_ => 2 + 38 * random.NextDouble()).ToArray();
            var rt = hi.Select(h => 1.5 + 0.55 * h + 0.004 * h * h + 0.1 * (random.NextDouble() - 0.5)).ToArray();
            for (int k = 0; k < count / 20; k++)
                rt[random.Next(count)] = 1.5 + 28 * random.NextDouble();
            var clock = Stopwatch.StartNew();
            var fit = KdeRidgeAlignment.Fit(rt, hi);
            TestContext.WriteLine(@"{0} points at grid {1}: {2:F1} s, {3} knots", count, KdeRidgeAlignment.DEFAULT_GRID, clock.Elapsed.TotalSeconds, fit.X.Count);
            for (double h = 5; h <= 35; h += 5)
            {
                double minutes = 1.5 + 0.55 * h + 0.004 * h * h;
                Assert.AreEqual(h, fit.Map(minutes), 0.2, string.Format(CultureInfo.InvariantCulture, @"HI {0} at {1:F2} min", h, minutes));
            }
        }

        /// <summary>
        /// The maps: applied clipped as Chronologer applies them, extended past their knots with the slope of their
        /// ends, inverted through flat stretches, and their pointwise median, which no single map need be.
        /// </summary>
        [TestMethod]
        public void TestMonotoneMap()
        {
            var map = new MonotoneMap(new[] { 0.0, 1, 2, 3, 4 }, new[] { 10.0, 10, 12, 14, 14 });
            Assert.AreEqual(10.0, map.Map(-5));
            Assert.AreEqual(14.0, map.Map(9));
            Assert.AreEqual(11.0, map.Map(1.5), 1e-12);
            // Flat ends are padding past the data: the inverse takes the end the data reached; it extends with the
            // slope of its outer knots.
            var inverse = map.Invert();
            CollectionAssert.AreEqual(new[] { 10.0, 12, 14 }, inverse.X.ToArray());
            CollectionAssert.AreEqual(new[] { 1.0, 2, 3 }, inverse.Y.ToArray());
            Assert.AreEqual(4.0, inverse.Extend(16), 1e-12);
            Assert.AreEqual(0.0, inverse.Extend(8), 1e-12);
            // A flat stretch inside the map inverts to its middle.
            CollectionAssert.AreEqual(new[] { 0.0, 1.5, 3 }, new MonotoneMap(new[] { 0.0, 1, 2, 3 }, new[] { 0.0, 5, 5, 9 }).Invert().Y.ToArray());
            Assert.ThrowsException<ArgumentException>(() => new MonotoneMap(new[] { 0.0, 1 }, new[] { 2.0, 1 }));
            Assert.ThrowsException<ArgumentException>(() => new MonotoneMap(new[] { 0.0, 0 }, new[] { 1.0, 2 }));

            // The median of three runs is the middle one; of two, their mean.
            var early = new MonotoneMap(new[] { 0.0, 10 }, new[] { 0.0, 10 });
            var middle = new MonotoneMap(new[] { 0.0, 10 }, new[] { 1.0, 11 });
            var late = new MonotoneMap(new[] { 0.0, 10 }, new[] { 5.0, 15 });
            var median = MonotoneMap.PointwiseMedian(new[] { late, early, middle }, 11);
            for (int x = 0; x <= 10; x++)
                Assert.AreEqual(x + 1.0, median.Map(x), 1e-12);
            Assert.AreEqual(3.0, MonotoneMap.PointwiseMedian(new[] { early, late }, 11).Map(0.5), 1e-12);
            // Runs that cross: the median switches from one to the other, which neither is.
            var crossing = new MonotoneMap(new[] { 0.0, 10 }, new[] { 2.0, 8 });
            var crossed = MonotoneMap.PointwiseMedian(new[] { early, crossing, late }, 101);
            Assert.AreEqual(2.0, crossed.Map(0), 1e-12);
            Assert.AreEqual(10.0, crossed.Map(10), 1e-12);
        }

        /// <summary>
        /// A run's map fitted as firmly as its peptide forms allow: the KDE ridge on 1000 or more, a LOESS below, its
        /// window widening below 200, and under 100 a robust line, which the forms must span half the gradient to fix.
        /// Each follows a curved gradient through noise and a few false identifications, and stays monotone.
        /// </summary>
        [TestMethod]
        public void TestRtMapFit()
        {
            const double gradient = 25;
            double Truth(double minutes) => 40 * Math.Pow(minutes / gradient, 1.3);
            (double[] Minutes, double[] Hi) Run(int n, double from, double to, int seed)
            {
                var random = new Random(seed);
                double[] minutes = Enumerable.Range(0, n).Select(_ => from + (to - from) * random.NextDouble()).ToArray();
                // Noise of 1.5 HI, as Chronologer's predictions scatter, and one form in 30 identified far from its HI.
                double[] hi = minutes.Select(m => Truth(m) + (random.Next(30) == 0 ? 15 * (random.NextDouble() - 0.5) : 0) +
                                                  1.5 * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble()))
                    .ToArray();
                return (minutes, hi);
            }
            double Error(MonotoneMap map) => Enumerable.Range(0, 41).Select(k => 3 + 0.5 * k).Max(m => Math.Abs(map.Map(m) - Truth(m)));

            foreach (var (n, fit, tolerance) in new[]
                     {
                         (1500, @"KDE ridge on 1500 peptide forms", 0.8),
                         (500, @"LOESS (bandwidth 0.30) on 500 peptide forms", 0.8),
                         (150, @"LOESS (bandwidth 0.40) on 150 peptide forms", 1.0),
                         (60, @"Theil-Sen line on 60 peptide forms", 3.0),
                     })
            {
                var (minutes, hi) = Run(n, 1, gradient, n);
                var result = RtMapFit.Fit(minutes, hi, gradient);
                double error = Error(result.Map);
                TestContext.WriteLine(@"{0}: largest error {1:F2} HI over {2} knots", result.Fit, error, result.Map.X.Count);
                Assert.AreEqual(fit, result.Fit);
                Assert.IsTrue(error < tolerance, result.Fit + @": " + error);
            }

            // Forty minutes of a 25-minute run would be the one gradient; four do not fix a line's slope.
            var (bunched, bunchedHi) = Run(60, 10, 14, 3);
            StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() => RtMapFit.Fit(bunched, bunchedHi, gradient)).Message,
                @"too little to fix a line's slope");
            StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() =>
                RtMapFit.Fit(bunched, bunchedHi.Select(h => -h).ToArray(), 4)).Message, @"does not rise");
            // The KDE ridge of HI that falls with the minutes does not rise either; minutes or HI that do not vary fit nothing.
            var (falling, fallingHi) = Run(300, 1, gradient, 5);
            StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() =>
                RtMapFit.Fit(falling, fallingHi.Select(h => -h).ToArray(), gradient, 100)).Message, @"does not rise");
            StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() =>
                RtMapFit.Fit(falling, falling.Select(_ => 7.0).ToArray(), gradient, 100)).Message, @"do not vary");
            // A curve that dips is pooled across the dip into one knot at its mean, so the map rises strictly and has no
            // flat stretch for Invert to take for padding; equal minutes are one point at their mean before pooling.
            var pooled = RtMapFit.Monotone(new[] { 0.0, 1, 2, 3 }, new[] { 0.0, 2, 1, 3 });
            CollectionAssert.AreEqual(new[] { 0.0, 1.5, 3 }, pooled.X.ToArray());
            CollectionAssert.AreEqual(new[] { 0.0, 1.5, 3 }, pooled.Y.ToArray());
            var merged = RtMapFit.Monotone(new[] { 0.0, 0, 1, 2 }, new[] { 1.0, 3, 2, 5 });
            CollectionAssert.AreEqual(new[] { 0.5, 2 }, merged.X.ToArray());
            CollectionAssert.AreEqual(new[] { 2.0, 5 }, merged.Y.ToArray());
            var tied = RtMapFit.Monotone(new[] { 1.0, 2, 2 }, new[] { 5.0, 3, 100 });
            CollectionAssert.AreEqual(new[] { 1.0, 2 }, tied.X.ToArray());
            CollectionAssert.AreEqual(new[] { 5.0, 51.5 }, tied.Y.ToArray());
            Assert.IsNull(RtMapFit.Monotone(new[] { 0.0, 1 }, new[] { 2.0, 1 }));
            // A pooled first block inverts to its middle, not its end: 1.5, the mean of 1 and 2, back from HI 1.5.
            var dipping = RtMapFit.Monotone(new[] { 1.0, 2, 3, 4 }, new[] { 2.0, 1, 3, 4 });
            Assert.AreEqual(1.5, dipping.Invert().Map(1.5), 1e-12);
        }

        /// <summary>
        /// Runs from minutes onto HI and back: the median map lies between the runs, the spread is how far they are from
        /// it, and the maps save and read back as they were.
        /// </summary>
        [TestMethod]
        public void TestRtAlignment()
        {
            // Run b elutes everything 0.5 min later than run a.
            var a = new MonotoneMap(new[] { 2.0, 20 }, new[] { 0.0, 36 });
            var b = new MonotoneMap(new[] { 2.5, 20.5 }, new[] { 0.0, 36 });
            var alignment = new RtAlignment(new[] { (@"a", a, (string)null), (@"b", b, null) }, @"test");
            Assert.AreEqual(11.25, alignment.ToMinutes(18), 1e-9);
            Assert.AreEqual(0.25, alignment.SpreadMax, 1e-9);
            Assert.AreEqual(0.25, alignment.SpreadP95, 1e-9);
            Assert.AreSame(b, alignment.GetRun(@"b"));
            Assert.ThrowsException<KeyNotFoundException>(() => alignment.GetRun(@"c"));
            // Past the runs' range it continues, rather than stopping at the last minute.
            Assert.AreEqual(alignment.ToMinutes(36) + 2.0, alignment.ToMinutes(40), 1e-9);
            // One run's minutes, by its own map; a run is named in full or by the end of its name.
            Assert.AreEqual(11.5, alignment.ToRunMinutes(@"b")(18), 1e-9);
            // A model of the median minutes, taken to one run's: 11.25 on the median is HI 18, 11.5 on run b.
            Assert.AreEqual(11.5, alignment.MedianToRunMinutes(@"b")(11.25), 1e-9);
            Assert.AreEqual(alignment.ToRunMinutes(@"a")(40), alignment.MedianToRunMinutes(@"a")(alignment.ToMinutes(40)), 1e-9);
            Assert.AreEqual(@"b", alignment.FindRun(@"b"));
            var replicates = new RtAlignment(new[] { (@"plate1_55", a, (string)null), (@"plate2_55", b, null), (@"plate2_60", a, null) }, @"test");
            Assert.AreEqual(@"plate2_60", replicates.FindRun(@"_60"));
            StringAssert.Contains(Assert.ThrowsException<ArgumentException>(() => replicates.FindRun(@"_55")).Message, @"several");
            StringAssert.Contains(Assert.ThrowsException<ArgumentException>(() => replicates.FindRun(@"_49")).Message, @"plate1_55, plate2_55, plate2_60");
            // A quarter minute on an 18-minute gradient is one gradient; a gradient half as long is another.
            Assert.IsFalse(alignment.IsWide, alignment.ToString());
            var shorter = new MonotoneMap(new[] { 1.0, 10 }, new[] { 0.0, 36 });
            var mixed = new RtAlignment(new[] { (@"a", a, (string)null), (@"short", shorter, null) }, @"test");
            Assert.IsTrue(mixed.IsWide, mixed.ToString());
            StringAssert.Contains(mixed.ToString(), @"(different gradients)");
            Assert.AreEqual(5.5, mixed.ToRunMinutes(@"short")(18), 1e-9);

            string path = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"rt_maps_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                alignment.Write(path);
                var read = RtAlignment.Read(path);
                Assert.AreEqual(@"test", read.Reference);
                CollectionAssert.AreEqual(new[] { @"a", @"b" }, read.Runs.Select(r => r.Run).ToArray());
                CollectionAssert.AreEqual(b.X.ToArray(), read.GetRun(@"b").X.ToArray());
                CollectionAssert.AreEqual(alignment.HiToMinutes.Y.ToArray(), read.HiToMinutes.Y.ToArray());
                Assert.AreEqual(alignment.SpreadMax, read.SpreadMax, 1e-12);
                Assert.IsTrue(double.IsNaN(read.RtMax), @"not recorded");

                // The training normalizer reads back; runs that share no HI range are different gradients, their spread
                // unknown, which the file holds as null.
                var early = new MonotoneMap(new[] { 2.0, 10 }, new[] { 0.0, 10 });
                var late = new MonotoneMap(new[] { 12.0, 20 }, new[] { 20.0, 30 });
                var apart = new RtAlignment(new[] { (@"early", early, (string)null), (@"late", late, null) }, @"test", 24.1);
                Assert.IsTrue(double.IsNaN(apart.SpreadP95));
                Assert.IsTrue(apart.IsWide, apart.ToString());
                StringAssert.Contains(apart.ToString(), @"share no HI range");
                apart.Write(path);
                StringAssert.Contains(File.ReadAllText(path), @"""p95"": null");
                var readApart = RtAlignment.Read(path);
                Assert.AreEqual(24.1, readApart.RtMax);
                Assert.IsTrue(readApart.IsWide);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// A training set of two runs of the same peptides, the second drifting later along the gradient (5% slower and
        /// 0.3 min late), each run winning half the peptides. Unaligned, every row the drifting run won carries its
        /// drift; aligned, by a LOESS on their 120 forms or the KDE ridge, each run's map takes it out first, and every
        /// row lies on one line of the peptides' HI, with the median map's minutes between the runs. A third run with too
        /// few peptides to align gives no RT rows, while the other two stay aligned; when no run can be aligned the set is
        /// unaligned, saying why. Two acquisitions of one file name, from two folders, are two runs.
        /// </summary>
        [TestMethod]
        public void TestTrainingSetAlignment()
        {
            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Align_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            try
            {
                var random = new Random(9);
                const string residues = @"ACDEFGHIKLMNPQRSTVWY";
                var sequences = Enumerable.Range(0, 120)
                    .Select(_ => new string(Enumerable.Range(0, random.Next(7, 13)).Select(i => residues[random.Next(residues.Length)]).ToArray()) + @"K")
                    .Distinct()
                    .ToArray();
                var hiOf = sequences.ToDictionary(s => s, _ => 2 + 36 * random.NextDouble(), StringComparer.Ordinal);
                double MinutesA(string s) => 2 + 0.5 * hiOf[s];
                string exportA = WriteExport(folder, @"run_a", sequences, MinutesA, (s, i) => i % 2 == 0);
                string exportB = WriteExport(folder, @"run_b", sequences, s => 1.05 * MinutesA(s) + 0.3, (s, i) => i % 2 == 1);
                var exports = new[] { OspreyTrainingExport.Read(exportA), OspreyTrainingExport.Read(exportB) };

                var unaligned = OspreyTrainingSet.Build(exports, new OspreyTrainingSetOptions());
                Assert.IsNull(unaligned.Alignment);
                double unalignedResidual = LineResidual(unaligned.Rt, hiOf, unaligned.Stats.RtMax);
                var options = new OspreyTrainingSetOptions
                {
                    PredictHi = forms => forms.Select(f => hiOf[f.Sequence]).ToArray(),
                    AlignmentReference = @"test",
                };
                foreach (var (kdeMinPoints, fit) in new[] { (RtMapFit.DEFAULT_KDE_MIN_POINTS, @"LOESS (bandwidth 0.50)"), (20, @"KDE ridge") })
                {
                    options.KdeMinPoints = kdeMinPoints;
                    var aligned = OspreyTrainingSet.Build(exports, options);
                    Assert.IsNotNull(aligned.Alignment);
                    Assert.IsNull(aligned.Unaligned);
                    CollectionAssert.AreEqual(new[] { @"run_a", @"run_b" }, aligned.Alignment.Runs.Select(r => r.Run).ToArray());
                    Assert.IsTrue(aligned.Alignment.Runs.All(r => r.Fit.StartsWith(fit, StringComparison.Ordinal)), string.Join(@"; ", aligned.Alignment.Runs.Select(r => r.Fit)));
                    Assert.AreEqual(sequences.Length, aligned.Rt.Count);
                    Assert.IsTrue(aligned.Rt.All(r => Math.Abs(r.Hi - hiOf[r.Peptide.Sequence]) < 0.5),
                        @"largest HI error " + aligned.Rt.Max(r => Math.Abs(r.Hi - hiOf[r.Peptide.Sequence])));
                    double alignedResidual = LineResidual(aligned.Rt, hiOf, aligned.Stats.RtMax);
                    TestContext.WriteLine(@"largest distance from one line of HI, minutes: unaligned {0:F3}, aligned {1:F3}; {2}; {3}",
                        unalignedResidual, alignedResidual, aligned.Alignment, aligned.Alignment.Runs[0].Fit);
                    Assert.IsTrue(unalignedResidual > 0.3, @"unaligned " + unalignedResidual);
                    Assert.IsTrue(alignedResidual < unalignedResidual / 3, @"aligned " + alignedResidual);
                    // The median map's minutes lie between the runs'.
                    double minutes = aligned.Alignment.ToMinutes(20);
                    Assert.IsTrue(minutes > 2 + 0.5 * 20 && minutes < 1.05 * (2 + 0.5 * 20) + 0.3, @"median minutes " + minutes);
                }
                // The runs' median per form gives one row per form too.
                options.RtSelection = RtSelectionType.median;
                Assert.AreEqual(sequences.Length, OspreyTrainingSet.Build(exports, options).Rt.Count);

                options.KdeMinPoints = RtMapFit.DEFAULT_KDE_MIN_POINTS;
                Assert.AreEqual(OspreyTrainingSet.Build(exports, options).Stats.RtMax, OspreyTrainingSet.Build(exports, options).Alignment.RtMax);

                // A third run too sparse to map gives no RT rows; the other two stay aligned.
                string exportC = WriteExport(folder, @"run_c", sequences.Take(10).ToArray(), MinutesA, (s, i) => true);
                var withSparse = OspreyTrainingSet.Build(exports.Append(OspreyTrainingExport.Read(exportC)).ToArray(), options);
                CollectionAssert.AreEqual(new[] { @"run_a", @"run_b" }, withSparse.Alignment.Runs.Select(r => r.Run).ToArray());
                Assert.AreEqual(1, withSparse.UnalignedRuns.Count);
                StringAssert.Contains(withSparse.UnalignedRuns[0], @"run run_c has 10 peptide forms");
                Assert.AreEqual(sequences.Length, withSparse.Rt.Count);
                Assert.IsTrue(LineResidual(withSparse.Rt, hiOf, withSparse.Stats.RtMax) < unalignedResidual / 3);

                // Two acquisitions with one file name, from two folders, are two runs.
                string dayA = Path.Combine(folder, @"dayA"), dayB = Path.Combine(folder, @"dayB");
                Directory.CreateDirectory(dayA);
                Directory.CreateDirectory(dayB);
                var sameName = new[]
                {
                    OspreyTrainingExport.Read(WriteExport(dayA, @"run_q", sequences, MinutesA, (s, i) => i % 2 == 0)),
                    OspreyTrainingExport.Read(WriteExport(dayB, @"run_q", sequences, s => 1.05 * MinutesA(s) + 0.3, (s, i) => i % 2 == 1)),
                };
                var twoFolders = OspreyTrainingSet.Build(sameName, options);
                CollectionAssert.AreEqual(new[] { @"dayA/run_q", @"dayB/run_q" }, twoFolders.Alignment.Runs.Select(r => r.Run).ToArray());
                Assert.IsTrue(LineResidual(twoFolders.Rt, hiOf, twoFolders.Stats.RtMax) < unalignedResidual / 3);

                options.MinAlignmentPoints = sequences.Length + 1;
                var sparse = OspreyTrainingSet.Build(exports, options);
                Assert.IsNull(sparse.Alignment);
                StringAssert.Contains(sparse.Unaligned, @"run run_a has " + sequences.Length + @" peptide forms");
                Assert.AreEqual(unalignedResidual, LineResidual(sparse.Rt, hiOf, sparse.Stats.RtMax), 1e-12);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>The largest distance, in minutes, of a set's RT rows from their least-squares line in the peptides' HI.</summary>
        private static double LineResidual(IReadOnlyList<RtTrainingExample> rows, IReadOnlyDictionary<string, double> hiOf, double rtMax)
        {
            double[] x = rows.Select(r => hiOf[r.Peptide.Sequence]).ToArray();
            double[] y = rows.Select(r => r.RtNorm * rtMax).ToArray();
            double meanX = x.Average(), meanY = y.Average();
            double slope = x.Zip(y, (a, b) => (a - meanX) * (b - meanY)).Sum() / x.Sum(a => (a - meanX) * (a - meanX));
            return x.Zip(y, (a, b) => Math.Abs(b - (meanY + slope * (a - meanX)))).Max();
        }

        /// <summary>
        /// A run's training export: one clean 2+ precursor per sequence at the given apex RT, scoring best (run q 0.001
        /// against 0.005) where <paramref name="wins"/> says so.
        /// </summary>
        private static string WriteExport(string folder, string run, IReadOnlyList<string> sequences, Func<string, double> minutes,
            Func<string, int, bool> wins)
        {
            var records = new List<OspreyTrainingRecord>();
            for (int i = 0; i < sequences.Count; i++)
            {
                var record = OspreyTestRecords.CleanRecord(sequences[i], 2);
                record.EntryId = (uint)(i + 1);
                record.FileName = run;
                record.RunPrecursorQ = wins(sequences[i], i) ? 0.001 : 0.005;
                record.ApexRt = minutes(sequences[i]);
                records.Add(record);
            }
            var footer = new Dictionary<string, string>
            {
                { @"osprey.training_export.format_version", OspreyTrainingExport.FORMAT_VERSION },
                { @"osprey.rt_max", @"24" },
                { @"osprey.instrument_model", @"Orbitrap Astral" },
                { @"osprey.isolation_mz_min", @"380" },
                { @"osprey.isolation_mz_max", @"980" },
                { @"osprey.ms2_scan_window", @"150,2000" },
            };
            string path = Path.Combine(folder, run + OspreyTrainingExport.FILE_SUFFIX);
            OspreyTestRecords.WriteExport(path, records, footer, 16);
            return path;
        }
    }
}
