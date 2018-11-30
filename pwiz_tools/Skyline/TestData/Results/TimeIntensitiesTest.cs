/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    [TestClass]
    public class TimeIntensitiesTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestMergeTimesAndAddIntensities()
        {
            TimeIntensities oddTimes = new TimeIntensities(new []{1f,3,5,7,9}, new []{3f,6,9,12,15}, null, null);
            TimeIntensities evenTimes = new TimeIntensities(new []{2f, 4, 6,8}, new[]{10f,9,8,7}, null, null);
            var expectedMergedTimes = ImmutableList.ValueOf(new[] {1f, 2, 3, 4, 5, 6, 7, 8, 9});
            var mergedAndAdded = oddTimes.MergeTimesAndAddIntensities(evenTimes);
            var mergedAndAdded2 = evenTimes.MergeTimesAndAddIntensities(oddTimes);
            Assert.AreEqual(expectedMergedTimes, mergedAndAdded.Times);
            Assert.AreEqual(expectedMergedTimes, mergedAndAdded2.Times);
            Assert.AreEqual(mergedAndAdded.Intensities, mergedAndAdded2.Intensities);

            var oddInterpolated = oddTimes.Interpolate(expectedMergedTimes, false);
            var evenInterpolated = evenTimes.Interpolate(expectedMergedTimes, false);
            var sumOfInterpolatedIntensities = oddInterpolated.Intensities
                .Zip(evenInterpolated.Intensities, (f1, f2) => f1 + f2)
                .ToArray();
            CollectionAssert.AreEqual(sumOfInterpolatedIntensities, mergedAndAdded.Intensities.ToArray());

            var addedToOdds = oddTimes.AddIntensities(evenTimes);
            Assert.AreEqual(oddTimes.Times, addedToOdds.Times);
            var addedToEvens = evenTimes.AddIntensities(oddTimes);
            Assert.AreEqual(evenTimes.Times, addedToEvens.Times);
        }
    }
}
