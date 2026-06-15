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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.IO
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

            // Build column arrays. Schema matches Rust's
            // write_scores_parquet_with_metadata; columns are name-indexed
            // so order is informational, but kept aligned for clarity.
            var entryIds = new uint[n];
            var isDecoys = new bool[n];
            var sequences = new string[n];
            var modifiedSequences = new string[n];
            var charges = new byte[n];
            var precursorMzs = new double[n];
            var proteinIds = new string[n];
            var scanNumbers = new uint[n];
            var apexRts = new double[n];
            var startRts = new double[n];
            var endRts = new double[n];
            var boundsAreas = new double[n];
            var boundsSnrs = new double[n];
            var fileNames = new string[n];
            var cwtCandidates = new byte[n][];
            var fragmentMzs = new byte[n][];
            var fragmentIntensities = new byte[n][];
            var refXicRts = new byte[n][];
            var refXicIntensities = new byte[n][];
            var featureArrays = new double[NUM_PIN_FEATURES][];
            for (int f = 0; f < NUM_PIN_FEATURES; f++)
                featureArrays[f] = new double[n];

            // Iterate in canonical sorted order (entry_id, charge, scan_number)
            // so per-side parquets have identical physical row layout across the
            // Rust and C# impls. Order-sensitive consumers downstream (Stage 5
            // standardizer, SVM training) then see the same row sequence
            // regardless of which side wrote the parquet. Mirrors Rust
            // pipeline.rs::write_scores_parquet_with_metadata.
            var sortedIndices = Enumerable.Range(0, n)
                .OrderBy(idx => entries[idx].EntryId)
                .ThenBy(idx => entries[idx].Charge)
                .ThenBy(idx => entries[idx].ScanNumber)
                .ToArray();

            for (int i = 0; i < n; i++)
            {
                var entry = entries[sortedIndices[i]];
                entryIds[i] = entry.EntryId;
                isDecoys[i] = entry.IsDecoy;
                sequences[i] = entry.Sequence ?? string.Empty;
                modifiedSequences[i] = entry.ModifiedSequence ?? string.Empty;
                charges[i] = entry.Charge;
                precursorMzs[i] = entry.PrecursorMz;
                proteinIds[i] = entry.ProteinIds != null
                    ? string.Join(";", entry.ProteinIds)
                    : null;
                scanNumbers[i] = entry.ScanNumber;
                apexRts[i] = entry.ApexRt;
                startRts[i] = entry.PeakBounds != null ? entry.PeakBounds.StartRt : 0.0;
                endRts[i] = entry.PeakBounds != null ? entry.PeakBounds.EndRt : 0.0;
                boundsAreas[i] = entry.PeakBounds != null ? entry.PeakBounds.Area : 0.0;
                boundsSnrs[i] = entry.PeakBounds != null ? entry.PeakBounds.SignalToNoise : 0.0;
                fileNames[i] = entry.FileName ?? string.Empty;

                ExtractPinFeatures(entry.Features, featureArrays, i);
            }

            // Write to a temp file first, then move to final path (safe NAS writes)
            string tempPath = Path.Combine(Path.GetTempPath(),
                string.Format("osprey_{0}_{1}", System.Diagnostics.Process.GetCurrentProcess().Id,
                    Path.GetFileName(path)));

            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            using (var writer = RunSync(ParquetWriter.CreateAsync(schema, stream)))
            {
                writer.CompressionMethod = CompressionMethod.Zstd;

                // Set custom metadata if provided
                if (metadata != null && metadata.Count > 0)
                    writer.CustomMetadata = metadata;

                using (var group = writer.CreateRowGroup())
                {
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_ENTRY_ID, entryIds)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_IS_DECOY, isDecoys)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_SEQUENCE, sequences)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_MODIFIED_SEQUENCE, modifiedSequences)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_CHARGE, charges)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_PRECURSOR_MZ, precursorMzs)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_PROTEIN_IDS, proteinIds)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_SCAN_NUMBER, scanNumbers)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_APEX_RT, apexRts)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_START_RT, startRts)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_END_RT, endRts)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_BOUNDS_AREA, boundsAreas)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_BOUNDS_SNR, boundsSnrs)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_FILE_NAME, fileNames)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_CWT_CANDIDATES, cwtCandidates)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_FRAGMENT_MZS, fragmentMzs)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_FRAGMENT_INTENSITIES, fragmentIntensities)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_REFERENCE_XIC_RTS, refXicRts)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_REFERENCE_XIC_INTENSITIES, refXicIntensities)));

                    for (int f = 0; f < NUM_PIN_FEATURES; f++)
                        RunSync(group.WriteColumnAsync(new DataColumn(featureFields[f], featureArrays[f])));
                }
            }

            // Move temp to final destination
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tempPath, path);
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

            var entryIds = new uint[n];
            var isDecoys = new bool[n];
            var sequences = new string[n];
            var modifiedSequences = new string[n];
            var charges = new byte[n];
            var precursorMzs = new double[n];
            var proteinIds = new string[n];
            var scanNumbers = new uint[n];
            var apexRts = new double[n];
            var startRts = new double[n];
            var endRts = new double[n];
            var boundsAreas = new double[n];
            var boundsSnrs = new double[n];
            var fileNames = new string[n];
            // The blob columns below carry per-entry binary payloads
            // matching Rust pipeline.rs:1620-1645's encoding:
            //   cwt_candidates             = u32 LE count + N×(6×f64 LE)
            //   fragment_mzs               = M×f64 LE   (no count prefix)
            //   fragment_intensities       = M×f32 LE   (no count prefix)
            //   reference_xic_rts          = K×f64 LE
            //   reference_xic_intensities  = K×f64 LE
            // Length is recovered on read as bytes / sizeof(element).
            var cwtCandidates = new byte[n][];
            var fragmentMzs = new byte[n][];
            var fragmentIntensities = new byte[n][];
            var refXicRts = new byte[n][];
            var refXicIntensities = new byte[n][];
            var featureArrays = new double[NUM_PIN_FEATURES][];
            for (int f = 0; f < NUM_PIN_FEATURES; f++)
                featureArrays[f] = new double[n];

            // Iterate in canonical sorted order (entry_id, charge, scan_number)
            // so per-side parquets have identical physical row layout across
            // Rust and C# impls. Order-sensitive consumers downstream (Stage 5
            // standardizer, SVM training) then see the same row sequence
            // regardless of which side wrote the parquet. Mirrors Rust
            // pipeline.rs::write_scores_parquet_with_metadata. ParquetIndex is
            // assigned to the post-sort destination row below.
            var sortedIndices = Enumerable.Range(0, n)
                .OrderBy(idx => entries[idx].EntryId)
                .ThenBy(idx => entries[idx].Charge)
                .ThenBy(idx => entries[idx].ScanNumber)
                .ToArray();

            for (int i = 0; i < n; i++)
            {
                var entry = entries[sortedIndices[i]];
                // Assign ParquetIndex to match the row position we are
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
                entry.ParquetIndex = (uint)i;
                entryIds[i] = entry.EntryId;
                isDecoys[i] = entry.IsDecoy;
                charges[i] = entry.Charge;
                scanNumbers[i] = entry.ScanNumber;
                modifiedSequences[i] = entry.ModifiedSequence ?? string.Empty;
                apexRts[i] = entry.ApexRt;
                startRts[i] = entry.StartRt;
                endRts[i] = entry.EndRt;
                boundsAreas[i] = entry.BoundsArea;
                boundsSnrs[i] = entry.BoundsSnr;
                fileNames[i] = fileName ?? string.Empty;
                fragmentMzs[i] = EncodeF64Blob(entry.FragmentMzs);
                fragmentIntensities[i] = EncodeF32Blob(entry.FragmentIntensities);
                refXicRts[i] = EncodeF64Blob(entry.ReferenceXicRts);
                refXicIntensities[i] = EncodeF64Blob(entry.ReferenceXicIntensities);

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
                cwtCandidates[i] = CwtCandidateCodec.Encode(
                    entry.CwtCandidates ?? (IReadOnlyList<CwtCandidate>)Array.Empty<CwtCandidate>());

                LibraryEntry libEntry = null;
                if (libraryById != null)
                    libraryById.TryGetValue(entry.EntryId, out libEntry);
                if (libEntry != null)
                {
                    sequences[i] = libEntry.Sequence ?? string.Empty;
                    precursorMzs[i] = libEntry.PrecursorMz;
                    proteinIds[i] = libEntry.ProteinIds != null
                        ? string.Join(";", libEntry.ProteinIds)
                        : null;
                }
                else
                {
                    sequences[i] = string.Empty;
                    precursorMzs[i] = 0.0;
                    proteinIds[i] = null;
                }

                var featureVec = entry.Features;
                if (featureVec != null && featureVec.Length == NUM_PIN_FEATURES)
                {
                    for (int f = 0; f < NUM_PIN_FEATURES; f++)
                        featureArrays[f][i] = Finite(featureVec[f]);
                }
                // else: leave zeros (entries without features can't drive Stage 5+).
            }

            string tempPath = Path.Combine(Path.GetTempPath(),
                string.Format("osprey_{0}_{1}", System.Diagnostics.Process.GetCurrentProcess().Id,
                    Path.GetFileName(path)));

            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            using (var writer = RunSync(ParquetWriter.CreateAsync(schema, stream)))
            {
                writer.CompressionMethod = CompressionMethod.Zstd;
                if (metadata != null && metadata.Count > 0)
                    writer.CustomMetadata = metadata;

                using (var group = writer.CreateRowGroup())
                {
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_ENTRY_ID, entryIds)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_IS_DECOY, isDecoys)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_SEQUENCE, sequences)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_MODIFIED_SEQUENCE, modifiedSequences)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_CHARGE, charges)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_PRECURSOR_MZ, precursorMzs)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_PROTEIN_IDS, proteinIds)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_SCAN_NUMBER, scanNumbers)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_APEX_RT, apexRts)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_START_RT, startRts)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_END_RT, endRts)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_BOUNDS_AREA, boundsAreas)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_BOUNDS_SNR, boundsSnrs)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_FILE_NAME, fileNames)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_CWT_CANDIDATES, cwtCandidates)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_FRAGMENT_MZS, fragmentMzs)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_FRAGMENT_INTENSITIES, fragmentIntensities)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_REFERENCE_XIC_RTS, refXicRts)));
                    RunSync(group.WriteColumnAsync(new DataColumn(FIELD_REFERENCE_XIC_INTENSITIES, refXicIntensities)));

                    for (int f = 0; f < NUM_PIN_FEATURES; f++)
                        RunSync(group.WriteColumnAsync(new DataColumn(featureFields[f], featureArrays[f])));
                }
            }

            if (File.Exists(path))
                File.Delete(path);
            File.Move(tempPath, path);
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
        /// byte-for-byte. A null or empty input encodes as a zero-length
        /// blob, NOT a null cell, so the column is non-nullable in
        /// practice.
        /// </summary>
        private static byte[] EncodeF64Blob(double[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<byte>();
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
        /// <c>BitConverter.SingleToInt32Bits</c>.
        /// </summary>
        private static byte[] EncodeF32Blob(float[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<byte>();
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
            var featureFields = BuildFeatureFields();

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
                        var cwtCol = ReadColumnByName<byte[][]>(groupReader, fieldsByName, FIELD_CWT_CANDIDATES.Name);
                        var boundsAreaCol = ReadColumnByName<double[]>(groupReader, fieldsByName, FIELD_BOUNDS_AREA.Name);
                        var boundsSnrCol = ReadColumnByName<double[]>(groupReader, fieldsByName, FIELD_BOUNDS_SNR.Name);
                        var fragMzCol = ReadColumnByName<byte[][]>(groupReader, fieldsByName, FIELD_FRAGMENT_MZS.Name);
                        var fragIntCol = ReadColumnByName<byte[][]>(groupReader, fieldsByName, FIELD_FRAGMENT_INTENSITIES.Name);
                        var refXicRtsCol = ReadColumnByName<byte[][]>(groupReader, fieldsByName, FIELD_REFERENCE_XIC_RTS.Name);
                        var refXicIntsCol = ReadColumnByName<byte[][]>(groupReader, fieldsByName, FIELD_REFERENCE_XIC_INTENSITIES.Name);

                        if (entryIdCol == null || isDecoyCol == null)
                            continue;

                        var featureCols = new double[NUM_PIN_FEATURES][];
                        for (int f = 0; f < NUM_PIN_FEATURES; f++)
                            featureCols[f] = ReadColumnByName<double[]>(groupReader, fieldsByName, featureFields[f].Name);

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
                                ParquetIndex = (uint)entries.Count,
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
        /// <c>--task MergeNode</c>) should consume for a given original
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

        #region Phase 3: --task FirstJoin group validation

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
        /// `check_parquet_metadata` helper. Returns null on success (with
        /// optional warning string in <paramref name="warning"/>) or an error
        /// message naming the file + offending field.
        /// </summary>
        public static string CheckParquetMetadata(
            string fileLabel,
            string cachedVersion,
            string cachedSearch,
            string cachedLibrary,
            string expectedSearch,
            string expectedLibrary,
            string currentVersion,
            out string warning)
        {
            warning = null;

            if (cachedVersion == null)
                return string.Format("{0}: parquet has no `osprey.version` metadata", fileLabel);
            int cY, cO, cB, cD, rY, rO, rB, rD;
            bool cachedOk = TryParseVersion(cachedVersion, out cY, out cO, out cB, out cD);
            bool currentOk = TryParseVersion(currentVersion, out rY, out rO, out rB, out rD);
            if (cachedOk && currentOk)
            {
                // YEAR.ORDINAL.BRANCH is the release identity: a difference means
                // the cache was produced by a different release line and is not
                // safe to reuse.
                if (cY != rY || cO != rO || cB != rB)
                {
                    return string.Format(
                        "{0}: osprey version mismatch: parquet was scored with {1} but current binary is {2} (incompatible release identity)",
                        fileLabel, cachedVersion, currentVersion);
                }
                // The day-of-year is daily-build drift within one release line:
                // warn but proceed.
                if (cD != rD)
                {
                    warning = string.Format(
                        "{0}: osprey daily-version drift (parquet={1}, current={2}); proceeding",
                        fileLabel, cachedVersion, currentVersion);
                }
            }
            else
            {
                warning = string.Format(
                    "{0}: could not parse osprey version (parquet=\"{1}\", current=\"{2}\"); proceeding",
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
        /// library hashes. Returns null on success (logging a warning to
        /// <paramref name="logWarning"/> for any patch-version drift) or an
        /// error message naming the offending file. Used at the start of
        /// --task FirstJoin mode.
        /// </summary>
        public static string ValidateScoresParquetGroup(
            IEnumerable<string> paths,
            OspreyConfig config,
            string currentVersion,
            Action<string> logWarning)
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

                string warning;
                string err = CheckParquetMetadata(
                    path, cachedV, cachedS, cachedL,
                    expectedSearch, expectedLibrary, currentVersion,
                    out warning);
                if (err != null)
                    return err;
                if (warning != null && logWarning != null)
                    logWarning(warning);

                // --task MergeNode strict reconciled-input gate. Mirrors
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
                            "--task MergeNode requires a reconciled (post-Stage-6) parquet, " +
                            "but {0} has osprey.reconciled = '{1}'. Either it is a Stage 4 " +
                            "(raw) parquet — run --task PerFileRescore to produce reconciled " +
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
