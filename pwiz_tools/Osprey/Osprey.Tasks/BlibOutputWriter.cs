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
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Orchestrates the BiblioSpecLite <c>.blib</c> output emission for the SecondPassFDR
    /// node (Stage 9): source-file IDs, a parallel zlib pre-compress pass, the
    /// sequential per-best-precursor RefSpectra + modifications + protein +
    /// RetentionTimes + Osprey extension-table emission, then metadata and
    /// finalize. Drives the low-level <see cref="BlibWriter"/> (the SQLite layer);
    /// this type owns only the per-spectrum row composition.
    ///
    /// Extracted verbatim from <c>SecondPassFdrTask.WriteBlibOutput</c> as pure code
    /// motion so <see cref="Write"/> reads as a sequencer; behavior (and therefore
    /// the blib bytes) is unchanged. Mirrors Rust pipeline.rs:4596-6272.
    /// </summary>
    internal static class BlibOutputWriter
    {
        /// <summary>
        /// Write the .blib for the passing precursors. The gating / best-run
        /// selection has already been done by the caller and is handed in as the
        /// pre-built lookup tables.
        /// </summary>
        internal static void Write(
            OspreyConfig config,
            IReadOnlyList<string> fileNames,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            Dictionary<(string, byte), KeyValuePair<string, FdrEntry>> bestByPrecursor,
            Dictionary<(string, byte), double> bestExpPrecursorQ,
            Dictionary<(string, string), double[]> sharedBounds,
            List<PassingObservation> passingEntries,
            Dictionary<(string, byte), (bool AnyPassesRunFdr, string BestRunFile, int NRuns)> precursorFacts)
        {
            double fdrThreshold = config.RunFdr; // run-level threshold for ID-line semantics
            // Write the blib to a FileSaver sibling temp, then atomically rename it into
            // place. This is safe with BlibWriter's WAL journaling ONLY because
            // FinalizeDatabase() runs PRAGMA wal_checkpoint(TRUNCATE) + journal_mode=DELETE
            // (BlibWriter.cs:577-578): by the time the inner using disposes BlibWriter -- which
            // disposes every prepared command before closing the connection, releasing the OS
            // handle -- the WAL has been merged back and the -wal/-shm sidecars are gone, so
            // saver.Commit() renames a single self-contained file (no orphaned WAL, no
            // sharing-violation on the still-open handle). Keep FinalizeDatabase() as the last
            // BlibWriter call before the connection closes; reordering or dropping it would
            // leave sidecar files that the rename would not carry.
            using (var saver = new FileSaver(config.OutputBlib))
            {
                using (var writer = OpenBlibWriter(saver.SafeName, config.OutputBlib))
                {
                    writer.BeginBatch();

                    var sourceFileIds = CreateSourceFiles(writer, config, fileNames, fdrThreshold);

                    var blibEntries = bestByPrecursor.Values.ToList();
                    var spectra = PrepareSpectra(blibEntries, libraryById, config.NThreads);

                    // TWO passes, and the split is the point. The first writes one row per
                    // precursor and hands back the RefSpectra ids; the second walks the
                    // passing entries in their own file-major order and writes each one's
                    // RetentionTimes row against those ids.
                    //
                    // It used to be one precursor-major pass, which had to reach every file's
                    // row for a precursor at once - the O(observations) index that made the
                    // blib phase the last consumer needing a whole-run view (#4486). Row
                    // ORDER in the table changes; nothing reads it. Compare-BlibGolden keys
                    // RetentionTimes on (peptideModSeq, precursorCharge, fileName), and the
                    // self-consistency legs go through Compare-BlibFull, table-based too.
                    var refIdByPrecursor = EmitSpectrumRows(
                        writer, blibEntries, spectra,
                        sourceFileIds, bestExpPrecursorQ, sharedBounds,
                        precursorFacts, fileNames.Count);

                    WriteRetentionTimesFileMajor(writer, passingEntries, refIdByPrecursor,
                        precursorFacts, bestByPrecursor, sourceFileIds, sharedBounds,
                        fdrThreshold);

                    writer.Commit();

                    WriteMetadata(writer, config);

                    writer.FinalizeDatabase();
                }
                try
                {
                    saver.Commit();
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    throw new BlibOutputException(config.OutputBlib, ex);
                }
            }
        }

        /// <summary>
        /// Open the SQLite writer on the temp file beside the output. A failure here is a
        /// user-correctable condition (the folder is not writable, or the file is locked), so
        /// it becomes a <see cref="BlibOutputException"/> naming the output rather than a raw
        /// <see cref="SQLiteException"/> whose type and stack would bury the cause.
        ///
        /// <para>Only SQLite failures that describe file access are translated. The constructor
        /// also creates the schema and prepares the insert statements, and a defect there is a
        /// bug in this build, not a locked file - reporting it as one would send the user
        /// looking for a program that does not exist.</para>
        /// </summary>
        private static BlibWriter OpenBlibWriter(string tempPath, string outputPath)
        {
            try
            {
                return new BlibWriter(tempPath);
            }
            catch (Exception ex) when (IsFileAccessFailure(ex))
            {
                throw new BlibOutputException(outputPath, ex);
            }
        }

        private static bool IsFileAccessFailure(Exception ex)
        {
            if (ex is IOException || ex is UnauthorizedAccessException)
                return true;
            if (!(ex is SQLiteException sqliteEx))
                return false;
            // Strip any extended code (IoErr_Write, CantOpen_IsDir, ...) to its primary code: SQLite
            // keeps the primary result code in the low byte.
            var primary = (SQLiteErrorCode)((int)sqliteEx.ResultCode & 0xFF);
            switch (primary)
            {
                case SQLiteErrorCode.CantOpen:
                case SQLiteErrorCode.ReadOnly:
                case SQLiteErrorCode.Busy:
                case SQLiteErrorCode.Locked:
                case SQLiteErrorCode.IoErr:
                case SQLiteErrorCode.Full:
                case SQLiteErrorCode.Perm:
                case SQLiteErrorCode.Auth:
                    return true;
                default:
                    return false;
            }
        }

        // Pre-create source file IDs once. SpectrumSourceFiles.fileName carries
        // the ABSOLUTE path of each spectrum source file, matching BiblioSpec's
        // BlibBuild (BuildParser.insertSpectrumFilename resolves every name it
        // is given to a full path). Every route names the acquisition on -i, so the
        // path comes from the input itself - it does not have to EXIST for that, and
        // on a join node it usually does not. A from-scores run used to arrive with
        // the acquisition absent from the inputs entirely, and the path was
        // synthesized beside the parquet. The golden and cross-impl
        // comparators key these strings by BASENAME, which is what keeps the
        // committed goldens machine-independent. SpectrumSourceFiles.idFileName
        // carries the library filename (Skyline expects this - Rust
        // pipeline.rs:6110 + blib.rs:435). The library file is the "ID
        // source"; the mzML file is the spectrum source.
        private static Dictionary<string, long> CreateSourceFiles(
            BlibWriter writer, OspreyConfig config,
            IReadOnlyList<string> fileNames, double fdrThreshold)
        {
            string libraryIdName = Path.GetFileName(config.LibrarySource.Path);
            // The data files, on every route. A Stage 7 node used to be handed parquets and
            // had to convert them back into data-file names right here, to write the
            // SpectrumSourceFiles rows Skyline reads; it is handed the names themselves now.
            var inputs = config.InputFiles;
            var sourcePathByName = new Dictionary<string, string>();
            if (inputs != null)
            {
                foreach (string input in inputs)
                    sourcePathByName[Path.GetFileNameWithoutExtension(input)] = Path.GetFullPath(input);
            }
            var sourceFileIds = new Dictionary<string, long>();
            foreach (string fileName in fileNames)
            {
                // A key with no matching input is Stage 5/7 name drift; record
                // the bare name the writer always used to record rather than
                // fail the whole blib over it.
                if (!sourcePathByName.TryGetValue(fileName, out string sourcePath))
                    sourcePath = fileName + SpectrumFileReader.EXT_MZML;
                sourceFileIds[fileName] = writer.AddSourceFile(
                    sourcePath, libraryIdName, fdrThreshold);
            }
            return sourceFileIds;
        }

        // Parallel preparation pass. Per-spectrum zlib dominates the blib write wall;
        // BlibSpectrum does it with the rest of the per-precursor rows, for every entry in
        // parallel, and EmitSpectrumRows writes them in iteration order so RefSpectra row IDs
        // stay deterministic. A precursor missing from the library gets null and no row.
        private static BlibSpectrum[] PrepareSpectra(
            List<KeyValuePair<string, FdrEntry>> blibEntries,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById, int nThreads)
        {
            int blibN = blibEntries.Count;
            var spectra = new BlibSpectrum[blibN];
            // Reported because per-spectrum zlib dominates the blib write and ran silent: on the
            // 82-file SEA-AD run this pass and the emission below were the bulk of a 47 s gap
            // ending at "Wrote 51597 library spectra". They are not all of it - Commit,
            // WriteMetadata and FinalizeDatabase run after the emission scope closes and are
            // still uninstrumented. The surrounding [COUNT] lines cannot serve here:
            // LogTag.COUNT is gated behind --perf-stats, so they appear only
            // under that flag (the same trap the calibration scoring loop records).
            int precompressed = 0;
            using (var progress = new ProgressReporter(
                       string.Format(OspreyTasksResources.BlibOutputWriter_PrecompressSpectra_Compressing__0__library_spectra_for_the_blib, blibN),
                       blibN, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                Parallel.For(0, blibN,
                    new ParallelOptions { MaxDegreeOfParallelism = nThreads },
                    i =>
                    {
                        // Reported before the early-out below, so a run whose library lookups all
                        // miss still advances to 100% rather than stalling at 0%.
                        progress.Report(Interlocked.Increment(ref precompressed));
                        if (libraryById.TryGetValue(blibEntries[i].Value.EntryId, out var libEntry))
                            spectra[i] = BlibSpectrum.FromLibraryEntry(libEntry);
                    });
            }
            return spectra;
        }

        // Sequential per-best-precursor emission: one RefSpectra row (plus its
        // modifications / protein mappings / Osprey extension
        // rows) for each prepared spectrum, in iteration order so row IDs stay
        // deterministic.
        private static Dictionary<(string, byte), long> EmitSpectrumRows(
            BlibWriter writer,
            List<KeyValuePair<string, FdrEntry>> blibEntries,
            BlibSpectrum[] spectra,
            Dictionary<string, long> sourceFileIds,
            Dictionary<(string, byte), double> bestExpPrecursorQ,
            Dictionary<(string, string), double[]> sharedBounds,
            Dictionary<(string, byte), (bool AnyPassesRunFdr, string BestRunFile, int NRuns)> precursorFacts,
            int perFileEntriesCount)
        {
            var refIdByPrecursor = new Dictionary<(string, byte), long>(blibEntries.Count);
            // Reported for the same reason as the pre-compress pass above: this emits five row
            // families per spectrum into SQLite and ran silent inside the same 47 s gap.
            using (var progress = new ProgressReporter(
                       string.Format(OspreyTasksResources.BlibOutputWriter_PrecompressSpectra_Writing__0__spectra_to_the_blib, blibEntries.Count),
                       blibEntries.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                for (int blibIdx = 0; blibIdx < blibEntries.Count; blibIdx++)
                {
                    progress.Report(blibIdx + 1);
                    var kvp = blibEntries[blibIdx];
                    string fileName = kvp.Key;
                    var entry = kvp.Value;

                    var spectrum = spectra[blibIdx];
                    if (spectrum == null)
                        continue;

                    long fileId = sourceFileIds[fileName];

                    // RefSpectra.score is the EXPERIMENT-PRECURSOR q-value (min
                    // across all observations of this (modseq, charge)). Mirrors
                    // Rust pipeline.rs:4670-4683 / 4795. Same value feeds
                    // OspreyExperimentScores.ExperimentQValue below.
                    var lookupKey = (entry.ModifiedSequence, entry.Charge);
                    double scoreQvalue;
                    if (!bestExpPrecursorQ.TryGetValue(lookupKey, out scoreQvalue))
                        scoreQvalue = entry.ExperimentPrecursorQvalue;

                    // nRunsDetected -> RefSpectra.copies (Rust pipeline.rs:6179
                    // passes n_runs_detected = group.len()). Reused by
                    // OspreyExperimentScores below.
                    int nRunsDetected = 1;
                    if (precursorFacts.TryGetValue(lookupKey, out var facts) && facts.NRuns > 0)
                        nRunsDetected = facts.NRuns;

                    // Shared peak boundaries when the peptide is detected at
                    // multiple charges in this file (Rust pipeline.rs:6160-6164).
                    var sharedKey = (entry.ModifiedSequence, fileName);
                    double sharedApex = entry.ApexRt;
                    double sharedStart = entry.StartRt;
                    double sharedEnd = entry.EndRt;
                    double[] sharedVals;
                    if (sharedBounds.TryGetValue(sharedKey, out sharedVals))
                    {
                        sharedApex = sharedVals[0];
                        sharedStart = sharedVals[1];
                        sharedEnd = sharedVals[2];
                    }

                    // The library precursor's own rows - peaks, modifications and proteins -
                    // exactly as --export-library writes them; this
                    // row adds the search result's apex, boundaries, q-value and run count.
                    long refId = writer.AddSpectrum(spectrum,
                        sharedApex, sharedStart, sharedEnd,
                        scoreQvalue, fileId, nRunsDetected);

                    refIdByPrecursor[lookupKey] = refId;

                    // Osprey extension tables - one row per RefSpectra each,
                    // mirroring Rust pipeline.rs:6255-6272. Best-run-only for
                    // OspreyPeakBoundaries + OspreyRunScores; experiment-level for
                    // OspreyExperimentScores. The 0.0 fields are the same "not yet
                    // plumbed through Stage 7 plan entries" placeholders Rust writes.
                    writer.AddPeakBoundaries(refId, fileName,
                        sharedStart, sharedEnd, sharedApex,
                        0.0, // ApexIntensity - matches Rust's apex_coefficient placeholder
                        entry.BoundsArea);
                    writer.AddRunScores(refId, fileName,
                        entry.EffectiveRunQvalue(FdrLevel.Both),
                        0.0, // DiscriminantScore - matches Rust's dot_product placeholder
                        0.0); // PosteriorErrorProb - matches Rust's PEP placeholder
                    writer.AddExperimentScores(refId,
                        scoreQvalue, // Same value as RefSpectra.score
                        nRunsDetected,
                        perFileEntriesCount);
                }
            }
            return refIdByPrecursor;
        }

        // Add metadata. OspreyMetadata key set must match Rust's
        // write_blib_from_plan (pipeline.rs:6078-6081) byte-for-byte.
        private static void WriteMetadata(BlibWriter writer, OspreyConfig config)
        {
            writer.AddMetadata(@"osprey_version", OspreyVersion.Current);
            writer.AddMetadata(@"search_mode", @"coelution");
            writer.AddMetadata(@"run_fdr",
                config.RunFdr.ToString(CultureInfo.InvariantCulture));
            writer.AddMetadata(@"experiment_fdr",
                config.ExperimentFdr.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// The RetentionTimes rows - one per passing observation, and the only table in the
        /// blib that is O(files x precursors). Written FILE-MAJOR, in the order
        /// <c>CollectPassingEntries</c> produced, against the RefSpectra ids
        /// <see cref="EmitSpectrumRows"/> assigned.
        ///
        /// <para>It used to be written precursor-major, nested inside the RefSpectra loop,
        /// which required an index of every observation of a precursor across every file.
        /// That index was O(observations) and held a reference to each one, which is what
        /// made the blib the last consumer needing a whole-run view of the survivor pool
        /// (#4486). Everything a row needs is now either per-observation (apex, boundaries,
        /// run q) or an O(distinct precursor) fact looked up by key.</para>
        ///
        /// <para>Row order in the table changes and nothing reads it: the golden comparison
        /// keys RetentionTimes on (peptideModSeq, precursorCharge, fileName), and the
        /// resume / HPC-chain legs compare tables rather than bytes. The blib's own consumers
        /// query by RefSpectraID.</para>
        ///
        /// <para>The ID-line rule is unchanged and is why the per-precursor facts exist:
        /// <c>retentionTime</c> is populated iff this run passes run-level FDR, OR no run of
        /// this precursor does and this is its lowest-q run - so that every RefSpectra keeps
        /// at least one ID line. Both halves of that are properties of the precursor, not of
        /// the row, and neither survives a per-row view without being folded first.</para>
        /// </summary>
        private static void WriteRetentionTimesFileMajor(
            BlibWriter writer,
            List<PassingObservation> passingEntries,
            Dictionary<(string, byte), long> refIdByPrecursor,
            Dictionary<(string, byte), (bool AnyPassesRunFdr, string BestRunFile, int NRuns)> precursorFacts,
            Dictionary<(string, byte), KeyValuePair<string, FdrEntry>> bestByPrecursor,
            Dictionary<string, long> sourceFileIds,
            Dictionary<(string, string), double[]> sharedBounds,
            double fdrThreshold)
        {
            using (var progress = new ProgressReporter(
                       string.Format(OspreyTasksResources.BlibOutputWriter_WriteRetentionTimesFileMajor_Writing__0__peak_retention_times_to_the_blib,
                                     passingEntries.Count),
                       passingEntries.Count, string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                int done = 0;
                foreach (var obs in passingEntries)
                {
                    progress.Report(++done);
                    var key = obs.PrecursorKey;
                    // A precursor with no RefSpectra row got no id - it did not survive the
                    // best-per-precursor selection - so it has nothing to attach a row to.
                    if (!refIdByPrecursor.TryGetValue(key, out long refId))
                        continue;
                    if (!precursorFacts.TryGetValue(key, out var facts))
                        continue;

                    long srcId = sourceFileIds[obs.FileName];
                    double runQ = obs.RunQvalue;
                    bool passesFdr = runQ <= fdrThreshold;
                    bool showIdLine = passesFdr ||
                        (!facts.AnyPassesRunFdr && obs.FileName == facts.BestRunFile);
                    // bestSpectrum flags the run whose spectrum this RefSpectra row was
                    // built from, so it must come from bestByPrecursor - the same source the
                    // RefSpectra loop used. facts.BestRunFile applies the same min-run-q rule
                    // over the same rows and should never disagree, but reproducing the old
                    // expression is byte-identity by construction rather than by argument.
                    bool isBest = bestByPrecursor.TryGetValue(key, out var bestKvp) &&
                                  obs.FileName == bestKvp.Key;

                    var runSharedKey = (obs.ModifiedSequence, obs.FileName);
                    double runApex = obs.ApexRt;
                    double runStart = obs.StartRt;
                    double runEnd = obs.EndRt;
                    double[] runShared;
                    if (sharedBounds.TryGetValue(runSharedKey, out runShared))
                    {
                        runApex = runShared[0];
                        runStart = runShared[1];
                        runEnd = runShared[2];
                    }

                    double? rtForIdLine = null;
                    if (showIdLine)
                        rtForIdLine = runApex;
                    writer.AddRetentionTime(
                        refId, srcId,
                        rtForIdLine,
                        runStart,
                        runEnd,
                        runQ,
                        isBest);
                }
            }
        }
    }

    /// <summary>
    /// The output .blib could not be opened or put in place. Reported as an Error: line
    /// naming the file and the likely cause, not as a stack trace.
    /// </summary>
    public class BlibOutputException : IOException
    {
        public BlibOutputException(string outputPath, Exception inner)
            : base(string.Format(
                OspreyTasksResources.BlibOutputException_Could_not_write_the_spectral_library__0____1____Check_that_the_file_is_not_open_in_,
                outputPath, inner.Message), inner)
        {
        }
    }
}
