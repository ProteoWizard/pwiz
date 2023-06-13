using SharedBatch;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Web.Caching;
using System.Windows.Forms;
using pwiz.PanoramaClient;
using SkylineBatch.Properties;
using AlertDlg = SharedBatch.AlertDlg;
using PanoramaServer = SharedBatch.PanoramaServer;
using PanoramaClientServer = pwiz.PanoramaClient.PanoramaServer;
using PanoramaUtil = pwiz.PanoramaClient.PanoramaUtil;
using UserState = pwiz.PanoramaClient.UserState;

namespace SkylineBatch
{
    public partial class RemoteFileControl : UserControl
    {
        private readonly IMainUiControl _mainControl;
        private int _lastStelectedIndex;
        private readonly string _downloadFolder;
        private readonly bool _fileRequired;
        private readonly bool _preferPanoramaSource;


        public RemoteFileControl(IMainUiControl mainControl, SkylineBatchConfigManagerState state, Server editingServer, string downloadFolder, bool fileRequired, bool preferPanoramaSource)
        {
            InitializeComponent();

            State = state;
            _mainControl = mainControl;
            _lastStelectedIndex = -1;
            _downloadFolder = downloadFolder;
            _fileRequired = fileRequired;
            _preferPanoramaSource = preferPanoramaSource;
            UpdateRemoteSourceList();
            if (editingServer != null)
            {
                textRelativePath.Text = editingServer.RelativePath;
                comboRemoteFileSource.SelectedItem = editingServer.FileSource.Name;
            }
            CheckIfPanoramaSource();

        }

        public SkylineBatchConfigManagerState State { get; private set; }
        public bool PanoramaSource { get; private set; } = false;

        private RemoteFileSource getRemoteFileSource(){

            RemoteFileSource remoteFileSource;
            try
            {
                remoteFileSource = RemoteFileSourceFromUI();
            }
            catch (Exception ex)
            {
                return null;
            }

            return remoteFileSource;

        }

        private void CheckIfPanoramaSource()
        {
            RemoteFileSource source = getRemoteFileSource();
            Uri uri = source.URI;
            UserState state = PanoramaUtil.ValidateServerAndUser(ref uri,source.Username,source.Password);
            if (state.IsValid())
            {
                
                btnOpenFromPanorama.Visible = true;
                textRelativePath.Width = comboRemoteFileSource.Width - btnOpenFromPanorama.Width;
            }
            else
            {
                btnOpenFromPanorama.Visible = false;
                textRelativePath.Width = comboRemoteFileSource.Width;
            }

        }


        private void UpdateRemoteSourceList()
        {
            comboRemoteFileSource.Items.Clear();
            foreach (var name in State.FileSources.Keys)
                comboRemoteFileSource.Items.Add(name);
            comboRemoteFileSource.Items.Add(Resources.RemoteFileControl_UpdateRemoteSourceList__Add____);
            comboRemoteFileSource.Items.Add(Resources.RemoteFileControl_UpdateRemoteSourceList__Edit_current____);
            comboRemoteFileSource.Items.Add(Resources.RemoteFileControl_UpdateRemoteSourceList__Edit_list____);
        }

        public void Clear()
        {
            RunUi(() =>
            {
                comboRemoteFileSource.SelectedIndex = -1;
                textRelativePath.Text = string.Empty;
            });
        }

        public Server ServerFromUI()
        {
            var remoteFileSource = RemoteFileSourceFromUI();
            return remoteFileSource != null ? new Server(remoteFileSource, textRelativePath.Text) : null;
        }

        public void CheckPanoramaServer(CancellationToken cancelToken, Action<PanoramaFile, Exception> callback)
        {
            RemoteFileSource remoteFileSource = getRemoteFileSource();
            new Thread(() =>
            {
                try
                {
                    var panoramaServer = remoteFileSource != null ? PanoramaFile.PanoramaFileFromUI(remoteFileSource, textRelativePath.Text, _downloadFolder,
                        cancelToken) : null;
                    callback(panoramaServer, null);
                }
                catch (Exception ex)
                {
                    callback(null, ex);
                }

            }).Start();
        }

        public RemoteFileSource RemoteFileSourceFromUI()
        {
            if (comboRemoteFileSource.SelectedIndex == -1 && string.IsNullOrEmpty(textRelativePath.Text) && !_fileRequired)
                return null;
            if (comboRemoteFileSource.SelectedIndex == -1 || !State.FileSources.ContainsKey((string)comboRemoteFileSource.SelectedItem))
                throw new ArgumentException(Resources.DataServerForm_GetServerFromUi_A_remote_file_source_is_required__Please_select_a_remote_file_source_);
            return State.FileSources[(string) comboRemoteFileSource.SelectedItem];
        }

