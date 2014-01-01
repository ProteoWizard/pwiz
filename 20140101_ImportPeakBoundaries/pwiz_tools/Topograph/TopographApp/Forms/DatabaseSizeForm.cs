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
using System.Collections.Generic;
using System.Diagnostics;
using NHibernate;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class DatabaseSizeForm : WorkspaceForm
    {
        public DatabaseSizeForm(Workspace workspace)
            : base(workspace)
        {
            InitializeComponent();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateGrid();
        }

        public void UpdateGrid()
        {
            using (var session = Workspace.OpenSession())
            {
                IEnumerable<TableData> tableDatas = null;
                using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Querying Table Sizes"))
                {
                    var broker = new LongOperationBroker(b => { tableDatas = RequeryGrid(b, session); }, longWaitDialog
                                                         , session);
                    if (!broker.LaunchJob() || tableDatas == null)
                    {
                        return;
                    }
                    dataGridView1.Rows.Clear();
                    foreach (var tableData in tableDatas)
                    {
                        var row = dataGridView1.Rows[dataGridView1.Rows.Add()];
                        row.Cells[colTableName.Index].Value = tableData.Name;
                        row.Cells[colRowCount.Index].Value = tableData.RowCount;
                        row.Cells[colDataFileSize.Index].Value = tableData.DataFileSize;
                        row.Cells[colFreeSpace.Index].Value = tableData.FreeSpace;
                        row.Cells[colIndexSize.Index].Value = tableData.IndexSize;
                    }
                }
            }
        }

        private IEnumerable<TableData> RequeryGrid(LongOperationBroker longOperationBroker, ISession session)
        {
            var connection = session.Connection;
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SHOW TABLE STATUS";
            var reader = cmd.ExecuteReader();
            Debug.Assert(null != reader);
            var result = new List<TableData>();
            int iColName = reader.GetOrdinal("Name");
            int iColRows = reader.GetOrdinal("Rows");
            int iColDataFileSize = reader.GetOrdinal("Data_length");
            int iColFreeSpace = reader.GetOrdinal("Data_free");
            int iColIndexSize = reader.GetOrdinal("Index_length");
            int iColEngine = reader.GetOrdinal("Engine");
            while (reader.Read())
            {
                result.Add(new TableData
                               {
                                   Name = reader.GetString(iColName),
                                   RowCount = reader.GetInt64(iColRows),
                                   DataFileSize = reader.GetInt64(iColDataFileSize),
                                   FreeSpace = reader.GetInt64(iColFreeSpace),
                                   IndexSize = reader.GetInt64(iColIndexSize),
                                   Engine = reader.GetString(iColEngine),
                               });
            }
            return result;
        }

        private class TableData
        {
            public String Name { get; set; }
            public long RowCount { get; set; }
            public long DataFileSize { get; set; }
            public long FreeSpace { get; set; }
            public long IndexSize { get; set; }
            public String Engine { get; set; }
        }

        private void BtnRefreshOnClick(object sender, EventArgs e)
        {
            UpdateGrid();
        }
    }
}
