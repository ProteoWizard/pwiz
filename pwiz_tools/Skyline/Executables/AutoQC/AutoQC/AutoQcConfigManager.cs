using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AutoQC.Properties;
using SharedBatch;

namespace AutoQC
{
    public class AutoQcConfigManager : ConfigManager
    {
        // Extension of ConfigManager, handles more specific AutoQC functionality
        // The UI should reflect the configs, runners, and log files from this class

        private AutoQcConfigManagerState _state;

        // Shared variables with ConfigManager:
        //  Protected -
        //    Importer importer; <- a ReadXml method to import the configurations
        //    List<IConfig> _configList; <- the list of configurations. Every config must have a runner in _configRunners
        //    Dictionary<string, bool> _configValidation; <- dictionary mapping from config name to if that config is valid
        //    List<Logger>  _logList; <- list of all loggers displayed in the dropDown list on the log tab, logger Names correspond to config names
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

        public AutoQcConfigManager(IMainUiControl uiControl = null)
        {
            SelectedLog = -1;
            _uiControl = uiControl;
            _state = AutoQcConfigManagerState.Empty();
            LoadConfigList();

        }

        public AutoQcConfigManagerState AutoQcState => GetAutoQcState();


        public void ChangeKeepRunningState(bool enable)
        {
            if (enable)
            {
                StartupManager.EnableKeepRunning();
            }
            else
            {
                StartupManager.DisableKeepRunning();
            }
        }


        #region Manipulate State

        public AutoQcConfigManagerState GetAutoQcState()
        {
            lock (_lock)
            {
                return _state.Copy();
            }
        }

        public bool SetState(AutoQcConfigManagerState expectedState, AutoQcConfigManagerState newState, bool updateLogFiles = true)
        {
            string errorMessage = null;
            lock (_lock)
            {
                try
                {
                    if (!Equals(expectedState, AutoQcState))
                    {
                        throw new ArgumentException(SharedBatch.Properties.Resources
                            .ConfigManager_SetState_The_state_of_the_configuration_list_has_changed_since_this_operation_started__Please_try_again_);
                    }

                    newState.ValidateState();
                    base.SetState(expectedState.BaseState, newState.BaseState);
                }
                catch (Exception e)
                {
                    errorMessage = e.Message;
                }
                if (errorMessage == null)
                {
                    _state = newState.Copy();
                    _logList = _state.LogList;
                    SelectedLog = -1;
                    if (_state.BaseState.HasSelectedConfig())
                    {
                        var selectedConfig = _state.BaseState.GetSelectedConfig();
                        SelectedLog = _state.GetLoggerIndex(selectedConfig.GetName());
                    }
                    _uiControl?.UpdateUiConfigurations();
                    if (updateLogFiles)
                    {
                        _uiControl?.UpdateUiLogFiles();
                    }
                }
            }

            if (errorMessage != null)
                DisplayError(_uiControl, errorMessage);
            return errorMessage != null;
        }

        #endregion

        #region State Method Wrappers

        private void LoadConfigList()
        {
            if (_uiControl == null)
                return; // don't load configs in test mode
            var initialState = AutoQcState;
            var state = initialState.Copy();
            state.LoadConfigList(_uiControl);
            SetState(initialState, state);
        }

        public void SelectConfig(int index)
        {
            var initialState = AutoQcState;
            var state = initialState.Copy();
            state.BaseState.SelectIndex(index);
            SetState(initialState, state);
        }

        public void DeselectConfig()
        {
            var initialState = AutoQcState;
            var state = initialState.Copy();
            state.BaseState.SelectIndex(-1);
            SetState(initialState, state);
        }

        public void SortByValue(int columnIndex)
        {
            var initialState = AutoQcState;
            var state = initialState.Copy().SortByValue(columnIndex);
            SetState(initialState, state);
        }