        private void comboRemoteFileSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedItem = comboRemoteFileSource.SelectedItem;
            var selectedIndex = comboRemoteFileSource.SelectedIndex;
            var itemCount = comboRemoteFileSource.Items.Count;

            if (selectedIndex >= itemCount - 3)
            {
                // Edit list...
                if (selectedItem.Equals(Resources.RemoteFileControl_UpdateRemoteSourceList__Edit_list____))
                {
                    var editSourceForm = new EditRemoteFileSourcesForm(_mainControl, State, _lastStelectedIndex, _preferPanoramaSource);
                    var dialogResult = editSourceForm.ShowDialog(this);
                    State = editSourceForm.State;
                    UpdateRemoteSourceList();
                    if (DialogResult.OK == dialogResult && editSourceForm.LastEditedName != null)
                        comboRemoteFileSource.SelectedItem = editSourceForm.LastEditedName;
                    else
                        comboRemoteFileSource.SelectedIndex = _lastStelectedIndex;
                }
                // Add.../Edit current...
                else
                {
                    RemoteFileSource editingFileSource = null;
                    if (selectedItem.Equals(Resources.RemoteFileControl_UpdateRemoteSourceList__Edit_current____))
                    {
                        if (_lastStelectedIndex == -1)
                        {
                            _mainControl.DisplayError(
                                "No remote file source is selected. Please select a remote file source to edit current.");
                            comboRemoteFileSource.SelectedIndex = _lastStelectedIndex;
                            return;
                        }
                        editingFileSource =
                            State.FileSources[(string) comboRemoteFileSource.Items[_lastStelectedIndex]];
                    }
                    var remoteSourceForm = new RemoteSourceForm(editingFileSource, _mainControl, State, _preferPanoramaSource); 
                    var dialogResult = remoteSourceForm.ShowDialog(this);
                    State = remoteSourceForm.State;
                    UpdateRemoteSourceList();
                    if (DialogResult.OK == dialogResult)
                        comboRemoteFileSource.SelectedItem = remoteSourceForm.RemoteFileSource.Name;
                    else
                        comboRemoteFileSource.SelectedIndex = _lastStelectedIndex;
                }
            }
            CheckIfPanoramaSource();

            _lastStelectedIndex = comboRemoteFileSource.SelectedIndex;
        }

        

        public void AddRemoteFileChangedEventHandler(EventHandler eventHandler)
        {
            comboRemoteFileSource.SelectedIndexChanged += eventHandler;
        }

        public void AddRelativePathChangedEventHandler(EventHandler eventHandler)
        {
            textRelativePath.TextChanged += eventHandler;
        }

        private void RunUi(Action action)
        {
            try
            {
                Invoke(action);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }


        public void OpenFromPanorama()
        {
            RemoteFileSource remoteFileSource = getRemoteFileSource();
            Uri uri = remoteFileSource.URI; // Need to get server URI
            // string userName= remoteFileSource.Username;
            // string textPassword = remoteFileSource.Password;

            string host = $"https://{uri.Host}";

            PanoramaClientServer server = new PanoramaClientServer(new Uri(host));

            var panoramaServers = new List<PanoramaClientServer>() { server };

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
                            textRelativePath.Text = dlg.FileUrl.Replace(uri.AbsoluteUri, "");
                            PanoramaSource = true; // if you select a folder then manually change the folder, PanoramaSource will still be true
                        }
                        Settings.Default.PanoramaTreeState = dlg.TreeState;
                        Settings.Default.ShowPanormaSkyFiles = dlg.ShowingSky;
                    }
                }
                else // if file not required use PanoramaDirectoryPicker
                {
                    using (PanoramaDirectoryPicker dlg = new PanoramaDirectoryPicker(panoramaServers, state, showWebDavFolders: true))
                    {

                        // dlg.InitializeDialog();
                        if (dlg.ShowDialog() != DialogResult.Cancel)
                        {
                            string url = dlg.Selected;
                            string uriString = uri.ToString();
                            string output = $"{url.Replace(uri.ToString(), "")}/";
                            textRelativePath.Text = output;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                AlertDlg.ShowError(this, Program.AppName(), e.Message);
            }

            Settings.Default.Save();
        }

        private void btnOpenFromPanorama_Click(object sender, EventArgs e)
        {
            OpenFromPanorama();
        }
    }
}
