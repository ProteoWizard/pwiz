/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// Keeps a library pair-complete when it is written with a pairing manifest: a target and
    /// its decoy, and an entrapment target and its entrapment decoy, are written only together.
    /// When one has fewer fragments than <c>-lf_min_n_frag</c>, its partner is dropped too, so
    /// no target wins target-decoy competition unopposed (Osprey counts a target without a
    /// decoy as a winner) and no decoy is written without its target. Carafe writes each on its
    /// own.
    /// <para>
    /// Partners are matched as <see cref="DecoyPairPlanner"/> pairs them: by I/L-normalized
    /// sequence, charge and modification set. A sequence with more than one place in the
    /// manifest, or whose partner has more than one, passes ungated, since the planner pairs
    /// such a precursor once at most. Every precursor to be predicted is counted first
    /// (<see cref="Expect"/>). A pair is decided once all of its precursors have been offered:
    /// each side keeps as many as the other side kept, in the order offered. Pair members are
    /// anagrams of one mass, and the library's forms are sorted by mass, so only the pairs
    /// split across chunks are held.
    /// </para>
    /// </summary>
    public sealed class DecoyPairGate
    {
        private static readonly string[] SIDE_TYPES = { @"target", @"decoy", @"p_target", @"p_decoy" };

        /// <summary>Each gated sequence's pair group, whether it is the entrapment pair, and its side (0 target, 1 decoy).</summary>
        private readonly Dictionary<string, (int PairIndex, bool Entrapment, int Side)> _members =
            new Dictionary<string, (int, bool, int)>(StringComparer.Ordinal);
        private readonly Dictionary<(int PairIndex, bool Entrapment, int Charge, string ModKey), PairState> _pairs =
            new Dictionary<(int, bool, int, string), PairState>();
        private readonly List<LibrarySpectrum> _dropped = new List<LibrarySpectrum>();
        private long _offered;

        public DecoyPairGate(IEnumerable<DecoyPairPlanner.ManifestEntry> manifest)
        {
            var places = new Dictionary<string, HashSet<(int, string)>>(StringComparer.Ordinal);
            var groups = new Dictionary<int, Dictionary<string, string>>();
            foreach (var entry in manifest)
            {
                string sequence = PairingManifestReconciler.Normalize(entry.Sequence);
                string type = JavaText.Trim(entry.PeptideType ?? string.Empty).ToLowerInvariant();
                if (!places.TryGetValue(sequence, out var set))
                    places.Add(sequence, set = new HashSet<(int, string)>());
                set.Add((entry.PairIndex, type));
                if (!groups.TryGetValue(entry.PairIndex, out var group))
                    groups.Add(entry.PairIndex, group = new Dictionary<string, string>(StringComparer.Ordinal));
                group[type] = sequence;
            }
            foreach (var pair in groups)
            {
                for (int i = 0; i < SIDE_TYPES.Length; i++)
                {
                    string partnerType = SIDE_TYPES[i ^ 1];
                    if (!pair.Value.TryGetValue(SIDE_TYPES[i], out string sequence) || !pair.Value.TryGetValue(partnerType, out string partner))
                        continue;
                    if (places[sequence].Count == 1 && places[partner].Count == 1)
                        _members[sequence] = (pair.Key, i >= 2, i & 1);
                }
            }
        }

        /// <summary>The precursors dropped because their partner fell below the minimum, in the order offered.</summary>
        public IReadOnlyList<LibrarySpectrum> Dropped
        {
            get { return _dropped; }
        }

        /// <summary>Whether <paramref name="strippedSequence"/> is one side of a gated pair.</summary>
        public bool IsMember(string strippedSequence)
        {
            return _members.ContainsKey(PairingManifestReconciler.Normalize(strippedSequence));
        }

        /// <summary>Counts a precursor that will be predicted and then offered.</summary>
        public void Expect(string strippedSequence, int charge, string modKey)
        {
            if (!TryGetKey(strippedSequence, charge, modKey, out var key, out int side))
                return;
            if (!_pairs.TryGetValue(key, out var state))
                _pairs.Add(key, state = new PairState());
            state.Expected[side]++;
        }

        /// <summary>
        /// Offers a predicted precursor: its <paramref name="spectrum"/>, or null when it fell
        /// below the minimum. Appends to <paramref name="released"/> what may be written now, in
        /// the order offered.
        /// </summary>
        public void Offer(string strippedSequence, int charge, string modKey, LibrarySpectrum spectrum, List<LibrarySpectrum> released)
        {
            long order = _offered++;
            if (!TryGetKey(strippedSequence, charge, modKey, out var key, out int side) || !_pairs.TryGetValue(key, out var state))
            {
                if (spectrum != null)
                    released.Add(spectrum);
                return;
            }
            if (state.Arrived[0] + state.Arrived[1] == 0)
                state.FirstOffered = order;
            state.Arrived[side]++;
            if (spectrum != null)
                state.Kept.Add((side, spectrum));
            if (state.Arrived[0] >= state.Expected[0] && state.Arrived[1] >= state.Expected[1])
            {
                Decide(state, released);
                _pairs.Remove(key);
            }
        }

        /// <summary>
        /// Decides the pairs still held, whose precursors were not all offered, in the order they
        /// were first offered, and appends what may be written to <paramref name="released"/>.
        /// </summary>
        public void Finish(List<LibrarySpectrum> released)
        {
            foreach (var state in _pairs.Values.Where(s => s.Arrived[0] + s.Arrived[1] > 0).OrderBy(s => s.FirstOffered))
                Decide(state, released);
            _pairs.Clear();
        }

        /// <summary>
        /// Releases as many of each side as the other side kept, in the order offered. A pair
        /// whose partner side has no precursor to predict (its peptide is not in the library's
        /// FASTA) has nothing to pair with, so all of it is released.
        /// </summary>
        private void Decide(PairState state, List<LibrarySpectrum> released)
        {
            int keep = state.Expected[0] == 0 || state.Expected[1] == 0
                ? int.MaxValue
                : Math.Min(state.Kept.Count(k => k.Side == 0), state.Kept.Count(k => k.Side == 1));
            var taken = new int[2];
            foreach (var (side, spectrum) in state.Kept)
            {
                if (taken[side]++ < keep)
                    released.Add(spectrum);
                else
                    _dropped.Add(spectrum);
            }
        }

        private bool TryGetKey(string strippedSequence, int charge, string modKey, out (int, bool, int, string) key, out int side)
        {
            if (!_members.TryGetValue(PairingManifestReconciler.Normalize(strippedSequence), out var member))
            {
                key = default;
                side = 0;
                return false;
            }
            key = (member.PairIndex, member.Entrapment, charge, modKey ?? string.Empty);
            side = member.Side;
            return true;
        }

        /// <summary>A pair's precursors: how many of each side will be offered, how many were, and those kept, in order.</summary>
        private sealed class PairState
        {
            public int[] Expected { get; } = new int[2];
            public int[] Arrived { get; } = new int[2];
            public List<(int Side, LibrarySpectrum Spectrum)> Kept { get; } = new List<(int, LibrarySpectrum)>();
            public long FirstOffered { get; set; }
        }
    }
}
