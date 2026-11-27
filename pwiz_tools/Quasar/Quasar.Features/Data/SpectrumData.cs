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
using System.Collections.Generic;

namespace pwiz.Quasar.Features.Data
{
    /// <summary>
    /// One spectrum as read from a file: its header, its peak arrays (ascending m/z), and descriptive
    /// metadata.
    /// </summary>
    public sealed class SpectrumData
    {
        /// <summary>First index whose value is &gt;= <paramref name="value"/>.</summary>
        public static int LowerBound(double[] sorted, double value)
        {
            int lo = 0, hi = sorted.Length;
            while (lo < hi)
            {
                var mid = (lo + hi) >>> 1;
                if (sorted[mid] < value)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        public SpectrumData(ScanHeader header, double[] mz, double[] intensity, SpectrumDetails details)
        {
            if (mz.Length != intensity.Length)
                throw new ArgumentException(@"m/z and intensity arrays differ in length.");
            Header = header;
            Mz = mz;
            Intensity = intensity;
            Details = details;
        }

        public ScanHeader Header { get; }

        /// <summary>Peak m/z values, ascending.</summary>
        public double[] Mz { get; }

        /// <summary>Peak intensities as read (ions/s for Orbitrap/Astral data), parallel to <see cref="Mz"/>.</summary>
        public double[] Intensity { get; }

        public SpectrumDetails Details { get; }

        /// <summary>Index of the most intense peak within [<paramref name="low"/>, <paramref name="high"/>], or -1.</summary>
        public int MostIntenseIn(double low, double high)
        {
            var i = LowerBound(Mz, low);
            var best = -1;
            var bestIntensity = double.NegativeInfinity;
            for (; i < Mz.Length && Mz[i] <= high; i++)
            {
                if (Intensity[i] > bestIntensity)
                {
                    bestIntensity = Intensity[i];
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Sum of the intensities within [<paramref name="low"/>, <paramref name="high"/>].</summary>
        public double SumIn(double low, double high)
        {
            var sum = 0.0;
            for (var i = LowerBound(Mz, low); i < Mz.Length && Mz[i] <= high; i++)
                sum += Intensity[i];
            return sum;
        }
    }

    /// <summary>Descriptive per-spectrum metadata: for display, not used for computation.</summary>
    public sealed record SpectrumDetails
    {
        /// <summary>Native spectrum identifier, e.g. "controllerType=0 controllerNumber=1 scan=1234".</summary>
        public string NativeId { get; init; } = string.Empty;

        /// <summary>Scan number parsed from the native id when it has one.</summary>
        public int? ScanNumber { get; init; }

        /// <summary>Vendor filter / scan description string (Thermo filter line), when present.</summary>
        public string FilterString { get; init; }

        /// <summary>Dissociation method(s), e.g. "HCD", "ETD", "EThcD".</summary>
        public string Dissociation { get; init; }

        /// <summary>Collision energy as reported (normalized or absolute), when present.</summary>
        public double? CollisionEnergy { get; init; }

        /// <summary>Selected precursor m/z (may differ from the isolation target for DDA), when present.</summary>
        public double? SelectedIonMz { get; init; }

        /// <summary>Selected precursor charge, when present.</summary>
        public int? SelectedIonCharge { get; init; }

        /// <summary>Scan window (acquisition m/z range) lower and upper limits, when present.</summary>
        public double? ScanWindowLower { get; init; }

        public double? ScanWindowUpper { get; init; }

        /// <summary>Ion mobility value and its units, when present.</summary>
        public string IonMobility { get; init; }

        /// <summary>Any other parameters worth showing, as name/value pairs in file order.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Other { get; init; } = Array.Empty<KeyValuePair<string, string>>();
    }
}
