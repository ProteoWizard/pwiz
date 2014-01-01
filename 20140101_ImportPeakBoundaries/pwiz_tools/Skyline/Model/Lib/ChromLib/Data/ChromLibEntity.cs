/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.Lib.ChromLib.Data
{
    public abstract class ChromLibEntity
    {
        public virtual int Id { get; set; }
        protected abstract Type EntityType { get; }
        #region Equality Members
        public override bool Equals(object o)
        {
            if (ReferenceEquals(this, o))
            {
                return true;
            }
            var that = o as ChromLibEntity;
            if (null == that)
            {
                return false;
            }
            if (0 == Id)
            {
                return false;
            }
            return Id == that.Id && ReferenceEquals(EntityType, that.EntityType);
        }
        public override int GetHashCode()
        {
            if (0 == Id)
            {
                // ReSharper disable BaseObjectGetHashCodeCallInGetHashCode
                return base.GetHashCode();
                // ReSharper restore BaseObjectGetHashCodeCallInGetHashCode
            }
            return Id*397 + EntityType.GetHashCode();
        }
        #endregion
    }

    public class ChromLibEntity<TEntity> : ChromLibEntity
    {
        protected override Type EntityType
        {
            get { return typeof(TEntity); }
        }
    }
}
