using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class TimeIntervalsTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestFromScanTimes()
        {
            var intervals = TimeIntervals.FromScanTimes(new float[] {1, 2, 3, 4, 8, 9, 11, 15, 16}, 1);
            Assert.AreEqual(3, intervals.Count);
            Assert.AreEqual(new KeyValuePair<float, float>(1, 4), intervals.Intervals.First());
            Assert.AreEqual(new KeyValuePair<float, float>(8, 9), intervals.Intervals.Skip(1).First());
            Assert.AreEqual(new KeyValuePair<float, float>(15, 16), intervals.Intervals.Last());

            intervals = TimeIntervals.FromScanTimes(new float[]{2,4,6,8}, 1);
            Assert.AreEqual(0, intervals.Count);
        }

        [TestMethod]
        public void TestMergeIntervals()
        {
            var intervals1 = TimeIntervals.FromIntervals(new []{new KeyValuePair<float, float>(1, 3),
                new KeyValuePair<float, float>(5, 7)});
            var intervals2 = TimeIntervals.FromIntervals(new[] {new KeyValuePair<float, float>(2, 6)});
            var intersection = intervals1.Intersect(intervals2);
            Assert.AreEqual(2, intersection.Count);
            Assert.AreEqual(new KeyValuePair<float, float>(2, 3), intersection.Intervals.First());
            Assert.AreEqual(new KeyValuePair<float, float>(5, 6), intersection.Intervals.Last());
        }
    }
}
