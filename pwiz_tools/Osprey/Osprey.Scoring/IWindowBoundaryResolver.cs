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

using System.Collections.Generic;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Chooses, inside the isolation window being scored, the boundary override of a Stage 6
    /// target whose new peak depends on that window's spectra: a moved peak re-picked at one of
    /// its CWT candidates, which are computed from the spectra the window has already loaded, so
    /// the rescore reads each window once.
    /// </summary>
    public interface IWindowBoundaryResolver
    {
        /// <summary>True when <paramref name="entryId"/>'s boundaries are chosen per window.</summary>
        bool Resolves(uint entryId);

        /// <summary>
        /// The (apex, start, end) RT boundaries to score <paramref name="entry"/> at in
        /// <paramref name="window"/>, from <paramref name="windowSpectra"/> - calibrated and in
        /// <see cref="CoelutionScorer.SortWindowSpectra"/> order - and their
        /// <paramref name="windowRts"/>. Called from the window's scoring thread.
        /// </summary>
        (double Apex, double Start, double End) Resolve(LibraryEntry entry, IsolationWindow window,
            List<Spectrum> windowSpectra, double[] windowRts);
    }
}
