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
using System.Threading;
using pwiz.Common.DataBinding.Attributes;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Delimited separated values writer.
    /// </summary>
    public class DsvWriter
    {
        public DsvWriter(CultureInfo cultureInfo, char separator)
        {
            CultureInfo = cultureInfo;
            Separator = separator;
        }

        public CultureInfo CultureInfo { get; private set; }
        public char Separator { get; private set; }

        /// <summary>
        /// Writes out a row containing the column headers.
        /// </summary>
        public virtual void WriteHeaderRow(TextWriter writer, IEnumerable<PropertyDescriptor> propertyDescriptors)
        {
            bool first = true;
            foreach (var pd in propertyDescriptors)
            {
                if (!first)
                {
                    writer.Write(Separator);
                }
                first = false;
                writer.Write(ToDsvField(pd.DisplayName));
            }
            writer.WriteLine();
        }

        /// <summary>
        /// Writes out a row containing the formatted values
        /// </summary>
        public virtual void WriteDataRow(TextWriter writer, RowItem rowItem, IEnumerable<PropertyDescriptor> propertyDescriptors)
        {
            bool first = true;
            foreach (var pd in propertyDescriptors)
            {
                if (!first)
                {
                    writer.Write(Separator);
                }
                first = false;
                writer.Write(ToDsvField(GetFormattedValue(rowItem, pd)));
            }
            writer.WriteLine();
        }

        protected virtual string GetFormattedValue(RowItem rowItem, PropertyDescriptor propertyDescriptor)
        {
            CultureInfo oldCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo oldUiCulture = Thread.CurrentThread.CurrentUICulture;
            try
            {
                Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture = CultureInfo;
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
                if (value is double || value is float)
                {
                    var formatAttribute = (FormatAttribute) propertyDescriptor.Attributes[typeof (FormatAttribute)];
                    try
                    {
                        var doubleValue = Convert.ToDouble(value);
                        if (null == formatAttribute || null == formatAttribute.Format)
                        {
                            return doubleValue.ToString(CultureInfo);
                        }
                        return doubleValue.ToString(formatAttribute.Format, CultureInfo);
                    }
                    catch (Exception)
                    {
                        return value.ToString();
                    }
                }
                return value.ToString();
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = oldUiCulture;
                Thread.CurrentThread.CurrentCulture = oldCulture;
            }
        }

        protected virtual object GetValue(RowItem rowItem, PropertyDescriptor propertyDescriptor)
        {
            return propertyDescriptor.GetValue(rowItem);
        }

        protected virtual string ToDsvField(string text)
        {
            if (text == null)
                return string.Empty;
            if (text.IndexOfAny(new[] { '"', Separator, '\r', '\n' }) == -1)
                return text;
            return '"' + text.Replace("\"", "\"\"") + '"'; // Not L10N
        }
    }
}
