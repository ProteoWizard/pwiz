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
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding
{
    public class BindingListEventHandler : IDisposable
    {
        private BindingListView _bindingListView;
        private TreeList<RowItem> _bindingTreeList;
        private IDictionary<object, EntityRow> _entityRows; // Constains IEntity or RedBlackNode
        private IList<ColumnDescriptor> _entityIdColumns;
        private TreeList<object> _innerTreeList;
        
        public BindingListEventHandler(BindingListView bindingListView)
        {
            BindingListView = bindingListView;
            BindingListViewReset();
        }

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
                    BindingListView.ListChanged -= BindingListViewListChanged;
                    BindingListView.DataSchema.DataRowsChanged -= DataSchema_DataRowsChanged;
                    _bindingTreeList = null;
                    var innerBindingList = BindingListView.InnerList as IBindingList;
                    if (innerBindingList != null)
                    {
                        innerBindingList.ListChanged -= InnerBindingList_ListChanged;
                        _innerTreeList = null;
                    }
                    _entityIdColumns.Clear();
                    _entityRows.Clear();

                }
                _bindingListView = value;
                if (BindingListView != null)
                {
                    BindingListView.ListChanged += BindingListViewListChanged;
                    BindingListView.DataSchema.DataRowsChanged += DataSchema_DataRowsChanged;
                    _bindingTreeList = new TreeList<RowItem>(BindingListView);
                    var innerBindingList = BindingListView.InnerList as IBindingList;
                    if (innerBindingList != null)
                    {
                        innerBindingList.ListChanged += InnerBindingList_ListChanged;
                        _innerTreeList = new TreeList<object>(innerBindingList.Cast<object>());
                    }
                    _entityIdColumns = GetIndexPropertyDescriptors(BindingListView.ViewInfo);
                    _entityRows = new Dictionary<object, EntityRow>();
                }
            }
        }

        void DataSchema_DataRowsChanged(object sender, DataRowsChangedEventArgs args)
        {
            var changedRows = new HashSet<RedBlackTree<LongDecimal,RowItem>.Node>();
            foreach (var entity in args.Changed)
            {
                EntityRow entityRow;
                if (_entityRows.TryGetValue(entity, out entityRow))
                {
                    foreach (var childEntry in entityRow.Children)
                    {
                        changedRows.UnionWith(childEntry.Value.Select(row => (RedBlackTree<LongDecimal, RowItem>.Node)row.Data));
                    }
                }
            }
            var rowIndexes = changedRows.Select(row => row.Index).ToArray();
            Array.Sort(rowIndexes);
            Array.Reverse(rowIndexes);
            foreach (var index in rowIndexes)
            {
                BindingListView.ResetItem(index);
            }
        }

        private static IList<ColumnDescriptor> GetIndexPropertyDescriptors(ViewInfo viewInfo)
        {
            var indexPropertyDescriptors = new List<ColumnDescriptor>();
            var handledIds = new HashSet<IdentifierPath>();
            foreach (var columnDescriptor in viewInfo.ColumnDescriptors)
            {
                AddIndexPropertyDescriptors(columnDescriptor, indexPropertyDescriptors, handledIds);
            }
            indexPropertyDescriptors.Sort((c1,c2)=>c1.IdPath.CompareTo(c2.IdPath));
            return indexPropertyDescriptors;
        }

        private static void AddIndexPropertyDescriptors(ColumnDescriptor columnDescriptor, List<ColumnDescriptor> indexPropertyDescriptors, HashSet<IdentifierPath> handledIdentifierPaths)
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
            if (typeof(IEntity).IsAssignableFrom(columnDescriptor.PropertyType))
            {
                indexPropertyDescriptors.Add(columnDescriptor);
            }
            handledIdentifierPaths.Add(columnDescriptor.IdPath);
        }


        void InnerBindingList_ListChanged(object sender, ListChangedEventArgs e)
        {
            switch (e.ListChangedType)
            {
                case ListChangedType.ItemAdded:
                    var newItem = BindingListView.InnerList[e.NewIndex];
                    _innerTreeList.Insert(e.NewIndex, newItem);
                    bool pivotValuesChanged = false;
                    var rowItems =
                        BindingListView.Pivoter.Pivot(BindingListView.Pivoter.Expand(new RowItem(null, newItem)), ref pivotValuesChanged);
                    foreach (var rowItem in rowItems)
                    {
                        if (BindingListView.FilterPredicate.Invoke(rowItem))
                        {
                            BindingListView.Add(rowItem);
                        }
                    }
                    if (pivotValuesChanged)
                    {
                        BindingListView.ResetBindings();
                    }
                    break;
                case ListChangedType.ItemDeleted:
                    InnerListReset();
//                    var oldItem = _innerTreeList.Tree[e.NewIndex];
//                    _innerTreeList.RemoveAt(e.NewIndex);
//                    EntityRow entityRow;
//                    if (_entityRows.TryGetValue(oldItem, out entityRow))
//                    {
//                        _entityRows.Remove(entityRow);
//                        foreach (var )
//                    }
//                    BindingListView.Remove(oldItem);
                    break;
                case ListChangedType.ItemChanged:
                    InnerListReset();
//                    var changedItem = _innerTreeList[e.NewIndex];
//                    var index = BindingListView.IndexOf(changedItem);
//                    if (index >= 0)
//                    {
//                        BindingListView.ResetItem(index);
//                    }
                    break;
                case ListChangedType.Reset:
                case ListChangedType.ItemMoved:
                    InnerListReset();
                    break;
            }
        }

        void BindingListViewListChanged(object sender, ListChangedEventArgs e)
        {
            switch (e.ListChangedType)
            {
                case ListChangedType.Reset:
                    BindingListViewReset();
                    break;
                case ListChangedType.ItemDeleted:
                    var deletedNode = _bindingTreeList.Tree[e.NewIndex];
                    RemoveNode(deletedNode);
                    _bindingTreeList.RemoveAt(e.NewIndex);
                    break;
                case ListChangedType.ItemAdded:
                    _bindingTreeList.Insert(e.NewIndex, BindingListView[e.NewIndex]);
                    AddNode(_bindingTreeList.Tree[e.NewIndex]);
                    break;
                case ListChangedType.ItemChanged:
                    var changedNode = _bindingTreeList.Tree[e.NewIndex];
                    RemoveNode(changedNode);
                    AddNode(changedNode);
                    break;
                case ListChangedType.ItemMoved:
                    BindingListViewReset();
                    break;
            }
        }

        void BindingListViewReset()
        {
            _entityRows.Clear();
            _bindingTreeList = new TreeList<RowItem>(BindingListView);
            foreach (var node in _bindingTreeList.Tree)
            {
                AddNode(node);
            }
        }
        void InnerListReset()
        {
            _entityRows.Clear();
            _innerTreeList = new TreeList<object>(BindingListView.InnerList.Cast<object>());
            BindingListView.UnfilteredItems =
                _innerTreeList.Tree.Select(redBlackNode => new RowItem(redBlackNode, redBlackNode.Value)).ToArray();
            _bindingTreeList = new TreeList<RowItem>(BindingListView);
            foreach (var node in _bindingTreeList.Tree)
            {
                AddNode(node);
            }
        }

        void AddNode(RedBlackTree<LongDecimal,RowItem>.Node node)
        {
            var entityRow = EntityRow.FromBindingListViewNode(node);
            _entityRows.Add(node, entityRow);
            var rowItem = node.Value;
            while (rowItem != null)
            {
                var innerTreeNode = rowItem.Key as RedBlackTree<LongDecimal, object>.Node;
                if (innerTreeNode != null && ReferenceEquals(innerTreeNode, _innerTreeList.Tree))
                {
                    EntityRow innerListEntityRow;
                    if (!_entityRows.TryGetValue(innerTreeNode, out innerListEntityRow))
                    {
                        innerListEntityRow = EntityRow.FromInnerListNode(innerTreeNode);
                        _entityRows.Add(rowItem.Key, innerListEntityRow);
                    }
                    innerListEntityRow.AddChild(BindingListView.ViewInfo.ParentColumn, entityRow);
                }
                rowItem = rowItem.Parent;
            }
            foreach (var columnDescriptor in _entityIdColumns)
            {
                var value = columnDescriptor.GetPropertyValue((RowItem) node.Value, null);
                var entity = value as IEntity;
                if (entity == null)
                {
                    continue;
                }
                EntityRow parentRow;
                if (!_entityRows.TryGetValue(entity, out parentRow))
                {
                    parentRow = EntityRow.FromEntity(entity);
                    _entityRows.Add(entity, parentRow);
                }
                parentRow.AddChild(columnDescriptor, entityRow);
            }
        }

        void RemoveNode(RedBlackTree<LongDecimal, RowItem>.Node node)
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

        public void Dispose()
        {
            BindingListView = null;
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
    }
}
