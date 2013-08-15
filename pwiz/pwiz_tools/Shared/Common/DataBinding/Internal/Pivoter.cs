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
using System.ComponentModel;
using System.Linq;
using System.Threading;

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
            var filterPredicates = new Predicate<RowNode>[CollectionColumns.Count];
            for (int iCollectionColumn = 0; iCollectionColumn < CollectionColumns.Count; iCollectionColumn++)
            {
                int column = iCollectionColumn;
                filterPredicates[iCollectionColumn] =
                    MakeFilterPredicate(
                        ViewInfo.Filters.Where(filter => filter.CollectionColumn == CollectionColumns[column]));
            }
            Filters = Array.AsReadOnly(filterPredicates);

            var sublistColumns = new List<ColumnDescriptor>();
            var pivotColumns = new List<ColumnDescriptor>();
            foreach (var collectionColumn in CollectionColumns)
            {
                if (ViewInfo.SublistId.StartsWith(collectionColumn.PropertyPath))
                {
                    sublistColumns.Add(collectionColumn);
                }
                else
                {
                    pivotColumns.Add(collectionColumn);
                }
            }

            SublistColumns = Array.AsReadOnly(sublistColumns.ToArray());
            PivotColumns = Array.AsReadOnly(pivotColumns.ToArray());
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
        /// <summary>
        /// Contains a Predicate which has the user-defined Filter for each
        /// ColumnDescriptor in CollectionColumns.
        /// </summary>
        internal IList<Predicate<RowNode>> Filters { get; private set; }

        private Predicate<RowNode> MakeFilterPredicate(IEnumerable<FilterInfo> filterInfos)
        {
            var predicates = new List<Predicate<RowNode>>();
            foreach (var grouping in filterInfos.ToLookup(filterInfo=>filterInfo.ColumnDescriptor, filterInfo=>filterInfo.Predicate))
            {
                var predicate = MakePredicate(grouping.Key, grouping);
                if (predicate != null)
                {
                    predicates.Add(predicate);    
                }
            }
            return Conjunction(predicates.ToArray());
        }
        private Predicate<RowNode> MakePredicate(ColumnDescriptor columnDescriptor, IEnumerable<Predicate<object>> predicates)
        {
            var predicate = Conjunction(predicates.Where(p=>null != p).ToArray());
            return rowNode=>predicate(columnDescriptor.DataSchema.UnwrapValue(columnDescriptor.GetPropertyValue(rowNode, false)));
        }

        private object GetValue(RowNode rowNode, ColumnDescriptor columnDescriptor)
        {
            if (columnDescriptor.PropertyPath.Length == rowNode.PropertyPath.Length)
            {
                return rowNode.RowItem.Value;
            }
            var parentValue = GetValue(rowNode, columnDescriptor.Parent);
            if (parentValue == null)
            {
                return null;
            }
            return columnDescriptor.GetPropertyValueFromParent(parentValue, null, false);
        }
        public int Expand(TickCounter tickCounter, RowNode rowNode, int columnIndex)
        {
            tickCounter.Tick();
            var unboundColumn = CollectionColumns[columnIndex];
            if (!unboundColumn.PropertyPath.StartsWith(rowNode.PropertyPath))
            {
                return columnIndex;
            }
            int result = columnIndex + 1;
            while (result < CollectionColumns.Count && CollectionColumns[result].PropertyPath.StartsWith(unboundColumn.PropertyPath))
            {
                result++;
            }
            if (unboundColumn.CollectionInfo == null)
            {
                return result;
            }
            object parentValue = GetValue(rowNode, unboundColumn.Parent);
            if (parentValue == null)
            {
                return result;
            }
            if (!Filters[columnIndex](rowNode))
            {
                return result;
            }
            var items = unboundColumn.CollectionInfo.GetItems(parentValue).Cast<object>().ToArray();
            IList<object> keys = null;
            if (unboundColumn.CollectionInfo.IsDictionary)
            {
                keys = unboundColumn.CollectionInfo.GetKeys(parentValue).Cast<object>().ToArray();
            }

            for (int index = 0; index < items.Length; index++)
            {
                object key = keys == null ? index : keys[index];
                var rowItem = new RowItem(rowNode.RowItem, unboundColumn.PropertyPath, key, items[index]);
                var child = new RowNode(rowItem);
                rowNode.AddChild(child);

                for (int currentColumnIndex = columnIndex + 1; currentColumnIndex < result;)
                {
                    currentColumnIndex = Expand(tickCounter, child, currentColumnIndex);
                }
            }
            return result;
        }
        public IEnumerable<RowNode> Expand(TickCounter tickCounter, RowItem rowItem)
        {
            var root = new RowNode(rowItem);
            if (!Filters[0](root))
            {
                return new RowNode[0];
            }

            for (int currentColumnIndex = 1; currentColumnIndex < CollectionColumns.Count; )
            {
                currentColumnIndex = Expand(tickCounter, root, currentColumnIndex);
            }
            return new[]{root};
        }
        public IEnumerable<RowNode> Expand(RowItem rowItem)
        {
            return Expand(new TickCounter(), rowItem);
        }


        public IEnumerable<RowItem> ExpandAndPivot(TickCounter tickCounter, IEnumerable<RowItem> rowItems)
        {
            var expandedNodes = rowItems.SelectMany(rowItem => Expand(rowItem)).ToArray();
            return Pivot(tickCounter, expandedNodes);
        }
        private PivotKey GetPivotKey(RowItem rowItem)
        {
            return rowItem.GetGroupKey().RemoveSublist(ViewInfo.SublistId);
        }

        private IEnumerable<RowItem> GetRowItems(TickCounter tickCounter, RowNode rowNode, RowItem parentRowItem, int sublistColumnIndex)
        {
            tickCounter.Tick();
            var sublistColumn = SublistColumns[sublistColumnIndex];
            var result = new List<RowItem>();
            HashSet<PivotKey> pivotKeySet = new HashSet<PivotKey>();
            foreach (var pivotColumn in PivotColumns)
            {
                if (!pivotColumn.PropertyPath.StartsWith(sublistColumn.PropertyPath))
                {
                    continue;
                }
                if (sublistColumnIndex + 1 < SublistColumns.Count)
                {
                    if (pivotColumn.PropertyPath.StartsWith(SublistColumns[sublistColumnIndex + 1].PropertyPath))
                    {
                        continue;
                    }
                }
                foreach (var descendant in rowNode.GetDescendants(pivotColumn.PropertyPath))
                {
                    tickCounter.Tick();
                    var pivotKey = GetPivotKey(descendant.RowItem);
                    pivotKeySet.Add(pivotKey);
                }
            }
            var rowItem = rowNode.RowItem.SetParent(parentRowItem);
            rowItem = rowItem.SetPivotKeys(pivotKeySet);
            if (sublistColumnIndex < SublistColumns.Count - 1)
            {
                foreach (var child in rowNode.GetChildren(SublistColumns[sublistColumnIndex + 1].PropertyPath))
                {
                    result.AddRange(GetRowItems(tickCounter, child, rowItem, sublistColumnIndex + 1));
                }
            }
            if (result.Count > 0)
            {
                return result;
            }
            for (int icolFilter = sublistColumnIndex; icolFilter < Filters.Count; icolFilter++)
            {
                if (!Filters[icolFilter](rowNode))
                {
                    return new RowItem[0];
                }
            }
            return new[] {rowItem};
        }
        public IEnumerable<RowItem> Pivot(TickCounter tickCounter, IEnumerable<RowNode> rowNodes)
        {
            var result = new List<RowItem>();
            foreach (var rowNode in rowNodes)
            {
                result.AddRange(GetRowItems(tickCounter, rowNode, null, 0));
            }
            return result;
        }
        private static Predicate<T> Conjunction<T>(IEnumerable<Predicate<T>> predicates)
        {
            return value =>
                       {
                           foreach (var predicate in predicates)
                           {
                               if (!predicate(value))
                               {
                                   return false;
                               }
                           }
                           return true;
                       };
        }

        public HashSet<PivotKey> GetPivotKeys(PropertyPath pivotColumnId, IEnumerable<RowItem> rowItems)
        {
            PropertyPath sublistId = PropertyPath.Root;
            foreach (ColumnDescriptor t in SublistColumns)
            {
                if (!pivotColumnId.StartsWith(t.PropertyPath))
                {
                    break;
                }
                sublistId = t.PropertyPath;
            }
            var result = new HashSet<PivotKey>();
            foreach (var sublistItem in rowItems)
            {
                var rowItem = sublistItem;
                while (rowItem != null)
                {
                    if (Equals(rowItem.SublistId, sublistId))
                    {
                        result.UnionWith(rowItem.PivotKeys.Where(pivotKey=>pivotKey.FindValue(pivotColumnId) != null));
                    }
                    rowItem = rowItem.Parent;
                }
            }
            return result;
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

        public IEnumerable<PropertyDescriptor> GetItemProperties(IEnumerable<RowItem> rowItems)
        {
            var columnNames = new HashSet<string>();
            var propertyDescriptors = new List<PropertyDescriptor>();
            var pivotDisplayColumns = new Dictionary<PivotKey, List<DisplayColumn>>();
            var rowItemsArray = rowItems as RowItem[] ?? rowItems.ToArray();
            foreach (var displayColumn in ViewInfo.DisplayColumns)
            {
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
                    var identifierPath = PivotKey.QualifyIdentifierPath(pivotKey, pivotColumn.PropertyPath);
                    var columnName = MakeUniqueName(columnNames, identifierPath);
                    propertyDescriptors.Add(new ColumnPropertyDescriptor(pivotColumn, columnName, identifierPath, pivotKey));
                }
            }
            return propertyDescriptors;
        }
        private string MakeUniqueName(HashSet<string> columnNames, PropertyPath propertyPath)
        {
            const string baseName = "COLUMN_";
            string columnName = baseName + propertyPath;
            for (int index = 1; columnNames.Contains(columnName); index++ )
            {
                columnName = baseName + propertyPath + index;
            }
            return columnName;
        }

        public class TickCounter
        {
            private long _tickCount;
            public TickCounter(CancellationToken cancellationToken) : this(cancellationToken, long.MaxValue)
            {
            }
            public TickCounter(CancellationToken cancellationToken, long maxTickCount)
            {
                CancellationToken = cancellationToken;
                MaxTickCount = maxTickCount;
            }
            public TickCounter(long maxTickCount) : this(CancellationToken.None, maxTickCount)
            {
            }
            public TickCounter() : this(1000000)
            {
            }
            public long TickCount {get { return _tickCount; }}
            public void Tick()
            {
                CancellationToken.ThrowIfCancellationRequested();
                if (Interlocked.Increment(ref _tickCount) >= MaxTickCount)
                {
                    throw new OperationCanceledException(string.Format("Number of steps exceeded {0}", MaxTickCount));
                }
            }
            public long MaxTickCount { get; private set; }
            public CancellationToken CancellationToken { get; private set; }
        }
    }

}
