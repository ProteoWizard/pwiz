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

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// A DIA MS2 spectrum with isolation window. Maps to osprey-core/src/types.rs Spectrum.
    /// </summary>
    public class Spectrum
    {
        public uint ScanNumber { get; set; }
        public double RetentionTime { get; set; }
        public double PrecursorMz { get; set; }
        public IsolationWindow IsolationWindow { get; set; }
        public double[] Mzs { get; set; }
        public float[] Intensities { get; set; }

        public int Count { get { return Mzs.Length; } }
        public bool IsEmpty { get { return Count == 0; } }

        /// <summary>
        /// Returns true if the given m/z is contained within this spectrum's isolation window.
        /// </summary>
        public bool ContainsPrecursor(double mz)
        {
            return IsolationWindow.Contains(mz);
        }
    }
}
