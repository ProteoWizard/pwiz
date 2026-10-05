/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// A DIA MS2 spectrum with isolation window. Maps to osprey-core/src/types.rs Spectrum.
    /// </summary>
    public class Spectrum
    {
        private double[] _mzs;
        private MzBucketIndex _mzIndex;

        public uint ScanNumber { get; set; }
        public double RetentionTime { get; set; }
        public double PrecursorMz { get; set; }
        public IsolationWindow IsolationWindow { get; set; }

        /// <summary>
        /// Peak m/z values, ascending. Assigning a new array discards the lookup index
        /// <see cref="MzLowerBound"/> builds; changing the array's values in place after the
        /// first lookup is not allowed (the window provider calibrates in place, before any).
        /// </summary>
        public double[] Mzs
        {
            get { return _mzs; }
            set
            {
                _mzs = value;
                _mzIndex = null;
            }
        }

        public float[] Intensities { get; set; }

        public int Count { get { return Mzs.Length; } }
        public bool IsEmpty { get { return Count == 0; } }

        /// <summary>
        /// The index of the first peak whose m/z is at least <paramref name="value"/> (the
        /// peak count when none is) - exactly what a binary search of <see cref="Mzs"/> returns,
        /// in O(1) through an m/z bucket index built on the first call. Scoring asks this for
        /// every top fragment of every candidate at every scan of its RT range, which made
        /// the binary search a third of first-pass scoring time.
        /// </summary>
        public int MzLowerBound(double value)
        {
            // Built into a local first: a concurrent first call builds an identical index.
            var index = _mzIndex;
            if (index == null)
                _mzIndex = index = new MzBucketIndex(_mzs);
            return index.LowerBound(_mzs, value);
        }

        /// <summary>
        /// Returns true if the given m/z is contained within this spectrum's isolation window.
        /// </summary>
        public bool ContainsPrecursor(double mz)
        {
            return IsolationWindow.Contains(mz);
        }
    }
}
