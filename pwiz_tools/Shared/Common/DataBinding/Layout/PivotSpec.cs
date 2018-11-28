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
using System.Linq;
using System.Xml;
using pwiz.Common.Collections;
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Layout
{
    public class PivotSpec : Immutable, IRowTransform
    {
        public static readonly PivotSpec EMPTY = new PivotSpec();
        [TrackChildren]
        public ImmutableList<Column> RowHeaders { get; private set; }

        public PivotSpec()
        {
            RowHeaders = ImmutableList<Column>.EMPTY;
            ColumnHeaders = ImmutableList<Column>.EMPTY;
            Values = ImmutableList<AggregateColumn>.EMPTY;
        }

        public PivotSpec ChangeRowHeaders(IEnumerable<Column> columns)
        {
            return ChangeProp(ImClone(this), im => im.RowHeaders = ImmutableList.ValueOfOrEmpty(columns));
        }
        [TrackChildren]
        public ImmutableList<Column> ColumnHeaders { get; private set; }

        public PivotSpec ChangeColumnHeaders(IEnumerable<Column> columns)
        {
            return ChangeProp(ImClone(this), im => im.ColumnHeaders = ImmutableList.ValueOfOrEmpty(columns));
        }
        [TrackChildren]
        public ImmutableList<AggregateColumn> Values { get; private set; }

        public PivotSpec ChangeValues(IEnumerable<AggregateColumn> columns)
        {
            return ChangeProp(ImClone(this), im => im.Values = ImmutableList.ValueOfOrEmpty(columns));
        }

        public bool IsEmpty
        {
            get { return RowHeaders.Count == 0 && ColumnHeaders.Count == 0 && Values.Count == 0; }
        }

        protected bool Equals(PivotSpec other)
        {
            return RowHeaders.Equals(other.RowHeaders) 
                && ColumnHeaders.Equals(other.ColumnHeaders) 
                && Values.Equals(other.Values);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PivotSpec) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RowHeaders.GetHashCode();
                hashCode = (hashCode * 397) ^ ColumnHeaders.GetHashCode();
                hashCode = (hashCode * 397) ^ Values.GetHashCode();
                return hashCode;
            }
        }

        public class Column : Immutable, IAuditLogObject
        {
            public Column(ColumnId sourceColumn)
            {
                SourceColumn = sourceColumn;
                Visible = true;
            }
            public ColumnId SourceColumn { get; private set; }
            public string Caption { get; private set; }

            public Column ChangeCaption(string caption)
            {
                return ChangeProp(ImClone(this), im => im.Caption = caption);
            }
            public bool Visible { get; private set; }

            public Column ChangeVisible(bool visible)
            {
                return ChangeProp(ImClone(this), im => im.Visible = visible);
            }

            protected bool Equals(Column other)
            {
                return Equals(SourceColumn, other.SourceColumn) &&
                       string.Equals(Caption, other.Caption) && 
                       Visible == other.Visible;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Column) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (SourceColumn != null ? SourceColumn.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Caption != null ? Caption.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ Visible.GetHashCode();
                    return hashCode;
                }
            }

            public string AuditLogText { get { return SourceColumn.Name; } }
            public bool IsName { get { return true; }}
        }

        public class AggregateColumn : Column
        {
            public AggregateColumn(ColumnId sourceColumn, AggregateOperation aggregateOperation) : base(sourceColumn)
            {
                AggregateOperation = aggregateOperation;
            }
            [Track]
            public AggregateOperation AggregateOperation { get; private set; }

            protected bool Equals(AggregateColumn other)
            {
                return base.Equals(other) && Equals(AggregateOperation, other.AggregateOperation);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((AggregateColumn) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (base.GetHashCode() * 397) ^ (AggregateOperation != null ? AggregateOperation.GetHashCode() : 0);
                }
            }
        }

        public string Summary
        {
            get { return IsEmpty ? string.Empty : String.Format(Resources.PivotSpec_Summary_Pivot__0_x_1_x_2_, RowHeaders.Count, ColumnHeaders.Count, Values.Count); }
        }

        public string GetDescription(DataSchema dataSchema)
        {
            var parts = new List<string>();
            if (RowHeaders.Any())
            {
                parts.Add(Resources.PivotSpec_GetDescription_Row_Headers_);
                parts.AddRange(RowHeaders.Select(header=>header.Caption ?? header.SourceColumn.ToString()));
            }
            if (ColumnHeaders.Any())
            {
                if (parts.Any())
                {
                    parts.Add(string.Empty);
                }
                parts.Add(Resources.PivotSpec_GetDescription_Column_Headers_);
                parts.AddRange(ColumnHeaders.Select(header => header.Caption ?? header.SourceColumn.ToString()));
            }
            if (Values.Any())
            {
                if (parts.Any())
                {
                    parts.Add(string.Empty);
                }
                parts.Add(Resources.PivotSpec_GetDescription_Values_);
                parts.AddRange(Values.Select(value =>
                    (ColumnCaption.ExplicitCaption(value.Caption) ??
                     value.AggregateOperation.QualifyColumnCaption(value.SourceColumn.ToColumnCaption()))
                    .GetCaption(dataSchema.DataSchemaLocalizer)));
            }
            return string.Join(Environment.NewLine, parts);
        }

        // ReSharper disable LocalizableElement
        public void WriteXml(XmlWriter writer)
        {
            foreach (var rowHeader in RowHeaders)
            {
                writer.WriteStartElement("rowHeader");
                writer.WriteAttributeString("sourceColumn", rowHeader.SourceColumn.ToPersistedString());
                if (null != rowHeader.Caption)
                {
                    writer.WriteAttributeString("caption", rowHeader.Caption);
                }
                writer.WriteEndElement();
            }
            foreach (var columnHeader in ColumnHeaders)
            {
                writer.WriteStartElement("columnHeader");
                writer.WriteAttributeString("sourceColumn", columnHeader.SourceColumn.ToPersistedString());
                writer.WriteEndElement();
            }
            foreach (var value in Values)
            {
                writer.WriteStartElement("value");
                writer.WriteAttributeString("sourceColumn", value.SourceColumn.ToPersistedString());
                if (null != value.Caption)
                {
                    writer.WriteAttributeString("caption", value.Caption);
                }
                writer.WriteAttributeString("op", value.AggregateOperation.Name);
                writer.WriteEndElement();
            }
        }

        public static PivotSpec ReadXml(XmlReader reader)
        {
            var pivotSpec = new PivotSpec();
            if (reader.IsEmptyElement)
            {
                reader.ReadElementString("pivot");
                return pivotSpec;
            }
            var rowHeaders = new List<Column>();
            var columnHeaders = new List<Column>();
            var values = new List<AggregateColumn>();
            reader.Read();
            while (true)
            {
                if (reader.IsStartElement("rowHeader"))
                {
                    rowHeaders.Add(new Column(ColumnId.ParsePersistedString(reader.GetAttribute("sourceColumn")))
                        .ChangeCaption(reader.GetAttribute("caption")));
                    ReadEndElement(reader, "rowHeader");
                }
                else if (reader.IsStartElement("columnHeader"))
                {
                    columnHeaders.Add(new Column(ColumnId.ParsePersistedString(reader.GetAttribute("sourceColumn"))));
                    ReadEndElement(reader, "columnHeader");
                }
                else if (reader.IsStartElement("value"))
                {
                    values.Add((AggregateColumn) new AggregateColumn(
                            ColumnId.ParsePersistedString(reader.GetAttribute("sourceColumn")),
                            AggregateOperation.FromName(reader.GetAttribute("op")))
                        .ChangeCaption(reader.GetAttribute("caption")));
                    ReadEndElement(reader, "value");
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
            pivotSpec.RowHeaders = ImmutableList.ValueOf(rowHeaders);
            pivotSpec.ColumnHeaders = ImmutableList.ValueOf(columnHeaders);
            pivotSpec.Values = ImmutableList.ValueOf(values);
            return pivotSpec;
        }
        // ReSharper restore LocalizableElement

        private static void ReadEndElement(XmlReader reader, string name)
        {
            if (reader.IsEmptyElement)
            {
                reader.ReadElementString(name);
            }
            else
            {
                reader.Skip();
            }
        }
    }
}
