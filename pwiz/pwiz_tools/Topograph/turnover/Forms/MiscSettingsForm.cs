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
    public partial class MiscSettingsForm : WorkspaceForm
    {
        public MiscSettingsForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            tbxMassAccuracy.Text = workspace.GetMassAccuracy().ToString();
            comboTracerCountType.Items.Add("Tracer Amounts");
            comboTracerCountType.Items.Add("Precursor Enrichments");
            comboTracerCountType.SelectedIndex = (int) Workspace.GetDefaultPeptideQuantity();
            cbxWeightSignalAbsenceMore.Checked = Workspace.GetErrOnSideOfLowerAbundance();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            using (Workspace.GetWriteLock())
            {
                Workspace.SetMassAccuracy(Convert.ToDouble(tbxMassAccuracy.Text));
                Workspace.SetDefaultPeptideQuantity((PeptideQuantity)comboTracerCountType.SelectedIndex);
                Workspace.SetErrOnSideOfLowerAbundance(cbxWeightSignalAbsenceMore.Checked);
            }
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