        public void SetConfigInvalid(IConfig config)
        {
            var initialState = AutoQcState;
            var state = initialState.Copy().SetConfigInvalid(config);
            SetState(initialState, state, false);
        }

        public List<ListViewItem> ConfigsListViewItems(Graphics graphics)
        {
            var state = AutoQcState;
            return state.BaseState.ConfigsAsListViewItems(state.ConfigRunners, graphics);

        }

        #endregion

        #region Import

        public void Import(string filePath, bool setConfigsDisabled = false)
        {
            var initialState = AutoQcState;
            var state = initialState.Copy();
            state.Import(filePath, _uiControl, out List<int> addedIndicies);
            var addedConfigs = new List<IConfig>();
            foreach (var index in addedIndicies)
            {
                var config = (AutoQcConfig)state.BaseState.ConfigList[index];
                if (setConfigsDisabled && config.IsEnabled)
                {
                    state.DisableConfig(index, _uiControl);
                }
                addedConfigs.Add(state.BaseState.ConfigList[index]);
            }

            if (SetState(initialState, state))
                // Do server validation
                DoServerValidation(state.BaseState, addedConfigs.ToImmutableList());
        }

        #endregion

        #region Run Configs


        public void RunEnabled()
        {
            IList<string> failedToStart = new List<string>();
            lock (_lock)
            {
                foreach (var config in AutoQcState.BaseState.ConfigList)
                {
                    var autoQcConfig = (AutoQcConfig) config;
                    if (!autoQcConfig.IsEnabled)
                        continue;
                    if (!TryStartConfig(autoQcConfig))
                    {
                        failedToStart.Add(autoQcConfig.Name);
                    }
                }
            }

            if (failedToStart.Count > 0)
            {
                var msg = "Failed to start the following configurations:";
                var configs = TextUtil.LineSeparate(failedToStart);
                _uiControl.DisplayError(TextUtil.LineSeparate(msg, configs));
            }
        }

        public void DoServerValidation()
        {
            DoServerValidation(AutoQcState.BaseState, AutoQcState.BaseState.ConfigList);
        }

        public void DoServerValidation(ConfigManagerState baseState, ImmutableList<IConfig> configs)
        {
            lock (_lock)
            {
                var toValidate = new List<AutoQcConfig>();
                foreach (var config in configs)
                {
                    var autoQcConfig = (AutoQcConfig)config;
                    if (autoQcConfig.IsEnabled  // Config is running
                        || !baseState.IsConfigValid(baseState.GetConfigIndex(autoQcConfig.Name)) // Already marked as invalid
                        || !autoQcConfig.PanoramaSettings.PublishToPanorama) // Not set to publish to Panorama
                    {
                        continue;
                    }

                    toValidate.Add(autoQcConfig);
                }
                var worker = new BackgroundWorker { WorkerSupportsCancellation = false, WorkerReportsProgress = false };
                worker.DoWork += ValidateServerSettings;
                worker.RunWorkerAsync(argument: toValidate);
            }
        }

        private void ValidateServerSettings(object sender, DoWorkEventArgs e)
        {
            var configs = (List<AutoQcConfig>)e.Argument;
            foreach (var config in configs)
            {
                if (config.PanoramaSettings.PublishToPanorama)
                {
                    var configRunner = AutoQcState.GetConfigRunner(config);
                    if (configRunner != null)
                    {
                        // Change the status while we are validating.
                        configRunner.ChangeStatus(RunnerStatus.Loading, false);
                    }
                }
            }
            _uiControl.UpdateUiConfigurations(); // Update the UI.

            foreach (var config in configs)
            {
                if (config.PanoramaSettings.PublishToPanorama)
                {
                    var configRunner = AutoQcState.GetConfigRunner(config);
                    if (configRunner != null)
                    {
                        try
                        {
                            // Thread.Sleep(5000);
                            // Only validate the Panorama server settings. Everything else should already have been validated
                            config.PanoramaSettings.ValidateSettings(true);
                            configRunner.ChangeStatus(RunnerStatus.Stopped);
                        }
                        catch (Exception)
                        {
                            configRunner.ChangeStatus(RunnerStatus.Stopped);
                            SetState(AutoQcState, AutoQcState.SetConfigInvalid(config), false);
                        }
                    }
                }
            }
        }

