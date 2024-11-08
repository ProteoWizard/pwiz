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
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Internal
{
    /// <summary>
    /// Handles transforming a ViewInfo and a collection of objects into a list of RowItems.
    /// First the objects are expanded into a list of RowNode's which lists of child
    /// nodes corresponding to the one-to-many relationships.
    /// Second, the RowNode's are and aggregated and grouped if the ViewInfo has any 
    /// aggregates.
    /// Third, the RowNodes are transformed into RowItems.  RowItems have only one 
    /// list of children, corresponding to the SublistId of the ViewInfo.  Other
    /// children become PivotKeys in the RowItem.
    /// </summary>
    internal class Pivoter
    {
        /// <summary>
        /// Pivoter Constructor.  Initializes many lists of ColumnDescriptors that
        /// get used while doing the work of expanding, aggregating, and pivoting.
        /// </summary>
        public Pivoter(ViewInfo viewInfo)
        {
            ViewInfo = viewInfo;
            var collectionColumnArray = ViewInfo.GetCollectionColumns().ToArray();
            Array.Sort(collectionColumnArray, (cd1, cd2) => cd1.PropertyPath.CompareTo(cd2.PropertyPath));
            CollectionColumns = Array.AsReadOnly(collectionColumnArray);
            var sublistColumns = new List<ColumnDescriptor>();
            var pivotColumns = new List<ColumnDescriptor>();
            foreach (var collectionColumn in CollectionColumns)
            {
                if (collectionColumn.PropertyPath.IsRoot)
                {
                    continue;
                }
                if (ViewInfo.SublistId.StartsWith(collectionColumn.PropertyPath))
                {
                    sublistColumns.Add(collectionColumn);
                }
                else
                {
                    pivotColumns.Add(collectionColumn);
                }
            }
            SublistColumns = ImmutableList.ValueOf(sublistColumns);
            PivotColumns = ImmutableList.ValueOf(pivotColumns);
        }

        /// <summary>
        /// The ViewInfo that this Pivoter was created from.
        /// </summary>
        public ViewInfo ViewInfo { get; private set; }
        /// <summary>
        /// The list of all of the collection columns used by the ViewInfo.
        /// This includes the ParentColumn of the ViewInfo.
        /// </summary>
        public IList<ColumnDescriptor> CollectionColumns { get; private set; }
        /// <summary>
        /// The list of collection columns which should be expanded into
        /// separate RowItems in the grid.
        /// </summary>
        public IList<ColumnDescriptor> SublistColumns { get; private set; }
        /// <summary>
        /// The list of collection columns which are not SublistColumns,
        /// and show be expanded horizontally in the grid.
        /// </summary>
        public IList<ColumnDescriptor> PivotColumns { get; private set; }

        public IEnumerable<RowItem> Expand(CancellationToken cancellationToken, RowItem rowItem, int sublistColumnIndex)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sublistColumnIndex >= SublistColumns.Count)
            {
                return new[] {rowItem};
            }
            var sublistColumn = SublistColumns[sublistColumnIndex];
            object parentValue = sublistColumn.Parent.GetPropertyValue(rowItem, null);
            if (null == parentValue)
            {
                return new[] {rowItem};
            }
            var items = sublistColumn.CollectionInfo.GetItems(parentValue).Cast<object>().ToArray();
            if (items.Length == 0)
            {
                return new[] {rowItem};
            }
            cancellationToken.ThrowIfCancellationRequested();
            IList<object> keys = null;
            if (sublistColumn.CollectionInfo.IsDictionary)
            {
                keys = sublistColumn.CollectionInfo.GetKeys(parentValue).Cast<object>().ToArray();
            }
            var expandedItems = new List<RowItem>();
            for (int index = 0; index < items.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                object key = keys == null ? index : keys[index];
                var child = rowItem.SetRowKey(rowItem.RowKey.AppendValue(sublistColumn.PropertyPath, key));
                expandedItems.AddRange(Expand(cancellationToken, child, sublistColumnIndex + 1));
            }
            return expandedItems;
        }

        public RowItem Pivot(CancellationToken cancellationToken, RowItem rowItem)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var pivotColumn in PivotColumns)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parent = pivotColumn.Parent.CollectionAncestor();
                IList<PivotKey> pivotKeys;
                if (null == parent)
                {
                    pivotKeys = new PivotKey[] { null };
                }
                else
                {
                    pivotKeys = rowItem.PivotKeys.Where(key => key.Last.Key.Equals(parent.PropertyPath)).ToList();
                    if (pivotKeys.Count == 0)
                    {
                        pivotKeys = new PivotKey[] {null};
                    }
                }
                foreach (var pivotKey in pivotKeys)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var parentValue = pivotColumn.Parent.GetPropertyValue(rowItem, pivotKey);
                    if (null != parentValue)
                    {
                        var keys = pivotColumn.CollectionInfo.GetKeys(parentValue).Cast<object>().ToArray();
                        if (keys.Length > 0)
                        {
                            var newPivotKeys = rowItem.PivotKeys.Except(new[] {pivotKey}).ToList();
                            var pivotKeyLocal = pivotKey ?? PivotKey.EMPTY;
                            var propertyPath = pivotColumn.PropertyPath;
                            newPivotKeys.AddRange(keys.Select(key => pivotKeyLocal.AppendValue(propertyPath, key)));
                            rowItem = rowItem.SetPivotKeys(new HashSet<PivotKey>(newPivotKeys));
                        }

                    }
                }
            }
            return rowItem;
        }

        public IEnumerable<RowItem> Filter(CancellationToken cancellationToken, IEnumerable<RowItem> rowItems)
        {
            if (ViewInfo.Filters.Count == 0)
            {
                return rowItems;
            }
            var filteredRows = new List<RowItem>();
            foreach (var rowItem in rowItems)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var filteredRowItem = rowItem;
                foreach (var filter in ViewInfo.Filters)
                {
                    filteredRowItem = filter.ApplyFilter(filteredRowItem);
                    if (null == filteredRowItem)
                    {
                        break;
                    }
                }
                if (null != filteredRowItem)
                {
                    filteredRows.Add(filteredRowItem);
                }
            }
            return filteredRows;
        }

        public ReportResults ExpandAndPivot(CancellationToken cancellationToken, IEnumerable<RowItem> rowItems)
        {
            var expandedItems = rowItems.SelectMany(rowItem => Expand(cancellationToken, rowItem, 0)).ToArray();
            var pivotedItems = expandedItems.Select(item => Pivot(cancellationToken, item));
            var filteredItems = Filter(cancellationToken, pivotedItems);
            var rows = ImmutableList.ValueOf(filteredItems);
            var result = new ReportResults(rows, GetItemProperties(rows));
            if (ViewInfo.HasTotals)
            {
                result = GroupAndTotal(cancellationToken, result);
            }
            return result;
        }

        public ReportResults GroupAndTotal(CancellationToken cancellationToken, ReportResults pivotedRows)
        {
            IDictionary<IList<Tuple<PropertyPath, PivotKey, object>>, List<GroupedRow>> allReportRows
                = new Dictionary<IList<Tuple<PropertyPath, PivotKey, object>>, List<GroupedRow>>();
            var groupColumns = ViewInfo.DisplayColumns
                .Where(col => TotalOperation.GroupBy == col.ColumnSpec.TotalOperation)
                .Select(col => col.ColumnDescriptor)
                .ToArray();
            var pivotOnColumns = ViewInfo.DisplayColumns
                .Where(col => TotalOperation.PivotKey == col.ColumnSpec.TotalOperation)
                .Select(col => col.ColumnDescriptor)
                .ToArray();
            var allInnerPivotKeys = new HashSet<PivotKey>();
            var allPivotKeys = new Dictionary<PivotKey, PivotKey>();
            foreach (var rowItem in pivotedRows.RowItems)
            {
                cancellationToken.ThrowIfCancellationRequested();
                allInnerPivotKeys.UnionWith(rowItem.PivotKeys);
                IList<Tuple<PropertyPath, PivotKey, object>> groupKey = new List<Tuple<PropertyPath, PivotKey, object>>();
                foreach (var column in groupColumns)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var pivotColumn = GetPivotColumn(column);
                    if (null == pivotColumn)
                    {
                        groupKey.Add(new Tuple<PropertyPath, PivotKey, object>(column.PropertyPath, null,
                            column.GetPropertyValue(rowItem, null)));
                    }
                    else
                    {
                        foreach (var pivotKey in GetPivotKeys(pivotColumn.PropertyPath, new []{rowItem}))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!pivotKey.Contains(pivotColumn.PropertyPath))
                            {
                                continue;
                            }
                            groupKey.Add(new Tuple<PropertyPath, PivotKey, object>(column.PropertyPath, pivotKey, column.GetPropertyValue(rowItem, pivotKey)));
                        }
                    }
                }
                groupKey = ImmutableList.ValueOf(groupKey);
                var pivotOnKeyValues = new List<KeyValuePair<PropertyPath, object>>();
                foreach (var column in pivotOnColumns)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var pivotColumn = GetPivotColumn(column);
                    if (null == pivotColumn)
                    {
                        pivotOnKeyValues.Add(new KeyValuePair<PropertyPath, object>(column.PropertyPath,
                            column.GetPropertyValue(rowItem, null)));
                    }
                    else
                    {
                        Messages.WriteAsyncDebugMessage(@"Unable to pivot on column {0} because it is already pivoted.", pivotColumn.PropertyPath); // N.B. see TraceWarningListener for output details
                    }
                }
                var pivotOnKey = PivotKey.GetPivotKey(allPivotKeys, pivotOnKeyValues);
                List<GroupedRow> rowGroups;
                if (!allReportRows.TryGetValue(groupKey, out rowGroups))
                {
                    rowGroups = new List<GroupedRow>();
                    allReportRows.Add(groupKey, rowGroups);
                }
                var rowGroup = rowGroups.FirstOrDefault(rg => !rg.ContainsKey(pivotOnKey));
                if (null == rowGroup)
                {
                    rowGroup = new GroupedRow();
                    rowGroups.Add(rowGroup);
                }
                rowGroup.AddInnerRow(pivotOnKey, rowItem);
            }
            var outerPivotKeys = allPivotKeys.Keys.Where(key=>key.Length == pivotOnColumns.Length).ToArray();
            var pivotKeyComparer = PivotKey.GetComparer(ViewInfo.DataSchema);
            Array.Sort(outerPivotKeys, pivotKeyComparer);
            var innerPivotKeys = allInnerPivotKeys.ToArray();
            Array.Sort(innerPivotKeys, pivotKeyComparer);
            var reportItemProperties = new List<DataPropertyDescriptor>();
            var propertyNames = new HashSet<string>();
            foreach (var displayColumn in ViewInfo.DisplayColumns)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (displayColumn.ColumnSpec.Hidden)
                {
                    continue;
                }
                var totalOperation = displayColumn.ColumnSpec.TotalOperation;
                if (TotalOperation.GroupBy == totalOperation)
                {
                    var pivotColumn = GetPivotColumn(displayColumn.ColumnDescriptor);
                    if (null == pivotColumn)
                    {
                        string propertyName = MakeUniqueName(propertyNames, displayColumn.PropertyPath);
                        reportItemProperties.Add(new GroupedPropertyDescriptor(propertyName, displayColumn, null));
                    }
                    else
                    {
                        foreach (var innerPivotKey in innerPivotKeys)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            string propertyName = MakeUniqueName(propertyNames, displayColumn.PropertyPath);
                            reportItemProperties.Add(new GroupedPropertyDescriptor(propertyName, displayColumn, innerPivotKey));
                        }
                    }
                }
            }
            foreach (var outerPivotKey in outerPivotKeys)
            {
                foreach (var displayColumn in ViewInfo.DisplayColumns)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (displayColumn.ColumnSpec.Hidden)
                    {
                        continue;
                    }
                    if (TotalOperation.PivotValue == displayColumn.ColumnSpec.TotalOperation || TotalOperation.PivotKey == displayColumn.ColumnSpec.TotalOperation)
                    {
                        var pivotColumn = GetPivotColumn(displayColumn.ColumnDescriptor);
                        if (null == pivotColumn)
                        {
                            string propertyName = MakeUniqueName(propertyNames, displayColumn.PropertyPath);
                            reportItemProperties.Add(new GroupedPropertyDescriptor(propertyName, outerPivotKey, displayColumn, null));
                        }
                        else
                        {
                            foreach (var innerPivotKey in allInnerPivotKeys)
                            {
                                string propertyName = MakeUniqueName(propertyNames, displayColumn.PropertyPath);
                                reportItemProperties.Add(new GroupedPropertyDescriptor(propertyName, outerPivotKey, displayColumn, innerPivotKey));
                            }
                        }
                    }
                }
            }
            return new ReportResults(allReportRows.SelectMany(entry=>entry.Value.Select(
                reportRow=>new RowItem(reportRow))), 
                reportItemProperties);
        }

        public HashSet<PivotKey> GetPivotKeys(PropertyPath pivotColumnId, IEnumerable<RowItem> rowItems)
        {
            var pivotKeys = new HashSet<PivotKey>();
            foreach (var row in rowItems)
            {
                pivotKeys.UnionWith(row.PivotKeys);
            }
            pivotKeys.RemoveWhere(pivotKey => !pivotKey.Contains(pivotColumnId));
            return pivotKeys;
        }
        public IDictionary<PropertyPath, ICollection<PivotKey>> GetAllPivotKeys(IEnumerable<RowItem> rowItems)
        {
            var result = new Dictionary<PropertyPath, ICollection<PivotKey>>();
            var enumerable = rowItems as RowItem[] ?? rowItems.ToArray();
            foreach (var pivotColumn in PivotColumns)
            {
                result.Add(pivotColumn.PropertyPath, GetPivotKeys(pivotColumn.PropertyPath, enumerable));
            }
            return result;
        }

        private ColumnDescriptor GetPivotColumn(ColumnDescriptor columnDescriptor)
        {
            return PivotColumns.LastOrDefault(col => columnDescriptor.PropertyPath.StartsWith(col.PropertyPath));
        }

        public IEnumerable<DataPropertyDescriptor> GetItemProperties(IEnumerable<RowItem> rowItems)
        {
            var columnNames = new HashSet<string>();
            var propertyDescriptors = new List<DataPropertyDescriptor>();
            var pivotDisplayColumns = new Dictionary<PivotKey, List<DisplayColumn>>();
            var rowItemsArray = rowItems as RowItem[] ?? rowItems.ToArray();
            foreach (var displayColumn in ViewInfo.DisplayColumns)
            {
                if (displayColumn.ColumnSpec.Hidden)
                {
                    continue;
                }
                var pivotColumn = PivotColumns.LastOrDefault(pc => displayColumn.PropertyPath.StartsWith(pc.PropertyPath));
                ICollection<PivotKey> pivotKeys = null;
                if (pivotColumn != null)
                {
                    pivotKeys = GetPivotKeys(pivotColumn.PropertyPath, rowItemsArray);
                }

                if (pivotKeys == null)
                {
                    propertyDescriptors.Add(new ColumnPropertyDescriptor(displayColumn, MakeUniqueName(columnNames, displayColumn.PropertyPath)));
                    continue;
                }
                foreach (var value in pivotKeys)
                {
                    List<DisplayColumn> columns;
                    if (!pivotDisplayColumns.TryGetValue(value, out columns))
                    {
                        columns = new List<DisplayColumn>();
                        pivotDisplayColumns.Add(value, columns);
                    }
                    columns.Add(displayColumn);
                }
            }
            var allPivotKeys = pivotDisplayColumns.Keys.ToArray();
            Array.Sort(allPivotKeys, PivotKey.GetComparer(ViewInfo.DataSchema));
            foreach (var pivotKey in allPivotKeys)
            {
                foreach (var pivotColumn in pivotDisplayColumns[pivotKey])
                {
                    var qualifiedPropertyPath = PivotKey.QualifyPropertyPath(pivotKey, pivotColumn.PropertyPath);
                    var columnName = MakeUniqueName(columnNames, qualifiedPropertyPath);
                    propertyDescriptors.Add(new ColumnPropertyDescriptor(pivotColumn, columnName, qualifiedPropertyPath, pivotKey));
                }
            }
            return propertyDescriptors;
        }
        private string MakeUniqueName(HashSet<string> columnNames, PropertyPath propertyPath)
        {
            return MakeUniqueName(columnNames, @"COLUMN_" + propertyPath);
        }

        private string MakeUniqueName(HashSet<string> existingNames, string baseName)
        {
            string columnName = baseName;
            for (int index = 1; !existingNames.Add(columnName); index++)
            {
                columnName = baseName + index;
            }
            return columnName;
            
        }
    }
}
