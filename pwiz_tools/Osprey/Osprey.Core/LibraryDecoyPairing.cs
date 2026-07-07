/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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
    /// Statistics from a target-decoy pairing pass (or sequence of passes
    /// across the manifest + composition fallback).
    /// Maps to Rust <c>osprey_core::types::PairingStats</c>.
    /// </summary>
    public class PairingStats
    {
        /// <summary>Number of target entries seen.</summary>
        public int NTargets { get; set; }

        /// <summary>Number of decoy entries seen.</summary>
        public int NDecoys { get; set; }

        /// <summary>
        /// Number of decoys successfully paired with a target. Equals
        /// <see cref="NPairedViaManifest"/> + <see cref="NPairedViaComposition"/>.
        /// </summary>
        public int NPaired { get; set; }

        /// <summary>
        /// Of <see cref="NPaired"/>, how many were resolved by the
        /// FDRBench-style manifest path.
        /// </summary>
        public int NPairedViaManifest { get; set; }

        /// <summary>
        /// Of <see cref="NPaired"/>, how many were resolved by the
        /// composition-based fallback (matching protein accession, charge,
        /// and sorted-AA composition).
        /// </summary>
        public int NPairedViaComposition { get; set; }

        /// <summary>Number of decoys for which no target match was found.</summary>
        public int NUnpairedDecoys { get; set; }

        /// <summary>Number of targets that no decoy claimed as its pair.</summary>
        public int NUnpairedTargets { get; set; }

        /// <summary>
        /// Fraction of decoys successfully paired with a target. Returns
        /// 1.0 when there are no decoys (no work to do, trivially OK) so
        /// callers can compare against a minimum-fraction threshold
        /// without special-casing empty libraries.
        /// </summary>
        public double PairedFraction
        {
            get { return NDecoys == 0 ? 1.0 : (double)NPaired / NDecoys; }
        }
    }

    /// <summary>
    /// Shared bookkeeping for multi-pass library-decoy pairing. The
    /// manifest path may pair some decoys; the composition fallback then
    /// needs to know which targets are already claimed and which decoys
    /// are already paired so it doesn't reconsider them.
    /// Maps to Rust <c>osprey_core::types::PairingState</c>.
    /// </summary>
    public class PairingState
    {
        /// <summary>Library indices of targets already claimed by a paired decoy.</summary>
        public HashSet<int> ClaimedTargets { get; } = new HashSet<int>();

        /// <summary>Library indices of decoys already paired with a target.</summary>
        public HashSet<int> PairedDecoys { get; } = new HashSet<int>();
    }

    /// <summary>
    /// Pair library-supplied decoys with their targets via amino-acid
    /// composition. Run after <c>LibraryDecoyMarker.ApplyLibraryDecoyMarking</c>
    /// so decoys already carry <c>IsDecoy = true</c>. Composition pairing
    /// is the fallback when an FDRBench-style manifest doesn't cover a
    /// given decoy.
    /// Maps to Rust <c>osprey_core::types::pair_library_decoys_by_composition</c>.
    /// </summary>
    public static class LibraryDecoyPairing
    {
        /// <summary>
        /// Pair each un-paired decoy with a target sharing the same
        /// stripped protein accession, charge, and sorted-AA composition.
        /// Updates <paramref name="state"/> in place with new pairings;
        /// targets already in <c>state.ClaimedTargets</c> and decoys
        /// already in <c>state.PairedDecoys</c> are skipped, so this can
        /// be chained after a manifest-based pass.
        ///
        /// Determinism: when a bucket holds multiple peptides, entries on
        /// both sides are sorted by <c>(Sequence, Id)</c> and zipped 1:1.
        /// Shared peptides: a decoy's accessions are sorted
        /// lexicographically and the first un-claimed target match wins.
        ///
        /// Returns the number of decoys paired on this call.
        /// </summary>
        public static int PairLibraryDecoysByComposition(
            IList<LibraryEntry> library,
            IList<string> decoyPrefixes,
            PairingState state)
        {
            if (library == null || library.Count == 0 || state == null)
                return 0;

            // Phase 1: build target index keyed by (accession, charge,
            // sorted_aa) -> list of target library indices. Targets
            // already claimed by an earlier pass are excluded entirely.
            var targetIndex =
                new Dictionary<TargetKey, List<int>>(TargetKeyComparer.Instance);
            for (int idx = 0; idx < library.Count; idx++)
            {
                var entry = library[idx];
                if (entry == null)
                    continue;
                if (entry.IsDecoy || state.ClaimedTargets.Contains(idx))
                    continue;
                if (entry.ProteinIds == null || entry.ProteinIds.Count == 0)
                    continue;
                string aa = SortedAa(entry.Sequence);
                for (int p = 0; p < entry.ProteinIds.Count; p++)
                {
                    var key = new TargetKey(entry.ProteinIds[p], entry.Charge, aa);
                    if (!targetIndex.TryGetValue(key, out var list))
                    {
                        list = new List<int>();
                        targetIndex[key] = list;
                    }
                    list.Add(idx);
                }
            }

            // Sort each bucket deterministically by (sequence, id).
            foreach (var list in targetIndex.Values)
            {
                if (list.Count > 1)
                {
                    var lib = library;
                    // Array.Sort OK: the secondary key is the unique library entry Id, so the
                    // comparator never returns 0 and the unstable-sort tie path is unreachable.
                    list.Sort((a, b) => // Array.Sort OK: (see above) secondary key is unique library entry Id, comparator never ties
                    {
                        int c = string.CompareOrdinal(lib[a].Sequence, lib[b].Sequence);
                        if (c != 0) return c;
                        return lib[a].Id.CompareTo(lib[b].Id);
                    });
                }
            }

            // Phase 2: scan still-unpaired decoys in (sequence, id) order.
            var decoyOrder = new List<int>();
            for (int i = 0; i < library.Count; i++)
            {
                if (library[i] != null && library[i].IsDecoy &&
                    !state.PairedDecoys.Contains(i))
                    decoyOrder.Add(i);
            }
            // Array.Sort OK: the secondary key is the unique library entry Id, so the
            // comparator never returns 0 and the unstable-sort tie path is unreachable.
            decoyOrder.Sort((a, b) => // Array.Sort OK: (see above) secondary key is unique library entry Id, comparator never ties
            {
                int c = string.CompareOrdinal(library[a].Sequence, library[b].Sequence);
                if (c != 0) return c;
                return library[a].Id.CompareTo(library[b].Id);
            });

            var pairings = new List<KeyValuePair<int, int>>();
            foreach (var decoyIdx in decoyOrder)
            {
                var decoy = library[decoyIdx];
                if (decoy.ProteinIds == null || decoy.ProteinIds.Count == 0)
                    continue;
                string aa = SortedAa(decoy.Sequence);
                byte charge = decoy.Charge;
                var accs = new List<string>(decoy.ProteinIds);
                accs.Sort(StringComparer.Ordinal); // Array.Sort OK: only the sequence of distinct accession strings drives the first-match lookup below; equal accession strings are byte-identical so tie order is irrelevant

                int matched = -1;
                for (int a = 0; a < accs.Count && matched < 0; a++)
                {
                    string targetAcc = StripDecoyPrefix(accs[a], decoyPrefixes);
                    var key = new TargetKey(targetAcc, charge, aa);
                    if (!targetIndex.TryGetValue(key, out var candidates))
                        continue;
                    for (int t = 0; t < candidates.Count; t++)
                    {
                        if (!state.ClaimedTargets.Contains(candidates[t]))
                        {
                            matched = candidates[t];
                            break;
                        }
                    }
                }

                if (matched >= 0)
                {
                    state.ClaimedTargets.Add(matched);
                    state.PairedDecoys.Add(decoyIdx);
                    pairings.Add(new KeyValuePair<int, int>(decoyIdx, matched));
                }
            }

            // Phase 3: apply pairings (mutations).
            for (int i = 0; i < pairings.Count; i++)
            {
                int decoyIdx = pairings[i].Key;
                int targetIdx = pairings[i].Value;
                uint targetId = library[targetIdx].Id;
                library[decoyIdx].Id = targetId | LibraryEntry.DECOY_ID_BIT;
            }

            return pairings.Count;
        }

        /// <summary>
        /// Count targets vs decoys in <paramref name="library"/>. Used to
        /// initialize <see cref="PairingStats.NTargets"/> /
        /// <see cref="PairingStats.NDecoys"/> outside the pairing
        /// functions (which only track delta counts).
        /// </summary>
        public static void CountTargetsAndDecoys(
            IList<LibraryEntry> library, out int nTargets, out int nDecoys)
        {
            nTargets = 0;
            nDecoys = 0;
            if (library == null)
                return;
            for (int i = 0; i < library.Count; i++)
            {
                var entry = library[i];
                if (entry == null)
                    continue;
                if (entry.IsDecoy)
                    nDecoys++;
                else
                    nTargets++;
            }
        }

        /// <summary>
        /// Sorted canonical form of a peptide's amino-acid composition.
        /// <c>PEPK</c> and <c>KPEP</c> both map to <c>EKPP</c>. Maps to
        /// Rust <c>sorted_aa</c>.
        /// </summary>
        private static string SortedAa(string sequence)
        {
            if (string.IsNullOrEmpty(sequence))
                return string.Empty;
            char[] chars = sequence.ToCharArray();
            Array.Sort(chars); // Array.Sort OK: sorting a single char[] to canonicalize AA composition; ties are byte-identical so stability is irrelevant
            return new string(chars);
        }

        /// <summary>
        /// Strip the first matching prefix (case-insensitive) from
        /// <paramref name="acc"/>; returns the trimmed accession or the
        /// input unchanged if no prefix matched. Maps to Rust
        /// <c>strip_decoy_prefix</c>.
        /// </summary>
        private static string StripDecoyPrefix(string acc, IList<string> prefixes)
        {
            if (string.IsNullOrEmpty(acc) || prefixes == null)
                return acc;
            for (int i = 0; i < prefixes.Count; i++)
            {
                string p = prefixes[i];
                if (string.IsNullOrEmpty(p))
                    continue;
                if (acc.Length >= p.Length &&
                    acc.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                {
                    return acc.Substring(p.Length);
                }
            }
            return acc;
        }

        // Composite key for the target index. Mirrors Rust's
        // (String, u8, String) tuple key.
        private struct TargetKey : IEquatable<TargetKey>
        {
            public readonly string Accession;
            public readonly byte Charge;
            public readonly string Composition;

            public TargetKey(string accession, byte charge, string composition)
            {
                Accession = accession ?? string.Empty;
                Charge = charge;
                Composition = composition ?? string.Empty;
            }

            public bool Equals(TargetKey other)
            {
                return Charge == other.Charge &&
                       string.Equals(Accession, other.Accession, StringComparison.Ordinal) &&
                       string.Equals(Composition, other.Composition, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is TargetKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = Accession.GetHashCode();
                    h = (h * 397) ^ Charge.GetHashCode();
                    h = (h * 397) ^ Composition.GetHashCode();
                    return h;
                }
            }
        }

        private sealed class TargetKeyComparer : IEqualityComparer<TargetKey>
        {
            public static readonly TargetKeyComparer Instance = new TargetKeyComparer();
            public bool Equals(TargetKey x, TargetKey y) { return x.Equals(y); }
            public int GetHashCode(TargetKey obj) { return obj.GetHashCode(); }
        }
    }
}
