/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results
{
    public static class IndexedMultiArray
    {
        /// <summary>
        /// Groups values which are keyed by index. Values with the same index stay in the order
        /// they were supplied in.
        /// </summary>
        public static IndexedMultiArray<T> ToIndexedMultiArray<T>(this IEnumerable<KeyValuePair<int, T>> valuesByIndex)
        {
            var valueLists = new List<List<T>>();
            foreach (var entry in valuesByIndex)
            {
                while (valueLists.Count <= entry.Key)
                {
                    valueLists.Add(null);
                }

                var valueList = valueLists[entry.Key];
                if (valueList == null)
                {
                    valueLists[entry.Key] = valueList = new List<T>();
                }

                valueList.Add(entry.Value);
            }

            if (valueLists.Count == 0)
            {
                return IndexedMultiArray<T>.EMPTY;
            }

            return IndexedMultiArray<T>.FromCounts(valueLists.Select(valueList => valueList?.Count ?? 0),
                valueLists.Where(valueList => valueList != null).SelectMany(valueList => valueList).ToArray());
        }

        /// <summary>
        /// Takes the values of each index in order, which is the shape that indexing an
        /// <see cref="IndexedMultiArray{T}"/> gives back.
        /// </summary>
        public static IndexedMultiArray<T> ToIndexedMultiArray<T>(this IEnumerable<IEnumerable<T>> valuesByIndex)
        {
            var flatValues = new List<T>();
            var counts = new List<int>();
            foreach (var values in valuesByIndex)
            {
                int prevCount = flatValues.Count;
                flatValues.AddRange(values);
                counts.Add(flatValues.Count - prevCount);
            }

            return IndexedMultiArray<T>.FromCounts(counts, flatValues.ToArray());
        }
    }

    /// <summary>
    /// A list of values for each of a range of indexes starting at zero, laid out by the positions
    /// of a <see cref="Results.ReplicatePositions"/>: the values are held in one flat list, and
    /// indexing gives the range of that list which belongs to one index.
    /// <para>
    /// This costs two objects rather than one per index, which is what an array per index costs.
    /// The tradeoff is that the space used is proportional to the highest index which has any
    /// values, whether or not the lower indexes have any, so the indexes have to be small numbers.
    /// </para>
    /// </summary>
    public class IndexedMultiArray<T> : IReadOnlyList<IList<T>>
    {
        public static readonly IndexedMultiArray<T> EMPTY
            = new IndexedMultiArray<T>(ReplicatePositions.FromCounts(Array.Empty<int>()), ImmutableList<T>.EMPTY);

        /// <summary>
        /// Constructs from the number of values at each index, and all of the values in index
        /// order, which is the shape that <see cref="GetCount"/> and <see cref="FlatValues"/>
        /// return.
        /// </summary>
        public static IndexedMultiArray<T> FromCounts(IEnumerable<int> counts, T[] values)
        {
            var replicatePositions = ReplicatePositions.FromCounts(counts);
            if (replicatePositions.TotalCount == 0)
            {
                return EMPTY;
            }

            return new IndexedMultiArray<T>(replicatePositions, ImmutableList<T>.ValueOf(values, true));
        }

        private IndexedMultiArray(ReplicatePositions replicatePositions, ImmutableList<T> flatValues)
        {
            ReplicatePositions = replicatePositions;
            FlatValues = flatValues;
        }

        public ReplicatePositions ReplicatePositions { get; }

        /// <summary>
        /// All of the values, ordered by their index.
        /// </summary>
        public ImmutableList<T> FlatValues { get; }

        /// <summary>
        /// How many indexes, which is what this is a list of, and one more than the highest index
        /// which has any values. The number of values is <see cref="ImmutableList{T}.Count"/> on
        /// <see cref="FlatValues"/>.
        /// </summary>
        public int Count
        {
            get { return ReplicatePositions.ReplicateCount; }
        }

        public bool IsEmpty
        {
            get { return FlatValues.Count == 0; }
        }

        /// <summary>
        /// The values at an index, which is empty when it has none. Indexes which are out of range
        /// are treated as having no values.
        /// </summary>
        public IList<T> this[int index]
        {
            get
            {
                int count = ReplicatePositions.GetCount(index);
                if (count == 0)
                {
                    return Array.Empty<T>();
                }

                int start = ReplicatePositions.GetStart(index);
                return ReadOnlyList.Create(count, i => FlatValues[start + i]);
            }
        }

        public int GetCount(int index)
        {
            return ReplicatePositions.GetCount(index);
        }

        /// <summary>
        /// Returns an equivalent instance which shares its <see cref="Results.ReplicatePositions"/>
        /// with the others in the cache. Instances which hold values at the same indexes have equal
        /// positions, so on a library where most spectra were found in the same files this leaves
        /// one positions object instead of one per spectrum.
        /// </summary>
        public IndexedMultiArray<T> ValueFromCache(ValueCache valueCache)
        {
            var replicatePositions = valueCache.CacheValue(ReplicatePositions);
            if (ReferenceEquals(replicatePositions, ReplicatePositions))
            {
                return this;
            }

            return new IndexedMultiArray<T>(replicatePositions, FlatValues);
        }

        /// <summary>
        /// Returns the values of both, with the values of <paramref name="other"/> after this
        /// one's at each index.
        /// </summary>
        public IndexedMultiArray<T> Merge(IndexedMultiArray<T> other)
        {
            int indexCount = Math.Max(Count, other.Count);
            return Enumerable.Range(0, indexCount).Select(index => this[index].Concat(other[index]))
                .ToIndexedMultiArray();
        }

        public IEnumerator<IList<T>> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(index => this[index]).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
