using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class PrecursorKey : Immutable
    {
        public static readonly PrecursorKey EMPTY = new PrecursorKey(Adduct.EMPTY);
        public PrecursorKey(Adduct adduct) : this(adduct, null)
        {
        }
        public PrecursorKey(Adduct adduct, SpectrumClassFilter spectrumClassFilter)
        {
            Adduct = adduct.Unlabeled;
            SpectrumClassFilter = spectrumClassFilter;
        }

        public Adduct Adduct { get; private set; }

        public SpectrumClassFilter SpectrumClassFilter { get; private set; }

        protected bool Equals(PrecursorKey other)
        {
            return Equals(Adduct, other.Adduct) && Equals(SpectrumClassFilter, other.SpectrumClassFilter);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PrecursorKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Adduct.GetHashCode() * 397) ^
                       (SpectrumClassFilter != null ? SpectrumClassFilter.GetHashCode() : 0);
            }
        }
    }
}
