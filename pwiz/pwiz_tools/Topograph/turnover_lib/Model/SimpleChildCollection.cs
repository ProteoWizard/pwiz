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
using NHibernate;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model
{
    public abstract class SimpleChildCollection<P,K,E> : ChildCollection<P,K,E,E> 
        where P : DbEntity<P> 
        where E : DbEntity<E>
    {
        protected SimpleChildCollection(Workspace workspace, P parent) : base(workspace, parent)
        {
        }
        protected SimpleChildCollection(Workspace workspace) : base(workspace)
        {
        }
        public override E WrapChild(E entity)
        {
            return entity;
        }

        public override void SaveEntity(ISession session, E child, P parent, E entity)
        {
            SetParent(child, parent);
            if (entity != null)
            {
                child.Id = entity.Id;
                child.Version = entity.Version;
                session.Merge(child);
            }
            else
            {
                child.Id = null;
                child.Version = 0;
                session.Save(child);
            }
        }

        protected abstract void SetParent(E child, P parent);
    }
}