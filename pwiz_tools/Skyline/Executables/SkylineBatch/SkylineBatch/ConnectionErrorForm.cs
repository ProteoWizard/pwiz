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
        private ServerConnector _serverConnector;

        private object _lastSelectedItem;


        public ConnectionErrorForm(List<SkylineBatchConfig> downloadingConfigs, ServerConnector serverConnector)
        {
            InitializeComponent();
            Icon = Program.Icon();
            _serverConnector = serverConnector;
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
            var addServerForm = new AddServerForm(config.MainSettings.Server, true);
            if (DialogResult.OK == addServerForm.ShowDialog(this))
            {
                var mainSettings = config.MainSettings;
                var newMainSettings = new MainSettings(mainSettings.TemplateFilePath, mainSettings.AnalysisFolderPath,mainSettings.DataFolderPath, 
                    addServerForm.Server, mainSettings.AnnotationsFilePath, mainSettings.ReplicateNamingPattern, mainSettings.DependentConfigName);
                var newConfig = new SkylineBatchConfig(config.Name, config.Enabled, config.Modified, newMainSettings, config.FileSettings, 
                    config.RefineSettings, config.ReportSettings, config.SkylineSettings);

                ReplacingConfigs.Add(newConfig);
                _configDict.Remove(config.Name);
                _disconnectedConfigs.Remove(config.Name);
                listConfigs.Items.Remove(config.Name);
                _serverConnector.Combine(addServerForm.serverConnector);
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
            var servers = new List<ServerInfo>();
            foreach (var configName in _disconnectedConfigs.Keys)
                servers.Add(_configDict[configName].MainSettings.Server);
            Reconnect(servers);
        }

        private void Reconnect(List<ServerInfo> servers)
        {
            var longWaitDlg = new LongWaitDlg(this, Program.AppName(), "Reconnecting...");
            var longWaitOperation = new LongWaitOperation(longWaitDlg);
            longWaitOperation.Start(true, (onProgress) =>
            {
                _serverConnector.Reconnect(servers, onProgress);
            }, (completed) => { DoneReconnecting(completed, servers); });
        }

        private void DoneReconnecting(bool completed, List<ServerInfo> servers)
        {
            if (IsDisposed || !completed) return;
            UpdateConfigList();
            foreach (var server in servers)
            {
                foreach (var config in _configDict.Values)
                {
                    if (((ServerInfo)config.MainSettings.Server).Equals(server) &&
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
                _serverConnector.GetFiles(config.MainSettings.Server, out Exception connectionException);
                if (connectionException != null)
                    _disconnectedConfigs.Add(config.Name, connectionException);
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
            var server = new List<ServerInfo>()
            {
                _configDict[(string)(listConfigs.SelectedItem)].MainSettings.Server
            };
            Reconnect(server);
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
