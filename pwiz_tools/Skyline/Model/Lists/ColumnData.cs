/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lists
{
    public abstract class ColumnData
    {
        private const char CSV_SEPARATOR_CHAR = ',';
        private const string CSV_SEPARATOR_STRING = @",";
        public static ColumnData MakeColumnData(AnnotationDef annotationDef)
        {
            if (annotationDef.ValueType == typeof(double))
            {
                return Doubles.EMPTY;
            }
            if (annotationDef.ValueType == typeof(bool))
            {
                return Booleans.EMPTY;
            }
            return Strings.EMPTY;
        }

        public abstract int RowCount { get; }
        public abstract string ToPersistedString();
        public abstract ColumnData SetPersistedString(string str);
        public abstract object GetValue(int row);
        public abstract ColumnData SetValue(int row, object value);
        public abstract ColumnData AddRow(object value);
        public abstract ColumnData SetRowCount(int rowCount);
        public abstract ColumnData ReplaceValues(IEnumerable<object> values);

        protected static IEnumerable<string> ReadCsvRow(string row)
        {
            if (string.IsNullOrEmpty(row))
            {
                return new string[0];
            }
            var csvFileReader = new DsvFileReader(new StringReader(row), CSV_SEPARATOR_CHAR, false);
            return csvFileReader.ReadLine();
        }

        protected static string ToCsvRow(IEnumerable<string> values)
        {
            StringBuilder stringBuilder = new StringBuilder();
            bool anyValues = false;
            foreach (var value in values)
            {
                if (anyValues)
                {
                    stringBuilder.Append(CSV_SEPARATOR_STRING);
                }
                anyValues = true;

                if (!string.IsNullOrEmpty(value))
                {
                    stringBuilder.Append(DsvWriter.ToDsvField(CSV_SEPARATOR_CHAR, value));
                }
            }

            if (!anyValues)
            {
                // Return the empty string if there are zero rows of data
                return string.Empty;
            }

            if (stringBuilder.Length == 0)
            {
                // Return the empty quoted string if there is one row of blank data, to
                // distinguish it from zero rows of data.
                // ReSharper disable LocalizableElement
                return "\"\"";
                // ReSharper restore LocalizableElement
            }

            return stringBuilder.ToString();
        }

        public abstract class AbstractColumnData<T> : ColumnData
        {
            protected readonly ImmutableList<T> _values;

            protected AbstractColumnData(IEnumerable<T> values)
            {
                _values = ImmutableList.ValueOf(values);
            }

            public override ColumnData ReplaceValues(IEnumerable<object> values)
            {
                return ReplaceValues(values.Cast<T>());
            }

            protected abstract AbstractColumnData<T> ReplaceValues(IEnumerable<T> values);

            protected virtual T ConvertValue(object value)
            {
                return (T)value;
            }
            public override int RowCount
            {
                get { return _values.Count; }
            }
            public override object GetValue(int row)
            {
                return _values[row];
            }
            public override ColumnData SetValue(int row, object value)
            {
                return ReplaceValues(_values.ReplaceAt(row, ConvertValue(value)));
            }

            public override ColumnData AddRow(object value)
            {
                return ReplaceValues(ImmutableList.ValueOf(_values.Concat(new[] { ConvertValue(value) })));
            }

            protected bool Equals(AbstractColumnData<T> other)
            {
                return _values.Equals(other._values);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((AbstractColumnData<T>)obj);
            }

            public override int GetHashCode()
            {
                return _values.GetHashCode();
            }

            public override ColumnData SetRowCount(int rowCount)
            {
                if (rowCount < RowCount)
                {
                    return ReplaceValues(_values.Take(rowCount));
                }
                return ReplaceValues(_values.Concat(Enumerable.Repeat(default(T), rowCount - RowCount)));
            }
        }

        public class Doubles : AbstractColumnData<double?>
        {
            public static readonly Doubles EMPTY = new Doubles(ImmutableList.Empty<double?>());

            public Doubles(IEnumerable<double?> values) : base(values)
            {
            }

            protected override AbstractColumnData<double?> ReplaceValues(IEnumerable<double?> values)
            {
                return new Doubles(values);
            }

            public override string ToPersistedString()
            {
                return ToCsvRow(_values.Select(value =>
                    value.HasValue
                        ? value.Value.ToString(Formats.RoundTrip, CultureInfo.InvariantCulture)
                        : string.Empty));
            }

            public override ColumnData SetPersistedString(string persistedString)
            {
                var doubles = new List<double?>();
                foreach (var value in ReadCsvRow(persistedString))
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        doubles.Add(null);
                    }
                    else
                    {
                        doubles.Add(double.Parse(value, CultureInfo.InvariantCulture));
                    }
                }
                return new Doubles(doubles);
            }
        }

        public class Strings : AbstractColumnData<string>
        {
            public static readonly Strings EMPTY = new Strings(ImmutableList.Empty<string>());

            public Strings(IEnumerable<string> values) : base(values)
            {
            }

            public override string ToPersistedString()
            {
                return ToCsvRow(_values);
            }

            public override ColumnData SetPersistedString(string persistedString)
            {
                return new Strings(ReadCsvRow(persistedString).Select(s => string.IsNullOrEmpty(s) ? null : s));
            }

            protected override AbstractColumnData<string> ReplaceValues(IEnumerable<string> values)
            {
                return new Strings(values);
            }
        }

        public class Booleans : AbstractColumnData<bool>
        {
            public static readonly Booleans EMPTY = new Booleans(ImmutableList.Empty<bool>());

            public Booleans(IEnumerable<bool> values) : base(values)
            {
            }

            protected override AbstractColumnData<bool> ReplaceValues(IEnumerable<bool> values)
            {
                return new Booleans(values);
            }

            public override int RowCount { get { return _values.Count; } }

            public override object GetValue(int row)
            {
                return _values[row];
            }

            protected override bool ConvertValue(object value)
            {
                return ((bool?) value).GetValueOrDefault();
            }

            public override string ToPersistedString()
            {
                return ToCsvRow(_values.Select(value => value ? @"1" : @"0"));
            }

            public override ColumnData SetPersistedString(string str)
            {
                return new Booleans(ReadCsvRow(str).Select(value => @"1" == value));
            }
        }
    }
}
