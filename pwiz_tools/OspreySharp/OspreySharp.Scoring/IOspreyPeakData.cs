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

using System.Collections.Generic;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Per-candidate peak data, mirroring Skyline's <c>ISummaryPeakData</c>: the
    /// identity and the chosen peak location a feature scores against. Osprey's
    /// scoring is flat -- one precursor (<see cref="Candidate"/>) at its best peak
    /// (<see cref="PeakBounds"/>) within one isolation window -- so this is a flat
    /// view, not Skyline's peptide/group/transition tree.
    /// </summary>
    public interface IOspreyPeakData
    {
        /// <summary>The candidate library entry: precursor plus theoretical fragments.</summary>
        LibraryEntry Candidate { get; }

        /// <summary>
        /// The chosen peak boundaries (apex/start/end indices into the shared XIC
        /// scan axis), already resolved upstream (override or detected).
        /// </summary>
        XICPeakBounds PeakBounds { get; }

        /// <summary>
        /// Retention time of the apex MS2 spectrum (the chosen peak apex). Mirrors
        /// Skyline's <c>ISummaryPeakData.RetentionTime</c>.
        /// </summary>
        double ApexRetentionTime { get; }

        /// <summary>
        /// The expected (calibration-predicted) retention time the harness computed
        /// once for this candidate; feeds rt_deviation. Not recomputed per feature.
        /// </summary>
        double ExpectedRt { get; }
    }

    /// <summary>
    /// Detailed per-candidate peak data, mirroring Skyline's
    /// <c>IDetailedPeakData</c>: adds the per-fragment extracted-ion chromatograms
    /// on the shared scan axis. Spectral accessors (apex / MS1 spectra) -- the part
    /// Skyline's chromatogram-centric results layer cannot supply -- are added by
    /// the feature families that read them.
    /// </summary>
    public interface IOspreyDetailedPeakData : IOspreyPeakData
    {
        /// <summary>
        /// Per-fragment XICs (FragmentIndex, RetentionTimes, Intensities) on the
        /// shared scan axis. The full set is iterated to select the reference XIC.
        /// </summary>
        IReadOnlyList<XicData> Xics { get; }

        /// <summary>
        /// The MS2 spectrum at the chosen peak apex
        /// (windowSpectra[startScan + bestPeak.ApexIndex]). This is the spectral
        /// surface Skyline's chromatogram-centric results layer cannot supply -- a
        /// Skyline implementation of this interface would throw here. The
        /// apex-match family matches the candidate's theoretical fragments against
        /// this spectrum's sorted m/z and index-aligned intensity arrays.
        /// </summary>
        Spectrum ApexSpectrum { get; }

        /// <summary>
        /// Window-global spectrum index of the chosen apex
        /// (= <see cref="WindowStartIndex"/> + <see cref="ApexLocalIndex"/>). The
        /// xcorr feature indexes the preprocessed cache HERE. INDEX TRAP: this is a
        /// distinct index space from the Savitzky-Golay sweep's candidate-local
        /// index -- see <see cref="ApexLocalIndex"/> / <see cref="WindowLength"/>.
        /// </summary>
        int ApexGlobalIndex { get; }

        /// <summary>
        /// Candidate-local apex index within the scoring range
        /// (= bestPeak.ApexIndex). The SG sweep builds candIdx = ApexLocalIndex +
        /// offset and bound-checks it against [0, <see cref="WindowLength"/>).
        /// </summary>
        int ApexLocalIndex { get; }

        /// <summary>
        /// startScan: the offset mapping a candidate-local index to a window-global
        /// index (globalIdx = WindowStartIndex + candIdx). Kept distinct from
        /// <see cref="ApexGlobalIndex"/>; at offset 0 they coincide by construction.
        /// </summary>
        int WindowStartIndex { get; }

        /// <summary>
        /// rangeLen = endScan - startScan + 1. The SG sweep bounds the
        /// candidate-local index against THIS, NOT <see cref="WindowSpectra"/>.Count.
        /// </summary>
        int WindowLength { get; }

        /// <summary>
        /// The window's MS2 spectra (sorted by RT). The SG sweep reads
        /// WindowSpectra[globalIdx] for each apex+/-2 offset.
        /// </summary>
        IReadOnlyList<Spectrum> WindowSpectra { get; }

        /// <summary>
        /// The candidate-local scan retention-time axis: ScanRetentionTimes[i] is the
        /// RT of XIC scan index i (= windowRts[startScan + i]). The MS1 family
        /// (features 13, 14) maps an XIC index to an absolute RT to find the nearest
        /// MS1 scan, so this hides the window/startScan offset arithmetic from the
        /// calculator. Precomputed once per candidate in <c>Set</c>; this is just RT,
        /// not a spectral surface, so unlike <see cref="ApexSpectrum"/> a
        /// chromatogram-centric implementation can supply it.
        /// </summary>
        IReadOnlyList<double> ScanRetentionTimes { get; }
    }
}
