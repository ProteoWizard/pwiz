using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using SharedBatch.Properties;

namespace SharedBatch
{
    public partial class SkylineTypeControl : UserControl, IValidatorControl
    {
        // A control used by a configuration set up manager to switch to a Skyline Installation that exists on this computer

        // Implements IValidatorControl:
        //    - GetVariable() returns a SkylineSettings instance using the Type from the radioButtons and CommandPath from textSkylineInstallationPath
        //    - IsValid() uses skylineSettings.Validate to determine if the selection is valid.

        private readonly IMainUiControl _mainUiControl;
        private readonly string _initialSkylineCmdPath;

        private readonly List<string> _runningConfigs;

        public SkylineTypeControl(IMainUiControl mainUiControl, bool skyline, bool skylineDaily, bool custom, string path, List<string> runningConfigs, ConfigManagerState baseState)
        {
            InitializeComponent();
            _mainUiControl = mainUiControl;
            _initialSkylineCmdPath = path;

            _runningConfigs = runningConfigs;

            BaseState = baseState;

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

        public ConfigManagerState BaseState;

        public SkylineTypeControl()
        {
            InitializeComponent();

            radioButtonSkyline.Enabled = SkylineInstallations.HasSkyline;
            radioButtonSkylineDaily.Enabled = SkylineInstallations.HasSkylineDaily;

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
                if (radioButtonSkyline.Checked)
                    return SkylineType.Skyline;
                if (radioButtonSkylineDaily.Checked)
                    return SkylineType.SkylineDaily;
                if (radioButtonSpecifySkylinePath.Checked)
                    return SkylineType.Custom;
                throw new ArgumentException(Resources.SkylineTypeControl_Type_No_existing_Skyline_type_selected__Please_select_a_Skyline_installation_that_exists_on_this_computer_);
            }
        }

        public string CommandPath => textSkylineInstallationPath.Text;


        public object GetVariable() => new SkylineSettings(Type, null, CommandPath);

        public bool IsValid(out string errorMessage)
        {
            errorMessage = null;
            try
            {
                var newSettings = new SkylineSettings(Type, null, CommandPath);
                newSettings.Validate();
                if (!newSettings.CmdPath.Equals(_initialSkylineCmdPath))
                {
                    BaseState.AskToReplaceAllSkylineVersions(newSettings, _runningConfigs, _mainUiControl, out _);
                }
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
                folderBrowserDlg.SelectedPath = FileUtil.GetInitialDirectory(textSkylineInstallationPath.Text, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
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

        public void SetInput(object variable)
        {
            radioButtonSpecifySkylinePath.Checked = true;
            textSkylineInstallationPath.Text = (string)variable;
        }
    }
}
