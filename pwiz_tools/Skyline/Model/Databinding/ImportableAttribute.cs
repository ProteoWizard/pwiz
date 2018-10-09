using System;
using System.Globalization;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// Indicates that the property can be included when you do File > Import > Annotations
    /// </summary>
    public class ImportableAttribute : Attribute
    {
        public Type Formatter { get; set; }
    }
    public interface IPropertyFormatter
    {
        object ParseValue(CultureInfo cultureInfo, string text);
        string FormatValue(CultureInfo cultureInfo, object value);
    }
}
