using MathNet.Numerics.LinearRegression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class BilinearFitTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestWeightedRegression()
        {
            var x = new[] {0.05, 0.07, 0.1, 0.3, 0.5, 0.7, 1.0};
            var y = new[]
            {
                0.17310666666666666, 0.1674433333333333, 0.15965666666666667, 0.18022333333333332, 0.18300000000000002,
                0.17776666666666666, 0.17531666666666668
            };
            var weights = new[]
            {
                20.0, 14.285714285714285, 10.0, 3.3333333333333335, 2.0, 1.4285714285714286, 1.0
            };
            var result = WeightedRegression.Weighted(x.Zip(y, (a, b) => Tuple.Create(new []{a}, b)), weights, true);
            // Assert.AreEqual(0.00239897, result[0], 1e-6);
            // Assert.AreEqual(0.16965172, result[1], 1e-6);
            var difference1 = CalculateSumOfDifferences(x, y, weights, result[1], result[0], false);
            var difference2 = CalculateSumOfDifferences(x, y, weights, .00239897, .16965172, false);
            var difference3 = CalculateSumOfDifferences(x, y, weights, 0.00113951, 0.17091118, false);
            Assert.IsTrue(difference1 < difference2);
            Assert.IsTrue(difference1 < difference3);
        }

        public static double CalculateSumOfDifferences(IList<double> x, IList<double> y, IList<double> weights, double slope,
            double intercept, bool squareArea)
        {
            double sum = 0;
            for (int i = 0; i < x.Count; i++)
            {
                var expected = slope * x[i] + intercept;
                var difference = y[i] - expected;
                sum += difference * difference * weights[i];
            }

            return sum;
        }
    }
}
