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
using System.Threading;

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
    ///
    /// <para>A SECOND use outlives the load: the per-file score sidecars carry the same
    /// modified sequences the library does, and a parquet reader hands out a fresh string
    /// per row, so the FDR pool would otherwise hold one string object per observation
    /// (issue #4486). Those readers share the pool seeded from the LIBRARY, which is the
    /// whole point - a pool of their own would canonicalize them onto a second set of
    /// instances and duplicate every sequence rather than collapse it. Seeding first is
    /// what makes the library's instance the one that wins.</para>
    /// </summary>
    public sealed class LibraryStringInterner
    {
        private readonly Dictionary<string, string> _pool =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private long _totalRefs;
        private long _frozenMisses;
        private bool _frozen;

        /// <summary>
        /// Return the shared instance for <paramref name="s"/>: the first call
        /// with a given value returns that value and remembers it; later calls
        /// with an equal value return the remembered instance. Null passes
        /// through unchanged.
        ///
        /// <para>Once <see cref="Freeze"/> has been called this only LOOKS UP: a value the
        /// pool does not hold is returned unchanged instead of being added, which is what
        /// makes the frozen pool safe to read from many threads at once.</para>
        /// </summary>
        public string Intern(string s)
        {
            if (s == null)
                return null;
            string existing;
            if (_frozen)
            {
                // Lookup only - see Freeze. The miss counter is interlocked rather than ++
                // because frozen readers are concurrent, and it is touched ONLY on a miss,
                // which the seeding is expected to make vanishingly rare. A hit, the whole
                // hot path, stays a bare dictionary read.
                if (_pool.TryGetValue(s, out existing))
                    return existing;
                Interlocked.Increment(ref _frozenMisses);
                return s;
            }
            _totalRefs++;
            if (_pool.TryGetValue(s, out existing))
                return existing;
            _pool[s] = s;
            return s;
        }

        /// <summary>
        /// Stop the pool accepting new values, making it safe for concurrent readers.
        ///
        /// <para>A <see cref="Dictionary{TKey,TValue}"/> supports any number of concurrent
        /// READERS; it is a writer alongside them that corrupts it. The pool shared with the
        /// sidecar readers is seeded from the library and then frozen, so the per-file loads -
        /// which Stage 6 runs under <c>Parallel.For</c> - only ever look up. Freezing rather
        /// than locking is what keeps that free: interning is called once per observation,
        /// over a hundred million times, and a lock on that path was already measured as a
        /// net loss.</para>
        ///
        /// <para>The cost is that a value absent from the library is not pooled. That is the
        /// right trade here because there should be none: every sequence in a score sidecar
        /// was written from the library entry the candidate came from. <see cref="FrozenMisses"/>
        /// counts any that are, so the assumption is measured rather than asserted.</para>
        /// </summary>
        public void Freeze()
        {
            _frozen = true;
        }

        /// <summary>Whether <see cref="Freeze"/> has been called.</summary>
        public bool IsFrozen { get { return _frozen; } }

        /// <summary>
        /// Values looked up while frozen that the pool did not hold, and which were therefore
        /// returned un-pooled. Expected to be zero or near it; a large count means the seeding
        /// missed a source of sequences and the interning is not doing its job.
        /// </summary>
        public long FrozenMisses { get { return Interlocked.Read(ref _frozenMisses); } }

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
