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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    [XmlRoot("column")]
    public class ColumnSpec : IAuditLogObject
    {
        public ColumnSpec()
        {
        }
        public ColumnSpec(PropertyPath propertyPath)
        {
            Name = propertyPath.ToString();
        }
        public ColumnSpec(ColumnSpec that)
        {
            Name = that.Name;
            Caption = that.Caption;
            Format = that.Format;
            Hidden = that.Hidden;
            SortIndex = that.SortIndex;
            SortDirection = that.SortDirection;
            Total = that.Total;
        }
        public string Name { get; private set; }
        public ColumnSpec SetName(string value)
        {
            return new ColumnSpec(this){Name = value};
        }
        public string Caption { get; private set; }
        public ColumnSpec SetCaption(string value)
        {
            return new ColumnSpec(this){Caption = value};
        }
        public string Format { get; private set; }
        public ColumnSpec SetFormat(string value)
        {
            return new ColumnSpec(this){Format = value};
        }
        public bool Hidden { get; private set; }
        public ColumnSpec SetHidden(bool value)
        {
            return new ColumnSpec(this) {Hidden = value};
        }
        public int? SortIndex { get; private set; }
        public ColumnSpec SetSortIndex(int? value)
        {
            return new ColumnSpec(this){SortIndex = value};
        }
        public ListSortDirection? SortDirection { get; private set; }
        public ColumnSpec SetSortDirection(ListSortDirection? value)
        {
            return new ColumnSpec(this){SortDirection = value};
        }
        public TotalOperation Total { get; private set; }
        public TotalOperation TotalOperation { get { return Total; } }

        public ColumnSpec SetTotal(TotalOperation totalOperation)
        {
            return new ColumnSpec(this){Total = totalOperation};
        }

        [XmlIgnore]
        public PropertyPath PropertyPath
        {
            get { return PropertyPath.Parse(Name); }
        }
        public ColumnSpec SetPropertyPath(PropertyPath value)
        {
            return new ColumnSpec(this) {Name = value == null ? string.Empty : value.ToString()};
        }

        // ReSharper disable LocalizableElement
        public static ColumnSpec ReadXml(XmlReader reader)
        {
            TotalOperation total = TotalOperation.GroupBy;
            string strTotal = reader.GetAttribute("total");
            if (strTotal != null)
            {
                total = (TotalOperation) Enum.Parse(typeof(TotalOperation), strTotal);
            }
            var columnSpec = new ColumnSpec
                {
                    Name = reader.GetAttribute("name"),
                    Caption = reader.GetAttribute("caption"),
                    Format = reader.GetAttribute("format"),
                    Hidden = "true" == reader.GetAttribute("hidden"),
                    Total = total,
                };
            string sortIndex = reader.GetAttribute("sortindex");
            if (sortIndex != null)
            {
                columnSpec.SortIndex = Convert.ToInt32(sortIndex);
            }
            string sortDirection = reader.GetAttribute("sortdirection");
            if (sortDirection != null)
            {
                columnSpec.SortDirection = (ListSortDirection) Enum.Parse(typeof(ListSortDirection), sortDirection);
            }
            
            
            bool empty = reader.IsEmptyElement;
            reader.ReadElementString("column");
            if (!empty)
            {
                reader.ReadEndElement();
            }
            return columnSpec;
        }
        // ReSharper restore LocalizableElement

        // ReSharper disable LocalizableElement
        public void WriteXml(XmlWriter writer)
        {
            if (Name != null)
            {
                writer.WriteAttributeString("name", Name);
            }
            if (Caption != null)
            {
                writer.WriteAttributeString("caption", Caption);
            }
            if (Format != null)
            {
                writer.WriteAttributeString("format", Format);
            }
            if (Hidden)
            {
                writer.WriteAttributeString("hidden", "true");
            }
            if (SortIndex != null)
            {
                writer.WriteAttributeString("sortindex", SortIndex.ToString());
            }
            if (SortDirection != null)
            {
                writer.WriteAttributeString("sortdirection", SortDirection.ToString());
            }
            if (Total != TotalOperation.GroupBy)
            {
                writer.WriteAttributeString("total", Total.ToString());
            }
        }
        // ReSharper restore LocalizableElement

        public string AuditLogText
        {
            get
            {
                return AuditLogParseHelper.GetParseString(ParseStringType.column_caption, PropertyPath.Name);
            }
        }

        public bool IsName
        {
            get { return true; }
        }

        public bool Equals(ColumnSpec other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Name, Name) 
                && Equals(other.Caption, Caption) 
                && Equals(other.Format, Format)
                && Equals(other.Hidden, Hidden)
                && Equals(other.SortIndex, SortIndex)
                && Equals(other.SortDirection, SortDirection)
                && Equals(other.Total, Total);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (ColumnSpec)) return false;
            return Equals((ColumnSpec) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Name.GetHashCode();
                result = (result*397) ^ (Caption != null ? Caption.GetHashCode() : 0);
                result = (result*397) ^ (Format != null ? Format.GetHashCode() : 0);
                result = (result*397) ^ Hidden.GetHashCode();
                result = (result*397) ^ (SortIndex != null ? SortIndex.GetHashCode() : 0);
                result = (result*397) ^ (SortDirection != null ? SortDirection.GetHashCode() : 0);
                result = (result*397) ^ Total.GetHashCode();
                return result;
            }
        }

        public override string ToString() // For debugging convenience
        {
            return Name;
        }
    }
    public class FilterSpec
    {
        private FilterSpec()
        {
        }

        public FilterSpec(PropertyPath propertyPath, FilterPredicate predicate)
        {
            Column = propertyPath.ToString();
            Predicate = predicate;
        }
        private FilterSpec(FilterSpec that)
        {
            Column = that.Column;
            Predicate = that.Predicate;
        }

        [Track]
        public string AuditLogColumn
        {
            get { return string.Format(@"{{5:{0}}}", ColumnId.Name); }
        }
        
        public string Column { get; private set; }
        public FilterSpec SetColumn(string column)
        {
            return new FilterSpec(this){Column = column};
        }
        public PropertyPath ColumnId { get { return PropertyPath.Parse(Column); } }
        public FilterSpec SetColumnId(PropertyPath columnId)
        {
            return SetColumn(columnId.ToString());
        }

        [TrackChildren(ignoreName:true)]
        public FilterPredicate Predicate { get; private set; }

        public FilterSpec SetPredicate(FilterPredicate predicate)
        {
            return new FilterSpec(this){Predicate = predicate};
        }
        public IFilterOperation Operation { get { return Predicate.FilterOperation; } }
        // ReSharper disable LocalizableElement
        public static FilterSpec ReadXml(XmlReader reader)
        {
            var filterSpec = new FilterSpec
                {
                    Column = reader.GetAttribute("column"),
                    Predicate = FilterPredicate.ReadXml(reader),
                };
            bool empty = reader.IsEmptyElement;
            reader.ReadElementString("filter");
            if (!empty)
            {
                reader.ReadEndElement();
            }
            return filterSpec;
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeString("column", Column);
            Predicate.WriteXml(writer);
        }
        // ReSharper restore LocalizableElement

        public bool Equals(FilterSpec other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Column, Column) && Equals(other.Predicate, Predicate);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(FilterSpec)) return false;
            return Equals((FilterSpec)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Column == null ? 0 : Column.GetHashCode();
                result = (result*397) ^ Predicate.GetHashCode();
                return result;
            }
        }

    }

    /// <summary>
    /// Models a user's customization of a view.
    /// A view has a list of columns to display.  It also can have a filter and sort to be applied (NYI).
    /// </summary>
    public class ViewSpec : Immutable, IComparable<ViewSpec>
    {
        public ViewSpec()
        {
            Columns = ImmutableList.Empty<ColumnSpec>();
            Filters = ImmutableList.Empty<FilterSpec>();
            UiMode = string.Empty;
        }
        public string Name { get; private set; }
        public ViewSpec SetName(string value)
        {
            return ChangeProp(ImClone(this), im=>im.Name = value);
        }
        public string RowSource { get; private set; }

        public ViewSpec SetRowSource(string value)
        {
            return ChangeProp(ImClone(this), im => im.RowSource = value);
        }

        public ViewSpec SetRowType(Type type)
        {
            return SetRowSource(type.FullName);
        }

        public string UiMode { get; private set; }

        public ViewSpec SetUiMode(string uiMode)
        {
            return ChangeProp(ImClone(this), im => im.UiMode = uiMode ?? string.Empty);
        }


        [Track]
        public ImmutableList<ColumnSpec> Columns { get; private set; }
        public ViewSpec SetColumns(IEnumerable<ColumnSpec> value)
        {
            return ChangeProp(ImClone(this), im => im.Columns = ImmutableList.ValueOf(value));
        }
        [Track]
        public ImmutableList<FilterSpec> Filters { get; private set; }
        public ViewSpec SetFilters(IEnumerable<FilterSpec> value)
        {
            return ChangeProp(ImClone(this), im => im.Filters = ImmutableList.ValueOf(value));
        }
        public string SublistName { get; private set; }
        public PropertyPath SublistId
        {
            get { return PropertyPath.Parse(SublistName); }
            set { SublistName = value.ToString(); }
        }
        public ViewSpec SetSublistId(PropertyPath sublistId)
        {
            return ChangeProp(ImClone(this), im => im.SublistId = sublistId);
        }

        public bool HasTotals
        {
            get
            {
                return Columns.Any(col => TotalOperation.PivotValue == col.Total);
            }
        }

        // ReSharper disable LocalizableElement
        public static ViewSpec ReadXml(XmlReader reader)
        {
            var viewSpec = new ViewSpec
                {
                    Name = reader.GetAttribute("name"),
                    RowSource = reader.GetAttribute("rowsource"),
                    SublistName = reader.GetAttribute("sublist"),
                    UiMode = reader.GetAttribute("uimode") ?? string.Empty
                };
            var columns = new List<ColumnSpec>();
            var filters = new List<FilterSpec>();
            if (reader.IsEmptyElement)
            {
                reader.ReadElementString("view");
                return viewSpec;
            }
            reader.Read();
            while (true)
            {
                if (reader.IsStartElement("column"))
                {
                    columns.Add(ColumnSpec.ReadXml(reader));
                }
                else if (reader.IsStartElement("filter"))
                {
                    filters.Add(FilterSpec.ReadXml(reader));
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    reader.ReadEndElement();
                    break;
                }
                else
                {
                    reader.Skip();
                }
            }
            viewSpec.Columns = ImmutableList.ValueOf(columns);
            viewSpec.Filters = ImmutableList.ValueOf(filters);
            return viewSpec;
        }

        public void WriteXml(XmlWriter writer)
        {
            if (Name != null)
            {
                writer.WriteAttributeString("name", Name);
            }
            if (RowSource != null)
            {
                writer.WriteAttributeString("rowsource", RowSource);
            }
            if (SublistName != null)
            {
                writer.WriteAttributeString("sublist", SublistName);
            }

            if (!string.IsNullOrEmpty(UiMode))
            {
                writer.WriteAttributeString("uimode", UiMode);
            }
            foreach (var column in Columns)
            {
                writer.WriteStartElement("column");
                column.WriteXml(writer);
                writer.WriteEndElement();
            }
            foreach (var filter in Filters)
            {
                writer.WriteStartElement("filter");
                filter.WriteXml(writer);
                writer.WriteEndElement();
            }
        }
        // ReSharper restore LocalizableElement

        public bool Equals(ViewSpec other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Name, Name)
                   && Equals(other.RowSource, RowSource)
                   && Columns.SequenceEqual(other.Columns)
                   && Filters.SequenceEqual(other.Filters)
                   && SublistId.Equals(other.SublistId)
                   && UiMode.Equals(other.UiMode);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (ViewSpec)) return false;
            return Equals((ViewSpec) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (Name != null ? Name.GetHashCode() : 0);
                result = result*397 ^ (RowSource != null ? RowSource.GetHashCode() : 0);
                result = (result*397) ^ CollectionUtil.GetHashCodeDeep(Columns);
                result = (result*397) ^ CollectionUtil.GetHashCodeDeep(Filters);
                result = (result*397) ^ SublistId.GetHashCode();
                result = (result*397) ^ UiMode.GetHashCode();
                return result;
            }
        }

        public int CompareTo(ViewSpec other)
        {
            return CaseInsensitiveComparer.Default.Compare(Name, other.Name);
        }

        public override string ToString() // For debugging convenience
        {
            return Name;
        }

    }
}