        public bool StopConfiguration()
        {
            var initialState = AutoQcState;
            var selectedConfig = initialState.GetSelectedConfig();
            if (!selectedConfig.IsEnabled) // TODO: Do we need this?
                return false;

            var configRunner = initialState.GetSelectedConfigRunner();
            if (configRunner.IsPending())
            {
                var action = configRunner.GetStatus().ToString();
                var message =
                    string.Format(Resources.AutoQcConfigManager_StopConfiguration_Cannot_stop_a_configuration_that_is__0___Please_wait_for_the_action_to_complete_, action);

                DisplayWarning(_uiControl, message);
                return false;
            }

            var doChange = ConfigManager.DisplayQuestion(_uiControl, string.Format(
                Resources.AutoQcConfigManager_StopConfiguration_Are_you_sure_you_want_to_stop_the_configuration___0___,
                configRunner.GetConfigName()));

            if (doChange == DialogResult.Yes)
            {
                configRunner.Stop();
                if (configRunner.IsStopped())
                {
                    var state = initialState.Copy().DisableSelectedConfig(_uiControl);
                    SetState(initialState, state);
                }
                return true;
            }

            return false;
        }

        public bool UpdateSelectedEnabled(bool newIsEnabled)
        {
            return newIsEnabled ? StartConfiguration() : StopConfiguration();
        }

        public bool StartConfiguration()
        {
            lock (_lock)
            {
                var selectedConfig = AutoQcState.GetSelectedConfig();
                if (selectedConfig.IsEnabled) // TODO: Do we need this?
                    return false;

                var configRunner = AutoQcState.GetSelectedConfigRunner();
                if (configRunner.IsPending())
                {
                    var action = configRunner.GetStatus().ToString();
                    var message =
                        string.Format(Resources.AutoQcConfigManager_StartConfiguration_Cannot_start_a_configuration_that_is__0___Please_wait_for_the_action_to_complete_, action);

                    DisplayWarning(_uiControl, message);
                    return false;
                }

                try
                {
                    StartConfig(selectedConfig);
                }
                catch (Exception e)
                {
                    DisplayErrorWithException(_uiControl,
                        TextUtil.LineSeparate(string.Format(Resources.AutoQcConfigManager_StartConfiguration_There_was_an_error_running_the_configuration___0___, selectedConfig.Name), e.Message), e);
                    return false;
                }
                return true;
            }
        }

        // Starts a configuration.  If there are any errors or exceptions they are logged to the program log
        private bool TryStartConfig(AutoQcConfig config)
        {
            try
            {
                StartConfig(config);
                return true;
            }
            catch (Exception e)
            {
                ProgramLog.Error(string.Format(Resources.AutoQcConfigManager_StartConfiguration_There_was_an_error_running_the_configuration___0___, config.Name), e);
            }

            return false;
        }

        private void StartConfig(AutoQcConfig config)
        {
            //State.ConfigRunners.TryGetValue(config.Name, out var configRunner);
            
            var initialState = AutoQcState;
            var state = initialState.Copy();
            var configIndex = state.BaseState.GetConfigIndex(config.Name);

            state.ConfigRunners.TryGetValue(config.Name, out var configRunner);
            if (configRunner == null)
            {
                state.DisableConfig(configIndex, _uiControl);
                SetState(initialState, state, false);
                throw new ConfigRunnerException(string.Format(Resources.AutoQcConfigManager_StartConfig_Could_not_find_a_config_runner_for_configuration_name___0___, config.Name));
            }
            ProgramLog.Info(string.Format(Resources.ConfigManager_StartConfig_Starting_configuration___0__, config.Name));
            state.EnableConfig(configIndex, _uiControl);
            SetState(initialState, state);
            state.ConfigRunners.TryGetValue(config.Name, out configRunner);
            try
            {
                ((ConfigRunner)configRunner).Start();
            }
            catch (Exception)
            {
                state.DisableConfig(configIndex, _uiControl);
                SetState(initialState, state);
                state.ConfigRunners.TryGetValue(config.Name, out configRunner);
                ((ConfigRunner)configRunner).ChangeStatus(RunnerStatus.Error);
                throw;
            }
        }

