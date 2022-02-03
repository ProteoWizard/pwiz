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
            var sortedList = ImmutableSortedList.FromValues(new Dictionary<float, float>
            {
                {1, 2},
                {2, 4},
                {3, 5}
            });
            var map = new ScoreQValueMap(sortedList);
            Assert.AreEqual(2, map.GetQValue(0));
            Assert.AreEqual(2, map.GetQValue(1));
            Assert.AreEqual(3, map.GetQValue(1.5f));
            Assert.AreEqual(4, map.GetQValue(2));
            Assert.AreEqual(4.5f, map.GetQValue(2.5f));
            Assert.AreEqual(5, map.GetQValue(3));
            Assert.AreEqual(5, map.GetQValue(3.5f));
        }
    }
}
