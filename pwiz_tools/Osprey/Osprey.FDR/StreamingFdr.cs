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
        ///
        /// <paramref name="applyExperimentAgg"/> is false on the 2nd pass, mirroring the
        /// projection score pass
        /// (<see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>); see
        /// <see cref="PercolatorQValues.ComputeExperimentPrecursorQMap"/> for why
        /// OSPREY_EXPERIMENT_AGG is a first-pass score by definition. The two score passes are
        /// each other's byte-identity oracle, so this flag must be threaded from the same
        /// pass label on both.
        /// </summary>
        internal static void ComputeStreamingCompetitionQvalues(
            double[] finalScores, bool[] labels, uint[] entryIds,
            string[] peptides, string[] fileNames,
            out double[] peps, out double[] runPrecursorQvalues,
            out double[] runPeptideQvalues, out double[] expPrecursorQvalues,
            out double[] expPeptideQvalues, bool applyExperimentAgg = true)
        {
            int n = finalScores.Length;

            // PEP via global target-decoy competition. The bounded winner->PEP map
            // (base_id-ascending KDE order -- see PercolatorQValues.ComputePepWinnerMap) is expanded to the
            // full per-row peps array here; the projection score pass reads the map directly
            // so the O(n) array is never materialized (issue #4355 Part B).
            var pepByWinnerIdx = PercolatorQValues.ComputePepWinnerMap(finalScores, labels, entryIds);
            peps = new double[n];
            for (int i = 0; i < n; i++)
                peps[i] = 1.0;
            foreach (var kv in pepByWinnerIdx)
                peps[kv.Key] = kv.Value;

            // Per-run precursor + peptide q-values (each file independently).
            runPrecursorQvalues = PercolatorQValues.ComputePerRunPrecursorQvalues(
                finalScores, labels, entryIds, fileNames);
            runPeptideQvalues = PercolatorQValues.ComputePerRunPeptideQvalues(
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
                expPrecursorQvalues = PercolatorQValues.ComputeExperimentPrecursorQvalues(
                    finalScores, labels, entryIds, applyExperimentAgg);
                expPeptideQvalues = PercolatorQValues.ComputeExperimentPeptideQvalues(
                    finalScores, labels, entryIds, peptides, applyExperimentAgg);
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
        /// Bounded-memory streaming form of <see cref="PercolatorQValues.ComputeFullPopulationPrecursorFdr"/> for
        /// OSPREY_PASS2_QVALUE=transfer-compete. Streams one file's 1st-pass population at a time
        /// (run-level competition + conservative q per file) while accumulating only the
        /// per-base_id best target/decoy observation for the experiment-level competition.
        ///
        /// NOTHING per-observation outlives the file that produced it. Each file's run q is handed
        /// to <paramref name="onFileRunQ"/> and then dropped, and the experiment-level values come
        /// back as the bounded <see cref="StreamedCompetitionState"/>, which derives a survivor's
        /// experiment q and PEP on demand from O(distinct) state, so the caller emits those one
        /// file at a time as well. Resident footprint is O(distinct precursors + distinct survivor
        /// entry_ids + largest single file), flat in file count, where the resident overload is
        /// O(total observations). The earlier streaming form was flat in its ACCUMULATORS but
        /// still returned three whole-run (file, entry_id)-keyed dictionaries, which measured
        /// ~16 GB of the 82-file Stage 7 peak (#4486); the roll-up order was already right, the
        /// retention was not.
        ///
        /// Emits run/experiment precursor q and PEP identical to the resident method for the
        /// reported survivors (verified byte-for-byte on the 3-file Stellar entrapment set). File
        /// reading is injected so this assembly needs no IO dependency.
        /// </summary>
        /// <param name="fileKeys">Stable per-file keys to stream, in any order.</param>
        /// <param name="readFile">Reads one file's full population as (entryIds, scores) plus the
        ///   frozen-model score to substitute for each of THAT FILE's reconciled survivors, keyed
        ///   by entry_id (observations absent from it keep their stored 1st-pass score). Invoked
        ///   once per file; everything it returns is released before the next file is read, so the
        ///   caller loads that file's features inside it rather than pre-building a whole-run
        ///   score map.</param>
        /// <param name="survivorEntryIds">Every reported survivor entry_id, across all files. The
        ///   best-of-runs floor is a per-entry_id minimum over every file the entry won in, so
        ///   this one set is genuinely global, but it is O(distinct entry_ids), not
        ///   O(files x entry_ids).</param>
        /// <param name="onFileRunQ">Receives each file's run-level precursor q by entry_id as soon
        ///   as that file's competition finishes, for the survivor entry_ids that won in it. The
        ///   map is the caller's to consume and is not retained here; an entry the caller holds
        ///   for this file that is absent from it won no competition and takes run q 1.0.</param>
        /// <param name="stratumBaseIds">Null for the full-population competition; non-null restricts
        ///   the competition to these base_ids (protein-compact).</param>
        public static StreamedCompetitionState ComputeFullPopulationPrecursorFdrStreaming(
            IReadOnlyList<string> fileKeys,
            Func<string, (uint[] entryIds, double[] scores, IReadOnlyDictionary<uint, double> survivorScores)> readFile,
            IReadOnlyCollection<uint> survivorEntryIds,
            Action<string, IReadOnlyDictionary<uint, double>> onFileRunQ,
            HashSet<uint> stratumBaseIds = null)
        {
            // stratumBaseIds == null -> full-population competition (transfer-compete).
            // non-null -> STRATIFIED competition (protein-compact): only observations whose
            // base_id is in the stratum participate in the run/experiment competitions, so
            // off-stratum decoys leave the null (reduced multiple testing). The per-base_id
            // maps hold only stratum members, so peak memory stays flat in file count -- it
            // only shrinks relative to the full-population path.
            var survivorIds = survivorEntryIds as HashSet<uint> ?? new HashSet<uint>(survivorEntryIds);

            // Experiment-level per-base_id best target/decoy observation (score + locator),
            // accumulated across every file. Bounded by the number of distinct precursors.
            var bestTarget = new Dictionary<uint, (double score, int fileIdx, uint entryId)>();
            var bestDecoy = new Dictionary<uint, (double score, int fileIdx, uint entryId)>();

            // Best (min) run q per SURVIVOR entry_id across the files it won in -- the
            // best-of-runs monotonicity floor for the experiment q (only survivors are emitted).
            var minRunQ = new Dictionary<uint, double>(survivorIds.Count);

            for (int fileIdx = 0; fileIdx < fileKeys.Count; fileIdx++)
            {
                string fileKey = fileKeys[fileIdx];
                var (entryIds, scores, survivorScores) = readFile(fileKey);
                int m = entryIds.Length;
                var labels = new bool[m];
                // Non-null only when stratified: the base_ids whose observations Stage 6 actually
                // CHANGED. Filled in the scoring pass below rather than recorded positionally in a
                // parallel bool[m] and re-read afterwards. Two reasons beyond the saved pass: the
                // bool[] and this set used to be live at the same time, so dropping it lowers the
                // peak; and a base_id-keyed set needs no materialized per-entry array, which is
                // the shape that survives if this ever streams entries instead of reading them
                // into arrays. See the admission rationale below for what "changed" means here.
                var changedBaseIds = stratumBaseIds != null ? new HashSet<uint>() : null;
                for (int i = 0; i < m; i++)
                {
                    uint eid = entryIds[i];
                    labels[i] = (eid & ~PercolatorEntry.BASE_ID_MASK) != 0u; // decoy high bit set
                    if (survivorScores.TryGetValue(eid, out double ov))
                    {
                        // BIT-EXACT inequality is the "changed" discriminator, the same one
                        // Pass2FdrSidecar.AssignPerRunQ uses to separate Moved from Unchanged: an
                        // unchanged survivor's reconciled features ARE its original Stage-4
                        // features (ReconciledParquetWriter streams unchanged rows through
                        // untouched) and the sidecar score came from those same features under
                        // this same averaged model, so the recomputation reproduces it exactly.
                        // A moved peak carries rescored features, so its score differs.
                        if (changedBaseIds != null && ov != scores[i])
                            changedBaseIds.Add(eid & PercolatorEntry.BASE_ID_MASK);
                        scores[i] = ov; // swap in the reconciled survivor's frozen-model score
                    }
                }

                // protein-compact: a peak Stage 6 CHANGED (reconciliation moved it, or gap-fill
                // created it) carries a NEW composite score and no longer has a valid pass-1
                // run q -- the old q described a peak that no longer exists, and
                // PerFileRescoreTask's post-rescore overlay zeroes it precisely to say so. Such
                // a peak must EARN a fresh run q here, exactly the way on-stratum members do;
                // neither inheriting the prior q nor keeping the q=1 sentinel is a calibrated
                // answer for it. The "changed" signal is a frozen-model score that DIFFERS from
                // the entry's 1st-pass sidecar score, computed above - NOT mere presence in
                // survivorScoreOverride. That map holds every post-reconciliation survivor whose
                // identity resolves in the effective parquet, including files Stage 6 never
                // touched (the effective path falls back to the ORIGINAL parquet), so keying on
                // presence admitted most of the survivor pool and quietly widened the very
                // stratum this mode exists to enforce.
                //
                // The score comparison is keyed by entry_id, so it means the same thing
                // in-process and on a distributed SecondPassFDR node (an index-keyed source does NOT -
                // see #4484), and it needs no extra plumbing: both scores are already in hand.
                //
                // Admitted BY BASE_ID so a target and its paired decoy always enter together.
                // Admitting a lone target would let it auto-win its competition and inflate the
                // null, the cross-validation grouping invariant this file depends on. The set is
                // complete before this point: it is filled by the scoring pass above, and every
                // entry of this file has been through that pass. That ordering matters, because a
                // base_id changed at a LATER entry still admits an earlier one.

                // Called only from the stratified branch below, where both sets are non-null -
                // the unstratified path builds allIdx directly and never asks. The null guards
                // this started with were therefore dead, and ReSharper reported them as such.
                bool Admit(uint baseId)
                {
                    return stratumBaseIds.Contains(baseId) || changedBaseIds.Contains(baseId);
                }

                // Run-level: compete within this file (stratum members plus changed peaks when
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
                        if (Admit(entryIds[i] & PercolatorEntry.BASE_ID_MASK)) idxList.Add(i);
                    allIdx = idxList.ToArray();
                }
                TargetDecoyCompetition.CompeteFromIndices(scores, labels, entryIds, allIdx,
                    out int[] wi, out double[] ws, out bool[] wd);
                var q = new double[wi.Length];
                PercolatorQValues.ComputeConservativeQvalues(ws, wd, q);
                // This file's run q, handed to the caller at the end of the iteration and then
                // dropped. Sized by the survivors that won a competition HERE, not by the survivor
                // set across all files, which is what makes the phase flat in file count. The
                // caller filters to the survivors it actually holds for this file, so the old
                // (fileKey, entryId) membership test is now the caller's own per-file list.
                var fileRunQ = new Dictionary<uint, double>();
                for (int rank = 0; rank < wi.Length; rank++)
                {
                    uint eid = entryIds[wi[rank]];
                    if (!survivorIds.Contains(eid)) continue;
                    double qv = q[rank];
                    fileRunQ[eid] = qv;
                    if (!minRunQ.TryGetValue(eid, out double cur) || qv < cur) minRunQ[eid] = qv;
                }

                // Experiment-level: fold every observation into the per-base_id bests. When
                // stratified this is the STRATUM ONLY - deliberately NOT the run-level admitted
                // set, which also carries the changed off-stratum peaks.
                //
                // Two reasons an off-stratum peak must not enter a cross-file maximum here.
                // First, it would be admitted only in the files that CHANGED it, so its best
                // would be a max over that subset while every stratum member maxes over all
                // files - and because reconciliation anchors on the best-scoring peak and
                // corrects the others toward it, a changed peak is never the one that supplied
                // the maximum. Maxing over changed observations alone is therefore GUARANTEED to
                // understate the precursor's experiment-wide score, not merely to skew it.
                // Second, the correct value is already known: the pass-1 experiment q, which by
                // that same anchor argument reconciliation cannot have invalidated. The caller
                // carries it through rather than recomputing it, which is what keeps the
                // re-scoping additive - see Pass2FdrSidecar's map-back.
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

                // Hand this file's run q over and let it go. Called after the experiment fold so
                // the caller cannot observe a partially-folded file.
                onFileRunQ(fileKey, fileRunQ);
                // entryIds/scores/labels/allIdx/fileRunQ and the caller's per-file survivor scores
                // are all released here, before the next file is read.
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
            // The experiment aggregate score PER FULL ENTRY_ID (sidecar v4, issue #4522) - the
            // score this competition ranked each entry on. Keyed by entry_id and not by base_id,
            // exactly as PercolatorQValues.ComputeExperimentAggregateScoreMap is on the 1st pass:
            // a target and its decoy are distinct entries with distinct aggregates even though
            // the competition below pairs them. winnerLoc cannot serve this purpose - it holds
            // only the WINNER of each pair, so reading it for a decoy would hand back the
            // target's score whenever the target won.
            //
            // bestTarget / bestDecoy already carry exactly what is needed: each is the max over
            // that entry's observations across every file, which is the same max-over-rows
            // reduction the 1st-pass producer performs. The mean-best-N branch is deliberately
            // absent because the experiment aggregation is 1st-pass only (see
            // ComputeExperimentPrecursorQMap), so effScores == scores here.
            var aggByEntryId = new Dictionary<uint, double>(w * 2);
            int wi2 = 0;
            foreach (uint bid in baseIds)
            {
                bool hasT = bestTarget.TryGetValue(bid, out var t);
                bool hasD = bestDecoy.TryGetValue(bid, out var d);
                if (hasT)
                    aggByEntryId[t.entryId] = t.score;
                if (hasD)
                    aggByEntryId[d.entryId] = d.score;
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
            PercolatorQValues.ComputeConservativeQvalues(sortedScore, sortedDecoy, qExp);
            // Keyed by the WINNER's full entry_id, decoy bit intact - never the shared base_id.
            // sortedDecoy carries which side won, so the winner's entry_id is reconstructible
            // here without a second array, leaving the competition's sort and tie-break
            // untouched. See PercolatorQValues.ComputeExperimentPrecursorQMap for the defect
            // this closes: on base_id, a target whose DECOY won inherited the winner's q.
            var expQByWinnerId = new Dictionary<uint, double>(w);
            for (int i = 0; i < w; i++)
            {
                uint winnerId = sortedDecoy[i]
                    ? sortedBaseId[i] | ~PercolatorEntry.BASE_ID_MASK
                    : sortedBaseId[i];
                expQByWinnerId[winnerId] = qExp[i];
            }

            var pepEstimator = PepEstimator.FitDefault(expScore, expIsDecoy);

            // The per-survivor loop that used to live here - one iteration per (file, entry_id),
            // filling three whole-run dictionaries - is now the caller's per-file emit pass. Every
            // value it produced is a function of the bounded state below plus the survivor's own
            // run q, so handing that state back costs O(distinct) instead of O(files x entries).
            return new StreamedCompetitionState(
                expQByWinnerId, minRunQ, winnerLoc, aggByEntryId, pepEstimator, fileKeys, fileKeys.Count > 1);
        }

        /// <summary>
        /// The bounded cross-file result of <see cref="ComputeFullPopulationPrecursorFdrStreaming"/>:
        /// everything needed to finish a survivor's q-values, held as O(distinct base_id) /
        /// O(distinct survivor entry_id) maps rather than one entry per observation. The caller
        /// walks its own per-file survivor lists and asks for each value in turn, so no
        /// (file, entry_id)-keyed buffer is ever materialized (#4486).
        /// </summary>
        public sealed class StreamedCompetitionState
        {
            private readonly Dictionary<uint, double> _expQByWinnerId;
            private readonly Dictionary<uint, double> _minRunQ;
            private readonly Dictionary<uint, (int fileIdx, uint entryId, double score)> _winnerLoc;
            private readonly Dictionary<uint, double> _aggByEntryId;
            private readonly PepEstimator _pepEstimator;
            private readonly IReadOnlyList<string> _fileKeys;
            private readonly bool _multiFile;

            internal StreamedCompetitionState(
                Dictionary<uint, double> expQByWinnerId,
                Dictionary<uint, double> minRunQ,
                Dictionary<uint, (int fileIdx, uint entryId, double score)> winnerLoc,
                Dictionary<uint, double> aggByEntryId,
                PepEstimator pepEstimator,
                IReadOnlyList<string> fileKeys,
                bool multiFile)
            {
                _expQByWinnerId = expQByWinnerId;
                _minRunQ = minRunQ;
                _winnerLoc = winnerLoc;
                _aggByEntryId = aggByEntryId;
                _pepEstimator = pepEstimator;
                _fileKeys = fileKeys;
                _multiFile = multiFile;
            }

            /// <summary>
            /// Experiment-level precursor q for one survivor observation: the base_id winner's q,
            /// floored up to that precursor's best (min-over-runs) run q. An entry_id that won no
            /// within-file competition has best run q 1.0 (every observation stayed at the q=1.0
            /// default), matching the resident bestRunQ. A single-file run short-circuits to the
            /// run q, as the resident path does.
            /// </summary>
            public double ExperimentQ(uint entryId, double runQ)
            {
                if (!_multiFile)
                    return runQ;
                // Full entry_id: an entry takes the experiment q only when it was the side
                // that WON its own target/decoy competition. The loser keeps 1.0.
                double eq = _expQByWinnerId.TryGetValue(entryId, out double bq) ? bq : 1.0;
                double floorQ = _minRunQ.TryGetValue(entryId, out double mrq) ? mrq : 1.0;
                return eq < floorQ ? floorQ : eq;
            }

            /// <summary>
            /// The experiment aggregate score this competition ranked <paramref name="entryId"/>
            /// on (sidecar v4, issue #4522), or <c>null</c> when the entry took no part in the
            /// experiment fold - which under protein-compact means an OFF-STRATUM entry, whose
            /// pass-1 aggregate the caller carries through untouched beside the pass-1
            /// experiment q it also carries.
            ///
            /// <para><b>Nullable, not an in-band sentinel.</b> The score is a signed
            /// discriminant, so 0.0 is an ordinary mid-distribution value: returning it for "not
            /// competed" is indistinguishable from a real score, and a consumer building a
            /// score-space acceptance boundary then takes a minimum over fabricated zeros. That
            /// is not hypothetical - it is exactly how this panel came to report 542,368 decoys
            /// against 117,783 targets on astral.</para>
            ///
            /// <para>NaN would fix that much, but <c>double?</c> is chosen over it deliberately.
            /// NaN propagates silently through arithmetic and comparisons, so a caller that
            /// forgets the check persists NaN into the v4 record - and the sidecar comparators
            /// test <c>Math.Abs(a - b) &lt;= tolerance</c>, which is FALSE for NaN against NaN,
            /// turning byte-identical files into a red gate. <c>double?</c> makes that caller
            /// fail to compile instead. The dictionary behind this stays
            /// <c>Dictionary&lt;uint,double&gt;</c>, so nothing is boxed and no per-entry
            /// Nullable overhead is paid; only the return is lifted.</para>
            /// </summary>
            public double? ExperimentAggregateScore(uint entryId)
            {
                return _aggByEntryId.TryGetValue(entryId, out double v) ? v : null;
            }

            /// <summary>
            /// PEP for one survivor observation. Real only on the single experiment-winner
            /// observation of each base_id - the winner set the estimator was fit over - so every
            /// other observation of that precursor reports 1.0.
            /// </summary>
            public double Pep(string fileKey, uint entryId)
            {
                uint baseId = entryId & PercolatorEntry.BASE_ID_MASK;
                if (_winnerLoc.TryGetValue(baseId, out var loc) &&
                    loc.entryId == entryId && _fileKeys[loc.fileIdx] == fileKey)
                    return _pepEstimator.PosteriorError(loc.score);
                return 1.0;
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

            // Reproducibility mean(best-N) mode (OSPREY_EXPERIMENT_AGG=mean-best-<N>): per-base_id
            // top-N accumulators, targets and decoys kept apart exactly like the resident
            // TargetDecoyCompetition.ComputeBaseIdMeanBestN pair of dicts, plus a bounded decoy-score
            // floor. Allocated only in mean-best-N mode (_meanBestN >= 2); the default max path leaves
            // them null, feeds only the raw bests above, and stays byte-identical to the committed
            // golden.
            private readonly int _meanBestN;
            private readonly Dictionary<uint, Mb2Entry> _mb2Targets;
            private readonly Dictionary<uint, Mb2Entry> _mb2Decoys;
            private readonly StreamingDecoyFloor _floor;

            public StreamingFirstPassQ() : this(0)
            {
            }

            public StreamingFirstPassQ(int meanBestN)
            {
                _meanBestN = meanBestN;
                if (meanBestN < 2)
                    return;
                _mb2Targets = new Dictionary<uint, Mb2Entry>();
                _mb2Decoys = new Dictionary<uint, Mb2Entry>();
                _floor = new StreamingDecoyFloor();
            }

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

                if (_meanBestN >= 2)
                {
                    // Experiment precursor/peptide q use the mean(best-N) score: accumulate the
                    // top-N per (base_id, side) and, from decoys, the missing-run floor. The raw
                    // _precTargets/_precDecoys bests above still feed PEP - matching the resident
                    // ComputeStreamingCompetitionQvalues, where PEP reads finalScores while the
                    // experiment q reads ComputeBaseIdMeanBestN. The per-peptide raw max (_peptBest)
                    // is not needed here (the roll-up is derived from the top-N accumulators).
                    var mb2 = isDecoy ? _mb2Decoys : _mb2Targets;
                    if (mb2.TryGetValue(baseId, out Mb2Entry e))
                    {
                        e.Acc.Add(score, _meanBestN);
                        mb2[baseId] = e;
                    }
                    else
                    {
                        mb2[baseId] = new Mb2Entry(
                            TargetDecoyCompetition.MeanBestNAcc.First(score, _meanBestN), g, peptide);
                    }
                    if (isDecoy)
                        _floor.Add(score);
                    return;
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
            /// Experiment-precursor <c>entry_id -&gt; q</c>: compete the global base_id bests,
            /// conservative-q, keyed by each WINNER's full entry_id -- byte-identical to
            /// <see cref="PercolatorQValues.ComputeExperimentPrecursorQMap"/>, which this is the
            /// streaming oracle for. Keyed on entry_id and not base_id because a target and its
            /// decoy share a base_id, so a base_id key hands the loser the winner's q; see that
            /// method for the measurement.
            /// </summary>
            public Dictionary<uint, double> BuildExperimentPrecursorQMap()
            {
                ResolveExperimentBests(out var targets, out var decoys);
                TargetDecoyCompetition.CompeteFromDicts(targets, decoys,
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
            /// <see cref="BuildExperimentPrecursorQMap"/> competes, via the shared
            /// <see cref="ResolveExperimentBests"/>, so the score reported beside an experiment
            /// q is by construction the one that q was computed from - on the default max path
            /// and the mean-best-N path alike.
            ///
            /// <para>Keyed by FULL entry_id: the two dictionaries are split by side, and a
            /// base_id plus its side is exactly the entry_id
            /// (<c>base_id | <see cref="LibraryEntry.DECOY_ID_BIT"/></c> for a decoy), so
            /// re-attaching the bit here recovers the per-entry key the sidecar record uses
            /// without the caller having to mask anything.</para>
            /// </summary>
            public Dictionary<uint, double> BuildExperimentAggregateScoreMap()
            {
                ResolveExperimentBests(out var targets, out var decoys);
                var map = new Dictionary<uint, double>(targets.Count + decoys.Count);
                foreach (var kvp in targets)
                    map[kvp.Key] = kvp.Value.Value;
                foreach (var kvp in decoys)
                    map[kvp.Key | LibraryEntry.DECOY_ID_BIT] = kvp.Value.Value;
                return map;
            }

            /// <summary>
            /// The per-(base_id, side) best scores the experiment-scope competitions run on:
            /// the raw streamed maxima on the default path, or each top-N accumulator reduced
            /// to its mean(best-N) score (missing runs at the decoy floor) under
            /// OSPREY_EXPERIMENT_AGG. The reduced maps are what the resident
            /// <c>ComputeBaseIdMeanBestN</c> -&gt; <c>CompeteAll</c> path builds; every row of a
            /// base_id shares that score there, so the resident max-per-base_id reduction is a
            /// no-op and the two paths agree (modulo the streaming floor).
            /// </summary>
            private void ResolveExperimentBests(
                out Dictionary<uint, KeyValuePair<int, double>> targets,
                out Dictionary<uint, KeyValuePair<int, double>> decoys)
            {
                if (_meanBestN >= 2)
                {
                    double floor = _floor.ComputeFloor();
                    targets = ReduceMeanBestN(_mb2Targets, floor, _meanBestN);
                    decoys = ReduceMeanBestN(_mb2Decoys, floor, _meanBestN);
                }
                else
                {
                    targets = _precTargets;
                    decoys = _precDecoys;
                }
            }

            /// <summary>
            /// Experiment-peptide <c>peptide -&gt; q</c>: materialize the best-per-peptide set
            /// sorted by ordinal (matching <see cref="PercolatorSampling.BestPrecursorPerPeptide"/>'s sort), compete
            /// by base_id, conservative-q, keyed by the winner's peptide -- byte-identical to
            /// <see cref="PercolatorQValues.ComputeExperimentPeptideQMap"/>.
            /// </summary>
            public Dictionary<string, double> BuildExperimentPeptideQMap()
            {
                if (_meanBestN >= 2)
                    return BuildMeanBestNPeptideQMap();
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
                PercolatorQValues.ComputeConservativeQvalues(ws, wd, q);
                var map = new Dictionary<string, double>(wi.Length);
                for (int rank = 0; rank < wi.Length; rank++)
                    map[best[wi[rank]].Peptide] = q[rank];
                return map;
            }

            /// <summary>
            /// PEP <c>winner-ordinal -&gt; pep</c>: compete the global base_id bests, fit the PEP
            /// estimator on winners sorted base_id-ascending (the non-associative KDE sum is
            /// order-sensitive), then posterior-error each winner -- byte-identical to
            /// <see cref="PercolatorQValues.ComputePepWinnerMap"/>.
            /// </summary>
            public Dictionary<int, double> BuildPepWinnerMap()
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
                var map = new Dictionary<int, double>(nWinners);
                for (int k = 0; k < nWinners; k++)
                    map[wi[k]] = pepEstimator.PosteriorError(ws[k]);
                return map;
            }

            /// <summary>The missing-run floor this builder would apply, or 0 outside mean(best-N)
            /// mode. Exposed so the parity test can hold the bounded histogram estimator directly
            /// against the resident sorted-quantile one
            /// (<see cref="TargetDecoyCompetition.ComputeFloorFromDecoyScores"/>): they are
            /// deliberately different estimators of the same statistic, agreeing only to about the
            /// histogram bin width, and that bound is not observable through the q-values (q comes
            /// from the competition RANKING, which a sub-bin shift normally leaves untouched).
            /// </summary>
            internal double ComputeDecoyFloor()
            {
                return _floor?.ComputeFloor() ?? 0.0;
            }

            /// <summary>The bin width of the streaming floor histogram - the bound within which
            /// <see cref="ComputeDecoyFloor"/> is expected to track the resident estimator, and the
            /// documented magnitude of this approximation.</summary>
            internal static double FloorBinWidth
            {
                get { return StreamingDecoyFloor.BIN_WIDTH; }
            }

            /// <summary>Reduce a per-base_id top-N accumulator dict to the <c>base_id -&gt;
            /// (min-ordinal, mean(best-N) score)</c> best map the target/decoy competition consumes.
            /// The stored ordinal is each base_id's earliest row (MinG), matching the resident
            /// <see cref="TargetDecoyCompetition.CompeteFromIndices"/> first-seen-max index over the
            /// per-row aggregate array (all rows of a base_id share the aggregate).</summary>
            private static Dictionary<uint, KeyValuePair<int, double>> ReduceMeanBestN(
                Dictionary<uint, Mb2Entry> src, double floor, int n)
            {
                var dict = new Dictionary<uint, KeyValuePair<int, double>>(src.Count);
                foreach (var kv in src)
                    dict[kv.Key] = new KeyValuePair<int, double>(
                        kv.Value.MinG, kv.Value.Acc.AggregateScore(floor, n));
                return dict;
            }

            /// <summary>
            /// Experiment-peptide <c>peptide -&gt; q</c> for mean(best-N): the peptide score is the
            /// MAX over its base_ids of each base_id's mean(best-N) precursor score, then the
            /// per-peptide winners compete by base_id - byte-identical (modulo the streaming floor)
            /// to the resident <see cref="PercolatorQValues.ComputeExperimentPeptideQMap"/> with
            /// OSPREY_EXPERIMENT_AGG=mean-best-N, whose
            /// <see cref="PercolatorSampling.BestPrecursorPerPeptide"/> over the per-row aggregate
            /// array reduces to exactly this per-peptide max.
            /// </summary>
            private Dictionary<string, double> BuildMeanBestNPeptideQMap()
            {
                double floor = _floor.ComputeFloor();
                var repByPeptide = new Dictionary<string, PeptideBest>();
                AccumulatePeptideReps(repByPeptide, _mb2Targets, floor, false, _meanBestN);
                AccumulatePeptideReps(repByPeptide, _mb2Decoys, floor, true, _meanBestN);

                var best = new List<PeptideBest>(repByPeptide.Values);
                best.Sort((a, b) => a.G.CompareTo(b.G)); // Array.Sort OK: G is each winning base_id's unique min ordinal, so the comparator never ties -- reproduces BestPrecursorPerPeptide's ascending-global-index sort
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
                PercolatorQValues.ComputeConservativeQvalues(ws, wd, q);
                var map = new Dictionary<string, double>(wi.Length);
                for (int rank = 0; rank < wi.Length; rank++)
                    map[best[wi[rank]].Peptide] = q[rank];
                return map;
            }

            /// <summary>Fold one side's per-base_id mean(best-2) scores into per-peptide winners:
            /// each peptide keeps the base_id with the strictly greatest aggregate, ties broken by
            /// the earliest row (min ordinal) - reproducing the first-seen strict '&gt;' that
            /// <see cref="PercolatorSampling.BestPrecursorPerPeptide"/> applies over the
            /// ascending-ordinal per-row aggregate array. The stored ordinal (<c>G = MinG</c>) is the
            /// winning base_id's earliest row, which is the global index
            /// <see cref="PercolatorSampling.BestPrecursorPerPeptide"/> returns.</summary>
            private static void AccumulatePeptideReps(
                Dictionary<string, PeptideBest> repByPeptide,
                Dictionary<uint, Mb2Entry> src, double floor, bool isDecoy, int n)
            {
                foreach (var kv in src)
                {
                    uint baseId = kv.Key;
                    Mb2Entry e = kv.Value;
                    double agg = e.Acc.AggregateScore(floor, n);
                    PeptideBest cur;
                    if (repByPeptide.TryGetValue(e.Peptide, out cur) &&
                        !(agg > cur.Score || (agg == cur.Score && e.MinG < cur.G)))
                    {
                        continue;
                    }
                    repByPeptide[e.Peptide] = new PeptideBest(e.MinG, agg, isDecoy, baseId, e.Peptide);
                }
            }

            /// <summary>Per-base_id top-2 accumulator plus the metadata the peptide roll-up needs:
            /// the earliest row ordinal (<see cref="MinG"/>) and the peptide string (constant for a
            /// (base_id, side)). Value type; callers copy back after mutating <see cref="Acc"/>.</summary>
            private struct Mb2Entry
            {
                internal TargetDecoyCompetition.MeanBestNAcc Acc;
                internal readonly int MinG;
                internal readonly string Peptide;

                internal Mb2Entry(TargetDecoyCompetition.MeanBestNAcc acc, int minG, string peptide)
                {
                    Acc = acc;
                    MinG = minG;
                    Peptide = peptide;
                }
            }

            /// <summary>Bounded (O(bins)) streaming estimator of the missing-run decoy floor,
            /// mirroring <c>TargetDecoyCompetition.ComputeFloorFromDecoyScores</c> without holding
            /// every decoy score. The mean (OSPREY_MEANBEST2_FLOOR_MEAN) is an exact running sum; the
            /// median (default) and low-percentile (OSPREY_MEANBEST2_FLOOR_PCT) use a fixed-width
            /// histogram over a wide fixed range with out-of-range counts tracked, so a central
            /// quantile stays accurate to the bin width (0.001). The floor is one global scalar
            /// applied to single-run precursors only, so this quantile approximation is negligible;
            /// the resident==streaming N=20 cross-check bounds it.</summary>
            private sealed class StreamingDecoyFloor
            {
                private const double RANGE_MIN = -100.0;
                private const double RANGE_MAX = 100.0;
                private const int BIN_COUNT = 200000; // Bin width 0.001 over [-100, 100].
                internal const double BIN_WIDTH = (RANGE_MAX - RANGE_MIN) / BIN_COUNT;

                private readonly long[] _bins = new long[BIN_COUNT];
                private long _underflow;
                private long _count;
                private double _sum;
                // Smallest / largest ADMITTED score. The resident twin's PercentileOfSorted
                // answers every out-of-histogram case with sorted[0] or sorted[Count-1] - always
                // a real observed score. Keeping the two extremes lets this estimator return the
                // same kind of value instead of a range constant, which is what makes the two
                // paths agree in the tails (see PercentileFromHistogram).
                private double _min = double.MaxValue;
                private double _max = double.MinValue;

                public void Add(double score)
                {
                    // Reject non-finite BEFORE _count/_sum, matching the resident floor sample.
                    // NaN: every NaN comparison below is false, so it would reach the cast and
                    // produce int.MinValue on net472 (throwing on _bins[-2147483648] mid-Stage 5)
                    // or 0 on net8.0 (silently counting it as a score of RANGE_MIN). Infinity:
                    // the range checks below would route it to the overflow bucket correctly, but
                    // it would already have entered _sum - and the MEAN branch of ComputeFloor
                    // returns _sum / _count, so a single infinite decoy score makes the floor
                    // infinite. AggregateScore's (n - _len) * floor is then 0 * Infinity == NaN for
                    // even a FULLY detected group, poisoning every base_id through the shared
                    // floor. Counting it and excluding it from the histogram would also make the
                    // quantile rank disagree with the bins.
                    if (!TargetDecoyCompetition.MeanBestNAcc.IsUsable(score))
                        return;
                    _count++;
                    _sum += score;
                    if (score < _min)
                        _min = score;
                    if (score > _max)
                        _max = score;
                    if (score < RANGE_MIN)
                    {
                        _underflow++;
                        return;
                    }
                    if (score >= RANGE_MAX)
                    {
                        // Counted (in _count and _max above), not binned. It MUST still be counted,
                        // because the quantile rank is taken over _count: dropping it silently
                        // shifted every quantile toward the low end. A quantile that lands in this
                        // region makes the bin walk fall off the end, which PercentileFromHistogram
                        // answers with _max - a real observed score.
                        return;
                    }
                    int bin = (int)((score - RANGE_MIN) / BIN_WIDTH);
                    if (bin < 0)
                        bin = 0;
                    if (bin >= BIN_COUNT)
                        bin = BIN_COUNT - 1;
                    _bins[bin]++;
                }

                public double ComputeFloor()
                {
                    if (_count == 0)
                        return 0.0;
                    if (OspreyEnvironment.MeanBest2FloorMean)
                        return _sum / _count;
                    double pct = OspreyEnvironment.MeanBest2FloorPercentile ?? 50.0;
                    return PercentileFromHistogram(pct);
                }

                // Linear-interpolated quantile at rank = pct/100 * (count - 1), matching
                // PercentileOfSorted's rank convention but computed from bin cumulative counts
                // (uniform interpolation within the straddling bin).
                //
                // Every degenerate case MIRRORS the resident TargetDecoyCompetition.
                // PercentileOfSorted rather than diverging from it, because the two estimators are
                // supposed to be the same statistic computed two ways. Two rules follow, and both
                // were previously broken:
                //
                //  * The answer is always a real OBSERVED score, never a range constant. The
                //    earlier pct >= 100 arm returned RANGE_MAX (+100), a floor ABOVE every real
                //    score, which inverts the feature: under-detected units would be PROMOTED
                //    instead of demoted. _max cannot do that, and it is exactly what
                //    PercentileOfSorted's sorted[Count-1] returns.
                //  * Nothing here throws. The resident twin clamps or short-circuits in each of
                //    these cases (its Count == 1 early return, its two boundary returns), so a
                //    throw would abort a multi-hour run at the very end of Stage 5 on input the
                //    other path handles - and would still leave the two paths disagreeing.
                //
                // The percentile RANGE itself is validated once at startup
                // (OspreyEnvironment.ValidateExperimentAggSettings), which is where an operator
                // error belongs; by the time execution reaches here pct is known to be in [0,100].
                // Tail answers are coarser than the resident interpolation between two neighbours,
                // which is the same deliberate bin-width approximation this estimator already
                // documents - bounded by the observed range, and never sign-inverting.
                private double PercentileFromHistogram(double pct)
                {
                    // Count == 1 short-circuits FIRST, mirroring PercentileOfSorted: with one
                    // observation every percentile is that observation (and rank below is 0).
                    if (_count == 1 || pct <= 0.0)
                        return _min;
                    if (pct >= 100.0)
                        return _max;
                    double rank = pct / 100.0 * (_count - 1);
                    double cum = _underflow;
                    // The quantile lies among the scores below RANGE_MIN, which the histogram
                    // counted but did not bin. _min is the smallest of them - the same end of the
                    // distribution PercentileOfSorted would return from its sorted[0] side.
                    if (rank < cum)
                        return _min;
                    for (int b = 0; b < BIN_COUNT; b++)
                    {
                        long c = _bins[b];
                        if (c == 0)
                            continue;
                        if (rank < cum + c)
                        {
                            double within = (rank - cum) / c;
                            return RANGE_MIN + (b + within) * BIN_WIDTH;
                        }
                        cum += c;
                    }
                    // Falling off the end means the quantile lies in the overflow region (scores
                    // at or above RANGE_MAX, counted but unbinned). _max is the largest observed
                    // decoy score, so the floor stays inside the data.
                    return _max;
                }
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
