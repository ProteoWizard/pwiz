using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumPrecursors : IFormattable
    {
        public static readonly SpectrumPrecursors EMPTY =
            new SpectrumPrecursors(ImmutableList.Empty<KeyValuePair<int, SignedMz>>());
        public SpectrumPrecursors(IEnumerable<KeyValuePair<int, SignedMz>> precursors)
        {
            Precursors = ImmutableList.ValueOfOrEmpty(precursors.OrderBy(p=>Tuple.Create(p.Key, p.Value)));
        }

        public ImmutableList<KeyValuePair<int, SignedMz>> Precursors { get; private set; }

        public bool Contains(SignedMz mz, double tolerance)
        {
            return Precursors.Any(precursor 
                => 1 == precursor.Key && 0 == mz.CompareTolerant(precursor.Value, tolerance));
        }

        public override string ToString()
        {
            return ToString(Formats.Mz, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (Precursors.Count == 0)
            {
                return string.Empty;
            }

            if (Precursors.Count == 1 && Precursors[0].Key == 1)
            {
                return Precursors[0].Value.ToString(format, formatProvider);
            }

            var csvSeparator = TextUtil.GetCsvSeparator(formatProvider).ToString();
            if (Precursors.All(p => p.Key == 1))
            {
                var precursorsString = string.Join(csvSeparator,
                    Precursors.Select(p => p.Value.ToString(format, formatProvider)));
                return string.Format(@"[{0}]", precursorsString);
            }
            else
            {
                var precursorsString = string.Join(csvSeparator,
                    Precursors.Select(p =>
                        string.Format(Resources.AlignedFile_AlignLibraryRetentionTimes__0__1__, p.Key, p.Value.ToString(format, formatProvider))));
                return string.Format(@"[{0}]", precursorsString);
            }
        }

        protected bool Equals(SpectrumPrecursors other)
        {
            return Precursors.Equals(other.Precursors);
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
            return Precursors.GetHashCode();
        }
    }
}
