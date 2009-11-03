/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Linq;
using System.Text;
using NHibernate;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public abstract class ChildCollection<P, K, E, C> : EntityModel<P>
        where P:DbEntity<P>
        where E:DbEntity<E>
    {
        private int _childCount;
        protected IDictionary<K, C> _childDict;
        protected ChildCollection(Workspace workspace, P entity)
            : base(workspace, entity)
        {
        }
        protected ChildCollection(EntityModel parent, P entity) : this(parent.Workspace, entity)
        {
            Parent = parent;
        }

        protected virtual IDictionary<K,C> ConstructChildDict()
        {
            return new Dictionary<K, C>();
        }

        protected ChildCollection(Workspace workspace) : base(workspace)
        {
            _childDict = ConstructChildDict();
        }
        protected abstract IEnumerable<KeyValuePair<K, E>> GetChildren(P parent);
        public void EnsureChildrenLoaded()
        {
            using (GetReadLock())
            {
                lock(this)
                {
                    if (_childDict != null)
                    {
                        return;
                    }
                    using (var session = Workspace.OpenSession())
                    {
                        _childDict = ConstructChildDict();
                        try
                        {
                            LoadChildren(session.Load<P>(Id));
                        }
                        catch (Exception e)
                        {
                            Console.Out.WriteLine(e);
                        }
                    }
                }
            }
        }
        public virtual IList<C> ListChildren()
        {
            using(GetReadLock())
            {
                EnsureChildrenLoaded();
                lock(this)
                {
                    return _childDict.Values.ToArray();
                }
            }
        }
        public int GetChildCount()
        {
            return ChildCount;
        }

        public int ChildCount
        {
            get
            {
                return _childCount;
            }
        }
        public ICollection<K> GetKeys()
        {
            using(GetReadLock())
            {
                EnsureChildrenLoaded();
                return _childDict.Keys.ToArray();
            }
        }

        public abstract C WrapChild(E entity);
        public abstract void SaveEntity(ISession session, C child, P parent, E entity);
        protected abstract int GetChildCount(P parent);
        protected abstract void SetChildCount(P parent, int childCount);
        protected override void Load(P parent)
        {
            _childCount = GetChildCount(parent);
            if (_childCount == 0)
            {
                _childDict = ConstructChildDict();
            }
        }
        protected virtual void LoadChildren(P parent)
        {
            LoadChildren(GetChildren(parent));
        }
        public void LoadChildren(IEnumerable<KeyValuePair<K,E>> children)
        {
            using(GetReadLock())
            {
                lock(this)
                {
                    Debug.Assert(_childDict.Count == 0);
                    foreach (var entry in children)
                    {
                        Debug.Assert(!_childDict.ContainsKey(entry.Key));
                        C child = entry.Value == null ? default(C) : WrapChild(entry.Value);
                        _childDict.Add(entry.Key, child);
                        AfterAddChild(child);
                    }
                }
            }
        }

        protected virtual void AfterAddChild(C child)
        {
        }

        protected virtual void AfterRemoveChild(C child)
        {
        }

        public virtual bool AreChildrenLoaded()
        {
            return _childDict != null;
        }

        protected override P UpdateDbEntity(ISession session)
        {
            using(GetReadLock())
            {
                P parent = base.UpdateDbEntity(session);
                SetChildCount(parent, _childCount);
                if (parent.Id == null)
                {
                    session.Save(parent);
                    SetId(parent.Id.Value);
                }
                if (_childDict == null)
                {
                    return parent;
                }
                var entities = new Dictionary<K, E>();
                foreach (var entry in GetChildren(parent))
                {
                    C child;
                    if (_childDict.TryGetValue(entry.Key, out child))
                    {
                        entities.Add(entry.Key, entry.Value);
                    }
                    else
                    {
                        session.Delete(entry.Value);
                    }
                }

                foreach (var entry in _childDict)
                {
                    E existing;
                    entities.TryGetValue(entry.Key, out existing);
                    SaveEntity(session, entry.Value, parent, existing);
                }
                return parent;
            }
        }
        public C GetChild(K key)
        {
            using(GetReadLock())
            {
                EnsureChildrenLoaded();
                C child;
                _childDict.TryGetValue(key, out child);
                return child;
            }
        }

        public virtual void AddChild(K key, C child)
        {
            using (GetReadLock())
            {
                lock (this)
                {
                    EnsureChildrenLoaded();
                    _childDict.Add(key, child);
                    _childCount = _childDict.Count;
                    AfterAddChild(child);
                }
            }
        }
        public virtual void RemoveChild(K key)
        {
            using(GetWriteLock())
            {
                EnsureChildrenLoaded();

                if (_childDict.Remove(key))
                {
                    _childCount = _childDict.Count;
                    OnChange();
                }
            }
        }
    }
}
