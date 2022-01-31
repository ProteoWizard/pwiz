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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;
using Resources = SkylineBatch.Properties.Resources;

namespace SkylineBatch
{
    public class SkylineBatchConfigManager : ConfigManager
    {
        // Extension of ConfigManager, handles more specific SkylineBatch functionality
        // The UI should reflect the state of this class (_state)
        
        private List<SkylineBatchConfigManagerState> _stateList;
        private int _currentIndex;

        private SkylineBatchConfigManagerState _state;

        private RunBatchOptions? _startingRunOption; // the run option of the currently starting run. null if no run is being started.
        private ServerFilesManager _runServerFiles; // the verified set of server files that will be used when the run starts

        // Shared variables with ConfigManager:
        //
        //    IMainUiControl _uiControl; <- null if no UI displayed
        //    object _lock = new object(); <- lock required for getting and setting the state
        //

        public SkylineBatchConfigManager(Logger logger, IMainUiControl mainForm = null)
        {
            _uiControl = mainForm;
            LoadLogs(logger);
            _state = SkylineBatchConfigManagerState.Empty(logger);
            _stateList = new List<SkylineBatchConfigManagerState>();
            _currentIndex = -1;
            _startingRunOption = null;
        }

        public new SkylineBatchConfigManagerState State => GetState();

        // Instance Methods

        #region Open / Close SkylineBatchConfigManager

        public void LoadConfigList()
        {
            var initialState = State;
            var state = initialState.Copy().LoadConfigList(_uiControl);
            SetState(initialState, state);
        }

        public new void Close()
        {
            lock (_lock)
            {
                base.Close();
                ArchiveFirstLog();
                _logList[0].Close();
                State.CancelRunners();
            }
        }

        #endregion

        #region Select Config

        public void SelectConfig(int newIndex)
        {
            var initialState = State;
            var newState = State.SelectIndex(newIndex);
            SetState(initialState, newState, true);
        }

        public void DeselectConfig()
        {
            var initialState = State;
            var newState = State.SelectIndex(-1);
            SetState(initialState, newState, true);
        }

        #endregion

        #region Information About Configs
        
        public List<ListViewItem> ConfigsListViewItems(Graphics graphics)
        {
            var state = State;
            return state.BaseState.ConfigsAsListViewItems(state.ConfigRunners, graphics);

        }

        #endregion

        #region Operation Wrappers


        public void UserAddConfig(SkylineBatchConfig config)
        {
            var initialState = State;
            var state = initialState.Copy().UserAddConfig(config, _uiControl);
            SetState(initialState, state);
        }

        public bool UserRemoveSelected()
        {
            var initialState = State;
            var state = initialState.Copy().UserRemoveSelected(_uiControl);
            return SetState(initialState, state);
        }

        public void UserReplaceSelected(SkylineBatchConfig config)
        {
            var initialState = GetState();
            var state = initialState.Copy().UserReplaceSelected(config, _uiControl);
            SetState(initialState, state);
        }

        public bool MoveSelectedConfig(bool moveUp)
        {
            var initialState = GetState();
            var state = initialState.Copy().MoveSelectedConfig(moveUp);
            return SetState(initialState, state, true);
        }

        public bool CheckConfigAtIndex(int index, out string errorMessage)
        {
            var initialState = GetState();
            var state = initialState.Copy().CheckConfigAtIndex(index, _uiControl, out errorMessage);
            if (errorMessage != null)
                return false;
            return SetState(initialState, state);
        }

        public bool Import(string filePath, ConfigManagerState.ShowDownloadedFileForm showDownloadedFileForm)
        {
            var initialState = State;
            var state = initialState.Copy().Import(filePath, _uiControl, showDownloadedFileForm);
            return SetState(initialState, state);
        }

        #endregion

        #region Run Configs

        public bool CanRun(RunBatchOptions runOption, bool checkOverwrite = true)
        {
            if (_startingRunOption != null)
                throw new Exception("Another run is already being started. Should not get here.");
            // Check that no configs are currently running
            _startingRunOption = runOption;
            var state = State;
            var configsRunning = state.ConfigsBusy();

            if (configsRunning.Count > 0)
            {
                DisplayError(_uiControl,
                    Resources.ConfigManager_RunAll_Cannot_run_while_the_following_configurations_are_running_ +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, configsRunning) + Environment.NewLine +
                    Resources.ConfigManager_RunAll_Please_wait_until_the_current_run_is_finished_);
                _startingRunOption = null;
                return false;
            }


            // check that configs exist
            if (state.BaseState.ConfigList.Count == 0)
            {
                DisplayError(_uiControl, Resources.SkylineBatchConfigManager_StartBatchRun_There_are_no_configurations_to_run_ +
                                         Environment.NewLine +
                                         Resources
                                             .SkylineBatchConfigManager_StartBatchRun_Please_add_configurations_before_running_);
                _startingRunOption = null;
                return false;
            }

            var enabledConfigs = state.GetEnabledConfigs();
            // Check there are enabled (checked) configs to run
            if (enabledConfigs.Count == 0)
            {
                DisplayError(_uiControl,
                    Resources.SkylineBatchConfigManager_StartBatchRun_There_are_no_enabled_configurations_to_run_ +
                    Environment.NewLine +
                    Resources
                        .SkylineBatchConfigManager_StartBatchRun_Please_check_the_checkbox_next_to_one_or_more_configurations_);
                _startingRunOption = null;
                return false;
            }

