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
    /// library, for a library that does not state them (a BiblioSpec .blib).
    ///
    /// <list type="number">
    /// <item><b>Most intense first:</b> each peak takes the preferred primary b or y ion within
    /// the search fragment tolerance that no peak has claimed. Each ion types one peak (as
    /// Skyline's library ranking lets each predicted ion match one peak), so two peaks at one m/z
    /// - a library listing <c>b2</c> and <c>b4^2</c> as two peaks - take the two ions rather than
    /// one twice.</item>
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
    /// is one extraction counts as y6; typing runs once per library, at the configured tolerance,
    /// before any run's MS2 calibration sets that run's extraction tolerance. A peak no
    /// unclaimed candidate reaches stays <see cref="IonType.Unknown"/>, which decoy generation
    /// copies unchanged.</para>
    ///
    /// <para>Typing runs over every peak of every library precursor - 4.8 million peaks of a
    /// 242,841-precursor library, 6.2 million precursors at Astral scale - so the per-peak path
    /// allocates nothing: the candidate window is scanned twice (nearest, then preferred) rather
    /// than collected and sorted. The rules above are a total order, so this is the same choice
    /// the sorted form made.</para>
    /// </summary>
    public static class FragmentTyping
    {
        /// <summary>
        /// Ions whose distances to a peak differ by less than this (Th) are equally near. Isobaric
        /// ions (<c>b2</c> and <c>b4^2</c> of one peptide) differ by at most a few 1e-6 Th of
        /// floating-point rounding; distinct ions of tryptic peptides come within 1e-4 Th of each
        /// other, so a wider window would let the charge preference override the nearer ion.
        /// </summary>
        public const double TIE_TOLERANCE = 1e-5;

        /// <summary>
        /// Types <paramref name="fragments"/> of the peptide <paramref name="sequence"/> carrying
        /// <paramref name="modifications"/> at <paramref name="precursorCharge"/>, in place, by the
        /// rules in the class summary. A typed peak gets the ion's type, ordinal and charge with no
        /// loss; any other keeps its annotation. Counts go to <paramref name="stats"/> when it is
        /// not null.
        /// </summary>
        public static void TypeFragments(string sequence, IEnumerable<Modification> modifications,
            int precursorCharge, LibraryFragment[] fragments, FragmentToleranceConfig tolerance,
            FragmentTypingStats stats)
        {
            if (fragments.Length == 0)
                return;
            var candidates = new FragmentCandidates(sequence, modifications, precursorCharge);
            var claimed = new bool[candidates.SlotCount];
            int typed = 0, untyped = 0;
            foreach (int i in IntensityOrder(fragments))
            {
                int slot = candidates.PreferredSlot(fragments[i].Mz, tolerance, claimed);
                if (slot < 0)
                {
                    untyped++;
                    continue;
                }
                fragments[i].Annotation = candidates.AnnotationOf(slot);
                claimed[slot] = true;
                typed++;
            }
            stats?.Add(typed, untyped);
        }

        /// <summary>
        /// The ion Osprey's own typing gives each of <paramref name="fragments"/>, or null - the
        /// rules of <see cref="TypeFragments"/>, leaving the fragments as they are - for comparing
        /// with the types a library states.
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

        /// <summary>
        /// The indexes of <paramref name="fragments"/> most intense first, so the ion a peak
        /// claims goes to the strongest peak it explains. Ties go to the lower m/z rather than
        /// the earlier row, so the typing does not depend on the order the library lists its
        /// peaks in (peaks equal in both are interchangeable), and then to the earlier row, so
        /// the order is total.
        /// </summary>
        private static int[] IntensityOrder(LibraryFragment[] fragments)
        {
            var order = new int[fragments.Length];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            Array.Sort(order, (a, b) => // Array.Sort OK: the index is the last key, so the order is total
            {
                int byIntensity = fragments[b].RelativeIntensity.CompareTo(fragments[a].RelativeIntensity);
                if (byIntensity != 0)
                    return byIntensity;
                int byMz = fragments[a].Mz.CompareTo(fragments[b].Mz);
                return byMz != 0 ? byMz : a.CompareTo(b);
            });
            return order;
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
        // The applicable slots by m/z: the m/z values and, at the same index, the slot of each.
        private readonly double[] _sortedMz;
        private readonly int[] _sortedSlot;

        public FragmentCandidates(string sequence, IEnumerable<Modification> modifications, int precursorCharge)
        {
            _length = sequence?.Length ?? 0;
            _ladder = FragmentLadder.Build(sequence, modifications, precursorCharge);
            int count = 0;
            for (int slot = 0; slot < _ladder.Length; slot++)
            {
                if (!double.IsNaN(_ladder[slot]))
                    count++;
            }
            _sortedMz = new double[count];
            _sortedSlot = new int[count];
            count = 0;
            for (int slot = 0; slot < _ladder.Length; slot++)
            {
                if (double.IsNaN(_ladder[slot]))
                    continue;
                _sortedMz[count] = _ladder[slot];
                _sortedSlot[count++] = slot;
            }
            // The order among equal m/z does not matter: a peak's window scan visits every
            // candidate in reach, and the preference among them is a total order of its own.
            Array.Sort(_sortedMz, _sortedSlot); // Array.Sort OK: equal keys are interchangeable here, see above
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
        /// The slot of the preferred unclaimed candidate within tolerance of the peak, or -1.
        /// A ppm tolerance is taken at the candidate's m/z, as extraction takes it.
        /// </summary>
        public int PreferredSlot(double peakMz, FragmentToleranceConfig tolerance, bool[] claimed)
        {
            if (_sortedMz.Length == 0 || !double.IsFinite(peakMz))
                return -1;
            double reach = tolerance.ToleranceDa(peakMz) * 1.01;
            int first = LowerBound(peakMz - reach);
            double high = peakMz + reach;
            // The nearest unclaimed candidate in reach, then the preferred of those equally
            // near it: the nearest, taking those within FragmentTyping.TIE_TOLERANCE of it as
            // equally near; then the lower fragment charge; then y before b; then the shorter
            // ion. Two passes over the window and no list, since typing runs over millions of
            // peaks.
            double nearest = double.PositiveInfinity;
            for (int i = first; i < _sortedMz.Length && _sortedMz[i] <= high; i++)
            {
                if (!claimed[_sortedSlot[i]] && tolerance.WithinTolerance(_sortedMz[i], peakMz))
                    nearest = Math.Min(nearest, Math.Abs(_sortedMz[i] - peakMz));
            }
            if (double.IsPositiveInfinity(nearest))
                return -1;
            int best = -1;
            for (int i = first; i < _sortedMz.Length && _sortedMz[i] <= high; i++)
            {
                int slot = _sortedSlot[i];
                if (claimed[slot] || !tolerance.WithinTolerance(_sortedMz[i], peakMz) ||
                    Math.Abs(_sortedMz[i] - peakMz) - nearest >= FragmentTyping.TIE_TOLERANCE)
                {
                    continue;
                }
                if (best < 0 || Prefer(slot, best))
                    best = slot;
            }
            return best;
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
        /// Whether <paramref name="slot"/> is preferred to <paramref name="other"/> among ions
        /// equally near a peak: the lower fragment charge, then y before b, then the shorter ion.
        /// </summary>
        private bool Prefer(int slot, int other)
        {
            int byCharge = FragmentLadder.ChargeOf(slot).CompareTo(FragmentLadder.ChargeOf(other));
            if (byCharge != 0)
                return byCharge < 0;
            int byType = TypeRank(slot).CompareTo(TypeRank(other));
            if (byType != 0)
                return byType < 0;
            return FragmentLadder.OrdinalOf(slot, _length) < FragmentLadder.OrdinalOf(other, _length);
        }

        private static int TypeRank(int slot)
        {
            return FragmentLadder.IonTypeOf(slot) == IonType.Y ? 0 : 1;
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
            int lo = 0, hi = _sortedMz.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (_sortedMz[mid] < mz)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }
    }

    /// <summary>
    /// What <see cref="FragmentTyping.TypeFragments"/> did across a whole library. Counted from
    /// parallel loads, so the counts are updated atomically, once per spectrum.
    /// </summary>
    public sealed class FragmentTypingStats
    {
        private long _typed;
        private long _untyped;

        /// <summary>Peaks typed as a b or y ion.</summary>
        public long Typed => Interlocked.Read(ref _typed);

        /// <summary>Peaks no unclaimed candidate reaches.</summary>
        public long Untyped => Interlocked.Read(ref _untyped);

        public long Total => Typed + Untyped;

        /// <summary>Counts one spectrum's peaks: <paramref name="typed"/> given an ion, <paramref name="untyped"/> left Unknown.</summary>
        public void Add(int typed, int untyped)
        {
            Interlocked.Add(ref _typed, typed);
            Interlocked.Add(ref _untyped, untyped);
        }
    }
}
