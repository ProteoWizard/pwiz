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

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class IntensityAccumulatorTest : AbstractUnitTest
    {
        private const double TARGET_MZ = 500.0;
        private const double EPSILON = 1e-9;

        [TestMethod]
        public void TestIntensityAccumulator()
        {
            TestSummedIntensityWeightedMean();
            TestSummedSingleIonMobility();
            TestBasePeakKeepsMostIntense();
            TestNotTracking();
            TestNullIonMobilityIgnored();
            TestPrecursorObservedIonMobilityAggregate();
        }

        // The per-ion value combines the per-transition observed IMs: MS1 isotope channels
        // weighted by predicted abundance, falling back to the offset-corrected fragment
        // channels (area-weighted) for MS2-only data.
        private static void TestPrecursorObservedIonMobilityAggregate()
        {
            // MS1 isotopes, abundance-weighted: 0.7*1.00 + 0.3*1.20 = 1.06
            var ms1 = new[]
            {
                new PrecursorResult.ObservedIonMobilityChannel(true, 1.00, null, 0.7, 0),
                new PrecursorResult.ObservedIonMobilityChannel(true, 1.20, null, 0.3, 0),
            };
            Assert.AreEqual(0.7 * 1.00 + 0.3 * 1.20, PrecursorResult.AggregateObservedIonMobility(ms1).Value, EPSILON);

            // MS2-only fallback: each fragment corrected by removing its high-energy offset
            // (-0.05 here, so 2.05 -> 2.10), then area-weighted: (100*2.10 + 50*2.20)/150.
            var ms2 = new[]
            {
                new PrecursorResult.ObservedIonMobilityChannel(false, 2.05, null, 100, -0.05),
                new PrecursorResult.ObservedIonMobilityChannel(false, 2.15, null, 50, -0.05),
            };
            Assert.AreEqual((100 * 2.10 + 50 * 2.20) / 150, PrecursorResult.AggregateObservedIonMobility(ms2).Value, EPSILON);

            // MS1 is preferred over MS2 when both are present (fragments ignored).
            var both = new[]
            {
                new PrecursorResult.ObservedIonMobilityChannel(true, 1.00, null, 1.0, 0),
                new PrecursorResult.ObservedIonMobilityChannel(false, 9.99, null, 100, 0),
            };
            Assert.AreEqual(1.00, PrecursorResult.AggregateObservedIonMobility(both).Value, EPSILON);

            // MS1 channels present but without an observed value fall through to the fragments.
            var ms1NoValue = new[]
            {
                new PrecursorResult.ObservedIonMobilityChannel(true, null, null, 0.7, 0),
                new PrecursorResult.ObservedIonMobilityChannel(false, 2.00, null, 100, 0),
            };
            Assert.AreEqual(2.00, PrecursorResult.AggregateObservedIonMobility(ms1NoValue).Value, EPSILON);

            // No usable channels -> null.
            Assert.IsNull(PrecursorResult.AggregateObservedIonMobility(Array.Empty<PrecursorResult.ObservedIonMobilityChannel>()));
        }

        // Summed extractor: ObservedIonMobility is the intensity-weighted mean of the
        // IM values across the extraction band - the "intensity center of gravity" of
        // the mobilogram, accumulated the same way as mass error. Averaging the IM
        // values directly is valid because the tracked units (1/K0, drift time) map
        // linearly to CCS.
        private static void TestSummedIntensityWeightedMean()
        {
            var acc = new IntensityAccumulator(false, ChromExtractor.summed, TARGET_MZ, true);
            acc.AddPoint(TARGET_MZ, 10, 1.10);
            acc.AddPoint(TARGET_MZ, 20, 0.90);
            acc.AddPoint(TARGET_MZ, 5, 1.05);
            // Intensity-weighted mean = (10*1.10 + 20*0.90 + 5*1.05) / 35 = 34.25/35
            Assert.AreEqual(35.0, acc.TotalIntensity, EPSILON);
            Assert.AreEqual(34.25 / 35.0, acc.ObservedIonMobility, EPSILON);
        }

        // All observations at a single IM collapse to that IM regardless of intensity.
        // Covers binned drift-time data (one IM per spectrum) and single-observation cases.
        private static void TestSummedSingleIonMobility()
        {
            var acc = new IntensityAccumulator(false, ChromExtractor.summed, TARGET_MZ, true);
            acc.AddPoint(TARGET_MZ, 10, 0.7);
            acc.AddPoint(TARGET_MZ, 100, 0.7);
            Assert.AreEqual(0.7, acc.ObservedIonMobility, EPSILON);
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
        }

        // Per-point null IM (e.g. spectra without IM arrays) must not contribute to
        // the observed-IM mean.
        private static void TestNullIonMobilityIgnored()
        {
            var acc = new IntensityAccumulator(false, ChromExtractor.summed, TARGET_MZ, true);
            acc.AddPoint(TARGET_MZ, 10, 1.4);  // contributes
            acc.AddPoint(TARGET_MZ, 30);       // no IM, must not perturb the mean
            Assert.AreEqual(1.4, acc.ObservedIonMobility, EPSILON);
        }
    }
}
