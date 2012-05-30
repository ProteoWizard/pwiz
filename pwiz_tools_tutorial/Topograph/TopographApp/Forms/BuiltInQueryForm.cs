using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Model;
using pwiz.Topograph.Query;

namespace pwiz.Topograph.ui.Forms
{
    public partial class BuiltInQueryForm : WorkspaceForm
    {
        public BuiltInQueryForm(Workspace workspace, BuiltInQuery builtInQuery) : base(workspace)
        {
            InitializeComponent();
            dataGridView1.Workspace = Workspace;
            BuiltInQuery = builtInQuery;
            TabText = Text = "Query:" + builtInQuery.Name;
        }

        public BuiltInQuery BuiltInQuery { get; private set; }

        protected override void OnShown(EventArgs e)
        {
            Requery();
        }

        private void btnExportResults_Click(object sender, EventArgs e)
        {
            dataGridView1.ExportResults(new ParsedQuery(BuiltInQuery.Hql), BuiltInQuery.Name);
        }

        private void btnRequery_Click(object sender, EventArgs e)
        {
            Requery();
        }

        private void Requery()
        {
            dataGridView1.ExecuteQuery(new ParsedQuery(BuiltInQuery.Hql));
        }

        public static BuiltInQueryForm FindForm(BuiltInQuery builtInQuery)
        {
            foreach (var form in Application.OpenForms)
            {
                var builtInQueryForm = form as BuiltInQueryForm;
                if (builtInQueryForm == null)
                {
                    continue;
                }
                if (builtInQueryForm.BuiltInQuery == builtInQuery)
                {
                    return builtInQueryForm;
                }
            }
            return null;
        }
    }
}
