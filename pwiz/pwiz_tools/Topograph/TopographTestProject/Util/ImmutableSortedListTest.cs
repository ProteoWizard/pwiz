using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;

namespace pwiz.Topograph.Test.Util
{
    /// <summary>
    /// Summary description for ImmutableSortedListTest
    /// </summary>
    [TestClass]
    public class ImmutableSortedListTest
    {
        [TestMethod]
        public void TestBinarySearch()
        {
            int seed = (int) DateTime.Now.Ticks;
            Console.Out.WriteLine("ImmutableSortedList.TestBinarySearch seed: {0}", seed);
            int iteration = 0;
            Random random = new Random(seed);
            for (int maxValue = 10; maxValue < 1000; maxValue *= 10)
            {
                int size = random.Next(maxValue*2);
                var list = new List<int>(size);
                while (list.Count < size)
                {
                    list.Add(random.Next(maxValue));
                }
                var immutableSortedList = ImmutableSortedList.FromValues(list.Select(i => new KeyValuePair<int, int>(i, i)));
                list.Sort();
                CollectionAssert.AreEqual(list, immutableSortedList.Keys.ToArray());
                for (int key = -1; key < maxValue + 1; key++)
                {
                    var msg = "Iteration #" + iteration+": Searching for " + key + " in [" + string.Join(",", list.ToArray()) + "]";
                    iteration++;
                    int key1 = key;
                    int firstIndex = CollectionUtil.BinarySearch(list, i => i.CompareTo(key1), true);
                    int lastIndex = CollectionUtil.BinarySearch(list, i => i.CompareTo(key1), false);
                    Assert.IsTrue(firstIndex <= lastIndex);
                    var range = immutableSortedList.BinarySearch(key);
                    if (range.Length <= 0)
                    {
                        Assert.AreEqual(0, range.Length, msg);
                        Assert.AreEqual(~firstIndex, range.Start, msg);
                        Assert.AreEqual(~lastIndex, range.End, msg);
                    }
                    else
                    {
                        Assert.AreEqual(firstIndex, range.Start, msg);
                        Assert.AreEqual(lastIndex + 1, range.End, msg);
                    }
                    Assert.IsTrue(range.Start == list.Count || list[range.Start] >= key);
                    Assert.IsTrue(range.End == list.Count || list[range.End] > key);
                    Assert.IsTrue(range.Start == 0 || list[range.Start - 1] < key);
                }
            }
        }
    }
}
