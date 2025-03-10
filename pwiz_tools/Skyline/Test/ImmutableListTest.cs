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
            var factorList = list.ToFactor();
            CollectionAssert.AreEqual(list, factorList.ToList());
            CollectionAssert.AreEqual(factorList.Levels.ToList(), factorList.Distinct().ToList());
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
            var oneFruitOtherList = new[] { "apple" }.ToImmutable();
            Assert.AreEqual(oneFruitList, oneFruitOtherList);
            Assert.AreEqual(oneFruitList.GetType(), oneFruitOtherList.GetType());
            Assert.AreSame(oneFruitList, oneFruitList.MaybeConstant());

            var twoFruits = new[] { "apple", "orange" };
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
        }
    }
}
