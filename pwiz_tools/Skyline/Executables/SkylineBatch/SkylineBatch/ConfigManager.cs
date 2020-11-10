/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2020 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */


using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class ConfigManager
    {
        // Handles all modification to configs, the config list, configRunners, and log files
        // The UI should reflect the configs, runners, and log files from this class


        private List<SkylineBatchConfig> _configList; // the list of configurations. Every config must have a runner in configRunners
        private readonly Dictionary<string, ConfigRunner> _configRunners; // dictionary mapping from config name to that config's runner

        private readonly ISkylineBatchLogger _logger; // the current logger - always logs to SkylineBatch.log
        private readonly List<ISkylineBatchLogger> _oldLogs; // list of archived loggers, from most recent to least recent

        private readonly bool _runningUi; // if the UI is displayed (false when testing)
        private readonly IMainUiControl _uiControl; // null if no UI displayed

        

        public ConfigManager(ISkylineBatchLogger logger, IMainUiControl uiControl = null)
        {
            SelectedConfig = -1;
            SelectedLog = 0;
            _logger = logger;
            _uiControl = uiControl;
            _runningUi = uiControl != null;
            _configRunners = new Dictionary<string, ConfigRunner>();
            _oldLogs = new List<ISkylineBatchLogger>();
            LoadOldLogs();
            LoadConfigList();
        }

        public int SelectedConfig { get; private set; } // index of the selected configuration
        public int SelectedLog { get; private set; } // index of the selected log. index 0 corresponds to _logger, any index > 0 corresponds to oldLogs[index - 1]


        public enum Operation
        {
            Add,
            Insert,
            Remove,
            Replace
        }


        

        private void LoadConfigList()
        {
            _configList = new List<SkylineBatchConfig>();
            foreach (var config in Settings.Default.ConfigList)
            {
                _configList.Add(config);
                var runner = new ConfigRunner(config, _logger, _uiControl);
                _configRunners.Add(config.Name, runner);
            }
        }

        public void Close()
        {
            SaveConfigList();
            CancelRunners();
            _logger.Archive();
        }

        private void SaveConfigList()
        {
            var updatedConfigs = new ConfigList();
            foreach (var config in _configList)
            {
                try
                {
                    config.Validate();
                    updatedConfigs.Add(config);
                }
                catch (ArgumentException e)
                {
                    if (_runningUi)
                    {
                        _uiControl.DisplayError(Resources.Save_configuration_error,
                            string.Format(Resources.Could_not_save_configuration_error_message, config.Name, e.Message));
                    }
                    else
                    {
                        throw;
                    }
                    return;
                }

            }
            Settings.Default.ConfigList = updatedConfigs;
            Settings.Default.Save();
        }


        #region Configs

        public List<ListViewItem> ConfigsListViewItems()
        {
            var listViewConfigs = new List<ListViewItem>();
            foreach (var config in _configList)
            {
                var lvi = new ListViewItem(config.Name);
                lvi.UseItemStyleForSubItems = false; // So that we can change the color for sub-items.
                lvi.SubItems.Add(config.Created.ToShortDateString());
                lvi.SubItems.Add(_configRunners[config.Name].GetDisplayStatus());
                listViewConfigs.Add(lvi);
            }
            return listViewConfigs;
        }

        public bool HasConfigs()
        {
            return _configRunners.Keys.Count > 0;
        }

        public void SelectConfig(int newIndex)
        {
            SelectedConfig = newIndex;
        }

        public SkylineBatchConfig CreateConfiguration()
        {
            Program.LogInfo("Creating new configuration");
            var configCount = _configList.Count;
            return configCount > 0 ? _configList[configCount - 1].MakeChild() : SkylineBatchConfig.GetDefault();
        }

        public SkylineBatchConfig CopySelectedConfig()
        {
            var config = _configList[SelectedConfig];
            Program.LogInfo(string.Format("Copying configuration \"{0}\"", config.Name));
            var newConfig = config.Copy();
            newConfig.Name = null;
            return newConfig;
        }

        public void AddConfiguration(SkylineBatchConfig config)
        {
            InsertConfiguration(config, _configRunners.Count, Operation.Add);
        }

        public void InsertConfiguration(SkylineBatchConfig config, int index, Operation operation = Operation.Insert)
        {

            CheckIfExists(config, false, operation);
            config.Validate();
            Program.LogInfo(string.Format("Adding configuration \"{0}\"", config.Name));
            _configList.Insert(index, config);

            var newRunner = new ConfigRunner(config, _logger, _uiControl);
            _configRunners.Add(config.Name, newRunner);
        }

        public void ReplaceConfig(SkylineBatchConfig oldConfig, SkylineBatchConfig newConfig)
        {
            CheckIfExists(oldConfig, true, Operation.Replace);
            newConfig.Validate();
            if (!string.Equals(oldConfig.Name, newConfig.Name))
                CheckIfExists(newConfig, false, Operation.Replace);
            var index = IndexOf(oldConfig);
            RemoveConfig(oldConfig);
            InsertConfiguration(newConfig, index);
        }

        public void MoveSelectedConfig(bool moveUp)
        {
            var movingConfig = _configList[SelectedConfig];
            var delta = moveUp ? -1 : 1;
            _configList.Remove(movingConfig);
            _configList.Insert(SelectedConfig + delta, movingConfig);
            SelectedConfig += delta;
        }
        
        public void RemoveSelected()
        {
            var config = _configList[SelectedConfig];
            // Get the selected configuration
            var configRunner = GetSelectedConfigRunner();
            if (configRunner == null)
            {
                return;
            }

            if (configRunner.IsBusy())
            {
                string message = null;
                if (configRunner.IsRunning())
                {
                    message =
                        string.Format(
                            @"Configuration ""{0}"" is running. Please stop the configuration and try again. ",
                            configRunner.GetConfigName());
                }

                if (_runningUi)
                    MessageBox.Show(message, Resources.Cannot_Delete, MessageBoxButtons.OK);
                return;
            }

            var doDelete = DialogResult.Yes;
            if (_runningUi)
                doDelete = MessageBox.Show(
                    string.Format(@"Are you sure you want to delete configuration ""{0}""?",
                        configRunner.GetConfigName()),
                    Resources.Confirm_Delete,
                    MessageBoxButtons.YesNo);

            if (doDelete != DialogResult.Yes) return;

            // remove config
            Program.LogInfo(string.Format("Removing configuration \"{0}\"", configRunner.Config.Name));
            RemoveConfig(config);
            SelectConfig(-1);
        }

        private void RemoveConfig(SkylineBatchConfig config)
        {
            CheckIfExists(config, true, Operation.Remove);
            _configList.Remove(config);
            _configRunners[config.Name].Cancel();
            _configRunners.Remove(config.Name);
        }

        private void CheckIfExists(SkylineBatchConfig config, bool expectedValue, Operation typeOperation)
        {
            bool exists = _configRunners.Keys.Contains(config.Name);
            if (exists != expectedValue)
            {
                var message = expectedValue
                        ? string.Format(Resources.Operation_fail_config_nonexistant, typeOperation.ToString(), config.Name)
                        : string.Format(Resources.Operation_fail_config_exists, typeOperation.ToString(), config.Name);
                throw new ArgumentException(message);
            }

        }

        private int IndexOf(SkylineBatchConfig config)
        {
            for (int i = 0; i < _configList.Count; i++)
            {
                if (_configList[i].Equals(config))
                    return i;
            }
            return -1;
        }

        #endregion


        #region Run Configs

        public ConfigRunner GetConfigRunnerAtIndex(int index)
        {
            return _configRunners[_configList[index].Name];
        }

        public ConfigRunner GetSelectedConfigRunner()
        {
            if (SelectedConfig < 0)
            {
                throw new IndexOutOfRangeException("No configuration selected.");
            }
            return GetConfigRunnerAtIndex(SelectedConfig);
        }

        public async void RunAll(int startStep)
        {
            if (ConfigsRunning())
            {
                if(_runningUi)
                    _uiControl.DisplayError(Resources.Run_error_title, Resources.Cannot_run_busy_configurations);
                return;
            }
            UpdateIsRunning(true);

            var oldLogger = _logger.Archive();
            if (oldLogger != null)
                _oldLogs.Insert(0, oldLogger);
            if (_runningUi)
            {
                _uiControl.UpdateUiLogFiles();
            }

            foreach (var runner in _configRunners.Values)
            {
                runner.ChangeStatus(ConfigRunner.RunnerStatus.Waiting);
            }
            var nextConfig = GetNextWaitingConfig();
            while (!string.IsNullOrEmpty(nextConfig))
            {
                await _configRunners[nextConfig].Run(startStep);
                nextConfig = GetNextWaitingConfig();
            }

            while (ConfigsRunning())
                await Task.Delay(2000);
            UpdateIsRunning(false);
        }

        private string GetNextWaitingConfig()
        {
            foreach (var config in _configList)
            {
                if (_configRunners[config.Name].IsWaiting())
                {
                    return config.Name;
                }
            }

            return null;
        }

        public bool ConfigsRunning()
        {
            foreach (var runner in _configRunners.Values)
            {
                if (runner.IsBusy())
                    return true;
            }
            return false;
        }

        public void CancelRunners()
         {
            foreach (var configRunner in _configRunners.Values)
            {
                configRunner.Cancel();
            }
        }

        private void UpdateIsRunning(bool isRunning)
        {
            if (_runningUi)
                _uiControl.UpdateRunningButtons(isRunning);
        }

        #endregion


        #region Logging

        public bool HasOldLogs()
        {
            return _oldLogs.Count > 0;
        }

        public void SelectLog(int selected)
        {
            SelectedLog = selected;
        }

        public ISkylineBatchLogger GetSelectedLogger()
        {
            return SelectedLog == 0 ? _logger : _oldLogs[SelectedLog - 1];
        }

        private void LoadOldLogs()
        {
            var logDirectory = Path.GetDirectoryName(_logger.GetFile());
            var files = logDirectory != null ? new DirectoryInfo(logDirectory).GetFiles() : new FileInfo[0];
            foreach (var file in files)
            {
                if (file.Name.EndsWith(".log") && !file.Name.Equals("SkylineBatch.log"))
                {
                    _oldLogs.Insert(0, new SkylineBatchLogger(file.FullName, _uiControl));
                }
            }
        }

        public object[] GetOldLogFiles()
        {
            var oldLogFiles = new object[_oldLogs.Count];
            for (int i = 0; i < oldLogFiles.Length; i++)
            {
                oldLogFiles[i] = _oldLogs[i].GetFileName();
            }

            return oldLogFiles;
        }

        public object[] GetAllLogFiles()
        {
            var logFiles = new object [_oldLogs.Count + 1];
            logFiles[0] = _logger.GetFileName();
            GetOldLogFiles().CopyTo(logFiles, 1);
            return logFiles;
        }

        public void DeleteLogs(object[] deletingLogs)
        {
            int i = 0;
            while (i < _oldLogs.Count)
            {
                if (deletingLogs.Contains(_oldLogs[i].GetFileName()))
                {
                    File.Delete(_oldLogs[i].GetFile());
                    _oldLogs.RemoveAt(i);
                    if (i <= SelectedLog - 1)
                        SelectedLog = SelectedLog - 1 == i ? 0 : SelectedLog - 1;
                    continue;
                }
                i++;
                    
            }
            if (_runningUi)
                _uiControl.UpdateUiLogFiles();
        }

        #endregion
        

        #region Import/Export

        public string Import(string filePath)
        {
            var readConfigs = new List<SkylineBatchConfig>();
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    using (var reader = XmlReader.Create(stream))
                    {
                        while (reader.Name != "skylinebatch_config")
                        {
                            if (reader.Name == "userSettings" && !reader.IsStartElement()) // there are no configurations in the file
                                break;
                            reader.Read();
                        }
                        while (reader.IsStartElement())
                        {
                            if (reader.Name == "skylinebatch_config")
                            {
                                var config = SkylineBatchConfig.Deserialize(reader);
                                readConfigs.Add(config);
                            }
                            reader.Read();
                            reader.Read();
                        }
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show(string.Format(Resources.No_configs_imported, filePath),
                    Resources.Import_configs_error_title,
                    MessageBoxButtons.OK);
                return null;
            }

            if (readConfigs.Count == 0)
            {
                MessageBox.Show(string.Format(Resources.No_configs_imported, filePath),
                    Resources.Import_configs_error_title,
                    MessageBoxButtons.OK);
            }

            var validationErrors = new List<string>();
            var duplicateConfigs = new List<string>();
            var numAdded = 0;
            foreach (SkylineBatchConfig config in readConfigs)
            {
                // Make sure that the configuration name is unique
                if (!_configRunners.Keys.Contains(config.Name))
                {
                    // If a configuration with the same name already exists, don't add it
                    duplicateConfigs.Add(config.Name);
                    continue;
                }

                try
                {
                    config.Validate();
                }
                catch (Exception ex)
                {
                    validationErrors.Add(string.Format(Resources.Configuration_error_message, config.Name, ex.Message));
                    continue;
                }
                
                AddConfiguration(config);
                numAdded++;
            }
            var message = new StringBuilder(Resources.Number_configs_imported);
            message.Append(numAdded).Append(Environment.NewLine);
            if (duplicateConfigs.Count > 0)
            {
                message.Append(Resources.Number_configs_duplicates)
                    .Append(Environment.NewLine);
                foreach (var name in duplicateConfigs)
                {
                    message.Append("\"").Append(name).Append("\"").Append(Environment.NewLine);
                }
            }
            if (validationErrors.Count > 0)
            {
                message.Append(Resources.Number_configs_not_valid)
                    .Append(Environment.NewLine);
                foreach (var error in validationErrors)
                {
                    message.Append(error).Append(Environment.NewLine);
                }
            }
            return message.ToString();
        }

        public void ExportAll(string filePath)
        {
            SaveConfigList();

            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            config.SaveAs(filePath);
        }
        #endregion


    }
}
