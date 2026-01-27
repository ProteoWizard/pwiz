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
            var columns = BuildColumns(rowItemEnumerator.ItemProperties);
            var schema = new Schema(columns.Select(col => col.SchemaField).ToArray());

            using var writer = new ParquetWriter(schema, stream);
            using var writeWorker = new QueueWorker<DataColumn[]>(
                consume: (dataColumns, threadIndex) =>
                {
                    using var groupWriter = writer.CreateRowGroup();
                    foreach (var dataColumn in dataColumns)
                    {
                        groupWriter.WriteColumn(dataColumn);
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
            ParallelEx.For(0, rowItems.Count, rowIndex =>
            {
                if (progressMonitor.IsCanceled)
                {
                    return;
                }
                var rowItem = rowItems[rowIndex];
                rowItems[rowIndex] = null;
                for (int colIndex = 0; colIndex < columns.Count; colIndex++)
                {
                    columns[colIndex].StoreValue(rowItem, rowIndex, chunkArrays[colIndex]);
                }
            }, threadName:nameof(PopulateChunk));
        }

        private string GetUniqueColumnName(PropertyDescriptor propertyDescriptor, HashSet<string> usedColumnNames)
        {
            // Get the display name from DisplayNameAttribute, or fall back to property name
            string baseName = propertyDescriptor.DisplayName;
            if (string.IsNullOrEmpty(baseName) || baseName == propertyDescriptor.Name)
            {
                baseName = propertyDescriptor.Name;
            }

            // Sanitize the column name - replace illegal characters with underscores
            // Parquet column names should be valid identifiers (alphanumeric and underscore)
            var sanitized = SanitizeColumnName(baseName);

            // Ensure uniqueness by appending a number if needed
            string uniqueName = Helpers.GetUniqueName(sanitized, usedColumnNames);
            usedColumnNames.Add(uniqueName);
            return uniqueName;
        }

        private string SanitizeColumnName(string name)
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

                // Check if this is a FormattableList<T>
                ListElementType = GetFormattableListElementType(valueType);
                if (ListElementType != null)
                {
                    // This is a list column
                    // Use nullable storage type so the array can hold nulls for absent lists
                    ElementStorageType = DecideStorageType(ListElementType);
                    StorageType = typeof(IEnumerable<>).MakeGenericType(ElementStorageType);
                    // Create DataField with explicit hasNulls:true to represent null lists
                    var elementField = new DataField(@"element", GetParquetDataType(ListElementType),
                        hasNulls: true, isArray: false);
                    SchemaField = new ListField(Name, elementField);
                    DataField = elementField;
                }
                else
                {
                    StorageType = DecideStorageType(valueType);
                    DataField = new DataField(Name, StorageType);
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

            public object GetValue(RowItem rowItem)
            {
                var dataSchema = PropertyDescriptor.DataSchema;
                return dataSchema.UnwrapValue(PropertyDescriptor.GetValue(rowItem));
            }

            public Array CreateArray(int rowCount)
            {
                return Array.CreateInstance(StorageType, rowCount);
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
                var flattenedArray = Array.CreateInstance(ElementStorageType, allElements.Count);
                for (int i = 0; i < allElements.Count; i++)
                {
                    flattenedArray.SetValue(allElements[i], i);
                }

                return new DataColumn(DataField, flattenedArray, repetitionLevels.ToArray());
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
                    // Extract the list from FormattableList<T>
                    value = ConvertListValue(value);
                }
                else 
                {
                    value = ConvertToStorageType(value, StorageType);
                }
                values.SetValue(value, rowIndex);
            }

            private Array ConvertListValue(object formattableList)
            {
                // Get the underlying list via ToImmutableList() method
                var toArrayMethod = formattableList.GetType().GetMethod(nameof(FormattableList<object>.ToArray));
                if (toArrayMethod == null)
                {
                    return null;
                }
                Array array = (Array)toArrayMethod.Invoke(formattableList, null);
                if (array == null)
                {
                    return null;
                }

                if (array.GetType().GetElementType() == ListElementType)
                {
                    return array;
                }

                var convertedArray = Array.CreateInstance(ListElementType, array.Length);
                for (int i = 0; i < array.Length; i++)
                {
                    var value = ConvertToStorageType(array.GetValue(i), ListElementType);
                    if (value != null)
                    {
                        convertedArray.SetValue(value, i);
                    }
                }

                return convertedArray;
            }

            private static Type GetFormattableListElementType(Type type)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(FormattableList<>))
                {
                    return DecideStorageType(type.GetGenericArguments()[0]);
                }
                return null;
            }
        }

        private static Dictionary<Type, Type> _storageTypes = new Dictionary<Type, Type>
        {
            { typeof(int), typeof(int?) },
            { typeof(long), typeof(long?) },
            { typeof(double), typeof(double?) },
            { typeof(float), typeof(float?) },
            { typeof(bool), typeof(bool?) },
            { typeof(decimal), typeof(decimal?) },
            { typeof(DateTime), typeof(DateTimeOffset?) }
        };

        static ParquetReportExporter()
        {
            foreach (var value in _storageTypes.Values.ToArray())
            {
                _storageTypes[value] = value;
            }
        }

        private static Dictionary<Type, DataType> _parquetDataTypes = new Dictionary<Type, DataType>
        {
            { typeof(int), DataType.Int32 },
            { typeof(long), DataType.Int64 },
            { typeof(double), DataType.Double },
            { typeof(float), DataType.Float },
            { typeof(bool), DataType.Boolean },
            { typeof(decimal), DataType.Decimal },
            { typeof(DateTime), DataType.DateTimeOffset },
            { typeof(string), DataType.String }
        };

        public static Type DecideStorageType(Type type)
        {
            if (_storageTypes.TryGetValue(type, out var storageType))
            {
                return storageType;
            }
            return typeof(string);
        }

        public static DataType GetParquetDataType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = type.GetGenericArguments()[0];
            }
            if (_parquetDataTypes.TryGetValue(type, out var dataType))
            {
                return dataType;
            }
            return DataType.String;
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
            if (type == typeof(DateTimeOffset) && value is DateTime dateTime)
            {
                return new DateTimeOffset(dateTime);
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

        public static int DecideRowCountPerGroup(ItemProperties itemProperties)
        {
            var targetGroupSize = 1 << 28; // Try to have row groups that are 256GB on disk
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
            if (columnType.IsGenericType && columnType.GetGenericTypeDefinition() == typeof(FormattableList<>))
            {
                return 64;
            }

            return 8;
        }
    }
}