            // Check that configs run in correct order
            var dependencies = state.GetDependencies();
            var enabledConfigNames = state.GetEnabledConfigNames();
            foreach (var dependency in dependencies)
            {
                foreach (var configToRun in enabledConfigNames)
                {
                    if (dependency.Value.Contains(configToRun))
                    {
                        var dependentIndex = enabledConfigNames.IndexOf(dependency.Key);
                        if (dependentIndex < 0 || dependentIndex > enabledConfigNames.IndexOf(configToRun))
                        {
                            if ((runOption == RunBatchOptions.FROM_TEMPLATE_COPY ||
                                 runOption == RunBatchOptions.ALL) &&
                                !File.Exists(state.ConfigFromName(configToRun).MainSettings.Template.FilePath))
                            {
                                DisplayError(_uiControl,
                                    string.Format(
                                        Resources
                                            .SkylineBatchConfigManager_StartBatchRun_Configuration__0__must_be_run_before__1__to_generate_its_template_file_,
                                        dependency.Key, configToRun) +
                                    Environment.NewLine +
                                    string.Format(
                                        Resources
                                            .SkylineBatchConfigManager_StartBatchRun_Please_reorder_the_configurations_so__0__runs_first_,
                                        dependency.Key));
                                _startingRunOption = null;
                                return false;
                            }
                        }
                    }
                }
            }

            // Check if files will be overwritten by run
            var overwriteInfo = "";
            if (runOption == RunBatchOptions.ALL || runOption == RunBatchOptions.FROM_TEMPLATE_COPY)
                overwriteInfo = Resources.SkylineBatchConfigManager_StartBatchRun_files;
            if (runOption == RunBatchOptions.FROM_REFINE)
                overwriteInfo = Resources.SkylineBatchConfigManager_StartBatchRun_refined_files;
            if (runOption == RunBatchOptions.FROM_REPORT_EXPORT)
                overwriteInfo = Resources.SkylineBatchConfigManager_StartBatchRun_exported_reports;
            if (runOption == RunBatchOptions.R_SCRIPTS)
                overwriteInfo = Resources
                    .SkylineBatchConfigManager_StartBatchRun_R_script_outputs_in_the_following_analysis_folders;
            var overwriteMessage = new StringBuilder();
            overwriteMessage.Append(string.Format(
                Resources
                    .SkylineBatchConfigManager_StartBatchRun_Running_the_enabled_configurations_from___0___would_overwrite_the__1__,
                StepToReadableString(runOption), overwriteInfo)).AppendLine().AppendLine();
            var showOverwriteMessage = false;

            foreach (var config in enabledConfigs)
            {
                var tab = "      ";
                var configurationHeader =
                    tab + string.Format(Resources.SkylineBatchConfigManager_StartBatchRun_Configuration___0___,
                        config.Name) + Environment.NewLine;
                var willOverwrite =
                    config.RunWillOverwrite(runOption, configurationHeader, out StringBuilder message);
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
                if (DisplayLargeOkCancel(_uiControl, overwriteMessage.ToString()) != DialogResult.OK)
                {
                    _startingRunOption = null;
                    return false;
                }
            }
            return true;
        }

        public void StartCheckingServers(LongWaitDlg longWaitDlg, Callback callback)
        {
            if (_startingRunOption == null)
                throw new Exception("No run was started with CanRun method.");
            if (_uiControl != null)
                _uiControl.UpdateRunningButtons(false, false);
            _runServerFiles = new ServerFilesManager();
            var enabledConfigs = State.GetEnabledConfigs();
            var downloadingConfigs = new List<SkylineBatchConfig>();
            // Try connecting to all necessary servers
            foreach (var config in enabledConfigs)
            {
                if (config.WillDownloadData)
                {
                    downloadingConfigs.Add(config);
                    config.AddDownloadingFiles(_runServerFiles);
                }
            }

            var longWaitOperation = new LongWaitOperation(longWaitDlg);


            longWaitOperation.Start(downloadingConfigs.Count > 0, (OnProgress, cancelToken) =>
            {
                _runServerFiles.Connect(OnProgress, cancelToken);
            }, (success) =>
            {
                FinishCheckingServers(success, downloadingConfigs, callback);
            });
        }

