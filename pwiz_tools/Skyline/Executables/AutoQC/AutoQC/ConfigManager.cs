using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using AutoQC.Properties;

namespace AutoQC
{
    public class ConfigManager
    {
        // Handles all modification to configs, the config list, configRunners, and log files
        // The UI should reflect the configs, runners, and log files from this class


        private List<AutoQcConfig> _configList; // the list of configurations. Every config must have a runner in configRunners
        private readonly Dictionary<string, ConfigRunner> _configRunners; // dictionary mapping from config name to that config's runner

        //private readonly IAutoQcLogger _logger; // the current logger - always logs to AutoQc.log
        private readonly List<IAutoQcLogger> _loggers; // list of archived loggers, from most recent to least recent

        private readonly bool _runningUi; // if the UI is displayed (false when testing)
        private readonly IMainUiControl _uiControl; // null if no UI displayed

        //private readonly object _lock = new object(); // lock required for any mutator or getter method on _configList, _configRunners, or SelectedConfig
        
        private int _sortedColumn; // column index the configurations were last sorted by

        public ConfigManager(IMainUiControl uiControl = null)
        {
            SelectedConfig = -1;
            SelectedLog = -1;
            _sortedColumn = -1;
            _uiControl = uiControl;
            _runningUi = uiControl != null;
            _configRunners = new Dictionary<string, ConfigRunner>();
            _loggers = new List<IAutoQcLogger>();
            LoadConfigList();
        }

        public int SelectedConfig { get; private set; } // index of the selected configuration
        public int SelectedLog { get; private set; } // index of the selected log
        

        public enum SortColumn
        {
            Name,
            User,
            Created,
            RunnerStatus
        }



        private void LoadConfigList()
        {
            _configList = new List<AutoQcConfig>();
            foreach (var config in Settings.Default.ConfigList)
            {
                if (config.IsEnabled && !Settings.Default.KeepAutoQcRunning)
                {
                    // If the config was running last time AutoQC Loader was running (and properties saved), but we are not 
                    // automatically starting configs on startup, change its IsEnabled state
                    config.IsEnabled = false;
                }

                var newLogger = new AutoQcLogger(config, _uiControl);
                var newRunner = new ConfigRunner(config, newLogger, _uiControl);
                _loggers.Add(newLogger);
                _configList.Add(config);
                _configRunners.Add(config.Name, newRunner);
            }
        }

        public void Close()
        {
            SaveConfigList();
            StopRunners();
        }

