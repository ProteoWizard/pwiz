/*
 * Original author: Mike MacCoss <maccoss .at. uw.edu>,
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests when <see cref="ChromGroups"/> releases a chromatogram relative to its spill file's MaxTime,
    /// and that spilled data is read back intact. Several spectra can share one retention time (e.g. the
    /// spectra demultiplexed from one overlapping-window DIA scan), so a spill file may only be released
    /// once extraction is strictly past its MaxTime. Releasing at MaxTime read the spill file while the
    /// extractor was still appending to it (GitHub issue #4888).
    /// </summary>
    [TestClass]
    public class ChromGroupsTest : AbstractUnitTest
    {
        private const int NOT_DONE = -1;
        private const int STATUS_ID = 7;
        private const int SPILLED_POINT_COUNT = 50;   // several 16-value blocks, so most go to the spill file

        // A retention time that is exactly representable both as the key's double MaxTime and as the
        // float extraction time, as when a peak boundary falls on a scan time.
        private const float MAX_TIME = 48.85519f;

        [TestMethod]
        public void TestChromGroupsReleaseTime()
        {
            // Bounded extraction window: not done before or AT MaxTime, done once past it.
            var boundedKey = MakeKey().ChangeOptionalTimes(MAX_TIME - 2, MAX_TIME);
            AssertEx.AreEqual(NOT_DONE, ReleaseInMemoryAt(boundedKey, MAX_TIME - 1));
            AssertEx.AreEqual(NOT_DONE, ReleaseInMemoryAt(boundedKey, MAX_TIME));
            AssertEx.AreEqual(STATUS_ID, ReleaseInMemoryAt(boundedKey, MathF.BitIncrement(MAX_TIME)));

            // No extraction window (MaxTime is float.MaxValue): only released when extraction completes.
            var unboundedKey = MakeKey();
            AssertEx.AreEqual(NOT_DONE, ReleaseInMemoryAt(unboundedKey, MAX_TIME));
            AssertEx.AreEqual(STATUS_ID, ReleaseInMemoryAt(unboundedKey, float.MaxValue));

            VerifySpilledChromatogram(boundedKey);
        }

        private static ChromKey MakeKey()
        {
            return new ChromKey(null, new SignedMz(630.8295), null, new SignedMz(328.1709), 0, 0, 0,
                ChromSource.fragment, ChromExtractor.summed);
        }

        private ChromGroups CreateChromGroups(ChromKey key)
        {
            var requestOrder = new List<IList<int>> { new[] { 0 } };
            var cachePath = Path.Combine(TestContext.GetTestResultsPath(), nameof(ChromGroupsTest) + ChromatogramCache.EXT);
            return new ChromGroups(requestOrder, new[] { key }, 100, 10, cachePath);
        }

        /// <summary>
        /// Builds a single-chromatogram <see cref="ChromGroups"/> whose data stays in memory
        /// (no spill file is written) and asks it to release the chromatogram at a retention time.
        /// </summary>
        private int ReleaseInMemoryAt(ChromKey key, float retentionTime)
        {
            using var chromGroups = CreateChromGroups(key);
            var collector = new ChromCollector(STATUS_ID, true, false);
            collector.AddTime(0, (float) key.OptionalMaxTime.GetValueOrDefault(1), null);
            collector.AddPoint(0, 1, null, null);
            return chromGroups.ReleaseChromatogram(0, retentionTime, collector, out _);
        }

        /// <summary>
        /// Writes a chromatogram through a <see cref="BlockWriter"/> so its blocks spill to disk, then
        /// checks that the release waits until extraction is past MaxTime, returns every point intact,
        /// and that adding data after the release fails loudly instead of being lost.
        /// </summary>
        private void VerifySpilledChromatogram(ChromKey key)
        {
            using var chromGroups = CreateChromGroups(key);
            var writer = new BlockWriter(chromGroups);
            var collector = new ChromCollector(STATUS_ID, true, false);
            var times = Enumerable.Range(0, SPILLED_POINT_COUNT)
                .Select(i => MAX_TIME - (SPILLED_POINT_COUNT - 1 - i) * 0.03f).ToArray();
            times[times.Length - 1] = MAX_TIME;
            var intensities = Enumerable.Range(1, SPILLED_POINT_COUNT).Select(i => (float) i).ToArray();
            for (int i = 0; i < times.Length; i++)
            {
                collector.AddTime(0, times[i], writer);
                collector.AddPoint(0, intensities[i], null, writer);
            }
            var spillDir = Path.Combine(TestContext.GetTestResultsPath(), @"xic");
            Assert.IsTrue(Directory.Exists(spillDir) && Directory.GetFiles(spillDir).Length > 0);

            AssertEx.AreEqual(NOT_DONE, chromGroups.ReleaseChromatogram(0, MAX_TIME, collector, out _));
            AssertEx.AreEqual(STATUS_ID, chromGroups.ReleaseChromatogram(0, MathF.BitIncrement(MAX_TIME), collector, out var timeIntensities));
            AssertEx.AreEqualDeep(times, timeIntensities.Times.ToArray());
            AssertEx.AreEqualDeep(intensities, timeIntensities.Intensities.ToArray());

            AssertEx.ThrowsException<AssumptionException>(() => collector.AddPoint(0, 1, null, writer));
            AssertEx.ThrowsException<AssumptionException>(() => collector.AddTime(0, MathF.BitIncrement(MAX_TIME), writer));
        }
    }
}
