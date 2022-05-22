using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class PeakShapeStatisticsTest : AbstractUnitTest
    {
        private const double epsilon = .00001;
        [TestMethod]
        public void TestPeakShapeQuadrilateral()
        {
            var times = new double[] {0, 1, 7.0/3};
            var intensities = new double[] {1, 3, 0};
            var stats = PeakShapeStatistics.Calculate(times, intensities);
            Assert.AreEqual(4, stats.Area, epsilon);
            Assert.AreEqual(1, stats.MedianTime, epsilon);
            VerifyStatisticsBySampling(times, intensities);
        }

        [TestMethod]
        public void TestPeakShapeOneTriangle()
        {
            var times = new double[] {0, 1, 2};
            var intensities = new double[] {0, 1, 0};
            var stats = PeakShapeStatistics.Calculate(times, intensities);
            Assert.AreEqual(1.0, stats.Area, epsilon);
            Assert.AreEqual(1.0, stats.MeanTime, epsilon);
            Assert.AreEqual(1.0, stats.MedianTime, epsilon);
            Assert.AreEqual(1 / Math.Sqrt(6), stats.StdDevTime, epsilon);
            VerifyStatisticsBySampling(times, intensities);
        }

        [TestMethod]
        public void TestPeakShapeThreeTriangles()
        {
            var times = new double[] {0, 1, 2, 5, 6, 7, 15, 16, 17};
            var intensities = new double[] { 0, 1, 0, 0, 1, 0, 0, 1, 0};
            var stats = PeakShapeStatistics.Calculate(times, intensities);
            Assert.AreEqual(3, stats.Area, epsilon);
            Assert.AreEqual(6, stats.MedianTime, epsilon);
            Assert.AreEqual(23.0/3, stats.MeanTime, epsilon);
            VerifyStatisticsBySampling(times, intensities);
        }

        [TestMethod]
        public void TestPeakShapeRectangle()
        {
            var times = new double[] {0, 1};
            var intensities = new double[] {1, 1};
            var stats = PeakShapeStatistics.Calculate(times, intensities);
            Assert.AreEqual(1.0, stats.Area, epsilon);
            Assert.AreEqual(.5, stats.MeanTime);
            Assert.AreEqual(.5, stats.MedianTime);
            Assert.AreEqual(1/ Math.Sqrt(12), stats.StdDevTime, epsilon);
            VerifyStatisticsBySampling(times, intensities);
        }

        [TestMethod]
        public void TestPeakShapeRectangle2()
        {
            var times = new double[] { 1, 2 };
            var intensities = new double[] { 1, 1 };
            var stats = PeakShapeStatistics.Calculate(times, intensities);
            Assert.AreEqual(1.0, stats.Area);
            Assert.AreEqual(1.5, stats.MeanTime);
            Assert.AreEqual(1.5, stats.MedianTime);
            Assert.AreEqual(1/Math.Sqrt(12), stats.StdDevTime, epsilon);
            VerifyStatisticsBySampling(times, intensities);
        }

        [TestMethod]
        public void TestPeakShapeSmallGaussian()
        {
            var times = new double[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
            var intensities = new double[]
            {
                0.00876415, 0.026995483, 0.064758798, 0.120985362, 0.176032663, 0.19947114, 0.176032663, 0.120985362,
                0.064758798, 0.026995483, 0.00876415
            };
            var stats = PeakShapeStatistics.Calculate(times, intensities);
            Assert.AreEqual(1.0, stats.Area, .15);
            Assert.AreEqual(2.0, stats.StdDevTime, .1);
            VerifyStatisticsBySampling(times, intensities);
        }

        [TestMethod]
        public void TestPeakShapeBigGaussian()
        {
            var times = Enumerable.Range(0, 121).Select(i => (double) i).ToArray();
            var normal = new MathNet.Numerics.Distributions.Normal(60, 20);
            var intensities = times.Select(t => normal.Density(t)).ToArray();
            var stats = PeakShapeStatistics.Calculate(times, intensities);
            // 99.7% of the area is supposed to be within 3 standard deviations of the mean
            Assert.AreEqual(.997, stats.Area, .001);

            Assert.AreEqual(20.0, stats.StdDevTime, .5);
            VerifyStatisticsBySampling(times, intensities);
        }

        [TestMethod]
        public void TestSkewness()
        {
            var times = new double[] {0, 1};
            var intensities = new double[] {0, 1};
            var stats = PeakShapeStatistics.Calculate(times, intensities);

            var samples = new List<double>();
            for (int i = 0; i <= 10000; i++)
            {
                var time = i / 10000.0;
                samples.AddRange(Enumerable.Repeat(time, i));
            }

            var runningStatistics = new RunningStatistics(samples);
            Assert.AreEqual(runningStatistics.Mean, stats.MeanTime, .001);
            Assert.AreEqual(runningStatistics.StandardDeviation, stats.StdDevTime, .001);
            Assert.AreEqual(runningStatistics.Skewness, stats.Skewness, .001);
            Assert.AreEqual(runningStatistics.Kurtosis, stats.Kurtosis, .001);

            VerifyStatisticsBySampling(times, intensities);
        }

        private void VerifyStatisticsBySampling(IList<double> times, IList<double> intensities)
        {
            var peakShapeStatistics = PeakShapeStatistics.Calculate(times, intensities);
            var runningStatistics = GetRunningStatisticsBySampling(times, intensities);
            Assert.AreEqual(runningStatistics.Mean, peakShapeStatistics.MeanTime, .01);
            Assert.AreEqual(runningStatistics.StandardDeviation, peakShapeStatistics.StdDevTime, .01);
            Assert.AreEqual(runningStatistics.Skewness, peakShapeStatistics.Skewness, .01);
            Assert.AreEqual(runningStatistics.Kurtosis, peakShapeStatistics.Kurtosis, .01);
        }

        private RunningStatistics GetRunningStatisticsBySampling(IList<double> times, IList<double> intensities)
        {
            var samples = new List<double>();
            var maxIntensity = intensities.Max();
            var timeIntensities = new TimeIntensities(times.Select(t => (float) t), intensities.Select(i => (float) i),
                null, null);
            const int nTimes = 1000;
            for (int i = 0; i < nTimes; i++)
            {
                var time = (times[0] + i * (times[times.Count - 1] - times[0]) / nTimes);
                var interpolatedTimeIntensities = timeIntensities.InterpolateTime((float) time);
                var intensity = interpolatedTimeIntensities.Intensities[interpolatedTimeIntensities.IndexOfNearestTime((float) time)];
                int count = (int) Math.Round(intensity * 100 / maxIntensity);
                samples.AddRange(Enumerable.Repeat(time, count));
            }

            return new RunningStatistics(samples);
        }
    }
}
