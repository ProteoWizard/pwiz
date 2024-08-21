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
    public sealed class AnnotatedDouble : AnnotatedValue<double?>, IFormattable, IConvertible
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

        TypeCode IConvertible.GetTypeCode()
        {
            return TypeCode.Object;
        }

        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToBoolean(provider);
        }

        char IConvertible.ToChar(IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToChar(provider);
        }

        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToSByte(provider);
        }

        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToByte(provider);
        }

        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToInt16(provider);
        }

        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToUInt16(provider);
        }

        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToInt32(provider);
        }

        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToUInt32(provider);
        }

        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToUInt32(provider);
        }

        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToUInt64(provider);
        }

        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToSingle(provider);
        }

        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToDouble(provider);
        }

        decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToDecimal(provider);
        }

        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToDateTime(provider);
        }

        public string ToString(IFormatProvider provider)
        {
            return Raw.ToString(provider);
        }

        object IConvertible.ToType(Type conversionType, IFormatProvider provider)
        {
            return ((IConvertible)Raw).ToType(conversionType, provider);
        }

        private class TypeConverterImpl : TypeConverter
        {
            private TypeConverter doubleTypeConverter = TypeDescriptor.GetConverter(typeof(double));
            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            {
                return doubleTypeConverter.CanConvertTo(context, destinationType);
            }

            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
            {
                var doubleValue = ((AnnotatedDouble)value).Raw;
                return doubleTypeConverter.ConvertTo(context, culture, doubleValue, destinationType);
            }
        }
    }
}
