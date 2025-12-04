/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class PiecewiseLinearMapTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestReducePointCount()
        {
            var points = Enumerable.Range(0, 100).Select(i => new KeyValuePair<double, double>(Math.E * i, Math.PI * i)).ToArray();
            int perturbedIndex = 27;
            var perturbedPoint = points[perturbedIndex];
            perturbedPoint = new KeyValuePair<double, double>(perturbedPoint.Key, perturbedPoint.Value + .000001);
            points[perturbedIndex] = perturbedPoint;
            var piecewiseLinearMap = PiecewiseLinearMap.FromValues(points);
            var reducedMap = piecewiseLinearMap.ReducePointCount(5);
            Assert.AreEqual(5, reducedMap.Count);
            int indexPerturbed = reducedMap.XValues.ToList().IndexOf(perturbedPoint.Key);
            Assert.IsTrue(indexPerturbed > 0, "{0} should be greater than 0", indexPerturbed);
            Assert.AreEqual(perturbedPoint.Value, reducedMap.YValues.ElementAt(indexPerturbed));
        }

        [TestMethod]
        public void TestExtrapolatePiecewiseMapGetY()
        {
            var map = PiecewiseLinearMap.FromValues(new[] { 0.0, 2.0 }, new[] { 3.0, 7.0 });
            Assert.AreEqual(1.0, map.GetY(-1));
            Assert.AreEqual(3.0, map.GetY(0));
            Assert.AreEqual(5.0, map.GetY(1));
            Assert.AreEqual(7.0, map.GetY(2));
            Assert.AreEqual(9.0, map.GetY(3));

            map = PiecewiseLinearMap.FromValues(new[] { 0.0, 2.0, 3.0 }, new[] { 3.0, 7.0, 11.0 });
            Assert.AreEqual(1.0, map.GetY(-1));
            Assert.AreEqual(3.0, map.GetY(0));
            Assert.AreEqual(5.0, map.GetY(1));
            Assert.AreEqual(7.0, map.GetY(2));
            Assert.AreEqual(11.0, map.GetY(3));
            Assert.AreEqual(15.0, map.GetY(4));
        }
    }
}
