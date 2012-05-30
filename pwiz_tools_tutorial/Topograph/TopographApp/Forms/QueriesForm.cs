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
using pwiz.Topograph.Query;

namespace pwiz.Topograph.ui.Forms
{
    public partial class QueriesForm : WorkspaceForm
    {
        public QueriesForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            foreach (var query in BuiltInQueries.List)
            {
                lbxBuiltInQueries.Items.Add(query);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Repopulate();
        }

        public void Repopulate()
        {
            lbxCustomQueries.Items.Clear();
            foreach (var setting in Workspace.Settings.ListChildren())
            {
                if (setting.Name.StartsWith(WorkspaceSetting.QueryPrefix))
                {
                    lbxCustomQueries.Items.Add(setting.Name.Substring(WorkspaceSetting.QueryPrefix.Length));
                }
            }
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            if (args.GetEntities<WorkspaceSetting>().Count > 0)
            {
                Repopulate();
            }
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            OpenSelectedQuery(true);
        }

        private void OpenSelectedQuery(bool execute)
        {
            var queryName = (String)lbxCustomQueries.SelectedItem;
            var setting = Workspace.Settings.GetChild(WorkspaceSetting.QueryPrefix + queryName);
            if (setting == null)
            {
                return;
            }
            var queryForm = Program.FindOpenEntityForm<QueryForm>(setting);
            if (queryForm != null)
            {
                queryForm.Activate();
                return;
            }
            queryForm = new QueryForm(setting);
            queryForm.Show(DockPanel, DigitalRune.Windows.Docking.DockState.Document);
            if (execute)
            {
                queryForm.SetPreviewMode();
            }
        }

        private void btnNewQuery_Click(object sender, EventArgs e)
        {
            var setting = new WorkspaceSetting(Workspace);
            var queryForm = new QueryForm(setting);
            queryForm.Show(DockPanel, DigitalRune.Windows.Docking.DockState.Document);
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnRunQuery.Enabled = btnOpen.Enabled = btnDelete.Enabled = lbxCustomQueries.SelectedItems.Count > 0;
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            var queryName = (String) lbxCustomQueries.SelectedItem;
            if (queryName == null)
            {
                return;
            }
            String message = "Are you sure you want to delete the query '" + queryName + "'";
            var result = MessageBox.Show(this, message, Program.AppName, MessageBoxButtons.OKCancel,
                                         MessageBoxIcon.Warning);
            if (result == DialogResult.Cancel)
            {
                return;
            }
            using (var session = Workspace.OpenWriteSession())
            {
                var setting = Workspace.Settings.GetChild(WorkspaceSetting.QueryPrefix + queryName);
                var dbSetting = session.Get<DbSetting>(setting.Id);
                session.BeginTransaction();
                session.Delete(dbSetting);
                session.Transaction.Commit();
                Workspace.Settings.RemoveChild(setting.Name);
                Repopulate();
            }
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            OpenSelectedQuery(false);
        }

        private void btnRunQuery_Click(object sender, EventArgs e)
        {
            OpenSelectedQuery(true);
        }

        private void lbxBuiltInQueries_DoubleClick(object sender, EventArgs e)
        {
            ExecuteBuiltInQuery();
        }

        private void ExecuteBuiltInQuery()
        {
            var builtInQuery = lbxBuiltInQueries.SelectedItem as BuiltInQuery;
            if (builtInQuery == null)
            {
                return;
            }
            var form = BuiltInQueryForm.FindForm(builtInQuery);
            if (form != null)
            {
                form.Activate();
                return;
            }
            form = new BuiltInQueryForm(Workspace, builtInQuery);
            form.Show(DockPanel, DockState);
        }

        private void btnExecuteBuiltIn_Click(object sender, EventArgs e)
        {
            ExecuteBuiltInQuery();
        }

        private void lbxBuiltInQueries_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnExecuteBuiltIn.Enabled = lbxBuiltInQueries.SelectedItem != null;
        }
    }
}
