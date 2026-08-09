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

            int totalDetected = 0;
            for (int f = 0; f < fileNames.Count; f++)
            {
                string reason;
                int detected = AddFile(builder, f, fileNames[f], perFileParquetPaths, config,
                    classByBaseId, libraryById, out reason);
                if (reason != null)
                {
                    logInfo(string.Format(
                        @"[MODEL-DIAGNOSTICS] peak co-assignment unavailable ({0}); panel omitted.", reason));
                    return null;
                }
                totalDetected += detected;
                builder.FlushFile();
            }

            var data = builder.Build();
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
            out string reason)
        {
            reason = null;
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
                // Both definitions of "detected" the report uses. Gate on them BEFORE any
                // allocation: this callback fires for every pre-compaction row in the file.
                double runQ = EffectiveQvalue(rec, config.FdrLevel, true);
                double experimentQ = EffectiveQvalue(rec, config.FdrLevel, false);
                if (runQ > config.RunFdr && experimentQ > config.RunFdr)
                    return;
                if (!libraryById.TryGetValue(rec.EntryId, out var lib) || lib == null ||
                    double.IsNaN(lib.PrecursorMz) || lib.PrecursorMz <= 0)
                    return;
                bool isDecoy = (rec.EntryId & LibraryEntry.DECOY_ID_BIT) != 0;
                var cls = ModelDiagnosticsData.ClassifyEntry(isDecoy, rec.EntryId, classByBaseId);
                string modSeq = lib.ModifiedSequence ?? lib.Sequence;
                builder.AddRow(fileIdx, new ModelDiagnosticsData.CoAssignmentRow(
                        modSeq + "|" + lib.Charge, modSeq, lib.Charge,
                        lib.PrecursorMz, apexRts[current], rec.Score, cls),
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
            return added;
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
