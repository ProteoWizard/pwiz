using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.GUI;
using SharedBatch;
using SkylineBatch.Properties;
using pwiz.PanoramaClient;
using PanoramaServer = pwiz.PanoramaClient.PanoramaServer;
using PanoramaUtil = pwiz.PanoramaClient.PanoramaUtil;

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

        public RemoteSourceForm(RemoteFileSource editingRemoteSource, IMainUiControl mainControl,
            SkylineBatchConfigManagerState state, bool preferPanoramaSource)
        {
            InitializeComponent();
            Bitmap bmp = (Bitmap)this.btnOpenFromPanorama.Image;
            bmp.MakeTransparent(bmp.GetPixel(0,0));


            _remoteFileSources = state.FileSources;
            _editingSourceName = null;
            _mainControl = mainControl;
            State = state;
            _initialState = state.Copy();
            _initialRemoteSource = editingRemoteSource;
            _adding = editingRemoteSource == null;
            _preferPanoramaSource = preferPanoramaSource;

            if (editingRemoteSource != null)
            {
                _editingSourceName = editingRemoteSource.Name;
                SelectedPath = editingRemoteSource.SelectedPath;
                textName.Text = editingRemoteSource.Name;
                textFolderUrl.Text = Uri.UnescapeDataString(editingRemoteSource.URI.AbsoluteUri);
                textUserName.Text = editingRemoteSource.Username;
                textPassword.Text = editingRemoteSource.Password;
                textServerName.Text = Uri.UnescapeDataString(editingRemoteSource.URI.GetLeftPart(UriPartial.Authority));
                checkBoxNoEncryption.Checked = !editingRemoteSource.Encrypt;
            }

            Icon = Program.Icon();
        }

        public RemoteFileSource RemoteFileSource { get; private set; }
        public SkylineBatchConfigManagerState State { get; private set; }
        private bool PanoramaSource { get; set; }
        private string SelectedPath { get; set; }


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
                AlertDlg.ShowError(this,
                    string.Format(
                        Resources
                            .RemoteSourceForm_btnSave_Click_Another_remote_file_location_with_the_name__0__already_exists__Please_choose_a_unique_name_,
                        textName.Text));
                return;
            }

            try
            {
                RemoteFileSource = RemoteFileSource.RemoteSourceFromUi(textName.Text, textFolderUrl.Text,
                    textUserName.Text, textPassword.Text, !checkBoxNoEncryption.Checked, PanoramaSource, SelectedPath);
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
                CommonAlertDlg.ShowException(this, ex);
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

        public void ConfigurePanoramaServer(string serverUrl, string userName, string password)
        {
            textServerName.Text = serverUrl;
            textUserName.Text = userName;
            textPassword.Text = password;
        }

        public void btn_OpenFromPanorama(object sender, EventArgs args)
        {
            OpenFromPanorama();
        }


        public void OpenFromPanorama()
        {
            PanoramaServer server;
            if (textUserName.Text != "" && textPassword.Text != "")
            {
                server = new PanoramaServer(new Uri(textServerName.Text), textUserName.Text, textPassword.Text);

            }
            else
            {
                server = new PanoramaServer(new Uri(textServerName.Text));
            }
            var panoramaServers = new List<PanoramaServer>() { server };

            var state = string.Empty;
            /*if (!string.IsNullOrEmpty(Settings.Default.PanoramaTreeState))
            {
                state = Settings.Default.PanoramaTreeState;
            }*/

            var decodedUrl = Uri.UnescapeDataString(textFolderUrl.Text).Replace(PanoramaUtil.FILES, "");
            try
            {
                
                using (var dlg = new PanoramaDirectoryPicker(panoramaServers, state, false, decodedUrl))
                {
                    dlg.InitializeDialog(); // TODO: Should use a LongOperationRunner to show busy-wait UI
                    if (dlg.ShowDialog() != DialogResult.Cancel)
                    {
                        dlg.OkButtonText = "Select";
                        var url = PanoramaFolderBrowser.GetSelectedUri(dlg.FolderBrowser, true) + PanoramaUtil.FILES_W_SLASH + @"/"; 
                        textFolderUrl.Text = url;
                        PanoramaSource = true; // if you select a folder then manually change the folder, PanoramaSource will still be true
                        SelectedPath = dlg.SelectedPath;
                    }

                    Settings.Default.PanoramaTreeState = dlg.FolderBrowser.TreeState;
                }
               
            }
            catch (Exception e)
            {
                CommonAlertDlg.ShowException(this, e);
            }

            Settings.Default.Save();
        }

    }
}