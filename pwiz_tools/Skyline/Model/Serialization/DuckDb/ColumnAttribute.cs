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

namespace pwiz.Skyline.Model.Serialization.DuckDb
{
    /// <summary>
    /// Attribute to mark a property as a database column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        /// <summary>
        /// The column name in the database. If null, uses the property name converted to snake_case.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The SQL type for the column (e.g., "VARCHAR", "BIGINT", "DOUBLE", "BOOLEAN").
        /// If null, inferred from the property type.
        /// </summary>
        public string SqlType { get; set; }

        /// <summary>
        /// Whether this column is required (NOT NULL). Default is false.
        /// </summary>
        public bool IsRequired { get; set; }
    }
}
