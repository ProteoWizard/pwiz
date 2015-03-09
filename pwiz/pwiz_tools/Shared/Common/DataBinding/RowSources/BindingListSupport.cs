/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace pwiz.Common.DataBinding.RowSources
{
    public class BindingListSupport<T> : Collection<T>, IBindingList
    {
        private HashSet<ListChangedEventHandler> _eventHandlers;

        public BindingListSupport(IList<T> list) : base(list)
        {
            AllowEdit = true;
        }

        public BindingListSupport()
        {
            AllowEdit = true;
        }
        
        public virtual object AddNew()
        {
            throw new InvalidOperationException();
        }

        public void AddIndex(PropertyDescriptor property)
        {
        }

        public void ApplySort(PropertyDescriptor property, ListSortDirection direction)
        {
        }

        public int Find(PropertyDescriptor property, object key)
        {
            return -1;
        }

        public void RemoveIndex(PropertyDescriptor property)
        {
        }

        public void RemoveSort()
        {
        }

        public bool AllowNew { get; protected set; }
        public bool AllowEdit { get; protected set; }
        public bool AllowRemove { get; protected set; }
        public bool SupportsChangeNotification { get; protected set; }
        public bool SupportsSearching { get; protected set; }
        public bool SupportsSorting { get; protected set; }
        public bool IsSorted { get; protected set; }
        public PropertyDescriptor SortProperty { get; protected set; }
        public ListSortDirection SortDirection { get; protected set; }
        public event ListChangedEventHandler ListChanged
        {
            add
            {
                if (null == _eventHandlers)
                {
                    BeforeFirstListenerAdded();
                    _eventHandlers = new HashSet<ListChangedEventHandler>();
                }
                _eventHandlers.Add(value);
            }
            remove
            {
                if (null != _eventHandlers)
                {
                    _eventHandlers.Remove(value);
                    if (_eventHandlers.Count == 0)
                    {
                        _eventHandlers = null;
                        AfterLastListenerRemoved();
                    }
                }
            }
        }

        protected virtual void BeforeFirstListenerAdded()
        {
        }

        protected virtual void AfterLastListenerRemoved()
        {
        }

        public virtual void FireListChanged(ListChangedEventArgs listChangedEventArgs)
        {
            if (null != _eventHandlers)
            {
                foreach (var eventHandler in _eventHandlers.ToArray())
                {
                    eventHandler(this, listChangedEventArgs);
                }
            }
        }
        protected virtual void OnListChanged(ListChangedEventArgs listChangedEventArgs)
        {
            FireListChanged(listChangedEventArgs);
        }
    }
}
