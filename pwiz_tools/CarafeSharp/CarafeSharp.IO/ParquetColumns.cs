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
using System.Reflection;
using System.Threading.Tasks;
using Parquet;
using Parquet.Schema;

namespace pwiz.CarafeSharp.IO
{
    /// <summary>
    /// Whole-column reads from a parquet file, concatenated across row groups. Parquet.Net's
    /// API is task-based; CarafeSharp does not use async/await, so every call is waited on
    /// synchronously, as Osprey's ParquetScoreCache does.
    /// </summary>
    public sealed class ParquetColumns
    {
        /// <summary>
        /// Reads the named columns. A requested column that the file lacks is an error.
        /// </summary>
        public static ParquetColumns Read(string path, params string[] columnNames)
        {
            var columns = new Dictionary<string, Array>(StringComparer.Ordinal);
            IReadOnlyDictionary<string, string> metadata;
            using (var stream = File.OpenRead(path))
            {
                var reader = RunSync(ParquetReader.CreateAsync(stream));
                try
                {
                    metadata = new Dictionary<string, string>(reader.CustomMetadata);
                    var fields = reader.Schema.GetDataFields().ToDictionary(f => f.Name, StringComparer.Ordinal);
                    foreach (string name in columnNames)
                    {
                        if (!fields.ContainsKey(name))
                            throw new InvalidDataException(string.Format(@"{0} has no column {1}.", path, name));
                    }
                    var parts = columnNames.ToDictionary(n => n, n => new List<Array>(), StringComparer.Ordinal);
                    for (int g = 0; g < reader.RowGroupCount; g++)
                    {
                        using (var group = reader.OpenRowGroupReader(g))
                        {
                            foreach (string name in columnNames)
                                parts[name].Add(ReadColumn(group, fields[name]));
                        }
                    }
                    foreach (string name in columnNames)
                        columns[name] = Concatenate(parts[name], fields[name]);
                }
                finally
                {
                    RunSync(reader.DisposeAsync());
                }
            }
            return new ParquetColumns(columns, metadata);
        }

        /// <summary>The file's key-value metadata alone, from its footer.</summary>
        public static IReadOnlyDictionary<string, string> ReadMetadata(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                var reader = RunSync(ParquetReader.CreateAsync(stream));
                try
                {
                    return new Dictionary<string, string>(reader.CustomMetadata);
                }
                finally
                {
                    RunSync(reader.DisposeAsync());
                }
            }
        }

        /// <summary>
        /// Writes whole columns as a parquet file, <paramref name="rowsPerGroup"/> rows to a row
        /// group, for tests that build their input in code. A column's type is its array's
        /// element type; a reference or <see cref="Nullable{T}"/> element type is nullable.
        /// </summary>
        internal static void Write(string path, IReadOnlyList<KeyValuePair<string, Array>> columns,
            IReadOnlyDictionary<string, string> metadata, int rowsPerGroup)
        {
            var elementTypes = columns.Select(c => c.Value.GetType().GetElementType() ?? typeof(object)).ToArray();
            var fields = columns.Select((c, i) => new DataField(c.Key, elementTypes[i])).ToArray();
            int rowCount = columns.Count == 0 ? 0 : columns[0].Value.Length;
            using (var stream = File.Create(path))
            {
                var writer = RunSync(ParquetWriter.CreateAsync(new ParquetSchema(fields), stream));
                writer.CustomMetadata = metadata.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                for (int start = 0; start < rowCount; start += Math.Max(1, rowsPerGroup))
                {
                    int count = Math.Min(Math.Max(1, rowsPerGroup), rowCount - start);
                    using (var group = writer.CreateRowGroup())
                    {
                        for (int i = 0; i < fields.Length; i++)
                        {
                            var slice = Array.CreateInstance(elementTypes[i], count);
                            Array.Copy(columns[i].Value, start, slice, 0, count);
                            WriteColumn(group, fields[i], slice);
                        }
                    }
                }
                // Writes the footer
                RunSync(writer.DisposeAsync());
            }
        }

