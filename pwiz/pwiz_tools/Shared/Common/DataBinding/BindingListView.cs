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
using System.Windows.Forms;
using pwiz.Common.Collections;

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
    /// When a BindingListView has its Owner set to a non-null control,
    /// the BindingListView populates itself asynchronously.
    /// </summary>
    [DebuggerTypeProxy(typeof(object))]
    public class BindingListView : BindingList<RowItem>, ITypedList, IBindingListView, IRaiseItemChangedEvents
    {
        private readonly BindingListEventHandler _bindingListEventHandler;
        private readonly HashSet<ListChangedEventHandler> _listChangedEventHandlers = new HashSet<ListChangedEventHandler>();
        private PropertyDescriptorCollection _itemProperties;
        private Control _owner;

        public BindingListView() : base(new RowItemList())
        {
            _bindingListEventHandler = new BindingListEventHandler(this);
            _itemProperties = new PropertyDescriptorCollection(new PropertyDescriptor[0], true);
            
            AllowNew = AllowRemove = AllowEdit = false;
        }

        protected override object AddNewCore()
        {
            return _bindingListEventHandler.AddNew();
        }

        protected override void RemoveItem(int index)
        {
            _bindingListEventHandler.RemoveItem(this[index]);
        }

        public ViewInfo ViewInfo
        {
            get { return _bindingListEventHandler.ViewInfo; }
            set
            {
                _bindingListEventHandler.ViewInfo = value;
            }
        }
        public ViewSpec ViewSpec
        {
            get
            {
                // TODO(nicksh):Apply current sort if any.
                return _bindingListEventHandler.ViewInfo.GetViewSpec();
            }
            set
            {
                _bindingListEventHandler.ViewInfo = new ViewInfo(_bindingListEventHandler.ViewInfo.ParentColumn, value);
            }
        }

        public Control Owner
        {
            get
            {
                return _owner;
            }
            set
            {
                if (ReferenceEquals(_owner, value))
                {
                    return;
                }
                if (Owner != null)
                {
                    Owner.HandleCreated -= OwnerHandleCreated;
                    Owner.HandleDestroyed -= OwnerHandleDestroyed;
                }
                _owner = value;
                if (Owner != null)
                {
                    Owner.HandleCreated += OwnerHandleCreated;
                    Owner.HandleDestroyed += OwnerHandleDestroyed;
                    _bindingListEventHandler.IsSingleThreaded = !Owner.IsHandleCreated;
                }
                else
                {
                    _bindingListEventHandler.IsSingleThreaded = true;
                }
            }
        }

        public IEnumerable RowSource
        {
            get
            {
                return _bindingListEventHandler.RowSource;
            }
            set
            {
                _bindingListEventHandler.RowSource = value;
            }
        }

        private void OwnerHandleCreated(object sender, EventArgs args)
        {
            _bindingListEventHandler.IsSingleThreaded = false;
        }

        private void OwnerHandleDestroyed(object sender, EventArgs args)
        {
            _bindingListEventHandler.IsSingleThreaded = true;
        }

        public void ApplySort(ListSortDescriptionCollection sorts)
        {
            _bindingListEventHandler.SortDescriptions = sorts;
        }

        public void RemoveFilter()
        {
            Filter = null;
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
            throw new InvalidOperationException();
        }

        protected override bool IsSortedCore
        {
            get
            {
                var sortDescriptions = _bindingListEventHandler.SortDescriptions;
                return sortDescriptions != null && sortDescriptions.Count > 0;
            }
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
        public ListSortDescriptionCollection SortDescriptions
        {
            get
            {
                return _bindingListEventHandler.SortDescriptions;
            }
        }

        public string GetListName(PropertyDescriptor[] listAccessors)
        {
            if (listAccessors == null || listAccessors.Length == 0)
            {
                return ViewInfo.ParentColumn.PropertyType.FullName;
            }
            return listAccessors[listAccessors.Length - 1].Name;
        }

        public PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[] listAccessors)
        {
            if (listAccessors == null || listAccessors.Length == 0)
            {
                return _itemProperties;
            }
            var propertyDescriptor = listAccessors[listAccessors.Length - 1];
            var collectionInfo = ViewInfo.DataSchema.GetCollectionInfo(propertyDescriptor.PropertyType);
            if (collectionInfo != null)
            {
                return new PropertyDescriptorCollection(ViewInfo.DataSchema.GetPropertyDescriptors(collectionInfo.ElementType).ToArray());
            }
            return new PropertyDescriptorCollection(ViewInfo.DataSchema.GetPropertyDescriptors(propertyDescriptor.PropertyType).ToArray());
        }

        public RowItemList RowItemList
        {
            get
            {
                return ((RowItemList) Items);
            }
        }

        public void ResetList(PropertyDescriptorCollection newItemProperties, IEnumerable<RowItem> newItems)
        {
            RowItemList.Reset(newItems);
            bool propsChanged = true;
            if (_itemProperties != null)
            {
                var oldNameSet = new HashSet<string>(_itemProperties.Cast<PropertyDescriptor>().Select(pd => pd.Name));
                var newNameSet = new HashSet<string>(newItemProperties.Cast<PropertyDescriptor>().Select(pd => pd.Name));
                if (oldNameSet.SetEquals(newNameSet))
                {
                    propsChanged = false;
                }
            }
            _itemProperties = newItemProperties;
            AllowNew = _bindingListEventHandler.AllowNew;
            AllowEdit = _bindingListEventHandler.AllowEdit;
            AllowRemove = _bindingListEventHandler.AllowRemove;
            if (propsChanged)
            {
                OnListChanged(new ListChangedEventArgs(ListChangedType.PropertyDescriptorChanged, 0));
            }
            ResetBindings();
        }
        public string Filter
        {
            get
            {
                return RowFilter.Text;
            }
            set
            {
                RowFilter = new RowFilter(value, RowFilter.CaseSensitive);
            }
        }

        public RowFilter RowFilter
        {
            get { return _bindingListEventHandler.RowFilter; }
            set { _bindingListEventHandler.RowFilter = value; }
        }

        public QueryResults QueryResults
        {
            get
            {
                return _bindingListEventHandler.QueryResults;
            }
        }
        /// <summary>
        /// Indicates that this IBindingList will send ListItemChange events 
        /// when properties on items in this list change.
        /// 
        /// If a BindingSource does not think 
        /// </summary>
        bool IRaiseItemChangedEvents.RaisesItemChangedEvents
        {
            get
            {
                return true;
            }
        }
        event ListChangedEventHandler IBindingList.ListChanged
        {
            add
            {
                lock (_listChangedEventHandlers)
                {
                    _listChangedEventHandlers.Add(value);
                    if (_listChangedEventHandlers.Count > 0)
                    {
                        RowItemList.ListChanged = RowItemListChanged;
                    }
                }
            }
            remove
            {
                lock(_listChangedEventHandlers)
                {
                    _listChangedEventHandlers.Remove(value);
                    if (_listChangedEventHandlers.Count == 0)
                    {
                        RowItemList.ListChanged = null;
                    }
                }
            }

        }

        protected override void OnListChanged(ListChangedEventArgs e)
        {
            IList<ListChangedEventHandler> handlers;
            lock (_listChangedEventHandlers)
            {
                handlers = _listChangedEventHandlers.ToArray();
            }
            foreach (var handler in handlers)
            {
                handler(this, e);
            }
        }

        private void RowItemListChanged(object sender, ListChangedEventArgs listChangedEventArgs)
        {
            OnListChanged(listChangedEventArgs);
        }

        public int IndexOf(object value)
        {
            return RowItemList.IndexOf(value as RowItem);
        }
        public override void EndNew(int itemIndex)
        {
            base.EndNew(itemIndex);
        }
        public override void CancelNew(int itemIndex)
        {
            base.CancelNew(itemIndex);
        }
    }
}
