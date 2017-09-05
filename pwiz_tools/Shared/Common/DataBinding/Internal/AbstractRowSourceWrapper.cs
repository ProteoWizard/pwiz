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

namespace pwiz.Common.DataBinding.Internal
{
    internal abstract class AbstractRowSourceWrapper : IRowSourceWrapper
    {
        private HashSet<Action> _eventHandlers;
        protected AbstractRowSourceWrapper(IRowSource list)
        {
            WrappedRowSource = list;
        }
        public event Action RowSourceChanged 
        { 
            add
            {
                lock (this)
                {
                    if (_eventHandlers == null)
                    {
                        _eventHandlers = new HashSet<Action>();
                        AttachListChanged();
                    }
                    if (!_eventHandlers.Add(value))
                    {
                        throw new InvalidOperationException("Listener already added"); // Not L10N
                    }
                }
            } 
            remove 
            { 
                lock (this)
                {
                    if (_eventHandlers == null || !_eventHandlers.Remove(value))
                    {
                        throw new InvalidOperationException("Listener has not been added"); // Not L10N
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

        public IRowSource WrappedRowSource { get; private set; }
        public abstract IEnumerable<RowItem> ListRowItems();

        public abstract void StartQuery(IQueryRequest queryRequest);

        public abstract QueryResults MakeLive(QueryResults rowItems);
    }
}