        public void StopRunners()
        {
            foreach (var configRunner in AutoQcState.ConfigRunners.Values)
            {
                ((ConfigRunner)configRunner).Stop();
            }
        }
        #endregion
        
        #region Logging

        public override void SelectLog(int selected)
        {
            if (selected < 0 || selected >= AutoQcState.LogList.Count) // Accessing the AutoQcState property requires _lock 
                throw new IndexOutOfRangeException("No log at index: " + selected);
            SelectedLog = selected;
        }

        public void SelectLogOfSelectedConfig()
        {
            var state = AutoQcState;
            SelectedLog = state.GetLoggerIndex(state.GetSelectedConfig().Name);
        }


        #endregion
        
        #region Tests

        public bool ConfigListEquals(List<AutoQcConfig> otherConfigs)
        {
            var state = AutoQcState;
            if (otherConfigs.Count != state.BaseState.ConfigList.Count) return false;

            for (int i = 0; i < state.BaseState.ConfigList.Count; i++)
            {
                if (!Equals(otherConfigs[i], state.BaseState.ConfigList[i])) return false;
            }

            return true;
        }

        public bool ConfigOrderEquals(string[] configNames)
        {
            var state = AutoQcState;
            if (configNames.Length != state.BaseState.ConfigList.Count) return false;

            for (int i = 0; i < state.BaseState.ConfigList.Count; i++)
            {
                if (!Equals(configNames[i], state.BaseState.ConfigList[i].GetName())) return false;
            }

            return true;
        }

        #endregion

    }

    public class AutoQcConfigManagerState
    {
        public readonly ConfigManagerState BaseState;
        public ImmutableDictionary<string, IConfigRunner> ConfigRunners { get; private set; }
        public ImmutableList<Logger> LogList { get; private set; }
        public int SortedColumn { get; private set; }

        public static AutoQcConfigManagerState Empty()
        {
            return new AutoQcConfigManagerState(ConfigManagerState.Empty(), ImmutableDictionary<string, IConfigRunner>.Empty, ImmutableList<Logger>.Empty);
        }

        public AutoQcConfigManagerState(ConfigManagerState baseState, ImmutableDictionary<string, IConfigRunner> configRunners, ImmutableList<Logger> logList, int sortedColumn = 0)
        {
            BaseState = baseState;
            ConfigRunners = configRunners;
            LogList = logList;
            SortedColumn = sortedColumn;
        }

        public AutoQcConfigManagerState Copy()
        {
            return new AutoQcConfigManagerState(BaseState.Copy(), ConfigRunners, LogList, SortedColumn);
        }

        public void ValidateState()
        {
            var validated = ConfigRunners.Count == BaseState.ConfigList.Count;
            foreach (var config in BaseState.ConfigList)
            {
                if (!validated) break;
                if (!ConfigRunners.ContainsKey(config.GetName()))
                    validated = false;
            }
            if (!validated)
                throw new ArgumentException("Could not validate the new state of the configuration list. The operation did not succeed.");
        }


        public AutoQcConfig GetSelectedConfig()
        {
            return (AutoQcConfig)BaseState.GetSelectedConfig();
        }

        public AutoQcConfigManagerState DisableSelectedConfig(IMainUiControl uiControl)
        {
            return SetConfigEnabled(BaseState.Selected, false, uiControl);
        }

