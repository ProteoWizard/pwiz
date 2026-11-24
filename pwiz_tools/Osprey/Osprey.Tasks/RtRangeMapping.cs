/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The library and MS2 retention-time ranges of one run, and the linear library-to-run RT
    /// mapping calibration starts from when the two scales differ. Its
    /// <see cref="FallbackRtMap"/> is the search centre of a run whose calibration failed
    /// (issue #4401), so Stage 6 rebuilds it from the same inputs to recompute what Stage 4
    /// detected in such a run.
    /// </summary>
    internal sealed class RtRangeMapping
    {
        /// <summary>
        /// Measure the target RT range of <paramref name="library"/> (decoys are skipped) and the
        /// RT range of <paramref name="ms2Rts"/>, and derive the mapping.
        /// </summary>
        public static RtRangeMapping Measure(IEnumerable<LibraryEntry> library, IEnumerable<double> ms2Rts)
        {
            double libMinRt = double.MaxValue, libMaxRt = double.MinValue;
            double mzmlMinRt = double.MaxValue, mzmlMaxRt = double.MinValue;
            foreach (var entry in library)
            {
                if (entry.IsDecoy)
                    continue;
                if (entry.RetentionTime < libMinRt)
                    libMinRt = entry.RetentionTime;
                if (entry.RetentionTime > libMaxRt)
                    libMaxRt = entry.RetentionTime;
            }

            // MS2 RT range, from the streaming index (every MS2 record's RT in file order).
            foreach (double rt in ms2Rts)
            {
                if (rt < mzmlMinRt)
                    mzmlMinRt = rt;
                if (rt > mzmlMaxRt)
                    mzmlMaxRt = rt;
            }
            return new RtRangeMapping(libMinRt, libMaxRt, mzmlMinRt, mzmlMaxRt);
        }

        private RtRangeMapping(double libMinRt, double libMaxRt, double mzmlMinRt, double mzmlMaxRt)
        {
            LibMinRt = libMinRt;
            LibMaxRt = libMaxRt;
            MzmlMinRt = mzmlMinRt;
            MzmlMaxRt = mzmlMaxRt;
            double libRtRange = LibRtRange;

            // Linear RT mapping when library and mzML scales differ significantly.
            RangesSimilar = libRtRange > 0 && MzmlRtRange > 0 &&
                Math.Max(libRtRange / MzmlRtRange, MzmlRtRange / libRtRange) < 2.0 &&
                Math.Abs(libMinRt - mzmlMinRt) < libRtRange * 0.5;
            Slope = 1.0;
            Intercept = 0.0;
            if (IsMapped)
            {
                Slope = MzmlRtRange / libRtRange;
                Intercept = mzmlMinRt - Slope * libMinRt;
            }
        }

        public double LibMinRt { get; }
        public double LibMaxRt { get; }
        public double MzmlMinRt { get; }
        public double MzmlMaxRt { get; }
        public double LibRtRange => LibMaxRt - LibMinRt;
        public double MzmlRtRange => MzmlMaxRt - MzmlMinRt;

        /// <summary>True when the two RT scales are close enough to search on the library's.</summary>
        public bool RangesSimilar { get; }

        /// <summary>True when the library RTs are mapped onto the run's (<see cref="Slope"/>, <see cref="Intercept"/>).</summary>
        public bool IsMapped => !RangesSimilar && LibRtRange > 0;

        public double Slope { get; }
        public double Intercept { get; }

        /// <summary>
        /// The mapping as a calibration: identity when the scales agree, null for a degenerate
        /// library RT range (<see cref="RTCalibration.FromLinearMapping"/>).
        /// </summary>
        public RTCalibration FallbackRtMap()
        {
            return RTCalibration.FromLinearMapping(LibMinRt, LibMaxRt, Slope, Intercept);
        }
    }
}
