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
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.Pipeline;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The PER-FILE half of the second pass, run by the rescore worker instead of by
    /// Stage 7 (issue #4486).
    ///
    /// <para>One file's run-level q-values are computable from that file alone: its own
    /// first-pass records and its own reconciled rows. Stage 7 was doing it only because
    /// that is where the code happened to live, and paying for it by holding every file's
    /// survivors resident and re-opening 52.3 GB of 1st-pass sidecars at 257 files.</para>
    ///
    /// <para><b>What stays in Stage 7:</b> the JOIN. This worker emits no aggregation across
    /// runs; SecondPassFDR folds the per-run records into the experiment scope.</para>
    ///
    /// <para><b>Absence is a stop, never a default.</b> Phase 1 produced seven defects and six
    /// were one of two shapes: a missing input becoming a plausible value, or the right values
    /// reaching the wrong SET of entries. This moves a computation across a process boundary,
    /// which is the same hazard larger - every value that used to arrive implicitly, in the
    /// enclosing scope, is now something the worker must obtain explicitly. So every input it
    /// cannot obtain fails the run rather than being defaulted.</para>
    ///
    /// <para><b>The per-file half is the transfer</b> (#4665): it re-maps the file's run
    /// q-values through its own 1st-pass score-to-q table
    /// (<see cref="Pass2FdrSidecar.TransferOneFile"/>). It used to hold every run's survivors
    /// resident in Stage 7 - not for any reason in its workload, but because its per-file half
    /// lived there. With it here, PerFileRescoring writes every run's second-pass records and
    /// SecondPassFDR writes no per-run file.</para>
    /// </summary>
    internal sealed class Pass2PerFileWorker
    {
        private readonly FrozenModelScorer _scorer;
        private readonly PinFeatureSelection _features;
        private readonly Action<string> _logWarning;

        /// <summary>
        /// The stamp of the first pass whose model this worker scores with, which each run's
        /// protein-best column must carry when the model scores with one; null when it does not.
        /// </summary>
        private readonly ArtifactStamp _firstPassStamp;

        /// <summary>The analysis-wide 1st-pass experiment records the transfer carries forward.</summary>
        private readonly IReadOnlyDictionary<uint, FdrExperimentRecord> _pass1Experiment;

        /// <summary>The transfer's run-wide counts, summed across the parallel file loop.</summary>
        private Pass2FdrSidecar.TransferTally _transferTally;

        private readonly object _transferTallyLock = new object();

        // The protein-best no-row tallies (a measurement), summed across the parallel file loop.
        private long _proteinBestSurvivors;
        private long _proteinBestNoRow;
        private long _proteinBestNoRowDecoys;
        private long _proteinBestNoRowWithSibling;
        private long _proteinBestNoRowWouldDiffer;

        /// <summary>
        /// Persists one file's pass-2 answer: the pool-image records. Supplied rather than
        /// constructed here so the worker owns the COMPUTATION and not the artifact policy -
        /// which task name the validity sidecar carries, and whether a --task ModelDiagnostics
        /// run declines the write at all, are the caller's to decide and are already implemented
        /// once.
        /// </summary>
        private readonly Action<string, IReadOnlyList<FdrScoreRecord>> _writeAnswer;

        /// <param name="scorer">The frozen 1st-pass model, or null when each run trained its own
        /// (per-run first-pass models): each run's reconciled parquet then carries every
        /// survivor's second-pass score, a moved row's from the run's own frozen model, and the
        /// transfer reads them from there.</param>
        /// <param name="features">The search's features, as the reconciled parquet holds them.</param>
        /// <param name="pass1Experiment">The analysis-wide 1st-pass experiment records.</param>
        /// <param name="writeAnswer">Persists one file's records.</param>
        /// <param name="logWarning">Where a per-file warning goes.</param>
        /// <param name="firstPassStamp">The frozen model's stamp, which every run's protein-best
        /// column must carry when the model scores with one (<c>--protein-boost best</c>).</param>
        public Pass2PerFileWorker(
            FrozenModelScorer scorer, PinFeatureSelection features,
            IReadOnlyDictionary<uint, FdrExperimentRecord> pass1Experiment,
            Action<string, IReadOnlyList<FdrScoreRecord>> writeAnswer,
            Action<string> logWarning, ArtifactStamp firstPassStamp = null)
        {
            _firstPassStamp = firstPassStamp;
            _writeAnswer = writeAnswer ?? throw new ArgumentNullException(nameof(writeAnswer));
            _scorer = scorer;
            _features = features ?? throw new ArgumentNullException(nameof(features));
            _pass1Experiment = pass1Experiment ?? throw new ArgumentNullException(nameof(pass1Experiment));
            _logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));
        }

        /// <summary>
        /// Compute one file's second-pass answer, stamp its survivors with it, and write the
        /// file's second-pass records, transferring its run q-values.
        ///
        /// <para>Returns the file's answer so the caller can write the sidecar from the same
        /// entries it stamped. The heavy per-entry payload is still live at the call site - the
        /// reconciled parquet has just been written and the release is gated behind it - which is
        /// exactly why the hook point is there and not later.</para>
        /// </summary>
        /// <param name="fileName">This file's stem, for diagnostics.</param>
        /// <param name="pass1SidecarPath">This file's <c>.1st-pass.fdr_scores.bin</c>.</param>
        /// <param name="effectiveParquetPath">Its reconciled parquet, or its Stage 4 parquet when
        /// reconciliation produced none.</param>
        /// <param name="survivors">This file's post-rescore survivors. Stamped in place.</param>
        public Pass2FileResult ComputeStampAndWrite(
            string fileName, string pass1SidecarPath, string effectiveParquetPath,
            List<FdrEntry> survivors)
        {
            var transferred = TransferAndStamp(
                fileName, pass1SidecarPath, effectiveParquetPath, survivors);
            _writeAnswer(fileName, transferred.Records);
            return transferred;
        }

        /// <summary>
        /// The transfer's per-file half: re-score every survivor with the frozen model on its
        /// RECONCILED features and map the score through this file's own 1st-pass score-to-q
        /// table (<see cref="Pass2FdrSidecar.TransferOneFile"/>, unchanged from when Stage 7 ran
        /// it over the whole pool).
        ///
        /// <para>The features come from the reconciled parquet just written, keyed by
        /// <c>score_index</c>, which is what Stage 7 used to reload them from - so the scores,
        /// and the UNCHANGED / MOVED / GAP-FILL classification that compares them bit for bit
        /// against the 1st-pass record, are the same. Every survivor must resolve: this file's
        /// parquet was written from these entries moments ago, so one that does not is a defect,
        /// not a stub mismatch to warn about and skip.</para>
        ///
        /// <para>The features are borrowed onto the entries for the transfer and each entry's own
        /// array is put back afterwards: lean stubs get their null back, and an entry that
        /// already carried features keeps its own.</para>
        /// </summary>
        private Pass2FileResult TransferAndStamp(
            string fileName, string pass1SidecarPath, string effectiveParquetPath,
            List<FdrEntry> survivors)
        {
            var featByScoreIndex = Pass2FdrSidecar.LoadReconciledFeaturesByScoreIndex(effectiveParquetPath, _features);
            var ownFeatures = new double[survivors.Count][];
            for (int i = 0; i < survivors.Count; i++)
                ownFeatures[i] = survivors[i].Features;
            int nMapped = Pass2FdrSidecar.MapFeaturesByScoreIndex(survivors, featByScoreIndex);
            var tally = new Pass2FdrSidecar.TransferTally();
            try
            {
                if (nMapped != survivors.Count)
                {
                    // Exception text for internal diagnostics, not displayed to user
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass transfer for '{0}': {1} of {2} precursor candidates have no " +
                        @"features in {3}, which was written from these same entries. See issue #4665.",
                        fileName, survivors.Count - nMapped, survivors.Count, effectiveParquetPath));
                }
                Pass2FdrSidecar.TransferOneFile(fileName, pass1SidecarPath, survivors, _scorer,
                    ProteinBestFor(fileName, pass1SidecarPath, survivors), _pass1Experiment, _logWarning, ref tally);
            }
            finally
            {
                for (int i = 0; i < survivors.Count; i++)
                    survivors[i].Features = ownFeatures[i];
            }
            return FinishTransfer(fileName, pass1SidecarPath, effectiveParquetPath, survivors, tally);
        }

        /// <summary>
        /// The transfer under per-run first-pass models, BEFORE
        /// the run's reconciled stub is written, which then carries its answer: each survivor's
        /// second-pass score is the one it holds in memory - its first-pass score from the stub
        /// for a row whose peak did not move, and the run's own frozen model's calibrated score
        /// for a moved, gap-filled or CWT row - and the transfer stamps its run q-values from the
        /// run's own first-pass table. No feature is read and no per-run sidecar is written.
        /// </summary>
        public void TransferInMemory(string fileName, string pass1SidecarPath, List<FdrEntry> survivors)
        {
            if (_scorer != null)
            {
                // Exception text for internal diagnostics, not displayed to user
                throw new InvalidOperationException(@"The in-memory transfer serves per-run first-pass models only.");
            }
            var tally = new Pass2FdrSidecar.TransferTally();
            Pass2FdrSidecar.TransferOneFileByScore(fileName, pass1SidecarPath, survivors,
                e => e.Score, _pass1Experiment, _logWarning, ref tally);
            AddTally(fileName, pass1SidecarPath, tally);
        }

        /// <summary>
        /// The end of the transfer: a run whose first-pass records could not be read stops, the
        /// run's counts join the run-wide tally, and the survivors become the run's records.
        /// </summary>
        private Pass2FileResult FinishTransfer(string fileName, string pass1SidecarPath, string effectiveParquetPath,
            List<FdrEntry> survivors, Pass2FdrSidecar.TransferTally tally)
        {
            AddTally(fileName, pass1SidecarPath, tally);
            return new Pass2FileResult(BuildRecords(survivors, fileName, effectiveParquetPath));
        }

        private void AddTally(string fileName, string pass1SidecarPath, Pass2FdrSidecar.TransferTally tally)
        {
            // TransferOneFile declines a file whose 1st-pass sidecar it cannot read, leaving its
            // survivors unadjusted. In Stage 7 that was a warning; here it is a file that would
            // be written with run q-values nobody computed.
            if (tally.FilesDone != 1)
            {
                throw new InvalidDataException(string.Format(
                    OspreyTasksResources.Pass2PerFileWorker_TransferAndStamp_The_first_pass_intermediate_file__0__could_not_be_read__so_the_run_level_q_values_of__1__cannot_be_carried_into_second_pass_FDR__Run__2__again_,
                    pass1SidecarPath, fileName, OspreyArgNames.TaskText(FirstPassFdrTask.TASK_NAME)));
            }
            lock (_transferTallyLock)
            {
                _transferTally.FilesDone += tally.FilesDone;
                _transferTally.Unchanged += tally.Unchanged;
                _transferTally.Moved += tally.Moved;
                _transferTally.GapFill += tally.GapFill;
            }
        }

        /// <summary>
        /// This run's protein-best column when the frozen model scores with one, else null. Its
        /// rescored rows - moved, gap-filled, CWT - read their entry's first-pass value: the
        /// column is what the entry's siblings scored, which a move of its own peak does not
        /// change.
        /// </summary>
        private ProteinBestRunValues ProteinBestFor(string fileName, string pass1SidecarPath, List<FdrEntry> survivors)
        {
            if (_scorer == null)
                return null;
            var proteinBest = Pass2FdrSidecar.LoadProteinBestForRun(_scorer, pass1SidecarPath, fileName, _firstPassStamp);
            if (proteinBest != null)
                CountNoRowFloor(proteinBest, survivors);
            return proteinBest;
        }

        /// <summary>
        /// A measurement only: how many of this run's survivors have no first-pass row in its
        /// protein-best column, and so read the run's floor, and how many of those have a sibling
        /// whose value the first pass recorded - the entry of the same base_id on the other side,
        /// or a target of the same modified sequence - which is the value the first pass would have
        /// given them. A lower bound on the entries a sibling-aware value would move: a peptide with
        /// no row of its own in the run also has protein siblings the column does not record.
        /// </summary>
        private void CountNoRowFloor(ProteinBestRunValues proteinBest, List<FdrEntry> survivors)
        {
            long floorBits = BitConverter.DoubleToInt64Bits(proteinBest.Floor);
            Dictionary<string, double> targetValueBySequence = null;
            long noRow = 0, noRowDecoys = 0, withSibling = 0, wouldDiffer = 0;
            foreach (var e in survivors)
            {
                if (proteinBest.TryGetValue(e.EntryId, out _))
                    continue;
                noRow++;
                if (e.IsDecoy)
                    noRowDecoys++;
                if (!proteinBest.TryGetValue(e.EntryId ^ LibraryEntry.DECOY_ID_BIT, out double sibling))
                {
                    if (e.IsDecoy || e.ModifiedSequence == null)
                        continue;
                    targetValueBySequence ??= TargetValuesBySequence(proteinBest, survivors);
                    if (!targetValueBySequence.TryGetValue(e.ModifiedSequence, out sibling))
                        continue;
                }
                withSibling++;
                if (BitConverter.DoubleToInt64Bits(sibling) != floorBits)
                    wouldDiffer++;
            }
            Interlocked.Add(ref _proteinBestSurvivors, survivors.Count);
            Interlocked.Add(ref _proteinBestNoRow, noRow);
            Interlocked.Add(ref _proteinBestNoRowDecoys, noRowDecoys);
            Interlocked.Add(ref _proteinBestNoRowWithSibling, withSibling);
            Interlocked.Add(ref _proteinBestNoRowWouldDiffer, wouldDiffer);
        }

        private static Dictionary<string, double> TargetValuesBySequence(ProteinBestRunValues proteinBest,
            List<FdrEntry> survivors)
        {
            var bySequence = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var e in survivors)
            {
                if (!e.IsDecoy && e.ModifiedSequence != null && proteinBest.TryGetValue(e.EntryId, out double value))
                    bySequence[e.ModifiedSequence] = value;
            }
            return bySequence;
        }

        /// <summary>
        /// Serialize this file's pass-2 answer: ONE RECORD PER POOL ENTRY, in pool order.
        ///
        /// <para><b>This artifact's population is not this writer's to choose.</b> The per-run
        /// 2nd-pass sidecar describes the file's Stage 6 pool, and that pool is already defined
        /// for every node by the join: <c>FirstPassFdrTask</c> writes the cross-run consensus
        /// from the node that traversed the whole population, each run's plan is a function of
        /// that consensus and the run alone, and <c>ReconciledParquetWriter</c> stamps the
        /// resulting parquet with the JOIN-wide reconciliation hash and
        /// <c>osprey.reconciled=survivors</c>. That is what makes any number of nodes emit the
        /// same rows. Writing this
        /// file from a set assembled some other way opts out of a contract the rest of the
        /// pipeline is already keyed to.</para>
        ///
        /// <para><b>Measured consequence of getting it wrong (issue #4486).</b> An earlier form
        /// of this method iterated the 1st-pass sidecar's entry ids and emitted a record only for
        /// per-file survivors, plus non-survivor decoys. A GAP-FILLED peak has no 1st-pass record
        /// in its file by definition, so it was unreachable: 594 gap-fill observations across 3
        /// Stellar files (208 / 185 / 201, exactly the <c>gap_fill_targets</c> the run's plan
        /// declares) lost their record. Those pool entries then never received experiment-scope
        /// values from the fold and kept <c>ResetScores</c>' defaults - experiment q 1.0 and
        /// aggregate 0.0 - for precursors whose analysis-wide q was as low as 6.6e-05, while the
        /// experiment sidecar still held the real values. That is strictly wrong, and it reached
        /// no gate: the blib, protein-q, resume, HPC-chain, warm-rerun and fragment-release legs
        /// all stayed green, and it surfaced only as two moved numbers in a diagnostics panel.
        /// <see cref="Pass2FdrSidecar.AssertSidecarDescribesPool"/> is the verifier that closes
        /// it.</para>
        ///
        /// <para><b>Pool ORDER, not the pool list's order.</b> The record sequence must be the
        /// reconciled parquet's row sequence, and the in-memory list is not it: gap-fill entries
        /// are APPENDED by the rescore task's phase 2, so emitting in list order puts them in a
        /// trailing block, while
        /// <see cref="ParquetScoreCache.StreamReconciledScoresParquet"/> merges each one into its
        /// canonical <c>(entry_id, charge, scan_number)</c> sorted position. Measured on Stellar:
        /// the branch's sequence was <c>(baseline minus gap-fills) + gap-fills ascending</c>
        /// against a baseline that is fully ascending.</para>
        ///
        /// <para>So the records are sorted on the parquet's OWN canonical key rather than on
        /// entry_id alone. Ordering by entry_id happens to reproduce it wherever a file holds one
        /// row per entry_id, which is the measured case here - but "happens to" is what this
        /// method already got wrong once, and the parquet writer hard-fails on a row that is out
        /// of canonical order, so the key it enforces is the one to reproduce. A STABLE sort
        /// (LINQ <c>OrderBy</c>, as the parquet's own gap-fill merge uses), so a full key tie
        /// leaves the original row ahead of the gap-fill - which is the disposition
        /// <c>KeyLess</c> gives it on the merge side.</para>
        ///
        /// <para><b>No decoy carry-forward.</b> Decoys that are pool entries are emitted like any
        /// other pool entry - the baseline artifact holds 466,055 decoy observations across these
        /// three files, 294,540 of them at run q 1.0, so the null was never short of them.
        /// Injecting non-pool decoys was measured inert: it moved none of the 313,537 shared
        /// experiment-wide values, and the baseline's experiment sidecar is exactly the union of
        /// what the per-run sidecars already contained (0 ids in either direction). If the fold
        /// ever does need something this file does not carry, that has to show up as a failure
        /// with a diagnosis, not as a pre-emptive redefinition of the artifact.</para>
        /// </summary>
        private static List<FdrScoreRecord> BuildRecords(
            List<FdrEntry> survivors, string fileName, string effectiveParquetPath)
        {
            var records = new List<FdrScoreRecord>(survivors.Count);
            foreach (var e in survivors.OrderBy(e => e.EntryId)
                         .ThenBy(e => e.Charge).ThenBy(e => e.ScanNumber))
            {
                records.Add(new FdrScoreRecord(
                    e.EntryId, e.Score, e.RunPrecursorQvalue, e.RunPeptideQvalue, e.ApexRt));
            }
            // Checked HERE rather than at the write, so a pool that arrived short fails on the
            // node that built the records instead of somewhere downstream that can only see a
            // plausible smaller file.
            Pass2FdrSidecar.AssertSidecarDescribesPool(fileName, effectiveParquetPath, records);
            return records;
        }

        /// <summary>
        /// Report what this worker did, ONCE and DETERMINISTICALLY, after the file loop: what
        /// the transfer moved, summed across the parallel file loop.
        /// </summary>
        public void LogSummary(PipelineContext ctx)
        {
            if (_scorer != null && _scorer.ScoresProteinBest)
            {
                ctx.LogInfo(LogTag.COUNT,
                    @"Second-pass protein-best: {0} of {1} survivors have no first-pass row and read the run's floor ({2} of them decoys); {3} of those have a recorded sibling value, {4} of which differ from the floor",
                    Interlocked.Read(ref _proteinBestNoRow), Interlocked.Read(ref _proteinBestSurvivors),
                    Interlocked.Read(ref _proteinBestNoRowDecoys), Interlocked.Read(ref _proteinBestNoRowWithSibling),
                    Interlocked.Read(ref _proteinBestNoRowWouldDiffer));
            }
            Pass2FdrSidecar.TransferTally tally;
            lock (_transferTallyLock)
                tally = _transferTally;
            ctx.LogInfo(string.Format(
                OspreyTasksResources.Pass2PerFileWorker_LogSummary_Second_pass_FDR_over__0__files_kept_the_first_pass_q_values_of__1__peaks__gave__2__peaks_moved_by_cross_run_reconciliation_and__3__missing_peaks_new_run_level_q_values_,
                tally.FilesDone, tally.Unchanged, tally.Moved, tally.GapFill));
        }
    }

    /// <summary>
    /// One file's pass-2 answer: the records that answer is written down as, which are the
    /// durable artifact the join reads.
    /// </summary>
    internal sealed class Pass2FileResult
    {
        public Pass2FileResult(List<FdrScoreRecord> records)
        {
            Records = records;
        }

        public List<FdrScoreRecord> Records { get; }
    }
}
