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

using System;
using System.Collections.Generic;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Supplies coelution scoring with the MS2 spectra of one isolation window at
    /// a time, already MS2-calibrated, keyed by the same
    /// <c>(int)Math.Round(center * 10.0)</c> the in-memory grouping has always
    /// used. This is the seam that lets <see cref="ScoringPipeline"/>.RunCoelutionScoring
    /// be fed either the whole resident spectra list
    /// (<see cref="ResidentWindowSpectraProvider"/>) or a streaming, load-on-demand
    /// source (the per-file streaming path wraps SpectraWindowIndex). Both return
    /// byte-for-byte identical window spectra, so scoring output is unchanged.
    /// </summary>
    public interface IWindowSpectraProvider
    {
        /// <summary>
        /// The MS2-calibrated spectra for one window key, in file order (before
        /// <see cref="CoelutionScorer.ScoreWindow"/>'s deterministic
        /// (RetentionTime, ScanNumber) re-sort). Returns an empty list for an
        /// absent key -- identical to the in-memory grouping's dictionary miss.
        /// </summary>
        List<Spectrum> GetCalibratedWindow(int windowKey);

        /// <summary>
        /// Every MS2 spectrum's retention time, in file order. The double-counting
        /// dedup needs only this RT multiset (it sorts internally), so it is the
        /// sole spectra dependency that survives once scoring is streamed.
        /// </summary>
        IReadOnlyList<double> Ms2RetentionTimes { get; }
    }

    /// <summary>
    /// The resident <see cref="IWindowSpectraProvider"/>: it applies MS2
    /// calibration to a local copy of the full spectra list and groups it by
    /// window key up front, exactly as <see cref="ScoringPipeline"/>.RunCoelutionScoring
    /// did inline before the streaming refactor. Used by the Stage-6 rescore /
    /// gap-fill passes (which re-score one shared spectra list repeatedly) and as
    /// the Stage-4 fallback when a streaming index cannot be built.
    /// </summary>
    public class ResidentWindowSpectraProvider : IWindowSpectraProvider
    {
        private readonly Dictionary<int, List<Spectrum>> _spectraByWindowKey;

        /// <param name="spectra">The full resident MS2 list.</param>
        /// <param name="ms2Calibration">MS2 mass calibration; when calibrated, a
        /// local calibrated copy is built (the input is never mutated except the
        /// gated <paramref name="consumeInputMzs"/> free below).</param>
        /// <param name="consumeInputMzs">When true, free each input spectrum's raw
        /// m/z array as its calibrated copy is built, so the two ~4 GB copies never
        /// coexist. Only the single Stage-4 caller passes true; the Stage-6 rescore
        /// loop calls scoring repeatedly on ONE shared list, so it must NOT set this
        /// (the next call still reads the raw m/z).</param>
        public ResidentWindowSpectraProvider(List<Spectrum> spectra,
            MzCalibrationResult ms2Calibration, bool consumeInputMzs)
        {
            // Capture RTs up front (RetentionTime is never freed by consumeInputMzs).
            var rts = new double[spectra.Count];
            for (int i = 0; i < spectra.Count; i++)
                rts[i] = spectra[i].RetentionTime;
            Ms2RetentionTimes = rts;

            // Apply MS2 calibration to a LOCAL copy of the spectra list, mirroring
            // Rust run_search which builds calibrated_spectra and operates on it.
            // Do NOT mutate the input parameter: the Stage 6 rescore loop scores the
            // same list multiple times, and an in-place offset would accumulate
            // (mz - mean -> mz - 2*mean -> ...) and mismatch fragments on later calls.
            List<Spectrum> calibratedSpectra;
            if (ms2Calibration.Calibrated)
            {
                calibratedSpectra = new List<Spectrum>(spectra.Count);
                for (int si = 0; si < spectra.Count; si++)
                {
                    var s = spectra[si];
                    double[] correctedMzs = new double[s.Mzs.Length];
                    for (int mi = 0; mi < s.Mzs.Length; mi++)
                        correctedMzs[mi] = MzCalibration.ApplyCalibration(s.Mzs[mi], ms2Calibration);
                    calibratedSpectra.Add(new Spectrum
                    {
                        ScanNumber = s.ScanNumber,
                        RetentionTime = s.RetentionTime,
                        PrecursorMz = s.PrecursorMz,
                        IsolationWindow = s.IsolationWindow,
                        Mzs = correctedMzs,
                        Intensities = s.Intensities
                    });
                    // Intensities are shared into the copy, so keep them; only the
                    // raw m/z is now dead for the single-scoring Stage-4 caller.
                    if (consumeInputMzs)
                        s.Mzs = null;
                }
            }
            else
            {
                // No calibration -> alias the input list (no copy).
                calibratedSpectra = spectra;
            }

            // Group spectra by isolation window center (rounded key) for lookup.
            _spectraByWindowKey = new Dictionary<int, List<Spectrum>>();
            foreach (var spectrum in calibratedSpectra)
            {
                int key = (int)Math.Round(spectrum.IsolationWindow.Center * 10.0);
                if (!_spectraByWindowKey.TryGetValue(key, out var list))
                {
                    list = new List<Spectrum>();
                    _spectraByWindowKey[key] = list;
                }
                list.Add(spectrum);
            }
        }

        public IReadOnlyList<double> Ms2RetentionTimes { get; }

        public List<Spectrum> GetCalibratedWindow(int windowKey)
        {
            return _spectraByWindowKey.TryGetValue(windowKey, out var list)
                ? list
                : new List<Spectrum>();
        }
    }
}
