/*
 * Original author: MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.Text;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Localization
{
    /// <summary>
    /// Pure, model-layer analyzer that identifies site-determining product ions for
    /// modification-localization, computed analytically (no positional-isomer enumeration).
    ///
    /// A modification <c>m</c> that is present with count <c>k</c> on a peptide, and whose
    /// specificity matches <c>n</c> residues in that peptide, is "ambiguous" when
    /// <c>C(n, k) &gt; 1</c> (i.e. <c>n &gt; k &gt; 0</c>). Fixed modifications occupy all of
    /// their sites (<c>n == k</c>) and so are never ambiguous. Isotope-label modifications and
    /// crosslinkers are treated as fixed context and are ignored.
    ///
    /// For a candidate product ion, defined by (<see cref="IonType"/>, cleavage offset), the
    /// fragment covers a contiguous span of residue indices. The ion is "site-determining" for an
    /// ambiguous modification when the number of that modification's sites falling inside the span
    /// can vary across positional placements. See <see cref="IsSiteDetermining"/>.
    /// </summary>
    public class SiteDeterminingIonAnalyzer
    {
        /// <summary>
        /// Clamp for integer binomial / product arithmetic. Combinatorial counts can grow past the
        /// range of <see cref="long"/>; values are clamped here so downstream consumers see a
        /// large-but-finite sentinel rather than an overflowed (possibly negative) number.
        /// </summary>
        public const long MAX_COUNT = 1_000_000_000_000_000L; // 1e15, well within long range

        private readonly int _length;
        private readonly string _unmodifiedSequence;
        private readonly IList<AmbiguousMod> _ambiguousMods;

        /// <summary>
        /// Performs the cheap analytic precompute (candidate positions per ambiguous modification
        /// via prefix arrays) for a single peptide placement.
        /// </summary>
        /// <param name="settings">Document settings (reserved for future mass cross-checks; the
        /// span analysis itself is purely combinatorial).</param>
        /// <param name="nodePep">The peptide, including its explicit (light) modification placement.</param>
        public SiteDeterminingIonAnalyzer(SrmSettings settings, PeptideDocNode nodePep)
        {
            _ambiguousMods = new List<AmbiguousMod>();

            var peptide = nodePep.Peptide;
            if (peptide == null || peptide.IsCustomMolecule)
                return; // Small molecule / non-proteomic - nothing to localize

            string sequence = peptide.Sequence;
            if (string.IsNullOrEmpty(sequence))
                return;
            _length = sequence.Length;
            _unmodifiedSequence = sequence;

            var explicitMods = nodePep.ExplicitMods;
            var staticMods = explicitMods?.StaticModifications;
            if (staticMods == null || staticMods.Count == 0)
                return;

            // Group the peptide's explicit static (light) modifications by their StaticMod, so a
            // modification present on several residues collapses to a single (mod, count) pair.
            foreach (var group in staticMods.GroupBy(em => em.Modification))
            {
                var mod = group.Key;
                // Skip isotope-label modifications and crosslinkers - fixed context, not localizable.
                if (mod.LabelAtoms != LabelAtoms.None || mod.CrosslinkerSettings != null)
                    continue;

                var placedPositions = group.Select(em => em.IndexAA).OrderBy(i => i).ToArray();
                int k = placedPositions.Length;
                if (k == 0)
                    continue;

                // prefixCandidates[i] = number of specificity-matching residues with index < i.
                var prefixCandidates = new int[_length + 1];
                for (int i = 0; i < _length; i++)
                {
                    prefixCandidates[i + 1] = prefixCandidates[i] +
                                              (mod.IsApplicableMod(sequence, i) ? 1 : 0);
                }

                int n = prefixCandidates[_length];
                // Ambiguous iff C(n, k) > 1, i.e. n > k > 0. Fully determined mods (n == k) excluded.
                if (n > k)
                {
                    _ambiguousMods.Add(new AmbiguousMod(mod, k, n, prefixCandidates, placedPositions));
                }
            }
        }

        /// <summary>
        /// True iff the peptide carries at least one ambiguous modification, so that localization
        /// analysis is meaningful.
        /// </summary>
        public bool CanLocalize
        {
            get { return _ambiguousMods.Count > 0; }
        }

        /// <summary>
        /// Number of positional isomers for the peptide, the product over ambiguous modifications
        /// of <c>C(n, k)</c>. Returns 1 when there are no ambiguous modifications (a single
        /// arrangement). Clamped to <see cref="MAX_COUNT"/> to guard against overflow.
        /// </summary>
        public long IsomerCount
        {
            get
            {
                long result = 1;
                foreach (var mod in _ambiguousMods)
                    result = MultiplyClamped(result, Binomial(mod.SiteCount, mod.Count));
                return result;
            }
        }

        /// <summary>
        /// Stable grouping key over the ambiguous modifications only: the unmodified sequence
        /// followed by a sorted list of "<c>ModName*count</c>" entries. Returns null when there
        /// are no ambiguous modifications. (An ASCII '*' is used as the count separator to keep
        /// the key free of non-ASCII characters.)
        /// </summary>
        public string LocalizationGroupKey
        {
            get
            {
                if (!CanLocalize)
                    return null;

                var entries = _ambiguousMods
                    .Select(mod => mod.Mod.Name + @"*" + mod.Count)
                    .OrderBy(s => s, StringComparer.Ordinal);

                var sb = new StringBuilder(_unmodifiedSequence ?? string.Empty);
                sb.Append('[');
                sb.Append(string.Join(@",", entries));
                sb.Append(']');
                return sb.ToString();
            }
        }

        /// <summary>
        /// True iff this product ion's in-span occupancy of some ambiguous modification can vary
        /// across positional placements, i.e. the ion helps localize that modification. False for
        /// precursor, custom, and non-proteomic transitions.
        /// </summary>
        public bool IsSiteDetermining(Transition transition)
        {
            return GetResolvedModification(transition) != null;
        }

        /// <summary>
        /// Returns the (primary) ambiguous modification that the given ion resolves, for tooltip /
        /// reporting use, or null when the ion is not site-determining. When an ion resolves more
        /// than one modification, the first (in precompute order) is returned.
        /// </summary>
        public StaticMod GetResolvedModification(Transition transition)
        {
            if (!TryGetSpan(transition, out int lo, out int hi))
                return null;

            foreach (var mod in _ambiguousMods)
            {
                int inside = mod.InsideSpan(lo, hi);
                int outside = mod.SiteCount - inside;
                // In-span occupancy ranges over [max(0, k - outside) .. min(k, inside)]. It can
                // vary (the ion is site-determining) iff that range is non-degenerate.
                if (Math.Min(mod.Count, inside) > Math.Max(0, mod.Count - outside))
                    return mod.Mod;
            }
            return null;
        }

        /// <summary>
        /// Number of positional isomers that produce this exact ion (with the same in-span
        /// modification masses) as the placement this analyzer was built from. A value of 1 means
        /// the ion is unique to this isomer. Returns 0 for precursor / custom / non-proteomic
        /// transitions. Clamped to <see cref="MAX_COUNT"/>.
        /// </summary>
        public int GetProducingSetSizeAsInt(Transition transition)
        {
            long size = GetProducingSetSize(transition);
            return size > int.MaxValue ? int.MaxValue : (int) size;
        }

        /// <summary>
        /// <see cref="GetProducingSetSizeAsInt"/> as a (clamped) <see cref="long"/>.
        /// </summary>
        public long GetProducingSetSize(Transition transition)
        {
            if (!TryGetSpan(transition, out int lo, out int hi))
                return 0;

            long product = 1;
            foreach (var mod in _ambiguousMods)
            {
                int inside = mod.InsideSpan(lo, hi);
                int outside = mod.SiteCount - inside;
                int c = mod.PlacedInsideSpan(lo, hi); // Sites of this mod actually inside the span
                long term = MultiplyClamped(Binomial(inside, c), Binomial(outside, mod.Count - c));
                product = MultiplyClamped(product, term);
            }
            return product;
        }

        /// <summary>
        /// True iff this ion is produced by exactly one positional isomer (the placement this
        /// analyzer was built from) and localization is meaningful for the peptide.
        /// </summary>
        public bool IsUniqueToPrecursor(Transition transition)
        {
            return CanLocalize && GetProducingSetSize(transition) == 1;
        }

        private bool TryGetSpan(Transition transition, out int lo, out int hi)
        {
            lo = 0;
            hi = -1;
            if (transition == null || _length == 0)
                return false;
            if (transition.IsPrecursor() || transition.IsCustom())
                return false;

            var type = transition.IonType;
            if (type.IsNTerminal())
            {
                // a/b/c: residues [0 .. cleavageOffset]
                lo = 0;
                hi = transition.CleavageOffset;
            }
            else if (type.IsCTerminal())
            {
                // x/y/z/zh/zhh: residues [cleavageOffset+1 .. len-1]
                lo = transition.CleavageOffset + 1;
                hi = _length - 1;
            }
            else
            {
                return false;
            }

            if (lo < 0)
                lo = 0;
            if (hi > _length - 1)
                hi = _length - 1;
            return hi >= lo;
        }

        /// <summary>
        /// Integer binomial coefficient C(n, k) using the multiplicative form (stays integral at
        /// each step), clamped to <see cref="MAX_COUNT"/> to guard against overflow.
        /// </summary>
        private static long Binomial(int n, int k)
        {
            if (k < 0 || k > n || n < 0)
                return 0;
            if (k == 0 || k == n)
                return 1;
            k = Math.Min(k, n - k);
            long result = 1;
            for (int i = 1; i <= k; i++)
            {
                result = result * (n - k + i) / i;
                if (result >= MAX_COUNT || result < 0)
                    return MAX_COUNT;
            }
            return result;
        }

        private static long MultiplyClamped(long a, long b)
        {
            if (a == 0 || b == 0)
                return 0;
            if (a >= MAX_COUNT || b >= MAX_COUNT)
                return MAX_COUNT;
            long result = a * b;
            if (result >= MAX_COUNT || result < 0)
                return MAX_COUNT;
            return result;
        }

        /// <summary>
        /// Precomputed per-ambiguous-modification analytic state.
        /// </summary>
        private sealed class AmbiguousMod
        {
            private readonly int[] _prefixCandidates; // prefixCandidates[i] = # candidate sites with index < i
            private readonly int[] _placedPositions;  // residue indices where this mod is actually placed

            public AmbiguousMod(StaticMod mod, int count, int siteCount, int[] prefixCandidates, int[] placedPositions)
            {
                Mod = mod;
                Count = count;
                SiteCount = siteCount;
                _prefixCandidates = prefixCandidates;
                _placedPositions = placedPositions;
            }

            public StaticMod Mod { get; }
            public int Count { get; }      // k_m
            public int SiteCount { get; }  // n_m

            public int InsideSpan(int lo, int hi)
            {
                // Number of candidate sites with index in [lo .. hi].
                return _prefixCandidates[hi + 1] - _prefixCandidates[lo];
            }

            public int PlacedInsideSpan(int lo, int hi)
            {
                int c = 0;
                foreach (int index in _placedPositions)
                {
                    if (index >= lo && index <= hi)
                        c++;
                }
                return c;
            }
        }
    }
}
