/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ScoreQValueMapTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestScoreQValueMap()
        {
            var sortedList = ImmutableSortedList.FromValues(new Dictionary<double, double>
            {
                {1, .5},
                {2, .4},
                {3, .2}
            });
            var map = new ScoreQValueMap(sortedList);
            const double epsilon = 1e-8;
            Assert.IsNull(map.GetQValue(0));
            Assert.AreEqual(.5, map.GetQValue(1).Value, epsilon);
            Assert.AreEqual(.45, map.GetQValue(1.5).Value, epsilon);
            Assert.AreEqual(.4, map.GetQValue(2).Value, epsilon);
            Assert.AreEqual(.3, map.GetQValue(2.5).Value, epsilon);
            Assert.AreEqual(.2, map.GetQValue(3).Value, epsilon);
            Assert.AreEqual(.2, map.GetQValue(3.5).Value, epsilon);
        }
    }
}
