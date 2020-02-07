/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class TimeIntervalsTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestFromScanTimes()
        {
            var intervals = TimeIntervals.FromScanTimes(new float[] {1, 2, 3, 4, 8, 9, 11, 15, 16}, 1);
            Assert.AreEqual(3, intervals.Count);
            Assert.AreEqual(new KeyValuePair<float, float>(1, 4), intervals.Intervals.First());
            Assert.AreEqual(new KeyValuePair<float, float>(8, 9), intervals.Intervals.Skip(1).First());
            Assert.AreEqual(new KeyValuePair<float, float>(15, 16), intervals.Intervals.Last());

            intervals = TimeIntervals.FromScanTimes(new float[]{2,4,6,8}, 1);
            Assert.AreEqual(0, intervals.Count);
        }

        [TestMethod]
        public void TestMergeIntervals()
        {
            var intervals1 = TimeIntervals.FromIntervals(new []{new KeyValuePair<float, float>(1, 3),
                new KeyValuePair<float, float>(5, 7)});
            var intervals2 = TimeIntervals.FromIntervals(new[] {new KeyValuePair<float, float>(2, 6)});
            var intersection = intervals1.Intersect(intervals2);
            Assert.AreEqual(2, intersection.Count);
            Assert.AreEqual(new KeyValuePair<float, float>(2, 3), intersection.Intervals.First());
            Assert.AreEqual(new KeyValuePair<float, float>(5, 6), intersection.Intervals.Last());
        }

        [TestMethod]
        public void TestMergeIntervals2()
        {
            var intervals1 = TimeIntervals.FromIntervals(new[]
            {
                new KeyValuePair<float, float>(24.81052f, 24.98775f),
                new KeyValuePair<float, float>(34.53197f, 34.53515f),
                new KeyValuePair<float, float>(34.83207f, 35.22802f),
                new KeyValuePair<float, float>(38.6576f, 38.69949f),
                new KeyValuePair<float, float>(39.49813f, 39.92231f),
                new KeyValuePair<float, float>(39.96412f, 40.01009f),
                new KeyValuePair<float, float>(40.05416f, 40.16293f),
                new KeyValuePair<float, float>(49.02427f, 49.05523f),
                new KeyValuePair<float, float>(54.82053f, 54.94222f),
                new KeyValuePair<float, float>(54.9833f, 55.0249f),
                new KeyValuePair<float, float>(57.92376f, 57.92695f),
                new KeyValuePair<float, float>(58.0083f, 58.04743f),
                new KeyValuePair<float, float>(63.76418f, 63.86788f),
                new KeyValuePair<float, float>(65.5108f, 65.52985f),
                new KeyValuePair<float, float>(65.57608f, 65.65861f),
                new KeyValuePair<float, float>(66.70279f, 67.11095f),
                new KeyValuePair<float, float>(70.06538f, 70.125f),
                new KeyValuePair<float, float>(77.27367f, 77.53282f),
                new KeyValuePair<float, float>(83.9983f, 84.04679f),
            });
            var intervals2 = TimeIntervals.FromIntervals(new[]
            {
                new KeyValuePair<float, float>(0.002652853f, 97.00591f)
            });
            var result = intervals1.Intersect(intervals2);
            Assert.AreEqual(19, result.Count);
        }
    }
}
