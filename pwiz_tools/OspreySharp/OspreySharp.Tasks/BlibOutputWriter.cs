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

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.IO;

namespace pwiz.OspreySharp.Tasks
{
    /// <summary>
    /// Orchestrates the BiblioSpecLite <c>.blib</c> output emission for the merge
    /// node (Stage 9): source-file IDs, a parallel zlib pre-compress pass, the
    /// sequential per-best-precursor RefSpectra + modifications + protein +
    /// RetentionTimes + Osprey extension-table emission, then metadata and
    /// finalize. Drives the low-level <see cref="BlibWriter"/> (the SQLite layer);
    /// this type owns only the per-spectrum row composition.
    ///
    /// Extracted verbatim from <c>MergeNodeTask.WriteBlibFile</c> as pure code
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
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            Dictionary<(string, byte), KeyValuePair<string, FdrEntry>> bestByPrecursor,
            Dictionary<(string, byte), double> bestExpPrecursorQ,
            Dictionary<(string, string), double[]> sharedBounds,
            Dictionary<(string, byte), List<KeyValuePair<string, FdrEntry>>> entriesByPrecursor)
        {
            double fdrThreshold = config.RunFdr; // run-level threshold for ID-line semantics
            using (var writer = new BlibWriter(config.OutputBlib))
            {
                writer.BeginBatch();

                var sourceFileIds = CreateSourceFiles(writer, config, perFileEntries, fdrThreshold);

                var blibEntries = bestByPrecursor.Values.ToList();
                PrecompressSpectra(blibEntries, libraryById, config.NThreads,
                    out byte[][] blibMzBlobs, out byte[][] blibIntBlobs, out int[] blibNumPeaks);

                EmitSpectrumRows(writer, blibEntries, blibMzBlobs, blibIntBlobs, blibNumPeaks,
                    sourceFileIds, libraryById, bestExpPrecursorQ, sharedBounds,
                    entriesByPrecursor, perFileEntries.Count, fdrThreshold);

                writer.Commit();

                WriteMetadata(writer, config);

                writer.FinalizeDatabase();
            }
        }

        // Pre-create source file IDs once. SpectrumSourceFiles.idFileName
        // carries the library filename (Skyline expects this — Rust
        // pipeline.rs:6110 + blib.rs:435). The library file is the "ID
        // source"; the mzML file is the spectrum source.
        private static Dictionary<string, long> CreateSourceFiles(
            BlibWriter writer, OspreyConfig config,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries, double fdrThreshold)
        {
            string libraryIdName = Path.GetFileName(config.LibrarySource.Path);
            var sourceFileIds = new Dictionary<string, long>();
            foreach (var kvp in perFileEntries)
            {
                sourceFileIds[kvp.Key] = writer.AddSourceFile(
                    kvp.Key + ".mzML", libraryIdName, fdrThreshold);
            }
            return sourceFileIds;
        }

        // Parallel pre-compress pass. Per-spectrum zlib dominates the blib
        // write wall; pre-compute (mzBlob, intBlob, numPeaks) for every
        // entry in parallel, then drive AddSpectrumPrecompressed in
        // iteration order so RefSpectra row IDs stay deterministic.
        private static void PrecompressSpectra(
            List<KeyValuePair<string, FdrEntry>> blibEntries,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById, int nThreads,
            out byte[][] blibMzBlobs, out byte[][] blibIntBlobs, out int[] blibNumPeaks)
        {
            int blibN = blibEntries.Count;
            var mzBlobs = new byte[blibN][];
            var intBlobs = new byte[blibN][];
            var numPeaks = new int[blibN];
            Parallel.For(0, blibN,
                new ParallelOptions { MaxDegreeOfParallelism = nThreads },
                i =>
                {
                    var entry = blibEntries[i].Value;
                    LibraryEntry libEntryP;
                    if (!libraryById.TryGetValue(entry.EntryId, out libEntryP))
                        return;
                    int nFrags = libEntryP.Fragments.Count;
                    var mzsP = new double[nFrags];
                    var intsP = new float[nFrags];
                    for (int j = 0; j < nFrags; j++)
                    {
                        mzsP[j] = libEntryP.Fragments[j].Mz;
                        intsP[j] = libEntryP.Fragments[j].RelativeIntensity;
                    }
                    mzBlobs[i] = BlibWriter.CompressMzs(mzsP);
                    intBlobs[i] = BlibWriter.CompressIntensities(intsP);
                    numPeaks[i] = nFrags;
                });
            blibMzBlobs = mzBlobs;
            blibIntBlobs = intBlobs;
            blibNumPeaks = numPeaks;
        }

        // Sequential per-best-precursor emission: one RefSpectra row (plus its
        // modifications / protein mappings / RetentionTimes / Osprey extension
        // rows) for each pre-compressed entry, in iteration order so row IDs stay
        // deterministic.
        private static void EmitSpectrumRows(
            BlibWriter writer,
            List<KeyValuePair<string, FdrEntry>> blibEntries,
            byte[][] blibMzBlobs, byte[][] blibIntBlobs, int[] blibNumPeaks,
            Dictionary<string, long> sourceFileIds,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            Dictionary<(string, byte), double> bestExpPrecursorQ,
            Dictionary<(string, string), double[]> sharedBounds,
            Dictionary<(string, byte), List<KeyValuePair<string, FdrEntry>>> entriesByPrecursor,
            int perFileEntriesCount, double fdrThreshold)
        {
            for (int blibIdx = 0; blibIdx < blibEntries.Count; blibIdx++)
            {
                var kvp = blibEntries[blibIdx];
                string fileName = kvp.Key;
                var entry = kvp.Value;

                LibraryEntry libEntry;
                if (!libraryById.TryGetValue(entry.EntryId, out libEntry))
                    continue;

                long fileId = sourceFileIds[fileName];

                byte[] mzBlobPre = blibMzBlobs[blibIdx];
                byte[] intBlobPre = blibIntBlobs[blibIdx];
                int numPeaksPre = blibNumPeaks[blibIdx];

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
                List<KeyValuePair<string, FdrEntry>> observations;
                int nRunsDetected = 1;
                if (entriesByPrecursor.TryGetValue(lookupKey, out observations) &&
                    observations.Count > 0)
                {
                    nRunsDetected = observations.Count;
                }

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

                long refId = writer.AddSpectrumPrecompressed(
                    libEntry.Sequence,
                    libEntry.ModifiedSequence,
                    libEntry.PrecursorMz,
                    libEntry.Charge,
                    sharedApex,
                    sharedStart,
                    sharedEnd,
                    mzBlobPre, intBlobPre, numPeaksPre,
                    scoreQvalue, fileId, nRunsDetected, 0.0);

                // Add modifications
                if (libEntry.Modifications != null && libEntry.Modifications.Count > 0)
                    writer.AddModifications(refId, libEntry.Modifications);

                // Add protein mappings
                if (libEntry.ProteinIds != null && libEntry.ProteinIds.Count > 0)
                    writer.AddProteinMapping(refId, libEntry.ProteinIds);

                WriteRetentionTimes(writer, refId, fileName, observations,
                    sourceFileIds, sharedBounds, fdrThreshold);

                // Osprey extension tables — one row per RefSpectra each,
                // mirroring Rust pipeline.rs:6255-6272. Best-run-only for
                // OspreyPeakBoundaries + OspreyRunScores; experiment-level for
                // OspreyExperimentScores. The 0.0 fields are the same "not yet
                // plumbed through Stage 7 plan entries" placeholders Rust writes.
                writer.AddPeakBoundaries(refId, fileName,
                    sharedStart, sharedEnd, sharedApex,
                    0.0, // ApexIntensity — matches Rust's apex_coefficient placeholder
                    entry.BoundsArea);
                writer.AddRunScores(refId, fileName,
                    entry.EffectiveRunQvalue(FdrLevel.Both),
                    0.0, // DiscriminantScore — matches Rust's dot_product placeholder
                    0.0); // PosteriorErrorProb — matches Rust's PEP placeholder
                writer.AddExperimentScores(refId,
                    scoreQvalue, // Same value as RefSpectra.score
                    nRunsDetected,
                    perFileEntriesCount);
            }
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

        // Per-observation RetentionTimes rows — one row for EVERY run where this
        // precursor was detected. retentionTime (drives Skyline ID-line display)
        // is populated iff the run passes run-level FDR, OR (fallback) no run
        // passes and this is the best run by lowest run_qvalue. Cross-charge
        // shared boundaries applied. Mirrors Rust pipeline.rs:6191-6243.
        private static void WriteRetentionTimes(
            BlibWriter writer, long refId, string fileName,
            List<KeyValuePair<string, FdrEntry>> observations,
            Dictionary<string, long> sourceFileIds,
            Dictionary<(string, string), double[]> sharedBounds,
            double fdrThreshold)
        {
            if (observations == null)
                return;
            // Compute the fallback ID-line file: if NO run passes run-level FDR,
            // the run with the lowest run_qvalue gets the ID line so every blib
            // RefSpectra has at least one ID line.
            bool anyPassesRunFdr = false;
            string bestRunFile = null;
            double bestRunQ = double.MaxValue;
            foreach (var obs in observations)
            {
                double rq = obs.Value.EffectiveRunQvalue(FdrLevel.Both);
                if (rq <= fdrThreshold)
                    anyPassesRunFdr = true;
                if (rq < bestRunQ)
                {
                    bestRunQ = rq;
                    bestRunFile = obs.Key;
                }
            }

            foreach (var obs in observations)
            {
                long srcId = sourceFileIds[obs.Key];
                var fileEntry = obs.Value;
                double runQ = fileEntry.EffectiveRunQvalue(FdrLevel.Both);
                bool passesFdr = runQ <= fdrThreshold;
                bool showIdLine = passesFdr ||
                    (!anyPassesRunFdr && obs.Key == bestRunFile);
                bool isBest = obs.Key == fileName;

                var runSharedKey = (fileEntry.ModifiedSequence, obs.Key);
                double runApex = fileEntry.ApexRt;
                double runStart = fileEntry.StartRt;
                double runEnd = fileEntry.EndRt;
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
