using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            var CmdPath = Path.Combine(textSkylineInstallPath.Text, Installations.SkylineCmdExe);
            if (File.Exists(CmdPath))
            {
                if (CmdPath.Contains("SkylineDaily"))
                    Settings.Default.SkylineDailyAdminCmdPath = CmdPath;
                else
                    Settings.Default.SkylineAdminCmdPath = CmdPath;
                DialogResult = DialogResult.OK;
                Close();
            }
            MessageBox.Show("No SkylineCmd.exe file in " + textSkylineInstallPath.Text, "Not a valid Skyline installation.", MessageBoxButtons.OK, MessageBoxIcon.Error);

        }
    }
}