        private void SaveConfigList()
        {
            var updatedConfigs = new ConfigList();
            foreach (var config in _configList)
            {
                updatedConfigs.Add(config);
            }
            Settings.Default.ConfigList = updatedConfigs;
            Settings.Default.Save();
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


        #region Configs

        public List<ListViewItem> ConfigsListViewItems()
        {
            var listViewConfigs = new List<ListViewItem>();
            var runnerStatusIndex = 3;
            foreach (var config in _configList)
            {
                var lvi = new ListViewItem(config.Name);
                var configRunner = _configRunners[config.Name];
                lvi.UseItemStyleForSubItems = false; // So that we can change the color for sub-items.
                lvi.SubItems.Add(config.User);
                lvi.SubItems.Add(config.Created.ToShortDateString());
                lvi.SubItems.Add(configRunner.GetDisplayStatus());
                lvi.SubItems[runnerStatusIndex].ForeColor = configRunner.GetDisplayColor();
                try
                {
                    config.Validate();
                }
                catch (ArgumentException)
                {
                    lvi.ForeColor = Color.Red;
                }
                listViewConfigs.Add(lvi);
            }
            return listViewConfigs;
        }

        public bool HasConfigs()
        {
            return _configList.Count > 0;
        }

        public bool HasSelectedConfig()
        {
            return SelectedConfig >= 0;
        }

        public void SelectConfig(int newIndex)
        {
            if (newIndex < 0 || newIndex >= _configList.Count)
                throw new IndexOutOfRangeException("No configuration at index: " + newIndex);
            SelectedConfig = newIndex;
        }

        public void DeselectConfig()
        {
            SelectedConfig = -1;
        }

        private void CheckConfigSelected()
        {
            if (SelectedConfig < 0)
            {
                throw new IndexOutOfRangeException("No configuration selected.");
            }
        }

        public AutoQcConfig GetSelectedConfig()
        {
            CheckConfigSelected();
            return _configList[SelectedConfig];
        }

        public AutoQcConfig GetConfigAt(int index)
        {
            return _configList[index];
        }

        public void AddConfiguration(AutoQcConfig config)
        {
            InsertConfiguration(config, _configList.Count);
        }

        private void InsertConfiguration(AutoQcConfig config, int index, IAutoQcLogger oldLogger = null)
        {
            CheckIfExists(config, false);
            Program.LogInfo(string.Format("Adding configuration \"{0}\"", config.Name));
            _configList.Insert(index, config);

            var newLogger = new AutoQcLogger(config, _uiControl, oldLogger);
            _loggers.Add(newLogger);
            var newRunner = new ConfigRunner(config, newLogger, _uiControl);
            _configRunners.Add(config.Name, newRunner);

        }

        public void ReplaceSelectedConfig(AutoQcConfig newConfig)
        {
            CheckConfigSelected();
            var oldConfig = _configList[SelectedConfig];

            if (!string.Equals(oldConfig.Name, newConfig.Name))
            {
                CheckIfExists(newConfig, false);
            }

            var oldLogger = GetLogger(oldConfig.Name);
            RemoveConfig(oldConfig);
            InsertConfiguration(newConfig, SelectedConfig, oldLogger);
        }

        public void RemoveSelected()
        {
            CheckConfigSelected();
            var configRunner = GetSelectedConfigRunner();

            var config = configRunner.Config;

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
                DisplayWarning(Resources.MainForm_btnDelete_Click_Cannot_Delete, message);
                return;
            }

            var doDelete = DisplayQuestion(Resources.MainForm_btnDelete_Click_Confirm_Delete,
                    string.Format(Resources.MainForm_btnDelete_Click_Are_you_sure_you_want_to_delete_configuration___0___, configRunner.GetConfigName()));


            if (doDelete != DialogResult.Yes)
                return;

            // remove config
            Program.LogInfo(string.Format("Removing configuration \"{0}\"", config.Name));
            RemoveConfig(config);
            DeselectConfig();
        }

        private void RemoveConfig(AutoQcConfig config)
        {
            CheckIfExists(config, true);
            _configList.Remove(config);
            _configRunners[config.Name].Stop();
            _configRunners.Remove(config.Name);
            RemoveLogger(config.Name);
        }

        private void CheckIfExists(AutoQcConfig config, bool expectedValue)
        {
            bool exists = _configRunners.Keys.Contains(config.Name);
            if (exists != expectedValue)
            {
                var message = expectedValue
                        ? string.Format(Resources.ConfigManager_CheckIfExists_Error_configuration_does_not_exist, config.Name)
                        : string.Format(Resources.ConfigManager_CheckIfExists_Error_configuration_already_exists, config.Name);
                throw new ArgumentException(message);
            }

        }

        #endregion


        #region Sort configs

