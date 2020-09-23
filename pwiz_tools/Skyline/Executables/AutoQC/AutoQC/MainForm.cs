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
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using AutoQC.Properties;

namespace AutoQC
{
    public partial class MainForm : Form, IMainUiControl
    {
        private Dictionary<string, ConfigRunner> _configRunners;

        private readonly ListViewColumnSorter _columnSorter;

        // Flag that gets set to true in the "Shown" event handler. 
        // ItemCheck and ItemChecked events on the listview are ignored until then.
        private bool _loaded;

        private IAutoQcLogger _currentAutoQcLogger;

        private const string XML_FILES_FILTER = "XML Files(*.xml)|*.xml";

        public MainForm()
        {
            InitializeComponent();

            _columnSorter = new ListViewColumnSorter();
            listViewConfigs.ListViewItemSorter = _columnSorter;

            btnCopy.Enabled = false;
            btnDelete.Enabled = false;
            btnEdit.Enabled = false;

            ReadSavedConfigurations();

            UpdateLabelVisibility();

            UpdateSettingsTab();

            Shown += ((sender, args) =>
            {
                _loaded = true;
                if (Settings.Default.KeepAutoQcRunning)
                {
                    RunEnabledConfigurations();
                }
            });

        }
        
        private void UpdateSkylineTypeAndInstallPathControls()
        {
            if (!SkylineSettings.IsInitialized())
            {
                return;
            }

            radioButtonUseSkyline.Checked = SkylineSettings.UseSkyline;
            radioButtonUseSkylineDaily.Checked = !SkylineSettings.UseSkyline;

            var useClickOnce = SkylineSettings.UseClickOnceInstall;
            radioButtonWebBasedSkyline.Checked = useClickOnce;
            radioButtonSpecifySkylinePath.Checked = !useClickOnce;
            textBoxSkylinePath.Text = SkylineSettings.SkylineInstallDir;

            textBoxSkylinePath.Enabled = !useClickOnce;
            buttonFileDialogSkylineInstall.Enabled = !useClickOnce;
        }

        public static string GetExePath()
        {
            return SkylineSettings.GetSkylineCmdLineExePath;
        }

        private void ReadSavedConfigurations()
        {
            Program.LogInfo("Reading configurations from saved settings.");
            var configList = Settings.Default.ConfigList;
            var sortedConfig = configList.OrderByDescending(c => c.Created);
            _configRunners = new Dictionary<string, ConfigRunner>();
            foreach (var config in sortedConfig)
            {
                if (config.IsEnabled && !Settings.Default.KeepAutoQcRunning)
                {
                    // If the config was running last time AutoQC Loader was running (and properties saved), but we are not 
                    // automatically starting configs on startup, change its IsEnabled state
                    config.IsEnabled = false;
                }

                AddConfiguration(config);
            }
        }

        private ConfigRunner GetSelectedConfigRunner()
        {
            if (listViewConfigs.SelectedItems.Count == 0)
                return null;

            var selectedConfig = listViewConfigs.SelectedItems[0].SubItems[0].Text;
            ConfigRunner configRunner;
            _configRunners.TryGetValue(selectedConfig, out configRunner);
            if (configRunner == null)
            {
                Program.LogError(string.Format("Could not get a config runner for configuration \"{0}\"", selectedConfig));
            }
            return configRunner;
        }

        private static void ShowConfigForm(AutoQcConfigForm configForm)
        {
            configForm.StartPosition = FormStartPosition.CenterParent;
            configForm.ShowDialog();
        }
        // TODO: Do we need this? 
        private void RunEnabledConfigurations()
        {
            foreach (var configRunner in _configRunners.Values)
            {
                if (!configRunner.IsConfigEnabled())
                    continue;
                Program.LogInfo(string.Format("Starting configuration {0}", configRunner.GetConfigName()));
                StartConfigRunner(configRunner);
            }
        }

        private void StartConfigRunner(ConfigRunner configRunner)
        {
            try
            {
                configRunner.Start();
            }
            catch (Exception e)
            {
                var title = string.Format(Resources.MainForm_StartConfigRunner_Error_Starting_Configuration___0__, configRunner.Config.Name);
                ShowErrorWithExceptionDialog(title, e.Message, e);
                // ReSharper disable once LocalizableElement
                Program.LogError(string.Format("Error Starting Configuration \"{0}\"", configRunner.Config.Name), e);
            }
        }

