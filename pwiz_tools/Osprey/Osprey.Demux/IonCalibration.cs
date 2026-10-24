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
using System.Collections.Generic;

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// Ion calibration (spec section 6.2): how many counts of reported intensity one ion is in a spectrum, so
    /// the Poisson weights count ions.
    /// </summary>
    /// <remarks>
    /// <para>Reported intensity is ion current, not ion count: y = alpha N / IT (Hsu et al., J. Proteome Res.
    /// 2025, 24(11), 5742-5754), N ions over the accumulation time IT, alpha a constant of the analyzer. One ion
    /// is therefore alpha / IT counts, fixed where IT is fixed and varying from spectrum to spectrum where it is
    /// not.</para>
    /// <para>A single-ion-counting TOF detector shows that value directly: profile intensities are sums of
    /// single-ion events rounded to integers, so a spectrum's lowest distinct nonzero levels are near n
    /// alpha / IT for small n. On a ZenoTOF 8600 ZT Scan run every MS2 spectrum is 99.665 counts per ion, the
    /// same at every m/z, while each MS1 spectrum has its own (5.9 to 19.3 across a run, following its TIC),
    /// again the same at every m/z.</para>
    /// </remarks>
    public static class IonCalibration
    {
        /// <summary>A lowest point lies on a whole number of ions when within this fraction of an ion of one.</summary>
        public const double ON_MULTIPLE = 0.25;

        /// <summary>
        /// The share of the lowest points that must lie on whole numbers of ions for an estimate to be trusted:
        /// a centroided spectrum, or one without single-ion events, has no such levels.
        /// </summary>
        public const double MIN_FIT = 0.9;

        /// <summary>
        /// Counts per ion of TOF profile intensities: q fitted by least squares to the lowest
        /// <paramref name="levels"/> distinct nonzero values as multiples n q, n = round(v / q), iterated from
        /// the smallest value. <paramref name="fit"/> receives the share of the points at or below those levels
        /// that lie on multiples of q. NaN, with a fit of 0, when there are fewer than
        /// <paramref name="minPoints"/> nonzero values.
        /// </summary>
        public static double CountsPerIon(IEnumerable<double> intensities, out double fit, int levels = 12, int minPoints = 50)
        {
            fit = 0;
            var values = new List<double>();
            foreach (double v in intensities)
            {
                if (v > 0)
                    values.Add(v);
            }
            if (values.Count < minPoints)
                return double.NaN;
            values.Sort(); // Array.Sort OK: primitive values, sorted only to find the lowest distinct levels
            var distinct = new List<double>();
            foreach (double v in values)
            {
                if (distinct.Count == 0 || v != distinct[distinct.Count - 1])
                {
                    distinct.Add(v);
                    if (distinct.Count == levels)
                        break;
                }
            }
            double q = distinct[0];
            for (int iteration = 0; iteration < 5; iteration++)
            {
                double numerator = 0, denominator = 0;
                foreach (double v in distinct)
                {
                    double n = Math.Max(1, Math.Round(v / q));
                    numerator += n * v;
                    denominator += n * n;
                }
                q = numerator / denominator;
            }
            double top = distinct[distinct.Count - 1];
            int low = 0, onMultiple = 0;
            foreach (double v in values)
            {
                if (v > top)
                    break;
                low++;
                if (Math.Abs(v / q - Math.Round(v / q)) <= ON_MULTIPLE)
                    onMultiple++;
            }
            fit = onMultiple / (double)low;
            return q;
        }
    }
}
