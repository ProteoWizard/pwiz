using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NHibernate.Criterion;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class PeakShapeStatisticsTest : AbstractUnitTest
    {
        private const double epsilon = .00001;
        [TestMethod]
        public void TestPeakShapeMedianTriangle()
        {
            var times = new float[] {0, 1};
            var intensities = new float[] {0, 1};
            var stats = PeakShapeStatistics.Calculate(times, intensities);
            Assert.AreEqual(.5, stats.Area, epsilon);
            Assert.AreEqual(2.0/3, stats.MeanTime, epsilon);
            Assert.AreEqual(2.0/3, stats.MedianTime, epsilon);
        }

        [TestMethod]
        public void TestPeakShapeOneTriangle()
        {
            var times = new float[] {0, 1, 2};
            var intensities = new float[] {0, 1, 0};
            var stats = PeakShapeStatistics.Calculate(times, intensities);
            Assert.AreEqual(1.0, stats.Area, epsilon);
            Assert.AreEqual(1.0, stats.MeanTime, epsilon);
            Assert.AreEqual(1.0, stats.MedianTime, epsilon);
        }

        [TestMethod]
        public void TestPeakShapeThreeTriangles()
        {
            var times = new float[] {0, 1, 2, 5, 6, 7, 15, 16, 17};
            var intensities = new float[] { 0, 1, 0, 0, 1, 0, 0, 1, 0};
            var stats = PeakShapeStatistics.Calculate(times, intensities);
            Assert.AreEqual(3, stats.Area, epsilon);
            Assert.AreEqual(6, stats.MedianTime, epsilon);
            Assert.AreEqual(23.0/3, stats.MeanTime, epsilon);
        }
    }
}
