using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SharedBatch;

namespace SkylineBatch
{
    public partial class ConnectionErrorForm : Form
    {
        //private List<string> _disconnectedNames;
        private Dictionary<string, Exception> _disconnectedConfigs;
        private Dictionary<string, SkylineBatchConfig> _configDict;
        private ServerFilesManager _serverFiles;

        private object _lastSelectedItem;


        public ConnectionErrorForm(List<SkylineBatchConfig> downloadingConfigs, ServerFilesManager serverFiles)
        {
            InitializeComponent();
            Icon = Program.Icon();
            _serverFiles = serverFiles;
            _disconnectedConfigs = new Dictionary<string, Exception>();
            _configDict = new Dictionary<string, SkylineBatchConfig>();
            foreach (var config in downloadingConfigs) _configDict.Add(config.Name, config);
            ReplacingConfigs = new List<SkylineBatchConfig>();

            Shown += ((sender, args) =>
            {
                UpdateConfigList();
            });
        }

        public List<SkylineBatchConfig> ReplacingConfigs;

        private void listConfigs_SelectedIndexChanged(object sender, EventArgs e)
        {
            _lastSelectedItem = listConfigs.SelectedItem;
            btnReconnect.Enabled = listConfigs.SelectedIndex >= 0;
            btnEdit.Enabled = listConfigs.SelectedIndex >= 0;
            textError.Text = listConfigs.SelectedIndex >= 0
                ? _disconnectedConfigs[(string) listConfigs.SelectedItem].Message
                : string.Empty;
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            var config = _configDict[(string) listConfigs.SelectedItem];
            var addServerForm = new AddServerForm(config.MainSettings.Server, config.MainSettings.Server.Folder, true);
            if (DialogResult.OK == addServerForm.ShowDialog(this))
            {
                var mainSettings = config.MainSettings;
                var newMainSettings = new MainSettings(mainSettings.Template, mainSettings.AnalysisFolderPath,mainSettings.DataFolderPath, 
                    addServerForm.Server, mainSettings.AnnotationsFilePath, mainSettings.ReplicateNamingPattern);
                var newConfig = new SkylineBatchConfig(config.Name, config.Enabled, config.Modified, newMainSettings, config.FileSettings, 
                    config.RefineSettings, config.ReportSettings, config.SkylineSettings);

                ReplacingConfigs.Add(newConfig);
                _configDict.Remove(config.Name);
                _disconnectedConfigs.Remove(config.Name);
                listConfigs.Items.Remove(config.Name);
                _serverFiles.Replace(config.MainSettings.Server, addServerForm.Server, addServerForm.serverConnector, new PanoramaServerConnector());
                //_serverConnector.Combine(addServerForm.serverConnector);
                CheckIfAllConnected();
            }
        }

        private void CheckIfAllConnected()
        {
            if (_disconnectedConfigs.Count == 0)
            {
                DialogResult = DialogResult.OK;
                RunUi(() => Close());
            }
        }

        private void btnReconnectAll_Click(object sender, EventArgs e)
        {
            var servers = new List<DataServerInfo>();
            foreach (var configName in _disconnectedConfigs.Keys)
                servers.Add(_configDict[configName].MainSettings.Server);
            Reconnect(servers);
        }

        private void Reconnect(List<DataServerInfo> ftpServers)
        {
            var longWaitDlg = new LongWaitDlg(this, Program.AppName(), "Reconnecting...");
            var longWaitOperation = new LongWaitOperation(longWaitDlg);
            var servers = new List<Server>();
            foreach (var server in ftpServers) servers.Add(server);
            longWaitOperation.Start(true, (onProgress) =>
            {
                _serverFiles.Reconnect(servers, onProgress);
            }, (completed) => { DoneReconnecting(completed, servers); });
        }

        private void DoneReconnecting(bool completed, List<Server> servers)
        {
            if (IsDisposed || !completed) return;
            UpdateConfigList();
            foreach (var server in servers)
            {
                foreach (var config in _configDict.Values)
                {
                    if ((config.MainSettings.Server).Equals(server) &&
                        _disconnectedConfigs[config.Name] != null)
                    {
                        RunUi(() => { AlertDlg.ShowError(this, Program.AppName(), _disconnectedConfigs[config.Name].Message); });
                        return;
                    }
                }
            }
        }

        private void UpdateConfigList()
        {
            var selectedName = _lastSelectedItem;
            _disconnectedConfigs.Clear();
            foreach (var config in _configDict.Values)
            {
                var ftpConnectionException = _serverFiles.ConnectionException(config.MainSettings.Server);
                var panoramaConnectionException = config.MainSettings.Template.PanoramaFile != null ? _serverFiles.ConnectionException(config.MainSettings.Template.PanoramaFile) : null;
                if (ftpConnectionException != null || panoramaConnectionException != null) {}
                    _disconnectedConfigs.Add(config.Name, panoramaConnectionException != null ? panoramaConnectionException : ftpConnectionException);
            }
            RunUi(() =>
            {
                textError.Text = String.Empty;
                listConfigs.Items.Clear();
                foreach (var configName in _disconnectedConfigs.Keys)
                    listConfigs.Items.Add(configName);
                if (selectedName != null && listConfigs.Items.Contains(selectedName))
                    listConfigs.SelectedItem = selectedName;
            });
            CheckIfAllConnected();
        }

        private void btnReconnect_Click(object sender, EventArgs e)
        {
            var config = _configDict[(string) (listConfigs.SelectedItem)];
            var ftpConnectionException = _serverFiles.ConnectionException(config.MainSettings.Server);
            var panoramaConnectionException = config.MainSettings.Template.PanoramaFile != null ? _serverFiles.ConnectionException(config.MainSettings.Template.PanoramaFile) : null;
            var servers = new List<DataServerInfo>();
            //if (panoramaConnectionException != null) servers.Add(config.MainSettings.Template.PanoramaFile.Server);
            if (ftpConnectionException != null) servers.Add(config.MainSettings.Server);
            Reconnect(servers);
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
    }
}
