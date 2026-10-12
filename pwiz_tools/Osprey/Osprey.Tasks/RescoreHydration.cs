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

using System;
using System.Collections.Generic;
using System.IO;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// One file's PRE-compaction reductions, captured while that file's full stub
    /// list is briefly resident during <see cref="RescoreHydration.HydrateOneRun"/>: the
    /// stub count and the run-level FDR passing-target count. Reducing them per file keeps
    /// both exact without holding more than one file's pool.
    /// </summary>
    public class PreCompactionTally
    {
        /// <summary>Stubs loaded from this file's parquet, before compaction.</summary>
        public int Stubs { get; set; }

        /// <summary>
        /// Non-decoy stubs in this file passing run-level FDR, before compaction.
        /// The caller owns the predicate (it needs <c>OspreyConfig</c>); this type
        /// only carries the result.
        /// </summary>
        public int PassingTargets { get; set; }
    }

    /// <summary>
    /// ONE run's Stage-6 rescore inputs, produced by
    /// <see cref="RescoreHydration.HydrateOneRun"/> from that run's own artifacts plus the
    /// analysis-wide summaries.
    ///
    /// <para>Every field here is a per-run quantity the removed all-runs bundle stored in a
    /// file-keyed dictionary. That is the whole difference, and it is the difference between a
    /// fan-out task and a join: an 86-run rescore built those dictionaries for all 86 runs
    /// before touching one, and the rescore loop then read exactly one slice per iteration.</para>
    /// </summary>
    public sealed class RunRescoreInputs
    {
        /// <summary>The run's stem, as every per-file artifact path is derived from it.</summary>
        public string FileName { get; set; }

        /// <summary>
        /// This run's POST-compaction survivors, already overlaid with its
        /// <c>.1st-pass.fdr_scores.bin</c> and filtered to the analysis-wide retained set.
        /// </summary>
        public List<FdrEntry> Survivors { get; set; }

        /// <summary>
        /// This run's reconciliation actions, keyed <c>(file_name, vec_idx)</c> against
        /// <see cref="Survivors"/>, with one file's entries in it, so a consumer written for
        /// the planner's all-runs map reads it unchanged.
        /// </summary>
        public Dictionary<(string FileName, int Index), ReconcileAction> ReconciliationActions { get; set; }

        /// <summary>This run's gap-fill targets; null when its envelope carried none.</summary>
        public List<GapFillTarget> GapFill { get; set; }

        /// <summary>This run's refined RT calibration; null when its envelope carried none.</summary>
        public RTCalibration RefinedCalibration { get; set; }

        /// <summary>
        /// The planner's full join file-stem set, carried in every run's envelope, for the
        /// reconciled parquet's join-wide metadata hash. Analysis-wide by content, per-run by
        /// storage - so reading it here costs no other run's file.
        /// </summary>
        public IReadOnlyList<string> JoinFileStems { get; set; }

        /// <summary>
        /// The join-wide first-pass passing base_ids off this run's envelope. Also analysis-wide
        /// by content and identical in every envelope.
        /// </summary>
        public HashSet<uint> GlobalFirstPassBaseIds { get; set; }

        /// <summary>This run's PRE-compaction tally, reduced while its full pool was resident.</summary>
        public PreCompactionTally Tally { get; set; }
    }

    /// <summary>
    /// Hydrate the Stage 5 → Stage 6 boundary file pair into the
    /// in-memory state needed to drive a per-file rescore. Mirrors
    /// <c>hydrate_for_rescore</c> in
    /// <c>osprey/crates/osprey/src/rescore.rs</c>.
    /// </summary>
    public static class RescoreHydration
    {
        /// <summary>
        /// Fold each run's PRE-compaction rows and DISCARD them: load the run's stubs, overlay
        /// its first-pass sidecar, hand them to <paramref name="onStubsHydrated"/>, drop them,
        /// next run. Retains nothing across runs.
        ///
        /// <para>This exists because <c>--task ModelDiagnostics</c> was reaching its rows through
        /// the all-runs bundle hydrate (since removed), whose product was Stage 6's bundle: that loop
        /// streams one run's pre-compaction pool at a time - the log line is true - and then
        /// KEEPS each run's survivors, because a bundle is what its caller wants. Measured on a
        /// 446-run cohort at 0.15-0.22 GB per run, which is the whole-run survivor pool
        /// <c>regression.ps1</c> already tracks at "0.197 GB/file live post-GC ... ~103 GB
        /// projected at 500". A report has no use for the survivors; it needs only what the
        /// callback takes.</para>
        ///
        /// <para>No envelope, no planning, no compaction, no calibration capture - a diagnostics
        /// fold needs none of them, and reading a run's <c>reconciliation.json</c> here would
        /// reintroduce a per-run cost for state nobody consumes.</para>
        /// </summary>
        public static void FoldPreCompactionPerRun(
            IList<string> parquetPaths,
            Func<int, string, string, List<FdrEntry>> loadStubs,
            Action<int, string, List<FdrEntry>> onStubsHydrated,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords,
            HashSet<uint> retainedBaseIds,
            IOspreyLog log = null)
        {
            if (parquetPaths == null)
                throw new ArgumentNullException(nameof(parquetPaths));
            if (loadStubs == null)
                throw new ArgumentNullException(nameof(loadStubs));
            if (onStubsHydrated == null)
                throw new ArgumentNullException(nameof(onStubsHydrated));
            if (retainedBaseIds == null)
                throw new ArgumentNullException(nameof(retainedBaseIds));

            using (var progress = new ProgressReporter(
                       OspreyTasksResources.RescoreHydration_FoldPreCompactionPerRun_Reading_first_pass_results_for_the_model_diagnostics_report, parquetPaths.Count))
            {
                for (int i = 0; i < parquetPaths.Count; i++)
                {
                    progress.Report(i);
                    string syntheticInput = SyntheticInputFromParquet(parquetPaths[i]);
                    string fileName = Path.GetFileNameWithoutExtension(syntheticInput);
                    if (string.IsNullOrEmpty(fileName))
                    {
                        throw new InvalidDataException(string.Format(
                            OspreyTasksResources.RescoreHydration_Cannot_tell_which_input_file_the_scores_file__0__belongs_to_from_its_name_,
                            parquetPaths[i]));
                    }

                    var stubs = loadStubs(i, fileName, parquetPaths[i]);
                    if (stubs == null)
                    {
                        throw new InvalidOperationException(string.Format(
                            @"FoldPreCompactionPerRun: no stubs loaded for {0}", fileName));
                    }

                    // Same overlay, same drift predicate as the bundle-building twin: the
                    // sidecar was written over the whole pre-compaction row set, so a record
                    // whose base_id did not survive legitimately has no entry to land on while
                    // any OTHER miss is real parquet drift.
                    OverlayFirstPassSidecar(syntheticInput, fileName, stubs, experimentRecords,
                        id => !retainedBaseIds.Contains(id & ScoringTaskShared.BASE_ID_MASK));

                    onStubsHydrated(i, fileName, stubs);

                    // The run's rows end here. Nothing references them afterwards, which is the
                    // whole difference from the bundle path - stated as code rather than left
                    // to the reader to infer from an absence.
                    stubs.Clear();
                    stubs.TrimExcess();

                    // Post-GC live set per run, under OSPREY_LOG_MEMORY only. The claim this
                    // method makes is that the floor does NOT rise with runs folded, and working
                    // set with --memstamp carries uncollected garbage, so it can only show shape.
                    // Reading shape as magnitude is what sent the previous fix after the wrong
                    // structure; this probe is what settles it.
                    ProfilerHooks.LogManagedHeapAfterGcIfEnabled(log, @"mdiag-fold-live",
                        string.Format(@"(post-GC, diagnostics fold, run {0} of {1})",
                            i + 1, parquetPaths.Count));
                }
            }
        }

        /// <summary>
        /// Read ONLY the gap-fill targets and refined calibrations out of each run's envelope -
        /// no parquet rows, no stubs, no action mapping.
        ///
        /// <para>This exists for the one consumer that genuinely spans runs: the Stage 7 pool
        /// rebuild in <c>PerFileRescoreTask.Rehydrate</c>, which overlays every run's reconciled
        /// parquet and needs the gap-fill targets to restore the detections gap-fill transferred
        /// into runs that did not find them independently. Publishing that map empty costs
        /// exactly those detections - measured on Stellar as 94 missing <c>RetentionTimes</c>
        /// rows and <c>NRunsDetected</c> falling 3 -> 2.</para>
        ///
        /// <para>It IS an all-runs read, and it is the honest scope of the remaining coupling:
        /// gap-fill is a cross-run product, so a consumer that rebuilds a cross-run pool needs
        /// all of it. It is far smaller than what it replaces - envelope JSON only, no parquet
        /// pass and no stub materialisation - and a per-run rescore never calls it, because
        /// nothing on that path rebuilds the pool.</para>
        /// </summary>
        public static void ReadGapFillAndCalibrations(
            IEnumerable<string> parquetPaths,
            Dictionary<string, List<GapFillTarget>> perFileGapFill,
            Dictionary<string, RTCalibration> refinedCalibrations,
            LibraryStringInterner sequencePool = null)
        {
            foreach (string parquetPath in parquetPaths)
            {
                string syntheticInput = SyntheticInputFromParquet(parquetPath);
                string fileName = Path.GetFileNameWithoutExtension(syntheticInput);
                if (string.IsNullOrEmpty(fileName))
                    continue;
                string reconPath = ReconciliationFile.PathForInput(syntheticInput);
                if (!File.Exists(reconPath))
                    continue;
                var envelope = LoadEnvelope(reconPath);
                CaptureCalibrationAndGapFill(envelope, fileName, refinedCalibrations, perFileGapFill,
                    sequencePool);
            }
        }

        /// <summary>
        /// Hydrate the Stage-6 rescore inputs for ONE run, from that run's own artifacts and the
        /// analysis-wide summaries - never from another run's.
        ///
        /// <para><b>The single <paramref name="parquetPath"/> is the point.</b> Its all-runs
        /// sibling, the all-runs bundle hydrate (since removed), took an <c>IList&lt;string&gt;</c> of
        /// every run's parquet, and that signature is what let a per-run task become a join: on
        /// an 86-run plate the caller spent 8m42s and 17.2 GB loading every run's stubs before
        /// rescoring one, and the rescore loop then RE-READ each run's parquet through
        /// <c>FirstPassSurvivorLoader</c> anyway and overwrote what had been loaded. The work
        /// was discarded, not used. Only the loop's discipline had ever bounded it, and
        /// discipline is what failed - so the fix is a signature that cannot express the
        /// mistake, per CRITICAL-RULES' "strengthen the verifier rather than the wording".</para>
        ///
        /// <para>What makes this correct rather than merely cheaper: every input a rescore needs
        /// is a per-run slice. <c>MultiChargeConsensus.SelectRescoreTargets</c> reads one run's
        /// entries; the reconciliation actions, gap-fill targets and refined calibration all come
        /// out of that run's own <c>reconciliation.json</c>; and the compaction predicate is the
        /// analysis-wide retained base_id set, which is library-bounded and passed in. The
        /// regression gate's mode 3 has always proved this by handing a node ONE run and getting
        /// byte-identical output - this method is that path, used as the loop body.</para>
        ///
        /// <para><paramref name="loadStubs"/> must return the run's FULL pre-compaction stub
        /// list; <paramref name="onStubsHydrated"/> is the caller's one look at it (per-run
        /// tallies, the <c>--model-diagnostics</c> fold) before compaction drops the
        /// non-survivors. Cross-run envelope agreement is not checked: a node holding one run
        /// has no sibling to compare against, and the all-runs hydrate that did hold several,
        /// and checked them, was removed with the last route that reached it.</para>
        /// </summary>
        public static RunRescoreInputs HydrateOneRun(
            string parquetPath,
            HashSet<uint> retainedBaseIds,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords,
            Func<string, string, List<FdrEntry>> loadStubs,
            Action<string, List<FdrEntry>, PreCompactionTally> onStubsHydrated = null,
            LibraryStringInterner sequencePool = null)
        {
            if (parquetPath == null)
                throw new ArgumentNullException(nameof(parquetPath));
            if (retainedBaseIds == null)
                throw new ArgumentNullException(nameof(retainedBaseIds));
            if (loadStubs == null)
                throw new ArgumentNullException(nameof(loadStubs));

            string syntheticInput = SyntheticInputFromParquet(parquetPath);
            string fileName = Path.GetFileNameWithoutExtension(syntheticInput);
            if (string.IsNullOrEmpty(fileName))
            {
                throw new InvalidDataException(string.Format(
                    OspreyTasksResources.RescoreHydration_Cannot_tell_which_input_file_the_scores_file__0__belongs_to_from_its_name_,
                    parquetPath));
            }

            string reconPath = ReconciliationFile.PathForInput(syntheticInput);
            var envelope = LoadEnvelope(reconPath);
            var planned = PlanActions(envelope);

            var refinedCalibrations = new Dictionary<string, RTCalibration>();
            var perFileGapFill = new Dictionary<string, List<GapFillTarget>>();
            CaptureCalibrationAndGapFill(envelope, fileName, refinedCalibrations, perFileGapFill,
                sequencePool);

            var stubs = loadStubs(fileName, parquetPath);
            if (stubs == null)
            {
                throw new InvalidOperationException(string.Format(
                    @"HydrateOneRun: no stubs loaded for {0}", fileName));
            }

            // Same order as the all-runs sibling: overlay, look, then compact. The
            // expectedAbsent predicate states the FILTER for the same reason it does there -
            // the sidecar was written over the whole pre-compaction row set, and a reconciled
            // parquet holds only survivors, so most records legitimately have no entry to land
            // on while any OTHER miss is real parquet drift.
            OverlayFirstPassSidecar(syntheticInput, fileName, stubs, experimentRecords,
                id => !retainedBaseIds.Contains(id & ScoringTaskShared.BASE_ID_MASK));

            var tally = new PreCompactionTally { Stubs = stubs.Count };
            onStubsHydrated?.Invoke(fileName, stubs, tally);

            stubs.RemoveAll(e => !retainedBaseIds.Contains(e.EntryId & ScoringTaskShared.BASE_ID_MASK));
            stubs.TrimExcess();

            var idToIdx = new Dictionary<uint, int>(stubs.Count);
            for (int idx = 0; idx < stubs.Count; idx++)
                idToIdx[stubs[idx].EntryId] = idx;
            var actions = new Dictionary<(string, int), ReconcileAction>();
            MapPlannedActions(planned, fileName, reconPath, idToIdx, actions);

            perFileGapFill.TryGetValue(fileName, out var gapFill);
            refinedCalibrations.TryGetValue(fileName, out var refinedCalibration);
            return new RunRescoreInputs
            {
                FileName = fileName,
                Survivors = stubs,
                ReconciliationActions = actions,
                GapFill = gapFill,
                RefinedCalibration = refinedCalibration,
                JoinFileStems = NormalizeStems(envelope.FileStems),
                GlobalFirstPassBaseIds = new HashSet<uint>(envelope.FirstPassBaseIds),
                Tally = tally,
            };
        }

        /// <summary>
        /// ONE run's post-compaction survivors, refilled IN PLACE, for a join that folds over the
        /// runs one at a time and drops each - the Stage 7 shape (issue #4486).
        ///
        /// <para>The same three steps <see cref="HydrateOneRun"/> takes - load the run's stubs,
        /// overlay its 1st-pass sidecar, compact to the analysis-wide retained set - and NOT the
        /// fourth. Stage 7 runs no rescore, so it needs neither the planned actions nor the
        /// <c>reconciliation.json</c> they come from, and reading that envelope per run would put
        /// ~6 MB of join-wide <c>first_pass_base_ids</c> through this call 446 times to answer a
        /// question nobody asks. Sharing the whole of <c>HydrateOneRun</c> would have been the
        /// tidier-looking choice and the more expensive one.</para>
        ///
        /// <para><b>Refills the caller's list rather than returning a new one.</b> That list is
        /// the shared backing store every <c>PerFileEntries</c> milestone wraps, so replacing the
        /// reference would leave the published milestones pointing at the old one - the same rule
        /// the Stage 6 refill follows. Contents are transient; identity is not.</para>
        ///
        /// <para>Equivalence with the resident path is what makes a streamed Stage 7 produce the
        /// same bytes: this is the state the removed all-runs bundle hydrate left a run in,
        /// reached by the same calls in the same order. The difference is only how long the list
        /// lived.</para>
        ///
        /// <para>No first-pass file is opened: the Boundary 3 -&gt; 4 contract. Every run
        /// carries a current <c>.2nd-pass.fdr_scores.bin</c>, which holds every scalar the join
        /// reads, so opening the first-pass one would reach back across the boundary issue #4486
        /// established - for every run in the cohort, on precisely the leg where an orchestrator
        /// is entitled not to have shipped them. It used to be opened for a run with no
        /// second-pass answer yet, which was a mode whose per-run half still ran in the join;
        /// none does since #4665.</para>
        /// </summary>
        public static void RefillOneRunSurvivors(
            string fileName,
            string parquetPath,
            List<FdrEntry> survivors,
            HashSet<uint> retainedBaseIds,
            Func<string, string, List<FdrEntry>> loadStubs)
        {
            if (survivors == null)
                throw new ArgumentNullException(nameof(survivors));
            if (retainedBaseIds == null)
                throw new ArgumentNullException(nameof(retainedBaseIds));
            if (loadStubs == null)
                throw new ArgumentNullException(nameof(loadStubs));

            var stubs = loadStubs(fileName, parquetPath);
            if (stubs == null)
            {
                throw new InvalidOperationException(string.Format(
                    @"RefillOneRunSurvivors: no stubs loaded for {0}", fileName));
            }
            // Kept even where the reconciled parquet is already the survivor subset and this
            // removes nothing. It is the analysis-wide compaction predicate, it is the same
            // one both siblings apply, and "the parquet is already subset so the filter is
            // redundant" is a property of an artifact generation rather than of the format -
            // exactly the kind of assumption that turns into a silently larger pool.
            stubs.RemoveAll(e => !retainedBaseIds.Contains(e.EntryId & ScoringTaskShared.BASE_ID_MASK));

            survivors.Clear();
            survivors.AddRange(stubs);
            survivors.TrimExcess();
        }

        /// <summary>
        /// Overlay the first-pass FDR statistics onto <paramref name="stubs"/>: the RUN-scope
        /// SVM score, run q-values and PEP from <c>&lt;stem&gt;.1st-pass.fdr_scores.bin</c>,
        /// and the EXPERIMENT-scope q-values from <paramref name="experimentRecords"/>, which
        /// the caller read once from the analysis-wide
        /// <c>&lt;blib-stem&gt;.1st-pass.fdr_experiment.bin</c> (format v5, issue #4486).
        /// <c>expected_pass = FirstPass</c>: the planner's actions were computed against
        /// first-pass FDR, and the compaction predicate uses first-pass q-values. The stub
        /// list must be the FULL pre-compaction set - the sidecar was written before
        /// compaction and its reader requires a superset of its records.
        ///
        /// <para>Both halves are restored because a hydrated stub has to be indistinguishable
        /// from the resident one it stands in for. The experiment q-values in particular reach
        /// the <c>--model-diagnostics</c> report through
        /// <c>ScoringTaskShared.FeedModelDiagnostics</c>, and that report is compared against a
        /// committed golden - so a stub rehydrated without them would not fail here, it would
        /// fail as a wrong number in a report two stages later.</para>
        ///
        /// <para><c>expectedAbsent</c> is non-null ONLY for a caller whose list is
        /// legitimately a subset of that, one that loads from the reconciled parquet, which
        /// carries only the Stage 5 survivors (issue #4486). Every other caller stays
        /// strict.</para>
        /// </summary>
        private static void OverlayFirstPassSidecar(
            string syntheticInput, string fileName, List<FdrEntry> stubs,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords,
            Func<uint, bool> expectedAbsent = null)
        {
            string sidecarPath = FdrScoresSidecar.Pass1Path(syntheticInput);
            // The experiment records go THROUGH the reader, not over the stub list afterwards:
            // the reader applies them to the entries it binds a record to, which is the set a
            // by-entry_id loop would wrongly widen to include gap-fill stubs.
            if (!FdrScoresSidecar.TryRead(sidecarPath, stubs, FdrScoresSidecar.Pass.FirstPass,
                    expectedAbsent, experimentRecords))
            {
                throw new InvalidDataException(string.Format(
                    OspreyTasksResources.RescoreHydration_OverlayFirstPassSidecar_Failed_to_read_the_first_pass_intermediate_file_for__0___expected_at__1___,
                    fileName, sidecarPath));
            }
        }

        /// <summary>
        /// Read one <c>reconciliation.json</c> envelope, wrapping any reader failure in
        /// <see cref="InvalidDataException"/> with the offending path named.
        /// </summary>
        private static ReconciliationFile LoadEnvelope(string reconPath)
        {
            try
            {
                return ReconciliationFile.Load(reconPath);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(string.Format(
                    OspreyTasksResources.RescoreHydration_LoadEnvelope_Failed_to_read__0____1_, reconPath, ex.Message), ex);
            }
        }
        /// <summary>
        /// The planner's non-Keep actions for one file, flattened out of the envelope's two
        /// homogeneous arrays in the order the hydrators consume them (<c>use_cwt_peak</c>
        /// first, then <c>forced_integration</c>, each in envelope order). Both hydrate paths
        /// go through this so they agree on action content, on which action wins when the
        /// planner emitted two for one <c>entry_id</c> (the later one, as a
        /// <c>vec_idx</c>-keyed assignment would), and on which missing <c>entry_id</c>
        /// raises the drift error first.
        /// </summary>
        private static List<PlannedAction> PlanActions(ReconciliationFile envelope)
        {
            var planned = new List<PlannedAction>(
                (envelope.UseCwtPeakActions?.Count ?? 0) + (envelope.ForcedIntegrationActions?.Count ?? 0));
            if (envelope.UseCwtPeakActions != null)
            {
                foreach (var entry in envelope.UseCwtPeakActions)
                {
                    planned.Add(new PlannedAction(entry.EntryId, @"use_cwt_peak",
                        new ReconcileAction.UseCwtPeak(
                            (int)entry.CandidateIdx, entry.StartRt, entry.ApexRt, entry.EndRt)));
                }
            }
            if (envelope.ForcedIntegrationActions != null)
            {
                foreach (var entry in envelope.ForcedIntegrationActions)
                {
                    planned.Add(new PlannedAction(entry.EntryId, @"forced_integration",
                        new ReconcileAction.ForcedIntegration(entry.ExpectedRt, entry.HalfWidth)));
                }
            }
            return planned;
        }

        /// <summary>
        /// Rehome one file's <see cref="PlanActions"/> output from <c>entry_id</c> onto the
        /// <c>(file_name, vec_idx)</c> keys the rescore engine consumes, using
        /// <paramref name="idToIdx"/> over whichever stub list the caller holds. An
        /// <c>entry_id</c> the stub list does not carry is parquet/boundary drift and stops
        /// the run: a Stage 6 worker proceeding with missing actions would scramble gap-fill.
        /// </summary>
        private static void MapPlannedActions(
            List<PlannedAction> planned,
            string fileName,
            string reconPath,
            Dictionary<uint, int> idToIdx,
            Dictionary<(string, int), ReconcileAction> reconciliationActions)
        {
            foreach (var action in planned)
            {
                if (!idToIdx.TryGetValue(action.EntryId, out int vecIdx))
                {
                    throw new InvalidDataException(string.Format(
                        OspreyTasksResources.RescoreHydration_MapPlannedActions__0__refers_to_precursor_candidate__1___which_is_not_in_the_scores_file_for_that_run_,
                        reconPath, action.EntryId, FdrScoresSidecar.FIRST_PASS_FILE_PATTERN));
                }
                reconciliationActions[(fileName, vecIdx)] = action.Action;
            }
        }

        /// <summary>
        /// Capture one envelope's refined RT calibration and gap-fill targets into the
        /// per-file dictionaries. A null <c>refined_rt_calibration</c> (Stage 5 refit failed)
        /// and an empty gap-fill list both leave the file absent, which is how the rescore
        /// engine reads "nothing to do here".
        ///
        /// <para>The gap-fill sequences go through <paramref name="sequencePool"/> for the same
        /// reason the stubs do: Json.NET hands out a fresh string per property, and these
        /// targets are retained for EVERY file at once (5.5 K - 12.3 K per file, ~2 M at 257
        /// CHS files). Null pool leaves them as read.</para>
        /// </summary>
        private static void CaptureCalibrationAndGapFill(
            ReconciliationFile envelope,
            string fileName,
            Dictionary<string, RTCalibration> refinedCalibrations,
            Dictionary<string, List<GapFillTarget>> perFileGapFill,
            LibraryStringInterner sequencePool)
        {
            if (envelope.RefinedRtCalibration != null)
            {
                refinedCalibrations[fileName] = RTCalibration.FromModelParams(
                    envelope.RefinedRtCalibration.LibraryRts,
                    envelope.RefinedRtCalibration.FittedRts,
                    envelope.RefinedRtCalibration.AbsResiduals,
                    envelope.RefinedRtCalibration.ResidualSd);
            }

            if (envelope.GapFillTargets != null && envelope.GapFillTargets.Count > 0)
            {
                var gapFill = new List<GapFillTarget>(envelope.GapFillTargets.Count);
                foreach (var g in envelope.GapFillTargets)
                {
                    gapFill.Add(new GapFillTarget
                    {
                        TargetEntryId = g.TargetEntryId,
                        DecoyEntryId = g.DecoyEntryId,
                        ExpectedRt = g.ExpectedRt,
                        HalfWidth = g.HalfWidth,
                        ModifiedSequence = sequencePool != null
                            ? sequencePool.Intern(g.ModifiedSequence)
                            : g.ModifiedSequence,
                        Charge = g.Charge,
                    });
                }
                perFileGapFill[fileName] = gapFill;
            }
        }

        /// <summary>
        /// One planner action paired with the <c>entry_id</c> it targets and the envelope
        /// array it came from (used only to name the array in the drift error).
        /// </summary>
        private sealed class PlannedAction
        {
            public uint EntryId { get; }
            public string Kind { get; }
            public ReconcileAction Action { get; }

            public PlannedAction(uint entryId, string kind, ReconcileAction action)
            {
                EntryId = entryId;
                Kind = kind;
                Action = action;
            }
        }

        /// <summary>
        /// Sort + dedup a list of file stems (Ordinal). Returns a new list;
        /// the input is not mutated. Empty / null input becomes an empty
        /// list. Used to canonicalize the <c>file_stems</c> field from
        /// each <c>reconciliation.json</c> envelope before consistency
        /// checks across siblings.
        /// </summary>
        private static List<string> NormalizeStems(IList<string> stems)
        {
            if (stems == null || stems.Count == 0)
                return new List<string>();
            var result = new List<string>(stems.Count);
            foreach (var s in stems)
            {
                if (!string.IsNullOrEmpty(s))
                    result.Add(s);
            }
            result.Sort(StringComparer.Ordinal); // Array.Sort OK: sorted only to dedup adjacent identical stems immediately below; equal keys are byte-identical so tie order is irrelevant
            for (int i = result.Count - 1; i > 0; i--)
            {
                if (string.Equals(result[i], result[i - 1], StringComparison.Ordinal))
                    result.RemoveAt(i);
            }
            return result;
        }

        /// <summary>
        /// Inverse of <c>scores_path_for_input</c>: given
        /// <c>/data/sample1.scores.parquet</c> (or its reconciled sibling
        /// <c>/data/sample1.scores-reconciled.parquet</c>), produce a synthetic
        /// input path <c>/data/sample1.mzML</c> whose stem matches what the
        /// worker used. This lets the worker reuse the existing
        /// path-derivation helpers (FDR sidecars, calibration JSON,
        /// reconciliation JSON) without duplicating them. The synthetic
        /// path is never opened — only its components are inspected.
        /// Mirrors Rust's <c>synthetic_input_from_parquet</c>.
        ///
        /// <para>Its REASON is gone. It existed because <c>--input-scores</c> named parquets
        /// on the command line, so the pipeline's first act was to convert them back into
        /// data-file names for the sidecar helpers - a round trip, and the clearest evidence
        /// that the flag was a second way of saying what <c>--task</c> already said. That
        /// flag has retired; every task is given the data-file names directly.</para>
        ///
        /// <para>What is left is internal: the hydrate methods below still take a PARQUET
        /// path per run (from <c>PerFileParquetPaths</c>, which is how the pipeline carries
        /// them), and derive the stem back from it. Inverting those signatures to take the
        /// input and derive the parquet is the remaining half of the retirement - a
        /// no-behaviour-change refactor, deliberately not folded into the CLI change so the
        /// gate can attribute a failure to one of them. See
        /// TODO-20260908_osprey_input_scores_retirement.md.</para>
        /// </summary>
        public static string SyntheticInputFromParquet(string parquetPath)
        {
            if (parquetPath == null) throw new ArgumentNullException(nameof(parquetPath));
            // GetFileNameWithoutExtension returns "" not null for valid paths
            // and throws on invalid input, so the result is never null here.
            string stem = Path.GetFileNameWithoutExtension(parquetPath);
            // Strip the trailing ".scores-reconciled" (Stage 6 reconciled output)
            // or ".scores" (Stage 4 output). These two tokens never collide with
            // an input stem because Stage 4 always appends exactly ".scores"
            // (so the only way a name ends in ".scores-reconciled" is Stage 6).
            // GetFileNameWithoutExtension of "x.scores-reconciled.parquet" is
            // "x.scores-reconciled"; check the longer token first.
            string reconciledScoresSuffix = Path.GetFileNameWithoutExtension(ParquetScoreCache.EXT_SCORES_RECONCILED);
            string scoresSuffix = Path.GetFileNameWithoutExtension(ParquetScoreCache.EXT_SCORES);
            if (stem.EndsWith(reconciledScoresSuffix, StringComparison.Ordinal))
                stem = stem.Substring(0, stem.Length - reconciledScoresSuffix.Length);
            else if (stem.EndsWith(scoresSuffix, StringComparison.Ordinal))
                stem = stem.Substring(0, stem.Length - scoresSuffix.Length);
            string parent = Path.GetDirectoryName(parquetPath);
            string filename = stem + SpectrumFileReader.EXT_MZML;
            return string.IsNullOrEmpty(parent) ? filename : Path.Combine(parent, filename);
        }
    }
}
