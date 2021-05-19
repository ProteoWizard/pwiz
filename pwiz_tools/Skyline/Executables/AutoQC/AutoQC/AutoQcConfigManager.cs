using System;
using System.Collections.Generic;
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

        private readonly Dictionary<string, IConfigRunner> _configRunners; // dictionary mapping from config name to that config's runner
        private int _sortedColumn; // column index the configurations were last sorted by

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
            importer = AutoQcConfig.ReadXml;
            SelectedLog = -1;
            _sortedColumn = -1;
            _configRunners = new Dictionary<string, IConfigRunner>();
            _uiControl = uiControl;
            _runningUi = uiControl != null;
            LoadConfigList();

            Init();
        }

        private new void LoadConfigList()
        {
            var configs = base.LoadConfigList();
            foreach (var iconfig in configs)
            {
                var config = (AutoQcConfig)iconfig;
                if (config.IsEnabled && !Settings.Default.KeepAutoQcRunning)
                {
                    // If the config was running last time AutoQC Loader was running (and properties saved), but we are not 
                    // automatically starting configs on startup, change its IsEnabled state
                    config.IsEnabled = false;
                }
                ProgramaticallyAddConfig(iconfig);
            }
        }

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

        public void ReplaceSelectedConfig(IConfig newConfig)
        {
            var index = SelectedConfig;
            var config = GetSelectedConfig();
            ProgramaticallyRemoveAt(index);
            try
            {
                UserInsertConfig(newConfig, index);
            }
            catch (Exception) // Catch all exceptions here otherwise, the old config is removed, and the new one is not added.
            {
                UserInsertConfig(config, index);
                throw;
            }
        }

        #region Add Configs

        public void UserAddConfig(IConfig iconfig) => UserInsertConfig(iconfig, _configList.Count);

        public new void UserInsertConfig(IConfig iconfig, int index)
        {
            base.UserInsertConfig(iconfig, index);
            AddConfig(iconfig);
            SelectConfig(index);
        }

        private void ProgramaticallyAddConfig(IConfig iconfig) => ProgramaticallyInsertConfig(iconfig, _configList.Count);

        private new void ProgramaticallyInsertConfig(IConfig iconfig, int index)
        {
            base.ProgramaticallyInsertConfig(iconfig, index);
            AddConfig(iconfig);
        }

        private void AddConfig(IConfig iconfig)
        {
            var config = (AutoQcConfig)iconfig;
            if (_configRunners.ContainsKey(config.Name))
            {
                throw new Exception(
                    string.Format("Config runner already exists for configuration with name '{0}'", config.Name));
            }

            // The config should have been validated by the time we get here. No need to check the directory path
            // var directory = Path.GetDirectoryName(config.MainSettings.SkylineFilePath);
            // if (directory == null)
            //     throw new Exception("Cannot have a null Skyline file directory.");

            var logFile = config.getConfigFilePath("AutoQC.log");

            var logger = new Logger(logFile, config.Name, 
                false, // Do not initialize the logger in the constructor; it will be initialized when we start the config.
                _uiControl);
            _logList.Add(logger);
            var runner = new ConfigRunner(config, logger, _uiControl);
            _configRunners.Add(config.Name, runner);
        }

        #endregion

        #region Remove Configs

        public void UserRemoveSelected()
        {
            AssertConfigSelected();
            UserRemoveAt(SelectedConfig);
        }

        public new void UserRemoveAt(int index)
        {
            lock (_lock)
            {
                var configRunner = (ConfigRunner)_configRunners[_configList[index].GetName()];
                if (configRunner.IsBusy())
                {
                    string message = null;
                    if (configRunner.IsStarting() || configRunner.IsRunning())
                    {
                        message =
                            string.Format(
                                Resources.MainForm_btnDelete_Click_Configuration___0___is_running__Please_stop_the_configuration_and_try_again__,
                                configRunner.GetConfigName());
                    }
                    else if (configRunner.IsStopping())
                    {
                        message =
                            string.Format(
                                Resources.MainForm_btnDelete_Click_Please_wait_for_the_configuration___0___to_stop_and_try_again_,
                                configRunner.GetConfigName());
                    }
                    DisplayWarning(message);
                    return;
                }
                // remove config
                base.UserRemoveAt(index);
                RemoveConfig(configRunner.Config);
            }
        }
        
        private new void ProgramaticallyRemoveAt(int index)
        {
            var config = _configList[index];
            base.ProgramaticallyRemoveAt(index);
            RemoveConfig(config);
        }

        private void RemoveConfig(IConfig iconfig)
        {
            var config = (AutoQcConfig) iconfig;
            if (!_configRunners.ContainsKey(config.Name))
                throw new Exception("Config runner does not exist.");
            int i = 0;
            while (i < _logList.Count)
            {
                if (_logList[i].Name.Equals(config.Name)) break;
                i++;
            }
            _logList.RemoveAt(i);
            _uiControl?.ClearLog();
            _configRunners.Remove(config.Name);
        }

        #endregion
        
        #region Configs

        public new AutoQcConfig GetSelectedConfig()
        {
            return (AutoQcConfig)base.GetSelectedConfig();
        }

        public List<ListViewItem> ConfigsListViewItems(Graphics graphics)
        {
            return ConfigsListViewItems(_configRunners, graphics);
        }

        public void ReplaceSkylineSettings(SkylineSettings skylineSettings)
        {
            var runningConfigs = ConfigsRunning();
            var replacedConfigs = GetReplacedSkylineSettings(skylineSettings, runningConfigs);
            foreach (var indexAndConfig in replacedConfigs)
            {
                ProgramaticallyRemoveAt(indexAndConfig.Item1);
                ProgramaticallyInsertConfig(indexAndConfig.Item2, indexAndConfig.Item1);
            }
            if (runningConfigs.Count > 0)
                throw new ArgumentException(Resources.AutoQcConfigManager_ReplaceSkylineSettings_The_following_configurations_are_running_and_could_not_be_updated_
                                            + Environment.NewLine +
                                            TextUtil.LineSeparate(runningConfigs));
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

        public void SortByValue(int columnIndex)
        {
            lock (_lock)
            {
                List<int> newIndexOrder = null;

                switch (columnIndex)
                {
                    case (int)SortColumn.Name:
                        var nameTuples = new List<Tuple<string, int>>();
                        for (int i = 0; i < _configList.Count; i++)
                            nameTuples.Add(new Tuple<string, int>(((AutoQcConfig)_configList[i]).Name, i));
                        nameTuples.Sort((a, b) => String.Compare(a.Item1, b.Item1, StringComparison.Ordinal));
                        newIndexOrder = nameTuples.Select(t => t.Item2).ToList();
                        break;

                    case (int)SortColumn.User:
                        var userTuples = new List<Tuple<string, int>>();
                        for (int i = 0; i < _configList.Count; i++)
                            userTuples.Add(new Tuple<string, int>(((AutoQcConfig)_configList[i]).User, i));
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
                        for (int i = 0; i < _configList.Count; i++)
                            createdTuples.Add(new Tuple<DateTime, int>(((AutoQcConfig)_configList[i]).Created, i));
                        createdTuples.Sort((a, b) => b.Item1.CompareTo(a.Item1));
                        newIndexOrder = createdTuples.Select(t => t.Item2).ToList();
                        break;

                    case (int)SortColumn.RunnerStatus:
                        var statusTuples = new List<Tuple<RunnerStatus, int>>();
                        for (int i = 0; i < _configList.Count; i++)
                            statusTuples.Add(new Tuple<RunnerStatus, int>(_configRunners[_configList[i].GetName()].GetStatus(), i));
                        statusTuples.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                        newIndexOrder = statusTuples.Select(t => t.Item2).ToList();
                        break;
                }

                if (newIndexOrder == null)
                    return;
                ReorderConfigs(newIndexOrder, _sortedColumn == columnIndex);
                _sortedColumn = columnIndex;
            }
        }

        private void ReorderConfigs(List<int> newIndexOrder, bool sameColumn)
        {
            if (sameColumn && IsSorted(newIndexOrder))
            {
                ReverseConfigList();
                return;
            }
            var selectedName = HasSelectedConfig() ? GetSelectedConfig().Name : null;
            var configListHolder = CutConfigList();
            foreach (var index in newIndexOrder)
            {
                _configList.Add(configListHolder[index]);
            }
            if (selectedName != null) SelectConfig(GetConfigIndex(selectedName));
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

        private AutoQcConfig[] CutConfigList()
        {
            var configListHolder = new AutoQcConfig[_configList.Count];
            for (int i = 0; i < _configList.Count; i++)
                configListHolder[i] = (AutoQcConfig) _configList[i];
            _configList.Clear();
            return configListHolder;
        }

        private void ReverseConfigList()
        {
            var selectedName = HasSelectedConfig() ? GetSelectedConfig().Name : null;
            var configListHolder = CutConfigList();
            foreach (var config in configListHolder)
                _configList.Insert(0, config);
            if (selectedName != null) SelectConfig(GetConfigIndex(selectedName));
        }


        #endregion
        
        #region Run Configs

        public ConfigRunner GetSelectedConfigRunner()
        {
            return (ConfigRunner)_configRunners[GetSelectedConfig().GetName()];
        }

        public void RunEnabled()
        {
            lock (_lock)
            {
                foreach (var config in _configList)
                {
                    var autoQcConfig = (AutoQcConfig) config;
                    if (!autoQcConfig.IsEnabled)
                        continue;
                    StartConfig(autoQcConfig);
                }
            }
        }

        public bool StopConfiguration()
        {
            lock (_lock)
            {
                var selectedConfig = GetSelectedConfig();
                if (!selectedConfig.IsEnabled) // TODO: Do we need this?
                    return false;
                
                var configRunner = GetSelectedConfigRunner();
                if (configRunner.IsStarting() || configRunner.IsStopping())
                {
                    var message = string.Format("Cannot stop a configuration that is {0}. Please wait until the configuration has finished {0}.",
                        configRunner.IsStarting() ? Resources.ConfigManager_UpdateSelectedEnabled_starting : Resources.ConfigManager_UpdateSelectedEnabled_stopping);

                    DisplayWarning(message);
                    return false;
                }

                var doChange = DisplayQuestion(string.Format(
                    Resources.ConfigManager_UpdateSelectedEnabled_Are_you_sure_you_want_to_stop_configuration__0__,
                    configRunner.GetConfigName()));

                if (doChange == DialogResult.Yes)
                {
                    selectedConfig.IsEnabled = false;
                    configRunner.Stop();
                    return true;
                }

                return false;
            }
        }

        public bool UpdateSelectedEnabled(bool newIsEnabled)
        {
            return newIsEnabled ? StartConfiguration() : StopConfiguration();
        }

        public bool StartConfiguration()
        {
            lock (_lock)
            {
                var selectedConfig = GetSelectedConfig();
                if (selectedConfig.IsEnabled) // TODO: Do we need this?
                    return false;
                try
                {
                    selectedConfig.Validate();
                }
                catch (ArgumentException e)
                {
                    DisplayError(TextUtil.LineSeparate(string.Format("Cannot run the configuration '{0}' because it could not be validated.", selectedConfig.Name),
                                 "The error was:",
                                 e.Message,
                                 "Please edit the configuration and try again."));

                    SetConfigInvalid(selectedConfig);
                    return false;
                }

                SetConfigValid(selectedConfig);

                var configRunner = GetSelectedConfigRunner();
                if (configRunner.IsStarting() || configRunner.IsStopping())
                {
                    var message = string.Format(
                        "Cannot start a configuration that is {0}. Please wait until the configuration has finished {0}.",
                        configRunner.IsStarting()
                            ? Resources.ConfigManager_UpdateSelectedEnabled_starting
                            : Resources.ConfigManager_UpdateSelectedEnabled_stopping);

                    DisplayWarning(message);
                    return false;
                }

                StartConfig(selectedConfig);
                return true;
            }
        }

        private void StartConfig(AutoQcConfig config)
        {
            config.IsEnabled = true;
            var configRunner = _configRunners[config.Name];
            ProgramLog.Info(string.Format(Resources.ConfigManager_StartConfig_Starting_configuration___0__, config.Name));
            try
            {
                ((ConfigRunner)configRunner).Start();
            }
            catch (Exception e)
            {
                DisplayErrorWithException(string.Format(Resources.ConfigManager_StartConfig_Error_starting_configuration__0__, configRunner.GetConfig().GetName()) + Environment.NewLine +
                                          e.Message, e);
                // ReSharper disable once LocalizableElement
                ProgramLog.Error(string.Format(Resources.ConfigManager_StartConfig_Error_starting_configuration___0__, configRunner.GetConfig().GetName()), e);
            }
        }

        public void StopRunners()
        {
            foreach (var configRunner in _configRunners.Values)
            {
                ((ConfigRunner)configRunner).Stop();
            }
        }

        public List<string> ConfigsRunning()
        {
            var runningConfigs = new List<string>();
            foreach (var configRunner in _configRunners.Values)
            {
                if (configRunner.IsBusy())
                    runningConfigs.Add(configRunner.GetConfigName());
            }
            return runningConfigs;
        }

        #endregion
        
        #region Logging

        public void SelectLog(int selected)
        {
            if (selected < 0 || selected >= _logList.Count)
                throw new IndexOutOfRangeException("No log at index: " + selected);
            GetSelectedLogger().DisableUiLogging();
            SelectedLog = selected;
            GetSelectedLogger().LogToUi(_uiControl);
        }

        public void SelectLogOfSelectedConfig()
        {
            lock (_lock)
            {
                SelectedLog = GetLoggerIndex(GetSelectedConfig().Name);
            }
        }

        public Logger GetLogger(string name)
        {
            return _logList[GetLoggerIndex(name)];
        }

        private int GetLoggerIndex(string name)
        {
            for (int i = 0; i < _logList.Count; i++)
            {
                if (_logList[i].Name.Equals(name))
                    return i;
            }
            return -1;
        }

        #endregion

        #region Import/Export

        public void Import(string filePath, ShowDownloadedFileForm showDownloadedFileForm)
        {
            var addedConfigs = ImportFrom(filePath, showDownloadedFileForm);
            foreach (var config in addedConfigs)
            {
                // Handle overwritten duplicate configs
                if (_configRunners.ContainsKey(config.GetName()))
                    ProgramaticallyRemoveAt(GetConfigIndex(config.GetName()));
                ProgramaticallyAddConfig(config);
            }
        }

        #endregion
        
        #region Tests

        public bool ConfigListEquals(List<AutoQcConfig> otherConfigs)
        {
            if (otherConfigs.Count != _configList.Count) return false;

            for (int i = 0; i < _configList.Count; i++)
            {
                if (!Equals(otherConfigs[i], _configList[i])) return false;
            }

            return true;
        }

        public bool ConfigOrderEquals(string[] configNames)
        {
            if (configNames.Length != _configList.Count) return false;

            for (int i = 0; i < _configList.Count; i++)
            {
                if (!Equals(configNames[i], _configList[i].GetName())) return false;
            }

            return true;
        }

        #endregion
    }
}
