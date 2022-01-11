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
            _skylineBatchLogger = new Logger(logPath, Program.AppName() + TextUtil.EXT_LOG);
            toolStrip1.Items.Insert(3,new ToolStripSeparator());
            _listViewColumnWidths = new ColumnWidthCalculator(listViewConfigs);
            listViewConfigs.ColumnWidthChanged += listViewConfigs_ColumnWidthChanged;
            ProgramLog.Info(Resources.MainForm_MainForm_Loading_configurations_from_saved_settings_);
            
            _outputLog = new Timer { Interval = 500 };
            _outputLog.Tick += OutputLog;
            _outputLog.Start();
            UpdateButtonsEnabled();

            Shown += ((sender, args) =>
            {
                _configManager = new SkylineBatchConfigManager(_skylineBatchLogger, this);
                _configManager.LoadConfigList();
                _rDirectorySelector = new RDirectorySelector(this, _configManager);
                if (!string.IsNullOrEmpty(openFile))
                    FileOpened(openFile);
                _rDirectorySelector = new RDirectorySelector(this, _configManager);
                ListViewSizeChanged();
                UpdateUiLogFiles();
                UpdateRunBatchSteps();
                _loaded = true;
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
                Invoke(action);
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
            var initialConfigValues = (SkylineBatchConfig)_configManager.GetLastModified();
            var configForm = new SkylineBatchConfigForm(this, _rDirectorySelector, initialConfigValues, ConfigAction.Add, false, _configManager);
            configForm.ShowDialog();
        }

        public void AssertUniqueConfigName(string newName, bool replacing)
        {
            _configManager.AssertUniqueName(newName, replacing);
        }

        public void AddConfiguration(IConfig config)
        {
            _configManager.UserAddConfig(config);
            UpdateUiConfigurations();
            ListViewSizeChanged();
        }

        public void ReplaceSelectedConfig(IConfig config)
        {
            _configManager.UserReplaceSelected(config);
            UpdateUiConfigurations();
        }

        private void HandleEditEvent(object sender, EventArgs e)
        {
            var configRunner = _configManager.GetSelectedConfigRunner();
            var config = (SkylineBatchConfig)configRunner.GetConfig();
            if (!_configManager.IsSelectedConfigValid())
            {
                if (configRunner.IsRunning()) throw new Exception("Invalid configuration cannot be running.");
                var validateConfigForm = new InvalidConfigSetupForm(this, config, _configManager, _rDirectorySelector);
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
            }
            var configForm = new SkylineBatchConfigForm(this, _rDirectorySelector, _configManager.GetSelectedConfig(), ConfigAction.Edit, configRunner.IsBusy(), _configManager);
            configForm.ShowDialog();
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            var configForm = new SkylineBatchConfigForm(this, _rDirectorySelector, _configManager.GetSelectedConfig(), ConfigAction.Copy, false, _configManager);
            configForm.ShowDialog();
        }

        public bool? ReplaceAllSkylineVersions(SkylineSettings skylineSettings)
        {
            try
            {
                skylineSettings.Validate();
            }
            catch (ArgumentException)
            {
                // Only ask to replace Skyline settings if new settings are valid
                return null;
            }
            if (DialogResult.Yes ==
                DisplayQuestion(Resources.MainForm_ReplaceAllSkylineVersions_Do_you_want_to_use_this_Skyline_version_for_all_configurations_))
            {
                try
                {
                    _configManager.ReplaceSkylineSettings(skylineSettings);
                }
                catch (ArgumentException e)
                {
                    DisplayError(e.Message);
                }
                UpdateUiConfigurations();
                return true;
            }

            return false;
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
                DisplayError(errorMessage);
                return;
            }
            UpdateUiConfigurations();
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
            var configSelected = _loaded ? _configManager.HasSelectedConfig() : false;
            btnEdit.Enabled = configSelected;
            btnCopy.Enabled = configSelected;
            btnUpArrow.Enabled = configSelected && _configManager.SelectedConfig != 0;
            btnDownArrow.Enabled = configSelected && _configManager.SelectedConfig < listViewConfigs.Items.Count - 1;
            btnDelete.Enabled = configSelected;
            btnOpenAnalysis.Enabled = configSelected;
            btnOpenTemplate.Enabled = configSelected;
            btnOpenResults.Enabled = configSelected;
            btnExportConfigs.Enabled = _loaded ? _configManager.HasConfigs() : false;
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
            var config = _configManager.GetSelectedConfig();
            config.MainSettings.CreateAnalysisFolderIfNonexistent();
            MainFormUtils.OpenFileExplorer(config.Name, _configManager.IsSelectedConfigValid(),
                config.MainSettings.AnalysisFolderPath, Resources.MainForm_btnOpenAnalysis_Click_analysis_folder, this);
        }

        private void btnOpenTemplate_Click(object sender, EventArgs e)
        {
            var config = _configManager.GetSelectedConfig();
            if (MainFormUtils.CanOpen(config.Name, _configManager.IsSelectedConfigValid(), config.MainSettings.TemplateFilePath,
                Resources.MainForm_btnOpenTemplate_Click_Skyline_template_file, this))
            {
                SkylineInstallations.OpenSkylineFile(config.MainSettings.TemplateFilePath, config.SkylineSettings);
            }
        }

        private void btnOpenResults_Click(object sender, EventArgs e)
        {
            var config = _configManager.GetSelectedConfig();
            var resultsFile = config.MainSettings.GetResultsFilePath();

            if (MainFormUtils.CanOpen(config.Name, _configManager.IsSelectedConfigValid(), resultsFile,
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
            _configManager.CancelRunners();
            btnStop.Enabled = false;
            btnLogStop.Enabled = false; 
        }

        #endregion
        
        #region Update UI

        // Reload configurations from configManager
        public void UpdateUiConfigurations()
        {
            RunUi(() =>
            {
                ProgramLog.Info("Updating configurations");
                var topItemIndex = listViewConfigs.TopItem != null ? listViewConfigs.TopItem.Index : -1;
                listViewConfigs.ItemCheck -= listViewConfigs_ItemCheck;
                var listViewItems = _configManager.ConfigsListViewItems(listViewConfigs.CreateGraphics());
                listViewConfigs.Items.Clear();
                foreach (var lvi in listViewItems)
                    listViewConfigs.Items.Add(lvi);
                if (topItemIndex != -1 && listViewConfigs.Items.Count > topItemIndex)
                    listViewConfigs.TopItem = listViewConfigs.Items[topItemIndex];
                listViewConfigs.ItemCheck += listViewConfigs_ItemCheck;
                UpdateLabelVisibility();
                UpdateButtonsEnabled();
                UpdateRunBatchSteps();
            });

        }

        // Reload logs in comboLogList
        public void UpdateUiLogFiles()
        {
            RunUi(() =>
            {
                ProgramLog.Info("Updating log files");
                comboLogList.Items.Clear();
                comboLogList.Items.AddRange(_configManager.GetAllLogFiles());
                comboLogList.SelectedIndex = _configManager.SelectedLog;
                btnDeleteLogs.Enabled = _configManager.HasOldLogs();
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
            if (_configManager.HasConfigs())
            {
                lblNoConfigs.Hide();
            }
            else
            {
                lblNoConfigs.Show();
            }
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
            if (_showRefineStep != _configManager.WillRefine() || !_loaded)
            {
                _showRefineStep = _configManager.WillRefine();
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
            }
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
            var importConfigs = false;
            var inDownloadsFolder = filePath.Contains(FileUtil.DOWNLOADS_FOLDER);
            if (!inDownloadsFolder) // Only show dialog if configs are not in downloads folder
            {
                RunUi(() =>
                {
                    importConfigs = DialogResult.Yes == DisplayQuestion(string.Format(
                        Resources.MainForm_FileOpenedImport_Do_you_want_to_import_configurations_from__0__,
                        Path.GetFileName(filePath)));
                });
            }
            RunUi(() =>
            {
                if (importConfigs || inDownloadsFolder)
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
            UpdateUiConfigurations();
            if (!_rDirectorySelector.ShownDialog)
                _rDirectorySelector.AddIfNecassary();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            var shareForm = new ShareConfigsForm(this, _configManager, TextUtil.FILTER_BCFG, Program.Icon());
            shareForm.ShowDialog();
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
            return listViewConfigs.Items.Count;
        }

        public int InvalidConfigCount()
        {
            var count = 0;
            foreach (ListViewItem lvi in listViewConfigs.Items)
                if (lvi.ForeColor == Color.Red)
                    count++;
            return count;
        }

        public bool ConfigRunning(string name)
        {
            foreach (ListViewItem lvi in listViewConfigs.Items)
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
            return _configManager.GetSelectedConfig().GetName();
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
            CheckDropDownOption(option);
            RunBatch();
        }

        public void ClickConfig(int index) => SelectConfig(index);
        public void SetConfigEnabled(int index, bool newValue) => listViewConfigs.SimulateItemCheck(new ItemCheckEventArgs(index, newValue ? CheckState.Checked : CheckState.Unchecked, listViewConfigs.Items[index].Checked ? CheckState.Checked : CheckState.Unchecked));

        public bool IsConfigEnabled(int index) => listViewConfigs.Items[index].Checked;
        

        #endregion
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
