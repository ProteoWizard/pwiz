using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace pwiz.Common.DataBinding
{
    internal class BindingListIndex
    {
        private BindingListView _bindingListView;
        private IList<PropertyDescriptor> _indexPropertyDescriptors;
        private IDictionary<object, ICollection<int>> _rowIndexes;
        private int _maxRowChanges = 1;

        public BindingListView BindingListView
        {
            get
            {
                return _bindingListView;
            }
            set
            {
                if (BindingListView == value)
                {
                    return;
                }
                if (BindingListView != null)
                {
                    BindingListView.DataSchema.DataRowsChanged -= OnDataRowsChanged;
                    var bindingList = BindingListView.InnerList as IBindingList;
                    if (bindingList != null)
                    {
                        bindingList.ListChanged -= InnerList_ListChanged;
                    }
                }
                _bindingListView = value;
                if (BindingListView != null)
                {
                    BindingListView.DataSchema.DataRowsChanged += OnDataRowsChanged;
                    var bindingList = BindingListView.InnerList as IBindingList;
                    if (bindingList != null)
                    {
                        bindingList.ListChanged += InnerList_ListChanged;
                    }
                }
            }
        }

        public int MaxRowChanges
        {
            get
            {
                return _maxRowChanges;
            }
            set
            {
                if (MaxRowChanges == value)
                {
                    return;
                }
                _maxRowChanges = value;
                _rowIndexes = null;
            }
        }

        private void OnDataRowsChanged(object sender, DataRowsChangedEventArgs args)
        {
            var bindingListView = BindingListView;
            if (bindingListView == null)
            {
                return;
            }
            if (args.Changed.Count == 0)
            {
                return;
            }
            var changedRows = new HashSet<int>();
            var rowIndexes = GetRowIndexes();
            foreach (var key in args.Changed)
            {
                ICollection<int> indices;
                if (!rowIndexes.TryGetValue(key, out indices))
                {
                    continue;
                }
                if (indices == null)
                {
                    bindingListView.ResetBindings();
                    return;
                }
                changedRows.UnionWith(indices);
                if (changedRows.Count > MaxRowChanges)
                {
                    bindingListView.ResetBindings();
                    return;
                }
            }
            foreach (var index in changedRows)
            {
                bindingListView.ResetItem(index);
            }
        }

        private IList<PropertyDescriptor> GetIndexPropertyDescriptors()
        {
            var result = _indexPropertyDescriptors;
            if (result != null)
            {
                return result;
            }
            var indexPropertyDescriptors = new List<PropertyDescriptor>();
            var handledIds = new HashSet<IdentifierPath>();
            foreach (var columnDescriptor in BindingListView.ViewInfo.ColumnDescriptors)
            {
                AddIndexPropertyDescriptors(columnDescriptor, indexPropertyDescriptors, handledIds);
            }
            _indexPropertyDescriptors = indexPropertyDescriptors;
            return indexPropertyDescriptors;
        }

        private IDictionary<object, ICollection<int>> GetRowIndexes()
        {
            var rowIndexes = _rowIndexes;
            if (rowIndexes != null)
            {
                return rowIndexes;
            }
            var indexPropertyDescriptors = GetIndexPropertyDescriptors();
            rowIndexes = new Dictionary<object, ICollection<int>>();
            var innerList = BindingListView.InnerList;
            for (int i = 0; i < innerList.Count; i++)
            {
                var row = innerList[i];
                foreach (var propertyDescriptor in indexPropertyDescriptors)
                {
                    var dataRow = propertyDescriptor.GetValue(row) as IDataRow;
                    if (dataRow == null)
                    {
                        continue;
                    }
                    var key = dataRow.GetRowIdentifier();
                    if (key == null)
                    {
                        continue;
                    }
                    ICollection<int> indices;
                    if (rowIndexes.TryGetValue(key, out indices))
                    {
                        if (indices != null)
                        {
                            if (!indices.Contains(i))
                            {
                                if (indices.Count >= _maxRowChanges)
                                {
                                    rowIndexes[key] = null;
                                }
                                else
                                {
                                    var newRowIndexes = new int[indices.Count + 1];
                                    newRowIndexes[0] = i;
                                    indices.CopyTo(newRowIndexes, 1);
                                }
                            }
                        }
                    }
                    else
                    {
                        rowIndexes.Add(key, new[]{i});
                    }
                }
            }
            _rowIndexes = rowIndexes;
            return rowIndexes;
        }


        private static void AddIndexPropertyDescriptors(ColumnDescriptor columnDescriptor, List<PropertyDescriptor> indexPropertyDescriptors, HashSet<IdentifierPath> handledIdentifierPaths)
        {
            if (columnDescriptor == null)
            {
                return;
            }
            AddIndexPropertyDescriptors(columnDescriptor.Parent, indexPropertyDescriptors, handledIdentifierPaths);
            if (handledIdentifierPaths.Contains(columnDescriptor.IdPath))
            {
                return;
            }
            if (typeof(IDataRow).IsAssignableFrom(columnDescriptor.PropertyType))
            {
                indexPropertyDescriptors.Add(new ColumnPropertyDescriptor(columnDescriptor));
            }
            handledIdentifierPaths.Add(columnDescriptor.IdPath);
        }
        
        private void InnerList_ListChanged(object sender, ListChangedEventArgs e)
        {
            _rowIndexes = null;
            var bindingListView = BindingListView;
            if (bindingListView != null)
            {
                bindingListView.InnerList_ListChanged(sender, e);
            }
        }
    }
}
