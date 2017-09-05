/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding.RowSources;

namespace pwiz.Common.DataBinding
{
    public interface IRowSource
    {
        IEnumerable GetItems();
        event Action RowSourceChanged;
    }

    public class StaticRowSource : IRowSource
    {
        public static readonly StaticRowSource EMPTY = new StaticRowSource(new object[0]);
        private readonly IEnumerable _items;
        public StaticRowSource(IEnumerable items)
        {
            _items = items;
        }

        public IEnumerable GetItems()
        {
            return _items;
        }

        public event Action RowSourceChanged
        {
            add
            {
            }
            remove
            {
            }
        }
    }

    public abstract class AbstractRowSource : IRowSource
    {
        private HashSet<Action> _eventHandlers;

        public abstract IEnumerable GetItems();

        public event Action RowSourceChanged
        {
            add
            {
                lock (this)
                {
                    if (_eventHandlers == null)
                    {
                        BeforeFirstListenerAdded();
                        _eventHandlers = new HashSet<Action>();
                    }
                    _eventHandlers.Add(value);
                }
            }
            remove
            {
                lock (this)
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

        protected void FireListChanged()
        {
            Action[] eventHandlers;
            lock (this)
            {
                if (null == _eventHandlers)
                {
                    return;
                }
                eventHandlers = _eventHandlers.ToArray();
            }
            foreach (var eventHandler in eventHandlers)
            {
                eventHandler();
            }
        }
    }

    public static class BindingListRowSource
    {
        public static IRowSource Create<T>(T listChanged) where T : IEnumerable, IListChanged
        {
            return new Impl(listChanged, handler=>listChanged.ListChanged+= handler, handler=>listChanged.ListChanged -= handler);
        }

        public static IRowSource Create<T>(BindingList<T> bindingList)
        {
            return new Impl(bindingList, handler=>bindingList.ListChanged += handler, handler=>bindingList.ListChanged -= handler);
        }

        private class Impl : AbstractRowSource
        {
            private readonly IEnumerable _items;
            private Action<ListChangedEventHandler> _addAction;
            private Action<ListChangedEventHandler> _removeAction;

            public Impl(IEnumerable items, Action<ListChangedEventHandler> addAction, Action<ListChangedEventHandler> removeAction)
            {
                _items = items;
                _addAction = addAction;
                _removeAction = removeAction;
            }

            public override IEnumerable GetItems()
            {
                return _items;
            }

            protected override void AfterLastListenerRemoved()
            {
                _removeAction(OnListChanged);
            }

            protected override void BeforeFirstListenerAdded()
            {
                _addAction(OnListChanged);
            }

            void OnListChanged(object sender, ListChangedEventArgs e)
            {
                FireListChanged();
            }
        }
    }
}
