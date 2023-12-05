using System;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Common.GUI;
using SharedBatch.Properties;

namespace SharedBatch
{
    public partial class FindSkylineForm : Form
    {
        // The dialog that appears if the program was unable to find a Skyline Installation when first started
        // User must enter a path to a valid skyline installation to continue

        public FindSkylineForm(string appName, Icon icon)
        {
            InitializeComponent();
            Icon = icon;
            label1.Text = string.Format(Resources.FindSkylineForm_FindSkylineForm__0__requires_Skyline_to_run__but_did_not_find_an_administrative_or_web_based_installation_, appName);

            Shown += ((sender, args) => { Text = appName; });
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
            var skylineSettings = new SkylineSettings(SkylineType.Custom, null, textSkylineInstallPath.Text);
            try
            {
                skylineSettings.Validate();
            }
            catch (ArgumentException ex)
            {
                CommonAlertDlg.ShowException(this, ex);
                return;
            }
            Settings.Default.SkylineCustomCmdPath = skylineSettings.CmdPath;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
