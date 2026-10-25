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
    /// A real-data slice for each demultiplexing path, its output pinned to a committed golden (Data/Demux/README.md):
    /// an Orbitrap Astral staggered slice through Osprey's <c>--demux auto</c>, a Thermo Stellar SuperOverlap profile
    /// slice through the staggered joint solve and Centrix, and a SCIEX ZT Scan profile slice through the scanning joint
    /// solve. One line per output spectrum (its source scan, window, peak count, summed intensity and intensity-weighted
    /// m/z), so a refactor must reproduce every spectrum and a change to any output shows. A change meant to move them
    /// rewrites the goldens (<see cref="REBLESS_VARIABLE"/>) and says why in its commit.
    /// </summary>
    [TestClass]
    public class DemuxFixtureTest
    {
        private const string FOLDER = @"pwiz_tools/Osprey/Osprey.Test/Data/Demux";
        private const string REBLESS_VARIABLE = @"OSPREY_REBLESS_DEMUX_FIXTURE";
        private const string ASTRAL_SLICE = @"astral-me-staggered-slice.mzML";
        private const string ASTRAL_GOLDEN = @"astral-me-osprey-demux.golden.tsv";
        private const string STELLAR_SLICE = @"stellar-superoverlap-profile-slice.mzML";
        private const string STELLAR_GOLDEN = @"stellar-superoverlap-joint-demux.golden.tsv";
        private const string ZTSCAN_SLICE = @"ztscan-a1-profile-slice.mzML";
        private const string ZTSCAN_KERNEL = @"ztscan-a1-kernel.profile.tsv";
        private const string ZTSCAN_GOLDEN = @"ztscan-a1-joint-demux.golden.tsv";

        /// <summary>The summed intensities and mean m/z of a golden are compared to this relative tolerance.</summary>
        private const double RELATIVE_TOLERANCE = 1e-9;

        /// <summary>
        /// Orbitrap Astral, 4 Th windows staggered by 2 Th, through the search's own path: the parse writes the spectra
        /// cache and each spectrum's fill time and analyzer beside it, and <c>--demux auto</c> counts each spectrum's ions
        /// from them and writes the demultiplexed cache Osprey searches, two 2 Th bins per acquired spectrum.
        /// </summary>
        [TestMethod]
        public void TestDemuxAstralFixture()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"OspreyDemuxAstral" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(dir);
            try
            {
                string input = Path.Combine(dir, ASTRAL_SLICE);
                File.Copy(FixturePath(ASTRAL_SLICE), input);
                var ctx = new PipelineContext(new OspreyConfig { DemuxMode = DemuxMode.auto, NThreads = 4 },
                    new OspreyTask[0], null, null, null);
                var index = ScoringTaskShared.EnsureSpectraCache(input, false, out _, ctx);
                Assert.AreEqual(SpectraCache.GetDemuxCachePath(input), index.CachePath);

                // Every MS2 spectrum's fill time, and the Astral analyzer, recorded beside the spectra cache.
                string cachePath = SpectraCache.GetCachePath(input);
                var acquisition = AcquisitionSidecar.Load(AcquisitionSidecar.PathFor(cachePath), input);
                Assert.IsNotNull(acquisition);
                var acquired = SpectraCache.LoadSpectraCache(cachePath, input).Ms2Spectra;
                Assert.AreEqual(344, acquired.Count);
                Assert.AreEqual(acquired.Count, acquisition.Count);
                foreach (var spectrum in acquired)
                {
                    Assert.IsTrue(acquisition.TryGet(spectrum.ScanNumber, out double fillTimeMs, out string analyzer));
                    Assert.IsTrue(fillTimeMs > 0 && fillTimeMs <= 10.2, string.Format(@"scan {0} fill time", spectrum.ScanNumber));
                    Assert.AreEqual(MassAnalyzerKind.astral, IonResponse.KindOf(analyzer));
                }

                // 8 windows of 4 Th in two sets 2 Th apart, 9 bins of 2 Th.
                var scheme = Demultiplexer.DetectScheme(acquired);
                Assert.AreEqual(DemuxSchemeKind.overlapping, scheme.Kind);
                Assert.AreEqual(2, scheme.OverlapFactor);
                Assert.AreEqual(8, scheme.Windows.Count);
                Assert.AreEqual(9, scheme.Bins.Count);

                string descriptor = DemuxCacheBuilder.CreateParams(ctx.Config).Descriptor;
                StringAssert.Contains(descriptor, @"ions_from_fill_time=True");
                var demultiplexed = SpectraCache.LoadSpectraCache(index.CachePath, input, descriptor).Ms2Spectra;
                Assert.AreEqual(2 * acquired.Count, demultiplexed.Count);
                var lines = demultiplexed.Select((s, o) => Line(o, s.ScanNumber.ToString(CultureInfo.InvariantCulture),
                    s.IsolationWindow.LowerBound, s.IsolationWindow.UpperBound, s.Mzs, Widen(s.Intensities))).ToList();
                CheckGolden(ASTRAL_GOLDEN, lines);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// Thermo Stellar SuperOverlap, profile: 4 Th windows in four passes 1 Th apart, each acquired spectrum split
        /// among the four 1 Th bins it covers by the staggered joint solve, each share centroided by Centrix, as
        /// <c>Osprey.DemuxTool --scheme staggered --profile --joint --ions</c> runs it. Each spectrum's ions are counted
        /// from its fill time and the ion trap's ion response.
        /// </summary>
        [TestMethod]
        public void TestDemuxStellarFixture()
        {
            var read = SpectrumFileReader.LoadAllSpectra(FixturePath(STELLAR_SLICE));
            var ms2 = read.Ms2Spectra;
            Assert.AreEqual(200, ms2.Count);
            var acquisitions = Acquisitions(read, MassAnalyzerKind.ion_trap);

            var scheme = Demultiplexer.DetectScheme(ms2);
            Assert.AreEqual(DemuxSchemeKind.overlapping, scheme.Kind);
            Assert.AreEqual(4, scheme.OverlapFactor);
            Assert.AreEqual(8, scheme.Windows.Count);
            Assert.AreEqual(11, scheme.Bins.Count);

            var options = new ScanningDemuxOptions { Joint = true, IonsFromFillTime = true, Threads = 4 };
            var pipeline = new StaggeredDemuxPipeline(new SpectraDemuxSource(ms2, acquisitions), options, TextWriter.Null);
            Assert.AreEqual(4 * ms2.Count, pipeline.OutputCount);
            var lines = new List<string>();
            for (int o = 0; o < pipeline.OutputCount; o++)
            {
                var bin = pipeline.BinOf(o);
                var (mz, ions) = pipeline.Peaks(o);
                double perIon = pipeline.IntensityPerIon(o);
                lines.Add(Line(o, ms2[pipeline.BaseIndex(o)].ScanNumber.ToString(CultureInfo.InvariantCulture),
                    bin.LowerBound, bin.UpperBound, mz, ions.Select(v => v * perIon).ToArray()));
            }
            CheckGolden(STELLAR_GOLDEN, lines);
        }

        /// <summary>
        /// SCIEX ZT Scan, profile: six sweeps of 35 encoded bins (584.8-625.0 m/z, fragments 540-565 m/z) through the
        /// scanning joint solve with its own centroids and the run's measured transmission, as
        /// <c>Osprey.DemuxTool --joint --layout centered:7</c> runs it, the survey scans centroided by the same solve.
        /// The nine output bins (600.2-609.6 m/z) keep 4 context bins and the transmission's 9-bin reach on each side,
        /// and the middle three gather all seven positions of their layout.
        /// The counts of one ion come from the profile's lowest levels, as the tool takes them.
        /// </summary>
        [TestMethod]
        public void TestDemuxZtScanFixture()
        {
            var read = SpectrumFileReader.LoadAllSpectra(FixturePath(ZTSCAN_SLICE));
            Assert.AreEqual(6 * 35, read.Ms2Spectra.Count);
            Assert.AreEqual(6, read.Ms1Spectra.Count);
            var source = new SpectraDemuxSource(read.Ms2Spectra, null, read.Ms1Spectra);

            int half = read.Ms2Spectra.Count / 2;
            double countsPerIon = IonCalibration.CountsPerIon(read.Ms2Spectra.Skip(half).Take(50)
                .SelectMany(s => s.Intensities.Select(v => (double)v)), out double fit);
            Assert.IsTrue(fit >= IonCalibration.MIN_FIT, string.Format(@"counts per ion fit {0}", fit));
            Assert.AreEqual(99.665, countsPerIon, 1.0);

            var options = new ScanningDemuxOptions
            {
                Joint = true,
                JointMs1 = true,
                JointApportion = false,
                GroupBins = 48,
                ContextBins = 4,
                BlockCycles = 4,
                CyclePad = 1,
                Layout = ScanningLayout.Parse(@"centered:7"),
                MinMz = 599.6,
                MaxMz = 610.2,
                CountsPerIon = countsPerIon,
                Threads = 4,
            };
            var kernel = ScanningKernel.Load(FixturePath(ZTSCAN_KERNEL));
            var pipeline = new ScanningDemuxPipeline(source, kernel, options, TextWriter.Null);
            Assert.AreEqual(6, pipeline.CycleCount);
            Assert.AreEqual(6 * 10, pipeline.OutputCount);
            var lines = new List<string>();
            for (int o = 0; o < pipeline.OutputCount; o++)
            {
                int baseIndex = pipeline.BaseIndex(o);
                string scan = source.NativeId(baseIndex);
                if (pipeline.IsSurvey(o))
                {
                    source.Read(baseIndex, out var surveyMz, out var surveyCounts);
                    var centroids = pipeline.CentroidSurvey(surveyMz, surveyCounts);
                    Assert.IsTrue(centroids.HasValue, string.Format(@"survey scan {0}", scan));
                    lines.Add(Line(o, scan, 0, 0, centroids.Value.Mz, centroids.Value.Counts));
                    continue;
                }
                pipeline.WindowOf(o, out double low, out double high);
                var (mz, ions) = pipeline.Peaks(o);
                lines.Add(Line(o, scan, low, high, mz, ions));
            }
            CheckGolden(ZTSCAN_GOLDEN, lines);
        }

        private static string FixturePath(string name)
        {
            return Path.Combine(IOTest.FindPwizRoot(), FOLDER, name);
        }

        /// <summary>Each MS2 spectrum's fill time and its analyzer's ion response, all of the kind expected.</summary>
        private static SpectrumAcquisition[] Acquisitions(SpectrumFileResult read, MassAnalyzerKind expected)
        {
            var acquisitions = new SpectrumAcquisition[read.Ms2Spectra.Count];
            for (int i = 0; i < acquisitions.Length; i++)
            {
                uint scan = read.Ms2Spectra[i].ScanNumber;
                Assert.IsTrue(read.Acquisition.TryGet(scan, out double fillTimeMs, out string analyzer));
                Assert.IsTrue(fillTimeMs > 0, string.Format(@"scan {0} fill time", scan));
                var kind = IonResponse.KindOf(analyzer);
                Assert.AreEqual(expected, kind, string.Format(@"scan {0} analyzer", scan));
                acquisitions[i] = new SpectrumAcquisition(fillTimeMs, IonResponse.Of(kind));
            }
            return acquisitions;
        }

        private static double[] Widen(float[] values)
        {
            return values.Select(v => (double)v).ToArray();
        }

        /// <summary>
        /// One output spectrum: its index, source scan, window (0 for a survey scan), peak count, summed intensity and
        /// intensity-weighted mean m/z.
        /// </summary>
        private static string Line(int output, string scan, double low, double high, IReadOnlyList<double> mz,
            IReadOnlyList<double> intensity)
        {
            var ic = CultureInfo.InvariantCulture;
            double sum = 0, weighted = 0;
            for (int i = 0; i < mz.Count; i++)
            {
                sum += intensity[i];
                weighted += intensity[i] * mz[i];
            }
            return string.Join("\t", output.ToString(ic), scan, low.ToString(@"F4", ic), high.ToString(@"F4", ic),
                mz.Count.ToString(ic), sum.ToString(@"R", ic), (sum > 0 ? weighted / sum : 0).ToString(@"R", ic));
        }

        /// <summary>
        /// The output spectra against the committed golden: index, scan, window and peak count exactly, summed intensity
        /// and mean m/z to <see cref="RELATIVE_TOLERANCE"/>. With the rebless variable set, rewrites the golden and fails,
        /// so a changed output is never accepted silently.
        /// </summary>
        private static void CheckGolden(string name, IReadOnlyList<string> lines)
        {
            string path = FixturePath(name);
            var all = new List<string> { string.Join("\t", @"output", @"scan", @"window_lower", @"window_upper", @"peaks",
                @"intensity", @"mean_mz") };
            all.AddRange(lines);
            if (Environment.GetEnvironmentVariable(REBLESS_VARIABLE) == @"1")
            {
                File.WriteAllLines(path, all);
                Assert.Fail(@"Reblessed {0} ({1} was set); review the diff and commit it.", path, REBLESS_VARIABLE);
            }
            Assert.IsTrue(File.Exists(path), path);
            var expected = File.ReadAllLines(path).Where(l => l.Length > 0).ToArray();
            Assert.AreEqual(expected.Length, all.Count, @"output spectra");
            for (int i = 1; i < all.Count; i++)
            {
                var want = expected[i].Split('\t');
                var have = all[i].Split('\t');
                for (int f = 0; f < 5; f++)
                    Assert.AreEqual(want[f], have[f], string.Format(@"{0} line {1} field {2}", name, i, f));
                for (int f = 5; f < 7; f++)
                {
                    double wantValue = double.Parse(want[f], CultureInfo.InvariantCulture);
                    double haveValue = double.Parse(have[f], CultureInfo.InvariantCulture);
                    Assert.AreEqual(wantValue, haveValue, Math.Abs(wantValue) * RELATIVE_TOLERANCE,
                        string.Format(@"{0} line {1} field {2}", name, i, f));
                }
            }
        }
    }
}
