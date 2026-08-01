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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// The ways a <see cref="ChromFileIdMap{T}"/> is rearranged: setting a value for a file which
    /// is there and one which is not, removing one, and dropping the entries which say nothing.
    /// Each of these has to keep the files and the values lined up, and has to leave the other
    /// replicates alone.
    /// </summary>
    [TestClass]
    public class ChromFileIdMapTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestChromFileIdMapSet()
        {
            var fileIds = MakeFileIds(4);
            // Two replicates: the first with two files, the second with one.
            var map = MakeMap(new[] { 2, 1 }, new[] { fileIds[0], fileIds[1], fileIds[2] },
                new[] { 10, 11, 12 });

            // A file the replicate already has is replaced in place.
            var replaced = map.Set(0, fileIds[1], 99);
            CollectionAssert.AreEqual(new[] { 10, 99, 12 }, replaced.FlatValues.ToArray());
            Assert.AreEqual(map.ChromFileIds, replaced.ChromFileIds, @"layout should not change");

            // Setting what is already there gives the same instance back.
            Assert.AreSame(replaced, replaced.Set(0, fileIds[1], 99));

            // A file the replicate does not have is added at the end of that replicate, and the
            // other replicate keeps its own entry.
            var added = map.Set(0, fileIds[3], 77);
            CollectionAssert.AreEqual(new[] { 10, 11, 77, 12 }, added.FlatValues.ToArray());
            CollectionAssert.AreEqual(new[] { 10, 11, 77 }, added.Values[0].ToArray());
            CollectionAssert.AreEqual(new[] { 12 }, added.Values[1].ToArray());

            // A replicate past the end grows the map, the ones before it holding nothing.
            var grown = map.Set(3, fileIds[3], 55);
            Assert.AreEqual(4, grown.Count);
            Assert.AreEqual(0, grown.Values[2].Count);
            CollectionAssert.AreEqual(new[] { 55 }, grown.Values[3].ToArray());
            CollectionAssert.AreEqual(new[] { 10, 11, 12, 55 }, grown.FlatValues.ToArray());
        }

        [TestMethod]
        public void TestChromFileIdMapRemove()
        {
            var fileIds = MakeFileIds(4);
            var map = MakeMap(new[] { 2, 1 }, new[] { fileIds[0], fileIds[1], fileIds[2] },
                new[] { 10, 11, 12 });

            var removed = map.Remove(0, fileIds[0]);
            CollectionAssert.AreEqual(new[] { 11, 12 }, removed.FlatValues.ToArray());
            CollectionAssert.AreEqual(new[] { 11 }, removed.Values[0].ToArray());
            // The replicate which was not touched still has its own file and value.
            CollectionAssert.AreEqual(new[] { 12 }, removed.Values[1].ToArray());
            CollectionAssert.AreEqual(new[] { fileIds[2] }, removed.Keys[1].ToArray());

            // A file the replicate does not have, and the same file in the wrong replicate, both
            // leave the map alone.
            Assert.AreSame(map, map.Remove(0, fileIds[3]));
            Assert.AreSame(map, map.Remove(1, fileIds[0]));

            // Removing the last entry leaves nothing, which is stored as no map at all.
            var single = MakeMap(new[] { 1 }, new[] { fileIds[0] }, new[] { 10 });
            Assert.IsNull(single.Remove(0, fileIds[0]));
        }

        [TestMethod]
        public void TestChromFileIdMapWithoutDefault()
        {
            var fileIds = MakeFileIds(4);
            var map = MakeMap(new[] { 2, 2 }, new[] { fileIds[0], fileIds[1], fileIds[2], fileIds[3] },
                new[] { 0, 11, 0, 13 });

            var stripped = map.WithoutDefault();
            CollectionAssert.AreEqual(new[] { 11, 13 }, stripped.FlatValues.ToArray());
            CollectionAssert.AreEqual(new[] { fileIds[1] }, stripped.Keys[0].ToArray());
            CollectionAssert.AreEqual(new[] { fileIds[3] }, stripped.Keys[1].ToArray());

            // Nothing to strip gives the same instance back.
            Assert.AreSame(stripped, stripped.WithoutDefault());

            // The default is a parameter because it is not always the zero one.
            Assert.IsNull(MakeMap(new[] { 2 }, new[] { fileIds[0], fileIds[1] }, new[] { 7, 7 })
                .WithoutDefault(7));
        }

        [TestMethod]
        public void TestChromFileIdMapNormalize()
        {
            var fileIds = MakeFileIds(3);

            // The replicates at the end which have nothing go, and the values are untouched.
            var trailing = MakeMap(new[] { 1, 0, 1, 0, 0 }, new[] { fileIds[0], fileIds[1] }, new[] { 10, 11 });
            var normalized = trailing.Normalize();
            Assert.AreEqual(3, normalized.Count);
            CollectionAssert.AreEqual(new[] { 10, 11 }, normalized.FlatValues.ToArray());

            // A replicate in the middle with nothing stays: the ones after it are at an index which
            // depends on it being there.
            Assert.AreEqual(0, normalized.Values[1].Count);
            CollectionAssert.AreEqual(new[] { 11 }, normalized.Values[2].ToArray());

            // Nothing to drop gives the same instance back.
            Assert.AreSame(normalized, normalized.Normalize());

            // A map with no entry anywhere is nothing, however many replicates it covers.
            Assert.IsNull(MakeMap(new[] { 0, 0 }, new ChromFileInfoId[0], new int[0]).Normalize());

            // What Set leaves behind when it grows a map to reach a later replicate, and what
            // Remove leaves when it takes the last entry of the last replicate.
            var grown = MakeMap(new[] { 1 }, new[] { fileIds[0] }, new[] { 10 })
                .Set(2, fileIds[1], 12);
            Assert.AreEqual(3, grown.Count);
            Assert.AreSame(grown, grown.Normalize());
            Assert.AreEqual(1, grown.Remove(2, fileIds[1]).Normalize().Count);
        }

        private static ChromFileInfoId[] MakeFileIds(int count)
        {
            return Enumerable.Range(0, count).Select(i => new ChromFileInfoId()).ToArray();
        }

        private static ChromFileIdMap<int> MakeMap(IEnumerable<int> counts, IEnumerable<ChromFileInfoId> fileIds,
            IEnumerable<int> values)
        {
            return new ChromFileIdMap<int>(new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds), values);
        }
    }
}
