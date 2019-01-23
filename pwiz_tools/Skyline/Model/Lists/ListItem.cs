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
using System;
using System.Collections.Generic;
using System.Globalization;

namespace pwiz.Skyline.Model.Lists
{
    /// <summary>
    /// Base class of list items that are displayed in the Document Grid. 
    /// <see cref="ListItemTypes"/>
    /// </summary>
    public abstract class ListItem : IFormattable
    {
        private const string STR_BRACKET_START = "<{";
        private const string STR_BRACKET_END = "}>";
        private const string STR_ESCAPE = "_";

        private RecordData _recordData;

        public RecordData GetRecord()
        {
            return _recordData;
        }

        protected void SetRecord(RecordData recordData)
        {
            _recordData = recordData;
        }

        public static ListItem ConstructListItem(Type listItemType, RecordData recordData)
        {
            ListItem listItem = (ListItem) Activator.CreateInstance(listItemType);
            listItem.SetRecord(recordData);
            return listItem;
        }

        public static ListItem ExistingRecord(ListData listData, ListItemId id)
        {
            var itemType = ListItemTypes.INSTANCE.GetListItemType(listData.ListName);
            return ExistingRecord(itemType, listData, listData.RowIndexOf(id));
        }

        public static ListItem ExistingRecord(Type listItemType, ListData listData, int rowIndex)
        {
            var pkValue = listData.PkColumn == null ? null : listData.PkColumn.GetValue(rowIndex);
            var displayValue = listData.DisplayColumn == null ? null : listData.DisplayColumn.GetValue(rowIndex);
            var recordData = new ExistingRecordData(listData.ListItemIds[rowIndex], pkValue, listData.DisplayColumn != null, displayValue);
            return ConstructListItem(listItemType, recordData);
        }

        public static ListItem NewRecord(Type listItemType)
        {
            return ConstructListItem(listItemType, new NewRecordData());
        }

        public static ListItem OrphanRecord(Type listItemType, object pkValue, bool bracketDisplayText)
        {
            return ConstructListItem(listItemType, new OrphanRecordData(pkValue, bracketDisplayText));
        }

        protected bool Equals(ListItem other)
        {
            return _recordData.Equals(other._recordData);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ListItem) obj);
        }

        public override int GetHashCode()
        {
            return _recordData.GetHashCode();
        }

        public override string ToString()
        {
            return ToString(null, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return _recordData.ToString(format, formatProvider);
        }

        /// <summary>
        /// Adds bracketing text around a string. For orphaned records, the brackets serve
        /// to indicate that the value displayed is not coming from the same column as the
        /// non-orphans are displaying.
        /// </summary>
        public static string BracketText(string text)
        {
            return STR_BRACKET_START + text + STR_BRACKET_END;
        }

        public static bool IsBracketed(string text)
        {
            return text != null && text.StartsWith(STR_BRACKET_START) && text.EndsWith(STR_BRACKET_END);
        }

        /// <summary>
        /// Change the text so that it cannot be confused with either "null" or a bracketed value.
        /// </summary>
        public static string EscapeText(string text)
        {
            if (string.IsNullOrEmpty(text) || text.StartsWith(STR_BRACKET_START))
            {
                text = STR_ESCAPE + text;
            }
            if (text.EndsWith(STR_BRACKET_END))
            {
                text = text + STR_ESCAPE;
            }
            return text;
        }

        private static string FormatValue(string format, IFormatProvider formatProvider, object value)
        {
            if (null == value)
            {
                return string.Empty;
            }
            var formattable = value as IFormattable;
            if (formattable != null)
            {
                return formattable.ToString(format, formatProvider);
            }
            return value.ToString();
        }

        public abstract class RecordData : IFormattable
        {
            public override string ToString()
            {
                return ToString(null, CultureInfo.CurrentCulture);
            }

            public abstract string ToString(string format, IFormatProvider formatProvider);
            public abstract object PrimaryKeyValue { get; }

            public virtual string GetFullyQualifiedLabel(string format, IFormatProvider formatProvider)
            {
                return ToString(format, formatProvider);
            }
        }

        public class NewRecordData : RecordData
        {
            public NewRecordData()
            {
                UncommittedValues = new Dictionary<string, object>();
            }
            public Dictionary<string, object> UncommittedValues { get; private set; }
            public override string ToString(string format, IFormatProvider formatProvider)
            {
                return @"#NEW RECORD#";
            }

            public object GetColumnValue(string columnName)
            {
                object value;
                UncommittedValues.TryGetValue(columnName, out value);
                return value;
            }

            public override object PrimaryKeyValue
            {
                get { return null; }
            }
        }

        public class OrphanRecordData : RecordData
        {
            private object _pkValue;
            private bool _bracketDisplayText;
            public OrphanRecordData(object value, bool bracketDisplayText)
            {
                if (value == null)
                {
                    throw new ArgumentNullException();
                }
                _bracketDisplayText = bracketDisplayText;
                _pkValue = value;
            }

            public override string ToString(string format, IFormatProvider formatProvider)
            {
                string str = FormatValue(format, formatProvider, PrimaryKeyValue);
                if (_bracketDisplayText)
                {
                    return BracketText(str);
                }
                return str;
            }

            protected bool Equals(OrphanRecordData other)
            {
                return Equals(_pkValue, other._pkValue);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((OrphanRecordData) obj);
            }

            public override int GetHashCode()
            {
                return _pkValue.GetHashCode();
            }

            public override object PrimaryKeyValue
            {
                get { return _pkValue; }
            }
        }

        public class ExistingRecordData : RecordData
        {
            private object _pkValue;
            private object _displayValue;
            private bool _hasSeparateDisplayValue;

            public ExistingRecordData(ListItemId listItemId, object primaryKey, bool hasSeparateDisplayValue, object displayValue)
            {
                ListItemId = listItemId;
                _pkValue = primaryKey;
                _displayValue = displayValue ?? _pkValue;
                _hasSeparateDisplayValue = hasSeparateDisplayValue;
            }

            public ListItemId ListItemId { get; private set; }

            public override string ToString(string format, IFormatProvider formatProvider)
            {
                return EscapeText(FormatValue(format, formatProvider, _displayValue));
            }

            public override string GetFullyQualifiedLabel(string format, IFormatProvider formatProvider)
            {
                var text = ToString(format, formatProvider);
                if (!_hasSeparateDisplayValue)
                {
                    return text;
                }
                return text + BracketText(FormatValue(format, formatProvider, PrimaryKeyValue));
            }

            protected bool Equals(ExistingRecordData other)
            {
                return ListItemId.Equals(other.ListItemId) && Equals(_pkValue, other._pkValue) && Equals(_displayValue, other._displayValue);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ExistingRecordData) obj);
            }

            public override int GetHashCode()
            {
                return ListItemId.GetHashCode();
            }

            public override object PrimaryKeyValue
            {
                get { return _pkValue; }
            }
        }
    }
}
