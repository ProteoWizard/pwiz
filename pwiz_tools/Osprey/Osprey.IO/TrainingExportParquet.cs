/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
    /// <c>&lt;stem&gt;.training.parquet</c>: one row per exported precursor
    /// (<see cref="TrainingRecord"/>), scalar columns for the precursor and little-endian typed
    /// blobs (<see cref="ParquetBlobCodec"/>, the scores parquet's convention) for the per-ion
    /// arrays, ZSTD, written through <see cref="FileSaver"/>. A run with nothing to export
    /// still gets the file - schema and footer, zero rows - so absence always means the task
    /// did not run (P13). The column list below IS the schema; docs/22-training-export.md
    /// documents every column and must change with it.
    /// </summary>
    public static class TrainingExportParquet
    {
        /// <summary>Written to the footer as <see cref="KEY_FORMAT_VERSION"/>.</summary>
        public const int FORMAT_VERSION = 1;

        public const string KEY_FORMAT_VERSION = @"osprey.training_export.format_version";

        /// <summary>Rows per parquet row group; the per-ion blobs make rows wide.</summary>
        private const int ROWS_PER_GROUP = 20_000;

        private static readonly ColumnSpec[] COLUMNS =
        {
            Scalar(@"entry_id", r => r.EntryId, (r, v) => r.EntryId = v),
            Scalar(@"base_id", r => r.EntryId & 0x7FFFFFFFu, (r, v) => { }),
            Scalar(@"is_decoy", r => r.IsDecoy, (r, v) => r.IsDecoy = v),
            Scalar(@"is_entrapment", r => r.IsEntrapment, (r, v) => r.IsEntrapment = v),
            Text(@"peptide_kind", r => r.PeptideKind, (r, v) => r.PeptideKind = v),
            Text(@"sequence", r => r.Sequence, (r, v) => r.Sequence = v),
            Text(@"modified_sequence", r => r.ModifiedSequence, (r, v) => r.ModifiedSequence = v),
            Blob(@"mod_positions", r => ParquetBlobCodec.EncodeI32Blob(r.ModPositions), (r, b) => r.ModPositions = ParquetBlobCodec.DecodeI32Blob(b)),
            Blob(@"mod_masses", r => ParquetBlobCodec.EncodeF64Blob(r.ModMasses), (r, b) => r.ModMasses = ParquetBlobCodec.DecodeF64Blob(b)),
            Blob(@"mod_unimod_ids", r => ParquetBlobCodec.EncodeI32Blob(r.ModUnimodIds), (r, b) => r.ModUnimodIds = ParquetBlobCodec.DecodeI32Blob(b)),
            Scalar(@"charge", r => r.Charge, (r, v) => r.Charge = v),
            Scalar(@"precursor_mz", r => r.PrecursorMz, (r, v) => r.PrecursorMz = v),
            Scalar(@"library_rt", r => r.LibraryRt, (r, v) => r.LibraryRt = v),
            Text(@"protein_ids", r => r.ProteinIds, (r, v) => r.ProteinIds = v),
            Text(@"file_name", r => r.FileName, (r, v) => r.FileName = v),
            Scalar(@"scan_number", r => r.ScanNumber, (r, v) => r.ScanNumber = v),
            Scalar(@"apex_rt", r => r.ApexRt, (r, v) => r.ApexRt = v),
            Scalar(@"start_rt", r => r.StartRt, (r, v) => r.StartRt = v),
            Scalar(@"end_rt", r => r.EndRt, (r, v) => r.EndRt = v),
            Scalar(@"n_peak_scans", r => r.NPeakScans, (r, v) => r.NPeakScans = v),
            Scalar(@"isolation_lower", r => r.IsolationLower, (r, v) => r.IsolationLower = v),
            Scalar(@"isolation_upper", r => r.IsolationUpper, (r, v) => r.IsolationUpper = v),
            Scalar(@"bounds_area", r => r.BoundsArea, (r, v) => r.BoundsArea = v),
            Scalar(@"coelution_sum", r => r.CoelutionSum, (r, v) => r.CoelutionSum = v),
            Scalar(@"score", r => r.Score, (r, v) => r.Score = v),
            Scalar(@"run_precursor_q", r => r.RunPrecursorQ, (r, v) => r.RunPrecursorQ = v),
            Scalar(@"run_peptide_q", r => r.RunPeptideQ, (r, v) => r.RunPeptideQ = v),
            Scalar(@"experiment_precursor_q", r => r.ExperimentPrecursorQ, (r, v) => r.ExperimentPrecursorQ = v),
            Scalar(@"experiment_peptide_q", r => r.ExperimentPeptideQ, (r, v) => r.ExperimentPeptideQ = v),
            Scalar(@"experiment_protein_q", r => r.ExperimentProteinQ, (r, v) => r.ExperimentProteinQ = v),
            Scalar(@"pep", r => r.Pep, (r, v) => r.Pep = v),
            Scalar(@"apex_tic", r => r.ApexTic, (r, v) => r.ApexTic = v),
            Scalar(@"explained_intensity", r => r.ExplainedIntensity, (r, v) => r.ExplainedIntensity = v),
            Scalar(@"n_slots", r => r.NSlots, (r, v) => r.NSlots = v),
            Scalar(@"n_ions_applicable", r => r.NIonsApplicable, (r, v) => r.NIonsApplicable = v),
            Scalar(@"n_ions_observed", r => r.NIonsObserved, (r, v) => r.NIonsObserved = v),
            Scalar(@"mp_fitted", r => r.MpFitted, (r, v) => r.MpFitted = v),
            Scalar(@"mp_converged", r => r.MpConverged, (r, v) => r.MpConverged = v),
            Scalar(@"mp_iterations", r => r.MpIterations, (r, v) => r.MpIterations = v),
            Scalar(@"mp_overall", r => r.MpOverall, (r, v) => r.MpOverall = v),
            Scalar(@"mp_cosine", r => r.MpCosine, (r, v) => r.MpCosine = v),
            Scalar(@"mp_cosine_parity", r => r.MpCosineParity, (r, v) => r.MpCosineParity = v),
            Scalar(@"mp_residual_mad", r => r.MpResidualMad, (r, v) => r.MpResidualMad = v),
            Scalar(@"mp_n_core", r => r.MpNCore, (r, v) => r.MpNCore = v),
            Scalar(@"mp_n_fragments_used", r => r.MpNFragmentsUsed, (r, v) => r.MpNFragmentsUsed = v),
            Scalar(@"boundary_start_ratio_median", r => r.BoundaryStartRatioMedian, (r, v) => r.BoundaryStartRatioMedian = v),
            Scalar(@"boundary_end_ratio_median", r => r.BoundaryEndRatioMedian, (r, v) => r.BoundaryEndRatioMedian = v),
            Scalar(@"n_coeluting_claimants", r => r.NCoelutingClaimants, (r, v) => r.NCoelutingClaimants = v),
            Scalar(@"n_same_apex_claimants", r => r.NSameApexClaimants, (r, v) => r.NSameApexClaimants = v),
            Scalar(@"ddc_neighbor_n", r => r.DdcNeighborCount, (r, v) => r.DdcNeighborCount = v),
            Blob(@"ion_mz", r => ParquetBlobCodec.EncodeF64Blob(r.IonMz), (r, b) => r.IonMz = ParquetBlobCodec.DecodeF64Blob(b)),
            Blob(@"ion_flags", r => ParquetBlobCodec.EncodeU8Blob(r.IonFlags), (r, b) => r.IonFlags = ParquetBlobCodec.DecodeU8Blob(b)),
            F32(@"apex_intensity", r => r.ApexIntensity, (r, v) => r.ApexIntensity = v),
            F32(@"apex_mz_error", r => r.ApexMzError, (r, v) => r.ApexMzError = v),
            F32(@"library_rel_intensity", r => r.LibraryRelIntensity, (r, v) => r.LibraryRelIntensity = v),
            Blob(@"n_finite_scans", r => ParquetBlobCodec.EncodeU16Blob(r.NFiniteScans), (r, b) => r.NFiniteScans = ParquetBlobCodec.DecodeU16Blob(b)),
            F32(@"xic_start", r => r.XicStart, (r, v) => r.XicStart = v),
            F32(@"xic_end", r => r.XicEnd, (r, v) => r.XicEnd = v),
            F32(@"xic_max", r => r.XicMax, (r, v) => r.XicMax = v),
            F32(@"corr_polish", r => r.CorrPolish, (r, v) => r.CorrPolish = v),
            F32(@"corr_reference", r => r.CorrReference, (r, v) => r.CorrReference = v),
            F32(@"polish_row_effect", r => r.PolishRowEffect, (r, v) => r.PolishRowEffect = v),
            F32(@"polish_r2", r => r.PolishR2, (r, v) => r.PolishR2 = v),
            F32(@"polish_pos_resid_max", r => r.PolishPosResidMax, (r, v) => r.PolishPosResidMax = v),
            F32(@"polish_apex_residual", r => r.PolishApexResidual, (r, v) => r.PolishApexResidual = v),
            F32(@"polish_outlier_z", r => r.PolishOutlierZ, (r, v) => r.PolishOutlierZ = v),
            F32(@"polish_apex_ratio", r => r.PolishApexRatio, (r, v) => r.PolishApexRatio = v),
            F32(@"polish_rel_intensity", r => r.PolishRelIntensity, (r, v) => r.PolishRelIntensity = v),
            Blob(@"shared_apex_n", r => ParquetBlobCodec.EncodeU8Blob(r.SharedApexN), (r, b) => r.SharedApexN = ParquetBlobCodec.DecodeU8Blob(b)),
            Blob(@"shared_coelute_n", r => ParquetBlobCodec.EncodeU8Blob(r.SharedCoeluteN), (r, b) => r.SharedCoeluteN = ParquetBlobCodec.DecodeU8Blob(b)),
            F32(@"min_claimant_q", r => r.MinClaimantQ, (r, v) => r.MinClaimantQ = v),
        };

        /// <summary>The optional XIC matrix columns (<c>--training-export-xics</c>).</summary>
        private static readonly ColumnSpec[] XIC_COLUMNS =
        {
            Blob(@"xic_rts", r => ParquetBlobCodec.EncodeF64Blob(r.XicRts), (r, b) => r.XicRts = ParquetBlobCodec.DecodeF64Blob(b)),
            F32(@"xic_intensities", r => r.XicIntensities, (r, v) => r.XicIntensities = v),
        };

        /// <summary>
        /// <c>&lt;stem&gt;.training.parquet</c> in the run's output directory
        /// (<see cref="ArtifactPaths.ResolveOutputDir"/>), beside its other products.
        /// </summary>
        public static string PathFor(string inputFile)
        {
            string fileName = Path.GetFileNameWithoutExtension(inputFile) + @".training.parquet";
            return Path.Combine(ArtifactPaths.ResolveOutputDir(inputFile), fileName);
        }

        /// <summary>
        /// The column names in schema order, with or without the XIC matrix - the list
        /// docs/22 documents.
        /// </summary>
        public static IEnumerable<string> ColumnNames(bool withXics)
        {
            return Columns(withXics).Select(c => c.Field.Name);
        }

        /// <summary>
        /// Write <paramref name="records"/> (already in their final order) with
        /// <paramref name="metadata"/> in the footer. Zero records write a valid zero-row file.
        /// </summary>
        public static void Write(string path, IReadOnlyList<TrainingRecord> records,
            Dictionary<string, string> metadata, bool withXics)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            var columns = Columns(withXics).ToArray();
            var schema = new ParquetSchema(columns.Select(c => (Field)c.Field).ToArray());
            var footer = new Dictionary<string, string>(metadata ?? new Dictionary<string, string>())
            {
                [KEY_FORMAT_VERSION] = FORMAT_VERSION.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            using (var saver = new FileSaver(path))
            {
                using (var stream = new FileStream(saver.SafeName, FileMode.Create, FileAccess.Write))
                using (var writer = RunSync(ParquetWriter.CreateAsync(schema, stream)))
                {
                    writer.CompressionMethod = CompressionMethod.Zstd;
                    writer.CustomMetadata = footer;
                    for (int start = 0; start < records.Count; start += ROWS_PER_GROUP)
                    {
                        int count = Math.Min(ROWS_PER_GROUP, records.Count - start);
                        var chunk = new TrainingRecord[count];
                        for (int i = 0; i < count; i++)
                            chunk[i] = records[start + i];
                        using (var group = writer.CreateRowGroup())
                        {
                            foreach (var column in columns)
                                RunSync(group.WriteColumnAsync(new DataColumn(column.Field, column.Build(chunk))));
                        }
                    }
                }
                saver.Commit();
            }
        }

        /// <summary>
        /// Read a training parquet back into records, with its footer. For tests and tools:
        /// a column absent from the file (the XIC matrix, say) leaves its field null.
        /// </summary>
        public static List<TrainingRecord> Read(string path, out Dictionary<string, string> metadata)
        {
            var records = new List<TrainingRecord>();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = RunSync(ParquetReader.CreateAsync(stream)))
            {
                // Parquet.Net 4.x's CustomMetadata is non-null (empty when the file has none).
                metadata = new Dictionary<string, string>(reader.CustomMetadata);
                var fieldsByName = reader.Schema.GetDataFields().ToDictionary(f => f.Name);
                var specs = Columns(true).ToArray();
                for (int g = 0; g < reader.RowGroupCount; g++)
                {
                    using (var group = reader.OpenRowGroupReader(g))
                    {
                        var chunk = new TrainingRecord[checked((int)group.RowCount)];
                        for (int i = 0; i < chunk.Length; i++)
                            chunk[i] = new TrainingRecord();
                        foreach (var spec in specs)
                        {
                            if (!fieldsByName.TryGetValue(spec.Field.Name, out var field))
                                continue;
                            spec.Assign(chunk, RunSync(group.ReadColumnAsync(field)).Data);
                        }
                        records.AddRange(chunk);
                    }
                }
            }
            return records;
        }

        private static IEnumerable<ColumnSpec> Columns(bool withXics)
        {
            return withXics ? COLUMNS.Concat(XIC_COLUMNS) : COLUMNS;
        }

        private static ColumnSpec Scalar<T>(string name, Func<TrainingRecord, T> get, Action<TrainingRecord, T> set)
        {
            return new ColumnSpec(new DataField<T>(name),
                rows =>
                {
                    var values = new T[rows.Length];
                    for (int i = 0; i < rows.Length; i++)
                        values[i] = get(rows[i]);
                    return values;
                },
                (rows, data) =>
                {
                    var values = (T[])data;
                    for (int i = 0; i < rows.Length; i++)
                        set(rows[i], values[i]);
                });
        }

        private static ColumnSpec Text(string name, Func<TrainingRecord, string> get, Action<TrainingRecord, string> set)
        {
            return new ColumnSpec(new DataField(name, typeof(string), isNullable: true, isArray: false),
                rows => rows.Select(get).ToArray(),
                (rows, data) =>
                {
                    var values = (string[])data;
                    for (int i = 0; i < rows.Length; i++)
                        set(rows[i], values[i]);
                });
        }

        private static ColumnSpec Blob(string name, Func<TrainingRecord, byte[]> get, Action<TrainingRecord, byte[]> set)
        {
            return new ColumnSpec(new DataField(name, typeof(byte[]), isNullable: true, isArray: false),
                rows => rows.Select(get).ToArray(),
                (rows, data) =>
                {
                    var values = (byte[][])data;
                    for (int i = 0; i < rows.Length; i++)
                        set(rows[i], values[i]);
                });
        }

        private static ColumnSpec F32(string name, Func<TrainingRecord, float[]> get, Action<TrainingRecord, float[]> set)
        {
            return Blob(name, r => ParquetBlobCodec.EncodeF32Blob(get(r)), (r, b) => set(r, ParquetBlobCodec.DecodeF32Blob(b)));
        }

        // Synchronously bridge an async Parquet.Net call, as ParquetScoreCache does: these run
        // with no captured SynchronizationContext, so GetResult cannot deadlock, and it rethrows
        // the original exception rather than an AggregateException.
        private static T RunSync<T>(Task<T> task)
        {
            return task.GetAwaiter().GetResult();
        }

        private static void RunSync(Task task)
        {
            task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// One column: its field (the instance attached to the schema, which Parquet.Net
        /// requires the written <see cref="DataColumn"/> to share), how to build its array from
        /// a chunk of records, and how to assign it back.
        /// </summary>
        private sealed class ColumnSpec
        {
            public ColumnSpec(DataField field, Func<TrainingRecord[], Array> build, Action<TrainingRecord[], Array> assign)
            {
                Field = field;
                Build = build;
                Assign = assign;
            }

            public DataField Field { get; }
            public Func<TrainingRecord[], Array> Build { get; }
            public Action<TrainingRecord[], Array> Assign { get; }
        }
    }
}
