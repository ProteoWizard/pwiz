/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.IO;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.ModelDiagnostics;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks.ModelDiagnostics
{
    /// <summary>
    /// Recovers the FIRST-PASS detection apex RT the peak co-assignment panel needs (issue #4522)
    /// and builds the pass-1 panel from it.
    ///
    /// <para>The panel wants the true per-run detection RT: pre-compaction, before Stage 6 moves
    /// any peak, so it measures scoring and peak assignment rather than reconciliation. That pool
    /// is never resident - the lean first pass carries no RT at all by design
    /// (<c>FdrProjection</c> is 32 bytes and every RT/bounds field is reload-obtained for the
    /// small survivor set after compaction, issue #4355).</para>
    ///
    /// <para>It comes off the <c>.1st-pass.fdr_scores.bin</c> sidecar, which carries
    /// <c>apex_rt</c> beside the score and the two run q-values from format v7 (issue #4522). So
    /// this source reads ONE artifact per file and joins nothing.</para>
    ///
    /// <para>It used to read two. The sidecar has no RT column before v7, so the panel opened
    /// each file's <c>.scores.parquet</c> a second time for the <c>apex_rt</c> column and matched
    /// it to the sidecar BY POSITION, asserting the alignment on entry_id per row because nothing
    /// in either format recorded the contract it relied on. Measured on the 446-run CHS cohort,
    /// that column read was 29 MB per file of large-object allocation against 4 MB for everything
    /// else this panel does, and it took the process from 10 to 26 GB of private bytes over the
    /// ten minutes the join ran. Moving the column into the sidecar costs 8 bytes per record of
    /// sequential IO and removes the read, the join and the assertion together - a column a
    /// consumer has to reconstruct by inference is a column in the wrong file.</para>
    ///
    /// <para>Bounded: one sidecar record at a time, nothing per file. Precursor identity and m/z
    /// come from the library by entry id, so no string column is materialized for the whole
    /// pre-compaction pool.</para>
    /// </summary>
    public static class PeakCoAssignmentSource
    {
        /// <summary>
        /// Build the pass-1 co-assignment panel, or return null (with a log line explaining which
        /// input was missing) when the apex RT source cannot be reconstructed. Never throws: a
        /// diagnostics-only artifact must not be able to take down a real run.
        /// </summary>
        /// <param name="fileNames">Input-file names in input order, as the report labels runs.</param>
        /// <param name="perFileParquetPaths">File name to its <c>.scores.parquet</c>.</param>
        /// <param name="config">Run configuration; supplies the FDR level and run FDR gating detection.</param>
        /// <param name="classByBaseId">Library base-id to entrapment class, as the rest of the report uses.</param>
        /// <param name="libraryById">
        /// Searched library by FULL entry id, supplying each row's precursor m/z and its
        /// modified sequence + charge (the precursor identity), so no parquet string column is read.
        /// </param>
        /// <param name="experimentRecords">
        /// The analysis-wide 1st-pass EXPERIMENT-scope records by entry_id, supplying each row's
        /// experiment q-values and the aggregate score the acceptance boundary is drawn from.
        /// Supplied by the caller because the score-pass path holds them in memory before the
        /// sidecar is written, while the rehydrate path reads one an earlier run left.
        /// </param>
        /// <param name="log">Run log sink; every degrade path explains itself through it.</param>
        public static ModelDiagnosticsData.CoAssignmentData Build(
            IReadOnlyList<string> fileNames,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords,
            IOspreyLog log)
        {
            // The "never throws" promise above needs an actual guard, and it was only ever
            // enforced around the parquet read inside AddFile. Everything else - the builder's
            // own misuse guards, the per-file sort and histogram writes in FlushFile, Build's
            // reduction, and the caller's library resolution - ran unprotected, and the callers
            // in FirstPassFdrTask invoke this OUTSIDE the report writer's try/catch. A panel
            // that aborts a ten-hour search is a worse outcome than a panel that is missing.
            try
            {
                return BuildCore(fileNames, perFileParquetPaths, config, classByBaseId,
                    libraryById, experimentRecords, log);
            }
            catch (Exception ex)
            {
                log.LogInfo(LogTag.MODEL_DIAGNOSTICS, string.Format(
                    @"peak co-assignment abandoned after an unexpected error: {0}",
                    ex.Message));
                return null;
            }
        }

        private static ModelDiagnosticsData.CoAssignmentData BuildCore(
            IReadOnlyList<string> fileNames,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords,
            IOspreyLog log)
        {
            if (fileNames == null || perFileParquetPaths == null || libraryById == null)
            {
                log.LogInfo(LogTag.MODEL_DIAGNOSTICS, @"peak co-assignment skipped: no per-file parquet or library available.");
                return null;
            }

            // The analysis-wide EXPERIMENT-scope records (format v5, issue #4486), supplied by
            // the caller rather than read here, because the two callers hold them in different
            // places: the score-pass path has them in memory BEFORE the sidecar is written, and
            // the rehydrate path reads a sidecar an earlier run left. Reading the file here
            // silently produced an EMPTY map on the score-pass path, for that ordering reason.
            //
            // This panel cannot proceed without them, and must not substitute a default for a
            // missing one: the experiment aggregate is a signed discriminant, so a fabricated
            // 0.0 is an ordinary mid-distribution score rather than an obvious absence, and the
            // run cutoff below is a MINIMUM over accepted precursors' aggregates. Zeros drag it
            // to 0.0 and admit the entire decoy pool - measured on StellarLibDecoy when this
            // defaulted: decoys 272 -> 483,220 and the cutoff 0.669 -> 0. See the remarks on
            // StreamingFdr.ExperimentAggregateScore, which chose double? over an in-band
            // sentinel for exactly this failure.
            if (experimentRecords == null || experimentRecords.Count == 0)
            {
                log.LogInfo(LogTag.MODEL_DIAGNOSTICS, @"peak co-assignment skipped: no 1st-pass experiment-scope FDR records were supplied, and their aggregate scores are what the acceptance boundary is drawn from.");
                return null;
            }

            var sw = Stopwatch.StartNew();
            var runNames = new string[fileNames.Count];
            for (int f = 0; f < fileNames.Count; f++)
                runNames[f] = fileNames[f];
            var builder = new ModelDiagnosticsData.CoAssignmentPassBuilder(runNames, 1, false);
            // Size the builder's per-file working set once, for the whole panel. Every record
            // phase 1 folds is gated on having an experiment-scope record, so the largest base
            // id in that map bounds every index the builder will see (issue #4657).
            // Logged because it is not instant and nothing else speaks until the reporter below
            // opens: this walks every experiment-scope key (6.2 M on the 446-run cohort) and then
            // NaN-fills ~100 MB. The two loops under it carry reporters for exactly this reason -
            // a silent stretch here reads as a hung run.
            log.LogInfo(LogTag.MODEL_DIAGNOSTICS, string.Format(
                @"peak co-assignment: sizing the run scope from {0} experiment-scope record(s)...",
                experimentRecords.Count));
            uint maxBaseId = 0;
            foreach (uint entryId in experimentRecords.Keys)
                maxBaseId = Math.Max(maxBaseId, entryId & BASE_ID_MASK);
            builder.ReserveRunScope(maxBaseId);

            // Phase 1: the decoy score cutoffs, from the sidecars alone (score + q + entry id -
            // no parquet, no library, no allocation per row). Decoys have no meaningful q of
            // their own, so the population that belongs beside the accepted targets is the one
            // scoring at least as well as the worst accepted target/entrapment precursor.
            //
            // Reported for the same reason the apex-RT join below is: this loop streams EVERY
            // record of every 1st-pass sidecar and it ran silent. On the 82-file SEA-AD run of
            // 2026-08-12 that was a 138 s gap ending at the experiment boundary line, with
            // managed memory falling 23.0 -> 12.5 GB across it - real work, but indistinguishable
            // from a hang for over two minutes. The join below already had a ProgressReporter;
            // it is scoped to the per-file pass AFTER this one, so it did not cover the
            // expensive half.
            using (var scanProgress = new ProgressReporter(
                string.Format(@"Peak co-assignment: scanning 1st-pass sidecars over {0} file(s)", fileNames.Count),
                fileNames.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                for (int f = 0; f < fileNames.Count; f++)
                {
                    scanProgress.Report(f + 1);
                    string sidecar = ResolveSidecarPath(fileNames[f], perFileParquetPaths, config);
                    if (sidecar == null)
                        continue;   // phase 2 reports the missing input and abandons the panel
                    int fileIdx = f;   // a for-loop variable is captured by reference, not per iteration
                    // ReadRecords returns false AFTER having invoked the callback, with the partial
                    // effects the caller must discard - the sibling RestorePass1Scalars stages into a
                    // buffer and applies only `if (ok)` for exactly this reason. Discarding the bool
                    // here meant a truncated or mid-write sidecar streamed N of M records into
                    // ObserveCutoff and then had SealRunCutoff called on the partial bests. Because
                    // the cutoff is a MIN over accepted precursors, the boundary came out too HIGH
                    // and the admitted decoy set too small, and both were then LOGGED as the run's
                    // acceptance boundary. The `admitted != tallied` self-check cannot catch it -
                    // both sides shrink together - so refuse the panel instead of publishing it.
                    bool readOk = FdrScoresSidecar.ReadRecords(sidecar, FdrScoresSidecar.Pass.FirstPass, rec =>
                    {
                        bool dec = (rec.EntryId & LibraryEntry.DECOY_ID_BIT) != 0;
                        // The experiment-scope half comes from the analysis-wide sidecar
                        // (format v5, issue #4486). An entry with no record there took part in
                        // no experiment competition, so it keeps the default q of 1.0 and a
                        // 0.0 aggregate - exactly what the pre-split record carried for it.
                        // A record with no experiment entry took part in no experiment
                        // competition, so it has no aggregate to be ranked on. Skip it rather
                        // than feed 0.0, for the reason given where the map is read.
                        if (!experimentRecords.TryGetValue(rec.EntryId, out var exp))
                            return;
                        builder.ObserveCutoff(fileIdx,
                            ModelDiagnosticsData.ClassifyEntry(dec, rec.EntryId, classByBaseId), rec.EntryId, rec.Score,
                            exp.ExperimentAggregateScore,
                            EffectiveQvalue(rec, exp, config.FdrLevel, true),
                            EffectiveQvalue(rec, exp, config.FdrLevel, false), config.RunFdr);
                    });
                    if (!readOk)
                    {
                        log.LogInfo(LogTag.MODEL_DIAGNOSTICS, string.Format(
                            @"peak co-assignment: 1st-pass sidecar for {0} could not be read in full; the acceptance boundary would be drawn from a partial pool, so the panel is not built",
                            fileNames[f]));
                        return null;
                    }
                    // Reduce this file's per-precursor bests to its run cutoff and the decoys
                    // clearing it, then let them go. Holding every file's bests to the end was
                    // O(files x distinct entry ids) - 4.18M per file on the full entrapment
                    // library, i.e. ~79 GB at the 500-file target.
                    builder.SealRunCutoff(fileIdx);
                }
            }
            // Also reported: SealCutoffs walks the whole experiment population in score order to
            // find where this pass's own count reaches the target FDR, which is the other half of
            // the silence the 138 s gap covered.
            log.LogInfo(LogTag.MODEL_DIAGNOSTICS, string.Format(
                @"peak co-assignment: reducing the experiment boundary over {0} file(s)...",
                fileNames.Count));
            builder.SealCutoffs();

            // Name the acceptance boundary in the log. The decoy row is the only class on this
            // panel gated by score rather than by its own q, so it is the only one with no
            // independent check on the page - a boundary that drifts produces a plausible-looking
            // number instead of an obvious failure. Printing the boundary, the accepted count
            // behind it, and how many precursors clear it makes the panel self-diagnosing.
            builder.CountAboveExperimentCutoff(out int aboveDecoys, out int aboveNonDecoys);
            log.LogInfo(LogTag.MODEL_DIAGNOSTICS, string.Format(
                @"peak co-assignment boundary (pass 1): experiment {0:F4} from {1} accepted precursor(s); {2} decoy + {3} non-decoy precursor(s) clear it",
                builder.ExperimentCutoff, builder.AcceptedForCutoff, aboveDecoys, aboveNonDecoys));
            for (int f = 0; f < fileNames.Count; f++)
            {
                log.LogInfo(LogTag.MODEL_DIAGNOSTICS, string.Format(
                    @"peak co-assignment boundary (pass 1): run {0} = {1:F4}",
                    fileNames[f], builder.RunCutoff(f)));
            }

            int totalDetected = 0, totalUnresolved = 0;
            // Reported because this loop is the panel's whole cost and it used to run SILENT. On
            // the 82-file SEA-AD run it went 101 s between the last per-run boundary line and the
            // completion summary with nothing in between, which reads as a hung run - the same
            // shape as the unreported spectra-cache write. The per-run boundary lines above are
            // one-per-file and cheap; this pass is where the time actually goes.
            // Attribution for this pass's allocation, off unless asked for. It is what found the
            // parquet column read the v7 sidecar removed - two 58-minute cohort runs looking at
            // memory curves found nothing, and ten seconds of this on three files named the call
            // site. Kept pointed at what remains, so the next regression here is measured rather
            // than guessed at.
            var tally = OspreyEnvironment.LogCoAssignmentAllocation ? new AllocationTally() : null;
            ProfilerHooks.CaptureRetentionSnapshot(@"coassign-join-start");
            using (var progress = new ProgressReporter(
                string.Format(@"Peak co-assignment: joining apex RT over {0} file(s)", fileNames.Count),
                fileNames.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                for (int f = 0; f < fileNames.Count; f++)
                {
                    progress.Report(f + 1);
                    string reason;
                    int fileUnresolved;
                    int detected = AddFile(builder, f, fileNames[f], perFileParquetPaths, config,
                        classByBaseId, libraryById, experimentRecords, tally,
                        out reason, out fileUnresolved);
                    totalUnresolved += fileUnresolved;
                    if (reason != null)
                    {
                        log.LogInfo(LogTag.MODEL_DIAGNOSTICS, string.Format(
                            @"peak co-assignment unavailable ({0}); panel omitted.", reason));
                        return null;
                    }
                    totalDetected += detected;
                    builder.FlushFile();
                }
            }
            ProfilerHooks.CaptureRetentionSnapshot(@"coassign-join-end");
            if (tally != null)
            {
                // Per file as well as total: a figure that grows with the file index is a
                // different problem from one that is simply large on every file.
                log.LogInfo(LogTag.MODEL_DIAGNOSTICS, string.Format(
                    @"peak co-assignment fold allocated {0:N1} GB over {1} file(s): " +
                    @"sidecar stream {2:N1} GB ({3:N0} MB/file), no parquet column read",
                    tally.SidecarStreamBytes / 1073741824.0,
                    fileNames.Count,
                    tally.SidecarStreamBytes / 1073741824.0,
                    tally.SidecarStreamBytes / 1048576.0 / Math.Max(1, fileNames.Count)));
            }

            var data = builder.Build();

            // Admitted must equal tallied. These were 468 and 72 while the decoy key collided with
            // its target's and the precursor registry silently kept only the first arrival - a
            // whole class 6.5x under-reported with nothing in the output to say so. The panel has
            // no independent check on its decoy row, so this comparison is the check.
            int admitted = builder.ExperimentDecoyIdCount;
            int tallied = data?.Experiment?.Decoy?.N ?? 0;
            log.LogInfo(LogTag.MODEL_DIAGNOSTICS, string.Format(
                @"peak co-assignment (pass 1): decoy precursors admitted {0}, tallied {1}",
                admitted, tallied));
            if (admitted != tallied)
            {
                log.LogInfo(LogTag.MODEL_DIAGNOSTICS, string.Format(
                    @"peak co-assignment WARNING: {0} decoy precursor(s) cleared the experiment boundary but {1} reached the panel; the decoy row is under-reported.",
                    admitted, tallied));
            }
            if (data == null)
            {
                log.LogInfo(LogTag.MODEL_DIAGNOSTICS, @"peak co-assignment: no detected rows resolved to a library m/z; panel omitted.");
                return null;
            }
            sw.Stop();
            // Name the cost rather than let it be a silent tax: this is a full stream of every
            // file's 1st-pass sidecar, and it happens only under --model-diagnostics.
            log.LogInfo(LogTag.MODEL_DIAGNOSTICS, string.Format(
                @"peak co-assignment (pass 1): {0} detected rows over {1} file(s) in {2:F1}s",
                totalDetected, fileNames.Count, sw.Elapsed.TotalSeconds));
            if (totalUnresolved > 0)
            {
                // Never silent: an unresolvable detected row is exactly how the decoy class went
                // 30x under-reported (19 counted against 598 in the sidecars) with nothing in the
                // log to say a whole class had been thinned.
                log.LogInfo(LogTag.MODEL_DIAGNOSTICS, string.Format(
                    @"peak co-assignment: {0} detected row(s) had no library entry and were excluded",
                    totalUnresolved));
            }
            return data;
        }

        /// <summary>
        /// Fold one file's detected rows into <paramref name="builder"/>, returning the number
        /// added. <paramref name="reason"/> is non-null when the file's sidecar could not be
        /// read, which abandons the whole panel: a panel built from the files that happened to
        /// work would silently under-report.
        /// </summary>
        private static int AddFile(
            ModelDiagnosticsData.CoAssignmentPassBuilder builder,
            int fileIdx,
            string fileName,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            IReadOnlyDictionary<uint, FdrExperimentRecord> experimentRecords,
            AllocationTally tally,
            out string reason,
            out int unresolved)
        {
            reason = null;
            unresolved = 0;
            string sidecarBase = ScoringTaskShared.ResolveSidecarBasePath(fileName, perFileParquetPaths, config);
            if (string.IsNullOrEmpty(sidecarBase))
            {
                reason = string.Format(@"no sidecar base path for {0}", fileName);
                return 0;
            }
            // The same path the score-pass sink wrote, so the panel reads exactly what this run
            // produced - and it is final when written, because the experiment-scope columns that
            // used to arrive by a later patch now live in their own analysis-wide sidecar.
            string sidecarPath = FdrScoresSidecar.Pass1Path(sidecarBase);
            if (!File.Exists(sidecarPath))
            {
                reason = string.Format(@"no 1st-pass fdr_scores.bin for {0}", fileName);
                return 0;
            }

            int added = 0;
            int nUnresolved = 0;
            long allocBefore = tally == null ? 0 : AllocatedBytes();
            bool ok = FdrScoresSidecar.ReadRecords(sidecarPath, FdrScoresSidecar.Pass.FirstPass, rec =>
            {
                // Gate BEFORE any allocation: this callback fires for every pre-compaction row.
                if (!experimentRecords.TryGetValue(rec.EntryId, out var exp))
                    return;
                double runQ = EffectiveQvalue(rec, exp, config.FdrLevel, true);
                double experimentQ = EffectiveQvalue(rec, exp, config.FdrLevel, false);
                bool isDecoy = (rec.EntryId & LibraryEntry.DECOY_ID_BIT) != 0;
                var cls = ModelDiagnosticsData.ClassifyEntry(isDecoy, rec.EntryId, classByBaseId);
                if (!builder.Includes(fileIdx, cls, rec.EntryId, runQ, experimentQ, config.RunFdr))
                    return;
                // Decoy entries are not always present in THIS task's library index, while their
                // target twin always is - and a decoy carries its target's precursor m/z by
                // construction (same composition). Falling back to the base id keeps the decoy
                // class populated; without it the panel silently dropped ~97% of detected decoys
                // (19 counted against 598 in the sidecars) and reported a decoy rate 30x too low.
                if (!libraryById.TryGetValue(rec.EntryId, out var lib) || lib == null)
                    libraryById.TryGetValue(rec.EntryId & BASE_ID_MASK, out lib);
                if (lib == null || double.IsNaN(lib.PrecursorMz) || lib.PrecursorMz <= 0)
                {
                    nUnresolved++;
                    return;
                }
                // Identity from the library entry that actually resolved. Tag EVERY decoy's key,
                // not just one resolved via the base-id fallback: a decoy's library entry carries
                // its target's modified sequence, so an untagged decoy key is byte-identical to
                // its target's. The precursor registry keeps the first arrival for a key and skips
                // the rest, so an untagged decoy is silently absorbed into its target's bucket and
                // never counted - measured at 396 of 468 admitted decoys (the panel reported 72).
                // Keying on the decoy BIT rather than on which library entry resolved covers the
                // fallback and own-entry cases with one rule.
                bool rowIsDecoy = (rec.EntryId & LibraryEntry.DECOY_ID_BIT) != 0;
                string modSeq = lib.ModifiedSequence ?? lib.Sequence;
                builder.AddRow(fileIdx, new ModelDiagnosticsData.CoAssignmentRow(
                        new ModelDiagnosticsData.PrecursorKey(modSeq, lib.Charge, rowIsDecoy),
                        rec.EntryId, lib.PrecursorMz, rec.ApexRt, rec.Score, cls),
                    runQ, experimentQ, config.RunFdr);
                added++;
            });

            if (!ok)
            {
                reason = string.Format(@"could not read {0}", Path.GetFileName(sidecarPath));
                return 0;
            }
            if (tally != null)
                tally.SidecarStreamBytes += AllocatedBytes() - allocBefore;
            unresolved = nUnresolved;
            return added;
        }

        /// <summary>
        /// Allocated bytes attributed to this panel's per-file fold. Instantiated only when
        /// <c>OSPREY_LOG_COASSIGN_ALLOC</c> is set, and passed as null otherwise, so the probes
        /// cost nothing when it is off.
        ///
        /// <para>Counts the CALLING thread. The fold loop is sequential, so that covers the work
        /// this phase does; allocation a library makes on its own threads is invisible here, and
        /// a large unattributed remainder against the process total is itself the finding - which
        /// is how the parquet column read this panel no longer does was located.</para>
        /// </summary>
        private sealed class AllocationTally
        {
            public long SidecarStreamBytes;
        }

        /// <summary>
        /// Bytes allocated so far, for <see cref="AllocationTally"/>. Only differences between
        /// two readings mean anything, so the two builds need not agree on the absolute figure.
        ///
        /// <para><c>GC.GetAllocatedBytesForCurrentThread</c> is .NET Core only. The framework
        /// build falls back to the AppDomain's process-wide counter, which is coarser - it counts
        /// every thread - but this diagnostic is read on net8.0 and the framework path has to
        /// compile and stay honest rather than be precise.</para>
        /// </summary>
        private static long AllocatedBytes()
        {
#if NETFRAMEWORK
            AppDomain.MonitoringIsEnabled = true;
            return AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
#else
            return GC.GetAllocatedBytesForCurrentThread();
#endif
        }

        /// <summary>Mask clearing the decoy high bit to get the shared target/decoy base id.</summary>
        private const uint BASE_ID_MASK = 0x7FFFFFFF;

        /// <summary>
        /// The pass-1 FDR sidecar for one input file, or null when it cannot be resolved. Same
        /// resolution the score-pass sink used to WRITE it, so the panel reads what this run made.
        /// </summary>
        private static string ResolveSidecarPath(string fileName,
            IReadOnlyDictionary<string, string> perFileParquetPaths, OspreyConfig config)
        {
            string b = ScoringTaskShared.ResolveSidecarBasePath(fileName, perFileParquetPaths, config);
            if (string.IsNullOrEmpty(b))
                return null;
            string p = FdrScoresSidecar.Pass1Path(b);
            return File.Exists(p) ? p : null;
        }

        /// <summary>
        /// The q-value for the configured FDR control level at one scope, off a sidecar record.
        /// Mirrors <see cref="FdrRowExtensions.EffectiveRunQvalue{T}"/> /
        /// <see cref="FdrRowExtensions.EffectiveExperimentQvalue{T}"/>, which cannot be used here because
        /// this path never materializes an <see cref="FdrEntry"/>.
        /// </summary>
        /// <param name="rec">One per-file sidecar record's decoded payload, for the run scope.</param>
        /// <param name="exp">The entry's experiment-scope record, for the experiment scope.</param>
        /// <param name="level">FDR control level the run reports at.</param>
        /// <param name="runScope">True for the run-level q, false for the experiment-wide q.</param>
        private static double EffectiveQvalue(in FdrScoreRecord rec, in FdrExperimentRecord exp,
            FdrLevel level, bool runScope)
        {
            switch (level)
            {
                case FdrLevel.Precursor:
                    return runScope ? rec.RunPrecursorQvalue : exp.ExperimentPrecursorQvalue;
                case FdrLevel.Peptide:
                    return runScope ? rec.RunPeptideQvalue : exp.ExperimentPeptideQvalue;
                case FdrLevel.Both:
                    return runScope
                        ? Math.Max(rec.RunPrecursorQvalue, rec.RunPeptideQvalue)
                        : Math.Max(exp.ExperimentPrecursorQvalue, exp.ExperimentPeptideQvalue);
                default:
                    throw new ArgumentOutOfRangeException(nameof(level));
            }
        }
    }
}
