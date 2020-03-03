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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ChromPeakTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestChromPeakIntegration()
        {
            var times = Enumerable.Range(0, 12).Select(i => (float) i).ToArray();
            var timeIntensities = new TimeIntensities(times, times.Select(t => 36 - (t - 6) * (t - 6)), null, null);
            var chromPeak = new ChromPeak(timeIntensities, 1, 11, 0);
            Assert.AreEqual(36f, chromPeak.Height);
            Assert.AreEqual(11, timeIntensities.Intensities[1]);
            Assert.AreEqual(20, timeIntensities.Intensities[2]);
            var fwhmStart = (7 * 2 + 2 * 1) / 9.0;
            Assert.AreEqual(20, timeIntensities.Intensities[10]);
            Assert.AreEqual(11, timeIntensities.Intensities[11]);
            var fwhmEnd = (7 * 10 + 2 * 11) / 9.0;
            Assert.AreEqual(fwhmEnd-fwhmStart, chromPeak.Fwhm, .00001);
            Assert.AreEqual(false, chromPeak.IsFwhmDegenerate);
            var chromPeak2 = new ChromPeak(timeIntensities, 2, 10, 0);
            Assert.AreEqual(8, chromPeak2.Fwhm);
            Assert.AreEqual(true, chromPeak2.IsFwhmDegenerate);
        }
    }
}
