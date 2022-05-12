using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Spectra
{
    public class SpectrumPrecursor : Immutable
    {
        public SpectrumPrecursor(SignedMz precursorMz)
        {
            PrecursorMz = precursorMz;
        }

        public SignedMz PrecursorMz { get; }
        
        protected bool Equals(SpectrumPrecursor other)
        {
            return PrecursorMz.Equals(other.PrecursorMz);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SpectrumPrecursor) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return PrecursorMz.GetHashCode() * 397;
            }
        }
    }
}
