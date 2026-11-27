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

using System.Collections.Generic;
using pwiz.Quasar.Features;

namespace pwiz.Quasar.Core.Traces
{
    /// <summary>
    /// One MS2 scan of an isolation window, as Stage 1 needs it: its time, centroids, the
    /// isotope envelopes Hardklor found in it, and the factor that turns its intensities into
    /// ion counts.
    /// </summary>
    public sealed class WindowScan
    {
        public WindowScan(double retentionTime, double ionsPerIntensity, double[] mzs, double[] intensities,
            IReadOnlyList<IsotopeEnvelope> envelopes)
        {
            RetentionTime = retentionTime;
            IonsPerIntensity = ionsPerIntensity;
            Mzs = mzs;
            Intensities = intensities;
            Envelopes = envelopes;
        }

        /// <summary>Scan time, in minutes.</summary>
        public double RetentionTime { get; }

        /// <summary>
        /// Ions per unit of intensity: injection time (ms) / alpha, from Glimpse's ion conversion
        /// (Hsu et al., J. Proteome Res. 2025, 24, 5742), or 1 when the injection time is unknown.
        /// </summary>
        public double IonsPerIntensity { get; }

        /// <summary>Centroid m/z values, ascending.</summary>
        public double[] Mzs { get; }

        public double[] Intensities { get; }

        /// <summary>Envelopes found in this scan; their peak indices point into <see cref="Mzs"/>.</summary>
        public IReadOnlyList<IsotopeEnvelope> Envelopes { get; }
    }
}
