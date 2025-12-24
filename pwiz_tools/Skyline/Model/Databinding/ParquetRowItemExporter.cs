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

            foreach (PropertyDescriptor property in itemProperties)
            {
                columns.Add(new ColumnData
                {
                    PropertyDescriptor = property,
                    Values = new List<object>()
                });
            }

            while (rowItemEnumerator.MoveNext())
            {
                var currentItem = rowItemEnumerator.Current;
                foreach (var column in columns)
                {
                    var value = column.PropertyDescriptor.GetValue(currentItem);
                    column.Values.Add(value);
                }
            }

            return columns;
        }

        private Schema BuildSchema(List<ColumnData> columns)
        {
            var fields = new List<DataField>();
            var usedColumnNames = new HashSet<string>();

            foreach (var column in columns)
            {
                var field = CreateDataField(column, usedColumnNames);
                fields.Add(field);
                var typedArray = ConvertToTypedArray(column);
                column.DataColumn = new DataColumn(field, typedArray, null);
            }

            return new Schema(fields.ToArray());
        }

        private DataField CreateDataField(ColumnData columnData, HashSet<string> usedColumnNames)
        {
            var propertyType = columnData.PropertyDescriptor.PropertyType;
            var columnName = GetUniqueColumnName(columnData.PropertyDescriptor, usedColumnNames);

            if (propertyType == typeof(string))
            {
                return new DataField<string>(columnName);
            }
            if (propertyType == typeof(int) || propertyType == typeof(int?))
            {
                return new DataField<int?>(columnName);
            }
            if (propertyType == typeof(long) || propertyType == typeof(long?))
            {
                return new DataField<long?>(columnName);
            }
            if (propertyType == typeof(double) || propertyType == typeof(double?))
            {
                return new DataField<double?>(columnName);
            }
            if (propertyType == typeof(float) || propertyType == typeof(float?))
            {
                return new DataField<float?>(columnName);
            }
            if (propertyType == typeof(bool) || propertyType == typeof(bool?))
            {
                return new DataField<bool?>(columnName);
            }
            if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
            {
                return new DataField<DateTime?>(columnName);
            }
            if (propertyType == typeof(decimal) || propertyType == typeof(decimal?))
            {
                return new DataField<decimal?>(columnName);
            }

            // Default to string representation for unknown types
            return new DataField<string>(columnName);
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

        private Array ConvertToTypedArray(ColumnData columnData)
        {
            var propertyType = columnData.PropertyDescriptor.PropertyType;
            var values = columnData.Values;

            if (propertyType == typeof(string))
            {
                return values.Select(v => v == null ? null : v.ToString()).ToArray();
            }
            if (propertyType == typeof(int) || propertyType == typeof(int?))
            {
                return values.Select(v => v == null ? (int?)null : Convert.ToInt32(v)).ToArray();
            }
            if (propertyType == typeof(long) || propertyType == typeof(long?))
            {
                return values.Select(v => v == null ? (long?)null : Convert.ToInt64(v)).ToArray();
            }
            if (propertyType == typeof(double) || propertyType == typeof(double?))
            {
                return values.Select(v => v == null ? (double?)null : Convert.ToDouble(v)).ToArray();
            }
            if (propertyType == typeof(float) || propertyType == typeof(float?))
            {
                return values.Select(v => v == null ? (float?)null : Convert.ToSingle(v)).ToArray();
            }
            if (propertyType == typeof(bool) || propertyType == typeof(bool?))
            {
                return values.Select(v => v == null ? (bool?)null : Convert.ToBoolean(v)).ToArray();
            }
            if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
            {
                return values.Select(v => v == null ? (DateTime?)null : Convert.ToDateTime(v)).ToArray();
            }
            if (propertyType == typeof(decimal) || propertyType == typeof(decimal?))
            {
                return values.Select(v => v == null ? (decimal?)null : Convert.ToDecimal(v)).ToArray();
            }

            // Default to string representation for unknown types
            return values.Select(v => v == null ? null : v.ToString()).ToArray();
        }

        private class ColumnData
        {
            public PropertyDescriptor PropertyDescriptor { get; set; }
            public List<object> Values { get; set; }
            public DataColumn DataColumn { get; set; }
        }
    }
}