        private void ChangeConfigState(ConfigRunner configRunner)
        {
            var config = configRunner.Config;
            if (config.IsEnabled)
            {
                Program.LogInfo(string.Format("Starting configuration \"{0}\"", config.Name));
                StartConfigRunner(configRunner);
            }
            else
            {
                Program.LogInfo(string.Format("Stopping configuration \"{0}\"", config.Name));
                configRunner.Stop();
            }
        }

        private void ShowErrorDialog(string title, string message)
        {
            AlertDlg.ShowError(this, message, title);
        }   

        private void ShowWarningDialog(string title, string message)
        {
            AlertDlg.ShowWarning(this, message, title);
        }

        private void ShowInfoDialog(string title, string message)
        {
            AlertDlg.ShowInfo(this, message, title);
        }

        private void ShowErrorWithExceptionDialog(string title, string message, Exception exception)
        {
            AlertDlg.ShowErrorWithException(this, message, title, exception);
        }

        private DialogResult ShowQuestionDialog(string title, string message)
        {
            return AlertDlg.ShowQuestion(this, message, title);
        }


        #region event handlers

        private void btnNewConfig_Click(object sender, EventArgs e)
        {
//            MessageBox.Show(Application.UserAppDataPath + " directory");
            Program.LogInfo("Creating new configuration");
            var configForm = new AutoQcConfigForm(this);
            configForm.StartPosition = FormStartPosition.CenterParent;
            ShowConfigForm(configForm);
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            // Get the selected configuration    
            var configRunner = GetSelectedConfigRunner();

            if (configRunner == null)
            {
                return;
            }

            Program.LogInfo(string.Format("{0} configuration \"{1}\"", (configRunner.IsStopped() ? "Editing" : "Viewing"),
                configRunner.GetConfigName()));

            var configForm = new AutoQcConfigForm(configRunner.Config, configRunner, this);
            ShowConfigForm(configForm);
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            // Get the selected configuration
            var configRunner = GetSelectedConfigRunner();
            if (configRunner == null)
            {
                return;
            }
            Program.LogInfo(string.Format("Copying configuration \"{0}\"", configRunner.GetConfigName()));
            var newConfig = configRunner.Config.Copy();
            newConfig.Name = null;
            var configForm = new AutoQcConfigForm(newConfig, null, this);
            ShowConfigForm(configForm);
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            Program.LogInfo("Delete clicked");
            // Get the selected configuration
            var configRunner = GetSelectedConfigRunner();
            if (configRunner == null)
            {
                return;
            }
            // Check if this configuration is running or in one of the intermidiate (starting, stopping) stages
            if (configRunner.IsBusy())
            {
                string message = null;
                if (configRunner.IsStarting() || configRunner.IsRunning())
                {
                    message =
                        string.Format(
                            Resources.MainForm_btnDelete_Click_Configuration___0___is_running__Please_stop_the_configuration_and_try_again__,
                            configRunner.GetConfigName());
                }
                else if (configRunner.IsStopping())
                {
                    message =
                        string.Format(
                            Resources.MainForm_btnDelete_Click_Please_wait_for_the_configuration___0___to_stop_and_try_again_,
                            configRunner.GetConfigName());
                }
                ShowWarningDialog(Resources.MainForm_btnDelete_Click_Cannot_Delete, message);
                return;
            }

            var doDelete = ShowQuestionDialog(Resources.MainForm_btnDelete_Click_Confirm_Delete,
                string.Format(Resources.MainForm_btnDelete_Click_Are_you_sure_you_want_to_delete_configuration___0___, configRunner.GetConfigName()));
            
            if (doDelete != DialogResult.Yes) return;

            RemoveConfiguration(configRunner.Config);
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            Settings.Default.Save();

            var dialog = new SaveFileDialog {Title = Resources.MainForm_btnExport_Click_Save_configurations___, Filter = XML_FILES_FILTER};
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var filePath = dialog.FileName;
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            config.SaveAs(filePath);
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = XML_FILES_FILTER;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var filePath = dialog.FileName;

            List<AutoQcConfig> readConfigs = new List<AutoQcConfig>();
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    using (var reader = XmlReader.Create(stream))
                    {
                        while (reader.Read())
                        {
                            if (reader.IsStartElement() && reader.Name.Equals(@"autoqc_config"))
                            {
                                AutoQcConfig config = AutoQcConfig.Deserialize(reader);
                                readConfigs.Add(config);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorWithExceptionDialog(Resources.MainForm_btnImport_Click_Import_Configurations_Error,
                    string.Format(Resources.MainForm_btnImport_Click_Could_not_import_configurations_from_file__0_, filePath), ex);
                return;
            }

            if (readConfigs.Count == 0)
            {
                ShowWarningDialog(Resources.MainForm_btnImport_Click_Import_Configurations,
                    string.Format(Resources.MainForm_btnImport_Click_No_configurations_were_found_in_file__0__,
                        filePath));
                return;
            }

            var validationErrors = new List<string>();
            var duplicateConfigs = new List<string>();
            var numAdded = 0;
            foreach (AutoQcConfig config in readConfigs)
            {
                // Make sure that the configuration name is unique
                if (GetConfig(config.Name) != null)
                {
                    // If a configuration with the same name already exists, don't add it
                    duplicateConfigs.Add(config.Name);
                    continue;
                }

                try
                {
                    config.Validate();
                }
                catch (Exception ex)
                {
                    validationErrors.Add(string.Format("\"{0}\" Error: {1}", config.Name, ex.Message));
                    continue;
                }

                config.IsEnabled = false;
                AddConfiguration(config);
                numAdded++;
            }

            var message = new StringBuilder(Resources.MainForm_btnImport_Click_Number_of_configurations_imported__);
            message.Append(numAdded).Append(Environment.NewLine);
            if (duplicateConfigs.Count > 0)
            {
                message.Append(Resources.MainForm_btnImport_Click_The_following_configurations_already_exist_and_were_not_imported_)
                    .Append(Environment.NewLine);
                foreach (var name in duplicateConfigs)
                {
                    message.Append("\"").Append(name).Append("\"").Append(Environment.NewLine);
                }
            }
            if (validationErrors.Count > 0)
            {
                message.Append(Resources.MainForm_btnImport_Click_The_following_configurations_could_not_be_validated_and_were_not_imported_)
                    .Append(Environment.NewLine);
                foreach (var error in validationErrors)
                {
                    message.Append(error).Append(Environment.NewLine);
                }
            }
            ShowInfoDialog(Resources.MainForm_btnImport_Click_Import_Configurations, message.ToString());

        }

        private void listViewConfigs_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (!_loaded)
                return;

            var configName = listViewConfigs.Items[e.Index].SubItems[0].Text;

            ConfigRunner configRunner;
            _configRunners.TryGetValue(configName, out configRunner);
            if (configRunner == null)
                return;

            if (configRunner.IsStarting() || configRunner.IsStopping())
            {
                e.NewValue = e.CurrentValue;
                var message = string.Format(Resources.MainForm_listViewConfigs_ItemCheck_Configuration_is__0___Please_wait_,
                    configRunner.IsStarting() ? Resources.MainForm_listViewConfigs_ItemCheck_starting : Resources.MainForm_listViewConfigs_ItemCheck_stopping);

                ShowWarningDialog(Resources.MainForm_listViewConfigs_ItemCheck_Please_Wait, message);
                return;
            }

            if (e.NewValue == CheckState.Checked) return;

            var doChange =
                ShowQuestionDialog(Resources.MainForm_listViewConfigs_ItemCheck_Confirm_Stop,
                    string.Format(Resources.MainForm_listViewConfigs_ItemCheck_Are_you_sure_you_want_to_stop_configuration___0___, configRunner.GetConfigName()));

            if (doChange != DialogResult.Yes)
            {
                e.NewValue = e.CurrentValue;
            }
        }

        private void listViewConfigs_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (!_loaded)
                return;

            var configName = e.Item.SubItems[0].Text; // Name of the configuration
            var configRunner = ChangeConfigEnabledSetting(configName, e.Item.Checked);

            if (configRunner != null)
            {
                ChangeConfigState(configRunner);
            }
        }

