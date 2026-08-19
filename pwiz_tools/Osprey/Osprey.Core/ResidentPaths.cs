/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// The high-water mark for O(files) RESIDENT first-pass pool paths: every path we have NOT
    /// yet streamed, named, so that a run may be granted exactly one of them via
    /// <c>OSPREY_ALLOW_UNFIXED_RESIDENT=&lt;token&gt;</c> and nothing else.
    ///
    /// <para>This list may SHRINK as paths are streamed. It must not GROW. Growing it means a
    /// path we believed bounded is resident again, which is a defect to fix rather than to
    /// admit - so <c>ResidentPoolGuardTest</c> pins the contents exactly, and any addition
    /// shows up in review as the ratchet running backwards instead of as an ambient
    /// environment variable somebody set months ago.</para>
    ///
    /// <para>Why named tokens instead of the former blanket <c>OSPREY_ALLOW_UNBOUNDED_MEMORY=1</c>:
    /// a boolean grants amnesty to every trigger at once, so it cannot distinguish "the one path
    /// we know is unfixed" from "a path that silently regressed". It did not: with the boolean in
    /// place, <c>OSPREY_PASS2_QVALUE=transfer</c> regressed back onto the resident pool for ten
    /// days unnoticed, because setting the boolean was routine. An unlisted path now errors even
    /// when the variable is set.</para>
    /// </summary>
    public static class ResidentPaths
    {
        // hpc-merge was removed here by #4486, which put the HPC reconciled-input merge
        // (--task SecondPassFDR) on the same file-count-bounded streaming hydrate every other
        // reconciled-bundle path already used. It never named a real consumer: FirstPassFdrTask
        // is excluded on that node, and each pass-2 consumer streams from disk one file at a
        // time, so the pool it forced was loaded and discarded - a measured 2.21 GB/file
        // (7.6 GB after file 1 to 40.8 GB after file 16), i.e. ~186 GB projected at 82 files,
        // which made the final join impossible on any HPC node. Not to
        // be re-added: a join that cannot stream its input is a defect to fix, not a path to
        // name.

        /// <summary>
        /// <c>--fdrbench-pass 1</c>, which reads the full pre-compaction first-pass pool
        /// (decoys + entrapment, with scores) - exactly what the projection path drops.
        /// </summary>
        public static readonly string FDRBENCH_PASS1 = @"fdrbench-pass1";

        // mdiag-full-resume is GONE (#4505), and this note is the record of the ratchet
        // shrinking rather than a gap. --model-diagnostics on a full resume took the resident
        // pool because FirstPassFDR skipped its score pass (every 1st-pass sidecar already on
        // disk), so the streaming accumulator was never fed and the report fell back to the
        // batch write over the resident entries. FirstPassFDR's rehydrate now feeds that
        // accumulator from the per-file load it already performs, off the same PRE-compaction
        // rows, so the flag arms no resident path at any file count and no token can name one.

        /// <summary>
        /// A non-Percolator <c>FdrMethod</c> (Simple / Mokapot), which does not use the
        /// projection framework at all. By design rather than unfinished work, but it still
        /// takes the resident path and so must be named to be allowed.
        /// </summary>
        public static readonly string NON_PERCOLATOR_FDR = @"non-percolator-fdr";

        /// <summary>
        /// <c>OSPREY_FDR_PROJECTION=0</c>: the operator explicitly forced the legacy
        /// <c>FdrEntry</c>-buffer implementation, so the whole run is resident by request. This
        /// is the A/B byte-identity oracle that proves the streaming path did not change
        /// results, and it is deliberately COARSE - naming it exempts the run wholesale, because
        /// residency is the thing being asked for rather than a symptom to diagnose.
        ///
        /// <para>It still has to be named. The previous blanket bypass let this switch silently
        /// exempt every OTHER resident trigger too, which is the same masking property that hid
        /// the transfer regression. This entry leaves the list last: it can only go when the
        /// legacy path itself does, which needs #4507 (FDRBench pass 1) first.</para>
        /// </summary>
        public static readonly string PROJECTION_OFF = @"projection-off";

        /// <summary>
        /// <c>OSPREY_STAGE6_STREAM_SURVIVORS=0</c>: the operator forced the resident
        /// POST-compaction handoff, where the all-files survivor buffer built by Stage 5 stays
        /// live across the whole Stage 6 rescore - 88.9 M entries / 28 GB at 163 files, held
        /// for 5.5 hours, growing super-linearly in file count because the passing base_id set
        /// grows too (issue #4526). Like <see cref="PROJECTION_OFF"/> this is the A/B
        /// byte-identity oracle for the streamed default, and it is named for the same reason.
        ///
        /// <para>This entry ADDS to the list, which the class remarks say must only shrink.
        /// The justification is that it names a path which was previously unnamed rather than
        /// re-admitting one that had been fixed: the older guard's invariant stops at the
        /// compaction line - it enforces "no unnamed PRE-compaction pool", and this buffer is
        /// built after it, so no token could refuse it and none was required. Naming it is the
        /// ratchet reaching further, not running backwards. It goes when the resident handoff
        /// itself goes.</para>
        /// </summary>
        public static readonly string COMPACTED_ENTRIES_BUFFER = @"compacted-entries-buffer";

        // resume-survivor-handoff was removed here by #4536, which gave FirstPassFdrTask's
        // rehydrate its own per-file survivor loader: the arm streams like any computed run, so
        // the token had nothing left to admit. Not to be re-added - a resume that cannot stream
        // the Stage 6 handoff is a defect to fix, not a path to name.

        /// <summary>
        /// Every legal <c>OSPREY_ALLOW_UNFIXED_RESIDENT</c> value. Pinned by
        /// <c>ResidentPoolGuardTest</c> - see the class remarks for why it may only shrink.
        /// </summary>
        public static readonly IReadOnlyList<string> KNOWN_UNFIXED = new[]
        {
            FDRBENCH_PASS1, NON_PERCOLATOR_FDR, PROJECTION_OFF, COMPACTED_ENTRIES_BUFFER
        };
    }
}
