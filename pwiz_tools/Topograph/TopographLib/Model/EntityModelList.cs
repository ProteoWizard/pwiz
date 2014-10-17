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
using System.Linq;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.RowSources;
using pwiz.Topograph.Model.Data;

namespace pwiz.Topograph.Model
{
    public abstract class EntityModelList<TKey, TData, TEntity> : ICloneableList<TKey, TEntity>, IListChanged where TEntity : EntityModel<TKey, TData>
    {
        private ImmutableSortedList<TKey, TEntity> _entityList;
        protected EntityModelList(Workspace workspace)
        {
            Workspace = workspace;
        }

        public Workspace Workspace { get; private set; }
        protected abstract ImmutableSortedList<TKey, TEntity> CreateEntityList();
        protected ImmutableSortedList<TKey, TEntity> EnsureEntityList()
        {
            if (null == _entityList)
            {
                if (null == GetData(Workspace.Data))
                {
                    return ImmutableSortedList<TKey, TEntity>.EMPTY;
                }
                _entityList = CreateEntityList();
            }
            return _entityList;
        }
        public bool TryGetValue(TKey key, out TEntity entity)
        {
            return EnsureEntityList().TryGetValue(key, out entity);
        }

        public virtual void Update(WorkspaceChangeArgs workspaceChange)
        {
            if (null == _entityList)
            {
                return;
            }
            var newData = GetData(Workspace.Data) ?? ImmutableSortedList<TKey, TData>.EMPTY;
            var newKeys = new HashSet<TKey>(newData.Keys);
            bool changed = !Equals(newData.Keys, _entityList.Keys);
            if (changed)
            {
                var entities = new Dictionary<TKey, TEntity>(_entityList.AsDictionary());
                foreach (var key in newKeys)
                {
                    TEntity entity;
                    if (!entities.TryGetValue(key, out entity))
                    {
                        TData itemData;
                        newData.TryGetValue(key, out itemData);
                        entities.Add(key, CreateEntityForKey(key, itemData));
                    }
                }
                _entityList = ImmutableSortedList.FromValues(entities, _entityList.KeyComparer);
            }
            foreach (var entity in _entityList.Values)
            {
                TData itemData;
                var key = GetKey(entity);
                newData.TryGetValue(key, out itemData);
                entity.Update(workspaceChange, itemData);
            }
            if (changed)
            {
                var listChanged = ListChanged;
                if (listChanged != null)
                {
                    listChanged(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
                }
            }
        }

        public abstract IList<TEntity> DeepClone();
        IEnumerable ICloneableList.DeepClone()
        {
            return DeepClone();
        }

        [CanBeNull]
        protected abstract ImmutableSortedList<TKey, TData> GetData(WorkspaceData workspaceData);
        protected abstract WorkspaceData SetData(WorkspaceData workspaceData, ImmutableSortedList<TKey, TData> data);
        protected abstract TEntity CreateEntityForKey(TKey key, TData data);
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<TEntity> GetEnumerator()
        {
            return EnsureEntityList().Values.GetEnumerator();
        }

        public void Add(TEntity item)
        {
            throw new InvalidOperationException();
        }

        public void Clear()
        {
            throw new InvalidOperationException();
        }

        public bool Contains(TEntity item)
        {
            return EnsureEntityList().ContainsKey(GetKey(item));
        }

        public void CopyTo(TEntity[] array, int arrayIndex)
        {
            EnsureEntityList().Values.CopyTo(array, arrayIndex);
        }

        public bool Remove(TEntity item)
        {
            throw new InvalidOperationException();
        }

        public int Count
        {
            get
            {
                var data = GetData(Workspace.Data);
                return data == null ? 0 : data.Count;
            }
        }
        public bool IsReadOnly
        {
            get { return true; }
        }
        public int IndexOf(TEntity item)
        {
            return EnsureEntityList().IndexOf(new KeyValuePair<TKey, TEntity>(GetKey(item), item));
        }

        public void Insert(int index, TEntity item)
        {
            throw new InvalidOperationException();
        }

        public void RemoveAt(int index)
        {
            throw new InvalidOperationException();
        }

        public TEntity this[int index]
        {
            get { return EnsureEntityList().Values[index]; }
            set { throw new InvalidOperationException(); }
        }

        public int IndexOfKey(TKey key)
        {
            var range = EnsureEntityList().BinarySearch(key);
            return range.Length == 0 ? -1 : range.Start;
        }
        public TEntity FindByKey(TKey key)
        {
            int index = IndexOfKey(key);
            return index < 0 ? null : this[index];
        }

        public abstract TKey GetKey(TEntity value);
        public virtual WorkspaceData Merge(WorkspaceData newData)
        {
            var baseList = GetData(Workspace.SavedData);
            var myList = GetData(Workspace.Data);
            var theirList = GetData(newData);
            if (myList == null)
            {
                return newData;
            }
            if (baseList == null || theirList == null)
            {
                return SetData(newData, myList);
            }

            if (Equals(baseList, theirList) || Equals(myList, theirList))
            {
                return SetData(newData, myList);
            }
            var mergedList = new Dictionary<TKey, TData>(myList.AsDictionary());
            foreach (var entry in theirList)
            {
                TData myData;
                if (!mergedList.TryGetValue(entry.Key, out myData))
                {
                    mergedList.Add(entry.Key, entry.Value);
                    continue;
                }
                TData baseData;
                if (baseList.TryGetValue(entry.Key, out baseData))
                {
                    if (!CheckDirty(baseData, myData))
                    {
                        mergedList[entry.Key] = entry.Value;
                    }
                    else
                    {
                        if (CheckDirty(myData, entry.Value))
                        {
                            Console.Out.WriteLine("Unable to merge {0}", entry.Key);
                        }
                    }
                }
            }
            foreach (var deletedKey in baseList.Keys.Except(theirList.Keys))
            {
                mergedList.Remove(deletedKey);
            }
            return SetData(newData, ImmutableSortedList.FromValues(mergedList, theirList.KeyComparer));
        }

        protected virtual bool CheckDirty(TData data, TData savedData)
        {
            return !Equals(data, savedData);
        }

        public bool IsDirty
        {
            get
            {
                return ListDirty().Any();
            }
        }
        public IEnumerable<TEntity> ListDirty()
        {
            if (null == _entityList)
            {
                yield break;
            }
            var savedData = GetData(Workspace.SavedData);
            foreach (var entity in this)
            {
                TData savedItemData;
                if (savedData == null || !savedData.TryGetValue(GetKey(entity), out savedItemData) ||
                    CheckDirty(entity.Data, savedItemData))
                {
                    yield return entity;
                }
            }
        }

        public event ListChangedEventHandler ListChanged;
    }
}
