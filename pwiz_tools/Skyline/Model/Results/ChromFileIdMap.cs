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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// A value for each file of each replicate: a map from a <see cref="ChromFileInfoId"/> to a
    /// value, laid out by the positions of a <see cref="Results.ChromFileIds"/>. Indexing by
    /// replicate gives that replicate's entries, and <see cref="Keys"/> and <see cref="Values"/>
    /// give the files and the values on their own, both as a list per replicate.
    /// <para>
    /// A replicate and a file identify a value on their own, unless optimization steps are involved:
    /// all the steps of one file share a position, because nothing kept this way can differ between
    /// them.
    /// </para>
    /// <para>
    /// A map need not have an entry for every file of a replicate. The molecule level values are
    /// stored only where they were set, and a molecule of a multi injection replicate is usually
    /// found in one of that replicate's files rather than all of them.
    /// </para>
    /// <para>
    /// The values every peak has are one map of a struct - <see cref="PrecursorPeak"/>,
    /// <see cref="TransitionPeak"/> - so that a peak costs one object and one indirection rather
    /// than one of each per value. The values only some peaks have get a map each, so that one
    /// which says the same thing everywhere collapses to a single entry.
    /// </para>
    /// </summary>
    public class ChromFileIdMap<T> : IReadOnlyList<IEnumerable<KeyValuePair<ChromFileInfoId, T>>>
    {
        /// <summary>
        /// A map over no replicates at all, for reaching past a null with <c>??</c>. A map holding
        /// nothing is stored as null rather than as this: over N replicates it would be N zero
        /// counts, which is not the same <see cref="ReplicatePositions"/> as no replicates, so the
        /// two would be unequal while meaning the same thing.
        /// </summary>
        public static readonly ChromFileIdMap<T> Empty = new ChromFileIdMap<T>(
            new ChromFileIds(ReplicatePositions.FromCounts(new int[0]), new ChromFileInfoId[0]), new T[0]);

        /// <summary>
        /// The values are stored as they are given. A caller which is going to hold the map for a
        /// long time reduces them itself - through
        /// <see cref="ImmutableListFactory.MaybeConstant{T}"/>, say - because a map is often an
        /// intermediate value which would not repay the work.
        /// </summary>
        public ChromFileIdMap(ChromFileIds chromFileIds, IEnumerable<T> values)
        {
            ChromFileIds = chromFileIds;
            FlatValues = ImmutableList.ValueOf(values);
        }

        /// <summary>
        /// The values of one replicate per entry, which is the shape the chrom infos had.
        /// </summary>
        public ChromFileIdMap(IList<IList<T>> valuesByReplicate, IEnumerable<ChromFileInfoId> fileIds)
            : this(new ChromFileIds(ReplicatePositions.FromCounts(valuesByReplicate.Select(v => v.Count)), fileIds),
                valuesByReplicate.SelectMany(values => values))
        {
        }

        public ChromFileIds ChromFileIds { get; private set; }
        public ImmutableList<T> FlatValues { get; private set; }

        public IList<IList<ChromFileInfoId>> Keys
        {
            get
            {
                return ReadOnlyList.Create(Count, GetKeys);
            }
        }
        private IList<ChromFileInfoId> GetKeys(int replicateIndex)
        {
            int count = ChromFileIds.ReplicatePositions.GetCount(replicateIndex);
            if (count == 0)
            {
                return Array.Empty<ChromFileInfoId>();
            }
            int start = ChromFileIds.ReplicatePositions.GetStart(replicateIndex);
            return ReadOnlyList.Create(count, i => ChromFileIds.FileIds[start + i].Value);
        }


        public IList<IList<T>> Values
        {
            get
            {
                return ReadOnlyList.Create(Count, GetValues);
            }
        }

        private IList<T> GetValues(int replicateIndex)
        {
            int count = ChromFileIds.ReplicatePositions.GetCount(replicateIndex);
            if (count == 0)
            {
                return Array.Empty<T>();
            }

            int start = ChromFileIds.ReplicatePositions.GetStart(replicateIndex);
            return ReadOnlyList.Create(count, i => FlatValues[start + i]);
        }

        public ReplicatePositions ReplicatePositions
        {
            get { return ChromFileIds.ReplicatePositions; }
        }

        /// <summary>
        /// How many replicates, which is what this is a list of. The number of values is
        /// <see cref="ImmutableList{T}.Count"/> on <see cref="FlatValues"/>.
        /// </summary>
        public int Count
        {
            get { return ReplicatePositions.ReplicateCount; }
        }

        public IEnumerable<KeyValuePair<ChromFileInfoId, T>> this[int replicateIndex]
        {
            get
            {
                return ReplicatePositions[replicateIndex].Select(position =>
                    new KeyValuePair<ChromFileInfoId, T>(ChromFileIds.FileIds[position].Value, FlatValues[position]));
            }
        }

        /// <summary>
        /// The value for one file of one replicate, which is how a caller which does not already
        /// hold a position of this map's own <see cref="Results.ChromFileIds"/> asks for one.
        /// <para>
        /// There is deliberately no method taking a bare position. A position means nothing without
        /// the <see cref="Results.ChromFileIds"/> it came from, and two maps only share positions
        /// when they were built over the same one - which the maps of a single
        /// <see cref="TransitionResults"/> are, and which a precursor's and its transitions' are
        /// not. Code which does hold a position of this map reaches <see cref="FlatValues"/> directly,
        /// where indexing by position is what it plainly looks like.
        /// </para>
        /// </summary>
        public bool TryGetValue(int replicateIndex, ChromFileInfoId fileId, out T value)
        {
            int position = ChromFileIds.IndexOfFile(replicateIndex, fileId);
            if (position < 0)
            {
                value = default(T);
                return false;
            }

            value = FlatValues[position];
            return true;
        }

        public IEnumerator<IEnumerable<KeyValuePair<ChromFileInfoId, T>>> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(replicateIndex => this[replicateIndex]).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        protected bool Equals(ChromFileIdMap<T> other)
        {
            return Equals(ChromFileIds, other.ChromFileIds) && Equals(FlatValues, other.FlatValues);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((ChromFileIdMap<T>) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ChromFileIds.GetHashCode() * 397) ^ FlatValues.GetHashCode();
            }
        }

        public ChromFileIdMap<T> WithFileIds(ChromFileIds chromFileIds, T defaultValue = default)
        {
            if (Equals(ChromFileIds, chromFileIds))
            {
                return this;
            }

            var newValues = new List<T>(chromFileIds.ReplicatePositions.TotalCount);
            for (int replicateIndex = 0; replicateIndex < chromFileIds.ReplicatePositions.ReplicateCount; replicateIndex++)
            {
                foreach (var fileId in chromFileIds.GetFileIds(replicateIndex))
                {
                    if (TryGetValue(replicateIndex, fileId, out var value))
                    {
                        newValues.Add(value);
                    }
                    else
                    {
                        newValues.Add(defaultValue);
                    }
                }
            }

            return new ChromFileIdMap<T>(chromFileIds, newValues);
        }

        /// <summary>
        /// Returns a new ChromFileIdMap with the entries removed that were equal to <paramref name="defaultValue"/>,
        /// or null when that removes all of them - which is how a value nobody has set is stored.
        /// <para>
        /// Returns this when there was nothing to remove, so a map which does not change stays
        /// reference equal.
        /// </para>
        /// <para>
        /// This is what makes a map sparse: an entry earns its place by saying something. The
        /// entries which survive keep the file they belong to, so the positions of the result are
        /// not the positions of this map, and nothing may carry one across.
        /// </para>
        /// <para>
        /// The default is worth passing explicitly for an enum whose "nothing set" value is not the
        /// zero one: <see cref="Results.UserSet.FALSE"/> is one rather than zero, so leaving it out
        /// would remove the peaks a user did set and keep the rest.
        /// </para>
        /// </summary>
        public ChromFileIdMap<T> WithoutDefault(T defaultValue = default)
        {
            var comparer = EqualityComparer<T>.Default;
            if (!FlatValues.Any(value => comparer.Equals(value, defaultValue)))
            {
                return this;
            }

            var counts = new List<int>(Count);
            var fileIds = new List<ChromFileInfoId>();
            var values = new List<T>();
            for (int replicateIndex = 0; replicateIndex < Count; replicateIndex++)
            {
                int count = 0;
                foreach (int position in ReplicatePositions[replicateIndex])
                {
                    var value = FlatValues[position];
                    if (comparer.Equals(value, defaultValue))
                    {
                        continue;
                    }

                    fileIds.Add(ChromFileIds.FileIds[position].Value);
                    values.Add(value);
                    count++;
                }

                counts.Add(count);
            }

            if (values.Count == 0)
            {
                return null;
            }

            return new ChromFileIdMap<T>(new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds),
                values);
        }

        /// <summary>
        /// The map without the entry for one file of one replicate, or this when there is no such
        /// entry. Null when it was the last entry, which is how a map holding nothing is stored.
        /// </summary>
        public ChromFileIdMap<T> Remove(int replicateIndex, ChromFileInfoId fileId)
        {
            var newChromFileIds = ChromFileIds.Remove(replicateIndex, fileId);
            if (ReferenceEquals(newChromFileIds, ChromFileIds))
            {
                return this;
            }

            if (newChromFileIds.ReplicatePositions.TotalCount == 0)
            {
                return null;
            }

            // The same positions the files were kept from, so the two stay lined up.
            return new ChromFileIdMap<T>(newChromFileIds,
                ChromFileIds.PositionsWithout(replicateIndex, fileId).Select(position => FlatValues[position]));
        }

        /// <summary>
        /// Replaces or adds the entry for a particular file in a particular replicate. A file the
        /// replicate has no entry for is added at the end of its entries, and a replicate past the
        /// end grows the map to reach it.
        /// </summary>
        public ChromFileIdMap<T> Set(int replicateIndex, ChromFileInfoId fileId, T value)
        {
            int position = ChromFileIds.IndexOfFile(replicateIndex, fileId);
            if (position >= 0)
            {
                if (EqualityComparer<T>.Default.Equals(FlatValues[position], value))
                {
                    return this;
                }

                var replaced = FlatValues.ToArray();
                replaced[position] = value;
                return new ChromFileIdMap<T>(ChromFileIds, replaced);
            }

            int replicateCount = replicateIndex < Count ? Count : replicateIndex + 1;
            var counts = new List<int>(replicateCount);
            var fileIds = new List<ChromFileInfoId>(FlatValues.Count + 1);
            var values = new List<T>(FlatValues.Count + 1);
            for (int i = 0; i < replicateCount; i++)
            {
                int count = 0;
                foreach (int p in ReplicatePositions[i])
                {
                    fileIds.Add(ChromFileIds.FileIds[p].Value);
                    values.Add(FlatValues[p]);
                    count++;
                }

                if (i == replicateIndex)
                {
                    fileIds.Add(fileId);
                    values.Add(value);
                    count++;
                }

                counts.Add(count);
            }

            return new ChromFileIdMap<T>(new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds),
                values);
        }
    }

    public static class ChromFileIdMaps
    {
        public static ChromFileIdMap<T?> ToNullables<T>(this ChromFileIdMap<T> map) where T : struct
        {
            return new ChromFileIdMap<T?>(map.ChromFileIds, map.FlatValues.Cast<T?>().Nullables());
        }

        public static ChromFileIdMap<T> FromNullables<T>(this ChromFileIdMap<T?> map, T defaultValue) where T : struct
        {
            return new ChromFileIdMap<T>(map.ChromFileIds, map.FlatValues.Select(value => value ?? defaultValue));
        }
    }
}
