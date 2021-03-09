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

        private readonly SkylineBatchConfigManager _configManager;
        private readonly Logger _skylineBatchLogger;
        private bool _loaded;
        private readonly ColumnWidthCalculator _listViewColumnWidths;

        public MainForm()
        {
            InitializeComponent();
            var roamingFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var localFolder = Path.Combine(Path.GetDirectoryName(roamingFolder) ?? throw new InvalidOperationException(), "local");
            var logPath= Path.Combine(localFolder, Program.AppName(), Program.AppName() + TextUtil.EXT_LOG);
            _skylineBatchLogger = new Logger(logPath, Program.AppName() + TextUtil.EXT_LOG, this);
            btnRunOptions.Text = char.ConvertFromUtf32(0x2BC6);
            toolStrip1.Items.Insert(3,new ToolStripSeparator());
            _listViewColumnWidths = new ColumnWidthCalculator(listViewConfigs);
            listViewConfigs.ColumnWidthChanged += listViewConfigs_ColumnWidthChanged;
            ProgramLog.Info(Resources.MainForm_MainForm_Loading_configurations_from_saved_settings_);
            _configManager = new SkylineBatchConfigManager(_skylineBatchLogger, this);

            UpdateUiConfigurations();
            ListViewSizeChanged();
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
            catch (InvalidOperationException)
            {
            }
        }

        #region Manipulating configuration list
        
        private void btnNewConfig_Click(object sender, EventArgs e)
        {
            ProgramLog.Info(Resources.MainForm_btnNewConfig_Click_Creating_a_new_configuration_);
            var initialConfigValues = (SkylineBatchConfig)_configManager.GetLastModified();
            var configForm = new SkylineBatchConfigForm(this, initialConfigValues, ConfigAction.Add, false);
            configForm.ShowDialog();
        }

        public void AssertUniqueConfigName(string newName, bool replacing)
        {
            _configManager.AssertUniqueName(newName, replacing);
        }

        public void AddConfiguration(IConfig config)
        {
            _configManager.AddConfiguration(config);
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

        private void HandleEditEvent(object sender, EventArgs e)
        {
            var configRunner = _configManager.GetSelectedConfigRunner();
            var config = (SkylineBatchConfig)configRunner.GetConfig();
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
            ListViewSizeChanged();
        }

        #endregion

        #region Open File/Folder

        private void btnOpenAnalysis_Click(object sender, EventArgs e)
        {
            var config = _configManager.GetSelectedConfig();
            config.MainSettings.CreateAnalysisFolderIfNonexistent();
            MainFormUtils.OpenFileExplorer(config.Name, _configManager.IsSelectedConfigValid(),
                Resources.MainForm_btnOpenAnalysis_Click_analysis_folder,
                config.MainSettings.AnalysisFolderPath, this);
        }

        private void btnOpenTemplate_Click(object sender, EventArgs e)
        {
            var config = _configManager.GetSelectedConfig();
            if (MainFormUtils.CanOpen(config.Name, _configManager.IsSelectedConfigValid(),
                Resources.MainForm_btnOpenTemplate_Click_Skyline_template_file, this))
            {
                SkylineInstallations.OpenSkylineFile(config.MainSettings.TemplateFilePath, config.SkylineSettings);
            }
        }

        private void btnOpenResults_Click(object sender, EventArgs e)
        {
            var config = _configManager.GetSelectedConfig();
            var resultsFile = config.MainSettings.GetResultsFilePath();

            if (MainFormUtils.CanOpen(config.Name, _configManager.IsSelectedConfigValid(),
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
                    _ =_configManager.RunAllEnabled(i); // configurations run asynchronously
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
                ProgramLog.Info("Updating configurations");
                var topItemIndex = listViewConfigs.TopItem != null ? listViewConfigs.TopItem.Index : -1;
                listViewConfigs.ItemCheck -= listViewConfigs_ItemCheck;
                var listViewItems = _configManager.ConfigsListViewItems();
                listViewConfigs.Items.Clear();
                foreach (var lvi in listViewItems)
                    listViewConfigs.Items.Add(lvi);
                if (topItemIndex != -1 && listViewConfigs.Items.Count > topItemIndex)
                    listViewConfigs.TopItem = listViewConfigs.Items[topItemIndex];
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
                btnCancel.Enabled = canStop;
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
            dialog.Filter = TextUtil.FILTER_BCFG;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var filePath = dialog.FileName;

            _configManager.Import(filePath);
            UpdateUiConfigurations();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            var shareForm = new ShareConfigsForm(this, _configManager, TextUtil.FILTER_BCFG, Program.Icon());
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

        public void LogToUi(string name, string text, bool scrollToEnd, bool trim)
        {
            RunUi(() =>
            {
                if (comboLogList.SelectedIndex != 0) return; // don't log if old log is displayed
                if (text.Contains("Fatal error: ") || text.Contains("Error: "))
                {
                    LogErrorToUi(name, text, scrollToEnd, trim);
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
            const int buffer = Logger.MaxLogLines / 10;
            if (numLines > Logger.MaxLogLines + buffer)
            {
                var unTruncated = textBoxLog.Text;
                var startIndex = textBoxLog.GetFirstCharIndexFromLine(numLines - Logger.MaxLogLines);
                var message = (_skylineBatchLogger != null)
                    ? string.Format(Resources.Logger_DisplayLog_____Log_truncated_____Full_log_is_in__0_, _skylineBatchLogger.GetFile())
                    : Resources.MainForm_TrimDisplayedLog_____Log_truncated____;
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

        public void LogLinesToUi(string name, List<string> lines)
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

        public void LogErrorLinesToUi(string name, List<string> lines)
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

        private void MainForm_Resize(object sender, EventArgs e)
        {
            ListViewSizeChanged();
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

        private async Task CloseForm()
        {
            _configManager.Close();
            // Wait for config manager to close
            while (!_configManager.Closed)
                await Task.Delay(3000);
            Close();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_configManager.Closed)
                e.Cancel = true;
            if (Visible)
            {
                Hide();
                _ = CloseForm();
            }
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

        public DialogResult DisplayLargeQuestion(string message)
        {
            return AlertDlg.ShowLargeQuestion(this, Program.AppName(), message);
        }

        #endregion
    }

    // ListView that prevents a double click from toggling checkbox
    class MyListView : ListView
    {
        private bool checkFromDoubleClick;

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
