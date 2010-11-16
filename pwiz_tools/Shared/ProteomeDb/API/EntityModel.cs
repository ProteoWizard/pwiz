/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using pwiz.ProteomeDatabase.DataModel;

namespace pwiz.ProteomeDatabase.API
{
    public class EntityModel<T> where T : DbEntity<T>, new()
    {
        private readonly T _entity;
        public EntityModel(ProteomeDb proteomeDb, T entity)
        {
            _entity = entity;
            ProteomeDb = proteomeDb;
        }
        public T GetEntity(ISession session)
        {
            return session.Get<T>(_entity.Id);
        }
        public long Id
        {
            get { return _entity.Id.HasValue ? _entity.Id.Value : 0; }
        }
        public ProteomeDb ProteomeDb { get; private set; }
        public override bool Equals(Object other)
        {
            if (other == this)
            {
                return true;
            }
            EntityModel<T> that = (EntityModel<T>) other;
            if (that == null)
            {
                return false;
            }
            return _entity.Equals(that._entity);
        }
        public override int GetHashCode()
        {
            return _entity.GetHashCode();
        }
    }
}
