/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding.Layout;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Delimited separated values writer.
    /// </summary>
    public class DsvWriter
    {
        public DsvWriter(char separator) : this(CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture, separator)
        {
            Separator = separator;
        }

        public DsvWriter(CultureInfo formatProvider, CultureInfo language, char separator)
        {
            FormatProvider = formatProvider;
            Language = language;
            Separator = separator;
        }

        public CultureInfo FormatProvider { get; private set; }
        public CultureInfo Language { get; private set; }
        public char Separator { get; private set; }
        public string NumberFormatOverride { get; set; }
        public ColumnFormats ColumnFormats { get; set; }

        /// <summary>
        /// Writes out a row containing the column headers.
        /// </summary>
        public virtual void WriteHeaderRow(TextWriter writer, IEnumerable<PropertyDescriptor> propertyDescriptors)
        {
            var transformedEnumerator = propertyDescriptors.AsEnumerable()
                .Select(pd => pd.DisplayName);
            WriteRowValues(writer, transformedEnumerator);
        }


        /// <summary>
        /// Writes out a row containing the formatted values
        /// </summary>
        public virtual void WriteDataRow(TextWriter writer, RowItem rowItem, IEnumerable<PropertyDescriptor> propertyDescriptors)
        {
            var transformedEnumerator = propertyDescriptors.AsEnumerable()
                .Select(pd =>  GetFormattedValue(rowItem, pd));
            WriteRowValues(writer, transformedEnumerator);
        }

        /// <summary>
        /// Writes out a row containing the given string values
        /// </summary>
        public virtual void WriteRowValues(TextWriter writer, IEnumerable<string> rowValues)
        {
            bool first = true;
            foreach (var rowValue in rowValues)
            {
                if (!first)
                {
                    writer.Write(Separator);
                }
                first = false;
                writer.Write(ToDsvField(rowValue));
            }
            writer.WriteLine();
        }
        
        public virtual string GetFormattedValue(RowItem rowItem, PropertyDescriptor propertyDescriptor)
        {
            CultureInfo oldCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo oldUiCulture = Thread.CurrentThread.CurrentUICulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = FormatProvider;
                Thread.CurrentThread.CurrentUICulture = Language;
                object value = GetValue(rowItem, propertyDescriptor);
                if (null == value)
                {
                    var formatAttribute = (FormatAttribute) propertyDescriptor.Attributes[typeof (FormatAttribute)];
                    if (null == formatAttribute)
                    {
                        return string.Empty;
                    }
                    return formatAttribute.NullValue;
                }

                try
                {
                    if (value is IFormattable formattable)
                    {
                        var formatString = GetFormatString(formattable, propertyDescriptor);
                        return FormatRoundTripCompatible(formattable, formatString, FormatProvider);
                    }
                }
                catch (Exception)
                {
                    return value.ToString();
                }
                return value.ToString();
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = oldUiCulture;
                Thread.CurrentThread.CurrentCulture = oldCulture;
            }
        }

        /// <summary>
        /// Formats a value, emulating .NET Framework's round-trip ("R") algorithm on net8 so invariant
        /// report exports stay byte-identical across frameworks. .NET Framework's "R" formatted a double
        /// at 15 significant digits, falling back to 17 only when 15 failed to round-trip; net8's "R"
        /// emits the shortest round-trippable form, which is 16 digits for some values where net472
        /// produced 17 (e.g. 742.90069580078125 -> 742.9006958007812). A blanket "G15" would instead
        /// corrupt the many legitimate 16-17 digit round-trip values, so reproduce the net472 sequence.
        /// </summary>
        private static string FormatRoundTripCompatible(IFormattable value, string formatString, IFormatProvider provider)
        {
#if NET472
            return value.ToString(formatString, provider);
#else
            if (formatString == @"R" || formatString == @"r")
            {
                if (value is double d)
                {
                    var g15 = d.ToString(@"G15", provider);
                    if (double.TryParse(g15, NumberStyles.Float, provider, out var back) && back == d)
                        return g15;
                    return d.ToString(@"G17", provider);
                }
                if (value is float f)
                {
                    var g7 = f.ToString(@"G7", provider);
                    if (float.TryParse(g7, NumberStyles.Float, provider, out var back) && back == f)
                        return g7;
                    return f.ToString(@"G9", provider);
                }
            }
            return value.ToString(formatString, provider);
#endif
        }

        protected virtual string GetFormatString(IFormattable value, PropertyDescriptor propertyDescriptor)
        {
            if (null != NumberFormatOverride)
            {
                if (value is double || value is float)
                {
                    return NumberFormatOverride;
                }
            }

            if (null != ColumnFormats && propertyDescriptor is DataPropertyDescriptor dataPropertyDescriptor)
            {
                var columnId = ColumnId.GetColumnId(dataPropertyDescriptor);
                var columnFormat = ColumnFormats.GetFormat(columnId);
                if (!string.IsNullOrEmpty(columnFormat?.Format))
                {
                    return columnFormat.Format;
                }
            }

            var formatAttribute = (FormatAttribute)propertyDescriptor.Attributes[typeof(FormatAttribute)];
            return formatAttribute?.Format;
        }

        protected virtual object GetValue(RowItem rowItem, PropertyDescriptor propertyDescriptor)
        {
            return propertyDescriptor.GetValue(rowItem);
        }

        protected virtual string ToDsvField(string text)
        {
            return ToDsvField(Separator, text);
        }

        public static string ToDsvField(char separator, string text)
        {
            if (text == null)
                return string.Empty;
            if (text.IndexOfAny(new[] { '"', separator, '\r', '\n' }) == -1)
                return text;
            // ReSharper disable LocalizableElement
            return '"' + text.Replace("\"", "\"\"") + '"';
            // ReSharper restore LocalizableElement
        }
    }
}
