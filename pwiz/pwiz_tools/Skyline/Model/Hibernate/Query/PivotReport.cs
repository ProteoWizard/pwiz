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
using System.Linq;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Hibernate.Query
{
    public class PivotReport : SimpleReport
    {
        public IList<ReportColumn> CrossTabHeaders { get; set; }
        public IList<ReportColumn> CrossTabValues { get; set; }
        public IList<ReportColumn> GroupByColumns { get; set; }
        public override ReportSpec GetReportSpec(String name)
        {
            return new ReportSpec(name, new QueryDef
                                            {
                                                Select = Columns,
                                                GroupBy = GroupByColumns,
                                                CrossTabHeaders = CrossTabHeaders,
                                                CrossTabValues = CrossTabValues
                                            });
        }
        public override ResultSet Execute(Database database)
        {
            List<ReportColumn> selectColumns = UnionAll(
                Columns, GroupByColumns, CrossTabHeaders, CrossTabValues);
            ResultSet flatResultSet = database.ExecuteQuery(selectColumns);
            return new Pivoter(this, flatResultSet).GetPivotedResultSet();
        }

        private static List<ReportColumn> UnionAll(params IList<ReportColumn>[] lists)
        {
            List<ReportColumn> result = new List<ReportColumn>();
            HashSet<ReportColumn> set = new HashSet<ReportColumn>();
            foreach (var list in lists)
            {
                foreach (var id in list)
                {
                    if (set.Add(id))
                    {
                        result.Add(id);
                    }
                }
            }
            return result;
        }
        /// <summary>
        /// Transforms a ResultSet so that rows are grouped by "pivotRowIdColumn", and
        /// some of the columns are made into a crosstab identified by "pivotNameColumn".
        /// </summary>
        class Pivoter
        {
            private readonly ResultSet _plainResultSet;
            private readonly List<RowKey> _pivotNames;
            private readonly IList<ColumnInfo> _normalColumns;
            private readonly IList<ColumnInfo> _pivotColumns;
            private readonly IList<ReportColumn> _crosstabColumns;
            private readonly IList<ReportColumn> _groupByColumns;
            private readonly ResultSet _pivotedResultSet;

            public Pivoter(PivotReport pivotReport, ResultSet plainResultSet)
            {
                _plainResultSet = plainResultSet;
                _normalColumns = new List<ColumnInfo>();
                _pivotColumns = new List<ColumnInfo>();
                _groupByColumns = pivotReport.GroupByColumns;
                _crosstabColumns = pivotReport.CrossTabHeaders;
                foreach (ReportColumn reportColumn in pivotReport.Columns)
                {
                    _normalColumns.Add(plainResultSet.GetColumnInfo(reportColumn));
                }
                foreach (ReportColumn reportColumn in pivotReport.CrossTabValues)
                {
                    _pivotColumns.Add(plainResultSet.GetColumnInfo(reportColumn));
                }
                Dictionary<RowKey, Dictionary<RowKey, int>> rowsById
                    = new Dictionary<RowKey, Dictionary<RowKey, int>>();
                ICollection<RowKey> pivotNameSet = new HashSet<RowKey>();
                for (int i = 0; i < plainResultSet.RowCount; i++)
                {
                    RowKey groupByKey = new RowKey();
                    foreach(var reportColumn in _groupByColumns)
                    {
                        groupByKey.Add(plainResultSet.GetValue(i, reportColumn));
                    }
                    RowKey crossTabKey = new RowKey();
                    foreach (var reportColumn in _crosstabColumns)
                    {
                        crossTabKey.Add(plainResultSet.GetValue(i, reportColumn));
                    }
                    Dictionary<RowKey, int> pivotNameDict;
                    if (!rowsById.TryGetValue(groupByKey, out pivotNameDict))
                    {
                        pivotNameDict = new Dictionary<RowKey, int>();
                        rowsById.Add(groupByKey, pivotNameDict);
                    }
                    if (pivotNameDict.ContainsKey(crossTabKey))
                    {
                        continue;
                    }
                    pivotNameDict.Add(crossTabKey, i);
                    pivotNameSet.Add(crossTabKey);
                }
                _pivotNames = new List<RowKey>(pivotNameSet);
                _pivotNames.Sort();
                List<Object[]> rows = new List<object[]>();
                foreach (Dictionary<RowKey, int> dict in rowsById.Values)
                {
                    rows.Add(PivotRow(dict));
                }
                _pivotedResultSet = new ResultSet(GetPivotedColumnInfos(), rows);
            }

            public ResultSet GetPivotedResultSet()
            {
                return _pivotedResultSet;
            }

            /// <summary>
            /// Returns the set of ColumnInfos for the transformed ResultSet.
            /// </summary>
            private List<ColumnInfo> GetPivotedColumnInfos()
            {
                List<ColumnInfo> result = new List<ColumnInfo>();
                result.AddRange(_normalColumns);
                foreach (RowKey pivotName in _pivotNames)
                {
                    foreach (ColumnInfo columnInfo in _pivotColumns)
                    {
                        result.Add(QualifyColumnInfo(columnInfo, pivotName));
                    }
                }
                return result;
            }

            /// <summary>
            /// Qualifies a ColumnInfo with the crosstab name.  Ensures that the Identifier
            /// in the ColumnInfo will be unique in the new ResultSet, and prepends the
            /// crosstab name to the ColumnInfo caption.
            /// </summary>
            private static ColumnInfo QualifyColumnInfo(ColumnInfo columnInfo, RowKey rowKey)
            {
                List<String> parts = new List<string>();
                String caption = "";
                parts.Add("pivot");

                foreach (var part in rowKey)
                {
                    String str = part == null ? "null" : part.ToString();
                    parts.Add(str);
                    caption += str + " ";
                }
                parts.AddRange(columnInfo.ReportColumn.Column.Parts);
                Identifier identifier = new Identifier(parts);

                // HACK: This is a hack to hide certain pivot columns which will never contain
                //       values in the case of label types and ratios.
                // TODO(nicksh): Something more general
                bool hidden = false;
                const string nameTotalRatioTo = "TotalAreaRatioTo";
                const string nameRatioTo = "AreaRatioTo";
                string colCap = columnInfo.Caption;
                if (Equals(colCap, "TotalAreaRatio") || Equals(colCap, "AreaRatio"))
                {
                    // HACK: Unfortunately, the default internal standard label type
                    //       is needed in order to do this correctly.  The string "heavy"
                    //       should be the default 99% of the time, and if someone used
                    //       the string "heavy" for something else, and complained about
                    //       losing this column in this case, we could always tell them
                    //       change the name of the label type to something else.
                    hidden = RowKeyContains(rowKey, IsotopeLabelType.heavy.ToString());
                }
                // Hide RatioTo columns for the label type for which they are a ratio
                else if (colCap.StartsWith(nameTotalRatioTo))
                {
                    hidden = RowKeyContains(rowKey, colCap.Substring(nameTotalRatioTo.Length));
                }
                else if (colCap.StartsWith(nameRatioTo))
                {
                    hidden = RowKeyContains(rowKey, colCap.Substring(nameRatioTo.Length));                    
                }

                return new ColumnInfo
                {
                    Format = columnInfo.Format,
                    ReportColumn = new ReportColumn(columnInfo.ReportColumn.Table, identifier),
                    Caption = caption + columnInfo.Caption,
                    ColumnType = columnInfo.ColumnType,
                    IsHidden = hidden
                };
            }

// ReSharper disable ParameterTypeCanBeEnumerable.Local
            private static bool RowKeyContains(RowKey rowKey, string keyPart)
// ReSharper restore ParameterTypeCanBeEnumerable.Local
            {
                string keyPartLower = keyPart.ToLower();
                string keyPartSpaces = keyPartLower.Replace('_', ' ');  // Ugh.

                foreach (var part in rowKey)
                {
                    if (part == null)
                        continue;
                    string partLower = part.ToString().ToLower();
                    if (Equals(partLower, keyPartLower) || Equals(partLower, keyPartSpaces))
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Transforms a set of rows in a group into a crosstab.
            /// </summary>
            /// <param name="rows">Dictionary from crosstab value to the row index in the original ResultSet.
            /// Each of these rows has the same value for _groupByColumn.
            /// </param>
            /// <returns></returns>
            private Object[] PivotRow(Dictionary<RowKey, int> rows)
            {
                List<Object> values = new List<object>();
                // First add the values that are common to all rows in the group.
                int firstRow = rows.Values.First();
                foreach (ColumnInfo columnInfo in _normalColumns)
                {
                    values.Add(_plainResultSet.GetValue(firstRow, columnInfo.ReportColumn));
                }
                // Then, for each of the crosstab values, add the additional columns.
                foreach (RowKey pivotName in _pivotNames)
                {
                    int? rowIndex = null;
                    if (rows.ContainsKey(pivotName))
                    {
                        rowIndex = rows[pivotName];
                    }
                    foreach (ColumnInfo columnInfo in _pivotColumns)
                    {
                        Object value = null;
                        if (rowIndex.HasValue)
                        {
                            value = _plainResultSet.GetValue(rowIndex.Value, columnInfo.ReportColumn);
                        }
                        values.Add(value);
                    }
                }
                return values.ToArray();
            }
        }

        /// <summary>
        /// List of column values.  This is used both for grouping rows in doing the crosstab.
        /// Also, each set of crosstab columns is identified by a RowKey.
        /// </summary>
        class RowKey : List<Object>, IComparable<RowKey>
        {
            public override bool Equals(object obj)
            {
                var list = obj as RowKey;
                if (list == null)
                {
                    return false;
                }
                return ArrayUtil.EqualsDeep(this, list);
            }

            public override int GetHashCode()
            {
                return this.GetHashCodeDeep();
            }

            /// <summary>
            /// Compare to another RowKey.  Used for determining the order that crosstab columns
            /// are displayed in.
            /// </summary>
            public int CompareTo(RowKey that)
            {
                if (that.Count != Count)
                {
                    return 0;
                }
                for (int i = 0; i < Count; i++)
                {
                    int value = Compare(this[i], that[i]);
                    if (value != 0)
                    {
                        return value;
                    }
                }
                return 0;
            }

            /// <summary>
            /// Compares two objects.  If they are of the same type and implement IComparable, 
            /// then invoke IComparable.CompareTo, otherwise, compare the objects as strings.
            /// This method is used to determine the order in which crosstab columns are
            /// displayed.
            /// </summary>
            private static int Compare(Object o1, Object o2)
            {
                if (o1 == null)
                {
                    return o2 == null ? 0 : -1;
                }
                if (o2 == null)
                {
                    return 1;
                }
                if (o1.GetType() == o2.GetType())
                {
                    var comparable1 = o1 as IComparable;
                    if (comparable1 != null)
                    {
                        return comparable1.CompareTo(o2);
                    }
                }
                return String.CompareOrdinal(o1.ToString(), o2.ToString());
            }

        }
    }
}
