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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace pwiz.Skyline.Model.Databinding
{
    public class ParquetReportExporter : IReportExporter
    {
        public void Export(Stream stream, RowItemEnumerator rowItemEnumerator)
        {
            // Build columns and schema from item properties
            var columnValueTree = new ColumnValueTree();
            var columns = BuildColumns(rowItemEnumerator.ItemProperties, columnValueTree);
            var schema = new ParquetSchema(columns.Select(col => col.SchemaField).ToArray());

            using var writer = ParquetWriter.CreateAsync(schema, stream).GetAwaiter().GetResult();
            writer.CompressionMethod = CompressionMethod.Zstd;
            using var writeWorker = new QueueWorker<DataColumn[]>(
                consume: (dataColumns, threadIndex) =>
                {
                    using var groupWriter = writer.CreateRowGroup();
                    foreach (var dataColumn in dataColumns)
                    {
                        groupWriter.WriteColumnAsync(dataColumn).GetAwaiter().GetResult();
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
                PopulateChunk(rowItemEnumerator.ProgressMonitor, chunk, columns, columnValueTree, chunkArrays);
                if (rowItemEnumerator.IsCanceled)
                {
                    break;
                }

                // Create DataColumns and queue for writing
                var dataColumns = new DataColumn[columns.Count];
                for (int i = 0; i < columns.Count; i++)
                {
                    dataColumns[i] = columns[i].CreateDataColumn(chunkArrays[i]);
                }
                writeWorker.Add(dataColumns);
            }

            writeWorker.DoneAdding(wait: true);
            if (writeWorker.Exception != null)
            {
                throw writeWorker.Exception;
            }
        }

        private List<ColumnData> BuildColumns(ItemProperties itemProperties, ColumnValueTree columnValueTree)
        {
            var columns = new List<ColumnData>();
            var usedColumnNames = new HashSet<string>();

            foreach (DataPropertyDescriptor property in itemProperties)
            {
                var name = GetUniqueColumnName(property, usedColumnNames);
                columns.Add(new ColumnData(name, property, columnValueTree));
            }

            return columns;
        }

        private void PopulateChunk(IProgressMonitor progressMonitor,
            IList<RowItem> rowItems, List<ColumnData> columns, ColumnValueTree columnValueTree, Array[] chunkArrays)
        {
            // Values with no Parquet storage type get stored as strings by calling ToString(),
            // which formats using the thread's culture, so the values have to be converted under
            // the culture this report is being exported with. All of the columns come from the
            // same DataSchema, so the culture only needs to be set once per row.
            var dataSchemaLocalizer = columns.FirstOrDefault()?.PropertyDescriptor.DataSchemaLocalizer
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
                if (progressMonitor.IsCanceled)
                {
                    return;
                }
                int startRow = runStarts[runIndex];
                int endRow = runIndex + 1 < runStarts.Count ? runStarts[runIndex + 1] : rowItems.Count;
                dataSchemaLocalizer.CallWithCultureInfo(() =>
                {
                    var nodeValues = new object[columnValueTree.NodeCount];
                    columnValueTree.Evaluate(rowItems[startRow], nodeValues, true);
                    for (int colIndex = 0; colIndex < columns.Count; colIndex++)
                    {
                        var column = columns[colIndex];
                        if (!column.DependsOnlyOnRowValue)
                        {
                            continue;
                        }
                        var value = column.ConvertToStorageValue(nodeValues[column.NodeIndex]);
                        if (value != null)
                        {
                            for (int rowIndex = startRow; rowIndex < endRow; rowIndex++)
                            {
                                chunkArrays[colIndex].SetValue(value, rowIndex);
                            }
                        }
                    }
                    for (int rowIndex = startRow; rowIndex < endRow; rowIndex++)
                    {
                        var rowItem = rowItems[rowIndex];
                        columnValueTree.Evaluate(rowItem, nodeValues, false);
                        for (int colIndex = 0; colIndex < columns.Count; colIndex++)
                        {
                            var column = columns[colIndex];
                            if (column.DependsOnlyOnRowValue)
                            {
                                continue;
                            }
                            var value = column.NodeIndex >= 0
                                ? column.ConvertToStorageValue(nodeValues[column.NodeIndex])
                                : column.GetStorageValue(rowItem);
                            if (value != null)
                            {
                                chunkArrays[colIndex].SetValue(value, rowIndex);
                            }
                        }
                    }
                });
                for (int rowIndex = startRow; rowIndex < endRow; rowIndex++)
                {
                    rowItems[rowIndex] = null;
                }
            }, threadName:nameof(PopulateChunk));
        }

        /// <summary>
        /// The distinct ColumnDescriptors which the report's columns and their ancestors consist of, ordered so
        /// that every parent precedes its children. Evaluating the nodes in that order calculates an ancestor
        /// shared by several columns (such as "Results!*.Value" for every result column) once per row instead
        /// of once per column.
        /// </summary>
        private class ColumnValueTree
        {
            private readonly List<ColumnDescriptor> _nodes = new List<ColumnDescriptor>();
            private readonly List<int> _parentIndexes = new List<int>();
            private readonly List<bool> _dependsOnlyOnRowValue = new List<bool>();
            private readonly Dictionary<ColumnDescriptor, int> _nodeIndexes = new Dictionary<ColumnDescriptor, int>();

            public int NodeCount
            {
                get { return _nodes.Count; }
            }

            /// <summary>
            /// Adds the column and any of its ancestors which are not already present,
            /// and returns the index of the column's node.
            /// </summary>
            public int AddNode(ColumnDescriptor columnDescriptor)
            {
                if (_nodeIndexes.TryGetValue(columnDescriptor, out int nodeIndex))
                {
                    return nodeIndex;
                }
                int parentIndex = columnDescriptor.Parent == null ? -1 : AddNode(columnDescriptor.Parent);
                nodeIndex = _nodes.Count;
                _nodes.Add(columnDescriptor);
                _parentIndexes.Add(parentIndex);
                _dependsOnlyOnRowValue.Add(columnDescriptor.DependsOnlyOnRowValue);
                _nodeIndexes.Add(columnDescriptor, nodeIndex);
                return nodeIndex;
            }

            /// <summary>
            /// Calculates the values of the nodes which depend only on the row's Value, or of the other nodes,
            /// into <paramref name="nodeValues"/>. The values of the nodes which depend only on the Value have
            /// to be there already when the other nodes are calculated.
            /// </summary>
            public void Evaluate(RowItem rowItem, object[] nodeValues, bool dependsOnlyOnRowValue)
            {
                for (int nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex++)
                {
                    if (_dependsOnlyOnRowValue[nodeIndex] != dependsOnlyOnRowValue)
                    {
                        continue;
                    }
                    int parentIndex = _parentIndexes[nodeIndex];
                    var parentValue = parentIndex < 0 ? null : nodeValues[parentIndex];
                    nodeValues[nodeIndex] = _nodes[nodeIndex].GetValueFromParent(parentValue, rowItem, null);
                }
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
            public ColumnData(string name, DataPropertyDescriptor propertyDescriptor, ColumnValueTree columnValueTree)
            {
                Name = name;
                PropertyDescriptor = propertyDescriptor;
                NodeIndex = -1;
                if (propertyDescriptor is ColumnPropertyDescriptor columnPropertyDescriptor &&
                    columnPropertyDescriptor.PivotKey == null &&
                    columnPropertyDescriptor.DisplayColumn.ColumnDescriptor != null)
                {
                    NodeIndex = columnValueTree.AddNode(columnPropertyDescriptor.DisplayColumn.ColumnDescriptor);
                    DependsOnlyOnRowValue = columnPropertyDescriptor.DependsOnlyOnRowValue;
                }
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
            /// Index of this column's node in the ColumnValueTree, or -1 if the column is not a plain
            /// ColumnPropertyDescriptor and has to be evaluated on its own with <see cref="GetStorageValue"/>.
            /// </summary>
            public int NodeIndex { get; }
            /// <summary>
            /// True if rows which share the same <see cref="RowItem.Value"/> have the same value in this column.
            /// Only known for columns in the ColumnValueTree; any other column is assumed to vary per row.
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

            /// <summary>
            /// Returns the column's value for the row, converted to something that can be stored
            /// in the array from <see cref="CreateArray"/>, or null.
            /// </summary>
            public object GetStorageValue(RowItem rowItem)
            {
                return ConvertToStorageValue(PropertyDescriptor.GetValue(rowItem));
            }

            /// <summary>
            /// Converts a value which the column's property descriptor returned to something
            /// that can be stored in the array from <see cref="CreateArray"/>, or null.
            /// </summary>
            public object ConvertToStorageValue(object value)
            {
                value = PropertyDescriptor.DataSchema.UnwrapValue(value);
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
