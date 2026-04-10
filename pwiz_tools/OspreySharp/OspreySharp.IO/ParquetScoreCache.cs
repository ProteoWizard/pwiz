using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public static readonly string[] PIN_FEATURE_NAMES = new string[]
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

        private static readonly DataField FIELD_ENTRY_ID = new DataField<int>("entry_id");
        private static readonly DataField FIELD_IS_DECOY = new DataField<bool>("is_decoy");
        private static readonly DataField FIELD_CHARGE = new DataField<int>("charge");
        private static readonly DataField FIELD_SCAN_NUMBER = new DataField<int>("scan_number");
        private static readonly DataField FIELD_MODIFIED_SEQUENCE = new DataField<string>("modified_sequence");
        private static readonly DataField FIELD_APEX_RT = new DataField<double>("apex_rt");
        private static readonly DataField FIELD_START_RT = new DataField<double>("start_rt");
        private static readonly DataField FIELD_END_RT = new DataField<double>("end_rt");
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
            var fields = new List<DataField>
            {
                FIELD_ENTRY_ID,
                FIELD_IS_DECOY,
                FIELD_CHARGE,
                FIELD_SCAN_NUMBER,
                FIELD_MODIFIED_SEQUENCE,
                FIELD_APEX_RT,
                FIELD_START_RT,
                FIELD_END_RT,
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
            if (entries == null || entries.Count == 0)
                return;

            int n = entries.Count;
            var schema = BuildWriteSchema();
            var featureFields = BuildFeatureFields();

            // Build column arrays
            var entryIds = new int[n];
            var isDecoys = new bool[n];
            var charges = new int[n];
            var scanNumbers = new int[n];
            var modifiedSequences = new string[n];
            var apexRts = new double[n];
            var startRts = new double[n];
            var endRts = new double[n];
            var featureArrays = new double[NUM_PIN_FEATURES][];
            for (int f = 0; f < NUM_PIN_FEATURES; f++)
                featureArrays[f] = new double[n];

            for (int i = 0; i < n; i++)
            {
                var entry = entries[i];
                entryIds[i] = (int)entry.EntryId;
                isDecoys[i] = entry.IsDecoy;
                charges[i] = entry.Charge;
                scanNumbers[i] = (int)entry.ScanNumber;
                modifiedSequences[i] = entry.ModifiedSequence ?? string.Empty;
                apexRts[i] = entry.ApexRt;
                startRts[i] = entry.PeakBounds != null ? entry.PeakBounds.StartRt : 0.0;
                endRts[i] = entry.PeakBounds != null ? entry.PeakBounds.EndRt : 0.0;

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
                    group.WriteColumn(new DataColumn(FIELD_CHARGE, charges));
                    group.WriteColumn(new DataColumn(FIELD_SCAN_NUMBER, scanNumbers));
                    group.WriteColumn(new DataColumn(FIELD_MODIFIED_SEQUENCE, modifiedSequences));
                    group.WriteColumn(new DataColumn(FIELD_APEX_RT, apexRts));
                    group.WriteColumn(new DataColumn(FIELD_START_RT, startRts));
                    group.WriteColumn(new DataColumn(FIELD_END_RT, endRts));

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
                        var entryIdCol = groupReader.ReadColumn(FIELD_ENTRY_ID).Data as int[];
                        var isDecoyCol = groupReader.ReadColumn(FIELD_IS_DECOY).Data as bool[];
                        var chargeCol = groupReader.ReadColumn(FIELD_CHARGE).Data as int[];
                        var scanCol = groupReader.ReadColumn(FIELD_SCAN_NUMBER).Data as int[];
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
                                EntryId = (uint)entryIdCol[row],
                                ParquetIndex = (uint)(stubs.Count),
                                IsDecoy = isDecoyCol[row],
                                Charge = (byte)(chargeCol != null ? chargeCol[row] : 0),
                                ScanNumber = (uint)(scanCol != null ? scanCol[row] : 0),
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
    }
}
