/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
    // Per-candidate peak data presented to the feature calculators, as a four-level
    // tier hierarchy of increasing data access. A calculator implements against the
    // narrowest tier it needs, so the type system records each score's data
    // dependency and the harness presents each score with no more than it requires.
    //
    // The first two tiers mirror Skyline's ISummaryPeakData / IDetailedPeakData; the
    // top two expose spectral data Skyline's chromatogram-centric results layer does
    // NOT provide at scoring time, in two honest levels:
    //
    //   IOspreySummaryPeakData       identity + chosen peak location (stats only)
    //   IOspreyDetailedPeakData      + per-fragment XICs                          (Skyline-achievable)
    //   IOspreyApexSpectrumPeakData  + the single apex MS2 spectrum               (one level above Skyline)
    //   IOspreyApexSpectraPeakData   + the apex +/- 2 MS2 spectra                 (two levels above Skyline)

    /// <summary>
    /// Summary per-candidate peak data, mirroring Skyline's <c>ISummaryPeakData</c>:
    /// the identity and the chosen peak location a feature scores against. Osprey's
    /// scoring is flat -- one precursor (<see cref="Candidate"/>) at its best peak
    /// (<see cref="PeakBounds"/>) within one isolation window -- so this is a flat
    /// view, not Skyline's peptide/group/transition tree. The rt-deviation family
    /// (rt_deviation / abs_rt_deviation) reads only this tier.
    /// </summary>
    public interface IOspreySummaryPeakData
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
    /// Detailed per-candidate peak data, mirroring Skyline's <c>IDetailedPeakData</c>:
    /// adds the per-fragment extracted-ion chromatograms on the shared scan axis. The
    /// coelution, peak-shape, and median-polish families read this tier (the
    /// median-polish fit is an XIC-derived byproduct the harness publishes). This is
    /// the richest tier Skyline's results layer can currently supply.
    /// </summary>
    public interface IOspreyDetailedPeakData : IOspreySummaryPeakData
    {
        /// <summary>
        /// Per-fragment XICs (FragmentIndex, RetentionTimes, Intensities) on the
        /// shared scan axis. The full set is iterated to select the reference XIC.
        /// </summary>
        IReadOnlyList<XicData> Xics { get; }

        /// <summary>
        /// The MS1 precursor-intensity chromatogram the harness produced for this
        /// candidate: the precursor m/z sampled at the nearest MS1 scan along the
        /// peak (missing MS1 scans skipped, so its length is the number of present
        /// MS1 scans, not the peak width). <c>null</c> for unit-resolution runs / no
        /// MS1 / a too-short peak. The ms1_precursor_coelution feature correlates it
        /// against <see cref="Ms1ReferenceXic"/>. Mirrors how Skyline produces MS1
        /// chromatograms upstream so the score is a pure consumer.
        /// </summary>
        XicData Ms1PrecursorXic { get; }

        /// <summary>
        /// The reference fragment XIC co-sampled at the SAME retained MS1 scans as
        /// <see cref="Ms1PrecursorXic"/> (the MS1-specific reference selection:
        /// highest-total-intensity fragment, last-on-tie). The partner the
        /// ms1_precursor_coelution Pearson correlation runs against. <c>null</c>
        /// whenever <see cref="Ms1PrecursorXic"/> is.
        /// </summary>
        XicData Ms1ReferenceXic { get; }

        /// <summary>
        /// The observed isotope-envelope intensities at the apex MS1 scan (the
        /// <see cref="IsotopeEnvelope"/> extraction the harness ran for this
        /// candidate). <c>null</c> when there is no apex MS1 scan. The
        /// ms1_isotope_cosine feature applies the M0 gate and cosines it against the
        /// theoretical envelope. This part has no Skyline analog yet.
        /// </summary>
        double[] ApexIsotopeEnvelope { get; }
    }

    /// <summary>
    /// Apex-spectrum per-candidate peak data: adds the single MS2 spectrum at the
    /// chosen peak apex. This is the first tier beyond what Skyline's
    /// chromatogram-centric results layer can supply -- a Skyline implementation
    /// would throw on <see cref="ApexSpectrum"/>. The xcorr feature and the
    /// apex-match family (consecutive_ions, explained_intensity, the two
    /// mass-accuracy means) read this tier: they match the candidate's theoretical
    /// fragments against the apex spectrum's sorted m/z and index-aligned intensity
    /// arrays.
    /// </summary>
    public interface IOspreyApexSpectrumPeakData : IOspreyDetailedPeakData
    {
        /// <summary>
        /// The MS2 spectrum at the chosen peak apex
        /// (windowSpectra[startScan + bestPeak.ApexIndex]). The spectral surface
        /// Skyline's chromatogram-centric results layer cannot supply.
        /// </summary>
        Spectrum ApexSpectrum { get; }

        /// <summary>
        /// Window-global spectrum index of the chosen apex
        /// (= WindowStartIndex + candidate-local apex). The xcorr feature indexes the
        /// preprocessed cache HERE. INDEX TRAP: this is a distinct index space from
        /// the Savitzky-Golay sweep's candidate-local index -- see
        /// <see cref="IOspreyApexSpectraPeakData"/>.
        /// </summary>
        int ApexGlobalIndex { get; }
    }

    /// <summary>
    /// Apex-spectra per-candidate peak data: adds bounded access to the apex +/- 2 MS2
    /// spectra (five scans) via <see cref="TryGetApexOffsetSpectrum"/>. This is the
    /// widest spectral access -- two levels beyond Skyline -- and is read only by the
    /// Savitzky-Golay sweep (sg_weighted_xcorr / sg_weighted_cosine), which weights the
    /// per-scan xcorr / cosine over the apex and its two neighbors on each side. (The
    /// MS1 family does NOT ride this tier: its precursor XIC + isotope envelope are
    /// produced upstream and exposed on <see cref="IOspreyDetailedPeakData"/>.)
    /// </summary>
    public interface IOspreyApexSpectraPeakData : IOspreyApexSpectrumPeakData
    {
        /// <summary>
        /// Get the MS2 spectrum at the given offset from the peak apex. The
        /// Savitzky-Golay sweep reads offsets -2..+2; this bounded accessor is the
        /// ONLY spectral reach beyond the apex the scoring contract grants (it
        /// replaces direct exposure of the whole window spectrum list, so a
        /// calculator cannot read an arbitrary scan).
        ///
        /// Returns <c>false</c> at a window edge -- where the candidate-local index
        /// (apex + <paramref name="offset"/>) falls outside [0, scoring-range) -- which
        /// is the asymmetric boundary skip the sweep relies on (out-of-range offsets
        /// contribute nothing, with no renormalization). On success
        /// <paramref name="cacheIndex"/> is the window-global spectrum index the
        /// preprocessed-xcorr cache is indexed by; at offset 0 it equals
        /// <see cref="IOspreyApexSpectrumPeakData.ApexGlobalIndex"/> by construction.
        /// The accessor owns the candidate-local/window-global index mapping (the
        /// former "index trap"), so calculators no longer juggle the two index spaces.
        /// </summary>
        bool TryGetApexOffsetSpectrum(int offset, out Spectrum spectrum, out int cacheIndex);
    }
}
