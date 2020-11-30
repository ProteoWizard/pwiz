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


        private readonly List<SkylineBatchConfig> _configList; // the list of configurations. Every config must have a runner in configRunners
        private readonly Dictionary<string, ConfigRunner> _configRunners; // dictionary mapping from config name to that config's runner

        private readonly ISkylineBatchLogger _logger; // the current logger - always logs to SkylineBatch.log
        private readonly List<ISkylineBatchLogger> _oldLogs; // list of archived loggers, from most recent to least recent

        private readonly bool _runningUi; // if the UI is displayed (false when testing)
        private readonly IMainUiControl _uiControl; // null if no UI displayed

        private readonly object _lock = new object(); // lock required for any mutator or getter method on _configList, _configRunners, or SelectedConfig
        private readonly object _loggerLock = new object(); // lock required for any mutator or getter method on _logger, _oldLogs or SelectedLog

        public ConfigManager(ISkylineBatchLogger logger, IMainUiControl uiControl = null)
        {
            SelectedConfig = -1;
            SelectedLog = 0;
            _logger = logger;
            _uiControl = uiControl;
            _runningUi = uiControl != null;
            _configRunners = new Dictionary<string, ConfigRunner>();
            _configList = new List<SkylineBatchConfig>();
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
            var invalidConfigNames = "";
            foreach (var config in _configList)
            {
                try
                {
                    config.Validate();
                    updatedConfigs.Add(config);
                }
                catch (ArgumentException)
                {
                    invalidConfigNames += config.Name + Environment.NewLine;
                }
            }
            if (invalidConfigNames.Length > 0)
            {
                DisplayError(Resources.Save_configuration_error,
                    Resources.Could_not_save_configurations + Environment.NewLine + invalidConfigNames);
            }
            Settings.Default.ConfigList = updatedConfigs;
            Settings.Default.Save();
        }


        #region Configs

        public List<ListViewItem> ConfigsListViewItems()
        {
            lock (_lock)
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
        }

        public bool HasConfigs()
        {
            lock (_lock)
            {
                return _configList.Count > 0;
            }
        }

        public bool HasSelectedConfig()
        {
            return SelectedConfig >= 0;
        }

        public void SelectConfig(int newIndex)
        {
            lock (_lock)
            {
                if (newIndex < 0 || newIndex >= _configList.Count)
                    throw new IndexOutOfRangeException("No configuration at index: " + newIndex);
                SelectedConfig = newIndex;
            }
        }

        public void DeselectConfig()
        {
            lock (_lock)
            {
                SelectedConfig = -1;
            }
        }

        private void CheckConfigSelected()
        {
            if (SelectedConfig < 0)
            {
                throw new IndexOutOfRangeException("No configuration selected.");
            }
        }

        public SkylineBatchConfig GetLastCreated() // creates config using most recently created config
        {
            lock (_lock)
            {
                if (!HasConfigs())
                    throw new ArgumentException("No configurations to create from.");
                Program.LogInfo("Creating new configuration");
                var lastCreated = _configList[0];
                foreach (var config in _configList)
                {
                    if (config.Created > lastCreated.Created)
                        lastCreated = config;
                }

                return lastCreated;
            }
        }

        public SkylineBatchConfig GetSelectedConfig()
        {
            lock (_lock)
            {
                CheckConfigSelected();
                return _configList[SelectedConfig];
            }
        }

        public void AddConfiguration(SkylineBatchConfig config)
        {
            InsertConfiguration(config, _configList.Count, Operation.Add);
        }

        private void InsertConfiguration(SkylineBatchConfig config, int index, Operation operation = Operation.Insert)
        {
            lock (_lock)
            {
                CheckIfExists(config, false, operation);
                Program.LogInfo(string.Format("Adding configuration \"{0}\"", config.Name));
                _configList.Insert(index, config);

                var newRunner = new ConfigRunner(config, _logger, _uiControl);
                _configRunners.Add(config.Name, newRunner);
            }
            
        }

        public void ReplaceSelectedConfig(SkylineBatchConfig newConfig)
        {
            lock (_lock)
            {
                CheckConfigSelected();
                var oldConfig = _configList[SelectedConfig];
                if (!string.Equals(oldConfig.Name, newConfig.Name))
                    CheckIfExists(newConfig, false, Operation.Replace);
                RemoveConfig(oldConfig);
                InsertConfiguration(newConfig, SelectedConfig);
            }
        }

        public void MoveSelectedConfig(bool moveUp)
        {
            lock (_lock)
            {
                var movingConfig = _configList[SelectedConfig];
                var delta = moveUp ? -1 : 1;
                _configList.Remove(movingConfig);
                _configList.Insert(SelectedConfig + delta, movingConfig);
                SelectedConfig += delta;
            }
        }
        
        public void RemoveSelected()
        {
            lock (_lock)
            {
                CheckConfigSelected();
                var configRunner = GetSelectedConfigRunner();

                var config = configRunner.Config;

                if (configRunner.IsBusy())
                {
                    DisplayWarning(Resources.Cannot_Delete, string.Format(
                        @"Configuration ""{0}"" is running. Please stop the configuration and try again. ",
                        configRunner.GetConfigName()));
                    return;
                }

                var doDelete = DisplayQuestion(Resources.Confirm_Delete, 
                    string.Format(@"Are you sure you want to delete configuration ""{0}""?",
                        configRunner.GetConfigName()));

                if (doDelete != DialogResult.Yes)
                    return;

                // remove config
                Program.LogInfo(string.Format("Removing configuration \"{0}\"", config.Name));
                RemoveConfig(config);
                DeselectConfig();
            }
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

        #endregion


        #region Run Configs

        public ConfigRunner GetSelectedConfigRunner()
        {
            lock (_lock)
            {
                CheckConfigSelected();
                return _configRunners[_configList[SelectedConfig].Name];
            }
        }

        public async void RunAll(int startStep)
        {
            if (ConfigsRunning())
            {
                DisplayError(Resources.Run_error_title, Resources.Cannot_run_busy_configurations);
                return;
            }
            UpdateIsRunning(true);

            lock (_loggerLock)
            {
                var oldLogger = _logger.Archive();
                if (oldLogger != null)
                    _oldLogs.Insert(0, oldLogger);
                UpdateUiLogs();
            }

            string nextConfig;
            lock (_lock)
            {
                foreach (var runner in _configRunners.Values)
                {
                    runner.ChangeStatus(ConfigRunner.RunnerStatus.Waiting);
                }

                nextConfig = GetNextWaitingConfig();
            }

            while (!string.IsNullOrEmpty(nextConfig))
            {
                await _configRunners[nextConfig].Run(startStep);
                nextConfig = GetNextWaitingConfig();
            }

            while (ConfigsRunning())
                await Task.Delay(3000);
            UpdateIsRunning(false);
        }

        private string GetNextWaitingConfig()
        {
            lock (_lock)
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
        }

        private bool ConfigsRunning()
        {
            lock (_lock)
            {
                foreach (var runner in _configRunners.Values)
                {
                    if (runner.IsBusy())
                        return true;
                }

                return false;
            }
        }

        public void CancelRunners()
        {
             lock (_lock){
                foreach (var configRunner in _configRunners.Values)
                {
                    configRunner.Cancel();
                }
             }
        }

        #endregion


        #region Logging

        public bool HasOldLogs()
        {
            lock (_loggerLock)
                return _oldLogs.Count > 0;
        }

        public void SelectLog(int selected)
        {
            lock (_loggerLock)
            {
                if (selected < 0 || selected > _oldLogs.Count)
                    throw new IndexOutOfRangeException("No log at index: " + selected);
                SelectedLog = selected;
            }
        }

        public ISkylineBatchLogger GetSelectedLogger()
        {
            lock (_loggerLock)
                return SelectedLog == 0 ? _logger : _oldLogs[SelectedLog - 1];
        }

        private void LoadOldLogs()
        {
            lock (_loggerLock)
            {
                var logDirectory = Path.GetDirectoryName(_logger.GetFile());
                var files = logDirectory != null ? new DirectoryInfo(logDirectory).GetFiles() : new FileInfo[0];
                foreach (var file in files)
                {
                    if (file.Name.EndsWith(".log") && !file.Name.Equals(_logger.GetFileName()))
                    {
                        _oldLogs.Insert(0, new SkylineBatchLogger(file.FullName, _uiControl));
                    }
                }
            }
        }

        public object[] GetOldLogFiles()
        {
            lock (_loggerLock)
            {
                var oldLogFiles = new object[_oldLogs.Count];
                for (int i = 0; i < oldLogFiles.Length; i++)
                {
                    oldLogFiles[i] = _oldLogs[i].GetFileName();
                }

                return oldLogFiles;
            }
        }

        public object[] GetAllLogFiles()
        {
            lock (_loggerLock)
            {
                var logFiles = new object[_oldLogs.Count + 1];
                logFiles[0] = _logger.GetFileName();
                GetOldLogFiles().CopyTo(logFiles, 1);
                return logFiles;
            }
        }

        public void DeleteLogs(object[] deletingLogs)
        {
            lock (_loggerLock)
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
                UpdateUiLogs();
            }
        }

        #endregion


        #region UI Control

        private void DisplayError(string title, string message)
        {
            if (!_runningUi)
                return;
            _uiControl.DisplayError(title, message);
        }

        private void DisplayWarning(string title, string message)
        {
            if (!_runningUi)
                return;
            _uiControl.DisplayWarning(title, message);
        }

        private void DisplayInfo(string title, string message)
        {
            if (!_runningUi)
                return;
            _uiControl.DisplayInfo(title, message);
        }

        private DialogResult DisplayQuestion(string title, string message)
        {
            if (!_runningUi)
                return DialogResult.Yes;
            return _uiControl.DisplayQuestion(title, message);
        }

        private void UpdateUiLogs()
        {
            if (!_runningUi)
                return;
            _uiControl.UpdateUiLogFiles();
        }

        private void UpdateIsRunning(bool isRunning)
        {
            if (!_runningUi)
                return;
            _uiControl.UpdateRunningButtons(isRunning);
        }



        #endregion


        #region Import/Export

        public void Import(string filePath)
        {
            var readConfigs = new List<SkylineBatchConfig>();
            var validationErrors = new List<string>();
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
                                SkylineBatchConfig config = null;
                                try
                                {
                                    config = SkylineBatchConfig.ReadXml(reader);
                                }
                                catch (Exception ex)
                                {
                                    validationErrors.Add(ex.Message);
                                }
                                
                                if (config != null)
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
                DisplayError(Resources.Import_configs_error_title, string.Format(Resources.No_configs_imported, filePath));
                return;
            }

            if (readConfigs.Count == 0 && validationErrors.Count == 0)
            {
                DisplayWarning(Resources.Import_configs_error_title,
                    string.Format(Resources.No_configs_imported, filePath));
                return;
            }

            var duplicateConfigs = new List<string>();
            var numAdded = 0;
            foreach (SkylineBatchConfig config in readConfigs)
            {
                // Make sure that the configuration name is unique
                if (_configRunners.Keys.Contains(config.Name))
                {
                    // If a configuration with the same name already exists, don't add it
                    duplicateConfigs.Add(config.Name);
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
            DisplayInfo(Resources.Import_configurations, message.ToString());
        }



        public object[] GetConfigNames()
        {
            var names = new object[_configList.Count];
            for (int i = 0; i < _configList.Count; i++)
                names[i] = _configList[i].Name;
            return names;
        }

        public void ExportConfigs(string filePath, int[] indiciesToSave)
        {
            var savingConfigs = new ConfigList();
            foreach(int index in indiciesToSave)
                savingConfigs.Add(_configList[index]);
            var tempSettings = new Settings();
            tempSettings.ConfigList = savingConfigs;
            tempSettings.Save();
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            config.SaveAs(filePath);
            Settings.Default.Save();
        }

        #endregion


        #region Tests
        
        public bool ConfigListEquals(List<SkylineBatchConfig> otherConfigs)
        {
            lock (_lock)
            {
                if (otherConfigs.Count != _configList.Count) return false;

                for (int i = 0; i < _configList.Count; i++)
                {
                    if (!Equals(otherConfigs[i], _configList[i])) return false;
                }

                return true;
            }
        }

        public string ListConfigNames()
        {
            lock (_lock)
            {
                var names = "";
                foreach (var config in _configList)
                {
                    names += config.Name + "  ";
                }

                return names;
            }
        }
        


        #endregion


    }
}

