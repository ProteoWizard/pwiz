/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
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

namespace pwiz.Common.Collections
{
    /// <summary>
    /// In-place sort of a double[] key array carrying up to two parallel double[] arrays with it,
    /// with no intermediate index array and no LINQ allocations.
    /// Lives here rather than in Skyline's ArrayUtil so that both Skyline and ProteowizardWrapper can use
    /// it: spectra have to arrive m/z sorted, and the cheapest place to guarantee that is where the
    /// arrays are assigned, which is here. Skyline's ArrayUtil.Sort delegates to this.
    /// </summary>
    public static class ParallelDoubleSort
    {
        private const int INSERTION_SORT_THRESHOLD = 16;

        /// <summary>
        /// Sort <paramref name="keys"/> ascending, reordering <paramref name="secondary1"/> and
        /// <paramref name="secondary2"/> the same way. Either secondary array may be null.
        /// Returns true if the already-sorted fast path was taken (no sort needed).
        ///
        /// Assumes keys contains no NaN. IsSorted and IntrosortDouble below both use raw &lt; and
        /// &gt; comparisons, which return false on any NaN-involved pair. The partition loop still
        /// terminates (no hang, no OOB), but on NaN input the result is silently wrong: either
        /// IsSorted returns a false positive and the sort is skipped, or a NaN pivot makes the
        /// partition meaningless. Callers sort m/z arrays, where a NaN would mean a corrupt file
        /// that could not be extracted meaningfully in any case.
        /// </summary>
        public static bool Sort(double[] keys, double[] secondary1 = null, double[] secondary2 = null)
        {
            if (keys == null || keys.Length < 2)
                return true;
            if (IsSorted(keys))
                return true;

            IntrosortDouble(keys, secondary1, secondary2, 0, keys.Length - 1);
            return false;
        }

        /// <summary>
        /// Returns true if <paramref name="array"/> is in non-decreasing order. Typed to avoid the
        /// Comparer&lt;T&gt;.Default virtual call per element in the hot path.
        /// </summary>
        public static bool IsSorted(double[] array)
        {
            if (array == null || array.Length < 2)
                return true;
            for (var i = 1; i < array.Length; i++)
            {
                if (array[i - 1] > array[i])
                    return false;
            }
            return true;
        }

        // Classic introsort-shaped quicksort over up to three parallel double[] arrays.
        // Iterative on the larger partition to bound stack depth; insertion sort for small
        // subranges. a and b may be null (handled with a branch per swap; predictable since
        // null-ness is loop-invariant).
        private static void IntrosortDouble(double[] keys, double[] a, double[] b, int lo, int hi)
        {
            while (hi - lo >= INSERTION_SORT_THRESHOLD)
            {
                var mid = lo + ((hi - lo) >> 1);

                // Median-of-three: arrange keys at lo, mid, hi so keys[lo] <= keys[mid] <= keys[hi].
                if (keys[mid] < keys[lo])
                    Swap3(keys, a, b, lo, mid);
                if (keys[hi] < keys[lo])
                    Swap3(keys, a, b, lo, hi);
                if (keys[hi] < keys[mid])
                    Swap3(keys, a, b, mid, hi);

                var pivot = keys[mid];
                // Move pivot out of the way to hi-1.
                Swap3(keys, a, b, mid, hi - 1);

                var i = lo;
                var j = hi - 1;
                while (true)
                {
                    while (keys[++i] < pivot) { }
                    while (keys[--j] > pivot) { }
                    if (i >= j)
                        break;
                    Swap3(keys, a, b, i, j);
                }
                // Restore pivot.
                Swap3(keys, a, b, i, hi - 1);

                // Recurse on smaller side, loop on larger (limits stack depth to O(log N)).
                if (i - lo < hi - i)
                {
                    IntrosortDouble(keys, a, b, lo, i - 1);
                    lo = i + 1;
                }
                else
                {
                    IntrosortDouble(keys, a, b, i + 1, hi);
                    hi = i - 1;
                }
            }
            InsertionSortDouble(keys, a, b, lo, hi);
        }

        private static void InsertionSortDouble(double[] keys, double[] a, double[] b, int lo, int hi)
        {
            for (var i = lo + 1; i <= hi; i++)
            {
                var kv = keys[i];
                var av = a?[i] ?? 0;
                var bv = b?[i] ?? 0;
                var j = i - 1;
                while (j >= lo && keys[j] > kv)
                {
                    keys[j + 1] = keys[j];
                    if (a != null)
                        a[j + 1] = a[j];
                    if (b != null)
                        b[j + 1] = b[j];
                    j--;
                }
                keys[j + 1] = kv;
                if (a != null)
                    a[j + 1] = av;
                if (b != null)
                    b[j + 1] = bv;
            }
        }

        private static void Swap3(double[] keys, double[] a, double[] b, int i, int j)
        {
            var t = keys[i];
            keys[i] = keys[j];
            keys[j] = t;
            if (a != null)
            {
                t = a[i];
                a[i] = a[j];
                a[j] = t;
            }
            if (b != null)
            {
                t = b[i];
                b[i] = b[j];
                b[j] = t;
            }
        }
    }
}
