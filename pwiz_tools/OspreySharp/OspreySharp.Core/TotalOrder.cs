/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Collections.Generic;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// IEEE 754-2008 section 5.10 total ordering on doubles, matching Rust's
    /// f64::total_cmp (so -0.0 sorts below +0.0 and NaNs order consistently).
    /// Pair <see cref="Comparer"/> with LINQ OrderBy/OrderByDescending (stable
    /// per the .NET contract) to reproduce Rust's
    /// slice::sort_by(... .total_cmp(...)) byte-for-byte.
    ///
    /// Relocated verbatim out of AbstractScoringTask, which carried two copies
    /// of the same bit transform (a comparer for the FDR ranking sort and a
    /// greater-than for the main-search tie-break). The arithmetic is unchanged,
    /// so cross-impl parity is unaffected.
    /// </summary>
    public static class TotalOrder
    {
        /// <summary>
        /// Total-order comparer: pair with a stable OrderBy/OrderByDescending to
        /// match Rust's total_cmp sort.
        /// </summary>
        public static readonly IComparer<double> Comparer =
            Comparer<double>.Create((a, b) => Key(a).CompareTo(Key(b)));

        /// <summary>
        /// Maps a double to the signed 64-bit key whose natural ordering is the
        /// IEEE-754 total order: flip all but the sign bit for negatives so the
        /// two's-complement comparison sorts -0.0 below +0.0 and orders NaNs.
        /// </summary>
        public static long Key(double v)
        {
            long bits = BitConverter.DoubleToInt64Bits(v);
            if (bits < 0)
                bits ^= 0x7FFFFFFFFFFFFFFFL;
            return bits;
        }

        /// <summary>
        /// True when <paramref name="a"/> is greater than <paramref name="b"/>
        /// under the total order. Used by the main-search peak-ranking tie-break.
        /// </summary>
        public static bool Greater(double a, double b)
        {
            return Key(a) > Key(b);
        }
    }
}
