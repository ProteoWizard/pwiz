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
using pwiz.Osprey.Core;
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
    /// (<see cref="Pass2FdrSidecar.TransferOneFileByScore"/>). It used to hold every run's
    /// survivors resident in Stage 7 - not for any reason in its workload, but because its per-file half
    /// lived there. With it here, PerFileRescoring writes every run's second-pass records and
    /// SecondPassFDR writes no per-run file.</para>
    /// </summary>
    internal sealed class Pass2PerFileWorker
    {
        private readonly Action<string> _logWarning;

        /// <summary>The analysis-wide 1st-pass experiment records the transfer carries forward.</summary>
        private readonly IReadOnlyDictionary<uint, FdrExperimentRecord> _pass1Experiment;

        /// <summary>The transfer's run-wide counts, summed across the parallel file loop.</summary>
        private Pass2FdrSidecar.TransferTally _transferTally;

        private readonly object _transferTallyLock = new object();

        /// <param name="pass1Experiment">The analysis-wide 1st-pass experiment records.</param>
        /// <param name="logWarning">Where a per-file warning goes.</param>
        public Pass2PerFileWorker(
            IReadOnlyDictionary<uint, FdrExperimentRecord> pass1Experiment, Action<string> logWarning)
        {
            _pass1Experiment = pass1Experiment ?? throw new ArgumentNullException(nameof(pass1Experiment));
            _logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));
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
            var tally = new Pass2FdrSidecar.TransferTally();
            Pass2FdrSidecar.TransferOneFileByScore(fileName, pass1SidecarPath, survivors,
                e => e.Score, _pass1Experiment, _logWarning, ref tally);
            AddTally(fileName, pass1SidecarPath, tally);
        }

        private void AddTally(string fileName, string pass1SidecarPath, Pass2FdrSidecar.TransferTally tally)
        {
            // The transfer declines a file whose 1st-pass sidecar it cannot read, leaving its
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
        /// Report what this worker did, ONCE and DETERMINISTICALLY, after the file loop: what
        /// the transfer moved, summed across the parallel file loop.
        /// </summary>
        public void LogSummary(PipelineContext ctx)
        {
            Pass2FdrSidecar.TransferTally tally;
            lock (_transferTallyLock)
                tally = _transferTally;
            ctx.LogInfo(string.Format(
                OspreyTasksResources.Pass2PerFileWorker_LogSummary_Second_pass_FDR_over__0__files_kept_the_first_pass_q_values_of__1__peaks__gave__2__peaks_moved_by_cross_run_reconciliation_and__3__missing_peaks_new_run_level_q_values_,
                tally.FilesDone, tally.Unchanged, tally.Moved, tally.GapFill));
        }
    }
}
