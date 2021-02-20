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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class SkylineBatchConfigManager : ConfigManager
    {
        // Handles all modification to configs, the config list, configRunners, and log files
        // The UI should reflect the configs, runners, and log files from this class

        //private readonly List<SkylineBatchConfig> _configList; // the list of configurations. Every config must have a runner in configRunners
        private readonly Dictionary<string, IConfigRunner> _configRunners; // dictionary mapping from config name to that config's runner
        //private readonly Dictionary<string, bool> _configValidation; // dictionary mapping from config name to if that config is valid

        //private readonly Logger _logger; // the current logger - always logs to SkylineBatch.log
        //private readonly List<Logger> _oldLogs; // list of archived loggers, from most recent to least recent

        //private readonly bool _runningUi; // if the UI is displayed (false when testing)
        //private readonly IMainUiControl _uiControl; // null if no UI displayed

        //private readonly object _lock = new object(); // lock required for any mutator or getter method on _configList, _configRunners, or SelectedConfig
        //private readonly object _loggerLock = new object(); // lock required for any mutator or getter method on _logger, _oldLogs or SelectedLog

        //private readonly ConfigListWrapper _configList;

        public SkylineBatchConfigManager(Logger logger, IMainUiControl uiControl = null)
        {
            importer = SkylineBatchConfig.ReadXml;
            SelectedLog = 0;
            _currentLogger = logger;
            _configRunners = new Dictionary<string, IConfigRunner>();
            //_uiControl = uiControl;
            //_runningUi = uiControl != null;
            //_configRunners = new Dictionary<string, ConfigRunner>();

            _uiControl = uiControl;
            _runningUi = uiControl != null;

            Init();
            LoadOldLogs();
            LoadConfigList();
            AssertAllInitialized();
        }

        //public int SelectedConfig { get; private set; } // index of the selected configuration
        //public int SelectedLog { get; private set; } // index of the selected log. index 0 corresponds to _logger, any index > 0 corresponds to oldLogs[index - 1]


        public new void Close()
        {
            base.Close();
            _currentLogger.Archive();
        }


        #region Configs

        private new void LoadConfigList()
        {
            base.LoadConfigList();
            AddConfigRunner(_configList);
        }


        private void AddConfigRunner(IConfig config)
        {
            if (_configRunners.ContainsKey(config.GetName()))
                throw new Exception("Config runner already exists.");
            var runner = new ConfigRunner((SkylineBatchConfig)config, _currentLogger, _uiControl);
            _configRunners.Add(config.GetName(), runner);
        }

        private void AddConfigRunner(List<IConfig> configs)
        {
            foreach (SkylineBatchConfig config in configs)
                AddConfigRunner(config);
        }

        private void RemoveConfigRunner(SkylineBatchConfig config)
        {
            if (!_configRunners.ContainsKey(config.Name))
                throw new Exception("Config runner does not exist.");
            _configRunners.Remove(config.Name);
        }

        public void AddConfiguration(SkylineBatchConfig config)
        {
            InsertConfiguration(config, _configList.Count);
        }

        private void InsertConfiguration(SkylineBatchConfig config, int index)
        {
            base.InsertConfiguration(config, index);
            AddConfigRunner(config);
        }

        public new void RemoveSelected()
        {
            lock (_lock)
            {
                var configRunner = GetSelectedConfigRunner();
                if (configRunner.IsBusy())
                {
                    DisplayWarning(string.Format(
                        Resources.ConfigManager_RemoveSelected___0___is_still_running__Please_stop_the_current_run_before_deleting___0___,
                        configRunner.GetConfigName()));
                    return;
                }
                if (base.RemoveSelected()) RemoveConfigRunner(configRunner.Config);
            }
        }

        public new void ReplaceSelectedConfig(IConfig newConfig)
        {
            lock (_lock)
            {
                var oldConfig = GetSelectedConfig();
                base.ReplaceSelectedConfig(newConfig);
                RemoveConfigRunner(oldConfig);
                AddConfigRunner(newConfig);
            }
        }

        public new SkylineBatchConfig GetSelectedConfig()
        {
            return (SkylineBatchConfig) base.GetSelectedConfig();
        }

        public List<ListViewItem> ConfigsListViewItems()
        {
            lock (_lock)
            {
                return ConfigsListViewItems(_configRunners);
            }
        }

        public bool CheckConfigAtIndex(int index, out string errorMessage)
        {
            lock (_lock)
            {
                errorMessage = "";
                var config = (SkylineBatchConfig)_configList[index];
                var runner = _configRunners[config.Name];
                if (!IsConfigValid(index))
                {
                    errorMessage =
                        string.Format(
                            Resources.MainForm_listViewConfigs_ItemCheck_Cannot_enable___0___while_it_is_invalid_,
                            config.Name) +
                        Environment.NewLine +
                        string.Format(Resources.ConfigManager_RunAll_Please_edit___0___to_enable_running_, config.Name);
                    return false;
                }
                if (runner.IsBusy())
                {
                    errorMessage =
                        string.Format( Resources.ConfigManager_CheckConfigAtIndex_Cannot_disable___0___while_it_has_status___1_,
                            config.Name, runner.GetStatus()) +
                        Environment.NewLine +
                        string.Format(Resources.ConfigManager_CheckConfigAtIndex_Please_wait_until___0___has_finished_running_, config.Name);
                    return false;
                }
                config.Enabled = !config.Enabled;
                return true;
            }
        }




        #endregion
        
        #region Run Configs

        public ConfigRunner GetSelectedConfigRunner()
        {
            return (ConfigRunner)_configRunners[GetSelectedConfig().GetName()];
        }
        public async Task RunAllEnabled(int startStep)
        {

            var configsRunning = ConfigsRunning();
            if (configsRunning.Count > 0)
            {
                DisplayError(Resources.ConfigManager_RunAll_Cannot_run_while_the_following_configurations_are_running_ + Environment.NewLine +
                             string.Join(Environment.NewLine, configsRunning) + Environment.NewLine +
                             Resources.ConfigManager_RunAll_Please_wait_until_the_current_run_is_finished_);
                return;
            }

            string nextConfig;
            lock (_lock)
            {
                // Check if files will be overwritten by run
                var overwriteInfo = "";
                if (startStep == 1) overwriteInfo = Resources.ConfigManager_RunAllEnabled_results_files;
                if (startStep == 2) overwriteInfo = Resources.ConfigManager_RunAllEnabled_chromatagram_files;
                if (startStep == 3) overwriteInfo = Resources.ConfigManager_RunAllEnabled_exported_reports;
                if (startStep == 4) overwriteInfo = Resources.ConfigManager_RunAllEnabled_R_script_outputs;
                var overwriteMessage = new StringBuilder();
                overwriteMessage.Append(string.Format(
                    Resources.ConfigManager_RunAllEnabled_Running_the_enabled_configurations_from_step__0__would_overwrite_the_following__1__,
                    startStep, overwriteInfo)).AppendLine().AppendLine();
                var showOverwriteMessage = false;

                foreach (SkylineBatchConfig config in _configList)
                {
                    if (!config.Enabled) continue;
                    var tab = "      ";
                    var configurationHeader = tab + string.Format(Resources.ConfigManager_RunAllEnabled_Configuration___0___, config.Name)  + Environment.NewLine;
                    var willOverwrite = config.MainSettings.RunWillOverwrite(startStep, configurationHeader, out StringBuilder message);
                    if (willOverwrite)
                    {
                        overwriteMessage.Append(message).AppendLine();
                        showOverwriteMessage = true;
                    }
                }
                // Ask if run should start if files will be overwritten
                overwriteMessage.Append(Resources.ConfigManager_RunAllEnabled_Do_you_want_to_continue_);
                if (showOverwriteMessage)
                {
                    if (DisplayLargeQuestion(overwriteMessage.ToString()) != DialogResult.OK)
                        return;
                }
                // Checks if there are enabled configs and starts them waiting
                var hasEnabledConfigs = false;
                foreach (ConfigRunner runner in _configRunners.Values)
                {
                    if (runner.Config.Enabled)
                    {
                        runner.ChangeStatus(RunnerStatus.Waiting);
                        hasEnabledConfigs = true;
                    }
                }
                if (!hasEnabledConfigs)
                {
                    DisplayError(Resources.ConfigManager_RunAllEnabled_There_are_no_enabled_configurations_to_run_ + Environment.NewLine +
                                 Resources.ConfigManager_RunAllEnabled_Please_check_the_checkbox_next_to_one_or_more_configurations_);
                    return;
                }

                nextConfig = GetNextWaitingConfig();
            }

            UpdateIsRunning(false, true);

            lock (_loggerLock)
            {
                var oldLogger = (_currentLogger).Archive();
                if (oldLogger != null)
                    _logList.Insert(1, oldLogger);
                UpdateUiLogs();
            }

            while (!string.IsNullOrEmpty(nextConfig))
            {
                await ((ConfigRunner)_configRunners[nextConfig]).Run(startStep);
                nextConfig = GetNextWaitingConfig();
            }

            while (ConfigsRunning().Count > 0)
                await Task.Delay(3000);
            UpdateIsRunning(true, false);
        }

        private string GetNextWaitingConfig()
        {
            lock (_lock)
            {
                foreach (var config in _configList)
                {
                    if (_configRunners[config.GetName()].GetStatus() == RunnerStatus.Waiting)
                    {
                        return config.GetName();
                    }
                }
                return null;
            }
        }

        public List<string> ConfigsRunning()
        {
            lock (_lock)
            {
                var configsRunning = new List<string>();
                foreach (var runner in _configRunners.Values)
                {
                    if (runner.IsBusy())
                        configsRunning.Add(runner.GetConfigName());
                }
                return configsRunning;
            }
        }

        public void CancelRunners()
        {
            lock (_lock)
            {
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
                return _logList.Count > 1;
        }

        public void SelectLog(int selected)
        {
            lock (_loggerLock)
            {
                if (selected < 0 || selected >= _logList.Count)
                    throw new IndexOutOfRangeException(Resources.ConfigManager_SelectLog_No_log_at_index__ + selected);
                SelectedLog = selected;
            }
        }

        

        private void LoadOldLogs()
        {
            lock (_loggerLock)
            {
                _logList.Add(_currentLogger);
                var logDirectory = Path.GetDirectoryName(_currentLogger.GetFile());
                var files = logDirectory != null ? new DirectoryInfo(logDirectory).GetFiles() : new FileInfo[0];
                foreach (var file in files)
                {
                    if (file.Name.EndsWith(TextUtil.EXT_LOG) && !file.Name.Equals(_currentLogger.GetFileName()))
                    {
                        _logList.Insert(1, new Logger(file.FullName, file.Name, _uiControl));
                    }
                }
            }
        }

        public object[] GetOldLogFiles()
        {
            lock (_loggerLock)
            {
                var oldLogFiles = new object[_logList.Count - 1];
                for (int i = 1; i < _logList.Count; i++)
                {
                    oldLogFiles[i - 1] = _logList[i].GetFileName();
                }

                return oldLogFiles;
            }
        }

        public object[] GetAllLogFiles()
        {
            lock (_loggerLock)
            {
                var logFiles = new object[_logList.Count];
                for (int i = 0; i < _logList.Count; i++)
                    logFiles[i] = _logList[i].GetFileName();
                return logFiles;
            }
        }

        public void DeleteLogs(object[] deletingLogs)
        {
            lock (_loggerLock)
            {
                int i = 0;
                while (i < _logList.Count)
                {
                    if (deletingLogs.Contains(_logList[i].GetFileName()))
                    {
                        _logList[i].Delete(); // closes and deletes log file
                        _logList.RemoveAt(i); // removes from list
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

        /*private void DisplayError(string message)
        {
            if (!_runningUi)
                return;
            _uiControl.DisplayError(message);
        }

        private void DisplayWarning(string message)
        {
            if (!_runningUi)
                return;
            _uiControl.DisplayWarning(message);
        }

        private DialogResult DisplayQuestion(string message)
        {
            if (!_runningUi)
                return DialogResult.Yes;
            return _uiControl.DisplayQuestion(message);
        }

        private DialogResult DisplayLargeQuestion(string message)
        {
            if (!_runningUi)
                return DialogResult.Yes;
            return _uiControl.DisplayLargeQuestion(message);
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
        }*/
        
        #endregion

        #region Import/Export

        public void Import(string filePath)
        {
            var importedConfigs = ImportFrom(filePath, SkylineBatchImporter);
            AddConfigRunner(importedConfigs);
        }

        public IConfig SkylineBatchImporter(XmlReader reader)
        {
            var config = SkylineBatchConfig.ReadXml(reader);
            config.Enabled = false;
            return config;
        }

        #endregion
    }
}

