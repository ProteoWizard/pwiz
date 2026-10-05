/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// A lower-bound lookup over one spectrum's ascending m/z array that answers in O(1) what a
    /// binary search answers in O(log n). The range [first m/z, last m/z] is cut into one bucket
    /// per peak; <c>_start[b]</c> is the first peak whose bucket is at least b.
    ///
    /// <para>Exact, not approximate: the bucket of an m/z, <c>(int)((mz - first) * scale)</c>
    /// clamped to the last bucket, never decreases as m/z increases (IEEE subtraction,
    /// multiplication by a positive constant and truncation are all monotone). So every peak in
    /// a lower bucket than the query is below it, and every peak in a higher bucket is above it,
    /// and the lower bound lies in [<c>_start[b]</c>, <c>_start[b + 1]</c>] - a range of about one
    /// peak, scanned linearly. The result is the binary search's for any query, including one
    /// below the first peak, above the last, or NaN (index 0, as the search's
    /// <c>mzs[mid] &lt; value</c> is then always false).</para>
    ///
    /// <para>Costs one int per peak, held only while the spectrum is.</para>
    /// </summary>
    public sealed class MzBucketIndex
    {
        private readonly double _first;
        private readonly double _last;
        private readonly double _scale;
        private readonly int _lastBucket;
        private readonly int[] _start;

        public MzBucketIndex(double[] mzs)
        {
            if (mzs == null || mzs.Length == 0)
            {
                _start = new int[2];
                return;
            }
            int n = mzs.Length;
            _first = mzs[0];
            _last = mzs[n - 1];
            double span = _last - _first;
            int nBuckets = n;
            _lastBucket = nBuckets - 1;
            _scale = span > 0 ? nBuckets / span : 0;
            _start = new int[nBuckets + 1];
            int b = 0;
            for (int i = 0; i < n; i++)
            {
                int bucket = BucketOf(mzs[i]);
                while (b <= bucket)
                    _start[b++] = i;
            }
            while (b <= nBuckets)
                _start[b++] = n;
        }

        /// <summary>
        /// The first index of <paramref name="mzs"/> (the array this index was built from) whose
        /// value is at least <paramref name="value"/>, or its length when none is.
        /// </summary>
        public int LowerBound(double[] mzs, double value)
        {
            // Not "value <= first": a NaN query must also land here, as it does in the search.
            if (mzs == null || mzs.Length == 0 || !(value > _first))
                return 0;
            if (value > _last)
                return mzs.Length;
            int bucket = BucketOf(value);
            int end = _start[bucket + 1];
            int i = _start[bucket];
            while (i < end && mzs[i] < value)
                i++;
            return i;
        }

        private int BucketOf(double mz)
        {
            int bucket = (int)((mz - _first) * _scale);
            return bucket > _lastBucket ? _lastBucket : bucket;
        }
    }
}
