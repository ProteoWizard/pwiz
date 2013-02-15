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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace pwiz.Common.DataBinding
{
    public class PropertyChangedSupport : INotifyPropertyChanged
    {
        private readonly object _sender;
        private IList<INotifyPropertyChanged> _propertyChangers;
        private HashSet<PropertyChangedEventHandler> _eventHandlers;

        public PropertyChangedSupport(object sender)
        {
            _sender = sender;
        }

        protected PropertyChangedSupport()
        {
            _sender = this;
        }

        [Browsable(false)]
        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                AddPropertyChangedListener(value);
            }
            remove
            {
                RemovePropertyChangedListener(value);
            }
        }

        public void AddPropertyChangedListener(PropertyChangedEventHandler eventHandler)
        {
            lock (this)
            {
                BeforeListenerAdded();
                Debug.Assert(_eventHandlers != null);
                if (!_eventHandlers.Add(eventHandler))
                {
                    throw new InvalidOperationException("Attempt to add same listener twice");
                }
            }
        }

        public void RemovePropertyChangedListener(PropertyChangedEventHandler eventHandler)
        {
            lock (this)
            {
                bool removed = false;
                if (_eventHandlers != null)
                {
                    removed = _eventHandlers.Remove(eventHandler);
                }
                if (!removed)
                {
                    throw new InvalidOperationException("Attempt to remove a listener which has not been added");
                }
                AfterListenerRemoved();
            }
        }

        protected void BeforeListenerAdded()
        {
            if (_eventHandlers == null)
            {
                _eventHandlers = new HashSet<PropertyChangedEventHandler>();
                if (null != _propertyChangers)
                {
                    foreach (var notifyPropertyChanged in _propertyChangers)
                    {
                        notifyPropertyChanged.PropertyChanged += ListenedPropertyChanged;
                    }
                }
                BeforeFirstListenerAdded();
            }
        }
        protected virtual void BeforeFirstListenerAdded()
        {
        }

        protected void AfterListenerRemoved()
        {
            if (_eventHandlers != null && _eventHandlers.Count == 0)
            {
                if (null != _propertyChangers)
                {
                    foreach (var notifyPropertyChanged in _propertyChangers)
                    {
                        notifyPropertyChanged.PropertyChanged -= ListenedPropertyChanged;
                    }
                }
                _eventHandlers = null;
                AfterLastListenerRemoved();
            }
        }

        protected virtual void AfterLastListenerRemoved()
        {
        }

        public virtual void FirePropertyChanged(PropertyChangedEventArgs args)
        {
            IList<PropertyChangedEventHandler> handlers;
            lock (this)
            {
                if (_eventHandlers == null)
                {
                    return;
                }
                handlers = _eventHandlers.ToArray();
            }
            foreach (var handler in handlers)
            {
                handler(_sender, args);
            }
        }

        public void ListenToChanges(INotifyPropertyChanged notifyPropertyChanged)
        {
            if (null == notifyPropertyChanged)
            {
                return;
            }
            lock (this)
            {
                if (null == _propertyChangers)
                {
                    _propertyChangers = new List<INotifyPropertyChanged>();
                }
                _propertyChangers.Add(notifyPropertyChanged);
                if (_eventHandlers != null)
                {
                    notifyPropertyChanged.PropertyChanged += ListenedPropertyChanged;
                }
            }
        }

        private void ListenedPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            FirePropertyChanged(new PropertyChangedEventArgs(null));
        }
    }
}
