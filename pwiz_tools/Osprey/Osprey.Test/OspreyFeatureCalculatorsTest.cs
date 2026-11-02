/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for the modular scoring SPI (<see cref="IOspreyFeatureCalculator"/> /
    /// <see cref="OspreyScoringContext"/> / <see cref="OspreyFeatureCalculators"/>),
    /// which mirrors Skyline's peak-scoring calculator model. These exercise the
    /// extracted calculators directly -- a capability the inline
    /// <c>ScoreCandidate</c> feature block did not have.
    /// </summary>
    [TestClass]
    public class OspreyFeatureCalculatorsTest
    {
        private const double TOLERANCE = 1e-6;

        /// <summary>
        /// Peak-shape family (peak_apex / peak_area / peak_sharpness): values come
        /// from the reference XIC (highest total intensity), apex is a direct
        /// lookup at the supplied apex index, area is the trapezoid over
        /// [start, end), sharpness is the mean of the left/right slopes.
        /// </summary>
        [TestMethod]
        public void TestPeakShapeCalculators()
        {
            var rts = new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            // frag1 has the higher total intensity (212 > 110), so it is the
            // reference XIC; its apex at scan 3 is value 90.
            var frag0 = new double[] { 0, 0, 0, 0, 5, 20, 60, 20, 5, 0 };
            var frag1 = new double[] { 0, 10, 50, 90, 50, 10, 2, 0, 0, 0 };
            var xics = new List<XicData>
            {
                new XicData(0, rts, frag0),
                new XicData(1, rts, frag1),
            };
            var bounds = new XICPeakBounds { StartIndex = 1, EndIndex = 5, ApexIndex = 3 };
            var peakData = new FakePeakData(xics, bounds);

            var context = new OspreyScoringContext(null);
            context.ClearByproducts();

            double apex = OspreyFeatureCalculators.Get(3).Calculate(context, peakData);
            double area = OspreyFeatureCalculators.Get(4).Calculate(context, peakData);
            double sharpness = OspreyFeatureCalculators.Get(5).Calculate(context, peakData);

            // The three intensity-scale features are log-conditioned (log10(x + 1))
            // so a heavy intensity tail cannot dominate the experiment-wide Percolator
            // standardizer; raw values below are the pre-log quantities.
            // peak_apex: reference XIC (frag1) intensity at apex index 3 = 90 -> log10(91).
            Assert.AreEqual(Math.Log10(91.0), apex, TOLERANCE);
            // peak_area: trapezoid over [1,5) with dt = 1:
            // (10+50)/2 + (50+90)/2 + (90+50)/2 + (50+10)/2 = 30+70+70+30 = 200 -> log10(201).
            Assert.AreEqual(Math.Log10(201.0), area, TOLERANCE);
            // peak_sharpness: left (90-10)/(3-1)=40, right (90-10)/(5-3)=40, mean 40 -> log10(41).
            Assert.AreEqual(Math.Log10(41.0), sharpness, TOLERANCE);

            // The three calculators expose the parity-critical PIN names.
            Assert.AreEqual("peak_apex", OspreyFeatureCalculators.Get(3).Name);
            Assert.AreEqual("peak_area", OspreyFeatureCalculators.Get(4).Name);
            Assert.AreEqual("peak_sharpness", OspreyFeatureCalculators.Get(5).Name);

            // Degenerate peak data (no XICs) yields 0.0 for every peak-shape feature.
            var empty = new FakePeakData(new List<XicData>(), bounds);
            context.ClearByproducts();
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(3).Calculate(context, empty), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(4).Calculate(context, empty), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(5).Calculate(context, empty), TOLERANCE);

            // Negative sharpness: the supplied apex (index 3, value 10) sits BELOW both
            // edges (90) -- possible because the apex is a CWT/override lookup, not the
            // XIC max. Raw mean slope = ((10-90)/2 + (10-90)/2) / 2 = -40. peak_sharpness
            // floors that at 0 before the log (log10(max(-40,0)+1) = 0) rather than
            // producing a non-finite value from log10 of a negative argument.
            var invFrag = new double[] { 0, 90, 50, 10, 50, 90, 2, 0, 0, 0 };
            var invPeak = new FakePeakData(
                new List<XicData> { new XicData(0, rts, invFrag) }, bounds);
            context.ClearByproducts();
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(5).Calculate(context, invPeak), TOLERANCE);

            // An all-zero XIC is a VALID peak (the reference exists) whose three features
            // all condition to exactly 0.0 -- log10(0 + 1) -- which is the same value the
            // invalid-peak sentinel above produces. That collision is intended: "no signal"
            // and "no peak" are the same thing to the SVM. Pinned here so a future change to
            // the sentinel (or to the +1 offset) has to face the question deliberately.
            var zeroFrag = new double[10];
            var zeroPeak = new FakePeakData(
                new List<XicData> { new XicData(0, rts, zeroFrag) }, bounds);
            context.ClearByproducts();
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(3).Calculate(context, zeroPeak), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(4).Calculate(context, zeroPeak), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(5).Calculate(context, zeroPeak), TOLERANCE);

            // The three log-conditioned features carry "(log10)" in their display label, so
            // the feature-contribution report cannot present a per-decade weight as though
            // it were per intensity unit.
            Assert.AreEqual(OspreyScoringResources.PeakApexCalc_DisplayName_Peak_apex_intensity__log10_, OspreyFeatureCalculators.Get(3).DisplayName);
            Assert.AreEqual(OspreyScoringResources.PeakAreaCalc_DisplayName_Peak_area__log10_, OspreyFeatureCalculators.Get(4).DisplayName);
            Assert.AreEqual(OspreyScoringResources.PeakSharpnessCalc_DisplayName_Peak_sharpness__log10_, OspreyFeatureCalculators.Get(5).DisplayName);
        }

        /// <summary>
        /// Coelution family (fragment_coelution_sum / _max / n_coeluting_fragments):
        /// all three are served from one shared pairwise-Pearson pass over the peak
        /// range, published once to the context byproduct cache.
        /// </summary>
        [TestMethod]
        public void TestCoelutionCalculators()
        {
            // Two perfectly-correlated fragments (frag1 = 10 * frag0) over the peak
            // range -> a single pairwise correlation, positive and identical.
            var rts = new double[] { 0, 1, 2, 3, 4 };
            var frag0 = new double[] { 1, 2, 3, 2, 1 };
            var frag1 = new double[] { 10, 20, 30, 20, 10 };
            var xics = new List<XicData>
            {
                new XicData(0, rts, frag0),
                new XicData(1, rts, frag1),
            };
            var bounds = new XICPeakBounds { StartIndex = 0, EndIndex = 4, ApexIndex = 2 };
            var peakData = new FakePeakData(xics, bounds);

            double expectedCorr = ScoringMath.PearsonCorrelationInRange(frag0, frag1, 0, 4);
            Assert.IsTrue(expectedCorr > 0.0, "fixture fragments should positively correlate");

            var context = new OspreyScoringContext(null);
            context.ClearByproducts();
            double sum = OspreyFeatureCalculators.Get(0).Calculate(context, peakData);
            double max = OspreyFeatureCalculators.Get(1).Calculate(context, peakData);
            double nCoeluting = OspreyFeatureCalculators.Get(2).Calculate(context, peakData);

            // One pair -> sum == max == that correlation; both fragments have a mean
            // pairwise correlation > 0, so n_coeluting = 2.
            Assert.AreEqual(expectedCorr, sum, TOLERANCE);
            Assert.AreEqual(expectedCorr, max, TOLERANCE);
            Assert.AreEqual(2.0, nCoeluting, TOLERANCE);

            Assert.AreEqual("fragment_coelution_sum", OspreyFeatureCalculators.Get(0).Name);
            Assert.AreEqual("fragment_coelution_max", OspreyFeatureCalculators.Get(1).Name);
            Assert.AreEqual("n_coeluting_fragments", OspreyFeatureCalculators.Get(2).Name);

            // Fewer than two fragments -> all coelution features 0.
            var single = new FakePeakData(
                new List<XicData> { new XicData(0, rts, frag0) }, bounds);
            context.ClearByproducts();
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(0).Calculate(context, single), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(1).Calculate(context, single), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(2).Calculate(context, single), TOLERANCE);
        }

        /// <summary>
        /// Median-polish family defaults: when no fit is published (peakLen &lt; 3 or
        /// a non-convergent Compute), each calculator returns its family-specific
        /// default -- critically median_polish_residual_ratio is 1.0, the others 0.0.
        /// A shared "return 0.0" would corrupt feature 16.
        /// </summary>
        [TestMethod]
        public void TestMedianPolishCalculatorDefaults()
        {
            var rts = new double[] { 0, 1, 2 };
            var frag0 = new double[] { 1, 2, 1 };
            var peakData = new FakePeakData(
                new List<XicData> { new XicData(0, rts, frag0) },
                new XICPeakBounds { StartIndex = 0, EndIndex = 2, ApexIndex = 1 });

            // No MedianPolishByproduct published -> each calculator uses its default.
            var context = new OspreyScoringContext(null);
            context.ClearByproducts();
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(15).Calculate(context, peakData), TOLERANCE);
            Assert.AreEqual(1.0, OspreyFeatureCalculators.Get(16).Calculate(context, peakData), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(19).Calculate(context, peakData), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(20).Calculate(context, peakData), TOLERANCE);

            Assert.AreEqual("median_polish_cosine", OspreyFeatureCalculators.Get(15).Name);
            Assert.AreEqual("median_polish_residual_ratio", OspreyFeatureCalculators.Get(16).Name);
            Assert.AreEqual("median_polish_min_fragment_r2", OspreyFeatureCalculators.Get(19).Name);
            Assert.AreEqual("median_polish_residual_correlation", OspreyFeatureCalculators.Get(20).Name);
        }

        /// <summary>
        /// RT-deviation family: rt_deviation = apex RT - expected RT (signed), and
        /// abs_rt_deviation = its absolute value.
        /// </summary>
        [TestMethod]
        public void TestRtDeviationCalculators()
        {
            var context = new OspreyScoringContext(null);
            context.ClearByproducts();

            var late = new FakePeakData(new List<XicData>(), new XICPeakBounds(),
                apexRetentionTime: 12.5, expectedRt: 10.0);
            Assert.AreEqual(2.5, OspreyFeatureCalculators.Get(11).Calculate(context, late), TOLERANCE);
            Assert.AreEqual(2.5, OspreyFeatureCalculators.Get(12).Calculate(context, late), TOLERANCE);

            // Earlier-than-expected apex -> negative deviation, positive absolute.
            var early = new FakePeakData(new List<XicData>(), new XICPeakBounds(),
                apexRetentionTime: 8.0, expectedRt: 10.0);
            Assert.AreEqual(-2.0, OspreyFeatureCalculators.Get(11).Calculate(context, early), TOLERANCE);
            Assert.AreEqual(2.0, OspreyFeatureCalculators.Get(12).Calculate(context, early), TOLERANCE);

            Assert.AreEqual("rt_deviation", OspreyFeatureCalculators.Get(11).Name);
            Assert.AreEqual("abs_rt_deviation", OspreyFeatureCalculators.Get(12).Name);
        }

        /// <summary>
        /// MS1 family (ms1_precursor_coelution / ms1_isotope_cosine): both features
        /// are HRAM-only and now pure consumers of MS1 data produced upstream by the
        /// extractor. When no MS1 data is supplied (the peak-data accessors are null,
        /// i.e. a unit-resolution run or no MS1 scan), both return exactly 0.0. The
        /// numeric path (calibration, reference-XIC pick, nearest-MS1 sampling,
        /// isotope envelope) lives in the extractor and is covered by the end-to-end
        /// 1e-9 cross-impl parity gate against the Rust reference on the HRAM datasets.
        /// </summary>
        [TestMethod]
        public void TestMs1Calculators()
        {
            var rts = new double[] { 0, 1, 2, 3, 4 };
            var frag0 = new double[] { 1, 2, 3, 2, 1 };
            var peakData = new FakePeakData(
                new List<XicData> { new XicData(0, rts, frag0) },
                new XICPeakBounds { StartIndex = 0, EndIndex = 4, ApexIndex = 2 },
                candidate: new LibraryEntry(1, "PEPTIDE", "PEPTIDE", 2, 500.0, 10.0));

            // The fake supplies no MS1 data (Ms1PrecursorXic / ApexIsotopeEnvelope
            // null), so both features return 0.0 without any MS1 work.
            var context = new OspreyScoringContext(null);
            context.ClearByproducts();
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(13).Calculate(context, peakData), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(14).Calculate(context, peakData), TOLERANCE);

            Assert.AreEqual("ms1_precursor_coelution", OspreyFeatureCalculators.Get(13).Name);
            Assert.AreEqual("ms1_isotope_cosine", OspreyFeatureCalculators.Get(14).Name);
        }

        /// <summary>
        /// Apex-match family: consecutive_ions (longest matched b/y ordinal run),
        /// explained_intensity (matched / total apex intensity), and the signed /
        /// absolute mean fragment mass error. The mass-accuracy trio share one
        /// ApexFragmentMatchSet pass; the no-match abs fallback is the live
        /// FragmentTolerance.Tolerance, NOT 0.0 (the historical ~65-row Astral bug).
        /// </summary>
        [TestMethod]
        public void TestApexMatchCalculators()
        {
            // Apex MS2 spectrum: four sorted peaks, total intensity 100.
            var apex = new Spectrum
            {
                Mzs = new[] { 100.0, 200.0, 300.0, 400.0 },
                Intensities = new[] { 10f, 20f, 30f, 40f },
            };
            // Da tolerance so the expected arithmetic is exact and easy to read.
            var config = new OspreyConfig { FragmentTolerance = FragmentToleranceConfig.UnitResolution(0.5) };

            // b1/b2 and y1 match (consecutive b-run of 2); y2 has no peak.
            var candidate = new LibraryEntry(1, "PEPTIDE", "PEPTIDE", 2, 500.0, 10.0)
            {
                Fragments = new List<LibraryFragment>
                {
                    Frag(100.05, IonType.B, 1),
                    Frag(200.0, IonType.B, 2),
                    Frag(300.0, IonType.Y, 1),
                    Frag(999.0, IonType.Y, 2),
                },
            };
            var peakData = new FakePeakData(new List<XicData>(), new XICPeakBounds(),
                candidate: candidate, apexSpectrum: apex);

            var context = new OspreyScoringContext(config);
            context.ClearByproducts();

            // consecutive_ions: b-ordinals {1,2} -> run 2; y-ordinals {1} -> run 1.
            Assert.AreEqual(2.0, OspreyFeatureCalculators.Get(7).Calculate(context, peakData), TOLERANCE);
            // explained_intensity: matched (10+20+30) / total 100 = 0.60.
            Assert.AreEqual(0.60, OspreyFeatureCalculators.Get(8).Calculate(context, peakData), TOLERANCE);
            // mass_accuracy_deviation_mean: signed errors (-0.05, 0, 0) / 3.
            Assert.AreEqual(-0.05 / 3.0, OspreyFeatureCalculators.Get(9).Calculate(context, peakData), TOLERANCE);
            // abs_mass_accuracy_deviation_mean: absolute errors (0.05, 0, 0) / 3.
            Assert.AreEqual(0.05 / 3.0, OspreyFeatureCalculators.Get(10).Calculate(context, peakData), TOLERANCE);

            Assert.AreEqual("consecutive_ions", OspreyFeatureCalculators.Get(7).Name);
            Assert.AreEqual("explained_intensity", OspreyFeatureCalculators.Get(8).Name);
            Assert.AreEqual("mass_accuracy_deviation_mean", OspreyFeatureCalculators.Get(9).Name);
            Assert.AreEqual("abs_mass_accuracy_deviation_mean", OspreyFeatureCalculators.Get(10).Name);

            // No matched fragments: consecutive / explained / signed-mean are 0.0,
            // but the abs mean falls back to the live tolerance (0.5), NOT 0.0.
            var noMatch = new LibraryEntry(1, "PEPTIDE", "PEPTIDE", 2, 500.0, 10.0)
            {
                Fragments = new List<LibraryFragment> { Frag(999.0, IonType.B, 1) },
            };
            var noMatchData = new FakePeakData(new List<XicData>(), new XICPeakBounds(),
                candidate: noMatch, apexSpectrum: apex);
            var noMatchContext = new OspreyScoringContext(config);
            noMatchContext.ClearByproducts();
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(7).Calculate(noMatchContext, noMatchData), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(8).Calculate(noMatchContext, noMatchData), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(9).Calculate(noMatchContext, noMatchData), TOLERANCE);
            Assert.AreEqual(0.5, OspreyFeatureCalculators.Get(10).Calculate(noMatchContext, noMatchData), TOLERANCE);
        }

        /// <summary>
        /// Xcorr + Savitzky-Golay family (xcorr / sg_weighted_xcorr /
        /// sg_weighted_cosine). Uses an <see cref="IResolutionStrategy"/> fake whose
        /// ScoreXcorr returns the spectrum index, so the SG-weighted sum, the
        /// apex+/-2 window, the candidate-local -> global mapping, and the asymmetric
        /// edge skip are analytically checkable. Cosine is 0 here (fragment-less
        /// candidate), which also exercises the empty-fragment guard.
        /// </summary>
        [TestMethod]
        public void TestXcorrSgCalculators()
        {
            // 20 placeholder spectra so any window-global index in range resolves.
            var windowSpectra = new List<Spectrum>();
            for (int i = 0; i < 20; i++)
                windowSpectra.Add(new Spectrum { Mzs = new double[0], Intensities = new float[0] });

            // Fragment-less candidate: sg_weighted_cosine collapses to 0 (empty-frag
            // guard), isolating the xcorr-side weighting under test.
            var candidate = new LibraryEntry(1, "PEPTIDE", "PEPTIDE", 2, 500.0, 10.0);
            var config = new OspreyConfig { FragmentTolerance = FragmentToleranceConfig.UnitResolution(0.5) };

            var context = new OspreyScoringContext(config);
            context.SetWindow(new IndexEchoResolution(), null, null, null);

            // Interior apex: startScan=10, apexLocal=5, rangeLen=8 -> candIdx 3..7 all
            // in range, globalIdx 13..17. ScoreXcorr echoes the index.
            var interior = new FakePeakData(new List<XicData>(), new XICPeakBounds(),
                candidate: candidate, apexSpectrum: windowSpectra[15],
                apexGlobalIndex: 15, apexLocalIndex: 5, windowStartIndex: 10,
                windowLength: 8, windowSpectra: windowSpectra);
            context.ClearByproducts();

            // xcorr (6) = ScoreXcorr at ApexGlobalIndex 15.
            Assert.AreEqual(15.0, OspreyFeatureCalculators.Get(6).Calculate(context, interior), TOLERANCE);
            // sg_weighted_xcorr (17): weights .* [13,14,15,16,17] = 15.0 (SG weights
            // sum to 1, so a linear score returns the center value).
            Assert.AreEqual(15.0, OspreyFeatureCalculators.Get(17).Calculate(context, interior), TOLERANCE);
            // sg_weighted_cosine (18) = 0 (fragment-less candidate).
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(18).Calculate(context, interior), TOLERANCE);

            Assert.AreEqual("xcorr", OspreyFeatureCalculators.Get(6).Name);
            Assert.AreEqual("sg_weighted_xcorr", OspreyFeatureCalculators.Get(17).Name);
            Assert.AreEqual("sg_weighted_cosine", OspreyFeatureCalculators.Get(18).Name);

            // Edge apex: apexLocal=0 -> offsets -2,-1 skip (candIdx<0); offsets 0,1,2
            // -> globalIdx 10,11,12. NO renormalization: only those weights apply.
            // 10*(17/35) + 11*(12/35) + 12*(-3/35) = 266/35 = 7.6.
            var edge = new FakePeakData(new List<XicData>(), new XICPeakBounds(),
                candidate: candidate, apexSpectrum: windowSpectra[10],
                apexGlobalIndex: 10, apexLocalIndex: 0, windowStartIndex: 10,
                windowLength: 8, windowSpectra: windowSpectra);
            var edgeContext = new OspreyScoringContext(config);
            edgeContext.SetWindow(new IndexEchoResolution(), null, null, null);
            edgeContext.ClearByproducts();
            Assert.AreEqual(266.0 / 35.0, OspreyFeatureCalculators.Get(17).Calculate(edgeContext, edge), TOLERANCE);
        }

        /// <summary>
        /// The production <see cref="OspreyPeakData.TryGetApexOffsetSpectrum"/> bounded
        /// accessor (tested directly, not via the fake's copy): interior offsets map
        /// candidate-local to window-global (cacheIndex = windowStartIndex +
        /// apexLocalIndex + offset) and return the window spectrum there; offsets that
        /// fall outside [0, windowLength) at a window edge return false -- the
        /// asymmetric skip the Savitzky-Golay sweep relies on.
        /// </summary>
        [TestMethod]
        public void TestApexOffsetSpectrumAccessor()
        {
            var windowSpectra = new List<Spectrum>();
            for (int i = 0; i < 20; i++)
                windowSpectra.Add(new Spectrum { Mzs = new double[0], Intensities = new float[0] });

            // startScan=10, apexLocal=5, rangeLen=8 -> candIdx 3..7 valid (globalIdx 13..17).
            var peakData = new OspreyPeakData();
            peakData.Set(null, new XICPeakBounds(), new List<XicData>(),
                0.0, 0.0, windowSpectra[15],
                apexGlobalIndex: 15, apexLocalIndex: 5, windowStartIndex: 10, windowLength: 8,
                windowSpectra: windowSpectra);
            for (int offset = -2; offset <= 2; offset++)
            {
                Assert.IsTrue(peakData.TryGetApexOffsetSpectrum(offset, out var s, out int idx),
                    string.Format("offset {0} should be in range", offset));
                Assert.AreEqual(15 + offset, idx);
                Assert.AreSame(windowSpectra[15 + offset], s);
            }

            // Lower edge: apexLocal=0 -> offsets -2,-1 fall below 0 and are skipped;
            // 0,1,2 resolve to globalIdx 10,11,12.
            var edge = new OspreyPeakData();
            edge.Set(null, new XICPeakBounds(), new List<XicData>(),
                0.0, 0.0, windowSpectra[10],
                apexGlobalIndex: 10, apexLocalIndex: 0, windowStartIndex: 10, windowLength: 8,
                windowSpectra: windowSpectra);
            Assert.IsFalse(edge.TryGetApexOffsetSpectrum(-2, out _, out _));
            Assert.IsFalse(edge.TryGetApexOffsetSpectrum(-1, out _, out _));
            Assert.IsTrue(edge.TryGetApexOffsetSpectrum(0, out _, out int i0));
            Assert.AreEqual(10, i0);
            Assert.IsTrue(edge.TryGetApexOffsetSpectrum(2, out _, out int i2));
            Assert.AreEqual(12, i2);

            // Upper edge: apexLocal=7 at the top of an 8-wide range -> candIdx 8 (offset
            // +1) equals windowLength and is skipped.
            var upper = new OspreyPeakData();
            upper.Set(null, new XICPeakBounds(), new List<XicData>(),
                0.0, 0.0, windowSpectra[17],
                apexGlobalIndex: 17, apexLocalIndex: 7, windowStartIndex: 10, windowLength: 8,
                windowSpectra: windowSpectra);
            Assert.IsTrue(upper.TryGetApexOffsetSpectrum(0, out _, out _));
            Assert.IsFalse(upper.TryGetApexOffsetSpectrum(1, out _, out _));
        }

        /// <summary>
        /// <see cref="OspreyFeatureCalculators.BuildFeatureInfos"/> must merge the
        /// caller's PIN feature names with the calculators by index: each info's name is
        /// the supplied <see cref="ParquetScoreCache.PIN_FEATURE_NAMES"/> entry, while
        /// its label and reversed-score direction come from the calculator at that same
        /// index. Validating each field against its own source (the names array, then the
        /// calculator) confirms the by-index merge without re-encoding the per-feature
        /// label / direction data as a second oracle that would just break on any
        /// deliberate calculator edit.
        /// </summary>
        [TestMethod]
        public void TestBuildFeatureInfos()
        {
            var names = ParquetScoreCache.PIN_FEATURE_NAMES;
            var infos = OspreyFeatureCalculators.BuildFeatureInfos(names);

            Assert.AreEqual(OspreyFeatureCalculators.FeatureCount, infos.Length);
            Assert.AreEqual(names.Length, infos.Length);
            for (int i = 0; i < infos.Length; i++)
            {
                var calc = OspreyFeatureCalculators.Get(i);
                Assert.AreEqual(names[i], infos[i].Name,
                    string.Format("Name[{0}] should be the supplied PIN name", i));
                Assert.AreEqual(calc.DisplayName, infos[i].Label,
                    string.Format("Label[{0}] should be calculator {1}'s DisplayName", i, calc.Name));
                Assert.AreEqual(calc.IsReversedScore, infos[i].IsReversedScore,
                    string.Format("IsReversedScore[{0}] should be calculator {1}'s direction", i, calc.Name));
            }

            // The model-diagnostics report label is the invariant (English) one in every UI language,
            // while the console label follows the language.
            OspreyFeatureInfo[] invariant;
            using (new CultureScope(CultureInfo.InvariantCulture))
                invariant = OspreyFeatureCalculators.BuildFeatureInfos(names);
            OspreyFeatureInfo[] japanese;
            using (new CultureScope(CultureInfo.GetCultureInfo(@"ja")))
                japanese = OspreyFeatureCalculators.BuildFeatureInfos(names);
            for (int i = 0; i < japanese.Length; i++)
                Assert.AreEqual(invariant[i].Label, japanese[i].ReportLabel, names[i]);
            Assert.IsTrue(Enumerable.Range(0, japanese.Length).Any(i => japanese[i].Label != japanese[i].ReportLabel),
                "the ja display labels should be translated");
        }

        /// <summary>
        /// Reference-correlation family: the reference is the fragment that correlates best with
        /// the others, so an interfered fragment that is also the most intense - the peak-shape
        /// reference - is not chosen, and the clean fragments still correlate with the reference.
        /// </summary>
        [TestMethod]
        public void TestReferenceCorrelationIgnoresInterferedTopFragment()
        {
            var rts = new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0 };
            var profile = new[] { 1.0, 5.0, 20.0, 40.0, 20.0, 5.0, 1.0 };
            var xics = new List<XicData>();
            foreach (double scale in new[] { 10.0, 8.0, 6.0, 4.0 })
                xics.Add(new XicData(xics.Count, rts, profile.Select(v => v * scale + 1.0).ToArray()));
            // Most intense, but its signal rises across the peak instead of following the profile.
            xics.Add(new XicData(xics.Count, rts, new[] { 100.0, 300.0, 500.0, 700.0, 900.0, 1100.0, 1300.0 }));
            var peakData = new FakePeakData(xics, new XICPeakBounds { StartIndex = 0, EndIndex = 6, ApexIndex = 3 });

            var context = new OspreyScoringContext(null);
            var refCorr = ReferenceCorrelations.GetOrCompute(context, peakData);
            Assert.AreEqual(4, refCorr.SortedCorrelations.Length);
            Assert.IsTrue(refCorr.KthBest(3) > 0.99, string.Format("Clean fragments follow the reference, got {0:F4}", refCorr.KthBest(3)));
            Assert.IsTrue(refCorr.KthBest(4) < 0.5, string.Format("The interfered fragment does not, got {0:F4}", refCorr.KthBest(4)));
            Assert.AreEqual(3, refCorr.CountGood());
            Assert.AreEqual(0.0, refCorr.KthBest(5), TOLERANCE);
            // Apex: 401 + 321 + 241 + 161 + 700; without the top (700) the rest sum to 1124.
            Assert.AreEqual(Math.Log10(1.0 + 1124.0), refCorr.ApexLogIntensityExcludingTop, TOLERANCE);
            Assert.AreSame(refCorr, ReferenceCorrelations.GetOrCompute(context, peakData), "Computed once per candidate");

            // A one-scan spike on a clean fragment hurts the raw correlation more than the spike-filtered one.
            var spiked = xics.Take(4).Select(x => new XicData(x.FragmentIndex, rts, (double[]) x.Intensities.Clone())).ToList();
            spiked[3].Intensities[5] = 400.0;
            var spikeData = new FakePeakData(spiked, new XICPeakBounds { StartIndex = 0, EndIndex = 6, ApexIndex = 3 });
            var spikeCorr = ReferenceCorrelations.GetOrCompute(new OspreyScoringContext(null), spikeData);
            Assert.IsTrue(spikeCorr.MinFilteredTopMean(3) > spikeCorr.SortedCorrelations.Average(),
                string.Format("Spike filtering should recover correlation: {0:F4} vs {1:F4}",
                    spikeCorr.MinFilteredTopMean(3), spikeCorr.SortedCorrelations.Average()));
        }

        /// <summary>
        /// Peak-profile family: width, a symmetric 5-bin shape that sums to 1, perfect apex-local
        /// correlation for fragments that share one profile, library intensities relative to the
        /// top fragment, and per-rank shares across the peak that follow the XIC scales.
        /// </summary>
        [TestMethod]
        public void TestPeakProfile()
        {
            var rts = new[] { 1.0, 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9 };
            var profile = new[] { 1.0, 5.0, 20.0, 40.0, 60.0, 60.0, 40.0, 20.0, 5.0, 1.0 };
            var scales = new[] { 2.0, 8.0, 4.0, 6.0 };   // XIC order differs from library rank order
            var library = new[] { 25f, 100f, 50f, 75f };  // so rank 1 is fragment 1, rank 2 fragment 3, ...
            var candidate = new LibraryEntry(1, "PEPTIDE", "PEPTIDE", 2, 500.0, 10.0)
            {
                Fragments = library.Select((v, i) => new LibraryFragment { Mz = 200 + 100 * i, RelativeIntensity = v }).ToList(),
            };
            var xics = scales.Select((s, i) => new XicData(i, rts, profile.Select(v => v * s).ToArray())).ToList();
            var bounds = new XICPeakBounds { StartIndex = 0, EndIndex = 9, ApexIndex = 4, StartRt = 1.0, EndRt = 1.9 };
            var peakData = new FakePeakData(xics, bounds, candidate: candidate);

            var context = new OspreyScoringContext(null);
            var peak = PeakProfile.GetOrCompute(context, peakData);
            Assert.AreEqual(0.9, peak.Width, TOLERANCE);
            Assert.AreEqual(1.0, peak.Shape.Sum(), TOLERANCE);
            Assert.AreEqual(peak.Shape[0], peak.Shape[4], TOLERANCE);
            Assert.AreEqual(peak.Shape[1], peak.Shape[3], TOLERANCE);
            Assert.IsTrue(peak.Shape[2] > peak.Shape[0]);
            Assert.AreEqual(1.0, peak.LocalCorrelationTop3Mean, TOLERANCE);
            CollectionAssert.AreEqual(new[] { 1.0, 0.75, 0.5, 0.25, 0.0, 0.0 }, peak.LibraryRelative);
            // Shares follow the XIC scales of fragments 1, 3, 2, 0: 8, 6, 4, 2 of 20.
            var expectedShares = new[] { 0.4, 0.3, 0.2, 0.1, 0.0, 0.0 };
            for (int r = 0; r < expectedShares.Length; r++)
                Assert.AreEqual(expectedShares[r], peak.WindowShares[r], TOLERANCE, string.Format("Rank {0}", r + 1));
            Assert.AreSame(peak, PeakProfile.GetOrCompute(context, peakData), "Computed once per candidate");
        }

        /// <summary>
        /// Time-correlation family: over apex +/- 0.15 min (15 scans at 0.01 min), fragments that
        /// follow one Gaussian profile correlate near 1 with the smoothed reference, a fragment that
        /// is a flat level with one spike does not, and fragments beyond the top six library ranks
        /// are summed apart from them.
        /// </summary>
        [TestMethod]
        public void TestTimeCorrelations()
        {
            const int n = 61;
            var rts = Enumerable.Range(0, n).Select(i => 10.0 + 0.01 * i).ToArray();
            var profile = rts.Select(rt => 1000.0 * Math.Exp(-Math.Pow((rt - 10.3) / 0.04, 2))).ToArray();
            var library = new[] { 100f, 90f, 80f, 70f, 60f, 50f, 40f, 30f };
            var candidate = new LibraryEntry(1, "PEPTIDE", "PEPTIDE", 2, 500.0, 10.0)
            {
                Fragments = library.Select((v, i) => new LibraryFragment { Mz = 200 + 100 * i, RelativeIntensity = v }).ToList(),
            };
            var xics = new List<XicData>();
            for (int f = 0; f < library.Length; f++)
            {
                double[] intensities;
                if (f == 2)
                {
                    // Library rank 3: a flat interference level with a one-scan spike far from the apex.
                    intensities = Enumerable.Repeat(500.0, n).ToArray();
                    intensities[20] = 900.0;
                }
                else
                    intensities = profile.Select(v => v * (1.0 - 0.05 * f) + 5.0).ToArray();
                xics.Add(new XicData(f, rts, intensities));
            }
            var bounds = new XICPeakBounds { StartIndex = 27, EndIndex = 33, ApexIndex = 30 };
            var timeCorr = TimeCorrelations.Compute(new FakePeakData(xics, bounds, candidate: candidate),
                TimeCorrelations.HALF_WINDOW_MINUTES);

            for (int r = 1; r <= TimeCorrelations.RANK_COUNT; r++)
            {
                double corr = timeCorr.RankCorrelations[r - 1];
                if (r == 3)
                    Assert.IsTrue(corr < 0.3, string.Format("The flat fragment does not follow the reference, got {0:F4}", corr));
                else
                    Assert.IsTrue(corr > 0.98, string.Format("Rank {0} follows the reference, got {1:F4}", r, corr));
            }
            Assert.AreEqual(timeCorr.RankCorrelations.Sum(), timeCorr.TopSum, TOLERANCE);
            Assert.IsTrue(timeCorr.ResidualSum > 1.96, string.Format("Two clean residual fragments, got {0:F4}", timeCorr.ResidualSum));
            Assert.AreEqual(timeCorr.ResidualSum / 2, timeCorr.ResidualMean, TOLERANCE);
        }

        /// <summary>
        /// Re-extracted-correlation family over synthetic window spectra: the M+1 isotopes of the top
        /// three fragments (one of them doubly charged) co-elute and score near 1, the absent ones 0;
        /// rank 6 is missing at its own m/z but has a co-eluting interferer 0.6 x tolerance away, which
        /// the full-tolerance XIC picks up and the 0.45 x and 0.2 x re-extractions do not.
        /// </summary>
        [TestMethod]
        public void TestReextractedCorrelations()
        {
            const int n = 61;
            const int apex = 30;
            var tolerance = FragmentToleranceConfig.Hram(10.0);
            var rts = Enumerable.Range(0, n).Select(i => 10.0 + 0.01 * i).ToArray();
            var profile = rts.Select(rt => 1000.0 * Math.Exp(-Math.Pow((rt - 10.3) / 0.04, 2)) + 5.0).ToArray();
            var library = new[] { 100f, 90f, 80f, 70f, 60f, 50f };
            var charges = new byte[] { 1, 2, 1, 1, 1, 1 };
            var candidate = new LibraryEntry(1, "PEPTIDE", "PEPTIDE", 2, 500.0, 10.0)
            {
                Fragments = library.Select((v, i) => new LibraryFragment
                {
                    Mz = 300 + 100 * i,
                    RelativeIntensity = v,
                    Annotation = new FragmentAnnotation { IonType = IonType.Y, Ordinal = (byte) (3 + i), Charge = charges[i] },
                }).ToList(),
            };
            const int interfered = 5;
            double interfererMz = candidate.Fragments[interfered].Mz + 0.6 * tolerance.ToleranceDa(candidate.Fragments[interfered].Mz);

            var spectra = new List<Spectrum>();
            var xicIntensities = library.Select(_ => new double[n]).ToArray();
            for (int i = 0; i < n; i++)
            {
                var peaks = new List<KeyValuePair<double, double>>();
                for (int f = 0; f < library.Length; f++)
                {
                    var fragment = candidate.Fragments[f];
                    double intensity = profile[i] * (1.0 - 0.1 * f);
                    xicIntensities[f][i] = intensity;
                    peaks.Add(new KeyValuePair<double, double>(f == interfered ? interfererMz : fragment.Mz, intensity));
                    if (f < 3)
                        peaks.Add(new KeyValuePair<double, double>(fragment.Mz + ReextractedCorrelations.C13_DELTA / charges[f], 0.3 * intensity));
                }
                var sorted = peaks.OrderBy(p => p.Key).ToArray();
                spectra.Add(new Spectrum
                {
                    RetentionTime = rts[i],
                    Mzs = sorted.Select(p => p.Key).ToArray(),
                    Intensities = sorted.Select(p => (float) p.Value).ToArray(),
                });
            }
            var xics = xicIntensities.Select((v, f) => new XicData(f, rts, v)).ToList();
            var bounds = new XICPeakBounds { StartIndex = 25, EndIndex = 35, ApexIndex = apex };
            var peakData = new FakePeakData(xics, bounds, candidate: candidate, apexSpectrum: spectra[apex],
                apexGlobalIndex: apex, apexLocalIndex: apex, windowStartIndex: 0, windowLength: n, windowSpectra: spectra);

            var context = new OspreyScoringContext(new OspreyConfig { FragmentTolerance = tolerance });
            var timeCorr = TimeCorrelations.GetOrCompute(context, peakData);
            Assert.IsTrue(timeCorr.RankCorrelations[interfered] > 0.98,
                string.Format("The full-tolerance XIC holds the interferer, got {0:F4}", timeCorr.RankCorrelations[interfered]));
            var reextracted = ReextractedCorrelations.GetOrCompute(context, peakData);
            Assert.AreEqual(3, reextracted.HeavyGoodCount);
            Assert.IsTrue(reextracted.HeavySum > 2.94 && reextracted.HeavySum <= 3.0 + TOLERANCE,
                string.Format("Three co-eluting isotopes and three absent ones, got {0:F4}", reextracted.HeavySum));
            Assert.IsTrue(reextracted.TightWideSum > 4.9 && reextracted.TightWideSum <= 5.0 + TOLERANCE,
                string.Format("The interferer drops out at 0.45x, got {0:F4}", reextracted.TightWideSum));
            Assert.IsTrue(reextracted.TightNarrowSum > 4.9 && reextracted.TightNarrowSum <= 5.0 + TOLERANCE,
                string.Format("The interferer drops out at 0.2x, got {0:F4}", reextracted.TightNarrowSum));
            Assert.IsTrue(reextracted.BestSum > 5.9, string.Format("The best of each keeps the full XIC, got {0:F4}", reextracted.BestSum));
            Assert.AreSame(reextracted, ReextractedCorrelations.GetOrCompute(context, peakData), "Computed once per candidate");
            Assert.AreEqual(reextracted.HeavySum, OspreyFeatureCalculators.Get(80).Calculate(context, peakData), TOLERANCE);
            Assert.AreEqual(3.0, OspreyFeatureCalculators.Get(81).Calculate(context, peakData), TOLERANCE);
            Assert.AreEqual(reextracted.TightWideSum, OspreyFeatureCalculators.Get(82).Calculate(context, peakData), TOLERANCE);
            Assert.AreEqual(reextracted.TightNarrowSum, OspreyFeatureCalculators.Get(83).Calculate(context, peakData), TOLERANCE);
            Assert.AreEqual(reextracted.BestSum, OspreyFeatureCalculators.Get(84).Calculate(context, peakData), TOLERANCE);
        }

        /// <summary>
        /// Peak-dominance family over two apexes: all six fragments co-elute at scan 30, only the top three
        /// at scan 90, the rest absent there. Each is accepted once, scoring about 5 and 2. Choosing the full
        /// peak gives a positive best-delta and margin; choosing the partial one makes both negative.
        /// </summary>
        [TestMethod]
        public void TestPeakDominance()
        {
            const int n = 121;
            var rts = Enumerable.Range(0, n).Select(i => 10.0 + 0.01 * i).ToArray();
            var library = new[] { 100f, 90f, 80f, 70f, 60f, 50f };
            var candidate = new LibraryEntry(1, "PEPTIDE", "PEPTIDE", 2, 500.0, 10.0)
            {
                Fragments = library.Select((v, i) => new LibraryFragment { Mz = 200 + 100 * i, RelativeIntensity = v }).ToList(),
            };
            var xics = new List<XicData>();
            for (int f = 0; f < library.Length; f++)
            {
                double scale = 1.0 - 0.05 * f;
                var intensities = rts.Select(rt =>
                    1000.0 * scale * Math.Exp(-Math.Pow((rt - 10.3) / 0.04, 2)) +
                    (f < 3 ? 500.0 * scale * Math.Exp(-Math.Pow((rt - 10.9) / 0.04, 2)) : 0.0)).ToArray();
                xics.Add(new XicData(f, rts, intensities));
            }

            var fullPeak = new FakePeakData(xics, new XICPeakBounds { StartIndex = 25, EndIndex = 35, ApexIndex = 30 }, candidate: candidate);
            var full = PeakDominance.Compute(fullPeak, TimeCorrelations.Compute(fullPeak, TimeCorrelations.HALF_WINDOW_MINUTES));
            Assert.AreEqual(2, full.ApexCount);
            Assert.IsTrue(full.BestCorrSum > 4.9 && full.BestCorrSum <= 5.0 + TOLERANCE,
                string.Format("Five partners co-elute at the full apex, got {0:F4}", full.BestCorrSum));
            Assert.IsTrue(full.TotalCorrSum > 6.8 && full.TotalCorrSum <= 7.0 + TOLERANCE,
                string.Format("The partial apex adds about 2, got {0:F4}", full.TotalCorrSum));
            Assert.IsTrue(full.BestCorrDelta > 0.8, string.Format("Six time correlations over five partners, got {0:F4}", full.BestCorrDelta));
            Assert.AreEqual(Math.Log(full.TimeCorrSum / (full.TotalCorrSum + 1.0)), full.TotCorrSum, TOLERANCE);
            Assert.IsTrue(full.CorrMargin > 2.8, string.Format("The full apex dominates, got {0:F4}", full.CorrMargin));

            var partialPeak = new FakePeakData(xics, new XICPeakBounds { StartIndex = 85, EndIndex = 95, ApexIndex = 90 }, candidate: candidate);
            var partial = PeakDominance.Compute(partialPeak, TimeCorrelations.Compute(partialPeak, TimeCorrelations.HALF_WINDOW_MINUTES));
            Assert.AreEqual(2, partial.ApexCount);
            Assert.AreEqual(full.BestCorrSum, partial.BestCorrSum, TOLERANCE);
            Assert.IsTrue(partial.BestCorrDelta < -1.8, string.Format("Three time correlations under the full apex, got {0:F4}", partial.BestCorrDelta));
            Assert.IsTrue(partial.CorrMargin < -2.8, string.Format("The full apex elsewhere dominates, got {0:F4}", partial.CorrMargin));

            var lonePeak = new FakePeakData(xics.Take(1).ToList(), fullPeak.PeakBounds, candidate: candidate);
            var lone = PeakDominance.Compute(lonePeak, TimeCorrelations.Compute(lonePeak, TimeCorrelations.HALF_WINDOW_MINUTES));
            Assert.AreEqual(0, lone.ApexCount);
            Assert.AreEqual(0.0, lone.CorrMargin, TOLERANCE);

            var context = new OspreyScoringContext(new OspreyConfig());
            var dominance = PeakDominance.GetOrCompute(context, fullPeak);
            Assert.AreSame(dominance, PeakDominance.GetOrCompute(context, fullPeak), "Computed once per candidate");
            Assert.AreEqual(dominance.BestCorrDelta, OspreyFeatureCalculators.Get(85).Calculate(context, fullPeak), TOLERANCE);
            Assert.AreEqual(dominance.TotCorrSum, OspreyFeatureCalculators.Get(86).Calculate(context, fullPeak), TOLERANCE);
            Assert.AreEqual(dominance.ApexCount, OspreyFeatureCalculators.Get(87).Calculate(context, fullPeak), TOLERANCE);
            Assert.AreEqual(dominance.CorrMargin, OspreyFeatureCalculators.Get(88).Calculate(context, fullPeak), TOLERANCE);
        }

        /// <summary>
        /// Top-fragment family: the three most intense library fragments co-elute while the other
        /// three are noise. Restricted to the top three, the reference count, median-polish R^2 and
        /// pairwise minimum stay high where the all-fragment minimum R^2 does not; a top-3 fragment
        /// without an XIC counts as not following.
        /// </summary>
        [TestMethod]
        public void TestTopFragments()
        {
            var rts = Enumerable.Range(0, 61).Select(i => 10.0 + 0.01 * i).ToArray();
            var candidate = SixFragmentCandidate();
            var xics = TopThreeCoelutingXics(rts, 10.3);
            var bounds = new XICPeakBounds { StartIndex = 22, EndIndex = 38, ApexIndex = 30 };
            var peakData = new FakePeakData(xics, bounds, candidate: candidate);
            var polish = PolishOverBounds(xics, bounds);

            var context = new OspreyScoringContext(new OspreyConfig());
            var top = TopFragments.Compute(peakData, ReferenceCorrelations.GetOrCompute(context, peakData), polish);
            Assert.AreEqual(2, top.RefGoodCount, "The two top fragments besides the reference follow it");
            Assert.IsTrue(top.CoelutionMin > 0.99, string.Format("The top three co-elute, got {0:F4}", top.CoelutionMin));
            var topR2 = Enumerable.Range(0, 3).Select(row => Math.Max(0.0, TukeyMedianPolish.FragmentR2(polish.Overall,
                polish.RowEffects[row], polish.ColEffects, polish.Residuals[row]))).OrderBy(r2 => r2).ToArray();
            Assert.AreEqual(topR2[0], top.MpMinR2, TOLERANCE);
            Assert.AreEqual(topR2[1], top.MpMedianR2, TOLERANCE);
            Assert.IsTrue(top.MpMinR2 > TukeyMedianPolish.MinFragmentR2(polish),
                string.Format("Top 3 min R2 {0:F4} above the all-fragment min {1:F4}", top.MpMinR2, TukeyMedianPolish.MinFragmentR2(polish)));
            Assert.AreEqual(0.0, TopFragments.Compute(peakData, ReferenceCorrelations.GetOrCompute(context, peakData), null).MpMinR2,
                "No median-polish fit");

            // Library fragment 1 (a top-3 fragment) without an XIC.
            var missing = xics.Where(x => x.FragmentIndex != 1).ToList();
            var missingPeak = new FakePeakData(missing, bounds, candidate: candidate);
            var missingContext = new OspreyScoringContext(new OspreyConfig());
            var gapped = TopFragments.Compute(missingPeak, ReferenceCorrelations.GetOrCompute(missingContext, missingPeak),
                PolishOverBounds(missing, bounds));
            Assert.AreEqual(1, gapped.RefGoodCount);
            Assert.AreEqual(0.0, gapped.CoelutionMin, TOLERANCE);
            Assert.AreEqual(0.0, gapped.MpMinR2, TOLERANCE);

            context.AddInfo(new MedianPolishByproduct(polish, null));
            var cached = TopFragments.GetOrCompute(context, peakData);
            Assert.AreSame(cached, TopFragments.GetOrCompute(context, peakData), "Computed once per candidate");
            Assert.AreEqual(top.RefGoodCount, OspreyFeatureCalculators.Get(89).Calculate(context, peakData), TOLERANCE);
            Assert.AreEqual(top.MpMinR2, OspreyFeatureCalculators.Get(90).Calculate(context, peakData), TOLERANCE);
            Assert.AreEqual(top.MpMedianR2, OspreyFeatureCalculators.Get(91).Calculate(context, peakData), TOLERANCE);
            Assert.AreEqual(top.CoelutionMin, OspreyFeatureCalculators.Get(92).Calculate(context, peakData), TOLERANCE);
        }

        /// <summary>
        /// Wide-span coelution family: narrow bounds of apex +/- 1 scan widen to apex +/- 0.08 min
        /// (8 scans at 0.01 min spacing), wide bounds stay as they are, and over the widened span each
        /// feature equals its bounds counterpart computed with the bounds set to that span. The margin
        /// widens a second candidate peak around its own apex.
        /// </summary>
        [TestMethod]
        public void TestWideSpanCoelution()
        {
            var rts = Enumerable.Range(0, 121).Select(i => 10.0 + 0.01 * i).ToArray();
            var candidate = SixFragmentCandidate();
            var xics = TopThreeCoelutingXics(rts, 10.3);
            var narrow = new XICPeakBounds { StartIndex = 29, EndIndex = 31, ApexIndex = 30 };
            var narrowPeak = new FakePeakData(xics, narrow, candidate: candidate);
            var wide = WideSpanCoelution.Compute(narrowPeak, null, WideSpanCoelution.HALF_WINDOW_MINUTES);
            Assert.AreEqual(22, wide.SpanStart);
            Assert.AreEqual(38, wide.SpanEnd);
            Assert.AreEqual(0.0, wide.Margin, TOLERANCE, "No candidate peaks");

            var broad = new XICPeakBounds { StartIndex = 10, EndIndex = 50, ApexIndex = 30 };
            var broadWide = WideSpanCoelution.Compute(new FakePeakData(xics, broad, candidate: candidate), null,
                WideSpanCoelution.HALF_WINDOW_MINUTES);
            Assert.AreEqual(10, broadWide.SpanStart);
            Assert.AreEqual(50, broadWide.SpanEnd);

            var spanBounds = new XICPeakBounds { StartIndex = 22, EndIndex = 38, ApexIndex = 30 };
            var spanPeak = new FakePeakData(xics, spanBounds, candidate: candidate);
            var spanContext = new OspreyScoringContext(new OspreyConfig());
            Assert.AreEqual(FeatureByName(@"fragment_coelution_sum").Calculate(spanContext, spanPeak), wide.Sum, TOLERANCE);
            Assert.AreEqual(FeatureByName(@"n_coeluting_fragments").Calculate(spanContext, spanPeak), wide.NCoeluting, TOLERANCE);
            Assert.AreEqual(TopFragments.Compute(spanPeak, ReferenceCorrelations.GetOrCompute(spanContext, spanPeak), null).CoelutionMin,
                wide.Top3Min, TOLERANCE);
            Assert.IsTrue(wide.Top3Min > 0.99, string.Format("The top three co-elute over the span, got {0:F4}", wide.Top3Min));

            // A second candidate in noise at scan 90 (narrow bounds too), widened to 82-98 around its apex.
            var other = new XICPeakBounds { StartIndex = 89, EndIndex = 91, ApexIndex = 90 };
            var otherSpanPeak = new FakePeakData(xics, new XICPeakBounds { StartIndex = 82, EndIndex = 98, ApexIndex = 90 }, candidate: candidate);
            const int pairs = 15;
            double otherMean = FeatureByName(@"fragment_coelution_sum").Calculate(new OspreyScoringContext(new OspreyConfig()), otherSpanPeak) / pairs;
            var candidates = new PeakCandidatesByproduct(new[] { (narrow, 0.5, 2.0), (other, 0.1, 1.0) }, narrow);
            var withMargin = WideSpanCoelution.Compute(narrowPeak, candidates, WideSpanCoelution.HALF_WINDOW_MINUTES);
            Assert.AreEqual(wide.Sum / pairs - Math.Max(0.0, otherMean), withMargin.Margin, TOLERANCE);
            var notChosen = new PeakCandidatesByproduct(new[] { (other, 0.1, 1.0) }, narrow);
            Assert.AreEqual(0.0, WideSpanCoelution.Compute(narrowPeak, notChosen, WideSpanCoelution.HALF_WINDOW_MINUTES).Margin,
                TOLERANCE, "The chosen peak is not among the candidates");

            var context = new OspreyScoringContext(new OspreyConfig());
            context.AddInfo(candidates);
            var cached = WideSpanCoelution.GetOrCompute(context, narrowPeak);
            Assert.AreSame(cached, WideSpanCoelution.GetOrCompute(context, narrowPeak), "Computed once per candidate");
            Assert.AreEqual(withMargin.Sum, OspreyFeatureCalculators.Get(93).Calculate(context, narrowPeak), TOLERANCE);
            Assert.AreEqual(withMargin.NCoeluting, OspreyFeatureCalculators.Get(94).Calculate(context, narrowPeak), TOLERANCE);
            Assert.AreEqual(withMargin.Margin, OspreyFeatureCalculators.Get(95).Calculate(context, narrowPeak), TOLERANCE);
            Assert.AreEqual(withMargin.Top3Min, OspreyFeatureCalculators.Get(96).Calculate(context, narrowPeak), TOLERANCE);
        }

        /// <summary>
        /// The parquet feature columns and the calculators are two hand-kept lists in lockstep:
        /// each new feature adds to both, so a calculator whose name differs from its column
        /// would write one feature under another's name. The default FDR mask resolves names
        /// against the columns, so each of its names must be a C#-only column, never one of the
        /// 21 Rust features.
        /// </summary>
        [TestMethod]
        public void TestFeatureNamesAndDefaultMask()
        {
            var names = ParquetScoreCache.PIN_FEATURE_NAMES;
            Assert.AreEqual(OspreyFeatureCalculators.FeatureCount, names.Length);
            for (int i = 0; i < names.Length; i++)
                Assert.AreEqual(names[i], OspreyFeatureCalculators.Get(i).Name, string.Format("Feature {0}", i));

            foreach (string name in PinFeatureMask.DEFAULT_MASKED_FEATURES)
                Assert.IsTrue(Array.IndexOf(names, name) >= 21, name);
        }

        private static LibraryFragment Frag(double mz, IonType ionType, byte ordinal)
        {
            return new LibraryFragment
            {
                Mz = mz,
                Annotation = new FragmentAnnotation { IonType = ionType, Ordinal = ordinal, Charge = 1 },
            };
        }

        // Six library fragments, intensities 100 down to 50, so fragments 0-2 are the top three.
        private static LibraryEntry SixFragmentCandidate()
        {
            var library = new[] { 100f, 90f, 80f, 70f, 60f, 50f };
            return new LibraryEntry(1, "PEPTIDE", "PEPTIDE", 2, 500.0, 10.0)
            {
                Fragments = library.Select((v, i) => new LibraryFragment { Mz = 200 + 100 * i, RelativeIntensity = v }).ToList(),
            };
        }

        // Fragments 0-2 share one Gaussian elution at apexRt over a small baseline; fragments 3-5 are positive
        // noise with no peak.
        private static List<XicData> TopThreeCoelutingXics(double[] rts, double apexRt)
        {
            var xics = new List<XicData>();
            for (int f = 0; f < 6; f++)
            {
                int fragment = f;
                double scale = 1.0 - 0.1 * f;
                var intensities = rts.Select((rt, i) => fragment < 3
                    ? scale * (1000.0 * Math.Exp(-Math.Pow((rt - apexRt) / 0.04, 2)) + 1.0)
                    : 50.0 + 40.0 * Math.Sin(1.3 * i + 2.1 * fragment)).ToArray();
                xics.Add(new XicData(f, rts, intensities));
            }
            return xics;
        }

        // The median-polish fit the harness publishes: every XIC cropped to the peak bounds.
        private static TukeyMedianPolishResult PolishOverBounds(IReadOnlyList<XicData> xics, XICPeakBounds bounds)
        {
            int len = bounds.EndIndex - bounds.StartIndex + 1;
            var peakXics = xics.Select(x => new KeyValuePair<int, double[]>(x.FragmentIndex,
                x.Intensities.Skip(bounds.StartIndex).Take(len).ToArray())).ToList();
            var peakRts = xics[0].RetentionTimes.Skip(bounds.StartIndex).Take(len).ToArray();
            return TukeyMedianPolish.Compute(peakXics, peakRts, TukeyMedianPolish.SCORING_MAX_ITERATIONS, TukeyMedianPolish.SCORING_TOLERANCE);
        }

        private static IOspreyFeatureCalculator FeatureByName(string name)
        {
            return OspreyFeatureCalculators.Get(Array.IndexOf(ParquetScoreCache.PIN_FEATURE_NAMES, name));
        }

        private sealed class FakePeakData : IOspreyApexSpectraPeakData
        {
            private readonly IReadOnlyList<XicData> _xics;
            private readonly XICPeakBounds _peakBounds;
            private readonly double _apexRetentionTime;
            private readonly double _expectedRt;
            private readonly LibraryEntry _candidate;
            private readonly Spectrum _apexSpectrum;
            private readonly int _apexGlobalIndex;
            private readonly int _apexLocalIndex;
            private readonly int _windowStartIndex;
            private readonly int _windowLength;
            private readonly IReadOnlyList<Spectrum> _windowSpectra;

            public FakePeakData(IReadOnlyList<XicData> xics, XICPeakBounds peakBounds,
                double apexRetentionTime = 0.0, double expectedRt = 0.0,
                LibraryEntry candidate = null, Spectrum apexSpectrum = null,
                int apexGlobalIndex = 0, int apexLocalIndex = 0, int windowStartIndex = 0,
                int windowLength = 0, IReadOnlyList<Spectrum> windowSpectra = null)
            {
                _xics = xics;
                _peakBounds = peakBounds;
                _apexRetentionTime = apexRetentionTime;
                _expectedRt = expectedRt;
                _candidate = candidate;
                _apexSpectrum = apexSpectrum;
                _apexGlobalIndex = apexGlobalIndex;
                _apexLocalIndex = apexLocalIndex;
                _windowStartIndex = windowStartIndex;
                _windowLength = windowLength;
                _windowSpectra = windowSpectra;
            }

            public LibraryEntry Candidate { get { return _candidate; } }
            public XICPeakBounds PeakBounds { get { return _peakBounds; } }
            public double ApexRetentionTime { get { return _apexRetentionTime; } }
            public double ExpectedRt { get { return _expectedRt; } }
            public IReadOnlyList<XicData> Xics { get { return _xics; } }
            public Spectrum ApexSpectrum { get { return _apexSpectrum; } }
            public int ApexGlobalIndex { get { return _apexGlobalIndex; } }

            public bool TryGetApexOffsetSpectrum(int offset, out Spectrum spectrum, out int cacheIndex)
            {
                int candIdx = _apexLocalIndex + offset;
                if (candIdx < 0 || candIdx >= _windowLength)
                {
                    spectrum = null;
                    cacheIndex = -1;
                    return false;
                }
                cacheIndex = _windowStartIndex + candIdx;
                spectrum = _windowSpectra[cacheIndex];
                return true;
            }

            // MS1 data is produced upstream by the extractor; the fake supplies none,
            // so the MS1 features evaluate to 0.0 (the HRAM-off path under test).
            public XicData Ms1PrecursorXic { get { return null; } }
            public XicData Ms1ReferenceXic { get { return null; } }
            public double[] ApexIsotopeEnvelope { get { return null; } }
        }

        /// <summary>
        /// Minimal <see cref="IResolutionStrategy"/> fake whose ScoreXcorr returns
        /// the spectrum index it was asked to score. That makes the Savitzky-Golay
        /// weighted sum and the apex index analytic: a linear "score = index"
        /// function lets the test pin the weights, the apex+/-2 window, the
        /// candidate-local -> global mapping, and the asymmetric edge skip.
        /// </summary>
        private sealed class IndexEchoResolution : IResolutionStrategy
        {
            public bool HasMs1Features { get { return false; } }
            public SpectralScorer CreateScorer() { return null; }
            public WindowXcorrCache PreprocessWindowSpectra(IList<Spectrum> spectra,
                SpectralScorer scorer, XcorrScratchPool scratchPool) { return null; }
            public void ReleaseWindowCache(WindowXcorrCache cache, XcorrScratchPool scratchPool) { }
            public double ScoreXcorr(WindowXcorrCache preprocessed, int spectrumIndex,
                Spectrum spectrum, LibraryEntry entry, SpectralScorer scorer,
                XcorrScratchPool scratchPool)
            {
                return spectrumIndex;
            }
        }
    }
}
