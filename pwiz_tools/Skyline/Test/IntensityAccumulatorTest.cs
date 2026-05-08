/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class IntensityAccumulatorTest : AbstractUnitTest
    {
        private const double TARGET_MZ = 500.0;
        private const double EPSILON = 1e-10;

        [TestMethod]
        public void TestIntensityAccumulator()
        {
            TestSummedCogBinIndex();
            TestCogIonMobilitySingleBin();
            TestCogIonMobilityEmpty();
            TestBasePeakKeepsMostIntense();
            TestNotTracking();
            TestNullIonMobilityIgnored();
            TestIdotpGuardKeepsMatchingBinsAndDropsInterferents();
            TestIdotpGuardEmptyInputs();
            TestIdotpGuardMissingExpectedSignalRejection();
            TestIdotpGuardUnionsImKeysAcrossChannels();
        }

        // Summed extractor: ObservedIonMobility comes from COG of the IM histogram
        // in BIN-INDEX space (sort by IM, then intensity-weighted COG of position),
        // returning the IM at the COG bin. This avoids averaging IM values, so it
        // works for non-linear coordinates like 1/K0.
        private static void TestSummedCogBinIndex()
        {
            var acc = new IntensityAccumulator(false, ChromExtractor.summed, TARGET_MZ, true);
            // Bins after AddPoint calls, sorted by IM:
            //   IM=0.9  -> intensity 20  (index 0)
            //   IM=1.05 -> intensity 5   (index 1)
            //   IM=1.10 -> intensity 10  (index 2)
            // COG of bin index = (0*20 + 1*5 + 2*10) / 35 = 25/35 ~= 0.714
            // round(0.714) = 1, IM at index 1 = 1.05
            acc.AddPoint(TARGET_MZ, 10, 1.10);
            acc.AddPoint(TARGET_MZ, 20, 0.90);
            acc.AddPoint(TARGET_MZ, 5, 1.05);

            Assert.AreEqual(35.0, acc.TotalIntensity, EPSILON);
            Assert.AreEqual(3, acc.IonMobilityIntensityBins.Count);
            Assert.AreEqual(1.05, acc.ObservedIonMobility, EPSILON);
        }

        // Single-bin histograms collapse trivially to that bin's IM regardless of
        // intensity. This covers both the binned-drift-time case (one IM per
        // spectrum) and pathological cases with one observation.
        private static void TestCogIonMobilitySingleBin()
        {
            var acc = new IntensityAccumulator(false, ChromExtractor.summed, TARGET_MZ, true);
            acc.AddPoint(TARGET_MZ, 10, 0.7);
            acc.AddPoint(TARGET_MZ, 100, 0.7);
            Assert.AreEqual(0.7, acc.ObservedIonMobility, EPSILON);
        }

        private static void TestCogIonMobilityEmpty()
        {
            Assert.AreEqual(0.0, IntensityAccumulator.CogIonMobility(null), EPSILON);
            Assert.AreEqual(0.0, IntensityAccumulator.CogIonMobility(new Dictionary<double, double>()), EPSILON);
        }

        // Base-peak extractor: IM tracks the single highest-intensity peak, with
        // resets when a stronger peak arrives.
        private static void TestBasePeakKeepsMostIntense()
        {
            var acc = new IntensityAccumulator(false, ChromExtractor.base_peak, TARGET_MZ, true);
            acc.AddPoint(TARGET_MZ, 10, 1.1);  // first hit, becomes the running max
            acc.AddPoint(TARGET_MZ, 5, 0.5);   // weaker, ignored
            Assert.AreEqual(10.0, acc.TotalIntensity, EPSILON);
            Assert.AreEqual(1.1, acc.ObservedIonMobility, EPSILON);

            acc.AddPoint(TARGET_MZ, 50, 0.8);  // stronger, resets
            Assert.AreEqual(50.0, acc.TotalIntensity, EPSILON);
            Assert.AreEqual(0.8, acc.ObservedIonMobility, EPSILON);
        }

        // Constructor with trackIonMobility=false: IM is never tracked.
        private static void TestNotTracking()
        {
            var acc = new IntensityAccumulator(false, ChromExtractor.summed, TARGET_MZ);
            acc.AddPoint(TARGET_MZ, 10, 1.5);
            acc.AddPoint(TARGET_MZ, 20, 0.5);
            Assert.AreEqual(0.0, acc.ObservedIonMobility, EPSILON);
            Assert.IsNull(acc.IonMobilityIntensityBins);
        }

        // Per-point null IM (e.g. spectra without IM arrays) must not contribute.
        private static void TestNullIonMobilityIgnored()
        {
            var acc = new IntensityAccumulator(false, ChromExtractor.summed, TARGET_MZ, true);
            acc.AddPoint(TARGET_MZ, 10, 1.4);  // contributes
            acc.AddPoint(TARGET_MZ, 30, null); // no IM, must not perturb the histogram
            Assert.AreEqual(1, acc.IonMobilityIntensityBins.Count);
            Assert.AreEqual(1.4, acc.ObservedIonMobility, EPSILON);
        }

        // Idotp guard: the IM bin where the cross-channel intensity vector matches
        // the expected envelope survives; the bin with a wildly mismatched vector
        // (e.g. an interferent dominating M+1) is rejected, even when its raw
        // intensity is high. Surviving bins reduce via COG-bin-index over total
        // intensity, so the answer is the IM at the matched bin.
        private static void TestIdotpGuardKeepsMatchingBinsAndDropsInterferents()
        {
            // Expected envelope: M0 ~= 0.55, M+1 ~= 0.30, M+2 ~= 0.15 (typical for a small peptide).
            var expected = new[] { 0.55f, 0.30f, 0.15f };
            // Channel histograms (IM -> summed intensity):
            //   IM=1.10: M0=110, M+1=60, M+2=30  (vector ~ envelope -> high idotp, kept)
            //   IM=1.20: M0=20,  M+1=200, M+2=10 (M+1 dominated by interferent -> low idotp, rejected)
            var perChannel = new Dictionary<double, double>[3];
            perChannel[0] = new Dictionary<double, double> { { 1.10, 110 }, { 1.20, 20 } };
            perChannel[1] = new Dictionary<double, double> { { 1.10, 60  }, { 1.20, 200 } };
            perChannel[2] = new Dictionary<double, double> { { 1.10, 30  }, { 1.20, 10 } };

            var resolved = IntensityAccumulator.ResolveObservedIonMobilityWithIdotpGuard(perChannel, expected);
            Assert.IsTrue(resolved.HasValue);
            Assert.AreEqual(1.10, resolved.Value, EPSILON);
        }

        private static void TestIdotpGuardEmptyInputs()
        {
            var expected = new[] { 0.5f, 0.5f };
            // Null channel array
            Assert.IsNull(IntensityAccumulator.ResolveObservedIonMobilityWithIdotpGuard(null, expected));
            // Null expected proportions
            Assert.IsNull(IntensityAccumulator.ResolveObservedIonMobilityWithIdotpGuard(
                new[] { new Dictionary<double, double> { { 1.0, 10 } } }, null));
            // Empty per-channel dicts
            var emptyChannels = new[] { new Dictionary<double, double>(), new Dictionary<double, double>() };
            Assert.IsNull(IntensityAccumulator.ResolveObservedIonMobilityWithIdotpGuard(emptyChannels, expected));
        }

        // A bin where M0 is expected (>=10% of the envelope) but observed signal
        // is zero must be rejected even if the dot product of remaining channels
        // happens to look fine - mirrors IonMobilityFinder's missing-where-expected
        // guard.
        private static void TestIdotpGuardMissingExpectedSignalRejection()
        {
            var expected = new[] { 0.55f, 0.30f, 0.15f };
            // Single bin at IM=1.10 with M0 missing (zero) but M+1, M+2 present.
            var perChannel = new Dictionary<double, double>[3];
            perChannel[0] = new Dictionary<double, double>();             // M0 missing
            perChannel[1] = new Dictionary<double, double> { { 1.10, 30 } };
            perChannel[2] = new Dictionary<double, double> { { 1.10, 15 } };

            var resolved = IntensityAccumulator.ResolveObservedIonMobilityWithIdotpGuard(perChannel, expected);
            Assert.IsNull(resolved);
        }

        // Channels can disagree on which IMs they observed. The resolver takes
        // the union of IM keys across channels, treating absent entries as zero.
        // This covers the realistic case where M+2 is sparse compared to M0.
        private static void TestIdotpGuardUnionsImKeysAcrossChannels()
        {
            var expected = new[] { 0.55f, 0.30f, 0.15f };
            // M0 sees IMs at 1.10 and 1.20; M+1 only sees 1.10; M+2 only sees 1.20.
            // The expected:[0.55, 0.30, 0.15] matches at neither bin (each has a
            // zero where >=10% is expected), so both are rejected by the missing-
            // signal guard. This both verifies union behavior and that the guard
            // doesn't create an observed-IM out of fragmentary signal.
            var perChannel = new Dictionary<double, double>[3];
            perChannel[0] = new Dictionary<double, double> { { 1.10, 110 }, { 1.20, 110 } };
            perChannel[1] = new Dictionary<double, double> { { 1.10, 60 } };
            perChannel[2] = new Dictionary<double, double> { { 1.20, 30 } };

            var resolved = IntensityAccumulator.ResolveObservedIonMobilityWithIdotpGuard(perChannel, expected);
            Assert.IsNull(resolved);
        }
    }
}
