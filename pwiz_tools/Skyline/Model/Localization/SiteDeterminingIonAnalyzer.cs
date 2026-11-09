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
using pwiz.Skyline.Util;

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

        private static readonly SequenceMassCalc MASS_CALC = new SequenceMassCalc(MassType.Monoisotopic);

        private readonly int _length;
        private readonly string _unmodifiedSequence;
        private readonly IList<AmbiguousMod> _ambiguousMods;

        /// <summary>
        /// Performs the cheap analytic precompute (candidate positions per ambiguous modification
        /// via prefix arrays) for a single peptide placement.
        /// </summary>
        /// <param name="settings">Document settings, whose structural modifications supply the
        /// candidate sites of sibling modifications with the same mass shift.</param>
        /// <param name="nodePep">The peptide, including its explicit (light) modification placement.</param>
        public SiteDeterminingIonAnalyzer(SrmSettings settings, PeptideDocNode nodePep)
            : this(settings, nodePep.Peptide, nodePep.ExplicitMods)
        {
        }

        /// <summary>
        /// The same precompute for a peptide and an explicit modification placement, for callers
        /// that have no <see cref="PeptideDocNode"/>, such as transition auto-selection.
        /// Ambiguity is grouped by monoisotopic mass shift rather than by modification entry, so
        /// "Phospho (ST)" and "Phospho (Y)" compete for one set of sites: the positional isomers of
        /// a precursor are the placements with the same mass, whatever the residue specificity.
        /// </summary>
        public SiteDeterminingIonAnalyzer(SrmSettings settings, Peptide peptide, ExplicitMods explicitMods)
        {
            _ambiguousMods = new List<AmbiguousMod>();

            if (peptide == null || peptide.IsCustomMolecule)
                return; // Small molecule / non-proteomic - nothing to localize

            string sequence = peptide.Sequence;
            if (string.IsNullOrEmpty(sequence))
                return;
            _length = sequence.Length;
            _unmodifiedSequence = sequence;

            var staticMods = explicitMods?.StaticModifications;
            if (staticMods == null || staticMods.Count == 0)
                return;

            // Structural modifications known to the document, the source of sibling modifications
            // with the same mass shift as a placed one (e.g. "Phospho (Y)" next to "Phospho (ST)").
            var documentMods = settings?.PeptideSettings.Modifications.StaticModifications ?? new StaticMod[0];

            // Group the peptide's explicit static (light) modifications by mass shift, so a
            // modification present on several residues collapses to a single (shift, count) pair.
            foreach (var group in staticMods.Where(em => IsLocalizableMod(em.Modification))
                         .GroupBy(em => GetMassShiftKey(em.Modification)))
            {
                long massShiftKey = group.Key;
                var placedMods = group.Select(em => em.Modification).Distinct().ToArray();
                var siblingMods = placedMods
                    .Concat(documentMods.Where(mod => IsLocalizableMod(mod) && GetMassShiftKey(mod) == massShiftKey))
                    .GroupBy(mod => mod.Name).Select(g => g.First())
                    .OrderBy(mod => mod.Name, StringComparer.Ordinal)
                    .ToArray();

                var placedPositions = group.Select(em => em.IndexAA).OrderBy(i => i).ToArray();
                int k = placedPositions.Length;

                // prefixCandidates[i] = number of residues with index < i that some sibling can occupy.
                var prefixCandidates = new int[_length + 1];
                for (int i = 0; i < _length; i++)
                {
                    bool candidate = siblingMods.Any(mod => mod.IsApplicableMod(sequence, i));
                    prefixCandidates[i + 1] = prefixCandidates[i] + (candidate ? 1 : 0);
                }

                int n = prefixCandidates[_length];
                // Ambiguous iff C(n, k) > 1, i.e. n > k > 0. Fully determined mods (n == k) excluded.
                if (n > k)
                {
                    string name = string.Join(@"/", siblingMods.Select(mod => mod.Name));
                    _ambiguousMods.Add(new AmbiguousMod(placedMods[0], name, massShiftKey, k, n, prefixCandidates, placedPositions));
                }
            }
        }

        /// <summary>
        /// Isotope-label modifications and crosslinkers are fixed context, never localized.
        /// </summary>
        private static bool IsLocalizableMod(StaticMod mod)
        {
            return mod.LabelAtoms == LabelAtoms.None && mod.CrosslinkerSettings == null;
        }

        /// <summary>
        /// The monoisotopic mass shift of a modification in thousandths of a dalton, the identity
        /// under which modifications with different residue specificity are the same ambiguity.
        /// </summary>
        private static long GetMassShiftKey(StaticMod mod)
        {
            return (long) Math.Round(MASS_CALC.GetModMass('\0', mod) * 1000);
        }

        /// <summary>
        /// The analyzer for a peptide placement, or null when there is nothing to localize, so
        /// callers can keep a single null check instead of testing <see cref="CanLocalize"/>.
        /// </summary>
        public static SiteDeterminingIonAnalyzer CreateIfLocalizable(SrmSettings settings, Peptide peptide, ExplicitMods explicitMods)
        {
            var analyzer = new SiteDeterminingIonAnalyzer(settings, peptide, explicitMods);
            return analyzer.CanLocalize ? analyzer : null;
        }

        /// <summary>
        /// <see cref="CreateIfLocalizable(SrmSettings, Peptide, ExplicitMods)"/> for a peptide node.
        /// </summary>
        public static SiteDeterminingIonAnalyzer CreateIfLocalizable(SrmSettings settings, PeptideDocNode nodePep)
        {
            return CreateIfLocalizable(settings, nodePep.Peptide, nodePep.ExplicitMods);
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
                    .Select(mod => mod.Name + @"*" + mod.Count)
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
        /// precursor, custom, and non-proteomic transitions, and for ions that have lost an
        /// ambiguous modification as a neutral loss.
        /// </summary>
        public bool IsSiteDetermining(Transition transition, TransitionLosses losses)
        {
            return GetResolvedModification(transition, losses) != null;
        }

        /// <summary>
        /// Returns the (primary) ambiguous modification that the given ion resolves, or null when
        /// the ion is not site-determining. When an ion resolves more than one modification, the
        /// first (in precompute order) is returned; see <see cref="GetResolvedModifications"/>.
        /// </summary>
        public StaticMod GetResolvedModification(Transition transition, TransitionLosses losses)
        {
            if (!IsFragment(transition))
                return null;
            return GetResolvedModifications(transition.IonType, transition.CleavageOffset, losses).FirstOrDefault();
        }

        /// <summary>
        /// Returns every ambiguous modification that a product ion, given by its type, cleavage
        /// offset and neutral losses, resolves: those whose in-span occupancy can vary across
        /// positional placements. Each is the modification entry placed on the peptide; sibling
        /// entries with the same mass shift are folded into it.
        /// </summary>
        public IEnumerable<StaticMod> GetResolvedModifications(IonType type, int cleavageOffset, TransitionLosses losses)
        {
            return GetResolvingMods(type, cleavageOffset, losses).Select(mod => mod.Mod);
        }

        private IEnumerable<AmbiguousMod> GetResolvingMods(IonType type, int cleavageOffset, TransitionLosses losses)
        {
            if (HasAmbiguousLoss(losses) || !TryGetSpan(type, cleavageOffset, out int lo, out int hi))
                yield break;

            foreach (var mod in _ambiguousMods)
            {
                int inside = mod.InsideSpan(lo, hi);
                int outside = mod.SiteCount - inside;
                // In-span occupancy ranges over [max(0, k - outside) .. min(k, inside)]. It can
                // vary (the ion is site-determining) iff that range is non-degenerate.
                if (Math.Min(mod.Count, inside) > Math.Max(0, mod.Count - outside))
                    yield return mod;
            }
        }

        /// <summary>
        /// True when a neutral loss of the ion comes from an ambiguous modification. Such an ion
        /// has shed the very group it would localize, and its m/z coincides with a common loss
        /// from the other placements: a phospho fragment minus H3PO4 is the unmodified fragment
        /// minus H2O, so every isomer produces it.
        /// </summary>
        private bool HasAmbiguousLoss(TransitionLosses losses)
        {
            if (losses == null)
                return false;
            foreach (var loss in losses.Losses)
            {
                if (loss.PrecursorMod == null)
                    continue;
                long massShiftKey = GetMassShiftKey(loss.PrecursorMod);
                if (_ambiguousMods.Any(mod => mod.MassShiftKey == massShiftKey))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The names of the modifications this ion uniquely localizes, comma separated for
        /// display, or null when the ion is not unique to the current placement.
        /// </summary>
        public string GetUniquelyLocalizedModificationNames(TransitionDocNode nodeTran)
        {
            return GetUniquelyLocalizedModificationNames(nodeTran.Transition, nodeTran.Losses);
        }

        /// <summary>
        /// <see cref="GetUniquelyLocalizedModificationNames(TransitionDocNode)"/> for a transition
        /// identity and its neutral losses.
        /// </summary>
        public string GetUniquelyLocalizedModificationNames(Transition transition, TransitionLosses losses)
        {
            if (!IsFragment(transition))
                return null;
            return GetUniquelyLocalizedModificationNames(transition.IonType, transition.CleavageOffset, losses);
        }

        /// <summary>
        /// <see cref="GetUniquelyLocalizedModificationNames(TransitionDocNode)"/> for a product ion
        /// given by its type, cleavage offset and neutral losses.
        /// </summary>
        public string GetUniquelyLocalizedModificationNames(IonType type, int cleavageOffset, TransitionLosses losses)
        {
            if (!IsUniqueToPrecursor(type, cleavageOffset, losses))
                return null;
            return string.Join(@", ", GetResolvingMods(type, cleavageOffset, losses).Select(mod => mod.Name));
        }

        /// <summary>
        /// Number of positional isomers that produce this exact ion (with the same in-span
        /// modification masses) as the placement this analyzer was built from. A value of 1 means
        /// the ion is unique to this isomer. Returns 0 for precursor / custom / non-proteomic
        /// transitions, and <see cref="IsomerCount"/> for an ion that has lost an ambiguous
        /// modification as a neutral loss, since every isomer then produces its m/z. Clamped to
        /// <see cref="MAX_COUNT"/>.
        /// </summary>
        public long GetProducingSetSize(Transition transition, TransitionLosses losses)
        {
            if (!IsFragment(transition))
                return 0;
            return GetProducingSetSize(transition.IonType, transition.CleavageOffset, losses);
        }

        /// <summary>
        /// <see cref="GetProducingSetSize(Transition, TransitionLosses)"/> for a product ion given by
        /// its type, cleavage offset and neutral losses, before any <see cref="Transition"/> exists.
        /// </summary>
        public long GetProducingSetSize(IonType type, int cleavageOffset, TransitionLosses losses)
        {
            if (!TryGetSpan(type, cleavageOffset, out int lo, out int hi))
                return 0;
            if (HasAmbiguousLoss(losses))
                return IsomerCount;

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
        public bool IsUniqueToPrecursor(TransitionDocNode nodeTran)
        {
            return IsUniqueToPrecursor(nodeTran.Transition, nodeTran.Losses);
        }

        /// <summary>
        /// <see cref="IsUniqueToPrecursor(TransitionDocNode)"/> for a transition identity and its
        /// neutral losses.
        /// </summary>
        public bool IsUniqueToPrecursor(Transition transition, TransitionLosses losses)
        {
            return CanLocalize && GetProducingSetSize(transition, losses) == 1;
        }

        /// <summary>
        /// <see cref="IsUniqueToPrecursor(TransitionDocNode)"/> for a product ion given by its type,
        /// cleavage offset and neutral losses.
        /// </summary>
        public bool IsUniqueToPrecursor(IonType type, int cleavageOffset, TransitionLosses losses)
        {
            return CanLocalize && GetProducingSetSize(type, cleavageOffset, losses) == 1;
        }

        private static bool IsFragment(Transition transition)
        {
            return transition != null && !transition.IsPrecursor() && !transition.IsCustom();
        }

        private bool TryGetSpan(IonType type, int cleavageOffset, out int lo, out int hi)
        {
            lo = 0;
            hi = -1;
            if (_length == 0)
                return false;

            if (type.IsNTerminal())
            {
                // a/b/c: residues [0 .. cleavageOffset]
                lo = 0;
                hi = cleavageOffset;
            }
            else if (type.IsCTerminal())
            {
                // x/y/z/zh/zhh: residues [cleavageOffset+1 .. len-1]
                lo = cleavageOffset + 1;
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
            if (k < 0 || k > n)
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
            // Compare before multiplying: a * b could overflow long even when both are below MAX_COUNT.
            if (a >= MAX_COUNT || b >= MAX_COUNT || a > MAX_COUNT / b)
                return MAX_COUNT;
            return a * b;
        }

        /// <summary>
        /// Precomputed per-ambiguous-modification analytic state.
        /// </summary>
        private sealed class AmbiguousMod
        {
            private readonly int[] _prefixCandidates; // prefixCandidates[i] = # candidate sites with index < i
            private readonly int[] _placedPositions;  // residue indices where this mod is actually placed

            public AmbiguousMod(StaticMod mod, string name, long massShiftKey, int count, int siteCount,
                int[] prefixCandidates, int[] placedPositions)
            {
                Mod = mod;
                Name = name;
                MassShiftKey = massShiftKey;
                Count = count;
                SiteCount = siteCount;
                _prefixCandidates = prefixCandidates;
                _placedPositions = placedPositions;
            }

            public StaticMod Mod { get; }      // The entry placed on the peptide
            public string Name { get; }        // Sibling entry names joined, for keys and tooltips
            public long MassShiftKey { get; }  // Identity of the ambiguity
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
