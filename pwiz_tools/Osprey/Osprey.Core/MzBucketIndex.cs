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

using System;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// A lower-bound lookup over one spectrum's ascending m/z array that answers in O(1)
    /// expected time what a binary search answers in O(log n). The range [first m/z, last m/z]
    /// is cut into one bucket per peak; <c>_start[b]</c> is the first peak whose bucket is at
    /// least b.
    ///
    /// <para>Exact, not approximate: the bucket of an m/z, <c>(mz - first) * scale</c> clamped
    /// to [0, last bucket] and truncated, never decreases as m/z increases (IEEE subtraction,
    /// multiplication by a positive constant, clamping and truncation are all monotone). So
    /// every peak in a lower bucket than the query is below it, and every peak in a higher
    /// bucket is above it, and the lower bound lies in [<c>_start[b]</c>, <c>_start[b + 1]</c>] -
    /// a range of about one peak, scanned linearly, or binary searched when it is crowded. The
    /// result is the binary search's for any query, including one below the first peak, above
    /// the last, or NaN (index 0, as the search's <c>mzs[mid] &lt; value</c> is then always
    /// false).</para>
    ///
    /// <para>An array the buckets cannot describe - empty, with a non-finite first or last m/z,
    /// or not ascending - gets no buckets, and every lookup is the plain binary search, so the
    /// answer stays the search's whatever the input.</para>
    ///
    /// <para>Costs one int per peak, held only while the spectrum is.</para>
    /// </summary>
    internal sealed class MzBucketIndex
    {
        // Above this many candidate peaks in one bucket, binary search them instead of scanning.
        private const int MAX_LINEAR_SCAN = 8;

        private readonly double[] _source;
        private readonly bool _fallback;
        private readonly double _first;
        private readonly double _last;
        private readonly double _scale;
        private readonly int _lastBucket;
        private readonly int[] _start;

        public MzBucketIndex(double[] mzs)
        {
            _source = mzs;
            if (mzs == null || mzs.Length == 0)
            {
                _fallback = true;
                return;
            }
            int n = mzs.Length;
            _first = mzs[0];
            _last = mzs[n - 1];
            if (!double.IsFinite(_first) || !double.IsFinite(_last))
            {
                _fallback = true;
                return;
            }
            double span = _last - _first;
            int nBuckets = n;
            _lastBucket = nBuckets - 1;
            // A span so small that n / span overflows gets one bucket, as does a zero span.
            double scale = span > 0 ? nBuckets / span : 0;
            _scale = double.IsFinite(scale) ? scale : 0;

            // _start[b] = first peak whose bucket is at least b; _start[nBuckets] = n. Record
            // the first peak of each bucket (descending, so the lowest index wins), then carry
            // the minimum down from the empty buckets above.
            _start = new int[nBuckets + 1];
            Array.Fill(_start, n);
            for (int i = n - 1; i >= 0; i--)
            {
                // Negated, so that a NaN peak also counts as out of order.
                if (i > 0 && !(mzs[i] >= mzs[i - 1]))
                {
                    _fallback = true;
                    _start = null;
                    return;
                }
                _start[BucketOf(mzs[i])] = i;
            }
            for (int b = nBuckets - 1; b >= 0; b--)
            {
                if (_start[b + 1] < _start[b])
                    _start[b] = _start[b + 1];
            }
        }

        /// <summary>
        /// The array this index was built from.
        /// </summary>
        public double[] Source { get { return _source; } }

        /// <summary>
        /// The first index of <see cref="Source"/> whose value is at least
        /// <paramref name="value"/>, or its length when none is - exactly the binary search's
        /// result. A NaN query returns 0, as the search does.
        /// </summary>
        public int LowerBound(double value)
        {
            var mzs = _source;
            if (_fallback)
                return BinarySearchLowerBound(mzs, 0, mzs == null ? 0 : mzs.Length, value);
            // Not "value <= first": a NaN query must also land here, as it does in the search.
            if (!(value > _first))
                return 0;
            if (value > _last)
                return mzs.Length;
            int bucket = BucketOf(value);
            int i = _start[bucket];
            int end = _start[bucket + 1];
            if (end - i > MAX_LINEAR_SCAN)
                return BinarySearchLowerBound(mzs, i, end, value);
            while (i < end && mzs[i] < value)
                i++;
            return i;
        }

        /// <summary>
        /// The bucket of an m/z, clamped to [0, last bucket] in floating point so the cast never
        /// sees a value outside the int range (or a NaN).
        /// </summary>
        private int BucketOf(double mz)
        {
            double position = (mz - _first) * _scale;
            if (!(position > 0))
                return 0;
            if (position >= _lastBucket)
                return _lastBucket;
            return (int)position;
        }

        /// <summary>
        /// Lower bound of <paramref name="value"/> in <paramref name="sorted"/>[lo, hi), with the
        /// same comparison as ScoringMath.BinarySearchLowerBound (which lives in Osprey.Scoring).
        /// </summary>
        private static int BinarySearchLowerBound(double[] sorted, int lo, int hi, double value)
        {
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (sorted[mid] < value)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }
    }
}
