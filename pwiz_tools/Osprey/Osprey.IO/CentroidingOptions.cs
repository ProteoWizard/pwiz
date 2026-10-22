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
using pwiz.Osprey.Centroid;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// How <see cref="SpectrumFileReader"/> centroids profile spectra itself, with Centrix
    /// (<see cref="CentrixCentroider"/>), instead of asking the vendor library to: the settings for MS1 and for MS2,
    /// and the threads that centroid in parallel while the file is read. A sigma of 0 is measured from the run
    /// (<see cref="CentrixCalibration"/>).
    /// </summary>
    public sealed class CentroidingOptions
    {
        public CentroidingOptions(CentrixParams ms1, CentrixParams ms2, int threads)
        {
            Ms1 = ms1.Copy();
            Ms2 = ms2.Copy();
            Threads = Math.Max(1, threads);
        }

        public CentrixParams Ms1 { get; }
        public CentrixParams Ms2 { get; }
        public int Threads { get; }

        /// <summary>The settings for an MS level: MS1's for 1, MS2's for every other.</summary>
        public CentrixParams ForLevel(int msLevel)
        {
            return msLevel == 1 ? Ms1 : Ms2;
        }
    }
}
