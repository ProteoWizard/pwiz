/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Holds a set of values for when a DataGridView has expanded a One-To-Many relationship.
    /// </summary>
    public class RowKey
    {
        public RowKey(RowKey parent, IdentifierPath identifierPath, object value)
        {
            Parent = parent;
            IdentifierPath = identifierPath;
            Value = value;
        }

        public RowKey Parent { get; private set; }
        public IdentifierPath IdentifierPath { get; private set; }
        public object Value { get; private set; }

        public object FindValue(IdentifierPath key)
        {
            if (IdentifierPath.Equals(key))
            {
                return Value;
            }
            if (Parent != null && key.Length < IdentifierPath.Length)
            {
                return Parent.FindValue(key);
            }
            return null;
        }
        
        #region object overrides

        public bool Equals(RowKey other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return Equals(Parent, other.Parent) && Equals(IdentifierPath, other.IdentifierPath) && Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (RowKey)) return false;
            return Equals((RowKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = 0;
                if (Parent != null)
                {
                    result = result*397 ^ Parent.GetHashCode();
                }
                result = result*397 ^ IdentifierPath.GetHashCode();
                result = result*397 ^ (ReferenceEquals(Value, null) ? 0 : Value.GetHashCode());
                return result;
            }
        }
        #endregion

        public static Comparison<RowKey> GetComparison(DataSchema dataSchema, IEnumerable<IdentifierPath> keys)
        {
            var sortedKeys = keys.ToArray();
            Array.Sort(sortedKeys);
            return (rowKey1, rowKey2) =>
                       {
                           foreach (var key in sortedKeys)
                           {
                               var result = dataSchema.Compare(rowKey1.FindValue(key), rowKey2.FindValue(key));
                               if (result != 0)
                               {
                                   return result;
                               }
                           }
                           return 0;
                       };
        }
        public static IdentifierPath QualifyIdentifierPath(RowKey rowKey, IdentifierPath identifierPath)
        {
            if (rowKey == null)
            {
                return identifierPath;
            }
            string[] parts = new string[identifierPath.Length];
            while (identifierPath.Length > 0)
            {
                parts[identifierPath.Length - 1] = identifierPath.Name;
                identifierPath = identifierPath.Parent;
            }
            while (rowKey != null)
            {
                if (rowKey.IdentifierPath.Length <= parts.Length)
                {
                    parts[rowKey.IdentifierPath.Length - 1] = rowKey.Value.ToString();
                }
                rowKey = rowKey.Parent;
            }
            foreach (var part in parts)
            {
                identifierPath = new IdentifierPath(identifierPath, part);
            }
            return identifierPath;
        }
    }
}
