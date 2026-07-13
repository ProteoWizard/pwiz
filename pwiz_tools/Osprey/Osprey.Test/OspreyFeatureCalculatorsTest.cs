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
        }

        private static LibraryFragment Frag(double mz, IonType ionType, byte ordinal)
        {
            return new LibraryFragment
            {
                Mz = mz,
                Annotation = new FragmentAnnotation { IonType = ionType, Ordinal = ordinal, Charge = 1 },
            };
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
