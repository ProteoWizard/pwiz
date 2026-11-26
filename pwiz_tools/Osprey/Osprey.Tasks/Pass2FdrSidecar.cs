/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using pwiz.Common.Pipeline;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.IO;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The SecondPassFDR 2nd-pass FDR sidecar step (Stage 8 input prep, mirrors
    /// Rust pipeline.rs:4394-4494): reload PIN features from the reconciled
    /// parquets, run 2nd-pass Percolator on the post-reconciliation entries,
    /// write the per-file <c>.2nd-pass.fdr_scores.bin</c> sidecars, then reload
    /// those sidecars onto the post-compaction stubs so run-wide protein FDR
    /// sees the 2nd-pass q-values rather than the stale 1st-pass values.
    ///
    /// Extracted verbatim from <see cref="SecondPassFdrTask.Run(PipelineContext)"/> as pure code
    /// motion so that method reads as a sequencer; behavior (and therefore the
    /// 2nd-pass sidecars and downstream protein-FDR / blib output) is unchanged.
    /// The parity-locked 2nd-pass scoring core (<c>FirstPassFdrTask.RunPercolatorFdr</c>)
    /// is invoked whole through the
    /// live <see cref="PipelineContext"/>; it is not decomposed here.
    /// </summary>
    internal static class Pass2FdrSidecar
    {
        /// <summary>
        /// The low 31 bits of an entry_id: its base_id, with the high bit marking a decoy.
        ///
        /// <para>Declared here because <c>BASE_ID_MASK</c> is internal to
        /// Osprey.FDR and therefore invisible from this assembly. Three other files in
        /// Osprey.Tasks already carry a private copy of the same literal
        /// (<c>FdrBenchInputWriter</c>, <c>ModelDiagnosticsReport</c>,
        /// <c>PeakCoAssignmentSource</c>); this one is internal rather than private so the
        /// second-pass worker shares it instead of adding a fifth. Consolidating all five - most
        /// cleanly by making the FDR constant public, since it encodes a cross-assembly wire
        /// convention rather than an implementation detail - is worth doing separately.</para>
        /// </summary>
        internal const uint BASE_ID_MASK = 0x7FFFFFFF;

        /// <summary>
        /// Run SecondPassFDR's second pass: fold every run's per-run 2nd-pass answer, written by
        /// PerFileRescoring, into the analysis-wide experiment scope, and put the answers back on
        /// the survivors the later folds read. <paramref name="taskName"/> and
        /// <paramref name="taskValidityKey"/> are the owning task's identity.
        ///
        /// <para><b>No per-run file is written here (#4665).</b> The second pass's per-file half
        /// runs in the rescore worker (<see cref="Pass2PerFileWorker"/>), so every run's
        /// second-pass records are PerFileRescoring's output and this stage's input. That removed
        /// three things that lived here: a whole-pool second pass, which held every run's
        /// survivors for the whole stage; a fallback that recomputed any run the worker had not
        /// answered; and a rewrite of every per-run file from the pool when Stage 6 had done no
        /// work anywhere - a path that published no experiment scope and so failed when the
        /// experiment sidecar was written.</para>
        ///
        /// <para>A run without a current answer is a stop rather than a run to recompute: there
        /// is nothing here to recompute it with, and the remedy is PerFileRescoring's.</para>
        /// </summary>
        internal static void ComputeAndPersist(
            PipelineContext ctx,
            RescoredEntries rescored,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            string taskName,
            string taskValidityKey)
        {
            var config = ctx.Config;

            // Log how this 2nd pass assigns reported q-values, so a run's provenance is in the
            // log. The transfer is the only mode: each run's own first-pass model scores the
            // moved peaks and each file maps score to run q with its own first-pass table;
            // experiment q stays anchored on the best peak.
            ctx.LogInfo(string.Format(
                @"OSPREY_PASS2_QVALUE={0}: first-pass q-values are kept; only peaks moved by " +
                @"cross-run reconciliation get a new run-level q-value.",
                OspreyEnvironment.PASS2_QVALUE_TRANSFER));
            ctx.LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_PASS2_QVALUE, config.Pass2QValue));

            if (perFileParquetPaths.Count == 0 || config.InputFiles == null)
                return;

            var pass2Writer = new Pass2SidecarWriter(config, taskName, taskValidityKey);
            RequireWorkerAnswers(rescored.FileNames, pass2Writer);

            // LogInfo, not LogVerbose: this is the heading for the longest stretch of work left
            // in Stage 7, and a --verbose-only heading is invisible on the runs that actually take
            // the time (#4571).
            ctx.LogInfo(CountText.Format(rescored.FileCount, OspreyTasksResources.Pass2FdrSidecar_ComputeAndPersist_Computing_second_pass_FDR_scores_for_1_file_,
                OspreyTasksResources.Pass2FdrSidecar_ComputeAndPersist_Computing_second_pass_FDR_scores_for__0__files_));
            var swPass2 = Stopwatch.StartNew();
            ComputePass2TransferFold(ctx, rescored, config, pass2Writer);
            swPass2.Stop();
            ctx.LogInfo(LogTag.STAGE_WALL, @"second-pass-fdr: {0:F1}s",
                swPass2.Elapsed.TotalSeconds);

            // Every later fold of this stage reads the second-pass answers, and the pool has no
            // entries to put them on: they are dropped as each run is folded. The overlay is
            // installed as a per-run hook instead (InstallStreamedPass2Overlay, called by the
            // stage right after this method), so every later fold rebuilds a run and immediately
            // gets its second-pass values. RunProteinFdr's detected_peptides gate filters on
            // ExperimentPrecursorQvalue, which has to be the 2nd-pass value to match Rust
            // pipeline.rs:4480-4494's reload-then-second-pass-FDR sequence; without it
            // single-file --task SecondPassFDR runs include ~19 borderline peptides whose
            // 1st-pass q passes 1% and whose 2nd-pass q does not, a 1-protein delta in the
            // picked-protein output cross-impl.
        }

        /// <summary>
        /// Stop unless every run has PerFileRescoring's current answer on disk: second-pass
        /// records carrying the stamp of the reconciled parquet beside them
        /// (<see cref="HasWorkerStamp"/>).
        ///
        /// <para>Every run, named, before any is folded. Stage 7 used to recompute a run the
        /// worker had not answered; there is no recompute any more, so a missing answer can only
        /// be reported - and reporting it here, with the whole list, beats discovering one run at
        /// a time partway through a fold that has already spent minutes per run. A key with no
        /// matching input file is in the list too: its answer has nowhere to be.</para>
        /// </summary>
        private static void RequireWorkerAnswers(IReadOnlyList<string> fileNames, Pass2SidecarWriter writer)
        {
            var missing = new List<string>();
            foreach (string fileName in fileNames)
            {
                string inputFile = writer.InputFor(fileName);
                if (inputFile == null || !HasWorkerStamp(inputFile))
                    missing.Add(fileName);
            }
            if (missing.Count == 0)
                return;
            throw new InvalidOperationException(string.Format(
                OspreyTasksResources.Pass2FdrSidecar_RequireWorkerAnswers__0__of__1__runs_have_no_current_second_pass_results_file___3____which__4__writes_for_every_run_,
                missing.Count, fileNames.Count, string.Join(@", ", missing),
                @"." + FdrScoresSidecar.LABEL_SECOND_PASS + FdrScoresSidecar.EXT,
                PerFileRescoreTask.TASK_NAME, OspreyArgNames.TaskText(PerFileRescoreTask.TASK_NAME)));
        }

        /// <summary>
        /// Overlay every file's <c>.2nd-pass.fdr_scores.bin</c> back onto its survivors.
        ///
        /// <para>Called for two mutually exclusive reasons. BEFORE the write when this run did
        /// not recompute, so the unconditional write puts a resumed run's own second-pass
        /// values back rather than downgrading the file to the standing first-pass ones; and
        /// AFTER it when it did, because the paths that write their own sidecars during the
        /// score pass (the projection sink) leave the survivors untouched, and
        /// RunProteinFdr's detected-peptide gate filters on
        /// <c>ExperimentPrecursorQvalue</c>, which has to be the second-pass
        /// value to match Rust pipeline.rs:4480-4494's reload-then-second-pass-FDR sequence.
        /// Without the second one, single-file <c>--task SecondPassFDR</c> runs include ~19
        /// borderline peptides whose 1st-pass q passes 1% and whose 2nd-pass q does not,
        /// producing a 1-protein delta in the Stage 7 picked-protein output cross-impl.</para>
        ///
        /// <para>A file with no readable sidecar is reported, not skipped silently: every input
        /// file has one declared as an output of this task, so on the post-write pass an absent
        /// file means the write failed. On the pre-write pass a first run that has never
        /// written one is the one legitimate absence, and it is not a fault - the entries
        /// already hold the values that run is about to write.</para>
        /// </summary>
        /// <summary>
        /// The second pass's EXPERIMENT-scope records for this run, from whichever source holds
        /// them. Handed to the per-file overlay so each entry gets them only where that file's
        /// own 2nd-pass sidecar carries a record for it.
        ///
        /// <para>TWO sources, because there are two ways to arrive here. When a pass-2 path
        /// actually computed, it published a <see cref="Pass2ExperimentScope"/> and the values
        /// are in memory. When nothing was recomputed - a resume that adopted standing 2nd-pass
        /// sidecars - there is no accumulator, and they come off the 2nd-pass experiment sidecar
        /// the earlier run left on disk. Before the v5 split both cases were served by the same
        /// mechanism, because the values rode in the per-file sidecar this overlays; they no
        /// longer do, so the resume case needs its own read or the entries silently keep
        /// pre-competition values.</para>
        /// </summary>
        private static IReadOnlyDictionary<uint, FdrExperimentRecord> ResolvePass2ExperimentRecords(
            PipelineContext ctx)
        {
            return ctx.TryGet<Pass2ExperimentScope>(out var scope)
                ? scope.Accumulator.Records
                : LoadExperimentRecords(ctx.Config, FdrScoresSidecar.Pass.SecondPass);
        }

        /// <summary>
        /// A DEFERRED resolution of <see cref="ResolvePass2ExperimentRecords"/>, to be handed to
        /// every consumer that should share ONE answer.
        ///
        /// <para>Deferred because WHEN it resolves is a correctness question: Stage 7 crosses the
        /// no-scope / scope boundary partway through - protein FDR publishes and writes - so a
        /// consumer installed before that point must not capture the earlier answer. Shared
        /// because resolving it twice on a path that has no scope deserializes the whole
        /// analysis-wide sidecar twice and holds two copies of it; on the 446-run CHS cohort
        /// that is 1,239,178 records, about 100 MB, per copy.</para>
        ///
        /// <para>One instance per group of consumers that must agree, created by the caller, so
        /// how many times an analysis deserializes this file is a visible property of the call
        /// site rather than a hidden one. The pre-write and post-write reloads inside
        /// <see cref="ComputeAndPersist"/> each take their own, deliberately: they sit on
        /// opposite sides of that boundary and must NOT share.</para>
        /// </summary>
        internal static Lazy<IReadOnlyDictionary<uint, FdrExperimentRecord>>
            LazyPass2ExperimentRecords(PipelineContext ctx)
        {
            return new Lazy<IReadOnlyDictionary<uint, FdrExperimentRecord>>(
                () => ResolvePass2ExperimentRecords(ctx));
        }

        /// <summary>
        /// Overlay ONE run's <c>.2nd-pass.fdr_scores.bin</c> - plus the analysis-wide 2nd-pass
        /// experiment records - onto that run's entries. Stage 7 applies it once per run per
        /// pass: the entries are rebuilt from disk carrying their 1st-pass values, so without
        /// this every fold after the second pass would read pass-1 q-values off pass-2 rows.
        ///
        /// <para>A run with no current sidecar keeps EXACTLY the entries it arrived with - its
        /// first-pass state, freshly rebuilt - rather than having them reset; nothing here
        /// invents a value. A sidecar that is present but unreadable is warned about.</para>
        ///
        /// <para>The join index is the CALLER's, reused across files and cleared here. It used to
        /// be allocated per call and sized to the file
        /// (<c>new Dictionary&lt;uint, FdrEntry&gt;(entries.Count)</c>), which at cohort scale
        /// is a ~4.2 M-entry bucket and entry array on the large-object heap per file - and this
        /// overlay runs once per file per STREAMED PASS: three times over a 446-run cohort in
        /// SecondPassFDR alone. Clearing keeps the capacity of the largest file seen and
        /// allocates nothing after it.</para>
        /// </summary>
        private static void OverlayPass2SidecarOntoFile(
            Pass2SidecarWriter writer, string fileName, List<FdrEntry> entries,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords,
            Action<string> logWarning, Dictionary<uint, FdrEntry> byEntryId)
        {
            string inputFile = writer.InputFor(fileName);
            if (inputFile == null)
                return;
            string pass2Path = FdrScoresSidecar.Pass2Path(inputFile);
            if (!FdrScoresSidecar.IsCurrentFormat(pass2Path, FdrScoresSidecar.Pass.SecondPass))
                return;
            byEntryId.Clear();
            foreach (var e in entries)
                byEntryId[e.EntryId] = e;
            if (!FdrScoresSidecar.TryReadOverlay(
                    pass2Path, byEntryId, FdrScoresSidecar.Pass.SecondPass, experimentRecords))
            {
                logWarning(string.Format(
                    OspreyTasksResources.Pass2FdrSidecar_OverlayPass2SidecarOntoFile_Failed_to_reload_the_second_pass_intermediate_file_for___0_____1____protein_FDR_will_use_, fileName, pass2Path));
            }
        }

        /// <summary>
        /// Make every fold that runs AFTER the second pass see the second pass's answer, on a
        /// Stage 7 whose runs are rebuilt from disk one at a time (#4486).
        ///
        /// <para>The pool has no entries to stamp between passes, so the overlay is installed as
        /// a per-run hook and re-applied to each run as it is rebuilt - once per run per pass,
        /// which is the price of not holding the pool.</para>
        ///
        /// <para>The experiment records arrive as a DEFERRED resolution the caller owns - see
        /// <see cref="LazyPass2ExperimentRecords"/> for why resolving it once, late, and shared
        /// with the fold arm's other consumers is what this needs. Resolving it at install time
        /// on the join path would capture the records before the second-pass competition
        /// publishes its scope, freezing pre-competition values into every later fold.</para>
        /// </summary>
        internal static void InstallStreamedPass2Overlay(
            PipelineContext ctx, RescoredEntries rescored, string taskName, string taskValidityKey,
            Lazy<IReadOnlyDictionary<uint, FdrExperimentRecord>> experimentRecords)
        {
            var writer = new Pass2SidecarWriter(ctx.Config, taskName, taskValidityKey);
            // ONE join index per thread rather than per file: the cohort does not pay a
            // large-object dictionary per file per pass. Per thread, not one shared: StreamFiles
            // prepares several files at once on its lanes, each overlay on its own lane thread.
            var byEntryId = new ThreadLocal<Dictionary<uint, FdrEntry>>(() => new Dictionary<uint, FdrEntry>());
            rescored.AddPostMaterialize((fileName, entries) =>
                OverlayPass2SidecarOntoFile(
                    writer, fileName, entries, experimentRecords.Value, ctx.LogWarning, byEntryId.Value));
        }

        /// <summary>
        /// Read one pass's analysis-wide EXPERIMENT-scope records, keyed by entry_id (format v5,
        /// issue #4486). Returns an EMPTY map when the analysis names no such sidecar or never
        /// wrote one, so callers look up unconditionally and an absent entry takes the same
        /// defaults an entry that competed in nothing would carry.
        ///
        /// <para>A sidecar that EXISTS but cannot be read is a STOP, not an empty map. The two
        /// used to be the same answer, and that tolerance is how an HPC chain once ran to
        /// completion on wrong inputs: every consumer applies these values through a
        /// <c>TryGetValue</c>, so an empty map is indistinguishable from one that simply holds
        /// no matching entry, and every entry then keeps its <c>ResetScores</c> defaults - an
        /// <c>ExperimentAggregateScore</c> of 0.0 that <c>BuildCoAssignment</c> takes a MINIMUM
        /// over, collapsing a run-wide acceptance boundary. No return value makes a truncated,
        /// wrong-version or wrong-pass file safe, so the run fails instead.</para>
        /// </summary>
        private static IReadOnlyDictionary<uint, FdrExperimentRecord> LoadExperimentRecords(
            OspreyConfig config, FdrScoresSidecar.Pass pass)
        {
            string path = FdrExperimentSidecar.PathFor(config?.OutputBlib,
                ScoringTaskShared.ArtifactSiblingPath(config), pass);
            return LoadExperimentRecordsCached(path, pass);
        }

        /// <summary>
        /// One deserialization per GENERATION of the experiment sidecar, rather than one per
        /// call, keyed on the file's identity (path, length, last write) so a rewrite invalidates
        /// the cache on its own.
        ///
        /// <para>The caller that needs this is the streamed pass-2 overlay: it resolves the
        /// records inside a per-run hook, deliberately (see
        /// <see cref="InstallStreamedPass2Overlay"/> - Stage 7 crosses the
        /// no-scope-to-scope boundary partway through, so capturing once would freeze
        /// pre-competition values into every later fold). On a resume where the competition is
        /// skipped and no <c>Pass2ExperimentScope</c> is ever published, that per-call resolution
        /// falls through to disk for EVERY run of EVERY <c>StreamFiles</c> pass - and Stage 7
        /// makes many. At 446 runs that is O(runs x sidecar) deserialization of a file with 1.24 M
        /// records, which is the shape this whole area exists to eliminate.</para>
        ///
        /// <para>Caching on identity rather than hoisting the call is what keeps the documented
        /// semantics intact: protein FDR REWRITES this sidecar mid-Stage-7, and a rewrite changes
        /// length or write time, so the next resolve re-reads. A plain hoist would have frozen the
        /// pre-competition answer, which is the bug the per-call resolution was written to avoid.
        /// One <c>FileInfo</c> stat per call replaces one full deserialization per call.</para>
        /// </summary>
        private static readonly object EXPERIMENT_CACHE_LOCK = new object();
        private static string _experimentCacheKey;
        private static IReadOnlyDictionary<uint, FdrExperimentRecord> _experimentCacheValue;

        private static IReadOnlyDictionary<uint, FdrExperimentRecord> LoadExperimentRecordsCached(
            string path, FdrScoresSidecar.Pass pass)
        {
            string key = null;
            if (!string.IsNullOrEmpty(path))
            {
                var info = new FileInfo(path);
                // A missing file gets no cache entry: LoadExperimentRecordsFrom owns that
                // degrade, and caching "absent" would outlive the write that fixes it.
                if (info.Exists)
                {
                    key = string.Format(@"{0}|{1}|{2}|{3}", path, info.Length,
                        info.LastWriteTimeUtc.Ticks, (int)pass);
                }
            }
            if (key == null)
                return LoadExperimentRecordsFrom(path, pass);
            lock (EXPERIMENT_CACHE_LOCK)
            {
                if (string.Equals(_experimentCacheKey, key, StringComparison.Ordinal))
                    return _experimentCacheValue;
            }
            var loaded = LoadExperimentRecordsFrom(path, pass);
            lock (EXPERIMENT_CACHE_LOCK)
            {
                _experimentCacheKey = key;
                _experimentCacheValue = loaded;
            }
            return loaded;
        }

        /// <summary>
        /// Whether <paramref name="inputFile"/>'s 2nd-pass scores are the worker answer for the
        /// reconciled parquet beside them.
        /// Kept in one place so every reader of "is this run answered" asks the same question:
        /// PerFileRescoring's resume, SecondPassFDR's check before it folds, and the training
        /// export's choice of q-values.
        ///
        /// <para>Decided from DISK, not from a published byproduct (#4486). Stage 6 and Stage 7
        /// are separate PROCESSES in an HPC chain, so anything published in one is simply absent
        /// in the other.</para>
        ///
        /// <para>Every artifact carries the stamp of the <c>PerFileRescoring</c> run that wrote
        /// it, and they must carry the SAME one. Existence alone is not enough: an earlier run
        /// leaves the same files behind. The reconciled parquet is rewritten by every
        /// PerFileRescoring run whose key changed, so an answer left by an earlier run under
        /// another key no longer matches it. Comparing the stamps to each other, rather than to
        /// PerFileRescoring's key recomputed in this process, is what keeps the test valid on a
        /// separate HPC leg - a key may fold in a per-leg term, which a SecondPassFDR process
        /// cannot reconstruct.</para>
        ///
        /// <para>The run's <c>.reconciliation.json</c> is one of the set: PerFileRescoring plans
        /// the run and writes that plan just before rescoring it, under the same stamp. A plan
        /// left by an earlier
        /// run, or a kill between the plan and the parquet, leaves a run that is not done.</para>
        /// </summary>
        internal static bool HasWorkerStamp(string inputFile)
        {
            var reconciled = ParquetScoreCache.ReadStamp(ParquetScoreCache.GetReconciledScoresPath(inputFile));
            if (reconciled == null ||
                !string.Equals(reconciled.Task, PerFileRescoreTask.TASK_NAME, StringComparison.Ordinal) ||
                !string.Equals(reconciled.Version, OspreyVersion.Current, StringComparison.Ordinal))
            {
                return false;
            }
            if (!IsSameStamp(FdrScoresSidecar.ReadStamp(FdrScoresSidecar.Pass2Path(inputFile), FdrScoresSidecar.Pass.SecondPass), reconciled))
                return false;
            return IsSameStamp(ArtifactStamp.TryReadJsonHead(ReconciliationFile.PathForInput(inputFile)), reconciled);
        }

        private static bool IsSameStamp(ArtifactStamp stamp, ArtifactStamp other)
        {
            return stamp != null &&
                   string.Equals(stamp.Task, other.Task, StringComparison.Ordinal) &&
                   string.Equals(stamp.Version, other.Version, StringComparison.Ordinal) &&
                   string.Equals(stamp.Key, other.Key, StringComparison.Ordinal);
        }

        /// <summary>
        /// The analysis-wide 1st-pass experiment-scope records, for a caller outside this class
        /// that needs the same map on the same terms - today the rescore worker's transfer
        /// (issue #4486). Deliberately the SAME entry point Stage 7 uses rather than a second
        /// reader, so the two stages cannot come to different conclusions about a missing or
        /// unreadable sidecar.
        /// </summary>
        internal static IReadOnlyDictionary<uint, FdrExperimentRecord> LoadPass1ExperimentRecords(
            OspreyConfig config)
        {
            return LoadExperimentRecords(config, FdrScoresSidecar.Pass.FirstPass);
        }

        /// <summary>
        /// The path-taking half of <see cref="LoadExperimentRecords"/>, split out so the
        /// absent-versus-unreadable distinction is testable without standing up an
        /// <see cref="OspreyConfig"/> and an artifact tree around it.
        /// </summary>
        internal static IReadOnlyDictionary<uint, FdrExperimentRecord> LoadExperimentRecordsFrom(
            string path, FdrScoresSidecar.Pass pass)
        {
            // No path names no artifact, and no file on disk means this analysis never wrote
            // one. Both are "the analysis has none", which the callers' defaults cover. Testing
            // existence SEPARATELY is what makes them distinguishable from a file that is there
            // and unreadable: ReadMap answers null to all three, by design, because it cannot
            // know which of them its caller can tolerate.
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new Dictionary<uint, FdrExperimentRecord>();
            var map = FdrExperimentSidecar.ReadMap(path, pass);
            if (map == null)
            {
                // Not read as empty: that would leave every entry on its reset defaults (an
                // experiment aggregate score of 0, an experiment q of 1) and report those as
                // computed values (issue #4486).
                throw new InvalidOperationException(string.Format(
                    pass == FdrScoresSidecar.Pass.FirstPass
                        ? OspreyTasksResources.Pass2FdrSidecar_LoadExperimentRecordsFrom_The_whole_experiment_first_pass_intermediate_file_exists_but_could_not_be_read___0_
                        : OspreyTasksResources.Pass2FdrSidecar_LoadExperimentRecordsFrom_The_whole_experiment_second_pass_intermediate_file_exists_but_could_not_be_read___0_,
                    path));
            }
            return map;
        }

        /// <summary>
        /// Patch every file's <c>.2nd-pass.fdr_scores.bin</c> with the SECOND-pass
        /// <see cref="FdrEntry.ExperimentProteinQvalue"/>, so that column is a pass-2 value like
        /// every other column in that file (issue #4559).
        ///
        /// <para>Must be called AFTER the second-pass protein FDR has propagated onto the
        /// entries (<c>SecondPassFdrTask.RunProteinFdr</c>), which is necessarily after the
        /// sidecar itself is written - the sidecar write feeds that protein FDR. Hence a patch
        /// rather than a reordering: the same two-phase shape the first-pass path already uses,
        /// where the score pass writes a placeholder and
        /// <c>FdrScoresSidecar.PatchProteinQvalues</c> fills the column in once the protein FDR
        /// is known.</para>
        ///
        /// <para>Why the column was a pass-1 value before: no pass-2 q-value mode writes a
        /// protein q at all - the first- and second-pass protein FDRs are its only producers -
        /// so the value present at sidecar-write time was whatever pass 1 left on the stub. Both
        /// routes copied it identically, which is why no two-route comparison could see it; the
        /// guard that can is <c>Test-Pass2ProteinQvalue</c> in
        /// <c>Regression/FdrSidecars.ps1</c>.</para>
        ///
        /// <para>One file's map is resident at a time, released before the next. A file with no
        /// 2nd-pass sidecar is skipped silently: the sidecar exists only where Stage 6 produced
        /// a reconciled parquet, and the caller runs on the same condition.</para>
        /// </summary>
        internal static void WritePass2ExperimentSidecar(
            PipelineContext ctx,
            IReadOnlyList<string> fileNames,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            IReadOnlyDictionary<string, double> peptideQvalues,
            ArtifactStamp stamp)
        {
            var inputByName = new Dictionary<string, string>();
            foreach (var inputFile in ctx.Config.InputFiles)
                inputByName[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;

            // The three EXPERIMENT-scope columns the second pass already computed, handed over
            // by the pass that computed them. Absent only when no pass-2 path ran, in which case
            // there is nothing to write.
            if (!ctx.TryGet<Pass2ExperimentScope>(out var scope))
            {
                // ABSENCE IS A STOP, on every mode that computes a second pass. This used to
                // log-and-return, which is how OSPREY_PASS2_QVALUE=transfer silently produced no
                // experiment sidecar for as long as nothing ran that arm - a whole artifact
                // missing, reported at verbose level. Every second-pass path now publishes a
                // scope, so reaching here means one stopped doing so.
                //
                // This is the gate for that class of defect, and it costs nothing: it runs on
                // every leg of every dataset already in the suite, rather than needing an arm of
                // its own. --task ModelDiagnostics is excluded by its caller, whose contract is
                // that it touches no artifact but the report.
                throw new InvalidOperationException(
                    @"No second-pass experiment-scope records were published, so the analysis-wide " +
                    @"2nd-pass FDR sidecar cannot be written. The second pass must publish this, " +
                    @"from the carried pass-1 values. See issue #4486.");
            }
            var experiment = scope.Accumulator;

            int filesPatched = 0;
            long nPatched = 0;
            var failed = new List<string>();
            // A heading alone was not enough here: with only the caller's line in place this loop
            // was still a 54 s gap on the --task SecondPassFDR measurement of 2026-08-15. It
            // rewrites one 8-byte field per record over every file's whole 2nd-pass sidecar, so
            // it is per-file work and reports as such.
            int patchIdx = 0;
            ProgressReporter.Run(
                CountText.Format(fileNames.Count, OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Writing_protein_q_values_for_1_file,
                    OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Writing_protein_q_values_for__0__files),
                fileNames.Count, progress =>
            {
                foreach (string fileName in fileNames)
                {
                    progress.Report(++patchIdx);
                    if (!inputByName.TryGetValue(fileName, out string inputFile))
                        continue;
                    // ONE gate now. There is no longer a file that legitimately has no 2nd-pass
                    // sidecar - every input file gets one written - so absent and unusable are
                    // the same outcome here and both are reported. The two-gate form this
                    // replaces treated absence as a silent skip, which is precisely the
                    // ambiguity always-writing removes: an absent file could not be told apart
                    // from a write that failed and never committed.
                    if (!FdrScoresSidecar.IsCurrentFormat(
                            FdrScoresSidecar.Pass2Path(inputFile), FdrScoresSidecar.Pass.SecondPass))
                    {
                        failed.Add(fileName);
                        continue;
                    }

                    // entry_id -> protein q, built from the RECONCILED parquet's own
                    // modified_sequence column rather than from the survivor entries. That
                    // column is where an entry's ModifiedSequence came from in the first place,
                    // so the peptide -> q lookup matches by construction - and it is what lets
                    // the write-back happen without the pool (#4486). The map is a SUPERSET of
                    // the sidecar's records, which the patch tolerates: it rewrites only the
                    // records the file holds. A peptide absent from the parsimony result takes
                    // 1.0, exactly as the resident PropagateProteinQvalues did.
                    var byEntryId = new Dictionary<uint, double>();
                    try
                    {
                        // The RECONCILED parquet, derived rather than probed. This runs both
                        // in-process - where the published map holds Stage 4 paths, because
                        // Stage 1-4 ran here - and on a --task SecondPassFDR node, where it
                        // already holds reconciled ones; the derivation is idempotent, so one
                        // expression states the same intent on both routes.
                        string parquetPath = ParquetScoreCache.ReconciledPathFromScoresPath(
                            perFileParquetPaths[fileName]);
                        ParquetScoreCache.ReadFdrStubScalars(parquetPath,
                            (entryId, charge, isDecoy, coelutionSum, modseq, apexRt) =>
                            {
                                double q;
                                if (!peptideQvalues.TryGetValue(modseq ?? string.Empty, out q))
                                    q = 1.0;
                                byEntryId[entryId] = q;
                            },
                            StubColumns.Core, ScoringTaskShared.StubIdentityFor(ctx));
                    }
                    catch (Exception ex)
                    {
                        ctx.LogWarning(string.Format(
                            OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Could_not_read_the_re_scored_results_for__0____1_,
                            fileName, ex.Message));
                        failed.Add(fileName);
                        continue;
                    }

                    foreach (var kvp in byEntryId)
                        experiment.SetProteinQvalue(kvp.Key, kvp.Value);
                    filesPatched++;
                    nPatched += byEntryId.Count;
                }
            }, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS);

            // Reported, not thrown on: nothing in this process reads the 2nd-pass sidecar's
            // protein column, so a failed patch cannot corrupt this run's output. It DOES leave
            // a file whose protein column is not a pass-2 value while its header says pass 2,
            // which is precisely the state #4559 existed to remove - so the warning names that
            // rather than implying the file is merely missing an optional extra.
            if (failed.Count > 0)
            {
                // Each record keeps what it held when the sidecar was written: the reset default
                // for every entry Stage 6 rescored or gap-filled, a pass-1 value only for entries
                // Stage 6 left alone. Any consumer joining on that column reads the wrong pass.
                ctx.LogWarning(string.Format(
                    OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Protein_q_values_could_not_be_written_for__0__files____1____The_second_pass_intermediate_,
                    failed.Count, string.Join(@", ", failed)));
            }
            ctx.LogVerbose(CountText.Format(filesPatched,
                OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Resolved_the_second_pass_protein_q_values_of__1__precursor_candidate_peaks_in_1_file_,
                OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Resolved_the_second_pass_protein_q_values_of__1__precursor_candidate_peaks_across__0__,
                nPatched));

            // The experiment-scope record set is complete now that protein FDR has filled the
            // one column it owns, so write it once beside the blib.
            string experimentPath = FdrExperimentSidecar.PathFor(ctx.Config?.OutputBlib,
                ScoringTaskShared.ArtifactSiblingPath(ctx.Config), FdrScoresSidecar.Pass.SecondPass);
            if (string.IsNullOrEmpty(experimentPath))
            {
                ctx.LogWarning(
                    string.Format(OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_No_output__blib_was_given__so_the_second_pass_experiment_level_q_values_are_not_saved_to_,
                        LibrarySource.EXT_BLIB));
                return;
            }
            try
            {
                FdrExperimentSidecar.Write(experimentPath, experiment.Records,
                    FdrScoresSidecar.Pass.SecondPass, stamp);
                ctx.LogVerbose(string.Format(
                    OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Wrote_experiment_level_FDR_results_for__1__precursor_candidates_to__0_,
                    experimentPath, experiment.Count));
            }
            catch (Exception ex)
            {
                ctx.LogWarning(string.Format(
                    OspreyTasksResources.Pass2FdrSidecar_WritePass2ExperimentSidecar_Failed_to_write__0____1_, experimentPath, ex.Message));
            }
        }

        /// <summary>
        /// The second pass's EXPERIMENT-scope records, published by whichever pass-2 path
        /// computed them and consumed by <see cref="WritePass2ExperimentSidecar"/> after the
        /// second-pass protein FDR has filled in the one column it owns.
        ///
        /// <para>A context byproduct rather than a return value because the two halves run in
        /// different places: the q-values come out of the second pass, the protein q out of the
        /// protein FDR the owning task runs afterwards, and the file cannot be written until
        /// both are in.</para>
        /// </summary>
        internal sealed class Pass2ExperimentScope
        {
            public Pass2ExperimentScope(FdrExperimentAccumulator accumulator)
            {
                Accumulator = accumulator;
            }

            public FdrExperimentAccumulator Accumulator { get; }
        }

        /// <summary>
        /// OSPREY_PASS2_QVALUE=transfer's JOIN: fold the per-run answers the rescore worker
        /// wrote into the analysis-wide experiment scope (#4665).
        ///
        /// <para>The per-file half - re-mapping each run's run q through its own 1st-pass
        /// score-to-q table - ran in <see cref="Pass2PerFileWorker"/>. What is left is genuinely
        /// experiment-wide (<see cref="FoldAndPublishExperimentScope"/>): each record takes its precursor's
        /// pass-1 experiment values, carried rather than recomputed because the transfer never
        /// re-competes, and the best-of-runs floors come out of the run q-values the records
        /// hold. Both used to run over the resident pool after the per-file half, which is why
        /// this mode alone held every run's survivors for the whole of Stage 7 - ~4.4 GB plus
        /// ~0.197 GB per run, 92.3 GB predicted against 91.1 GB measured at 446 runs.</para>
        ///
        /// <para>The analysis-wide 1st-pass experiment sidecar is required, not defaulted: an
        /// absent one would carry every precursor forward at q = 1.0 and drop it from the
        /// output.</para>
        /// </summary>
        private static void ComputePass2TransferFold(PipelineContext ctx, RescoredEntries rescored,
            OspreyConfig config, Pass2SidecarWriter writer)
        {
            RequireTransferExperimentSidecar(config);
            var pass1Experiment = LoadExperimentRecords(config, FdrScoresSidecar.Pass.FirstPass);
            // Say whether this run verified, ALWAYS: a
            // silent flag is indistinguishable from one that stopped reaching the child process,
            // and regression.ps1 and SubsetPipelineTest assert this line to prove the straight leg
            // verified and the HPC chain did not.
            bool verify = OspreyEnvironment.Pass2VerifyWorker;
            if (verify)
            {
                ctx.LogInfo(string.Format(
                    @"Second-pass worker verification ACTIVE (OSPREY_PASS2_VERIFY_WORKER): " +
                    @"re-scoring and re-transferring {0} file(s) to assert the worker's answer. This " +
                    @"re-reads each run's model, features and first-pass scores; it is a test " +
                    @"instrument and is off by default.",
                    rescored.FileCount));
            }
            ctx.LogInfo(LogTag.PATH, LogKey.Format(LogKey.ROUTE_SECOND_PASS_FOLD, @"verify={0} runs={1}",
                verify ? @"on" : @"off", rescored.FileCount));
            if (verify)
                VerifyTransferWorkerAnswers(ctx, rescored.FileNames, config, pass1Experiment);
            var floors = new ExperimentQFloors();
            // The precursors the blib reports, from the first-pass experiment q-values this
            // fold carries forward: only they may set a reported peptide's floor.
            var reported = new ExperimentOutputSet(config.FdrLevel, config.ExperimentFdr);
            WalkSurvivors(rescored, floors, entries =>
            {
                foreach (var e in entries)
                {
                    if (!pass1Experiment.TryGetValue(e.EntryId, out var q1))
                        continue;
                    reported.Add(e.EntryId, e.IsDecoy, e.ModifiedSequence, e.Charge,
                        q1.ExperimentPrecursorQvalue, q1.EffectiveExperimentQvalue(config.FdrLevel));
                }
            });
            reported.Finish(out _, out var reportedBaseIds);
            var reportedTargets = new HashSet<uint>();
            foreach (var baseId in reportedBaseIds)
                reportedTargets.Add(baseId.Item1);
            FoldAndPublishExperimentScope(ctx, writer, rescored.FileNames, floors,
                rec => CarryPass1Experiment(rec.EntryId, pass1Experiment), reportedTargets.Contains);
        }

        /// <summary>
        /// OSPREY_PASS2_VERIFY_WORKER under the transfer: recompute each run's second-pass answer
        /// from the files the rescore worker computed it from, and assert that the run's reconciled
        /// stub carries it bit for bit. One run at a time: nothing is kept between runs.
        /// </summary>
        private static void VerifyTransferWorkerAnswers(PipelineContext ctx, IEnumerable<string> fileNames,
            OspreyConfig config, IReadOnlyDictionary<uint, FdrExperimentRecord> pass1Experiment)
        {
            var inputByFileName = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string input in config.InputFiles)
                inputByFileName[Path.GetFileNameWithoutExtension(input) ?? string.Empty] = input;
            var features = PinFeatureMask.For(config);
            foreach (string fileName in fileNames)
            {
                if (!inputByFileName.TryGetValue(fileName, out string inputFile))
                {
                    // Exception text for internal diagnostics, not displayed to user
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass worker verification: '{0}' is not among this run's inputs.", fileName));
                }
                var verified = VerifyTransferFile(fileName, inputFile, features, pass1Experiment, ctx.LogWarning);
                ctx.LogInfo(LogTag.COUNT, string.Format(
                    @"Second-pass worker verified for {0}: {1} rows, {2} re-scored ({3} at their first-pass peak)",
                    fileName, verified.Rows, verified.Rescored, verified.RescoredInPlace));
            }
        }

        /// <summary>
        /// Recompute one run's second-pass answer and assert its reconciled stub against it, as
        /// <c>PerFileRescoreTask.WriteRunModelReconciled</c> computed it: a row the rescore
        /// re-scored takes the run's own frozen model's calibrated score of its features in
        /// <c>&lt;stem&gt;.features-reconciled.parquet</c>, every other row keeps its first-pass
        /// score from the run's stub, and the run q-values are the transfer of those scores through
        /// the run's first-pass table (<see cref="TransferOneFileByScore"/>).
        ///
        /// <para>Which rows were re-scored is read from the files, since the worker's in-memory mark
        /// (a feature vector on the entry) is not written down. A gap-fill row has a score_index past
        /// the stub's rows. A row the rescore did not touch is the stub's row copied verbatim, so
        /// one whose peak differs from its stub row in any bit was re-scored at new boundaries. A
        /// row whose peak matches its stub row's bit for bit is either unmoved or a rescore target
        /// whose new peak is its first-pass peak - measured on the StellarLibDecoy and Astral
        /// regression data, with the same features too - so it must carry one of the two scores
        /// those give: its first-pass score, or the run model's score of its features. Every other
        /// row has exactly one right answer.</para>
        ///
        /// <para>Throws <see cref="InvalidOperationException"/> naming the file, the first entry_id
        /// that differs, and both values. Returns the rows verified, how many were re-scored, and
        /// how many of those were re-scored at their first-pass peak.</para>
        /// </summary>
        internal static (int Rows, int Rescored, int RescoredInPlace) VerifyTransferFile(string fileName, string inputFile, PinFeatureSelection features,
            IReadOnlyDictionary<uint, FdrExperimentRecord> pass1Experiment, Action<string> logWarning)
        {
            string scoresPath = ParquetScoreCache.GetScoresPath(inputFile);
            string reconciledPath = ParquetScoreCache.GetReconciledScoresPath(inputFile);
            string featuresPath = ParquetScoreCache.GetReconciledFeaturesPath(inputFile);

            // The worker's answer: every survivor's second-pass score and run q-values.
            var answers = new List<ParquetScoreCache.StubScoreRow>();
            if (!ParquetScoreCache.ReadStubScoreRows(reconciledPath, null, answers.Add, out _))
                throw VerifyInputMissing(fileName, reconciledPath);
            var answeredRows = new HashSet<uint>();
            foreach (var answer in answers)
                answeredRows.Add(answer.ScoreIndex);
            var firstPassRows = new Dictionary<uint, ParquetScoreCache.StubScoreRow>(answers.Count);
            if (!ParquetScoreCache.ReadStubScoreRows(scoresPath, answeredRows.Contains,
                    row => firstPassRows[row.ScoreIndex] = row, out int firstPassRowCount))
            {
                throw VerifyInputMissing(fileName, scoresPath);
            }
            if (!File.Exists(featuresPath))
                throw VerifyInputMissing(fileName, featuresPath);
            var featuresByScoreIndex = LoadReconciledFeaturesByScoreIndex(featuresPath, features);
            var runScorer = RunModelScorer.Load(FirstPassModelIO.RunModelPathFor(inputFile), scoresPath, features);

            var recomputed = new List<FdrEntry>(answers.Count);
            int rescoredRows = 0, rescoredInPlace = 0;
            foreach (var answer in answers)
            {
                double score;
                var firstPass = default(ParquetScoreCache.StubScoreRow);
                bool samePeak = answer.ScoreIndex < firstPassRowCount &&
                                firstPassRows.TryGetValue(answer.ScoreIndex, out firstPass) &&
                                answer.SamePeakAs(firstPass);
                if (samePeak && SameBits(firstPass.Score, answer.Score))
                {
                    score = firstPass.Score;
                }
                else if (featuresByScoreIndex.TryGetValue(answer.ScoreIndex, out double[] rowFeatures))
                {
                    // Re-scored at new boundaries - or at its own: a rescore target whose new peak is
                    // its first-pass peak bit for bit, which the files cannot tell from an unmoved
                    // row, and whose score is then the run model's rather than the first pass's.
                    score = runScorer.Score(answer.EntryId, rowFeatures);
                    rescoredRows++;
                    if (samePeak)
                        rescoredInPlace++;
                }
                else
                {
                    // Exception text for internal diagnostics, not displayed to user
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass worker verification failed for '{0}': entry_id {1} (score_index {2}) " +
                        @"was re-scored, and {3} holds no features for it.",
                        fileName, answer.EntryId, answer.ScoreIndex, featuresPath));
                }
                AssertVerifiedValue(fileName, reconciledPath, answer, @"score", score, answer.Score);
                recomputed.Add(new FdrEntry { EntryId = answer.EntryId, ParquetIndex = answer.ScoreIndex, Score = score });
            }

            var tally = new TransferTally();
            TransferOneFileByScore(fileName, FdrScoresSidecar.Pass1Path(inputFile), recomputed, e => e.Score,
                pass1Experiment, logWarning, ref tally);
            if (tally.FilesDone != 1)
                throw VerifyInputMissing(fileName, FdrScoresSidecar.Pass1Path(inputFile));
            for (int i = 0; i < answers.Count; i++)
            {
                AssertVerifiedValue(fileName, reconciledPath, answers[i], @"run precursor q-value",
                    recomputed[i].RunPrecursorQvalue, answers[i].RunPrecursorQvalue);
                AssertVerifiedValue(fileName, reconciledPath, answers[i], @"run peptide q-value",
                    recomputed[i].RunPeptideQvalue, answers[i].RunPeptideQvalue);
            }
            return (answers.Count, rescoredRows, rescoredInPlace);
        }

        private static bool SameBits(double a, double b)
        {
            return BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
        }

        private static InvalidOperationException VerifyInputMissing(string fileName, string path)
        {
            // Exception text for internal diagnostics, not displayed to user
            return new InvalidOperationException(string.Format(
                @"Second-pass worker verification for '{0}' could not read {1}. OSPREY_PASS2_VERIFY_WORKER " +
                @"needs every file the rescore worker computed its answer from.", fileName, path));
        }

        private static void AssertVerifiedValue(string fileName, string reconciledPath,
            ParquetScoreCache.StubScoreRow answer, string what, double recomputed, double written)
        {
            if (SameBits(recomputed, written))
                return;
            // Exception text for internal diagnostics, not displayed to user
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                @"Second-pass worker verification failed for '{0}': entry_id {1} (score_index {2}) has " +
                @"{3} {4:R} in {5}, and the recompute gives {6:R}.",
                fileName, answer.EntryId, answer.ScoreIndex, what, written, reconciledPath, recomputed));
        }

        /// <summary>
        /// Stop unless the analysis-wide 1st-pass experiment sidecar the transfer carries forward
        /// is on disk. Required, not defaulted: an absent one would carry every precursor forward
        /// at q = 1.0 and drop it from the output, and its loader reads absence as an empty map.
        /// Shared by both halves of the transfer - the rescore worker and the join - so they
        /// refuse the same state with the same words.
        /// </summary>
        internal static void RequireTransferExperimentSidecar(OspreyConfig config)
        {
            string path = FdrExperimentSidecar.PathFor(config?.OutputBlib,
                ScoringTaskShared.ArtifactSiblingPath(config), FdrScoresSidecar.Pass.FirstPass);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return;
            throw new InvalidOperationException(string.Format(
                OspreyTasksResources.Pass2FdrSidecar_RequireTransferExperimentSidecar_Second_pass_FDR_needs_the_whole_experiment_first_pass_intermediate_file__0___which_is_missing__Run__1__first_,
                path ?? FdrExperimentSidecar.EXT, OspreyArgNames.TaskText(FirstPassFdrTask.TASK_NAME)));
        }

        /// <summary>
        /// One entry_id's experiment-scope record under the transfer: its pass-1 experiment
        /// values, carried verbatim, with the protein q left at 1.0 for the second-pass protein
        /// FDR to fill in. A precursor with no pass-1 record never competed: q = 1.0, aggregate
        /// 0.0, both PEPs 1.0 - the values <see cref="AssignPerRunQ"/> gives its entries.
        /// </summary>
        private static FdrExperimentRecord CarryPass1Experiment(uint entryId,
            IReadOnlyDictionary<uint, FdrExperimentRecord> pass1Experiment)
        {
            if (!pass1Experiment.TryGetValue(entryId, out var q1))
                return new FdrExperimentRecord(entryId, 1.0, 1.0, 1.0, 0.0, 1.0, 1.0);
            return new FdrExperimentRecord(entryId, q1.ExperimentPrecursorQvalue,
                q1.ExperimentPeptideQvalue, 1.0, q1.ExperimentAggregateScore, q1.Pep, q1.PeptidePep);
        }

        /// <summary>
        /// Walk every run's survivors once, recording each entry_id's peptide identity for the
        /// best-of-runs PEPTIDE floor.
        ///
        /// <para>The identities are recorded HERE because this is the one walk that sees
        /// entries - the floors themselves come from the per-file 2nd-pass records, which carry
        /// run q but no sequence - and because the entries are the only route-independent source
        /// of them. LibraryById is NOT: a --task SecondPassFDR node loads a library with no
        /// generated decoys, so resolving a decoy entry_id there answers on the straight route
        /// and returns nothing on the distributed one (measured on Stellar: 166,680 of 333,404
        /// records differing, every one a decoy).</para>
        ///
        /// <para>Folded over the files one at a time and each dropped, so this walk does not
        /// build the pool (#4486). Reported because it walks EVERY survivor observation -
        /// 89,068,375 of them on the 82-file SEA-AD run - before anything downstream logs a
        /// word; it sat inside a 195 s silence that read as a hung run at the very end of a
        /// multi-hour search.</para>
        /// </summary>
        /// <param name="rescored">The survivor source.</param>
        /// <param name="floors">Receives the peptide identities.</param>
        /// <param name="observe">Sees each run's survivors as they are walked.</param>
        private static void WalkSurvivors(RescoredEntries rescored, ExperimentQFloors floors,
            Action<IReadOnlyList<FdrEntry>> observe)
        {
            int fileCount = rescored.FileCount;
            ProgressReporter.Run(
                CountText.Format(fileCount, OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Collecting_second_pass_precursor_candidates_from_1_file,
                    OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Collecting_second_pass_precursor_candidates_from__0__files),
                fileCount, mergeProgress =>
            {
                int mergeIdx = 0;
                foreach (var kvp in rescored.StreamFiles())
                {
                    mergeProgress.Report(++mergeIdx);
                    floors.ObserveIdentities(kvp.Value);
                    observe(kvp.Value);
                }
            }, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS);
        }

        /// <summary>
        /// Fold every run's per-run 2nd-pass records into the analysis-wide experiment scope,
        /// raise it to the best-of-runs floors, and publish it for the protein-FDR step - the
        /// last step of the second pass.
        ///
        /// <para>Over the SIDECARS, not the entries: each file's records carry the entry_id and
        /// run q this needs. READ-ONLY over them. This loop used to finish by rewriting each
        /// one's pep column (PatchPep), which is what made a per-file sidecar mutable; PEP is now
        /// stored once, as a winner fact on the experiment record.</para>
        ///
        /// <para><paramref name="finishRecord"/> gives the experiment-scope values a record
        /// takes.</para>
        ///
        /// <para>The peptide floors are derived from the entry floors just folded - no per-run
        /// data and no second pass over anything - and both halves are stamped onto the records
        /// before anyone sees them, which is what makes this file's q-values FINAL rather than a
        /// value the pipeline overrides afterwards (issue #4522).</para>
        ///
        /// <para><paramref name="reportedTarget"/> restricts which target entries set
        /// a peptide's floor (<see cref="ExperimentQFloors.DerivePeptideFloors"/>).</para>
        /// </summary>
        private static void FoldAndPublishExperimentScope(
            PipelineContext ctx, Pass2SidecarWriter writer, IReadOnlyList<string> fileKeys,
            ExperimentQFloors floors, Func<FdrScoreRecord, FdrExperimentRecord> finishRecord,
            Func<uint, bool> reportedTarget)
        {
            var unpatched = new List<string>();
            var experiment = new FdrExperimentAccumulator();
            ProgressReporter.Run(
                CountText.Format(fileKeys.Count, OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Writing_experiment_level_q_values_for_1_file,
                    OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Writing_experiment_level_q_values_for__0__files),
                fileKeys.Count, patchProgress =>
            {
                int patchIdx = 0;
                foreach (string fileKey in fileKeys)
                {
                    patchProgress.Report(++patchIdx);
                    string inputFile = writer.InputFor(fileKey);
                    if (inputFile == null)
                        continue;
                    string pass2Path = FdrScoresSidecar.Pass2Path(inputFile);
                    // Staged, then applied: ReadRecords can return false AFTER invoking the
                    // callback, so accumulating experiment values as they arrive would leave the
                    // analysis-wide record half-built from a file that then failed.
                    var staged = new List<FdrExperimentRecord>();
                    if (!FdrScoresSidecar.ReadRecords(pass2Path, FdrScoresSidecar.Pass.SecondPass,
                            rec =>
                            {
                                floors.Observe(rec.EntryId,
                                    rec.RunPrecursorQvalue, rec.RunPeptideQvalue);
                                staged.Add(finishRecord(rec));
                            }))
                    {
                        unpatched.Add(fileKey);
                        continue;
                    }
                    foreach (var exp in staged)
                    {
                        experiment.Add(exp.EntryId, exp.ExperimentPrecursorQvalue,
                            exp.ExperimentPeptideQvalue, exp.ExperimentProteinQvalue,
                            exp.ExperimentAggregateScore, exp.Pep, exp.PeptidePep);
                    }
                }
            }, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS);
            floors.DerivePeptideFloors(reportedTarget);
            int raised = experiment.ApplyRunQFloors(entryId => floors.FloorsFor(entryId));
            ctx.LogInfo(string.Format(
                OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Raised__0__of__1__experiment_level_precursor_candidate_q_values_to_their_best_run_level_,
                raised, experiment.Count));

            // Handed to the protein-FDR step, which fills the one column it owns and writes the
            // 2nd-pass experiment sidecar. Published rather than returned because the protein
            // FDR runs in the owning task after this method returns.
            ctx.Publish(new Pass2ExperimentScope(experiment));
            if (unpatched.Count > 0)
            {
                // Hard, not a warning. A run whose records could not be read contributed nothing
                // to the experiment q, PEP and aggregate - a q-value a consumer would reasonably
                // trust and could not audit. The protein FDR that runs next gates on
                // ExperimentPrecursorQvalue, so continuing means reporting a protein set computed
                // from unfinished numbers.
                throw new IOException(string.Format(
                    OspreyTasksResources.Pass2FdrSidecar_ComputePass2TransferCompeteFull_Second_pass_experiment_level_q_values_could_not_be_written_for__0__files____1____Stopping_,
                    unpatched.Count, string.Join(@", ", unpatched)));
            }
        }

        /// <summary>
        /// Load the reconciled parquet's 21-PIN feature rows keyed by each row's
        /// stable identity (entry_id, charge, scan_number). The Stage 6 reconciled
        /// parquet is re-sorted and re-indexed by <c>ParquetScoreCache.WriteScoresParquet</c>
        /// -- the appended gap-fill rows interleave into the (entry_id, charge,
        /// scan_number) sort order -- so a post-compaction stub's
        /// <see cref="FdrEntry.ParquetIndex"/> (assigned against the ORIGINAL Stage
        /// 4 parquet, or carried on the in-memory buffer through rescore) no longer
        /// addresses that stub's own row in the reconciled parquet. Identity is
        /// invariant across the reindex, so <see cref="MapFeaturesByScoreIndex"/>
        /// keys on it. Reads the lean stub columns + the PIN feature columns (no
        /// heavy fragment/XIC/CWT blobs), one file at a time, so the reload stays
        /// within the issue #4355 memory bound. (issue #4355)
        /// </summary>
        /// <summary>
        /// Assert that a per-run 2nd-pass sidecar about to be written describes the POOL its
        /// file's Stage 6 parquet defines - one record per row - and throw naming both counts
        /// if it does not.
        ///
        /// <para><b>The invariant this protects.</b> The per-run 2nd-pass sidecar is the only
        /// thing a separate experiment-wide node receives about a run. Its population is not the
        /// writer's to choose: the join node fixes it, writing the cross-run consensus from the
        /// traversal of the whole population that each run's plan is made from, and
        /// <c>ReconciledParquetWriter</c> stamps the parquet Stage 6 derives from it with the
        /// join-wide reconciliation hash. Every node emitting the same rows is what makes an
        /// HPC split equal a single-machine run. Nothing checked it.</para>
        ///
        /// <para><b>Why it is worth a per-file parquet footer read.</b> The omission it was
        /// written for (594 gap-fill observations across 3 Stellar files) passed every other
        /// gate the project has - golden blib, 2nd-pass protein q, resume, HPC-chain route
        /// independence, warm re-run and library-fragment release - and surfaced only as two
        /// moved numbers in a diagnostics panel. A silently short artifact is exactly the shape
        /// that reads as correct downstream: the entries it omits keep <c>ResetScores</c>'
        /// defaults, so they report as never having competed rather than as missing. The probe
        /// decodes no column data, so the cost is a footer per file.</para>
        ///
        /// <para>Skipped when the effective parquet is not a reconciled SURVIVORS parquet: with
        /// no reconciled sibling the caller falls back to the Stage 4 file, whose rows are the
        /// whole pre-compaction population and are not this artifact's population.</para>
        /// </summary>
        internal static void AssertSidecarDescribesPool(
            string fileName, string effectiveParquetPath, IReadOnlyList<FdrScoreRecord> records)
        {
            var pool = ParquetScoreCache.ProbePoolPopulation(effectiveParquetPath);
            if (!pool.IsReconciledSurvivors)
                return;
            if (pool.RowCount != records.Count)
            {
                throw new InvalidOperationException(string.Format(
                    @"Second-pass per-file competition wrote {0} record(s) for '{1}', but its Stage 6 " +
                    @"pool holds {2} (from {3}). The per-run 2nd-pass sidecar must describe that pool " +
                    @"one record per row - it is all a separate experiment-wide node receives about " +
                    @"this run, and its population is fixed by the join, not chosen by the writer. " +
                    @"See issue #4486.",
                    records.Count, fileName, pool.RowCount, effectiveParquetPath));
            }
            AssertRecordsMatchPoolSequence(fileName, effectiveParquetPath, records,
                ParquetScoreCache.StreamEntryIds(effectiveParquetPath));
        }

        /// <summary>
        /// The per-run 2nd-pass sidecar's record SEQUENCE must equal the reconciled parquet's row
        /// sequence, position for position - not merely its population or its length.
        ///
        /// <para><b>Why a count is not enough.</b> A count cannot see a PERMUTATION, and a
        /// permutation is exactly what this file acquired: the rescore task APPENDS gap-fill
        /// entries to its in-memory pool list, while
        /// <see cref="ParquetScoreCache.StreamReconciledScoresParquet"/> merges each one into its
        /// canonical <c>(entry_id, charge, scan_number)</c> position, so writing in list order
        /// emitted the same rows with the gap-fills in a trailing block. Every count-based check
        /// passed. The reconciled parquet is the authority here rather than the writer's
        /// convenience: its population and order are fixed by the JOIN, stamped with a join-wide
        /// reconciliation hash, and the parquet writer itself hard-fails a row out of canonical
        /// order - so a sidecar that disagrees is the thing that is wrong.</para>
        ///
        /// <para><b>Why it matters beyond byte-identity with a baseline.</b> A consumer that
        /// reduces the records to a per-precursor best takes the FIRST record at the maximum, so
        /// which observation represents a precursor depends on the order. A baseline comparison
        /// catches a reordering today and will not exist for the next dataset.</para>
        ///
        /// <para>Pure, and takes the pool sequence as an <see cref="IEnumerable{T}"/>, so the
        /// comparison is unit-testable without writing a parquet and stays O(1) in memory over a
        /// streamed column.</para>
        /// </summary>
        internal static void AssertRecordsMatchPoolSequence(
            string fileName, string effectiveParquetPath,
            IReadOnlyList<FdrScoreRecord> records, IEnumerable<uint> poolEntryIds)
        {
            int i = 0;
            foreach (uint poolEntryId in poolEntryIds)
            {
                if (i >= records.Count)
                {
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass per-file competition for '{0}': its Stage 6 pool has more " +
                        @"rows than the {1} record(s) written, starting at row {2} (from {3}). " +
                        @"See issue #4486.",
                        fileName, records.Count, i, effectiveParquetPath));
                }
                if (records[i].EntryId != poolEntryId)
                {
                    throw new InvalidOperationException(string.Format(
                        @"Second-pass per-file competition for '{0}': record {1} is entry_id {2}, " +
                        @"but its Stage 6 pool holds entry_id {3} at that row (from {4}). The " +
                        @"per-run 2nd-pass sidecar must describe the pool in the pool's own order " +
                        @"- the experiment fold takes the FIRST observation at a per-base_id " +
                        @"maximum, so a reordering silently changes which observation represents " +
                        @"a precursor. See issue #4486.",
                        fileName, i, records[i].EntryId, poolEntryId, effectiveParquetPath));
                }
                i++;
            }
            if (i == records.Count)
                return;
            throw new InvalidOperationException(string.Format(
                @"Second-pass per-file competition for '{0}': wrote {1} record(s) but its Stage 6 " +
                @"pool yielded only {2} row(s) (from {3}). See issue #4486.",
                fileName, records.Count, i, effectiveParquetPath));
        }

        /// <summary>
        /// One run's protein-best column (<c>--protein-boost best</c>), read from beside its
        /// first-pass FDR scores file, for a frozen model that scores with it; null for a model
        /// that does not, which is every run with the column off.
        ///
        /// <para>Absence is a stop: a model with the column cannot score a vector without it, and
        /// a run scored at the floor everywhere would be a plausible, wrong second pass. With
        /// <paramref name="expectedStamp"/> (the frozen model's), a column written by another
        /// first pass is refused as well.</para>
        /// </summary>
        internal static ProteinBestRunValues LoadProteinBestForRun(FrozenModelScorer scorer,
            string pass1SidecarPath, string fileName, ArtifactStamp expectedStamp)
        {
            if (!scorer.ScoresProteinBest)
                return null;
            string path = ProteinBestSidecar.PathBesidePass1(pass1SidecarPath);
            var column = ProteinBestSidecar.Read(path);
            bool current = column != null;
            if (current && expectedStamp != null)
            {
                var stamp = ProteinBestSidecar.ReadStamp(path);
                current = stamp != null && string.Equals(stamp.ToString(), expectedStamp.ToString(), StringComparison.Ordinal);
            }
            if (!current)
            {
                throw new InvalidDataException(string.Format(
                    OspreyTasksResources.Pass2FdrSidecar_LoadProteinBestForRun_The_protein_best_column_file__0__of__1__is_missing_or_was_not_written_by_this_analysis_s_first_pass__Run__2__again_,
                    path, fileName, OspreyArgNames.TaskText(FirstPassFdrTask.TASK_NAME)));
            }
            return new ProteinBestRunValues(column.EntryIds, column.Values, column.Floor);
        }

        internal static Dictionary<uint, double[]> LoadReconciledFeaturesByScoreIndex(
            string reconciledPath, PinFeatureSelection features)
        {
            var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(reconciledPath);
            var featRows = ParquetScoreCache.LoadPinFeaturesFromParquet(reconciledPath, features);
            int n = Math.Min(stubs.Count, featRows.Count);
            var map = new Dictionary<uint, double[]>(n);
            for (int i = 0; i < n; i++)
            {
                if (stubs[i].ParquetIndex.HasValue)
                    map[stubs[i].ParquetIndex.Value] = featRows[i];
            }
            return map;
        }

        /// <summary>
        /// Overlay re-scored PIN features onto <paramref name="entries"/> by each
        /// entry's stable identity (entry_id, charge, scan_number), skipping any
        /// entry whose identity is absent from <paramref name="featByScoreIndex"/> (a
        /// stub/parquet mismatch). Returns the number of entries whose
        /// <see cref="FdrEntry.Features"/> were assigned; the caller compares it
        /// against the entry count to detect and report a mismatch. Identity (not
        /// <see cref="FdrEntry.ParquetIndex"/>) is used because the reconciled
        /// parquet is re-indexed relative to the compacted stubs -- see
        /// <see cref="LoadReconciledFeaturesByScoreIndex"/>. Pure: no I/O, no logging.
        /// </summary>
        internal static int MapFeaturesByScoreIndex(
            IReadOnlyList<FdrEntry> entries,
            IReadOnlyDictionary<uint, double[]> featByScoreIndex)
        {
            int nMapped = 0;
            foreach (var entry in entries)
            {
                if (entry.ParquetIndex.HasValue &&
                    featByScoreIndex.TryGetValue(entry.ParquetIndex.Value, out double[] features))
                {
                    entry.Features = features;
                    nMapped++;
                }
            }
            return nMapped;
        }

        /// <summary>Counts one <see cref="TransferOneFile"/> call adds to. A struct rather
        /// than six ref parameters, so the per-file body could be lifted out of the whole-run
        /// loop without its signature becoming the reason not to.</summary>
        internal struct TransferTally
        {
            public int Unchanged, Moved, GapFill, Skipped, MissingSidecar, FilesDone;
        }

        /// <summary>
        /// Transfer ONE run's per-run q-values: build that run's own score->q tables from its own
        /// <c>.1st-pass.fdr_scores.bin</c>, then classify and re-map its survivors.
        ///
        /// <para>Nothing in it reads another run's state - the tables come from this run's
        /// sidecar and the experiment records are the analysis-wide map every run shares - which
        /// is what makes the mode a fan-out computation. It ran in the join, over the whole pool,
        /// until #4665 moved its caller to <see cref="Pass2PerFileWorker"/>; #4438 established
        /// the per-run form.</para>
        ///
        /// <para>Classifies every survivor by its reconciled feature score against its 1st-pass
        /// sidecar record - UNCHANGED (bit-exact score match: carry the 1st-pass record), MOVED
        /// (re-map run q through this file's tables) or GAP-FILL (no record: run q from the
        /// tables) - and gives each the precursor's pass-1 experiment values; see
        /// <see cref="AssignPerRunQ"/>. The sidecar Score is the averaged-model score, the SAME
        /// scale <see cref="FrozenModelScorer.Score(double[])"/> produces, so each table is
        /// scale-consistent by construction.</para>
        ///
        /// <para>Scores through <see cref="FrozenModelScorer"/>, so it applies whichever
        /// classifier the first pass trained. The transfer used to average the fold weights
        /// itself and inline the dot product, which threw on a gradient-boosted-tree model (no
        /// weights to average). The scorer reuses one buffer, so a caller running files in parallel needs
        /// one scorer per thread.</para>
        /// </summary>
        internal static void TransferOneFile(
            string fileName, string pass1Path, List<FdrEntry> survivors,
            FrozenModelScorer scorer, ProteinBestRunValues proteinBest,
            IReadOnlyDictionary<uint, FdrExperimentRecord> globalExperiment,
            Action<string> logWarning, ref TransferTally tally)
        {
            int nFeatures = scorer.InputFeatures;
            TransferOneFileByScore(fileName, pass1Path, survivors,
                // No reconciled features resolved (a stub/parquet mismatch the reload already
                // warned about): the entry is skipped and keeps its q as-is.
                entry => entry.Features == null || entry.Features.Length != nFeatures
                    ? null
                    : scorer.Score(entry.Features, entry.EntryId, proteinBest),
                globalExperiment, logWarning, ref tally);
        }

        /// <summary>
        /// <see cref="TransferOneFile"/>
        /// with each survivor's second-pass score from <paramref name="scoreOf"/>, null for a
        /// survivor that has none and keeps its q-values. Under per-run first-pass models a
        /// survivor's score is the one its reconciled parquet carries: the first-pass score of a
        /// row that did not move, which then matches its first-pass record bit for bit, and the
        /// run's own frozen model's calibrated score of a row that did.
        /// </summary>
        internal static void TransferOneFileByScore(
            string fileName, string pass1Path, List<FdrEntry> survivors,
            Func<FdrEntry, double?> scoreOf,
            IReadOnlyDictionary<uint, FdrExperimentRecord> globalExperiment,
            Action<string> logWarning, ref TransferTally tally)
        {
            // Build this file's per-run tables + record map from its own 1st-pass sidecar.
            var firstPassByEntryId = new Dictionary<uint, FdrScoreRecord>();
            var precScores = new List<double>();
            var precQs = new List<double>();
            var pepScores = new List<double>();
            var pepQs = new List<double>();
            bool ok = FdrScoresSidecar.ReadRecords(
                pass1Path, FdrScoresSidecar.Pass.FirstPass, rec =>
            {
                firstPassByEntryId[rec.EntryId] = rec; // entry_id is unique per file (DeduplicatePairs)
                precScores.Add(rec.Score);
                precQs.Add(rec.RunPrecursorQvalue);
                pepScores.Add(rec.Score);
                pepQs.Add(rec.RunPeptideQvalue);
            });
            if (!ok || precScores.Count == 0)
            {
                tally.MissingSidecar++;
                logWarning(string.Format(
                    @"OSPREY_PASS2_QVALUE=transfer: could not read the first-pass intermediate file " +
                    @"for '{0}' ({1}); this file's run-level q-values are left unadjusted.", fileName, pass1Path));
                return;
            }
            BuildScoreToQTable(precScores, precQs, out double[] precScoresDesc, out double[] precQDesc);
            BuildScoreToQTable(pepScores, pepQs, out double[] pepScoresDesc, out double[] pepQDesc);
            // OSPREY_DUMP_RUN_PEP (experiment): this run's own score-to-PEP curve from its first-pass
            // competition, read at each survivor's final score - the per-replicate PEP of a peak.
            PepEstimator runPep = OspreyEnvironment.RunPepDumpDir != null ? FitRunPep(firstPassByEntryId.Values) : null;
            var runPepRows = runPep != null ? new List<string>(survivors.Count) : null;

            foreach (var entry in survivors)
            {
                double? scored = scoreOf(entry);
                if (!scored.HasValue)
                {
                    // No second-pass score resolved. Leave this ENTRY's q as-is rather than guess,
                    // and go on to the next one - a `return` here would abandon the rest of the
                    // file's survivors at their Stage-6 q AND skip the FilesDone count below,
                    // which is a whole run silently dropped for one entry's missing features.
                    tally.Skipped++;
                    continue;
                }
                double newScore = scored.Value;

                FdrScoreRecord? rec1 = null;
                if (firstPassByEntryId.TryGetValue(entry.EntryId, out FdrScoreRecord recFound))
                    rec1 = recFound;
                // The precursor's analysis-wide pass-1 experiment record, which supplies
                // every disposition: an UNCHANGED or MOVED peak carries these values through,
                // and a gap-fill peak (no 1st-pass run-scope record) takes them so
                // the experiment-q clamp - a floor that only raises - lands it at the
                // precursor's best-run q. A precursor with no record anywhere gets the
                // default 1.0 q-values and a 0.0 aggregate, which pair correctly: never
                // competed, never accepted, so nothing reads it.
                FdrExperimentRecord? exp1 = null;
                if (globalExperiment.TryGetValue(entry.EntryId, out var expFound))
                    exp1 = expFound;
                var perRunClass = AssignPerRunQ(entry, newScore, rec1, exp1,
                    precScoresDesc, precQDesc, pepScoresDesc, pepQDesc);
                runPepRows?.Add(RunPepRow(entry, perRunClass, runPep));
                switch (perRunClass)
                {
                    case PerRunClass.Unchanged: tally.Unchanged++; break;
                    case PerRunClass.Moved: tally.Moved++; break;
                    default: tally.GapFill++; break;
                }
            }
            if (runPepRows != null)
            {
                WriteRunPepRows(fileName, runPepRows);
                WriteRunPepCurve(fileName, runPep, precScoresDesc);
            }
            // Counted only HERE, where the whole-run loop counted it: the unreadable-sidecar
            // path above returns first, so a run whose sidecar could not be read was never one
            // this pass finished.
            tally.FilesDone++;
        }

        /// <summary>
        /// The run's posterior error probability as a function of score: <see cref="PepEstimator"/>
        /// fitted on the run's first-pass target-decoy competition (<see cref="CompeteRunPepWinners"/>).
        /// </summary>
        private static PepEstimator FitRunPep(IEnumerable<FdrScoreRecord> records)
        {
            var winners = CompeteRunPepWinners(records);
            return PepEstimator.FitDefault(winners.Scores, winners.IsDecoyWinner);
        }

        /// <summary>
        /// The run's target-decoy competition: one winner per base id, the higher-scoring of the
        /// target and its paired decoy, the decoy on a tie as in first-pass competition
        /// (<c>TargetDecoyCompetition.CompeteFromDicts</c>). In base-id order, because the KDE
        /// sum the PEP fit runs is order-sensitive.
        /// </summary>
        internal static (double[] Scores, bool[] IsDecoyWinner) CompeteRunPepWinners(IEnumerable<FdrScoreRecord> records)
        {
            var best = new SortedDictionary<uint, (double Target, double Decoy)>();
            foreach (var rec in records)
            {
                uint baseId = rec.EntryId & ~LibraryEntry.DECOY_ID_BIT;
                bool isDecoy = (rec.EntryId & LibraryEntry.DECOY_ID_BIT) != 0;
                var pair = best.TryGetValue(baseId, out var found) ? found : (double.NaN, double.NaN);
                if (isDecoy)
                    pair.Item2 = double.IsNaN(pair.Item2) ? rec.Score : Math.Max(pair.Item2, rec.Score);
                else
                    pair.Item1 = double.IsNaN(pair.Item1) ? rec.Score : Math.Max(pair.Item1, rec.Score);
                best[baseId] = pair;
            }
            var scores = new List<double>(best.Count);
            var isDecoyWinner = new List<bool>(best.Count);
            foreach (var pair in best.Values)
            {
                bool decoyWins = double.IsNaN(pair.Target) || (!double.IsNaN(pair.Decoy) && pair.Decoy >= pair.Target);
                scores.Add(decoyWins ? pair.Decoy : pair.Target);
                isDecoyWinner.Add(decoyWins);
            }
            return (scores.ToArray(), isDecoyWinner.ToArray());
        }

        private static string RunPepRow(FdrEntry entry, PerRunClass perRunClass, PepEstimator runPep)
        {
            return string.Join(TextUtil.SEPARATOR_TSV, entry.ModifiedSequence, entry.Charge.ToString(CultureInfo.InvariantCulture),
                entry.IsDecoy ? @"1" : @"0", perRunClass.ToString(),
                entry.Score.ToString(@"R", CultureInfo.InvariantCulture),
                entry.RunPrecursorQvalue.ToString(@"R", CultureInfo.InvariantCulture),
                runPep.PosteriorError(entry.Score).ToString(@"R", CultureInfo.InvariantCulture));
        }

        private static void WriteRunPepRows(string fileName, List<string> rows)
        {
            Directory.CreateDirectory(OspreyEnvironment.RunPepDumpDir);
            string path = Path.Combine(OspreyEnvironment.RunPepDumpDir,
                Path.GetFileNameWithoutExtension(fileName) + @".run_pep.tsv");
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine(string.Join(TextUtil.SEPARATOR_TSV,
                    @"modified_sequence", @"charge", @"is_decoy", @"class", @"score", @"run_q", @"run_pep"));
                foreach (string row in rows)
                    writer.WriteLine(row);
            }
        }

        /// <summary>
        /// The run's whole score-to-PEP curve, on a grid of 2,000 scores across the run's first-pass score
        /// range, so the PEP of every first-pass peak (not only the compaction survivors) can be read off it.
        /// </summary>
        private static void WriteRunPepCurve(string fileName, PepEstimator runPep, double[] scoresDesc)
        {
            if (scoresDesc.Length == 0)
                return;
            double max = scoresDesc[0];
            double min = scoresDesc[scoresDesc.Length - 1];
            const int points = 2000;
            string path = Path.Combine(OspreyEnvironment.RunPepDumpDir,
                Path.GetFileNameWithoutExtension(fileName) + @".run_pep_curve.tsv");
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine(string.Join(TextUtil.SEPARATOR_TSV, @"score", @"run_pep"));
                for (int i = 0; i < points; i++)
                {
                    double score = min + (max - min) * i / (points - 1);
                    writer.WriteLine(string.Join(TextUtil.SEPARATOR_TSV, score.ToString(@"R", CultureInfo.InvariantCulture),
                        runPep.PosteriorError(score).ToString(@"R", CultureInfo.InvariantCulture)));
                }
            }
        }

        /// <summary>How a survivor was classified against its 1st-pass sidecar record.</summary>
        internal enum PerRunClass
        {
            /// <summary>Reconciliation did not move the peak (recomputed score == the sidecar's).</summary>
            Unchanged,
            /// <summary>Reconciliation moved the peak to a different position (score differs).</summary>
            Moved,
            /// <summary>A new detection with no 1st-pass record (gap-fill).</summary>
            GapFill,
        }

        /// <summary>
        /// Assign one survivor's pass-2 q-values per the per-run-only invariant and return its
        /// classification. Pure (no I/O): the caller supplies the recomputed frozen-model score
        /// (<paramref name="newScore"/>), the entry's 1st-pass sidecar record
        /// (<paramref name="firstPass"/>, null for a gap-fill), that file's per-run lookup tables,
        /// and the precursor's analysis-wide pass-1 experiment record
        /// (<paramref name="firstPassExperiment"/>). The experiment values are NEVER derived from
        /// a table -- they are the pass-1 carry, frozen by the best-peak anchor, and every
        /// disposition takes them from the same place:
        /// <list type="bullet">
        /// <item>UNCHANGED (<paramref name="newScore"/> == the record's Score, bit-exact): carry the
        /// 1st-pass run-scope record verbatim.</item>
        /// <item>MOVED: run q re-mapped from the tables; PEP carried from the record.</item>
        /// <item>GAP-FILL (no run-scope record): run q from the tables.</item>
        /// </list>
        /// </summary>
        internal static PerRunClass AssignPerRunQ(
            FdrEntry entry,
            double newScore,
            FdrScoreRecord? firstPass,
            FdrExperimentRecord? firstPassExperiment,
            double[] precScoresDesc,
            double[] precQDesc,
            double[] pepScoresDesc,
            double[] pepQDesc)
        {
            // The EXPERIMENT-scope half is one record per entry_id for the whole analysis
            // (format v5, issue #4486), so every disposition below reads the same three values -
            // there is no longer a per-run copy for an unchanged peak to prefer over the
            // cross-file one a gap-fill peak fell back to. An entry with no record never
            // competed: q = 1.0, aggregate = 0.0.
            double expPrecQ = firstPassExperiment?.ExperimentPrecursorQvalue ?? 1.0;
            double expPepQ = firstPassExperiment?.ExperimentPeptideQvalue ?? 1.0;
            double expAgg = firstPassExperiment?.ExperimentAggregateScore ?? 0.0;
            // PEP rides with the other experiment-scope values: one per entry_id, from the
            // analysis-wide record rather than from any run's own file (issue #4486).
            double expPep = firstPassExperiment?.Pep ?? 1.0;
            double expPeptidePep = firstPassExperiment?.PeptidePep ?? 1.0;
            if (firstPass.HasValue)
            {
                FdrScoreRecord rec1 = firstPass.Value;
                // Bit-exact equality is the reliable MOVED discriminator: an UNCHANGED survivor's
                // reconciled features ARE its original Stage-4 features (ReconciledParquetWriter
                // streams unchanged rows through untouched), and the sidecar Score was computed from
                // those same parquet features with this same averaged model -- so the recomputation
                // is bit-identical. A MOVED peak carries rescored features, so its score differs.
                if (newScore == rec1.Score)
                {
                    entry.Score = rec1.Score;
                    entry.RunPrecursorQvalue = rec1.RunPrecursorQvalue;
                    entry.RunPeptideQvalue = rec1.RunPeptideQvalue;
                    entry.ExperimentPrecursorQvalue = expPrecQ;
                    entry.ExperimentPeptideQvalue = expPepQ;
                    entry.Pep = expPep;
                    entry.ExperimentPeptidePep = expPeptidePep;
                    entry.ExperimentAggregateScore = expAgg;
                    return PerRunClass.Unchanged;
                }
                entry.Score = newScore;
                entry.RunPrecursorQvalue = LookupQForScore(newScore, precScoresDesc, precQDesc);
                entry.RunPeptideQvalue = LookupQForScore(newScore, pepScoresDesc, pepQDesc);
                // Experiment q is a pass-1 property (best-peak anchor) -- carry it, never re-map.
                entry.ExperimentPrecursorQvalue = expPrecQ;
                entry.ExperimentPeptideQvalue = expPepQ;
                entry.Pep = expPep;
                entry.ExperimentPeptidePep = expPeptidePep;
                // Carried with the experiment q for the same reason, and NOT re-derived from
                // newScore: it is the score that pass-1 experiment q was computed from, so
                // re-mapping it to the rescored value would break the pairing that is the
                // whole point of persisting it.
                entry.ExperimentAggregateScore = expAgg;
                return PerRunClass.Moved;
            }
            entry.Score = newScore;
            entry.RunPrecursorQvalue = LookupQForScore(newScore, precScoresDesc, precQDesc);
            entry.RunPeptideQvalue = LookupQForScore(newScore, pepScoresDesc, pepQDesc);
            entry.ExperimentPrecursorQvalue = expPrecQ;
            entry.ExperimentPeptideQvalue = expPepQ;
            // Carried for the same reason as the experiment q beside it, and from the same
            // record: the aggregate is a per-entry roll-up, so a gap-fill is entitled to it even
            // with no run-scope record of its own. Leaving it at ResetScores' 0.0 would persist
            // a real experiment q next to a score that q was not computed from, and a
            // score-space acceptance boundary built from the 2nd-pass artifacts would then be
            // drawn from the wrong ranking.
            entry.ExperimentAggregateScore = expAgg;
            return PerRunClass.GapFill;
        }

        /// <summary>Number of equal-count score-quantile bins
        /// <see cref="BuildScoreToQTable"/> smooths the per-entry q into. Large enough to
        /// trace the FDR curve finely, small enough that each bin averages out the
        /// per-entry q noise from the raw-vs-calibrated score scale mismatch.</summary>
        private const int SCORE_Q_TABLE_BINS = 1000;

        /// <summary>
        /// Build the score-&gt;q lookup table from parallel (score, q) lists (the raw
        /// averaged-model score paired with the unbiased 1st-pass effective q). A calibrated
        /// q is monotone NON-INCREASING in score, but the per-entry pairs are not
        /// individually monotone (the stored 1st-pass q was computed on the per-fold
        /// calibrated CV score, a different scale from this raw averaged-model score), so a
        /// running-min/max envelope would collapse to the global extreme on one outlier.
        /// Instead: (1) sort by score ascending; (2) partition into
        /// <see cref="SCORE_Q_TABLE_BINS"/> equal-count quantile bins and take each bin's
        /// MEAN q; (3) run pool-adjacent-violators (isotonic regression) so q is
        /// non-decreasing as score decreases. Emits parallel arrays:
        /// <paramref name="scoresDesc"/> (bin score, descending) and <paramref name="qDesc"/>
        /// (isotonic bin-mean q, non-decreasing as score decreases).
        /// </summary>
        internal static void BuildScoreToQTable(
            IReadOnlyList<double> scores,
            IReadOnlyList<double> qs,
            out double[] scoresDesc,
            out double[] qDesc)
        {
            int nPts = scores.Count;
            var order = new int[nPts];
            for (int i = 0; i < nPts; i++)
                order[i] = i;
            // Sort indices by score ASCENDING (ties by q ascending, deterministic).
            Array.Sort(order, (a, b) => // Array.Sort OK: quantile-bin means are tie-order-insensitive, and this table feeds only the OSPREY_PASS2_QVALUE=transfer path (never cross-impl parity output)
            {
                int c = scores[a].CompareTo(scores[b]);
                if (c != 0)
                    return c;
                return qs[a].CompareTo(qs[b]);
            });

            int nBins = Math.Min(SCORE_Q_TABLE_BINS, nPts);
            var binScoreAsc = new double[nBins];   // representative (max) score in bin
            var binQAsc = new double[nBins];        // mean q in bin
            for (int b = 0; b < nBins; b++)
            {
                // Equal-count partition of the ascending-sorted points.
                int start = (int)((long)b * nPts / nBins);
                int end = (int)((long)(b + 1) * nPts / nBins);
                if (end <= start)
                    end = start + 1;
                double qSum = 0.0;
                double maxScore = double.NegativeInfinity;
                for (int k = start; k < end; k++)
                {
                    int idx = order[k];
                    qSum += qs[idx];
                    if (scores[idx] > maxScore)
                        maxScore = scores[idx];
                }
                binScoreAsc[b] = maxScore;
                binQAsc[b] = qSum / (end - start);
            }

            // Pool-adjacent-violators (isotonic regression) over the ascending-score bins to
            // force q NON-INCREASING as score increases. Blocks are stored low-score-first;
            // blockW[j] counts bins from the low-score end.
            var blockQ = new double[nBins];
            var blockW = new int[nBins];
            int nBlocks = 0;
            for (int b = 0; b < nBins; b++)
            {
                double q = binQAsc[b];
                int w = 1;
                while (nBlocks > 0 && blockQ[nBlocks - 1] < q)
                {
                    double pooledSum = blockQ[nBlocks - 1] * blockW[nBlocks - 1] + q * w;
                    w += blockW[nBlocks - 1];
                    q = pooledSum / w;
                    nBlocks--;
                }
                blockQ[nBlocks] = q;
                blockW[nBlocks] = w;
                nBlocks++;
            }
            // Expand blocks back to per-bin isotonic q (low-score-first).
            var binQIso = new double[nBins];
            int fillLo = 0;
            for (int j = 0; j < nBlocks; j++)
            {
                for (int c = 0; c < blockW[j]; c++)
                {
                    binQIso[fillLo] = blockQ[j];
                    fillLo++;
                }
            }

            // Emit descending-by-score (highest score first) for LookupQForScore.
            scoresDesc = new double[nBins];
            qDesc = new double[nBins];
            for (int b = 0; b < nBins; b++)
            {
                scoresDesc[b] = binScoreAsc[nBins - 1 - b];
                qDesc[b] = binQIso[nBins - 1 - b];
            }
        }

        /// <summary>
        /// Map a score to a q via the score-&gt;q table built by
        /// <see cref="BuildScoreToQTable"/>. Binary search for the deepest table entry whose
        /// score is still &gt;= the query score and return its q; clamp at both ends (a score
        /// above the table max gets the table's minimum q; a score below the table min gets
        /// the maximum q).
        /// </summary>
        internal static double LookupQForScore(
            double score, double[] scoresDesc, double[] qDesc)
        {
            int n = scoresDesc.Length;
            if (n == 0)
                return 1.0;
            // scoresDesc is descending; qDesc is non-decreasing along it. A score above the
            // best table score is the most confident -> the minimum q at qDesc[0]; a score
            // below the worst table score is the least confident -> the maximum q at qDesc[n-1].
            if (score > scoresDesc[0])
                return qDesc[0];
            if (score <= scoresDesc[n - 1])
                return qDesc[n - 1];
            // Largest index i such that scoresDesc[i] >= score (deepest table position still
            // at least as good as the query); qDesc non-decreasing -> most conservative q.
            int lo = 0, hi = n - 1, best = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (scoresDesc[mid] >= score)
                {
                    best = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return qDesc[best];
        }

        /// <summary>
        /// Resolves each per-file key to its input file, and carries the stamp the verifier's
        /// diagnostic dump embeds.
        ///
        /// <para>It wrote every per-run <c>.2nd-pass.fdr_scores.bin</c> once - from three paths,
        /// which is why it existed. SecondPassFDR writes none now (#4665): PerFileRescoring
        /// writes every one.</para>
        /// </summary>
        private sealed class Pass2SidecarWriter
        {
            private readonly ArtifactStamp _stamp;
            private readonly Dictionary<string, string> _inputByFileName =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public Pass2SidecarWriter(OspreyConfig config, string taskName, string taskValidityKey)
            {
                _stamp = OspreyVersion.Stamp(taskName, taskValidityKey);
                if (config.InputFiles == null)
                    return;
                foreach (string inputFile in config.InputFiles)
                    _inputByFileName[Path.GetFileNameWithoutExtension(inputFile)] = inputFile;
            }

            /// <summary>The validity stamp the verifier's diagnostic dump embeds.</summary>
            public ArtifactStamp Stamp => _stamp;

            /// <summary>
            /// The input file a per-file key names, or null when no <c>config.InputFiles</c>
            /// entry matches it - a name drift between Stage 5 and Stage 7, which
            /// <see cref="RequireWorkerAnswers"/> refuses before anything is folded.
            /// </summary>
            public string InputFor(string fileName)
            {
                return _inputByFileName.TryGetValue(fileName, out string inputFile) ? inputFile : null;
            }
        }
    }
}
