/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using pwiz.Osprey.Core;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// The streaming (bounded-memory) FDR paths, extracted from
    /// the original <c>PercolatorFdr</c> god class (issue #4468): the shared competition + PEP + per-run /
    /// experiment q-value block that both flat score passes feed, and the
    /// <see cref="StreamingFirstPassQ"/> accumulator.
    ///
    /// These exist because the resident forms do not fit large file counts (issue
    /// #4355): they compute the SAME q-values as the resident path while holding
    /// only the bounded working set - one entry per competition winner rather than
    /// one per observation. That equivalence is what the regression asserts, so the
    /// ordering here is parity-locked and the code moves verbatim.
    /// </summary>
    public static class StreamingFdr
    {
        /// <summary>
        /// The streaming competition + PEP + per-run / experiment q-value math, over
        /// the flat per-observation arrays the score passes produce (the projection-native
        /// <see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/> among them).
        /// Extracted as a single source of truth (issue #4355 step (b) increment iii) so the
        /// buffer shapes cannot drift on the byte-parity-locked ordering. UNCHANGED
        /// math relative to the pre-extraction inline block:
        /// <list type="bullet">
        /// <item>PEP is fed to <see cref="PepEstimator.FitDefault"/> in
        /// <c>base_id</c>-ascending order (risk #6): the KDE sum is non-associative,
        /// so the winner arrays are reordered by <c>entryIds &amp; PercolatorEntry.BASE_ID_MASK</c>
        /// before the fit; the score-sorted arrays stay intact for the q-value
        /// calls.</item>
        /// <item>Per-run q-values group by <paramref name="fileNames"/>; experiment
        /// q-values take the single-file shortcut (clone the per-run arrays) exactly
        /// as the direct path does.</item>
        /// </list>
        /// The six outputs are returned as parallel arrays (index-aligned to the
        /// inputs); the caller either packs them into <see cref="PercolatorResult"/>s
        /// or writes them straight onto the projection rows.
        /// </summary>
        internal static void ComputeStreamingCompetitionQvalues(
            double[] finalScores, bool[] labels, uint[] entryIds,
            string[] peptides, string[] fileNames,
            out double[] peps, out double[] runPrecursorQvalues,
            out double[] runPeptideQvalues, out double[] expPrecursorQvalues,
            out double[] expPeptideQvalues, out double[] peptidePeps)
        {
            int n = finalScores.Length;

            // PEP via global target-decoy competition. The bounded ENTRY_ID->PEP map
            // (base_id-ascending KDE order -- see PercolatorQValues.ComputePepWinnerMap) is
            // expanded to the full per-row peps array here; the projection score pass reads the
            // map directly so the O(n) array is never materialized (issue #4355 Part B).
            //
            // Expanded BY ENTRY, not by winning row: PEP is one value per base_id, so every
            // observation of the entry that won reports it. It used to be written onto the
            // winner's row alone with 1.0 on the rest, which made a per-entry fact look like a
            // per-observation one and is what issue #4486 removed. A row whose entry won nothing
            // still takes 1.0 - it has no posterior error of its own.
            var pepByEntryId = PercolatorQValues.ComputePepWinnerMap(finalScores, labels, entryIds);
            peps = new double[n];
            for (int i = 0; i < n; i++)
                peps[i] = pepByEntryId.TryGetValue(entryIds[i], out double pv) ? pv : 1.0;

            // Per-run precursor + peptide q-values (each file independently).
            runPrecursorQvalues = PercolatorQValues.ComputePerRunPrecursorQvalues(
                finalScores, labels, entryIds, fileNames);
            runPeptideQvalues = PercolatorQValues.ComputePerRunPeptideQvalues(
                finalScores, labels, entryIds, fileNames, peptides);

            // Experiment-level q-values: single-file shortcut matches
            // direct-path semantics. The peptide PEP comes from the experiment peptide
            // competition either way: with one file that competition is the run's own.
            var expPeptideCompetitionQvalues = PercolatorQValues.ComputeExperimentPeptideQvalues(
                finalScores, labels, entryIds, peptides, out peptidePeps);
            var uniqueFiles = new HashSet<string>(fileNames);
            bool isSingleFile = uniqueFiles.Count <= 1;
            if (isSingleFile)
            {
                expPrecursorQvalues = (double[])runPrecursorQvalues.Clone();
                expPeptideQvalues = (double[])runPeptideQvalues.Clone();
            }
            else
            {
                expPrecursorQvalues = PercolatorQValues.ComputeExperimentPrecursorQvalues(
                    finalScores, labels, entryIds);
                expPeptideQvalues = expPeptideCompetitionQvalues;
            }

            // Best-of-runs monotonicity (issue #4390 clamp, memory-bounded flat form): floor
            // each experiment q up to the entry's best (min-over-runs) combined run q. Shared by
            // the FdrEntry streaming path and the projection score pass, so both clamp
            // identically without a resident FdrEntry buffer.
            PercolatorQValues.ClampExperimentQToBestRunFlat(
                entryIds, labels, peptides, runPrecursorQvalues, runPeptideQvalues,
                expPrecursorQvalues, expPeptideQvalues);
        }

        /// <summary>
        /// Streaming builder for the three GLOBAL bounded first-pass q maps (issue #4355
        /// struct-shrink S3, Stage B): the experiment-precursor <c>base_id -&gt; q</c> map, the
        /// experiment-peptide <c>peptide -&gt; q</c> map, and the PEP <c>winner-ordinal -&gt; pep</c>
        /// map -- built by pushing each scored row via <see cref="Add"/> in flat (file,row) order
        /// instead of reading the resident <c>finalScores/labels/entryIds/peptides[n]</c> arrays.
        /// Bounded: it retains only per-base_id and per-peptide bests (O(distinct)), never an O(n)
        /// buffer. Each Build* reuses the SAME <see cref="TargetDecoyCompetition.CompeteFromDicts"/> +
        /// <see cref="PercolatorQValues.ComputeConservativeQvalues"/> (+ <c>PepEstimator</c>) finish the flat
        /// <see cref="PercolatorQValues.ComputeExperimentPrecursorQMap"/> / <see cref="PercolatorQValues.ComputeExperimentPeptideQMap"/>
        /// / <see cref="PercolatorQValues.ComputePepWinnerMap"/> run, so a population fed in the same order yields
        /// byte-identical maps (verified by <c>FdrTest.TestStreamingFirstPassQMatchesFlat</c>). The
        /// PEP map is keyed by the streaming ordinal <c>g</c>, which equals the flat winner index
        /// because both visit rows in the same nested (file,row) order.
        /// </summary>
        internal sealed class StreamingFirstPassQ
        {
            // Global experiment-precursor / PEP competition: base_id -> best (g, score), strict
            // '>' first-seen, split target/decoy -- the identical maps CompeteAll builds.
            private readonly Dictionary<uint, KeyValuePair<int, double>> _precTargets =
                new Dictionary<uint, KeyValuePair<int, double>>();
            private readonly Dictionary<uint, KeyValuePair<int, double>> _precDecoys =
                new Dictionary<uint, KeyValuePair<int, double>>();
            // Experiment-peptide: peptide -> best row, mirroring BestPrecursorPerPeptide.
            private readonly Dictionary<string, PeptideBest> _peptBest =
                new Dictionary<string, PeptideBest>();

            /// <summary>Fold one scored row (in flat (file,row) order) into the bounded bests.</summary>
            public void Add(int g, double score, uint entryId, bool isDecoy, string peptide)
            {
                uint baseId = entryId & PercolatorEntry.BASE_ID_MASK;
                ReduceBest(isDecoy ? _precDecoys : _precTargets, baseId, g, score);
                ReducePeptide(_peptBest, new PeptideBest(g, score, isDecoy, entryId, peptide));
            }

            /// <summary>A reduction for one file's rows, buildable on a file lane; merge it with
            /// <see cref="MergeFile"/> in file order. Possible because every reduction
            /// <see cref="Add"/> makes is a first-seen maximum.</summary>
            public FileReduction BeginFile()
            {
                return new FileReduction();
            }

            public void MergeFile(FileReduction file)
            {
                file.MergeInto(this);
            }

            /// <summary>
            /// One file's rows reduced the way <see cref="Add"/> reduces them - per base_id and
            /// side, and per peptide, the first row with the highest score - each carrying its
            /// GLOBAL ordinal. Merged in file order with the same strict comparison, a file's first
            /// highest row replaces the experiment's only when it is strictly higher, so the winner
            /// is the first highest row overall, at the ordinal it has in the plain loop; and a key
            /// enters the maps when its first file merges, in that file's row order.
            /// </summary>
            internal sealed class FileReduction
            {
                private readonly Dictionary<uint, KeyValuePair<int, double>> _targets =
                    new Dictionary<uint, KeyValuePair<int, double>>();
                private readonly Dictionary<uint, KeyValuePair<int, double>> _decoys =
                    new Dictionary<uint, KeyValuePair<int, double>>();
                private readonly Dictionary<string, PeptideBest> _peptides =
                    new Dictionary<string, PeptideBest>();

                public void Add(int g, double score, uint entryId, bool isDecoy, string peptide)
                {
                    ReduceBest(isDecoy ? _decoys : _targets, entryId & PercolatorEntry.BASE_ID_MASK, g, score);
                    ReducePeptide(_peptides, new PeptideBest(g, score, isDecoy, entryId, peptide));
                }

                internal void MergeInto(StreamingFirstPassQ competition)
                {
                    foreach (var kv in _targets)
                        ReduceBest(competition._precTargets, kv.Key, kv.Value.Key, kv.Value.Value);
                    foreach (var kv in _decoys)
                        ReduceBest(competition._precDecoys, kv.Key, kv.Value.Key, kv.Value.Value);
                    foreach (var kv in _peptides)
                        ReducePeptide(competition._peptBest, kv.Value);
                }
            }

            private static void ReduceBest(Dictionary<uint, KeyValuePair<int, double>> dict, uint baseId, int g, double score)
            {
                if (!dict.TryGetValue(baseId, out var existing) || score > existing.Value)
                    dict[baseId] = new KeyValuePair<int, double>(g, score);
            }

            private static void ReducePeptide(Dictionary<string, PeptideBest> dict, PeptideBest candidate)
            {
                if (!dict.TryGetValue(candidate.Peptide, out var pb) || candidate.Score > pb.Score)
                    dict[candidate.Peptide] = candidate;
            }

            /// <summary>
            /// Experiment-precursor <c>entry_id -&gt; q</c>: compete the global base_id bests,
            /// conservative-q, keyed by each WINNER's full entry_id -- byte-identical to
            /// <see cref="PercolatorQValues.ComputeExperimentPrecursorQMap"/>, which this is the
            /// streaming oracle for. Keyed on entry_id and not base_id because a target and its
            /// decoy share a base_id, so a base_id key hands the loser the winner's q; see that
            /// method for the measurement.
            /// </summary>
            public Dictionary<uint, double> BuildExperimentPrecursorQMap()
            {
                TargetDecoyCompetition.CompeteFromDicts(_precTargets, _precDecoys,
                    out _, out double[] ws, out bool[] wd, out uint[] wb);
                var q = new double[ws.Length];
                PercolatorQValues.ComputeConservativeQvalues(ws, wd, q);
                var map = new Dictionary<uint, double>(wb.Length);
                for (int rank = 0; rank < wb.Length; rank++)
                {
                    uint winnerId = wd[rank] ? wb[rank] | ~PercolatorEntry.BASE_ID_MASK : wb[rank];
                    map[winnerId] = q[rank];
                }
                return map;
            }

            /// <summary>
            /// Streaming counterpart of
            /// <see cref="PercolatorQValues.ComputeExperimentAggregateScoreMap"/> (sidecar v4,
            /// issue #4522): <c>entry_id -&gt; the score the experiment competitions ranked that
            /// entry on</c>. Reads the SAME per-(base_id, side) bests
            /// <see cref="BuildExperimentPrecursorQMap"/> competes, so the score reported beside
            /// an experiment q is by construction the one that q was computed from.
            ///
            /// <para>Keyed by FULL entry_id: the two dictionaries are split by side, and a
            /// base_id plus its side is exactly the entry_id
            /// (<c>base_id | <see cref="LibraryEntry.DECOY_ID_BIT"/></c> for a decoy), so
            /// re-attaching the bit here recovers the per-entry key the sidecar record uses
            /// without the caller having to mask anything.</para>
            /// </summary>
            public Dictionary<uint, double> BuildExperimentAggregateScoreMap()
            {
                var map = new Dictionary<uint, double>(_precTargets.Count + _precDecoys.Count);
                foreach (var kvp in _precTargets)
                    map[kvp.Key] = kvp.Value.Value;
                foreach (var kvp in _precDecoys)
                    map[kvp.Key | LibraryEntry.DECOY_ID_BIT] = kvp.Value.Value;
                return map;
            }

            /// <summary>
            /// Drops the per-precursor and per-peptide bests once every map has been built from
            /// them; each Build method returns a new map, so nothing built depends on them. The
            /// file lanes' closures keep this object reachable through pass 2, the stage's memory
            /// peak, so dropping the reference would not be enough.
            /// </summary>
            public void Release()
            {
                _precTargets.Clear();
                _precTargets.TrimExcess();
                _precDecoys.Clear();
                _precDecoys.TrimExcess();
                _peptBest.Clear();
                _peptBest.TrimExcess();
            }

            /// <summary>
            /// Experiment-peptide <c>peptide -&gt; q</c>: materialize the best-per-peptide set
            /// sorted by ordinal (matching <see cref="PercolatorSampling.BestPrecursorPerPeptide"/>'s sort), compete
            /// by base_id, conservative-q, keyed by the winner's peptide, with each winning
            /// peptide's PEP in <paramref name="pepByPeptide"/> -- byte-identical to
            /// <see cref="PercolatorQValues.ComputeExperimentPeptideQMap"/>.
            /// </summary>
            public Dictionary<string, double> BuildExperimentPeptideQMap(
                out Dictionary<string, double> pepByPeptide)
            {
                var best = new List<PeptideBest>(_peptBest.Values);
                best.Sort((a, b) => a.G.CompareTo(b.G)); // Array.Sort OK: G is the unique streaming ordinal of each peptide's best row, so the comparator never ties -- reproduces BestPrecursorPerPeptide's result.Sort() on ascending global index
                return CompetePeptideBests(best, out pepByPeptide);
            }

            /// <summary>
            /// PEP <c>winner-ordinal -&gt; pep</c>: compete the global base_id bests, fit the PEP
            /// estimator on winners sorted base_id-ascending (the non-associative KDE sum is
            /// order-sensitive), then posterior-error each winner -- byte-identical to
            /// <see cref="PercolatorQValues.ComputePepWinnerMap"/>.
            /// </summary>
            public Dictionary<uint, double> BuildPepWinnerMap()
            {
                TargetDecoyCompetition.CompeteFromDicts(_precTargets, _precDecoys,
                    out int[] wi, out double[] ws, out bool[] wd, out uint[] wb);
                int nWinners = wi.Length;
                var pepOrder = new int[nWinners];
                for (int k = 0; k < nWinners; k++)
                    pepOrder[k] = k;
                Array.Sort(pepOrder, (a, b) => wb[a].CompareTo(wb[b])); // Array.Sort OK: one winner per base_id, so wb has no ties -- matches PercolatorQValues.ComputePepWinnerMap
                var pepScores = new double[nWinners];
                var pepIsDecoy = new bool[nWinners];
                for (int k = 0; k < nWinners; k++)
                {
                    pepScores[k] = ws[pepOrder[k]];
                    pepIsDecoy[k] = wd[pepOrder[k]];
                }
                var pepEstimator = PepEstimator.FitDefault(pepScores, pepIsDecoy);
                // Keyed by the WINNER's entry_id (decoy bit intact), matching
                // PercolatorQValues.ComputePepWinnerMap and expQByWinnerId above: PEP is one
                // value per base_id, reported by every observation of the entry that won.
                var map = new Dictionary<uint, double>(nWinners);
                for (int k = 0; k < nWinners; k++)
                {
                    uint winnerId = wd[k] ? wb[k] | ~PercolatorEntry.BASE_ID_MASK : wb[k];
                    map[winnerId] = pepEstimator.PosteriorError(ws[k]);
                }
                return map;
            }

            /// <summary>
            /// Compete the per-peptide bests (already sorted by ordinal) by base_id and return
            /// the winning peptides' q-values, and in <paramref name="pepByPeptide"/> their PEPs.
            /// </summary>
            private static Dictionary<string, double> CompetePeptideBests(
                List<PeptideBest> best, out Dictionary<string, double> pepByPeptide)
            {
                var targets = new Dictionary<uint, KeyValuePair<int, double>>();
                var decoys = new Dictionary<uint, KeyValuePair<int, double>>();
                for (int i = 0; i < best.Count; i++)
                {
                    uint baseId = best[i].EntryId & PercolatorEntry.BASE_ID_MASK;
                    var dict = best[i].IsDecoy ? decoys : targets;
                    KeyValuePair<int, double> existing;
                    if (dict.TryGetValue(baseId, out existing))
                    {
                        if (best[i].Score > existing.Value)
                            dict[baseId] = new KeyValuePair<int, double>(i, best[i].Score);
                    }
                    else
                    {
                        dict[baseId] = new KeyValuePair<int, double>(i, best[i].Score);
                    }
                }
                TargetDecoyCompetition.CompeteFromDicts(targets, decoys,
                    out int[] wi, out double[] ws, out bool[] wd, out uint[] wb);
                var q = new double[ws.Length];
                PercolatorQValues.ComputeConservativeQvalues(ws, wd, q);
                var peps = PercolatorQValues.ComputeWinnerPeps(ws, wd, wb);
                var map = new Dictionary<string, double>(wi.Length);
                pepByPeptide = new Dictionary<string, double>(wi.Length);
                for (int rank = 0; rank < wi.Length; rank++)
                {
                    map[best[wi[rank]].Peptide] = q[rank];
                    pepByPeptide[best[wi[rank]].Peptide] = peps[rank];
                }
                return map;
            }

            private readonly struct PeptideBest
            {
                public readonly int G;
                public readonly double Score;
                public readonly bool IsDecoy;
                public readonly uint EntryId;
                public readonly string Peptide;

                public PeptideBest(int g, double score, bool isDecoy, uint entryId, string peptide)
                {
                    G = g;
                    Score = score;
                    IsDecoy = isDecoy;
                    EntryId = entryId;
                    Peptide = peptide;
                }
            }
        }
    }
}
