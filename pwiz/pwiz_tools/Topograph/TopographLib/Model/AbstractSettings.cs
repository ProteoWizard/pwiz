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
using System.Linq;
using NHibernate;
using pwiz.Common.Collections;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model.Data;

namespace pwiz.Topograph.Model
{
    public abstract class AbstractSettings<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        protected AbstractSettings(Workspace workspace)
        {
            Workspace = workspace;
        }

        public Workspace Workspace { get; private set; }

        public ImmutableSortedList<TKey, TValue> SavedData
        {
            get { return GetData(Workspace.SavedData); }
        }
        public ImmutableSortedList<TKey, TValue> Data
        {
            get { return GetData(Workspace.Data); }
            set { Workspace.Data = SetData(Workspace.Data, value); }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return Data.GetEnumerator();
        }

        public int Count
        {
            get { return Data == null ? 0 : Data.Count; }
        }

        public void Update(WorkspaceChangeArgs workspaceChange)
        {
            Diff(workspaceChange);
        }

        public virtual WorkspaceData Merge(WorkspaceData newWorkspaceData)
        {
            var myList = Data;
            var theirList = GetData(newWorkspaceData);
            var baseList = GetData(Workspace.SavedData);
            if (null == myList || Equals(baseList, myList))
            {
                return newWorkspaceData;
            }
            if (null == theirList || null == baseList || Equals(theirList, baseList))
            {
                return SetData(newWorkspaceData, myList);
            }
            var myChanges = new HashSet<KeyValuePair<TKey, TValue>>(Data);
            myChanges.SymmetricExceptWith(SavedData);

            var myChangedKeys = new HashSet<TKey>(myChanges.Select(pair => pair.Key)).Distinct();
            var newValues =
                new List<KeyValuePair<TKey, TValue>>(Data.Where(pair => myChangedKeys.Contains(pair.Key)));
            newValues.AddRange(theirList.Where(pair => !myChangedKeys.Contains(pair.Key)));

            var mergedData = ImmutableSortedList.FromValues(newValues, Data.KeyComparer);

            return SetData(newWorkspaceData, mergedData);
        }

        public void CompareValues(WorkspaceChangeArgs workspaceChange, ImmutableSortedList<TKey, TValue> newValues)
        {
            Diff(workspaceChange, newValues, Data);
        }

        protected abstract void Diff(WorkspaceChangeArgs workspaceChange,
                                                 ImmutableSortedList<TKey, TValue> newValues,
                                                 ImmutableSortedList<TKey, TValue> oldValues);
        public void Diff(WorkspaceChangeArgs workspaceChange)
        {
            var oldData = GetData(workspaceChange.OriginalData);
            var newData = Data;
            if (null != oldData && null != newData && !Equals(oldData, newData))
            {
                Diff(workspaceChange, oldData, newData);
            }
        }

        public abstract bool Save(ISession session, DbWorkspace dbWorkspace);

        protected bool SaveChangedEntities<TEntity>(ISession session, 
                                            IDictionary<TKey, TEntity> existingEntities,
                                            Func<TEntity, TValue> getValue, 
                                            Action<TEntity, TValue> setValue, 
                                            Func<TKey, TEntity> makeEntity)
        {
            bool anyChanges = false;
            foreach (var pair in Data)
            {
                TEntity entity;
                if (existingEntities.TryGetValue(pair.Key, out entity))
                {
                    anyChanges = anyChanges || !Equals(pair.Value, getValue(entity));
                    setValue(entity, pair.Value);
                    session.Update(entity);
                }
                else
                {
                    anyChanges = true;
                    entity = makeEntity(pair.Key);
                    setValue(entity, pair.Value);
                    session.Save(entity);
                }
            }
            foreach (var pair in existingEntities)
            {
                anyChanges = true;
                if (!Data.ContainsKey(pair.Key))
                {
                    session.Delete(pair.Value);
                }
            }
            return anyChanges;
        }
        public bool IsDirty
        {
            get { return !Equals(SavedData, Data); }
        }

        protected abstract ImmutableSortedList<TKey, TValue> GetData(WorkspaceData workspaceData);
        protected abstract WorkspaceData SetData(WorkspaceData workspaceData, ImmutableSortedList<TKey, TValue> value);
    }
}
