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
using System.Collections;
using System.Collections.Generic;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Hibernate.Query
{
    public class ResultSet
    {
        private readonly List<Object[]> _rows;
        private readonly Dictionary<ReportColumn, int> _columnIndexes;
        public ResultSet(IList<ColumnInfo> columns, IList rows)
        {
            ColumnInfos = columns;
            _rows = new List<Object[]>(rows.Count);
            _columnIndexes = new Dictionary<ReportColumn, int>();
            for (int i = 0; i < columns.Count; i++ )
            {
                _columnIndexes.Add(columns[i].ReportColumn, i);
            }

            foreach (var row in rows)
            {
                if (row is Object[])
                {
                    _rows.Add((Object[])row);
                }
                else
                {
                    _rows.Add(new[] { row });
                }
            }
        }

        public IList<ColumnInfo> ColumnInfos { get; private set; }
        public int RowCount { get { return _rows.Count;} }

        public Object[] GetRow(int index)
        {
            return _rows[index];
        }

        public Object GetValue(int rowIndex, int columnIndex)
        {
            Object[] row = _rows[rowIndex];
            if (row.Length <= columnIndex)
            {
                return null;
            }
            return row[columnIndex];
        }

        public String FormatValue(int rowIndex, int columnIndex, IFormatProvider formatProvider)
        {
            Object value = GetValue(rowIndex, columnIndex);
            if (value == null)
            {
                return GetNullValueString(columnIndex, "");
            }
            ColumnInfo columnInfo = ColumnInfos[columnIndex];
            if (columnInfo.Format != null || value is double || value is float)
            {
                try
                {
                    double dblValue = (double)Convert.ChangeType(value, typeof(Double));
                    return (columnInfo.Format != null ?
                        dblValue.ToString(columnInfo.Format, formatProvider) :
                        dblValue.ToString(formatProvider));
                }
// ReSharper disable EmptyGeneralCatchClause
                catch
                {
                    // ignore
                }
// ReSharper restore EmptyGeneralCatchClause
            }
            return value.ToString();
        }

        public Object GetValue(int rowIndex, ReportColumn identifier)
        {
            return GetValue(rowIndex, _columnIndexes[identifier]);
        }

        public String GetNullValueString(int columnIndex, String defaultValue)
        {
            return (ColumnInfos[columnIndex].IsNumeric ? "#N/A" : defaultValue);
        }

        public ColumnInfo GetColumnInfo(ReportColumn identifier)
        {
            return ColumnInfos[_columnIndexes[identifier]];
        }
    }

    public class ColumnInfo
    {
        public ReportColumn ReportColumn { get; set; }
        public String Format { get; set; }
        public String Caption { get; set; }
        public Type ColumnType { get; set; }
        private bool _isHidden;

        public bool IsHidden
        {
            get { return _isHidden || Caption == null; }
            set { _isHidden = value; }
        }

        public bool IsNumeric
        {
            get
            {
                return Equals(ColumnType, typeof (int)) ||
                       Equals(ColumnType, typeof (double)) ||
                       Equals(ColumnType, typeof (int?)) ||
                       Equals(ColumnType, typeof (double?));
            }
        }
    }
}