        private ConfigRunner ChangeConfigEnabledSetting(string configName, bool enabled)
        {
            _configRunners.TryGetValue(configName, out var configRunner);
            if (configRunner == null)
                return null;
            configRunner.Config.IsEnabled = enabled;
            var configList =
                Properties.Settings.Default
                    .ConfigList; // NOTE: This is required for settings to get updated.
            Properties.Settings.Default.Save(); // Save the configuration state
            return configRunner;
        }

        private void listViewConfigs_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            var lvi = e.Item;

            if (!lvi.Selected)
            {
                UpdateButtons(null);
            }
            else
            {
                ConfigRunner configRunner;
                _configRunners.TryGetValue(lvi.Text, out configRunner);
                UpdateButtons(configRunner);
            }

        }

        private void listViewConfigs_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == _columnSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (_columnSorter.Order == SortOrder.Ascending)
                {
                    _columnSorter.Order = SortOrder.Descending;
                }
                else
                {
                    _columnSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                _columnSorter.SortColumn = e.Column;
                _columnSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            listViewConfigs.Sort();
        }

        // Event triggered by button on the main tab that lists all configurations
        private void btnViewLog1_Click(object sender, EventArgs e)
        {
            var selectedItems = listViewConfigs.SelectedItems;
            if (selectedItems.Count > 0)
            {
                var selectedConfigName = selectedItems[0].Text;
                comboConfigs.SelectedItem = selectedConfigName;
                ViewLog(selectedConfigName);
            }
            tabMain.SelectTab(tabLog);
        }

