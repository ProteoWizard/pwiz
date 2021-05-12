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
using System.Collections.Immutable;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentFTP;
using SharedBatch;
using Resources = SkylineBatch.Properties.Resources;

namespace SkylineBatch
{
    public class SkylineBatchConfigManager : ConfigManager
    {
        // Extension of ConfigManager, handles more specific SkylineBatch functionality
        // The UI should reflect the configs, runners, and log files from this class

        private ImmutableDictionary<string, IConfigRunner> _configRunners; // dictionary mapping from config name to that config's runner
        private ImmutableDictionary<string, string> _refinedTemplates; // dictionary mapping from config name to it's refined output file (not included if no refinement occurs)

        private SkylineBatchConfigManagerState _importedConfigsState;

        private CancellationTokenSource _longOperationCancelToken;
        

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


        public SkylineBatchConfigManager(Logger logger, IMainUiControl mainForm = null)
        {
            importer = SkylineBatchConfig.ReadXml;
            SelectedLog = 0;
            _logList.Add(logger);
            _configRunners = ImmutableDictionary<string, IConfigRunner>.Empty;
            _refinedTemplates = ImmutableDictionary<string, string>.Empty;

            _uiControl = mainForm;
            _runningUi = mainForm != null;
            Init();
            LoadOldLogs();
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

        public void StartLoadingConfigList(LongWaitOperation longImportOperation, Callback importCallback)
        {
            var configs = LoadConfigList();
            var state = new SkylineBatchConfigManagerState(this);
            configs = AssignDependencies(configs, true, state, out _); // ignore warning

            DoLongImportOperation(configs, longImportOperation, importCallback, state);
        }


        #region Add/Remove Configs

        private List<IConfig> AssignDependencies(List<IConfig> newConfigs, bool checkExistingConfigs, SkylineBatchConfigManagerState state, out string warningMessage)
        {
            warningMessage = null;
            var configDictionary = new Dictionary<string, IConfig>();
            if (checkExistingConfigs)
            {
                foreach (var existingConfig in state.baseState.configList)
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
                if (!string.IsNullOrEmpty(dependentName))
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
            var state = new SkylineBatchConfigManagerState(this);
            var index = state.baseState.selected;
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

            state = ProgramaticallyRemoveAt(index, state);
            state = UserInsertConfig(newConfig, index, state); // can throw ArgumentException

            if ((nameChanged || refineFileChanged) && oldDependencies.ContainsKey(config.Name))
            {
                foreach (var dependent in oldDependencies[config.Name])
                    state = DependencyReplace(dependent, replacingConfig.Name, replacingConfig.RefineSettings.OutputFilePath, state);
            }

            SetState(state);
        }

        public void UserAddConfig(IConfig iconfig)
        {
            lock (_lock)
            {
                var newState = UserInsertConfig(iconfig, _configList.Count, new SkylineBatchConfigManagerState(this));
                SetState(newState);
            }
        }

        private SkylineBatchConfigManagerState UserInsertConfig(IConfig iconfig, int index, SkylineBatchConfigManagerState state)
        {
            state.baseState = base.UserInsertConfig(iconfig, index, state.baseState);
            state = AddConfig(iconfig, state);
            return state;
        }

        private SkylineBatchConfigManagerState ProgramaticallyAddConfig(IConfig iconfig, SkylineBatchConfigManagerState state)
        {
            return ProgramaticallyInsertConfig(iconfig, state.baseState.configList.Count, state);
        }

        private SkylineBatchConfigManagerState ProgramaticallyInsertConfig(IConfig iconfig, int index, SkylineBatchConfigManagerState state)
        {
            state.baseState = base.ProgramaticallyInsertConfig(iconfig, index, state.baseState);
            return AddConfig(iconfig, state);
        }

        private SkylineBatchConfigManagerState AddConfig(IConfig iconfig, SkylineBatchConfigManagerState state)
        {
            var config = (SkylineBatchConfig)iconfig;
            if (state.configRunners.ContainsKey(config.GetName()))
                throw new Exception("Config runner already exists.");
            var runner = new ConfigRunner(config, _logList[0], _uiControl);
            var configRunners = state.configRunners.Add(config.GetName(), runner);
            var refinedTemplates = state.templates;
            if (config.RefineSettings.WillRefine())
                refinedTemplates = refinedTemplates.Add(config.Name, config.RefineSettings.OutputFilePath);
            return new SkylineBatchConfigManagerState(state)
            {
                configRunners = configRunners,
                templates = refinedTemplates
            };
        }

        private Dictionary<string, List<string>> GetDependencies()
        {
            var dependencies = new Dictionary<string, List<string>>();
            foreach (var iconfig in _configList)
            {
                var config = (SkylineBatchConfig) iconfig;
                var dependentConfigName = config.MainSettings.DependentConfigName;
                if (!string.IsNullOrEmpty(dependentConfigName))
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
                var state = new SkylineBatchConfigManagerState(this);
                AssertConfigSelected(state.baseState);
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
                        state = DependencyRemove(dependentName, state);
                }

                state = RemoveConfig(_configList[index], state);
                state.baseState = UserRemoveAt(index, state.baseState);
                SetState(state);
            }
        }

        private SkylineBatchConfigManagerState ProgramaticallyRemoveAt(int index, SkylineBatchConfigManagerState state)
        {
            state = RemoveConfig(state.baseState.configList[index], state);
            state.baseState = base.ProgramaticallyRemoveAt(index, state.baseState);
            return state;
        }

        private SkylineBatchConfigManagerState DependencyReplace(string configName, string dependentName, string templateFile, SkylineBatchConfigManagerState state)
        {
            var index = GetConfigIndex(configName);
            var config = (SkylineBatchConfig) _configList[index];
            state = ProgramaticallyRemoveAt(index, state);
            var newConfig = config.DependentChanged(dependentName, templateFile);
            state = ProgramaticallyInsertConfig(newConfig, index, state);
            state = DisableInvalidConfigs(state);
            return state;
        }

        private SkylineBatchConfigManagerState DependencyRemove(string configName, SkylineBatchConfigManagerState state)
        {
            var index = GetConfigIndex(configName, state.baseState);
            var config = (SkylineBatchConfig)state.baseState.configList[index];
            state = ProgramaticallyRemoveAt(index, state);
            var newConfig = config.WithoutDependency();
            state = ProgramaticallyInsertConfig(newConfig, index, state);
            state = DisableInvalidConfigs(state);
            return state;
        }

        private SkylineBatchConfigManagerState RemoveConfig(IConfig iconfig, SkylineBatchConfigManagerState state)
        {
            var config = (SkylineBatchConfig)iconfig;
            
            if (!state.configRunners.ContainsKey(config.Name))
                throw new Exception("Config runner does not exist.");
            var configRunners = state.configRunners.Remove(config.Name);
            var templates = state.templates;
            if (state.templates.ContainsKey(config.Name))
                templates = state.templates.Remove(config.Name);
            return new SkylineBatchConfigManagerState(state)
            {
                configRunners = configRunners,
                templates = templates
            };
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
            var state = new SkylineBatchConfigManagerState(this);
            return ConfigsListViewItems(state.configRunners, graphics, state.baseState);

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
                var state = new SkylineBatchConfigManagerState(this);
                var runningConfigs = ConfigsBusy();
                var replacingConfigs = GetReplacedSkylineSettings(skylineSettings, runningConfigs);
                foreach (var indexAndConfig in replacingConfigs)
                {
                    state = ProgramaticallyRemoveAt(indexAndConfig.Item1, state);
                    state = ProgramaticallyInsertConfig(indexAndConfig.Item2, indexAndConfig.Item1, state);
                }
                if (runningConfigs.Count > 0)
                    throw new ArgumentException(
                        Resources
                            .SkylineBatchConfigManager_ReplaceSkylineSettings_The_following_configurations_are_running_and_could_not_be_updated_
                        + Environment.NewLine +
                        TextUtil.LineSeparate(runningConfigs));
                SetState(state);
            }
        }

