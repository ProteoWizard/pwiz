/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR.Reconciliation;
using pwiz.OspreySharp.Tasks;

namespace pwiz.OspreySharp.PerFileRescore
{
    /// <summary>
    /// Stage 6 per-file rescore phase: re-scores each input file's
    /// previously-scored entries against the consensus + reconciliation
    /// boundaries produced by the first-join phase, runs the gap-fill
    /// two-pass for missing precursors, and writes the reconciled
    /// results back into the per-file <c>.scores.parquet</c>. The HPC
    /// "second per-file fan-out" boundary in the
    /// <c>Osprey-workflow.html</c> view — each input file's rescore is
    /// independent of the others.
    ///
    /// Phase A scope: this task is a thin orchestration wrapper around
    /// <see cref="AnalysisPipeline.ExecuteStage6Rescore"/> (which
    /// already lives in its own partial-class file, <c>AnalysisPipeline
    /// .Stage6Rescore.cs</c>) plus the Rust-mirrored cross-impl
    /// post-rescore diagnostic dump and the per-process diagnostic
    /// writer close calls. The heavy lifting stays in
    /// AnalysisPipeline.Stage6Rescore.cs for now.
    /// </summary>
    internal sealed class PerFileRescoreTask : OspreyTask
    {
        private readonly AnalysisPipeline _pipeline;
        private readonly List<KeyValuePair<string, List<FdrEntry>>> _perFileEntries;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> _perFileConsensusTargets;
        private readonly IReadOnlyDictionary<(string FileName, int Index), ReconcileAction> _reconciliationActions;
        private readonly IReadOnlyDictionary<string, RTCalibration> _refinedCalibrations;
        private readonly IReadOnlyDictionary<string, RTCalibration> _perFileCalibrations;
        private readonly IReadOnlyDictionary<string, List<GapFillTarget>> _perFileGapFill;
        private readonly IReadOnlyDictionary<string, string> _perFileParquetPaths;
        private readonly List<LibraryEntry> _fullLibrary;

        public PerFileRescoreTask(
            AnalysisPipeline pipeline,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> perFileConsensusTargets,
            IReadOnlyDictionary<(string FileName, int Index), ReconcileAction> reconciliationActions,
            IReadOnlyDictionary<string, RTCalibration> refinedCalibrations,
            IReadOnlyDictionary<string, RTCalibration> perFileCalibrations,
            IReadOnlyDictionary<string, List<GapFillTarget>> perFileGapFill,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            List<LibraryEntry> fullLibrary)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _perFileEntries = perFileEntries ?? throw new ArgumentNullException(nameof(perFileEntries));
            _perFileConsensusTargets = perFileConsensusTargets;
            _reconciliationActions = reconciliationActions
                ?? new Dictionary<(string, int), ReconcileAction>();
            _refinedCalibrations = refinedCalibrations;
            _perFileCalibrations = perFileCalibrations;
            _perFileGapFill = perFileGapFill;
            _perFileParquetPaths = perFileParquetPaths;
            _fullLibrary = fullLibrary ?? throw new ArgumentNullException(nameof(fullLibrary));
        }

        public override string Name => @"PerFileRescore";

        public override bool Run(PipelineContext ctx)
        {
            var rescoreStats = _pipeline.ExecuteStage6Rescore(
                _perFileEntries,
                _perFileConsensusTargets,
                _reconciliationActions,
                _refinedCalibrations,
                _perFileCalibrations,
                perFileGapFill: _perFileGapFill,
                _perFileParquetPaths,
                _fullLibrary,
                ctx.Config);
            ctx.LogInfo(string.Format(
                @"Stage 6 rescore: {0} entries re-scored ({1} reconciliation actions executed)",
                rescoreStats.TotalRescored, rescoreStats.TotalReconciliation));

            // Cross-impl bisection seam: dump per-precursor state
            // immediately after the rescore loop. Mirrors Rust's
            // dump_stage6_rescored call from pipeline.rs.
            if (OspreyDiagnostics.DumpRescored)
            {
                OspreyDiagnostics.WriteStage6RescoredDump(_perFileEntries);
                if (OspreyDiagnostics.RescoredOnly)
                    OspreyDiagnostics.ExitAfterDump(@"OSPREY_RESCORED_ONLY");
            }

            // Flush + close the persistent per-process diagnostic
            // dump writers (no-ops when their env vars are unset).
            // Mirrors the worker-mode close calls in
            // AnalysisPipeline.Stage6Rescore.Run; without these,
            // the in-process pipeline path can leave the writers
            // unflushed and produce truncated bisection dumps.
            OspreyDiagnostics.CloseMpInputsDump();
            OspreyDiagnostics.ClosePredictRtDump();
            OspreyDiagnostics.CloseCwtPathDump();
            return true;
        }
    }
}
