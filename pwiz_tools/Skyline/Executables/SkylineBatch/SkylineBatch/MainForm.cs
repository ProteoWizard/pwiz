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
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SkylineBatch
{
    public partial class MainForm : Form, IMainUiControl
    {
        //private Dictionary<string, ConfigRunner> _configRunners;
        

        private ConfigManager configManager;
        
        private bool _loaded;

        private ISkylineBatchLogger _skylineBatchLogger;
        

        public MainForm()
        {
            // TODO generalize skyline file
            var skylineFileDir = Path.GetDirectoryName(Directory.GetCurrentDirectory()); //Config.MainSettings.SkylineFileDir;
            var logFile = Path.Combine(skylineFileDir, "SkylineBatch.log");
            _skylineBatchLogger = new SkylineBatchLogger(logFile);
            //((SkylineBatchLogger) _skylineBatchLogger).Init();
            InitializeComponent();
            

            btnUpArrow.Text = char.ConvertFromUtf32(0x2BC5);
            btnDownArrow.Text = char.ConvertFromUtf32(0x2BC6);
            btnRunOptions.Text = char.ConvertFromUtf32(0x2BC6);

            btnCopy.Enabled = false;
            btnDelete.Enabled = false;
            btnEdit.Enabled = false;
            btnUpArrow.Enabled = false;
            btnDownArrow.Enabled = false;
            btnCancel.Enabled = false;


            Program.LogInfo("Loading configurations from saved settings.");
            configManager = new ConfigManager(_skylineBatchLogger, this);
            UpdateUiConfigurations();

            UpdateLabelVisibility();

            RunUI(ViewLog); // start log so no weird scroll
        }


        private ConfigRunner GetSelectedConfigRunner()
        {
            if (listViewConfigs.SelectedItems.Count == 0)
                return null;

            return configManager.GetConfigRunnerAtIndex(listViewConfigs.SelectedIndices[0]);
            
        }

        private static void ShowConfigForm(SkylineBatchConfigForm configForm)
        {
            configForm.StartPosition = FormStartPosition.CenterParent;
            configForm.ShowDialog();
        }

       

        private void ShowErrorDialog(string title, string message)
        {
            MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ShowWarningDialog(string title, string message)
        {
            MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ShowInfoDialog(string title, string message)
        {
            MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }


        #region event handlers

        private void btnNewConfig_Click(object sender, EventArgs e)
        {
            var configForm = new SkylineBatchConfigForm(configManager.CreateConfiguration(), this, false);
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

            Program.LogInfo(string.Format("{0} configuration \"{1}\"",
                (!configRunner.IsRunning() ? "Editing" : "Viewing"),
                configRunner.GetConfigName()));

            var configForm = new SkylineBatchConfigForm(configRunner.Config, this, configRunner.IsBusy());
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

            var configForm = new SkylineBatchConfigForm(configManager.MakeNoNameCopy(configRunner.Config), this, false);
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
            
            if (configRunner.IsBusy())
            {
                string message = null;
                if (configRunner.IsRunning())
                {
                    message =
                        string.Format(
                            @"Configuration ""{0}"" is running. Please stop the configuration and try again. ",
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

            // remove config
            Program.LogInfo(string.Format("Removing configuration \"{0}\"", configRunner.Config.Name));
            configManager.Remove(configRunner.Config);
            UpdateUiConfigurations();

            UpdateLabelVisibility();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {

            var dialog = new SaveFileDialog { Title = "Save configurations...", Filter = "XML Files(*.xml)|*.xml" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            configManager.ExportAll(dialog.FileName);
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "XML Files(*.xml)|*.xml";
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var filePath = dialog.FileName;
            var importMessage = configManager.Import(filePath);
           
            UpdateUiConfigurations();

            if (!string.IsNullOrEmpty(importMessage))
                MessageBox.Show(importMessage, "Import Configurations", MessageBoxButtons.OK);
        }

        

        // Event triggered by button on the main tab that lists all configurations
        private void btnViewLog1_Click(object sender, EventArgs e)
        {
            ViewLog();
            tabMain.SelectTab(tabLog);
        }

        private async void ViewLog()
        {
            _skylineBatchLogger.LogToUi(this);

            try
            {
                await Task.Run(() =>
                {
                    // Read the log contents and display in the log tab.
                    _skylineBatchLogger.DisplayLog();
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

       

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            configManager.CloseConfigs();
        }

        #endregion


        #region Implementation of IMainUiControl

       

        public void AddConfiguration(SkylineBatchConfig config)
        {
            configManager.AddConfiguration(config);
            UpdateUiConfigurations();
        }


        public void UpdateUiConfigurations()
        {
            RunUI(() =>
            {
                Program.LogInfo("Updating configurations");
                listViewConfigs.Items.Clear();
                foreach (var config in configManager.ConfigList)
                {
                    /*if (!_configRunners.ContainsKey(config.Name))
                    {
                        var configRunner = new ConfigRunner(config, this, _skylineBatchLogger);
                        _configRunners.Add(config.Name, configRunner);
                    }
                    var runner = _configRunners[config.Name];*/

                    var lvi = new ListViewItem(config.Name);
                    lvi.UseItemStyleForSubItems = false; // So that we can change the color for sub-items.
                    lvi.SubItems.Add(config.Created.ToShortDateString());
                    lvi.SubItems.Add(configManager.GetDisplayStatus(config));
                    listViewConfigs.Items.Add(lvi);
                }

                UpdateLabelVisibility();
            });

        }

        private void RemoveConfiguration(SkylineBatchConfig config)
        {
            Program.LogInfo(string.Format("Removing configuration \"{0}\"", config.Name));
            configManager.Remove(config);
            UpdateUiConfigurations();

            UpdateLabelVisibility();
            
        }

        private void UpdateLabelVisibility()
        {
            if (configManager.HasConfigs())
            {
                lblNoConfigs.Hide();
            }
            else
            {
                lblNoConfigs.Show();
            }
        }

        public void UpdateConfiguration(SkylineBatchConfig oldConfig, SkylineBatchConfig newConfig)
        {
            configManager.ReplaceConfig(oldConfig, newConfig);
            UpdateUiConfigurations();
        }


        public SkylineBatchConfig GetConfig(string name)
        {
            return configManager.GetConfig(name);
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
            const int buffer = SkylineBatchLogger.MaxLogLines / 10;
            if (numLines > SkylineBatchLogger.MaxLogLines + buffer)
            {
                textBoxLog.ReadOnly = false; // Make text box editable. This is required for the following to work
                textBoxLog.SelectionStart = 0;
                textBoxLog.SelectionLength =
                    textBoxLog.GetFirstCharIndexFromLine(numLines - SkylineBatchLogger.MaxLogLines);
                textBoxLog.SelectedText = string.Empty;

                var message = (_skylineBatchLogger != null)
                    ? string.Format(SkylineBatchLogger.LogTruncatedMessage, _skylineBatchLogger.GetFile())
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
            RunUI(() => { ShowErrorDialog(title, message); });
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
                    return (T) Invoke(function);
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
            if (WindowState == FormWindowState.Minimized && Properties.Settings.Default.MinimizeToSystemTray)
            {
                Hide();
                systray_icon.Visible = true;
            }
        }
        



        private void buttonFileDialogSkylineInstall_click(object sender, EventArgs e)
        {
            using (var folderBrowserDlg = new FolderBrowserDialog())
            {
                folderBrowserDlg.Description = "Select the Skyline installation directory.";
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

        public void UpdateRunningButtons(bool isRunning)
        {
            RunUI(() =>
            {
                btnRunBatch.Enabled = !isRunning;
                btnRunOptions.Enabled = btnRunBatch.Enabled;
                btnCancel.Enabled = isRunning;
            });
        }

        private void btnRunBatch_Click(object sender, EventArgs e)
        {
            for (int i = 1; i <= batchRunDropDown.Items.Count; i++)
            {
                if (((ToolStripMenuItem) batchRunDropDown.Items[i - 1]).Checked)
                {
                    configManager.RunAll(i);
                    break;
                }
            }
            if (configManager.HasConfigs())
                btnCancel.Enabled = true;

        }

        private void btnRunOptions_Click(object sender, EventArgs e)
        {
            batchRunDropDown.Show(btnRunBatch, new Point(0, btnRunBatch.Height));
            
        }

        private void batchRunDropDown_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            int startStep = 0;
            for (int i = 0; i < batchRunDropDown.Items.Count; i++)
            {
                if (batchRunDropDown.Items[i].Text == e.ClickedItem.Text)
                    startStep = i + 1;
                ((ToolStripMenuItem)batchRunDropDown.Items[i]).Checked = false;
            }
            ((ToolStripMenuItem)batchRunDropDown.Items[startStep - 1]).Checked = true;
            btnRunBatch.Text = "&" + e.ClickedItem.Text;
            configManager.RunAll(startStep);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            configManager.CancelRunners();
            btnCancel.Enabled = false;
        }

        private void listViewConfigs_SelectedIndexChanged(object sender, EventArgs e)
        {
            // update buttons enabled
            bool oneSelected = listViewConfigs.SelectedItems.Count == 1;
            int selectedIndex = oneSelected ? listViewConfigs.SelectedIndices[0] : -1;
            btnEdit.Enabled = oneSelected;
            btnCopy.Enabled = oneSelected;
            btnDelete.Enabled = oneSelected;
            btnUpArrow.Enabled = oneSelected && selectedIndex > 0;
            btnDownArrow.Enabled = oneSelected && selectedIndex < listViewConfigs.Items.Count - 1;
        }


        private void MoveConfiguration(int currentIndex, int newIndex)
        {
            configManager.MoveConfig(currentIndex, newIndex);
            UpdateUiConfigurations();
            listViewConfigs.Items[newIndex].Selected = true;
        }

        private void btnUpArrow_Click(object sender, EventArgs e)
        {
            var selected = listViewConfigs.SelectedIndices[0];
            MoveConfiguration(selected, selected - 1);
        }

        private void btnDownArrow_Click(object sender, EventArgs e)
        {
            var selected = listViewConfigs.SelectedIndices[0];
            MoveConfiguration(selected, selected + 1);
        }

        private void tabLog_Enter(object sender, EventArgs e)
        {
            //ViewLog();
        }
    }



    public interface IMainUiControl
    {
        //void ChangeConfigUiStatus(ConfigRunner configRunner);
        void AddConfiguration(SkylineBatchConfig config);
        void UpdateConfiguration(SkylineBatchConfig oldConfig, SkylineBatchConfig newConfig);
        void UpdateUiConfigurations();
        void UpdateRunningButtons(bool isRunning);
        //void UiChangedEvent(object sender, EventArgs e);
        SkylineBatchConfig GetConfig(string name);
        void LogToUi(string text, bool scrollToEnd = true, bool trim = true);
        void LogErrorToUi(string text, bool scrollToEnd = true, bool trim = true);
        void LogLinesToUi(List<string> lines);
        void LogErrorLinesToUi(List<string> lines);
        void DisplayError(string title, string message);
    }
}
