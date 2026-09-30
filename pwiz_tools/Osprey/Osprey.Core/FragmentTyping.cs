/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Threading;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Types a library spectrum's peaks against the peptide's own ions, as Skyline does for a
    /// library, accepting what the library states where Osprey agrees it is possible.
    ///
    /// <list type="number">
    /// <item><b>The library's word, where possible.</b> A peak whose library-stated ion is one
    /// Osprey can produce - a primary b or y ion (<see cref="IsPrimary"/>) whose m/z is within the
    /// search fragment tolerance of the peak - takes that ion. The library may know what Osprey
    /// cannot: a stable isotope label resolves the b/y isobars an unlabeled peptide has, and one on
    /// the third or fourth residue separates <c>b2</c> from <c>b4^2</c>. A stated ion out of
    /// tolerance, a neutral loss or another ion type is outside what Osprey accepts, and the peak is
    /// typed as in 2.</item>
    /// <item><b>Osprey's own typing</b> for every other peak, most intense first: the preferred
    /// primary b or y ion within the search fragment tolerance that no peak has claimed. Each ion
    /// types one peak (as Skyline's library ranking lets each predicted ion match one peak), so
    /// two peaks at one m/z - a library listing <c>b2</c> and <c>b4^2</c> as two peaks - take the
    /// two ions rather than one twice.</item>
    /// <item><b>Preference among candidates:</b> the nearest, counting ions within
    /// <see cref="TIE_TOLERANCE"/> of each other as equally near; then the lower fragment charge
    /// (a <c>b2</c> over a <c>b4^2</c> at one m/z); then y before b, then the shorter ion, so
    /// the choice is always made. A peak with any candidate in reach is never left untyped: left
    /// Unknown, a peak reads as not produced by the peptide's fragmentation at all, which is worse
    /// than a possibly wrong choice between ions that each could have produced it.</item>
    /// </list>
    ///
    /// <para>Candidates are the primary b and y ions of <see cref="FragmentLadder"/> - fragment
    /// charge 1 to min(precursor charge, 2), no neutral losses; a loss, if one is ever added,
    /// belongs to the residue or modification that makes it and is added there. The tolerance is
    /// the search's fragment tolerance, the one chromatogram extraction uses, so a peak typed y6
    /// is one extraction counts as y6; typing runs once per library, before any run's MS2
    /// calibration narrows extraction, so it is the upper bound of what extraction uses. A peak no
    /// unclaimed candidate reaches stays <see cref="IonType.Unknown"/>, which decoy generation
    /// copies unchanged.</para>
    /// </summary>
    public static class FragmentTyping
    {
        /// <summary>
        /// Ions whose distances to a peak differ by less than this (Th) are equally near: isobaric
        /// ions sum to one m/z within floating-point rounding, far below it.
        /// </summary>
        public const double TIE_TOLERANCE = 1e-3;

        /// <summary>
        /// Types <paramref name="fragments"/> of the peptide <paramref name="sequence"/> carrying
        /// <paramref name="modifications"/> at <paramref name="precursorCharge"/>, in place, by the
        /// rules in the class summary. <paramref name="stated"/>, when not null, holds per peak the
        /// ions the library states for it (null or empty for none). A typed peak gets the ion's
        /// type, ordinal and charge with no loss; any other keeps its annotation. Counts go to
        /// <paramref name="stats"/> when it is not null.
        /// </summary>
        public static void TypeFragments(string sequence, IEnumerable<Modification> modifications,
            int precursorCharge, LibraryFragment[] fragments, FragmentToleranceConfig tolerance,
            FragmentTypingStats stats, IReadOnlyList<FragmentAnnotation>[] stated = null)
        {
            if (fragments.Length == 0)
                return;
            var candidates = new FragmentCandidates(sequence, modifications, precursorCharge);
            var claimed = new bool[candidates.SlotCount];
            var typed = new bool[fragments.Length];
            if (stated != null)
            {
                for (int i = 0; i < fragments.Length; i++)
                {
                    int slot = candidates.PreferredStatedSlot(stated[i], fragments[i].Mz, tolerance);
                    if (slot < 0)
                        continue;
                    fragments[i].Annotation = candidates.AnnotationOf(slot);
                    claimed[slot] = true;
                    typed[i] = true;
                    stats?.CountStated();
                }
            }
            // Most intense first, so the ion a peak claims goes to the strongest peak it explains.
            foreach (int i in Enumerable.Range(0, fragments.Length)
                         .OrderByDescending(i => fragments[i].RelativeIntensity).ThenBy(i => i))
            {
                if (typed[i])
                    continue;
                int slot = candidates.PreferredSlot(fragments[i].Mz, tolerance, claimed);
                if (slot < 0)
                {
                    stats?.CountUntyped();
                    continue;
                }
                fragments[i].Annotation = candidates.AnnotationOf(slot);
                claimed[slot] = true;
                stats?.CountTyped();
            }
        }

        /// <summary>
        /// The ion Osprey's own typing gives each of <paramref name="fragments"/>, or null - the
        /// rules of <see cref="TypeFragments"/> with no library statement, leaving the fragments as
        /// they are - for comparing with the types a library states.
        /// </summary>
        public static FragmentAnnotation?[] Compute(string sequence, IEnumerable<Modification> modifications,
            int precursorCharge, IReadOnlyList<LibraryFragment> fragments, FragmentToleranceConfig tolerance)
        {
            var copy = fragments.Select(f => new LibraryFragment
            {
                Mz = f.Mz,
                RelativeIntensity = f.RelativeIntensity,
                Annotation = new FragmentAnnotation { IonType = IonType.Unknown }
            }).ToArray();
            TypeFragments(sequence, modifications, precursorCharge, copy, tolerance, null);
            return copy.Select(f => f.Annotation.IonType == IonType.Unknown ? (FragmentAnnotation?)null : f.Annotation)
                .ToArray();
        }

        /// <summary>
        /// Whether <paramref name="annotation"/> is an ion typing can produce: a b or y ion with
        /// no loss at charge 1 or 2. A library stating anything else is outside the model.
        /// </summary>
        public static bool IsPrimary(FragmentAnnotation annotation)
        {
            return (annotation.IonType == IonType.B || annotation.IonType == IonType.Y) &&
                   !annotation.HasNeutralLoss && annotation.Ordinal >= 1 &&
                   annotation.Charge >= 1 && annotation.Charge <= FragmentLadder.MAX_FRAGMENT_CHARGE;
        }
    }

    /// <summary>
    /// The primary b and y ions of one peptide precursor that <see cref="FragmentTyping"/> types
    /// peaks as: the applicable <see cref="FragmentLadder"/> slots, sorted by m/z so each peak is
    /// placed by binary search.
    /// </summary>
    public sealed class FragmentCandidates
    {
        private readonly int _length;
        private readonly double[] _ladder;
        private readonly Candidate[] _sorted;

        public FragmentCandidates(string sequence, IEnumerable<Modification> modifications, int precursorCharge)
        {
            _length = sequence?.Length ?? 0;
            _ladder = FragmentLadder.Build(sequence, modifications, precursorCharge);
            var candidates = new List<Candidate>(_ladder.Length);
            for (int slot = 0; slot < _ladder.Length; slot++)
            {
                if (!double.IsNaN(_ladder[slot]))
                    candidates.Add(new Candidate(_ladder[slot], slot));
            }
            candidates.Sort((a, b) => a.Mz != b.Mz ? a.Mz.CompareTo(b.Mz) : a.Slot.CompareTo(b.Slot)); // Array.Sort OK: slots are unique, so the order is total
            _sorted = candidates.ToArray();
        }

        /// <summary>Slots of the ladder, applicable or not.</summary>
        public int SlotCount => _ladder.Length;

        /// <summary>
        /// Whether <paramref name="ion"/> is a candidate within <paramref name="tolerance"/> of a
        /// peak at <paramref name="peakMz"/> - an ion Osprey agrees could have produced the peak.
        /// </summary>
        public bool IsPossible(FragmentAnnotation ion, double peakMz, FragmentToleranceConfig tolerance)
        {
            return SlotOf(ion, peakMz, tolerance) >= 0;
        }

        /// <summary>
        /// The slot of the preferred ion among <paramref name="stated"/> that is possible for the
        /// peak (<see cref="IsPossible"/>), or -1 when none is.
        /// </summary>
        public int PreferredStatedSlot(IReadOnlyList<FragmentAnnotation> stated, double peakMz,
            FragmentToleranceConfig tolerance)
        {
            if (stated == null || stated.Count == 0)
                return -1;
            var possible = new List<Candidate>(stated.Count);
            foreach (var ion in stated)
            {
                int slot = SlotOf(ion, peakMz, tolerance);
                if (slot >= 0)
                    possible.Add(new Candidate(_ladder[slot], slot));
            }
            return Preferred(possible, peakMz);
        }

        /// <summary>
        /// The slot of the preferred unclaimed candidate within tolerance of the peak, or -1.
        /// A ppm tolerance is taken at the candidate's m/z, as extraction takes it.
        /// </summary>
        public int PreferredSlot(double peakMz, FragmentToleranceConfig tolerance, bool[] claimed)
        {
            if (_sorted.Length == 0 || !double.IsFinite(peakMz))
                return -1;
            double reach = tolerance.ToleranceDa(peakMz) * 1.01;
            var inReach = new List<Candidate>();
            for (int i = LowerBound(peakMz - reach); i < _sorted.Length && _sorted[i].Mz <= peakMz + reach; i++)
            {
                var candidate = _sorted[i];
                if (!claimed[candidate.Slot] && tolerance.WithinTolerance(candidate.Mz, peakMz))
                    inReach.Add(candidate);
            }
            return Preferred(inReach, peakMz);
        }

        public FragmentAnnotation AnnotationOf(int slot)
        {
            return new FragmentAnnotation
            {
                IonType = FragmentLadder.IonTypeOf(slot),
                Ordinal = (byte)FragmentLadder.OrdinalOf(slot, _length),
                Charge = (byte)FragmentLadder.ChargeOf(slot),
                NeutralLoss = NeutralLossCode.None
            };
        }

        /// <summary>
        /// The preferred of <paramref name="candidates"/> for a peak at <paramref name="peakMz"/>:
        /// the nearest, taking those within <see cref="FragmentTyping.TIE_TOLERANCE"/> of it as
        /// equally near; then the lower fragment charge; then y before b; then the shorter ion.
        /// -1 for none.
        /// </summary>
        private int Preferred(List<Candidate> candidates, double peakMz)
        {
            if (candidates.Count == 0)
                return -1;
            double nearest = candidates.Min(c => Math.Abs(c.Mz - peakMz));
            return candidates.Where(c => Math.Abs(c.Mz - peakMz) - nearest < FragmentTyping.TIE_TOLERANCE)
                .OrderBy(c => FragmentLadder.ChargeOf(c.Slot))
                .ThenBy(c => FragmentLadder.IonTypeOf(c.Slot) == IonType.Y ? 0 : 1)
                .ThenBy(c => FragmentLadder.OrdinalOf(c.Slot, _length))
                .First().Slot;
        }

        /// <summary>The slot of a primary <paramref name="ion"/> within tolerance of the peak, or -1.</summary>
        private int SlotOf(FragmentAnnotation ion, double peakMz, FragmentToleranceConfig tolerance)
        {
            if (!FragmentTyping.IsPrimary(ion) || !double.IsFinite(peakMz))
                return -1;
            int slot = FragmentLadder.SlotOf(ion, _length);
            if (slot < 0 || slot >= _ladder.Length || double.IsNaN(_ladder[slot]) ||
                !tolerance.WithinTolerance(_ladder[slot], peakMz))
            {
                return -1;
            }
            return slot;
        }

        /// <summary>The first index whose m/z is at least <paramref name="mz"/>.</summary>
        private int LowerBound(double mz)
        {
            int lo = 0, hi = _sorted.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (_sorted[mid].Mz < mz)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        private readonly struct Candidate
        {
            public Candidate(double mz, int slot)
            {
                Mz = mz;
                Slot = slot;
            }

            public double Mz { get; }
            public int Slot { get; }
        }
    }

    /// <summary>
    /// What <see cref="FragmentTyping.TypeFragments"/> did across a whole library. Counted from
    /// parallel loads, so the counts are updated atomically.
    /// </summary>
    public sealed class FragmentTypingStats
    {
        private long _stated;
        private long _typed;
        private long _untyped;

        /// <summary>Peaks typed as the ion the library states for them.</summary>
        public long Stated => Interlocked.Read(ref _stated);

        /// <summary>Peaks typed by Osprey's own typing.</summary>
        public long Typed => Interlocked.Read(ref _typed);

        /// <summary>Peaks no unclaimed candidate reaches.</summary>
        public long Untyped => Interlocked.Read(ref _untyped);

        public long Total => Stated + Typed + Untyped;

        public void CountStated()
        {
            Interlocked.Increment(ref _stated);
        }

        public void CountTyped()
        {
            Interlocked.Increment(ref _typed);
        }

        public void CountUntyped()
        {
            Interlocked.Increment(ref _untyped);
        }
    }
}
