using System;
using System.IO;
using System.Windows.Forms;
using SkylineBatch.Properties;

namespace SkylineBatch
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

        public SkylineTypeControl()
        {
            InitializeComponent();

            radioButtonSkyline.Enabled = Installations.HasSkyline;
            radioButtonSkylineDaily.Enabled = Installations.HasSkylineDaily;
            
            // Chooses the first enabled option between Skyline, Skyline-daily, and custom path
            radioButtonSpecifySkylinePath.Checked = true;
            radioButtonSkylineDaily.Checked = radioButtonSkylineDaily.Enabled;
            radioButtonSkyline.Checked = radioButtonSkyline.Enabled;

            // Custom path set to saved value, defaults to C:\Program Files\Skyline if none saved
            if (!string.IsNullOrEmpty(Settings.Default.SkylineCustomCmdPath))
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