        // Event triggered by button on the "Log" tab
        private void btnViewLog2_Click(object sender, EventArgs e)
        {
            var selectedConfig = comboConfigs.SelectedItem;
            if (selectedConfig == null)
                return;
            ViewLog(selectedConfig.ToString());
        }

        private async void ViewLog(string configName)
        {
            ConfigRunner runner;
            _configRunners.TryGetValue(configName, out runner);
            var logger = runner != null ? runner.GetLogger() : null;

            if (_currentAutoQcLogger != null && logger == _currentAutoQcLogger)
            {
                return;
            }

            foreach (var configRunner in _configRunners.Values)
            {
                configRunner.DisableUiLogging(); // Disable logging on all configurations first
            }

            textBoxLog.Clear(); // clear any existing log

            if (runner == null)
            {
                ShowWarningDialog(string.Empty, string.Format(Resources.MainForm_ViewLog_No_configuration_found_for_name___0__, configName));
                return;
            }

            if (logger == null)
            {
                ShowWarningDialog(string.Empty, Resources.MainForm_ViewLog_Log_for_this_configuration_is_not_yet_initialized_);
                return;
            }

            _currentAutoQcLogger = logger;
            runner.EnableUiLogging();

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
                ShowErrorWithExceptionDialog(Resources.MainForm_ViewLog_Error_Reading_Log, ex.Message, ex);
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
            var selectedConfig = comboConfigs.SelectedItem;
            if (selectedConfig == null)
                return;

            ConfigRunner runner;
            _configRunners.TryGetValue(selectedConfig.ToString(), out runner);
            if (runner == null)
                return;

            if (File.Exists(runner.GetLogger().GetFile()))
            {
                var arg = "/select, \"" + runner.GetLogger().GetFile() + "\"";
                Process.Start("explorer.exe", arg);
            }
            else if (Directory.Exists(runner.GetLogDirectory()))
            {
                Process.Start(runner.GetLogDirectory());
            }
            else
            {
                var err = string.Format(Resources.MainForm_btnOpenFolder_Click_Directory_does_not_exist___0_, runner.GetLogDirectory());
                ShowErrorDialog(Resources.MainForm_btnOpenFolder_Click_Directory_Not_Found, err);
            }

        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Settings.Default.Save();
            // TODO
//            foreach (var configRunner in _configRunners.Values)
//            {
//                configRunner.Stop();
//            }
        }

        #endregion


        #region Implementation of IMainUiControl

