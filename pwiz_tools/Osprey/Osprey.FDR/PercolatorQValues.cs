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

// Target-decoy q-value estimation for the Percolator FDR pipeline.
//
// The conservative (n_decoy+1)/n_target formula, per-run and experiment-level
// precursor and peptide q-values, the best-of-runs clamp, and posterior error
// probability via KDE + isotonic regression.
//
// Port of the q-value half of osprey-fdr/src/percolator.rs. Training lives in
// PercolatorTrainer, model application in PercolatorScorer, competition in
// TargetDecoyCompetition, and the bounded-memory forms in StreamingFdr.

using System;
using System.Collections.Generic;
using pwiz.Osprey.Core;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Target-decoy q-value estimation: the conservative (n_decoy+1)/n_target
    /// formula, the per-run and experiment-level precursor and peptide families,
    /// the best-of-runs monotonicity clamp, and the PEP fit.
    ///
    /// Takes a scored, competed population and says how confident each identification
    /// is. What produced the scores (<see cref="PercolatorTrainer"/>,
    /// <see cref="PercolatorScorer"/>) and which observation won its base id
    /// (<see cref="TargetDecoyCompetition"/>) are someone else's job.
    /// </summary>
    public static class PercolatorQValues
    {


        /// <summary>
        /// Bounded (O(base_ids)) posterior-error-probability (PEP) map: the global
        /// target-decoy competition winner index -&gt; its PEP. This is the intrinsic working
        /// set of the PEP step -- one PEP per competition winner (every other row's PEP is the
        /// default 1.0) -- so the projection score pass
        /// (<see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>) reads the map directly to set
        /// the winning rows' PEP without materializing the O(n) per-row array (issue #4355
        /// Part B). <see cref="StreamingFdr.ComputeStreamingCompetitionQvalues"/> expands the same map, so
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
                TargetDecoyCompetition.CompeteAll(finalScores, labels, entryIds,
                    out winnerIndices, out winnerScores, out winnerIsDecoy, pepProgress);

            int nWinners = winnerIndices.Length;
            var pepOrder = new int[nWinners];
            for (int k = 0; k < nWinners; k++)
                pepOrder[k] = k;
            Array.Sort(pepOrder, (a, b) => // Array.Sort OK: TDC's CompeteAll already produced one winner per base_id, so each base_id appears at most once in pepOrder -- no ties.
            {
                uint ba = entryIds[winnerIndices[a]] & PercolatorEntry.BASE_ID_MASK;
                uint bb = entryIds[winnerIndices[b]] & PercolatorEntry.BASE_ID_MASK;
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
            TargetDecoyCompetition.CompeteAll(scores, labels, entryIds,
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
                TargetDecoyCompetition.CompeteFromIndices(scores, labels, entryIds, allIndices, out wi, out ws, out wd);

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

            int winnerCount = TargetDecoyCompetition.CompeteFromIndicesInto(
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
            TargetDecoyCompetition.CompeteFromIndices(scores, labels, entryIds, allIndices, out wi, out ws, out wd);

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
            TargetDecoyCompetition.CompeteFromIndices(scores, labels, entryIds, indices, out wi, out ws, out wd);

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
        /// <see cref="TargetDecoyCompetition.CompeteFromIndices"/> then competes directly (both take an index subset).
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
            TargetDecoyCompetition.CompeteFromIndices(scores, labels, entryIds, bestPerPeptide, out wi, out ws, out wd);

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
                TargetDecoyCompetition.CompeteFromIndices(fileScores, fileLabels, fileEntryIds, allIndices,
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
                if (stratumBaseIds.Contains(entryIds[i] & PercolatorEntry.BASE_ID_MASK))
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
            TargetDecoyCompetition.CompeteFromIndices(sScores, sLabels, sEntryIds, allIndices, out wi, out ws, out wd);

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
                TargetDecoyCompetition.CompeteFromIndices(peptScores, peptLabels, peptEntryIds, allIndices,
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
        ///
        /// <c>applyExperimentAgg</c> is false on the 2nd pass. OSPREY_EXPERIMENT_AGG is a
        /// FIRST-pass score by definition (see its docs), and two of its premises break on the
        /// post-reconciliation survivor pool: gap-fill rows are appended there, so a group's
        /// observation count is inflated by fabricated detections and "runs detected" starts
        /// counting non-independent evidence - inverting the reproducibility metric the whole
        /// feature rests on - and the decoy floor would be estimated from the small,
        /// compaction-enriched survivor decoy set instead of the full null. Without this gate the
        /// shared primitive silently re-aggregated at pass 2.
        /// </summary>
        internal static Dictionary<uint, double> ComputeExperimentPrecursorQMap(
            double[] scores, bool[] labels, uint[] entryIds, bool applyExperimentAgg = true)
        {
            int n = scores.Length;
            int[] wi;
            double[] ws;
            bool[] wd;
            using (var progress = QProgress(@"Experiment precursor q-values", n, n))
            {
                if (applyExperimentAgg && OspreyEnvironment.ExperimentAggMeanBest)
                {
                    var aggScore = TargetDecoyCompetition.ComputeBaseIdMeanBestN(
                        scores, labels, entryIds, OspreyEnvironment.MeanBestN);
                    TargetDecoyCompetition.CompeteAll(aggScore, labels, entryIds, out wi, out ws, out wd, progress);
                }
                else
                {
                    TargetDecoyCompetition.CompeteAll(scores, labels, entryIds, out wi, out ws, out wd, progress);
                }
            }

            var q = new double[wi.Length];
            ComputeConservativeQvalues(ws, wd, q);

            // Winner's q-value keyed by the winner's FULL entry_id -- decoy bit intact.
            //
            // The reason for a map at all is unchanged: it assigns the q to every per-file
            // observation of the winning precursor, so that non-winning observations of a
            // multi-file precursor do not stay at q=1.0 and leave Stage 6 calibration refit
            // and reconciliation missing the bulk of the consensus pool. Keying on entry_id
            // still does that - all observations of one entry share an entry_id across files.
            //
            // What it must NOT do is cross the target/decoy boundary, and keying on base_id
            // did: a target and its decoy SHARE a base_id, so when the decoy won the
            // competition the target inherited the winner's q. Measured on the 82-file SEA-AD
            // run before this fix: 5 accepted precursors carried their paired decoy's q to 12
            // decimal places while scoring below it - e.g. base 1205336, target aggregate
            // -0.0521 against its decoy's +1.5943, both reporting q=0.004766. Those 5 are
            // reported at q <= 1% having LOST their pair, and they drag any score-space
            // acceptance boundary built from the accepted set onto the DECOY's scale (the
            // --model-diagnostics decoy row read 15.7x its definition because of them).
            //
            // This is the same rule ClampExperimentQToBestRun already states for the run-level
            // floors: "never the shared base_id / bare sequence - a target must not inherit its
            // paired decoy's good run". The loser of a competition is not in the ranking, so it
            // keeps the 1.0 default, which is what TDC means.
            //
            // Rust's base_id_exp_prec_q (osprey-fdr/src/percolator.rs) carried the identical
            // defect and was fixed with it in maccoss/osprey#63 (02d3df0), across all three of
            // its sites. Cross-impl is green again on this pair - precursors 29300 on both
            // sides, FDR sidecars per-field at 1e-9 - so the two are matched, not C#-ahead.
            var expQByWinnerId = new Dictionary<uint, double>();
            for (int rank = 0; rank < wi.Length; rank++)
            {
                expQByWinnerId[entryIds[wi[rank]]] = q[rank];
            }
            return expQByWinnerId;
        }

        /// <summary>
        /// Full-length (O(n) per-row) expansion of <see cref="ComputeExperimentPrecursorQMap"/>,
        /// used by the RESIDENT score pass. <c>applyExperimentAgg</c> must be threaded through
        /// from the pass label exactly as the projection path does: the two paths are each
        /// other's byte-identity oracle (<c>Pass2FdrSidecar</c> compares them), so a wrapper
        /// that always aggregates while the projection gates on the first pass makes them
        /// DISAGREE under OSPREY_EXPERIMENT_AGG.
        /// </summary>
        internal static double[] ComputeExperimentPrecursorQvalues(
            double[] scores, bool[] labels, uint[] entryIds, bool applyExperimentAgg = true)
        {
            int n = scores.Length;
            var qvalues = new double[n];
            var expQByWinnerId = ComputeExperimentPrecursorQMap(scores, labels, entryIds, applyExperimentAgg);
            for (int i = 0; i < n; i++)
            {
                // Full entry_id, NOT the base id: an entry takes the q only when it was the
                // side that won its own competition. See the map builder for why.
                double qv;
                qvalues[i] = expQByWinnerId.TryGetValue(entryIds[i], out qv) ? qv : 1.0;
            }
            return qvalues;
        }

        /// <summary>
        /// Bounded (O(distinct entry_ids)) map of the score the EXPERIMENT-scope competitions
        /// rank each entry on: <c>entry_id -&gt; aggregate score</c> (sidecar v4, issue #4522).
        /// Persisted beside the experiment q-values it produced, so a consumer can build a
        /// score-space acceptance boundary at experiment scope without rebuilding the roll-up
        /// itself - the reconstruction that has to branch on <c>OSPREY_EXPERIMENT_AGG</c> and is
        /// therefore silently wrong on exactly the arms where the aggregation is under study.
        ///
        /// <para>Uses the SAME <c>effScores</c> selection as
        /// <see cref="ComputeExperimentPrecursorQMap"/> / <see cref="ComputeExperimentPeptideQMap"/>,
        /// so it cannot report a score those competitions did not rank on. Under the default
        /// aggregation <c>effScores == scores</c> and the max-per-entry reduction below is the
        /// same reduction <see cref="TargetDecoyCompetition.CompeteAll"/> performs per base_id;
        /// under mean-best-N every row of an entry already carries the group value, so the max
        /// is the identity. Either way every row of an entry gets the same number and the
        /// consumer just compares.</para>
        ///
        /// <para>Keyed by FULL entry_id, not base_id: a target and its decoy are distinct
        /// entries with distinct aggregates (and
        /// <see cref="TargetDecoyCompetition.ComputeBaseIdMeanBestN"/> likewise accumulates
        /// them separately), even though the competition that consumes them pairs the two.</para>
        /// </summary>
        internal static Dictionary<uint, double> ComputeExperimentAggregateScoreMap(
            double[] scores, bool[] labels, uint[] entryIds, bool applyExperimentAgg = true)
        {
            double[] effScores = applyExperimentAgg && OspreyEnvironment.ExperimentAggMeanBest
                ? TargetDecoyCompetition.ComputeBaseIdMeanBestN(scores, labels, entryIds, OspreyEnvironment.MeanBestN)
                : scores;

            var aggByEntryId = new Dictionary<uint, double>();
            for (int i = 0; i < effScores.Length; i++)
            {
                if (!aggByEntryId.TryGetValue(entryIds[i], out double cur) || effScores[i] > cur)
                    aggByEntryId[entryIds[i]] = effScores[i];
            }
            return aggByEntryId;
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
        ///
        /// <c>applyExperimentAgg</c> is false on the 2nd pass; see
        /// <see cref="ComputeExperimentPrecursorQMap"/> for why the aggregation is first-pass only.
        /// </summary>
        internal static Dictionary<string, double> ComputeExperimentPeptideQMap(
            double[] scores, bool[] labels, uint[] entryIds, string[] peptides,
            bool applyExperimentAgg = true)
        {
            int n = scores.Length;

            // Reproducibility roll-up (OSPREY_EXPERIMENT_AGG=mean-best-2): the peptide score is
            // the MAX over its precursors of each precursor's mean-best-2 score. Substituting the
            // per-row mean-best-2 array for the raw scores turns BestPrecursorPerPeptide's
            // max-over-observations into exactly that (every observation of a base_id carries the
            // same precursor score). Default (max) is byte-identical: effScores == scores.
            double[] effScores = applyExperimentAgg && OspreyEnvironment.ExperimentAggMeanBest
                ? TargetDecoyCompetition.ComputeBaseIdMeanBestN(scores, labels, entryIds, OspreyEnvironment.MeanBestN)
                : scores;

            var allIndices = new int[n];
            for (int i = 0; i < n; i++)
                allIndices[i] = i;

            var bestPerPeptide = PercolatorSampling.BestPrecursorPerPeptide(allIndices, effScores, labels, peptides);

            var peptScores = new double[bestPerPeptide.Length];
            var peptLabels = new bool[bestPerPeptide.Length];
            var peptEntryIds = new uint[bestPerPeptide.Length];
            var allPeptIndices = new int[bestPerPeptide.Length];
            for (int i = 0; i < bestPerPeptide.Length; i++)
            {
                peptScores[i] = effScores[bestPerPeptide[i]];
                peptLabels[i] = labels[bestPerPeptide[i]];
                peptEntryIds[i] = entryIds[bestPerPeptide[i]];
                allPeptIndices[i] = i;
            }

            int[] wi;
            double[] ws;
            bool[] wd;
            using (var progress = QProgress(@"Experiment peptide q-values", bestPerPeptide.Length, bestPerPeptide.Length))
                TargetDecoyCompetition.CompeteFromIndices(peptScores, peptLabels, peptEntryIds, allPeptIndices,
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

        /// <summary>
        /// Full-length (O(n) per-row) expansion of <see cref="ComputeExperimentPeptideQMap"/>,
        /// used by the RESIDENT score pass; see
        /// <see cref="ComputeExperimentPrecursorQvalues"/> for why
        /// <c>applyExperimentAgg</c> must be threaded through rather than defaulted.
        /// </summary>
        internal static double[] ComputeExperimentPeptideQvalues(
            double[] scores, bool[] labels, uint[] entryIds, string[] peptides,
            bool applyExperimentAgg = true)
        {
            int n = scores.Length;
            var qvalues = new double[n];
            var peptideQvalue = ComputeExperimentPeptideQMap(
                scores, labels, entryIds, peptides, applyExperimentAgg);
            for (int i = 0; i < n; i++)
            {
                double qv;
                qvalues[i] = peptideQvalue.TryGetValue(peptides[i], out qv) ? qv : 1.0;
            }
            return qvalues;
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
