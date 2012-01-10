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
using System.Collections.Generic;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Model
{
    public abstract class WeakEntityModelCollection<P, E, C> : EntityModelCollection<P, long, E, C>
        where P : DbEntity<P>
        where E : DbEntity<E>
        where C : EntityModel<E>
    {
        protected WeakEntityModelCollection(Workspace workspace, P parent) : base(workspace, parent)
        {
        }

        protected override IDictionary<long, C> ConstructChildDict()
        {
            return new WeakDictionary<long, C>();
        }
        public virtual C GetChild(long id, ISession session)
        {
            var child = GetChild(id);
            if (child != null)
            {
                return child;
            }
            var entity = session.Get<E>(id);
            child = WrapChild(entity);
            return TryAddChild(child);
        }
        protected virtual C TryAddChild(C child)
        {
            using (Workspace.GetReadLock())
            {
                lock (this)
                {
                    C existing = GetChild(child.Id.Value);
                    if (existing != null)
                    {
                        return existing;
                    }
                    _childDict.Remove(child.Id.Value);
                    _childDict.Add(child.Id.Value, child);
                    child.Parent = this;
                    return child;
                }
            }
        }

        protected override void LoadChildren(P parent)
        {
        }

        protected virtual IList<long> GetChildIds(P parent)
        {
            var result = new List<long>();
            foreach (var entry in GetChildren(parent))
            {
                result.Add(entry.Key);
            }
            return result;
        }
        public override IList<C> ListChildren()
        {
            var children = new List<C>();
            foreach (var child in base.ListChildren())
            {
                if (child != null)
                {
                    children.Add(child);
                }
            }
            return children;
        }
        public void AddChildId(long id)
        {
            lock(this)
            {
                if (!_childDict.ContainsKey(id))
                {
                    _childDict.Add(id, null);
                    _childCount = _childDict.Count;
                }
            }
        }
    }
}