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
using System.Globalization;
using System.Linq;
using pwiz.Quasar.Features.Data;

namespace pwiz.Quasar.Features
{
    /// <summary>The peptide-like isotope envelopes of one spectrum, with their peaks, for labeling it.</summary>
    public static class PeptideLikeEnvelopes
    {
        /// <summary>Proton mass (Glimpse.Core.Chemistry.Mass.Proton, the value Skyline and pwiz use).</summary>
        private const double PROTON = 1.00727646688;

        /// <summary>
        /// The envelopes of scan <paramref name="scan"/> that belong to persistent features, with their peaks. The
        /// spectrum is searched again with <paramref name="finder"/> (about a millisecond): Hardklor is deterministic,
        /// so it finds the same envelopes, in the same order, as the run's search, and each is checked against the
        /// one the search linked before it is used. A search read from a .hk file keeps no peaks, so this is how its
        /// envelopes get them back. Empty when the scan was not searched or the envelopes found differ (a spectrum
        /// read another way, another finder).
        /// </summary>
        /// <param name="signal">The run's peptide-like signal.</param>
        /// <param name="scan">Index of the spectrum in the run.</param>
        /// <param name="mz">The spectrum's peak m/z values, ascending.</param>
        /// <param name="intensity">The spectrum's peak intensities.</param>
        /// <param name="analyzer">The analyzer that recorded the spectrum.</param>
        /// <param name="finder">The finder the run was searched with.</param>
        /// <param name="ms1MinCharge">Lowest charge an MS1 envelope may have (MS2 envelopes of every charge count).</param>
        public static IReadOnlyList<IsotopeEnvelope> Persistent(PeptideLikeSignal signal, int scan, double[] mz, double[] intensity,
            MassAnalyzer analyzer, IEnvelopeFinder finder, int ms1MinCharge = 1)
        {
            if (!signal.Covers(scan) || !signal.Envelopes.TryGetValue(scan, out var linked) || linked.Keys.Length == 0)
                return Array.Empty<IsotopeEnvelope>();
            var found = finder.Find(mz, intensity, analyzer);
            if (found.Count != linked.Keys.Length)
                return Array.Empty<IsotopeEnvelope>();
            for (var e = 0; e < found.Count; e++)
            {
                var key = linked.Keys[e];
                if (found[e].Charge != key.Charge || found[e].MonoMass != key.MonoMass || found[e].Intensity != key.Intensity)
                    return Array.Empty<IsotopeEnvelope>();
            }
            var ids = signal.FeatureOf[scan];
            var minCharge = signal.Headers[scan].MsLevel == 1 ? ms1MinCharge : 1;
            return found.Where((e, i) => ids[i] >= 0 && e.Charge >= minCharge).ToArray();
        }

        /// <summary>An envelope's monoisotopic m/z: its precursor m/z.</summary>
        public static double MonoisotopicMz(IsotopeEnvelope envelope) => (envelope.MonoMass + envelope.Charge * PROTON) / envelope.Charge;

        /// <summary>An envelope's label: its monoisotopic m/z to <paramref name="decimals"/> places, then its charge ("542.29 2+").</summary>
        public static string Label(IsotopeEnvelope envelope, int decimals) =>
            MonoisotopicMz(envelope).ToString(@"F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            + @" " + envelope.Charge.ToString(CultureInfo.InvariantCulture) + @"+";

        /// <summary>The index of an envelope's base peak in the spectrum (its matched peak nearest the base peak m/z).</summary>
        public static int BasePeakIndex(IsotopeEnvelope envelope, double[] mz) =>
            envelope.PeakIndices.MinBy(p => Math.Abs(mz[p] - envelope.BasePeakMz));
    }
}
