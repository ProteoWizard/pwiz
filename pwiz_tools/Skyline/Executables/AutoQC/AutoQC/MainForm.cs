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
using System.Web;
using AutoQC.Properties;

namespace AutoQC
{
    public partial class MainForm : Form, IMainUiControl
    {

        private readonly ConfigManager _configManager;

        // Flag that gets set to true in the "Shown" event handler. 
        // ItemCheck and ItemChecked events on the listview are ignored until then.
        private bool _loaded;
        private double[] _listViewColumnWidths;
        private bool _resizing;

        public MainForm()
        {
            InitializeComponent();

            toolStrip.Items.Insert(2,new ToolStripSeparator());
            _listViewColumnWidths = new[]
            {
                (double)columnName.Width/listViewConfigs.Width,
                (double)columnUser.Width/listViewConfigs.Width,
                (double)columnCreated.Width/listViewConfigs.Width,
                (double)columnStatus.Width/listViewConfigs.Width,
            };
            listViewConfigs.ColumnWidthChanged += listViewConfigs_ColumnWidthChanged;

            Program.LogInfo("Loading configurations from saved settings.");
            _configManager = new ConfigManager(this);

            UpdateUiConfigurations();
            UpdateUiLoggers();
            UpdateSettingsTab();

            Shown += ((sender, args) =>
            {
                _loaded = true;
                if (Settings.Default.KeepAutoQcRunning)
                    _configManager.RunEnabled();
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
            Program.LogInfo("Creating a new configuration");
            var configForm = new AutoQcConfigForm(this, null, ConfigAction.Add, false);
            configForm.ShowDialog();
        }

        private void HandleEditEvent(object sender, EventArgs e)
        {
            var configRunner = _configManager.GetSelectedConfigRunner();
            var config = configRunner.Config;
            try
            {
                config.MainSettings.ValidateSettings();
                config.SkylineSettings.Validate();
            }
            catch (ArgumentException)
            {
                if (configRunner.IsRunning()) throw new Exception("Invalid configuration cannot be running.");
                var validateConfigForm = new InvalidConfigSetupForm(config, this);
                validateConfigForm.ShowDialog();
                if (validateConfigForm.DialogResult != DialogResult.OK)
                    return;
                config = validateConfigForm.ValidConfig;
            }
            var configForm = new AutoQcConfigForm(this, config, ConfigAction.Edit, configRunner.IsBusy());
            configForm.ShowDialog();
        }

        public void TryExecuteOperation(ConfigAction operation, AutoQcConfig config)
        {
            var existingIndex = _configManager.GetConfigIndex(config.Name);
            var duplicateName = operation != ConfigAction.Edit && existingIndex >= 0 ||
                        operation == ConfigAction.Edit && existingIndex != _configManager.SelectedConfig;
            if (duplicateName)
            {
                throw new ArgumentException(string.Format("Cannot add \"{0}\" because there is another configuration with the same name.", config.Name) + Environment.NewLine +
                             "Please choose a unique name.");
            }
            config.Validate();
            if (operation == ConfigAction.Edit)
                _configManager.ReplaceSelectedConfig(config);
            else
            {
                _configManager.AddConfiguration(config);
                _configManager.SelectConfig(_configManager.GetConfigIndex(config.Name));
            }
            UpdateUiConfigurations();
            UpdateUiLoggers();
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            var configForm = new AutoQcConfigForm(this, _configManager.GetSelectedConfig(), ConfigAction.Copy, false);
            configForm.ShowDialog();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            _configManager.RemoveSelected();
            UpdateUiConfigurations();
            UpdateUiLoggers();
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = Resources.XML_file_extension;
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var filePath = dialog.FileName;
            _configManager.Import(filePath);
            UpdateUiConfigurations();
            UpdateUiLoggers();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            var shareForm = new ShareConfigsForm(this, _configManager);
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
            if (!_configManager.IsSelectedConfigValid())
            {
                DisplayError("Cannot open the Skyline file of an invalid configuration." + Environment.NewLine +
                             string.Format("Please fix \"{0}\" and try again.", config.Name));
                return;
            }
            // Open skyline file
            var file = config.MainSettings.SkylineFilePath;
            Process.Start(file);
        }

        private void btnOpenPanoramaFolder_Click(object sender, EventArgs e)
        {
            var config = _configManager.GetSelectedConfig();
            if (!_configManager.IsSelectedConfigValid())
            {
                DisplayError("Cannot open the Panorama folder of an invalid configuration." + Environment.NewLine +
                             string.Format("Please fix {0} and try again.", config.Name));
                return;
            }
            
            var uri = new Uri(config.PanoramaSettings.PanoramaServerUri + config.PanoramaSettings.PanoramaFolder);
            var username = HttpUtility.UrlEncode(config.PanoramaSettings.PanoramaUserEmail);
            var password = HttpUtility.UrlEncode(config.PanoramaSettings.PanoramaPassword);

            var uriWithCred = new UriBuilder(uri) { UserName = username, Password = password }.Uri;
            Process.Start(uriWithCred.AbsoluteUri);
        }

        private void btnOpenResultsFolder_Click(object sender, EventArgs e)
        {
            var config = _configManager.GetSelectedConfig();
            if (!_configManager.IsSelectedConfigValid())
            {
                DisplayError("Cannot open the results folder of an invalid configuration." + Environment.NewLine +
                             string.Format("Please fix {0} and try again.", config.Name));
                return;
            }
            var folder = config.MainSettings.FolderToWatch;
            Process.Start("explorer.exe", "/n," + folder);
        }

        #endregion
        
        #region Update UI

        public void UpdateUiConfigurations()
        {
            RunUi(() =>
            {
                Program.LogInfo("Updating configurations");
                listViewConfigs.Items.Clear();
                var listViewItems = _configManager.ConfigsListViewItems();
                foreach (var lvi in listViewItems)
                    listViewConfigs.Items.Add(lvi);
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
                btnOpenResultsFolder.Enabled = configSelected;

                btnEdit.Enabled = configSelected;
                btnCopy.Enabled = configSelected;
                btnViewLog.Enabled = configSelected;

                btnRun.Enabled = configSelected && _configManager.GetSelectedConfigRunner().CanStart();
                btnStop.Enabled = configSelected && _configManager.GetSelectedConfigRunner().CanStop();
            });

        }

        public void UpdateUiLoggers()
        {
            RunUi(() =>
            {
                Program.LogInfo("Updating loggers");
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
                UpdateUiLoggers();
                SwitchLogger();
            }
            tabMain.SelectTab(tabLog);
        }

        private void comboConfigs_SelectedIndexChanged(object sender, EventArgs e)
        {
            _configManager.SelectLog(comboConfigs.SelectedIndex);
            if (_configManager.SelectedLog >= 0)
                btnOpenFolder.Enabled = true;
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
                DisplayErrorWithException(Resources.MainForm_ViewLog_Error_Reading_Log + Environment.NewLine + ex.Message, ex);
            }

            ScrollToLogEnd();
        }

        private void ScrollToLogEnd()
        {
            textBoxLog.SelectionStart = textBoxLog.Text.Length;
            textBoxLog.ScrollToCaret();
        }

        private void btnOpenFolder_Click(object sender, EventArgs e)
        {
            var logger = _configManager.GetSelectedLogger();
            if (!File.Exists(logger.GetFile()))
            {
                if (!Directory.Exists(logger.GetDirectory()))
                {
                    DisplayError(string.Format(Resources.MainForm_btnOpenFolder_Click_Directory_does_not_exist___0_, logger.GetFile()));
                    return;
                }
                Process.Start(logger.GetDirectory());
                return;
            }

            var arg = "/select, \"" + logger.GetFile() + "\"";
            Process.Start("explorer.exe", arg);
        }

        public void LogToUi(string name, string text, bool scrollToEnd, bool trim)
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

                if (!scrollToEnd) return;

                ScrollToLogEnd();
            });
        }

