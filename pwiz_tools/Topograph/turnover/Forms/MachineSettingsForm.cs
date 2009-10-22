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
    public partial class MachineSettingsForm : WorkspaceForm
    {
        public MachineSettingsForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            tbxMassAccuracy.Text = workspace.GetMassAccuracy().ToString();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            Workspace.SetMassAccuracy(Convert.ToDouble(tbxMassAccuracy.Text));
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
