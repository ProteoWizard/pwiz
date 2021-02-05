using System;
using System.IO;
using System.Windows.Forms;
using SharedAutoQcBatch;
using SharedAutoQcBatch.Properties;

namespace SharedAutoQcBatch
{
    public partial class SkylineTypeControl : UserControl, IValidatorControl
    {
        // A control used by the InvalidConfigSetupForm to switch to a Skyline Installation that exists on this computer

        // Implements IValidatorControl:
        //    - GetVariable() returns a SkylineSettings instance using the Type from the radioButtons and CommandPath from textSkylineInstallationPath
        //    - IsValid() uses skylineSettings.Validate to determine if the selection is valid.

        public SkylineTypeControl(bool skyline, bool skylineDaily, bool custom, string path)
        {
            InitializeComponent();

            radioButtonSkyline.Enabled = SkylineInstallations.HasSkyline;
            radioButtonSkylineDaily.Enabled = SkylineInstallations.HasSkylineDaily;

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
                throw new ArgumentException("No valid skyline type selected.");
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
