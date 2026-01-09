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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using pwiz.Common.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Databinding
{
    public class ParquetRowItemExporter : IRowItemExporter
    {
        private const int ROWS_PER_GROUP = 100_000;

        public void Export(IProgressMonitor progressMonitor, ref IProgressStatus status, Stream stream,
            RowItemEnumerator rowItemEnumerator, ColumnFormats columnFormats)
        {
            // Build columns and schema from item properties
            var columns = BuildColumns(rowItemEnumerator.ItemProperties);
            var schema = new Schema(columns.Select(col => col.DataField).ToArray());

            using (var writer = new ParquetWriter(schema, stream))
            {
                long totalRows = rowItemEnumerator.Count;
                long rowsProcessed = 0;

                // Process in chunks
                while (rowsProcessed < totalRows)
                {
                    if (progressMonitor.IsCanceled)
                    {
                        return;
                    }

                    int rowsInChunk = (int) Math.Min(ROWS_PER_GROUP, totalRows - rowsProcessed);

                    // Resize arrays for this chunk
                    foreach (var column in columns)
                    {
                        column.SetRowCount(rowsInChunk);
                    }
                    progressMonitor.UpdateProgress(status = status.ChangeMessage(string.Format(Resources.AbstractViewContext_WriteData_Writing_row__0___1_,
                            rowsProcessed, totalRows))
                        .ChangePercentComplete((int)(rowsProcessed * 100 / totalRows)));

                    // Populate chunk data
                    PopulateChunk(progressMonitor, ref status, rowItemEnumerator, columns, rowsInChunk, rowsProcessed,
                        totalRows);
                    if (progressMonitor.IsCanceled)
                    {
                        return;
                    }

                    // Write chunk
                    using (var groupWriter = writer.CreateRowGroup())
                    {
                        foreach (var column in columns)
                        {
                            if (progressMonitor.IsCanceled)
                            {
                                return;
                            }
                            groupWriter.WriteColumn(column.GetDataColumn());
                        }
                    }

                    rowsProcessed += rowsInChunk;
                }
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

        private void PopulateChunk(IProgressMonitor progressMonitor, ref IProgressStatus status,
            RowItemEnumerator rowItemEnumerator, List<ColumnData> columns, int rowsInChunk,
            long rowsProcessed, long totalRows)
        {
            var rowItems = new RowItem[rowsInChunk];
            for (int i = 0; i < rowsInChunk; i++)
            {
                if (progressMonitor.IsCanceled)
                {
                    return;
                }
                if (!rowItemEnumerator.MoveNext())
                {
                    throw new InvalidOperationException(string.Format(@"Unable to get row {0}", i));
                }

                rowItems[i] = rowItemEnumerator.Current;
            }

            // Update progress
            long overallRowIndex = rowsProcessed + rowItems.Length;
            int percentComplete = (int)(overallRowIndex * 100 / totalRows);
            if (percentComplete > status.PercentComplete)
            {
                status = status.ChangeMessage(string.Format(Resources.AbstractViewContext_WriteData_Writing_row__0___1_,
                        overallRowIndex, totalRows))
                    .ChangePercentComplete(percentComplete);
                progressMonitor.UpdateProgress(status);
            }

            ParallelEx.For(0, rowItems.Length, rowIndex =>
            {
                if (progressMonitor.IsCanceled)
                {
                    return;
                }
                var rowItem = rowItems[rowIndex];
                rowItems[rowIndex] = null;
                foreach (var column in columns)
                {
                    column.StoreValue(rowItem, rowIndex);
                }
            });
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
                StorageType = DecideStorageType(valueType);
                DataField = new DataField(Name, StorageType);
            }

            public string Name { get; }
            public DataPropertyDescriptor PropertyDescriptor { get; }
            public Type StorageType { get; }
            public DataField DataField { get; }

            public object GetValue(RowItem rowItem)
            {
                var dataSchema = PropertyDescriptor.DataSchema;
                return dataSchema.UnwrapValue(PropertyDescriptor.GetValue(rowItem));
            }

            public void SetRowCount(int rowCount)
            {
                Values = Array.CreateInstance(StorageType, rowCount);
            }

            public void StoreValue(RowItem rowItem, int rowIndex)
            {
                var value = GetValue(rowItem);
                if (value == null)
                {
                    return;
                }

                if (StorageType == typeof(string))
                {
                    value = value as string ?? value.ToString();
                }
                else if (StorageType == typeof(DateTimeOffset?) && value is DateTime dateTime)
                {
                    // Parquet library expects DateTimeOffset for timestamps
                    value = new DateTimeOffset(dateTime);
                }
                Values.SetValue(value, rowIndex);
            }

            public Array Values { get; private set; }

            public DataColumn GetDataColumn()
            {
                return new DataColumn(DataField, Values);
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
        public static Type DecideStorageType(Type type)
        {
            if (_storageTypes.TryGetValue(type, out var storageType))
            {
                return storageType;
            }
            return typeof(string);
        }
    }
}
