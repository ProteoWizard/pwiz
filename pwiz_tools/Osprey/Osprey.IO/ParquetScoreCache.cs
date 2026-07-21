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
using System.Linq;
using System.Threading.Tasks;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Parquet-based score cache for persisting coelution search results.
    /// Ported from osprey/src/pipeline.rs (write_scores_parquet, load_fdr_stubs_from_parquet,
    /// load_pin_features_from_parquet).
    ///
    /// Uses ParquetNet for columnar I/O. The Parquet schema matches the Rust implementation
    /// to enable cross-platform cache compatibility.
    /// </summary>
    public static class ParquetScoreCache
    {
        #region PIN Feature Names

        /// <summary>
        /// The 21 PIN feature column names, matching the Rust get_pin_feature_names().
        /// </summary>
        public static readonly string[] PIN_FEATURE_NAMES =
        {
            // Pairwise coelution (3)
            "fragment_coelution_sum",
            "fragment_coelution_max",
            "n_coeluting_fragments",
            // Peak shape (3)
            "peak_apex",
            "peak_area",
            "peak_sharpness",
            // Spectral at apex (3)
            "xcorr",
            "consecutive_ions",
            "explained_intensity",
            // Mass accuracy (2)
            "mass_accuracy_deviation_mean",
            "abs_mass_accuracy_deviation_mean",
            // RT deviation (2)
            "rt_deviation",
            "abs_rt_deviation",
            // MS1 (2)
            "ms1_precursor_coelution",
            "ms1_isotope_cosine",
            // Median polish (2)
            "median_polish_cosine",
            "median_polish_residual_ratio",
            // SG-weighted multi-scan (4)
            "sg_weighted_xcorr",
            "sg_weighted_cosine",
            "median_polish_min_fragment_r2",
            "median_polish_residual_correlation",
        };

        public const int NUM_PIN_FEATURES = 21;

        #endregion

        #region Schema Fields

        // Schema column types and names are aligned with the Rust impl's
        // parquet schema (UInt32 for entry_id/scan_number, UInt8 for charge)
        // so a C#-written parquet can be loaded by Rust's `--task FirstJoin`
        // (which does strict downcasts) and vice versa. Reading is also
        // strict: pre-2026-04-19 C#-written parquets used Int32 for these
        // fields and need to be regenerated via a fresh `--task PerFileScoring` run.
        //
        // Fields are declared in the same order Rust writes them. Order
        // doesn't affect Parquet correctness (columns are name-indexed),
        // but matching makes diffing easier.
        private static readonly DataField FIELD_ENTRY_ID = new DataField<uint>("entry_id");
        private static readonly DataField FIELD_IS_DECOY = new DataField<bool>("is_decoy");
        private static readonly DataField FIELD_SEQUENCE = new DataField<string>("sequence");
        private static readonly DataField FIELD_MODIFIED_SEQUENCE = new DataField<string>("modified_sequence");
        private static readonly DataField FIELD_CHARGE = new DataField<byte>("charge");
        private static readonly DataField FIELD_PRECURSOR_MZ = new DataField<double>("precursor_mz");
        private static readonly DataField FIELD_PROTEIN_IDS = new DataField("protein_ids", typeof(string), isNullable: true, isArray: false);
        private static readonly DataField FIELD_SCAN_NUMBER = new DataField<uint>("scan_number");
        private static readonly DataField FIELD_APEX_RT = new DataField<double>("apex_rt");
        private static readonly DataField FIELD_START_RT = new DataField<double>("start_rt");
        private static readonly DataField FIELD_END_RT = new DataField<double>("end_rt");
        private static readonly DataField FIELD_BOUNDS_AREA = new DataField<double>("bounds_area");
        private static readonly DataField FIELD_BOUNDS_SNR = new DataField<double>("bounds_snr");
        private static readonly DataField FIELD_FILE_NAME = new DataField<string>("file_name");
        // Binary blobs that Rust's reconciliation/gap-fill code paths read.
        // C# writes them as nullable placeholders so the schema bit-matches
        // Rust's; populating them with the actual fragment/XIC/CWT byte
        // serialization is a future sprint (Stage 5+8 cross-impl works
        // without them).
        private static readonly DataField FIELD_CWT_CANDIDATES = new DataField("cwt_candidates", typeof(byte[]), isNullable: true, isArray: false);
        private static readonly DataField FIELD_FRAGMENT_MZS = new DataField("fragment_mzs", typeof(byte[]), isNullable: true, isArray: false);
        private static readonly DataField FIELD_FRAGMENT_INTENSITIES = new DataField("fragment_intensities", typeof(byte[]), isNullable: true, isArray: false);
        private static readonly DataField FIELD_REFERENCE_XIC_RTS = new DataField("reference_xic_rts", typeof(byte[]), isNullable: true, isArray: false);
        private static readonly DataField FIELD_REFERENCE_XIC_INTENSITIES = new DataField("reference_xic_intensities", typeof(byte[]), isNullable: true, isArray: false);
        // Reader-only alias for the fragment_coelution_sum PIN feature
        // column (the same column is read both as a stub for FDR loading
        // and as one of the 21 PIN features). Not added to the write
        // schema -- it's already there via BuildFeatureFields().
        private static readonly DataField FIELD_COELUTION_SUM = new DataField<double>("fragment_coelution_sum");

        private static DataField[] BuildFeatureFields()
        {
            var fields = new DataField[NUM_PIN_FEATURES];
            for (int i = 0; i < NUM_PIN_FEATURES; i++)
                fields[i] = new DataField<double>(PIN_FEATURE_NAMES[i]);
            return fields;
        }

        // Parquet.Net 4.x requires the DataField passed to DataColumn's ctor
        // to be the same instance attached to the schema. The caller builds
        // featureFields once and passes the array here so the same instances
        // can be reused for the WriteColumnAsync calls.
        private static ParquetSchema BuildWriteSchema(DataField[] featureFields)
        {
            // Order matches Rust's pipeline.rs build of `write_scores_parquet_with_metadata`.
            // Field order is informational only -- Parquet is name-indexed.
            var fields = new List<DataField>
            {
                FIELD_ENTRY_ID,
                FIELD_IS_DECOY,
                FIELD_SEQUENCE,
                FIELD_MODIFIED_SEQUENCE,
                FIELD_CHARGE,
                FIELD_PRECURSOR_MZ,
                FIELD_PROTEIN_IDS,
                FIELD_SCAN_NUMBER,
                FIELD_APEX_RT,
                FIELD_START_RT,
                FIELD_END_RT,
                FIELD_BOUNDS_AREA,
                FIELD_BOUNDS_SNR,
                FIELD_FILE_NAME,
                FIELD_CWT_CANDIDATES,
                FIELD_FRAGMENT_MZS,
                FIELD_FRAGMENT_INTENSITIES,
                FIELD_REFERENCE_XIC_RTS,
                FIELD_REFERENCE_XIC_INTENSITIES,
            };
            fields.AddRange(featureFields);
            return new ParquetSchema(fields.ToArray());
        }

        #endregion

        #region Write

        // Cap each Parquet row group at this many rows so the writer materializes,
        // compresses, and flushes one chunk's columns at a time instead of the whole
        // file. The rows are emitted in the same global (entry_id, charge, scan_number)
        // order as a single-group write, so the logical rows, their order, and the
        // read-side ParquetIndex (running row position) are unchanged -- only the
        // physical row-group framing differs. No gate compares parquet bytes (the
        // regression + cross-impl gates compare the blib + protein-FDR at 1e-9), and
        // every reader already loops RowGroupCount, so N groups read back identically
        // to one. See ai/todos/active/TODO-20260716_osprey_parquet_bounded_rowgroup_write.md.
        private const int MAX_ROWS_PER_ROW_GROUP = 100_000;

        // Test seam: when non-null, overrides MAX_ROWS_PER_ROW_GROUP so a unit test can
        // force several row groups from a handful of rows and assert the multi-group
        // round-trip is logically identical. Always null in production.
        internal static int? RowGroupRowCapForTest;

        /// <summary>
        /// Write scored entries to a Parquet file.
        /// Schema columns: entry_id, is_decoy, charge, scan_number, modified_sequence,
        /// apex_rt, start_rt, end_rt, then 21 PIN feature columns.
        /// Metadata key-value pairs are written to the Parquet footer.
        /// </summary>
        public static void WriteScoresParquet(string path, List<CoelutionScoredEntry> entries,
            Dictionary<string, string> metadata)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (entries == null || entries.Count == 0)
                return;

            int n = entries.Count;
            var featureFields = BuildFeatureFields();
            var schema = BuildWriteSchema(featureFields);

            // Iterate in canonical sorted order (entry_id, charge, scan_number)
            // so per-side parquets have identical physical row order across the
            // Rust and C# impls. Order-sensitive consumers downstream (Stage 5
            // standardizer, SVM training) then see the same row sequence
            // regardless of which side wrote the parquet. Mirrors Rust
            // pipeline.rs::write_scores_parquet_with_metadata. The row groups
            // written below preserve this order across chunk boundaries.
            var sortedIndices = Enumerable.Range(0, n)
                .OrderBy(idx => entries[idx].EntryId)
                .ThenBy(idx => entries[idx].Charge)
                .ThenBy(idx => entries[idx].ScanNumber)
                .ToArray();

            // Build and write one bounded row group at a time. Each chunk
            // materializes only its own column arrays for rows [start, start+count)
            // in the sorted order above; they are released once the group is
            // flushed, so peak residency is one row group rather than all n rows.
            WriteChunkedParquet(path, schema, metadata, n, (start, count) =>
            {
                var entryIds = new uint[count];
                var isDecoys = new bool[count];
                var sequences = new string[count];
                var modifiedSequences = new string[count];
                var charges = new byte[count];
                var precursorMzs = new double[count];
                var proteinIds = new string[count];
                var scanNumbers = new uint[count];
                var apexRts = new double[count];
                var startRts = new double[count];
                var endRts = new double[count];
                var boundsAreas = new double[count];
                var boundsSnrs = new double[count];
                var fileNames = new string[count];
                var cwtCandidates = new byte[count][];
                var fragmentMzs = new byte[count][];
                var fragmentIntensities = new byte[count][];
                var refXicRts = new byte[count][];
                var refXicIntensities = new byte[count][];
                var featureArrays = new double[NUM_PIN_FEATURES][];
                for (int f = 0; f < NUM_PIN_FEATURES; f++)
                    featureArrays[f] = new double[count];

                for (int j = 0; j < count; j++)
                {
                    var entry = entries[sortedIndices[start + j]];
                    entryIds[j] = entry.EntryId;
                    isDecoys[j] = entry.IsDecoy;
                    sequences[j] = entry.Sequence ?? string.Empty;
                    modifiedSequences[j] = entry.ModifiedSequence ?? string.Empty;
                    charges[j] = entry.Charge;
                    precursorMzs[j] = entry.PrecursorMz;
                    proteinIds[j] = entry.ProteinIds != null
                        ? string.Join(";", entry.ProteinIds)
                        : null;
                    scanNumbers[j] = entry.ScanNumber;
                    apexRts[j] = entry.ApexRt;
                    startRts[j] = entry.PeakBounds != null ? entry.PeakBounds.StartRt : 0.0;
                    endRts[j] = entry.PeakBounds != null ? entry.PeakBounds.EndRt : 0.0;
                    boundsAreas[j] = entry.PeakBounds != null ? entry.PeakBounds.Area : 0.0;
                    boundsSnrs[j] = entry.PeakBounds != null ? entry.PeakBounds.SignalToNoise : 0.0;
                    fileNames[j] = entry.FileName ?? string.Empty;

                    ExtractPinFeatures(entry.Features, featureArrays, j);
                }

                return BuildRowGroupColumns(
                    entryIds, isDecoys, sequences, modifiedSequences, charges,
                    precursorMzs, proteinIds, scanNumbers, apexRts, startRts, endRts,
                    boundsAreas, boundsSnrs, fileNames, cwtCandidates, fragmentMzs,
                    fragmentIntensities, refXicRts, refXicIntensities,
                    featureFields, featureArrays);
            });
        }

        /// <summary>
        /// Write FdrEntry results to a Parquet file. Same schema as the
        /// CoelutionScoredEntry overload — used by --task PerFileScoring HPC mode where
        /// the pipeline keeps full features on the FdrEntry directly
        /// (FdrEntry.Features is the already-extracted 21-feature vector).
        /// Library lookup (by entry_id) supplies the sequence / precursor_mz
        /// / protein_ids columns that Rust expects in the schema.
        /// </summary>
        public static void WriteScoresParquet(string path, List<FdrEntry> entries,
            Dictionary<string, string> metadata,
            Dictionary<uint, LibraryEntry> libraryById,
            string fileName)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (entries == null || entries.Count == 0)
                return;

            int n = entries.Count;
            var featureFields = BuildFeatureFields();
            var schema = BuildWriteSchema(featureFields);

            // Iterate in canonical sorted order (entry_id, charge, scan_number)
            // so per-side parquets have identical physical row order across
            // Rust and C# impls. Order-sensitive consumers downstream (Stage 5
            // standardizer, SVM training) then see the same row sequence
            // regardless of which side wrote the parquet. Mirrors Rust
            // pipeline.rs::write_scores_parquet_with_metadata. ParquetIndex is
            // assigned to the post-sort destination row below (the global row
            // position, preserved across the bounded row-group boundaries).
            var sortedIndices = Enumerable.Range(0, n)
                .OrderBy(idx => entries[idx].EntryId)
                .ThenBy(idx => entries[idx].Charge)
                .ThenBy(idx => entries[idx].ScanNumber)
                .ToArray();

            // Build and write one bounded row group at a time. Each chunk
            // materializes only its own column arrays (including the heavy blob
            // columns) for rows [start, start+count) in the sorted order above,
            // then releases them once the group is flushed -- so peak residency
            // is one row group's columns + native compression buffers rather
            // than the whole file's. BuildFdrEntryColumns holds the per-row column
            // build so the Stage-6 streaming reconciled transfer reuses the exact
            // same layout per row group.
            WriteChunkedParquet(path, schema, metadata, n, (start, count) =>
            {
                var chunk = new FdrEntry[count];
                for (int j = 0; j < count; j++)
                    chunk[j] = entries[sortedIndices[start + j]];
                return BuildFdrEntryColumns(chunk, start, libraryById, fileName, featureFields);
            });
        }

        /// <summary>
        /// Assemble one row group's <see cref="DataColumn"/> list from a chunk of
        /// <see cref="FdrEntry"/> rows already in their final output order. Each
        /// entry's <see cref="FdrEntry.ParquetIndex"/> is (re)assigned to its global
        /// row position <paramref name="startIndex"/> + j, matching
        /// <see cref="LoadFdrStubsFromParquet"/>'s read-side "ParquetIndex = row"
        /// convention. Shared by the chunked <see cref="WriteScoresParquet(string,List{FdrEntry},Dictionary{string,string},Dictionary{uint,LibraryEntry},string)"/>
        /// write and the Stage-6 streaming reconciled transfer
        /// (<see cref="StreamReconciledScoresParquet"/>) so both emit byte-identical
        /// row groups.
        /// </summary>
        private static List<DataColumn> BuildFdrEntryColumns(IReadOnlyList<FdrEntry> entries,
            int startIndex, Dictionary<uint, LibraryEntry> libraryById, string fileName,
            DataField[] featureFields)
        {
            int count = entries.Count;
            var entryIds = new uint[count];
            var isDecoys = new bool[count];
            var sequences = new string[count];
            var modifiedSequences = new string[count];
            var charges = new byte[count];
            var precursorMzs = new double[count];
            var proteinIds = new string[count];
            var scanNumbers = new uint[count];
            var apexRts = new double[count];
            var startRts = new double[count];
            var endRts = new double[count];
            var boundsAreas = new double[count];
            var boundsSnrs = new double[count];
            var fileNames = new string[count];
            // The blob columns below carry per-entry binary payloads
            // matching Rust pipeline.rs:1620-1645's encoding:
            //   cwt_candidates             = u32 LE count + N×(6×f64 LE)
            //   fragment_mzs               = M×f64 LE   (no count prefix)
            //   fragment_intensities       = M×f32 LE   (no count prefix)
            //   reference_xic_rts          = K×f64 LE
            //   reference_xic_intensities  = K×f64 LE
            // Length is recovered on read as bytes / sizeof(element).
            var cwtCandidates = new byte[count][];
            var fragmentMzs = new byte[count][];
            var fragmentIntensities = new byte[count][];
            var refXicRts = new byte[count][];
            var refXicIntensities = new byte[count][];
            var featureArrays = new double[NUM_PIN_FEATURES][];
            for (int f = 0; f < NUM_PIN_FEATURES; f++)
                featureArrays[f] = new double[count];

            for (int j = 0; j < count; j++)
            {
                var entry = entries[j];
                // Assign ParquetIndex to match the global row position we are
                // about to write. Mirrors LoadFdrStubsFromParquet, which
                // assigns ParquetIndex = row on read. Without this
                // assignment, in-memory entries reach Stage 5
                // ReconciliationPlanner with ParquetIndex = 0 (FdrEntry
                // default), and every entry's per-file CWT lookup
                // (fileCwt[entry.ParquetIndex]) grabs the first row's
                // CwtCandidate list instead of its own -- the planner
                // then force-integrates almost every entry because the
                // wrong CWT list has no candidate near the expected RT.
                // The HPC chain path was unaffected because its entries
                // are reloaded via LoadFdrStubsFromParquet, which sets
                // ParquetIndex correctly. Found by C# in-memory vs
                // C# HPC-chain strict-rehydration bisection on Stellar
                // (Stage 5 boundary check: .1st-pass.fdr_scores.bin
                // byte-identical but reconciliation.json action shape
                // diverged -- 35K use_cwt actions on HPC side, 814 on
                // in-memory side, total identical).
                entry.ParquetIndex = (uint)(startIndex + j);
                entryIds[j] = entry.EntryId;
                isDecoys[j] = entry.IsDecoy;
                charges[j] = entry.Charge;
                scanNumbers[j] = entry.ScanNumber;
                modifiedSequences[j] = entry.ModifiedSequence ?? string.Empty;
                apexRts[j] = entry.ApexRt;
                startRts[j] = entry.StartRt;
                endRts[j] = entry.EndRt;
                boundsAreas[j] = entry.BoundsArea;
                boundsSnrs[j] = entry.BoundsSnr;
                fileNames[j] = fileName ?? string.Empty;
                fragmentMzs[j] = EncodeF64Blob(entry.FragmentMzs);
                fragmentIntensities[j] = EncodeF32Blob(entry.FragmentIntensities);
                refXicRts[j] = EncodeF64Blob(entry.ReferenceXicRts);
                refXicIntensities[j] = EncodeF64Blob(entry.ReferenceXicIntensities);

                // Mirror Rust's invariant: every row carries a cwt_candidates
                // blob, even when the candidate list is empty. Rust's
                // pipeline.rs::write_scores_parquet (at the cwt_candidates
                // serialization site) unconditionally appends a 4-byte
                // little-endian count prefix, so an entry with zero
                // candidates becomes a 4-byte zero-length blob, never a
                // null cell. ~57k post-compaction stubs per Stellar file
                // had no peaks; without this normalization C# emitted
                // null cells while Rust emitted empty blobs, producing
                // a spurious cross-impl parquet diff at end-of-Stage-6.
                //
                // TODO(osprey-rust): the proper fix is on the Rust side --
                // pipeline.rs should write null for empty candidate lists,
                // which is more parquet-idiomatic and saves 4 bytes per
                // empty row for downstream consumers. When that lands in
                // maccoss/osprey, revert this branch to the original
                // "skip null/empty" form.
                // Use Array.Empty<>() (not `new List<>()`) on the null
                // branch so we still emit the 4-byte zero-count blob
                // without allocating a fresh List per empty row.
                // CwtCandidateCodec.Encode takes IReadOnlyList<CwtCandidate>
                // which both List<T> and T[] satisfy.
                cwtCandidates[j] = CwtCandidateCodec.Encode(
                    entry.CwtCandidates ?? (IReadOnlyList<CwtCandidate>)Array.Empty<CwtCandidate>());

                LibraryEntry libEntry = null;
                if (libraryById != null)
                    libraryById.TryGetValue(entry.EntryId, out libEntry);
                if (libEntry != null)
                {
                    sequences[j] = libEntry.Sequence ?? string.Empty;
                    precursorMzs[j] = libEntry.PrecursorMz;
                    proteinIds[j] = libEntry.ProteinIds != null
                        ? string.Join(";", libEntry.ProteinIds)
                        : null;
                }
                else
                {
                    sequences[j] = string.Empty;
                    precursorMzs[j] = 0.0;
                    proteinIds[j] = null;
                }

                var featureVec = entry.Features;
                if (featureVec != null && featureVec.Length == NUM_PIN_FEATURES)
                {
                    for (int f = 0; f < NUM_PIN_FEATURES; f++)
                        featureArrays[f][j] = Finite(featureVec[f]);
                }
                // else: leave zeros (entries without features can't drive Stage 5+).
            }

            return BuildRowGroupColumns(
                entryIds, isDecoys, sequences, modifiedSequences, charges,
                precursorMzs, proteinIds, scanNumbers, apexRts, startRts, endRts,
                boundsAreas, boundsSnrs, fileNames, cwtCandidates, fragmentMzs,
                fragmentIntensities, refXicRts, refXicIntensities,
                featureFields, featureArrays);
        }

        /// <summary>
        /// Assemble one row group's <see cref="DataColumn"/> list in the exact physical order
        /// both <see cref="WriteScoresParquet(string,List{CoelutionScoredEntry},Dictionary{string,string})"/>
        /// overloads write -- the 19 fixed columns followed by the 21 PIN feature columns.
        /// Centralizing the order keeps the two overloads identical. Parquet is name-indexed,
        /// so the order is informational for correctness, but it is kept identical to the
        /// prior explicit-call sequence so the within-group column layout is unchanged.
        /// </summary>
        private static List<DataColumn> BuildRowGroupColumns(
            uint[] entryIds, bool[] isDecoys, string[] sequences, string[] modifiedSequences,
            byte[] charges, double[] precursorMzs, string[] proteinIds, uint[] scanNumbers,
            double[] apexRts, double[] startRts, double[] endRts, double[] boundsAreas,
            double[] boundsSnrs, string[] fileNames, byte[][] cwtCandidates, byte[][] fragmentMzs,
            byte[][] fragmentIntensities, byte[][] refXicRts, byte[][] refXicIntensities,
            DataField[] featureFields, double[][] featureArrays)
        {
            var columns = new List<DataColumn>(19 + NUM_PIN_FEATURES)
            {
                new DataColumn(FIELD_ENTRY_ID, entryIds),
                new DataColumn(FIELD_IS_DECOY, isDecoys),
                new DataColumn(FIELD_SEQUENCE, sequences),
                new DataColumn(FIELD_MODIFIED_SEQUENCE, modifiedSequences),
                new DataColumn(FIELD_CHARGE, charges),
                new DataColumn(FIELD_PRECURSOR_MZ, precursorMzs),
                new DataColumn(FIELD_PROTEIN_IDS, proteinIds),
                new DataColumn(FIELD_SCAN_NUMBER, scanNumbers),
                new DataColumn(FIELD_APEX_RT, apexRts),
                new DataColumn(FIELD_START_RT, startRts),
                new DataColumn(FIELD_END_RT, endRts),
                new DataColumn(FIELD_BOUNDS_AREA, boundsAreas),
                new DataColumn(FIELD_BOUNDS_SNR, boundsSnrs),
                new DataColumn(FIELD_FILE_NAME, fileNames),
                new DataColumn(FIELD_CWT_CANDIDATES, cwtCandidates),
                new DataColumn(FIELD_FRAGMENT_MZS, fragmentMzs),
                new DataColumn(FIELD_FRAGMENT_INTENSITIES, fragmentIntensities),
                new DataColumn(FIELD_REFERENCE_XIC_RTS, refXicRts),
                new DataColumn(FIELD_REFERENCE_XIC_INTENSITIES, refXicIntensities),
            };
            for (int f = 0; f < NUM_PIN_FEATURES; f++)
                columns.Add(new DataColumn(featureFields[f], featureArrays[f]));
            return columns;
        }

        /// <summary>
        /// Write <paramref name="totalRows"/> rows to <paramref name="path"/> as a sequence
        /// of bounded row groups (at most <see cref="MAX_ROWS_PER_ROW_GROUP"/> rows each),
        /// invoking <paramref name="buildChunkColumns"/> to materialize each chunk's
        /// <see cref="DataColumn"/> list just before that group is written and releasing it
        /// after -- so peak residency is one row group's column arrays plus its native
        /// (Zstd / IronCompress) compression buffers, not the whole file's. The chunks are
        /// written in order, so the physical row sequence (and therefore the read-side
        /// ParquetIndex, a running row count) equals a single-group write of the same rows.
        /// Writes to a sibling temp file, then atomically renames into <paramref name="path"/>
        /// (safe NAS writes): the temp lives in the SAME directory as the destination, so the
        /// promote is an in-volume rename rather than a cross-volume copy that could truncate.
        /// The metadata footer is written once. A throttled row-level
        /// <see cref="ProgressReporter"/> covers the whole file so an Astral-scale write
        /// (~2.9M rows) reports percent instead of stalling silently.
        /// </summary>
        private static void WriteChunkedParquet(string path, ParquetSchema schema,
            Dictionary<string, string> metadata, int totalRows,
            Func<int, int, List<DataColumn>> buildChunkColumns)
        {
            using (var saver = new FileSaver(path))
            {
                using (var stream = new FileStream(saver.SafeName, FileMode.Create, FileAccess.Write))
                using (var writer = RunSync(ParquetWriter.CreateAsync(schema, stream)))
                using (var progress = new ProgressReporter(
                    string.Format("Writing {0} entries", totalRows), totalRows, string.Empty,
                    ProgressReporter.IO_INTERVAL_SECONDS))
                {
                    writer.CompressionMethod = CompressionMethod.Zstd;

                    // Set custom metadata if provided
                    if (metadata != null && metadata.Count > 0)
                        writer.CustomMetadata = metadata;

                    // Guard the (test-only) cap to at least 1 so a nonsensical
                    // RowGroupRowCapForTest (0 or negative) can never make the loop
                    // spin forever; production always uses MAX_ROWS_PER_ROW_GROUP.
                    int rowsPerGroup = Math.Max(1, RowGroupRowCapForTest ?? MAX_ROWS_PER_ROW_GROUP);
                    for (int start = 0; start < totalRows; start += rowsPerGroup)
                    {
                        int count = Math.Min(rowsPerGroup, totalRows - start);
                        var columns = buildChunkColumns(start, count);
                        using (var group = writer.CreateRowGroup())
                        {
                            WriteRowGroupColumns(group, columns);
                        }
                        progress.Report(start + count);
                    }
                }
                saver.Commit();
            }
        }

        /// <summary>
        /// Write the assembled <paramref name="columns"/> to <paramref name="group"/> in the
        /// <see cref="BuildRowGroupColumns"/> order. The write order (and therefore the
        /// parquet bytes within the group) is exactly that order.
        /// </summary>
        private static void WriteRowGroupColumns(ParquetRowGroupWriter group, List<DataColumn> columns)
        {
            foreach (var column in columns)
                RunSync(group.WriteColumnAsync(column));
        }

        /// <summary>
        /// Extract PIN feature values from a CoelutionFeatureSet into the column arrays.
        /// Matches the Rust pin_feature_value() ordering.
        /// </summary>
        private static void ExtractPinFeatures(CoelutionFeatureSet f, double[][] arrays, int row)
        {
            if (f == null)
            {
                for (int i = 0; i < NUM_PIN_FEATURES; i++)
                    arrays[i][row] = 0.0;
                return;
            }

            arrays[0][row] = Finite(f.CoelutionSum);
            arrays[1][row] = Finite(f.CoelutionMax);
            arrays[2][row] = Finite(f.NCoelutingFragments);
            arrays[3][row] = Finite(f.PeakApex);
            arrays[4][row] = Finite(f.PeakArea);
            arrays[5][row] = Finite(f.PeakSharpness);
            arrays[6][row] = Finite(f.Xcorr);
            arrays[7][row] = Finite(f.ConsecutiveIons);
            arrays[8][row] = Finite(f.ExplainedIntensity);
            arrays[9][row] = Finite(f.MassAccuracyMean);
            arrays[10][row] = Finite(f.AbsMassAccuracyMean);
            arrays[11][row] = Finite(f.RtDeviation);
            arrays[12][row] = Finite(f.AbsRtDeviation);
            arrays[13][row] = Finite(f.Ms1PrecursorCoelution);
            arrays[14][row] = Finite(f.Ms1IsotopeCosine);
            arrays[15][row] = Finite(f.MedianPolishCosine);
            arrays[16][row] = Finite(f.MedianPolishResidualRatio);
            arrays[17][row] = Finite(f.SgWeightedXcorr);
            arrays[18][row] = Finite(f.SgWeightedCosine);
            arrays[19][row] = Finite(f.MedianPolishMinFragmentR2);
            arrays[20][row] = Finite(f.MedianPolishResidualCorrelation);
        }

        private static double Finite(double v)
        {
            return double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : v;
        }

        // Synchronously bridge an async Parquet.Net call. GetAwaiter().GetResult()
        // rethrows the original exception instead of wrapping it in an
        // AggregateException. This is only safe because these calls run without a
        // captured SynchronizationContext to deadlock on.
        private static T RunSync<T>(Task<T> task)
        {
            return task.GetAwaiter().GetResult();
        }

        private static void RunSync(Task task)
        {
            task.GetAwaiter().GetResult();
        }

        // Build a name -> DataField lookup from the reader's actual schema.
        // Parquet.Net 4.x requires DataField instances passed to
        // ReadColumnAsync to be attached to the schema being read, so we
        // can't reuse our own static FIELD_* instances directly.
        private static Dictionary<string, DataField> BuildFieldLookup(ParquetReader reader)
        {
            return reader.Schema.GetDataFields().ToDictionary(f => f.Name);
        }

        // Read a column by name, returning null if the column is absent so
        // callers can tolerate partial schemas the same way the old `as T[]`
        // casts did.
        private static T ReadColumnByName<T>(ParquetRowGroupReader groupReader,
            IReadOnlyDictionary<string, DataField> fieldsByName, string name)
            where T : class
        {
            DataField field;
            if (!fieldsByName.TryGetValue(name, out field))
                return null;
            return RunSync(groupReader.ReadColumnAsync(field)).Data as T;
        }

        /// <summary>
        /// Encode an array of f64 values as a little-endian byte blob with
        /// no length prefix — bytes / 8 recovers the count on read. Mirrors
        /// Rust pipeline.rs:1620-1623 (`v.to_le_bytes().flat_map(...)`)
        /// byte-for-byte for a non-empty input. A null or empty input encodes
        /// as a NULL cell (the column is declared nullable). A zero-length blob
        /// would instead leave a whole row group's column zero-length whenever
        /// every row in that group is empty -- reachable once the file is
        /// written in bounded row groups (e.g. a group that falls entirely in
        /// the contiguous decoy region, where reference XICs can be absent) --
        /// which the Parquet reader cannot decode (an all-zero-length page
        /// overruns on the length prefix). A null cell reads back as an empty
        /// array (DecodeF64Blob(null) == empty), so the decoded value is
        /// unchanged and no gate is affected (the regression + cross-impl gates
        /// compare the blib + protein-FDR, never parquet bytes). This is the
        /// "more parquet-idiomatic" form the cwt_candidates TODO above anticipates.
        /// </summary>
        private static byte[] EncodeF64Blob(double[] values)
        {
            if (values == null || values.Length == 0)
                return null;
            var buf = new byte[values.Length * 8];
            for (int i = 0; i < values.Length; i++)
            {
                long bits = BitConverter.DoubleToInt64Bits(values[i]);
                System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(
                    new Span<byte>(buf, i * 8, 8), bits);
            }
            return buf;
        }

        /// <summary>
        /// Encode an array of f32 values as a little-endian byte blob with
        /// no length prefix — bytes / 4 recovers the count on read. Mirrors
        /// Rust pipeline.rs:1626-1631 byte-for-byte. Used for
        /// `fragment_intensities` (f32 in both impls). Uses a single
        /// <see cref="Buffer.BlockCopy"/> over the underlying float[] storage
        /// (allocation-free per element); the IEEE-754 little-endian byte
        /// layout matches the Rust blob exactly on LE hosts (x64/x86 — both
        /// pwiz target archs are LE), avoiding net472's missing
        /// <c>BitConverter.SingleToInt32Bits</c>. A null or empty input encodes
        /// as a NULL cell (not a zero-length blob) for the same reason as
        /// <see cref="EncodeF64Blob"/> -- see that method for the rationale.
        /// </summary>
        private static byte[] EncodeF32Blob(float[] values)
        {
            if (values == null || values.Length == 0)
                return null;
            var buf = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, buf, 0, buf.Length);
            return buf;
        }

        /// <summary>
        /// Inverse of <see cref="EncodeF64Blob"/>. Returns an empty array
        /// for null or empty input (preserves <see cref="EncodeF64Blob"/>'s
        /// invariant). Throws if the byte length is not a multiple of 8.
        /// </summary>
        private static double[] DecodeF64Blob(byte[] blob)
        {
            if (blob == null || blob.Length == 0)
                return Array.Empty<double>();
            if (blob.Length % 8 != 0)
                throw new InvalidDataException(string.Format(
                    "f64 blob length {0} is not a multiple of 8", blob.Length));
            int n = blob.Length / 8;
            var values = new double[n];
            for (int i = 0; i < n; i++)
            {
                long bits = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(
                    new ReadOnlySpan<byte>(blob, i * 8, 8));
                values[i] = BitConverter.Int64BitsToDouble(bits);
            }
            return values;
        }

        /// <summary>
        /// Inverse of <see cref="EncodeF32Blob"/>. Returns an empty array
        /// for null or empty input. Throws if the byte length is not a
        /// multiple of 4.
        /// </summary>
        private static float[] DecodeF32Blob(byte[] blob)
        {
            if (blob == null || blob.Length == 0)
                return Array.Empty<float>();
            if (blob.Length % 4 != 0)
                throw new InvalidDataException(string.Format(
                    "f32 blob length {0} is not a multiple of 4", blob.Length));
            int n = blob.Length / 4;
            var values = new float[n];
            Buffer.BlockCopy(blob, 0, values, 0, blob.Length);
            return values;
        }

        #endregion

        #region Load FDR Stubs

        /// <summary>
        /// Load only the columns needed for FDR stubs from a Parquet cache.
        /// Reads: entry_id, is_decoy, charge, scan_number, apex_rt, start_rt, end_rt,
        /// fragment_coelution_sum, bounds_area, modified_sequence.
        /// Sets parquet_index = row index. <c>bounds_area</c> feeds the
        /// .blib's <c>OspreyPeakBoundaries.IntegratedArea</c> column at
        /// Stage 7 — without it, the stage-7 .blib write emits zero for
        /// every IntegratedArea row and silently diverges from Rust.
        /// </summary>
        public static List<FdrEntry> LoadFdrStubsFromParquet(string path)
        {
            var stubs = new List<FdrEntry>();

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = RunSync(ParquetReader.CreateAsync(stream)))
            {
                var fieldsByName = BuildFieldLookup(reader);
                for (int g = 0; g < reader.RowGroupCount; g++)
                {
                    using (var groupReader = reader.OpenRowGroupReader(g))
                    {
                        var entryIdCol = ReadColumnByName<uint[]>(groupReader, fieldsByName, FIELD_ENTRY_ID.Name);
                        var isDecoyCol = ReadColumnByName<bool[]>(groupReader, fieldsByName, FIELD_IS_DECOY.Name);
                        var chargeCol = ReadColumnByName<byte[]>(groupReader, fieldsByName, FIELD_CHARGE.Name);
                        var scanCol = ReadColumnByName<uint[]>(groupReader, fieldsByName, FIELD_SCAN_NUMBER.Name);
                        var modseqCol = ReadColumnByName<string[]>(groupReader, fieldsByName, FIELD_MODIFIED_SEQUENCE.Name);
                        var apexCol = ReadColumnByName<double[]>(groupReader, fieldsByName, FIELD_APEX_RT.Name);
                        var startCol = ReadColumnByName<double[]>(groupReader, fieldsByName, FIELD_START_RT.Name);
                        var endCol = ReadColumnByName<double[]>(groupReader, fieldsByName, FIELD_END_RT.Name);
                        var coelutionCol = ReadColumnByName<double[]>(groupReader, fieldsByName, FIELD_COELUTION_SUM.Name);
                        var boundsAreaCol = ReadColumnByName<double[]>(groupReader, fieldsByName, FIELD_BOUNDS_AREA.Name);

                        if (entryIdCol == null || isDecoyCol == null)
                            continue;

                        int rowCount = entryIdCol.Length;
                        for (int row = 0; row < rowCount; row++)
                        {
                            stubs.Add(new FdrEntry
                            {
                                EntryId = entryIdCol[row],
                                ParquetIndex = (uint)(stubs.Count),
                                IsDecoy = isDecoyCol[row],
                                Charge = chargeCol != null ? chargeCol[row] : (byte)0,
                                ScanNumber = scanCol != null ? scanCol[row] : 0u,
                                ApexRt = apexCol != null ? apexCol[row] : 0.0,
                                StartRt = startCol != null ? startCol[row] : 0.0,
                                EndRt = endCol != null ? endCol[row] : 0.0,
                                CoelutionSum = coelutionCol != null ? coelutionCol[row] : 0.0,
                                BoundsArea = boundsAreaCol != null ? boundsAreaCol[row] : 0.0,
                                ModifiedSequence = modseqCol != null ? modseqCol[row] : string.Empty,
                            });
                        }
                    }
                }
            }

            return stubs;
        }

        /// <summary>
        /// Streams the scalar stub columns of a <c>.scores.parquet</c> without allocating a
        /// single <see cref="FdrEntry"/>. Reads exactly the five columns the first-pass
        /// FdrProjection needs -- entry_id, charge, is_decoy, coelution_sum,
        /// modified_sequence -- and invokes <paramref name="onRow"/> once per row in the
        /// same order as <see cref="LoadFdrStubsFromParquet"/>, applying the identical
        /// "skip this row group when entry_id/is_decoy are absent" rule. The caller's
        /// running row count therefore equals that method's <c>ParquetIndex</c>.
        ///
        /// Exists because rematerializing the whole 191M-row stub buffer just to convert it
        /// into 32 B projection rows cost ~53 GB on an 82-file Astral run. Osprey.IO must
        /// not depend on Osprey.FDR, so the projection row is assembled by the caller from
        /// these scalars rather than returned from here.
        /// </summary>
        public static void ReadFdrStubScalars(string path,
            Action<uint, byte, bool, double, string> onRow)
        {
            if (onRow == null)
                throw new ArgumentNullException(nameof(onRow));

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = RunSync(ParquetReader.CreateAsync(stream)))
            {
                var fieldsByName = BuildFieldLookup(reader);
                for (int g = 0; g < reader.RowGroupCount; g++)
                {
                    using (var groupReader = reader.OpenRowGroupReader(g))
                    {
                        var entryIdCol = ReadColumnByName<uint[]>(groupReader, fieldsByName, FIELD_ENTRY_ID.Name);
                        var isDecoyCol = ReadColumnByName<bool[]>(groupReader, fieldsByName, FIELD_IS_DECOY.Name);
                        var chargeCol = ReadColumnByName<byte[]>(groupReader, fieldsByName, FIELD_CHARGE.Name);
                        var modseqCol = ReadColumnByName<string[]>(groupReader, fieldsByName, FIELD_MODIFIED_SEQUENCE.Name);
                        var coelutionCol = ReadColumnByName<double[]>(groupReader, fieldsByName, FIELD_COELUTION_SUM.Name);

                        if (entryIdCol == null || isDecoyCol == null)
                            continue;

                        int rowCount = entryIdCol.Length;
                        for (int row = 0; row < rowCount; row++)
                        {
                            onRow(
                                entryIdCol[row],
                                chargeCol != null ? chargeCol[row] : (byte)0,
                                isDecoyCol[row],
                                coelutionCol != null ? coelutionCol[row] : 0.0,
                                modseqCol != null ? modseqCol[row] : string.Empty);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Load only the <c>cwt_candidates</c> column from a Parquet cache,
        /// returning one <see cref="CwtCandidate"/> list per row in the same
        /// order as <see cref="LoadFdrStubsFromParquet"/>. Used by Stage 6
        /// reconciliation planning, which needs the per-entry CWT peak
        /// candidates without paying the cost of loading features and
        /// fragments. Mirrors the Rust loader at
        /// <c>osprey/crates/osprey/src/pipeline.rs::load_cwt_candidates_from_parquet</c>.
        /// </summary>
        public static List<List<CwtCandidate>> LoadCwtCandidatesFromParquet(string path)
        {
            var allCandidates = new List<List<CwtCandidate>>();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = RunSync(ParquetReader.CreateAsync(stream)))
            {
                var fieldsByName = BuildFieldLookup(reader);
                DataField cwtField;
                if (!fieldsByName.TryGetValue(FIELD_CWT_CANDIDATES.Name, out cwtField))
                    return allCandidates;
                for (int g = 0; g < reader.RowGroupCount; g++)
                {
                    using (var groupReader = reader.OpenRowGroupReader(g))
                    {
                        var col = RunSync(groupReader.ReadColumnAsync(cwtField));
                        // The cwt_candidates column is binary; if Parquet.Net
                        // hands back something other than byte[][] the
                        // schema or write path is wrong upstream and any
                        // silent fallback would desynchronize this list
                        // from LoadFdrStubsFromParquet's row order. Throw
                        // with the offending file + group so the caller
                        // sees a clear error rather than empty CWT lists.
                        var blobs = col.Data as byte[][];
                        if (blobs == null)
                        {
                            // Parquet.Net 4.x types col.Data as non-null IArray.
                            throw new InvalidDataException(string.Format(
                                "{0}: cwt_candidates column in row group {1} " +
                                "decoded as {2}, expected byte[][] -- parquet schema mismatch",
                                Path.GetFileName(path), g, col.Data.GetType().Name));
                        }
                        for (int row = 0; row < blobs.Length; row++)
                            allCandidates.Add(CwtCandidateCodec.Decode(blobs[row]));
                    }
                }
            }
            return allCandidates;
        }

        /// <summary>
        /// Footer-only probe of a scores parquet: the total row count and whether
        /// the <c>cwt_candidates</c> column is present, read from the Parquet
        /// metadata WITHOUT decoding any column data. Lets Stage 6 validate that
        /// every file's stub <see cref="FdrEntry.ParquetIndex"/> is in range (the
        /// reconciliation all-or-nothing gate) without holding all files'
        /// candidate lists resident -- the streaming counterpart of
        /// <see cref="LoadCwtCandidatesFromParquet"/>, whose per-file result count
        /// equals the returned <c>RowCount</c> when the column is present and 0 when
        /// it is absent (that method returns an empty list for a missing column).
        /// </summary>
        public static (long RowCount, bool HasCwtCandidatesField) ProbeCwtRowMetadata(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = RunSync(ParquetReader.CreateAsync(stream)))
            {
                long rowCount = reader.Metadata?.NumRows ?? 0L;
                var fieldsByName = BuildFieldLookup(reader);
                bool hasCwt = fieldsByName.ContainsKey(FIELD_CWT_CANDIDATES.Name);
                return (rowCount, hasCwt);
            }
        }

        /// <summary>
        /// Footer-only check that a scores parquet carries the PIN feature columns,
        /// reading the schema WITHOUT decoding any column data. The lean resume /
        /// HPC-merge paths stream only the scalar stub columns
        /// (<see cref="ReadFdrStubScalars"/>) and never materialize the 21-float
        /// feature vectors, so they lose the fat path's implicit
        /// <c>features.Count == stubs.Count</c> corruption guard (which throws when
        /// <see cref="LoadPinFeaturesFromParquet"/> yields zero rows because the
        /// feature schema is absent). This restores an equivalent fail-fast up front
        /// without paying the feature-load memory the lean path exists to avoid.
        /// Presence of the first PIN feature column is decisive: parquet keeps every
        /// column in a row group the same length, so a present column has the stub
        /// row count, and an absent one is exactly the desync the fat path rejected.
        /// </summary>
        public static bool HasPinFeatureColumns(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = RunSync(ParquetReader.CreateAsync(stream)))
            {
                var fieldsByName = BuildFieldLookup(reader);
                return fieldsByName.ContainsKey(PIN_FEATURE_NAMES[0]);
            }
        }

        #endregion

        #region Load PIN Features

        /// <summary>
        /// Load FdrEntry stubs + 21-feature PIN vectors + CWT candidate
        /// lists from a Parquet cache, joined per row. Returns
        /// <see cref="FdrEntry"/> objects with <see cref="FdrEntry.Features"/>
        /// and <see cref="FdrEntry.CwtCandidates"/> populated, ready to
        /// feed into the Phase 3 reconciled parquet write-back step.
        ///
        /// Equivalent to calling <see cref="LoadFdrStubsFromParquet"/>,
        /// <see cref="LoadPinFeaturesFromParquet"/>, and
        /// <see cref="LoadCwtCandidatesFromParquet"/> separately and
        /// zipping them by row index — but does it in a single parquet
        /// open. Mirrors the columns Rust's <c>load_scores_parquet</c>
        /// loads. Decodes the four binary blob columns (<c>fragment_mzs</c>,
        /// <c>fragment_intensities</c>, <c>reference_xic_rts</c>,
        /// <c>reference_xic_intensities</c>) and the <c>bounds_area</c> /
        /// <c>bounds_snr</c> scalars onto the matching <see cref="FdrEntry"/>
        /// fields so the Stage 6 reconciled parquet write-back round-
        /// trips them for every row, not just freshly rescored rows.
        /// </summary>
        public static List<FdrEntry> LoadFullFdrEntries(string path)
        {
            var entries = new List<FdrEntry>();

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = RunSync(ParquetReader.CreateAsync(stream)))
            {
                var fieldsByName = BuildFieldLookup(reader);
                for (int g = 0; g < reader.RowGroupCount; g++)
                    entries.AddRange(ReadFdrEntryGroup(reader, g, fieldsByName, entries.Count));
            }

            return entries;
        }

        /// <summary>
        /// Stream the Stage-6 reconciled transfer: read <paramref name="originalPath"/>
        /// one row group at a time, overlay the re-scored rows from
        /// <paramref name="overlayByIndex"/> (keyed by the original row's
        /// <see cref="FdrEntry.ParquetIndex"/>), MERGE the <paramref name="gapFill"/> rows
        /// into their canonical (entry_id, charge, scan_number) sorted position, and write
        /// the result to <paramref name="reconciledPath"/> as bounded row groups. Peak
        /// residency is one original row group being read + one output group being filled +
        /// the small resident overlay map / gap-fill list -- it never materializes the whole
        /// file's <see cref="FdrEntry"/> list the way LoadFullFdrEntries +
        /// <see cref="WriteScoresParquet(string,List{FdrEntry},Dictionary{string,string},Dictionary{uint,LibraryEntry},string)"/>
        /// did (the ~4.4 GB reload this replaces).
        ///
        /// The reconciled physical row order must equal the former load-all + re-sort write:
        /// Pass 2's projection sort recovers scan order from the reconciled row index (see
        /// Pass2FdrSidecar / TestScanOmittedProjectionSortMatchesLegacyOrder), so gap-fill
        /// CANNOT simply be appended at the end -- it must interleave by scan. The original
        /// rows are already in canonical order (Stage 4 wrote them sorted, and an overlay
        /// preserves the row's key), so a single-pass 2-way merge of the streamed original
        /// rows with the sorted gap-fill list reproduces the exact stable-sorted sequence
        /// WriteScoresParquet produced, without ever holding the whole file. No gate compares
        /// .scores.parquet bytes directly (regression + cross-impl compare the blib +
        /// protein-FDR at 1e-9); this physical equivalence is what keeps them green.
        /// See ai/todos/active/TODO-20260717_osprey_stage6_chunked_reconciled_transfer.md.
        ///
        /// Returns the replaced-row, appended-row (gap-fill), and original-row counts.
        /// Overlay indices that fall past the original's rows are dropped with a warning
        /// (never written), matching the whole-file overlay's out-of-range handling.
        /// </summary>
        public static (int NReplaced, int NAppended, int OrigRowCount) StreamReconciledScoresParquet(
            string originalPath, string reconciledPath,
            IReadOnlyDictionary<uint, FdrEntry> overlayByIndex,
            IReadOnlyList<FdrEntry> gapFill,
            Dictionary<string, string> metadata,
            Dictionary<uint, LibraryEntry> libraryById, string fileName,
            Action<string> logWarning)
        {
            if (originalPath == null)
                throw new ArgumentNullException(nameof(originalPath));
            if (reconciledPath == null)
                throw new ArgumentNullException(nameof(reconciledPath));
            if (overlayByIndex == null)
                overlayByIndex = new Dictionary<uint, FdrEntry>();

            // Sort gap-fill into canonical (entry_id, charge, scan_number) order with a
            // STABLE sort (LINQ OrderBy), mirroring the former WriteScoresParquet OrderBy.
            // The 2-way merge below emits each original row before an equal-key gap-fill
            // row, exactly as that stable sort placed the (earlier-in-list) original rows
            // ahead of an appended equal-key gap-fill row.
            var sortedGapFill = (gapFill ?? Array.Empty<FdrEntry>())
                .OrderBy(e => e.EntryId).ThenBy(e => e.Charge).ThenBy(e => e.ScanNumber)
                .ToList();
            int gapFillCount = sortedGapFill.Count;

            var featureFields = BuildFeatureFields();
            var schema = BuildWriteSchema(featureFields);
            int rowsPerGroup = Math.Max(1, RowGroupRowCapForTest ?? MAX_ROWS_PER_ROW_GROUP);

            int nReplaced = 0;
            int origRowCount = 0;

            using (var readStream = new FileStream(originalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = RunSync(ParquetReader.CreateAsync(readStream)))
            using (var saver = new FileSaver(reconciledPath))
            {
                var fieldsByName = BuildFieldLookup(reader);
                int totalRows = checked((int)(reader.Metadata?.NumRows ?? 0L)) + gapFillCount;

                using (var writeStream = new FileStream(saver.SafeName, FileMode.Create, FileAccess.Write))
                using (var writer = RunSync(ParquetWriter.CreateAsync(schema, writeStream)))
                using (var progress = new ProgressReporter(
                    string.Format("Writing {0} entries", totalRows), totalRows, string.Empty,
                    ProgressReporter.IO_INTERVAL_SECONDS))
                {
                    writer.CompressionMethod = CompressionMethod.Zstd;
                    if (metadata != null && metadata.Count > 0)
                        writer.CustomMetadata = metadata;

                    // Output accumulator: fill to rowsPerGroup, flush as one bounded row
                    // group, release. `written` is the running output row position;
                    // `origRead` is the running ORIGINAL row count the overlay is keyed on.
                    var buffer = new List<FdrEntry>(rowsPerGroup);
                    int written = 0;
                    int origRead = 0;
                    int gapIdx = 0;

                    void FlushGroup()
                    {
                        if (buffer.Count == 0)
                            return;
                        using (var group = writer.CreateRowGroup())
                            WriteRowGroupColumns(group, BuildFdrEntryColumns(
                                buffer, written, libraryById, fileName, featureFields));
                        written += buffer.Count;
                        progress.Report(written);
                        buffer.Clear();
                    }

                    // Emit one row in output order, GUARDING canonical monotonicity. The
                    // merge preserves (entry_id, charge, scan_number) order only if the
                    // original parquet is already sorted AND each overlay keeps the key of the
                    // row it replaces -- but a reconciliation rescore can move the apex scan,
                    // so an overlay CAN change scan_number. If that (or a mis-sorted original)
                    // makes an emitted row's key fall below the previous, the output would be
                    // non-canonical and silently corrupt Pass 2's scan recovery. Hard-fail
                    // instead (ReconciledParquetWriter.Write re-throws this to abort the run,
                    // rather than skipping the file). No-op on valid, already-sorted input.
                    FdrEntry lastEmitted = null;
                    void Emit(FdrEntry e)
                    {
                        if (lastEmitted != null && KeyLess(e, lastEmitted))
                            throw new InvalidOperationException(string.Format(
                                "Stage 6 reconciled transfer for {0}: rows out of canonical " +
                                "(entry_id, charge, scan_number) order at output row {1} -- the " +
                                "original parquet is not sorted, or a re-scored overlay changed its " +
                                "scan across a same-(entry_id,charge) sibling. Refusing to write a " +
                                "mis-ordered reconciled parquet.", fileName, written + buffer.Count));
                        lastEmitted = e;
                        buffer.Add(e);
                        if (buffer.Count == rowsPerGroup)
                            FlushGroup();
                    }

                    for (int g = 0; g < reader.RowGroupCount; g++)
                    {
                        var groupEntries = ReadFdrEntryGroup(reader, g, fieldsByName, origRead);
                        for (int j = 0; j < groupEntries.Count; j++)
                        {
                            var row = groupEntries[j];
                            FdrEntry rescored;
                            if (overlayByIndex.Count > 0 &&
                                overlayByIndex.TryGetValue((uint)(origRead + j), out rescored))
                            {
                                row = rescored;
                                nReplaced++;
                            }
                            // Emit gap-fill rows that sort strictly before this original
                            // row; a key tie keeps the original first (stable-sort order).
                            while (gapIdx < gapFillCount && KeyLess(sortedGapFill[gapIdx], row))
                                Emit(sortedGapFill[gapIdx++]);
                            Emit(row);
                        }
                        origRead += groupEntries.Count;
                    }
                    origRowCount = origRead;

                    // Trailing gap-fill (keys at or beyond the last original row).
                    while (gapIdx < gapFillCount)
                        Emit(sortedGapFill[gapIdx++]);
                    FlushGroup();
                }
                saver.Commit();
            }

            // Overlay indices that never matched a streamed row lie past the original's
            // rows; report them exactly as the whole-file overlay did -- dropped, never
            // written -- so a corrupt ParquetIndex still surfaces a warning.
            if (nReplaced < overlayByIndex.Count && logWarning != null)
            {
                foreach (var kv in overlayByIndex)
                {
                    if (kv.Key >= (uint)origRowCount)
                        logWarning(string.Format(
                            "Stage 6 write-back: ParquetIndex {0} out of range for {1} ({2} rows)",
                            kv.Key, fileName, origRowCount));
                }
            }

            return (nReplaced, gapFillCount, origRowCount);
        }

        // Strict (entry_id, charge, scan_number) less-than, the canonical scores-parquet
        // sort key. Used by the Stage-6 streaming merge to interleave gap-fill rows into
        // the already-sorted original stream. A key tie returns false so the original row
        // is emitted first, matching the stable WriteScoresParquet re-sort.
        private static bool KeyLess(FdrEntry a, FdrEntry b)
        {
            if (a.EntryId != b.EntryId)
                return a.EntryId < b.EntryId;
            if (a.Charge != b.Charge)
                return a.Charge < b.Charge;
            return a.ScanNumber < b.ScanNumber;
        }

        /// <summary>
        /// Read one row group's full <see cref="FdrEntry"/> rows -- the per-group body of
        /// <see cref="LoadFullFdrEntries"/>, extracted so the Stage-6 streaming reconciled
        /// transfer (<see cref="StreamReconciledScoresParquet"/>) can read the original
        /// parquet one group at a time instead of materializing every row. Each row's
        /// <see cref="FdrEntry.ParquetIndex"/> is the running global position
        /// <paramref name="startParquetIndex"/> + row, matching the whole-file loader.
        /// Returns an empty list when the group lacks the entry_id / is_decoy columns
        /// (the same "skip this group" rule the whole-file loader applies).
        /// </summary>
        private static List<FdrEntry> ReadFdrEntryGroup(ParquetReader reader, int g,
            IReadOnlyDictionary<string, DataField> fieldsByName, int startParquetIndex)
        {
            var entries = new List<FdrEntry>();
            using (var groupReader = reader.OpenRowGroupReader(g))
            {
                var entryIdCol = ReadColumnByName<uint[]>(groupReader, fieldsByName, FIELD_ENTRY_ID.Name);
                var isDecoyCol = ReadColumnByName<bool[]>(groupReader, fieldsByName, FIELD_IS_DECOY.Name);
                var chargeCol = ReadColumnByName<byte[]>(groupReader, fieldsByName, FIELD_CHARGE.Name);
                var scanCol = ReadColumnByName<uint[]>(groupReader, fieldsByName, FIELD_SCAN_NUMBER.Name);
                var modseqCol = ReadColumnByName<string[]>(groupReader, fieldsByName, FIELD_MODIFIED_SEQUENCE.Name);
                var apexCol = ReadColumnByName<double[]>(groupReader, fieldsByName, FIELD_APEX_RT.Name);
                var startCol = ReadColumnByName<double[]>(groupReader, fieldsByName, FIELD_START_RT.Name);
                var endCol = ReadColumnByName<double[]>(groupReader, fieldsByName, FIELD_END_RT.Name);
                var coelutionCol = ReadColumnByName<double[]>(groupReader, fieldsByName, FIELD_COELUTION_SUM.Name);
                var cwtCol = ReadColumnByName<byte[][]>(groupReader, fieldsByName, FIELD_CWT_CANDIDATES.Name);
                var boundsAreaCol = ReadColumnByName<double[]>(groupReader, fieldsByName, FIELD_BOUNDS_AREA.Name);
                var boundsSnrCol = ReadColumnByName<double[]>(groupReader, fieldsByName, FIELD_BOUNDS_SNR.Name);
                var fragMzCol = ReadColumnByName<byte[][]>(groupReader, fieldsByName, FIELD_FRAGMENT_MZS.Name);
                var fragIntCol = ReadColumnByName<byte[][]>(groupReader, fieldsByName, FIELD_FRAGMENT_INTENSITIES.Name);
                var refXicRtsCol = ReadColumnByName<byte[][]>(groupReader, fieldsByName, FIELD_REFERENCE_XIC_RTS.Name);
                var refXicIntsCol = ReadColumnByName<byte[][]>(groupReader, fieldsByName, FIELD_REFERENCE_XIC_INTENSITIES.Name);

                if (entryIdCol == null || isDecoyCol == null)
                    return entries;

                var featureCols = new double[NUM_PIN_FEATURES][];
                for (int f = 0; f < NUM_PIN_FEATURES; f++)
                    featureCols[f] = ReadColumnByName<double[]>(groupReader, fieldsByName, PIN_FEATURE_NAMES[f]);

                int rowCount = entryIdCol.Length;
                for (int row = 0; row < rowCount; row++)
                {
                    var features = new double[NUM_PIN_FEATURES];
                    for (int f = 0; f < NUM_PIN_FEATURES; f++)
                    {
                        double v = featureCols[f] != null ? featureCols[f][row] : 0.0;
                        features[f] = double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : v;
                    }

                    List<CwtCandidate> cwt = null;
                    if (cwtCol != null && cwtCol[row] != null && cwtCol[row].Length > 0)
                        cwt = CwtCandidateCodec.Decode(cwtCol[row]);

                    entries.Add(new FdrEntry
                    {
                        EntryId = entryIdCol[row],
                        ParquetIndex = (uint)(startParquetIndex + entries.Count),
                        IsDecoy = isDecoyCol[row],
                        Charge = chargeCol != null ? chargeCol[row] : (byte)0,
                        ScanNumber = scanCol != null ? scanCol[row] : 0u,
                        ApexRt = apexCol != null ? apexCol[row] : 0.0,
                        StartRt = startCol != null ? startCol[row] : 0.0,
                        EndRt = endCol != null ? endCol[row] : 0.0,
                        CoelutionSum = coelutionCol != null ? coelutionCol[row] : 0.0,
                        ModifiedSequence = modseqCol != null ? modseqCol[row] : string.Empty,
                        Features = features,
                        CwtCandidates = cwt,
                        BoundsArea = boundsAreaCol != null ? boundsAreaCol[row] : 0.0,
                        BoundsSnr = boundsSnrCol != null ? boundsSnrCol[row] : 0.0,
                        FragmentMzs = DecodeF64Blob(fragMzCol != null ? fragMzCol[row] : null),
                        FragmentIntensities = DecodeF32Blob(fragIntCol != null ? fragIntCol[row] : null),
                        ReferenceXicRts = DecodeF64Blob(refXicRtsCol != null ? refXicRtsCol[row] : null),
                        ReferenceXicIntensities = DecodeF64Blob(refXicIntsCol != null ? refXicIntsCol[row] : null),
                    });
                }
            }
            return entries;
        }

        /// <summary>
        /// Load only the 21 PIN feature columns from a Parquet cache.
        /// Returns a list of feature vectors (one double[] per row).
        /// </summary>
        public static List<double[]> LoadPinFeaturesFromParquet(string path)
        {
            var allFeatures = new List<double[]>();

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = RunSync(ParquetReader.CreateAsync(stream)))
            {
                var fieldsByName = BuildFieldLookup(reader);
                for (int g = 0; g < reader.RowGroupCount; g++)
                {
                    using (var groupReader = reader.OpenRowGroupReader(g))
                    {
                        // Read all feature columns
                        var featureCols = new double[NUM_PIN_FEATURES][];
                        for (int f = 0; f < NUM_PIN_FEATURES; f++)
                        {
                            featureCols[f] = ReadColumnByName<double[]>(groupReader, fieldsByName, PIN_FEATURE_NAMES[f]);
                        }

                        if (featureCols[0] == null)
                            continue;

                        int rowCount = featureCols[0].Length;
                        for (int row = 0; row < rowCount; row++)
                        {
                            var features = new double[NUM_PIN_FEATURES];
                            for (int f = 0; f < NUM_PIN_FEATURES; f++)
                            {
                                double v = featureCols[f][row];
                                features[f] = double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : v;
                            }
                            allFeatures.Add(features);
                        }
                    }
                }
            }

            return allFeatures;
        }

        #endregion

        #region Path and Metadata Helpers

        /// <summary>
        /// Returns the scores Parquet path for a given mzML path: {stem}.scores.parquet
        /// in the same directory.
        /// </summary>
        public static string GetScoresPath(string mzmlPath)
        {
            string dir = ArtifactPaths.ResolveOutputDir(mzmlPath);
            string stem = Path.GetFileNameWithoutExtension(mzmlPath);
            return Path.Combine(dir, stem + ".scores.parquet");
        }

        // The reconciled-output marker is appended AFTER the ".scores" token
        // (".scores-reconciled.parquet"), NOT inserted before it. Stage 4 always
        // writes exactly "<stem>.scores.parquet" (GetScoresPath appends that
        // literal), so a Stage 4 path can never end in ".scores-reconciled.parquet"
        // even when the input stem itself ends in ".reconciled". That makes the
        // suffix an UNAMBIGUOUS "this is a Stage 6 reconciled output" signal --
        // no parquet-metadata read needed to tell the two apart.
        public const string ScoresParquetSuffix = ".scores.parquet";
        public const string ReconciledScoresParquetSuffix = ".scores-reconciled.parquet";

        /// <summary>
        /// Returns the reconciled scores Parquet path for a given mzML path:
        /// {stem}.scores-reconciled.parquet in the same directory. Stage 6
        /// (<c>PerFileRescoreTask</c>) writes this file instead of overwriting
        /// the Stage 4 <see cref="GetScoresPath"/> output, so the original
        /// per-file scores survive a reconciliation pass (and a partial Stage 6
        /// crash can no longer leave the Stage 4 parquet in an indeterminate
        /// half-rewritten state).
        /// </summary>
        public static string GetReconciledScoresPath(string mzmlPath)
        {
            string dir = ArtifactPaths.ResolveOutputDir(mzmlPath);
            string stem = Path.GetFileNameWithoutExtension(mzmlPath);
            return Path.Combine(dir, stem + ReconciledScoresParquetSuffix);
        }

        /// <summary>
        /// True if <paramref name="path"/> is a Stage 6 reconciled-scores parquet
        /// (ends in <c>.scores-reconciled.parquet</c>). Unambiguous: see the note
        /// on <see cref="ReconciledScoresParquetSuffix"/>.
        /// </summary>
        public static bool IsReconciledScoresPath(string path)
        {
            return !string.IsNullOrEmpty(path)
                && path.EndsWith(ReconciledScoresParquetSuffix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Map an original <c>.scores.parquet</c> path to its reconciled sibling
        /// <c>.scores-reconciled.parquet</c> by swapping the trailing suffix. A
        /// path that is already a reconciled output is returned unchanged (safe
        /// and unambiguous, since the two suffixes never collide).
        /// </summary>
        public static string ReconciledPathFromScoresPath(string scoresPath)
        {
            if (string.IsNullOrEmpty(scoresPath))
                return scoresPath;
            if (scoresPath.EndsWith(ReconciledScoresParquetSuffix, StringComparison.Ordinal))
                return scoresPath;
            if (scoresPath.EndsWith(ScoresParquetSuffix, StringComparison.Ordinal))
                return scoresPath.Substring(0, scoresPath.Length - ScoresParquetSuffix.Length)
                    + ReconciledScoresParquetSuffix;
            return scoresPath;
        }

        /// <summary>
        /// The path a post-Stage-6 reader (Stage 7 feature reload, resume /
        /// <c>--task SecondPassFDR</c>) should consume for a given original
        /// <c>.scores.parquet</c> path: the reconciled sibling when it exists
        /// on disk, otherwise the original. This per-file selection is the
        /// read-side contract that makes the separate-reconciled-file design
        /// byte-equivalent to the former in-place overwrite: files that had
        /// reconciliation work read the reconciled bytes (which used to be
        /// written over the original), while files with no Stage 6 work -- which
        /// <c>PerFileRescoreTask</c> deliberately skips, leaving no reconciled
        /// file -- read the untouched original (which used to be left in place).
        /// </summary>
        public static string EffectiveScoresPathFromScoresPath(string scoresPath)
        {
            string reconciled = ReconciledPathFromScoresPath(scoresPath);
            return File.Exists(reconciled) ? reconciled : scoresPath;
        }

        /// <summary>
        /// Check if an existing Parquet file's custom metadata matches the expected values.
        /// Returns true if all expected keys exist with matching values.
        /// </summary>
        public static bool ValidateMetadata(string path, Dictionary<string, string> expected)
        {
            if (!File.Exists(path))
                return false;

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = RunSync(ParquetReader.CreateAsync(stream)))
                {
                    // Parquet.Net 4.x types CustomMetadata as a non-null
                    // IReadOnlyDictionary; an empty file simply yields an
                    // empty dictionary, so no null guard is needed.
                    var metaDict = reader.CustomMetadata;
                    foreach (var kvp in expected)
                    {
                        string value;
                        if (!metaDict.TryGetValue(kvp.Key, out value))
                            return false;
                        if (value != kvp.Value)
                            return false;
                    }

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Phase 3: --task FirstPassFDR group validation

        /// <summary>
        /// Read all key-value pairs from a parquet footer. Returns an empty
        /// dictionary if the file has no metadata. Throws on IO/parse errors.
        /// </summary>
        public static Dictionary<string, string> LoadFooterMetadata(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = RunSync(ParquetReader.CreateAsync(stream)))
            {
                // Parquet.Net 4.x's CustomMetadata is non-null
                // (empty dictionary when no metadata is present).
                return new Dictionary<string, string>(reader.CustomMetadata);
            }
        }

        /// <summary>
        /// Parse a Skyline-scheme "YEAR.ORDINAL.BRANCH.DOY" version string into
        /// its four integer components. Returns false if any component is missing
        /// or non-numeric. Used by <see cref="CheckParquetMetadata"/>.
        /// </summary>
        public static bool TryParseVersion(string s, out int year, out int ordinal, out int branch, out int doy)
        {
            year = ordinal = branch = doy = 0;
            if (string.IsNullOrEmpty(s))
                return false;
            string[] parts = s.Split('.');
            if (parts.Length != 4)
                return false;
            return int.TryParse(parts[0], out year)
                && int.TryParse(parts[1], out ordinal)
                && int.TryParse(parts[2], out branch)
                && int.TryParse(parts[3], out doy);
        }

        /// <summary>
        /// Pure helper that checks one parquet's footer metadata against the
        /// expected hashes. Separated from the IO so it's unit-testable
        /// without constructing real parquet files. Mirror of the Rust
        /// `check_parquet_metadata` helper. Returns null on success or an error
        /// message naming the file + offending field.
        /// </summary>
        public static string CheckParquetMetadata(
            string fileLabel,
            string cachedVersion,
            string cachedSearch,
            string cachedLibrary,
            string expectedSearch,
            string expectedLibrary,
            string currentVersion)
        {
            if (cachedVersion == null)
                return string.Format("{0}: parquet has no `osprey.version` metadata", fileLabel);
            int cY, cO, cB, cD, rY, rO, rB, rD;
            bool cachedOk = TryParseVersion(cachedVersion, out cY, out cO, out cB, out cD);
            bool currentOk = TryParseVersion(currentVersion, out rY, out rO, out rB, out rD);
            // Hard-fail on ANY osprey.version mismatch rather than warn-and-
            // proceed: a cache produced by a different build may carry
            // incompatible scoring, and a logged warning is easily missed while
            // the run still completes and looks fully valid. Reuse requires an
            // exact version match. The component breakdown is only for a clearer
            // error message (release line vs daily build vs unrecognized).
            if (!cachedOk || !currentOk)
            {
                return string.Format(
                    "{0}: unrecognized osprey version (parquet=\"{1}\", current=\"{2}\"); refusing to reuse a cache whose compatibility cannot be verified",
                    fileLabel, cachedVersion, currentVersion);
            }
            if (cY != rY || cO != rO || cB != rB)
            {
                return string.Format(
                    "{0}: osprey version mismatch: parquet was scored with {1} but current binary is {2} (incompatible release identity)",
                    fileLabel, cachedVersion, currentVersion);
            }
            if (cD != rD)
            {
                return string.Format(
                    "{0}: osprey version mismatch: parquet was scored with {1} but current binary is {2} (different daily build)",
                    fileLabel, cachedVersion, currentVersion);
            }

            if (cachedSearch == null)
                return string.Format("{0}: parquet has no `osprey.search_hash` metadata", fileLabel);
            if (cachedSearch != expectedSearch)
            {
                return string.Format(
                    "{0}: search_hash mismatch: parquet was scored with search_hash={1} but current config hashes to {2}",
                    fileLabel, cachedSearch, expectedSearch);
            }

            if (cachedLibrary == null)
                return string.Format("{0}: parquet has no `osprey.library_hash` metadata", fileLabel);
            if (cachedLibrary != expectedLibrary)
            {
                return string.Format(
                    "{0}: library_hash mismatch: parquet was scored with library_hash={1} but --library hashes to {2}",
                    fileLabel, cachedLibrary, expectedLibrary);
            }

            return null;
        }

        /// <summary>
        /// Open each `.scores.parquet` in <paramref name="paths"/> and assert
        /// its footer metadata matches <paramref name="config"/>'s search and
        /// library hashes. Returns null on success or an error message naming
        /// the offending file. Used at the start of --task FirstPassFDR mode.
        /// </summary>
        public static string ValidateScoresParquetGroup(
            IEnumerable<string> paths,
            OspreyConfig config,
            string currentVersion)
        {
            string expectedSearch = config.Identity.SearchParameterHash();
            string expectedLibrary = config.Identity.LibraryIdentityHash();

            foreach (string path in paths)
            {
                Dictionary<string, string> kv;
                try
                {
                    kv = LoadFooterMetadata(path);
                }
                catch (Exception ex)
                {
                    return string.Format("{0}: cannot read parquet metadata: {1}", path, ex.Message);
                }
                string cachedV; kv.TryGetValue("osprey.version", out cachedV);
                string cachedS; kv.TryGetValue("osprey.search_hash", out cachedS);
                string cachedL; kv.TryGetValue("osprey.library_hash", out cachedL);

                string err = CheckParquetMetadata(
                    path, cachedV, cachedS, cachedL,
                    expectedSearch, expectedLibrary, currentVersion);
                if (err != null)
                    return err;

                // --task SecondPassFDR strict reconciled-input gate. Mirrors
                // Rust pipeline.rs:3313-3344: every input parquet must
                // carry osprey.reconciled = "true" so the operator
                // cannot mix raw Stage 4 parquets into a Stages 7-8-only
                // run. Failing fast here is the contract that lets the
                // post-Stage-6 entry point be a useful HPC boundary
                // (sidecar fanout across compute nodes).
                if (config.ExpectReconciledInput)
                {
                    string cachedReconciled;
                    kv.TryGetValue("osprey.reconciled", out cachedReconciled);
                    if (!string.Equals(cachedReconciled, "true", StringComparison.Ordinal))
                    {
                        return string.Format(
                            "--task SecondPassFDR requires a reconciled (post-Stage-6) parquet, " +
                            "but {0} has osprey.reconciled = '{1}'. Either it is a Stage 4 " +
                            "(raw) parquet — run --task PerFileRescoring to produce reconciled " +
                            "parquets first — or run the full pipeline.",
                            path, cachedReconciled ?? "<unset>");
                    }
                }
            }
            return null;
        }

        #endregion
    }
}
