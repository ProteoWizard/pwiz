/*
 * Original author: Vagisha Sharma <vsharma .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2015 University of Washington - Seattle, WA
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
using AutoQC.Properties;
using SharedBatch;

namespace AutoQC
{
    public partial class MainForm : Form, IMainUiControl
    {

        private readonly AutoQcConfigManager _configManager;

        // Flag that gets set to true in the "Shown" event handler. 
        // ItemCheck and ItemChecked events on the listview are ignored until then.
        private bool _loaded;
        private readonly ColumnWidthCalculator _listViewColumnWidths;
        private Timer _outputLog;

        public MainForm(string openFile)
        {
            InitializeComponent();
            
            toolStrip.Items.Insert(1,new ToolStripSeparator());
            _listViewColumnWidths = new ColumnWidthCalculator(listViewConfigs);
            listViewConfigs.ColumnWidthChanged += listViewConfigs_ColumnWidthChanged;

            ProgramLog.Info(Resources.MainForm_MainForm_Loading_configurations_from_saved_settings_);
            _configManager = new AutoQcConfigManager(this);

            UpdateUiConfigurations();
            ListViewSizeChanged();
            UpdateUiLogFiles();
            UpdateSettingsTab();

            _outputLog = new Timer { Interval = 500 };
            _outputLog.Tick += OutputLog;
            _outputLog.Start();

            Shown += ((sender, args) =>
            {
                _loaded = true;
                UpdateUiConfigurations();
                if (Settings.Default.KeepAutoQcRunning)
                    _configManager.RunEnabled();
                _configManager.DoServerValidation();
                if (!string.IsNullOrEmpty(openFile))
                    FileOpened(openFile);
            });

        }

        private void RunUi(Action action)
        {
            if (!_loaded)
            {
                action();
                return;
            }

            try
            {
                Invoke(action);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        #region Configuration list

        private void btnAdd_Click(object sender, EventArgs e)
        {
            ProgramLog.Info("Creating a new configuration");
            var initialState = _configManager.AutoQcState;
            var configForm = new AutoQcConfigForm(this, (AutoQcConfig)initialState.BaseState.GetLastModified(), ConfigAction.Add, initialState.Copy());
            if (DialogResult.OK == configForm.ShowDialog())
                _configManager.SetState(initialState, configForm.State);
        }

        private void HandleEditEvent(object sender, EventArgs e)
        {
            var initialState = _configManager.AutoQcState;
            var configRunner = initialState.GetSelectedConfigRunner();
            var config = configRunner.Config;
            if (!initialState.BaseState.IsSelectedConfigValid())
            {
                if (configRunner.IsRunning())
                {
                    // This should not happen.  But we will display an error instead of throwing an exception 
                    DisplayError(Resources.MainForm_HandleEditEvent_Configuration___0___is_invalid__It_cannot_be_running___Please_stop_the_configuration_and_fix_the_errors_, config.Name);
                    return;
                }
                var validateConfigForm = new InvalidConfigSetupForm(config, _configManager, this, initialState.Copy());
                if (validateConfigForm.ShowDialog() != DialogResult.OK)
                    return;
                _configManager.SetState(initialState, validateConfigForm.State);
                initialState = validateConfigForm.State;
            }
            var configForm = new AutoQcConfigForm(this, initialState.GetSelectedConfig(), ConfigAction.Edit, initialState.Copy(), initialState.GetSelectedConfigRunner().GetStatus());
            configForm.ShowDialog();
            if (configForm.DialogResult == DialogResult.OK)
                _configManager.SetState(initialState, configForm.State);
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            var initialState = _configManager.AutoQcState;
            var configForm = new AutoQcConfigForm(this, initialState.GetSelectedConfig(), ConfigAction.Copy, initialState.Copy());
            configForm.ShowDialog();
            if (configForm.DialogResult == DialogResult.OK)
                _configManager.SetState(initialState, configForm.State);
        }
        
        public void SetConfigInvalid(IConfig config)
        {
            _configManager.SetConfigInvalid(config);
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            var initialState = _configManager.AutoQcState;
            var selectedConfig = initialState.GetSelectedConfig();
            if (selectedConfig == null)
            {
                return;
            }
            if (DialogResult.Yes == DisplayQuestion(string.Format(
                Resources.MainForm_btnDelete_Click_Are_you_sure_you_want_to_delete_the_configuration___0___,
                selectedConfig.Name)))
            {
                var state = initialState.Copy().UserRemoveSelected(this, out bool removed);
                if (removed)
                {
                    if (_configManager.SetState(initialState, state))
                    {
                        ListViewSizeChanged();
                        UpdateUiLogFiles();
                    }
                }
            }
        }

        public void FileOpened(string filePath)
        {
            var importConfigs = true;
            if (!filePath.Contains(FileUtil.DOWNLOADS_FOLDER)) // Only show dialog if configs are not in downloads folder
            {
                importConfigs = DialogResult.Yes == DisplayQuestion(string.Format(
                    Resources.MainForm_FileOpened_Do_you_want_to_import_configurations_from__0__,
                    Path.GetFileName(filePath)));
            }

            if (!importConfigs) return;
            ProgramLog.Info($"Importing configurations from {filePath}");
            DoImport(filePath);
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = TextUtil.FILTER_QCFG;
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            DoImport(dialog.FileName, true);
        }

        public void DoImport(string filePath, bool setConfigsDisabled = false)
        {
            // AutoQC Loader .qcfg files can be imported from the Downloads folder. Pass null for showDownloadedFileForm
            // so that we don't see the dialog to "...specify a root folder for the configurations".
            _configManager.Import(filePath, setConfigsDisabled);
            UpdateUiConfigurations();
            UpdateUiLogFiles();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            var state = _configManager.AutoQcState;
            var shareForm = new ShareConfigsForm(this, state.BaseState, Program.Icon());
            if (shareForm.ShowDialog(this) != DialogResult.OK)
                return;
            var dialog = new SaveFileDialog {Filter = TextUtil.FILTER_QCFG};
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            state.BaseState.ExportConfigs(dialog.FileName, Settings.Default.XmlVersion, shareForm.IndiciesToSave);
        }

        private void listViewConfigs_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (!_loaded || e.NewValue == e.CurrentValue) return;
            var newIsEnabled = e.NewValue == CheckState.Checked;
            e.NewValue = e.CurrentValue;
            if (_configManager.UpdateSelectedEnabled(newIsEnabled))
                UpdateUiConfigurations();
        }

        public void DisableConfig(IConfig iconfig, RunnerStatus runnerStatus = RunnerStatus.Stopped)
        {
            var initialState = _configManager.AutoQcState;

            var state = initialState.Copy().DisableConfig(initialState.BaseState.GetConfigIndex(iconfig.GetName()), this);
            if (runnerStatus != RunnerStatus.Stopped && state.ConfigRunners.TryGetValue(iconfig.GetName(), out var configRunner))
            {
                ((ConfigRunner)configRunner).ChangeStatus(runnerStatus, false);
            }
            _configManager.SetState(initialState, state,
                false); // Set updatedLogFiles to false.  We do not need to update the selected log file when a config is disabled.
                        // Setting updatedLogFiles is set to true causes a UI freeze, because:
                        // This method is called from a worker thread that will acquire a lock in SetState().
                        // Calling MainForm.UpdateUiLogFiles() in SetState() will change the selected index in the logs combobox on the Main thread
                        // triggering the event comboConfigs_SelectedIndexChanged. This will call AutoQcConfigManager.SelectLog() on the Main thread
                        // and will attempt to acquire the same lock, causing a deadlock.
        }

        private void listViewConfigs_PreventItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            // Disable automatic item selection - selected configuration set through _configManager
            //      Automatic selection disables red text, can't see invalid configurations
            listViewConfigs.SelectedIndices.Clear();
        }

        private void listViewConfigs_MouseUp(object sender, MouseEventArgs e)
        {
            // Select configuration through _configManager
            var item = listViewConfigs.GetItemAt(e.X, e.Y);
            if (item == null)
            {
                _configManager.DeselectConfig();
            }
            else
            {
                _configManager.SelectConfig(item.Index);
            }
        }

        private void listViewConfigs_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            _configManager.SortByValue(e.Column);
            UpdateUiConfigurations();
        }

        #endregion

        #region Open File/Folder
        

        private void btnOpenResults_Click(object sender, EventArgs e)
        {
            var config = _configManager.AutoQcState.GetSelectedConfig();
            if (config != null && File.Exists(config.MainSettings.SkylineFilePath))
            {
                SkylineInstallations.OpenSkylineFile(config.MainSettings.SkylineFilePath, config.SkylineSettings);
            }
            else
            {
                DisplayError(config == null ? "No configuration is selected." : 
                    $"Skyline file \"{config.MainSettings.SkylineFilePath}\" for configuration \"{config.Name}\" does not exist. Please fix the configuration.");
            }
        }

        private void btnOpenPanoramaFolder_Click(object sender, EventArgs e)
        {
            var config = _configManager.AutoQcState.GetSelectedConfig();
            if (MainFormUtils.CanOpen(config.Name, _configManager.AutoQcState.BaseState.IsSelectedConfigValid(), 
                string.Empty, Resources.MainForm_btnOpenPanoramaFolder_Click_Panorama_folder, this))
            {
                var uri = new Uri(config.PanoramaSettings.PanoramaServerUri + config.PanoramaSettings.PanoramaFolder);
                Process.Start(uri.AbsoluteUri);
            }
        }

        private void btnOpenFolder_Click(object sender, EventArgs e)
        {
            openFolderMenuStrip.Show(toolStrip, new Point(0, btnOpenFolder.Height * toolStrip.Items.Count));
        }

        private void toolStripFolderToWatch_Click(object sender, EventArgs e)
        {
            var config = _configManager.AutoQcState.GetSelectedConfig();
            MainFormUtils.OpenFileExplorer(config.Name, _configManager.AutoQcState.BaseState.IsSelectedConfigValid(),
                config.MainSettings.FolderToWatch,
                Resources.MainForm_toolStripFolderToWatch_Click_folder_to_watch, this);
        }

        private void toolStripLogFolder_Click(object sender, EventArgs e)
        {
            var config = _configManager.AutoQcState.GetSelectedConfig();
            var logger = _configManager.AutoQcState.GetLogger(config.Name);
            MainFormUtils.OpenFileExplorer(config.Name, _configManager.AutoQcState.BaseState.IsSelectedConfigValid(),
                logger.LogDirectory, 
                Resources.MainForm_toolStripLogFolder_Click_log_folder, this);
        }

        #endregion

        #region Update UI

        public void UpdateUiConfigurations()
        {
            if (!_loaded) return;
            var listViewItems = _configManager.ConfigsListViewItems(listViewConfigs.CreateGraphics());
            RunUi(() =>
            {
                var topItemIndex = listViewConfigs.TopItem != null ? listViewConfigs.TopItem.Index : -1;
                listViewConfigs.Items.Clear();
                listViewConfigs.ItemCheck -= listViewConfigs_ItemCheck;
                foreach (var lvi in listViewItems)
                    listViewConfigs.Items.Add(lvi);
                listViewConfigs.ItemCheck += listViewConfigs_ItemCheck;
                if (topItemIndex != -1 && listViewConfigs.Items.Count > topItemIndex)
                    listViewConfigs.TopItem = listViewConfigs.Items[topItemIndex];
            });
            UpdateLabelVisibility();
            UpdateButtonsEnabled();
        }

        public void UpdateButtonsEnabled()
        {
            var configSelected = _configManager.AutoQcState.BaseState.HasSelectedConfig();
            var config = configSelected ? _configManager.AutoQcState.GetSelectedConfig() : null;
            RunUi(() =>
            {
                btnDelete.Enabled = configSelected;
                btnOpenResults.Enabled = configSelected;
                btnOpenPanoramaFolder.Enabled = configSelected && config != null && config.PanoramaSettings.PublishToPanorama;
                btnOpenFolder.Enabled = configSelected;
                

                btnEdit.Enabled = configSelected;
                btnCopy.Enabled = configSelected;
                btnViewLog.Enabled = configSelected;
            });
        }

        public void UpdateUiLogFiles()
        {
            if (!_loaded)
                return;
            RunUi(() =>
            {
                comboConfigs.Items.Clear();
                comboConfigs.Items.AddRange(_configManager.GetLogNameList());
                comboConfigs.SelectedIndex = _configManager.SelectedLog;
            });
        }

        private void UpdateLabelVisibility()
        {
            var hasConfigs = _configManager.AutoQcState.BaseState.HasConfigs();
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

            if (_configManager.SelectedLog < 0)
            {
                textBoxLog.Clear();
            }
            else
            {
                var logger = _configManager.GetSelectedLogger();
                if (textBoxLog.TextLength == 0)
                {
                    if (logger.WillTruncate)
                        LogErrorToUi(string.Format(Resources.AutoQcLogger_LogTruncatedMessage_____Log_truncated__Full_log_is_in__0_, _configManager.GetSelectedLogger().LogFile));
                }
                var logChanged = logger.OutputLog(LogToUi, LogErrorToUi);
                if (logChanged)
                    TrimDisplayedLog();
                if (logChanged && _scrolling)
                    ScrollToLogEnd();
            }
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

            _scrolling = true;
            _outputLog.Tick += OutputLog;
        }

        private void comboConfigs_SelectedIndexChanged(object sender, EventArgs e)
        {
            _configManager.SelectLog(comboConfigs.SelectedIndex);
            if (_configManager.SelectedLog >= 0)
                btnOpenLogFolder.Enabled = true;
            SwitchLogger();
        }
        
        private void btnOpenLogFolder_Click(object sender, EventArgs e)
        {
            var logger = _configManager.GetSelectedLogger();
            if (!File.Exists(logger.LogFile))
            {
                var logFolder = Path.GetDirectoryName(logger.LogFile);
                if (!Directory.Exists(logFolder))
                {
                    DisplayError(string.Format(Resources.MainForm_btnOpenFolder_Click_File_location_does_not_exist___0_, logger.LogFile));
                    return;
                }
                Process.Start(logFolder);
                return;
            }

            var arg = "/select, \"" + logger.LogFile + "\"";
            Process.Start("explorer.exe", arg);
        }

        private void btnViewLog_Click(object sender, EventArgs e)
        {
            tabMain.SelectTab(tabLog); // Select the log tab first
            // Set the focus on the combobox.
            // If the focus is on the textbox we will see a lot of scrolling for big log files.
            comboConfigs.Focus();
            var config = _configManager.AutoQcState.GetSelectedConfig();
            // Switch the displayed log only if it is for a different configuration than the one already displayed
            if (config != null && !string.Equals(config.Name, _configManager.GetSelectedLogger()?.Name))
            {
                _configManager.SelectLogOfSelectedConfig();
                UpdateUiLogFiles();
                // SwitchLogger(); // We don't need this. UpdateUiLogFiles will change the selected index in the log combobox which will end up calling SwitchLogger.
            }
        }

        private void tabLog_Enter(object sender, EventArgs e)
        {
            ScrollToLogEnd();
        }



        /*

        private void btnViewLog_Click(object sender, EventArgs e)
        {
            tabMain.SelectTab(tabLog); // Select the log tab first
            // Set the focus on the combobox.
            // If the focus is on the textbox we will see a lot of scrolling for big log files.
            comboConfigs.Focus();
            if (_configManager.HasSelectedConfig())
            {
                _configManager.SelectLogOfSelectedConfig();
                UpdateUiLogFiles();
                // SwitchLogger(); // We don't need this. UpdateUiLogFiles will change the selected index in the log combobox which will end up calling SwitchLogger.
            }
        }

        private void tabLog_Enter(object sender, EventArgs e)
        {
            ScrollToLogEnd(true);
        }

        private void comboConfigs_SelectedIndexChanged(object sender, EventArgs e)
        {
            _configManager.SelectLog(comboConfigs.SelectedIndex);
            if (_configManager.SelectedLog >= 0)
                btnOpenLogFolder.Enabled = true;
            SwitchLogger();
        }

        private async void SwitchLogger()
        {
            textBoxLog.Clear();
            
            var logger = _configManager.GetSelectedLogger();
            try
            {
                await Task.Run(() =>
                {
                    // Read the log contents and display in the log tab.
                    logger.DisplayLog();
                });
            }
            catch (Exception ex)
            {
                DisplayErrorWithException(Resources.MainForm_SwitchLogger_Error_reading_log_ + Environment.NewLine + ex.Message, ex);
            }

            ScrollToLogEnd(true);
        }

        private void ScrollToLogEnd(bool forceScroll = false)
        {
            // Only scroll to end if forced or user is already scrolled to bottom of log
            if (forceScroll || textBoxLog.GetPositionFromCharIndex(textBoxLog.Text.Length - 1).Y <= textBoxLog.Height)
            {
                textBoxLog.SelectionStart = textBoxLog.Text.Length;
                textBoxLog.ScrollToCaret();
            }
        }

        private void btnOpenLogFolder_Click(object sender, EventArgs e)
        {
            var logger = _configManager.GetSelectedLogger();
            if (!File.Exists(logger.GetFile()))
            {
                if (!Directory.Exists(logger.GetDirectory()))
                {
                    DisplayError(string.Format(Resources.MainForm_btnOpenFolder_Click_File_location_does_not_exist___0_, logger.GetFile()));
                    return;
                }
                Process.Start(logger.GetDirectory());
                return;
            }

            var arg = "/select, \"" + logger.GetFile() + "\"";
            Process.Start("explorer.exe", arg);
        }

        public void LogToUi(string name, string text, bool trim)
        {
            RunUi(() =>
            {
                if (!_configManager.LoggerIsDisplayed(name))
                    return;
                if (trim)
                {
                    TrimDisplayedLog();
                }
                
                textBoxLog.AppendText(text);
                textBoxLog.AppendText(Environment.NewLine);
                
                ScrollToLogEnd();
            });
        }

        private void TrimDisplayedLog()
        {
            var numLines = textBoxLog.Lines.Length;
            const int buffer = Logger.MaxLogLines / 10;
            if (numLines > Logger.MaxLogLines + buffer)
            {
                var unTruncated = textBoxLog.Text;
                var startIndex = textBoxLog.GetFirstCharIndexFromLine(numLines - Logger.MaxLogLines);
                var message = (_configManager.GetSelectedLogger() != null)
                    ? string.Format(Logger.LogTruncatedMessage, _configManager.GetSelectedLogger().GetFile())
                    : Resources.MainForm_ViewLog_Log_Truncated;
                message += Environment.NewLine;
                textBoxLog.Text = message + unTruncated.Substring(startIndex);
                textBoxLog.SelectionStart = 0;
                textBoxLog.SelectionLength = message.Length;
                textBoxLog.SelectionColor = Color.Red;
            }
        }

        public void LogErrorToUi(string name, string text, bool trim)
        {
            RunUi(() =>
            {
                if (!_configManager.LoggerIsDisplayed(name))
                    return;
                if (trim)
                {
                    TrimDisplayedLog();
                }

                textBoxLog.SelectionStart = textBoxLog.TextLength;
                textBoxLog.SelectionLength = 0;
                textBoxLog.SelectionColor = Color.Red;
                LogToUi(name, text,
                    false); // Already trimmed
                textBoxLog.SelectionColor = textBoxLog.ForeColor;
            });
        }

        public void LogLinesToUi(string name, List<string> lines)
        {
            RunUi(() =>
            {
                if (!_configManager.LoggerIsDisplayed(name))
                    return;
                var text = TextUtil.LineSeparate(lines);
                textBoxLog.AppendText(text + Environment.NewLine);
            });
        }

        public void LogErrorLinesToUi(string name, List<string> lines)
        {
            RunUi(() =>
            {
                if (!_configManager.LoggerIsDisplayed(name))
                    return;
                var selectionStart = textBoxLog.SelectionStart;
                var text = TextUtil.LineSeparate(lines);
                textBoxLog.AppendText(text + Environment.NewLine);
                textBoxLog.Select(selectionStart, textBoxLog.TextLength);
                textBoxLog.SelectionColor = Color.Red;
            });
        }

        public void ClearLog()
        {
            textBoxLog.Clear();
        }*/

        #endregion

        #region Settings Tab

        private void UpdateSettingsTab()
        {
            cb_minimizeToSysTray.Checked = Settings.Default.MinimizeToSystemTray;
            cb_keepRunning.Checked = Settings.Default.KeepAutoQcRunning;

            cb_minimizeToSysTray.CheckedChanged += cb_minimizeToSysTray_CheckedChanged;
            cb_keepRunning.CheckedChanged += cb_keepRunning_CheckedChanged;

            
        }

        private void cb_keepRunning_CheckedChanged(object sender, EventArgs e)
        {
            cb_keepRunning.Enabled = false;
            var enable = cb_keepRunning.Checked;
            try
            {
                _configManager.ChangeKeepRunningState(enable);
            }
            catch (Exception ex)
            {
                var err = enable ? string.Format(Resources.MainForm_cb_keepRunning_CheckedChanged_Error_enabling__Keep__0__running_, Program.AppName)
                    : string.Format(Resources.MainForm_cb_keepRunning_CheckedChanged_Error_disabling__Keep__0__running_, Program.AppName);
                // ReSharper disable once LocalizableElement
                ProgramLog.Error($"Error {(enable ? "enabling" : "disabling")} \"Keep AutoQC Loader running\"", ex);

                DisplayErrorWithException(TextUtil.LineSeparate(
                        $"{err},{ex.Message},{(ex.InnerException != null ? ex.InnerException.StackTrace : ex.StackTrace)}"),
                    ex);

                cb_keepRunning.CheckedChanged -= cb_keepRunning_CheckedChanged;
                cb_keepRunning.Checked = !enable;
                cb_keepRunning.CheckedChanged += cb_keepRunning_CheckedChanged;
                cb_keepRunning.Enabled = true;
                return;
            }

            Settings.Default.KeepAutoQcRunning = cb_keepRunning.Checked;
            Settings.Default.Save();
            cb_keepRunning.Enabled = true;
        }

        private void cb_minimizeToSysTray_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.MinimizeToSystemTray = cb_minimizeToSysTray.Checked;
            Settings.Default.Save();
        }

        #endregion

        #region Form event handlers and errors

        private void MainForm_Resize(object sender, EventArgs e)
        {
            ListViewSizeChanged();

            //If the form is minimized hide it from the task bar  
            //and show the system tray icon (represented by the NotifyIcon control)  
            if (WindowState == FormWindowState.Minimized && Settings.Default.MinimizeToSystemTray)
            {
                Hide();
                systray_icon.Visible = true;
            }
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
            if (_listViewColumnWidths != null)
            {
                listViewConfigs.ColumnWidthChanged -= listViewConfigs_ColumnWidthChanged;
                _listViewColumnWidths.ListViewContainerResize();
                listViewConfigs.ColumnWidthChanged += listViewConfigs_ColumnWidthChanged;
            }
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
            RunUi(() => { AlertDlg.ShowError(this, message); });
        }

        public void DisplayError(string message, params object[] args)
        {
            RunUi(() => { AlertDlg.ShowError(this, string.Format(message, args)); });
        }

        public void DisplayWarning(string message)
        {
            RunUi(() => { AlertDlg.ShowWarning(this, message); });
        }

        public void DisplayInfo(string message)
        {
            RunUi(() => { AlertDlg.ShowInfo(this, message); });
        }

        public void DisplayErrorWithException(string message, Exception exception)
        {
            RunUi(() => { AlertDlg.ShowErrorWithException(this, message, exception); });
        }

        public DialogResult DisplayQuestion(string message)
        {
            return AlertDlg.ShowQuestion(this, message);
        }

        public DialogResult DisplayLargeOkCancel(string message)
        {
            return AlertDlg.ShowLargeOkCancel(this, message);
        }

        public void UpdateRunningButtons(bool canStart, bool canStop)
        {
            // Implements interface member, AutoQC does not use running buttons
        }

        public void DisplayForm(Form form)
        {
            RunUi(() =>
            {
                form.ShowDialog(this);
            });
        }

        #endregion

        #region Methods used for tests
        public int ConfigCount()
        {
            return _configManager.GetAutoQcState().BaseState.ConfigList.Count;
        }
        public void ClickAdd() => btnAdd_Click(new object(), new EventArgs());
        public void ClickEdit() => HandleEditEvent(new object(), new EventArgs());

        public void SelectLogTab()
        {
            tabLog.Select();
        }

        public void SelectConfigsTab()
        {
            tabConfigs.Select();
        }

        public string GetSelectedLogName()
        {
            return comboConfigs.SelectedItem.ToString();
        }

        public void ClickConfig(int index) => SelectConfig(index);

        private void SelectConfig(int index)
        {
            if (index < 0)
            {
                _configManager.DeselectConfig();
                return;
            }
            _configManager.SelectConfig(index);
        }

        public void StartConfig(int index) => listViewConfigs.SimulateItemCheck(new ItemCheckEventArgs(index, CheckState.Checked, listViewConfigs.Items[index].Checked ? CheckState.Checked : CheckState.Unchecked));

        public void StopConfig(int index) => listViewConfigs.SimulateItemCheck(new ItemCheckEventArgs(index, CheckState.Unchecked, listViewConfigs.Items[index].Checked ? CheckState.Checked : CheckState.Unchecked));

        public bool IsConfigEnabled(int index) => listViewConfigs.Items[index].Checked;

        public int GetConfigIndex(string configName)
        {
            return _configManager.AutoQcState.BaseState.GetConfigIndex(configName);
        }

        public AutoQcConfig GetConfig(int configIndex)
        {
            return (AutoQcConfig)_configManager.AutoQcState.BaseState.GetConfig(configIndex);
        }

        public ConfigRunner GetConfigRunner(IConfig config)
        {
            return _configManager.AutoQcState.GetConfigRunner(config);
        }

        public ConfigRunner GetConfigRunner(int configIndex)
        {
            var config = GetConfig(configIndex);
            return config == null ? null : GetConfigRunner(config);
        }

        public string GetLogFilePath(int configIndex)
        {
            var configRunner = GetConfigRunner(configIndex);
            return configRunner?.GetLogger()?.LogFile;
        }

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
