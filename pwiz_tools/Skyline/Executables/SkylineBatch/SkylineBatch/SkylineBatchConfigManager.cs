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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public class SkylineBatchConfigManager : ConfigManager
    {
        // Extension of ConfigManager, handles more specific SkylineBatch functionality
        // The UI should reflect the configs, runners, and log files from this class

        private readonly Dictionary<string, IConfigRunner> _configRunners; // dictionary mapping from config name to that config's runner
        private readonly Dictionary<string, string> _refinedTemplates; // dictionary mapping from config name to it's refined output file (not included if no refinement occurs)
        private readonly Dictionary<string, ServerInfo> _dataServers; // dictionary mapping from config name to it's refined output file (not included if no refinement occurs)

        // Shared variables with ConfigManager:
        //  Protected -
        //    Importer importer; <- a ReadXml method to import the configurations
        //    List<IConfig> _configList; <- the list of configurations. Every config must have a runner in _configRunners
        //    Dictionary<string, bool> _configValidation; <- dictionary mapping from config name to if that config is valid
        //    List<Logger>  _logList; <- list of all loggers displayed in the dropDown list on the log tab, _logList[0] is always "Skyline Batch.log"
        //
        //    _runningUi; <- if the UI is displayed (false when testing)
        //    IMainUiControl _uiControl; <- null if no UI displayed
        //
        //    object _lock = new object(); <- lock required for any mutator or getter method on _configList, _configRunners, or SelectedConfig
        //    object _loggerLock = new object(); <- lock required for any mutator or getter method on _logList or SelectedLog
        //  
        //  Public - 
        //    int SelectedConfig <- index of the selected configuration
        //    int SelectedLog <- index of the selected log. index 0 corresponds to _logger, any index > 0 corresponds to oldLogs[index - 1]
        //    Dictionary<string, string> RootReplacement; <- dictionary mapping from roots of invalid file paths to roots of valid file paths


        public SkylineBatchConfigManager(Logger logger, IMainUiControl uiControl = null)
        {
            importer = SkylineBatchConfig.ReadXml;
            SelectedLog = 0;
            _logList.Add(logger);
            _configRunners = new Dictionary<string, IConfigRunner>();
            _refinedTemplates = new Dictionary<string, string>();
            _dataServers = new Dictionary<string, ServerInfo>();

            _uiControl = uiControl;
            _runningUi = uiControl != null;

            Init();
            LoadOldLogs();
            LoadConfigList();
            AssertAllInitialized();
        }

        public new void Close()
        {
            base.Close();
            lock (_loggerLock)
            {
                _logList[0].Archive();
                _logList[0].Close();
            }
            foreach (var runner in _configRunners.Values)
                runner.Cancel();
        }

        private new void LoadConfigList()
        {
            lock (_lock)
            {
                var configs = base.LoadConfigList();
                configs = AssignDependencies(configs, true, out _); // ignore warning
                foreach (var config in configs)
                    ProgramaticallyAddConfig(config);
                DisableInvalidConfigs();
            }
        }

        public List<string> GetServerNames => _dataServers.Keys.ToList();

        #region Add/Remove Configs

        public List<IConfig> AssignDependencies(List<IConfig> newConfigs, bool checkExistingConfigs, out string warningMessage)
        {
            warningMessage = null;
            var configDictionary = new Dictionary<string, IConfig>();
            if (checkExistingConfigs)
            {
                foreach (var existingConfig in _configList)
                    configDictionary.Add(existingConfig.GetName(), existingConfig);
            }
            foreach (var newConfig in newConfigs)
                configDictionary.Add(newConfig.GetName(), newConfig);
            var errorConfigs = new List<string>();
            var configsWithDependency = new List<IConfig>();

            foreach (var iconfig in newConfigs)
            {
                var config = (SkylineBatchConfig) iconfig;
                var dependentName = config.MainSettings.DependentConfigName;
                if (dependentName != null)
                {
                    SkylineBatchConfig dependentConfig;
                    try
                    {
                        dependentConfig = (SkylineBatchConfig)configDictionary[dependentName];
                    }
                    catch (Exception)
                    {
                        errorConfigs.Add(config.Name);
                        configsWithDependency.Add(config.WithoutDependency());
                        continue;
                    }
                    configsWithDependency.Add(config.DependentChanged(dependentConfig.Name, dependentConfig.RefineSettings.OutputFilePath));
                }
                else
                {
                    configsWithDependency.Add(config);
                }
            }

            if (errorConfigs.Count != 0)
                warningMessage = Resources.SkylineBatchConfigManager_AssignDependencies_The_following_configurations_use_refined_template_files_from_other_configurations_that_do_not_exist_ + Environment.NewLine +
                               TextUtil.LineSeparate(errorConfigs) + Environment.NewLine +
                               Resources.SkylineBatchConfigManager_AssignDependencies_You_may_want_to_update_the_template_file_paths_;
            return configsWithDependency;
        }

        public void UserReplaceSelected(IConfig newConfig)
        {
            lock (_lock)
            {
                var index = SelectedConfig;
                var config = GetSelectedConfig();
                var replacingConfig = (SkylineBatchConfig)newConfig;
                var oldDependencies = GetDependencies();

                var nameChanged = !config.Name.Equals(replacingConfig.Name);
                var refineFileChanged =
                    !config.RefineSettings.OutputFilePath.Equals(replacingConfig.RefineSettings.OutputFilePath);
                if (oldDependencies.ContainsKey(config.Name) && (nameChanged || refineFileChanged))
                {
                    var runningConfigs = ConfigsBusy();
                    var runningDependentConfigs = oldDependencies[config.Name].Where(x => runningConfigs.Contains(x)).ToList();
                    if (runningDependentConfigs.Any())
                    {
                        DisplayWarning(
                            Resources.SkylineBatchConfigManager_UserReplaceSelected_Could_not_update_the_configuration_name_or_the_refined_output_file_path_while_the_following_dependent_configurations_are_running_ +
                            Environment.NewLine +
                            TextUtil.LineSeparate(runningDependentConfigs) + Environment.NewLine + Environment.NewLine +
                            Resources.SkylineBatchConfigManager_UserReplaceSelected_Please_wait_until_the_dependent_configurations_have_stopped_to_change_these_values_);
                        var newRefineSettings = RefineSettings.GetPathChanged(replacingConfig.RefineSettings, config.RefineSettings.OutputFilePath);
                        newConfig = new SkylineBatchConfig(config.Name, replacingConfig.Enabled, DateTime.Now,
                            replacingConfig.MainSettings,
                            replacingConfig.FileSettings, newRefineSettings, replacingConfig.ReportSettings,
                            replacingConfig.SkylineSettings);
                        nameChanged = refineFileChanged = false;
                    }
                }

                ProgramaticallyRemoveAt(index);
                try
                {
                    UserInsertConfig(newConfig, index);
                }
                catch (ArgumentException)
                {
                    UserInsertConfig(config, index);
                    throw;
                }

                if ((nameChanged || refineFileChanged) && oldDependencies.ContainsKey(config.Name))
                {
                    foreach (var dependent in oldDependencies[config.Name])
                        DependencyReplace(dependent, replacingConfig.Name, replacingConfig.RefineSettings.OutputFilePath);
                }
            }
        }

        public void UserAddConfig(IConfig iconfig)
        {
            UserInsertConfig(iconfig, _configList.Count);
        }

        public new void UserInsertConfig(IConfig iconfig, int index)
        {
            lock (_lock)
            {
                base.UserInsertConfig(iconfig, index);
                AddConfig(iconfig);
            }
        }

        private void ProgramaticallyAddConfig(IConfig iconfig)
        {
            ProgramaticallyInsertConfig(iconfig, _configList.Count);
        }

        private new void ProgramaticallyInsertConfig(IConfig iconfig, int index)
        {
            lock (_lock)
            {
                base.ProgramaticallyInsertConfig(iconfig, index);
                AddConfig(iconfig);
            }
        }

        private void AddConfig(IConfig iconfig)
        {
            var config = (SkylineBatchConfig)iconfig;
            if (_configRunners.ContainsKey(config.GetName()))
                throw new Exception("Config runner already exists.");

            var runner = new ConfigRunner(config, _logList[0], _uiControl);
            _configRunners.Add(config.GetName(), runner);

            if (config.RefineSettings.WillRefine())
                _refinedTemplates.Add(config.Name, config.RefineSettings.OutputFilePath);
            var server = config.MainSettings.Server;
            if (server != null && !_dataServers.ContainsKey(server.Url))
                AddValidServer(server);
        }

        private Dictionary<string, List<string>> GetDependencies()
        {
            var dependencies = new Dictionary<string, List<string>>();
            foreach (var iconfig in _configList)
            {
                var config = (SkylineBatchConfig) iconfig;
                var dependentConfigName = config.MainSettings.DependentConfigName;
                if (dependentConfigName != null)
                {
                    if (!dependencies.ContainsKey(dependentConfigName))
                        dependencies.Add(dependentConfigName, new List<string>());
                    dependencies[dependentConfigName].Add(config.Name);
                }
            }
            return dependencies;
        }

        public void UserRemoveSelected()
        {
            lock (_lock)
            {
                AssertConfigSelected();
                var index = SelectedConfig;
                var removingConfigName = _configList[index].GetName();
                if (_configRunners[removingConfigName].IsBusy())
                {
                    DisplayWarning(string.Format(
                        Resources.ConfigManager_RemoveSelected___0___is_still_running__Please_stop_the_current_run_before_deleting___0___,
                        removingConfigName));
                    return;
}
                var configDependencies = GetDependencies();
                if (configDependencies.ContainsKey(removingConfigName))
                {
                    var runningConfigs = ConfigsBusy();
                    var runningDependentConfigs = configDependencies[removingConfigName].Where(x => runningConfigs.Contains(x)).ToList();

                    if (runningDependentConfigs.Any())
                    {
                        DisplayError(string.Format(
                            Resources.SkylineBatchConfigManager_UserRemoveSelected_Cannot_delete___0___while_the_following_configurations_are_running_, removingConfigName) + Environment.NewLine +
                            TextUtil.LineSeparate(runningDependentConfigs) + Environment.NewLine + Environment.NewLine +
                            Resources.SkylineBatchConfigManager_UserRemoveSelected_Please_wait_until_these_configurations_have_finished_running_);
                        return;
                    }
                    var answer = DisplayQuestion(
                        string.Format(
                            Resources.SkylineBatchConfigManager_UserRemoveSelected_Deleting___0___may_impact_the_template_files_of_the_following_configurations_,
                            removingConfigName) +
                        Environment.NewLine +
                        TextUtil.LineSeparate(configDependencies[removingConfigName]) + Environment.NewLine +
                        string.Format(Resources.SkylineBatchConfigManager_UserRemoveSelected_Are_you_sure_you_want_to_delete___0___, removingConfigName));

                    if (answer != DialogResult.Yes) return;
                    foreach (var dependentName in configDependencies[removingConfigName])
                        DependencyRemove(dependentName);
                }

                RemoveConfig(_configList[index]);
                UserRemoveAt(index);
            }
        }

        private new void ProgramaticallyRemoveAt(int index)
        {
            lock (_lock)
            {
                RemoveConfig(_configList[index]);
                base.ProgramaticallyRemoveAt(index);
            }
        }

        private void DependencyReplace(string configName, string dependentName, string templateFile)
        {
            var index = GetConfigIndex(configName);
            var config = (SkylineBatchConfig) _configList[index];
            ProgramaticallyRemoveAt(index);
            var newConfig = config.DependentChanged(dependentName, templateFile);
            ProgramaticallyInsertConfig(newConfig, index);
            DisableInvalidConfigs();
        }

        private void DependencyRemove(string configName)
        {
            var index = GetConfigIndex(configName);
            var config = (SkylineBatchConfig)_configList[index];
            ProgramaticallyRemoveAt(index);
            var newConfig = config.WithoutDependency();
            ProgramaticallyInsertConfig(newConfig, index);
            DisableInvalidConfigs();
        }

        private void RemoveConfig(IConfig iconfig)
        {
            var config = (SkylineBatchConfig)iconfig;
            
            if (!_configRunners.ContainsKey(config.Name))
                throw new Exception("Config runner does not exist.");
            _configRunners.Remove(config.Name);
            if (_refinedTemplates.ContainsKey(config.Name))
                _refinedTemplates.Remove(config.Name);
        }
        
        #endregion

        #region Configs

        public HashSet<string> RVersionsUsed()
        {
            lock (_lock)
            {
                var RVersions = new HashSet<string>();
                foreach (var iconfig in _configList)
                {
                    var config = (SkylineBatchConfig) iconfig;
                    RVersions.UnionWith(config.ReportSettings.RVersions());
                }

                return RVersions;
            }
        }

        public new SkylineBatchConfig GetSelectedConfig()
        {
            return (SkylineBatchConfig) base.GetSelectedConfig();
        }

        public new SkylineBatchConfig GetConfig(int index)
        {
            return (SkylineBatchConfig)base.GetConfig(index);
        }

        public List<ListViewItem> ConfigsListViewItems(Graphics graphics)
        {
            lock (_lock)
            {
                return ConfigsListViewItems(_configRunners, graphics);
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

        public Dictionary<string, string> GetRefinedTemplates()
        {
            return new Dictionary<string, string>(_refinedTemplates);
        }

        public void ReplaceSkylineSettings(SkylineSettings skylineSettings)
        {
            lock (_lock)
            {
                var runningConfigs = ConfigsBusy();
                var replacingConfigs = GetReplacedSkylineSettings(skylineSettings, runningConfigs);
                foreach (var indexAndConfig in replacingConfigs)
                {
                    ProgramaticallyRemoveAt(indexAndConfig.Item1);
                    ProgramaticallyInsertConfig(indexAndConfig.Item2, indexAndConfig.Item1);
                }
                if (runningConfigs.Count > 0)
                    throw new ArgumentException(
                        Resources
                            .SkylineBatchConfigManager_ReplaceSkylineSettings_The_following_configurations_are_running_and_could_not_be_updated_
                        + Environment.NewLine +
                        TextUtil.LineSeparate(runningConfigs));
            }
        }

        public void RootReplaceConfigs(string oldRoot)
        {
            var replacedConfigs = GetRootReplacedConfigs(oldRoot);
            replacedConfigs = AssignDependencies(replacedConfigs, false, out _);
            foreach (var config in replacedConfigs)
            {
                var configIndex = GetConfigIndex(config.GetName());
                ProgramaticallyRemoveAt(configIndex);
                ProgramaticallyInsertConfig(config, configIndex);
            }
        }

        #endregion
        
        #region Run Configs

        public bool WillRefine()
        {
            lock (_lock)
            {
                foreach (var iconfig in _configList)
                {
                    var config = (SkylineBatchConfig) iconfig;
                    if (config.Enabled && config.RefineSettings.WillRefine())
                        return true;
                }
                return false;
            }
        }

        public ConfigRunner GetSelectedConfigRunner()
        {
            lock (_lock)
            {
                return (ConfigRunner) _configRunners[GetSelectedConfig().GetName()];
            }
        }

        public List<string> GetEnabledConfigs()
        {
            var enabledConfigs = new List<string>();
            foreach (var iconfig in _configList)
            {
                var config = (SkylineBatchConfig) iconfig;
                if (config.Enabled) enabledConfigs.Add(config.Name);
            }
            return enabledConfigs;
        }

        public SkylineBatchConfig ConfigFromName(string name)
        {
            return (SkylineBatchConfig) _configRunners[name].GetConfig();
        }

        public bool StartBatchRun(int startStep)
        {
            // Check that no configs are currently running
            var configsRunning = ConfigsBusy();
            if (configsRunning.Count > 0)
            {
                DisplayError(Resources.ConfigManager_RunAll_Cannot_run_while_the_following_configurations_are_running_ + Environment.NewLine +
                             string.Join(Environment.NewLine, configsRunning) + Environment.NewLine +
                             Resources.ConfigManager_RunAll_Please_wait_until_the_current_run_is_finished_);
                return false;
            }

            lock (_lock)
            {
                var enabledConfigs = GetEnabledConfigs();
                // Check there are enabled (checked) configs to run
                if (enabledConfigs.Count == 0)
                {
                    DisplayError(Resources.SkylineBatchConfigManager_StartBatchRun_There_are_no_enabled_configurations_to_run_ + Environment.NewLine +
                                 Resources.SkylineBatchConfigManager_StartBatchRun_Please_check_the_checkbox_next_to_one_or_more_configurations_);
                    return false;
                }

                // Check that configs run in correct order
                var dependencies = GetDependencies();
                foreach (var dependency in dependencies)
                {
                    foreach (var configToRun in enabledConfigs)
                    {
                        if (dependency.Value.Contains(configToRun))
                        {
                            var dependentIndex = enabledConfigs.IndexOf(dependency.Key);
                            if (dependentIndex < 0 || dependentIndex > enabledConfigs.IndexOf(configToRun))
                            {
                                if (startStep == 1 &&
                                    !File.Exists(ConfigFromName(configToRun).MainSettings.TemplateFilePath))
                                {
                                    DisplayError(string.Format(Resources.SkylineBatchConfigManager_StartBatchRun_Configuration__0__must_be_run_before__1__to_generate_its_template_file_, dependency.Key, configToRun) +
                                                 Environment.NewLine + string.Format(Resources.SkylineBatchConfigManager_StartBatchRun_Please_reorder_the_configurations_so__0__runs_first_, dependency.Key));
                                    return false;
                                }
                            }
                        }
                    }
                }

                // Check if files will be overwritten by run
                var overwriteInfo = "";
                if (startStep == 1) overwriteInfo = Resources.SkylineBatchConfigManager_StartBatchRun_results_files;
                if (startStep == 2) overwriteInfo = Resources.SkylineBatchConfigManager_StartBatchRun_chromatagram_files;
                if (startStep == 3) overwriteInfo = Resources.SkylineBatchConfigManager_StartBatchRun_refined_files;
                if (startStep == 4) overwriteInfo = Resources.SkylineBatchConfigManager_StartBatchRun_exported_reports;
                if (startStep == 5) overwriteInfo = Resources.SkylineBatchConfigManager_StartBatchRun_R_script_outputs;
                var overwriteMessage = new StringBuilder();
                overwriteMessage.Append(string.Format(
                    Resources.SkylineBatchConfigManager_StartBatchRun_Running_the_enabled_configurations_from_step__0__would_overwrite_the_following__1__,
                    startStep, overwriteInfo)).AppendLine().AppendLine();
                var showOverwriteMessage = false;

                foreach (var config in _configList)
                {
                    var skylineBatchConfig = (SkylineBatchConfig) config;
                    if (!skylineBatchConfig.Enabled) continue;
                    var tab = "      ";
                    var configurationHeader = tab + string.Format(Resources.SkylineBatchConfigManager_StartBatchRun_Configuration___0___, skylineBatchConfig.Name)  + Environment.NewLine;
                    var willOverwrite = skylineBatchConfig.RunWillOverwrite(startStep, configurationHeader, out StringBuilder message);
                    if (willOverwrite)
                    {
                        overwriteMessage.Append(message).AppendLine();
                        showOverwriteMessage = true;
                    }
                }
                // Ask if run should start if files will be overwritten
                overwriteMessage.Append(Resources.SkylineBatchConfigManager_StartBatchRun_Do_you_want_to_continue_);
                if (showOverwriteMessage)
                {
                    if (DisplayLargeOkCancel(overwriteMessage.ToString()) != DialogResult.OK)
                        return false;
                }

                // Starts config runners waiting
                foreach (var config in enabledConfigs)
                    ((ConfigRunner)_configRunners[config]).ChangeStatus(RunnerStatus.Waiting);
            }
            // Archives old log
            lock (_loggerLock)
            {
                var oldLogger = (_logList[0]).Archive();
                if (oldLogger != null)
                    _logList.Insert(1, oldLogger);
            }
            new Thread(() => _ = RunAsync(startStep)).Start();
            return true;
        }


        public async Task RunAsync(int startStep)
        {
            UpdateUiLogs();
            UpdateIsRunning(false, true);
            string nextConfig = GetNextWaitingConfig();
            while (!string.IsNullOrEmpty(nextConfig))
            {
                ConfigRunner startingConfigRunner;
                lock (_lock)
                {
                    startingConfigRunner = (ConfigRunner)_configRunners[nextConfig];
                }

                try
                {
                    await startingConfigRunner.Run(startStep);
                }
                catch (Exception e)
                {
                    startingConfigRunner.ChangeStatus(RunnerStatus.Error);
                    DisplayErrorWithException(string.Format(Resources.SkylineBatchConfigManager_RunAsync_An_unexpected_error_occurred_while_running__0_, nextConfig), e);
                }

                nextConfig = GetNextWaitingConfig();
            }

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

        public List<string> ConfigsBusy()
        {
            var configsRunning = new List<string>();
            lock (_lock)
            {
                foreach (var runner in _configRunners.Values)
                {
                    if (runner.IsBusy())
                        configsRunning.Add(runner.GetConfigName());
                }
            }
            return configsRunning;
        }

        public bool ConfigRunning()
        {
            lock (_lock)
            {
                foreach (var runner in _configRunners.Values)
                {
                    if (runner.IsRunning())
                        return true;
                }
            }
            return false;
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
                var logDirectory = Path.GetDirectoryName(_logList[0].GetFile());
                var files = logDirectory != null ? new DirectoryInfo(logDirectory).GetFiles() : new FileInfo[0];
                foreach (var file in files)
                {
                    if (file.Name.EndsWith(TextUtil.EXT_LOG) && !file.Name.Equals(_logList[0].GetFileName()))
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
                var oldSelectedLog = _logList[SelectedLog].Name;
                SelectedLog = 0;
                int i = 0;
                while (i < _logList.Count)
                {
                    if (deletingLogs.Contains(_logList[i].GetFileName()))
                    {
                        _logList[i].Delete(); // closes and deletes log file
                        _logList.RemoveAt(i); // removes from list
                        continue;
                    }
                    i++;
                }
                for (int j = 0; j < _logList.Count; j++)
                {
                    if (_logList[j].Name.Equals(oldSelectedLog))
                    {
                        SelectedLog = j;
                        break;
                    }
                }
                UpdateUiLogs();
            }
        }

        #endregion

        public void AddValidServer(ServerInfo server)
        {
            if (!_dataServers.ContainsKey(server.Name))
                _dataServers.Add(server.Name, server);
        }

        public ServerInfo GetServer(string name)
        {
            return _dataServers[name];
        }

        #region Import/Export

        public void Import(string filePath, ShowDownloadedFileForm showDownloadedFileForm)
        {
            var importedConfigs = ImportFrom(filePath, showDownloadedFileForm);
            foreach (var config in importedConfigs)
            {
                if (_configRunners.ContainsKey(config.GetName()))
                    ProgramaticallyRemoveAt(GetConfigIndex(config.GetName()));
            }
            importedConfigs = AssignDependencies(importedConfigs, true, out string warningMessage);
            foreach (var config in importedConfigs)
                ProgramaticallyAddConfig(config);
            DisableInvalidConfigs();
            _uiControl?.UpdateUiConfigurations();
            if (warningMessage != null)
                DisplayWarning(warningMessage);
        }

        private void DisableInvalidConfigs()
        {
            foreach (var config in _configList)
            {
                if (!_configValidation[config.GetName()])
                    ((SkylineBatchConfig)config).Enabled = false;
            }
        }

        #endregion
    }
}

