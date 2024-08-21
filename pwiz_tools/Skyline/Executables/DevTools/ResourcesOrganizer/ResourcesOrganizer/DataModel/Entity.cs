/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using NHibernate.Mapping.Attributes;

namespace ResourcesOrganizer.DataModel
{
    public abstract class Entity
    {
        [Id(TypeType = typeof(long), Column="Id", Name="Id")]
        [Generator(Class = "identity")]
        public long? Id { get; set; }

        public abstract Type EntityType { get; }

        public override bool Equals(object? other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }
            if (!Id.HasValue)
            {
                return false;
            }

            if (!(other is Entity that))
            {
                return false;
            }

            return Id == that.Id && EntityType == that.EntityType;
        }

        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override int GetHashCode()
        {
            if (Id.HasValue)
            {
                return HashCode.Combine(Id, EntityType);
            }
            return RuntimeHelpers.GetHashCode(this);
        }
    }

    public class Entity<T> : Entity
    {
        public override Type EntityType
        {
            get { return typeof(T); }
        }
    }

}
