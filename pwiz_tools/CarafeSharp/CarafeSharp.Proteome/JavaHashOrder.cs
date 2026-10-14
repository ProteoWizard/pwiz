/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// The iteration order of a <c>java.util.HashMap</c> (or <c>HashSet</c>) filled by one thread
    /// and never shrunk, which Carafe's output depends on wherever it iterates one: by bucket
    /// index (the spread hash masked by the table size the map grew to), then by insertion
    /// order within a bucket, as resizing preserves it. The one departure, a bucket of nine or
    /// more keys being turned into a tree, needs adversarial hashes and is not modeled.
    /// </summary>
    public static class JavaHashOrder
    {
        /// <summary>HashMap's default initial capacity.</summary>
        public const int DEFAULT_CAPACITY = 16;

        private const float LOAD_FACTOR = 0.75f;
        private const int MAXIMUM_CAPACITY = 1 << 30;

        /// <summary><c>String.hashCode()</c>.</summary>
        public static int StringHash(string s)
        {
            int h = 0;
            foreach (char c in s)
                h = unchecked(31 * h + c);
            return h;
        }

        /// <summary>
        /// The table size a map constructed with <paramref name="initialCapacity"/> has after
        /// <paramref name="count"/> distinct keys: <c>tableSizeFor</c> of the initial capacity,
        /// doubled whenever the size passes three quarters of the table.
        /// </summary>
        public static int TableSize(int count, int initialCapacity = DEFAULT_CAPACITY)
        {
            int capacity = TableSizeFor(initialCapacity);
            while (capacity < MAXIMUM_CAPACITY && count > (int)(capacity * LOAD_FACTOR))
                capacity <<= 1;
            return capacity;
        }

        /// <summary>The bucket <c>HashMap</c> puts a key with hash code <paramref name="hashCode"/> in.</summary>
        public static int BucketIndex(int hashCode, int tableSize)
        {
            int spread = hashCode ^ (int)((uint)hashCode >> 16);
            return spread & (tableSize - 1);
        }

        /// <summary>
        /// String keys, distinct and in insertion order, in the order a HashMap constructed with
        /// <paramref name="initialCapacity"/> iterates them.
        /// </summary>
        public static List<string> OrderStringKeys(IReadOnlyList<string> keysInInsertionOrder, int initialCapacity = DEFAULT_CAPACITY)
        {
            int tableSize = TableSize(keysInInsertionOrder.Count, initialCapacity);
            return Enumerable.Range(0, keysInInsertionOrder.Count)
                .OrderBy(i => BucketIndex(StringHash(keysInInsertionOrder[i]), tableSize))
                .ThenBy(i => i)
                .Select(i => keysInInsertionOrder[i])
                .ToList();
        }

        private static int TableSizeFor(int capacity)
        {
            int n = 1;
            while (n < capacity && n < MAXIMUM_CAPACITY)
                n <<= 1;
            return n;
        }
    }
}
