using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;
using pwiz.PanoramaClient;
using PanoramaServer = pwiz.PanoramaClient.PanoramaServer;
using AlertDlg = SharedBatch.AlertDlg;

namespace SkylineBatch
{
    public partial class RemoteSourceForm : Form
    {
        private readonly ImmutableDictionary<string, RemoteFileSource> _remoteFileSources;
        private CancellationTokenSource _cancelSource;
        private readonly string _editingSourceName;
        private readonly IMainUiControl _mainControl;
        private readonly bool _adding;
        private readonly RemoteFileSource _initialRemoteSource;
        private readonly SkylineBatchConfigManagerState _initialState;
        private readonly bool _preferPanoramaSource;
        private readonly bool _fileRequired;

        public RemoteSourceForm(RemoteFileSource editingRemoteSource, IMainUiControl mainControl,
            SkylineBatchConfigManagerState state, bool preferPanoramaSource, bool fileRequired = false)
        {
            InitializeComponent();
            _remoteFileSources = state.FileSources;
            _editingSourceName = null;
            _mainControl = mainControl;
            State = state;
            _initialState = state.Copy();
            _initialRemoteSource = editingRemoteSource;
            _adding = editingRemoteSource == null;
            _preferPanoramaSource = preferPanoramaSource;
            _fileRequired = fileRequired;

            if (editingRemoteSource != null)
            {
                _editingSourceName = editingRemoteSource.Name;
                textName.Text = editingRemoteSource.Name;
                textFolderUrl.Text = editingRemoteSource.URI.AbsoluteUri;
                textUserName.Text = editingRemoteSource.Username;
                textPassword.Text = editingRemoteSource.Password;
                checkBoxNoEncryption.Checked = !editingRemoteSource.Encrypt;
            }

            Icon = Program.Icon();
        }

        public RemoteFileSource RemoteFileSource { get; private set; }
        public SkylineBatchConfigManagerState State { get; private set; }

        private void textPassword_TextChanged(object sender, EventArgs e)
        {
            checkBoxNoEncryption.Enabled = textPassword.Text.Length > 0;
            if (textPassword.Text.Length == 0)
                checkBoxNoEncryption.Checked = false;
        }

        private void checkBoxNoEncryption_CheckedChanged(object sender, EventArgs e)
        {
            textPassword.PasswordChar = checkBoxNoEncryption.Checked ? '\0' : '*';
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            _cancelSource = new CancellationTokenSource();

            if (_remoteFileSources.ContainsKey(textName.Text) && !textName.Text.Equals(_editingSourceName))
            {
                AlertDlg.ShowError(this, Program.AppName(),
                    string.Format(
                        Resources
                            .RemoteSourceForm_btnSave_Click_Another_remote_file_location_with_the_name__0__already_exists__Please_choose_a_unique_name_,
                        textName.Text));
                return;
            }

            try
            {
                RemoteFileSource = RemoteFileSource.RemoteSourceFromUi(textName.Text, textFolderUrl.Text,
                    textUserName.Text, textPassword.Text, !checkBoxNoEncryption.Checked);
                if (_adding)
                    State.UserAddRemoteFileSource(RemoteFileSource, _preferPanoramaSource, _mainControl);
                else
                {
                    State.ReplaceRemoteFileSource(_initialRemoteSource, RemoteFileSource, _mainControl);
                }

                DialogResult = DialogResult.OK;
            }
            catch (ArgumentException ex)
            {
                AlertDlg.ShowError(this, Program.AppName(), ex.Message);
            }
        }

        private void CancelValidate()
        {
            if (_cancelSource != null)
                _cancelSource.Cancel();
        }

        private void RemoteSourceForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            CancelValidate();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            State = _initialState;
        }

        public void OpenFromPanorama(object sender, EventArgs args)
        {
            var server = new PanoramaServer(new Uri(textServerName.Text));
            var panoramaServers = new List<PanoramaServer>() { server };

            var state = string.Empty;
            if (!string.IsNullOrEmpty(Settings.Default.PanoramaTreeState))
            {
                state = Settings.Default.PanoramaTreeState;
            }

            try
            {
                if (_fileRequired) // If file is required use PanoramaFilePicker
                {
                    using (PanoramaFilePicker dlg = new PanoramaFilePicker(panoramaServers, true, state, false))
                    {

                        dlg.InitializeDialog();
                        if (dlg.ShowDialog() != DialogResult.Cancel)
                        {
                            Settings.Default.PanoramaTreeState = dlg.TreeState;
                            Settings.Default.ShowPanormaSkyFiles = dlg.ShowingSky;
                            textFolderUrl.Text = dlg.FileUrl;

                        }
                        Settings.Default.PanoramaTreeState = dlg.TreeState;
                        Settings.Default.ShowPanormaSkyFiles = dlg.ShowingSky;
                    }
                }
                else // if file not required use PanoramaDirectoryPicker
                {
                    using (PanoramaDirectoryPicker dlg = new PanoramaDirectoryPicker(panoramaServers, state))
                    {

                        // dlg.InitializeDialog();
                        if (dlg.ShowDialog() != DialogResult.Cancel)
                        {
                            //Find better way to do this. Will all URL's have /_webdav/?
                            string url = $"{textServerName.Text}/_webdav{dlg.Folder}";
                            textFolderUrl.Text = url;

                        }
                    }
                }
                // using (PanoramaFilePicker dlg = new PanoramaFilePicker(panoramaServers, true, state, false))
               
            }
            catch (Exception e)
            {
                AlertDlg.ShowError(this, Program.AppName(), e.Message);
            }

            Settings.Default.Save();
        }

    }
}