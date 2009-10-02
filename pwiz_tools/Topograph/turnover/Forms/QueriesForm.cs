using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
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
            var queryName = (String) listBox1.SelectedItem;
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
        }

        private void btnNewQuery_Click(object sender, EventArgs e)
        {
            var setting = new WorkspaceSetting(Workspace);
            var queryForm = new QueryForm(setting);
            queryForm.Show(DockPanel, DigitalRune.Windows.Docking.DockState.Document);
        }
    }
}
