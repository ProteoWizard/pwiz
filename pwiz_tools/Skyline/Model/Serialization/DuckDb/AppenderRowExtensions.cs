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

using DuckDB.NET.Data;

namespace pwiz.Skyline.Model.Serialization.DuckDb
{
    /// <summary>
    /// Extension methods for DuckDB Appender to handle nullable values.
    /// </summary>
    public static class AppenderRowExtensions
    {
        public static IDuckDBAppenderRow AppendNullableValue(this IDuckDBAppenderRow row, string value)
        {
            if (value == null)
                return row.AppendNullValue();
            return row.AppendValue(value);
        }

        public static IDuckDBAppenderRow AppendNullableValue(this IDuckDBAppenderRow row, double? value)
        {
            if (value == null)
                return row.AppendNullValue();
            return row.AppendValue(value.Value);
        }

        public static IDuckDBAppenderRow AppendNullableValue(this IDuckDBAppenderRow row, int? value)
        {
            if (value == null)
                return row.AppendNullValue();
            return row.AppendValue(value.Value);
        }

        public static IDuckDBAppenderRow AppendNullableValue(this IDuckDBAppenderRow row, long? value)
        {
            if (value == null)
                return row.AppendNullValue();
            return row.AppendValue(value.Value);
        }

        public static IDuckDBAppenderRow AppendNullableValue(this IDuckDBAppenderRow row, bool? value)
        {
            if (value == null)
                return row.AppendNullValue();
            return row.AppendValue(value.Value);
        }
    }
}
