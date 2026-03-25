/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
    public class BigListTest : AbstractUnitTest
    {
        //[TestMethod]
        public void TestToBigList()
        {
            var bigList = Enumerable.Range(0, int.MaxValue).ToBigList(20_000);
            Assert.AreEqual(int.MaxValue, bigList.Count);
            Assert.AreEqual(bigList.Count, bigList.Count());
        }

        //[TestMethod]
        public void TestToBigListBigChunkSize()
        {
            var bigList = Enumerable.Range(0, int.MaxValue).ToBigList(536_000_000);
            Assert.AreEqual(int.MaxValue, bigList.Count);
            Assert.AreEqual(bigList.Count, bigList.Count());
        }

        [TestMethod]
        public void TestSortBigList()
        {
            int count = 100_000;
            int chunkSize = 10_000;
            var random = new Random(0);
            int maxValue = 1_000;
            var bigList = Enumerable.Range(0, count).Select(i=>random.Next(maxValue)).ToBigList(chunkSize);
            var expectedCounts = GetCounts(bigList);
            var sorted = bigList.Sort(Comparer<int>.Default);
            var sortedCounts = GetCounts(sorted);
            AssertEx.AreEqual<int, int>(expectedCounts, sortedCounts);
            int? previous = null;
            foreach (var value in sorted)
            {
                if (value < previous)
                {
                    Assert.Fail("{0} should not be less than {1}", value, previous);
                }

                previous = value;
            }
        }

        private Dictionary<T, int> GetCounts<T>(IEnumerable<T> values)
        {
            var counts = new Dictionary<T, int>();
            foreach (var value in values)
            {
                counts.TryGetValue(value, out int count);
                count++;
                counts[value] = count;
            }

            return counts;
        }
    }
}
