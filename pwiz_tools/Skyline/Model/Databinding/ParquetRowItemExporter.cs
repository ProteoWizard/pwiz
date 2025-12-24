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
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

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

            foreach (var column in columns)
            {
                var field = CreateDataField(column);
                fields.Add(field);
                column.DataColumn = new DataColumn(field, column.Values.ToArray(), null);
            }

            return new Schema(fields);
        }

        private DataField CreateDataField(ColumnData columnData)
        {
            var propertyType = columnData.PropertyDescriptor.PropertyType;
            var columnName = columnData.PropertyDescriptor.Name;

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

        private class ColumnData
        {
            public PropertyDescriptor PropertyDescriptor { get; set; }
            public List<object> Values { get; set; }
            public DataColumn DataColumn { get; set; }
        }
    }
}
