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

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Optional knobs for <see cref="LibraryLoader.Load(pwiz.Osprey.Core.OspreyConfig,LibraryLoadOptions,System.Action{string},System.Action{string})"/>.
    /// A carrier so callers with different needs (full pipeline vs. an
    /// FDR-only worker) can shape the load without a growing parameter list.
    /// </summary>
    public sealed class LibraryLoadOptions
    {
        /// <summary>
        /// Shared "no special handling" instance (a full load with every
        /// fragment peak retained). Immutable: never mutate this instance.
        /// </summary>
        public static readonly LibraryLoadOptions Default = new LibraryLoadOptions();

        /// <summary>
        /// When true, the loaded library keeps every entry's six identity
        /// scalars (Id, ModifiedSequence, Charge, PrecursorMz, IsDecoy,
        /// ProteinIds) but drops the per-entry <see cref="pwiz.Osprey.Core.LibraryEntry.Fragments"/>
        /// peak arrays -- the ~3.2 GB (SEA-AD scale) of fragment m/z + intensity
        /// that a FirstPassFDR / <c>StopAfterStage5</c> worker never reads (the
        /// FDR stages consume only the scalars). Set ONLY for a run that stops
        /// after Stage 5; a run that later writes the .blib needs the fragments.
        ///
        /// Byte-identity note: the loader still reads the fragment blocks (to
        /// count them for the min-fragment filter and to tie-break duplicates in
        /// <see cref="LibraryDeduplicator"/>) and writes them to the shared
        /// .libcache; only the RETAINED per-entry arrays are dropped, after all
        /// count-dependent work is done. Decoy generation is told about the
        /// omission separately so it skips its own fragment-count gate.
        /// </summary>
        public bool OmitFragments { get; set; }
    }
}
