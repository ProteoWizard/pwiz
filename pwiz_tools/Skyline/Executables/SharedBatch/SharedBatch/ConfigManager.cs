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
        
        protected IMainUiControl _uiControl; // null if no UI displayed
        protected ImmutableList<Logger> _logList;

        private ConfigManagerState _baseState; // the current state

        protected readonly object _lock = new object(); // lock required for getting or setting the state
        protected readonly object _loggerLock = new object(); // lock required for any mutator or getter method on _logList or SelectedLog

        public ConfigManager()
        {
            _baseState = ConfigManagerState.Empty();
            _logList = ImmutableList<Logger>.Empty;
        }

        public ConfigManagerState State => GetState();
        
        public int SelectedLog { get; protected set; } // index of the selected log. index 0 corresponds to _logger, any index > 0 corresponds to oldLogs[index - 1]

       

        public static void SaveConfigList(ConfigManagerState state)
        {
            Settings.Default.SetConfigList(state.ConfigList.ToList());
            Settings.Default.Save();
        }



        // Static Methods

        #region Alert Dialogs

        public static void DisplayError(IMainUiControl uiControl, string message)
        {
            uiControl?.DisplayError(message);
        }

        public static void DisplayErrorWithException(IMainUiControl uiControl, string message, Exception ex)
        {
            uiControl?.DisplayErrorWithException(message, ex);
        }

        public static void DisplayWarning(IMainUiControl uiControl, string message)
        {
            uiControl?.DisplayWarning(message);
        }

        public static DialogResult DisplayQuestion(IMainUiControl uiControl, string message)
        {
            if (uiControl == null)
                return DialogResult.Yes;
            return uiControl.DisplayQuestion(message);
        }

        public static DialogResult DisplayLargeOkCancel(IMainUiControl uiControl, string message)
        {
            if (uiControl == null)
                return DialogResult.Yes;
            return uiControl.DisplayLargeOkCancel(message);
        }

        #endregion
        

        // Instance methods

        #region Manipulate State

        // Updates the config manager to a new valid state when the configuration list has changed
        // newState should contain a configuration list that is different from the current state in at least one way
        public void SetState(ConfigManagerState oldState, ConfigManagerState newState)
        {
            lock (_lock)
            {
                if (!Equals(oldState, GetState()))
                {
                    throw new ArgumentException(Resources
                        .ConfigManager_SetState_The_state_of_the_configuration_list_has_changed_since_this_operation_started__Please_try_again_);
                }
                newState.ValidateState();
                if (newState.ModelChanged)
                    SaveConfigList(newState);
                _baseState = newState.Copy();
            }
        }

        protected ConfigManagerState GetState()
        {
            lock (_lock)
            {
                return _baseState.Copy();
            }
        }

        public void Close()
        {
            SaveConfigList(GetState());
        }

        #endregion

        #region Logging

        public Logger GetSelectedLogger()
        {
            lock (_loggerLock)
            {
                if (SelectedLog == -1)
                    return null;
                return _logList[SelectedLog];
            }
        }

        public void SelectLog(int selected)
        {
            lock (_loggerLock)
            {
                if (selected < 0 || selected >= _logList.Count)
                    throw new IndexOutOfRangeException(Resources.ConfigManagerState_SelectLog_No_log_at_index__ + selected);
                SelectedLog = selected;
            }
        }

        public void ClearLogs()
        {
            lock (_loggerLock)
            {
                _logList = ImmutableList<Logger>.Empty;
            }
        }

        public void RemoveLogsByName(object[] removingLogs)
        {
            lock (_loggerLock)
            {
                SelectedLog = -1;
                int i = 0;
                while (i < _logList.Count)
                {
                    if (removingLogs.Contains(_logList[i].LogFileName))
                    {
                        _logList[i].Delete(); // closes and deletes log file
                        _logList = _logList.RemoveAt(i); // removes from list
                        continue;
                    }
                    i++;
                }
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

        public void InsertLog(int index, Logger logger)
        {
            lock (_loggerLock)
            {
                _logList = _logList.Insert(index, logger);
            }
        }

        public bool LoggerIsDisplayed(string name)
        {
            lock (_loggerLock)
            {
                if (SelectedLog < 0)
                    return false;
                return GetSelectedLogger().Name.Equals(name);
            }
        }

        public object[] GetLogNameList()
        {
            lock (_loggerLock)
            {
                var logNames = new object[_logList.Count];
                for (int i = 0; i < _logList.Count; i++)
                    logNames[i] = _logList[i].Name;
                return logNames;
            }
        }

        public void ArchiveFirstLog()
        {
            lock (_loggerLock)
            {
                var oldLogger = (_logList[0]).Archive();
                if (oldLogger != null)
                    _logList = _logList.Insert(1, oldLogger);
            }
        }

        #endregion

        #region UI Control

        protected void UpdateUiLogs()
        {
            if (_uiControl == null)
                return;
            _uiControl.UpdateUiLogFiles();
        }

        protected void UpdateIsRunning(bool canStart, bool canStop)
        {
            if (_uiControl == null)
                return;
            _uiControl.UpdateRunningButtons(canStart, canStop);
        }

        #endregion
        
        #region Tests

        public bool ConfigListEquals(List<IConfig> otherConfigs)
        {
            lock (_lock)
            {
                if (otherConfigs.Count != State.ConfigList.Count) return false;

                for (int i = 0; i < State.ConfigList.Count; i++)
                {
                    if (!Equals(otherConfigs[i], State.ConfigList[i])) return false;
                }
                return true;
            }
        }

        public string ListConfigNames()
        {
            lock (_lock)
            {
                var names = string.Empty;
                foreach (var config in State.ConfigList)
                {
                    names += config.GetName() + "  ";
                }
                return names;
            }
        }

        public int InvalidConfigCount()
        {
            var invalidCount = 0;
            foreach (var validation in State.ConfigValidation.Values)
                invalidCount += validation ? 0 : 1;
            return invalidCount;
        }

        #endregion

    }

    // Represents a state of the config manager
    public class ConfigManagerState
    {
        public ImmutableList<IConfig> ConfigList { get; private set; }
        public ImmutableDictionary<string, bool> ConfigValidation { get; private set; }
        public ImmutableDictionary<string, string> RootReplacement { get; private set; }
        public int Selected { get; private set; }
        public bool ModelChanged { get; private set; }

        public static ConfigManagerState Empty()
        {
            return new ConfigManagerState(ImmutableList<IConfig>.Empty, ImmutableDictionary<string, bool>.Empty, ImmutableDictionary<string, string>.Empty);
        }

        public ConfigManagerState(ImmutableList<IConfig> configList, ImmutableDictionary<string, bool> configValidation, ImmutableDictionary<string, string> rootReplacement, int selected = -1,  bool modelChanged = true)
        {
            ConfigList = configList;
            ConfigValidation = configValidation;
            RootReplacement = rootReplacement;
            Selected = selected;
            ModelChanged = modelChanged;
        }

        public void ValidateState()
        {
            foreach (var config in ConfigList)
            {
                if (ConfigList.Count != ConfigValidation.Count || !ConfigValidation.ContainsKey(config.GetName()))
                    throw new ArgumentException("Could not validate the new state of the configuration list. The operation did not succeed.");
            }
            if (Selected < -1 || Selected >= ConfigList.Count)
                throw new IndexOutOfRangeException(string.Format(
                    Resources.ConfigManager_SelectConfig_There_is_no_configuration_at_index___0_, Selected));
        }

        public ConfigManagerState Copy()
        {
            return new ConfigManagerState(ConfigList, ConfigValidation, RootReplacement, Selected, false);
        }

        public ConfigManagerState ModelHasChanged()
        {
            ModelChanged = true;
            return this;
        }

        public ConfigManagerState ModelUnchanged()
        {
            ModelChanged = false;
            return this;
        }

        // State methods

        #region Config List

        public ConfigManagerState LoadConfigList()
        {
            ConfigList = ImmutableList<IConfig>.Empty;
            ConfigValidation = ImmutableDictionary<string, bool>.Empty;
            Selected = -1;
            foreach (var config in Settings.Default.ConfigList.ToList())
                ProgramaticallyInsertConfig(ConfigList.Count, config);
            return this;
        }

        public bool HasConfigs()
        {
            return ConfigList.Count > 0;
        }

        public IConfig GetConfig(int index)
        {
            return ConfigList[index];
        }

        public int GetConfigIndex(string name)
        {
            for (int i = 0; i < this.ConfigList.Count; i++)
            {
                if (ConfigList[i].GetName().Equals(name))
                    return i;
            }
            return -1;
        }

        public ConfigManagerState MoveSelectedConfig(bool moveUp)
        {
            var movingConfig = ConfigList[Selected];
            var delta = moveUp ? -1 : 1;
            Selected += delta;
            ConfigList = ConfigList.Remove(movingConfig).Insert(Selected, movingConfig);
            ModelChanged = true;
            return this;
        }

        public ConfigManagerState ReorderConfigs(List<int> newIndexOrder)
        {
            var selectedName = Selected >= 0 ? ConfigList[Selected].GetName() : null;
            var oldConfigList = ConfigList;
            ConfigList = ImmutableList<IConfig>.Empty;
            foreach (var index in newIndexOrder)
                ConfigList = ConfigList.Add(oldConfigList[index]);
            if (selectedName != null)
                Selected = GetConfigIndex(selectedName);
            return this;
        }

        public List<ListViewItem> ConfigsAsListViewItems(ImmutableDictionary<string, IConfigRunner> configRunners,
            Graphics graphics)
        {
            var listViewConfigs = new List<ListViewItem>();
            foreach (var config in ConfigList)
            {
                var lvi = config.AsListViewItem(configRunners[config.GetName()], graphics);
                if (!ConfigValidation[config.GetName()])
                    lvi.ForeColor = Color.Red;
                if (HasSelectedConfig() && ConfigList[Selected].GetName().Equals(config.GetName()))
                {
                    lvi.BackColor = Color.LightSteelBlue;
                    foreach (ListViewItem.ListViewSubItem subItem in lvi.SubItems)
                        subItem.BackColor = Color.LightSteelBlue;
                }
                listViewConfigs.Add(lvi);
            }

            return listViewConfigs;
        }

        public object[] ConfigNamesAsObjectArray()
        {
            var names = new object[ConfigList.Count];
            for (int i = 0; i < ConfigList.Count; i++)
                names[i] = ConfigList[i].GetName();
            return names;
        }

        public IConfig GetLastModified() // creates config using most recently modified config
        {
            if (!HasConfigs())
                return null;
            var lastModified = ConfigList[0];
            foreach (var config in ConfigList)
            {
                if (config.GetModified() > lastModified.GetModified())
                    lastModified = config;
            }
            return lastModified;
        }

        public void AssertUniqueName(string newName, bool replacingSelected)
        {
            if (ConfigValidation.Keys.Contains(newName))
            {
                if (!replacingSelected || !ConfigList[Selected].GetName().Equals(newName))
                    throw new ArgumentException(
                        string.Format(Resources.ConfigManager_InsertConfiguration_Configuration___0___already_exists_,
                            newName) + Environment.NewLine +
                        Resources.ConfigManager_InsertConfiguration_Please_enter_a_unique_name_for_the_configuration_);
            }
        }

        public ConfigManagerState UserInsertConfig(int index, IConfig config)
        {
            AssertUniqueName(config.GetName(), false);
            ProgramLog.Info(string.Format(Resources.ConfigManager_InsertConfiguration_Adding_configuration___0___,
                config.GetName()));
            ConfigList = ConfigList.Insert(index, config);
            ConfigValidation = ConfigValidation.Add(config.GetName(), true);
            Selected = index;
            ModelChanged = true;
            return this;
        }

        public ConfigManagerState UserRemoveAt(int index)
        {
            RemoveAt(index);
            if (Selected == ConfigList.Count)
                Selected -= 1;
            ModelChanged = true;
            return this;
        }

        public ConfigManagerState ProgramaticallyInsertConfig(int index, IConfig config)
        {
            ConfigList = ConfigList.Insert(index, config);
            try
            {
                config.Validate();
                ConfigValidation = ConfigValidation.Add(config.GetName(), true);
            }
            catch (ArgumentException)
            {
                // Invalid configurations are loaded
                ConfigValidation = ConfigValidation.Add(config.GetName(), false);
            }
            return this;
        }

        public ConfigManagerState ProgramaticallyRemoveAt(int index)
        {
            return RemoveAt(index);
        }

        private ConfigManagerState RemoveAt(int index)
        {
            var config = ConfigList[index];
            ProgramLog.Info(string.Format(Resources.ConfigManager_RemoveSelected_Removing_configuration____0__,
                config.GetName()));
            if (!ConfigValidation.Keys.Contains(config.GetName()))
            {
                throw new ArgumentException(string.Format(
                    Resources.ConfigManager_RemoveConfig_Cannot_delete___0____configuration_does_not_exist_,
                    config.GetName()));
            }

            ConfigList = ConfigList.Remove(config);
            ConfigValidation = ConfigValidation.Remove(config.GetName());
            return this;
        }

        #endregion

        #region Config Selection

        public bool HasSelectedConfig()
        {
            return Selected >= 0;
        }

        public ConfigManagerState SelectIndex(int newIndex)
        {
            Selected = newIndex;
            return this;
        }

        public void AssertConfigSelected()
        {
            if (Selected < 0)
            {
                throw new IndexOutOfRangeException(Resources
                    .ConfigManager_CheckConfigSelected_There_is_no_configuration_selected_);
            }
        }

        public IConfig GetSelectedConfig()
        {
            AssertConfigSelected();
            return ConfigList[Selected];
        }

        #endregion

        #region Config Validation

        public bool IsSelectedConfigValid()
        {
            AssertConfigSelected();
            return ConfigValidation[ConfigList[Selected].GetName()];
        }

        public bool IsConfigValid(int index)
        {
            return ConfigValidation[ConfigList[index].GetName()];
        }

        public ConfigManagerState UpdateConfigValidation()
        {
            ConfigValidation = ImmutableDictionary<string, bool>.Empty;
            foreach (var config in ConfigList)
            {
                try
                {
                    config.Validate();
                    ConfigValidation = ConfigValidation.Add(config.GetName(), true);
                }
                catch (ArgumentException)
                {
                    ConfigValidation = ConfigValidation.Add(config.GetName(), false);
                }
            }
            return this;
        }

        public ConfigManagerState SetConfigInvalid(IConfig invalidConfig)
        {
            ConfigValidation = ConfigValidation.Remove(invalidConfig.GetName())
                .Add(invalidConfig.GetName(), false);
            return this;
        }

        #endregion

        #region Read / Write Configs

        public delegate DialogResult ShowDownloadedFileForm(string filePath, out string copiedDestination);

        public ConfigManagerState ImportFrom(Importer importer, string filePath, decimal currentXmlVersion,
             IMainUiControl uiControl, ShowDownloadedFileForm showDownloadedFileForm, out List<int> addedIndicies)
        {
            var copiedDestination = string.Empty;
            var copiedConfigFile = string.Empty;
            var forceReplaceRoot = string.Empty;
            addedIndicies = new List<int>();

            if (filePath.Contains(FileUtil.DOWNLOADS_FOLDER) && showDownloadedFileForm != null)
            {
                var dialogResult = showDownloadedFileForm(filePath, out copiedDestination);
                if (dialogResult != DialogResult.Yes)
                    return this;
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
                                var importingXmlVersion = Properties.ConfigList.ReadXmlVersion(reader);
                                if (importingXmlVersion > currentXmlVersion)
                                {
                                    throw new ArgumentException(string.Format(
                                        Resources
                                            .ConfigManager_ImportFrom_The_version_of_the_file_to_import_from__0__is_newer_than_the_version_of_the_program__1___Please_update_the_program_to_import_configurations_from_this_file_,
                                        importingXmlVersion, currentXmlVersion));
                                }

                                var oldFolder = reader.GetAttribute(Attr.saved_path_root);
                                var oldConfigFile = reader.GetAttribute(Attr.SavedConfigsFilePath);
                                oldFolder = oldFolder ?? reader.GetAttribute(Attr.SavedPathRoot);
                                if (!string.IsNullOrEmpty(oldConfigFile) && string.IsNullOrEmpty(oldFolder))
                                    oldFolder = Path.GetDirectoryName(oldConfigFile);

                                if (!string.IsNullOrEmpty(oldFolder))
                                {
                                    var newFolder = string.IsNullOrEmpty(copiedDestination)
                                        ? Path.GetDirectoryName(filePath)
                                        : Path.GetDirectoryName(copiedConfigFile);
                                    AddRootReplacement(oldFolder, newFolder, false, uiControl, out string oldRoot, out _, out _);
                                    if (!string.IsNullOrEmpty(copiedDestination))
                                        forceReplaceRoot = oldRoot;
                                }
                                else if (!string.IsNullOrEmpty(copiedConfigFile))
                                {
                                    ConfigManager.DisplayWarning(uiControl, string.Format(Resources.ConfigManager_ImportFrom_The_imported_configurations_are_from_an_old_file_format_and_could_not_be_copied_to__0_, copiedDestination));
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
                ConfigManager.DisplayError(uiControl,
                    string.Format(
                        Resources.ConfigManager_Import_An_error_occurred_while_importing_configurations_from__0__,
                        filePath) + Environment.NewLine +
                    e.Message);
                return this;
            }
            return HandleImportedConfigErrors(readConfigs, readXmlErrors, filePath, forceReplaceRoot, uiControl, out addedIndicies);
        }

        // handles imported config errors like duplicate configs
        protected ConfigManagerState HandleImportedConfigErrors(List<IConfig> readConfigs, List<string> readXmlErrors, string filePath, string forceReplaceRoot,
            IMainUiControl uiControl, out List<int> addedIndicies)
        {
            addedIndicies = new List<int>();
            if (readConfigs.Count == 0 && readXmlErrors.Count == 0)
            {
                // warn if no configs found
                ConfigManager.DisplayWarning(uiControl, string.Format(Resources.ConfigManager_Import_No_configurations_were_found_in__0__,
                    filePath));
                return this;
            }

            var duplicateConfigNames = new List<string>();
            foreach (IConfig config in readConfigs)
            {
                // Make sure that the configuration name is unique
                if (ConfigValidation.Keys.Contains(config.GetName()))
                    duplicateConfigNames.Add(config.GetName());
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
                if (DialogResult.Yes == ConfigManager.DisplayQuestion(uiControl, duplicateMessage.ToString()))
                {
                    message.Append(Resources.ConfigManager_ImportFrom_Overwriting_).Append(Environment.NewLine);
                    duplicateConfigNames.Clear();
                }
            }
            
            var numAddedConfigs = 0;
            foreach (IConfig config in readConfigs)
            {
                if (duplicateConfigNames.Contains(config.GetName())) continue;


                IConfig addingConfig;
                if (string.IsNullOrEmpty(forceReplaceRoot))
                    addingConfig = RunRootReplacement(config);
                else
                    addingConfig = ForceRootReplacement(config, forceReplaceRoot, RootReplacement[forceReplaceRoot], out _);
                numAddedConfigs++;
                var existingConfigIndex = GetConfigIndex(addingConfig.GetName());
                ModelChanged = true;
                if (existingConfigIndex >= 0)
                    ProgramaticallyRemoveAt(existingConfigIndex);
                addedIndicies.Add(existingConfigIndex >= 0 ? existingConfigIndex : ConfigList.Count);
                ProgramaticallyInsertConfig(existingConfigIndex >= 0 ? existingConfigIndex : ConfigList.Count, addingConfig);
            }

            message.Append(Resources.ConfigManager_Import_Number_of_configurations_imported_);
            message.Append(numAddedConfigs).Append(Environment.NewLine);

            if (readXmlErrors.Count > 0)
            {
                var errorMessage = new StringBuilder(Resources
                        .ConfigManager_Import_Number_of_configurations_with_errors_that_could_not_be_imported_)
                    .Append(Environment.NewLine);
                foreach (var errorLine in readXmlErrors)
                {
                    errorMessage.Append(errorLine).Append(Environment.NewLine);
                }
                message.Append(errorMessage);
                ConfigManager.DisplayError(uiControl, errorMessage.ToString());
            }
            ProgramLog.Info(message.ToString());
            return this;
        }

        public void ExportConfigs(string filePath, decimal xmlVersion, int[] indiciesToSave)
        {
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
                            ConfigList[index].WriteXml(writer);
                        writer.WriteEndElement();
                    }
                }
            }
        }

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

        #region Modify Configs

        private List<Tuple<int, IConfig>> GetReplacedSkylineSettings(SkylineSettings newSettings,
            List<string> runningConfigs)
        {
            var replacedConfigs = new List<Tuple<int, IConfig>>();
            for (int i = 0; i < ConfigList.Count; i++)
            {
                var config = ConfigList[i];
                if (runningConfigs.Contains(config.GetName()))
                    continue;
                var newConfig = config.ReplaceSkylineVersion(newSettings);
                replacedConfigs.Add(new Tuple<int, IConfig>(i, newConfig));
            }
            return replacedConfigs;
        }

        public ConfigManagerState AskToReplaceAllSkylineVersions(SkylineSettings skylineSettings, List<string> runningConfigs, IMainUiControl uiControl, out bool? replaced)
        {
            replaced = null;
            if (ConfigList.Count < 2)
                return this;
            try
            {
                skylineSettings.Validate();
            }
            catch (ArgumentException)
            {
                // Only ask to replace Skyline settings if new settings are valid
                return this;
            }
            if (DialogResult.Yes ==
                ConfigManager.DisplayQuestion(uiControl, Resources.ConfigManager_ReplaceAllSkylineVersions_Do_you_want_to_use_this_Skyline_version_for_all_configurations_))
            {
                return ReplaceAllSkylineVersions(skylineSettings, runningConfigs, uiControl, out replaced);
            }
            replaced = false;
            return this;
        }

        public ConfigManagerState ReplaceAllSkylineVersions(SkylineSettings skylineSettings, List<string> runningConfigs, IMainUiControl uiControl, out bool? replaced)
        {
            var replacedConfigs = GetReplacedSkylineSettings(skylineSettings, runningConfigs);
            foreach (var indexAndConfig in replacedConfigs)
            {
                if (!runningConfigs.Contains(indexAndConfig.Item2.GetName()))
                {
                    ProgramaticallyRemoveAt(indexAndConfig.Item1);
                    ProgramaticallyInsertConfig(indexAndConfig.Item1, indexAndConfig.Item2);
                }
            }
            if (runningConfigs.Count > 0)
            {
                ConfigManager.DisplayError(uiControl,
                    Resources.ConfigManagerState_ReplaceAllSkylineVersions_The_following_configurations_are_running_and_could_not_be_updated_
                    + Environment.NewLine +
                    TextUtil.LineSeparate(runningConfigs));
                replaced = false;
            }
            replaced = true;
            return this;
        }


        #endregion

        #region Root Replacement

        public ConfigManagerState AddRootReplacement(string oldPath, string newPath, bool askAboutRootReplacement,
            IMainUiControl uiControl, out string oldRoot, out bool askedAboutRootReplacement, out bool addedRootReplacement)
        {
            var oldPathFolders = oldPath.Split('\\');
            var newPathFolders = newPath.Split('\\');
            oldRoot = string.Empty;
            askedAboutRootReplacement = false;
            addedRootReplacement = false;

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

            if (oldRoot.Length > 0)
            {
                var replaceRoot = true;
                if (askAboutRootReplacement)
                {
                    replaceRoot =
                        ConfigManager.DisplayQuestion(uiControl, string.Format(
                            Resources.InvalidConfigSetupForm_GetValidPath_Would_you_like_to_replace__0__with__1___,
                            oldRoot, newRoot)) == DialogResult.Yes;
                    askedAboutRootReplacement = true;
                }

                if (replaceRoot)
                {
                    addedRootReplacement = true;
                    if (RootReplacement.ContainsKey(oldRoot))
                        RootReplacement = RootReplacement.Remove(oldRoot);
                    RootReplacement = RootReplacement.Add(oldRoot, newRoot);
                }
            }
            return this;
        }

        public ConfigManagerState GetRootReplacedConfigs(string oldRoot, string newRoot)
        {
            for (int i = 0; i < ConfigList.Count; i++)
            {
                var config = ConfigList[i];
                if (!ConfigValidation[config.GetName()])
                {
                    var pathsReplaced = config.TryPathReplace(oldRoot, newRoot, out IConfig replacedPathConfig);
                    if (pathsReplaced)
                    {
                        var index = GetConfigIndex(config.GetName());
                        ProgramaticallyRemoveAt(index);
                        ProgramaticallyInsertConfig(index, replacedPathConfig);
                    }
                }
            }
            return this;
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
        private IConfig ForceRootReplacement(IConfig config, string oldRoot, string newRoot, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                return config.ForcePathReplace(oldRoot, newRoot);
            }
            catch (ArgumentException e)
            {
                errorMessage = e.Message;
                return config;
            }
        }

        #endregion
        
        #region Override Equals

        protected bool Equals(ConfigManagerState other)
        {
            if (ConfigList.Count != other.ConfigList.Count)
                return false;
            for (int i = 0; i < ConfigList.Count; i++)
            {
                if (!Equals(ConfigList[i], other.ConfigList[i]))
                    return false;
            }
            if (ConfigValidation.Count != other.ConfigValidation.Count)
                return false;
            foreach (var config in ConfigValidation.Keys)
            {
                if (!other.ConfigValidation.ContainsKey(config) || !Equals(ConfigValidation[config], other.ConfigValidation[config]))
                    return false;
            }
            /*if (LogList.Count != other.LogList.Count)
                return false;
            for (int i = 0; i < LogList.Count; i++)
            {
                if (!Equals(LogList[i], other.LogList[i]))
                    return false;
            }*/
            if (RootReplacement.Count != other.RootReplacement.Count)
                return false;
            foreach (var root in RootReplacement.Keys)
            {
                if (!other.RootReplacement.ContainsKey(root) || !Equals(RootReplacement[root], other.RootReplacement[root]))
                    return false;
            }

            return Selected == other.Selected;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ConfigManagerState)obj);
        }

        public override int GetHashCode()
        {
            return ConfigList.GetHashCode() +
                   ConfigValidation.GetHashCode();
        }

        #endregion

    }
}

