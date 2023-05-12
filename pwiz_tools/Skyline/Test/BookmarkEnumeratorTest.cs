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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Results;
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
            var document = ReadDocument("BookmarkEnumeratorTest.sky");
            Assert.IsNotNull(document);
            var forwardEnumerator = new BookmarkEnumerator(document);
            var backwardEnumerator = new BookmarkEnumerator(new BookmarkStartPosition(document, Bookmark.ROOT, false));
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
            VerifyDocument(document);
        }

        [TestMethod]
        public void TestBookmarkEnumeratorWithOptSteps()
        {
            var document = ReadDocument("BookmarkEnumeratorTest2.sky");
            VerifyDocument(document);
        }

        public void VerifyDocument(SrmDocument document)
        {
            var expectedBookmarks = EnumerateBookmarks(new BookmarkStartPosition(document)).ToList();
            VerifyElementCounts(document, expectedBookmarks);
            int bookmarkCount = expectedBookmarks.Count;
            for (int iBookmark = 0; iBookmark < expectedBookmarks.Count; iBookmark++)
            {
                var forward = new BookmarkStartPosition(document, expectedBookmarks[iBookmark], true);
                var forwardBookmarks = EnumerateBookmarks(forward).ToList();
                VerifyCompareBookmarks(forward, forwardBookmarks);
                int rotationIndex = bookmarkCount - iBookmark - 1;
                var rotatedBookmarks = forwardBookmarks.Skip(rotationIndex)
                    .Concat(forwardBookmarks.Take(rotationIndex)).ToList();
                AssertBookmarkListEqual(expectedBookmarks, rotatedBookmarks);
                var backward = new BookmarkStartPosition(document, expectedBookmarks[iBookmark], false);
                var backwardBookmarks = EnumerateBookmarks(backward).ToList();
                VerifyCompareBookmarks(backward, backwardBookmarks);
                var backwardBookmarksReversed = backwardBookmarks.Take(backwardBookmarks.Count - 1).Reverse()
                    .Append(backwardBookmarks.Last()).ToList();
                AssertBookmarkListEqual(forwardBookmarks, backwardBookmarksReversed);
            }
        }

        public void VerifyCompareBookmarks(BookmarkStartPosition startPosition, IList<Bookmark> bookmarks)
        {
            // Ensure that each bookmark compares greater than its predecessor
            for (int i = 1; i < bookmarks.Count; i++)
            {
                Assert.AreEqual(1, startPosition.Compare(bookmarks[i], bookmarks[i - 1]));
            }
            // Verify that any two bookmarks in the list compare as expected
            // but only look at a subset of them because otherwise it takes too long
            for (int i = 0; i < bookmarks.Count; i += 17)
            {
                for (int j = 0; j < bookmarks.Count; j += 31)
                {
                    int actual = Math.Sign(startPosition.Compare(bookmarks[i], bookmarks[j]));
                    int expected = Math.Sign(i.CompareTo(j));
                    if (expected != actual)
                    {
                        Assert.AreEqual(expected, actual,
                            "Unexpected result at indexes {0} and {1} comparing {2} and {3}", i, j, bookmarks[i],
                            bookmarks[j]);
                    }
                }
            }
        }

        public void AssertBookmarkListEqual(IList<Bookmark> expectedList, IList<Bookmark> actualList)
        {
            AssertEx.AreEqual(expectedList.Count, actualList.Count);
            for (int i = 0; i < expectedList.Count; i++)
            {
                var expected = expectedList[i];
                var actual = actualList[i];
                if (!Equals(expected, actual))
                {
                    Assert.AreEqual(expectedList[i], actualList[i], "Mismatch at position {0}", i);
                }
            }
        }

        public IEnumerable<Bookmark> EnumerateBookmarks(BookmarkStartPosition startPosition)
        {
            var hashSet = new HashSet<Bookmark>();
            var bookmarkEnumerator = new BookmarkEnumerator(startPosition);
            do
            {
                bookmarkEnumerator.MoveNext();
                var bookmark = bookmarkEnumerator.Current;
                if (!hashSet.Add(bookmark))
                {
                    Assert.Fail("Duplicate bookmark {0}", bookmark);
                }
                yield return bookmark;
            } while (!bookmarkEnumerator.AtStart);
        }

        public void VerifyElementCounts(SrmDocument document, IList<Bookmark> bookmarks)
        {
            var expectedElementCounts = GetElementCounts(document);
            var actualElementCounts = new Dictionary<Type, int>();
            foreach (var bookmark in bookmarks)
            {
                Increment(actualElementCounts, GetElementType(bookmark), 1);
            }

            foreach (var expectedKvp in expectedElementCounts)
            {
                actualElementCounts.TryGetValue(expectedKvp.Key, out int actualCount);
                Assert.AreEqual(expectedKvp.Value, actualCount, "Mismatch on type: {0}", expectedKvp.Key);
            }
            Assert.AreEqual(expectedElementCounts.Count, actualElementCounts.Count);
        }

        private static void Increment<TKey>(Dictionary<TKey, int> dictionary, TKey key, int difference)
        {
            dictionary.TryGetValue(key, out int count);
            dictionary[key] = count + difference;
        }

        public Dictionary<Type, int> GetElementCounts(SrmDocument document)
        {
            var elementCounts = new Dictionary<Type, int>();
            elementCounts[typeof(SrmDocument)] = 1;
            elementCounts[typeof(PeptideGroupDocNode)] = document.MoleculeGroupCount;
            elementCounts[typeof(PeptideDocNode)] = document.MoleculeCount;
            elementCounts[typeof(TransitionGroupDocNode)] = document.MoleculeTransitionGroupCount;
            elementCounts[typeof(TransitionDocNode)]= document.MoleculeTransitionCount;
            foreach (var molecule in document.Molecules)
            {
                if (molecule.HasResults)
                {
                    Increment(elementCounts,typeof(PeptideChromInfo), molecule.Results.Sum(result=>result.Count));
                }

                foreach (var transitionGroup in molecule.TransitionGroups)
                {
                    if (transitionGroup.HasResults)
                    {
                        Increment(elementCounts, typeof(TransitionGroupChromInfo), transitionGroup.Results.Sum(result=>result.Count));
                    }

                    foreach (var transition in transitionGroup.Transitions)
                    {
                        if (transition.HasResults)
                        {
                            Increment(elementCounts, typeof(TransitionChromInfo), transition.Results.Sum(result=>result.Count));
                        }
                    }
                }
            }
            return elementCounts;
        }

        private static SrmDocument ReadDocument(string resourceName)
        {
            var type = typeof(BookmarkEnumeratorTest);
            using (var stream = type.Assembly.GetManifestResourceStream(type, resourceName))
            {
                Assert.IsNotNull(stream);
                return (SrmDocument)new XmlSerializer(typeof(SrmDocument)).Deserialize(stream);
            }
        }

        private static Type GetElementType(Bookmark bookmark)
        {
            if (bookmark.ReplicateIndex.HasValue)
            {
                Assert.IsNotNull(bookmark.ChromFileInfoId);
                switch (bookmark.IdentityPath.Length)
                {
                    case 4:
                        return typeof(TransitionChromInfo);
                    case 3:
                        return typeof(TransitionGroupChromInfo);
                    case 2:
                        return typeof(PeptideChromInfo);
                }
                Assert.Fail("Unexpected ReplicateIndex for {0}", bookmark);
            }

            switch (bookmark.IdentityPath.Length)
            {
                case 4:
                    return typeof(TransitionDocNode);
                case 3:
                    return typeof(TransitionGroupDocNode);
                case 2:
                    return typeof(PeptideDocNode);
                case 1:
                    return typeof(PeptideGroupDocNode);
                case 0:
                    return typeof(SrmDocument);
            }
            Assert.Fail("Unrecognized bookmark {0}", bookmark);
            return null;
        }
    }
}
