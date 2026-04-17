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
    /// Quality metrics for a chromatographic peak. Maps to osprey-core/src/types.rs PeakQuality.
    /// </summary>
    public class PeakQuality
    {
        public double SignalToNoise { get; set; }
        public double Symmetry { get; set; }
        public double Fwhm { get; set; }
    }

    /// <summary>
    /// Defines the boundaries and metrics of an integrated chromatographic peak.
    /// Maps to osprey-core/src/types.rs PeakBoundaries.
    /// </summary>
    public class PeakBoundaries
    {
        public double StartRt { get; set; }
        public double EndRt { get; set; }
        public double ApexRt { get; set; }
        public double ApexCoefficient { get; set; }
        public double IntegratedArea { get; set; }
        public PeakQuality PeakQuality { get; set; }
    }
}
