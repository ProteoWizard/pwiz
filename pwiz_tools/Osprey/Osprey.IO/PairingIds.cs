/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using pwiz.BiblioSpec.IO;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Assigns every library entry its <see cref="LibraryEntry.PairingId"/> once, at load, so
    /// the pairing of an entrapment peptide with its original target lives in the library -
    /// the cache, a .blib's <c>DecoyPairs</c>, the export - and every consumer reads the entry
    /// rather than the pairing manifest file. Every entry gets an id. Whether an entrapment
    /// peptide's id pairs it with an original target is a property of its group, read by
    /// <see cref="ExcludedEntrapment"/> (for <c>EntrapmentPairing.Build</c>), not of the id.
    ///
    /// <para>An entry with a <c>DecoyPairs</c> row takes the row's id: the design as the
    /// library's writer recorded it. A peptide the manifest covers takes the manifest's
    /// <c>peptide_pair_index</c>. The library legitimately holds peptide forms the manifest
    /// never had - N-terminal-Met-clipped variants a predictor makes (Carafe <c>-clip_n_m</c>) -
    /// and a clipped peptide whose M-prefixed sequence is a manifest peptide takes a clip index
    /// above the manifest's range (one per original pair index), which a clipped entrapment
    /// shares with the clip of its manifest target when the library holds that clip as an
    /// extra; a clipped entrapment alone in its group is the known orphan the FDRBench input
    /// and the diagnostics exclude. Any other peptide - an extra that is not a clip form,
    /// every peptide of a library with neither table nor manifest (which the loader admits only
    /// when it has no entrapment peptides) - takes a standalone index. A decoy takes the index
    /// of its own sequence when the manifest covers it, else its target's, else a standalone
    /// one.</para>
    /// </summary>
    public static class PairingIds
    {
        // N-terminal methionine, prepended to reverse a Met clip.
        private const string MET_PREFIX = @"M";

        /// <summary>What the pairing excludes: for the load log.</summary>
        public sealed class Summary
        {
            /// <summary>Entrapment sequences with no target pair that are the known N-terminal-Met-clip artifact.</summary>
            public int MetClipDropped { get; set; }

            /// <summary>Entrapment sequences with no target pair and no known cause, surfaced rather than silently dropped.</summary>
            public List<string> Unexplained { get; } = new List<string>();
        }

        /// <summary>
        /// Sets <see cref="LibraryEntry.PairingId"/> on every entry of <paramref name="library"/>
        /// by the rules in the class summary: from <paramref name="tableRows"/> (a .blib's
        /// <c>DecoyPairs</c> rows by entry; null for a library without the table), then from
        /// <paramref name="manifest"/> (null for a library with none). Entries are visited in
        /// library order, so the standalone indices a library gets do not depend on anything
        /// else. Returns what the group rule excludes, for the log.
        /// </summary>
        public static Summary Assign(IList<LibraryEntry> library, DecoyPairingManifest manifest,
            IReadOnlyDictionary<LibraryEntry, BlibDecoyPair> tableRows = null)
        {
            var manifestPairIdx = new Dictionary<string, uint>(StringComparer.Ordinal);
            uint maxIdx = 0;
            if (manifest != null)
            {
                foreach (var kv in manifest.PairIndices())
                {
                    manifestPairIdx[kv.Key] = kv.Value;
                    if (kv.Value > maxIdx)
                        maxIdx = kv.Value;
                }
            }

            // The table's rows first: their ids are the design as written, and the indices
            // assigned below start above them.
            var assigned = new HashSet<LibraryEntry>(ReferenceEqualityComparer.Instance);
            var pairIndexBySeq = new Dictionary<string, uint>(StringComparer.Ordinal);
            if (tableRows != null)
            {
                foreach (var entry in library)
                {
                    if (entry == null || !tableRows.TryGetValue(entry, out var row))
                        continue;
                    entry.PairingId = (uint)row.PairingId;
                    assigned.Add(entry);
                    if (entry.PairingId > maxIdx)
                        maxIdx = entry.PairingId;
                    if (entry.Sequence != null && !IsDecoySide(entry) && !pairIndexBySeq.ContainsKey(entry.Sequence))
                        pairIndexBySeq[entry.Sequence] = entry.PairingId;
                }
            }
            // Clip-quartet indices live above the manifest's range so they never collide with it
            // or with each other (one per original pair index); standalone indices above those.
            bool haveManifest = manifestPairIdx.Count > 0;
            uint clipBase = maxIdx + 1;
            uint nextStandalone = clipBase + maxIdx + 1;

            // Each target-side sequence not already placed: its manifest index, its clip index,
            // or a standalone one.
            foreach (var entry in library)
            {
                if (entry?.Sequence == null || IsDecoySide(entry) || assigned.Contains(entry))
                    continue;
                string seq = entry.Sequence;
                if (pairIndexBySeq.ContainsKey(seq))
                    continue;
                if (manifestPairIdx.TryGetValue(seq, out uint mi))
                {
                    pairIndexBySeq[seq] = mi;
                }
                else if (haveManifest && manifestPairIdx.TryGetValue(MET_PREFIX + seq, out uint origIdx))
                {
                    // A Met-clip form of a manifest peptide: the clip index of its original pair,
                    // which a clipped target and a clipped entrapment share when the library holds
                    // both. A clipped entrapment alone there is the known orphan.
                    pairIndexBySeq[seq] = clipBase + origIdx;
                }
                else
                {
                    // Nothing to reconstruct from: no manifest, or an extra that is not a clip form.
                    pairIndexBySeq[seq] = nextStandalone++;
                }
            }

            // Target-side entries take their sequence's index; then the decoys, whose fallback
            // is their target's id, which the pass before this one set.
            var targetById = new Dictionary<uint, LibraryEntry>();
            foreach (var entry in library)
            {
                if (entry?.Sequence == null || IsDecoySide(entry))
                    continue;
                targetById[entry.Id] = entry;
                if (!assigned.Contains(entry))
                    entry.PairingId = pairIndexBySeq[entry.Sequence];
            }
            var decoyIndexBySeq = new Dictionary<string, uint>(StringComparer.Ordinal);
            foreach (var entry in library)
            {
                if (entry?.Sequence == null || !IsDecoySide(entry) || assigned.Contains(entry))
                    continue;
                if (manifestPairIdx.TryGetValue(entry.Sequence, out uint own))
                {
                    entry.PairingId = own;
                }
                else if (targetById.TryGetValue(entry.Id & ~LibraryEntry.DECOY_ID_BIT, out var target))
                {
                    entry.PairingId = target.PairingId;
                }
                else
                {
                    // An unpaired decoy: a group of its own, shared by its charge states.
                    if (!decoyIndexBySeq.TryGetValue(entry.Sequence, out uint standalone))
                        decoyIndexBySeq[entry.Sequence] = standalone = nextStandalone++;
                    entry.PairingId = standalone;
                }
            }

            // What the group rule excludes, named for the log: a clip form of a manifest peptide
            // is the understood Met-clip orphan, and anything else is surfaced. With no manifest
            // (a .blib, whose rows carried the ids), a clip form of a library peptide stands in.
            var libSeqs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in library)
            {
                if (entry?.Sequence != null && !IsDecoySide(entry))
                    libSeqs.Add(entry.Sequence);
            }
            var summary = new Summary();
            foreach (string seq in ExcludedEntrapment(library).OrderBy(s => s, StringComparer.Ordinal))
            {
                bool metClip = haveManifest ? manifestPairIdx.ContainsKey(MET_PREFIX + seq) : libSeqs.Contains(MET_PREFIX + seq);
                if (metClip)
                    summary.MetClipDropped++;
                else
                    summary.Unexplained.Add(seq);
            }
            return summary;
        }

        /// <summary>
        /// The entrapment sequences of <paramref name="library"/> that the FDRBench input and the
        /// diagnostics exclude: those whose <see cref="LibraryEntry.PairingId"/> no original
        /// (non-entrapment) target shares. The loader refuses an entrapment library that brings
        /// no pairing, so an entrapment peptide alone in its group is an orphan, never a guess.
        /// </summary>
        public static HashSet<string> ExcludedEntrapment(IEnumerable<LibraryEntry> library)
        {
            var originals = new HashSet<uint>();
            var entrapmentIdBySeq = new Dictionary<string, uint>(StringComparer.Ordinal);
            foreach (var entry in library)
            {
                if (entry?.Sequence == null || IsDecoySide(entry))
                    continue;
                if (!EntrapmentLibraryClassifier.IsEntrapment(entry.ProteinIds))
                    originals.Add(entry.PairingId);
                else if (!entrapmentIdBySeq.ContainsKey(entry.Sequence))
                    entrapmentIdBySeq[entry.Sequence] = entry.PairingId;
            }
            var excluded = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in entrapmentIdBySeq)
            {
                if (!originals.Contains(kv.Value))
                    excluded.Add(kv.Key);
            }
            return excluded;
        }

        private static bool IsDecoySide(LibraryEntry entry)
        {
            return EntrapmentLibraryClassifier.IsDecoySide(entry);
        }
    }
}
