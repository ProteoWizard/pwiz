/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
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

        #region object overrides

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

        #endregion
    }

    [XmlRoot("grid_columns")]
    public class GridColumns : XmlNamedElement
    {
        private ImmutableList<GridColumn> _columns;

        public GridColumns(String name, IEnumerable<GridColumn> columns) : base(name)
        {
            _columns = MakeReadOnly(columns);
        }

        public ImmutableList<GridColumn> Columns
        {
            get { return _columns; }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
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
            _columns = ImmutableList.ValueOf(columns);
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

        #endregion

        #region object overrides

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

        #endregion
    }
}
