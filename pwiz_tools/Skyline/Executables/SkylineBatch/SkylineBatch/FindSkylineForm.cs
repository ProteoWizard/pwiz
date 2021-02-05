using System;
using System.Windows.Forms;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class FindSkylineForm : Form
    {
        // The dialog that appears if Skyline Batch was unable to find a Skyline Installation when first started
        // User must enter a path to a valid skyline installation to continue

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
