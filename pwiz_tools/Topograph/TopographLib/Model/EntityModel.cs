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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using pwiz.Common.DataBinding.RowSources;
using pwiz.Topograph.Model.Data;

namespace pwiz.Topograph.Model
{
    public abstract class EntityModel : PropertyChangedSupport, IComparable
    {
        protected EntityModel(Workspace workspace)
        {
            Workspace = workspace;
        }
        [Browsable(false)]
        public Workspace Workspace { get; private set; }
        protected virtual void OnChange()
        {
            FirePropertyChanged(new PropertyChangedEventArgs(null));
        }
        public override void FirePropertyChanged(PropertyChangedEventArgs args)
        {
            if (RaisePropertyChangedEvents)
            {
                base.FirePropertyChanged(args);
            }
        }

        public virtual int CompareTo(object obj)
        {
            if (null == obj)
            {
                return 1;
            }
            return CaseInsensitiveComparer.DefaultInvariant.Compare(ToString(), obj.ToString());
        }

        public static T MergeValue<T>(T myValue, T savedValue, T theirValue)
        {
            if (Equals(myValue, savedValue))
            {
                return theirValue;
            }
            return myValue;
        }

        [Browsable(false)]
        public bool RaisePropertyChangedEvents { get; set; }
        public IDisposable DisablePropertyChangedEvents()
        {
            var result = new RestorePropertyChangedEvents(this, RaisePropertyChangedEvents);
            RaisePropertyChangedEvents = false;
            return result;
        }

        private class RestorePropertyChangedEvents : IDisposable
        {
            private readonly EntityModel _entityModel;
            private bool _restore;
            public RestorePropertyChangedEvents(EntityModel entityModel, bool restore)
            {
                _entityModel = entityModel;
                _restore = restore;
            }
            public void Dispose()
            {
                if (_restore)
                {
                    _restore = false;
                    _entityModel.RaisePropertyChangedEvents = true;
                    _entityModel.FirePropertyChanged(new PropertyChangedEventArgs(null));
                }
            }
        }

        public abstract KeyValuePair<Type, object> GetKey();
    }

    public abstract class EntityModel<TKey, TData> : EntityModel
    {
        private TData _data;

        protected EntityModel(Workspace workspace, TKey key) : this(workspace, key, default(TData))
        {
            _data = GetData(workspace.Data);
        }
        protected EntityModel(Workspace workspace, TKey key, TData data) : base(workspace)
        {
            Key = key;
            _data = data;
        }

        protected TKey Key { get; private set; }
        [Browsable(false)]
        public TData Data
        {
            get { return _data; }
            set
            {
                if (Equals(_data, value))
                {
                    return;
                }
                Workspace.Data = SetData(Workspace.Data, value);
            }
        }
        public virtual void Update(WorkspaceChangeArgs workspaceChange, TData newData)
        {
            var oldData = _data;
            _data = newData;
            if (!Equals(oldData, _data))
            {
                OnChange();
            }
        }

        public abstract TData GetData(WorkspaceData workspaceData);
        public abstract WorkspaceData SetData(WorkspaceData workspaceData, TData value);
        public override KeyValuePair<Type, object> GetKey()
        {
            return new KeyValuePair<Type, object>(GetType(), Key);
        }
    }
}
