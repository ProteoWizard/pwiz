/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using Parquet;
using Parquet.Schema;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace pwiz.Skyline.Model.Databinding
{
    public class ParquetReportExporter : IReportExporter
    {
        public void Export(Stream stream, RowItemEnumerator rowItemEnumerator)
        {
            // Build columns and schema from item properties
            var columns = BuildColumns(rowItemEnumerator.ItemProperties);
            var schema = new ParquetSchema(columns.Select(col => col.SchemaField).ToArray());

            var options = new ParquetOptions
            {
                CompressionMethod = CompressionMethod.Zstd,
                // Parquet.Net's default is SmallestSize, which is Zstd level 19 and many times slower
                // than the level 3 that Optimal maps to, for a few percent smaller file
                CompressionLevel = CompressionLevel.Optimal,
                // Parquet.Net 6 only dictionary-encodes a column when asked to, and decides by scanning the
                // column's values; a sample of this many rows rejects a mostly unique column without the full scan
                DictionaryEncodingSampleSize = 10000
            };
            // The strings in a report are mostly repeated (protein, peptide, replicate and file names),
            // which Parquet.Net 4 dictionary-encoded without being asked. List columns are left plain
            // because some readers cannot decode dictionary-encoded lists.
            foreach (var column in columns.Where(col => col.ListElementType == null && col.StorageType == typeof(string)))
            {
                options.ColumnEncodingHints[column.DataField.Path.ToString()] = EncodingHint.Dictionary;
            }
            // Parquet.Net 6 does not use ConfigureAwait(false) when it writes the file's header and footer,
            // so blocking on it from a thread with a WinForms SynchronizationContext would deadlock
            var writer = ActionUtil.CallWithoutSynchronizationContext(() =>
                ParquetWriter.CreateAsync(schema, stream, options).GetAwaiter().GetResult());
            using var writeWorker = new QueueWorker<Array[]>(
                consume: (chunkArrays, threadIndex) =>
                {
                    using var groupWriter = writer.CreateRowGroup();
                    // A row group's columns have to be written in schema order, one after another
                    for (int i = 0; i < columns.Count; i++)
                    {
                        columns[i].WriteColumnAsync(groupWriter, chunkArrays[i]).GetAwaiter().GetResult();
                    }
                });
            // Single writer thread, queue at most 1 chunk ahead
            writeWorker.RunAsync(1, @"Parquet Writer", maxQueueSize: 1);
            int rowsPerGroup = DecideRowCountPerGroup(rowItemEnumerator.ItemProperties);
            // Process in chunks
            while (true)
            {
                if (rowItemEnumerator.IsCanceled || writeWorker.Exception != null)
                {
                    break;
                }

                var chunk = new List<RowItem>();
                while (chunk.Count < rowsPerGroup && rowItemEnumerator.MoveNext())
                {
                    chunk.Add(rowItemEnumerator.Current);
                }

                if (chunk.Count == 0)
                {
                    break;
                }

                // Create arrays for this chunk
                var chunkArrays = columns.Select(col => col.CreateArray(chunk.Count)).ToArray();

                // Populate chunk data
                PopulateChunk(rowItemEnumerator.ProgressMonitor, chunk, columns, chunkArrays);
                if (rowItemEnumerator.IsCanceled)
                {
                    break;
                }

                writeWorker.Add(chunkArrays);
            }

            writeWorker.DoneAdding(wait: true);
            if (writeWorker.Exception != null)
            {
                throw writeWorker.Exception;
            }
            // Disposing the writer writes the file's footer, so it is not disposed when the export fails:
            // that would throw again and hide the exception which says why. The writer holds nothing but
            // the caller's stream.
            ActionUtil.CallWithoutSynchronizationContext(() => writer.DisposeAsync().GetAwaiter().GetResult());
        }

        private List<ColumnData> BuildColumns(ItemProperties itemProperties)
        {
            var columns = new List<ColumnData>();
            var usedColumnNames = new HashSet<string>();

            foreach (DataPropertyDescriptor property in itemProperties)
            {
                var name = GetUniqueColumnName(property, usedColumnNames);
                columns.Add(new ColumnData(name, property));
            }

            return columns;
        }

        private void PopulateChunk(IProgressMonitor progressMonitor,
            IList<RowItem> rowItems, List<ColumnData> columns, Array[] chunkArrays)
        {
            // Values with no Parquet storage type get stored as strings by calling ToString(),
            // which formats using the thread's culture, so the values have to be converted under
            // the culture this report is being exported with. All of the columns come from the
            // same DataSchema, so the culture only needs to be set once per row.
            var dataSchemaLocalizer = columns.FirstOrDefault()?.PropertyDescriptor.DataSchemaLocalizer
                                      ?? DataSchemaLocalizer.INVARIANT;
            ParallelEx.For(0, rowItems.Count, rowIndex =>
            {
                if (progressMonitor.IsCanceled)
                {
                    return;
                }
                var rowItem = rowItems[rowIndex];
                rowItems[rowIndex] = null;
                dataSchemaLocalizer.CallWithCultureInfo(() =>
                {
                    for (int colIndex = 0; colIndex < columns.Count; colIndex++)
                    {
                        columns[colIndex].StoreValue(rowItem, rowIndex, chunkArrays[colIndex]);
                    }
                });
            }, threadName:nameof(PopulateChunk));
        }

        public static IEnumerable<string> MakeValidColumnNames(IEnumerable<string> columnNames)
        {
            var usedColumnNames = new HashSet<string>();
            foreach (var name in columnNames)
            {
                yield return MakeUniqueColumnName(name, usedColumnNames);
            }
        }

        private string GetUniqueColumnName(PropertyDescriptor propertyDescriptor, HashSet<string> usedColumnNames)
        {
            // Get the display name from DisplayNameAttribute, or fall back to property name
            string baseName = propertyDescriptor.DisplayName;
            if (string.IsNullOrEmpty(baseName) || baseName == propertyDescriptor.Name)
            {
                baseName = propertyDescriptor.Name;
            }

            return MakeUniqueColumnName(baseName, usedColumnNames);
        }

        private static string MakeUniqueColumnName(string name, HashSet<string> usedColumnNames)
        {
            // Sanitize the column name - replace illegal characters with underscores
            // Parquet column names should be valid identifiers (alphanumeric and underscore)
            var sanitized = SanitizeColumnName(name);

            // Ensure uniqueness by appending a number if needed
            string uniqueName = Helpers.GetUniqueName(sanitized, usedColumnNames);
            usedColumnNames.Add(uniqueName);
            return uniqueName;
        }

        private static string SanitizeColumnName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return @"Column";
            }

            var sanitized = new System.Text.StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                // Allow alphanumeric characters and underscores
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    sanitized.Append(c);
                }
                else
                {
                    // Replace illegal characters with underscores
                    sanitized.Append('_');
                }
            }

            // Ensure the name doesn't start with a digit
            string result = sanitized.ToString();
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result = @"_" + result;
            }

            return result;
        }

        private class ColumnData
        {
            public ColumnData(string name, DataPropertyDescriptor propertyDescriptor)
            {
                Name = name;
                PropertyDescriptor = propertyDescriptor;
                var valueType = PropertyDescriptor.DataSchema.GetWrappedValueType(PropertyDescriptor.PropertyType);
                IsUtcTimestamp = PropertyDescriptor.Attributes[typeof(UtcTimestampAttribute)] != null;

                // Check if this is a ListColumnValue<T>
                ListElementType = GetListColumnValueStorageType(valueType);
                if (ListElementType != null)
                {
                    // This is a list column
                    // Use nullable storage type so the array can hold nulls for absent lists
                    ElementStorageType = DecideStorageType(ListElementType);
                    StorageType = typeof(IEnumerable<>).MakeGenericType(ElementStorageType);
                    // Element is nullable so individual list slots can hold nulls
                    var elementField = MakeDataField(@"element", ElementStorageType,
                        isNullable: true, isArray: false);
                    SchemaField = new ListField(Name, elementField);
                    DataField = elementField;
                }
                else
                {
                    StorageType = DecideStorageType(valueType);
                    DataField = MakeDataField(Name, StorageType);
                    SchemaField = DataField;
                }
            }

            public string Name { get; }
            public DataPropertyDescriptor PropertyDescriptor { get; }
            public Type StorageType { get; }
            public Type ListElementType { get; }
            public Type ElementStorageType { get; }
            public Field SchemaField { get; }
            public DataField DataField { get; }
            public bool IsUtcTimestamp { get; }

            public object GetValue(RowItem rowItem)
            {
                var dataSchema = PropertyDescriptor.DataSchema;
                return dataSchema.UnwrapValue(PropertyDescriptor.GetValue(rowItem));
            }

            public Array CreateArray(int rowCount)
            {
                return Array.CreateInstance(StorageType, rowCount);
            }

            /// <summary>
            /// Writes the chunk's values, from the array made by <see cref="CreateArray"/>, as the next
            /// column of the row group.
            /// </summary>
            public Task WriteColumnAsync(ParquetRowGroupWriter groupWriter, Array chunkArray)
            {
                if (ListElementType != null)
                {
                    // Parquet.Net 6 stores a string as ReadOnlyMemory<char>
                    var elementType = ElementStorageType == typeof(string)
                        ? typeof(ReadOnlyMemory<char>)
                        : Nullable.GetUnderlyingType(ElementStorageType);
                    return (Task) WRITE_LIST_COLUMN_METHOD.MakeGenericMethod(elementType)
                        .Invoke(null, BindingFlags.DoNotWrapExceptions, null, new object[] { groupWriter, DataField, chunkArray }, null);
                }
                if (StorageType == typeof(string))
                {
                    return groupWriter.WriteAsync(DataField, (string[]) chunkArray);
                }
                return (Task) WRITE_NULLABLE_COLUMN_METHOD.MakeGenericMethod(Nullable.GetUnderlyingType(StorageType))
                    .Invoke(null, BindingFlags.DoNotWrapExceptions, null, new object[] { groupWriter, DataField, chunkArray }, null);
            }

            public void StoreValue(RowItem rowItem, int rowIndex, Array values)
            {
                var value = GetValue(rowItem);
                if (value == null)
                {
                    return;
                }

                if (ListElementType != null)
                {
                    // Extract the list from ListColumnValue<T>
                    value = ConvertListColumnValue(value);
                }
                else
                {
                    value = ConvertToColumnValue(value, StorageType);
                }
                values.SetValue(value, rowIndex);
            }

            private DataField MakeDataField(string name, Type storageType, bool? isNullable = null, bool? isArray = null)
            {
                if (storageType != typeof(DateTime?))
                {
                    return new DataField(name, storageType, isNullable, isArray);
                }
                // Without a format, Parquet.Net writes DateTime as the deprecated INT96. The
                // TIMESTAMP logical type says whether the value is a moment in time (adjusted
                // to UTC) or a wall-clock reading with no time zone. The values are stored to the
                // millisecond because Parquet.Net 6 converts a local DateTime to UTC when it
                // writes Micros or Nanos, which would shift a wall-clock time, and writes the
                // digits of a Millis value as they are.
                return new DateTimeDataField(name, DateTimeFormat.Timestamp, IsUtcTimestamp,
                    DateTimeTimeUnit.Millis, isNullable ?? true, isArray);
            }

            private object ConvertToColumnValue(object value, Type type)
            {
                value = ConvertToStorageType(value, type);
                // Parquet.Net writes the DateTime digits without looking at DateTime.Kind,
                // so a local time has to be converted here. Other values are written as-is,
                // which keeps the output the same on every computer.
                if (IsUtcTimestamp && value is DateTime dateTime && dateTime.Kind == DateTimeKind.Local)
                {
                    return dateTime.ToUniversalTime();
                }
                return value;
            }

            private Array ConvertListColumnValue(object listColumnValue)
            {
                // Get the underlying list as an array
                Array array = (listColumnValue as IListColumnValue)?.ToArray();
                if (array == null)
                {
                    return null;
                }

                if (array.GetType().GetElementType() == ListElementType && !IsUtcTimestamp)
                {
                    return array;
                }

                var convertedArray = Array.CreateInstance(ListElementType, array.Length);
                for (int i = 0; i < array.Length; i++)
                {
                    var value = ConvertToColumnValue(array.GetValue(i), ListElementType);
                    if (value != null)
                    {
                        convertedArray.SetValue(value, i);
                    }
                }

                return convertedArray;
            }
        }
        private static readonly MethodInfo WRITE_NULLABLE_COLUMN_METHOD =
            typeof(ParquetReportExporter).GetMethod(nameof(WriteNullableColumnAsync), BindingFlags.NonPublic | BindingFlags.Static);

        private static Task WriteNullableColumnAsync<T>(ParquetRowGroupWriter groupWriter, DataField field, Array values) where T : struct
        {
            return groupWriter.WriteAsync<T>(field, new ReadOnlyMemory<T?>((T?[]) values));
        }

        private static readonly MethodInfo WRITE_LIST_COLUMN_METHOD =
            typeof(ParquetReportExporter).GetMethod(nameof(WriteListColumnAsync), BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// Flattens the lists of a chunk into the elements which are not null, with the repetition levels
        /// which say where each list starts and the definition levels which say whether a row's list is
        /// null, empty, or has a null element, and writes them as the next column of the row group.
        /// </summary>
        private static Task WriteListColumnAsync<T>(ParquetRowGroupWriter groupWriter, DataField elementField, Array lists) where T : struct
        {
            var values = new List<T>();
            var definitionLevels = new List<int>();
            var repetitionLevels = new List<int>();
            // The element's definition level counts the levels of nesting which are present: the
            // list, the list's repeated group (so at least one element), and the element's value
            int valueLevel = elementField.MaxDefinitionLevel;
            foreach (Array list in lists)
            {
                if (list == null || list.Length == 0)
                {
                    repetitionLevels.Add(0);
                    definitionLevels.Add(list == null ? valueLevel - 3 : valueLevel - 2);
                    continue;
                }
                for (int i = 0; i < list.Length; i++)
                {
                    // 0 starts a new list, 1 continues the previous element's list
                    repetitionLevels.Add(i == 0 ? 0 : 1);
                    var element = list.GetValue(i);
                    if (element == null)
                    {
                        definitionLevels.Add(valueLevel - 1);
                    }
                    else
                    {
                        definitionLevels.Add(valueLevel);
                        values.Add(element is string s ? (T) (object) s.AsMemory() : (T) element);
                    }
                }
            }
            return groupWriter.WriteAllPartsAsync<T>(elementField, values.ToArray(),
                definitionLevels.ToArray(), repetitionLevels.ToArray(), CancellationToken.None);
        }

        private static Type GetListColumnValueStorageType(Type type)
        {
            var elementType = ListColumnValue.GetElementType(type);
            if (elementType == null)
            {
                return null;
            }

            return DecideStorageType(elementType);
        }

        private static Dictionary<Type, Type> _storageTypes = new Dictionary<Type, Type>
        {
            { typeof(int), typeof(int?) },
            { typeof(long), typeof(long?) },
            { typeof(double), typeof(double?) },
            { typeof(float), typeof(float?) },
            { typeof(bool), typeof(bool?) },
            { typeof(decimal), typeof(decimal?) },
            // Old call sites wrapped DateTime as DateTimeOffset with a local-Kind
            // assumption that wasn't actually valid; store DateTime? directly so
            // the value goes through unchanged. See ColumnData.MakeDataField for
            // how it is encoded.
            { typeof(DateTime), typeof(DateTime?) }
        };

        static ParquetReportExporter()
        {
            foreach (var value in _storageTypes.Values.ToArray())
            {
                _storageTypes[value] = value;
            }
        }

        public static Type DecideStorageType(Type type)
        {
            if (_storageTypes.TryGetValue(type, out var storageType))
            {
                return storageType;
            }
            return typeof(string);
        }

        public static object ConvertToStorageType(object value, Type type)
        {
            if (value == null)
            {
                return null;
            }

            if (value.GetType() == type)
            {
                return value;
            }

            var nullableUnderlyingType = Nullable.GetUnderlyingType(type);
            if (nullableUnderlyingType != null)
            {
                value = ConvertToStorageType(value, nullableUnderlyingType);
                return value == null ? null : Activator.CreateInstance(type, value);
            }
            if (type == typeof(string))
            {
                return value.ToString();
            }
            return Convert.ChangeType(value, type);
        }

        /// <summary>
        /// Returns the number of rows that should be in each row group based on the
        /// set of columns that are going to be written.
        /// 
        /// </summary>
        public static int DecideRowCountPerGroup(ItemProperties itemProperties)
        {
            var targetGroupSize = 1 << 28; // Try to have row groups that are 256MB on disk
            int rowCount = targetGroupSize / EstimateRowByteCount(itemProperties);
            return Math.Max(rowCount, 1000);
        }

        /// <summary>
        /// Returns a loose estimate of the number of bytes each row will take up.
        /// </summary>
        public static int EstimateRowByteCount(IEnumerable<DataPropertyDescriptor> propertyDescriptors)
        {
            int columnCount = 0;
            int maxSublistDepth = 0;
            int leafColumnByteCount = 0;
            foreach (var propertyDescriptor in propertyDescriptors)
            {
                columnCount++;
                int sublistDepth = 0;
                if (propertyDescriptor is ColumnPropertyDescriptor columnPropertyDescriptor)
                {
                    var propertyPath = columnPropertyDescriptor.PropertyPath;
                    for (; false == propertyPath?.IsRoot; propertyPath = propertyPath.Parent)
                    {
                        if (propertyPath.IsUnboundLookup)
                        {
                            sublistDepth++;
                        }
                    }
                }

                int columnSize = EstimateColumnSize(propertyDescriptor);
                // Only include the column byte counts for the columns from the deepest depth of sublist.
                // Other columns are assumed to have a lot of duplication
                if (sublistDepth > maxSublistDepth)
                {
                    maxSublistDepth = sublistDepth;
                    leafColumnByteCount = columnSize;
                }
                else if (sublistDepth == maxSublistDepth)
                {
                    leafColumnByteCount += columnSize;
                }
            }

            return 1 + columnCount + leafColumnByteCount;
        }

        public static int EstimateColumnSize(DataPropertyDescriptor propertyDescriptor)
        {
            var columnType = propertyDescriptor.DataSchema.GetWrappedValueType(propertyDescriptor.PropertyType);
            if (columnType.IsPrimitive)
            {
                return Marshal.SizeOf(columnType);
            }
            if (GetListColumnValueStorageType(columnType) != null)
            {
                return 64;
            }

            return 8;
        }
    }
}
