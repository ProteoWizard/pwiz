/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.SkylineTestUtil;

namespace CommonTest.DataBinding
{
    /// <summary>
    /// Summary description for CollectionInfoTest
    /// </summary>
    [TestClass]
    public class CollectionInfoTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestDataBindingArray()
        {
            var collectionInfo = CollectionInfo.ForType(typeof (string[]));
            var strings = new[] {"One", "Two", "Three"};
            CollectionAssert.AreEqual(Enumerable.Range(0, 3).ToArray(), collectionInfo.GetKeys(strings).Cast<int>().ToArray());
            CollectionAssert.AreEqual(strings, collectionInfo.GetItems(strings).Cast<string>().ToArray());
            foreach (var key in collectionInfo.GetKeys(strings))
            {
                Assert.AreEqual(strings[(int)key], collectionInfo.GetItemFromKey(strings, key));
            }
            Assert.IsNull(collectionInfo.GetItemFromKey(strings, -1));
            Assert.IsNull(collectionInfo.GetItemFromKey(strings, 3));
            Assert.IsNull(collectionInfo.GetItemFromKey(null, 0));
        }

        [TestMethod]
        public void TestDataBindingDictionary()
        {
            var dict = new Dictionary<string, double>
                           {
                               {"zero", 3},
                               {"one", 3.1},
                               {"two", 3.14}
                           };
            var collectionInfo = CollectionInfo.ForType(dict.GetType());
            CollectionAssert.AreEqual(dict.Keys, collectionInfo.GetKeys(dict).Cast<string>().ToArray());
            CollectionAssert.AreEqual(dict, collectionInfo.GetItems(dict).Cast<KeyValuePair<string,double>>().ToArray());
            foreach (var key in collectionInfo.GetKeys(dict))
            {
                var kvp = (KeyValuePair<string, double>) collectionInfo.GetItemFromKey(dict, key);
                Assert.AreEqual(key, kvp.Key);
                Assert.AreEqual(dict[(string)key], kvp.Value);
            }
        }
    }
}
