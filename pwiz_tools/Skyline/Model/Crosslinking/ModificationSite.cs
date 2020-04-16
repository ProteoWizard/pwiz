using System;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class ModificationSite : IComparable<ModificationSite>
    {
        public ModificationSite(int aaIndex, string modName)
        {
            AaIndex = aaIndex;
            ModName = modName;
        }

        public int AaIndex { get; private set; }
        public string ModName { get; private set; }

        protected bool Equals(ModificationSite other)
        {
            return AaIndex == other.AaIndex && ModName == other.ModName;
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
                return (AaIndex * 397) ^ (ModName != null ? ModName.GetHashCode() : 0);
            }
        }

        public int CompareTo(ModificationSite other)
        {
            if (other == null)
            {
                return 1;
            }

            int result = AaIndex.CompareTo(other.AaIndex);
            if (result == 0)
            {
                result = StringComparer.Ordinal.Compare(ModName, other.ModName);
            }

            return result;
        }

        public override string ToString()
        {
            return (AaIndex + 1) + @":" + ModName;
        }
    }
}