        /// <summary>
        /// One row group's column as an array: string[], byte[][], T[] for a required column, or
        /// T?[] for a nullable one, as Parquet.Net 4's DataColumn.Data was.
        /// </summary>
        private static Array ReadColumn(ParquetRowGroupReader group, DataField field)
        {
            int rowCount = checked((int)group.RowCount);
            // Parquet.Net 6 describes a string column as ReadOnlyMemory<char>; read it as strings.
            if (field.ClrType == typeof(string) || field.ClrType == typeof(ReadOnlyMemory<char>))
            {
                var strings = new string[rowCount];
                RunSync(group.ReadAsync(field, strings.AsMemory()));
                return strings;
            }
            // and a binary column as ReadOnlyMemory<byte>; read it as byte arrays.
            if (field.ClrType == typeof(byte[]) || field.ClrType == typeof(ReadOnlyMemory<byte>))
            {
                var bytes = new byte[rowCount][];
                RunSync(group.ReadAsync(field, bytes.AsMemory()));
                return bytes;
            }
            var method = field.IsNullable ? READ_NULLABLE : READ_REQUIRED;
            return (Array)method.MakeGenericMethod(field.ClrType).Invoke(null, new object[] { group, field, rowCount });
        }

        private static readonly MethodInfo READ_REQUIRED = typeof(ParquetColumns).GetMethod(nameof(ReadRequired), BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo READ_NULLABLE = typeof(ParquetColumns).GetMethod(nameof(ReadNullable), BindingFlags.NonPublic | BindingFlags.Static);

        private static T[] ReadRequired<T>(ParquetRowGroupReader group, DataField field, int rowCount) where T : struct
        {
            var values = new T[rowCount];
            RunSync(group.ReadAsync(field, values.AsMemory()));
            return values;
        }

        private static T?[] ReadNullable<T>(ParquetRowGroupReader group, DataField field, int rowCount) where T : struct
        {
            var values = new T?[rowCount];
            RunSync(group.ReadAsync(field, values.AsMemory()));
            return values;
        }

        private static void WriteColumn(ParquetRowGroupWriter group, DataField field, Array values)
        {
            if (values is string[] strings)
            {
                RunSync(group.WriteAsync(field, strings));
                return;
            }
            if (values is byte[][] bytes)
            {
                RunSync(group.WriteAsync(field, bytes));
                return;
            }
            var elementType = values.GetType().GetElementType() ?? typeof(object);
            var underlying = Nullable.GetUnderlyingType(elementType);
            var method = underlying != null ? WRITE_NULLABLE.MakeGenericMethod(underlying) : WRITE_REQUIRED.MakeGenericMethod(elementType);
            method.Invoke(null, new object[] { group, field, values });
        }

        private static readonly MethodInfo WRITE_REQUIRED = typeof(ParquetColumns).GetMethod(nameof(WriteRequired), BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo WRITE_NULLABLE = typeof(ParquetColumns).GetMethod(nameof(WriteNullable), BindingFlags.NonPublic | BindingFlags.Static);

        private static void WriteRequired<T>(ParquetRowGroupWriter group, DataField field, T[] values) where T : struct
        {
            RunSync(group.WriteAsync(field, new ReadOnlyMemory<T>(values)));
        }

        private static void WriteNullable<T>(ParquetRowGroupWriter group, DataField field, T?[] values) where T : struct
        {
            RunSync(group.WriteAsync(field, new ReadOnlyMemory<T?>(values)));
        }

        private readonly Dictionary<string, Array> _columns;

        private ParquetColumns(Dictionary<string, Array> columns, IReadOnlyDictionary<string, string> metadata)
        {
            _columns = columns;
            Metadata = metadata;
        }

        /// <summary>The file's key-value metadata.</summary>
        public IReadOnlyDictionary<string, string> Metadata { get; }

        public int RowCount
        {
            get { return _columns.Count == 0 ? 0 : _columns.Values.First().Length; }
        }

        /// <summary>
        /// The column as <typeparamref name="T"/>[]. Nullable columns without nulls may be read
        /// as their non-nullable type.
        /// </summary>
        public T[] Get<T>(string name)
        {
            if (!_columns.TryGetValue(name, out var column))
                throw new ArgumentException(string.Format(@"Column {0} was not read.", name), nameof(name));
            if (column is T[] typed)
                return typed;
            var converted = new T[column.Length];
            for (int i = 0; i < column.Length; i++)
            {
                object value = column.GetValue(i);
                if (value == null)
                    throw new InvalidDataException(string.Format(@"Column {0} has a null at row {1}.", name, i));
                converted[i] = (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
            }
            return converted;
        }

        private static Array Concatenate(List<Array> parts, DataField field)
        {
            if (parts.Count == 1)
                return parts[0];
            int total = parts.Sum(p => p.Length);
            var elementType = parts.Count > 0 ? parts[0].GetType().GetElementType() : field.ClrType;
            var result = Array.CreateInstance(elementType ?? typeof(object), total);
            int offset = 0;
            foreach (var part in parts)
            {
                Array.Copy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }
            return result;
        }

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
}
