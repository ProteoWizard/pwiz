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
