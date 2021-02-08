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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class MainForm : Form, IMainUiControl
    {

        private readonly ConfigManager _configManager;
        private readonly ISkylineBatchLogger _skylineBatchLogger;
        private bool _loaded;
        private double[] _listViewColumnWidths;
        private bool _resizing;

        public MainForm()
        {
            InitializeComponent();

            _skylineBatchLogger = new SkylineBatchLogger(Program.AppName() + ".log", this);
            btnRunOptions.Text = char.ConvertFromUtf32(0x2BC6);
            toolStrip1.Items.Insert(3,new ToolStripSeparator());
            _listViewColumnWidths = new[]
            {
                (double)listViewConfigName.Width/listViewConfigs.Width,
                (double)listViewModified.Width/listViewConfigs.Width,
                (double)listViewStatus.Width/listViewConfigs.Width,
            };
            listViewConfigs.ColumnWidthChanged += listViewConfigs_ColumnWidthChanged;

            Program.LogInfo(Resources.MainForm_MainForm_Loading_configurations_from_saved_settings_);
            _configManager = new ConfigManager(_skylineBatchLogger, this);

            UpdateUiConfigurations();
            UpdateLabelVisibility();
            UpdateUiLogFiles();

            Shown += ((sender, args) => { _loaded = true; });
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
        }

        #region Manipulating configuration list
        
        private void btnNewConfig_Click(object sender, EventArgs e)
        {
            Program.LogInfo(Resources.MainForm_btnNewConfig_Click_Creating_a_new_configuration_);
            var initialConfigValues =_configManager.GetLastModified();
            var configForm = new SkylineBatchConfigForm(this, initialConfigValues, ConfigAction.Add, false);
            configForm.ShowDialog();
        }

        public void AddConfiguration(SkylineBatchConfig config)
        {
            _configManager.AddConfiguration(config);
            _configManager.SelectConfig(_configManager.ConfigNamesAsObjectArray().Length - 1);
            UpdateUiConfigurations();
        }

        private void HandleEditEvent(object sender, EventArgs e)
        {
            var configRunner = _configManager.GetSelectedConfigRunner();
            var config = configRunner.Config;
            try
            {
                config.Validate();
            }
            catch (ArgumentException)
            {
                if (configRunner.IsRunning()) throw new Exception("Invalid configuration cannot be running.");
                var validateConfigForm = new InvalidConfigSetupForm(config, _configManager, this);
                validateConfigForm.ShowDialog();
                if (validateConfigForm.DialogResult != DialogResult.OK)
                    return;
                config = validateConfigForm.ValidConfig;
            }
            var configForm = new SkylineBatchConfigForm(this, config, ConfigAction.Edit, configRunner.IsBusy());
            configForm.ShowDialog();
        }

        public void EditSelectedConfiguration(SkylineBatchConfig newVersion)
        {
            _configManager.ReplaceSelectedConfig(newVersion);
            UpdateUiConfigurations();
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            var configForm = new SkylineBatchConfigForm(this, _configManager.GetSelectedConfig(), ConfigAction.Copy, false);
            configForm.ShowDialog();
        }

        private void listViewConfigs_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (!_loaded) return;
            var success = _configManager.CheckConfigAtIndex(e.Index, out string errorMessage);
            if (!success)
            {
                e.NewValue = e.CurrentValue;
                DisplayError(errorMessage);
            }
        }

        private void listViewConfigs_MouseUp(object sender, MouseEventArgs e)
        {
            // Select configuration through _configManager
            var index = listViewConfigs.GetItemAt(e.X, e.Y) != null ? listViewConfigs.GetItemAt(e.X, e.Y).Index : -1;

            if (index < 0)
            {
                _configManager.DeselectConfig();
                return;
            }
            _configManager.SelectConfig(index);
        }
        
        private void listViewConfigs_PreventItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            // Disable automatic item selection - selected configuration set through _configManager
            //      Automatic selection disables red text, can't see invalid configurations
            listViewConfigs.SelectedIndices.Clear();
        }

        private void UpdateButtonsEnabled()
        {
            var configSelected = _configManager.HasSelectedConfig();
            btnEdit.Enabled = configSelected;
            btnCopy.Enabled = configSelected;
            btnUpArrow.Enabled = configSelected && _configManager.SelectedConfig != 0;
            btnDownArrow.Enabled = configSelected && _configManager.SelectedConfig < listViewConfigs.Items.Count - 1;
            btnDelete.Enabled = configSelected;
            btnOpenAnalysis.Enabled = configSelected;
            btnOpenTemplate.Enabled = configSelected;
            btnOpenResults.Enabled = configSelected;
            btnExportConfigs.Enabled = _configManager.HasConfigs();
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
            _configManager.RemoveSelected();
            UpdateUiConfigurations();
        }

        #endregion

        #region Open File/Folder

        private void btnOpenAnalysis_Click(object sender, EventArgs e)
        {
            var config = _configManager.GetSelectedConfig();
            if (!_configManager.IsSelectedConfigValid())
            {
                DisplayError(Resources.MainForm_btnOpenAnalysis_Click_Cannot_open_the_analysis_folder_of_an_invalid_configuration_ + Environment.NewLine +
                             string.Format(Resources.MainForm_btnOpenTemplate_Click_Please_fix___0___and_try_again_, config.Name));
                return;
            }
            config.MainSettings.CreateAnalysisFolderIfNonexistent();
            var folder = config.MainSettings.AnalysisFolderPath;
            Process.Start("explorer.exe", "/n," + folder);
        }

        private void btnOpenTemplate_Click(object sender, EventArgs e)
        {
            var config = _configManager.GetSelectedConfig();
            if (!_configManager.IsSelectedConfigValid())
            {
                DisplayError(Resources.MainForm_btnOpenTemplate_Click_Cannot_open_the_Skyline_template_file_of_an_invalid_configuration_ + Environment.NewLine +
                             string.Format(Resources.MainForm_btnOpenTemplate_Click_Please_fix___0___and_try_again_, config.Name));
                return;
            }
            // Open template file
            var template = config.MainSettings.TemplateFilePath;
            Process.Start(template);
        }

        private void btnOpenResults_Click(object sender, EventArgs e)
        {
            var config = _configManager.GetSelectedConfig();
            if (!_configManager.IsSelectedConfigValid())
            {
                DisplayError(Resources.MainForm_btnOpenResults_Click_Cannot_open_the_Skyline_results_file_of_an_invalid_configuration_ + Environment.NewLine +
                             string.Format(Resources.MainForm_btnOpenTemplate_Click_Please_fix___0___and_try_again_, config.Name));
                return;
            }
            var resultsFile = config.MainSettings.GetResultsFilePath();
            if (!File.Exists(resultsFile))
            {
                DisplayError(Resources.MainForm_btnOpenResults_Click_The_Skyline_results_file_for_this_configuration_has_not_been_generated_yet_ + Environment.NewLine +
                             string.Format(Resources.MainForm_btnOpenResults_Click_Please_run___0___from_step_one_and_try_again_, config.Name));
                return;
            }
            // Open results file
            Process.Start(resultsFile);
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
                ((ToolStripMenuItem)batchRunDropDown.Items[i]).Checked = false;
            }
            ((ToolStripMenuItem)batchRunDropDown.Items[selectedIndex]).Checked = true;
            btnRunBatch.TextAlign = selectedIndex == 0 ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            btnRunBatch.Text = e.ClickedItem.Text.Insert(1,"&");
            RunBatch();
        }

        private void btnRunBatch_Click(object sender, EventArgs e)
        {
            RunBatch();
        }

        private void RunBatch()
        {
            for (int i = 1; i <= batchRunDropDown.Items.Count; i++)
            {
                if (((ToolStripMenuItem)batchRunDropDown.Items[i - 1]).Checked)
                {
                    _configManager.RunAllEnabled(i); // configurations run asynchronously
                    break;
                }
            }
            // update ui log and switch to log tab
            if (_configManager.ConfigsRunning().Count > 0)
            {
                comboLogList.SelectedIndex = 0;
                RunUi(() =>
                {
                    tabMain.SelectTab(tabLog);
                });
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            _configManager.CancelRunners();
            btnCancel.Enabled = false;
        }

        #endregion
        
        #region Update UI

        // Reload configurations from configManager
        public void UpdateUiConfigurations()
        {
            RunUi(() =>
            {
                Program.LogInfo("Updating configurations");
                listViewConfigs.Items.Clear();
                listViewConfigs.ItemCheck -= listViewConfigs_ItemCheck;
                var listViewItems = _configManager.ConfigsListViewItems();
                foreach (var lvi in listViewItems)
                    listViewConfigs.Items.Add(lvi);
                listViewConfigs.ItemCheck += listViewConfigs_ItemCheck;
                UpdateLabelVisibility();
                UpdateButtonsEnabled();
            });

        }

        // Reload logs in comboLogList
        public void UpdateUiLogFiles()
        {
            RunUi(() =>
            {
                Program.LogInfo("Updating log files");
                comboLogList.Items.Clear();
                comboLogList.Items.AddRange(_configManager.GetAllLogFiles());
                comboLogList.SelectedIndex = _configManager.SelectedLog;
                btnDeleteLogs.Enabled = _configManager.HasOldLogs();
            });

        }

        public void UpdateRunningButtons(bool isRunning)
        {
            RunUi(() =>
            {
                btnRunBatch.Enabled = !isRunning;
                btnRunOptions.Enabled = btnRunBatch.Enabled;
                btnCancel.Enabled = isRunning;
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

        #endregion
        
        #region Import / export

        private void btnImport_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = TextUtil.FILTER_XML;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var filePath = dialog.FileName;

            _configManager.Import(filePath);
            UpdateUiConfigurations();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            var shareForm = new ShareConfigsForm(this, _configManager);
            shareForm.ShowDialog();
        }
        
        #endregion
        
        #region Logging

        private void btnViewLog_Click(object sender, EventArgs e)
        {
            comboLogList.SelectedIndex = 0;
            tabMain.SelectTab(tabLog);
        }

        private void comboLogList_SelectedIndexChanged(object sender, EventArgs e)
        {
            _configManager.SelectLog(comboLogList.SelectedIndex);
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
                DisplayError(ex.Message);
            }
            ScrollToLogEnd();
        }

        private void ScrollToLogEnd()
        {
            RunUi(() =>
            {
                textBoxLog.SelectionStart = textBoxLog.Text.Length;
                textBoxLog.ScrollToCaret();
            });
        }

        private void btnDeleteLogs_Click(object sender, EventArgs e)
        {
            var manageLogsForm = new LogForm(_configManager);
            manageLogsForm.ShowDialog();
        }

        private void btnOpenFolder_Click(object sender, EventArgs e)
        {
            var logger = _configManager.GetSelectedLogger();
            var arg = "/select, \"" + logger.GetFile() + "\"";
            Process.Start("explorer.exe", arg);
        }

        public void LogToUi(string text, bool scrollToEnd, bool trim)
        {
            RunUi(() =>
            {
                if (comboLogList.SelectedIndex != 0) return; // don't log if old log is displayed
                if (text.Contains("Fatal error: ") || text.Contains("Error: "))
                {
                    LogErrorToUi(text, scrollToEnd, trim);
                    return;
                }

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
            const int buffer = SkylineBatchLogger.MaxLogLines / 10;
            if (numLines > SkylineBatchLogger.MaxLogLines + buffer)
            {
                var unTruncated = textBoxLog.Text;
                var startIndex = textBoxLog.GetFirstCharIndexFromLine(numLines - SkylineBatchLogger.MaxLogLines);
                var message = (_skylineBatchLogger != null)
                    ? string.Format(Resources.SkylineBatchLogger_DisplayLog_____Log_truncated_____Full_log_is_in__0_, _skylineBatchLogger.GetFile())
                    : Resources.MainForm_TrimDisplayedLog_____Log_truncated____;
                message += Environment.NewLine;
                textBoxLog.Text = message + unTruncated.Substring(startIndex);
                textBoxLog.SelectionStart = 0;
                textBoxLog.SelectionLength = message.Length;
                textBoxLog.SelectionColor = Color.Red;
            }
        }

        public void LogErrorToUi(string text, bool scrollToEnd, bool trim)
        {
            RunUi(() =>
            {
                if (trim)
                {
                    TrimDisplayedLog();
                }

                textBoxLog.SelectionStart = textBoxLog.TextLength;
                textBoxLog.SelectionLength = 0;
                textBoxLog.SelectionColor = Color.Red;
                textBoxLog.AppendText(text);
                textBoxLog.AppendText(Environment.NewLine);
                textBoxLog.SelectionColor = textBoxLog.ForeColor;
            });
        }

        public void LogLinesToUi(List<string> lines)
        {
            RunUi(() =>
            {
                foreach (var line in lines)
                {
                    textBoxLog.AppendText(line);
                    textBoxLog.AppendText(Environment.NewLine);
                }
            });
        }

        public void LogErrorLinesToUi(List<string> lines)
        {
            RunUi(() =>
            {
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

        #region Mainform event handlers and errors

        private void listViewConfigs_Resize(object sender, EventArgs e)
        {
            ResizeListViewColumns();
        }

        private void ResizeListViewColumns()
        {
            _resizing = true;
            var listViewWidth = listViewConfigs.Width;
            listViewConfigName.Width = (int)Math.Floor(listViewWidth * _listViewColumnWidths[0]);
            listViewModified.Width = (int)Math.Floor(listViewWidth * _listViewColumnWidths[1]);
            listViewStatus.Width = -2;
            _resizing = false;
        }

        private void listViewConfigs_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            if (_resizing) return;

            var listViewWidth = listViewConfigs.Width - 10;
            _listViewColumnWidths = new[]
            {
                (double)listViewConfigName.Width/listViewWidth,
                (double)listViewModified.Width/listViewWidth,
                1 - (double)listViewConfigName.Width/listViewWidth - (double)listViewModified.Width/listViewWidth
            };

            ResizeListViewColumns();
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

        public DialogResult DisplayLargeQuestion(string message)
        {
            return AlertDlg.ShowLargeQuestion(this, message);
        }

        #endregion

    }

    // ListView that prevents a double click from toggling checkbox
    class MyListView : ListView
    {
        private bool checkFromDoubleClick = false;

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

    public interface IMainUiControl
    {
        void AddConfiguration(SkylineBatchConfig config);
        void EditSelectedConfiguration(SkylineBatchConfig newVersion);
        void UpdateUiConfigurations();

        void UpdateUiLogFiles();
        void UpdateRunningButtons(bool isRunning);
        
        void LogToUi(string text, bool scrollToEnd = true, bool trim = true);
        void LogErrorToUi(string text, bool scrollToEnd = true, bool trim = true);
        void LogLinesToUi(List<string> lines);
        void LogErrorLinesToUi(List<string> lines);

        void DisplayError(string message);
        void DisplayWarning(string message);
        void DisplayInfo(string message);
        void DisplayErrorWithException(string message, Exception exception);
        DialogResult DisplayQuestion(string message);
        DialogResult DisplayLargeQuestion(string message);
    }
}
