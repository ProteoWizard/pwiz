/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// A list of objects which formats itself as a comma separated list, and passes
    /// the formatting parameters to the list elements.
    /// </summary>
    public class FormattableList<T> : IFormattable
    {
        private IList<T> _list;
        public FormattableList(IList<T> list)
        {
            _list = list;
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (_list == null)
            {
                return string.Empty;
            }
            return SeparateValues(TextUtil.GetCsvSeparator(formatProvider),
                _list.Select(item => ValueToString(item, format, formatProvider)));
        }

        public override string ToString()
        {
            if (_list == null)
            {
                return string.Empty;
            }
            return SeparateValues(TextUtil.CsvSeparator, 
                _list.Select(item => ((object) item ?? string.Empty).ToString()));
        }

        private static string SeparateValues(char separator, IEnumerable<string> values)
        {
            var stringWriter = new StringWriter();
            bool first = true;
            foreach (var value in values)
            {
                if (!first)
                {
                    stringWriter.Write(separator);
                }
                first = false;
                stringWriter.Write(DsvWriter.ToDsvField(separator, value));
            }
            return stringWriter.ToString();
            
        }

        public static string ValueToString(object value, string format, IFormatProvider formatProvider)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var formattableValue = value as IFormattable;
            if (formattableValue == null)
            {
                return value.ToString();
            }
            return formattableValue.ToString(format, formatProvider);
        }
    }
}
