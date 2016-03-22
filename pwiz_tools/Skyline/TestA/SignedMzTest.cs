/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Tests to verify operation of the SignedMz class
    /// </summary>
    [TestClass]
    public class SignedMzTest : AbstractUnitTest
    {
    
        /// <summary>
        /// Tests the SignedMz class.
        /// </summary>
        [TestMethod]
        public void SignedMzUnitTest()
        {
            var empty = new SignedMz(null, false);
            Assert.AreEqual(empty, SignedMz.EMPTY);
            Assert.AreEqual(((double?) null).GetHashCode(), empty.GetHashCode());
            Assert.IsFalse(empty.HasValue);
            var zero = new SignedMz(0, false);
            var zeroNeg = new SignedMz(0, true);
            Assert.AreEqual(zero, SignedMz.ZERO);
            Assert.AreNotEqual(zeroNeg, SignedMz.ZERO);
            AssertEx.ThrowsException<Exception>(() => zero.Value == empty.Value);
            var neg = new SignedMz(-9.0);  // For deserialization (note single ctor arg) - a negative value is taken to mean negative polarity
            Assert.AreEqual(9.0, neg.Value);
            Assert.IsTrue(neg.IsNegative);

            for (var loop = 0; loop < 2; loop++)
            {
                var negative = loop > 0;
                var one = new SignedMz(1, negative);
                var two = new SignedMz(2, negative);
                var three = new SignedMz(3, negative);
                var minusOne = new SignedMz(-1, negative);  // The notion of ion polarity is unaffected by the sign of the mz value, since we can do addition and subtraction on these
                Assert.AreEqual(negative, minusOne.IsNegative);
                Assert.AreEqual(negative, one.IsNegative);
                Assert.AreEqual(2, two.Value);
                Assert.AreEqual(negative ? -2 : 2, two.RawValue);
                Assert.AreEqual(negative ? 1 : -1, minusOne.RawValue);
                Assert.AreEqual(three, one + two);
                Assert.AreEqual(two, three + minusOne);
                Assert.AreEqual(one, three - two);
                Assert.AreEqual(-1.0, two - three);
                Assert.AreEqual(minusOne, two - three);
                Assert.AreEqual(one, three - 2);
                Assert.AreEqual(three, one + 2);
                Assert.IsTrue(three > one);
                Assert.IsFalse(three < one);
                Assert.IsTrue(three == one + two);
                Assert.IsFalse(three != one + two);
                var bad = new SignedMz(1, !negative);
                AssertEx.ThrowsException<InvalidOperationException>(() => one + bad);  // Mixed-polarity math is meaningless
                AssertEx.ThrowsException<InvalidOperationException>(() => one - bad);
            }
        }
    }
}