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
using System.Windows.Forms;
using System.Xml;
using SharedBatch.Properties;

namespace SharedBatch
{
    public delegate IConfig Importer(XmlReader reader);

    public class ConfigManager
    {
        // Handles all modification to configs, the config list, configRunners, and log files
        // The UI should reflect the configs, runners, and log files from this class

        protected readonly List<IConfig> _configList; // the list of configurations. Every config must have a runner in configRunners
        protected readonly Dictionary<string, bool> _configValidation; // dictionary mapping from config name to if that config is valid

        protected Logger _currentLogger; // the current logger - always logs to SkylineBatch.log
        protected List<Logger> _logList; // list of archived loggers, from most recent to least recent

        protected bool _runningUi; // if the UI is displayed (false when testing)
        protected IMainUiControl _uiControl; // null if no UI displayed

        protected readonly object _lock = new object(); // lock required for any mutator or getter method on _configList, _configRunners, or SelectedConfig
        protected readonly object _loggerLock = new object(); // lock required for any mutator or getter method on _logger, _oldLogs or SelectedLog

        public Dictionary<string, string> RootReplacement;
        

        public ConfigManager()
        {
            SelectedConfig = -1;
            SelectedLog = 0;
            _logList = new List<Logger>();
            _configList = new List<IConfig>();
            //_currentLogger = logger;
            _configValidation = new Dictionary<string, bool>();
            RootReplacement = new Dictionary<string, string>();
        }

        public void Init()
        {
            AssertAllInitialized();
        }

        protected void AssertAllInitialized()
        {
            if (_configList == null || _configValidation == null || 
                _logList == null || _lock == null || _loggerLock == null || RootReplacement == null)
                throw new NullReferenceException("Not all Config Manager variables have been initialized.");
        }

        public int SelectedConfig { get; private set; } // index of the selected configuration
        public int SelectedLog { get; protected set; } // index of the selected log. index 0 corresponds to _logger, any index > 0 corresponds to oldLogs[index - 1]
        
        protected void LoadConfigList()
        {
            foreach (var config in Settings.Default.ConfigList)
            {
                _configList.Add(config);
                try
                {
                    config.Validate();
                    _configValidation.Add(config.GetName(), true);
                }
                catch (ArgumentException e)
                {
                    //Program.LogInfo(e.Message);
                    _configValidation.Add(config.GetName(), false);
                }
            }
        }