        public AutoQcConfigManagerState DisableConfig(int index, IMainUiControl uiControl)
        {
            return SetConfigEnabled(index, false, uiControl);
        }

        public AutoQcConfigManagerState EnableConfig(int index, IMainUiControl uiControl)
        {
            return SetConfigEnabled(index, true, uiControl);
        }

        private AutoQcConfigManagerState SetConfigEnabled(int index, bool enabled, IMainUiControl uiControl)
        {
            var config = (AutoQcConfig) BaseState.GetConfig(index);
            var configRunner = GetConfigRunner(config);
            if (configRunner == null)
            {
                ConfigManager.DisplayError(uiControl, string.Format("Could not find a config runner for config '{0}'", config.Name));
                return this;
            }
            if (GetConfigRunner(config).IsStarting())
            {
                ConfigManager.DisplayError(uiControl, "Cannot change config enabled while it is busy.");
                return this;
            }
            ProgramaticallyRemoveAt(index);
            ProgramaticallyInsertConfig(index,
                new AutoQcConfig(config.Name, enabled, config.Created, config.Modified, config.MainSettings,
                    config.PanoramaSettings, config.SkylineSettings), uiControl, configRunner.GetStatus());
            BaseState.ModelHasChanged();
            return this;
        }

        public AutoQcConfigManagerState DisableConfigProgramatically(int index, IMainUiControl uiControl)
        {
            var config = (AutoQcConfig)BaseState.GetConfig(index);

            BaseState.ProgramaticallyRemoveAt(index);
            BaseState.ProgramaticallyInsertConfig(index,
                 new AutoQcConfig(config.Name, false, config.Created, config.Modified, config.MainSettings,
                    config.PanoramaSettings, config.SkylineSettings));
            BaseState.ModelHasChanged();
            return this;
        }


        #region Add Configs

        public AutoQcConfigManagerState LoadConfigList(IMainUiControl uiControl)
        {
            BaseState.LoadConfigList();
            UpdateFromBaseState(uiControl);
            for (int i = 0; i < BaseState.ConfigList.Count; i++)
            {
                var config = (AutoQcConfig)BaseState.ConfigList[i];
                if (config.IsEnabled && !Settings.Default.KeepAutoQcRunning)
                {
                    // If the config was running last time AutoQC Loader was running (and properties saved), but we are not 
                    // automatically starting configs on startup, change its IsEnabled state
                    DisableConfig(i, uiControl);
                }
            }
            return this;
        }

        public AutoQcConfigManagerState ReplaceSelectedConfig(IConfig newConfig, IMainUiControl uiControl)
        {
            var index = BaseState.Selected;
            ProgramaticallyRemoveAt(index);
            UserInsertConfig(index, newConfig, uiControl);
            return this;
        }

        public AutoQcConfigManagerState UserAddConfig(IConfig iconfig, IMainUiControl uiControl)
        {
            UserInsertConfig(BaseState.ConfigList.Count, iconfig, uiControl);
            return this;
        }

        protected AutoQcConfigManagerState UserInsertConfig(int index, IConfig iconfig, IMainUiControl uiControl)
        {
            BaseState.UserInsertConfig(index, iconfig);
            AddConfig(iconfig, uiControl);
            //UpdateValues(selected: index);
            return this;
        }

        protected AutoQcConfigManagerState ProgramaticallyInsertConfig(int index, IConfig iconfig, IMainUiControl uiControl, 
            RunnerStatus runnerStatus)
        {
            BaseState.ProgramaticallyInsertConfig(index, iconfig);
            return AddConfig(iconfig, uiControl, runnerStatus);
        }

