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

namespace pwiz.Quasar.Features.Data
{
    /// <summary>Ion polarity of a scan.</summary>
    public enum Polarity : byte
    {
        unknown = 0,
        positive = 1,
        negative = 2,
    }

    /// <summary>
    /// The per-spectrum facts kept for every spectrum in a run. Small and fixed-size so a
    /// 300,000-spectrum Astral run costs a few tens of MB, not hundreds.
    /// </summary>
    /// <param name="Index">Zero-based spectrum index in the file.</param>
    /// <param name="MsLevel">1, 2, ... or 0 when the file does not say.</param>
    /// <param name="RetentionTime">Scan start time in minutes, NaN when absent.</param>
    /// <param name="InjectionTimeMs">Ion injection (accumulation) time in milliseconds, NaN when absent.</param>
    /// <param name="IsolationTarget">MS2+: isolation window target m/z, NaN for MS1 or when absent.</param>
    /// <param name="IsolationLower">MS2+: lower bound of the isolation window (absolute m/z).</param>
    /// <param name="IsolationUpper">MS2+: upper bound of the isolation window (absolute m/z).</param>
    /// <param name="TotalIonCurrent">Sum of the peak intensities as read (ions/s for Orbitrap/Astral data).</param>
    /// <param name="BasePeakMz">m/z of the most intense peak, NaN for an empty spectrum.</param>
    /// <param name="BasePeakIntensity">Intensity of the most intense peak, 0 for an empty spectrum.</param>
    /// <param name="PeakCount">Number of (m/z, intensity) points.</param>
    /// <param name="Polarity">Scan polarity.</param>
    /// <param name="Centroided">True when the peak list is centroided (by the instrument or the vendor library).</param>
    /// <param name="Analyzer">The mass analyzer that recorded the scan (its peak width and ion calibration alpha
    /// depend on it).</param>
    public readonly record struct ScanHeader(
        int Index,
        byte MsLevel,
        double RetentionTime,
        float InjectionTimeMs,
        double IsolationTarget,
        double IsolationLower,
        double IsolationUpper,
        double TotalIonCurrent,
        double BasePeakMz,
        float BasePeakIntensity,
        int PeakCount,
        Polarity Polarity,
        bool Centroided,
        MassAnalyzer Analyzer = MassAnalyzer.unknown)
    {
        /// <summary>True when the scan has an isolation window, i.e. it is an MSn scan with precursor information.</summary>
        public bool HasIsolationWindow => !double.IsNaN(IsolationLower) && !double.IsNaN(IsolationUpper);

        /// <summary>True when the injection time is known and positive, so intensities can be converted to ions.</summary>
        public bool HasInjectionTime => !float.IsNaN(InjectionTimeMs) && InjectionTimeMs > 0;

        /// <summary>True when the isolation window contains <paramref name="mz"/> (half-open, like Skyline).</summary>
        public bool IsolationContains(double mz) => HasIsolationWindow && mz >= IsolationLower && mz < IsolationUpper;
    }
}