        public void SortByValue(int columnIndex)
        {
            List<int> newIndexOrder = null;

            switch (columnIndex)
            {
                case (int)SortColumn.Name:
                    var nameTuples = new List<Tuple<string, int>>();
                    for (int i = 0; i < _configList.Count; i++)
                        nameTuples.Add(new Tuple<string, int>(_configList[i].Name, i));
                    nameTuples.Sort((a, b) => String.Compare(a.Item1, b.Item1, StringComparison.Ordinal));
                    newIndexOrder = nameTuples.Select(t => t.Item2).ToList();
                    break;

                case (int)SortColumn.User:
                    var userTuples = new List<Tuple<string, int>>();
                    for (int i = 0; i < _configList.Count; i++)
                        userTuples.Add(new Tuple<string, int>(_configList[i].User, i));
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
                        createdTuples.Add(new Tuple<DateTime, int>(_configList[i].Created, i));
                    createdTuples.Sort((a, b) => b.Item1.CompareTo(a.Item1));
                    newIndexOrder = createdTuples.Select(t => t.Item2).ToList();
                    break;

                case (int)SortColumn.RunnerStatus:
                    var statusTuples = new List<Tuple<ConfigRunner.RunnerStatus, int>>();
                    for (int i = 0; i < _configList.Count; i++)
                        statusTuples.Add(new Tuple<ConfigRunner.RunnerStatus, int>(_configRunners[_configList[i].Name].GetStatus(), i));
                    statusTuples.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                    newIndexOrder = statusTuples.Select(t => t.Item2).ToList();
                    break;
            }

            if (newIndexOrder == null)
                return;
            ReorderConfigs(newIndexOrder, _sortedColumn == columnIndex);
            _sortedColumn = columnIndex;
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
            SelectConfigFromName(selectedName);
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

        private void SelectConfigFromName(string name)
        {
            DeselectConfig();
            for (int i = 0; i < _configList.Count; i++)
            {
                if (_configList[i].Name.Equals(name))
                    SelectConfig(i);
            }
        }

        private AutoQcConfig[] CutConfigList()
        {
            var configListHolder = new AutoQcConfig[_configList.Count];
            _configList.CopyTo(configListHolder);
            _configList.Clear();
            return configListHolder;
        }

        private void ReverseConfigList()
        {
            var selectedName = HasSelectedConfig() ? GetSelectedConfig().Name : null;
            var configListHolder = CutConfigList();
            foreach (var config in configListHolder)
                _configList.Insert(0, config);
            SelectConfigFromName(selectedName);
        }


        #endregion
        

        #region Run Configs

        public ConfigRunner GetSelectedConfigRunner()
        {
            CheckConfigSelected();
            return _configRunners[_configList[SelectedConfig].Name];
        }

        public bool GetSelectedConfigBusy()
        {
            if (SelectedConfig < 0)
                return false;
            return GetSelectedConfigRunner().IsBusy();
        }

        public void RunEnabled()
        {

            foreach (var config in _configList)
            {
                if (!config.IsEnabled)
                    continue;
                StartConfig(config);
            }
        }

        public void UpdateSelectedEnabled(bool newIsEnabled)
        {
            var selectedConfig = GetSelectedConfig();
            if (selectedConfig.IsEnabled == newIsEnabled)
                return;
            try
            {
                selectedConfig.Validate();
            }
            catch (ArgumentException)
            {
                DisplayError(Resources.ConfigManager_Run_Error,
                    string.Format(Resources.ConfigManager_Please_edit_configuration__0__and_try_again_, selectedConfig.Name));
                return;
            }
            var configRunner = GetSelectedConfigRunner();
            if (configRunner.IsStarting() || configRunner.IsStopping())
            {
                var message = string.Format(Resources.MainForm_listViewConfigs_ItemCheck_Configuration_is__0___Please_wait_,
                    configRunner.IsStarting() ? Resources.MainForm_listViewConfigs_ItemCheck_starting : Resources.MainForm_listViewConfigs_ItemCheck_stopping);

                DisplayWarning(Resources.MainForm_listViewConfigs_ItemCheck_Please_Wait, message);
                return;
            }

            if (!newIsEnabled)
            {
                var doChange = DisplayQuestion(Resources.MainForm_listViewConfigs_ItemCheck_Confirm_Stop,
                        string.Format(
                            Resources
                                .MainForm_listViewConfigs_ItemCheck_Are_you_sure_you_want_to_stop_configuration___0___,
                            configRunner.GetConfigName()));

                if (doChange == DialogResult.Yes)
                {
                    selectedConfig.IsEnabled = false;
                    configRunner.Stop();
                }
                return;
            }

            StartConfig(selectedConfig);
        }

        public void StartConfig(AutoQcConfig config)
        {
            config.IsEnabled = true;
            var configRunner = _configRunners[config.Name];
            Program.LogInfo(string.Format("Starting configuration {0}", config.Name));
            try
            {
                configRunner.Start();
            }
            catch (Exception e)
            {
                var title = string.Format(Resources.MainForm_StartConfigRunner_Error_Starting_Configuration___0__, configRunner.Config.Name);
                DisplayErrorWithException(title, e.Message, e);
                // ReSharper disable once LocalizableElement
                Program.LogError(string.Format("Error Starting Configuration \"{0}\"", configRunner.Config.Name), e);
            }
        }

        public void StopRunners()
        {
            foreach (var configRunner in _configRunners.Values)
            {
                configRunner.Stop();
            }
        }

        #endregion


        #region Logging

        public void SelectLog(int selected)
        {
            if (selected < 0 || selected > _loggers.Count)
                throw new IndexOutOfRangeException("No log at index: " + selected);
            if (SelectedLog >= 0)
                GetSelectedLogger().DisableUiLogging();
            SelectedLog = selected;
            GetSelectedLogger().LogToUi(_uiControl);
        }

        public void SelectLogOfSelectedConfig()
        {
            CheckConfigSelected();
            SelectedLog = GetLoggerIndex(GetSelectedConfig().Name);
        }

        public IAutoQcLogger GetSelectedLogger()
        {
            return _loggers[SelectedLog];
        }

        public bool LoggerIsDisplayed(string name)
        {
            if (SelectedLog < 0)
                return false;
            return GetSelectedLogger().GetConfigName().Equals(name);
        }

        public IAutoQcLogger GetLogger(string name)
        {
            return _loggers[GetLoggerIndex(name)];
        }

        private int GetLoggerIndex(string name)
        {
            for (int i = 0; i < _loggers.Count; i++)
            {
                if (_loggers[i].GetConfigName().Equals(name))
                    return i;
            }
            return -1;
        }

        private void RemoveLogger(string name)
        {
            int index = GetLoggerIndex(name);
            _loggers.RemoveAt(index);
            if (SelectedLog >= index)
            {
                SelectedLog--;
            }
        }

        public object[] GetLogList()
        {
            var logNames = new object[_loggers.Count];
            for (int i = 0; i < _loggers.Count; i++)
                logNames[i] = _loggers[i].GetConfigName();
            return logNames;
        }

        #endregion


        #region Import/Export

        public void Import(string filePath)
        {
            var readConfigs = new List<AutoQcConfig>();
            var validationErrors = new List<string>();
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    using (var reader = XmlReader.Create(stream))
                    {
                        while (reader.Name != "autoqc_config")
                        {
                            if (reader.Name == "userSettings" && !reader.IsStartElement()) // there are no configurations in the file
                                break;
                            reader.Read();
                        }
                        while (reader.IsStartElement())
                        {
                            if (reader.Name == "autoqc_config")
                            {
                                AutoQcConfig config = null;
                                try
                                {
                                    config = AutoQcConfig.ReadXml(reader);
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
                DisplayError(Resources.MainForm_btnImport_Click_Import_Configurations_Error, 
                    string.Format(Resources.MainForm_btnImport_Click_Could_not_import_configurations_from_file__0_, filePath));
                return;
            }

            if (readConfigs.Count == 0)
            {
                DisplayError(Resources.MainForm_btnImport_Click_Import_Configurations_Error,
                    string.Format(Resources.MainForm_btnImport_Click_No_configurations_were_found_in_file__0__, filePath));
                return;
            }

            var duplicateConfigs = new List<string>();
            var numAdded = 0;
            foreach (AutoQcConfig config in readConfigs)
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
            var message = new StringBuilder(Resources.MainForm_btnImport_Click_Number_of_configurations_imported__);
            message.Append(numAdded).Append(Environment.NewLine);
            if (duplicateConfigs.Count > 0)
            {
                message.Append(Resources.MainForm_btnImport_Click_The_following_configurations_already_exist_and_were_not_imported_)
                    .Append(Environment.NewLine);
                foreach (var name in duplicateConfigs)
                {
                    message.Append("\"").Append(name).Append("\"").Append(Environment.NewLine);
                }
            }
            if (validationErrors.Count > 0)
            {
                message.Append(Resources.MainForm_btnImport_Click_The_following_configurations_could_not_be_validated_and_were_not_imported_)
                    .Append(Environment.NewLine);
                foreach (var error in validationErrors)
                {
                    message.Append(error).Append(Environment.NewLine);
                }
            }
            DisplayInfo(Resources.MainForm_btnImport_Click_Import_Configurations, message.ToString());
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
            foreach (int index in indiciesToSave)
                savingConfigs.Add(_configList[index]);
            var tempSettings = new Settings();
            tempSettings.ConfigList = savingConfigs;
            tempSettings.Save();
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            config.SaveAs(filePath);
            Settings.Default.Save();
        }


        #endregion


        #region UI Control

        private void DisplayError(string title, string message)
        {
            if (!_runningUi)
                return;
            _uiControl.DisplayError(title, message);
        }

        private void DisplayErrorWithException(string title, string message, Exception ex)
        {
            if (!_runningUi)
                return;
            _uiControl.DisplayErrorWithException(title, message, ex);
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
                if (!Equals(configNames[i], _configList[i].Name)) return false;
            }

            return true;
        }

        public string ListConfigNames()
        {
            var names = "";
            foreach (var config in _configList)
            {
                names += config.Name + "  ";
            }

            return names;
        }



        #endregion
    }
}
