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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp.Test
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
            var peakData = new FakeDetailedPeakData(xics, bounds);

            var context = new OspreyScoringContext(null);
            context.ClearByproducts();

            double apex = OspreyFeatureCalculators.Get(3).Calculate(context, peakData);
            double area = OspreyFeatureCalculators.Get(4).Calculate(context, peakData);
            double sharpness = OspreyFeatureCalculators.Get(5).Calculate(context, peakData);

            // peak_apex: reference XIC (frag1) intensity at apex index 3 = 90.
            Assert.AreEqual(90.0, apex, TOLERANCE);
            // peak_area: trapezoid over [1,5) with dt = 1:
            // (10+50)/2 + (50+90)/2 + (90+50)/2 + (50+10)/2 = 30+70+70+30 = 200.
            Assert.AreEqual(200.0, area, TOLERANCE);
            // peak_sharpness: left (90-10)/(3-1)=40, right (90-10)/(5-3)=40, mean 40.
            Assert.AreEqual(40.0, sharpness, TOLERANCE);

            // The three calculators expose the parity-critical PIN names.
            Assert.AreEqual("peak_apex", OspreyFeatureCalculators.Get(3).Name);
            Assert.AreEqual("peak_area", OspreyFeatureCalculators.Get(4).Name);
            Assert.AreEqual("peak_sharpness", OspreyFeatureCalculators.Get(5).Name);

            // Degenerate peak data (no XICs) yields 0.0 for every peak-shape feature.
            var empty = new FakeDetailedPeakData(new List<XicData>(), bounds);
            context.ClearByproducts();
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(3).Calculate(context, empty), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(4).Calculate(context, empty), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(5).Calculate(context, empty), TOLERANCE);
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
            var peakData = new FakeDetailedPeakData(xics, bounds);

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
            var single = new FakeDetailedPeakData(
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
            var peakData = new FakeDetailedPeakData(
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

            var late = new FakeDetailedPeakData(new List<XicData>(), new XICPeakBounds(),
                apexRetentionTime: 12.5, expectedRt: 10.0);
            Assert.AreEqual(2.5, OspreyFeatureCalculators.Get(11).Calculate(context, late), TOLERANCE);
            Assert.AreEqual(2.5, OspreyFeatureCalculators.Get(12).Calculate(context, late), TOLERANCE);

            // Earlier-than-expected apex -> negative deviation, positive absolute.
            var early = new FakeDetailedPeakData(new List<XicData>(), new XICPeakBounds(),
                apexRetentionTime: 8.0, expectedRt: 10.0);
            Assert.AreEqual(-2.0, OspreyFeatureCalculators.Get(11).Calculate(context, early), TOLERANCE);
            Assert.AreEqual(2.0, OspreyFeatureCalculators.Get(12).Calculate(context, early), TOLERANCE);

            Assert.AreEqual("rt_deviation", OspreyFeatureCalculators.Get(11).Name);
            Assert.AreEqual("abs_rt_deviation", OspreyFeatureCalculators.Get(12).Name);
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
            var peakData = new FakeDetailedPeakData(new List<XicData>(), new XICPeakBounds(),
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
            var noMatchData = new FakeDetailedPeakData(new List<XicData>(), new XICPeakBounds(),
                candidate: noMatch, apexSpectrum: apex);
            var noMatchContext = new OspreyScoringContext(config);
            noMatchContext.ClearByproducts();
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(7).Calculate(noMatchContext, noMatchData), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(8).Calculate(noMatchContext, noMatchData), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(9).Calculate(noMatchContext, noMatchData), TOLERANCE);
            Assert.AreEqual(0.5, OspreyFeatureCalculators.Get(10).Calculate(noMatchContext, noMatchData), TOLERANCE);
        }

        private static LibraryFragment Frag(double mz, IonType ionType, byte ordinal)
        {
            return new LibraryFragment
            {
                Mz = mz,
                Annotation = new FragmentAnnotation { IonType = ionType, Ordinal = ordinal },
            };
        }

        private sealed class FakeDetailedPeakData : IOspreyDetailedPeakData
        {
            private readonly IReadOnlyList<XicData> _xics;
            private readonly XICPeakBounds _peakBounds;
            private readonly double _apexRetentionTime;
            private readonly double _expectedRt;
            private readonly LibraryEntry _candidate;
            private readonly Spectrum _apexSpectrum;

            public FakeDetailedPeakData(IReadOnlyList<XicData> xics, XICPeakBounds peakBounds,
                double apexRetentionTime = 0.0, double expectedRt = 0.0,
                LibraryEntry candidate = null, Spectrum apexSpectrum = null)
            {
                _xics = xics;
                _peakBounds = peakBounds;
                _apexRetentionTime = apexRetentionTime;
                _expectedRt = expectedRt;
                _candidate = candidate;
                _apexSpectrum = apexSpectrum;
            }

            public LibraryEntry Candidate { get { return _candidate; } }
            public XICPeakBounds PeakBounds { get { return _peakBounds; } }
            public double ApexRetentionTime { get { return _apexRetentionTime; } }
            public double ExpectedRt { get { return _expectedRt; } }
            public IReadOnlyList<XicData> Xics { get { return _xics; } }
            public Spectrum ApexSpectrum { get { return _apexSpectrum; } }
        }
    }
}
