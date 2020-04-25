using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkerSettings : Immutable
    {
        public static readonly CrosslinkerSettings EMPTY 
            = new CrosslinkerSettings();
        protected bool Equals(CrosslinkerSettings other)
        {
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CrosslinkerSettings) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return 0;
            }
        }
    }
}
