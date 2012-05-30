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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using NHibernate;
using pwiz.Common.DataBinding;
using pwiz.Topograph.Data;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Model
{
    public class EntityModel : IEntity, IComparable
    {
        private EntityModel _parent;
        protected EntityModel(Workspace workspace, long? id)
        {
            Workspace = workspace;
            Id = id;
        }

        [Browsable(false)]
        public Workspace Workspace { get; private set; }
        [Browsable(false)]
        public long? Id { get; private set; }
        [Browsable(false)]
        public string EntityName {get { return ModelType.Name;}}
        [Browsable(false)]
        public virtual Type ModelType { get { return GetType(); } }
        public virtual void SetId(long? id)
        {
            Id = id;
        }
        
        public override bool Equals(Object o)
        {
            if (Id == null)
            {
                return base.Equals(o);
            }
            if (o == this)
            {
                return true;
            }
            var that = o as EntityModel;
            if (that == null)
            {
                return false;
            }
            return Equals(Workspace, that.Workspace) && Equals(ModelType, that.ModelType) && Equals(Id, that.Id);
        }

        public override int GetHashCode()
        {
            if (Id == null)
            {
                return base.GetHashCode();
            }
            int result = Workspace.GetHashCode();
            result = result*31 + ModelType.GetHashCode();
            result = result*31 + Id.GetHashCode();
            return result;
        }

        protected void SetIfChanged<T>(ref T currentValue, T newValue)
        {
            using(GetWriteLock())
            {
                lock (this)
                {
                    if (Equals(currentValue, newValue))
                    {
                        return;
                    }
                    currentValue = newValue;
                    OnChange();
                }
            }
        }

        public virtual void Save(ISession session)
        {
        }

        protected virtual void OnChange()
        {
            if (Parent != null)
            {
                Workspace.EntityChanged(this);
            }
        }

        [Browsable(false)]
        public virtual EntityModel Parent
        {
            get
            {
                return _parent;
            }
            set
            {
                _parent = value;
            }
        }
        public AutoLock GetReadLock()
        {
            return Workspace.GetReadLock();
        }
        public AutoLock GetWriteLock()
        {
            return Workspace.GetWriteLock();
        }

        public virtual int CompareTo(object obj)
        {
            if (null == obj)
            {
                return 1;
            }
            return CaseInsensitiveComparer.DefaultInvariant.Compare(ToString(), obj.ToString());
        }
    }
    
    public abstract class EntityModel<T> : EntityModel where T : DbEntity<T>
    {
        protected EntityModel(Workspace workspace, T entity) : base(workspace, entity.Id)
        {
            Load(entity);
        }

        protected EntityModel(Workspace workspace) : base(workspace, null)
        {
        }

        protected virtual void Load(T entity)
        {
            foreach (var modelProperty in GetModelProperties())
            {
                if (modelProperty.IsDirty(this, SavedEntity))
                {
                    continue;
                }
                modelProperty.ModelSetter.Invoke(this, modelProperty.EntityGetter.Invoke(entity));
            }
            SavedEntity = entity;
        }

        public virtual void Merge(T entity)
        {
            Load(entity);
        }

        public virtual bool IsDirty()
        {
            if (SavedEntity == null)
            {
                return true;
            }
            foreach (var modelProperty in GetModelProperties())
            {
                if (modelProperty.IsDirty(this, SavedEntity))
                {
                    return true;
                }
            }
            return false;
        }
        public bool IsNew()
        {
            return SavedEntity == null;
        }
        [Browsable(false)]
        public T SavedEntity { get; set; }
        public virtual bool IsPropDirty(ModelProperty property)
        {
            return SavedEntity == null || property.IsDirty(this, SavedEntity);
        }
        protected virtual T ConstructEntity(ISession session)
        {
            throw new InvalidOperationException();
        }

        public override void Save(ISession session)
        {
            T entity = UpdateDbEntity(session);
            if (Id == null)
            {
                session.Save(entity);
                SetId(entity.Id.Value);
            }
            else
            {
                session.Update(entity);
            }
            base.Save(session);
        }

        protected virtual T UpdateDbEntity(ISession session)
        {
            T result = null;
            if (Id.HasValue)
            {
                result = session.Get<T>(Id);
            }
            if (result == null)
            {
                result = ConstructEntity(session);
                SetId(null);
            }
            foreach (var modelProperty in GetModelProperties())
            {
                modelProperty.EntitySetter.Invoke(result, modelProperty.ModelGetter.Invoke(this)); 
            }
            return result;
        }
        protected static ModelProperty Property<M,V>(Func<M,V> getter, Action<M,V> setter, Func<T,V> entityGetter, Action<T,V> entitySetter)
        {
            return ModelProperty.Property(getter, setter, entityGetter, entitySetter);
        }
        protected void LoadValue<V>(ref V value, T entity, Func<T,V> func)
        {
            value = NewValueUnlessCurrentChanged(value, entity, func);
        }
        protected V NewValueUnlessCurrentChanged<V>(V currentValue, T newEntity, Func<T,V> func)
        {
            if (SavedEntity == null || AreEqual(currentValue, func(SavedEntity)))
            {
                return func(newEntity);
            }
            return currentValue;
        }
        protected bool AreEqual<V>(V value1, V value2)
        {
            if (typeof(V).IsArray)
            {
                return Lists.EqualsDeep((IList) value1, (IList) value2);
            }
            return Equals(value1, value2);
        }
        protected virtual IEnumerable<ModelProperty> GetModelProperties()
        {
            yield break;
        }
    }

    public class EntityModelChangeEventArgs
    {
        public EntityModelChangeEventArgs(EntityModel entityModel)
        {
            EntityModel = entityModel;
        }
        public EntityModel EntityModel { get;private set;}
    }
}
