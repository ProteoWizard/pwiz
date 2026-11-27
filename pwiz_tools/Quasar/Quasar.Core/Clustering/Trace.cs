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

using System;

namespace pwiz.Quasar.Core.Clustering
{
    /// <summary>
    /// One MS2 trace in one isolation window (spec Stage 1): a fragment followed across the
    /// window's scans, zero where it is absent. Only the span from its first to its last non-zero
    /// scan is stored: most traces cover a few of a window's thousand-odd scans, and a full-axis
    /// array for each of 200,000 traces was most of the memory of a run on 8 threads.
    /// </summary>
    public sealed class Trace
    {
        private readonly int _first;
        private readonly double[] _values;

        /// <summary>A trace from its intensities on the full scan axis.</summary>
        public Trace(int id, bool isAnchor, double mz, int charge, double[] intensities)
            : this(id, isAnchor, mz, charge, intensities.Length, FirstNonZero(intensities), NonZeroSpan(intensities))
        {
        }

        /// <summary>
        /// A trace on an axis of <paramref name="scanCount"/> scans whose intensities from scan
        /// <paramref name="first"/> on are <paramref name="values"/>, and zero elsewhere.
        /// </summary>
        public Trace(int id, bool isAnchor, double mz, int charge, int scanCount, int first, double[] values)
        {
            if (first < 0 || first + values.Length > scanCount)
                throw new ArgumentOutOfRangeException(nameof(first));
            Id = id;
            IsAnchor = isAnchor;
            Mz = mz;
            Charge = charge;
            ScanCount = scanCount;
            _first = first;
            _values = values;
        }

        /// <summary>Unique within the window.</summary>
        public int Id { get; }

        /// <summary>
        /// True for a Hardklor/Kronik feature (an isotope envelope with a monoisotopic mass and
        /// charge), false for a plain centroid trace. Only anchors build templates.
        /// </summary>
        public bool IsAnchor { get; }

        /// <summary>For an anchor, the singly charged monoisotopic m/z; otherwise the observed m/z.</summary>
        public double Mz { get; }

        /// <summary>The observed charge, or 0 when unknown (centroid traces).</summary>
        public int Charge { get; }

        /// <summary>The length of the window's scan axis.</summary>
        public int ScanCount { get; }

        /// <summary>The first and last scans of the stored span (Last is First - 1 for a trace with no signal).</summary>
        public int First => _first;
        public int Last => _first + _values.Length - 1;

        /// <summary>The intensity at <paramref name="scan"/>.</summary>
        public double this[int scan] => scan >= _first && scan <= Last ? _values[scan - _first] : 0.0;

        /// <summary>
        /// The intensities on the full scan axis, as a new array: take it once for a fit, not
        /// once per point.
        /// </summary>
        public double[] Dense()
        {
            var dense = new double[ScanCount];
            Array.Copy(_values, 0, dense, _first, _values.Length);
            return dense;
        }

        /// <summary>Copies the intensities of <paramref name="count"/> scans from <paramref name="from"/> into <paramref name="destination"/>.</summary>
        public void CopyTo(int from, double[] destination, int count)
        {
            for (int i = 0; i < count; i++)
                destination[i] = this[from + i];
        }

        /// <summary>How many of scans <paramref name="first"/> to <paramref name="last"/> have signal.</summary>
        public int PositiveCount(int first, int last)
        {
            int count = 0;
            for (int i = Math.Max(first, _first); i <= Math.Min(last, Last); i++)
            {
                if (_values[i - _first] > 0.0)
                    count++;
            }

            return count;
        }

        public override string ToString()
        {
            return $@"Trace[{Id}, {(IsAnchor ? @"anchor" : @"centroid")}, m/z {Mz}]";
        }

        private static int FirstNonZero(double[] intensities)
        {
            int i = Array.FindIndex(intensities, v => v != 0.0);
            return i < 0 ? 0 : i;
        }

        private static double[] NonZeroSpan(double[] intensities)
        {
            int first = Array.FindIndex(intensities, v => v != 0.0);
            if (first < 0)
                return Array.Empty<double>();
            int last = Array.FindLastIndex(intensities, v => v != 0.0);
            var span = new double[last - first + 1];
            Array.Copy(intensities, first, span, 0, span.Length);
            return span;
        }
    }
}
