/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Collections.ObjectModel;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.Hibernate.Query;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("report")]    
    public sealed class ReportSpec : XmlNamedElement
    {
        /// <summary>
        /// ReportSpec constructor
        /// </summary>
        public ReportSpec(string name, QueryDef queryDef) : base(name)
        {
            Table = queryDef.Table;
            Select = new ReadOnlyCollection<Identifier>(queryDef.Select.ToArrayStd());
            GroupBy = queryDef.GroupBy == null ? null : 
                new ReadOnlyCollection<Identifier>(queryDef.GroupBy.ToArrayStd());
            CrossTabHeaders = queryDef.CrossTabHeaders == null
                                  ? null
                                  : new ReadOnlyCollection<Identifier>(queryDef.CrossTabHeaders.ToArrayStd());
            CrossTabValues = queryDef.CrossTabValues == null
                                 ? null
                                 : new ReadOnlyCollection<Identifier>(queryDef.CrossTabValues.ToArrayStd());
        }

        public Type Table { get; private set; }
        /// <summary>
        /// The list of columns that are displayed in the report.
        /// </summary>
        public IList<Identifier> Select { get; private set; }
        /// <summary>
        /// The list of columns which are used to determine whether two 
        /// separate rows in the flat report are supposed to be displayed in 
        /// the same row in the crosstab report.
        /// </summary>
        public IList<Identifier> GroupBy { get; private set; }
        /// <summary>
        /// If this is a pivot report, then this is the column which provides 
        /// the names that are displayed horizontally in the crosstab.
        /// If this is not a pivot report, then CrossTabColumn is null.
        /// </summary>
        public IList<Identifier> CrossTabHeaders { get; private set; }

        public IList<Identifier> CrossTabValues { get; private set; }


        private enum Attr
        {
            table,
        }
        private enum El
        {
            select,
            group_by,
            cross_tab_headers,
            cross_tab_values,
            column,
        }
        /// <summary>
        /// For serialization
        /// </summary>
        private ReportSpec()
        {
        }
        public static ReportSpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ReportSpec());
        }
        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            Table = Type.GetType(reader.GetAttribute(Attr.table));
            reader.ReadStartElement();
            if (reader.IsStartElement(El.select))
            {
                reader.Read();
                Select = ReadColumns(reader);
                reader.ReadEndElement();
            }
            if (reader.IsStartElement(El.group_by))
            {
                reader.Read();
                GroupBy = ReadColumns(reader);
                reader.ReadEndElement();
            }
            if (reader.IsStartElement(El.cross_tab_headers))
            {
                reader.Read();
                CrossTabHeaders = ReadColumns(reader);
                reader.ReadEndElement();
            }
            if (reader.IsStartElement(El.cross_tab_values))
            {
                reader.Read();
                CrossTabValues = ReadColumns(reader);
                reader.ReadEndElement();
            }
            reader.ReadEndElement();
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttribute(Attr.table, Table);
            writer.WriteStartElement(El.select);
            WriteColumns(writer, Select);
            writer.WriteEndElement();
            if (GroupBy != null)
            {
                writer.WriteStartElement(El.group_by);
                WriteColumns(writer, GroupBy);
                writer.WriteEndElement();
            }
            if (CrossTabHeaders != null)
            {
                writer.WriteStartElement(El.cross_tab_headers);
                WriteColumns(writer, CrossTabHeaders);
                writer.WriteEndElement();
            }
            if (CrossTabValues != null)
            {
                writer.WriteStartElement(El.cross_tab_values);
                WriteColumns(writer, CrossTabValues);
                writer.WriteEndElement();
            }
        }

        #region object overrides

        public bool Equals(ReportSpec other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other)
                   && Equals(other.Table, Table)
                   && ListEquals(other.Select, Select)
                   && ListEquals(other.GroupBy, GroupBy)
                   && ListEquals(other.CrossTabHeaders, CrossTabHeaders)
                   && ListEquals(other.CrossTabValues, CrossTabValues);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as ReportSpec);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ Table.GetHashCode();
                result = (result*397) ^ ListHashCode(Select);
                result = (result*397) ^ ListHashCode(GroupBy);
                result = (result*397) ^ ListHashCode(CrossTabHeaders);
                result = (result*397) ^ ListHashCode(CrossTabValues);
                return result;
            }
        }
        #endregion

        /// <summary>
        /// Returns true if the two lists contain equal elements in the same order.
        /// </summary>
        private static bool ListEquals<T>(IList<T> list1, IList<T> list2)
        {
            if (list1 == list2)
            {
                return true;
            }
            if (list1 == null || list2 == null)
            {
                return false;
            }
            return ArrayUtil.EqualsDeep(list1, list2);
        }
        private static int ListHashCode<T>(IList<T> list)
        {
            if (list == null)
            {
                return 0;
            }
            return list.GetHashCodeDeep();
        }

        private static IList<Identifier> ReadColumns(XmlReader reader)
        {
            List<Identifier> identifiers = new List<Identifier>();
            while (reader.IsStartElement(El.column))
            {
                identifiers.Add(Identifier.Parse(reader.ReadString()));
                reader.ReadEndElement();
            }
            return new ReadOnlyCollection<Identifier>(identifiers);
        }

        private static void WriteColumns(XmlWriter writer, IList<Identifier> columns)
        {
            foreach (var identifier in columns)
            {
                writer.WriteStartElement(El.column);
                writer.WriteString(identifier.ToString());
                writer.WriteEndElement();
            }
        }
    }

    public class QueryDef
    {
        public Type Table { get; set;}
        public IList<Identifier> Select { get; set; }
        public IList<Identifier> GroupBy { get; set; }
        public IList<Identifier> CrossTabHeaders { get; set; }
        public IList<Identifier> CrossTabValues { get; set; }
    }
}
