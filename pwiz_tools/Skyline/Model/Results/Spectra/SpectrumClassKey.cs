using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumClassKey : Immutable
    {
        public SpectrumClassKey(ImmutableList<SpectrumClassColumn> columns, SpectrumMetadata spectrumMetadata)
        {
            Columns = columns;
            Values = ImmutableList.ValueOf(Columns.Select(col=>col.GetValue(spectrumMetadata)));
        }

        public ImmutableList<SpectrumClassColumn> Columns { get; private set; }
        public ImmutableList<object> Values { get; private set; }

        protected bool Equals(SpectrumClassKey other)
        {
            return Equals(Columns, other.Columns) && Equals(Values, other.Values);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SpectrumClassKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Columns != null ? Columns.GetHashCode() : 0) * 397) ^ (Values != null ? Values.GetHashCode() : 0);
            }
        }
    }
}
