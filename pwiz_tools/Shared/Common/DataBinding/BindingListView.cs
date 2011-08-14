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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// DataModel for a list which is being displayed to the user.
    /// Whereas a <see cref="IBindingList"/> models a list of objects,
    /// the BindingListView models a list of objects as well as the sort or
    /// filter that is applied.
    /// The BindingListView also keeps track of which columns the user has chosen
    /// to display (<see cref="ViewSpec"/>) and how they have reorderd the
    /// columns in a DataGridView.
    /// The BindingListView works best if the data is being displayed in a 
    /// <see cref="BoundDataGridView"/>.
    /// </summary>
    [DebuggerTypeProxy(typeof(object))]
    public class BindingListView : BindingList<RowItem>, IBindingListView, ITypedList
    {
        private Func<RowItem, bool> _filterPredicate = (rowItem)=>true;
        private ViewInfo _viewInfo;
        private string[] _columnDisplayOrder;
        private BindingListEventHandler _bindingListEventHandler;
        private Pivoter _pivoter;
        private IList<RowItem> _unfilteredItems;

        
        public BindingListView(ViewInfo viewInfo, IList innerList)
        {
            ViewInfo = viewInfo;
            InnerList = innerList;
            _pivoter = new Pivoter(viewInfo);
            UnfilteredItems = _pivoter.ExpandAndPivot(innerList.Cast<object>().Select(o => new RowItem(null, o))).ToArray();
        }

        public DataSchema DataSchema { get { return ViewInfo.DataSchema; } }

        public ViewInfo ViewInfo 
        { 
            get
            {
                return _viewInfo;
            }
            private set
            {
                _viewInfo = value;
            }
        }

        public string ViewName
        {
            get
            {
                return _viewInfo.Name;
            }
        }

        public ViewSpec GetViewSpec()
        {
            var columnSpecs = ViewInfo.ColumnDescriptors.Select(cd => cd.GetColumnSpec()).ToArray();
            if (_columnDisplayOrder != null)
            {
                int lastDisplayIndex = -1;
                var nameToDisplayIndex = new Dictionary<string, int>();
                _columnDisplayOrder.Select((name, index) => nameToDisplayIndex[name]=index);
                var nameToIndex = new Dictionary<string, int>();
                var displayIndexes = new int[columnSpecs.Length];

                for (int i = 0; i < columnSpecs.Count(); i++)
                {
                    var columnSpec = columnSpecs[i];
                    nameToIndex.Add(columnSpec.Name, i);
                    int displayIndex;
                    if (nameToDisplayIndex.TryGetValue(columnSpec.Name, out displayIndex))
                    {
                        lastDisplayIndex = displayIndex;
                    }
                    displayIndexes[i] = lastDisplayIndex;
                }
                Array.Sort(columnSpecs, (c1, c2) =>
                {
                    int index1 = nameToIndex[c1.Name];
                    int index2 = nameToIndex[c2.Name];
                    int result = displayIndexes[index1].CompareTo(displayIndexes[index2]);
                    if (result == 0)
                    {
                        result = index1.CompareTo(index2);
                    }
                    return result;
                });
            }
            return new ViewSpec()
                .SetName(ViewInfo.Name)
                .SetColumns(columnSpecs)
                .SetSublistId(ViewInfo.SublistId);
        }

        public void ApplySort(ListSortDescriptionCollection sorts)
        {
            DoSort(sorts);
        }

        protected void DoSort(ListSortDescriptionCollection sorts)
        {
            if (sorts.Count == 0)
            {
                return;
            }
            var sortRows = Items.Select((item, index) => new SortRow(DataSchema, sorts, item, index)).ToArray();
            Array.Sort(sortRows);
            Items.Clear();
            foreach (var row in sortRows)
            {
                Items.Add(row.RowItem);
            }
            SortDescriptions = sorts;
            ResetBindings();
        }

        public void RemoveFilter()
        {
//            if (_unfilteredItems == null)
//            {
//                return;
//            }
//            Items.Clear();
//            foreach (var item in _unfilteredItems)
//            {
//                Items.Add(item);
//            }
//            _unfilteredItems = null;
        }

        public IList<RowItem> UnfilteredItems
        {
            get
            {
                return _unfilteredItems;
            }
            set
            {
                _unfilteredItems = value;
                foreach (var rowItem in UnfilteredItems)
                {
                    Items.Add(rowItem);
                }
                ResetBindings();
            }
        }

        public string Filter
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public void SetFilterPredicate(Func<object, bool> filterPredicate, IEnumerable<object> items)
        {
            
        }

        
        public Func<RowItem, bool> FilterPredicate { 
            get
            {
                return _filterPredicate;
            } 
            set
            {
                if (Equals(_filterPredicate, value))
                {
                    return;
                }
                _filterPredicate = value;
                ResetBindings();
            }
        }

        public ListSortDescriptionCollection SortDescriptions
        {
            get; protected set;
        }

        public virtual bool SupportsAdvancedSorting
        {
            get { return true; }
        }

        public virtual bool SupportsFiltering
        {
            get { return true; }
        }

        protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
        {
            ApplySort(new ListSortDescriptionCollection(new[] {new ListSortDescription(prop, direction)}));
        }

        protected override int FindCore(PropertyDescriptor prop, object key)
        {
            throw new NotImplementedException();
        }

        protected override bool IsSortedCore
        {
            get { return SortDescriptions != null && SortDescriptions.Count > 0; }
        }

        protected override void RemoveSortCore()
        {
            ApplySort(new ListSortDescriptionCollection());
        }

        protected override ListSortDirection SortDirectionCore
        {
            get 
            { 
                if (SortDescriptions == null || SortDescriptions.Count == 0)
                {
                    return ListSortDirection.Ascending;
                }
                return SortDescriptions[0].SortDirection;
            }
        }

        protected override PropertyDescriptor SortPropertyCore
        {
            get { 
                if (SortDescriptions == null || SortDescriptions.Count == 0)
                {
                    return null;
                }
                return SortDescriptions[0].PropertyDescriptor;
            }
        }

        protected override bool SupportsSearchingCore
        {
            get { return true; }
        }

        protected override bool SupportsSortingCore
        {
            get { return true; }
        }

        private class SortRow : IComparable<SortRow>
        {
            private object[] _keys;
            public SortRow(DataSchema dataSchema, ListSortDescriptionCollection sorts, RowItem rowItem, int rowIndex)
            {
                DataSchema = dataSchema;
                Sorts = sorts;
                RowItem = rowItem;
                OriginalRowIndex = rowIndex;
                _keys = new object[sorts.Count];
                for (int i = 0; i < sorts.Count; i++)
                {
                    _keys[i] = sorts[i].PropertyDescriptor.GetValue(RowItem);
                }
            }
            public DataSchema DataSchema { get; private set; }
            public RowItem RowItem { get; private set; }
            public int OriginalRowIndex { get; private set; }
            public ListSortDescriptionCollection Sorts { get; private set; }
            public int CompareTo(SortRow other)
            {
                for (int i = 0; i < Sorts.Count; i++)
                {
                    var sort = Sorts[i];
                    int result = DataSchema.Compare(_keys[i], other._keys[i]);
                    if (sort.SortDirection == ListSortDirection.Descending)
                    {
                        result = -result;
                    }
                    if (result != 0)
                    {
                        return result;
                    }
                }
                return OriginalRowIndex.CompareTo(other.OriginalRowIndex);
            }
        }

        public string GetListName(PropertyDescriptor[] listAccessors)
        {
            return ViewInfo.ParentColumn.Name;
        }

        public PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[] listAccessors)
        {
            var propertyDescriptors = new List<PropertyDescriptor>();
            var pivotColumns = new Dictionary<RowKey, List<ColumnDescriptor>>();
            foreach (var columnDescriptor in ViewInfo.ColumnDescriptors)
            {
                var pivotValues = _pivoter.GetPivotValues(columnDescriptor.IdPath);
                if (pivotValues == null)
                {
                    propertyDescriptors.Add(new ColumnPropertyDescriptor(columnDescriptor, null));
                    continue;
                }
                List<ColumnDescriptor> columns;
                foreach (var value in pivotValues)
                {
                    if (!pivotColumns.TryGetValue(value, out columns))
                    {
                        columns = new List<ColumnDescriptor>();
                        pivotColumns.Add(value, columns);
                    }
                    columns.Add(columnDescriptor);
                }
            }
            var pivotKeys = pivotColumns.Keys.ToArray();
            Array.Sort(pivotKeys, RowKey.GetComparison(DataSchema, pivotColumns.Keys.Select(rk=>rk.IdentifierPath)));
            foreach (var pivotKey in pivotKeys)
            {
                propertyDescriptors.AddRange(pivotColumns[pivotKey].Select(cd=>new ColumnPropertyDescriptor(cd, pivotKey)).ToArray());
            }
            return new PropertyDescriptorCollection(propertyDescriptors.ToArray());
        }

        public void SetColumnDisplayOrder(IEnumerable<string> columnDisplayOrder)
        {
            _columnDisplayOrder = columnDisplayOrder == null ? null : columnDisplayOrder.ToArray();
        }

        public IList InnerList
        {
            get; private set;
        }

        private HashSet<ListChangedEventHandler> _listChangedEventHandlers = new HashSet<ListChangedEventHandler>();
        event ListChangedEventHandler IBindingList.ListChanged
        {
            add
            {
                _listChangedEventHandlers.Add(value);
                if (_listChangedEventHandlers.Count > 0 && _bindingListEventHandler == null)
                {
                    _bindingListEventHandler = new BindingListEventHandler(this);
                }
            }
            remove
            {
                _listChangedEventHandlers.Remove(value);
                if (_listChangedEventHandlers.Count == 0 && _bindingListEventHandler != null)
                {
                    _bindingListEventHandler.Dispose();
                    _bindingListEventHandler = null;
                }
            }
        }

        protected override void OnListChanged(ListChangedEventArgs e)
        {
            foreach (var eventHandler in _listChangedEventHandlers.ToArray())
            {
                eventHandler.Invoke(this, e);
            }
        }

        public void SetFilteredItems(IList<RowItem> items)
        {
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }
            ResetBindings();
        }

        public Pivoter Pivoter { get { return _pivoter; } }
    }
}
