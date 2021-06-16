using System;
using System.Linq;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class RScriptForm : Form
    {
        private static string _lastChosenVersion = null;
        private static string _lastUsedPath = null;

        private readonly RDirectorySelector _rDirectorySelector;
        private string _version;
        private DownloadingFileControl _fileControl;


        public RScriptForm(string currentPath, string currentVersion, PanoramaFile remoteFile, RDirectorySelector rDirectorySelector)
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

            _fileControl = new DownloadingFileControl(Resources.RScriptForm_RScriptForm_R_script_file_path,
                Resources.RScriptForm_RScriptForm_R_Script, currentPath,
                TextUtil.FILTER_R, remoteFile, false);
            _fileControl.Dock = DockStyle.Fill;
            _fileControl.Show();
            panelPath.Controls.Add(_fileControl);
            
        }

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

        public PanoramaFile RemoteFile => (PanoramaFile)_fileControl.Server;

        public string Path => _fileControl.Path;

        private void btnAddRLocation_Click(object sender, EventArgs e)
        {
            _rDirectorySelector.ShowAddDirectoryDialog();
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
