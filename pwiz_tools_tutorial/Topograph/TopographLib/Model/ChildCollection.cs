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
        protected int _childCount;
        protected IDictionary<K, C> _childDict;

        protected ChildCollection(Workspace workspace, P entity)
            : base(workspace, entity)
        {
            _childDict = ConstructChildDict();
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
        public virtual IList<C> ListChildren()
        {
            using(GetReadLock())
            {
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
                return _childDict.Keys.ToArray();
            }
        }

        public abstract C WrapChild(E entity);
        public abstract void SaveEntity(ISession session, C child, P parent, E entity);
        protected abstract int GetChildCount(P parent);
        protected abstract void SetChildCount(P parent, int childCount);
        protected override void Load(P parent)
        {
            base.Load(parent);
            _childCount = GetChildCount(parent);
            if (_childCount == 0 && TrustChildCount)
            {
                _childDict = ConstructChildDict();
            }
        }
        protected virtual bool TrustChildCount { get { return true;} }
        protected virtual void LoadChildren(P parent)
        {
            LoadChildren(GetChildren(parent));
        }
        public void LoadChildren(IEnumerable<KeyValuePair<K,E>> children)
        {
            lock(this)
            {
                var keys = new HashSet<K>();
                foreach (var entry in children)
                {
                    keys.Add(entry.Key);
                    C child;
                    if (_childDict.TryGetValue(entry.Key, out child))
                    {
                        MergeChild(child, entry.Value);
                    }
                    else
                    {
                        child = entry.Value == null ? default(C) : WrapChild(entry.Value);
                        _childDict.Add(entry.Key, child);
                    }
                    AfterAddChild(child);
                }
                foreach (var entry in _childDict.ToArray())
                {
                    if (!keys.Contains(entry.Key))
                    {
                        RemoveChild(entry.Key);
                    }
                }
            }
        }

        protected virtual void MergeChild(C child, E entity)
        {
            throw new InvalidOperationException();
        }

        public void LoadChildren(IEnumerable<E> children, Func<E,K> func)
        {
            LoadChildren(children.ToDictionary(func));
        }

        protected virtual void AfterAddChild(C child)
        {
        }

        protected virtual void AfterRemoveChild(C child)
        {
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
                if (_childDict.Remove(key))
                {
                    _childCount = _childDict.Count;
                    OnChange();
                }
            }
        }
    }
}
