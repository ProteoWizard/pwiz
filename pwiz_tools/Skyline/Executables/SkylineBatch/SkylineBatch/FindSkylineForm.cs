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
    public partial class FindSkylineForm : Form
    {

        public FindSkylineForm(bool skylineWebBased, bool skylineDailyWebBased)
        {
            InitializeComponent();
            radioButtonSkyline.Enabled = skylineWebBased;
            radioButtonSkylineDaily.Enabled = skylineDailyWebBased;

            var skylineCommandPath = Settings.Default.SkylineCommandPath == null ? "" : Settings.Default.SkylineCommandPath;

            radioButtonSpecifySkylinePath.Checked = true;
            radioButtonSkylineDaily.Checked = radioButtonSkylineDaily.Enabled && skylineCommandPath.EndsWith(SkylineSettings.SkylineDailyRunnerExe);
            radioButtonSkyline.Checked = radioButtonSkyline.Enabled && skylineCommandPath.EndsWith(SkylineSettings.SkylineRunnerExe);

            if (radioButtonSpecifySkylinePath.Checked && skylineCommandPath != "")
                textBoxSkylinePath.Text = Path.GetDirectoryName(skylineCommandPath);

        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (!SkylineSettings.UpdateSettings(radioButtonSkyline.Checked, radioButtonSkylineDaily.Checked,
                textBoxSkylinePath.Text, out var errors))
            {
                MessageBox.Show(errors, string.Format(@"Cannot Update {0} Settings",
                    SkylineSettings.Skyline));
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var folderBrowserDlg = new FolderBrowserDialog())
            {
                folderBrowserDlg.Description =
                    string.Format("Select the {0} installation directory",
                        SkylineSettings.Skyline);
                folderBrowserDlg.ShowNewFolderButton = false;
                folderBrowserDlg.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (folderBrowserDlg.ShowDialog() == DialogResult.OK)
                {
                    textBoxSkylinePath.Text = folderBrowserDlg.SelectedPath;
                }
            }
        }

        private void radioButtonSpecifySkylinePath_CheckChanged(object sender, EventArgs e)
        {
            textBoxSkylinePath.Enabled = radioButtonSpecifySkylinePath.Checked;
            btnBrowse.Enabled = radioButtonSpecifySkylinePath.Checked;
        }
    }
}