        public void RootReplaceConfigs(string oldRoot)
        {
            var state = new SkylineBatchConfigManagerState(this);
            var replacedConfigs = GetRootReplacedConfigs(oldRoot, state.baseState);
            replacedConfigs = AssignDependencies(replacedConfigs, false, state, out _);
            foreach (var config in replacedConfigs)
            {
                var configIndex = GetConfigIndex(config.GetName(), state.baseState);
                state = ProgramaticallyRemoveAt(configIndex, state);
                state = ProgramaticallyInsertConfig(config, configIndex, state);
            }

            SetState(state);
        }

        #endregion
        
        #region Run Configs

        public bool WillRefine()
        {
            var state = new SkylineBatchConfigManagerState(this);
            foreach (var iconfig in state.baseState.configList)
            {
                var config = (SkylineBatchConfig) iconfig;
                if (config.Enabled && config.RefineSettings.WillRefine())
                    return true;
            }
            return false;
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

        public bool StartBatchRun(RunBatchOptions runOption, bool checkOverwrite = true)
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
                                if ((runOption == RunBatchOptions.FROM_COPY_TEMPLATE ||
                                    runOption == RunBatchOptions.RUN_ALL_STEPS) &&
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

                if (runOption == RunBatchOptions.RUN_ALL_STEPS ||
                    runOption == RunBatchOptions.DOWNLOAD_DATA)
                {
                    var filesToDownload = new Dictionary<string, FtpListItem>();
                    foreach (var config in _configList)
                    {
                        var skylineBatchConfig = (SkylineBatchConfig)config;
                        if (!skylineBatchConfig.Enabled) continue;
                        var newFiles = skylineBatchConfig.MainSettings.FilesToDownload();
                        foreach (var file in newFiles)
                            if (!filesToDownload.ContainsKey(file.Key)) filesToDownload.Add(file.Key, file.Value);
                    }
                    var driveSpaceNeeded = new Dictionary<string, long>();
                    foreach (var filePath in filesToDownload.Keys)
                    {
                        var driveName = filePath.Substring(0, 3);
                        if (!driveSpaceNeeded.ContainsKey(driveName))
                            driveSpaceNeeded.Add(driveName, 0);
                        driveSpaceNeeded[driveName] += filesToDownload[filePath].Size;
                    }
                    var spaceError = false;
                    var errorMessage =
                        Resources.SkylineBatchConfigManager_StartBatchRun_There_is_not_enough_space_on_this_computer_to_download_the_data_for_these_configurations__You_need_an_additional_ +
                        Environment.NewLine + Environment.NewLine;
                    foreach (var driveName in driveSpaceNeeded.Keys)
                    {
                        var spaceRemainingAfterDownload = (FileUtil.GetTotalFreeSpace(driveName) - driveSpaceNeeded[driveName] - FileUtil.ONE_GB) / FileUtil.ONE_GB;
                        if (spaceRemainingAfterDownload < 0)
                        {
                            spaceError = true;
                            errorMessage += string.Format(Resources.SkylineBatchConfigManager_StartBatchRun__0__GB_on_the__1__drive, Math.Abs(spaceRemainingAfterDownload),
                                driveName) + Environment.NewLine;
                        }
                    }

                    if (spaceError)
                    {
                        DisplayError(errorMessage + Environment.NewLine + Resources.SkylineBatchConfigManager_StartBatchRun_Please_free_up_some_space_to_download_the_data_);
                        return false;
                    }
                }

                // Check if files will be overwritten by run
                var overwriteInfo = "";
                if (runOption == RunBatchOptions.RUN_ALL_STEPS || runOption == RunBatchOptions.FROM_COPY_TEMPLATE) 
                    overwriteInfo = Resources.SkylineBatchConfigManager_StartBatchRun_results_files;
                if (runOption == RunBatchOptions.FROM_IMPORT_DATA) overwriteInfo = Resources.SkylineBatchConfigManager_StartBatchRun_chromatagram_files;
                if (runOption == RunBatchOptions.FROM_REFINE) overwriteInfo = Resources.SkylineBatchConfigManager_StartBatchRun_refined_files;
                if (runOption == RunBatchOptions.FROM_EXPORT_REPORT) overwriteInfo = Resources.SkylineBatchConfigManager_StartBatchRun_exported_reports;
                if (runOption == RunBatchOptions.FROM_R_SCRIPTS) overwriteInfo = Resources.SkylineBatchConfigManager_StartBatchRun_R_script_outputs_in_the_following_analysis_folders;
                var overwriteMessage = new StringBuilder();
                overwriteMessage.Append(string.Format(
                    Resources.SkylineBatchConfigManager_StartBatchRun_Running_the_enabled_configurations_from___0___would_overwrite_the__1__,
                    StepToReadableString(runOption), overwriteInfo)).AppendLine().AppendLine();
                var showOverwriteMessage = false;

                foreach (var config in _configList)
                {
                    var skylineBatchConfig = (SkylineBatchConfig) config;
                    if (!skylineBatchConfig.Enabled) continue;
                    var tab = "      ";
                    var configurationHeader = tab + string.Format(Resources.SkylineBatchConfigManager_StartBatchRun_Configuration___0___, skylineBatchConfig.Name)  + Environment.NewLine;
                    var willOverwrite = skylineBatchConfig.RunWillOverwrite(runOption, configurationHeader, out StringBuilder message);
                    if (willOverwrite)
                    {
                        overwriteMessage.Append(message).AppendLine();
                        showOverwriteMessage = true;
                    }
                }
                // Ask if run should start if files will be overwritten
                overwriteMessage.Append(Resources.SkylineBatchConfigManager_StartBatchRun_Do_you_want_to_continue_);
                if (showOverwriteMessage && checkOverwrite)
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
            new Thread(() => _ = RunAsync(runOption)).Start();
            return true;
        }


        public async Task RunAsync(RunBatchOptions runOption)
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
                    await startingConfigRunner.Run(runOption);
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

        private string StepToReadableString(RunBatchOptions runOption)
        {
            string text = string.Empty;
            switch (runOption)
            {
                case RunBatchOptions.RUN_ALL_STEPS:
                    text = Resources.MainForm_UpdateRunBatchSteps_Run_All_Steps;
                    break;
                case RunBatchOptions.DOWNLOAD_DATA:
                    text = Resources.MainForm_UpdateRunBatchSteps_Download_data_only;
                    break;
                case RunBatchOptions.FROM_COPY_TEMPLATE:
                    text = Resources.MainForm_UpdateRunBatchSteps_Run_from_step_1__save_analysis_template;
                    break;
                case RunBatchOptions.FROM_IMPORT_DATA:
                    text = Resources.MainForm_UpdateRunBatchSteps_Run_from_step_2__data_import;
                    break;
                case RunBatchOptions.FROM_REFINE:
                    text = Resources.MainForm_UpdateRunBatchSteps_Run_from_step_3__refine_file;
                    break;
                case RunBatchOptions.FROM_EXPORT_REPORT:
                    text = Resources.MainForm_UpdateRunBatchSteps_Run_from_step_3__export_reports;
                    break;
                case RunBatchOptions.FROM_R_SCRIPTS:
                    text = Resources.MainForm_UpdateRunBatchSteps_Run_from_step_4__run_R_scripts;
                    break;
                default:
                    throw new Exception("The run option was not recognized");
            }
            if (text.IndexOf(':') > 0) return text.Substring(text.IndexOf(':') + 2);
            return text;

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
                var logDirectory = Path.GetDirectoryName(_logList[0].LogFile);
                var files = logDirectory != null ? new DirectoryInfo(logDirectory).GetFiles() : new FileInfo[0];
                foreach (var file in files)
                {
                    if (file.Name.EndsWith(TextUtil.EXT_LOG) && !file.Name.Equals(_logList[0].LogFileName))
                    {
                        _logList.Insert(1, new Logger(file.FullName, file.Name));
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
                    oldLogFiles[i - 1] = _logList[i].LogFileName;
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
                    logFiles[i] = _logList[i].LogFileName;
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
                    if (deletingLogs.Contains(_logList[i].LogFileName))
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


        #region Import/Export

        private void DoLongImportOperation(List<IConfig> importingConfigs, LongWaitOperation longWaitOperation, Callback importCallback, SkylineBatchConfigManagerState state)
        {
            var numberConnectingToServer = 0.0;
            foreach (var config in importingConfigs)
            {
                if (((SkylineBatchConfig) config).MainSettings.WillDownloadData)
                    numberConnectingToServer++;
            }

            var showLongWaitDlg = numberConnectingToServer > 0;
            var numberConnectedToServer = 0;
            _longOperationCancelToken = longWaitOperation.CancelToken;

            longWaitOperation.Start(showLongWaitDlg, (OnProgress) =>
            {
                OnProgress(0, (int)((numberConnectedToServer + 1) / numberConnectingToServer * 100));
                foreach (var config in importingConfigs)
                {
                    OnProgress((int)(numberConnectedToServer / numberConnectingToServer * 100),
                        (int)((numberConnectedToServer + 1) / numberConnectingToServer * 100));
                    if (_longOperationCancelToken.IsCancellationRequested) break;
                    state = ProgramaticallyAddConfig(config, state);
                    if (((SkylineBatchConfig)config).MainSettings.WillDownloadData)
                        numberConnectedToServer++;
                }

                if (!_longOperationCancelToken.IsCancellationRequested)
                {
                    _importedConfigsState = state;
                    OnProgress(100, 100);
                }
            }, (success) =>
            {
                FinishImport(success);
                importCallback(success);
            });
        }
        

        public void StartImport(string filePath, LongWaitOperation longWaitOperation, Callback importCallback, ShowDownloadedFileForm showDownloadedFileForm)
        {
            var state = new SkylineBatchConfigManagerState(this);
            var importedConfigs = ImportFrom(filePath, showDownloadedFileForm);
            //var showLongWaitDlg = false;
            foreach (var config in importedConfigs)
            {
                if (state.configRunners.ContainsKey(config.GetName()))
                    state = ProgramaticallyRemoveAt(GetConfigIndex(config.GetName(), state.baseState), state);
            }
            importedConfigs = AssignDependencies(importedConfigs, true, state, out string warningMessage);
            if (warningMessage != null)
                DisplayWarning(warningMessage);

            DoLongImportOperation(importedConfigs, longWaitOperation, importCallback, state);
        }

        private void FinishImport(bool success)
        {
            var newState = _importedConfigsState;
            _importedConfigsState = null;
            _longOperationCancelToken = null;
            if (!success)
                return;
            newState = DisableInvalidConfigs(newState);
            SetState(newState);
        }

        private SkylineBatchConfigManagerState DisableInvalidConfigs(SkylineBatchConfigManagerState state)
        {
            var newState = new SkylineBatchConfigManagerState(state);
            foreach (var config in newState.baseState.configList)
            {
                if (!newState.baseState.configValidation[config.GetName()])
                    ((SkylineBatchConfig)config).Enabled = false;
            }
            return newState;
        }

        #endregion

        private void SetState(SkylineBatchConfigManagerState newState)
        {
            // TODO add checks fior valid
            lock (_lock)
            {
                newState.ValidateState();
                base.SetState(newState.baseState);
                _refinedTemplates = newState.templates;
                _configRunners = newState.configRunners;
                _uiControl?.UpdateUiConfigurations();
            }
        }


        class SkylineBatchConfigManagerState
        {
            public ConfigManagerState baseState;
            public ImmutableDictionary<string, string> templates;
            public ImmutableDictionary<string, IConfigRunner> configRunners;

            public SkylineBatchConfigManagerState(SkylineBatchConfigManager configManager)
            {
                baseState = new ConfigManagerState(configManager);
                templates = configManager._refinedTemplates;
                configRunners = configManager._configRunners;
            }

            public SkylineBatchConfigManagerState(SkylineBatchConfigManagerState state)
            {
                baseState = new ConfigManagerState(state.baseState);
                templates = state.templates;
                configRunners = state.configRunners;
            }

            public void ValidateState()
            {
                var validated = configRunners.Count == baseState.configList.Count;
                foreach (var config in baseState.configList)
                {
                    if (!validated) break;
                    if (!configRunners.ContainsKey(config.GetName()))
                        validated = false;
                }

                foreach (var configName in templates.Keys)
                {
                    if (!validated) break;
                    if (!configRunners.ContainsKey(configName) || !templates[configName]
                        .Equals(((SkylineBatchConfig) configRunners[configName].GetConfig()).RefineSettings
                            .OutputFilePath))
                        validated = false;
                }
                if (!validated)
                    throw new ArgumentException("Could not validate the new state of the configuration list. The operation did not succeed.");
            }
        }
    }

    public enum RunBatchOptions
    {
        RUN_ALL_STEPS,
        DOWNLOAD_DATA,
        FROM_COPY_TEMPLATE,
        FROM_IMPORT_DATA,
        FROM_REFINE,
        FROM_EXPORT_REPORT,
        FROM_R_SCRIPTS,
    }
}

