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
using System.Linq;
using log4net;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Handles events that come from a <see cref="IBindingList" />
    /// 
    /// </summary>
    internal class BindingListEventHandler
    {
        private static readonly ILog Log
            = LogManager.GetLogger(typeof (BindingListEventHandler));
        private bool _singleThreaded;

        private IEnumerable _rowSource;
        private IBindingList _innerBindingList;
        private TreeList<object> _innerTreeList;

        private QueryResults _queryResults = QueryResults.Empty;
        private QueryWorker _queryWorker;

        private ListIndex _listIndex;
        private ListIndexer _listIndexer;

        private bool _inAddNew;
        private int _addNewIndex;

        public BindingListEventHandler(BindingListView bindingListView)
        {
            BindingListView = bindingListView;
            _queryResults = QueryResults.Empty;
            IsSingleThreaded = true;
        }

        public bool IsSingleThreaded
        {
            get
            {
                return _singleThreaded;
            }
            set
            {
                if (Equals(_singleThreaded, value))
                {
                    return;
                }
                _singleThreaded = value;
            }
        }

        public ViewInfo ViewInfo 
        { 
            get
            {
                return QueryParameters.ViewInfo;
            }
            set
            {
                lock (this)
                {
                    QueryParameters = QueryParameters.SetViewInfo(value);
                }
            }
        }
        public ListSortDescriptionCollection SortDescriptions
        {
            get
            {
                return QueryParameters.SortDescriptions;
            }
            set
            {
                lock(this)
                {
                    QueryParameters = QueryParameters.SetSortDescriptions(value);
                }
            }
        }

        private ListIndex GetListIndex()
        {
            var listIndex = _listIndex;
            if (listIndex != null || _listIndexer != null)
            {
                return listIndex;
            }
            var listIndexer = new ListIndexer(this);
            lock(this)
            {
                if (_listIndexer == null)
                {
                    new Action(listIndexer.BuildIndex).BeginInvoke(null, null);
                    _listIndexer = listIndexer;
                }
            }
            return _listIndex;
        }

        private void InvokeIfRequired(Action action)
        {
            if (IsSingleThreaded || !BindingListView.Owner.InvokeRequired)
            {
                action();
                return;
            }
            BindingListView.Owner.BeginInvoke(action);
        }

        public BindingListView BindingListView
        {
            get; private set;
        }

        public IEnumerable RowSource
        {
            get
            {
                return _rowSource;
            }
            set
            {
                lock (this)
                {
                    if (ReferenceEquals(RowSource, value))
                    {
                        return;
                    }
                    if (_innerBindingList != null)
                    {
                        _innerBindingList.ListChanged -= InnerBindingList_ListChanged;
                    }
                    _rowSource = value;
                    _innerBindingList = _rowSource as IBindingList;
                    if (_innerBindingList != null)
                    {
                        _innerBindingList.ListChanged += InnerBindingList_ListChanged;
                    }
                    ResetRowSource();
                }
            }
        }

        void InnerBindingList_ListChanged(object sender, ListChangedEventArgs e)
        {
            Log.DebugFormat("ListChangedType: {0} NewIndex: {1} OldIndex: {2}", 
                e.ListChangedType, e.NewIndex, e.OldIndex);
            if (!ReferenceEquals(sender, _innerBindingList))
            {
                Log.Warn("Event received from wrong list");
                return;
            }
            switch (e.ListChangedType)
            {
                case ListChangedType.ItemAdded:
                    if (_inAddNew)
                    {
                        _addNewIndex = e.NewIndex;
                        Log.DebugFormat("AddNewIndex:{0}", _addNewIndex);
                    }
                    _innerTreeList.Insert(e.NewIndex, _innerBindingList[e.NewIndex]);
                    RepivotBindingList();
                    break;
                case ListChangedType.ItemDeleted:
                    _innerTreeList.RemoveAt(e.NewIndex);
                    RepivotBindingList();
                    break;
                case ListChangedType.ItemChanged:
                    RepivotBindingList();
                    break;
                case ListChangedType.Reset:
                case ListChangedType.ItemMoved:
                    ResetRowSource();
                    break;
            }
        }

        private void RepivotBindingList()
        {
            lock (this)
            {
                var newRows =
                    Array.AsReadOnly(_innerTreeList.Tree.Select(node => new RowItem(node, node.Value)).ToArray());
                QueryParameters = QueryParameters.SetRows(newRows);
            }
        }

        void ResetRowSource()
        {
            if (_innerBindingList != null)
            {
                _innerTreeList = new TreeList<object>(_innerBindingList.Cast<object>());
                RepivotBindingList();
                return;
            }
            IList<RowItem> newRows;
            _innerTreeList = null;
            if (_rowSource != null)
            {
                newRows = Array.AsReadOnly(_rowSource.Cast<object>().Select(o => new RowItem(null, o)).ToArray());
            }
            else
            {
                newRows = new RowItem[0];
            }
            lock (this)
            {
                QueryParameters = QueryParameters.SetRows(newRows);
            }
        }

        public class EntityRow
        {
            public static EntityRow FromBindingListViewNode(RedBlackTree<LongDecimal, RowItem>.Node bindingListViewNode)
            {
                return new EntityRow(bindingListViewNode);
            }
            public static EntityRow FromInnerListNode(RedBlackTree<LongDecimal, object>.Node innerListNode)
            {
                return new EntityRow(innerListNode);
            }
            public static EntityRow FromEntity(IEntity entity)
            {
                return new EntityRow(entity);
            }
            private EntityRow(object value)
            {
                Data = value;
                Parents = new Dictionary<ColumnDescriptor, EntityRow>();
                Children = new Dictionary<ColumnDescriptor, HashSet<EntityRow>>();
            }
            public object Data { get; private set;}
            public IDictionary<ColumnDescriptor, EntityRow> Parents { get; private set; }
            public IDictionary<ColumnDescriptor, HashSet<EntityRow>> Children { get; private set; }
            public void AddChild(ColumnDescriptor columnDescriptor, EntityRow child)
            {
                HashSet<EntityRow> children;
                if (!Children.TryGetValue(columnDescriptor, out children))
                {
                    children = new HashSet<EntityRow>();
                    Children.Add(columnDescriptor, children);
                }
                if (children.Add(child))
                {
                    child.Parents.Add(columnDescriptor, this);
                }
            }
        }

        public bool AllowNew
        {
            get 
            {
                return _innerBindingList != null && _innerBindingList.AllowNew;
            }
        }
        public bool AllowEdit
        {
            get
            {
                if (_innerBindingList != null)
                {
                    return _innerBindingList.AllowEdit;
                }
                return true;
            }
        }
        public bool AllowRemove
        {
            get
            {
                return _innerBindingList != null && _innerBindingList.AllowRemove;
            }
        }

        class ListIndex
        {
            private ViewInfo _viewInfo;
            private RedBlackTree<LongDecimal, object> _innerTree;
            private IDictionary<object, EntityRow> _entityRows;
            public ListIndex(ViewInfo viewInfo, RedBlackTree<LongDecimal, object> innerTree)
            {
                _viewInfo = viewInfo;
                _innerTree = innerTree;
                _entityRows = new Dictionary<object, EntityRow>();
            }
            public void AddNode(RedBlackTree<LongDecimal, RowItem>.Node node)
            {
                var entityRow = EntityRow.FromBindingListViewNode(node);
                _entityRows.Add(node, entityRow);
                var rowItem = node.Value;
                while (rowItem != null)
                {
                    var innerTreeNode = rowItem.Key as RedBlackTree<LongDecimal, object>.Node;
                    if (innerTreeNode != null && innerTreeNode.ComesFromTree(_innerTree))
                    {
                        EntityRow innerListEntityRow;
                        if (!_entityRows.TryGetValue(innerTreeNode, out innerListEntityRow))
                        {
                            innerListEntityRow = EntityRow.FromInnerListNode(innerTreeNode);
                            _entityRows.Add(rowItem.Key, innerListEntityRow);
                        }
                        innerListEntityRow.AddChild(_viewInfo.ParentColumn, entityRow);
                    }
                    rowItem = rowItem.Parent;
                }
            }
            public void RemoveNode(RedBlackTree<LongDecimal, RowItem>.Node node)
            {
                var entityRow = _entityRows[node];
                foreach (var parentEntry in entityRow.Parents)
                {
                    var children = parentEntry.Value.Children[parentEntry.Key];
                    children.Remove(entityRow);
                    if (children.Count == 0)
                    {
                        parentEntry.Value.Children.Remove(parentEntry.Key);
                    }
                    if (parentEntry.Value.Children.Count == 0)
                    {
                        _entityRows.Remove(parentEntry.Value.Data);
                    }
                }
            }
            public void AddAffectedRows(HashSet<RedBlackTree<LongDecimal, RowItem>.Node> nodes, IEnumerable<RedBlackTree<LongDecimal, object>.Node> innerTreeNodes)
            {
                foreach (var node in innerTreeNodes)
                {
                    EntityRow entityRow;
                    if (_entityRows.TryGetValue(node, out entityRow))
                    {
                        foreach (var childEntry in entityRow.Children)
                        {
                            nodes.UnionWith(childEntry.Value.Select(row => (RedBlackTree<LongDecimal, RowItem>.Node)row.Data));
                        }
                    }
                }
            }
        }

        class ListIndexer : MustDispose
        {
            private BindingListEventHandler _bindingListEventHandler;
            private ViewInfo _viewInfo;
            private RedBlackTree<LongDecimal, RowItem>.Node[] _treeNodes;
            public ListIndexer(BindingListEventHandler bindingListEventHandler)
            {
                _bindingListEventHandler = bindingListEventHandler;
                _viewInfo = bindingListEventHandler.ViewInfo;
                _treeNodes = _bindingListEventHandler.BindingListView.RowItemList.Tree.ToArray();
            }

            public void BuildIndex()
            {
                try
                {
                    var innerTree = _bindingListEventHandler._innerTreeList == null ? null : _bindingListEventHandler._innerTreeList.Tree;
                    var listIndex = new ListIndex(_viewInfo, innerTree);
                    foreach (var node in _treeNodes)
                    {
                        CheckDisposed();
                        listIndex.AddNode(node);
                    }
                    lock (_bindingListEventHandler)
                    {
                        if (this == _bindingListEventHandler._listIndexer)
                        {
                            _bindingListEventHandler._listIndex = listIndex;
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    // ignore
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
                finally
                {
                    lock (_bindingListEventHandler)
                    {
                        if (this == _bindingListEventHandler._listIndexer)
                        {
                            _bindingListEventHandler._listIndexer = null;
                        }
                    }
                }
            }
        }

        public QueryParameters QueryParameters
        {
            get
            {
                return QueryResults.Parameters;
            }
            set
            {
                lock(this)
                {
                    QueryResults = QueryResults.SetParameters(value);
                }
            }
        }
        public QueryResults QueryResults
        {
            get
            {
                return _queryResults;
            }
            private set
            {
                lock(this)
                {
                    bool resultsChanged = !ReferenceEquals(value.ResultRows, QueryResults.ResultRows);
                    _queryResults = value;
                    if (resultsChanged)
                    {
                        _listIndex = null;
                        if (_listIndexer != null)
                        {
                            _listIndexer.Dispose();
                            _listIndexer = null;
                        }
                        InvokeIfRequired(EnsureQueryRunning);
                    }
                }
            }
        }
        public void SetQueryResults(QueryResults newResults)
        {
            lock (this)
            {
                QueryResults = newResults.SetParameters(QueryParameters);
            }
        }
        public IList<RowItem> UnfilteredRows
        {
            get
            {
                return _queryResults.PivotedRows;
            }
        }
        public bool IsComplete
        {
            get
            {
                return _queryWorker != null;
            }
        }
        public RowFilter RowFilter
        {
            get
            {
                return QueryParameters.RowFilter;
            }
            set
            {
                lock(this)
                {
                    QueryParameters = QueryParameters.SetRowFilter(value);
                }
            }
        }
        private void EnsureQueryRunning()
        {
            lock (this)
            {
                if (QueryResults.IsComplete)
                {
                    if (_queryWorker != null)
                    {
                        _queryWorker.Dispose();
                        _queryWorker = null;
                    }
                    BindingListView.ResetList(QueryResults.ItemProperties, QueryResults.ResultRows);
                    return;
                }
                if (_queryWorker != null)
                {
                    return;
                }
                _queryWorker = new QueryWorker(this);
                if (IsSingleThreaded)
                {
                    RunQueryBackground();
                    return;
                }
                new Action(RunQueryBackground).BeginInvoke(null, null);
            }
        }
        private void RunQueryBackground()
        {
            var singleThreaded = IsSingleThreaded;
            while (true)
            {
                QueryWorker queryWorker;
                lock (this)
                {
                    if (singleThreaded != IsSingleThreaded)
                    {
                        return;
                    }
                    queryWorker = _queryWorker;
                }
                if (queryWorker == null)
                {
                    return;
                }
                try
                {
                    var results = queryWorker.DoWork();
                    SetQueryResults(results);
                }
                catch (ObjectDisposedException)
                {
                    // ignore
                }
            }
        }

        public RowItem AddNew()
        {
            try
            {
                lock (this)
                {
                    _addNewIndex = -1;
                    _inAddNew = true;
                    object o = _innerBindingList.AddNew();
                    if (_addNewIndex == -1)
                    {
                        throw new InvalidOperationException();
                    }
                    var node = _innerTreeList.Tree[_addNewIndex];
                    var pivoter = new Pivoter(QueryParameters.ViewInfo);
                    var newRows = pivoter.ExpandAndPivot(new[] { new RowItem(node, node.Value) }).ToArray();
                    if (newRows.Length != 1)
                    {
                        throw new InvalidOperationException();
                    }
                    BindingListView.Add(newRows[0]);
                    return BindingListView[BindingListView.Count - 1];
                }
            }
            finally
            {
                _inAddNew = false;
            }
        }

        public void RemoveItem(RowItem rowItem)
        {
            var innerTreeNode = rowItem.Key as RedBlackTree<LongDecimal, object>.Node;
            if (innerTreeNode == null)
            {
                throw new InvalidOperationException();
            }
            _innerBindingList.RemoveAt(innerTreeNode.Index);
        }
    }
}
