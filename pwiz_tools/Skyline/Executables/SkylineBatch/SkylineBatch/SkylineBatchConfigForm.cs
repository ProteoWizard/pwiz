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
using System.IO;
using System.Windows.Forms;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class SkylineBatchConfigForm : Form
    {
        // User creates new config or modifies existing config
        // takes a new configuration, or a copy of an existing configuration, and allows user to edit it and add it to the list
        // the passed in configuration is not modified

        private readonly IMainUiControl _mainControl;
        private readonly SkylineBatchConfig _config;
        private readonly bool _isBusy;

        private readonly ReportSettings _newReportSettings;
        

        public SkylineBatchConfigForm(SkylineBatchConfig config, IMainUiControl mainControl, bool isBusy)
        {
            _mainControl = mainControl;
            _config = config;
            _isBusy = isBusy;
            _newReportSettings = _config.ReportSettings.Copy();
            InitializeComponent();

            foreach (var report in _newReportSettings.Reports)
                gridReportSettings.Rows.Add(report.AsArray());
            
            textConfigName.Text = config.Name;
            textConfigName.TextChanged += textConfigName_TextChanged;

            SetUiMainSettings(_config.MainSettings);
            btnSaveConfig.Show();

            if (isBusy)
            {
                // configuration is running and cannot be edited
                lblConfigRunning.Show();
                DisableEverything();
                // save and cancel buttons are replaced with OK button
                btnSaveConfig.Hide(); 
                btnCancelConfig.Text = @"OK";
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



        #region Edit main settings

        private void SetUiMainSettings(MainSettings mainSettings)
        {
            textSkylinePath.Text = mainSettings.TemplateFilePath;
            textAnalysisPath.Text = mainSettings.AnalysisFolderPath;
            textDataPath.Text = mainSettings.DataFolderPath;
            textNamingPattern.Text = mainSettings.ReplicateNamingPattern;
        }

        private MainSettings GetMainSettingsFromUi()
        {
            var mainSettings = new MainSettings();
            mainSettings.TemplateFilePath = textSkylinePath.Text;
            mainSettings.AnalysisFolderPath = textAnalysisPath.Text;
            mainSettings.DataFolderPath = textDataPath.Text;
            mainSettings.ReplicateNamingPattern = textNamingPattern.Text;
            return mainSettings;
        }

        private void textConfigName_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(textAnalysisPath.Text))
            {
                var parentPath = Path.GetDirectoryName(textAnalysisPath.Text);
                textAnalysisPath.Text = Path.Combine(parentPath ?? string.Empty, textConfigName.Text);
            }

        }

        private void btnSkylineFilePath_Click(object sender, EventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = Resources.Sky_file_extension;
            openDialog.Title = Resources.Open_skyline_file;
            openDialog.ShowDialog();
            textSkylinePath.Text = openDialog.FileName;
        }

        private void btnAnalysisFilePath_Click(object sender, EventArgs e)
        {
            OpenFolder(textAnalysisPath);
        }

        private void btnDataPath_Click(object sender, EventArgs e)
        {
            OpenFolder(textDataPath);
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

        private void linkLabelRegex_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.regular-expressions.info/reference.html");
        }

        #endregion


        #region Reports

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
                _newReportSettings.Add(addingReport);
                gridReportSettings.Rows.Add(addingReport.AsArray());
            }
        }

        private void btnEditReport_Click(object sender, EventArgs e)
        {
            Program.LogInfo("Editing report");
            var indexSelected = gridReportSettings.SelectedRows[0].Index;
            if (indexSelected == _newReportSettings.Reports.Count)
            {
                AddReport();
                return;
            }
            var editingReport = _newReportSettings.Reports[indexSelected].Copy();
            var addReportsForm = new ReportsAddForm(editingReport);
            addReportsForm.StartPosition = FormStartPosition.CenterParent;
            addReportsForm.ShowDialog();

            if (!editingReport.Empty())
            {
                _newReportSettings.Reports[indexSelected] = editingReport;
                gridReportSettings.Rows.RemoveAt(indexSelected);
                gridReportSettings.Rows.Insert(indexSelected, editingReport.AsArray());
            }
        }

        private void btnDeleteReport_Click(object sender, EventArgs e)
        {
            var indexToDelete = gridReportSettings.SelectedRows[0].Index;
            _newReportSettings.Reports.RemoveAt(indexToDelete);
            gridReportSettings.Rows.RemoveAt(indexToDelete);

        }

        private void gridReportSettings_SelectionChanged(object sender, EventArgs e)
        {
            if (_isBusy)
                gridReportSettings.ClearSelection();
            var selectedRows = gridReportSettings.SelectedRows;
            btnEditReport.Enabled = selectedRows.Count > 0;
            btnDeleteReport.Enabled = selectedRows.Count > 0 && selectedRows[0].Index < _newReportSettings.Reports.Count;

        }

        #endregion


        #region Save config
        
        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            Save();
        }

        private SkylineBatchConfig GetConfigFromUi()
        {
            SkylineBatchConfig config = new SkylineBatchConfig();
            config.Name = textConfigName.Text;
            config.MainSettings = GetMainSettingsFromUi();
            config.ReportSettings = _newReportSettings;
            return config;
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
                    _mainControl.EditConfiguration(_config, newConfig);
                }
                catch (ArgumentException e)
                {
                    ShowErrorDialog(e.Message);
                    return;
                }
            }

            Close();
        }

        private void ShowErrorDialog(string message)
        {
            _mainControl.DisplayError("Configuration Validation Error", message);
        }

        #endregion







    }
}
