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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Quasar.Core.Traces;
using pwiz.Quasar.Features;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// Stage 1 trace building: anchors from linked envelopes in ion counts, centroid traces linked
    /// within the ppm tolerance across at most one missed scan, claimed isotope peaks left out of
    /// the centroid traces, and short traces dropped.
    /// </summary>
    [TestClass]
    public class TraceBuilderTest
    {
        private const double IONS_PER_INTENSITY = 0.004;

        [TestMethod]
        public void TestTraceBuilding()
        {
            const int scanCount = 10;
            var peaks = Enumerable.Range(0, scanCount).Select(_ => new List<(double Mz, double Intensity)>()).ToArray();
            var envelopes = Enumerable.Range(0, scanCount).Select(_ => new List<IsotopeEnvelope>()).ToArray();

            // A fragment in scans 2-6 with 3 ppm of m/z jitter: one trace.
            for (int s = 2; s <= 6; s++)
                peaks[s].Add((500.0 * (1.0 + (s % 2 == 0 ? 3e-6 : -3e-6)), 1000.0 * s));
            // 20 ppm away from it: a different trace.
            for (int s = 2; s <= 5; s++)
                peaks[s].Add((500.01, 800.0));
            // Scans 0, 2 and 4: one missed scan at a time is allowed, so one trace of three points.
            foreach (int s in new[] { 0, 2, 4 })
                peaks[s].Add((300.0, 500.0));
            // Scans 0 and 3: two missed scans break it into two one-point traces, both dropped.
            foreach (int s in new[] { 0, 3 })
                peaks[s].Add((700.0, 500.0));
            // An isotope envelope in scans 3-7 (charge 2, monoisotopic mass 1000), whose two peaks
            // must not also appear as centroid traces.
            for (int s = 3; s <= 7; s++)
            {
                peaks[s].Add((501.007276, 2000.0));
                peaks[s].Add((501.508710, 1500.0));
            }

            var scans = new List<WindowScan>();
            for (int s = 0; s < scanCount; s++)
            {
                var sorted = peaks[s].OrderBy(p => p.Mz).ToArray();
                var mzs = sorted.Select(p => p.Mz).ToArray();
                if (s >= 3 && s <= 7)
                {
                    int first = Array.IndexOf(mzs, 501.007276);
                    envelopes[s].Add(new IsotopeEnvelope(1000.0, 2, 3500f, 501.007276, 0.99, 501.0, 501.6,
                        new[] { first, first + 1 }));
                }

                scans.Add(new WindowScan(20.0 + s * 0.0183, IONS_PER_INTENSITY, mzs,
                    sorted.Select(p => p.Intensity).ToArray(), envelopes[s]));
            }

            var traces = TraceBuilder.Build(scans, new TraceBuilderSettings { IntensityFloorQuantile = 0.0 });

            // One anchor: singly charged monoisotopic m/z, charge kept, intensities in ions.
            var anchors = traces.Where(t => t.IsAnchor).ToList();
            Assert.AreEqual(1, anchors.Count);
            Assert.AreEqual(1000.0 + TraceBuilder.PROTON, anchors[0].Mz, 1e-9);
            Assert.AreEqual(2, anchors[0].Charge);
            Assert.AreEqual(3500.0 * IONS_PER_INTENSITY, anchors[0].Intensities[5], 1e-6);
            Assert.AreEqual(0.0, anchors[0].Intensities[2]);

            // Centroid traces: 500.000, 500.010 and 300.000; not 700 (broken), not the envelope's peaks.
            var centroids = traces.Where(t => !t.IsAnchor).OrderBy(t => t.Mz).ToList();
            Assert.AreEqual(3, centroids.Count, string.Join(@"; ", centroids));
            Assert.AreEqual(300.0, centroids[0].Mz, 1e-9);
            Assert.AreEqual(3, centroids[0].Intensities.Count(v => v > 0.0));
            Assert.AreEqual(500.0, centroids[1].Mz, 500.0 * 5e-6);
            CollectionAssert.AreEqual(new[] { 0.0, 0.0, 8.0, 12.0, 16.0, 20.0, 24.0, 0.0, 0.0, 0.0 },
                centroids[1].Intensities.Select(v => Math.Round(v, 6)).ToArray());
            Assert.AreEqual(500.01, centroids[2].Mz, 1e-9);
            Assert.AreEqual(4, centroids[2].Intensities.Count(v => v > 0.0));
            Assert.IsFalse(centroids.Any(t => Math.Abs(t.Mz - 501.007276) < 0.01 || Math.Abs(t.Mz - 501.50871) < 0.01));

            // The intensity floor drops traces whose highest point is below the quantile.
            var floored = TraceBuilder.Build(scans, new TraceBuilderSettings { IntensityFloorQuantile = 0.5 });
            Assert.IsFalse(floored.Any(t => !t.IsAnchor && Math.Abs(t.Mz - 300.0) < 0.01));
        }
    }
}
