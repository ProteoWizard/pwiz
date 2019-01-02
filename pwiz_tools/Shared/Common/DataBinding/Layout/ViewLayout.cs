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
using System.Xml;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Layout
{
    public class ViewLayout : Immutable, IAuditLogObject
    {
        public ViewLayout(string name)
        {
            Name = name;
            ColumnFormats = ImmutableList<Tuple<ColumnId, ColumnFormat>>.EMPTY;
            RowTransforms = ImmutableList<IRowTransform>.EMPTY;
        }
        public string Name { get; private set; }
        public ImmutableList<Tuple<ColumnId, ColumnFormat>> ColumnFormats { get; private set; }

        public ViewLayout ChangeColumnFormats(IEnumerable<Tuple<ColumnId, ColumnFormat>> formats)
        {
            return ChangeProp(ImClone(this), im => im.ColumnFormats = ImmutableList.ValueOf(formats));
        }
        [Track(ignoreName:true)]
        public ImmutableList<IRowTransform> RowTransforms { get; private set; } // PivotSpec, RowFilter

        public ViewLayout ChangeRowTransforms(IEnumerable<IRowTransform> rowTransforms)
        {
            return ChangeProp(ImClone(this), im => im.RowTransforms = ImmutableList.ValueOf(rowTransforms));
        }

        public string AuditLogText
        {
            get { return Name; }
        }

        public bool IsName
        {
            get { return true; }
        }

        protected bool Equals(ViewLayout other)
        {
            return string.Equals(Name, other.Name) && 
                   Equals(ColumnFormats, other.ColumnFormats) && Equals(RowTransforms, other.RowTransforms);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ViewLayout) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ColumnFormats != null ? ColumnFormats.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (RowTransforms != null ? RowTransforms.GetHashCode() : 0);
                return hashCode;
            }
        }

        // ReSharper disable LocalizableElement
        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeString("name", Name);
            foreach (var columnFormat in ColumnFormats)
            {
                writer.WriteStartElement("columnFormat");
                writer.WriteAttributeString("column", columnFormat.Item1.ToPersistedString());
                if (columnFormat.Item2.Width.HasValue)
                {
                    writer.WriteAttributeString("width", columnFormat.Item2.Width.ToString());
                }
                if (!string.IsNullOrEmpty(columnFormat.Item2.Format))
                {
                    writer.WriteAttributeString("format", columnFormat.Item2.Format);
                }
                writer.WriteEndElement();
            }
            foreach (var rowTransform in RowTransforms)
            {
                var pivotSpec = rowTransform as PivotSpec;
                if (pivotSpec != null)
                {
                    writer.WriteStartElement("pivot");
                    pivotSpec.WriteXml(writer);
                    writer.WriteEndElement();
                }
                else
                {
                    var rowFilter = rowTransform as RowFilter;
                    if (rowFilter != null)
                    {
                        writer.WriteStartElement("rowFilter");
                        rowFilter.WriteXml(writer);
                        writer.WriteEndElement();
                    }
                }
            }
        }

        public static ViewLayout ReadXml(XmlReader reader)
        {
            var viewLayout = new ViewLayout(reader.GetAttribute("name"));
            if (reader.IsEmptyElement)
            {
                reader.ReadElementString("layout");
                return viewLayout;
            }
            reader.Read();
            var columnFormats = new List<Tuple<ColumnId, ColumnFormat>>();
            var rowTransforms = new List<IRowTransform>();
            while (true)
            {
                if (reader.IsStartElement("columnFormat"))
                {
                    var columnFormat = ColumnFormat.EMPTY;

                    var strWidth = reader.GetAttribute("width");
                    if (strWidth != null)
                    {
                        columnFormat = columnFormat.ChangeWidth(int.Parse(strWidth));
                    }
                    columnFormat = columnFormat.ChangeFormat(reader.GetAttribute("format"));
                    columnFormats.Add(Tuple.Create(ColumnId.ParsePersistedString(reader.GetAttribute("column")), columnFormat));
                    if (reader.IsEmptyElement)
                    {
                        reader.ReadElementString("columnFormat");
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
                else if (reader.IsStartElement("pivot"))
                {
                    rowTransforms.Add(PivotSpec.ReadXml(reader));
                }
                else if (reader.IsStartElement("rowFilter"))
                {
                    rowTransforms.Add(RowFilter.ReadXml(reader));
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
            viewLayout.ColumnFormats = ImmutableList.ValueOf(columnFormats);
            viewLayout.RowTransforms = ImmutableList.ValueOf(rowTransforms);
            return viewLayout;
        }
        // ReSharper restore LocalizableElement

    }
}
