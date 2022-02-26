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
            Assert.AreEqual(1, map.GetQValue(0).Value, epsilon);
            Assert.AreEqual(.5, map.GetQValue(1).Value, epsilon);
            Assert.AreEqual(.45, map.GetQValue(1.5).Value, epsilon);
            Assert.AreEqual(.4, map.GetQValue(2).Value, epsilon);
            Assert.AreEqual(.3, map.GetQValue(2.5).Value, epsilon);
            Assert.AreEqual(.2, map.GetQValue(3).Value, epsilon);
            Assert.AreEqual(.2, map.GetQValue(3.5).Value, epsilon);
        }
    }
}
