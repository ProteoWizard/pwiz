using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Internal
{
    internal class RowItemIndex
    {
        private readonly IDictionary<object, IndexSet> _index;
        public RowItemIndex(IEnumerable<RowItem> rowItems)
        {
            RowItems = ImmutableList.ValueOf(rowItems);
            _index = RowItems.Select((rowItem, index) => new KeyValuePair<object, int>(GetKey(rowItem), index))
                .Where(pair => null != pair.Key)
                .ToLookup(pair => pair.Key, pair => pair.Value)
                .ToDictionary(grouping => grouping.Key, IndexSet.OfValues);
        }
        public IList<RowItem> RowItems { get; private set; }
        public IndexSet GetRowIndexes(object key)
        {
            IndexSet indexSet;
            if (_index.TryGetValue(key, out indexSet))
            {
                return indexSet;
            }
            return IndexSet.EMPTY;
        }
        private object GetKey(RowItem rowItem)
        {
            while (rowItem.Parent != null)
            {
                rowItem = rowItem.Parent;
            }
            return rowItem.Key;
        }

        public class IndexSet : ValueSet<IndexSet, int>
        {
        }
    }
}