        private AutoQcConfigManagerState AddConfig(IConfig iconfig, IMainUiControl uiControl, RunnerStatus runnerStatus = RunnerStatus.Stopped)
        {
            var config = (AutoQcConfig)iconfig;
            if (ConfigRunners.ContainsKey(config.Name))
            {
                // TODO: Should the config be programatically removed if this exception is thrown?
                // If the config has just been added, there should not be another config with this name, and there should not be a config runner
                // with this name. Should we just replace the existing config runner if this ever happens? 
                throw new Exception(
                    string.Format("Config runner already exists for configuration with name '{0}'", config.Name));
            }

            var logFile = config.getConfigFilePath("AutoQC.log");

            var logger = new Logger(logFile, config.Name, false);  // Do not initialize the logger in the constructor; it will be initialized when we start the config.
            LogList = LogList.Add(logger);
            var runner = new ConfigRunner(config, logger, runnerStatus, uiControl);
            ConfigRunners = ConfigRunners.Add(config.Name, runner);
            return this;
        }


        #endregion

        #region Remove Configs

        public AutoQcConfigManagerState UserRemoveSelected(IMainUiControl uiControl, out bool removed)
        {
            BaseState.AssertConfigSelected();
            UserRemoveAt(BaseState.Selected, uiControl, out removed);
            return this;
        }

        public AutoQcConfigManagerState UserRemoveAt(int index, IMainUiControl uiControl, out bool removed)
        {
            var configRunner = (ConfigRunner)ConfigRunners[BaseState.ConfigList[index].GetName()];
            removed = false;
            if (configRunner.IsBusy()) // Not stopped or error
            {
                string message;

                if (configRunner.IsPending()) // Stopping / Starting / Loading
                {
                    message =
                        (string.Format(Resources.AutoQcConfigManager_UserRemoveAt_The_configuration___0___is__1___Please_wait_for_the_action_to_complete_and_try_again_, configRunner.GetConfigName(), configRunner.GetStatus().ToString()));
                }
                else
                {
                    message =
                        (string.Format(Resources.AutoQcConfigManager_UserRemoveAt_The_configuration___0___is_running__Please_stop_the_configuration_and_try_again_, configRunner.GetConfigName()));
                }
                ConfigManager.DisplayWarning(uiControl, message);
                return this;
            }
            // remove config
            BaseState.UserRemoveAt(index);
            RemoveConfig(configRunner.Config);
            removed = true;
            return this;
        }

        private AutoQcConfigManagerState ProgramaticallyRemoveAt(int index)
        {
            var config = BaseState.ConfigList[index];
            BaseState.ProgramaticallyRemoveAt(index);
            RemoveConfig(config);
            return this;
        }

        private AutoQcConfigManagerState RemoveConfig(IConfig iconfig)
        {
            var config = (AutoQcConfig)iconfig;
            if (!ConfigRunners.ContainsKey(config.Name))
                throw new Exception("Config runner does not exist.");
            int i = 0;
            while (i < LogList.Count)
            {
                if (LogList[i].Name.Equals(config.Name)) break;
                i++;
            }
            LogList = LogList.RemoveAt(i);
            // TODO: what happens here?
            //_uiControl?.ClearLog();
            ConfigRunners = ConfigRunners.Remove(config.Name);
            return this;
        }

        #endregion

        #region Update Configs

        public AutoQcConfigManagerState ReplaceSkylineSettings(SkylineSettings skylineSettings, IMainUiControl uiControl, out bool? replaced)
        {
            var runningConfigs = GetRunningConfigs();
            BaseState.AskToReplaceAllSkylineVersions(skylineSettings, runningConfigs, uiControl, out replaced);
            UpdateFromBaseState(uiControl);
            return this;
        }

        public AutoQcConfigManagerState SetConfigInvalid(IConfig config)
        {
            BaseState.SetConfigInvalid(config);
            return this;
        }

