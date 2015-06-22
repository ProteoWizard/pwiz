using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class BlockedListTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestBlockedList()
        {
            const int ITERATIONS = 1000;

            var random = new Random(1);
            for (int i = 0; i < ITERATIONS; i++)
            {
                var array = new float[random.Next(100, 1000)];
                var blockedList = new BlockedList<float>(random.Next(1, 9));
                for (int j = 0; j < array.Length; j++)
                {
                    array[j] = j;
                    blockedList.Add(j);
                }

                Assert.IsTrue(ArrayUtil.EqualsDeep(array, blockedList.ToArray()));
            }
        }
    }
}
