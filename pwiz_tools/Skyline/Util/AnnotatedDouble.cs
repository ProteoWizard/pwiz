using System;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{
    public class AnnotatedDouble : AnnotatedValue<double?>, IFormattable
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

        public string ToString(string format)
        {
            return GetPrefix() + Raw.ToString(format);
        }

        public AnnotatedDouble ChangeValue(double? rawValue)
        {
            if (rawValue == null)
            {
                return null;
            }

            return new AnnotatedDouble(rawValue.Value, Message);
        }
    }
}
