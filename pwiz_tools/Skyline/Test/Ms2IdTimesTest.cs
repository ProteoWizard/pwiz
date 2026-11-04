/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests rejection of outlier MS/MS ID times before they set the extraction window
    /// for <see cref="RetentionTimeFilterType.ms2_ids"/>.
    /// </summary>
    [TestClass]
    public class Ms2IdTimesTest : AbstractUnitTest
    {
        private const double TOLERANCE = TransitionFullScan.DEFAULT_TIME_AROUND_MS2_IDS;

        [TestMethod]
        public void TestRemoveOutlierIdTimes()
        {
            // From the DDASearch tutorial: charge 2 and 3 IDs of R.TGEGFLCVFAINNTK.S in 2_UPS1_300fmol,
            // where a false charge 3 match at 20.79 extended extraction to nearly the whole run
            AssertRemoved(new[] { 138.38, 137.65, 137.98, 20.79 }, new[] { 138.38, 137.65, 137.98 });
            // Aligned times are filtered the same way
            AssertRemoved(new[] { 137.4, 21.3, 137.9, 138.1, 137.6 }, new[] { 137.4, 137.9, 138.1, 137.6 });

            // Nothing to reject
            AssertRemoved(new double[0], new double[0]);
            AssertRemoved(new[] { 42.0 }, new[] { 42.0 });
            AssertRemoved(new[] { 40.0, 44.0, 41.5 }, new[] { 40.0, 44.0, 41.5 });

            // A time exactly the tolerance from the median is kept
            AssertRemoved(new[] { 50.0, 50.0 + TOLERANCE, 50.0 - TOLERANCE }, new[] { 50.0, 50.0 + TOLERANCE, 50.0 - TOLERANCE });

            // IDs spread densely across a broad elution are all kept, though most are far from the median,
            // as in the low resolution BSA data of the TargetedMSMS tutorial
            var broadElution = Enumerable.Range(0, 41).Select(i => 40.0 + i * 0.5).ToArray();
            AssertRemoved(broadElution.Append(77.0).ToArray(), broadElution);
            // A gap wider than the tolerance ends the run of IDs connected to the median
            AssertRemoved(new[] { 30.0, 34.0, 38.0, 43.5 }, new[] { 30.0, 34.0, 38.0 });
            AssertRemoved(new[] { 20.0, 60.0, 61.0 }, new[] { 60.0, 61.0 });

            // With no majority, the evidence cannot say which ID is wrong, so all are kept
            AssertRemoved(new[] { 20.0, 60.0 }, new[] { 20.0, 60.0 });
            AssertRemoved(new[] { 20.0, 60.0, 100.0, 140.0 }, new[] { 20.0, 60.0, 100.0, 140.0 });
            // Including an odd number of IDs, where the median is itself one of them
            AssertRemoved(new[] { 20.0, 60.0, 100.0 }, new[] { 20.0, 60.0, 100.0 });
            AssertRemoved(new[] { 20.0, 20.2, 60.0, 100.0, 100.2 }, new[] { 20.0, 20.2, 60.0, 100.0, 100.2 });

            // Only the ms2_ids filter uses its length as a tolerance
            var times = new[] { 30.0, 30.5, 31.0, 90.0 };
            var fullScan = new TransitionFullScan();
            AssertEx.AreEqualDeep(times, fullScan.RemoveOutlierIdTimes(times));
            var fullScanMs2Ids = fullScan.ChangeRetentionTimeFilter(RetentionTimeFilterType.ms2_ids, TOLERANCE);
            AssertEx.AreEqualDeep(new[] { 30.0, 30.5, 31.0 }, fullScanMs2Ids.RemoveOutlierIdTimes(times));
            var fullScanWindows = fullScan.ChangeRetentionTimeFilter(RetentionTimeFilterType.scheduling_windows, TOLERANCE);
            AssertEx.AreEqualDeep(times, fullScanWindows.RemoveOutlierIdTimes(times));
        }

        private static void AssertRemoved(double[] times, double[] expected)
        {
            AssertEx.AreEqualDeep(expected, TransitionFullScan.RemoveOutlierIdTimes(times, TOLERANCE));
        }
    }
}
