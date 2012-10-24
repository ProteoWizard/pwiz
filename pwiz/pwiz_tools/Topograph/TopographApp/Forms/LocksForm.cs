using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class LocksForm : WorkspaceForm
    {
        public LocksForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            Requery();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Requery();
        }

        private void Requery()
        {
            var locks = new List<DbLock>();
            using (var session = Workspace.OpenSession())
            {
                using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Querying Database"))
                {
                    var broker = new LongOperationBroker(b => session.CreateCriteria(typeof (DbLock)).List(locks),
                                                         longWaitDialog, session);
                    if (broker.LaunchJob())
                    {
                        BeginInvoke(new Action<List<DbLock>>(DisplayResults), locks);
                    }
                }
            }
        }

        private void DisplayResults(List<DbLock> locks)
        {
            dataGridView1.Rows.Clear();
            foreach (var dbLock in locks)
            {
                var row = dataGridView1.Rows[dataGridView1.Rows.Add()];
                row.Tag = dbLock.Id;
                row.Cells[colInstance.Index].Value = dbLock.InstanceIdGuid;
                if (dbLock.InstanceIdGuid.Equals(Workspace.InstanceId))
                {
                    row.Cells[colInstance.Index].Style.BackColor = Color.LightGreen;
                }
                if (dbLock.MsDataFileId != null)
                {
                    var msDataFile = Workspace.MsDataFiles.GetChild(dbLock.MsDataFileId.Value);
                    if (msDataFile == null)
                    {
                        row.Cells[colObject.Index].Value = "MSDataFile#" + dbLock.MsDataFileId;
                        row.Cells[colObject.Index].Style.BackColor = Color.Red;
                    }
                    else
                    {
                        row.Cells[colObject.Index].Value = "MSDataFile " + msDataFile.Name;
                    }
                }
                else if (dbLock.PeptideAnalysisId != null)
                {
                    row.Cells[colObject.Index].Value = "PeptideAnalysis#" + dbLock.PeptideAnalysisId;
                }
                row.Cells[colLockType.Index].Value = dbLock.LockType.ToString();
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0)
            {
                MessageBox.Show(this, "No rows selected", Program.AppName);
                return;
            }
            using (var session = Workspace.OpenWriteSession())
            {
                session.BeginTransaction();
                for (int i = 0; i < dataGridView1.SelectedRows.Count; i++)
                {
                    var row = dataGridView1.SelectedRows[i];
                    var dbLock = session.Get<DbLock>(row.Tag);
                    if (dbLock == null)
                    {
                        continue;
                    }
                    session.Delete(dbLock);
                }
                session.Transaction.Commit();
            }
            Requery();
        }
    }
}
