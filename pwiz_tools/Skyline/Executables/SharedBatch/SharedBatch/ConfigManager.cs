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

        protected Importer importer; // a ReadXml method to import the configurations

        protected readonly List<IConfig> _configList; // the list of configurations. Every config must have a validation status in _configValidation
        protected readonly Dictionary<string, bool> _configValidation; // dictionary mapping from config name to if that config is valid

        protected List<Logger> _logList; // list of all loggers displayed in the dropDown list on the log tab

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
            _configList = new List<IConfig>();
            _configValidation = new Dictionary<string, bool>();
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
        
        protected List<IConfig> LoadConfigList()
        {
            ConfigList.Importer = importer;
            // Do not load saved configurations in test mode
            return _runningUi ? Settings.Default.ConfigList.ToList() : new List<IConfig>();
        }

        protected void ProgramaticallyInsertConfig(IConfig config, int index)
        {
            _configList.Insert(index, config);
            try
            {
                config.Validate();
                _configValidation.Add(config.GetName(), true);
            }
            catch (ArgumentException)
            {
                // Invalid configurations are loaded
                _configValidation.Add(config.GetName(), false);
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

        protected List<ListViewItem> ConfigsListViewItems(Dictionary<string, IConfigRunner> configRunners, Graphics graphics)
        {
            lock (_lock)
            {
                var listViewConfigs = new List<ListViewItem>();
                foreach (var config in _configList)
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

        protected void AssertConfigSelected()
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

        public IConfig GetConfig(int index)
        {
            lock (_lock)
            {
                return _configList[index];
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

        public void UpdateConfigValidation()
        {
            lock (_lock)
            {
                _configValidation.Clear();
                foreach (var config in _configList)
                {
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
            _uiControl.UpdateUiConfigurations();
        }

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

        public void AssertUniqueName(string newName, bool replacingSelected)
        {
            if (_configValidation.Keys.Contains(newName))
            {
                if (!replacingSelected || !_configList[SelectedConfig].GetName().Equals(newName))
                    throw new ArgumentException(string.Format(Resources.ConfigManager_InsertConfiguration_Configuration___0___already_exists_, newName) + Environment.NewLine +
                                            Resources.ConfigManager_InsertConfiguration_Please_enter_a_unique_name_for_the_configuration_);
            }
        }

        protected void UserInsertConfig(IConfig config, int index)
        {
            lock (_lock)
            {
                AssertUniqueName(config.GetName(), false);
                ProgramLog.Info(string.Format(Resources.ConfigManager_InsertConfiguration_Adding_configuration___0___, config.GetName()));
                _configList.Insert(index, config);
                _configValidation.Add(config.GetName(), true);
                SelectedConfig = index;
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

        protected void UserRemoveAt(int index)
        {
            lock (_lock)
            {
                RemoveAt(index);
                if (SelectedConfig == _configList.Count)
                    SelectedConfig--;
            }
        }

        protected void ProgramaticallyRemoveAt(int index)
        {
            RemoveAt(index);
        }

        private void RemoveAt(int index)
        {
            var config = _configList[index];
            ProgramLog.Info(string.Format(Resources.ConfigManager_RemoveSelected_Removing_configuration____0__, config.GetName()));
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

        protected List<IConfig> ImportFrom(string filePath, ShowDownloadedFileForm showDownloadedFileForm)
        {
            var copiedDestination = string.Empty;
            var copiedConfigFile = string.Empty;
            // TODO (Ali) uncomment this when data and templates can be downloaded
            /*
            if (filePath.Contains(FileUtil.DOWNLOADS_FOLDER))
            {
                var dialogResult = showDownloadedFileForm(filePath, out copiedDestination);
                if (dialogResult != DialogResult.Yes)
                    return new List<IConfig>();
                copiedConfigFile = Path.Combine(copiedDestination, Path.GetFileName(filePath));
                var file = new FileInfo(filePath);
                if (!File.Exists(copiedConfigFile))
                    file.CopyTo(copiedConfigFile, false);
            }*/

            var readConfigs = new List<IConfig>();
            var addedConfigs = new List<IConfig>();
            var readXmlErrors = new List<string>();
            // read configs from file
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    using (var reader = XmlReader.Create(stream))
                    {
                        while (!reader.Name.Equals("ConfigList"))
                            reader.Read();
                        var oldConfigFile = reader.GetAttribute(Attr.SavedConfigsFilePath);
                        var oldFolder = reader.GetAttribute(Attr.SavedPathRoot);
                        if (!string.IsNullOrEmpty(oldConfigFile) && string.IsNullOrEmpty(oldFolder))
                            oldFolder = Path.GetDirectoryName(oldConfigFile);
                        if (!string.IsNullOrEmpty(oldFolder))
                        {
                            var newFolder = string.IsNullOrEmpty(copiedDestination)
                                ? Path.GetDirectoryName(filePath)
                                : Path.GetDirectoryName(copiedConfigFile);
                            AddRootReplacement(oldFolder, newFolder, false, out _, out _);
                        }

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
                // possible xml format error
                DisplayError(string.Format(Resources.ConfigManager_Import_An_error_occurred_while_importing_configurations_from__0__, filePath) + Environment.NewLine +
                             e.Message);
                return addedConfigs;
            }
            if (readConfigs.Count == 0 && readXmlErrors.Count == 0)
            {
                // warn if no configs found
                DisplayWarning(string.Format(Resources.ConfigManager_Import_No_configurations_were_found_in__0__, filePath));
                return addedConfigs;
            }

            var duplicateConfigNames = new List<string>();
            foreach (IConfig config in readConfigs)
            {
                // Make sure that the configuration name is unique
                if (_configValidation.Keys.Contains(config.GetName()))
                    duplicateConfigNames.Add(config.GetName());
            }

            var message = new StringBuilder();
            if (duplicateConfigNames.Count > 0)
            {
                var duplicateMessage = new StringBuilder(Resources.ConfigManager_ImportFrom_The_following_configurations_already_exist_)
                    .Append(Environment.NewLine);
                foreach (var name in duplicateConfigNames)
                    duplicateMessage.Append("\"").Append(name).Append("\"").Append(Environment.NewLine);

                message.Append(duplicateMessage).Append(Environment.NewLine);
                duplicateMessage.Append(Resources.ConfigManager_ImportFrom_Do_you_want_to_overwrite_these_configurations_);
                if (DialogResult.Yes == DisplayQuestion(duplicateMessage.ToString()))
                {
                    message.Append(Resources.ConfigManager_ImportFrom_Overwriting_).Append(Environment.NewLine); 
                    duplicateConfigNames.Clear();
                }
            }
            
            var numAdded = 0;

            foreach (IConfig config in readConfigs)
            {
                if (duplicateConfigNames.Contains(config.GetName())) continue;
                var addingConfig = RunRootReplacement(config);
                addedConfigs.Add(addingConfig);
                numAdded++;
            }
            message.Append(Resources.ConfigManager_Import_Number_of_configurations_imported_);
            message.Append(numAdded).Append(Environment.NewLine);

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
            ProgramLog.Info(message.ToString());
            return addedConfigs;
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
            var directory = string.Empty;
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
                // pass
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
                        writer.WriteAttributeString(Attr.SavedPathRoot, directory);
                        foreach (int index in indiciesToSave)
                            _configList[index].WriteXml(writer);
                        writer.WriteEndElement();
                    }
                }
            }
        }

        enum Attr
        {
            SavedConfigsFilePath, // deprecated since SkylineBatch release 20.2.0.475
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

        protected List<Tuple<int, IConfig>> GetReplacedSkylineSettings(SkylineSettings newSettings, List<string> runningConfigs)
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
                    replaceRoot = DisplayQuestion(string.Format(Resources.InvalidConfigSetupForm_GetValidPath_Would_you_like_to_replace__0__with__1___, oldRoot, newRoot)) == DialogResult.Yes;
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

        protected List<IConfig> GetRootReplacedConfigs(string oldRoot)
        {
            var replacingConfigs = new List<IConfig>();
            var newRoot = RootReplacement[oldRoot];
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
                            replacingConfigs.Add(replacedPathConfig);
                        }
                    }
                }
            }
            return replacingConfigs;
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

        #endregion
    }
}

