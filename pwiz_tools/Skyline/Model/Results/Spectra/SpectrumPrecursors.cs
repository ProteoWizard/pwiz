/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
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
            _precursors = SortedByMz(precursors);
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

        private static ImmutableList<SpectrumPrecursor> SortedByMz(IEnumerable<SpectrumPrecursor> precursors)
        {
            var list = ImmutableList.ValueOf(precursors);
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].PrecursorMz.RawValue < list[i - 1].PrecursorMz.RawValue)
                {
                    // List was not sorted: return a new list in the correct order
                    return ImmutableList.ValueOf(list.OrderBy(p => p.PrecursorMz.RawValue));
                }
            }
            // list was already sorted
            return list;
        }
    }
}
