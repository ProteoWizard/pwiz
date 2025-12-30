/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Linq;
using System.Reflection;
using System.Text;
using DuckDB.NET.Data;
using DuckDB.NET.Native;

namespace pwiz.Skyline.Model.Serialization.DuckDb
{
    /// <summary>
    /// Tracks which columns have non-null values and can build dynamic schema.
    /// Uses reflection to discover columns from properties marked with ColumnAttribute.
    /// Works with classes derived from Record, using Id as the primary key.
    /// </summary>
    internal class TableSchema
    {
        public string TableName { get; }
        public Type ItemType { get; }
        private readonly List<ColumnDef> _allColumns;
        private readonly HashSet<string> _usedColumns;

        /// <summary>
        /// Creates a new TableSchema by discovering columns from properties with ColumnAttribute.
        /// </summary>
        /// <typeparam name="T">The record type, must derive from Record.</typeparam>
        /// <param name="tableName">The name of the database table.</param>
        public static TableSchema Create<T>(string tableName) where T : Record
        {
            return new TableSchema(tableName, typeof(T));
        }

        /// <summary>
        /// Creates a new TableSchema by discovering columns from properties with ColumnAttribute.
        /// The type must derive from Record, and Id will be used as the primary key.
        /// </summary>
        public TableSchema(string tableName, Type itemType)
        {
            if (!typeof(Record).IsAssignableFrom(itemType))
                throw new ArgumentException($"Type {itemType.Name} must derive from Record", nameof(itemType));

            TableName = tableName;
            ItemType = itemType;
            _allColumns = DiscoverColumnsFromType(itemType);
            _usedColumns = new HashSet<string>();

            // Id (primary key) and required columns are always used
            _usedColumns.Add(nameof(Record.Id));
            foreach (var col in _allColumns.Where(c => c.IsRequired))
                _usedColumns.Add(col.Name);
        }

        /// <summary>
        /// Discovers column definitions from properties with ColumnAttribute.
        /// </summary>
        private static List<ColumnDef> DiscoverColumnsFromType(Type type)
        {
            var columns = new List<ColumnDef>();

            // Add Id column first (from Record base class)
            var idProp = typeof(Record).GetProperty(nameof(Record.Id));
            columns.Add(new ColumnDef(nameof(Record.Id), "BIGINT PRIMARY KEY", idProp, true));

            // Add columns from properties with ColumnAttribute
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Skip Id since we already added it
                if (prop.Name == nameof(Record.Id))
                    continue;

                var attr = prop.GetCustomAttribute<ColumnAttribute>();
                if (attr == null)
                    continue;

                var name = attr.Name ?? prop.Name;
                var sqlType = attr.SqlType ?? InferSqlType(prop.PropertyType);
                columns.Add(new ColumnDef(name, sqlType, prop, attr.IsRequired));
            }

            return columns;
        }

        /// <summary>
        /// Infers SQL type from a C# property type.
        /// </summary>
        private static string InferSqlType(Type type)
        {
            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

            if (underlyingType == typeof(long))
                return "BIGINT";
            if (underlyingType == typeof(int))
                return "INTEGER";
            if (underlyingType == typeof(double))
                return "DOUBLE";
            if (underlyingType == typeof(bool))
                return "BOOLEAN";
            if (underlyingType == typeof(string))
                return "VARCHAR";

            // Default to VARCHAR for unknown types
            return "VARCHAR";
        }

        /// <summary>
        /// Scans an item to discover which columns have non-null values.
        /// </summary>
        public void DiscoverColumns(Record item)
        {
            foreach (var col in _allColumns)
            {
                if (_usedColumns.Contains(col.Name))
                    continue;

                var value = col.GetValue(item);
                if (value != null)
                    _usedColumns.Add(col.Name);
            }
        }

        /// <summary>
        /// Gets only the columns that have data.
        /// </summary>
        public List<ColumnDef> GetUsedColumns()
        {
            return _allColumns.Where(c => _usedColumns.Contains(c.Name)).ToList();
        }

        /// <summary>
        /// Builds the CREATE TABLE SQL statement with only used columns.
        /// </summary>
        public string BuildCreateTableSql()
        {
            var usedColumns = GetUsedColumns();
            var sb = new StringBuilder();
            sb.AppendLine($@"CREATE TABLE {TableName} (");

            for (int i = 0; i < usedColumns.Count; i++)
            {
                var col = usedColumns[i];
                // Id column already has PRIMARY KEY in its SqlType, don't add NOT NULL
                var notNull = col.IsRequired && col.Name != nameof(Record.Id) ? " NOT NULL" : "";
                var comma = i < usedColumns.Count - 1 ? "," : "";
                // Quote column name to handle reserved words
                sb.AppendLine($@"    ""{col.Name}"" {col.SqlType}{notNull}{comma}");
            }

            sb.AppendLine(")");
            return sb.ToString();
        }

        /// <summary>
        /// Appends a row using only the used columns.
        /// </summary>
        public void AppendRow(IDuckDBAppenderRow row, Record item)
        {
            foreach (var col in GetUsedColumns())
            {
                var value = col.GetValue(item);
                AppendValue(row, value);
            }
            row.EndRow();
        }

        private static void AppendValue(IDuckDBAppenderRow row, object value)
        {
            if (value == null)
            {
                row.AppendNullValue();
                return;
            }

            switch (value)
            {
                case string s:
                    row.AppendValue(s);
                    break;
                case long l:
                    row.AppendValue(l);
                    break;
                case int i:
                    row.AppendValue(i);
                    break;
                case double d:
                    row.AppendValue(d);
                    break;
                case bool b:
                    row.AppendValue(b);
                    break;
                default:
                    row.AppendValue(value.ToString());
                    break;
            }
        }
    }
}
