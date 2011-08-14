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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;

namespace pwiz.Common.DataBinding
{
    [XmlRoot("column")]
    public class ColumnSpec
    {
        public ColumnSpec()
        {
        }
        public ColumnSpec(ColumnSpec that)
        {
            Name = that.Name;
            Caption = that.Caption;
            Format = that.Format;
            Aggregate = that.Aggregate;
            Hidden = that.Hidden;
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
        public string Aggregate { get; private set; }
        public ColumnSpec SetAggregate(string value)
        {
            return new ColumnSpec(this) {Aggregate = value};
        }
        public bool Hidden { get; private set; }
        public ColumnSpec SetHidden(bool value)
        {
            return new ColumnSpec(this) {Hidden = value};
        }
        [XmlIgnore]
        public IdentifierPath IdentifierPath
        {
            get { return IdentifierPath.Parse(Name); }
        }
        public ColumnSpec SetIdentifierPath(IdentifierPath value)
        {
            return new ColumnSpec(this) {Name = value == null ? "" : value.ToString()};
        }
        public static ColumnSpec ReadXml(XmlReader reader)
        {
            var columnSpec = new ColumnSpec();
            columnSpec.Name = reader.GetAttribute("name");
            columnSpec.Caption = reader.GetAttribute("caption");
            columnSpec.Format = reader.GetAttribute("format");
            columnSpec.Hidden = "true" == reader.GetAttribute("hidden");
            
            bool empty = reader.IsEmptyElement;
            reader.ReadElementString("column");
            if (!empty)
            {
                reader.ReadEndElement();
            }
            return columnSpec;
        }

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
            if (Aggregate != null)
            {
                writer.WriteAttributeString("aggregate", Aggregate);
            }
        }

