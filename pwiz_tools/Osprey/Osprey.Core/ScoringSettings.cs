/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// The scoring settings of a search that default by resolution and have no command-line
    /// option: <see cref="OspreyConfig.Scoring"/>. <see cref="OspreyConfig.ApplyResolutionMode"/>
    /// takes them from <see cref="ResolutionDefaults"/>, with each one a developer <c>OSPREY_*</c>
    /// variable sets in its place (<see cref="OspreyEnvironment.OverrideScoringSettings"/>), so the
    /// configuration describes the search and nothing reads the variables while scoring. Immutable,
    /// so a <see cref="OspreyConfig.ShallowClone"/> shares it safely.
    /// </summary>
    public sealed class ScoringSettings
    {
        public ScoringSettings(int xicFragments, double? corrFragmentTolerancePpm, bool refXicByCorrelation,
            int? localRtToleranceNeighbors, double? tcHalfWindowMinutes, bool gbtOutOfFold)
        {
            XicFragments = xicFragments;
            CorrFragmentTolerancePpm = corrFragmentTolerancePpm;
            RefXicByCorrelation = refXicByCorrelation;
            LocalRtToleranceNeighbors = localRtToleranceNeighbors;
            TcHalfWindowMinutes = tcHalfWindowMinutes;
            GbtOutOfFold = gbtOutOfFold;
        }

        /// <summary>
        /// How many of a candidate's most intense library fragments the search extracts XICs for
        /// (OSPREY_XIC_FRAGMENTS): the input of the CWT peak detection and of the coelution,
        /// peak-shape and median-polish features. Calibration and the 2-of-top-6 prefilter keep 6
        /// (<c>TopFragmentExtractor.CAL_TOP_N_FRAGMENTS</c>) either way.
        /// </summary>
        public int XicFragments { get; }

        /// <summary>
        /// The tolerance in ppm the correlation-shape features re-extract their fragment XICs with,
        /// as max(calibrated, setting), over the same scans, or null for none
        /// (OSPREY_CORR_FRAGMENT_TOLERANCE). Peak detection, the apex, the intensity-pattern and the
        /// mass-error features stay on the XICs at the calibrated tolerance.
        /// </summary>
        public double? CorrFragmentTolerancePpm { get; }

        /// <summary>
        /// True to choose the reference fragment XIC inside the chosen peak as the fragment with the
        /// largest sum of correlations to the other fragments there, in place of the fragment with the
        /// largest total intensity over the whole extraction window (OSPREY_REF_XIC). The reference
        /// sets the apex scan (and so every apex-spectrum feature), the recomputed bounds area and SNR,
        /// the MS1 correlation reference, and the apex/area of the CWT candidates; the peak_apex,
        /// peak_area and peak_sharpness features keep their own most-intense-fragment reference.
        /// </summary>
        public bool RefXicByCorrelation { get; }

        /// <summary>
        /// The calibration points nearest in library RT whose residual spread widens the RT search
        /// window at each library RT (3 * 1.4826 * median |residual|, clamped between the global
        /// half-width and the maximum RT tolerance), or null for the global window alone
        /// (OSPREY_LOCAL_RT_TOLERANCE). Aimed at a library whose predicted RTs saturate at the
        /// gradient end, where many precursors share a library RT but elute minutes apart.
        /// </summary>
        public int? LocalRtToleranceNeighbors { get; }

        /// <summary>
        /// The half-width in minutes of the window the tc_* time-correlation features correlate over,
        /// or null for their own <see cref="OspreyEnvironment.LEGACY_TC_HALF_WINDOW_MINUTES"/>
        /// (OSPREY_TC_HALF_WINDOW).
        /// </summary>
        public double? TcHalfWindowMinutes { get; }

        /// <summary>
        /// True to score each gbdt first-pass training row with only the fold model that held it out,
        /// as the cross-validated direct path does, instead of the average of every fold model (two of
        /// which trained on it) (OSPREY_GBT_OOF). The tree model only. Keyed under gbdt (the
        /// <c>;gbtoof=1</c> term of <c>PercolatorEngine.GbdtValidityKeySuffix</c>); the held-out folds
        /// are not persisted with the first-pass model, so a resumed first pass retrains rather than
        /// adopt the saved model. Known limitation: the second-pass rescoring and transfer
        /// (FrozenModelScorer, PerFileRescoreTask) still score training rows with the fold mean.
        /// </summary>
        public bool GbtOutOfFold { get; }
    }
}
