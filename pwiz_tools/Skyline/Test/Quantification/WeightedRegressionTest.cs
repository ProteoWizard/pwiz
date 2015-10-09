/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using MathNet.Numerics.LinearRegression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Quantification
{
    [TestClass]
    public class WeightedRegressionTest : AbstractUnitTest
    {
        [TestMethod]
        public void CompareIntegerWeights()
        {
            Random random = new Random((int) DateTime.Now.Ticks);
            List<double> xRepeatedValues = new List<double>();
            List<double> yRepeatedValues = new List<double>();
            List<int> weights = new List<int>();
            var sampleTuples = new List<Tuple<double[], double>>();
            for (int i = 0; i < 10; i++)
            {
                int weight = random.Next(1, 10);
                weights.Add(weight);
                double x = random.NextDouble();
                xRepeatedValues.AddRange(Enumerable.Repeat(x, weight));
                double y = random.NextDouble();
                yRepeatedValues.AddRange(Enumerable.Repeat(y, weight));
                sampleTuples.Add(new Tuple<double[], double>(new[]{x}, y));
            }
            const double epsilon = 1E-12;
            var repeatedIntercept = Statistics.Intercept(new Statistics(yRepeatedValues),
                new Statistics(xRepeatedValues));
            var repeatedSlope = Statistics.Slope(new Statistics(yRepeatedValues), new Statistics(xRepeatedValues));
            var repeatedSlopeWithoutIntercept = SlopeWithoutIntercept(yRepeatedValues, xRepeatedValues);

            var weightedRegression = WeightedRegression.Weighted(sampleTuples, weights.Select(w=>(double)w).ToArray(), true);
            Assert.AreEqual(repeatedIntercept, weightedRegression[0], epsilon);
            Assert.AreEqual(repeatedSlope, weightedRegression[1], epsilon);
            var weightedRegressionWithoutIntercept = WeightedRegression.Weighted(sampleTuples,
                weights.Select(w => (double) w).ToArray());
            Assert.AreEqual(repeatedSlopeWithoutIntercept, weightedRegressionWithoutIntercept[0], epsilon);
        }

        private static double SlopeWithoutIntercept(List<double> y, List<double> x)
        {
            double dotProduct = 0;
            double sumOfXSquared = 0;
            for (int i = 0; i < y.Count; i++)
            {
                dotProduct += y[i]*x[i];
                sumOfXSquared += x[i]*x[i];
            }
            return dotProduct/sumOfXSquared;
        }
    }
}