        public bool Equals(ColumnSpec other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Name, Name) 
                && Equals(other.Caption, Caption) 
                && Equals(other.Format, Format)
                && Equals(other.Hidden, Hidden)
                && Equals(other.Aggregate, Aggregate);
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
                result = (result*397) ^ (Aggregate != null ? Aggregate.GetHashCode() : 0);
                result = (result*397) ^ Hidden.GetHashCode();
                return result;
            }
        }
    }
    public class SortSpec
    {
        public SortSpec()
        {
            Direction = ListSortDirection.Ascending;
        }
        public SortSpec(SortSpec that)
        {
            Column = that.Column;
            Direction = that.Direction;
        }
        public string Column { get; private set; }
        public SortSpec SetColumn(string value)
        {
            return new SortSpec(this){Column = value};
        }
        public ListSortDirection Direction { get; private set; }
        public SortSpec SetDirection(ListSortDirection value)
        {
            return new SortSpec(this){Direction = value};
        }
        public static SortSpec ReadXml(XmlReader reader)
        {
            var sortSpec = new SortSpec();
            sortSpec.Column = reader.GetAttribute("column");
            var strDirection = reader.GetAttribute("direction");
            if (strDirection != null)
            {
                sortSpec.Direction = (ListSortDirection) Enum.Parse(typeof (ListSortDirection), strDirection);
            }
            else
            {
                sortSpec.Direction = ListSortDirection.Ascending;
            }
            bool empty = reader.IsEmptyElement;
            reader.ReadElementString("sort");
            if (!empty)
            {
                reader.ReadEndElement();
            }
            return sortSpec;
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeString("column", Column);
            writer.WriteAttributeString("direction", Direction.ToString());
        }

        public bool Equals(SortSpec other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Column, Column) && Equals(other.Direction, Direction);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (SortSpec)) return false;
            return Equals((SortSpec) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Column.GetHashCode()*397) ^ Direction.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Models a user's customization of a view.
    /// A view has a list of columns to display.  It also can have a filter and sort to be applied (NYI).
    /// </summary>
    public class ViewSpec : IComparable<ViewSpec>
    {
        public ViewSpec()
        {
            Columns = new ColumnSpec[0];
            Sorts = new SortSpec[0];
        }
        public ViewSpec(ViewSpec that)
        {
            Name = that.Name;
            Columns = that.Columns;
            Sorts = that.Sorts;
            SublistName = that.SublistName;
        }
        public string Name { get; private set; }
        public ViewSpec SetName(string value)
        {
            return new ViewSpec(this){Name = value};
        }
        public IList<ColumnSpec> Columns { get; private set; }
        public ViewSpec SetColumns(IEnumerable<ColumnSpec> value)
        {
            return new ViewSpec(this)
                       {
                           Columns = Array.AsReadOnly(value.ToArray())
                       };
        }
        public IList<SortSpec> Sorts { get; private set; }
        public ViewSpec SetSorts(IEnumerable<SortSpec> value)
        {
            return new ViewSpec(this)
                       {
                           Sorts = Array.AsReadOnly(value.ToArray())
                       };
        }
        public string SublistName { get; private set; }
        public IdentifierPath SublistId
        {
            get { return IdentifierPath.Parse(SublistName); }
            set { SublistName = value.ToString(); }
        }
        public ViewSpec SetSublistId(IdentifierPath sublistId)
        {
            return new ViewSpec(this){SublistId = sublistId};
        }
        public static ViewSpec ReadXml(XmlReader reader)
        {
            var viewSpec = new ViewSpec();
            viewSpec.Name = reader.GetAttribute("name");
            viewSpec.SublistName = reader.GetAttribute("sublist");
            var columns = new List<ColumnSpec>();
            var sorts = new List<SortSpec>();
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
                else if (reader.IsStartElement("sort"))
                {
                    sorts.Add(SortSpec.ReadXml(reader));
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
            viewSpec.Sorts = new ReadOnlyCollection<SortSpec>(sorts.ToArray());
            viewSpec.Columns = new ReadOnlyCollection<ColumnSpec>(columns.ToArray());
            return viewSpec;
        }

        public void WriteXml(XmlWriter writer)
        {
            if (Name != null)
            {
                writer.WriteAttributeString("name", Name);
            }
            if (SublistName != null)
            {
                writer.WriteAttributeString("sublist", SublistName);
            }
            foreach (var column in Columns)
            {
                writer.WriteStartElement("column");
                column.WriteXml(writer);
                writer.WriteEndElement();
            }
            foreach (var sort in Sorts)
            {
                writer.WriteStartElement("sort");
                sort.WriteXml(writer);
                writer.WriteEndElement();
            }
        }

        public bool Equals(ViewSpec other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Name, Name)
                   && Columns.SequenceEqual(other.Columns)
                   && Sorts.SequenceEqual(other.Sorts)
                   && SublistId.Equals(other.SublistId);
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
                result = (result*397) ^ CollectionUtil.GetHashCodeDeep(Columns);
                result = (result*397) ^ CollectionUtil.GetHashCodeDeep(Sorts);
                result = (result*397) ^ SublistId.GetHashCode();
                return result;
            }
        }

        public int CompareTo(ViewSpec other)
        {
            return CaseInsensitiveComparer.Default.Compare(Name, other.Name);
        }
    }

    [XmlRoot("views")]
    public class ViewSpecList : IXmlSerializable
    {
        public ViewSpecList()
        {
            ViewSpecs = new ViewSpec[0];
        }
        public string Name { get; set; }
        public IList<ViewSpec> ViewSpecs { get; set; }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            Name = reader.GetAttribute("name");
            if (reader.IsEmptyElement)
            {
                reader.ReadElementString("views");
                return;
            }
            reader.Read();
            var viewSpecs = new List<ViewSpec>();
            while (true)
            {
                if (reader.IsStartElement("view"))
                {
                    viewSpecs.Add(ViewSpec.ReadXml(reader));
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
            ViewSpecs = viewSpecs.AsReadOnly();
        }

        public void WriteXml(XmlWriter writer)
        {
            if (Name != null)
            {
                writer.WriteAttributeString("name", Name);
            }
            foreach (var viewSpec in ViewSpecs)
            {
                writer.WriteStartElement("view");
                viewSpec.WriteXml(writer);
                writer.WriteEndElement();
            }
        }
    }
}
