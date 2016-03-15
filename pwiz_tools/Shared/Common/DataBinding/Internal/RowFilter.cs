/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding.Internal
{
    public class RowFilter
    {
        public static readonly RowFilter Empty = new RowFilter();
        private string _normalizedText;

        private RowFilter()
        {
            Text = null;
            CaseSensitive = true;
            ColumnFilters = ImmutableList.Empty<ColumnFilter>();
        }

        protected RowFilter(RowFilter rowFilter)
        {
            Text = rowFilter.Text;
            _normalizedText = rowFilter._normalizedText;
            ColumnFilters = rowFilter.ColumnFilters;
        }
        
        public RowFilter(string text, bool caseSensitive, IEnumerable<ColumnFilter> columnFilters)
        {
            Text = string.IsNullOrEmpty(text) ? null : text;
            CaseSensitive = caseSensitive;
            if (Text != null)
            {
                _normalizedText = CaseSensitive ? Text : Text.ToLower();
            }
            ColumnFilters = ImmutableList.ValueOf(columnFilters) ?? ImmutableList.Empty<ColumnFilter>();
        }

        public string Text { get; private set; }

        public RowFilter SetText(string text, bool caseSensitive)
        {
            RowFilter rowFilter = new RowFilter(this)
            {
                CaseSensitive = caseSensitive,
            };
            if (string.IsNullOrEmpty(text))
            {
                rowFilter._normalizedText = rowFilter.Text = null;
            }
            else
            {
                rowFilter.Text = text;
                rowFilter._normalizedText = caseSensitive ? text : text.ToLower();
            }
            return rowFilter;
        }
        public bool CaseSensitive { get; private set; }
        public bool IsEmpty { get { return null == Text && 0 == ColumnFilters.Count; } }
        public IList<ColumnFilter> ColumnFilters { get; private set; }

        public RowFilter SetColumnFilters(IEnumerable<ColumnFilter> columnFilters)
        {
            return new RowFilter(this)
            {
                ColumnFilters = ImmutableList.ValueOf(columnFilters) ?? ImmutableList.Empty<ColumnFilter>(),
            };
        }

        public IEnumerable<ColumnFilter> GetColumnFilters(PropertyDescriptor propertyDescriptor)
        {
            return ColumnFilters.Where(columnFilter => columnFilter.ColumnCaption == propertyDescriptor.DisplayName);
        }

        public Predicate<object> GetPredicate(DataSchema dataSchema, PropertyDescriptor propertyDescriptor)
        {
            var columnFilters = GetColumnFilters(propertyDescriptor).ToArray();
            if (columnFilters.Length == 0)
            {
                return null;
            }
            var predicates = columnFilters
                .Select(filter => filter.Predicate.MakePredicate(dataSchema, propertyDescriptor.PropertyType))
                .ToArray();
            return value => predicates.All(predicate => predicate(value));
        }

        public bool MatchesText(string value)
        {
            if (null == Text)
            {
                return true;
            }
            if (value == null)
            {
                return false;
            }
            if (CaseSensitive)
            {
                return value.IndexOf(_normalizedText, StringComparison.Ordinal) >= 0;
            }
            return value.ToLower().IndexOf(_normalizedText, StringComparison.Ordinal) >= 0;
        }

        public bool Equals(RowFilter other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Text, Text) && other.CaseSensitive.Equals(CaseSensitive) && Equals(other.ColumnFilters, ColumnFilters);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (RowFilter)) return false;
            return Equals((RowFilter) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Text.GetHashCode()*397) ^ CaseSensitive.GetHashCode() ^ ColumnFilters.GetHashCode();
            }
        }

        public static PropertyPath GetPropertyPath(PropertyDescriptor propertyDescriptor)
        {
            var columnPropertyDescriptor = propertyDescriptor as ColumnPropertyDescriptor;
            if (null != columnPropertyDescriptor && PivotKey.EMPTY.Equals(columnPropertyDescriptor.PivotKey))
            {
                return columnPropertyDescriptor.PropertyPath;
            }
            return null;
        }

        public class ColumnFilter
        {
            public ColumnFilter(String columnCaption, FilterPredicate predicate)
            {
                ColumnCaption = columnCaption;
                Predicate = predicate;
            }
            public String ColumnCaption { get; private set; }
            public FilterPredicate Predicate { get; private set; }

            public bool Matches(PropertyDescriptor propertyDescriptor)
            {
                return Equals(ColumnCaption, propertyDescriptor.DisplayName);
            }

            protected bool Equals(ColumnFilter other)
            {
                return string.Equals(ColumnCaption, other.ColumnCaption) 
                    && Equals(Predicate, other.Predicate);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ColumnFilter) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (ColumnCaption != null ? ColumnCaption.GetHashCode() : 0);
                    hashCode = (hashCode*397) ^ Predicate.GetHashCode();
                    return hashCode;
                }
            }
        }
    }
}
