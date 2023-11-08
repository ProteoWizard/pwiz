/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ReferenceValueTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestReferenceValueDictionary()
        {
            var dictionary = new Dictionary<ReferenceValue<string>, int>();
            var hello1 = new string("hello".ToCharArray());
            var hello2 = new string("hello".ToCharArray());
            var hello3 = new string("hello".ToCharArray());
            Assert.AreEqual(hello1, hello2);
            Assert.AreNotSame(hello1, hello2);
            Assert.AreEqual(hello1, hello3);
            Assert.AreNotSame(hello1, hello3);
            Assert.AreNotSame(hello2, hello3);
            dictionary.Add(hello1, 1);
            dictionary.Add(hello2, 2);
            Assert.IsTrue(dictionary.TryGetValue(hello1, out var hello1Value));
            Assert.AreEqual(1, hello1Value);
            Assert.IsTrue(dictionary.TryGetValue(hello2, out var hello2Value));
            Assert.AreEqual(2, hello2Value);
            Assert.IsFalse(dictionary.TryGetValue(hello3, out _));
        }
    }
}
