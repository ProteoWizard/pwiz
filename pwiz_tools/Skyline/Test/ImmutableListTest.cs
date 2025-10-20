/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ImmutableListTest : AbstractUnitTest
    {
        /// <summary>
        /// Tests <see cref="IntegerList.FromIntegers"/>
        /// </summary>
        [TestMethod]
        public void TestIntegerListFromIntegers()
        {
            var bits = IntegerListFromIntegers(Enumerable.Repeat(0, 100).Concat(Enumerable.Repeat(1, 100)));
            Assert.AreEqual("Bits", bits.GetType().Name);
            var constantList = IntegerListFromIntegers(Enumerable.Repeat(1000, 100));
            Assert.IsInstanceOfType(constantList, typeof(ConstantList<int>));
            var bytes = IntegerListFromIntegers(Enumerable.Range(0, 255));
            Assert.AreEqual("Bytes", bytes.GetType().Name);
            var shorts = IntegerListFromIntegers(Enumerable.Range(0, 512));
            Assert.AreEqual("Shorts", shorts.GetType().Name);
            IntegerListFromIntegers(Enumerable.Range(99999, 100));
        }

        private ImmutableList<int> IntegerListFromIntegers(IEnumerable<int> values)
        {
            var list = values.ToList();
            VerifyEquivalentLists(list);
            var intList = IntegerList.FromIntegers(list);
            CollectionAssert.AreEqual(list, intList.ToList());
            if (list.Count == 0)
            {
                Assert.AreSame(ImmutableList.Empty<int>(), intList);
            }

            if (list.Count > 1 && list.Distinct().Count() == 1)
            {
                Assert.IsInstanceOfType(intList, typeof(ConstantList<int>));
            }
            VerifyEquivalentLists(intList);
            return intList;
        }

        [TestMethod]
        public void TestFactorList()
        {
            var list = Enumerable.Repeat("apple", 100).Concat(Enumerable.Repeat("orange", 50)).ToList();
            var factorList = ToFactorList(list);
            Assert.AreEqual(2, factorList.Levels.Count);
            // The factor levels should be in the same order as they were encountered in the list
            Assert.AreEqual("apple", factorList.Levels[0]);
            Assert.AreEqual("orange", factorList.Levels[1]);
            
            var otherFactorList = ToFactorList(list.AsEnumerable().Reverse());
            CollectionAssert.AreEqual(factorList.Levels.ToList(), otherFactorList.Levels.Reverse().ToList());
        }

        private Factor<T> ToFactorList<T>(IEnumerable<T> items)
        {
            var list = items.ToList();
            VerifyEquivalentLists(list);
            var factorList = list.ToFactor();
            CollectionAssert.AreEqual(list, factorList.ToList());
            CollectionAssert.AreEqual(factorList.Levels.ToList(), factorList.Distinct().ToList());
            VerifyEquivalentLists(factorList);
            return factorList;
        }

        /// <summary>
        /// Tests <see cref="ImmutableListFactory.ToImmutable{T}"/>
        /// </summary>
        [TestMethod]
        public void TestToImmutable()
        {
            Assert.AreSame(ImmutableList.Empty<object>(), ImmutableList.ValueOf(Array.Empty<object>()));
            
            var oneFruitList = ImmutableList.Singleton("apple");
            VerifyEquivalentLists(oneFruitList);
            var oneFruitOtherList = new[] { "apple" }.ToImmutable();
            Assert.AreEqual(oneFruitList, oneFruitOtherList);
            Assert.AreEqual(oneFruitList.GetType(), oneFruitOtherList.GetType());
            Assert.AreSame(oneFruitList, oneFruitList.MaybeConstant());

            var twoFruits = new[] { "apple", "orange" };
            VerifyEquivalentLists(twoFruits);
            var twoFruitList = ImmutableList.ValueOf(twoFruits);
            var twoFruitOtherList = twoFruits.ToImmutable();
            Assert.AreEqual(twoFruitList.GetType(), twoFruitOtherList.GetType());
            Assert.AreNotEqual(oneFruitList.GetType(), twoFruitList.GetType());
        }


        /// <summary>
        /// Tests <see cref="ImmutableListFactory.MaybeConstant{T}"/>
        /// </summary>
        [TestMethod]
        public void TestMaybeConstant()
        {
            var singleton = ImmutableList.Singleton("apple");
            var oneElementConstant = ImmutableList.ValueOf(new[]{"apple"});
            Assert.AreEqual(singleton, oneElementConstant);
            Assert.AreEqual(singleton.GetType(), oneElementConstant.GetType());
            var twoElementList = new[] { "apple", "apple" }.ToImmutable();
            Assert.AreNotEqual(twoElementList.GetType(), singleton.GetType());
            var twoElementConstant = twoElementList.MaybeConstant();
            Assert.IsInstanceOfType(twoElementConstant, typeof(ConstantList<string>));
            Assert.AreNotEqual(twoElementConstant.GetType(), singleton.GetType());

            var twoDifferentElements = new[] { "apple", "orange" }.ToImmutable();
            var twoDifferentElementsMaybeConstant = twoDifferentElements.MaybeConstant();
            Assert.AreSame(twoDifferentElements, twoDifferentElementsMaybeConstant);
        }

        /// <summary>
        /// Tests <see cref="NullableList{T}"/>>
        /// </summary>
        [TestMethod]
        public void TestNullableList()
        {
            var values = new float?[] { 1, 2, null, 3 };
            var nullables = values.Nullables();
            CollectionAssert.AreEqual(values, nullables.ToList());
            VerifyEquivalentLists(values);
        }

        [TestMethod]
        public void TestConstantList()
        {
            VerifyConstantList("hello");
            VerifyConstantList(1.0);
            VerifyConstantList(1);
            VerifyConstantList(default(float?));
            VerifyConstantList((float?) 1.0);
        }

        [SuppressMessage("ReSharper", "CollectionNeverUpdated.Local")]
        private void VerifyConstantList<T>(T value)
        {
            var emptyList = new ConstantList<T>(0, value);
            Assert.AreEqual(ImmutableList<T>.EMPTY, emptyList);
            VerifyEquivalentLists(emptyList);

            var singletonList = new ConstantList<T>(1, value);
            Assert.AreEqual(ImmutableList<T>.Singleton(value), singletonList);
            VerifyEquivalentLists(singletonList);

            var twoItemList = new ConstantList<T>(2, value);
            CollectionAssert.AreEqual(Enumerable.Repeat(value, 2).ToList(), twoItemList.ToList());
            VerifyEquivalentLists(twoItemList);

            var longList = new ConstantList<T>(1000, value);
            Assert.AreEqual(1000, longList.Count);
            VerifyEquivalentLists(longList);

            var veryLongList = new ConstantList<T>(int.MaxValue, value);
            Assert.AreEqual(int.MaxValue, veryLongList.Count);
            var veryLongList2 = new ConstantList<T>(int.MaxValue, value);
            // The veryLongList is too large to call "GetHashCode" on but it's Equals method will work
            Assert.AreEqual(veryLongList, veryLongList2);
            Assert.AreNotEqual(longList, veryLongList2);
            Assert.AreNotEqual(singletonList, veryLongList2);
        }
        

        /// <summary>
        /// Verifies that all the sorts of ImmutableList's that can be created from a list of elements
        /// are equal to each other and have the same hash code.
        /// </summary>
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        private void VerifyEquivalentLists<T>(IEnumerable<T> enumerable)
        {
            var equivalentLists = new List<ImmutableList<T>>();
            bool alreadyImmutableList;
            if (enumerable is ImmutableList<T> immutableList)
            {
                equivalentLists.Add(immutableList);
                alreadyImmutableList = true;
            }
            else
            {
                alreadyImmutableList = false;
            }

            equivalentLists.Add(ImmutableList.ValueOf(enumerable));
            equivalentLists.Add(enumerable.ToImmutable());

            int count = enumerable.Count();
            int distinctCount = enumerable.Distinct().Count();

            if (typeof(T) == typeof(int))
            {
                var integerList = (ImmutableList<T>)(object)IntegerList.FromIntegers((IEnumerable<int>)enumerable);
                equivalentLists.Add(integerList);
                if (distinctCount == 1 && count > 1)
                {
                    Assert.IsInstanceOfType(integerList, typeof(ConstantList<T>));
                }
            }

            var maybeConstantList = enumerable.ToImmutable().MaybeConstant();
            equivalentLists.Add(maybeConstantList);
            Assert.AreSame(maybeConstantList, maybeConstantList.MaybeConstant());

            if (distinctCount == 1)
            {
                if (count == 1)
                {
                    var singletonList = ImmutableList.Singleton(enumerable.First());
                    if (!alreadyImmutableList)
                    {
                        Assert.AreEqual(singletonList.GetType(), maybeConstantList.GetType());
                    }
                    equivalentLists.Add(singletonList);
                }
                else
                {
                    Assert.IsInstanceOfType(maybeConstantList, typeof(ConstantList<T>));
                }
                equivalentLists.Add(new ConstantList<T>(enumerable.Count(), enumerable.First()));
            }

            if (count == 1)
            {
                Assert.AreEqual(1, distinctCount);
                equivalentLists.Add(ImmutableList.Singleton(enumerable.First()));
            }
            
            if (count == 0)
            {
                Assert.AreEqual(0, distinctCount);
                if (!alreadyImmutableList)
                {
                    foreach (var list in equivalentLists)
                    {
                        Assert.AreSame(ImmutableList<T>.EMPTY, list);
                    }
                }
            }

            var factorList = enumerable.ToFactor();
            equivalentLists.Add(factorList);
            CollectionAssert.AreEqual(enumerable.Distinct().ToList(), factorList.Levels.ToList());
            if (distinctCount == count)
            {
                equivalentLists.Add(factorList.Levels);
            }
            equivalentLists.Add(enumerable.ToFactor().MaybeConstant());
            equivalentLists.Add(enumerable.ToImmutable().MaybeConstant());

            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var nullableListType = typeof(NullableList<>).MakeGenericType(typeof(T).GetGenericArguments());
                var enumerableType = typeof(IEnumerable<>).MakeGenericType(typeof(T));
                var constructor = nullableListType.GetConstructor(new[] { enumerableType });
                Assert.IsNotNull(constructor);
                var nullableListObject = constructor.Invoke(new[]{enumerable});
                equivalentLists.Add((ImmutableList<T>)nullableListObject);
            }

            for (int i = 0; i < equivalentLists.Count; i++)
            {
                for (int j = 0; j < equivalentLists.Count; j++)
                {
                    Assert.AreEqual(equivalentLists[i], equivalentLists[j]);
                    Assert.AreEqual(equivalentLists[i].GetHashCode(), equivalentLists[j].GetHashCode());
                }
            }
        }
    }
}
