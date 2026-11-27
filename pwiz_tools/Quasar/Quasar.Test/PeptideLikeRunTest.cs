/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on skyline-glimpse (https://github.com/maccoss/skyline-glimpse)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Quasar.Features;
using pwiz.Quasar.Features.Data;
using pwiz.Quasar.Features.Hardklor;
using pwiz.Quasar.IO;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// The peptide-like signal with the real Hardklor port, by analyzer and on small.RAW (ported from
    /// skyline-glimpse's PeptideLikeRunTests, every assertion and expected value kept). Glimpse's
    /// BigRaw_PeptideLikeTiming, a timing run on a raw file named by GLIMPSE_BIG_RAW, is not ported: Quasar has
    /// no raw-file reader yet.
    /// </summary>
    [TestClass]
    public class PeptideLikeRunTest
    {
        /// <summary>Each analyzer's peak width, the scan's analyzer choosing it, and the search settings carried over.</summary>
        [TestMethod]
        public void TestAnalyzerEnvelopeFinder()
        {
            EachAnalyzerGetsItsPeakWidth();
            TheFinderUsesTheScansAnalyzer();
            TheChargeLimitLimitsHardklor();
        }

        /// <summary>
        /// small.RAW is DDA (MS2 on a few precursors): MS1 only, and of that only the FT scans: its ion trap full
        /// scans are not searched. The peptide-like signal is never more than the TIC.
        /// </summary>
        [TestMethod]
        public void TestPeptideLikeOnSmallRaw()
        {
            PeptideLikeSignal signal;
            ScanHeader[] headers;
            using (var observer = new PeptideLikeObserver(new AnalyzerEnvelopeFinder(new PwizEnvelopeModel()), workers: 4))
            {
                headers = ScanPass.Run(SmallRaw.Spectra, observer);
                Assert.IsTrue(observer.Completion.Wait(TimeSpan.FromMinutes(2)));
                signal = observer.Completion.Result;
            }

            var ms1Scans = ScanPass.IndicesOfLevel(headers, 1);
            var ft = ms1Scans.Where(i => headers[i].Analyzer != MassAnalyzer.ion_trap).ToArray();
            Assert.AreEqual(7, ft.Length);
            FeatureAssert.AreSequenceEqual(ft, signal.Series[0]);
            foreach (var i in ms1Scans.Except(ft))
                Assert.IsFalse(signal.Covers(i));
            var ms1 = ft.Select(i => signal.IntensityByScan()[i]).ToArray();
            var tic = ft.Select(i => headers[i].TotalIonCurrent).ToArray();
            Console.WriteLine($@"{signal.Features.Count} features; MS1 peptide-like/TIC: "
                + string.Join(@" ", ms1.Zip(tic, (p, t) => (p / t).ToString(@"0.00", CultureInfo.InvariantCulture))));
            Assert.AreNotEqual(0, signal.Features.Count);
            foreach (var (first, second) in ms1.Zip(tic))
                FeatureAssert.InRange(first, 0, second * 1.0000001);   // never more than the TIC
            Assert.IsTrue(ms1.Any(v => v > 0));
            Assert.IsTrue(signal.IntensityByScan(2).Zip(signal.IntensityByScan()).All(p => double.IsNaN(p.First) || p.First <= p.Second));
        }

        private static void EachAnalyzerGetsItsPeakWidth()
        {
            Assert.AreEqual((HardklorInstrument.orbitrap, 60000.0), Of(AnalyzerEnvelopeFinder.SettingsFor(MassAnalyzer.orbitrap)));
            Assert.AreEqual((HardklorInstrument.tof, 80000.0), Of(AnalyzerEnvelopeFinder.SettingsFor(MassAnalyzer.astral)));
            Assert.AreEqual((HardklorInstrument.orbitrap, 60000.0), Of(AnalyzerEnvelopeFinder.SettingsFor(MassAnalyzer.other)));
            Assert.AreEqual((HardklorInstrument.orbitrap, 60000.0), Of(AnalyzerEnvelopeFinder.SettingsFor(MassAnalyzer.unknown)));
            // Peak widths at m/z 800: Orbitrap 800^1.5 / (20 x 60000), Astral 800 / 80000.
            FeatureAssert.AreEqualRounded(0.018856, AnalyzerEnvelopeFinder.SettingsFor(MassAnalyzer.orbitrap).PeakWidth(800), 6);
            FeatureAssert.AreEqualRounded(0.01, AnalyzerEnvelopeFinder.SettingsFor(MassAnalyzer.astral).PeakWidth(800), 9);
            // An ion trap cannot resolve the isotopes of multiply charged ions: not searched.
            Assert.IsNull(AnalyzerEnvelopeFinder.SettingsFor(MassAnalyzer.ion_trap));
            var finder = new AnalyzerEnvelopeFinder(new PwizEnvelopeModel());
            Assert.IsFalse(finder.CanSearch(MassAnalyzer.ion_trap));
            Assert.IsTrue(finder.CanSearch(MassAnalyzer.orbitrap));
            Assert.IsTrue(finder.CanSearch(MassAnalyzer.astral));
            Assert.IsTrue(finder.CanSearch(MassAnalyzer.unknown));
        }

        private static (HardklorInstrument, double) Of(HardklorSettings s) => (s.Instrument, s.Resolution);

        private static void TheFinderUsesTheScansAnalyzer()
        {
            // A 2+ envelope whose isotopes sit 0.005 m/z off: inside the Orbitrap's peaks (0.012 wide at m/z 600),
            // outside the Astral's (0.0075).
            var mz = new[] { 600.3, 600.3 + 1.00290 / 2 + 0.005, 600.3 + 2 * 1.00290 / 2 + 0.005, 600.3 + 3 * 1.00290 / 2 + 0.005 };
            var intensity = new[] { 100000.0, 64000, 25500, 7400 };
            var finder = new AnalyzerEnvelopeFinder(new PwizEnvelopeModel());
            var orbitrap = finder.Find(mz, intensity, MassAnalyzer.orbitrap);
            Assert.AreEqual(2, FeatureAssert.Single(orbitrap).Charge);
            var expected = new HardklorFinder(AnalyzerEnvelopeFinder.SettingsFor(MassAnalyzer.orbitrap), new PwizEnvelopeModel()).Find(mz, intensity);
            Assert.AreEqual(expected.Single().MonoMass, orbitrap.Single().MonoMass);
            Assert.AreEqual(2, FeatureAssert.Single(finder.Find(mz, intensity, MassAnalyzer.other)).Charge);       // Orbitrap settings
            Assert.IsFalse(finder.Find(mz, intensity, MassAnalyzer.astral).Any(e => e.Charge == 2));
            FeatureAssert.IsEmpty(finder.Find(mz, intensity, MassAnalyzer.ion_trap));                               // not searched
        }

        private static void TheChargeLimitLimitsHardklor()
        {
            var mz = new[] { 600.3, 600.3 + 1.00290 / 2, 600.3 + 2 * 1.00290 / 2, 600.3 + 3 * 1.00290 / 2 };
            var intensity = new[] { 100000.0, 64000, 25500, 7400 };
            var model = new PwizEnvelopeModel();
            Assert.AreEqual(HardklorSettings.Default, new AnalyzerEnvelopeFinder(model).Search);
            Assert.AreEqual(2, FeatureAssert.Single(new AnalyzerEnvelopeFinder(model, HardklorSettings.Default with { MaxCharge = 7 })
                .Find(mz, intensity, MassAnalyzer.orbitrap)).Charge);
            var oneOnly = new AnalyzerEnvelopeFinder(model, HardklorSettings.Default with { MaxCharge = 1 });
            Assert.AreEqual(1, oneOnly.Search.MaxCharge);
            Assert.IsFalse(oneOnly.Find(mz, intensity, MassAnalyzer.orbitrap).Any(e => e.Charge > 1));
            Assert.IsFalse(oneOnly.Find(mz, intensity, MassAnalyzer.astral).Any(e => e.Charge > 1));
            // The search settings carry over; each analyzer sets its own peak width.
            var search = HardklorSettings.Default with { MaxCharge = 3, Depth = 4, Correlation = 0.95, Instrument = HardklorInstrument.fticr, Resolution = 1 };
            Assert.AreEqual(search with { Instrument = HardklorInstrument.tof, Resolution = 80000 }, AnalyzerEnvelopeFinder.SettingsFor(MassAnalyzer.astral, search));
            Assert.AreEqual(search with { Instrument = HardklorInstrument.orbitrap, Resolution = 60000 }, AnalyzerEnvelopeFinder.SettingsFor(MassAnalyzer.orbitrap, search));
            Assert.IsNull(AnalyzerEnvelopeFinder.SettingsFor(MassAnalyzer.ion_trap, search));
            // A stricter correlation than the envelope reaches: not found.
            FeatureAssert.IsEmpty(new AnalyzerEnvelopeFinder(model, HardklorSettings.Default with { Correlation = 0.99999 }).Find(mz, intensity, MassAnalyzer.orbitrap));
        }
    }
}
