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
using pwiz.CarafeSharp.Contracts;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Assigns every library entry its <see cref="LibraryEntry.PairingId"/> once, at load, so
    /// the pairing of an entrapment peptide with its original target lives in the library -
    /// the cache, a .blib's <c>DecoyPairs</c>, the export - and every consumer reads the entry
    /// rather than the pairing manifest file.
    ///
    /// <para>A peptide the manifest covers takes the manifest's <c>peptide_pair_index</c>. The
    /// library legitimately holds peptide forms the manifest never had - N-terminal-Met-clipped
    /// variants a predictor makes (Carafe <c>-clip_n_m</c>) - and for those the pairing is
    /// reconstructed as it always was: a clipped peptide whose M-prefixed sequence is a manifest
    /// peptide pairs on a clip index above the manifest's range (one per original pair index),
    /// which a clipped entrapment shares with the clip of its manifest target when that clip is
    /// itself an extra in the library; a clipped entrapment with no such target is the known
    /// orphan the FDRBench input and the diagnostics exclude (it gets no id), and an extra
    /// entrapment that is not a clip form is excluded and reported. An extra target, and every
    /// peptide of a library with no manifest, takes a standalone index. A decoy takes the index
    /// of its own sequence when the manifest covers it, else its target's.</para>
    /// </summary>
    public static class PairingIds
    {
        // N-terminal methionine, prepended to reverse a Met clip.
        private const string MET_PREFIX = @"M";

        /// <summary>What <see cref="Assign"/> excluded: for the load log.</summary>
        public sealed class Summary
        {
            /// <summary>Entrapment sequences with no target pair that are the known N-terminal-Met-clip artifact.</summary>
            public int MetClipDropped { get; set; }

            /// <summary>Entrapment sequences with no target pair and no known cause, surfaced rather than silently dropped.</summary>
            public List<string> Unexplained { get; } = new List<string>();
        }

        /// <summary>
        /// Sets <see cref="LibraryEntry.PairingId"/> on every entry of <paramref name="library"/>
        /// from <paramref name="manifest"/> (null for a library with none) by the rules in the
        /// class summary. Entries are visited in library order, so the standalone indices a
        /// library gets do not depend on anything else.
        /// </summary>
        public static Summary Assign(IList<LibraryEntry> library, DecoyPairingManifest manifest)
        {
            var manifestKind = new Dictionary<string, PeptideKind>(StringComparer.Ordinal);
            var manifestPairIdx = new Dictionary<string, uint>(StringComparer.Ordinal);
            var targetByPairIdx = new Dictionary<uint, string>();
            uint maxIdx = 0;
            if (manifest != null)
            {
                foreach (var kv in manifest.Kinds())
                    manifestKind[kv.Key] = kv.Value;
                foreach (var kv in manifest.PairIndices())
                {
                    manifestPairIdx[kv.Key] = kv.Value;
                    if (kv.Value > maxIdx)
                        maxIdx = kv.Value;
                }
                foreach (var kv in manifestKind)
                {
                    if (kv.Value == PeptideKind.Target && manifestPairIdx.TryGetValue(kv.Key, out uint idx))
                        targetByPairIdx[idx] = kv.Key;
                }
            }
            // Clip-quartet indices live above the manifest's range so they never collide with it
            // or with each other (one per original pair index).
            bool haveManifest = manifestKind.Count > 0;
            uint clipBase = maxIdx + 1;
            uint nextStandalone = clipBase + maxIdx + 1;

            // Library non-decoy sequences, for the "is the clipped target in the library?" test.
            var libSeqs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in library)
            {
                if (entry?.Sequence != null && !EntrapmentLibraryClassifier.IsDecoySide(entry.ProteinIds))
                    libSeqs.Add(entry.Sequence);
            }

            var summary = new Summary();
            var pairIndexBySeq = new Dictionary<string, uint>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in library)
            {
                if (entry?.Sequence == null || EntrapmentLibraryClassifier.IsDecoySide(entry.ProteinIds))
                    continue;
                string seq = entry.Sequence;
                if (!seen.Add(seq))
                    continue;

                // Covered by the manifest: its pairing directly.
                if (manifestPairIdx.TryGetValue(seq, out uint mi))
                {
                    pairIndexBySeq[seq] = mi;
                    continue;
                }

                // With no manifest there is nothing to reconcile against: every peptide is kept,
                // none excluded (Met-clip artifacts are undetectable).
                if (!haveManifest)
                {
                    pairIndexBySeq[seq] = nextStandalone++;
                    continue;
                }

                bool entrapment = EntrapmentLibraryClassifier.IsEntrapment(entry.ProteinIds);
                // An extra: it should be a Met-clip form of a manifest peptide.
                if (!manifestKind.ContainsKey(MET_PREFIX + seq))
                {
                    if (entrapment)
                        summary.Unexplained.Add(seq); // not a clip form and unpaired: surfaced, not silently dropped
                    else
                        pairIndexBySeq[seq] = nextStandalone++; // an extra target only adds to n_t
                    continue;
                }

                uint origIdx = manifestPairIdx[MET_PREFIX + seq];
                if (entrapment)
                {
                    // A clipped entrapment pairs only with the clip of the manifest target at this
                    // pair index, which exists only if that target starts with M and its clipped
                    // form was predicted into the library as an extra (a clipped form that is itself
                    // a manifest peptide keeps that manifest index, leaving this one an orphan).
                    string target = targetByPairIdx.TryGetValue(origIdx, out var t) ? t : null;
                    string clippedTarget = target != null && target.StartsWith(MET_PREFIX, StringComparison.Ordinal)
                        ? target.Substring(1) : null;
                    bool clippedTargetExists = clippedTarget != null &&
                                               libSeqs.Contains(clippedTarget) && !manifestKind.ContainsKey(clippedTarget);
                    if (clippedTargetExists)
                        pairIndexBySeq[seq] = clipBase + origIdx;
                    else
                        summary.MetClipDropped++; // the understood Met-clip orphan entrapment
                }
                else
                {
                    // A clipped target: the clip-quartet index, shared with its clipped entrapment.
                    pairIndexBySeq[seq] = clipBase + origIdx;
                }
            }

            // Every entry: a target-side entry takes its sequence's index (none for an excluded
            // orphan); a decoy takes its own sequence's manifest index when the manifest covers
            // it, else its target's.
            var targetById = new Dictionary<uint, LibraryEntry>();
            foreach (var entry in library)
            {
                if (entry != null && !entry.IsDecoy)
                    targetById[entry.Id] = entry;
            }
            foreach (var entry in library)
            {
                if (entry?.Sequence == null)
                    continue;
                bool decoySide = entry.IsDecoy || EntrapmentLibraryClassifier.IsDecoySide(entry.ProteinIds);
                if (!decoySide)
                {
                    entry.PairingId = pairIndexBySeq.TryGetValue(entry.Sequence, out uint idx) ? idx : null;
                }
                else if (manifestPairIdx.TryGetValue(entry.Sequence, out uint own))
                {
                    entry.PairingId = own;
                }
                else
                {
                    entry.PairingId = targetById.TryGetValue(entry.Id & ~LibraryEntry.DECOY_ID_BIT, out var target)
                        ? target.PairingId : null;
                }
            }
            return summary;
        }
    }
}
