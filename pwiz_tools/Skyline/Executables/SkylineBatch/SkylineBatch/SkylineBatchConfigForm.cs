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
using System.Windows.Forms;

namespace SkylineBatch
{
    public partial class SkylineBatchConfigForm : Form
    {
        // User creates/modifies a config
        // takes a new configuration, or a copy of an existing configuration, and allows user to edit it and add it to the list
        // the passed in configuration IS NOT MODIFIED

        private readonly IMainUiControl _mainControl;
        private readonly SkylineBatchConfig _config;
        private readonly bool _isBusy;

        private ReportSettings newReportSettings;

        public SkylineBatchConfigForm(IMainUiControl mainControl) : this(SkylineBatchConfig.GetDefault(), mainControl, false)
        {
        }

        public SkylineBatchConfigForm(SkylineBatchConfig config, IMainUiControl mainControl, bool isBusy)
        {
            _mainControl = mainControl;
            _config = config;
            _isBusy = isBusy;
            newReportSettings = _config.ReportSettings.Copy();
            InitializeComponent();

            foreach (var report in newReportSettings.Reports)
                gridReportSettings.Rows.Add(report.AsArray());

            // Initialize file filter combobox

            
            textConfigName.Text = config.Name;
            textConfigName.TextChanged += textConfigName_TextChanged;

            SetUIMainSettings(_config.MainSettings);
            btnSaveConfig.Show();

            if (isBusy)
            {
                lblConfigRunning.Show();
                btnSaveConfig.Hide();
                btnCancelConfig.Text = "OK";
                DisableEverything();
            }
            else
            {
                lblConfigRunning.Hide();
            }

            ActiveControl = textConfigName;
        }

        public void DisableEverything()
        {
            textConfigName.ReadOnly = true;
            textSkylinePath.ReadOnly = true;
            textAnalysisPath.ReadOnly = true;
            textDataPath.ReadOnly = true;
            textNamingPattern.ReadOnly = true;
            btnAddReport.Enabled = false;
            btnAnalysisPath.Enabled = false;
            btnDataPath.Enabled = false;
            btnSkylineFilePath.Enabled = false;
        }

        private void Save()
        {
            SkylineBatchConfig newConfig;
            try
            {
                newConfig = GetConfigFromUi();
            }
            catch (ArgumentException e)
            {
                ShowErrorDialog(e.Message);
                return;
            }

            if (string.IsNullOrEmpty(_config.Name))
            {
                // If the original configuration that we started with does not have a name,
                // it means this is a brand new configuration
                newConfig.Created = DateTime.Now;
                newConfig.Modified = DateTime.Now;

                try
                {
                    _mainControl.AddConfiguration(newConfig);
                }
                catch (ArgumentException e)
                {
                    ShowErrorDialog(e.Message);
                    return;
                }    
            }

            else if (!newConfig.Equals(_config))
            {
                // If the original configuration has a name it means the user is editing an existing configuration
                // and some changes have been made.
                newConfig.Created = _config.Created;
                newConfig.Modified = DateTime.Now;

                try
                {
                    _mainControl.UpdateConfiguration(_config, newConfig);
                }
                catch (ArgumentException e)
                {
                    ShowErrorDialog(e.Message);
                    return;
                }
            }

            Close();      
        }

        private bool ValidateConfig(SkylineBatchConfig newConfig)
        {
            try
            {
                newConfig.Validate();
            }
            catch (ArgumentException e)
            {
                ShowErrorDialog(e.Message);
                return false;   
            }

            return true;
        }

        private void ShowErrorDialog(string message)
        {
            _mainControl.DisplayError("Configuration Validation Error", message);
        }

        private SkylineBatchConfig GetConfigFromUi()
        {
            SkylineBatchConfig config  = new SkylineBatchConfig();
            config.Name = textConfigName.Text;
            config.MainSettings = GetMainSettingsFromUI();
            config.ReportSettings = newReportSettings;
            return config;
        }

        private void SetUIMainSettings(MainSettings mainSettings)
        {
            RunUI(() =>
            {
                textSkylinePath.Text = mainSettings.TemplateFilePath;
                textAnalysisPath.Text = mainSettings.AnalysisFolderPath;
                textDataPath.Text = mainSettings.DataFolderPath;
                textNamingPattern.Text = mainSettings.ReplicateNamingPattern;
            });
        }

