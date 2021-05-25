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
using System.Collections.Generic;
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

            Shown += ((sender, args) =>
            {
                _loaded = true;
                if (Settings.Default.KeepAutoQcRunning)
                    _configManager.RunEnabled();
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
            var configForm = new AutoQcConfigForm(this, (AutoQcConfig)_configManager.GetLastModified(), ConfigAction.Add, false);
            configForm.ShowDialog();
        }

        private void HandleEditEvent(object sender, EventArgs e)
        {
            var configRunner = _configManager.GetSelectedConfigRunner();
            var config = configRunner.Config;
            if (!_configManager.IsSelectedConfigValid())
            {
                if (configRunner.IsRunning()) throw new Exception("Invalid configuration cannot be running.");
                var validateConfigForm = new InvalidConfigSetupForm(config, _configManager, this);
                if (validateConfigForm.ShowDialog() != DialogResult.OK)
                    return;
            }
            var configForm = new AutoQcConfigForm(this, _configManager.GetSelectedConfig(), ConfigAction.Edit, configRunner.IsBusy());
            configForm.ShowDialog();
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            var configForm = new AutoQcConfigForm(this, _configManager.GetSelectedConfig(), ConfigAction.Copy, false);
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
            UpdateUiLogFiles();
        }

        public void ReplaceSelectedConfig(IConfig config)
        {
            _configManager.ReplaceSelectedConfig(config);
            UpdateUiConfigurations();
            UpdateUiLogFiles();
        }

        public void ReplaceAllSkylineVersions(SkylineSettings skylineSettings)
        {
            try
            {
                skylineSettings.Validate();
            }
            catch (ArgumentException)
            {
                // Only ask to replace Skyline settings if new settings are valid
                return;
            }
            if (DialogResult.Yes ==
                DisplayQuestion("Do you want to use this Skyline version for all configurations?"))
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
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            _configManager.UserRemoveSelected();
            UpdateUiConfigurations();
            ListViewSizeChanged();
            UpdateUiLogFiles();
        }

        public void FileOpened(string filePath)
        {
            var importConfigs = false;
            var inDownloadsFolder = filePath.Contains(FileUtil.DOWNLOADS_FOLDER);
            if (!inDownloadsFolder) // Only show dialog if configs are not in downloads folder
            {
                importConfigs = DialogResult.Yes == DisplayQuestion(string.Format(
                    Resources.MainForm_FileOpened_Do_you_want_to_import_configurations_from__0__,
                    Path.GetFileName(filePath)));
            }
            if (importConfigs || inDownloadsFolder)
                DoImport(filePath);
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = TextUtil.FILTER_QCFG;
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            DoImport(dialog.FileName);
        }

        public void DoImport(string filePath)
        {
            _configManager.Import(filePath, ShowDownloadedFileForm);
            UpdateUiConfigurations();
            UpdateUiLogFiles();
        }

        public DialogResult ShowDownloadedFileForm(string filePath, out string newRootDirectory)
        {
            var fileOpenedForm = new FileOpenedForm(this, filePath, Program.Icon());
            var dialogResult = fileOpenedForm.ShowDialog(this);
            newRootDirectory = fileOpenedForm.NewRootDirectory;
            return dialogResult;
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            var shareForm = new ShareConfigsForm(this, _configManager, TextUtil.FILTER_QCFG, Program.Icon());
            shareForm.ShowDialog();
        }

        private void btnRun_MouseClick(object sender, MouseEventArgs e)
        {
            _configManager.UpdateSelectedEnabled(true);
            UpdateUiConfigurations();
        }

        private void btnStop_MouseClick(object sender, MouseEventArgs e)
        {
            _configManager.UpdateSelectedEnabled(false);
            UpdateUiConfigurations();
        }

        private void listViewConfigs_PreventItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            // Disable automatic item selection - selected configuration set through _configManager
            //      Automatic selection disables red text, can't see invalid configurations
            listViewConfigs.SelectedIndices.Clear();
        }

        private void listViewConfigs_MouseDown(object sender, MouseEventArgs e)
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
            var config = _configManager.GetSelectedConfig();
            if (MainFormUtils.CanOpen(config.Name, _configManager.IsSelectedConfigValid(),
                config.MainSettings.SkylineFilePath, Resources.MainForm_btnOpenResults_Click_Skyline_file, this))
            {
                SkylineInstallations.OpenSkylineFile(config.MainSettings.SkylineFilePath, config.SkylineSettings);
            }
        }

        private void btnOpenPanoramaFolder_Click(object sender, EventArgs e)
        {
            var config = _configManager.GetSelectedConfig();
            if (MainFormUtils.CanOpen(config.Name, _configManager.IsSelectedConfigValid(), 
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
            var config = _configManager.GetSelectedConfig();
            MainFormUtils.OpenFileExplorer(config.Name, _configManager.IsSelectedConfigValid(),
                config.MainSettings.FolderToWatch,
                Resources.MainForm_toolStripFolderToWatch_Click_folder_to_watch, this);
        }

        private void toolStripLogFolder_Click(object sender, EventArgs e)
        {
            var config = _configManager.GetSelectedConfig();
            var logger = _configManager.GetLogger(config.Name);
            MainFormUtils.OpenFileExplorer(config.Name, _configManager.IsSelectedConfigValid(),
                logger.GetDirectory(), 
                Resources.MainForm_toolStripLogFolder_Click_log_folder, this);
        }

        #endregion

        #region Update UI

        public void UpdateUiConfigurations()
        {
            RunUi(() =>
            {
                var topItemIndex = listViewConfigs.TopItem != null ? listViewConfigs.TopItem.Index : -1;
                var listViewItems = _configManager.ConfigsListViewItems(listViewConfigs.CreateGraphics());
                listViewConfigs.Items.Clear();
                foreach (var lvi in listViewItems)
                    listViewConfigs.Items.Add(lvi);
                if (topItemIndex != -1 && listViewConfigs.Items.Count > topItemIndex)
                    listViewConfigs.TopItem = listViewConfigs.Items[topItemIndex];
                UpdateLabelVisibility();
                UpdateButtonsEnabled();
            });
        }

        public void UpdateButtonsEnabled()
        {
            RunUi(() =>
            {
                var configSelected = _configManager.HasSelectedConfig();
                var config = configSelected ? _configManager.GetSelectedConfig() : null;
                btnDelete.Enabled = configSelected;
                btnOpenResults.Enabled = configSelected;
                btnOpenPanoramaFolder.Enabled = configSelected && config.PanoramaSettings.PublishToPanorama;
                btnOpenFolder.Enabled = configSelected;

                btnEdit.Enabled = configSelected;
                btnCopy.Enabled = configSelected;
                btnViewLog.Enabled = configSelected;

                var canStart = configSelected && _configManager.GetSelectedConfigRunner().CanStart();
                var canStop = configSelected && _configManager.GetSelectedConfigRunner().CanStop();
                UpdateRunningButtons(canStart, canStop);
            });
        }

        public void UpdateRunningButtons(bool canStart, bool canStop)
        {
            btnRun.Enabled = canStart;
            btnStop.Enabled = canStop;
        }

        public void UpdateUiLogFiles()
        {
            RunUi(() =>
            {
                comboConfigs.Items.Clear();
                comboConfigs.Items.AddRange(_configManager.GetLogList());
                comboConfigs.SelectedIndex = _configManager.SelectedLog;
            });
        }

        private void UpdateLabelVisibility()
        {
            lblNoConfigs.Hide();
            if (!_configManager.HasConfigs())
            {
                lblNoConfigs.Show();
            }
        }

        #endregion
        
        #region Logging

        private void btnViewLog_Click(object sender, EventArgs e)
        {
            if (_configManager.HasSelectedConfig())
            {
                _configManager.SelectLogOfSelectedConfig();
                UpdateUiLogFiles();
                SwitchLogger();
            }
            tabMain.SelectTab(tabLog);
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
                foreach (var line in lines)
                {
                    textBoxLog.AppendText(line);
                    textBoxLog.AppendText(Environment.NewLine);
                }
            });
        }

        public void LogErrorLinesToUi(string name, List<string> lines)
        {
            RunUi(() =>
            {
                if (!_configManager.LoggerIsDisplayed(name))
                    return;
                var selectionStart = textBoxLog.SelectionStart;
                foreach (var line in lines)
                {
                    textBoxLog.AppendText(line);
                    textBoxLog.AppendText(Environment.NewLine);
                }
                textBoxLog.Select(selectionStart, textBoxLog.TextLength);
                textBoxLog.SelectionColor = Color.Red;
            });
        }

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
            RunUi(() => { AlertDlg.ShowError(this, Program.AppName, message); });
        }

        public void DisplayWarning(string message)
        {
            RunUi(() => { AlertDlg.ShowWarning(this, Program.AppName, message); });
        }

        public void DisplayInfo(string message)
        {
            RunUi(() => { AlertDlg.ShowInfo(this, Program.AppName, message); });
        }

        public void DisplayErrorWithException(string message, Exception exception)
        {
            RunUi(() => { AlertDlg.ShowErrorWithException(this, Program.AppName, message, exception); });
        }

        public DialogResult DisplayQuestion(string message)
        {
            return AlertDlg.ShowQuestion(this, Program.AppName, message);
        }

        public DialogResult DisplayLargeOkCancel(string message)
        {
            return AlertDlg.ShowLargeOkCancel(this, Program.AppName, message);
        }

        #endregion
    }

    class MyListView : ListView
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x203)
            {
                // override double click behavior - default changes checkbox checked value
                OnMouseDoubleClick(new MouseEventArgs(new MouseButtons(), 2, MousePosition.X, MousePosition.Y, 0));
                return;
            }

            base.WndProc(ref m);
        }
    }

}
