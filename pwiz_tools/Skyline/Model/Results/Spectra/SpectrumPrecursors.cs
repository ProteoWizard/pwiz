using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumPrecursors : IFormattable
    {
        public static readonly SpectrumPrecursors EMPTY =
            new SpectrumPrecursors(ImmutableList.Empty<SpectrumPrecursor>());

        private ImmutableList<SpectrumPrecursor> _precursors;
        public SpectrumPrecursors(IEnumerable<SpectrumPrecursor> precursors)
        {
            _precursors = ImmutableList.ValueOf(precursors.OrderBy(p=>p.PrecursorMz.RawValue));
        }

        public override string ToString()
        {
            return ToString(Formats.Mz, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return string.Join(TextUtil.GetCsvSeparator(formatProvider).ToString(),
                _precursors.Select(p => p.PrecursorMz.ToString(format, formatProvider)));
        }

        protected bool Equals(SpectrumPrecursors other)
        {
            return _precursors.Equals(other._precursors);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SpectrumPrecursors) obj);
        }

        public override int GetHashCode()
        {
            return _precursors.GetHashCode();
        }
    }
}