        public void Close()
        {
            SaveConfigList();
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


        #region Configs

        protected List<ListViewItem> ConfigsListViewItems(Dictionary<string, IConfigRunner> configRunners)
        {
            lock (_lock)
            {
                var listViewConfigs = new List<ListViewItem>();
                foreach (var config in _configList)
                {
                    var lvi = config.AsListViewItem(configRunners[config.GetName()]);
                    if (!_configValidation[config.GetName()])
                        lvi.ForeColor = Color.Red;
                    if (HasSelectedConfig() && _configList[SelectedConfig].GetName().Equals(config.GetName()))
                    {
                        lvi.BackColor = Color.LightSteelBlue;
                        foreach (ListViewItem.ListViewSubItem subItem in lvi.SubItems)
                            subItem.BackColor = Color.LightSteelBlue;
                    }
                        
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
                    throw new IndexOutOfRangeException(string.Format(Resources.ConfigManager_SelectConfig_There_is_no_configuration_at_index___0_, newIndex));
                if (SelectedConfig != newIndex)
                {
                    SelectedConfig = newIndex;
                    _uiControl?.UpdateUiConfigurations();
                }
            }
        }

        public void DeselectConfig()
        {
            lock (_lock)
            {
                SelectedConfig = -1;
                _uiControl?.UpdateUiConfigurations();
            }
        }

        private void AssertConfigSelected()
        {
            if (SelectedConfig < 0)
            {
                throw new IndexOutOfRangeException(Resources.ConfigManager_CheckConfigSelected_There_is_no_configuration_selected_);
            }
        }

        public int GetConfigIndex(string name)
        {
            lock (_lock)
            {
                for (int i = 0; i < _configList.Count; i++)
                {
                    if (_configList[i].GetName().Equals(name))
                        return i;
                }
                return -1;
            }
        }

        public IConfig GetSelectedConfig()
        {
            lock (_lock)
            {
                AssertConfigSelected();
                return _configList[SelectedConfig];
            }
        }

        public bool IsSelectedConfigValid()
        {
            lock (_lock)
            {
                AssertConfigSelected();
                return _configValidation[_configList[SelectedConfig].GetName()];
            }
        }

        public bool IsConfigValid(int index)
        {
            return _configValidation[_configList[index].GetName()];
        }

        /*public bool CheckConfigAtIndex(int index, out string errorMessage)
        {
            lock (_lock)
            {
                errorMessage = "";
                var config = _configList[index];
                var runner = _configRunners[config.GetName()];
                if (!_configValidation[config.GetName()])
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
        }*/

        public IConfig GetLastModified() // creates config using most recently modified config
        {
            lock (_lock)
            {
                if (!HasConfigs())
                    return null;
                var lastModified = _configList[0];
                foreach (var config in _configList)
                {
                    if (config.GetModified() > lastModified.GetModified())
                        lastModified = config;
                }
                return lastModified;
            }
        }

        protected void InsertConfiguration(IConfig config, int index)
        {
            lock (_lock)
            {
                if (_configValidation.Keys.Contains(config.GetName()))
                {
                    throw new ArgumentException(string.Format(Resources.ConfigManager_InsertConfiguration_Configuration___0___already_exists_, config.GetName()) + Environment.NewLine +
                                                Resources.ConfigManager_InsertConfiguration_Please_enter_a_unique_name_for_the_configuration_);
                }
                //Program.LogInfo(string.Format(Resources.ConfigManager_InsertConfiguration_Adding_configuration___0___, config.GetName()));
                _configList.Insert(index, config);
                
                try
                {
                    config.Validate();
                    _configValidation.Add(config.GetName(), true);
                }
                catch (ArgumentException)
                {
                    _configValidation.Add(config.GetName(), false);
                }
            }
        }

        protected void ReplaceSelectedConfig(IConfig newConfig)
        {
            lock (_lock)
            {
                AssertConfigSelected();
                var oldConfig = _configList[SelectedConfig];
                if (!string.Equals(oldConfig.GetName(), newConfig.GetName()))
                {
                    if (_configValidation.Keys.Contains(newConfig.GetName()))
                    {
                        throw new ArgumentException(string.Format(Resources.ConfigManager_InsertConfiguration_Configuration___0___already_exists_, newConfig.GetName()) + Environment.NewLine +
                                                    Resources.ConfigManager_InsertConfiguration_Please_enter_a_unique_name_for_the_configuration_);
                    }
                }
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
                AssertConfigSelected();
                var config = GetSelectedConfig();
                var doDelete = DisplayQuestion( 
                    string.Format(Resources.ConfigManager_RemoveSelected_Are_you_sure_you_want_to_delete___0___,
                        config.GetName()));
                if (doDelete != DialogResult.Yes)
                    return;
                //Program.LogInfo(string.Format(Resources.ConfigManager_RemoveSelected_Removing_configuration____0__, config.Name));
                RemoveConfig(config);
                if (SelectedConfig == _configList.Count)
                    SelectedConfig--;
            }
        }

        private void RemoveConfig(IConfig config)
        {
            if (!_configValidation.Keys.Contains(config.GetName()))
            {
                throw new ArgumentException(string.Format(Resources.ConfigManager_RemoveConfig_Cannot_delete___0____configuration_does_not_exist_, config.GetName()));
            }
            _configList.Remove(config);
            _configValidation.Remove(config.GetName());
        }

        #endregion

        #region UI Control

        protected void DisplayError(string message)
        {
            if (!_runningUi)
                return;
            _uiControl.DisplayError(message);
        }

        protected void DisplayErrorWithException(string message, Exception ex)
        {
            if (!_runningUi)
                return;
            _uiControl.DisplayErrorWithException(message, ex);
        }

        protected void DisplayWarning(string message)
        {
            if (!_runningUi)
                return;
            _uiControl.DisplayWarning(message);
        }

        protected DialogResult DisplayQuestion(string message)
        {
            if (!_runningUi)
                return DialogResult.Yes;
            return _uiControl.DisplayQuestion(message);
        }

        protected DialogResult DisplayLargeQuestion(string message)
        {
            if (!_runningUi)
                return DialogResult.Yes;
            return _uiControl.DisplayLargeQuestion(message);
        }

        protected void UpdateUiLogs()
        {
            if (!_runningUi)
                return;
            _uiControl.UpdateUiLogFiles();
        }

        protected void UpdateIsRunning(bool canStart, bool canStop)
        {
            if (!_runningUi)
                return;
            _uiControl.UpdateRunningButtons(canStart, canStop);
        }
        
        #endregion

        #region Import/Export

        protected List<IConfig> ImportFrom(string filePath, Importer importer)
        {
            var readConfigs = new List<IConfig>();
            var addedConfigs = new List<IConfig>();
            var readXmlErrors = new List<string>();
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    using (var reader = XmlReader.Create(stream))
                    {
                        while (!reader.Name.EndsWith("_config"))
                        {
                            if (reader.Name == "userSettings" && !reader.IsStartElement())
                                break; // there are no configurations in the file
                            reader.Read();
                        }
                        while (reader.IsStartElement())
                        {
                            if (reader.Name.EndsWith("_config"))
                            {
                                IConfig config = null;
                                try
                                {
                                    config = importer(reader);
                                }
                                catch (Exception ex)
                                {
                                    readXmlErrors.Add(ex.Message);
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
            catch (Exception e)
            {
                DisplayError(string.Format(Resources.ConfigManager_Import_An_error_occurred_while_importing_configurations_from__0__, filePath) + Environment.NewLine +
                             e.Message);
                return addedConfigs;
            }
            if (readConfigs.Count == 0 && readXmlErrors.Count == 0)
            {
                DisplayWarning(string.Format(Resources.ConfigManager_Import_No_configurations_were_found_in__0__, filePath));
                return addedConfigs;
            }

            var duplicateConfigs = new List<string>();
            var numAdded = 0;
            foreach (IConfig config in readConfigs)
            {
                // Make sure that the configuration name is unique
                if (_configValidation.Keys.Contains(config.GetName()))
                {
                    duplicateConfigs.Add(config.GetName());
                    continue;
                }

                var addingConfig = RunRootReplacement(config);
                addedConfigs.Add(addingConfig);
                InsertConfiguration(addingConfig, _configList.Count);
                numAdded++;
            }
            var message = new StringBuilder(Resources.ConfigManager_Import_Number_of_configurations_imported_);
            message.Append(numAdded).Append(Environment.NewLine);
            if (duplicateConfigs.Count > 0)
            {
                var duplicateMessage = new StringBuilder(Resources.ConfigManager_Import_These_configurations_already_exist_and_could_not_be_imported_)
                    .Append(Environment.NewLine);
                foreach (var name in duplicateConfigs)
                {
                    duplicateMessage.Append("\"").Append(name).Append("\"").Append(Environment.NewLine);
                }

                duplicateMessage.Append(Resources.ConfigManager_Import_Please_remove_the_configurations_you_would_like_to_import_);
                message.Append(duplicateMessage);
                DisplayError(duplicateMessage.ToString());
            }
            if (readXmlErrors.Count > 0)
            {
                var errorMessage = new StringBuilder(Resources.ConfigManager_Import_Number_of_configurations_with_errors_that_could_not_be_imported_)
                    .Append(Environment.NewLine);
                foreach (var error in readXmlErrors)
                {
                    errorMessage.Append(error).Append(Environment.NewLine);
                }
                message.Append(errorMessage);
                DisplayError(errorMessage.ToString());
            }

            return addedConfigs;
            //Program.LogInfo(message.ToString());
        }
        
        public object[] ConfigNamesAsObjectArray()
        {
            var names = new object[_configList.Count];
            for (int i = 0; i < _configList.Count; i++)
                names[i] = _configList[i].GetName();
            return names;
        }

        public void ExportConfigs(string filePath, int[] indiciesToSave)
        {
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            // Exception if no configurations are selected to export
            if (indiciesToSave.Length == 0)
            {
                throw new ArgumentException(Resources.ConfigManager_ExportConfigs_There_is_no_configuration_selected_ + Environment.NewLine +
                                           Resources.ConfigManager_ExportConfigs_Please_select_a_configuration_to_share_);
            }
            try
            {
                directory = Path.GetDirectoryName(filePath);
            }
            catch (ArgumentException)
            {
            }
            // Exception if file folder does not exist
            if (!Directory.Exists(directory))
                throw new ArgumentException(Resources.ConfigManager_ExportConfigs_Could_not_save_configurations_to_ + Environment.NewLine +
                                            filePath + Environment.NewLine +
                                            Resources.ConfigManager_ExportConfigs_Please_provide_a_path_to_a_file_inside_an_existing_folder_);

            using (var file = File.Create(filePath))
            {
                using (var streamWriter = new StreamWriter(file))
                {
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Indent = true;
                    settings.NewLineChars = Environment.NewLine;
                    using (XmlWriter writer = XmlWriter.Create(streamWriter, settings))
                    {
                        writer.WriteStartElement("ConfigList");
                        foreach (int index in indiciesToSave)
                            _configList[index].WriteXml(writer);
                        writer.WriteEndElement();
                    }
                }
            }
        }


        #endregion
        
        #region Tests
        
        public bool ConfigListEquals(List<IConfig> otherConfigs)
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
                var names = string.Empty;
                foreach (var config in _configList)
                {
                    names += config.GetName() + "  ";
                }
                return names;
            }
        }

        #endregion

        public Logger GetSelectedLogger()
        {
            return _logList[SelectedLog];
        }

        public bool LoggerIsDisplayed(string name)
        {
            if (SelectedLog < 0)
                return false;
            return GetSelectedLogger().Name.Equals(name);
        }

        public object[] GetLogList()
        {
            var logNames = new object[_logList.Count];
            for (int i = 0; i < _logList.Count; i++)
                logNames[i] = _logList[i].Name;
            return logNames;
        }

        public void AddRootReplacement(string oldRoot, string newRoot)
        {
            RootReplacement.Add(oldRoot, newRoot);
            lock (_lock)
            {
                for (int i = 0; i < _configList.Count; i++)
                {
                    var config = _configList[i];
                    if (!_configValidation[config.GetName()])
                    {
                        var pathsReplaced = config.TryPathReplace(oldRoot, newRoot, out IConfig replacedPathConfig);
                        if (pathsReplaced)
                        {
                            RemoveConfig(config);
                            InsertConfiguration(replacedPathConfig, i);
                        }
                    }
                }
            }
        }

        public IConfig RunRootReplacement(IConfig config)
        {
            foreach (var oldRoot in RootReplacement.Keys)
            {
                var success = config.TryPathReplace(oldRoot, RootReplacement[oldRoot],
                    out IConfig pathReplacedConfig);
                if (success) return pathReplacedConfig;
            }
            return config;
        }
    }
}

