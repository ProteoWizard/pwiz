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
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// The streaming (bounded-memory) FDR paths, 
    /// the original <c>PercolatorFdr</c> god class (issue #4468): the shared competition + PEP + per-run /
    /// experiment q-value block that both flat score passes feed, the streaming
    /// full-population precursor FDR, and the <see cref="StreamingFirstPassQ"/>
    /// accumulator.
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
        /// the flat per-observation arrays that both the <see cref="PercolatorEntry"/>
        /// score pass (<see cref="PercolatorScorer.ScorePopulationAndComputeFdr"/>) and the
        /// projection-native score pass
        /// (<see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>) produce. Extracted as a
        /// single source of truth (issue #4355 step (b) increment iii) so the two
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
        /// The five outputs are returned as parallel arrays (index-aligned to the
        /// inputs); the caller either packs them into <see cref="PercolatorResult"/>s
        /// or writes them straight onto the projection rows.
        /// </summary>
        internal static void ComputeStreamingCompetitionQvalues(
            double[] finalScores, bool[] labels, uint[] entryIds,
            string[] peptides, string[] fileNames,
            out double[] peps, out double[] runPrecursorQvalues,
            out double[] runPeptideQvalues, out double[] expPrecursorQvalues,
            out double[] expPeptideQvalues)
        {
            int n = finalScores.Length;

            // PEP via global target-decoy competition. The bounded winner->PEP map
            // (base_id-ascending KDE order -- see QValueCalculator.ComputePepWinnerMap) is expanded to the
            // full per-row peps array here; the projection score pass reads the map directly
            // so the O(n) array is never materialized (issue #4355 Part B).
            var pepByWinnerIdx = QValueCalculator.ComputePepWinnerMap(finalScores, labels, entryIds);
            peps = new double[n];
            for (int i = 0; i < n; i++)
                peps[i] = 1.0;
            foreach (var kv in pepByWinnerIdx)
                peps[kv.Key] = kv.Value;

            // Per-run precursor + peptide q-values (each file independently).
            runPrecursorQvalues = QValueCalculator.ComputePerRunPrecursorQvalues(
                finalScores, labels, entryIds, fileNames);
            runPeptideQvalues = QValueCalculator.ComputePerRunPeptideQvalues(
                finalScores, labels, entryIds, fileNames, peptides);

            // Experiment-level q-values: single-file shortcut matches
            // direct-path semantics.
            var uniqueFiles = new HashSet<string>(fileNames);
            bool isSingleFile = uniqueFiles.Count <= 1;
            if (isSingleFile)
            {
                expPrecursorQvalues = (double[])runPrecursorQvalues.Clone();
                expPeptideQvalues = (double[])runPeptideQvalues.Clone();
            }
            else
            {
                expPrecursorQvalues = QValueCalculator.ComputeExperimentPrecursorQvalues(
                    finalScores, labels, entryIds);
                expPeptideQvalues = QValueCalculator.ComputeExperimentPeptideQvalues(
                    finalScores, labels, entryIds, peptides);
            }

            // Best-of-runs monotonicity (issue #4390 clamp, memory-bounded flat form): floor
            // each experiment q up to the entry's best (min-over-runs) combined run q. Shared by
            // the FdrEntry streaming path and the projection score pass, so both clamp
            // identically without a resident FdrEntry buffer.
            QValueCalculator.ClampExperimentQToBestRunFlat(
                entryIds, labels, peptides, runPrecursorQvalues, runPeptideQvalues,
                expPrecursorQvalues, expPeptideQvalues);
        }

        /// <summary>
        /// Bounded-memory streaming form of <see cref="QValueCalculator.ComputeFullPopulationPrecursorFdr"/> for
        /// OSPREY_PASS2_QVALUE=transfer-compete. Streams one file's 1st-pass population at a time
        /// (run-level competition + conservative q per file) while accumulating only the
        /// per-base_id best target/decoy observation for the experiment-level competition.
        /// Resident footprint is therefore O(distinct precursors + largest single file +
        /// survivors) -- flat in file count -- where the resident overload is O(total
        /// observations). Emits run/experiment precursor q and PEP identical to the resident
        /// method for the reported survivors (verified byte-for-byte on the 3-file Stellar
        /// entrapment set). File reading is injected so this assembly needs no IO dependency.
        /// </summary>
        /// <param name="fileKeys">Stable per-file keys to stream, in any order.</param>
        /// <param name="readFileScalars">Reads one file's full population as (entryIds, scores);
        ///   invoked once per file, arrays released before the next file is read.</param>
        /// <param name="survivorScoreOverride">Frozen-model score to substitute for a reconciled
        ///   survivor observation, keyed (fileKey, entryId). Observations absent here keep their
        ///   stored 1st-pass score.</param>
        /// <param name="survivors">Every reported survivor (fileKey, entryId) to emit q/PEP for.</param>
        /// <param name="survivorRunQ">Out: run-level precursor q per reported (fileKey, entryId).</param>
        /// <param name="survivorExpQ">Out: experiment-level precursor q per reported (fileKey, entryId).</param>
        /// <param name="survivorPep">Out: PEP per reported (fileKey, entryId).</param>
        /// <param name="stratumBaseIds">Null for the full-population competition; non-null restricts
        ///   the competition to these base_ids (protein-compact).</param>
        public static void ComputeFullPopulationPrecursorFdrStreaming(
            IReadOnlyList<string> fileKeys,
            Func<string, (uint[] entryIds, double[] scores)> readFileScalars,
            IReadOnlyDictionary<(string, uint), double> survivorScoreOverride,
            IReadOnlyCollection<(string, uint)> survivors,
            out Dictionary<(string, uint), double> survivorRunQ,
            out Dictionary<(string, uint), double> survivorExpQ,
            out Dictionary<(string, uint), double> survivorPep,
            HashSet<uint> stratumBaseIds = null)
        {
            // stratumBaseIds == null -> full-population competition (transfer-compete).
            // non-null -> STRATIFIED competition (protein-compact): only observations whose
            // base_id is in the stratum participate in the run/experiment competitions, so
            // off-stratum decoys leave the null (reduced multiple testing). The per-base_id
            // maps hold only stratum members, so peak memory stays flat in file count -- it
            // only shrinks relative to the full-population path.
            var survivorSet = new HashSet<(string, uint)>(survivors);
            var survivorEntryIds = new HashSet<uint>();
            foreach (var s in survivorSet) survivorEntryIds.Add(s.Item2);

            survivorRunQ = new Dictionary<(string, uint), double>(survivorSet.Count);
            survivorExpQ = new Dictionary<(string, uint), double>(survivorSet.Count);
            survivorPep = new Dictionary<(string, uint), double>(survivorSet.Count);

            // Experiment-level per-base_id best target/decoy observation (score + locator),
            // accumulated across every file. Bounded by the number of distinct precursors.
            var bestTarget = new Dictionary<uint, (double score, int fileIdx, uint entryId)>();
            var bestDecoy = new Dictionary<uint, (double score, int fileIdx, uint entryId)>();

            // Best (min) run q per SURVIVOR entry_id across the files it won in -- the
            // best-of-runs monotonicity floor for the experiment q (only survivors are emitted).
            var minRunQ = new Dictionary<uint, double>(survivorEntryIds.Count);

            for (int fileIdx = 0; fileIdx < fileKeys.Count; fileIdx++)
            {
                string fileKey = fileKeys[fileIdx];
                var (entryIds, scores) = readFileScalars(fileKey);
                int m = entryIds.Length;
                var labels = new bool[m];
                for (int i = 0; i < m; i++)
                {
                    uint eid = entryIds[i];
                    labels[i] = (eid & ~PercolatorEntry.BASE_ID_MASK) != 0u; // decoy high bit set
                    if (survivorScoreOverride.TryGetValue((fileKey, eid), out double ov))
                        scores[i] = ov; // swap in the reconciled survivor's frozen-model score
                }

                // Run-level: compete within this file (only stratum members when
                // stratified), conservative q on the winners.
                int[] allIdx;
                if (stratumBaseIds == null)
                {
                    allIdx = new int[m];
                    for (int i = 0; i < m; i++) allIdx[i] = i;
                }
                else
                {
                    var idxList = new List<int>(m);
                    for (int i = 0; i < m; i++)
                        if (stratumBaseIds.Contains(entryIds[i] & PercolatorEntry.BASE_ID_MASK)) idxList.Add(i);
                    allIdx = idxList.ToArray();
                }
                TargetDecoyCompetition.CompeteFromIndices(scores, labels, entryIds, allIdx,
                    out int[] wi, out double[] ws, out bool[] wd);
                var q = new double[wi.Length];
                QValueCalculator.ComputeConservativeQvalues(ws, wd, q);
                for (int rank = 0; rank < wi.Length; rank++)
                {
                    uint eid = entryIds[wi[rank]];
                    if (!survivorEntryIds.Contains(eid)) continue;
                    double qv = q[rank];
                    var key = (fileKey, eid);
                    if (survivorSet.Contains(key)) survivorRunQ[key] = qv;
                    if (!minRunQ.TryGetValue(eid, out double cur) || qv < cur) minRunQ[eid] = qv;
                }

                // Experiment-level: fold every observation into the per-base_id bests
                // (stratum members only when stratified -> the experiment competition
                // below runs over exactly the stratum's base_ids).
                for (int i = 0; i < m; i++)
                {
                    uint eid = entryIds[i];
                    uint bid = eid & PercolatorEntry.BASE_ID_MASK;
                    if (stratumBaseIds != null && !stratumBaseIds.Contains(bid)) continue;
                    double s = scores[i];
                    if (labels[i])
                    {
                        if (!bestDecoy.TryGetValue(bid, out var cur) || s > cur.score)
                            bestDecoy[bid] = (s, fileIdx, eid);
                    }
                    else
                    {
                        if (!bestTarget.TryGetValue(bid, out var cur) || s > cur.score)
                            bestTarget[bid] = (s, fileIdx, eid);
                    }
                }
                // entryIds/scores/labels/allIdx released here before the next file is read.
            }

            // Experiment competition: one winner per base_id, conservative q, PEP fit over
            // exactly the winner set the resident method fits.
            var baseIds = new HashSet<uint>(bestTarget.Keys);
            baseIds.UnionWith(bestDecoy.Keys);
            int w = baseIds.Count;
            var expScore = new double[w];
            var expIsDecoy = new bool[w];
            var expBaseId = new uint[w];
            var winnerLoc = new Dictionary<uint, (int fileIdx, uint entryId, double score)>(w);
            int wi2 = 0;
            foreach (uint bid in baseIds)
            {
                bool hasT = bestTarget.TryGetValue(bid, out var t);
                bool hasD = bestDecoy.TryGetValue(bid, out var d);
                // CompeteFromIndices: target wins strictly (tScore > dScore); ties go to the decoy.
                bool decoyWins = hasT && hasD ? !(t.score > d.score) : !hasT;
                var win = decoyWins ? d : t;
                expScore[wi2] = win.score; expIsDecoy[wi2] = decoyWins; expBaseId[wi2] = bid;
                winnerLoc[bid] = (win.fileIdx, win.entryId, win.score);
                wi2++;
            }

            // Sort winners by score desc, base_id asc (unique base_id => total order).
            var perm = new int[w];
            for (int i = 0; i < w; i++) perm[i] = i;
            Array.Sort(perm, (a, b) => // Array.Sort OK: unique baseId tie-break makes comparator total
            {
                int cmp = expScore[b].CompareTo(expScore[a]);
                return cmp != 0 ? cmp : expBaseId[a].CompareTo(expBaseId[b]);
            });
            var sortedScore = new double[w];
            var sortedDecoy = new bool[w];
            var sortedBaseId = new uint[w];
            for (int i = 0; i < w; i++)
            {
                sortedScore[i] = expScore[perm[i]];
                sortedDecoy[i] = expIsDecoy[perm[i]];
                sortedBaseId[i] = expBaseId[perm[i]];
            }
            var qExp = new double[w];
            QValueCalculator.ComputeConservativeQvalues(sortedScore, sortedDecoy, qExp);
            var baseIdExpQ = new Dictionary<uint, double>(w);
            for (int i = 0; i < w; i++) baseIdExpQ[sortedBaseId[i]] = qExp[i];

            var pepEstimator = PepEstimator.FitDefault(expScore, expIsDecoy);

            bool multiFile = fileKeys.Count > 1;
            foreach (var key in survivorSet)
            {
                string fileKey = key.Item1;
                uint eid = key.Item2;
                uint bid = eid & PercolatorEntry.BASE_ID_MASK;

                if (!survivorRunQ.ContainsKey(key)) survivorRunQ[key] = 1.0;

                if (multiFile)
                {
                    // Experiment q = base_id winner q, floored up to this precursor's best run q.
                    // An entry_id that won no within-file competition has best run q = 1.0 (every
                    // observation stayed at the q=1.0 default), matching the resident bestRunQ.
                    double eq = baseIdExpQ.TryGetValue(bid, out double bq) ? bq : 1.0;
                    double floorQ = minRunQ.TryGetValue(eid, out double mrq) ? mrq : 1.0;
                    if (eq < floorQ) eq = floorQ;
                    survivorExpQ[key] = eq;
                }
                else
                {
                    // Single file: experiment q == run q (resident short-circuit).
                    survivorExpQ[key] = survivorRunQ[key];
                }

                // PEP is real only on the single experiment-winner observation of each base_id.
                double pep = 1.0;
                if (winnerLoc.TryGetValue(bid, out var loc) &&
                    loc.entryId == eid && fileKeys[loc.fileIdx] == fileKey)
                    pep = pepEstimator.PosteriorError(loc.score);
                survivorPep[key] = pep;
            }
        }

        /// <summary>
        /// Streaming builder for the three GLOBAL bounded first-pass q maps (issue #4355
        /// struct-shrink S3, Stage B): the experiment-precursor <c>base_id -&gt; q</c> map, the
        /// experiment-peptide <c>peptide -&gt; q</c> map, and the PEP <c>winner-ordinal -&gt; pep</c>
        /// map -- built by pushing each scored row via <see cref="Add"/> in flat (file,row) order
        /// instead of reading the resident <c>finalScores/labels/entryIds/peptides[n]</c> arrays.
        /// Bounded: it retains only per-base_id and per-peptide bests (O(distinct)), never an O(n)
        /// buffer. Each Build* reuses the SAME <see cref="TargetDecoyCompetition.CompeteFromDicts"/> +
        /// <see cref="QValueCalculator.ComputeConservativeQvalues"/> (+ <c>PepEstimator</c>) finish the flat
        /// <see cref="QValueCalculator.ComputeExperimentPrecursorQMap"/> / <see cref="QValueCalculator.ComputeExperimentPeptideQMap"/>
        /// / <see cref="QValueCalculator.ComputePepWinnerMap"/> run, so a population fed in the same order yields
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
                var dict = isDecoy ? _precDecoys : _precTargets;
                KeyValuePair<int, double> existing;
                if (dict.TryGetValue(baseId, out existing))
                {
                    if (score > existing.Value)
                        dict[baseId] = new KeyValuePair<int, double>(g, score);
                }
                else
                {
                    dict[baseId] = new KeyValuePair<int, double>(g, score);
                }

                PeptideBest pb;
                if (_peptBest.TryGetValue(peptide, out pb))
                {
                    if (score > pb.Score)
                        _peptBest[peptide] = new PeptideBest(g, score, isDecoy, entryId, peptide);
                }
                else
                {
                    _peptBest[peptide] = new PeptideBest(g, score, isDecoy, entryId, peptide);
                }
            }

            /// <summary>
            /// Experiment-precursor <c>base_id -&gt; q</c>: compete the global base_id bests,
            /// conservative-q, keyed by each winner's base_id -- byte-identical to
            /// <see cref="QValueCalculator.ComputeExperimentPrecursorQMap"/>.
            /// </summary>
            public Dictionary<uint, double> BuildExperimentPrecursorQMap()
            {
                TargetDecoyCompetition.CompeteFromDicts(_precTargets, _precDecoys,
                    out _, out double[] ws, out bool[] wd, out uint[] wb);
                var q = new double[ws.Length];
                QValueCalculator.ComputeConservativeQvalues(ws, wd, q);
                var map = new Dictionary<uint, double>(wb.Length);
                for (int rank = 0; rank < wb.Length; rank++)
                    map[wb[rank]] = q[rank];
                return map;
            }

            /// <summary>
            /// Experiment-peptide <c>peptide -&gt; q</c>: materialize the best-per-peptide set
            /// sorted by ordinal (matching <see cref="PercolatorSampling.BestPrecursorPerPeptide"/>'s sort), compete
            /// by base_id, conservative-q, keyed by the winner's peptide -- byte-identical to
            /// <see cref="QValueCalculator.ComputeExperimentPeptideQMap"/>.
            /// </summary>
            public Dictionary<string, double> BuildExperimentPeptideQMap()
            {
                var best = new List<PeptideBest>(_peptBest.Values);
                best.Sort((a, b) => a.G.CompareTo(b.G)); // Array.Sort OK: G is the unique streaming ordinal of each peptide's best row, so the comparator never ties -- reproduces BestPrecursorPerPeptide's result.Sort() on ascending global index
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
                    out int[] wi, out double[] ws, out bool[] wd, out _);
                var q = new double[ws.Length];
                QValueCalculator.ComputeConservativeQvalues(ws, wd, q);
                var map = new Dictionary<string, double>(wi.Length);
                for (int rank = 0; rank < wi.Length; rank++)
                    map[best[wi[rank]].Peptide] = q[rank];
                return map;
            }

            /// <summary>
            /// PEP <c>winner-ordinal -&gt; pep</c>: compete the global base_id bests, fit the PEP
            /// estimator on winners sorted base_id-ascending (the non-associative KDE sum is
            /// order-sensitive), then posterior-error each winner -- byte-identical to
            /// <see cref="QValueCalculator.ComputePepWinnerMap"/>.
            /// </summary>
            public Dictionary<int, double> BuildPepWinnerMap()
            {
                TargetDecoyCompetition.CompeteFromDicts(_precTargets, _precDecoys,
                    out int[] wi, out double[] ws, out bool[] wd, out uint[] wb);
                int nWinners = wi.Length;
                var pepOrder = new int[nWinners];
                for (int k = 0; k < nWinners; k++)
                    pepOrder[k] = k;
                Array.Sort(pepOrder, (a, b) => wb[a].CompareTo(wb[b])); // Array.Sort OK: one winner per base_id, so wb has no ties -- matches QValueCalculator.ComputePepWinnerMap
                var pepScores = new double[nWinners];
                var pepIsDecoy = new bool[nWinners];
                for (int k = 0; k < nWinners; k++)
                {
                    pepScores[k] = ws[pepOrder[k]];
                    pepIsDecoy[k] = wd[pepOrder[k]];
                }
                var pepEstimator = PepEstimator.FitDefault(pepScores, pepIsDecoy);
                var map = new Dictionary<int, double>(nWinners);
                for (int k = 0; k < nWinners; k++)
                    map[wi[k]] = pepEstimator.PosteriorError(ws[k]);
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
