using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class DatabaseSizeForm : WorkspaceForm
    {
        private readonly string[] _tableNames =
            {
                typeof (DbChangeLog).Name,
                typeof (DbChromatogram).Name,
                typeof (DbChromatogramSet).Name,
                typeof (DbLock).Name,
                typeof (DbModification).Name,
                typeof (DbMsDataFile).Name,
                typeof (DbPeak).Name,
                typeof (DbPeptide).Name,
                typeof (DbPeptideAnalysis).Name,
                typeof (DbPeptideFileAnalysis).Name,
                typeof (DbPeptideSearchResult).Name,
                typeof (DbSetting).Name,
                typeof (DbTracerDef).Name,
                typeof (DbWorkspace).Name,
            };
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
            var result = new List<TableData>();
            int iColName = reader.GetOrdinal("Name");
            int iColRows = reader.GetOrdinal("Rows");
            int iColDataFileSize = reader.GetOrdinal("Data_length");
            int iColFreeSpace = reader.GetOrdinal("Data_free");
            int iColIndexSize = reader.GetOrdinal("Index_length");
            while (reader.Read())
            {
                result.Add(new TableData
                               {
                                   Name = reader.GetString(iColName),
                                   RowCount = reader.GetInt64(iColRows),
                                   DataFileSize = reader.GetInt64(iColDataFileSize),
                                   FreeSpace = reader.GetInt64(iColFreeSpace),
                                   IndexSize = reader.GetInt64(iColIndexSize),
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
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            UpdateGrid();
        }
    }
}
