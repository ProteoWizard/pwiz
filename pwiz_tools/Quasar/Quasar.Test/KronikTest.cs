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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Quasar.Features;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// The Kronik port: envelopes linked across scans into persistent features (ported from skyline-glimpse's
    /// KronikTests, every assertion and expected value kept).
    /// </summary>
    [TestClass]
    public class KronikTest
    {
        private static readonly CultureInfo INV = CultureInfo.InvariantCulture;

        /// <summary>Linking rules on small hand-made series: tolerance, charge, gaps, seeds, SIM windows.</summary>
        [TestMethod]
        public void TestKronikLinking()
        {
            AnEnvelopeInThreeScansPersists();
            OneMissedScanIsBridgedTwoEndTheFeature();
            ChargeAndToleranceMustAgree();
            TheMostIntenseMatchJoinsAndARejectedSeedLeavesItsNeighbors();
            AScanWindowThatMissesTheFeatureIsSkippedNotAGap();
        }

        /// <summary>
        /// The same features as BullseyeSharp finds in a synthetic Hardklor file (TestData/make-kronik-synthetic.py
        /// made it; Skyline's BullseyeSharp.exe wrote the .bs.kro).
        /// </summary>
        [TestMethod]
        public void TestKronikMatchesBullseyeSharp()
        {
            var scanNumbers = new List<int>();
            var scans = new List<List<EnvelopeKey>>();
            foreach (var line in File.ReadLines(SmallRaw.TestDataPath("kronik-synthetic.hk")))
            {
                var t = line.Split('\t');
                if (t[0] == "S")
                {
                    scanNumbers.Add(int.Parse(t[1], INV));
                    scans.Add(new List<EnvelopeKey>());
                }
                else
                {
                    scans[^1].Add(new EnvelopeKey(double.Parse(t[1], INV), int.Parse(t[2], INV), (float)double.Parse(t[3], INV)));
                }
            }
            var result = Kronik.Link(scans, KronikSettings.Default);

            // First scan, last scan, scans spanned (Kronik counts missed scans in between), charge, mass; then the
            // best intensity, which the .NET Framework BullseyeSharp wrote rounded to 7 digits its own way.
            var expected = File.ReadLines(SmallRaw.TestDataPath("kronik-synthetic.hk.bs.kro")).Skip(1)
                .Select(l => l.Split('\t'))
                .Select(t => (Key: (int.Parse(t[1], INV), int.Parse(t[2], INV), int.Parse(t[3], INV), int.Parse(t[4], INV),
                    Math.Round(double.Parse(t[5], INV), 4)), Intensity: double.Parse(t[7], INV)))
                .OrderBy(x => x.Key).ToList();
            var actual = result.Features
                .Select(f => (Key: (scanNumbers[f.FirstScan], scanNumbers[f.LastScan], f.LastScan - f.FirstScan + 1, f.Charge,
                    Math.Round(f.MonoMass, 4)), Intensity: (double)f.BestIntensity))
                .OrderBy(x => x.Key).ToList();
            Assert.AreEqual(98, expected.Count);
            FeatureAssert.AreSequenceEqual(expected.Select(x => x.Key), actual.Select(x => x.Key));
            foreach (var (first, second) in expected.Zip(actual))
                FeatureAssert.AreEqualRounded(1.0, second.Intensity / first.Intensity, 6);
        }

        private static EnvelopeKey E(double mass, int charge, float intensity) => new EnvelopeKey(mass, charge, intensity);

        private static IReadOnlyList<EnvelopeKey>[] Scans(params EnvelopeKey[][] scans) => scans;

        private static void AnEnvelopeInThreeScansPersists()
        {
            var result = Kronik.Link(Scans(
                new[] { E(1000, 2, 10) },
                new[] { E(1000.004, 2, 30), E(1500, 3, 5) },     // 4 ppm away: the same feature
                new[] { E(1000, 2, 20) },
                new[] { E(2000, 2, 99) }), KronikSettings.Default);

            var f = FeatureAssert.Single(result.Features);
            Assert.AreEqual(new PersistentFeature(1000.004, 2, 1, 30, 0, 2, 3, 60), f);
            FeatureAssert.AreSequenceEqual(new[] { 0 }, result.FeatureOf[0]);
            FeatureAssert.AreSequenceEqual(new[] { 0, -1 }, result.FeatureOf[1]);
            FeatureAssert.AreSequenceEqual(new[] { 0 }, result.FeatureOf[2]);
            FeatureAssert.AreSequenceEqual(new[] { -1 }, result.FeatureOf[3]);   // one scan only
        }

        private static void OneMissedScanIsBridgedTwoEndTheFeature()
        {
            // Found in scans 0, 2 and 3 (one gap): persists. Scans 6 and 7 are two misses past scan 3: not linked.
            var x = E(800, 1, 50);
            var result = Kronik.Link(Scans(new[] { x }, Array.Empty<EnvelopeKey>(), new[] { x }, new[] { x },
                Array.Empty<EnvelopeKey>(), Array.Empty<EnvelopeKey>(), new[] { x }, new[] { x }), KronikSettings.Default);
            var f = FeatureAssert.Single(result.Features);
            Assert.AreEqual((0, 3, 3), (f.FirstScan, f.LastScan, f.ScanCount));
            FeatureAssert.AreSequenceEqual(new[] { -1 }, result.FeatureOf[6]);
            FeatureAssert.AreSequenceEqual(new[] { -1 }, result.FeatureOf[7]);   // only two scans together
            // With no gap allowed, scans 0 and 2 are apart: scans 2-3 alone are too few.
            FeatureAssert.IsEmpty(Kronik.Link(Scans(new[] { x }, Array.Empty<EnvelopeKey>(), new[] { x }, new[] { x }),
                KronikSettings.Default with { GapTolerance = 0 }).Features);
        }

        private static void ChargeAndToleranceMustAgree()
        {
            // Same mass at another charge, and the same charge 11 ppm away: neither links.
            var result = Kronik.Link(Scans(
                new[] { E(1000, 2, 10) },
                new[] { E(1000, 3, 30), E(1000.011, 2, 20) },
                new[] { E(1000, 2, 20) }), KronikSettings.Default);
            FeatureAssert.IsEmpty(result.Features);
            // A tighter minimum: two scans are enough.
            FeatureAssert.Single(Kronik.Link(Scans(new[] { E(1000, 2, 10) }, new[] { E(1000, 2, 20) }),
                KronikSettings.Default with { MinScans = 2 }).Features);
        }

        private static void TheMostIntenseMatchJoinsAndARejectedSeedLeavesItsNeighbors()
        {
            // Scan 1 has two candidates within tolerance: the more intense joins the feature.
            var result = Kronik.Link(Scans(
                new[] { E(1000, 2, 100) },
                new[] { E(1000.002, 2, 5), E(999.998, 2, 7) },
                new[] { E(1000, 2, 50) }), KronikSettings.Default);
            FeatureAssert.AreSequenceEqual(new[] { -1, 0 }, result.FeatureOf[1]);

            // The seed at scan 4 (alone with scan 3) is rejected; scan 3's envelope still joins the later feature
            // seeded at scan 1 (scans 1-3).
            var y = E(1200, 2, 10);
            var second = Kronik.Link(Scans(Array.Empty<EnvelopeKey>(), new[] { E(1200, 2, 20) }, new[] { y }, new[] { y },
                new[] { E(1200, 2, 90) }), KronikSettings.Default with { GapTolerance = 0 });
            // Seed 90 (scan 4) looks left: scans 3, 2, 1 all match -> it persists with all of them.
            var f = FeatureAssert.Single(second.Features);
            Assert.AreEqual((1, 4, 4, 4), (f.FirstScan, f.LastScan, f.ScanCount, f.BestScan));
        }

        private static void AScanWindowThatMissesTheFeatureIsSkippedNotAGap()
        {
            var x = E(1000, 2, 10);   // m/z 501.007
            var scans = Scans(new[] { x }, Array.Empty<EnvelopeKey>(), Array.Empty<EnvelopeKey>(), new[] { x }, new[] { x });
            // Scans 1 and 2 are SIM windows elsewhere: skipped, so scans 0, 3 and 4 link.
            var windows = new (double, double)[] { (0, 0), (700, 800), (300, 400), (0, 0), (400, 600) };
            FeatureAssert.Single(Kronik.Link(scans, KronikSettings.Default, windows).Features);
            FeatureAssert.IsEmpty(Kronik.Link(scans, KronikSettings.Default).Features);   // two plain misses in a row
        }
    }
}