        public void ChangeConfigUiStatus(ConfigRunner configRunner)
        {
            RunUI(() =>
            {
                var lvi = listViewConfigs.FindItemWithText(configRunner.GetConfigName(), false, 0, false); // Do not allow partial match

                if (lvi == null) return;

                const int index = 3;
                lvi.SubItems[index].Text = configRunner.GetDisplayStatus();
                if (configRunner.IsRunning())
                {
                    lvi.SubItems[index].ForeColor = Color.Green;
                }
                else if (configRunner.IsDisconnected())
                {
                    lvi.SubItems[index].ForeColor = Color.Orange;
                }
                else if (configRunner.IsError())
                {
                    lvi.SubItems[index].ForeColor = Color.Red;
                    listViewConfigs.ItemChecked -= listViewConfigs_ItemChecked;
                    listViewConfigs.ItemCheck -= listViewConfigs_ItemCheck;
                    lvi.Checked = false;
                    ChangeConfigEnabledSetting(lvi.SubItems[0].Text, false);
                    listViewConfigs.ItemChecked += listViewConfigs_ItemChecked;
                    listViewConfigs.ItemCheck += listViewConfigs_ItemCheck;
                }
                else if (!configRunner.IsStopped())
                {
                    lvi.SubItems[index].ForeColor = Color.DarkOrange;
                }
                else
                {
                    lvi.SubItems[index].ForeColor = Color.Black;
                }
                if (!lvi.Selected)
                {
                    return;
                }
                UpdateButtons(configRunner);
            });
        }

        private void UpdateButtons(ConfigRunner configRunner)
        {
            if (configRunner == null)
            {
                btnCopy.Enabled = false;
                btnEdit.Text = Resources.MainForm_UpdateButtons_Edit;
                btnEdit.Enabled = false;
                btnDelete.Enabled = false;
            }
            else
            {
                btnCopy.Enabled = true;
                btnDelete.Enabled = true;
                btnEdit.Enabled = true;
                btnEdit.Text = configRunner.IsStopped() ? Resources.MainForm_UpdateButtons_Edit : Resources.MainForm_UpdateButtons_View;
            }
        }

        public void AddConfiguration(AutoQcConfig config)
        {
            AddConfiguration(config, -1);
        }

        public void AddConfiguration(AutoQcConfig config, int index)
        {
            Program.LogInfo(string.Format("Adding configuration \"{0}\"", config.Name));
            var lvi = new ListViewItem(config.Name);
            lvi.Checked = config.IsEnabled;
            lvi.UseItemStyleForSubItems = false; // So that we can change the color for sub-items.
            lvi.SubItems.Add(config.User);
            lvi.SubItems.Add(config.Created.ToShortDateString());
            lvi.SubItems.Add(ConfigRunner.RunnerStatus.Stopped.ToString());
            if (index == -1)
            {
                listViewConfigs.Items.Add(lvi);
            }
            else
            {
                listViewConfigs.Items.Insert(index, lvi);
            }

            comboConfigs.Items.Add(config.Name);

            // Add a ConfigRunner for this configuration
            var configRunner = new ConfigRunner(config, this);
            _configRunners.Add(config.Name, configRunner);

            var configList = Settings.Default.ConfigList;
            if (!configList.Contains(config))
            {
                configList.Add(config);
                Settings.Default.Save();
            }
            UpdateLabelVisibility();
        }

        private int RemoveConfiguration(AutoQcConfig config)
        {
            Program.LogInfo(string.Format("Removing configuration \"{0}\"", config.Name));
            var lvi = listViewConfigs.FindItemWithText(config.Name, false, 0, false);
            var lviIndex = lvi == null ? -1 : lvi.Index;
            if (lvi != null)
            {
                listViewConfigs.Items.Remove(lvi);
            }

            comboConfigs.Items.Remove(config.Name); // On the log tab

            ConfigRunner configRunner;
            _configRunners.TryGetValue(config.Name, out configRunner);
            if (configRunner != null)
            {
                configRunner.Stop();
            }
            _configRunners.Remove(config.Name);

            var configList = Settings.Default.ConfigList;
            configList.Remove(config);
            Settings.Default.Save();

            UpdateLabelVisibility();

            return lviIndex;
        }

        private void UpdateLabelVisibility()
        {
            if (_configRunners.Keys.Count > 0)
            {
                lblNoConfigs.Hide();
            }
            else
            {
                lblNoConfigs.Show();
            }
        }