        private AutoQcConfigManagerState UpdateFromBaseState(IMainUiControl uiControl)
        {
            foreach (var iconfig in BaseState.ConfigList)
            {
                var name = iconfig.GetName();
                Logger configLogger = null;
                foreach (var logger in LogList)
                {
                    if (logger.Name.Equals(iconfig.GetName()))
                        configLogger = logger;
                }
                var logFile = ((AutoQcConfig)iconfig).getConfigFilePath("AutoQC.log");
                var newLogger = configLogger ?? new Logger(logFile, iconfig.GetName(), false);
                if (ConfigRunners.ContainsKey(name))
                {
                    if (!ConfigRunners[name].GetConfig().Equals(iconfig))
                        ConfigRunners = ConfigRunners.Remove(name).Add(name,
                            new ConfigRunner((AutoQcConfig)iconfig, newLogger, uiControl));
                }
                else
                {
                    ConfigRunners = ConfigRunners.Add(name, new ConfigRunner((AutoQcConfig)iconfig, newLogger, uiControl));
                    LogList = LogList.Add(newLogger);
                }
            }
            foreach (var config in ConfigRunners.Keys)
            {
                if (BaseState.GetConfigIndex(config) < 0)
                    ConfigRunners = ConfigRunners.Remove(config);
            }

            var deletingLoggerIndicies = new List<int>();
            int i = 0;
            foreach (var logger in LogList)
            {
                if (!ConfigRunners.ContainsKey(logger.Name))
                    deletingLoggerIndicies.Add(i);
                i++;
            }

            deletingLoggerIndicies.Reverse();
            foreach (var index in deletingLoggerIndicies)
                LogList = LogList.RemoveAt(index);
            return this;
        }


        #endregion

        #region Sort configs

        public enum SortColumn
        {
            Name,
            User,
            Created,
            RunnerStatus
        }

        public AutoQcConfigManagerState SortByValue(int columnIndex)
        {
            List<int> newIndexOrder = null;
            switch (columnIndex)
            {
                case (int)SortColumn.Name:
                    var nameTuples = new List<Tuple<string, int>>();
                    for (int i = 0; i < BaseState.ConfigList.Count; i++)
                        nameTuples.Add(new Tuple<string, int>(((AutoQcConfig)BaseState.ConfigList[i]).Name, i));
                    nameTuples.Sort((a, b) => String.Compare(a.Item1, b.Item1, StringComparison.Ordinal));
                    newIndexOrder = nameTuples.Select(t => t.Item2).ToList();
                    break;

                case (int)SortColumn.User:
                    var userTuples = new List<Tuple<string, int>>();
                    for (int i = 0; i < BaseState.ConfigList.Count; i++)
                        userTuples.Add(new Tuple<string, int>(((AutoQcConfig)BaseState.ConfigList[i]).User, i));
                    userTuples.Sort((((a, b) =>
                    {
                        if (string.IsNullOrEmpty(a.Item1) && string.IsNullOrEmpty(a.Item1)) return 1;
                        if (string.IsNullOrEmpty(b.Item1)) return -1;
                        return String.Compare(a.Item1, b.Item1, StringComparison.Ordinal);
                    })));
                    newIndexOrder = userTuples.Select(t => t.Item2).ToList();
                    break;

                case (int)SortColumn.Created:
                    var createdTuples = new List<Tuple<DateTime, int>>();
                    for (int i = 0; i < BaseState.ConfigList.Count; i++)
                        createdTuples.Add(new Tuple<DateTime, int>(((AutoQcConfig)BaseState.ConfigList[i]).Created, i));
                    createdTuples.Sort((a, b) => b.Item1.CompareTo(a.Item1));
                    newIndexOrder = createdTuples.Select(t => t.Item2).ToList();
                    break;

                case (int)SortColumn.RunnerStatus:
                    var statusTuples = new List<Tuple<RunnerStatus, int>>();
                    for (int i = 0; i < BaseState.ConfigList.Count; i++)
                        statusTuples.Add(new Tuple<RunnerStatus, int>(ConfigRunners[BaseState.ConfigList[i].GetName()].GetStatus(), i));
                    statusTuples.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                    newIndexOrder = statusTuples.Select(t => t.Item2).ToList();
                    break;
            }

            if (newIndexOrder == null)
                return this;
            ReorderConfigs(newIndexOrder, columnIndex == SortedColumn);
            SortedColumn = columnIndex;
            return this;
        }

