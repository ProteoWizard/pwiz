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
using NHibernate;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public abstract class EntityModelCollection<P, K, E, C> : ChildCollection<P,K,E,C>
        where P : DbEntity<P>
        where E : DbEntity<E>
        where C : EntityModel<E>
    {
        protected EntityModelCollection(Workspace workspace, P parent) : base(workspace, parent)
        {
        }
        protected EntityModelCollection(Workspace workspace) : base(workspace)
        {
        }
        protected override void AfterAddChild(C child)
        {
            child.Parent = this;
        }
        public override void SaveEntity(ISession session, C child, P parent, E entity)
        {
            if (!child.IsDirty())
            {
                return;
            }
            if (entity != null && entity.Id.HasValue && child.Id == null)
            {
                child.SetId(entity.Id.Value);
            }
            child.Save(session);
        }

        public bool SaveChildren(ISession session)
        {
            bool dirty = false;
            foreach (var child in ListChildren())
            {
                if (child.IsDirty())
                {
                    dirty = true;
                    break;
                }
            }
            var parent = session.Get<P>(Id);
            dirty = dirty || GetChildCount(parent) != _childDict.Count;
            if (dirty)
            {
                Save(session);
            }
            return dirty;
        }

        protected override void MergeChild(C child, E entity)
        {
            child.Merge(entity);
        }

        private ModelProperty ChildCountProperty
        {
            get
            {
                return Property<EntityModelCollection<P, K, E, C>, int>(m => m.GetChildCount(), (m, v) =>
                                                                                                    {
                                                                                                        throw new InvalidOperationException
                                                                                                            ();
                                                                                                    },
                                                                        GetChildCount,
                                                                        SetChildCount);
            }
        }
        public override bool IsDirty()
        {
            if (base.IsDirty())
            {
                return true;
            }
            if (IsPropDirty(ChildCountProperty))
            {
                return true;
            }
            foreach (var child in ListChildren())
            {
                if (child.IsDirty())
                {
                    return true;
                }
            }
            return false;
        }
    }
}