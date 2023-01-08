using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.Spectra;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results.Spectra
{
    [TypeConverter(typeof(Converter))]
    [Filterable]
    public class SpectrumPrecursors : IFormattable
    {
        private ImmutableList<SpectrumPrecursor> _precursors;
        public SpectrumPrecursors(IEnumerable<SpectrumPrecursor> precursors)
        {
            _precursors = ImmutableList.ValueOf(precursors.OrderBy(p=>p.PrecursorMz.RawValue));
        }

        public static SpectrumPrecursors FromPrecursors(IEnumerable<SpectrumPrecursor> precursors)
        {
            var spectrumPrecursors = new SpectrumPrecursors(precursors);
            if (spectrumPrecursors._precursors.Count == 0)
            {
                return null;
            }

            return spectrumPrecursors;
        }

        public override string ToString()
        {
            return ToString(Formats.RoundTrip, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return string.Join(TextUtil.GetCsvSeparator(formatProvider) + @" ",
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

        public static SpectrumPrecursors Parse(CultureInfo culture, string s)
        {
            try
            {
                var listSeparator = TextUtil.GetCsvSeparator(culture);
                var precursors = new List<SpectrumPrecursor>();
                foreach (var field in TextUtil.ParseDsvFields(s, listSeparator))
                {
                    var doubleValue = double.Parse(field.Trim(), culture);
                    precursors.Add(new SpectrumPrecursor(new SignedMz(doubleValue, doubleValue < 0)));
                }

                return new SpectrumPrecursors(precursors);
            }
            catch (FormatException formatException)
            {
                throw new FormatException(string.Format(@"Unable to convert '{0}' to a list of precursors", s), formatException);
            }
        }

        public class Converter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
            }

            public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            {
                if (!(value is string stringValue))
                {
                    return base.ConvertFrom(context, culture, value);
                }

                return Parse(culture, stringValue);
            }
        }
    }
}
