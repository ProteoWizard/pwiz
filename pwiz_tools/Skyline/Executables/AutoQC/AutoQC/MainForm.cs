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

        private readonly ListViewColumnSorter columnSorter;

        // Flag that gets set to true in the "Shown" event handler. 
        // ItemCheck and ItemChecked events on the listview are ignored until then.
        private bool _loaded;

        public const bool IS_DAILY = false;
        public const string SKYLINE_RUNNER = IS_DAILY ? "SkylineDailyRunner.exe" : "SkylineRunner.exe";

        // Path to SkylineRunner.exe / SkylineDailyRunner.exe
        // Expect SkylineRunner to be in the same directory as AutoQC
        public static readonly string SkylineRunnerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            SKYLINE_RUNNER);

        private IAutoQcLogger _currentAutoQcLogger;

        public MainForm()
        {
            InitializeComponent();

            columnSorter = new ListViewColumnSorter();
            listViewConfigs.ListViewItemSorter = columnSorter;

            btnCopy.Enabled = false;
            btnDelete.Enabled = false;
            btnEdit.Enabled = false;

            ReadSavedConfigurations();

            UpdateLabelVisibility();

            Shown += ((sender, args) =>
            {
                _loaded = true;
                RunEnabledConfigurations();
            });
        }

        private void ReadSavedConfigurations()
        {
            Program.LogInfo("Reading configurations from saved settings.");
            var configList = Settings.Default.ConfigList;
            var sortedConfig = configList.OrderByDescending(c => c.Created);
            _configRunners = new Dictionary<string, ConfigRunner>();
            foreach (var config in sortedConfig)
            {
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
                var title = string.Format("Error Starting Configuration \"{0}\"", configRunner.Config.Name);
                var msg = string.Format("{0}\n\nMore details can be found in the program log: {1}", e.Message, Program.GetProgramLogFilePath());
                ShowErrorDialog(title, msg);
                Program.LogError(title, e);
            }
        }

        private void ChangeConfigState(AutoQcConfig config)
        {
            ConfigRunner configRunner;
            _configRunners.TryGetValue(config.Name, out configRunner);
            if (configRunner == null)
            {
                return;
            }
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
            MessageBox.Show(this, message, title, MessageBoxButtons.OK);    
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
                            @"Configuration ""{0}"" is running. Please stop the configuration and try again. ",
                            configRunner.GetConfigName());
                }
                else if (configRunner.IsStopping())
                {
                    message =
                        string.Format(
                            @"Please wait for the configuration ""{0}"" to stop and try again.",
                            configRunner.GetConfigName());
                }
                MessageBox.Show(message,
                    "Cannot Delete",
                    MessageBoxButtons.OK);
                return;
            }
            var doDelete =
                MessageBox.Show(
                    string.Format(@"Are you sure you want to delete configuration ""{0}""?",
                        configRunner.GetConfigName()),
                    "Confirm Delete",
                    MessageBoxButtons.YesNo);

            if (doDelete != DialogResult.Yes) return;

            RemoveConfiguration(configRunner.Config);
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            Settings.Default.Save();

            var dialog = new SaveFileDialog { Title = "Save configurations...", Filter = "XML Files(*.xml)|*.xml" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var filePath = dialog.FileName;
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            config.SaveAs(filePath);
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "XML Files(*.xml)|*.xml";
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var filePath = dialog.FileName;

            List<AutoQcConfig> readConfigs = new List<AutoQcConfig>();
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    using (var reader = XmlReader.Create(stream))
                    {
                        while (reader.IsStartElement())
                        {  
                            if (reader.Name == "autoqc_config")
                            {
                                AutoQcConfig config = AutoQcConfig.Deserialize(reader);
                                readConfigs.Add(config);
                            }
                            reader.Read();
                        }
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show(string.Format("Could not import configurations from file {0}", filePath),
                    "Import Configurations Error",
                    MessageBoxButtons.OK);
                return;
            }

            if (readConfigs.Count == 0)
            {
                MessageBox.Show(string.Format("Could not import configurations from file {0}", filePath),
                    "Import Configurations",
                    MessageBoxButtons.OK);  
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

            var message = new StringBuilder("Number of configurations imported: ");
            message.Append(numAdded).Append(Environment.NewLine);
            if (duplicateConfigs.Count > 0)
            {
                message.Append("The following configurations already exist and were not imported:")
                    .Append(Environment.NewLine);
                foreach (var name in duplicateConfigs)
                {
                    message.Append("\"").Append(name).Append("\"").Append(Environment.NewLine);
                }
            }
            if (validationErrors.Count > 0)
            {
                message.Append("The following configurations could not be validated and were not imported:")
                    .Append(Environment.NewLine);
                foreach (var error in validationErrors)
                {
                    message.Append(error).Append(Environment.NewLine);
                }
            }
            MessageBox.Show(message.ToString(), "Import Configurations", MessageBoxButtons.OK);
            
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
                var message = string.Format("Configuration is {0}. Please wait.",
                    configRunner.IsStarting() ? "starting" : "stopping");

                MessageBox.Show(message,
                    "Please Wait",
                    MessageBoxButtons.OK);
                return;
            }

            if (e.NewValue == CheckState.Checked) return;

            var doChange =
                MessageBox.Show(
                    string.Format(@"Are you sure you want to stop configuration ""{0}""?", configRunner.GetConfigName()),
                    "Confirm Stop",
                    MessageBoxButtons.YesNo);

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
                ChangeConfigState(configRunner.Config);
            }
        }

        private ConfigRunner ChangeConfigEnabledSetting(string configName, bool enabled)
        {
            ConfigRunner configRunner;
            _configRunners.TryGetValue(configName, out configRunner);
            if (configRunner == null)
                return null;
            configRunner.Config.IsEnabled = enabled;
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
            if (e.Column == columnSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (columnSorter.Order == SortOrder.Ascending)
                {
                    columnSorter.Order = SortOrder.Descending;
                }
                else
                {
                    columnSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                columnSorter.SortColumn = e.Column;
                columnSorter.Order = SortOrder.Ascending;
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
                MessageBox.Show(string.Format("No configuration found for name \"{0}\"", configName), "",
                    MessageBoxButtons.OK);
                return;
            }
            
            if (logger == null)
            {
                MessageBox.Show("Log for this configuration is not yet initialized.", "",
                    MessageBoxButtons.OK);
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
                ShowErrorDialog("Error Reading Log", ex.Message);
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
            else if(Directory.Exists(runner.GetLogDirectory()))
            {
                Process.Start(runner.GetLogDirectory());
            }
            else
            {
                var err = string.Format("Directory does not exist: {0}", runner.GetLogDirectory());
                ShowErrorDialog("Directory Not Found", err);
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
                btnEdit.Text = "Edit";
                btnEdit.Enabled = false;
                btnDelete.Enabled = false;
            }
            else
            {
                btnCopy.Enabled = true;
                btnDelete.Enabled = true;
                btnEdit.Enabled = true;
                btnEdit.Text = configRunner.IsStopped() ? "Edit" : "View";
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
            var lvi = listViewConfigs.FindItemWithText(config.Name);
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
            const int buffer = AutoQcLogger.MaxLogLines / 10;
            if (numLines > AutoQcLogger.MaxLogLines + buffer)
            {
                textBoxLog.ReadOnly = false; // Make text box editable. This is required for the following to work
                textBoxLog.SelectionStart = 0;
                textBoxLog.SelectionLength = textBoxLog.GetFirstCharIndexFromLine(numLines - AutoQcLogger.MaxLogLines);
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
                if (trim )
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

        private T RunUI<T>(Func<T> function)
        {
            if (InvokeRequired)
            {
                try
                {
                    return (T)Invoke(function);
                }
                catch (ObjectDisposedException)
                {
                }
            }
            else
            {
                return function();
            }
            return default(T);
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
        private int ColumnToSort;
        /// <summary>
        /// Specifies the order in which to sort (i.e. 'Ascending').
        /// </summary>
        private SortOrder OrderOfSort;
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
            ColumnToSort = 0;

            // Initialize the sort order to 'none'
            OrderOfSort = SortOrder.None;

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
                ColumnToSort = value;
            }
            get
            {
                return ColumnToSort;
            }
        }

        /// <summary>
        /// Gets or sets the order of sorting to apply (for example, 'Ascending' or 'Descending').
        /// </summary>
        public SortOrder Order
        {
            set
            {
                OrderOfSort = value;
            }
            get
            {
                return OrderOfSort;
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
            var compareResult = ObjectCompare.Compare(listviewX.SubItems[ColumnToSort].Text, listviewY.SubItems[ColumnToSort].Text);

            // Calculate correct return value based on object comparison
            switch (OrderOfSort)
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
