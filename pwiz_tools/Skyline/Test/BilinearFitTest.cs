using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearRegression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class BilinearFitTest : AbstractUnitTest
    {
        const double delta = 1e-6;
        [TestMethod]
        public void TestFitBilinearCurve()
        {
            var points = GetWeightedPoints();
            var result = BilinearCurveFit.FitBilinearCurve(points);
            Assert.AreEqual(0.025854009062249942, result.Slope, delta);
            Assert.AreEqual(0.16317916521766687, result.Intercept, delta);
            Assert.AreEqual(0.172315, result.BaselineHeight, delta);
            Assert.AreEqual(0.057632885215053185, result.Error, delta);
        }

        [TestMethod]
        public void TestBilinearFitWithOffset()
        {
            var points = GetWeightedPoints();
            var result = BilinearCurveFit.FitBilinearCurveWithOffset(0.03, points);
            Assert.AreEqual(0.00239897, result.Slope, delta);
            Assert.AreEqual(0.16965172, result.Intercept, delta);
            Assert.AreEqual(0.17205111, result.BaselineHeight, delta);
            Assert.AreEqual(0.07240250761116633, result.Error);
        }

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

        private IList<WeightedPoint> GetWeightedPoints()
        {
            var areas = new[]
            {
                0.16751, 0.17056, 0.18132, 0.17482, 0.17060, 0.16879, 0.17469, 0.17645, 0.16372, 0.17310, 0.17941,
                0.16681, 0.18539, 0.17096, 0.14598, 0.15127, 0.17316, 0.15454, 0.18983, 0.17845, 0.17239, 0.18395,
                0.18260, 0.18245, 0.19410, 0.16476, 0.17444, 0.18214, 0.17844, 0.16537
            };
            var concentrations = new[]
            {
                0.005, 0.005, 0.005, 0.01, 0.01, 0.01, 0.03, 0.03, 0.03, 0.05, 0.05, 0.05, 0.07, 0.07, 0.07,
                0.1, 0.1, 0.1, 0.3, 0.3, 0.3, 0.5, 0.5, 0.5, 0.7, 0.7, 0.7, 1.0, 1.0, 1.0
            };
            return concentrations
                .Zip(areas, (conc, area) => Tuple.Create(conc, area)).ToLookup(tuple => tuple.Item1)
                .Select(grouping => new WeightedPoint(grouping.Key, grouping.Average(tuple => tuple.Item2),
                    Math.Pow(grouping.Key <= 0 ? 1e-6 : 1 / grouping.Key, 2))).ToList();
        }
    }
}
