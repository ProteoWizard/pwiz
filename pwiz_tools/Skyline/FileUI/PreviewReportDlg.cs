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
using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    /// <summary>
    /// Grid view which displays results of a query.
    /// </summary>
    public partial class PreviewReportDlg : FormEx
    {
        private const float FILL_WEIGHT = 1;
        public PreviewReportDlg()
        {
            InitializeComponent();

            Icon = Resources.Skyline;
        }

        public DataGridView DataGridView { get { return dataGridView; } }
        public int RowCount { get { return dataGridView.RowCount; } }
        public int ColumnCount { get { return dataGridView.ColumnCount; } }
        public IEnumerable<string> ColumnHeaderNames
        {
            get
            {
                foreach (DataGridViewColumn column in dataGridView.Columns)
                    yield return column.HeaderText;
            }
        }

        public void SetResults(ResultSet resultSet)
        {
            dataGridView.Columns.Clear();
            foreach (var columnInfo in resultSet.ColumnInfos)
            {
                if (columnInfo.IsHidden)
                    continue;

                DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn
                                                       {
                                                           HeaderText = columnInfo.Caption,
                                                           DefaultCellStyle = {Format = columnInfo.Format},
                                                           FillWeight = FILL_WEIGHT
                                                       };
                if (columnInfo.IsNumeric)
                {
                    column.DefaultCellStyle.NullValue = TextUtil.EXCEL_NA; // Not L10N
                }
                dataGridView.Columns.Add(column);
            }
            for (int iRow = 0; iRow < resultSet.RowCount; iRow++ )
            {
                var gridRow = dataGridView.Rows[dataGridView.Rows.Add()];
                for (int iColumn = 0, iColumnGrid = 0; iColumn < resultSet.ColumnInfos.Count; iColumn++ )
                {
                    if (resultSet.ColumnInfos[iColumn].IsHidden)
                        continue;

                    gridRow.Cells[iColumnGrid++].Value = resultSet.GetValue(iRow, iColumn);
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }

            base.OnKeyDown(e);
        }

        public void OkDialog()
        {
            Close();
        }
    }
}