        private MainSettings GetMainSettingsFromUI()
        {
            var mainSettings = new MainSettings();
            mainSettings.TemplateFilePath = textSkylinePath.Text;
            mainSettings.AnalysisFolderPath = textAnalysisPath.Text;
            mainSettings.DataFolderPath = textDataPath.Text;
            mainSettings.ReplicateNamingPattern = textNamingPattern.Text;
            return mainSettings;
        }

        private int ValidateIntTextField(string textToParse, string fieldName)
        {
            int parsedInt;
            if (!Int32.TryParse(textToParse, out parsedInt))
            {
                throw new ArgumentException(string.Format("Invalid value for \"{0}\": {1}.", fieldName, textToParse));
            }
            return parsedInt;
        }

        

        public void RunUI(Action action)
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

        private void OpenFolder(TextBox textbox)
        {
            var dialog = new FolderBrowserDialog();
            dialog.SelectedPath = textbox.Text;
            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                textbox.Text = dialog.SelectedPath;
            }
        }

        private void btnAnalysisFilePath_Click(object sender, EventArgs e)
        {
            OpenFolder(textAnalysisPath);
        }

        private void btnDataPath_Click(object sender, EventArgs e)
        {
            OpenFolder(textDataPath);
        }

        #region [UI event handlers]



        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            Save();
        }

        private void btnOkConfig_Click(object sender, EventArgs e)
        {
            this.Close();
        }




        #endregion

        private void btnSkylineFilePath_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = "SKY|*.sky";
            openDialog.Title = "Open Skyline File";
            openDialog.ShowDialog();
            textSkylinePath.Text = openDialog.FileName;
        }

        private void textConfigName_TextChanged(object sender, EventArgs e)
        {

            if (!string.IsNullOrEmpty(textAnalysisPath.Text))
            {
                var parentPath = Path.GetDirectoryName(textAnalysisPath.Text);
                textAnalysisPath.Text = parentPath + "\\" + textConfigName.Text;
            }
                
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.regular-expressions.info/reference.html");
        }

        private void btnAddReport_Click(object sender, EventArgs e)
        {
            Program.LogInfo("Creating new report");
            AddReport();
        }

        private void AddReport()
        {
            var addingReport = new ReportInfo();
            var addReportsForm = new ReportsAddForm(addingReport);
            addReportsForm.StartPosition = FormStartPosition.CenterParent;
            addReportsForm.ShowDialog();

            if (!addingReport.Empty())
            {
                newReportSettings.Add(addingReport);
                gridReportSettings.Rows.Add(addingReport.AsArray());
            }
        }

        private void btnEditReport_Click(object sender, EventArgs e)
        {
            Program.LogInfo("Editing report");
            var indexSelected = gridReportSettings.SelectedRows[0].Index;
            if (indexSelected == newReportSettings.Reports.Count)
            {
                AddReport();
                return;
            }
            //gridReportSettings.SelectedRows.Clear();
            var editingReport = newReportSettings.Reports[indexSelected].Copy();
            var addReportsForm = new ReportsAddForm(editingReport);
            addReportsForm.StartPosition = FormStartPosition.CenterParent;
            addReportsForm.ShowDialog();

            if (!editingReport.Empty())
            {
                newReportSettings.Reports[indexSelected] = editingReport;
                gridReportSettings.Rows.RemoveAt(indexSelected);
                gridReportSettings.Rows.Insert(indexSelected, editingReport.AsArray());
            }
        }

        private void btnDeleteReport_Click(object sender, EventArgs e)
        {
            var indexToDelete = gridReportSettings.SelectedRows[0].Index;
            newReportSettings.Reports.RemoveAt(indexToDelete);
            gridReportSettings.Rows.RemoveAt(indexToDelete);

        }

        private void gridReportSettings_SelectionChanged(object sender, EventArgs e)
        {
            if (_isBusy)
                gridReportSettings.ClearSelection();
            var selectedRows = gridReportSettings.SelectedRows;
            btnEditReport.Enabled = selectedRows.Count > 0;
            btnDeleteReport.Enabled = selectedRows.Count > 0 && selectedRows[0].Index < newReportSettings.Reports.Count;
            
        }
    }
}