        private void UpdateSettingsTab()
        {
            cb_minimizeToSysTray.Checked = Settings.Default.MinimizeToSystemTray;
            cb_keepRunning.Checked = Settings.Default.KeepAutoQcRunning;

            cb_minimizeToSysTray.CheckedChanged += cb_minimizeToSysTray_CheckedChanged;
            cb_keepRunning.CheckedChanged += cb_keepRunning_CheckedChanged;

            if (SkylineSettings.IsInitialized())
            {
                UpdateSkylineTypeAndInstallPathControls();
                Program.LogInfo(new StringBuilder("Skyline settings are: ").Append(SkylineSettings.GetSkylineSettingsStr()).ToString());
            }
            else
            {
                // If Skyline settings are not initialized (most likely because we could not find a valid Skyline installation at first startup)
                // show the "Settings" tab for the user to enter the details of the Skyline installation they want to use. 
                // If they try to switch to another tab before saving valid Skyline settings a warning will be displayed.
                tabMain.SelectedTab = tabSettings;
            }
        }

        public void UpdateConfiguration(AutoQcConfig oldConfig, AutoQcConfig newConfig)
        {
            var index = -1;
            if (_configRunners.ContainsKey(oldConfig.Name))
            {
                index = RemoveConfiguration(oldConfig);
            }
            AddConfiguration(newConfig, index);
        }

        public void UpdatePanoramaServerUrl(AutoQcConfig config)
        {
            var configList = Settings.Default.ConfigList;
            if (configList.Contains(config))
            {
                Settings.Default.Save();
            }
        }

        public AutoQcConfig GetConfig(string name)
        {
            ConfigRunner configRunner;
            _configRunners.TryGetValue(name, out configRunner);
            return configRunner == null ? null : configRunner.Config;
        }

