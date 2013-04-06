/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Reflection;
using System.Collections.Generic;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Find;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests for the BookmarkEnumerator class.
    /// </summary>
    [TestClass]
    public class BookmarkEnumeratorTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestForwardBackward()
        {
            var assembly = Assembly.GetAssembly(typeof (BookmarkEnumeratorTest));
            var stream = assembly.GetManifestResourceStream(
                typeof (BookmarkEnumeratorTest).Namespace + ".BookmarkEnumeratorTest.sky");
            Assert.IsNotNull(stream);
            var document = (SrmDocument) new XmlSerializer(typeof (SrmDocument)).Deserialize(stream);
            Assert.IsNotNull(document);
            var forwardEnumerator = new BookmarkEnumerator(document);
            var backwardEnumerator = new BookmarkEnumerator(forwardEnumerator) {Forward = false};
            var forwardList = new List<Bookmark>(forwardEnumerator);
            var backwardList = new List<Bookmark>(backwardEnumerator);
            Assert.AreNotEqual(0, forwardList.Count);
            Assert.AreEqual(forwardList.Count, backwardList.Count);
            
            // The very last location is the same for both the forwards and backwards enumerators.
            backwardList.Reverse(0, backwardList.Count - 1);
            CollectionAssert.AreEqual(forwardList, backwardList);

            var forwardSet = new HashSet<Bookmark>(forwardList);
            Assert.AreEqual(forwardList.Count, forwardSet.Count);
            forwardSet.UnionWith(backwardList);
            Assert.AreEqual(forwardList.Count, forwardSet.Count);
        }
    }
}
