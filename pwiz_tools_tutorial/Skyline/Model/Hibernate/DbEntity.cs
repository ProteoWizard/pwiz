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
using System.Collections.Generic;

namespace pwiz.Skyline.Model.Hibernate
{
    /// <summary>
    /// Base class of Hibernate entities.
    /// </summary>
    public abstract class DbEntity
    {
        protected DbEntity()
        {
            Annotations = new Dictionary<string, string>();
        }
        /// <summary>
        /// Primary key of this entity.  Entities which have not been saved yet have a null Id.
        /// </summary>
        public virtual long? Id { get; set; }
        /// <summary>
        /// Returns the type of this Entity, which determines the table it is saved in.
        /// </summary>
        public abstract Type EntityClass { get; }
        /// <summary>
        /// Implementation of Equals.  In Hibernate, persisted objects are equal if they refer to the same rows 
        /// in the database (that is, entity name and id are equal).  Objects which have not been saved yet
        /// use reference equality.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (!Id.HasValue)
            {
// ReSharper disable BaseObjectEqualsIsObjectEquals
                return base.Equals(obj);
// ReSharper restore BaseObjectEqualsIsObjectEquals
            }
            if (obj == this)
            {
                return true;
            }
            var that = obj as DbEntity;
            if (that == null)
            {
                return false;
            }
            return EntityClass == that.EntityClass && Id == that.Id;
        }
        public override int GetHashCode()
        {
            if (!Id.HasValue)
            {
// ReSharper disable BaseObjectGetHashCodeCallInGetHashCode
                return base.GetHashCode();
// ReSharper restore BaseObjectGetHashCodeCallInGetHashCode
            }
            return EntityClass.GetHashCode()*31 + Id.GetHashCode();
        }
        public virtual IDictionary<string, string> Annotations { get; private set; }
    }
}