        public void LogToUi(string text, bool scrollToEnd, bool trim)
        {
            RunUI(() =>
            {
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
                textBoxLog.ReadOnly = false; // Make text box editable. This is required for the following to work
                textBoxLog.SelectionStart = 0;
                textBoxLog.SelectionLength = textBoxLog.GetFirstCharIndexFromLine(numLines - AutoQcLogger.MAX_LOG_LINES);
                textBoxLog.SelectedText = string.Empty;

                var message = (_currentAutoQcLogger != null) ? 
                    string.Format(AutoQcLogger.LogTruncatedMessage, _currentAutoQcLogger.GetFile()) 
                    : "... Log truncated ...";
                textBoxLog.Text = textBoxLog.Text.Insert(0, message + Environment.NewLine);
                textBoxLog.SelectionStart = 0;
                textBoxLog.SelectionLength = textBoxLog.GetFirstCharIndexFromLine(1); // 0-based index
                textBoxLog.SelectionColor = Color.Red;

                textBoxLog.SelectionStart = textBoxLog.TextLength;
                textBoxLog.SelectionColor = textBoxLog.ForeColor;
                textBoxLog.ReadOnly = true; // Make text box read-only
            }
        }

        public void LogErrorToUi(string text, bool scrollToEnd, bool trim)
        {
            RunUI(() =>
            {
                if (trim)
                {
                    TrimDisplayedLog();
                }

                textBoxLog.SelectionStart = textBoxLog.TextLength;
                textBoxLog.SelectionLength = 0;
                textBoxLog.SelectionColor = Color.Red;
                LogToUi(text, scrollToEnd,
                    false); // Already trimmed
                textBoxLog.SelectionColor = textBoxLog.ForeColor;
            });
        }

        public void LogLinesToUi(List<string> lines)
        {
            RunUI(() =>
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
            RunUI(() =>
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

        public void DisplayError(string title, string message)
        {
            RunUI(() =>
            {
                ShowErrorDialog(title, message);
            }); 
        }

        #endregion

        private void RunUI(Action action)
        {
            if (InvokeRequired)
            {
                try
                {
                    Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                }
            }
            else
            {
                action();
            }
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

        private void cb_keepRunning_CheckedChanged(object sender, EventArgs e)
        {
            cb_keepRunning.Enabled = false;
            var enable = cb_keepRunning.Checked;
            try
            {
                ChangeKeepRunningState(enable);
            }
            catch (Exception ex)
            {
                var err = enable ? string.Format(Resources.MainForm_cb_keepRunning_CheckedChanged_Error_enabling__Keep__0__running_, Program.AppName)
                    : string.Format(Resources.MainForm_cb_keepRunning_CheckedChanged_Error_disabling__Keep__0__running_, Program.AppName);
                // ReSharper disable once LocalizableElement
                Program.LogError($"Error {(enable ? "enabling" : "disabling")} \"Keep AutoQC Loader running\"", ex);

                ShowErrorWithExceptionDialog(Resources.MainForm_cb_keepRunning_CheckedChanged_Error_Changing_Settings,
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

        private void ChangeKeepRunningState(bool enable)
        {
            if (enable)
            {
                StartupManager.EnableKeepRunning();
            }
            else
            {
                StartupManager.DisableKeepRunning();
            }
        }

        private void cb_minimizeToSysTray_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.MinimizeToSystemTray = cb_minimizeToSysTray.Checked;
            Settings.Default.Save();
        }

        private bool ApplyChangesToSkylineSettings()
        {
            if (!SkylineSettings.UpdateSettings(radioButtonUseSkyline.Checked, radioButtonWebBasedSkyline.Checked,
                textBoxSkylinePath.Text, out var errors))
            {
                ShowWarningDialog(
                    string.Format(Resources.MainForm_ApplyChangesToSkylineSettings_Cannot_Update__0__Settings,
                        SkylineSettings.Skyline), errors);
                UpdateSkylineTypeAndInstallPathControls(); // Reset controls to saved settings
                return false;
            }
            else
            {
                if (radioButtonWebBasedSkyline.Checked)
                {
                    textBoxSkylinePath.Text = string.Empty;
                }

                ShowInfoDialog(
                    string.Format(Resources.MainForm_ApplyChangesToSkylineSettings__0__Settings_Updated,
                        SkylineSettings.Skyline),
                    string.Format(
                        Resources.MainForm_ApplyChangesToSkylineSettings__0__settings_were_updated_successfully_,
                        SkylineSettings.Skyline));
            }

            return true;
        }

        private void buttonFileDialogSkylineInstall_click(object sender, EventArgs e)
        {
            using (var folderBrowserDlg = new FolderBrowserDialog())
            {
                folderBrowserDlg.Description =
                    string.Format(
                        Resources.MainForm_buttonFileDialogSkylineInstall_click_Select_the__0__installation_directory_,
                        SkylineSettings.Skyline);
                folderBrowserDlg.ShowNewFolderButton = false;
                folderBrowserDlg.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (folderBrowserDlg.ShowDialog() == DialogResult.OK)
                {
                    textBoxSkylinePath.Text = folderBrowserDlg.SelectedPath;
                }
            }
        }

        private void WebBasedInstall_Click(object sender, EventArgs e)
        {
            textBoxSkylinePath.Enabled = false;
            buttonFileDialogSkylineInstall.Enabled = false;
        }

        private void SpecifyInstall_Click(object sender, EventArgs e)
        {
            textBoxSkylinePath.Enabled = true;
            buttonFileDialogSkylineInstall.Enabled = true;
        }

        private void ApplySkylineSettings_Click(object sender, EventArgs e)
        {
            ApplyChangesToSkylineSettings();
        }

        private void TabMain_Deselecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage.Name.Equals("tabSettings"))
            {
                if (!SkylineSettings.IsInitialized())
                {
                    // Do not let the user switch to another tab without specifying a valid Skyline installation.
                    ShowErrorDialog(
                        string.Format(Resources.MainForm_TabMain_Deselecting__0__Settings_Not_Initialized,
                            SkylineSettings.Skyline),
                        string.Format(
                            Resources
                                .MainForm_TabMain_Deselecting_An_installation_of__0__or__1__is_required_to_use__2___Please_select__3__installation_details_to_continue_,
                            SkylineSettings.Skyline, SkylineSettings.SkylineDaily, Program.AppName,
                            SkylineSettings.Skyline));
                    e.Cancel = true;
                    return;
                }
                if (SkylineSettings.SettingsChanged(radioButtonUseSkyline.Checked, radioButtonWebBasedSkyline.Checked,
                    textBoxSkylinePath.Text))
                {
                    var result = ShowQuestionDialog(
                        string.Format(Resources.MainForm_TabMain_Deselecting_Unsaved__0__Settings,
                            SkylineSettings.Skyline),
                        string.Format(
                            Resources
                                .MainForm_TabMain_Deselecting__0__settings_have_not_been_saved__Would_you_like_to_save_them_,
                            SkylineSettings.Skyline));
                    if (result == DialogResult.Yes)
                    {
                        if (!ApplyChangesToSkylineSettings())
                        {
                            e.Cancel = true;
                        }
                    }
                    else
                    {
                        UpdateSkylineTypeAndInstallPathControls();
                    }
                }
            }
        }
    }

    //
    // Code from https://support.microsoft.com/en-us/kb/319401
    //
    class ListViewColumnSorter : IComparer
    {
        /// <summary>
        /// Specifies the column to be sorted
        /// </summary>
        private int _columnToSort;
        /// <summary>
        /// Specifies the order in which to sort (i.e. 'Ascending').
        /// </summary>
        private SortOrder _orderOfSort;
        /// <summary>
        /// Case insensitive comparer object
        /// </summary>
        private CaseInsensitiveComparer ObjectCompare;

        /// <summary>
        /// Class constructor.  Initializes various elements
        /// </summary>
        public ListViewColumnSorter()
        {
            // Initialize the column to '0'
            _columnToSort = 0;

            // Initialize the sort order to 'none'
            _orderOfSort = SortOrder.None;

            // Initialize the CaseInsensitiveComparer object
            ObjectCompare = new CaseInsensitiveComparer();
        }

        /// <summary>
        /// Gets or sets the number of the column to which to apply the sorting operation (Defaults to '0').
        /// </summary>
        public int SortColumn
        {
            set
            {
                _columnToSort = value;
            }
            get
            {
                return _columnToSort;
            }
        }

        /// <summary>
        /// Gets or sets the order of sorting to apply (for example, 'Ascending' or 'Descending').
        /// </summary>
        public SortOrder Order
        {
            set
            {
                _orderOfSort = value;
            }
            get
            {
                return _orderOfSort;
            }
        }

        #region Implementation of IComparer

        /// <summary>
        /// This method is inherited from the IComparer interface.  It compares the two objects passed using a case insensitive comparison.
        /// </summary>
        /// <param name="x">First object to be compared</param>
        /// <param name="y">Second object to be compared</param>
        /// <returns>The result of the comparison. "0" if equal, negative if 'x' is less than 'y' and positive if 'x' is greater than 'y'</returns>
        public int Compare(object x, object y)
        {
            ListViewItem listviewX, listviewY;

            // Cast the objects to be compared to ListViewItem objects
            listviewX = (ListViewItem)x;
            listviewY = (ListViewItem)y;

            // Compare the two items
            var compareResult = ObjectCompare.Compare(listviewX?.SubItems[_columnToSort].Text, listviewY?.SubItems[_columnToSort].Text);

            // Calculate correct return value based on object comparison
            switch (_orderOfSort)
            {
                case SortOrder.Ascending:
                    // Ascending sort is selected, return normal result of compare operation
                    return compareResult;
                case SortOrder.Descending:
                    // Descending sort is selected, return negative result of compare operation
                    return (-compareResult);
                default:
                    return 0;
            }
        }

        #endregion
    }

    public interface IMainUiControl
    {
        void ChangeConfigUiStatus(ConfigRunner configRunner);
        void AddConfiguration(AutoQcConfig config);
        void UpdateConfiguration(AutoQcConfig oldConfig, AutoQcConfig newConfig);
        void UpdatePanoramaServerUrl(AutoQcConfig config);
        AutoQcConfig GetConfig(string name);
        void LogToUi(string text, bool scrollToEnd = true, bool trim = true);
        void LogErrorToUi(string text, bool scrollToEnd = true, bool trim = true);
        void LogLinesToUi(List<string> lines);
        void LogErrorLinesToUi(List<string> lines);
        void DisplayError(string title, string message);
    }
}
