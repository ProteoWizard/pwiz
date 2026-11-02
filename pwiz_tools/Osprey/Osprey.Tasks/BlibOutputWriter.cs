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
using pwiz.Common.Pipeline;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Orchestrates the BiblioSpecLite <c>.blib</c> output emission for the SecondPassFDR
    /// node (Stage 9): source-file IDs, a parallel zlib pre-compress pass, the
    /// sequential per-best-precursor RefSpectra + modifications + protein group +
    /// RetentionTimes emission, then metadata and
    /// finalize. Adds the experiment-level <c>RefSpectra.PrecursorQValue</c>,
    /// <c>RefSpectra.PrecursorPep</c>, <c>RefSpectra.PeptideQValue</c> and
    /// <c>Proteins.QValue</c> columns (<see cref="OspreyBlibWriter.AddSearchGlobalColumns"/>),
    /// and its <c>Proteins</c> rows are protein groups. Drives <see cref="OspreyBlibWriter"/>
    /// (the SQLite layer, through the one .blib writer in BiblioSpec.IO); this type owns only
    /// the per-spectrum row composition.
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
            SecondPassProteinFdrResult proteinFdr,
            Dictionary<(string, byte), KeyValuePair<string, FdrEntry>> bestByPrecursor,
            Dictionary<(string, byte), double> bestExpPrecursorQ,
            Dictionary<(string, string), double[]> sharedBounds,
            List<PassingObservation> passingEntries,
            Dictionary<(string, byte), (bool AnyPassesRunFdr, string BestRunFile, int NRuns)> precursorFacts,
            ArtifactStamp stamp)
        {
            double fdrThreshold = config.RunFdr; // run-level threshold for ID-line semantics
            // The writer writes a temp beside the output and Complete moves it over the final
            // name, so a run that stops short leaves any library already there as it was.
            using (var writer = OpenBlibWriter(config.OutputBlib))
            {
                writer.AddSearchGlobalColumns();
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
                    writer, blibEntries, spectra, new ProteinGroupRows(writer, proteinFdr),
                    sourceFileIds, bestExpPrecursorQ, sharedBounds,
                    precursorFacts);

                WriteRetentionTimesFileMajor(writer, passingEntries, refIdByPrecursor,
                    precursorFacts, bestByPrecursor, sourceFileIds, sharedBounds,
                    fdrThreshold);

                writer.CommitBatch();

                WriteMetadata(writer, config);
                // The blib carries its own validity record, so SecondPassFDR's resume check
                // reads it from the library rather than from a file beside it.
                writer.AddStamp(stamp);

                CompleteBlib(writer, config.OutputBlib);
            }
        }

        /// <summary>
        /// Remove an earlier run's .blib at the output name, for a run that writes none. A locked
        /// file is reported as a <see cref="BlibOutputException"/>, as a failure to replace it is.
        /// </summary>
        internal static void RemoveOutput(string outputPath)
        {
            try
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new BlibOutputException(outputPath, ex);
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
        private static OspreyBlibWriter OpenBlibWriter(string outputPath)
        {
            try
            {
                return new OspreyBlibWriter(outputPath);
            }
            catch (Exception ex) when (IsFileAccessFailure(ex))
            {
                throw new BlibOutputException(outputPath, ex);
            }
        }

        /// <summary>
        /// Complete the library and move it over the output. A failure to put it in place (the
        /// output is open in another program, or the folder became unwritable) is reported as a
        /// <see cref="BlibOutputException"/> naming the output, as a failure to open it is.
        /// </summary>
        private static void CompleteBlib(OspreyBlibWriter writer, string outputPath)
        {
            try
            {
                writer.Complete();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
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
            OspreyBlibWriter writer, OspreyConfig config,
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
        // The library's own protein list is left off: a search blib's Proteins rows are the
        // parsimony protein groups, which EmitSpectrumRows writes.
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
                            spectra[i] = BlibSpectrum.FromLibraryEntry(libEntry, Array.Empty<string>());
                    });
            }
            return spectra;
        }

        // Sequential per-best-precursor emission: one RefSpectra row (plus its
        // modifications / protein group mappings) for each prepared spectrum, in iteration
        // order so row IDs stay deterministic.
        private static Dictionary<(string, byte), long> EmitSpectrumRows(
            OspreyBlibWriter writer,
            List<KeyValuePair<string, FdrEntry>> blibEntries,
            BlibSpectrum[] spectra,
            ProteinGroupRows proteinGroups,
            Dictionary<string, long> sourceFileIds,
            Dictionary<(string, byte), double> bestExpPrecursorQ,
            Dictionary<(string, string), double[]> sharedBounds,
            Dictionary<(string, byte), (bool AnyPassesRunFdr, string BestRunFile, int NRuns)> precursorFacts)
        {
            var refIdByPrecursor = new Dictionary<(string, byte), long>(blibEntries.Count);
            // Reported for the same reason as the pre-compress pass above: this emits several
            // row families per spectrum into SQLite and ran silent inside the same 47 s gap.
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
                    // Rust pipeline.rs:4670-4683 / 4795.
                    var lookupKey = (entry.ModifiedSequence, entry.Charge);
                    double scoreQvalue;
                    if (!bestExpPrecursorQ.TryGetValue(lookupKey, out scoreQvalue))
                        scoreQvalue = entry.ExperimentPrecursorQvalue;

                    // nRunsDetected -> RefSpectra.copies (Rust pipeline.rs:6179
                    // passes n_runs_detected = group.len()).
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

                    // The library precursor's own rows - peaks and modifications - exactly as
                    // --export-library writes them; this row adds the search result's apex,
                    // boundaries, q-value and run count, and its experiment-level values: the
                    // precursor q-value is the one in score; Pep is one value per entry for the
                    // whole analysis, so any observation of the precursor carries it; the
                    // peptide q-value is computed once per modified sequence, so every charge
                    // state's row gets the same value.
                    long refId = writer.AddSpectrum(spectrum,
                        sharedApex, sharedStart, sharedEnd,
                        scoreQvalue, fileId, nRunsDetected,
                        new SearchGlobalValues(scoreQvalue, entry.Pep, entry.ExperimentPeptideQvalue));
                    proteinGroups.AddMappings(refId, entry.ModifiedSequence);

                    refIdByPrecursor[lookupKey] = refId;
                }
            }
            return refIdByPrecursor;
        }

        // The LibMetadata rows: the values Rust's write_blib_from_plan (pipeline.rs:6078-6081)
        // records, under Osprey's namespaced keys, plus the command line that made the library.
        private static void WriteMetadata(OspreyBlibWriter writer, OspreyConfig config)
        {
            writer.AddMetadata(OspreyBlibWriter.KEY_VERSION, OspreyVersion.Current);
            writer.AddMetadata(OspreyBlibWriter.KEY_SEARCH_MODE, @"coelution");
            writer.AddMetadata(OspreyBlibWriter.KEY_RUN_FDR,
                config.RunFdr.ToString(CultureInfo.InvariantCulture));
            writer.AddMetadata(OspreyBlibWriter.KEY_EXPERIMENT_FDR,
                config.ExperimentFdr.ToString(CultureInfo.InvariantCulture));
            if (config.CommandLine != null)
                writer.AddMetadata(OspreyBlibWriter.KEY_COMMAND, config.CommandLine);
            // The runs the library's models were tuned on, which SpectrumSourceFiles does not name
            // when they were searched for the tuning only.
            if (config.LibraryBuild.Tuning)
                writer.AddMetadata(OspreyBlibWriter.KEY_TRAINING_INPUT, string.Join(@";", config.LibraryBuild.TrainingInputs));
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
            OspreyBlibWriter writer,
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

        /// <summary>
        /// The <c>Proteins</c> and <c>RefSpectraProteins</c> rows of a search blib. Each
        /// <c>Proteins</c> row is one parsimony protein group: its accessions, sorted and joined
        /// by semicolons, and its experiment-level picked-protein q-value. A group is written the
        /// first time a spectrum links to it.
        ///
        /// <para>A peptide links to every group the parsimony assigned it to - several under the
        /// default shared-peptide mode, one under razor. A peptide with no group (a shared peptide
        /// under the unique mode, or a library entry with no proteins) gets no rows, which matches
        /// the protein q-value of 1.0 Osprey itself gives it. A group with no target winner in the
        /// picked-protein competition also gets 1.0, as in the protein group report.</para>
        /// </summary>
        private sealed class ProteinGroupRows
        {
            private readonly OspreyBlibWriter _writer;
            private readonly SecondPassProteinFdrResult _proteinFdr;
            private readonly Dictionary<uint, long> _rowIdByGroup = new Dictionary<uint, long>();

            public ProteinGroupRows(OspreyBlibWriter writer, SecondPassProteinFdrResult proteinFdr)
            {
                _writer = writer;
                _proteinFdr = proteinFdr;
            }

            public void AddMappings(long refId, string modifiedSequence)
            {
                if (!_proteinFdr.Parsimony.PeptideToGroupMap.TryGetValue(modifiedSequence, out var groupIds))
                    return;
                foreach (uint groupId in groupIds)
                    _writer.AddSpectrumProtein(refId, GetRowId(groupId));
            }

            private long GetRowId(uint groupId)
            {
                if (_rowIdByGroup.TryGetValue(groupId, out long rowId))
                    return rowId;
                // Group IDs are indices into Groups (BuildProteinParsimony assigns them that way).
                var accessions = new List<string>(_proteinFdr.Parsimony.Groups[(int)groupId].Accessions);
                accessions.Sort(StringComparer.Ordinal); // Array.Sort OK: distinct accession strings, so the joined key is the same in any tie order
                double qValue = _proteinFdr.ProteinFdr.GroupQvalues.TryGetValue(groupId, out double q) ? q : 1.0;
                rowId = _writer.AddProteinGroup(string.Join(@";", accessions), qValue);
                _rowIdByGroup[groupId] = rowId;
                return rowId;
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
