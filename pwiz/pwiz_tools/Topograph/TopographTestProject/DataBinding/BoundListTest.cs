using System;
using System.ComponentModel;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;

namespace pwiz.Topograph.Test.DataBinding
{
    /// <summary>
    /// Summary description for BoundListTest
    /// </summary>
    [TestClass]
    public class BoundListTest
    {
        [TestMethod]
        public void TestInsertOnly()
        {
            var random = new Random(0);
            var target = new List<int>();
            var boundList = new BoundList<int>(new int[0]);
            var indexes = Enumerable.Range(1, 10).Select(i => random.Next(i)).ToArray();
            foreach (int index in indexes)
            {
                int value = target.Count;
                target.Insert(index, value);
                boundList.Insert(index, value);
                CollectionAssert.AreEqual(target, boundList.ToArray());
            }
        }
        [TestMethod]
        public void TestRemoveOnly()
        {
            var random = new Random(0);
            var target = Enumerable.Range(0, 10).ToList();
            var boundList = new BoundList<int>(target.ToArray());
            var indexes = target.Select(i => random.Next(10 - i)).ToArray();
            foreach (int index in indexes)
            {
                target.RemoveAt(index);
                boundList.RemoveAt(index);
            }
        }

        static Func<IList<T>, string> InsertAction<T>(int index, T value)
        {
            return list =>
                       {
                           list.Insert(index, value);
                           return string.Format("Insert {0} at {1}", index, value);
                       };
        }
        static Func<IList<T>, string> RemoveAction<T>(int index)
        {
            return list =>
                       {
                           list.RemoveAt(index);
                           return string.Format("Remove at {0}", index);
                       };
        }
        static Func<IList<T>, string> ReplaceAction<T>(int index, T value)
        {
            return list =>
                       {
                           list[index] = value;
                           return string.Format("Replace at {0} with {1}", index, value);
                       };
        }

        [TestMethod]
        public void TestBoundList()
        {
            foreach (int seed in new[] { 0, Environment.TickCount })
            {
                var random = new Random(seed);
                var actions = new List<Func<IList<int>, string>>();
                int listSize = 0;
                while (actions.Count < 100)
                {
                    int iAction = random.Next(3);
                    if (listSize == 0 || iAction == 0)
                    {
                        actions.Add(InsertAction(random.Next(listSize + 1), actions.Count));
                        listSize++;
                    }
                    else if (iAction == 1)
                    {
                        actions.Add(RemoveAction<int>(random.Next(listSize)));
                        listSize--;
                    }
                    else
                    {
                        actions.Add(ReplaceAction(random.Next(listSize), actions.Count));
                    }
                }

                for (int listIndexMin = 0; listIndexMin < actions.Count(); listIndexMin++)
                {
                    var targetList = new List<int>();
                    var boundLists = new List<BoundList<int>>();
                    var appliedActions = new List<string>();
                    for (int i = 0; i < actions.Count; i++)
                    {
                        boundLists.Add(new BoundList<int>(ImmutableList.ValueOf(targetList)));
                        var action = actions[i];
                        appliedActions.Add(action(targetList));
                        foreach (var boundList in boundLists)
                        {
                            action(boundList);
                            //                                while (true)
                            //                                {
                            //                                    try
                            //                                    {
                            //                                        var clone = boundList.Clone();
                            //                                        action(clone);
                            //                                        clone.Validate();
                            //                                        break;
                            //                                    }
                            //                                    catch (Exception exception)
                            //                                    {
                            //                                        Console.Out.WriteLine(exception);
                            //                                    }
                            //                                }
                            int index = 0;
                            foreach (var item in boundList)
                            {
                                if (!Equals(targetList[index], item))
                                {
                                    Assert.AreEqual(targetList[index], item);
                                }
                                if (!Equals(targetList[index], boundList[index]))
                                {
                                    Assert.AreEqual(targetList[index], boundList[index]);
                                }
                                index++;
                            }
                            CollectionAssert.AreEqual(targetList, boundList.ToArray());

                            foreach (var deletion in boundList.ItemDeletions)
                            {
                                Assert.AreEqual(ListChangedType.ItemDeleted, deletion.ListChangedType);
                                Assert.AreEqual(boundList.OriginalList[deletion.OldIndex], deletion.OldItem);
                                Assert.AreEqual(-1, deletion.NewIndex);
                                Assert.AreEqual(0, deletion.NewItem);
                            }
                            foreach (var addition in boundList.ItemAdditions)
                            {
                                Assert.AreEqual(ListChangedType.ItemAdded, addition.ListChangedType);
                                Assert.AreEqual(-1, addition.OldIndex);
                                Assert.AreEqual(0, addition.OldItem);
                                if (!Equals(boundList[addition.NewIndex], addition.NewItem))
                                {
                                    Assert.AreEqual(boundList[addition.NewIndex], addition.NewItem);
                                }
                            }
                            foreach (var itemChange in boundList.ItemChanges)
                            {
                                Assert.AreEqual(ListChangedType.ItemChanged, itemChange.ListChangedType);
                                Assert.AreEqual(boundList.OriginalList[itemChange.OldIndex], itemChange.OldItem);
                                if (!Equals(boundList[itemChange.NewIndex], itemChange.NewItem))
                                {
                                    Assert.AreEqual(boundList[itemChange.NewIndex], itemChange.NewItem);
                                }
                            }
                            Assert.AreEqual(boundList.Count,
                                            boundList.OriginalList.Count - boundList.ItemDeletions.Count() +
                                            boundList.ItemAdditions.Count());

                        }

                    }
                }
            }
        }
    }
}
