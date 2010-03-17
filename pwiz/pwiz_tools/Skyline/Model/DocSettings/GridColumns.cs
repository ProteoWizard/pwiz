using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    public class GridColumn : XmlNamedElement
    {
        public GridColumn(String name, bool visible, int width) : base(name)
        {
            Visible = visible;
            Width = width;
        }

        public bool Visible { get; private set; }
        public int Width { get; private set; }

        public bool Equals(GridColumn other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && other.Visible.Equals(Visible) && other.Width == Width;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as GridColumn);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ Visible.GetHashCode();
                result = (result*397) ^ Width;
                return result;
            }
        }
    }

    [XmlRoot("grid_columns")]
    public class GridColumns : XmlNamedElement
    {
        public GridColumns(String name, IList<GridColumn> columns) : base(name)
        {
            Columns = new ReadOnlyCollection<GridColumn>(columns.ToArray());
        }

        private GridColumns()
        {
        }

        private enum Attr
        {
            name,
            visible,
            width
        }
        private enum El
        {
            grid_column
        }
        public IList<GridColumn> Columns { get; private set; }
        public static GridColumns Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new GridColumns());
        }
        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            reader.ReadStartElement();
            var columns = new List<GridColumn>();
            while (reader.IsStartElement(El.grid_column))
            {
                var name = reader.GetAttribute(Attr.name);
                var visible = reader.GetBoolAttribute(Attr.visible);
                var width = reader.GetIntAttribute(Attr.width);
                columns.Add(new GridColumn(name, visible, width));
                reader.Read();
            }
            Columns = new ReadOnlyCollection<GridColumn>(columns);
            reader.ReadEndElement();
        }
        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            foreach (var column in Columns)
            {
                writer.WriteStartElement(El.grid_column);
                writer.WriteAttribute(Attr.name, column.Name);
                writer.WriteAttribute(Attr.visible, column.Visible);
                writer.WriteAttribute(Attr.width, column.Width);
                writer.WriteEndElement();
            }
        }

        public bool Equals(GridColumns other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && ArrayUtil.EqualsDeep(other.Columns, Columns);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as GridColumns);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode()*397) ^ Columns.GetHashCodeDeep();
            }
        }
    }
}
