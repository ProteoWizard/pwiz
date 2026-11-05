/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Parquet;
using Parquet.Schema;

namespace pwiz.BiblioSpec.IO
{
    /// <summary>
    /// The columns of a DIA-NN 2.x library <c>.parquet</c> (<c>report-lib.parquet</c>, or the
    /// <c>--lib x.speclib --gen-spec-lib --out-lib x.parquet</c> conversion of a predicted
    /// library) that a reader can ask <see cref="DiannLibraryParquetReader"/> for.
    /// </summary>
    [Flags]
    public enum DiannLibraryColumns
    {
        none = 0,
        precursor_id = 1 << 0,
        modified_sequence = 1 << 1,
        stripped_sequence = 1 << 2,
        precursor_charge = 1 << 3,
        precursor_mz = 1 << 4,
        rt = 1 << 5,
        im = 1 << 6,
        q_value = 1 << 7,
        decoy = 1 << 8,
        proteotypic = 1 << 9,
        flags = 1 << 10,
        product_mz = 1 << 11,
        relative_intensity = 1 << 12,
        fragment_type = 1 << 13,
        fragment_charge = 1 << 14,
        fragment_series_number = 1 << 15,
        fragment_loss_type = 1 << 16,
        protein_ids = 1 << 17,
        genes = 1 << 18,
    }

    /// <summary>
    /// Reads a DIA-NN 2.x spectral library <c>.parquet</c>: the one definition of its column
    /// names and types, shared by BiblioSpec's <c>DiaNNSpecLibReader</c> and Osprey's library
    /// loader so the two cannot read the format differently. It knows the file and nothing of
    /// either tool's model: each caller assembles its own entries from the columns.
    ///
    /// <para>The file is long format, one row per fragment, the precursor columns repeated on
    /// every row of a precursor. DIA-NN 2.7 writes floats as float32 and integers as int64, none
    /// nullable; a column written at another width, or nullable, is converted (a null reads as
    /// zero, or the empty string). The row groups are read one at a time and NONE is
    /// representative: DIA-NN 2.7 writes an empty row group beside the rows, and an empty
    /// library (a search that identified nothing) has only that one.</para>
    ///
    /// <para>Synchronous: Parquet.Net's calls are waited on here, as Osprey's other parquet
    /// readers wait on them, with no captured synchronization context to deadlock on.</para>
    /// </summary>
    public sealed class DiannLibraryParquetReader : IDisposable
    {
        /// <summary>The DIA-NN column name of each <see cref="DiannLibraryColumns"/> member.</summary>
        public static readonly IReadOnlyList<KeyValuePair<DiannLibraryColumns, string>> COLUMN_NAMES = new[]
        {
            Column(DiannLibraryColumns.precursor_id, @"Precursor.Id"),
            Column(DiannLibraryColumns.modified_sequence, @"Modified.Sequence"),
            Column(DiannLibraryColumns.stripped_sequence, @"Stripped.Sequence"),
            Column(DiannLibraryColumns.precursor_charge, @"Precursor.Charge"),
            Column(DiannLibraryColumns.precursor_mz, @"Precursor.Mz"),
            Column(DiannLibraryColumns.rt, @"RT"),
            Column(DiannLibraryColumns.im, @"IM"),
            Column(DiannLibraryColumns.q_value, @"Q.Value"),
            Column(DiannLibraryColumns.decoy, @"Decoy"),
            Column(DiannLibraryColumns.proteotypic, @"Proteotypic"),
            Column(DiannLibraryColumns.flags, @"Flags"),
            Column(DiannLibraryColumns.product_mz, @"Product.Mz"),
            Column(DiannLibraryColumns.relative_intensity, @"Relative.Intensity"),
            Column(DiannLibraryColumns.fragment_type, @"Fragment.Type"),
            Column(DiannLibraryColumns.fragment_charge, @"Fragment.Charge"),
            Column(DiannLibraryColumns.fragment_series_number, @"Fragment.Series.Number"),
            Column(DiannLibraryColumns.fragment_loss_type, @"Fragment.Loss.Type"),
            Column(DiannLibraryColumns.protein_ids, @"Protein.Ids"),
            Column(DiannLibraryColumns.genes, @"Genes"),
        };

