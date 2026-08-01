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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// One value for each position of a <see cref="Results.ChromFileIds"/>, which is to say one for
    /// each file of each replicate. Indexing by replicate gives that replicate's values, the way
    /// indexing a <see cref="ReplicatePositions"/> gives its positions.
    /// <para>
    /// A replicate and a file identify a value on their own, unless optimization steps are involved:
    /// all the steps of one file share a position, because nothing kept this way can differ between
    /// them.
    /// </para>
    /// <para>
    /// Each value of a peak is its own map rather than one map of a compound object, so that a
    /// column which is the same everywhere - almost every document's user sets, for instance -
    /// collapses to a single entry on its own. Combining some of them into one object would cost
    /// fewer indirections per read, and is worth doing if reading them ever shows up.
    /// </para>
    /// </summary>
    public class ChromFileIdMap<T> : IReadOnlyList<IEnumerable<T>>
    {
        public ChromFileIdMap(ChromFileIds chromFileIds, IEnumerable<T> values)
        {
            ChromFileIds = chromFileIds;
            Values = ImmutableList.ValueOf(values);
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
        public ImmutableList<T> Values { get; private set; }

        public ReplicatePositions ReplicatePositions
        {
            get { return ChromFileIds.ReplicatePositions; }
        }

        /// <summary>
        /// How many replicates, which is what this is a list of. The number of values is
        /// <see cref="ImmutableList{T}.Count"/> on <see cref="Values"/>.
        /// </summary>
        public int Count
        {
            get { return ReplicatePositions.ReplicateCount; }
        }

        public IEnumerable<T> this[int replicateIndex]
        {
            get { return ReplicatePositions[replicateIndex].Select(position => Values[position]); }
        }

        /// <summary>
        /// The value for one file of one replicate, which is how a caller which does not already
        /// hold a position of this map's own <see cref="Results.ChromFileIds"/> asks for one.
        /// <para>
        /// There is deliberately no method taking a bare position. A position means nothing without
        /// the <see cref="Results.ChromFileIds"/> it came from, and two maps only share positions
        /// when they were built over the same one - which the maps of a single
        /// <see cref="TransitionResults"/> are, and which a precursor's and its transitions' are
        /// not. Code which does hold a position of this map reaches <see cref="Values"/> directly,
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

            value = Values[position];
            return true;
        }

        public IEnumerator<IEnumerable<T>> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(replicateIndex => this[replicateIndex]).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        protected bool Equals(ChromFileIdMap<T> other)
        {
            return Equals(ChromFileIds, other.ChromFileIds) && Equals(Values, other.Values);
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
                return (ChromFileIds.GetHashCode() * 397) ^ Values.GetHashCode();
            }
        }
    }
}
