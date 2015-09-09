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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using pwiz.Common.DataBinding.Controls;

namespace pwiz.Common.DataBinding.Internal
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
    internal class BindingListView : BindingList<RowItem>, ITypedList, IBindingListView, IRaiseItemChangedEvents, IDisposable
    {
        private readonly HashSet<ListChangedEventHandler> _listChangedEventHandlers = new HashSet<ListChangedEventHandler>();
        private PropertyDescriptorCollection _itemProperties;
        private QueryResults _queryResults;
        private readonly QueryRequestor _queryRequestor;

        public BindingListView(TaskScheduler eventTaskScheduler) : this(eventTaskScheduler, CancellationToken.None)
        {
        }
        
        public BindingListView(TaskScheduler eventTaskScheduler, CancellationToken cancellationToken) : base(new List<RowItem>())
        {
            EventTaskScheduler = eventTaskScheduler;
            CancellationToken = cancellationToken;
            _queryResults = QueryResults.Empty;
            _itemProperties = new PropertyDescriptorCollection(new PropertyDescriptor[0], true);
            _queryRequestor = new QueryRequestor(this);
            AllowNew = AllowRemove = AllowEdit = false;
        }

        protected override object AddNewCore()
        {
            return null;
            //return _bindingListEventHandler.AddNew();
        }

        protected override void RemoveItem(int index)
        {
            // _bindingListEventHandler.RemoveItem(this[index]);
        }

        public ViewInfo ViewInfo
        {
            get { return _queryRequestor.QueryParameters.ViewInfo; }
            set
            {
                _queryRequestor.QueryParameters = _queryRequestor.QueryParameters.SetViewInfo(value);
            }
        }

        public void SetViewAndRows(ViewInfo viewInfo, IEnumerable rows)
        {
            _queryRequestor.SetRowsAndParameters(rows, _queryRequestor.QueryParameters.SetViewInfo(viewInfo));
        }
        public ViewSpec ViewSpec
        {
            get
            {
                // TODO(nicksh):Apply current sort if any.
                return ViewInfo.ViewSpec;
            }
            set
            {
                ViewInfo = new ViewInfo(ViewInfo.ParentColumn, value);
            }
        }

        public IEnumerable RowSource
        {
            get
            {
                return _queryRequestor.RowSource;
            }
            set
            {
                if (ReferenceEquals(RowSource, value))
                {
                    return;
                }
                var bindingList = RowSource as IBindingList;
                if (bindingList != null)
                {
                    bindingList.ListChanged -= RowSourceListChanged;
                }
                _queryRequestor.RowSource = value;
                bindingList = RowSource as IBindingList;
                if (bindingList != null)
                {
                    bindingList.ListChanged += RowSourceListChanged;
                }
            }
        }

        private void RowSourceListChanged(object sender, ListChangedEventArgs args)
        {
            OnAllRowsChanged();
            _queryRequestor.Requery();
        }

        public void ApplySort(ListSortDescriptionCollection sorts)
        {
            _queryRequestor.QueryParameters = _queryRequestor.QueryParameters.SetSortDescriptions(sorts);
            // Fire an event so that the NavBar updates to show that the DataGridView is sorting
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
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
                var sortDescriptions = SortDescriptions;
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
                return _queryRequestor.QueryParameters.SortDescriptions;
            }
        }

        public TaskScheduler EventTaskScheduler { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

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

        private List<RowItem> RowItemList
        {
            get
            {
                return ((List<RowItem>) Items);
            }
        }

        public void UpdateResults()
        {
            if (null == _queryRequestor)
            {
                return;
            }
            if (ReferenceEquals(_queryRequestor.QueryResults,  QueryResults))
            {
                return;
            }
            _queryResults = _queryRequestor.QueryResults;
            bool rowCountChanged = Count != QueryResults.ResultRows.Count;
            RowItemList.Clear();
            RowItemList.AddRange(QueryResults.ResultRows);
            bool propsChanged = false;
            if (_itemProperties == null)
            {
                propsChanged = true;
            }
            else if (!_itemProperties.Cast<PropertyDescriptor>()
                .SequenceEqual(QueryResults.ItemProperties.Cast<PropertyDescriptor>()))
            {
                propsChanged = true;
            }
            _itemProperties = QueryResults.ItemProperties;
            AllowNew = false;
            AllowEdit = true;
            AllowRemove = false;
            if (propsChanged)
            {
                OnListChanged(new ListChangedEventArgs(ListChangedType.PropertyDescriptorChanged, 0));
                ResetBindings();
            }
            else if (rowCountChanged || 0 == Count)
            {
                ResetBindings();
            }
            else
            {
                OnAllRowsChanged();
            }
        }
        public string Filter
        {
            get
            {
                return RowFilter.Text;
            }
            set
            {
                RowFilter = RowFilter.SetText(value, true);
            }
        }

        public RowFilter RowFilter
        {
            get { return QueryResults.Parameters.RowFilter; }
            set
            {
                _queryRequestor.QueryParameters = _queryRequestor.QueryParameters.SetRowFilter(value);
            }
        }

        public QueryResults QueryResults
        {
            get { return _queryResults; }
        }

        public bool IsRequerying
        {
            get { return null == _queryRequestor.QueryResults; }
        }

        /// <summary>
        /// Indicates that this IBindingList will send ListItemChange events 
        /// when properties on items in this list change.
        /// 
        /// If a BindingSource does not think that the elements in the list raises
        /// item change events, the BindingSource will call PropertyDescriptor.AddValueChanged 
        /// for all items in the list, which we do not want it to do.
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
                }
            }
            remove
            {
                lock(_listChangedEventHandlers)
                {
                    _listChangedEventHandlers.Remove(value);
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

        protected void OnAllRowsChanged()
        {
            for (int i = 0; i < Count; i++)
            {
                OnListChanged(new ListChangedEventArgs(ListChangedType.ItemChanged, i));
            }
        }

        public int IndexOf(object value)
        {
            return RowItemList.IndexOf(value as RowItem);
        }

        public void Dispose()
        {
            RowSource = null;
            _queryRequestor.Dispose();
            _queryResults = null;
        }

        public event EventHandler<BindingManagerDataErrorEventArgs> UnhandledExceptionEvent;

        public void OnUnhandledException(Exception exception)
        {
            Trace.TraceError("BindingListView unhandled exception {0}", exception); // Not L10N
            var unhandledExceptionEvent = UnhandledExceptionEvent;
            if (null != unhandledExceptionEvent)
            {
                unhandledExceptionEvent(this, new BindingManagerDataErrorEventArgs(exception));
            }
        }
    }
}