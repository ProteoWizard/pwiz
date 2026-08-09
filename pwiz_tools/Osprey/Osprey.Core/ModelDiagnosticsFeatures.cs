/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.Linq;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// The named <c>--model-diagnostics</c> panels that cost real time or memory to compute, and
    /// are therefore OPT-IN: <c>--model-diagnostics &lt;token&gt;[,&lt;token&gt;...]</c>. A bare
    /// <c>--model-diagnostics</c> writes the standard report and nothing here, so every existing
    /// invocation keeps its old cost.
    ///
    /// <para><b>Named tokens rather than a level.</b> This follows the reasoning
    /// <see cref="ResidentPaths"/> already records for
    /// <c>OSPREY_ALLOW_UNFIXED_RESIDENT</c>: a single blanket switch grants everything at once, so
    /// it cannot distinguish "the expensive panel I asked for" from "two more that were added
    /// since". A <c>--model-diagnostics full</c> level would be that same blanket switch one notch
    /// up - it would silently start doing more work each time a panel is added, which is exactly
    /// the property that let a resident regression sit unnoticed for ten days under the old
    /// boolean. A token names what you are paying for, and it appears verbatim in the run log.</para>
    ///
    /// <para><b>Not the same ratchet, though.</b> <see cref="ResidentPaths"/> is amnesty for known
    /// defects and may only SHRINK. This list is the opposite: it is a menu of working features and
    /// is expected to GROW as panels are added. Same shape, opposite direction - which is why it
    /// lives here rather than as more entries on that class.</para>
    /// </summary>
    public static class ModelDiagnosticsFeatures
    {
        /// <summary>
        /// Single-peak multiple-ID co-assignment (issue #4522): how often a detected precursor
        /// sits on a peak a better-scoring same-m/z precursor already explains, for targets,
        /// entrapment and decoys.
        ///
        /// <para>Opt-in because pass 1 has to RECOVER the detection apex RT the lean first pass
        /// does not carry, by re-reading two columns (<c>entry_id</c>, <c>apex_rt</c>) of every
        /// <c>.scores.parquet</c> and joining them to that file's FDR sidecar. That is a second
        /// pass over every pre-compaction row - ~340M of them on an 82-file Astral run - and it
        /// buys a panel most runs do not need. Pass 2 is nearly free by comparison (the reported
        /// pool is already resident and already carries apex RT), but the two are one token: a
        /// panel that silently covered only the pass it could afford would be worse than one that
        /// is absent.</para>
        /// </summary>
        public static readonly string PEAK_COASSIGNMENT = @"peak-coassignment";

        /// <summary>
        /// Every legal token. Order is the order they are offered in help and error text.
        /// </summary>
        public static readonly IReadOnlyList<string> ALL_TOKENS = new[]
        {
            PEAK_COASSIGNMENT,
        };

        /// <summary>
        /// The catch-all value. Deliberately NOT a stable contract: it means "whatever expensive
        /// panels this build has", so a run scripted with <c>all</c> can get slower when Osprey is
        /// upgraded. Scripts that care about cost should name their tokens.
        /// </summary>
        public const string ALL = @"all";

        /// <summary>
        /// Parse the optional <c>--model-diagnostics</c> value into the set of enabled panels.
        /// Null or empty (the bare flag) yields an empty set - the standard report only.
        /// </summary>
        /// <param name="value">Comma-separated tokens, or <see cref="ALL"/>, or null for the bare flag.</param>
        /// <exception cref="ArgumentException">
        /// An unrecognized token. Deliberately a hard error listing the legal values rather than a
        /// silent skip: a typo'd panel name would otherwise look exactly like a panel that had
        /// nothing to report, and the user would be waiting for output that never comes.
        /// </exception>
        public static HashSet<string> Parse(string value)
        {
            var enabled = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(value))
                return enabled;
            foreach (string raw in value.Split(','))
            {
                string token = raw.Trim().ToLowerInvariant();
                if (token.Length == 0)
                    continue;
                if (string.Equals(token, ALL, StringComparison.Ordinal))
                {
                    foreach (string t in ALL_TOKENS)
                        enabled.Add(t);
                    continue;
                }
                if (!ALL_TOKENS.Contains(token))
                {
                    throw new ArgumentException(string.Format(
                        @"Unknown --model-diagnostics value '{0}'. Legal values: {1}, or '{2}' for all of them. Omit the value for the standard report.",
                        raw.Trim(), string.Join(@", ", ALL_TOKENS), ALL));
                }
                enabled.Add(token);
            }
            return enabled;
        }
    }
}
