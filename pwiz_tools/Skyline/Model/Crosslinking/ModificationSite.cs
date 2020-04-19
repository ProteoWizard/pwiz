using System;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class ModificationSite : IComparable<ModificationSite>
    {
        public ModificationSite(int indexAa, string modName)
        {
            IndexAa = indexAa;
            ModName = modName;
        }

        public int IndexAa { get; private set; }
        public string ModName { get; private set; }

        protected bool Equals(ModificationSite other)
        {
            return IndexAa == other.IndexAa && ModName == other.ModName;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ModificationSite) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (IndexAa * 397) ^ (ModName != null ? ModName.GetHashCode() : 0);
            }
        }

        public int CompareTo(ModificationSite other)
        {
            if (other == null)
            {
                return 1;
            }

            int result = IndexAa.CompareTo(other.IndexAa);
            if (result == 0)
            {
                result = StringComparer.Ordinal.Compare(ModName, other.ModName);
            }

            return result;
        }

        public override string ToString()
        {
            return (IndexAa + 1) + @":" + ModName;
        }
    }
}
