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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Collapses duplicate per-entry strings of a resident spectral library to
    /// a single shared instance. A large library repeats the same protein
    /// accessions, gene names, stripped sequences, and modification names across
    /// millions of entries (one protein maps to many peptides; one stripped
    /// sequence spans many charge/modification states), so the distinct string
    /// count is far smaller than the total. Sharing one instance per distinct
    /// value drops the per-duplicate string object (header + chars) from the
    /// resident set.
    ///
    /// Interning happens DURING entry construction: a loader (or the decoy
    /// generator) creates one pool per load call and routes every string it
    /// emits (Sequence, ModifiedSequence, each Modification.Name, and every
    /// protein / gene accession) through <see cref="Intern"/> as the interned
    /// arrays are filled, so no member is mutated after assignment. The pool is
    /// a plain single-threaded dictionary built and released within one load
    /// call -- unlike the concurrent per-observation interning that was a net
    /// loss on the FDR path, this runs once over the library and only the shared
    /// instances survive. Values are unchanged (only object identity), so output
    /// stays byte-identical.
    ///
    /// Lives in Core (not IO) so both the format loaders in
    /// <c>Osprey.IO</c> and the decoy generator in <c>Osprey.Scoring</c> can
    /// intern as they build entries.
    /// </summary>
    public sealed class LibraryStringInterner
    {
        private readonly Dictionary<string, string> _pool =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private long _totalRefs;

        /// <summary>
        /// Return the shared instance for <paramref name="s"/>: the first call
        /// with a given value returns that value and remembers it; later calls
        /// with an equal value return the remembered instance. Null passes
        /// through unchanged.
        /// </summary>
        public string Intern(string s)
        {
            if (s == null)
                return null;
            _totalRefs++;
            string existing;
            if (_pool.TryGetValue(s, out existing))
                return existing;
            _pool[s] = s;
            return s;
        }

        /// <summary>
        /// Intern every element of <paramref name="items"/> and return them as a
        /// fresh array (null / empty -&gt; the shared empty array), preserving
        /// enumeration order. The array-backed form (rather than a <c>List</c>)
        /// drops the per-list growth slack, matching how the loaders and decoy
        /// generator fill <c>LibraryEntry</c> members. Values other than string
        /// identity are unchanged, so output stays byte-identical.
        /// </summary>
        public string[] InternToArray(ICollection<string> items)
        {
            if (items == null || items.Count == 0)
                return Array.Empty<string>();
            var result = new string[items.Count];
            int i = 0;
            foreach (var s in items)
                result[i++] = Intern(s);
            return result;
        }

        /// <summary>Number of distinct values the pool retains.</summary>
        public int DistinctCount { get { return _pool.Count; } }

        /// <summary>Total non-null <see cref="Intern"/> calls seen.</summary>
        public long TotalReferences { get { return _totalRefs; } }

        /// <summary>
        /// Log a one-line distinct/total summary of what this pool collapsed.
        /// No-op when <paramref name="logInfo"/> is null.
        /// </summary>
        public void LogSummary(Action<string> logInfo)
        {
            if (logInfo == null)
                return;
            long collapsed = _totalRefs - _pool.Count;
            double pct = _totalRefs > 0 ? 100.0 * collapsed / _totalRefs : 0.0;
            logInfo(string.Format(
                "Interned library strings: {0} distinct / {1} total ({2:F1}% collapsed)",
                _pool.Count, _totalRefs, pct));
        }
    }
}
