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

namespace pwiz.Topograph.ui.Forms
{
    public partial class QueriesForm : WorkspaceForm
    {
        public QueriesForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Repopulate();
        }

        public void Repopulate()
        {
            listBox1.Items.Clear();
            foreach (var setting in Workspace.Settings.ListChildren())
            {
                if (setting.Name.StartsWith(WorkspaceSetting.QueryPrefix))
                {
                    listBox1.Items.Add(setting.Name.Substring(WorkspaceSetting.QueryPrefix.Length));
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
            var queryName = (String)listBox1.SelectedItem;
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
            btnRunQuery.Enabled = btnOpen.Enabled = btnDelete.Enabled = listBox1.SelectedItems.Count > 0;
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            var queryName = (String) listBox1.SelectedItem;
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
    }
}
