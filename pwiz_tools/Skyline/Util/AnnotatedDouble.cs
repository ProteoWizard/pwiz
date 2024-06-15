/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Globalization;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{
    [TypeConverter(typeof(TypeConverterImpl))]
    public sealed class AnnotatedDouble : AnnotatedValue<double?>, IFormattable
    {
        public static AnnotatedDouble Of(double? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return new AnnotatedDouble(value.Value, null);
        }
        public static AnnotatedDouble WithMessage(double rawValue, string message)
        {
            return new AnnotatedDouble(rawValue, message);
        }

        public AnnotatedDouble(double rawValue, string message) : base(rawValue, message)
        {
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public new double Raw
        {
            get { return base.Raw.GetValueOrDefault(); }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public new double? Strict
        {
            get { return base.Strict; }
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return GetPrefix() + Raw.ToString(format, formatProvider);
        }

        public AnnotatedDouble ChangeValue(double? rawValue)
        {
            if (rawValue == null)
            {
                return null;
            }

            return new AnnotatedDouble(rawValue.Value, Message);
        }

        private class TypeConverterImpl : TypeConverter
        {
            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            {
                if (destinationType == typeof(double))
                {
                    return true;
                }
                return base.CanConvertTo(context, destinationType);
            }

            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
            {
                if (destinationType == typeof(double))
                {
                    return ((AnnotatedDouble)value).Raw;
                }
                return base.ConvertTo(context, culture, value, destinationType);
            }
        }
    }
}