        public void FinishCheckingServers(bool finishedOperation, List<SkylineBatchConfig> downloadingConfigs, Callback callback)
        {
            if (!finishedOperation)
            {
                CannotRun(callback);
                return;
            }

            if (_runServerFiles.HadConnectionExceptions)
            {
                var initialState = State;
                var connectionForm = new ConnectionErrorForm(initialState.Copy(), downloadingConfigs, _runServerFiles, _uiControl);
                _uiControl.DisplayForm(connectionForm);
                if (connectionForm.DialogResult != DialogResult.OK)
                {
                    CannotRun(callback);
                    return;
                }
                
                foreach (var config in connectionForm.ReplacingConfigs)
                {
                    int i = 0;
                    while (i < downloadingConfigs.Count && !downloadingConfigs[i].Name.Equals(config.Name)) i++;
                    downloadingConfigs.RemoveAt(i);
                    downloadingConfigs.Insert(i, config);
                }
                //connectionForm.State.ModelUnchanged();
                if (!SetState(initialState, connectionForm.State, true))
                    return;
            }

            var configsWillDownload = new List<string>();
            foreach (var config in downloadingConfigs)
            {
                if ((config.MainSettings.Server != null && _runServerFiles.GetDataFilesToDownload(config.MainSettings.Server, config.MainSettings.DataFolderPath).Count > 0) ||
                    config.MainSettings.Template.PanoramaFile != null && _runServerFiles.GetPanoramaFilesToDownload(config.MainSettings.Template.PanoramaFile).Count > 0)
                    configsWillDownload.Add(config.GetName());
            }

            var driveSpaceNeeded = _runServerFiles.GetSize();
            if (_startingRunOption == RunBatchOptions.ALL ||
                _startingRunOption == RunBatchOptions.DOWNLOAD_DATA)
            {
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
                    DisplayError(_uiControl, errorMessage + Environment.NewLine + Resources.SkylineBatchConfigManager_StartBatchRun_Please_free_up_some_space_to_download_the_data_);
                    CannotRun(callback);
                    return;
                }
            }
            else if (_startingRunOption == RunBatchOptions.FROM_TEMPLATE_COPY && driveSpaceNeeded.Keys.Count > 0)
            {
                // data files need to be downloaded but user did not start from a download step
                var errorMessage = Resources.SkylineBatchConfigManager_StartBatchRun_The_data_for_the_following_configurations_has_not_fully_downloaded_ + Environment.NewLine + Environment.NewLine;
                errorMessage += TextUtil.LineSeparate(configsWillDownload) + Environment.NewLine +
                                Environment.NewLine;
                errorMessage +=
                    Resources.SkylineBatchConfigManager_StartBatchRun_Please_download_the_data_for_these_configurations_before_running_them_from_a_later_step_;
                DisplayError(_uiControl, errorMessage);
                CannotRun(callback);
                return;
            }
            callback(true);
        }

        public void CannotRun(Callback callback)
        {
            _startingRunOption = null;
            callback(false);
            if (_uiControl != null)
                _uiControl.UpdateRunningButtons(true, false);
        }

        public void StartBatchRun()
        {
            if (_startingRunOption == null)
                throw new Exception("No run initiated.");
            var serverFiles = _runServerFiles;
            var enabledConfigNames = State.GetEnabledConfigNames();
            // Starts config runners waiting
            foreach (var config in enabledConfigNames)
                ((ConfigRunner)State.ConfigRunners[config]).ChangeStatus(RunnerStatus.Waiting);
            
            ArchiveFirstLog();
            var runOption = _startingRunOption ?? RunBatchOptions.ALL;
            _startingRunOption = null;
            new Thread(() => _ = RunAsync(runOption, serverFiles)).Start();
        }
        
        public async Task RunAsync(RunBatchOptions runOption, ServerFilesManager serverFiles)
        {
            UpdateUiLogs();
            UpdateIsRunning(false, true);
            var state = State;
            string nextConfig = state.GetNextWaitingConfig();
            while (!string.IsNullOrEmpty(nextConfig))
            {
                var startingConfigRunner = (ConfigRunner)state.ConfigRunners[nextConfig];

                try
                {
                    await startingConfigRunner.Run(runOption, serverFiles);
                }
                catch (Exception e)
                {
                    startingConfigRunner.ChangeStatus(RunnerStatus.Error);
                    DisplayErrorWithException(_uiControl, string.Format(Resources.SkylineBatchConfigManager_RunAsync_An_unexpected_error_occurred_while_running__0_, nextConfig), e);
                }
                nextConfig = state.GetNextWaitingConfig();
            }

            UpdateIsRunning(true, false);
        }

        private static string StepToReadableString(RunBatchOptions runOption)
        {
            switch (runOption)
            {
                case RunBatchOptions.ALL:
                    return Resources.MainForm_UpdateRunBatchSteps_All;
                case RunBatchOptions.DOWNLOAD_DATA:
                    return Resources.MainForm_UpdateRunBatchSteps_Download_files_only;
                case RunBatchOptions.FROM_TEMPLATE_COPY:
                    return Resources.MainForm_UpdateRunBatchSteps_Start_from_template_copy;
                case RunBatchOptions.FROM_REFINE:
                    return Resources.MainForm_UpdateRunBatchSteps_Start_from_refinement;
                case RunBatchOptions.FROM_REPORT_EXPORT:
                    return Resources.MainForm_UpdateRunBatchSteps_Start_from_report_export;
                case RunBatchOptions.R_SCRIPTS:
                    return Resources.MainForm_UpdateRunBatchSteps_R_scripts_only;
                default:
                    throw new Exception("The run option was not recognized");
            }
        }

        #endregion

        #region Logs

        public void LoadLogs(Logger mainLogger)
        {
            lock (_loggerLock)
            {
                var logDirectory = Path.GetDirectoryName(mainLogger.LogFile);
                var files = logDirectory != null ? new DirectoryInfo(logDirectory).GetFiles() : new FileInfo[0];
                InsertLog(0, mainLogger);
                foreach (var file in files)
                {
                    if (file.Name.EndsWith(TextUtil.EXT_LOG) && !file.Name.Equals(mainLogger.LogFileName))
                    {
                        InsertLog(1, new Logger(file.FullName, file.Name, true));
                    }
                }
            }
        }

        public bool HasOldLogs()
        {
            return _logList.Count > 1;
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

        public void DeleteLogs(object[] deletingLogs)
        {
            lock (_loggerLock)
            {
                //var initialState = State;
                var oldSelectedLog = _logList[SelectedLog].Name;
                RemoveLogsByName(deletingLogs);
                SelectedLog = 0;
                for (int i = 0; i < _logList.Count; i++)
                {
                    if (_logList[i].Name.Equals(oldSelectedLog))
                    {
                        SelectedLog = i;
                        break;
                    }
                }
            }
            UpdateUiLogs();
        }

        #endregion

        #region Update State

        public new SkylineBatchConfigManagerState GetState()
        {
            lock (_lock)
            {
                return _state.Copy();
            }
        }

        public bool SetState(SkylineBatchConfigManagerState expectedState, SkylineBatchConfigManagerState newState, bool overrideRunStart = false)
        {
            string errorMessage = null;
            lock (_lock)
            {
                try
                {
                    if (!Equals(expectedState, GetState()))
                    {
                        throw new ArgumentException(SharedBatch.Properties.Resources
                            .ConfigManager_SetState_The_state_of_the_configuration_list_has_changed_since_this_operation_started__Please_try_again_);
                    }

                    if (Equals(expectedState, newState) && _currentIndex > 0)
                        newState = newState.Copy(); // sets model unchanged
                    if (_startingRunOption != null && !overrideRunStart)
                    {
                        throw new ArgumentException(Resources.SkylineBatchConfigManager_SetState_The_state_of_the_configuration_list_cannot_be_changed_while_a_run_is_being_started__Please_try_again_);
                    }
                    newState.ValidateState();
                    SetState(expectedState.BaseState, newState.BaseState); // sets the base state in ConfigManager
                }
                catch (Exception e)
                {
                    errorMessage = e.Message;
                }
                if (errorMessage == null)
                {
                    _state = newState.Copy();
                    if (newState.BaseState.ModelChanged || _currentIndex < 0)
                    {
                        if (_currentIndex != _stateList.Count - 1)
                        {
                            _stateList.RemoveRange(_currentIndex + 1, _stateList.Count - _currentIndex - 1);
                        }
                        _stateList.Add(newState);
                        _currentIndex++;
                    }
                    else
                    {
                        _stateList[_currentIndex] = _state;
                    }
                }
            }
            if (errorMessage != null)
            {
                DisplayError(_uiControl, errorMessage);
                return false;
            }
            _uiControl?.UpdateUiConfigurations();
            return true;
        }

        public bool CanUndo()
        {
            return _currentIndex > 0;
        }
        
        public void Undo()
        {
            if (CanUndo())
            {
                var expectedState = _stateList[_currentIndex];
                _currentIndex--;
                if (!SetState(expectedState, _stateList[_currentIndex].ModelUnchanged()))
                    return;
                if (_stateList[_currentIndex].BaseState.Selected > -1)
                {
                    SelectConfig(_stateList[_currentIndex].BaseState.Selected);
                }
                else
                {
                    DeselectConfig();
                }
            }
        }

        public bool CanRedo()
        {
            return _currentIndex < _stateList.Count - 1;
        }

        public void Redo()
        {
            if (CanRedo())
            {
                var expectedState = _stateList[_currentIndex];
                _currentIndex++;
                if (!SetState(expectedState, _stateList[_currentIndex].ModelUnchanged()))
                    return;
                if (_stateList[_currentIndex].BaseState.Selected > -1)
                {
                    SelectConfig(_stateList[_currentIndex].BaseState.Selected);
                }
                else
                {
                    DeselectConfig();
                }
            }
        }

        #endregion

        #region Tests

        public SkylineBatchConfig GetConfig(int index)
        {
            return (SkylineBatchConfig) State.BaseState.GetConfig(index);
        }

        public List<string> ConfigsBusy() => State.ConfigsBusy();


        // For testing only - reset remote file sources
        public void ClearRemoteFileSources()
        {
            var currentState = State;
            var newState = currentState;
            if (State.BaseState.ConfigList.Count > 0)
                throw new Exception("Cannot clear remote file source list while configurations exist.");
            foreach (var name in currentState.FileSources.Keys)
            {
                newState = newState.RemoveRemoteFileSource(name);
            }
            SetState(currentState, newState);
        }

        #endregion

    }

    public class SkylineBatchConfigManagerState
    {
        public readonly ConfigManagerState BaseState; // the ConfigManagerState that holds the list of configurations etc
        public ImmutableDictionary<string, string> Templates { get; private set; } // dictionary mapping from config name to it's refined output file (not included if no refinement occurs)
        public ImmutableDictionary<string, IConfigRunner> ConfigRunners { get; private set; } // dictionary mapping from config name to that config's runner

        public ImmutableDictionary<string, RemoteFileSource> FileSources { get; private set; } // dictionary mapping from config name to that config's runner

        private readonly Logger _mainLogger; // the ConfigManagerState that holds the list of configurations etc

        public static SkylineBatchConfigManagerState Empty(Logger mainLogger)
        {
            return new SkylineBatchConfigManagerState(ConfigManagerState.Empty(), ImmutableDictionary<string, string>.Empty, 
                ImmutableDictionary<string, IConfigRunner>.Empty, ImmutableDictionary<string, RemoteFileSource>.Empty, mainLogger);
        }

        public SkylineBatchConfigManagerState(ConfigManagerState baseState, ImmutableDictionary<string, string> templates, ImmutableDictionary<string, IConfigRunner> configRunners, 
            ImmutableDictionary<string, RemoteFileSource> fileSources, Logger mainLogger)
        {
            BaseState = baseState;
            Templates = templates;
            ConfigRunners = configRunners;
            FileSources = fileSources;
            _mainLogger = mainLogger;
        }


        public void ValidateState()
        {
            var validated = ConfigRunners.Count == BaseState.ConfigList.Count;
            foreach (var config in BaseState.ConfigList)
            {
                if (!ConfigRunners.ContainsKey(config.GetName()))
                {
                    validated = false;
                    break;
                }
            }
            foreach (var configName in Templates.Keys)
            {
                if (!ConfigRunners.ContainsKey(configName) || !Templates[configName]
                    .Equals(((SkylineBatchConfig)ConfigRunners[configName].GetConfig()).RefineSettings
                        .OutputFilePath))
                {
                    validated = false;
                    break;
                }
            }
            if (!validated)
                throw new ArgumentException("Could not validate the new state of the configuration list. The operation did not succeed.");
        }

        public SkylineBatchConfigManagerState Copy()
        {
            return new SkylineBatchConfigManagerState(BaseState.Copy(), Templates, ConfigRunners, FileSources, _mainLogger);
        }

        // State methods
        
        #region Load

        public SkylineBatchConfigManagerState LoadConfigList(IMainUiControl uiControl)
        {
            BaseState.LoadConfigList();
            UpdateFromBaseState(uiControl);
            DisableInvalidConfigs();
            return this;
        }

        #endregion

        #region Add / Remove Configs

        public SkylineBatchConfigManagerState UserAddConfig(IConfig iconfig, IMainUiControl uiControl)
        {
            return UserInsertConfig(BaseState.ConfigList.Count, iconfig, uiControl);
        }

        private SkylineBatchConfigManagerState UserInsertConfig(int index, IConfig iconfig, IMainUiControl uiControl)
        {
            var newConfig = ((SkylineBatchConfig)iconfig).UpdateRemoteFileSet(FileSources,
                out ImmutableDictionary<string, RemoteFileSource> newRemoteFileSources);
            BaseState.UserInsertConfig(index, newConfig);
            AddConfig(newConfig, uiControl);
            FileSources = newRemoteFileSources;
            return this;
        }

        public SkylineBatchConfigManagerState ProgramaticallyInsertConfig(int index, IConfig iconfig, IMainUiControl uiControl)
        {
            var newConfig = ((SkylineBatchConfig)iconfig).UpdateRemoteFileSet(FileSources,
                out ImmutableDictionary<string, RemoteFileSource> newRemoteFileSources);
            BaseState.ProgramaticallyInsertConfig(index, newConfig);
            AddConfig(iconfig, uiControl);
            FileSources = newRemoteFileSources;
            return this;
        }

        private SkylineBatchConfigManagerState AddConfig(IConfig iconfig, IMainUiControl uiControl)
        {
            var config = (SkylineBatchConfig)iconfig;
            if (ConfigRunners.ContainsKey(config.GetName()))
                return this; // this will fail state validation
            var runner = new ConfigRunner(config, _mainLogger, uiControl);
            ConfigRunners = ConfigRunners.Add(config.GetName(), runner);
            if (config.RefineSettings.WillRefine())
                Templates = Templates.Add(config.Name, config.RefineSettings.OutputFilePath);
            return this;
        }

        public SkylineBatchConfigManagerState ProgramaticallyRemoveAt(int index)
        {
            RemoveConfig(BaseState.ConfigList[index]);
            BaseState.ProgramaticallyRemoveAt(index);
            return this;
        }

        private SkylineBatchConfigManagerState RemoveConfig(IConfig iconfig)
        {
            var config = (SkylineBatchConfig)iconfig;
            if (!ConfigRunners.ContainsKey(config.Name))
                throw new Exception("Config runner does not exist.");
            ConfigRunners = ConfigRunners.Remove(config.Name);
            if (Templates.ContainsKey(config.Name))
                Templates = Templates.Remove(config.Name);
            return this;
        }

        public SkylineBatchConfigManagerState UserReplaceSelected(IConfig iConfig, IMainUiControl uiControl)
        {
            var index = BaseState.Selected;
            var config = GetSelectedConfig();
            var newConfig = (SkylineBatchConfig)iConfig;
            var oldDependencies = GetDependencies();

            var nameChanged = !config.Name.Equals(newConfig.Name);
            var refineFileChanged =
                !config.RefineSettings.OutputFilePath.Equals(newConfig.RefineSettings.OutputFilePath);
            if (oldDependencies.ContainsKey(config.Name) && (nameChanged || refineFileChanged))
            {
                var runningConfigs = ConfigsBusy();
                var runningDependentConfigs = oldDependencies[config.Name].Where(x => runningConfigs.Contains(x)).ToList();
                if (runningDependentConfigs.Any())
                {
                    ConfigManager.DisplayWarning(uiControl,
                        Resources.SkylineBatchConfigManager_UserReplaceSelected_Could_not_update_the_configuration_name_or_the_refined_output_file_path_while_the_following_dependent_configurations_are_running_ +
                        Environment.NewLine +
                        TextUtil.LineSeparate(runningDependentConfigs) + Environment.NewLine + Environment.NewLine +
                        Resources.SkylineBatchConfigManager_UserReplaceSelected_Please_wait_until_the_dependent_configurations_have_stopped_to_change_these_values_);
                    var newRefineSettings = RefineSettings.GetPathChanged(newConfig.RefineSettings, config.RefineSettings.OutputFilePath);
                    newConfig = new SkylineBatchConfig(config.Name, newConfig.Enabled, newConfig.LogTestFormat, DateTime.Now,
                        newConfig.MainSettings,
                        newConfig.FileSettings, newRefineSettings, newConfig.ReportSettings,
                        newConfig.SkylineSettings);
                    nameChanged = refineFileChanged = false;
                }
            }

            ProgramaticallyRemoveAt(index);
            UserInsertConfig(index, newConfig, uiControl); // can throw ArgumentException

            if ((nameChanged || refineFileChanged) && oldDependencies.ContainsKey(config.Name))
            {
                foreach (var dependent in oldDependencies[config.Name])
                    DependencyReplace(dependent, newConfig.Name, newConfig.RefineSettings.OutputFilePath, uiControl);
            }
            return this;
        }

        public SkylineBatchConfigManagerState UserRemoveSelected(IMainUiControl uiControl)
        {
            BaseState.AssertConfigSelected();
            var index = BaseState.Selected;
            var removingConfigName = BaseState.ConfigList[index].GetName();
            if (ConfigRunners[removingConfigName].IsBusy())
            {
                ConfigManager.DisplayWarning(uiControl, string.Format(
                    Resources.ConfigManager_RemoveSelected___0___is_still_running__Please_stop_the_current_run_before_deleting___0___,
                    removingConfigName));
                return this;
            }
            var configDependencies = GetDependencies();
            if (configDependencies.ContainsKey(removingConfigName))
            {
                var runningConfigs = ConfigsBusy();
                var runningDependentConfigs = configDependencies[removingConfigName].Where(x => runningConfigs.Contains(x)).ToList();

                if (runningDependentConfigs.Any())
                {
                    ConfigManager.DisplayError(uiControl, string.Format(
                                              Resources.SkylineBatchConfigManager_UserRemoveSelected_Cannot_delete___0___while_the_following_configurations_are_running_, removingConfigName) + Environment.NewLine +
                                          TextUtil.LineSeparate(runningDependentConfigs) + Environment.NewLine + Environment.NewLine +
                                          Resources.SkylineBatchConfigManager_UserRemoveSelected_Please_wait_until_these_configurations_have_finished_running_);
                    return this;
                }
                var answer = ConfigManager.DisplayQuestion(uiControl,
                    string.Format(
                        Resources.SkylineBatchConfigManager_UserRemoveSelected_Deleting___0___may_impact_the_template_files_of_the_following_configurations_,
                        removingConfigName) +
                    Environment.NewLine +
                    TextUtil.LineSeparate(configDependencies[removingConfigName]) + Environment.NewLine +
                    string.Format(Resources.SkylineBatchConfigManager_UserRemoveSelected_Are_you_sure_you_want_to_delete___0___, removingConfigName));

                if (answer != DialogResult.Yes)
                {
                    //BaseState.ModelUnchanged();
                    return this;
                }
                foreach (var dependentName in configDependencies[removingConfigName])
                    DependencyRemove(dependentName, uiControl);
            }

            RemoveConfig(BaseState.ConfigList[index]);
            BaseState.UserRemoveAt(index);
            return this;
        }


        #endregion

        #region Import/Export

        public SkylineBatchConfigManagerState Import(string filePath, IMainUiControl uiControl, ConfigManagerState.ShowDownloadedFileForm showDownloadedFileForm)
        {
            BaseState.ImportFrom(SkylineBatchConfig.ReadXml, filePath, Settings.Default.XmlVersion, uiControl, showDownloadedFileForm, out _);
            UpdateFromBaseState(uiControl);
            return DisableInvalidConfigs();
        }

        public SkylineBatchConfigManagerState UpdateFromBaseState(IMainUiControl uiControl)
        {
            foreach (var iconfig in BaseState.ConfigList)
            {
                var name = iconfig.GetName();
                if (ConfigRunners.ContainsKey(name))
                {
                    if (!ConfigRunners[name].GetConfig().Equals(iconfig))
                        ConfigRunners = ConfigRunners.Remove(name).Add(name, new ConfigRunner((SkylineBatchConfig)iconfig, _mainLogger, uiControl));
                }
                else
                {
                    ConfigRunners = ConfigRunners.Add(name, new ConfigRunner((SkylineBatchConfig)iconfig, _mainLogger, uiControl));
                }
            }
            foreach (var config in ConfigRunners.Keys)
            {
                if (BaseState.GetConfigIndex(config) < 0)
                    ConfigRunners = ConfigRunners.Remove(config);
            }
            UpdateDependencies(uiControl);
            UpdateRemoteFileSources(uiControl);
            return this;
        }

        #endregion

        #region Config Template Dependencies

        private SkylineBatchConfigManagerState UpdateTemplates()
        {
            Templates = ImmutableDictionary<string, string>.Empty;
            foreach (var iconfig in BaseState.ConfigList)
            {
                var config = (SkylineBatchConfig) iconfig;
                if (config.RefineSettings.WillRefine())
                    Templates = Templates.Add(config.Name, config.RefineSettings.OutputFilePath);
            }
            return this;
        }

        private SkylineBatchConfigManagerState UpdateDependencies(IMainUiControl uiControl)
        {
            UpdateTemplates();
            var errorConfigs = new List<string>();

            foreach (var iconfig in BaseState.ConfigList)
            {
                var config = (SkylineBatchConfig)iconfig;
                var dependentName = config.MainSettings.Template.DependentConfigName;
                if (!string.IsNullOrEmpty(dependentName))
                {
                    var index = BaseState.GetConfigIndex(config.Name);
                    var oldConfigRunner = ConfigRunners[config.Name];
                    var oldTemplatePath = config.MainSettings.Template.FilePath;
                    ProgramaticallyRemoveAt(index);
                    string templatePath;
                    try
                    {
                        templatePath = Templates[dependentName];
                    }
                    catch (Exception)
                    {
                        errorConfigs.Add(config.Name);
                        ProgramaticallyInsertConfig(index, config.WithoutDependency(), uiControl);
                        continue;
                    }
                    ProgramaticallyInsertConfig(index, config.DependentChanged(dependentName, templatePath), uiControl);
                    if (oldTemplatePath.Equals(templatePath))
                        ConfigRunners = ConfigRunners.Remove(config.Name).Add(config.Name, oldConfigRunner);
                }
            }

            if (errorConfigs.Count != 0)
                ConfigManager.DisplayWarning(uiControl, Resources.SkylineBatchConfigManager_AssignDependencies_The_following_configurations_use_refined_template_files_from_other_configurations_that_do_not_exist_ + Environment.NewLine +
                               TextUtil.LineSeparate(errorConfigs) + Environment.NewLine +
                               Resources.SkylineBatchConfigManager_AssignDependencies_You_may_want_to_update_the_template_file_paths_);
            return this;
        }

        private SkylineBatchConfigManagerState DependencyRemove(string configName, IMainUiControl uiControl)
        {
            var index = BaseState.GetConfigIndex(configName);
            var config = (SkylineBatchConfig) BaseState.ConfigList[index];
            ProgramaticallyRemoveAt(index);
            var newConfig = config.WithoutDependency();
            ProgramaticallyInsertConfig(index, newConfig, uiControl);
            DisableInvalidConfigs();
            return this;
        }

        public Dictionary<string, List<string>> GetDependencies()
        {
            var dependencies = new Dictionary<string, List<string>>();
            foreach (var iconfig in BaseState.ConfigList)
            {
                var config = (SkylineBatchConfig)iconfig;
                var dependentConfigName = config.MainSettings.Template.DependentConfigName;
                if (!string.IsNullOrEmpty(dependentConfigName))
                {
                    if (!dependencies.ContainsKey(dependentConfigName))
                        dependencies.Add(dependentConfigName, new List<string>());
                    dependencies[dependentConfigName].Add(config.Name);
                }
            }
            return dependencies;
        }

        private SkylineBatchConfigManagerState DependencyReplace(string configName, string dependentName, string templateFile, IMainUiControl uiControl)
        {
            var index = BaseState.GetConfigIndex(configName);
            var config = (SkylineBatchConfig) BaseState.ConfigList[index];
            ProgramaticallyRemoveAt(index);
            var newConfig = config.DependentChanged(dependentName, templateFile);
            ProgramaticallyInsertConfig(index, newConfig, uiControl);
            DisableInvalidConfigs();
            return this;
        }

        #endregion

        #region Update All Configs

        public SkylineBatchConfigManagerState ReplaceSkylineSettings(SkylineSettings skylineSettings, IMainUiControl uiControl, out bool? replaced)
        {
            var runningConfigs = ConfigsBusy();
            BaseState.AskToReplaceAllSkylineVersions(skylineSettings, runningConfigs, uiControl, out replaced);
            UpdateFromBaseState(uiControl);
            return this;
        }

        public SkylineBatchConfigManagerState RootReplaceConfigs(string oldRoot, string newRoot, IMainUiControl uiControl)
        {
            BaseState.GetRootReplacedConfigs(oldRoot, newRoot);
            return UpdateFromBaseState(uiControl);
        }

        public SkylineBatchConfigManagerState UpdateConfigValidation()
        {
            BaseState.UpdateConfigValidation();
            //BaseState.ModelUnchanged();
            return this;
        }

        #endregion

        #region Change Config Enabled

        public SkylineBatchConfigManagerState CheckConfigAtIndex(int index, IMainUiControl uiControl, out string errorMessage)
        {
            errorMessage = null;
            var config = (SkylineBatchConfig) BaseState.ConfigList[index];
            var runner = ConfigRunners[config.Name];
            if (!BaseState.IsConfigValid(index))
            {
                errorMessage =
                    string.Format(
                        Resources.MainForm_listViewConfigs_ItemCheck_Cannot_enable___0___while_it_is_invalid_,
                        config.Name) +
                    Environment.NewLine +
                    string.Format(Resources.ConfigManager_RunAll_Please_edit___0___to_enable_running_, config.Name);
                return this;
            }
            if (runner.IsRunning() || runner.IsCanceling())
            {
                errorMessage =
                    string.Format(Resources.ConfigManager_CheckConfigAtIndex_Cannot_disable___0___while_it_has_status___1_,
                        config.Name, runner.GetStatus()) +
                    Environment.NewLine +
                    string.Format(Resources.ConfigManager_CheckConfigAtIndex_Please_wait_until___0___has_finished_running_, config.Name);
                return this;
            }
            UpdateConfigEnabled(config, !config.Enabled, uiControl);
            if (runner.IsWaiting())
                runner.Cancel();
            return this;
        }

        private SkylineBatchConfigManagerState DisableInvalidConfigs()
        {
            foreach (var iconfig in BaseState.ConfigList)
            {
                if (!BaseState.ConfigValidation[iconfig.GetName()])
                {
                    var config = (SkylineBatchConfig)iconfig;
                    UpdateConfigEnabled(config, false);
                }
            }
            return this;
        }

        private SkylineBatchConfigManagerState UpdateConfigEnabled(SkylineBatchConfig config, bool enabled, IMainUiControl uiControl = null)
        {
            var index = BaseState.GetConfigIndex(config.Name);
            var newConfig = new SkylineBatchConfig(config.GetName(), enabled, config.LogTestFormat,
                config.Modified, config.MainSettings, config.FileSettings, config.RefineSettings,
                config.ReportSettings, config.SkylineSettings);
            ProgramaticallyRemoveAt(index);
            ProgramaticallyInsertConfig(index, newConfig, uiControl);
            //BaseState.ModelUnchanged();
            return this;
        }

        #endregion

        #region Root Replacement

        public SkylineBatchConfigManagerState AddRootReplacement(string oldPath, string newPath, bool askAboutRootReplacement, IMainUiControl uiControl,
            out bool addedRootReplacement, out string oldRoot, out bool askedAboutRootReplacement)
        {
            BaseState.AddRootReplacement(oldPath, newPath, askAboutRootReplacement, uiControl, out oldRoot,
                out askedAboutRootReplacement, out addedRootReplacement);
            return this;
        }


        #endregion

        #region Information About State

        public HashSet<string> RVersionsUsed()
        {
            var RVersions = new HashSet<string>();
            foreach (var iconfig in BaseState.ConfigList)
            {
                var config = (SkylineBatchConfig)iconfig;
                RVersions.UnionWith(config.ReportSettings.RVersions());
            }

            return RVersions;
        }

        public SkylineBatchConfig GetSelectedConfig()
        {
            return (SkylineBatchConfig)BaseState.GetSelectedConfig();
        }

        public Dictionary<string, string> GetRefinedTemplates()
        {
            return Templates.ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        public bool WillRefine()
        {
            foreach (var iconfig in BaseState.ConfigList)
            {
                var config = (SkylineBatchConfig)iconfig;
                if (config.Enabled && config.RefineSettings.WillRefine())
                    return true;
            }
            return false;
        }

        public ConfigRunner GetSelectedConfigRunner()
        {
            return (ConfigRunner) ConfigRunners[BaseState.GetSelectedConfig().GetName()];
        }

        public List<string> GetEnabledConfigNames()
        {
            var enabledConfigNames = new List<string>();
            var enabledConfigs = GetEnabledConfigs();
            foreach (var config in enabledConfigs)
                enabledConfigNames.Add(config.Name);
            return enabledConfigNames;
        }

        public List<SkylineBatchConfig> GetEnabledConfigs()
        {
            var enabledConfigs = new List<SkylineBatchConfig>();
            foreach (var iconfig in BaseState.ConfigList)
            {
                var config = (SkylineBatchConfig)iconfig;
                if (config.Enabled) enabledConfigs.Add(config);
            }
            return enabledConfigs;
        }

        public SkylineBatchConfig ConfigFromName(string name)
        {
            return (SkylineBatchConfig) ConfigRunners[name].GetConfig();
        }

        public List<string> ConfigsBusy()
        {
            var configsRunning = new List<string>();
            foreach (var runner in ConfigRunners.Values)
            {
                if (runner.IsBusy())
                    configsRunning.Add(runner.GetConfigName());
            }
            return configsRunning;
        }

        public string GetNextWaitingConfig()
        {
            foreach (var config in BaseState.ConfigList)
            {
                if (ConfigRunners[config.GetName()].GetStatus() == RunnerStatus.Waiting)
                {
                    return config.GetName();
                }
            }
            return null;
        }

        public bool ConfigRunning()
        {
            foreach (var runner in ConfigRunners.Values)
            {
                if (runner.IsRunning())
                    return true;
            }
            return false;
        }

        public void CancelRunners()
        {
            foreach (var configRunner in ConfigRunners.Values)
            {
                configRunner.Cancel();
            }
        }

        #endregion

        #region Wrappers for BaseSate Methods

        public SkylineBatchConfigManagerState SelectIndex(int index)
        {
            BaseState.SelectIndex(index);
            return this;
        }

        public SkylineBatchConfigManagerState MoveSelectedConfig(bool moveUp)
        {
            BaseState.MoveSelectedConfig(moveUp);
            return this;
        }

        public SkylineBatchConfigManagerState ModelUnchanged()
        {
            BaseState.ModelUnchanged();
            return this;
        }

        #endregion

        #region Override Equals

        protected bool Equals(SkylineBatchConfigManagerState other)
        {
            if (Templates.Count != other.Templates.Count)
                return false;
            foreach (var config in Templates.Keys)
            {
                if (!other.Templates.ContainsKey(config) || !Equals(Templates[config], other.Templates[config]))
                    return false;
            }
            if (ConfigRunners.Count != other.ConfigRunners.Count)
                return false;
            foreach (var config in ConfigRunners.Keys)
            {
                if (!other.ConfigRunners.ContainsKey(config))
                    return false;
                if ((ConfigRunners[config].IsBusy() || other.ConfigRunners[config].IsBusy()) &&
                    !ReferenceEquals(ConfigRunners[config], other.ConfigRunners[config]))
                    return false;
                if (!Equals(ConfigRunners[config].GetConfig(), other.ConfigRunners[config].GetConfig()))
                    return false;
            }
            return Equals(BaseState, other.BaseState);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SkylineBatchConfigManagerState)obj);
        }

        public override int GetHashCode()
        {
            return BaseState.GetHashCode() +
                   Templates.GetHashCode() +
                   ConfigRunners.GetHashCode();
        }

        #endregion

        #region Remote File Sources

        public SkylineBatchConfigManagerState UserAddRemoteFileSource(RemoteFileSource remoteFileSource, bool preferPanorama, IMainUiControl uiControl)
        {
            if (FileSources.ContainsKey(remoteFileSource.Name))
                ConfigManager.DisplayError(uiControl, string.Format(Resources.SkylineBatchConfigManagerState_UserAddRemoteFileSource_A_remote_file_source_named___0___already_exists__Please_choose_a_different_name_, remoteFileSource.Name));
            else
            {
                if (preferPanorama && remoteFileSource.FtpSource)
                {
                    if (DialogResult.No == ConfigManager.DisplayQuestion(uiControl, Resources.SkylineBatchConfigManagerState_UserAddRemoteFileSource_This_file_type_must_be_downloaded_from_Panorama_instead_of_an_FTP_source__Do_you_want_to_add_this_FTP_file_source_anyway_))
                        return this;
                }
                FileSources = FileSources.Add(remoteFileSource.Name, remoteFileSource);
            }
            return this;
        }

        public SkylineBatchConfigManagerState ProgramaticallyAddRemoteFileSource(RemoteFileSource remoteFileSource)
        {
            if (FileSources.ContainsKey(remoteFileSource.Name))
            {
                var name = remoteFileSource.Name;
                if (Equals(FileSources[name]))
                    return this;
                var duplicateIndexRegex = new Regex("\\(([1-9][0-9]*)\\)$");
                var regexMatches = duplicateIndexRegex.Match(name).Groups;
                string newName;
                if (regexMatches.Count > 0)
                {
                    var lastIndex = int.Parse(regexMatches[0].Value);
                    newName = duplicateIndexRegex.Replace(name, $"({lastIndex + 1})");
                }
                else
                {
                    newName = name + "(2)";
                }
                remoteFileSource = new RemoteFileSource(newName, remoteFileSource.URI, remoteFileSource.Username, remoteFileSource.Password, remoteFileSource.Encrypt);
            }
            FileSources = FileSources.Add(remoteFileSource.Name, remoteFileSource);
            return this;
        }

        public SkylineBatchConfigManagerState RemoveRemoteFileSource(string name)
        {
            if (!FileSources.ContainsKey(name))
                return this;
            FileSources = FileSources.Remove(name);
            return this;
        }

        public SkylineBatchConfigManagerState ReplaceRemoteFileSource(RemoteFileSource existingSource, RemoteFileSource newSource, IMainUiControl uiControl, string editingConfigName = null)
        {
            if (!FileSources.ContainsKey(existingSource.Name))
                return this;
            var replacedConfigs = new List<IConfig>();
            foreach (var iconfig in BaseState.ConfigList)
            {
                var config = (SkylineBatchConfig) iconfig;
                var newConfig = config.ReplacedRemoteFileSource(existingSource, newSource, out bool replaced);
                if (replaced)
                    replacedConfigs.Add(newConfig);
            }

            var replacedConfigsString = string.Empty;
            foreach (var config in replacedConfigs)
            {
                if (!config.GetName().Equals(editingConfigName))
                    replacedConfigsString += config.GetName() + Environment.NewLine;
            }
            
            if (replacedConfigsString.Length > 0 && !existingSource.Equivalent(newSource))
            {
                var message = Resources.SkylineBatchConfigManagerState_ReplaceRemoteFileSource_Changing_this_file_source_will_impact_the_following_configurations_ +
                              Environment.NewLine + Environment.NewLine +
                              replacedConfigsString + Environment.NewLine +
                    Resources.SkylineBatchConfigManagerState_ReplaceRemoteFileSource_Do_you_want_to_continue_;
                if (DialogResult.OK != ConfigManager.DisplayLargeOkCancel(uiControl, message))
                    return this;
            }

            FileSources = FileSources.Remove(existingSource.Name);
            foreach (var config in replacedConfigs)
            {
                var index = BaseState.GetConfigIndex(config.GetName());
                ProgramaticallyRemoveAt(index);
                ProgramaticallyInsertConfig(index, config, uiControl);
            }
            FileSources = FileSources.Remove(existingSource.Name).Add(newSource.Name, newSource);
            return this;
        }

        public SkylineBatchConfigManagerState UpdateRemoteFileSources(IMainUiControl uiControl)
        {
            FileSources = ImmutableDictionary<string, RemoteFileSource>.Empty;
            int i = 0;
            foreach (var iconfig in BaseState.ConfigList)
            {
                var config = (SkylineBatchConfig) iconfig;
                var newConfig = config.UpdateRemoteFileSet(FileSources,
                    out ImmutableDictionary<string, RemoteFileSource> newRemoteFileSources);
                if (!Equals(config, newConfig))
                {
                    ProgramaticallyRemoveAt(i);
                    ProgramaticallyInsertConfig(i, newConfig, uiControl);
                }
                FileSources = newRemoteFileSources;
            }
            return this;
        }


        #endregion

    }

    public enum RunBatchOptions
    {
        ALL,
        DOWNLOAD_DATA,
        FROM_TEMPLATE_COPY,
        FROM_REFINE,
        FROM_REPORT_EXPORT,
        R_SCRIPTS,
    }
}

