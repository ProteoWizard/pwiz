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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pwiz.Util.Chemistry;
using pwiz.Quasar.Features;
using pwiz.Quasar.Features.Hardklor;
using pwiz.Quasar.IO;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// The Hardklor port (ported from skyline-glimpse's HardklorTests, every assertion and expected value kept):
    /// its settings, averagine and model library, the isotope model, the ported CHardklor2 pieces, the finder on
    /// synthetic spectra, and agreement with Hardklor.exe on small.RAW.
    /// </summary>
    [TestClass]
    public class HardklorTest
    {
        private static readonly PwizEnvelopeModel MODEL = new PwizEnvelopeModel();

        /// <summary>Settings: Skyline's defaults, its idotp conversion, the peak widths, and validation.</summary>
        [TestMethod]
        public void TestHardklorSettings()
        {
            DefaultsAreSkylinesConfiguration();
            CorrelationFromIdotPIsSkylinesConversion();
            PeakWidthIsCalcFwhm();
            InvalidSettingsAreRejected();
            ExtremeValidSettingsAreAccepted();
        }

        /// <summary>Averagine, the model library, and the pwiz isotope model behind it.</summary>
        [TestMethod]
        public void TestHardklorModels()
        {
            AveragineIsHardklorsComposition();
            ModelLibraryBinsBy5MzAndChargeAndBuildsLazily();
            ModelLibraryConvertsToMzAtChargeAndScalesTo100();
            MonoisotopicMassOfChnos();
            PwizModelSumsFineStructureIntoNominalPeaks();
            PwizModelDropsPeaksBelowTheCutoffAndCaches();
        }

        /// <summary>The ported CHardklor2 pieces: BinarySearch, LinReg, QuickCharge, PeakMatcher, PeakMatcherB.</summary>
        [TestMethod]
        public void TestHardklorPortedPieces()
        {
            BinarySearchFindsTheNeighborOnOneSide();
            BinarySearchLongerSpectrum();
            LinRegIsTheCosine();
            LinRegUsesOnlyCount();
            QuickChargeFindsSpacingsOfStrongPeaks();
            QuickChargeIgnoresPeaksBelowAQuarterAndDuplicates();
            PeakMatcherMatchesAndScoresAndReportsOverlap();
            PeakMatcherKeepsTheClosestPeakWithinHalfAWidth();
            PeakMatcherCountsMissingStrongPeaksAsZero();
            PeakMatcherBStopsAtTheFirstMissingPeak();
        }

        /// <summary>The finder on synthetic spectra and small.RAW: envelopes found, settings obeyed, threads safe.</summary>
        [TestMethod]
        public void TestHardklorFinder()
        {
            FindSyntheticEnvelope();
            FindRespectsCorrelationAndChargeRange();
            FindIsSafeFromSeveralThreads();
            FindTwoEnvelopesAndHardklorsTieQuirk();
            FindKeepsAtMostDepthEnvelopesPerWindow();
            FindEmptyAndInvalidInput();
            FindDoesNotChangeItsInput();
        }

        /// <summary>
        /// Against Hardklor.exe's output on small.RAW (TestData/Hardklor/README.md): with Hardklor's isotope spacing
        /// the port decides exactly as Hardklor.exe does; with the pwiz model it agrees closely, and its own output
        /// is pinned.
        /// </summary>
        [TestMethod]
        public void TestHardklorMatchesHardklorExe()
        {
            DecidesExactlyAsHardklorExe();
            AgreesWithHardklorExe(@"small_orbitrap.hk", HardklorInstrument.orbitrap, 60000, 0.975, 0.97, 0.005,
                1049, 1021, 2848, 2924373L, 1270890.180633624, 100364860.61022949);
            AgreesWithHardklorExe(@"small_fticr_matthews.hk", HardklorInstrument.fticr, 100000, 0.975, 0.98, 0.001,
                1006, 992, 2718, 2829507L, 1203542.6905437617, 98438767.89886475);
        }

        // ---- Settings ----

        private static void DefaultsAreSkylinesConfiguration()
        {
            var s = new HardklorSettings();
            Assert.AreEqual(s, HardklorSettings.Default);
            Assert.AreEqual(HardklorInstrument.orbitrap, s.Instrument);
            Assert.AreEqual(60000.0, s.Resolution);
            Assert.AreEqual(1, s.MinCharge);
            Assert.AreEqual(7, s.MaxCharge);
            Assert.AreEqual(2, s.Depth);
            Assert.AreEqual(5000.0, s.MaxMoleculeMz);
            Assert.IsTrue(s.DistributionArea);
            // Skyline writes cos((1 - 0.9) * pi / 2) for its default idotp of 0.9.
            FeatureAssert.AreEqualRounded(0.98768834059513777, s.Correlation, 15);
        }

        private static void CorrelationFromIdotPIsSkylinesConversion()
        {
            var cases = new[]
            {
                (0.9, 0.98768834059513777),
                (0.5, 0.70710678118654757),
                (1.0, 1.0),
                (1.5, 1.0),  // clamped to 1, as Skyline does
                (0.0, 0.0),
            };
            foreach (var (idotp, expected) in cases)
                FeatureAssert.AreEqualRounded(expected, HardklorSettings.CorrelationFromIdotP(idotp), 12);
        }

        private static void PeakWidthIsCalcFwhm()
        {
            var cases = new[]
            {
                (HardklorInstrument.orbitrap, 60000.0, 400.0, 0.0066666666666667),  // 400^1.5 / (20 * 60000)
                (HardklorInstrument.orbitrap, 60000.0, 900.0, 0.0225),              // 900^1.5 / (20 * 60000)
                (HardklorInstrument.fticr, 100000.0, 400.0, 0.004),                 // 400^2 / (400 * 100000)
                (HardklorInstrument.fticr, 100000.0, 800.0, 0.016),
                (HardklorInstrument.tof, 20000.0, 400.0, 0.02),                     // 400 / 20000
                (HardklorInstrument.tof, 20000.0, 1000.0, 0.05),
                (HardklorInstrument.qit, 2.0, 400.0, 0.0004),                       // 2 / 5000 at any m/z
                (HardklorInstrument.qit, 2.0, 1200.0, 0.0004),
            };
            foreach (var (instrument, resolution, mz, expected) in cases)
            {
                FeatureAssert.AreEqualRounded(expected,
                    new HardklorSettings { Instrument = instrument, Resolution = resolution }.PeakWidth(mz), 12);
            }
        }

        private static void InvalidSettingsAreRejected()
        {
            var cases = new[]
            {
                (0, 7, 60000.0, 2, 5000.0),
                (3, 2, 60000.0, 2, 5000.0),
                (1, 101, 60000.0, 2, 5000.0),
                (1, 7, 0.0, 2, 5000.0),
                (1, 7, double.NaN, 2, 5000.0),
                (1, 7, 60000.0, 0, 5000.0),
                (1, 7, 60000.0, 2, 4.0),
            };
            foreach (var (minCharge, maxCharge, resolution, depth, maxMz) in cases)
            {
                var settings = new HardklorSettings
                {
                    MinCharge = minCharge, MaxCharge = maxCharge, Resolution = resolution, Depth = depth, MaxMoleculeMz = maxMz,
                };
                Assert.ThrowsException<ArgumentException>(() => new HardklorFinder(settings, MODEL));
            }
        }

        private static void ExtremeValidSettingsAreAccepted()
        {
            Assert.IsNotNull(new HardklorFinder(new HardklorSettings { MinCharge = 3, MaxCharge = 3 }, MODEL));
            var finder = new HardklorFinder(new HardklorSettings { MinCharge = 1, MaxCharge = 100, Depth = 1, MaxMoleculeMz = 5 }, MODEL);
            Assert.AreEqual(2, finder.Models.BinCount);
            Assert.ThrowsException<ArgumentNullException>(() => new HardklorFinder(null, MODEL));
            Assert.ThrowsException<ArgumentNullException>(() => new HardklorFinder(HardklorSettings.Default, null));
        }

        // ---- Averagine and the model library ----

        private static void AveragineIsHardklorsComposition()
        {
            var cases = new[]
            {
                // The formulas Hardklor.exe reported for these library bins (see TestData/Hardklor/small_fticr_matthews.hk).
                (54 * 5 * 6 - 1.007276466 * 6, new ElementCounts(72, 102, 20, 21, 1)),  // m/z 270, z 6: H102C72N20O21S1
                (71 * 5 * 3 - 1.007276466 * 3, new ElementCounts(47, 92, 13, 14, 0)),   // m/z 355, z 3: H92C47N13O14
                (5 - 1.007276466, new ElementCounts(0, 4, 0, 0, 0)),                     // bin 1, z 1: H4
                (1000.0, new ElementCounts(45, 84, 12, 13, 0)),
            };
            foreach (var (mass, expected) in cases)
                Assert.AreEqual(expected, Averagine.Compute(mass));
        }

        private static void ModelLibraryBinsBy5MzAndChargeAndBuildsLazily()
        {
            var model = new CountingModel();
            var library = new HardklorModelLibrary(model, 2, 4, 5000);
            Assert.AreEqual(1001, library.BinCount);
            Assert.AreSame(EnvelopeModel.EMPTY, library.Get(2, 4.9));   // bin 0 is empty, as in Hardklor
            Assert.IsNull(library.Get(2, 5005.0));                       // past molecule_max_mz
            Assert.AreSame(EnvelopeModel.EMPTY, library.Get(2, -1.0));  // (int)(-0.2) is bin 0, as in the C++
            Assert.IsNull(library.Get(2, -5.1));
            Assert.IsNotNull(library.Get(2, 5004.9));                    // the last bin

            var a = library.Get(3, 600.0);
            var b = library.Get(3, 604.99);
            Assert.AreSame(a, b);                                        // one bin, built once
            Assert.AreNotSame(a, library.Get(3, 605.0));
            Assert.AreNotSame(a, library.Get(2, 600.0));
            Assert.AreEqual(4, model.Calls);                             // 5004.9, 600 (z 3), 605 (z 3), 600 (z 2)
            Assert.AreEqual(Averagine.Compute(120 * 5 * 3 - 1.007276466 * 3), a.Formula);
        }

        private static void ModelLibraryConvertsToMzAtChargeAndScalesTo100()
        {
            var library = new HardklorModelLibrary(new FixedModel(), 1, 3, 5000);
            var m = library.Get(2, 600);
            // FixedModel: neutral masses 1000, 1001, 1002 at 0.5, 1, 0.25.
            FeatureAssert.AreSequenceEqual(new[] { 501.007276466, 501.507276466, 502.007276466 }, m.Mz.Select(v => Math.Round(v, 9)));
            FeatureAssert.AreSequenceEqual(new[] { 50f, 100f, 25f }, m.Intensity);
            Assert.AreEqual(1.75f, m.Area);
            Assert.AreEqual(3, m.Size);
            var formula = Averagine.Compute(120 * 5 * 2 - 1.007276466 * 2);
            FeatureAssert.AreEqualRounded(HardklorModelLibrary.MonoisotopicMass(formula), m.ZeroMass, 9);
        }

        private static void MonoisotopicMassOfChnos()
        {
            FeatureAssert.AreEqualRounded(12.0, HardklorModelLibrary.MonoisotopicMass(new ElementCounts(1, 0, 0, 0, 0)), 9);
            FeatureAssert.AreEqualRounded(18.0105646837, HardklorModelLibrary.MonoisotopicMass(new ElementCounts(0, 2, 0, 1, 0)), 9);
            FeatureAssert.AreEqualRounded(14.0030740048, HardklorModelLibrary.MonoisotopicMass(new ElementCounts(0, 0, 1, 0, 0)), 9);
            FeatureAssert.AreEqualRounded(31.972071, HardklorModelLibrary.MonoisotopicMass(new ElementCounts(0, 0, 0, 0, 1)), 9);
        }

        private static void PwizModelSumsFineStructureIntoNominalPeaks()
        {
            // C100: binomial with p(13C) = 0.01084 (Dwight Matthews' value, in place of pwiz's NIST 0.0107).
            var peaks = MODEL.Distribution(new ElementCounts(100, 0, 0, 0, 0), 0.01);
            FeatureAssert.AreEqualRounded(1200.0, peaks[0].Mass, 9);
            // Spaced by Monocle's 1.00286864 Da, not 13C's 1.0033548.
            FeatureAssert.AreEqualRounded(1201.00286864, peaks[1].Mass, 7);
            FeatureAssert.AreEqualRounded(1202.00573728, peaks[2].Mass, 7);
            // M+1 / M+0 = 100 p / (1 - p) = 1.0816, so M+1 is the most abundant (relative 1);
            // M+0 = (1 - p) / (100 p), M+2 / M+1 = 49.5 p / (1 - p).
            FeatureAssert.AreEqualRounded(0.91250922509225092, peaks[0].Abundance, 9);
            Assert.AreEqual(1.0, peaks[1].Abundance);
            FeatureAssert.AreEqualRounded(0.54246026932, peaks[2].Abundance, 9);
            FeatureAssert.AreEqualRounded(0.194194, peaks[3].Abundance, 5);   // 1617 p^2 / (1 - p)^2
            Assert.AreEqual(0.01084, PwizEnvelopeModel.CARBON13_ABUNDANCE);
            Assert.AreEqual(1.00286864, PwizEnvelopeModel.ISOTOPE_SPACING);
        }

        private static void PwizModelDropsPeaksBelowTheCutoffAndCaches()
        {
            var formula = Averagine.Compute(1200);
            var all = MODEL.Distribution(formula, 0.0001);
            var kept = MODEL.Distribution(formula, 0.05);
            Assert.IsTrue(all.Count > kept.Count);
            foreach (var p in kept)
                Assert.IsTrue(p.Abundance >= 0.05);
            Assert.AreSame(kept, MODEL.Distribution(formula, 0.05));
            FeatureAssert.IsEmpty(MODEL.Distribution(new ElementCounts(0, 0, 0, 0, 0), 0.01));
            // M+1 of averagine 1200: sum over ~54 C, 84 H, 15 N, 16 O; masses step about 1.0029 Da.
            FeatureAssert.AreEqualRounded(1.0029, all[1].Mass - all[0].Mass, 3);
        }

        // ---- Ported pieces ----

        private static void BinarySearchFindsTheNeighborOnOneSide()
        {
            var cases = new[]
            {
                (250.0, true, 1), (250.0, false, 2), (300.0, true, 2), (300.0, false, 2), (50.0, true, 0), (450.0, false, 3),
                (100.0, false, 0), (400.0, true, 3), (101.0, true, 0), (399.0, false, 3), (199.0, false, 1), (201.0, true, 1),
            };
            foreach (var (mz, floor, expected) in cases)
                Assert.AreEqual(expected, HardklorFinder.BinarySearch(new double[] { 100, 200, 300, 400 }, 4, mz, floor));
        }

        private static void BinarySearchLongerSpectrum()
        {
            var s = Enumerable.Range(0, 11).Select(i => 100.0 + 10 * i).ToArray(); // 100..200
            Assert.AreEqual(3, HardklorFinder.BinarySearch(s, s.Length, 135, true));
            Assert.AreEqual(4, HardklorFinder.BinarySearch(s, s.Length, 135, false));
            Assert.AreEqual(8, HardklorFinder.BinarySearch(s, s.Length, 181, true));
            Assert.AreEqual(9, HardklorFinder.BinarySearch(s, s.Length, 181, false));
            Assert.AreEqual(1, HardklorFinder.BinarySearch(s, s.Length, 110, true));
        }

        private static void LinRegIsTheCosine()
        {
            var cases = new[]
            {
                (new float[] { 100, 50 }, new float[] { 200, 100 }, 1.0),
                (new float[] { 3, 4 }, new float[] { 4, 3 }, 0.96),
                (new float[] { 100, 0 }, new float[] { 0, 100 }, 0.0),
                (new float[] { 0, 0 }, new float[] { 1, 2 }, 0.0),
                (new float[] { 1, 2 }, new float[] { 0, 0 }, 0.0),
                (new float[] { 1, 1, 1 }, new float[] { 1, 1, 0 }, 0.81649658092772603),
            };
            foreach (var (mer, obs, expected) in cases)
                FeatureAssert.AreEqualRounded(expected, HardklorFinder.LinReg(mer, obs, mer.Length), 6);
        }

        private static void LinRegUsesOnlyCount()
        {
            FeatureAssert.AreEqualRounded(1.0, HardklorFinder.LinReg(new float[] { 1, 2, 5 }, new float[] { 2, 4, 1 }, 2), 9);
            FeatureAssert.AreEqualRounded(0.0, HardklorFinder.LinReg(new float[] { -1, -2 }, new float[] { 1, 2 }, 2), 9);
        }

        private static void QuickChargeFindsSpacingsOfStrongPeaks()
        {
            var cases = new[]
            {
                (new[] { 500.0, 501.003 }, new[] { 2 }, new[] { 1 }),
                (new[] { 500.0, 500.5017 }, new[] { 2 }, new[] { 2 }),
                (new[] { 500.0, 500.3344 }, new[] { 2 }, new[] { 3 }),
                (new[] { 500.0, 500.1434 }, new[] { 2 }, new[] { 7 }),
                (new[] { 500.0, 500.125 }, new[] { 2 }, new int[0]),            // charge 8 is out of range
                (new[] { 500.0, 500.4 }, new[] { 2 }, new int[0]),              // 1/0.4 = 2.5: between charges
                (new[] { 500.0, 501.2 }, new[] { 2 }, new int[0]),              // beyond 1.1 m/z
                (new[] { 499.5, 500.0 }, new[] { 2 }, new int[0]),              // nothing to the right
                (new[] { 499.5, 500.0, 501.0 }, new[] { 2 }, new[] { 1, 2 }),   // left adds charges once right matched
                (new[] { 499.0, 499.6667, 500.0, 500.5 }, new[] { 2 }, new[] { 1, 2, 3 }),  // left: 0.333 -> 3, 1.0 -> 1
            };
            foreach (var (mz, indexOfBase, expected) in cases)
            {
                var inten = mz.Select(_ => 100f).ToArray();
                var charges = new List<int>();
                var index = Array.IndexOf(mz, 500.0);
                Assert.IsTrue(index >= 0 && indexOfBase.Length == 1);
                HardklorFinder.QuickCharge(mz, inten, mz.Length, index, 1, 7, new bool[8], charges);
                FeatureAssert.AreSequenceEqual(expected, charges);
            }
        }

        private static void QuickChargeIgnoresPeaksBelowAQuarterAndDuplicates()
        {
            var mz = new[] { 499.5, 500.0, 500.0, 500.5, 501.0 };
            var inten = new[] { 100f, 100f, 100f, 24.9f, 25f };
            var charges = new List<int>();
            HardklorFinder.QuickCharge(mz, inten, mz.Length, 1, 1, 7, new bool[8], charges);
            FeatureAssert.AreSequenceEqual(new[] { 1, 2 }, charges);   // 501.0 gives 1 on the right; 499.5 gives 2 on the left
            HardklorFinder.QuickCharge(mz, inten, mz.Length, 1, 2, 7, new bool[8], charges);
            FeatureAssert.IsEmpty(charges);                            // charge 1 out of range: nothing on the right
        }

        private static void PeakMatcherMatchesAndScoresAndReportsOverlap()
        {
            var vMR = new HardklorFinder.Aligned();
            vMR.Set(new EnvelopeModel(default, 0, new[] { 500.0, 500.5, 501.0, 501.5 }, new[] { 100f, 60f, 20f, 5f }, 1.85f), 0);
            var mz = new[] { 499.0, 500.001, 500.5, 501.0005, 501.5, 503 };
            var inten = new[] { 10f, 1000f, 600f, 200f, 1500f, 10f };
            var mask = new bool[6];
            mask[2] = true; // the base peak of an earlier envelope
            var match = new HardklorFinder.Match();
            var mer = new float[8];
            var obs = new float[8];
            var corr = HardklorFinder.PeakMatcher(vMR, mz, inten, 6, mask, 499.9, 501.6, 0.01, 1, match, out var overlap, mer, obs);
            FeatureAssert.AreSequenceEqual(new[] { 1, 2, 3, 4 }, match.Index);
            FeatureAssert.AreSequenceEqual(new[] { 1f, 0.6f, 0.2f, 0.05f }, match.Peak);
            Assert.AreEqual(2, overlap);             // matched at 60%, above 50
            // The last peak (5% expected, 1500 observed) pulls the correlation below 0.9 and is trimmed.
            Assert.AreEqual(3, match.Count);
            FeatureAssert.AreEqualRounded(1.0, corr, 6);

            mask[2] = false;
            mask[3] = true;                          // matched only at 20%: not an overlap
            HardklorFinder.PeakMatcher(vMR, mz, inten, 6, mask, 499.9, 501.6, 0.01, 1, match, out overlap, mer, obs);
            Assert.AreEqual(-1, overlap);

            // A poor last peak that is expected above 50% is not trimmed.
            vMR.Set(new EnvelopeModel(default, 0, new[] { 500.0, 500.5, 501.0, 501.5 }, new[] { 100f, 60f, 55f, 51f }, 2.66f), 0);
            corr = HardklorFinder.PeakMatcher(vMR, mz, inten, 6, new bool[6], 499.9, 501.6, 0.01, 1, match, out _, mer, obs);
            Assert.AreEqual(4, match.Count);
            // mer (100, 60, 55, 51) . obs (1000, 600, 200, 1500)
            FeatureAssert.AreEqualRounded((100.0 * 1000 + 60 * 600 + 55 * 200 + 51 * 1500)
                / Math.Sqrt((100.0 * 100 + 60 * 60 + 55 * 55 + 51 * 51) * (1000.0 * 1000 + 600 * 600 + 200 * 200 + 1500 * 1500)), corr, 6);
        }

        private static void PeakMatcherKeepsTheClosestPeakWithinHalfAWidth()
        {
            var vMR = new HardklorFinder.Aligned();
            vMR.Set(new EnvelopeModel(default, 0, new[] { 500.0, 500.5 }, new[] { 100f, 50f }, 1.5f), 0);
            // Two candidates near 500.5: 500.497 is closer than 500.504; 499.992 is just outside 0.005 of 500.0.
            var mz = new[] { 499.992, 500.0004, 500.497, 500.504 };
            var inten = new[] { 50f, 100f, 50f, 70f };
            var match = new HardklorFinder.Match();
            var corr = HardklorFinder.PeakMatcher(vMR, mz, inten, 4, new bool[4], 499.9, 500.6, 0.005, 1, match, out _,
                new float[4], new float[4]);
            FeatureAssert.AreSequenceEqual(new[] { 1, 2 }, match.Index);
            FeatureAssert.AreEqualRounded(1.0, corr, 6);
            // Starting from the right end, the left search still finds the closest.
            HardklorFinder.PeakMatcher(vMR, mz, inten, 4, new bool[4], 499.9, 500.6, 0.005, 3, match, out _,
                new float[4], new float[4]);
            FeatureAssert.AreSequenceEqual(new[] { 1, 2 }, match.Index);
            // Bounds limit the search: with upper below 500.49 the second peak is missing (below 50%, ignored).
            corr = HardklorFinder.PeakMatcher(vMR, mz, inten, 4, new bool[4], 499.9, 500.49, 0.005, 1, match, out _,
                new float[4], new float[4]);
            FeatureAssert.AreSequenceEqual(new[] { 1 }, match.Index);
            Assert.AreEqual(0.0, corr);
        }

        private static void PeakMatcherCountsMissingStrongPeaksAsZero()
        {
            var vMR = new HardklorFinder.Aligned();
            vMR.Set(new EnvelopeModel(default, 0, new[] { 500.0, 500.5, 501.0 }, new[] { 70f, 100f, 60f }, 2.3f), 0);
            // No peak at 500.5 (100%): it counts as an observed zero and stops the match.
            var mz = new[] { 500.0, 501.0 };
            var inten = new[] { 700f, 600f };
            var match = new HardklorFinder.Match();
            var corr = HardklorFinder.PeakMatcher(vMR, mz, inten, 2, new bool[2], 499.9, 501.1, 0.01, 0, match, out _,
                new float[4], new float[4]);
            FeatureAssert.AreSequenceEqual(new[] { 0 }, match.Index);
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual(0.0, corr);              // fewer than two matched peaks

            // Solved peaks (intensity below 0.1) match but count as zero.
            mz = new[] { 500.0, 500.5, 501.0 };
            inten = new[] { -700f, 1000f, 600f };
            corr = HardklorFinder.PeakMatcher(vMR, mz, inten, 3, new bool[3], 499.9, 501.1, 0.01, 1, match, out _,
                new float[4], new float[4]);
            FeatureAssert.AreSequenceEqual(new[] { 0, 1, 2 }, match.Index);
            Assert.AreEqual(2, match.Count);
            // mer (70, 100, 60) . obs (0, 1000, 600)
            FeatureAssert.AreEqualRounded((100.0 * 1000 + 60 * 600) / Math.Sqrt((70.0 * 70 + 100 * 100 + 60 * 60) * (1000.0 * 1000 + 600 * 600)), corr, 6);
        }

        private static void PeakMatcherBStopsAtTheFirstMissingPeak()
        {
            var vMR = new HardklorFinder.Aligned();
            vMR.Set(new EnvelopeModel(default, 0, new[] { 500.0, 500.5, 501.0, 501.5 }, new[] { 100f, 60f, 20f, 10f }, 1.9f), 0);
            var mz = new[] { 500.0, 500.5, 501.5 };
            var inten = new[] { 1000f, 600f, 100f };
            var match = new HardklorFinder.Match();
            var corr = HardklorFinder.PeakMatcherB(vMR, mz, inten, 3, 499.9, 501.6, 0.01, 0, match, new float[4], new float[4]);
            FeatureAssert.AreSequenceEqual(new[] { 0, 1 }, match.Index);
            Assert.AreEqual(2, match.Count);
            FeatureAssert.AreEqualRounded(1.0, corr, 6);

            // A poor third peak is trimmed while the correlation is below 0.9.
            mz = new[] { 500.0, 500.5, 501.0 };
            inten = new[] { 1000f, 100f, 1000f };
            corr = HardklorFinder.PeakMatcherB(vMR, mz, inten, 3, 499.9, 501.6, 0.01, 0, match, new float[4], new float[4]);
            FeatureAssert.AreSequenceEqual(new[] { 0, 1, 2 }, match.Index);
            Assert.AreEqual(2, match.Count);
            FeatureAssert.AreEqualRounded((100.0 * 1000 + 60 * 100) / Math.Sqrt((100.0 * 100 + 60 * 60) * (1000.0 * 1000 + 100 * 100)), corr, 6);
        }

        // ---- The finder on synthetic spectra ----

        /// <summary>An averagine-like envelope at charge 2 from m/z 600.3, among noise.</summary>
        private static (double[] Mz, double[] Intensity) SyntheticSpectrum()
        {
            var peaks = new List<(double, double)>
            {
                (450.2, 300), (590.11, 500), (599.0, 200),
                (600.3, 100000), (600.3 + 1.00290 / 2, 64000), (600.3 + 2 * 1.00290 / 2, 25500), (600.3 + 3 * 1.00290 / 2, 7400),
                (612.7, 800), (700.4, 150),
            };
            return (peaks.Select(p => p.Item1).ToArray(), peaks.Select(p => p.Item2).ToArray());
        }

        private static void FindSyntheticEnvelope()
        {
            var finder = new HardklorFinder(HardklorSettings.Default, MODEL);
            var (mz, intensity) = SyntheticSpectrum();
            var found = finder.Find(mz, intensity);
            var e = FeatureAssert.Single(found);
            Assert.AreEqual(2, e.Charge);
            Assert.AreEqual(600.3, e.BasePeakMz);
            FeatureAssert.AreSequenceEqual(new[] { 3, 4, 5, 6 }, e.PeakIndices);
            // The monoisotopic peak is the base peak: 2 * (600.3 - proton).
            FeatureAssert.AreEqualRounded(1198.585447, e.MonoMass, 3);
            Assert.IsTrue(e.Correlation > 0.999);
            Assert.AreEqual(599.0, e.LowMz);
            Assert.AreEqual(612.7, e.HighMz);
            // distribution_area: base peak times the model's summed relative abundance (about 1.95 here).
            FeatureAssert.InRange(e.Intensity, 190000f, 200000f);

            var heights = new HardklorFinder(HardklorSettings.Default with { DistributionArea = false }, MODEL).Find(mz, intensity);
            Assert.AreEqual(100000f, FeatureAssert.Single(heights).Intensity);
        }

        private static void FindRespectsCorrelationAndChargeRange()
        {
            var (mz, intensity) = SyntheticSpectrum();
            FeatureAssert.IsEmpty(new HardklorFinder(HardklorSettings.Default with { MinCharge = 3 }, MODEL).Find(mz, intensity));
            FeatureAssert.IsEmpty(new HardklorFinder(HardklorSettings.Default with { Correlation = 0.99999 }, MODEL).Find(mz, intensity));
            // M+2 at 2000 instead of 25500: correlation 0.982, below Skyline's 0.9877.
            intensity[5] = 2000;
            FeatureAssert.IsEmpty(new HardklorFinder(HardklorSettings.Default, MODEL).Find(mz, intensity));
            FeatureAssert.Single(new HardklorFinder(HardklorSettings.Default with { Correlation = 0.97 }, MODEL).Find(mz, intensity));
        }

        private static void FindIsSafeFromSeveralThreads()
        {
            var finder = new HardklorFinder(HardklorSettings.Default with { Instrument = HardklorInstrument.fticr, Resolution = 100000 }, MODEL);
            var spectra = SmallRaw.Ms1();
            var serial = spectra.Select(s => Describe(new HardklorFinder(finder.Settings, MODEL).Find(s.Mz, s.Intensity))).ToArray();
            var parallel = new string[spectra.Count * 8];
            Parallel.For(0, parallel.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                i => parallel[i] = Describe(finder.Find(spectra[i % spectra.Count].Mz, spectra[i % spectra.Count].Intensity)));
            for (var i = 0; i < parallel.Length; i++)
                Assert.AreEqual(serial[i % spectra.Count], parallel[i]);
        }

        private static string Describe(IReadOnlyList<IsotopeEnvelope> found) => string.Join("\n", found.Select(e =>
            FormattableString.Invariant($"{e.MonoMass:R} {e.Charge} {e.Intensity:R} {e.Correlation:R} {string.Join(",", e.PeakIndices)}")));

        private static void FindTwoEnvelopesAndHardklorsTieQuirk()
        {
            // Two copies of the synthetic envelope (without its noise), 3 m/z apart: the same library bin.
            var rel = new[] { 100000.0, 64000, 25500, 7400 };
            double[] Mz(double start) => rel.Select((_, k) => start + k * 1.00290 / 2).ToArray();
            var mz = Mz(600.3).Concat(Mz(603.3)).ToArray();
            var finder = new HardklorFinder(HardklorSettings.Default, MODEL);

            // The second copy 20% weaker: both found, in m/z order, with their own peaks.
            var found = finder.Find(mz, rel.Concat(rel.Select(v => v * 0.8)).ToArray());
            Assert.AreEqual(2, found.Count);
            FeatureAssert.AreSequenceEqual(new[] { 600.3, 603.3 }, found.Select(e => e.BasePeakMz));
            FeatureAssert.AreSequenceEqual(new[] { 0, 1, 2, 3 }, found[0].PeakIndices);
            FeatureAssert.AreSequenceEqual(new[] { 4, 5, 6, 7 }, found[1].PeakIndices);

            // Equal intensities: Hardklor takes the next peak strictly below the last one, so the second copy's
            // base peak, as intense as the first's, is never a starting point.
            found = finder.Find(mz, rel.Concat(rel).ToArray());
            Assert.IsFalse(found.Any(e => e.BasePeakMz == 603.3));
            Assert.AreEqual(600.3, found[0].BasePeakMz);
        }

        private static void FindKeepsAtMostDepthEnvelopesPerWindow()
        {
            // Three interleaved envelopes at charge 2 whose base peaks (600.3, 600.4, 600.7) lie within 0.5 m/z:
            // RefineHits keeps the Depth most intense.
            var peaks = new List<(double Mz, double Intensity)>();
            void Add(double start, double scale)
            {
                var rel = new[] { 100.0, 64, 25.5, 7.4 };
                for (var k = 0; k < rel.Length; k++)
                    peaks.Add((start + k * 1.00290 / 2, rel[k] * scale));
            }
            Add(600.30, 1000);
            Add(600.40, 800);
            Add(600.70, 600);
            peaks.Sort();
            var mz = peaks.Select(p => p.Mz).ToArray();
            var intensity = peaks.Select(p => p.Intensity).ToArray();
            var depth2 = new HardklorFinder(HardklorSettings.Default, MODEL).Find(mz, intensity);
            var depth3 = new HardklorFinder(HardklorSettings.Default with { Depth = 3 }, MODEL).Find(mz, intensity);
            var depth1 = new HardklorFinder(HardklorSettings.Default with { Depth = 1 }, MODEL).Find(mz, intensity);
            FeatureAssert.AreSequenceEqual(new[] { 600.30, 600.40, 600.70 }, depth3.Select(e => e.BasePeakMz));
            FeatureAssert.AreSequenceEqual(new[] { 600.30, 600.40 }, depth2.Select(e => e.BasePeakMz));
            FeatureAssert.AreSequenceEqual(new[] { 600.30 }, depth1.Select(e => e.BasePeakMz));
        }

        private static void FindEmptyAndInvalidInput()
        {
            var finder = new HardklorFinder(HardklorSettings.Default, MODEL);
            FeatureAssert.IsEmpty(finder.Find(Array.Empty<double>(), Array.Empty<double>()));
            FeatureAssert.IsEmpty(finder.Find(new[] { 500.0 }, new[] { 1000.0 }));
            FeatureAssert.IsEmpty(finder.Find(new[] { 500.0, 500.5 }, new[] { 0.5, 0.25 }));   // all below 1: no low point
            Assert.ThrowsException<ArgumentException>(() => finder.Find(new[] { 500.0 }, Array.Empty<double>()));
            Assert.ThrowsException<ArgumentNullException>(() => finder.Find(null, Array.Empty<double>()));
            Assert.ThrowsException<ArgumentNullException>(() => finder.Find(Array.Empty<double>(), null));
        }

        private static void FindDoesNotChangeItsInput()
        {
            var finder = new HardklorFinder(HardklorSettings.Default, MODEL);
            var (mz, intensity) = SyntheticSpectrum();
            var copy = intensity.ToArray();
            finder.Find(mz, intensity);
            FeatureAssert.AreSequenceEqual(copy, intensity);
        }

        // ---- Against Hardklor.exe ----

        /// <summary>One envelope line of a Hardklor .hk file (its correlation, column 8, is not compared).</summary>
        private sealed record HkPeptide(int Scan, double MonoMass, int Charge, double Intensity, double BaseMz);

        private static List<HkPeptide> ReadHk(string path)
        {
            var result = new List<HkPeptide>();
            var scan = 0;
            foreach (var line in File.ReadLines(path))
            {
                var f = line.Split('\t');
                if (f[0] == "S")
                {
                    scan = int.Parse(f[1], CultureInfo.InvariantCulture);
                }
                else if (f[0] == "P")
                {
                    result.Add(new HkPeptide(scan, double.Parse(f[1], CultureInfo.InvariantCulture), int.Parse(f[2], CultureInfo.InvariantCulture),
                        double.Parse(f[3], CultureInfo.InvariantCulture), double.Parse(f[4], CultureInfo.InvariantCulture)));
                }
            }
            return result;
        }

        private static string HkPath(string name) => SmallRaw.TestDataPath("Hardklor", name);

        /// <summary>
        /// The port decides exactly as Hardklor.exe does: given Hardklor's isotope peak masses and the same
        /// abundances (small_fticr_matthews.hk), it finds the same envelopes, on the same base peaks with the same
        /// charges. Every difference left in <see cref="AgreesWithHardklorExe"/> is the isotope model's.
        /// </summary>
        private static void DecidesExactlyAsHardklorExe()
        {
            var expected = ReadHk(HkPath("small_fticr_matthews.hk")).Select(h => (h.Scan, h.Charge, h.BaseMz)).Order().ToList();
            var finder = new HardklorFinder(HardklorSettings.Default with { Instrument = HardklorInstrument.fticr, Resolution = 100000 }, new MercurySpacedModel());
            var ours = SmallRaw.Ms1().SelectMany(s => finder.Find(s.Mz, s.Intensity).Select(e => (s.Scan, e.Charge, e.BasePeakMz))).Order().ToList();
            Assert.AreEqual(1014, expected.Count);
            Assert.AreEqual(expected.Count, ours.Count);
            // Hardklor.exe writes the base peak to 4 decimals.
            foreach (var (first, second) in expected.Zip(ours))
            {
                Assert.IsTrue(first.Scan == second.Scan && first.Charge == second.Charge
                    && Math.Abs(first.BaseMz - second.BasePeakMz) <= 0.00005, $"{first} against {second}");
            }
        }

        /// <remarks>
        /// small_orbitrap.hk is Skyline's configuration. Hardklor.exe and PwizEnvelopeModel use the same 13C
        /// abundance (0.01084, Dwight Matthews) but place isotope peaks differently: Hardklor's Mercury spaces them
        /// by about 1.0021 Da, PwizEnvelopeModel by Monocle's 1.00286864 Da (closer to observed isotope peaks), so a
        /// few envelopes near the matching tolerance or the correlation threshold fall on different sides of it, and
        /// each difference changes what is left of the spectrum for the next envelope. Measured: Hardklor.exe 1043,
        /// ours 1049, 97.8% of Hardklor.exe's found and 97.3% of ours matched. small_fticr_matthews.hk gave
        /// Hardklor.exe PwizEnvelopeModel's exact abundances (TestData/Hardklor/README.md): 97.8% and 98.6%, with
        /// identical intensities. With Hardklor's spacing the port finds exactly Hardklor.exe's envelopes (see
        /// <see cref="DecidesExactlyAsHardklorExe"/>), so these differences are the isotope model's alone.
        /// <para>The last values pin this port's own output (envelopes, matched, peaks, sum of peak indices, sum
        /// of masses, sum of intensities), recorded in skyline-glimpse once it agreed with Hardklor.exe as above:
        /// the agreement thresholds alone let small changes to the algorithm through. A reader that centroids
        /// small.RAW differently changes them too; re-record them only after checking the agreement.</para>
        /// </remarks>
        private static void AgreesWithHardklorExe(string hkName, HardklorInstrument instrument, double resolution,
            double minRecall, double minPrecision, double intensityTolerance,
            int pinnedCount, int pinnedMatched, int pinnedPeaks, long pinnedIndexSum, double pinnedMassSum, double pinnedIntensitySum)
        {
            var expected = ReadHk(HkPath(hkName));
            var finder = new HardklorFinder(HardklorSettings.Default with { Instrument = instrument, Resolution = resolution }, MODEL);
            var spectra = SmallRaw.Ms1();
            Assert.AreEqual(14, spectra.Count);
            Assert.AreEqual(expected.Select(p => p.Scan).Distinct().Count(), spectra.Count);

            var ours = new List<HkPeptide>();
            long peakCount = 0, peakIndexSum = 0;
            foreach (var (scan, mz, intensity) in spectra)
            {
                foreach (var e in finder.Find(mz, intensity))
                {
                    ours.Add(new HkPeptide(scan, e.MonoMass, e.Charge, e.Intensity, e.BasePeakMz));
                    peakCount += e.PeakIndices.Length;
                    peakIndexSum += e.PeakIndices.Sum();
                }
            }
            var massSum = ours.Sum(o => o.MonoMass);
            var intensitySum = ours.Sum(o => o.Intensity);

            var byScan = ours.ToLookup(p => p.Scan);
            var matched = expected.Where(h => byScan[h.Scan].Any(o => Same(h, o))).ToList();
            var expectedByScan = expected.ToLookup(p => p.Scan);
            var oursMatched = ours.Count(o => expectedByScan[o.Scan].Any(h => Same(h, o)));
            var recall = (double)matched.Count / expected.Count;
            var precision = (double)oursMatched / ours.Count;

            // Mass offsets and intensity ratios of the matched pairs, and how many share the base peak.
            var ppm = new List<double>();
            var intensityRatio = new List<double>();
            var sameBase = 0;
            foreach (var h in matched)
            {
                var o = byScan[h.Scan].Where(x => Same(h, x)).MinBy(x => Math.Abs(x.MonoMass - h.MonoMass));
                ppm.Add((o.MonoMass - h.MonoMass) / h.MonoMass * 1e6);
                intensityRatio.Add(o.Intensity / h.Intensity);
                if (Math.Abs(o.BaseMz - h.BaseMz) < 1e-3)
                    sameBase++;
            }
            ppm.Sort();
            intensityRatio.Sort();
            Console.WriteLine(FormattableString.Invariant(
                $"{hkName}: Hardklor.exe {expected.Count}, ours {ours.Count}; Hardklor.exe's found {matched.Count} ({recall:P1}), ours matched {oursMatched} ({precision:P1}); same base peak {sameBase}"));
            Console.WriteLine(FormattableString.Invariant(
                $"  peaks {peakCount}, index sum {peakIndexSum}, mass sum {massSum:R}, intensity sum {intensitySum:R}"));
            Console.WriteLine(FormattableString.Invariant(
                $"  mass offset ppm: 5% {Pct(ppm, 0.05):F2}, median {Pct(ppm, 0.5):F2}, 95% {Pct(ppm, 0.95):F2}; intensity ratio: 5% {Pct(intensityRatio, 0.05):F4}, median {Pct(intensityRatio, 0.5):F4}, 95% {Pct(intensityRatio, 0.95):F4}"));

            Assert.IsTrue(recall >= minRecall, $"recall {recall:P1}");
            Assert.IsTrue(precision >= minPrecision, $"precision {precision:P1}");
            Assert.IsTrue(sameBase >= 0.99 * matched.Count, $"same base peak {sameBase} of {matched.Count}");
            // Masses agree to a fraction of a ppm (Mercury's peak masses are close to, not exactly, the true ones).
            FeatureAssert.InRange(Pct(ppm, 0.05), -0.5, 0);
            FeatureAssert.InRange(Pct(ppm, 0.5), -0.2, 0.2);
            FeatureAssert.InRange(Pct(ppm, 0.95), 0, 0.5);
            FeatureAssert.InRange(Pct(intensityRatio, 0.05), 1 - 2 * intensityTolerance, 1.0001);
            FeatureAssert.InRange(Pct(intensityRatio, 0.95), 1 - intensityTolerance, 1 + intensityTolerance);

            Assert.AreEqual(pinnedCount, ours.Count);
            Assert.AreEqual(pinnedMatched, oursMatched);
            Assert.AreEqual(pinnedPeaks, peakCount);
            Assert.AreEqual(pinnedIndexSum, peakIndexSum);
            Assert.AreEqual(pinnedMassSum, massSum, pinnedMassSum * 1e-12);
            Assert.AreEqual(pinnedIntensitySum, intensitySum, pinnedIntensitySum * 1e-9);
        }

        private static bool Same(HkPeptide a, HkPeptide b) =>
            a.Scan == b.Scan && a.Charge == b.Charge && Math.Abs(a.MonoMass - b.MonoMass) <= a.MonoMass * 10e-6;

        private static double Pct(List<double> v, double p) => v[(int)(p * (v.Count - 1))];

        /// <summary>
        /// PwizEnvelopeModel's abundances at the masses Hardklor's Mercury gives them in its fast mode
        /// (CMercury8::Mercury and ConvertMass): evenly spaced by sqrt(mass variance / integer-mass variance),
        /// anchored so the mean is the average mass. Only to check this port against Hardklor.exe exactly.
        /// </summary>
        private sealed class MercurySpacedModel : IEnvelopeModel
        {
            private readonly PwizEnvelopeModel _inner = new PwizEnvelopeModel();

            public IReadOnlyList<IsotopePeak> Distribution(ElementCounts f, double cutoff)
            {
                var peaks = _inner.Distribution(f, cutoff);
                double mw = 0, intMw = 0, molVar = 0, intVar = 0, mono = 0;
                foreach (var (element, n) in new[]
                {
                    (ElementType.C, f.C), (ElementType.H, f.H), (ElementType.N, f.N), (ElementType.O, f.O), (ElementType.S, f.S),
                })
                {
                    var isotopes = PwizEnvelopeModel.Isotopes(element).Where(i => i.Abundance > 0).ToArray();
                    double m1 = isotopes.Sum(i => i.Abundance * i.Mass), m2 = isotopes.Sum(i => i.Abundance * i.Mass * i.Mass);
                    double r1 = isotopes.Sum(i => i.Abundance * Math.Round(i.Mass)), r2 = isotopes.Sum(i => i.Abundance * Math.Round(i.Mass) * Math.Round(i.Mass));
                    mw += n * m1;
                    intMw += n * r1;
                    molVar += n * (m2 - m1 * m1);
                    intVar += n * (r2 - r1 * r1);
                    mono += n * isotopes.Min(i => i.Mass);
                }
                var ratio = intVar == 0 ? 1 : Math.Sqrt(molVar) / Math.Sqrt(intVar);
                var monoInt = Math.Round(mono);
                return peaks.Select(p => new IsotopePeak(mw + (monoInt + Math.Round(p.Mass - mono) - intMw) * ratio, p.Abundance)).ToArray();
            }
        }

        /// <summary>Counts the distributions asked for; returns a fixed one.</summary>
        private sealed class CountingModel : IEnvelopeModel
        {
            public int Calls;

            public IReadOnlyList<IsotopePeak> Distribution(ElementCounts formula, double abundanceCutoff)
            {
                Interlocked.Increment(ref Calls);
                return new[] { new IsotopePeak(1000, 1) };
            }
        }

        /// <summary>Neutral masses 1000, 1001, 1002 at relative abundances 0.5, 1, 0.25, whatever the formula.</summary>
        private sealed class FixedModel : IEnvelopeModel
        {
            public IReadOnlyList<IsotopePeak> Distribution(ElementCounts formula, double abundanceCutoff) =>
                new[] { new IsotopePeak(1000, 0.5), new IsotopePeak(1001, 1), new IsotopePeak(1002, 0.25) };
        }
    }
}
