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
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Databinding
{
    public class ParquetRowItemExporter : IRowItemExporter
    {
        public void Export(IProgressMonitor progressMonitor, ref IProgressStatus status, Stream stream,
            RowItemEnumerator rowItemEnumerator)
        {
            var columns = BuildColumns(rowItemEnumerator);
            var schema = BuildSchema(columns);

            using (var writer = new ParquetWriter(schema, stream))
            {
                using (var groupWriter = writer.CreateRowGroup())
                {
                    foreach (var column in columns)
                    {
                        groupWriter.WriteColumn(column.DataColumn);
                    }
                }
            }
        }

        private List<ColumnData> BuildColumns(RowItemEnumerator rowItemEnumerator)
        {
            var columns = new List<ColumnData>();
            var itemProperties = rowItemEnumerator.ItemProperties;
            var usedColumnNames = new HashSet<string>();
            foreach (DataPropertyDescriptor property in itemProperties)
            {
                var name = GetUniqueColumnName(property, usedColumnNames);
                columns.Add(new ColumnData(name, property, rowItemEnumerator.Count));
            }

            int rowIndex = 0;
            while (rowItemEnumerator.MoveNext())
            {
                var currentItem = rowItemEnumerator.Current!;
                foreach (var column in columns)
                {
                    column.StoreValue(currentItem, rowIndex);
                }

                rowIndex++;
            }

            // Create DataFields and DataColumns now that all values are populated
            foreach (var column in columns)
            {
                var field = new DataField(column.Name, column.StorageType);
                column.DataColumn = new DataColumn(field, column.Values, null);
            }

            return columns;
        }

        private Schema BuildSchema(List<ColumnData> columns)
        {
            var fields = columns.Select(c => c.DataColumn.Field).ToArray();
            return new Schema(fields);
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
                return "Column";
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
                result = "_" + result;
            }

            // Handle case where sanitization resulted in an empty string
            if (string.IsNullOrEmpty(result))
            {
                result = "Column";
            }

            return result;
        }

        private class ColumnData
        {
            public ColumnData(string name, DataPropertyDescriptor propertyDescriptor, int rowCount)
            {
                Name = name;
                PropertyDescriptor = propertyDescriptor;
                var valueType = PropertyDescriptor.DataSchema.GetWrappedValueType(PropertyDescriptor.PropertyType);
                StorageType = DecideStorageType(valueType);
                Values = Array.CreateInstance(StorageType, rowCount);
            }
            public DataPropertyDescriptor PropertyDescriptor { get; }
            public string Name { get; }

            public Type StorageType
            {
                get;
            }

            public object GetValue(RowItem rowItem)
            {
                var dataSchema = PropertyDescriptor.DataSchema;
                return dataSchema.UnwrapValue(PropertyDescriptor.GetValue(rowItem));
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
                Values.SetValue(value, rowIndex);
            }
            
            public Array Values { get; }
            public DataColumn DataColumn { get; set; }
        }

        private static HashSet<Type> _primitiveTypes = new HashSet<Type>
        {
            typeof(int), typeof(long), typeof(double), typeof(float), typeof(bool), typeof(DateTime),
            typeof(decimal)
        };
        public static Type DecideStorageType(Type type)
        {
            if (_primitiveTypes.Contains(type))
            {
                return typeof(Nullable<>).MakeGenericType(new[] { type });
            }

            return typeof(string);
        }
    }
}
