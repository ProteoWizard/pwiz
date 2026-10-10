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
using System.Linq;
using System.Threading;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The PER-FILE half of the second-pass competition, run by the rescore worker instead of by
    /// Stage 7 (issue #4486).
    ///
    /// <para>One file's run-level competition is computable from that file alone: its own
    /// 1st-pass sidecar, its own reconciled parquet, and three whole-run constants that already
    /// ride the per-file <c>.1st-pass.model.json</c> relay. Stage 7 was doing it only because
    /// that is where the code happened to live, and paying for it by holding every file's
    /// survivors resident and re-opening 52.3 GB of 1st-pass sidecars at 257 files.</para>
    ///
    /// <para><b>What stays in Stage 7:</b> the JOIN. This worker emits NO precomputed
    /// per-base_id bests and no aggregation. SecondPassFDR folds the bests out of the per-run
    /// sidecars via <see cref="Pass2FdrSidecar.FileCompetitionFromRecords"/>. Emitting bests here
    /// would be a partial join in the wrong stage and would freeze the aggregation method into
    /// Stage 6, making mean-best-N (#4484) a worker change instead of a Stage 7 change.</para>
    ///
    /// <para><b>Absence is a stop, never a default.</b> Phase 1 produced seven defects and six
    /// were one of two shapes: a missing input becoming a plausible value, or the right values
    /// reaching the wrong SET of entries. This moves a computation across a process boundary,
    /// which is the same hazard larger - every value that used to arrive implicitly, in the
    /// enclosing scope, is now something the worker must obtain explicitly. So every input it
    /// cannot obtain fails the run rather than being defaulted.</para>
    ///
    /// <para><b>Every pass-2 mode has a per-file half, and this is where each one runs</b>
    /// (#4665). protein-compact competes the file over its stratum; <c>transfer</c> re-maps the
    /// file's run q-values through its own 1st-pass score-to-q table
    /// (<see cref="Pass2FdrSidecar.TransferOneFile"/>). Transfer does LESS per-file work than the
    /// competition and used to be the one mode that held every run's survivors resident in
    /// Stage 7 - not for any reason in its workload, but because its per-file half lived there.
    /// With both here, PerFileRescoring writes every run's <c>.2nd-pass.fdr_scores.bin</c> and
    /// SecondPassFDR writes no per-run file.</para>
    /// </summary>
    internal sealed class Pass2PerFileWorker : IDisposable
    {
        private readonly FrozenModelScorer _scorer;
        private readonly int _nFeatures;
        private readonly HashSet<uint> _stratumBaseIds;
        private readonly Action<string> _logWarning;

        /// <summary>
        /// True under <c>OSPREY_PASS2_QVALUE=transfer</c>: re-map run q through the file's own
        /// 1st-pass table instead of competing. There is no competition, so no decoy side is
        /// written either.
        /// </summary>
        private readonly bool _transfer;

        /// <summary>The analysis-wide 1st-pass experiment records the transfer carries forward.</summary>
        private readonly IReadOnlyDictionary<uint, FdrExperimentRecord> _pass1Experiment;

        /// <summary>The transfer's run-wide counts, summed across the parallel file loop.</summary>
        private Pass2FdrSidecar.TransferTally _transferTally;

        private readonly object _transferTallyLock = new object();

        /// <summary>
        /// One seeder PER WORKER THREAD, not per file and not one shared.
        ///
        /// <para>Per file is the documented anti-pattern: the seeder's index and staging buffer
        /// are both far past the Large Object Heap threshold at cohort scale (~533 K records per
        /// file on CHS), the LOH is swept only on a gen2 collection, and a fresh pair per file
        /// left ~125 MB of dead buffers standing each time - +24 GB over 257 files, which WAS
        /// that run's global memory peak. Shared is simply wrong: <c>Seed</c>/<c>Apply</c> mutate
        /// the index and the staging list, and the rescore loop is a <c>Parallel.For</c> over
        /// files. Thread-local gives back the buffer reuse the single instance had, bounded by
        /// the loop's degree of parallelism rather than by file count.</para>
        /// </summary>
        private readonly ThreadLocal<Pass2FdrSidecar.Pass1ScalarSeeder> _seeders;

        /// <summary>Every seeder handed out, so the run-wide summary can aggregate them.</summary>
        private readonly List<Pass2FdrSidecar.Pass1ScalarSeeder> _allSeeders =
            new List<Pass2FdrSidecar.Pass1ScalarSeeder>();

        private readonly object _seederListLock = new object();

        /// <summary>
        /// Persists one file's pass-2 answer: the pool-image records AND the decoy side of the
        /// competition that produced them. Supplied rather than constructed here so the worker
        /// owns the COMPUTATION and not the artifact policy - which task name the validity
        /// sidecar carries, and whether a --task ModelDiagnostics run declines the write at all,
        /// are the caller's to decide and are already implemented once.
        ///
        /// <para>ONE callback for both files, not two. They are two halves of a single answer and
        /// a node that received one without the other would either fold a competition with an
        /// empty null or reject the run; making them separate calls would make "wrote one, not
        /// the other" a reachable state.</para>
        /// </summary>
        private readonly Action<string, IReadOnlyList<FdrScoreRecord>,
            IReadOnlyDictionary<uint, (double score, uint entryId)>> _writeAnswer;

        /// <param name="scorer">The frozen 1st-pass model.</param>
        /// <param name="transfer">True for <c>OSPREY_PASS2_QVALUE=transfer</c>, false for the
        /// protein-compact competition.</param>
        /// <param name="stratumBaseIds">The protein stratum the competition is constrained to;
        /// unused by the transfer.</param>
        /// <param name="pass1Experiment">The analysis-wide 1st-pass experiment records.</param>
        /// <param name="writeAnswer">Persists one file's records, and its competition decoys
        /// when there was a competition (null under transfer).</param>
        /// <param name="logWarning">Where a per-file warning goes.</param>
        public Pass2PerFileWorker(
            FrozenModelScorer scorer, bool transfer, HashSet<uint> stratumBaseIds,
            IReadOnlyDictionary<uint, FdrExperimentRecord> pass1Experiment,
            Action<string, IReadOnlyList<FdrScoreRecord>,
                IReadOnlyDictionary<uint, (double score, uint entryId)>> writeAnswer,
            Action<string> logWarning)
        {
            _writeAnswer = writeAnswer ?? throw new ArgumentNullException(nameof(writeAnswer));
            _scorer = scorer ?? throw new ArgumentNullException(nameof(scorer));
            _nFeatures = scorer.NumFeatures;
            _transfer = transfer;
            _stratumBaseIds = stratumBaseIds;
            _pass1Experiment = pass1Experiment ?? throw new ArgumentNullException(nameof(pass1Experiment));
            _logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));
            _seeders = new ThreadLocal<Pass2FdrSidecar.Pass1ScalarSeeder>(() =>
            {
                var seeder = new Pass2FdrSidecar.Pass1ScalarSeeder(0, pass1Experiment);
                lock (_seederListLock)
                    _allSeeders.Add(seeder);
                return seeder;
            });
        }

        /// <summary>
        /// Compute one file's second-pass answer, stamp its survivors with it, and write the
        /// file's <c>.2nd-pass.fdr_scores.bin</c> - competing it under protein-compact,
        /// transferring its run q-values under <c>transfer</c>.
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
            if (_transfer)
            {
                var transferred = TransferAndStamp(
                    fileName, pass1SidecarPath, effectiveParquetPath, survivors);
                // No decoy side: nothing competed, so there is no null to carry to the join.
                _writeAnswer(fileName, transferred.Records, null);
                return transferred;
            }
            var result = CompeteAndStamp(
                fileName, pass1SidecarPath, effectiveParquetPath, survivors);
            // The competition's BestDecoy is serialized as the worker COMPUTED it, never
            // reassembled from the records, the stratum or the 1st-pass population - see
            // Pass2CompetitionDecoys. This is the whole of the transmission the join needs and
            // the pool image cannot carry.
            _writeAnswer(fileName, result.Records, result.Competition.BestDecoy);
            return result;
        }

        private Pass2FileResult CompeteAndStamp(
            string fileName, string pass1SidecarPath, string effectiveParquetPath,
            List<FdrEntry> survivors)
        {
            // Per-call scratch rather than per-worker: these are the two buffers
            // ReadOneFilePass2Inputs refills, and the parallel file loop means a worker-level
            // pair would be shared across concurrent files. They are O(one file's survivors),
            // so the allocation is bounded by the loop's parallelism, not by file count.
            var survivorIds = new HashSet<uint>();
            var pass1Records = new List<FdrScoreRecord>();

            Pass2FdrSidecar.ReadOneFilePass2Inputs(
                pass1SidecarPath, effectiveParquetPath, survivors,
                _scorer, _nFeatures, _seeders.Value, _logWarning,
                survivorIds, pass1Records,
                out uint[] entryIds, out double[] scores, out var survivorScores);

            // The whole point of step 1 (commit 3593cd2ff6): the filter takes THIS FILE's own
            // survivor set, because that is all a per-file worker can have. Measured equivalent
            // to the global union over 8.2 M observations and enforced with a throw in Stage 7
            // while both sets were still in one place - which is the only reason this call is
            // safe to make from here at all.
            var competition = StreamingFdr.CompeteOneFile(
                entryIds, scores, survivorScores, survivorIds, _stratumBaseIds, fileName);

            // FREE VERIFICATION of the artifact that has no other one. Every decoy best must be
            // an observation of THIS file at the score it competed on - which is exactly what
            // entryIds/scores hold, already in memory, so this costs one pass over a map that is
            // O(distinct base_id) and no I/O at all.
            //
            // It is deliberately NOT the gated Stage 7 recompute: that re-reads the 1st-pass
            // sidecar to answer the same question, and both sides of it call this very function
            // so it cannot see a defect inside it. This catches the shape that a recompute
            // cannot - a serialization or aliasing fault between competing and writing.
            AssertDecoyBestsAreLocalObservations(fileName, competition.BestDecoy, entryIds, scores);

            // Stamp the run q onto the entries the sidecar write will serialize. An entry absent
            // from the map won no competition in this file and takes 1.0 - the same default the
            // streamed form filled in centrally, and the value the join relies on being harmless
            // in a minimum (see FileCompetitionFromRecords).
            foreach (var e in survivors)
            {
                double rq = competition.RunQ.TryGetValue(e.EntryId, out double v) ? v : 1.0;
                e.RunPrecursorQvalue = rq;
                // Precursor-level path: keep peptide q in step with precursor q for the reported
                // set (peptide-level FDR is not the target here).
                e.RunPeptideQvalue = rq;
            }
            return new Pass2FileResult(
                competition,
                BuildRecords(survivors, fileName, effectiveParquetPath));
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
        /// array is put back afterwards: lean stubs get their null back, while a resident pool
        /// that loaded features on purpose (<c>OSPREY_FDR_PROJECTION=0</c>) keeps its own.</para>
        /// </summary>
        private Pass2FileResult TransferAndStamp(
            string fileName, string pass1SidecarPath, string effectiveParquetPath,
            List<FdrEntry> survivors)
        {
            var featByScoreIndex = Pass2FdrSidecar.LoadReconciledFeaturesByScoreIndex(effectiveParquetPath);
            var ownFeatures = new double[survivors.Count][];
            for (int i = 0; i < survivors.Count; i++)
                ownFeatures[i] = survivors[i].Features;
            int nMapped = Pass2FdrSidecar.MapFeaturesByScoreIndex(survivors, featByScoreIndex);
            var tally = new Pass2FdrSidecar.TransferTally();
            try
            {
                if (nMapped != survivors.Count)
                {
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass transfer for '{0}': {1} of {2} precursor candidates have no " +
                        @"features in {3}, which was written from these same entries. See issue #4665.",
                        fileName, survivors.Count - nMapped, survivors.Count, effectiveParquetPath));
                }
                Pass2FdrSidecar.TransferOneFile(fileName, pass1SidecarPath, survivors, _scorer,
                    _pass1Experiment, _logWarning, ref tally);
            }
            finally
            {
                for (int i = 0; i < survivors.Count; i++)
                    survivors[i].Features = ownFeatures[i];
            }
            // TransferOneFile declines a file whose 1st-pass sidecar it cannot read, leaving its
            // survivors unadjusted. In Stage 7 that was a warning; here it is a file that would
            // be written with run q-values nobody computed.
            if (tally.FilesDone != 1)
            {
                throw new InvalidOperationException(string.Format(
                    @"Second-pass transfer for '{0}': the first-pass intermediate file {1} could not " +
                    @"be read, so this run's q-values cannot be transferred. See issue #4665.",
                    fileName, pass1SidecarPath));
            }
            lock (_transferTallyLock)
            {
                _transferTally.FilesDone += tally.FilesDone;
                _transferTally.Unchanged += tally.Unchanged;
                _transferTally.Moved += tally.Moved;
                _transferTally.GapFill += tally.GapFill;
            }
            return new Pass2FileResult(null, BuildRecords(survivors, fileName, effectiveParquetPath));
        }

        /// <summary>
        /// Every entry in the competition's decoy side must be an observation THIS file holds, at
        /// the score it competed on.
        ///
        /// <para>The decoys artifact is the one thing the join cannot re-derive - that is why it
        /// is written - so it is also the one thing a downstream consumer must take on trust. This
        /// makes the trust cheap: <paramref name="entryIds"/> and <paramref name="scores"/> are
        /// the arrays the competition just reduced, still in memory, so the check is O(distinct
        /// base_id) with no I/O. A wrong POPULATION is not what this catches (only the mode knows
        /// which base_ids should be admitted); it catches a wrong VALUE or a wrong entry_id, which
        /// is the failure a serialization step can introduce and a recompute of the same function
        /// cannot see.</para>
        /// </summary>
        private static void AssertDecoyBestsAreLocalObservations(
            string fileName, IReadOnlyDictionary<uint, (double score, uint entryId)> bestDecoy,
            uint[] entryIds, double[] scores)
        {
            if (bestDecoy.Count == 0)
                return;
            // One pass to index this file's observations, then one pass over the bests. Built
            // here rather than kept by the competition because it is scratch: it dies with this
            // call, where a field would live for the length of the run times the thread count.
            var scoreByEntryId = new Dictionary<uint, double>(entryIds.Length);
            for (int i = 0; i < entryIds.Length; i++)
                scoreByEntryId[entryIds[i]] = scores[i];
            foreach (var kv in bestDecoy)
            {
                if (!scoreByEntryId.TryGetValue(kv.Value.entryId, out double competed))
                {
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass competition decoys for '{0}': base_id {1} names entry_id " +
                        @"{2}, which is not an observation of this file. See issue #4486.",
                        fileName, kv.Key, kv.Value.entryId));
                }
                if (!competed.Equals(kv.Value.score))
                {
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass competition decoys for '{0}': base_id {1} records score {2} " +
                        @"for entry_id {3}, which competed at {4}. See issue #4486.",
                        fileName, kv.Key, kv.Value.score, kv.Value.entryId, competed));
                }
            }
        }

        /// <summary>
        /// Serialize this file's pass-2 answer: ONE RECORD PER POOL ENTRY, in pool order.
        ///
        /// <para><b>This artifact's population is not this writer's to choose.</b> The per-run
        /// 2nd-pass sidecar describes the file's Stage 6 pool, and that pool is already defined
        /// for every node by the join: <c>FirstPassFdrTask</c> writes each file's
        /// <c>.reconciliation.json</c> from the node that traversed the whole population, and
        /// <c>ReconciledParquetWriter</c> stamps the resulting parquet with the JOIN-wide
        /// reconciliation hash and <c>osprey.reconciled=survivors</c>. Stage 6 abides by that
        /// envelope, which is what makes any number of nodes emit the same rows. Writing this
        /// file from a set assembled some other way opts out of a contract the rest of the
        /// pipeline is already keyed to.</para>
        ///
        /// <para><b>Measured consequence of getting it wrong (issue #4486).</b> An earlier form
        /// of this method iterated the 1st-pass sidecar's entry ids and emitted a record only for
        /// per-file survivors, plus non-survivor decoys. A GAP-FILLED peak has no 1st-pass record
        /// in its file by definition, so it was unreachable: 594 gap-fill observations across 3
        /// Stellar files (208 / 185 / 201, exactly the <c>gap_fill_targets</c> the envelope
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
        /// <para>The join (<see cref="Pass2FdrSidecar.FileCompetitionFromRecords"/>) resolves a
        /// per-base_id maximum with strict greater-than and takes the FIRST observation at the
        /// maximum, so the order it reduces over has to be the one <c>CompeteOneFile</c> reduced
        /// over; both are subsequences of the same canonical order, which is what keeps them
        /// agreeing on ties.</para>
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
        /// Report what this worker did, ONCE and DETERMINISTICALLY, after the file loop - what
        /// the transfer moved, or what the competition's seeders restored.
        ///
        /// <para>Summed across the thread-local seeders rather than logged per instance, because
        /// which seeder took which file is decided by which thread happened to take it. Logging
        /// per instance would emit the same facts with a run-to-run varying split - identical
        /// inputs producing differing output, which is the invariant this project holds for
        /// testing and for scientific review. The COMPUTED values were never at risk (each entry
        /// is seeded from its own file's record, looked up by entry_id), but "the run is
        /// deterministic" has to include what it says about itself.</para>
        /// </summary>
        public void LogSummary(PipelineContext ctx)
        {
            if (_transfer)
            {
                // The transfer seeds nothing - every survivor takes its values from the transfer
                // itself - so its summary is what the transfer did, not what a seeder restored.
                Pass2FdrSidecar.TransferTally tally;
                lock (_transferTallyLock)
                    tally = _transferTally;
                ctx.LogInfo(string.Format(
                    @"OSPREY_PASS2_QVALUE=transfer over {0:N0} files: {1:N0} peaks keep their first-pass " +
                    @"q-values, {2:N0} peaks moved by cross-run reconciliation and {3:N0} missing peaks get " +
                    @"a new run-level q-value.",
                    tally.FilesDone, tally.Unchanged, tally.Moved, tally.GapFill));
                return;
            }
            int restored = 0;
            int filesRead = 0;
            lock (_seederListLock)
            {
                foreach (var seeder in _allSeeders)
                {
                    restored += seeder.Restored;
                    filesRead += seeder.FilesRead;
                }
            }
            Pass2FdrSidecar.Pass1ScalarSeeder.LogSeedSummary(ctx, restored, filesRead);
        }

        public void Dispose()
        {
            _seeders.Dispose();
        }
    }

    /// <summary>
    /// One file's pass-2 answer: the competition it just ran, and the records that answer is
    /// written down as.
    ///
    /// <para>Both, rather than just the records, because the two have different lifetimes during
    /// the transition. The RECORDS are the durable artifact the join reads. The COMPETITION is
    /// what Stage 7 folds today, in process - so keeping it lets the sidecar-fold be introduced
    /// and proven equal against the in-process value before anything depends on it alone.</para>
    /// </summary>
    internal sealed class Pass2FileResult
    {
        public Pass2FileResult(
            StreamingFdr.FileCompetition competition, List<FdrScoreRecord> records)
        {
            Competition = competition;
            Records = records;
        }

        public StreamingFdr.FileCompetition Competition { get; }
        public List<FdrScoreRecord> Records { get; }
    }
}
