using System;
using System.Linq;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class RScriptForm : Form
    {
        private static string _lastChosenVersion;

        private readonly RDirectorySelector _rDirectorySelector;


        public RScriptForm(string currentPath, string currentVersion, PanoramaFile remoteFile, RDirectorySelector rDirectorySelector, IMainUiControl mainControl, SkylineBatchConfigManagerState state)
        {
            InitializeComponent();
            Icon = Program.Icon();

            var sortedRVersions = Settings.Default.RVersions.Keys.ToList();
            sortedRVersions.Sort();
            foreach (var version in sortedRVersions)
                comboRVersions.Items.Add(version);

            if (_lastChosenVersion == null || !Settings.Default.RVersions.ContainsKey(_lastChosenVersion))
            {
                if (Settings.Default.RVersions.Count > 0)
                    _lastChosenVersion = (string)comboRVersions.Items[comboRVersions.Items.Count - 1];
                else
                    _lastChosenVersion = null;
            }

            Version = currentVersion ?? _lastChosenVersion;
            _rDirectorySelector = rDirectorySelector;

            fileControl = new DownloadingFileControl(Resources.RScriptForm_RScriptForm_R_script_file_path,
                Resources.RScriptForm_RScriptForm_R_Script, currentPath,
                TextUtil.FILTER_R, remoteFile, false, "Download R script from Panorama", mainControl, 
                new Action<SkylineBatchConfigManagerState>((newState) => State = newState),
                new Func<SkylineBatchConfigManagerState>(() => State));
            fileControl.Dock = DockStyle.Fill;
            fileControl.Show();
            panelPath.Controls.Add(fileControl);

            State = state;

        }

        public DownloadingFileControl fileControl;

        public SkylineBatchConfigManagerState State { get; private set; }

        public string Version
        {
            get => (string)comboRVersions.SelectedItem;
            private set
            {
                if (!string.IsNullOrEmpty(value) && comboRVersions.Items.Contains(value))
                {
                    _lastChosenVersion = value;
                    comboRVersions.SelectedItem = value;
                    return;
                }

                comboRVersions.SelectedIndex = -1;
            }
        }

        public PanoramaFile RemoteFile => (PanoramaFile)fileControl.Server;

        public string Path => fileControl.Path;

        private void btnAddRLocation_Click(object sender, EventArgs e)
        {
            if (_rDirectorySelector.ShowAddDirectoryDialog(State))
                State = _rDirectorySelector.State;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            try
            {
                ReportInfo.ValidateRScript(Path, RemoteFile != null);
                ReportInfo.ValidateRVersion(Version);
            }
            catch (ArgumentException ex)
            {
                AlertDlg.ShowError(this, Program.AppName(), ex.Message);
                return;
            }
            DialogResult = DialogResult.OK;
        }
        
    }
}
