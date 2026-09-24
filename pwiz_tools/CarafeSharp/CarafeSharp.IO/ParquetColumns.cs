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
            using (var reader = RunSync(ParquetReader.CreateAsync(stream)))
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
                            parts[name].Add(RunSync(group.ReadColumnAsync(fields[name])).Data);
                    }
                }
                foreach (string name in columnNames)
                    columns[name] = Concatenate(parts[name], fields[name]);
            }
            return new ParquetColumns(columns, metadata);
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
    }
}
