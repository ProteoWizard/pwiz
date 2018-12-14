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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class LinearInLogSpaceTest : AbstractUnitTest
    {
        /// <summary>
        /// Verifies that for y=mx^p, the slope of the calibration curve is p, and the intercept is
        /// log(m).
        /// </summary>
        [TestMethod]
        public void TestLinearInLogSpace()
        {
            foreach (double factor in new[] {.5, 1.0, 2.0})
            {
                foreach (double power in new [] {-1.0, 0, 1.0, 2.5})
                {
                    var points = Enumerable.Range(1, 10).Select(x => new WeightedPoint(x, factor * Math.Pow(x, power), 1)).ToArray();
                    var calibrationCurve = RegressionFit.LINEAR_IN_LOG_SPACE.Fit(points);
                    Assert.AreEqual(power, calibrationCurve.Slope.Value, .00001);
                    Assert.AreEqual(Math.Log(factor), calibrationCurve.Intercept.Value, .00001);
                }
            }
        }
    }
}
