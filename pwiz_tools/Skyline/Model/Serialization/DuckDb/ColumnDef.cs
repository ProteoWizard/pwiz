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
using System.Reflection;
using DuckDB.NET.Native;

namespace pwiz.Skyline.Model.Serialization.DuckDb
{
    /// <summary>
    /// Represents a column definition discovered from a property with ColumnAttribute.
    /// </summary>
    public class ColumnDef
    {
        public string Name { get; }
        public DuckDBType DuckDbType { get; }
        public PropertyInfo Property { get; }
        public Type PropertyType { get; }
        public object DefaultValue { get; }

        public ColumnDef(string name, DuckDBType duckDbType, PropertyInfo property)
        {
            Name = name;
            DuckDbType = duckDbType;
            Property = property;
            PropertyType = property.PropertyType;
            if (PropertyType.IsValueType)
            {
                DefaultValue = Activator.CreateInstance(PropertyType);
            }
            else
            {
                DefaultValue = null;
            }
        }

        /// <summary>
        /// Gets the value from an object using reflection.
        /// </summary>
        public object GetValue(object item)
        {
            return Property.GetValue(item);
        }

        /// <summary>
        /// Returns true if the value equals default(PropertyType).
        /// For reference types and nullable types, default is null.
        /// For value types, default is 0/false/etc.
        /// </summary>
        public bool IsDefaultValue(object value)
        {
            return value == null || Equals(value, DefaultValue);
        }
    }
}
