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
    /// is never resident. The lean first pass carries no RT at all by design - <c>FdrProjection</c>
    /// is 32 bytes and every RT/bounds field is reload-obtained for the (small) survivor set after
    /// compaction (issue #4355) - and the streaming score path reads only entry_id / charge /
    /// is_decoy / coelution_sum / modseq from parquet. Plumbing apex RT through that path would
    /// put an extra column on the one code path that touches all ~340M pre-compaction rows.</para>
    ///
    /// <para>So this reconstructs it the way the prototype that measured the effect did
    /// (<c>ai/scripts/Osprey/Entrapment/pass1_entrap.py</c>): per file, join the
    /// <c>.1st-pass.fdr_scores.bin</c> sidecar (score + q-values, one record per row in row order)
    /// against the same file's <c>.scores.parquet</c> (<c>apex_rt</c>, same rows, same order). The
    /// two are positionally aligned, and that alignment is ASSERTED on entry_id per row rather
    /// than assumed - a mismatch abandons the panel with a log line instead of reporting numbers
    /// off a bad join.</para>
    ///
    /// <para>Bounded: one file's entry_id + apex_rt arrays (~12 bytes per row) and one sidecar
    /// record at a time. Precursor identity and m/z come from the library by entry id, so no
    /// string column is materialized for the whole pre-compaction pool.</para>
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
        /// <param name="logInfo">Run log sink; every degrade path explains itself through it.</param>
        public static ModelDiagnosticsData.CoAssignmentData Build(
            IReadOnlyList<string> fileNames,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            Action<string> logInfo)
        {
            // The "never throws" promise above needs an actual guard, and it was only ever
            // enforced around the parquet read inside AddFile. Everything else - the builder's
            // own misuse guards, the per-file sort and histogram writes in FlushFile, Build's
            // reduction, and the caller's library resolution - ran unprotected, and the callers
            // in FirstPassFdrTask invoke this OUTSIDE the report writer's try/catch. A panel
            // that aborts a ten-hour search is a worse outcome than a panel that is missing.
            try
            {
                return BuildCore(fileNames, perFileParquetPaths, config, classByBaseId, libraryById, logInfo);
            }
            catch (Exception ex)
            {
                logInfo(string.Format(
                    @"[MODEL-DIAGNOSTICS] peak co-assignment abandoned after an unexpected error: {0}",
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
            Action<string> logInfo)
        {
            if (fileNames == null || perFileParquetPaths == null || libraryById == null)
            {
                logInfo(@"[MODEL-DIAGNOSTICS] peak co-assignment skipped: no per-file parquet or library available.");
                return null;
            }

            var sw = Stopwatch.StartNew();
            var runNames = new string[fileNames.Count];
            for (int f = 0; f < fileNames.Count; f++)
                runNames[f] = fileNames[f];
            var builder = new ModelDiagnosticsData.CoAssignmentPassBuilder(runNames, 1, false);

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
                        builder.ObserveCutoff(fileIdx,
                            ModelDiagnosticsData.ClassifyEntry(dec, rec.EntryId, classByBaseId), rec.EntryId, rec.Score,
                            rec.ExperimentAggregateScore,
                            EffectiveQvalue(rec, config.FdrLevel, true),
                            EffectiveQvalue(rec, config.FdrLevel, false), config.RunFdr);
                    });
                    if (!readOk)
                    {
                        logInfo(string.Format(
                            @"[MODEL-DIAGNOSTICS] peak co-assignment: 1st-pass sidecar for {0} could not be read in full; the acceptance boundary would be drawn from a partial pool, so the panel is not built",
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
            logInfo(string.Format(
                @"[MODEL-DIAGNOSTICS] peak co-assignment: reducing the experiment boundary over {0} file(s)...",
                fileNames.Count));
            builder.SealCutoffs();

            // Name the acceptance boundary in the log. The decoy row is the only class on this
            // panel gated by score rather than by its own q, so it is the only one with no
            // independent check on the page - a boundary that drifts produces a plausible-looking
            // number instead of an obvious failure. Printing the boundary, the accepted count
            // behind it, and how many precursors clear it makes the panel self-diagnosing.
            builder.CountAboveExperimentCutoff(out int aboveDecoys, out int aboveNonDecoys);
            logInfo(string.Format(
                @"[MODEL-DIAGNOSTICS] peak co-assignment boundary (pass 1): experiment {0:F4} from {1} accepted precursor(s); {2} decoy + {3} non-decoy precursor(s) clear it",
                builder.ExperimentCutoff, builder.AcceptedForCutoff, aboveDecoys, aboveNonDecoys));
            for (int f = 0; f < fileNames.Count; f++)
            {
                logInfo(string.Format(
                    @"[MODEL-DIAGNOSTICS] peak co-assignment boundary (pass 1): run {0} = {1:F4}",
                    fileNames[f], builder.RunCutoff(f)));
            }

            int totalDetected = 0, totalUnresolved = 0;
            // Reported because this loop is the panel's whole cost and it used to run SILENT:
            // it re-reads two columns of every .scores.parquet and joins them per file. On the
            // 82-file SEA-AD run it went 101 s between the last per-run boundary line and the
            // completion summary with nothing in between, which reads as a hung run - the same
            // shape as the unreported spectra-cache write. The per-run boundary lines above are
            // one-per-file and cheap; this pass is where the time actually goes.
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
                        classByBaseId, libraryById, out reason, out fileUnresolved);
                    totalUnresolved += fileUnresolved;
                    if (reason != null)
                    {
                        logInfo(string.Format(
                            @"[MODEL-DIAGNOSTICS] peak co-assignment unavailable ({0}); panel omitted.", reason));
                        return null;
                    }
                    totalDetected += detected;
                    builder.FlushFile();
                }
            }

            var data = builder.Build();

            // Admitted must equal tallied. These were 468 and 72 while the decoy key collided with
            // its target's and the precursor registry silently kept only the first arrival - a
            // whole class 6.5x under-reported with nothing in the output to say so. The panel has
            // no independent check on its decoy row, so this comparison is the check.
            int admitted = builder.ExperimentDecoyIdCount;
            int tallied = data?.Experiment?.Decoy?.N ?? 0;
            logInfo(string.Format(
                @"[MODEL-DIAGNOSTICS] peak co-assignment (pass 1): decoy precursors admitted {0}, tallied {1}",
                admitted, tallied));
            if (admitted != tallied)
            {
                logInfo(string.Format(
                    @"[MODEL-DIAGNOSTICS] peak co-assignment WARNING: {0} decoy precursor(s) cleared the experiment boundary but {1} reached the panel; the decoy row is under-reported.",
                    admitted, tallied));
            }
            if (data == null)
            {
                logInfo(@"[MODEL-DIAGNOSTICS] peak co-assignment: no detected rows resolved to a library m/z; panel omitted.");
                return null;
            }
            sw.Stop();
            // Name the cost rather than let it be a silent tax: this is a second read of two
            // columns of every .scores.parquet, and it happens only under --model-diagnostics.
            logInfo(string.Format(
                @"[MODEL-DIAGNOSTICS] peak co-assignment (pass 1): {0} detected rows over {1} file(s) in {2:F1}s",
                totalDetected, fileNames.Count, sw.Elapsed.TotalSeconds));
            if (totalUnresolved > 0)
            {
                // Never silent: an unresolvable detected row is exactly how the decoy class went
                // 30x under-reported (19 counted against 598 in the sidecars) with nothing in the
                // log to say a whole class had been thinned.
                logInfo(string.Format(
                    @"[MODEL-DIAGNOSTICS] peak co-assignment: {0} detected row(s) had no library entry and were excluded",
                    totalUnresolved));
            }
            return data;
        }

        /// <summary>
        /// Fold one file's detected rows into <paramref name="builder"/>, returning the number
        /// added. <paramref name="reason"/> is non-null when the file could not be read or its
        /// sidecar did not align with its parquet, which abandons the whole panel: a panel built
        /// from the files that happened to work would silently under-report.
        /// </summary>
        private static int AddFile(
            ModelDiagnosticsData.CoAssignmentPassBuilder builder,
            int fileIdx,
            string fileName,
            IReadOnlyDictionary<string, string> perFileParquetPaths,
            OspreyConfig config,
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            out string reason,
            out int unresolved)
        {
            reason = null;
            unresolved = 0;
            if (!perFileParquetPaths.TryGetValue(fileName, out string parquetPath) ||
                string.IsNullOrEmpty(parquetPath) || !File.Exists(parquetPath))
            {
                reason = string.Format(@"no .scores.parquet for {0}", fileName);
                return 0;
            }
            string sidecarBase = ScoringTaskShared.ResolveSidecarBasePath(fileName, perFileParquetPaths, config);
            if (string.IsNullOrEmpty(sidecarBase))
            {
                reason = string.Format(@"no sidecar base path for {0}", fileName);
                return 0;
            }
            // The same path the score-pass sink wrote, so the panel reads exactly what this run
            // produced. At this point the records are the PARTIAL ones (run_protein_qvalue is
            // still the 1.0 placeholder that first-pass protein FDR patches later) - harmless
            // here, since the panel reads only the score and the precursor/peptide q-values.
            string sidecarPath = FdrScoresSidecar.Pass1Path(sidecarBase);
            if (!File.Exists(sidecarPath))
            {
                reason = string.Format(@"no 1st-pass fdr_scores.bin for {0}", fileName);
                return 0;
            }

            uint[] entryIds;
            double[] apexRts;
            try
            {
                if (!ParquetScoreCache.TryReadEntryIdsAndApexRts(parquetPath, out entryIds, out apexRts))
                {
                    reason = string.Format(@"no apex_rt column in {0}", Path.GetFileName(parquetPath));
                    return 0;
                }
            }
            catch (Exception ex)
            {
                reason = string.Format(@"{0}: {1}", Path.GetFileName(parquetPath), ex.Message);
                return 0;
            }

            int row = 0;
            int added = 0;
            int nUnresolved = 0;
            bool misaligned = false;
            bool ok = FdrScoresSidecar.ReadRecords(sidecarPath, FdrScoresSidecar.Pass.FirstPass, rec =>
            {
                if (misaligned)
                    return;
                // Assert the positional join instead of trusting it. The sidecar is written in
                // parquet row order by the score-pass sink, but nothing in either format records
                // that contract, so a future change to either side would otherwise silently
                // attach one row's apex RT to another row's score.
                if (row >= entryIds.Length || entryIds[row] != rec.EntryId)
                {
                    misaligned = true;
                    return;
                }
                int current = row++;
                // Gate BEFORE any allocation: this callback fires for every pre-compaction row.
                double runQ = EffectiveQvalue(rec, config.FdrLevel, true);
                double experimentQ = EffectiveQvalue(rec, config.FdrLevel, false);
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
                string key = rowIsDecoy
                    ? modSeq + "|" + lib.Charge + "|decoy"
                    : modSeq + "|" + lib.Charge;
                builder.AddRow(fileIdx, new ModelDiagnosticsData.CoAssignmentRow(
                        key, rec.EntryId, modSeq, lib.Charge, lib.PrecursorMz, apexRts[current], rec.Score, cls),
                    runQ, experimentQ, config.RunFdr);
                added++;
            });

            if (!ok)
            {
                reason = string.Format(@"could not read {0}", Path.GetFileName(sidecarPath));
                return 0;
            }
            if (misaligned || row != entryIds.Length)
            {
                reason = string.Format(@"{0}: sidecar/parquet row misalignment", fileName);
                return 0;
            }
            unresolved = nUnresolved;
            return added;
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
        /// Mirrors <see cref="FdrEntry.EffectiveRunQvalue"/> /
        /// <see cref="FdrEntry.EffectiveExperimentQvalue"/>, which cannot be used here because
        /// this path never materializes an <see cref="FdrEntry"/>.
        /// </summary>
        /// <param name="rec">One 60-byte sidecar record's decoded payload.</param>
        /// <param name="level">FDR control level the run reports at.</param>
        /// <param name="runScope">True for the run-level q, false for the experiment-wide q.</param>
        private static double EffectiveQvalue(in FdrScoreRecord rec, FdrLevel level, bool runScope)
        {
            switch (level)
            {
                case FdrLevel.Precursor:
                    return runScope ? rec.RunPrecursorQvalue : rec.ExperimentPrecursorQvalue;
                case FdrLevel.Peptide:
                    return runScope ? rec.RunPeptideQvalue : rec.ExperimentPeptideQvalue;
                case FdrLevel.Both:
                    return runScope
                        ? Math.Max(rec.RunPrecursorQvalue, rec.RunPeptideQvalue)
                        : Math.Max(rec.ExperimentPrecursorQvalue, rec.ExperimentPeptideQvalue);
                default:
                    throw new ArgumentOutOfRangeException(nameof(level));
            }
        }
    }
}