        private AutoQcConfigManagerState ReorderConfigs(List<int> newIndexOrder, bool sameColumn)
        {
            if (sameColumn && IsSorted(newIndexOrder))
                return ReverseConfigList();
            BaseState.ReorderConfigs(newIndexOrder);
            return this;
        }

        private bool IsSorted(List<int> list)
        {
            if (list.Count == 0) return true;
            var lastValue = list[0];
            foreach (var value in list)
            {
                if (value < lastValue)
                    return false;
            }
            return true;
        }

        private AutoQcConfigManagerState ReverseConfigList()
        {
            var newIndexOrder = new List<int>();
            for(int i = 0; i < BaseState.ConfigList.Count; i++)
                newIndexOrder.Insert(0, i);
            BaseState.ReorderConfigs(newIndexOrder);
            return this;
        }


        #endregion

        #region Config Runners

        public ConfigRunner GetSelectedConfigRunner()
        {
            return (ConfigRunner)ConfigRunners[GetSelectedConfig().GetName()];
        }

        public ConfigRunner GetConfigRunner(IConfig config)
        {
            if (ConfigRunners.TryGetValue(config.GetName(), out var configRunner))
            {
                return (ConfigRunner)configRunner;
            }

            return null;
        }

        public List<string> GetRunningConfigs()
        {
            var runningConfigs = new List<string>();
            foreach (var configRunner in ConfigRunners.Values)
            {
                if (configRunner.IsBusy())
                    runningConfigs.Add(configRunner.GetConfigName());
            }
            return runningConfigs;
        }

        #endregion

        #region Logging

        public Logger GetLogger(string name)
        {
            return LogList[GetLoggerIndex(name)];
        }

        public int GetLoggerIndex(string name)
        {
            for (int i = 0; i < LogList.Count; i++)
            {
                if (LogList[i].Name.Equals(name))
                    return i;
            }
            return -1;
        }

        #endregion

        #region Import/Export

        public AutoQcConfigManagerState Import(string filePath, IMainUiControl uiControl, out List<int> addedIndicies)
        {
            BaseState.ImportFrom(AutoQcConfig.ReadXml, filePath, Settings.Default.XmlVersion, uiControl, null, out addedIndicies);
            UpdateFromBaseState(uiControl);
            return this;
        }

        #endregion

        #region Override Equals

        protected bool Equals(AutoQcConfigManagerState other)
        {
            if (LogList.Count != other.LogList.Count)
                return false;
            for (int i = 0; i < LogList.Count; i++)
            {
                if (!Equals(LogList[i], other.LogList[i]))
                    return false;
            }
            if (ConfigRunners.Count != other.ConfigRunners.Count)
                return false;
            foreach (var config in ConfigRunners.Keys)
            {
                if (!other.ConfigRunners.ContainsKey(config))
                    return false;
                if ((ConfigRunners[config].IsBusy() || other.ConfigRunners[config].IsBusy()) &&
                    !Equals(ConfigRunners[config], other.ConfigRunners[config]))
                    return false;
                if (!Equals(ConfigRunners[config].GetConfig(), other.ConfigRunners[config].GetConfig()))
                    return false;
            }
            return Equals(BaseState, other.BaseState) && SortedColumn == other.SortedColumn;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AutoQcConfigManagerState)obj);
        }

        public override int GetHashCode()
        {
            return BaseState.GetHashCode() +
                   LogList.GetHashCode() +
                   ConfigRunners.GetHashCode();
        }

        #endregion

    }
}
