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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Util.Extensions;

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
            Select = new ReadOnlyCollection<ReportColumn>(queryDef.Select.ToArrayStd());
            GroupBy = queryDef.GroupBy == null
                          ? null
                          : new ReadOnlyCollection<ReportColumn>(queryDef.GroupBy.ToArrayStd());
            CrossTabHeaders = queryDef.CrossTabHeaders == null
                                  ? null
                                  : new ReadOnlyCollection<ReportColumn>(queryDef.CrossTabHeaders.ToArrayStd());
            CrossTabValues = queryDef.CrossTabValues == null
                                 ? null
                                 : new ReadOnlyCollection<ReportColumn>(queryDef.CrossTabValues.ToArrayStd());
        }

        /// <summary>
        /// The list of columns that are displayed in the report.
        /// </summary>
        public IList<ReportColumn> Select { get; private set; }

        /// <summary>
        /// The list of columns which are used to determine whether two 
        /// separate rows in the flat report are supposed to be displayed in 
        /// the same row in the crosstab report.
        /// </summary>
        public IList<ReportColumn> GroupBy { get; private set; }

        /// <summary>
        /// If this is a pivot report, then this is the column which provides 
        /// the names that are displayed horizontally in the crosstab.
        /// If this is not a pivot report, then CrossTabColumn is null.
        /// </summary>
        public IList<ReportColumn> CrossTabHeaders { get; private set; }

        public IList<ReportColumn> CrossTabValues { get; private set; }

        /// <summary>
        /// Returns a string representation of the report based on the document.         
        /// </summary>       
        public string ReportToCsvString(SrmDocument doc, IProgressMonitor progressMonitor)
        {
            return ReportToCsvString(doc, TextUtil.CsvSeparator, progressMonitor);
        }

        /// <summary>
        /// Returns a string representation of the report based on the document.         
        /// </summary>       
        private string ReportToCsvString(SrmDocument doc, char separator, IProgressMonitor progressMonitor)
        {
            var status = new ProgressStatus(string.Format(Resources.ReportSpec_ReportToCsvString_Exporting__0__report, Name));
            progressMonitor.UpdateProgress(status);

            Report report = Report.Load(this);
            StringWriter writer = new StringWriter();
            using (Database database = new Database(doc.Settings)
                {
                    ProgressMonitor = progressMonitor,
                    Status = status,
                    PercentOfWait = 80
                })
            {
                database.AddSrmDocument(doc);
                status = database.Status;

                ResultSet resultSet;
                try
                {
                    resultSet = report.Execute(database);
                }
                catch (Exception)
                {
                    progressMonitor.UpdateProgress(status.Cancel());
                    throw;
                }

                progressMonitor.UpdateProgress(status = status.ChangePercentComplete(95));

                ResultSet.WriteReportHelper(resultSet, separator, writer, LocalizationHelper.CurrentCulture);
            }
            writer.Flush();
            string csv = writer.ToString();
            writer.Close();
            progressMonitor.UpdateProgress(status.Complete());

            return csv;
        }

        private enum ATTR
        {
            table,
            name,
        }

        private enum EL
        {
            table,
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

        public const string TABLE_ALIAS_ELEMENT = "Element"; // Not L10N
        public const string TABLE_ALIAS_RESULT = "Result"; // Not L10N

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            
            // Backward compatibility support for v0.5
            var dictAliasTable = new Dictionary<string, Type>();
            string tableName = reader.GetAttribute(ATTR.table);
            if (tableName != null)
            {
                string tableTypeName = tableName.Substring(tableName.LastIndexOf('.') + 1);

                Type table = GetTable(tableTypeName);

                const string resultSuffix = "Result"; // Not L10N
                if (!tableTypeName.EndsWith(resultSuffix))
                    dictAliasTable.Add(TABLE_ALIAS_ELEMENT, table);
                else
                {
                    dictAliasTable.Add(TABLE_ALIAS_RESULT, table);

                    tableTypeName = tableTypeName.Substring(0, tableTypeName.Length - resultSuffix.Length);
                    table = GetTable(tableTypeName);
                    // If the element type does not exist, report an error on the original XML name string.
                    if (table == null)
                        throw new InvalidDataException(String.Format(Resources.ReportSpec_ReadXml_The_name__0__is_not_a_valid_table_name, tableName));

                    dictAliasTable.Add(TABLE_ALIAS_ELEMENT, table);
                }
                reader.ReadStartElement();
            }
            else
            {
                reader.ReadStartElement();
                while (reader.IsStartElement(EL.table))
                {
                    string tableAlias = reader.GetAttribute(ATTR.name);
                    if (String.IsNullOrEmpty(tableAlias))
                        throw  new InvalidDataException(Resources.ReportSpec_ReadXml_Missing_table_name);

                    dictAliasTable.Add(tableAlias, GetTable(reader.ReadString()));
                    reader.ReadEndElement();
                }
            }

            Select = ReadColumns(reader, EL.@select, dictAliasTable);
            GroupBy = ReadColumns(reader, EL.group_by, dictAliasTable);
            CrossTabHeaders = ReadColumns(reader, EL.cross_tab_headers, dictAliasTable);
            CrossTabValues = ReadColumns(reader, EL.cross_tab_values, dictAliasTable);

            reader.ReadEndElement();
        }

        private static Type GetTable(string tableTypeName)
        {
            tableTypeName = typeof(DbProtein).Namespace + '.' + tableTypeName; // Not L10N  

            Type table = Type.GetType(tableTypeName);
            if (table == null)
            {
                throw new InvalidDataException(String.Format(Resources.ReportSpec_GetTable_The_name__0__is_not_a_valid_table_name,
                    tableTypeName.Substring(tableTypeName.LastIndexOf('.') + 1))); // Not L10N         
            }

            return table;
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);

            var dictTableAlias = new Dictionary<Type, string>();
            EnsureAliases(Select, dictTableAlias);
            EnsureAliases(GroupBy, dictTableAlias);
            EnsureAliases(CrossTabHeaders, dictTableAlias);
            EnsureAliases(CrossTabValues, dictTableAlias);

            foreach(var tableAlias in ReportColumn.Order(dictTableAlias))
            {
                writer.WriteStartElement(EL.table);
                writer.WriteAttribute(ATTR.name, tableAlias.Value);
                writer.WriteString(tableAlias.Key.Name);
                writer.WriteEndElement();
            }

            WriteColumns(writer, EL.@select, Select, dictTableAlias);
            WriteColumns(writer, EL.group_by, GroupBy, dictTableAlias);
            WriteColumns(writer, EL.cross_tab_headers, CrossTabHeaders, dictTableAlias);
            WriteColumns(writer, EL.cross_tab_values, CrossTabValues, dictTableAlias);
        }

        private static void EnsureAliases(IEnumerable<ReportColumn> reportColumns,
            IDictionary<Type, string> dictTableAlias)
        {
            if (reportColumns != null)
            {
                foreach (var reportColumn in reportColumns)
                    reportColumn.EnsureAlias(dictTableAlias);
            }
        }

        #region object overrides

        public bool Equals(ReportSpec other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other)
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
        private static bool ListEquals<TItem>(IList<TItem> list1, IList<TItem> list2)
        {
            if (ReferenceEquals(list1, list2))
            {
                return true;
            }
            if (list1 == null || list2 == null)
            {
                return false;
            }
            return ArrayUtil.EqualsDeep(list1, list2);
        }

        private static int ListHashCode<TItem>(IList<TItem> list)
        {
            if (list == null)
            {
                return 0;
            }
            return list.GetHashCodeDeep();
        }

        private static IList<ReportColumn> ReadColumns(XmlReader reader, Enum elGroup, IDictionary<string, Type> dictAliasTable)
        {
            if (!reader.IsStartElement(elGroup))
            {
                return null;
            }

            if (reader.IsEmptyElement)
            {
                reader.Read();
                return new ReportColumn[0];
            }
            reader.Read();
            List<ReportColumn> identifiers = new List<ReportColumn>();

            while (reader.IsStartElement(EL.column))
            {
                string alias = reader.GetAttribute(ATTR.name);
                string columnString = reader.ReadString();
                Identifier colId = Identifier.Parse(columnString);

                if (String.IsNullOrEmpty(alias))
                {
                    // Support for v0.5 format when only a single table was used
                    if (dictAliasTable.Count == 1)
                        alias = TABLE_ALIAS_ELEMENT;
                    else if (colId.Parts.Count < 2 || colId.Parts[0].Contains("Result")) // Not L10N
                        alias = TABLE_ALIAS_RESULT;
                    else
                    {
                        // A result table was used to access the element table
                        alias = TABLE_ALIAS_ELEMENT;
                        colId = colId.RemovePrefix(1);
                    }
                }
                Type table;
                if (!dictAliasTable.TryGetValue(alias, out table))
                    throw new InvalidDataException(String.Format(Resources.ReportSpec_ReadColumns_Failed_to_find_the_table_for_the_column__0__, columnString));

                identifiers.Add(new ReportColumn(table, colId));
                reader.ReadEndElement();
            }
            reader.ReadEndElement();

            return new ReadOnlyCollection<ReportColumn>(identifiers);
        }

        private static void WriteColumns(XmlWriter writer,
                                         Enum elGroup,
                                         IEnumerable<ReportColumn> columns,
                                         IDictionary<Type, string> dictTableAlias)
        {
            if (columns != null)
            {
                writer.WriteStartElement(elGroup);
                foreach (var reportColumn in columns)
                {
                    writer.WriteStartElement(EL.column);
                    writer.WriteAttribute(ATTR.name, dictTableAlias[reportColumn.Table]);
                    writer.WriteString(reportColumn.Column.ToString());
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
        }
    }

    public sealed class ReportColumn : Immutable
    {
        public ReportColumn(Type table, Identifier column)
        {
            Table = table;
            Column = column;
        }

        public ReportColumn(Type table, params string[] idParts)
            : this(table, new Identifier(idParts))
        {
        }

        public Type Table { get; private set; }
        public TableType TableType { get { return GetTableType(Table); } }
        public Identifier Column { get; private set; }

        public string GetHql(IDictionary<Type, string> tableAliases)
        {
            EnsureAlias(tableAliases);

            return tableAliases[Table] + "." + Column; // Not L10N
        }

        public void EnsureAlias(IDictionary<Type, string> tableAliases)
        {
            EnsureAlias(Table, tableAliases);
        }

        public static void EnsureAlias(Type table, IDictionary<Type, string> tableAliases)
        {
            if (!tableAliases.ContainsKey(table))
                tableAliases.Add(table, "T" + (tableAliases.Count + 1)); // Not L10N
        }

        public static IEnumerable<KeyValuePair<Type, string>> Order(IDictionary<Type, string> tableAliases)
        {
            var listPairs = new List<KeyValuePair<Type, string>>(tableAliases);
            listPairs.Sort((p1, p2) => Comparer.Default.Compare(p1.Value, p2.Value));
            return listPairs;
        }

        public static TableType GetTableType(Type table)
        {
            foreach (var queryTable in (QueryTable[])table.GetCustomAttributes(typeof(QueryTable), false))
            {
                return queryTable.TableType;
            }
            return TableType.unknown;
        }

        #region object overrides

        public bool Equals(ReportColumn other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return ReferenceEquals(other.Table, Table) && Equals(other.Column, Column);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (ReportColumn)) return false;
            return Equals((ReportColumn) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Table.GetHashCode()*397) ^ Column.GetHashCode();
            }
        }

        public override string ToString()
        {
            return Table.Name + "." + Column; // Not L10N
        }

        #endregion
    }

    public class QueryDef
    {
        public IList<ReportColumn> Select { get; set; }
        public IList<ReportColumn> GroupBy { get; set; }
        public IList<ReportColumn> CrossTabHeaders { get; set; }
        public IList<ReportColumn> CrossTabValues { get; set; }
    }
}
