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
using System.Xml;
using pwiz.Common.Collections;
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Layout
{
    public class RowFilter : Immutable, IRowTransform, IAuditLogComparable
    {
        public static readonly RowFilter Empty = new RowFilter();
        private const string SORT_DESC = "-";
        private const string SORT_ASC = "+";
        private string _normalizedText;

        private RowFilter()
        {
            Text = null;
            CaseSensitive = true;
            ColumnFilters = ImmutableList.Empty<ColumnFilter>();
            ColumnSorts = ImmutableList<ColumnSort>.EMPTY;
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
            ColumnSorts = ImmutableList<ColumnSort>.EMPTY;
        }

        public string Text { get; private set; }

        public RowFilter SetText(string text, bool caseSensitive)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.CaseSensitive = caseSensitive;
                if (string.IsNullOrEmpty(text))
                {
                    im._normalizedText = im.Text = null;
                }
                else
                {
                    im.Text = text;
                    im._normalizedText = caseSensitive ? text : text.ToLower();
                }
            });
        }
        public bool CaseSensitive { get; private set; }
        public bool IsEmptyFilter { get { return null == Text && 0 == ColumnFilters.Count; } }
        public bool IsEmpty { get { return IsEmptyFilter && 0 == ColumnSorts.Count; } }
        [TrackChildren]
        public ImmutableList<ColumnFilter> ColumnFilters { get; private set; }
        [TrackChildren]
        public ImmutableList<ColumnSort> ColumnSorts { get; private set; }

        public RowFilter SetColumnFilters(IEnumerable<ColumnFilter> columnFilters)
        {
            return ChangeProp(ImClone(this), im =>
                im.ColumnFilters = ImmutableList.ValueOfOrEmpty(columnFilters));
        }

        public RowFilter SetColumnSorts(IEnumerable<ColumnSort> columnSorts)
        {
            return ChangeProp(ImClone(this), im => im.ColumnSorts = ImmutableList.ValueOfOrEmpty(columnSorts));
        }

        public RowFilter ChangeListSortDescriptionCollection(
            ListSortDescriptionCollection listSortDescriptionCollection)
        {
            var newSorts = new List<ColumnSort>();
            foreach (var listSortDescription in listSortDescriptionCollection.OfType<ListSortDescription>())
            {
                var columnId = ColumnId.TryGetColumnId(listSortDescription.PropertyDescriptor);
                if (columnId == null)
                {
                    continue;
                }
                newSorts.Add(new ColumnSort(columnId, listSortDescription.SortDirection));
            }
            return SetColumnSorts(newSorts);
        }

        public IEnumerable<ColumnFilter> GetColumnFilters(DataPropertyDescriptor propertyDescriptor)
        {
            var columnId = ColumnId.GetColumnId(propertyDescriptor);
            return ColumnFilters.Where(columnFilter => Equals(columnId, columnFilter.ColumnId));
        }

        public Predicate<object> GetPredicate(DataSchema dataSchema, DataPropertyDescriptor propertyDescriptor)
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

        public ListSortDescriptionCollection GetListSortDescriptionCollection(
            IEnumerable<PropertyDescriptor> properties)
        {
            if (ColumnSorts.Count == 0)
            {
                return new ListSortDescriptionCollection();
            }
            var propertiesByColumnId = properties.ToLookup(ColumnId.TryGetColumnId);
            var sorts = new List<ListSortDescription>();
            foreach (var columnSort in ColumnSorts)
            {
                var pd = propertiesByColumnId[columnSort.ColumnId].FirstOrDefault();
                if (pd == null)
                {
                    continue;
                }
                sorts.Add(new ListSortDescription(pd, columnSort.ListSortDirection));
            }
            return new ListSortDescriptionCollection(sorts.ToArray());
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
            return Equals(other.Text, Text) && other.CaseSensitive.Equals(CaseSensitive) &&
                   Equals(other.ColumnFilters, ColumnFilters) && Equals(other.ColumnSorts, ColumnSorts);
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
                int result = Text == null ? 0 : Text.GetHashCode();
                result = (result * 397) ^ CaseSensitive.GetHashCode();
                result = (result * 397) ^ ColumnFilters.GetHashCode();
                result = (result * 397) ^ ColumnSorts.GetHashCode();
                return result;
            }
        }

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return Empty;
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
            public ColumnFilter(ColumnId columnId, FilterPredicate predicate)
            {
                ColumnId = columnId;
                Predicate = predicate;
            }
            [Track]
            public ColumnId ColumnId { get; private set; }
            [TrackChildren(ignoreName:true)]
            public FilterPredicate Predicate { get; private set; }

            protected bool Equals(ColumnFilter other)
            {
                return Equals(ColumnId, other.ColumnId) 
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
                    int hashCode = ColumnId.GetHashCode();
                    hashCode = (hashCode*397) ^ Predicate.GetHashCode();
                    return hashCode;
                }
            }
        }

        public class ColumnSort
        {
            public ColumnSort(ColumnId columnId, ListSortDirection listSortDirection)
            {
                ColumnId = columnId;
                ListSortDirection = listSortDirection;
            }

            [Track]
            public ColumnId ColumnId { get; private set; }
            [Track]
            public ListSortDirection ListSortDirection { get; private set; }

            protected bool Equals(ColumnSort other)
            {
                return ColumnId.Equals(other.ColumnId) && ListSortDirection == other.ListSortDirection;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ColumnSort) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (ColumnId.GetHashCode() * 397) ^ (int) ListSortDirection;
                }
            }
        }

        public string Summary
        {
            get
            {
                if (string.IsNullOrEmpty(Text))
                {
                    if (ColumnFilters.Count == 0)
                    {
                        var primaryColumnSort = ColumnSorts.FirstOrDefault();
                        if (primaryColumnSort != null)
                        {
                            string summary = Resources.RowFilter_Summary_Sort_on_ + primaryColumnSort.ColumnId;
                            if (primaryColumnSort.ListSortDirection == ListSortDirection.Descending)
                            {
                                summary += Resources.RowFilter_Summary__desc;
                            }
                            return summary;
                        }
                        return string.Empty;
                    }
                    if (ColumnFilters.Count == 1)
                    {
                        return string.Format(Resources.RowFilter_Summary_Filter_on__0_, ColumnFilters[0].ColumnId);
                    }
                    return string.Format(Resources.RowFilter_Summary_Filter_on__0__columns, ColumnFilters.Count);
                }
                else
                {
                    if (ColumnFilters.Count == 0)
                    {
                        return string.Format(Resources.RowFilter_Summary_Filter_for__0_, Text);
                    }
                    return string.Format(Resources.RowFilter_Summary_Filter_for_text_and__0__columns, ColumnFilters.Count);
                }
            }
        }

        public string GetDescription(DataSchema dataSchema)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Text))
            {
                parts.Add(string.Format(Resources.RowFilter_Summary_Filter_for__0_ , Text) + (CaseSensitive ? Resources.RowFilter_GetDescription__case_sensitive : string.Empty));
            }
            foreach (var columnFilter in ColumnFilters)
            {
                parts.Add(columnFilter.ColumnId + @" " + columnFilter.Predicate.FilterOperation.OpName + @" "
                    + columnFilter.Predicate.GetOperandDisplayText(dataSchema, typeof(object)));
            }
            string strOrderBy = Resources.RowFilter_GetDescription_Order_by_;
            foreach (var columnSort in ColumnSorts)
            {
                parts.Add(strOrderBy + columnSort.ColumnId +
                          (columnSort.ListSortDirection == ListSortDirection.Descending ? Resources.RowFilter_Summary__desc : string.Empty));
                strOrderBy = Resources.RowFilter_GetDescription_then_;
            }
            return string.Join(Environment.NewLine, parts);
        }
        // ReSharper disable LocalizableElement
        public void WriteXml(XmlWriter writer)
        {
            if (!string.IsNullOrEmpty(Text))
            {
                writer.WriteAttributeString("text", Text);
                if (CaseSensitive)
                {
                    writer.WriteAttributeString("caseSensitive", "true");
                }
            }
            foreach (var columnFilter in ColumnFilters)
            {
                writer.WriteStartElement("columnFilter");
                writer.WriteAttributeString("column", columnFilter.ColumnId.ToPersistedString());
                columnFilter.Predicate.WriteXml(writer);
                writer.WriteEndElement();
            }
            foreach (var columnSort in ColumnSorts)
            {
                writer.WriteStartElement("columnSort");
                writer.WriteAttributeString("column", columnSort.ColumnId.ToPersistedString());
                writer.WriteAttributeString("direction", columnSort.ListSortDirection == ListSortDirection.Descending ? SORT_DESC : SORT_ASC);
                writer.WriteEndElement();
            }
        }

        public static RowFilter ReadXml(XmlReader reader)
        {
            var rowFilter = new RowFilter();
            rowFilter.Text = reader.GetAttribute("text");
            if (!string.IsNullOrEmpty(rowFilter.Text))
            {
                rowFilter.CaseSensitive = reader.GetAttribute("caseSensitive") != null;
            }
            if (reader.IsEmptyElement)
            {
                reader.Read();
                return rowFilter;
            }
            reader.Read();
            var columnFilters = new List<ColumnFilter>();
            var columnSorts = new List<ColumnSort>();
            while (true)
            {
                if (reader.IsStartElement("columnFilter"))
                {
                    var columnFilter = new ColumnFilter(new ColumnId(reader.GetAttribute("column")), FilterPredicate.ReadXml(reader));
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                    }
                    else
                    {
                        reader.Read();
                        reader.ReadEndElement();
                    }
                    columnFilters.Add(columnFilter);
                }
                else if (reader.IsStartElement("columnSort"))
                {
                    var direction = Equals(SORT_DESC, reader.GetAttribute("direction"))
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending;
                    var columnSort = new ColumnSort(new ColumnId(reader.GetAttribute("column")), direction);
                    columnSorts.Add(columnSort);
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                    }
                    else
                    {
                        reader.Read();
                        reader.ReadEndElement();
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    reader.ReadEndElement();
                    break;
                }
                else
                {
                    reader.Read();
                }
            }
            rowFilter.ColumnFilters = ImmutableList.ValueOf(columnFilters);
            rowFilter.ColumnSorts = ImmutableList.ValueOf(columnSorts);
            return rowFilter;
        }
        // ReSharper restore LocalizableElement
    }
}
