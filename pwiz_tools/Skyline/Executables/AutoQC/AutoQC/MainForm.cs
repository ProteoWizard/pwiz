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

namespace AutoQC
{
    public partial class MainForm : Form, IMainUiControl
    {

        private ConfigManager configManager;

        // Flag that gets set to true in the "Shown" event handler. 
        // ItemCheck and ItemChecked events on the listview are ignored until then.
        private bool _loaded;
        

        public MainForm()
        {
            InitializeComponent();

            Program.LogInfo("Loading configurations from saved settings.");
            configManager = new ConfigManager(this);

            UpdateButtonsEnabled();
            UpdateUiConfigurations();
            UpdateUiLoggers();
            UpdateSettingsTab();

            Shown += ((sender, args) =>
            {
                _loaded = true;
                if (Settings.Default.KeepAutoQcRunning)
                    configManager.RunEnabled();
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


        private void btnNewConfig_Click(object sender, EventArgs e)
        {
            Program.LogInfo("Creating new configuration");
            //var configForm = new AutoQcConfigForm(this);
            var configForm = new AutoQcConfigForm(this, null, ConfigAction.Add, false);
            configForm.StartPosition = FormStartPosition.CenterParent;
            configForm.ShowDialog();
        }

        public void AddConfiguration(AutoQcConfig config)
        {
            configManager.AddConfiguration(config);
            UpdateUiConfigurations();
            UpdateUiLoggers();
        }

        private void HandleEditEvent(object sender, EventArgs e)
        {
            var configRunner = configManager.GetSelectedConfigRunner();
            // can edit if config is not busy running, otherwise is view only
            Program.LogInfo(string.Format("{0} configuration \"{1}\"",
                (!configRunner.IsRunning() ? "Editing" : "Viewing"),
                configRunner.GetConfigName()));
            var configForm = new AutoQcConfigForm(this, configRunner.Config, ConfigAction.Edit, configRunner.IsBusy());
            configForm.ShowDialog();
        }

        public void EditSelectedConfiguration(AutoQcConfig newVersion)
        {
            configManager.ReplaceSelectedConfig(newVersion);
            UpdateUiConfigurations();
            UpdateUiLoggers();
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            Program.LogInfo(string.Format("Copying configuration \"{0}\"", configManager.GetSelectedConfig().Name));
            var configForm = new AutoQcConfigForm(this, configManager.GetSelectedConfig(), ConfigAction.Copy, false);
            configForm.ShowDialog();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            configManager.RemoveSelected();
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
            configManager.Import(filePath);
            UpdateUiConfigurations();
            UpdateUiLoggers();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            var shareForm = new ShareConfigsForm(this, configManager);
            shareForm.ShowDialog();
        }

        private void btnRun_MouseClick(object sender, MouseEventArgs e)
        {
            configManager.UpdateSelectedEnabled(true);
            UpdateUiConfigurations();
            UpdateButtonsEnabled();
        }

        private void btnStop_MouseClick(object sender, MouseEventArgs e)
        {
            configManager.UpdateSelectedEnabled(false);
            UpdateUiConfigurations();
            UpdateButtonsEnabled();
        }

        private void listViewConfigs_SelectedIndexChanged(object sender, EventArgs e)
        {
            listViewConfigs.SelectedIndices.Clear();
        }

        private void listViewConfigs_MouseUp(object sender, MouseEventArgs e)
        {
            // Select configuration through _configManager
            var index = listViewConfigs.GetItemAt(e.X, e.Y) != null ? listViewConfigs.GetItemAt(e.X, e.Y).Index : -1;

            if (index < 0)
            {
                configManager.DeselectConfig();
                return;
            }
            configManager.SelectConfig(index);
        }

        public void UpdateButtonsEnabled()
        {
            RunUi(() =>
            {
                var configSelected = configManager.HasSelectedConfig();
                btnEdit.Enabled = configSelected;
                btnCopy.Enabled = configSelected;
                btnDelete.Enabled = configSelected;
                btnViewLog.Enabled = configSelected;
                
                btnRun.Enabled = configSelected && configManager.GetSelectedConfigRunner().CanStart();
                btnStop.Enabled = configSelected && configManager.GetSelectedConfigRunner().CanStop();
            });

        }

        private void listViewConfigs_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            configManager.SortByValue(e.Column);
            UpdateUiConfigurations();
        }

        #endregion


        #region Update UI

        public void UpdateUiConfigurations()
        {
            RunUi(() =>
            {
                Program.LogInfo("Updating configurations");
                listViewConfigs.Items.Clear();
                var listViewItems = configManager.ConfigsListViewItems();
                foreach (var lvi in listViewItems)
                    listViewConfigs.Items.Add(lvi);
                if (configManager.SelectedConfig >= 0)
                    listViewConfigs.Items[configManager.SelectedConfig].Selected = true;
                UpdateLabelVisibility();
                UpdateButtonsEnabled();
            });
        }

        public void UpdateUiLoggers()
        {
            RunUi(() =>
            {
                Program.LogInfo("Updating loggers");
                comboConfigs.Items.Clear();
                comboConfigs.Items.AddRange(configManager.GetLogList());
                comboConfigs.SelectedIndex = configManager.SelectedLog;
            });
        }

        private void UpdateLabelVisibility()
        {
            lblNoConfigs.Hide();
            if (!configManager.HasConfigs())
            {
                lblNoConfigs.Show();
            }
        }

        #endregion


        #region Logging

        private void btnViewLog_Click(object sender, EventArgs e)
        {
            if (configManager.HasSelectedConfig())
            {
                configManager.SelectLogOfSelectedConfig();
                UpdateUiLoggers();
                SwitchLogger();
            }
            tabMain.SelectTab(tabLog);
        }

        private void comboConfigs_SelectedIndexChanged(object sender, EventArgs e)
        {
            configManager.SelectLog(comboConfigs.SelectedIndex);
            if (configManager.SelectedLog >= 0)
                btnOpenFolder.Enabled = true;
            SwitchLogger();
        }

        private async void SwitchLogger()
        {
            textBoxLog.Clear();

            var logger = configManager.GetSelectedLogger();
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
                DisplayErrorWithException(Resources.MainForm_ViewLog_Error_Reading_Log, ex.Message, ex);
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
            var logger = configManager.GetSelectedLogger();
            if (!File.Exists(logger.GetFile()))
            {
                if (!Directory.Exists(logger.GetDirectory()))
                {
                    var err = string.Format(Resources.MainForm_btnOpenFolder_Click_Directory_does_not_exist___0_, logger.GetFile());
                    DisplayError(Resources.MainForm_btnOpenFolder_Click_Directory_Not_Found, err);
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
                if (!configManager.LoggerIsDisplayed(name))
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
                var message = (configManager.GetSelectedLogger() != null)
                    ? string.Format(AutoQcLogger.LogTruncatedMessage, configManager.GetSelectedLogger().GetFile())
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
                if (!configManager.LoggerIsDisplayed(name))
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
                if (!configManager.LoggerIsDisplayed(name))
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
                if (!configManager.LoggerIsDisplayed(name))
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
                configManager.ChangeKeepRunningState(enable);
            }
            catch (Exception ex)
            {
                var err = enable ? string.Format(Resources.MainForm_cb_keepRunning_CheckedChanged_Error_enabling__Keep__0__running_, Program.AppName)
                    : string.Format(Resources.MainForm_cb_keepRunning_CheckedChanged_Error_disabling__Keep__0__running_, Program.AppName);
                // ReSharper disable once LocalizableElement
                Program.LogError($"Error {(enable ? "enabling" : "disabling")} \"Keep AutoQC Loader running\"", ex);

                DisplayErrorWithException(Resources.MainForm_cb_keepRunning_CheckedChanged_Error_Changing_Settings,
                    TextUtil.LineSeparate(
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
            configManager.Close();
        }

        public void DisplayError(string title, string message)
        {
            RunUi(() => { AlertDlg.ShowError(this, message, title); });
        }

        public void DisplayWarning(string title, string message)
        {
            RunUi(() => { AlertDlg.ShowWarning(this, message, title); });
        }

        public void DisplayInfo(string title, string message)
        {
            RunUi(() => { AlertDlg.ShowInfo(this, message, title); });
        }

        public void DisplayErrorWithException(string title, string message, Exception exception)
        {
            RunUi(() => { AlertDlg.ShowErrorWithException(this, message, title, exception); });
        }

        public DialogResult DisplayQuestion(string title, string message)
        {
            return AlertDlg.ShowQuestion(this, message, title);
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
        void AddConfiguration(AutoQcConfig config);
        void EditSelectedConfiguration(AutoQcConfig newVersion);
        void UpdateUiConfigurations();

        void UpdateButtonsEnabled();

        void LogToUi(string name, string text, bool scrollToEnd = true, bool trim = true);
        void LogErrorToUi(string name, string text, bool scrollToEnd = true, bool trim = true);
        void LogLinesToUi(string name, List<string> lines);
        void LogErrorLinesToUi(string name, List<string> lines);

        void DisplayError(string title, string message);
        void DisplayWarning(string title, string message);
        void DisplayInfo(string title, string message);
        void DisplayErrorWithException(string title, string message, Exception exception);
        DialogResult DisplayQuestion(string title, string message);


    }
}
