/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// A Comet fast-XCorr preprocessed spectrum stored as its sparse pre-subtraction
    /// source rather than its dense post-subtraction result (issue #4398).
    ///
    /// The HRAM cache used to retain one <c>float[NBins]</c> per spectrum
    /// (NBins = 2000/0.02 = 100,001 -> 391 KB, on the LOH) for every spectrum in a
    /// window, for the whole window's candidate loop. With ~2,000 spectra per Astral
    /// window and <c>NThreads</c> windows scored concurrently that reached ~18-37 GB.
    ///
    /// The *result* of the sliding-window subtraction genuinely IS dense -- every bin
    /// within +/-offset of any peak becomes nonzero -- so it cannot be stored sparsely.
    /// But it never needs to be materialized: XCorr only ever probes the result at the
    /// ~10-30 fragment bins of a library entry. Each probe is recoverable on demand from
    /// the sparse *input* (the binned + windowing-normalized peaks, ~1-3k nonzero bins)
    /// plus a prefix sum over those peaks.
    ///
    /// Bit-exactness: the dense implementation accumulates
    /// <c>prefix[i+1] = prefix[i] + spectrum[i]</c>, adding <c>0.0</c> at every empty bin.
    /// <c>x + 0.0 == x</c> exactly in IEEE-754 (intensities are non-negative, so the
    /// -0.0 case cannot arise), so a prefix accumulated over only the nonzero bins in
    /// ascending bin order equals the dense prefix at every index, bit for bit. The
    /// window sum, the centered value, and the final narrowing to <c>float</c> are then
    /// identical -- see <see cref="CenteredAt"/>.
    ///
    /// Memory: 20 B per retained peak (int bin + double value + double prefix) vs
    /// 391 KB per spectrum, i.e. ~10x at ~2,000 peaks.
    /// </summary>
    public sealed class SparseXcorrSpectrum
    {
        // Ascending, de-duplicated bins whose windowing-normalized value is nonzero.
        private readonly int[] _bins;
        // _values[i] is the windowed (pre-subtraction) value at _bins[i].
        private readonly double[] _values;
        // _prefix[i] == sum(_values[0..i-1]); length _bins.Length + 1. Accumulated in
        // ascending bin order so it matches the dense prefix element for element.
        private readonly double[] _prefix;
        private readonly int _nBins;
        private readonly int _offset;
        private readonly double _normFactor;

        internal SparseXcorrSpectrum(int[] bins, double[] values, double[] prefix,
            int nBins, int offset)
        {
            _bins = bins;
            _values = values;
            _prefix = prefix;
            _nBins = nBins;
            _offset = offset;
            _normFactor = 1.0 / (2 * offset);
        }

        /// <summary>Bin count of the underlying bin config (the dense width).</summary>
        public int NBins { get { return _nBins; } }

        /// <summary>Retained nonzero bins; the memory this type actually costs.</summary>
        public int PeakCount { get { return _bins.Length; } }

        /// <summary>
        /// The Comet fast-XCorr centered value at <paramref name="bin"/>, computed on
        /// demand and narrowed to <c>float</c> exactly as the dense f32 cache stored it.
        ///
        /// The narrowing is load-bearing, not cosmetic: the dense path computed in f64,
        /// stored <c>float</c>, and <c>XcorrFromPreprocessed</c> widened back to f64 on
        /// read. Returning the raw f64 here would drift every XCorr away from the golden.
        /// </summary>
        public float CenteredAt(int bin)
        {
            if (bin < 0 || bin >= _nBins)
                return 0f;

            int idx = Array.BinarySearch(_bins, bin);
            double v = idx >= 0 ? _values[idx] : 0.0;

            int left = bin - _offset;
            if (left < 0)
                left = 0;
            int right = bin + _offset + 1;
            if (right > _nBins)
                right = _nBins;

            // prefix[k] in the dense implementation is the sum of all bins strictly
            // below k, which is exactly the sum of the retained peaks below k.
            double windowSum = PrefixBelow(right) - PrefixBelow(left);
            double sumExcludingCenter = windowSum - v;
            double centered = v - sumExcludingCenter * _normFactor;
            return (float)centered;
        }

        /// <summary>Sum of the retained values at bins strictly less than <paramref name="bin"/>.</summary>
        private double PrefixBelow(int bin)
        {
            // Lower bound: first index whose bin is >= `bin`. _prefix at that index is
            // the running sum of everything below it.
            int lo = 0, hi = _bins.Length;
            while (lo < hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                if (_bins[mid] < bin)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return _prefix[lo];
        }
    }
}
