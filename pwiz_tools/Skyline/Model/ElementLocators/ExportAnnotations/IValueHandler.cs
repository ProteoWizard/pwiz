using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Hibernate;

namespace pwiz.Skyline.Model.ElementLocators.ExportAnnotations
{
    public interface IValueHandler
    {
        string ValueToString(SkylineDataSchema dataSchema, object value);
        object ValueFromString(SkylineDataSchema dataSchema, string strValue);
    }

    public abstract class AbstractValueHandler<T> : IValueHandler
    {
        public object ValueFromString(SkylineDataSchema dataSchema, string strValue)
        {
            if (string.IsNullOrEmpty(strValue))
            {
                return default(T);
            }
            return ParseValue(dataSchema, strValue);
        }

        public string ValueToString(SkylineDataSchema dataSchema, object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            return FormatValue(dataSchema, (T) value);
        }

        protected abstract T ParseValue(SkylineDataSchema dataSchema, string strValue);
        protected abstract string FormatValue(SkylineDataSchema dataSchema, T value);
    }

    public class StringValueHandler : AbstractValueHandler<string>
    {
        protected override string ParseValue(SkylineDataSchema dataSchema, string strValue)
        {
            return strValue;
        }

        protected override string FormatValue(SkylineDataSchema dataSchema, string value)
        {
            return value;
        }
    }

    public class DoubleValueHandler : AbstractValueHandler<double>
    {
        protected override double ParseValue(SkylineDataSchema dataSchema, string strValue)
        {
            return double.Parse(strValue, dataSchema.DataSchemaLocalizer.FormatProvider);
        }

        protected override string FormatValue(SkylineDataSchema dataSchema, double value)
        {
            return value.ToString(Formats.RoundTrip, dataSchema.DataSchemaLocalizer.FormatProvider);
        }
    }
}
