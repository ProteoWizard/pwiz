/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on skyline-glimpse (https://github.com/maccoss/skyline-glimpse)
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
using System.Linq;

namespace pwiz.Quasar.Features.Data
{
    /// <summary>
    /// Ion counts from intensities, as Glimpse converts them: ions = intensity x injection time
    /// (ms) / 1000 / alpha, where alpha is the signal per ion of the scan's analyzer on the
    /// instrument model (Hsu et al., J. Proteome Res. 2025, 24, 5742, Table 1). Alpha is about 1
    /// for the Astral and ion trap analyzers and about 10 for Orbitraps.
    /// </summary>
    public static class IonCalibration
    {
        private static readonly (string Instrument, MassAnalyzer Analyzer, double Alpha)[] TABLE =
        {
            (@"Orbitrap Astral Zoom", MassAnalyzer.astral, 1.31),
            (@"Orbitrap Astral", MassAnalyzer.astral, 1.53),
            (@"Orbitrap Astral Zoom", MassAnalyzer.orbitrap, 10.30),
            (@"Orbitrap Astral", MassAnalyzer.orbitrap, 10.62),
            (@"Orbitrap Exploris 480", MassAnalyzer.orbitrap, 10.95),
            (@"Orbitrap Ascend", MassAnalyzer.orbitrap, 9.40),
            (@"Orbitrap Fusion Lumos", MassAnalyzer.orbitrap, 15.44),
            (@"Stellar", MassAnalyzer.ion_trap, 0.97),
            (@"Orbitrap Ascend", MassAnalyzer.ion_trap, 0.98),
            (@"Orbitrap Fusion Lumos", MassAnalyzer.ion_trap, 1.09)
        };

        /// <summary>
        /// Alpha for <paramref name="analyzer"/> on <paramref name="instrumentModel"/> (matched
        /// ignoring case and surrounding spaces), or NaN when the table has none.
        /// </summary>
        public static double Alpha(string instrumentModel, MassAnalyzer analyzer)
        {
            if (string.IsNullOrWhiteSpace(instrumentModel))
                return double.NaN;
            string model = instrumentModel.Trim();
            return TABLE.Where(e => e.Analyzer == analyzer &&
                                    string.Equals(e.Instrument, model, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Alpha)
                .DefaultIfEmpty(double.NaN)
                .First();
        }

        /// <summary>
        /// Ions per unit of intensity for a scan: injection time / 1000 / alpha. NaN when the scan
        /// has no injection time (no ion count is ever guessed); alpha NaN is read as 1, which
        /// gives Skyline's uncalibrated ions (intensity x injection time / 1000).
        /// </summary>
        public static double IonsPerIntensity(float injectionTimeMs, double alpha)
        {
            if (float.IsNaN(injectionTimeMs) || injectionTimeMs <= 0f)
                return double.NaN;
            double divisor = double.IsFinite(alpha) && alpha > 0.0 ? alpha : 1.0;
            return injectionTimeMs / 1000.0 / divisor;
        }
    }
}
