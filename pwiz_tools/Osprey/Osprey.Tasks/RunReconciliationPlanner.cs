/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Threading;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The per-run half of Stage 6 planning, run by <see cref="PerFileRescoreTask"/> on each run
    /// just before rescoring it: refit the run's RT calibration on the cross-run consensus,
    /// choose its reconciliation actions, and find its gap-fill targets. Produces the run's
    /// <see cref="ReconciliationFile"/>.
    ///
    /// <para>Every input is the run's own or analysis-wide and bounded:
    /// the <see cref="Stage6Consensus"/> FirstPassFDR wrote, the run's own calibration and
    /// isolation windows, and two O(library) lookups. The planners read their per-run
    /// dictionaries only at this run's key, so a node handed one run plans it exactly as a
    /// process holding the whole cohort does. Built once per task and shared by every run, so
    /// <see cref="PlanRun"/> is safe to call from the rescore's file lanes.</para>
    ///
    /// <para>The consensus is loaded, and the planners built from it, when the first run is
    /// planned - a resume that finds every run already done never reads it.</para>
    /// </summary>
    internal sealed class RunReconciliationPlanner
    {
        private readonly double _consensusFdr;
        private readonly IReadOnlyDictionary<string, string> _perFileParquetPaths;
        private readonly Lazy<State> _state;
        private readonly PipelineContext _ctx;
        private int _runsPlanned;
        private int _refitCount;
        private int _actionCount;

        /// <summary>The consensus and the two planners built from it. Planners are null when the
        /// consensus is empty: there is nothing to plan, only the refit to run.</summary>
        private sealed class State
        {
            public Stage6Consensus Consensus;
            public ReconciliationPlanner.FilePlanner Planner;
            public GapFillTargetIdentifier.FileIdentifier GapFiller;
        }

        /// <param name="loadConsensus">Reads the analysis's consensus, or returns null when
        /// cross-run reconciliation is off, which plans every run as one with no consensus.
        /// Called once, by whichever lane plans first.</param>
        /// <param name="perFileCalibrations">Each run's Stage 3 RT calibration.</param>
        /// <param name="perFileIsolationMz">Each run's isolation-window m/z coverage.</param>
        /// <param name="fullLibrary">The library, for the gap-fill lookups.</param>
        /// <param name="perFileParquetPaths">Each run's Stage 4 scores parquet, for its CWT
        /// candidates.</param>
        /// <param name="config">The analysis configuration.</param>
        /// <param name="ctx">For the per-run and summary log lines.</param>
        internal RunReconciliationPlanner(
            Func<Stage6Consensus> loadConsensus,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz,
            IReadOnlyList<LibraryEntry> fullLibrary,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            PipelineContext ctx)
        {
            _consensusFdr = config.Reconciliation.ConsensusFdr;
            _perFileParquetPaths = perFileParquetPaths;
            _ctx = ctx;
            _state = new Lazy<State>(() => BuildState(loadConsensus(), perFileCalibrations,
                perFileIsolationMz, fullLibrary), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Plan one run from its compacted survivors and return the plan as the run's
        /// <see cref="ReconciliationFile"/>, ready to save.
        ///
        /// <para>Plans over a canonically sorted copy of <paramref name="survivors"/>, the order
        /// FirstPassFDR's survivor loader gave the planner when planning ran there. The refit's
        /// LOESS input is built in entry order, so the order is part of the answer; the caller's
        /// list keeps its own order, since the plan names its targets by entry_id.</para>
        /// </summary>
        internal ReconciliationFile PlanRun(string fileName, IReadOnlyList<FdrEntry> survivors)
        {
            var state = _state.Value;
            var entries = new List<FdrEntry>(survivors);
            FdrEntry.SortCanonicalResolved(entries, fileName);
            var refined = CalibrationRefit.Refit(state.Consensus.Consensus, entries, _consensusFdr);

            List<KeyValuePair<int, ReconcileAction>> actions = null;
            IReadOnlyList<GapFillTarget> gapFill = Array.Empty<GapFillTarget>();
            if (state.Planner != null)
            {
                // The run's CWT candidates are read here and released with the plan.
                actions = new List<KeyValuePair<int, ReconcileAction>>();
                state.Planner.PlanFile(fileName, refined, entries,
                    CwtCandidateLoader.LoadOneFile(fileName, _perFileParquetPaths), actions);
                gapFill = state.GapFiller.IdentifyFile(fileName, refined, entries);
            }

            var plan = BuildReconciliationFile(entries, actions, gapFill, refined);
            Interlocked.Increment(ref _runsPlanned);
            if (refined != null)
                Interlocked.Increment(ref _refitCount);
            if (actions != null)
            {
                Interlocked.Add(ref _actionCount, actions.Count);
                // Skyline's peak-boundary-imputation vocabulary: use_cwt re-picks a peak, forced
                // integration imputes its boundaries, and a gap-fill target is a missing peak.
                // Only when planning ran: without a cross-run consensus every count is zero and
                // the line describes work that cannot happen.
                _ctx.LogInfo(string.Format(
                    OspreyTasksResources.FirstPassFdrTask_WriteReconciliationFile_Cross_run_reconciliation_for__0____1__peaks_to_re_pick___2__peak_boundaries_to_impute___3_,
                    fileName, plan.UseCwtPeakActions.Count, plan.ForcedIntegrationActions.Count,
                    plan.GapFillTargets.Count));
            }
            return plan;
        }

        /// <summary>Non-Keep actions planned over every run this process planned.</summary>
        internal int ActionCount => _actionCount;

        /// <summary>
        /// The totals over every run this process planned. Silent when it planned none - a
        /// resume that adopted every run - or had no consensus to plan from.
        /// </summary>
        internal void LogSummary()
        {
            if (_runsPlanned == 0 || _state.Value.Planner == null)
                return;
            _ctx.LogInfo(string.Format(
                OspreyTasksResources.Stage6Planner_PlanFiles_Refined_the_retention_time_calibration_of__0__of__1__files_using_the_cross_run_consensus_,
                _refitCount, _runsPlanned));
            _ctx.LogInfo(string.Format(
                OspreyTasksResources.Stage6Planner_PlanFiles_Cross_run_reconciliation___0__peak_re_picks_and_boundary_imputations_planned_across__1__,
                _actionCount, _runsPlanned));
        }

        /// <summary>
        /// The consensus and, when it is not empty, the planners built from it. An empty
        /// consensus (one run, or no peptide in enough runs) is a legitimate skip: each run is
        /// still refit, and planning finds nothing to do.
        /// </summary>
        private static State BuildState(Stage6Consensus consensus,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyDictionary<string, IReadOnlyList<(double Lo, double Hi)>> perFileIsolationMz,
            IReadOnlyList<LibraryEntry> fullLibrary)
        {
            var state = new State
            {
                Consensus = consensus ?? new Stage6Consensus
                {
                    Consensus = Array.Empty<PeptideConsensusRT>(),
                    PassingBaseIds = new HashSet<(uint, byte)>(),
                    PassingPrecursors = new HashSet<(string ModifiedSequence, byte Charge)>(),
                },
            };
            if (state.Consensus.Consensus.Count == 0)
                return state;
            // Each run's refit is passed to PlanFile and IdentifyFile explicitly, so the refit
            // lookup they also take is empty and no call on this path reads it.
            IReadOnlyDictionary<string, RTCalibration> noRefitLookup = new Dictionary<string, RTCalibration>();
            BuildGapFillLibraryLookups(fullLibrary, out var libLookup, out var libPrecursorMz);
            state.Planner = new ReconciliationPlanner.FilePlanner(
                state.Consensus.Consensus, state.Consensus.PassingBaseIds, noRefitLookup, perFileCalibrations);
            state.GapFiller = new GapFillTargetIdentifier.FileIdentifier(
                state.Consensus.Consensus, state.Consensus.PassingPrecursors, noRefitLookup, perFileCalibrations,
                libLookup, libPrecursorMz, perFileIsolationMz);
            return state;
        }

        /// <summary>
        /// Build the <c>(modified_sequence, charge) -&gt; (target_id, decoy_id)</c> and
        /// <c>entry_id -&gt; precursor_mz</c> lookups gap-fill identification needs. Decoy ID
        /// convention: <c>target_id | 0x80000000</c> (mirrors Rust at pipeline.rs:3330-3340).
        /// O(library), built once per task.
        /// </summary>
        private static void BuildGapFillLibraryLookups(
            IReadOnlyList<LibraryEntry> fullLibrary,
            out Dictionary<(string ModifiedSequence, byte Charge), (uint TargetEntryId, uint DecoyEntryId)> libLookup,
            out Dictionary<uint, double> libPrecursorMz)
        {
            libLookup = new Dictionary<(string ModifiedSequence, byte Charge), (uint TargetEntryId, uint DecoyEntryId)>();
            libPrecursorMz = new Dictionary<uint, double>();
            foreach (var entry in fullLibrary)
            {
                if (entry.IsDecoy)
                    continue;
                uint decoyId = entry.Id | 0x80000000u;
                libLookup[(entry.ModifiedSequence, entry.Charge)] = (entry.Id, decoyId);
                libPrecursorMz[entry.Id] = entry.PrecursorMz;
            }
        }

        /// <summary>
        /// Convert one run's plan into the <see cref="ReconciliationFile"/> wire format: resolve
        /// each entry index to its entry_id, split the non-Keep actions into homogeneous
        /// use_cwt_peak / forced arrays, snapshot the refined RT calibration if present, and copy
        /// the gap-fill targets (already sorted by <c>target_entry_id</c> by the identifier).
        /// </summary>
        private static ReconciliationFile BuildReconciliationFile(
            IReadOnlyList<FdrEntry> fileEntries,
            IReadOnlyList<KeyValuePair<int, ReconcileAction>> fileActions,
            IReadOnlyList<GapFillTarget> gapFillTargets,
            RTCalibration refinedCalibration)
        {
            var useCwt = new List<UseCwtPeakEntry>();
            var forced = new List<ForcedIntegrationEntry>();
            if (fileActions != null)
            {
                foreach (var kvp in fileActions)
                {
                    int idx = kvp.Key;
                    if (idx < 0 || idx >= fileEntries.Count)
                        continue;
                    uint entryId = fileEntries[idx].EntryId;
                    var useCwtAction = kvp.Value as ReconcileAction.UseCwtPeak;
                    var forcedAction = kvp.Value as ReconcileAction.ForcedIntegration;
                    if (useCwtAction != null)
                    {
                        useCwt.Add(new UseCwtPeakEntry
                        {
                            ApexRt = useCwtAction.ApexRt,
                            CandidateIdx = (uint)useCwtAction.CandidateIndex,
                            EndRt = useCwtAction.EndRt,
                            EntryId = entryId,
                            StartRt = useCwtAction.StartRt,
                        });
                    }
                    else if (forcedAction != null)
                    {
                        forced.Add(new ForcedIntegrationEntry
                        {
                            EntryId = entryId,
                            ExpectedRt = forcedAction.ExpectedRt,
                            HalfWidth = forcedAction.HalfWidth,
                        });
                    }
                }
            }
            // Sort by entry_id for deterministic output (matches Rust).
            // Array.Sort OK: EntryId is effectively unique here (reconcile actions are
            // keyed by distinct per-file entry index, at most one action per row), so the
            // comparator does not tie in practice. Tie hazard, conversion deferred: if a
            // file ever carried duplicate EntryIds each with an action they would tie, and
            // this is not a #4362 approved U-site (converting could change the golden).
            useCwt.Sort((a, b) => a.EntryId.CompareTo(b.EntryId)); // Array.Sort OK: (see above) EntryId effectively unique; tie hazard deferred, not a #4362 approved U-site
            forced.Sort((a, b) => a.EntryId.CompareTo(b.EntryId)); // Array.Sort OK: (see above) EntryId effectively unique; tie hazard deferred, not a #4362 approved U-site

            RefinedRtCalibrationJson refinedJson = null;
            if (refinedCalibration != null)
            {
                refinedJson = new RefinedRtCalibrationJson
                {
                    AbsResiduals = (double[])refinedCalibration.AbsResiduals.Clone(),
                    FittedRts = (double[])refinedCalibration.FittedValues.Clone(),
                    LibraryRts = (double[])refinedCalibration.LibraryRts.Clone(),
                    ResidualSd = refinedCalibration.ResidualSD,
                };
            }

            // Map per-file GapFillTarget records (already sorted by
            // target_entry_id) to the wire form. Field-for-field copy.
            var gap = new List<GapFillEntry>(gapFillTargets?.Count ?? 0);
            if (gapFillTargets != null)
            {
                foreach (var g in gapFillTargets)
                {
                    gap.Add(new GapFillEntry
                    {
                        Charge = g.Charge,
                        DecoyEntryId = g.DecoyEntryId,
                        ExpectedRt = g.ExpectedRt,
                        HalfWidth = g.HalfWidth,
                        ModifiedSequence = g.ModifiedSequence,
                        TargetEntryId = g.TargetEntryId,
                    });
                }
            }

            return new ReconciliationFile
            {
                ForcedIntegrationActions = forced,
                FormatVersion = ReconciliationFile.CurrentFormatVersion,
                GapFillTargets = gap,
                RefinedRtCalibration = refinedJson,
                UseCwtPeakActions = useCwt,
            };
        }
    }
}
