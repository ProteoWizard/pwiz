﻿/*
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
using System.Windows.Forms;
using System.Xml;
using SharedBatch.Properties;

namespace SharedBatch
{
    public delegate IConfig Importer(XmlReader reader, decimal importingXmlVersion);

    public delegate string XmlUpdater(string oldXmlFile, decimal currentXmlVersion);

    public class ConfigManager
    {
        // Handles all modification to configs, the config list, configRunners, and log files
        // The UI should reflect the configs, runners, and log files from this class

        protected Importer importer; // a ReadXml method to import the configurations

        protected ImmutableList<IConfig> _configList { get; private set; } // the list of configurations. Every config must have a validation status in _configValidation

        protected ImmutableDictionary<string, bool> _configValidation { get; private set; } // dictionary mapping from config name to if that config is valid

        protected readonly List<Logger> _logList; // list of all loggers displayed in the dropDown list on the log tab

        protected bool _runningUi; // if the UI is displayed (false when testing)
        protected IMainUiControl _uiControl; // null if no UI displayed

        protected readonly object _lock = new object(); // lock required for any mutator or getter method on _configList, _configValidators, or SelectedConfig
        protected readonly object _loggerLock = new object(); // lock required for any mutator or getter method on _logList or SelectedLog

        public Dictionary<string, string> RootReplacement; // dictionary mapping from roots of invalid file paths to roots of valid file paths

        public ConfigManager()
        {
            SelectedConfig = -1;
            SelectedLog = 0;
            _logList = new List<Logger>();
            _configList = ImmutableList<IConfig>.Empty;
            _configValidation = ImmutableDictionary<string, bool>.Empty;
            RootReplacement = new Dictionary<string, string>();
        }

        public void Init()
        {
            AssertAllInitialized();
        }

        protected void AssertAllInitialized()
        {
            if (importer == null || _configList == null || _configValidation == null || 
                _logList == null || _lock == null || _loggerLock == null || RootReplacement == null)
                throw new NullReferenceException("Not all Config Manager variables have been initialized.");
        }

        public int SelectedConfig { get; private set; } // index of the selected configuration
        public int SelectedLog { get; protected set; } // index of the selected log. index 0 corresponds to _logger, any index > 0 corresponds to oldLogs[index - 1]
        
        protected List<IConfig> LoadConfigList(decimal currentXmlVersion)
        {
            ConfigList.Importer = importer;
            ConfigList.XmlVersion = currentXmlVersion;
            // Do not load saved configurations in test mode
            return _runningUi ? Settings.Default.ConfigList.ToList() : new List<IConfig>();
        }


        protected ConfigManagerState ProgramaticallyInsertConfig(IConfig config, int index, ConfigManagerState state)
        {
            if (state == null) state = new ConfigManagerState(this);
            var configList = state.configList.Insert(index, config);
            ImmutableDictionary<string, bool> configValidation;
            try
            {
                config.Validate();
                configValidation = state.configValidation.Add(config.GetName(), true);
            }
            catch (ArgumentException)
            {
                // Invalid configurations are loaded
                configValidation = state.configValidation.Add(config.GetName(), false);
            }
            var newState = new ConfigManagerState(state)
            {
                configList = configList,
                configValidation = configValidation
            };
            return newState;
        }

        public void Close()
        {
            SaveConfigList();
        }

        public void SaveConfigList()
        {
            lock (_lock)
            {
                Settings.Default.SetConfigList(_configList.ToList());
                Settings.Default.Save();
            }
        }


        #region Configs

        protected List<ListViewItem> ConfigsListViewItems(ImmutableDictionary<string, IConfigRunner> configRunners,
            Graphics graphics, ConfigManagerState state)
        {
            var listViewConfigs = new List<ListViewItem>();
            foreach (var config in state.configList)
            {
                var lvi = config.AsListViewItem(configRunners[config.GetName()], graphics);
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

        public bool HasConfigs()
        {
            return HasConfigs(new ConfigManagerState(this));
        }

        protected bool HasConfigs(ConfigManagerState state)
        {
            return state.configList.Count > 0;
        }

        public bool HasSelectedConfig()
        {
            return SelectedConfig >= 0;
        }

        protected bool HasSelectedConfig(ConfigManagerState state)
        {
            return state.selected >= 0;
        }

        public void SelectConfig(int newIndex)
        {
            lock (_lock)
            {
                new ConfigManagerState(this).ValidateState(newIndex);
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
                if (SelectedConfig >= 0)
                {
                    SelectedConfig = -1;
                    _uiControl?.UpdateUiConfigurations();
                }
            }
        }

        protected void AssertConfigSelected(ConfigManagerState state)
        {
            if (SelectedConfig < 0)
            {
                throw new IndexOutOfRangeException(Resources
                    .ConfigManager_CheckConfigSelected_There_is_no_configuration_selected_);
            }
        }

        public int GetConfigIndex(string name)
        {
            return GetConfigIndex(name, new ConfigManagerState(this));
        }

        protected int GetConfigIndex(string name, ConfigManagerState state)
        {
            for (int i = 0; i < state.configList.Count; i++)
            {
                if (state.configList[i].GetName().Equals(name))
                    return i;
            }
            return -1;
        }

        public IConfig GetSelectedConfig()
        {
            var state = new ConfigManagerState(this);
            AssertConfigSelected(state);
            return state.configList[SelectedConfig];
        }

        public IConfig GetConfig(int index)
        {
            return _configList[index];
        }

        public bool IsSelectedConfigValid()
        {
            var state = new ConfigManagerState(this);
            AssertConfigSelected(state);
            return state.configValidation[state.configList[state.selected].GetName()];
        }

        public bool IsConfigValid(int index)
        {
            return _configValidation[_configList[index].GetName()];
        }

        public void UpdateConfigValidation()
        {
            var state = new ConfigManagerState(this);
            var configValidationMutable = new Dictionary<string, bool>();
            foreach (var config in state.configList)
            {
                try
                {
                    config.Validate();
                    configValidationMutable.Add(config.GetName(), true);
                }
                catch (ArgumentException)
                {
                    configValidationMutable.Add(config.GetName(), false);
                }
            }

            state.configValidation = ImmutableDictionary<string, bool>.Empty.AddRange(configValidationMutable);
            SetState(state);
            _uiControl.UpdateUiConfigurations();
        }

        public void SetConfigInvalid(IConfig invalidConfig)
        {
            lock (_lock)
            {
                var state = new ConfigManagerState(this);
                var configValidationMutable = new Dictionary<string, bool>();
                foreach (var config in state.configList)
                {
                    if (invalidConfig.GetName().Equals(config.GetName()))
                        configValidationMutable.Add(config.GetName(), false);
                    else
                        configValidationMutable.Add(config.GetName(), state.configValidation[config.GetName()]);
                }

                state.configValidation = ImmutableDictionary<string, bool>.Empty.AddRange(configValidationMutable);
                SetState(state);
            }
            _uiControl.UpdateUiConfigurations();
        }

        public IConfig GetLastModified() // creates config using most recently modified config
        {
            var state = new ConfigManagerState(this);
            if (!HasConfigs(state))
                return null;
            var lastModified = state.configList[0];
            foreach (var config in state.configList)
            {
                if (config.GetModified() > lastModified.GetModified())
                    lastModified = config;
            }

            return lastModified;
        }

        public void AssertUniqueName(string newName, bool replacingSelected)
        {
            AssertUniqueName(newName, replacingSelected, new ConfigManagerState(this));
        }

        private void AssertUniqueName(string newName, bool replacingSelected, ConfigManagerState state)
        {
            if (state.configValidation.Keys.Contains(newName))
            {
                if (!replacingSelected || !_configList[SelectedConfig].GetName().Equals(newName))
                    throw new ArgumentException(
                        string.Format(Resources.ConfigManager_InsertConfiguration_Configuration___0___already_exists_,
                            newName) + Environment.NewLine +
                        Resources.ConfigManager_InsertConfiguration_Please_enter_a_unique_name_for_the_configuration_);
            }
        }

        protected ConfigManagerState UserInsertConfig(IConfig config, int index, ConfigManagerState state)
        {
            lock (_lock)
            {
                AssertUniqueName(config.GetName(), false, state);
                ProgramLog.Info(string.Format(Resources.ConfigManager_InsertConfiguration_Adding_configuration___0___,
                    config.GetName()));
                var configList = state.configList.Insert(index, config);
                var configValidation = state.configValidation.Add(config.GetName(), true);
                return new ConfigManagerState(state)
                {
                    configList = configList,
                    configValidation = configValidation,
                    selected = index,
                    editedConfigFromRedo = index
                };
            }
        }

        protected ConfigManagerState MoveSelectedConfig(ConfigManagerState state, bool moveUp)
        {
            var movingConfig = _configList[SelectedConfig];
            var delta = moveUp ? -1 : 1;
            state.configList = state.configList.Remove(movingConfig);
            state.configList = state.configList.Insert(SelectedConfig + delta, movingConfig);
            state.editedConfigFromUndo = state.selected;
            state.selected += delta;
            state.editedConfigFromRedo = state.selected;
            return state;
        }

        protected ConfigManagerState UserRemoveAt(int index, ConfigManagerState state)
        {
            lock (_lock)
            {
                state = RemoveAt(index, state);
                if (state.selected == state.configList.Count)
                    state.selected--;
                return state;
            }
        }

        protected ConfigManagerState ProgramaticallyRemoveAt(int index, ConfigManagerState state)
        {
            return RemoveAt(index, state);
        }

        private ConfigManagerState RemoveAt(int index, ConfigManagerState state)
        {
            var config = state.configList[index];
            ProgramLog.Info(string.Format(Resources.ConfigManager_RemoveSelected_Removing_configuration____0__,
                config.GetName()));
            if (!state.configValidation.Keys.Contains(config.GetName()))
            {
                throw new ArgumentException(string.Format(
                    Resources.ConfigManager_RemoveConfig_Cannot_delete___0____configuration_does_not_exist_,
                    config.GetName()));
            }

            var configList = state.configList.Remove(config);
            var configValidation = state.configValidation.Remove(config.GetName());
            return new ConfigManagerState(state)
            {
                configList = configList,
                configValidation = configValidation,
                editedConfigFromUndo = index
            };
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

        protected DialogResult DisplayLargeOkCancel(string message)
        {
            if (!_runningUi)
                return DialogResult.Yes;
            return _uiControl.DisplayLargeOkCancel(message);
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


        public delegate DialogResult ShowDownloadedFileForm(string filePath, out string copiedDestination);

        // gets the list of importing configs
        protected List<IConfig> ImportFrom(string filePath, decimal currentXmlVersion, ShowDownloadedFileForm showDownloadedFileForm)
        {
            var copiedDestination = string.Empty;
            var copiedConfigFile = string.Empty;
            var forceReplaceRoot = string.Empty;
            
            if (filePath.Contains(FileUtil.DOWNLOADS_FOLDER) && showDownloadedFileForm != null)
            {
                var dialogResult = showDownloadedFileForm(filePath, out copiedDestination);
                if (dialogResult != DialogResult.Yes)
                    return new List<IConfig>();
                copiedConfigFile = Path.Combine(copiedDestination, Path.GetFileName(filePath));
                var file = new FileInfo(filePath);
                file.CopyTo(copiedConfigFile, true);
                filePath = copiedConfigFile;
            }

            var readConfigs = new List<IConfig>();
            var readXmlErrors = new List<string>();

            // read configs from file
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    using (var reader = XmlReader.Create(stream))
                    {
                        while (reader.Read())
                        {
                            if (reader.Name.Equals("config_list") || reader.Name.Equals("ConfigList"))
                            {
                                var importingXmlVersion = ConfigList.ReadXmlVersion(reader);
                                if (importingXmlVersion > currentXmlVersion)
                                {
                                    throw new ArgumentException(string.Format(
                                        Resources
                                            .ConfigManager_ImportFrom_The_version_of_the_file_to_import_from__0__is_newer_than_the_version_of_the_program__1___Please_update_the_program_to_import_configurations_from_this_file_,
                                        importingXmlVersion, currentXmlVersion));
                                }

                                var oldFolder = reader.GetAttribute(Attr.saved_path_root);
                                var oldConfigFile = reader.GetAttribute(Attr.SavedConfigsFilePath);
                                oldFolder = oldFolder?? reader.GetAttribute(Attr.SavedPathRoot);
                                if (!string.IsNullOrEmpty(oldConfigFile) && string.IsNullOrEmpty(oldFolder))
                                    oldFolder = Path.GetDirectoryName(oldConfigFile);

                                if (!string.IsNullOrEmpty(oldFolder))
                                {
                                    var newFolder = string.IsNullOrEmpty(copiedDestination)
                                        ? Path.GetDirectoryName(filePath)
                                        : Path.GetDirectoryName(copiedConfigFile);
                                    AddRootReplacement(oldFolder, newFolder, false, out string oldRoot, out _);
                                    if (!string.IsNullOrEmpty(copiedDestination))
                                        forceReplaceRoot = oldRoot;
                                }
                                else if (!string.IsNullOrEmpty(copiedConfigFile))
                                {
                                    DisplayWarning(string.Format(Resources.ConfigManager_ImportFrom_The_imported_configurations_are_from_an_old_file_format_and_could_not_be_copied_to__0_, copiedDestination));
                                }

                                while (!reader.Name.EndsWith("_config"))
                                {
                                    if (reader.EOF || reader.Name == "userSettings" && !reader.IsStartElement())
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
                                            config = importer(reader, importingXmlVersion);
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
                                break; // We are done reading the config list
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // possible xml format error
                DisplayError(
                    string.Format(
                        Resources.ConfigManager_Import_An_error_occurred_while_importing_configurations_from__0__,
                        filePath) + Environment.NewLine +
                    e.Message);
                return new List<IConfig>();
            }

            // delete temporary file used to rewrite XML
            if (filePath.EndsWith(TextUtil.EXT_TMP))
                File.Delete(filePath);

            if (readConfigs.Count == 0 && readXmlErrors.Count == 0)
            {
                // warn if no configs found
                DisplayWarning(string.Format(Resources.ConfigManager_Import_No_configurations_were_found_in__0__,
                    filePath));
                return new List<IConfig>();
            }

            var duplicateConfigNames = new List<string>();
            lock (_lock)
            {
                foreach (IConfig config in readConfigs)
                {
                    // Make sure that the configuration name is unique
                    if (_configValidation.Keys.Contains(config.GetName()))
                        duplicateConfigNames.Add(config.GetName());
                }
            }

            var message = new StringBuilder();
            if (duplicateConfigNames.Count > 0)
            {
                var duplicateMessage =
                    new StringBuilder(Resources.ConfigManager_ImportFrom_The_following_configurations_already_exist_)
                        .Append(Environment.NewLine);
                foreach (var name in duplicateConfigNames)
                    duplicateMessage.Append("\"").Append(name).Append("\"").Append(Environment.NewLine);

                message.Append(duplicateMessage).Append(Environment.NewLine);
                duplicateMessage.Append(Resources
                    .ConfigManager_ImportFrom_Do_you_want_to_overwrite_these_configurations_);
                if (DialogResult.Yes == DisplayQuestion(duplicateMessage.ToString()))
                {
                    message.Append(Resources.ConfigManager_ImportFrom_Overwriting_).Append(Environment.NewLine);
                    duplicateConfigNames.Clear();
                }
            }
            
            var addedConfigs = new List<IConfig>();

            foreach (IConfig config in readConfigs)
            {
                if (duplicateConfigNames.Contains(config.GetName())) continue;


                IConfig addingConfig;
                if (string.IsNullOrEmpty(forceReplaceRoot))
                    addingConfig = RunRootReplacement(config);
                else
                    addingConfig = ForceRootReplacement(config, forceReplaceRoot, out _);
                addedConfigs.Add(addingConfig);
            }

            message.Append(Resources.ConfigManager_Import_Number_of_configurations_imported_);
            message.Append(addedConfigs.Count).Append(Environment.NewLine);

            if (readXmlErrors.Count > 0)
            {
                var errorMessage = new StringBuilder(Resources
                        .ConfigManager_Import_Number_of_configurations_with_errors_that_could_not_be_imported_)
                    .Append(Environment.NewLine);
                foreach (var error in readXmlErrors)
                {
                    errorMessage.Append(error).Append(Environment.NewLine);
                }

                message.Append(errorMessage);
                DisplayError(errorMessage.ToString());
            }

            ProgramLog.Info(message.ToString());
            return addedConfigs;
        }
        
        public object[] ConfigNamesAsObjectArray()
        {
            var state = new ConfigManagerState(this);
            var names = new object[_configList.Count];
            for (int i = 0; i < state.configList.Count; i++)
                names[i] = state.configList[i].GetName();
            return names;
        }

        public void ExportConfigs(string filePath, decimal xmlVersion, int[] indiciesToSave)
        {
            var state = new ConfigManagerState(this);
            var directory = string.Empty;
            // Exception if no configurations are selected to export
            if (indiciesToSave.Length == 0)
            {
                throw new ArgumentException(Resources.ConfigManager_ExportConfigs_There_is_no_configuration_selected_ +
                                            Environment.NewLine +
                                            Resources
                                                .ConfigManager_ExportConfigs_Please_select_a_configuration_to_share_);
            }
            try
            {
                directory = Path.GetDirectoryName(filePath);
            }
            catch (ArgumentException)
            {
                // pass
            }
            // Exception if file folder does not exist
            if (!Directory.Exists(directory))
                throw new ArgumentException(Resources.ConfigManager_ExportConfigs_Could_not_save_configurations_to_ +
                                            Environment.NewLine +
                                            filePath + Environment.NewLine +
                                            Resources
                                                .ConfigManager_ExportConfigs_Please_provide_a_path_to_a_file_inside_an_existing_folder_);

            using (var file = File.Create(filePath))
            {
                using (var streamWriter = new StreamWriter(file))
                {
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Indent = true;
                    settings.NewLineChars = Environment.NewLine;
                    using (XmlWriter writer = XmlWriter.Create(streamWriter, settings))
                    {
                        writer.WriteStartElement("config_list");
                        writer.WriteAttributeString(Attr.saved_path_root, directory);
                        writer.WriteAttribute(Attr.xml_version, xmlVersion);
                        foreach (int index in indiciesToSave)
                            state.configList[index].WriteXml(writer);
                        writer.WriteEndElement();
                    }
                }
            }
        }

        // TODO (Ali): fix autoQC SavedFilePath
        public enum Attr
        {
            saved_path_root,
            xml_version,

            // deprecated
            version,
            SavedConfigsFilePath,
            SavedPathRoot
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

        public int InvalidConfigCount()
        {
            var invalidCount = 0;
            foreach (var validation in _configValidation.Values)
                invalidCount += validation ? 0 : 1;
            return invalidCount;
        }

        #endregion


        #region Logging

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

        protected List<Tuple<int, IConfig>> GetReplacedSkylineSettings(SkylineSettings newSettings,
            List<string> runningConfigs)
        {
            lock (_lock)
            {
                var replacedConfigs = new List<Tuple<int, IConfig>>();
                for (int i = 0; i < _configList.Count; i++)
                {
                    var config = _configList[i];
                    if (runningConfigs.Contains(config.GetName()))
                        continue;
                    var newConfig = config.ReplaceSkylineVersion(newSettings);
                    replacedConfigs.Add(new Tuple<int, IConfig>(i, newConfig));
                }
                return replacedConfigs;
            }
        }

        #endregion

        #region Root Replacement

        public bool AddRootReplacement(string oldPath, string newPath, bool askAboutRootReplacement, 
            out string oldRoot, out bool askedAboutRootReplacement)
        {
            var oldPathFolders = oldPath.Split('\\');
            var newPathFolders = newPath.Split('\\');
            oldRoot = string.Empty;
            askedAboutRootReplacement = false;

            var matchingEndFolders = 1;
            while (matchingEndFolders <= Math.Min(oldPathFolders.Length, newPathFolders.Length))
            {
                // Check how many end folders match
                if (!oldPathFolders[oldPathFolders.Length - matchingEndFolders]
                    .Equals(newPathFolders[newPathFolders.Length - matchingEndFolders]))
                    break;
                matchingEndFolders++;
            }

            matchingEndFolders--;
            oldRoot = string.Join("\\", oldPathFolders.Take(oldPathFolders.Length - matchingEndFolders).ToArray());
            var newRoot = string.Join("\\", newPathFolders.Take(newPathFolders.Length - matchingEndFolders).ToArray());

            var replaceRoot = false;
            if (oldRoot.Length > 0)
            {
                replaceRoot = true;
                if (askAboutRootReplacement)
                {
                    replaceRoot =
                        DisplayQuestion(string.Format(
                            Resources.InvalidConfigSetupForm_GetValidPath_Would_you_like_to_replace__0__with__1___,
                            oldRoot, newRoot)) == DialogResult.Yes;
                    askedAboutRootReplacement = true;
                }
                if (replaceRoot)
                {
                    if (RootReplacement.ContainsKey(oldRoot))
                        RootReplacement[oldRoot] = newRoot;
                    else
                        RootReplacement.Add(oldRoot, newRoot);
                }
            }
            return replaceRoot;
        }

        protected List<IConfig> GetRootReplacedConfigs(string oldRoot, ConfigManagerState state)
        {
            var replacingConfigs = new List<IConfig>();
            var newRoot = RootReplacement[oldRoot];
            lock (_lock)
            {
                for (int i = 0; i < state.configList.Count; i++)
                {
                    var config = state.configList[i];
                    if (!state.configValidation[config.GetName()])
                    {
                        var pathsReplaced = config.TryPathReplace(oldRoot, newRoot, out IConfig replacedPathConfig);
                        if (pathsReplaced)
                        {
                            replacingConfigs.Add(replacedPathConfig);
                        }
                    }
                }
            }
            return replacingConfigs;
        }

        private IConfig RunRootReplacement(IConfig config)
        {
            foreach (var oldRoot in RootReplacement.Keys)
            {
                var success = config.TryPathReplace(oldRoot, RootReplacement[oldRoot],
                    out IConfig pathReplacedConfig);
                if (success) return pathReplacedConfig;
            }
            return config;
        }

        // replaces all roots and creates the folders of the new paths. Throws exception if path replacement fails
        private IConfig ForceRootReplacement(IConfig config, string oldRoot, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                return config.ForcePathReplace(oldRoot, RootReplacement[oldRoot]);
            }
            catch (ArgumentException e)
            {
                errorMessage = e.Message;
                return config;
            }
        }

        #endregion

        // Updates the config manager to a new valid state when the configuration list has changed
        // newState should contain a configuration list that is different from the current state in at least one way
        protected void SetState(ConfigManagerState newState)
        {
            lock (_lock)
            {
                newState.ValidateState(newState.selected);
                _configList = newState.configList;
                _configValidation = newState.configValidation;
                SelectedConfig = newState.selected;
                SaveConfigList();
            }
        }

        // Represents a state of the config manager
        protected class ConfigManagerState
        {
            public ImmutableList<IConfig> configList;
            public ImmutableDictionary<string, bool> configValidation;
            public int selected;
            public int editedConfigFromUndo; // -1 or the index of a configuration to be selected if an operation is undone
            public int editedConfigFromRedo; // -1 or the index of a configuration to be selected if an operation is redone

            public ConfigManagerState()
            {
                editedConfigFromUndo = -1;
                editedConfigFromRedo = -1;
            }

            public ConfigManagerState(ConfigManager configManager)
            {
                configList = configManager._configList;
                configValidation = configManager._configValidation;
                selected = configManager.SelectedConfig;
                editedConfigFromUndo = -1;
                editedConfigFromRedo = -1;
            }

            public ConfigManagerState(ConfigManagerState state)
            {
                configList = state.configList;
                configValidation = state.configValidation;
                selected = state.selected;
                editedConfigFromUndo = state.editedConfigFromUndo;
                editedConfigFromRedo = state.editedConfigFromRedo;
            }

            public void ValidateState(int selectedConfig)
            {
                foreach (var config in configList)
                {
                    if (configList.Count != configValidation.Count || !configValidation.ContainsKey(config.GetName()))
                        throw new ArgumentException("Could not validate the new state of the configuration list. The operation did not succeed.");
                }
                if (selectedConfig < -1 || selectedConfig >= configList.Count)
                    throw new IndexOutOfRangeException(string.Format(
                        Resources.ConfigManager_SelectConfig_There_is_no_configuration_at_index___0_, selectedConfig));
            }

        }
    }
}

