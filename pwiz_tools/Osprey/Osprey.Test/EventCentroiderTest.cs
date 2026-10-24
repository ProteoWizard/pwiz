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
using pwiz.Osprey.Demux;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for <see cref="EventCentroider"/> on a simulated TOF profile, sampled uniformly in
    /// sqrt(m/z) as a digitizer is, with the zero points either kept or dropped.
    /// </summary>
    [TestClass]
    public class EventCentroiderTest
    {
        private const double FIRST_ROOT = 20.0;       // sqrt(400 m/z)
        private const double ROOT_STEP = 0.00025;      // about 10 mDa at 400 m/z

        /// <summary>
        /// A single-point event is its own centroid; a six-point peak is one centroid holding its sum
        /// at its intensity-weighted m/z; two peaks touching over a deep valley are two; a shallow
        /// dip does not split a peak; a one-point gap separates two peaks; and dropping the zero
        /// points changes nothing.
        /// </summary>
        [TestMethod]
        public void TestEventCentroids()
        {
            // Sample index -> intensity; every other sample is zero.
            var signal = new Dictionary<int, double>
            {
                [10] = 100,                                                        // a single ion event
                [30] = 100, [31] = 200, [32] = 400, [33] = 300, [34] = 200, [35] = 100,  // one peak
                [60] = 300, [61] = 600, [62] = 100, [63] = 500, [64] = 200,       // two peaks, deep valley
                [90] = 300, [91] = 500, [92] = 400, [93] = 450, [94] = 200,       // one peak, shallow dip
                [120] = 200, [122] = 200,                                          // one missing sample apart
            };
            var expected = new List<(double Mz, double Intensity)>
            {
                Centroid(signal, 10, 10),
                Centroid(signal, 30, 35),
                Centroid(signal, 60, 62),   // the valley point goes to the left peak
                Centroid(signal, 63, 64),
                Centroid(signal, 90, 94),
                Centroid(signal, 120, 120),
                Centroid(signal, 122, 122),
            };

            var mz = Enumerable.Range(0, 150).Select(MzOf).ToList();
            var dense = Enumerable.Range(0, 150).Select(i => signal.TryGetValue(i, out double v) ? v : 0).ToList();
            AssertCentroids(expected, mz, dense);

            var sparseIndex = signal.Keys.OrderBy(i => i).ToList();
            AssertCentroids(expected, sparseIndex.Select(MzOf).ToList(), sparseIndex.Select(i => signal[i]).ToList());

            AssertCentroids(new List<(double Mz, double Intensity)>(), Array.Empty<double>(), Array.Empty<double>());
        }

        private static void AssertCentroids(List<(double Mz, double Intensity)> expected, IReadOnlyList<double> mz,
            IReadOnlyList<double> intensity)
        {
            var centroidMz = new List<double>();
            var centroidIntensity = new List<double>();
            EventCentroider.Centroid(mz, intensity, centroidMz, centroidIntensity);
            Assert.AreEqual(expected.Count, centroidMz.Count);
            for (int k = 0; k < expected.Count; k++)
            {
                Assert.AreEqual(expected[k].Mz, centroidMz[k], 1e-9, @"centroid " + k);
                Assert.AreEqual(expected[k].Intensity, centroidIntensity[k], 1e-9, @"centroid " + k);
            }
        }

        private static (double Mz, double Intensity) Centroid(Dictionary<int, double> signal, int first, int last)
        {
            double sum = 0, weighted = 0;
            for (int i = first; i <= last; i++)
            {
                if (!signal.TryGetValue(i, out double v))
                    continue;
                sum += v;
                weighted += v * MzOf(i);
            }
            return (weighted / sum, sum);
        }

        private static double MzOf(int sample)
        {
            return Math.Pow(FIRST_ROOT + sample * ROOT_STEP, 2);
        }
    }
}
