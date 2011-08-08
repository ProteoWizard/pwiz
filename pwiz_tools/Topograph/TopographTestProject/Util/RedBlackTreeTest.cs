using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;

namespace pwiz.Topograph.Test.Util
{
    /// <summary>
    /// Summary description for RedBlackTreeTest
    /// </summary>
    [TestClass]
    public class 
RedBlackTreeTest
    {
        [TestMethod]
        public void TestRedBlackTree()
        {
            var sizes = new [] {0, 1, 2, 3, 4, 5, 6, 97, 354, 678};
            foreach (int seed in new[] {0, Environment.TickCount})
            {
                var random = new Random(seed);
                var intSet = new HashSet<int>();
                var intList = new List<int>();
                foreach (int size in sizes)
                {
                    while (intList.Count < size)
                    {
                        int nextInt;
                        do
                        {
                            nextInt = random.Next();
                        } while (intSet.Contains(nextInt));
                        intSet.Add(nextInt);
                        intList.Add(nextInt);
                    }
                    var tree = new RedBlackTree();
                    var sortedList = intSet.ToList();
                    sortedList.Sort();
                    foreach (var i in intList)
                    {
                        tree.Add(i,i);
                        tree.Validate();
                    }
                    string message = "[" + string.Join(",", intList.Select(i => i.ToString()).ToArray()) + "]";
                    Assert.IsNull(tree.Lower(int.MinValue), message);
                    Assert.IsTrue(sortedList.SequenceEqual(tree.Keys.Cast<int>()), message);
                    var indexes = tree.Select(node => node.Index).ToArray();
                    Assert.IsTrue(Enumerable.Range(0, size).SequenceEqual(indexes), message);
                    for (int listIndex = 0; listIndex < sortedList.Count; listIndex++)
                    {
                        Assert.AreEqual(sortedList[listIndex], tree[listIndex].Key);
                        var lowerNode = tree.Lower(sortedList[listIndex]);
                        if (listIndex == 0)
                        {
                            Assert.IsNull(lowerNode);
                        }
                        else
                        {
                            Assert.AreEqual(listIndex - 1, lowerNode.Index, listIndex + message);
                        }
                        var floorNode = tree.Floor(sortedList[listIndex]);
                        Assert.AreEqual(listIndex, floorNode.Index, listIndex + message);
                    }
                    var enumerator = tree.GetEnumerator();
                    var backwardEnumerator = tree.GetEnumerator();
                    backwardEnumerator.Forward = false;
                    var list = new List<int>();
                    var backwardList = new List<int>();
                    while (enumerator.MoveNext())
                    {
                        list.Add((int) enumerator.Current.Key);
                        backwardEnumerator.MoveNext();
                        backwardList.Add((int) backwardEnumerator.Current.Key);
                    }
                    Assert.IsTrue(sortedList.SequenceEqual(list), message);
                    list.Reverse();
                    Assert.IsTrue(list.SequenceEqual(backwardList));
                    while (sortedList.Count > 0)
                    {
                        int index = random.Next(sortedList.Count);
                        var oldTree = new RedBlackTree(tree);
                        tree.RemoveKey(sortedList[index]);
                        try
                        {
                            tree.Validate();
                        }
                        catch(Exception x)
                        {
                            Console.Out.WriteLine(x);
                            tree = new RedBlackTree(oldTree);
                            Assert.Fail(x.ToString());
                        }
                        sortedList.RemoveAt(index);
//                        string msg = "size:" + size + " " + string.Join(",", sortedList.Select(i => i.ToString()).ToArray())
//                                         + "<>" + string.Join(",", tree.Keys.Select(k => k.ToString()).ToArray());
                        Assert.IsTrue(sortedList.SequenceEqual(tree.Keys.Cast<int>()));
                    }
                }
            }
        }
    }
}
