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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests when <see cref="ChromGroups"/> releases a chromatogram relative to its spill file's MaxTime.
    /// Several spectra can share one retention time (e.g. the spectra demultiplexed from one
    /// overlapping-window DIA scan), so a spill file may only be released once extraction is
    /// strictly past its MaxTime. Releasing at MaxTime read the spill file while the extractor was
    /// still appending to it (GitHub issue #4888).
    /// </summary>
    [TestClass]
    public class ChromGroupsTest : AbstractUnitTest
    {
        private const int NOT_DONE = -1;
        private const int STATUS_ID = 7;

        [TestMethod]
        public void TestChromGroupsReleaseTime()
        {
            // A retention time that is exactly representable both as the key's double MaxTime and
            // as the float extraction time, as when a peak boundary falls on a scan time.
            const float maxTime = 48.85519f;

            // Bounded extraction window: not done before or AT MaxTime, done once past it.
            var boundedKey = MakeKey().ChangeOptionalTimes(maxTime - 2, maxTime);
            AssertEx.AreEqual(NOT_DONE, ReleaseAt(boundedKey, maxTime - 1));
            AssertEx.AreEqual(NOT_DONE, ReleaseAt(boundedKey, maxTime));
            AssertEx.AreEqual(STATUS_ID, ReleaseAt(boundedKey, MathF.BitIncrement(maxTime)));

            // No extraction window (MaxTime is float.MaxValue): only released when extraction completes.
            var unboundedKey = MakeKey();
            AssertEx.AreEqual(NOT_DONE, ReleaseAt(unboundedKey, maxTime));
            AssertEx.AreEqual(STATUS_ID, ReleaseAt(unboundedKey, float.MaxValue));
        }

        private static ChromKey MakeKey()
        {
            return new ChromKey(null, new SignedMz(630.8295), null, new SignedMz(328.1709), 0, 0, 0,
                ChromSource.fragment, ChromExtractor.summed);
        }

        /// <summary>
        /// Builds a single-chromatogram <see cref="ChromGroups"/> whose data stays in memory
        /// (no spill file is written) and asks it to release the chromatogram at a retention time.
        /// </summary>
        private static int ReleaseAt(ChromKey key, float retentionTime)
        {
            var requestOrder = new List<IList<int>> { new[] { 0 } };
            // Spill files are only created when blocks are written, which never happens here.
            var cachePath = Path.Combine(Path.GetTempPath(), nameof(ChromGroupsTest) + ChromatogramCache.EXT);
            using var chromGroups = new ChromGroups(requestOrder, new[] { key }, 100, 10, cachePath);
            var collector = new ChromCollector(STATUS_ID, true, false);
            collector.AddTime(0, (float) key.OptionalMaxTime.GetValueOrDefault(1), null);
            collector.AddPoint(0, 1, null, null);
            return chromGroups.ReleaseChromatogram(0, retentionTime, collector, out _);
        }
    }
}
