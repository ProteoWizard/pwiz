using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Common.DataBinding
{
    public class Pivoter
    {
        private IDictionary<IdentifierPath, HashSet<RowKey>> _pivotValues = new Dictionary<IdentifierPath, HashSet<RowKey>>();
        public Pivoter(ViewInfo viewInfo)
        {
            ViewInfo = viewInfo;
            var unboundColumnArray = ViewInfo.GetCollectionColumns().ToArray();
            Array.Sort(unboundColumnArray, (cd1, cd2) => cd1.IdPath.CompareTo(cd2.IdPath));
            CollectionColumns = Array.AsReadOnly(unboundColumnArray);
            var sublistColumns = new List<ColumnDescriptor>();
            var pivotColumns = new List<ColumnDescriptor>();
            foreach (var collectionColumn in CollectionColumns)
            {
                if (ViewInfo.SublistId.StartsWith(collectionColumn.IdPath))
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

        public ViewInfo ViewInfo { get; private set; }
        public IList<ColumnDescriptor> CollectionColumns { get; private set; }
        public IList<ColumnDescriptor> SublistColumns { get; private set; }
        public IList<ColumnDescriptor> PivotColumns { get; private set; }
        public ICollection<RowKey> GetPivotValues(IdentifierPath identifierPath)
        {
            var deepestId = IdentifierPath.Root;
            ICollection<RowKey> deepestResult = null;
            foreach (var entry in _pivotValues)
            {
                if (entry.Key.Length > deepestId.Length && identifierPath.StartsWith(entry.Key))
                {
                    deepestResult = entry.Value;
                }
            }
            return deepestResult;
        }
        
        private object GetValue(RowNode rowNode, ColumnDescriptor columnDescriptor)
        {
            if (columnDescriptor.IdPath.Length == rowNode.IdentifierPath.Length)
            {
                return rowNode.RowItem.Value;
            }
            var parentValue = GetValue(rowNode, columnDescriptor.Parent);
            if (parentValue == null)
            {
                return null;
            }
            return columnDescriptor.GetPropertyValueFromParent(parentValue, null);
        }
        private int Expand(RowNode rowNode, int columnIndex)
        {
            var unboundColumn = CollectionColumns[columnIndex];
            if (!unboundColumn.IdPath.StartsWith(rowNode.IdentifierPath))
            {
                return columnIndex;
            }
            int result = columnIndex + 1;
            while (result < CollectionColumns.Count && CollectionColumns[result].IdPath.StartsWith(unboundColumn.IdPath))
            {
                result++;
            }
            object parentValue = GetValue(rowNode, unboundColumn.Parent);
            if (parentValue == null)
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
                var child = new RowNode(new RowItem(rowNode.RowItem, unboundColumn.IdPath, key, items[index]));
                rowNode.AddChild(child);

                for (int currentColumnIndex = columnIndex + 1; currentColumnIndex < result;)
                {
                    currentColumnIndex = Expand(child, currentColumnIndex);
                }
            }
            return result;
        }
        public RowNode Expand(RowItem rowItem)
        {
            var root = new RowNode(rowItem);
            for (int currentColumnIndex = 0; currentColumnIndex < CollectionColumns.Count;)
            {
                currentColumnIndex = Expand(root, currentColumnIndex);
            }
            return root;
        }
        public IEnumerable<RowItem> ExpandAndPivot(IEnumerable<RowItem> rowItems)
        {
            bool pivotValuesChanged = false;
            return
                rowItems.Select(rowItem => Expand(rowItem)).SelectMany(rowNode => Pivot(rowNode, ref pivotValuesChanged));
        }
        private RowKey GetRowKey(RowItem rowItem)
        {
            if (rowItem.SublistId.IsRoot)
            {
                return null;
            }
            return new RowKey(GetRowKey(rowItem.Parent), rowItem.SublistId, rowItem.Key);
        }

        private IEnumerable<RowItem> GetSublistItems(RowNode rowNode, int sublistColumnIndex)
        {
            if (sublistColumnIndex >= SublistColumns.Count)
            {
                return new[] {rowNode.RowItem};
            }
            var sublistColumn = SublistColumns[sublistColumnIndex];
            var result = new List<RowItem>();
            foreach (var child in rowNode.GetChildren(sublistColumn.IdPath))
            {
                result.AddRange(GetSublistItems(child, sublistColumnIndex + 1));
            }
            if (result.Count > 0)
            {
                return result;
            }
            return new[] {rowNode.RowItem};
        }
        public IEnumerable<RowItem> Pivot(IEnumerable<RowNode> rowNodes, out bool pivotValuesChanged)
        {
            pivotValuesChanged = false;
            var result = new List<RowItem>();
            foreach (var rowNode in rowNodes)
            {
                foreach (var pivotColumn in PivotColumns)
                {
                    HashSet<RowKey> valueSet;
                    if (!_pivotValues.TryGetValue(pivotColumn.IdPath, out valueSet))
                    {
                        valueSet = new HashSet<RowKey>();
                        _pivotValues.Add(pivotColumn.IdPath, valueSet);
                    }
                    foreach (var descendant in rowNode.GetDescendants(pivotColumn.IdPath))
                    {
                        pivotValuesChanged = valueSet.Add(GetRowKey(descendant.RowItem)) || pivotValuesChanged;
                    }
                }
                result.AddRange(GetSublistItems(rowNode, 0));
            }
            return result;
        }
        public IEnumerable<RowItem> Pivot(RowNode rowNode, ref bool pivotValuesChanged)
        {
            bool valuesChanged;
            var result = Pivot(new[] {rowNode}, out valuesChanged);
            pivotValuesChanged = pivotValuesChanged || valuesChanged;
            return result;
        }
    }
}
