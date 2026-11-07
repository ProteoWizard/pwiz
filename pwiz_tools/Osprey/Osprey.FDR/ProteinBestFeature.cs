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
using pwiz.Osprey.Core;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// The protein-aware training column (<c>OSPREY_PROTEIN_BEST</c>, issue #4580): for one run and
    /// a round of prior scores, what the OTHER peptides on a candidate's protein scored. A pure
    /// function of its inputs, independent of row order, so that it can be tested alone and two
    /// runs of it are the same bits at any thread count.
    ///
    /// <para>For a candidate on protein P: the prior scores of the other peptides on P in the same
    /// run, every precursor of the candidate's own peptide excluded so it never reads itself, each
    /// peptide reduced to its best precursor. The value is their maximum, or the mean of the top N
    /// padded with the floor when fewer than N exist. The floor is the median of the run's decoy
    /// precursor scores, the same for every candidate, and also the value when there is no sibling.
    /// A shared peptide takes the largest value across its accessions.</para>
    ///
    /// <para>Paired (the default): a decoy reads exactly the value computed for its paired target's
    /// peptide from TARGET sibling scores, so it gets the same evidence a false target would at that
    /// spot. Own: a decoy's value comes from decoy sibling scores on the same accessions, and a
    /// target's from target scores. Entrapment peptides are targets, on whatever accession the
    /// library gives them.</para>
    /// </summary>
    public static class ProteinBestFeature
    {
        /// <summary>The column's name in the model, the diagnostics and the feature reports.</summary>
        public const string FEATURE_NAME = @"PROTEIN_BEST";

        /// <summary>
        /// One run's column: a value for each row of <paramref name="baseIds"/>, whose prior scores
        /// are <paramref name="priorScores"/>. Rows may repeat a precursor (a run's first-pass rows
        /// hold several candidate peaks) and may come in any order. A precursor with no row has no
        /// score, so it cannot appear as a sibling.
        /// </summary>
        public static double[] ComputeColumn(ProteinBestMembership membership, int topN, bool decoyOwn,
            IReadOnlyList<uint> baseIds, IReadOnlyList<bool> isDecoys, IReadOnlyList<double> priorScores)
        {
            if (topN < 1)
                throw new ArgumentOutOfRangeException(nameof(topN), @"The protein-best column needs at least the top sibling.");
            int count = baseIds.Count;
            double floor = DecoyMedianFloor(baseIds, isDecoys, priorScores);
            var targets = new SideValues(membership, topN, floor, BestByPeptide(membership, baseIds, isDecoys, priorScores, false));
            var decoys = decoyOwn
                ? new SideValues(membership, topN, floor, BestByPeptide(membership, baseIds, isDecoys, priorScores, true))
                : targets;
            var column = new double[count];
            for (int r = 0; r < count; r++)
            {
                int peptide = membership.PeptideOf(baseIds[r]);
                column[r] = peptide < 0
                    ? floor
                    : (isDecoys[r] ? decoys : targets).ValueFor(peptide);
            }
            return column;
        }

        /// <summary>
        /// The floor of one run: the exact median of the decoy precursors' best scores in it, the
        /// mean of the middle two for an even count. A precursor counts once however many candidate
        /// peaks it has, the same footing as the sibling scores it pads. A run with no decoy has no
        /// such median, so it takes the run's lowest score (or 0 for an empty run): no evidence.
        /// </summary>
        internal static double DecoyMedianFloor(IReadOnlyList<uint> baseIds, IReadOnlyList<bool> isDecoys,
            IReadOnlyList<double> priorScores)
        {
            var bestDecoy = new Dictionary<uint, double>();
            double lowest = double.PositiveInfinity;
            for (int r = 0; r < baseIds.Count; r++)
            {
                double score = priorScores[r];
                if (TotalOrder.Greater(lowest, score))
                    lowest = score;
                if (!isDecoys[r])
                    continue;
                if (!bestDecoy.TryGetValue(baseIds[r], out double best) || TotalOrder.Greater(score, best))
                    bestDecoy[baseIds[r]] = score;
            }
            if (bestDecoy.Count == 0)
                return baseIds.Count == 0 ? 0.0 : lowest;
            var sorted = bestDecoy.Values.ToArray();
            Array.Sort(sorted); // Array.Sort OK: single primitive array sorted for a median; equal values are interchangeable, and sorting erases the dictionary's enumeration order
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) * 0.5;
        }

        /// <summary>The trained feature metadata with this column appended.</summary>
        public static OspreyFeatureInfo[] WithColumnInfo(OspreyFeatureInfo[] featureInfos)
        {
            if (featureInfos == null)
                return null;
            var extended = new OspreyFeatureInfo[featureInfos.Length + 1];
            Array.Copy(featureInfos, extended, featureInfos.Length);
            // No label of its own: the reports then show the name, the one a reader can look up.
            extended[featureInfos.Length] = new OspreyFeatureInfo(FEATURE_NAME, null, false);
            return extended;
        }

        /// <summary>
        /// Each peptide's best row score on one side (targets or decoys), or null when it has no
        /// row there. By the total order, so the result does not depend on the rows' order.
        /// </summary>
        private static double?[] BestByPeptide(ProteinBestMembership membership, IReadOnlyList<uint> baseIds,
            IReadOnlyList<bool> isDecoys, IReadOnlyList<double> priorScores, bool decoySide)
        {
            var best = new double?[membership.PeptideCount];
            for (int r = 0; r < baseIds.Count; r++)
            {
                if (isDecoys[r] != decoySide)
                    continue;
                int peptide = membership.PeptideOf(baseIds[r]);
                if (peptide < 0)
                    continue;
                double score = priorScores[r];
                if (!best[peptide].HasValue || TotalOrder.Greater(score, best[peptide].Value))
                    best[peptide] = score;
            }
            return best;
        }

        /// <summary>
        /// One side's protein tallies for a run: each protein's best N+1 peptide scores, which is
        /// enough for any peptide on it to read the top N without itself. Peptides are visited in
        /// ascending index and scores compared by the total order, so a protein's tally is one
        /// fixed list whatever order the rows came in. Sums run from the highest score down, a
        /// fixed order, because float addition is order-sensitive.
        /// </summary>
        private sealed class SideValues
        {
            private readonly ProteinBestMembership _membership;
            private readonly int _topN;
            private readonly double _floor;
            private readonly int _width;
            private readonly double[] _scores;
            private readonly int[] _peptides;
            private readonly int[] _counts;
            private readonly double?[] _values;

            public SideValues(ProteinBestMembership membership, int topN, double floor, double?[] bestByPeptide)
            {
                _membership = membership;
                _topN = topN;
                _floor = floor;
                _width = topN + 1;
                int proteins = membership.ProteinCount;
                _scores = new double[proteins * _width];
                _peptides = new int[proteins * _width];
                _counts = new int[proteins];
                _values = new double?[membership.PeptideCount];
                for (int peptide = 0; peptide < bestByPeptide.Length; peptide++)
                {
                    if (!bestByPeptide[peptide].HasValue)
                        continue;
                    foreach (int protein in membership.ProteinsOf(peptide))
                        AddToTally(protein, peptide, bestByPeptide[peptide].Value);
                }
            }

            /// <summary>The value of a candidate on this peptide; a peptide on no protein has the floor.</summary>
            public double ValueFor(int peptide)
            {
                double? known = _values[peptide];
                if (known.HasValue)
                    return known.Value;
                double? best = null;
                foreach (int protein in _membership.ProteinsOf(peptide))
                {
                    double value = ValueOnProtein(protein, peptide);
                    if (!best.HasValue || TotalOrder.Greater(value, best.Value))
                        best = value;
                }
                double result = best ?? _floor;
                _values[peptide] = result;
                return result;
            }

            private double ValueOnProtein(int protein, int peptide)
            {
                int start = protein * _width;
                double sum = 0;
                int taken = 0;
                for (int i = 0; i < _counts[protein] && taken < _topN; i++)
                {
                    if (_peptides[start + i] == peptide)
                        continue;
                    sum += _scores[start + i];
                    taken++;
                }
                if (taken == 0)
                    return _floor;
                for (; taken < _topN; taken++)
                    sum += _floor;
                return sum / _topN;
            }

            private void AddToTally(int protein, int peptide, double score)
            {
                int start = protein * _width;
                int count = _counts[protein];
                // Strictly greater: the earlier peptide keeps its place on a tie, and a score below
                // a full tally is dropped.
                int place = count;
                while (place > 0 && TotalOrder.Greater(score, _scores[start + place - 1]))
                    place--;
                if (place >= _width)
                    return;
                int last = Math.Min(count, _width - 1);
                for (int i = last; i > place; i--)
                {
                    _scores[start + i] = _scores[start + i - 1];
                    _peptides[start + i] = _peptides[start + i - 1];
                }
                _scores[start + place] = score;
                _peptides[start + place] = peptide;
                _counts[protein] = Math.Min(count + 1, _width);
            }
        }
    }

    /// <summary>What a first pass asked of the protein-aware column.</summary>
    public sealed class ProteinBestRequest
    {
        public ProteinBestRequest(ProteinBestMembership membership, int topN, bool decoyOwn)
        {
            Membership = membership;
            TopN = topN;
            DecoyOwn = decoyOwn;
        }

        public ProteinBestMembership Membership { get; }

        /// <summary>1 takes the top sibling; N >= 2 the mean of the top N.</summary>
        public int TopN { get; }

        /// <summary>A decoy reads decoy siblings, not its paired target's value.</summary>
        public bool DecoyOwn { get; }
    }

    /// <summary>
    /// Which peptides sit on which proteins, reduced to indices in a canonical order: peptides and
    /// accessions by ordinal name, so the indices depend on no input order. A peptide is a modified
    /// sequence, so its charge states are one peptide; a decoy shares its target's base_id, so it
    /// reads the same entry. Immutable once built, so the file lanes share one.
    /// </summary>
    public sealed class ProteinBestMembership
    {
        private readonly Dictionary<uint, int> _peptideByBaseId;
        private readonly int[][] _proteinsByPeptide;

        /// <summary>One target library entry: its base_id, modified sequence and accessions.</summary>
        public readonly struct Precursor
        {
            public uint BaseId { get; }
            public string Peptide { get; }
            public IReadOnlyList<string> Proteins { get; }

            public Precursor(uint entryId, string peptide, IReadOnlyList<string> proteins)
            {
                BaseId = entryId & PercolatorEntry.BASE_ID_MASK;
                Peptide = peptide;
                Proteins = proteins;
            }
        }

        /// <summary>
        /// The membership of <paramref name="precursors"/>, any order. With
        /// <paramref name="shuffleSeed"/> (the negative control,
        /// <c>OSPREY_PROTEIN_BEST_SHUFFLE</c>), each peptide takes the accession list of another by
        /// a seeded permutation of the peptides, so every protein keeps its peptide count, and
        /// every peptide's protein evidence is unrelated to it.
        /// </summary>
        public static ProteinBestMembership Build(IEnumerable<Precursor> precursors, ulong? shuffleSeed = null)
        {
            var accessionsByPeptide = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var peptideByBaseId = new Dictionary<uint, string>();
            foreach (var precursor in precursors)
            {
                peptideByBaseId[precursor.BaseId] = precursor.Peptide;
                if (!accessionsByPeptide.TryGetValue(precursor.Peptide, out var accessions))
                    accessionsByPeptide[precursor.Peptide] = accessions = new List<string>();
                if (precursor.Proteins != null)
                    accessions.AddRange(precursor.Proteins);
            }

            var peptides = accessionsByPeptide.Keys.ToArray();
            Array.Sort(peptides, StringComparer.Ordinal); // Array.Sort OK: distinct dictionary keys, so the ordinal comparer never ties
            var indexOfPeptide = new Dictionary<string, int>(peptides.Length, StringComparer.Ordinal);
            for (int p = 0; p < peptides.Length; p++)
                indexOfPeptide[peptides[p]] = p;

            var allAccessions = accessionsByPeptide.Values.SelectMany(a => a).Distinct(StringComparer.Ordinal).ToArray();
            Array.Sort(allAccessions, StringComparer.Ordinal); // Array.Sort OK: Distinct leaves no two equal strings, so the ordinal comparer never ties
            var indexOfAccession = new Dictionary<string, int>(allAccessions.Length, StringComparer.Ordinal);
            for (int a = 0; a < allAccessions.Length; a++)
                indexOfAccession[allAccessions[a]] = a;

            var proteinsByPeptide = new int[peptides.Length][];
            for (int p = 0; p < peptides.Length; p++)
            {
                proteinsByPeptide[p] = accessionsByPeptide[peptides[p]]
                    .Select(accession => indexOfAccession[accession]).Distinct().OrderBy(a => a).ToArray();
            }
            if (shuffleSeed.HasValue)
                proteinsByPeptide = Shuffled(proteinsByPeptide, shuffleSeed.Value);

            var byBaseId = new Dictionary<uint, int>(peptideByBaseId.Count);
            foreach (var kvp in peptideByBaseId)
                byBaseId[kvp.Key] = indexOfPeptide[kvp.Value];
            return new ProteinBestMembership(byBaseId, proteinsByPeptide, allAccessions.Length);
        }

        private ProteinBestMembership(Dictionary<uint, int> peptideByBaseId, int[][] proteinsByPeptide, int proteinCount)
        {
            _peptideByBaseId = peptideByBaseId;
            _proteinsByPeptide = proteinsByPeptide;
            ProteinCount = proteinCount;
        }

        /// <summary>Distinct peptides.</summary>
        public int PeptideCount => _proteinsByPeptide.Length;

        /// <summary>Distinct accessions.</summary>
        public int ProteinCount { get; }

        /// <summary>The peptide index of a base_id, or -1 when the library has no target for it.</summary>
        public int PeptideOf(uint baseId)
        {
            return _peptideByBaseId.TryGetValue(baseId, out int peptide) ? peptide : -1;
        }

        /// <summary>The ascending protein indices of a peptide. Empty for one on no protein.</summary>
        public IReadOnlyList<int> ProteinsOf(int peptide)
        {
            return _proteinsByPeptide[peptide];
        }

        private static int[][] Shuffled(int[][] proteinsByPeptide, ulong seed)
        {
            int n = proteinsByPeptide.Length;
            var order = new int[n];
            for (int i = 0; i < n; i++)
                order[i] = i;
            // Salted so the permutation is not correlated with the other uses of the FDR seed.
            var rng = new XorShift64(seed ^ 0x9E3779B97F4A7C15UL);
            for (int i = n - 1; i >= 1; i--)
            {
                int j = (int)(rng.Next() % (ulong)(i + 1));
                (order[i], order[j]) = (order[j], order[i]);
            }
            var shuffled = new int[n][];
            for (int i = 0; i < n; i++)
                shuffled[i] = proteinsByPeptide[order[i]];
            return shuffled;
        }
    }
}
