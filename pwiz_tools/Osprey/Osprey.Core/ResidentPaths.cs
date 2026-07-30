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
        /// <summary>
        /// The HPC reconciled-input merge (<c>--task SecondPassFDR</c>), which loads every
        /// worker's entries to reconcile them. Tracked by issue #4486.
        /// </summary>
        public const string HPC_MERGE = @"hpc-merge";

        /// <summary>
        /// <c>--fdrbench-pass 1</c>, which reads the full pre-compaction first-pass pool
        /// (decoys + entrapment, with scores) - exactly what the projection path drops.
        /// </summary>
        public const string FDRBENCH_PASS1 = @"fdrbench-pass1";

        /// <summary>
        /// <c>--model-diagnostics</c> on a FULL resume, where the 1st-pass sidecars are already
        /// on disk so FirstJoin skips its score pass, the streaming accumulator is never fed,
        /// and the report falls back to the resident batch write. Tracked by issue #4505; the
        /// scale case was streamed by #4420, and a verified fix for this remainder is parked on
        /// the closed #4437 branch.
        /// </summary>
        public const string MDIAG_FULL_RESUME = @"mdiag-full-resume";

        /// <summary>
        /// A non-Percolator <c>FdrMethod</c> (Simple / Mokapot), which does not use the
        /// projection framework at all. By design rather than unfinished work, but it still
        /// takes the resident path and so must be named to be allowed.
        /// </summary>
        public const string NON_PERCOLATOR_FDR = @"non-percolator-fdr";

        /// <summary>
        /// Every legal <c>OSPREY_ALLOW_UNFIXED_RESIDENT</c> value. Pinned by
        /// <c>ResidentPoolGuardTest</c> - see the class remarks for why it may only shrink.
        /// </summary>
        public static readonly IReadOnlyList<string> KNOWN_UNFIXED = new[]
        {
            HPC_MERGE, FDRBENCH_PASS1, MDIAG_FULL_RESUME, NON_PERCOLATOR_FDR
        };
    }
}
