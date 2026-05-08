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
    }
}
