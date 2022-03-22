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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class MainForm : Form, IMainUiControl
    {

        private SkylineBatchConfigManager _configManager;
        private readonly Logger _skylineBatchLogger;
        private RDirectorySelector _rDirectorySelector;
        private bool _loaded;
        private readonly ColumnWidthCalculator _listViewColumnWidths;
        private bool _showRefineStep;
        private Timer _outputLog;

        public MainForm(string openFile)
        {
            InitializeComponent();
            Icon = Program.Icon();
            var roamingFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var localFolder = Path.Combine(Path.GetDirectoryName(roamingFolder) ?? throw new InvalidOperationException(), "local");
            var logPath= Path.Combine(localFolder, Program.AppName(), Program.AppName() + TextUtil.EXT_LOG);
            Logger.AddErrorMatch(string.Format(Resources.ConfigRunner_Run_________________________________0____1_________________________________, ".*", RunnerStatus.Error));
            _skylineBatchLogger = new Logger(logPath, Program.AppName() + TextUtil.EXT_LOG, true);
            toolStrip1.Items.Insert(3,new ToolStripSeparator());
            toolStrip1.Items.Insert(7, new ToolStripSeparator());
            _listViewColumnWidths = new ColumnWidthCalculator(listViewConfigs);
            listViewConfigs.ColumnWidthChanged += listViewConfigs_ColumnWidthChanged;
            ProgramLog.Info(Resources.MainForm_MainForm_Loading_configurations_from_saved_settings_);
            
            _outputLog = new Timer { Interval = 500 };
            _outputLog.Tick += OutputLog;
            _outputLog.Start();

            Shown += ((sender, args) =>
            {
                _configManager = new SkylineBatchConfigManager(_skylineBatchLogger, this);
                _configManager.LoadConfigList();
                _loaded = true;
                UpdateButtonsEnabled();
                if (!string.IsNullOrEmpty(openFile))
                    FileOpened(openFile);
                _rDirectorySelector = new RDirectorySelector(this);
                ListViewSizeChanged();
                UpdateUiLogFiles();
                UpdateRunBatchSteps();
            });
        }

        private void RunUi(Action action)
        {
            if (!_loaded)
            {
                try
                {
                    action();
                }
                catch (InvalidOperationException)
                {
                }
                return;
            }

            try
            {
                BeginInvoke(action);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        #region Manipulating configuration list
        
        private void btnNewConfig_Click(object sender, EventArgs e)
        {
            ProgramLog.Info(Resources.MainForm_btnNewConfig_Click_Creating_a_new_configuration_);
            var initialState = _configManager.State;
            var initialConfigValues = (SkylineBatchConfig)_configManager.State.BaseState.GetLastModified();
            var configForm = new SkylineBatchConfigForm(this, _rDirectorySelector, initialConfigValues, ConfigAction.Add, false, initialState.Copy());
            configForm.ShowDialog();
            _configManager.SetState(initialState, configForm.State);
        }

        private void HandleEditEvent(object sender, EventArgs e)
        {
            var initialState = _configManager.State;
            var configRunner = initialState.GetSelectedConfigRunner();
            var config = (SkylineBatchConfig)configRunner.GetConfig();
            if (!initialState.BaseState.IsSelectedConfigValid())
            {
                if (configRunner.IsRunning()) throw new Exception("Invalid configuration cannot be running.");
                var validateConfigForm = new InvalidConfigSetupForm(this, config, initialState.Copy(), _rDirectorySelector);
                try
                {
                    validateConfigForm.ShowDialog();
                    if (validateConfigForm.DialogResult != DialogResult.OK)
                        return;
                }
                catch (ObjectDisposedException)
                {
                    // pass - the field making the config invalid cannot be set up in the invalid config manager
                }

                _configManager.SetState(initialState, validateConfigForm.State);
                initialState = validateConfigForm.State;
            }
            var configForm = new SkylineBatchConfigForm(this, _rDirectorySelector, initialState.GetSelectedConfig(), ConfigAction.Edit, configRunner.IsBusy(), initialState.Copy());
            configForm.ShowDialog();
            _configManager.SetState(initialState, configForm.State);
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            var initialState = _configManager.State;
            var configForm = new SkylineBatchConfigForm(this, _rDirectorySelector, initialState.GetSelectedConfig(), ConfigAction.Copy, false, initialState.Copy());
            configForm.ShowDialog();
            _configManager.SetState(initialState, configForm.State);
        }

        

        private void listViewConfigs_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (!_loaded || e.NewValue == e.CurrentValue) return;
            e.NewValue = e.CurrentValue;
            ChangeConfigEnabled(e.Index);
        }

        private void ChangeConfigEnabled(int index)
        {
            var success = _configManager.CheckConfigAtIndex(index, out string errorMessage);
            if (!success)
            {
                if (errorMessage != null) DisplayError(errorMessage);
                return;
            }
            UpdateRunBatchSteps();
        }

        private void SelectConfig(int index)
        {
            if (index < 0)
            {
                _configManager.DeselectConfig();
                return;
            }
            _configManager.SelectConfig(index);
        }

        private void listViewConfigs_MouseUp(object sender, MouseEventArgs e)
        {
            // Select configuration through _configManager
            var index = listViewConfigs.GetItemAt(e.X, e.Y) != null ? listViewConfigs.GetItemAt(e.X, e.Y).Index : -1;
            SelectConfig(index);
        }
        
        private void listViewConfigs_PreventItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            // Disable automatic item selection - selected configuration set through _configManager
            //      Automatic selection disables red text, can't see invalid configurations
            listViewConfigs.SelectedIndices.Clear();
        }

        private void UpdateButtonsEnabled()
        {
            var baseState = _configManager.State.BaseState;
            var canUndo = _configManager.CanUndo();
            var canRedo = _configManager.CanRedo();
            RunUi(() =>
            {
                var configSelected = _loaded ? baseState.HasSelectedConfig() : false;
                var indexSelected = baseState.Selected;
                var hasConfigs = baseState.HasConfigs();
                btnEdit.Enabled = configSelected;
                btnCopy.Enabled = configSelected;
                btnUpArrow.Enabled = configSelected && indexSelected != 0;
                btnDownArrow.Enabled = configSelected && indexSelected < listViewConfigs.Items.Count - 1;
                btnDelete.Enabled = configSelected;
                btnOpenAnalysis.Enabled = configSelected;
                btnOpenTemplate.Enabled = configSelected;
                btnOpenResults.Enabled = configSelected;
                btnExportConfigs.Enabled = _loaded ? hasConfigs : false;
                btnUndo.Enabled = canUndo;
                btnRedo.Enabled = canRedo;
            });
        }

        private void btnUpArrow_Click(object sender, EventArgs e)
        {
            _configManager.MoveSelectedConfig(true);
            UpdateUiConfigurations();
        }

        private void btnDownArrow_Click(object sender, EventArgs e)
        {
            _configManager.MoveSelectedConfig(false);
            UpdateUiConfigurations();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            _configManager.UserRemoveSelected();
            UpdateUiConfigurations();
            ListViewSizeChanged();
        }
        
        #endregion

        #region Open File/Folder

        private void btnOpenAnalysis_Click(object sender, EventArgs e)
        {
            var state = _configManager.State;
            var config = state.GetSelectedConfig();
            config.MainSettings.CreateAnalysisFolderIfNonexistent();
            MainFormUtils.OpenFileExplorer(config.Name, state.BaseState.IsSelectedConfigValid(),
                config.MainSettings.AnalysisFolderPath, Resources.MainForm_btnOpenAnalysis_Click_analysis_folder, this);
        }

        private void btnOpenTemplate_Click(object sender, EventArgs e)
        {
            var state = _configManager.State;
            var config = state.GetSelectedConfig();
            if (!config.MainSettings.Template.Exists())
            {
                DisplayError(string.Format(Resources.MainForm_btnOpenTemplate_Click_The_template_file_for___0___has_not_been_downloaded__Please_run___0___and_try_again_, config.Name));
            }
            if (MainFormUtils.CanOpen(config.Name, state.BaseState.IsSelectedConfigValid(), config.MainSettings.Template.FilePath,
                Resources.MainForm_btnOpenTemplate_Click_Skyline_template_file, this))
            {
                SkylineInstallations.OpenSkylineFile(config.MainSettings.Template.FilePath, config.SkylineSettings);
            }
        }

        private void btnOpenResults_Click(object sender, EventArgs e)
        {
            var state = _configManager.State;
            var config = state.GetSelectedConfig();
            var resultsFile = config.MainSettings.GetResultsFilePath();

            if (MainFormUtils.CanOpen(config.Name, state.BaseState.IsSelectedConfigValid(), resultsFile,
                Resources.MainForm_btnOpenResults_Click_Skyline_results_file, this))
            {
                if (File.Exists(resultsFile))
                    SkylineInstallations.OpenSkylineFile(resultsFile, config.SkylineSettings);
                else
                {
                    DisplayError(Resources.MainForm_btnOpenResults_Click_The_Skyline_results_file_for_this_configuration_has_not_been_generated_yet_ + Environment.NewLine +
                                 string.Format(Resources.MainForm_btnOpenResults_Click_Please_run___0___from_step_one_and_try_again_, config.Name));
                }
            }
        }

        #endregion
        
        #region Running configurations

        private void btnRunOptions_Click(object sender, EventArgs e)
        {
            batchRunDropDown.Show(btnRunBatch, new Point(0, btnRunBatch.Height));
        }

        private void batchRunDropDown_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            int selectedIndex = 0;
            for (int i = 0; i < batchRunDropDown.Items.Count; i++)
            {
                if (batchRunDropDown.Items[i].Text == e.ClickedItem.Text)
                    selectedIndex = i;
            }
            CheckDropDownOption(selectedIndex);
            RunBatch();
        }

        private void CheckDropDownOption(int index)
        {
            for (int i = 0; i < batchRunDropDown.Items.Count; i++)
            {
                ((ToolStripMenuItem)batchRunDropDown.Items[i]).Checked = false;
            }
            ((ToolStripMenuItem)batchRunDropDown.Items[index]).Checked = true;
            btnRunBatch.TextAlign = index == 0 ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            btnRunBatch.Text = string.Format(Resources.MainForm_CheckDropDownOption_R_un___0_, batchRunDropDown.Items[index].Text);
        }

        private void btnRunBatch_Click(object sender, EventArgs e)
        {
            RunBatch();
        }

        private void RunBatch()
        {
            var stepIndex = GetCheckedRunOptionIndex();
            if (!_showRefineStep && stepIndex >= (int)RunBatchOptions.FROM_REFINE)
                stepIndex += 1; // step 2 and 3 become step 3 and 4 when refine step is hidden

            var runOption = (RunBatchOptions)stepIndex;
            var checkServersLongWaitDlg = new LongWaitDlg(this, Program.AppName(),
                Resources.MainForm_RunBatch_Checking_servers_for_files_to_download___);

            if (!_configManager.CanRun(runOption)) return;
            _configManager.StartCheckingServers(checkServersLongWaitDlg, StartRun);
        }

        private void StartRun(bool success)
        {
            if (!success) return;
            _configManager.StartBatchRun();
            RunUi(() =>
            {
                comboLogList.SelectedIndex = 0;
                tabMain.SelectTab(tabLog);
            });
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            _configManager.State.CancelRunners();
            btnStop.Enabled = false;
            btnLogStop.Enabled = false; 
        }

        #endregion
        
        #region Update UI

        // Reload configurations from configManager
        public void UpdateUiConfigurations()
        {
            List<ListViewItem> listViewItems;
            try
            {
                listViewItems = _configManager.ConfigsListViewItems(listViewConfigs.CreateGraphics());
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            RunUi(() =>
            {
                ProgramLog.Info("Updating configurations");
                var topItemIndex = listViewConfigs.TopItem != null ? listViewConfigs.TopItem.Index : -1;
                listViewConfigs.ItemCheck -= listViewConfigs_ItemCheck;
                listViewConfigs.Items.Clear();
                foreach (var lvi in listViewItems)
                    listViewConfigs.Items.Add(lvi);
                if (topItemIndex != -1 && listViewConfigs.Items.Count > topItemIndex)
                    listViewConfigs.TopItem = listViewConfigs.Items[topItemIndex];
                listViewConfigs.ItemCheck += listViewConfigs_ItemCheck;
            });
            UpdateLabelVisibility();
            UpdateButtonsEnabled();
            UpdateRunBatchSteps();
        }

        // Reload logs in comboLogList
        public void UpdateUiLogFiles()
        {
            var logFiles = _configManager.GetAllLogFiles();
            var selectedLog = _configManager.SelectedLog;
            var hasOldLogs = _configManager.HasOldLogs();
            RunUi(() =>
            {
                ProgramLog.Info("Updating log files");
                comboLogList.Items.Clear();
                comboLogList.Items.AddRange(logFiles);
                comboLogList.SelectedIndex = selectedLog;
                btnDeleteLogs.Enabled = hasOldLogs;
            });
        }

        public void UpdateRunningButtons(bool canStart, bool canStop)
        {
            RunUi(() =>
            {
                btnRunBatch.Enabled = canStart;
                btnRunOptions.Enabled = btnRunBatch.Enabled;
                btnStop.Enabled = canStop;
                btnLogStop.Enabled = canStop;
            });
        }

        // Toggle label if no configs
        private void UpdateLabelVisibility()
        {
            var hasConfigs = _configManager.State.BaseState.HasConfigs();
            RunUi(() =>
            {
                if (hasConfigs)
                {
                    lblNoConfigs.Hide();
                }
                else
                {
                    lblNoConfigs.Show();
                }
            });
        }

        private int GetCheckedRunOptionIndex()
        {
            for (int i = 0; i < batchRunDropDown.Items.Count; i++)
            {
                if (((ToolStripMenuItem)batchRunDropDown.Items[i]).Checked)
                    return i;
            }

            return 0;
        }

        private void UpdateRunBatchSteps()
        {
            var willRefine = _configManager.State.WillRefine();
            if (_showRefineStep == willRefine && _loaded)
                return;
            _showRefineStep = willRefine;
            RunUi(() =>
            {
                var oldChecked = GetCheckedRunOptionIndex();
                batchRunDropDown.Items.Clear();
                batchRunDropDown.Items.Add(Resources.MainForm_UpdateRunBatchSteps_All);
                batchRunDropDown.Items.Add(Resources.MainForm_UpdateRunBatchSteps_Download_files_only);
                batchRunDropDown.Items.Add(Resources.MainForm_UpdateRunBatchSteps_Start_from_template_copy);
                if (_showRefineStep)
                {
                    batchRunDropDown.Items.Add(Resources.MainForm_UpdateRunBatchSteps_Start_from_refinement);
                    batchRunDropDown.Items.Add(Resources.MainForm_UpdateRunBatchSteps_Start_from_report_export);
                    batchRunDropDown.Items.Add(Resources.MainForm_UpdateRunBatchSteps_R_scripts_only);
                }
                else
                {
                    batchRunDropDown.Items.Add(Resources.MainForm_UpdateRunBatchSteps_Start_from_report_export);
                    batchRunDropDown.Items.Add(Resources.MainForm_UpdateRunBatchSteps_R_scripts_only);
                }

                var newChecked = oldChecked;
                if (oldChecked == (int)RunBatchOptions.FROM_REFINE && _showRefineStep)
                    newChecked += 1;
                else if (newChecked > (int)RunBatchOptions.FROM_REFINE)
                    newChecked = _showRefineStep ? oldChecked + 1 : oldChecked - 1;
                
                CheckDropDownOption(newChecked);
            });
        }

        #endregion
        
        #region Import / export

        private void btnImport_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = TextUtil.FILTER_BCFG;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            DoImport(dialog.FileName);
        }

        public void FileOpened(string filePath)
        {
            bool importConfigs;
            var inDownloadsFolder = filePath.Contains(FileUtil.DOWNLOADS_FOLDER);

            RunUi(() =>
            {
                // Only show dialog if configs are not in downloads folder
                importConfigs = inDownloadsFolder || DialogResult.Yes == DisplayQuestion(string.Format(
                    Resources.MainForm_FileOpenedImport_Do_you_want_to_import_configurations_from__0__,
                    Path.GetFileName(filePath)));
                if (importConfigs)
                    DoImport(filePath);
            });
        }

        public DialogResult ShowDownloadedFileForm(string filePath, out string newRootDirectory)
        {
            var fileOpenedForm = new FileOpenedForm(this, filePath, Program.Icon());
            var dialogResult = fileOpenedForm.ShowDialog(this);
            newRootDirectory = fileOpenedForm.NewRootDirectory;
            return dialogResult;
        }

        public void DoImport(string filePath)
        {
            _configManager.Import(filePath, ShowDownloadedFileForm);
            var initialState = _configManager.GetState();
            bool stateChanged = false;
            if (!_rDirectorySelector.ShownDialog)
                _rDirectorySelector.AddIfNecassary(initialState.Copy(), out stateChanged);
            if (stateChanged)
                _configManager.SetState(initialState, _rDirectorySelector.State);
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            var baseState = _configManager.GetState().BaseState;
            var shareForm = new ShareConfigsForm(this, baseState, Program.Icon());
            if (shareForm.ShowDialog(this) != DialogResult.OK)
                return;
            var dialog = new SaveFileDialog { Filter = TextUtil.FILTER_BCFG };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            baseState.ExportConfigs(dialog.FileName, Settings.Default.XmlVersion, shareForm.IndiciesToSave);
        }
        
        #endregion
        
        #region Logging

        private bool _scrolling = true;

        private void OutputLog(object sender, EventArgs e)
        {
            if (textBoxLog.IsDisposed) return;
            _outputLog.Tick -= OutputLog;
            if (WindowState != FormWindowState.Minimized && tabLog.Visible && textBoxLog.TextLength > 0)
            {
                _scrolling = textBoxLog.GetPositionFromCharIndex(textBoxLog.Text.Length - 1).Y <=
                             textBoxLog.Height;
            }
            var logger = _configManager.GetSelectedLogger();
            if (textBoxLog.TextLength == 0)
            {
                if (logger.WillTruncate)
                    LogErrorToUi(string.Format(Resources.Logger_DisplayLog_____Log_truncated_____Full_log_is_in__0_, _skylineBatchLogger.LogFile));
            }
            var logChanged = logger.OutputLog(LogToUi, LogErrorToUi);
            if (logChanged)
                TrimDisplayedLog();
            if (logChanged && _scrolling)
                ScrollToLogEnd();
            _outputLog.Tick += OutputLog;
        }

        private void TrimDisplayedLog()
        {
            var numLines = textBoxLog.Lines.Length;
            const int buffer = Logger.MaxLogLines / 10;
            if (numLines > Logger.MaxLogLines + buffer)
            {
                textBoxLog.Text = string.Empty;
                _configManager.GetSelectedLogger().DisplayLogFromFile();
                ScrollToLogEnd();
            }
        }

        private void ScrollToLogEnd()
        {
            _scrolling = true;
            textBoxLog.SelectionStart = textBoxLog.TextLength;
            textBoxLog.ScrollToCaret();
        }

        private void LogToUi(string text)
        {
            textBoxLog.AppendText(text);
            textBoxLog.AppendText(Environment.NewLine);
        }

        private void LogErrorToUi(string text)
        {
            textBoxLog.SelectionStart = textBoxLog.TextLength;
            textBoxLog.SelectionLength = 0;
            textBoxLog.SelectionColor = Color.Red;
            textBoxLog.AppendText(text);
            textBoxLog.AppendText(Environment.NewLine);
            textBoxLog.SelectionColor = textBoxLog.ForeColor;
        }

        private async void SwitchLogger()
        {
            _outputLog.Tick -= OutputLog;
            textBoxLog.Clear();

            var logger = _configManager.GetSelectedLogger();
            try
            {
                await Task.Run(() =>
                {
                    // Read the log contents and display in the log tab.
                    logger.DisplayLogFromFile();
                });
            }
            catch (Exception ex)
            {
                DisplayError(ex.Message);
            }

            _scrolling = _configManager.SelectedLog == 0;
            _outputLog.Tick += OutputLog;
        }

        private void tabLog_Enter(object sender, EventArgs e)
        {
            // force the log to be redrawn
            textBoxLog.Invalidate();
        }

        private void tabLog_Leave(object sender, EventArgs e)
        {
            _scrolling = _configManager.SelectedLog == 0;
        }

        private void comboLogList_SelectedIndexChanged(object sender, EventArgs e)
        {
            _configManager.SelectLog(comboLogList.SelectedIndex);
            SwitchLogger();
        }

        private void btnDeleteLogs_Click(object sender, EventArgs e)
        {
            var manageLogsForm = new LogForm(_configManager);
            manageLogsForm.ShowDialog();
        }

        private void btnOpenFolder_Click(object sender, EventArgs e)
        {
            var logger = _configManager.GetSelectedLogger();
            var arg = "/select, \"" + logger.LogFile + "\"";
            Process.Start("explorer.exe", arg);
        }
        
        #endregion

        #region Mainform event handlers and errors

        private void MainForm_Resize(object sender, EventArgs e)
        {
            ListViewSizeChanged();
        }

        private void tabConfigs_Enter(object sender, EventArgs e)
        {
            // only toggle paint event when switching to main tab
            tabConfigs.Paint += tabConfigs_Paint;
        }

        private void tabConfigs_Paint(object sender, PaintEventArgs e)
        {
            ListViewSizeChanged();
            tabConfigs.Paint -= tabConfigs_Paint;
        }

        private void ListViewSizeChanged()
        {
            listViewConfigs.ColumnWidthChanged -= listViewConfigs_ColumnWidthChanged;
            _listViewColumnWidths.ListViewContainerResize();
            listViewConfigs.ColumnWidthChanged += listViewConfigs_ColumnWidthChanged;
        }

        private void listViewConfigs_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            listViewConfigs.ColumnWidthChanged -= listViewConfigs_ColumnWidthChanged;
            _listViewColumnWidths.WidthsChangedByUser();
            listViewConfigs.ColumnWidthChanged += listViewConfigs_ColumnWidthChanged;
        }
        private void systray_icon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            systray_icon.Visible = false;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _configManager.Close();
        }

        public void DisplayError(string message)
        {
            RunUi(() => { AlertDlg.ShowError(this, Program.AppName(), message); });
        }

        public void DisplayWarning(string message)
        {
            RunUi(() => { AlertDlg.ShowWarning(this, Program.AppName(), message); });
        }

        public void DisplayInfo(string message)
        {
            RunUi(() => { AlertDlg.ShowInfo(this, Program.AppName(), message); });
        }

        public void DisplayErrorWithException(string message, Exception exception)
        {
            RunUi(() => { AlertDlg.ShowErrorWithException(this, Program.AppName(), message, exception); });
        }

        public DialogResult DisplayQuestion(string message)
        {
            return AlertDlg.ShowQuestion(this, Program.AppName(), message);
        }

        public DialogResult DisplayLargeOkCancel(string message)
        {
            return AlertDlg.ShowLargeOkCancel(this, Program.AppName(), message);
        }

        public void DisplayForm(Form form)
        {
            RunUi(() =>
            {
                form.ShowDialog(this);
            });
        }

        #endregion

        #region For Tests

        public int ConfigCount()
        {
            return _configManager.GetState().BaseState.ConfigList.Count;
        }

        public int InvalidConfigCount()
        {
            var count = 0;
            var baseState = _configManager.GetState().BaseState;
            foreach (string configName in baseState.ConfigValidation.Keys)
                if (!baseState.ConfigValidation[configName])
                    count++;
            return count;
        }

        public bool ConfigRunning(string name)
        {
            var listViewItems = _configManager.ConfigsListViewItems(listViewConfigs.CreateGraphics());
            foreach (var lvi in listViewItems)
            {
                if (lvi.Text.Equals(name))
                {
                    return lvi.SubItems[2].Text.Equals("Running");
                }
            }
            throw new Exception("Configuration not found");
        }

        public string SelectedConfigName()
        {
            return _configManager.State.BaseState.GetSelectedConfig().GetName();
        }

        public string ConfigName(int index)
        {
            return _configManager.GetConfig(index).GetName();
        }

        public void ClickAdd() => btnNewConfig_Click(new object(), new EventArgs());
        public void ClickEdit() => HandleEditEvent(new object(), new EventArgs());
        public void ClickCopy() => btnCopy_Click(new object(), new EventArgs());
        public void ClickDelete() => btnDelete_Click(new object(), new EventArgs());
        public void ClickUp() => btnUpArrow_Click(new object(), new EventArgs());
        public void ClickDown() => btnDownArrow_Click(new object(), new EventArgs());
        public void ClickShare() => btnExport_Click(new object(), new EventArgs());

        public void ClickRun(int option = 0)
        {
            if (_configManager.State.ConfigRunning() || !btnRunBatch.Enabled)
                throw new Exception("Configurations are still running");
            CheckDropDownOption(option);
            RunBatch();
        }

        public void ClickConfig(int index) => SelectConfig(index);
        public void SetConfigEnabled(int index, bool newValue) => listViewConfigs.SimulateItemCheck(new ItemCheckEventArgs(index, newValue ? CheckState.Checked : CheckState.Unchecked, listViewConfigs.Items[index].Checked ? CheckState.Checked : CheckState.Unchecked));

        public bool IsConfigEnabled(int index) => listViewConfigs.Items[index].Checked;

        public void ClearRemoteFileSources() => _configManager.ClearRemoteFileSources();
        

        #endregion

        private void tabMain_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ControlKey && ModifierKeys == Keys.Control) { }
            else if (e.KeyCode == Keys.Z && ModifierKeys == Keys.Control)
            {
                _configManager.Undo();
            }
            else if (e.KeyCode == Keys.Y && ModifierKeys == Keys.Control)
            {
                _configManager.Redo();
            }
        }

        private void btnUndo_Click(object sender, EventArgs e)
        {
            _configManager.Undo();
        }

        private void btnRedo_Click(object sender, EventArgs e)
        {
            _configManager.Redo();
        }
    }

    // ListView that prevents a double click from toggling checkbox
    class MyListView : ListView
    {
        private bool checkFromDoubleClick;

        public void SimulateItemCheck(ItemCheckEventArgs ice) => OnItemCheck(ice);
        
        protected override void OnItemCheck(ItemCheckEventArgs ice)
        {
            if (this.checkFromDoubleClick)
            {
                ice.NewValue = ice.CurrentValue;
                this.checkFromDoubleClick = false;
            }
            else
                base.OnItemCheck(ice);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // Is this a double-click?
            if ((e.Button == MouseButtons.Left) && (e.Clicks > 1))
            {
                this.checkFromDoubleClick = true;
            }
            base.OnMouseDown(e);
        }
    }
}
