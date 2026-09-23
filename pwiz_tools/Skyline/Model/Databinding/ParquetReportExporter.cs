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
using Parquet.Data;
using Parquet.Schema;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace pwiz.Skyline.Model.Databinding
{
    public class ParquetReportExporter : IReportExporter
    {
        public void Export(Stream stream, RowItemEnumerator rowItemEnumerator)
        {
            // Build columns and schema from item properties
            var columns = BuildColumns(rowItemEnumerator.ItemProperties);
            var schema = new ParquetSchema(columns.Select(col => col.SchemaField).ToArray());

            var writer = ParquetWriter.CreateAsync(schema, stream).GetAwaiter().GetResult();
            writer.CompressionMethod = CompressionMethod.Zstd;
            using (var pipeline = new ExportPipeline(writer, columns, rowItemEnumerator))
            {
                pipeline.RowsPerGroup = DecideRowCountPerGroup(rowItemEnumerator.ItemProperties);
                pipeline.Run();
            }
            // Disposing the writer writes the footer. On a stream the export has already failed on
            // that throws again, and from a "using" that exception would replace the one which says
            // why the export failed. The writer holds nothing but the caller's stream, so a failed
            // export leaves it undisposed.
            writer.Dispose();
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

        /// <summary>
        /// Runs one export as three stages on separate threads: a reader which pulls rows from the
        /// <see cref="RowItemEnumerator"/> into chunks, the calling thread which calculates the column
        /// values of each chunk on the ParallelEx workers, and a writer which encodes and compresses each
        /// chunk into a row group. Each stage hands its output to the next through a queue holding one
        /// chunk, so a stage never gets more than one chunk ahead of the one after it.
        /// The first exception from any stage cancels the others and is rethrown by <see cref="Run"/>.
        /// </summary>
        private class ExportPipeline : IDisposable
        {
            private readonly ParquetWriter _writer;
            private readonly IList<ColumnData> _columns;
            private readonly RowItemEnumerator _rowItemEnumerator;
            private readonly BlockingCollection<List<RowItem>> _chunks = new BlockingCollection<List<RowItem>>(1);
            private readonly BlockingCollection<DataColumn[]> _rowGroups = new BlockingCollection<DataColumn[]>(1);
            private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
            private readonly CancellationToken _cancellationToken;
            private Exception _exception;

            public ExportPipeline(ParquetWriter writer, IList<ColumnData> columns, RowItemEnumerator rowItemEnumerator)
            {
                _writer = writer;
                _columns = columns;
                _rowItemEnumerator = rowItemEnumerator;
                _cancellationToken = _cancellationTokenSource.Token;
            }

            public int RowsPerGroup { get; set; } = 1000;

            public void Run()
            {
                var readThread = StartThread(@"Parquet Row Reader", ReadChunks);
                var writeThread = StartThread(@"Parquet Writer", WriteRowGroups);
                RunStage(() =>
                {
                    foreach (var chunk in _chunks.GetConsumingEnumerable(_cancellationToken))
                    {
                        var rowGroup = PopulateChunk(chunk);
                        if (_rowItemEnumerator.IsCanceled)
                        {
                            // Stops the reader and the writer without waiting for what they are doing
                            _cancellationTokenSource.Cancel();
                            break;
                        }
                        _rowGroups.Add(rowGroup, _cancellationToken);
                    }
                });
                _rowGroups.CompleteAdding();
                readThread.Join();
                writeThread.Join();
                if (_exception != null)
                {
                    ExceptionDispatchInfo.Capture(_exception).Throw();
                }
            }

            public void Dispose()
            {
                _cancellationTokenSource.Dispose();
                _chunks.Dispose();
                _rowGroups.Dispose();
            }

            private Thread StartThread(string name, Action stage)
            {
                var thread = new Thread(() =>
                {
                    LocalizationHelper.InitThread();
                    RunStage(stage);
                })
                {
                    Name = name,
                    IsBackground = true
                };
                thread.Start();
                return thread;
            }

            private void RunStage(Action stage)
            {
                try
                {
                    stage();
                }
                catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
                {
                    // Another stage failed, or the export was canceled
                }
                catch (Exception exception)
                {
                    // The first exception wins. Cancelling unblocks the other stages, whose
                    // OperationCanceledExceptions are then ignored above
                    Interlocked.CompareExchange(ref _exception, exception, null);
                    _cancellationTokenSource.Cancel();
                }
            }

            private void ReadChunks()
            {
                try
                {
                    while (true)
                    {
                        var chunk = new List<RowItem>();
                        while (chunk.Count < RowsPerGroup && _rowItemEnumerator.MoveNext())
                        {
                            chunk.Add(_rowItemEnumerator.Current);
                        }

                        if (chunk.Count == 0)
                        {
                            return;
                        }
                        _chunks.Add(chunk, _cancellationToken);
                    }
                }
                finally
                {
                    _chunks.CompleteAdding();
                }
            }

            private void WriteRowGroups()
            {
                foreach (var rowGroup in _rowGroups.GetConsumingEnumerable(_cancellationToken))
                {
                    using var groupWriter = _writer.CreateRowGroup();
                    foreach (var dataColumn in rowGroup)
                    {
                        groupWriter.WriteColumnAsync(dataColumn).GetAwaiter().GetResult();
                    }
                }
            }

            /// <summary>
            /// Calculates every column's value for every row in the chunk and returns the columns
            /// ready to be written as one row group.
            /// </summary>
            private DataColumn[] PopulateChunk(IList<RowItem> rowItems)
            {
                var chunkArrays = _columns.Select(col => col.CreateArray(rowItems.Count)).ToArray();
                // Values with no Parquet storage type get stored as strings by calling ToString(),
                // which formats using the thread's culture, so the values have to be converted under
                // the culture this report is being exported with. All of the columns come from the
                // same DataSchema, so the culture only needs to be set once per row.
                var dataSchemaLocalizer = _columns.FirstOrDefault()?.PropertyDescriptor.DataSchemaLocalizer
                                          ?? DataSchemaLocalizer.INVARIANT;
                // Consecutive rows which share the same Value object (for instance the rows expanded from one
                // transition by a "Results" sublist) form a run. Columns which depend only on the Value have
                // the same value for every row in the run, so they are calculated once per run.
                var runStarts = new List<int>();
                for (int rowIndex = 0; rowIndex < rowItems.Count; rowIndex++)
                {
                    if (rowIndex == 0 || !ReferenceEquals(rowItems[rowIndex].Value, rowItems[rowIndex - 1].Value))
                    {
                        runStarts.Add(rowIndex);
                    }
                }
                ParallelEx.For(0, runStarts.Count, runIndex =>
                {
                    if (_rowItemEnumerator.IsCanceled)
                    {
                        return;
                    }
                    int startRow = runStarts[runIndex];
                    int endRow = runIndex + 1 < runStarts.Count ? runStarts[runIndex + 1] : rowItems.Count;
                    dataSchemaLocalizer.CallWithCultureInfo(() =>
                    {
                        for (int colIndex = 0; colIndex < _columns.Count; colIndex++)
                        {
                            var column = _columns[colIndex];
                            var values = chunkArrays[colIndex];
                            if (column.DependsOnlyOnRowValue)
                            {
                                var value = column.GetStorageValue(rowItems[startRow]);
                                if (value != null)
                                {
                                    for (int rowIndex = startRow; rowIndex < endRow; rowIndex++)
                                    {
                                        values.SetValue(value, rowIndex);
                                    }
                                }
                            }
                            else
                            {
                                for (int rowIndex = startRow; rowIndex < endRow; rowIndex++)
                                {
                                    column.StoreValue(rowItems[rowIndex], rowIndex, values);
                                }
                            }
                        }
                    });
                    for (int rowIndex = startRow; rowIndex < endRow; rowIndex++)
                    {
                        rowItems[rowIndex] = null;
                    }
                }, threadName: nameof(PopulateChunk));
                // Constructing a DataColumn packs the nulls out of a nullable array, and a list column
                // has to be flattened first, so the columns are built in parallel rather than one after
                // another on this thread while the workers wait for the next chunk
                var dataColumns = new DataColumn[_columns.Count];
                ParallelEx.For(0, _columns.Count, colIndex =>
                {
                    dataColumns[colIndex] = _columns[colIndex].CreateDataColumn(chunkArrays[colIndex]);
                }, threadName: nameof(ColumnData.CreateDataColumn));
                return dataColumns;
            }
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
                DependsOnlyOnRowValue = (propertyDescriptor as ColumnPropertyDescriptor)?.DependsOnlyOnRowValue ?? false;
                var valueType = PropertyDescriptor.DataSchema.GetWrappedValueType(PropertyDescriptor.PropertyType);

                // Check if this is a ListColumnValue<T>
                ListElementType = GetListColumnValueStorageType(valueType);
                if (ListElementType != null)
                {
                    // This is a list column
                    // The element storage type is nullable so individual list slots can hold nulls
                    StorageType = new StorageType(typeof(IEnumerable<>).MakeGenericType(ListElementType.Type));
                    var elementField = new DataField(@"element", ListElementType.Type,
                        isNullable: true, isArray: false);
                    SchemaField = new ListField(Name, elementField);
                    DataField = elementField;
                }
                else
                {
                    StorageType = new StorageType(DecideStorageType(valueType));
                    DataField = new DataField(Name, StorageType.Type);
                    SchemaField = DataField;
                }
            }

            public string Name { get; }
            public DataPropertyDescriptor PropertyDescriptor { get; }
            /// <summary>
            /// True if rows which share the same <see cref="RowItem.Value"/> have the same value in this column.
            /// Only known for ColumnPropertyDescriptor; any other descriptor is assumed to vary per row.
            /// </summary>
            public bool DependsOnlyOnRowValue { get; }
            /// <summary>
            /// The type of the values in the array that holds a chunk of this column.
            /// For list columns this is an IEnumerable of <see cref="ListElementType"/>.
            /// </summary>
            public StorageType StorageType { get; }
            /// <summary>
            /// For list columns, the storage type of each list element. Otherwise null.
            /// </summary>
            public StorageType ListElementType { get; }
            public Field SchemaField { get; }
            public DataField DataField { get; }

            public object GetValue(RowItem rowItem)
            {
                var dataSchema = PropertyDescriptor.DataSchema;
                return dataSchema.UnwrapValue(PropertyDescriptor.GetValue(rowItem));
            }

            public Array CreateArray(int rowCount)
            {
                return Array.CreateInstance(StorageType.Type, rowCount);
            }

            public DataColumn CreateDataColumn(Array chunkArray)
            {
                if (ListElementType == null)
                {
                    // Simple column - no flattening needed
                    return new DataColumn(DataField, chunkArray);
                }

                // List column - need to flatten data and create repetition levels
                var allElements = new List<object>();
                var repetitionLevels = new List<int>();

                for (int rowIndex = 0; rowIndex < chunkArray.Length; rowIndex++)
                {
                    var listValue = chunkArray.GetValue(rowIndex) as Array;
                    if (listValue == null || listValue.Length == 0)
                    {
                        // Empty or null list - still need to represent this row
                        allElements.Add(null);
                        repetitionLevels.Add(0);
                    }
                    else
                    {
                        for (int i = 0; i < listValue.Length; i++)
                        {
                            allElements.Add(listValue.GetValue(i));
                            repetitionLevels.Add(i == 0 ? 0 : 1); // 0 = start of new list, 1 = continuation
                        }
                    }
                }

                // Create flattened array of the element storage type
                var flattenedArray = Array.CreateInstance(ListElementType.Type, allElements.Count);
                for (int i = 0; i < allElements.Count; i++)
                {
                    flattenedArray.SetValue(allElements[i], i);
                }

                return new DataColumn(DataField, flattenedArray, repetitionLevels.ToArray());
            }

            public void StoreValue(RowItem rowItem, int rowIndex, Array values)
            {
                var value = GetStorageValue(rowItem);
                if (value != null)
                {
                    values.SetValue(value, rowIndex);
                }
            }

            /// <summary>
            /// Returns the column's value for the row, converted to something that can be stored
            /// in the array from <see cref="CreateArray"/>, or null.
            /// </summary>
            public object GetStorageValue(RowItem rowItem)
            {
                var value = GetValue(rowItem);
                if (value == null)
                {
                    return null;
                }

                if (ListElementType != null)
                {
                    // Extract the list from ListColumnValue<T>
                    return ConvertListColumnValue(value);
                }
                return StorageType.ConvertValue(value);
            }

            private Array ConvertListColumnValue(object listColumnValue)
            {
                // Get the underlying list as an array
                Array array = (listColumnValue as IListColumnValue)?.ToArray();
                if (array == null)
                {
                    return null;
                }

                if (array.GetType().GetElementType() == ListElementType.Type)
                {
                    return array;
                }

                var convertedArray = Array.CreateInstance(ListElementType.Type, array.Length);
                for (int i = 0; i < array.Length; i++)
                {
                    var value = ListElementType.ConvertValue(array.GetValue(i));
                    if (value != null)
                    {
                        convertedArray.SetValue(value, i);
                    }
                }

                return convertedArray;
            }
        }
        /// <summary>
        /// If the type is a ListColumnValue, returns the storage type of the list elements.
        /// Otherwise returns null.
        /// </summary>
        private static StorageType GetListColumnValueStorageType(Type type)
        {
            var elementType = ListColumnValue.GetElementType(type);
            if (elementType == null)
            {
                return null;
            }

            return new StorageType(DecideStorageType(elementType));
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
            // the value goes through unchanged.
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

        /// <summary>
        /// The .NET type that a column's values are held in before being written to Parquet.
        /// The nullable underlying type is computed once in the constructor so that converting
        /// each value does not have to call <see cref="Nullable.GetUnderlyingType"/>, which is slow.
        /// </summary>
        public class StorageType
        {
            public StorageType(Type type)
            {
                Type = type;
                NullableUnderlyingType = Nullable.GetUnderlyingType(type);
            }

            public Type Type { get; }
            /// <summary>
            /// If <see cref="Type"/> is a <see cref="Nullable{T}"/>, then T. Otherwise null.
            /// </summary>
            public Type NullableUnderlyingType { get; }

            /// <summary>
            /// Converts a value to something which can be stored in an array whose element type is <see cref="Type"/>.
            /// Note that a boxed <see cref="Nullable{T}"/> with a value is a boxed T, so for a nullable
            /// storage type the returned value is a boxed T.
            /// </summary>
            public object ConvertValue(object value)
            {
                if (value == null)
                {
                    return null;
                }

                var valueType = value.GetType();
                if (valueType == Type)
                {
                    return value;
                }

                if (NullableUnderlyingType != null)
                {
                    if (valueType == NullableUnderlyingType)
                    {
                        return value;
                    }
                    return Convert.ChangeType(value, NullableUnderlyingType);
                }
                if (Type == typeof(string))
                {
                    return value.ToString();
                }
                return Convert.ChangeType(value, Type);
            }
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
