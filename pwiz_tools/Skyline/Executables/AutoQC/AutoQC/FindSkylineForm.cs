using System;
using System.IO;
using System.Windows.Forms;
using AutoQC.Properties;
using SharedBatch;

namespace AutoQC
{
    public partial class FindSkylineForm : Form
    {

        public FindSkylineForm()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var folderBrowserDlg = new FolderBrowserDialog())
            {
                folderBrowserDlg.ShowNewFolderButton = false;
                folderBrowserDlg.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (folderBrowserDlg.ShowDialog() == DialogResult.OK)
                {
                    textSkylineInstallPath.Text = folderBrowserDlg.SelectedPath;
                }
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            var skylineSettings = new SkylineSettings(SkylineType.Custom, textSkylineInstallPath.Text);
            try
            {
                skylineSettings.Validate();
            }
            catch (ArgumentException ex)
            {
                AlertDlg.ShowError(this, ex.Message);
                return;
            }
            Settings.Default.SkylineCustomCmdPath = skylineSettings.CmdPath;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
