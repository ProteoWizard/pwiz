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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class IntegerListTest : AbstractUnitTest
    {
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
    }
}