        private void TrimDisplayedLog()
        {
            var numLines = textBoxLog.Lines.Length;
            const int buffer = AutoQcLogger.MAX_LOG_LINES / 10;
            if (numLines > AutoQcLogger.MAX_LOG_LINES + buffer)
            {
                var unTruncated = textBoxLog.Text;
                var startIndex = textBoxLog.GetFirstCharIndexFromLine(numLines - AutoQcLogger.MAX_LOG_LINES);
                var message = (_configManager.GetSelectedLogger() != null)
                    ? string.Format(AutoQcLogger.LogTruncatedMessage, _configManager.GetSelectedLogger().GetFile())
                    : Resources.MainForm_ViewLog_Log_Truncated;
                message += Environment.NewLine;
                textBoxLog.Text = message + unTruncated.Substring(startIndex);
                textBoxLog.SelectionStart = 0;
                textBoxLog.SelectionLength = message.Length;
                textBoxLog.SelectionColor = Color.Red;
            }
        }

        public void LogErrorToUi(string name, string text, bool scrollToEnd, bool trim)
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
                LogToUi(name, text, scrollToEnd,
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
                Program.LogError($"Error {(enable ? "enabling" : "disabling")} \"Keep AutoQC Loader running\"", ex);

                DisplayErrorWithException(TextUtil.LineSeparate(
                        Resources.MainForm_cb_keepRunning_CheckedChanged_Error_Changing_Settings,
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

        private void listViewConfigs_Resize(object sender, EventArgs e)
        {
            ResizeListViewColumns();
        }

        private void ResizeListViewColumns()
        {
            _resizing = true;
            var listViewWidth = listViewConfigs.Width;
            columnName.Width = (int)Math.Floor(listViewWidth * _listViewColumnWidths[0]);
            columnUser.Width = (int)Math.Floor(listViewWidth * _listViewColumnWidths[1]);
            columnCreated.Width = (int)Math.Floor(listViewWidth * _listViewColumnWidths[2]);
            columnStatus.Width = -2;
            _resizing = false;
        }

        private void listViewConfigs_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            if (_resizing) return;
            _listViewColumnWidths = new[]
            {
                (double)columnName.Width/listViewConfigs.Width,
                (double)columnUser.Width/listViewConfigs.Width,
                (double)columnCreated.Width/listViewConfigs.Width,
                1 - (columnName.Width + columnUser.Width + columnCreated.Width) / listViewConfigs.Width,
            };
            ResizeListViewColumns();
        }

        private void systray_icon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            systray_icon.Visible = false;
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            //If the form is minimized hide it from the task bar  
            //and show the system tray icon (represented by the NotifyIcon control)  
            if (WindowState == FormWindowState.Minimized && Settings.Default.MinimizeToSystemTray)
            {
                Hide();
                systray_icon.Visible = true;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _configManager.Close();
        }

        public void DisplayError(string message)
        {
            RunUi(() => { AlertDlg.ShowError(this, message); });
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

    public interface IMainUiControl
    {
        void TryExecuteOperation(ConfigAction operation, AutoQcConfig config);
        void UpdateUiConfigurations();

        void UpdateButtonsEnabled();

        void LogToUi(string name, string text, bool scrollToEnd = true, bool trim = true);
        void LogErrorToUi(string name, string text, bool scrollToEnd = true, bool trim = true);
        void LogLinesToUi(string name, List<string> lines);
        void LogErrorLinesToUi(string name, List<string> lines);

        void DisplayError(string message);
        void DisplayWarning(string message);
        void DisplayInfo(string message);
        void DisplayErrorWithException(string message, Exception exception);
        DialogResult DisplayQuestion(string message);

    }
}
