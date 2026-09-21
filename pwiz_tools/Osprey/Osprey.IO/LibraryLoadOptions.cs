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

        /// <summary>
        /// Retain fragment peaks ONLY for these base_ids. Null (the default) retains everything.
        ///
        /// <para><b>The contract is equivalence, not leanness.</b> Loading with this set must
        /// leave the library in EXACTLY the state that loading everything and then calling
        /// <c>LibraryFragmentRelease.ReleaseFragments</c> with the same set would leave - a
        /// direct swap, differing only in what was allocated on the way. So a skipped entry is
        /// RELEASED (<see cref="pwiz.Osprey.Core.LibraryEntry.ReleaseSpectrum"/>), not left
        /// holding an empty array. That is not a detail: an empty spectrum is readable, and
        /// every scorer's <c>Fragments == null || Fragments.Count == 0</c> guard absorbs it as
        /// "this entry has no spectrum" and scores a degenerate zero, where a released one
        /// throws. Skipping the allocation must not also skip the tripwire that says the skip
        /// was wrong. <c>IOTest.TestLibraryCacheRetainMatchesRelease</c> pins the two states equal
        /// entry by entry.</para>
        ///
        /// <para>Honoured on the CACHE arm only, where the skip costs exactly what
        /// <c>SkipFragment</c> already pays to advance the stream and nothing is allocated. The
        /// source-parse arm ignores it, for two reasons that point the same way: it has already
        /// built every fragment by the time it could act, and the ids are not final there -
        /// pairing rewrites a supplied decoy's Id AFTER the load returns, so filtering on
        /// <c>entry.Id</c> would test a parse-order id against a set of final base_ids. A
        /// source-parsed library stays fat until the Stage 7 release drops the same set.</para>
        ///
        /// <para>This differs from <see cref="OmitFragments"/> in more than degree.
        /// <c>OmitFragments</c> leaves the documented readable-empty state and has no
        /// load-and-release counterpart to match: <c>LibraryFragmentRelease</c> refuses the
        /// <c>StopAfterStage5</c> leg outright, because releasing there would swap one shared
        /// singleton for another and report millions released having freed nothing.</para>
        ///
        /// <para>The alternative is to build every entry's peaks and then hand them back:
        /// <c>LibraryFragmentRelease</c> walks the whole library to release the ones no later
        /// stage will score - ~1m54s over 6,175,389 entries on the CHS cohort, releasing
        /// 4,924,513 of them. That cost is O(library), so it is the same two minutes whether the
        /// analysis has 3 runs or 4,000, and after the 2026-09-03 resume work it was 66% of the
        /// entire resume startup.</para>
        ///
        /// <para>Set this only where the retained set is already KNOWN before the library is
        /// read - the rescore and SecondPassFDR paths, where FirstPassFDR has already written
        /// <c>&lt;blib-stem&gt;.1st-pass.retained_base_ids.bin</c> (2.98 MB at 446 runs). A fresh
        /// run must leave it null: it needs every entry's peaks to score against, and the
        /// retained set does not exist yet.</para>
        /// </summary>
        public HashSet<uint> RetainFragmentsFor { get; set; }
    }
}
