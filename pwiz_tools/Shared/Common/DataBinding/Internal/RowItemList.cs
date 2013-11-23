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
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Internal
{
    internal class RowItemList : TreeList<RowItem>, IDisposable
    {
        private readonly HashSet<ChangeNotifyRowItem> _rowItemsWithEventHandlers = new HashSet<ChangeNotifyRowItem>();

        public override void Clear()
        {
            var oldValues = this.Cast<ChangeNotifyRowItem>().ToArray();
            base.Clear();
            foreach (var item in oldValues)
            {
                item.RemovedFromTree();
            }
            Debug.Assert(_rowItemsWithEventHandlers.Count == 0);
        }

        public void Dispose()
        {
            Clear();
        }

        public override void RemoveAt(int index)
        {
            var oldValue = this[index];
            base.RemoveAt(index);
            ((ChangeNotifyRowItem)oldValue).RemovedFromTree();
        }

        public override RowItem this[int index]
        {
            get
            {
                return base[index];
            }
            set
            {
                var changeNotifyRowItem = new ChangeNotifyRowItem(this, value);
                base[index] = changeNotifyRowItem;
                changeNotifyRowItem.TreeNode = Tree[index];
            }
        }

        public override void Reset(IEnumerable<RowItem> values)
        {
            Clear();
            base.Reset(values.Select(rowItem => new ChangeNotifyRowItem(this, rowItem)));
            foreach (var node in Tree)
            {
                ((ChangeNotifyRowItem) node.Value).TreeNode = node;
            }
        }

        private class ChangeNotifyRowItem : RowItem
        {
            private IDictionary<PropertyChangeKey, Delegate> _eventHandlers;
            private RowItemList _rowItemList;
            private RedBlackTree<LongDecimal, RowItem>.Node _treeNode;
            public ChangeNotifyRowItem(RowItemList rowItemList, RowItem rowItem) : base(rowItem)
            {
                _rowItemList = rowItemList;
            }
            public RedBlackTree<LongDecimal, RowItem>.Node TreeNode
            {
                get
                {
                    return _treeNode;
                }
                set
                {
                    _treeNode = value;
                }
            }
            public void RemovedFromTree()
            {
                lock (this)
                {
                    RemovePropertyChangeEventHandlers();
                    _rowItemList = null;
                }
                _treeNode = null;
            }
            public override void HookPropertyChange(object component, PropertyDescriptor propertyDescriptor)
            {
                if (null == component)
                {
                    return;
                }
                var notifyPropertyChange = component as INotifyPropertyChanged;
                PropertyChangeKey propertyChangeKey;
                if (notifyPropertyChange != null)
                {
                    propertyChangeKey = new PropertyChangeKey(component, null);
                }
                else
                {
                    if (propertyDescriptor == null || !propertyDescriptor.SupportsChangeEvents)
                    {
                        return;
                    }
                    propertyChangeKey = new PropertyChangeKey(component, propertyDescriptor);
                }
                lock (this)
                {
                    if (_rowItemList == null)
                    {
                        return;
                    }
                    if (_eventHandlers == null)
                    {
                        _eventHandlers = new Dictionary<PropertyChangeKey, Delegate>();
                        lock (_rowItemList._rowItemsWithEventHandlers)
                        {
                            Debug.Assert(!_rowItemList._rowItemsWithEventHandlers.Contains(this));
                            _rowItemList._rowItemsWithEventHandlers.Add(this);
                        }
                    }
                    if (_eventHandlers.ContainsKey(propertyChangeKey))
                    {
                        return;
                    }

                    Delegate eventHandler;
                    if (notifyPropertyChange != null)
                    {
                        Debug.Assert(propertyChangeKey.ComponentProperty == null);
                        var propertyHandler = new PropertyChangedEventHandler(OnPropertyChanged);
                        eventHandler = propertyHandler;
                        notifyPropertyChange.PropertyChanged += propertyHandler;
                    }
                    else
                    {
                        var propertyHandler = new EventHandler(OnPropertyChanged);
                        propertyDescriptor.AddValueChanged(component, propertyHandler);
                        eventHandler = propertyHandler;
                    }
                    _eventHandlers.Add(propertyChangeKey, eventHandler);
                }
            }

            private void OnPropertyChanged(object sender, EventArgs eventArgs)
            {
                var rowItemList = _rowItemList;
                var treeNode = _treeNode;
                if (rowItemList == null || treeNode == null)
                {
                    return;
                }
                RemovePropertyChangeEventHandlers();
                rowItemList.OnListChanged(new ListChangedEventArgs(ListChangedType.ItemChanged, treeNode.Index));
            }
            public void RemovePropertyChangeEventHandlers()
            {
                IDictionary<PropertyChangeKey, Delegate> handlers;
                lock(this)
                {
                    if (_eventHandlers == null)
                    {
                        return;
                    }
                    lock (_rowItemList._rowItemsWithEventHandlers)
                    {
                        Debug.Assert(_rowItemList._rowItemsWithEventHandlers.Contains(this));
                        _rowItemList._rowItemsWithEventHandlers.Remove(this);
                    }
                    handlers = _eventHandlers;
                    _eventHandlers = null;
                }
                foreach (var entry in handlers)
                {
                    if (entry.Key.ComponentProperty == null)
                    {
                        ((INotifyPropertyChanged) entry.Key.Component).PropertyChanged -= (PropertyChangedEventHandler) entry.Value;                            
                    }
                    else
                    {
                        entry.Key.ComponentProperty.RemoveValueChanged(entry.Key.Component, (EventHandler) entry.Value);
                    }
                }
            }
            public bool IsInList(RowItemList rowItemList)
            {
                return ReferenceEquals(_rowItemList, rowItemList);
            }
        }

        class PropertyChangeKey
        {
            public PropertyChangeKey(object component, PropertyDescriptor componentProperty)
            {
                Component = component;
                ComponentProperty = componentProperty;
            }
            public object Component { get; private set; }
            public PropertyDescriptor ComponentProperty { get; private set; }
            public override int GetHashCode()
            {
                int result = RuntimeHelpers.GetHashCode(Component);
                result = result*397 ^ (ComponentProperty == null ? 0 : ComponentProperty.GetHashCode());
                return result;
            }
            public override bool Equals(object obj)
            {
                return Equals(obj as PropertyChangeKey);
            }

// ReSharper disable once MemberCanBePrivate.Local
            public bool Equals(PropertyChangeKey that)
            {
                if (null == that)
                {
                    return false;
                }
                if (ReferenceEquals(this, that))
                {
                    return true;
                }
                return ReferenceEquals(Component, that.Component)
                       && Equals(ComponentProperty, that.ComponentProperty);
            }
        }

        private ListChangedEventHandler _listChangedEventHandler;
        public ListChangedEventHandler ListChanged
        {
            get
            {
                return _listChangedEventHandler;
            }
            set
            {
                _listChangedEventHandler = value;
                if (_listChangedEventHandler == null)
                {
                    IList<ChangeNotifyRowItem> rowItems;
                    lock (_rowItemsWithEventHandlers)
                    {
                        rowItems = _rowItemsWithEventHandlers.ToArray();
                    }
                    foreach (var row in rowItems)
                    {
                        row.RemovePropertyChangeEventHandlers();
                    }
                }
            }
        }
        public void OnListChanged(ListChangedEventArgs listChangedEventArgs)
        {
            var eventHandler = _listChangedEventHandler;
            if (eventHandler != null)
            {
                eventHandler(this, listChangedEventArgs);
            }
        }

        public override int IndexOf(RowItem rowItem)
        {
            var changeNotifyRowItem = rowItem as ChangeNotifyRowItem;
            if (changeNotifyRowItem != null && changeNotifyRowItem.IsInList(this))
            {
                return changeNotifyRowItem.TreeNode.Index;
            }
            return base.IndexOf(rowItem);
        }

        public override void Insert(int index, RowItem item)
        {
            var changeNotifyRowItem = new ChangeNotifyRowItem(this, item);
            base.Insert(index, changeNotifyRowItem);
            changeNotifyRowItem.TreeNode = Tree[index];
        }
    }
}
