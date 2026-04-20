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

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Parquet;
using Parquet.Data;
using Parquet.Thrift;
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
        // so a C#-written parquet can be loaded by Rust's `--join-only`
        // (which does strict downcasts) and vice versa. Reading is also
        // strict: pre-2026-04-19 C#-written parquets used Int32 for these
        // fields and need to be regenerated via a fresh `--no-join` run.
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
        private static readonly DataField FIELD_PROTEIN_IDS = new DataField("protein_ids", DataType.String, hasNulls: true, isArray: false);
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
        private static readonly DataField FIELD_CWT_CANDIDATES = new DataField("cwt_candidates", DataType.ByteArray, hasNulls: true, isArray: false);
        private static readonly DataField FIELD_FRAGMENT_MZS = new DataField("fragment_mzs", DataType.ByteArray, hasNulls: true, isArray: false);
        private static readonly DataField FIELD_FRAGMENT_INTENSITIES = new DataField("fragment_intensities", DataType.ByteArray, hasNulls: true, isArray: false);
        private static readonly DataField FIELD_REFERENCE_XIC_RTS = new DataField("reference_xic_rts", DataType.ByteArray, hasNulls: true, isArray: false);
        private static readonly DataField FIELD_REFERENCE_XIC_INTENSITIES = new DataField("reference_xic_intensities", DataType.ByteArray, hasNulls: true, isArray: false);
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

        private static Schema BuildWriteSchema()
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
            fields.AddRange(BuildFeatureFields());
            return new Schema(fields.ToArray());
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
                throw new System.ArgumentNullException(nameof(path));
            if (entries == null || entries.Count == 0)
                return;

            int n = entries.Count;
            var schema = BuildWriteSchema();
            var featureFields = BuildFeatureFields();

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

            for (int i = 0; i < n; i++)
            {
                var entry = entries[i];
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
            using (var writer = new ParquetWriter(schema, stream))
            {
                writer.CompressionMethod = CompressionMethod.Snappy;

                // Set custom metadata if provided
                SetWriterMetadata(writer, metadata);

                using (var group = writer.CreateRowGroup())
                {
                    group.WriteColumn(new DataColumn(FIELD_ENTRY_ID, entryIds));
                    group.WriteColumn(new DataColumn(FIELD_IS_DECOY, isDecoys));
                    group.WriteColumn(new DataColumn(FIELD_SEQUENCE, sequences));
                    group.WriteColumn(new DataColumn(FIELD_MODIFIED_SEQUENCE, modifiedSequences));
                    group.WriteColumn(new DataColumn(FIELD_CHARGE, charges));
                    group.WriteColumn(new DataColumn(FIELD_PRECURSOR_MZ, precursorMzs));
                    group.WriteColumn(new DataColumn(FIELD_PROTEIN_IDS, proteinIds));
                    group.WriteColumn(new DataColumn(FIELD_SCAN_NUMBER, scanNumbers));
                    group.WriteColumn(new DataColumn(FIELD_APEX_RT, apexRts));
                    group.WriteColumn(new DataColumn(FIELD_START_RT, startRts));
                    group.WriteColumn(new DataColumn(FIELD_END_RT, endRts));
                    group.WriteColumn(new DataColumn(FIELD_BOUNDS_AREA, boundsAreas));
                    group.WriteColumn(new DataColumn(FIELD_BOUNDS_SNR, boundsSnrs));
                    group.WriteColumn(new DataColumn(FIELD_FILE_NAME, fileNames));
                    group.WriteColumn(new DataColumn(FIELD_CWT_CANDIDATES, cwtCandidates));
                    group.WriteColumn(new DataColumn(FIELD_FRAGMENT_MZS, fragmentMzs));
                    group.WriteColumn(new DataColumn(FIELD_FRAGMENT_INTENSITIES, fragmentIntensities));
                    group.WriteColumn(new DataColumn(FIELD_REFERENCE_XIC_RTS, refXicRts));
                    group.WriteColumn(new DataColumn(FIELD_REFERENCE_XIC_INTENSITIES, refXicIntensities));

                    for (int f = 0; f < NUM_PIN_FEATURES; f++)
                        group.WriteColumn(new DataColumn(featureFields[f], featureArrays[f]));
                }
            }

            // Move temp to final destination
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tempPath, path);
        }

        /// <summary>
        /// Write FdrEntry results to a Parquet file. Same schema as the
        /// CoelutionScoredEntry overload — used by --no-join HPC mode where
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
                throw new System.ArgumentNullException(nameof(path));
            if (entries == null || entries.Count == 0)
                return;

            int n = entries.Count;
            var schema = BuildWriteSchema();
            var featureFields = BuildFeatureFields();

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
            var boundsAreas = new double[n];   // not on FdrEntry; left at 0
            var boundsSnrs = new double[n];    // not on FdrEntry; left at 0
            var fileNames = new string[n];
            // Binary blobs are nullable placeholders -- C# doesn't currently
            // populate fragments / XICs / CWT byte serialization. Stage 5+8
            // don't need them; Stage 6 reconciliation does and is documented
            // as not yet supported cross-impl.
            var cwtCandidates = new byte[n][];
            var fragmentMzs = new byte[n][];
            var fragmentIntensities = new byte[n][];
            var refXicRts = new byte[n][];
            var refXicIntensities = new byte[n][];
            var featureArrays = new double[NUM_PIN_FEATURES][];
            for (int f = 0; f < NUM_PIN_FEATURES; f++)
                featureArrays[f] = new double[n];

            for (int i = 0; i < n; i++)
            {
                var entry = entries[i];
                entryIds[i] = entry.EntryId;
                isDecoys[i] = entry.IsDecoy;
                charges[i] = entry.Charge;
                scanNumbers[i] = entry.ScanNumber;
                modifiedSequences[i] = entry.ModifiedSequence ?? string.Empty;
                apexRts[i] = entry.ApexRt;
                startRts[i] = entry.StartRt;
                endRts[i] = entry.EndRt;
                fileNames[i] = fileName ?? string.Empty;

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
            using (var writer = new ParquetWriter(schema, stream))
            {
                writer.CompressionMethod = CompressionMethod.Snappy;
                SetWriterMetadata(writer, metadata);

                using (var group = writer.CreateRowGroup())
                {
                    group.WriteColumn(new DataColumn(FIELD_ENTRY_ID, entryIds));
                    group.WriteColumn(new DataColumn(FIELD_IS_DECOY, isDecoys));
                    group.WriteColumn(new DataColumn(FIELD_SEQUENCE, sequences));
                    group.WriteColumn(new DataColumn(FIELD_MODIFIED_SEQUENCE, modifiedSequences));
                    group.WriteColumn(new DataColumn(FIELD_CHARGE, charges));
                    group.WriteColumn(new DataColumn(FIELD_PRECURSOR_MZ, precursorMzs));
                    group.WriteColumn(new DataColumn(FIELD_PROTEIN_IDS, proteinIds));
                    group.WriteColumn(new DataColumn(FIELD_SCAN_NUMBER, scanNumbers));
                    group.WriteColumn(new DataColumn(FIELD_APEX_RT, apexRts));
                    group.WriteColumn(new DataColumn(FIELD_START_RT, startRts));
                    group.WriteColumn(new DataColumn(FIELD_END_RT, endRts));
                    group.WriteColumn(new DataColumn(FIELD_BOUNDS_AREA, boundsAreas));
                    group.WriteColumn(new DataColumn(FIELD_BOUNDS_SNR, boundsSnrs));
                    group.WriteColumn(new DataColumn(FIELD_FILE_NAME, fileNames));
                    group.WriteColumn(new DataColumn(FIELD_CWT_CANDIDATES, cwtCandidates));
                    group.WriteColumn(new DataColumn(FIELD_FRAGMENT_MZS, fragmentMzs));
                    group.WriteColumn(new DataColumn(FIELD_FRAGMENT_INTENSITIES, fragmentIntensities));
                    group.WriteColumn(new DataColumn(FIELD_REFERENCE_XIC_RTS, refXicRts));
                    group.WriteColumn(new DataColumn(FIELD_REFERENCE_XIC_INTENSITIES, refXicIntensities));

                    for (int f = 0; f < NUM_PIN_FEATURES; f++)
                        group.WriteColumn(new DataColumn(featureFields[f], featureArrays[f]));
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

        #endregion

        #region Load FDR Stubs

        /// <summary>
        /// Load only the columns needed for FDR stubs from a Parquet cache.
        /// Reads: entry_id, is_decoy, charge, scan_number, apex_rt, start_rt, end_rt,
        /// fragment_coelution_sum, modified_sequence.
        /// Sets parquet_index = row index.
        /// </summary>
        public static List<FdrEntry> LoadFdrStubsFromParquet(string path)
        {
            var stubs = new List<FdrEntry>();

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new ParquetReader(stream))
            {
                for (int g = 0; g < reader.RowGroupCount; g++)
                {
                    using (var groupReader = reader.OpenRowGroupReader(g))
                    {
                        var entryIdCol = groupReader.ReadColumn(FIELD_ENTRY_ID).Data as uint[];
                        var isDecoyCol = groupReader.ReadColumn(FIELD_IS_DECOY).Data as bool[];
                        var chargeCol = groupReader.ReadColumn(FIELD_CHARGE).Data as byte[];
                        var scanCol = groupReader.ReadColumn(FIELD_SCAN_NUMBER).Data as uint[];
                        var modseqCol = groupReader.ReadColumn(FIELD_MODIFIED_SEQUENCE).Data as string[];
                        var apexCol = groupReader.ReadColumn(FIELD_APEX_RT).Data as double[];
                        var startCol = groupReader.ReadColumn(FIELD_START_RT).Data as double[];
                        var endCol = groupReader.ReadColumn(FIELD_END_RT).Data as double[];
                        var coelutionCol = groupReader.ReadColumn(FIELD_COELUTION_SUM).Data as double[];

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
                                ModifiedSequence = modseqCol != null ? modseqCol[row] : string.Empty,
                            });
                        }
                    }
                }
            }

            return stubs;
        }

        #endregion

        #region Load PIN Features

        /// <summary>
        /// Load only the 21 PIN feature columns from a Parquet cache.
        /// Returns a list of feature vectors (one double[] per row).
        /// </summary>
        public static List<double[]> LoadPinFeaturesFromParquet(string path)
        {
            var allFeatures = new List<double[]>();
            var featureFields = BuildFeatureFields();

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new ParquetReader(stream))
            {
                for (int g = 0; g < reader.RowGroupCount; g++)
                {
                    using (var groupReader = reader.OpenRowGroupReader(g))
                    {
                        // Read all feature columns
                        var featureCols = new double[NUM_PIN_FEATURES][];
                        for (int f = 0; f < NUM_PIN_FEATURES; f++)
                        {
                            featureCols[f] = groupReader.ReadColumn(featureFields[f]).Data as double[];
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

        #region Writer Metadata

        /// <summary>
        /// Sets custom key-value metadata on a ParquetWriter via reflection.
        /// ParquetNet stores the Thrift FileMetaData internally; we walk the
        /// inheritance chain to find the internal _meta or _fileMeta field
        /// and populate its Key_value_metadata list before the writer disposes
        /// and writes the footer.
        /// </summary>
        private static void SetWriterMetadata(ParquetWriter writer, Dictionary<string, string> metadata)
        {
            if (metadata == null || metadata.Count == 0)
                return;

            try
            {
                // ParquetNet stores metadata on the internal ThriftFooter (_footer field).
                // ThriftFooter has a CustomMetadata property (Dictionary<string,string>)
                // and an internal _fileMeta of type FileMetaData with Key_value_metadata.

                // 1) Try public CustomMetadata on ParquetWriter itself (newer versions)
                var writerProp = writer.GetType().GetProperty("CustomMetadata",
                    BindingFlags.Public | BindingFlags.Instance);
                if (writerProp != null && writerProp.CanWrite)
                {
                    writerProp.SetValue(writer, metadata);
                    return;
                }

                // 2) Find _footer field on ParquetWriter
                var footerField = writer.GetType().GetField("_footer",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (footerField == null)
                    return;

                var footer = footerField.GetValue(writer);
                if (footer == null)
                    return;

                var footerType = footer.GetType();

                // 3) Try CustomMetadata property on the footer (ThriftFooter)
                var footerProp = footerType.GetProperty("CustomMetadata",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (footerProp != null && footerProp.CanWrite)
                {
                    footerProp.SetValue(footer, metadata);
                    return;
                }

                // 4) Fall back to setting Key_value_metadata directly on _fileMeta
                var fileMetaField = footerType.GetField("_fileMeta",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (fileMetaField == null)
                    return;

                var fileMeta = fileMetaField.GetValue(footer) as FileMetaData;
                if (fileMeta == null)
                    return;

                if (fileMeta.Key_value_metadata == null)
                    fileMeta.Key_value_metadata = new List<KeyValue>();

                foreach (var kvp in metadata)
                {
                    fileMeta.Key_value_metadata.Add(new KeyValue
                    {
                        Key = kvp.Key,
                        Value = kvp.Value,
                    });
                }
            }
            catch
            {
                // Metadata writing is best-effort; cache still functions without it
            }
        }

        #endregion

        #region Path and Metadata Helpers

        /// <summary>
        /// Returns the scores Parquet path for a given mzML path: {stem}.scores.parquet
        /// in the same directory.
        /// </summary>
        public static string GetScoresPath(string mzmlPath)
        {
            string dir = Path.GetDirectoryName(mzmlPath) ?? string.Empty;
            string stem = Path.GetFileNameWithoutExtension(mzmlPath);
            return Path.Combine(dir, stem + ".scores.parquet");
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
                using (var reader = new ParquetReader(stream))
                {
                    var fileMetadata = reader.ThriftMetadata.Key_value_metadata;
                    if (fileMetadata == null)
                        return expected == null || expected.Count == 0;

                    var metaDict = new Dictionary<string, string>();
                    foreach (var kv in fileMetadata)
                        metaDict[kv.Key] = kv.Value;

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

        #region Phase 3: --join-only group validation

        /// <summary>
        /// Read all key-value pairs from a parquet footer. Returns an empty
        /// dictionary if the file has no metadata. Throws on IO/parse errors.
        /// </summary>
        public static Dictionary<string, string> LoadFooterMetadata(string path)
        {
            var result = new Dictionary<string, string>();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new ParquetReader(stream))
            {
                var fileMetadata = reader.ThriftMetadata.Key_value_metadata;
                if (fileMetadata == null)
                    return result;
                foreach (var kv in fileMetadata)
                    result[kv.Key] = kv.Value;
            }
            return result;
        }

        /// <summary>
        /// Parse a "MAJOR.MINOR.PATCH" version string. Returns true on success
        /// with the three components in <paramref name="major"/>, <paramref name="minor"/>,
        /// <paramref name="patch"/>. Returns false if any component is missing
        /// or non-numeric. Used by <see cref="CheckParquetMetadata"/>.
        /// </summary>
        public static bool TryParseVersion(string s, out int major, out int minor, out int patch)
        {
            major = minor = patch = 0;
            if (string.IsNullOrEmpty(s))
                return false;
            string[] parts = s.Split('.');
            if (parts.Length != 3)
                return false;
            return int.TryParse(parts[0], out major)
                && int.TryParse(parts[1], out minor)
                && int.TryParse(parts[2], out patch);
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
            int cM, cmn, cp, rM, rmn, rp;
            bool cachedOk = TryParseVersion(cachedVersion, out cM, out cmn, out cp);
            bool currentOk = TryParseVersion(currentVersion, out rM, out rmn, out rp);
            if (cachedOk && currentOk)
            {
                if (cM != rM || cmn != rmn)
                {
                    return string.Format(
                        "{0}: osprey version mismatch: parquet was scored with {1} but current binary is {2} (incompatible major/minor)",
                        fileLabel, cachedVersion, currentVersion);
                }
                if (cp != rp)
                {
                    warning = string.Format(
                        "{0}: osprey patch-version drift (parquet={1}, current={2}); proceeding",
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
        /// --join-only mode.
        /// </summary>
        public static string ValidateScoresParquetGroup(
            IEnumerable<string> paths,
            OspreyConfig config,
            string currentVersion,
            System.Action<string> logWarning)
        {
            string expectedSearch = config.SearchParameterHash();
            string expectedLibrary = config.LibraryIdentityHash();

            foreach (string path in paths)
            {
                Dictionary<string, string> kv;
                try
                {
                    kv = LoadFooterMetadata(path);
                }
                catch (System.Exception ex)
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
            }
            return null;
        }

        #endregion
    }
}
