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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Supplies coelution scoring with the MS2 spectra of one isolation window at
    /// a time, already MS2-calibrated, keyed by the same
    /// <c>(int)Math.Round(center * 10.0)</c> the in-memory grouping has always
    /// used. This is the seam that lets <see cref="ScoringPipeline"/>.RunCoelutionScoring
    /// pull each window from a streaming, load-on-demand source: the concrete
    /// implementation is Osprey.Tasks' StreamingWindowSpectraProvider, which decodes and
    /// calibrates one window from the .spectra.bin index per call (Stage-4 scoring and the
    /// Stage-6 rescore / gap-fill passes both stream through this seam), so scoring never
    /// materializes the whole resident MS2 list. The window spectra it returns are
    /// byte-for-byte identical to a full cache load, so scoring output is unchanged.
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
}
