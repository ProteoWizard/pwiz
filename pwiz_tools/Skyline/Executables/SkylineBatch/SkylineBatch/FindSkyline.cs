using System;
using System.IO;
using System.Windows.Forms;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class FindSkyline : Form
    {
        public FindSkyline()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var folderBrowserDlg = new FolderBrowserDialog())
            {
                folderBrowserDlg.Description =
                    string.Format(Resources.FindSkylineForm_btnBrowse_Click_Select_the__0__installation_directory,
                        Installations.Skyline);
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
            var cmdPath = Path.Combine(textSkylineInstallPath.Text, Installations.SkylineCmdExe);
            if (File.Exists(cmdPath))
            {
                Settings.Default.SkylineCustomCmdPath = cmdPath;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            MessageBox.Show(string.Format(Resources.FindSkyline_btnOkClick_No_SkylineCmd_exe_file_in__0__, textSkylineInstallPath.Text),
                Resources.FindSkyline_btnOkClick_Not_a_valid_Skyline_installation___, MessageBoxButtons.OK, MessageBoxIcon.Error);

        }
    }
}
