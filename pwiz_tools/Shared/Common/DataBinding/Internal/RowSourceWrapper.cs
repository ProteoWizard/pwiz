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
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Internal
{
    internal class RowSourceWrapper
    {
        private HashSet<Action> _eventHandlers;
        private IList<RowItem> _rowItems;

        public RowSourceWrapper(IRowSource list)
        {
            WrappedRowSource = list;
        }

        public IRowSource WrappedRowSource { get; private set; }

        public event Action RowSourceChanged 
        { 
            add
            {
                lock (this)
                {
                    _rowItems = null;
                    if (_eventHandlers == null)
                    {
                        _eventHandlers = new HashSet<Action>();
                        AttachListChanged();
                    }
                    if (!_eventHandlers.Add(value))
                    {
                        throw new InvalidOperationException(@"Listener already added");
                    }
                }
            } 
            remove 
            { 
                lock (this)
                {
                    if (_eventHandlers == null || !_eventHandlers.Remove(value))
                    {
                        throw new InvalidOperationException(@"Listener has not been added");
                    }
                    if (0 == _eventHandlers.Count)
                    {
                        DetachListChanged();
                        _eventHandlers = null;
                    }
                }
            }
        }

        private void OnBindingListChanged()
        {
            IList<Action> handlers;
            lock (this)
            {
                if (null == _eventHandlers)
                {
                    return;
                }
                handlers = _eventHandlers.ToArray();
            }
            foreach (var handler in handlers)
            {
                handler();
            }
        }

        private void AttachListChanged()
        {
            if (null != WrappedRowSource)
            {
                WrappedRowSource.RowSourceChanged += OnBindingListChanged;
            }
        }

        private void DetachListChanged()
        {
            if (null != WrappedRowSource)
            {
                WrappedRowSource.RowSourceChanged -= OnBindingListChanged;
            }
        }

        public IEnumerable<RowItem> ListRowItems()
        {
            lock (this)
            {
                if (_rowItems != null)
                {
                    return _rowItems;
                }
            }
            var rowItems = WrappedRowSource.GetItems().Cast<object>().Select(item => new RowItem(item));
            lock (this)
            {
                rowItems = _rowItems = ImmutableList.ValueOf(rowItems);
            }
            return rowItems;
        }
    }
}
