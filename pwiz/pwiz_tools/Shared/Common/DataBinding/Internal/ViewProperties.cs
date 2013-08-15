/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Linq;

namespace pwiz.Common.DataBinding.Internal
{
    internal class ViewProperties : PropertyDescriptorCollection
    {
        public const string ColumnNamePrefix = "COLUMN_";
        private const string DisplayColumnPrefix = "DISPLAYCOLUMN_";
        public ViewProperties(ViewInfo viewInfo, IDictionary<PropertyPath, ICollection<PivotKey>> pivotKeys) 
            : this(viewInfo, new DisplayColumnProperties(viewInfo, pivotKeys))
        {
            
        }

        private ViewProperties(ViewInfo viewInfo, DisplayColumnProperties displayColumnProperties)
            : base(displayColumnProperties.PropertyDescriptorArray, true)
        {
            ViewInfo = viewInfo;
        }

        public ViewInfo ViewInfo
        {
            get; private set;
        }

        public override PropertyDescriptor Find(string name, bool ignoreCase)
        {
            if (!name.StartsWith(ColumnNamePrefix))
            {
                return base.Find(name, ignoreCase);
            }
            var propertyPath = PropertyPath.Parse(name.Substring(ColumnNamePrefix.Length));
            PivotKey pivotKey;
            var columnDescriptor = FindColumn(propertyPath, out pivotKey);
            if (columnDescriptor == null)
            {
                return base.Find(name, ignoreCase);
            }
            return new ColumnPropertyDescriptor(new DisplayColumn(
                ViewInfo, new ColumnSpec(), columnDescriptor), name);
        }

        private ColumnDescriptor FindColumn(PropertyPath propertyPath, out PivotKey pivotKey)
        {
            if (propertyPath.IsRoot)
            {
                pivotKey = null;
                return ViewInfo.ParentColumn;
            }
            var parentColumn = FindColumn(propertyPath.Parent, out pivotKey);
            if (parentColumn == null)
            {
                return null;
            }
            var column = parentColumn.ResolveChild(propertyPath.Name);
            if (column != null)
            {
                return column;
            }
            column = parentColumn.ResolveChild(null);
            if (column != null)
            {
                pivotKey = PivotKey.OfValues(pivotKey, PropertyPath.Root, new[] {
                    new KeyValuePair<PropertyPath, object>(column.PropertyPath, 
                        Convert.ChangeType(propertyPath.Name, column.CollectionInfo.KeyType))});
                return column;
            }
            return null;
        }

        class DisplayColumnProperties
        {
            public DisplayColumnProperties(ViewInfo viewInfo, IDictionary<PropertyPath, ICollection<PivotKey>> pivotKeysByColumn)
            {
                var propertyDescriptors = new List<PropertyDescriptor>();
                var displayColumnsByKey = new Dictionary<PivotKey, List<DisplayColumn>>();
                var columnNames = new HashSet<string>();
                foreach (var displayColumn in viewInfo.DisplayColumns)
                {
                    if (displayColumn.ColumnSpec.Hidden)
                    {
                        continue;
                    }
                    IEnumerable<PivotKey> pivotKeys = GetPivotKeys(pivotKeysByColumn, displayColumn);
                    if (pivotKeys != null)
                    {
                        foreach (var value in pivotKeys)
                        {
                            List<DisplayColumn> columns;
                            if (!displayColumnsByKey.TryGetValue(value, out columns))
                            {
                                columns = new List<DisplayColumn>();
                                displayColumnsByKey.Add(value, columns);
                            }
                            columns.Add(displayColumn);
                        }
                    }
                    else
                    {
                        string propertyName = MakeUniqueName(columnNames, 
                                                             displayColumn.PropertyPath);
                        propertyDescriptors.Add(new ColumnPropertyDescriptor(displayColumn, propertyName, displayColumn.PropertyPath, null));
                    }
                }
                var allPivotKeys = displayColumnsByKey.Keys.ToArray();
                Array.Sort(allPivotKeys, PivotKey.GetComparer(viewInfo.DataSchema));
                foreach (var pivotKey in allPivotKeys)
                {
                    foreach (var displayColumn in displayColumnsByKey[pivotKey])
                    {
                        var identifierPath = PivotKey.QualifyIdentifierPath(pivotKey, displayColumn.PropertyPath);
                        var columnName = MakeUniqueName(columnNames, identifierPath);
                        propertyDescriptors.Add(new ColumnPropertyDescriptor(displayColumn, columnName, identifierPath, pivotKey));
                    }
                }
                PropertyDescriptorArray = propertyDescriptors.ToArray();
            }
            private IEnumerable<PivotKey> GetPivotKeys(IEnumerable<KeyValuePair<PropertyPath, ICollection<PivotKey>>> pivotKeyDict, DisplayColumn displayColumn)
            {
                ICollection<PivotKey> result = null;
                PropertyPath longestId = null;
                foreach (var entry in pivotKeyDict)
                {
                    if (longestId != null && longestId.Length > entry.Key.Length)
                    {
                        continue;
                    }
                    if (!displayColumn.PropertyPath.StartsWith(entry.Key))
                    {
                        continue;
                    }
                    longestId = entry.Key;
                    result = entry.Value;
                }
                return result;
            }
            public PropertyDescriptor[] PropertyDescriptorArray { get; private set; }
        }
        static string MakeUniqueName(HashSet<string> columnNames, PropertyPath propertyPath)
        {
            string baseName = DisplayColumnPrefix + propertyPath;

            var columnName = baseName;
            for (int index = 1; columnNames.Contains(columnName); index++)
            {
                columnName = baseName + propertyPath + index;
            }
            columnNames.Add(columnName);
            return columnName;
        }

    }
}
