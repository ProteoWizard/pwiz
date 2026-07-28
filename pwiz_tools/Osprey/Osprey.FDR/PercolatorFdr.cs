/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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

// Native Percolator implementation for semi-supervised FDR control
//
// Implements the Percolator algorithm (Kall et al. 2007) as refined by
// mokapot (Fondrie & Noble, 2021):
// - 3-fold cross-validation with peptide-grouped fold assignment
// - Iterative linear SVM training on high-confidence targets vs all decoys
// - Grid search for SVM cost parameter C
// - Per-run and experiment-level FDR with conservative (n_decoy+1)/n_target formula
// - Posterior error probability via KDE + isotonic regression
//
// Port of osprey-fdr/src/percolator.rs.

using System;
using System.Collections.Generic;
using pwiz.Osprey.Core;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Performs false discovery rate estimation using the Percolator algorithm.
    /// Port of osprey-fdr/src/percolator.rs.
    /// </summary>
    public static class PercolatorFdr
    {
        // internal so the extracted PercolatorDiagnosticsDump can mask base IDs
        // the same way the pipeline does.
        internal static readonly uint BASE_ID_MASK = 0x7FFFFFFF;


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
        /// so the winner arrays are reordered by <c>entryIds &amp; BASE_ID_MASK</c>
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
            // (base_id-ascending KDE order -- see ComputePepWinnerMap) is expanded to the
            // full per-row peps array here; the projection score pass reads the map directly
            // so the O(n) array is never materialized (issue #4355 Part B).
            var pepByWinnerIdx = ComputePepWinnerMap(finalScores, labels, entryIds);
            peps = new double[n];
            for (int i = 0; i < n; i++)
                peps[i] = 1.0;
            foreach (var kv in pepByWinnerIdx)
                peps[kv.Key] = kv.Value;

            // Per-run precursor + peptide q-values (each file independently).
            runPrecursorQvalues = ComputePerRunPrecursorQvalues(
                finalScores, labels, entryIds, fileNames);
            runPeptideQvalues = ComputePerRunPeptideQvalues(
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
                expPrecursorQvalues = ComputeExperimentPrecursorQvalues(
                    finalScores, labels, entryIds);
                expPeptideQvalues = ComputeExperimentPeptideQvalues(
                    finalScores, labels, entryIds, peptides);
            }

            // Best-of-runs monotonicity (issue #4390 clamp, memory-bounded flat form): floor
            // each experiment q up to the entry's best (min-over-runs) combined run q. Shared by
            // the FdrEntry streaming path and the projection score pass, so both clamp
            // identically without a resident FdrEntry buffer.
            ClampExperimentQToBestRunFlat(
                entryIds, labels, peptides, runPrecursorQvalues, runPeptideQvalues,
                expPrecursorQvalues, expPeptideQvalues);
        }

        /// <summary>
        /// Bounded (O(base_ids)) posterior-error-probability (PEP) map: the global
        /// target-decoy competition winner index -&gt; its PEP. This is the intrinsic working
        /// set of the PEP step -- one PEP per competition winner (every other row's PEP is the
        /// default 1.0) -- so the projection score pass
        /// (<see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>) reads the map directly to set
        /// the winning rows' PEP without materializing the O(n) per-row array (issue #4355
        /// Part B). <see cref="ComputeStreamingCompetitionQvalues"/> expands the same map, so
        /// both share the one PEP fit and cannot drift.
        ///
        /// The KDE is fed in base_id-ascending order (risk #6): CompeteAll returns winners
        /// score-descending, but PepEstimator.FitDefault's KDE sum is NOT associative, so for
        /// cross-impl byte parity the winners must be reordered to the same base_id-sorted
        /// order Rust's compute_fdr_from_stubs uses before the fit.
        /// </summary>
        internal static Dictionary<int, double> ComputePepWinnerMap(
            double[] finalScores, bool[] labels, uint[] entryIds)
        {
            int n = finalScores.Length;
            int[] winnerIndices;
            double[] winnerScores;
            bool[] winnerIsDecoy;
            // Throttled progress over the ~344M-row population competition (the big walk that
            // ran silent at 82 files); null (silent) on small runs. Console-only, byte-neutral.
            using (var pepProgress = QProgress(@"Population target/decoy competition", n, n))
                CompeteAll(finalScores, labels, entryIds,
                    out winnerIndices, out winnerScores, out winnerIsDecoy, pepProgress);

            int nWinners = winnerIndices.Length;
            var pepOrder = new int[nWinners];
            for (int k = 0; k < nWinners; k++)
                pepOrder[k] = k;
            Array.Sort(pepOrder, (a, b) => // Array.Sort OK: TDC's CompeteAll already produced one winner per base_id, so each base_id appears at most once in pepOrder -- no ties.
            {
                uint ba = entryIds[winnerIndices[a]] & BASE_ID_MASK;
                uint bb = entryIds[winnerIndices[b]] & BASE_ID_MASK;
                return ba.CompareTo(bb);
            });
            var pepScores = new double[nWinners];
            var pepIsDecoy = new bool[nWinners];
            for (int k = 0; k < nWinners; k++)
            {
                pepScores[k] = winnerScores[pepOrder[k]];
                pepIsDecoy[k] = winnerIsDecoy[pepOrder[k]];
            }

            var pepEstimator = PepEstimator.FitDefault(pepScores, pepIsDecoy);
            var pepByWinnerIdx = new Dictionary<int, double>(nWinners);
            foreach (int idx in winnerIndices)
                pepByWinnerIdx[idx] = pepEstimator.PosteriorError(finalScores[idx]);
            return pepByWinnerIdx;
        }

        /// <summary>
        /// Memory-bounded flat form of <see cref="PercolatorEngine.ClampExperimentQToBestRun"/>
        /// (issue #4378): floor each experiment q up to the entry's best (min-over-runs)
        /// combined run q (<c>runBoth = max(runPrecursorQ, runPeptideQ)</c>), keyed by EntryId
        /// for the precursor floor and by <c>(peptide, isDecoy)</c> for the peptide floor (an
        /// empty peptide is skipped). Operates on the flat score-pass scalar arrays the FDR math
        /// already holds -- no resident FdrEntry buffer -- so the streaming path clamps without
        /// materializing every entry. <c>min</c>/<c>max</c> are order-independent, so the result
        /// is byte-identical to the resident overload on the same values.
        /// </summary>
        internal static void ClampExperimentQToBestRunFlat(
            uint[] entryIds, bool[] labels, string[] peptides,
            double[] runPrecursorQvalues, double[] runPeptideQvalues,
            double[] expPrecursorQvalues, double[] expPeptideQvalues)
        {
            BuildExperimentQClampFloors(
                entryIds, labels, peptides, runPrecursorQvalues, runPeptideQvalues,
                out var minRunBothByEntryId, out var minRunBothByPeptide);

            int n = entryIds.Length;
            for (int i = 0; i < n; i++)
            {
                double floorPrec;
                if (minRunBothByEntryId.TryGetValue(entryIds[i], out floorPrec) &&
                    floorPrec > expPrecursorQvalues[i])
                    expPrecursorQvalues[i] = floorPrec;

                if (!string.IsNullOrEmpty(peptides[i]))
                {
                    double floorPept;
                    if (minRunBothByPeptide.TryGetValue((peptides[i], labels[i]), out floorPept) &&
                        floorPept > expPeptideQvalues[i])
                        expPeptideQvalues[i] = floorPept;
                }
            }
        }

        /// <summary>
        /// Builds the min-over-runs combined-run-q floors that
        /// <see cref="ClampExperimentQToBestRunFlat"/> applies:
        /// <c>minRunBothByEntryId[entryId]</c> and <c>minRunBothByPeptide[(peptide, isDecoy)]</c>
        /// = the minimum over that entry's / peptide's rows of
        /// <c>max(runPrecursorQ, runPeptideQ)</c>. Bounded (O(distinct entryIds) +
        /// O(distinct peptides)), shared with the projection score pass
        /// (<see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>) so both clamp identically
        /// without a resident per-row experiment-q array (issue #4355 Part B).
        /// </summary>
        private static void BuildExperimentQClampFloors(
            uint[] entryIds, bool[] labels, string[] peptides,
            double[] runPrecursorQvalues, double[] runPeptideQvalues,
            out Dictionary<uint, double> minRunBothByEntryId,
            out Dictionary<(string, bool), double> minRunBothByPeptide)
        {
            int n = entryIds.Length;
            minRunBothByEntryId = new Dictionary<uint, double>();
            minRunBothByPeptide = new Dictionary<(string, bool), double>();
            for (int i = 0; i < n; i++)
            {
                double runBoth = Math.Max(runPrecursorQvalues[i], runPeptideQvalues[i]);
                UpdateExperimentQClampFloor(
                    minRunBothByEntryId, minRunBothByPeptide, entryIds[i], peptides[i], labels[i], runBoth);
            }
        }

        /// <summary>
        /// Folds one row into the best-of-runs clamp floors (issue #4390): tracks the minimum over
        /// an entry's / peptide's rows of <paramref name="runBoth"/> = <c>max(runPrecursorQ,
        /// runPeptideQ)</c>, keyed by <paramref name="entryId"/> and by
        /// <c>(<paramref name="peptide"/>, <paramref name="isDecoy"/>)</c>. Shared by
        /// <see cref="BuildExperimentQClampFloors"/> (flat path) and the projection score pass's
        /// floor reduction so the two cannot drift on a byte-identity-locked path (issue #4355
        /// Part B). An empty ModifiedSequence has no peptide identity and is not bucketed; peptide
        /// identity is (sequence, isDecoy) so a decoy's good run never lowers its target's floor.
        /// </summary>
        internal static void UpdateExperimentQClampFloor(
            Dictionary<uint, double> minRunBothByEntryId,
            Dictionary<(string, bool), double> minRunBothByPeptide,
            uint entryId, string peptide, bool isDecoy, double runBoth)
        {
            double curPrec;
            if (!minRunBothByEntryId.TryGetValue(entryId, out curPrec) || runBoth < curPrec)
                minRunBothByEntryId[entryId] = runBoth;

            if (string.IsNullOrEmpty(peptide))
                return;
            var pkey = (peptide, isDecoy);
            double curPept;
            if (!minRunBothByPeptide.TryGetValue(pkey, out curPept) || runBoth < curPept)
                minRunBothByPeptide[pkey] = runBoth;
        }




        // ============================================================
        // Target-decoy competition and q-value computation
        // ============================================================

        /// <summary>
        /// Core competition logic: group by base_id, compete, return winners sorted by score desc.
        ///
        /// This is deliberately a SEPARATE implementation from
        /// <see cref="FdrController.CompeteAndFilter{T}"/>, not a duplicate to be
        /// merged: the two serve different regimes. This array/index form is the
        /// hot Percolator path -- it works on pre-flattened primitive arrays and a
        /// caller-supplied index subset, returns winner arrays for downstream
        /// scratch-pooled q-value passes (see <c>CountPassing</c>), and
        /// allocates nothing on the scratch overload. <c>CompeteAndFilter</c> is
        /// the ergonomic generic form for simple-FDR callers
        /// (<see cref="PercolatorEngine.RunSimpleFdr"/>): it competes an
        /// <c>IEnumerable&lt;T&gt;</c> via score/decoy/id selectors and returns a
        /// typed result. Same competition rule (strict &gt;, ties to decoy), two
        /// shapes tuned to performance vs. ergonomics.
        /// </summary>
        public static void CompeteFromIndices(
            double[] scores,
            bool[] labels,
            uint[] entryIds,
            int[] indices,
            out int[] winnerIndices,
            out double[] winnerScores,
            out bool[] winnerIsDecoy,
            ProgressReporter progress = null)
        {
            var targets = new Dictionary<uint, KeyValuePair<int, double>>();
            var decoys = new Dictionary<uint, KeyValuePair<int, double>>();

            // Throttled per-row progress for the large experiment / PEP competitions -- the
            // ~344M-row base_id reduction below ran ~90 s silent at 82 files. Console-only via
            // the caller's reporter (null on the small per-file per-run calls, which report at
            // their own per-file granularity); never affects the winners, so q-values are
            // byte-identical.
            long processed = 0;
            foreach (int idx in indices)
            {
                if (progress != null && (++processed & 0x3FFFFF) == 0)
                    progress.Report(processed);
                uint baseId = entryIds[idx] & BASE_ID_MASK;
                if (labels[idx])
                {
                    KeyValuePair<int, double> existing;
                    if (decoys.TryGetValue(baseId, out existing))
                    {
                        if (scores[idx] > existing.Value)
                            decoys[baseId] = new KeyValuePair<int, double>(idx, scores[idx]);
                    }
                    else
                    {
                        decoys[baseId] = new KeyValuePair<int, double>(idx, scores[idx]);
                    }
                }
                else
                {
                    KeyValuePair<int, double> existing;
                    if (targets.TryGetValue(baseId, out existing))
                    {
                        if (scores[idx] > existing.Value)
                            targets[baseId] = new KeyValuePair<int, double>(idx, scores[idx]);
                    }
                    else
                    {
                        targets[baseId] = new KeyValuePair<int, double>(idx, scores[idx]);
                    }
                }
            }

            CompeteFromDicts(targets, decoys,
                out winnerIndices, out winnerScores, out winnerIsDecoy, out _);
        }

        /// <summary>
        /// Shared finish for target/decoy competition: given per-base_id best-target and
        /// best-decoy maps (winning row index + score), compete each pair (higher score wins,
        /// ties to decoy), add unpaired decoys, and sort winners by score desc / base_id asc.
        /// Extracted from <see cref="CompeteFromIndices"/> so the flat-array path (which builds
        /// the maps by walking an index subset) and the streaming path (issue #4355 struct-shrink
        /// S3, which builds the identical maps by pushing rows in flat (file,row) order) share the
        /// EXACT compete + sort, and so cannot drift. The stored index is the winning row's flat
        /// index / streaming ordinal; both label the same row because the streaming pass visits
        /// rows in the same order the flat arrays were built. <paramref name="winnerBaseIds"/>
        /// carries each winner's base_id (the map key) so the streaming path can key the
        /// experiment-precursor / PEP maps WITHOUT a resident <c>entryIds[]</c> array (the flat
        /// path recovers the same base_id via <c>entryIds[wi[rank]] &amp; BASE_ID_MASK</c>).
        /// </summary>
        internal static void CompeteFromDicts(
            Dictionary<uint, KeyValuePair<int, double>> targets,
            Dictionary<uint, KeyValuePair<int, double>> decoys,
            out int[] winnerIndices,
            out double[] winnerScores,
            out bool[] winnerIsDecoy,
            out uint[] winnerBaseIds)
        {
            // Compete pairs: higher score wins, ties go to decoy
            var winners = new List<Tuple<int, double, bool, uint>>(targets.Count);
            foreach (var kvp in targets)
            {
                uint baseId = kvp.Key;
                int tIdx = kvp.Value.Key;
                double tScore = kvp.Value.Value;

                KeyValuePair<int, double> decoyEntry;
                if (decoys.TryGetValue(baseId, out decoyEntry))
                {
                    if (tScore > decoyEntry.Value)
                        winners.Add(Tuple.Create(tIdx, tScore, false, baseId));
                    else
                        winners.Add(Tuple.Create(decoyEntry.Key, decoyEntry.Value, true, baseId));
                }
                else
                {
                    winners.Add(Tuple.Create(tIdx, tScore, false, baseId));
                }
            }
            // Unpaired decoys
            foreach (var kvp in decoys)
            {
                if (!targets.ContainsKey(kvp.Key))
                    winners.Add(Tuple.Create(kvp.Value.Key, kvp.Value.Value, true, kvp.Key));
            }

            // Sort by score desc, then base_id asc for deterministic tiebreaking.
            // Array.Sort OK: the secondary key Item4 is the unique base_id, so the
            // comparator never returns 0 and the unstable-sort tie path is unreachable.
            winners.Sort((a, b) => // Array.Sort OK: (see above) secondary key Item4 is unique base_id, comparator never ties
            {
                int cmp = b.Item2.CompareTo(a.Item2);
                if (cmp != 0)
                    return cmp;
                return a.Item4.CompareTo(b.Item4);
            });

            winnerIndices = new int[winners.Count];
            winnerScores = new double[winners.Count];
            winnerIsDecoy = new bool[winners.Count];
            winnerBaseIds = new uint[winners.Count];
            for (int i = 0; i < winners.Count; i++)
            {
                winnerIndices[i] = winners[i].Item1;
                winnerScores[i] = winners[i].Item2;
                winnerIsDecoy[i] = winners[i].Item3;
                winnerBaseIds[i] = winners[i].Item4;
            }
        }

        /// <summary>
        /// OSPREY_PASS2_QVALUE=transfer-compete (full-population form): given the FULL
        /// 1st-pass population as flat SCALAR arrays -- scores, is_decoy labels, entry_ids,
        /// file names, all index-aligned, with the reconciled minority's scores already
        /// overwritten by the caller -- run the global target-decoy competition and compute
        /// per-run + experiment PRECURSOR q-values and PEP over that full population. Same
        /// competition/PEP/q math as <see cref="PercolatorScorer.ScorePopulationAndComputeFdr"/>, but takes
        /// pre-computed scores (no features, no model application), so the 2nd pass can
        /// recompete over the persisted full-population scalars from
        /// <c>.1st-pass.fdr_scores.bin</c> -- with only the ~0.4% reconciled scores swapped in
        /// -- without ever holding features resident. Outputs are index-aligned to the inputs.
        /// Precursor-level only (the entrapment-FDR path); peptide-level q is not computed here.
        /// </summary>
        public static void ComputeFullPopulationPrecursorFdr(
            double[] scores, bool[] labels, uint[] entryIds, string[] fileNames,
            out double[] runPrecursorQ, out double[] experimentPrecursorQ, out double[] pep)
        {
            int n = scores.Length;

            // Global target-decoy competition (group by base_id, winner per pair) + PEP on winners.
            CompeteAll(scores, labels, entryIds,
                out int[] winnerIndices, out double[] winnerScores, out bool[] winnerIsDecoy);
            var pepEstimator = PepEstimator.FitDefault(winnerScores, winnerIsDecoy);
            pep = new double[n];
            for (int i = 0; i < n; i++) pep[i] = 1.0;
            foreach (int idx in winnerIndices)
                pep[idx] = pepEstimator.PosteriorError(scores[idx]);

            // Per-run and experiment-wide precursor q over the full population.
            runPrecursorQ = ComputePerRunPrecursorQvalues(scores, labels, entryIds, fileNames);
            var uniqueFiles = new HashSet<string>(fileNames);
            experimentPrecursorQ = uniqueFiles.Count <= 1
                ? (double[])runPrecursorQ.Clone()
                : ComputeExperimentPrecursorQvalues(scores, labels, entryIds);

            // Best-of-runs monotonicity (issue #4390): floor each entry's experiment q up to
            // its own best (min-over-runs) run q -- an experiment q is never more confident
            // than the precursor's best single run. Keyed by full entry_id (target and decoy
            // are distinct entries), matching ClampExperimentQToBestRunFlat's precursor clamp.
            var bestRunQ = new Dictionary<uint, double>();
            for (int i = 0; i < n; i++)
                if (!bestRunQ.TryGetValue(entryIds[i], out double q) || runPrecursorQ[i] < q)
                    bestRunQ[entryIds[i]] = runPrecursorQ[i];
            for (int i = 0; i < n; i++)
                if (experimentPrecursorQ[i] < bestRunQ[entryIds[i]])
                    experimentPrecursorQ[i] = bestRunQ[entryIds[i]];
        }

        /// <summary>
        /// Bounded-memory streaming form of <see cref="ComputeFullPopulationPrecursorFdr"/> for
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
                    labels[i] = (eid & ~BASE_ID_MASK) != 0u; // decoy high bit set
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
                        if (stratumBaseIds.Contains(entryIds[i] & BASE_ID_MASK)) idxList.Add(i);
                    allIdx = idxList.ToArray();
                }
                CompeteFromIndices(scores, labels, entryIds, allIdx,
                    out int[] wi, out double[] ws, out bool[] wd);
                var q = new double[wi.Length];
                ComputeConservativeQvalues(ws, wd, q);
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
                    uint bid = eid & BASE_ID_MASK;
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
            ComputeConservativeQvalues(sortedScore, sortedDecoy, qExp);
            var baseIdExpQ = new Dictionary<uint, double>(w);
            for (int i = 0; i < w; i++) baseIdExpQ[sortedBaseId[i]] = qExp[i];

            var pepEstimator = PepEstimator.FitDefault(expScore, expIsDecoy);

            bool multiFile = fileKeys.Count > 1;
            foreach (var key in survivorSet)
            {
                string fileKey = key.Item1;
                uint eid = key.Item2;
                uint bid = eid & BASE_ID_MASK;

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

        internal static void CompeteAll(
            double[] scores,
            bool[] labels,
            uint[] entryIds,
            out int[] winnerIndices,
            out double[] winnerScores,
            out bool[] winnerIsDecoy,
            ProgressReporter progress = null)
        {
            var allIndices = new int[scores.Length];
            for (int i = 0; i < scores.Length; i++)
                allIndices[i] = i;
            CompeteFromIndices(scores, labels, entryIds, allIndices,
                out winnerIndices, out winnerScores, out winnerIsDecoy, progress);
        }

        /// <summary>
        /// Compute conservative q-values: FDR = (n_decoy + 1) / n_target.
        /// Input must be sorted by score descending (winners from competition).
        /// </summary>
        public static void ComputeConservativeQvalues(
            double[] scores, bool[] isDecoy, double[] qValues)
        {
            ComputeQvaluesCore(isDecoy, qValues, isDecoy.Length, decoyOffset: 1);
        }

        /// <summary>
        /// Compute non-conservative q-values: FDR = n_decoy / n_target.
        /// Used internally for iteration tracking and positive training set selection.
        /// </summary>
        internal static void ComputeQvalues(
            double[] scores, bool[] isDecoy, double[] qValues)
        {
            ComputeQvaluesCore(isDecoy, qValues, isDecoy.Length, decoyOffset: 0);
        }

        /// <summary>
        /// Count targets passing FDR threshold using non-conservative formula.
        /// </summary>
        public static int CountPassing(
            double[] scores, bool[] labels, uint[] entryIds, double fdrThreshold)
        {
            return CountPassing(scores, labels, entryIds, fdrThreshold, null);
        }

        /// <summary>
        /// Overload that reuses pre-allocated buffers from a
        /// <see cref="SvmTrainScratch"/>. Pass null
        /// to allocate per-call (the legacy path). For the hot Percolator
        /// path (CountPassing is called ~570x per grid-search session),
        /// passing scratch eliminates ~400 KB of per-call LOH allocation
        /// (int[scores.Length] + double[winners]) plus the
        /// CompeteFromIndices internal allocations via the scratch-aware
        /// helper below.
        /// </summary>
        public static int CountPassing(
            double[] scores, bool[] labels, uint[] entryIds, double fdrThreshold,
            SvmTrainScratch scratch)
        {
            if (scratch == null)
            {
                // Allocating path -- preserved verbatim for callers
                // that don't have a scratch (tests, non-hot sites).
                var allIndices = new int[scores.Length];
                for (int i = 0; i < scores.Length; i++)
                    allIndices[i] = i;

                int[] wi;
                double[] ws;
                bool[] wd;
                CompeteFromIndices(scores, labels, entryIds, allIndices, out wi, out ws, out wd);

                var qValues = new double[wi.Length];
                ComputeQvalues(ws, wd, qValues);

                int count = 0;
                for (int rank = 0; rank < wi.Length; rank++)
                {
                    if (!labels[wi[rank]] && qValues[rank] <= fdrThreshold)
                        count++;
                }
                return count;
            }

            scratch.EnsureCountPassingCapacity(scores.Length);
            int[] allIdx = scratch.CountPassingIndices;
            for (int i = 0; i < scores.Length; i++)
                allIdx[i] = i;

            int winnerCount = CompeteFromIndicesInto(
                scores, labels, entryIds, allIdx, scores.Length, scratch);

            double[] qVals = scratch.CountPassingQvalues;
            // ComputeQvalues operates on a winner-sized slice; pass the
            // prefix of the pooled arrays (Compute reads scores[i] for
            // i in [0, n), assuming n = winnerCount).
            ComputeQvaluesInto(
                scratch.CompetitionWinnerScores, scratch.CompetitionWinnerIsDecoy,
                qVals, winnerCount);

            int[] winIdx = scratch.CompetitionWinnerIndices;
            int passCount = 0;
            for (int rank = 0; rank < winnerCount; rank++)
            {
                if (!labels[winIdx[rank]] && qVals[rank] <= fdrThreshold)
                    passCount++;
            }
            return passCount;
        }

        /// <summary>
        /// Scratch-pooled internal variant of <see cref="CompeteFromIndices"/>.
        /// Writes winners into <paramref name="scratch"/>'s three
        /// CompetitionWinner* arrays (prefix [0..returned count) is
        /// active). Same algorithm as the allocating version; only the
        /// output destination differs. Returns the active winner count.
        /// </summary>
        private static int CompeteFromIndicesInto(
            double[] scores, bool[] labels, uint[] entryIds,
            int[] indices, int indicesCount,
            SvmTrainScratch scratch)
        {
            // Allocate the small per-call dictionaries / list at full
            // expected capacity to avoid rehash growth. Could be pooled
            // on scratch in a follow-up; the n*p allocations above are
            // the bigger LOH issue.
            var targets = new Dictionary<uint, KeyValuePair<int, double>>(indicesCount / 2);
            var decoys = new Dictionary<uint, KeyValuePair<int, double>>(indicesCount / 2);

            for (int ii = 0; ii < indicesCount; ii++)
            {
                int idx = indices[ii];
                uint baseId = entryIds[idx] & BASE_ID_MASK;
                double s = scores[idx];
                if (labels[idx])
                {
                    KeyValuePair<int, double> existing;
                    if (decoys.TryGetValue(baseId, out existing))
                    {
                        if (s > existing.Value)
                            decoys[baseId] = new KeyValuePair<int, double>(idx, s);
                    }
                    else
                    {
                        decoys[baseId] = new KeyValuePair<int, double>(idx, s);
                    }
                }
                else
                {
                    KeyValuePair<int, double> existing;
                    if (targets.TryGetValue(baseId, out existing))
                    {
                        if (s > existing.Value)
                            targets[baseId] = new KeyValuePair<int, double>(idx, s);
                    }
                    else
                    {
                        targets[baseId] = new KeyValuePair<int, double>(idx, s);
                    }
                }
            }

            // Walk pairs into local struct array (parallel-array layout
            // avoids the per-element Tuple class allocation that the
            // public CompeteFromIndices pays).
            int maxWinners = targets.Count + decoys.Count;
            scratch.EnsureCountPassingCapacity(maxWinners);
            int[] winIdx = scratch.CompetitionWinnerIndices;
            double[] winScores = scratch.CompetitionWinnerScores;
            bool[] winDecoy = scratch.CompetitionWinnerIsDecoy;
            // baseIds for tie-break ordering; reuse CountPassingIndices
            // as a uint[] surrogate (interpret bits). Cleaner: small
            // separate buffer; for now allocate per-call (small).
            var winBaseIds = new uint[maxWinners];

            int n = 0;
            foreach (var kvp in targets)
            {
                uint baseId = kvp.Key;
                int tIdx = kvp.Value.Key;
                double tScore = kvp.Value.Value;
                KeyValuePair<int, double> de;
                if (decoys.TryGetValue(baseId, out de))
                {
                    if (tScore > de.Value)
                    { winIdx[n] = tIdx; winScores[n] = tScore; winDecoy[n] = false; winBaseIds[n] = baseId; n++; }
                    else
                    { winIdx[n] = de.Key; winScores[n] = de.Value; winDecoy[n] = true; winBaseIds[n] = baseId; n++; }
                }
                else
                {
                    winIdx[n] = tIdx; winScores[n] = tScore; winDecoy[n] = false; winBaseIds[n] = baseId; n++;
                }
            }
            foreach (var kvp in decoys)
            {
                if (!targets.ContainsKey(kvp.Key))
                {
                    winIdx[n] = kvp.Value.Key; winScores[n] = kvp.Value.Value;
                    winDecoy[n] = true; winBaseIds[n] = kvp.Key; n++;
                }
            }

            // Sort: score desc, then baseId asc. Build index permutation
            // then permute the parallel arrays. Sorting an int[] of
            // length n with a comparison delegate beats the previous
            // List<Tuple<...>>.Sort because no per-element boxing was
            // required to populate the list.
            var perm = new int[n];
            for (int i = 0; i < n; i++) perm[i] = i;
            // The tie-break key (winBaseIds) is unique per row -- post-deduplication
            // best-per-precursor selection above guarantees one row per (base_id, isDecoy)
            // tuple -- so the comparator never returns 0 for distinct rows and introsort's
            // instability is moot. Exemption comment must be on the Array.Sort line itself
            // for the regex in CodeInspectionTest.TestNoUnstableArraySort to recognize it.
            Array.Sort(perm, (a, b) => // Array.Sort OK: unique baseId tie-break makes comparator total
            {
                int cmp = winScores[b].CompareTo(winScores[a]);
                if (cmp != 0) return cmp;
                return winBaseIds[a].CompareTo(winBaseIds[b]);
            });

            // Apply permutation in-place via scratch swap arrays. Reuse
            // the still-spare prefix of CountPassingQvalues as a double
            // swap buffer; for int and bool we need small temp arrays.
            var tmpIdx = new int[n];
            var tmpScores = new double[n];
            var tmpDecoy = new bool[n];
            for (int i = 0; i < n; i++)
            {
                tmpIdx[i] = winIdx[perm[i]];
                tmpScores[i] = winScores[perm[i]];
                tmpDecoy[i] = winDecoy[perm[i]];
            }
            Array.Copy(tmpIdx, winIdx, n);
            Array.Copy(tmpScores, winScores, n);
            Array.Copy(tmpDecoy, winDecoy, n);
            return n;
        }

        /// <summary>
        /// Variant of <see cref="ComputeQvalues"/> that operates on the
        /// active prefix [0..n) of pre-allocated arrays.
        /// </summary>
        private static void ComputeQvaluesInto(
            double[] scores, bool[] isDecoy, double[] qValuesOut, int n)
        {
            ComputeQvaluesCore(isDecoy, qValuesOut, n, decoyOffset: 0);
        }

        /// <summary>
        /// Shared core behind <see cref="ComputeConservativeQvalues"/>,
        /// <see cref="ComputeQvalues"/>, and <see cref="ComputeQvaluesInto"/>.
        /// Walks the score-descending prefix [0..<paramref name="n"/>)
        /// accumulating target / decoy counts, writes
        /// FDR = (nDecoy + <paramref name="decoyOffset"/>) / nTarget at each rank,
        /// then enforces a monotone-non-increasing q-value with a backward pass.
        /// <paramref name="decoyOffset"/> is 1 for the conservative (Savitski +1)
        /// estimate and 0 for the plain ratio. Scores are not read -- the input is
        /// assumed already sorted by score descending.
        /// </summary>
        private static void ComputeQvaluesCore(
            bool[] isDecoy, double[] qValues, int n, int decoyOffset)
        {
            int nTarget = 0;
            int nDecoy = 0;
            for (int i = 0; i < n; i++)
            {
                if (isDecoy[i])
                    nDecoy++;
                else
                    nTarget++;
                qValues[i] = nTarget > 0 ? (double)(nDecoy + decoyOffset) / nTarget : 1.0;
            }

            double qMin = 1.0;
            for (int i = n - 1; i >= 0; i--)
            {
                qMin = Math.Min(qMin, qValues[i]);
                qValues[i] = qMin;
            }
        }

        /// <summary>
        /// Count targets passing FDR threshold using conservative formula.
        /// </summary>
        public static int CountPassingConservative(
            double[] scores, bool[] labels, uint[] entryIds, double fdrThreshold)
        {
            var allIndices = new int[scores.Length];
            for (int i = 0; i < scores.Length; i++)
                allIndices[i] = i;

            int[] wi;
            double[] ws;
            bool[] wd;
            CompeteFromIndices(scores, labels, entryIds, allIndices, out wi, out ws, out wd);

            var qValues = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, qValues);

            int count = 0;
            for (int rank = 0; rank < wi.Length; rank++)
            {
                if (!labels[wi[rank]] && qValues[rank] <= fdrThreshold)
                    count++;
            }
            return count;
        }






        // ============================================================
        // Per-run and experiment-level q-value computation
        // ============================================================

        /// <summary>
        /// One file's per-run PRECURSOR q-values. Competes the file's rows -- the contiguous
        /// global index range in <paramref name="indices"/> (<c>[off, off+count)</c>) -- directly
        /// over the global score-pass arrays (no per-file slice copy; issue #4355 Part B), then
        /// maps each winning global index back to its local offset. Byte-identical to the per-file
        /// group body of <see cref="ComputePerRunPrecursorQvalues"/>: winners get their q, every
        /// other row stays 1.0. Returns a local array indexed 0..count-1 (the caller scatters it back).
        /// </summary>
        private static double[] ComputePerRunPrecursorQvaluesForFile(
            double[] scores, bool[] labels, uint[] entryIds, int[] indices, int off)
        {
            int count = indices.Length;
            var qvalues = new double[count];
            for (int i = 0; i < count; i++)
                qvalues[i] = 1.0;

            int[] wi;
            double[] ws;
            bool[] wd;
            CompeteFromIndices(scores, labels, entryIds, indices, out wi, out ws, out wd);

            var q = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, q);
            for (int rank = 0; rank < wi.Length; rank++)
                qvalues[wi[rank] - off] = q[rank];   // wi[rank] is a global index in [off, off+count)
            return qvalues;
        }

        /// <summary>
        /// One file's per-run PEPTIDE q-values, competing directly over the global arrays via the
        /// file's contiguous global index range <paramref name="indices"/> (<c>[off, off+count)</c>)
        /// -- no per-file slice copy (issue #4355 Part B). Byte-identical to the per-file group body
        /// of <see cref="ComputePerRunPeptideQvalues"/>: best-per-peptide over the file, competition,
        /// then the peptide's q propagated to every row of that peptide (others stay 1.0).
        /// <see cref="PercolatorSampling.BestPrecursorPerPeptide"/> returns global indices, which
        /// <see cref="CompeteFromIndices"/> then competes directly (both take an index subset).
        /// Returns a local array indexed 0..count-1.
        /// </summary>
        private static double[] ComputePerRunPeptideQvaluesForFile(
            double[] scores, bool[] labels, uint[] entryIds, string[] peptides, int[] indices, int off)
        {
            int count = indices.Length;
            var qvalues = new double[count];
            for (int i = 0; i < count; i++)
                qvalues[i] = 1.0;

            var bestPerPeptide = PercolatorSampling.BestPrecursorPerPeptide(indices, scores, labels, peptides);

            int[] wi;
            double[] ws;
            bool[] wd;
            CompeteFromIndices(scores, labels, entryIds, bestPerPeptide, out wi, out ws, out wd);

            var q = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, q);

            var peptideQvalue = new Dictionary<string, double>();
            for (int rank = 0; rank < wi.Length; rank++)
                peptideQvalue[peptides[wi[rank]]] = q[rank];   // wi[rank] is a global index

            for (int r = 0; r < count; r++)
            {
                double qv;
                if (peptideQvalue.TryGetValue(peptides[off + r], out qv))
                    qvalues[r] = qv;
            }
            return qvalues;
        }

        /// <summary>
        /// Computes one file's per-run precursor + peptide q-values from its contiguous slice
        /// <c>[off, off+count)</c> of the flat score-pass arrays (nested (file, row) order, so a
        /// file's rows are contiguous). Used by the projection score pass in place of the full
        /// double[n] per-run arrays (issue #4355 Part B); bounded to one file.
        /// </summary>
        internal static void ComputePerFileRunQvalues(
            double[] scores, bool[] labels, uint[] entryIds, string[] peptides,
            int off, int count,
            out double[] runPrecursorQvalues, out double[] runPeptideQvalues)
        {
            // A file's rows are the contiguous global range [off, off+count); compete directly over
            // the global arrays through this index buffer instead of copying four per-file slices
            // (issue #4355 Part B, Copilot review). One int[count] shared by both per-run passes.
            var indices = new int[count];
            for (int r = 0; r < count; r++)
                indices[r] = off + r;
            runPrecursorQvalues = ComputePerRunPrecursorQvaluesForFile(scores, labels, entryIds, indices, off);
            runPeptideQvalues = ComputePerRunPeptideQvaluesForFile(scores, labels, entryIds, peptides, indices, off);
        }

        internal static double[] ComputePerRunPrecursorQvalues(
            double[] scores, bool[] labels, uint[] entryIds, string[] fileNames)
        {
            int n = scores.Length;
            var qvalues = new double[n];
            for (int i = 0; i < n; i++)
                qvalues[i] = 1.0;

            var fileGroups = new Dictionary<string, List<int>>();
            for (int i = 0; i < n; i++)
            {
                List<int> list;
                if (!fileGroups.TryGetValue(fileNames[i], out list))
                {
                    list = new List<int>();
                    fileGroups[fileNames[i]] = list;
                }
                list.Add(i);
            }

            var progress = QProgress(@"Per-run precursor q-values", fileGroups.Count, n);
            int fileDone = 0;
            foreach (var group in fileGroups.Values)
            {
                progress?.Report(++fileDone);
                var fileScores = new double[group.Count];
                var fileLabels = new bool[group.Count];
                var fileEntryIds = new uint[group.Count];
                var allIndices = new int[group.Count];
                for (int i = 0; i < group.Count; i++)
                {
                    fileScores[i] = scores[group[i]];
                    fileLabels[i] = labels[group[i]];
                    fileEntryIds[i] = entryIds[group[i]];
                    allIndices[i] = i;
                }

                int[] wi;
                double[] ws;
                bool[] wd;
                CompeteFromIndices(fileScores, fileLabels, fileEntryIds, allIndices,
                    out wi, out ws, out wd);

                var q = new double[wi.Length];
                ComputeConservativeQvalues(ws, wd, q);

                for (int rank = 0; rank < wi.Length; rank++)
                {
                    int globalIdx = group[wi[rank]];
                    qvalues[globalIdx] = q[rank];
                }
            }
            progress?.Dispose();

            return qvalues;
        }

        /// <summary>
        /// STRATIFIED target-decoy competition q-values (OSPREY_PASS2_QVALUE=protein-compact):
        /// compete + compute q over ONLY the observations whose <c>base_id</c>
        /// (<c>entry_id &amp; 0x7FFFFFFF</c>) is in <paramref name="stratumBaseIds"/> --
        /// the peptides of proteins detected in the 1st pass, admitted as target+decoy
        /// PAIRS. Off-stratum observations get q = 1.0 (not reported).
        ///
        /// The sensitivity comes from reduced multiple testing: removing off-stratum
        /// (mostly-false) peptides removes their decoys from the null, so the decoy count
        /// above a given score drops and q falls for the stratum's marginal targets
        /// (independent filtering; Bourgon 2010). It stays honest because (a) the stratum
        /// is defined by protein membership, ~independent of a peptide's own decoy score
        /// under the null since a protein is detected via its OTHER peptides, and (b) the
        /// stratum keeps its paired decoys, including the ones that win -- so the null is a
        /// fair sample, not a target-winner-selected one (the failure mode of the old
        /// two-pass compaction). Uses the same conservative competition + q the
        /// full-population path uses; only the participating index set is constrained.
        /// </summary>
        internal static double[] ComputeStratifiedCompetitionQvalues(
            double[] scores, bool[] labels, uint[] entryIds, HashSet<uint> stratumBaseIds)
        {
            int n = scores.Length;
            var qvalues = new double[n];
            for (int i = 0; i < n; i++)
                qvalues[i] = 1.0;
            if (stratumBaseIds == null || stratumBaseIds.Count == 0)
                return qvalues;

            // Indices whose base_id is in the stratum -- target and decoy alike, so the
            // pair-symmetric null is preserved.
            var stratIdx = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (stratumBaseIds.Contains(entryIds[i] & BASE_ID_MASK))
                    stratIdx.Add(i);
            }
            if (stratIdx.Count == 0)
                return qvalues;

            var sScores = new double[stratIdx.Count];
            var sLabels = new bool[stratIdx.Count];
            var sEntryIds = new uint[stratIdx.Count];
            var allIndices = new int[stratIdx.Count];
            for (int i = 0; i < stratIdx.Count; i++)
            {
                sScores[i] = scores[stratIdx[i]];
                sLabels[i] = labels[stratIdx[i]];
                sEntryIds[i] = entryIds[stratIdx[i]];
                allIndices[i] = i;
            }

            int[] wi;
            double[] ws;
            bool[] wd;
            CompeteFromIndices(sScores, sLabels, sEntryIds, allIndices, out wi, out ws, out wd);

            var q = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, q);

            for (int rank = 0; rank < wi.Length; rank++)
                qvalues[stratIdx[wi[rank]]] = q[rank];

            return qvalues;
        }

        internal static double[] ComputePerRunPeptideQvalues(
            double[] scores, bool[] labels, uint[] entryIds,
            string[] fileNames, string[] peptides)
        {
            int n = scores.Length;
            var qvalues = new double[n];
            for (int i = 0; i < n; i++)
                qvalues[i] = 1.0;

            var fileGroups = new Dictionary<string, List<int>>();
            for (int i = 0; i < n; i++)
            {
                List<int> list;
                if (!fileGroups.TryGetValue(fileNames[i], out list))
                {
                    list = new List<int>();
                    fileGroups[fileNames[i]] = list;
                }
                list.Add(i);
            }

            var progress = QProgress(@"Per-run peptide q-values", fileGroups.Count, n);
            int fileDone = 0;
            foreach (var group in fileGroups.Values)
            {
                progress?.Report(++fileDone);
                var bestPerPeptide = PercolatorSampling.BestPrecursorPerPeptide(
                    group.ToArray(), scores, labels, peptides);

                var peptScores = new double[bestPerPeptide.Length];
                var peptLabels = new bool[bestPerPeptide.Length];
                var peptEntryIds = new uint[bestPerPeptide.Length];
                var allIndices = new int[bestPerPeptide.Length];
                for (int i = 0; i < bestPerPeptide.Length; i++)
                {
                    peptScores[i] = scores[bestPerPeptide[i]];
                    peptLabels[i] = labels[bestPerPeptide[i]];
                    peptEntryIds[i] = entryIds[bestPerPeptide[i]];
                    allIndices[i] = i;
                }

                int[] wi;
                double[] ws;
                bool[] wd;
                CompeteFromIndices(peptScores, peptLabels, peptEntryIds, allIndices,
                    out wi, out ws, out wd);

                var q = new double[wi.Length];
                ComputeConservativeQvalues(ws, wd, q);

                var peptideQvalue = new Dictionary<string, double>();
                for (int rank = 0; rank < wi.Length; rank++)
                {
                    int globalIdx = bestPerPeptide[wi[rank]];
                    peptideQvalue[peptides[globalIdx]] = q[rank];
                }

                foreach (int idx in group)
                {
                    double qv;
                    if (peptideQvalue.TryGetValue(peptides[idx], out qv))
                        qvalues[idx] = qv;
                }
            }
            progress?.Dispose();

            return qvalues;
        }

        /// <summary>
        /// Bounded (O(base_ids)) experiment-precursor q map: <c>base_id -&gt; q</c>. This is
        /// the intrinsic working set of the experiment-precursor competition -- one q per
        /// distinct base_id -- so it is what the projection score pass
        /// (<see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>) reads to assign each row's
        /// experiment-precursor q WITHOUT ever materializing the O(n) per-row array
        /// (issue #4355 Part B, bounded q-value reconstruction). The full-length
        /// <see cref="ComputeExperimentPrecursorQvalues"/> wrapper simply expands this map,
        /// so the two share the SAME competition + conservative-q math and cannot drift.
        /// </summary>
        internal static Dictionary<uint, double> ComputeExperimentPrecursorQMap(
            double[] scores, bool[] labels, uint[] entryIds)
        {
            int n = scores.Length;
            int[] wi;
            double[] ws;
            bool[] wd;
            using (var progress = QProgress(@"Experiment precursor q-values", n, n))
                CompeteAll(scores, labels, entryIds, out wi, out ws, out wd, progress);

            var q = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, q);

            // Winner's q-value keyed by base_id -- assigned to all observations sharing the
            // same base_id (both target and decoy sides) at expand/assign time. Matches
            // Rust's base_id_exp_prec_q HashMap at osprey-fdr/src/percolator.rs:2168 --
            // without this, non-winning per-file observations of a multi-file precursor stay
            // at q=1.0 and downstream stages that gate on experiment_precursor_qvalue (Stage
            // 6 calibration refit and reconciliation) miss the bulk of the consensus pool.
            var baseIdExpQ = new Dictionary<uint, double>();
            for (int rank = 0; rank < wi.Length; rank++)
            {
                uint baseId = entryIds[wi[rank]] & BASE_ID_MASK;
                baseIdExpQ[baseId] = q[rank];
            }
            return baseIdExpQ;
        }

        internal static double[] ComputeExperimentPrecursorQvalues(
            double[] scores, bool[] labels, uint[] entryIds)
        {
            int n = scores.Length;
            var qvalues = new double[n];
            var baseIdExpQ = ComputeExperimentPrecursorQMap(scores, labels, entryIds);
            for (int i = 0; i < n; i++)
            {
                double qv;
                qvalues[i] = baseIdExpQ.TryGetValue(entryIds[i] & BASE_ID_MASK, out qv) ? qv : 1.0;
            }
            return qvalues;
        }

        /// <summary>
        /// Bounded (O(peptides)) experiment-peptide q map: <c>peptide -&gt; q</c>. The
        /// intrinsic working set of the experiment-peptide competition -- one q per distinct
        /// peptide string -- which the projection score pass
        /// (<see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>) reads to assign each row's
        /// experiment-peptide q without materializing the O(n) per-row array (issue #4355
        /// Part B). The full-length <see cref="ComputeExperimentPeptideQvalues"/> wrapper
        /// expands this map, so both share the SAME best-per-peptide + competition +
        /// conservative-q math and cannot drift.
        /// </summary>
        internal static Dictionary<string, double> ComputeExperimentPeptideQMap(
            double[] scores, bool[] labels, uint[] entryIds, string[] peptides)
        {
            int n = scores.Length;
            var allIndices = new int[n];
            for (int i = 0; i < n; i++)
                allIndices[i] = i;

            var bestPerPeptide = PercolatorSampling.BestPrecursorPerPeptide(allIndices, scores, labels, peptides);

            var peptScores = new double[bestPerPeptide.Length];
            var peptLabels = new bool[bestPerPeptide.Length];
            var peptEntryIds = new uint[bestPerPeptide.Length];
            var allPeptIndices = new int[bestPerPeptide.Length];
            for (int i = 0; i < bestPerPeptide.Length; i++)
            {
                peptScores[i] = scores[bestPerPeptide[i]];
                peptLabels[i] = labels[bestPerPeptide[i]];
                peptEntryIds[i] = entryIds[bestPerPeptide[i]];
                allPeptIndices[i] = i;
            }

            int[] wi;
            double[] ws;
            bool[] wd;
            using (var progress = QProgress(@"Experiment peptide q-values", bestPerPeptide.Length, bestPerPeptide.Length))
                CompeteFromIndices(peptScores, peptLabels, peptEntryIds, allPeptIndices,
                    out wi, out ws, out wd, progress);

            var q = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, q);

            var peptideQvalue = new Dictionary<string, double>();
            for (int rank = 0; rank < wi.Length; rank++)
            {
                int globalIdx = bestPerPeptide[wi[rank]];
                peptideQvalue[peptides[globalIdx]] = q[rank];
            }
            return peptideQvalue;
        }

        internal static double[] ComputeExperimentPeptideQvalues(
            double[] scores, bool[] labels, uint[] entryIds, string[] peptides)
        {
            int n = scores.Length;
            var qvalues = new double[n];
            var peptideQvalue = ComputeExperimentPeptideQMap(scores, labels, entryIds, peptides);
            for (int i = 0; i < n; i++)
            {
                double qv;
                qvalues[i] = peptideQvalue.TryGetValue(peptides[i], out qv) ? qv : 1.0;
            }
            return qvalues;
        }

        /// <summary>
        /// Streaming builder for the three GLOBAL bounded first-pass q maps (issue #4355
        /// struct-shrink S3, Stage B): the experiment-precursor <c>base_id -&gt; q</c> map, the
        /// experiment-peptide <c>peptide -&gt; q</c> map, and the PEP <c>winner-ordinal -&gt; pep</c>
        /// map -- built by pushing each scored row via <see cref="Add"/> in flat (file,row) order
        /// instead of reading the resident <c>finalScores/labels/entryIds/peptides[n]</c> arrays.
        /// Bounded: it retains only per-base_id and per-peptide bests (O(distinct)), never an O(n)
        /// buffer. Each Build* reuses the SAME <see cref="CompeteFromDicts"/> +
        /// <see cref="ComputeConservativeQvalues"/> (+ <c>PepEstimator</c>) finish the flat
        /// <see cref="ComputeExperimentPrecursorQMap"/> / <see cref="ComputeExperimentPeptideQMap"/>
        /// / <see cref="ComputePepWinnerMap"/> run, so a population fed in the same order yields
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
                uint baseId = entryId & BASE_ID_MASK;
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
            /// <see cref="ComputeExperimentPrecursorQMap"/>.
            /// </summary>
            public Dictionary<uint, double> BuildExperimentPrecursorQMap()
            {
                CompeteFromDicts(_precTargets, _precDecoys,
                    out _, out double[] ws, out bool[] wd, out uint[] wb);
                var q = new double[ws.Length];
                ComputeConservativeQvalues(ws, wd, q);
                var map = new Dictionary<uint, double>(wb.Length);
                for (int rank = 0; rank < wb.Length; rank++)
                    map[wb[rank]] = q[rank];
                return map;
            }

            /// <summary>
            /// Experiment-peptide <c>peptide -&gt; q</c>: materialize the best-per-peptide set
            /// sorted by ordinal (matching <see cref="PercolatorSampling.BestPrecursorPerPeptide"/>'s sort), compete
            /// by base_id, conservative-q, keyed by the winner's peptide -- byte-identical to
            /// <see cref="ComputeExperimentPeptideQMap"/>.
            /// </summary>
            public Dictionary<string, double> BuildExperimentPeptideQMap()
            {
                var best = new List<PeptideBest>(_peptBest.Values);
                best.Sort((a, b) => a.G.CompareTo(b.G)); // Array.Sort OK: G is the unique streaming ordinal of each peptide's best row, so the comparator never ties -- reproduces BestPrecursorPerPeptide's result.Sort() on ascending global index
                var targets = new Dictionary<uint, KeyValuePair<int, double>>();
                var decoys = new Dictionary<uint, KeyValuePair<int, double>>();
                for (int i = 0; i < best.Count; i++)
                {
                    uint baseId = best[i].EntryId & BASE_ID_MASK;
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
                CompeteFromDicts(targets, decoys,
                    out int[] wi, out double[] ws, out bool[] wd, out _);
                var q = new double[ws.Length];
                ComputeConservativeQvalues(ws, wd, q);
                var map = new Dictionary<string, double>(wi.Length);
                for (int rank = 0; rank < wi.Length; rank++)
                    map[best[wi[rank]].Peptide] = q[rank];
                return map;
            }

            /// <summary>
            /// PEP <c>winner-ordinal -&gt; pep</c>: compete the global base_id bests, fit the PEP
            /// estimator on winners sorted base_id-ascending (the non-associative KDE sum is
            /// order-sensitive), then posterior-error each winner -- byte-identical to
            /// <see cref="ComputePepWinnerMap"/>.
            /// </summary>
            public Dictionary<int, double> BuildPepWinnerMap()
            {
                CompeteFromDicts(_precTargets, _precDecoys,
                    out int[] wi, out double[] ws, out bool[] wd, out uint[] wb);
                int nWinners = wi.Length;
                var pepOrder = new int[nWinners];
                for (int k = 0; k < nWinners; k++)
                    pepOrder[k] = k;
                Array.Sort(pepOrder, (a, b) => wb[a].CompareTo(wb[b])); // Array.Sort OK: one winner per base_id, so wb has no ties -- matches ComputePepWinnerMap
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

        // A console progress reporter for the large first-pass q-value / competition passes,
        // or null (no output) when the population is small enough that the pass is sub-second
        // -- keeps unit tests / Stellar clutter-free. Console-only; never affects the q-values.
        internal static ProgressReporter QProgress(string activity, long reportTotal, long workSize)
        {
            return workSize > 2_000_000 ? new ProgressReporter(activity, reportTotal) : null;
        }



    }
}
