using System;
using System.IO;
using System.Windows.Forms;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class SkylineTypeControl : UserControl, IVariableInputControl
    {
        public SkylineTypeControl(bool skyline, bool skylineDaily, bool custom, string path)
        {
            InitializeComponent();

            radioButtonSkyline.Enabled = Installations.HasSkyline;
            radioButtonSkylineDaily.Enabled = Installations.HasSkylineDaily;

            radioButtonSkyline.Checked = skyline;
            radioButtonSkylineDaily.Checked = skylineDaily;
            radioButtonSpecifySkylinePath.Checked = custom;
            if (custom)
            {
                textSkylineInstallationPath.Text = Path.GetDirectoryName(path);
            }
            else if (!string.IsNullOrEmpty(Settings.Default.SkylineCustomCmdPath))
            {
                textSkylineInstallationPath.Text = Path.GetDirectoryName(Settings.Default.SkylineCustomCmdPath);
            }
        }

        public SkylineType Type
        {
            get
            {
                if (radioButtonSkyline.Enabled && radioButtonSkyline.Checked)
                    return SkylineType.Skyline;
                if (radioButtonSkylineDaily.Enabled && radioButtonSkylineDaily.Checked)
                    return SkylineType.SkylineDaily;
                if (radioButtonSpecifySkylinePath.Checked)
                    return SkylineType.Custom;
                throw new Exception("No skyline type selected.");
            }
        }

        public string CommandPath => textSkylineInstallationPath.Text;


        public object GetVariable() => new SkylineSettings(Type, CommandPath);

        public bool IsValid(out string errorMessage)
        {
            errorMessage = null;
            try
            {
                new SkylineSettings(Type, CommandPath).Validate();
                return true;
            } catch (ArgumentException e)
            {
                errorMessage = e.Message;
                return false;
            }
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
                    textSkylineInstallationPath.Text = folderBrowserDlg.SelectedPath;
                }
            }
        }

        private void RadioButtonChanged(object sender, EventArgs e)
        {
            textSkylineInstallationPath.Enabled = radioButtonSpecifySkylinePath.Checked;
            btnBrowse.Enabled = radioButtonSpecifySkylinePath.Checked;
        }
    }
}
