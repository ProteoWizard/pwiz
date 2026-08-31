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
    /// </summary>
    internal sealed class Pass2PerFileWorker : IDisposable
    {
        private readonly FrozenModelScorer _scorer;
        private readonly int _nFeatures;
        private readonly string _mode;
        private readonly HashSet<uint> _stratumBaseIds;
        private readonly Action<string> _logWarning;

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
        /// Persists one file's records. Supplied rather than constructed here so the worker owns
        /// the COMPUTATION and not the artifact policy - which task name the validity sidecar
        /// carries, and whether a --task ModelDiagnostics run declines the write at all, are the
        /// caller's to decide and are already implemented once.
        /// </summary>
        private readonly Action<string, IReadOnlyList<FdrScoreRecord>> _writeSidecar;

        public Pass2PerFileWorker(
            FrozenModelScorer scorer, string mode, HashSet<uint> stratumBaseIds,
            IReadOnlyDictionary<uint, FdrExperimentRecord> pass1Experiment,
            Action<string, IReadOnlyList<FdrScoreRecord>> writeSidecar,
            Action<string> logWarning)
        {
            _writeSidecar = writeSidecar ?? throw new ArgumentNullException(nameof(writeSidecar));
            _scorer = scorer ?? throw new ArgumentNullException(nameof(scorer));
            _nFeatures = scorer.NumFeatures;
            _mode = mode;
            _stratumBaseIds = stratumBaseIds;
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
        /// Compete one file and stamp its survivors with the run q it just earned.
        ///
        /// <para>Returns the file's competition so the caller can write the sidecar from the same
        /// entries it stamped. The heavy per-entry payload is still live at the call site - the
        /// reconciled parquet has just been written and the release is gated behind it - which is
        /// exactly why the hook point is there and not later.</para>
        /// </summary>
        /// <param name="fileName">This file's stem, for diagnostics.</param>
        /// <param name="pass1SidecarPath">This file's <c>.1st-pass.fdr_scores.bin</c>.</param>
        /// <param name="effectiveParquetPath">Its reconciled parquet, or its Stage 4 parquet when
        /// reconciliation produced none.</param>
        /// <param name="survivors">This file's post-rescore survivors. Stamped in place.</param>
        public Pass2FileResult CompeteStampAndWrite(
            string fileName, string pass1SidecarPath, string effectiveParquetPath,
            List<FdrEntry> survivors)
        {
            var result = CompeteAndStamp(
                fileName, pass1SidecarPath, effectiveParquetPath, survivors);
            _writeSidecar(fileName, result.Records);
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
                _scorer, _nFeatures, _seeders.Value, _logWarning, _mode,
                survivorIds, pass1Records,
                out uint[] entryIds, out double[] scores, out var survivorScores);

            // The whole point of step 1 (commit 3593cd2ff6): the filter takes THIS FILE's own
            // survivor set, because that is all a per-file worker can have. Measured equivalent
            // to the global union over 8.2 M observations and enforced with a throw in Stage 7
            // while both sets were still in one place - which is the only reason this call is
            // safe to make from here at all.
            var competition = StreamingFdr.CompeteOneFile(
                entryIds, scores, survivorScores, survivorIds, _stratumBaseIds);

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
        /// <para><b>Pool order, not competition order.</b> The pool list is the order Stage 6
        /// wrote the reconciled parquet in - measured identical, ids and order, to the per-run
        /// sidecar this replaces - so emitting in list order reproduces it exactly. The join
        /// (<see cref="Pass2FdrSidecar.FileCompetitionFromRecords"/>) resolves a per-base_id
        /// maximum with strict greater-than and takes the FIRST observation at the maximum, so
        /// the order it reduces over has to be the one <c>CompeteOneFile</c> reduced over; both
        /// are subsequences of the same pool order, which is what keeps them agreeing on ties.
        /// </para>
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
            foreach (var e in survivors)
            {
                records.Add(new FdrScoreRecord(
                    e.EntryId, e.Score, e.RunPrecursorQvalue, e.RunPeptideQvalue));
            }
            // Checked HERE rather than at the write, so a pool that arrived short fails on the
            // node that built the records instead of somewhere downstream that can only see a
            // plausible smaller file.
            Pass2FdrSidecar.AssertSidecarDescribesPool(fileName, effectiveParquetPath, records.Count);
            return records;
        }

        /// <summary>
        /// Report what the seeders restored, ONCE and DETERMINISTICALLY, after the file loop.
        ///
        /// <para>Aggregated across the thread-local instances rather than logged per instance,
        /// and the file names sorted, because which seeder holds which file name is decided by
        /// which thread happened to take that file. Logging per instance would emit the same
        /// facts in a run-to-run varying order and with a varying split - identical inputs
        /// producing differing output, which is the invariant this project holds for testing and
        /// for scientific review. The COMPUTED values were never at risk (each entry is seeded
        /// from its own file's record, looked up by entry_id), but "the run is deterministic"
        /// has to include what it says about itself.</para>
        /// </summary>
        public void LogSummary(PipelineContext ctx)
        {
            var unreadable = new List<string>();
            int restored = 0;
            int filesRead = 0;
            lock (_seederListLock)
            {
                foreach (var seeder in _allSeeders)
                {
                    unreadable.AddRange(seeder.Unreadable);
                    restored += seeder.Restored;
                    filesRead += seeder.FilesRead;
                }
            }
            // Ordinal AND stable, so the text is byte-stable across runs and machines.
            // OrderBy rather than List.Sort: the latter is introsort and reorders ties, which is
            // the wrong tool to reach for in the one method whose entire purpose is to stop this
            // output varying between runs - even though equal file names make the tie moot here.
            unreadable = unreadable.OrderBy(s => s, StringComparer.Ordinal).ToList();
            if (unreadable.Count > 0)
            {
                ctx.LogWarning(string.Format(
                    "1st-pass Score/Pep/ExperimentAggregateScore could not be " +
                    "restored for {0} file(s) (no readable 1st-pass sidecar): [{1}]. Peaks Stage 6 " +
                    "changed in those files keep reset defaults, so their 2nd-pass sidecars are " +
                    "wrong AND a Score of 0 enters the second-pass protein FDR null unfiltered. " +
                    "Treat this run's protein-level numbers as unreliable.",
                    unreadable.Count, string.Join(", ", unreadable)));
            }
            ctx.LogVerbose(string.Format(
                "Restored 1st-pass Score/Pep/ExperimentAggregateScore onto {0} survivor(s) across {1} file(s).",
                restored, filesRead));
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
