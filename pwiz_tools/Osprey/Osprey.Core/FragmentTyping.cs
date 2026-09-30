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
    /// Types a library spectrum's peaks from m/z against the peptide's own ions, as Skyline does
    /// for a library: Osprey computes which ion a peak is rather than taking a library's word for
    /// it.
    ///
    /// <list type="bullet">
    /// <item>Candidates are the primary b and y ions of <see cref="FragmentLadder"/> - fragment
    /// charge 1 to min(precursor charge, 2), no neutral losses. Every allowed loss would add
    /// candidate m/z and random matches; a loss, if one is ever added, belongs to the ion or the
    /// modification that makes it and is added in <see cref="FragmentLadder"/>, the one place
    /// candidates are built.</item>
    /// <item>A peak takes the nearest candidate within the SEARCH fragment tolerance - the
    /// tolerance chromatogram extraction uses - so a peak Osprey calls y6 is one extraction would
    /// count as y6. The tolerance is the configured one: typing runs once per library, before any
    /// run's MS2 calibration narrows extraction, so it is the upper bound of what extraction
    /// uses.</item>
    /// <item>Two candidates as near as each other, within <see cref="TIE_TOLERANCE"/>, leave the
    /// peak Unknown rather than guessing: <c>b6</c> and <c>b12^2</c> of a peptide whose first
    /// twelve residues repeat its first six sum to one m/z.</item>
    /// <item>A peak no candidate reaches stays <see cref="IonType.Unknown"/>, which decoy
    /// generation copies unchanged.</item>
    /// </list>
    /// </summary>
    public static class FragmentTyping
    {
        /// <summary>
        /// Distances to the two nearest candidates closer than this (Th) are a tie. Isobaric ions
        /// sum to the same m/z within floating-point rounding, far below it; distinct ions a
        /// search could tell apart differ by far more.
        /// </summary>
        public const double TIE_TOLERANCE = 1e-3;

        /// <summary>
        /// Types <paramref name="fragments"/> of the peptide <paramref name="sequence"/> carrying
        /// <paramref name="modifications"/> at <paramref name="precursorCharge"/>, in place: a
        /// typed peak gets the ion's type, ordinal and charge with no loss; any other keeps its
        /// annotation. Counts go to <paramref name="stats"/> when it is not null.
        /// </summary>
        public static void TypeFragments(string sequence, IEnumerable<Modification> modifications,
            int precursorCharge, LibraryFragment[] fragments, FragmentToleranceConfig tolerance,
            FragmentTypingStats stats)
        {
            if (fragments.Length == 0)
                return;
            var candidates = Candidates(sequence, modifications, precursorCharge);
            for (int i = 0; i < fragments.Length; i++)
            {
                int slot = NearestSlot(candidates, fragments[i].Mz, tolerance, null, out bool tie);
                if (slot >= 0)
                {
                    fragments[i].Annotation = AnnotationOf(slot, sequence.Length);
                    stats?.CountTyped();
                }
                else
                {
                    stats?.CountUntyped(tie);
                }
            }
        }

        /// <summary>
        /// What Osprey computes for each of <paramref name="fragments"/> - the same rule as
        /// <see cref="TypeFragments"/>, leaving the fragments as they are - for comparing with
        /// the types a library states: the ion, or for a tie the ions it could not tell apart,
        /// or neither.
        /// </summary>
        public static ComputedType[] Compute(string sequence, IEnumerable<Modification> modifications,
            int precursorCharge, IReadOnlyList<LibraryFragment> fragments, FragmentToleranceConfig tolerance)
        {
            var computed = new ComputedType[fragments.Count];
            if (fragments.Count == 0)
                return computed;
            var candidates = Candidates(sequence, modifications, precursorCharge);
            var tiedSlots = new List<int>();
            for (int i = 0; i < fragments.Count; i++)
            {
                int slot = NearestSlot(candidates, fragments[i].Mz, tolerance, tiedSlots, out bool tie);
                if (slot >= 0)
                    computed[i] = new ComputedType(AnnotationOf(slot, sequence.Length), null);
                else if (tie)
                    computed[i] = new ComputedType(null, tiedSlots.Select(s => AnnotationOf(s, sequence.Length)).ToArray());
            }
            return computed;
        }

        /// <summary>
        /// Whether <paramref name="annotation"/> is an ion typing can produce: a b or y ion with
        /// no loss at charge 1 or 2. A library stating anything else is outside the model, not
        /// in disagreement with it.
        /// </summary>
        public static bool IsPrimary(FragmentAnnotation annotation)
        {
            return (annotation.IonType == IonType.B || annotation.IonType == IonType.Y) &&
                   !annotation.HasNeutralLoss && annotation.Ordinal >= 1 &&
                   annotation.Charge >= 1 && annotation.Charge <= FragmentLadder.MAX_FRAGMENT_CHARGE;
        }

        /// <summary>
        /// The applicable ladder slots, as parallel arrays sorted by m/z, so each peak is placed
        /// by binary search.
        /// </summary>
        private static Candidate[] Candidates(string sequence, IEnumerable<Modification> modifications,
            int precursorCharge)
        {
            double[] ladder = FragmentLadder.Build(sequence, modifications, precursorCharge);
            var candidates = new List<Candidate>(ladder.Length);
            for (int slot = 0; slot < ladder.Length; slot++)
            {
                if (!double.IsNaN(ladder[slot]))
                    candidates.Add(new Candidate(ladder[slot], slot));
            }
            candidates.Sort((a, b) => a.Mz != b.Mz ? a.Mz.CompareTo(b.Mz) : a.Slot.CompareTo(b.Slot)); // Array.Sort OK: slots are unique, so the order is total
            return candidates.ToArray();
        }

        /// <summary>
        /// The slot of the candidate nearest <paramref name="peakMz"/> within tolerance of the
        /// candidate's m/z, or -1 - with <paramref name="tie"/> set, and the tied slots in
        /// <paramref name="tiedSlots"/> when it is not null, when two or more were equally near.
        /// </summary>
        private static int NearestSlot(Candidate[] candidates, double peakMz, FragmentToleranceConfig tolerance,
            List<int> tiedSlots, out bool tie)
        {
            tie = false;
            tiedSlots?.Clear();
            if (candidates.Length == 0 || !double.IsFinite(peakMz))
                return -1;
            // The candidates that could be in reach, scanning outward from the insertion point;
            // a ppm tolerance is taken at the candidate's m/z, as extraction takes it.
            int start = LowerBound(candidates, peakMz);
            double reach = tolerance.ToleranceDa(peakMz) * 1.01 + TIE_TOLERANCE;
            int first = start;
            while (first > 0 && peakMz - candidates[first - 1].Mz <= reach)
                first--;
            int end = start;
            while (end < candidates.Length && candidates[end].Mz - peakMz <= reach)
                end++;

            int best = -1;
            double bestDistance = double.MaxValue;
            for (int i = first; i < end; i++)
            {
                double distance = Math.Abs(candidates[i].Mz - peakMz);
                if (distance < bestDistance && tolerance.WithinTolerance(candidates[i].Mz, peakMz))
                {
                    bestDistance = distance;
                    best = candidates[i].Slot;
                }
            }
            if (best < 0)
                return -1;
            int nearest = 0;
            for (int i = first; i < end; i++)
            {
                if (Math.Abs(candidates[i].Mz - peakMz) - bestDistance < TIE_TOLERANCE &&
                    tolerance.WithinTolerance(candidates[i].Mz, peakMz))
                {
                    nearest++;
                    tiedSlots?.Add(candidates[i].Slot);
                }
            }
            if (nearest > 1)
            {
                tie = true;
                return -1;
            }
            tiedSlots?.Clear();
            return best;
        }

        /// <summary>The first index whose m/z is at least <paramref name="mz"/>.</summary>
        private static int LowerBound(Candidate[] candidates, double mz)
        {
            int lo = 0, hi = candidates.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (candidates[mid].Mz < mz)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        private static FragmentAnnotation AnnotationOf(int slot, int length)
        {
            return new FragmentAnnotation
            {
                IonType = FragmentLadder.IonTypeOf(slot),
                Ordinal = (byte)FragmentLadder.OrdinalOf(slot, length),
                Charge = (byte)FragmentLadder.ChargeOf(slot),
                NeutralLoss = NeutralLossCode.None
            };
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
    /// What <see cref="FragmentTyping.Compute"/> found for one peak: the ion it types the peak
    /// as, or the ions it could not tell apart, or neither.
    /// </summary>
    public readonly struct ComputedType
    {
        public ComputedType(FragmentAnnotation? ion, FragmentAnnotation[] tiedIons)
        {
            Ion = ion;
            TiedIons = tiedIons;
        }

        /// <summary>The ion the peak is typed as, or null.</summary>
        public FragmentAnnotation? Ion { get; }

        /// <summary>The equally near ions that left the peak untyped, or null for no tie.</summary>
        public FragmentAnnotation[] TiedIons { get; }
    }

    /// <summary>
    /// What <see cref="FragmentTyping.TypeFragments"/> did across a whole library. Counted from
    /// parallel loads, so the counts are updated atomically.
    /// </summary>
    public sealed class FragmentTypingStats
    {
        private long _typed;
        private long _untyped;
        private long _ties;

        /// <summary>Peaks typed as a primary b or y ion.</summary>
        public long Typed => Interlocked.Read(ref _typed);

        /// <summary>Peaks no candidate typed, ties included.</summary>
        public long Untyped => Interlocked.Read(ref _untyped);

        /// <summary>Peaks left Unknown because two candidates were equally near.</summary>
        public long Ties => Interlocked.Read(ref _ties);

        public void CountTyped()
        {
            Interlocked.Increment(ref _typed);
        }

        public void CountUntyped(bool tie)
        {
            Interlocked.Increment(ref _untyped);
            if (tie)
                Interlocked.Increment(ref _ties);
        }
    }
}