        /// <summary>The DIA-NN column name of <paramref name="column"/>, a single member.</summary>
        public static string ColumnName(DiannLibraryColumns column)
        {
            foreach (var kv in COLUMN_NAMES)
            {
                if (kv.Key == column)
                    return kv.Value;
            }
            throw new ArgumentException(column.ToString());
        }

        /// <summary>
        /// Opens the library at <paramref name="path"/> to read <paramref name="columns"/>. Every
        /// one of them must be in the file, except those also in <paramref name="optional"/>,
        /// which read as null arrays when absent (<see cref="Has"/>); a missing required column
        /// is a <see cref="DiannLibraryMissingColumnException"/>, for the caller to report in its
        /// own words.
        /// </summary>
        public static DiannLibraryParquetReader Open(string path, DiannLibraryColumns columns,
            DiannLibraryColumns optional = DiannLibraryColumns.none)
        {
            var stream = File.OpenRead(path);
            try
            {
                var reader = RunSync(ParquetReader.CreateAsync(stream));
                try
                {
                    return new DiannLibraryParquetReader(path, stream, reader, columns, optional);
                }
                catch
                {
                    RunSync(reader.DisposeAsync());
                    throw;
                }
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        private readonly Stream _stream;
        private readonly ParquetReader _reader;
        private readonly Dictionary<string, DataField> _fieldsByName = new Dictionary<string, DataField>(StringComparer.Ordinal);
        private readonly Dictionary<DiannLibraryColumns, DataField> _fields = new Dictionary<DiannLibraryColumns, DataField>();

        private DiannLibraryParquetReader(string path, Stream stream, ParquetReader reader,
            DiannLibraryColumns columns, DiannLibraryColumns optional)
        {
            Path = path;
            _stream = stream;
            _reader = reader;
            foreach (var field in reader.Schema.GetDataFields())
                _fieldsByName[field.Name] = field;
            Require(columns & ~optional);
            foreach (var kv in COLUMN_NAMES)
            {
                if ((columns & kv.Key) != 0 && _fieldsByName.TryGetValue(kv.Value, out var field))
                    _fields[kv.Key] = field;
            }
            RowCount = reader.Metadata?.NumRows ?? 0;
        }

        /// <summary>The library file.</summary>
        public string Path { get; }

        /// <summary>The number of row groups, empty ones included.</summary>
        public int RowGroupCount => _reader.RowGroupCount;

        /// <summary>The rows of the file, one per fragment, over every row group.</summary>
        public long RowCount { get; }

        /// <summary>True when <paramref name="column"/> was asked for and is in the file.</summary>
        public bool Has(DiannLibraryColumns column)
        {
            return _fields.ContainsKey(column);
        }

        /// <summary>
        /// Requires every one of <paramref name="columns"/> to be in the file, read or not: a
        /// <see cref="DiannLibraryMissingColumnException"/> names the first that is not.
        /// </summary>
        public void Require(DiannLibraryColumns columns)
        {
            foreach (var kv in COLUMN_NAMES)
            {
                if ((columns & kv.Key) != 0 && !_fieldsByName.ContainsKey(kv.Value))
                    throw new DiannLibraryMissingColumnException(Path, kv.Value);
            }
        }

        /// <summary>
        /// The columns asked for, read from row group <paramref name="index"/>; an absent
        /// optional column, or one not asked for, is null.
        /// </summary>
        public DiannLibraryRowGroup ReadRowGroup(int index)
        {
            using (var group = _reader.OpenRowGroupReader(index))
            {
                int rowCount = checked((int)group.RowCount);
                return new DiannLibraryRowGroup
                {
                    RowCount = rowCount,
                    PrecursorId = ReadStrings(group, DiannLibraryColumns.precursor_id),
                    ModifiedSequence = ReadStrings(group, DiannLibraryColumns.modified_sequence),
                    StrippedSequence = ReadStrings(group, DiannLibraryColumns.stripped_sequence),
                    PrecursorCharge = ReadLongs(group, DiannLibraryColumns.precursor_charge),
                    PrecursorMz = ReadFloats(group, DiannLibraryColumns.precursor_mz),
                    RT = ReadFloats(group, DiannLibraryColumns.rt),
                    IM = ReadFloats(group, DiannLibraryColumns.im),
                    QValue = ReadFloats(group, DiannLibraryColumns.q_value),
                    Decoy = ReadLongs(group, DiannLibraryColumns.decoy),
                    Proteotypic = ReadLongs(group, DiannLibraryColumns.proteotypic),
                    Flags = ReadLongs(group, DiannLibraryColumns.flags),
                    ProductMz = ReadFloats(group, DiannLibraryColumns.product_mz),
                    RelativeIntensity = ReadFloats(group, DiannLibraryColumns.relative_intensity),
                    FragmentType = ReadStrings(group, DiannLibraryColumns.fragment_type),
                    FragmentCharge = ReadLongs(group, DiannLibraryColumns.fragment_charge),
                    FragmentSeriesNumber = ReadLongs(group, DiannLibraryColumns.fragment_series_number),
                    FragmentLossType = ReadStrings(group, DiannLibraryColumns.fragment_loss_type),
                    ProteinIds = ReadStrings(group, DiannLibraryColumns.protein_ids),
                    Genes = ReadStrings(group, DiannLibraryColumns.genes),
                };
            }
        }

        public void Dispose()
        {
            RunSync(_reader.DisposeAsync());
            _stream.Dispose();
        }

        private static KeyValuePair<DiannLibraryColumns, string> Column(DiannLibraryColumns column, string name)
        {
            return new KeyValuePair<DiannLibraryColumns, string>(column, name);
        }

        private string[] ReadStrings(ParquetRowGroupReader group, DiannLibraryColumns column)
        {
            if (!_fields.TryGetValue(column, out var field))
                return null;
            int rowCount = checked((int)group.RowCount);
            if (field.ClrType == typeof(string) || field.ClrType == typeof(ReadOnlyMemory<char>))
            {
                var values = new string[rowCount];
                RunSync(group.ReadAsync(field, values.AsMemory()));
                for (int i = 0; i < values.Length; i++)
                    values[i] = values[i] ?? string.Empty;
                return values;
            }
            var boxed = ReadBoxed(group, field);
            var converted = new string[rowCount];
            for (int i = 0; i < rowCount; i++)
                converted[i] = Convert.ToString(boxed[i], CultureInfo.InvariantCulture) ?? string.Empty;
            return converted;
        }

        private float[] ReadFloats(ParquetRowGroupReader group, DiannLibraryColumns column)
        {
            if (!_fields.TryGetValue(column, out var field))
                return null;
            int rowCount = checked((int)group.RowCount);
            var values = new float[rowCount];
            if (field.ClrType == typeof(float) && !field.IsNullable)
            {
                RunSync(group.ReadAsync(field, values.AsMemory()));
                return values;
            }
            var boxed = ReadBoxed(group, field);
            for (int i = 0; i < rowCount; i++)
                values[i] = boxed[i] == null ? 0f : Convert.ToSingle(boxed[i], CultureInfo.InvariantCulture);
            return values;
        }

        private long[] ReadLongs(ParquetRowGroupReader group, DiannLibraryColumns column)
        {
            if (!_fields.TryGetValue(column, out var field))
                return null;
            int rowCount = checked((int)group.RowCount);
            var values = new long[rowCount];
            if (field.ClrType == typeof(long) && !field.IsNullable)
            {
                RunSync(group.ReadAsync(field, values.AsMemory()));
                return values;
            }
            var boxed = ReadBoxed(group, field);
            for (int i = 0; i < rowCount; i++)
                values[i] = boxed[i] == null ? 0L : Convert.ToInt64(boxed[i], CultureInfo.InvariantCulture);
            return values;
        }

        /// <summary>
        /// A column of a width or nullability the typed reads above do not take, boxed by value,
        /// null where the cell is null.
        /// </summary>
        private static object[] ReadBoxed(ParquetRowGroupReader group, DataField field)
        {
            switch (Type.GetTypeCode(field.ClrType))
            {
                case TypeCode.Single:
                    return ReadBoxed<float>(group, field);
                case TypeCode.Double:
                    return ReadBoxed<double>(group, field);
                case TypeCode.Int32:
                    return ReadBoxed<int>(group, field);
                case TypeCode.Int64:
                    return ReadBoxed<long>(group, field);
                case TypeCode.Int16:
                    return ReadBoxed<short>(group, field);
                case TypeCode.Byte:
                    return ReadBoxed<byte>(group, field);
                case TypeCode.UInt32:
                    return ReadBoxed<uint>(group, field);
                case TypeCode.UInt64:
                    return ReadBoxed<ulong>(group, field);
                case TypeCode.UInt16:
                    return ReadBoxed<ushort>(group, field);
                case TypeCode.SByte:
                    return ReadBoxed<sbyte>(group, field);
                case TypeCode.Boolean:
                    return ReadBoxed<bool>(group, field);
                case TypeCode.Decimal:
                    return ReadBoxed<decimal>(group, field);
                default:
                    throw new InvalidDataException(string.Format(CultureInfo.InvariantCulture,
                        @"Parquet column '{0}' has unsupported type {1}.", field.Name, field.ClrType.Name));
            }
        }

        private static object[] ReadBoxed<T>(ParquetRowGroupReader group, DataField field) where T : struct
        {
            int rowCount = checked((int)group.RowCount);
            var boxed = new object[rowCount];
            if (field.IsNullable)
            {
                var values = new T?[rowCount];
                RunSync(group.ReadAsync(field, values.AsMemory()));
                for (int i = 0; i < rowCount; i++)
                    boxed[i] = values[i];
            }
            else
            {
                var values = new T[rowCount];
                RunSync(group.ReadAsync(field, values.AsMemory()));
                for (int i = 0; i < rowCount; i++)
                    boxed[i] = values[i];
            }
            return boxed;
        }

        // GetAwaiter().GetResult() rethrows the original exception rather than an
        // AggregateException; safe because no synchronization context is captured here.
        private static T RunSync<T>(Task<T> task)
        {
            return task.GetAwaiter().GetResult();
        }

        private static void RunSync(Task task)
        {
            task.GetAwaiter().GetResult();
        }

        private static void RunSync(ValueTask task)
        {
            task.GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// The columns of one row group of a DIA-NN library, each an array of
    /// <see cref="RowCount"/> values, or null for a column not asked for or absent.
    /// </summary>
    public sealed class DiannLibraryRowGroup
    {
        public int RowCount { get; set; }
        public string[] PrecursorId { get; set; }
        public string[] ModifiedSequence { get; set; }
        public string[] StrippedSequence { get; set; }
        public long[] PrecursorCharge { get; set; }
        public float[] PrecursorMz { get; set; }
        public float[] RT { get; set; }
        public float[] IM { get; set; }
        public float[] QValue { get; set; }
        public long[] Decoy { get; set; }
        public long[] Proteotypic { get; set; }
        public long[] Flags { get; set; }
        public float[] ProductMz { get; set; }
        public float[] RelativeIntensity { get; set; }
        public string[] FragmentType { get; set; }
        public long[] FragmentCharge { get; set; }
        public long[] FragmentSeriesNumber { get; set; }
        public string[] FragmentLossType { get; set; }
        public string[] ProteinIds { get; set; }
        public string[] Genes { get; set; }
    }

    /// <summary>
    /// A DIA-NN library <c>.parquet</c> without a column its reader requires. The message is for
    /// diagnostics; a caller reports <see cref="ColumnName"/> in its own words.
    /// </summary>
    public sealed class DiannLibraryMissingColumnException : IOException
    {
        public DiannLibraryMissingColumnException(string path, string columnName)
            : base(string.Format(CultureInfo.InvariantCulture,
                @"DIA-NN library '{0}' is missing required column '{1}'.", path, columnName))
        {
            Path = path;
            ColumnName = columnName;
        }

        public string Path { get; }
        public string ColumnName { get; }
    }
}
